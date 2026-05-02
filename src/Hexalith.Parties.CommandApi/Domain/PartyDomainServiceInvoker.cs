using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Parties.Server.Aggregates;

namespace Hexalith.Parties.CommandApi.Domain;

internal sealed class PartyDomainServiceInvoker(
    IEventPayloadProtectionService payloadProtectionService) : IDomainServiceInvoker
{
    private const string PartyDomain = "party";

    private readonly PartyAggregate _aggregate = new();

    public async Task<DomainResult> InvokeAsync(
        CommandEnvelope command,
        object? currentState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(command.Domain, PartyDomain, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainServiceNotFoundException(command.TenantId, command.Domain);
        }

        object? unprotectedState = await UnprotectCurrentStateAsync(command, currentState, cancellationToken)
            .ConfigureAwait(false);

        return await _aggregate.ProcessAsync(command, unprotectedState).ConfigureAwait(false);
    }

    private async Task<object?> UnprotectCurrentStateAsync(
        CommandEnvelope command,
        object? currentState,
        CancellationToken cancellationToken)
    {
        if (currentState is not DomainServiceCurrentState state)
        {
            return currentState;
        }

        object? snapshotState = state.SnapshotState is null
            ? null
            : await payloadProtectionService
                .UnprotectSnapshotStateAsync(command.AggregateIdentity, state.SnapshotState, cancellationToken)
                .ConfigureAwait(false);

        var events = new List<EventEnvelope>(state.Events.Count);
        foreach (EventEnvelope envelope in state.Events)
        {
            PayloadProtectionResult result = await payloadProtectionService
                .UnprotectEventPayloadAsync(
                    command.AggregateIdentity,
                    envelope.Metadata.EventTypeName,
                    envelope.Payload,
                    envelope.Metadata.SerializationFormat,
                    cancellationToken)
                .ConfigureAwait(false);

            events.Add(result.PayloadBytes == envelope.Payload
                    && string.Equals(result.SerializationFormat, envelope.Metadata.SerializationFormat, StringComparison.Ordinal)
                ? envelope
                : new EventEnvelope(
                    envelope.Metadata with { SerializationFormat = result.SerializationFormat },
                    result.PayloadBytes,
                    envelope.Extensions));
        }

        return state with { SnapshotState = snapshotState, Events = events };
    }
}
