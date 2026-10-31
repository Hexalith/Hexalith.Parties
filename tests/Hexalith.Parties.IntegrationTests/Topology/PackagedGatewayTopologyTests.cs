extern alias apphost;

using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks that package compatibility changes only the gateway project selected before model construction.</summary>
[Collection("Non-parallel")]
public sealed class PackagedGatewayTopologyTests
{
    /// <summary>The original project metadata, endpoints and complete shared wiring survive either gateway choice.</summary>
    /// <param name="isolatedMtls">Whether both models use the same isolated mTLS identity and trust bundle.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitPackageProfilePreservesNativeEndpointsAndSharedWiring(bool isolatedMtls)
    {
        string directory = Path.Combine(Path.GetTempPath(), "parties-gateway-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            if (isolatedMtls)
            {
                using RSA key = RSA.Create(2048);
                var request = new CertificateRequest("CN=package-gateway-model", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
                File.WriteAllText(Path.Combine(directory, "ca.crt"), certificate.ExportCertificatePem());
                File.WriteAllText(Path.Combine(directory, "issuer.crt"), certificate.ExportCertificatePem());
                File.WriteAllText(Path.Combine(directory, "issuer.key"), key.ExportPkcs8PrivateKeyPem());
            }
            IDistributedApplicationTestingBuilder native = await CreateAsync(false, isolatedMtls ? directory : null).ConfigureAwait(true);
            await using var nativeLifetime = native.ConfigureAwait(true);
            IDistributedApplicationTestingBuilder packaged = await CreateAsync(true, isolatedMtls ? directory : null).ConfigureAwait(true);
            await using var packageLifetime = packaged.ConfigureAwait(true);
            ProjectResource nativeGateway = native.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
            ProjectResource packageGateway = packaged.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
            nativeGateway.GetProjectMetadata().ProjectPath.Replace('\\', '/')
                .ShouldEndWith("/src/Hexalith.EventStore/Hexalith.EventStore.csproj");
            packageGateway.GetProjectMetadata().ProjectPath.Replace('\\', '/')
                .ShouldEndWith("/tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj");
            packageGateway.GetProjectMetadata().ShouldBeSameAs(packageGateway.Annotations.OfType<IProjectMetadata>().ShouldHaveSingleItem());
            packageGateway.Annotations.OfType<EndpointAnnotation>().Select(endpoint => (endpoint.Name, endpoint.UriScheme, endpoint.TargetPort))
                .ShouldBe(nativeGateway.Annotations.OfType<EndpointAnnotation>().Select(endpoint => (endpoint.Name, endpoint.UriScheme, endpoint.TargetPort)));
            packageGateway.Annotations.OfType<EndpointAnnotation>().Select(endpoint => endpoint.Name).ShouldContain("http");
            // Path-based AddProject metadata does not populate IProjectMetadata.LaunchSettings.
            // Compare the actual profile files used by project defaults, including the HTTPS profile.
            using JsonDocument nativeProfiles = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(nativeGateway.GetProjectMetadata().ProjectPath)!, "Properties", "launchSettings.json")));
            using JsonDocument packageProfiles = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(packageGateway.GetProjectMetadata().ProjectPath)!, "Properties", "launchSettings.json")));
            foreach (string profile in new[] { "http", "https" })
            {
                packageProfiles.RootElement.GetProperty("profiles").GetProperty(profile).GetProperty("applicationUrl").GetString()
                    .ShouldBe(nativeProfiles.RootElement.GetProperty("profiles").GetProperty(profile).GetProperty("applicationUrl").GetString());
            }
            packaged.Resources.Select(resource => resource.Name).Order(StringComparer.Ordinal)
                .ShouldBe(native.Resources.Select(resource => resource.Name).Order(StringComparer.Ordinal));

            foreach (ProjectResource original in native.Resources.OfType<ProjectResource>())
            {
                ProjectResource selected = packaged.Resources.OfType<ProjectResource>().Single(resource => resource.Name == original.Name);
                var expected = await EnvironmentAsync(native, original).ConfigureAwait(true);
                var actual = await EnvironmentAsync(packaged, selected).ConfigureAwait(true);
                AssertEnvironmentMatches(expected, actual, original.Name);
                selected.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name)
                    .ShouldBe(original.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name));
                selected.Annotations.OfType<DaprSidecarAnnotation>().Select(sidecar => sidecar.Sidecar.Name)
                    .ShouldBe(original.Annotations.OfType<DaprSidecarAnnotation>().Select(sidecar => sidecar.Sidecar.Name));
                var nativeOptions = original.Annotations.OfType<DaprSidecarAnnotation>()
                    .SelectMany(sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>()).Select(annotation => annotation.Options);
                var selectedOptions = selected.Annotations.OfType<DaprSidecarAnnotation>()
                    .SelectMany(sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>()).Select(annotation => annotation.Options);
                selectedOptions.Select(options => (options.AppId, options.Config)).ShouldBe(nativeOptions.Select(options => (options.AppId, options.Config)));
                foreach (DaprSidecarAnnotation originalSidecar in original.Annotations.OfType<DaprSidecarAnnotation>())
                {
                    IDaprSidecarResource selectedSidecar = selected.Annotations.OfType<DaprSidecarAnnotation>()
                        .Single(sidecar => sidecar.Sidecar.Name == originalSidecar.Sidecar.Name).Sidecar;
                    var expectedSidecarEnvironment = await EnvironmentAsync(native, originalSidecar.Sidecar).ConfigureAwait(true);
                    var actualSidecarEnvironment = await EnvironmentAsync(packaged, selectedSidecar).ConfigureAwait(true);
                    AssertEnvironmentMatches(expectedSidecarEnvironment, actualSidecarEnvironment, selectedSidecar.Name);
                    selectedSidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Select(reference => reference.Component.Name)
                        .ShouldBe(originalSidecar.Sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Select(reference => reference.Component.Name));
                    var options = selectedSidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                    if (isolatedMtls)
                    {
                        options.AppId.ShouldEndWith("-model");
                        File.ReadAllText(options.Config!).ShouldContain("enabled: true");
                        actualSidecarEnvironment["NAMESPACE"].ShouldBe("parties-model");
                        actualSidecarEnvironment["DAPR_TRUST_ANCHORS"].ShouldBe(File.ReadAllText(Path.Combine(directory, "ca.crt")));
                        actualSidecarEnvironment["DAPR_CERT_KEY"].ShouldBe(File.ReadAllText(Path.Combine(directory, "issuer.key")));
                        options.PlacementHostAddress.ShouldBe("127.0.0.1:55005");
                        options.SchedulerHostAddress.ShouldBe("127.0.0.1:55006");
                    }
                }
            }
            (await EnvironmentAsync(packaged, packageGateway).ConfigureAwait(true))["EventStore__DomainServices__Registrations__wildcard_party_v1__AppId"].ShouldBe(isolatedMtls ? "parties-model" : "parties");
            (await EnvironmentAsync(packaged, packageGateway).ConfigureAwait(true))["EventStore__SignalR__Enabled"].ShouldBe("true");
            if (isolatedMtls)
            {
                var gatewayEnvironment = await EnvironmentAsync(packaged, packageGateway).ConfigureAwait(true);
                gatewayEnvironment["Authentication__WorkloadIssuer__Workload"].ShouldBe("eventstore-model");
                gatewayEnvironment["EventStore__DomainServices__Registrations__system|tenants|v1__AppId"].ShouldBe("tenants-model");
                ProjectResource parties = packaged.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "parties");
                (await EnvironmentAsync(packaged, parties).ConfigureAwait(true))["Authentication__Workload__AllowedCallers__0"].ShouldBe("eventstore-model");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The session selects package compatibility before executing the AppHost only for compiled package consumers.</summary>
    [Fact]
    public void SessionSelectsTheGatewayMatchingItsCompiledDependencyMode()
    {
        string[] arguments = PartiesTopologySession.Create().Arguments(complete: false, mtls: false);
#if HEXALITH_DEVELOPMENT_CATALOG_SOURCE
        arguments.ShouldNotContain(argument => argument.StartsWith("--Parties:Testing:UsePackagedGateway", StringComparison.Ordinal));
#else
        arguments.ShouldContain("--Parties:Testing:UsePackagedGateway=true");
#endif
    }

    /// <summary>Every publication model keeps the native gateway and excludes the owned test host.</summary>
    [Fact]
    public async Task PublishKeepsNativeProjectAndRejectsThePackageTestFlag()
    {
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            var refused = await DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                ["--operation=publish", "--PartiesUi:OidcClientSecret=model-only-secret", "--Parties:Testing:UsePackagedGateway=true"],
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using var refusedLifetime = refused.ConfigureAwait(true);
        }).ConfigureAwait(true);
        error.Message.ShouldContain("Parties:Testing:UsePackagedGateway");
        error.Message.ShouldContain("cannot be published");
        var published = await DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
            ["--operation=publish", "--PUBLISH_TARGET=", "--PartiesUi:OidcClientSecret=model-only-secret"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var publishedLifetime = published.ConfigureAwait(true);
        published.Resources.OfType<ProjectResource>().ShouldAllBe(resource => !resource.GetProjectMetadata().ProjectPath.Contains("EventStoreGateway.TestHost", StringComparison.Ordinal));
        published.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore")
            .GetProjectMetadata().ProjectPath.Replace('\\', '/').ShouldEndWith("/src/Hexalith.EventStore/Hexalith.EventStore.csproj");
    }

    private static Task<IDistributedApplicationTestingBuilder> CreateAsync(bool packaged, string? certificateDirectory = null) =>
        DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
            ["--operation=run", "--EnableKeycloak=false", "--EnableEventStoreSampleUi=true", "--EnableMemoriesSearch=true",
                $"--Parties:Testing:UsePackagedGateway={packaged}",
                .. certificateDirectory is null ? Array.Empty<string>() : new[]
                {
                    "--Dapr:Mtls:Enabled=true", "--Dapr:Mtls:Namespace=parties-model", "--Dapr:Mtls:AppIdSuffix=model",
                    $"--Dapr:Mtls:CertificateDirectory={certificateDirectory}",
                    $"--Dapr:Mtls:ConfigurationDirectory={Path.Combine(certificateDirectory, "configuration")}",
                }], TestContext.Current.CancellationToken);

    private static void AssertEnvironmentMatches(IDictionary<string, object> expected, IDictionary<string, object> actual, string resource)
    {
        actual.Keys.Order(StringComparer.Ordinal).ShouldBe(expected.Keys.Order(StringComparer.Ordinal), resource);
        foreach ((string key, object value) in expected)
        {
            switch (value)
            {
                case string:
                    actual[key].ShouldBe(value, $"{resource}: {key}");
                    break;
                case IManifestExpressionProvider expression:
                    actual[key].ShouldBeAssignableTo<IManifestExpressionProvider>().ValueExpression.ShouldBe(expression.ValueExpression);
                    break;
                case null:
                    actual[key].ShouldBeNull();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported environment value {resource}:{key}: {value.GetType().FullName}.");
            }
        }
    }

    private static async Task<IDictionary<string, object>> EnvironmentAsync(IDistributedApplicationTestingBuilder builder, IResource resource)
    {
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, resource,
            new Dictionary<string, object>(), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }
        return context.EnvironmentVariables;
    }
}
