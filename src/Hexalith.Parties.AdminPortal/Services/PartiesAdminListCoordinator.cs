namespace Hexalith.Parties.AdminPortal.Services;

// TODO(Story 10-1.1): wire into PartiesAdminPortal.razor — currently scaffolding for
// FrontComposer integration. The state enum has 10 values but only Loading is reachable
// from the live component path.
public sealed class PartiesAdminListCoordinator
{
    private long _version;
    private int _state = (int)AdminPortalListState.Loading;

    public long Version => Volatile.Read(ref _version);

    public AdminPortalListState State
    {
        get => (AdminPortalListState)Volatile.Read(ref _state);
        private set => Volatile.Write(ref _state, (int)value);
    }

    public void ResetForTenantSwitch()
    {
        Interlocked.Increment(ref _version);
        State = AdminPortalListState.Loading;
    }
}
