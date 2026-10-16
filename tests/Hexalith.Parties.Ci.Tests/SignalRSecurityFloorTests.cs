using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Protects restored package and UI shared-runtime security minimums in the configured CI lane.</summary>
public sealed class SignalRSecurityFloorTests
{
    private static readonly Version MinimumFixedVersion = new(10, 0, 13);

    [Theory]
    [InlineData("Hexalith.Parties.Contracts.Tests")]
    [InlineData("Hexalith.Parties.Client.Tests")]
    [InlineData("Hexalith.Parties.AdminPortal.Tests")]
    [InlineData("Hexalith.Parties.ConsumerPortal.Tests")]
    [InlineData("Hexalith.Parties.UI.Tests")]
    public void RestoredConsumerGraph_RequiresFixedSignalRAndActiveNuGetAudit(string project)
    {
        string assetsPath = CiTestPaths.RepoFile($"tests/{project}/obj/project.assets.json");
        File.Exists(assetsPath).ShouldBeTrue("CI must restore the configured solution before executing its verification lane.");
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        foreach (string packageId in new[]
        {
            "Microsoft.AspNetCore.SignalR.Common", "Microsoft.Extensions.Options", "Microsoft.Extensions.DependencyInjection.Abstractions",
        })
        {
            JsonProperty package = assets.RootElement.GetProperty("libraries").EnumerateObject()
                .Single(library => library.Name.StartsWith(packageId + "/", StringComparison.OrdinalIgnoreCase));
            Version.Parse(package.Name.Split('/')[1]).ShouldBeGreaterThanOrEqualTo(MinimumFixedVersion, assetsPath);
        }
        assets.RootElement.GetProperty("project").GetProperty("restore").GetProperty("restoreAuditProperties")
            .GetProperty("enableAudit").GetString().ShouldBe("true", assetsPath);
    }

    [Fact]
    public async Task UiGeneratedAndPublishedRuntimeConfigurations_RequireFixedSharedFrameworks()
    {
        string ui = CiTestPaths.RepoFile("src/Hexalith.Parties.UI");
        using (JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(ui, "obj", "project.assets.json"))))
        {
            assets.RootElement.GetProperty("project").GetProperty("frameworks").GetProperty("net10.0")
                .GetProperty("frameworkReferences").TryGetProperty("Microsoft.AspNetCore.App", out _).ShouldBeTrue();
            assets.RootElement.GetProperty("project").GetProperty("restore").GetProperty("restoreAuditProperties")
                .GetProperty("enableAudit").GetString().ShouldBe("true");
            // Common may be pruned by the Web SDK. If it remains in a future graph, it
            // must still meet the security floor alongside the shared frameworks below.
            foreach (JsonProperty package in assets.RootElement.GetProperty("libraries").EnumerateObject()
                .Where(package => package.Name.StartsWith("Microsoft.AspNetCore.SignalR.Common/", StringComparison.OrdinalIgnoreCase)))
            {
                Version.Parse(package.Name.Split('/')[1]).ShouldBeGreaterThanOrEqualTo(MinimumFixedVersion);
            }
        }

        AssertRuntimeMinimums(Path.Combine(ui, "bin", "Release", "net10.0", "Hexalith.Parties.UI.runtimeconfig.json"));
        string publish = Path.Combine(Path.GetTempPath(), $"parties-ui-runtime-floor-{Guid.NewGuid():N}");
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = CiTestPaths.RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string argument in new[]
            {
                "publish", Path.Combine(ui, "Hexalith.Parties.UI.csproj"), "--configuration", "Release",
                "--no-restore", "--no-build", "-m:1", "-nr:false", "--output", publish,
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("UI publish could not start.");
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            string diagnostic = (await output.ConfigureAwait(true)) + (await error.ConfigureAwait(true));
            process.ExitCode.ShouldBe(0, diagnostic);
            AssertRuntimeMinimums(Path.Combine(publish, "Hexalith.Parties.UI.runtimeconfig.json"));
        }
        finally
        {
            if (Directory.Exists(publish))
            {
                Directory.Delete(publish, recursive: true);
            }
        }
    }

    private static void AssertRuntimeMinimums(string runtimeConfig)
    {
        File.Exists(runtimeConfig).ShouldBeTrue("The UI must be built and published by the normal CI path.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(runtimeConfig));
        JsonElement frameworks = document.RootElement.GetProperty("runtimeOptions").GetProperty("frameworks");
        foreach (string name in new[] { "Microsoft.NETCore.App", "Microsoft.AspNetCore.App" })
        {
            JsonElement framework = frameworks.EnumerateArray()
                .Single(framework => framework.GetProperty("name").GetString() == name);
            Version.Parse(framework.GetProperty("version").GetString()!)
                .ShouldBeGreaterThanOrEqualTo(MinimumFixedVersion, $"{runtimeConfig}: {name}");
        }
    }
}
