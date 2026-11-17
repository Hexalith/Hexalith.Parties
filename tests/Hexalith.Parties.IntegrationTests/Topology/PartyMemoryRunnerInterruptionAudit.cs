using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Independently checks exact kernel and Docker identities outside the interrupted runner.</summary>
internal static class PartyMemoryRunnerInterruptionAudit
{
    /// <summary>Names inherited runner lineage, assigned before the runner starts, including DCP and helpers.</summary>
    internal const string LineageVariable = "PARTIES_TEST_RUNNER_OWNER";
    private const string OwnerLabel = "hexalith.parties.test.owner";
    private const string SessionLabel = "hexalith.parties.test.session";

    /// <summary>Issues immutable external authority before any runner or backing-resource creation.</summary>
    internal static JsonElement Authorize(string id, string root, string marker, string lineage)
        => JsonSerializer.SerializeToElement(new
        {
            Id = id, Root = root, Marker = marker, LineageMarker = lineage,
            Processes = Array.Empty<object>(), LiveProcesses = Array.Empty<object>(), OwnedWorkers = Array.Empty<object>(), Containers = Array.Empty<object>(),
            Networks = Array.Empty<object>(), AnonymousVolumes = Array.Empty<object>(),
        });

    /// <summary>Finds only exact inherited markers and immutable labels, retaining every previously captured identity.</summary>
    internal static async Task<JsonElement> DiscoverAsync(JsonElement authority, Action<JsonElement>? publish = null,
        CancellationToken token = default, Func<CancellationToken, string[], Task<string>>? command = null)
    {
        using var scan = CancellationTokenSource.CreateLinkedTokenSource(token);
        scan.CancelAfter(TimeSpan.FromSeconds(30));
        token = scan.Token;
        string marker = authority.GetProperty("Marker").GetString()!, session = authority.GetProperty("Id").GetString()!;
        string lineage = authority.GetProperty("LineageMarker").GetString()!;
        var processes = authority.GetProperty("Processes").EnumerateArray()
            .Select(item => (Pid: item.GetProperty("Pid").GetInt32(), Start: item.GetProperty("Start").GetInt64())).ToHashSet();
        var workers = authority.GetProperty("OwnedWorkers").EnumerateArray()
            .Select(item => (Pid: item.GetProperty("Pid").GetInt32(), Start: item.GetProperty("Start").GetInt64())).ToHashSet();
        var current = new HashSet<(int Pid, long Start)>();
        var containers = authority.GetProperty("Containers").EnumerateArray().ToDictionary(item => item.GetProperty("Id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        var networks = authority.GetProperty("Networks").EnumerateArray().ToDictionary(item => item.GetProperty("Id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        var volumes = authority.GetProperty("AnonymousVolumes").EnumerateArray().ToDictionary(item => item.GetProperty("Name").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        JsonElement Snapshot() => JsonSerializer.SerializeToElement(new
        {
            Id = session, Root = authority.GetProperty("Root").GetString(), Marker = marker, LineageMarker = lineage,
            Processes = processes.OrderBy(item => item.Pid).ThenBy(item => item.Start).Select(item => new { item.Pid, item.Start }),
            LiveProcesses = current.OrderBy(item => item.Pid).ThenBy(item => item.Start).Select(item => new { item.Pid, item.Start }),
            OwnedWorkers = workers.OrderBy(item => item.Pid).ThenBy(item => item.Start).Select(item => new { item.Pid, item.Start }),
            Containers = containers.OrderBy(item => item.Key).Select(item => item.Value), Networks = networks.OrderBy(item => item.Key).Select(item => item.Value),
            AnonymousVolumes = volumes.OrderBy(item => item.Key).Select(item => item.Value),
        });
        void Publish() => publish?.Invoke(Snapshot());
        _ = OwnedProcesses(PartiesAspireTopologyFixture.WorkerOwnershipVariable, marker, token, (pid, birth) =>
        { workers.Add((pid, birth)); processes.Add((pid, birth)); current.Add((pid, birth)); Publish(); });
        _ = OwnedProcesses(LineageVariable, lineage, token, (pid, birth) => { processes.Add((pid, birth)); current.Add((pid, birth)); Publish(); });
        foreach (string id in Lines(await ExecuteAsync(command, token, "ps", "-aq", "--no-trunc", "--filter", "label=" + OwnerLabel + "=" + marker).ConfigureAwait(false)))
        {
            JsonElement? inspected = await InspectOrAbsentAsync(() => InspectContainerAsync(id, token, command), command, token, id,
                ["ps", "-aq", "--no-trunc"]).ConfigureAwait(false);
            if (inspected is null) { continue; }
            JsonElement container = inspected.Value;
            RequireLabels(container.GetProperty("Labels"), marker, session);
            if (containers.TryGetValue(id, out JsonElement before) && !SameContainerIdentity(before, container))
            { throw new InvalidDataException("An externally owned container changed immutable identity."); }
            containers[id] = container;
            Publish();
            foreach (JsonElement volume in container.GetProperty("AnonymousVolumes").EnumerateArray())
            {
                string name = volume.GetString()!;
                JsonElement? ownedVolume = await InspectOrAbsentAsync(async () => ProjectVolume(JsonSerializer.Deserialize<JsonElement>(
                    await ExecuteAsync(command, token, "volume", "inspect", name).ConfigureAwait(false))[0]), command, token, name,
                    ["volume", "ls", "-q"]).ConfigureAwait(false);
                if (ownedVolume is null) { continue; }
                if (volumes.TryGetValue(name, out JsonElement previous) && previous.GetRawText() != ownedVolume.Value.GetRawText())
                { throw new InvalidDataException("Owned anonymous-volume identity changed."); }
                volumes[name] = ownedVolume.Value;
                Publish();
            }
        }
        // A previously authenticated container may disappear before its volume's first full inspection.
        // Its already-published mount identity still authorizes exact inspection of that anonymous volume.
        foreach (string name in containers.Values.SelectMany(container => container.GetProperty("AnonymousVolumes").EnumerateArray()
            .Select(volume => volume.GetString()!)).Distinct(StringComparer.Ordinal).Where(name => !volumes.ContainsKey(name)))
        {
            JsonElement? ownedVolume = await InspectOrAbsentAsync(async () => ProjectVolume(JsonSerializer.Deserialize<JsonElement>(
                await ExecuteAsync(command, token, "volume", "inspect", name).ConfigureAwait(false))[0]), command, token, name,
                ["volume", "ls", "-q"]).ConfigureAwait(false);
            if (ownedVolume is not null) { volumes[name] = ownedVolume.Value; Publish(); }
        }
        foreach (string id in Lines(await ExecuteAsync(command, token, "network", "ls", "-q", "--no-trunc", "--filter", "label=" + OwnerLabel + "=" + marker).ConfigureAwait(false)))
        {
            JsonElement? inspected = await InspectOrAbsentAsync(() => InspectNetworkAsync(id, token, command), command, token, id,
                ["network", "ls", "-q", "--no-trunc"]).ConfigureAwait(false);
            if (inspected is null) { continue; }
            JsonElement network = inspected.Value;
            RequireLabels(network.GetProperty("Labels"), marker, session);
            if (networks.TryGetValue(id, out JsonElement before) && !SameNetworkIdentity(before, network))
            { throw new InvalidDataException("An externally owned network changed immutable identity."); }
            networks[id] = network;
            Publish();
        }
        token.ThrowIfCancellationRequested();
        return Snapshot();
    }

    private static async Task<JsonElement?> InspectOrAbsentAsync(Func<Task<JsonElement>> inspect,
        Func<CancellationToken, string[], Task<string>>? command, CancellationToken token, string identity, string[] list)
        => await ExecuteOrAbsentAsync(inspect, command, token, identity, list).ConfigureAwait(false);

    private static async Task<T?> ExecuteOrAbsentAsync<T>(Func<Task<T>> operation,
        Func<CancellationToken, string[], Task<string>>? command, CancellationToken token, string identity, string[] list) where T : struct
    {
        try { return await operation().ConfigureAwait(false); }
        catch (Exception failure) when (!token.IsCancellationRequested)
        {
            // An exact fresh absence query distinguishes asynchronous disposal from an inspection failure.
            string[] current = Lines(await ExecuteAsync(command, token, list).ConfigureAwait(false));
            if (current.Contains(identity, StringComparer.Ordinal))
            { throw new InvalidDataException("Owned object operation failed while its exact identity remains present.", failure); }
            return null;
        }
    }

    /// <summary>Rechecks live backing state and network attachments separately from immutable recovery identity.</summary>
    internal static async Task<JsonElement> ValidateLiveAsync(JsonElement checkpoint, CancellationToken token = default)
    {
        using var scan = CancellationTokenSource.CreateLinkedTokenSource(token);
        scan.CancelAfter(TimeSpan.FromSeconds(30));
        token = scan.Token;
        JsonElement topology = checkpoint.GetProperty("Topology");
        Dictionary<int, long> live = OwnedProcesses(PartiesAspireTopologyFixture.WorkerOwnershipVariable, topology.GetProperty("Marker").GetString()!, token);
        foreach (JsonElement process in topology.GetProperty("LiveProcesses").EnumerateArray())
        {
            if (!PartiesAspireTopologyFixture.IsWorkerRunning(process.GetProperty("Pid").GetInt32(), process.GetProperty("Start").GetInt64()))
            { throw new InvalidDataException("A captured runner descendant changed before interruption."); }
        }
        foreach (JsonElement container in topology.GetProperty("Containers").EnumerateArray())
        {
            JsonElement current = await InspectContainerAsync(container.GetProperty("Id").GetString()!, token).ConfigureAwait(false);
            RequireLiveContainer(container, current);
        }
        foreach (JsonElement network in topology.GetProperty("Networks").EnumerateArray())
        {
            JsonElement current = await InspectNetworkAsync(network.GetProperty("Id").GetString()!, token).ConfigureAwait(false);
            if (!SameNetworkIdentity(network, current) || network.GetProperty("Containers").GetRawText() != current.GetProperty("Containers").GetRawText())
            { throw new InvalidDataException("Live network identity or attachments changed before interruption."); }
        }
        foreach (JsonProperty file in checkpoint.GetProperty("ExecutedFiles").EnumerateObject().Concat(checkpoint.GetProperty("Observation").GetProperty("ProducerFiles").EnumerateObject()))
        { token.ThrowIfCancellationRequested(); if (MemoryRuntimeIdentity.Hash(file.Name) != file.Value.GetString()) { throw new InvalidDataException("Executed bytes changed before interruption."); } }
        bool pending = RequireReplacement(checkpoint.GetProperty("Phase").GetString()!, checkpoint.GetProperty("Observation"), live);
        return JsonSerializer.SerializeToElement(new { LiveCustodyVerified = true, ExecutedBytesVerified = true, PendingReplacementVerified = pending, ObservedAtUtc = DateTimeOffset.UtcNow });
    }

    /// <summary>Refuses absent/null admitted replacement evidence instead of treating it as ordinary observation.</summary>
    internal static bool RequireReplacement(string phase, JsonElement observation, IReadOnlyDictionary<int, long> live)
    {
        bool present = observation.TryGetProperty("PendingReplacement", out JsonElement pending) && pending.ValueKind != JsonValueKind.Null;
        if (phase == "admitted-empty-mappings" && !present) { throw new InvalidDataException("Admitted interruption requires actual pending replacement proof."); }
        if (!present) { return false; }
        int original = pending.GetProperty("OriginalPid").GetInt32(), replacement = pending.GetProperty("ReplacementPid").GetInt32();
        long birth = pending.GetProperty("OriginalStart").GetInt64(), replacementBirth = pending.GetProperty("ReplacementStart").GetInt64();
        if (!observation.TryGetProperty("FrozenStartupProof", out JsonElement startup) || startup.ValueKind != JsonValueKind.Object
            || startup.GetProperty("Pid").GetInt32() != replacement || startup.GetProperty("Start").GetInt64() != replacementBirth
            || !startup.GetProperty("KernelStopped").GetBoolean() || !startup.GetProperty("OwnedListeningSocketsAbsent").GetBoolean())
        { throw new InvalidDataException("Pending replacement lacks a conservative stopped-incarnation startup proof."); }
        if (!pending.GetProperty("OriginalExited").GetBoolean() || !pending.GetProperty("BeforeHealthyStartup").GetBoolean()
            || PartiesAspireTopologyFixture.IsWorkerRunning(original, birth) || original == replacement && birth == replacementBirth
            || !live.TryGetValue(replacement, out long actual) || actual != replacementBirth
            || observation.GetProperty("ProducerPid").GetInt32() != replacement || observation.GetProperty("ProducerStart").GetInt64() != replacementBirth)
        { throw new InvalidDataException("Pending replacement lacks original exit and a distinct owned frozen incarnation."); }
        return true;
    }

    /// <summary>Requires every captured and late owned identity, anonymous volume and session path to be absent.</summary>
    internal static async Task<JsonElement> VerifyClosedAsync(JsonElement topology, Func<CancellationToken, string[], Task<string>>? command = null)
    {
        string[] containers = Lines(await ExecuteAsync(command, CancellationToken.None, "ps", "-aq", "--no-trunc").ConfigureAwait(false));
        string[] networks = Lines(await ExecuteAsync(command, CancellationToken.None, "network", "ls", "-q", "--no-trunc").ConfigureAwait(false));
        string[] volumes = Lines(await ExecuteAsync(command, CancellationToken.None, "volume", "ls", "-q").ConfigureAwait(false));
        var processes = topology.GetProperty("Processes").EnumerateArray().Select(process => new
        {
            Pid = process.GetProperty("Pid").GetInt32(), Start = process.GetProperty("Start").GetInt64(),
            Exited = !IsCapturedProcessPresent(process.GetProperty("Pid").GetInt32(), process.GetProperty("Start").GetInt64()),
        }).ToArray();
        string[] remainingContainers = topology.GetProperty("Containers").EnumerateArray().Select(item => item.GetProperty("Id").GetString()!).Where(containers.Contains).ToArray();
        string[] remainingNetworks = topology.GetProperty("Networks").EnumerateArray().Select(item => item.GetProperty("Id").GetString()!).Where(networks.Contains).ToArray();
        string[] remainingVolumes = topology.GetProperty("AnonymousVolumes").EnumerateArray().Select(item => item.GetProperty("Name").GetString()!).Where(volumes.Contains).ToArray();
        string marker = topology.GetProperty("Marker").GetString()!, lineage = topology.GetProperty("LineageMarker").GetString()!;
        int late = OwnedProcesses(PartiesAspireTopologyFixture.WorkerOwnershipVariable, marker).Count + (lineage.Length == 0 ? 0 : OwnedProcesses(LineageVariable, lineage).Count);
        string lateContainers = await ExecuteAsync(command, CancellationToken.None, "ps", "-aq", "--filter", "label=" + OwnerLabel + "=" + marker).ConfigureAwait(false);
        string lateNetworks = await ExecuteAsync(command, CancellationToken.None, "network", "ls", "-q", "--filter", "label=" + OwnerLabel + "=" + marker).ConfigureAwait(false);
        bool directory = Directory.Exists(topology.GetProperty("Root").GetString()!);
        return JsonSerializer.SerializeToElement(new
        {
            Closed = processes.All(item => item.Exited) && remainingContainers.Length == 0 && remainingNetworks.Length == 0 && remainingVolumes.Length == 0
                && late == 0 && !directory && string.IsNullOrWhiteSpace(lateContainers) && string.IsNullOrWhiteSpace(lateNetworks),
            Processes = processes, RemainingContainerIds = remainingContainers, RemainingNetworkIds = remainingNetworks,
            RemainingAnonymousVolumeNames = remainingVolumes, LateOwnedWorkers = late, SessionDirectoryExists = directory,
        });
    }

    /// <summary>Quiesces externally authenticated lineages before removing backing resources; never grants qualification.</summary>
    internal static async Task RecoverAsync(JsonElement authority, Action<JsonElement>? publish = null, Func<CancellationToken, string[], Task<string>>? command = null)
    {
        JsonElement topology = authority;
        var failures = new List<Exception>();
        void Capture(JsonElement captured) { topology = captured; publish?.Invoke(captured); }
        try { topology = await DiscoverAsync(topology, Capture, command: command).ConfigureAwait(false); }
        catch (Exception failure) { failures.Add(failure); }
        // Discovery failure cannot bypass quiescence of already established identities.
        // Retain fresh marker-bound workers as well, even when Docker is unavailable.
        Dictionary<int, long> fresh = OwnedProcesses(PartiesAspireTopologyFixture.WorkerOwnershipVariable, topology.GetProperty("Marker").GetString()!);
        foreach ((int pid, long birth) in OwnedProcesses(LineageVariable, topology.GetProperty("LineageMarker").GetString()!)) { fresh[pid] = birth; }
        var known = topology.GetProperty("Processes").EnumerateArray()
            .Select(item => (Pid: item.GetProperty("Pid").GetInt32(), Start: item.GetProperty("Start").GetInt64()))
            .Concat(fresh.Select(item => (Pid: item.Key, Start: item.Value))).Distinct().ToArray();
        foreach ((int pid, long birth) in known)
        {
            try { await PartyMemoryProducerRestartRuntime.QuiesceProcessAsync(pid, birth).ConfigureAwait(false); }
            catch (Exception failure) { failures.Add(failure); }
        }
        string marker = topology.GetProperty("Marker").GetString()!, lineage = topology.GetProperty("LineageMarker").GetString()!;
        if (OwnedProcesses(PartiesAspireTopologyFixture.WorkerOwnershipVariable, marker).Count != 0 || lineage.Length != 0 && OwnedProcesses(LineageVariable, lineage).Count != 0)
        { throw new InvalidOperationException("Owned workers survived emergency quiescence; backing retained."); }
        if (known.Any(process => PartiesAspireTopologyFixture.IsWorkerRunning(process.Pid, process.Start)))
        { throw new AggregateException("Known workers survived emergency quiescence; backing retained.", failures); }
        // No worker can create another backing resource after this point. A failed fresh discovery retains backing.
        try { topology = await DiscoverAsync(topology, Capture, command: command).ConfigureAwait(false); }
        catch (Exception failure) { failures.Add(failure); throw new AggregateException("Fresh recovery inventory failed after owned workers were quiesced; backing retained.", failures); }
        string[] ids = Lines(await ExecuteAsync(command, CancellationToken.None, "ps", "-aq", "--no-trunc").ConfigureAwait(false));
        foreach (JsonElement container in topology.GetProperty("Containers").EnumerateArray())
        {
            string id = container.GetProperty("Id").GetString()!;
            if (!ids.Contains(id)) { continue; }
            JsonElement? current = await InspectOrAbsentAsync(() => InspectContainerAsync(id, command: command), command, CancellationToken.None,
                id, ["ps", "-aq", "--no-trunc"]).ConfigureAwait(false);
            if (current is null) { continue; }
            if (!SameContainerIdentity(container, current.Value)) { throw new InvalidOperationException("Emergency immutable container custody changed; resource retained."); }
            _ = await ExecuteOrAbsentAsync(async () =>
            {
                _ = await ExecuteAsync(command, CancellationToken.None, "rm", "-f", "-v", id).ConfigureAwait(false);
                return true;
            }, command, CancellationToken.None, id, ["ps", "-aq", "--no-trunc"]).ConfigureAwait(false);
        }
        string[] volumes = Lines(await ExecuteAsync(command, CancellationToken.None, "volume", "ls", "-q").ConfigureAwait(false));
        foreach (JsonElement volume in topology.GetProperty("AnonymousVolumes").EnumerateArray())
        {
            string name = volume.GetProperty("Name").GetString()!;
            if (!volumes.Contains(name)) { continue; }
            JsonElement? current = await InspectOrAbsentAsync(async () => ProjectVolume(JsonSerializer.Deserialize<JsonElement>(
                await ExecuteAsync(command, CancellationToken.None, "volume", "inspect", name).ConfigureAwait(false))[0]), command, CancellationToken.None,
                name, ["volume", "ls", "-q"]).ConfigureAwait(false);
            if (current is null) { continue; }
            if (current.Value.GetRawText() != volume.GetRawText()) { throw new InvalidOperationException("Anonymous volume identity changed; retained."); }
            _ = await ExecuteOrAbsentAsync(async () =>
            {
                _ = await ExecuteAsync(command, CancellationToken.None, "volume", "rm", name).ConfigureAwait(false);
                return true;
            }, command, CancellationToken.None, name, ["volume", "ls", "-q"]).ConfigureAwait(false);
        }
        string[] networks = Lines(await ExecuteAsync(command, CancellationToken.None, "network", "ls", "-q", "--no-trunc").ConfigureAwait(false));
        foreach (JsonElement network in topology.GetProperty("Networks").EnumerateArray())
        {
            string id = network.GetProperty("Id").GetString()!;
            if (!networks.Contains(id)) { continue; }
            JsonElement? current = await InspectOrAbsentAsync(() => InspectNetworkAsync(id, command: command), command, CancellationToken.None,
                id, ["network", "ls", "-q", "--no-trunc"]).ConfigureAwait(false);
            if (current is null) { continue; }
            if (!SameNetworkIdentity(network, current.Value) || current.Value.GetProperty("Containers").EnumerateObject().Any())
            { throw new InvalidOperationException("Emergency network identity changed or has attachments; retained."); }
            _ = await ExecuteOrAbsentAsync(async () =>
            {
                _ = await ExecuteAsync(command, CancellationToken.None, "network", "rm", id).ConfigureAwait(false);
                return true;
            }, command, CancellationToken.None, id, ["network", "ls", "-q", "--no-trunc"]).ConfigureAwait(false);
        }
        string root = topology.GetProperty("Root").GetString()!;
        if (Path.GetFileName(root) != "hexalith-parties-topology-" + topology.GetProperty("Id").GetString() || new DirectoryInfo(root).LinkTarget is not null)
        { throw new InvalidDataException("Emergency session path is not bound to its identity."); }
        if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        if (failures.Count != 0) { throw new AggregateException("Emergency cleanup completed with retained discovery or quiescence failures.", failures); }
    }

    private static bool IsCapturedProcessPresent(int pid, long birth)
    {
        if (!OperatingSystem.IsLinux()) { return PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth); }
        try
        {
            string stat = File.ReadAllText($"/proc/{pid}/stat");
            string[] fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // A matching zombie/dead task still retains its captured kernel lifetime until actual reaping.
            return long.Parse(fields[19], CultureInfo.InvariantCulture) == birth;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static Dictionary<int, long> OwnedProcesses(string variable, string marker, CancellationToken token = default, Action<int, long>? capture = null)
    {
        var result = new Dictionary<int, long>();
        if (marker.Length == 0) { return result; }
        foreach (string path in Directory.EnumerateDirectories("/proc"))
        {
            token.ThrowIfCancellationRequested();
            if (!int.TryParse(Path.GetFileName(path), out int pid) || pid == Environment.ProcessId) { continue; }
            long? captured = null;
            try
            {
                long? start = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid);
                if (start is not null && File.ReadAllText(Path.Combine(path, "environ")).Split('\0').Contains(variable + "=" + marker, StringComparer.Ordinal)
                    && PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid) == start) { captured = start; }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            // Only transient procfs reads are ignorable. A durable publisher failure must stop the scan.
            if (captured is long birth) { result.Add(pid, birth); capture?.Invoke(pid, birth); }
        }
        return result;
    }

    /// <summary>Checks immutable container identity separately from live running state and attachment state.</summary>
    internal static void RequireLiveContainer(JsonElement expected, JsonElement actual)
    {
        if (!SameContainerIdentity(expected, actual) || !actual.GetProperty("Running").GetBoolean()
            || expected.GetProperty("Networks").GetRawText() != actual.GetProperty("Networks").GetRawText())
        { throw new InvalidDataException("Backing identity, running state or attachments changed before interruption."); }
    }

    private static bool SameContainerIdentity(JsonElement left, JsonElement right) => new[] { "Id", "Name", "Image", "Created", "Labels", "Mounts", "AnonymousVolumes" }
        .All(name => left.GetProperty(name).GetRawText() == right.GetProperty(name).GetRawText());
    private static bool SameNetworkIdentity(JsonElement left, JsonElement right) => new[] { "Id", "Name", "Driver", "Labels" }
        .All(name => left.GetProperty(name).GetRawText() == right.GetProperty(name).GetRawText());
    private static void RequireLabels(JsonElement labels, string marker, string session)
    {
        if (labels.GetProperty(OwnerLabel).GetString() != marker || labels.GetProperty(SessionLabel).GetString() != session)
        { throw new InvalidDataException("External backing-resource authority does not match this session."); }
    }
    private static string[] Lines(string value) => value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static async Task<JsonElement> InspectNetworkAsync(string id, CancellationToken token = default, Func<CancellationToken, string[], Task<string>>? command = null)
    {
        JsonElement network = JsonSerializer.Deserialize<JsonElement>(await ExecuteAsync(command, token, "network", "inspect", id).ConfigureAwait(false))[0];
        return JsonSerializer.SerializeToElement(new { Id = network.GetProperty("Id"), Name = network.GetProperty("Name"), Driver = network.GetProperty("Driver"),
            Labels = network.GetProperty("Labels"), Containers = network.GetProperty("Containers") });
    }
    private static JsonElement ProjectVolume(JsonElement volume) => JsonSerializer.SerializeToElement(new
    {
        Name = volume.GetProperty("Name"), Driver = volume.GetProperty("Driver"), Mountpoint = volume.GetProperty("Mountpoint"),
        CreatedAt = volume.GetProperty("CreatedAt"), Labels = volume.GetProperty("Labels"), Options = volume.GetProperty("Options"), Scope = volume.GetProperty("Scope"),
    });
    private static async Task<JsonElement> InspectContainerAsync(string id, CancellationToken token = default, Func<CancellationToken, string[], Task<string>>? command = null)
    {
        JsonElement container = JsonSerializer.Deserialize<JsonElement>(await ExecuteAsync(command, token, "inspect", id).ConfigureAwait(false))[0];
        return ProjectContainer(container);
    }

    /// <summary>Identifies only implicit image-declared anonymous volumes, excluding all explicit bind/mount declarations.</summary>
    internal static JsonElement ProjectContainer(JsonElement container)
    {
        JsonElement host = container.GetProperty("HostConfig"), config = container.GetProperty("Config");
        string[] anonymous = container.GetProperty("Mounts").EnumerateArray().Where(mount => mount.GetProperty("Type").GetString() == "volume")
            .Where(mount => mount.TryGetProperty("Name", out JsonElement name) && name.GetString() is { Length: 64 } value && value.All(char.IsAsciiHexDigit))
            .Where(mount => config.TryGetProperty("Volumes", out JsonElement declared) && declared.ValueKind == JsonValueKind.Object && declared.TryGetProperty(mount.GetProperty("Destination").GetString()!, out _))
            .Where(mount => !host.TryGetProperty("Binds", out JsonElement binds) || binds.ValueKind == JsonValueKind.Null || !binds.EnumerateArray().Any(bind => bind.GetString()!.Split(':').Skip(1).Contains(mount.GetProperty("Destination").GetString())))
            .Where(mount => !host.TryGetProperty("Mounts", out JsonElement mounts) || mounts.ValueKind == JsonValueKind.Null || !mounts.EnumerateArray().Any(item => item.GetProperty("Target").GetString() == mount.GetProperty("Destination").GetString()))
            .Select(mount => mount.GetProperty("Name").GetString()!).Order(StringComparer.Ordinal).ToArray();
        return JsonSerializer.SerializeToElement(new
        {
            Id = container.GetProperty("Id"), Name = container.GetProperty("Name"), Image = container.GetProperty("Image"), Created = container.GetProperty("Created"),
            Labels = config.GetProperty("Labels"), Mounts = container.GetProperty("Mounts"), AnonymousVolumes = anonymous,
            Running = container.GetProperty("State").GetProperty("Running"), Networks = container.GetProperty("NetworkSettings").GetProperty("Networks"),
        });
    }

    private static Task<string> ExecuteAsync(Func<CancellationToken, string[], Task<string>>? command, CancellationToken token, params string[] arguments)
    {
        token.ThrowIfCancellationRequested();
        return command is null ? DockerAsync(token, arguments) : command(token, arguments);
    }

    /// <summary>Runs Docker with independent execution, reap and drain budgets.</summary>
    internal static Task<string> DockerAsync(params string[] arguments) => DockerAsync(CancellationToken.None, arguments);

    /// <summary>Passes the shared aggregate scan cancellation into execution; cleanup retains independent budgets.</summary>
    internal static Task<string> DockerAsync(CancellationToken token, params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        return PartyMemoryRunnerInterruptionProcess.RunAsync(start, TimeSpan.FromSeconds(10), token: token);
    }
}
