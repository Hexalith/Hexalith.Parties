---
title: 'CI shard failure aggregation'
type: 'bugfix'
created: '2026-09-06'
status: 'done'
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

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind 1: nonzero `dotnet test` can hide infrastructure failure | medium | patch — a nonzero result is now aggregated only when its expected TRX exists; missing evidence fails that shard step immediately. |
| Blind 2: an active shard can have an absent output | medium | patch — the final gate now checks the selected platform and configured tier inputs and rejects a missing active count. |
| Blind 3: step conditions and input mappings were not protected | medium | patch — each shard contract now asserts its exact platform/input condition and project environment mapping. |
| Blind 4: no Tier 1-to-Tier 2-to-final-gate runtime sequence | medium | patch — legal VSTest and MTP sequences now prove Tier 2 runs after a Tier 1 test failure and the final gate blocks afterward. |
| Blind 5: aggregate fixture mixed VSTest and MTP outputs | medium | patch — aggregate fixtures now use legal same-platform pairs, supplemented by one isolated runtime case per output channel. |
| Blind 6: inactive-path test did not protect GitHub step selection | medium | patch — exact `if:` contracts protect all four selection paths and the empty-output gate runtime remains covered. |
| Blind 7: failure-count assertions allowed substring matches | low | patch — outputs must now match one exact newline-terminated assignment. |
| Blind 8: summary assertions did not bind status to project order | low | patch — tests compare the exact ordered row sequence for failure and passing scenarios. |
| Blind 9: summary and annotation values were not escaped | low | patch — Markdown metacharacters and workflow-command percent data are escaped and exercised by metacharacter fixtures. |
| Blind 10: evidence-upload preservation was weakly tested | medium | patch — the harness now protects `if: always()`, artifact name, TRX glob, and coverage glob. |
| Blind 11: the diff contains deferred-ledger edits | false | rejected — the ledger was already modified before this implementation began; it was neither edited nor reverted here, preserving concurrent user work and the spec boundary. |
| Edge 1: nonzero without test results is misreported | medium | patch — the expected TRX guard distinguishes aggregatable test failures from evidence-less infrastructure failures. |
| Edge 2: a nonempty input made only of blank lines succeeds | false | rejected — exact empty inputs skip the step as specified; whitespace-only nonempty text is not a configured project and the matrix requires no false failure for an empty list. |
| Edge 3: a whitespace-only project line is attempted | false | rejected — such a line is invalid caller data rather than a project; it cannot hide a configured result and would fail visibly. |
| Edge 4: duplicate basenames overwrite evidence | medium | rejected as outside this intent — this is pre-existing result-path behavior, while the approved intent explicitly requires retaining per-project TRX arguments and caller inventory. |
| Edge 5: Markdown metacharacters corrupt summary rows | low | patch — paths are escaped before table emission and exact-row tests include pipe and backtick characters. |
| Edge 6: leading-zero counts use octal arithmetic | medium | patch — validated digit strings are now converted with an explicit base-10 prefix and `08` is covered at runtime. |
| Edge 7: failure-count addition can overflow | false | rejected — counts are emitted internally from bounded workflow-call project lists, whose maximum cardinality is far below Bash integer range. |
| Edge 8: output assertions accepted expected substrings | low | patch — exact assignment matching now rejects duplicate or overwritten output values. |
| Verification 1: two output channels lacked behavioral coverage | medium | patch — isolated nonzero runtime cases now cover unit/integration for VSTest and MTP independently. |
| Verification 2: deferred-work was modified against the boundary | false | rejected — the modification predates this implementation and belongs to concurrent outer-repository work; this task did not touch the ledger. |

| Resumed Blind 1: stale TRX accepted after a basename collision | medium | patch — the new evidence guard can accept the previous invocation's report. Remove only the expected TRX before each invocation and exercise a passing same-basename project followed by an evidence-less failure; retain all result arguments. |
| Resumed Blind 2: MTP infrastructure exit 10 with a report is deferred | medium | patch — [MTP documents exit 10 as infrastructure failure](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting). Recognize it in the two MTP shards even when a fresh TRX exists, retaining immediate infrastructure failure and summary evidence. |
| Resumed Blind 3: infrastructure failure omits its summary row | medium | patch — all four evidence-less branches exit before the row append. Emit the attempted project's FAIL row before that exit and assert its exact contents. |
| Resumed Blind 4: result-file annotation text is unescaped | low | patch — a literal percent escape in the basename remains raw in the report path. Escape the interpolated report path and verify the complete workflow-command message. |
| Resumed Blind 5: swapped final-gate mappings pass the harness | medium | patch — mutations swapping output references pass the 169 existing checks because runtime environments are constructed independently. Bind each variable to its exact reference and resolve actual mappings in passing single-tier runtime cases. |
| Resumed Blind 6: sequence checks ignore deployed step order | medium | patch — moving integration before its Dapr prerequisite still passes the current harness. Assert the actual unit, Dapr, integration, coverage, and final-gate order for both platforms. |
| Resumed Blind 7: contract subprocess wait is unbounded | low | rejected — the shipped bodies terminate for finite caller lists, and the workflow already bounds the job. A hypothetical loop regression is uncommon; adding timeout policy and process-tree lifecycle handling exceeds a direct correction. |
| Resumed Edge 1: stale same-basename report hides infrastructure failure | medium | patch — same root cause as Resumed Blind 1; remove the expected file before each invocation without changing result-path arguments. |
| Resumed Edge 2: evidence-less failure has no summary row | medium | patch — same root cause as Resumed Blind 3; preserve the immediate exit while recording the attempted outcome. |
| Resumed Edge 3: corrupted huge numeric output overflows | false | carried — the earlier Edge 7 verdict applies: emitted counts come from bounded configured project lists and cannot reach Bash integer overflow. The synthetic corrupted value is not produced by a shard. |
| Resumed Edge 4: infrastructure failure prevents later projects | false | rejected — the captured intent's Design Notes and existing infrastructure fixtures explicitly require unexpected infrastructure errors to fail their own step; the continuation contract applies to aggregatable test-process failures. |
| Resumed Verification 1: actual output mappings lack single-tier coverage | medium | patch — pre-verified swapped Tier 1/Tier 2 mappings leave all 169 checks green but reject a passing unit-only CI run. Resolve the gate's actual references against shard outputs in single-tier runtime fixtures. |
| Resumed Verification 2: infrastructure summary is untested and incomplete | medium | patch — the reviewer ran the existing infrastructure fixture and observed only a heading. Add the missing row and exact-row assertions for all four shards. |

