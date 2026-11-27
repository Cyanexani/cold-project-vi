// VI hardware database: one way in, nothing out.
//
// Two routes take data in, none gives it out: POST /v1/hw (hardware, below) and POST /v1/report (a
// bug report's zip the player chose to send from the launcher, see report()).
//
// POST /v1/hw It takes the launcher's hardware report (the exact fields below,
// nothing else), keeps one row per ID, and answers with a bare status code. There is no route that
// reads anything: the database is read only by edupode, in the Cloudflare dashboard or with the
// separate admin Worker behind Cloudflare Access (admin.js).
//
// A report for an ID that exists is taken only with the secret its first report came with (the
// database keeps the secret's SHA-256), so knowing someone's ID is not enough to change their row.
// No IP address is stored; the rate limit keeps per-IP counts as a salted hash for one hour only.

const FIELDS = {
  schema: 'number', id: 'string', gpu: 'string', gpu_vendor: 'string', gpu_driver: 'string',
  vram_mb: 'number', rebar_bios: 'boolean', rebar_d3d12: 'nullable-boolean', cpu: 'string',
  cpu_cores: 'number', cpu_threads: 'number', ram_gb: 'number', board_maker: 'string', board: 'string',
  os: 'string', disk: 'string', laptop: 'boolean',
};
const ID = /^VI-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$/;
const MAX_BODY = 2048;
const PER_HOUR = 20;

async function sha256(text) {
  const d = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  return [...new Uint8Array(d)].map(b => b.toString(16).padStart(2, '0')).join('');
}

function valid(r) {
  if (typeof r !== 'object' || r === null || Array.isArray(r)) return false;
  for (const k of Object.keys(r)) if (!(k in FIELDS)) return false;
  for (const [k, t] of Object.entries(FIELDS)) {
    const v = r[k];
    if (t === 'string' && (typeof v !== 'string' || v.length > 200)) return false;
    if (t === 'number' && (typeof v !== 'number' || !Number.isFinite(v) || v < 0 || v > 1e7)) return false;
    if (t === 'boolean' && typeof v !== 'boolean') return false;
    if (t === 'nullable-boolean' && !(v === null || typeof v === 'boolean')) return false;
  }
  return r.schema === 1 && ID.test(r.id);
}

// Same length and every byte compared: how long it takes says nothing about the token.
function sameToken(a, b) {
  if (typeof a !== 'string' || typeof b !== 'string' || a.length !== b.length || a.length < 32) return false;
  let d = 0;
  for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return d === 0;
}

// The averages, never a row: GET /v1/stats with `Authorization: Bearer <STATS_TOKEN>` (a secret only
// the GitHub workflow that publishes them holds, .github/workflows/hardware-stats.yml). No ID, no
// single PC: groups of fewer than 3 PCs are folded into "other".
async function stats(env) {
  const rows = (await env.DB.prepare('SELECT report, last_seen FROM hardware').all()).results.map(r => ({ ...JSON.parse(r.report), last_seen: r.last_seen }));
  const n = rows.length;
  const avg = f => n ? Math.round(rows.reduce((s, r) => s + (Number(r[f]) || 0), 0) / n * 10) / 10 : 0;
  const share = f => n ? Math.round(rows.filter(f).length / n * 1000) / 10 : 0;
  const top = (key, extra) => {
    const groups = new Map();
    for (const r of rows) {
      const k = key(r) || 'unknown';
      if (!groups.has(k)) groups.set(k, []);
      groups.get(k).push(r);
    }
    const out = [];
    let other = 0;
    for (const [k, list] of [...groups.entries()].sort((a, b) => b[1].length - a[1].length)) {
      if (list.length < 3) { other += list.length; continue; }
      out.push({ name: k, pcs: list.length, ...(extra ? extra(list) : {}) });
    }
    if (other) out.push({ name: 'other', pcs: other });
    return out;
  };
  const mean = (list, f) => Math.round(list.reduce((s, r) => s + (Number(r[f]) || 0), 0) / list.length);
  const month = Date.now() - 30 * 86400000;
  return {
    updated: new Date().toISOString(),
    pcs: n,
    active_30_days: rows.filter(r => Date.parse(r.last_seen + 'Z') > month).length,
    average: { ram_gb: avg('ram_gb'), vram_gb: Math.round(avg('vram_mb') / 1024 * 10) / 10, cpu_threads: avg('cpu_threads') },
    percent: {
      rebar_in_bios: share(r => r.rebar_bios),
      rebar_for_the_game: share(r => r.rebar_d3d12 === true),
      laptops: share(r => r.laptop),
      ssd: share(r => r.disk === 'SSD'),
      ram_16_gb_or_less: share(r => r.ram_gb <= 16),
      vram_8_gb_or_less: share(r => r.vram_mb <= 8 * 1024),
    },
    gpu_vendors: top(r => r.gpu_vendor),
    gpus: top(r => r.gpu, list => ({ vram_gb: Math.round(mean(list, 'vram_mb') / 1024), rebar_percent: Math.round(list.filter(r => r.rebar_d3d12 === true).length / list.length * 100) })),
    cpus: top(r => r.cpu, list => ({ threads: mean(list, 'cpu_threads') })),
    ram_gb: top(r => String(r.ram_gb)),
    windows: top(r => { const m = (r.os || '').match(/Windows (10|11)/); return m ? 'Windows ' + m[1] : 'other'; }),
  };
}

