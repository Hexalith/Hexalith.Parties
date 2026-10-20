using Dapr.Client;
using Hexalith.Parties.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class ConfiguredComponentHealthCheckTests
{
    [Fact]
    public async Task RegistrationsProbeBoundNamesAndRetainReadinessSemantics()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        dapr.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(new DaprMetadata(
            "parties", [], new Dictionary<string, string>(),
            [new DaprComponentsMetadata("custom-events", "pubsub.redis", "v1", [])]));
        ServiceCollection services = new();
        services.AddSingleton(dapr);
        services.AddLogging();
        services.AddHealthChecks().AddPartiesDaprHealthChecks("custom-security", "custom-events");
        using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckServiceOptions options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration state = options.Registrations.Single(check => check.Name == "dapr-statestore");
        HealthCheckRegistration events = options.Registrations.Single(check => check.Name == "dapr-pubsub");
        state.Tags.ShouldContain("ready");
        state.FailureStatus.ShouldBe(HealthStatus.Unhealthy);
        events.Tags.ShouldNotContain("ready");
        events.FailureStatus.ShouldBe(HealthStatus.Degraded);
        (await state.Factory(provider).CheckHealthAsync(new HealthCheckContext { Registration = state })).Status.ShouldBe(HealthStatus.Healthy);
        (await events.Factory(provider).CheckHealthAsync(new HealthCheckContext { Registration = events })).Status.ShouldBe(HealthStatus.Healthy);
        await dapr.Received(1).GetStateAsync<string>("custom-security", "__health_check__", cancellationToken: Arg.Any<CancellationToken>());
    }
}
