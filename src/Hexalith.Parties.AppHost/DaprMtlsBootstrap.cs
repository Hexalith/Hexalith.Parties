using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Microsoft.Extensions.Configuration;

namespace Hexalith.Parties.AppHost;

/// <summary>Loads one self-hosted Dapr trust bundle and applies it only to Dapr sidecar resources.</summary>
internal sealed class DaprMtlsBootstrap
{
    private const string CertificateDirectoryConfigurationKey = "Dapr:Mtls:CertificateDirectory";
    private const string EnabledConfigurationKey = "Dapr:Mtls:Enabled";
    private const string Namespace = "default";
    private const string PlacementHostAddress = "127.0.0.1:55005";
    private const string SchedulerHostAddress = "127.0.0.1:55006";
    private const string TrustDomain = "public";
    private readonly string _certificateChain;
    private readonly string _certificateKey;
    private readonly string _trustAnchors;

    private DaprMtlsBootstrap(string trustAnchors, string certificateChain, string certificateKey)
    {
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
            ReadPem(certificateDirectory, "issuer.key", "PRIVATE KEY"));
    }

    /// <summary>Adds Dapr's mTLS switch to a generated copy of an existing configuration.</summary>
    public static string CreateApplicationConfiguration(string sourceConfigurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceConfigurationPath);
        string sourcePath = Path.GetFullPath(sourceConfigurationPath);
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

        string generatedDirectory = Path.Combine(AppContext.BaseDirectory, "DaprComponents-mtls");
        Directory.CreateDirectory(generatedDirectory);
        string generatedPath = Path.Combine(generatedDirectory, Path.GetFileName(sourcePath));
        string[] generatedLines =
        [
            .. sourceLines.Take(specLines[0] + 1),
            "  mtls:",
            "    enabled: true",
            "  features:",
            "    - name: HotReload",
            "      enabled: false",
            .. sourceLines.Skip(specLines[0] + 1),
        ];
        File.WriteAllText(generatedPath, string.Join(Environment.NewLine, generatedLines) + Environment.NewLine);
        return generatedPath;
    }

    /// <summary>Applies the trust bundle to a directly composed sidecar.</summary>
    public void ConfigureSidecar(IResourceBuilder<IDaprSidecarResource> sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ConfigureSidecar(sidecar.Resource);
    }

    /// <summary>Applies the trust bundle to the single sidecar already attached to a project resource.</summary>
    public void ConfigureProjectSidecar(IResourceBuilder<ProjectResource> project)
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

        ConfigureSidecar(materialized[0].Sidecar);
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

    private void ConfigureSidecar(IDaprSidecarResource sidecar)
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
            PlacementHostAddress = PlacementHostAddress,
            SchedulerHostAddress = SchedulerHostAddress,
        }));
        sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["DAPR_TRUST_ANCHORS"] = _trustAnchors;
            context.EnvironmentVariables["DAPR_CERT_CHAIN"] = _certificateChain;
            context.EnvironmentVariables["DAPR_CERT_KEY"] = _certificateKey;
            context.EnvironmentVariables["DAPR_CONTROLPLANE_NAMESPACE"] = Namespace;
            context.EnvironmentVariables["DAPR_CONTROLPLANE_TRUST_DOMAIN"] = TrustDomain;
            context.EnvironmentVariables["NAMESPACE"] = Namespace;
        }));
    }
}
