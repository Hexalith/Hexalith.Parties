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
    /// </summary>
    public static PartyProcessingSdkReadModel Fold(
        ProjectionRequest request,
        PartyProcessingSdkReadModel? current,
        ILogger? logger = null)
    {
        var records = new List<ProcessingActivityRecord>(current?.Records ?? []);
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

            bool isErasure = payload is PartyErased;
            if (erasureSequence is null
                && !records.Exists(record => record.SequenceNumber == @event.SequenceNumber))
            {
                records.Add(new ProcessingActivityRecord
                {
                    SequenceNumber = @event.SequenceNumber,
                    PartyId = request.AggregateId,
                    TenantId = request.TenantId,
                    ActorId = NormalizeMetadata(@event.UserId, "system"),
                    CorrelationId = NormalizeMetadata(@event.CorrelationId, "unspecified"),
                    OperationCategory = GetOperationCategory(@event.EventTypeName, payload),
                    Outcome = outcome,
                    EventType = GetShortEventTypeName(@event.EventTypeName),
                    Timestamp = @event.Timestamp.ToUniversalTime(),
                    Summary = CreateProcessingSummary(@event.EventTypeName, payload),
                });
            }

            if (isErasure)
            {
                erasureSequence = Math.Max(erasureSequence ?? long.MinValue, @event.SequenceNumber);
                erasedAt = @event.Timestamp.ToUniversalTime();
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

        return new PartyProcessingSdkReadModel
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

    private static string GetOperationCategory(string eventTypeName, IEventPayload? payload)
        => payload switch
        {
            ConsentRecorded or ConsentRevoked => "Consent",
            ProcessingRestricted or RestrictionLifted => "Restriction",
            ErasePartyRequested or PartyEncryptionKeyDeleted or ErasureVerified or PartyErased => "Erasure",
            PartyCreated or PartyDisplayNameDerived or PersonDetailsUpdated or OrganizationDetailsUpdated
                or ContactChannelAdded or ContactChannelUpdated or ContactChannelRemoved
                or PreferredContactChannelChanged or IdentifierAdded or IdentifierRemoved
                or PartyDeactivated or PartyReactivated => "PartyCommand",
            _ => GetShortEventTypeName(eventTypeName),
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
