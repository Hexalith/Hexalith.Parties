using Hexalith.Commons.ServiceDefaults;
using Hexalith.Parties.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class ServiceDefaultsCompatibilityTests
{
    [Fact]
    public void AddDefaultHealthChecks_DoesNotRegisterCommonsSelfCheck()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddDefaultHealthChecks();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        HealthCheckServiceOptions options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        options.Registrations.ShouldNotContain(static registration => registration.Name == "self");
    }

    [Fact]
    public void MapDefaultEndpoints_MapsPartiesHealthPaths()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddDefaultHealthChecks();
        WebApplication app = builder.Build();

        _ = app.MapDefaultEndpoints();

        string[] routes = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)];
        routes.ShouldContain("/health");
        routes.ShouldContain("/alive");
        routes.ShouldContain("/ready");
    }

    [Fact]
    public void HealthStatusCodes_PreservePartiesMapping_DegradedOkUnhealthyServiceUnavailable()
    {
        // Parties relies on the shared mapping where Degraded is still serviceable (200)
        // and only Unhealthy returns 503. MapDefaultEndpoints delegates to this mapping.
        IDictionary<HealthStatus, int> statusCodes = HexalithServiceDefaults.CreateHealthStatusCodes();

        statusCodes[HealthStatus.Healthy].ShouldBe(StatusCodes.Status200OK);
        statusCodes[HealthStatus.Degraded].ShouldBe(StatusCodes.Status200OK);
        statusCodes[HealthStatus.Unhealthy].ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Theory]
    [InlineData("/health", false)]
    [InlineData("/alive", false)]
    [InlineData("/ready", false)]
    [InlineData("/process", true)]
    public void ShouldTraceHttpRequest_ExcludesPartiesHealthEndpoints(string path, bool expectedTraced)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        bool traced = HexalithServiceDefaults.ShouldTraceHttpRequest(
            context,
            static options =>
            {
                options.HealthEndpointPath = "/health";
                options.LivenessEndpointPath = "/alive";
                options.ReadinessEndpointPath = "/ready";
            });

        traced.ShouldBe(expectedTraced);
    }
}
