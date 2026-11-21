# Epic 8 update reviewer gate · 21 November 2026

**Architecture passes. One medium correction is needed in the linked subscriber guide.** The eleven findings in the latest recheck have corresponding rules, owner decisions or source corrections in the amended spine, adopted SPEC and team overview. Reviewed spine SHA-256 `b19e31d4ba7d6c4a70c93b92e3147e23643abf42707715967b2982e443a8f557`.

### Medium · Subscriber quick start contradicts I7

The overview’s §7 directs integrators to the subscriber guide. During review its quick start (line 12), dispatch example (line 101) and key rules (lines 152–154) called for success even after handler errors. Its deduplication snippet (lines 119–131) marked an event processed on receipt. A consumer can then acknowledge redelivery while the original effect is still outstanding. Correct the quick start, examples and key rules to distinguish durable completion, confirmed duplicates, intentional skips and durable handoff from transient failure. The adjacent handler guide already gives the correct contract. [Subscriber guide](../../../../../docs/event-subscribing.md) · [Handler guide](../../../../../docs/event-handler-patterns.md) · [I7](../ARCHITECTURE-SPINE.md)

### Recheck dispositions

| Decision area | Evidence in the amended documents |
| --- | --- |
| Host readiness, receiver authentication and external ACL | I1/I1a and CAP-4/CAP-6 specify readiness by dependency, authenticated callbacks, receiver/operation-bound assertions and grant-by-grant ACL evidence at the selected revision. The existing registration distinguishes sidecar, state store and enabled Tenants from degraded health dependencies. |
| Empty degraded UI results | I10/I14 and CAP-5 require the server to validate reuse of the same authorized representation. They distinguish denial and a newly admitted empty result, and keep 8.9 parity open until list/search behavior and tests agree. |
| Subscriber redelivery | I7 and CAP-2 require retry before durable completion or handoff. The operation identity, retry owner, duplicate handling and downstream erasure duty are explicit. |
| File-based work and key-provider custody | §5 assigns a deterministic slot and affected owners to resolve uncertain mappings. I19/I20 and SPEC OQ-3 retain an approval gate for authoritative key storage and its consistency tests. |
| Replay, Pact, test inventory and GDPR notice | The overview separates SDK replay selection from source-order fitness, dates the legacy Pact job, distinguishes 18 script suites from three Platform suites and identifies production custody as open. |

The parent’s AD-1 through AD-6 retain their identities and authority. The six Epic 8 capabilities and recorded 8.10 closure remain traceable. New retirement work still needs applicable successor tests, owner approvals and rollback proof. Deployment ownership, regulated custody, cross-replica revocation, terminal erasure and blocked Stories 8.7–8.9 remain explicit. [Parent spine](../../epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md) · [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [SPEC](../../../../specs/spec-epic-8-domain-focus/SPEC.md) · [Team overview](../../../../../docs/architecture.md) · [Earlier recheck](validation-report-2026-11-21-recheck.md)

### Final verification · Pass

The medium finding above is closed. In the revised subscriber guide (`4457b1ce7062`), the quick start, dispatch and deduplication sections require durable completion or resumable handoff. The retry rules preserve broker redelivery on transient failure and assign reconciliation after exhaustion. The ordering examples require checkpoint and effect to commit together, gaps to be retried or reconciled, and delayed writes to respect newer state and erasure tombstones. Both guides identify the shipped `PartyErased` contract and the duty of consumers holding party references. The deployment qualification remains open. [Subscriber guide](../../../../../docs/event-subscribing.md) · [Handler guidance](../../../../../docs/event-handler-patterns.md#partyerased-handler-mandatory)
