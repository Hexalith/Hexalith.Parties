using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class ValidateDeploymentLintFitnessTests : IDisposable
{
    private static readonly string[] s_expectedCategories =
    [
        "DaprACL-WildcardAppId",
        "DaprACL-WildcardOperation",
        "K8sWorkload-DirtyTagOnConsumerImage",
        "K8sWorkload-MissingDaprAnnotations",
        "K8sWorkload-MissingImagePullSecret",
        "K8sWorkload-MissingProbes",
        "K8sWorkload-NonSemVerTag",
        "Secret-Plaintext",
    ];

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "hexalith-parties-lint-fitness-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CuratedFixtureMatrixReportsExactStoryNineSixCategorySet()
    {
        string workspace = CopyFixtureToWorkspace("valid-deploy-tree");
        foreach (string category in s_expectedCategories)
        {
            CopyDirectory(
                Path.Combine(DeploymentTestPaths.FixturesDirectory, "lint-negative", category),
                Path.Combine(workspace, "negative", category));
        }

        string configPath = Path.Combine(workspace, "deploy", "dapr");
        string k8sPath = Path.Combine(workspace, "deploy", "k8s");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(workspace, "negative"), "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(Path.Combine(workspace, "negative"), file);
            string targetRoot = relative.Contains("DaprACL-", StringComparison.Ordinal) ? configPath : k8sPath;
            string target = Path.Combine(targetRoot, relative.Replace(Path.DirectorySeparatorChar, '-'));
            File.Copy(file, target);
        }

        ProcessResult result = RunValidator("-ConfigPath", configPath, "-K8sPath", k8sPath, "-Format", "json");

        result.ExitCode.ShouldBe(1, result.CombinedOutput);
        using JsonDocument document = JsonDocument.Parse(result.Stdout);
        document.RootElement.GetProperty("version").GetString().ShouldBe("1");
        string[] categories = document.RootElement.GetProperty("findings").EnumerateArray()
            .Select(static finding => finding.GetProperty("category").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        categories.ShouldBe(s_expectedCategories);
        foreach (JsonElement finding in document.RootElement.GetProperty("findings").EnumerateArray())
        {
            finding.TryGetProperty("severity", out _).ShouldBeTrue();
            finding.TryGetProperty("category", out _).ShouldBeTrue();
            finding.TryGetProperty("file", out _).ShouldBeTrue();
            finding.TryGetProperty("jsonpath", out _).ShouldBeTrue();
            finding.TryGetProperty("reason", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public void ValidAndNearMissFixturesPassWithoutFindings()
    {
        foreach (string fixture in new[] { "valid-deploy-tree", "lint-near-miss" })
        {
            string workspace = CopyFixtureToWorkspace(fixture);
            ProcessResult result = RunValidator(
                "-ConfigPath",
                Path.Combine(workspace, "deploy", "dapr"),
                "-K8sPath",
                Path.Combine(workspace, "deploy", "k8s"));

            result.ExitCode.ShouldBe(0, result.CombinedOutput);
            result.Stdout.ShouldContain("[validate] 0 findings (0 blocking, 0 warnings) - PASS");
        }
    }

    [Fact]
    public void ValidatorSourceKeepsGoldenApiAndContextFreeContract()
    {
        string source = DeploymentTestPaths.ReadRepoFile("deploy/validate-deployment.ps1");

        source.ShouldContain("$JsonVersion = '1'");
        source.ShouldContain("^-Output$");
        source.ShouldContain("^--config-path$");
        foreach (string category in s_expectedCategories)
        {
            source.ShouldContain(category);
        }

        source.ShouldNotContain("-ConfirmContext");
        source.ShouldNotContain("_lib/Confirm-KubeContext.ps1");
        source.ShouldNotContain("kubectl apply");
        source.ShouldNotContain("kubectl delete");
        source.ShouldNotContain("dapr init");
    }

    private string CopyFixtureToWorkspace(string fixtureName)
    {
        string source = Path.Combine(DeploymentTestPaths.FixturesDirectory, fixtureName);
        string target = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        CopyDirectory(source, target);
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
        }
    }

    private static ProcessResult RunValidator(params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh",
            WorkingDirectory = DeploymentTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(DeploymentTestPaths.RepoFile("deploy/validate-deployment.ps1"));
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start pwsh.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr)
    {
        public string CombinedOutput => Stdout + Stderr;
    }
}
