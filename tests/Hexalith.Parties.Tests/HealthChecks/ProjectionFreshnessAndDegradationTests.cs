using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Parties.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

// Story 2.7 — Handle Projection Freshness and Graceful Degradation.
// Test-design risk references: R-05 mixed-provenance cache (P1), R-10 cross-tenant freshness
// leakage (P1).
public sealed class ProjectionFreshnessAndDegradationTests
{
    // AC2 — Healthy current projection state must NOT emit X-Service-Degraded or X-Stale-Data-Age.
    // Reference: 2.7-GTW-090.
    [Fact]
    public async Task InvokeAsync_HealthyCurrent_NoFreshnessOrDegradedHeadersAsync()
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

    // AC3 — Stale/rebuilding projection state must emit bounded freshness/degradation metadata.
    // Values must be from the bounded vocabulary: current/stale/rebuilding/degraded/local-only/
    // unavailable. No raw sequence positions, stream names, actor ids, or exception text.
    // Reference: 2.7-GTW-091, 2.7-FIT-093.
    [Fact]
    public async Task InvokeAsync_ProjectionRebuilding_EmitsBoundedFreshnessVocabularyAsync()
    {
        HealthCheckService healthCheckService = CreateRebuildingProjectionHealthCheckService();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");
        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        string degradedHeader = context.Response.Headers["X-Service-Degraded"].ToString();
        HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            "true", "false", "current", "stale", "rebuilding", "degraded", "local-only", "unavailable",
        };
        allowed.ShouldContain(degradedHeader);

        // X-Stale-Data-Age must be a bucketed or invariant-formatted value, not a raw sequence position.
        string staleAge = context.Response.Headers["X-Stale-Data-Age"].ToString();
        staleAge.ShouldNotContain("Sequence", Case.Insensitive);
        staleAge.ShouldNotContain("Offset", Case.Insensitive);
        staleAge.ShouldNotContain("Stream", Case.Insensitive);
        staleAge.ShouldNotContain("Partition", Case.Insensitive);
        staleAge.ShouldNotContain("actor", Case.Insensitive);
    }

    // AC4 — Safe degraded reads (state-store write-path unavailable while actor state is loaded)
    // must include the bounded degraded indicator. Reference: 2.7-UNIT-040.
    [Fact]
    public async Task InvokeAsync_StateStoreUnavailableWithLoadedActorState_EmitsDegradedSignalAsync()
    {
        HealthCheckService healthCheckService = CreateStateStoreUnavailableButProjectionLoadedHealthCheckService();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");
        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.Response.Headers["X-Service-Degraded"].ToString().ShouldBe("true");
    }

    // AC5 — Sidecar unavailable must NOT pretend stale reads are safe. Reference: 2.7-UNIT-041.
    [Fact]
    public async Task InvokeAsync_SidecarUnavailable_DoesNotEmitSafeDegradedSignalAsync()
    {
        HealthCheckService healthCheckService = CreateSidecarUnavailableHealthCheckService();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");
        await middleware.InvokeAsync(context);

        // Sidecar unavailable means no safe stale-read path — degraded headers must be absent so
        // clients fall through to readiness/unavailable handling.
        context.Response.Headers.ContainsKey("X-Service-Degraded").ShouldBeFalse();
    }

    // AC3/AC6 — Degraded response headers must contain only bounded vocabulary: no projection
    // identifiers, sequence positions, actor/state keys, or rebuild internals. Cross-tenant
    // isolation at the query layer is covered by PartyDetailProjectionQueryActorTests and
    // PartyIndexProjectionQueryActorTests; the middleware has no tenant context so this is a
    // header-shape invariant rather than a tenant-scoping assertion.
    [Fact]
    public async Task InvokeAsync_DegradedHeaders_ContainOnlyBoundedMetadataAsync()
    {
        HealthCheckService healthCheckService = CreateStateStoreUnavailableButProjectionLoadedHealthCheckService();

        var middleware = new DegradedResponseMiddleware(
            context => context.Response.WriteAsync("ok"),
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        string serializedHeaders = string.Join(
            "|",
            context.Response.Headers.Select(static h => h.Key + "=" + h.Value.ToString()));
        serializedHeaders.ShouldNotContain("party-index", Case.Insensitive);
        serializedHeaders.ShouldNotContain("party-detail", Case.Insensitive);
        serializedHeaders.ShouldNotContain("position", Case.Insensitive);
        serializedHeaders.ShouldNotContain("sequence", Case.Insensitive);
        serializedHeaders.ShouldNotContain("rebuilding", Case.Insensitive);
        serializedHeaders.ShouldNotContain("stateKey", Case.Insensitive);
        serializedHeaders.ShouldNotContain("actor", Case.Insensitive);

        // X-Stale-Data-Age must be a non-negative integer (bucketed seconds), not a raw position.
        string staleAge = context.Response.Headers["X-Stale-Data-Age"].ToString();
        long.TryParse(staleAge, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long ageSeconds).ShouldBeTrue();
        ageSeconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    // AC5 — Corrupt projection state must map to bounded unavailable/degraded outcome with
    // metadata-only diagnostics. Reference: 2.7-UNIT-042 (cross-cutting with Story 2.8 corruption).
    [Fact]
    public async Task InvokeAsync_5xxResponse_StripsDegradedHeadersEvenIfPriorWriteAttemptedThemAsync()
    {
        HealthCheckService healthCheckService = CreateHealthCheckService(HealthStatus.Degraded);

        var middleware = new DegradedResponseMiddleware(
            context =>
            {
                context.Response.StatusCode = 500;
                return context.Response.WriteAsync("server failure");
            },
            healthCheckService);

        HttpContext context = CreateHttpContext("GET");
        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.Response.Headers.ContainsKey("X-Service-Degraded").ShouldBeFalse();
        context.Response.Headers.ContainsKey("X-Stale-Data-Age").ShouldBeFalse();
    }

    private static HealthCheckService CreateHealthCheckService(HealthStatus status)
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["dapr-sidecar"] = new(HealthStatus.Healthy, "test", TimeSpan.Zero, null, null),
            ["dapr-pubsub"] = new(status, "test", TimeSpan.Zero, null, null),
        };
        var report = new HealthReport(entries, TimeSpan.Zero);
        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);
        return service;
    }

    private static HealthCheckService CreateRebuildingProjectionHealthCheckService()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["dapr-sidecar"] = new(HealthStatus.Healthy, "test", TimeSpan.Zero, null, null),
            ["projection-actors"] = new(HealthStatus.Degraded, "rebuilding", TimeSpan.Zero, null, null),
        };
        var report = new HealthReport(entries, TimeSpan.Zero);
        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);
        return service;
    }

    private static HealthCheckService CreateStateStoreUnavailableButProjectionLoadedHealthCheckService()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["dapr-sidecar"] = new(HealthStatus.Healthy, "test", TimeSpan.Zero, null, null),
            ["dapr-statestore"] = new(HealthStatus.Unhealthy, "write-path unavailable", TimeSpan.Zero, null, null),
            ["projection-actors"] = new(HealthStatus.Healthy, "loaded", TimeSpan.Zero, null, null),
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
            ["dapr-sidecar"] = new(HealthStatus.Unhealthy, "sidecar unreachable", TimeSpan.Zero, null, null),
        };
        var report = new HealthReport(entries, TimeSpan.Zero);
        HealthCheckService service = Substitute.For<HealthCheckService>();
        service.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);
        return service;
    }

    private static HttpContext CreateHttpContext(string method)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = "/api/v1/queries";
        context.Response.Body = new System.IO.MemoryStream();
        return context;
    }
}
