using API.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Common;

[ApiController]
public abstract class StaticControllerBase : ControllerBase
{
    protected IActionResult StaticOk(string endpoint, object? parameters = null, string? message = null)
    {
        var result = new StaticEndpointResult(
            endpoint,
            message ?? "Endpoint route is ready. Connect domain/service logic when the use-case is implemented.",
            parameters);

        return Ok(ApiResponse<StaticEndpointResult>.Ok(result));
    }
}
