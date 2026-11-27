-- wrangler d1 execute vi-hardware --remote --file schema.sql
CREATE TABLE IF NOT EXISTS hardware (
  id TEXT PRIMARY KEY,          -- VI-XXXX-XXXX (a hash of PC parts; the parts never leave the PC)
  secret_hash TEXT NOT NULL,    -- SHA-256 of the secret only that PC holds
  report TEXT NOT NULL,         -- the hardware JSON, the launcher's fields only
  first_seen TEXT NOT NULL,
  last_seen TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS rate (
  who TEXT PRIMARY KEY,         -- salted hash of IP and hour; rows go after the hour
  hour INTEGER NOT NULL,
  n INTEGER NOT NULL
);
