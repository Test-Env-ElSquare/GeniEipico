using API.Extensions;
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

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI(options =>
//    {
//        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MES Platform API v1");
//        options.RoutePrefix = "swagger";
//    });
//}


app.UseSwagger();
//app.UseSwaggerUI();
////app.UseMiddleware<ExceptionMiddleware>();   // ? add (must be before UseCors/Auth)

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("v1/swagger.json", "MES Platform API v1");
    options.RoutePrefix = "swagger";
});
app.UseStaticFiles();
app.UseCors("AllowAll");

app.MapGet("/", () => Results.Redirect("/swagger"));

if (builder.Configuration.GetValue<bool>("RuntimeFeatures:EnableIdentitySeed"))
{
    await app.Services.SeedMesIdentityAsync();
}

if (builder.Configuration.GetValue<bool>("Hosting:UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ScadaRealtimeHub>("/hubs/scada");
app.Run();
