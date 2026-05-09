namespace Hexalith.Parties.AdminPortal.Services;

/// <summary>
/// Per-circuit observable state for the admin portal browse surface. The live component
/// drives transitions through <see cref="Transition"/> on every load/error path; FrontComposer
/// shells observe <see cref="State"/> and <see cref="Version"/> to render coherent paging
/// and discard in-flight responses after a tenant switch.
/// </summary>
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

    public void Transition(AdminPortalListState newState) => State = newState;

    public void ResetForTenantSwitch()
    {
        Interlocked.Increment(ref _version);
        State = AdminPortalListState.Loading;
    }
}
