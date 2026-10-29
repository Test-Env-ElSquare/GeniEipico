# SKU Management API

## UI mapping

| UI area | Endpoint | DB/static |
|---|---|---|
| SKU management table | `GET /v1/skus` | Database |
| Batch/New Batch SKU dropdown | `GET /v1/skus/dropdown?customerId=1&activeOnly=true` | Database |
| Create SKU | `POST /v1/skus/create` | Database transaction |
| View/Edit SKU | `GET /v1/skus/{id}/detail` | Database |
| Save Edit | `PUT /v1/skus/{id}/update` | Database transaction |
| Deactivate | `DELETE /v1/skus/{id}/deactivate` | Database; blocked for open batches |
| Raw-material dropdown | `GET /v1/materials/dropdown?customerId=1&materialType=RM&activeOnly=true` | Database |
| SKU BOM | `GET /v1/skus/{id}/bom` | Database |
| Add BOM row separately | `POST /v1/skus/{id}/bom` | Database |
| Update BOM row | `PUT /v1/skus/{id}/bom/{bomId}` | Database |
| Delete BOM row | `DELETE /v1/skus/{id}/bom/{bomId}` | Database |

`id` مطلوب فقط لقراءة/تعديل resource موجود. إنشاء SKU جديد لا يرسل `id`.

## Dropdown response

`GET /v1/skus/dropdown?customerId=1`

```json
{
  "success": true,
  "data": [
    {
      "value": 1,
      "label": "Paracetamol 500mg Tablets",
      "code": "SKU-PARA-500",
      "uom": "tablet",
      "standardBatchSize": 100000
    }
  ],
  "meta": null,
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

## Create SKU

`POST /v1/skus/create`

```json
{
  "customerId": 1,
  "code": "SKU-PARA-650",
  "name": "Paracetamol 650mg Tablets",
  "category": "Analgesic",
  "uom": "tablet",
  "standardBatchSize": 100000,
  "shelfLifeMonths": 34,
  "ratedSpeedRpm": 1500,
  "unitWeightG": 0.75,
  "erpSource": false,
  "erpMaterialCode": null,
  "isActive": true,
  "processParameters": [
    { "name": "min_time", "value": "15", "unit": "min", "dataType": "number" },
    { "name": "press_force", "value": "12", "unit": "kN", "dataType": "number" },
    { "name": "drying_temperature", "value": "60", "unit": "C", "dataType": "number" },
    { "name": "coating_thickness", "value": "0.1", "unit": "mm", "dataType": "number" }
  ],
  "rawMaterials": [
    {
      "materialId": 1,
      "qtyPerBatch": 75.5,
      "tolerancePct": 2,
      "sequenceNo": 1,
      "isCritical": true
    },
    {
      "materialId": 3,
      "qtyPerBatch": 120,
      "tolerancePct": 2,
      "sequenceNo": 2,
      "isCritical": false
    }
  ]
}
```

### Field meaning

| Field | Required | Meaning |
|---|---|---|
| `customerId` | yes | Tenant/organization owner |
| `code` | yes | Unique per customer, e.g. `SKU-PARA-650` |
| `name` | yes | Product display name |
| `category` | no | E.g. Analgesic |
| `uom` | yes | Finished-product unit: tablet, capsule, bottle... |
| `standardBatchSize` | yes | Output quantity for which all `qtyPerBatch` BOM values are defined |
| `shelfLifeMonths` | no | Positive integer when supplied |
| `ratedSpeedRpm` | no | Positive integer when supplied |
| `unitWeightG` | no | Weight of one finished unit in grams |
| `erpSource` | yes | `true` when SKU is ERP-managed; otherwise manual MES SKU |
| `erpMaterialCode` | no | ERP product/material reference |
| `processParameters` | no | Generic configurable parameters shown in the design |
| `rawMaterials` | no | BOM rows used by Batch creation |

`rawMaterials[].unit` is not submitted. It is read from the selected Material to prevent a BOM row from contradicting the master Material UOM.

The create transaction validates customer, unique SKU code, positive sizes, unique process-parameter names, unique BOM materials, and active materials belonging to the same customer.

## Raw-material dropdown

`GET /v1/materials/dropdown?customerId=1&materialType=RM`

```json
{
  "success": true,
  "data": [
    {
      "value": 1,
      "label": "Paracetamol Active Ingredient",
      "code": "RM-PARA",
      "unit": "kg",
      "type": "RM"
    }
  ],
  "meta": null,
  "error": null,
  "ts": "2026-07-19T10:30:00Z"
}
```

## Update semantics

`PUT /v1/skus/{id}/update` uses the same JSON shape as Create.

- When `processParameters` or `rawMaterials` is `null`, existing child rows are left unchanged.
- When supplied, the respective collection replaces the current collection.
- Sending `[]` intentionally clears that collection.

## Relationship to Batch Management

When `POST /v1/batches/manual` receives `skuId`, the Batch workflow reads:

- SKU `standardBatchSize`.
- Active production route for the SKU.
- SKU raw materials from BillOfMaterials.

It then creates BatchMaterials using:

```text
factor = batch plannedQty / SKU standardBatchSize
batch material plannedQty = BOM qtyPerBatch × factor
```

Therefore an active SKU needs a valid BOM and active production route before it can create a manual Batch successfully.