## Design Notes

Individual test steps finish successfully after recording expected test-process failures so later tiers remain eligible. They expose numeric failure counts through step outputs; one `if: always()` gate validates and sums active outputs, emits a workflow error, and exits 1 only after coverage validation and all configured shard loops have had an opportunity to run. Unexpected shell/infrastructure errors continue to fail their own step.

## Verification

**Commands:**
- `pwsh -NoProfile -File ./Tools/test-domain-workflow-test-platforms.ps1` from `references/Hexalith.Builds` -- expected: all structural and shimmed aggregation contracts pass for four loop variants and the final gate.
- `bash -n` against each extracted `run: |` shard and aggregate-gate body -- expected: every generated shell body parses.
- `git diff --check` in both owning repositories -- expected: no whitespace errors.
- PowerShell parser check for `scripts/test.ps1` plus a content diff -- expected: clean parse and no local-runner changes.

### Resumed review verification — 2026-10-08

The shared aggregation implementation was already committed in Builds before
this run. Three independent review layers were completed, all 13 new findings
were triaged above, and the accepted corrections were applied. Nothing was
added to the deferred-work ledger.

- `pwsh -NoProfile -File ./Tools/test-domain-workflow-test-platforms.ps1`
  from `references/Hexalith.Builds` — exit 0, **207 assertions passed**
  (169 before the review corrections).
- `bash -n` on each extracted literal Tier 1/Tier 2 body and the aggregate
  gate — all five passed after the corrections.
- Seven temporary workflow mutations — all returned exit 1 from the harness:
  swapped platform outputs, swapped tier outputs, integration before Dapr,
  removed TRX cleanup, removed infrastructure summary row, unescaped report
  annotation path, and removed MTP infrastructure-exit guard.
- `scripts/test.ps1` PowerShell parser — no parse errors. A shimmed local unit
  lane stopped after one failed project by default (exit 1); with
  `-ContinueOnFailure`, all 11 projects ran and the complete summary ended
  with exit 1; an all-passing continued run executed 11 projects and exited 0.
- Builds `git diff --check` and Parties
  `git diff --check -- _bmad-output/implementation-artifacts/spec-ci-shard-failure-aggregation.md`
  — both exit 0. The broader Parties `git diff --check` returned exit 2 on
  pre-existing CRLF Story 8.7 and test-summary edits;
  `git -c core.whitespace=cr-at-eol diff --check` passed without changing
  repository configuration or those edits.
- SHA-256 preservation checks passed for the Parties CI caller,
  `scripts/test.ps1`, deferred-work ledger, shared release workflow, and
  central package props. Coverage validation, upload arguments, project
  inventory, dependency pins, and the parent's submodule gitlink were preserved.

Review provenance: the recorded workspace baseline is
`904b0ca928616130ec0092983c3c0c346c8fc2e1`; its Builds gitlink resolves to
`6daad3d501e97204eba66d971bba6a7103b85ccd`. The saved workspace diff includes
all changes since that baseline. Reviewers received the three aggregation
files' unified diff against that owning-repository baseline, avoiding unrelated
subsequent workspace work. The reviewed Builds starting HEAD was
`58d9b546b4741a246121ab40fc3945703db2e19b`.

Validation covers workflow structure and shimmed process behavior. No live
GitHub Actions run was started or credited.

Local completion commit in Builds: `86817400f62122246f55124fe6774c7677b872fd`.
The Parties index retains the original Builds gitlink `58d9b546b4741a246121ab40fc3945703db2e19b`;
this task does not enroll a submodule revision change or push either repository.

Commit validation used the repository lockfile versions: Builds commitlint
`21.2.3` and Parties commitlint `21.2.2`. The exact full candidates
`fix(ci): harden shard failure aggregation` and
`docs: complete shard aggregation review` each passed with exit 0. Builds'
installed CLI was older than its lockfile, so its locked toolchain and
byte-identical configuration were restored in a temporary directory. The
initial temporary-directory `--edit` attempt returned exit 1 because it lacked
a Git root; validating the same full candidate through stdin from Builds
succeeded. No package or lockfile was changed.
