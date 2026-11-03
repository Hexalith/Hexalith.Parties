using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Exercises actual bounded local file I/O only; no native environment or hosted credit is supplied.</summary>
/// <param name="output">The local diagnostic output.</param>
public sealed class FunctionalObservationWriterTests(ITestOutputHelper output)
{
    /// <summary>Reproduces duplicate CreateNew refusal and proves the recheck preserves the first canonical bytes.</summary>
    /// <returns>The bounded real-file control.</returns>
    [Fact]
    public async Task DuplicateCanonicalWriteRefusesWhileRecheckPreservesOriginalBytesAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "parties-observation-io-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "native-tests");
        Directory.CreateDirectory(directory);
        string descriptor = Path.Combine(root, "local-io-control-not-a-runner-descriptor.json");
        string report = Path.Combine(directory, "native.trx");
        byte[] first = Encoding.UTF8.GetBytes("{\"classification\":\"local-io-control-only\",\"value\":\"first\"}");
        byte[] recheck = Encoding.UTF8.GetBytes("{\"classification\":\"local-io-control-only\",\"value\":\"recheck\"}");
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await FunctionalObservationWriter.WriteAsync(first, descriptor, report, true, bound.Token).ConfigureAwait(true);
            string path = Path.Combine(directory, "functional-observations.json");
            IOException duplicate = await Assert.ThrowsAsync<IOException>(() =>
                FunctionalObservationWriter.WriteAsync(recheck, descriptor, report, true, bound.Token)).ConfigureAwait(true);
            Assert.Equal(first, await File.ReadAllBytesAsync(path, bound.Token).ConfigureAwait(true));
            await FunctionalObservationWriter.WriteAsync(recheck, descriptor, report, false, bound.Token).ConfigureAwait(true);
            await FunctionalObservationWriter.WriteAsync(recheck, descriptor, report, false, bound.Token).ConfigureAwait(true);
            Assert.Equal(first, await File.ReadAllBytesAsync(path, bound.Token).ConfigureAwait(true));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            output.WriteLine($"Local I/O control only; duplicate={duplicate.GetType().Name}; rechecks=2; canonicalBytesPreserved=true; firstSha256={Convert.ToHexStringLower(SHA256.HashData(first))}; nativeQualification=false.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Refuses a recheck without its first observation, wrong ownership paths and oversized output.</summary>
    /// <returns>The bounded negative I/O controls.</returns>
    [Fact]
    public async Task RecheckRequiresOriginalAndPreservesOutputOwnershipBoundsAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "parties-observation-io-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "native-tests");
        Directory.CreateDirectory(directory);
        string descriptor = Path.Combine(root, "local-io-control-not-a-runner-descriptor.json");
        string report = Path.Combine(directory, "native.trx");
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => FunctionalObservationWriter.WriteAsync([1], descriptor, report, false, bound.Token)).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => FunctionalObservationWriter.WriteAsync([1], descriptor, Path.Combine(root, "native.trx"), true, bound.Token)).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => FunctionalObservationWriter.WriteAsync(new byte[65537], descriptor, report, true, bound.Token)).ConfigureAwait(true);
            Assert.False(File.Exists(Path.Combine(directory, "functional-observations.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
