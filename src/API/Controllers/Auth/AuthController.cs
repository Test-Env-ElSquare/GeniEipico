using API.Common;
using API.Contracts;
using Application.Abstractions.Identity;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Auth;

[ApiController]
[Route("v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserManagementService _users;

    public AuthController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _users.LoginAsync(new LoginCommand(request.Email, request.Password));
        return this.ToApiActionResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _users.RefreshAsync(new RefreshTokenCommand(request.RefreshToken));
        return this.ToApiActionResult(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? request)
    {
        var result = await _users.LogoutAsync(request?.RefreshToken ?? string.Empty);
        return this.ToApiActionResult(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var result = await _users.GetCurrentUserAsync(User);
        return this.ToApiActionResult(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _users.ForgotPasswordAsync(new ForgotPasswordCommand(request.Email));
        return this.ToApiActionResult(result);
    }

    [HttpPost("verify-reset-code")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyResetToken([FromBody] VerifyPasswordResetTokenRequest request)
    {
        var result = await _users.VerifyPasswordResetTokenAsync(
            new VerifyPasswordResetTokenCommand(request.Email, request.Code));
        return this.ToApiActionResult(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _users.ResetPasswordAsync(new ResetPasswordCommand(request.Email, request.Code, request.NewPassword));
        return this.ToApiActionResult(result);
    }

    [HttpPost("signup")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Signup([FromBody] CreateUserRequest request)
    {
        var result = await _users.CreateUserAsync(ToCreateUserCommand(request, GetCurrentCustomerId()));
        return this.ToApiActionResult(result);
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("not_authenticated", "No authenticated user was found."));
        }

        var result = await _users.ChangePasswordAsync(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword));
        return this.ToApiActionResult(result);
    }

    //[HttpPost("mfa/setup")]
    //public IActionResult MfaSetup()
    //{
    //    return BadRequest(ApiResponse<object>.Fail("mfa_not_implemented", "MFA is not implemented yet in this project version."));
    //}


    //[HttpPost("mfa/verify")]
    //public IActionResult MfaVerify([FromBody] MfaVerifyRequest request)
    //{
    //    return BadRequest(ApiResponse<object>.Fail("mfa_not_implemented", "MFA is not implemented yet in this project version."));
    //}

    private int? GetCurrentCustomerId()
    {
        var idValue = User.FindFirstValue("customerId");
        return int.TryParse(idValue, out var customerId) ? customerId : null;
    }

    private static CreateUserCommand ToCreateUserCommand(CreateUserRequest request, int? currentCustomerId)
    {
        return new CreateUserCommand(
            request.Email,
            request.FullName,
            request.Password,
            request.CustomerId ?? currentCustomerId,
            request.Role,
            request.FactoryId,
            request.LineId,
            request.FactoryIds,
            request.LineIds,
            request.ExpertBoards,
            request.PhoneNumber,
            request.IsActive);
    }
}
