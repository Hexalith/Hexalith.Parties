extern alias gateway;

using System.Net;
using System.Security.Cryptography;

using Hexalith.EventStore.HealthChecks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

using Credential = gateway::Hexalith.Parties.EventStoreGateway.TestHost.PrivateDaprPlacementCredential;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises the actual SDK placement probe against a protected HTTP metadata endpoint.</summary>
public sealed class PrivateDaprPlacementCredentialTests
{
    /// <summary>Preserves actual authentication, runtime readiness and the explicit private-profile boundary.</summary>
    [Theory]
    [InlineData("Development", true, "valid", true, true, true)]
    [InlineData("Development", true, "missing", true, false, false)]
    [InlineData("Development", true, "invalid", true, true, false)]
    [InlineData("Development", true, "valid", false, true, false)]
    [InlineData("Development", false, "valid", true, true, true)]
    [InlineData("Production", true, "valid", true, true, true)]
    public async Task PlacementHealth_RequiresActualPrivateCredentialAndReadyRuntimeAsync(
        string environment, bool privateProfile, string credential, bool hostReady, bool sendsHeader, bool healthy)
    {
        string expectedToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        bool headerObserved = false;
        int requests = 0;
        WebApplicationBuilder sidecarBuilder = WebApplication.CreateBuilder();
        sidecarBuilder.Logging.ClearProviders();
        sidecarBuilder.WebHost.UseTestServer();
        await using WebApplication sidecar = sidecarBuilder.Build();
        sidecar.MapGet("/v1.0/metadata", (HttpContext context) =>
        {
            requests++;
            headerObserved = context.Request.Headers.ContainsKey("dapr-api-token");
            if (context.Request.Headers["dapr-api-token"].Count != 1
                || context.Request.Headers["dapr-api-token"][0] != expectedToken)
            {
                return Results.Unauthorized();
            }

            return Results.Json(new { actorRuntime = new { hostReady, placement = "connected" } });
        });
        await sidecar.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "http://private-sidecar",
            ["DAPR_API_TOKEN"] = credential switch
            {
                "valid" => expectedToken,
                "invalid" => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                _ => null,
            },
            ["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"] = privateProfile ? "/private/run/catalog.json" : null,
        });
        builder.Services.AddHealthChecks().AddEventStoreDaprHealthChecks();
        int registrations = builder.Services.Count;
        Credential.AddTo(builder);
        if (!privateProfile || environment != Environments.Development)
        {
            builder.Services.Count.ShouldBe(registrations, "The private helper must not configure other profiles; SDK4.1 authenticates its own metadata requests.");
        }
        builder.Services.AddHttpClient<IDaprActorPlacementProbe, DaprActorPlacementProbe>()
            .ConfigurePrimaryHttpMessageHandler(() => sidecar.GetTestServer().CreateHandler());
        await using WebApplication app = builder.Build();

        app.Services.GetRequiredService<IDaprActorPlacementProbe>().ShouldBeOfType<DaprActorPlacementProbe>();
        app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("unrelated-client")
            .DefaultRequestHeaders.Contains("dapr-api-token").ShouldBeFalse();
        HealthReport report = await app.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == "dapr-actor-placement", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        requests.ShouldBe(1);
        headerObserved.ShouldBe(sendsHeader);
        report.Status.ShouldBe(healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy);
        HealthReportEntry placement = report.Entries.ShouldHaveSingleItem().Value;
        if (credential == "valid")
        {
            placement.Exception.ShouldBeNull();
            placement.Description.ShouldNotBeNull().ShouldContain(hostReady ? "host ready" : "host not ready");
        }
        else
        {
            placement.Exception.ShouldBeOfType<HttpRequestException>().StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
