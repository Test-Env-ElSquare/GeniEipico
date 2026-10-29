using Domain.Entities.Organization;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Infrastructure.Identity;

public static class IdentitySeedExtensions
{
    public static async Task SeedMesIdentityAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        var db = provider.GetRequiredService<MesDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var eipicoSyncEnabled = configuration.GetValue<bool>("RuntimeFeatures:EnableEipicoSync");
        var eipicoCustomerCode = configuration.GetValue<string>("EipicoSync:CustomerCode")?.Trim();
        var eipicoCustomerName = configuration.GetValue<string>("EipicoSync:CustomerName")?.Trim();

        const string adminEmail = "Admin@admin.com";
        const string adminPassword = "Admin@123";
        const string adminRole = "Admin";
        const string oldSeedEmail = "seed.admin@local";

        try
        {
            var oldSeed = await userManager.FindByEmailAsync(oldSeedEmail);
            if (oldSeed is not null)
            {
                await userManager.DeleteAsync(oldSeed);
                logger.LogInformation("Deleted old seed user {Email}.", oldSeedEmail);
            }

            foreach (var roleName in new[] { adminRole, "Supervisor", "Operator", "Viewer" })
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                }
            }

            Customer? customer = null;
            if (eipicoSyncEnabled && !string.IsNullOrWhiteSpace(eipicoCustomerCode))
            {
                customer = await db.Customers
                    .IgnoreQueryFilters()
                    .OrderBy(x => x.IsDeleted)
                    .FirstOrDefaultAsync(x => x.Code == eipicoCustomerCode);

                if (customer is not null)
                {
                    customer.Name = string.IsNullOrWhiteSpace(eipicoCustomerName)
                        ? customer.Name
                        : eipicoCustomerName;
                    customer.IsActive = true;
                    customer.IsDeleted = false;
                    customer.DeletedAt = null;
                }
            }
            else
            {
                customer = await db.Customers
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted);
            }

            if (customer is null)
            {
                customer = new Customer
                {
                    Code = eipicoSyncEnabled && !string.IsNullOrWhiteSpace(eipicoCustomerCode)
                        ? eipicoCustomerCode
                        : "SYS-ADMIN-CUST",
                    Name = eipicoSyncEnabled && !string.IsNullOrWhiteSpace(eipicoCustomerName)
                        ? eipicoCustomerName
                        : "System Administration Customer",
                    Tier = "premium",
                    CountryCode = "EGY",
                    ContactEmail = adminEmail,
                    Industry = "MES",
                    ErpSystem = "N/A",
                    IsActive = true
                };

                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }

            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    CustomerId = customer.Id,
                    FactoryId = null,
                    LineId = null,
                    IsActive = true,
                    PhoneNumberConfirmed = true
                };

                var createResult = await userManager.CreateAsync(admin, adminPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Failed to create admin user: {Errors}", string.Join(" | ", createResult.Errors.Select(x => x.Description)));
                    return;
                }
            }
            else
            {
                admin.FullName = string.IsNullOrWhiteSpace(admin.FullName) ? "System Administrator" : admin.FullName;
                // The seeded Eipico administrator must share the same customer
                // scope as the factories created by EipicoDefinitionSyncService.
                // customerId is embedded in the JWT and drives every factory/
                // line filter, so retaining the old system customer here makes
                // the Overview filter lists appear empty.
                if (eipicoSyncEnabled || admin.CustomerId <= 0)
                {
                    admin.CustomerId = customer.Id;
                    admin.FactoryId = null;
                    admin.LineId = null;
                }
                admin.EmailConfirmed = true;
                admin.IsActive = true;
                await userManager.UpdateAsync(admin);
            }

            if (!await userManager.IsInRoleAsync(admin, adminRole))
            {
                await userManager.AddToRoleAsync(admin, adminRole);
            }

            var adminClaims = await userManager.GetClaimsAsync(admin);
            if (eipicoSyncEnabled)
            {
                // Factory and line identities are reseeded when the Eipico test
                // database is reset. Keeping old positive scope claims would
                // override the Admin-wide customer scope and make every factory
                // and line dropdown empty even though the rows exist.
                var staleScopeClaims = adminClaims
                    .Where(x => x.Type is "factoryId" or "lineId")
                    .ToArray();
                if (staleScopeClaims.Length > 0)
                {
                    var removeResult = await userManager.RemoveClaimsAsync(admin, staleScopeClaims);
                    if (!removeResult.Succeeded)
                    {
                        logger.LogError(
                            "Failed to clear stale factory/line claims from the Eipico admin: {Errors}",
                            string.Join(" | ", removeResult.Errors.Select(x => x.Description)));
                        return;
                    }

                    adminClaims = await userManager.GetClaimsAsync(admin);
                }
            }

            foreach (var expertBoard in Enum.GetValues<ExpertBoardType>())
            {
                var value = ((int)expertBoard).ToString();
                if (adminClaims.Any(x =>
                        x.Type == UserManagementService.ExpertBoardAccessClaimType &&
                        x.Value == value))
                {
                    continue;
                }

                var claimResult = await userManager.AddClaimAsync(
                    admin,
                    new Claim(UserManagementService.ExpertBoardAccessClaimType, value));
                if (!claimResult.Succeeded)
                {
                    logger.LogError(
                        "Failed to assign Expert Board {ExpertBoard} to the admin user: {Errors}",
                        expertBoard,
                        string.Join(" | ", claimResult.Errors.Select(x => x.Description)));
                    return;
                }
            }

            logger.LogInformation("Admin identity seed ready. Email: {Email}", adminEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Identity seed failed.");
            throw;
        }
    }
}
