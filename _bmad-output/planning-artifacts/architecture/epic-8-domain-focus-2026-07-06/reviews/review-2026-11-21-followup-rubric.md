# Post-update rubric review · Epic 8 · 21 November 2026

**Pass for the architecture update.** The final spine, companion spec and team overview address all eleven findings from the standalone validation. The remaining approvals and deployment proofs appear as conditions for the affected work.

| Prior finding | Resolution in the final documents |
| --- | --- |
| Reads during a storage outage | I10 defines admissible cached results. I14 requires clients to clear personal data after denial. |
| Memories cleanup inventory | I19 records the shipped conditional SDK operations and preserves the ledger across rebuilds. Parties and EventStore owners must resolve the interface choice and approve the consistency tests in I20 before using them for migration evidence. |
| Actor attribution history | I7 ends current eligibility at profile erasure. I8 binds past attribution to separate custody, exclusive expiry at 365 fixed days from binding effectiveness, and restore-time lifecycle verification. Production use still requires owner evidence. |
| CAP-1 closure evidence | CAP-1 names the August baseline, November 2 closure and subsequent test retirement. The spec and §7 require an approved successor test and results at the selected identities for a new deletion. |
| Projection delivery and rebuild | I9 distinguishes folding the contiguous tail after a checkpoint from full replay into an empty candidate. Replacement must preserve newer checkpoints and tombstones. |
| Tenant-claims and McpCli routing | §1a and §2 retain parent authority and the rollback surfaces. The I20 divergence record remains pending. |
| Create retries | I11 specifies format and provisioning checks, then the ordinary and composite no-event outcomes for an existing party, including after erasure. |
| Dependency rollback | §4 records forward and restored identities separately for a package or gitlink reversal. Adapter-only switches use the same dependency identity. |
| Isolated Dapr test callers | I1 records the canonical and exercised caller, session namespace, trust domain and permission set. Deployment ACL changes retain their approval requirement. |
| Protection adapter registration | The team overview identifies the Parties host’s SDK registration followed by `AddParties`, and places `AddEventStoreServer` in the separate gateway test host. |
| Gateway-test deferral | §9 drops the superseded reminder and §7a dates the retired coverage test. DW-96 retains the recorded run. Successor-host and external-deployment proofs retain their own gates. |

The six inherited Epic 7 decision IDs and the §7 closure evidence map remain identifiable. The amended spec retains the six capabilities, Parties ownership of GDPR wording and the I16/I18/I20 retirement gate. The sprint tracker records Epic 8 and Story 8.10 as done and Stories 8.7–8.9 as blocked. The governing records continue to assign the pending ledger-test and parent-boundary decisions to their accountable owners. [Parent decisions](../../epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md) · [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Adopted spec](../../../../specs/spec-epic-8-domain-focus/SPEC.md) · [Approval matrix](../../../../implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#i20-approval-table--reconciled-2026-10-05) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml) · [Team overview](../../../../../docs/architecture.md)
