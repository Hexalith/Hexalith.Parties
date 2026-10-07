using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Test processor used only by authenticated Parties endpoint fixtures.</summary>
internal sealed class PayloadProducingDomainProcessor : IDomainProcessor, IAsyncDomainProcessor
{
    /// <inheritdoc />
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ProcessAsync(command, currentState);
    }

    /// <inheritdoc />
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
    {
        ArgumentNullException.ThrowIfNull(command);
        var detail = new PartyDetail
        {
            Id = command.AggregateId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
        };
        IEventPayload[] events = [new PartyCreated { Type = PartyType.Person }];
        return Task.FromResult<DomainResult>(new PartyCommandResult(events, detail));
    }
}
