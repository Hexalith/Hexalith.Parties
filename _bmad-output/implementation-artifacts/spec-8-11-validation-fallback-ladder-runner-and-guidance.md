---
title: '8.11 Validation fallback ladder — durable tooling guidance and runner support'
type: 'chore'
created: '2026-07-07T20:56:15+02:00'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
provenance: 'bmad-correct-course (sprint-change-proposal-2026-07-07-validation-ladder-runner.md); implemented in-session, not bmad-dev-auto'
baseline_revision: '24e9f41902e6c87658bd55a7d5307978e65ec997'
final_revision: 'pending-commit'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-7-retro-2026-07-07.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-retro-2026-07-07.md'
  - '{project-root}/_bmad-output/implementation-artifacts/deferred-work.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** The repo's fallback validation ladder was documented (Story 8.1) but not enforced by the runner. `scripts/test.ps1 -Lane all` and each CI shard stopped at the first failing project, so a package-mode restore blocker could hide later project-specific failures (false-green-by-omission). The local runner also emitted no inspectable results and could not forward the build/restore properties some blockers require, so validation records could not prove "every focused lane attempted, every broad blocker classified." This closes the Epic 7 retrospective QA action item and the two `spec-8-1`-sourced deferred-work runner entries.

**Approach:** Add opt-in continue-after-failure, inspectable TRX output, and safe property forwarding to the local lane runner; extend the same continue-after-failure guarantee within each CI shard with a per-project PASS/FAIL summary; and name the ladder explicitly in the durable docs. All runner changes are additive — default behavior stays fail-fast.

## Boundaries & Constraints

**Always:** Keep the default lane behavior fail-fast (no `-ContinueOnFailure`); keep the CI/local test-project inventory guard valid; keep `.slnx` for restore/build only; keep TRX + coverage + artifact upload on CI; preserve per-project execution.

**Block If:** A change would require editing submodule contents (the shared `hexalith-llm-instructions.md` testing-rules guidance is upstream-owned and out of scope — routed as a follow-up), weakening a gate, or changing public product behavior.

**Never:** Do not make continue-after-failure the default (would mask a broken lane as "ran"); do not suppress a failing project to reach green; do not add solution-level `dotnet test`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Continue after failure (local) | `scripts/test.ps1 -Lane all -ContinueOnFailure` | Runs every project, prints a PASS/FAIL summary listing all projects, exits 1 if any failed | No project is skipped because an earlier one failed |
| Default fail-fast (local) | `scripts/test.ps1 -Lane all` | Stops at the first failing project and throws, as before | Behavior unchanged from baseline |
| Inspectable output (local) | `-ResultsDirectory TestResults` | Emits `<Project>.trx` per project under the given repo-relative path | Directory is created if absent |
| Property forwarding (local) | `-Properties MinVerVersionOverride=1.0.0,NuGetAudit=false` | Each value forwarded as `-p:<value>` to `dotnet test` | Empty/omitted `-Properties` forwards nothing |
| Continue after failure (CI) | `Run test shard` step | Continues past a failing project, writes a PASS/FAIL row per project to the step summary, fails the step at the end if any failed | `fail-fast: false` keeps shards independent; the loop keeps within-shard projects independent too |

</intent-contract>

## Code Map

- `scripts/test.ps1` — local lane runner; add `-ContinueOnFailure`, `-ResultsDirectory`, `-Properties`; aggregate per-project results and print a summary; preserve inventory guard and fail-fast default.
- `.github/workflows/test.yml` (`Run test shard`) — continue past a failing project; per-project PASS/FAIL step-summary table; fail the step at the end if any project failed.
- `docs/development-guide.md` — name the fallback validation ladder (5 rungs) and document the new runner flags.
- `docs/ci.md` — document within-shard continue-after-failure and the local-parity command.
- `tests/README.md` — surface `-ContinueOnFailure` / `-ResultsDirectory` / `-Properties`.
- `_bmad-output/implementation-artifacts/deferred-work.md` — mark the two `spec-8-1` runner entries resolved.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — add the Story 8-11 row and flip the Epic 6/7 ladder action items to done.

## Tasks & Acceptance

**Execution:**
- [x] `scripts/test.ps1` — add `-ContinueOnFailure` (run all, summary, exit 1 on any failure), `-ResultsDirectory` (per-project TRX), and `-Properties` (`-p:` forwarding); keep the four lane arrays, `Assert-TestProjectInventory`, and fail-fast default intact.
- [x] `.github/workflows/test.yml` — extend the shard loop to continue after a failing project, summarize every project, and fail the step at the end if any failed; keep TRX/coverage/artifact upload and the project inventory unchanged.
- [x] `docs/development-guide.md`, `docs/ci.md`, `tests/README.md` — name the ladder and document the new flags.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — resolve the two runner deferred-work entries with a note pointing at this story.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — add `8-11-...: done` and set the Epic 6 ("Document the standard fallback validation ladder") and Epic 7 ("Turn the validation fallback ladder into durable tooling guidance and runner support") action items to done.

**Acceptance Criteria:**
- Given the local lane runner with `-ContinueOnFailure`, when a lane contains failing projects, then every project runs, a PASS/FAIL summary lists all of them, and the run exits non-zero — no project is omitted because an earlier one failed.
- Given the local lane runner without `-ContinueOnFailure`, when a project fails, then the lane stops at that project and throws (baseline fail-fast preserved).
- Given `-ResultsDirectory`, when a lane runs, then each project writes an inspectable `<Project>.trx` under the given path.
- Given `-Properties k=v,k2=v2`, when a project runs, then each value is forwarded to `dotnet test` as `-p:<value>`.
- Given a CI shard with a failing project, when the shard runs, then it continues to the remaining projects, records a PASS/FAIL row per project in the step summary, and fails the step at the end.
- Given the CI/local inventory guard, when it runs against the modified `scripts/test.ps1` and `.github/workflows/test.yml`, then it still reports all 15 test projects with no drift.

## Auto Run Result

Implemented and verified in-session (bmad-correct-course, 2026-07-07):

- PowerShell parse check on `scripts/test.ps1` — **PASS**.
- CI/local test-project inventory guard (the `lint` job's Python check) against `scripts/test.ps1` and `.github/workflows/test.yml` — **PASS** (15 projects, no drift, no duplicates).
- Behavioral verification with a `dotnet test` shim (real tests not run — local package-mode restore is a recorded release blocker):
  - `-Lane all -ContinueOnFailure` with two seeded failures → ran all 15 projects, printed the full PASS/FAIL summary, exited 1.
  - `-Lane all` (default) → stopped at the 4th project (first seeded failure) and threw — fail-fast preserved.
  - `-Lane deploy -ResultsDirectory TestResults/verify -Properties …` → created the results directory and forwarded `--logger trx;…`, `--results-directory`, and `-p:<value>` arguments.
- CI shard step extracted and run against the shim → continued past the failing project, wrote the PASS/FAIL summary table, exited 1. `bash -n` syntax check passed; `yaml.safe_load` of `test.yml` passed.

Residual (unchanged by this story, recorded not resolved): full solution build / package-mode restore, UI accessibility, deploy validation, and root gitlink drift remain the Epic 7 release-candidate blockers. Upstream follow-up: mirror the ladder guidance into the shared `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` testing rules (submodule-owned).

## Spec Change Log

- 2026-07-07 — Created by bmad-correct-course from the Epic 7 retrospective QA action item; implemented in the same session.

## Review Triage Log
