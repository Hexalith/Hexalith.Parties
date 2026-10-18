using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Projections.Actors;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Handlers;

internal static class PartySdkProjectionFold
{
    internal const string DeliverySequenceGapReason = "delivery-sequence-gap";
    internal const string ConflictingDuplicateEventReason = "conflicting-duplicate-event";
    internal const string UnresolvedOrUnsupportedEventReason = "unresolved-or-unsupported-event";
    internal const string RedactedFormat = "json-redacted";
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

    /// <summary>
    /// Yields events newer than <paramref name="lastSequenceNumber"/> in sequence order.
    /// <list type="bullet">
    /// <item>Whole-payload redacted (empty object or JSON null): <c>(null, advance: true)</c>, without resolving a CLR type.</item>
    /// <item>Other non-JSON / unresolved types: <c>(null, advance: false)</c> — checkpoint must not move.</item>
    /// <item>Corrupt JSON (<see cref="JsonException"/> and related): <c>(null, advance: true)</c> — skip permanently.</item>
    /// <item>Successful payload, including nonempty field-redacted JSON: <c>(payload, advance: true)</c>.</item>
    /// </list>
    /// When <paramref name="logger"/> is supplied, every drop/skip emits a distinct operator-facing
    /// diagnostic. The message templates never embed event payloads, aggregate ids, tenant ids, or
    /// correlation ids — only the event type name and sequence number, which are not personal data.
    /// A genuinely corrupt live event (<see cref="RedactedFormat"/> not set) and an expected
    /// post-erasure redacted-tail decode failure use distinct log signals so operators do not have
    /// to guess which case produced a given drop.
    /// Optional rebuild diagnostics retain only fixed counts. Supply them on one walk per
    /// accumulation, keeping validation probes silent to avoid counting the same drop twice.
    /// </summary>
    public static IEnumerable<(ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint)> DeserializeNew(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber,
        ILogger? logger = null,
        PartyProjectionRebuildDiagnostics? diagnostics = null)
    {
        long checkpoint = lastSequenceNumber;
        var seenDeliverySequences = new HashSet<long>();
        foreach (ProjectionEventDto @event in events.OrderBy(static item => item.SequenceNumber))
        {
            if (@event.SequenceNumber <= checkpoint)
            {
                continue;
            }

            if (!seenDeliverySequences.Add(@event.SequenceNumber))
            {
                continue;
            }

            bool isRedacted = string.Equals(@event.SerializationFormat, RedactedFormat, StringComparison.OrdinalIgnoreCase);
            bool isJson = string.Equals(@event.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase) || isRedacted;
            if (!isJson)
            {
                diagnostics?.RecordNonJson();
                if (logger is not null)
                {
                    PartySdkProjectionFoldLog.NonJsonEventDropped(logger, @event.EventTypeName, @event.SerializationFormat);
                }

                yield return (@event, null, false);
                continue;
            }

            if (isRedacted && IsWholePayloadRedacted(@event.Payload))
            {
                diagnostics?.RecordWholePayloadRedacted();
                checkpoint = @event.SequenceNumber;
                if (logger is not null)
                {
                    PartySdkProjectionFoldLog.WholePayloadRedactedEventDropped(logger, @event.EventTypeName, @event.SequenceNumber);
                }

                yield return (@event, null, true);
                continue;
            }

            Type? eventType = PartyEventTypeResolver.Resolve(@event.EventTypeName);
            if (eventType is null)
            {
                bool ambiguous = PartyEventTypeResolver.IsAmbiguousShortName(@event.EventTypeName);
                if (ambiguous)
                {
                    diagnostics?.RecordAmbiguousType();
                }
                else
                {
                    diagnostics?.RecordUnknownType();
                }

                if (logger is not null)
                {
                    if (ambiguous)
                    {
                        PartySdkProjectionFoldLog.AmbiguousEventTypeDropped(logger, @event.EventTypeName);
                    }
                    else
                    {
                        PartySdkProjectionFoldLog.UnknownEventTypeDropped(logger, @event.EventTypeName);
                    }
                }

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
                    or FormatException
                    or OverflowException
                    or ArgumentNullException
                    or NotSupportedException
                    or InvalidOperationException)
            {
                deserializationFailed = true;
                if (isRedacted)
                {
                    diagnostics?.RecordRedactedPayload();
                }
                else
                {
                    diagnostics?.RecordCorruptPayload();
                }

                if (logger is not null)
                {
                    // Distinct log signal for a genuinely corrupt live event versus an expected
                    // post-erasure redacted-tail decode failure — both skip-and-advance the
                    // checkpoint identically, but operators need to tell them apart.
                    string exceptionType = ex.GetType().Name;
                    if (isRedacted)
                    {
                        PartySdkProjectionFoldLog.RedactedEventDropped(logger, @event.EventTypeName, @event.SequenceNumber, exceptionType);
                    }
                    else
                    {
                        PartySdkProjectionFoldLog.PayloadDeserializationFailed(logger, @event.EventTypeName, @event.SequenceNumber, exceptionType);
                    }
                }
            }

            if (deserializationFailed)
            {
                checkpoint = @event.SequenceNumber;
                yield return (@event, null, true);
                continue;
            }

            bool advance = deserialized is IEventPayload || isRedacted;
            if (advance)
            {
                checkpoint = @event.SequenceNumber;
            }

            IEventPayload? payload = deserialized as IEventPayload;
            if (payload is null)
            {
                if (advance)
                {
                    diagnostics?.RecordWholePayloadRedacted();
                    // A remaining redacted payload can deserialize to null without an exception
                    // (for example through a converter). Preserve its skip-and-advance diagnostic.
                    if (logger is not null)
                    {
                        PartySdkProjectionFoldLog.WholePayloadRedactedEventDropped(logger, @event.EventTypeName, @event.SequenceNumber);
                    }
                }
                else
                {
                    diagnostics?.RecordNullPayload();
                    // A resolved, non-redacted event type deserialized without throwing but
                    // produced no payload (e.g. a literal JSON "null" body). The checkpoint does
                    // not advance — the same non-advancing category as an unresolved/non-JSON
                    // drop — so this needs its own distinct operator-facing signal too, not
                    // silence.
                    if (logger is not null)
                    {
                        PartySdkProjectionFoldLog.NullPayloadEventDropped(logger, @event.EventTypeName, @event.SequenceNumber);
                    }
                }
            }

            yield return (@event, payload, advance);
        }
    }

