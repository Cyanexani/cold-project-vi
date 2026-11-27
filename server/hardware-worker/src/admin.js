// The admin side, a Worker of its own (wrangler.admin.toml) on a hostname protected by Cloudflare
// Access (only edupode's account, with 2FA). Access checks the login before any request reaches
// this code; the Worker also checks Access's signed header, so it refuses to answer if the Access
// application is ever removed. GET /?id=VI-XXXX-XXXX shows one PC; GET / lists the latest 200.

async function accessOk(request, env) {
  const jwt = request.headers.get('Cf-Access-Jwt-Assertion');
  if (!jwt) return false;
  const res = await fetch(`https://${env.ACCESS_TEAM}.cloudflareaccess.com/cdn-cgi/access/certs`);
  const { keys } = await res.json();
  const [h, p, s] = jwt.split('.');
  const header = JSON.parse(atob(h.replace(/-/g, '+').replace(/_/g, '/')));
  const payload = JSON.parse(atob(p.replace(/-/g, '+').replace(/_/g, '/')));
  const jwk = keys.find(k => k.kid === header.kid);
  if (!jwk || !payload.aud?.includes(env.ACCESS_AUD) || payload.exp * 1000 < Date.now()) return false;
  const key = await crypto.subtle.importKey('jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);
  const sig = Uint8Array.from(atob(s.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));
  return crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, sig, new TextEncoder().encode(`${h}.${p}`));
}

const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

export default {
  async fetch(request, env) {
    if (request.method !== 'GET' || !(await accessOk(request, env))) return new Response(null, { status: 403 });
    const id = new URL(request.url).searchParams.get('id');
    const rows = id
      ? (await env.DB.prepare('SELECT id, report, first_seen, last_seen FROM hardware WHERE id = ?').bind(id.toUpperCase()).all()).results
      : (await env.DB.prepare('SELECT id, report, first_seen, last_seen FROM hardware ORDER BY last_seen DESC LIMIT 200').all()).results;
    const body = rows.map(r => {
      const h = JSON.parse(r.report);
      return `<tr><td>${esc(r.id)}</td><td>${esc(h.gpu)} (${h.vram_mb} MB, ${esc(h.gpu_driver)})</td><td>${h.rebar_bios}/${h.rebar_d3d12}</td>` +
        `<td>${esc(h.cpu)}</td><td>${h.ram_gb} GB</td><td>${esc(h.board_maker)} ${esc(h.board)}</td><td>${esc(h.os)}</td><td>${esc(h.disk)}${h.laptop ? ' laptop' : ''}</td><td>${esc(r.last_seen)}</td></tr>`;
    }).join('');
    return new Response(`<!doctype html><meta charset=utf-8><title>VI hardware</title>
<style>body{font:14px system-ui;background:#13171f;color:#e2e8f0}td,th{padding:4px 8px;border-bottom:1px solid #2c3442}</style>
<form><input name=id placeholder="VI-XXXX-XXXX" value="${esc(id || '')}"><button>Search</button></form>
<table><tr><th>ID</th><th>GPU</th><th>ReBAR bios/d3d12</th><th>CPU</th><th>RAM</th><th>Board</th><th>Windows</th><th>Disk</th><th>Last seen</th></tr>${body}</table>`,
      { headers: { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' } });
  },
};
