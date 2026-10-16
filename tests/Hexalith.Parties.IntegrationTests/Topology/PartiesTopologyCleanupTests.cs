using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Verifies cleanup runs after topology shutdown, including partial and failed startup.</summary>
public sealed class PartiesTopologyCleanupTests
{
    [Theory]
    [InlineData("")]
    [InlineData("stop")]
    [InlineData("application")]
    [InlineData("builder")]
    public async Task Cleanup_OrdersQuiescenceBeforeOwnedInfrastructureAndSurvivesFailures(string failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var events = new List<string>();
        Task Step(string name)
        {
            events.Add(name);
            return name == failure ? Task.FromException(new InvalidOperationException(name)) : Task.CompletedTask;
        }

        Task cleanup = PartiesTopologyCleanup.RunAsync(
            () => Step("stop"), () => Step("application"), () => Step("builder"), () => Step("control-plane"));
        if (failure.Length == 0)
        {
            await cleanup.ConfigureAwait(true);
        }
        else
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => cleanup)).Message.ShouldBe(failure);
        }

        events.ShouldBe(["stop", "application", "builder", "control-plane"]);
    }
    [Fact]
    public async Task Cleanup_WhenQuiescenceCannotBeConfirmed_PreservesTheControlPlane()
    {
        bool builderDisposed = false;
        bool controlPlaneRemoved = false;
        await Should.ThrowAsync<InvalidOperationException>(() => PartiesTopologyCleanup.RunAsync(
            () => Task.FromException(new InvalidOperationException("stop failed")),
            () => Task.FromException(new InvalidOperationException("disposal failed")),
            () => { builderDisposed = true; return Task.CompletedTask; },
            () => { controlPlaneRemoved = true; return Task.CompletedTask; })).ConfigureAwait(true);
        builderDisposed.ShouldBeTrue();
        controlPlaneRemoved.ShouldBeFalse();
    }
}
