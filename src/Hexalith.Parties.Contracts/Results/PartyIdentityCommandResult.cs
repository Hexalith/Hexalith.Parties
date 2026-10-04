using System.Text.Json;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.Parties.Contracts.Results;

/// <summary>Safe identity result that preserves the original payload on exact retry.</summary>
public sealed record PartyIdentityCommandResult : DomainResult
{
    private readonly string _payload;

    /// <summary>Creates a safe identity result independently of PartyDetail.</summary>
    public PartyIdentityCommandResult(IReadOnlyList<IEventPayload> events, object result) : base(events)
    {
        _payload = JsonSerializer.Serialize(result, PartiesJsonOptions.Default);
    }

    /// <inheritdoc/>
    public override string? ResultPayload => _payload;
}
