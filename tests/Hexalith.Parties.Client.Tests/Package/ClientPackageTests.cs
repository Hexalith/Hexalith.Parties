using System.IO.Compression;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.Package;

public sealed class ClientPackageTests : IDisposable
{
    private const string LocalVersionOverride = "0.0.0-local.0";
    private const string LocalPackVersionProperties = $"-p:MinVerVersionOverride={LocalVersionOverride} -p:PackageVersion={LocalVersionOverride}";
    private const int MaxDeclaredDependencyCount = 16;
    private const long MaxPackedClientPackageBytes = 5L * 1024L * 1024L;

    private static readonly string[] s_forbiddenDependencyTerms =
    [
        "Dapr",
        "MediatR",
        "FluentValidation",
        "Swagger",
        "OpenApi",
        "AspNetCore.Mvc",
        "Hexalith.Parties.Server",
        "Hexalith.Parties.Projections",
        "Hexalith.Parties.Mcp",
        "Hexalith.Parties.AdminPortal",
        "Hexalith.Parties.Picker",
    ];

    private readonly string _workDirectory = Path.Combine(
        Path.GetTempPath(),
        "hexalith-parties-client-package-" + Guid.NewGuid().ToString("N"));

    public ClientPackageTests()
    {
        Directory.CreateDirectory(_workDirectory);
    }

    [Fact]
    public void PackedClientPackage_HasOnlyApprovedDeclaredDependenciesAndFitsSizeBudget()
    {
        PackageProbe probe = BuildLocalPackageFeed();
        string nuspecXml = ReadNuspecXml(probe.ClientPackagePath);
        IReadOnlyList<string> dependencyIds = ReadDependencyIds(nuspecXml);
        ReadDependencyVersion(nuspecXml, "Hexalith.EventStore.Contracts")
            .ShouldBe(probe.EventStoreVersion);

        dependencyIds.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ShouldBe(
        [
            "ByteAether.Ulid",
            "Hexalith.Commons.Http",
            "Hexalith.Commons.UniqueIds",
            "Hexalith.EventStore.Contracts",
            "Hexalith.McpCli.Abstractions",
            "Hexalith.Parties.Contracts",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Configuration.Abstractions",
            "Microsoft.Extensions.Configuration.Binder",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Diagnostics.Abstractions",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Options",
            "Microsoft.Extensions.Options.ConfigurationExtensions",
        ]);

        dependencyIds.Count.ShouldBeLessThanOrEqualTo(MaxDeclaredDependencyCount);
        new FileInfo(probe.ClientPackagePath).Length.ShouldBeLessThan(MaxPackedClientPackageBytes);

        foreach (string forbidden in s_forbiddenDependencyTerms)
        {
            dependencyIds.ShouldNotContain(
                id => id.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"Client package must not declare forbidden dependency '{forbidden}'. Nuspec: {nuspecXml}");
        }
    }

    [Fact]
    public void CleanPackageConsumer_RegistersTypedClientsWithoutForbiddenTransitivePackages()
    {
        PackageProbe probe = BuildLocalPackageFeed();
        string consumerDirectory = Path.Combine(_workDirectory, "consumer");
        Directory.CreateDirectory(consumerDirectory);

        string projectPath = Path.Combine(consumerDirectory, "Consumer.csproj");
        File.WriteAllText(
            projectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <RestoreNoCache>true</RestoreNoCache>
                <RestorePackagesPath>$(MSBuildProjectDirectory)\packages</RestorePackagesPath>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Hexalith.Parties.Client" Version="{{probe.ClientPackageVersion}}" />
              </ItemGroup>
            </Project>
            """);


        File.WriteAllText(
            Path.Combine(consumerDirectory, "Program.cs"),
            """
            using Hexalith.Parties.Client.Abstractions;
            using Hexalith.Parties.Client.Extensions;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Parties:BaseUrl"] = "https://eventstore.example",
                    ["Parties:Tenant"] = "tenant-a",
                })
                .Build();

            var services = new ServiceCollection();
            IServiceCollection returned = services.AddPartiesClient(configuration);
            if (!ReferenceEquals(services, returned))
            {
                throw new InvalidOperationException("AddPartiesClient must return the original service collection.");
            }

