namespace Hexalith.Parties.AdminPortal.Services;

public sealed class AdminPortalGdprStateCoordinator
{
    public string? ActiveTenantId { get; private set; }

    public string? ActivePartyId { get; private set; }

    public long RequestVersion { get; private set; }

    public AdminPortalGdprOperationState State { get; private set; } = AdminPortalGdprOperationState.NotLoaded;

    public void Track(string? tenantId, string? partyId)
    {
        if (string.Equals(ActiveTenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(ActivePartyId, partyId, StringComparison.Ordinal))
        {
            return;
        }

        ActiveTenantId = tenantId;
        ActivePartyId = partyId;
        RequestVersion++;
        State = string.IsNullOrWhiteSpace(partyId)
            ? AdminPortalGdprOperationState.NotLoaded
            : AdminPortalGdprOperationState.Ready;
    }

    public void ResetForTenantSwitch() => Reset(AdminPortalGdprOperationState.NotLoaded);

    public void ResetForSignOut() => Reset(AdminPortalGdprOperationState.MissingToken);

    public void ResetForPartyChange() => Reset(AdminPortalGdprOperationState.NotLoaded);

    public void ResetForAuthorizationFailure() => Reset(AdminPortalGdprOperationState.Forbidden);

    public void ResetForErasedTerminalState() => Reset(AdminPortalGdprOperationState.Erased);

    public bool TryApplyResponse(
        string? tenantId,
        string? partyId,
        string operation,
        long requestVersion,
        AdminPortalGdprOperationState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!string.Equals(ActiveTenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(ActivePartyId, partyId, StringComparison.Ordinal)
            || requestVersion != RequestVersion)
        {
            return false;
        }

        State = state;
        return true;
    }

#pragma warning disable CA1822 // Reflection contract expects an instance coordinator method.
    public bool CanMutateParty(AdminPortalGdprOperationState state)
        => state is not (AdminPortalGdprOperationState.ErasurePending
            or AdminPortalGdprOperationState.VerificationPartial
            or AdminPortalGdprOperationState.VerificationFailed
            or AdminPortalGdprOperationState.Verified
            or AdminPortalGdprOperationState.Erased);
#pragma warning restore CA1822

    private void Reset(AdminPortalGdprOperationState state)
    {
        ActivePartyId = null;
        RequestVersion++;
        State = state;
    }
}
