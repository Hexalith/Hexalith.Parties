using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Exercises the runtime preflight with real compilation and locally packed, version-matched SDK fixtures.</summary>
public sealed class ExternalRuntimeCompilationTests
{
    private const string FixtureVersion = "9.9.9";
    private const string TenantsProject = "references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj";
    private static readonly string[] SdkPackages =
    [
        "Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults",
        "Hexalith.EventStore.Admin.Server", "Hexalith.EventStore.Admin.Abstractions", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.SignalR",
    ];

    private static readonly string[] PeerPackages =
    [
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
    ];

    private static readonly string[] RuntimeProjects =
    [
        "tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj",
        "tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj",
        TenantsProject,
        "references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
        "references/Hexalith.Memories/src/Hexalith.Memories.DomainService/Hexalith.Memories.DomainService.csproj",
        "tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj",
        "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj",
        "src/Hexalith.Parties/Hexalith.Parties.csproj",
        "src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj",
        "src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj",
    ];

    /// <summary>Matching version strings cannot admit missing contracts, while supplying those contracts permits the same runtime preparation.</summary>
    [Fact]
    public void RuntimePreparationCompilesTheSelectedPackagesBeforeCompleting()
    {
        if (OperatingSystem.IsWindows()) { Assert.Skip("Runtime preparation requires Bash."); return; }
        string workspace = Path.Combine(Path.GetTempPath(), "parties-runtime-compilation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            File.Copy(CiTestPaths.RepoFile("global.json"), Path.Combine(workspace, "global.json"));
            File.WriteAllText(Path.Combine(workspace, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
            string commonFeed = Path.Combine(workspace, "common-feed");
            foreach (string package in SdkPackages.Where(package => package != "Hexalith.EventStore.DomainService").Concat(PeerPackages))
            {
                PackFixture(workspace, commonFeed, package, includeAdministratorContracts: false);
            }

            foreach (bool contractsAvailable in new[] { false, true })
            {
                string root = Path.Combine(workspace, contractsAvailable ? "compatible" : "missing-contracts");
                string selectedFeed = Path.Combine(root, "selected-feed");
                Directory.CreateDirectory(root);
                PackFixture(workspace, selectedFeed, "Hexalith.EventStore.DomainService", contractsAvailable);
                WriteRuntimeTree(root, commonFeed, selectedFeed);
                string receiptDirectory = Path.Combine(root, "artifacts", "runtime-profile", PackageTestProcess.ProbeConfiguration);
                string manifest = Path.Combine(receiptDirectory, "prepared.json");
                Directory.CreateDirectory(receiptDirectory);
                File.WriteAllText(manifest, "stale preparation must not qualify this attempt");
                string publicArchives = Path.Combine(receiptDirectory, "public-archives");
                Directory.CreateDirectory(publicArchives);
                foreach (string package in SdkPackages.Concat(PeerPackages))
                {
                    string archive = package + "." + FixtureVersion + ".nupkg";
                    string official = File.Exists(Path.Combine(commonFeed, archive)) ? Path.Combine(commonFeed, archive) : Path.Combine(selectedFeed, archive);
                    string cached = Path.Combine(publicArchives, archive.ToLowerInvariant());
                    File.Copy(official, cached);
                    File.AppendAllText(cached, "stale cached fixture bytes must be replaced");
                }

                // The unmodified script invokes the real SDK for every project. No compiler or validator is replaced.
                ProcessStartInfo start = CreateIsolatedFixtureStartInfo("bash", root);
                PackageTestProcess.UseOfficialArchiveFixtures(start, Path.Combine(root, "official-transport"), commonFeed, selectedFeed);
                foreach (string argument in new[] { Path.Combine(root, "scripts", "prepare-aspire-runtime-hosts.sh"), "--configuration", PackageTestProcess.ProbeConfiguration })
                {
                    start.ArgumentList.Add(argument);
                }
                PackageTestProcessResult result = PackageTestProcess.Run(start, root, processTimeoutMilliseconds: 240_000);
                string diagnostic = result.StandardOutput + result.StandardError;
                string assetsPath = Path.Combine(root, Path.GetDirectoryName(TenantsProject)!, "obj", "project.assets.json");
                File.Exists(assetsPath).ShouldBeTrue(diagnostic);
                using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
                assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(folder => Path.TrimEndingDirectorySeparator(folder.Name))
                    .ShouldBe([Path.Combine(root, "packages")]);
                Directory.Exists(Path.Combine(root, "inherited-cache")).ShouldBeFalse("Fixture packages must never enter the caller-selected cache.");
                foreach (string package in SdkPackages)
                {
                    assets.RootElement.GetProperty("libraries").GetProperty(package + "/" + FixtureVersion)
                        .GetProperty("type").GetString().ShouldBe("package");
                }

                if (!contractsAvailable)
                {
                    result.ExitCode.ShouldNotBe(0, diagnostic);
                    diagnostic.ShouldContain("error CS0246");
                    diagnostic.ShouldContain("IDomainServiceAdministratorVerifier");
                    diagnostic.ShouldContain("DomainServiceAdministratorClaim");
                    diagnostic.ShouldContain("Preparing Aspire runtime/artifact: " + TenantsProject);
                    diagnostic.ShouldNotContain("Preparing Aspire runtime/artifact: " + RuntimeProjects[4]);
                    File.Exists(manifest).ShouldBeFalse("A failed compile must invalidate an earlier completed manifest.");
                    foreach (string earlierProject in RuntimeProjects.Take(3))
                    {
                        Directory.EnumerateFiles(receiptDirectory, "*.json")
                            .Select(File.ReadAllText).ShouldContain(content => content.Contains(earlierProject, StringComparison.Ordinal));
                    }
                    Directory.Exists(Path.Combine(root, Path.GetDirectoryName(RuntimeProjects[4])!, "bin")).ShouldBeFalse();
                }
                else
                {
                    result.ExitCode.ShouldBe(0, diagnostic);
                    using JsonDocument completed = JsonDocument.Parse(File.ReadAllText(manifest));
                    completed.RootElement.GetProperty("completed").GetBoolean().ShouldBeTrue();
                    completed.RootElement.GetProperty("sdkVersion").GetString().ShouldBe(FixtureVersion);
                    completed.RootElement.GetProperty("projects").EnumerateObject().Count().ShouldBe(RuntimeProjects.Length);
                    foreach (string project in RuntimeProjects)
                    {
                        completed.RootElement.GetProperty("projects").TryGetProperty(Path.Combine(root, project), out _).ShouldBeTrue(project);
                    }
                    foreach (string package in SdkPackages.Concat(PeerPackages))
                    {
                        string archive = package + "." + FixtureVersion + ".nupkg";
                        string official = File.Exists(Path.Combine(commonFeed, archive)) ? Path.Combine(commonFeed, archive) : Path.Combine(selectedFeed, archive);
                        File.ReadAllBytes(Path.Combine(publicArchives, archive.ToLowerInvariant())).ShouldBe(File.ReadAllBytes(official));
                    }
                }
            }
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    private static void PackFixture(string workspace, string feed, string package, bool includeAdministratorContracts)
    {
        string source = Path.Combine(workspace, "package-source", package + (includeAdministratorContracts ? "-compatible" : "-baseline"));
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(feed);
        string repository = package.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "Hexalith.EventStore"
            : package.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) ? "Hexalith.Tenants" : "Hexalith.Memories";
        string project = Path.Combine(source, package + ".csproj");
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0"), new XElement("ImplicitUsings", "enable"),
                new XElement("PackageId", package), new XElement("Version", FixtureVersion),
                new XElement("RepositoryType", "git"), new XElement("RepositoryUrl", "https://github.com/Hexalith/" + repository),
                new XElement("RepositoryCommit", new string('a', 40)), new XElement("NuGetAudit", "false"))))
            .Save(project);
        if (package == "Hexalith.EventStore.DomainService")
        {
            File.WriteAllText(Path.Combine(source, "EventStoreDomainServicePolicies.cs"),
                "namespace Hexalith.EventStore.DomainService; public static class EventStoreDomainServicePolicies { public const string Query = \"fixture:query\"; }");
            if (includeAdministratorContracts)
            {
                foreach (string type in new[] { "IDomainServiceAdministratorVerifier", "DomainServiceAdministratorClaim" })
                {
                    File.Copy(CiTestPaths.RepoFile("references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/" + type + ".cs"), Path.Combine(source, type + ".cs"));
                }
            }
        }
        else
        {
            File.WriteAllText(Path.Combine(source, "FixtureMarker.cs"), "namespace " + package + "; public sealed class FixtureMarker;");
        }
        ProcessStartInfo start = CreateIsolatedFixtureStartInfo("dotnet", workspace);
        foreach (string argument in new[] { "pack", project, "--configuration", PackageTestProcess.ProbeConfiguration, "--output", feed, "--disable-build-servers", "-m:1" })
        {
            start.ArgumentList.Add(argument);
        }
        PackageTestProcessResult result = PackageTestProcess.Run(start, workspace, processTimeoutMilliseconds: 120_000);
        result.ExitCode.ShouldBe(0, result.Diagnostic);
    }

