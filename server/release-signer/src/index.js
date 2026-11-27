// VI release signer: signs a release's manifest.json and posts its announcement, for the release workflow of
// the two repositories that publish Julia's Launcher, and for nothing else.
//
// Secrets (wrangler secret put; nobody can read them back, not even from the dashboard):
//   SIGNING_KEY      the release key's private half, as a JWK (scripts\put-signer-secrets.ps1 converts it)
//   DISCORD_WEBHOOK  the announcements channel's webhook
//
// Who may ask: a GitHub Actions run, proved by the OIDC token GitHub signs for it (audience vi-release-signer),
// of one of REPOS. Signing and announcing take only the release workflow run for a pushed v* tag; the check
// route takes only the check workflow and signs a fixed, non-manifest text. So a collaborator who can push a
// tag can release (as with the local script before); nobody else can get anything signed or posted, and no
// route ever answers with a secret.

const ISSUER = 'https://token.actions.githubusercontent.com';
const AUDIENCE = 'vi-release-signer';
const REPOS = ['edupode/cold-project-vi', 'felicienradio/cold-project-vi'];
const MAX_MANIFEST = 64 * 1024;
const MAX_ANNOUNCEMENT = 2000;

const b64urlToBytes = s => Uint8Array.from(atob(s.replace(/-/g, '+').replace(/_/g, '/') + '==='.slice((s.length + 3) % 4)), c => c.charCodeAt(0));
const bytesToB64 = b => btoa(String.fromCharCode(...new Uint8Array(b)));
const json = (o, status = 200) => new Response(JSON.stringify(o), { status, headers: { 'content-type': 'application/json' } });
const deny = (status, why) => json({ error: why }, status);

// The run's claims, or null when the token is not one GitHub signed for us, now, for one of REPOS.
async function githubClaims(request) {
  const jwt = (request.headers.get('Authorization') || '').replace(/^Bearer\s+/i, '');
  const parts = jwt.split('.');
  if (parts.length !== 3) return null;
  let header, claims;
  try {
    header = JSON.parse(new TextDecoder().decode(b64urlToBytes(parts[0])));
    claims = JSON.parse(new TextDecoder().decode(b64urlToBytes(parts[1])));
  } catch { return null; }
  if (header.alg !== 'RS256' || !header.kid) return null;
  const jwks = await (await fetch(`${ISSUER}/.well-known/jwks`, { cf: { cacheTtl: 3600 } })).json();
  const jwk = (jwks.keys || []).find(k => k.kid === header.kid);
  if (!jwk) return null;
  const key = await crypto.subtle.importKey('jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);
  const ok = await crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, b64urlToBytes(parts[2]), new TextEncoder().encode(`${parts[0]}.${parts[1]}`));
  if (!ok) return null;
  const now = Date.now() / 1000;
  const aud = Array.isArray(claims.aud) ? claims.aud : [claims.aud];
  if (claims.iss !== ISSUER || !aud.includes(AUDIENCE) || !(claims.exp > now) || (claims.nbf && claims.nbf > now + 60)) return null;
  if (!REPOS.includes(claims.repository)) return null;
  return claims;
}

// The release workflow, run for a pushed v* tag.
function releaseRun(c) {
  return c.event_name === 'push' && /^refs\/tags\/v[\w.-]+$/.test(c.ref || '')
    && (c.job_workflow_ref || '').startsWith(`${c.repository}/.github/workflows/release.yml@${c.ref}`);
}

async function sign(env, bytes) {
  const jwk = JSON.parse(env.SIGNING_KEY.trim());
  const key = await crypto.subtle.importKey('jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);
  return bytesToB64(await crypto.subtle.sign('RSASSA-PKCS1-v1_5', key, bytes));
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method !== 'POST' || !['/v1/sign', '/v1/announce', '/v1/check'].includes(url.pathname)) return new Response(null, { status: 404 });
    const c = await githubClaims(request);
    if (!c) return deny(401, 'not a GitHub Actions run of an allowed repository');

    if (url.pathname === '/v1/check') {
      // The check workflow learns whether signing works, from a text that can never be a manifest.
      if (c.event_name !== 'push' || !(c.job_workflow_ref || '').startsWith(`${c.repository}/.github/workflows/signer-check.yml@refs/tags/signer-check-`)) return deny(403, 'only the check workflow');
      const text = `vi-signer-check:${c.repository}:${c.run_id}`;
      return json({ text, signature: await sign(env, new TextEncoder().encode(text)) });
    }

    if (!releaseRun(c)) return deny(403, 'only the release workflow of a pushed v* tag');
    const tag = c.ref.slice('refs/tags/'.length);
    const bytes = new Uint8Array(await request.arrayBuffer());

    if (url.pathname === '/v1/sign') {
      if (bytes.length > MAX_MANIFEST) return deny(413, 'manifest too large');
      // Only a manifest for this run's own tag and repository: its version, and every file from its release.
      let m;
      try { m = JSON.parse(new TextDecoder().decode(bytes)); } catch { return deny(400, 'not JSON'); }
      const prefix = `https://github.com/${c.repository}/releases/download/${tag}/`;
      if (m.version !== tag.slice(1) || !Array.isArray(m.files) || m.files.length === 0
        || !m.files.every(f => typeof f.url === 'string' && f.url.startsWith(prefix) && /^[0-9a-f]{64}$/.test(f.sha256 || '')))
        return deny(400, 'the manifest is not this tag\'s');
      return json({ signature: await sign(env, bytes) });
    }

    // /v1/announce: the release's announcement in the Discord channel.
    const text = new TextDecoder().decode(bytes).trim();
    if (!text || text.length > MAX_ANNOUNCEMENT || !text.includes(tag)) return deny(400, 'the announcement is empty, too long, or not about this tag');
    if (!env.DISCORD_WEBHOOK) return deny(500, 'no webhook set');
    const r = await fetch(env.DISCORD_WEBHOOK.trim(), {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ content: text, username: "Julia's Launcher", allowed_mentions: { parse: ['everyone'] } }),
    });
    return json({ posted: r.ok, status: r.status }, r.ok ? 200 : 502);
  },
};
