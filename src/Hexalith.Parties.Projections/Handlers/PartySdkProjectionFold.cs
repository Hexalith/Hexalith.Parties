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
    /// When <paramref name="logger"/> is supplied, every drop/skip emits a distinct operator-facing
    /// diagnostic. The message templates never embed event payloads, aggregate ids, tenant ids, or
    /// correlation ids — only the event type name and sequence number, which are not personal data.
    /// A genuinely corrupt live event (<see cref="RedactedFormat"/> not set) and an expected
    /// post-erasure redacted-tail decode failure use distinct log signals so operators do not have
    /// to guess which case produced a given drop.
    /// </summary>
    public static IEnumerable<(ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint)> DeserializeNew(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber,
        ILogger? logger = null)
    {
        long checkpoint = lastSequenceNumber;
        foreach (ProjectionEventDto @event in events.OrderBy(static item => item.SequenceNumber))
        {
            if (@event.SequenceNumber <= checkpoint)
            {
                continue;
            }

            bool isRedacted = string.Equals(@event.SerializationFormat, RedactedFormat, StringComparison.OrdinalIgnoreCase);
            bool isJson = string.Equals(@event.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase) || isRedacted;
            if (!isJson)
            {
                if (logger is not null)
                {
                    Log.NonJsonEventDropped(logger, @event.EventTypeName, @event.SerializationFormat);
                }

                yield return (@event, null, false);
                continue;
            }

            Type? eventType = PartyEventTypeResolver.Resolve(@event.EventTypeName);
            if (eventType is null)
            {
                if (logger is not null)
                {
                    if (PartyEventTypeResolver.IsAmbiguousShortName(@event.EventTypeName))
                    {
                        Log.AmbiguousEventTypeDropped(logger, @event.EventTypeName);
                    }
                    else
                    {
                        Log.UnknownEventTypeDropped(logger, @event.EventTypeName);
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
                    or ArgumentNullException
                    or NotSupportedException
                    or InvalidOperationException)
            {
                deserializationFailed = true;
                if (logger is not null)
                {
                    // Distinct log signal for a genuinely corrupt live event versus an expected
                    // post-erasure redacted-tail decode failure — both skip-and-advance the
                    // checkpoint identically, but operators need to tell them apart.
                    if (isRedacted)
                    {
                        Log.RedactedEventDropped(logger, ex, @event.EventTypeName, @event.SequenceNumber);
                    }
                    else
                    {
                        Log.PayloadDeserializationFailed(logger, ex, @event.EventTypeName, @event.SequenceNumber);
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
            if (payload is null && logger is not null)
            {
                if (advance)
                {
                    // Whole-payload redaction (root-level $enc collapsed to {}) can yield a
                    // default-valued instance that does not implement IEventPayload. No exception
                    // was thrown, so this is not the same signal as RedactedEventDropped above.
                    Log.WholePayloadRedactedEventDropped(logger, @event.EventTypeName, @event.SequenceNumber);
                }
                else
                {
                    // A resolved, non-redacted event type deserialized without throwing but
                    // produced no payload (e.g. a literal JSON "null" body). The checkpoint does
                    // not advance — the same non-advancing category as an unresolved/non-JSON
                    // drop — so this needs its own distinct operator-facing signal too, not
                    // silence.
                    Log.NullPayloadEventDropped(logger, @event.EventTypeName, @event.SequenceNumber);
                }
            }

            yield return (@event, payload, advance);
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
    public static bool HasUnresolvedNewEvent(
        IReadOnlyCollection<ProjectionEventDto> events,
        long lastSequenceNumber,
        ILogger? logger = null)
        => DeserializeNew(events, lastSequenceNumber, logger).Any(static item => !item.AdvanceCheckpoint);

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

    // AC7 — structured logging contract: message templates never embed aggregate/party ids,
    // tenant ids, correlation ids, state-store keys, or exception content — only the event type
    // name, sequence number, and (where relevant) the exception's type name, none of which is
    // personal data. Exceptions are NOT passed to ILogger here: most sinks render
    // exception.ToString() (including .Message) into the emitted log text, and a syntax-level
    // JsonException from a corrupt/adversarial payload can embed a fragment of the raw offending
    // bytes in .Message (confirmed by direct testing) — a type-conversion JsonException does not,
    // but the two are not reliably distinguishable at the catch site, so both are treated as
    // unsafe to log verbatim.
    //
    // These use the plain ILogger extension methods rather than [LoggerMessage]-generated
    // partials: this project has no direct package reference to
    // Microsoft.Extensions.Logging.Abstractions (only a transitive one through the EventStore
    // SDK project references), and the [LoggerMessage] source generator does not activate for a
    // project across a ProjectReference boundary without one — the same class of .NET SDK
    // analyzer-propagation gap documented in Hexalith.Parties.csproj for a different (package
    // pruning) root cause. Plain extension-method logging avoids depending on the generator here.
    private static class Log
    {
        public static void NonJsonEventDropped(ILogger logger, string eventTypeName, string serializationFormat)
            => logger.LogWarning(
                "Party projection received event {EventTypeName} with non-JSON serialization format '{SerializationFormat}'. Event dropped without advancing the checkpoint.",
                eventTypeName,
                serializationFormat);

        public static void UnknownEventTypeDropped(ILogger logger, string eventTypeName)
            => logger.LogWarning(
                "Party projection could not resolve event type '{EventTypeName}'. Event dropped without advancing the checkpoint.",
                eventTypeName);

        public static void AmbiguousEventTypeDropped(ILogger logger, string eventTypeName)
            => logger.LogWarning(
                "Party projection found an ambiguous short event-type name '{EventTypeName}' (multiple types share this short name). Event dropped without advancing the checkpoint.",
                eventTypeName);

        // Deliberately does NOT pass the raw Exception object to ILogger: a System.Text.Json
        // syntax-level JsonException (as opposed to a type-conversion JsonException) can embed a
        // fragment of the offending raw JSON bytes/token in Exception.Message (confirmed by direct
        // testing — e.g. "'n' is an invalid start of a property name..." echoes a byte from the
        // input), and most logging sinks render exception.ToString() — including .Message — into
        // the emitted log text. A corrupt or adversarial event payload must never be able to leak
        // any of its raw bytes into an operator-visible log line. Only the exception's type name is
        // logged as a structured field, matching the same audited pattern already used by
        // PartySdkQueryService.LogQueryFailed.
        public static void PayloadDeserializationFailed(ILogger logger, Exception exception, string eventTypeName, long sequenceNumber)
            => logger.LogWarning(
                "Party projection failed to deserialize live event {EventTypeName} at sequence {SequenceNumber} with {ExceptionType}. Event dropped and checkpoint advanced past it.",
                eventTypeName,
                sequenceNumber,
                exception.GetType().Name);

        public static void RedactedEventDropped(ILogger logger, Exception exception, string eventTypeName, long sequenceNumber)
            => logger.LogInformation(
                "Party projection dropped redacted event {EventTypeName} at sequence {SequenceNumber} with {ExceptionType} (expected post-erasure deserialization failure). Checkpoint advanced.",
                eventTypeName,
                sequenceNumber,
                exception.GetType().Name);

        public static void WholePayloadRedactedEventDropped(ILogger logger, string eventTypeName, long sequenceNumber)
            => logger.LogInformation(
                "Party projection dropped whole-payload-redacted event {EventTypeName} at sequence {SequenceNumber} (no deserialization error). Checkpoint advanced.",
                eventTypeName,
                sequenceNumber);

        public static void NullPayloadEventDropped(ILogger logger, string eventTypeName, long sequenceNumber)
            => logger.LogWarning(
                "Party projection resolved and deserialized event {EventTypeName} at sequence {SequenceNumber} without error, but it produced no payload. Event dropped without advancing the checkpoint.",
                eventTypeName,
                sequenceNumber);
    }
}
