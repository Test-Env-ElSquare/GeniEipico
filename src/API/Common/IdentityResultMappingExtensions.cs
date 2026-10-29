using Application.Abstractions.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Common;

public static class IdentityResultMappingExtensions
{
    public static IActionResult ToApiActionResult<T>(this ControllerBase controller, IdentityResultDto<T> result)
    {
        if (result.Succeeded)
        {
            return controller.Ok(ApiResponse<T>.Ok(result.Data, new { result.Code, result.Message }));
        }

        var response = ApiResponse<T>.Fail(result.Code, result.Message, result.Errors);

        return result.Code switch
        {
            "invalid_credentials" or "not_authenticated" or "invalid_refresh_token" or "refresh_token_expired" => controller.Unauthorized(response),
            "user_not_found" or "role_not_found" or "customer_not_found" or "factory_not_found" or "line_not_found" => controller.NotFound(response),
            "email_already_exists" or "role_already_exists" or "role_in_use" => controller.Conflict(response),
            _ => controller.BadRequest(response)
        };
    }
}
