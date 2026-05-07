namespace Hexalith.Parties.AdminPortal.Services;

public sealed class PartiesAdminListCoordinator
{
    private long _version;

    public long Version => Volatile.Read(ref _version);

    public AdminPortalListState State { get; private set; } = AdminPortalListState.Loading;

    public void ResetForTenantSwitch()
    {
        Interlocked.Increment(ref _version);
        State = AdminPortalListState.Loading;
    }
}
