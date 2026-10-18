using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>Records the categories and rendered diagnostics emitted through a real logging factory.</summary>
internal sealed class ProjectionCategoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<ProjectionCategoryLogRecord> _records = new();

    /// <summary>Gets the diagnostics observed by this sink.</summary>
    public IReadOnlyList<ProjectionCategoryLogRecord> Records => _records.ToArray();

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new ProjectionCategoryLogger(categoryName, _records);

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
