---
title: Sprint Change Proposal — Validation Fallback Ladder → Durable Guidance and Runner Support
date: 2026-07-07
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
trigger: >
  epic-7-retro-2026-07-07.md §"Action Items" QA row (Murat): "Turn the validation
  fallback ladder into durable tooling guidance and runner support, including
  continue-after-failure reporting and inspectable result output." Matures the
  Epic 6 retro QA action ("Document the standard fallback validation ladder").
status: approved
approved: 2026-07-07T20:56:15+02:00
related:
  - _bmad-output/implementation-artifacts/epic-7-retro-2026-07-07.md
  - _bmad-output/implementation-artifacts/epic-6-retro-2026-07-07.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md
  - _bmad-output/implementation-artifacts/spec-8-11-validation-fallback-ladder-runner-and-guidance.md
  - scripts/test.ps1
  - .github/workflows/test.yml
---

# Sprint Change Proposal — Validation Fallback Ladder → Durable Guidance and Runner Support

## 1. Issue Summary

**Problem.** Across Epics 6 and 7, story validation was repeatedly recorded as
"blocked" when the repo's known fallback ladder could in fact produce focused
evidence, and a first-project failure could mask later failures. The Epic 6
retrospective committed to *documenting* the ladder (direct xUnit v3 assemblies,
serialized `-m:1` builds, focused per-project builds). Story 8.1 delivered most
of that documentation, but the Epic 7 retrospective found the follow-through only
**"In progress"** and raised the bar:

> **QA (Murat):** "Turn the validation fallback ladder into durable tooling
> guidance and runner support, including continue-after-failure reporting and
> inspectable result output."
> **Success criteria:** "Validation records show every focused lane attempted,
> every broad blocker classified, and no false green from an omitted project."

**Discovery & evidence.** The gap was already captured as two concrete
deferred-work entries sourced from `spec-8-1`:

1. *"Add a lane-runner mode that continues after failed projects and reports every
   failing project in one run."* — `scripts/test.ps1 -Lane all` and each CI shard
   stop at the first failing project, so a package-mode restore blocker can hide
   later project-specific failures (**false-green-by-omission** risk).
2. *"Add inspectable local test result output and optional build/restore property
   forwarding to `scripts/test.ps1`."* — CI writes TRX/coverage artifacts, but the
   local runner exposes neither a results-directory/logger option nor a safe
   property-forwarding interface for blockers that need e.g.
   `UseHexalithProjectReferences=true`.

Confirmed in code: `scripts/test.ps1` `Invoke-TestProject` `throw`s on the first
non-zero exit; the CI `Run test shard` step is a `set -euo pipefail` loop that
aborts the shard on the first failing `dotnet test`.

## 2. Impact Analysis

- **Epic Impact.** Epic 8 (Domain-Focus Refactoring, Class C maintenance,
  `in-progress`) gains one QA/tooling story (**8.11**). No rescoping of 8.1–8.10.
  The change is independent of the 8.6–8.10 deletion sequence and the Story 8.3
  platform-prerequisite gate — it can land at any time and improves the validation
  evidence quality those gated stories will produce.
- **Story Impact.** New Story **8.11**; the two `spec-8-1` deferred-work runner
  entries are resolved by it. No existing story is reopened.
- **Artifact Conflicts.**
  - **PRD:** none — Epics 7/8 are explicitly maintenance scope with no new
    functional coverage.
  - **Architecture:** none — the Epic 8 architecture spine is unchanged; runner/CI
    are outside the domain boundary.
  - **UI/UX:** none.
  - **Tooling/docs/CI (the whole change):** `scripts/test.ps1`,
    `.github/workflows/test.yml`, `docs/development-guide.md`, `docs/ci.md`,
    `tests/README.md`, `deferred-work.md`, `sprint-status.yaml`.
- **Technical Impact.** Runner changes are **additive and opt-in**; the default
  lane behavior stays fail-fast, so no existing caller (local or CI-parity command)
  changes meaning. CI keeps TRX + coverage + artifact upload and the same 15-project
  inventory; the inventory-lint guard still passes.
- **Out of scope / upstream follow-up.** The shared
  `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` also carries testing
  rules but is a submodule (upstream-owned). Mirroring the ladder guidance there is
  routed as a follow-up, not performed in this repo.

## 3. Recommended Approach

**Selected path: Direct Adjustment (Option 1).** Add Story 8.11 and implement the
runner + CI + docs changes additively.

- **Rollback (Option 2):** rejected — nothing to revert; the documented ladder is
  correct, only under-tooled.
- **MVP Review (Option 3):** N/A — MVP unaffected.

**Rationale.** Lowest-risk path that directly satisfies the retro success criteria:
opt-in continue-after-failure guarantees *every focused lane is attempted* and *no
project is omitted because an earlier one failed*; inspectable TRX output plus
property forwarding lets local validation reproduce CI evidence and record *every
broad blocker* precisely. Effort **Low–Medium**, risk **Low** (defaults preserved,
inventory guard preserved).

