namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class CarveOutPreservationFitnessTest
{
    [Fact]
    public void PublishCleanupPreservesCarveOutsAndEntryPoints()
    {
        string publish = DeploymentTestPaths.ReadRepoFile("deploy/k8s/publish.ps1");

        foreach (string preserved in new[] { "redis", "keycloak", "falkordb", "kustomization.yaml", "namespace.yaml", "README.md", "publish.ps1", "teardown.ps1", "_lib" })
        {
            publish.ShouldContain($"'{preserved}'");
        }

        foreach (string generated in new[] { "eventstore", "eventstore-admin", "eventstore-admin-ui", "parties", "parties-mcp", "tenants", "memories" })
        {
            publish.ShouldContain($"Remove-Item -LiteralPath $path -Recurse -Force");
            publish.ShouldContain($"'{generated}'");
        }
    }

    [Theory]
    [InlineData("redis")]
    [InlineData("keycloak")]
    [InlineData("falkordb")]
    public void CarveOutDeploymentManifestsDoNotCarryDaprJwtOrZotPatchArtifacts(string folder)
    {
        string deployment = File.ReadAllText(Path.Combine(DeploymentTestPaths.K8sDirectory, folder, "deployment.yaml"));

        deployment.ShouldNotContain("dapr.io/");
        deployment.ShouldNotContain("Authentication__JwtBearer__SigningKey");
        deployment.ShouldNotContain("hexalith-jwt-signing");
        deployment.ShouldNotContain("zot-pull-secret");
        deployment.ShouldNotContain("imagePullSecrets:");
    }

    [Fact]
    public void CuratedCarveOutFixturePreservesBaselineBytes()
    {
        string fixtureRoot = Path.Combine(DeploymentTestPaths.FixturesDirectory, "carve-out-preservation");
        string tempWorkspace = Path.Combine(Path.GetTempPath(), "hexalith-parties-carveout-" + Guid.NewGuid().ToString("N"));

        try
        {
            CopyDirectory(Path.Combine(fixtureRoot, "generated-workspace"), tempWorkspace);
            foreach (string generatedFolder in new[] { "eventstore", "eventstore-admin", "eventstore-admin-ui", "parties", "parties-mcp", "tenants", "memories" })
            {
                string path = Path.Combine(tempWorkspace, generatedFolder);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }

            foreach (string folder in new[] { "redis", "keycloak", "falkordb" })
            {
                string baseline = File.ReadAllText(Path.Combine(fixtureRoot, "baseline", folder, "deployment.yaml"));
                string preserved = File.ReadAllText(Path.Combine(tempWorkspace, folder, "deployment.yaml"));

                preserved.ShouldBe(baseline, $"{folder} must remain byte-identical through simulated publish cleanup.");
            }
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
