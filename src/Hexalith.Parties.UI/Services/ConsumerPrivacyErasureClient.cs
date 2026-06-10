using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.UI.Services;

internal sealed class ConsumerPrivacyErasureClient(ISelfScopedPartiesClient selfScopedPartiesClient) : IConsumerPrivacyErasureClient
{
    public async Task<ConsumerPrivacyErasureResult> GetMyErasureStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            PartyErasureStatusRecord? status = await selfScopedPartiesClient
                .GetMyErasureStatusAsync(cancellationToken)
                .ConfigureAwait(false);

            return MapStatus(status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Forbidden);
        }
        catch
        {
            return ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.TransientFailure);
        }
    }

    public async Task<ConsumerPrivacyErasureResult> RequestMyErasureAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AdminPortalGdprCommandResult result = await selfScopedPartiesClient
                .RequestMyErasureAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result.Outcome is not AdminPortalGdprOutcome.Accepted and not AdminPortalGdprOutcome.Completed)
            {
                return MapOutcome(result.Outcome);
            }

            ConsumerPrivacyErasureResult status = await GetMyErasureStatusAsync(cancellationToken).ConfigureAwait(false);
            return status.State is ConsumerPrivacyErasureState.Active or ConsumerPrivacyErasureState.Unknown
                ? new ConsumerPrivacyErasureResult(
                    ConsumerPrivacyErasureOutcome.Pending,
                    ConsumerPrivacyErasureState.ErasurePending,
                    CanCancel: true,
                    status.Freshness)
                : status;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Forbidden);
        }
        catch
        {
            return ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.TransientFailure);
        }
    }

    public async Task<ConsumerPrivacyErasureResult> CancelMyErasureAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AdminPortalGdprCommandResult result = await selfScopedPartiesClient
                .CancelMyErasureAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result.Outcome is not AdminPortalGdprOutcome.Accepted and not AdminPortalGdprOutcome.Completed)
            {
                return MapOutcome(result.Outcome);
            }

            ConsumerPrivacyErasureResult status = await GetMyErasureStatusAsync(cancellationToken).ConfigureAwait(false);
            return status.State is ConsumerPrivacyErasureState.Active
                ? status with { Outcome = ConsumerPrivacyErasureOutcome.CancellationAccepted }
                : status;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Forbidden);
        }
        catch
        {
            return ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.TransientFailure);
        }
    }

    private static ConsumerPrivacyErasureResult MapStatus(PartyErasureStatusRecord? status)
    {
        if (status is null)
        {
            return ConsumerPrivacyErasureResult.Active();
        }

        return status.Status switch
        {
            "Active" => ConsumerPrivacyErasureResult.Active(),
            "ErasurePending" => new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                ConsumerPrivacyErasureState.ErasurePending,
                CanCancel: true),
            "KeyDestroyed" => new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                ConsumerPrivacyErasureState.KeyDestroyed,
                CanCancel: false),
            "VerificationInProgress" => new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                ConsumerPrivacyErasureState.VerificationInProgress,
                CanCancel: false),
            "Verified" => new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                ConsumerPrivacyErasureState.Verified,
                CanCancel: false),
            "Erased" => new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Permanent,
                ConsumerPrivacyErasureState.Erased,
                CanCancel: false),
            _ => ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Unavailable),
        };
    }

    private static ConsumerPrivacyErasureResult MapOutcome(AdminPortalGdprOutcome outcome)
        => outcome switch
        {
            AdminPortalGdprOutcome.Forbidden or AdminPortalGdprOutcome.MissingTenant => ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Forbidden),
            AdminPortalGdprOutcome.AuthenticationRequired => ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.AuthenticationRequired),
            AdminPortalGdprOutcome.ErasureInProgress or AdminPortalGdprOutcome.Erased => ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Rejected),
            AdminPortalGdprOutcome.ContractUnavailable => ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Unavailable),
            AdminPortalGdprOutcome.TransientFailure => ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.TransientFailure),
            _ => ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Rejected),
        };
}
