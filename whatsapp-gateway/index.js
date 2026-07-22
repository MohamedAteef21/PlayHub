const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const express = require('express');
const qrcodeTerminal = require('qrcode-terminal');
const QRCode = require('qrcode');
const { Client, LocalAuth, MessageMedia, WAState } = require('whatsapp-web.js');

/**
 * PlayHub WhatsApp gateway (multi-tenant).
 * Each Master/tenant uses X-Client-Id (tenant GUID) → isolated LocalAuth session.
 *
 * IIS / MonsterASP: run Node on HTTP localhost; reverse-proxy HTTPS here.
 * Frontend (Vercel) talks to PlayHub API only — never to this port directly.
 */
const PORT = Number(process.env.PORT) || 3000;
const HOST = process.env.HOST || '127.0.0.1';
/** Persistent data root (Render disk mounts at /data). Falls back to app dir locally. */
const DATA_DIR = process.env.DATA_DIR || __dirname;
const SESSIONS_DIR = path.join(DATA_DIR, 'sessions');
const AUTH_ROOT = path.join(DATA_DIR, '.wwebjs_auth');
const REQUIRE_SESSION_ID = process.env.REQUIRE_SESSION_ID !== '0' && process.env.REQUIRE_SESSION_ID !== 'false';
const QUIET_QR_TERMINAL = process.env.QUIET_QR_TERMINAL === '1' || process.env.QUIET_QR_TERMINAL === 'true';
const PUPPETEER_EXECUTABLE_PATH = process.env.PUPPETEER_EXECUTABLE_PATH || '';
const ALLOWED_ORIGINS = (process.env.ALLOWED_ORIGINS || '')
  .split(',')
  .map((s) => s.trim())
  .filter(Boolean);

fs.mkdirSync(SESSIONS_DIR, { recursive: true });
fs.mkdirSync(AUTH_ROOT, { recursive: true });

const app = express();
app.set('trust proxy', 1);
app.use(express.json({ limit: '15mb' }));

app.use((req, res, next) => {
  const origin = req.headers.origin;
  if (ALLOWED_ORIGINS.length === 0) {
    res.setHeader('Access-Control-Allow-Origin', '*');
  } else if (origin && ALLOWED_ORIGINS.includes(origin)) {
    res.setHeader('Access-Control-Allow-Origin', origin);
    res.setHeader('Vary', 'Origin');
  }
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, X-Session-Id, X-Client-Id');
  res.setHeader('Access-Control-Expose-Headers', 'Content-Type');
  if (req.method === 'OPTIONS') {
    return res.sendStatus(204);
  }
  next();
});

/** @type {Map<string, WaSession>} */
const sessions = new Map();

function sanitizeClientId(raw) {
  const s = String(raw || 'default').trim().toLowerCase();
  const cleaned = s.replace(/[^a-z0-9_-]/g, '').slice(0, 64);
  return cleaned || 'default';
}

function resolveClientId(req) {
  return sanitizeClientId(
    req.headers['x-client-id'] || req.query.clientId || req.body?.clientId || 'default'
  );
}

function widFromInfo(info) {
  if (!info?.wid) return null;
  const w = info.wid;
  if (typeof w === 'string') return w;
  return w._serialized || (w.user ? `${w.user}@${w.server || 'c.us'}` : null);
}

function phoneDigitsFromWid(widUser) {
  if (!widUser) return '';
  const base = String(widUser).split('@')[0] || '';
  return base.replace(/\D/g, '');
}

function formatChatId(number) {
  const digits = String(number).replace(/\D/g, '');
  if (!digits) throw new Error('Invalid number');
  return `${digits}@c.us`;
}

function isTransientBrowserError(err) {
  const msg = String(err?.message || err || '');
  return /Execution context was destroyed|Target closed|Session closed|Protocol error|EBUSY|lockfile|Navigating frame was detached/i.test(
    msg
  );
}

