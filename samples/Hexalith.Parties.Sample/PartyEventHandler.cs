using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.Sample;

/// <summary>
/// Handles party events received via DAPR pub/sub.
/// Events arrive as CloudEvents wrapping a flat EventStore Server envelope.
/// </summary>
public static class PartyEventHandler
{
    private static readonly ConcurrentDictionary<string, bool> _processedEventIds = new();

    public static void ClearProcessedEventIds() => _processedEventIds.Clear();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void MapPartyEventEndpoint(this WebApplication app)
    {
        app.MapPost("/events/parties", HandlePartyEventAsync);
    }

    private static IResult HandlePartyEventAsync(
        [FromBody] JsonElement body,
        ILogger<Program> logger)
    {
        if (!TryDeserializeEnvelope(body, out EventEnvelope? envelope, out string? cloudEventId)
            || envelope is null)
        {
            logger.LogWarning("Failed to deserialize CloudEvent payload for /events/parties");
            return Results.BadRequest();
        }

        // Primary idempotency key: CloudEvents id from DAPR delivery.
        // Fallback keeps the sample tolerant of direct local posts in tests/tools.
        string eventId = string.IsNullOrWhiteSpace(cloudEventId)
            ? $"{envelope.CorrelationId}:{envelope.SequenceNumber}"
            : cloudEventId;

        if (!_processedEventIds.TryAdd(eventId, true))
        {
            logger.LogInformation("Skipping already-processed event {EventId}", eventId);
            return Results.Ok();
        }

        // Decode the base64 payload
        byte[] payloadBytes = Convert.FromBase64String(envelope.Payload);
        string payloadJson = Encoding.UTF8.GetString(payloadBytes);

        string aggregateId = envelope.AggregateId;
        // Extract the party ID from aggregateId format "tenant:domain:partyId"
        string partyId = aggregateId.Contains(':')
            ? aggregateId[(aggregateId.LastIndexOf(':') + 1)..]
            : aggregateId;
        string eventType = NormalizeEventTypeName(envelope.EventTypeName);

        // Selective event handling: Subscribers can choose which events to handle.
        // Handle the events relevant to your domain-specific projection and ignore the rest.
        // Unhandled events should always return 200 OK to prevent DAPR redelivery loops.
        switch (eventType)
        {
            case "PartyCreated":
                HandlePartyCreated(partyId, payloadJson, logger);
                break;

            case "PersonDetailsUpdated":
                HandlePersonDetailsUpdated(partyId, payloadJson, logger);
                break;

            case "OrganizationDetailsUpdated":
                HandleOrganizationDetailsUpdated(partyId, payloadJson, logger);
                break;

            case "ContactChannelAdded":
                HandleContactChannelAdded(partyId, payloadJson, logger);
                break;

            case "ContactChannelUpdated":
                HandleContactChannelUpdated(partyId, payloadJson, logger);
                break;

            case "ContactChannelRemoved":
                HandleContactChannelRemoved(partyId, payloadJson, logger);
                break;

            case "PreferredContactChannelChanged":
                HandlePreferredContactChannelChanged(partyId, payloadJson, logger);
                break;

            case "IdentifierAdded":
                HandleIdentifierAdded(partyId, payloadJson, logger);
                break;

            case "IdentifierRemoved":
                HandleIdentifierRemoved(partyId, payloadJson, logger);
                break;

            case "PartyDeactivated":
                HandlePartyDeactivated(partyId, logger);
                break;

            case "PartyReactivated":
                HandlePartyReactivated(partyId, logger);
                break;

            case "PartyDisplayNameDerived":
                HandlePartyDisplayNameDerived(partyId, payloadJson, logger);
                break;

            case "IsNaturalPersonChanged":
                HandleIsNaturalPersonChanged(partyId, payloadJson, logger);
                break;

            // Forward-compatibility: PartyMerged (v2) — log and skip gracefully.
            // When v2 is implemented, update this handler to map survivor/merged party IDs
            // and reconcile local read models accordingly.
            case "PartyMerged":
                HandlePartyMerged(partyId, payloadJson, logger);
                break;

            // Future GDPR cleanup path: when PartyErased is introduced, consumers should
            // remove or anonymize local read-model entries for the erased party.
            case "PartyErased":
                HandlePartyErased(partyId, logger);
                break;

            // Unknown event types: always return 200 OK to prevent DAPR redelivery.
            // Other additive events will arrive here until explicit handlers are added.
            // Tolerant deserialization ensures no errors.
            default:
                logger.LogInformation(
                    "Unknown event type '{EventType}' (normalized as '{NormalizedEventType}') for aggregate {AggregateId} — acknowledged without processing",
                    envelope.EventTypeName,
                    eventType,
                    aggregateId);
                break;
        }

        return Results.Ok();
    }

