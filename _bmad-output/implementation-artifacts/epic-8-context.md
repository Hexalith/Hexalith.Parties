# Epic 8 Context: Domain-Focus Refactoring and Platform Extraction (Class C)

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Move reusable platform mechanics out of Parties while preserving its domain behavior, public contracts, GDPR policy, and user experience. This is post-MVP maintenance with no new PRD functional requirements. It makes the module domain-focused without expanding MVP scope.

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

Preserve command/query behavior, tenant isolation, consumer own-data checks (`aggregateId == party_id`), and the public Client, Contracts, and three UI RCL shapes. Breaking changes need an approved versioning plan. Keep diagnostics free of personal data, payloads, key material, and high-cardinality identifiers. Production protection requires KMS or secret-backed keys.

Each remaining migration or deferral activation needs a spec naming prerequisites, touched repositories, an exercised rollback, validation lanes, non-goals, and a parity checklist. Split or hard-gate broad cross-module work. Retain baseline test surfaces and local rollback code until replacement parity is executable; do not count unrun or skipped tests as proof. Pin evidence to the exact consumed release or root gitlink. Moving a dependency identity invalidates affected parity claims until revalidated. Required approvals belong in the prerequisite ledger with a decision, artifact, named human, and date; observed code or package availability is not adoption approval.

Maintain .NET 10, `.slnx`, central package management, warnings as errors, root-only submodules, and MinVer. Run xUnit v3 projects individually and use built assemblies for focused filters. Epic 8 remains maintenance scope; the separately governed external consumer identity extension adds no Epic 8 story or PRD coverage.

## Technical Decisions

Extract through compatible adapters: EventStore owns domain-service hosting, projection/query mechanics, envelopes, and shared payload protection; Commons owns cross-cutting HTTP and paging helpers; FrontComposer owns UI mechanics; Builds owns common build logic. Parties keeps aggregate and GDPR policy, query meaning, typed domain clients, legal copy, and singly defined Contracts anchors. Retire local paths only after owner APIs, parity, and rollback are proven.

The domain-service host has no public API. Gateway access and DAPR ACLs remain deny-default and EventStore-only; subscription delivery is a separate ingress. Route changes need recorded owner approval. Keep replay-from-zero, checkpoints, idempotency, duplicate/out-of-order tolerance, last-known degraded reads, versioned freshness, erased-party exclusion, permanent tombstones, and rebuild-versus-aggregate-replay proof. Classify touched stores as rebuildable read models or operational ledgers; ledgers survive rebuilds, have consistency tests, and still obey erasure.

Generic key storage, wrapping, rotation, audit, retry, circuit breaking, and typed unreadable mechanics belong behind an approved shared provider. Parties alone decides erasure, restriction, personal-data classification, and lawful basis; shared policy hooks may only read aggregate/event-stream state. The payload engine prerequisite is still closed: key-ring and cursor DataProtection APIs, or an internal cryptographic core, do not establish a consumable provider. Until the approved runtime/backend, release identity, policy-hook classification, dual-provider parity, and an exercised switch-back after shared-provider writes exist, retain the local engine and DI. Preserve `json+pdenc-v1`, `json-redacted`, and legacy unprotected reads, key zeroing, typed failures, tenant isolation, no-leak diagnostics, exports, processing records, and one versioned erasure certificate/report shape. Decide predecessor key-ring, cursor-purpose, and payload readability across any AppHost cutover, or approve explicit invalidation with typed outcomes.

Keep the two erasure doors, Admin and Consumer, on the same aggregate commands and verification semantics; MCP `delete_party` remains soft deactivation. Replace module MCP presentation through McpCli only with contract parity or approved withdrawal. Retain the Parties AppHost as a rollback surface until the approved integrated topology and dependent rollback obligations are proven. Runtime deployment stays with the external orchestrator; this repository owns source, local topology, CI, and Parties container publication.

## UX & Interaction Patterns

Use FrontComposer and Fluent UI V5/Fluent 2 and remove legacy FAST/v4 tokens. Preserve WCAG 2.2 AA keyboard and pointer access, visible focus, skip links, forced colors, reduced motion, non-color status cues, safe destructive confirmation, polite status versus assertive errors, and focus stability during optimistic updates. Runtime content-control focus and accessibility parity remain open under the UI deferral.

Stale or degraded reads show last-known data with honest freshness and never imply immediate read-your-write. Parties owns the legal wording: consent versus other lawful bases, consent edits during restriction but not erasure, cancellable requests versus permanent deletion, and realistic export timing.

## Cross-Story Dependencies

The migration order is `8.1 → 8.2 → 8.3 → 8.4 → 8.5 → 8.6 → 8.7 → 8.8 → 8.9 → 8.10`; 8.5–8.7 require platform API readiness. Accepted deferrals and child work inherit the `8.6 → 8.7 → 8.8 → 8.9` order. Deferral closure does not authorize migration or deletion.
