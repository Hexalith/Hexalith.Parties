using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Exercises native UI asset staging against producer changes without starting a host.</summary>
public sealed class NativeAdminUiAssetStagingTests
{
    /// <summary>Independent evaluated artifact roots retain their own SDK asset copies and never mutate source staging.</summary>
    [Fact]
    public async Task IsolatedArtifactRootsRetainIndependentStaticAssets()
    {
        string root = Path.Combine(Path.GetTempPath(), "parties-ui-staging-" + Guid.NewGuid().ToString("N"));
        string producer = Path.Combine(root, "sdk", "src", "Hexalith.EventStore.Admin.UI");
        string project = Path.Combine(root, "source", "Host.csproj");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
            XDocument fixture = XDocument.Load(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj"));
            // Isolate the unchanged staging targets from the unrelated SDK UI executable and package graph.
            fixture.Descendants("PackageReference").Remove();
            fixture.Root!.Elements("Target").Where(target => (string?)target.Attribute("Name") is "BuildNativeAdminUiArtifact" or "RecordNativeAdminUiArtifact").Remove();
            fixture.Root.AddFirst(new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")));
            fixture.Save(project);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(project)!, "Program.cs"), "public static class Program { public static void Main() { } }");
            foreach (string name in new[] { "Hexalith.EventStore.Admin.UI.csproj", "appsettings.json", "appsettings.Development.json",
                "Properties/launchSettings.json", "wwwroot/css/app.css", "wwwroot/js/interop.js", "wwwroot/js/download.js", "wwwroot/js/obsolete.js" })
            {
                string path = Path.Combine(producer, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, name == "wwwroot/js/obsolete.js" ? new string('x', 8192) : name);
            }
            string retainedSource = Path.Combine(Path.GetDirectoryName(project)!, "wwwroot", "retained.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(retainedSource)!);
            File.WriteAllText(retainedSource, "unrelated source staging");

            async Task<JsonDocument> StageAsync(string artifacts, bool metadataOnly = false)
            {
                ProcessStartInfo start = new("dotnet")
                {
                    WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                };
                PackageTestProcess.RemoveSourceMappingEnvironment(start);
                string[] arguments = metadataOnly
                    ? ["msbuild", project, "-target:PrepareForBuild;GetTargetPath;GetStaticWebAssetsProjectConfiguration",
                        "-p:Configuration=Release", "-p:ArtifactsPath=" + artifacts, "-p:BuildProjectReferences=false", "-m:1", "-nr:false"]
                    : ["build", project, "--configuration", "Release", "--artifacts-path", artifacts,
                        "--disable-build-servers", "-m:1", "-nr:false", "-p:NuGetAudit=false"];
                foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
                start.ArgumentList.Add("-p:HexalithEventStoreRoot=" + Path.Combine(root, "sdk"));
                using PackageTestProcessSupervisor supervisor = new(start, 10_000);
                using Process process = Process.Start(supervisor.StartInfo)!;
                (string output, string error) = await CaptureStagingProcessAsync(process, supervisor, TestContext.Current.CancellationToken).ConfigureAwait(true);
                process.ExitCode.ShouldBe(0, output + error);
                JsonDocument built = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(artifacts, "staticwebassets.build.json", SearchOption.AllDirectories).Single()));
                string staged = AssertSdkMetadata(built, artifacts);
                using JsonDocument runtime = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(artifacts, "Host.staticwebassets.runtime.json", SearchOption.AllDirectories).Single()));
                string[] contentRoots = runtime.RootElement.GetProperty("ContentRoots").EnumerateArray().Select(value => Path.GetFullPath(value.GetString()!).TrimEnd(Path.DirectorySeparatorChar)).ToArray();
                contentRoots.ShouldContain(Path.GetFullPath(staged).TrimEnd(Path.DirectorySeparatorChar));
                contentRoots.ShouldNotContain(Path.Combine(Path.GetDirectoryName(project)!, "wwwroot"));
                return built;
            }
            string first = Path.Combine(root, "first-artifacts"), second = Path.Combine(root, "second-artifacts");
            using JsonDocument firstAssets = await StageAsync(first).ConfigureAwait(true);
            string firstStage = AssertSdkMetadata(firstAssets, first);
            string oldOutput = Path.Combine(Path.GetDirectoryName(Directory.GetFiles(first, "Host.dll", SearchOption.AllDirectories).First(path => path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))!, "wwwroot", "js", "obsolete.js");
            File.Exists(oldOutput).ShouldBeTrue("The SDK must deploy the prepared static asset.");
            File.Exists(oldOutput + ".gz").ShouldBeTrue("The SDK must generate the obsolete producer's compressed copy.");
            string firstOutput = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(oldOutput)!, ".."));
            string currentCompressed = Path.Combine(firstOutput, "css", "app.css.gz");
            string foreignOutput = Path.Combine(firstOutput, "_framework", "foreign-sdk.bin");
            File.Exists(currentCompressed).ShouldBeTrue("The SDK must generate the current producer's compressed copy.");
            Directory.CreateDirectory(Path.GetDirectoryName(foreignOutput)!);
            File.WriteAllText(foreignOutput, "unrelated SDK-owned output");
            Dictionary<string, string> prepared = AssetInventory(first);
            using JsonDocument probed = await StageAsync(first, metadataOnly: true).ConfigureAwait(true);
            AssetInventory(first).ShouldBe(prepared, "A metadata probe must preserve the completed manifests and asset inventory.");
            File.WriteAllText(Path.Combine(producer, "wwwroot", "css", "app.css"), "new producer bytes");
            File.Delete(Path.Combine(producer, "wwwroot", "js", "obsolete.js"));
            using JsonDocument secondAssets = await StageAsync(second).ConfigureAwait(true);
            string secondStage = AssertSdkMetadata(secondAssets, second);
            firstStage.ShouldNotBe(secondStage);
            File.ReadAllText(Path.Combine(firstStage, "css", "app.css")).ShouldBe("wwwroot/css/app.css");
            File.Exists(Path.Combine(firstStage, "js", "obsolete.js")).ShouldBeTrue();
            File.ReadAllText(Path.Combine(secondStage, "css", "app.css")).ShouldBe("new producer bytes");
            File.Exists(Path.Combine(secondStage, "js", "obsolete.js")).ShouldBeFalse();
            using JsonDocument repeated = await StageAsync(first).ConfigureAwait(true);
            File.Exists(Path.Combine(firstStage, "js", "obsolete.js")).ShouldBeFalse();
            File.Exists(oldOutput).ShouldBeFalse();
            File.Exists(oldOutput + ".gz").ShouldBeFalse();
            repeated.RootElement.GetProperty("Assets").EnumerateArray()
                .ShouldNotContain(asset => asset.GetProperty("RelativePath").GetString()!.StartsWith("js/obsolete.js", StringComparison.Ordinal));
            File.Exists(currentCompressed).ShouldBeTrue();
            File.ReadAllText(foreignOutput).ShouldBe("unrelated SDK-owned output");
            File.ReadAllText(Path.Combine(secondStage, "css", "app.css")).ShouldBe("new producer bytes");
            File.ReadAllText(retainedSource).ShouldBe("unrelated source staging");
            File.Exists(Path.Combine(Path.GetDirectoryName(project)!, "wwwroot", "css", "app.css")).ShouldBeFalse();
        }
        finally { if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); } }
    }

    /// <summary>Cancellation reaps the real MSBuild child and closes descendant output before deleting the fixture.</summary>
    [Fact]
    public async Task CanceledStagingReapsMsbuildBeforeFixtureDeletion()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The owned subprocess liveness control requires Linux."); }
        string root = Directory.CreateTempSubdirectory("parties-staging-cancellation-").FullName;
        try
        {
            string ready = Path.Combine(root, "ready");
            File.WriteAllText(Path.Combine(root, "hold.py"), """
                import os, pathlib, time
                ready = pathlib.Path(__file__).with_name('ready')
                staged = ready.with_suffix('.new')
                staged.write_text(str(os.getpid()))
                staged.replace(ready)
                while True:
                    time.sleep(0.05)
                """);
            string project = Path.Combine(root, "Hold.proj");
            new XDocument(new XElement("Project", new XElement("Target", new XAttribute("Name", "Hold"),
                new XElement("Exec", new XAttribute("Command", "python3 \"hold.py\""))))).Save(project);
            ProcessStartInfo start = new("dotnet")
            {
                WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            };
            PackageTestProcess.RemoveSourceMappingEnvironment(start);
            foreach (string argument in new[] { "msbuild", project, "-target:Hold", "-m:1", "-nr:false" }) { start.ArgumentList.Add(argument); }
            using PackageTestProcessSupervisor supervisor = new(start, 10_000);
            using Process process = Process.Start(supervisor.StartInfo)!;
            using CancellationTokenSource canceled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            Task<(string Output, string Error)> running = CaptureStagingProcessAsync(process, supervisor, canceled.Token);
            try
            {
                using CancellationTokenSource readyDeadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                readyDeadline.CancelAfter(TimeSpan.FromSeconds(30));
                while (!File.Exists(ready)) { await Task.Delay(25, readyDeadline.Token).ConfigureAwait(true); }
            }
            finally
            {
                canceled.Cancel();
                OperationCanceledException cancellation = await Should.ThrowAsync<OperationCanceledException>(() => running).ConfigureAwait(true);
                cancellation.CancellationToken.ShouldBe(canceled.Token);
            }
            process.HasExited.ShouldBeTrue();
            int child = int.Parse(File.ReadAllText(ready), System.Globalization.CultureInfo.InvariantCulture);
            string stat = $"/proc/{child}/stat";
            try
            {
                File.ReadAllText(stat).Split(") ", 2, StringSplitOptions.None)[1][0].ShouldBe('Z', "No staging descendant may keep running after cancellation.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            Directory.Delete(root, recursive: true);
            Directory.Exists(root).ShouldBeFalse();
        }
        finally { if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); } }
    }

    /// <summary>The existing supervisor reaps detached children even after the staging command exits first.</summary>
    [Fact]
    public async Task StagingRootExitReapsDescendantsBeforeReturning()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Detached descendant containment requires Linux."); }
        string root = Directory.CreateTempSubdirectory("parties-staging-root-exit-").FullName;
        try
        {
            string ready = Path.Combine(root, "child");
            ProcessStartInfo start = new("python3")
            {
                WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("import pathlib, subprocess, sys; child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(45)'], start_new_session=True); pathlib.Path('child').write_text(str(child.pid))");
            using PackageTestProcessSupervisor supervisor = new(start, 10_000);
            using Process process = Process.Start(supervisor.StartInfo)!;
            await CaptureStagingProcessAsync(process, supervisor, TestContext.Current.CancellationToken).ConfigureAwait(true);
            process.ExitCode.ShouldBe(0);
            int child = int.Parse(File.ReadAllText(ready), System.Globalization.CultureInfo.InvariantCulture);
            Directory.Exists($"/proc/{child}").ShouldBeFalse("The supervisor must reap the orphan before returning its parent's successful exit.");
            Directory.Delete(root, recursive: true);
        }
        finally { if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); } }
    }

    /// <summary>Always finishes child exit and output capture under an independent cleanup deadline.</summary>
    /// <param name="process">The started staging process owned by this test.</param>
    /// <param name="supervisor">The existing process-tree owner, retained until descendant cleanup completes.</param>
    /// <param name="cancellationToken">The caller's execution deadline, independent of cleanup.</param>
    /// <returns>The completed standard output and error streams.</returns>
    private static async Task<(string Output, string Error)> CaptureStagingProcessAsync(Process process, PackageTestProcessSupervisor supervisor, CancellationToken cancellationToken)
    {
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); }
        finally
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10));
            if (supervisor.IsContained) { supervisor.RequestTermination(); }
            else if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                catch (System.ComponentModel.Win32Exception) when (process.HasExited) { }
            }
            try
            {
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).WaitAsync(cleanup.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cleanup.IsCancellationRequested)
            {
                throw new TimeoutException("The staging process cleanup exceeded its independent deadline.", exception);
            }
            supervisor.EnsureCleanupSucceeded(process.StartInfo.FileName);
        }
        return (await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    private static Dictionary<string, string> AssetInventory(string artifacts) => Directory.EnumerateFiles(artifacts, "*", SearchOption.AllDirectories)
        .Where(path => path.Contains(Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || path.Contains(Path.DirectorySeparatorChar + "compressed" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.GetFileName(path).Contains("staticwebassets", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".json", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(artifacts, path),
            path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    private static string AssertSdkMetadata(JsonDocument document, string artifacts)
    {
        JsonElement[] assets = document.RootElement.GetProperty("Assets").EnumerateArray().ToArray();
        JsonElement selected = assets.Single(asset => asset.GetProperty("RelativePath").GetString() == "css/app.css");
        string stage = selected.GetProperty("ContentRoot").GetString()!;
        Path.GetFullPath(stage).ShouldStartWith(artifacts + Path.DirectorySeparatorChar);
        JsonElement[] staged = assets.Where(asset => asset.GetProperty("ContentRoot").GetString() == stage).ToArray();
        staged.ShouldNotBeEmpty();
        staged.ShouldAllBe(asset => asset.GetProperty("Identity").GetString()!.StartsWith(stage, StringComparison.Ordinal));
        document.RootElement.GetProperty("DiscoveryPatterns").EnumerateArray()
            .Where(pattern => pattern.GetProperty("Source").GetString() == "Host")
            .ShouldAllBe(pattern => Path.GetFullPath(pattern.GetProperty("ContentRoot").GetString()!).TrimEnd(Path.DirectorySeparatorChar)
                == Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar));
        return stage;
    }
}
