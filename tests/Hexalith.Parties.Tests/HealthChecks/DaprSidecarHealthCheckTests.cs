using Dapr.Client;

using Hexalith.Parties.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class DaprSidecarHealthCheckTests
{
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();

    private readonly HealthCheckContext _context = new()
    {
        Registration = new HealthCheckRegistration(
            "dapr-sidecar",
            Substitute.For<IHealthCheck>(),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready"]),
    };

    [Fact]
    public async Task CheckHealthAsync_SidecarHealthy_ReturnsHealthyAsync()
    {
        _daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new DaprSidecarHealthCheck(_daprClient, NullLogger<DaprSidecarHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldNotBeNull().ShouldContain("responsive");
    }

    [Fact]
    public async Task CheckHealthAsync_SidecarNotHealthy_ReturnsUnhealthyAsync()
    {
        _daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = new DaprSidecarHealthCheck(_daprClient, NullLogger<DaprSidecarHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("not responsive");
    }

    [Fact]
    public async Task CheckHealthAsync_SidecarThrowsException_ReturnsUnhealthyAsync()
    {
        _daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();

        var sut = new DaprSidecarHealthCheck(_daprClient, NullLogger<DaprSidecarHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("HttpRequestException");
        result.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_SidecarTimesOut_ReturnsUnhealthyAsync()
    {
        _daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync<TaskCanceledException>();

        var sut = new DaprSidecarHealthCheck(_daprClient, NullLogger<DaprSidecarHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("TaskCanceledException");
        result.Exception.ShouldNotBeNull();
    }
}
