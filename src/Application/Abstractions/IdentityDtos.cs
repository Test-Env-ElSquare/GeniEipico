using System.Security.Claims;

namespace Application.Abstractions.Identity;

public sealed record IdentityResultDto<T>(
    bool Succeeded,
    string Code,
    string Message,
    T? Data = default,
    IReadOnlyList<string>? Errors = null)
{
    public static IdentityResultDto<T> Ok(T? data, string message = "Success")
        => new(true, "ok", message, data);

    public static IdentityResultDto<T> Fail(string code, string message, IReadOnlyList<string>? errors = null)
        => new(false, code, message, default, errors);
}

public sealed record AuthUserDto(
    int Id,
    string Email,
    string UserName,
    string FullName,
    int CustomerId,
    int? FactoryId,
    int? LineId,
    IReadOnlyList<int> FactoryIds,
    IReadOnlyList<int> LineIds,
    IReadOnlyList<int> ExpertBoards,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    IReadOnlyList<ClaimDto> Claims);

public sealed record AuthTokenDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc,
    AuthUserDto User);

public sealed record LoginCommand(string Email, string Password);
public sealed record RefreshTokenCommand(string RefreshToken);
public sealed record ForgotPasswordCommand(string Email);
public sealed record VerifyPasswordResetTokenCommand(string Email, string Code);
public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword);
public sealed record ChangePasswordCommand(int UserId, string CurrentPassword, string NewPassword);
public sealed record AdminResetPasswordCommand(int UserId, string NewPassword);

public sealed record UserQuery(string? Role = null, bool? IsActive = null, int? FactoryId = null, string? Email = null);

public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    int? CustomerId,
    string? Role = null,
    int? FactoryId = null,
    int? LineId = null,
    IReadOnlyList<int>? FactoryIds = null,
    IReadOnlyList<int>? LineIds = null,
    IReadOnlyList<int>? ExpertBoards = null,
    string? PhoneNumber = null,
    bool IsActive = true);

public sealed record UpdateUserCommand(
    string? Email = null,
    string? FullName = null,
    string? Role = null,
    int? FactoryId = null,
    int? LineId = null,
    IReadOnlyList<int>? FactoryIds = null,
    IReadOnlyList<int>? LineIds = null,
    IReadOnlyList<int>? ExpertBoards = null,
    string? PhoneNumber = null,
    bool? IsActive = null);

public sealed record ClaimDto(string Type, string Value);

public sealed record RoleDto(
    int Id,
    string Name,
    IReadOnlyList<ClaimDto> Claims,
    int UsersCount);

public sealed record CreateRoleCommand(string Name, IReadOnlyList<ClaimDto>? Claims = null);
public sealed record UpdateRoleCommand(string? Name = null, IReadOnlyList<ClaimDto>? Claims = null);
