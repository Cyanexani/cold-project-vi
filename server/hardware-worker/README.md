# VI hardware database

Where the launcher's hardware reports go when a player turns **Hardware sharing** on (it is off by default).

- **Only hardware:** graphics card, video memory, driver, Resizable BAR, processor, RAM, motherboard, Windows, disk type, laptop or not, and the `VI-XXXX-XXXX` ID (a hash of PC parts).
- **Never:** logs, options, paths, user names, IP addresses.
- **One way in, nothing out:** the public Worker has a single route, `POST /v1/hw`, and answers with a status code only. There is no route that reads anything.
- **Only you read it:** in the Cloudflare dashboard (D1 > vi-hardware), or with the admin Worker behind Cloudflare Access.
- **Nobody can overwrite someone else's row:** knowing an ID is not enough. The first report for an ID fixes its secret, which only that PC holds.

## How you get the list, and the public averages

- **The list** (one row per PC) stays in the D1 database. You read it in the Cloudflare dashboard (D1 > vi-hardware > Console: `SELECT * FROM hardware`) or on the admin page (step 5), searching by ID.
- **The averages** are published in the repository by the GitHub workflow [`hardware-stats.yml`](../../.github/workflows/hardware-stats.yml), every 6 hours:
  - it reads `GET /v1/stats` with a secret token;
  - it writes `docs/hardware-stats.json` and `docs/HARDWARE-STATS.md`.
- **What the endpoint returns:** only totals and averages, with no ID and no row. A group of fewer than 3 PCs counts as "other", so nobody can be picked out.
- **If the token leaked,** someone would see only the averages, which are public anyway.
- **Asking players:** while sharing is off, the launcher asks before every game. It explains why it helps, what the list is, what is sent and what never is. "Don't ask again" stops it.

## Setting it up (once, about 10 minutes)

1. Make a free Cloudflare account and install Wrangler: `npm i -g wrangler`, then `wrangler login`.
2. Create the database and put its id in both `wrangler.toml` and `wrangler.admin.toml`:
   ```
   wrangler d1 create vi-hardware
   wrangler d1 execute vi-hardware --remote --file schema.sql
   ```
3. Deploy the public Worker:
   ```
   wrangler secret put RATE_SALT
   wrangler secret put STATS_TOKEN
   wrangler deploy
   ```
   When `wrangler secret put` asks for each value, type a different long random text (at least 32 characters). Keep a copy of the `STATS_TOKEN` value for step 6.
4. Put the Worker's address plus `/v1/hw` (for example `https://vi-hardware.<you>.workers.dev/v1/hw`) in `HardwareEndpoint` in `launcher/Hardware.cs`, then publish a release. Until then the launcher sends nothing.
5. Set up the admin Worker:
   1. In Cloudflare Zero Trust, create an Access application for a hostname, for example `vi-admin.<your domain>`, with a policy that lets in only your email and requires 2FA.
   2. Put the team name and the application's AUD tag in `wrangler.admin.toml`.
   3. Deploy it with `wrangler deploy -c wrangler.admin.toml` and add the route for that hostname.
   4. Open the hostname and search by `VI-XXXX-XXXX`.
6. Set up the averages: on GitHub, open the repository's Settings > Secrets and variables > Actions and add two secrets:
   - `VI_STATS_URL`: the Worker's address plus `/v1/stats`;
   - `VI_STATS_TOKEN`: the `STATS_TOKEN` value from step 3.

   Then run **Actions > Hardware averages > Run workflow** once. GitHub runs scheduled workflows only from the repository's default branch, so for the 6-hour schedule keep `hardware-stats.yml` there too.
