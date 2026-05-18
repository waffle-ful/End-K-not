# endknot-lobby-relay

Cloudflare Workers relay that forwards End K not lobby announcements to a
Discord channel. The DLL never sees the Discord webhook URL — it's held in
Worker Secrets and only the Worker can speak to Discord.

```
[End K not host] --HMAC POST--> [Worker] --webhook--> [Discord #lobby-board]
                                   |
                                   ↓
                         [KV: dedup / rate-limit / denylist]
```

## What this defends against (and what it doesn't)

| Attack | Defended? | How |
|---|---|---|
| Curl-spam against the public URL with no DLL | ✅ | HMAC signature required (`X-Signature` header) |
| Replay of a captured legit request | ✅ | `X-Timestamp` window ±5 minutes |
| Same host spamming the same code | ✅ | Per-host rate limit (`RATE_LIMIT_SECONDS`) + dedup window |
| One IP spamming many distinct codes | ✅ | Per-IP rate limit |
| Cross-host code hijack (B re-announces A's code) | ✅ | `409 code already announced by another host` |
| Banned griefer using their usual Steam account | ✅ | `fcHash` denylist (silent ignore) |
| **Determined reverser extracting the HMAC key from a released DLL** | ❌ | Each DLL release ships a single key; the Worker holds a list (`SHARED_HMAC_KEYS`). Mitigation = drop the leaked key from the list (only that release goes silent) and/or add `BLOCKED_VERSIONS` for the affected `modVersion`. New release uses a fresh key. |
| **Same attacker spoofing arbitrary `fcHash` values** | ❌ | Once they have the HMAC key, they can bypass the denylist by minting a fresh fake `fcHash` per request. Per-IP rate-limit still slows them down. |
| Fake (non-existent) game codes posted to Discord | ❌ | The Worker can't reach AU's matchmaker (it requires the host's EOS bearer token, which the relay doesn't have). Mitigation = Discord moderators clean up + ban via `/admin/ban`. |

In one sentence: **this stops casual abuse and keeps a known griefer out, but it isn't crypto-strong identity**. Treat the denylist as a soft block. If griefing gets worse we add stronger measures (per-host token issued by a Discord bot, EOS-token forwarding, etc.).

See [CONTRACT.md](./CONTRACT.md) for the exact wire format.

## One-time setup

```bash
# 0. Install deps (Node 20+)
cd relay
npm install
npx wrangler login

# 1. Create the KV namespace, copy the printed id into wrangler.toml
npx wrangler kv namespace create STATE
#   → paste the returned id into [[kv_namespaces]].id in wrangler.toml

# 2. Discord side: create a webhook in your #lobby-board channel
#    (Channel Settings → Integrations → Webhooks → New Webhook → Copy URL)

# 3. Generate the shared HMAC key and admin token
#    On Linux/Mac/WSL:
openssl rand -hex 32   # → HMAC key
openssl rand -hex 32   # → admin token
#    Save both somewhere safe — the HMAC key also goes into the DLL.

# 4. Set secrets (NEVER commit these)
npx wrangler secret put DISCORD_WEBHOOK_URL   # paste webhook URL
npx wrangler secret put ADMIN_TOKEN           # paste admin token
npx wrangler secret put SHARED_HMAC_KEY       # paste HMAC key

# 5. Local dry-run
npx wrangler dev
#   → curl http://localhost:8787/  (health)

# 6. Deploy
npx wrangler deploy
#   → prints the Worker URL, e.g. https://endknot-lobby-relay.<you>.workers.dev
#   → put this URL + the HMAC key into Modules/LobbyShare.cs (Phase 2)
```

## API summary

See [CONTRACT.md](./CONTRACT.md) for the canonical spec. Quick reference:

- `POST /api/announce` — new lobby
- `POST /api/start` — game started (PATCH the embed)
- `POST /api/end` — lobby closed / game ended (DELETE the message)
- `POST /admin/ban` / `POST /admin/unban` — bearer-auth
- `GET /admin/list` — bearer-auth, list denylist

All three `/api/*` endpoints require `X-Timestamp` + `X-Signature` headers.

## Admin operations

```bash
TOKEN="<ADMIN_TOKEN>"
WORKER="https://endknot-lobby-relay.<you>.workers.dev"

# Ban a host by fcHash
curl -X POST $WORKER/admin/ban \
    -H "Authorization: Bearer $TOKEN" \
    -H "content-type: application/json" \
    -d '{"fcHash":"<64 hex>"}'

# Unban
curl -X POST $WORKER/admin/unban \
    -H "Authorization: Bearer $TOKEN" \
    -H "content-type: application/json" \
    -d '{"fcHash":"<64 hex>"}'

# List current denylist
curl $WORKER/admin/list -H "Authorization: Bearer $TOKEN"
```

To get a target's `fcHash`, run `wrangler tail` while they announce and read it
from the log line. (Adding `/admin/seek?code=XXXXXX` to look up a live code is
a TODO if this turns into routine moderation work.)

## Tuning

Edit `wrangler.toml` `[vars]`:

| var | default | meaning |
|---|---|---|
| `ANNOUNCE_TTL_SECONDS` | 10800 | KV expiry for a lobby record (3h) |
| `RATE_LIMIT_SECONDS` | 60 | min interval between announces per IP / host |
| `DEDUP_WINDOW_SECONDS` | 30 | same host re-announcing same code returns cached message id |

## Cost model

Cloudflare free tier:
- Workers: 100,000 requests / day
- KV: 100,000 reads + 1,000 writes / day

A typical announce = 4 KV writes + ~3 KV reads + 1 Discord POST.
~250 announces / day fit comfortably. Over the free limit the Worker returns
503 — there is no auto-billing escalation.

## Rotating the HMAC key (non-breaking)

The Worker accepts a **list** of valid keys (`SHARED_HMAC_KEYS`, comma-separated).
A new release ships one new key; the old key stays in the list during a grace
window so already-distributed DLLs keep working.

```bash
# 1) Generate a fresh key
openssl rand -hex 32   # → K_new
# (on Windows PowerShell, see DEPLOY.md Step 3 for the equivalent)

# 2) Add K_new to the front of the list, keep K_old behind it
npx wrangler secret put SHARED_HMAC_KEYS
#   → paste:  K_new,K_old

# 3) Bake K_new into Modules/LobbyShareSecrets.cs in your build dir, ship release.
#    Hosts on the new DLL sign with K_new; old DLLs still sign with K_old and the
#    Worker accepts both.

# 4) After the grace window (or immediately on confirmed leak), drop K_old:
npx wrangler secret put SHARED_HMAC_KEYS
#   → paste:  K_new

# Hosts still on the old DLL then start receiving 401 bad signature and silently
# stop announcing — at that point the feature is opt-in anyway, so this is fine.
```

### Targeted version block

If a specific release leaks but you don't want to invalidate its key (perhaps
because legit hosts are still using it), you can block just that `modVersion`:

```bash
# In wrangler.toml [vars]:
BLOCKED_VERSIONS = "0.4.0,0.4.1"
npx wrangler deploy
```

Hosts on a blocked version receive `426 version blocked` and surface an upgrade
prompt in-game.
