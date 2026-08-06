using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Testing;

/// <summary>
/// Minimal in-memory <see cref="ILogger{TCategoryName}"/> double that records every logged
/// message so tests can assert on operator-facing diagnostics without a real logging provider.
/// Shared by multiple test projects rather than duplicated locally in each.
/// </summary>
/// <typeparam name="T">The logging category type.</typeparam>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _records = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Records => _records;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _records.Add((logLevel, formatter(state, exception), exception));
    }
}
