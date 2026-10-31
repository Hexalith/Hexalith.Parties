using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Executes runtime preparation with a recording compiler to verify explicit gateway selection.</summary>
public sealed class AspireRuntimePreparationTests
{
    /// <summary>Only the selected gateway is built; invalid or incomplete profiles fail before invoking the compiler.</summary>
    /// <param name="profile">The explicit profile or an empty string for the package CI default.</param>
    /// <param name="missingHost">Whether the selected package host is deliberately absent.</param>
    [Theory]
    [InlineData("", false)]
    [InlineData("packaged", false)]
    [InlineData("native", false)]
    [InlineData("invalid", false)]
    [InlineData("missing-value", false)]
    [InlineData("unknown-option", false)]
    [InlineData("extra-arguments", false)]
    [InlineData("packaged", true)]
    public async Task PreparationBuildsExactlyTheSelectedHost(string profile, bool missingHost)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (OperatingSystem.IsWindows()) { Assert.Skip("Runtime preparation requires Bash."); return; }
        string root = Path.Combine(Path.GetTempPath(), "parties-runtime-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        string script = Path.Combine(root, "scripts", "prepare-aspire-runtime-hosts.sh");
        string calls = Path.Combine(root, "compiler-calls.jsonl");
        string[] commonHosts =
        [
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj",
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj",
            "references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.DomainService/Hexalith.Memories.DomainService.csproj",
            "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj",
            "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj",
        ];
        const string PackageHost = "tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj";
        const string NativeHost = "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj";
        try
        {
            File.Copy(CiTestPaths.RepoFile("scripts/prepare-aspire-runtime-hosts.sh"), script);
            foreach (string relative in commonHosts.Append(NativeHost).Concat(missingHost ? [] : new[] { PackageHost }))
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
            if (profile is "invalid" or "missing-value" or "unknown-option" or "extra-arguments" || missingHost)
            {
                process.ExitCode.ShouldBe(missingHost ? 1 : 2, diagnostics);
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
    [Theory]
    [InlineData("standalone")]
    [InlineData("umbrella")]
    [InlineData("explicit")]
    [InlineData("undeclared")]
    public async Task GatewaySettingsResolveOnlyTheSelectedOrRootDeclaredOwner(string layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        string workspace = Path.Combine(Path.GetTempPath(), "parties-gateway-settings-" + Guid.NewGuid().ToString("N"));
        string repository = layout is "umbrella" or "undeclared" ? Path.Combine(workspace, "references", "Hexalith.Parties") : Path.Combine(workspace, "parties");
        string projectDirectory = Path.Combine(repository, "tests", "Hexalith.Parties.EventStoreGateway.TestHost");
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
                string settings = Path.Combine(owner, "src", "Hexalith.EventStore");
                Directory.CreateDirectory(settings);
                File.WriteAllText(Path.Combine(settings, "appsettings.json"), "{}");
                File.WriteAllText(Path.Combine(settings, "appsettings.Development.json"), "{}");
            }
            string project = Path.Combine(projectDirectory, "Hexalith.Parties.EventStoreGateway.TestHost.csproj");
            File.Copy(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj"), project);
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string argument in new[] { "msbuild", project, "-target:ValidateGatewaySettingsRoot", "-getItem:Content",
                "-getProperty:_GatewayEventStoreRoot,HexalithEventStoreFromSource,UseHexalithProjectReferences" }) { start.ArgumentList.Add(argument); }
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
                Path.GetFullPath(properties.GetProperty("_GatewayEventStoreRoot").GetString()!).ShouldBe(selected);
                properties.GetProperty("HexalithEventStoreFromSource").GetString().ShouldNotBe("true");
                properties.GetProperty("UseHexalithProjectReferences").GetString().ShouldNotBe("true");
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
}
