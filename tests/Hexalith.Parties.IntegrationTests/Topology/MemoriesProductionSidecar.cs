using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Owns an operator-configured Dapr runtime for app startup controls; it does not prove sidecar manifest emission.</summary>
internal sealed class MemoriesProductionSidecar : IAsyncDisposable
{
    private readonly string _directory;
    private readonly Process _process;
    private readonly Task<string> _output;
    private readonly Task<string> _error;
    private readonly Uri _outboundHealth;
    private readonly string _apiToken;
    private readonly string _appId;

    private MemoriesProductionSidecar(string directory, Process process, int httpPort, string apiToken, string appId)
    {
        _directory = directory;
        _process = process;
        _outboundHealth = new Uri($"http://127.0.0.1:{httpPort}/v1.0/healthz/outbound");
        _apiToken = apiToken;
        _appId = appId;
        _output = process.StandardOutput.ReadToEndAsync();
        _error = process.StandardError.ReadToEndAsync();
    }

    /// <summary>Starts Dapr with operator-supplied tokens, owned listeners and a real store for app-only startup proof.</summary>
    internal static MemoriesProductionSidecar Start(ProcessStartInfo producer, string redis)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentException.ThrowIfNullOrWhiteSpace(redis);
        string runtime = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_TEST_DAPRD")
            ?? throw new InvalidOperationException("PARTIES_MEMORIES_TEST_DAPRD must name the selected real daprd binary.");
        string directory = Path.Combine(Path.GetTempPath(), $"parties-production-sidecar-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "statestore.yaml"),
            $"apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n    - name: redisHost\n      value: '{redis}'\n    - name: actorStateStore\n      value: 'true'\n");
        File.WriteAllText(Path.Combine(directory, "pubsub.yaml"),
            $"apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: pubsub\nspec:\n  type: pubsub.redis\n  version: v1\n  metadata:\n    - name: redisHost\n      value: '{redis}'\n");
        int[] ports = AllocatePorts();
        producer.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{ports[0]}";
        producer.Environment["DAPR_HTTP_PORT"] = ports[1].ToString(System.Globalization.CultureInfo.InvariantCulture);
        producer.Environment["DAPR_GRPC_PORT"] = ports[2].ToString(System.Globalization.CultureInfo.InvariantCulture);
        string appId = "memories-production-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(runtime) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "--app-id", appId, "--app-port", ports[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--dapr-http-port", ports[1].ToString(System.Globalization.CultureInfo.InvariantCulture), "--dapr-grpc-port", ports[2].ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--dapr-internal-grpc-port", ports[3].ToString(System.Globalization.CultureInfo.InvariantCulture), "--enable-metrics=false",
            "--resources-path", directory, "--placement-host-address", "", "--scheduler-host-address", "", "--enable-app-health-check=false" }) { start.ArgumentList.Add(argument); }
        start.Environment["APP_API_TOKEN"] = producer.Environment["APP_API_TOKEN"];
        start.Environment["DAPR_API_TOKEN"] = producer.Environment["DAPR_API_TOKEN"];
        try { return new MemoriesProductionSidecar(directory, Process.Start(start) ?? throw new InvalidOperationException("The actual producer sidecar could not start."), ports[1], start.Environment["DAPR_API_TOKEN"]!, appId); }
        catch { Directory.Delete(directory, recursive: true); throw; }
    }

    /// <summary>Requires outbound health and protected metadata for this live runtime within a finite observation budget.</summary>
    internal async Task WaitForOutboundHealthAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("dapr-api-token", _apiToken);
        while (true)
        {
            if (_process.HasExited) { throw new InvalidOperationException("The actual Dapr sidecar exited before outbound readiness could be proved."); }
            try
            {
                using HttpResponseMessage response = await client.GetAsync(_outboundHealth, deadline.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode && !_process.HasExited)
                {
                    // Health routes may be public. Prove token enforcement on
                    // metadata and bind the answer to this exact sidecar app ID.
                    var metadata = new Uri(_outboundHealth, "/v1.0/metadata");
                    using HttpResponseMessage authenticated = await client.GetAsync(metadata, deadline.Token).ConfigureAwait(false);
                    authenticated.EnsureSuccessStatusCode();
                    using JsonDocument identity = JsonDocument.Parse(await authenticated.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false));
                    if (identity.RootElement.GetProperty("id").GetString() != _appId)
                    {
                        throw new InvalidOperationException("Dapr metadata belonged to another sidecar app ID.");
                    }
                    using var untrusted = new HttpClient();
                    using HttpResponseMessage missingToken = await untrusted.GetAsync(metadata, deadline.Token).ConfigureAwait(false);
                    using var wrongRequest = new HttpRequestMessage(HttpMethod.Get, metadata);
                    wrongRequest.Headers.Add("dapr-api-token", "wrong-" + Guid.NewGuid().ToString("N"));
                    using HttpResponseMessage wrongToken = await untrusted.SendAsync(wrongRequest, deadline.Token).ConfigureAwait(false);
                    if (missingToken.StatusCode != HttpStatusCode.Unauthorized || wrongToken.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        throw new InvalidOperationException("Protected Dapr metadata did not reject missing and wrong API tokens.");
                    }
                    if (_process.HasExited) { throw new InvalidOperationException("The actual Dapr sidecar exited during readiness observation."); }
                    return;
                }
            }
            catch (HttpRequestException) when (!_process.HasExited) { }
            await Task.Delay(50, deadline.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Stops the owned real runtime while the producer remains running for the negative control.</summary>
    internal async Task StopForFailureControlAsync()
    {
        if (!_process.HasExited) { _process.Kill(entireProcessTree: true); }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        try { await StopForFailureControlAsync().ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        try
        {
            using var capture = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await Task.WhenAll(_output, _error).WaitAsync(capture.Token).ConfigureAwait(false);
        }
        catch (Exception exception) { failures.Add(exception); }
        try { _process.Dispose(); }
        catch (Exception exception) { failures.Add(exception); }
        try { Directory.Delete(_directory, recursive: true); }
        catch (Exception exception) { failures.Add(exception); }
        if (failures.Count > 0) { throw new AggregateException("Owned sidecar cleanup failed after independently bounded attempts.", failures); }
    }

    private static int[] AllocatePorts()
    {
        var listeners = new List<TcpListener>();
        try
        {
            for (int index = 0; index < 4; index++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                listeners.Add(listener);
            }
            return listeners.Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port).ToArray();
        }
        finally { foreach (TcpListener listener in listeners) { listener.Stop(); } }
    }
}
