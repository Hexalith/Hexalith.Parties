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
        "tenants",
    ];

    [Fact]
    public void GeneratedServiceFolderSetMatchesStoryNineTwoContract()
    {
        string[] generatedFolders = Directory.EnumerateDirectories(DeploymentTestPaths.K8sDirectory)
            .Select(Path.GetFileName)
            .Where(static name => name is not "redis" and not "keycloak" and not "_lib")
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        generatedFolders.ShouldBe(s_expectedGeneratedFolders, "Story 9.2 owns exactly seven generated application folders; carve-outs are tested separately.");
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

    private static string[] NormalizeNonImageLines(string text)
        => text.ReplaceLineEndings("\n")
            .Split('\n')
            .Where(static line => !line.TrimStart().StartsWith("image:", StringComparison.Ordinal))
            .ToArray();
}
