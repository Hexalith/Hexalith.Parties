using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>Records provider output after the real logging factory applies category filters.</summary>
/// <param name="category">The category requested from the provider.</param>
/// <param name="records">The sink receiving rendered diagnostics.</param>
internal sealed class ProjectionCategoryLogger(
    string category,
    ConcurrentQueue<ProjectionCategoryLogRecord> records) : ILogger
{
    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        records.Enqueue(new ProjectionCategoryLogRecord(category, logLevel, eventId,
            formatter(state, exception), exception,
            state is IEnumerable<KeyValuePair<string, object?>> fields
                ? fields.ToDictionary(static field => field.Key, static field => field.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal)));
    }
}