// A bug report: the zip the launcher made (summary, settings and log tails, the user name taken out of
// every path), sent only when the player presses "Send to edupode". Kept whole in `reports`, read only by
// edupode (scripts/get-reports.ps1 or the dashboard). Headers: X-VI-Id (the launcher's ID), X-VI-Kind
// (game or launcher); the body is the zip itself, at most MAX_REPORT bytes (a D1 row holds 2 MB).
const MAX_REPORT = 1800 * 1024;
const REPORTS_PER_HOUR = 5;

async function report(request, env) {
  const id = request.headers.get('X-VI-Id') || '';
  const kind = request.headers.get('X-VI-Kind') || '';
  if (!ID.test(id) || !['game', 'launcher'].includes(kind)) return new Response(null, { status: 400 });
  const length = Number(request.headers.get('content-length') || '0');
  if (length > MAX_REPORT) return new Response(null, { status: 413 });
  const hour = Math.floor(Date.now() / 3600000);
  const who = await sha256(`${env.RATE_SALT}|report|${request.headers.get('CF-Connecting-IP') || ''}|${hour}`);
  await env.DB.prepare('DELETE FROM rate WHERE hour < ?').bind(hour).run();
  const seen = await env.DB.prepare('INSERT INTO rate (who, hour, n) VALUES (?, ?, 1) ON CONFLICT(who) DO UPDATE SET n = n + 1 RETURNING n')
    .bind(who, hour).first('n');
  if (seen > REPORTS_PER_HOUR) return new Response(null, { status: 429 });
  const zip = new Uint8Array(await request.arrayBuffer());
  // A zip and nothing else: its first bytes are PK\x03\x04.
  if (zip.length < 22 || zip.length > MAX_REPORT || zip[0] !== 0x50 || zip[1] !== 0x4b || zip[2] !== 3 || zip[3] !== 4)
    return new Response(null, { status: 400 });
  const n = await env.DB.prepare(
    "INSERT INTO reports (created, vi_id, kind, bytes, zip) VALUES (datetime('now'), ?, ?, ?, ?) RETURNING n"
  ).bind(id, kind, zip.length, zip).first('n');
  return new Response(JSON.stringify({ n }), { status: 201, headers: { 'content-type': 'application/json' } });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method === 'POST' && url.pathname === '/v1/report') return report(request, env);
    if (request.method === 'GET' && url.pathname === '/v1/stats') {
      const auth = request.headers.get('Authorization') || '';
      if (!env.STATS_TOKEN || !sameToken(auth.replace(/^Bearer /, ''), env.STATS_TOKEN)) return new Response(null, { status: 404 });
      return new Response(JSON.stringify(await stats(env)), { headers: { 'content-type': 'application/json', 'cache-control': 'no-store' } });
    }
    if (request.method !== 'POST' || url.pathname !== '/v1/hw') return new Response(null, { status: 404 });
    const secret = request.headers.get('X-VI-Secret') || '';
    if (!/^[0-9a-f]{64}$/.test(secret)) return new Response(null, { status: 400 });
    const length = Number(request.headers.get('content-length') || '0');
    if (length > MAX_BODY) return new Response(null, { status: 413 });
    const text = await request.text();
    if (text.length > MAX_BODY) return new Response(null, { status: 413 });

    // Rate limit: a salted hash of the IP and the hour, never the IP itself.
    const hour = Math.floor(Date.now() / 3600000);
    const who = await sha256(`${env.RATE_SALT}|${request.headers.get('CF-Connecting-IP') || ''}|${hour}`);
    await env.DB.prepare('DELETE FROM rate WHERE hour < ?').bind(hour).run();
    const seen = await env.DB.prepare('INSERT INTO rate (who, hour, n) VALUES (?, ?, 1) ON CONFLICT(who) DO UPDATE SET n = n + 1 RETURNING n')
      .bind(who, hour).first('n');
    if (seen > PER_HOUR) return new Response(null, { status: 429 });

    let r;
    try { r = JSON.parse(text); } catch { return new Response(null, { status: 400 }); }
    if (!valid(r)) return new Response(null, { status: 400 });

    const secretHash = await sha256(secret);
    const row = await env.DB.prepare('SELECT secret_hash FROM hardware WHERE id = ?').bind(r.id).first();
    if (row && row.secret_hash !== secretHash) return new Response(null, { status: 403 });
    await env.DB.prepare(
      `INSERT INTO hardware (id, secret_hash, report, first_seen, last_seen) VALUES (?, ?, ?, datetime('now'), datetime('now'))
       ON CONFLICT(id) DO UPDATE SET report = excluded.report, last_seen = excluded.last_seen`
    ).bind(r.id, secretHash, JSON.stringify(r)).run();
    return new Response(null, { status: 204 });
  },
};
