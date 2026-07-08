# Sprint Change Proposal — File List Pre-Review Gate

- **Date:** 2026-07-07
- **Author:** Administrator (via `bmad-correct-course`)
- **Scope classification:** Minor (direct Developer implementation)
- **Path forward:** Option 1 — Direct Adjustment
- **Status:** Approved & implemented (edits applied and gate verified — see §6)

---

## 1. Issue Summary

The BMAD "File List" check was **self-attested, not verified**. Across the dev and
review workflows the File List — the story's declaration of every file it touched —
was confirmed by assertion, never by comparison against git:

- `bmad-dev-story` step 9 instructed the dev agent to *"Confirm File List includes
  every changed file"* and to HALT *"if File List is incomplete"* — but nothing
  mechanically determined completeness.
- `bmad-dev-story/checklist.md` carried a manual **"File List Complete"** checkbox.
- `bmad-code-review/step-01` built the review diff from git but **never**
  cross-referenced it against the story's declared File List.

**Consequence:** a file changed on disk but omitted from the File List (the
"undeclared" case) enters review with no story traceability, and no gate catches it.
The reconciliation that did happen was a human/agent eyeball — e.g. story 7-8's review
note *"File List vs git reality: Matches"* — which is unauditable and trivially skipped.

**Evidence / discovery:** Review of the existing skill definitions plus real story
artifacts. Story/spec files already carry a frontmatter `baseline_commit`
(e.g. `spec-8-5` → `2b209ada…`), which makes `git diff --name-only <baseline_commit>`
a deterministic ground-truth set — the mechanism to mechanize the check already
existed but was unused.

## 2. Impact Analysis

- **Epic impact:** None. This is a process/tooling change to the BMAD workflow skills,
  not a product change.
- **Story impact:** None retroactively. Going forward, dev-story stories cannot reach
  `review` with an inaccurate File List; the review workflow reports File List drift
  before a review begins.
- **Artifact conflicts (PRD / Architecture / UX):** None (N/A).
- **Technical impact:** One new shared script under `_bmad/scripts/`; edits to two
  skill definitions (`bmad-dev-story`, `bmad-code-review`). No product code, CI, or
  deployment changes.

Two real-world wrinkles the gate must (and does) handle:
1. **Legitimately-excluded paths.** Story 7-8 deliberately excludes submodule gitlink
   drift (`references/*`), and transient automator state
   (`_bmad-output/story-automator/*`) from its File List, while *including* the story
   file and `sprint-status.yaml`. The gate never faults excluded paths in either
   direction, and never faults a story for omitting itself.
2. **Missing File List section.** Some Epic-8 spec files (`bmad-dev-auto` output, e.g.
   `spec-8-5`) carry `baseline_commit` but have no `### File List` section. The
   gate fails before dev-story can move to review, and before code-review can begin,
   because there is no story File List to reconcile.

## 3. Recommended Approach

**Option 1 — Direct Adjustment.** Effort **Low**, Risk **Low**, timeline impact
**none**, fully additive. No rollback (Option 2) or MVP change (Option 3) is warranted.

**Design — one gate, two call sites.** An *actual* gate must be deterministic, so the
core is a single Python script, `_bmad/scripts/check_file_list.py`, reachable from both
skills exactly as `resolve_customization.py` is today. Each skill stays self-contained;
both invoke the script and act on its exit code. This avoids duplicating the algorithm
(and its drift) across two skill definitions.

- **Ground truth for "changed":** committed (`base..HEAD`) ∪ working-tree (`base`) ∪
  untracked (`git ls-files --others --exclude-standard`), where `base` = the story's
  frontmatter `baseline_commit` (overridable via `--base`).
- **Disposition (per approval):** undeclared (changed, not listed) → **fail/HALT**;
  phantom (listed, not changed) → **warn**. `EXCLUDE` paths (submodule gitlinks,
  `sprint-status.yaml`, automator/orchestration state) and the story file itself never
  fail or warn.
- **Exit codes:** `0` reconciled · `1` FAIL · `2` usage/error.

## 4. Detailed Change Proposals

### 4.1 NEW — `_bmad/scripts/check_file_list.py`

The deterministic gate. Reads `baseline_commit` from frontmatter, computes the changed
set (committed ∪ working-tree ∪ untracked), parses the `### File List` block
(CRLF-safe, backtick-wrapped bullets), applies `EXCLUDE`, and reports UNDECLARED
(fail) / phantom (warn). Flags:
`--story <path>` (required), `--base <commit>` (override), `--require-file-list`
(missing File List section → fail; used by dev-story and code-review gates).

### 4.2 EDIT — `bmad-dev-story/SKILL.md` step 9

`OLD:` `<action>Confirm File List includes every changed file</action>`
`NEW:` run `check_file_list.py --story <story-file> --require-file-list`; exit 1 with
UNDECLARED files → HALT and add them (or revert); phantom warnings → remove stale
entries unless intentionally documented. The final gate
`if="File List is incomplete"` HALT is rewritten to `if="check_file_list.py exits
non-zero"`.

### 4.3 EDIT — `bmad-dev-story/checklist.md` (Definition of Done)

`OLD:` `**File List Complete:** File List includes EVERY new, modified, or deleted file`
`NEW:` `**File List Reconciled (gated):** check_file_list.py … --require-file-list
exits 0 — every file changed since baseline_commit is listed (no UNDECLARED); phantom
entries resolved or intentionally justified`.

### 4.4 EDIT — `bmad-code-review/steps/step-01-gather-context.md`

New instruction **6 (pre-review gate)**: when `{spec_file}` is set and has a
`baseline_commit`, run
`check_file_list.py --story {spec_file} --require-file-list`. Exit 1
(UNDECLARED or missing File List) → HALT before review and require the story File
List to be updated, or the undeclared files reverted, before continuing. Phantom
warnings remain non-blocking CHECKPOINT notes. Skipped when VCS is unavailable or
`review_mode == "no-spec"`. The original sanity-check step is renumbered to **7**,
and the CHECKPOINT summary now includes the File List gate result.

## 5. Implementation Handoff

- **Recipient:** Developer agent (Minor scope — direct implementation).
- **Deliverables:** the script + three skill edits above.
- **Success criteria:** gate runs deterministically with correct exit codes across
  reconciled / undeclared / phantom / missing-File-List / no-spec paths; existing
  workflows unaffected apart from the new gate.

## 6. Verification (post-implementation)

`check_file_list.py` was exercised against real artifacts and a controlled throwaway
git repo:

| Scenario | Expected | Result |
|---|---|---|
| No File List, no `--require` (non-gate caller) | WARN, exit 0 | ✅ |
| No File List, `--require` (dev-story/code-review gate) | FAIL, exit 1 | ✅ |
| Changed file not in File List (undeclared) | FAIL, exit 1 | ✅ |
| `references/*` + automator + story-file changed | not flagged | ✅ |
| Listed-but-unchanged (phantom) | WARN, exit 0 | ✅ |
| Perfect match | OK, exit 0 | ✅ |

A logic fix was applied during verification: a missing File List section with no
`--require-file-list` now short-circuits to WARN/exit 0 (previously it mis-counted all
changed files as undeclared and failed).
