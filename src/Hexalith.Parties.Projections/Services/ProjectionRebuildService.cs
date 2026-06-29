using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Handlers;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Services;

public sealed partial class ProjectionRebuildService : IProjectionRebuildService {
    private const string Domain = "party";
    private const string AggregateActorType = "AggregateActor";
    private const string DetailActorType = "PartyDetailProjectionActor";
    private const string IndexActorType = "PartyIndexProjectionActor";
    private const string DetailProjectionName = "party-detail";
    private const string IndexProjectionName = "party-index";
    private const string IndexManifestStateKeySuffix = "manifest";
    private const string RebuildCheckpointPrefix = "rebuild-checkpoint";

    private static readonly JsonSerializerOptions s_projectionRebuildReaderJsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _daprHttpClient;
    private readonly IEventPayloadProtectionService _payloadProtectionService;
    private readonly ILogger<ProjectionRebuildService> _logger;

    public ProjectionRebuildService(
        HttpClient daprHttpClient,
        IEventPayloadProtectionService payloadProtectionService,
        ILogger<ProjectionRebuildService> logger) {
        _daprHttpClient = daprHttpClient;
        _payloadProtectionService = payloadProtectionService;
        _logger = logger;
    }

    public async Task RebuildDetailProjectionAsync(string tenantId, string? partyId, CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        string projectionKey = partyId is not null ? $"detail:{partyId}" : "detail";
        ProjectionRebuildCheckpoint? checkpoint = await ReadCheckpointAsync(
            tenantId,
            projectionKey,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> partyIds = partyId is not null
            ? [partyId]
            : await GetPartyIdsForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        EnsureTrustedPartyIdsAvailable(partyIds);

        Log.RebuildStarted(_logger, "detail", tenantId, partyIds.Count);

        bool resumeReached = checkpoint is null;
        foreach (string id in partyIds.OrderBy(static x => x, StringComparer.Ordinal)) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!resumeReached) {
                if (!string.Equals(id, checkpoint!.PartyId, StringComparison.Ordinal)) {
                    continue;
                }

                resumeReached = true;
            }

            long resumeSequence = checkpoint is not null && string.Equals(id, checkpoint.PartyId, StringComparison.Ordinal)
                ? checkpoint.SequenceNumber
                : 0;

            await RebuildSingleDetailAsync(tenantId, id, projectionKey, resumeSequence, cancellationToken).ConfigureAwait(false);
            checkpoint = null;
        }

        await DeleteCheckpointAsync(tenantId, projectionKey, cancellationToken).ConfigureAwait(false);

