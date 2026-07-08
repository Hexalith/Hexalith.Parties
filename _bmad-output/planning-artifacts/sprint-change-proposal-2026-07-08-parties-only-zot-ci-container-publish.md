---
project_name: parties
user_name: Administrator
date: 2026-07-08
scope_classification: Moderate
status: approved-implemented-pending-live-push
mode: Batch
trigger: "Add CI/CD container publishing for Parties-only workloads to the modified Zot registry path at registry.hexalith.com, whose live auth now advertises Keycloak OIDC plus Zot API keys."
---

# Sprint Change Proposal - Parties-only Zot CI container publishing

## 1. Issue Summary

The repository has a documented Zot registry substrate at `registry.hexalith.com`, but the root GitHub Actions workflows currently stop at quality gates. They do not publish the Parties application containers to Zot.

The requested change is to publish only the Parties-owned application images for now:

- `registry.hexalith.com/parties:<version>`
- `registry.hexalith.com/parties-mcp:<version>`
- `registry.hexalith.com/parties-ui:<version>`

This is intentionally narrower than the existing operator flow in `deploy/k8s/publish.ps1`, which regenerates and deploys the full topology and verifies all ten generated application images.

Current live-registry evidence gathered on 2026-07-08:

- `GET https://registry.hexalith.com/v2/` returns `200` without authentication.
- `GET https://registry.hexalith.com/v2/_catalog` returns `200` without authentication and lists the ten known repositories.
- `GET https://registry.hexalith.com/v2/parties/tags/list`, `parties-mcp/tags/list`, and `parties-ui/tags/list` return tags without authentication. Reads are currently public.
- `GET https://registry.hexalith.com/v2/_zot/ext/mgmt` returns Zot `v2.1.17` and advertises `http.auth.openid.providers.oidc.name = "Keycloak"` plus `http.auth.apikey = true`.
- `GET https://registry.hexalith.com/zot/auth/login?provider=oidc` redirects to `https://auth.tache.ai/realms/tache/protocol/openid-connect/auth` with `client_id=zot-private`.
- Zot's UI asset exposes "Manage your API Keys" and calls `/zot/auth/apikey`.

Repository evidence:

- `.github/workflows/test.yml` runs restore/build/tests/a11y/contract readiness only; it does not push OCI images.
- `.github/workflows/rc-gate.yml` validates root gitlink release-candidate signoff only.
- `docs/deployment-guide.md` states CI-side Zot wiring is post-MVP and still describes the older local `docker login -u parties-publisher` path.
- `deploy/zot/README.md` documents an older htpasswd-centered registry contract with builder identities including `github-ci`, `kaniko`, and `parties-publisher`.
- `docs/kubernetes-deployment-architecture.md` and `deploy/k8s/publish.ps1` now assume the Kubernetes nginx Ingress path for Zot, not a local NodePort or workstation bridge.

Terminology note: CI should publish to the OCI registry host `registry.hexalith.com`; image references should not include the URL scheme. The HTTPS endpoint remains `https://registry.hexalith.com/v2/...` for registry API checks.

## 2. Impact Analysis

### Epic Impact

Epics 1-5 remain complete and unaffected. The requested change adds deployment automation, not product functionality.

Epic 8 is the active maintenance epic and already contains Story 8.8, "Client, MCP, AppHost, build, and deploy cleanup." That story is broad and currently gated by package/source-mode prerequisites. This CI publishing work should be split as a dedicated, smaller deployment automation story rather than folded into all of 8.8.

Recommended backlog placement:

- Add a new Epic 8 deployment automation story after the existing done Story 8.11.
- Suggested ID/title: `8-12-parties-only-zot-container-publish-ci`.
- Keep it explicitly post-MVP maintenance with no new PRD functional coverage.

### Story Impact

Affected existing story:

- Story 1.10 remains done. Its container metadata and deploy topology are reused. Do not reopen it unless the team wants to backfill historical acceptance criteria.

Affected active backlog:

- Story 8.8 should keep the larger AppHost/build/deploy cleanup scope. The new CI story should be independent and narrower, because publishing three Parties containers to Zot does not require moving platform-owned deploy assets or resolving every 8.8 prerequisite.

### Artifact Conflicts

PRD:

- No product requirement change. The PRD remains a UI/product-feature source. This work supports NFR9/build-quality and release operations only.

Architecture:

- `docs/kubernetes-deployment-architecture.md` needs a CI/CD subsection distinguishing:
  - "container publish to Zot" for the three Parties-only images, and
  - "cluster publish/apply" through `deploy/k8s/publish.ps1`, which remains full-topology.

