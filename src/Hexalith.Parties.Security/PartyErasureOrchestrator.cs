using Hexalith.Parties.Contracts.Security;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Security;

public sealed partial class PartyErasureOrchestrator(
    IPartyKeyManagementService keyManagementService,
    IErasureVerificationService verificationService,
    ILogger<PartyErasureOrchestrator> logger)
{
    private const int DefaultMaxRetries = 5;

    public int MaxRetries { get; set; } = DefaultMaxRetries;

    public async Task<ErasureCertificate?> ExecuteKeyDestructionAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        while (attempt < MaxRetries)
        {
            attempt++;
            try
            {
                ErasureCertificate certificate = await keyManagementService
                    .DeleteKeyAsync(tenantId, partyId, cancellationToken)
                    .ConfigureAwait(false);

                LogKeyDestructionSucceeded(tenantId, partyId, certificate.KeyVersionsDestroyed.Count);
                return certificate;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                LogKeyDestructionRetry(tenantId, partyId, attempt, MaxRetries, ex.Message);
            }
            catch (Exception ex)
            {
                LogKeyDestructionExhausted(tenantId, partyId, MaxRetries, ex.Message);
                return null;
            }
        }

        return null;
    }

    public async Task<ErasureVerificationReport> ExecuteVerificationAsync(
        string tenantId,
        string partyId,
        ErasureCertificate erasureCertificate,
        CancellationToken cancellationToken = default)
    {
        LogVerificationStarted(tenantId, partyId);
        ErasureVerificationReport report = await verificationService
            .VerifyErasureAsync(tenantId, partyId, erasureCertificate, cancellationToken)
            .ConfigureAwait(false);
        LogVerificationCompleted(tenantId, partyId, report.OverallStatus.ToString());
        return report;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Key destruction succeeded for party {TenantId}/{PartyId}: {VersionsDestroyed} versions destroyed")]
    private partial void LogKeyDestructionSucceeded(string tenantId, string partyId, int versionsDestroyed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Key destruction attempt {Attempt}/{MaxRetries} failed for party {TenantId}/{PartyId}: {Error}")]
    private partial void LogKeyDestructionRetry(string tenantId, string partyId, int attempt, int maxRetries, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Key destruction exhausted all {MaxRetries} retries for party {TenantId}/{PartyId}: {Error}")]
    private partial void LogKeyDestructionExhausted(string tenantId, string partyId, int maxRetries, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Erasure verification started for party {TenantId}/{PartyId}")]
    private partial void LogVerificationStarted(string tenantId, string partyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Erasure verification completed for party {TenantId}/{PartyId}: {OverallStatus}")]
    private partial void LogVerificationCompleted(string tenantId, string partyId, string overallStatus);
}
