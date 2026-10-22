using System.Diagnostics;

using Hexalith.Parties.TestInfrastructure;

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
        foreach (int port in new[] { 50001, 55005, 55006 })
        {
            requests.ShouldContain(request => request.StartsWith($"timeout 1 bash -c ", StringComparison.Ordinal)
                && request.EndsWith($"_ {port}", StringComparison.Ordinal));
        }
        requests.Count(request => request.StartsWith("aspire start --apphost ", StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>Verifies CI can prepare the secure control plane without launching a second AppHost.</summary>
    [Fact]
    public void PrepareOnlyStartsTheControlPlaneWithoutStartingAspire()
    {
        (int exitCode, string error, string[] requests) = RunBootstrap(prepareOnly: true);

        exitCode.ShouldBe(0, error);
        requests.ShouldNotContain(request => request.StartsWith("aspire ", StringComparison.Ordinal));
        foreach (string role in new[] { "sentry", "placement", "scheduler" })
        {
            requests.ShouldContain($"docker container start hexalith-parties-dapr-{role}");
        }
    }

    /// <summary>Rejects a network that cannot provide the scoped control-plane boundary.</summary>
    [Theory]
    [InlineData("host", "")]
    [InlineData("bridge", "unrelated-service")]
    public void BootstrapRejectsAnUntrustedControlPlaneNetwork(string driver, string extraMember)
    {
        ArgumentNullException.ThrowIfNull(extraMember);
        (int exitCode, string error, string[] requests) = RunBootstrap(
            networkDriver: driver,
            extraNetworkMember: extraMember);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("hexalith-parties-dapr-mtls");
        requests.ShouldNotContain(request => request.StartsWith("docker container start ", StringComparison.Ordinal));
        requests.ShouldNotContain(request => request.StartsWith("aspire ", StringComparison.Ordinal));
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

    /// <summary>Exercises scoped cleanup and verifies owned removal with foreign-resource preservation.</summary>
    [Theory]
    [InlineData("", "", true, "")]
    [InlineData("placement", "mount", true, "")]
    [InlineData("sentry", "image", true, "")]
    [InlineData("", "", false, "")]
    [InlineData("", "", true, "unrelated-service")]
    public void CleanupRemovesOnlyOwnedContainersAndAnOwnedEmptyNetwork(
        string foreignRole, string foreignKind, bool ownsNetwork, string unrelatedMember)
    {
        ArgumentNullException.ThrowIfNull(foreignRole);
        ArgumentNullException.ThrowIfNull(unrelatedMember);
        (int exitCode, string error, string[] requests) = RunBootstrap(
            cleanupOnly: true, foreignRole: foreignRole, foreignKind: foreignKind,
            ownsNetwork: ownsNetwork, extraNetworkMember: unrelatedMember);

        exitCode.ShouldBe(0, error);
        foreach (string role in new[] { "sentry", "placement", "scheduler" })
        {
            requests.Contains($"docker rm -f hexalith-parties-dapr-{role}").ShouldBe(role != foreignRole);
        }

        requests.Contains("docker network rm hexalith-parties-dapr-mtls")
            .ShouldBe(ownsNetwork && foreignRole.Length == 0 && unrelatedMember.Length == 0);
        requests.ShouldNotContain(request => request.StartsWith("aspire ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DAPR_MTLS_SENTRY_PORT", "42001", "custom Sentry port")]
    [InlineData("DAPR_MTLS_NAMESPACE", "parties-custom", "custom Dapr namespace")]
    public async Task UnsupportedIdentityCombinationsFailBeforeSideEffects(string key, string value, string diagnostic)
    {
        using var commands = new MtlsBootstrapCommandDouble();
        string certificates = Path.Combine(commands.Root, "not-created");
        (int exit, string error) = await commands.RunAsync("--prepare-only", certificates, TestContext.Current.CancellationToken,
            new Dictionary<string, string?> { ["DAPR_MTLS_SESSION_ID"] = "", [key] = value });
        exit.ShouldBe(2);
        error.ShouldContain(diagnostic);
        commands.Requests.ShouldBeEmpty();
        Directory.Exists(certificates).ShouldBeFalse();
    }

    [Fact]
    public async Task SessionLaunchExportsAllOwnedAppHostSettings()
    {
        using var commands = new MtlsBootstrapCommandDouble();
        string certificates = Path.Combine(commands.Root, "certificates");
        var settings = new Dictionary<string, string?>
        {
            ["DAPR_MTLS_SESSION_ID"] = "launch123", ["DAPR_MTLS_NAMESPACE"] = "parties-launch123",
            ["DAPR_MTLS_SENTRY_PORT"] = "42001", ["DAPR_MTLS_SENTRY_HEALTH_PORT"] = "42002",
            ["DAPR_MTLS_PLACEMENT_PORT"] = "42003", ["DAPR_MTLS_PLACEMENT_HEALTH_PORT"] = "42004",
            ["DAPR_MTLS_SCHEDULER_PORT"] = "42005", ["DAPR_MTLS_SCHEDULER_HEALTH_PORT"] = "42006",
        };
        (int exit, string error) = await commands.RunAsync("", certificates, TestContext.Current.CancellationToken, settings);
        exit.ShouldBe(0, error);
        IReadOnlyDictionary<string, string?> exported = commands.LaunchedConfiguration;
        exported["Dapr:Mtls:Enabled"].ShouldBe("true");
        exported["Dapr:Mtls:CertificateDirectory"].ShouldBe(certificates);
        exported["Dapr:Mtls:ConfigurationDirectory"].ShouldBe(Path.Combine(certificates, "configuration"));
        exported["Dapr:Mtls:Namespace"].ShouldBe("parties-launch123");
        exported["Dapr:Mtls:AppIdSuffix"].ShouldBe("launch123");
        exported["Dapr:Mtls:SessionId"].ShouldBe("launch123");
        exported["Dapr:Mtls:SentryHostAddress"].ShouldBe("127.0.0.1:42001");
        exported["Dapr:Mtls:PlacementHostAddress"].ShouldBe("127.0.0.1:42003");
        exported["Dapr:Mtls:SchedulerHostAddress"].ShouldBe("127.0.0.1:42005");
        commands.Requests.ShouldContain(request => request.StartsWith("aspire start --apphost ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SessionDefaultsSeparateTrustAndGeneratedConfigurationDirectories()
    {
        using var commands = new MtlsBootstrapCommandDouble();
        var paths = new List<string>();
        foreach (string session in new[] { "first123", "second123" })
        {
            string state = Path.Combine(commands.Root, "state");
            string expected = Path.Combine(state, "hexalith-parties", "dapr-certs-" + session);
            var settings = new Dictionary<string, string?>
            {
                ["DAPR_MTLS_SESSION_ID"] = session, ["DAPR_MTLS_NAMESPACE"] = "default",
                ["XDG_STATE_HOME"] = state, ["FAKE_MTLS_EXPECTED_CERTIFICATES"] = expected,
            };
            (int exit, string error) = await commands.RunAsync("", null, TestContext.Current.CancellationToken, settings);
            exit.ShouldBe(0, error);
            commands.LaunchedConfiguration["Dapr:Mtls:CertificateDirectory"].ShouldBe(expected);
            commands.LaunchedConfiguration["Dapr:Mtls:ConfigurationDirectory"].ShouldBe(Path.Combine(expected, "configuration"));
            File.Exists(Path.Combine(expected, "ca.crt")).ShouldBeTrue();
            paths.Add(expected);
        }
        paths.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        paths.ShouldAllBe(path => File.Exists(Path.Combine(path, "ca.crt")));
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
        string incompatibleBinding = "",
        bool prepareOnly = false,
        string networkDriver = "bridge",
        string extraNetworkMember = "",
        bool cleanupOnly = false,
        string foreignRole = "",
        string foreignKind = "",
        bool ownsNetwork = true)
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

            if (cleanupOnly)
            {
                foreach (string role in new[] { "sentry", "placement", "scheduler" })
                {
                    File.WriteAllText(Path.Combine(temporary, $"hexalith-parties-dapr-{role}"), "true");
                }

                File.WriteAllText(Path.Combine(temporary, "network"), "bridge");
                if (ownsNetwork)
                {
                    File.WriteAllText(Path.Combine(certificateDirectory, ".owned-control-plane-network"), string.Empty);
                }
            }

            string requestLog = Path.Combine(temporary, "requests");
            WriteCommandDouble(temporary, "docker", """
                #!/usr/bin/env bash
                set -euo pipefail
                printf 'docker %s\n' "$*" >> "$FAKE_REQUEST_LOG"
                if [[ "$1 $2" == 'container ls' ]]; then
                  for role in sentry placement scheduler; do
                    if [[ -f "$FAKE_STATE_DIRECTORY/hexalith-parties-dapr-$role" ]]; then
                      echo "hexalith-parties-dapr-$role"
                    fi
                  done
                  exit 0
                fi
                if [[ "$1 $2" == 'network ls' ]]; then
                  if [[ -f "$FAKE_STATE_DIRECTORY/network" ]]; then echo hexalith-parties-dapr-mtls; fi
                  exit 0
                fi
                if [[ "$1 $2" == 'network inspect' ]]; then
                  if [[ "$FAKE_CLEANUP_ONLY" == True && ! -f "$FAKE_STATE_DIRECTORY/network" ]]; then exit 1; fi
                  if [[ "$#" == 3 ]]; then exit 0; fi
                  case "$4" in
                    '{{len .Containers}}')
                      count=0
                      for role in sentry placement scheduler; do
                        if [[ -f "$FAKE_STATE_DIRECTORY/hexalith-parties-dapr-$role" ]]; then count=$((count + 1)); fi
                      done
                      if [[ -n "$FAKE_EXTRA_NETWORK_MEMBER" ]]; then count=$((count + 1)); fi
                      echo "$count" ;;
                    '{{.Driver}}') echo "$FAKE_NETWORK_DRIVER" ;;
                    '{{index .Labels "hexalith.parties.dapr.role"}}') echo control-plane-v1 ;;
                    '{{range .Containers}}{{println .Name}}{{end}}')
                      printf '%s\n' hexalith-parties-dapr-sentry hexalith-parties-dapr-placement hexalith-parties-dapr-scheduler
                      if [[ -n "$FAKE_EXTRA_NETWORK_MEMBER" ]]; then echo "$FAKE_EXTRA_NETWORK_MEMBER"; fi ;;
                    *) echo 'Unexpected fixture network inspect format.' >&2; exit 1 ;;
                  esac
                  exit 0
                fi
                if [[ "$1 $2" == 'network rm' ]]; then
                  rm "$FAKE_STATE_DIRECTORY/network"
                  exit 0
                fi
                container="${!#}"
                if [[ "$1 $2" == 'rm -f' ]]; then
                  rm "$FAKE_STATE_DIRECTORY/$container"
                  exit 0
                fi
                if [[ "$FAKE_CLEANUP_ONLY" == True && ! -f "$FAKE_STATE_DIRECTORY/$container" ]]; then exit 1; fi
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
                  '{{.Config.Image}}')
                    if [[ "$container" == "hexalith-parties-dapr-$FAKE_FOREIGN_ROLE" && "$FAKE_FOREIGN_KIND" == image ]]; then
                      echo foreign/image
                    else printf '%s\n' "$FAKE_SENTRY_IMAGE"; fi ;;
                  '{{index .Config.Labels "hexalith.parties.dapr.role"}}')
                    case "$container" in
                      *-placement) echo placement-v1 ;;
                      *-scheduler) echo scheduler-v2 ;;
                      *) exit 1 ;;
                    esac ;;
                  *'.Mounts'*'/etc/dapr/sentry.yaml'*) printf '%s\n' "$FAKE_SENTRY_CONFIGURATION" ;;
                  *'.Mounts'*'/var/run/dapr/credentials'*|*'.Mounts'*'/var/run/secrets/dapr.io/tls'*)
                    if [[ "$container" == "hexalith-parties-dapr-$FAKE_FOREIGN_ROLE" && "$FAKE_FOREIGN_KIND" == mount ]]; then
                      echo /unrelated/certificates
                    else printf '%s\n' "$DAPR_MTLS_CERTIFICATE_DIRECTORY"; fi ;;
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
                  *'.NetworkSettings.Networks'*) echo hexalith-parties-dapr-mtls ;;
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
                [[ "$Dapr__Mtls__Namespace" == default ]]
                [[ "$Dapr__Mtls__PlacementHostAddress" == 127.0.0.1:55005 ]]
                [[ "$Dapr__Mtls__SchedulerHostAddress" == 127.0.0.1:55006 ]]
                printf 'aspire %s\n' "$*" >> "$FAKE_REQUEST_LOG"
                """);
            WriteCommandDouble(temporary, "timeout", """
                #!/usr/bin/env bash
                set -euo pipefail
                printf 'timeout %s\n' "$*" >> "$FAKE_REQUEST_LOG"
                """);

            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = CiTestPaths.RepositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(CiTestPaths.RepoFile("scripts/aspire-start-mtls.sh"));
            if (cleanupOnly)
            {
                start.ArgumentList.Add("--cleanup-only");
            }
            else if (prepareOnly)
            {
                start.ArgumentList.Add("--prepare-only");
            }
            start.Environment["PATH"] = $"{temporary}{Path.PathSeparator}{start.Environment["PATH"]}";
            start.Environment["DAPR_MTLS_CERTIFICATE_DIRECTORY"] = certificateDirectory;
            start.Environment["FAKE_CLEANUP_ONLY"] = cleanupOnly.ToString();
            start.Environment["FAKE_FOREIGN_ROLE"] = foreignRole;
            start.Environment["FAKE_FOREIGN_KIND"] = foreignKind;
            start.Environment["FAKE_REQUEST_LOG"] = requestLog;
            start.Environment["FAKE_STATE_DIRECTORY"] = temporary;
            start.Environment["FAKE_INCOMPATIBLE_ROLE"] = incompatibleRole;
            start.Environment["FAKE_INCOMPATIBLE_PORT"] = incompatiblePort;
            start.Environment["FAKE_INCOMPATIBLE_BINDING"] = incompatibleBinding;
            start.Environment["FAKE_NETWORK_DRIVER"] = networkDriver;
            start.Environment["FAKE_EXTRA_NETWORK_MEMBER"] = extraNetworkMember;
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
            if (cleanupOnly)
            {
                foreach (string role in new[] { "sentry", "placement", "scheduler" })
                {
                    File.Exists(Path.Combine(temporary, $"hexalith-parties-dapr-{role}")).ShouldBe(role == foreignRole);
                }

                File.Exists(Path.Combine(temporary, "network"))
                    .ShouldBe(!ownsNetwork || foreignRole.Length > 0 || extraNetworkMember.Length > 0);
            }

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
