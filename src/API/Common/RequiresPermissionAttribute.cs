using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace API.Common;

/// <summary>
/// Applies an Eipico permission to a Genie controller or action without
/// introducing a second authentication/JWT pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresPermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] requiredPermissions;

    public RequiresPermissionAttribute(params string[] requiredPermissions)
    {
        this.requiredPermissions = requiredPermissions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Preserve Genie administrator semantics: an Admin can use all
        // Eipico functionality without duplicating every permission claim.
        if (principal.IsInRole("Admin") || HasPermission(principal, EipicoPermissions.Everything))
        {
            return;
        }

        if (requiredPermissions.Length == 0 ||
            requiredPermissions.Any(permission => HasPermission(principal, permission)))
        {
            return;
        }

        context.Result = new ForbidResult();
    }

    private static bool HasPermission(ClaimsPrincipal principal, string permission)
        => principal.Claims.Any(claim =>
            (string.Equals(claim.Type, EipicoPermissions.ClaimType, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
}
