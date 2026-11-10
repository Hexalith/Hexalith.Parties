using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Sockets;

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
        // An expired lifecycle may not start persistence, even before a handoff is consumed.
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync().ConfigureAwait(true);
        var assertions = new PartyPersistenceAssertions(new PlatformEnvironmentFixture(), output);
        OperationCanceledException cancellation = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            assertions.ExecuteAsync(canceled.Token)).ConfigureAwait(true);
        Assert.Equal(canceled.Token, cancellation.CancellationToken);
        await AssertPersistenceReadCancelsAsync().ConfigureAwait(true);

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
        // Re-admission uses the same remaining lifetime as initial persistence.
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync().ConfigureAwait(true);
        var assertions = new PartyPersistenceAssertions(new PlatformEnvironmentFixture(), output);
        OperationCanceledException cancellation = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            assertions.RecheckAsync(canceled.Token)).ConfigureAwait(true);
        Assert.Equal(canceled.Token, cancellation.CancellationToken);
        await AssertPersistenceReadCancelsAsync().ConfigureAwait(true);

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
    private static async Task AssertPersistenceReadCancelsAsync()
    {
        using var caller = new CancellationTokenSource();
        using var request = new CancellationTokenSource();
        using CancellationTokenSource operation = PartyEnvironmentTests.CreatePersistenceDeadline(caller.Token, request.Token);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
        Task<HttpResponseMessage> pending = client.GetAsync(new Uri($"http://127.0.0.1:{port}/"), HttpCompletionOption.ResponseHeadersRead, operation.Token);
        using TcpClient peer = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await peer.GetStream().WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 1024\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpResponseMessage response = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
        Task<byte[]> body = response.Content.ReadAsByteArrayAsync(operation.Token);
        Assert.False(body.IsCompleted);
        await caller.CancelAsync().ConfigureAwait(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => body.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ConfigureAwait(true);
        Assert.False(request.IsCancellationRequested);
    }
}
