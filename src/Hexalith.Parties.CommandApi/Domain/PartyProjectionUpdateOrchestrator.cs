using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Options;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.Parties.CommandApi.Domain;

internal sealed class PartyProjectionUpdateOrchestrator(
    IActorProxyFactory actorProxyFactory,
    IEventPayloadProtectionService payloadProtectionService,
    IServiceProvider serviceProvider,
    ILogger<PartyProjectionUpdateOrchestrator> logger) : IProjectionUpdateOrchestrator, IProjectionPollerDeliveryGateway
{
    private const string PartyDomain = "party";

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

        IPartyDetailProjectionActor detailProjection = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            new ActorId($"{identity.TenantId}:party-detail:{identity.AggregateId}"),
            nameof(PartyDetailProjectionActor));
        IPartyIndexProjectionActor indexProjection = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            new ActorId($"{identity.TenantId}:party-index"),
            nameof(PartyIndexProjectionActor));

        foreach (ServerEventEnvelope envelope in events.OrderBy(static e => e.SequenceNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

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
            catch (Exception ex) when (IsKeyDestroyedFailure(ex))
            {
                // Narrowed catch (was: any Exception). Only fall back to a redacted payload
                // when the failure is specifically the post-erasure "no encryption key
                // available" case. Transient KMS errors, key-version mismatches, or HSM
                // permission errors propagate so projections don't silently corrupt with
                // null personal-data fields on a recoverable failure.
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
                    envelope.SequenceNumber)
                .ConfigureAwait(false);
            await indexProjection
                .HandleSerializedEventAsync(
                    identity.AggregateId,
                    envelope.EventTypeName,
                    protectionResult.PayloadBytes,
                    protectionResult.SerializationFormat,
                    envelope.SequenceNumber)
                .ConfigureAwait(false);

            logger.LogDebug(
                "Delivered party event {EventTypeName} (sequence {SequenceNumber}) to projections for {TenantId}/{AggregateId}.",
                envelope.EventTypeName,
                envelope.SequenceNumber,
                identity.TenantId,
                identity.AggregateId);
        }

        // After delivering all events, push the latest index entry into Memories search if
        // configured. Indexing is best-effort: failures are logged inside the indexing service
        // and do not abort the projection-delivery contract.
        await TryIndexLatestEntryAsync(identity, indexProjection, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryIndexLatestEntryAsync(
        AggregateIdentity identity,
        IPartyIndexProjectionActor indexProjection,
        CancellationToken cancellationToken)
    {
        PartyMemoryIndexingService? indexer = serviceProvider.GetService(typeof(PartyMemoryIndexingService)) as PartyMemoryIndexingService;
        if (indexer is null)
        {
            return;
        }

        IOptionsMonitor<PartyMemorySearchOptions>? optionsMonitor = serviceProvider.GetService(typeof(IOptionsMonitor<PartyMemorySearchOptions>)) as IOptionsMonitor<PartyMemorySearchOptions>;
        PartyMemorySearchOptions? memorySearchOptions = optionsMonitor?.CurrentValue;
        if (memorySearchOptions is null || !memorySearchOptions.Enabled || string.IsNullOrWhiteSpace(memorySearchOptions.CaseId))
        {
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
                        AggregateId: identity.AggregateId,
                        EventType: "PartyProjectionChanged",
                        SourceService: "Hexalith.Parties",
                        Timestamp: DateTimeOffset.UtcNow),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Memories indexing failed for {TenantId}/{AggregateId}; search may be stale until repair runs.",
                identity.TenantId,
                identity.AggregateId);
        }
    }

    /// <summary>
    /// Recognises the specific failures thrown when a party's encryption key
    /// has been destroyed (post-erasure). Other failures from the
    /// payload protection service (missing nonce / tag / ciphertext / key-version metadata)
    /// indicate transient or structural failures that must propagate, not be redacted away.
    /// </summary>
    private static bool IsKeyDestroyedFailure(Exception ex)
        => (ex is InvalidOperationException or KeyNotFoundException)
            && (ex.Message.Contains("No encryption key", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("key destroyed", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("key has been deleted", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Secret not found", StringComparison.OrdinalIgnoreCase));
}