function puppeteerArgs() {
  return [
    '--no-sandbox',
    '--disable-setuid-sandbox',
    '--disable-dev-shm-usage',
    '--disable-gpu',
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-extensions',
    '--disable-background-networking',
    '--disable-features=TranslateUI',
    '--mute-audio'
  ];
}

class WaSession {
  constructor(clientId) {
    this.clientId = clientId;
    this.sessionFile = path.join(SESSIONS_DIR, `${clientId}.json`);
    this.authPath = path.join(AUTH_ROOT, `session-${clientId}`);
    this.lockfile = path.join(this.authPath, 'lockfile');

    this.isReady = false;
    this.currentSessionId = null;
    this.currentWidUser = null;
    this.sessionConnectedAt = null;
    this.qrDataUrl = null;
    this.qrGeneratedAt = null;
    this.lastQrRaw = null;
    this.qrRotationCount = 0;
    this.hasLoggedAuthenticated = false;
    this.hasHandledReady = false;
    this.isInitializing = false;
    this.reconnectTimer = null;
    this.client = null;
  }

  loadSessionFile() {
    try {
      return JSON.parse(fs.readFileSync(this.sessionFile, 'utf8'));
    } catch {
      return {};
    }
  }

  saveSessionFile(data) {
    fs.writeFileSync(this.sessionFile, JSON.stringify(data, null, 2), 'utf8');
  }

  clearConnectionState(reason) {
    const wasReady = this.isReady;
    this.isReady = false;
    this.qrDataUrl = null;
    this.qrGeneratedAt = null;
    this.lastQrRaw = null;
    this.currentSessionId = null;
    this.currentWidUser = null;
    this.sessionConnectedAt = null;

    try {
      const f = this.loadSessionFile();
      if (f.sessionId) {
        this.saveSessionFile({ ...f, disconnectedAt: Date.now(), lastReason: String(reason) });
      }
    } catch (_) {
      /* ignore */
    }

    if (wasReady) {
      console.warn(`[${this.clientId}] Connection cleared:`, reason);
    }
  }

  tryRemoveStaleLockfile() {
    const names = ['lockfile', 'SingletonLock', 'SingletonCookie', 'SingletonSocket'];
    for (const name of names) {
      const p = path.join(this.authPath, name);
      try {
        if (fs.existsSync(p)) {
          fs.unlinkSync(p);
          console.warn(`[${this.clientId}] Removed stale ${name}`);
        }
      } catch (e) {
        console.warn(`[${this.clientId}] Could not remove ${name}:`, e.message);
      }
    }
  }

  async destroyBrowser({ logout = false } = {}) {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    const client = this.client;
    this.client = null;
    this.isInitializing = false;
    this.hasLoggedAuthenticated = false;
    this.hasHandledReady = false;
    if (!client) {
      this.tryRemoveStaleLockfile();
      return;
    }
    try {
      if (logout && typeof client.logout === 'function') {
        await client.logout();
      }
    } catch (e) {
      console.warn(`[${this.clientId}] logout:`, e.message || e);
    }
    try {
      await client.destroy();
    } catch (e) {
      console.warn(`[${this.clientId}] destroy:`, e.message || e);
    }
    this.tryRemoveStaleLockfile();
  }

