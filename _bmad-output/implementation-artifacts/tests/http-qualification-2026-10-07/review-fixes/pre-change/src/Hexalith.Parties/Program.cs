using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Compliance;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Middleware;
using Hexalith.Parties.Projections.Handlers;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Domain-service SDK host surface: service defaults, EventStore discovery, domain telemetry,
// and canonical DAPR-invoked endpoints are owned by Hexalith.EventStore.
builder.AddEventStoreDomainService(
    typeof(PartyAggregate).Assembly,
    typeof(PartyDetailProjectionHandler).Assembly);

// Story 8.5 keeps the historical Hexalith.Parties telemetry source until the
// platform degraded-response / DAPR-health parity row is resolved.
builder.Services.ConfigureOpenTelemetryTracerProvider(static tracing => tracing.AddSource("Hexalith.Parties"));
builder.Services.ConfigureOpenTelemetryMeterProvider(static metrics => metrics.AddMeter("Hexalith.Parties"));

builder.Services.AddDaprClient();

// DAPR health checks:
// - readiness is gated by sidecar + state store (command-processing dependencies)
// - /health also reports pub/sub degradation and SDK read-model store reachability
builder.Services.AddHealthChecks().AddPartiesDaprHealthChecks();

builder.Services.AddParties(builder.Configuration);
builder.Services.Configure<HealthCheckServiceOptions>(RemoveEventStoreDefaultSelfCheck);

WebApplication app = builder.Build();

// GDPR compliance warning (FR62) — non-dismissable, logged at startup
ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.Parties");
if (!app.Configuration.GetValue<bool>(MvpComplianceWarning.ActivationConfigurationKey))
{
    startupLogger.LogWarning("{ComplianceWarning}", MvpComplianceWarning.Message);
}

// Middleware pipeline (order matters)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<MvpComplianceWarningMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<DegradedResponseMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseCloudEvents();

// Sidecar discovery, pub/sub delivery and actor runtime calls require the SDK's
// authenticated Dapr application channel. Canonical gateway operations additionally
// require a workload assertion for this service and their exact route operation.
app.MapSubscribeHandler().RequireEventStoreSidecarChannel();
app.MapEventStoreDomainEvents();
app.MapActorsHandlers().RequireEventStoreSidecarChannel();
app.UseEventStoreDomainService();

app.Run();

static void RemoveEventStoreDefaultSelfCheck(HealthCheckServiceOptions options)
{
    ArgumentNullException.ThrowIfNull(options);

    foreach (HealthCheckRegistration registration in options.Registrations
        .Where(static registration => string.Equals(registration.Name, "self", StringComparison.Ordinal))
        .ToArray())
    {
        _ = options.Registrations.Remove(registration);
    }
}

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
