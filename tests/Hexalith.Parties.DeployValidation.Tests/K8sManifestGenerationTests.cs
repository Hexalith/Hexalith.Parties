namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class K8sManifestGenerationTests
{
    private static readonly string[] s_expectedGeneratedFolders =
    [
        "eventstore",
        "eventstore-admin",
        "eventstore-admin-ui",
        "memories",
        "parties",
        "parties-mcp",
        "parties-ui",
        "sample",
        "sample-blazor-ui",
        "tenants",
    ];

    [Fact]
    public void GeneratedServiceFolderSetMatchesStoryNineTwoContract()
    {
        string[] generatedFolders = Directory.EnumerateDirectories(DeploymentTestPaths.K8sDirectory)
            .Select(Path.GetFileName)
            .Where(static name => name is not "redis" and not "falkordb" and not "_lib")
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        generatedFolders.ShouldBe(s_expectedGeneratedFolders, "The publish topology owns the generated application folders; carve-outs are tested separately.");
    }

    [Fact]
    public void GeneratedDeploymentsKeepNonImageLinesStableAcrossFixtureSamples()
    {
        string before = File.ReadAllText(Path.Combine(DeploymentTestPaths.FixturesDirectory, "byte-determinism", "before.yaml"));
        string after = File.ReadAllText(Path.Combine(DeploymentTestPaths.FixturesDirectory, "byte-determinism", "after.yaml"));

        NormalizeNonImageLines(after).ShouldBe(
            NormalizeNonImageLines(before),
            "A publish may change image tags, but non-image deployment lines must remain byte-stable.");
    }

    [Fact]
    public void GeneratedDeploymentsUseRegistryImagesWithSemVerShapedTags()
    {
        Regex imagePattern = new(@"^\s*image:\s*registry\.hexalith\.com/(?<name>[a-z0-9-]+):(?<tag>[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?)\s*$", RegexOptions.Multiline);

        foreach (string folder in s_expectedGeneratedFolders)
        {
            string deploymentPath = Path.Combine(DeploymentTestPaths.K8sDirectory, folder, "deployment.yaml");
            string deployment = File.ReadAllText(deploymentPath);
            Match match = imagePattern.Match(deployment);

            match.Success.ShouldBeTrue($"{deploymentPath} must use a MinVer-shaped registry image tag.");
            match.Groups["name"].Value.ShouldBe(folder, $"{deploymentPath} image repository must match the service folder.");
            match.Groups["tag"].Value.Contains("+dirty", StringComparison.Ordinal).ShouldBeFalse($"{deploymentPath} must not commit a dirty MinVer tag.");
        }
    }

    [Fact]
    public void PublicIngressRoutesOnlyBrowserUiServicesWithNginxTls()
    {
        string ingress = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "ingress.yaml"));

        ingress.ShouldContain("name: hexalith-pages-ingress");
        ingress.ShouldContain("ingressClassName: nginx-public");
        ingress.ShouldContain("secretName: hexalith-pages-letsencrypt-tls");
        ingress.ShouldContain("host: eventstore.hexalith.com");
        ingress.ShouldContain("name: eventstore-admin-ui");
        ingress.ShouldContain("host: sample.hexalith.com");
        ingress.ShouldContain("name: sample-blazor-ui");
        ingress.ShouldContain("host: parties.hexalith.com");
        ingress.ShouldContain("name: parties-ui");
        ingress.ShouldNotContain("name: eventstore\n");
        ingress.ShouldNotContain("name: eventstore-admin\n");
        ingress.ShouldNotContain("name: parties\n");
        ingress.ShouldNotContain("name: tenants\n");
        ingress.ShouldNotContain("name: sample\n");
    }

    [Fact]
    public void SampleWorkloadsUseFullAndClientOnlyDaprAnnotations()
    {
        string sample = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "sample", "deployment.yaml"));
        string sampleUi = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "sample-blazor-ui", "deployment.yaml"));

        sample.ShouldContain("dapr.io/app-id: sample");
        sample.ShouldContain("dapr.io/app-port: '8080'");
        sample.ShouldContain("dapr.io/config: accesscontrol-sample");

        sampleUi.ShouldContain("dapr.io/app-id: sample-blazor-ui");
        sampleUi.ShouldNotContain("dapr.io/app-port");
        sampleUi.ShouldNotContain("dapr.io/config");
    }

    [Fact]
    public void GeneratedWorkloadsUsePublicHttpsKeycloakIssuerWithoutHostAliases()
    {
        string[] jwtBearerWorkloads = ["eventstore", "eventstore-admin", "parties", "parties-mcp", "tenants"];
        string[] eventStoreClientWorkloads = ["eventstore-admin-ui", "sample-blazor-ui"];
        string legacyTacheIssuer = "http://auth." + "tache.ai:8080/realms/tache";
        string legacyClusterIp = "10.233." + "41.235";

        foreach (string folder in s_expectedGeneratedFolders)
        {
            string deployment = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, folder, "deployment.yaml"));
            string kustomization = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, folder, "kustomization.yaml"));

            deployment.ShouldNotContain("hostAliases:");
            deployment.ShouldNotContain("auth.tache.ai");
            deployment.ShouldNotContain(legacyClusterIp);
            kustomization.ShouldNotContain(legacyTacheIssuer);
            kustomization.ShouldNotContain("Authentication__JwtBearer__RequireHttpsMetadata=false");
        }

        foreach (string folder in jwtBearerWorkloads)
        {
            string kustomization = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, folder, "kustomization.yaml"));

            kustomization.ShouldContain("Authentication__JwtBearer__Authority=https://auth.tache.ai/realms/tache");
            kustomization.ShouldContain("Authentication__JwtBearer__Issuer=https://auth.tache.ai/realms/tache");
            kustomization.ShouldContain("Authentication__JwtBearer__RequireHttpsMetadata=true");
        }

        foreach (string folder in eventStoreClientWorkloads)
        {
            string deployment = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, folder, "deployment.yaml"));
            string kustomization = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, folder, "kustomization.yaml"));

            deployment.ShouldContain("EventStore__Authentication__Username");
            deployment.ShouldContain("EventStore__Authentication__Password");
            deployment.ShouldContain("EventStore__Authentication__ClientSecret");
            deployment.ShouldContain("name: hexalith-eventstore-ui-oidc-client");
            deployment.ShouldContain("key: client-secret");
            deployment.ShouldContain("optional: true");
            kustomization.ShouldContain("EventStore__Authentication__Authority=https://auth.tache.ai/realms/tache");
            kustomization.ShouldContain("EventStore__Authentication__Issuer=https://auth.tache.ai/realms/tache");
            kustomization.ShouldContain("EventStore__Authentication__ClientCredentialsClientId=hexalith-eventstore-ui");
        }

        string adminUiKustomization = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "eventstore-admin-ui", "kustomization.yaml"));
        adminUiKustomization.ShouldContain("EventStore__SignalR__HubUrl=http://eventstore:8080/hubs/projection-changes");

        string partiesUiKustomization = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "parties-ui", "kustomization.yaml"));
        partiesUiKustomization.ShouldContain("Authentication__OpenIdConnect__Authority=https://auth.tache.ai/realms/tache");
    }

    [Fact]
    public void PartiesUiIsBrowserOnlyNonDaprWorkloadWithOidcSecretReference()
    {
        string deployment = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "parties-ui", "deployment.yaml"));
        string service = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "parties-ui", "service.yaml"));
        string kustomization = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, "parties-ui", "kustomization.yaml"));

        deployment.ShouldContain("name: parties-ui");
        deployment.ShouldContain("image: registry.hexalith.com/parties-ui:");
        deployment.ShouldContain("name: zot-pull-secret");
        deployment.ShouldContain("path: /health");
        deployment.ShouldContain("port: http");
        deployment.ShouldNotContain("dapr.io/enabled");
        deployment.ShouldNotContain("dapr.io/app-id");
        deployment.ShouldNotContain("dapr.io/app-port");
        deployment.ShouldNotContain("dapr.io/config");
        deployment.ShouldContain("name: Authentication__OpenIdConnect__ClientSecret");
        deployment.ShouldContain("secretKeyRef:");
        deployment.ShouldContain("name: hexalith-parties-ui-oidc-client");
        deployment.ShouldContain("key: client-secret");
        deployment.ShouldNotContain("value: Authentication__OpenIdConnect__ClientSecret");

        service.ShouldContain("name: parties-ui");
        service.ShouldContain("port: 8080");
        service.ShouldContain("targetPort: 8080");

        kustomization.ShouldContain("Authentication__OpenIdConnect__Authority=https://auth.tache.ai/realms/tache");
        kustomization.ShouldContain("Authentication__OpenIdConnect__ClientId=hexalith-parties-ui");
        kustomization.ShouldContain("Authentication__OpenIdConnect__Audience=hexalith-eventstore");
        kustomization.ShouldNotContain("Authentication__OpenIdConnect__ClientSecret=");
    }

    private static string[] NormalizeNonImageLines(string text)
        => text.ReplaceLineEndings("\n")
            .Split('\n')
            .Where(static line => !line.TrimStart().StartsWith("image:", StringComparison.Ordinal))
            .ToArray();
}