  ensureClient() {
    if (this.client) return this.client;

    fs.mkdirSync(AUTH_ROOT, { recursive: true });

    const client = new Client({
      authStrategy: new LocalAuth({
        clientId: this.clientId,
        dataPath: AUTH_ROOT
      }),
      authTimeoutMs: 120000,
      restartOnAuthFail: true,
      takeoverOnConflict: true,
      puppeteer: {
        headless: true,
        args: puppeteerArgs(),
        ...(PUPPETEER_EXECUTABLE_PATH ? { executablePath: PUPPETEER_EXECUTABLE_PATH } : {})
      }
    });

    client.on('qr', async (qr) => {
      if (qr === this.lastQrRaw) return;

      if (this.isReady) {
        this.clearConnectionState('qr:session_invalidated');
      }

      this.lastQrRaw = qr;
      this.qrRotationCount += 1;

      if (QUIET_QR_TERMINAL) {
        console.log(`[${this.clientId}][QR] refreshed #${this.qrRotationCount}`);
      } else {
        console.log(`\n[${this.clientId}][QR #${this.qrRotationCount}] Scan soon\n`);
        qrcodeTerminal.generate(qr, { small: true });
      }

      try {
        this.qrDataUrl = await QRCode.toDataURL(qr, {
          width: 512,
          margin: 4,
          errorCorrectionLevel: 'H',
          color: { dark: '#000000', light: '#ffffff' }
        });
        this.qrGeneratedAt = Date.now();
      } catch (err) {
        console.error(`[${this.clientId}] Failed to build QR:`, err);
        this.qrDataUrl = null;
        this.qrGeneratedAt = null;
      }
    });

    client.on('authenticated', () => {
      this.qrDataUrl = null;
      this.qrGeneratedAt = null;
      this.lastQrRaw = null;
      if (this.hasLoggedAuthenticated) return;
      this.hasLoggedAuthenticated = true;
      console.log(`[${this.clientId}] Authenticated`);
    });

    client.on('ready', async () => {
      if (this.hasHandledReady && this.isReady && this.currentSessionId) return;
      this.hasHandledReady = true;
      this.isReady = true;
      this.qrDataUrl = null;
      this.qrGeneratedAt = null;
      this.lastQrRaw = null;

      const widUser = widFromInfo(client.info);
      this.currentWidUser = widUser;
      const file = this.loadSessionFile();
      if (file.sessionId && file.widUser === widUser) {
        this.currentSessionId = file.sessionId;
      } else {
        this.currentSessionId = crypto.randomUUID();
      }
      this.sessionConnectedAt = Date.now();

      this.saveSessionFile({
        sessionId: this.currentSessionId,
        clientId: this.clientId,
        widUser,
        pushname: client.info?.pushname || '',
        platform: client.info?.platform || '',
        connectedAt: this.sessionConnectedAt
      });

      console.log(`[${this.clientId}] Ready. sessionId=${this.currentSessionId} phone=${phoneDigitsFromWid(widUser)}`);
    });

    client.on('auth_failure', (msg) => {
      this.hasLoggedAuthenticated = false;
      this.hasHandledReady = false;
      this.clearConnectionState(`auth_failure:${msg}`);
      console.error(`[${this.clientId}] Auth failure:`, msg);
    });

    client.on('disconnected', (reason) => {
      this.hasLoggedAuthenticated = false;
      this.hasHandledReady = false;
      this.clearConnectionState(String(reason));
      console.warn(`[${this.clientId}] Disconnected:`, reason);
      this.scheduleReconnect(String(reason));
    });

    client.on('loading_screen', (percent, message) => {
      console.log(`[${this.clientId}] loading ${percent}% — ${message}`);
    });

    client.on('change_state', (state) => {
      console.log(`[${this.clientId}] state`, state);
      if (!this.isReady) return;
      if (state === WAState.CONNECTED) return;
      if (
        state === WAState.UNPAIRED ||
        state === WAState.UNPAIRED_IDLE ||
        state === WAState.CONFLICT ||
        state === WAState.TOS_BLOCK ||
        state === WAState.PROXYBLOCK
      ) {
        this.clearConnectionState(`change_state:${state}`);
      }
    });

    this.client = client;
    return client;
  }

