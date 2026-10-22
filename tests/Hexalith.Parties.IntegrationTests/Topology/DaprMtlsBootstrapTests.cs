extern alias apphost;

using System.Diagnostics;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Parties.TestInfrastructure;

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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualAppHostModel_ConfiguresEverySidecarAndIsolatesApplicationCredentials(bool mtls, bool complete)
    {
        string directory = CreateCertificateDirectory(includePrivateKey: true);
        try
        {
            string[] arguments =
            [
                "--EnableKeycloak=false",
                $"--EnableMemoriesSearch={complete}",
                $"--EnableEventStoreSampleUi={complete}",
                $"--Dapr:Mtls:Enabled={mtls}",
                $"--Dapr:Mtls:CertificateDirectory={directory}",
            ];
            IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(arguments, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using var lifetime = builder.ConfigureAwait(true);
            string[] expected = complete
                ? ["eventstore", "eventstore-admin", "eventstore-admin-ui", "memories", "parties", "sample", "sample-blazor-ui", "tenants"]
                : ["eventstore", "eventstore-admin", "eventstore-admin-ui", "parties", "tenants"];
            var sidecars = builder.Resources.OfType<ProjectResource>()
                .SelectMany(project => project.Annotations.OfType<DaprSidecarAnnotation>())
                .ToArray();
            sidecars.Select(sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId)
                .Order(StringComparer.Ordinal).ShouldBe(expected);
            foreach (DaprSidecarAnnotation sidecar in sidecars)
            {
                DaprSidecarOptions options = sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                IDictionary<string, object> environment = await ResolveEnvironmentAsync(sidecar.Sidecar);
                if (mtls)
                {
                    options.Config.ShouldNotBeNullOrWhiteSpace();
                    string rendered = File.ReadAllText(options.Config);
                    rendered.ShouldContain("  mtls:" + Environment.NewLine + "    enabled: true");
                    environment["DAPR_TRUST_ANCHORS"].ShouldBe(CertificatePem);
                    environment["DAPR_CERT_CHAIN"].ShouldBe(CertificatePem);
                    environment["DAPR_CERT_KEY"].ShouldBe(PrivateKeyPem);
                    environment["NAMESPACE"].ShouldBe("default");
                    options.PlacementHostAddress.ShouldBe("127.0.0.1:55005");
                    options.SchedulerHostAddress.ShouldBe("127.0.0.1:55006");
                    if (options.AppId is not ("eventstore-admin-ui" or "sample-blazor-ui"))
                    {
                        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DaprComponents", Path.GetFileName(options.Config)));
                        rendered.Replace("  mtls:" + Environment.NewLine + "    enabled: true" + Environment.NewLine
                            + "  features:" + Environment.NewLine + "    - name: HotReload" + Environment.NewLine
                            + "      enabled: false" + Environment.NewLine, string.Empty).ShouldBe(source.Replace("\r\n", Environment.NewLine));
                    }
                }
                else
                {
                    environment.Keys.ShouldNotContain("DAPR_TRUST_ANCHORS");
                    environment.Keys.ShouldNotContain("DAPR_CERT_CHAIN");
                    environment.Keys.ShouldNotContain("DAPR_CERT_KEY");
                    if (options.AppId == "eventstore-admin-ui")
                    {
                        options.Config.ShouldBeNull();
                    }
                }
            }

            foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
            {
                IDictionary<string, object> environment = await ResolveEnvironmentAsync(project);
                environment.Keys.ShouldNotContain("DAPR_TRUST_ANCHORS");
                environment.Keys.ShouldNotContain("DAPR_CERT_CHAIN");
                environment.Keys.ShouldNotContain("DAPR_CERT_KEY");
            }

            ProjectResource adminUi = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore-admin-ui");
            adminUi.Annotations.OfType<ExplicitStartupAnnotation>().ShouldNotBeEmpty();
            adminUi.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name).ShouldContain("eventstore-admin");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentAppHostModel_MapsSidecarsAclRoutingAndWorkloadAuthorityTogether()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The session mTLS launcher requires Linux bash."); return; }
        PartiesTopologySession session = PartiesTopologySession.Create(isolatedIdentities: true);
        using var commands = new MtlsBootstrapCommandDouble();
        var settings = new Dictionary<string, string?>();
        session.ConfigureBootstrapEnvironment(settings);
        (int exit, string error) = await commands.RunAsync("", session.CertificateDirectory, TestContext.Current.CancellationToken, settings);
        exit.ShouldBe(0, error);
        IReadOnlyDictionary<string, string?> exported = commands.LaunchedConfiguration;
        exported["Dapr:Mtls:Namespace"].ShouldBe(session.Namespace);
        exported["Dapr:Mtls:AppIdSuffix"].ShouldBe(session.AppIdSuffix);
        exported["Dapr:Mtls:SessionId"].ShouldBe(session.Id);
        exported["Dapr:Mtls:CertificateDirectory"].ShouldBe(session.CertificateDirectory);
        exported["Dapr:Mtls:SentryHostAddress"].ShouldBe($"127.0.0.1:{session.SentryPort}");
        exported["Dapr:Mtls:PlacementHostAddress"].ShouldBe($"127.0.0.1:{session.PlacementPort}");
        exported["Dapr:Mtls:SchedulerHostAddress"].ShouldBe($"127.0.0.1:{session.SchedulerPort}");
        exported["Dapr:Mtls:ConfigurationDirectory"].ShouldBe(Path.Combine(session.CertificateDirectory, "configuration"));
        string[] arguments = session.Arguments(true, true)
            .Where(argument => !exported.Keys.Any(key => argument.StartsWith("--" + key + "=", StringComparison.Ordinal)))
            .Concat(exported.Select(pair => "--" + pair.Key + "=" + pair.Value)).ToArray();
        File.WriteAllText(Path.Combine(session.CertificateDirectory, "ca.crt"), CertificatePem);
        File.WriteAllText(Path.Combine(session.CertificateDirectory, "issuer.crt"), CertificatePem);
        File.WriteAllText(Path.Combine(session.CertificateDirectory, "issuer.key"), PrivateKeyPem);
        try
        {
            IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(arguments, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using var lifetime = builder.ConfigureAwait(true);
            foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
            {
                foreach (DaprSidecarAnnotation sidecar in project.Annotations.OfType<DaprSidecarAnnotation>())
                {
                    DaprSidecarOptions options = sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                    options.AppId.ShouldBe(session.AppId(project.Name));
                    options.DaprInternalGrpcPort.ShouldBe(session.InternalGrpcPorts[project.Name]);
                    options.PlacementHostAddress.ShouldBe($"127.0.0.1:{session.PlacementPort}");
                    options.SchedulerHostAddress.ShouldBe($"127.0.0.1:{session.SchedulerPort}");
                    Path.GetDirectoryName(options.Config).ShouldBe(exported["Dapr:Mtls:ConfigurationDirectory"]);
                    IDictionary<string, object> sidecarEnvironment = await ResolveEnvironmentAsync(sidecar.Sidecar).ConfigureAwait(true);
                    sidecarEnvironment["NAMESPACE"].ShouldBe(exported["Dapr:Mtls:Namespace"]);
                    string configuration = File.ReadAllText(options.Config!);
                    if (configuration.Contains("- appId:", StringComparison.Ordinal))
                    {
                        configuration.ShouldContain("-" + session.Id);
                        configuration.ShouldContain("namespace: \"" + session.Namespace + "\"");
                        configuration.ShouldNotContain("trustDomain: hexalith.io");
                    }
                }
            }
            using ServiceProvider services = builder.Services.BuildServiceProvider();
            string launcher = services.GetRequiredService<IOptions<DaprOptions>>().Value.DaprPath!;
            Path.GetDirectoryName(launcher).ShouldBe(exported["Dapr:Mtls:ConfigurationDirectory"]);
            File.ReadAllText(launcher).ShouldContain("--sentry-address '" + exported["Dapr:Mtls:SentryHostAddress"] + "'");
            ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore");
            IDictionary<string, object> gatewayEnvironment = await ResolveEnvironmentAsync(gateway).ConfigureAwait(true);
            gatewayEnvironment["Authentication__WorkloadIssuer__Workload"].ShouldBe(session.AppId("eventstore"));
            gatewayEnvironment["EventStore__DomainServices__Registrations__wildcard_party_v1__AppId"].ShouldBe(session.AppId("parties"));
            gatewayEnvironment["EventStore__DomainServices__Registrations__*|counter|v1__AppId"].ShouldBe(session.AppId("sample"));
            ProjectResource parties = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "parties");
            IDictionary<string, object> environment = await ResolveEnvironmentAsync(parties).ConfigureAwait(true);
            environment["Authentication__Workload__AllowedCallers__0"].ShouldBe(session.AppId("eventstore"));
            environment["Tenants__ServiceName"].ShouldBe(session.AppId("tenants"));
            environment["Tenants__CommandApiAppId"].ShouldBe(session.AppId("eventstore"));
        }
        finally { Directory.Delete(session.Root, recursive: true); }
    }

    [Fact]
    public void IsolatedConfiguration_RemapsOnlyGeneratedAclIdentitiesAndNamespace()
    {
        string directory = CreateCertificateDirectory(includePrivateKey: true);
        string source = Path.Combine(AppContext.BaseDirectory, "DaprComponents", "accesscontrol.parties.yaml");
        string original = File.ReadAllText(source);
        try
        {
            string generated = DaprMtlsBootstrap.CreateApplicationConfiguration(source, directory, "session123", "parties-session123");
            string rendered = File.ReadAllText(generated);
            rendered.ShouldContain("- appId: eventstore-session123");
            rendered.ShouldContain("namespace: \"parties-session123\"");
            rendered.ShouldContain("- name: /project/rebuild/shared/v1");
            File.ReadAllText(source).ShouldBe(original);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SessionLauncher_PreservesArgumentsAndUsesOwnedSentryWithoutCliDefault()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The session mTLS launcher requires Linux bash."); return; }
        string directory = CreateCertificateDirectory(includePrivateKey: true);
        try
        {
            string capture = Path.Combine(directory, "arguments");
            string runtime = Path.Combine(directory, "daprd");
            File.WriteAllText(runtime, "#!/usr/bin/env bash\nprintf '%s\\n' \"$@\" > '" + capture + "'\n");
            File.SetUnixFileMode(runtime, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dapr:Mtls:Enabled"] = "true", ["Dapr:Mtls:CertificateDirectory"] = directory,
                ["Dapr:Mtls:ConfigurationDirectory"] = directory, ["Dapr:Mtls:SentryHostAddress"] = "127.0.0.1:42001",
                ["Dapr:Mtls:DaprdPath"] = runtime, ["Dapr:Mtls:SessionId"] = "session123",
            }).Build();
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
            DaprMtlsBootstrap.Load(configuration)!.ConfigureRuntimeLauncher(builder);
            using ServiceProvider services = builder.Services.BuildServiceProvider();
            string launcher = services.GetRequiredService<IOptions<DaprOptions>>().Value.DaprPath!;
            var start = new ProcessStartInfo(launcher) { UseShellExecute = false };
            foreach (string argument in new[] { "run", "--app-id", "parties-session123", "--config", "configuration with spaces.yaml" })
            {
                start.ArgumentList.Add(argument);
            }
            using Process process = Process.Start(start)!;
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            process.ExitCode.ShouldBe(0);
            File.ReadAllLines(capture).ShouldBe(new[] { "--app-id", "parties-session123", "--config", "configuration with spaces.yaml", "--enable-mtls", "--sentry-address", "127.0.0.1:42001" });
            File.ReadAllText(launcher).ShouldNotContain(CertificatePem);
            File.ReadAllText(launcher).ShouldNotContain(PrivateKeyPem);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("Dapr:Mtls:SentryHostAddress", "127.0.0.1:42001", "session identity")]
    [InlineData("Dapr:Mtls:Namespace", "parties-custom", "app-ID suffix")]
    public void UnsupportedProfileFailsBeforeReadingCredentialsOrWritingConfiguration(string key, string value, string diagnostic)
    {
        string nonexistent = Path.Combine(Path.GetTempPath(), "parties-no-side-effects-" + Guid.NewGuid().ToString("N"));
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dapr:Mtls:Enabled"] = "true", ["Dapr:Mtls:CertificateDirectory"] = nonexistent,
            ["Dapr:Mtls:ConfigurationDirectory"] = nonexistent, [key] = value,
        }).Build();
        Should.Throw<InvalidOperationException>(() => DaprMtlsBootstrap.Load(configuration)).Message.ShouldContain(diagnostic);
        Directory.Exists(nonexistent).ShouldBeFalse();
    }

    [Fact]
    public void GeneratedConfigurationRejectsCanonicalSourceOutputCollisionAndPreservesBytes()
    {
        string directory = CreateCertificateDirectory(includePrivateKey: true);
        try
        {
            string source = Path.Combine(directory, "source.yaml");
            byte[] original = System.Text.Encoding.UTF8.GetBytes("spec:\n  accessControl:\n    defaultAction: deny\n");
            File.WriteAllBytes(source, original);
            Should.Throw<InvalidOperationException>(() => DaprMtlsBootstrap.CreateApplicationConfiguration(source,
                Path.Combine(directory, "child", ".."))).Message.ShouldContain("authoritative source");
            File.ReadAllBytes(source).ShouldBe(original);
            Directory.Exists(Path.Combine(directory, "child")).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("eventstore # caller", "eventstore-session123 # caller")]
    [InlineData("\"eventstore\" # caller", "\"eventstore-session123\" # caller")]
    [InlineData("'eventstore' # caller", "'eventstore-session123' # caller")]
    public void GeneratedAclSuffixPreservesScalarQuotesAndTrailingComments(string scalar, string expected)
    {
        string directory = CreateCertificateDirectory(includePrivateKey: true);
        try
        {
            string source = Path.Combine(directory, "source.yaml");
            string original = "spec:\n  accessControl:\n    policies:\n      - appId: " + scalar + "\n";
            File.WriteAllText(source, original);
            string generated = DaprMtlsBootstrap.CreateApplicationConfiguration(source, Path.Combine(directory, "generated"), "session123");
            File.ReadAllText(generated).ShouldContain("      - appId: " + expected);
            File.ReadAllText(source).ShouldBe(original);
        }
        finally { Directory.Delete(directory, recursive: true); }
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
