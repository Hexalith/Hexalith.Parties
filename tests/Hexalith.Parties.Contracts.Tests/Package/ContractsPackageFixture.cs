using System.IO.Compression;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Package;

/// <summary>Builds and reaps isolated package-only contract consumer probes.</summary>
public sealed class ContractsPackageFixture : IDisposable
{
    /// <summary>Creates the isolated package-only contract probe.</summary>
    public ContractsPackageFixture()
    {
        Artifacts = CreatePackageArtifacts();
    }

    /// <summary>Gets the exact isolated package probe outputs.</summary>
    internal ContractsPackageArtifacts Artifacts { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Artifacts.WorkingDirectory))
            {
                Directory.Delete(Artifacts.WorkingDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup — locked package cache files are tolerable across CI agents.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup — read-only artefacts inside the NuGet cache are tolerable.
        }
    }

    private static ContractsPackageArtifacts CreatePackageArtifacts()
    {
        string repoRoot = FindRepoRoot();
        string workingDirectory = Path.Combine(Path.GetTempPath(), "hexalith-parties-contracts-package-tests", Guid.NewGuid().ToString("N"));
        string feedDirectory = Path.Combine(workingDirectory, "feed");
        Directory.CreateDirectory(feedDirectory);
        string configPath = Path.Combine(workingDirectory, "NuGet.Config");
        new XDocument(new XElement("configuration",
            new XElement("packageSources", new XElement("clear"),
                new XElement("add", new XAttribute("key", "fixture"), new XAttribute("value", feedDirectory)),
                new XElement("add", new XAttribute("key", "public"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
            new XElement("packageSourceMapping",
                new XElement("packageSource", new XAttribute("key", "fixture"),
                    new XElement("package", new XAttribute("pattern", "Hexalith.Parties.Contracts")),
                    new XElement("package", new XAttribute("pattern", "Hexalith.EventStore.Contracts")),
                    new XElement("package", new XAttribute("pattern", "Hexalith.Commons.UniqueIds"))),
                new XElement("packageSource", new XAttribute("key", "public"),
                    new XElement("package", new XAttribute("pattern", "*")))))).Save(configPath);
        // Local packages must be resolved from this fixture's feed, even when the same
        // catalog version already exists with published bytes in the global NuGet cache.
        string restoreSources = $"--source \"{feedDirectory}\" --source \"https://api.nuget.org/v3/index.json\" "
            + $"-p:RestorePackagesPath=\"{Path.Combine(workingDirectory, "packages")}\" -p:RestoreNoCache=true -p:RestoreConfigFile=\"{configPath}\"";
        string isolatedBuild = PackageTestProcess.CreateIsolatedSourceBuildProperty(workingDirectory);
        string eventStoreVersion = PackageTestProcess.ResolveMsbuildProperty(
            repoRoot,
            "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj",
            "HexalithEventStoreVersion");
        string commonsVersion = PackageTestProcess.ResolveMsbuildProperty(
            repoRoot, "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj", "HexalithCommonsVersion");
        string commonsPackVersionProperties = $"-p:MinVerVersionOverride={commonsVersion} -p:PackageVersion={commonsVersion}";
        string mcpCliVersion = PackageTestProcess.ResolveMsbuildProperty(
            repoRoot,
            "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj",
            "HexalithMcpCliVersion");
        string eventStorePackVersionProperties =
            $"-p:MinVerVersionOverride={eventStoreVersion} -p:PackageVersion={eventStoreVersion}";
        string uniqueIdsProject = RepositoryDependencyPaths.Resolve(repoRoot,
            "references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj");
        string eventStoreProject = RepositoryDependencyPaths.Resolve(repoRoot,
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj");
        string partiesContractsProject = Path.Combine(repoRoot, "src", "Hexalith.Parties.Contracts", "Hexalith.Parties.Contracts.csproj");

        ContractsPackageTests.RunDotnet(
            $"build \"{uniqueIdsProject}\" --configuration {PackageTestProcess.ProbeConfiguration} -m:1 {isolatedBuild} {commonsPackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{uniqueIdsProject}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {isolatedBuild} {commonsPackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{eventStoreProject}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {isolatedBuild} {eventStorePackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{partiesContractsProject}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {isolatedBuild} {ContractsPackageTests.LocalPackVersionProperties}",
            repoRoot);
        PackageTestProcess.AssertSharedAssetsDoNotUseFixture(
            workingDirectory, uniqueIdsProject, eventStoreProject, partiesContractsProject);

        string partiesPackage = Directory
            .GetFiles(feedDirectory, "Hexalith.Parties.Contracts.*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
        string eventStorePackage = Directory
            .GetFiles(feedDirectory, "Hexalith.EventStore.Contracts.*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();

        string version = ReadPackageVersion(partiesPackage);
        ReadPackageVersion(eventStorePackage).ShouldBe(eventStoreVersion);
        return new ContractsPackageArtifacts(
            workingDirectory,
            feedDirectory,
            partiesPackage,
            eventStorePackage,
            version,
            eventStoreVersion,
            mcpCliVersion);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))
                && File.Exists(Path.Combine(directory.FullName, "src", "Hexalith.Parties.Contracts", "Hexalith.Parties.Contracts.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ReadPackageVersion(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        XDocument nuspec = XDocument.Load(nuspecEntry.Open());
        XNamespace ns = nuspec.Root!.Name.Namespace;
        return nuspec.Root.Element(ns + "metadata")!.Element(ns + "version")!.Value;
    }
}