## 4. Detailed Change Proposals

### 4.1 `scripts/test.ps1` — continue-after-failure + inspectable output + property forwarding

**Before:** `Invoke-TestProject` throws on the first non-zero exit; lanes stop at
the first failing project; no results-directory/logger; no property forwarding.

**After:** three opt-in params, results aggregated into a per-run summary.

```
NEW params:
  [switch]   $ContinueOnFailure   # run every project, report all failures, exit 1 if any failed
  [string]   $ResultsDirectory    # per-project TRX (repo-relative); local parity with CI
  [string[]] $Properties          # each forwarded as -p:<value> to dotnet test

Invoke-TestProject: now returns the project exit code (adds --logger/--results-directory
  when -ResultsDirectory set, and -p:<value> for each -Properties entry).
Lane loop: collects {Project, ExitCode, Passed}; without -ContinueOnFailure it throws on
  the first failure exactly as before; with it, runs all, prints a PASS/FAIL summary table,
  and exit 1 if any project failed. Inventory guard (Assert-TestProjectInventory) unchanged.
```

### 4.2 `.github/workflows/test.yml` (`Run test shard`) — within-shard continue-after-failure

**Before:** `set -euo pipefail` loop; `dotnet test` per project; first failure aborts the shard.

**After:** each project's result captured; PASS/FAIL row appended to
`$GITHUB_STEP_SUMMARY`; `::error::` annotation per failing project; the step fails
at the end if any project failed. TRX, coverage, and artifact upload unchanged;
matrix `fail-fast: false` still keeps shards independent.

### 4.3 Durable docs

- **`docs/development-guide.md` §4** — new **"Fallback validation ladder"** callout
  naming the five rungs (focused lane → direct xUnit v3 assembly filter → serialized
  `-m:1` triage → property pins → record exact broad blocker), plus documented
  `-ContinueOnFailure` / `-ResultsDirectory` / `-Properties` usage.
- **`docs/ci.md`** — within-shard continue-after-failure behavior and the local
  parity command (`-ContinueOnFailure -ResultsDirectory TestResults`).
- **`tests/README.md`** — the three flags surfaced in the Running Tests section.

### 4.4 Ledger + tracking

- **`deferred-work.md`** — the two `spec-8-1` runner entries marked
  `status: resolved` with a `resolved_by: Story 8-11` note (kept for audit).
- **`sprint-status.yaml`** — add `8-11-validation-fallback-ladder-runner-and-guidance: done`;
  flip the Epic 6 ("Document the standard fallback validation ladder") and Epic 7
  ("Turn the validation fallback ladder …") action items to `done`.
- **`spec-8-11-…md`** — new story spec (intent contract, I/O matrix, acceptance
  criteria, Auto Run Result) authored by correct-course and implemented in-session.

## 5. Implementation Handoff

**Scope classification: Moderate** — a new backlog story plus deferred-work
reconciliation. Per the approved session decision, it was **implemented directly in
this session** (Developer + Test Architect lens).

**Verification performed (this session):**

| Check | Result |
|-------|--------|
| `scripts/test.ps1` PowerShell parse | PASS |
| CI/local inventory guard vs `test.ps1` + `test.yml` (15 projects) | PASS, no drift/dupes |
| Runner `-ContinueOnFailure` (2 seeded failures, shimmed `dotnet`) | Ran all 15, full PASS/FAIL summary, exit 1 |
| Runner default fail-fast | Stopped at first failing project, threw |
| Runner `-ResultsDirectory` + `-Properties` | Results dir created; `--logger`/`--results-directory`/`-p:` forwarded |
| CI shard step (extracted, shimmed `dotnet`) | Continued past failure, summary table, exit 1; `bash -n` + `yaml.safe_load` pass |

Real test suites were **not** executed: local package-mode restore is a recorded
Epic 7 release blocker, so behavior was proven with a `dotnet test` shim. First
real-environment run of `scripts/test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults`
should be recorded in `tests/test-summary.md` when a package-enabled environment is available.

**Success criteria mapping (Epic 7 retro):**

- *"every focused lane attempted"* → `-ContinueOnFailure` (local) + within-shard
  continue (CI) run every project.
- *"every broad blocker classified"* → ladder rung 5 in `docs/development-guide.md`;
  inspectable TRX via `-ResultsDirectory`.
- *"no false green from an omitted project"* → aggregate summary + non-zero exit on
  any failure; inventory guard prevents a silently dropped project.

**Residual (recorded, not resolved by this story):** full solution build /
package-mode restore, UI accessibility, deploy validation, and root gitlink drift
remain the Epic 7 release-candidate blockers. Upstream follow-up: mirror the ladder
guidance into `hexalith-llm-instructions.md` (submodule owner).
