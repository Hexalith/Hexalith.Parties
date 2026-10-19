---
title: 'DW-78 consistent projection erasure timestamps'
type: 'bugfix'
created: '2026-10-19'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-78 remains open: Detail uses the payload's `PartyErased.ErasedAt` while the processing read model records the envelope timestamp as its erasure instant. Those times can differ, leaving the same party with inconsistent tombstones.

**Approach:** Follow DW-78's existing payload-erasure-instant decision: both SDK projection tombstones use `PartyErased.ErasedAt`. Preserve envelope timestamps for processing audit records and projection freshness. Cover serialized persisted live delivery, duplicate replay, later checkpoint advancement and full rebuild using deliberately different payload and envelope times, including non-UTC offsets. Document the semantics and the supported rebuild path for previously checkpointed values; close only DW-78 after tests and independent review. Keep cleanup-clock behavior, wire contracts and dependencies unchanged.

</frozen-after-approval>

## Implementation Notes

- Investigation: `PartyProcessingActivityFold.Fold` assigns the envelope time inside its PartyErased branch; `PartyDetailSdkProjectionHandler.Fold` already uses the payload time for the wrapper and nested Detail. Reuse the existing serialized stateful read-model test store in `PartySdkProjectionHandlerTests`; no new persistence mechanism or public API is needed.
- Route assessment: this is one small local fix with an existing recorded decision, no unresolved intent gaps and no irreversible action. Production edits are limited to the processing fold; tests and the processing-activity guide explain the distinction.
- Baseline revision: `9beab0283620e25636e1fdbcc1e07f151f2430a5`. Initial working tree is clean on main. Aspire Parties readiness passed; resource inspection reported EventStore, Parties, UI, Tenants and security healthy. Stop the owned AppHost before builds.

- Implementation: processing tombstones now read the matched PartyErased payload. Added six persisted live/rebuild cases with UTC and non-UTC payload offsets, earlier/later envelope times, duplicate redelivery and post-erasure checkpoint advancement. Rebuild cases seed the legacy mismatched value explicitly. The guide documents payload versus audit/freshness time and full rebuild adoption.
- Regression evidence: all six new cases fail on the original production fold at the processing ErasedAt assertion; the Debug/source baseline test build has zero warnings/errors.

- Review corrections: exact representation assertions now cover nested Detail values, later checkpoint advancement and persisted cleanup. Legacy values are redelivered and compared before rebuild to prove that normal delivery does not migrate them. The guide identifies party/party-detail, the affected tenant/aggregate, coordinated replacement, the restricted SDK surface and post-rebuild verification. Production logic did not need further changes.

- Final verification: Debug/source build has zero warnings/errors; all six regression cases and the complete affected Projections.Tests assembly pass (385/385, no failures/errors/skips/not-run). Independent follow-up verifies all four review corrections. Warning-policy and CRLF-aware whitespace gates pass. Commitlint 21.2.2 matches the lockfile and validates the exact candidate with zero problems/warnings. Evidence and final source/assembly SHA-256 hashes are in `tests/dw-78-erasure-timestamp-2026-10-19/validation.json`. No new deferred work.

## Review Triage Log

- low, patched — DateTimeOffset equality alone cannot enforce offset preservation for nested Detail and post-erasure advancement. Added EqualsExact assertions for those values and persisted cleanup output.
- low, patched — Legacy seeded processing metadata went directly to rebuild. Added an AlreadyCompleted redelivery and exact serialized before/after comparisons for both slots before rebuilding.
- low, patched — Existing cleanup tests did not establish retention of event-derived non-UTC erasure instants. Each new live/rebuild case now invokes the real eraser and reads back both slots, checking exact timestamps, retained audit records, checkpoints, high-water marks and freshness.
- low, patched — Rebuild guidance lacked the concrete projection and operational reference. Documented party/party-detail full replay for the tenant/aggregate, coordinated Detail/Processing replacement, the SDK-only surface and post-completion verification.
