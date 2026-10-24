namespace Hexalith.Parties.Search;

/// <summary>Persists content-free recovery obligations independently of the mapping inventory.</summary>
internal interface IPartyMemoryIngestionIntentStore
{
    /// <summary>Round-trips and conditionally erases a unique reserved inventory sentinel.</summary>
    Task ProbeAsync(CancellationToken cancellationToken);

    /// <summary>Atomically reserves an attempt before submission, rejecting fenced or full inventories.</summary>
    Task<PartyMemoryIngestionRegistration> RegisterAsync(string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken);

    /// <summary>Retains a definitively superseded terminal producer and a stable successor reservation for redelivery.</summary>
    Task RegisterSupersededSuccessorAsync(string tenantId, string partyId, PartyMemoryIngestionIntent prior,
        string supersededOutputId, string? currentPredecessor, CancellationToken cancellationToken);

    /// <summary>Captures a workflow or canonical identity without recreating a removed obligation.</summary>
    Task UpdateAsync(string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken);

    /// <summary>Closes admission before taking the cleanup snapshot, including racing reservations.</summary>
    Task<IReadOnlyList<PartyMemoryIngestionIntent>> BeginCleanupAsync(string tenantId, string partyId, CancellationToken cancellationToken, bool permanent = true);

    /// <summary>Retains validated legacy workflow provenance under the cleanup fence before remote deletion.</summary>
    Task<PartyMemoryIngestionIntent> RetainLegacyAsync(string tenantId, string partyId, PartyMemoryUnitMappingEntry mapping, string caseId, string canonicalId, CancellationToken cancellationToken, string? producerAuthority = null);

    /// <summary>Durably retains a producer discovered in the authoritative remote source inventory under the cleanup fence.</summary>
    Task RetainDiscoveredAsync(string tenantId, string partyId, string caseId, string sourceUri, string? workflowId,
        string? canonicalId, string? retryIdentityHash, bool terminalVerified, CancellationToken cancellationToken, string? producerAuthority = null);

    /// <summary>Reads previously verified complete absence under an enduring remote permanent fence.</summary>
    Task<string?> GetSourceClosureVersionAsync(string tenantId, string partyId, CancellationToken cancellationToken, string? producerAuthority = null);

    /// <summary>Persists complete terminal remote absence only after every local obligation has settled.</summary>
    Task RetainSourceClosureVersionAsync(string tenantId, string partyId, string version, CancellationToken cancellationToken, string? producerAuthority = null);

    /// <summary>Checks that this exact attempt belongs to the open generation and has no settled canonical receipt.</summary>
    Task<bool> CanPublishAsync(string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken);

    /// <summary>Reopens a completed transient cleanup only with readable canonical live projection evidence.</summary>
    Task<bool> TryReopenAsync(string tenantId, string partyId, CancellationToken cancellationToken);

    /// <summary>Reads obligations after restart or conditional cleanup.</summary>
    Task<IReadOnlyList<PartyMemoryIngestionIntent>> GetAsync(string tenantId, string partyId, CancellationToken cancellationToken);

    /// <summary>Subtracts only verified exact obligations, preserving changed or concurrent entries.</summary>
    Task<int> RemoveAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryIngestionIntent> intents, CancellationToken cancellationToken, string? absenceAuthority = null);
}
