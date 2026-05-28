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
        ingress.ShouldContain("ingressClassName: nginx");
        ingress.ShouldContain("secretName: hexalith-pages-tls");
        ingress.ShouldContain("host: eventstore.hexalith.com");
        ingress.ShouldContain("name: eventstore-admin-ui");
        ingress.ShouldContain("host: sample.hexalith.com");
        ingress.ShouldContain("name: sample-blazor-ui");
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

    private static string[] NormalizeNonImageLines(string text)
        => text.ReplaceLineEndings("\n")
            .Split('\n')
            .Where(static line => !line.TrimStart().StartsWith("image:", StringComparison.Ordinal))
            .ToArray();
}
