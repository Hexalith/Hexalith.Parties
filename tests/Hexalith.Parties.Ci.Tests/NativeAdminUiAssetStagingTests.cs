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
                using Process process = Process.Start(start)!;
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                string output = await stdout.ConfigureAwait(true);
                process.ExitCode.ShouldBe(0, output + await stderr.ConfigureAwait(true));
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
