namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Writes one canonical native observation; post-hold rechecks preserve that first file.</summary>
internal static class FunctionalObservationWriter
{
    /// <summary>Records the initial observation or verifies that a recheck has an existing canonical observation.</summary>
    /// <param name="bytes">The bounded, unchanged observation serialization.</param>
    /// <param name="descriptor">The owning runner descriptor path.</param>
    /// <param name="report">The owning native TRX report path.</param>
    /// <param name="recordCanonicalObservation">Whether this is the initial canonical write.</param>
    /// <param name="token">The bounded test token.</param>
    /// <returns>The single initial write or read-only recheck of the canonical file's presence.</returns>
    internal static async Task WriteAsync(byte[] bytes, string? descriptor, string? report, bool recordCanonicalObservation, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(descriptor) || string.IsNullOrWhiteSpace(report)
            || !Path.IsPathFullyQualified(descriptor) || !Path.IsPathFullyQualified(report))
            throw new InvalidOperationException("The explicit owning native observation binding is unavailable.");
        string workspace = Path.GetDirectoryName(Path.GetFullPath(descriptor))!;
        string directory = Path.Combine(workspace, "native-tests");
        if (Path.GetDirectoryName(Path.GetFullPath(report)) != directory || Path.GetFileName(report) != "native.trx"
            || !Directory.Exists(directory))
            throw new InvalidOperationException("The native report differs from the descriptor-owned output directory.");
        if (bytes.Length > 65536) throw new InvalidOperationException("The native observation exceeds its bounded output contract.");
        string path = Path.Combine(directory, "functional-observations.json");
        if (!recordCanonicalObservation)
        {
            FileInfo original = new(path);
            if (!original.Exists || original.LinkTarget is not null || original.Length > 65536)
                throw new InvalidOperationException("The original canonical observation is unavailable for the recheck.");
            return;
        }
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        FileStream stream = new(path, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
    }
}
