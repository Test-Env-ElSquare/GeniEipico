# Planning — دليل ربط الشاشة

الكونترولر: `src/API/Controllers/Planning/PlanningController.cs`.
Base URL: `/v1/planning`، والخدمة `Infrastructure/Services/PlanningService.cs`.
كل الـ endpoints تتطلب Bearer token صالحًا، بنفس تحقق Expert Board claims الحالي.
هذه ليست صفحة Expert Board معينة. `CustomerId` من التوكن ولا يُرسل في الطلب.
المصانع واللاينات تُحصر في صلاحيات المستخدم. FactoryId أو LineId غير مسموح به يرجع 403.

## قبل التشغيل

Migration جاهزة: `20260929093408_AddPlanningStoppageSupport`.
تجعل `Planning.ProductionSchedules.BatchId` اختياريًا للتوقفات وتضيف `Title`؛ لا تحذف بيانات.
لا تنشئ Migration ثانية لنفس التعديل. طبّق الموجودة، بعد مراجعة البيئة المستهدفة:

```powershell
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

أو من Package Manager Console:

```powershell
Update-Database -Project Infrastructure -StartupProject API
```

لم يتم تشغيل Update-Database أو الـ feed بواسطة Codex.
الرجوع من هذه Migration يتوقف عمدًا إذا وُجدت توقفات بدون BatchId؛ لا يحذفها ولا يربطها بباتش وهمية.

## خريطة الشاشة

| عنصر UI | الطلب | مفاتيح data |
|---|---|---|
| Attainment | GET `/summary` | `attainmentPct` |
| Planned units + produced | GET `/summary` | `plannedQty`, `producedQty` |
| Active / Upcoming / Finished | GET `/summary` | `activeCount`, `upcomingCount`, `finishedCount` |
| Route readiness + Routing badge | GET `/summary` | `routeReadiness.active`, `.total` |
| MRP exceptions + MRP badge | GET `/summary` | `mrp.exceptions`, `.total` |
| Stoppages badge | GET `/summary` | `stoppagesCount` |
| Production schedule + shifts + line labels + now marker | GET `/schedule` | `title`, `periods`, `lines`, `shifts`, `batches`, `stoppages`, `nowUtc` |
| Planned vs produced chart | GET `/planned-vs-produced` | `items` |
| All batches cards | GET `/batches` | `items`, `pagination` |
| New Schedule dropdowns | GET `/form-options` | `factories`, `lines`, `batches` |
| New Schedule → Add | POST `/schedules` | الجدولة المحفوظة |
| Routing list | GET `/routes` | `items`, `count` |
| New Route dropdowns | GET `/form-options` | `skus`, `routeStatuses` |
| New Route → Add | POST `/routes` | المسار المحفوظ وخطواته |
| Material Plan cards + table | GET `/mrp` | `summary`, `items` |
| Stoppages list | GET `/stoppages` | `items`, `count` |
| Add Stoppage dropdowns | GET `/form-options` | `lines`, `stoppageTypes` |
| Add Stoppage → Add | POST `/stoppages` | التوقف المحفوظ |
| Shift handover | مؤجل | لا تعرض أرقامًا تجريبية |

الضغط على بلوك باتش لتحديد الكارت يستخدم `batchId` في الفرونت؛ لا يحتاج طلبًا جديدًا.
Cancel في نموذج الإضافة يغلق النموذج فقط. لا يوجد endpoint إلغاء/حذف جديد ضمن هذه الشاشة.

## الفلاتر وشكل الرد

مثال:

```http
GET /v1/planning/summary?FactoryId=1&LineId=1&Duration=0
GET /v1/planning/batches?FactoryId=1&LineId=1&Duration=5&From=2026-09-29T00:00:00Z&To=2026-09-30T00:00:00Z&Page=1&PageSize=10
```

- Duration: `0` الشيفت الحالي، `1` السابق، `2` آخر 24 ساعة، `3` آخر 7 أيام، `4` آخر شهر، `5` فترة مخصصة.
- `From/To` يجب إرسالهما معًا وFrom < To. إذا أُرسلا فلهما الأولوية. الحد الأقصى للفترة المخصصة 366 يومًا.
- أرسل التوقيت بـ `Z` أو offset واضح؛ يتحول الحفظ إلى UTC.
- لا يوجد State filter. Page/PageSize يُستخدمان لكروت الباتشات فقط، PageSize من 1 إلى 100.
- غياب FactoryId/LineId يعني كل النطاق المسموح، وليس كل عملاء النظام.
- مع All factories يوجد `periods[]` منفصل لكل مصنع؛ الشيفتات قد تختلف بين المصانع.
- الشيفتات تتبع منطق الوقت الحالي في المشروع (أوقات UTC)، بما فيها الشيفت الذي يعبر منتصف الليل.
- الرسم يعرض الفترة المختارة وليس 24 ساعة ثابتة. استخدم `title` و`periods` لبناء العنوان والمحور.
- البيانات/الحسابات لا تُقص عند الآن للشيفت الحالي: الباتشات القادمة داخل نفس الشيفت تظهر أيضًا.
- Routing على مستوى العميل، لا تتغير قائمته بفلتر الوقت/اللاين. الـ MRP أيضًا على مستوى العميل؛ الوقت يختار أيام الخطة فقط، وليس مخزون مصنع/لاين.
- Form options لا تقيد الباتشات بالتاريخ، حتى لا تختفي الباتشات المستقبلية عند فتح New Schedule.
- الأيام/الأوقات ذات النهاية الحدّية تستخدم `[from,to)`؛ جدولة تنتهي وقت بداية التالية ليست تداخلًا.

كل الأمثلة التالية تمثل `data` داخل هذا الغلاف، والأرقام توضيحية وليست قيمًا ثابتة في الكود:

```json
{
  "success": true,
  "data": {},
  "meta": null,
  "error": null,
  "ts": "2026-09-29T09:00:00Z"
}
```

التحقق القياسي من DTO بواسطة ASP.NET قد يرجع ValidationProblemDetails عند 400؛ أخطاء العمل ترجع الغلاف المعتاد.

## 1. GET /summary

```json
{
  "periods": [
    {"duration":0,"factoryId":1,"lineId":1,"from":"2026-09-29T08:00:00Z","to":"2026-09-29T16:00:00Z","shiftId":1,"shiftName":"Morning Shift","mode":"current_shift","note":"Current factory shift resolved from Planning.Shifts."}
  ],
  "plannedQty": 3000,
  "producedQty": 1200,
  "attainmentPct": 40,
  "activeCount": 1,
  "upcomingCount": 1,
  "finishedCount": 1,
  "routeReadiness": {"active":1,"total":1,"scope":"customer"},
  "mrp": {"scope":"customer","total":3,"sufficient":1,"monitor":1,"critical":1,"other":0,"exceptions":2},
  "stoppagesCount": 1
}
```

الحسابات:

- مجموع PlannedQty وProducedQty من الباتشات المختارة، كل باتش مرة واحدة؛ ليس تجميع إشارات Production.
- `cancelled` و`split` يظهران في الكروت، لكن لا يدخلان الكميات ولا رسم الإنتاج. نتجنب عد الباتش الأصلية وأبنائها معًا.
- Attainment = produced / planned × 100. إذا المخطط صفر، `null`. لا يتم إخفاء إنتاج أكثر من المخطط بقص النسبة عند 100.
- Active = in_progress + production. Upcoming = planned وموعد البداية بعد الآن، ضمن الفترة.
- Route readiness = Active / Total لمسارات العميل، وليس فحص جاهزية المعدات.
- MRP counts تخص سجلات المواد/الأيام المعروضة؛ نفس المادة في يومين تُعد سجلين. الحالات غير المعروفة تُعد في `other`، ولا نخمن درجة خطورتها.

## 2. GET /schedule

```json
{
  "periods": [{"duration":0,"factoryId":1,"lineId":1,"from":"2026-09-29T08:00:00Z","to":"2026-09-29T16:00:00Z","shiftId":1,"shiftName":"Morning Shift","mode":"current_shift","note":"Current factory shift resolved from Planning.Shifts."}],
  "title":"Production schedule - current shift",
  "nowUtc":"2026-09-29T09:00:00Z",
  "lines":[{"id":1,"name":"Solid Dosage Line","code":"L01","factoryId":1}],
  "shifts":[{"shiftId":1,"name":"Morning Shift","factoryId":1,"from":"2026-09-29T08:00:00Z","to":"2026-09-29T16:00:00Z"}],
  "batches":[{
    "schedule":{"id":12,"batchId":40,"lineId":1,"title":null,"start":"2026-09-29T08:00:00Z","end":"2026-09-29T10:00:00Z","durationHours":2,"isStoppage":false,"type":null,"notes":null},
    "batch":{"batchId":40,"uid":"BT-2609-00040","skuId":1,"skuCode":"SKU-01","productName":"Demo product","lineId":1,"lineName":"Solid Dosage Line","status":"production","stage":"production","displayStatus":"production","plannedQty":1000,"producedQty":250,"rejectedQty":0,"attainmentPct":25,"plannedStart":"2026-09-29T08:00:00Z","plannedEnd":"2026-09-29T10:00:00Z","actualStart":"2026-09-29T08:05:00Z","actualEnd":null}
  }],
  "stoppages":[{"id":13,"batchId":null,"lineId":1,"title":"Line cleaning","start":"2026-09-29T10:00:00Z","end":"2026-09-29T11:00:00Z","durationHours":1,"isStoppage":true,"type":"cleaning","notes":null}]
}
```

الباتشات القديمة التي ليس لها سجل Schedule تظهر من PlannedStart/PlannedEnd:

```json
{"id":null,"batchId":41,"lineId":1,"start":"2026-09-29T12:00:00Z","end":null,"isStoppage":false,"source":"batch_planned_dates"}
```

هذا بديل كائن `schedule` فقط؛ معه `batch` بنفس الشكل أعلاه. إذا النهاية null اعرض نقطة/بداية غير محددة المدة، لا تفترض عدد ساعات. القائمة لا تُكرر الباتش بين المصدرين.
قص البلوكات بصريًا عند حدود الفترة إن بدأت قبلها/انتهت بعدها، ولا تغير التوقيت الأصلي.

## 3. GET /planned-vs-produced

```json
{
  "periods": [],
  "items": [
    {"batchId":40,"uid":"BT-2609-00040","productName":"Demo product","plannedQty":1000,"producedQty":250}
  ]
}
```

`periods` لها نفس الشكل السابق؛ اختُصرت هنا وفي بعض الأمثلة فقط. استخدم UID مع اسم المنتج للتمييز بين باتشين لنفس المنتج.

## 4. GET /batches

```json
{
  "periods": [],
  "items": [
    {"batchId":40,"uid":"BT-2609-00040","skuId":1,"skuCode":"SKU-01","productName":"Demo product","lineId":1,"lineName":"Solid Dosage Line","status":"production","stage":"production","displayStatus":"production","plannedQty":1000,"producedQty":250,"rejectedQty":0,"attainmentPct":25,"plannedStart":"2026-09-29T08:00:00Z","plannedEnd":"2026-09-29T10:00:00Z","actualStart":"2026-09-29T08:05:00Z","actualEnd":null}
  ],
  "pagination":{"page":1,"pageSize":10,"total":1,"totalPages":1,"hasPrevious":false,"hasNext":false}
}
```

`displayStatus=dispensing` عندما Status=in_progress وStage=dispense فقط.
الحالات الأخرى: planned / in_progress / production / finished / cancelled / split.

## 5. GET /form-options

```http
GET /v1/planning/form-options?FactoryId=1&LineId=1
```

```json
{
  "factories":[{"value":1,"label":"Factory A"}],
  "lines":[{"value":1,"label":"Solid Dosage Line","factoryId":1}],
  "batches":[{"value":41,"label":"BT-2609-00041","productName":"Demo product","lineId":1,"plannedQty":1000,"start":"2026-09-29T12:00:00Z","end":"2026-09-29T14:00:00Z"}],
  "skus":[{"value":1,"label":"Demo product","code":"SKU-01"}],
  "routeStatuses":["draft","active"],
  "stoppageTypes":["maintenance","cleaning","calibration"]
}
```

في New Schedule اختر batch من `batches` بدل Product الحر؛ اعرض المنتج واللاين والكمية للقراءة فقط. لا نغير LineId للباتش.
القائمة تحتوي planned فقط وغير المسجلة بالفعل في ProductionSchedules. لا تستبعد المستقبل.
حقل البداية Date+Time بدل ساعة فقط. الفرونت يمكنه حساب End من Duration بالساعات.

## 6. POST /schedules

```json
{"batchId":41,"start":"2026-09-29T12:00:00Z","end":"2026-09-29T14:00:00Z","notes":"Afternoon plan"}
```

Response data:

```json
{"id":14,"batchId":41,"lineId":1,"title":null,"start":"2026-09-29T12:00:00Z","end":"2026-09-29T14:00:00Z","durationHours":2,"isStoppage":false,"type":null,"notes":"Afternoon plan"}
```

يحفظ السجل ويحدّث PlannedStart/PlannedEnd للباتش في transaction واحدة؛ لا ينشئ باتش ولا يبدأها ولا يغير كمياتها.
يعيد تحديد ShiftId إذا وقعت المدة بالكامل داخل شيفت مُعرّف، وإلا تصبح null بدل الاحتفاظ بشيفت قديمة خاطئة.
تكرار جدولة نفس الباتش يرجع 409 `already_scheduled`؛ لا يحدث Update صامت.
التداخل يرجع 409 `overlap`، بما فيه مواعيد الباتشات الموجودة بدون سجل Schedule إذا كانت نهايتها معروفة.
يستخدم SQL Server transaction-owned application lock لكل لاين لمنع سباق طلبي جدولة/توقف عبر هذه الخدمة.
هذه الحماية تخص endpoints التخطيط الجديدة؛ الأدوات المباشرة/SQL ومسارات CRUD القديمة لا تكتسبها تلقائيًا.

## 7. GET /routes

```json
{
  "scope":"customer",
  "count":1,
  "items":[{"id":7,"code":"RT-abc123","name":"Demo product","skuId":1,"productName":"Demo product","status":"active","version":1,"cycleHours":2,"steps":[{"id":10,"stepNo":1,"stepName":"Mixing","machineType":null,"estDurationMin":null},{"id":11,"stepNo":2,"stepName":"Packaging","machineType":null,"estDurationMin":null}]}]
}
```

## 8. POST /routes

```json
{"skuId":1,"cycleHours":2,"status":"draft","steps":["Mixing","Packaging"]}
```

Response data:

```json
{"id":8,"code":"RT-generated-code","skuId":1,"productName":"Demo product","status":"draft","version":2,"cycleHours":2,"steps":[{"id":12,"stepNo":1,"stepName":"Mixing"},{"id":13,"stepNo":2,"stepName":"Packaging"}]}
```

لو UI يكتب خطوات بفواصل، الفرونت يحولها إلى Array. الحد 100 خطوة، اسم الخطوة 80 حرفًا، والدورة من 0.01 إلى 9999.99 ساعة.
الكود والـ version يولدهما السيرفر. يستخدم `IRouteWorkflowService` الموجودة لحفظ الخطوات والتفعيل، داخل transaction.
إرسال active يؤرشف المسار النشط السابق لنفس SKU وفق السلوك الحالي؛ لا يُعاد توجيه الباتشات القديمة للمسار الجديد.

## 9. GET /mrp

```json
{
  "scope":"customer",
  "factoryAndLineFilterApplied":false,
  "dateGranularity":"day",
  "periods":[],
  "summary":{"scope":"customer","total":3,"sufficient":1,"monitor":1,"critical":1,"other":0,"exceptions":2},
  "items":[
    {"id":1,"materialId":5,"materialName":"Demo material","unit":"kg","planDate":"2026-09-29T00:00:00","requiredQty":100,"availableQty":80,"gapQty":20,"status":"monitor","poReference":null,"poExpectedDate":null}
  ]
}
```

نقرأ المحفوظ كما هو من MrpRequirements؛ لا حساب مخزون ولا خصم ولا استدعاء SAP.
اليوم الذي يتقاطع مع الفترة يُعرض حتى لو بداية الشيفت 08:00 وPlanDate محفوظ منتصف الليل.
`ok` و`sufficient` يدخلان كارت Sufficient، وmonitor وcritical حسب القيمة المخزنة. لا نحسب الحالة من GapQty.
اعرض الوحدة من Material.Uom، لا تكتب kg ثابتة لكل المواد. لا تجمع كميات مواد بوحدات مختلفة.
مع All factories يتم اختيار اتحاد الأيام بدون تكرار سجلات MRP، وليس جمع نفس مخزون العميل مرة لكل مصنع.

## 10. GET /stoppages

```json
{
  "periods":[],
  "count":1,
  "items":[{"id":13,"batchId":null,"lineId":1,"title":"Line cleaning","start":"2026-09-29T10:00:00Z","end":"2026-09-29T11:00:00Z","durationHours":1,"isStoppage":true,"type":"cleaning","notes":null}]
}
```

يستخدم نفس الفترة، وليس upcoming دائمًا؛ غير عنوان القائمة حسب الفلتر. اسم اللاين من lines في form-options/schedule.

## 11. POST /stoppages

```json
{"lineId":1,"title":"Line cleaning","type":"cleaning","start":"2026-09-29T10:00:00Z","end":"2026-09-29T11:00:00Z","notes":null}
```

Response data هو نفس كائن التوقف أعلاه، بالـ id الجديد.
الاختيار على لاين فقط؛ لا Scale ولا Machine. النوع النصي من form-options. العنوان مطلوب حتى 200 حرف.
هذا حجز تخطيطي، لا يوقف الماكينة فعليًا ولا ينشئ Work Order تلقائيًا.

## دورة طلبات الفرونت

1. عند فتح Machine Schedule أو تغيير الفلاتر: summary + schedule + planned-vs-produced + batches.
2. عند فتح Routing: routes. عند فتح MRP: mrp بنفس الفترة. عند فتح Stoppages: stoppages.
3. عند فتح أي نموذج: form-options بالنطاق المختار. New Schedule نفس الطلب مهما كان التاب المفتوح.
4. بعد نجاح إضافة Schedule: أعد summary/schedule/planned-vs-produced/batches/form-options.
5. بعد إضافة Route: أعد routes/summary. بعد إضافة Stoppage: أعد stoppages/schedule/summary.
6. لا SignalR أو خدمة refresh جديدة للصفحة؛ الطلبات حسب فتح الصفحة وتغيير الفلاتر والحفظ.

## Feed وتجربة

الملف: `scripts/seed_planning_page_data.sql`.

- اختَر قاعدة البيانات الصحيحة في SSMS ثم FactoryId وLineId وDemoDate في أول السكريبت.
- افتراضيًا `@Commit=0`: يعرض ما سيضاف ثم Rollback. لجعل البيانات دائمة غيرها إلى 1 وأعد التشغيل.
- يضيف منتجًا و3 مواد وBOM ومسارًا وخطواته و5 باتشات تجريبية وجداولها وتوقفًا و3 سجلات MRP.
- إحدى الباتشات planned بدون Schedule لتجربة زر Add. لا يضيف telemetries ولا يحدّث Production ولا مخزون Silo.
- يبحث عن فترات خالية على اللاين داخل DemoDate. لو اليوم ممتلئ يرجع خطأ ويعمل Rollback؛ غير اليوم/اللاين، لا تحذف حجوزاتك.
- إعادة التشغيل لنفس اليوم واللاين لا تكرر سجلاته. لا يغيّر سجلات حقيقية أو يحذفها، لكنه يضيف بيانات مرئية لباقي شاشات الباتش/المنتجات: استخدم بيئة اختبار.
- يعرض TestUrl بـ Duration=5 وحدود اليوم الذي زرع فيه البيانات. استخدم **نفس الفلاتر** لكل طلبات العرض. Current shift ليس مضمونًا أن يحتوي الفترات الخالية المختارة.
- الأرقام وحالات الباتشات synthetic لاختبار العرض، وليست محاكاة تشغيل واقعية. لا يشغل/يكمل باتشات حقيقية ولا يغيرها.
- لم يتم تشغيل السكريبت على SQL Server خلال التنفيذ؛ راجع نتيجة Dry Run قبل الحفظ.

## التحقق

```powershell
dotnet build src/API/API.csproj -c Release
dotnet run --project tests/Planning.RegressionTests
```

الاختبارات offline تغطي قواعد التداخل والحسابات والـ DTO والـ Auth وموديل EF وترجمة SQL.
اختبار تكامل مطلوب على قاعدة اختبار بعد Migration: اطلب جدولة وتوقف متداخلين بالتوازي؛ يجب أن ينجح واحد والآخر يرجع 409. جرّب أيضًا حدود متجاورة (مسموحة)، نطاق عميل/لاين آخر (403)، وطلبًا مكررًا لنفس الباتش (409).