    private static bool TryDeserializeEnvelope(
        JsonElement body,
        out EventEnvelope? envelope,
        out string? cloudEventId)
    {
        envelope = null;
        cloudEventId = null;

        if (body.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (body.TryGetProperty("data", out JsonElement dataElement))
        {
            envelope = dataElement.Deserialize<EventEnvelope>(_jsonOptions);

            if (body.TryGetProperty("id", out JsonElement idElement)
                && idElement.ValueKind == JsonValueKind.String)
            {
                cloudEventId = idElement.GetString();
            }

            return envelope is not null;
        }

        envelope = body.Deserialize<EventEnvelope>(_jsonOptions);
        return envelope is not null;
    }

    private static void HandlePartyCreated(string partyId, string payloadJson, ILogger logger)
    {
        PartyCreatedPayload? payload = JsonSerializer.Deserialize<PartyCreatedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize PartyCreated payload for {PartyId}", partyId);
            return;
        }

        string displayName = payload.Type == "Person" && payload.PersonDetails is not null
            ? $"{payload.PersonDetails.FirstName} {payload.PersonDetails.LastName}"
            : payload.OrganizationDetails?.LegalName ?? "Unknown";

        CustomerSummary summary = new()
        {
            Id = partyId,
            DisplayName = displayName,
            LastUpdated = DateTimeOffset.UtcNow,
        };
        CustomerSummaryStore.Customers[partyId] = summary;

        logger.LogInformation(
            "EVENT: PartyCreated -> CustomerSummary created: {PartyId} ({DisplayName})",
            partyId,
            displayName);
    }

