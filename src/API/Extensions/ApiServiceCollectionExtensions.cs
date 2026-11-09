using Application.DependencyInjection;
using Application.Abstractions.Services;
using API.Common;
using API.Realtime.Alerts;
using API.Realtime.Scada;
using Domain.Enums;
using Infrastructure.DependencyInjection;
using Infrastructure.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddMesPlatformApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);
        services.AddApplicationServices();
        services.AddHttpContextAccessor();

        services.AddScoped<
            Application.Abstractions.Services.IOverviewAccessProvider,
            API.Common.OverviewAccessProvider>();

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new BatchPriorityJsonConverter());

                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(
                        JsonNamingPolicy.SnakeCaseLower));
            });

        services.AddSignalR();
        services.AddScoped<IRealtimeAlertPublisher, SignalRAlertPublisher>();

        services.AddSingleton<ScadaRealtimeSubscriptionRegistry>();

        if (configuration.GetValue<bool>(
            "RuntimeFeatures:EnableProductionProjection"))
        {
            services.AddHostedService<ScadaProductionProjectionService>();
        }

        if (configuration.GetValue<bool>(
            "RuntimeFeatures:EnableEnergyProjection"))
        {
            services.AddHostedService<EnergyProjectionService>();
        }

        if (configuration.GetValue<bool>(
            "RuntimeFeatures:EnableRealtimeMonitor"))
        {
            services.AddHostedService<ScadaRealtimeMonitorService>();
            services.AddHostedService<
                API.Realtime.Signage.SignageSignalsMonitor>();
        }

        if (configuration.GetValue<bool>(
            "RuntimeFeatures:EnableEipicoSync"))
        {
            services.AddHostedService<EipicoSyncWorker>();
        }

        if (configuration.GetValue<bool>(
            "RuntimeFeatures:EnableEmsEnergySync"))
        {
            services.AddHostedService<EmsEnergySyncWorker>();
        }

        // CORS Policies
        services.AddCors(options =>
        {
            // Default policy for regular APIs
            options.AddDefaultPolicy(policy =>
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod());

            // Policy for SignalR hubs
            options.AddPolicy("SignalRCors", policy =>
                policy
                    .WithOrigins(
                        "http://localhost:4200",
                        "http://prd.elsquare.local:4201",
                        "http://10.1.1.240:4201",
                        "http://10.1.1.240:66")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        return services;
    }
}
