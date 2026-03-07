using Dapr.Client;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class KeyOperationAuditService(DaprClient daprClient) : IKeyOperationAuditService
{
    private const string StoreName = "statestore";

    public async Task RecordOperationAsync(KeyOperationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string stateKey = BuildStateKey(entry.TenantId, entry.PartyId);

        List<KeyOperationAuditEntry>? existing = await daprClient.GetStateAsync<List<KeyOperationAuditEntry>>(
            StoreName, stateKey, cancellationToken: cancellationToken).ConfigureAwait(false);

        existing ??= [];
        existing.Add(entry);

        await daprClient.SaveStateAsync(StoreName, stateKey, existing, cancellationToken: cancellationToken).ConfigureAwait(false);
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
