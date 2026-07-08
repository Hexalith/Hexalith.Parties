namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class PartiesContainerPublishWorkflowTests
{
    [Fact]
    public void WorkflowPublishesOnlyPartiesImagesToZotWithApiKeySecrets()
    {
        string workflow = DeploymentTestPaths.ReadRepoFile(".github/workflows/publish-parties-containers.yml");

        workflow.ShouldContain("Publish Parties Containers");
        workflow.ShouldContain("registry.hexalith.com");
        workflow.ShouldContain("docker/login-action@v4");
        workflow.ShouldContain("secrets.ZOT_REGISTRY_USERNAME");
        workflow.ShouldContain("secrets.ZOT_REGISTRY_API_KEY");
        workflow.ShouldContain("scripts/publish-parties-containers.ps1");
        workflow.ShouldContain("src/Hexalith.Parties/Hexalith.Parties.csproj");
        workflow.ShouldContain("src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj");
        workflow.ShouldContain("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj");
        workflow.ShouldNotContain("deploy/k8s/publish.ps1");
        workflow.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
        workflow.ShouldNotContain(":latest");
    }

    [Fact]
    public void PublishScriptPinsPartiesOnlyRepositoriesAndImmutableTagPolicy()
    {
        string script = DeploymentTestPaths.ReadRepoFile("scripts/publish-parties-containers.ps1");

        script.ShouldContain("Repository = \"parties\"");
        script.ShouldContain("Repository = \"parties-mcp\"");
        script.ShouldContain("Repository = \"parties-ui\"");
        script.ShouldContain("src/Hexalith.Parties/Hexalith.Parties.csproj");
        script.ShouldContain("src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj");
        script.ShouldContain("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj");
        script.ShouldContain("-p:ContainerRegistry=$Registry");
        script.ShouldContain("-p:ContainerRepository=$($image.Repository)");
        script.ShouldContain("-p:ContainerImageTag=$ImageTag");
        script.ShouldContain("\"--os\"");
        script.ShouldContain("\"linux\"");
        script.ShouldContain("\"--arch\"");
        script.ShouldContain("\"x64\"");
        script.ShouldContain("\"msbuild\"");
        script.ShouldContain("-getProperty:Version");
        script.ShouldContain("$normalized.Contains(\"+dirty\"");
        script.ShouldContain("staging-latest");
        script.ShouldContain("Invoke-WebRequest -Uri $uri -Method Head");
        script.ShouldContain("ZOT_REGISTRY_USERNAME");
        script.ShouldContain("ZOT_REGISTRY_API_KEY");
        script.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
        script.ShouldNotContain("eventstore-admin");
        script.ShouldNotContain("sample-blazor-ui");
        script.ShouldNotContain("tenants");
        script.ShouldNotContain("memories");
    }

    [Fact]
    public void CiDocsDescribeCurrentZotOidcApiKeyPublishContract()
    {
        string ci = DeploymentTestPaths.ReadRepoFile("docs/ci.md");
        string secrets = DeploymentTestPaths.ReadRepoFile("docs/ci-secrets-checklist.md");

        ci.ShouldContain("Publish Parties Containers");
        ci.ShouldContain("registry.hexalith.com/parties");
        ci.ShouldContain("registry.hexalith.com/parties-mcp");
        ci.ShouldContain("registry.hexalith.com/parties-ui");
        ci.ShouldContain("ZOT_REGISTRY_USERNAME");
        ci.ShouldContain("ZOT_REGISTRY_API_KEY");
        ci.ShouldContain("does not run deploy/k8s/publish.ps1");
        secrets.ShouldContain("ZOT_REGISTRY_USERNAME");
        secrets.ShouldContain("ZOT_REGISTRY_API_KEY");
        secrets.ShouldContain("Zot API key");
        secrets.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
    }
}
