# نشر WhatsApp Integration على IIS مع HTTPS

هذا المشروع **Node.js + Puppeteer** — لا يُنشر كموقع ثابت على IIS.

# Dev vs Prod (مشروع HR)

نفس الفكرة في الاتنين — الفرق مكان تشغيل Node فقط:

| | Dev | Prod |
|--|-----|------|
| Frontend | `ng serve` → `environment.ts` | `ng build --configuration=production` + `config.json` |
| FE API | `https://localhost:44312/HR_BE/` | `https://10.10.50.32:2025/HR_BE/` (أو من `assets/config.json`) |
| WhatsApp calls | `{APIUrl}/api/whatsapp/qr\|status\|send` | نفس المسار |
| HrBe config | `appsettings.Development.json` → `WhatsAppNode:BaseUrl` | `appsettings.json` → `WhatsAppNode:BaseUrl` |
| Node | `http://127.0.0.1:3000` على جهاز الـ Dev | `http://127.0.0.1:3000` على سيرفر الـ Prod (نفس سيرفر HrBe) |

```
[Dev أو Prod FE HTTPS]
        ↓
[HrBe HTTPS /api/WhatsApp/*]
        ↓
[Node WhatsAppIntgration على 127.0.0.1:3000]
```

### Checklist — Dev
1. شغّل Node: `cd WhatsAppIntgration && npm start`
2. شغّل HrBe (Visual Studio / `dotnet run`)
3. شغّل Angular: `ng serve`
4. افتح `/#/appSettings` → تبويب WhatsApp

### Checklist — Prod
1. انشر Node على سيرفر الـ Backend كـ Windows Service (NSSM) على `127.0.0.1:3000`
2. في `appsettings.json` على السيرفر: `"WhatsAppNode": { "BaseUrl": "http://127.0.0.1:3000" }`
3. تأكد إن شهادة IIS/HTTPS لـ HrBe شغالة
4. في `AllowedOrigin` حط أصل الـ Frontend الـ HTTPS
5. انشر Angular؛ `config.json` يشير لـ APIUrl الصحيح

---

## لو حابب تعرّض Node وحدّه على HTTPS

```
Angular (HTTPS)  →  IIS (HTTPS :443)  →  Node (HTTP 127.0.0.1:3000)
```

IIS يستلم شهادة SSL، وNode يعمل محليًا فقط.

---

## المتطلبات

