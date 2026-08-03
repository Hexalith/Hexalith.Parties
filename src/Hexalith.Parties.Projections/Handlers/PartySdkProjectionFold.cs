using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Projections.Actors;

namespace Hexalith.Parties.Projections.Handlers;

internal static class PartySdkProjectionFold
{
    internal const string DeliverySequenceGapReason = "delivery-sequence-gap";
    internal const string UnresolvedOrUnsupportedEventReason = "unresolved-or-unsupported-event";
    internal const string RedactedFormat = "json-redacted";
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

    /// <summary>
    /// Yields events newer than <paramref name="lastSequenceNumber"/> in sequence order.
    /// <list type="bullet">
    /// <item>Non-JSON / unresolved types: <c>(null, advance: false)</c> — checkpoint must not move.</item>
    /// <item>Corrupt JSON (<see cref="JsonException"/> and related): <c>(null, advance: true)</c> — skip permanently.</item>
    /// <item>Successful payload or whole-payload redacted: <c>(payload?, advance: true)</c>.</item>
    /// </list>
    /// </summary>
    public static IEnumerable<(ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint)> DeserializeNew(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber)
    {
        long checkpoint = lastSequenceNumber;
        foreach (ProjectionEventDto @event in events.OrderBy(static item => item.SequenceNumber))
        {
            if (@event.SequenceNumber <= checkpoint)
            {
                continue;
            }

            bool isJson = string.Equals(@event.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(@event.SerializationFormat, RedactedFormat, StringComparison.OrdinalIgnoreCase);
            if (!isJson)
            {
                yield return (@event, null, false);
                continue;
            }

            Type? eventType = PartyEventTypeResolver.Resolve(@event.EventTypeName);
            if (eventType is null)
            {
                yield return (@event, null, false);
                continue;
            }

            object? deserialized = null;
            bool deserializationFailed = false;
            try
            {
                deserialized = JsonSerializer.Deserialize(@event.Payload, eventType, s_jsonOptions);
            }
            catch (Exception ex) when (
                ex is JsonException
                    or ArgumentNullException
                    or NotSupportedException
                    or InvalidOperationException)
            {
                deserializationFailed = true;
            }

            if (deserializationFailed)
            {
                checkpoint = @event.SequenceNumber;
                yield return (@event, null, true);
                continue;
            }

            bool advance = deserialized is IEventPayload
                || string.Equals(@event.SerializationFormat, RedactedFormat, StringComparison.OrdinalIgnoreCase);
            if (advance)
            {
                checkpoint = @event.SequenceNumber;
            }

            yield return (@event, deserialized as IEventPayload, advance);
        }
    }

    /// <summary>
    /// Determines whether a delivery contains a new event whose type or serialization format
    /// cannot currently be resolved. Such a delivery must fail before any checkpoint is persisted
    /// so EventStore can retry it after the consumer is upgraded.
    /// </summary>
    public static bool HasUnresolvedNewEvent(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber)
        => DeserializeNew(events, lastSequenceNumber).Any(static item => !item.AdvanceCheckpoint);

    /// <summary>
    /// Returns a bounded retry reason when a delivery cannot safely be folded from the supplied
    /// aggregate checkpoint. Aggregate sequences are one-based and contiguous across deliveries.
    /// </summary>
    public static string? GetDeliveryFailureReason(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber)
    {
        long checkpoint = lastSequenceNumber;
        foreach (ProjectionEventDto @event in events.OrderBy(static item => item.SequenceNumber))
        {
            if (@event.SequenceNumber <= checkpoint)
            {
                continue;
            }

            long expected = checkpoint == long.MinValue ? 1 : checked(checkpoint + 1);
            if (@event.SequenceNumber != expected)
            {
                return DeliverySequenceGapReason;
            }

            checkpoint = @event.SequenceNumber;
        }

        return HasUnresolvedNewEvent(events, lastSequenceNumber)
            ? UnresolvedOrUnsupportedEventReason
            : null;
    }

    public static DateTimeOffset ProjectedAt(IReadOnlyCollection<ProjectionEventDto> events, DateTimeOffset fallback)
        => events.Count == 0
            ? fallback
            : Max(fallback, events.Max(static item => item.Timestamp).ToUniversalTime());

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;
}
