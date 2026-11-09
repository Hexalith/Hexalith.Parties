---
title: 'DW-27 distinguish invalid page sizes from cursors'
type: 'bugfix'
created: '2026-10-17'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 611fa39803d716b377c67801459a7dd4f3059829
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties list/search handlers report an out-of-range `Paging.PageSize` as `invalid-cursor`, even without a cursor. EventStore has no `invalid-page` adapter sentinel; emitting it today would become HTTP 500. Its HTTP handler also labels every downstream `query_invalid_page` failure as a cursor error.

**Approach:** Add shared EventStore `InvalidPage` support, then distinguish page-size rejection in Parties. Preserve HTTP 400 and `query_invalid_page`, using `reason=invalid-page` and generic page detail for the new failure. Preserve existing cursor responses.

## Boundaries & Constraints

**Always:** Work in each owning repository. Preserve the exception constructor signature; add an optional init-only adapter discriminator. Keep the shared cursor codec, strict payload parsing, payload page-size clamping, envelope/search precedence, offset validation, successful paging and privacy protections. Invalid envelope page sizes fail before codec/store/search access. Parties retains maximum 100; the public gateway retains maximum 200.

**Never:** Add gateway plumbing to Parties, encode page failures as envelope failures, use reflection to detect SDK APIs, invent package versions, publish, deploy, commit or push. Prepare the shared prerequisite first. Activate the Parties producer only with a genuinely published compatible SDK and passing package checks. If publication is unavailable, retain the tested consumer changes as an unapplied patch, preserve the tracked pinned-package build and leave DW-27 open with its dependency.

## I/O & Edge-Case Matrix

| Scenario | Input | Result |
|----------|-------|--------|
| Invalid page size | List/search `Paging.PageSize` negative, zero or above 100, with/without cursor | `InvalidPage` before codec/data access; HTTP 400, `query_invalid_page`, `reason=invalid-page`, safe generic page detail |
| Existing cursor/offset errors | Decode, decoded-position, alignment, negative/conflicting offset failures | Existing `InvalidCursor` classification and sanitization |
| Accepted paging | Sizes 1/100, defaults, valid continuation/offset, omitted envelope paging | Existing contents and metadata; legacy payload clamping remains |
| Hostile detail | Page sentinel alone or colon-prefixed with protected text or not-found/not-implemented words | Exact/prefix mapping to 400; generic output and logs |
| Legacy exception | 400 + `query_invalid_page`, no discriminator | Existing safe invalid-cursor response |

</frozen-after-approval>

## Code Map

`ES/` below means `references/Hexalith.EventStore/`, an owning repository; its skills are prohibited. Production changes belong in these files:

- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs`: `TryResolvePaging` conflates all failures; list/search callers hard-code `InvalidCursor`. Return the precise failure reason. Preserve other paging behavior.
- `ES/src/Hexalith.EventStore.Contracts/Queries/QueryAdapterFailureReason.cs`: add documented `InvalidPage = "invalid-page"`. Existing `QueryProblemReasonCodes.InvalidPage` remains unchanged.
- `ES/src/Hexalith.EventStore.Server/Queries/QueryExecutionFailedException.cs`: add documented init-only `AdapterFailureReason`; preserve the current constructor.
- `ES/src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`: map the new sentinel before fuzzy checks, set discriminator, sanitize page details/logs.
- `ES/src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`: sanitize page-prefixed log text alongside cursor text.
- `ES/src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs`: map explicit page discriminator; preserve legacy cursor fallback.

## Tasks & Acceptance

**Execution:**
- [x] Update the five EventStore production files above with the additive contract and mapping before preparing producer adoption.
- [x] Extend EventStore tests: `ES/tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs` for constant/wire compatibility; `ES/tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs`, `Queries/QueryRouterTests.cs` and `ErrorHandling/QueryProblemDetailsTests.cs` for exact/prefix boundaries, HTTP mapping and safe logging. Keep the existing legacy cursor test unchanged.
- [x] Prepare Parties classification in the mapped service and regression tests in `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs`: both query types, size boundaries, no reads/codec calls, cursor controls, precedence and legacy clamping. Keep an unapplied patch if the SDK is unpublished.
- [x] Extend `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` to pass an actual handler result through gateway mapping. Include sizes 101/200 admitted publicly but rejected by Parties, plus cursor controls. Verify HTTP status, reason, detail and no data leakage.
- [x] Update `ES/docs/reference/query-api.md`, this spec and `_bmad-output/implementation-artifacts/deferred-work.md` with behavior, rollout dependency and verification evidence. Close DW-27 only after package adoption and required checks pass.

**Acceptance Criteria:**
- Given invalid page size without a cursor, when list/search reaches the compatible gateway, then HTTP 400 identifies paging rather than a cursor.
- Given an existing cursor client or exception caller, when cursor validation fails, then prior safe status/code/detail/reason remain unchanged.
- Given no compatible published SDK, when prerequisite source work is verified, then tracked Parties still builds against pinned packages and DW-27 retains the explicit adoption gate.

## Implementation Notes

**Current disposition (2026-11-09):** DW-27 was implemented and closed by the later
`spec-five-sdk-runtime-deferred-items.md` batch on October 22, using published
EventStore 3.126.1. Its final receipt records 2,534/2,534 package tests and
2,534/2,534 mapped Debug service tests. The ledger and archive already reflect
that closure. Current selection is EventStore 4.1.0; the consumer code and
regressions are active. The dated prerequisite notes below retain their original
historical scope. Do not reapply the saved pre-adoption patch.

Fresh package build and all 952 handler/gateway tests pass with zero warnings,
errors or targeted skips. Current broad verification is blocked: mapped source
service-test compilation has three `CS0234` errors in
`PartiesCatalogGatewaySelectorTests.cs` (lines 20–22), and the package full suite
has four dependency-observation fitness failures (2,848 passed, 13 skipped,
2,865 total). This spec stays `in-review` for those current validation blockers;
it does not reopen the already-completed ledger feature or claim a current full
source/package pass. Exact commands, identities and results are in
`tests/dw-27-invalid-page-failure-2026-11-09/validation.json`.

- Initial clean main revisions: Parties `611fa39803d716b377c67801459a7dd4f3059829`; EventStore `77d9af18a54d92506e310fe5a45021a395e66f60`. Preserve concurrent changes; do not branch or update dependencies speculatively.
- Catalog selects EventStore 3.124.0; package inspection confirms missing `InvalidPage`. Publication/adoption is a known rollout prerequisite, not established by source tests.
- Aspire Parties health passed; EventStore/tenants/UI were Finished. Exact AppHost stopped successfully. Raw baseline logs: `/tmp/dw-27-validation/`. Full-topology qualification is unavailable.


- Implementation prepared on 2026-10-17: shared EventStore support is applied in its owning repository. The three-file Parties consumer change is tested and retained only as `tests/dw-27-invalid-page-failure-2026-10-17/parties-consumer-adoption.patch`; the tracked originals were restored byte for byte, and the patch passes applicability checking.
- Fresh NuGet index/package downloads confirm Contracts, Server, DomainService and Gateway latest stable packages remain 3.124.0, without the new contract/discriminator APIs. No consumer activation, version change, publication, deployment or Git mutation was performed. DW-27 remains open pending a compatible published SDK and passing adoption gates.
- Debug/source builds of both EventStore test projects and the prepared Parties consumer pass with zero warnings/errors. Focused checks pass 112/112 (Contracts), 116/116 (Server) and 214/214 (consumer). Restored pinned-package Parties build passes with zero warnings/errors and 146/146 focused checks.
- The earlier pre-review consumer assembly passed 1170/1171; immutable consumption proof fails because required EventStore prerequisite edits leave its checkout dirty. Full EventStore attempts were stopped after more than five minutes following packaging/Dapr failures outside the changed test classes (not reproduced against baseline); two Contracts package-inventory tests also skipped without release-package inventory. No full assembly or integrated-topology pass is claimed. See `tests/dw-27-invalid-page-failure-2026-10-17/validation.json`, raw logs and README for exact commands, counts and blockers.

- Final parent verification passed on 2026-10-17: candidate source build (zero warnings/errors), candidate handler/gateway 214/214, shared Contracts 112/112 and Server 116/116, all four normal/optimized verifier fixtures, restored pinned-package build (zero warnings/errors), whitespace and warning guards. Consumer bytes were restored and all ten shared source bindings match. Commands and raw hashes are in `tests/dw-27-invalid-page-failure-2026-10-17/parent-final-verification.json`.
- The targeted independent follow-up confirmed the original evidence/test/documentation findings are resolved. No further work was deferred. DW-27 remains open, and this spec remains in-review pending compatible SDK publication/adoption and full validation. The approved frozen no-commit boundary overrides the workflow's default local-commit step; no index or history mutation was performed.

- The user subsequently authorized committing and pushing all changes. EventStore was rebased onto current main and pushed as `9ed9b9742bd006e568f026cc1c588275cf9db097`; all ten tested source/test/doc files are byte-identical to the manifest. Current Parties source-observation constants/row are refreshed to that identity; historical receipts, owner approvals, package pins and the unapplied consumer patch remain unchanged. Commit messages passed the owning pinned CLIs; see `tests/dw-27-invalid-page-failure-2026-10-17/push-preparation.json`.

## Spec Change Log

- 2026-11-09 — Additional resumed review retains exact original source-build and full-suite diagnostics, records fresh affected-path verification, and carries existing broad blockers without duplicate deferrals. Preserve active code, package selections, frozen intent, historical receipts and the exact archival fixture; keep `in-review`.

- 2026-11-09 — Reconcile this resumed spec with the later published SDK adoption and ledger closure. Preserve the frozen intent, historical patch/receipts, package selections, and active consumer behavior. Clarify the archival verifier prerequisites and record fresh verification blockers separately from the October 22 acceptance.
- 2026-11-09 — Follow-up review documents the active HTTP response distinction, retains the exact archival fixture and original broad-failure diagnostics, binds future inventory indexes, and labels superseded rollout instructions. Preserve runtime behavior, dependencies and historical receipts; retain `in-review` while the existing broad validation blockers remain.

## Review Triage Log

- B01 — low, rejected: the matrix combines direct-handler and gateway expectations for sizes intercepted by public validation. Public bounds remain unchanged and gateway tests cover 201; the proposed correction edits the frozen spec, which review routing rejects. No paging-path defect was demonstrated.
- B02 — low, patch: the SDK inspection script hard-codes the selected version and a compatibility result; a later rerun can produce stale qualification data. Resolve the configured selection and separate documentation observations from unperformed qualification.
- B03 — low, patch: XML documentation does not prove assembly API absence. Label XML observations accurately and retain actual assembly API inspection for the selected packages.
- B04 — false, rejected: independent latest-package inventory is not claimed to be a restorable candidate, and consumer activation additionally requires package build/gateway verification. No incompatible package set is adopted or certified; clarify inventory limits alongside B02/B03.
- B05 — low, patch: the restoration verifier never checks tested_candidate_sha256. Reconstruct the saved patch against baseline files in a temporary directory and compare the resulting hashes.
- B06 — low, patch: Python optimization removes the verifier's assert statements while it still emits PASS. Replace proof assertions with explicit checks that fail under optimized Python too.
- B07 — low, patch: baseline revision alone does not identify the modified shared prerequisite tested here. Bind current EventStore source/test bytes to the validation record after final focused verification.
- B08 — low, patch: accepted-size tests assert metadata but omit expected contents, and derive HasMore from actual counts. Assert independent expected IDs/counts and continuation behavior for both handlers.
- B09 — low, patch: compound invalid size/offset inputs lack direct coverage of page-size precedence. Add negative/conflicting/misaligned offset combinations while retaining cursor controls.
- B10 — low, patch: the adapter-category inventory omits invalid-page and invalid-cursor despite the adjacent mapping prose. Add both rows.
- P01 — low, patch: newly added EventStore blocks use same-line braces and new public test members lack XML documentation, contrary to the loaded Hexalith baseline. Correct newly added code only and preserve the legacy cursor test byte for byte.
- Edge-case layer: no findings. Verification-gap layer: no findings.

- Resolution: B02/B03/B05/B06/B07/B08/B09/B10 and P01 are patched. Independent follow-up inspected actual final artifacts and confirmed their original claims are resolved; parent checks independently verified reconstructed candidate and shared-source bindings. B01/B04 retain their recorded rejection decisions. No new deferred entry is needed; the existing DW-27 publication/adoption dependency remains.


### Resumed review — 2026-11-09

The workflow baseline includes accumulated repository history; this review traced the invalid-page change and its evidence tools. The independent edge-case and verification-gap reviewers found no issues. The blind reviewer reported the following ten findings; each was checked before grouping or routing.

- R01 — low, patch: the October 17 README still invites running the restoration verifier against the current checkout, although adoption intentionally changed all three bound files. Mark that packet historical and link the October 22 adoption and current verification; preserve the original patch and receipts.
- R02 — low, rejected: a failed archival verifier rerun does leave an older receipt in place, but its nonzero exit reports failure and the retained receipt describes the dated pre-adoption run. These scripts have no active CI/runtime caller. Failure-state receipt management adds complexity for an unlikely archival workflow; clarify the historical prerequisites through R01 instead.
- R03 — low, rejected: a deliberately selected output path can overwrite an input after successful verification on a restored historical checkout. Protecting arbitrary output aliases requires additional path/symlink guards; this dormant evidence script is not an everyday consumer path. Do not expand its interface for this low-impact scenario.
- R04 — low, patch: the verifier's recorded command omits a custom receipt destination. Add the resolved `--receipt` argument so that its command reproduces the destination as well as the selected manifest; exercise it only in an isolated historical fixture.
- R05 — low, rejected: an interrupted inventory can leave partial files next to dated earlier receipts. The script is an archival observation tool, not an activation/qualification gate; transactional multi-file publication is additional complexity for an unlikely rerun. R01 makes its historical-only context explicit, and no retained inventory is regenerated here.
- R06 — low, rejected: the archival inventory uses an unpinned inspection tool. Original metadata outputs, package hashes and module identifiers are retained, and schema/tool errors fail the command rather than certify adoption. Adding a new tool-version selection changes this dormant workflow without establishing a current consumer defect; preserve its dated outputs.
- R07 — low, rejected: archival MSBuild and inspection subprocesses lack explicit timeouts. They are manual evidence collection, with no runtime or CI caller. Adding timeout/error-state branches exceeds a direct correction for this unlikely rerun.
- R08 — low, rejected: the new HTTP theory omits zero/negative sizes. Those are rejected by the unchanged SDK public validator before Parties, while direct handler tests cover zero, negative and extreme sizes and the HTTP tests cover schema-admitted 101/200 plus the public maximum. Additional HTTP fixtures would expand coverage without a demonstrated missing changed behavior.
- R09 — low, rejected: HTTP cursor controls use codec rejection rather than every offset variant. Direct handler controls establish the same `InvalidCursor` sentinel for negative, conflicting and misaligned offsets; the gateway maps that sentinel independently of its source. Public validation intercepts negative/conflicting offsets. Additional cross-layer combinations add fixtures without a demonstrated mapping defect.
- R10 — low, rejected: the size-100 success fixture has four rows. It independently proves acceptance, unchanged contents and metadata; a separate two-page fixture proves continuation through the same unchanged paginator. A new 101-row matrix would expand coverage without showing a regression from the classification change.

- R01/R04 resolution — the historical README now links adoption/current verification and states the restoration prerequisites. The verifier includes the resolved custom receipt path in its recorded command. An isolated fixture reconstructs and hashes the original pre-adoption files, applies the retained patch, checks the saved candidate bindings, and verifies the custom destination without modifying retained evidence.
- V01 — medium, defer: the current mapped source test build fails on three aliased catalog-selector types at `PartiesCatalogGatewaySelectorTests.cs:20–22`. The source host/SDK projects compile; these selector aliases are outside the invalid-page implementation. Record the exact source mapping and command for the owning follow-up; do not change dependency selection or remove the test.
- V02 — medium, defer: four full-suite `PlatformApiPrerequisitesTests` fail because current committed Builds/McpCli/Platform identities and EventStore sprint state differ from the retained observations. These assertions and their inputs predate this resumed run; record them without rewriting immutable observations or weakening gates.

### Follow-up review — 2026-11-09

The three workflow layers reviewed the accumulated baseline diff, focusing on
DW-27. Edge-case and verification-gap reviewers returned no findings. The blind
reviewer's ten findings received these individual verdicts before grouping:

- F01 — low, rejected: the HTTP fixture exercises the actor router rather than the newer canonical handler-aware route. The current `HandlerAwareQueryRouter` forwards unsuccessful `QueryResult.ErrorMessage` unchanged to the same `SubmitQueryHandler` mapper. Extra route fixtures add coverage without a demonstrated classification defect.
- F02 — low, rejected: the package fixture uses `NullLogger`, so it does not itself assert log contents. Shared `QueryRouterTests` and `SubmitQueryHandlerTests` already exercise exact/prefixed paging failures and protected-detail logging. Additional package log-capture fixtures add complexity without a demonstrated leak from this change.
- F03 — low, rejected: current consumer HTTP cases use exact handler sentinels; hostile prefixes and the discriminator-free legacy exception are covered in the owning SDK tests and historical acceptance. Another package-only mapper matrix would add fixtures without a demonstrated changed mapping failure.
- F04 — low, patch: current `docs/api-contracts.md` describes paging bounds but omits the response distinction. Document the HTTP status, stable problem code, page/cursor reasons and generic details, explicitly retaining public validation precedence and its maximum of 200.
- F05 — low, patch: the November receipt's new archival fixture exists only under `/tmp`. Retain its exact hash-bound bytes as `tests/dw-27-invalid-page-failure-2026-11-09/check-archival-receipt.py` and execute that repository copy in normal and optimized Python.
- F06 — low, patch: the November full-suite receipt lists failures without retaining their assertion diagnostics outside `/tmp`. Extract all four messages, expected/actual values and stacks from its hash-bound original XML into `package-full-failure-diagnostics.json`; preserve the original receipt and identify the extraction as historical.
- F07 — low, rejected: the archival reconstruction verifier checks patch/manifest consistency without treating the separate validation receipt as an additional trust anchor. Jointly replacing patch and manifest can describe another reconstructed candidate, but the tool is a dormant byte-binding check, not an authenticity gate. Additional trust guards exceed a direct correction for an unlikely archival rerun; the independent historical hashes remain preserved and checked by the follow-up receipt.
- F08 — low, rejected: the archival inventory reads the configured package-version property without checking whether source mapping is active. Its manual inventory-only scope establishes neither active consumer selection nor compatibility, and the historical README forbids regenerating the dated packet in the current checkout. Adding selector branches would expand this dormant tool without a demonstrated current consumer defect.
- F09 — low, patch: inventory output names its retained NuGet indexes without hashes. Add `index_sha256` directly to future output records. Preserve all dated inventory outputs and validate script syntax without running the network inventory against the historical packet.
- F10 — low, patch: the historical README's later patch-application instructions and open-dependency conclusion remain easy to read as current. Mark the entire remaining results/rollout section as superseded October 17 history, preserving its original text and receipts.

F04/F05/F06/F09/F10 are separate direct documentation/evidence corrections;
none requires changing production behavior, dependencies or the frozen intent.
Fresh package Debug build passes with zero warnings/errors and all 952 focused
handler/gateway tests pass without skips. The retained archival fixture passes in
normal and optimized Python; syntax, warning and whitespace guards pass. Current
source/test assembly hashes match the earlier November receipt, so its broad
source-compilation and four full-suite fitness failures remain the validation
blockers; those unchanged broad lanes were not repeated. Durable focused logs/XML,
commands, hashes and limits are recorded in
`tests/dw-27-invalid-page-failure-2026-11-09/follow-up-verification.json`.
The original blind reviewer independently confirmed all five direct fixes and
preservation of the five bound historical artifacts, with no unresolved patched
claims.
The spec remains `in-review`; no Git or dependency mutation was performed.

### Additional resumed review — 2026-11-09

All three workflow layers reviewed DW-27 and its directly referenced code/evidence
within the accumulated baseline diff (50,620,130 bytes). Unrelated historical
changes were not exhaustively reviewed. Edge-case and verification-gap reviewers
returned no findings. Each blind-review finding received its own verdict before
grouping:

- S01 — medium, defer, carried V01: the same three unresolved aliases at `PartiesCatalogGatewaySelectorTests.cs:20–22` prevent the mapped source test assembly from compiling. Keep the existing recorded follow-up and source-build refusal; no duplicate ledger entry or source/test change.
- S02 — medium, defer, carried V02: the Builds gitlink assertion in `PlatformApiPrerequisitesTests.cs` still differs from the selected committed identity. Keep the existing observation-policy follow-up and immutable diagnostics; do not rewrite observations or add a duplicate deferral.
- S03 — medium, defer, carried V02: the McpCli identity assertion is the same recorded full-suite failure. Preserve its existing follow-up and diagnostics without changing dependencies or duplicating its deferral.
- S04 — medium, defer, carried V02: the Platform identity assertion is the same recorded full-suite failure. Preserve its existing follow-up and diagnostics without changing dependencies or duplicating its deferral.
- S05 — medium, defer, carried V02: the required EventStore sprint `backlog` entry is the same recorded historical-status mismatch. Preserve the existing observation-policy follow-up rather than altering sprint state or historical evidence.
- S06 — false, rejected: `validation.json` already records the exact tool version, Platform range and HXW010 diagnostic in `source_preparation`, and explicitly states that source compilation failed and no source qualification is claimed. The unsupported selection is disclosed, not treated as a qualified mapping.
- S07 — low, patch: the original source-build log is hash-bound but only retained under `/tmp`; losing that file would discard diagnostic context for the recorded source blocker. Retain its exact original bytes in a compressed artifact and bind both compressed and original hashes in the new receipt.
- S08 — low, patch: the original full-suite XML/log are only under `/tmp`, leaving the identities and reasons of the 13 broad-suite skips unavailable after cleanup. Retain the exact original XML/log, preserving their failure/skip status and identifying them as the original run rather than new verification.
- S09 — low, rejected: the new archival fixture exercises successful reconstruction and receipt destination, while the retained October `verification-script-tests.json` already records corrupt-candidate rejection under normal and optimized Python. Adding another negative fixture requires extra fixture mutation/control flow for a dormant archival tool, without a demonstrated rejected-binding regression. Preserve the exact retained fixture and historical receipt.
- S10 — low, rejected: the historical inventory deliberately writes into its dated packet, and its README explicitly forbids regenerating that packet in the present checkout. Adding a new output-directory interface or a separate execution workflow expands a dormant inventory tool; no active runtime/CI caller or current activation claim relies on it.

S07/S08 share the missing durable broad-diagnostics root cause and are corrected
by retaining the original hash-matching source log and full-suite log/XML. The
original blind reviewer independently confirmed those bindings, all 13 skip
names/reasons, and preservation of the original validation receipt.
No implementation subagent from the earlier run is available, so these direct
evidence corrections are applied in the parent session. S01–S05 retain their
existing verdicts and deferrals; no new deferred work is created.

Fresh selected-package Debug build passes with zero warnings/errors, and all
952 handler/gateway tests pass with zero skips. The normal/optimized archival
fixture and warning/whitespace guards pass. Active source, test assembly, all
four selected EventStore 4.1.0 package/DLL hashes, and five historical artifact
hashes match the prior receipts. Exact commands, results, durable logs/XML and
review limits are in
`tests/dw-27-invalid-page-failure-2026-11-09/resumed-review-verification.json`.
The original broad source/full-package lanes were not rerun. The spec remains
`in-review` for the existing broad blockers; the October 22 feature closure and
all pre-existing edits, dependencies and historical receipts are preserved.

## Verification

The November 9 receipt records the current selected-package build, focused/full
tests, mapped source build, and isolated historical-verifier check. It records
both broad blockers and preserves the October 22 successful adoption as historical
evidence. The original planned commands below describe the October 17 workflow;
current source mapping must come from the pinned module tool's `prepare-build`
environment, rather than handwritten source selectors.

Build affected test projects individually in Debug with `-m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`. Invoke their built xUnit assemblies with single-dash `-class` filters for the named classes, then run full affected assemblies. Before consumer activation, verify published SDK availability and the pinned-package build/gateway tests. Apply repository fallback validation without weakening gates. Run whitespace/warning-override guards and independent implementation review. Record exact commands, counts and blockers under `_bmad-output/implementation-artifacts/tests/dw-27-invalid-page-failure-2026-10-17/`.
