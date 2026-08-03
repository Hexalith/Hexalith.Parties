using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Tests.Gateway;

internal sealed class RecordingLogger<T> : ILogger<T>
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
