using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.Memories.Contracts.V1;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Traces actual direct-owner calls and retains valid, unforced divergent-asset counterexamples in isolated children.</summary>
public sealed class PartyMemoryOwnerDependencyTests(ITestOutputHelper output)
{
    /// <summary>Exercises Options first, then a private transitive Redis asset without forcing either candidate into the call path.</summary>
    [Fact]
    public async Task ActualOwnerDependencyTraceQualifiesDivergentAssetCounterexamples()
    {
        string? server = Environment.GetEnvironmentVariable("PARTIES_MEMORY_OWNER_DEPENDENCY_SERVER");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(server), "Set PARTIES_MEMORY_OWNER_DEPENDENCY_SERVER to the prepared current-source owner assembly.");
        string root = Path.Combine(Path.GetTempPath(), "owner-dependency-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string child = Path.Combine(root, "child");
            string owner = Path.Combine(root, "owner");
            CopyDirectory(Path.GetDirectoryName(typeof(PartyMemoryOwnerDependencyTests).Assembly.Location)!, child);
            CopyDirectory(Path.GetDirectoryName(server!)!, owner);
            // This explicitly prepared source-owner cohort shares the original three boundary assets.
            foreach (string file in Directory.GetFiles(owner, "*.dll"))
                File.Copy(file, Path.Combine(child, Path.GetFileName(file)), overwrite: true);
            await RunChildAsync(child, owner, "matching");
            string frameworkPeer = Path.Combine(owner, "Microsoft.Extensions.Options.dll");
            File.Copy(typeof(OptionsWrapper<>).Assembly.Location, frameworkPeer);
            await File.AppendAllTextAsync(frameworkPeer, "\nvalid-unselected-framework-peer-overlay\n", TestContext.Current.CancellationToken);
            _ = AssemblyName.GetAssemblyName(frameworkPeer);
            await RunChildAsync(child, owner, "framework-refutation");
            File.Delete(frameworkPeer);
            string privatePeer = Path.Combine(owner, "System.IO.Hashing.dll");
            string identity = AssemblyName.GetAssemblyName(privatePeer).FullName;
            await File.AppendAllTextAsync(privatePeer, "\nvalid-private-pe-overlay-counterexample\n", TestContext.Current.CancellationToken);
            AssemblyName.GetAssemblyName(privatePeer).FullName.ShouldBe(identity);
            FileHash(privatePeer).ShouldNotBe(FileHash(Path.Combine(child, "System.IO.Hashing.dll")));
            await RunChildAsync(child, owner, "private-refutation");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Executes only in the isolated copy selected by the parent control.</summary>
    [Fact]
    public async Task RunDependencyChild()
    {
        string? mode = Environment.GetEnvironmentVariable("PARTIES_MEMORY_DEPENDENCY_CHILD_MODE");
        if (mode is null) return;
        string server = Path.Combine(Environment.GetEnvironmentVariable("PARTIES_MEMORY_DEPENDENCY_CHILD_OWNER")!, "Hexalith.Memories.Server.dll");
        using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(
            Environment.GetEnvironmentVariable("PARTIES_MEMORY_OWNER_DEPENDENCY_REDIS") ?? throw new InvalidOperationException("Owned Redis endpoint required."));
        await connection.GetDatabase().PingAsync();
        var before = AssemblyLoadContext.Default.Assemblies.Select(assembly => assembly.FullName).ToHashSet();
        var config = new PartyMemoryRuntimeConfiguration { OwnershipServerAssemblyPath = server };
        string tenant = "owned-dependency-" + Guid.NewGuid().ToString("N");
        string unit = "unit-" + Guid.NewGuid().ToString("N");
        var input = new IngestionInput { TenantId = tenant, CaseId = "case", SourceUri = "https://synthetic.invalid/dependency",
            SourceType = SourceType.Url, ContentType = "text/plain", IngestedBy = "owned-dependency-control" };
        await PartyMemoryOwnerSourceRuntime.WithServerAsync(config, async assembly =>
        {
            object store = PartyMemoryOwnerSourceRuntime.Store(assembly, connection, config);
            store.GetType().FullName.ShouldBe("Hexalith.Memories.Server.Ingestion.SourceInventoryStore");
            // The actual constructor consumes the already-loaded IOptions<T>.Value here.
            store.GetType().GetField("_ownershipSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store).ShouldNotBeNull();
            await PartyMemoryOwnerSourceRuntime.InvokeAsync(store, "AdmitAsync", [unit, input, TestContext.Current.CancellationToken, false]).ConfigureAwait(false);
            await PartyMemoryOwnerSourceRuntime.InvokeAsync(store, "RecordCandidateAsync", [tenant, input.SourceUri, unit, unit, TestContext.Current.CancellationToken]).ConfigureAwait(false);
            await PartyMemoryOwnerSourceRuntime.InvokeAsync(store, "ReadAsync", [tenant, input.SourceUri, null, 100, TestContext.Current.CancellationToken]).ConfigureAwait(false);
            Type discovery = assembly.GetType("Hexalith.Memories.Server.Ingestion.SemanticKeyDiscovery", throwOnError: true)!;
            var refused = await Should.ThrowAsync<InvalidOperationException>(() => PartyMemoryOwnerSourceRuntime.InvokeAsync(discovery, "FindAsync", [connection.GetDatabase(), tenant, unit, TestContext.Current.CancellationToken])).ConfigureAwait(false);
            refused.Message.ShouldBe("source-publication-legacy-owner-reconciliation-required");
        });
        var resolver = new AssemblyDependencyResolver(server);
        Assembly[] loaded = AssemblyLoadContext.Default.Assemblies.Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location)).OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
        object[] mismatches = loaded.Where(assembly => resolver.ResolveAssemblyToPath(assembly.GetName()) is { } selected && FileHash(selected) != FileHash(assembly.Location))
            .Select(assembly => (object)new { assembly.FullName, Loaded = FileHash(assembly.Location), Selected = FileHash(resolver.ResolveAssemblyToPath(assembly.GetName())!) }).ToArray();
        string optionsPath = typeof(OptionsWrapper<>).Assembly.Location;
        string privatePath = Path.Combine(Path.GetDirectoryName(server)!, "System.IO.Hashing.dll");
        object receipt = new { Mode = mode, ReceiptNonce = Environment.GetEnvironmentVariable("PARTIES_MEMORY_DEPENDENCY_CHILD_RECEIPT_NONCE"),
            Pid = Environment.ProcessId, StartTicks = ProcessBirth(Environment.ProcessId),
            Owner = new { Path = Label(server, server), Sha256 = FileHash(server) },
            ActualCalls = new[] { "SourceInventoryStore..ctor/IOptions.Value", "AdmitAsync", "RecordCandidateAsync", "ReadAsync", "SemanticKeyDiscovery.FindAsync/refusal" },
            Options = new { LoadedPath = Label(optionsPath, server), LoadedSha256 = FileHash(optionsPath), SelectedPrivateAsset = resolver.ResolveAssemblyToPath(typeof(OptionsWrapper<>).Assembly.GetName()),
                AttemptedPeerSha256 = File.Exists(Path.Combine(Path.GetDirectoryName(server)!, "Microsoft.Extensions.Options.dll")) ? FileHash(Path.Combine(Path.GetDirectoryName(server)!, "Microsoft.Extensions.Options.dll")) : null },
            PrivateCandidate = new { Name = "System.IO.Hashing", Identity = AssemblyName.GetAssemblyName(privatePath).FullName, Sha256 = FileHash(privatePath),
                ResolverSelectsCandidate = resolver.ResolveAssemblyToPath(new AssemblyName("System.IO.Hashing")) == privatePath,
                ActuallyLoaded = loaded.Any(assembly => assembly.GetName().Name == "System.IO.Hashing") },
            Mismatches = mismatches, ReuseDefectReproduced = mismatches.Length != 0,
            Loaded = loaded.Select(assembly => new { Identity = assembly.FullName, Path = Label(assembly.Location, server), Sha256 = FileHash(assembly.Location), ActivatedDuringOwnerCalls = !before.Contains(assembly.FullName),
                SelectedPath = resolver.ResolveAssemblyToPath(assembly.GetName()) is { } path ? Label(path, server) : null,
                SelectedSha256 = resolver.ResolveAssemblyToPath(assembly.GetName()) is { } selected ? FileHash(selected) : null }) };
        string json = JsonSerializer.Serialize(receipt);
        output.WriteLine(json);
        await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("PARTIES_MEMORY_DEPENDENCY_CHILD_RECEIPT")!, json, TestContext.Current.CancellationToken);
        mismatches.ShouldBeEmpty("Only a reachable mismatch on the executed owner path warrants a new pre-mutation guard.");
        resolver.ResolveAssemblyToPath(typeof(OptionsWrapper<>).Assembly.GetName()).ShouldBeNull();
        if (mode == "private-refutation")
        {
            resolver.ResolveAssemblyToPath(new AssemblyName("System.IO.Hashing")).ShouldBe(privatePath);
            loaded.ShouldNotContain(assembly => assembly.GetName().Name == "System.IO.Hashing");
        }
    }

    private async Task RunChildAsync(string child, string owner, string mode)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        string receipt = Path.Combine(child, mode + ".json");
        File.Exists(receipt).ShouldBeFalse("Each child must create a new receipt for this launch.");
        string receiptNonce = Guid.NewGuid().ToString("N");
        using var process = new Process { StartInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = child } };
        process.StartInfo.ArgumentList.Add(Path.Combine(child, "Hexalith.Parties.Tests.dll"));
        process.StartInfo.ArgumentList.Add("-method"); process.StartInfo.ArgumentList.Add("Hexalith.Parties.Tests.Search.PartyMemoryOwnerDependencyTests.RunDependencyChild");
        process.StartInfo.ArgumentList.Add("-failSkips");
        process.StartInfo.Environment["PARTIES_MEMORY_DEPENDENCY_CHILD_MODE"] = mode;
        process.StartInfo.Environment["PARTIES_MEMORY_DEPENDENCY_CHILD_OWNER"] = owner;
        process.StartInfo.Environment["PARTIES_MEMORY_DEPENDENCY_CHILD_RECEIPT"] = receipt;
        process.StartInfo.Environment["PARTIES_MEMORY_DEPENDENCY_CHILD_RECEIPT_NONCE"] = receiptNonce;
        process.Start().ShouldBeTrue();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            int observedPid = process.Id;
            long observedBirth = ProcessBirth(observedPid);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            process.ExitCode.ShouldBe(0, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
            string json = await File.ReadAllTextAsync(receipt, deadline.Token).ConfigureAwait(false);
            using JsonDocument captured = JsonDocument.Parse(json);
            captured.RootElement.GetProperty("Mode").GetString().ShouldBe(mode);
            captured.RootElement.GetProperty("ReceiptNonce").GetString().ShouldBe(receiptNonce);
            captured.RootElement.GetProperty("Pid").GetInt32().ShouldBe(observedPid);
            captured.RootElement.GetProperty("StartTicks").GetInt64().ShouldBe(observedBirth);
            captured.RootElement.GetProperty("ActualCalls").EnumerateArray().Select(item => item.GetString()).ShouldBe(new[]
                { "SourceInventoryStore..ctor/IOptions.Value", "AdmitAsync", "RecordCandidateAsync", "ReadAsync", "SemanticKeyDiscovery.FindAsync/refusal" });
            string parentReceipt = JsonSerializer.Serialize(new { Mode = mode, ReceiptNonce = receiptNonce,
                Pid = observedPid, StartTicks = observedBirth, ExitObserved = process.HasExited, process.ExitCode,
                ChildReceiptSha256 = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))) });
            output.WriteLine(json);
            output.WriteLine(parentReceipt);
            if (Environment.GetEnvironmentVariable("MEMORIES_ERASURE_RECEIPT_DIR") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "dependency-" + mode + ".json"), json, deadline.Token).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(directory, "dependency-" + mode + "-parent.json"), parentReceipt, deadline.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).WaitAsync(cleanup.Token).ConfigureAwait(false);
        }
    }

    private static long ProcessBirth(int pid)
    {
        if (!OperatingSystem.IsLinux())
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime().Ticks;
        }
        string stat = File.ReadAllText("/proc/" + pid + "/stat");
        return long.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19],
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Label(string path, string server)
    {
        if (path.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal)) return "${CHILD}/" + Path.GetRelativePath(AppContext.BaseDirectory, path);
        string owner = Path.GetDirectoryName(server)! + Path.DirectorySeparatorChar;
        if (path.StartsWith(owner, StringComparison.Ordinal)) return "${OWNER}/" + Path.GetRelativePath(owner, path);
        int shared = path.IndexOf("/shared/", StringComparison.Ordinal);
        return shared >= 0 ? "${DOTNET}" + path[shared..] : Path.GetFileName(path);
    }
    private static string FileHash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
}
