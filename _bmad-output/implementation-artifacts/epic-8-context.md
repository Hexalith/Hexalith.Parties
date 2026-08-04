# Epic 8 Context: Domain-Focus Refactoring and Platform Extraction (Class C)

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 8 removes remaining reusable platform infrastructure from Hexalith.Parties so the module contains only Parties domain substance: aggregates, contracts, validators, projection/query semantics, GDPR policy, typed domain clients, domain UI, MCP tool definitions, and samples. Platform mechanics move to their owning shared modules only after ownership, compatibility, rollback, and validation are proven. This is approved post-MVP maintenance that adds no PRD functional requirements and must not be reported as feature delivery.

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

Preserve command/query behavior, tenant isolation, self-scoped consumer authorization including aggregate-to-party identity checks, public Client and Contracts shapes, the Picker/AdminPortal/ConsumerPortal packages, and GDPR legal semantics. Protected data must remain compatible with encrypted, redacted, legacy-unprotected, key-zeroed, and typed-unreadable states without leaking personal data through diagnostics. Exports, processing records, erasure reports, and certificates must retain their behavior.

Deletion-heavy work requires prerequisites, touched repositories, rollback, validation lanes, non-goals, and a parity checklist in its story specification before development. Broad projection/query, data-protection, and runtime-boundary changes must be split or hard-gated. Local rollback paths remain until the replacement has parity evidence and a proven rollback; compilation alone is not deletion evidence.

No Parties migration may consume an unapproved or unidentified dependency. Each prerequisite must be owner-approved as an additive API or identified as an available surface with the exact released package version or root-declared submodule gitlink selected by the consumer. The dependency mode and identity used by the implementation must match that record.

Validation must preserve .NET 10, `.slnx`, central package management, warnings-as-errors, root-only submodules, and the established xUnit v3, Shouldly, NSubstitute, bUnit, topology, package/API, deployment, and Playwright accessibility lanes. Multi-project fallback validation must attempt all configured projects when continue-after-failure is enabled, report every result, and fail if any project fails.

## Technical Decisions

Traffic continues through the deny-by-default EventStore gateway and DAPR `POST /process`; no public actor-host API may be introduced. The host targets the EventStore domain-service SDK shape, with Parties retaining only domain registrations, Parties-specific policy, and payload-protection hooks that the platform cannot own.

Projection folds target `IDomainProjectionHandler`, queries target `IDomainQueryHandler`, read-model writes use `IReadModelStore` with `ReadModelWritePolicy`, and pagination uses `IQueryCursorCodec`. Preserve replay from zero, per-actor checkpoints, set-based idempotency, duplicate/out-of-order tolerance, erased-party exclusion, permanent identifier tombstones, freshness metadata, and last-known stale/degraded reads. A full rebuild must match aggregate replay before local projection, query, rebuild, sequence-key, adapter, or remoting-fallback mechanics are deleted.

Generic payload protection, key storage, wrapping, rotation, audit, retry, circuit-breaker, and unreadable-payload mechanics move behind EventStore or shared-security contracts. Parties retains its commands, legal policy, erasure orchestration, domain-specific reports/certificates, and UX/copy obligations. Valid ULID-compatible aggregate identifiers must be accepted while GUID-shaped replay compatibility remains; new identifiers use the approved Commons helper where required.

Shared command envelopes, paging/freshness models, ProblemDetails scrubbing, MCP plumbing, security/module helpers, HTTP helpers, and build-root logic belong to their platform owners. A Parties AppHost may remain only as a migration/rollback surface; it is retired after an approved integrated platform topology proves security, publishing, topology, and rollback parity. Runtime deployment orchestration stays external, while this repository retains workload source, CI, and Parties-owned container publication. Published Parties images use immutable version tags without applying deployment manifests; obsolete in-repository Kubernetes, DAPR, Zot, and deployment-validation assets are retired after that publication path exists.

## UX & Interaction Patterns

UI work is conformance-only. FrontComposer and Fluent UI V5/Fluent 2 remain authoritative; local parallel freshness, status, reconciliation, navigation, picker, and test primitives may be removed only with behavior proof. Purge legacy FAST/v4 tokens and preserve WCAG 2.2 AA behavior: semantic controls, keyboard/pointer parity, visible focus, skip links, forced-colors, reduced-motion, non-color state cues, typed destructive confirmation, polite status versus assertive error announcements, and no focus stealing during optimistic updates.

Stale or degraded reads show last-known values and freshness cues rather than blanking or throwing. Preserve honest GDPR interactions: consent is not conflated with other lawful bases, exports make no unsupported timing promise, and cancellable pending erasure is clearly distinct from permanent completed erasure.

## Cross-Story Dependencies

Epic 8 starts from Epic 7 readiness, compatibility, and rollback evidence. The recorded core sequence is `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`; Stories 8.5-8.7 require Story 8.3 platform readiness, and Story 8.10 closes or explicitly defers remaining work with owners and evidence. Story 8.13 follows the Parties-only publication capability in Story 8.12 so legacy deployment assets are removed only after the replacement boundary is established.
