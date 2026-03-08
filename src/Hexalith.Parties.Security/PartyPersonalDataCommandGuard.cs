using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class PartyPersonalDataCommandGuard(
    ICryptoStatusProvider cryptoStatusProvider,
    IKeyStorageBackend keyStorageBackend) : IPersonalDataCommandGuard
{
    public async Task<string?> GetBlockingReasonAsync(string tenantId, string partyId, object command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentNullException.ThrowIfNull(command);

        if (!PersonalDataGraphInspector.ContainsProtectedData(command))
        {
            return null;
        }

        if (command is CreateParty or CreatePartyComposite)
        {
            return null;
        }

        if (await cryptoStatusProvider.IsCryptoPendingAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false))
        {
            return "Personal data writes are blocked while the party encryption key is in CryptoPending state. Retry after key recovery.";
        }

        IReadOnlyList<int> versions = await keyStorageBackend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        if (versions.Count == 0)
        {
            return "Personal data writes are blocked because no party encryption key is available for this party.";
        }

        return null;
    }
}