using System.Diagnostics;

namespace Hexalith.Parties.TestInfrastructure;

/// <summary>Runs the tracked mTLS bootstrap with isolated, stateful command doubles.</summary>
internal sealed class MtlsBootstrapCommandDouble : IDisposable
{
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);
    private readonly string _script;
    private readonly string _commands;
    private readonly Dictionary<string, string?> _environment;

    /// <summary>Creates a private command/state directory and foreign resources that must survive cleanup.</summary>
    internal MtlsBootstrapCommandDouble()
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "global.json")))
        {
            repository = repository.Parent;
        }

        string repositoryRoot = repository?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        _script = Path.Combine(repositoryRoot, "scripts", "aspire-start-mtls.sh");
        Root = Path.Combine(Path.GetTempPath(), $"parties-mtls-double-{Guid.NewGuid():N}");
        _commands = Path.Combine(Root, "commands");
        Directory.CreateDirectory(_commands);
        File.WriteAllText(Path.Combine(Root, "unrelated-service"), "foreign container");
        File.WriteAllText(Path.Combine(Root, "unrelated-network"), "foreign network");
        _environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = _commands + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            ["FAKE_MTLS_STATE"] = Root,
            ["FAKE_MTLS_IMAGE"] = File.ReadAllLines(_script)
                .Single(line => line.StartsWith("sentry_image=", StringComparison.Ordinal)).Split('"')[1],
            ["FAKE_MTLS_SENTRY_CONFIG"] = Path.Combine(repositoryRoot, "src", "Hexalith.Parties.AppHost", "DaprComponents", "sentry.yaml"),
        };
        WriteCommand("docker", """
            #!/usr/bin/env bash
            set -euo pipefail
            printf 'docker %s\n' "$*" >> "$FAKE_MTLS_STATE/requests"
            failure="$(cat "$FAKE_MTLS_STATE/failure" 2>/dev/null || true)"
            case "$1 $2" in
              'container ls'|'network ls'|'container inspect'|'network inspect')
                if [[ "$failure" == "$1-$2" || "$failure" == unavailable ]]; then
                  echo 'Docker unavailable (isolated command double).' >&2
                  exit 1
                fi ;;
            esac
            case "$1 $2" in
              'container ls')
                echo unrelated-service
                for role in sentry placement scheduler; do
                  if [[ -f "$FAKE_MTLS_STATE/hexalith-parties-dapr-$role" ]]; then echo "hexalith-parties-dapr-$role"; fi
                done
                exit 0 ;;
              'network ls')
                echo unrelated-network
                if [[ -f "$FAKE_MTLS_STATE/network" ]]; then echo hexalith-parties-dapr-mtls; fi
                exit 0 ;;
              'network create') touch "$FAKE_MTLS_STATE/network"; exit 0 ;;
              'network rm')
                [[ "${!#}" == hexalith-parties-dapr-mtls ]]
                rm "$FAKE_MTLS_STATE/network"; exit 0 ;;
              'network inspect')
                [[ "${!#}" == hexalith-parties-dapr-mtls && -f "$FAKE_MTLS_STATE/network" ]] || exit 1
                if [[ "$#" == 3 ]]; then exit 0; fi
                case "$4" in
                  '{{.Driver}}') echo bridge ;;
                  '{{index .Labels "hexalith.parties.dapr.role"}}') echo control-plane-v1 ;;
                  '{{range .Containers}}{{println .Name}}{{end}}')
                    for role in sentry placement scheduler; do
                      if [[ -f "$FAKE_MTLS_STATE/hexalith-parties-dapr-$role" ]]; then echo "hexalith-parties-dapr-$role"; fi
                    done ;;
                  '{{len .Containers}}')
                    count=0
                    for role in sentry placement scheduler; do
                      if [[ -f "$FAKE_MTLS_STATE/hexalith-parties-dapr-$role" ]]; then count=$((count + 1)); fi
                    done
                    echo "$count" ;;
                  *) exit 91 ;;
                esac
                exit 0 ;;
            esac
            if [[ "$1" == run ]]; then
              name=''
              while (( $# )); do
                if [[ "$1" == --name ]]; then name="$2"; break; fi
                shift
              done
              case "$name" in
                hexalith-parties-dapr-sentry)
                  for file in ca.crt issuer.crt issuer.key; do
                    printf 'isolated test credential\n' > "$DAPR_MTLS_CERTIFICATE_DIRECTORY/$file"
                  done ;;
                hexalith-parties-dapr-placement|hexalith-parties-dapr-scheduler) ;;
                *) exit 92 ;;
              esac
              touch "$FAKE_MTLS_STATE/$name"
              exit 0
            fi
            container="${!#}"
            case "$container" in
              hexalith-parties-dapr-sentry|hexalith-parties-dapr-placement|hexalith-parties-dapr-scheduler) ;;
              *) echo 'Foreign resource must not be inspected or modified.' >&2; exit 93 ;;
            esac
            if [[ "$1 $2" == 'rm -f' ]]; then rm "$FAKE_MTLS_STATE/$container"; exit 0; fi
            if [[ "$1 $2" != 'container inspect' || ! -f "$FAKE_MTLS_STATE/$container" ]]; then exit 1; fi
            if [[ "$#" == 3 ]]; then exit 0; fi
            case "$4" in
              '{{.State.Running}}') echo true ;;
              '{{.Config.Image}}') echo "$FAKE_MTLS_IMAGE" ;;
              '{{index .Config.Labels "hexalith.parties.dapr.role"}}')
                case "$container" in *-placement) echo placement-v1 ;; *-scheduler) echo scheduler-v2 ;; *) exit 94 ;; esac ;;
              *'.Mounts'*'/etc/dapr/sentry.yaml'*) echo "$FAKE_MTLS_SENTRY_CONFIG" ;;
              *'.Mounts'*) echo "$DAPR_MTLS_CERTIFICATE_DIRECTORY" ;;
              *'.HostConfig.PortBindings'*)
                port="${4#*\"}"
                port="${port%%\"*}"
                case "$container/$port" in
                  *-sentry/50001/tcp) echo 127.0.0.1:50001 ;;
                  *-sentry/18080/tcp) echo 127.0.0.1:18080 ;;
                  *-placement/50005/tcp) echo 127.0.0.1:55005 ;;
                  *-placement/8080/tcp) echo 127.0.0.1:18081 ;;
                  *-scheduler/50006/tcp) echo 127.0.0.1:55006 ;;
                  *-scheduler/8080/tcp) echo 127.0.0.1:18082 ;;
                  *) exit 95 ;;
                esac ;;
              *'.NetworkSettings.Networks'*) echo hexalith-parties-dapr-mtls ;;
              *) exit 96 ;;
            esac
            """);
        WriteCommand("curl", "#!/usr/bin/env bash\nexit 0");
        WriteCommand("timeout", "#!/usr/bin/env bash\nexit 0");
    }

    /// <summary>Gets the private resource-state directory.</summary>
    internal string Root { get; }

    /// <summary>Gets the command requests issued by the real tracked bootstrap script.</summary>
    internal string[] Requests => File.Exists(Path.Combine(Root, "requests"))
        ? File.ReadAllLines(Path.Combine(Root, "requests")) : [];

    /// <summary>Gets the owned container roles still present in the isolated Docker model.</summary>
    internal string[] OwnedRoles => new[] { "sentry", "placement", "scheduler" }
        .Where(role => File.Exists(Path.Combine(Root, $"hexalith-parties-dapr-{role}"))).ToArray();

    /// <summary>Gets whether the owned bridge remains.</summary>
    internal bool OwnedNetworkExists => File.Exists(Path.Combine(Root, "network"));

    /// <summary>Gets whether foreign container/network contents remain unchanged.</summary>
    internal bool ForeignResourcesPreserved => File.ReadAllText(Path.Combine(Root, "unrelated-service")) == "foreign container"
        && File.ReadAllText(Path.Combine(Root, "unrelated-network")) == "foreign network";

    /// <summary>Selects an enumeration/inspection failure; null restores the Docker model.</summary>
    internal void SetFailure(string? operation) => File.WriteAllText(Path.Combine(Root, "failure"), operation ?? string.Empty);

    /// <summary>Applies the command doubles and caller settings to this process for fixture child commands.</summary>
    internal void ApplyToCurrentProcess(IReadOnlyDictionary<string, string?> settings)
    {
        foreach ((string name, string? value) in _environment.Concat(settings))
        {
            _previousEnvironment.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>Executes preparation or cleanup without contacting Docker or network services.</summary>
    internal async Task<(int ExitCode, string Error)> RunAsync(string option, string certificates, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(_script);
        start.ArgumentList.Add(option);
        foreach ((string name, string? value) in _environment)
        {
            start.Environment[name] = value;
        }

        start.Environment["DAPR_MTLS_CERTIFICATE_DIRECTORY"] = certificates;
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Bootstrap could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

        _ = await output.ConfigureAwait(false);
        return (process.ExitCode, await error.ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach ((string name, string? value) in _previousEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        Directory.Delete(Root, recursive: true);
    }

    private void WriteCommand(string name, string content)
    {
        string path = Path.Combine(_commands, name);
        File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
