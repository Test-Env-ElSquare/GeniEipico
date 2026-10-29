using Application.DependencyInjection;
using API.Common;
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
    public static IServiceCollection AddMesPlatformApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);
        services.AddApplicationServices();
        services.AddHttpContextAccessor();
        services.AddScoped<Application.Abstractions.Services.IOverviewAccessProvider, API.Common.OverviewAccessProvider>();
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new BatchPriorityJsonConverter());
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
            });
        services.AddSignalR();
        services.AddSingleton<ScadaRealtimeSubscriptionRegistry>();
        if (configuration.GetValue<bool>("RuntimeFeatures:EnableProductionProjection"))
            services.AddHostedService<ScadaProductionProjectionService>();
        if (configuration.GetValue<bool>("RuntimeFeatures:EnableEnergyProjection"))
            services.AddHostedService<EnergyProjectionService>();
        if (configuration.GetValue<bool>("RuntimeFeatures:EnableRealtimeMonitor"))
            services.AddHostedService<ScadaRealtimeMonitorService>();
        if (configuration.GetValue<bool>("RuntimeFeatures:EnableEipicoSync"))
            services.AddHostedService<EipicoSyncWorker>();
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        return services;
    }
}