  scheduleReconnect(reason) {
    if (this.reconnectTimer) return;
    // LOGOUT / UNPAIRED means local auth is dead — don't thrash reconnect; wait for QR via ensure.
    const fatal = /LOGOUT|UNPAIRED|auth_failure|TOS_BLOCK|PROXYBLOCK/i.test(String(reason));
    if (fatal) {
      console.warn(`[${this.clientId}] Fatal disconnect (${reason}) — destroy browser; next /ensure will show QR`);
      this.destroyBrowser({ logout: false }).catch(() => {});
      return;
    }
    console.log(`[${this.clientId}] Will re-init in 5s (${reason})...`);
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.destroyBrowser({ logout: false })
        .then(() => this.start(1))
        .catch((e) => console.error(`[${this.clientId}] reconnect failed:`, e.message || e));
    }, 5000);
  }

  async start(attempt = 1) {
    if (this.isInitializing && attempt === 1) {
      console.warn(`[${this.clientId}] Already initializing — skip`);
      return;
    }
    this.isInitializing = true;
    const maxAttempts = 5;
    this.tryRemoveStaleLockfile();
    this.hasLoggedAuthenticated = false;
    this.hasHandledReady = false;

    // Always create a fresh Client after failures / reconnects
    if (this.client && attempt > 1) {
      await this.destroyBrowser({ logout: false });
      this.isInitializing = true;
    }

    const client = this.ensureClient();
    try {
      console.log(`[${this.clientId}] initialize (attempt ${attempt}/${maxAttempts})...`);
      await client.initialize();
      this.isInitializing = false;
    } catch (err) {
      console.error(`[${this.clientId}] init failed (attempt ${attempt}):`, err.message || err);
      await this.destroyBrowser({ logout: false });
      if (attempt >= maxAttempts) {
        this.isInitializing = false;
        this.clearConnectionState(`init_failed:${err.message || err}`);
        return;
      }
      const delayMs = Math.min(30000, 2500 * attempt);
      await new Promise((r) => setTimeout(r, delayMs));
      this.isInitializing = false;
      return this.start(attempt + 1);
    }
  }

  async ensureStarted() {
    if (this.isReady || this.isInitializing) return;
    if (this.client && !this.isReady) {
      // Client exists but stuck waiting — leave it (QR flow)
      return;
    }
    await this.start(1);
  }

  statusPayload() {
    const status = this.isReady ? 'AUTHENTICATED' : 'WAITING_QR';
    return {
      ok: true,
      clientId: this.clientId,
      ready: this.isReady,
      status,
      hasQr: Boolean(this.qrDataUrl),
      qrGeneratedAt: this.qrGeneratedAt,
      hasSession: Boolean(this.currentSessionId),
      sessionId: this.currentSessionId,
      phone: phoneDigitsFromWid(this.currentWidUser),
      phoneNumber: phoneDigitsFromWid(this.currentWidUser),
      qr: this.isReady ? null : this.qrDataUrl,
      initializing: this.isInitializing
    };
  }

  sessionPayload() {
    if (!this.isReady || !this.currentSessionId) {
      return {
        ready: false,
        clientId: this.clientId,
        sessionId: null,
        widUser: null,
        phone: null,
        phoneNumber: null,
        status: null,
        connectedAt: null
      };
    }
    return {
      ready: true,
      clientId: this.clientId,
      sessionId: this.currentSessionId,
      widUser: this.currentWidUser,
      phone: phoneDigitsFromWid(this.currentWidUser),
      phoneNumber: phoneDigitsFromWid(this.currentWidUser),
      status: 'Connected',
      connectedAt: this.sessionConnectedAt,
      pushname: this.client?.info?.pushname || ''
    };
  }

  qrPayload() {
    return {
      ready: this.isReady,
      clientId: this.clientId,
      qr: this.qrDataUrl,
      qrBase64: this.qrDataUrl,
      generatedAt: this.qrGeneratedAt,
      rotation: this.qrRotationCount,
      sessionId: this.currentSessionId,
      widUser: this.currentWidUser,
      phone: phoneDigitsFromWid(this.currentWidUser)
    };
  }

  async sendText(number, message, sessionId) {
    if (!this.isReady) {
      const err = new Error('WhatsApp client is not ready yet');
      err.status = 503;
      throw err;
    }

    if (REQUIRE_SESSION_ID) {
      if (!sessionId || typeof sessionId !== 'string') {
        const err = new Error('sessionId required (body or X-Session-Id header)');
        err.status = 401;
        throw err;
      }
      if (!this.currentSessionId || sessionId !== this.currentSessionId) {
        const err = new Error('Invalid or expired sessionId');
        err.status = 403;
        throw err;
      }
    }

    const chatId = formatChatId(number);
    const result = await this.client.sendMessage(chatId, String(message));
    return { success: true, messageId: result?.id?._serialized || null };
  }

  async sendDocument(number, caption, base64, fileName, mimeType, sessionId) {
    if (!this.isReady) {
      const err = new Error('WhatsApp client is not ready yet');
      err.status = 503;
      throw err;
    }

    if (REQUIRE_SESSION_ID) {
      if (!sessionId || typeof sessionId !== 'string') {
        const err = new Error('sessionId required');
        err.status = 401;
        throw err;
      }
      if (!this.currentSessionId || sessionId !== this.currentSessionId) {
        const err = new Error('Invalid or expired sessionId');
        err.status = 403;
        throw err;
      }
    }

    const chatId = formatChatId(number);
    const media = new MessageMedia(mimeType || 'application/pdf', base64, fileName || 'file.pdf');
    const result = await this.client.sendMessage(chatId, media, { caption: caption || '' });
    return { success: true, messageId: result?.id?._serialized || null };
  }

  async disconnect({ logout = true } = {}) {
    this.clearConnectionState('user_disconnect');
    await this.destroyBrowser({ logout });

    try {
      if (fs.existsSync(this.sessionFile)) fs.unlinkSync(this.sessionFile);
    } catch (_) {
      /* ignore */
    }

    // Wipe LocalAuth so next scan is clean
    try {
      fs.rmSync(this.authPath, { recursive: true, force: true });
    } catch (e) {
      console.warn(`[${this.clientId}] auth wipe:`, e.message || e);
    }

    return { ok: true, disconnected: true, clientId: this.clientId };
  }
}

