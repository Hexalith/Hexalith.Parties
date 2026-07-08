using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Package;

public sealed class ContractsPackageTests : IClassFixture<ContractsPackageFixture>
{
    internal const string LocalVersionOverride = "0.0.0-local.0";
    internal const string LocalPackVersionProperties = $"-p:MinVerVersionOverride={LocalVersionOverride} -p:PackageVersion={LocalVersionOverride}";
    internal const string CommonsPackVersionProperties = "-p:MinVerVersionOverride=2.27.0 -p:PackageVersion=2.27.0";
    internal const string EventStorePackVersionProperties = "-p:MinVerVersionOverride=3.47.0 -p:PackageVersion=3.47.0";

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

        docs.ShouldContain("T:Hexalith.Parties.Contracts.Events.PartyMerged");
        docs.ShouldContain("T:Hexalith.Parties.Contracts.Models.TemporalNameResult");
    }

    [Fact]
    public void PackedContractsPackage_DependencyGraphStaysContractOnly()
    {
        ContractsPackageArtifacts package = _fixture.Artifacts;

        string[] dependencies = ReadDependencyIds(package.PartiesPackagePath);

        dependencies.ShouldBe(["ByteAether.Ulid", "Hexalith.Commons.UniqueIds", "Hexalith.EventStore.Contracts"]);
        AssertNoForbiddenDependencyIds(dependencies, package.PartiesPackagePath);
    }

    [Fact]
    public void PackedEventStoreContractsPackage_DoesNotLeakDaprOrInfrastructureDependencies()
    {
        // Story 3.1 keeps Hexalith.EventStore.Contracts as the only direct Parties.Contracts
        // dependency. AC1 therefore requires proving the transitive nuspec of EventStore.Contracts
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
            using Hexalith.Parties.Contracts.Search;
            using Hexalith.Parties.Contracts.Security;
            using Hexalith.Parties.Contracts.State;
            using Hexalith.Parties.Contracts.ValueObjects;

            Type[] contractTypes =
            [
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

        RunDotnet("build Consumer.csproj --configuration Release", consumerDirectory);

        string assetsJson = File.ReadAllText(Path.Combine(consumerDirectory, "obj", "project.assets.json"));
        assetsJson.ShouldContain("Hexalith.Parties.Contracts");
        assetsJson.ShouldContain("Hexalith.EventStore.Contracts");
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

    private static void AssertNoForbiddenDependencyIds(string[] dependencies, string packagePath)
    {
        string[] violations = dependencies
            .Where(dependency => s_forbiddenDependencyTerms
                .Any(term => dependency.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        violations.ShouldBeEmpty($"Packed dependencies in {packagePath} must not include forbidden infrastructure packages.");
    }

    internal static void RunDotnet(string arguments, string workingDirectory)
    {
        using Process process = Process.Start(
            new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("Could not start dotnet process.");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited between the timeout check and the kill attempt.
            }

            throw new TimeoutException($"dotnet {arguments} timed out.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }

        process.ExitCode.ShouldBe(0, $"dotnet {arguments} failed.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
    }
}

internal sealed record ContractsPackageArtifacts(
    string WorkingDirectory,
    string PackageFeed,
    string PartiesPackagePath,
    string EventStoreContractsPackagePath,
    string PartiesVersion);

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
        string restoreSources = $"--source \"{feedDirectory}\" --source \"https://api.nuget.org/v3/index.json\"";

        ContractsPackageTests.RunDotnet(
            $"pack \"{Path.Combine(repoRoot, "references", "Hexalith.Commons", "src", "libraries", "Hexalith.Commons.UniqueIds", "Hexalith.Commons.UniqueIds.csproj")}\" --configuration Release --output \"{feedDirectory}\" {restoreSources} {ContractsPackageTests.CommonsPackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{Path.Combine(repoRoot, "references", "Hexalith.EventStore", "src", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Contracts.csproj")}\" --configuration Release --output \"{feedDirectory}\" {restoreSources} {ContractsPackageTests.EventStorePackVersionProperties}",
            repoRoot);
        ContractsPackageTests.RunDotnet(
            $"pack \"{Path.Combine(repoRoot, "src", "Hexalith.Parties.Contracts", "Hexalith.Parties.Contracts.csproj")}\" --configuration Release --output \"{feedDirectory}\" {restoreSources} {ContractsPackageTests.LocalPackVersionProperties}",
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
        return new ContractsPackageArtifacts(workingDirectory, feedDirectory, partiesPackage, eventStorePackage, version);
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
