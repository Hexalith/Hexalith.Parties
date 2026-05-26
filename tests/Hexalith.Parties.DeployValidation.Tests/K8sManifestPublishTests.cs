namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class K8sManifestPublishTests
{
    private static readonly string[] s_daprApps = ["eventstore", "eventstore-admin", "parties", "tenants", "memories"];

    private static readonly string[] s_daprClientOnlyApps = ["eventstore-admin-ui"];

    private static readonly string[] s_nonDaprApps = ["parties-mcp", "redis", "keycloak", "falkordb"];

    [Fact]
    public void PublishScriptDeclaresMinVerAspirateAndPatchContracts()
    {
        string publish = DeploymentTestPaths.ReadRepoFile("deploy/k8s/publish.ps1");

        publish.ShouldContain("dotnet msbuild $AppHostProject -t:Build -p:Configuration=Release -getProperty:Version");
        publish.ShouldContain("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[A-Za-z0-9.-]+)?(?:\\+[A-Za-z0-9.-]+)?$");
        publish.ShouldContain("$normalized.Contains('+dirty')");
        publish.ShouldContain("--container-registry', $Registry");
        publish.ShouldContain("--container-image-tag', $ImageTag");
        publish.ShouldContain("--include-dashboard', 'false'");
        publish.ShouldContain("--image-pull-policy', 'IfNotPresent'");
        publish.ShouldContain("DOTNET_ROLL_FORWARD");
        publish.ShouldContain("$env:ContainerImageTag = $ImageTag");
        publish.ShouldContain("Remove-Item Env:ContainerImageTags");
        publish.ShouldNotContain("$env:ContainerImageTags = $ImageTag");
        publish.ShouldContain("PUBLISH_TARGET must be unset before aspirate generate");
        publish.ShouldContain("Assert-ZotImageManifests $imageTag");
        publish.ShouldContain("Invoke-WebRequest -Uri $uri -Method Head");
        publish.ShouldContain("-TimeoutSec 30");
        publish.ShouldContain("Zot manifest verification failed for ${repository}:$ImageTag");
        publish.ShouldContain("Invoke-DeploymentValidator");
        publish.ShouldContain("Normalize-GeneratedKustomizations");
        publish.ShouldContain("Restart-GeneratedDeployments");
        publish.ShouldContain("Wait-WorkloadsReady");
    }

    [Fact]
    public void PublishScriptPatchesOnlyDocumentedDaprAndJwtTargets()
    {
        string publish = DeploymentTestPaths.ReadRepoFile("deploy/k8s/publish.ps1");

        foreach (string app in s_daprApps)
        {
            publish.Contains($"'{app}' = 'accesscontrol", StringComparison.Ordinal).ShouldBeTrue($"{app} must have an explicit Dapr config patch target.");
        }

        foreach (string app in s_nonDaprApps)
        {
            publish.Contains(app, StringComparison.Ordinal).ShouldBeTrue($"{app} must be named so tests can guard accidental patch target expansion.");
        }

        foreach (string app in s_daprClientOnlyApps)
        {
            publish.Contains(app, StringComparison.Ordinal).ShouldBeTrue($"{app} must be named as a Dapr client-only patch target.");
        }

        publish.ShouldContain("$DaprClientOnlyTargets = @('eventstore-admin-ui')");
        publish.ShouldContain("$ForbiddenDaprTargets = @('parties-mcp', 'redis', 'keycloak', 'falkordb')");
        publish.ShouldContain("Authentication__JwtBearer__SigningKey");
        publish.ShouldContain("EventStore__Authentication__SigningKey");
        publish.ShouldContain("secretKeyRef:");
        publish.ShouldContain("name: $JwtSecretName");
        publish.ShouldContain("Ensure-JwtSecretRef");
        publish.ShouldContain("Ensure-AdminUiJwtSecretRef");
    }

    [Fact]
    public void PublishScriptDeclaresImagePullSecretPatchContract()
    {
        string publish = DeploymentTestPaths.ReadRepoFile("deploy/k8s/publish.ps1");

        publish.ShouldContain("imagePullSecrets:");
        publish.ShouldContain("zot-pull-secret");
        publish.ShouldContain("$usesRegistry = $content -match \"image:\\s*$([regex]::Escape($Registry))/\"");
        publish.ShouldContain("Test-PodTemplateImagePullSecret");
        publish.ShouldContain("name:\\s*$([regex]::Escape($ZotSecretName))");
        publish.ShouldContain("Write-Host \"[publish] imagePullSecrets patch targets:");
        publish.ShouldNotContain("FromBase64String");
    }

    [Fact]
    public void PartiesMcpProjectIsPublishableForContainerEmission()
    {
        string project = DeploymentTestPaths.ReadRepoFile("src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj");

        project.ShouldContain("<IsPublishable>true</IsPublishable>");
        project.ShouldContain("<EnableContainer>true</EnableContainer>");
        project.ShouldContain("<ContainerRepository>parties-mcp</ContainerRepository>");
    }
}
