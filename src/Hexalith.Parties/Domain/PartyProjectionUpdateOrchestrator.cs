using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Search;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.Parties.Domain;

internal sealed partial class PartyProjectionUpdateOrchestrator(
    IActorProxyFactory actorProxyFactory,
    IEventPayloadProtectionService payloadProtectionService,
    IPartyProjectionPlatformAdapter projectionPlatformAdapter,
    IServiceProvider serviceProvider,
    ILogger<PartyProjectionUpdateOrchestrator> logger) : IProjectionUpdateOrchestrator, IProjectionPollerDeliveryGateway
{
    private const string PartyDomain = "party";

    // Track per-(tenant, party) "we already warned about missing CaseId" so a misconfigured
    // operator doesn't see a warning storm — one warning per (tenant, party) per process is
    // enough to surface the issue without dashboard pollution.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> s_caseIdMissingWarned =
        new(StringComparer.Ordinal);

    public Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
        => DeliverProjectionAsync(identity, cancellationToken);

    public async Task DeliverProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!string.Equals(identity.Domain, PartyDomain, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IAggregateActor aggregate = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            nameof(AggregateActor));

        ServerEventEnvelope[] events;
        try
        {
            events = await aggregate
                .GetEventsAsync(0)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        if (events.Length == 0)
        {
            logger.LogDebug(
                "Aggregate {TenantId}/{AggregateId} has no events to deliver to projections.",
                identity.TenantId,
                identity.AggregateId);
            return;
        }

        _ = await TryReadDeliveredSequenceAsync(identity, cancellationToken).ConfigureAwait(false);

        IPartyDetailProjectionActor detailProjection = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            new ActorId(PartyActorIds.Detail(identity.TenantId, identity.AggregateId)),
            nameof(PartyDetailProjectionActor));
        IPartyIndexProjectionActor indexProjection = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            new ActorId(PartyActorIds.Index(identity.TenantId)),
            nameof(PartyIndexProjectionActor));

        ServerEventEnvelope? latestEnvelope = null;
        long previousSequence = long.MinValue;

        foreach (ServerEventEnvelope envelope in events.OrderBy(static e => e.SequenceNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Stable sort places same-sequence events in source order; the projection actor's
            // checkpoint will skip the second occurrence, but a duplicate sequence number is a
            // corruption signal that must be visible to operations rather than silently merged.
            if (envelope.SequenceNumber == previousSequence)
            {
                Log.DuplicateSequenceDetected(
                    logger,
                    identity.TenantId,
                    identity.AggregateId,
                    envelope.EventTypeName,
                    envelope.SequenceNumber);
            }

            previousSequence = envelope.SequenceNumber;

            PayloadProtectionResult protectionResult;
            try
            {
                protectionResult = await payloadProtectionService
                    .UnprotectEventPayloadAsync(
                        identity,
                        envelope.EventTypeName,
                        envelope.Payload,
                        envelope.SerializationFormat,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (PartyEncryptionKeyDestroyedException.IsMatch(ex))
            {
                // Narrowed catch (was: any Exception, then message-text matched). Only fall
                // back to a redacted payload when the failure is the typed post-erasure
                // PartyEncryptionKeyDestroyedException (recognized via exact-type match in
                // PartyEncryptionKeyDestroyedException.IsMatch). Transient KMS errors,
                // key-version mismatches, or HSM permission errors propagate so projections
                // don't silently corrupt with null personal-data fields on a recoverable
                // failure.
                logger.LogWarning(
                    ex,
                    "Falling back to redacted payload during projection delivery for {TenantId}/{AggregateId} event {EventTypeName} (sequence {SequenceNumber}). The party's encryption key is not available — proceeding with post-erasure tail only.",
                    identity.TenantId,
                    identity.AggregateId,
                    envelope.EventTypeName,
                    envelope.SequenceNumber);
                protectionResult = PartyPayloadProtectionService.RedactProtectedPayload(envelope.Payload, envelope.SerializationFormat);
            }

            await detailProjection
                .HandleSerializedEventAsync(
                    identity.AggregateId,
                    envelope.EventTypeName,
                    protectionResult.PayloadBytes,
                    protectionResult.SerializationFormat,
                    envelope.SequenceNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            await indexProjection
                .HandleSerializedEventAsync(
                    identity.AggregateId,
                    envelope.EventTypeName,
                    protectionResult.PayloadBytes,
                    protectionResult.SerializationFormat,
                    envelope.SequenceNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            await TrySaveDeliveredSequenceAsync(identity, envelope.SequenceNumber, cancellationToken).ConfigureAwait(false);

            latestEnvelope = envelope;

            logger.LogDebug(
                "Delivered party event {EventTypeName} (sequence {SequenceNumber}) to projections for {TenantId}/{AggregateId}.",
                envelope.EventTypeName,
                envelope.SequenceNumber,
                identity.TenantId,
                identity.AggregateId);
        }

        // After delivering all events, push the latest index entry into Memories search if
        // configured. Indexing is best-effort: failures are logged inside the indexing service
        // and do not abort the projection-delivery contract. Pass the latest envelope so the
        // EventType and Timestamp metadata reflect the actual triggering event (AC1).
        if (latestEnvelope is not null)
        {
            await TryIndexLatestEntryAsync(identity, indexProjection, latestEnvelope, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<long> TryReadDeliveredSequenceAsync(AggregateIdentity identity, CancellationToken cancellationToken)
    {
        try
        {
            return await projectionPlatformAdapter
                .ReadDeliveredSequenceAsync(identity.TenantId, identity.AggregateId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.PlatformCheckpointReadUnavailable(logger, ex);
            return 0;
        }
    }

    private async Task TrySaveDeliveredSequenceAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            bool saved = await projectionPlatformAdapter
                .TrySaveDeliveredSequenceAsync(identity.TenantId, identity.AggregateId, sequenceNumber, cancellationToken)
                .ConfigureAwait(false);
            if (!saved)
            {
                Log.PlatformCheckpointSaveSkipped(logger);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.PlatformCheckpointSaveUnavailable(logger, ex);
        }
    }

    private async Task TryIndexLatestEntryAsync(
        AggregateIdentity identity,
        IPartyIndexProjectionActor indexProjection,
        ServerEventEnvelope latestEnvelope,
        CancellationToken cancellationToken)
    {
        PartyMemoryIndexingService? indexer = serviceProvider.GetService(typeof(PartyMemoryIndexingService)) as PartyMemoryIndexingService;
        if (indexer is null)
        {
            return;
        }

        IOptionsMonitor<PartyMemorySearchOptions>? optionsMonitor = serviceProvider.GetService(typeof(IOptionsMonitor<PartyMemorySearchOptions>)) as IOptionsMonitor<PartyMemorySearchOptions>;
        PartyMemorySearchOptions? memorySearchOptions = optionsMonitor?.CurrentValue;
        if (memorySearchOptions is null || !memorySearchOptions.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(memorySearchOptions.CaseId))
        {
            // Operator enabled Memories search but did not configure a CaseId. Without a case
            // scope, indexing cannot run — emit a one-shot warning per (tenant, party) so the
            // misconfiguration surfaces without flooding logs on every command.
            string warningKey = $"{identity.TenantId}:{identity.AggregateId}";
            if (s_caseIdMissingWarned.TryAdd(warningKey, 0))
            {
                Log.IndexingSkippedMissingCaseId(logger, identity.TenantId, identity.AggregateId);
            }

            return;
        }

        try
        {
            IReadOnlyDictionary<string, PartyIndexEntry> entries = await indexProjection.GetEntriesAsync().ConfigureAwait(false);
            if (!entries.TryGetValue(identity.AggregateId, out PartyIndexEntry? entry) || entry is null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _ = await indexer
                .IndexAsync(
                    entry,
                    new PartyMemoryUnitMappingContext(
                        TenantId: identity.TenantId,
                        CaseId: memorySearchOptions.CaseId!,
                        // AC1: thread the actual triggering event type and timestamp through
                        // to Memories metadata. The previous "PartyProjectionChanged" /
                        // DateTimeOffset.UtcNow shape lost event-type granularity and re-stamped
                        // every replay with the rehydration moment. EventType is required by
                        // the record contract (P15); fall back to a stable marker if the
                        // envelope's event type name is unexpectedly missing.
                        EventType: string.IsNullOrWhiteSpace(latestEnvelope.EventTypeName)
                            ? "PartyProjectionChanged"
                            : latestEnvelope.EventTypeName,
                        AggregateId: identity.AggregateId,
                        SourceService: "Hexalith.Parties",
                        Timestamp: latestEnvelope.Timestamp),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException
                or TaskCanceledException
                or TimeoutException
                or InvalidOperationException
                or PartyEncryptionKeyDestroyedException)
        {
            // Narrowed catch (was: any Exception). Recognized indexing failures degrade to
            // best-effort; OutOfMemoryException, StackOverflowException, and other unanticipated
            // failures must propagate so the host can surface them.
            logger.LogWarning(
                ex,
                "Memories indexing failed for {TenantId}/{AggregateId}; search may be stale until repair runs.",
                identity.TenantId,
                identity.AggregateId);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 8400,
            Level = LogLevel.Error,
            Message = "Duplicate sequence number {SequenceNumber} detected during projection delivery for {TenantId}/{AggregateId} (event {EventTypeName}). Second occurrence will be skipped by checkpoint guard, but the source event stream is corrupt.")]
        public static partial void DuplicateSequenceDetected(ILogger logger, string tenantId, string aggregateId, string eventTypeName, long sequenceNumber);

        [LoggerMessage(
            EventId = 8401,
            Level = LogLevel.Warning,
            Message = "Memories search is enabled but no CaseId is configured (Parties:MemoriesSearch:CaseId). Skipping indexing for {TenantId}/{AggregateId}. Subsequent commands for this party will not log this warning again until the process restarts.")]
        public static partial void IndexingSkippedMissingCaseId(ILogger logger, string tenantId, string aggregateId);

        [LoggerMessage(
            EventId = 8402,
            Level = LogLevel.Warning,
            Message = "Projection platform checkpoint read unavailable; continuing full replay from sequence zero.")]
        public static partial void PlatformCheckpointReadUnavailable(ILogger logger, Exception exception);

        [LoggerMessage(
            EventId = 8403,
            Level = LogLevel.Warning,
            Message = "Projection platform checkpoint save returned no progress; local actor companion checkpoints remain authoritative.")]
        public static partial void PlatformCheckpointSaveSkipped(ILogger logger);

        [LoggerMessage(
            EventId = 8404,
            Level = LogLevel.Warning,
            Message = "Projection platform checkpoint save unavailable; local actor companion checkpoints remain authoritative.")]
        public static partial void PlatformCheckpointSaveUnavailable(ILogger logger, Exception exception);
    }
}
