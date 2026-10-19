---
title: 'DW-60 bounded SDK detail query identifiers'
type: 'bugfix'
created: '2026-10-19'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-60 remains open: SDK detail and privacy queries accept oversized or malformed party identifiers before constructing read-model keys or accessing erasure records. Typed clients and command validators already enforce a bounded shared identifier policy.

**Approach:** Apply the existing `PartyIdentifier.IsValid` policy in `PartySdkQueryService.TryValidateDetailEnvelope` for all six detail query types. Preserve ordinal AggregateId/EntityId agreement, optional EntityId fallback, tenant validation, ULIDs, supported semantic identifiers up to 128 characters, and the helper's legacy GUID representations. Return the existing `InvalidEnvelope` failure before any store access for rejected identifiers. Add handler-level regressions for invalid inputs, supported identifiers, fallback and mismatches; resolve only DW-60 after validation and independent review. No contract, persistence, dependency or gitlink changes are needed.

</frozen-after-approval>

## Implementation Notes

- Investigation: `PartyIdentifier.IsValid` is the established policy used by `HttpPartiesQueryClient`, `HttpAdminPortalGdprClient`, and command validators. Reuse it directly; do not introduce a narrower regex or GUID parser. Source changes are confined to the shared SDK query envelope validator and its existing handler test class, with this spec, validation evidence and DW-60 ledger closure.
- Route assessment: no unresolved intent gaps, irreversible actions or new public API; small local change.

- Implementation: the shared validator calls `PartyIdentifier.IsValid` and retains ordinal aggregate/entity equality; obsolete reserved-character field/import removed. Handler regressions cover rejected IDs before both stores, accepted one/128-character semantic IDs, ULIDs, all existing GUID representations and omitted EntityId across all six operations; mismatch coverage now includes every operation.
- Validation: the first 150 rejection rows reproduced 102 failures on the original service. Final Debug/source build has zero warnings/errors; focused handlers pass 511/511 and the complete affected service assembly passes 1724/1724 without errors, failures, skips or not-run tests. Static warning and whitespace gates pass. See `tests/dw-60-bounded-detail-query-identifiers-2026-10-19/validation.json`.
- Runtime baseline: existing-build Parties/Security healthy, EventStore/Tenants Finished; full integrated topology remains unqualified. Owned AppHost stopped before edits/builds.

- Review corrections: added null, empty and whitespace EntityId fallback rows; case-only aggregate/entity mismatch rows; malformed B/P/X GUID-shaped inputs; exact processing-store address and returned record assertions for export/processing operations. No production change was needed after review.

- Final after review: Debug/source build again has zero warnings/errors; focused handlers pass 685/685 and full affected service assembly passes 1898/1898 with no failures, errors, skips or not-run tests. Independent follow-up confirms all four corrections and no new defect. No new deferrals; final source and assembly hashes are retained with validation evidence.

## Review Triage Log

- low, patched — Blank EntityId fallback was under-tested: a null-only implementation would pass the earlier added tests. Accepted-ID rows now cover null, empty and whitespace EntityId alongside an explicit matching value for every handler.
- low, patched — Number-only mismatches did not prove ordinal agreement. All six handlers now also reject case-only differences before either store receives a call.
- low, patched — The added legacy compatibility matrix lacked realistic malformed GUID-shaped inputs. Added nonhex and delimiter faults at the expected B/P/X lengths to the rejection matrix.
- low, patched — Positive export/processing coverage could miss an incorrect processing address. Seeded an identifiable record, asserted the exact tenant/party processing key and checked that the complete record reaches both response payloads.
