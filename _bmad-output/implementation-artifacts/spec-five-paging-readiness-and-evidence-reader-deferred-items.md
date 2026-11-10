---
title: 'Close five paging, readiness and evidence-reader deferrals'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
baseline_commit: '5e5d42e3c31b39ca9f2ea51a2072e101754dd8e1'
route: 'dispatch'
review_loop_iteration: 1
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open deferred-work entries are still live or unsettled at
HEAD `5e5d42e3`. Processing history accepts a cursor with offset zero, which the
gateway and list/search refuse. Four consumer-portal e2e interactions can run
before Blazor interactivity. The integration-evidence readers accept results
missing fields the released `hexalith.test-result.v1` schema requires, and
their fixture is schema-invalid. Whether the readers need the full released
validator is undetermined. Lifecycle `read()` checks links, then opens the path
again by name, so it follows a linked or swapped parent directory.
Separately, main CI is red: the lifecycle boundary unit test still asserts the
call that `87a7b600` replaced.

**Approach:** Make small, reversible fixes, each with a red-proof control.
Settle the two "determine" entries with traced evidence. Repair the stale CI
assertion; it does not count among the five. Close the seven already-fixed
ledger rows with commit citations; they get no new credit. Commit and push on
main, as the user requested.

Decision (owner, 2026-11-10): processing history rejects a nonblank cursor
combined with offset 0 as `invalid-page` before any detail or processing read,
matching the EventStore gateway and processing's existing offset rule.
`docs/api-contracts.md` is updated to match.

Decision (owner, 2026-11-10): keep the full spec (about 2,100 tokens) as one
batch, including the CI assertion repair and the stale-row closures.

## Boundaries & Constraints

**Always:** Preserve authorization, the list/search paging behavior, the single
domain request and status assertions in each e2e case, historical receipts and
evidence packets, and the existing reader refusals. Use package-mode Release
with `-warnaserror` for .NET checks. Write new evidence only in a new packet,
`tests/five-paging-readiness-and-evidence-readers-2026-11-10/`.

