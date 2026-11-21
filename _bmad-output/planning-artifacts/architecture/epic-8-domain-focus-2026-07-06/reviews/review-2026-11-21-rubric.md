# Epic 8 rubric review · 21 November 2026

**Verdict on the reviewed draft.** The updated release selections and story statuses agree with the recorded decisions. Two inconsistencies affect how a deployment or deletion owner could apply the spine.

| Finding | Evidence | Disposition |
| --- | --- | --- |
| High · Environment requirements | The §9 infrastructure row limits the external topology contract to I1 and I1a. I7 and I8 also bind production erasure, key custody, and replica revocation. A deployment owner could satisfy the row while leaving those gates open. | Amend §9 to defer provider and topology choices while retaining I1/I1a and I7/I8 acceptance criteria. |
| High · Closure reproducibility | CAP-1 and the adopted spec’s success signal require two named test classes to pass on a fresh HEAD. The November 16 CI migration retired both. The historic six-lane result at `c7dd607b` documents the tested source and dependencies. Current work needs a named product witness for the affected claim. | Identify CAP-1 as a companion conflict, keep the historical receipts, and require I18/I20 owner approval of a successor before a deletion relies on it. Offer to reconcile the adopted spec. |
| Medium · Erasure operations | The November 20 action assigns the subscriber, subscription and retry owner and calls for monitoring how long requests remain pending. I7 gives the deployment owner the consumer-identification task but omits the operational threshold. | Point to the tracker action and retain the owner-defined threshold and retry ownership when describing the operating contract. |

**Inheritance and status.** The update preserves all six Epic 7 decision IDs and their adapter, owner, release-sequencing and rollback rules. The tracker records Epic 8 and Story 8.10 as done and Stories 8.7–8.9 as blocked. CAP-3 through CAP-6 retain their distinct crypto, runtime, UI and deployment exit proofs. DW-111 records a narrower browser result within the still-deferred UI migration. No new migration or release acceptance follows from this update.

[Parent spine](../../epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md) · [Epic 8 contract](../../../../specs/spec-epic-8-domain-focus/SPEC.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml) · [November 20 action ownership](../../../sprint-change-proposal-2026-11-20.md) · [Current product checks](../../../../../docs/ci.md)
