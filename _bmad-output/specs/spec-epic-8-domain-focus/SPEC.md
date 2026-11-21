---
id: SPEC-epic-8-domain-focus
companions:
  - ../../planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - ../../implementation-artifacts/deferred-work.md
  - ../../implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
sources: []
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Epic 8 Remaining Work & Deferral Contract

## Why

Epic 8 is post-MVP maintenance (Class C). Parties keeps domain behavior and
moves shared mechanics to their platform owners. Story 8.10 and Epic 8 closed
with five accepted deferrals. Stories 8.7–8.9 remain blocked on their owners’
prerequisites. The architecture spine binds each subsequent migration to
evidence that it preserves GDPR, projection and contract behavior before a
rollback surface can be removed. [Sprint status](../../implementation-artifacts/sprint-status.yaml)

## Capabilities

- **CAP-1** — Closure durability
  - **intent:** Preserve the committed baseline and the evidence for the
    original closure so a future migration can identify the behavior it must protect.
  - **success:** Commit `2b63ab9` froze the I18 baseline and recorded
    `PlatformApiPrerequisitesTests` and `EpicEightClosureFitnessTests` with
    their dependency identities. Commit `c7dd607b` recorded the six passing
    Round 10 lanes and closure of Story 8.10 and Epic 8. Both records can be
    retrieved from Git. Commit `0f261ccd` subsequently retired the two test
    classes. For a new deletion, apply the I16/I18/I20 gate below.
    [Closure receipts](../../implementation-artifacts/tests/test-summary.md)
