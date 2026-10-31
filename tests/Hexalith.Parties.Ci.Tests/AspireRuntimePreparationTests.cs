using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Executes runtime preparation with a recording compiler to verify explicit package runtime selection.</summary>
public sealed class AspireRuntimePreparationTests
{
    /// <summary>Only the matching gateway/sample profile is built; invalid or incomplete profiles fail before compilation.</summary>
    /// <param name="profile">The explicit profile or an empty string for the package CI default.</param>
    /// <param name="missingHost">Whether the selected package gateway is deliberately absent.</param>
    /// <param name="missingSample">Whether the selected package sample is deliberately absent.</param>
    [Theory]
    [InlineData("", false, false)]
    [InlineData("packaged", false, false)]
    [InlineData("native", false, false)]
    [InlineData("invalid", false, false)]
    [InlineData("missing-value", false, false)]
    [InlineData("unknown-option", false, false)]
    [InlineData("extra-arguments", false, false)]
    [InlineData("packaged", true, false)]
    [InlineData("packaged", false, true)]
    public async Task PreparationBuildsExactlyTheSelectedHost(string profile, bool missingHost, bool missingSample)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (OperatingSystem.IsWindows()) { Assert.Skip("Runtime preparation requires Bash."); return; }
        string root = Path.Combine(Path.GetTempPath(), "parties-runtime-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        string script = Path.Combine(root, "scripts", "prepare-aspire-runtime-hosts.sh");
        string calls = Path.Combine(root, "compiler-calls.jsonl");
        const string NativeSample = "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj";
        const string PackageSample = "tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj";
        string[] commonHosts =
        [
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj",
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj",
            "references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.DomainService/Hexalith.Memories.DomainService.csproj",
            profile == "native" ? NativeSample : PackageSample,
            "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj",
        ];
        const string PackageHost = "tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj";
        const string NativeHost = "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj";
        try
        {
            File.Copy(CiTestPaths.RepoFile("scripts/prepare-aspire-runtime-hosts.sh"), script);
            foreach (string relative in commonHosts.Append(NativeHost).Append(NativeSample).Append(PackageSample)
                .Concat(missingHost ? [] : new[] { PackageHost }).Distinct(StringComparer.Ordinal)
                .Where(relative => !missingSample || relative != PackageSample))
            {
                string project = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(project)!);
                File.WriteAllText(project, "<Project />");
            }
            string compiler = Path.Combine(root, "tools", "dotnet");
            File.WriteAllText(compiler, "#!/usr/bin/env python3\nimport json, os, sys\nwith open(os.environ['PARTIES_COMPILER_CALLS'], 'a') as output:\n    output.write(json.dumps(sys.argv[1:]) + '\\n')\n");
            File.SetUnixFileMode(compiler, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(script);
            if (profile == "missing-value") { start.ArgumentList.Add("--gateway-profile"); }
            else if (profile == "unknown-option") { start.ArgumentList.Add("--unknown"); start.ArgumentList.Add("packaged"); }
            else if (profile == "extra-arguments")
            {
                start.ArgumentList.Add("--gateway-profile"); start.ArgumentList.Add("packaged"); start.ArgumentList.Add("extra");
            }
            else if (profile.Length > 0) { start.ArgumentList.Add("--gateway-profile"); start.ArgumentList.Add(profile); }
            start.Environment["PATH"] = Path.GetDirectoryName(compiler) + Path.PathSeparator + start.Environment["PATH"];
            start.Environment["PARTIES_COMPILER_CALLS"] = calls;
            using Process process = Process.Start(start)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().ConfigureAwait(true); throw; }
            string diagnostics = await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true);
            if (profile is "invalid" or "missing-value" or "unknown-option" or "extra-arguments" || missingHost || missingSample)
            {
                process.ExitCode.ShouldBe(missingHost || missingSample ? 1 : 2, diagnostics);
                File.Exists(calls).ShouldBeFalse("Admission must fail before any runtime output is changed.");
                return;
            }
            process.ExitCode.ShouldBe(0, diagnostics);
            string[][] arguments = File.ReadAllLines(calls).Select(line => JsonSerializer.Deserialize<string[]>(line)!).ToArray();
            arguments.Select(call => call[1]).ShouldBe(new[] { profile == "native" ? NativeHost : PackageHost }.Concat(commonHosts));
            arguments.ShouldAllBe(call => call[0] == "build" && call.Contains("Release") && call.Contains("-warnaserror") && call.Contains("-m:1"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Configuration uses the explicit owner root or bounded declared layouts without initializing submodules.</summary>
    /// <param name="layout">The selected checkout layout or missing root declaration.</param>
    /// <param name="sampleHost">Whether the sample rather than gateway package host is evaluated.</param>
    [Theory]
    [InlineData("standalone", false)]
    [InlineData("standalone", true)]
    [InlineData("umbrella", false)]
    [InlineData("umbrella", true)]
    [InlineData("explicit", false)]
    [InlineData("explicit", true)]
    [InlineData("undeclared", false)]
    [InlineData("undeclared", true)]
    public async Task PackageRuntimeSettingsResolveOnlyTheSelectedOrRootDeclaredOwner(string layout, bool sampleHost)
    {
        ArgumentNullException.ThrowIfNull(layout);
        string workspace = Path.Combine(Path.GetTempPath(), "parties-gateway-settings-" + Guid.NewGuid().ToString("N"));
        string repository = layout is "umbrella" or "undeclared" ? Path.Combine(workspace, "references", "Hexalith.Parties") : Path.Combine(workspace, "parties");
        string kind = sampleHost ? "Sample" : "Gateway";
        string hostName = sampleHost ? "Hexalith.Parties.EventStoreSample.TestHost" : "Hexalith.Parties.EventStoreGateway.TestHost";
        string projectDirectory = Path.Combine(repository, "tests", hostName);
        Directory.CreateDirectory(projectDirectory);
        try
        {
            string nested = Path.Combine(repository, "references", "Hexalith.EventStore");
            string selected = layout switch
            {
                "umbrella" or "undeclared" => Path.Combine(workspace, "references", "Hexalith.EventStore"),
                "explicit" => Path.Combine(workspace, "tool-selected-eventstore"),
                _ => nested,
            };
            const string Declaration = "[submodule \"Hexalith.EventStore\"]\n path = references/Hexalith.EventStore\n";
            File.WriteAllText(Path.Combine(repository, ".gitmodules"), Declaration);
            if (layout is "umbrella" or "undeclared")
            {
                File.WriteAllText(Path.Combine(workspace, ".gitmodules"), layout == "undeclared" ? string.Empty : Declaration);
            }
            foreach (string owner in new[] { nested, selected }.Distinct(StringComparer.Ordinal))
            {
                string settings = sampleHost ? Path.Combine(owner, "samples", "Hexalith.EventStore.Sample") : Path.Combine(owner, "src", "Hexalith.EventStore");
                Directory.CreateDirectory(settings);
                File.WriteAllText(Path.Combine(settings, "appsettings.json"), "{}");
                File.WriteAllText(Path.Combine(settings, "appsettings.Development.json"), "{}");
                if (sampleHost) { WriteRepresentativeSampleRoot(owner, owner == selected ? "selected" : "stale"); }
            }
            string project = Path.Combine(projectDirectory, hostName + ".csproj");
            File.Copy(CiTestPaths.RepoFile($"tests/{hostName}/{hostName}.csproj"), project);
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string argument in new[] { "msbuild", project, $"-target:Validate{kind}SettingsRoot,ComputeRunArguments", "-getItem:Content,Compile",
                $"-getProperty:_{kind}EventStoreRoot,HexalithEventStoreFromSource,UseHexalithProjectReferences,RunWorkingDirectory,TargetDir" }) { start.ArgumentList.Add(argument); }
            if (layout == "explicit") { start.ArgumentList.Add("-p:HexalithEventStoreRoot=" + selected); }
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().ConfigureAwait(true); throw; }
            string stdout = await output.ConfigureAwait(true);
            string stderr = await error.ConfigureAwait(true);
            if (layout == "undeclared")
            {
                process.ExitCode.ShouldNotBe(0, stdout + stderr);
                (stdout + stderr).ShouldContain("root-declared standalone/umbrella EventStore reference");
            }
            else
            {
                process.ExitCode.ShouldBe(0, stdout + stderr);
                using JsonDocument result = JsonDocument.Parse(stdout);
                JsonElement properties = result.RootElement.GetProperty("Properties");
                Path.GetFullPath(properties.GetProperty($"_{kind}EventStoreRoot").GetString()!).ShouldBe(selected);
                properties.GetProperty("HexalithEventStoreFromSource").GetString().ShouldNotBe("true");
                properties.GetProperty("UseHexalithProjectReferences").GetString().ShouldNotBe("true");
                if (sampleHost)
                {
                    properties.GetProperty("RunWorkingDirectory").GetString().ShouldBe(properties.GetProperty("TargetDir").GetString());
                    string[] compiled = result.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
                        .Select(item => item.GetProperty("FullPath").GetString()!).Order(StringComparer.Ordinal).ToArray();
                    compiled.ShouldBe(RepresentativeSampleSources(selected).Order(StringComparer.Ordinal));
                    compiled.ShouldAllBe(path => path.StartsWith(selected + Path.DirectorySeparatorChar, StringComparison.Ordinal));
                    File.ReadAllText(Path.Combine(selected, "samples", "Hexalith.EventStore.Sample", "Program.cs")).ShouldContain("selected");
                }
                var contents = result.RootElement.GetProperty("Items").GetProperty("Content").EnumerateArray()
                    .Where(item => item.TryGetProperty("Link", out _)).ToArray();
                contents.Length.ShouldBe(2);
                contents.ShouldAllBe(item => item.GetProperty("FullPath").GetString()!.StartsWith(selected + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            }
            Directory.EnumerateFileSystemEntries(workspace, ".git", SearchOption.AllDirectories).ShouldBeEmpty();
            Directory.EnumerateFileSystemEntries(workspace, "modules", SearchOption.AllDirectories).ShouldBeEmpty();
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    /// <summary>The compatibility host retains native defaults while remaining outside every publication inventory.</summary>
    [Fact]
    public void PackageGatewayUsesNativeDefaultsAndCannotBePublished()
    {
        XDocument project = XDocument.Load(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj"));
        project.Descendants("IsPackable").Single().Value.ShouldBe("false");
        project.Descendants("IsPublishable").Single().Value.ShouldBe("false");
        project.Descendants("PackageReference").Single().Attribute("Include")!.Value.ShouldBe("Hexalith.EventStore.Gateway");
        XElement[] defaults = project.Descendants("Content").ToArray();
        defaults.Select(item => item.Attribute("Link")!.Value).ShouldBe(["appsettings.gateway.json", "appsettings.gateway.Development.json"]);
        defaults.ShouldAllBe(item => item.Attribute("CopyToOutputDirectory")!.Value == "PreserveNewest"
            && item.Attribute("CopyToPublishDirectory")!.Value == "Never");
        project.Descendants("HexalithEventStoreFromSource").ShouldBeEmpty();
        project.Descendants("UseHexalithProjectReferences").ShouldBeEmpty();
        CiTestPaths.ReadRepoFile("tests/Hexalith.Parties.EventStoreGateway.TestHost/Properties/launchSettings.json")
            .ReplaceLineEndings().ShouldBe(CiTestPaths.ReadRepoFile("references/Hexalith.EventStore/src/Hexalith.EventStore/Properties/launchSettings.json").ReplaceLineEndings());
        CiTestPaths.ReadRepoFile("tools/release-packages.json").ShouldNotContain("EventStoreGateway.TestHost");
        CiTestPaths.ReadRepoFile("tests/Hexalith.Parties.EventStoreGateway.TestHost/Program.cs").ShouldContain("builder.Configuration.Sources.Insert(defaultsFile == \"appsettings.gateway.json\" ? 0 : 1,");
    }

    /// <summary>The real linked sample program/domain/contracts use the published SDK without offline source catalogs.</summary>
    [Fact]
    public void PackageSampleRetainsNativeProgramProfilesAndNonpublicationBoundary()
    {
        const string Host = "tests/Hexalith.Parties.EventStoreSample.TestHost/";
        XDocument project = XDocument.Load(CiTestPaths.RepoFile(Host + "Hexalith.Parties.EventStoreSample.TestHost.csproj"));
        project.Descendants("IsPackable").Single().Value.ShouldBe("false");
        project.Descendants("IsPublishable").Single().Value.ShouldBe("false");
        project.Descendants("ProjectReference").ShouldBeEmpty();
        XElement dependency = project.Descendants("PackageReference").ShouldHaveSingleItem();
        dependency.Attribute("Include")!.Value.ShouldBe("Hexalith.EventStore.DomainService");
        dependency.Attribute("Version").ShouldBeNull();
        project.Descendants("Compile").Select(item => item.Attribute("Include")!.Value).ShouldBe(
        [
            "$(_SampleEventStoreRoot)/samples/Hexalith.EventStore.Sample/**/*.cs",
            "$(_SampleEventStoreRoot)/samples/Hexalith.EventStore.Sample.Contracts/**/*.cs",
        ]);
        string exclusions = string.Join(';', project.Descendants("Compile").Select(item => item.Attribute("Exclude")!.Value));
        exclusions.Split(';').Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal))
            .Select(Path.GetFileName).ShouldBe(["SampleCatalog.cs", "GreetingCatalogDeclarations.cs", "CounterCatalogDeclarations.cs"]);
        File.Exists(CiTestPaths.RepoFile(Host + "Program.cs")).ShouldBeFalse("The wrapper must execute the original program and its SDK security policy.");
        project.Descendants("Content").Single().Attribute("CopyToPublishDirectory")!.Value.ShouldBe("Never");
        project.Descendants("RunWorkingDirectory").Single().Value.ShouldBe("$(TargetDir)");
        CiTestPaths.ReadRepoFile(Host + "Properties/launchSettings.json").ReplaceLineEndings()
            .ShouldBe(CiTestPaths.ReadRepoFile("references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Properties/launchSettings.json").ReplaceLineEndings());
        CiTestPaths.ReadRepoFile("tools/release-packages.json").ShouldNotContain("EventStoreSample.TestHost");
        foreach (string solution in new[] { "Hexalith.Parties.slnx", "Hexalith.Parties.Standalone.slnx" })
        {
            CiTestPaths.ReadRepoFile(solution).ShouldContain(Host + "Hexalith.Parties.EventStoreSample.TestHost.csproj");
        }
    }

    /// <summary>Nonempty roots must still contain every required native source/config input before compilation.</summary>
    /// <param name="missing">The required input or complete root deliberately removed.</param>
    [Theory]
    [InlineData("root")]
    [InlineData("samples/Hexalith.EventStore.Sample/Program.cs")]
    [InlineData("samples/Hexalith.EventStore.Sample/Counter")]
    [InlineData("samples/Hexalith.EventStore.Sample/Greeting")]
    [InlineData("samples/Hexalith.EventStore.Sample.Contracts")]
    [InlineData("samples/Hexalith.EventStore.Sample/appsettings.json")]
    [InlineData("samples/Hexalith.EventStore.Sample/appsettings.Development.json")]
    [InlineData("samples/Hexalith.EventStore.Sample/Properties/launchSettings.json")]
    public async Task PackageSampleRefusesIncompleteSelectedSourcesBeforeCompile(string missing)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "parties-sample-missing-" + Guid.NewGuid().ToString("N"));
        string owner = Path.Combine(workspace, "selected-owner");
        Directory.CreateDirectory(workspace);
        try
        {
            WriteRepresentativeSampleRoot(owner, "selected");
            string removed = missing == "root" ? owner : Path.Combine(owner, missing);
            if (Directory.Exists(removed)) { Directory.Delete(removed, recursive: true); }
            else { File.Delete(removed); }
            string project = Path.Combine(workspace, "Sample.csproj");
            File.Copy(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj"), project);
            var result = await RunDotnetAsync(workspace, "msbuild", project, "-nologo", "-target:ValidateSampleSettingsRoot",
                "-getItem:Compile", "-p:HexalithEventStoreRoot=" + owner).ConfigureAwait(true);
            result.ExitCode.ShouldNotBe(0);
            (result.Output + result.Error).ShouldContain("missing required native source/config");
            (result.Output + result.Error).ShouldContain(owner);
            (result.Output + result.Error).ShouldContain("do not initialize nested submodules");
            Directory.Exists(Path.Combine(workspace, "bin")).ShouldBeFalse("Source admission must fail before compilation.");
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    /// <summary>The actual linked native program loads copied base/Development JSON and environment overrides.</summary>
    [Fact]
    public async Task PackageSampleLoadsCopiedConfigurationWithNormalProviderPrecedence()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "parties-sample-config-" + Guid.NewGuid().ToString("N"));
        string owner = Path.Combine(workspace, "selected-owner");
        string projectDirectory = Path.Combine(workspace, "tests", "Hexalith.Parties.EventStoreSample.TestHost");
        Directory.CreateDirectory(projectDirectory);
        try
        {
            foreach (string name in new[] { "Hexalith.EventStore.Sample", "Hexalith.EventStore.Sample.Contracts" })
            {
                string source = CiTestPaths.RepoFile("references/Hexalith.EventStore/samples/" + name);
                foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                    .Where(file => !Path.GetRelativePath(source, file).Split(Path.DirectorySeparatorChar).Any(segment => segment is "obj" or "bin"))
                    .Where(file => Path.GetExtension(file) is ".cs" or ".json" or ".csproj"))
                {
                    string target = Path.Combine(owner, "samples", name, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target);
                }
                foreach (string generated in new[] { "obj", "bin" })
                {
                    string excluded = Path.Combine(owner, "samples", name, generated, "NeverCompile.cs");
                    Directory.CreateDirectory(Path.GetDirectoryName(excluded)!);
                    File.WriteAllText(excluded, "#error build output must not compile\n");
                }
            }
            string project = Path.Combine(projectDirectory, "Hexalith.Parties.EventStoreSample.TestHost.csproj");
            File.Copy(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj"), project);
            File.Copy(CiTestPaths.RepoFile("global.json"), Path.Combine(workspace, "global.json"));
            File.WriteAllText(Path.Combine(workspace, "Directory.Build.props"),
                "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", CiTestPaths.RepoFile("Directory.Packages.props")))))
                .Save(Path.Combine(workspace, "Directory.Packages.props"));
            string native = Path.Combine(owner, "samples", "Hexalith.EventStore.Sample");
            foreach ((string file, string host) in new[] { ("appsettings.json", "base.invalid"), ("appsettings.Development.json", "development.invalid") })
            {
                string path = Path.Combine(native, file);
                JsonObject settings = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                settings["AllowedHosts"] = host;
                File.WriteAllText(path, settings.ToJsonString());
            }
            var built = await RunDotnetAsync(workspace, "build", project, "-c", "Release", "--disable-build-servers", "-m:1",
                "-p:HexalithEventStoreRoot=" + owner, "-warnaserror").ConfigureAwait(true);
            built.ExitCode.ShouldBe(0, built.Output + built.Error);
            var evaluated = await RunDotnetAsync(workspace, "msbuild", project, "-nologo", "-p:Configuration=Release",
                "-p:HexalithEventStoreRoot=" + owner, "-target:ComputeRunArguments", "-getProperty:RunWorkingDirectory").ConfigureAwait(true);
            evaluated.ExitCode.ShouldBe(0, evaluated.Output + evaluated.Error);
            string output = evaluated.Output.Trim();
            foreach (string file in new[] { "appsettings.json", "appsettings.Development.json" })
            {
                File.ReadAllBytes(Path.Combine(output, file)).ShouldBe(File.ReadAllBytes(Path.Combine(native, file)));
            }
            // Same actual program and Development environment in each case; only provider inputs change.
            string development = Path.Combine(output, "appsettings.Development.json");
            File.Move(development, development + ".saved");
            await AssertActualSampleAllowedHostsAsync(output, "base.invalid", null).ConfigureAwait(true);
            File.Move(development + ".saved", development);
            await AssertActualSampleAllowedHostsAsync(output, "development.invalid", null).ConfigureAwait(true);
            await AssertActualSampleAllowedHostsAsync(output, "environment.invalid", "environment.invalid").ConfigureAwait(true);
            Directory.EnumerateDirectories(workspace, ".git", SearchOption.AllDirectories).ShouldBeEmpty();
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    private static string[] RepresentativeSampleSources(string owner) => [.. new[]
    {
        "samples/Hexalith.EventStore.Sample/Program.cs",
        "samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs",
        "samples/Hexalith.EventStore.Sample/Greeting/GreetingAggregate.cs",
        "samples/Hexalith.EventStore.Sample.Contracts/Counter/Commands/IncrementCounter.cs",
        "samples/Hexalith.EventStore.Sample.Contracts/Counter/Queries/GetCounterStatusQuery.cs",
    }.Select(path => Path.GetFullPath(Path.Combine(owner, path)))];

    private static void WriteRepresentativeSampleRoot(string owner, string marker)
    {
        foreach (string file in RepresentativeSampleSources(owner))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "// " + marker + "\n");
        }
        string sample = Path.Combine(owner, "samples", "Hexalith.EventStore.Sample");
        Directory.CreateDirectory(Path.Combine(sample, "Properties"));
        File.WriteAllText(Path.Combine(sample, "appsettings.json"), "{\"AllowedHosts\":\"base-" + marker + ".invalid\"}");
        File.WriteAllText(Path.Combine(sample, "appsettings.Development.json"), "{\"AllowedHosts\":\"development-" + marker + ".invalid\"}");
        File.WriteAllText(Path.Combine(sample, "Properties", "launchSettings.json"), "{\"profiles\":{}}");
        File.WriteAllText(Path.Combine(owner, "samples", "Hexalith.EventStore.Sample.Contracts", "Hexalith.EventStore.Sample.Contracts.csproj"), "<Project />");
        foreach (string excluded in new[]
        {
            "samples/Hexalith.EventStore.Sample/Counter/SampleCatalog.cs",
            "samples/Hexalith.EventStore.Sample/Greeting/GreetingCatalogDeclarations.cs",
            "samples/Hexalith.EventStore.Sample.Contracts/Counter/CounterCatalogDeclarations.cs",
            "samples/Hexalith.EventStore.Sample/obj/NeverCompile.cs", "samples/Hexalith.EventStore.Sample/bin/NeverCompile.cs",
            "samples/Hexalith.EventStore.Sample.Contracts/obj/NeverCompile.cs", "samples/Hexalith.EventStore.Sample.Contracts/bin/NeverCompile.cs",
        })
        {
            string path = Path.Combine(owner, excluded);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "#error excluded offline/build output\n");
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunDotnetAsync(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().ConfigureAwait(true); throw; }
        return (process.ExitCode, await output.ConfigureAwait(true), await error.ConfigureAwait(true));
    }

    private static async Task AssertActualSampleAllowedHostsAsync(string output, string allowedHost, string? environmentOverride)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = output, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(output, "Hexalith.Parties.EventStoreSample.TestHost.dll"));
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment.Remove("AllowedHosts");
        if (environmentOverride is not null) { start.Environment["AllowedHosts"] = environmentOverride; }
        var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnostics = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null) { return; }
            lock (diagnostics) { diagnostics.AppendLine(args.Data); }
            Match match = Regex.Match(args.Data, @"Now listening on: (?<url>http://127\.0\.0\.1:\d+)");
            if (match.Success) { listening.TrySetResult(new Uri(match.Groups["url"].Value)); }
        };
        process.ErrorDataReceived += (_, args) => { lock (diagnostics) { diagnostics.AppendLine(args.Data); } };
        process.Start().ShouldBeTrue();
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        try
        {
            Uri address;
            try { address = await listening.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken).ConfigureAwait(true); }
            catch (TimeoutException) { throw new InvalidOperationException("The actual sample configuration probe did not start: " + diagnostics); }
            using var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(5) };
            foreach (string host in new[] { "base.invalid", "development.invalid", "environment.invalid" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/alive");
                request.Headers.Host = host;
                using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(host == allowedHost ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
                    "The actual linked program must load copied files below the environment provider: " + diagnostics);
            }
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            await process.WaitForExitAsync().ConfigureAwait(true);
        }
    }

}
