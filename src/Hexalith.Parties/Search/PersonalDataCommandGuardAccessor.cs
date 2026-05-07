using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Search;

internal static class PersonalDataCommandGuardAccessor
{
    public static async Task EnsureWriteAllowedAsync<TCommand>(
        IServiceProvider services,
        string tenantId,
        string partyId,
        TCommand command,
        CancellationToken cancellationToken)
    {
        IPersonalDataCommandGuard? commandGuard = services.GetService<IPersonalDataCommandGuard>();
        if (commandGuard is null)
        {
            return;
        }

        string? blockingReason = await commandGuard
            .GetBlockingReasonAsync(tenantId, partyId, command!, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(blockingReason))
        {
            throw new InvalidOperationException(blockingReason);
        }
    }
}
