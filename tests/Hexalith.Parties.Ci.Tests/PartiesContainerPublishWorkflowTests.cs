namespace Hexalith.Parties.Ci.Tests;

public sealed class PartiesContainerPublishWorkflowTests
{
    [Fact]
    public void CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/ci.yml");

        workflow.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        workflow.ShouldContain("solution: Hexalith.Parties.slnx");
        workflow.ShouldContain("run-consumer-validation: true");
        workflow.ShouldContain("run-coverage-gate: false");
        workflow.ShouldContain("tests/Hexalith.Parties.Contracts.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Authentication.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Client.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Server.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Projections.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Security.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.AdminPortal.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.ConsumerPortal.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.UI.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Picker.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Mcp.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Sample.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Ci.Tests");
        workflow.ShouldContain("aspire-test-project: tests/Hexalith.Parties.IntegrationTests");
        workflow.ShouldNotContain("submodules: recursive");
    }

    [Fact]
    public void ReleaseWorkflowPublishesOnlyPartiesContainersThroughSharedDomainRelease()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml");

        workflow.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main");
        workflow.ShouldContain("publish-containers: true");
        workflow.ShouldContain("src/Hexalith.Parties/Hexalith.Parties.csproj|parties");
        workflow.ShouldContain("src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj|parties-mcp");
        workflow.ShouldContain("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj|parties-ui");
        workflow.ShouldContain("secrets: inherit");
        workflow.ShouldContain("tests/Hexalith.Parties.Ci.Tests");
        workflow.ShouldNotContain("eventstore-admin");
        workflow.ShouldNotContain("sample-blazor-ui");
        workflow.ShouldNotContain("|tenants");
        workflow.ShouldNotContain("|memories");
        workflow.ShouldNotContain(":latest");
    }

    [Fact]
    public void ReleaseSupportFilesDeclareSemanticReleaseAndSecretContracts()
    {
        string packageJson = CiTestPaths.ReadRepoFile("package.json");
        string releaseConfig = CiTestPaths.ReadRepoFile("release.config.cjs");
        string secretCheck = CiTestPaths.ReadRepoFile("scripts/validate-release-secrets.sh");

        packageJson.ShouldContain("\"semantic-release\"");
        packageJson.ShouldContain("\"@commitlint/cli\"");
        releaseConfig.ShouldContain("scripts/pack-release-packages.py");
        releaseConfig.ShouldContain("scripts/validate-nuget-packages.py");
        releaseConfig.ShouldContain("scripts/validate-consumer-package-references.py");
        releaseConfig.ShouldContain("dotnet nuget push ./nupkgs/*.nupkg");
        releaseConfig.ShouldContain("./.hexalith/release/publish-containers.sh");
        secretCheck.ShouldContain("NUGET_API_KEY");
        secretCheck.ShouldContain("HEXALITH_ZOT_USERNAME");
        secretCheck.ShouldContain("HEXALITH_ZOT_API_KEY");
        secretCheck.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
    }

    [Fact]
    public void CiDocsDescribeSharedCiReleaseAndZotApiKeyPublishContract()
    {
        string ci = CiTestPaths.ReadRepoFile("docs/ci.md");
        string secrets = CiTestPaths.ReadRepoFile("docs/ci-secrets-checklist.md");

        ci.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        ci.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main");
        ci.ShouldContain("registry.hexalith.com/parties");
        ci.ShouldContain("registry.hexalith.com/parties-mcp");
        ci.ShouldContain("registry.hexalith.com/parties-ui");
        ci.ShouldContain("does not apply runtime deployment manifests");
        secrets.ShouldContain("NUGET_API_KEY");
        secrets.ShouldContain("HEXALITH_ZOT_USERNAME");
        secrets.ShouldContain("HEXALITH_ZOT_API_KEY");
        secrets.ShouldContain("Zot API key");
        secrets.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
    }
}