            using ServiceProvider provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<IPartiesCommandClient>();
            _ = provider.GetRequiredService<IPartiesQueryClient>();
            """);

        RunDotnet("build", $"\"{projectPath}\" --configuration {PackageTestProcess.ProbeConfiguration}", consumerDirectory);
        string listOutput = RunDotnet(
            "package",
            $"list --project \"{projectPath}\" --include-transitive",
            consumerDirectory);
        string assetsJson = File.ReadAllText(Path.Combine(consumerDirectory, "obj", "project.assets.json"));
        assetsJson.ShouldContain($"Hexalith.EventStore.Contracts/{probe.EventStoreVersion}");

        foreach (string forbidden in s_forbiddenDependencyTerms)
        {
            listOutput.ShouldNotContain(forbidden);
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private PackageProbe BuildLocalPackageFeed()
    {
        string repoRoot = LocateRepositoryRoot();
        string feedDirectory = Path.Combine(_workDirectory, "feed");
        Directory.CreateDirectory(feedDirectory);
        string configPath = Path.Combine(_workDirectory, "NuGet.Config");
        new XDocument(new XElement("configuration",
            new XElement("packageSources", new XElement("clear"),
                new XElement("add", new XAttribute("key", "fixture"), new XAttribute("value", feedDirectory)),
                new XElement("add", new XAttribute("key", "public"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
            new XElement("packageSourceMapping",
                new XElement("packageSource", new XAttribute("key", "fixture"),
                    new XElement("package", new XAttribute("pattern", "Hexalith.Parties.Contracts")),
                    new XElement("package", new XAttribute("pattern", "Hexalith.Parties.Client")),
                    new XElement("package", new XAttribute("pattern", "Hexalith.EventStore.Contracts")),
                    new XElement("package", new XAttribute("pattern", "Hexalith.Commons.UniqueIds"))),
                new XElement("packageSource", new XAttribute("key", "public"),
                    new XElement("package", new XAttribute("pattern", "*")))))).Save(configPath);
        string restoreSources = $"--source \"{feedDirectory}\" --source \"https://api.nuget.org/v3/index.json\" "
            + $"-p:RestorePackagesPath=\"{Path.Combine(_workDirectory, "packages")}\" -p:RestoreNoCache=true -p:RestoreConfigFile=\"{configPath}\"";
        string eventStoreVersion = PackageTestProcess.ResolveMsbuildProperty(
            repoRoot,
            "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
            "HexalithEventStoreVersion");
        string commonsVersion = PackageTestProcess.ResolveMsbuildProperty(
            repoRoot, "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj", "HexalithCommonsVersion");
        string commonsPackVersionProperties = $"-p:MinVerVersionOverride={commonsVersion} -p:PackageVersion={commonsVersion}";
        string uniqueIdsProject = RepositoryDependencyPaths.Resolve(repoRoot,
            "references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj");
        // Pack actual owner prerequisites at the selected catalog versions. This is
        // fixture validation, with no claim about published same-version bytes.
        RunDotnet("build", $"\"{uniqueIdsProject}\" --configuration {PackageTestProcess.ProbeConfiguration} -m:1 {commonsPackVersionProperties}", repoRoot);
        RunDotnet("pack", $"\"{uniqueIdsProject}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {commonsPackVersionProperties}", repoRoot);
        RunDotnet("pack", $"\"{RepositoryDependencyPaths.Resolve(repoRoot, "references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj")}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} -p:MinVerVersionOverride={eventStoreVersion} -p:PackageVersion={eventStoreVersion}", repoRoot);
        RunDotnet(
            "pack",
            $"\"{Path.Combine(repoRoot, "src", "Hexalith.Parties.Contracts", "Hexalith.Parties.Contracts.csproj")}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {LocalPackVersionProperties}",
            repoRoot);
        RunDotnet(
            "pack",
            $"\"{Path.Combine(repoRoot, "src", "Hexalith.Parties.Client", "Hexalith.Parties.Client.csproj")}\" --configuration {PackageTestProcess.ProbeConfiguration} --output \"{feedDirectory}\" {restoreSources} {LocalPackVersionProperties}",
            repoRoot);

        string clientPackagePath = Directory
            .GetFiles(feedDirectory, "Hexalith.Parties.Client.*.nupkg", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();

        return new PackageProbe(
            feedDirectory,
            clientPackagePath,
            ReadPackageVersion(clientPackagePath),
            eventStoreVersion);
    }

    private static IReadOnlyList<string> ReadDependencyIds(string nuspecXml)
    {
        XDocument document = XDocument.Parse(nuspecXml);

        return
        [
            .. document
                .Descendants()
                .Where(static element => element.Name.LocalName == "dependency")
                .Select(static element => element.Attribute("id")?.Value)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static string ReadDependencyVersion(string nuspecXml, string dependencyId)
    {
        XDocument document = XDocument.Parse(nuspecXml);
        return document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "dependency"
                && string.Equals(element.Attribute("id")?.Value, dependencyId, StringComparison.Ordinal))
            .Attribute("version")?.Value
            ?? throw new InvalidOperationException($"Dependency {dependencyId} has no version.");
    }

    private static string ReadPackageVersion(string packagePath)
    {
        string nuspecXml = ReadNuspecXml(packagePath);
        XDocument document = XDocument.Parse(nuspecXml);
        string? version = document
            .Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "version")
            ?.Value;

        return string.IsNullOrWhiteSpace(version)
            ? throw new InvalidOperationException($"Package version not found in {packagePath}.")
            : version;
    }

    private static string ReadNuspecXml(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry nuspecEntry = archive
            .Entries
            .Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        using Stream stream = nuspecEntry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string RunDotnet(string command, string arguments, string workingDirectory)
    {
        string effectiveArguments = string.Equals(command, "pack", StringComparison.Ordinal)
            ? $"-m:1 {arguments}"
            : arguments;
        PackageTestProcessResult result = PackageTestProcess.RunDotnet(
            $"{command} {effectiveArguments}",
            workingDirectory);
        return result.StandardOutput + result.StandardError;
    }

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed record PackageProbe(
        string FeedDirectory,
        string ClientPackagePath,
        string ClientPackageVersion,
        string EventStoreVersion);
}
