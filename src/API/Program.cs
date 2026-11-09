using API.Extensions;
using API.Realtime.Alerts;
using API.Realtime.Scada;
using Infrastructure.Identity;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddMesPlatformApiServices(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

// Swagger
app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "v1/swagger.json",
        "MES Platform API v1");

    options.RoutePrefix = "swagger";
});

// Static Files
app.UseStaticFiles();

// Routing must run before CORS
app.UseRouting();

// Separate CORS handling for SignalR.
// Every request passes through only one CORS policy.
app.UseCors();
//app.UseWhen(
//    context =>
//        context.Request.Path.StartsWithSegments("/signalsHub") ||
//        context.Request.Path.StartsWithSegments("/hubs/scaleStatus"),
//    branch =>
//    {
//        branch.UseCors("SignalRCors");
//    });

//// Keep the original AllowAll policy for all other endpoints.
//app.UseWhen(
//    context =>
//        !context.Request.Path.StartsWithSegments("/signalsHub") &&
//        !context.Request.Path.StartsWithSegments("/hubs/scaleStatus"),
//    branch =>
//    {
//        branch.UseCors("AllowAll");
//    });

// Root endpoint
app.MapGet("/", () => Results.Redirect("/swagger"));

// Identity Seeding
if (builder.Configuration.GetValue<bool>(
    "RuntimeFeatures:EnableIdentitySeed"))
{
    await app.Services.SeedMesIdentityAsync();
}

// HTTPS Redirection
if (builder.Configuration.GetValue<bool>(
    "Hosting:UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Controllers
app.MapControllers();

// SCADA Realtime Hub
app.MapHub<ScadaRealtimeHub>("/hubs/scada");

// Signage Signals Hub
app.MapHub<API.Realtime.Signage.SignageSignalsHub>(
    "/signalsHub")
    .RequireCors("SignalRCors");

app.MapHub<API.Realtime.Signage.SignageScaleStatusHub>(
    "/hubs/scaleStatus")
    .RequireCors("SignalRCors");

// EMS-compatible realtime alerts used by the Energy Dashboard.
app.MapHub<AlertsHub>(AlertsHub.Route)
    .RequireCors("SignalRCors");

app.Run();
