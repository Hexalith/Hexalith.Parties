using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Microsoft.Extensions.Configuration;

namespace Hexalith.Parties.AppHost;

/// <summary>Loads one self-hosted Dapr trust bundle and applies it only to Dapr sidecar resources.</summary>
internal sealed class DaprMtlsBootstrap
{
    private const string CertificateDirectoryConfigurationKey = "Dapr:Mtls:CertificateDirectory";
    private const string EnabledConfigurationKey = "Dapr:Mtls:Enabled";
    private readonly string _namespace;
    private readonly string _placementHostAddress;
    private readonly string _schedulerHostAddress;
    private readonly string _configurationDirectory;
    private readonly string _appIdSuffix;
    private readonly string? _sentryHostAddress;
    private readonly string _daprdPath;
    private readonly IReadOnlyDictionary<string, int> _internalGrpcPorts;
    private const string PlacementHostAddress = "127.0.0.1:55005";
    private const string SchedulerHostAddress = "127.0.0.1:55006";
    private const string TrustDomain = "public";
    private readonly string _certificateChain;
    private readonly string _certificateKey;
    private readonly string _trustAnchors;

    private DaprMtlsBootstrap(string trustAnchors, string certificateChain, string certificateKey, IConfiguration configuration)
    {
        _internalGrpcPorts = configuration.GetSection("Dapr:Mtls:InternalGrpcPorts").Get<Dictionary<string, int>>() ?? [];
        if (_internalGrpcPorts.Values.Any(port => port < 1024 || port > 65535)
            || _internalGrpcPorts.Values.Distinct().Count() != _internalGrpcPorts.Count)
        {
            throw new InvalidOperationException("Dapr internal gRPC listeners must be distinct unprivileged ports.");
        }
        _namespace = configuration["Dapr:Mtls:Namespace"] ?? "default";
        _placementHostAddress = configuration["Dapr:Mtls:PlacementHostAddress"] ?? PlacementHostAddress;
        _schedulerHostAddress = configuration["Dapr:Mtls:SchedulerHostAddress"] ?? SchedulerHostAddress;
        _configurationDirectory = configuration["Dapr:Mtls:ConfigurationDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "DaprComponents-mtls");
        _appIdSuffix = configuration["Dapr:Mtls:AppIdSuffix"] ?? string.Empty;
        _sentryHostAddress = configuration["Dapr:Mtls:SentryHostAddress"];
        _daprdPath = configuration["Dapr:Mtls:DaprdPath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr", "bin", "daprd");
        if (!Path.IsPathFullyQualified(_configurationDirectory)
            || !System.Text.RegularExpressions.Regex.IsMatch(_namespace, "^[a-z0-9-]+$")
            || (_appIdSuffix.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(_appIdSuffix, "^[a-z0-9-]+$")))
        {
            throw new InvalidOperationException("Dapr mTLS session identities and configuration directory are invalid.");
        }
        _trustAnchors = trustAnchors;
        _certificateChain = certificateChain;
        _certificateKey = certificateKey;
    }

    /// <summary>Loads the configured trust bundle when the explicit self-hosted mTLS switch is enabled.</summary>
    public static DaprMtlsBootstrap? Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.GetValue<bool>(EnabledConfigurationKey))
        {
            return null;
        }

