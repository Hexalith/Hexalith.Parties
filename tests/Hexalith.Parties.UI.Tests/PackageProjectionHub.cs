using Microsoft.AspNetCore.SignalR;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Provides the legacy wire contract to qualify the selected transport's cancellation API.</summary>
public sealed class PackageProjectionHub(PackageProjectionBarrier barrier) : Hub
{
    /// <summary>Defers group admission independently of caller cancellation.</summary>
    public async Task JoinGroup(string projectionType, string tenant)
    {
        barrier.Entered.TrySetResult();
        await barrier.Release.Task.ConfigureAwait(false);
        await Groups.AddToGroupAsync(Context.ConnectionId, projectionType + ":" + tenant).ConfigureAwait(false);
        barrier.Finished.TrySetResult();
        barrier.Joined();
    }

    /// <summary>Retains legacy group removal compatibility.</summary>
    public async Task LeaveGroup(string projectionType, string tenant)
    {
        barrier.LeaveEntered.TrySetResult();
        await barrier.LeaveRelease.Task.ConfigureAwait(false);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectionType + ":" + tenant).ConfigureAwait(false);
        barrier.LeaveFinished.TrySetResult();
    }

    /// <inheritdoc/>
    public override Task OnConnectedAsync()
    {
        barrier.Connected(Context);
        return base.OnConnectedAsync();
    }
}
