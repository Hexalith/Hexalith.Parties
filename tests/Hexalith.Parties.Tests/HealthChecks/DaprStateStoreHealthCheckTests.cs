using Dapr.Client;

using Hexalith.Parties.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class DaprStateStoreHealthCheckTests
{
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();

    private readonly HealthCheckContext _context = new()
    {
        Registration = new HealthCheckRegistration(
            "dapr-statestore",
            Substitute.For<IHealthCheck>(),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready"]),
    };

    [Fact]
    public async Task CheckHealthAsync_StateStoreAccessible_ReturnsHealthyAsync()
    {
        _daprClient.GetStateAsync<string?>(
            "statestore",
            "__health_check__",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var sut = new DaprStateStoreHealthCheck(_daprClient, "statestore");

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldNotBeNull().ShouldContain("accessible");
    }

    [Fact]
    public async Task CheckHealthAsync_StateStoreThrowsException_ReturnsUnhealthyAsync()
    {
        _daprClient.GetStateAsync<string?>(
            "statestore",
            "__health_check__",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();

        var sut = new DaprStateStoreHealthCheck(_daprClient, "statestore");

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("not accessible");
        result.Exception.ShouldNotBeNull();
    }
}