    private static ProcessStartInfo CreateIsolatedFixtureStartInfo(string executable, string root)
    {
        var start = new ProcessStartInfo(executable);
        string poison = Path.Combine(root, "hostile-import.props");
        File.WriteAllText(poison, "<Project><Target Name=\"RejectForeignBuildImport\" BeforeTargets=\"PrepareForBuild\"><Error Text=\"A caller build import entered the independent runtime fixture\" /></Target></Project>");
        string[] foreignBuildInputs =
        [
            "CustomBeforeDirectoryBuildProps", "CustomAfterMicrosoftCommonProps", "CustomBeforeDirectoryBuildTargets",
            "CustomAfterDirectoryBuildTargets", "AfterMicrosoftNETSdkTargets", "HEXALITH_SOURCE_MAPPING_HASH", "Configuration",
            "Hexalith1BuildPackageProps", "HexalithBuildsRoot", "HexalithEventStoreRoot", "HexalithEventStoreFromSource",
            "DirectoryBuildPropsPath", "DirectoryBuildTargetsPath",
        ];
        // Exercise the actual child launches with hostile inherited inputs without changing the test process environment.
        foreach (string name in foreignBuildInputs) { start.Environment[name] = poison; }
        start.Environment["NUGET_PACKAGES"] = Path.Combine(root, "inherited-cache");
        PackageTestProcess.RemoveSourceMappingEnvironment(start);
        HashSet<string> platform = new(StringComparer.OrdinalIgnoreCase)
        {
            "PATH", "PATHEXT", "HOME", "USERPROFILE", "TMP", "TEMP", "TMPDIR", "SystemRoot",
            "SystemDrive", "WINDIR", "COMSPEC", "APPDATA", "LOCALAPPDATA", "ProgramFiles",
            "ProgramFiles(x86)", "ProgramW6432", "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86",
            "LD_LIBRARY_PATH", "DYLD_LIBRARY_PATH", "LANG", "LC_ALL", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",
        };
        foreach (string name in start.Environment.Keys.Where(name => !platform.Contains(name)).ToArray())
        {
            start.Environment.Remove(name);
        }
        start.Environment["NUGET_PACKAGES"] = Path.Combine(root, "packages");
        foreach (string name in foreignBuildInputs) { start.Environment.ContainsKey(name).ShouldBeFalse(name); }
        start.Environment["NUGET_PACKAGES"].ShouldBe(Path.Combine(root, "packages"));
        return start;
    }

