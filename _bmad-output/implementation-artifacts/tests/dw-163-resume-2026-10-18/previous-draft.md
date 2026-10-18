---
title: 'DW-163 coordinated unresolved projection recovery'
type: 'bugfix'
created: '2026-10-17'
status: 'draft'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

**Current outcome: withdrawn candidate; DW-163 remains open.** The implementation notes below are historical investigation, not a retained implementation. The initial event-dependent manifest was rejected; immutable initialization and safe SDK recovery remain requirements. A portable reproduction is retained in `tests/dw-163-sdk-recovery-investigation-2026-10-17/README.md`.

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An unresolved first projection delivery creates only the processing-activity slot. Every later retry then fails the coordinated-slot guard with `projection-rebuild-required`, even after the event becomes resolvable.

**Approach:** Initialize both absent slots through the existing EventStore batch store: an empty, unadvanced detail placeholder and the existing failed processing audit. Keep audit initialization separate from the normal dispatch batch identity, with bounded deterministic scope hashing and create-only concurrency. Retain the existing optimistic audit updates when both slots already exist, the genuine missing-slot rebuild guard, and normal projection/rebuild behavior. Prove persisted slot visibility, repeated unresolved delivery, later successful recovery, interrupted initialization and competing writes. Complete only DW-163.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps, irreversible operations, migrations, external mutations, dependency updates or new public API. Small footprint: `PartyDetailSdkProjectionHandler.cs`, two existing projection unit tests, a recovery test class in the existing service test project, this spec and the DW-163 ledger record. Use the oneshot route.

Reuse `IReadModelBatchStore`, `ReadModelBatchScope.ComputeScopeHash`, and create-only operations. Keep detail unadvanced until a fully supported delivery can fold it; the existing minimum-of-both-checkpoints admission preserves earlier events when the processing audit has incorporated a known prefix. Separate the initial audit batch identity from the normal dispatch identity because the SDK fingerprint includes ETags and payloads. Both absent slots give stable initial manifests on interrupted retry.

Regression coverage will use `Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore` from the service test project's existing dependency. It executes the real batch protocol, including ETags, marker visibility and fingerprints; the private projection-test double does not. Run the affected individual Debug/source project builds and complete test assemblies, plus the warning override guard. Preserve existing file line endings. Leave changes uncommitted under the user's repository safeguards.

Owned isolated Aspire baseline started. The 30-second `aspire wait parties` timed out, but the subsequent resource snapshot reported Parties, EventStore and Tenants Running/Healthy. Exact-target shutdown succeeded before editing source; this is a baseline observation, not full topology acceptance. Raw resource/startup evidence stays outside the repository to avoid retaining credentials and local paths.

Implementation: the first unresolved audit now creates both slots in one create-only SDK batch. Its ID is the fixed `unresolved-audit:` prefix plus the original scope hash, so maximum-length dispatch IDs remain accepted. Later unresolved attempts retain the existing optimistic processing update; later supported redelivery uses the untouched normal dispatch identity. The missing-slot guard remains unchanged.

Initial regression reproduction: all five new recovery cases failed before the production fix. They pass after it, using the actual SDK marker/envelope protocol and serialized persisted reads. Service Debug/source builds have zero warnings/errors. Two existing projection unit tests now assert the two-operation create-only audit manifest and unadvanced detail placeholder. Full affected suites and independent review are pending.

The candidate was withdrawn after independent review and a concrete SDK race reproduction. With a fault at `BeforeReceipt`, a competing erasure writes checkpoint/tombstone sequence 5; SDK reconciliation then restores the older empty Detail candidate (checkpoint `long.MinValue`, no erasure tombstone). The six-case reproduction reports five passes and this one failure. Raw reproduction, candidate source, build logs and XML are retained locally under `/tmp/dw-163-validation`; no candidate production/test change remains in the working tree. DW-163 stays open. A platform-owned recovery guarantee must precede a safe domain fix. The successful 1,032-test service run established only the earlier candidate's ordinary paths, not erasure safety.

## Review Triage Log

- Medium, verified: a prepared event-dependent audit batch cannot resume when the event becomes supported or the dispatch ID changes. A fixed empty initialization manifest would address that part, but was not retained because of the separate erasure race.
- Medium, verified: committed audit envelopes with empty ETags need compaction through their original batch identity before single-key audit updates can proceed. No candidate initialization path remains.
- Low, verified in withdrawn candidate: scope hashing before validation accepted oversized dispatch IDs on the unresolved branch. Withdrawal removes the new inconsistency; the existing supported path retains SDK validation.
- High, verified platform blocker: `BeforeReceipt` fault injection after competing tombstones demonstrates reconciliation overwriting the newer erased state. The required recovery guarantee belongs to EventStore and must not be reimplemented in Parties.
- Low, verified verification gap in withdrawn candidate: the test did not contend on the second operation after the first installed. Compensation/interrupted compaction and direct supported recovery need coverage when the platform guarantee permits implementation.

Independent follow-up: the earlier statement that initially absent slots imply stable manifests was incomplete. Resolution changes, changed dispatch IDs, and interrupted compaction invalidate that design. The portable reproducer preserves the rejected candidate solely to reproduce the separately verified SDK tombstone overwrite.
