using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Operation-owned rebuild state, separate from the canonical index schema.</summary>
/// <param name="Model">The accumulated index.</param>
/// <param name="Diagnostics">The bounded drop counts for this candidate.</param>
internal sealed record PartyIndexRebuildCandidate(
    PartyIndexSdkReadModel Model,
    PartyProjectionRebuildDiagnostics Diagnostics);
