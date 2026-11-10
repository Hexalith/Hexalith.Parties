namespace Hexalith.Parties.Platform.LifecycleTests;

/// <summary>Publishes only a complete execution marker to the controller's admitted final name.</summary>
internal static class QualificationExecutionMarker
{
    /// <summary>Writes privately in the same directory, then atomically replaces the published marker.</summary>
    /// <param name="directory">The controller-owned marker directory.</param>
    /// <param name="ready">The complete serialized marker.</param>
    /// <param name="token">The bounded execution token.</param>
    /// <param name="write">An optional controlled writer for publication-boundary tests.</param>
    /// <returns>A task completing after atomic publication.</returns>
    internal static async Task PublishAsync(string directory, string ready, CancellationToken token,
        Func<string, string, CancellationToken, Task>? write = null)
    {
        string temporary = Path.Combine(directory, ".execution-ready-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await (write ?? File.WriteAllTextAsync)(temporary, ready, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, Path.Combine(directory, "execution-ready.json"), overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
