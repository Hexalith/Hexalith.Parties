---
title: 'Reconcile DW-182 and current I16 committed selection'
type: 'bugfix'
created: '2026-11-02'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'a92d15866bbf4f49cefa6c03f4a9eeec442af1de'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-182 remains open and current I16 tests compare today's committed source and package graph to the dated 2026-11-01 owner selection. The source signoff ledger permits the current gitlinks at the RC gate, but it does not approve I16 parity or refresh immutable current-selection assertions.

**Approach:** Update only present-tense selection guards and observations for the owner-approved current identities, execute the canonical Release build and all-tests lane, record remaining parity limits, close the two deferrals where evidence permits, then commit and push main.

**Owner decision (2026-11-02):** The owner approved the ten current committed root gitlinks and effective Release package graph in `dw-182-i16-approval-comparison-2026-11-02.md` for current-selection assertions and approved continuing this spec. The decision does not turn any parity receipt into Pass.

## Boundaries & Constraints

**Always:** Treat the 2026-11-01 selection and earlier parity receipts as dated history; keep all six canonical parity receipts Unvalidated until rerun at their stamped identities. Require committed root gitlinks and the independently evaluated Release package graph. Preserve source/package differences where root pins override Builds defaults.

**Never:** Infer parity, deployment, publication, custody, rollback or deletion approval from source selection, a passing build, or an RC gate. Do not move gitlinks, change dependency versions, edit submodule source, or rewrite historical receipts in this correction.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Current selection | Approved exact HEAD gitlinks and Release graph | Current-only tests and matrix match committed SHA, checkout, describe and package evaluation | Any mismatch fails fitness |
| Historical evidence | Prior identity stamps and unrun parity surfaces | Dated approvals remain intact; current parity stays Unvalidated | No new parity Pass or deletion claim |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:51,97,992` — dated approval, stale current observation, and I20 approval scope; preserve historical rows.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:61-103,755-845` — current SHA/describe/tag constants and graph assertions; keep historical constants and immutable refusal checks.
- `Directory.Packages.props`, `references/Hexalith.Builds/Props/Directory.Packages.props` — root family pins and imported defaults; evaluate rather than infer effective values.
- `.gitlink-signoff.tsv`, `scripts/gitlink-rc-gate.sh`, `_bmad-output/implementation-artifacts/deferred-work.md:1412,2121` — signed source selection, RC gate, DW-182 and I16 deferral.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` and Epic 8 `ARCHITECTURE-SPINE.md` I16 — parity disposition and stop rule.
- `docs/ci.md:268-295`, `scripts/test.ps1` — canonical Release restore/build and all-tests commands.

- `_bmad-output/implementation-artifacts/dw-182-i16-approval-comparison-2026-11-02.md` — exact ten-gitlink and effective package-graph approval packet; update it only if committed identities change before decision.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` — refresh current-only identities, describes and effective catalog assertions after owner approval; retain historical evidence and refusal behavior.
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` — append the owner decision and correct the present-tense observation without rewriting dated approval.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` and test evidence — reconcile DW-182 and the new I16 entry with exact commands, results and parity limits.
- [x] Root `main` — prepare validated commit evidence for the reviewed changes; workflow delivery follows review.

**Acceptance Criteria:**
- Given approved identities, when committed-selection fitness runs, then all ten gitlinks and the effective graph match without promoting historical parity.
- Given the selected tree, when canonical Release restore/build and all-tests run, then results and any remaining failures are recorded accurately.
- Given a current signed selection, when the RC diff gate compares `v1.2.1`, then all ten pointers pass; unresolved parity remains visible.

## Implementation Notes

- Baseline advanced to `a92d15866bbf4f49cefa6c03f4a9eeec442af1de` before implementation. It updated current-selection test and matrix observations while retaining the same ten gitlinks approved by the owner. Its EventStore local tag assumption and effective Builds catalog assertions require verification.

## Spec Change Log

## Review Triage Log

- **Blind 1 — medium, patch:** The spine frontmatter and I16 disposition still call the 2026-11-01 b6443b24 set current. Reviewers can read the wrong I16 identity from the authoritative spine; date that history and state the approved 2026-11-02 selection and six Unvalidated receipts.
- **Blind 2 — low, patch:** The comparison packet supplies exact SHAs but no approval scope, although the matrix and spec cite it as the decision source. Add the 2026-11-02 owner decision and its selection-only limit there.
- **Blind 3 — false:** The v3.132.1 describe anchor is available in this root-declared checkout and default CI initializes the submodule without a shallow flag. `DescribeObservedSource` deliberately matches the recorded anchor, so a later v3.132.2 tag cannot change the observation; a checkout missing required history fails loudly rather than receiving qualification credit.
- **Blind 4 — false:** The selected Builds SHA fixes the catalog, and its relevant family members use shared version properties. `CentralPackageVersionOverrideEnabled=false`; no Parties project declares a relevant per-project version override. An altered catalog needs a new Builds gitlink and fails the exact SHA guard.
- **Blind 5 — medium, patch:** The current matrix prose can state stale non-EventStore package values while the guard checks only EventStore prose. Assert the stated versions against independently evaluated current package items.
- **Blind 6 — medium, patch:** Removing or desynchronizing the new I20 owner row leaves the current source-observation test green. Assert the decision row exists and carries the same ten committed SHAs and seven selected package versions.
- **Edge 1 — false:** No supported Release consumer supplies the proposed per-project family override; root package props and the fixed Builds catalog select the same central items, and central version override is disabled. The existing external-runtime graph preflight separately rejects EventStore package skew.
- **Verification gap — none:** The independent verification reviewer found no missing executed test for the stated change.

## Verification

**Commands:**
- `dotnet restore Hexalith.Parties.Standalone.slnx` — succeeds.
- `dotnet build Hexalith.Parties.Standalone.slnx --configuration Release --no-restore -m:1` — zero errors.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release` — all required test projects pass or exact blocker recorded.
- `bash scripts/gitlink-rc-gate.sh --diff v1.2.1` — all committed root gitlinks have valid owner source signoff.

**Results (2026-11-02):** All four commands exited 0. The Release build had zero
warnings and errors. The all-tests lane passed 16 projects with 6,483 succeeded,
zero failed, and 26 explicit skips; its log is
`/tmp/dw-182-i16-all-tests-reviewed.log`. The RC gate reported `BUMP ok` for all ten
root gitlinks. The committed-selection method passed independently (1/1).
All six canonical parity receipts remain Unvalidated; no parity or release
qualification is inferred from these checks.

**Commit validation:** Exact candidate `test: reconcile approved committed selections` passed the repository-pinned `./node_modules/.bin/commitlint --edit /tmp/parties-dw182-i16-commit-message.txt` with exit 0; output is `/tmp/parties-dw182-i16-commitlint.txt`.
