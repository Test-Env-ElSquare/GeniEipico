using API.Common;
using API.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Erp;

[Route("v1/erp-adapter")]
public class ErpAdapterController : StaticControllerBase
{
    // Endpoint Type B: Workflow / Business Logic. ERP config placeholder.
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return StaticOk("/v1/erp-adapter/config");
    }

    // Endpoint Type B: Workflow / Business Logic. ERP config placeholder.
    [HttpPut("config")]
    public IActionResult UpdateConfig([FromBody] ErpConfigRequest request)
    {
        return StaticOk("/v1/erp-adapter/config", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP connection test placeholder.
    [HttpPost("test-connection")]
    public IActionResult TestConnection()
    {
        return StaticOk("/v1/erp-adapter/test-connection");
    }

    // Endpoint Type D: Read Model / View. ERP sync log skipped for now.
    [HttpGet("sync-log")]
    public IActionResult SyncLog()
    {
        return StaticOk("/v1/erp-adapter/sync-log");
    }

    // Endpoint Type B: Workflow / Business Logic. ERP field mapping placeholder.
    [HttpGet("field-mappings")]
    public IActionResult GetFieldMappings()
    {
        return StaticOk("/v1/erp-adapter/field-mappings");
    }

    // Endpoint Type B: Workflow / Business Logic. ERP field mapping placeholder.
    [HttpPut("field-mappings")]
    public IActionResult UpdateFieldMappings([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/field-mappings", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP inbound validation/import placeholder.
    [HttpPost("inbound/skus")]
    public IActionResult InboundSkus([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/inbound/skus", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP inbound validation/import placeholder.
    [HttpPost("inbound/bom")]
    public IActionResult InboundBom([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/inbound/bom", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP inbound validation/import placeholder.
    [HttpPost("inbound/production-orders")]
    public IActionResult InboundProductionOrders([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/inbound/production-orders", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP inbound validation/import placeholder.
    [HttpPost("inbound/mrp-requirements")]
    public IActionResult InboundMrpRequirements([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/inbound/mrp-requirements", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP inbound validation/import placeholder.
    [HttpPost("inbound/goods-receipt")]
    public IActionResult InboundGoodsReceipt([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/inbound/goods-receipt", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP inbound validation/import placeholder.
    [HttpPost("inbound/routing")]
    public IActionResult InboundRouting([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/inbound/routing", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP webhook placeholder.
    [HttpPost("webhook")]
    public IActionResult Webhook([FromBody] ErpInboundRequest request)
    {
        return StaticOk("/v1/erp-adapter/webhook", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP manual sync placeholder.
    [HttpPost("sync/skus")]
    public IActionResult SyncSkus([FromBody] ErpManualSyncRequest request)
    {
        return StaticOk("/v1/erp-adapter/sync/skus", request);
    }

    // Endpoint Type B: Workflow / Business Logic. ERP manual sync placeholder.
    [HttpPost("sync/production-orders")]
    public IActionResult SyncProductionOrders([FromBody] ErpManualSyncRequest request)
    {
        return StaticOk("/v1/erp-adapter/sync/production-orders", request);
    }

    // Endpoint Type D: Read Model / View. ERP sync status placeholder.
    [HttpGet("sync/status/{jobId}")]
    public IActionResult SyncStatus(string jobId)
    {
        return StaticOk($"/v1/erp-adapter/sync/status/{jobId}", new { jobId });
    }
}
