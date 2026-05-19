namespace Hexalith.Parties.DeployValidation.Tests;

/// <summary>
/// Story 9.3 AC8 — End-to-end deploy validation against a live local Kubernetes cluster.
/// **REQUIRES** a documented local-cluster context (kind-*, k3d-*, minikube, docker-desktop)
/// be active and the operator to have run <c>pwsh deploy/k8s/deploy-local.ps1</c>.
/// </summary>
/// <remarks>
/// <para>The class is annotated with <c>[Trait("RequiresCluster", "true")]</c> so the lane
/// runner (<c>scripts/test.ps1 -Lane deploy</c>) excludes it by default. The Epic 9 closure
/// gate is the operator running <c>dotnet test --filter "Trait=RequiresCluster"</c> against
/// a documented local cluster and posting the <c>kubectl get pods</c> snapshot in the
/// Epic 9 retro file.</para>
/// <para>Per AC3 Outcome B, the expected pod count is <b>9</b> (eventstore, eventstore-admin,
/// eventstore-admin-ui, parties, parties-mcp, tenants, memories, keycloak, redis). The
/// FrontComposer 10th pod is gated on Story 9.4 done.</para>
/// </remarks>
[Trait("RequiresCluster", "true")]
public sealed class K8sEndToEndDeployTests
{
    [Fact(Skip = "RequiresCluster trait — operator-gated; see Epic 9 retro for the manual run procedure.")]
    public void NinePodsReachReady_OnLocalClusterDeploy()
    {
        // Placeholder for the live-cluster assertion. The operator's manual workflow:
        //
        //   kubectl delete ns hexalith-parties
        //   pwsh deploy/k8s/deploy-local.ps1
        //   # wait for the 15-min NFR30 budget
        //   kubectl get pods -n hexalith-parties -o wide
        //
        // The kubectl output is posted in the Epic 9 retro file as the closure gate evidence.
        // Outcome B (FrontComposer carved out to Story 9.4): expect 9 pods.
        // Outcome A would expect 10 pods (frontcomposer added).
        true.ShouldBeTrue();
    }

    [Fact(Skip = "RequiresCluster trait — operator-gated.")]
    public void CreatePartyRoundTrip_SucceedsAgainstDeployedTopology()
    {
        // Placeholder. Operator runs the documented `docs/getting-started.md` Step 1b
        // CreateParty round-trip against the deployed topology and posts the result in
        // the Epic 9 retro file.
        true.ShouldBeTrue();
    }
}
