using System.Security.Claims;
using Application.Abstractions.Identity;

namespace Application.Abstractions.Services;

public interface IUserManagementService
{
    Task<IdentityResultDto<AuthTokenDto>> LoginAsync(LoginCommand command);
    Task<IdentityResultDto<AuthTokenDto>> RefreshAsync(RefreshTokenCommand command);
    Task<IdentityResultDto<object>> LogoutAsync(string refreshToken);
    Task<IdentityResultDto<AuthUserDto>> GetCurrentUserAsync(ClaimsPrincipal principal);
    Task<IdentityResultDto<object>> ForgotPasswordAsync(ForgotPasswordCommand command);
    Task<IdentityResultDto<object>> VerifyPasswordResetTokenAsync(VerifyPasswordResetTokenCommand command);
    Task<IdentityResultDto<object>> ResetPasswordAsync(ResetPasswordCommand command);

    Task<IdentityResultDto<IReadOnlyList<AuthUserDto>>> GetUsersAsync(UserQuery query);
    Task<IdentityResultDto<AuthUserDto>> GetUserByIdAsync(int id);
    Task<IdentityResultDto<AuthUserDto>> CreateUserAsync(CreateUserCommand command);
    Task<IdentityResultDto<AuthUserDto>> UpdateUserAsync(int id, UpdateUserCommand command);
    Task<IdentityResultDto<object>> DeactivateUserAsync(int id);
    Task<IdentityResultDto<object>> ChangePasswordAsync(ChangePasswordCommand command);
    Task<IdentityResultDto<object>> AdminResetPasswordAsync(AdminResetPasswordCommand command);

    Task<IdentityResultDto<IReadOnlyList<RoleDto>>> GetRolesAsync();
    Task<IdentityResultDto<RoleDto>> GetRoleByIdAsync(int id);
    Task<IdentityResultDto<RoleDto>> CreateRoleAsync(CreateRoleCommand command);
    Task<IdentityResultDto<RoleDto>> UpdateRoleAsync(int id, UpdateRoleCommand command);
    Task<IdentityResultDto<object>> DeleteRoleAsync(int id);
}

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetAsync(string recipientEmail, string recipientName, string code, CancellationToken cancellationToken = default);
}
