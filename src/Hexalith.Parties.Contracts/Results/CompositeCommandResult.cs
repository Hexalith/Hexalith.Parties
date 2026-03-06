using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Contracts.Results;

/// <summary>
/// Extends DomainResult with per-sub-operation outcome tracking for composite commands.
/// Applied/Skipped/Rejected contain human-readable descriptions of each sub-operation outcome.
/// </summary>
public sealed record CompositeCommandResult : DomainResult
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public CompositeCommandResult(
        IReadOnlyList<IEventPayload> events,
        IReadOnlyList<string> applied,
        IReadOnlyList<string> skipped,
        IReadOnlyList<string> rejected,
        PartyDetail? updatedPartyDetail = null)
        : base(events)
    {
        Applied = applied;
        Skipped = skipped;
        Rejected = rejected;
        UpdatedPartyDetail = updatedPartyDetail;
    }

    /// <summary>Gets descriptions of sub-operations that were successfully applied.</summary>
    public IReadOnlyList<string> Applied { get; }

    /// <summary>Gets descriptions of sub-operations that were skipped (e.g., duplicate additions).</summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>Gets descriptions of sub-operations that were rejected (e.g., invalid IDs).</summary>
    public IReadOnlyList<string> Rejected { get; }

    /// <summary>Gets the updated party detail for update-composite responses (null for create-composite).</summary>
    public PartyDetail? UpdatedPartyDetail { get; }

    /// <inheritdoc/>
    public override string? ResultPayload
    {
        get
        {
            if (IsRejection || IsNoOp)
            {
                return null;
            }

            if (UpdatedPartyDetail is not null)
            {
                return JsonSerializer.Serialize(
                    new { result = new { applied = Applied, skipped = Skipped, rejected = Rejected }, party = UpdatedPartyDetail },
                    _serializerOptions);
            }

            return JsonSerializer.Serialize(
                new { result = new { applied = Applied, skipped = Skipped, rejected = Rejected } },
                _serializerOptions);
        }
    }
}
