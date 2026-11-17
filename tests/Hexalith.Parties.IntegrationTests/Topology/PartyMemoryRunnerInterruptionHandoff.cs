using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Parties.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Authenticates a bounded private checkpoint between the owned runner and its external supervisor.</summary>
internal sealed class PartyMemoryRunnerInterruptionHandoff
{
    /// <summary>The private configuration passed only to the disposable runner.</summary>
    internal const string ConfigurationVariable = "PARTIES_MEMORY_RUNNER_INTERRUPTION";

    private readonly JsonElement _configuration;
    private JsonElement _topology;

    private PartyMemoryRunnerInterruptionHandoff(JsonElement configuration)
    {
        _configuration = configuration;
        _topology = PartyMemoryRunnerInterruptionAudit.Authorize(configuration.GetProperty("SessionId").GetString()!,
            configuration.GetProperty("SessionRoot").GetString()!, configuration.GetProperty("WorkerMarker").GetString()!,
            configuration.GetProperty("LineageMarker").GetString()!);
        if (!PartyMemoryProducerRestartProtocol.Phases.Contains(Phase)) { throw new InvalidDataException("Unknown runner interruption phase."); }
    }

    /// <summary>Gets the one authenticated phase at which this runner must remain suspended.</summary>
    internal string WorkerMarker => _configuration.GetProperty("WorkerMarker").GetString()!;

    /// <summary>Consumes the supervisor-issued session before any startup side effect.</summary>
    internal PartiesTopologySession CreateSession() => PartiesTopologySession.CreateAuthorized(_configuration.GetProperty("SessionId").GetString()!, _configuration.GetProperty("SessionRoot").GetString()!);

    /// <summary>Publishes incremental creation and failure custody while the fixture owns its observations.</summary>
    internal Task PublishCustodyAsync(PartiesAspireTopologyFixture fixture, string privateRoot)
        => PublishAsync(fixture, privateRoot, JsonSerializer.SerializeToElement(new { Phase = "startup-custody-only", fixture.InitializationPhase }), custodyOnly: true);

    /// <summary>Gets the selected authenticated business checkpoint.</summary>
    internal string Phase => _configuration.GetProperty("Phase").GetString()!;

    /// <summary>Loads the optional supervisor handoff without changing ordinary workload behavior.</summary>
    internal static PartyMemoryRunnerInterruptionHandoff? Read()
    {
        string? path = Environment.GetEnvironmentVariable(ConfigurationVariable);
        return path is null ? null : new(ReadBounded(path));
    }

    /// <summary>Publishes fresh observed evidence and exact owned resource identities before the supervisor sends SIGINT.</summary>
    internal async Task PublishAsync(PartiesAspireTopologyFixture fixture, string privateRoot, JsonElement observation, bool custodyOnly = false,
        CancellationToken token = default)
    {
        JsonElement startup = JsonSerializer.SerializeToElement(new { Phase = "startup-custody-only", fixture.InitializationPhase });
        _topology = await PartyMemoryRunnerInterruptionAudit.DiscoverAsync(_topology, captured =>
        {
            _topology = captured;
            WriteReceipt(captured, startup, true);
        }, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        JsonElement topology = JsonSerializer.SerializeToElement(_topology.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal)
            .Append(new KeyValuePair<string, object?>("HostLifetime", fixture.CurrentApplication?.Services
                .GetRequiredService<IHostLifetime>().GetType().FullName))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
        WriteReceipt(topology, observation, custodyOnly);

        void WriteReceipt(JsonElement captured, JsonElement observed, bool startupOnly)
        {
            string path = Path.Combine(_configuration.GetProperty("Directory").GetString()!, startupOnly ? "custody.json" : "checkpoint.json");
            string staging = path + ".pending";
            File.WriteAllText(staging, JsonSerializer.Serialize(new
            {
                Phase, Secret = _configuration.GetProperty("Secret").GetString(), Nonce = _configuration.GetProperty("Nonce").GetString(),
                RunnerPid = Environment.ProcessId, RunnerStart = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(Environment.ProcessId),
                CreatedUtc = DateTimeOffset.UtcNow, PrivateRoot = privateRoot, Topology = captured, Observation = observed,
                ExecutedFiles = File.Exists(Path.Combine(privateRoot, "executed-identities.json")) ? ReadBounded(Path.Combine(privateRoot, "executed-identities.json")) : JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
            }));
            File.Move(staging, path, overwrite: startupOnly);
        }
    }

    /// <summary>Rejects stale, foreign or changed-incarnation evidence before any signal is delivered.</summary>
    internal static void Validate(JsonElement checkpoint, string phase, string secret, string nonce, int pid, long start, string? session = null)
    {
        DateTimeOffset created = checkpoint.GetProperty("CreatedUtc").GetDateTimeOffset();
        if (checkpoint.GetProperty("Phase").GetString() != phase || checkpoint.GetProperty("Secret").GetString() != secret
            || checkpoint.GetProperty("Nonce").GetString() != nonce || checkpoint.GetProperty("RunnerPid").GetInt32() != pid
            || checkpoint.GetProperty("RunnerStart").GetInt64() != start || DateTimeOffset.UtcNow - created > TimeSpan.FromSeconds(15)
            || created > DateTimeOffset.UtcNow || PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid) != start
            || checkpoint.GetProperty("Observation").GetProperty("Phase").GetString() != phase
            || session is not null && checkpoint.GetProperty("Topology").GetProperty("Id").GetString() != session)
        { throw new InvalidDataException("Stale, foreign or changed runner checkpoint."); }
    }

