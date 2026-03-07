using System.Collections.Concurrent;

using Hexalith.Parties.Contracts.Security;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Security;

public sealed partial class PartyKeyLifecycleService(
    IPartyKeyManagementService keyManagementService,
    ILogger<PartyKeyLifecycleService> logger) : ICryptoStatusProvider
{
    private readonly ConcurrentDictionary<string, bool> _cryptoPendingParties = new();

    public async Task OnPartyCreatedAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        try
        {
            PartyKeyInfo keyInfo = await keyManagementService.CreateKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            LogKeyCreated(tenantId, partyId, keyInfo.Version);
        }
        catch (Exception ex)
        {
            // Party creation MUST NEVER fail due to key infrastructure unavailability
            string pendingKey = BuildPendingKey(tenantId, partyId);
            _cryptoPendingParties[pendingKey] = true;
            LogKeyCreationFailed(tenantId, partyId, ex.Message);
        }
    }

    public async Task RetryPendingKeyCreationAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        string pendingKey = BuildPendingKey(tenantId, partyId);
        if (!_cryptoPendingParties.ContainsKey(pendingKey))
        {
            return;
        }

        try
        {
            PartyKeyInfo keyInfo = await keyManagementService.CreateKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            _cryptoPendingParties.TryRemove(pendingKey, out _);
            LogKeyCreatedAfterRetry(tenantId, partyId, keyInfo.Version);
        }
        catch (Exception ex)
        {
            LogKeyRetryFailed(tenantId, partyId, ex.Message);
        }
    }

    public Task<bool> IsCryptoPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        string pendingKey = BuildPendingKey(tenantId, partyId);
        return Task.FromResult(_cryptoPendingParties.ContainsKey(pendingKey));
    }

    private static string BuildPendingKey(string tenantId, string partyId) => $"{tenantId}:{partyId}";

    [LoggerMessage(Level = LogLevel.Information, Message = "Encryption key created for party {TenantId}/{PartyId} version {Version}")]
    private partial void LogKeyCreated(string tenantId, string partyId, int version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Encryption key creation failed for party {TenantId}/{PartyId}: {Error}. Party marked CryptoPending.")]
    private partial void LogKeyCreationFailed(string tenantId, string partyId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encryption key created after retry for party {TenantId}/{PartyId} version {Version}")]
    private partial void LogKeyCreatedAfterRetry(string tenantId, string partyId, int version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Encryption key retry failed for party {TenantId}/{PartyId}: {Error}")]
    private partial void LogKeyRetryFailed(string tenantId, string partyId, string error);
}
