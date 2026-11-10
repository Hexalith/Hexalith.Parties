extern alias apphost;

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

using Shouldly;

using CatalogTool = apphost::Hexalith.Parties.AppHost.PartiesDevelopmentCatalogTool;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises the actual preparation ownership boundary on success, failure and foreign directories.</summary>
public sealed class DevelopmentCatalogPreparationCleanupTests
{
    /// <summary>Competing preparations never replace the successful run's marker or delete its files.</summary>
    [Fact]
    public async Task ConcurrentPreparationsPreserveTheWinningOwner()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        string? winner = null;
        using var barrier = new Barrier(8);
        try
        {
            Task[] attempts = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
            {
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                string owner = "run-" + index;
                try
                {
                    await CatalogTool.RunOwnedPreparationAsync(path, owner, () =>
                    {
                        Interlocked.CompareExchange(ref winner, owner, null).ShouldBeNull();
                        File.WriteAllText(Path.Combine(path, "retained"), owner);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }
                catch (Exception failure) when (failure is IOException or InvalidDataException) { }
            }, TestContext.Current.CancellationToken)).ToArray();
            await Task.WhenAll(attempts).ConfigureAwait(true);
            winner.ShouldNotBeNull();
            File.ReadAllText(Path.Combine(path, ".owner")).ShouldBe(winner);
            File.ReadAllText(Path.Combine(path, "retained")).ShouldBe(winner);
            Directory.EnumerateDirectories(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".preparing-*").ShouldBeEmpty();
        }
        finally
        {
            if (winner is not null) { CatalogTool.DeleteOwnedDirectory(path, winner); }
        }
    }

    /// <summary>Failed marker initialization never admits preparation and removes its partial private staging files.</summary>
    /// <param name="partialWrite">Whether the marker writer persisted partial bytes before failing.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedMarkerInitializationRemovesOnlyItsPrivateStaging(bool partialWrite)
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-marker-" + Guid.NewGuid().ToString("N"));
        bool entered = false;
        IOException expected = new("controlled marker initialization failure");
        IOException actual = await Should.ThrowAsync<IOException>(() => CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
        {
            entered = true;
            return Task.CompletedTask;
        }, (marker, owner) =>
        {
            if (partialWrite)
            {
                marker.Write(owner[..4]);
                marker.Flush();
            }
            throw expected;
        })).ConfigureAwait(true);
        actual.ShouldBeSameAs(expected);
        entered.ShouldBeFalse();
        Directory.Exists(path).ShouldBeFalse();
        Directory.EnumerateDirectories(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".preparing-*").ShouldBeEmpty();
        try
        {
            await CatalogTool.RunOwnedPreparationAsync(path, "retry", () => Task.CompletedTask).ConfigureAwait(true);
            File.ReadAllText(Path.Combine(path, ".owner")).ShouldBe("retry");
        }
        finally { CatalogTool.DeleteOwnedDirectory(path, "retry"); }
    }

    /// <summary>A file claiming the destination during marker initialization survives failed admission unchanged.</summary>
    [Fact]
    public async Task DestinationClaimedDuringInitializationPreservesTheForeignFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-marker-" + Guid.NewGuid().ToString("N"));
        bool entered = false;
        try
        {
            await Should.ThrowAsync<IOException>(() => CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
            {
                entered = true;
                return Task.CompletedTask;
            }, (marker, owner) =>
            {
                marker.Write(owner);
                File.WriteAllText(path, "foreign-owner-value");
            })).ConfigureAwait(true);
            entered.ShouldBeFalse();
            File.ReadAllText(path).ShouldBe("foreign-owner-value");
            Directory.EnumerateDirectories(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".preparing-*").ShouldBeEmpty();
        }
        finally { File.Delete(path); }
    }

    /// <summary>A directory claiming the destination during marker initialization retains its foreign marker and files.</summary>
    [Fact]
    public async Task DestinationClaimedDuringInitializationPreservesTheForeignDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-marker-" + Guid.NewGuid().ToString("N"));
        bool entered = false;
        try
        {
            await Should.ThrowAsync<IOException>(() => CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
            {
                entered = true;
                return Task.CompletedTask;
            }, (marker, owner) =>
            {
                marker.Write(owner);
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, ".owner"), "foreign-run");
                File.WriteAllText(Path.Combine(path, "retained"), "foreign-owner-value");
            })).ConfigureAwait(true);
            entered.ShouldBeFalse();
            File.ReadAllText(Path.Combine(path, ".owner")).ShouldBe("foreign-run");
            File.ReadAllText(Path.Combine(path, "retained")).ShouldBe("foreign-owner-value");
            Directory.EnumerateDirectories(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".preparing-*").ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); }
        }
    }

    /// <summary>Cleanup losing ownership preserves foreign contents and retains both failures without replacing the primary one.</summary>
    [Fact]
    public async Task CleanupRefusalPreservesThePrimaryFailureAndForeignContents()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        InvalidOperationException expected = new("controlled preparation failure");
        try
        {
            InvalidOperationException actual = await Should.ThrowAsync<InvalidOperationException>(() =>
                CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
                {
                    File.WriteAllText(Path.Combine(path, ".owner"), "foreign-run");
                    File.WriteAllText(Path.Combine(path, "retained"), "foreign-owner-value");
                    throw expected;
                })).ConfigureAwait(true);
            actual.ShouldBeSameAs(expected);
            actual.Data["CatalogOwnedDirectoryCleanupFailure"].ShouldBeOfType<InvalidDataException>()
                .Message.ShouldContain("foreign preparation directory");
            File.ReadAllText(Path.Combine(path, ".owner")).ShouldBe("foreign-run");
            File.ReadAllText(Path.Combine(path, "retained")).ShouldBe("foreign-owner-value");
        }
        finally
        {
            if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); }
        }
    }

    /// <summary>The actual split Platform catalog sources build and generate a receipt over coherently prepared package owners.</summary>
    [Fact]
    public async Task ActualGenerateStagesEverySelectedCatalogSourceAndProducesItsReceipt()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) { root = root.Parent; }
        root.ShouldNotBeNull();
        string configuration = typeof(DevelopmentCatalogPreparationCleanupTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
            ?? throw new InvalidOperationException("The executing test assembly configuration is required.");
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-generation-" + Guid.NewGuid().ToString("N"));
        const string Owner = "split-source-control";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        CancellationToken token = deadline.Token;
        bool completed = false;
        try
        {
            await CatalogTool.RunOwnedPreparationAsync(path, Owner, async () =>
            {
                byte[] sdkPin = await File.ReadAllBytesAsync(Path.Combine(root.FullName, "global.json"), token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(path, "global.json"), sdkPin, token).ConfigureAwait(false);
                string prepared = Path.Combine(path, "prepared");
                Directory.CreateDirectory(prepared);
                string project = Path.Combine(prepared, "PreparedCatalogOwners.csproj");
                new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("Import", new XAttribute("Project", Path.Combine(root.FullName, "Directory.Build.props"))),
                    new XElement("Import", new XAttribute("Project", Path.Combine(root.FullName, "Directory.Packages.props"))),
                    new XElement("PropertyGroup", new XElement("IsPackable", "false"),
                        new XElement("CopyLocalLockFileAssemblies", "true")),
                    new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Hexalith.Parties.Contracts")),
                        new XElement("PackageReference", new XAttribute("Include", "Hexalith.Parties.EventStoreExtensions")),
                        new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts")),
                        new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.DomainService")),
                        new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Server"))))).Save(project);
                string output = Path.Combine(prepared, "output");
                ProcessStartInfo start = new("dotnet")
                {
                    WorkingDirectory = prepared, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                };
                foreach (string argument in new[] { "build", project, "-c", configuration, "-m:1", "--disable-build-servers", "-nr:false",
                    "-p:UseSharedCompilation=false", "-p:UseHexalithProjectReferences=false", "-p:CentralPackageVersionsFileImported=true",
                    "--output", output, "--nologo", "-warnaserror" })
                {
                    start.ArgumentList.Add(argument);
                }
                CatalogTool.ConfigurePublishedPackageProcess(start);
                await CatalogTool.RunProcessAsync(start, Path.Combine(prepared, "build.log"), token).ConfigureAwait(false);
                using JsonDocument assets = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(prepared, "obj", "project.assets.json"), token).ConfigureAwait(false));
                JsonProperty[] sdk = assets.RootElement.GetProperty("libraries").EnumerateObject()
                    .Where(library => library.Name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal)).ToArray();
                sdk.ShouldNotBeEmpty();
                sdk.ShouldAllBe(library => library.Value.GetProperty("type").GetString() == "package");
                string sdkVersion = sdk.Select(library => library.Name[(library.Name.LastIndexOf('/') + 1)..]).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
                string contracts = Path.Combine(output, "Hexalith.Parties.Contracts.dll");
                string adapter = Path.Combine(output, "Hexalith.Parties.EventStoreExtensions.dll");
                using JsonDocument receipt = await CatalogTool.GenerateAsync(root, path, Owner, "eventstore", sdkVersion,
                    [(contracts, adapter, "parties")], token).ConfigureAwait(false);
                receipt.RootElement.GetProperty("instanceId").GetString().ShouldBe(Owner);
                receipt.RootElement.GetProperty("rootDigest").GetString().ShouldNotBeNullOrWhiteSpace();
                receipt.RootElement.GetProperty("ownerAssemblies").EnumerateArray().ShouldHaveSingleItem().GetString()
                    .ShouldEndWith("Hexalith.Parties.Contracts.dll");
                receipt.RootElement.GetProperty("adapterAssemblies").EnumerateArray().ShouldHaveSingleItem().GetString()
                    .ShouldEndWith("Hexalith.Parties.EventStoreExtensions.dll");
                new FileInfo(Path.Combine(path, "catalog.json")).Length.ShouldBeGreaterThan(0);
                string reader = Path.Combine(path, "reader");
                using JsonDocument preparation = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(reader, "preparation.json"), token).ConfigureAwait(false));
                string template = preparation.RootElement.GetProperty("templateSource").GetString()!;
                string companion = Path.Combine(Path.GetDirectoryName(template)!, "PublishedCatalogArtifactCohort.cs");
                File.Exists(companion).ShouldBeTrue();
                (await File.ReadAllBytesAsync(Path.Combine(path, "global.json"), token).ConfigureAwait(false)).ShouldBe(sdkPin);
                foreach (string owner in new[] { contracts, adapter })
                {
                    byte[] expected = await File.ReadAllBytesAsync(owner, token).ConfigureAwait(false);
                    string copied = Path.Combine(reader, "owners", AssemblyName.GetAssemblyName(owner).Name!, Path.GetFileName(owner));
                    (await File.ReadAllBytesAsync(copied, token).ConfigureAwait(false)).ShouldBe(expected);
                    preparation.RootElement.GetProperty("inputHashes").GetProperty(owner).GetString()
                        .ShouldBe(Convert.ToHexStringLower(SHA256.HashData(expected)));
                }
                foreach (string source in Directory.EnumerateFiles(Path.GetDirectoryName(template)!, "*.cs"))
                {
                    byte[] actual = await File.ReadAllBytesAsync(Path.Combine(reader, Path.GetFileName(source)), token).ConfigureAwait(false);
                    actual.ShouldBe(await File.ReadAllBytesAsync(source, token).ConfigureAwait(false));
                    preparation.RootElement.GetProperty("inputHashes").GetProperty(source).GetString()
                        .ShouldBe(Convert.ToHexStringLower(SHA256.HashData(actual)));
                }
            }).ConfigureAwait(true);
            completed = true;
        }
        finally
        {
            if (completed) { CatalogTool.DeleteOwnedDirectory(path, Owner); }
        }
        Directory.Exists(path).ShouldBeFalse();
    }

    /// <summary>Actual failed tool output remains actionable after its private working directory is removed.</summary>
    [Fact]
    public async Task FailedToolRetainsBoundedDiagnosticsAfterOwnedCleanup()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        MethodInfo run = typeof(CatalogTool).GetMethod("RunAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        InvalidDataException failure = await Should.ThrowAsync<InvalidDataException>(() => CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
            (Task)run.Invoke(null, [path, "tool.log", new[] { "parties-catalog-no-such-command" }, TestContext.Current.CancellationToken])!)).ConfigureAwait(true);
        Directory.Exists(path).ShouldBeFalse();
        failure.Message.ShouldContain("parties-catalog-no-such-command");
        failure.Message.Length.ShouldBeLessThan(4200);
    }

    /// <summary>Large simultaneous stdout/stderr are drained without retaining unbounded logs or losing their tails.</summary>
    [Fact]
    public async Task ExcessiveProcessOutputRetainsOnlyBoundedDiagnosticTails()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            ProcessStartInfo start = new("python3")
            {
                WorkingDirectory = path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("import sys\nfor i in range(2048):\n sys.stdout.write('x'*8192)\n sys.stderr.write('y'*8192)\nsys.stdout.write('STDOUT_END')\nsys.stderr.write('STDERR_END')\nsys.exit(1)");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            InvalidDataException failure = await Should.ThrowAsync<InvalidDataException>(() =>
                CatalogTool.RunProcessAsync(start, Path.Combine(path, "bounded.log"), timeout.Token)).ConfigureAwait(true);
            string retained = await File.ReadAllTextAsync(Path.Combine(path, "bounded.log"), TestContext.Current.CancellationToken).ConfigureAwait(true);
            retained.Length.ShouldBeLessThanOrEqualTo(8192);
            retained.ShouldContain("STDOUT_END");
            retained.ShouldContain("STDERR_END");
            failure.Message.Length.ShouldBeLessThan(4200);
            failure.Message.ShouldContain("STDERR_END");
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    /// <summary>Cancellation remains cancellation when process exit races with termination.</summary>
    [Fact]
    public async Task CanceledToolConfirmsTerminationAndPreservesCancellation()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        MethodInfo run = typeof(CatalogTool).GetMethod("RunAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync().ConfigureAwait(true);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() => CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
                (Task)run.Invoke(null, [path, "tool.log", new[] { "--info" }, canceled.Token])!)).ConfigureAwait(true);
            failure.Data.Contains("CatalogOwnedProcessStillRunning").ShouldBeFalse();
            Directory.Exists(path).ShouldBeFalse();
        }
    }

    /// <summary>Ordinary failed preparation removes partial owned output while a running owned child prevents deletion.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationFailurePreservesOnlyUnreapedOwnedProcesses(bool unreaped)
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        const string Owner = "this-run";
        try
        {
            InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => CatalogTool.RunOwnedPreparationAsync(path, Owner, () =>
            {
                File.WriteAllText(Path.Combine(path, "partial-secret"), "run-owned-value");
                InvalidOperationException error = new("actual preparation failed");
                if (unreaped) { error.Data["CatalogOwnedProcessStillRunning"] = true; }
                throw error;
            })).ConfigureAwait(true);
            failure.Message.ShouldBe("actual preparation failed");
            Directory.Exists(path).ShouldBe(unreaped);
            if (unreaped) { File.Exists(Path.Combine(path, "partial-secret")).ShouldBeTrue(); }
        }
        finally { CatalogTool.DeleteOwnedDirectory(path, Owner); }
    }

    /// <summary>Successful run preparation retains its private files until disposal and refuses foreign ownership.</summary>
    [Fact]
    public async Task SuccessRetainsOwnedFilesUntilCleanupAndForeignFilesSurvive()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        const string Owner = "this-run";
        try
        {
            await CatalogTool.RunOwnedPreparationAsync(path, Owner, () => Task.CompletedTask).ConfigureAwait(true);
            Directory.Exists(path).ShouldBeTrue();
            Should.Throw<InvalidDataException>(() => CatalogTool.DeleteOwnedDirectory(path, "foreign-run"));
            File.ReadAllText(Path.Combine(path, ".owner")).ShouldBe(Owner);
            await Should.ThrowAsync<InvalidDataException>(() => CatalogTool.RunOwnedPreparationAsync(path, "foreign-run", () => Task.CompletedTask)).ConfigureAwait(true);
            CatalogTool.DeleteOwnedDirectory(path, Owner);
            CatalogTool.DeleteOwnedDirectory(path, Owner);
            Directory.Exists(path).ShouldBeFalse();
        }
        finally { CatalogTool.DeleteOwnedDirectory(path, Owner); }
    }
}
