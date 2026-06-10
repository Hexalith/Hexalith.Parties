using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.UI.Services;

internal sealed class ConsumerConsentClient(ISelfScopedPartiesClient selfScopedPartiesClient) : IConsumerConsentClient
{
    public async Task<ConsumerConsentOverview> GetMyConsentOverviewAsync(CancellationToken cancellationToken = default)
    {
        PartyDetail detail = await selfScopedPartiesClient.GetMyPartyAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ConsentRecord> consentRecords = await selfScopedPartiesClient
            .GetMyConsentAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ConsumerConsentOverview(
            detail.IsErased,
            detail.Freshness,
            detail.ContactChannels
                .Select(static channel => new ConsumerConsentChannel(channel.Id, channel.Type))
                .ToArray(),
            consentRecords);
    }

    public async Task<ConsumerConsentOperationResult> GrantMyConsentAsync(
        ConsumerConsentGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AdminPortalGdprCommandResult result = await selfScopedPartiesClient
                .GrantMyConsentAsync(request.ChannelId, request.Purpose, request.LawfulBasis, cancellationToken)
                .ConfigureAwait(false);

            return new ConsumerConsentOperationResult(MapOutcome(result.Outcome));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new ConsumerConsentOperationResult(ConsumerConsentOperationOutcome.Failed);
        }
    }

    public async Task<ConsumerConsentOperationResult> WithdrawMyConsentAsync(
        ConsumerConsentWithdrawRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AdminPortalGdprCommandResult result = await selfScopedPartiesClient
                .RevokeMyConsentAsync(request.ConsentId, cancellationToken)
                .ConfigureAwait(false);

            return new ConsumerConsentOperationResult(MapOutcome(result.Outcome));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new ConsumerConsentOperationResult(ConsumerConsentOperationOutcome.Failed);
        }
    }

    private static ConsumerConsentOperationOutcome MapOutcome(AdminPortalGdprOutcome outcome)
        => outcome switch
        {
            AdminPortalGdprOutcome.Accepted or AdminPortalGdprOutcome.Completed => ConsumerConsentOperationOutcome.Accepted,
            AdminPortalGdprOutcome.ValidationRejected => ConsumerConsentOperationOutcome.ValidationRejected,
            AdminPortalGdprOutcome.Forbidden
                or AdminPortalGdprOutcome.MissingTenant
                or AdminPortalGdprOutcome.AuthenticationRequired
                or AdminPortalGdprOutcome.NotFound => ConsumerConsentOperationOutcome.Forbidden,
            AdminPortalGdprOutcome.Erased or AdminPortalGdprOutcome.ErasureInProgress => ConsumerConsentOperationOutcome.Erased,
            AdminPortalGdprOutcome.TransientFailure or AdminPortalGdprOutcome.ContractUnavailable => ConsumerConsentOperationOutcome.TransientFailure,
            _ => ConsumerConsentOperationOutcome.Failed,
        };
}
