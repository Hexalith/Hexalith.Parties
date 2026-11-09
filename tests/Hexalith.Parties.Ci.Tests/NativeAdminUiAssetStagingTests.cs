using System.Diagnostics;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Exercises native UI asset staging against producer deletions without starting a host.</summary>
public sealed class NativeAdminUiAssetStagingTests
{
    /// <summary>Repeated preparation removes deleted producer assets from both generated staging locations.</summary>
    [Fact]
    public async Task DeletedProducerAssetsDisappearFromStagingAndOutput()
    {
        string root = Path.Combine(Path.GetTempPath(), "parties-ui-staging-" + Guid.NewGuid().ToString("N"));
        string producer = Path.Combine(root, "sdk", "src", "Hexalith.EventStore.Admin.UI");
        string project = Path.Combine(root, "Host.csproj");
        string output = Path.Combine(root, "output") + Path.DirectorySeparatorChar;
        try
        {
            Directory.CreateDirectory(root);
            File.Copy(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj"), project);
            foreach (string name in new[] { "Hexalith.EventStore.Admin.UI.csproj", "appsettings.json", "appsettings.Development.json",
                "Properties/launchSettings.json", "wwwroot/css/app.css", "wwwroot/js/interop.js", "wwwroot/js/download.js", "wwwroot/js/obsolete.js" })
            {
                string path = Path.Combine(producer, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, name);
            }

            async Task StageAsync()
            {
                ProcessStartInfo start = new("dotnet")
                {
                    WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                };
                foreach (string argument in new[] { "msbuild", project, "-nologo", "-t:StageNativeAdminUiAssets", "-p:TargetFramework=net10.0",
                    "-p:HexalithEventStoreRoot=" + Path.Combine(root, "sdk"), "-p:TargetDir=" + output })
                {
                    start.ArgumentList.Add(argument);
                }

                using Process process = Process.Start(start)!;
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                process.ExitCode.ShouldBe(0, await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true));
            }

            await StageAsync().ConfigureAwait(true);
            string staged = Path.Combine(root, "wwwroot/js/obsolete.js");
            File.Exists(staged).ShouldBeTrue();
            string deployed = Path.Combine(output, "wwwroot/js/obsolete.js");
            Directory.CreateDirectory(Path.GetDirectoryName(deployed)!);
            File.Copy(staged, deployed);
            File.Delete(Path.Combine(producer, "wwwroot/js/obsolete.js"));
            await StageAsync().ConfigureAwait(true);
            File.Exists(staged).ShouldBeFalse();
            File.Exists(deployed).ShouldBeFalse();
            File.ReadAllText(Path.Combine(root, "wwwroot/css/app.css")).ShouldBe("wwwroot/css/app.css");
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }
}
