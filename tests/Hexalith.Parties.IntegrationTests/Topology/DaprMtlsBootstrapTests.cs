extern alias apphost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Microsoft.Extensions.Configuration;

using Shouldly;

using DaprMtlsBootstrap = apphost::Hexalith.Parties.AppHost.DaprMtlsBootstrap;

namespace Hexalith.Parties.IntegrationTests.Topology;

[Collection("Non-parallel")]
public sealed class DaprMtlsBootstrapTests
{
    [Fact]
    public void Load_WhenCredentialIsMissing_FailsClosed()
    {
        string certificateDirectory = CreateCertificateDirectory(includePrivateKey: false);
        try
        {
            IConfiguration configuration = CreateConfiguration(certificateDirectory);

            FileNotFoundException exception = Should.Throw<FileNotFoundException>(() => DaprMtlsBootstrap.Load(configuration));

            exception.FileName.ShouldBe(Path.Combine(certificateDirectory, "issuer.key"));
        }
        finally
        {
            Directory.Delete(certificateDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateApplicationConfiguration_EnablesMtlsAndPreservesExactAcl()
    {
        string sourceDirectory = Path.Combine(Path.GetTempPath(), "parties-dapr-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDirectory);
        string fileName = "accesscontrol-" + Guid.NewGuid().ToString("N") + ".yaml";
        string sourcePath = Path.Combine(sourceDirectory, fileName);
        File.WriteAllText(
            sourcePath,
            "apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: test\nspec:\n  accessControl:\n    defaultAction: deny\n    operations:\n      - name: /project/rebuild/shared/v1\n");
        string generatedPath = string.Empty;
        try
        {
            generatedPath = DaprMtlsBootstrap.CreateApplicationConfiguration(sourcePath);
            string generated = File.ReadAllText(generatedPath);

            generated.ShouldContain("spec:" + Environment.NewLine + "  mtls:" + Environment.NewLine + "    enabled: true");
            generated.ShouldContain("  features:" + Environment.NewLine + "    - name: HotReload" + Environment.NewLine + "      enabled: false");
            generated.ShouldContain("defaultAction: deny");
            generated.ShouldContain("- name: /project/rebuild/shared/v1");
            generated.Split("- name: /project/rebuild/shared/v1", StringSplitOptions.None).Length.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(sourceDirectory, recursive: true);
            if (generatedPath.Length > 0 && File.Exists(generatedPath))
            {
                File.Delete(generatedPath);
            }
        }
    }

    [Fact]
    public async Task ConfigureSidecar_InjectsTrustBundleOnlyIntoDaprResourceAsync()
    {
        string certificateDirectory = CreateCertificateDirectory(includePrivateKey: true);
        try
        {
            DaprMtlsBootstrap bootstrap = DaprMtlsBootstrap.Load(CreateConfiguration(certificateDirectory))!;
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
            IResourceBuilder<ContainerResource> application = builder.AddContainer("application", "example/image");
            _ = application.WithDaprSidecar(bootstrap.ConfigureSidecar);
            DaprSidecarAnnotation sidecarAnnotation = application.Resource.Annotations
                .OfType<DaprSidecarAnnotation>()
                .Single();
            DaprSidecarOptions sidecarOptions = sidecarAnnotation.Sidecar.Annotations
                .OfType<DaprSidecarOptionsAnnotation>()
                .Single()
                .Options;

            IDictionary<string, object> sidecarEnvironment = await ResolveEnvironmentAsync(sidecarAnnotation.Sidecar);
            IDictionary<string, object> applicationEnvironment = await ResolveEnvironmentAsync(application.Resource);

            sidecarOptions.PlacementHostAddress.ShouldBe("127.0.0.1:55005");
            sidecarOptions.SchedulerHostAddress.ShouldBe("127.0.0.1:55006");
            sidecarEnvironment["DAPR_TRUST_ANCHORS"].ShouldBe(CertificatePem);
            sidecarEnvironment["DAPR_CERT_CHAIN"].ShouldBe(CertificatePem);
            sidecarEnvironment["DAPR_CERT_KEY"].ShouldBe(PrivateKeyPem);
            sidecarEnvironment["DAPR_CONTROLPLANE_NAMESPACE"].ShouldBe("default");
            sidecarEnvironment["DAPR_CONTROLPLANE_TRUST_DOMAIN"].ShouldBe("public");
            sidecarEnvironment["NAMESPACE"].ShouldBe("default");
            applicationEnvironment.Keys.ShouldNotContain(static key => key.StartsWith("DAPR_CERT_", StringComparison.Ordinal));
            applicationEnvironment.Keys.ShouldNotContain("DAPR_TRUST_ANCHORS");
        }
        finally
        {
            Directory.Delete(certificateDirectory, recursive: true);
        }
    }

    private const string CertificatePem = "-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----\n";
    private const string PrivateKeyPem = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----\n";

    private static string CreateCertificateDirectory(bool includePrivateKey)
    {
        string directory = Path.Combine(Path.GetTempPath(), "parties-dapr-certs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "ca.crt"), CertificatePem);
        File.WriteAllText(Path.Combine(directory, "issuer.crt"), CertificatePem);
        if (includePrivateKey)
        {
            File.WriteAllText(Path.Combine(directory, "issuer.key"), PrivateKeyPem);
        }

        return directory;
    }

    private static IConfiguration CreateConfiguration(string certificateDirectory)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dapr:Mtls:Enabled"] = "true",
                ["Dapr:Mtls:CertificateDirectory"] = certificateDirectory,
            })
            .Build();

    private static async Task<IDictionary<string, object>> ResolveEnvironmentAsync(IResource resource)
    {
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource,
            new Dictionary<string, object>(),
            CancellationToken.None);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }

        return context.EnvironmentVariables;
    }
}