function getSession(clientId) {
  let s = sessions.get(clientId);
  if (!s) {
    s = new WaSession(clientId);
    sessions.set(clientId, s);
  }
  return s;
}

async function withSession(req, res, fn, { start = true } = {}) {
  const clientId = resolveClientId(req);
  const session = getSession(clientId);
  try {
    if (start) await session.ensureStarted();
    return await fn(session, req, res);
  } catch (err) {
    const status = err.status || 500;
    return res.status(status).json({
      success: false,
      ok: false,
      error: err.message || 'Failed'
    });
  }
}

app.get('/health', (_req, res) => {
  res.json({
    ok: true,
    service: 'playhub-whatsapp-gateway',
    sessions: sessions.size,
    time: new Date().toISOString()
  });
});

app.get('/status', (req, res) =>
  withSession(req, res, async (session) => {
    res.json(session.statusPayload());
  })
);

app.get('/session', (req, res) =>
  withSession(req, res, async (session) => {
    res.json(session.sessionPayload());
  })
);

app.get('/qr', (req, res) =>
  withSession(req, res, async (session) => {
    res.json(session.qrPayload());
  })
);

app.get('/newQR', (req, res) =>
  withSession(req, res, async (session) => {
    if (session.isReady) {
      return res.status(400).json({
        ok: false,
        error: 'Already connected; disconnect first to scan again.'
      });
    }
    const page = session.client?.pupPage;
    if (!page) {
      return res.status(503).json({
        ok: false,
        error: 'Browser not ready yet; wait a few seconds and retry.'
      });
    }
    session.lastQrRaw = null;
    try {
      await page.evaluate(() => {
        if (window.Store?.Cmd?.refreshQR) {
          window.Store.Cmd.refreshQR();
          return;
        }
        throw new Error('WhatsApp Store not ready (refreshQR unavailable).');
      });
      return res.json({ ok: true, message: 'Refresh triggered.' });
    } catch (err) {
      return res.status(500).json({ ok: false, error: String(err.message || err) });
    }
  })
);

