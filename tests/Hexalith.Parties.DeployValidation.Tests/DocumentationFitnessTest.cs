namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class DocumentationFitnessTest
{
    private static readonly string[] s_entryPointDocs =
    [
        "deploy/k8s/README.md",
        "docs/deployment-guide.md",
        "docs/getting-started.md",
        "docs/deployment-security-checklist.md",
    ];

    private static readonly string[] s_liveDeploymentDocs =
    [
        "docs/kubernetes-deployment-architecture.md",
        .. s_entryPointDocs,
    ];

    [Fact]
    public void LiveDeploymentDocsDoNotContainStaleDeploymentContracts()
    {
        Regex stalePattern = new(
            @"regen\.ps1|deploy-local\.ps1|teardown-local\.ps1|-AllowCloudCapabilities|--output\s+json|\bkind-[A-Za-z0-9-]*\b|\bminikube\b|\bdocker-desktop\b|\bk3d-[A-Za-z0-9-]*\b|registry\.hexalith\.com/[^\s`'""]+:(?:latest|staging-latest)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (string relativePath in s_liveDeploymentDocs)
        {
            string text = DeploymentTestPaths.ReadRepoFile(relativePath);
            Match match = stalePattern.Match(text);

            match.Success.ShouldBeFalse($"{relativePath} contains stale deployment contract text: {match.Value}");
        }
    }

    [Fact]
    public void EntryPointDocsLinkToCanonicalKubernetesDeploymentArchitecture()
    {
        foreach (string relativePath in s_entryPointDocs)
        {
            string text = DeploymentTestPaths.ReadRepoFile(relativePath);

            text.Contains("kubernetes-deployment-architecture.md", StringComparison.Ordinal).ShouldBeTrue($"{relativePath} must point operators to the canonical deployment architecture.");
        }
    }

    [Fact]
    public void StaleDocsFixturesExercisePositiveAndNegativePatterns()
    {
        string fixtureRoot = Path.Combine(DeploymentTestPaths.FixturesDirectory, "stale-docs");
        string stale = File.ReadAllText(Path.Combine(fixtureRoot, "stale.md"));
        string current = File.ReadAllText(Path.Combine(fixtureRoot, "current.md"));
        Regex stalePattern = new(@"regen\.ps1|deploy-local\.ps1|teardown-local\.ps1|-AllowCloudCapabilities|--output\s+json", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        stalePattern.IsMatch(stale).ShouldBeTrue("The stale-docs fixture must keep a positive sample.");
        stalePattern.IsMatch(current).ShouldBeFalse("The stale-docs fixture must keep a current negative sample.");
        current.ShouldContain("kubernetes-deployment-architecture.md");
    }
}
