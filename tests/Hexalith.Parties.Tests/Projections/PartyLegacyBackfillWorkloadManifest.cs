using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Immutable synthetic input transported across seed, rebuild, stage, commit and verification workloads.</summary>
/// <param name="Tenant">The exact exclusively synthetic tenant.</param>
/// <param name="Histories">The original complete captured histories, including event identities and payloads.</param>
/// <param name="IndexOperation">The one stable shared operation resumed by every workload.</param>
/// <param name="Existing">Whether the rehearsal replaces existing ETag-bearing canonical models.</param>
/// <param name="LargeOperation">The stable aggregate operation interrupted and resumed across processes.</param>
internal sealed record PartyLegacyBackfillWorkloadManifest(string Tenant, ProjectionRequest[] Histories, string IndexOperation, bool Existing, string LargeOperation);
