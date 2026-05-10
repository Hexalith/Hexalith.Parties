using Hexalith.Parties.Extensions;
using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Middleware;
using Hexalith.Parties.ServiceDefaults;
using Hexalith.Tenants.Client.Subscription;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Service defaults (OpenTelemetry, health checks, resilience, service discovery)
builder.AddServiceDefaults();

builder.Services.AddDaprClient();

// DAPR health checks:
// - readiness is gated by sidecar + state store (command-processing dependencies)
// - /health also reports pub/sub degradation and projection actor responsiveness
builder.Services.AddHealthChecks().AddPartiesDaprHealthChecks();

builder.Services.AddParties(builder.Configuration);

WebApplication app = builder.Build();

// GDPR compliance warning (FR62) — non-dismissable, logged at startup
ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.Parties");
startupLogger.LogWarning(
    "GDPR Notice: Personal-data encryption at rest is enabled for protected fields, but full GDPR workflows "
    + "(consent management and erasure verification) are not complete. Treat this service as partially compliant only.");

// Middleware pipeline (order matters)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<DegradedResponseMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseCloudEvents();
// DAPR sidecar-internal endpoints only. Client-facing access is blocked by
// AppHost DAPR accesscontrol.parties.yaml; EventStore is the public gateway.
app.MapSubscribeHandler();
app.MapTenantEventSubscription();
app.MapActorsHandlers();
app.MapDefaultEndpoints();                    // Health checks: /health, /alive, /ready

app.Run();

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
