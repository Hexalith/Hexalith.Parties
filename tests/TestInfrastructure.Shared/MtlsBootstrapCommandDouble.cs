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
            suffix="${DAPR_MTLS_SESSION_ID:+-${DAPR_MTLS_SESSION_ID}}"
            certificates="${DAPR_MTLS_CERTIFICATE_DIRECTORY:-${FAKE_MTLS_EXPECTED_CERTIFICATES:-}}"
            sentry="hexalith-parties-dapr-sentry${suffix}"
            placement="hexalith-parties-dapr-placement${suffix}"
            scheduler="hexalith-parties-dapr-scheduler${suffix}"
            network="hexalith-parties-dapr-mtls${suffix}"
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
                  if [[ -f "$FAKE_MTLS_STATE/hexalith-parties-dapr-$role${suffix}" ]]; then echo "hexalith-parties-dapr-$role${suffix}"; fi
                done
                exit 0 ;;
              'network ls')
                echo unrelated-network
                if [[ -f "$FAKE_MTLS_STATE/network${suffix}" ]]; then echo ${network}; fi
                exit 0 ;;
              'network create') touch "$FAKE_MTLS_STATE/network${suffix}"; exit 0 ;;
              'network rm')
                [[ "${!#}" == ${network} ]]
                rm "$FAKE_MTLS_STATE/network${suffix}"; exit 0 ;;
              'network inspect')
                [[ "${!#}" == ${network} && -f "$FAKE_MTLS_STATE/network${suffix}" ]] || exit 1
                if [[ "$#" == 3 ]]; then exit 0; fi
                case "$4" in
                  '{{.Driver}}') echo bridge ;;
                  '{{index .Labels "hexalith.parties.dapr.role"}}') echo "${FAKE_MTLS_NETWORK_LABEL:-control-plane-v1}" ;;
                  '{{range .Containers}}{{println .Name}}{{end}}')
                    for role in sentry placement scheduler; do
                      if [[ -f "$FAKE_MTLS_STATE/hexalith-parties-dapr-$role${suffix}" ]]; then echo "hexalith-parties-dapr-$role${suffix}"; fi
                    done ;;
                  '{{len .Containers}}')
                    count=0
                    for role in sentry placement scheduler; do
                      if [[ -f "$FAKE_MTLS_STATE/hexalith-parties-dapr-$role${suffix}" ]]; then count=$((count + 1)); fi
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
                ${sentry})
                  for file in ca.crt issuer.crt issuer.key; do
                    printf 'isolated test credential\n' > "$certificates/$file"
                  done ;;
                ${placement}|${scheduler}) ;;
                *) exit 92 ;;
              esac
              touch "$FAKE_MTLS_STATE/$name"
              exit 0
            fi
            container="${!#}"
            case "$container" in
              ${sentry}|${placement}|${scheduler}) ;;
              *) echo 'Foreign resource must not be inspected or modified.' >&2; exit 93 ;;
            esac
            if [[ "$1 $2" == 'rm -f' ]]; then rm "$FAKE_MTLS_STATE/$container"; exit 0; fi
            if [[ "$1 $2" != 'container inspect' || ! -f "$FAKE_MTLS_STATE/$container" ]]; then exit 1; fi
            if [[ "$#" == 3 ]]; then exit 0; fi
            case "$4" in
              '{{.State.Running}}') echo true ;;
              '{{.Config.Image}}') echo "$FAKE_MTLS_IMAGE" ;;
              '{{index .Config.Labels "hexalith.parties.dapr.role"}}')
                case "$container" in *-placement${suffix}) echo placement-v1 ;; *-scheduler${suffix}) echo scheduler-v2 ;; *) exit 94 ;; esac ;;
              *'.Mounts'*'/etc/dapr/sentry.yaml'*) echo "$FAKE_MTLS_SENTRY_CONFIG" ;;
              *'.Mounts'*) echo "$certificates" ;;
              *'.HostConfig.PortBindings'*)
                port="${4#*\"}"
                port="${port%%\"*}"
                case "$container/$port" in
                  *-sentry${suffix}/50001/tcp) echo 127.0.0.1:${DAPR_MTLS_SENTRY_PORT:-50001} ;;
                  *-sentry${suffix}/18080/tcp) echo 127.0.0.1:${DAPR_MTLS_SENTRY_HEALTH_PORT:-18080} ;;
                  *-placement${suffix}/50005/tcp) echo 127.0.0.1:${DAPR_MTLS_PLACEMENT_PORT:-55005} ;;
                  *-placement${suffix}/8080/tcp) echo 127.0.0.1:${DAPR_MTLS_PLACEMENT_HEALTH_PORT:-18081} ;;
                  *-scheduler${suffix}/50006/tcp) echo 127.0.0.1:${DAPR_MTLS_SCHEDULER_PORT:-55006} ;;
                  *-scheduler${suffix}/8080/tcp) echo 127.0.0.1:${DAPR_MTLS_SCHEDULER_HEALTH_PORT:-18082} ;;
                  *) exit 95 ;;
                esac ;;
              *'.NetworkSettings.Networks'*) echo ${network} ;;
              *) exit 96 ;;
            esac
            """);
        WriteCommand("aspire", """
            #!/usr/bin/env bash
            set -euo pipefail
            printf 'aspire %s\n' "$*" >> "$FAKE_MTLS_STATE/requests"
            : > "$FAKE_MTLS_STATE/launched-configuration"
            for name in Dapr__Mtls__Enabled Dapr__Mtls__CertificateDirectory Dapr__Mtls__Namespace Dapr__Mtls__PlacementHostAddress Dapr__Mtls__SchedulerHostAddress Dapr__Mtls__SentryHostAddress Dapr__Mtls__ConfigurationDirectory Dapr__Mtls__AppIdSuffix Dapr__Mtls__SessionId; do
                printf '%s=%s\n' "$name" "${!name-}" >> "$FAKE_MTLS_STATE/launched-configuration"
            done
            """);
        WriteCommand("curl", "#!/usr/bin/env bash\nexit 0");
        WriteCommand("timeout", "#!/usr/bin/env bash\nexit 0");
    }

    /// <summary>Gets the private resource-state directory.</summary>
    internal string Root { get; }

    /// <summary>Gets the command requests issued by the real tracked bootstrap script.</summary>
    internal string[] Requests => File.Exists(Path.Combine(Root, "requests"))
        ? File.ReadAllLines(Path.Combine(Root, "requests")) : [];

    /// <summary>Gets settings captured by the executable at the real script launch boundary.</summary>
    internal IReadOnlyDictionary<string, string?> LaunchedConfiguration => File.ReadAllLines(Path.Combine(Root, "launched-configuration"))
        .Select(line => line.Split('=', 2)).ToDictionary(pair => pair[0].Replace("__", ":", StringComparison.Ordinal), pair => (string?)pair[1]);

    /// <summary>Gets the owned container roles still present in the isolated Docker model.</summary>
    internal string[] OwnedRoles => new[] { "sentry", "placement", "scheduler" }
        .Where(role => Directory.EnumerateFiles(Root, $"hexalith-parties-dapr-{role}*").Any()).ToArray();

    /// <summary>Gets whether the owned bridge remains.</summary>
    internal bool OwnedNetworkExists => Directory.EnumerateFiles(Root, "network*").Any();

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
    internal async Task<(int ExitCode, string Error)> RunAsync(string option, string? certificates, CancellationToken cancellationToken, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var start = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(_script);
        if (option.Length > 0) { start.ArgumentList.Add(option); }
        foreach ((string name, string? value) in _environment)
        {
            start.Environment[name] = value;
        }

        if (settings is not null)
        {
            foreach ((string name, string? value) in settings) { start.Environment[name] = value; }
        }
        if (certificates is null) { start.Environment.Remove("DAPR_MTLS_CERTIFICATE_DIRECTORY"); }
        else { start.Environment["DAPR_MTLS_CERTIFICATE_DIRECTORY"] = certificates; }
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
