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

The 2026-10-05 prerequisite-ledger selections are dated approvals. Later package/gitlink identities are observations, never successor approvals; revalidate against the actual consumed graph and approved identity stamps.

Keep .NET 10, `.slnx`, CPM, warnings-as-errors, MinVer, root-only submodules, and established validation gates. Run xUnit v3 projects individually; focused runs invoke built assemblies. Regulated production data requires production KMS/secret-backed keys.

## Technical Decisions

Continue adapter-first extraction: EventStore owns hosting/projection/query/envelope mechanics; Commons owns utilities/paging; FrontComposer owns UI mechanics; Builds owns shared build logic. Parties retains semantic policy, compatibility mapping, and singly defined Contracts anchors.

The host has no public API. DAPR remains deny-default/EventStore-only; subscriptions stay separate. Approve route changes; external ACLs attest equivalent tuples. AppHost retirement requires topology, security, publish, rollback, and dependent-deferral proof. Runtime orchestration stays external; Parties publishes immutable images.

Preserve replay/checkpoints, idempotency, duplicate/out-of-order tolerance, rebuild-vs-replay proof, last-known fallback, versioned freshness, and permanent tombstones. Identifier acceptance never authorizes tombstoned allocation. Classify stores as rebuildable read models or operational ledgers; ledgers survive rebuilds with consistency tests and obey erasure.

G5 payload protection remains `needs-additive-api`; available key-ring/cursor DataProtection APIs do not authorize engine adoption. Parties alone decides erasure, restriction, personal-data classification, and lawful basis. Stateless policy/erasure hooks read aggregate/event-stream state; owner approval must classify them before G5 adoption. Preserve `json+pdenc-v1`, `json-redacted`, legacy reads, key zeroing, typed unreadability, no-leak diagnostics, exports, processing records, and one versioned certificate/report shape. Erasure has Admin and Consumer doors; MCP deletion is soft-deactivation.

Consumer login issuer/subject-to-PartyId aliases remain private IdP/UI/BFF routing with one verified `party_id` claim and external provisioning audit; they never become Parties event-stream identity evidence. Opaque Human ActorId attribution is a separate, independently authorized Party-history contract. Parties owns bindings; Platform owns independent actor authority/custody; EventStore owns the protected source seam. Past attribution grants no present authority; current eligibility requires active Party and current actor authority. Organization Branch B has no human binding.

The approved `party-actor-retention-v1` policy retains minimal opaque lifecycle/provenance for 365 fixed days from `binding-effective-at`, including bounded post-profile-erasure attribution. Protect it independently from profile data; exclude login mappings, profile payloads, credentials and role claims. Expiry is exclusive and never restarts on closure, rebind, erasure, restore or retry; successors use their own effective instant. Policy changes cannot extend existing records. Reads/writes require matching finite policy and fresh independent purpose custody; changed configuration fails closed.

Production history remains disabled pending qualification of the selected custody/source, copy inventory, authenticated cleanup/restore proof and complete owner acceptance. Expiry denies reads immediately; cleanup must destroy decryption capability and identity-bearing derived copies across caches, replicas, backups and exports. Restore checks fresh durable lifecycle before decrypting; missing proof makes reads unavailable and cleanup pending. Shared seams, applied numeric policy and synthetic tests establish neither production qualification nor G5 adoption approval.

Before crypto deletion, decide backend/readability window. One approval binds security and AppHost cutover: prove predecessor key-ring, cursor-purpose, and payload continuity or approve explicit invalidation with typed outcomes. McpCli replacement or approved withdrawal proves parity before module MCP retirement.

## UX & Interaction Patterns

Inherit FrontComposer and Fluent V5/Fluent 2; purge FAST/v4 tokens. Preserve WCAG 2.2 AA, keyboard/pointer parity, focus, skip links, forced colors, reduced motion, non-color cues, destructive confirmation, polite status/assertive errors, and no optimistic focus stealing. I13 remains deferred: DW-111 accepts missing runtime content-control proof without certifying parity.

Stale/degraded reads show last-known data; acceptance never promises read-your-write. Parties owns legal strings: consent versus lawful basis, restriction allowing consent edits except during erasure, cancellation versus permanence, and honest export timing.

## Cross-Story Dependencies

Order: `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`; 8.5–8.7 require platform readiness. Deferrals/children inherit `8.6 -> 8.7 -> 8.8 -> 8.9`, taking the later parent slot. Concurrency requires disjoint repositories/parity invariants beyond Parties. Deferral closure never authorizes migration/deletion.
