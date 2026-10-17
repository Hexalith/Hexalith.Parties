using System.Collections.Concurrent;

using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Tests.Search;

/// <summary>
/// Captures the search indexer's warnings safely across concurrent notifications.
/// </summary>
internal sealed class PartyMemoryIndexEntryWarningLogger : ILogger<PartyMemoryIndexEntrySearchIndexer>
{
    private readonly ConcurrentQueue<(string Message, IReadOnlyList<KeyValuePair<string, object?>> State, Exception? Exception)> _warnings = new();

    /// <summary>Gets a snapshot of the captured warnings.</summary>
    public string[] Warnings => _warnings.Select(record => record.Message).ToArray();

    /// <summary>Gets captured structured state and exceptions as well as rendered messages.</summary>
    public (string Message, IReadOnlyList<KeyValuePair<string, object?>> State, Exception? Exception)[] Records => _warnings.ToArray();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
        {
            IReadOnlyList<KeyValuePair<string, object?>> properties = state is IEnumerable<KeyValuePair<string, object?>> structured
                ? structured.ToArray()
                : [];
            _warnings.Enqueue((formatter(state, exception), properties, exception));
        }
    }
}
