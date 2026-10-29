using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Alarms;

// SMS targets are reserved for a future UI action and are not exposed as direct CRUD.
[ApiController]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("v1/alarm-rule-sms-targets")]
public sealed class AlarmRuleSmsTargetsController : ControllerBase
{
}
