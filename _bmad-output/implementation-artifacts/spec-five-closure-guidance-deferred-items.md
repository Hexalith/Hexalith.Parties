---
title: 'Close five deferred planning and closure guidance gaps'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred maintenance items leave project guidance inconsistent with default-main policy, historical G5 inspection evidence hard to reproduce, Epic 8 context missing prerequisite and external-owner constraints, the sprint planner's fresh rebuild fix unattributed, and resolved closure deferrals eligible as accepted waits.

**Approach:** Complete DW-16, DW-42, DW-43, DW-45 and DW-114 together. Correct the narrow guidance gaps using repository and historical evidence, document the existing fresh rebuild behavior, and make the closure parser honor each ledger entry's first-class status while preserving legacy accepted deferrals. Verify, independently review, reconcile the ledger, commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Starting clean main: 4fbe30883eb18ec074530e49a42669b5600ba2af. The user explicitly requested a five-item batch and push. Small reversible documentation and test-helper changes; no new public API, production behavior, data mutation, dependency update or deployment. No unresolved intent gap; use oneshot.
- Code map: `_bmad-output/project-context.md` has one typed-branch/PR requirement conflicting with authoritative Hexalith default-main guidance. Correct only that sentence.
- Code map: `story-8-3-platform-api-prerequisite-matrix.md` carries dated G5 observations. Reproduce historical root gitlinks from an exact canonical Parties commit, preserve old receipts and gate statuses, and distinguish historical inspection from current approval.
- Code map: `_bmad-output/implementation-artifacts/epic-8-context.md` needs explicit G8 run/publish security proof, DataProtection prerequisite identity binding and external deployment responsibilities, consistent with the prerequisite matrix and architecture spine.
- Code map: `.agents/skills/bmad-sprint-planning/scripts/sprint_plan.py` already suppresses status merging for `--fresh`, preserves retrospective action items, and applies explicit overrides last. Attribute that existing fix with its exact Git revision and run its existing tests on temporary files; never regenerate canonical sprint status.
- Code map: `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs` parses only packed reason fields. Parse whole current ledger entries, select only authoritative open statuses, and retain legacy accepted/open forms. A resolved entry must no longer satisfy the fixed accepted-wait expectation; genuine future closure requires deliberate reconciliation of that expectation and the dependent invariants.
- Given a completed ledger entry with retained accepted reason fields, parsing excludes it; given an open entry with stale packed status, first-class open wins. Missing or unknown statuses cannot establish accepted waits. Reordered legacy fields, evidence diagnostics and current five accepted waits remain validated.
- Verify with a Debug build and focused/full service assembly runs, existing Python sprint-planning tests, historical Git-object checks, repository guards and exact pinned commitlint. Retain commands/results under `tests/five-closure-guidance-deferred-items-2026-10-21/`.

- Investigation found DW-45 regressed: original timestamp guard/assertions at d4e1d83d22d554345dec816c52b00b87954002af were reverted by 4d39759a4dc72d5d679f88dc449dd54c6881885a. Restored the small original correction and added JSON-output assertions; this is separate from STORY_RANK/slug handling. Canonical sprint-status is untouched.
- DW-42 mutable commands were previously removed; restored explicit reproducible historical commands using the already-designated receipt at Parties 75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6, not an invented August 4 parent pin. Actual historical EventStore/Builds gitlinks and exact EventStore tag were read directly from Git.
- Failing-before evidence: the new closure cases produce 11 failures in 48 cases against the baseline parser; the timestamp regression fails 1 of the 37 sprint-planner tests before the guard restoration. An initial new-test build hit CA1062; the test now uses the null-safe string check and the corrected build passes without warning suppression.

- Final affected assembly passes 2227/2227 without failures/errors/skips; focused closure/prerequisite cases pass 106/106. The Debug build has zero warnings/errors. Sprint-planning tests pass 37/37. Repository guards and exact pinned commitlint 21.2.2 pass. No root gitlink or canonical sprint-status change; matrix byte comparison confirms only the G5 row changed. Independent review is in progress.

- Independent review completed. Added ten boundary cases, reproducing four actual failures in 58 closure cases before the review corrections. Packed data now comes only from the unique reason line, legacy bullets cannot bypass enclosing ledger status, and duplicate fields fail closed. Duplicate-status and legacy-adjacency cases also exercise the previously uncovered branches. A new full service verification is running after those changes.

## Review Triage Log

- Medium, patch (finding 1): a completed ledger entry could contribute a nested accepted legacy bullet because the legacy scan ignored enclosing entry spans. Exclude such matches; a completed mixed-format regression proves the rejection.
- Medium, patch (finding 2): reading packed fields from the entire entry could take a missing reason owner from origin metadata. Parse only the unique reason line; the regression uses one unrelated historical owner and requires the owner gap.
- Low, rejected (finding 3): a fenced status example could be interpreted as metadata, but the supported flat migrated-ledger schema and current ledger contain no fenced examples. A Markdown fence parser would add format support for a usage absent from the actual work queue. Revisit if fenced examples become supported ledger content.
- Medium, patch (finding 4): after introducing status eligibility, first-match-wins legacy status allowed contradictory evidence. Require unique packed fields; duplicate accepted and conflicting legacy status cases reject.
- Low, patch (finding 5): added duplicate open and conflicting open/done first-class status cases in both orders to directly exercise the rejection branch claimed by the evidence.
- Low, patch (finding 6): added separate following-bullet and following-heading cases; unrelated owner metadata cannot repair an incomplete legacy deferral.

- Final verification after review patches: full service assembly passes 2237/2237, including 58 closure and 58 prerequisite cases, with zero failures/errors/skips/not-run. Final Debug build has zero warnings/errors. Independent follow-up found no remaining material introduced defect and confirmed the fenced-example disposition. All five ledger entries are done; no new deferred work.