**Never:** Change dependency versions, gitlinks or submodule contents. Add a
new CI dependency (the reader tests run with `submodules: false` and without
`jsonschema`). Weaken the warnings-as-errors build gate. Change FrontComposer,
Platform or Memories code. Edit existing evidence packets.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Processing cursor + offset 0 | `(10, 0, cursor)`, any store state | `invalid-page` | No detail or processing read |
| Processing cursor, null offset | `(10, null, cursor)` | Unchanged: decoded and paged | `invalid-cursor` on decode failure |
| Reader: missing required key | Result lacking `retention` or `bindings.tool` | Refused | Existing refusal type |
| Reader: schema-valid result | Fixture shaped like real hosted artifact | Accepted | — |
| `read()` linked parent | `link/dir/x.json` where `link` is a symlink | Refused | `ValueError` |
| `read()` symlink / hardlink / oversized file | As today | Refused, unchanged | `ValueError` |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.ProcessingBuckets.cs:63-64` -- `IsSupportedProcessingPaging`. List/search counterpart: `PartySdkQueryService.cs:648`.
- `docs/api-contracts.md:415-419` -- the processing paging paragraph ("A nonblank cursor with offset zero remains accepted").
- `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs:74-77` -- `ProcessingUnsupportedPagingCases` matrix across store states.
- `tests/e2e/specs/consumer-portal-routes.spec.ts` -- unguarded sites: L167-168 (edit Save), L185-188 (edit validation), L233 (consent Marketing emails), L401 (privacy Delete my data). The guarded pattern is at L268/L484.
- `tests/e2e/helpers/interactive-shell.ts` -- `expectInteractiveShell`; reuse it.
- `EditMyProfilePage.razor:78` -- `EditForm FormName`. A pre-interactive click may become a native POST.
- `scripts/verify-current-owner-integration.py:~270` -- `validate_held`, the shared result check. The public reader reuses it at `scripts/verify-public-package-integration.py:106-115`.
- `scripts/test_current_owner_integration.py:177` -- the shared result fixture (18 schema errors), reused by `scripts/test_public_package_integration.py`.
- `references/Hexalith.Builds/schemas/hexalith.test-result.v1.json` -- released schema, sha256 `9773678f…7e39` at Builds `5ec17790`. Top-level required: `schema, policyId, bindings, diagnostics, terminal, termination, retention, cleanup`.
- Builds `TestResultWriter.cs:194-200` -- the producer validates against the released schema before publishing. This is the evidence that settles L2304.
- `scripts/qualify-disposable-ci.py:119-125` -- `read()`. `private_directory()` (L128-132) enforces the same no-linked-ancestor policy. Callers: L224, 389, 393, 459, 503, 509, 557, 637, 655, 663, 669, 690, 706, 725, 766, 854, 1013, 1133.
- `tests/ci-lifecycle/test_qualification_controller.py:35` -- `test_linked_and_hardlinked_metadata_are_refused`; extend it.
- `tests/ci-lifecycle/test_workflow_boundaries.py:120-125` -- the stale assertion. Current calls: `PartyPersistenceAssertions(fixture, output).ExecuteAsync(bound.Token)` and `.RecheckAsync(bound.Token)`.

## Tasks & Acceptance

**Execution:**
- [x] `PartySdkQueryService.ProcessingBuckets.cs`, `docs/api-contracts.md`, `PartySdkQueryHandlerTests.cs` -- apply the cursor + offset 0 decision and add a `(10, 0, cursor)` row (red: it reaches the store at HEAD).
- [x] `tests/e2e/specs/` -- add a control that holds `_framework/blazor.web.js` and shows a pre-interactive click issues no command. Then add `expectInteractiveShell` before the four sites.
- [x] `scripts/verify-current-owner-integration.py` -- refuse results missing any schema-required top-level or `bindings` key, using constants bound to the schema hash.
- [x] `scripts/test_current_owner_integration.py`, `scripts/test_public_package_integration.py` -- rebuild the fixture as schema-valid. Add missing-key refusal controls. Add a full-schema check that runs only when `jsonschema` and the Builds schema are both present.
- [x] `scripts/qualify-disposable-ci.py` -- make `read()` open the path one component at a time without following links, then check size and link count on the open file and read from it.
- [x] `tests/ci-lifecycle/test_qualification_controller.py` -- add linked-parent and post-check swap refusal cases.
- [x] `tests/ci-lifecycle/test_workflow_boundaries.py` -- assert the current calls.
- [x] `deferred-work.md` -- resolve the five, close the seven stale rows (L2249 `0be5b476`; L2253 `806ed7b8`; L2347/L2232 `2849c5b4`/`4c0167b8`; L2351/L2356/L2360 `a69fa179`/`0ec80012`), and add review deferrals.

**Acceptance Criteria:**
- Given each red-proof control, when it runs against the reverted code, then it fails; after the fix, it passes.
- Given the full `Hexalith.Parties.Tests` suite, the script `unittest` lanes and `tests/ci-lifecycle`, when they run, then all pass except existing environment-gated skips.
- Given five retries-disabled consumer-portal e2e repeats, when they run, then all cases pass.

## Design Notes

L2304 resolution: the readers do not need the full validator. A schema-invalid
result cannot come from the producer, and after-the-fact tampering can
recompute every hash anyway. The stdlib required-key guard catches producer
drift without adding a CI dependency.

## Verification

Exact package-mode commands, the e2e server setup and the red-proof style are
in `tests/five-cleanup-readiness-and-processing-paging-2026-11-10/verification.json`
(relative to this directory). Reuse its `final-service-build` property set,
including `-p:MinVerVersionOverride=1.0.0`. Run xUnit v3 tests through the
built DLL with `-class` / `-method`, not `dotnet test --filter`. The
consumer-portal e2e run needs `CI=1 PLAYWRIGHT_SKIP_WEBSERVER=1` and the
package-mode Release UI on `http://127.0.0.1:5072`. Record a `verification.json`
in the new packet with the same shape, gzipping the logs.