Epics / sprint status:

- `epics.md` and `sprint-status.yaml` need a new maintenance story entry once this proposal is approved.

CI docs:

- `docs/ci.md` should document the new container-publish workflow, triggers, tags, and non-goals.
- `docs/ci-secrets-checklist.md` should add Zot publishing secrets for the infra-confirmed Zot/Keycloak identity and its API key.

Deployment docs:

- `docs/deployment-guide.md` should stop describing CI-side wiring as purely post-MVP once the workflow lands.
- `deploy/zot/README.md` should remain the registry-side source for infra-owned htpasswd/TLS configuration. Do not commit registry secrets.

Technical artifacts:

- Add `.github/workflows/publish-parties-containers.yml` or equivalent.
- Prefer a small repo-owned script such as `scripts/publish-parties-containers.ps1` to keep workflow YAML thin and locally reproducible.
- Add deploy-validation or script tests proving the workflow publishes only `parties`, `parties-mcp`, and `parties-ui`, uses SemVer/MinVer-derived immutable tags, avoids `latest`, and never calls `deploy/k8s/publish.ps1`.

### Technical Impact

The current build already marks the three target projects as SDK-container publishable:

- `src/Hexalith.Parties/Hexalith.Parties.csproj`: `ContainerRepository=parties`
- `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj`: `ContainerRepository=parties-mcp`
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`: `ContainerRepository=parties-ui`

The workflow should:

- run after the quality gate succeeds, or as a separate tag/manual workflow that repeats restore/build before publish;
- use .NET SDK `10.0.301`;
- check out root submodules under `references/` without recursive submodule initialization;
- resolve the image tag from MinVer or a `v*` tag, dropping the leading `v` for image tags;
- authenticate to `registry.hexalith.com` using GitHub Actions secrets for the infra-confirmed Zot API key;
- publish only the three target projects with `/t:PublishContainer`;
- verify each pushed manifest through `HEAD https://registry.hexalith.com/v2/<repo>/manifests/<tag>`;
- avoid mutable `latest` tags unless a separate explicit policy is approved.

## 3. Recommended Approach

Recommended path: Direct Adjustment with backlog split.

This is not a rollback and does not require an MVP review. The safe path is to add a narrow maintenance story and implement a dedicated CI publish workflow for the three Parties containers only.

Effort estimate: Medium.

Risk level: Medium.

Main risks:

- Registry credentials: the new GitHub secrets must carry a Zot API key generated for an identity that has builder permissions. The API key replaces the password for automation; do not store a human SSO password.
- Tag policy: the workflow must not push `latest` or dirty tags unless explicitly allowed.
- Scope creep: reusing `deploy/k8s/publish.ps1` would accidentally return to full-topology deployment and all ten image checks. The new workflow should not do that.
- Package availability: package-mode restore may still be blocked by unpublished Hexalith packages. The story must either use package mode when available or document a source-mode CI decision aligned with the existing G12 action item.

## 4. Detailed Change Proposals

### Proposal A - New Story

Story: `8-12-parties-only-zot-container-publish-ci`

Section: Story

OLD:

No backlog item exists for Parties-only CI container publishing to Zot.

NEW:

As a release operator,
I want GitHub Actions to publish the `parties`, `parties-mcp`, and `parties-ui` container images to Zot at `registry.hexalith.com`,
so that Parties images are available in the registry without running the full Kubernetes publish/apply flow.

Rationale:

The registry exists and the three projects are already container-enabled. The missing piece is a bounded CI/CD path that respects the recent Zot ingress/auth changes while limiting scope to Parties.

### Proposal B - Acceptance Criteria

Story: `8-12-parties-only-zot-container-publish-ci`

Section: Acceptance Criteria

NEW:

1. Given a push to `main`, a `v*` tag, or a manual workflow dispatch, when the Parties container publish workflow runs after quality checks, then it publishes exactly `parties`, `parties-mcp`, and `parties-ui` to `registry.hexalith.com`.
2. Given the workflow resolves a version, when image tags are generated, then tags are SemVer/MinVer-shaped, omit the leading git-tag `v`, and do not use `latest`.
3. Given Zot credentials are required, when the workflow authenticates, then it uses a dedicated Zot API key stored in GitHub Actions secrets. The key must be generated after Keycloak/OIDC login by a registry identity authorized to create/update the `parties`, `parties-mcp`, and `parties-ui` repositories.
4. Given publishing completes, when verification runs, then each image manifest is checked through the Zot v2 API over `https://registry.hexalith.com`.
5. Given the workflow is Parties-only, when it runs, then it does not run `deploy/k8s/publish.ps1`, does not apply Kubernetes manifests, and does not require the non-Parties images `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `tenants`, or `memories` to exist at the same tag.
6. Given CI secrets or registry auth are malformed or missing, when the workflow runs, then it fails closed without printing secret values.
7. Given deploy-validation tests run, when they inspect the workflow/script contract, then they prove the repository list, tag policy, manifest verification, and no-`publish.ps1` boundary.

### Proposal C - Workflow Shape

Artifact: `.github/workflows/publish-parties-containers.yml`

OLD:

Only `test.yml` and `rc-gate.yml` exist at the repository root. Neither publishes application images.

NEW:

Add a workflow with a narrow job:

- checkout with root submodules only;
- setup .NET `10.0.301`;
- restore/build the three target projects or the solution;
- resolve a MinVer image tag;
- authenticate to Zot with `ZOT_REGISTRY_USERNAME` / `ZOT_REGISTRY_API_KEY`;
- publish each target project with `/t:PublishContainer`, `--configuration Release`, `--os linux`, `--arch x64`, `-p:ContainerRegistry=registry.hexalith.com`, and `-p:ContainerImageTag=<tag>`;
- verify manifests with authenticated `HEAD` requests to Zot.

Rationale:

This avoids coupling the three-image publish path to full-topology Kubernetes apply and keeps the recent registry ingress/auth contract explicit.

### Proposal D - Local Script

Artifact: `scripts/publish-parties-containers.ps1`

OLD:

No local equivalent exists for the proposed CI publish path. The only publish script is `deploy/k8s/publish.ps1`, which owns the full topology.

NEW:

Add a local script with parameters:

- `-Registry registry.hexalith.com`
- `-ImageTag <semver>`
- `-SkipManifestVerification` only for local dry-run diagnostics, not CI

The script publishes:

- `src/Hexalith.Parties/Hexalith.Parties.csproj`
- `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj`
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`

Rationale:

Keeping publish logic in a script makes it testable by `DeployValidation.Tests` and avoids duplicating repository lists in YAML.

### Proposal E - Secrets Documentation

Artifact: `docs/ci-secrets-checklist.md`

OLD:

No secrets are required for the current .NET build and test jobs. Pact secrets are the only future CI secrets listed.

NEW:

Add "Required for Parties-only Zot container publish":

- `ZOT_REGISTRY_USERNAME`: the Zot username / mapped Keycloak identity that owns the CI API key. Prefer `github-ci` only if infra confirms this is the mapped Zot identity with builder rights.
- `ZOT_REGISTRY_API_KEY`: Zot API key created after Keycloak/OIDC login for that identity. Per Zot behavior, the key is shown only once at creation time and then replaces the password in Basic-auth-compatible clients.

Rationale:

The registry auth model now advertises Keycloak OIDC plus Zot API keys. CI should not use SSO browser credentials directly; it should use an API key whose permissions are scoped by the creating Zot identity.

### Proposal F - Documentation Updates

Artifacts:

- `docs/ci.md`
- `docs/deployment-guide.md`
- `docs/kubernetes-deployment-architecture.md`
- `deploy/k8s/README.md`

OLD:

Docs describe `publish.ps1` as the operator path and CI-side registry wiring as post-MVP.

NEW:

Docs distinguish:

- CI container publish: authenticates with a Zot API key and pushes only `parties`, `parties-mcp`, `parties-ui` to Zot.
- Operator Kubernetes publish: `deploy/k8s/publish.ps1` remains full-topology and still verifies ten generated images before apply.

Rationale:

This prevents operators from assuming that a successful Parties-only image publish is also a full cluster deployment.

## 5. Implementation Handoff

Scope classification: Moderate.

Routed to:

- Developer agent: implement the workflow, script, tests, and docs.
- Test Architect: review the workflow's fail-closed and no-secret-output behavior.
- Infra/operator owner: provision or confirm the Zot/Keycloak publishing identity, API key, and repository permissions.

Recommended sequencing:

1. Confirm infra has a Keycloak/Zot identity with builder permissions for `parties`, `parties-mcp`, and `parties-ui`, then generate a Zot API key through the registry UI or `/zot/auth/apikey`.
2. Add the story to `epics.md` and `sprint-status.yaml` after approval.
3. Implement `scripts/publish-parties-containers.ps1`.
4. Add `.github/workflows/publish-parties-containers.yml`.
5. Add deploy-validation tests for repository list, tag policy, secret hygiene, and no full-topology publish.
6. Update CI/deployment docs.
7. Run focused validation: `scripts/test.ps1 -Lane deploy`, plus a manual `workflow_dispatch` in a registry-enabled environment.

