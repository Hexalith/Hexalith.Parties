using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.Parties.Contracts.Results;

/// <summary>
/// Extends DomainResult with per-sub-operation outcome tracking for composite commands.
/// Applied/Skipped/Rejected contain human-readable descriptions of each sub-operation outcome.
/// </summary>
public sealed record CompositeCommandResult : DomainResult
{
    public CompositeCommandResult(
        IReadOnlyList<IEventPayload> events,
        IReadOnlyList<string> applied,
        IReadOnlyList<string> skipped,
        IReadOnlyList<string> rejected)
        : base(events)
    {
        Applied = applied;
        Skipped = skipped;
        Rejected = rejected;
    }

    /// <summary>Gets descriptions of sub-operations that were successfully applied.</summary>
    public IReadOnlyList<string> Applied { get; }

    /// <summary>Gets descriptions of sub-operations that were skipped (e.g., duplicate additions).</summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>Gets descriptions of sub-operations that were rejected (e.g., invalid IDs).</summary>
    public IReadOnlyList<string> Rejected { get; }
}
