using API.Common;
using API.Contracts;
using Application.Abstractions.Identity;
using Application.Abstractions.Services;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Users;

[ApiController]
[Route("v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] bool? isActive, [FromQuery] int? factoryId, [FromQuery] string? email)
    {
        var result = await _users.GetUsersAsync(new UserQuery(role, isActive, factoryId, email));
        return this.ToApiActionResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var result = await _users.GetUserByIdAsync(id);
        return this.ToApiActionResult(result);
    }

    [HttpGet("expert-board-options")]
    public IActionResult GetExpertBoardOptions()
    {
        var options = Enum.GetValues<ExpertBoardType>()
            .Select(x => new
            {
                value = (int)x,
                label = x.ToString()
            })
            .ToArray();

        return Ok(ApiResponse<object>.Ok(options));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _users.CreateUserAsync(new CreateUserCommand(
            request.Email,
            request.FullName,
            request.Password,
            request.CustomerId ?? GetCurrentCustomerId(),
            request.Role,
            request.FactoryId,
            request.LineId,
            request.FactoryIds,
            request.LineIds,
            request.ExpertBoards,
            request.PhoneNumber,
            request.IsActive));

        return this.ToApiActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var result = await _users.UpdateUserAsync(id, new UpdateUserCommand(
            request.Email,
            request.FullName,
            request.Role,
            request.FactoryId,
            request.LineId,
            request.FactoryIds,
            request.LineIds,
            request.ExpertBoards,
            request.PhoneNumber,
            request.IsActive));

        return this.ToApiActionResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await _users.DeactivateUserAsync(id);
        return this.ToApiActionResult(result);
    }

    [HttpPut("{id:int}/change-password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        var result = await _users.ChangePasswordAsync(new ChangePasswordCommand(id, request.CurrentPassword, request.NewPassword));
        return this.ToApiActionResult(result);
    }

    [HttpPut("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPasswordByAdmin(int id, [FromBody] AdminResetPasswordRequest request)
    {
        var result = await _users.AdminResetPasswordAsync(new AdminResetPasswordCommand(id, request.NewPassword));
        return this.ToApiActionResult(result);
    }

    private int? GetCurrentCustomerId()
    {
        var idValue = User.FindFirstValue("customerId");
        return int.TryParse(idValue, out var customerId) ? customerId : null;
    }
}
