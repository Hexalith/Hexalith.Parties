using Hexalith.Parties.Contracts.Security;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Security;

/// <summary>
/// Verifies erasure across projection stores by delegating cleanup to injected functions.
/// The cleanup delegates are wired in DI to call the actual actor EraseAsync methods,
/// keeping this service decoupled from the Projections project.
/// </summary>
public sealed partial class ErasureVerificationService(
    IReadOnlyList<ErasureStoreCleanupDelegate> storeCleanups,
    ILogger<ErasureVerificationService> logger) : IErasureVerificationService
{
    public async Task<ErasureVerificationReport> VerifyErasureAsync(
        string tenantId,
        string partyId,
        ErasureCertificate erasureCertificate,
        CancellationToken cancellationToken = default)
    {
        LogVerificationStarted();

        List<ErasureVerificationStoreResult> storeResults = [];

        for (int i = 0; i < storeCleanups.Count; i++)
        {
            ErasureVerificationStoreResult result;
            try
            {
                result = await storeCleanups[i](tenantId, partyId, cancellationToken).ConfigureAwait(false);
                result = SanitizeResult(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // SDK read models, caches, and search data are not protected by the destroyed
                // aggregate key. An unexpected fault (including a timeout/non-caller
                // cancellation) therefore cannot certify this store as cleaned.
                LogStoreCleanupFailed(i, ex.GetType().Name);
                result = new ErasureVerificationStoreResult
                {
                    StoreName = $"store-{i}",
                    Status = ErasureStoreCleanupStatus.Failed,
                    Timestamp = DateTimeOffset.UtcNow,
                    ErrorMessage = "Store cleanup did not complete.",
                };
            }

            storeResults.Add(result);
        }

        ErasureVerificationOverallStatus overallStatus = DetermineOverallStatus(storeResults);

        ErasureVerificationReport report = new()
        {
            PartyId = partyId,
            TenantId = tenantId,
            Timestamp = DateTimeOffset.UtcNow,
            StoreResults = storeResults,
            OverallStatus = overallStatus,
        };

        LogVerificationCompleted(overallStatus.ToString(), storeResults.Count);
        return report;
    }

    public static ErasureVerificationOverallStatus DetermineOverallStatus(
        List<ErasureVerificationStoreResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        bool anyFailed = false;
        bool anyPending = false;
        bool anyCleaned = false;
        bool anyNonBlocking = false;

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].Status == ErasureStoreCleanupStatus.Failed)
            {
                anyFailed = true;
            }
            else if (results[i].Status == ErasureStoreCleanupStatus.Pending)
            {
                anyPending = true;
            }
            else if (results[i].Status == ErasureStoreCleanupStatus.Cleaned)
            {
                anyCleaned = true;
            }
            else
            {
                anyNonBlocking = true;
            }
        }

        if (anyFailed && (anyCleaned || anyPending || anyNonBlocking))
        {
            return ErasureVerificationOverallStatus.Partial;
        }

        if (anyFailed)
        {
            return ErasureVerificationOverallStatus.Failed;
        }

        if (anyPending)
        {
            return ErasureVerificationOverallStatus.Pending;
        }

        return ErasureVerificationOverallStatus.Complete;
    }

    private static ErasureVerificationStoreResult SanitizeResult(ErasureVerificationStoreResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? result
            : result with
            {
                ErrorMessage = $"Verification did not complete for store '{result.StoreName}' (status {result.Status}).",
            };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erasure store {StoreIndex} cleanup failed with {ExceptionType}.")]
    private partial void LogStoreCleanupFailed(int storeIndex, string exceptionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Erasure verification started.")]
    private partial void LogVerificationStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Erasure verification completed: {OverallStatus} ({StoreCount} stores checked).")]
    private partial void LogVerificationCompleted(string overallStatus, int storeCount);
}

/// <summary>
/// Delegate for cleaning a specific data store during erasure verification.
/// Returns the result of the cleanup operation for the store.
/// </summary>
public delegate Task<ErasureVerificationStoreResult> ErasureStoreCleanupDelegate(
    string tenantId, string partyId, CancellationToken cancellationToken);
