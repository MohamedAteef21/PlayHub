# PlayHub WhatsApp Gateway

Node service that links each Master/tenant WhatsApp number via QR (whatsapp-web.js).

## Deploy layout

| Piece | Where |
|-------|--------|
| React frontend | **Vercel** |
| .NET API | **MonsterASP / IIS** |
| This gateway | **Render** (Docker + disk) or VPS — **not** Vercel |

The browser never talks to this service directly. PlayHub API proxies status / QR / send / disconnect.

## Run locally

```bash
cd whatsapp-gateway
npm install
npm start
```

Default: `http://127.0.0.1:3000`

## Render

Use repo-root `render.yaml` (Blueprint) or build `whatsapp-gateway/Dockerfile`.
Sessions persist under `DATA_DIR` (default `/data` on Render).

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

- `PORT` (default 3000; Render sets this)
- `HOST` (default `127.0.0.1`; use `0.0.0.0` on Render)
- `DATA_DIR` (session root; `/data` on Render disk)
- `PUPPETEER_EXECUTABLE_PATH` (system Chromium in Docker)
- `REQUIRE_SESSION_ID` (default true)
- `QUIET_QR_TERMINAL=1`
- `ALLOWED_ORIGINS` (optional; API proxies so usually unused)
