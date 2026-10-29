using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.Security;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Users;

[Route("v1/audit")]
public class AuditController : CrudController<AuditLog, IAuditLogService>
{
    public AuditController(IAuditLogService service) : base(service)
    {
    }

    // Endpoint Type D: Read Model / View. Audit export skipped for now.
    [HttpPost("export")]
    public IActionResult Export([FromBody] ExportRequest request)
    {
        return StaticOk("/v1/audit/export", request);
    }
}
