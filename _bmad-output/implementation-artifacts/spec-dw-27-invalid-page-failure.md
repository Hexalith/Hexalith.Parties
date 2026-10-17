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

## Verification

Build affected test projects individually in Debug with `-m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`. Invoke their built xUnit assemblies with single-dash `-class` filters for the named classes, then run full affected assemblies. Before consumer activation, verify published SDK availability and the pinned-package build/gateway tests. Apply repository fallback validation without weakening gates. Run whitespace/warning-override guards and independent implementation review. Record exact commands, counts and blockers under `_bmad-output/implementation-artifacts/tests/dw-27-invalid-page-failure-2026-10-17/`.
