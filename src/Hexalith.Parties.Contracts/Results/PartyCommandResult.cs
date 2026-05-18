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

    public PartyCommandResult(IReadOnlyList<IEventPayload> events, PartyDetail updatedPartyDetail)
        : base(events)
    {
        UpdatedPartyDetail = updatedPartyDetail ?? throw new ArgumentNullException(nameof(updatedPartyDetail));
    }

    public PartyDetail UpdatedPartyDetail { get; }

    public override string? ResultPayload => JsonSerializer.Serialize(UpdatedPartyDetail, JsonOptions);

    internal static string? SerializePayload(PartyDetail? updatedPartyDetail)
        => updatedPartyDetail is null ? null : JsonSerializer.Serialize(updatedPartyDetail, JsonOptions);
}
