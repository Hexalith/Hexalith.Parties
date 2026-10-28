using System.Globalization;
using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Immutable identities, private paths and loopback listeners owned by one topology lifetime.</summary>
internal sealed class PartiesTopologySession
{
    private static readonly Lock PortAllocationLock = new();
    private static readonly HashSet<int> AllocatedPorts = [];
    private static readonly string[] SidecarNames = ["eventstore", "eventstore-admin", "eventstore-admin-ui", "memories", "parties", "sample", "sample-blazor-ui", "tenants"];
    private PartiesTopologySession(bool isolatedIdentities)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        AppIdSuffix = isolatedIdentities ? Id : string.Empty;
        Namespace = isolatedIdentities ? "parties-" + Id : "default";
        Root = Path.Combine(Path.GetTempPath(), "hexalith-parties-topology-" + Id);
        CertificateDirectory = Path.Combine(Root, "certificates");
        ConfigurationDirectory = Path.Combine(Root, "configuration");
        // Released listener probes must not be reused as automatic outbound client ports.
        // Linux exposes its configured range; other supported hosts use the IANA dynamic range.
        int[] ephemeral = OperatingSystem.IsLinux()
            ? File.ReadAllText("/proc/sys/net/ipv4/ip_local_port_range")
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray()
            : [49_152, 65_535];
        if (ephemeral.Length != 2 || ephemeral[0] < 1_024 || ephemeral[1] > 65_535 || ephemeral[0] > ephemeral[1])
        {
            throw new InvalidOperationException("The OS ephemeral client port range could not be validated.");
        }
        // Hold candidates together and exclude prior in-process sessions. Explicit binds can
        // still race after release; readiness continues to fail closed on any such collision.
        lock (PortAllocationLock)
        {
            var listeners = new List<TcpListener>();
            bool allocated = false;
            try
            {
                for (int attempt = 0; listeners.Count < 8 + SidecarNames.Length; attempt++)
                {
                    if (attempt >= 1_024)
                    {
                        throw new InvalidOperationException("Could not reserve topology listeners outside the OS ephemeral client port range.");
                    }
                    // Avoid the IANA dynamic range on an external Docker host as well.
                    int candidate = Random.Shared.Next(20_000, 30_000);
                    if ((candidate >= ephemeral[0] && candidate <= ephemeral[1]) || AllocatedPorts.Contains(candidate)) { continue; }
                    var listener = new TcpListener(Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, candidate);
                    try
                    {
                        if (Socket.OSSupportsIPv6) { listener.Server.DualMode = true; }
                        listener.Start();
                    }
                    catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        listener.Stop();
                        continue;
                    }
                    catch
                    {
                        listener.Stop();
                        throw;
                    }
                    AllocatedPorts.Add(candidate);
                    listeners.Add(listener);
                }
                int[] ports = listeners.Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port).ToArray();
                SentryPort = ports[0]; SentryHealthPort = ports[1];
                PlacementPort = ports[2]; PlacementHealthPort = ports[3];
                SchedulerPort = ports[4]; SchedulerHealthPort = ports[5];
                EventStoreHttpPort = ports[6];
                MemoriesDomainInternalGrpcPort = ports[^1];
                InternalGrpcPorts = SidecarNames.Select((name, index) => new KeyValuePair<string, int>(name, ports[index + 7]))
                    .ToFrozenDictionary(StringComparer.Ordinal);
                allocated = true;
            }
            finally
            {
                foreach (TcpListener listener in listeners)
                {
                    if (!allocated) { AllocatedPorts.Remove(((IPEndPoint)listener.LocalEndpoint).Port); }
                    listener.Stop();
                }
            }
        }
    }

    /// <summary>Gets the session identity used to name its control plane.</summary>
    internal string Id { get; }
    /// <summary>Gets the optional literal Dapr identity suffix for concurrent profiles.</summary>
    internal string AppIdSuffix { get; }
    /// <summary>Gets the Dapr certificate/ACL namespace.</summary>
    internal string Namespace { get; }
    /// <summary>Gets the owned private root retained when cleanup fails.</summary>
    internal string Root { get; }
    /// <summary>Gets the trust bundle directory.</summary>
    internal string CertificateDirectory { get; }
    /// <summary>Gets the generated configuration directory.</summary>
    internal string ConfigurationDirectory { get; }
    /// <summary>Gets the Sentry listener.</summary>
    internal int SentryPort { get; }
    /// <summary>Gets the Sentry health listener.</summary>
    internal int SentryHealthPort { get; }
    /// <summary>Gets the placement listener.</summary>
    internal int PlacementPort { get; }
    /// <summary>Gets the placement health listener.</summary>
    internal int PlacementHealthPort { get; }
    /// <summary>Gets the scheduler listener.</summary>
    internal int SchedulerPort { get; }
    /// <summary>Gets the scheduler health listener.</summary>
    internal int SchedulerHealthPort { get; }
    /// <summary>Gets the gateway's sidecar HTTP listener.</summary>
    internal int EventStoreHttpPort { get; }

    /// <summary>Gets an optional listener reserved only for the three-target Memories component profile.</summary>
    internal int MemoriesDomainInternalGrpcPort { get; }

    /// <summary>Gets explicitly allocated internal listeners, avoiding Dapr's colliding app-id hash defaults.</summary>
    internal IReadOnlyDictionary<string, int> InternalGrpcPorts { get; }

    /// <summary>Creates a distinct session without changing the process environment.</summary>
    internal static PartiesTopologySession Create(bool isolatedIdentities = false) => new(isolatedIdentities);

    /// <summary>Maps a logical resource name to its literal Dapr identity.</summary>
    internal string AppId(string name) => AppIdSuffix.Length == 0 ? name : name + "-" + AppIdSuffix;

    /// <summary>Returns model-construction settings before the AppHost executes.</summary>
    internal string[] Arguments(bool complete, bool mtls, bool includeMemoriesDomainService = false) =>
    [
        "--EnableKeycloak=false", "--environment=Development",
        $"--EnableMemoriesSearch={complete}", $"--EnableEventStoreSampleUi={complete}",
        $"--Dapr:Mtls:Enabled={mtls}", $"--Dapr:Mtls:CertificateDirectory={CertificateDirectory}",
        $"--Dapr:Mtls:ConfigurationDirectory={ConfigurationDirectory}", $"--Dapr:Mtls:Namespace={Namespace}",
        $"--Dapr:Mtls:AppIdSuffix={AppIdSuffix}", $"--Dapr:Mtls:SessionId={Id}",
        $"--Dapr:Mtls:SentryHostAddress=127.0.0.1:{SentryPort}",
        $"--Dapr:Mtls:PlacementHostAddress=127.0.0.1:{PlacementPort}",
        $"--Dapr:Mtls:SchedulerHostAddress=127.0.0.1:{SchedulerPort}",
        $"--Dapr:EventStoreHttpPort={EventStoreHttpPort}",
        .. includeMemoriesDomainService ? new[] { "--EnableMemoriesDomainService=true", $"--Dapr:Mtls:InternalGrpcPorts:memories-domain-service={MemoriesDomainInternalGrpcPort}" } : Array.Empty<string>(),
        .. InternalGrpcPorts.Select(pair => $"--Dapr:Mtls:InternalGrpcPorts:{pair.Key}={pair.Value}"),
    ];

    /// <summary>Populates only a bootstrap child process with the exact owned control-plane configuration.</summary>
    internal void ConfigureBootstrapEnvironment(IDictionary<string, string?> environment)
    {
        environment["DAPR_MTLS_SESSION_ID"] = Id;
        environment["DAPR_MTLS_NAMESPACE"] = Namespace;
        environment["DAPR_MTLS_CERTIFICATE_DIRECTORY"] = CertificateDirectory;
        environment["DAPR_MTLS_SENTRY_PORT"] = SentryPort.ToString(CultureInfo.InvariantCulture);
        environment["DAPR_MTLS_SENTRY_HEALTH_PORT"] = SentryHealthPort.ToString(CultureInfo.InvariantCulture);
        environment["DAPR_MTLS_PLACEMENT_PORT"] = PlacementPort.ToString(CultureInfo.InvariantCulture);
        environment["DAPR_MTLS_PLACEMENT_HEALTH_PORT"] = PlacementHealthPort.ToString(CultureInfo.InvariantCulture);
        environment["DAPR_MTLS_SCHEDULER_PORT"] = SchedulerPort.ToString(CultureInfo.InvariantCulture);
        environment["DAPR_MTLS_SCHEDULER_HEALTH_PORT"] = SchedulerHealthPort.ToString(CultureInfo.InvariantCulture);
    }
}
