using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Contracts.Tests.Privacy;

/// <summary>A value-record fixture proving that nested identity inventory traversal reaches its payload.</summary>
/// <param name="Evidence">The identifying record nested inside a nullable value-record container.</param>
internal readonly record struct IdentityInventoryValueRecord(HumanActorBindingEvidence Evidence);
