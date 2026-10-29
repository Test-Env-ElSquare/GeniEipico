using API.Common;
using API.Contracts;
using Application.Abstractions.Identity;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppClaimDto = Application.Abstractions.Identity.ClaimDto;

namespace API.Controllers.Users;

[ApiController]
[Route("v1/roles")]
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly IUserManagementService _users;

    public RolesController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var result = await _users.GetRolesAsync();
        return this.ToApiActionResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRole(int id)
    {
        var result = await _users.GetRoleByIdAsync(id);
        return this.ToApiActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var result = await _users.CreateRoleAsync(new CreateRoleCommand(
            request.Name,
            request.Claims?.Select(x => new AppClaimDto(x.Type, x.Value)).ToList()));

        return this.ToApiActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
    {
        var result = await _users.UpdateRoleAsync(id, new UpdateRoleCommand(
            request.Name,
            request.Claims?.Select(x => new AppClaimDto(x.Type, x.Value)).ToList()));

        return this.ToApiActionResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var result = await _users.DeleteRoleAsync(id);
        return this.ToApiActionResult(result);
    }
}
