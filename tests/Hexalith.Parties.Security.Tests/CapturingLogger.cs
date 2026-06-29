using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Parties.Security.Tests;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => NullLogger<T>.Instance.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _messages.Add(formatter(state, exception));
        if (exception is not null)
        {
            _messages.Add(exception.GetType().Name);
            _messages.Add(exception.Message);
        }
    }
}