app.post('/ensure', (req, res) =>
  withSession(req, res, async (session) => {
    if (!session.client && !session.isInitializing) {
      await session.start(1);
    }
    res.json(session.statusPayload());
  })
);

app.post('/disconnect', (req, res) =>
  withSession(
    req,
    res,
    async (session) => {
      const result = await session.disconnect({ logout: true });
      sessions.delete(session.clientId);
      res.json(result);
    },
    { start: false }
  )
);

app.post('/send', (req, res) =>
  withSession(req, res, async (session) => {
    const body = req.body || {};
    const number = body.number ?? body.toNumber ?? body.phone;
    const message = body.message;
    const sessionId = body.sessionId ?? req.headers['x-session-id'];

    if (number === undefined || number === null || message === undefined || message === null) {
      return res.status(400).json({
        success: false,
        error: 'number/phone and message are required'
      });
    }

    try {
      const result = await session.sendText(number, message, sessionId);
      return res.json(result);
    } catch (err) {
      const status = err.status || 500;
      return res.status(status).json({ success: false, error: err.message || 'Failed to send' });
    }
  })
);

app.post('/send-document', (req, res) =>
  withSession(req, res, async (session) => {
    const body = req.body || {};
    const number = body.number ?? body.toNumber ?? body.phone;
    const caption = body.caption ?? body.message ?? '';
    const base64 = body.base64 ?? body.fileBase64 ?? body.data;
    const fileName = body.fileName ?? body.filename ?? 'document.pdf';
    const mimeType = body.mimeType ?? body.contentType ?? 'application/pdf';
    const sessionId = body.sessionId ?? req.headers['x-session-id'];

    if (!number || !base64) {
      return res.status(400).json({
        success: false,
        error: 'phone/number and base64 are required'
      });
    }

    try {
      const result = await session.sendDocument(number, caption, base64, fileName, mimeType, sessionId);
      return res.json(result);
    } catch (err) {
      const status = err.status || 500;
      return res.status(status).json({ success: false, error: err.message || 'Failed to send document' });
    }
  })
);

/** Heartbeat: detect logout across all active sessions */
setInterval(async () => {
  for (const session of sessions.values()) {
    if (!session.isReady || typeof session.client?.getState !== 'function') continue;
    try {
      const s = await session.client.getState();
      if (s == null) continue;
      if (s === WAState.CONNECTED || s === WAState.OPENING || s === WAState.PAIRING) continue;
      session.clearConnectionState(`heartbeat:${s}`);
    } catch (e) {
      if (isTransientBrowserError(e)) {
        console.warn(`[${session.clientId}] heartbeat transient:`, e.message || e);
        continue;
      }
      session.clearConnectionState(`heartbeat:${e.message || e}`);
    }
  }
}, 30000);

process.on('unhandledRejection', (reason) => {
  if (isTransientBrowserError(reason)) {
    console.warn('[unhandledRejection] transient:', reason?.message || reason);
    return;
  }
  console.error('[unhandledRejection]', reason);
});

process.on('uncaughtException', (err) => {
  if (isTransientBrowserError(err)) {
    console.warn('[uncaughtException] transient:', err.message);
    return;
  }
  console.error('[uncaughtException]', err);
  process.exit(1);
});

app.listen(PORT, HOST, () => {
  console.log(`PlayHub WhatsApp gateway on http://${HOST}:${PORT}`);
  console.log(`DATA_DIR=${DATA_DIR}`);
  if (PUPPETEER_EXECUTABLE_PATH) {
    console.log(`Chromium: ${PUPPETEER_EXECUTABLE_PATH}`);
  }
  console.log('Multi-tenant: pass X-Client-Id (tenant GUID) on every request.');
  console.log('Sessions start lazily when /status|/qr|/ensure is called.');
});