    private static void HandleContactChannelAdded(string partyId, string payloadJson, ILogger logger)
    {
        ContactChannelAddedPayload? payload = JsonSerializer.Deserialize<ContactChannelAddedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize ContactChannelAdded payload for {PartyId}", partyId);
            return;
        }

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            UpsertContactChannel(summary, payload.ContactChannelId, payload.Type, payload.Value, payload.IsPreferred);
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: ContactChannelAdded -> Contact channel added for {PartyId}",
                partyId);
        }
    }

    private static void HandlePersonDetailsUpdated(string partyId, string payloadJson, ILogger logger)
    {
        PersonDetailsUpdatedPayload? payload = JsonSerializer.Deserialize<PersonDetailsUpdatedPayload>(payloadJson, _jsonOptions);
        if (payload?.PersonDetails is null)
        {
            logger.LogWarning("Failed to deserialize PersonDetailsUpdated payload for {PartyId}", partyId);
            return;
        }

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.DisplayName = $"{payload.PersonDetails.FirstName} {payload.PersonDetails.LastName}";
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: PersonDetailsUpdated -> DisplayName updated for {PartyId}",
                partyId);
        }
    }

    private static void HandleOrganizationDetailsUpdated(string partyId, string payloadJson, ILogger logger)
    {
        OrganizationDetailsUpdatedPayload? payload = JsonSerializer.Deserialize<OrganizationDetailsUpdatedPayload>(payloadJson, _jsonOptions);
        if (payload?.OrganizationDetails is null)
        {
            logger.LogWarning("Failed to deserialize OrganizationDetailsUpdated payload for {PartyId}", partyId);
            return;
        }

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.DisplayName = payload.OrganizationDetails.LegalName;
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: OrganizationDetailsUpdated -> DisplayName updated for {PartyId}",
                partyId);
        }
    }

    private static void HandleContactChannelUpdated(string partyId, string payloadJson, ILogger logger)
    {
        ContactChannelUpdatedPayload? payload = JsonSerializer.Deserialize<ContactChannelUpdatedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize ContactChannelUpdated payload for {PartyId}", partyId);
            return;
        }

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            if (!summary.ContactChannels.TryGetValue(payload.ContactChannelId, out CustomerContactChannel? existing)
                && (payload.Type is null || payload.Value is null))
            {
                logger.LogInformation(
                    "EVENT: ContactChannelUpdated -> Ignored for {PartyId} because channel {ContactChannelId} was not known and payload was incomplete",
                    partyId,
                    payload.ContactChannelId);
                return;
            }

            string channelType = payload.Type ?? existing!.Type;
            string channelValue = payload.Value ?? existing!.Value;
            bool isPreferred = payload.IsPreferred ?? existing?.IsPreferred ?? false;

            UpsertContactChannel(summary, payload.ContactChannelId, channelType, channelValue, isPreferred);
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: ContactChannelUpdated -> Contact updated for {PartyId}",
                partyId);
        }
    }

    private static void HandleContactChannelRemoved(string partyId, string payloadJson, ILogger logger)
    {
        ContactChannelRemovedPayload? payload = JsonSerializer.Deserialize<ContactChannelRemovedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize ContactChannelRemoved payload for {PartyId}", partyId);
            return;
        }

        logger.LogInformation(
            "EVENT: ContactChannelRemoved -> Contact removed for {PartyId}",
            partyId);

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            _ = summary.ContactChannels.TryRemove(payload.ContactChannelId, out _);
            RefreshPreferredContactFields(summary);
            summary.LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    private static void HandlePreferredContactChannelChanged(string partyId, string payloadJson, ILogger logger)
    {
        PreferredContactChannelChangedPayload? payload = JsonSerializer.Deserialize<PreferredContactChannelChangedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize PreferredContactChannelChanged payload for {PartyId}", partyId);
            return;
        }

        logger.LogInformation(
            "EVENT: PreferredContactChannelChanged -> Preferred contact updated for {PartyId}",
            partyId);

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            if (summary.ContactChannels.TryGetValue(payload.ContactChannelId, out CustomerContactChannel? selectedChannel))
            {
                foreach (KeyValuePair<string, CustomerContactChannel> channel in summary.ContactChannels)
                {
                    if (string.Equals(channel.Value.Type, selectedChannel.Type, StringComparison.Ordinal))
                    {
                        summary.ContactChannels[channel.Key] = channel.Value with { IsPreferred = channel.Key == payload.ContactChannelId };
                    }
                }

                RefreshPreferredContactFields(summary);
            }

            summary.LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    private static void HandleIdentifierAdded(string partyId, string payloadJson, ILogger logger)
    {
        IdentifierAddedPayload? payload = JsonSerializer.Deserialize<IdentifierAddedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize IdentifierAdded payload for {PartyId}", partyId);
            return;
        }

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.Identifiers[payload.IdentifierId] = new CustomerIdentifier(payload.Type, payload.Value);
            summary.IdentifierCount = summary.Identifiers.Count;
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: IdentifierAdded -> IdentifierCount incremented for {PartyId}",
                partyId);
        }
    }

    private static void HandleIdentifierRemoved(string partyId, string payloadJson, ILogger logger)
    {
        IdentifierRemovedPayload? payload = JsonSerializer.Deserialize<IdentifierRemovedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize IdentifierRemoved payload for {PartyId}", partyId);
            return;
        }

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            _ = summary.Identifiers.TryRemove(payload.IdentifierId, out _);
            summary.IdentifierCount = summary.Identifiers.Count;
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: IdentifierRemoved -> IdentifierCount decremented for {PartyId}",
                partyId);
        }
    }

    private static void HandlePartyDeactivated(string partyId, ILogger logger)
    {
        // Idempotent: setting IsActive to false multiple times is safe
        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.IsActive = false;
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: PartyDeactivated -> CustomerSummary marked inactive: {PartyId}",
                partyId);
        }
        else
        {
            logger.LogInformation(
                "EVENT: PartyDeactivated -> No CustomerSummary found for {PartyId} (idempotent skip)",
                partyId);
        }
    }

    private static void HandlePartyReactivated(string partyId, ILogger logger)
    {
        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.IsActive = true;
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: PartyReactivated -> CustomerSummary marked active: {PartyId}",
                partyId);
        }
        else
        {
            logger.LogInformation(
                "EVENT: PartyReactivated -> No CustomerSummary found for {PartyId} (idempotent skip)",
                partyId);
        }
    }

    private static void HandlePartyDisplayNameDerived(string partyId, string payloadJson, ILogger logger)
    {
        PartyDisplayNameDerivedPayload? payload = JsonSerializer.Deserialize<PartyDisplayNameDerivedPayload>(payloadJson, _jsonOptions);
        if (payload is null)
        {
            logger.LogWarning("Failed to deserialize PartyDisplayNameDerived payload for {PartyId}", partyId);
            return;
        }

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.DisplayName = payload.DisplayName;
            summary.LastUpdated = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "EVENT: PartyDisplayNameDerived -> DisplayName set to derived value for {PartyId}",
                partyId);
        }
    }

    private static void HandleIsNaturalPersonChanged(string partyId, string payloadJson, ILogger logger)
    {
        logger.LogInformation(
            "EVENT: IsNaturalPersonChanged -> Type flag updated for {PartyId}",
            partyId);

        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    private static void HandlePartyMerged(string partyId, string payloadJson, ILogger logger)
    {
        // Forward-compatibility: PartyMerged is a v2 placeholder.
        // When v2 is implemented, this handler should:
        // 1. Look up the survivor party by SurvivorPartyId
        // 2. Merge the merged party's data into the survivor
        // 3. Remove or redirect the merged party's local record
        logger.LogInformation(
            "EVENT: PartyMerged -> v2 placeholder acknowledged for {PartyId} (no action taken)",
            partyId);
    }

    private static void HandlePartyErased(string partyId, ILogger logger)
    {
        _ = CustomerSummaryStore.Customers.TryRemove(partyId, out _);
        logger.LogInformation(
            "EVENT: PartyErased -> CustomerSummary removed for {PartyId}; extend this cleanup to every subscriber-owned local read model",
            partyId);
    }

    private static string NormalizeEventTypeName(string eventTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);

        int separator = eventTypeName.LastIndexOf('.');
        return separator >= 0 ? eventTypeName[(separator + 1)..] : eventTypeName;
    }

    private static void UpsertContactChannel(
        CustomerSummary summary,
        string contactChannelId,
        string type,
        string value,
        bool isPreferred)
    {
        if (isPreferred)
        {
            foreach (KeyValuePair<string, CustomerContactChannel> channel in summary.ContactChannels)
            {
                if (string.Equals(channel.Value.Type, type, StringComparison.Ordinal))
                {
                    summary.ContactChannels[channel.Key] = channel.Value with { IsPreferred = false };
                }
            }
        }

        bool preferred = isPreferred || !HasPreferredChannel(summary, type);
        summary.ContactChannels[contactChannelId] = new CustomerContactChannel(type, value, preferred);
        RefreshPreferredContactFields(summary);
    }

    private static bool HasPreferredChannel(CustomerSummary summary, string type)
    {
        foreach (CustomerContactChannel channel in summary.ContactChannels.Values)
        {
            if (channel.IsPreferred && string.Equals(channel.Type, type, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshPreferredContactFields(CustomerSummary summary)
    {
        summary.Email = GetPreferredContactValue(summary, "Email");
        summary.Phone = GetPreferredContactValue(summary, "Phone");
    }

    private static string? GetPreferredContactValue(CustomerSummary summary, string type)
    {
        CustomerContactChannel? fallback = null;
        foreach (CustomerContactChannel channel in summary.ContactChannels.Values)
        {
            if (!string.Equals(channel.Type, type, StringComparison.Ordinal))
            {
                continue;
            }

            if (channel.IsPreferred)
            {
                return channel.Value;
            }

            fallback ??= channel;
        }

        return fallback?.Value;
    }
}

/// <summary>
/// Local record matching the flat EventStore Server envelope JSON shape.
/// This avoids coupling to EventStore internals -- external consumers
/// should define their own envelope type matching the wire format.
/// </summary>
public sealed record EventEnvelope
{
    public required string AggregateId { get; init; }

    public string? TenantId { get; init; }

    public string? Domain { get; init; }

    public long SequenceNumber { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public string? UserId { get; init; }

    public string? DomainServiceVersion { get; init; }

    public required string EventTypeName { get; init; }

    public string? SerializationFormat { get; init; }

    public required string Payload { get; init; }

    public Dictionary<string, object>? Extensions { get; init; }
}

/// <summary>
/// Local deserialization types for event payloads.
/// These mirror the contract shapes without referencing Contracts directly for events.
/// </summary>
public sealed record PartyCreatedPayload
{
    public required string Type { get; init; }

    public PersonDetailsPayload? PersonDetails { get; init; }

    public OrganizationDetailsPayload? OrganizationDetails { get; init; }
}

public sealed record PersonDetailsPayload
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public string? DateOfBirth { get; init; }

    public string? Prefix { get; init; }

    public string? Suffix { get; init; }
}

public sealed record OrganizationDetailsPayload
{
    public required string LegalName { get; init; }

    public string? TradingName { get; init; }
}

public sealed record ContactChannelAddedPayload
{
    public required string ContactChannelId { get; init; }

    public required string Type { get; init; }

    public required string Value { get; init; }

    public bool IsPreferred { get; init; }
}

public sealed record ContactChannelUpdatedPayload
{
    public required string ContactChannelId { get; init; }

    public string? Type { get; init; }

    public string? Value { get; init; }

    public bool? IsPreferred { get; init; }
}

public sealed record ContactChannelRemovedPayload
{
    public required string ContactChannelId { get; init; }
}

public sealed record PreferredContactChannelChangedPayload
{
    public required string ContactChannelId { get; init; }
}

public sealed record PersonDetailsUpdatedPayload
{
    public required PersonDetailsPayload PersonDetails { get; init; }
}

public sealed record OrganizationDetailsUpdatedPayload
{
    public required OrganizationDetailsPayload OrganizationDetails { get; init; }
}

public sealed record IdentifierAddedPayload
{
    public required string IdentifierId { get; init; }

    public required string Type { get; init; }

    public required string Value { get; init; }
}

public sealed record IdentifierRemovedPayload
{
    public required string IdentifierId { get; init; }
}

public sealed record PartyDisplayNameDerivedPayload
{
    public required string DisplayName { get; init; }

    public required string SortName { get; init; }
}

public sealed record IsNaturalPersonChangedPayload
{
    public required bool IsNaturalPerson { get; init; }
}

public sealed record PartyMergedPayload
{
    public required string SurvivorPartyId { get; init; }

    public required string MergedPartyId { get; init; }
}
