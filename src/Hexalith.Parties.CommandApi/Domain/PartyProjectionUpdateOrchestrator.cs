using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Security;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.Parties.CommandApi.Domain;

internal sealed class PartyProjectionUpdateOrchestrator(
    IActorProxyFactory actorProxyFactory,
    IEventPayloadProtectionService payloadProtectionService,
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

        IAggregateActor aggregate = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            nameof(AggregateActor));

        ServerEventEnvelope[] events = await aggregate
            .GetEventsAsync(0)
            .ConfigureAwait(false);

        if (events.Length == 0)
        {
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // After a party's encryption key has been destroyed, encrypted historical events
                // can no longer be decrypted. Fall back to a redacted payload so projection
                // delivery does not stall on the post-erasure tail (PartyEncryptionKeyDeleted,
                // ErasureVerified, PartyErased) that contains no personal data.
                logger.LogWarning(
                    ex,
                    "Falling back to redacted payload during projection delivery for {TenantId}/{AggregateId} event {EventTypeName}.",
                    identity.TenantId,
                    identity.AggregateId,
                    envelope.EventTypeName);
                protectionResult = PartyPayloadProtectionService.RedactProtectedPayload(envelope.Payload, envelope.SerializationFormat);
            }

            await detailProjection
                .HandleSerializedEventAsync(
                    identity.AggregateId,
                    envelope.EventTypeName,
                    protectionResult.PayloadBytes,
                    protectionResult.SerializationFormat)
                .ConfigureAwait(false);
            await indexProjection
                .HandleSerializedEventAsync(
                    identity.AggregateId,
                    envelope.EventTypeName,
                    protectionResult.PayloadBytes,
                    protectionResult.SerializationFormat)
                .ConfigureAwait(false);

            logger.LogDebug(
                "Delivered party event {EventTypeName} to projections for {TenantId}/{AggregateId}.",
                envelope.EventTypeName,
                identity.TenantId,
                identity.AggregateId);
        }
    }
}
