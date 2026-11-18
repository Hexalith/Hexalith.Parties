using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Checks shipped browser dependencies, package inventory and container provenance.</summary>
public sealed class ContainerProductTests
{
    private const int ExpectedPackageCount = 11;

    /// <summary>The locked browser tools enforce the selected distribution before installing.</summary>
    /// <param name="directory">The npm project directory.</param>
    [Theory]
    [InlineData("tests/e2e/")]
    public void NpmProjectsEnforceExactNodeAndNpm(string directory)
    {
        using JsonDocument manifest = JsonDocument.Parse(CiTestPaths.ReadRepoFile($"{directory}package.json"));
        using JsonDocument packageLock = JsonDocument.Parse(CiTestPaths.ReadRepoFile($"{directory}package-lock.json"));
        foreach (JsonElement project in new[] { manifest.RootElement, packageLock.RootElement.GetProperty("packages").GetProperty("") })
        {
            JsonElement engines = project.GetProperty("engines");
            engines.GetProperty("node").GetString().ShouldBe("26.13.1");
            engines.GetProperty("npm").GetString().ShouldBe("11.20.0");
        }

        CiTestPaths.ReadRepoFile($"{directory}.npmrc").Trim().ShouldBe("engine-strict=true");
    }

    [Theory]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "@axe-core/playwright", "^4.14.0", "4.14.0")]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "@playwright/test", "^1.65.1", "1.65.1")]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "@types/node", "^26.6.6", "26.6.6")]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "typescript", "^7.0.2", "7.0.2")]
    public void DependencyManifestsAndLocksMatchSelectedVersions(
        string manifestPath,
        string lockPath,
        string packageName,
        string expectedRange,
        string expectedResolvedVersion)
    {
        using JsonDocument manifest = JsonDocument.Parse(CiTestPaths.ReadRepoFile(manifestPath));
        using JsonDocument packageLock = JsonDocument.Parse(CiTestPaths.ReadRepoFile(lockPath));

        manifest.RootElement.GetProperty("devDependencies").GetProperty(packageName).GetString()
            .ShouldBe(expectedRange);
        JsonElement lockPackages = packageLock.RootElement.GetProperty("packages");
        lockPackages.GetProperty("").GetProperty("devDependencies").GetProperty(packageName).GetString()
            .ShouldBe(expectedRange);
        lockPackages.GetProperty($"node_modules/{packageName}").GetProperty("version").GetString()
            .ShouldBe(expectedResolvedVersion);
    }

    /// <summary>The UI requires ICU-capable runtime assets rather than invariant globalization.</summary>
    [Fact]
    public void UiContainerPreservesGlobalizationAndAlpineExtraRuntime()
    {
        XDocument project = XDocument.Parse(CiTestPaths.ReadRepoFile("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj"));

        project.Descendants("InvariantGlobalization").Single().Value.ShouldBe("false");
        project.Descendants("ContainerBaseImage").Single().Value.ShouldBe("mcr.microsoft.com/dotnet/aspnet:11.0-alpine-extra");
        project.Descendants("ContainerFamily").Single().Value.ShouldBe("alpine-extra");
    }

    [Fact]
    public void ServerAndIntegrationTestsReplaceObsoleteCollectionBehaviorWithNonParallelCollections()
    {
        string serverAssembly = CiTestPaths.ReadRepoFile("tests/Hexalith.Parties.Server.Tests/NonParallelCollection.cs");
        string integrationAssembly = CiTestPaths.ReadRepoFile("tests/Hexalith.Parties.IntegrationTests/NonParallelCollection.cs");
        string serverCollectionUse = CiTestPaths.ReadRepoFile(
            "tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCreateTests.cs");
        string integrationCollectionUse = CiTestPaths.ReadRepoFile(
            "tests/Hexalith.Parties.IntegrationTests/Events/TenantIsolationTests.cs");

        serverAssembly.ShouldContain("[CollectionDefinition(Name, DisableParallelization = true)]");
        serverAssembly.ShouldNotContain("CollectionBehavior");
        integrationAssembly.ShouldContain("[CollectionDefinition(Name, DisableParallelization = true)]");
        integrationAssembly.ShouldNotContain("CollectionBehavior");
        serverCollectionUse.ShouldContain("[Collection(\"Non-parallel\")]");
        integrationCollectionUse.ShouldContain("[Collection(\"Non-parallel\")]");

        foreach (string path in Directory.EnumerateFiles(
            CiTestPaths.RepoFile("tests/Hexalith.Parties.Server.Tests"),
            "*.cs",
            SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(
            CiTestPaths.RepoFile("tests/Hexalith.Parties.IntegrationTests"),
            "*.cs",
            SearchOption.AllDirectories)))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            File.ReadAllText(path).ShouldNotContain("CollectionBehavior");
        }
    }

    /// <summary>The existing publication reader rejects missing/invalid created labels with current shared source.</summary>
    [Fact]
    public async Task CurrentSharedValidatorPreservesThePublicationReaderContract()
    {
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = CiTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { CiTestPaths.RepoFile("tests/Hexalith.Parties.Ci.Tests/oci_validator_consumer_cases.py"), CiTestPaths.RepoFile("references/Hexalith.Builds") },
        };
        using Process process = Process.Start(start).ShouldNotBeNull();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            process.ExitCode.ShouldBe(0, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
        }
    }

    /// <summary>Executes the actual provenance targets at the image-creation boundary.</summary>
    /// <param name="created">The publisher-owned created instant.</param>
    /// <param name="valid">Whether the Gregorian calendar instant is valid.</param>
    [Theory]
    [InlineData("2024-02-29T23:59:59Z", true)]
    [InlineData("2000-02-29T00:00:00Z", true)]
    [InlineData("2026-04-30T12:34:56Z", true)]
    [InlineData("2023-02-29T00:00:00Z", false)]
    [InlineData("1900-02-29T00:00:00Z", false)]
    [InlineData("2024-04-31T00:00:00Z", false)]
    [InlineData("0000-01-01T00:00:00Z", false)]
    [InlineData("2026-01-01T24:00:00Z", false)]
    [InlineData("", false)]
    public async Task ContainerProvenanceTargetsValidateCalendarAndPreserveExactLabels(string created, bool valid)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"parties-provenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string labelsPath = Path.Combine(directory, "labels.txt");
            string projectPath = Path.Combine(directory, "fixture.proj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("EnableContainer", "true"),
                    new XElement("_IsSingleRIDBuild", "true"),
                    new XElement("ContainerProvenanceSourceSha", new string('a', 40)),
                    new XElement("ContainerProvenanceReleaseVersion", "1.2.3"),
                    new XElement("ContainerProvenanceCreated", created)),
                new XElement("Import", new XAttribute("Project", CiTestPaths.RepoFile("Directory.Build.targets"))),
                new XElement("Target", new XAttribute("Name", "_ParseItemsForPublishingSingleContainer"),
                    new XElement("ItemGroup", new XElement("ContainerLabel", new XAttribute("Include", "org.opencontainers.image.source"), new XAttribute("Value", "https")))),
                new XElement("Target", new XAttribute("Name", "_PublishSingleContainer"),
                    new XAttribute("DependsOnTargets", "_ParseItemsForPublishingSingleContainer"),
                    new XElement("WriteLinesToFile", new XAttribute("File", labelsPath),
                        new XAttribute("Lines", "@(ContainerLabel->'%(Identity)=%(Value)')"), new XAttribute("Overwrite", "true"))))).Save(projectPath);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    ArgumentList = { "msbuild", projectPath, "-nologo", "-v:q", "-t:_PublishSingleContainer" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            process.Start().ShouldBeTrue();
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Provenance target fixture exceeded its deadline.");
            }
            string diagnostic = await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true);
            if (valid)
            {
                process.ExitCode.ShouldBe(0, diagnostic);
                string[] labels = File.ReadAllLines(labelsPath);
                labels.Count(line => line.StartsWith("org.opencontainers.image.source=", StringComparison.Ordinal)).ShouldBe(1);
                labels.ShouldContain("org.opencontainers.image.source=https://github.com/Hexalith/Hexalith.Parties");
                labels.ShouldContain("org.opencontainers.image.created=" + created);
                labels.ShouldContain("org.opencontainers.artifact.created=" + created);
            }
            else
            {
                process.ExitCode.ShouldNotBe(0);
                diagnostic.ShouldContain("ContainerProvenanceCreated");
                File.Exists(labelsPath).ShouldBeFalse();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReleasePackageManifestDeclaresExactElevenPackageInventory()
    {
        using JsonDocument manifest = JsonDocument.Parse(CiTestPaths.ReadRepoFile("tools/release-packages.json"));
        JsonElement[] packages = manifest.RootElement.GetProperty("packages").EnumerateArray().ToArray();

        packages.Length.ShouldBe(ExpectedPackageCount);
        using JsonDocument policy = JsonDocument.Parse(CiTestPaths.ReadRepoFile("tools/nuget-trusted-publishing-policy.json"));
        policy.RootElement.GetProperty("packageIds").EnumerateArray().Select(package => package.GetString())
            .ShouldBe(packages.Select(package => package.GetProperty("id").GetString()));
        packages.Select(package => package.GetProperty("id").GetString()).ShouldBe(
        [
            "Hexalith.Parties.Contracts",
            "Hexalith.Parties.Client",
            "Hexalith.Parties.AdminPortal",
            "Hexalith.Parties.ConsumerPortal",
            "Hexalith.Parties.Picker",
            "Hexalith.Parties.Authentication",
            "Hexalith.Parties.Projections",
            "Hexalith.Parties.Security",
            "Hexalith.Parties.Testing",
            "Hexalith.Parties.Aspire",
            "Hexalith.Parties.EventStoreExtensions",
        ]);
        packages.Select(package => package.GetProperty("project").GetString()).ShouldBe(
        [
            "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj",
            "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
            "src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj",
            "src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj",
            "src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj",
            "src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj",
            "src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj",
            "src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj",
            "src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj",
            "src/Hexalith.Parties.Aspire/Hexalith.Parties.Aspire.csproj",
            "src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj",
        ]);
    }

    /// <summary>Uses shared source/version inputs and refuses conflicting legacy publisher inputs.</summary>
    /// <param name="sourceOverride">An optional legacy source override.</param>
    /// <param name="versionOverride">An optional legacy version override.</param>
    /// <param name="valid">Whether the frozen identities agree.</param>
    [Theory]
    [InlineData("", "", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "1.2.3", true)]
    [InlineData("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "", false)]
    [InlineData("", "2.0.0", false)]
    public void SharedReleaseIdentityFeedsContainerProvenanceAndRejectsConflicts(
        string sourceOverride, string versionOverride, bool valid)
    {
        string directory = Directory.CreateTempSubdirectory("parties-shared-provenance-").FullName;
        try
        {
            string project = Path.Combine(directory, "fixture.proj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("EnableContainer", "true"),
                    new XElement("HexalithPublishContainer", "true"),
                    new XElement("SourceRevisionId", new string('a', 40)),
                    new XElement("Version", "1.2.3"),
                    new XElement("ContainerProvenanceSourceSha", sourceOverride),
                    new XElement("ContainerProvenanceReleaseVersion", versionOverride),
                    new XElement("ContainerProvenanceCreated", "2026-11-16T12:34:56Z")),
                new XElement("Import", new XAttribute("Project", CiTestPaths.RepoFile("Directory.Build.targets"))))).Save(project);
            PackageTestProcessResult result = PackageTestProcess.Run("dotnet",
                ["msbuild", project, "-nologo", "-v:q", "-t:ValidateContainerProvenanceInputs",
                 "-getProperty:ContainerProvenanceSourceSha,ContainerProvenanceReleaseVersion"],
                directory, 30_000);
            if (valid)
            {
                result.ExitCode.ShouldBe(0, result.Diagnostic);
                using JsonDocument metadata = JsonDocument.Parse(result.StandardOutput);
                metadata.RootElement.GetProperty("Properties").GetProperty("ContainerProvenanceSourceSha")
                    .GetString().ShouldBe(new string('a', 40));
                metadata.RootElement.GetProperty("Properties").GetProperty("ContainerProvenanceReleaseVersion")
                    .GetString().ShouldBe("1.2.3");
            }
            else
            {
                result.ExitCode.ShouldNotBe(0, result.Diagnostic);
                result.Diagnostic.ShouldContain("must equal the shared");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
