using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Package;

public sealed class ContractsPackageTests : IClassFixture<ContractsPackageFixture>
{
    internal const string LocalVersionOverride = "0.0.0-local.0";
    internal const string LocalPackVersionProperties = $"-p:MinVerVersionOverride={LocalVersionOverride} -p:PackageVersion={LocalVersionOverride}";

    private readonly ContractsPackageFixture _fixture;

    private static readonly string[] s_forbiddenDependencyTerms =
    [
        "Dapr",
        "Aspire",
        "Hosting",
        "MediatR",
        "FluentValidation",
        "Hexalith.Parties.AdminPortal",
        "Hexalith.Parties.Mcp",
        "Hexalith.Parties.Picker",
        "Hexalith.Parties.Projections",
        "Hexalith.Parties.Security",
        "Hexalith.Parties.Server",
        "Microsoft.AspNetCore.Components",
        "Microsoft.Extensions.Hosting",
    ];

    public ContractsPackageTests(ContractsPackageFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void PackedContractsPackage_HasContractSpecificMetadataAndXmlDocs()
    {
        ContractsPackageArtifacts package = _fixture.Artifacts;

        using ZipArchive archive = ZipFile.OpenRead(package.PartiesPackagePath);
        ZipArchiveEntry nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        XDocument nuspec = XDocument.Load(nuspecEntry.Open());
        XNamespace ns = nuspec.Root!.Name.Namespace;

        string description = nuspec.Root
            .Element(ns + "metadata")!
            .Element(ns + "description")!
            .Value;
        string tags = nuspec.Root
            .Element(ns + "metadata")!
            .Element(ns + "tags")!
            .Value;

        description.ShouldContain("contract types", Case.Insensitive);
        description.ShouldNotContain("microservice", Case.Insensitive);
        tags.ShouldContain("contracts", Case.Insensitive);
        tags.ShouldNotContain("dapr", Case.Insensitive);

        archive.Entries.ShouldContain(entry => entry.FullName == "lib/net10.0/Hexalith.Parties.Contracts.dll");
        ZipArchiveEntry xmlEntry = archive.Entries.Single(entry => entry.FullName == "lib/net10.0/Hexalith.Parties.Contracts.xml");
        XDocument xmlDocs = XDocument.Load(xmlEntry.Open());
        string docs = xmlDocs.ToString(SaveOptions.DisableFormatting);

        docs.ShouldContain("T:Hexalith.Parties.Contracts.Queries.GetParty");
        docs.ShouldContain("T:Hexalith.Parties.Contracts.Queries.ListParties");
        docs.ShouldContain("T:Hexalith.Parties.Contracts.Queries.SearchParties");
        docs.ShouldContain("T:Hexalith.Parties.Contracts.Events.PartyMerged");
        docs.ShouldContain("T:Hexalith.Parties.Contracts.Models.TemporalNameResult");
    }

    [Fact]
    public void PackedContractsPackage_DependencyGraphStaysContractOnly()
    {
        ContractsPackageArtifacts package = _fixture.Artifacts;

        string[] dependencies = ReadDependencyIds(package.PartiesPackagePath);

        dependencies.ShouldBe(["ByteAether.Ulid", "Hexalith.Commons.UniqueIds", "Hexalith.EventStore.Contracts", "Hexalith.McpCli.Abstractions"]);
        ReadDependencyVersion(package.PartiesPackagePath, "Hexalith.EventStore.Contracts")
            .ShouldBe(package.EventStoreVersion);
        ReadDependencyVersion(package.PartiesPackagePath, "Hexalith.McpCli.Abstractions")
            .ShouldBe(package.McpCliVersion);
        AssertNoForbiddenDependencyIds(dependencies, package.PartiesPackagePath);
    }

    [Fact]
    public void PackedEventStoreContractsPackage_DoesNotLeakDaprOrInfrastructureDependencies()
    {
        // EventStore.Contracts remains a contract-only dependency alongside the declaration-only
        // McpCli.Abstractions package. AC1 therefore requires proving the transitive nuspec of EventStore.Contracts
        // is also contract-only — otherwise a consumer of Parties.Contracts inherits the leak.
        ContractsPackageArtifacts package = _fixture.Artifacts;

        string[] dependencies = ReadDependencyIds(package.EventStoreContractsPackagePath);

        AssertNoForbiddenDependencyIds(dependencies, package.EventStoreContractsPackagePath);
    }

    [Fact]
    public void CleanPackageConsumer_CompilesRepresentativeContractSurfaceAndMetadata()
    {
        ContractsPackageArtifacts package = _fixture.Artifacts;
        string consumerDirectory = Path.Combine(package.WorkingDirectory, "consumer");
        Directory.CreateDirectory(consumerDirectory);

        File.WriteAllText(
            Path.Combine(consumerDirectory, "Consumer.csproj"),
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <RestoreAdditionalProjectSources>{{package.PackageFeed}}</RestoreAdditionalProjectSources>
                <RestoreNoCache>true</RestoreNoCache>
                <RestorePackagesPath>{{Path.Combine(consumerDirectory, "packages")}}</RestorePackagesPath>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Hexalith.Parties.Contracts" Version="{{package.PartiesVersion}}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(consumerDirectory, "Program.cs"),
            """
            using Hexalith.Parties.Contracts;
            using Hexalith.Parties.Contracts.Commands;
            using Hexalith.Parties.Contracts.Events;
            using Hexalith.Parties.Contracts.Models;
            using Hexalith.Parties.Contracts.Results;
            using Hexalith.Parties.Contracts.Queries;
            using Hexalith.Parties.Contracts.Search;
            using Hexalith.Parties.Contracts.Security;
            using Hexalith.Parties.Contracts.State;
            using Hexalith.Parties.Contracts.ValueObjects;

            Type[] contractTypes =
            [
                typeof(GetParty),
                typeof(ListParties),
                typeof(SearchParties),
                typeof(CreateParty),
                typeof(UpdatePersonDetails),
                typeof(PartyCreated),
                typeof(PartyMerged),
                typeof(PartyDetail),
                typeof(PartyIndexEntry),
                typeof(PartySearchResult),
                typeof(CompositeCommandResult),
                typeof(PartyCommandResult),
                typeof(IPartySearchProvider),
                typeof(IPersonalDataCommandGuard),
                typeof(PartyState),
                typeof(PersonDetails),
                typeof(PersonalDataAttribute),
            ];

            bool firstNameClassified = typeof(PersonDetails)
                .GetProperty(nameof(PersonDetails.FirstName))!
                .IsDefined(typeof(PersonalDataAttribute), inherit: true);

            if (!firstNameClassified || contractTypes.Length < 14)
            {
                throw new InvalidOperationException("Representative contract surface was not available.");
            }
            """);

        RunDotnet($"build Consumer.csproj --configuration {PackageTestProcess.ProbeConfiguration}", consumerDirectory);

        string assetsJson = File.ReadAllText(Path.Combine(consumerDirectory, "obj", "project.assets.json"));
        assetsJson.ShouldContain("Hexalith.Parties.Contracts");
        assetsJson.ShouldContain($"Hexalith.EventStore.Contracts/{package.EventStoreVersion}");
        foreach (string forbidden in s_forbiddenDependencyTerms)
        {
            assetsJson.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public void PersonalDataMetadata_IsVisibleFromPackagedContractsAssembly()
    {
        ContractsPackageArtifacts package = _fixture.Artifacts;
        ExtractPackageAssemblies(package.PackageFeed, package.WorkingDirectory);
        CopyTestOutputDependencyAssemblies(package.WorkingDirectory);

        ResolveEventHandler resolver = (_, args) =>
        {
            string dependencyPath = Path.Combine(package.WorkingDirectory, new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(dependencyPath) ? Assembly.LoadFile(dependencyPath) : null;
        };

        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            string assemblyPath = Path.Combine(package.WorkingDirectory, "Hexalith.Parties.Contracts.dll");
            Assembly packagedAssembly = Assembly.LoadFile(assemblyPath);

            Type personalDataAttribute = packagedAssembly.GetType("Hexalith.Parties.Contracts.PersonalDataAttribute", throwOnError: true)!;

            // Cross-surface representative checks. Story 3.1 keeps personal-data classification
            // for representative commands, events, models, state, and value objects; the packaged
            // assembly must surface every one of them without help from the source build.
            (string TypeName, string PropertyName)[] requiredMarkers =
            [
                ("Hexalith.Parties.Contracts.Commands.AddContactChannel", "Value"),
                ("Hexalith.Parties.Contracts.Commands.AddIdentifier", "Value"),
                ("Hexalith.Parties.Contracts.Commands.UpdateContactChannel", "Value"),
                ("Hexalith.Parties.Contracts.Events.ContactChannelAdded", "Value"),
                ("Hexalith.Parties.Contracts.Events.IdentifierAdded", "Value"),
                ("Hexalith.Parties.Contracts.Events.PartyDisplayNameDerived", "DisplayName"),
                ("Hexalith.Parties.Contracts.Models.PartyDetail", "DisplayName"),
                ("Hexalith.Parties.Contracts.Models.PartyIndexEntry", "DisplayName"),
                ("Hexalith.Parties.Contracts.Models.TemporalNameResult", "DisplayName"),
                ("Hexalith.Parties.Contracts.State.PartyState", "DisplayName"),
                ("Hexalith.Parties.Contracts.ValueObjects.PersonDetails", "FirstName"),
                ("Hexalith.Parties.Contracts.ValueObjects.PersonDetails", "LastName"),
                ("Hexalith.Parties.Contracts.ValueObjects.EmailAddress", "Address"),
                ("Hexalith.Parties.Contracts.ValueObjects.PhoneNumber", "Number"),
                ("Hexalith.Parties.Contracts.ValueObjects.PostalAddress", "Street"),
                ("Hexalith.Parties.Contracts.ValueObjects.SocialMediaHandle", "Handle"),
                ("Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier", "Value"),
                ("Hexalith.Parties.Contracts.ValueObjects.NameHistoryEntry", "DisplayName"),
            ];

            string[] missing = requiredMarkers
                .Where(marker =>
                {
                    Type packagedType = packagedAssembly.GetType(marker.TypeName, throwOnError: true)!;
                    PropertyInfo property = packagedType.GetProperty(marker.PropertyName, BindingFlags.Public | BindingFlags.Instance)
                        ?? throw new InvalidOperationException($"Property {marker.TypeName}.{marker.PropertyName} not found in packaged assembly.");
                    return !property.IsDefined(personalDataAttribute, inherit: true);
                })
                .Select(marker => $"{marker.TypeName}.{marker.PropertyName}")
                .Order(StringComparer.Ordinal)
                .ToArray();

            missing.ShouldBeEmpty("Required personal-data markers must remain discoverable from the packaged Contracts assembly.");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }

    private static void ExtractPackageAssemblies(string packageFeed, string outputDirectory)
    {
        foreach (string packagePath in Directory.GetFiles(packageFeed, "*.nupkg", SearchOption.TopDirectoryOnly))
        {
            using ZipArchive archive = ZipFile.OpenRead(packagePath);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(static item =>
                item.FullName.StartsWith("lib/net10.0/", StringComparison.Ordinal)
                && item.FullName.EndsWith(".dll", StringComparison.Ordinal)))
            {
                entry.ExtractToFile(Path.Combine(outputDirectory, Path.GetFileName(entry.FullName)), overwrite: true);
            }
        }
    }

    private static void CopyTestOutputDependencyAssemblies(string outputDirectory)
    {
        string[] dependencyNames =
        [
            "ByteAether.Ulid.dll",
        ];

        foreach (string dependencyName in dependencyNames)
        {
            string source = Path.Combine(AppContext.BaseDirectory, dependencyName);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(outputDirectory, dependencyName), overwrite: true);
            }
        }
    }

    private static string[] ReadDependencyIds(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        XDocument nuspec = XDocument.Load(nuspecEntry.Open());
        XNamespace ns = nuspec.Root!.Name.Namespace;

        return nuspec
            .Descendants(ns + "dependency")
            .Select(element => (string?)element.Attribute("id") ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadDependencyVersion(string packagePath, string dependencyId)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        XDocument nuspec = XDocument.Load(nuspecEntry.Open());
        XNamespace ns = nuspec.Root!.Name.Namespace;

        return nuspec
            .Descendants(ns + "dependency")
            .Single(element => string.Equals((string?)element.Attribute("id"), dependencyId, StringComparison.Ordinal))
            .Attribute("version")?.Value
            ?? throw new InvalidOperationException($"Dependency {dependencyId} has no version in {packagePath}.");
    }

    private static void AssertNoForbiddenDependencyIds(string[] dependencies, string packagePath)
    {
        string[] violations = dependencies
            .Where(dependency => s_forbiddenDependencyTerms
                .Any(term => dependency.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        violations.ShouldBeEmpty($"Packed dependencies in {packagePath} must not include forbidden infrastructure packages.");
    }

    internal static void RunDotnet(string arguments, string workingDirectory)
        => _ = PackageTestProcess.RunDotnet(arguments, workingDirectory);
}

internal sealed record ContractsPackageArtifacts(
    string WorkingDirectory,
    string PackageFeed,
    string PartiesPackagePath,
    string EventStoreContractsPackagePath,
    string PartiesVersion,
    string EventStoreVersion,
    string McpCliVersion);

public sealed class ContractsPackageFixture : IDisposable
{
    public ContractsPackageFixture()
    {
        Artifacts = CreatePackageArtifacts();
    }

    internal ContractsPackageArtifacts Artifacts { get; }

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

        ContractsPackageTests.RunDotnet(
            $"build \"{uniqueIdsProject}\" --configuration {PackageTestProcess.ProbeConfiguration} -m:1 {commonsPackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{uniqueIdsProject}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {commonsPackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{RepositoryDependencyPaths.Resolve(repoRoot, "references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj")}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {eventStorePackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{Path.Combine(repoRoot, "src", "Hexalith.Parties.Contracts", "Hexalith.Parties.Contracts.csproj")}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {ContractsPackageTests.LocalPackVersionProperties}",
            repoRoot);

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