    /// <summary>Rechecks persisted state after Docker/file I/O, then authorizes the exact stopped producer at held-pidfd delivery.</summary>
    internal static async Task SignalRunnerAsync(JsonElement checkpoint, string phase, string secret, string nonce, int pid, long start,
        string session, CancellationToken token)
    {
        string root = checkpoint.GetProperty("PrivateRoot").GetString()!;
        JsonElement config = ReadBounded(Path.Combine(root, "runtime.json"));
        JsonElement observation = checkpoint.GetProperty("Observation");
        JsonElement persisted = await PartyMemoryProducerRestartRuntime.ReadPersistedPhaseAsync(phase, observation.GetProperty("RequestEvidence"),
            config.GetProperty("ownershipRedisEndpoint").GetString()!, new Uri(config.GetProperty("daprHttpEndpoint").GetString()!),
            config.GetProperty("daprApiToken").GetString()!, config.GetProperty("ownershipGeneration").GetString()!,
            config.GetProperty("ownershipWriterExclusionEvidenceId").GetString()!, root, token).ConfigureAwait(false);
        JsonElement expected = observation.GetProperty("PersistedInterruption");
        foreach (string property in new[] { "Inventory", "Authority", "Provenance", "LegacyArtifacts", "DeletionReceiptPresent", "MemoryUnitPresent" })
        {
            if (persisted.GetProperty(property).GetRawText() != expected.GetProperty(property).GetRawText())
            { throw new InvalidDataException("Frozen persisted phase changed during final interruption audit: " + property); }
        }
        if (phase == "cross-source-authority" && persisted.GetProperty("OwnerSourceBaselines").GetProperty("BaselineSha256").GetString()
            != expected.GetProperty("OwnerSourceBaselines").GetProperty("BaselineSha256").GetString())
        { throw new InvalidDataException("Owned source baseline changed during final interruption audit."); }
        Validate(checkpoint, phase, secret, nonce, pid, start, session);
        token.ThrowIfCancellationRequested();
        await AuthorizeSignalAsync(pid, start, checkpoint.GetProperty("Topology").GetProperty("LineageMarker").GetString()!,
            observation, checkpoint.GetProperty("Topology").GetProperty("Marker").GetString()!, checkpoint.GetProperty("CreatedUtc").GetDateTimeOffset(), deliver: true, token).ConfigureAwait(false);
    }

    /// <summary>Opens the runner handle before checking current birth, marker, stopped producer and final expiry, with no slow audits afterward.</summary>
    internal static async Task AuthorizeSignalAsync(int runnerPid, long runnerStart, string lineage, JsonElement observation, string marker,
        DateTimeOffset created, bool deliver, CancellationToken token = default)
    {
        const string script = """
            import datetime,os,pathlib,signal,sys
            runner,birth,producer,producer_birth=map(int,sys.argv[1:5])
            marker,lineage,observed,expires,created,deliver=sys.argv[5:11]
            def identity(pid,expected,variable,value,stopped=False):
                raw=pathlib.Path('/proc/'+str(pid)+'/stat').read_text()
                fields=raw[raw.rfind(')')+2:].split()
                if fields[0] in ('Z','X') or int(fields[19])!=expected or (stopped and fields[0] not in ('T','t')):
                    raise RuntimeError('current owned stopped incarnation required')
                env=pathlib.Path('/proc/'+str(pid)+'/environ').read_bytes().split(b'\0')
                if (variable+'='+value).encode() not in env: raise RuntimeError('inherited ownership marker mismatch')
                again=pathlib.Path('/proc/'+str(pid)+'/stat').read_text()
                after=again[again.rfind(')')+2:].split()
                if after[0]!=fields[0] or after[19]!=fields[19]: raise RuntimeError('incarnation changed during final authorization')
            def instant(value): return datetime.datetime.fromisoformat(value.replace('Z','+00:00'))
            fd=os.pidfd_open(runner)
            producer_fd=os.pidfd_open(producer)
            try:
                identity(runner,birth,'PARTIES_TEST_RUNNER_OWNER',lineage)
                identity(producer,producer_birth,'PARTIES_TEST_TOPOLOGY_OWNER',marker,True)
                identity(producer,producer_birth,'PARTIES_TEST_TOPOLOGY_OWNER',marker,True)
                now=datetime.datetime.now(datetime.timezone.utc)
                if not (instant(observed)<=now<instant(expires)) or not (0<=(now-instant(observed)).total_seconds()<=15) or not (0<=(now-instant(created)).total_seconds()<=15):
                    raise RuntimeError('frozen phase expired before signal delivery')
                if deliver=='true': signal.pidfd_send_signal(fd,signal.SIGINT)
            finally:
                os.close(producer_fd)
                os.close(fd)
            """;
        var command = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string value in new[] { "-c", script, runnerPid.ToString(CultureInfo.InvariantCulture), runnerStart.ToString(CultureInfo.InvariantCulture),
            observation.GetProperty("ProducerPid").GetInt32().ToString(CultureInfo.InvariantCulture), observation.GetProperty("ProducerStart").GetInt64().ToString(CultureInfo.InvariantCulture),
            marker, lineage, observation.GetProperty("ObservedAtUtc").GetDateTimeOffset().ToString("O", CultureInfo.InvariantCulture),
            observation.GetProperty("ExpiresAtUtc").GetDateTimeOffset().ToString("O", CultureInfo.InvariantCulture), created.ToString("O", CultureInfo.InvariantCulture), deliver ? "true" : "false" })
        { command.ArgumentList.Add(value); }
        _ = await PartyMemoryRunnerInterruptionProcess.RunAsync(command, TimeSpan.FromSeconds(5), token: token).ConfigureAwait(false);
    }

    /// <summary>Reads only bounded JSON private handoff files.</summary>
    internal static JsonElement ReadBounded(string path)
    {
        if (new FileInfo(path).Length > 262144) { throw new InvalidDataException("Runner checkpoint exceeded its private bound."); }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.Clone();
    }
}
