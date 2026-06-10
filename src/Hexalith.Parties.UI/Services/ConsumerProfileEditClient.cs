using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.UI.Services;

internal sealed class ConsumerProfileEditClient(ISelfScopedPartiesClient selfScopedPartiesClient) : IConsumerProfileEditClient
{
    public async Task<ConsumerProfileUpdateResult> UpdateMyProfileAsync(
        ConsumerProfileUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            PartiesCommandResult<PartyDetail> result = await selfScopedPartiesClient
                .UpdateMyProfileAsync(
                    new SelfScopedProfileUpdateRequest
                    {
                        PersonDetails = request.PersonDetails,
                        OrganizationDetails = request.OrganizationDetails,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted, result.Payload);
        }
        catch (PartiesClientException ex) when (ex.Status is 400 or 422)
        {
            return new ConsumerProfileUpdateResult(
                ConsumerProfileUpdateOutcome.ValidationRejected,
                ValidationFailures:
                [
                    new ConsumerProfileValidationFailure("Profile"),
                ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Failed);
        }
    }
}
