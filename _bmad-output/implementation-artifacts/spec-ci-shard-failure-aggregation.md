---
title: 'CI shard failure aggregation'
type: 'bugfix'
created: '2026-09-06'
status: 'in-review'
baseline_commit: '904b0ca928616130ec0092983c3c0c346c8fc2e1'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: '311b141f70ccc24376b2bc4bbfe5ad1a462c21a3'
context:
  - '{project-root}/references/Hexalith.Builds/DEVELOPMENT.md'
  - '{project-root}/references/Hexalith.Builds/.github/workflows/ci-cd-standards.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The reusable domain CI workflow runs test projects sequentially under `set -e`, so one failed project hides later project results and an earlier failed tier prevents the next tier from running.

**Approach:** Aggregate per-project failures in every shared Tier 1/Tier 2 loop, publish complete GitHub step summaries, and defer the blocking exit to one final test gate after all configured projects have run.

## Boundaries & Constraints

**Always:** Cover both VSTest and Microsoft.Testing.Platform paths; retain per-project TRX and coverage arguments; annotate each failure; return nonzero when any configured project fails; keep the consuming Parties workflow inputs and inventory unchanged.

**Never:** Do not edit the deferred-work ledger, release-workflow test loops, coverage thresholds, package/dependency pins, submodule revision, or `scripts/test.ps1`; its opt-in `-ContinueOnFailure` summary and default fail-fast mode are read-only compatibility requirements.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| All projects pass | Multiple configured projects in the selected platform's Tier 1 and Tier 2 lists | Every project runs and each tier appends a complete PASS table to the GitHub summary | Final test gate succeeds |
| Early project fails | First project exits nonzero and later projects remain | The failed project is annotated, every later project and tier still runs, and summaries contain every attempted project | Final test gate returns nonzero after all loops finish |
| Multiple projects fail | Failures occur across one or both tiers | Every failure and exit code appears in its tier summary | Final gate aggregates failure counts and returns nonzero |
| Empty or inactive path | A project list is empty or the alternate test platform is selected | Inactive steps remain skipped and do not create false failures | Final gate ignores absent outputs |

</intent-contract>

## Code Map

- `references/Hexalith.Builds/.github/workflows/domain-ci.yml:354` -- four Tier 1/Tier 2 project loops currently abort on the first `dotnet test` failure; the coverage gate at line 436 and always-run evidence upload at line 458 must retain their behavior.
- `references/Hexalith.Builds/Tools/test-domain-workflow-test-platforms.ps1:72` -- existing named-step extractor and workflow contract harness can validate all four loop bodies plus the final gate, including mutation-resistant runtime checks with a shimmed `dotnet`.
- `references/Hexalith.Builds/.github/workflows/build-release.yml:69` -- already executes the domain-workflow contract harness in Builds CI; no new workflow registration is needed.
- `references/Hexalith.Builds/.github/workflows/domain-ci.md` -- user-facing reusable-workflow behavior; document aggregate shard execution and final failure semantics.
- `.github/workflows/ci.yml:18` -- Parties consumes `domain-ci.yml@main` and supplies the Tier 1/Tier 2 project lists; read-only outer surface.
- `scripts/test.ps1:143` -- local runner already implements opt-in continuation and default fail-fast behavior; read-only regression surface.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds/Tools/test-domain-workflow-test-platforms.ps1` -- add contracts and shimmed execution checks for all four shard loops, inactive outputs, complete summaries, and the final nonzero gate -- prevent a static-only false green.
- [x] `references/Hexalith.Builds/.github/workflows/domain-ci.yml` -- collect per-project outcomes in every Tier 1/Tier 2 loop, publish PASS/FAIL tables and output counts, then add an always-run aggregate failure gate after coverage validation -- allow all configured projects to execute before the job fails.
- [x] `references/Hexalith.Builds/.github/workflows/domain-ci.md` -- describe failure-continuing shard execution, summary evidence, and final blocking behavior -- keep shared CI guidance aligned with the workflow contract.

**Acceptance Criteria:**
- Given a Parties CI run with multiple Tier 1 and Tier 2 projects, when any non-final project fails, then all later configured projects run and all attempted outcomes appear in GitHub step summaries before the job returns nonzero.
- Given either supported test platform, when test shards execute, then the same aggregate behavior applies without changing that platform's TRX or coverage arguments.
- Given `scripts/test.ps1` without `-ContinueOnFailure`, when a local project fails, then it remains fail-fast; given the switch, it retains its existing complete summary and nonzero exit behavior.

## Spec Change Log

## Review Triage Log

## Design Notes

Individual test steps finish successfully after recording expected test-process failures so later tiers remain eligible. They expose numeric failure counts through step outputs; one `if: always()` gate validates and sums active outputs, emits a workflow error, and exits 1 only after coverage validation and all configured shard loops have had an opportunity to run. Unexpected shell/infrastructure errors continue to fail their own step.

## Verification

**Commands:**
- `pwsh -NoProfile -File ./Tools/test-domain-workflow-test-platforms.ps1` from `references/Hexalith.Builds` -- expected: all structural and shimmed aggregation contracts pass for four loop variants and the final gate.
- `bash -n` against each extracted `run: |` shard and aggregate-gate body -- expected: every generated shell body parses.
- `git diff --check` in both owning repositories -- expected: no whitespace errors.
- PowerShell parser check for `scripts/test.ps1` plus a content diff -- expected: clean parse and no local-runner changes.
