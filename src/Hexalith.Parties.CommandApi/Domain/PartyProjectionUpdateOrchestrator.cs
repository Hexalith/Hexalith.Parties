using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

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

            PayloadProtectionResult protectionResult = await payloadProtectionService
                .UnprotectEventPayloadAsync(
                    identity,
                    envelope.EventTypeName,
                    envelope.Payload,
                    envelope.SerializationFormat,
                    cancellationToken)
                .ConfigureAwait(false);

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
