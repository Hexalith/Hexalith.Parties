using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.Package;

public sealed class ClientPackageTests : IDisposable
{
    private const string LocalVersionOverride = "0.0.0-local.0";
    private const string LocalPackVersionProperties = $"-p:MinVerVersionOverride={LocalVersionOverride} -p:PackageVersion={LocalVersionOverride}";
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

        dependencyIds.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ShouldBe(
        [
            "Hexalith.EventStore.Contracts",
            "Hexalith.Parties.Contracts",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Configuration.Binder",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Options",
        ]);

        dependencyIds.Count.ShouldBeLessThan(10);
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
            Path.Combine(consumerDirectory, "NuGet.Config"),
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local-story-feed" value="{{probe.FeedDirectory}}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
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

        RunDotnet("build", $"\"{projectPath}\" --configuration Release", consumerDirectory);
        DotnetResult listResult = RunDotnet(
            "package",
            $"list --project \"{projectPath}\" --include-transitive",
            consumerDirectory);

        foreach (string forbidden in s_forbiddenDependencyTerms)
        {
            listResult.Output.ShouldNotContain(forbidden);
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
        string artifactsDirectory = Path.Combine(_workDirectory, "artifacts");
        Directory.CreateDirectory(feedDirectory);

        RunDotnet(
            "pack",
            $"\"{Path.Combine(repoRoot, "references", "Hexalith.Commons", "src", "libraries", "Hexalith.Commons.UniqueIds", "Hexalith.Commons.UniqueIds.csproj")}\" --configuration Release --output \"{feedDirectory}\" --artifacts-path \"{artifactsDirectory}\" {LocalPackVersionProperties}",
            repoRoot);
        RunDotnet(
            "pack",
            $"\"{Path.Combine(repoRoot, "references", "Hexalith.EventStore", "src", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Contracts.csproj")}\" --configuration Release --output \"{feedDirectory}\" --artifacts-path \"{artifactsDirectory}\" {LocalPackVersionProperties}",
            repoRoot);
        RunDotnet(
            "pack",
            $"\"{Path.Combine(repoRoot, "src", "Hexalith.Parties.Contracts", "Hexalith.Parties.Contracts.csproj")}\" --configuration Release --output \"{feedDirectory}\" --artifacts-path \"{artifactsDirectory}\" {LocalPackVersionProperties}",
            repoRoot);
        RunDotnet(
            "pack",
            $"\"{Path.Combine(repoRoot, "src", "Hexalith.Parties.Client", "Hexalith.Parties.Client.csproj")}\" --configuration Release --output \"{feedDirectory}\" --artifacts-path \"{artifactsDirectory}\" {LocalPackVersionProperties}",
            repoRoot);

        string clientPackagePath = Directory
            .GetFiles(feedDirectory, "Hexalith.Parties.Client.*.nupkg", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();

        return new PackageProbe(
            feedDirectory,
            clientPackagePath,
            ReadPackageVersion(clientPackagePath));
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

    private static DotnetResult RunDotnet(string command, string arguments, string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"{command} {arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {command} timed out. stdout: {stdout} stderr: {stderr}");
        }

        var result = new DotnetResult(stdout + stderr);
        process.ExitCode.ShouldBe(
            0,
            $"dotnet {command} {arguments} failed with exit code {process.ExitCode}.{Environment.NewLine}{result.Output}");
        return result;
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

    private sealed record DotnetResult(string Output);

    private sealed record PackageProbe(string FeedDirectory, string ClientPackagePath, string ClientPackageVersion);
}
