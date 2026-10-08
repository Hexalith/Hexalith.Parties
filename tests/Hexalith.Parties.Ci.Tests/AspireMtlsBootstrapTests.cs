using System.Diagnostics;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>
/// Executes the mTLS bootstrap against isolated stopped-container command doubles.
/// </summary>
public sealed class AspireMtlsBootstrapTests
{
    /// <summary>
    /// Verifies persistent bindings permit all compatible stopped containers to restart.
    /// </summary>
    [Fact]
    public void BootstrapRestartsCompatibleStoppedContainersUsingPersistentBindings()
    {
        (int exitCode, string error, string[] requests) = RunBootstrap();

        exitCode.ShouldBe(0, error);
        foreach (string role in new[] { "sentry", "placement", "scheduler" })
        {
            requests.Count(request => request == $"docker container start hexalith-parties-dapr-{role}").ShouldBe(1);
        }

        requests.Count(request => request.Contains(".HostConfig.PortBindings", StringComparison.Ordinal)).ShouldBe(6);
        requests.ShouldNotContain(request => request.StartsWith("docker port ", StringComparison.Ordinal));
        requests.ShouldNotContain(request => request.StartsWith("docker run ", StringComparison.Ordinal));
        requests.ShouldContain("curl --fail --silent --show-error --max-time 1 http://127.0.0.1:18080/healthz");
        requests.ShouldContain("curl --fail --silent --show-error --max-time 1 http://127.0.0.1:18081/healthz");
        requests.ShouldContain("curl --fail --silent --show-error --max-time 1 http://127.0.0.1:18082/healthz");
        requests.Count(request => request.StartsWith("aspire start --apphost ", StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>
    /// Verifies unsafe addresses, incorrect mappings, and extra bindings fail before restart.
    /// </summary>
    /// <param name="role">The existing container whose binding is incompatible.</param>
    /// <param name="port">The container port with the incompatible persistent binding.</param>
    /// <param name="binding">The configured host binding returned by Docker inspect.</param>
    [Theory]
    [InlineData("sentry", "50001/tcp", "0.0.0.0:50001")]
    [InlineData("sentry", "18080/tcp", "127.0.0.1:18081")]
    [InlineData("placement", "50005/tcp", "0.0.0.0:55005")]
    [InlineData("placement", "8080/tcp", "127.0.0.1:18080")]
    [InlineData("scheduler", "50006/tcp", "0.0.0.0:55006")]
    [InlineData("scheduler", "8080/tcp", "127.0.0.1:18082\n0.0.0.0:18082")]
    public void BootstrapRejectsIncompatibleBindingsWithoutStartingTheContainer(string role, string port, string binding)
    {
        (int exitCode, string error, string[] requests) = RunBootstrap(role, port, binding);

        exitCode.ShouldNotBe(0);
        error.ShouldContain($"Existing hexalith-parties-dapr-{role}");
        requests.ShouldContain(request => request.Contains(".HostConfig.PortBindings", StringComparison.Ordinal)
            && request.EndsWith($"hexalith-parties-dapr-{role}", StringComparison.Ordinal));
        requests.ShouldNotContain($"docker container start hexalith-parties-dapr-{role}");
        requests.ShouldNotContain(request => request.StartsWith("docker run ", StringComparison.Ordinal));
        requests.ShouldNotContain(request => request.StartsWith("aspire ", StringComparison.Ordinal));
    }

    /// <summary>
    /// Runs the tracked script without contacting Docker, HTTP endpoints, or Aspire.
    /// </summary>
    /// <param name="incompatibleRole">Optional role with an incompatible binding.</param>
    /// <param name="incompatiblePort">Optional container port whose binding is overridden.</param>
    /// <param name="incompatibleBinding">Optional incompatible host binding.</param>
    /// <returns>The script exit code, stderr, and isolated command requests.</returns>
    private static (int ExitCode, string Error, string[] Requests) RunBootstrap(
        string incompatibleRole = "",
        string incompatiblePort = "",
        string incompatibleBinding = "")
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The mTLS bootstrap requires bash.");
        }

        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-mtls-bootstrap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string certificateDirectory = Path.Combine(temporary, "certificates");
            Directory.CreateDirectory(certificateDirectory);
            foreach (string file in new[] { "ca.crt", "issuer.crt", "issuer.key" })
            {
                File.WriteAllText(Path.Combine(certificateDirectory, file), "Isolated bootstrap test fixture.");
            }

            string requestLog = Path.Combine(temporary, "requests");
            WriteCommandDouble(temporary, "docker", """
                #!/usr/bin/env bash
                set -euo pipefail
                printf 'docker %s\n' "$*" >> "$FAKE_REQUEST_LOG"
                container="${!#}"
                if [[ "$1" == port ]]; then
                  # Runtime listings are empty for these stopped containers. A regression
                  # to docker port therefore makes the compatible-container test fail.
                  exit 0
                fi
                case "$container" in
                  hexalith-parties-dapr-sentry|hexalith-parties-dapr-placement|hexalith-parties-dapr-scheduler) ;;
                  *) echo 'Unexpected fixture Docker container.' >&2; exit 1 ;;
                esac
                if [[ "$*" == "container start $container" ]]; then
                  [[ ! -f "$FAKE_STATE_DIRECTORY/$container" ]]
                  printf 'true\n' > "$FAKE_STATE_DIRECTORY/$container"
                  exit 0
                fi
                if [[ "$*" == "container inspect $container" ]]; then exit 0; fi
                if [[ "$#" != 5 || "$1 $2 $3" != 'container inspect --format' ]]; then
                  echo 'Unexpected fixture Docker command.' >&2
                  exit 1
                fi
                format="$4"
                case "$format" in
                  '{{.State.Running}}')
                    if [[ -f "$FAKE_STATE_DIRECTORY/$container" ]]; then echo true; else echo false; fi ;;
                  '{{.Config.Image}}') printf '%s\n' "$FAKE_SENTRY_IMAGE" ;;
                  '{{index .Config.Labels "hexalith.parties.dapr.role"}}')
                    case "$container" in
                      *-placement) echo placement-v1 ;;
                      *-scheduler) echo scheduler-v2 ;;
                      *) exit 1 ;;
                    esac ;;
                  *'.Mounts'*'/etc/dapr/sentry.yaml'*) printf '%s\n' "$FAKE_SENTRY_CONFIGURATION" ;;
                  *'.Mounts'*'/var/run/dapr/credentials'*|*'.Mounts'*'/var/run/secrets/dapr.io/tls'*)
                    printf '%s\n' "$DAPR_MTLS_CERTIFICATE_DIRECTORY" ;;
                  *'.HostConfig.PortBindings'*)
                    port="${format#*\"}"
                    port="${port%%\"*}"
                    if [[ "$container" == "hexalith-parties-dapr-$FAKE_INCOMPATIBLE_ROLE" && "$port" == "$FAKE_INCOMPATIBLE_PORT" ]]; then
                      printf '%s\n' "$FAKE_INCOMPATIBLE_BINDING"
                      exit 0
                    fi
                    case "$container/$port" in
                      *-sentry/50001/tcp) echo 127.0.0.1:50001 ;;
                      *-sentry/18080/tcp) echo 127.0.0.1:18080 ;;
                      *-placement/50005/tcp) echo 127.0.0.1:55005 ;;
                      *-placement/8080/tcp) echo 127.0.0.1:18081 ;;
                      *-scheduler/50006/tcp) echo 127.0.0.1:55006 ;;
                      *-scheduler/8080/tcp) echo 127.0.0.1:18082 ;;
                      *) echo 'Unexpected fixture port binding.' >&2; exit 1 ;;
                    esac ;;
                  *) echo 'Unexpected fixture Docker inspect format.' >&2; exit 1 ;;
                esac
                """);
            WriteCommandDouble(temporary, "curl", """
                #!/usr/bin/env bash
                set -euo pipefail
                printf 'curl %s\n' "$*" >> "$FAKE_REQUEST_LOG"
                """);
            WriteCommandDouble(temporary, "aspire", """
                #!/usr/bin/env bash
                set -euo pipefail
                [[ "$Dapr__Mtls__Enabled" == true ]]
                [[ "$Dapr__Mtls__CertificateDirectory" == "$DAPR_MTLS_CERTIFICATE_DIRECTORY" ]]
                printf 'aspire %s\n' "$*" >> "$FAKE_REQUEST_LOG"
                """);

            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = CiTestPaths.RepositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(CiTestPaths.RepoFile("scripts/aspire-start-mtls.sh"));
            start.Environment["PATH"] = $"{temporary}{Path.PathSeparator}{start.Environment["PATH"]}";
            start.Environment["DAPR_MTLS_CERTIFICATE_DIRECTORY"] = certificateDirectory;
            start.Environment["FAKE_REQUEST_LOG"] = requestLog;
            start.Environment["FAKE_STATE_DIRECTORY"] = temporary;
            start.Environment["FAKE_INCOMPATIBLE_ROLE"] = incompatibleRole;
            start.Environment["FAKE_INCOMPATIBLE_PORT"] = incompatiblePort;
            start.Environment["FAKE_INCOMPATIBLE_BINDING"] = incompatibleBinding;
            start.Environment["FAKE_SENTRY_CONFIGURATION"] = CiTestPaths.RepoFile("src/Hexalith.Parties.AppHost/DaprComponents/sentry.yaml");
            start.Environment["FAKE_SENTRY_IMAGE"] = CiTestPaths.ReadRepoFile("scripts/aspire-start-mtls.sh")
                .Split('\n').Single(line => line.StartsWith("sentry_image=", StringComparison.Ordinal)).Split('"')[1];

            using Process process = new() { StartInfo = start };
            process.Start().ShouldBeTrue();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(10_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            string stderr = error.GetAwaiter().GetResult();
            output.GetAwaiter().GetResult();
            exited.ShouldBeTrue($"mTLS bootstrap timed out: {stderr}");
            return (process.ExitCode, stderr, File.Exists(requestLog) ? File.ReadAllLines(requestLog) : []);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Writes an executable command double into the private fixture directory.
    /// </summary>
    /// <param name="directory">The private directory prepended to the child PATH.</param>
    /// <param name="name">The command to replace.</param>
    /// <param name="script">The fixture command body.</param>
    private static void WriteCommandDouble(string directory, string name, string script)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, script.Replace("\r\n", "\n") + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
