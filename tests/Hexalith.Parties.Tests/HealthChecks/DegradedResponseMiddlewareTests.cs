using Hexalith.Parties.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class DegradedResponseMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_HealthyStatus_NoHeadersAddedAsync()
    {
        HealthCheckService healthCheckService = CreateHealthCheckService(HealthStatus.Healthy);

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Service-Degraded").ShouldBeFalse();
        context.Response.Headers.ContainsKey("X-Stale-Data-Age").ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_DegradedStatus_GetRequest_HeadersAddedAsync()
    {
        HealthCheckService healthCheckService = CreateHealthCheckService(HealthStatus.Degraded);

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.Response.Headers["X-Service-Degraded"].ToString().ShouldBe("true");
        context.Response.Headers.ContainsKey("X-Stale-Data-Age").ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_DegradedStatus_PostRequest_NoHeadersAddedAsync()
    {
        HealthCheckService healthCheckService = CreateHealthCheckService(HealthStatus.Degraded);

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("POST");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Service-Degraded").ShouldBeFalse();
        context.Response.Headers.ContainsKey("X-Stale-Data-Age").ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_PostRequest_DoesNotRunHealthChecksAsync()
    {
        HealthCheckService healthCheckService = Substitute.For<HealthCheckService>();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("POST");

        await middleware.InvokeAsync(context);

        _ = healthCheckService
            .DidNotReceive()
            .CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/dapr/config")]
    [InlineData("/dapr/subscribe")]
    [InlineData("/tenants/events")]
    public async Task InvokeAsync_DaprInternalPath_DoesNotRunHealthChecksAsync(string path)
    {
        HealthCheckService healthCheckService = Substitute.For<HealthCheckService>();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET", path);

        await middleware.InvokeAsync(context);

        _ = healthCheckService
            .DidNotReceive()
            .CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_StateStoreUnavailable_GetRequest_HeadersAddedAsync()
    {
        HealthCheckService healthCheckService = CreateStateStoreUnavailableHealthCheckService();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.Response.Headers["X-Service-Degraded"].ToString().ShouldBe("true");
        context.Response.Headers.ContainsKey("X-Stale-Data-Age").ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ReadModelsDegradedButReadStillSucceeds_HeadersAddedAsync()
    {
        HealthCheckService healthCheckService = CreateReadModelsDegradedHealthCheckService();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.Response.Headers["X-Service-Degraded"].ToString().ShouldBe("true");
        context.Response.Headers.ContainsKey("X-Stale-Data-Age").ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SidecarUnavailable_GetRequest_NoHeadersAddedAsync()
    {
        HealthCheckService healthCheckService = CreateSidecarUnavailableHealthCheckService();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Service-Degraded").ShouldBeFalse();
        context.Response.Headers.ContainsKey("X-Stale-Data-Age").ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_DegradedThenHealthy_HeadersClearedAsync()
    {
        HealthCheckService degradedService = CreateHealthCheckService(HealthStatus.Degraded);

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            degradedService);

        HttpContext context1 = CreateHttpContext("GET");
        await middleware.InvokeAsync(context1);
        await context1.Response.StartAsync();

        context1.Response.Headers["X-Service-Degraded"].ToString().ShouldBe("true");

        // Switch to healthy — re-create with healthy service to verify no headers
        HealthCheckService healthyService = CreateHealthCheckService(HealthStatus.Healthy);

        var middleware2 = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthyService);

        HttpContext context2 = CreateHttpContext("GET");
        await middleware2.InvokeAsync(context2);

        context2.Response.Headers.ContainsKey("X-Service-Degraded").ShouldBeFalse();
    }

    private static HttpContext CreateHttpContext(string method, string path = "/")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private static HealthCheckService CreateHealthCheckService(HealthStatus status)
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["dapr-pubsub"] = new(status, "test", TimeSpan.Zero, null, null),
        };

        var report = new HealthReport(entries, TimeSpan.Zero);

        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);

        return service;
    }

    private static HealthCheckService CreateStateStoreUnavailableHealthCheckService()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["dapr-sidecar"] = new(HealthStatus.Healthy, "test", TimeSpan.Zero, null, null),
            ["dapr-statestore"] = new(HealthStatus.Unhealthy, "test", TimeSpan.Zero, null, null),
            ["party-read-models"] = new(HealthStatus.Healthy, "test", TimeSpan.Zero, null, null),
        };

        var report = new HealthReport(entries, TimeSpan.Zero);

        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);

        return service;
    }

    private static HealthCheckService CreateSidecarUnavailableHealthCheckService()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["dapr-sidecar"] = new(HealthStatus.Unhealthy, "test", TimeSpan.Zero, null, null),
            ["dapr-statestore"] = new(HealthStatus.Unhealthy, "test", TimeSpan.Zero, null, null),
            ["party-read-models"] = new(HealthStatus.Unhealthy, "test", TimeSpan.Zero, null, null),
        };

        var report = new HealthReport(entries, TimeSpan.Zero);

        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);

        return service;
    }

    private static HealthCheckService CreateReadModelsDegradedHealthCheckService()
    {
        // Only the production key is degraded; pub/sub stays healthy so headers prove
        // CanServeStaleReads adopts party-read-models (not a retired projection-actors key).
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["dapr-sidecar"] = new(HealthStatus.Healthy, "test", TimeSpan.Zero, null, null),
            ["dapr-pubsub"] = new(HealthStatus.Healthy, "test", TimeSpan.Zero, null, null),
            ["party-read-models"] = new(HealthStatus.Degraded, "test", TimeSpan.Zero, null, null),
        };

        var report = new HealthReport(entries, TimeSpan.Zero);

        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);

        return service;
    }
}
