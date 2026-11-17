using System.Diagnostics;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Bounds command execution, reaping and inherited output handles independently.</summary>
internal static class PartyMemoryRunnerInterruptionProcess
{
    /// <summary>Executes an owned command and retains execution and cleanup failures.</summary>
    internal static async Task<string> RunAsync(ProcessStartInfo start, TimeSpan execution, Action<Process>? observe = null,
        Func<Process, Task>? terminateOverride = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var supervisor = new PackageTestProcessSupervisor(start, 5000);
        using Process child = Process.Start(supervisor.StartInfo) ?? throw new InvalidOperationException("Owned audit command did not start.");
        using var outputCancellation = new CancellationTokenSource();
        Task<string> output = child.StandardOutput.ReadToEndAsync(outputCancellation.Token);
        Task<string> error = child.StandardError.ReadToEndAsync(outputCancellation.Token);
        var failures = new List<Exception>();
        string result = string.Empty;
        try
        {
            observe?.Invoke(child);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(execution);
            await child.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            if (child.ExitCode != 0) { throw new InvalidOperationException("Owned audit command failed: " + start.FileName + " (exit " + child.ExitCode + ")."); }
        }
        catch (OperationCanceledException failure) when (token.IsCancellationRequested) { failures.Add(failure); }
        catch (OperationCanceledException failure) { failures.Add(new TimeoutException("Owned audit command " + start.FileName + " exceeded its " + execution.TotalSeconds + " second execution budget.", failure)); }
        catch (Exception failure) { failures.Add(failure); }
        await AttemptAsync(async () =>
        {
            if (terminateOverride is not null) { await terminateOverride(child).ConfigureAwait(false); return; }
            if (!child.HasExited)
            {
                if (supervisor.IsContained) { supervisor.RequestTermination(); }
                else { child.Kill(entireProcessTree: true); }
            }
        }).ConfigureAwait(false);
        await AttemptAsync(() =>
        {
            if (supervisor.IsContained && !child.HasExited) { supervisor.RequestTermination(); }
            return Task.CompletedTask;
        }).ConfigureAwait(false);
        await AttemptAsync(async () =>
        {
            using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await child.WaitForExitAsync(reap.Token).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await AttemptAsync(async () => { result = await DrainAsync(child, output, error, outputCancellation, TimeSpan.FromSeconds(3)).ConfigureAwait(false); }).ConfigureAwait(false);
        await AttemptAsync(() => { supervisor.EnsureCleanupSucceeded(start.FileName); return Task.CompletedTask; }).ConfigureAwait(false);
        if (failures.Count != 0 && error.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(error.Result))
        {
            string diagnostic = error.Result;
            failures.Add(new InvalidOperationException("Owned audit stderr: " + diagnostic[..Math.Min(diagnostic.Length, 8192)]));
        }
        if (failures.Count != 0) { throw new AggregateException("Owned command execution or bounded cleanup failed.", failures); }
        return result;

        async Task AttemptAsync(Func<Task> action)
        {
            try { await action().ConfigureAwait(false); }
            catch (Exception failure) { failures.Add(failure); }
        }
    }

    /// <summary>Closes inherited pipes under a separate finite budget even after the command has exited.</summary>
    internal static async Task<string> DrainAsync(Process child, Task<string> output, Task<string> error,
        CancellationTokenSource cancellation, TimeSpan budget)
    {
        try
        {
            await Task.WhenAll(output, error).WaitAsync(budget).ConfigureAwait(false);
            return await output.ConfigureAwait(false);
        }
        finally
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            child.StandardOutput.Dispose();
            child.StandardError.Dispose();
            try { await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
            catch (Exception failure) when (failure is OperationCanceledException or ObjectDisposedException or IOException) { }
        }
    }
}
