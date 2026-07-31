# Epic 8 Context: Domain-Focus Refactoring and Platform Extraction (Class C)

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 8 removes reusable platform infrastructure from Hexalith.Parties after ownership, compatibility, rollback, and validation are proven, leaving the module focused on aggregates, contracts, validators, domain projection/query semantics, Parties-specific GDPR policy, typed domain clients, domain UI, MCP tool definitions, and samples. It is approved post-MVP maintenance that improves domain boundaries, observability, build quality, and maintainability; it adds no PRD functional requirements and must not be reported as feature delivery.

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
- Story 8.12: Parties-only Zot container publish CI
- Story 8.13: Retire legacy in-repo deployment artifacts

## Requirements & Constraints

Preserve completed command/query behavior, tenant isolation, self-scoped consumer authorization, public Client and Contracts shapes, the Picker/AdminPortal/ConsumerPortal packages, and all GDPR legal semantics. Protected data must remain compatible with encrypted, redacted, legacy-unprotected, key-zeroed, and typed-unreadable states without diagnostic leakage; exports, processing records, erasure reports, and certificates must continue to behave consistently.

Deletion-heavy stories require a declared prerequisite set, touched repositories, rollback path, explicit non-goals, validation lanes, and a parity checklist before development. Broad projection/query, data-protection, and runtime-boundary changes must be split or hard-gated. Local projection, query, crypto, AppHost, and release-recovery paths remain available until their replacements have both parity evidence and a proven rollback; rollback-only code must not be deleted merely because the forward path compiles.

No Parties migration may consume an unapproved or unidentified dependency. Each prerequisite must be owner-approved as an additive API or proven already available, and every `available` item must identify the exact released package version or root-declared submodule gitlink selected by the consumer. The actual dependency mode and identity must match that record; a status label, source path, or checked-out file is not consumption evidence.

## Technical Decisions

Runtime traffic continues through the deny-by-default EventStore gateway and DAPR `POST /process`; migration must not add public actor-host APIs. The host uses the EventStore domain-service SDK shape, retaining only domain registrations, Parties policy, and payload-protection hooks that the platform cannot own. The target state has no domain-owned AppHost: the current AppHost may remain as a migration/rollback surface until an approved integrated platform topology proves security, publish, topology, and rollback parity.

Projection folds target `IDomainProjectionHandler`; queries target `IDomainQueryHandler`; read-model writes use `IReadModelStore` with `ReadModelWritePolicy`; cursors use `IQueryCursorCodec`. Before `AddEventStoreDataProtection`, `DaprXmlRepository`, or the cursor-codec path is consumed, the EventStore DataProtection prerequisite identity must match the selected EventStore release or root gitlink.

Projection/query parity must cover replay from zero, per-actor sequence checkpoints, set-based idempotency, duplicate and out-of-order delivery, stale/degraded last-known reads with freshness metadata, erased-party exclusion, GDPR processing-record reads, and rebuild output verified against aggregate replay. A full rebuild must be executed and verified before local actors, companion sequence keys, rebuild services, adapters, or remoting fallback control flow are deleted. Rollback must replace the EventStore SDK path, not merely prove that retained local code is safe in isolation.

Generic crypto, key storage/wrapping/rotation, retry, circuit-breaker, and unreadable-payload mechanics move behind EventStore/shared security contracts; Parties retains domain policy and erasure orchestration. Shared envelopes, freshness/paging models, ProblemDetails scrubbing, MCP plumbing, security/module helpers, build-root logic, and runtime concerns move only when their owning platform surfaces meet the same identity and parity gates. Runtime deployment orchestration remains externally owned; this repository retains workload source, CI, and Parties container publication.

## UX & Interaction Patterns

UI work is conformance-only. FrontComposer and Fluent UI V5/Fluent 2 remain authoritative, local parallel status/freshness/reconcile/navigation/picker primitives are removed only with behavior proof, and legacy FAST/v4 tokens must be purged. Preserve WCAG 2.2 AA behavior, keyboard/pointer parity, visible focus, forced-colors and reduced-motion support, semantic controls, typed destructive confirmation, polite/assertive live-region separation, non-color state cues, and no focus stealing during optimistic updates.

Stale or degraded reads show last-known values and freshness cues rather than blanking or throwing. GDPR interactions must avoid consent dark patterns, distinguish cancellable pending erasure from permanent completed erasure, and avoid promising unsupported export timing.

## Cross-Story Dependencies

The recorded main sequence is `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`; Stories 8.5-8.7 depend on the platform readiness established by Story 8.3, and Story 8.10 closes or explicitly defers remaining work with owners and evidence. Epic 8 starts from Epic 7 readiness, adapters, compatibility harnesses, and rollback records rather than rewriting that completed history.

Story 8.12 publishes only the Parties-owned images with immutable version tags and no runtime deployment. Story 8.13 retires legacy in-repository deployment artifacts only once that publication path exists, leaving production manifests, DAPR components, ingress, secrets, scans, signatures, and promotion gates to the external deployment owner.
