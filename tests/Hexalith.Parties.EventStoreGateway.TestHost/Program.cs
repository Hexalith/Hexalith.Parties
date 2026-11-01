using Hexalith.EventStore;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.SignalRHub;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// Native gateway defaults remain below normal appsettings, environment and command-line overrides.
foreach (string defaultsFile in new[] { "appsettings.gateway.json", $"appsettings.gateway.{builder.Environment.EnvironmentName}.json" })
{
    builder.Configuration.Sources.Insert(defaultsFile == "appsettings.gateway.json" ? 0 : 1,
        new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory),
            Path = defaultsFile,
            Optional = defaultsFile != "appsettings.gateway.json",
            ReloadOnChange = false,
        });
}

builder.AddServiceDefaults();
builder.Services.AddDaprClient();
builder.Services.AddHealthChecks()
    .AddEventStoreDaprHealthChecks()
#if HEXALITH_EVENTSTORE_CATALOG_API
    .AddEventStoreAppChannelTokenHealthCheck()
#endif
    ;
Hexalith.Parties.EventStoreGateway.TestHost.PrivateDaprPlacementCredential.AddTo(builder);
builder.Services.AddEventStore();
builder.Services.AddEventStoreServer(builder.Configuration);
builder.Services.AddEventStoreDomainQueryRouting();
#if HEXALITH_EVENTSTORE_CATALOG_API
if (!string.IsNullOrEmpty(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"]))
{
    Hexalith.EventStore.Server.Routing.RunScopedRouteCatalogGateway.AddEventStoreRunScopedCatalogGateway(builder.Services, builder.Configuration);
}
bool workloadAuthorityEnabled = builder.Configuration.GetValue<bool>("Authentication:WorkloadAuthority:Enabled");
if (workloadAuthorityEnabled)
{
    builder.Services.AddEventStoreWorkloadBindingAuthority(builder.Configuration);
}
#endif
builder.Services.AddEventStoreSignalR(builder.Configuration);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();

if (app.Configuration.GetValue("EventStore:OpenApi:Enabled", true))
{
#if HEXALITH_EVENTSTORE_CATALOG_API
    _ = app.MapOpenApi().RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
    _ = app.UseEventStoreHumanSurface("/swagger");
#else
    _ = app.MapOpenApi();
#endif
    _ = app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore API v1");
        options.RoutePrefix = "swagger";
    });
}

#if HEXALITH_EVENTSTORE_CATALOG_API
app.UseEventStoreHttpHostSecurity();
#endif
app.UseAuthorization();
#if HEXALITH_EVENTSTORE_CATALOG_API
if (!string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"]))
{
#if HEXALITH_EVENTSTORE_CATALOG_HOSTS
    Hexalith.EventStore.ServiceDefaults.Routing.FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app,
        Hexalith.EventStore.Server.Routing.RunScopedRouteCatalogGateway.Operation, technical: true);
#else
    Hexalith.EventStore.ServiceDefaults.Routing.FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app,
        Hexalith.EventStore.Server.Routing.RunScopedRouteCatalogGateway.Operation);
#endif
}
#endif
app.UseCloudEvents();
#if HEXALITH_EVENTSTORE_CATALOG_API
bool declarativeSubscriptions = app.Configuration.GetValue<bool>("Dapr:DeclarativeSubscriptions");
#endif
#if HEXALITH_EVENTSTORE_CATALOG_HOSTS
app.MapControllers().WithEventStoreProjectionNotifications(app.Services, declarativeSubscriptions);
#else
app.MapControllers();
#endif
app.MapErrorReferences();
app.MapApiVersionFallback();
#if HEXALITH_EVENTSTORE_CATALOG_API
if (!declarativeSubscriptions)
{
    app.MapSubscribeHandler().RequireEventStoreChannel();
}
app.MapActorsHandlers()
    .WithMetadata(EventStoreActorCallbackEndpointMetadata.Instance)
    .RequireEventStoreChannel();
if (workloadAuthorityEnabled)
{
    app.MapEventStoreWorkloadBindingAuthority();
}
#else
app.MapSubscribeHandler();
app.MapActorsHandlers();
#endif
app.MapApplicationRuntimeProofEndpoints();

SignalROptions? signalROptions = app.Configuration
    .GetSection("EventStore:SignalR")
    .Get<SignalROptions>();

if (signalROptions?.Enabled == true)
{
    _ = app.MapHub<ProjectionChangedHub>(ProjectionChangedHub.HubPath)
#if HEXALITH_EVENTSTORE_CATALOG_API
        .RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy)
#endif
        ;
    _ = app.MapSignalRRuntimeProofEndpoints();
}


app.Run();

/// <summary>
/// Entry point exposed for package-built gateway tests.
/// </summary>
public partial class Program;
