using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.PackageTests;

using Shouldly;

using static Hexalith.Parties.Tests.FitnessTests.RepositoryTestProcess;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Checks executable runtime declarations and package dependency boundaries.</summary>
public sealed class ExternalRuntimeGraphTests
{
    private static readonly string[] RuntimeSourceSelectors =
    [
        "HexalithEventStoreFromSource", "HexalithCommonsFromSource", "HexalithCommonsHttpFromSource",
        "HexalithCommonsServiceDefaultsFromSource", "HexalithFrontComposerFromSource", "HexalithTenantsFromSource",
        "HexalithMemoriesFromSource", "HexalithMcpCliFromSource", "HexalithPolymorphicSerializationsFromSource",
    ];
    private static readonly string[] RuntimeDependencyModules =
    [
        "Hexalith.EventStore", "Hexalith.Commons", "Hexalith.FrontComposer", "Hexalith.Tenants",
        "Hexalith.Memories", "Hexalith.McpCli", "Hexalith.PolymorphicSerializations",
    ];
    private static readonly string[] RuntimeReferenceContextMetadata =
    [
        "AdditionalProperties", "Properties", "GlobalPropertiesToRemove", "SetConfiguration", "SetTargetFramework", "SetPlatform",
    ];

    /// <summary>Verifies the runtime projects that Aspire launches by path instead of solution references.</summary>
    [Fact]
    public void ExternalRuntimeInventoryMatchesAppHostDeclarations()
    {
        string root = RepositoryRoot.Locate();
        string[] actual = ReadExternalRuntimeProjects(root);
        string[] expected =
        [
            "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj",
            "tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj",
            "tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj",
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj",
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj",
            "references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.DomainService/Hexalith.Memories.DomainService.csproj",
            "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj",
            "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj",
        ];
        actual.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
        string preparation = File.ReadAllText(Path.Combine(root, "scripts", "prepare-aspire-runtime-hosts.sh"));
        foreach (string project in actual) { preparation.ShouldContain(project); }
    }

    /// <summary>Checks every selected external runtime dependency before starting a topology.</summary>
    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public void ExternalRuntimeGraphsMatchTheIndependentlyEvaluatedPartiesPackageSelection(string configuration)
    {
        string root = RepositoryRoot.Locate();
        string expectedVersion = EffectivePackageVersions.ReadProperty(root, "HexalithEventStoreVersion", configuration: configuration);
        IReadOnlyDictionary<string, EvaluatedProjectGraph> graphs = ReadExternalRuntimeGraphs(root, ReadExternalRuntimeProjects(root), expectedVersion, configuration,
            $"-p:HexalithEventStoreVersion={expectedVersion}");

        graphs.Count.ShouldBeGreaterThan(7, "The runtime preflight must inspect transitive projects as well as both gateway choices and their peers.");
        graphs.Keys.ShouldContain(path => path.EndsWith("Hexalith.Tenants.csproj", StringComparison.Ordinal));
        graphs.Keys.ShouldContain(path => path.EndsWith("Hexalith.Memories.Server.csproj", StringComparison.Ordinal));
        graphs.Values.SelectMany(static graph => graph.PackageReferences)
            .ShouldContain("Hexalith.EventStore.DomainService");
    }

