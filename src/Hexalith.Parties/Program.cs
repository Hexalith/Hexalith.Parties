using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Parties.Compliance;
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
//   - app.MapTenantEventSubscription() exposes POST /tenants/events, the Tenants
//     pub/sub event-delivery callback. The Parties sidecar delivers Tenants lifecycle
//     events here after the Tenants app publishes on the shared pubsub component.
//     Delivery is enforced by the pubsub component (scoped to parties + tenants in
//     the AppHost composition), not by service-invocation access control.
// Client-facing service-invocation access is blocked by AppHost DAPR
// accesscontrol.parties.yaml (defaultAction: deny; only eventstore -> POST /process).
// EventStore is the public command/query gateway after Story 12.2.
app.MapSubscribeHandler();
app.MapTenantEventSubscription();
app.MapPost("/process", static async (
    DomainServiceRequest request,
    IDomainServiceInvoker invoker,
    CancellationToken cancellationToken) =>
{
    DomainResult result = await invoker
        .InvokeAsync(request.Command, request.CurrentState, cancellationToken)
        .ConfigureAwait(false);
    return Results.Json(DomainServiceWireResult.FromDomainResult(result));
}).ExcludeFromDescription();
app.MapActorsHandlers();
app.MapDefaultEndpoints();                    // Health checks: /health, /alive, /ready

app.Run();

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
