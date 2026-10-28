using Hexalith.EventStore.Client.Projections;

using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Operation-owned rebuild state, separate from the canonical index schema.</summary>
/// <param name="Model">The accumulated index.</param>
/// <param name="Diagnostics">The bounded drop counts for this candidate.</param>
/// <param name="Concurrency">The original conditional publication policy, retained across terminal retries.</param>
internal sealed record PartyIndexRebuildCandidate(
    PartyIndexSdkReadModel Model,
    PartyProjectionRebuildDiagnostics Diagnostics,
    ReadModelBatchConcurrency? Concurrency = null);
