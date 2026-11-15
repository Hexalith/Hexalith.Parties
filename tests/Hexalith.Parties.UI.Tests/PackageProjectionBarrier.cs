using Microsoft.AspNetCore.SignalR;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Controls a real packaged projection RPC acknowledgement.</summary>
public sealed class PackageProjectionBarrier
{
    private Action? _disconnect;
    private string? _originalConnectionId;
    private string _connectionId = string.Empty;
    /// <summary>Gets the currently observed physical connection.</summary>
    public string ConnectionId => Volatile.Read(ref _connectionId);
    /// <summary>Gets the server's observed termination of the original connection.</summary>
    public TaskCompletionSource OriginalDisconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets actual survivor membership on a replacement connection.</summary>
    public TaskCompletionSource<string> SurvivorRejoined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets entry into a real leave RPC.</summary>
    public TaskCompletionSource LeaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets explicit leave release.</summary>
    public TaskCompletionSource LeaveRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets actual leave completion.</summary>
    public TaskCompletionSource LeaveFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets the replacement connection's completed group admission.</summary>
    public TaskCompletionSource Rejoined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Captures an actual server connection without accessing the client internals.</summary>
    public void Connected(Microsoft.AspNetCore.SignalR.HubCallerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Interlocked.CompareExchange(ref _originalConnectionId, context.ConnectionId, null);
        Volatile.Write(ref _connectionId, context.ConnectionId);
        // End the HTTP transport without sending a terminal SignalR Close message.
        // The production client's automatic reconnect must observe a recoverable loss.
        var transport = context.GetHttpContext() ?? throw new InvalidOperationException("No HTTP transport.");
        Volatile.Write(ref _disconnect, transport.Abort);
    }
    /// <summary>Signals actual group admission on a replacement connection.</summary>
    public void Joined(string projectionType, string connectionId)
    {
        if (connectionId != Volatile.Read(ref _originalConnectionId))
        {
            if (projectionType == "counter") { Rejoined.TrySetResult(); }
            if (projectionType == "survivor") { SurvivorRejoined.TrySetResult(connectionId); }
        }
    }
    /// <summary>Records actual server-side disconnection without inferring it from a replacement.</summary>
    public void Disconnected(string connectionId)
    {
        if (connectionId == Volatile.Read(ref _originalConnectionId)) { OriginalDisconnected.TrySetResult(); }
    }
    /// <summary>Ends the actual transport so the production client must reconnect and rejoin.</summary>
    public void Disconnect() => (Volatile.Read(ref _disconnect) ?? throw new InvalidOperationException("No connected transport."))();
    /// <summary>Gets join entry.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets explicit join release.</summary>
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets actual server completion.</summary>
    public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
