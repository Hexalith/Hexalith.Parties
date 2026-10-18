using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>Captures the category and rendered output received by a logging provider.</summary>
/// <param name="Category">The logger category.</param>
/// <param name="Level">The diagnostic severity.</param>
/// <param name="EventId">The diagnostic identifier and name.</param>
/// <param name="Message">The rendered message.</param>
/// <param name="Exception">The supplied exception, if any.</param>
/// <param name="Fields">The structured diagnostic fields.</param>
internal sealed record ProjectionCategoryLogRecord(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Fields);
