using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Projections.Actors;

namespace Hexalith.Parties.Projections.Handlers;

internal static class PartySdkProjectionFold
{
    internal const string RedactedFormat = "json-redacted";
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

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
            catch (JsonException)
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

    public static DateTimeOffset ProjectedAt(IReadOnlyCollection<ProjectionEventDto> events, DateTimeOffset fallback)
        => events.Count == 0
            ? fallback
            : Max(fallback, events.Max(static item => item.Timestamp).ToUniversalTime());

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;
}
