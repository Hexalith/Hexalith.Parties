using Dapr.Client;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class KeyOperationAuditService(DaprClient daprClient) : IKeyOperationAuditService
{
    private const string StoreName = "statestore";
    private const int MaxRetries = 5;

    public async Task RecordOperationAsync(KeyOperationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string stateKey = BuildStateKey(entry.TenantId, entry.PartyId);

        // Use ETag-based optimistic concurrency to prevent audit record loss under contention.
        // Retries on ETag mismatch (concurrent write by another instance).
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            (List<KeyOperationAuditEntry>? existing, string etag) = await daprClient
                .GetStateAndETagAsync<List<KeyOperationAuditEntry>>(StoreName, stateKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            List<KeyOperationAuditEntry> updated = existing is null ? [] : [.. existing];
            updated.Add(entry);

            bool saved = await daprClient.TrySaveStateAsync(
                StoreName,
                stateKey,
                updated,
                etag,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (saved)
            {
                return;
            }

            // ETag mismatch — another writer updated the state concurrently. Retry with fresh state.
        }

        throw new InvalidOperationException(
            $"Failed to record audit entry after {MaxRetries} attempts due to concurrent writes. TenantId={entry.TenantId}, PartyId={entry.PartyId}");
    }

    public async Task<IReadOnlyList<KeyOperationAuditEntry>> GetAuditTrailAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        string stateKey = BuildStateKey(tenantId, partyId);

        List<KeyOperationAuditEntry>? entries = await daprClient.GetStateAsync<List<KeyOperationAuditEntry>>(
            StoreName, stateKey, cancellationToken: cancellationToken).ConfigureAwait(false);

        return entries ?? [];
    }

    private static string BuildStateKey(string tenantId, string partyId)
        => $"{tenantId}:party-key-audit:{partyId}";
}
