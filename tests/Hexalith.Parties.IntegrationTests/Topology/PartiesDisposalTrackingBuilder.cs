using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Tracks the actual fixture builder-disposal callback without constructing another application.</summary>
public class PartiesDisposalTrackingBuilder : DispatchProxy
{
    /// <summary>Gets or sets the real model services used by composition cleanup.</summary>
    public IServiceCollection Services { get; set; } = null!;

    /// <summary>Gets or sets the owned backing-removal operation invoked by actual builder disposal.</summary>
    public Func<Task> Dispose { get; set; } = null!;

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        "get_Services" => Services,
        "DisposeAsync" => new ValueTask(Dispose()),
        _ => throw new InvalidOperationException("Unexpected testing-builder operation: " + targetMethod?.Name),
    };
}