    private static bool IsWholePayloadRedacted(byte[] payload)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Null
                || (root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().MoveNext());
        }
        catch (JsonException)
        {
            // Keep malformed redacted data on the existing type-resolution/deserialization path
            // so its retry policy and privacy-safe diagnostic do not change.
            return false;
        }
    }

    /// <summary>
    /// Determines whether a delivery contains a new event whose type or serialization format
    /// cannot currently be resolved. Such a delivery must fail before any checkpoint is persisted
    /// so EventStore can retry it after the consumer is upgraded.
    /// </summary>
    /// <param name="events">The candidate events.</param>
    /// <param name="lastSequenceNumber">The current checkpoint.</param>
    /// <param name="logger">
    /// Optional logger. Only pass this when the caller can guarantee the resulting non-JSON /
    /// unknown-type / ambiguous-type diagnostics will not also be emitted by a second, independent
    /// <see cref="DeserializeNew"/> walk over the same events for the same delivery (see
    /// <see cref="PartyIndexSdkProjectionHandler"/>'s guarded use in its failure branch, which
    /// returns immediately afterward so no later walk can double-log).
    /// </param>
    /// <param name="diagnostics">Optional rebuild counts for the inspected prefix through the first unresolved event.</param>
    public static bool HasUnresolvedNewEvent(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber,
        ILogger? logger = null,
        PartyProjectionRebuildDiagnostics? diagnostics = null)
        => DeserializeNew(events, lastSequenceNumber, logger, diagnostics).Any(static item => !item.AdvanceCheckpoint);

    /// <summary>
    /// Returns a bounded retry reason when a delivery cannot safely be folded from the supplied
    /// aggregate checkpoint. Aggregate sequences are one-based and contiguous across deliveries.
    /// </summary>
    public static string? GetDeliveryFailureReason(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber)
    {
        var deliveryEventsBySequence = new Dictionary<long, ProjectionEventDto>();
        foreach (ProjectionEventDto @event in events)
        {
            if (deliveryEventsBySequence.TryGetValue(@event.SequenceNumber, out ProjectionEventDto? prior)
                && !AreEquivalent(prior, @event))
            {
                return ConflictingDuplicateEventReason;
            }

            deliveryEventsBySequence[@event.SequenceNumber] = @event;
        }

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

    private static bool AreEquivalent(ProjectionEventDto left, ProjectionEventDto right)
        => string.Equals(left.EventTypeName, right.EventTypeName, StringComparison.Ordinal)
            && (left.Payload is null
                ? right.Payload is null
                : right.Payload is not null && left.Payload.AsSpan().SequenceEqual(right.Payload))
            && string.Equals(left.SerializationFormat, right.SerializationFormat, StringComparison.Ordinal)
            && left.SequenceNumber == right.SequenceNumber
            && left.Timestamp.Equals(right.Timestamp)
            && string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal)
            && string.Equals(left.MessageId, right.MessageId, StringComparison.Ordinal)
            && string.Equals(left.UserId, right.UserId, StringComparison.Ordinal)
            && left.GlobalPosition == right.GlobalPosition;
}
