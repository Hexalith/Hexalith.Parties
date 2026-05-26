# Sprint Change Proposal: Epic 9 Publish Runtime Correction

**Project:** Hexalith.Parties
**Date:** 2026-05-22
**Triggering context:** Manual publish attempt after PR #41 squash-merge of Story 9.7

## 1. Issue Summary

Story 9.7 closed the deployment fitness and LiveCluster test surface, but the first operator publish attempt after the squash-merge exposed runtime gaps that the static and fixture-based suite did not fully cover.

Concrete evidence from the publish attempt:

- `publish.ps1` initially failed at MinVer resolution because the AppHost did not compile with the post-merge Memories topology.
- .NET 10 SDK container publishing rejected the combination of `ContainerImageTags` and `ContainerImageTag`.
- Removing the plural environment variable made submodule projects fall back to `staging-latest`; setting the singular `ContainerImageTag` enforced the MinVer tag in generated publish commands.
- Aspirate printed successful build/push lines, but registry manifests for `registry.hexalith.com/*:0.0.0-preview.0.474` were absent. A manual publish exposed `CONTAINER1016` for `registry.hexalith.com/eventstore-admin-ui`, pointing to registry ACL/repository access gaps.
- `dapr init -k` failed on an already-installed healthy Dapr control plane with `cannot re-use a name that is still in use`.
- Aspirate regenerated a top-level `kustomization.yaml` without `namespace: hexalith-parties`, without `redis`/`keycloak` carve-outs, and with stale `dapr/*` resource references.
- A legacy `hexalith-keycloak-admin` Secret existed but lacked the `KC_BOOTSTRAP_ADMIN_*` keys expected by the current Keycloak image.
- Generated deployments lost mandatory liveness/readiness probes before apply.
- `memories` required `ConnectionStrings__redis` and `ConnectionStrings__falkordb`; pointing both to the MVP Redis service made the pod run but left FalkorDB health degraded (`ERR unknown command 'GRAPH.LIST'`). The topology therefore lacks a real graph backend for Memories.

## 2. Impact Analysis

**Epic impact:** Epic 9 remains valid but cannot be considered complete after Story 9.7. Add Story 9.8 to close runtime publish hardening and Memories backing-service gaps before the Epic 9 retrospective.

**Story impact:**

- Story 9.2 needs an AppHost correction for publish-mode Memories composition and backing-service connection strings.
- Story 9.3 needs a topology correction: either promote FalkorDB as a hand-authored carve-out or explicitly remove Memories graph dependency from the publish path. The recommended path is a FalkorDB carve-out because Memories.Server requires it at runtime.
- Story 9.5 needs stronger runtime publish behavior: existing Dapr detection, post-push manifest verification, canonical kustomization restoration, health-probe patching, and Secret key upgrade semantics.
- Story 9.6/9.7 need validation/fitness additions so these regressions fail before cluster mutation.

**Artifact conflicts:**

- `epics.md` currently frames Epic 9 as a 9-workload topology. If FalkorDB is added, docs and tests must move to a 10-workload topology.
- `docs/kubernetes-deployment-architecture.md`, `deploy/k8s/README.md`, and `docs/getting-started.md` must describe the corrected topology and publish preflight.
- `sprint-status.yaml` must add Story 9.8 and keep the Epic 9 retrospective gated until 9.8 is done.

**Technical impact:**

- Registry access must be verified per repository before applying Kubernetes manifests.
- Publish must fail before `kubectl apply -k` if any expected MinVer image is missing in Zot.
- The live cluster must not be left in a partial MinVer state when registry push fails.
- Existing operator-managed Secrets must be upgraded in place when key names change.

## 3. Recommended Approach

**Selected path:** Direct Adjustment.

Add Story 9.8: Publish Runtime Hardening, Registry Verification, and Memories Backing Services.

