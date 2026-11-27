using Hexalith.Parties.Search;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Delegates ingestion-intent persistence while intercepting direct cleanup confirmation boundaries.</summary>
/// <param name="inner">The underlying durable ingestion-intent store.</param>
/// <param name="afterAuthorization">Optional callback invoked after an empty pre-delete confirmation succeeds.</param>
internal sealed class PartyMemoryDirectCleanupInterceptingIntentStore(
    IPartyMemoryIngestionIntentStore inner,
    Func<CancellationToken, Task>? afterAuthorization = null) : IPartyMemoryIngestionIntentStore
{
    private readonly IPartyMemoryIngestionIntentStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>Gets the number of pre-delete authorization confirmations completed.</summary>
    internal int AuthorizationCount { get; private set; }

    /// <summary>Gets the number of post-delete settlement confirmations attempted.</summary>
    internal int SettlementCount { get; private set; }

    /// <summary>Gets the exception thrown by the most recent settlement confirmation, if any.</summary>
    internal Exception? SettlementException { get; private set; }

    /// <inheritdoc/>
    public Task ProbeAsync(CancellationToken cancellationToken)
        => _inner.ProbeAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<PartyMemoryIngestionRegistration> RegisterAsync(
        string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
        => _inner.RegisterAsync(tenantId, partyId, intent, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryClaimSubmissionAsync(
        string tenantId, string partyId, PartyMemoryIngestionIntent prepared, CancellationToken cancellationToken)
        => _inner.TryClaimSubmissionAsync(tenantId, partyId, prepared, cancellationToken);

    /// <inheritdoc/>
    public Task RegisterSupersededSuccessorAsync(
        string tenantId, string partyId, PartyMemoryIngestionIntent prior,
        string supersededOutputId, string? currentPredecessor, CancellationToken cancellationToken)
        => _inner.RegisterSupersededSuccessorAsync(tenantId, partyId, prior, supersededOutputId, currentPredecessor, cancellationToken);

    /// <inheritdoc/>
    public Task UpdateAsync(
        string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
        => _inner.UpdateAsync(tenantId, partyId, intent, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<PartyMemoryIngestionIntent>> BeginCleanupAsync(
        string tenantId, string partyId, CancellationToken cancellationToken, bool permanent = true)
        => _inner.BeginCleanupAsync(tenantId, partyId, cancellationToken, permanent);

    /// <inheritdoc/>
    public async Task ConfirmDirectCleanupAsync(
        string tenantId,
        string partyId,
        string caseId,
        IReadOnlyCollection<PartyMemoryIngestionIntent> expected,
        IReadOnlyList<PartyMemoryIngestionIntent> confirmed,
        string producerAuthority,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmed);

        if (confirmed.Count == 0)
        {
            await _inner.ConfirmDirectCleanupAsync(
                tenantId, partyId, caseId, expected, confirmed, producerAuthority, cancellationToken).ConfigureAwait(false);
            AuthorizationCount++;
            if (afterAuthorization is not null)
            {
                await afterAuthorization(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        SettlementCount++;
        try
        {
            await _inner.ConfirmDirectCleanupAsync(
                tenantId, partyId, caseId, expected, confirmed, producerAuthority, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SettlementException = ex;
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<PartyMemoryIngestionIntent> SetTerminalObservationAsync(
        string tenantId, string partyId, PartyMemoryIngestionIntent snapshot, string? observationId, CancellationToken cancellationToken)
        => _inner.SetTerminalObservationAsync(tenantId, partyId, snapshot, observationId, cancellationToken);

    /// <inheritdoc/>
    public Task<PartyMemoryIngestionIntent> RetainTerminalStatusAsync(
        string tenantId, string partyId, PartyMemoryIngestionIntent snapshot,
        string canonicalId, string producerAuthority, CancellationToken cancellationToken)
        => _inner.RetainTerminalStatusAsync(tenantId, partyId, snapshot, canonicalId, producerAuthority, cancellationToken);

    /// <inheritdoc/>
    public Task<PartyMemoryIngestionIntent> RetainLegacyAsync(
        string tenantId, string partyId, PartyMemoryUnitMappingEntry mapping, string caseId, string canonicalId,
        CancellationToken cancellationToken, string? producerAuthority = null, bool requirePending = false,
        IReadOnlyCollection<PartyMemoryIngestionIntent>? expectedCase = null)
        => _inner.RetainLegacyAsync(tenantId, partyId, mapping, caseId, canonicalId, cancellationToken, producerAuthority, requirePending, expectedCase);

    /// <inheritdoc/>
    public Task<bool> RetainDiscoveredAsync(
        string tenantId, string partyId, string caseId, string sourceUri, string? workflowId,
        string? canonicalId, string? retryIdentityHash, bool terminalVerified, CancellationToken cancellationToken,
        string? producerAuthority = null, bool requireTerminalEvidence = false,
        (string? ProtocolVersion, long? SourceRevision, string? IdentityDigest) admission = default,
        bool unitPresent = false)
        => _inner.RetainDiscoveredAsync(tenantId, partyId, caseId, sourceUri, workflowId, canonicalId, retryIdentityHash,
            terminalVerified, cancellationToken, producerAuthority, requireTerminalEvidence, admission, unitPresent);

    /// <inheritdoc/>
    public Task<string?> GetSourceClosureVersionAsync(
        string tenantId, string partyId, CancellationToken cancellationToken, string? producerAuthority = null)
        => _inner.GetSourceClosureVersionAsync(tenantId, partyId, cancellationToken, producerAuthority);

    /// <inheritdoc/>
    public Task RetainSourceClosureVersionAsync(
        string tenantId, string partyId, string version, CancellationToken cancellationToken, string? producerAuthority = null)
        => _inner.RetainSourceClosureVersionAsync(tenantId, partyId, version, cancellationToken, producerAuthority);

    /// <inheritdoc/>
    public Task<bool> CanPublishAsync(
        string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
        => _inner.CanPublishAsync(tenantId, partyId, intent, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryReopenAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        => _inner.TryReopenAsync(tenantId, partyId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<PartyMemoryIngestionIntent>> GetAsync(
        string tenantId, string partyId, CancellationToken cancellationToken)
        => _inner.GetAsync(tenantId, partyId, cancellationToken);

    /// <inheritdoc/>
    public Task<int> RemoveAsync(
        string tenantId, string partyId, IReadOnlyList<PartyMemoryIngestionIntent> intents,
        CancellationToken cancellationToken, string? absenceAuthority = null)
        => _inner.RemoveAsync(tenantId, partyId, intents, cancellationToken, absenceAuthority);
}
