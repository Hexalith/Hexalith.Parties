using Microsoft.AspNetCore.SignalR;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Controls a real packaged projection RPC acknowledgement.</summary>
public sealed class PackageProjectionBarrier
{
    private Action? _disconnect;
    private int _joins;
    /// <summary>Gets the currently observed physical connection.</summary>
    public string ConnectionId { get; private set; } = string.Empty;
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
        ConnectionId = context.ConnectionId;
        // End the HTTP transport without sending a terminal SignalR Close message.
        // The production client's automatic reconnect must observe a recoverable loss.
        var transport = context.GetHttpContext() ?? throw new InvalidOperationException("No HTTP transport.");
        Volatile.Write(ref _disconnect, transport.Abort);
    }
    /// <summary>Signals actual group admission on a replacement connection.</summary>
    public void Joined() { if (Interlocked.Increment(ref _joins) > 1) { Rejoined.TrySetResult(); } }
    /// <summary>Ends the actual transport so the production client must reconnect and rejoin.</summary>
    public void Disconnect() => (Volatile.Read(ref _disconnect) ?? throw new InvalidOperationException("No connected transport."))();
    /// <summary>Gets join entry.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets explicit join release.</summary>
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets actual server completion.</summary>
    public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
