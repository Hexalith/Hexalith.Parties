---
title: 'DW-70 reject forbidden NuGet dependencies regardless of casing'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NuGet package identifiers are case-insensitive, but the release package validator compares forbidden dependency IDs and fragments case-sensitively. Changing dependency spelling can bypass the existing package boundary.

**Approach:** Compare dependency IDs against the existing forbidden IDs and fragments without regard to case. Preserve the forbidden vocabulary, substring semantics, accepted dependencies and original spelling in diagnostics. Verify real nuspec inputs with direct and grouped dependencies across canonical, lower, upper and mixed casing.

</frozen-after-approval>

## Implementation Notes

- Selected DW-70 after completed DW-69. Baseline: 08fff3144c6617c01f0824af8a808937fb5d4539, clean main. Read-only investigation delegated as directed by bmad-build. No story-key transition applies to this freeform deferred item.
- Route assessment: no intent gaps or irreversible implementation action; small footprint in the validator, existing CI tests and workflow/evidence records. User explicitly authorized push on completion.
- `scripts/validate-nuget-packages.py`: normalize forbidden identifiers/fragments and dependency identifiers with casefold for boundary comparisons, retaining raw metadata for diagnostics. Reuse the existing EventStore validator's normalization convention. Keep allowlist and version validation outside this fix.
- `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs`: use the existing bounded RunPython harness and temporary zipped nuspec fixtures. Cover every exact forbidden ID and fragment, casing variants, direct and named-group layouts, safe near matches, and original diagnostic spelling. Use explicit runtime checks that cannot be disabled by PYTHONOPTIMIZE.
- Baseline Aspire start/wait/describe/stop succeeded using existing build output; Parties healthy. Some sibling projects were Finished or NotStarted; this is not fresh full-topology qualification. No AppHost, dependency, gitlink or deployment changes.
- Validate focused Debug CI build with source dependency selection, direct xUnit v3 focused and complete affected CI assembly, Python syntax, warning override guard and whitespace. Retain baseline red and final green evidence under `tests/dw-70-case-insensitive-forbidden-packages-2026-10-20/`.

- Implementation: both forbidden collections and dependency comparisons use casefold; original metadata remains intact. Canonical and case-variant direct/group fixtures reject all forbidden cases and accept five safe near matches. Initial Debug build has zero warnings/errors; baseline new cases fail 9/14 and focused/full tests turn green at 30/30 and 144/144. All 14 initial new cases also pass with PYTHONOPTIMIZE=1.
- Review corrections expand the matrix to 18 cases with 12 fixtures each: longer names containing forbidden substrings, candidate dependencies in either first or last group beside a safe group, and multiple forbidden dependencies whose original spellings must all appear in the diagnostic. Single-ID cases remain independent to detect bypasses without companion errors masking failures.

## Review Triage Log

- Medium, patch: initial fragment fixtures used dot boundaries, allowing an incorrect segment-only matcher to pass. Added TestTools, SampleTools and AppHostTools cases to protect existing substring semantics.
- Medium, patch: initial grouped fixtures placed candidates only in the last group, allowing omission of earlier groups to pass. Every theory case now runs direct, first-group and last-group layouts, with an allowed dependency in the other group.
- Low, patch: one forbidden ID per fixture could not detect diagnostic truncation. Added a separate multi-ID case requiring every original spelling while retaining the single-ID regressions.

- Final verification: the post-review Debug build has zero warnings/errors; focused tests pass 34/34, full affected CI assembly 148/148, and optimized-environment new cases 18/18, with no failures/errors/skips/not-run. Independent review executed all 216 fixtures and confirmed segment-only, one-group-only and truncated-diagnostic mutations are rejected. All three findings are resolved; no new deferred work. Pinned commitlint 21.2.2 validates the exact commit message. Commands, results and source hashes are retained in `tests/dw-70-case-insensitive-forbidden-packages-2026-10-20/validation.json`.
