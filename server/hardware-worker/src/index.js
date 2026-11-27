// VI hardware database: one way in, nothing out.
//
// The only route is POST /v1/hw. It takes the launcher's hardware report (the exact fields below,
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

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
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
