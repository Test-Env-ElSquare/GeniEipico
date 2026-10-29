using Application.Abstractions.Identity;
using Application.Abstractions.Services;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AppClaimDto = Application.Abstractions.Identity.ClaimDto;

namespace Infrastructure.Identity;

public sealed class UserManagementService : IUserManagementService
{
    private const string RefreshTokenProvider = "MESPlatForm";
    private const string RefreshTokenName = "RefreshToken";
    private const string RefreshTokenExpiryName = "RefreshTokenExpiresAtUtc";
    private const string PasswordResetProvider = "MESPasswordReset";
    private const string PasswordResetCodeName = "ResetCodeHash";
    private const string PasswordResetTokenName = "ResetToken";
    private const string PasswordResetExpiryName = "ResetCodeExpiresAtUtc";
    private const string FactoryAccessClaimType = "factoryId";
    private const string LineAccessClaimType = "lineId";
    internal const string ExpertBoardAccessClaimType = "expertBoard";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly MesDbContext _db;
    private readonly JwtOptions _jwtOptions;
    private readonly IPasswordResetEmailSender _passwordResetEmailSender;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        MesDbContext db,
        IOptions<JwtOptions> jwtOptions,
        IPasswordResetEmailSender passwordResetEmailSender,
        ILogger<UserManagementService> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _jwtOptions = jwtOptions.Value;
        _passwordResetEmailSender = passwordResetEmailSender;
        _logger = logger;