This is a moderate Epic 9 correction, not an MVP scope reduction. The MVP deploy goal still stands: a developer/operator can deploy a working instance from source. The corrective work is the missing runtime enforcement around that goal.

**Effort estimate:** Medium.
**Risk:** Medium, mainly because registry ACL and the FalkorDB carve-out cross the boundary between local code and live infrastructure.
**Timeline impact:** One additional Epic 9 story before retrospective.

Rollback is not recommended. The post-Story 9.7 state contains useful validation and should remain the baseline; the problem is that the suite missed runtime-only failure modes.

MVP review is not required. The deployment capability remains part of FR31/FR31a/FR60/FR61/NFR30.

## 4. Detailed Change Proposals

### Story Addition

**NEW:** Story 9.8: Publish Runtime Hardening, Registry Verification, and Memories Backing Services.

Rationale: Story 9.7 verified contracts, but the publish attempt exposed failure modes that only appear when aspirate, .NET SDK container publishing, Zot, Dapr, Kubernetes, and Memories runtime dependencies are exercised together.

### Epic Update

**OLD:**

Epic 9 is a 7-story greenfield rewrite with retrospective gated on Stories 9.1-9.7.

**NEW:**

Epic 9 is an 8-story greenfield rewrite with retrospective gated on Stories 9.1-9.8. Story 9.8 closes publish runtime hardening after the first post-merge operator publish attempt.

### Architecture / Deployment Documentation Update

**OLD:**

The topology is described as a 9-workload deployment.

**NEW:**

The topology must either be updated to include a real FalkorDB workload for Memories, or explicitly document and test a no-graph Memories mode. The recommended implementation is a hand-authored `deploy/k8s/falkordb/` carve-out and a 10-workload topology.

## 5. Implementation Handoff

**Scope classification:** Moderate.

**Route to:** Developer agent for Story 9.8 implementation, with operator support for Zot access-control verification.

**Developer responsibilities:**

- Update AppHost and `publish.ps1` contracts.
- Add/adjust deploy-validation tests.
- Add the FalkorDB carve-out and update topology docs if selected.
- Ensure `publish.ps1` fails before cluster mutation when registry manifests are missing.
- Verify static validator, focused fitness tests, and a real publish run.

**Operator responsibilities:**

- Confirm `parties-publisher` can push and pull every required repository in Zot.
- Confirm Dapr control plane health and version behavior on the target cluster.

**Success criteria:**

- `publish.ps1 -ConfirmContext kubernetes-admin@cluster.local` succeeds from a clean post-merge checkout or fails before cluster mutation with bounded actionable diagnostics.
- All expected registry manifests exist before `kubectl apply -k`.
- No generated resources land in the `default` namespace.
- All expected pods reach Ready.
- `deploy/validate-deployment.ps1` passes.
- Story 9.8 focused tests pass.

## 6. Checklist Summary

- [x] 1.1 Triggering story identified: Story 9.7 post-merge publish attempt.
- [x] 1.2 Core problem defined: runtime publish pipeline gaps and incomplete Memories backing-service topology.
- [x] 1.3 Evidence collected: command failures, registry manifest absence, Dapr init behavior, kustomization drift, Secret key drift, Memories runtime errors.
- [x] 2.1 Current epic remains viable with modification.
- [x] 2.2 Add a new Epic 9 story.
- [x] 2.3 Remaining planned epics unaffected.
- [x] 2.4 No epic invalidated.
- [x] 2.5 Priority: Story 9.8 before Epic 9 retrospective.
- [x] 3.1 PRD remains valid; deploy success gate still applies.
- [x] 3.2 Architecture/deployment docs require update.
- [N/A] 3.3 UI/UX unaffected.
- [x] 3.4 Scripts, manifests, tests, and docs affected.
- [x] 4.1 Direct adjustment viable.
- [x] 4.2 Rollback not recommended.
- [x] 4.3 MVP review not required.
- [x] 4.4 Recommended path selected: Direct Adjustment.