        ValidateProfile(configuration["Dapr:Mtls:Namespace"] ?? "default",
            configuration["Dapr:Mtls:AppIdSuffix"] ?? string.Empty,
            configuration["Dapr:Mtls:SessionId"] ?? string.Empty, configuration["Dapr:Mtls:SentryHostAddress"]);
        string? configuredDirectory = configuration[CertificateDirectoryConfigurationKey];
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredDirectory, CertificateDirectoryConfigurationKey);
        if (!Path.IsPathFullyQualified(configuredDirectory))
        {
            throw new InvalidOperationException(
                $"{CertificateDirectoryConfigurationKey} must be an absolute path when Dapr mTLS is enabled.");
        }

        string certificateDirectory = Path.GetFullPath(configuredDirectory);
        return new DaprMtlsBootstrap(
            ReadPem(certificateDirectory, "ca.crt", "CERTIFICATE"),
            ReadPem(certificateDirectory, "issuer.crt", "CERTIFICATE"),
            ReadPem(certificateDirectory, "issuer.key", "PRIVATE KEY"), configuration);
    }

    /// <summary>Adds Dapr's mTLS switch to a generated copy of an existing configuration.</summary>
    public static string CreateApplicationConfiguration(string sourceConfigurationPath, string? outputDirectory = null, string appIdSuffix = "", string daprNamespace = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceConfigurationPath);
        ValidateProfile(daprNamespace, appIdSuffix);
        string sourcePath = Path.GetFullPath(sourceConfigurationPath);
        string generatedDirectory = Path.GetFullPath(outputDirectory ?? Path.Combine(AppContext.BaseDirectory, "DaprComponents-mtls"));
        string generatedPath = Path.GetFullPath(Path.Combine(generatedDirectory, Path.GetFileName(sourcePath)));
        if (string.Equals(sourcePath, generatedPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Generated Dapr configuration must not overwrite its authoritative source path.");
        }
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The source Dapr configuration was not found.", sourcePath);
        }

        string[] sourceLines = File.ReadAllLines(sourcePath);
        int[] specLines = sourceLines
            .Select(static (line, index) => (Line: line, Index: index))
            .Where(static item => string.Equals(item.Line.Trim(), "spec:", StringComparison.Ordinal))
            .Select(static item => item.Index)
            .ToArray();
        if (specLines.Length != 1)
        {
            throw new InvalidOperationException(
                $"Dapr configuration '{sourcePath}' must contain exactly one root 'spec:' mapping.");
        }

        if (sourceLines.Any(static line => string.Equals(line.Trim(), "mtls:", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Dapr configuration '{sourcePath}' already declares mTLS and cannot be bootstrapped twice.");
        }

        if (sourceLines.Any(static line => string.Equals(line.Trim(), "features:", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Dapr configuration '{sourcePath}' already declares features and cannot receive the mTLS runtime profile safely.");
        }

        string[] generatedLines =
        [
            .. sourceLines.Take(specLines[0] + 1),
            "  mtls:",
            "    enabled: true",
            "  features:",
            "    - name: HotReload",
            "      enabled: false",
            .. sourceLines.Skip(specLines[0] + 1).Select(line =>
                line.TrimStart().StartsWith("- appId:", StringComparison.Ordinal) && appIdSuffix.Length > 0
                    ? RewriteScalar(line, "appId", value => value + "-" + appIdSuffix)
                    : appIdSuffix.Length > 0 && line.TrimStart().StartsWith("namespace:", StringComparison.Ordinal)
                        ? RewriteScalar(line, "namespace", _ => daprNamespace, quoteValue: true)
                        : appIdSuffix.Length > 0 && line.TrimStart().StartsWith("trustDomain:", StringComparison.Ordinal)
                            ? RewriteScalar(line, "trustDomain", _ => TrustDomain, quoteValue: true)
                            : line),
        ];
        Directory.CreateDirectory(generatedDirectory);
        File.WriteAllText(generatedPath, string.Join(Environment.NewLine, generatedLines) + Environment.NewLine);
        return generatedPath;
    }

    private static void ValidateProfile(string daprNamespace, string appIdSuffix, string sessionId = "", string? sentry = null)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(daprNamespace, "^[a-z0-9-]+$")
            || (appIdSuffix.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(appIdSuffix, "^[a-z0-9-]+$"))
            || (sessionId.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(sessionId, "^[a-z0-9-]{1,40}$")))
        {
            throw new InvalidOperationException("Dapr mTLS session identity, suffix or namespace is invalid.");
        }
        if (daprNamespace != "default" && appIdSuffix.Length == 0)
        {
            throw new InvalidOperationException("A custom Dapr namespace requires an app-ID suffix for generated ACL routing.");
        }
        if (sentry is not null && sentry != "127.0.0.1:50001" && sessionId.Length == 0)
        {
            throw new InvalidOperationException("A custom Sentry port requires a Dapr mTLS session identity.");
        }
    }

    private static string RewriteScalar(string line, string key, Func<string, string> rewrite, bool quoteValue = false)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line,
            "^(?<prefix>\\s*(?:-\\s+)?" + key + ":\\s*)(?<quote>['\"]?)(?<value>[a-zA-Z0-9._-]+)\\k<quote>(?<tail>\\s*(?:#.*)?)$");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Unsupported scalar syntax for Dapr ACL {key}.");
        }
        string quote = quoteValue ? "\"" : match.Groups["quote"].Value;
        return match.Groups["prefix"].Value + quote + rewrite(match.Groups["value"].Value) + quote + match.Groups["tail"].Value;
    }

    /// <summary>Generates this session's configuration while retaining authoritative source bytes.</summary>
    public string CreateConfiguration(string sourcePath)
        => CreateApplicationConfiguration(sourcePath, _configurationDirectory, _appIdSuffix, _namespace);

    /// <summary>Launches session sidecars directly so Dapr CLI's fixed Sentry address cannot cross sessions.</summary>
    public void ConfigureRuntimeLauncher(IDistributedApplicationBuilder builder)
    {
        if (_sentryHostAddress is null) { return; }
        if (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(_daprdPath) || !File.Exists(_daprdPath)
            || !System.Text.RegularExpressions.Regex.IsMatch(_sentryHostAddress, @"^127\.0\.0\.1:[0-9]{4,5}$"))
        {
            throw new InvalidOperationException("The session mTLS launcher requires a Linux daprd and loopback Sentry address.");
        }
        Directory.CreateDirectory(_configurationDirectory);
        string launcher = Path.Combine(_configurationDirectory, "dapr-session");
        // Toolkit supplies sidecar arguments through its normal generated resources and endpoints.
        // CLI 1.18 otherwise hardcodes Sentry to localhost:50001; daprd accepts the owned address.
        string executable = "'" + _daprdPath.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
        File.WriteAllText(launcher, "#!/usr/bin/env bash\nset -euo pipefail\n[[ \"$1\" == run ]] || exit 2\nshift\nexec "
            + executable + " \"$@\" --enable-mtls --sentry-address '" + _sentryHostAddress + "'\n");
        File.SetUnixFileMode(launcher, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _ = builder.AddDapr(options => options.DaprPath = launcher);
    }

    /// <summary>Maps a production app identity only for the explicitly isolated concurrent profile.</summary>
    public string AppId(string appId) => _appIdSuffix.Length == 0 ? appId : appId + "-" + _appIdSuffix;

    /// <summary>Applies the trust bundle to a directly composed sidecar.</summary>
    public void ConfigureSidecar(IResourceBuilder<IDaprSidecarResource> sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ConfigureSidecar(sidecar.Resource);
    }

    /// <summary>Applies the trust bundle to the single sidecar already attached to a project resource.</summary>
    /// <param name="project">The project with its existing sidecar.</param>
    /// <param name="configurationPath">An optional generated configuration; other sidecar options are preserved.</param>
    public void ConfigureProjectSidecar(IResourceBuilder<ProjectResource> project, string? configurationPath = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.Resource.TryGetAnnotationsOfType<DaprSidecarAnnotation>(out IEnumerable<DaprSidecarAnnotation>? annotations))
        {
            throw new InvalidOperationException($"Project resource '{project.Resource.Name}' has no Dapr sidecar.");
        }

        DaprSidecarAnnotation[] materialized = annotations.ToArray();
        if (materialized.Length != 1)
        {
            throw new InvalidOperationException(
                $"Project resource '{project.Resource.Name}' must have exactly one Dapr sidecar, but has {materialized.Length}.");
        }

        ConfigureSidecar(materialized[0].Sidecar, configurationPath);
    }

    private static string ReadPem(string certificateDirectory, string fileName, string expectedPemLabel)
    {
        string path = Path.Combine(certificateDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required Dapr mTLS credential '{fileName}' was not found.", path);
        }

        string value = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(value)
            || !value.Contains($"-----BEGIN {expectedPemLabel}-----", StringComparison.Ordinal)
            || !value.Contains($"-----END {expectedPemLabel}-----", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Dapr mTLS credential '{path}' is not a valid PEM envelope.");
        }

        return value;
    }

    private void ConfigureSidecar(IDaprSidecarResource sidecar, string? configurationPath = null)
    {
        DaprSidecarOptionsAnnotation[] optionsAnnotations = sidecar.Annotations
            .OfType<DaprSidecarOptionsAnnotation>()
            .ToArray();
        if (optionsAnnotations.Length > 1)
        {
            throw new InvalidOperationException(
                $"Dapr sidecar '{sidecar.Name}' must have at most one options annotation, but has {optionsAnnotations.Length}.");
        }

        DaprSidecarOptions options = optionsAnnotations.Length == 0
            ? new DaprSidecarOptions()
            : optionsAnnotations[0].Options;
        if (optionsAnnotations.Length == 1)
        {
            _ = sidecar.Annotations.Remove(optionsAnnotations[0]);
        }

        sidecar.Annotations.Add(new DaprSidecarOptionsAnnotation(options with
        {
            AppId = options.AppId is null ? null : AppId(options.AppId),
            DaprInternalGrpcPort = options.AppId is not null && _internalGrpcPorts.TryGetValue(options.AppId, out int internalPort)
                ? internalPort : options.DaprInternalGrpcPort,
            Config = configurationPath ?? options.Config,
            PlacementHostAddress = _placementHostAddress,
            SchedulerHostAddress = _schedulerHostAddress,
        }));
        sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["DAPR_TRUST_ANCHORS"] = _trustAnchors;
            context.EnvironmentVariables["DAPR_CERT_CHAIN"] = _certificateChain;
            context.EnvironmentVariables["DAPR_CERT_KEY"] = _certificateKey;
            context.EnvironmentVariables["DAPR_CONTROLPLANE_NAMESPACE"] = _namespace;
            context.EnvironmentVariables["DAPR_CONTROLPLANE_TRUST_DOMAIN"] = TrustDomain;
            context.EnvironmentVariables["NAMESPACE"] = _namespace;
        }));
    }
}
