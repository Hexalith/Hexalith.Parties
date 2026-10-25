using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Models;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Handlers;

internal static class PartyProcessingActivityFold
{
    /// <summary>
    /// Folds new events into the Art.30 processing-activity read model. <paramref name="logger"/>
    /// is the sole operator-facing diagnostic seam for the whole aggregate-owned Detail slot: this
    /// fold runs on every <see cref="PartyDetailSdkProjectionHandler"/> code path that
    /// observes the raw event delivery (successful project, rebuild, and the unresolved-only audit
    /// write), so passing the logger here — and leaving <c>PartyDetailSdkProjectionHandler.Fold</c>
    /// itself silent — reports every drop/skip exactly once per delivery instead of twice.
    /// The handler supplies the named <c>Hexalith.Parties.Projections.Handlers.PartyProcessingActivityFold</c>
    /// category through its optional logger factory. Direct callers may supply any optional logger.
    /// Event-derived erasure tombstones use <see cref="PartyErased.ErasedAt"/>; audit record
    /// timestamps and projection freshness continue to use source envelope timestamps.
    /// </summary>
    public static PartyProcessingSdkReadModel Fold(
        ProjectionRequest request,
        PartyProcessingSdkReadModel? current,
        ILogger? logger = null)
    {
        var records = new List<ProcessingActivityRecord>(current?.Records ?? []);
        var recordIndexes = new Dictionary<long, int>();
        for (int index = 0; index < records.Count; index++)
        {
            recordIndexes.Add(records[index].SequenceNumber, index);
        }
        long lastSequence = current?.LastSequenceNumber ?? long.MinValue;
        long? erasureSequence = current?.ErasureSequenceNumber;
        DateTimeOffset? erasedAt = current?.ErasedAt;
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;

        // Once an unresolved event is seen, the checkpoint must not advance past it even if a
        // later event in the same batch resolves and would otherwise advance the checkpoint —
        // otherwise a future redelivery (e.g. after a consumer upgrade) would never revisit the
        // still-unresolved event, and its "Failed" Art.30 record would be stuck permanently.
        bool blockedByUnresolvedEvent = false;
        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence, logger))
        {
            // `payload` is null both for a genuine JSON deserialization failure and for a
            // whole-payload-redacted event (see PartySdkProjectionFold.DeserializeNew) — the two
            // must not both be recorded as "Succeeded" in this Art.30 processing-activity log.
            // Non-advancing unresolved/non-JSON events are also recorded as Failed (deduped by
            // sequence) without moving the checkpoint so a later successful delivery can still apply.
            bool isRedacted = string.Equals(@event.SerializationFormat, PartySdkProjectionFold.RedactedFormat, StringComparison.OrdinalIgnoreCase);
            string outcome = !advance
                ? "Failed"
                : payload is not null
                    ? "Succeeded"
                    : isRedacted
                        ? "Redacted"
                        : "Failed";

            int existingRecordIndex = recordIndexes.TryGetValue(@event.SequenceNumber, out int found) ? found : -1;
            ProcessingActivityRecord derivedRecord = CreateRecord(request, @event, payload, outcome);
            if (existingRecordIndex >= 0)
            {
                // An unresolved record deliberately remains retryable at the same sequence. Once
                // an upgraded consumer resolves that event, replace the failed audit fact with
                // the now-authoritative derived fact rather than letting sequence-only dedup keep
                // the stale failure forever.
                if (advance
                    && string.Equals(records[existingRecordIndex].Outcome, "Failed", StringComparison.Ordinal))
                {
                    records[existingRecordIndex] = derivedRecord;
                }
            }
            else if (erasureSequence is null)
            {
                recordIndexes.Add(@event.SequenceNumber, records.Count);
                records.Add(derivedRecord);
            }

            if (payload is PartyErased erased)
            {
                erasureSequence = Math.Max(erasureSequence ?? long.MinValue, @event.SequenceNumber);
                erasedAt = erased.ErasedAt;
            }

            if (!advance)
            {
                blockedByUnresolvedEvent = true;
                continue;
            }

            if (blockedByUnresolvedEvent)
            {
                continue;
            }

            lastSequence = @event.SequenceNumber;
            projectedAt = PartySdkProjectionFold.ProjectedAt([@event], projectedAt);
        }

        return (current ?? new PartyProcessingSdkReadModel()) with
        {
            Records = records,
            LastSequenceNumber = lastSequence,
            ErasureSequenceNumber = erasureSequence,
            ErasedAt = erasedAt,
            ProjectedAt = projectedAt,
            ProjectionVersion = lastSequence == long.MinValue
                ? null
                : lastSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private static ProcessingActivityRecord CreateRecord(
        ProjectionRequest request,
        ProjectionEventDto @event,
        IEventPayload? payload,
        string outcome)
        => new()
        {
            SequenceNumber = @event.SequenceNumber,
            PartyId = request.AggregateId,
            TenantId = request.TenantId,
            ActorId = NormalizeMetadata(@event.UserId, "system"),
            CorrelationId = NormalizeMetadata(@event.CorrelationId, "unspecified"),
            OperationCategory = GetOperationCategory(payload),
            Outcome = outcome,
            EventType = GetShortEventTypeName(@event.EventTypeName),
            Timestamp = @event.Timestamp.ToUniversalTime(),
            Summary = CreateProcessingSummary(@event.EventTypeName, payload),
        };

    private static string CreateProcessingSummary(string eventTypeName, IEventPayload? payload)
        => payload switch
        {
            PartyCreated => "Party record created.",
            PartyDisplayNameDerived => "Party display name derived.",
            PersonDetailsUpdated => "Person details updated.",
            OrganizationDetailsUpdated => "Organization details updated.",
            ContactChannelAdded => "Contact channel added.",
            ContactChannelUpdated => "Contact channel updated.",
            ContactChannelRemoved => "Contact channel removed.",
            PreferredContactChannelChanged => "Preferred contact channel changed.",
            IdentifierAdded => "Identifier added.",
            IdentifierRemoved => "Identifier removed.",
            PartyDeactivated => "Party deactivated.",
            PartyReactivated => "Party reactivated.",
            ConsentRecorded => "Consent recorded.",
            ConsentRevoked => "Consent revoked.",
            ProcessingRestricted => "Processing restricted.",
            RestrictionLifted => "Processing restriction lifted.",
            ErasePartyRequested => "Party erasure requested.",
            PartyEncryptionKeyDeleted => "Party encryption key deleted.",
            ErasureVerified => "Party erasure verified.",
            PartyErased => "Party erased.",
            _ => $"{GetShortEventTypeName(eventTypeName)} recorded.",
        };

    private static string GetOperationCategory(IEventPayload? payload)
        => payload switch
        {
            ConsentRecorded or ConsentRevoked => "Consent",
            ProcessingRestricted or RestrictionLifted => "Restriction",
            ErasePartyRequested or PartyEncryptionKeyDeleted or ErasureVerified or PartyErased => "Erasure",
            PartyCreated or PartyDisplayNameDerived or PersonDetailsUpdated or OrganizationDetailsUpdated
                or ContactChannelAdded or ContactChannelUpdated or ContactChannelRemoved
                or PreferredContactChannelChanged or IdentifierAdded or IdentifierRemoved
                or PartyDeactivated or PartyReactivated => "PartyCommand",
            _ => "Other",
        };

    private static string GetShortEventTypeName(string? eventTypeName)
    {
        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            return "UnknownEvent";
        }

        int lastDot = eventTypeName.LastIndexOf('.');
        return lastDot >= 0 ? eventTypeName[(lastDot + 1)..] : eventTypeName;
    }

    private static string NormalizeMetadata(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
