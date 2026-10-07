# Epic 8 Context: Domain-Focus Refactoring and Platform Extraction (Class C)

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Extract platform mechanics while preserving Parties domain behavior, policy, clients, UI, and samples. This maintenance adds no PRD requirements. Epic 8/Story 8.10 closed through deferrals on 2026-10-05; Stories 8.7–8.9 stay blocked with rollback retained. Live `sprint-status.yaml` remains authoritative.

## Stories

- Story 8.1: Baseline and release-blocker stabilization
- Story 8.2: Identifier correctness and zero-risk hygiene
- Story 8.3: Platform API prerequisites
- Story 8.4: Leaf-project retirement
- Story 8.5: EventStore domain-service SDK host cutover
- Story 8.6: Projection and query SDK migration
- Story 8.7: Data-protection extraction
- Story 8.8: Client, MCP, AppHost, build, and runtime-boundary cleanup
- Story 8.9: UI FrontComposer and Fluent consolidation
- Story 8.10: Final readiness, documentation, and retirement gate
- Story 8.11: Validation fallback ladder runner and guidance
- Story 8.12: Parties-only Zot container publish CI
- Story 8.13: Retire legacy in-repo deployment artifacts

## Requirements & Constraints

Preserve command/query behavior, tenant isolation, `aggregateId == party_id` own-data checks, public Client/Contracts and UI RCL compatibility. Breaking contracts require approved versioning. Diagnostics remain PII-free and low-cardinality.

Migration/deferral specs must name prerequisites, touched repositories, exercised rollback, validation lanes, non-goals, and parity checklist. Split or hard-gate broad migrations. Prove parity before deletion; retain baseline tests absent approved successors, canonical sprint keys, and `blocked` status.

Evidence names exact consumed packages/gitlinks. Identity movement invalidates affected claims and stops covered deletion until revalidation. Approval requires a prerequisite-ledger row naming decision, artifact, human, and date, written by that human/reviewer gate. Selection/compile never imply parity or adoption approval.

Dated 2026-10-05 approved selections:

| Dependency | Root gitlink | Package |
| --- | --- | --- |
| EventStore | `865cd9e49273dffbb1cdae85efeaf1aac322e09e` | `3.113.0` |
| Builds | `360a2b9c4e96809365a7de785be9a68152d5ac28` | — |
| Commons HTTP | `116d26815eb81e35b3c161e1799e5ee12805fc0a` | fallback `2.30.1` |
| FrontComposer | `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` | `4.5.0` |
| Memories | `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` | `2.27.1` |
| Tenants | `72b8e4f508176b69826549e87b7b2a286f607fd1` | `5.7.0` |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | — |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | — |

Later Builds, FrontComposer, Memories, and Tenants gitlinks differ. Observed Builds `90f3836dd7482db35c2c187a50999b99215919b0` selects EventStore `3.113.0`; this grants no approval. Revalidate against the actual graph.

Keep .NET 10, `.slnx`, CPM, warnings-as-errors, MinVer, root-only submodules, and established validation gates. Run xUnit v3 projects individually; focused runs invoke built assemblies. Regulated production data requires production KMS/secret-backed keys.

## Technical Decisions

Continue adapter-first extraction: EventStore owns hosting/projection/query/envelope mechanics; Commons owns utilities/paging; FrontComposer owns UI mechanics; Builds owns shared build logic. Parties retains semantic policy, compatibility mapping, and singly defined Contracts anchors.

The host has no public API. DAPR remains deny-default/EventStore-only; subscriptions stay separate. Approve route changes; external ACLs attest equivalent tuples. AppHost retirement requires topology, security, publish, rollback, and dependent-deferral proof. Runtime orchestration stays external; Parties publishes immutable images.

Preserve replay/checkpoints, idempotency, duplicate/out-of-order tolerance, rebuild-vs-replay proof, last-known fallback, versioned freshness, and permanent tombstones. Identifier acceptance never authorizes tombstoned allocation. Classify stores as rebuildable read models or operational ledgers; ledgers survive rebuilds with consistency tests and obey erasure.

G5 payload protection remains `needs-additive-api`; available key-ring/cursor DataProtection APIs do not authorize engine adoption. Parties alone decides erasure, restriction, personal-data classification, and lawful basis. Stateless policy/erasure hooks read aggregate/event-stream state; owner approval must classify them before G5 adoption. Preserve `json+pdenc-v1`, `json-redacted`, legacy reads, key zeroing, typed unreadability, no-leak diagnostics, exports, processing records, and one versioned certificate/report shape. Erasure has Admin and Consumer doors; MCP deletion is soft-deactivation.

Before crypto deletion, decide backend/readability window. One approval binds security and AppHost cutover: prove predecessor key-ring, cursor-purpose, and payload continuity or approve explicit invalidation with typed outcomes. McpCli replacement or approved withdrawal proves parity before module MCP retirement.

## UX & Interaction Patterns

Inherit FrontComposer and Fluent V5/Fluent 2; purge FAST/v4 tokens. Preserve WCAG 2.2 AA, keyboard/pointer parity, focus, skip links, forced colors, reduced motion, non-color cues, destructive confirmation, polite status/assertive errors, and no optimistic focus stealing. I13 remains deferred: DW-111 accepts missing runtime content-control proof without certifying parity.

Stale/degraded reads show last-known data; acceptance never promises read-your-write. Parties owns legal strings: consent versus lawful basis, restriction allowing consent edits except during erasure, cancellation versus permanence, and honest export timing.

## Cross-Story Dependencies

Order: `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`; 8.5–8.7 require platform readiness. Deferrals/children inherit `8.6 -> 8.7 -> 8.8 -> 8.9`, taking the later parent slot. Concurrency requires disjoint repositories/parity invariants beyond Parties. Deferral closure never authorizes migration/deletion.
