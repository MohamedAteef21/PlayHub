# PlayHub WhatsApp Gateway

Node service that links each Master/tenant WhatsApp number via QR (whatsapp-web.js).

## Deploy layout

| Piece | Where |
|-------|--------|
| React frontend | **Vercel** |
| .NET API | **MonsterASP / IIS** |
| This gateway | **Same VPS as the API** (Node process on `127.0.0.1:3000`) — **not** Vercel |

The browser never talks to this service directly. PlayHub API proxies status / QR / send / disconnect.

## Run locally

```bash
cd whatsapp-gateway
npm install
npm start
```

Default: `http://127.0.0.1:3000`

## Multi-tenant

Every request must include tenant id:

- Header: `X-Client-Id: <tenant-guid>`
- Or query: `?clientId=<tenant-guid>`

Each client id gets its own Chromium auth folder under `.wwebjs_auth/`.

## API

- `GET /health`
- `GET /status` — connection state (+ QR if waiting)
- `GET /qr`
- `GET /session`
- `POST /ensure` — start browser if needed
- `POST /disconnect` — logout + clear local session
- `POST /send` — `{ phone|number, message, sessionId? }`
- `POST /send-document` — `{ phone, caption, base64, fileName, mimeType, sessionId? }`

Send requires `X-Session-Id` (or body `sessionId`) matching the connected session unless `REQUIRE_SESSION_ID=0`.

## Env

- `PORT` (default 3000)
- `HOST` (default 127.0.0.1)
- `REQUIRE_SESSION_ID` (default true)
- `QUIET_QR_TERMINAL=1`
- `ALLOWED_ORIGINS=https://your-vercel-app.vercel.app` (optional; API proxies so usually unused)
