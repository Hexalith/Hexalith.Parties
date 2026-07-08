---
project_name: parties
user_name: Administrator
date: 2026-07-08
scope_classification: Moderate
status: approved-implemented
mode: Batch
trigger: "Remove retired deployment references now that Parties container publication is handled by GitHub Actions and runtime deployment is external."
---

# Sprint Change Proposal - Retire legacy deployment artifacts

## 1. Issue Summary

The repository still carried the previous in-repo deployment path:

- Kubernetes manifests and publish/teardown scripts.
- Production Dapr component manifests.
- Zot registry manifests.
- Static deployment validation tests and fixtures.
- User documentation that described the retired operator workflow.

The new operating model is narrower and clearer:

- GitHub Actions publishes only `parties`, `parties-mcp`, and `parties-ui` containers to Zot.
- Runtime deployment orchestration is owned outside this repository.
- This repository keeps source code, local Aspire/Dapr development topology, CI, and container publication support.

## 2. Impact Analysis

### Epic Impact

Epic 8 is the correct owner because this is maintenance/refactoring scope with no PRD functional change.

Story 8.12 remains the container-publication story. This change adds Story 8.13 to remove the retired deployment surface and update active docs/tests.

### Artifact Impact

Removed:

- `deploy/`
- the retired static deployment-validation test project and fixtures.
- the retired Kubernetes topology and deployment security checklist documents.

Replaced:

- `tests/Hexalith.Parties.Ci.Tests/` now validates the GitHub Actions Zot publication contract.
- `docs/deployment-guide.md` now documents the runtime ownership boundary instead of an in-repo cluster workflow.

Updated:

- `.slnx`, `scripts/test.ps1`, and `.github/workflows/test.yml` now use a `ci` test lane instead of the retired deployment-validation project.
- Active docs no longer point to retired Kubernetes, Dapr, Zot, or deployment validator paths.

### Non-Goals

- No runtime orchestrator implementation in this repository.
- No change to local Aspire/Dapr development topology.
- No deletion of historical BMAD story records whose purpose is audit trail.

## 3. Recommended Approach

Proceed with the deletion and documentation update in one direct `main` push, because the product owner approved deleting old deployment artifacts and explicitly requested direct push without PR.

Validation should prove:

- Active repository files no longer reference the retired deployment paths.
- The new CI test lane builds and passes.
- The container publish dry-run still resolves the Parties-only image set.
- No registry credentials were committed.

## 4. Checklist Assessment

- Trigger and context: complete.
- Epic/story impact: complete.
- Artifact conflict review: complete.
- Path forward: direct implementation approved.
- Acceptance alignment: complete.
- Final validation: complete for local checks and Zot publication; GitHub Test Pipeline remains blocked by the pre-existing package-mode restore gap for unpublished Hexalith packages.

## 5. Resolution

Approved and implemented as Story 8.13.

Validation evidence:

- Active-file sweep for retired deployment paths: no matches.
- Registry API-key prefix scan: no matches.
- `git diff --check`: passed.
- `scripts/test.ps1 -Lane ci -Configuration Release`: passed, 3 tests.
- `Hexalith.Parties.Sample.Tests`: passed in source-mode, 58 tests.
- `Hexalith.Parties.UI.Tests`: passed in source-mode, 324 tests.
- `scripts/publish-parties-containers.ps1 -DryRun`: passed after rebase, tag `0.0.0-preview.0.667`.
- GitHub Actions `Publish Parties Containers` run `28947711733`: passed and published tag `0.0.0-preview.0.667`.
- Zot manifest HEAD checks for `parties`, `parties-mcp`, and `parties-ui` at `0.0.0-preview.0.667`: all returned `HTTP 200`.