| المطلوب | ملاحظة |
|---------|--------|
| Node.js 18+ | على السيرفر |
| Google Chrome | لـ whatsapp-web.js / Puppeteer |
| IIS + URL Rewrite | [تحميل](https://www.iis.net/downloads/microsoft/url-rewrite) |
| ARR (Application Request Routing) | [تحميل](https://www.iis.net/downloads/microsoft/application-request-routing) |
| شهادة SSL | لربط HTTPS على موقع IIS |
| NSSM أو PM2 | لتشغيل Node كخدمة Windows |

### تفعيل الـ Proxy في ARR

1. IIS Manager → اختر السيرفر (Server node)
2. **Application Request Routing Cache** → **Server Proxy Settings**
3. فعّل **Enable proxy** → Apply

### السماح بـ Server Variables (مرة واحدة)

IIS Manager → الموقع → **URL Rewrite** → **View Server Variables** → أضف:

- `HTTP_X_FORWARDED_PROTO`
- `HTTP_X_FORWARDED_HOST`
- `HTTP_ACCEPT_ENCODING`

---

## 1) نسخ المشروع وتشغيل Node

```powershell
# مثال مسار النشر
xcopy /E /I "D:\mechoo\movies\WhatsAppIntgration" "C:\Apps\WhatsAppIntegration"

cd C:\Apps\WhatsAppIntegration
npm ci --omit=dev
```

تأكد أن الحساب الذي يشغّل الخدمة يملك صلاحية كتابة على:

- `C:\Apps\WhatsAppIntegration\.wwebjs_auth\`
- `C:\Apps\WhatsAppIntegration\whatsapp_session.json`

### تشغيل كخدمة (NSSM)

```powershell
nssm install WhatsAppIntegration "C:\Program Files\nodejs\node.exe" "C:\Apps\WhatsAppIntegration\index.js"
nssm set WhatsAppIntegration AppDirectory "C:\Apps\WhatsAppIntegration"
nssm set WhatsAppIntegration AppEnvironmentExtra ^
PORT=3000 ^
HOST=127.0.0.1 ^
QUIET_QR_TERMINAL=1 ^
ALLOWED_ORIGINS=https://your-angular-app.com

nssm set WhatsAppIntegration Start SERVICE_AUTO_START
nssm start WhatsAppIntegration
```

اختبار محلي:

```powershell
curl http://127.0.0.1:3000/status
```

---

## 2) إعداد موقع IIS بـ HTTPS

1. أنشئ موقع IIS جديد (أو Application).
2. اربط **HTTPS :443** بشهادة SSL صحيحة (نفس الدومين اللي Angular بيستخدمه أو subdomain مثل `whatsapp-api.example.com`).
3. انسخ محتويات مجلد `iis\` إلى جذر موقع IIS، خصوصًا **`web.config`**.

الملف الجاهز: [`iis/web.config`](iis/web.config)

لو API على مسار فرعي مثل `/whatsapp-api` استخدم: [`iis/web.config.subpath.example`](iis/web.config.subpath.example)

---

## 3) ربط Angular (HTTPS)

في `environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  // لو الـ API على نفس الدومين عبر IIS proxy
  whatsappApiUrl: 'https://your-domain.com'
  // أو: 'https://your-domain.com/whatsapp-api'
};
```

عرض الـ QR:

```typescript
this.http.get<{ ready: boolean; qr: string | null }>(`${environment.whatsappApiUrl}/qr`)
  .subscribe(res => {
    if (res.qr) this.qrImage = res.qr; // <img [src]="qrImage">
  });
```

أو SSE (أفضل لأن الـ QR يتحدث تلقائيًا):

```typescript
const es = new EventSource(`${environment.whatsappApiUrl}/events`);
es.addEventListener('qr', (e: MessageEvent) => {
  const data = JSON.parse(e.data);
  this.qrImage = data.qr;
});
es.addEventListener('ready', (e: MessageEvent) => {
  const data = JSON.parse(e.data);
  this.sessionId = data.sessionId;
  this.qrImage = null;
});
```

بعد الاتصال احفظ `sessionId` وأرسله مع كل `POST /send`.

---

## متغيرات البيئة

| المتغير | الافتراضي | الوظيفة |
|---------|-----------|---------|
| `PORT` | `3000` | منفذ Node (يجب أن يطابق `web.config`) |
| `HOST` | `127.0.0.1` | يستمع محليًا فقط خلف IIS |
| `ALLOWED_ORIGINS` | فارغ = `*` | قائمة Origins مسموحة مفصولة بفاصلة، مثال: `https://app.example.com` |
| `QUIET_QR_TERMINAL` | off | تقليل طباعة QR في الكونسول |
| `SESSION_WEBHOOK_URL` | فارغ | إشعار .NET عند اتصال الجلسة |
| `REQUIRE_SESSION_ID` | on | طلب `sessionId` في `/send` |

---

## Endpoints عبر HTTPS

| Method | Path | وصف |
|--------|------|-----|
| GET | `/status` | صحة الخدمة |
| GET | `/qr` | صورة QR (`data:image/png;base64,...`) |
| GET | `/events` | SSE لتحديثات QR/الاتصال |
| GET | `/session` | `sessionId` بعد المسح |
| POST | `/send` | إرسال رسالة |

---

## استكشاف الأخطاء

| المشكلة | الحل |
|---------|------|
| Angular يقول Mixed Content | لا تنادِ `http://...` من صفحة HTTPS — استخدم رابط IIS الـ HTTPS فقط |
| 502 Bad Gateway من IIS | خدمة Node واقفة أو المنفذ غلط — `curl http://127.0.0.1:3000/status` |
| CORS في المتصفح | عيّن `ALLOWED_ORIGINS` لرابط Angular الـ HTTPS |
| `/events` ينقطع | ARR Proxy مفعّل + `HTTP_ACCEPT_ENCODING` متاح كـ server variable |
| Puppeteer يفشل | ثبّت Chrome + صلاحيات كتابة على `.wwebjs_auth` |

---

## قائمة تحقق سريعة

- [ ] `npm ci` تم على السيرفر
- [ ] خدمة Node تعمل على `127.0.0.1:3000`
- [ ] ARR Enable proxy مفعّل
- [ ] `web.config` في جذر موقع IIS
- [ ] HTTPS شهادة مربوطة على الموقع
- [ ] Angular يشير لـ `https://...` وليس `http://localhost:3000`
- [ ] (اختياري) `ALLOWED_ORIGINS` = أصل تطبيق Angular
