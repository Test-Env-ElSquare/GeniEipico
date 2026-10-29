# User Management API

Base URL:

```text
/v1
```

## Authentication flow

### Login

```http
POST /v1/auth/login
```

يرجع `accessToken` للطلبات المحمية و`refreshToken` لتجديد الجلسة.

### Refresh

```http
POST /v1/auth/refresh
```

يستقبل `refreshToken` صالحًا، ثم يرجع زوجًا جديدًا من الرموز ويلغي رمز التجديد القديم. لا يغيّر بيانات المستخدم.

### Logout

```http
POST /v1/auth/logout
```

يلغي `refreshToken`. رمز الدخول الحالي يظل صالحًا إلى وقت انتهائه؛ لذلك عمره قصير نسبيًا.

### Current user

```http
GET /v1/auth/me
Authorization: Bearer {accessToken}
```

يقرأ رقم المستخدم من رمز الدخول ويرجع بياناته وأدواره وصلاحياته من قاعدة البيانات. المستخدم المحذوف أو غير النشط يُرفض.

## Password recovery

### Request password reset

```http
POST /v1/auth/forgot-password
Content-Type: application/json

{
  "email": "user@example.com"
}
```

لا يستقبل كلمة مرور ولا رمزًا. يولّد رمز إعادة التعيين ويرسله بالبريد، ولا يرجع الرمز في الاستجابة. الاستجابة واحدة سواء كان البريد موجودًا أم لا لمنع اكتشاف حسابات النظام.

### Verify reset code

```http
POST /v1/auth/verify-reset-code
Content-Type: application/json

{
  "email": "user@example.com",
  "code": "123456"
}
```

يتحقق من أن الكود صحيح وغير منتهي. مدة الكود ساعة واحدة. المسار القديم `POST /v1/auth/verify-reset-token` ما زال مدعومًا مؤقتًا لكنه يستقبل `code`.

### Reset password

```http
POST /v1/auth/reset-password
Content-Type: application/json

{
  "email": "user@example.com",
  "code": "123456",
  "newPassword": "NewPassword@123"
}
```

يرفض الرمز الخطأ أو المنتهي، ويرفض إعادة استخدام كلمة المرور الحالية، ويلغي رموز التجديد القديمة بعد النجاح.

### Change own password

```http
PUT /v1/auth/change-password
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "currentPassword": "CurrentPassword@123",
  "newPassword": "NewPassword@123"
}
```

رقم المستخدم يأتي من رمز الدخول ولا يرسله العميل.

## Admin-only user management

المسارات التالية تحتاج رمز مستخدم له دور `Admin`:

```text
POST   /v1/auth/signup
GET    /v1/users
GET    /v1/users/{id}
POST   /v1/users
PUT    /v1/users/{id}
DELETE /v1/users/{id}
PUT    /v1/users/{id}/reset-password
GET    /v1/roles
GET    /v1/roles/{id}
POST   /v1/roles
PUT    /v1/roles/{id}
DELETE /v1/roles/{id}
```

مثال إنشاء مستخدم:

```json
{
  "email": "operator@example.com",
  "fullName": "Line Operator",
  "password": "Operator@123",
  "customerId": 1,
  "role": "Operator",
  "factoryId": 1,
  "lineId": 1,
  "phoneNumber": null,
  "isActive": true
}
```

لو لم يُرسل الدور، يكون الدور الافتراضي `Viewer`. يتم التحقق من أن المصنع تابع للعميل وأن الخط تابع للمصنع.

## SMTP configuration

إعدادات البريد موجودة تحت `SmtpSettings`. لا تحفظ كلمة مرور البريد داخل المستودع. استخدم متغيرات البيئة على جهاز التشغيل:

```text
SmtpSettings__User
SmtpSettings__Password
SmtpSettings__SenderEmail
SmtpSettings__ResetPasswordUrl
```

يجب أن يكون `ResetPasswordUrl` عنوان شاشة إعادة كلمة المرور في الواجهة. إذا تُرك فارغًا، تصل الرسالة وبداخلها الرمز مباشرة.

للتشغيل المحلي يمكن حفظ القيم خارج المشروع باستخدام:

```powershell
dotnet user-secrets set "SmtpSettings:User" "your-email@gmail.com" --project src/API
dotnet user-secrets set "SmtpSettings:Password" "your-new-app-password" --project src/API
dotnet user-secrets set "SmtpSettings:SenderEmail" "your-email@gmail.com" --project src/API
```

## MFA status

المساران التاليان خاصان بالمصادقة متعددة العوامل، وليس إعادة كلمة المرور:

```text
POST /v1/auth/mfa/setup
POST /v1/auth/mfa/verify
```

كلاهما غير منفّذ حاليًا ويرجع `mfa_not_implemented`. لم يتم تغيير سلوكهما في هذه المراجعة.