    private static void WriteRuntimeTree(string root, string commonFeed, string selectedFeed)
    {
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        foreach (string script in new[] { "prepare-aspire-runtime-hosts.sh", "validate-aspire-runtime-cohort.py" })
        {
            File.Copy(CiTestPaths.RepoFile("scripts/" + script), Path.Combine(root, "scripts", script));
        }
        new XDocument(new XElement("configuration", new XElement("packageSources", new XElement("clear"),
            new XElement("add", new XAttribute("key", "common"), new XAttribute("value", commonFeed)),
            new XElement("add", new XAttribute("key", "selected"), new XAttribute("value", selectedFeed))),
            new XElement("config", new XElement("add", new XAttribute("key", "globalPackagesFolder"), new XAttribute("value", Path.Combine(root, "packages"))))))
            .Save(Path.Combine(root, "NuGet.Config"));
        new XDocument(new XElement("Project", new XElement("PropertyGroup",
            new XElement("TargetFramework", "net10.0"), new XElement("ImplicitUsings", "enable"), new XElement("Nullable", "enable"),
            new XElement("TreatWarningsAsErrors", "true"), new XElement("NuGetAudit", "false"),
            new XElement("HexalithEventStoreVersion", FixtureVersion), new XElement("HexalithTenantsVersion", FixtureVersion),
            new XElement("HexalithMemoriesVersion", FixtureVersion), new XElement("HexalithFrontComposerVersion", FixtureVersion),
            new XElement("HexalithMcpCliVersion", FixtureVersion),
            new XElement("HexalithEventStoreCatalogHostsSupported", "true"))))
            .Save(Path.Combine(root, "Directory.Build.props"));
        foreach (string relative in RuntimeProjects.Append("references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj"))
        {
            string project = Path.Combine(root, relative);
            string directory = Path.GetDirectoryName(project)!;
            Directory.CreateDirectory(directory);
            new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("OutputType", "Exe")),
                new XElement("ItemGroup", SdkPackages.Select(package => new XElement("PackageReference", new XAttribute("Include", package), new XAttribute("Version", "$(HexalithEventStoreVersion)"))),
                    new XElement("FrameworkReference", new XAttribute("Include", "Microsoft.AspNetCore.App")))))
                .Save(project);
            File.WriteAllText(Path.Combine(directory, "Program.cs"), "System.Console.WriteLine(\"runtime compile fixture\");");
            if (relative == TenantsProject)
            {
                foreach (string type in new[] { "TenantsGlobalAdministratorVerifier", "ITenantsGlobalAdministratorAuthority" })
                {
                    File.Copy(CiTestPaths.RepoFile("references/Hexalith.Tenants/src/Hexalith.Tenants/Authorization/" + type + ".cs"), Path.Combine(directory, type + ".cs"));
                }
            }
        }
    }
}
