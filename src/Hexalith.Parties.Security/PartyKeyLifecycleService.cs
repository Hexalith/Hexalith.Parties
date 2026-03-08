using Hexalith.Parties.Contracts.Security;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Security;

public sealed partial class PartyKeyLifecycleService(
    IPartyKeyManagementService keyManagementService,
    IPartyKeyRetryScheduler retryScheduler,
    ILogger<PartyKeyLifecycleService> logger) : ICryptoStatusProvider
{
    public async Task MarkCryptoPendingAsync(string tenantId, string partyId, string reason, CancellationToken cancellationToken = default)
    {
        await retryScheduler.MarkPendingAsync(tenantId, partyId, reason, cancellationToken).ConfigureAwait(false);
        LogKeyCreationFailed(tenantId, partyId, reason);
    }

    public Task ClearCryptoPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => retryScheduler.ClearPendingAsync(tenantId, partyId, cancellationToken);

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
            await MarkCryptoPendingAsync(tenantId, partyId, ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RetryPendingKeyCreationAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        if (!await retryScheduler.IsPendingAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            PartyKeyInfo keyInfo = await keyManagementService.CreateKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            await ClearCryptoPendingAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            LogKeyCreatedAfterRetry(tenantId, partyId, keyInfo.Version);
        }
        catch (Exception ex)
        {
            LogKeyRetryFailed(tenantId, partyId, ex.Message);
        }
    }

    public Task<bool> IsCryptoPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => retryScheduler.IsPendingAsync(tenantId, partyId, cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encryption key created for party {TenantId}/{PartyId} version {Version}")]
    private partial void LogKeyCreated(string tenantId, string partyId, int version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Encryption key creation failed for party {TenantId}/{PartyId}: {Error}. Party marked CryptoPending.")]
    private partial void LogKeyCreationFailed(string tenantId, string partyId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encryption key created after retry for party {TenantId}/{PartyId} version {Version}")]
    private partial void LogKeyCreatedAfterRetry(string tenantId, string partyId, int version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Encryption key retry failed for party {TenantId}/{PartyId}: {Error}")]
    private partial void LogKeyRetryFailed(string tenantId, string partyId, string error);
}
