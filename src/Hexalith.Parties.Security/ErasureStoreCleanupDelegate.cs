using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

/// <summary>Delegate for cleaning a specific data store during erasure verification.</summary>
/// <param name="tenantId">The tenant identifier.</param>
/// <param name="partyId">The party identifier.</param>
/// <param name="cancellationToken">A token that cancels cleanup.</param>
/// <returns>The result of the store cleanup operation.</returns>
public delegate Task<ErasureVerificationStoreResult> ErasureStoreCleanupDelegate(
    string tenantId, string partyId, CancellationToken cancellationToken);
