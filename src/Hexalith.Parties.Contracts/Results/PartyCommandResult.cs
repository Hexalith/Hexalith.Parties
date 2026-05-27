using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Contracts.Results;

/// <summary>
/// Domain result for successful party mutations that can return the resulting party detail.
/// </summary>
public sealed record PartyCommandResult : DomainResult
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _resultPayload;

    public PartyCommandResult(IReadOnlyList<IEventPayload> events, PartyDetail updatedPartyDetail)
        : base(events)
    {
        UpdatedPartyDetail = updatedPartyDetail ?? throw new ArgumentNullException(nameof(updatedPartyDetail));

        // Serialize once at construction rather than on every ResultPayload read. The producer
        // (DomainServiceWireResult.FromDomainResult) and the command handler each read the property,
        // so a getter-time serialize ran at least twice per command. A readonly field keeps record
        // value-equality intact — it is derived deterministically from UpdatedPartyDetail, so equal
        // instances carry an equal payload — and moves any serialization failure out of a property getter.
        _resultPayload = JsonSerializer.Serialize(UpdatedPartyDetail, JsonOptions);
    }

    public PartyDetail UpdatedPartyDetail { get; }

    public override string? ResultPayload => _resultPayload;

    internal static string? SerializePayload(PartyDetail? updatedPartyDetail)
        => updatedPartyDetail is null ? null : JsonSerializer.Serialize(updatedPartyDetail, JsonOptions);
}