        Log.RebuildCompleted(_logger, "detail", tenantId, partyIds.Count);
    }

    public async Task RebuildIndexProjectionAsync(string tenantId, CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        ProjectionRebuildCheckpoint? checkpoint = await ReadCheckpointAsync(
            tenantId,
            "index",
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> partyIds = await GetPartyIdsForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        EnsureTrustedPartyIdsAvailable(partyIds);

        Log.RebuildStarted(_logger, "index", tenantId, partyIds.Count);

        string indexActorId = GetIndexActorId(tenantId);
        string indexStateKey = GetIndexStateKey(tenantId);
        Dictionary<string, PartyIndexEntry> rebuiltIndex = checkpoint is not null
            ? await ReadActorStateAsync<Dictionary<string, PartyIndexEntry>>(
                IndexActorType,
                indexActorId,
                indexStateKey,
                cancellationToken).ConfigureAwait(false) ?? []
            : [];

        bool resumeReached = checkpoint is null;
        foreach (string id in partyIds.OrderBy(static x => x, StringComparer.Ordinal)) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!resumeReached) {
                if (!string.Equals(id, checkpoint!.PartyId, StringComparison.Ordinal)) {
                    continue;
                }

                resumeReached = true;
            }

            long resumeSequence = checkpoint is not null && string.Equals(id, checkpoint.PartyId, StringComparison.Ordinal)
                ? checkpoint.SequenceNumber
                : 0;
            PartyIndexEntry? entry = rebuiltIndex.GetValueOrDefault(id);
            IReadOnlyList<EventReplayRecord> events = await ReadAggregateEventRecordsAsync(
                tenantId,
                id,
                resumeSequence + 1,
                cancellationToken).ConfigureAwait(false);

            foreach (EventReplayRecord evt in events) {
                PartyIndexEntry? applied = PartyIndexProjectionHandler.Apply(id, evt.Payload, entry);
                if (applied is not null) {
                    entry = applied;
                    rebuiltIndex[id] = applied;
                }
                else if (entry is null) {
                    _ = rebuiltIndex.Remove(id);
                }

                await WriteIndexProjectionStateAsync(tenantId, rebuiltIndex, cancellationToken).ConfigureAwait(false);
                await WriteCheckpointAsync(
                    tenantId,
                    "index",
                    new ProjectionRebuildCheckpoint(id, evt.SequenceNumber),
                    cancellationToken).ConfigureAwait(false);
            }

            Log.PartyRebuilt(_logger, tenantId, id, events.Count);
            checkpoint = null;
        }

        await DeleteCheckpointAsync(tenantId, "index", cancellationToken).ConfigureAwait(false);

        Log.RebuildCompleted(_logger, "index", tenantId, partyIds.Count);
    }

    public async Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string actorId = $"{tenantId}:{Domain}:{partyId}";
        string metadataKey = $"{tenantId}:{Domain}:{partyId}:metadata";

        AggregateMetadataDto? metadata = await ReadActorStateAsync<AggregateMetadataDto>(
            AggregateActorType,
            actorId,
            metadataKey,
            cancellationToken).ConfigureAwait(false);

        if (metadata is null || metadata.CurrentSequence <= 0) {
            return [];
        }

        List<ProcessingActivityRecord> records = new(checked((int)metadata.CurrentSequence));
        for (long seq = 1; seq <= metadata.CurrentSequence; seq++) {
            string eventKey = $"{tenantId}:{Domain}:{partyId}:events:{seq}";
            EventEnvelopeDto? envelope = await ReadActorStateAsync<EventEnvelopeDto>(
                AggregateActorType,
                actorId,
                eventKey,
                cancellationToken).ConfigureAwait(false);

            if (envelope is null) {
                Log.EventMissing(_logger, tenantId, partyId, seq);
                continue;
            }

            IEventPayload? payload = DeserializeEventPayload(envelope);
            records.Add(new ProcessingActivityRecord {
                SequenceNumber = envelope.SequenceNumber,
                PartyId = envelope.AggregateId,
                TenantId = envelope.TenantId,
                ActorId = NormalizeMetadata(envelope.UserId, "system"),
                CorrelationId = NormalizeMetadata(envelope.CorrelationId, "unspecified"),
                OperationCategory = GetOperationCategory(envelope.EventTypeName, payload),
                Outcome = "Succeeded",
                EventType = GetShortEventTypeName(envelope.EventTypeName),
                Timestamp = envelope.Timestamp,
                Summary = CreateProcessingSummary(envelope.EventTypeName, payload),
            });
        }

        return records;
    }

    internal async Task<IReadOnlyList<IEventPayload>> ReadAggregateEventsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
        => (await ReadAggregateEventRecordsAsync(tenantId, partyId, 1, cancellationToken).ConfigureAwait(false))
            .Select(static x => x.Payload)
            .ToList();

    internal async Task<IReadOnlyList<EventReplayRecord>> ReadAggregateEventRecordsAsync(
        string tenantId,
        string partyId,
        long startSequence,
        CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfLessThan(startSequence, 1L);

        string actorId = $"{tenantId}:{Domain}:{partyId}";
        string metadataKey = $"{tenantId}:{Domain}:{partyId}:metadata";

        AggregateMetadataDto? metadata = await ReadActorStateAsync<AggregateMetadataDto>(
            AggregateActorType, actorId, metadataKey, cancellationToken).ConfigureAwait(false);

        if (metadata is null || metadata.CurrentSequence <= 0 || startSequence > metadata.CurrentSequence) {
            return [];
        }

        List<EventReplayRecord> events = new(checked((int)(metadata.CurrentSequence - startSequence + 1)));
        for (long seq = startSequence; seq <= metadata.CurrentSequence; seq++) {
            string eventKey = $"{tenantId}:{Domain}:{partyId}:events:{seq}";
            EventEnvelopeDto? envelope = await ReadActorStateAsync<EventEnvelopeDto>(
                AggregateActorType, actorId, eventKey, cancellationToken).ConfigureAwait(false);

            if (envelope is null) {
                Log.EventMissing(_logger, tenantId, partyId, seq);
                continue;
            }

            IEventPayload? payload = DeserializeEventPayload(envelope);
            if (payload is not null) {
                events.Add(new EventReplayRecord(seq, payload));
            }
        }

        return events;
    }

    private async Task RebuildSingleDetailAsync(
        string tenantId,
        string partyId,
        string projectionKey,
        long resumeSequence,
        CancellationToken cancellationToken) {
        string detailActorId = GetDetailActorId(tenantId, partyId);
        string detailStateKey = GetDetailStateKey(tenantId, partyId);
        PartyDetail? detail = resumeSequence > 0
            ? await ReadActorStateAsync<PartyDetail>(
                DetailActorType,
                detailActorId,
                detailStateKey,
                cancellationToken).ConfigureAwait(false)
            : null;
        IReadOnlyList<EventReplayRecord> events = await ReadAggregateEventRecordsAsync(
            tenantId,
            partyId,
            resumeSequence + 1,
            cancellationToken).ConfigureAwait(false);

        foreach (EventReplayRecord evt in events) {
            PartyDetail? applied = PartyDetailProjectionHandler.Apply(partyId, evt.Payload, detail);
            if (applied is not null) {
                detail = applied;
                await WriteActorStateAsync(DetailActorType, detailActorId, detailStateKey, detail, cancellationToken).ConfigureAwait(false);
            }

            await WriteCheckpointAsync(
                tenantId,
                projectionKey,
                new ProjectionRebuildCheckpoint(partyId, evt.SequenceNumber),
                cancellationToken).ConfigureAwait(false);
        }

        if (events.Count == 0 && detail is not null) {
            await WriteActorStateAsync(DetailActorType, detailActorId, detailStateKey, detail, cancellationToken).ConfigureAwait(false);
        }

        Log.PartyRebuilt(_logger, tenantId, partyId, events.Count);
    }

    private async Task<IReadOnlyList<string>> GetPartyIdsForTenantAsync(string tenantId, CancellationToken cancellationToken) {
        string indexActorId = GetIndexActorId(tenantId);
        string indexStateKey = GetIndexStateKey(tenantId);

        Dictionary<string, PartyIndexEntry>? index = await ReadActorStateAsync<Dictionary<string, PartyIndexEntry>>(
            IndexActorType, indexActorId, indexStateKey, cancellationToken).ConfigureAwait(false);

        if (index is not null && index.Count > 0) {
            return index.Keys.OrderBy(static x => x, StringComparer.Ordinal).ToList();
        }

        List<string>? manifest = await ReadActorStateAsync<List<string>>(
            IndexActorType,
            indexActorId,
            GetIndexManifestStateKey(tenantId),
            cancellationToken).ConfigureAwait(false);

        if (manifest is not null && manifest.Count > 0) {
            return manifest
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static x => x, StringComparer.Ordinal)
                .ToList();
        }

        Log.IndexUnavailableForRebuild(_logger, tenantId);
        return [];
    }

    private async Task<T?> ReadActorStateAsync<T>(
        string actorType, string actorId, string stateKey, CancellationToken cancellationToken)
        where T : class {
        string encodedActorId = Uri.EscapeDataString(actorId);
        string encodedKey = Uri.EscapeDataString(stateKey);
        string url = $"/v1.0/actors/{actorType}/{encodedActorId}/state/{encodedKey}";

        HttpResponseMessage response = await _daprHttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(s_projectionRebuildReaderJsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteActorStateAsync<T>(
        string actorType, string actorId, string stateKey, T value, CancellationToken cancellationToken) {
        string encodedActorId = Uri.EscapeDataString(actorId);
        string url = $"/v1.0/actors/{actorType}/{encodedActorId}/state";

        var stateTransaction = new[]
        {
            new
            {
                operation = "upsert",
                request = new { key = stateKey, value },
            },
        };

        HttpResponseMessage response = await _daprHttpClient.PutAsJsonAsync(
            url, stateTransaction, PartiesJsonOptions.Default, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task WriteIndexProjectionStateAsync(
        string tenantId,
        IReadOnlyDictionary<string, PartyIndexEntry> rebuiltIndex,
        CancellationToken cancellationToken) {
        string actorId = GetIndexActorId(tenantId);
        string url = $"/v1.0/actors/{IndexActorType}/{Uri.EscapeDataString(actorId)}/state";
        string indexStateKey = GetIndexStateKey(tenantId);
        string manifestStateKey = GetIndexManifestStateKey(tenantId);
        string[] manifest = rebuiltIndex.Keys.OrderBy(static x => x, StringComparer.Ordinal).ToArray();

        var stateTransaction = new object[]
        {
            new
            {
                operation = "upsert",
                request = new { key = indexStateKey, value = rebuiltIndex },
            },
            new
            {
                operation = "upsert",
                request = new { key = manifestStateKey, value = manifest },
            },
        };

        HttpResponseMessage response = await _daprHttpClient.PutAsJsonAsync(
            url,
            stateTransaction,
            PartiesJsonOptions.Default,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ProjectionRebuildCheckpoint?> ReadCheckpointAsync(
        string tenantId,
        string projectionKey,
        CancellationToken cancellationToken)
        => await ReadActorStateAsync<ProjectionRebuildCheckpoint>(
            IndexActorType,
            GetIndexActorId(tenantId),
            GetCheckpointStateKey(tenantId, projectionKey),
            cancellationToken).ConfigureAwait(false);

    private async Task WriteCheckpointAsync(
        string tenantId,
        string projectionKey,
        ProjectionRebuildCheckpoint checkpoint,
        CancellationToken cancellationToken)
        => await WriteActorStateAsync(
            IndexActorType,
            GetIndexActorId(tenantId),
            GetCheckpointStateKey(tenantId, projectionKey),
            checkpoint,
            cancellationToken).ConfigureAwait(false);

    private async Task DeleteCheckpointAsync(
        string tenantId,
        string projectionKey,
        CancellationToken cancellationToken) {
        string actorId = GetIndexActorId(tenantId);
        string url = $"/v1.0/actors/{IndexActorType}/{Uri.EscapeDataString(actorId)}/state";
        var stateTransaction = new[]
        {
            new
            {
                operation = "delete",
                request = new { key = GetCheckpointStateKey(tenantId, projectionKey) },
            },
        };

        HttpResponseMessage response = await _daprHttpClient.PutAsJsonAsync(
            url,
            stateTransaction,
            PartiesJsonOptions.Default,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static string GetCheckpointStateKey(string tenantId, string projectionKey)
        => $"{tenantId}:{RebuildCheckpointPrefix}:{projectionKey}";

    private static void EnsureTrustedPartyIdsAvailable(IReadOnlyCollection<string> partyIds)
    {
        if (partyIds.Count == 0)
        {
            throw new InvalidOperationException("Projection rebuild cannot enumerate trusted party ids.");
        }
    }

    private static string GetDetailActorId(string tenantId, string partyId)
        => $"{tenantId}:{DetailProjectionName}:{partyId}";

    private static string GetDetailStateKey(string tenantId, string partyId)
        => $"{tenantId}:{DetailProjectionName}:{partyId}";

    private static string GetIndexActorId(string tenantId)
        => $"{tenantId}:{IndexProjectionName}";

    private static string GetIndexStateKey(string tenantId)
        => $"{tenantId}:{IndexProjectionName}:all";

    private static string GetIndexManifestStateKey(string tenantId)
        => $"{tenantId}:{IndexProjectionName}:{IndexManifestStateKeySuffix}";

    private static string GetShortEventTypeName(string? eventTypeName) {
        if (string.IsNullOrWhiteSpace(eventTypeName)) {
            return "UnknownEvent";
        }

        int lastDot = eventTypeName.LastIndexOf('.');
        return lastDot >= 0 ? eventTypeName[(lastDot + 1)..] : eventTypeName;
    }

    private static string CreateProcessingSummary(string eventTypeName, IEventPayload? payload)
        => payload switch {
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
        => payload switch {
            ConsentRecorded or ConsentRevoked => "Consent",
            ProcessingRestricted or RestrictionLifted => "Restriction",
            ErasePartyRequested or PartyEncryptionKeyDeleted or ErasureVerified or PartyErased => "Erasure",
            PartyCreated or PartyDisplayNameDerived or PersonDetailsUpdated or OrganizationDetailsUpdated
                or ContactChannelAdded or ContactChannelUpdated or ContactChannelRemoved
                or PreferredContactChannelChanged or IdentifierAdded or IdentifierRemoved
                or PartyDeactivated or PartyReactivated => "PartyCommand",
            _ => GetShortEventTypeName(eventTypeName),
        };

    private static string NormalizeMetadata(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private IEventPayload? DeserializeEventPayload(EventEnvelopeDto envelope) {
        if (envelope.Payload is null || envelope.Payload.Length == 0) {
            return null;
        }

        Type? eventType = ResolveEventType(envelope.EventTypeName);
        if (eventType is null) {
            Log.UnknownEventType(_logger, envelope.EventTypeName, envelope.AggregateId);
            return null;
        }

        try {
            AggregateIdentity identity = new(envelope.TenantId, envelope.Domain, envelope.AggregateId);
            PayloadProtectionResult protectionResult = _payloadProtectionService
                .UnprotectEventPayloadAsync(
                    identity,
                    envelope.EventTypeName,
                    envelope.Payload,
                    envelope.SerializationFormat)
                .GetAwaiter()
                .GetResult();

            object? deserialized = JsonSerializer.Deserialize(protectionResult.PayloadBytes, eventType, s_projectionRebuildReaderJsonOptions);
            return deserialized as IEventPayload;
        }
        catch (InvalidOperationException ex) when (
            string.Equals(envelope.SerializationFormat, "json+pdenc-v1", StringComparison.Ordinal)) {
            // Decryption failure for an encrypted event — likely an erased party whose key was destroyed.
            // Gracefully skip instead of crashing the entire rebuild.
            Log.DecryptionFailedDuringRebuild(_logger, envelope.EventTypeName, envelope.AggregateId, envelope.SequenceNumber, ex);
            return null;
        }
        catch (JsonException ex) {
            Log.EventDeserializationFailed(_logger, envelope.EventTypeName, envelope.AggregateId, envelope.SequenceNumber, ex);
            return null;
        }
    }

    private static Type? ResolveEventType(string? eventTypeName) {
        if (string.IsNullOrWhiteSpace(eventTypeName)) {
            return null;
        }

        // Try direct type resolution (assembly-qualified name)
        Type? type = Type.GetType(eventTypeName);
        if (type is not null) {
            return type;
        }

        // Search in the Parties.Contracts assembly where all domain events live
        type = typeof(PartyCreated).Assembly.GetType(eventTypeName);
        if (type is not null) {
            return type;
        }

        // Try short name match (just the class name without namespace)
        string shortName = eventTypeName.Contains('.', StringComparison.Ordinal)
            ? eventTypeName[(eventTypeName.LastIndexOf('.') + 1)..]
            : eventTypeName;

        return typeof(PartyCreated).Assembly
            .GetTypes()
            .FirstOrDefault(t => string.Equals(t.Name, shortName, StringComparison.Ordinal)
                && typeof(IEventPayload).IsAssignableFrom(t));
    }

    internal sealed record AggregateMetadataDto {
        [JsonPropertyName("currentSequence")]
        public long CurrentSequence { get; init; }

        [JsonPropertyName("lastModified")]
        public DateTimeOffset LastModified { get; init; }
    }

    internal sealed record EventEnvelopeDto {
        [JsonPropertyName("aggregateId")]
        public string AggregateId { get; init; } = string.Empty;

        [JsonPropertyName("tenantId")]
        public string TenantId { get; init; } = string.Empty;

        [JsonPropertyName("domain")]
        public string Domain { get; init; } = string.Empty;

        [JsonPropertyName("sequenceNumber")]
        public long SequenceNumber { get; init; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; init; }

        [JsonPropertyName("eventTypeName")]
        public string EventTypeName { get; init; } = string.Empty;

        [JsonPropertyName("serializationFormat")]
        public string SerializationFormat { get; init; } = string.Empty;

        [JsonPropertyName("payload")]
        public byte[] Payload { get; init; } = [];

        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; init; } = string.Empty;

        [JsonPropertyName("causationId")]
        public string CausationId { get; init; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; init; } = string.Empty;
    }

    internal sealed record EventReplayRecord(long SequenceNumber, IEventPayload Payload);

    internal sealed record ProjectionRebuildCheckpoint(string PartyId, long SequenceNumber);

    private static partial class Log {
        [LoggerMessage(
            EventId = 8320,
            Level = LogLevel.Information,
            Message = "Starting {Projection} projection rebuild for tenant {TenantId} ({PartyCount} parties).")]
        public static partial void RebuildStarted(ILogger logger, string projection, string tenantId, int partyCount);

        [LoggerMessage(
            EventId = 8321,
            Level = LogLevel.Information,
            Message = "{Projection} projection rebuild completed for tenant {TenantId} ({PartyCount} parties replayed).")]
        public static partial void RebuildCompleted(ILogger logger, string projection, string tenantId, int partyCount);

        [LoggerMessage(
            EventId = 8322,
            Level = LogLevel.Information,
            Message = "Party {PartyId} in tenant {TenantId} rebuilt from {EventCount} events.")]
        public static partial void PartyRebuilt(ILogger logger, string tenantId, string partyId, int eventCount);

        [LoggerMessage(
            EventId = 8323,
            Level = LogLevel.Warning,
            Message = "Event at sequence {SequenceNumber} missing for aggregate {AggregateId} in tenant {TenantId}.")]
        public static partial void EventMissing(ILogger logger, string tenantId, string aggregateId, long sequenceNumber);

        [LoggerMessage(
            EventId = 8324,
            Level = LogLevel.Warning,
            Message = "Unknown event type {EventTypeName} in aggregate {AggregateId}. Skipping.")]
        public static partial void UnknownEventType(ILogger logger, string eventTypeName, string aggregateId);

        [LoggerMessage(
            EventId = 8325,
            Level = LogLevel.Error,
            Message = "Failed to deserialize event {EventTypeName} in aggregate {AggregateId} at sequence {SequenceNumber}.")]
        public static partial void EventDeserializationFailed(ILogger logger, string eventTypeName, string aggregateId, long sequenceNumber, Exception exception);

        [LoggerMessage(
            EventId = 8326,
            Level = LogLevel.Warning,
            Message = "Index projection unavailable for tenant {TenantId}. Cannot enumerate party IDs for rebuild.")]
        public static partial void IndexUnavailableForRebuild(ILogger logger, string tenantId);

        [LoggerMessage(
            EventId = 8327,
            Level = LogLevel.Information,
            Message = "Decryption failed during rebuild for event {EventTypeName} in aggregate {AggregateId} at sequence {SequenceNumber}. Skipping (expected for erased parties).")]
        public static partial void DecryptionFailedDuringRebuild(ILogger logger, string eventTypeName, string aggregateId, long sequenceNumber, Exception exception);
    }
}
