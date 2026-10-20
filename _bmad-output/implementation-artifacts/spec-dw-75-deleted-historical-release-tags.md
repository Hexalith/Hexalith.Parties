---
title: 'DW-75 tolerate deleted historical release tags'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The release publication verifier stops when an older published GitHub Release references a deleted tag, even when another release tag proves successful publication of the dispatched source.

**Approach:** Continue past explicit HTTP 404 responses when resolving published release tag refs. Preserve failure for other API errors, malformed responses, unsupported objects, and the absence of any valid tag resolving to the exact dispatched SHA. Cover the actual workflow shell with isolated GitHub API fixtures, then commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Selected open DW-75 following completed DW-74. No epic-story status change. The footprint is the publication-verification shell, a focused CI test class, short CI documentation, this spec, deferred-work completion, and validation evidence. No intent gaps, dependencies, public API changes, or irreversible implementation steps; the user explicitly authorized the final push. Use oneshot.
- Read the Hexalith baseline/Git instructions, tracked repository/project guidance, build configuration, editor/git attributes, docs/architecture.md and docs/ci.md. Main is clean at f48d5b9db4ce7b02f8b00c1f8fcf9aa8d2484408. Authoritative Git guidance and the user request govern work on main.
- Read-only delegated investigation confirmed release.yml currently aborts on every unsuccessful exact-ref lookup. Reuse existing release enumeration, URI encoding, ref/SHA validation, annotated peeling, and final exact-source requirement. Change only failed reference lookups with an explicit 404; no blanket error suppression or target_commitish fallback.
- Add ReleasePublicationVerificationTests.cs in the existing Hexalith.Parties.Ci.Tests project, following ReleaseSourcePreflightTests: extract and execute the tracked shell, isolate gh through temporary PATH, capture stdout/stderr/request order, bound the process, and clean up. Exercise deleted tags before lightweight/annotated matches; missing-only, unrelated/draft-only releases; non-404/API/transport failures; malformed refs and tag objects; multiple release pages and encoded names.
- This offline CI workflow change does not touch runtime application code or require an Aspire topology. Build the CI test project in Debug/source mode, run the focused class and complete affected assembly, actionlint release.yml, and run the existing warning/submodule and whitespace guards. Retain exact commands/results and commitlint validation. Complete the independent blind review before marking DW-75 done and pushing.

- Implemented explicit first-status-line HTTP 404 handling for failed ref lookups using gh --include; normalize header framing before validating the unchanged JSON body/ref/SHA and annotated-tag boundaries. Deleted refs log a skip and cannot themselves satisfy publication.
- Initial 32-case harness reproduces eight failures against the original workflow and passes all cases after the patch. Debug/source build has zero warnings/errors; the initial complete affected CI assembly passes 180/180 without failures/errors/skips/not-run.
- Independent blind review raised five verification/documentation improvements. Patched all five: enforce pagination in the fake, traverse an unrelated live tag before a match, include misleading target_commitish on missing/mismatched release evidence, reject three malformed successful HTTP frames, and document Bash/jq requirements and Windows skips. Targeted independent follow-up confirms all corrections with no remaining issue.

## Review Triage Log

- Low, patch: the paginated fake returned all pages even without --paginate. It now returns only the first page when the flag is absent, so the cross-page success case detects that regression.
- Low, patch: no fixture traversed a live unrelated ref before the matching ref. Added deleted → unrelated → matching coverage; success still requires the final exact commit.
- Low, patch: release fixtures omitted target_commitish. Every release now carries target_commitish equal to SourceSha, including missing-only and mismatched refs that must still fail. This detects a fallback that fabricates tag proof.
- Low, patch: successful HTTP framing was always complete. Added missing-separator, truncated-headers and empty-response cases; each fails at existing exact-ref validation after parsing.
- Low, patch: local commands omitted prerequisites and Windows skips. docs/ci.md now states Bash/jq and non-Windows execution, with test-count inspection.

- Final Debug/source build has zero warnings/errors. Expanded focused tests pass 36/36 and the complete affected CI assembly passes 184/184 without errors, failures, skips or not-run cases. actionlint, the warning/submodule guard and CRLF-aware whitespace check pass. Commitlint 21.2.2 validates the exact commit message with zero warnings/problems. All review findings are corrected; no new deferred work. Evidence is retained in tests/dw-75-deleted-historical-release-tags-2026-10-20/validation.json.
