using Application.Abstractions.Repositories;
using Domain.Enums;
using Infrastructure.Identity;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Infrastructure.Persistence;
using Infrastructure.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Infrastructure.Integration;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPlanningService, Infrastructure.Services.PlanningService>();
        var connectionString = configuration.GetConnectionString("MCPAConn");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'MCPAConn' was not found in appsettings.json.");
        }

        EnsureNoDeploymentPlaceholder(connectionString, "ConnectionStrings:MCPAConn");

        var expectedTargetDatabase = configuration["DatabaseSafety:ExpectedTargetDatabase"];
        if (!string.IsNullOrWhiteSpace(expectedTargetDatabase))
        {
            var configuredTargetDatabase = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            if (!string.Equals(configuredTargetDatabase, expectedTargetDatabase, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Database safety check failed. MCPAConn targets '{configuredTargetDatabase}', " +
                    $"but '{expectedTargetDatabase}' is required.");
            }
        }

        services.AddDbContext<MesDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
        });

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(1);
        });

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 4;
            })
            .AddEntityFrameworkStores<MesDbContext>()
            .AddDefaultTokenProviders();

        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) ||
            jwtKey.Length < 32 ||
            ContainsDeploymentPlaceholder(jwtKey))
        {
            throw new InvalidOperationException(
                "Jwt:Key must be supplied by User Secrets or the server environment and contain at least 32 characters.");
        }

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var requestPath = context.HttpContext.Request.Path;

                        var isHubRequest =
                            requestPath.StartsWithSegments("/hubs") ||
                            requestPath.Equals(new Microsoft.AspNetCore.Http.PathString("/signalsHub")) ||
                            requestPath.Value?.Contains(
                                "/hubs/",
                                StringComparison.OrdinalIgnoreCase) == true;

                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            isHubRequest)
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var expertBoardClaims = context.Principal?
                            .FindAll(UserManagementService.ExpertBoardAccessClaimType)
                            .ToArray() ?? Array.Empty<System.Security.Claims.Claim>();

                        var hasValidExpertBoardAccess =
                            expertBoardClaims.Length > 0 &&
                            expertBoardClaims.All(x =>
                                int.TryParse(x.Value, out var value) &&
                                Enum.IsDefined(typeof(ExpertBoardType), value));

                        if (!hasValidExpertBoardAccess)
                        {
                            context.Fail(
                                "The access token does not contain a valid Expert Board assignment.");
                        }

                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "MESPlatForm",
                    ValidAudience = configuration["Jwt:Audience"] ?? "MESPlatFormClient",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        services.AddAuthorization();

        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOverviewRepository, OverviewRepository>();
        services.AddScoped<IFactoryLayoutSignalReader, FactoryLayoutSignalReader>();
        services.AddScoped<ISignageLayoutService, Application.Services.SignageLayoutService>();
        services.AddScoped<IOverviewLiveProvider, EipicoOverviewProvider>();
        services.AddSingleton(TimeProvider.System);
        services.Configure<Application.Dtos.Overview.EipicoOverviewOptions>(configuration.GetSection("Overview:Eipico"));
        services.AddScoped<IProductionRecordWriter, ProductionRecordWriter>();
        services.AddScoped<IEnergyRecordWriter, EnergyRecordWriter>();

        services.Configure<EipicoSyncOptions>(configuration.GetSection(EipicoSyncOptions.SectionName));
        if (configuration.GetValue<bool>("RuntimeFeatures:EnableEipicoSync"))
        {
            var sourceConnectionString = configuration.GetConnectionString("EipicoSource");
            if (string.IsNullOrWhiteSpace(sourceConnectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'EipicoSource' is required when Eipico sync is enabled.");
            }


            EnsureNoDeploymentPlaceholder(sourceConnectionString, "ConnectionStrings:EipicoSource");

            var sourceBuilder = new SqlConnectionStringBuilder(sourceConnectionString);
            var expectedSourceDatabase = configuration[
                $"{EipicoSyncOptions.SectionName}:ExpectedSourceDatabase"] ?? "Eipico";

            if (!string.Equals(
                    sourceBuilder.InitialCatalog,
                    expectedSourceDatabase,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Eipico source safety check failed. EipicoSource targets " +
                    $"'{sourceBuilder.InitialCatalog}', but '{expectedSourceDatabase}' is required.");
            }

            if (sourceBuilder.ApplicationIntent != ApplicationIntent.ReadOnly)
            {
                throw new InvalidOperationException(
                    "EipicoSource must include ApplicationIntent=ReadOnly.");
            }

            var targetBuilder = new SqlConnectionStringBuilder(connectionString);
            if (string.Equals(
                    sourceBuilder.InitialCatalog,
                    targetBuilder.InitialCatalog,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "EipicoSource and MCPAConn must target different databases.");
            }

            services.AddSingleton(new EipicoSourceReader(sourceConnectionString));
            services.AddSingleton(new EipicoRealtimeSourceReader(sourceConnectionString));
            services.AddSingleton(new EipicoDefinitionSourceReader(sourceConnectionString));
            services.AddSingleton<EipicoSourceTime>();
            services.AddScoped<EipicoSapSyncService>();
            services.AddScoped<EipicoRealtimeSyncService>();
            services.AddScoped<EipicoDefinitionSyncService>();
        }

        return services;
    }

    private static void EnsureNoDeploymentPlaceholder(string value, string configurationKey)
    {
        if (ContainsDeploymentPlaceholder(value))
        {
            throw new InvalidOperationException(
                $"{configurationKey} still contains a deployment placeholder. " +
                "Supply the real value through User Secrets or the server environment.");
        }
    }

    private static bool ContainsDeploymentPlaceholder(string value)
        => value.Contains("SET_IN_", StringComparison.OrdinalIgnoreCase);
}
