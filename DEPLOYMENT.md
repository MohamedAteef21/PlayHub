# PlayHub — دليل النشر (Deployment Guide)

النظام 3 أجزاء، كل جزء يترفع على منصة مختلفة:

| الجزء | المنصة | ليه |
|---|---|---|
| Backend (.NET 10 API + SignalR + Hangfire) | **MonsterASP.NET** | يدعم .NET 10 و SignalR و SQL Server |
| Frontend (React/Vite) | **Vercel** | استضافة static سريعة ومجانية |
| WhatsApp Gateway (Node.js + Chrome) | **Render** (موصى به) / VPS / Railway | محتاج بروسيس شغال 24/7 + Chromium + disk — مايشتغلش على Vercel ولا MonsterASP shared |

---

## 1) Backend على MonsterASP.NET

### أ. إنشاء الموقع وقاعدة البيانات
1. سجّل حساب على [MonsterASP.NET](https://www.monsterasp.net/) وأنشئ **Website** جديد (اختر .NET 10). هتاخد دومين زي `playhub.runasp.net`.
2. من الـ Control Panel أنشئ **MSSQL Database** وانسخ الـ **Connection String** بتاعها.

### ب. تعديل الإعدادات قبل الرفع
عدّل `src/PlayHub.Api/appsettings.Production.json`:
- `ConnectionStrings:HrConnection` → الـ connection string بتاع MonsterASP.
- `Seed:Password` → باسورد قوي لحساب الأدمن الأول (`PlayHubAdmin`).
- `Cors:Origins` → دومين Vercel النهائي (مثلاً `https://playhub.vercel.app`).
- `WhatsApp:ApiBaseUrl` → رابط الـ WhatsApp Gateway بعد استضافته (سيبها placeholder لو هتأجل الواتساب).

> الـ JWT Key مولّد جاهز في الملف — ماتغيرهوش بعد أول تشغيل وإلا كل المستخدمين هيتسجل خروجهم.

### ج. البناء والرفع
```powershell
dotnet publish src/PlayHub.Api/PlayHub.Api.csproj -c Release -o publish/api
Compress-Archive -Path publish/api/* -DestinationPath publish/playhub-api.zip -Force
```
1. من MonsterASP Control Panel → **Files** → ارفع `playhub-api.zip` جوه مجلد `wwwroot` واعمل **Unzip** (مع Overwrite).
2. من إعدادات الموقع فعّل **WebSockets** (مطلوب للـ SignalR) وتأكد إن **Environment = Production** (ASPNETCORE_ENVIRONMENT).
3. أعد تشغيل الـ App Pool (زر Restart).
4. أول تشغيل هيعمل Migrations تلقائيًا وينشئ حساب `PlayHubAdmin` بالباسورد اللي حطيته.

> بديل أسهل من الـ zip: Visual Studio WebDeploy — نزّل ملف `.publishSettings` من الـ Control Panel واستورده في Publish wizard.

### د. اختبار
افتح `https://<your-site>.runasp.net/api/auth/login` بـ POST — لو رجع 401/400 يبقى الـ API شغال.

---

## 2) Frontend على Vercel

1. ارفع المشروع على GitHub (لو مش مرفوع).
2. من [vercel.com](https://vercel.com) → **Add New Project** → اختر الريبو.
3. الإعدادات:
   - **Root Directory**: `web`
   - **Framework Preset**: Vite (هيتحدد تلقائي)
   - **Build Command**: `npm run build` — **Output**: `dist`
4. **Environment Variables** — ضيف:
   - `VITE_API_URL` = `https://<your-site>.runasp.net` (من غير `/` في الآخر)
5. Deploy. ملف `web/vercel.json` موجود بالفعل وبيظبط الـ SPA routing.
6. **مهم:** بعد ما تعرف دومين Vercel النهائي، ارجع حطه في `Cors:Origins` في `appsettings.Production.json` على MonsterASP وأعد تشغيل الموقع.

> بديل بدون GitHub: `npm i -g vercel` ثم من مجلد `web`: `vercel --prod` (هيسألك تسجل دخول).

---

## 3) WhatsApp Gateway على Render

الـ Gateway (مجلد `whatsapp-gateway`) بيشغّل WhatsApp Web عبر Chromium، فمحتاج:
- بروسيس Node.js شغال باستمرار (مش serverless)
- مساحة دائمة لحفظ session الواتساب (عشان ما تعملش مسح QR كل مرة)

الملفات جاهزة في الريبو:
- `whatsapp-gateway/Dockerfile` — Node 20 + Chromium
- `render.yaml` — Blueprint (Web Service + disk على `/data`)

> **مهم:** الخطة المجانية على Render بتنام وملهاش disk ثابت — الواتساب هيطلب QR كل مرة. استخدم **Starter** (أو أعلى) زي ما هو مضبوط في `render.yaml`.

### أ. رفع الخدمة (Blueprint — مرة واحدة)

1. ادفع الريبو على GitHub (الفرع فيه `render.yaml`).
2. من [dashboard.render.com](https://dashboard.render.com) → **New** → **Blueprint**.
3. اربط الريبو `PlayHub` واختار الفرع اللي فيه `render.yaml` (أو `main` بعد الدمج).
4. **Apply** — Render هيبني الـ Docker ويطلع رابط زي:
   `https://playhub-whatsapp-gateway.onrender.com`
5. افتح `/health` على الرابط — لازم يرجع `{ "ok": true, ... }`.

### ب. ربط الـ API

حط رابط الـ Gateway (من غير `/` في الآخر) في:
- `WhatsApp:ApiBaseUrl` في إعدادات الإنتاج على MonsterASP  
  أو في حقل `WhatsAppApiBaseUrl` للـ tenant من لوحة الماستر

بعدها من الإعدادات في البرنامج اعمل مسح QR وربط الرقم.

### ج. إنشاء الخدمة من الـ API (بديل)

من Account Settings → API Keys انسخ المفتاح، وابعتّه للوكيل عشان ينشئ الخدمة تلقائيًا عبر `https://api.render.com/v1/services`.

### بدائل

| الخيار | ملاحظات |
|---|---|
| **VPS** | `pm2` + Chromium — أثبت لو عندك سيرفر |
| **Railway** | نفس الـ Dockerfile + Volume على `/data` |
| **جهاز المحل** | Cloudflare Tunnel يوصل الـ API للـ Gateway المحلي |

---

## 4) المطلوب منك عشان أكمل الرفع

1. **حساب MonsterASP.NET**: أنشئ الموقع + قاعدة MSSQL وابعتلي:
   - رابط الموقع (`xxx.runasp.net`)
   - الـ Connection String بتاع الداتابيز
   - (لو عايزني أرفع بنفسي) بيانات FTP أو ملف WebDeploy `.publishSettings`
2. **حساب Vercel**: اربط الريبو بنفسك بالخطوات فوق، أو ابعتلي Vercel Token لو عايزني أرفع بالـ CLI.
3. **حساب Render** (للواتساب): Blueprint من `render.yaml`، أو ابعت Render API Key لو عايز الوكيل ينشئ الخدمة.
4. **باسورد الأدمن** اللي هيتحط في `Seed:Password`.

---

## ملف مرجعي سريع

- الباك مبني جاهز في: `publish/api/` + مضغوط في `publish/playhub-api.zip`
- الفرونت مبني جاهز في: `web/dist/`
- إعدادات الإنتاج: `src/PlayHub.Api/appsettings.Production.json`
- متغير الفرونت الوحيد: `VITE_API_URL`