**Commands:**
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj` with that property set, then `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll` -- expected: 0 failed. Also rerun the `PlatformApiPrerequisitesTests` and `EpicEightClosureFitnessTests` classes after the ledger edits.
- `python3 -m unittest discover -s scripts -p 'test_*_integration.py'` and `python3 -B -m unittest discover -s tests/ci-lifecycle -v` -- expected: OK.
- consumer-portal e2e spec with `--retries=0 --repeat-each=5`, and `npm run typecheck` -- expected: pass.

## Implementation Notes

- Ledger rows are identified by summary; the spec's line numbers are the
  `source_spec` lines at `5e5d42e3`. The five: processing cursor with offset 0;
  remaining consumer-portal interactivity; complete released G4 test-result
  schema; full-validator determination (L2304); lifecycle linked parent (L2343).
- Processing: `IsSupportedProcessingPaging` accepts an offset only when it is 0
  with a blank cursor. The `(10, 0, cursor)` row reached the store in all four
  states on the reverted code (4/52 failed). New blank-cursor rows pin that
  `""` and whitespace cursors with offset 0 still read the first page.
- e2e: with `blazor.web.js` held, Fluent elements are inert and a click issues
  no command on `/me/edit`, `/me/consent` and `/me/privacy`. The un-upgraded
  switch has no box, so its click event is dispatched. Red-proof: with circuit
  negotiation delayed 3 s, the unguarded consent and erasure cases fail on HEAD
  (click lost). The edit cases pass because `fill` waits for the editable input.
  All four guarded copies pass.
- Readers: `result_contract()` in the private reader is shared by the public
  reader. The fixture now has the genuine hosted shape; its full-schema errors
  drop from 18 to 0. HEAD readers fail 24 and error 12 of the 73 controls. The
  unchanged genuine 42690523916 artifact passes the public reader.
- Lifecycle: `read()` delegates to `read_bytes()`. It holds ancestors as
  `O_PATH` descriptors and opens the final entry with
  `O_NOFOLLOW|O_NONBLOCK|O_NOCTTY`. It checks type, link count and size on that
  descriptor and reads from it. `peer_scope` hashes the same held claim bytes.
  A HEAD probe returned the replacement bytes for all three swaps. The new
  controls fail on the reverted reader under Python 3.14 and 3.12.
- CI repair: `test_workflow_boundaries` asserts
  `ExecuteAsync(bound.Token)` and `RecheckAsync(bound.Token)`. Lifecycle suite
  113/113.
- Verification: see `tests/five-paging-readiness-and-evidence-readers-2026-11-10/verification.json`.
  Not run: source-mode Debug builds, Docker topology suites, hosted
  qualification, owned Memories runtime facts.
- Review follow-up (patches S1–S13): see
  `tests/five-paging-readiness-and-evidence-readers-2026-11-10/review-followup.json`.
  Results: scripts 74/74; CI-like clone 74 (3 checkout skips); lifecycle 115;
  service 2964, 0 failed, 13 skipped; e2e 140/140 over five retries-disabled
  repeats; typecheck clean. Red-proofs: descriptor swap, `peer_scope` alone,
  and the stale Builds pin.

## Review Triage Log

Blind review (one layer), 10 findings:

- R1 medium, patched: on Python 3.12 the swap hook fired on `is_symlink()`'s
  `os.stat(follow_symlinks=False)`, so the hard-link subtest passed on the
  reverted reader. The hook now ignores no-follow probes; 5/5 fail on both
  versions.
- R2 low-medium, patched: directory opens needed read permission. Ancestors are
  now `O_PATH`, and a search-only (0311) ancestor control was added.
- R3 low, patched: a socket raised `ENXIO` instead of the nonregular refusal.
  Special files now map to the existing `ValueError`, `O_NOCTTY` is added, and a
  socket case was added.
- R4 low, partly patched, partly deferred: the specified control passes on HEAD
  by construction. The guard red-proof is the negotiation-delay run. The
  post-release status check now names both route outcomes. A negotiation-held
  control and the native edit-form submission it exposes are deferred.
- R5 low, patched: offset 0 without a cursor was already pinned by
  `(1, 0)`/`(100, 0)`. Blank-cursor rows were added.
- R6 low, patched as a note: `FormatChecker` needs optional extras; the
  schema's utc and endpoint patterns are enforced either way.
- R7 low, resolved: the packet was completed after the review.
- R8 medium, patched: `peer_scope` re-read claims by name to hash them; it now
  hashes the held bytes.
- R9 medium, deferred: `write()`/`private_directory()` still check by name and
  then use the path.
- R10 low, deferred (Builds-owned): the receipt read in `issued()` reopens
  through Builds `receipt_owner()`.

Step-04 review of the pushed `3eccce24` (three layers: Blind Hunter, Edge Case
Hunter, Verification Gap; none skipped). R1–R10 above came from the
implementation subagent's own pass. The subagent committed and pushed before
this review because the Intent says "commit and push"; these fixes ship as a
follow-up commit. Rows below give the verdict and route; reporters are in
brackets.

- S1 medium, patched [BH1, VG3, ECH3]: CI runs the reader tests without the
  Builds checkout, so the schema-binding test skipped and a Builds bump could
  change the required keys silently. VG3's fix (initialize the submodule in
  CI) conflicts with the frozen "no new CI dependency" rule. Instead, the
  reader pins `RESULT_SCHEMA_BUILDS_COMMIT`, and a test compares it with
  `git ls-tree HEAD references/Hexalith.Builds`. That test runs in a fresh
  clone without submodules, and fails when the pin is the stale `3c809618`.
- S2 medium, patched [VG1]: no test swapped the file after the descriptor's
  `fstat`, so a reopen by name could have returned unnoticed. New subtests
  swap the parent, a symlink, a hard link and a regular replacement after
  `fstat`, and require the held bytes. All four fail when
  `return Path(path).read_bytes()` is injected.
- S3 medium, patched [VG2]: no test ran the real `peer_scope` claim loop. A new
  test drives it with real claim files, replaces the claim after `fstat`, and
  requires the digest of the validated bytes. It also covers array and
  foreign-owner claims. The test fails when the hash-by-name regression alone
  is injected.
- S4 low, patched [BH3]: the `read_bytes` docstring claimed a replaced regular
  file is "refused". It now says links are refused and later replacements
  cannot change the returned bytes.
- S5 false [BH5, ECH1]: the stricter reader does not newly refuse hosted
  paths. Builds `prepare_module_build.py:121` builds the mapping workspace
  under `real_path(temporary_root)`. `control()` already runs
  `private_directory()` on `stateDirectory.parent`, so a linked state-root
  ancestor was refused at baseline. The workflow also requires a resolved
  `RUNNER_TEMP`.
- S6 low, patched [BH7, ECH5]: the control's pathname check could not see a
  native same-path POST reload. It now also requires a single `blazor.web.js`
  request, and the comment claims only what is observed.
- S7 low, rejected [BH6, ECH4, VG-other]: the post-release zero checks have no
  settle time. Blazor has no mechanism to replay pre-circuit DOM events, and a
  settle wait adds timing complexity. The meaningful check runs while the
  script is held.
- S8 carried low, deferred [BH8, ECH6]: same claim as R4. The committed control
  passes on baseline by construction, and a negotiation-held control is
  already in the ledger.
- S9 low, patched [BH9]: closed stale rows cited this packet, which has no
  matching check. They now cite the hxw006 source-selector check, the
  workflow digest steps and their tests, and the committed gitlink
  equalities.
- S10 low, patched [BH10]: the genuine 42690523916 artifact is now kept in the
  packet as a deterministic tarball. The public reader (exit 0) and the
  released schema (0 errors) rerun against the extracted copy.
- S11 low, patched [BH11]: the pushed message (sha256 `95121d00…`) was
  revalidated with pinned commitlint 21.2.4 (exit 0).
- S12 low, patched [BH12]: the evidence descriptions said "HEAD" where they
  meant baseline `5e5d42e3`.
- S13 low, patched [BH14]: `docs/api-contracts.md` now explains why processing
  reports `invalid-page` where list/search report `invalid-cursor`.
- S14 low, rejected [BH2, ECH2]: a present but non-object contract value raises
  `AttributeError`, so the gate fails closed with a traceback. The producer
  validates the schema, so such a value is unreachable from it, and the fix
  adds guards.
- S15 low, rejected [BH4]: opening the final entry before `fstat` could touch a
  device node. A same-user writer cannot place one there: symlinks are
  refused, `mknod` needs privilege, and hard links cannot cross into `/dev`.
- S16 false [BH13]: the new ledger rows use the format this workflow
  prescribes; a missing status already reads as open.
- S17 low, rejected [BH15]: the "environment missing" branch of
  `issued_synthetic_tenant` is now unreachable through `validate()`, so a
  direct test would only cover dead defensive code.