Success criteria:

- A GitHub Actions run can push all three Parties images to `registry.hexalith.com` using a Zot API key.
- The image tags are immutable MinVer/SemVer tags, not `latest`.
- No registry secret appears in logs, manifests, docs, or committed files.
- The workflow does not deploy to Kubernetes.
- Existing `test.yml` quality gate remains unchanged.
- `deploy/k8s/publish.ps1` remains the full-topology operator path and is not weakened.

## 6. Checklist Summary

| Checklist Item | Status | Notes |
|---|---|---|
| 1.1 Triggering story identified | [x] | Existing Story 1.10 owns container/deploy topology; new request needs a follow-up CI story. |
| 1.2 Core problem defined | [x] | New deployment automation requirement after Zot registry changes. |
| 1.3 Evidence gathered | [x] | Root workflows lack image publish; docs mark CI wiring post-MVP; live Zot advertises Keycloak OIDC plus API keys; committed docs still name older builder users. |
| 2.1 Current epic evaluated | [x] | Epic 8 is active maintenance; Story 8.8 is related but too broad. |
| 2.2 Epic-level changes | [x] | Approved; add Story 8.12 to Epic 8. |
| 2.3 Future epics reviewed | [x] | No product-feature epic changes. |
| 2.4 New epic needed | [N/A] | New story is sufficient. |
| 2.5 Priority/order | [x] | Can be implemented independently before broader 8.8 cleanup. |
| 3.1 PRD conflicts | [x] | No PRD functional impact. |
| 3.2 Architecture conflicts | [x] | Needs CI/CD distinction in deployment architecture docs. |
| 3.3 UI/UX conflicts | [N/A] | No UI behavior or UX surface change. |
| 3.4 Other artifacts | [x] | GitHub Actions, scripts, CI secrets docs, deployment docs, and deploy-validation tests updated. |
| 4.1 Direct adjustment | [x] | Viable with a new narrow story. |
| 4.2 Rollback | [N/A] | No rollback simplifies the issue. |
| 4.3 MVP review | [N/A] | MVP product scope unaffected. |
| 4.4 Recommended path | [x] | Direct adjustment with backlog split. |
| 5.1 Issue summary | [x] | Captured above. |
| 5.2 Impact and artifact needs | [x] | Captured above. |
| 5.3 Recommended path | [x] | Captured above. |
| 5.4 MVP impact/action plan | [x] | No MVP change; action plan included. |
| 5.5 Handoff plan | [x] | DEV + Test Architect + infra/operator owner. |
| 6.1 Checklist completion | [x] | Action-needed items are documented. |
| 6.2 Proposal accuracy | [x] | Draft based on current repository artifacts. |
| 6.3 User approval | [x] | Approved by operator on 2026-07-08; account rights confirmed and recommended approach selected. |
| 6.4 Sprint status update | [x] | Story 8.12 added for implementation tracking. |
| 6.5 Next steps/handoff | [x] | Developer implementation completed; live push remains to validate through GitHub Actions secrets. |

## 7. Registry Verification Notes - 2026-07-08

The live registry configuration no longer matches the committed `deploy/zot/configmap.yaml` exactly.

Observed live behavior:

- OCI read endpoints are public today (`/v2/`, `_catalog`, tags, and manifests returned `200` unauthenticated).
- Zot management endpoint advertises `releaseTag: v2.1.17`.
- Zot management endpoint advertises `http.auth.openid.providers.oidc.name: Keycloak`.
- Zot management endpoint advertises `http.auth.apikey: true`.
- OIDC login redirects to Keycloak realm `tache` with client `zot-private`.

Implications for CI:

- GitHub Actions should not use a long-lived Keycloak human password.
- The expected automation credential is a Zot API key, used as the password portion of Basic auth by Docker/.NET container publishing.
- GitHub Secrets should store the username and API key separately.
- A mutating push probe was not run because no registry credential was available and unauthenticated mutation tests should not create registry state.

Test credential note supplied by the operator:

- Test username for GitHub Actions registry login: `qdassivignon@itaneo.com`.
- Test API key: supplied out-of-band in chat on 2026-07-08; intentionally not written to this file, docs, workflow YAML, scripts, or logs.
- The test API key is expected to expire on 2026-07-09 and be deleted after validation.

Production hardening note:

- The temporary validation identity is accepted for this test. Before durable production use, prefer a dedicated non-human Keycloak/Zot service identity for GitHub Actions, then rotate the GitHub secret to that identity's Zot API key.
