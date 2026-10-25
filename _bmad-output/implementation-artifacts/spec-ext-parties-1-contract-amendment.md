---
title: 'EXT-PARTIES-1 contract amendment review artifact'
type: 'bugfix'
created: '2026-10-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The existing `specs/spec-ext-parties-1-contract-amendment` packet
contains the six proposed clause replacements and five unresolved owner
decisions, but the decision table does not render its rows. Its existing static
receipts predate this build and do not verify rendered table structure.

**Approach:** Complete the review artifact by correcting the decision table,
independently checking its quoted sources, crosswalk and preservation claims,
and recording fresh document-only evidence linked from the verification plan.
Keep the proposal `proposed-unapproved`, all proposed bounds, all five owner
decisions, accepted records and historical evidence unchanged. This build
approves no amendment, backend, principal, spend, deployment or runtime
qualification. User approval permits continuing with existing workspace work.

</frozen-after-approval>

## Implementation Notes

- Planning: no unresolved intent choice for this review-artifact scope; the five
  owner decisions remain output for later review. No irreversible action or
  runtime/API change is needed. The footprint is the table, verification-plan
  addendum, a fresh static receipt and this build-tracking file.
- Investigation: the first table in `owner-decisions.md` has a blank line after
  its delimiter, so Markdown renders zero decision rows. Independent checks
  found all ten Before quotations, sixteen source hashes and twenty-eight
  crosswalk IDs intact; no concrete clause contradiction was found.
- The earlier staged edits were committed externally before this continuation.
  Baseline HEAD is `9ee857b11ebcd13d44644be7c04c4ea6da5fdc44`; the working tree
  was clean. Captured hashes cover 6,944 tracked non-reference regular files
  and all eight existing packet files. No Git mutation was performed.
- This build record's eventual `done` status describes completion of document
  repair and review only; it does not change the source proposal's status.
- Implementation: removed exactly one blank line in `owner-decisions.md`;
  independent byte comparison confirms its decision text is unchanged. Appended
  a dated build-validation account and repeatable table-inspection command to
  `verification-plan.md`. Kept `validation.json` and `validation-final.json`
  unchanged; added separate `validation-build.json` evidence.
- Verification: the ad hoc Python document check passed all fourteen checks,
  including rendered rows, source hashes, exact quotations, original ledger
  IDs, preserved historical packet bytes and tracked-file/index preservation.
  Executing the verification plan's inspection command printed six rows
  (header plus five decisions), exit 0. No .NET build or runtime lane applies
  to this document-only change.
- Commit-message validation: repository-pinned and installed `@commitlint/cli`
  21.2.2 accepted the exact message `docs: verify contract amendment review`
  with its final newline. Command: `./node_modules/.bin/commitlint --edit
  /tmp/ext-parties-amendment-build-7yft7d8x/commit-message.txt --verbose`.
  Result: exit 0, `found 0 problems, 0 warnings`. The message uses the required
  documentation type, lowercase imperative description and no trailing period.
- Review fixes: archived the full read-only checker and original preservation
  baseline as `validation-build-support.zip`; documented pinned Python packages
  and extraction/use. Added committed-byte provenance for all fourteen sibling
  sources at Agents `476b8b94036bc7ea17741c4b66ac7d6f0c5e3ef3` and Platform
  `51ea5deca1cbb70d1df0483766db8072b76cc348`. Unrelated sibling worktree edits
  remain outside the source-preservation claim and were not changed.
- Final document checks pass fifteen checks. The extracted archived checker also
  passes. The table command passes the repaired packet and exits 1 for the
  original blank-line defect, a missing decision, an empty cell and a wrong
  header. An initial empty-cell control exposed Markdown's automatic padding;
  requiring nonempty cells corrected the check before these final results.
- Final follow-up verification passed the extracted runner, dynamic command
  metadata and every packet hash. All five review findings are patched; no
  deferred work or runtime changes were introduced. The workflow's commit step
  records only this completed review-artifact change; it confers no contract
  acceptance or deployment authority.

## Review Triage Log

- **Medium, patched:** the initial receipt referenced a temporary checker and
  baseline; after cleanup its claimed checks could not be audited. Both are now
  archived with the packet and their extracted execution passed. The baseline
  checks remain explicitly historical, separate from future runtime gates.
- **Low, patched:** the documented table inspection printed rows but did not
  reject malformed input. It now asserts the exact header, decision IDs and
  nonempty cells; four negative controls fail as expected.
- **Low, patched:** the inspection assumed an installed Markdown parser. The
  plan now states exact installed package versions, Python compatibility and
  an isolated `uv run` invocation for the archived checker or focused command.
- **Low, patched:** hashes alone did not identify retrievable sibling revisions.
  The new receipt records full Agents/Platform commit IDs and confirms that all
  fourteen cited sibling files match their committed bytes. It does not claim
  unrelated dirty files are clean. No findings were deferred.
- **Low, patched in follow-up:** the archived checker's report still named its
  original temporary command and claimed to write the receipt. It now records
  its actual script path and checks the receipt link like any other file; its
  execution only emits JSON. Follow-up review verified the four earlier fixes
  and found no other concrete issue.