        // Backward-compatible fallback if IOptions is empty for any reason.
        if (string.IsNullOrWhiteSpace(_jwtOptions.Key))
        {
            _jwtOptions.Key = configuration["Jwt:Key"] ?? string.Empty;
            _jwtOptions.Issuer = configuration["Jwt:Issuer"] ?? "MESPlatForm";
            _jwtOptions.Audience = configuration["Jwt:Audience"] ?? "MESPlatFormClient";
            _jwtOptions.DurationMinutes = int.TryParse(configuration["Jwt:DurationMinutes"], out var minutes) ? minutes : 120;
            _jwtOptions.RefreshTokenDays = int.TryParse(configuration["Jwt:RefreshTokenDays"], out var days) ? days : 7;
        }
    }

    public async Task<IdentityResultDto<AuthTokenDto>> LoginAsync(LoginCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return IdentityResultDto<AuthTokenDto>.Fail("invalid_login_request", "Email and password are required.");
        }

        var user = await _userManager.FindByEmailAsync(command.Email.Trim());

        if (user is null || !await _userManager.CheckPasswordAsync(user, command.Password))
        {
            return IdentityResultDto<AuthTokenDto>.Fail("invalid_credentials", "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            return IdentityResultDto<AuthTokenDto>.Fail("user_inactive", "This user account is inactive.");
        }

        if ((await GetExpertBoardsAsync(user)).Count == 0)
        {
            return IdentityResultDto<AuthTokenDto>.Fail(
                "expert_board_access_required",
                "The user does not have a valid Expert Board assignment.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = await BuildTokenResponseAsync(user);
        return IdentityResultDto<AuthTokenDto>.Ok(token, "Login completed successfully.");
    }

    public async Task<IdentityResultDto<AuthTokenDto>> RefreshAsync(RefreshTokenCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return IdentityResultDto<AuthTokenDto>.Fail("refresh_token_required", "Refresh token is required.");
        }

        var tokenEntry = await _db.Set<IdentityUserToken<int>>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.LoginProvider == RefreshTokenProvider &&
                x.Name == RefreshTokenName &&
                x.Value == command.RefreshToken);

        if (tokenEntry is null)
        {
            return IdentityResultDto<AuthTokenDto>.Fail("invalid_refresh_token", "Refresh token is invalid.");
        }

        var user = await _userManager.FindByIdAsync(tokenEntry.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return IdentityResultDto<AuthTokenDto>.Fail("invalid_refresh_token", "Refresh token user is invalid or inactive.");
        }

        if ((await GetExpertBoardsAsync(user)).Count == 0)
        {
            return IdentityResultDto<AuthTokenDto>.Fail(
                "expert_board_access_required",
                "The user does not have a valid Expert Board assignment.");
        }

        var expiresValue = await _userManager.GetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenExpiryName);
        if (!DateTime.TryParse(expiresValue, out var expiresAtUtc) || expiresAtUtc <= DateTime.UtcNow)
        {
            await ClearRefreshTokenAsync(user);
            return IdentityResultDto<AuthTokenDto>.Fail("refresh_token_expired", "Refresh token has expired.");
        }

        var token = await BuildTokenResponseAsync(user);
        return IdentityResultDto<AuthTokenDto>.Ok(token, "Token refreshed successfully.");
    }

    public async Task<IdentityResultDto<object>> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return IdentityResultDto<object>.Ok(new { message = "No refresh token supplied." }, "Logout completed.");
        }

        var tokenEntry = await _db.Set<IdentityUserToken<int>>()
            .FirstOrDefaultAsync(x =>
                x.LoginProvider == RefreshTokenProvider &&
                x.Name == RefreshTokenName &&
                x.Value == refreshToken);

        if (tokenEntry is not null)
        {
            var user = await _userManager.FindByIdAsync(tokenEntry.UserId.ToString());
            if (user is not null)
            {
                await ClearRefreshTokenAsync(user);
            }
        }

        return IdentityResultDto<object>.Ok(new { message = "Logout completed." });
    }

    public async Task<IdentityResultDto<AuthUserDto>> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var id))
        {
            return IdentityResultDto<AuthUserDto>.Fail("not_authenticated", "No authenticated user was found.");
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || !user.IsActive)
        {
            return IdentityResultDto<AuthUserDto>.Fail("not_authenticated", "The authenticated user no longer exists or is inactive.");
        }

        return IdentityResultDto<AuthUserDto>.Ok(await ToUserDtoAsync(user));
    }

    public async Task<IdentityResultDto<object>> ForgotPasswordAsync(ForgotPasswordCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return IdentityResultDto<object>.Fail("email_required", "Email is required.");
        }

        var user = await _userManager.FindByEmailAsync(command.Email.Trim());
        if (user is not null && user.IsActive && !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var code = GenerateNumericCode();
                var expiresAtUtc = DateTime.UtcNow.AddHours(1);

                await _userManager.SetAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetCodeName, HashResetCode(code));
                await _userManager.SetAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetTokenName, token);
                await _userManager.SetAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetExpiryName, expiresAtUtc.ToString("O"));

                await _passwordResetEmailSender.SendPasswordResetAsync(user.Email, user.FullName, code);
            }
            catch (Exception ex)
            {
                // Keep the public response identical so this endpoint cannot be used
                // to discover registered email addresses.
                _logger.LogError(ex, "Failed to send a password reset email for user {UserId}.", user.Id);
            }
        }

        return IdentityResultDto<object>.Ok(new
        {
            message = "If the email belongs to an active account, password reset instructions will be sent."
        }, "Password reset request accepted.");
    }

    public async Task<IdentityResultDto<object>> VerifyPasswordResetTokenAsync(VerifyPasswordResetTokenCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Code))
        {
            return IdentityResultDto<object>.Fail("invalid_verify_request", "Email and code are required.");
        }

        var user = await _userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return IdentityResultDto<object>.Fail("invalid_reset_code", "The password reset code is invalid or expired.");
        }

        var isValid = await ValidatePasswordResetCodeAsync(user, command.Code);

        return isValid
            ? IdentityResultDto<object>.Ok(new { valid = true }, "Password reset code is valid.")
            : IdentityResultDto<object>.Fail("invalid_reset_code", "The password reset code is invalid or expired.");
    }

    public async Task<IdentityResultDto<object>> ResetPasswordAsync(ResetPasswordCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return IdentityResultDto<object>.Fail("invalid_reset_request", "Email, code and new password are required.");
        }

        var user = await _userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return IdentityResultDto<object>.Fail("invalid_reset_code", "The password reset code is invalid or expired.");
        }

        if (!await ValidatePasswordResetCodeAsync(user, command.Code))
        {
            return IdentityResultDto<object>.Fail("invalid_reset_code", "The password reset code is invalid or expired.");
        }

        var token = await _userManager.GetAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetTokenName);
        if (string.IsNullOrWhiteSpace(token))
        {
            return IdentityResultDto<object>.Fail("invalid_reset_code", "The password reset code is invalid or expired.");
        }

        if (await _userManager.CheckPasswordAsync(user, command.NewPassword))
        {
            return IdentityResultDto<object>.Fail("password_reused", "The new password must be different from the current password.");
        }

        var result = await _userManager.ResetPasswordAsync(user, token, command.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityResultDto<object>.Fail("password_reset_failed", "Password reset failed.", ToErrors(result));
        }

        await ClearRefreshTokenAsync(user);
        await ClearPasswordResetCodeAsync(user);
        return IdentityResultDto<object>.Ok(new { message = "Password reset successfully." });
    }

    public async Task<IdentityResultDto<IReadOnlyList<AuthUserDto>>> GetUsersAsync(UserQuery query)
    {
        var usersQuery = _userManager.Users.AsNoTracking();

        if (query.IsActive.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.IsActive == query.IsActive.Value);
        }

        if (query.FactoryId.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.FactoryId == query.FactoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var email = query.Email.Trim().ToUpperInvariant();
            usersQuery = usersQuery.Where(x => x.NormalizedEmail != null && x.NormalizedEmail.Contains(email));
        }

        var users = await usersQuery.OrderBy(x => x.Id).ToListAsync();
        var result = new List<AuthUserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (!string.IsNullOrWhiteSpace(query.Role) && !roles.Any(x => string.Equals(x, query.Role, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(await ToUserDtoAsync(user));
        }

        return IdentityResultDto<IReadOnlyList<AuthUserDto>>.Ok(result);
    }

    public async Task<IdentityResultDto<AuthUserDto>> GetUserByIdAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return IdentityResultDto<AuthUserDto>.Fail("user_not_found", $"User {id} was not found.");
        }

        return IdentityResultDto<AuthUserDto>.Ok(await ToUserDtoAsync(user));
    }

    public async Task<IdentityResultDto<AuthUserDto>> CreateUserAsync(CreateUserCommand command)
    {
        var validation = ValidateCreateUser(command);
        if (validation is not null)
        {
            return IdentityResultDto<AuthUserDto>.Fail(validation.Value.Code, validation.Value.Message);
        }

        var expertBoardValidation = ValidateExpertBoards(command.ExpertBoards, required: true);
        if (expertBoardValidation is not null)
        {
            return IdentityResultDto<AuthUserDto>.Fail(
                expertBoardValidation.Value.Code,
                expertBoardValidation.Value.Message);
        }

        if (await _userManager.FindByEmailAsync(command.Email.Trim()) is not null)
        {
            return IdentityResultDto<AuthUserDto>.Fail("email_already_exists", "A user with this email already exists.");
        }

        var customerId = command.CustomerId!.Value;
        var accessValidation = await ResolveAccessScopeAsync(
            customerId,
            command.FactoryId,
            command.LineId,
            command.FactoryIds,
            command.LineIds);
        if (accessValidation.Error is not null)
        {
            return IdentityResultDto<AuthUserDto>.Fail(accessValidation.Error.Value.Code, accessValidation.Error.Value.Message);
        }

        var roleName = string.IsNullOrWhiteSpace(command.Role) ? "Viewer" : command.Role.Trim();
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            return IdentityResultDto<AuthUserDto>.Fail("role_not_found", $"Role '{roleName}' was not found.");
        }

        var user = new ApplicationUser
        {
            UserName = command.Email.Trim(),
            Email = command.Email.Trim(),
            EmailConfirmed = true,
            FullName = command.FullName.Trim(),
            CustomerId = customerId,
            FactoryId = accessValidation.FactoryIds.FirstOrDefault() == 0 ? null : accessValidation.FactoryIds.First(),
            LineId = accessValidation.LineIds.FirstOrDefault() == 0 ? null : accessValidation.LineIds.First(),
            PhoneNumber = command.PhoneNumber,
            IsActive = command.IsActive
        };

        var result = await _userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            return IdentityResultDto<AuthUserDto>.Fail("user_create_failed", "User creation failed.", ToErrors(result));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return IdentityResultDto<AuthUserDto>.Fail("role_assignment_failed", "User creation was rolled back because role assignment failed.", ToErrors(roleResult));
        }

        var accessResult = await ReplaceUserAccessClaimsAsync(user, accessValidation.FactoryIds, accessValidation.LineIds);
        if (!accessResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return IdentityResultDto<AuthUserDto>.Fail(accessResult.Code, accessResult.Message, accessResult.Errors);
        }

        var expertBoardResult = await ReplaceUserExpertBoardClaimsAsync(
            user,
            NormalizeExpertBoards(command.ExpertBoards!));
        if (!expertBoardResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return IdentityResultDto<AuthUserDto>.Fail(
                expertBoardResult.Code,
                expertBoardResult.Message,
                expertBoardResult.Errors);
        }

        return IdentityResultDto<AuthUserDto>.Ok(await ToUserDtoAsync(user), "User created successfully.");
    }

    public async Task<IdentityResultDto<AuthUserDto>> UpdateUserAsync(int id, UpdateUserCommand command)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return IdentityResultDto<AuthUserDto>.Fail("user_not_found", $"User {id} was not found.");
        }

        if (!string.IsNullOrWhiteSpace(command.Role) && !await _roleManager.RoleExistsAsync(command.Role.Trim()))
        {
            return IdentityResultDto<AuthUserDto>.Fail("role_not_found", $"Role '{command.Role.Trim()}' was not found.");
        }

        var expertBoardValidation = ValidateExpertBoards(command.ExpertBoards, required: false);
        if (expertBoardValidation is not null)
        {
            return IdentityResultDto<AuthUserDto>.Fail(
                expertBoardValidation.Value.Code,
                expertBoardValidation.Value.Message);
        }

        var shouldReplaceAccess =
            command.FactoryId.HasValue ||
            command.LineId.HasValue ||
            command.FactoryIds is not null ||
            command.LineIds is not null;

        var accessValidation = shouldReplaceAccess
            ? await ResolveAccessScopeAsync(user.CustomerId, command.FactoryId, command.LineId, command.FactoryIds, command.LineIds)
            : (FactoryIds: Array.Empty<int>() as IReadOnlyList<int>, LineIds: Array.Empty<int>() as IReadOnlyList<int>, Error: ((string Code, string Message)?)null);
        if (accessValidation.Error is not null)
        {
            return IdentityResultDto<AuthUserDto>.Fail(accessValidation.Error.Value.Code, accessValidation.Error.Value.Message);
        }

        if (!string.IsNullOrWhiteSpace(command.Email) && !string.Equals(user.Email, command.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(command.Email.Trim());
            if (existing is not null && existing.Id != user.Id)
            {
                return IdentityResultDto<AuthUserDto>.Fail("email_already_exists", "Another user already uses this email.");
            }

            user.Email = command.Email.Trim();
            user.UserName = command.Email.Trim();
            user.EmailConfirmed = true;
        }

        if (!string.IsNullOrWhiteSpace(command.FullName))
        {
            user.FullName = command.FullName.Trim();
        }

        if (shouldReplaceAccess)
        {
            user.FactoryId = accessValidation.FactoryIds.FirstOrDefault() == 0 ? null : accessValidation.FactoryIds.First();
            user.LineId = accessValidation.LineIds.FirstOrDefault() == 0 ? null : accessValidation.LineIds.First();
        }

        if (command.PhoneNumber is not null)
        {
            user.PhoneNumber = command.PhoneNumber;
        }

        if (command.IsActive.HasValue)
        {
            user.IsActive = command.IsActive.Value;
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return IdentityResultDto<AuthUserDto>.Fail("user_update_failed", "User update failed.", ToErrors(updateResult));
        }

        if (shouldReplaceAccess)
        {
            var accessResult = await ReplaceUserAccessClaimsAsync(user, accessValidation.FactoryIds, accessValidation.LineIds);
            if (!accessResult.Succeeded)
            {
                return IdentityResultDto<AuthUserDto>.Fail(accessResult.Code, accessResult.Message, accessResult.Errors);
            }
        }

        if (command.ExpertBoards is not null)
        {
            var expertBoardResult = await ReplaceUserExpertBoardClaimsAsync(
                user,
                NormalizeExpertBoards(command.ExpertBoards));
            if (!expertBoardResult.Succeeded)
            {
                return IdentityResultDto<AuthUserDto>.Fail(
                    expertBoardResult.Code,
                    expertBoardResult.Message,
                    expertBoardResult.Errors);
            }
        }

        if (!string.IsNullOrWhiteSpace(command.Role))
        {
            var oldRoles = await _userManager.GetRolesAsync(user);
            if (oldRoles.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, oldRoles);
                if (!removeResult.Succeeded)
                {
                    return IdentityResultDto<AuthUserDto>.Fail("role_update_failed", "Failed to remove old roles.", ToErrors(removeResult));
                }
            }

            var addResult = await _userManager.AddToRoleAsync(user, command.Role.Trim());
            if (!addResult.Succeeded)
            {
                return IdentityResultDto<AuthUserDto>.Fail("role_update_failed", "Failed to add new role.", ToErrors(addResult));
            }
        }

        return IdentityResultDto<AuthUserDto>.Ok(await ToUserDtoAsync(user), "User updated successfully.");
    }

    public async Task<IdentityResultDto<object>> DeactivateUserAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return IdentityResultDto<object>.Fail("user_not_found", $"User {id} was not found.");
        }

        user.IsActive = false;
        await ClearRefreshTokenAsync(user);
        var result = await _userManager.UpdateAsync(user);

        return result.Succeeded
            ? IdentityResultDto<object>.Ok(new { message = "User deactivated successfully." })
            : IdentityResultDto<object>.Fail("user_deactivate_failed", "User deactivation failed.", ToErrors(result));
    }

    public async Task<IdentityResultDto<object>> ChangePasswordAsync(ChangePasswordCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CurrentPassword) || string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return IdentityResultDto<object>.Fail("invalid_password_change_request", "Current password and new password are required.");
        }

        var user = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
        {
            return IdentityResultDto<object>.Fail("user_not_found", $"User {command.UserId} was not found.");
        }

        if (!await _userManager.CheckPasswordAsync(user, command.CurrentPassword))
        {
            return IdentityResultDto<object>.Fail("current_password_invalid", "The current password is incorrect.");
        }

        if (await _userManager.CheckPasswordAsync(user, command.NewPassword))
        {
            return IdentityResultDto<object>.Fail("password_reused", "The new password must be different from the current password.");
        }

        var result = await _userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityResultDto<object>.Fail("password_change_failed", "Password change failed.", ToErrors(result));
        }

        await ClearRefreshTokenAsync(user);
        return IdentityResultDto<object>.Ok(new { message = "Password changed successfully." });
    }

    public async Task<IdentityResultDto<object>> AdminResetPasswordAsync(AdminResetPasswordCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return IdentityResultDto<object>.Fail("new_password_required", "New password is required.");
        }

        var user = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
        {
            return IdentityResultDto<object>.Fail("user_not_found", $"User {command.UserId} was not found.");
        }

        if (await _userManager.CheckPasswordAsync(user, command.NewPassword))
        {
            return IdentityResultDto<object>.Fail("password_reused", "The new password must be different from the current password.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, command.NewPassword);

        if (!result.Succeeded)
        {
            return IdentityResultDto<object>.Fail("password_reset_failed", "Password reset failed.", ToErrors(result));
        }

        await ClearRefreshTokenAsync(user);
        return IdentityResultDto<object>.Ok(new { message = "Password reset successfully." });
    }

    public async Task<IdentityResultDto<IReadOnlyList<RoleDto>>> GetRolesAsync()
    {
        var roles = await _roleManager.Roles.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var result = new List<RoleDto>();

        foreach (var role in roles)
        {
            result.Add(await ToRoleDtoAsync(role));
        }

        return IdentityResultDto<IReadOnlyList<RoleDto>>.Ok(result);
    }

    public async Task<IdentityResultDto<RoleDto>> GetRoleByIdAsync(int id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
        {
            return IdentityResultDto<RoleDto>.Fail("role_not_found", $"Role {id} was not found.");
        }

        return IdentityResultDto<RoleDto>.Ok(await ToRoleDtoAsync(role));
    }

    public async Task<IdentityResultDto<RoleDto>> CreateRoleAsync(CreateRoleCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return IdentityResultDto<RoleDto>.Fail("role_name_required", "Role name is required.");
        }

        if (await _roleManager.RoleExistsAsync(command.Name.Trim()))
        {
            return IdentityResultDto<RoleDto>.Fail("role_already_exists", "Role already exists.");
        }

        var role = new ApplicationRole { Name = command.Name.Trim() };
        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return IdentityResultDto<RoleDto>.Fail("role_create_failed", "Role creation failed.", ToErrors(result));
        }

        var claimResult = await ReplaceRoleClaimsAsync(role, command.Claims);
        if (!claimResult.Succeeded)
        {
            return IdentityResultDto<RoleDto>.Fail(claimResult.Code, claimResult.Message, claimResult.Errors);
        }

        return IdentityResultDto<RoleDto>.Ok(await ToRoleDtoAsync(role), "Role created successfully.");
    }

    public async Task<IdentityResultDto<RoleDto>> UpdateRoleAsync(int id, UpdateRoleCommand command)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
        {
            return IdentityResultDto<RoleDto>.Fail("role_not_found", $"Role {id} was not found.");
        }

        if (!string.IsNullOrWhiteSpace(command.Name) && !string.Equals(role.Name, command.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            if (await _roleManager.RoleExistsAsync(command.Name.Trim()))
            {
                return IdentityResultDto<RoleDto>.Fail("role_already_exists", "Another role already uses this name.");
            }

            role.Name = command.Name.Trim();
            var updateResult = await _roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                return IdentityResultDto<RoleDto>.Fail("role_update_failed", "Role update failed.", ToErrors(updateResult));
            }
        }

        if (command.Claims is not null)
        {
            var claimResult = await ReplaceRoleClaimsAsync(role, command.Claims);
            if (!claimResult.Succeeded)
            {
                return IdentityResultDto<RoleDto>.Fail(claimResult.Code, claimResult.Message, claimResult.Errors);
            }
        }

        return IdentityResultDto<RoleDto>.Ok(await ToRoleDtoAsync(role), "Role updated successfully.");
    }

    public async Task<IdentityResultDto<object>> DeleteRoleAsync(int id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
        {
            return IdentityResultDto<object>.Fail("role_not_found", $"Role {id} was not found.");
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Any())
        {
            return IdentityResultDto<object>.Fail("role_in_use", "Cannot delete a role assigned to users.");
        }

        var result = await _roleManager.DeleteAsync(role);
        return result.Succeeded
            ? IdentityResultDto<object>.Ok(new { message = "Role deleted successfully." })
            : IdentityResultDto<object>.Fail("role_delete_failed", "Role deletion failed.", ToErrors(result));
    }

    private async Task<AuthTokenDto> BuildTokenResponseAsync(ApplicationUser user)
    {
        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.DurationMinutes <= 0 ? 120 : _jwtOptions.DurationMinutes);
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays <= 0 ? 7 : _jwtOptions.RefreshTokenDays);
        var userDto = await ToUserDtoAsync(user);
        var accessToken = await GenerateAccessTokenAsync(user, userDto.Roles);
        var refreshToken = GenerateSecureToken();

        await _userManager.SetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName, refreshToken);
        await _userManager.SetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenExpiryName, refreshTokenExpiresAt.ToString("O"));

        return new AuthTokenDto(accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt, userDto);
    }

    private async Task<string> GenerateAccessTokenAsync(ApplicationUser user, IReadOnlyList<string> roles)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.Key) || _jwtOptions.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("fullName", user.FullName),
            new("customerId", user.CustomerId.ToString()),
            new("factoryId", user.FactoryId?.ToString() ?? string.Empty),
            new("lineId", user.LineId?.ToString() ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var userAccessClaims = await _userManager.GetClaimsAsync(user);
        claims.AddRange(userAccessClaims.Where(x =>
            x.Type == FactoryAccessClaimType ||
            x.Type == LineAccessClaimType ||
            x.Type == ExpertBoardAccessClaimType));

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));

            var roleEntity = await _roleManager.FindByNameAsync(role);
            if (roleEntity is not null)
            {
                claims.AddRange(await _roleManager.GetClaimsAsync(roleEntity));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.DurationMinutes <= 0 ? 120 : _jwtOptions.DurationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<AuthUserDto> ToUserDtoAsync(ApplicationUser user)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var claims = new List<AppClaimDto>();
        var userClaims = await _userManager.GetClaimsAsync(user);
        var factoryIds = ExtractIntClaimValues(userClaims, FactoryAccessClaimType, user.FactoryId);
        var lineIds = ExtractIntClaimValues(userClaims, LineAccessClaimType, user.LineId);
        var expertBoards = ExtractExpertBoardValues(userClaims);

        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is null) continue;

            var roleClaims = await _roleManager.GetClaimsAsync(role);
            claims.AddRange(roleClaims.Select(x => new AppClaimDto(x.Type, x.Value)));
        }

        return new AuthUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.FullName,
            user.CustomerId,
            user.FactoryId,
            user.LineId,
            factoryIds,
            lineIds,
            expertBoards,
            user.IsActive,
            user.LastLoginAt,
            roles,
            claims.Distinct().ToList());
    }

    private async Task<RoleDto> ToRoleDtoAsync(ApplicationRole role)
    {
        var claims = await _roleManager.GetClaimsAsync(role);
        var usersCount = string.IsNullOrWhiteSpace(role.Name)
            ? 0
            : (await _userManager.GetUsersInRoleAsync(role.Name)).Count;

        return new RoleDto(
            role.Id,
            role.Name ?? string.Empty,
            claims.Select(x => new AppClaimDto(x.Type, x.Value)).ToList(),
            usersCount);
    }

    private async Task<IdentityResultDto<object>> ReplaceRoleClaimsAsync(ApplicationRole role, IReadOnlyList<AppClaimDto>? claims)
    {
        if (claims is null)
        {
            return IdentityResultDto<object>.Ok(null);
        }

        var duplicate = claims
            .GroupBy(x => new { x.Type, x.Value })
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate is not null)
        {
            return IdentityResultDto<object>.Fail("duplicate_claim", $"Duplicate claim: {duplicate.Key.Type}={duplicate.Key.Value}");
        }

        var oldClaims = await _roleManager.GetClaimsAsync(role);
        foreach (var claim in oldClaims)
        {
            var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
            if (!removeResult.Succeeded)
            {
                return IdentityResultDto<object>.Fail("claim_remove_failed", "Failed to remove old role claims.", ToErrors(removeResult));
            }
        }

        foreach (var claim in claims)
        {
            if (string.IsNullOrWhiteSpace(claim.Type) || string.IsNullOrWhiteSpace(claim.Value))
            {
                return IdentityResultDto<object>.Fail("invalid_claim", "Claim type and value are required.");
            }

            var addResult = await _roleManager.AddClaimAsync(role, new Claim(claim.Type.Trim(), claim.Value.Trim()));
            if (!addResult.Succeeded)
            {
                return IdentityResultDto<object>.Fail("claim_add_failed", "Failed to add role claim.", ToErrors(addResult));
            }
        }

        return IdentityResultDto<object>.Ok(null);
    }

    private static (string Code, string Message)? ValidateCreateUser(CreateUserCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Email)) return ("email_required", "Email is required.");
        if (string.IsNullOrWhiteSpace(command.FullName)) return ("full_name_required", "Full name is required.");
        if (string.IsNullOrWhiteSpace(command.Password)) return ("password_required", "Password is required.");
        if (!command.CustomerId.HasValue || command.CustomerId.Value <= 0) return ("customer_id_required", "CustomerId is required.");
        return null;
    }

    private async Task<(IReadOnlyList<int> FactoryIds, IReadOnlyList<int> LineIds, (string Code, string Message)? Error)> ResolveAccessScopeAsync(
        int customerId,
        int? factoryId,
        int? lineId,
        IReadOnlyList<int>? factoryIds,
        IReadOnlyList<int>? lineIds)
    {
        if (!await _db.Customers.AnyAsync(x => x.Id == customerId && x.IsActive && !x.IsDeleted))
        {
            return (Array.Empty<int>(), Array.Empty<int>(), ("customer_not_found", $"Customer {customerId} was not found or inactive."));
        }

        var resolvedFactoryIds = NormalizeIds(factoryId, factoryIds);
        var resolvedLineIds = NormalizeIds(lineId, lineIds);

        if (resolvedFactoryIds.Count > 0)
        {
            var validFactoryIds = await _db.Factories
                .AsNoTracking()
                .Where(x => resolvedFactoryIds.Contains(x.Id) &&
                            x.CustomerId == customerId &&
                            x.IsActive &&
                            !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync();

            var invalidFactoryIds = resolvedFactoryIds.Except(validFactoryIds).ToList();
            if (invalidFactoryIds.Count > 0)
            {
                return (Array.Empty<int>(), Array.Empty<int>(), ("factory_not_found", $"Factory ids are invalid, inactive, or do not belong to customer {customerId}: {string.Join(", ", invalidFactoryIds)}."));
            }
        }

        if (resolvedLineIds.Count == 0 && resolvedFactoryIds.Count > 0 && lineIds is null && !lineId.HasValue)
        {
            resolvedLineIds = await _db.ProductionLines
                .AsNoTracking()
                .Where(x => resolvedFactoryIds.Contains(x.FactoryId) && x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();
        }

        if (resolvedLineIds.Count > 0)
        {
            var lineRows = await _db.ProductionLines
                .AsNoTracking()
                .Where(x => resolvedLineIds.Contains(x.Id) && x.IsActive && !x.IsDeleted)
                .Select(x => new { x.Id, x.FactoryId })
                .ToListAsync();

            var validLineIds = lineRows.Select(x => x.Id).ToHashSet();
            var invalidLineIds = resolvedLineIds.Where(x => !validLineIds.Contains(x)).ToList();
            if (invalidLineIds.Count > 0)
            {
                return (Array.Empty<int>(), Array.Empty<int>(), ("line_not_found", $"Line ids are invalid or inactive: {string.Join(", ", invalidLineIds)}."));
            }

            if (resolvedFactoryIds.Count == 0)
            {
                resolvedFactoryIds = lineRows.Select(x => x.FactoryId).Distinct().OrderBy(x => x).ToList();
            }

            var lineOutsideFactories = lineRows
                .Where(x => !resolvedFactoryIds.Contains(x.FactoryId))
                .Select(x => x.Id)
                .ToList();

            if (lineOutsideFactories.Count > 0)
            {
                return (Array.Empty<int>(), Array.Empty<int>(), ("line_not_found", $"Line ids do not belong to the supplied factories: {string.Join(", ", lineOutsideFactories)}."));
            }
        }

        return (resolvedFactoryIds, resolvedLineIds, null);
    }

    private async Task<IdentityResultDto<object>> ReplaceUserAccessClaimsAsync(
        ApplicationUser user,
        IReadOnlyList<int> factoryIds,
        IReadOnlyList<int> lineIds)
    {
        var oldClaims = await _userManager.GetClaimsAsync(user);
        var accessClaims = oldClaims
            .Where(x => x.Type == FactoryAccessClaimType || x.Type == LineAccessClaimType)
            .ToList();

        if (accessClaims.Count > 0)
        {
            var removeResult = await _userManager.RemoveClaimsAsync(user, accessClaims);
            if (!removeResult.Succeeded)
            {
                return IdentityResultDto<object>.Fail("access_claim_update_failed", "Failed to remove old user access claims.", ToErrors(removeResult));
            }
        }

        var newClaims = factoryIds.Select(x => new Claim(FactoryAccessClaimType, x.ToString()))
            .Concat(lineIds.Select(x => new Claim(LineAccessClaimType, x.ToString())))
            .ToList();

        if (newClaims.Count == 0)
        {
            return IdentityResultDto<object>.Ok(null);
        }

        var addResult = await _userManager.AddClaimsAsync(user, newClaims);
        return addResult.Succeeded
            ? IdentityResultDto<object>.Ok(null)
            : IdentityResultDto<object>.Fail("access_claim_update_failed", "Failed to add user access claims.", ToErrors(addResult));
    }

    private async Task<IdentityResultDto<object>> ReplaceUserExpertBoardClaimsAsync(
        ApplicationUser user,
        IReadOnlyList<int> expertBoards)
    {
        var oldClaims = await _userManager.GetClaimsAsync(user);
        var expertBoardClaims = oldClaims
            .Where(x => x.Type == ExpertBoardAccessClaimType)
            .ToList();

        if (expertBoardClaims.Count > 0)
        {
            var removeResult = await _userManager.RemoveClaimsAsync(user, expertBoardClaims);
            if (!removeResult.Succeeded)
            {
                return IdentityResultDto<object>.Fail(
                    "expert_board_claim_update_failed",
                    "Failed to remove old Expert Board access claims.",
                    ToErrors(removeResult));
            }
        }

        var newClaims = expertBoards
            .Select(x => new Claim(ExpertBoardAccessClaimType, x.ToString()))
            .ToList();
        var addResult = await _userManager.AddClaimsAsync(user, newClaims);

        return addResult.Succeeded
            ? IdentityResultDto<object>.Ok(null)
            : IdentityResultDto<object>.Fail(
                "expert_board_claim_update_failed",
                "Failed to add Expert Board access claims.",
                ToErrors(addResult));
    }

    private async Task<IReadOnlyList<int>> GetExpertBoardsAsync(ApplicationUser user)
        => ExtractExpertBoardValues(await _userManager.GetClaimsAsync(user));

    private static (string Code, string Message)? ValidateExpertBoards(
        IReadOnlyList<int>? expertBoards,
        bool required)
    {
        if (expertBoards is null)
        {
            return required
                ? ("expert_boards_required", "At least one Expert Board is required.")
                : null;
        }

        var normalized = expertBoards.Distinct().ToArray();
        if (normalized.Length == 0)
        {
            return ("expert_boards_required", "At least one Expert Board is required.");
        }

        var invalidValues = normalized
            .Where(x => !Enum.IsDefined(typeof(ExpertBoardType), x))
            .OrderBy(x => x)
            .ToArray();

        return invalidValues.Length == 0
            ? null
            : (
                "invalid_expert_boards",
                $"Expert Board values must be between 1 and 3. Invalid values: {string.Join(", ", invalidValues)}.");
    }

    private static IReadOnlyList<int> NormalizeExpertBoards(IEnumerable<int> expertBoards)
        => expertBoards
            .Where(x => Enum.IsDefined(typeof(ExpertBoardType), x))
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

    private static IReadOnlyList<int> ExtractExpertBoardValues(IEnumerable<Claim> claims)
        => claims
            .Where(x =>
                x.Type == ExpertBoardAccessClaimType &&
                int.TryParse(x.Value, out var value) &&
                Enum.IsDefined(typeof(ExpertBoardType), value))
            .Select(x => int.Parse(x.Value))
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

    private static List<int> NormalizeIds(int? singleId, IReadOnlyList<int>? ids)
    {
        var result = new List<int>();
        if (singleId.HasValue && singleId.Value > 0) result.Add(singleId.Value);
        if (ids is not null) result.AddRange(ids.Where(x => x > 0));
        return result.Distinct().OrderBy(x => x).ToList();
    }

    private static IReadOnlyList<int> ExtractIntClaimValues(IEnumerable<Claim> claims, string type, int? fallback)
    {
        var values = claims
            .Where(x => x.Type == type && int.TryParse(x.Value, out _))
            .Select(x => int.Parse(x.Value))
            .ToList();

        if (values.Count == 0 && fallback.HasValue)
        {
            values.Add(fallback.Value);
        }

        return values.Distinct().OrderBy(x => x).ToList();
    }

    private async Task<(string Code, string Message)?> ValidateOrganizationScopeAsync(
        int customerId,
        int? factoryId,
        int? lineId)
    {
        if (!await _db.Customers.AnyAsync(x => x.Id == customerId && x.IsActive && !x.IsDeleted))
        {
            return ("customer_not_found", $"Customer {customerId} was not found or inactive.");
        }

        if (lineId.HasValue && !factoryId.HasValue)
        {
            return ("factory_required", "FactoryId is required when LineId is supplied.");
        }

        if (factoryId.HasValue && !await _db.Factories.AnyAsync(x =>
                x.Id == factoryId.Value &&
                x.CustomerId == customerId &&
                x.IsActive &&
                !x.IsDeleted))
        {
            return ("factory_not_found", $"Factory {factoryId.Value} was not found, inactive, or does not belong to customer {customerId}.");
        }

        if (lineId.HasValue && !await _db.ProductionLines.AnyAsync(x =>
                x.Id == lineId.Value &&
                x.FactoryId == factoryId!.Value &&
                x.IsActive &&
                !x.IsDeleted))
        {
            return ("line_not_found", $"Line {lineId.Value} was not found, inactive, or does not belong to factory {factoryId}.");
        }

        return null;
    }

    private async Task ClearRefreshTokenAsync(ApplicationUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenExpiryName);
    }

    private async Task ClearPasswordResetCodeAsync(ApplicationUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetCodeName);
        await _userManager.RemoveAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetExpiryName);
    }

    private async Task<bool> ValidatePasswordResetCodeAsync(ApplicationUser user, string code)
    {
        var expectedHash = await _userManager.GetAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetCodeName);
        var expiresValue = await _userManager.GetAuthenticationTokenAsync(user, PasswordResetProvider, PasswordResetExpiryName);

        if (string.IsNullOrWhiteSpace(expectedHash) ||
            string.IsNullOrWhiteSpace(expiresValue) ||
            !DateTime.TryParse(expiresValue, out var expiresAtUtc) ||
            expiresAtUtc <= DateTime.UtcNow)
        {
            return false;
        }

        return FixedTimeEquals(expectedHash, HashResetCode(code.Trim()));
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateNumericCode()
        => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string HashResetCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static IReadOnlyList<string> ToErrors(IdentityResult result)
    {
        return result.Errors.Select(x => $"{x.Code}: {x.Description}").ToList();
    }
}
