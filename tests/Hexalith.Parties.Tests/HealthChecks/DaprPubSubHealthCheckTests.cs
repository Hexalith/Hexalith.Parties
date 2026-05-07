using Dapr.Client;

using Hexalith.Parties.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class DaprPubSubHealthCheckTests
{
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();

    private readonly HealthCheckContext _context = new()
    {
        Registration = new HealthCheckRegistration(
            "dapr-pubsub",
            Substitute.For<IHealthCheck>(),
            failureStatus: HealthStatus.Degraded,
            tags: ["ready"]),
    };

    private static DaprMetadata CreateMetadata(params DaprComponentsMetadata[] components) => new(
        id: "test-app",
        actors: [],
        extended: new Dictionary<string, string>(),
        components: components);

    [Fact]
    public async Task CheckHealthAsync_PubSubComponentPresent_ReturnsHealthyAsync()
    {
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []));

        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);

        var sut = new DaprPubSubHealthCheck(_daprClient, "pubsub");

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("available");
    }

    [Fact]
    public async Task CheckHealthAsync_PubSubComponentMissing_ReturnsDegradedAsync()
    {
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata("statestore", "state.redis", "v1", []));

        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);

        var sut = new DaprPubSubHealthCheck(_daprClient, "pubsub");

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("not found");
    }

    [Fact]
    public async Task CheckHealthAsync_MetadataThrowsException_ReturnsDegradedAsync()
    {
        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();

        var sut = new DaprPubSubHealthCheck(_daprClient, "pubsub");

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("HttpRequestException");
        result.Exception.ShouldNotBeNull();
    }
}
