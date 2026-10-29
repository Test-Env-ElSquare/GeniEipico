using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Alarms;

// Email targets are managed atomically through POST/PUT /v1/alarm-rules.
// Keeping an empty controller avoids exposing unsafe cross-customer generic CRUD routes.
[ApiController]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("v1/alarm-rule-email-targets")]
public sealed class AlarmRuleEmailTargetsController : ControllerBase
{
}
