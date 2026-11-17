using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Builds a configuration-only fixture application without executing an AppHost entrypoint.</summary>
/// <param name="options">The same owned fixture model settings.</param>
internal sealed class PartiesConfigurationTestingBuilder(DistributedApplicationOptions options)
    : DistributedApplicationBuilder(options), IDistributedApplicationTestingBuilder
{
    private DistributedApplication? _application;

    /// <summary>Builds the application whose actual disposal remains owned by the fixture.</summary>
    public new DistributedApplication Build() => _application = base.Build();

    /// <summary>Builds without queuing an independent AppHost Run call.</summary>
    public Task<DistributedApplication> BuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Build());
    }

    /// <inheritdoc />
    public void Dispose() => _application?.Dispose();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _application?.DisposeAsync() ?? ValueTask.CompletedTask;
}
