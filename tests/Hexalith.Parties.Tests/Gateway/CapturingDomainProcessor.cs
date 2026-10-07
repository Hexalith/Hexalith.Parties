using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Test processor used only by authenticated Parties endpoint fixtures.</summary>
internal sealed class CapturingDomainProcessor : IDomainProcessor, IAsyncDomainProcessor
{
    private readonly List<CommandEnvelope> _receivedCommands = [];

    /// <summary>Gets all commands admitted by the real HTTP security boundary.</summary>
    public IReadOnlyList<CommandEnvelope> ReceivedCommands => _receivedCommands;

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
        _receivedCommands.Add(command);
        return Task.FromResult(DomainResult.Success(
            [new PartyCreated { Type = PartyType.Person }]));
    }
}
