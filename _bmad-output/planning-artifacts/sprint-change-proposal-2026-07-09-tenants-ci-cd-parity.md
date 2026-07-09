---
project_name: parties
user_name: Administrator
date: 2026-07-09
scope_classification: Moderate
status: implemented-direct-user-request
mode: Batch
trigger: "Analyze Hexalith.Tenants CI/CD and apply the same reusable Hexalith.Builds-based CI/CD shape to Hexalith.Parties."
---

# Sprint Change Proposal - Tenants CI/CD parity for Parties

## 1. Issue Summary

Hexalith.Tenants now uses a shared Hexalith.Builds CI/CD model:

- `ci.yml` delegates build/test/package validation to `domain-ci.yml`.
- `release.yml` delegates semantic-release, NuGet publish, and container publish to `domain-release.yml`.
- supporting workflows split commitlint, dependency review, and CodeQL.
- root `package.json`, `package-lock.json`, commitlint config, release config, and local validation scripts support release automation.

Hexalith.Parties still used a bespoke `.github/workflows/test.yml` and a separate `.github/workflows/publish-parties-containers.yml`. That duplicated shared build logic, kept release orchestration split from semantic-release, and left package/release support below the Tenants baseline.

## 2. Impact Analysis

Epic impact:

- Epics 1-5 remain complete and unaffected.
- Epic 8 maintenance/release hygiene is affected because this changes CI/CD and package/release automation.
- No PRD functional requirement coverage changes.

Story impact:

- Existing CI publication guard tests required update because they pinned the retired standalone publish workflow.
- No new story was added; this is a direct infrastructure adjustment requested by the user.

Artifact conflicts:

- `docs/ci.md`, `docs/build-gate.md`, `docs/architecture.md`, `docs/source-tree-analysis.md`, and `docs/ci-secrets-checklist.md` referenced the retired workflow shape and needed updates.
- `scripts/check-no-warning-override.sh` had a stale workflow comment.
- The local Parties container publish helper still referenced old Zot secret names.

Technical impact:

- Release now requires `NUGET_API_KEY`, `HEXALITH_ZOT_USERNAME`, and `HEXALITH_ZOT_API_KEY`.
- Release publishes exactly `parties`, `parties-mcp`, and `parties-ui` containers through the shared release publisher.
- Coverage is not enabled in `ci.yml` because the project still documents the local coverage lane as blocked under the current Microsoft.Testing.Platform/xUnit v3 setup.

## 3. Recommended Approach

Selected path: Direct Adjustment.

Rationale:

- The change is infrastructure/release hygiene, not product scope.
- Tenants provides the target pattern.
- The existing Parties test/docs guardrails can be updated directly.
- Keeping `rc-gate.yml` preserves Parties-specific root gitlink governance.

Effort: Medium.

Risk: Medium. The main risks are release-secret configuration, package-mode dependency availability, and accidental duplicate container publication. The implementation mitigates these by using shared Hexalith.Builds release publishing, validating package-only consumers, and deleting the duplicate standalone container workflow.

## 4. Detailed Change Proposals

### Workflows

OLD:

- `.github/workflows/test.yml` implemented a bespoke lint/test/ui-a11y/contract/report workflow.
- `.github/workflows/publish-parties-containers.yml` separately published Parties containers.

NEW:

- `.github/workflows/ci.yml` delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main`.
- `.github/workflows/release.yml` delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main`.
- `.github/workflows/codeql.yml`, `commitlint.yml`, and `dependency-review.yml` match the Tenants split-workflow shape.
- `.github/dependabot.yml` adds NuGet, npm, and GitHub Actions dependency update coverage.
- `rc-gate.yml` remains unchanged for Parties release-candidate gitlink governance.

### Release Support

OLD:

- No root npm release metadata existed.
- No release package pack/validate scripts existed.

NEW:

- `package.json`, `package-lock.json`, `commitlint.config.mjs`, and `release.config.cjs` support semantic-release.
- `scripts/pack-release-packages.py` packs the Parties package set.
- `scripts/validate-nuget-packages.py` validates package metadata and forbidden dependency boundaries.
- `scripts/validate-consumer-package-references.py` builds isolated package-only consumers, including a temporary local support feed for unpublished Hexalith support packages.
- `scripts/validate-release-secrets.sh` fails closed if NuGet or Zot release secrets are missing.

### Tests And Docs

OLD:

- `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs` pinned the standalone publish workflow.
- Active docs referenced `test.yml`, the standalone publish workflow, and `ZOT_REGISTRY_*` secret names.

NEW:

- CI tests pin the shared CI/release workflow contract, release support files, and `HEXALITH_ZOT_*` secret names.
- Active docs describe shared CI/CD, semantic-release, package validation, and release-owned container publishing.

## 5. Implementation Handoff

Scope classification: Moderate.

Implemented by: Developer agent.

Success criteria:

- Workflow YAML parses.
- Build-gate script reports no warning override or recursive-submodule regressions.
- Focused CI guard tests pass.
- Full Release build passes with zero warnings.
- Release package scripts pack and validate the Parties package set.
- Package-only consumer validation passes.

Checklist status:

- 1.1 Triggering story: N/A; direct CI/CD parity request.
- 1.2 Core problem: Done; bespoke Parties CI/CD diverged from Tenants shared workflow model.
- 1.3 Evidence: Done; compared Tenants workflows, Parties workflows, package support files, and docs/tests.
- 2.1-2.5 Epic impact: Done; Epic 8 maintenance/release hygiene only.
- 3.1 PRD conflict: N/A; no PRD functional scope change.
- 3.2 Architecture conflict: Done; CI/CD docs and architecture references updated.
- 3.3 UX conflict: N/A; no UX behavior change.
- 3.4 Other artifacts: Done; workflows, scripts, tests, docs, npm release files updated.
- 4.1 Direct adjustment: Viable and selected; medium effort, medium risk.
- 4.2 Rollback: Not selected; revert workflow/release support files if needed.
- 4.3 MVP review: Not applicable.
- 5.1-5.5 Proposal components and handoff: Done.
- 6.1-6.5 Final review/handoff: Done; implementation evidence recorded in the agent response.

## 6. Validation Evidence

Commands run:

- `npm install --package-lock-only`
- `python3 -m py_compile scripts/pack-release-packages.py scripts/validate-nuget-packages.py scripts/validate-consumer-package-references.py`
- `bash -n scripts/validate-release-secrets.sh scripts/check-no-warning-override.sh`
- YAML parse of all `.github/workflows/*.yml`
- `bash scripts/check-no-warning-override.sh`
- `dotnet test tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Release -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`
- `npm ci --ignore-scripts`
- `dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`
- `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`
- `python3 scripts/pack-release-packages.py ./nupkgs 0.0.0-ci-test`
- `python3 scripts/validate-nuget-packages.py ./nupkgs`
- `python3 scripts/validate-consumer-package-references.py ./nupkgs`
- `pwsh -NoProfile -Command "& { . ./scripts/publish-parties-containers.ps1 -DryRun -ImageTag 1.2.3 }"`

Result: all commands passed.
