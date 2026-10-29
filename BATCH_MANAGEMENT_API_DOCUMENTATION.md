# Batch Management — UI/API Contract and Use-Case Audit

تمت المراجعة مقابل صور واجهة Batch Management وضد Domain 4 (UC-B01 إلى UC-B14) في
`geniesys_mes_usecases (1).pdf`، الصفحات 24–32.

## 1. قواعد عامة للربط

- المسار الأساسي المقترح للفرونت هو `/v1/batches`. مسارات `/v1/ui/batches/*` القديمة ما زالت موجودة للتوافق، لكن لا يفضّل بدء ربط جديد عليها.
- كل التواريخ المخزنة/المرجعة من الـAPI تكون UTC بصيغة ISO-8601.
- كل responses داخل envelope موحّد:

```json
{
  "success": true,
  "data": {},
  "meta": null,
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

- عند عدم وجود batch بالـID يرجع `404` مع:

```json
{
  "success": false,
  "data": null,
  "meta": null,
  "error": { "code": "not_found", "message": "Batch 99 was not found.", "details": null },
  "ts": "2026-07-19T10:30:00Z"
}
```

## 2. خريطة الـUI والـendpoints

| جزء الـUI | Method وEndpoint | المصدر | Static؟ |
|---|---|---|---|
| UID legend/format | `GET /v1/ui/batches/uid-legend` | Metadata لتعريف الصيغة | نعم، metadata فقط وليست بيانات تشغيل |
| Kanban | `GET /v1/batches/kanban` | Batches + SKUs + Lines + Alarms | لا |
| Table view | `GET /v1/batches/table` | Batches + SKUs + Lines + Alarms | لا |
| Search/status/filter bar | query params على table/kanban: `search`, `status`, `lineId`, `factoryId`, `from`, `to`, `duration` | Database query/read model | لا |
| Batch stages dropdown | `GET /v1/batches/stages` | Static workflow catalog | نعم |
| View batch modal | `GET /v1/batches/{id}/detail` | Batch + BOM actuals + stage logs + alarms | لا |
| New Batch form dropdowns | `GET /v1/batches/form-options` أو `?factoryId=1` | Factories + Lines + SKUs المسموحة من JWT | لا؛ ما عدا status catalog |
| New Batch UID preview | `GET /v1/batches/preview-uid` | BatchUidSequences + Customer من JWT | لا؛ preview محسوب من DB ولا يحجز الرقم |
| New Batch SKU dropdown | `GET /v1/batches/form-options` | Active SKUs لنفس Customer الموجود في JWT | لا |
| New Batch line dropdown | `GET /v1/batches/form-options?factoryId=1` | ProductionLines المسموحة للمستخدم والتابعة للمصنع | لا |
| New Batch shift dropdown | `GET /v1/shifts` | Shifts | لا |
| Create New Batch | `POST /v1/batches/manual` | يكتب Batch وBatchMaterials وBatchStageLog | لا |
| Start Batch | `POST /v1/batches/{id}/start` | workflow + DB update | لا |
| Advance Stage | `POST /v1/batches/{id}/advance-stage` | workflow + DB update/log | لا |
| Complete Batch | `POST /v1/batches/{id}/complete` | workflow + DB update/log | لا |
| Split Batch | `POST /v1/batches/{id}/split` | transaction تنشئ child batches وBOMs | لا |
| BOM/dispensing rows | `GET /v1/batches/{id}/materials` | BatchMaterials + Materials | لا |
| Manual weight bridge | `POST /v1/batches/{id}/materials/{materialId}/dispense` | request حاليًا ثم يحفظ actual/deviation/alarm في DB | لا؛ المدخل manual مؤقت لحين scale telemetry |
| Stage timeline | `GET /v1/batches/{id}/stage-log` | BatchStageLogs | لا |
| History list | `GET /v1/batches/history` | finished/cancelled Batches + SKU/Line/Machine | لا |
| History detail/timeline modal | `GET /v1/batches/{id}/history-detail` | BatchStageLogs + BatchMaterials | لا |
| Import button | لا يوجد batch-import endpoint مكتمل حاليًا | ERP integration مطلوب | غير مطبق |
| Edit Batch button | لا يوجد workflow آمن ومخصص للتعديل حاليًا | المطلوب تحديد الحقول القابلة للتعديل حسب الحالة | غير مطبق؛ لا يربط على generic PUT |
| History PDF export | لا يوجد endpoint حاليًا | يحتاج PDF + signatures + tamper-evident hash | غير مطبق |

مهم: بيانات Batch History وBatch Detail كانت ترجع demo rows عند فراغ قاعدة البيانات. تم حذف هذا السلوك؛ القوائم الفارغة ترجع `[]` والـID غير الموجود يرجع `404`.

## 3. JSON contracts

### 3.1 UID legend

`GET /v1/ui/batches/uid-legend`

```json
{
  "success": true,
  "data": {
    "format": "BT-YYMM-SSSSS-ORG",
    "examples": ["BT-2606-00001-EGY", "BT-2406-00033-EGY"],
    "types": ["BT", "RM", "SF", "FG"]
  },
  "meta": {},
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

التصنيف: **Static metadata** مقبول؛ لا يمثل rows يفترض قراءتها من DB.

### 3.2 Table view

`GET /v1/batches/table?status=in_progress&search=BT-2607&lineId=1&from=2026-07-01&to=2026-07-31`

```json
{
  "success": true,
  "data": [
    {
      "id": 12,
      "uid": "BT-2607-00012-EGY",
      "skuId": 4,
      "skuCode": "SKU-PARA-500",
      "skuName": "Paracetamol 500mg",
      "lineId": 1,
      "lineName": "Line 1 — Tablets",
      "status": "in_progress",
      "stage": "dispense",
      "plannedQty": 200000,
      "producedQty": 0,
      "rejectedQty": 0,
      "progressPct": 0,
      "plannedStart": "2026-07-19T06:00:00Z",
      "plannedEnd": "2026-07-19T14:00:00Z",
      "actualStart": "2026-07-19T06:05:00Z",
      "actualEnd": null,
      "priority": 5,
      "hasActiveAlarm": false,
      "detailEndpoint": "/v1/batches/12/detail"
    }
  ],
  "meta": {},
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

التصنيف: **Dynamic DB data**. `progressPct` فقط قيمة محسوبة من `ProducedQty / PlannedQty` وليست demo data.

### 3.3 Kanban

`GET /v1/batches/kanban` ويقبل نفس filters السابقة.

```json
{
  "success": true,
  "data": [
    { "key": "in_progress", "title": "In Progress", "cards": [] },
    { "key": "production", "title": "Production", "cards": [] },
    { "key": "finished", "title": "Finished", "cards": [] },
    { "key": "planned", "title": "Planned", "cards": [] }
  ],
  "meta": {},
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

كل عنصر في `cards` له نفس contract الخاص بصف Table. التصنيف: **Dynamic DB data**.

### 3.4 Create New Batch

`GET /v1/batches/preview-uid`

```json
{ "success": true, "data": "BT-2607-00013-EGY", "meta": null, "error": null, "ts": "2026-07-19T10:30:00Z" }
```

`POST /v1/batches/manual`

```json
{
  "skuId": 4,
  "factoryId": 1,
  "lineId": 1,
  "plannedQty": 200000,
  "plannedStart": "2026-07-23T14:00:00Z",
  "plannedEnd": null,
  "shiftId": 1,
  "priority": 5,
  "notes": "Trial batch"
}
```

قواعد الوصول: لا يرسل الفرونت `customerId`؛ الخادم يقرأه من JWT. كل من `factoryId` و`lineId` قيمة واحدة فقط. يجب أن يكون المصنع ضمن صلاحيات المستخدم وتابعًا لنفس العميل، ويجب أن يكون الخط ضمن صلاحياته وتابعًا للمصنع المختار. نفس نطاق JWT مطبق على table وkanban وhistory وdetail وكل workflow actions.

`status` لا يُرسل عند الإنشاء؛ السيرفر ينشئ الـBatch دائمًا بحالة `planned` طبقًا لـUC-B03. الحقول `plannedEnd` و`shiftId` و`priority` اختيارية، والقيمة الافتراضية لـ`priority` هي `5`.

```json
{
  "success": true,
  "data": {
    "type": "Type B/C Workflow Action",
    "useCase": "UC-B05/UC-B06/UC-B11/UC-B12 depending on called endpoint",
    "message": "Manual batch created with UID and BOM lines.",
    "data": {
      "id": 13,
      "uid": "BT-2607-00013-EGY",
      "status": "planned",
      "stage": "warehouse",
      "plannedQty": 200000,
      "producedQty": 0,
      "rejectedQty": 0,
      "actualStart": null,
      "actualEnd": null,
      "priority": 5,
      "refreshEndpoint": "/v1/ui/batches/13/detail"
    },
    "futureNote": "DTO action response. Refresh the related /v1/ui/* page endpoint after successful action."
  },
  "meta": null,
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

الـworkflow يتحقق من Customer/SKU/Line/Shift، active SKU، positive quantity، date range، وجود BOM وactive route، ثم يولد UID وينسخ BOM بكميات proportional إلى `StandardBatchSize` داخل transaction. التصنيف: **Dynamic DB write/read**.

### 3.5 Batch detail modal

`GET /v1/batches/13/detail`

```json
{
  "success": true,
  "data": {
    "batch": {
      "id": 13,
      "uid": "BT-2607-00013-EGY",
      "customerId": 1,
      "skuId": 4,
      "skuCode": "SKU-PARA-500",
      "skuName": "Paracetamol 500mg",
      "lineId": 1,
      "lineName": "Line 1 — Tablets",
      "routeId": 2,
      "status": "planned",
      "stage": "warehouse",
      "plannedQty": 200000,
      "producedQty": 0,
      "rejectedQty": 0,
      "progressPct": 0,
      "plannedStart": "2026-07-23T14:00:00Z",
      "plannedEnd": null,
      "actualStart": null,
      "actualEnd": null,
      "priority": 5,
      "notes": "Trial batch"
    },
    "materials": [
      {
        "id": 44,
        "materialId": 7,
        "materialCode": "RM-LACTOSE",
        "materialName": "Lactose Monohydrate",
        "plannedQty": 80,
        "dispensedQty": 0,
        "deviationPct": null,
        "status": "pending",
        "scaleId": null,
        "dispenseStartedAt": null,
        "dispenseCompletedAt": null
      }
    ],
    "stageTimeline": [
      { "fromStage": null, "toStage": "warehouse", "transitionedAt": "2026-07-19T10:30:00Z", "notes": "Manual batch created." }
    ],
    "alarms": []
  },
  "meta": {},
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

التصنيف: **Dynamic DB data** بالكامل.

### 3.6 Start / advance / complete

`POST /v1/batches/13/start`

```json
{ "notes": "Pre-start checklist approved" }
```

Start يقبل فقط `planned`، يشترط route وBOM صالحين، ويمنع وجود batch آخر `in_progress` على نفس line.

`POST /v1/batches/13/advance-stage`

```json
{ "notes": "Stage signed off" }
```

الانتقالات لا تسمح بالقفز: `warehouse -> dispense -> production -> finished`. الانتقال من dispense إلى production يتوقف إذا كان أي BOM line ليس `complete`.

`POST /v1/batches/13/complete`

```json
{ "producedQty": 198500, "rejectedQty": 1500, "notes": "QA release reference QA-441" }
```

Complete يشترط وصول stage إلى `finished` ويسجل `ActualEnd` وstage log. التصنيف للثلاثة: **Dynamic workflow + DB**.

### 3.7 Dispensing / BOM actuals

`GET /v1/batches/13/materials`

```json
{
  "success": true,
  "data": [
    {
      "id": 44,
      "batchId": 13,
      "materialId": 7,
      "materialCode": "RM-LACTOSE",
      "materialName": "Lactose Monohydrate",
      "plannedQty": 80,
      "dispensedQty": 24.82,
      "deviationPct": 68.975,
      "status": "pending",
      "scaleId": 2,
      "type": "D read model, C when used by dispense endpoint"
    }
  ],
  "meta": {},
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

`POST /v1/batches/13/materials/7/dispense`

```json
{ "scaleId": 2, "dispensedQty": 79.8, "timestamp": "2026-07-19T10:35:00Z" }
```

الحالي هو **manual bridge وليس static response**: القيمة تأتي من request مؤقتًا، ثم يحسب السيرفر deviation بتسامح الـSKU/material الموجود في BillOfMaterials، ويحفظ النتيجة. عند تجاوز tolerance ينشئ AlarmEvent ويضع السطر `deviation`، وبالتالي يمنع advance إلى production. يحتاج Scale له `CalibratedAt` و`CalibrationCert`.

المستقبل: `dispensedQty` يجب أن يأتي من stable scale telemetry بدل request، مع operator PIN والتحقق من انتهاء شهادة المعايرة.

### 3.8 Split batch

`POST /v1/batches/13/split`

```json
{ "splitQty": 80000, "reason": "Line capacity constraint" }
```

```json
{
  "success": true,
  "data": {
    "message": "Batch split successfully.",
    "data": [
      { "id": 14, "uid": "BT-2607-00014-EGY", "status": "in_progress", "stage": "dispense", "plannedQty": 80000, "parentBatchId": 13 },
      { "id": 15, "uid": "BT-2607-00015-EGY", "status": "in_progress", "stage": "dispense", "plannedQty": 120000, "parentBatchId": 13 }
    ]
  },
  "meta": null,
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

يتم داخل transaction؛ الطفلان يحتفظان بالحالة والمرحلة، يحملان `ParentBatchId`، ولكل منهما UID جديد وBOM proportional. الأصل يصبح `split`. التصنيف: **Dynamic DB workflow**.

### 3.9 History

`GET /v1/batches/history?search=BT-2607&status=finished&from=2026-07-01&to=2026-07-31`

```json
{
  "success": true,
  "data": [
    {
      "id": 13,
      "batchName": "BT-2607-00013-EGY",
      "skuCode": "SKU-PARA-500",
      "skuName": "Paracetamol 500mg",
      "jobOrder": "13",
      "timestamp": "2026-07-19T13:30:00Z",
      "machineName": "Tablet Press 1"
    }
  ],
  "meta": {},
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

`GET /v1/batches/13/history-detail`

```json
{
  "success": true,
  "data": {
    "uid": "BT-2607-00013-EGY",
    "productName": "Paracetamol 500mg",
    "timeline": [
      { "name": "warehouse", "fromStage": null, "start": "2026-07-19T06:00:00Z", "end": "2026-07-19T07:00:00Z", "color": "blue" },
      { "name": "production", "fromStage": "dispense", "start": "2026-07-19T09:00:00Z", "end": "2026-07-19T13:30:00Z", "color": "yellow" },
      { "name": "finished", "fromStage": "production", "start": "2026-07-19T13:30:00Z", "end": "2026-07-19T13:30:00Z", "color": "green" }
    ],
    "materials": [
      { "materialName": "Lactose Monohydrate", "materialCode": "RM-LACTOSE", "bagWeight": "80 kg", "actualWeight": "79.8 kg", "deviationPct": "0.25%", "timestamp": "2026-07-19 10:35:00" }
    ]
  },
  "meta": {},
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

التصنيف: **Dynamic DB data**. أزمنة الرسم مأخوذة من `BatchStageLogs` فقط؛ لا يتم اختلاق مدد عند غياب logs.

## 4. مراجعة UC-B01 إلى UC-B14

| Use case | الحالة الحالية | الموجود | الناقص قبل اعتبارها Complete |
|---|---|---|---|
| UC-B01 Receive ERP order | غير مطبق | CRUD/manual batch فقط | endpoint ERP مخصص، external order id + duplicate constraint، validation، acknowledgement |
| UC-B02 Receive BOM from ERP | غير مطبق | master BOM ونسخه في manual create | endpoint بالـUID، validation مقابل recipe، منع التعديل بعد start |
| UC-B03 Manual create | مطبق جزئيًا قويًا | validation + UID + route + proportional BOM + transaction | Factory Manager authorization/approval وoptional ERP push |
| UC-B04 Allocate UID | مطبق | monthly/customer sequence + unique DB index + preview | إضافة DB concurrency token/sequence أو retry لضمان atomicity تحت الضغط العالي؛ منع تغيير UID عبر generic update |
| UC-B05 Start batch | مطبق جزئيًا | planned-only، BOM، route، one active batch/line، timestamp | silo stock، overdue PM checks، ERP start notification، supervisor authorization |
| UC-B06 Advance stage | مطبق جزئيًا | ordered state machine، dispense completeness، stage log | supervisor e-signature/actor id، lock قوي للمرحلة السابقة |
| UC-B07 Dispense/capture scale | manual bridge فقط | calibrated scale metadata، actual save، stage validation | MQTT stable reading، online check، calibration expiry، operator PIN |
| UC-B08 Planned vs actual | مطبق جزئيًا | formula، BOM tolerance، persisted result، block progression | tolerance override per batch/material history، immutable multiple attempts/sign-offs |
| UC-B09 Stage timestamp | مطبق جزئيًا | UTC BatchStageLog لكل transition | `TransitionedById` من authenticated user، منع CRUD mutation على logs، local-time rendering responsibility |
| UC-B10 Deviation alarm | مطبق جزئيًا | high AlarmEvent + blocked advance | SignalR push، accept/reject endpoint، justification، void/re-dispense audit trail |
| UC-B11 Split batch | core مطبق | before production، transaction، two UIDs، parent ref، proportional BOM | Factory Manager authorization، خيار manual BOM distribution، richer audit |
| UC-B12 Complete + ERP | مطبق جزئيًا | finished-stage gate، quantities، status/end/log | QA release entity، yield/deviation/duration summary، ERP confirmation/retry/alert، final record lock |
| UC-B13 Kanban | مطبق جزئيًا | DB cards + filters + columns + detail link | SignalR/WebSocket refresh، OEE-risk colour، enforced user factory scope |
| UC-B14 Detail | مطبق جزئيًا | metadata + BOM actuals + deviations + timeline + alarms | route step status، PDF export، signatures، tamper-evident hash |

## 5. الموجود زيادة عن صور الـUI

- Start, Advance Stage, Complete, Split, manual dispense endpoints موجودة حتى لو كل الأزرار غير ظاهرة في screenshots الحالية.
- `GET /v1/batches/by-uid/{uid}` للبحث المباشر بالـUID.
- `GET /v1/batches/{id}/stage-log` و`GET /v1/batches/{id}/materials` كـcomponent endpoints مستقلة.
- Legacy aggregate endpoint `/v1/ui/batches` ما زال موجودًا لكنه مخفي من Swagger ومخصص initial hydration فقط.

## 6. قرارات static data

- المسموح static داخل Batch UI حاليًا: UID legend/labels/colours فقط لأنها metadata للعرض.
- غير مسموح static: batches، products، lines، shifts، BOM rows، quantities، statuses، timestamps، history، alarms. كلها الآن تأتي من DB أو ترجع empty/404.
- البيانات التي ستنتج مستقبلًا من SQL views أو telemetry مثل OEE risk، computed stage durations summaries، وlive scale readings يجب أن تحمل بوضوح `source/mode` عند إضافة fallback مؤقت؛ لا يجوز خلطها مع DB rows على أنها حقيقية.

## 7. ملاحظات لازمة قبل production

1. إضافة authentication/authorization policies للأدوار المذكورة في الـPDF؛ وجود `UseAuthorization()` وحده لا يحمي endpoints من دون `[Authorize]`/policies.
2. عدم ربط Edit Batch على generic `PUT /v1/batches/{id}` قبل إنشاء DTO وworkflow يمنع تغيير UID/status/stage والحقول المقفولة.
3. جعل BatchStageLogs وBatchUidSequences read-only خارجيًا أو إخفاء CRUD controllers الخاصة بها.
4. تصميم ERP inbox/outbox وجداول external references/retry قبل تنفيذ UC-B01/B02/B12.
5. تصميم deviation resolution/dispense attempt audit قبل اعتماد UC-B10 تنظيميًا.
6. إضافة pagination حقيقية لـHistory؛ الشاشة تعرض pagination لكن العقد الحالي يرجع القائمة كاملة بعد filters.
