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

        switch (envelope.EventTypeName)
        {
            case "PartyCreated":
                HandlePartyCreated(partyId, payloadJson, logger);
                break;

            case "ContactChannelAdded":
                HandleContactChannelAdded(partyId, payloadJson, logger);
                break;

            case "PartyDeactivated":
                HandlePartyDeactivated(partyId, logger);
                break;

            // Forward-compatibility: these events will be available in future versions.
            // PartyMerged (v2): Handle survivor/merged party ID mapping.
            // PartyErased (v1.1 GDPR): See dangling reference guidance in docs
            //   for handling crypto-shredded data and read model cleanup.
            default:
                logger.LogInformation(
                    "Unhandled event type '{EventType}' for aggregate {AggregateId}",
                    envelope.EventTypeName,
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

        if (payload.Type == "Email"
            && CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.Email = payload.Value;
            logger.LogInformation(
                "EVENT: ContactChannelAdded -> Email updated for {PartyId}: {Email}",
                partyId,
                payload.Value);
        }
    }

    private static void HandlePartyDeactivated(string partyId, ILogger logger)
    {
        // Idempotent: setting IsActive to false multiple times is safe
        if (CustomerSummaryStore.Customers.TryGetValue(partyId, out CustomerSummary? summary))
        {
            summary.IsActive = false;
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