- **CAP-2** — Residual review-debt discharge (`8.6-residual-review-debt`)
  - **intent:** The 8.6 umbrella debt is discharged: deny-default
    EventStore-only ACL enforced in a runnable topology, authenticated
    end-to-end handler discovery, erasure-certificate identity/status
    validation, Memories cleanup races closed, Art.30/search inputs bounded.
  - **success:** The deferral's exit proof passes and every `[Review][Defer]`
    item in the 8.6 story file is checked. For erasure cleanup, acknowledge
    delivery after durable completion, confirmed duplicate completion, or an
    acknowledged handoff to a durable retry owner. A transient failure before any
    of those outcomes must return a retryable failure or explicit retry result.
    [Delivery and retry policy](../../../docs/event-publishing.md#dead-letter-topic-setup)
- **CAP-3** — Data-protection extraction (`8.7-data-protection-extraction`)
  - **intent:** Parties consumes the shared EventStore data-protection and
    key-management engine while GDPR policy and legal semantics stay local.
  - **success:** The full I8 compatibility packet is green at the recorded
    consumption identity, with an exercised switch-back.
- **CAP-4** — Runtime boundary cleanup (`8.8-runtime-boundary-cleanup`)
  - **intent:** Client envelopes/freshness, MCP plumbing, identifier helpers,
    and the integrated topology move to their platform owners; the Parties
    AppHost retires.
  - **success:** Topology, security, publish, and rollback parity are proven
    and every I1a condition holds (all AppHost-naming deferrals passed or
    owner-re-approved) before the retirement merges. On the successor, exercise
    loss of Dapr sidecar or state-store readiness, enabled Tenants probe failure,
    and healthy readiness with Tenants disabled when the other ready checks
    pass. Pub/sub, Memories and projection degradation alone must leave the
    ready endpoint healthy. Prove that the
    receiver accepts authenticated Dapr callbacks and refuses an invalid
    application channel. Gateway operations also require a valid workload
    assertion for the intended service and operation. Exercise both acceptance
    and refusal at the receiver.
    [Readiness checks](../../../src/Hexalith.Parties/HealthChecks/PartiesHealthCheckExtensions.cs) · [Domain host](../../../src/Hexalith.Parties/Program.cs)
- **CAP-5** — UI consolidation (`8.9-frontcomposer-ui-consolidation`)
  - **intent:** Adopt the remaining G4 FrontComposer picker, freshness/status,
    safe download and typed confirmation primitives. Parties retains ownership
    of GDPR wording. The shell slice was delivered on 2026-08-18.
  - **success:** Producer bUnit plus Parties bUnit/Playwright parity per
    slice, per the deferral's exit proof. For admin list and search, clear prior
    rows, counts, paging and detail on denial. For an admitted empty page,
    render its counts and paging and clear superseded detail, including when
    degraded. Prior rows and counts may remain only when the server
    validates that the same authorized representation is unchanged (same
    caller, tenant, operation, filters, search, sort, page and validator). The grid currently
    can preserve previous names on a changed search with degradation metadata.
    Resolve this component mismatch and exercise all three outcomes before
    accepting Story 8.9 UI parity.
    [Grid behavior](../../../src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor) · [Component expectations](../../../tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs)
- **CAP-6** — External runtime deployment (`external-runtime-deployment`)
  - **intent:** An external platform-ops owner runs Parties from immutable
    image tags with environment-specific DAPR configuration and promotion
    evidence.
  - **success:** Owner-repository consumption evidence plus a proven rollback
    to the prior immutable image set. For every service-invocation grant, compare
    caller app ID, namespace, trust domain, operation path, HTTP verb, action
    and both deny defaults with the canonical ACL at the selected revision.
    Record the canonical and exercised identities for isolated tests. Record
    any deployment change to the grants through I1/I20.
    [Canonical ACL](../../../src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml)

## Constraints

- Spine invariants I1–I20, including I1a and I19a, bind every slice. Record a conflict for its owner to resolve.
- I17: activating any deferral — or any deletion-heavy spec, whatever its
  label — requires a spec declaring all six §4 gate clauses first; the spine's
  §5 order binds the deferrals (CAP-2 → CAP-3 → CAP-4 → CAP-5).
  For an item without a declared parent, map all touched surfaces to their
  accepted rollback, exit-proof and §7 obligations. Assign the latest
  applicable slot across those boundaries and wait for its predecessors.
  Schedule the item after 8.9 if none applies. The affected deferral owners
  resolve ambiguous mappings before the work starts.
- **I16/I18/I20.** Before deleting a rollback surface, name the affected
  behavior and its §7 baseline check. If a baseline check was retired, map its
  behavior to a supported product test and obtain the accountable owner's
  approval for that successor in the Story 8.3 I20 table. Run the applicable
  checks at the selected source and package identities and record the results.
  The successor-test row is pending. Deletions relying on a retired check
  remain blocked until the approval and passing evidence are recorded.
  [Approval table and prerequisites](../../implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md) · [Product checks](../../../docs/ci.md#product-boundary)
- **I15.** Every slice remains maintenance work with zero new PRD functional requirements.
- Run focused xUnit v3 checks against the built test assembly. A reopened
  closure requires fresh receipts at the qualifying identities under I16. Before
  recording closure as done again, the Release build and Playwright accessibility
  receipts must both report Pass.
  [Verification procedure](../../../docs/ci.md#local-verification)

## Non-goals

- No re-derivation or rollback of the ratified stories 8.1–8.10.
- No in-repo runtime deployment orchestration — the assets Story 8.13 retired
  stay retired.
- No deletion of `Hexalith.Parties.Authentication` outside CAP-4's gated
  supersession.
- No domain-feature expansion; maintenance only.

## Success signal

The six Round 10 receipts establish historical closure at `c7dd607b`.
To complete remaining work, the owning story must satisfy its deferral’s
exit proof or obtain the recorded owner’s re-approval under the spine.
Each proposed retirement must independently satisfy I16/I18/I20 for its
affected behavior and exercise its rollback.

## Assumptions

- The G-row platform APIs (G4/G5/G6) land under their EventStore/FrontComposer
  owners as recorded in the 8.3 matrix; capability order follows spine §5.

## Decision status and open questions

- **OQ-1 · Freshness.** I10 assigns the versioned freshness grammar, including
  `ProjectionVersion`, to EventStore.Contracts when G6 becomes available.
  Consumers use the existing Parties compatibility mapping until then.
- **OQ-2 · Memories cleanup.** I19 classifies the party-memory mapping inventory
  as an operational ledger that survives projection rebuilds. DW-81 records the
  shipped conditional operations on `IReadModelStore`. The owner decision on
  this storage interface and the I20-approved consistency tests are still due
  before a migration relies on them.
  [DW-81 evidence](../../implementation-artifacts/deferred-work-archive.md) · [Approval table](../../implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md)
- **OQ-3 · Production custody.** GATE-KMS remains unverified. Before processing
  real regulated EU personal data, the deployment owner must prove rejection
  of ephemeral storage outside Development and replay with durable keys after
  host replacement and on a second replica. I7/I8 also require verified erasure
  through committed terminal state and refusal of retired key versions across
  replicas. I19 classifies projection and operational records. The per-party
  key provider holds authoritative secrets and wrapping metadata. Before a
  provider migration, the custody and storage owners must record the applicable
  I19 disposition and consistency tests through I20 alongside I8/GATE-KMS.
  That disposition is pending. Story 8.7 retains its G5 prerequisite and rollback.
  [Deployment gate](../../planning-artifacts/parties-ui-prd.md#deployment-gates) · [Erasure qualification](../../planning-artifacts/sprint-change-proposal-2026-11-20.md)
- **OQ-4 · AppHost cutover.** The first CAP-3 or CAP-4 activation must record
  one I20 decision for both. Demonstrate that the successor can read
  predecessor payloads and cursors, or specify the invalidation window and
  observable failure. The second activation inherits that decision.
- **OQ-5 · Approvals.** I20 defines the mechanism for I1a, I2, I5 and I18.
  The Story 8.3 row must give the decision, artifact, named accountable human
  and date, recorded by that human or the reviewer gate. Each pending row
  gates its corresponding action.
