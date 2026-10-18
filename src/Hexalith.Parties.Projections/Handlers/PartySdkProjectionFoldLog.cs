using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Provides payload-safe diagnostics for SDK projection event folding.</summary>
/// <remarks>
/// Templates contain event type names, serialization formats, sequence numbers, and exception
/// type names. Event type names and serialization formats remain producer-controlled metadata;
/// scalar parameters do not sanitize their content. These methods exclude event payloads,
/// separately supplied aggregate, tenant, and correlation identifiers, and state-store keys.
/// Raw exceptions must not reach logging sinks: a syntax-level
/// <see cref="System.Text.Json.JsonException"/> can include offending payload bytes in its
/// message, and sinks commonly render the exception text. Callers convert exceptions to type
/// names before invoking these methods, which accept scalar metadata instead of raw exceptions.
/// Handlers supply named fold loggers: Detail uses the processing-activity fold category and
/// Index uses the SDK fold category. Direct fold callers retain their supplied logger's category;
/// this generated logging type does not create or change categories.
/// </remarks>
internal static partial class PartySdkProjectionFoldLog
{
    /// <summary>Reports an event whose serialization format is unsupported.</summary>
    /// <param name="logger">The caller's logger.</param>
    /// <param name="eventTypeName">The event type name.</param>
    /// <param name="serializationFormat">The serialization format.</param>
    [LoggerMessage(
        EventId = 4802,
        Level = LogLevel.Warning,
        Message = "Party projection received event {EventTypeName} with non-JSON serialization format '{SerializationFormat}'. Event dropped without advancing the checkpoint.")]
    internal static partial void NonJsonEventDropped(ILogger logger, string eventTypeName, string serializationFormat);

    /// <summary>Reports an event whose type cannot be resolved.</summary>
    /// <param name="logger">The caller's logger.</param>
    /// <param name="eventTypeName">The event type name.</param>
    [LoggerMessage(
        EventId = 4803,
        Level = LogLevel.Warning,
        Message = "Party projection could not resolve event type '{EventTypeName}'. Event dropped without advancing the checkpoint.")]
    internal static partial void UnknownEventTypeDropped(ILogger logger, string eventTypeName);

    /// <summary>Reports an event whose short type name has multiple matches.</summary>
    /// <param name="logger">The caller's logger.</param>
    /// <param name="eventTypeName">The event type name.</param>
    [LoggerMessage(
        EventId = 4804,
        Level = LogLevel.Warning,
        Message = "Party projection found an ambiguous short event-type name '{EventTypeName}' (multiple types share this short name). Event dropped without advancing the checkpoint.")]
    internal static partial void AmbiguousEventTypeDropped(ILogger logger, string eventTypeName);

    /// <summary>Reports a corrupt live payload that is skipped permanently.</summary>
    /// <param name="logger">The caller's logger.</param>
    /// <param name="eventTypeName">The event type name.</param>
    /// <param name="sequenceNumber">The event sequence number.</param>
    /// <param name="exceptionType">The exception type name without exception content.</param>
    [LoggerMessage(
        EventId = 4805,
        Level = LogLevel.Warning,
        Message = "Party projection failed to deserialize live event {EventTypeName} at sequence {SequenceNumber} with {ExceptionType}. Event dropped and checkpoint advanced past it.")]
    internal static partial void PayloadDeserializationFailed(ILogger logger, string eventTypeName, long sequenceNumber, string exceptionType);

    /// <summary>Reports an expected post-erasure payload deserialization failure.</summary>
    /// <param name="logger">The caller's logger.</param>
    /// <param name="eventTypeName">The event type name.</param>
    /// <param name="sequenceNumber">The event sequence number.</param>
    /// <param name="exceptionType">The exception type name without exception content.</param>
    [LoggerMessage(
        EventId = 4806,
        Level = LogLevel.Information,
        Message = "Party projection dropped redacted event {EventTypeName} at sequence {SequenceNumber} with {ExceptionType} (expected post-erasure deserialization failure). Checkpoint advanced.")]
    internal static partial void RedactedEventDropped(ILogger logger, string eventTypeName, long sequenceNumber, string exceptionType);

    /// <summary>Reports a whole-payload redaction that advances the checkpoint.</summary>
    /// <param name="logger">The caller's logger.</param>
    /// <param name="eventTypeName">The event type name.</param>
    /// <param name="sequenceNumber">The event sequence number.</param>
    [LoggerMessage(
        EventId = 4807,
        Level = LogLevel.Information,
        Message = "Party projection dropped whole-payload-redacted event {EventTypeName} at sequence {SequenceNumber} (no deserialization error). Checkpoint advanced.")]
    internal static partial void WholePayloadRedactedEventDropped(ILogger logger, string eventTypeName, long sequenceNumber);

    /// <summary>Reports a live event that deserializes without producing a payload.</summary>
    /// <param name="logger">The caller's logger.</param>
    /// <param name="eventTypeName">The event type name.</param>
    /// <param name="sequenceNumber">The event sequence number.</param>
    [LoggerMessage(
        EventId = 4808,
        Level = LogLevel.Warning,
        Message = "Party projection resolved and deserialized event {EventTypeName} at sequence {SequenceNumber} without error, but it produced no payload. Event dropped without advancing the checkpoint.")]
    internal static partial void NullPayloadEventDropped(ILogger logger, string eventTypeName, long sequenceNumber);
}
