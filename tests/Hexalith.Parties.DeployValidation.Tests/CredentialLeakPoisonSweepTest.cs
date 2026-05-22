using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class CredentialLeakPoisonSweepTest : IDisposable
{
    private static readonly (string Descriptor, string Value)[] s_poisonValues =
    [
        ("jwt-shaped", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJwb2lzb24ifQ.signature"),
        ("base64-shaped", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
        ("password-prefixed", "Password=CorrectHorseBatteryStaple"),
        ("password-prefixed", "client_secret=super-secret-value"),
        ("password-prefixed", "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.redacted"),
        ("kubeconfig-shaped", "certificate-authority-data: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
        ("kubeconfig-shaped", "client-key-data: BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"),
        ("kubeconfig-shaped", "certificate-authority-data eyJhbGciOiJIUzI1NiJ9 client-key-data"),
        ("password-prefixed", "redis://default:plain-password@redis:6379"),
        ("docker-auth-shaped", """{"auths":{"registry.hexalith.com":{"auth":"ZG9ja2VyOnBvaXNvbg=="}}}"""),
        ("docker-auth-shaped", "ZG9ja2VyOnBvaXNvbg=="),
    ];

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "hexalith-parties-poison-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ValidatorHumanAndJsonOutputsRedactCuratedPoisonFixtures()
    {
        string workspace = CopyFixtureToWorkspace("poisoned-secrets");
        string daprPath = Path.Combine(workspace, "deploy", "dapr");
        string k8sPath = Path.Combine(workspace, "deploy", "k8s");

        ProcessResult human = RunValidator("-ConfigPath", daprPath, "-K8sPath", k8sPath);
        human.ExitCode.ShouldBe(1);
        AssertContainsDescriptor(human.CombinedOutput, "jwt-shaped");
        AssertContainsDescriptor(human.CombinedOutput, "base64-shaped");
        AssertContainsDescriptor(human.CombinedOutput, "password-prefixed");
        AssertContainsDescriptor(human.CombinedOutput, "docker-auth-shaped");
        AssertNoPoison(human.CombinedOutput);

        ProcessResult json = RunValidator("-ConfigPath", daprPath, "-K8sPath", k8sPath, "-Format", "json");
        json.ExitCode.ShouldBe(1);
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json.Stdout);
        document.RootElement.GetProperty("version").GetString().ShouldBe("1");
        AssertContainsDescriptor(json.CombinedOutput, "docker-auth-shaped");
        AssertNoPoison(json.CombinedOutput);
    }

    [Fact]
    public void ConfirmContextFailureRedactsKubeconfigShapedContextBeforeMutation()
    {
        string bin = Path.Combine(_tempRoot, "bin");
        Directory.CreateDirectory(bin);
        string logPath = Path.Combine(_tempRoot, "commands.log");
        WriteKubectlShim(bin, logPath, "certificate-authority-data eyJhbGciOiJIUzI1NiJ9 client-key-data");

        string script = CreateTempK8sScriptWorkspace();
        ProcessResult result = RunScript(script, bin, "-ConfirmContext", "sandbox-context");

        result.ExitCode.ShouldBe(2);
        AssertContainsDescriptor(result.CombinedOutput, "<redacted-context>");
        AssertNoPoison(result.CombinedOutput);
        File.ReadAllLines(logPath).ShouldBe(["kubectl config current-context"]);
    }

    private string CreateTempK8sScriptWorkspace()
    {
        string k8sRoot = Path.Combine(_tempRoot, "script-workspace", "deploy", "k8s");
        Directory.CreateDirectory(Path.Combine(k8sRoot, "_lib"));
        File.Copy(DeploymentTestPaths.RepoFile("deploy/k8s/publish.ps1"), Path.Combine(k8sRoot, "publish.ps1"));
        File.Copy(DeploymentTestPaths.RepoFile("deploy/k8s/_lib/Confirm-KubeContext.ps1"), Path.Combine(k8sRoot, "_lib", "Confirm-KubeContext.ps1"));
        return Path.Combine(k8sRoot, "publish.ps1");
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
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }

    private static void AssertContainsDescriptor(string text, string descriptor)
    {
        if (!text.Contains(descriptor, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected sanitized output descriptor '{descriptor}' was not present.");
        }
    }

    private static void AssertNoPoison(string text)
    {
        foreach ((string descriptor, string poison) in s_poisonValues)
        {
            if (text.Contains(poison, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Output leaked raw {descriptor} poison.");
            }
        }
    }

    private static ProcessResult RunValidator(params string[] arguments)
    {
        ProcessStartInfo start = CreatePwshStart(DeploymentTestPaths.RepositoryRoot);
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(DeploymentTestPaths.RepoFile("deploy/validate-deployment.ps1"));
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Run(start);
    }

    private static ProcessResult RunScript(string scriptPath, string binDirectory, params string[] arguments)
    {
        ProcessStartInfo start = CreatePwshStart(Path.GetDirectoryName(scriptPath)!);
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["PATH"] = binDirectory + Path.PathSeparator + start.Environment["PATH"];
        return Run(start);
    }

    private static ProcessStartInfo CreatePwshStart(string workingDirectory)
    {
        ProcessStartInfo start = new()
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        return start;
    }

    private static ProcessResult Run(ProcessStartInfo start)
    {
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start pwsh.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static void WriteKubectlShim(string binDirectory, string logPath, string currentContext)
    {
        string path = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kubectl.cmd" : "kubectl");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(path, $"""
@echo off
echo kubectl %*>> "{logPath}"
if "%1 %2"=="config current-context" (
  echo {currentContext}
  exit /b 0
)
exit /b 1
""");
            return;
        }

        File.WriteAllText(path, $"""
#!/usr/bin/env bash
printf 'kubectl %s\n' "$*" >> "{logPath}"
if [ "$1 $2" = "config current-context" ]; then
  printf '%s\n' "{currentContext}"
  exit 0
fi
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr)
    {
        public string CombinedOutput => Stdout + Stderr;
    }
}
