# Epic 8 Context: Domain-Focus Refactoring and Platform Extraction (Class C)

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Remove reusable platform infrastructure from Hexalith.Parties while retaining domain substance, policy, typed clients, UI, and samples. Mechanics move to their platform owners only after ownership, compatibility, rollback, and validation are proven. This approved post-MVP maintenance adds no PRD functional requirements and must not be reported as feature delivery.

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

Preserve command/query behavior, tenant isolation, consumer own-data checks including `aggregateId == party_id`, public Client/Contracts shapes, and Picker/AdminPortal/ConsumerPortal compatibility. Contract changes require approved versioning. Diagnostics remain PII-free and low-cardinality.

Every remaining migration or activated deferral needs a specification declaring prerequisites, all touched repositories, an exercised rollback, validation lanes, non-goals, and a parity checklist. Broad migrations must be split or hard-gated. Replacement adapters prove parity before deletion. Named baseline tests cannot be weakened; successors need owner approval.

Consumption and parity evidence name exact package versions or root gitlink identities matching the dependency mode. Identity movement immediately invalidates affected claims and stops covered deletions until revalidation, even without a written marker. Approvals require prerequisite-matrix table rows naming decision, artifact, human, and date, written by that human or the reviewer gate. Executors cannot manufacture approval; a missing table blocks approval-gated actions.

Keep .NET 10, `.slnx`, central packages, warnings-as-errors, MinVer, root-only submodules, and established unit, topology, package, and accessibility lanes. Run xUnit v3 projects individually; focused runs invoke built assemblies. Production regulated data still requires production KMS/secret-backed keys.

## Technical Decisions

Continue Epic 7's adapter-first migration. EventStore owns hosting, projection/query mechanics, envelopes, and freshness; Commons owns cross-cutting utilities and paging; FrontComposer owns UI mechanics; Builds owns shared build logic. Parties retains semantic policy and compatibility mapping. Shared Contracts anchors remain defined once; authentication retirement cannot authorize Contracts breakage.

The domain host has no public API. Gateway/DAPR invocation stays deny-by-default and EventStore-only; subscription delivery remains separate. Route changes need approval; external ACL copies attest equivalent policy. The SDK host retains domain registrations, Parties policy, and approved hooks.

Preserve replay from zero, checkpoints, set-based idempotency, duplicate/out-of-order tolerance, and rebuild-vs-replay verification. Use SDK projection/query handlers, read-model/write-policy and cursor seams. Freshness has one versioned grammar; pending the shared contract, emit Current/Stale/Unavailable with defined degraded metadata. Erased IDs remain tombstoned. ULID acceptance and GUID-shaped replay survive; allocation checks aggregate/event-stream tombstones.

Classify every touched store as an event-derived, rebuildable read model or an operational side-effect ledger retained across rebuilds with separate consistency tests. Personal-data ledgers still obey erasure. A ledger cannot use rebuildable persistence without approved reclassification and parity.

Generic payload protection and key-management mechanics move behind shared contracts. Parties alone writes erasure, restriction, personal-data classification, and lawful-basis policy. Policy/state adapters are stateless and read aggregate/event-stream state. Before adoption, owners must separate engine capabilities from Parties hooks. Preserve `json+pdenc-v1`, `json-redacted`, legacy reads, key zeroing, typed-unreadable outcomes, no-leak diagnostics, exports, processing records, and the single versioned certificate/report shape. Erasure has exactly Admin and Consumer doors; MCP deletion remains soft-deactivation.

`Hexalith.McpCli` is the CLI/MCP target through decorated Contracts and approved enrollment/authorization; replacement or approved withdrawal proves parity before module MCP retirement. AppHost retirement needs integrated topology, security, publish, rollback, and dependent-deferral proofs. Runtime orchestration is external; Parties publishes immutable image tags without deployment manifests.

## UX & Interaction Patterns

Use FrontComposer and Fluent V5/Fluent 2; purge FAST/v4 tokens. Preserve WCAG 2.2 AA semantics, keyboard/pointer parity, focus, skip links, forced colors, reduced motion, non-color cues, typed destructive confirmation, polite status/assertive errors, and no optimistic focus stealing.

Stale/degraded reads show last-known values and freshness; accepted commands never promise read-your-write. Parties owns legal strings: consent differs from lawful basis, restriction permits consent edits unless erasure is underway, cancellation differs from permanent erasure, and export copy makes no unsupported timing promise.

## Cross-Story Dependencies

Continue Epic 7 parity/rollback. Core order: `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`; 8.5-8.7 require platform readiness. Deferrals/children inherit `8.6 -> 8.7 -> 8.8 -> 8.9`, taking the later parent slot. Concurrency needs disjoint non-Parties repositories and touched-surface parity invariants. Final readiness closes or explicitly defers work with owners, proof, rollback, and evidence. Deployment retirement follows replacement publication.
