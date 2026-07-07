using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Compliance;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Middleware;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Domain-service SDK host surface: service defaults, EventStore discovery, domain telemetry,
// and canonical DAPR-invoked endpoints are owned by Hexalith.EventStore.
builder.AddEventStoreDomainService(typeof(PartyAggregate).Assembly);

// Story 8.5 keeps the historical Hexalith.Parties telemetry source until the
// platform degraded-response / DAPR-health parity row is resolved.
builder.Services.ConfigureOpenTelemetryTracerProvider(static tracing => tracing.AddSource("Hexalith.Parties"));
builder.Services.ConfigureOpenTelemetryMeterProvider(static metrics => metrics.AddMeter("Hexalith.Parties"));

builder.Services.AddDaprClient();

// DAPR health checks:
// - readiness is gated by sidecar + state store (command-processing dependencies)
// - /health also reports pub/sub degradation and projection actor responsiveness
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

// DAPR sidecar-internal endpoints — these are NOT public REST surfaces:
//   - app.MapSubscribeHandler() exposes POST /dapr/subscribe, the DAPR pub/sub
//     subscription-discovery callback. The Parties sidecar invokes it at startup to
//     learn which topics this app subscribes to. Never called by external clients.
//   - app.MapEventStoreDomainEvents() exposes POST /tenants/events, the Tenants
//     pub/sub event-delivery callback. The Parties sidecar delivers Tenants lifecycle
//     events here after the Tenants app publishes on the shared pubsub component.
//     Delivery is enforced by the pubsub component (scoped to parties + tenants in
//     the AppHost composition), not by service-invocation access control.
// Client-facing service-invocation access is blocked by AppHost DAPR
// accesscontrol.parties.yaml (defaultAction: deny; only eventstore -> POST /process).
// EventStore is the public command/query gateway after Story 12.2.
app.MapSubscribeHandler();
app.MapEventStoreDomainEvents();
// Canonical SDK endpoints: /process, /replay-state, /query, /project, and
// /admin/operational-index-metadata. DAPR service invocation remains ACL-limited
// to only eventstore -> POST /process in accesscontrol.parties.yaml.
app.UseEventStoreDomainService();
app.MapActorsHandlers();

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
