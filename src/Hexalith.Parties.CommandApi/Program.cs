using Hexalith.Parties.CommandApi.Extensions;
using Hexalith.Parties.CommandApi.Middleware;
using Hexalith.Parties.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Service defaults (OpenTelemetry, health checks, resilience, service discovery)
builder.AddServiceDefaults();

builder.Services.AddDaprClient();
builder.Services.AddParties(builder.Configuration);

WebApplication app = builder.Build();

// GDPR compliance warning (FR62) — non-dismissable, logged at startup
ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.Parties");
startupLogger.LogWarning(
    "GDPR Notice: This MVP does not include GDPR compliance features "
    + "(crypto-shredding, consent, erasure). Do not store regulated EU personal data. "
    + "See v1.1 roadmap.");

// OpenAPI/Swagger UI (development mode only)
if (app.Environment.IsDevelopment())
{
    _ = app.MapOpenApi();
    _ = app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith.Parties API v1");
    });
}

// Middleware pipeline (order matters)
app.UseMiddleware<GdprWarningMiddleware>();  // FIRST — every response gets GDPR header
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapMcp().RequireAuthorization();
app.MapActorsHandlers();
app.MapDefaultEndpoints();                    // Health checks: /health, /alive, /ready

app.Run();

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
