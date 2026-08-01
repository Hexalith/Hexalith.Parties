using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Projections.Handlers;

internal static class PartyProcessingActivityFold
{
    public static PartyProcessingSdkReadModel Fold(
        ProjectionRequest request,
        PartyProcessingSdkReadModel? current)
    {
        var records = new List<ProcessingActivityRecord>(current?.Records ?? []);
        long lastSequence = current?.LastSequenceNumber ?? long.MinValue;
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;
        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence))
        {
            if (!advance)
            {
                continue;
            }

            records.Add(new ProcessingActivityRecord
            {
                SequenceNumber = @event.SequenceNumber,
                PartyId = request.AggregateId,
                TenantId = request.TenantId,
                ActorId = NormalizeMetadata(@event.UserId, "system"),
                CorrelationId = NormalizeMetadata(@event.CorrelationId, "unspecified"),
                OperationCategory = GetOperationCategory(@event.EventTypeName, payload),
                Outcome = "Succeeded",
                EventType = GetShortEventTypeName(@event.EventTypeName),
                Timestamp = @event.Timestamp.ToUniversalTime(),
                Summary = CreateProcessingSummary(@event.EventTypeName, payload),
            });
            lastSequence = @event.SequenceNumber;
            projectedAt = PartySdkProjectionFold.ProjectedAt([@event], projectedAt);
        }

        return new PartyProcessingSdkReadModel
        {
            Records = records,
            LastSequenceNumber = lastSequence,
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
