# Epic 8 Context: Domain-Focus Refactoring and Platform Extraction (Class C)

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Move reusable platform mechanics out of Parties while preserving its domain behavior, public contracts, GDPR policy, and user experience. Epic 8 is approved post-MVP maintenance with zero new PRD functional requirements. Completion or accepted deferral does not establish feature delivery, production readiness, or permission to remove retained implementations.

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

Preserve command/query behavior, tenant isolation, consumer own-data authorization (`aggregateId == party_id`), and public Client, Contracts, and UI RCL compatibility. Breaking changes require an approved versioning plan. Logs, traces, exceptions, and ProblemDetails must remain free of personal data, payloads, key material, and unbounded identifiers. Production protection requires KMS or secret-backed keys; development backends provide no production qualification.

Every remaining migration and accepted-deferral activation needs a spec declaring prerequisites, touched repositories, an exercised rollback, specific validation lanes, non-goals, and a parity checklist. Split or hard-gate broad cross-module work. Retain local implementations and baseline tests until the replacement proves compatibility and rollback. Unrun or skipped tests confer no proof. Stamp receipts with the exact consumed package release or root gitlink; dependency movement invalidates affected claims until revalidated. Record approvals in the prerequisite ledger with the decision, artifact, named human, date, and authorized provenance. Source presence, selection approval, and successful compilation do not establish adoption or parity approval.

Keep .NET 10, modern `.slnx` solutions, central package management, warnings as errors, MinVer, and root-only submodules. Run xUnit v3 projects individually and use built assemblies for focused filters. The separately governed external-consumer identity extension adds no Epic 8 story or PRD coverage.

## Technical Decisions

Use adapter-first extraction. EventStore owns domain-service hosting, projection/query mechanics, envelopes, and shared payload protection; Commons owns cross-cutting HTTP and paging; FrontComposer owns UI mechanics; Builds owns shared build logic. Parties keeps aggregates, domain contracts, query meaning, typed domain clients, GDPR policy, and legal wording. Shared Contracts anchors have one definition.

The domain-service host exposes no public API. EventStore gateway access and DAPR invocation ACLs remain deny-default and EventStore-only with owner-approved routes; subscription delivery is a separately governed ingress. Preserve replay-from-zero, sequence checkpoints, duplicate/out-of-order tolerance, last-known degraded reads, versioned freshness, erased-party exclusion, permanent tombstones, and rebuild-versus-aggregate-replay proof. Classify each touched store as a rebuildable read model or an operational side-effect ledger; ledgers survive rebuilds, carry consistency tests, and still obey erasure.

Before consuming `AddEventStoreDataProtection`, `DaprXmlRepository`, or query cursor codecs, match the EventStore DataProtection ledger row to the exact selected release or root gitlink. Revalidate cursor purpose/scope compatibility, persisted DAPR key rings, and rollback at that identity. This gate is separate from shared payload-engine G5 approval.

Shared crypto adoption requires an approved consumable provider, backend, release identity, policy-hook classification, dual-provider parity, and exercised switch-back after shared-provider writes. Cursor/key-ring DataProtection APIs and an internal cryptographic core do not satisfy that gate. Parties alone writes erasure, restriction, personal-data classification, and lawful basis; shared hooks must read aggregate/event-stream state without keeping or changing policy state. Preserve legacy plaintext, protected and redacted histories, key zeroing, typed unreadable outcomes, no-leak diagnostics, Art.20 exports, Art.30 records, and one versioned erasure certificate/report shape. Seven recorded proofs cover payload compatibility, typed failures, diagnostics, exports, records, certificates, and rollback before crypto deletion. Decide predecessor key-ring, cursor-purpose, and payload readability across a topology cutover, or approve an explicit invalidation window and typed outcomes.

Admin and self-scoped Consumer erasure use the same aggregate commands and verification semantics. MCP soft deactivation must not become an erasure door. McpCli is the target shared MCP/CLI surface; retire module presentation only after contract enrollment, authorization, replacement parity, or approved withdrawal.

Retain the Parties AppHost until the approved integrated topology and every dependent rollback obligation pass. G8 requires authentication proof for local run and publish/external-orchestrator configuration: authority, issuer, workload and EventStore audiences, HTTPS metadata, signing-key clearing, and secret-free publish manifests. Delivered registration symbols grant no adoption approval. Prove granular command/query/GDPR registration, Parties/FrontComposer typed-client coexistence, deterministic ordering/re-registration, exact producer/consumer library and host identities, and rollback. Record preserve-or-replace decisions for MCP/UI workloads and Docker/Kubernetes/ACA targets before cutover. Runtime deployment belongs to the external orchestrator; Parties owns source, local topology, CI, and its container publication. Container publication does not discharge production deployment, security, or promotion obligations.

## UX & Interaction Patterns

Use FrontComposer and Fluent UI V5/Fluent 2, preserving WCAG 2.2 AA keyboard/pointer parity, visible focus, skip links, forced colors, reduced motion, semantic controls, non-color status cues, and safe destructive confirmation. Remove FAST/v4 tokens only with conformance evidence. Keep polite status announcements distinct from assertive errors and preserve focus during optimistic updates. Runtime content-control focus and accessibility parity remain open under the UI deferral.

Stale/degraded reads show last-known data and honest freshness without implying immediate read-your-write. Parties owns consent-versus-lawful-basis wording, consent edits allowed during restriction and rejected during erasure, cancellation-versus-permanence distinctions, and realistic export timing; shared components render those semantics.

## Cross-Story Dependencies

The migration order is `8.1 → 8.2 → 8.3 → 8.4 → 8.5 → 8.6 → 8.7 → 8.8 → 8.9 → 8.10`; 8.5–8.7 require platform API readiness. Accepted deferrals and child work inherit the `8.6 → 8.7 → 8.8 → 8.9` order and relevant rollback gates. Closing an umbrella under accepted deferrals does not complete its blocked migrations or authorize deletion.
