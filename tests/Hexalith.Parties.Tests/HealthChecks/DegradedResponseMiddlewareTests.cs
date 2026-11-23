using Hexalith.Parties.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
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
    [InlineData("/healthz")]
    [InlineData("/healthz/")]
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

    [Fact]
    public async Task HealthyRequestCanResetSharedStateWhileDegradedRequestPreparesHeadersAsync()
    {
        var health = Substitute.For<HealthCheckService>();
        health.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(PubSubReport(HealthStatus.Degraded), PubSubReport(HealthStatus.Healthy));
        var middleware = new DegradedResponseMiddleware(_ => Task.CompletedTask, health);
        var degraded = CreateHttpContext("GET");
        var healthy = CreateHttpContext("GET");
        var first = Substitute.For<HttpContext>();
        first.Request.Returns(degraded.Request);
        Task? healthyRequest = null;
        first.Response.Returns(_ =>
        {
            // Start a healthy request after the degraded health check and before its headers are set.
            healthyRequest ??= middleware.InvokeAsync(healthy);
            return degraded.Response;
        });

        await middleware.InvokeAsync(first);
        await healthyRequest.ShouldNotBeNull();
        degraded.Response.Headers["X-Service-Degraded"].ToString().ShouldBe("true");
        healthy.Response.Headers.ContainsKey("X-Service-Degraded").ShouldBeFalse();
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(503, false)]
    [InlineData(503, true)]
    public async Task StartedResponsePreservesStatusHeadersAndDownstreamExceptionAsync(int status, bool downstreamThrows)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using WebApplication app = builder.Build();
        var failure = new IOException("downstream failed after starting the response");
        Exception? observedFailure = null;
        var middleware = new DegradedResponseMiddleware(async context =>
        {
            context.Response.StatusCode = status;
            await context.Response.StartAsync().ConfigureAwait(false);
            context.Response.HasStarted.ShouldBeTrue();
            if (downstreamThrows) { throw failure; }
        }, CreateHealthCheckService(HealthStatus.Degraded));
        app.Run(async context =>
        {
            try { await middleware.InvokeAsync(context).ConfigureAwait(false); }
            catch (Exception ex) { observedFailure = ex; }
            await context.Response.WriteAsync("finished").ConfigureAwait(false);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("finished");
        ((int)response.StatusCode).ShouldBe(status);
        observedFailure.ShouldBeSameAs(downstreamThrows ? failure : null);
        response.Headers.Contains("X-Service-Degraded").ShouldBe(status == 200);
        response.Headers.Contains("X-Stale-Data-Age").ShouldBe(status == 200);
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
        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(PubSubReport(status));
        return service;
    }

    private static HealthReport PubSubReport(HealthStatus status) =>
        new(new Dictionary<string, HealthReportEntry>
        {
            ["dapr-pubsub"] = new(status, "test", TimeSpan.Zero, null, null),
        }, TimeSpan.Zero);

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