    /// <summary>Rejects package skew, hidden source selection, and missing transitive runtime projects.</summary>
    [Theory]
    [InlineData("version")]
    [InlineData("override")]
    [InlineData("source")]
    [InlineData("source-uppercase")]
    [InlineData("project")]
    [InlineData("missing")]
    public void ExternalRuntimePreflightRejectsIncompatibleSelectedDependencies(string failure)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            new XDocument(new XElement("Project", new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include", "RuntimeConsumer.csproj")))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            XElement package = new("PackageReference", new XAttribute("Include", "Hexalith.EventStore.DomainService"));
            if (failure is "version" or "override")
            {
                package.Add(new XAttribute(failure == "override" ? "VersionOverride" : "Version", "8.0.0"));
            }

            XElement items = new("ItemGroup", package,
                new XElement("PackageVersion", new XAttribute("Include", "Hexalith.EventStore.DomainService"), new XAttribute("Version", "9.0.0")));
            if (failure == "missing")
            {
                items.Add(new XElement("ProjectReference", new XAttribute("Include", "Missing.csproj")));
            }
            else if (failure == "project")
            {
                File.WriteAllText(Path.Combine(root, "Hexalith.EventStore.Contracts.csproj"), "<Project />");
                items.Add(new XElement("ProjectReference", new XAttribute("Include", "Hexalith.EventStore.Contracts.csproj")));
            }

            new XDocument(new XElement("Project", new XElement("PropertyGroup",
                new XElement("HexalithEventStoreVersion", "9.0.0"),
                new XElement("HexalithEventStoreFromSource", failure == "source" ? "true" : failure == "source-uppercase" ? "True" : string.Empty)), items))
                .Save(Path.Combine(root, "RuntimeConsumer.csproj"));

            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain(failure == "missing" ? "Missing.csproj" : "RuntimeConsumer.csproj");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Unsupported edge contexts cannot hide the child's effective version selection.</summary>
    [Theory]
    [InlineData("AdditionalProperties", "DependencyVersion=8.0.0", "")]
    [InlineData("Properties", "DependencyVersion=8.0.0", "")]
    [InlineData("GlobalPropertiesToRemove", "DependencyVersion", "")]
    [InlineData("GlobalPropertiesToRemove", "WebPublishProfileFile", "selected-profile")]
    [InlineData("SetConfiguration", "Configuration=Debug", "")]
    [InlineData("SetTargetFramework", "TargetFramework=net9.0", "")]
    [InlineData("SetPlatform", "Platform=x86", "")]
    public void ExternalRuntimePreflightRejectsUnevaluatedReferenceContexts(string metadata, string value, string publishProfile)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            new XDocument(new XElement("Project", new XElement("PropertyGroup",
                new XElement("DependencyVersion", "9.0.0"), new XElement("WebPublishProfileFile", publishProfile)),
                new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", "Consumer.csproj"),
                    new XAttribute(metadata, value)))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            File.WriteAllText(Path.Combine(root, "Consumer.csproj"), """
                <Project><PropertyGroup><DependencyVersion Condition="'$(DependencyVersion)' == ''">9.0.0</DependencyVersion></PropertyGroup>
                <ItemGroup><PackageVersion Include="Hexalith.EventStore.Contracts" Version="$(DependencyVersion)" />
                <PackageReference Include="Hexalith.EventStore.Contracts" /></ItemGroup></Project>
                """);

            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain($"unsupported ProjectReference context metadata {metadata}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The package and runtime readers must both honor the startup/build configuration.</summary>
    [Theory]
    [InlineData("Debug", "8.0.0")]
    [InlineData("Release", "9.0.0")]
    public void ExternalRuntimePreflightEvaluatesConfigurationSpecificSelections(string configuration, string expected)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-configuration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            const string versionProperties = """
                <HexalithEventStoreVersion Condition="'$(Configuration)' == 'Debug'">8.0.0</HexalithEventStoreVersion>
                <HexalithEventStoreVersion Condition="'$(Configuration)' == 'Release'">9.0.0</HexalithEventStoreVersion>
                """;
            File.WriteAllText(Path.Combine(root, "Expected.csproj"), $"<Project><PropertyGroup>{versionProperties}</PropertyGroup></Project>");
            XElement package = new("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts"));
            XDocument runtime = XDocument.Parse($"<Project><PropertyGroup>{versionProperties}</PropertyGroup><ItemGroup><PackageVersion Include=\"Hexalith.EventStore.Contracts\" Version=\"$(HexalithEventStoreVersion)\" /></ItemGroup></Project>");
            runtime.Root!.Element("ItemGroup")!.Add(package);
            string project = Path.Combine(root, "Runtime.csproj");
            runtime.Save(project);
            string independentlyEvaluatedVersion = EffectivePackageVersions.ReadProperty(root, "HexalithEventStoreVersion", "Expected.csproj", configuration);
            independentlyEvaluatedVersion.ShouldBe(expected);
            ReadExternalRuntimeGraphs(root, [project], independentlyEvaluatedVersion, configuration).Count.ShouldBe(1);

            package.Add(new XAttribute("VersionOverride", "7.0.0"));
            package.Add(new XAttribute("Condition", $"'$(Configuration)' == '{configuration}'"));
            runtime.Save(project);
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, [project], independentlyEvaluatedVersion, configuration))
                .Message.ShouldContain("independently evaluated Parties EventStore package selection");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>An outer multitarget evaluation cannot certify packages selected only by inner builds.</summary>
    [Fact]
    public void ExternalRuntimePreflightRejectsUnexpandedTargetFrameworks()
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-frameworks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Runtime.csproj"), """
                <Project><PropertyGroup><TargetFrameworks>net9.0;net10.0</TargetFrameworks></PropertyGroup>
                <ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
                <PackageReference Include="Hexalith.EventStore.Contracts" Version="8.0.0" /></ItemGroup></Project>
                """);
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain("unexpanded TargetFrameworks");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Every known external source selector participates in the runtime package boundary.</summary>
    [Theory]
    [InlineData("HexalithCommonsFromSource")]
    [InlineData("HexalithCommonsHttpFromSource")]
    [InlineData("HexalithCommonsServiceDefaultsFromSource")]
    [InlineData("HexalithFrontComposerFromSource")]
    [InlineData("HexalithTenantsFromSource")]
    [InlineData("HexalithMemoriesFromSource")]
    [InlineData("HexalithMcpCliFromSource")]
    [InlineData("HexalithPolymorphicSerializationsFromSource")]
    public void ExternalRuntimePreflightRejectsOtherSourceSelectors(string selector)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-selector-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            new XDocument(new XElement("Project", new XElement("PropertyGroup", new XElement(selector, "True"))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain(selector);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Missing selector metadata cannot conceal cross-module source references.</summary>
    [Theory]
    [InlineData("Hexalith.Commons.Http")]
    [InlineData("Hexalith.FrontComposer.Contracts")]
    [InlineData("Hexalith.Tenants.Contracts")]
    [InlineData("Hexalith.Memories.Contracts")]
    [InlineData("Hexalith.McpCli.Abstractions")]
    [InlineData("Hexalith.PolymorphicSerializations")]
    public void ExternalRuntimePreflightRejectsOtherHiddenSourceProjects(string dependency)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-hidden-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, dependency + ".csproj"), "<Project />");
            new XDocument(new XElement("Project", new XElement("ItemGroup", new XElement("ProjectReference",
                new XAttribute("Include", dependency + ".csproj")))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain("source projects");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Shared dependencies and cycles are evaluated once without hiding their packages.</summary>
    [Fact]
    public void ExternalRuntimePreflightDeduplicatesSelectedReferencesAndCycles()
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-cycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach ((string project, string referencedProject) in new[] { ("Runtime", "Consumer"), ("Consumer", "Runtime") })
            {
                new XDocument(new XElement("Project", new XElement("ItemGroup",
                    new XElement("ProjectReference", new XAttribute("Include", referencedProject + ".csproj")),
                    new XElement("PackageVersion", new XAttribute("Include", "Hexalith.EventStore.Contracts"), new XAttribute("Version", "9.0.0")),
                    new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts")))))
                    .Save(Path.Combine(root, project + ".csproj"));
            }

            ReadExternalRuntimeGraphs(root, ["Runtime.csproj", "Runtime.csproj"], "9.0.0")
                .Count.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Unrecognized runtime project expressions cannot silently escape the inventory.</summary>
    [Fact]
    public void ExternalRuntimeInventoryRejectsUnsupportedAddProjectArguments()
    {
        string root = RepositoryRoot.Locate();
        string appHost = File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/Program.cs"));
        Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeProjects(root, appHost + "\nbuilder.AddProject(\"extra\", \"literal/path.csproj\");"))
            .Message.ShouldContain("unsupported AddProject");
    }

    /// <summary>Variable path segments cannot be ignored while harvesting surrounding string literals.</summary>
    [Fact]
    public void ExternalRuntimeInventoryRejectsNonliteralPathCombineSegments()
    {
        string root = RepositoryRoot.Locate();
        string appHost = File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/Program.cs"));
        appHost = appHost.Replace("Path.Combine(\"src\", \"Hexalith.EventStore.Admin.Server.Host\",", "Path.Combine(\"src\", extraSegment, \"Hexalith.EventStore.Admin.Server.Host\",", StringComparison.Ordinal);
        Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeProjects(root, appHost))
            .Message.ShouldContain("nonliteral Path.Combine segments");
    }

    /// <summary>The explicit package/native gateway decision cannot be replaced with an unrecognized selector.</summary>
    [Fact]
    public void ExternalRuntimeInventoryRejectsUnsupportedGatewaySelection()
    {
        string root = RepositoryRoot.Locate();
        string appHost = File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/Program.cs"));
        appHost = appHost.Replace("Parties:Testing:UsePackagedGateway", "Other:GatewaySelection", StringComparison.Ordinal);
        Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeProjects(root, appHost))
            .Message.ShouldContain("unsupported ResolveGateway arguments");
    }

    /// <summary>Selected SDK properties flow to external evaluation without hiding explicit package skew.</summary>
    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public void ExternalRuntimeCommandContextPreservesExactPackageValidation(string configuration)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-command-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string project = Path.Combine(root, "Runtime.csproj");
            XDocument runtime = XDocument.Parse("<Project><PropertyGroup><HexalithEventStoreVersion>3.128.4</HexalithEventStoreVersion></PropertyGroup><ItemGroup><PackageVersion Include=\"Hexalith.EventStore.Contracts\" Version=\"$(HexalithEventStoreVersion)\" /><PackageReference Include=\"Hexalith.EventStore.Contracts\" /></ItemGroup></Project>");
            runtime.Save(project);
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, [project], "3.129.0", configuration));
            ReadExternalRuntimeGraphs(root, [project], "3.129.0", configuration, "-p:HexalithEventStoreVersion=3.129.0").Count.ShouldBe(1);
            runtime.Descendants("PackageReference").Single().SetAttributeValue("VersionOverride", "3.128.4");
            runtime.Save(project);
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, [project], "3.129.0", configuration, "-p:HexalithEventStoreVersion=3.129.0"))
                .Message.ShouldContain("independently evaluated Parties EventStore package selection");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Runtime declarations cannot introduce dependencies outside the root submodule manifest.</summary>
    [Fact]
    public void ExternalRuntimeInventoryRejectsUndeclaredRootSubmodules()
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-inventory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, ".gitmodules"), string.Empty);
            const string appHost = """
                string runtimePath = ReferenceProjectResolver.ResolveRequired(repositoryRoot,
                    "Hexalith.Unlisted", Path.Combine("src", "Runtime.csproj"));
                builder.AddProject("runtime", runtimePath);
                """;
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeProjects(root, appHost))
                .Message.ShouldContain("declared root submodule");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] ReadExternalRuntimeProjects(string root, string? appHost = null)
    {
        appHost ??= File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/Program.cs"));
        HashSet<string> rootSubmodules = Regex.Matches(File.ReadAllText(Path.Combine(root, ".gitmodules")), @"(?m)^\s*path\s*=\s*(?<path>[^\r\n]+)")
            .Select(static match => match.Groups["path"].Value.Trim()).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string[]> declarations = new(StringComparer.Ordinal);
        foreach (Match declaration in Regex.Matches(appHost,
                     "(?<variable>[A-Za-z][A-Za-z0-9]*)\\s*=\\s*(?:ReferenceProjectResolver\\.ResolveRequired|ResolveOptionalReferenceProjectPath)\\s*\\(\\s*(?:repositoryRoot,\\s*)?\"(?<module>Hexalith\\.[A-Za-z.]+)\"\\s*,\\s*Path\\.Combine\\((?<segments>[^)]+)\\)",
                     RegexOptions.CultureInvariant))
        {
            string moduleRoot = "references/" + declaration.Groups["module"].Value;
            rootSubmodules.ShouldContain(moduleRoot, "Runtime projects must belong to a declared root submodule.");
            Regex.IsMatch(declaration.Groups["segments"].Value, "\\A\\s*\"[^\"]+\"\\s*(?:,\\s*\"[^\"]+\"\\s*)*\\z", RegexOptions.CultureInvariant)
                .ShouldBeTrue($"{declaration.Groups["variable"].Value}: nonliteral Path.Combine segments are unsupported by the external runtime inventory");
            string[] segments = Regex.Matches(declaration.Groups["segments"].Value, "\"(?<segment>[^\"]+)\"")
                .Select(static match => match.Groups["segment"].Value).ToArray();
            string project = moduleRoot + "/" + string.Join('/', segments);
            File.Exists(RepositoryDependencyPaths.Resolve(root, project)).ShouldBeTrue($"AppHost external runtime project is missing: {project}");
            declarations.Add(declaration.Groups["variable"].Value, [project]);
        }

        MatchCollection selectionCalls = Regex.Matches(appHost, @"ReferenceProjectResolver\.Resolve(?:Gateway|Sample)\s*\(", RegexOptions.CultureInvariant);
        MatchCollection selections = Regex.Matches(appHost,
            "(?<variable>[A-Za-z][A-Za-z0-9]*)\\s*=\\s*ReferenceProjectResolver\\.Resolve(?<kind>Gateway|Sample)\\s*\\(\\s*repositoryRoot\\s*,\\s*builder\\.Configuration\\.GetValue<bool>\\(\"Parties:Testing:UsePackagedGateway\"\\)\\s*,\\s*builder\\.ExecutionContext\\.IsPublishMode\\s*\\)",
            RegexOptions.CultureInvariant);
        selections.Count.ShouldBe(selectionCalls.Count, "AppHost contains unsupported ResolveGateway arguments or unsupported ResolveSample arguments; explicit package selection must be inventoried exactly.");
        foreach (Match selection in selections)
        {
            const string moduleRoot = "references/Hexalith.EventStore";
            rootSubmodules.ShouldContain(moduleRoot, "The native runtime must belong to a declared root submodule.");
            string resolver = File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/ReferenceProjectResolver.cs"));
            resolver.ShouldContain("return ResolveRequired(repositoryRoot, \"Hexalith.EventStore\",");
            bool sample = selection.Groups["kind"].Value == "Sample";
            string nativeProject = sample
                ? moduleRoot + "/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj"
                : moduleRoot + "/src/Hexalith.EventStore/Hexalith.EventStore.csproj";
            string hostName = sample ? "Hexalith.Parties.EventStoreSample.TestHost" : "Hexalith.Parties.EventStoreGateway.TestHost";
            resolver.ShouldContain(sample
                ? "Path.Combine(\"samples\", \"Hexalith.EventStore.Sample\", \"Hexalith.EventStore.Sample.csproj\")"
                : "Path.Combine(\"src\", \"Hexalith.EventStore\", \"Hexalith.EventStore.csproj\")");
            resolver.ShouldContain($"Path.Combine(repositoryRoot.FullName, \"tests\", \"{hostName}\",");
            resolver.ShouldContain($"\"{hostName}.csproj\")");
            string[] selectedProjects = [nativeProject, $"tests/{hostName}/{hostName}.csproj"];
            foreach (string project in selectedProjects)
            {
                File.Exists(RepositoryDependencyPaths.Resolve(root, project)).ShouldBeTrue($"Selectable gateway is missing: {project}");
            }
            declarations.Add(selection.Groups["variable"].Value, selectedProjects);
        }

        MatchCollection runtimeCalls = Regex.Matches(appHost, @"\bbuilder\s*\.\s*AddProject\s*\(", RegexOptions.CultureInvariant);
        MatchCollection recognizedCalls = Regex.Matches(appHost, "builder\\s*\\.\\s*AddProject\\s*\\(\\s*\"[^\"]+\"\\s*,\\s*(?<variable>[A-Za-z][A-Za-z0-9]*)\\s*\\)", RegexOptions.CultureInvariant);
        recognizedCalls.Count.ShouldBe(runtimeCalls.Count, "AppHost contains unsupported AddProject arguments; update the external runtime inventory parser.");
        return recognizedCalls
            .SelectMany(match => declarations.TryGetValue(match.Groups["variable"].Value, out string[]? projects)
                ? projects : throw new InvalidOperationException($"No external runtime project declaration for {match.Groups["variable"].Value}."))
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyDictionary<string, EvaluatedProjectGraph> ReadExternalRuntimeGraphs(
        string root, IEnumerable<string> runtimeProjects, string expectedEventStoreVersion, string configuration = "Release",
        params string[] commandSelectedProperties)
    {
        Dictionary<string, EvaluatedProjectGraph> graphs = new(StringComparer.Ordinal);
        Queue<string> pending = new(runtimeProjects.Select(path => RepositoryDependencyPaths.Resolve(root, path)));
        while (pending.TryDequeue(out string? project))
        {
            if (graphs.ContainsKey(project))
            {
                continue;
            }

            File.Exists(project).ShouldBeTrue($"External runtime project does not exist: {project}");
            EvaluatedProjectGraph graph = EvaluateProjectGraphForConfiguration(root, project, configuration, useSource: null, commandSelectedProperties);
            string.IsNullOrWhiteSpace(graph.Properties["TargetFrameworks"])
                .ShouldBeTrue($"{project}: unexpanded TargetFrameworks are unsupported by the external runtime preflight; evaluate an explicit inner framework context first");
            foreach (string selector in RuntimeSourceSelectors)
            {
                string.Equals(graph.Properties[selector], "true", StringComparison.OrdinalIgnoreCase)
                    .ShouldBeFalse($"{project}: unexpected {selector} source selection in the runtime package graph ({configuration})");
            }
            foreach (IReadOnlyDictionary<string, string> reference in graph.ProjectMetadata)
            {
                foreach (string metadata in RuntimeReferenceContextMetadata)
                {
                    string value = reference.GetValueOrDefault(metadata, string.Empty);
                    // The Web SDK removes this unset publishing-only property on every web edge.
                    bool removesUnsetPublishProfile = metadata == "GlobalPropertiesToRemove"
                        && value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .All(static name => name == "WebPublishProfileFile")
                        && string.IsNullOrEmpty(graph.Properties["WebPublishProfileFile"]);
                    (string.IsNullOrWhiteSpace(value) || removesUnsetPublishProfile).ShouldBeTrue(
                        $"{project}: {reference["FullPath"]} has unsupported ProjectReference context metadata {metadata}; refusing to evaluate it under the parent's context");
                }
            }
            foreach (string package in graph.PackageReferences.Where(static identity => IsDependencyIdentity(identity, "Hexalith.EventStore")))
            {
                graph.PackageMetadata[package]["EffectiveVersion"].ShouldBe(expectedEventStoreVersion,
                    $"{project}: {package} must match the independently evaluated Parties EventStore package selection");
            }

            foreach (string module in RuntimeDependencyModules)
            {
                string moduleRoot = RepositoryDependencyPaths.Resolve(root, "references/" + module) + Path.DirectorySeparatorChar;
                if (!project.StartsWith(moduleRoot, StringComparison.Ordinal))
                {
                    graph.ProjectReferences.Any(reference => IsDependencyIdentity(Path.GetFileNameWithoutExtension(reference), module))
                        .ShouldBeFalse($"{project}: external package consumers must not select {module} source projects");
                }
            }

            graphs.Add(project, graph);
            foreach (string reference in graph.ProjectReferences)
            {
                pending.Enqueue(Path.GetFullPath(reference));
            }
        }

        return graphs;
    }

    private static bool IsDependencyIdentity(string identity, string dependency)
        => string.Equals(identity, dependency, StringComparison.Ordinal)
            || identity.StartsWith(dependency + ".", StringComparison.Ordinal);

    private static string[] DeclaredDependencyReferences(string projectFile, string dependency, string? itemType)
        => XDocument.Load(projectFile).Descendants()
            .Where(item => (item.Name.LocalName is "PackageReference" or "ProjectReference")
                && (itemType is null || string.Equals(item.Name.LocalName, itemType, StringComparison.Ordinal)))
            .Select(item => item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value ?? string.Empty)
            .Select(static identity => identity.EndsWith(".csproj", StringComparison.Ordinal)
                ? Path.GetFileNameWithoutExtension(identity.Replace('\\', '/'))
                : identity)
            .Where(identity => IsDependencyIdentity(identity, dependency))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void AssertPackageConsumer(
        EvaluatedProjectGraph graph,
        string projectFile,
        string dependency,
        string expectedVersion)
    {
        string[] expectedPackages = DeclaredDependencyReferences(projectFile, dependency, "PackageReference");
        expectedPackages.ShouldNotBeEmpty(projectFile);
        graph.PackageReferences.Where(identity => IsDependencyIdentity(identity, dependency))
            .Order(StringComparer.Ordinal).ShouldBe(expectedPackages, Case.Sensitive, projectFile);
        foreach (string identity in expectedPackages)
        {
            graph.PackageMetadata[identity]["EffectiveVersion"].ShouldBe(expectedVersion,
                $"{projectFile}: {identity} effective version (VersionOverride, Version, then central PackageVersion)");
        }

        graph.ProjectReferences.Any(path => IsDependencyIdentity(Path.GetFileNameWithoutExtension(path), dependency))
            .ShouldBeFalse($"{projectFile} package graph");
    }

    private static void AssertSourceConsumer(
        EvaluatedProjectGraph graph,
        string projectFile,
        string dependency,
        string expectedVersion,
        string expectedRoot)
    {
        string[] expectedProjects = DeclaredDependencyReferences(projectFile, dependency, "ProjectReference");
        if (expectedProjects.Length == 0)
        {
            // The package-only Gateway test host deliberately has no source branch.
            AssertPackageConsumer(graph, projectFile, dependency, expectedVersion);
            return;
        }

        string[] selectedProjects = graph.ProjectReferences
            .Where(path => IsDependencyIdentity(Path.GetFileNameWithoutExtension(path), dependency))
            .ToArray();
        selectedProjects.Select(static path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal).ShouldBe(expectedProjects, Case.Sensitive, projectFile);
        selectedProjects.ShouldAllBe(path => path.StartsWith(
            Path.GetFullPath(expectedRoot).Replace('\\', '/') + "/", StringComparison.Ordinal),
            $"{projectFile}: selected projects must use the supplied consumer root.");
        graph.PackageReferences.Any(identity => IsDependencyIdentity(identity, dependency))
            .ShouldBeFalse($"{projectFile} source graph");
    }

    [Theory]
    [InlineData("Hexalith.McpCli")]
    [InlineData("references/Hexalith.McpCli")]
    public void McpCliSiblingLayoutsDoNotSelectSourceWithoutToolMapping(string relativeMcpCliRoot)
    {
        string root = RepositoryRoot.Locate();
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-mcpcli-layout-{Guid.NewGuid():N}");
        string partiesRoot = Path.Combine(temporaryRoot, "Hexalith.Parties");
        string contractsRelativePath = "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj";
        string contractsProject = Path.Combine(partiesRoot, contractsRelativePath);
        string abstractionsProject = Path.Combine(temporaryRoot, relativeMcpCliRoot,
            "src/Hexalith.McpCli.Abstractions/Hexalith.McpCli.Abstractions.csproj");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(contractsProject)!);
            Directory.CreateDirectory(Path.GetDirectoryName(abstractionsProject)!);
            File.Copy(Path.Combine(root, "Directory.Build.props"), Path.Combine(partiesRoot, "Directory.Build.props"));
            File.Copy(Path.Combine(root, contractsRelativePath), contractsProject);
            File.WriteAllText(abstractionsProject, "<Project />");
            Directory.Exists(Path.Combine(partiesRoot, "references/Hexalith.McpCli")).ShouldBeFalse();

            EvaluatedProjectGraph graph = EvaluateProjectGraph(partiesRoot, contractsProject, useSource: true);

            graph.Properties["HexalithMcpCliFromSource"].ShouldNotBe("true");
            graph.ProjectReferences.ShouldNotContain(Path.GetFullPath(abstractionsProject).Replace('\\', '/'));
            graph.PackageReferences.ShouldContain("Hexalith.McpCli.Abstractions");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Hexalith.McpCli")]
    [InlineData("references/Hexalith.McpCli")]
    public void ExplicitlyMappedMcpCliSiblingLayoutsSelectOnlyAbstractionsProject(string relativeMcpCliRoot)
    {
        string root = RepositoryRoot.Locate();
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-mcpcli-mapped-layout-{Guid.NewGuid():N}");
        string partiesRoot = Path.Combine(temporaryRoot, "Hexalith.Parties");
        string contractsRelativePath = "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj";
        string contractsProject = Path.Combine(partiesRoot, contractsRelativePath);
        string mcpCliRoot = Path.Combine(temporaryRoot, relativeMcpCliRoot);
        string abstractionsProject = Path.Combine(mcpCliRoot,
            "src/Hexalith.McpCli.Abstractions/Hexalith.McpCli.Abstractions.csproj");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(contractsProject)!);
            Directory.CreateDirectory(Path.GetDirectoryName(abstractionsProject)!);
            File.Copy(Path.Combine(root, "Directory.Build.props"), Path.Combine(partiesRoot, "Directory.Build.props"));
            File.Copy(Path.Combine(root, contractsRelativePath), contractsProject);
            File.WriteAllText(abstractionsProject, "<Project />");
            Directory.Exists(Path.Combine(partiesRoot, "references/Hexalith.McpCli")).ShouldBeFalse();

            EvaluatedProjectGraph graph = EvaluateProjectGraph(
                partiesRoot,
                contractsProject,
                useSource: true,
                $"-p:HexalithMcpCliRoot={mcpCliRoot}",
                "-p:HexalithMcpCliFromSource=true");

            graph.Properties["HexalithMcpCliRoot"].ShouldBe(mcpCliRoot);
            graph.Properties["HexalithMcpCliFromSource"].ShouldBe("true");
            graph.ProjectReferences.ShouldBe([Path.GetFullPath(abstractionsProject).Replace('\\', '/')]);
            graph.PackageReferences.ShouldNotContain("Hexalith.McpCli.Abstractions");
            graph.PackageReferences.ShouldContain("Hexalith.EventStore.Contracts");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("'$(HexalithEventStoreFromSorce)' == 'true'")]
    public void ConsumerDiscoveryRetainsIdentitiesWithRemovedOrMisspelledSelectors(string condition)
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-consumer-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string projectFile = Path.Combine(temporaryRoot, "Consumer.csproj");
            new XDocument(new XElement("Project", new XElement("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts"), new XAttribute("Condition", condition)),
                new XElement("ProjectReference", new XAttribute("Include", "source/Hexalith.EventStore.Contracts.csproj"), new XAttribute("Condition", condition)))))
                .Save(projectFile);
            DeclaredDependencyReferences(projectFile, "Hexalith.EventStore", null)
                .ShouldBe(["Hexalith.EventStore.Contracts"]);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("3.125.0", "", false)]
    [InlineData("", "3.125.0", false)]
    [InlineData("", "", true)]
    public void ConsumerPropertyFixturesRejectEffectiveVersionDriftAndMissingProjects(string version, string versionOverride, bool missingProject)
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-consumer-property-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string projectFile = Path.Combine(temporaryRoot, "Consumer.csproj");
            XElement items = new("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts"),
                    new XAttribute("Version", version), new XAttribute("VersionOverride", versionOverride)),
                new XElement("PackageVersion", new XAttribute("Include", "Hexalith.EventStore.Contracts"), new XAttribute("Version", "3.126.0")));
            if (missingProject)
            {
                items.Add(new XElement("ProjectReference", new XAttribute("Include", "missing.csproj")));
            }

            new XDocument(new XElement("Project", items)).Save(projectFile);
            Should.Throw<ShouldAssertException>(() =>
                {
                    EvaluatedProjectGraph graph = EvaluateProjectGraph(temporaryRoot, projectFile, useSource: null);
                    AssertPackageConsumer(graph, projectFile, "Hexalith.EventStore", "3.126.0");
                })
                .Message.ShouldContain(missingProject ? "selected source project does not exist" : "effective version");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static EvaluatedProjectGraph EvaluateProjectGraph(
        string root,
        string projectFile,
        bool? useSource,
        params string[] mappedSourceProperties)
        => EvaluateProjectGraphForConfiguration(root, projectFile, useSource == true ? "Debug" : "Release", useSource, mappedSourceProperties);

    private static EvaluatedProjectGraph EvaluateProjectGraphForConfiguration(
        string root,
        string projectFile,
        string configuration,
        bool? useSource,
        params string[] mappedSourceProperties)
    {
        string relativeProject = Path.GetRelativePath(root, projectFile);
        string output = RunProcess(
            root,
            "dotnet",
            [
                "msbuild",
                relativeProject,
                "-nologo",
                "-getProperty:HexalithEventStoreVersion,HexalithCommonsVersion,HexalithMemoriesVersion,UseHexalithProjectReferences,HexalithMcpCliRoot,TargetFramework,TargetFrameworks,WebPublishProfileFile," + string.Join(',', RuntimeSourceSelectors),
                "-getItem:PackageReference,PackageVersion,ProjectReference",
                .. (useSource.HasValue ? new[] { $"-p:UseHexalithProjectReferences={useSource.Value.ToString().ToLowerInvariant()}" } : []),
                $"-p:Configuration={configuration}",
                "-p:MinVerVersionOverride=1.0.0",
                .. mappedSourceProperties
            ]);
        using JsonDocument document = ParseMsbuildJson(output, relativeProject);
        Dictionary<string, string> properties = document.RootElement.GetProperty("Properties")
            .EnumerateObject()
            .ToDictionary(static property => property.Name, static property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
        JsonElement items = document.RootElement.GetProperty("Items");
        string[] packages = ReadEvaluatedItems(items, "PackageReference", "Identity");
        string[] projects = ReadEvaluatedItems(items, "ProjectReference", "FullPath")
            .Select(static path => path.Replace('\\', '/'))
            .ToArray();
        foreach (string project in projects)
        {
            File.Exists(project).ShouldBeTrue($"{relativeProject}: selected source project does not exist: {project}");
        }

        IReadOnlyDictionary<string, string>[] projectMetadata = items.GetProperty("ProjectReference").EnumerateArray()
            .Select(static item => (IReadOnlyDictionary<string, string>)item.EnumerateObject()
                .ToDictionary(static metadata => metadata.Name, static metadata => metadata.Value.GetString() ?? string.Empty, StringComparer.Ordinal))
            .ToArray();
        return new EvaluatedProjectGraph(properties, packages, projects, ReadPackageMetadata(items), projectMetadata);
    }

    /// <summary>Consumes one structured MSBuild payload while leaving surrounding diagnostics alone.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("Restore complete.\n")]
    [InlineData("warning: unresolved {Property}\n")]
    [InlineData("warning: {\"level\":\"diagnostic\"}\n")]
    [InlineData("{\"level\":\"diagnostic\"}\n")]
    public void MsbuildJsonParsingIgnoresSurroundingDiagnostics(string prefix)
    {
        const string payload = """{"Properties":{"Value":"brace } and { and quote \""},"Items":{"PackageReference":[{"Identity":"example"}]}}""";
        using JsonDocument document = ParseMsbuildJson(prefix + payload + "\nBuild succeeded. {not JSON}", "fixture.csproj");

        document.RootElement.GetProperty("Properties").GetProperty("Value").GetString().ShouldBe("brace } and { and quote \"");
        document.RootElement.GetProperty("Items").GetProperty("PackageReference")[0].GetProperty("Identity").GetString().ShouldBe("example");
    }

    /// <summary>Absent or incomplete payloads must not become successful graph evaluations.</summary>
    [Fact]
    public void MsbuildJsonParsingRejectsMissingPayload()
        => Should.Throw<JsonException>(() => ParseMsbuildJson("diagnostics without a payload", "fixture.csproj"));

    /// <summary>Malformed or incomplete JSON remains an explicit parse failure.</summary>
    [Theory]
    [InlineData("preamble\n{\"Properties\":{")]
    [InlineData("preamble\n{invalid}")]
    [InlineData("{\"Properties\":{\"Value\":\"outer\"},\"Items\":{\"Properties\":{},\"Items\":{}}")]
    public void MsbuildJsonParsingRejectsInvalidPayload(string output)
        => Should.Throw<JsonException>(() => ParseMsbuildJson(output, "fixture.csproj"));

    /// <summary>Reports the owning project and bounded stdout when the graph payload is unavailable.</summary>
    [Fact]
    public void MsbuildJsonParsingFailureRetainsBoundedProjectDiagnostics()
    {
        JsonException failure = Should.Throw<JsonException>(() => ParseMsbuildJson("restore failed " + new string('x', 2_000), "fixture.csproj"));
        failure.Message.ShouldContain("fixture.csproj");
        failure.Message.ShouldContain("restore failed");
        failure.Message.Length.ShouldBeLessThan(1_200);
    }

    private static JsonDocument ParseMsbuildJson(string output, string projectFile)
    {
        for (int start = output.IndexOf('{', StringComparison.Ordinal); start >= 0; start = output.IndexOf('{', start + 1))
        {
            // MSBuild emits its root object at the start of a line. Braces inside diagnostics
            // or a malformed payload must never become nested replacement graph roots.
            if (start > 0 && output[start - 1] != '\n')
            {
                continue;
            }

            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(output[start..]);
                var reader = new Utf8JsonReader(payload);
                JsonDocument document = JsonDocument.ParseValue(ref reader);
                if (document.RootElement.TryGetProperty("Properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("Items", out JsonElement items) && items.ValueKind == JsonValueKind.Object)
                {
                    return document;
                }

                document.Dispose();
                start += Encoding.UTF8.GetCharCount(payload.AsSpan(0, (int)reader.BytesConsumed)) - 1;
            }
            catch (JsonException)
            {
                // A malformed independent object cannot be repaired by accepting one of its children.
                break;
            }
        }

        string diagnostic = output[..Math.Min(output.Length, 1_000)].Replace('\r', ' ').Replace('\n', ' ');
        throw new JsonException($"MSBuild produced no complete project graph JSON for {projectFile}: {diagnostic}");
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadPackageMetadata(JsonElement items)
    {
        Dictionary<string, string> centralVersions = items.TryGetProperty("PackageVersion", out JsonElement versions)
            ? versions.EnumerateArray().ToDictionary(
                static item => item.GetProperty("Identity").GetString()!,
                static item => ReadItemMetadata(item, "Version"),
                StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
        return items.GetProperty("PackageReference").EnumerateArray().ToDictionary(
            static item => item.GetProperty("Identity").GetString()!,
            item =>
            {
                string identity = item.GetProperty("Identity").GetString()!;
                string version = ReadItemMetadata(item, "Version");
                string versionOverride = ReadItemMetadata(item, "VersionOverride");
                string centralVersion = centralVersions.GetValueOrDefault(identity, string.Empty);
                return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Version"] = version,
                    ["VersionOverride"] = versionOverride,
                    ["CentralVersion"] = centralVersion,
                    ["EffectiveVersion"] = !string.IsNullOrWhiteSpace(versionOverride) ? versionOverride
                        : !string.IsNullOrWhiteSpace(version) ? version : centralVersion,
                };
            },
            StringComparer.Ordinal);
    }

    private static string ReadItemMetadata(JsonElement item, string name)
        => item.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;

    [Theory]
    [InlineData("", "", "3.126.0")]
    [InlineData("3.125.0", "", "3.125.0")]
    [InlineData("3.125.0", "3.124.0", "3.124.0")]
    public void EvaluatedPackageMetadataRetainsVersionAndOverridePrecedence(string version, string versionOverride, string expected)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            PackageReference = new[] { new { Identity = "Hexalith.EventStore.Contracts", Version = version, VersionOverride = versionOverride } },
            PackageVersion = new[] { new { Identity = "Hexalith.EventStore.Contracts", Version = "3.126.0" } },
        }));
        IReadOnlyDictionary<string, string> metadata = ReadPackageMetadata(document.RootElement)["Hexalith.EventStore.Contracts"];
        metadata["Version"].ShouldBe(version);
        metadata["VersionOverride"].ShouldBe(versionOverride);
        metadata["EffectiveVersion"].ShouldBe(expected);
    }

    private static string[] ReadEvaluatedItems(JsonElement items, string itemName, string metadataName)
        => items.TryGetProperty(itemName, out JsonElement values)
            ? values.EnumerateArray()
                .Select(item => item.TryGetProperty(metadataName, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty)
                .ToArray()
            : [];

}
