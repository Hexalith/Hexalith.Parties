using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Projections.Configuration;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class PartyReadModelHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ProbeTimeoutReturnsDegradedWithoutExceptionLeakAsync()
    {
        var check = new PartyReadModelHealthCheck(
            new CancelingReadModelStore(),
            Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = "statestore" }),
            NullLogger<PartyReadModelHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "party-read-models",
                check,
                failureStatus: HealthStatus.Degraded,
                tags: []),
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        HealthCheckResult result = await check.CheckHealthAsync(context, cancellation.Token);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe("SDK read-model health probe timed out.");
        result.Exception.ShouldBeNull();
        result.Data["stateStore"].ShouldBe("statestore");
    }
}
