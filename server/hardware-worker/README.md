# VI hardware database

Where the launcher's hardware reports go when a player turns **Hardware sharing** on (it is off by default).

- **Only hardware:** graphics card, video memory, driver, Resizable BAR, processor, RAM, motherboard, Windows, disk type, laptop or not, and the `VI-XXXX-XXXX` ID (a hash of PC parts).
- **Never:** logs, options, paths, user names, IP addresses.
- **One way in, nothing out:** the public Worker has a single route, `POST /v1/hw`, and answers with a status code only. There is no route that reads anything.
- **Only you read it:** in the Cloudflare dashboard (D1 > vi-hardware), or with the admin Worker behind Cloudflare Access.
- **Nobody can overwrite someone else's row:** knowing an ID is not enough. The first report for an ID fixes its secret, which only that PC holds.

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
   wrangler deploy
   ```
   When `wrangler secret put` asks for the value, type any long random text.
4. Put the Worker's address plus `/v1/hw` (for example `https://vi-hardware.<you>.workers.dev/v1/hw`) in `HardwareEndpoint` in `launcher/Hardware.cs`, then publish a release. Until then the launcher sends nothing.
5. Set up the admin Worker:
   1. In Cloudflare Zero Trust, create an Access application for a hostname, for example `vi-admin.<your domain>`, with a policy that lets in only your email and requires 2FA.
   2. Put the team name and the application's AUD tag in `wrangler.admin.toml`.
   3. Deploy it with `wrangler deploy -c wrangler.admin.toml` and add the route for that hostname.
   4. Open the hostname and search by `VI-XXXX-XXXX`.
