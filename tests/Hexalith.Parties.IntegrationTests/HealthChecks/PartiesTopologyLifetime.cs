using Microsoft.Extensions.Hosting;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Leaves console interruption to the test runner so owned quiescence precedes embedded Aspire shutdown.</summary>
internal sealed class PartiesTopologyLifetime : IHostLifetime
{
    /// <summary>Starts under explicit fixture custody without registering competing console signal handlers.</summary>
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Allows the fixture's awaited StopAsync/DisposeAsync sequence to complete normally.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
