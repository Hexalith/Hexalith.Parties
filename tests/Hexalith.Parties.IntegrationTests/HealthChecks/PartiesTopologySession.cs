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
        // Hold candidates together and exclude prior in-process sessions. The eventual bind
        // still fails closed if an unrelated process acquires a selected listener meanwhile.
        lock (PortAllocationLock)
        {
            var listeners = new List<TcpListener>();
            try
            {
                while (listeners.Count < 7 + SidecarNames.Length)
                {
                    var listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    if (AllocatedPorts.Add(((IPEndPoint)listener.LocalEndpoint).Port))
                    {
                        listeners.Add(listener);
                    }
                    else
                    {
                        listener.Stop();
                    }
                }
                int[] ports = listeners.Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port).ToArray();
                SentryPort = ports[0]; SentryHealthPort = ports[1];
                PlacementPort = ports[2]; PlacementHealthPort = ports[3];
                SchedulerPort = ports[4]; SchedulerHealthPort = ports[5];
                EventStoreHttpPort = ports[6];
                InternalGrpcPorts = SidecarNames.Select((name, index) => new KeyValuePair<string, int>(name, ports[index + 7]))
                    .ToFrozenDictionary(StringComparer.Ordinal);
            }
            finally
            {
                foreach (TcpListener listener in listeners)
                {
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

    /// <summary>Gets explicitly allocated internal listeners, avoiding Dapr's colliding app-id hash defaults.</summary>
    internal IReadOnlyDictionary<string, int> InternalGrpcPorts { get; }

    /// <summary>Creates a distinct session without changing the process environment.</summary>
    internal static PartiesTopologySession Create(bool isolatedIdentities = false) => new(isolatedIdentities);

    /// <summary>Maps a logical resource name to its literal Dapr identity.</summary>
    internal string AppId(string name) => AppIdSuffix.Length == 0 ? name : name + "-" + AppIdSuffix;

    /// <summary>Returns model-construction settings before the AppHost executes.</summary>
    internal string[] Arguments(bool complete, bool mtls) =>
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
