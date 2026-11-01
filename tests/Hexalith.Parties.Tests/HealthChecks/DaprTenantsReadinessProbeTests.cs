using System.Net;

using Hexalith.Parties.Configuration;
using Hexalith.Parties.HealthChecks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

/// <summary>Exercises the actual readiness HTTP probe and health decision through its explicit receiver binding.</summary>
public sealed class DaprTenantsReadinessProbeTests
{
    /// <summary>The actual selected receiver response governs health; a configured address cannot fabricate readiness.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BoundReceiver_RequiresActualReadyResponseAsync(bool ready)
    {
        int requests = 0;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        await using WebApplication receiver = builder.Build();
        receiver.MapGet("/ready", (HttpContext context) =>
        {
            context.Request.Host.Value.ShouldBe("selected-tenants");
            requests++;
            return Results.StatusCode(ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
        await receiver.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        ServiceCollection services = new();
        services.AddHttpClient("tenants-readiness-probe")
            .ConfigurePrimaryHttpMessageHandler(() => receiver.GetTestServer().CreateHandler());
        using ServiceProvider provider = services.BuildServiceProvider();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tenants:ReadinessBaseAddress"] = "http://selected-tenants/",
        }).Build();
        DaprTenantsReadinessProbe probe = new(provider.GetRequiredService<IHttpClientFactory>(), configuration);
        TenantsIntegrationHealthCheck check = new(Options.Create(new TenantIntegrationOptions { Enabled = true, ServiceName = "tenants-actual-session" }), probe);
        HealthCheckContext context = new()
        {
            Registration = new HealthCheckRegistration("tenants-integration", check, HealthStatus.Unhealthy, ["ready"]),
        };

        HealthCheckResult result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken).ConfigureAwait(true);

        requests.ShouldBe(1);
        result.Status.ShouldBe(ready ? HealthStatus.Healthy : HealthStatus.Unhealthy);
        result.Exception.ShouldBeNull();
    }

    /// <summary>Ambiguous and non-HTTP receiver bindings refuse before issuing an HTTP request.</summary>
    [Theory]
    [InlineData("relative")]
    [InlineData("ftp://tenants/")]
    [InlineData("http://user@tenants/")]
    [InlineData("http://tenants/other")]
    [InlineData("http://tenants/?route=other")]
    [InlineData("http://tenants/#other")]
    public async Task MalformedReceiverBinding_RefusesAsync(string binding)
    {
        ServiceCollection services = new();
        services.AddHttpClient();
        using ServiceProvider provider = services.BuildServiceProvider();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tenants:ReadinessBaseAddress"] = binding,
        }).Build();
        DaprTenantsReadinessProbe probe = new(provider.GetRequiredService<IHttpClientFactory>(), configuration);
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(
            () => probe.IsReadyAsync("tenants-actual-session", TestContext.Current.CancellationToken)).ConfigureAwait(true);
        failure.Message.ShouldBe("The explicitly supplied Tenants readiness endpoint is invalid.");
    }
}
