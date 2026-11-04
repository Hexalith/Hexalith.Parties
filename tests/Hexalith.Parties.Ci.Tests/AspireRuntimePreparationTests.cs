using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Executes runtime preparation with a recording compiler to verify explicit package runtime selection.</summary>
public sealed class AspireRuntimePreparationTests
{
    /// <summary>Only the matching gateway/sample profile is built; invalid or incomplete profiles fail before compilation.</summary>
    /// <param name="profile">The explicit profile or an empty string for the package CI default.</param>
    /// <param name="missingHost">Whether the selected package gateway is deliberately absent.</param>
    /// <param name="missingSample">Whether the selected package sample is deliberately absent.</param>
    /// <param name="cohortMutation">The actual restored SDK graph mutation, independent of evaluated property echo.</param>
    [Theory]
    [InlineData("", false, false, "valid")]
    [InlineData("packaged", false, false, "valid")]
    [InlineData("packaged-qualification", false, false, "valid")]
    [InlineData("native", false, false, "valid")]
    [InlineData("invalid", false, false, "valid")]
    [InlineData("missing-value", false, false, "valid")]
    [InlineData("unknown-option", false, false, "valid")]
    [InlineData("extra-arguments", false, false, "valid")]
    [InlineData("packaged", true, false, "valid")]
    [InlineData("packaged", false, true, "valid")]
    [InlineData("packaged", false, false, "wrong-version")]
    [InlineData("packaged", false, false, "project-type")]
    [InlineData("packaged", false, false, "missing-sdk")]
    [InlineData("packaged", false, false, "missing-assets")]
    [InlineData("packaged", false, false, "missing-core-library")]
    public async Task PreparationBuildsExactlyTheSelectedHost(string profile, bool missingHost, bool missingSample, string cohortMutation)
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
        const string PackageAdminUi = "tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj";
        const string PackageHost = "tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj";
        const string NativeHost = "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj";
        string[] qualificationProjects = profile == "packaged-qualification" ? ["tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj",
            "tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj"] : [];
        try
        {
            File.Copy(CiTestPaths.RepoFile("scripts/prepare-aspire-runtime-hosts.sh"), script);
            File.Copy(CiTestPaths.RepoFile("scripts/validate-aspire-runtime-cohort.py"), Path.Combine(root, "scripts", "validate-aspire-runtime-cohort.py"));
            string[] catalogProjects = profile == "native" ? [] : ["src/Hexalith.Parties/Hexalith.Parties.csproj",
                "src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj"];
            foreach (string relative in commonHosts.Concat(qualificationProjects).Concat(catalogProjects).Append(NativeHost).Append(NativeSample).Append(PackageSample).Append(PackageAdminUi)
                .Concat(missingHost ? [] : new[] { PackageHost }).Distinct(StringComparer.Ordinal)
                .Where(relative => !missingSample || relative != PackageSample))
            {
                string project = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(project)!);
                File.WriteAllText(project, "<Project />");
            }
            string compiler = Path.Combine(root, "tools", "dotnet");
            File.WriteAllText(compiler, """
                #!/usr/bin/env python3
                import json, os, sys, zipfile
                from pathlib import Path
                args = sys.argv[1:]
                with open(os.environ['PARTIES_COMPILER_CALLS'], 'a') as output:
                    output.write(json.dumps(args) + '\n')
                project = Path(args[1])
                configuration = next((arg.split('=', 1)[1] for arg in args if arg.startswith('-p:Configuration=')), 'Release')
                if '--configuration' in args:
                    configuration = args[args.index('--configuration') + 1]
                target = (project.parent / 'bin' / configuration / 'net10.0' / (project.stem + '.dll')).resolve()
                assets = (project.parent / 'obj/project.assets.json').resolve()
                families = {'HexalithTenantsVersion':'6.0.0', 'HexalithMemoriesVersion':'3.0.13', 'HexalithFrontComposerVersion':'4.7.4'}
                if '-getProperty:HexalithEventStoreVersion' in args:
                    print('3.130.2')
                elif '-getProperty:HexalithEventStoreCatalogHostsSupported' in args:
                    print('true')
                elif any(arg.startswith('-getProperty:HexalithTenantsVersion,') for arg in args):
                    print(json.dumps({'Properties':families}))
                elif any(arg.startswith('-getProperty:TargetPath,') for arg in args):
                    print(json.dumps({'Properties':{'TargetPath':str(target), 'ProjectAssetsFile':str(assets),
                        'Configuration':configuration, 'HexalithEventStoreVersion':'3.130.2'}}))
                elif args[0] == 'build':
                    mode = os.environ['PARTIES_COHORT_MUTATION']
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(b'completed entry PE')
                    target.with_suffix('.runtimeconfig.json').write_text('{"runtimeOptions":{"tfm":"net10.0"}}')
                    assets.parent.mkdir(parents=True, exist_ok=True)
                    cache = Path('packages').resolve()
                    libraries, targets = {}, {}
                    packages = ['Hexalith.EventStore.Client','Hexalith.EventStore.Contracts','Hexalith.EventStore.ServiceDefaults']
                    if project.name == 'Hexalith.EventStore.Admin.Server.Host.csproj':
                        packages = ['Hexalith.EventStore.Admin.Server','Hexalith.EventStore.Admin.Abstractions','Hexalith.EventStore.Contracts','Hexalith.EventStore.ServiceDefaults']
                    for package in packages:
                        relative = package.lower() + '/3.130.2'
                        folder = cache / relative
                        folder.mkdir(parents=True, exist_ok=True)
                        entry = 'lib/net10.0/' + package + '.dll'
                        contents = ('runtime archive ' + package).encode()
                        (target.parent / (package + '.dll')).write_bytes(contents)
                        archive_path = folder / (package.lower() + '.3.130.2.nupkg')
                        if not archive_path.exists():
                            with zipfile.ZipFile(archive_path, 'w') as archive:
                                archive.writestr(package + '.nuspec','<package><metadata><id>' + package + '</id><version>3.130.2</version><repository type="git" url="https://github.com/Hexalith/Hexalith.EventStore" commit="' + 'a'*40 + '" /></metadata></package>')
                                archive.writestr(entry,contents)
                        identity = package + '/3.130.2'
                        libraries[identity] = {'type':'package','path':relative}
                        targets[identity] = {'runtime':{entry:{}}}
                    if mode == 'wrong-version': libraries['Hexalith.EventStore.Client/3.128.4'] = libraries.pop('Hexalith.EventStore.Client/3.130.2')
                    elif mode == 'project-type': libraries['Hexalith.EventStore.Client/3.130.2']['type'] = 'project'
                    elif mode == 'missing-core-library': libraries.pop('Hexalith.EventStore.Contracts/3.130.2')
                    elif mode == 'missing-sdk': libraries = {}
                    if mode != 'missing-assets': assets.write_text(json.dumps({'libraries':libraries,'targets':{'net10.0':targets},'packageFolders':{str(cache):{}}}))
                    target.with_suffix('.deps.json').write_text(json.dumps({'libraries':libraries,'targets':{'net10.0':targets}}))
                    archive_cache = Path('artifacts/runtime-profile') / configuration / 'public-archives'
                    archive_cache.mkdir(parents=True,exist_ok=True)
                    for package,version,repo in [('Hexalith.Tenants.Contracts','6.0.0','Hexalith.Tenants'),('Hexalith.Tenants.Server','6.0.0','Hexalith.Tenants'),('Hexalith.Memories.EventStore','3.0.13','Hexalith.Memories'),('Hexalith.Memories.EventStoreExtensions','3.0.13','Hexalith.Memories')]:
                        archive_path = archive_cache / (package.lower()+'.'+version+'.nupkg')
                        if not archive_path.exists():
                            with zipfile.ZipFile(archive_path,'w') as archive:
                                archive.writestr(package+'.nuspec','<package><metadata><id>'+package+'</id><version>'+version+'</version><repository type="git" url="https://github.com/Hexalith/'+repo+'" commit="'+'b'*40+'" /></metadata></package>')
                """.ReplaceLineEndings("\n") + "\n");
            File.SetUnixFileMode(compiler, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(script);
            if (profile == "missing-value") { start.ArgumentList.Add("--gateway-profile"); }
            else if (profile == "unknown-option") { start.ArgumentList.Add("--unknown"); start.ArgumentList.Add("packaged"); }
            else if (profile == "extra-arguments")
            {
                start.ArgumentList.Add("--gateway-profile"); start.ArgumentList.Add("packaged"); start.ArgumentList.Add("extra");
            }
            else if (profile == "packaged-qualification")
            {
                start.ArgumentList.Add("--gateway-profile"); start.ArgumentList.Add("packaged");
                start.ArgumentList.Add("--include-memory-qualification"); start.ArgumentList.Add("true");
            }
            else if (profile.Length > 0) { start.ArgumentList.Add("--gateway-profile"); start.ArgumentList.Add(profile); }
            start.Environment["PATH"] = Path.GetDirectoryName(compiler) + Path.PathSeparator + start.Environment["PATH"];
            start.Environment["PARTIES_COMPILER_CALLS"] = calls;
            start.Environment["PARTIES_COHORT_MUTATION"] = cohortMutation;
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
            string[][] arguments = File.ReadAllLines(calls).Select(line => JsonSerializer.Deserialize<string[]>(line)!).ToArray();
            string[][] builds = arguments.Where(call => call[0] == "build").ToArray();
            if (cohortMutation != "valid")
            {
                process.ExitCode.ShouldBe(1, diagnostics);
                diagnostics.ShouldContain("Runtime SDK qualification failed");
                builds.ShouldHaveSingleItem("Restore drift must stop preparation before another host or any runner starts.");
                return;
            }
            process.ExitCode.ShouldBe(0, diagnostics);
            string[] selectedCommonHosts = commonHosts.Select(path => profile != "native" && path.EndsWith("Hexalith.EventStore.Admin.UI.csproj", StringComparison.Ordinal)
                ? PackageAdminUi : path).ToArray();
            builds.Select(call => call[1]).ShouldBe(new[] { profile == "native" ? NativeHost : PackageHost }.Concat(selectedCommonHosts).Concat(qualificationProjects).Concat(catalogProjects));
            builds.ShouldAllBe(call => call.Contains("Release") && call.Contains("-warnaserror") && call.Contains("-m:1"));
            if (profile != "native")
            {
                builds.ShouldAllBe(call => call.Contains("-p:HexalithEventStoreVersion=3.130.2") && call.Contains("-p:HexalithUsePublishedSdkForHostedTests=true"));
                arguments.Count(call => call.Any(argument => argument.StartsWith("-getProperty:TargetPath,", StringComparison.Ordinal))).ShouldBe(10 + qualificationProjects.Length);
                builds.ShouldAllBe(call => call.Contains("-t:Rebuild") && call.Contains("-p:HexalithFrontComposerVersion=4.7.4")
                    && call.Contains("-p:HexalithTenantsVersion=6.0.0") && call.Contains("-p:HexalithMemoriesVersion=3.0.13"));
                File.Exists(Path.Combine(root, "artifacts", "runtime-profile", "Release", "prepared.json")).ShouldBeTrue();
            }
            else
            {
                arguments.Length.ShouldBe(8);
                builds.ShouldAllBe(call => !call.Any(argument => argument.StartsWith("-p:HexalithEventStoreVersion=", StringComparison.Ordinal)));
            }
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
            PackageTestProcess.RemoveSourceMappingEnvironment(start);
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
                    compiled.ShouldBe(RepresentativeSampleSources(selected).Where(path => !path.Contains("Hexalith.EventStore.Sample.Contracts", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
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

    /// <summary>The linked native program and distinct Contracts identity retain complete catalog inputs and stay outside publication.</summary>
    [Fact]
    public void PackageSampleRetainsNativeProgramProfilesAndNonpublicationBoundary()
    {
        const string Host = "tests/Hexalith.Parties.EventStoreSample.TestHost/";
        XDocument project = XDocument.Load(CiTestPaths.RepoFile(Host + "Hexalith.Parties.EventStoreSample.TestHost.csproj"));
        project.Descendants("IsPackable").Single().Value.ShouldBe("false");
        project.Descendants("IsPublishable").Single().Value.ShouldBe("false");
        project.Descendants("ProjectReference").Single().Attribute("Include")!.Value.ShouldContain("Hexalith.Parties.EventStoreSample.Contracts.TestArtifact");
        XElement dependency = project.Descendants("PackageReference").ShouldHaveSingleItem();
        dependency.Attribute("Include")!.Value.ShouldBe("Hexalith.EventStore.DomainService");
        dependency.Attribute("Version").ShouldBeNull();
        project.Descendants("Compile").Where(item => item.Attribute("Include") is not null).Select(item => item.Attribute("Include")!.Value)
            .ShouldBe(["$(_SampleEventStoreRoot)/samples/Hexalith.EventStore.Sample/**/*.cs"]);
        XElement catalogTarget = project.Descendants("Target").Single(target => target.Attribute("Name")?.Value == "SelectSampleCatalogCompilation");
        catalogTarget.Descendants("DefineConstants").Single().Value.ShouldContain("HEXALITH_SAMPLE_CATALOG");
        catalogTarget.Descendants("Compile").Single().Attribute("Remove")!.Value.Split(';').Select(Path.GetFileName)
            .ShouldBe(["SampleCatalog.cs", "SampleCatalogAdapters.cs", "GreetingCatalogDeclarations.cs"]);
        const string ContractsArtifact = "tests/Hexalith.Parties.EventStoreSample.Contracts.TestArtifact/Hexalith.Parties.EventStoreSample.Contracts.TestArtifact.csproj";
        XDocument contracts = XDocument.Load(CiTestPaths.RepoFile(ContractsArtifact));
        contracts.Descendants("AssemblyName").Single().Value.ShouldBe("Hexalith.EventStore.Sample.Contracts");
        contracts.Descendants("IsPackable").Single().Value.ShouldBe("false");
        contracts.Descendants("IsPublishable").Single().Value.ShouldBe("false");
        contracts.Descendants("Compile").Single(item => item.Attribute("Include") is not null).Attribute("Include")!.Value
            .ShouldBe("$(_SampleEventStoreRoot)/samples/Hexalith.EventStore.Sample.Contracts/**/*.cs");
        CiTestPaths.ReadRepoFile("tools/release-packages.json").ShouldNotContain("EventStoreSample.Contracts.TestArtifact");
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
            string contractsDirectory = Path.Combine(workspace, "tests", "Hexalith.Parties.EventStoreSample.Contracts.TestArtifact");
            Directory.CreateDirectory(contractsDirectory);
            const string ContractsProject = "Hexalith.Parties.EventStoreSample.Contracts.TestArtifact.csproj";
            File.Copy(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreSample.Contracts.TestArtifact/" + ContractsProject), Path.Combine(contractsDirectory, ContractsProject));
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

    /// <summary>Actual file/archive/configuration mutations fail through the same completed-receipt entrypoint used before startup.</summary>
    [Fact]
    public async Task CompletedRuntimeReceiptsRefuseDriftBeforeAnyRunner()
    {
        var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(CiTestPaths.RepoFile("tests/Hexalith.Parties.Ci.Tests/runtime_cohort_receipt_cases.py"));
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(bound.Token).ConfigureAwait(true); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().ConfigureAwait(true); throw; }
        process.ExitCode.ShouldBe(0, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
    }

    /// <summary>Package API availability never selects native hosts and refuses unqualified future cohorts.</summary>
    /// <param name="version">The selected EventStore package version.</param>
    /// <param name="source">Whether this is actual source mode.</param>
    /// <param name="available">Whether the catalog API has been explicitly qualified.</param>
    /// <param name="hostsAvailable">Whether the complete private hosted topology is qualified.</param>
    [Theory]
    [InlineData("3.128.4", false, false, false)]
    [InlineData("3.129.0", false, true, false)]
    [InlineData("3.129.1", false, false, false)]
    [InlineData("3.130.0", false, true, true)]
    [InlineData("3.130.1", false, true, true)]
    [InlineData("3.130.2", false, true, true)]
    [InlineData("3.132.2", false, true, true)]
    [InlineData("3.130.3", false, false, false)]
    [InlineData("3.131.0", false, false, false)]
    [InlineData("4.0.3", false, false, false)]
    [InlineData("4.0.4", false, true, true)]
    [InlineData("4.0.5", false, false, false)]
    [InlineData("4.1.0", false, false, false)]
    [InlineData("4.0.4-preview.1", false, false, false)]
    [InlineData("4.0.4+build.1", false, false, false)]
    [InlineData("4.0.4.0", false, false, false)]
    [InlineData("v4.0.4", false, false, false)]
    [InlineData("4.0", false, false, false)]
    [InlineData("invalid", false, false, false)]
    [InlineData("", false, false, false)]
    [InlineData("4.0.5", true, true, true)]
    [InlineData("3.128.4", true, true, true)]
    public async Task CatalogApiAvailabilityDoesNotSelectNativeHosts(string version, bool source, bool available, bool hostsAvailable)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "parties-catalog-capability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            string project = Path.Combine(workspace, "Capability.csproj");
            new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0"),
                    new XElement("HexalithEventStoreVersion", version), new XElement("HexalithEventStoreFromSource", source.ToString().ToLowerInvariant())),
                new XElement("Import", new XAttribute("Project", CiTestPaths.RepoFile("Directory.Build.targets")))))
                .Save(project);
            var result = await RunDotnetAsync(workspace, "msbuild", project, "-nologo",
                "-getProperty:DefineConstants,HexalithEventStoreCatalogApiSupported,HexalithEventStoreCatalogHostsSupported").ConfigureAwait(true);
            result.ExitCode.ShouldBe(0, result.Output + result.Error);
            using JsonDocument evaluation = JsonDocument.Parse(result.Output);
            JsonElement properties = evaluation.RootElement.GetProperty("Properties");
            properties.GetProperty("HexalithEventStoreCatalogApiSupported").GetString().ShouldBe(available ? "true" : string.Empty);
            properties.GetProperty("HexalithEventStoreCatalogHostsSupported").GetString().ShouldBe(hostsAvailable ? "true" : string.Empty);
            string[] constants = properties.GetProperty("DefineConstants").GetString()!.Split(';');
            constants.Contains("HEXALITH_EVENTSTORE_CATALOG_API", StringComparer.Ordinal).ShouldBe(available);
            constants.Contains("HEXALITH_EVENTSTORE_CATALOG_HOSTS", StringComparer.Ordinal).ShouldBe(hostsAvailable);
            constants.Contains("HEXALITH_DEVELOPMENT_CATALOG_SOURCE", StringComparer.Ordinal).ShouldBe(source);
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    /// <summary>Evaluates exact published UI and readiness capabilities without accepting unknown packages.</summary>
    /// <param name="version">The isolated candidate package selection.</param>
    /// <param name="source">The existing source selector branch.</param>
    /// <param name="sharedUi">Whether shared UI APIs are enabled.</param>
    /// <param name="readiness">Whether shared reconciliation readiness is enabled.</param>
    [Theory]
    [InlineData("4.7.1", false, true, false)]
    [InlineData("4.7.3", false, true, false)]
    [InlineData("4.7.4", false, true, false)]
    [InlineData("4.7.5", false, true, true)]
    [InlineData("4.7.6", false, true, true)]
    [InlineData("4.7.0", false, false, false)]
    [InlineData("4.7.2", false, false, false)]
    [InlineData("4.7.7", false, false, false)]
    [InlineData("4.8.0", false, false, false)]
    [InlineData("4.7.6-preview.1", false, false, false)]
    [InlineData("invalid", false, false, false)]
    [InlineData("", false, false, false)]
    [InlineData("4.7.0", true, true, true)]
    [InlineData("", true, true, true)]
    public async Task FrontComposerExactSelectionPreservesUiAndReadinessBoundaries(
        string version, bool source, bool sharedUi, bool readiness)
    {
        string workspace = Directory.CreateTempSubdirectory("parties-feedback-capability-").FullName;
        try
        {
            string project = Path.Combine(workspace, "Capability.csproj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("DefineConstants", "UNCHANGED"),
                    new XElement("HexalithEventStoreVersion", "4.0.4"),
                    new XElement("HexalithFrontComposerVersion", version),
                    new XElement("HexalithFrontComposerFromSource", source.ToString().ToLowerInvariant())),
                new XElement("Import", new XAttribute("Project", CiTestPaths.RepoFile("Directory.Build.targets")))))
                .Save(project);
            var result = await RunDotnetAsync(workspace, "msbuild", project, "-nologo",
                "-target:ConfigureFrontComposerFeedbackCandidate", "-getProperty:DefineConstants").ConfigureAwait(true);
            result.ExitCode.ShouldBe(0, result.Output + result.Error);
            string[] symbols = result.Output.Trim().Split(';');
            symbols.ShouldContain("UNCHANGED");
            symbols.ShouldContain("HES_CANCELLABLE_PROJECTIONS");
            symbols.Contains("HFC_SHARED_UI", StringComparer.Ordinal).ShouldBe(sharedUi);
            symbols.Contains("HFC_SHARED_RECONCILE_READINESS", StringComparer.Ordinal).ShouldBe(readiness);
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
        PackageTestProcess.RemoveSourceMappingEnvironment(start);
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
