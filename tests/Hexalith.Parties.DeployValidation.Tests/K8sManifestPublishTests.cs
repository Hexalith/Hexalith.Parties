namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class K8sManifestPublishTests
{
    private static readonly string[] s_daprApps = ["eventstore", "eventstore-admin", "sample", "parties", "tenants", "memories"];

    private static readonly string[] s_daprClientOnlyApps = ["eventstore-admin-ui", "sample-blazor-ui"];

    private static readonly string[] s_nonDaprApps = ["parties-mcp", "parties-ui", "redis", "falkordb"];

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

        publish.ShouldContain("$DaprClientOnlyTargets = @('eventstore-admin-ui', 'sample-blazor-ui')");
        publish.ShouldContain("$ForbiddenDaprTargets = @('parties-mcp', 'parties-ui', 'redis', 'falkordb')");
        publish.ShouldContain("http://auth.tache.ai:8080/realms/tache");
        publish.ShouldContain("$UiCredentialsSecretName = 'hexalith-tache-ui-credentials'");
        publish.ShouldContain("$PartiesUiOidcSecretName = 'hexalith-parties-ui-oidc-client'");
        publish.ShouldContain("$PartiesUiOidcSecretKey = 'client-secret'");
        publish.ShouldContain("EventStore__Authentication__Username");
        publish.ShouldContain("EventStore__Authentication__Password");
        publish.ShouldContain("Authentication__OpenIdConnect__ClientSecret");
        publish.ShouldContain("Patch-KeycloakHostAlias");
        publish.ShouldContain("Test-KeycloakTacheRealmContract");
        publish.ShouldContain("Assert-KeycloakTokenContract");
        publish.ShouldContain("Reconcile-LegacyLocalKeycloakResources");
        publish.ShouldContain("deployment/keycloak");
        publish.ShouldContain("hexalith-keycloak-admin");
        publish.ShouldContain("secretKeyRef:");
        publish.ShouldContain("name: $UiCredentialsSecretName");
        publish.ShouldContain("Validate-UiCredentialsSecret");
        publish.ShouldContain("Validate-PartiesUiOidcClientSecret");
        publish.ShouldContain("$presenceTemplate = \"{{- if index .data `\"$PartiesUiOidcSecretKey`\" -}}present{{- end -}}\"");
        publish.ShouldContain("go-template=$presenceTemplate");
        publish.ShouldNotContain("jsonpath={.data.$PartiesUiOidcSecretKey}");
        publish.ShouldContain("Ensure-PartiesUiOidcClientSecretRef");
        publish.ShouldContain("Assert-NoSigningKeyReferences");
        publish.ShouldNotContain("Ensure-JwtSecretRef");
        publish.ShouldNotContain("Ensure-UiJwtSecretRefs");
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
    }

    [Fact]
    public void PartiesMcpProjectIsPublishableForContainerEmission()
    {
        string project = DeploymentTestPaths.ReadRepoFile("src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj");

        project.ShouldContain("<IsPublishable>true</IsPublishable>");
        project.ShouldContain("<EnableContainer>true</EnableContainer>");
        project.ShouldContain("<ContainerRepository>parties-mcp</ContainerRepository>");
    }

    [Fact]
    public void PartiesUiProjectIsPublishableForContainerEmissionAndServiceDefaults()
    {
        string project = DeploymentTestPaths.ReadRepoFile("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj");
        string program = DeploymentTestPaths.ReadRepoFile("src/Hexalith.Parties.UI/Program.cs");

        project.ShouldContain("<IsPublishable>true</IsPublishable>");
        project.ShouldContain("<EnableContainer>true</EnableContainer>");
        project.ShouldContain("<ContainerRepository>parties-ui</ContainerRepository>");
        project.ShouldContain("..\\Hexalith.Parties.ServiceDefaults\\Hexalith.Parties.ServiceDefaults.csproj");
        project.ShouldNotContain("Version=");
        program.ShouldContain("using Hexalith.Parties.ServiceDefaults;");
        program.ShouldContain("builder.AddServiceDefaults();");
        program.ShouldContain("app.MapDefaultEndpoints();");
    }
}
