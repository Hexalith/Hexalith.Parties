---
title: 'DW-74 reject duplicate BMAD array identifiers'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** BMAD configuration merging retains duplicate base codes or ids and silently collapses duplicate override identities, producing ambiguous effective configuration. Arrays introduced by a single layer bypass the keyed-array merge entirely.

**Approach:** Reject duplicate selected identities within each configuration input, including nested and newly introduced arrays, using ConfigError. Preserve valid cross-layer whole-entry replacement, stable ordering, code-before-id selection, and ordinary array append semantics. Verify the shared helper and both resolver entry points, then commit and push as requested.

</frozen-after-approval>

## Implementation Notes

- Selected open DW-74 after completed DW-73. Small Python tooling fix; no unresolved intent gaps, irreversible implementation steps, new public API, dependency change, or sprint story transition. Use oneshot route.
- Read required Hexalith baseline/Git instructions, tracked guidance/configuration and architecture. Main starts clean. The authoritative default-main policy takes precedence over the older project-context branch advice (existing DW-16).
- bmad-build delegated read-only investigation confirmed duplicate base retention, override collapse, and the first-layer/nested assignment bypass. Existing CLI callers already catch ConfigError and return exit 1.
- _bmad/scripts/config_utils.py: validate input trees once per public structural_merge call; retain existing merge mechanics in a private recursive helper. Reuse current code/id detection and string validation; reject duplicates within a selected keyed array before merging. Do not reject matching identifiers in separate layers.
- _bmad/scripts/tests/test_config_utils.py: deterministic stdlib unittest coverage of duplicate code/id inputs, single/new/nested/replacement arrays, valid whole-record replacement and ordering, code precedence, non-keyed append, input preservation, and actual central/customization loader and CLI behavior using temporary TOML fixtures.
- Preserve existing Python file newline style; new files follow CRLF. This change affects offline BMAD configuration tooling; .NET application topology does not participate in its validation.
- Validate with python3 -B -m unittest discover -s _bmad/scripts/tests -v, real repository resolver smoke checks, whitespace and warning guard. Preserve exact validation and pinned commitlint evidence under tests/dw-74-duplicate-bmad-identifiers-2026-10-20/. Complete independent blind review, triage all findings, and only then mark DW-74 complete and push.

- Implemented recursive validation before the public merge and per-input uniqueness checks at the array merge boundary. The latter covers the identity switching from code to common id across differently shaped layers.
- Initial baseline run: 12 test methods executed; 23 failing subcases reproduced duplicate acceptance. Final focused run: 13/13 methods pass, including central four-layer and customization three-layer ingestion and subprocess exit/stdout/stderr checks.
- Compared real repository central configuration and all 49 installed .agents skill customizations against the unchanged HEAD helper: all effective dictionaries match exactly. Both real resolver smoke checks, git diff --check and the warning/submodule guard pass.

- Review corrections add file/path/item-index diagnostics without identifier values, loader context for cross-layer identity switches, a dedicated bmad-config CI job and its local command in docs/ci.md. Real renderer subprocess regressions prove duplicate central/custom layers halt before snapshot creation and a valid synthetic skill publishes the resolved snapshot.
- Expanded focused suite passes 17/17. The Debug/source CI test project builds with zero warnings/errors; all 48 PartiesContainerPublishWorkflowTests pass without errors, failures, skips or not-run cases. actionlint accepts ci.yml. Existing valid configuration parity remains unchanged.
- Plain git diff --check treats required CRLF documentation lines as trailing whitespace; the CRLF-aware git -c core.whitespace=cr-at-eol diff --check passes without suppressing real trailing spaces. Existing file newline styles are preserved.

## Review Triage Log

- False, reject: per-layer code-first validation rejects duplicate codes even when a counterpart forces common id for merging. This is the intended rejection of ambiguous input, not a valid-override regression. Added the converse regression to document it; independent per-input validation and common-identity validation serve distinct boundaries.
- Low, patch: duplicate diagnostics identified only the field. Added array paths and both zero-based indexes; loaded TOML failures also identify the file. Identifier values remain absent.
- Medium, patch: the new Python suite had no automated caller. Added a required-success job to the existing CI workflow using its existing pinned checkout revision, without root or nested submodules. docs/ci.md records the command.
- Low, patch: renderer behavior was not covered. Added actual synthetic-skill subprocess cases for duplicate central/customization input without snapshot creation and a successful publication control.
- Low, patch (follow-up): combined identity selection could fail after successful per-file validation and omit file context. Both loaders now retain the incoming and earlier file paths around structural merging; a regression covers each loader and each duplicate side.

- Final independent follow-up verified file context, both loaders and duplicate sides, with no remaining finding or directly introduced defect. Exact commands, source hashes and commitlint 21.2.2 evidence are retained under tests/dw-74-duplicate-bmad-identifiers-2026-10-20/. No work was deferred.
