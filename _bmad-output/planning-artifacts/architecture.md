---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-03-03'
inputDocuments:
  - prd.md
  - prd-validation-report.md
  - product-brief-Hexalith.Parties-2026-03-01.md
  - Hexalith.EventStore/CLAUDE.md
  - Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md
  - Hexalith.EventStore/_bmad-output/planning-artifacts/architecture-documentation.md
  - Hexalith.EventStore/docs/concepts/architecture-overview.md
  - Hexalith.EventStore/docs/concepts/command-lifecycle.md
  - Hexalith.EventStore/docs/concepts/event-envelope.md
  - Hexalith.EventStore/docs/concepts/identity-scheme.md
workflowType: 'architecture'
project_name: 'Hexalith.Parties'
user_name: 'Jérôme'
date: '2026-03-02'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements: 74 FRs across 11 groups**

| Group | FRs | Architectural Significance |
|-------|-----|---------------------------|
| Party Lifecycle Management | FR1-FR7 | Single aggregate, type discrimination (person/org), client-generated UUIDs, display name derivation |
| Contact Channel Management | FR8-FR11 | Type-discriminated value objects (postal, email, phone, social), preferred channel logic |
| Identifier Management | FR12-FR13 | Jurisdiction-specific value objects (VAT, SIRET, national ID) |
| Party Discovery & Search | FR14-FR19, FR56, FR68, FR72 | CQRS read projection with search scoring intelligence (match metadata), eventual consistency, temporal queries (v1.1) |
| AI Agent Identity Resolution | FR20-FR25, FR74 | MCP server (5 tools), composite command orchestration, forgiving input schemas, patch semantics |
| Developer Integration | FR26-FR33, FR57-FR60, FR69 | NuGet packages (Contracts + Client), REST API, OpenAPI, sample integration, one-line DI |
| Event-Driven Integration | FR34-FR38, FR63, FR70, FR73 | DAPR pub/sub, CloudEvents, at-least-once delivery, causal ordering, erasure propagation |
| Multi-Tenancy & Security | FR39-FR43, FR61-FR62 | JWT tenant extraction, fail-closed, framework-enforced filtering, log sanitization, deployment validation |
| GDPR Compliance (v1.1) | FR44-FR55 | Crypto-shredding, per-party keys, consent management, restriction, portability, erasure verification |
| System Resilience | FR64, FR71 | Graceful degradation, health/readiness endpoints |
| Administration (v1.2) | FR65-FR67 | Admin portal, GDPR operations UI, embeddable party picker |

**Non-Functional Requirements: 33 NFRs across 6 categories**

| Category | NFRs | Key Architectural Drivers |
|----------|------|--------------------------|
| Performance | NFR1-NFR6 | Command < 1s, query < 500ms, rehydration < 200ms, cold start < 30s, projection consistency < 2s |
| Security | NFR7-NFR13 | TLS, per-party encryption (v1.1), zero cross-tenant leakage, JWT fail-closed, log sanitization |
| Scalability | NFR14-NFR19 | 100 tenants, 100K parties/tenant, 100 reads/s + 20 writes/s per tenant, < 10% degradation at scale |
| Reliability | NFR20-NFR24 | Crash recovery < 30s, cached reads on failure, zero event loss, at-least-once delivery, idempotent commands |
| Integration | NFR25-NFR29 | OpenAPI 3.x, MCP protocol, stable event schemas, < 10 transitive deps in client, zero infra dependencies |
| Developer Experience | NFR30-NFR32 | Deploy < 15 min, package < 5MB, XSS-safe frontend (v1.2) |

**Scale & Complexity:**

- Primary domain: Domain Microservice (API backend + developer tool hybrid)
- Complexity level: High — event sourcing, CQRS, DAPR, multi-tenancy, GDPR, 4 consumption surfaces, AI-native
- Estimated architectural components: ~14 packages/projects (aggregate, contracts, client, server, MCP orchestration layer, read projection infrastructure, projection data store, API, Aspire host, service defaults, testing, sample, integration tests, deployment validation)

### Technical Constraints & Dependencies

**Platform Foundation — Hexalith.EventStore:**
- Programming model: Pure `Handle(Command, State?) -> DomainResult` + `Apply(Event)` functions
- Convention-based aggregate discovery (reflection-based Handle/Apply method scanning)
- MediatR pipeline with FluentValidation behaviors
- DAPR building blocks: State store, pub/sub, actors, configuration store, service invocation
- Identity scheme: `tenant:domain:aggregateId` canonical form, all keys derived automatically
- Event envelope: 11 metadata fields, opaque payload, CloudEvents 1.0 publishing
- Actor model: AggregateActor with 5-step pipeline (idempotency, tenant validation, rehydration, domain invocation, persist-and-publish)
- Persist-then-publish with drain recovery on publish failure
- Snapshot support for rehydration performance

**Key dependencies (inherited from EventStore):**
- .NET 10 SDK
- DAPR SDK 1.17.0 (Client, AspNetCore, Actors)
- .NET Aspire 13.1.x
- MediatR 14.0.0
- FluentValidation 12.1.1
- OpenTelemetry 1.15.x
- xUnit + Shouldly + NSubstitute for testing

**Parties-specific constraints:**
- Single Party aggregate (not separate Person/Organization aggregates)
- `[PersonalData]` attribute infrastructure at MVP, encryption activation at v1.1
- Contracts package: zero runtime dependencies beyond netstandard2.1
- Client package: HTTP abstractions only, no DAPR/MediatR/FluentValidation
- MCP server: 5 composite tools designed for AI ergonomics, not 1:1 command mirrors
- REST API versioning: URL-path (`/api/v1/parties`)

### Critical Conflicts & Unresolved Questions

These items surfaced during collaborative analysis. Each requires an explicit architecture decision — they cannot be deferred to implementation.

**1. MCP Composite Command Latency Conflict (HIGHEST PRIORITY)**

NFR1 requires MCP tool calls complete in < 1 second end-to-end. But `create_party` maps to potentially 1 + N + M sequential commands (CreateParty + N x AddContactChannel + M x AddIdentifier), each requiring a full actor turn. At NFR1 per-command targets, creating a party with 3 channels and 2 identifiers = 6 commands = potentially 6 seconds — a 6x violation.

Options to resolve:
- **Composite aggregate command:** `CreatePartyComposite` that includes channels and identifiers in one payload, processed in a single actor turn. Cleanest, but requires validating that EventStore's convention discovery handles composite commands.
- **Batch command at API level:** Single HTTP request carrying multiple commands, processed in one actor activation. Requires EventStore batch support (unverified).
- **Relaxed MCP latency target:** Accept multi-second MCP operations. Violates NFR1 as written.

Architecture must resolve this before implementation begins. Composite aggregate commands also validate a useful EventStore pattern for future domain services.

**2. MCP Patch Semantics Orchestration**

`update_party` with patch semantics (FR74) requires the MCP layer to: (a) fetch current party state, (b) diff against requested changes, (c) determine which domain commands to issue (UpdatePersonDetails, AddContactChannel, RemoveContactChannel, etc.), (d) execute commands in correct order, (e) handle partial failures (3 of 5 commands succeed — what state is the party in?). This is non-trivial orchestration logic that needs explicit architectural design — error handling, rollback strategy (or lack thereof), and response semantics on partial failure.

**3. Personal Data Scope — Type-Dependent Classification**

`[PersonalData]` attribute placement depends on party type:
- **Person party:** PersonDetails fields (first name, last name, DOB, prefix, suffix) + all contact channel payloads + identifier values = clearly personal data
- **Organization party:** OrganizationDetails fields (legal name, trading name, legal form, registration number) = NOT personal data for corporations, but a sole trader's legal name IS personal data. Contact channel payloads on organizations (e.g., "jean@acme.com") = potentially personal data (identifies a natural person).
- **Derived fields:** Display name and sort name are derived from personal data and must participate in crypto-shredding.

Architecture must decide: **conservative** (encrypt all contact channels and identifiers on all party types — simpler, safer, slightly over-encrypts organization entity data) vs. **precise** (type-dependent encryption — more correct, adds conditional logic to crypto-shredding). Recommendation: conservative for v1.1 simplicity.

**4. Event Ordering — Broker-Dependent Guarantee**

FR73 requires causal ordering per aggregate to each subscriber. DAPR pub/sub ordering guarantees depend on broker configuration:
- Redis Streams: yes (within a consumer group)
- RabbitMQ: yes (per queue)
- Kafka: yes (per partition, with key-based routing configured)
- Some brokers: no ordering guarantees at all

Architecture must: (a) verify ordering guarantees for each supported deployment target, (b) document required broker configuration for ordering, (c) specify handler design requirements if ordering cannot be guaranteed (sequence-checking, order-tolerant projection updates).

**5. Projection Data Store — Unbounded Decision**

The read projection infrastructure is greenfield. The data store choice has fundamental implications:
- **DAPR state store queries:** Same infrastructure as EventStore, but DAPR state stores are key-value — limited query capability, no full-text search, no efficient "search by name contains 'dup'"
- **Dedicated query database:** Separate database (PostgreSQL, SQLite, etc.) behind the projection with full query capability. Different operational footprint, separate consistency management.
- **In-memory read model:** Rebuilt from events on startup. Fast queries, no persistence layer. But startup time grows with event count, and memory scales with party count.

Architecture must resolve this — it determines the projection component design, operational model, and scaling characteristics.

**6. Snapshot Rebuild Impact on Party Aggregate**

EventStore handles corrupted snapshots by rebuilding from the event stream. For a Party aggregate with hundreds of events (active party with many contact channel updates over time), full replay may violate NFR3 (rehydration < 200ms). Architecture should specify: forced re-snapshot interval, maximum acceptable event tail length, and in-flight command handling during rebuild.

### Cross-Cutting Concerns Identified

1. **Multi-tenancy** — Enforced at every layer: aggregate, event store, projections, API, MCP, pub/sub topics. Two distinct isolation mechanisms: **write-side isolation** (actor ID scoping — inherited from EventStore, structural) and **read-side isolation** (query-time tenant filtering — Parties must implement on projections, requires framework-enforced filtering base class and CI-automated negative tests with 10+ concurrent tenants).

2. **GDPR/Privacy** — `[PersonalData]` attributes, crypto-shredding, consent management, erasure verification, log sanitization. Structural, not bolt-on. Architecture must define the **complete personal data field inventory** with type-dependent classification (see Critical Conflicts #3) — this is an architecture decision, not just a development task.

3. **Event schema evolution & forward compatibility** — Append-only contracts, additive fields, tolerant deserialization, forward-compatible events (PartyMerged at MVP). Forward compatibility is a **first-order design principle**: the architecture must support additive evolution without breaking consumers at every layer (event schemas, API versions, NuGet packages, projection contracts, consent model).

4. **Infrastructure portability** — DAPR abstracts state store, pub/sub, secrets. Swap backends without code changes. Event ordering guarantees are broker-dependent (see Critical Conflicts #4).

5. **AI ergonomics & MCP orchestration** — MCP tools are a **primary consumption interface**, not just another API surface. They compose multiple domain commands into single operations with different validation rules (forgiving schemas vs. strict FluentValidation), return complete entities, and include match metadata for AI disambiguation. This orchestration layer is an architecturally distinct component requiring explicit design for composite command strategy (see Critical Conflicts #1), patch semantics (see Critical Conflicts #2), and partial failure handling.

6. **Read projection infrastructure (greenfield)** — The most significant architectural area that EventStore does **not** provide. Parties must build or adopt a projection framework. The read side is a separate service boundary with: its own data store (see Critical Conflicts #5), its own scaling characteristics, search scoring intelligence for match metadata (FR17), eventual consistency management, and tenant-filtered query enforcement. 9 FRs touch projections directly. The extensible projection contract (v1.1) means the architecture must support pluggable projection backends from the start.

7. **Observability** — OpenTelemetry tracing, structured logging with personal data exclusion, health/readiness endpoints, correlation IDs.

8. **Graceful degradation** — Fail-safe writes, cached reads, documented behavior per component failure.

9. **Developer experience** — NuGet package design (zero-dep Contracts, minimal-dep Client), one-line DI, getting-started guide, sample integration, OpenAPI, error catalog.

10. **Testability** — The write side inherits EventStore's test infrastructure (pure domain unit tests, actor integration tests). Three areas require Parties-specific test architecture:
    - **Projection testing** — No inherited test framework for the read side. Must test stateful, ordered event processing pipelines (e.g., `PartyCreated` → `ContactChannelAdded` → `ContactChannelUpdated` → `PartyErased`) with match metadata correctness and tenant isolation verification at the query layer.
    - **MCP composite operation testing** — Composite operations mapping single tool calls to multiple domain commands require end-to-end test coverage validating orchestration logic, forgiving input handling, partial failure scenarios, and complete response assembly.
    - **Event ordering verification** — Per-deployment-target verification that projection event handlers receive events in causal order, with fallback handler design tested if ordering is not guaranteed.

### Architectural Principles (from analysis)

1. **Platform validation, not workaround** — Parties must not work around EventStore limitations. If the aggregate pattern, projection model, or any EventStore abstraction doesn't fit, we fix EventStore — not Parties. Parties exists to *validate* the platform, and discovering gaps is a success outcome, not a failure.

2. **Forward compatibility by design** — Every contract (events, API, NuGet packages, projection interface, consent model) must support additive evolution without breaking consumers. Design for the v2 you can see (merge, relationships, Elasticsearch) without building it now.

3. **Aggregate size as first-order concern** — The Party aggregate with 50 contact channels, 10 identifiers, and consent records is a fundamentally different rehydration and snapshot challenge than EventStore's Counter sample. Snapshot strategy, aggregate state serialization performance, and command processing latency under realistic aggregate sizes are first-order architecture concerns.

4. **Conservative privacy by default** — When personal data classification is ambiguous (organization contact channels, sole trader legal names), default to treating data as personal. Over-encryption is a minor performance cost; under-encryption is a compliance failure.

## Starter Template Evaluation

### Primary Technology Domain

.NET domain microservice built on Hexalith.EventStore — technology stack fully inherited from the platform. No starter template selection needed; the "starter" is the EventStore solution structure pattern adapted for a domain service.

### Starter Options Considered

| Option | Description | Verdict |
|--------|-------------|---------|
| Generic .NET web API template (`dotnet new webapi`) | Standard ASP.NET Core scaffolding | Rejected — doesn't include DAPR, Aspire, or EventStore conventions |
| EventStore solution structure clone | Mirror EventStore's project layout, Directory.Build.props, Directory.Packages.props | **Selected** — ensures consistency, inherits all conventions, validates pattern reuse |
| Hexalith template/generator | Automated scaffolding from EventStore patterns | Doesn't exist yet — Parties success may justify creating one for future domain services |

### Selected Starter: EventStore Solution Structure Pattern

**Rationale for Selection:**
Hexalith.Parties is the first domain service on EventStore. Its solution structure should mirror EventStore's conventions exactly — validating that the pattern is reusable (a platform validation goal). Deviations from the EventStore structure should be intentional and documented, as they inform the pattern for all future domain services.

**Initialization Approach:**
Manual scaffolding following EventStore conventions. No CLI generator — the act of scaffolding validates the pattern's reproducibility.

### Architectural Decisions Provided by Starter

**Language & Runtime:**
- C# on .NET 10.0 (SDK 10.0.300, pinned in `global.json`)
- File-scoped namespaces, nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)
- Allman brace style, 4-space indentation, CRLF line endings, UTF-8

**Solution Format:**
- Modern XML solution format (`Hexalith.Parties.slnx`) — no legacy .sln files
- Solution folder structure: `/src/`, `/tests/`, `/samples/`

**Package Management:**
- Central package management via `Directory.Packages.props`
- Shared build properties via `Directory.Build.props`
- MinVer 7.0.0 for git tag-based SemVer versioning (prefix `v`)

**Verified Current Dependency Versions:**

| Dependency | EventStore Version | Current Latest | Parties Version | Notes |
|------------|-------------------|----------------|-----------------|-------|
| .NET SDK | 10.0.102 | 10.0.300 | 10.0.300 | Update to latest patch |
| DAPR SDK | 1.16.1 | 1.17.0 | 1.16.1 | Match EventStore; upgrade as coordinated effort |
| Aspire | 13.1.2 | 13.1.2 | 13.1.2 | Current |
| MediatR | 14.0.0 | 14.0.0 | 14.0.0 | Current |
| FluentValidation | 12.1.1 | 12.1.1 | 12.1.1 | Current |
| OpenTelemetry | 1.15.0 | 1.15.0 | 1.15.0 | Current |
| xUnit | 2.9.3 | 3.2.2 | 3.2.2 | Migrated to xUnit v3 package IDs |
| Shouldly | 4.3.0 | 4.3.0 | 4.3.0 | Current |
| NSubstitute | 5.3.0 | 5.3.0 | 5.3.0 | Current |
| ModelContextProtocol | N/A | 1.0.0 | 1.0.0 | New — Parties-specific. Stable release 2/25/2026 |

**Proposed Solution Structure (mirroring EventStore pattern):**

```
Hexalith.Parties.slnx
Directory.Build.props
Directory.Packages.props
global.json

src/
  Hexalith.Parties.Contracts          # Commands, events, query types, [PersonalData] attributes
  Hexalith.Parties.Client             # Client abstractions, IPartiesCommandClient, IPartiesQueryClient, AddPartiesClient()
  Hexalith.Parties.Server             # Party aggregate, Handle/Apply, domain processors
  Hexalith.Parties.Projections        # Read projection infrastructure, match metadata, search
  Hexalith.Parties         # REST API endpoints, MCP server, validation
  Hexalith.Parties.Aspire             # Aspire hosting extensions for Parties
  Hexalith.Parties.AppHost            # Aspire AppHost (DAPR topology orchestrator)
  Hexalith.Parties.ServiceDefaults    # Shared service config, OpenTelemetry
  Hexalith.Parties.Testing            # Testing utilities and helpers

tests/
  Hexalith.Parties.Contracts.Tests    # Tier 1 — pure domain types
  Hexalith.Parties.Client.Tests       # Tier 1 — client abstractions
  Hexalith.Parties.Server.Tests       # Tier 1 — aggregate logic (pure Handle/Apply)
  Hexalith.Parties.Projections.Tests  # Tier 1 — projection logic
  Hexalith.Parties.Tests   # Tier 2 — API + MCP integration
  Hexalith.Parties.IntegrationTests   # Tier 3 — full-stack Aspire topology

samples/
  Hexalith.Parties.Sample             # BasicConsumingApp — REST + MCP + event subscription
```

**Key structural differences from EventStore:**
- `Hexalith.Parties.Projections` — new project, no EventStore equivalent. Houses read projection infrastructure.
- MCP server hosted within the Parties service (same process, shared auth pipeline) — architecture decision to be confirmed in step 4.
- `Hexalith.Parties.Server.Tests` tests pure aggregate logic without EventStore — validates domain correctness independently.

**Testing Framework:**
- xUnit v3 3.2.2 with Shouldly assertions, NSubstitute mocking
- Three-tier strategy: Tier 1 (unit, no external deps), Tier 2 (DAPR slim init), Tier 3 (full Aspire topology)
- coverlet.collector 6.0.4 for coverage

**Development Experience:**
- `dotnet aspire run` on AppHost for full local topology
- DAPR sidecars managed by Aspire
- Hot reload for domain service development
- Swagger UI for API exploration

**CI/CD:**
- GitHub Actions on push/PR to main
- Restore → build (Release) → Tier 1+2 tests → optional Tier 3
- Release triggered by `v*` tags — pack and push NuGet packages

**Note:** Project initialization using this structure should be the first implementation story.

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**

1. Projection data store choice and actor granularity
2. MCP composite command strategy (create + update)
3. MCP layer boundary definition (translation layer)
4. Personal data classification scope
5. Composite sub-operation idempotency and conflict detection
6. Index actor state management strategy (partitioned, interface-first)
7. Maximum composite payload size limit

**Important Decisions (Shape Architecture):**

8. Projection rebuild strategy (v1.0: manual; v1.1: automated drift detection)
9. Projection health monitoring with auto-rebuild on corruption
10. Index actor batch event processing
11. IsNaturalPerson reclassification strategy
12. Projection testability approach
13. Composite command test matrix approach
14. MCP layer architectural fitness enforcement

**Deferred Decisions (Post-MVP):**

15. Dedicated search engine selection (v1.1)
16. Automated projection drift detection (v1.1)
17. Crypto-shredding activation and re-encryption mechanics (v1.1)
18. Admin portal frontend architecture (v1.2) — decided by the FrontComposer/EventStore course correction; see "Frontend Architecture" below.

### Frontend Architecture

**D20 — Administration Frontend: FrontComposer Domain Surface**

- **Decision:** The Parties Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor.
- **Decision:** The portal registers Parties-domain views with the FrontComposer shell, reads through EventStore query/client abstractions, routes supported commands through the typed Parties client/EventStore command boundary, and delegates generic event/stream browsing to EventStore Admin UI safe deep-links.
- **Rationale:** This aligns the administration experience with the EventStore-fronted architecture pivot and avoids building a standalone TypeScript SPA or duplicating generic EventStore stream inspection.
- **Consequence:** The portal must fail closed and clear sensitive state on sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures.
- **Consequence:** Labels, dates, counts, status messages, validation messages, and operation outcomes must be localized. Focus management, keyboard access, non-color-only state, and polite status announcements are part of the frontend architecture contract.
- **Affects:** Administration portal, GDPR operations UI, EventStore Admin UI deep-links, FrontComposer integration, party picker UX.

### Party Picker Frontend Surface

The party picker is a v1.2 embeddable FrontComposer/Blazor component for tenant-safe party search and durable selection. Its durable selection contract is party id only. It must not store or emit display names, contact values, identifiers, consent text, search text, tenant ids, tokens, raw ProblemDetails, or raw query payloads as durable host keys, URLs, telemetry dimensions, filenames, DOM event names, JavaScript event payloads, or logs.

The picker queries through the accepted EventStore-fronted Parties client/gateway boundary. It must not call retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals. Host request/auth context is supplied through accepted client/gateway configuration; the picker does not persist, refresh, parse for authorization, or log tokens.

The picker suppresses stale responses when token, tenant, user, host configuration, selected id, or search options change, and it handles loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, and transient-failure states without leaking personal data.

### Data Architecture

**D1 — Projection Data Store: DAPR Actor-Managed, Key/Value JSON**

- **Decision:** Read projections are DAPR actors with JSON state persisted to DAPR state store
- **Rejected:** Dedicated query database (operational complexity), in-memory rebuild (cold start violation at scale)
- **Rationale:** Same infrastructure as write side, actor lifecycle provides in-memory performance when activated, no separate database to manage
- **Consequence:** Search limited to basic key-lookup in v1.0; full-text search deferred to v1.1 with dedicated search engine
- **Affects:** Projections, Parties service, MCP tools, Client query abstractions

**D2 — Search: Separate Concern, Deferred to v1.1**

- **Decision:** Basic key-lookup and list/filter in v1.0. Dedicated search engine (Elasticsearch or similar) in v1.1
- **Rejected:** Building search into v1.0 projection model
- **Rationale:** Keeps projection model clean; search is a distinct infrastructure concern
- **Consequence:** MCP `find_parties` limited to exact/prefix match in v1.0. Index actor schema designed search-ready for v1.1 extensibility
- **Affects:** MCP tools, query API, index actor schema design

**D3 — Snapshot Strategy: Managed by Hexalith.EventStore**

- **Decision:** Snapshot configuration, rebuild, and corruption recovery delegated entirely to EventStore
- **Rejected:** Parties-level snapshot configuration
- **Rationale:** Platform concern, not domain concern. Parties validates EventStore defaults work at realistic aggregate sizes
- **Consequence:** If NFR3 (rehydration < 200ms) is violated at scale, the fix goes into EventStore, not Parties
- **Affects:** None directly — EventStore responsibility

**D4 — Projection Actor Granularity: Hybrid (Per-Party Detail + Per-Tenant Index)**

- **Decision:** Two projection actor types:
  - `PartyDetailProjectionActor` — one per party, holds full party detail projection. Key: `tenant:party-detail:{partyId}`
  - `PartyIndexProjectionActor` — one per tenant, holds lightweight party summaries for list/filter. Key: `tenant:party-index:{tenantId}`
- **Rejected:** Per-party only (no list capability without separate index), per-tenant only (state size at 100K parties)
- **Rationale:** Clean separation — index for list/search, detail for get-by-ID. Each has independent scaling and testing characteristics
- **Consequence:** Two event handler implementations, two test surfaces. Index actor needs partitioned state management (see D5)
- **Affects:** Projections, Parties service query routing, testing strategy

**D5 — Index Actor State Management: Partitioned State (Interface-First)**

- **Decision:** Index actor must support partitioned state via an `IIndexPartitionStrategy` abstraction. V1.0 implementation uses a single-key strategy (simplest). Multi-key partitioning (alphabetical buckets, page-based shards) activated when scale demands it
- **Rejected:** Single monolithic state key without abstraction (breaks CosmosDB 2MB limit at scale, no migration path), premature multi-key partitioning (over-engineering for v1.0 tenant sizes)
- **Rationale:** DAPR state store backends have varying per-entry size limits. The partition interface costs almost nothing to implement but keeps the door open for scale. At v1.0 launch, tenants will have hundreds to low thousands of parties — single-key is sufficient. At 100K+ parties, swap strategy without architectural change
- **Consequence:** Document minimum state store requirements for deployment. Partition strategy is a configuration concern, not a code change
- **Affects:** PartyIndexProjectionActor implementation, deployment documentation, state store selection guidance

### Authentication & Security

**D6 — Personal Data Scope: Precise Type-Dependent, GDPR-Compliant**

- **Decision:** Encryption scope varies by party type when crypto-shredding activates in v1.1:
  - **Person parties:** All PII encrypted (names, DOB, derived fields like display name and sort name)
  - **Organization parties:** Entity-level fields (legal name, trading name, legal form) NOT encrypted by default
  - **All party types:** Contact channels and identifiers always encrypted (may reference natural persons)
  - **Sole trader:** `IsNaturalPerson` boolean flag on organization parties — when true, elevates to person-level encryption scope
- **Rejected:** Conservative approach (encrypt all on all types — simpler but over-encrypts)
- **Rationale:** Best-effort GDPR compliance. Over-encryption of organization entity data is unnecessary; under-encryption is a compliance risk handled by `IsNaturalPerson` flag
- **Consequence:** Type-conditional encryption logic in v1.1 crypto-shredding. `[PersonalData]` attribute placement varies by field and party type
- **Affects:** Contracts (attribute placement), Server (crypto-shredding logic), event schema design

**D7 — IsNaturalPerson Reclassification: Mid-Lifecycle Classification Change**

- **Decision:** When `IsNaturalPerson` changes from false to true on an organization party, v1.1 crypto-shredding must handle re-encryption of previously unencrypted fields. Documented as a complexity hotspot requiring explicit design in v1.1
- **Rationale:** Sole trader status may be discovered after initial party creation. Events already published unencrypted cannot be retroactively encrypted — re-encryption applies to state going forward, subscribers notified via reclassification event
- **Consequence:** v1.1 must define re-encryption strategy, subscriber notification, and operational guidance for this scenario
- **Affects:** Server (reclassification command/event), crypto-shredding infrastructure, subscriber documentation

### API & Communication Patterns

**D8 — MCP Create Strategy: Composite Aggregate Command**

- **Decision:** `CreatePartyComposite` command processed in a single actor turn, emitting multiple events atomically (PartyCreated + N × ContactChannelAdded + M × IdentifierAdded)
- **Rejected:** Sequential commands (latency violation), batch API (unverified EventStore support), relaxed latency target
- **Rationale:** Eliminates partial failure by design. Single actor turn = atomic. Latency reduced from N×command_time to 1×command_time. Also justified by atomicity alone — even if per-command latency is low
- **Prerequisite validated:** EventStore `Handle` supports multi-event return from single command ✅
- **Consequence:** EventStore convention discovery must handle composite command types. Validates a reusable pattern for future domain services
- **Affects:** Contracts (CreatePartyComposite type), Server (Handle method), EventStore pattern validation

**D9 — MCP Update Strategy: Composite Command with Aggregate-Side Diff**

- **Decision:** `UpdatePartyComposite` command with explicit add/update/remove lists:
  ```
  UpdatePartyComposite {
    PersonDetails?            // present = replace, absent = no change
    OrganizationDetails?      // present = replace, absent = no change
    AddContactChannels[]      // items to add
    UpdateContactChannels[]   // items to update (by ID)
    RemoveContactChannelIds[] // IDs to remove
    AddIdentifiers[]
    UpdateIdentifiers[]
    RemoveIdentifierIds[]
  }
  ```
- **Rejected:** MCP-side diff (puts domain logic outside aggregate), generic patch with null/absent ambiguity
- **Rationale:** Explicit lists eliminate all interpretation ambiguity. Aggregate `Handle` processes each list independently, emitting corresponding events. Domain logic stays in the aggregate (DDD principle). MCP translation layer converts AI patch intent into explicit lists
- **Consequence:** Most complex `Handle` method in the system — requires exceptional test coverage (see D14)
- **Affects:** Contracts (UpdatePartyComposite type), Server (Handle method), MCP translation layer

**D10 — Composite Sub-Operation Idempotency and Conflict Detection**

- **Decision:** Composite command `Handle` validates each sub-operation against current state:
  - Skip duplicate additions (channel/identifier already exists) — essential for MCP retry safety
  - Reject invalid IDs in update/remove lists (return error, not silent skip)
  - Reject conflicting operations on the same entity ID within one composite (e.g., AddContactChannel and RemoveContactChannelId for the same ID) — explicit error: "conflicting operations on same channel ID"
  - Return a result indicating what was actually applied vs skipped vs rejected
- **Rejected:** Trust caller to send correct payloads (fragile), fail entire composite on any sub-op issue (too strict), reject duplicates (forces MCP layer to diff before every call, violating translation layer boundary)
- **Rationale:** Consumers (MCP, REST API, Client) may construct payloads from stale state. AI agents may retry with the same payload. Aggregate is the authority on current state and must validate. Skip-duplicates is the simpler *system* design even though the handler is slightly more complex
- **Consequence:** `Handle` return type must convey per-sub-operation outcomes. Test matrix must cover duplicate, invalid, conflicting, and mixed scenarios
- **Affects:** Server (Handle method), Contracts (result types), MCP response assembly

**D11 — MCP Layer Boundary: Translation Layer**

- **Decision:** MCP layer is a **translation layer**, not "thin orchestration":
  - **Allowed:** Input normalization (forgiving-to-strict), command construction, aggregate invocation, response assembly (complete entity + match metadata)
  - **Forbidden:** Business rules, domain validation, state caching, direct state store access, retry logic with domain awareness
  - **Input normalization** is an implementation detail of the translation layer — starts as private methods in MCP tool classes, extracted to shared utilities only if actual reuse emerges (e.g., REST API needs the same normalization)
- **Rejected:** "Thin pass-through" (insufficient for forgiving input + response assembly), "smart orchestration" (domain logic leakage risk), premature normalization abstraction (YAGNI)
- **Rationale:** MCP tools have forgiving input schemas (FR74) and return complete entities with match metadata. This is non-trivial translation but contains zero domain logic. Explicit boundary prevents scope creep
- **Architectural fitness enforcement:** MCP layer code must have zero references to domain event types — only command types and query result types. Enforced via lint rule or compilation test (MCP project references Contracts but not Server). Violations indicate domain logic leakage
- **Consequence:** MCP layer will be a significant component (~30-40% of the Parties service) but architecturally bounded. Boundary is machine-verifiable, not just documented
- **Affects:** Parties service (MCP server implementation), testing strategy, developer documentation, CI pipeline (fitness test)

**D12 — Partial Failure: Eliminated by Design**

- **Decision:** All MCP composite operations are all-or-nothing. Single actor turn = atomic success or atomic failure
- **Rejected:** Best-effort with partial success reporting
- **Rationale:** Composite aggregate commands process in one actor turn. No partial state is possible. Simplifies error handling for all consumers
- **Consequence:** No partial party creation — if one sub-operation fails validation, entire composite is rejected with specific error details
- **Affects:** All consumers (MCP, REST API, Client), error response design

### Infrastructure & Deployment

**D13 — Event Ordering: Managed by Hexalith.EventStore**

- **Decision:** Event ordering guarantees delegated entirely to EventStore. Parties consumes events through whatever delivery contract the platform provides
- **Rejected:** Parties-level sequence checking, defensive handlers
- **Rationale:** Platform concern. EventStore defines and enforces ordering guarantees per deployment target
- **Affects:** None directly — EventStore responsibility

**D14 — Projection Rebuild Strategy: Event Replay Through Pure Handlers**

- **Decision:** Projection state can be rebuilt by replaying events from EventStore through the pure projection handler classes (same handlers used in normal operation)
  - **v1.0:** Manual rebuild triggered via admin endpoint. Per-tenant, parallelizable, and resumable (can restart from last successfully processed event sequence number)
  - **v1.1:** Automated drift detection — health check compares index count against event store aggregate count, triggers rebuild on divergence
- **Rationale:** Pure handler extraction (D15) makes this straightforward — feed historical events through handlers, write resulting state to actor state store. No separate rebuild infrastructure needed. At 1M parties × 20 events average = 20M events, rebuild may take significant time — must be per-tenant and resumable
- **Consequence:** Operational runbook must document manual rebuild procedure for v1.0. Admin endpoint exposed for rebuild trigger
- **Affects:** Projections (rebuild tooling), operational documentation, admin API

**D15 — Projection Health Monitoring with Auto-Rebuild on Corruption**

- **Decision:** Projection actors must handle state corruption gracefully:
  - Catch deserialization failure on actor activation
  - Log corruption alert
  - Trigger automatic rebuild from event stream (D14)
  - Return "service degraded" to callers during rebuild
- **Rationale:** DAPR state store entries can be corrupted (partial writes, operator error, store migration). Without graceful handling, corrupted state = permanent query failure for the affected tenant until manual intervention
- **Consequence:** Actor activation includes corruption detection. Callers (API, MCP) must handle "degraded" responses
- **Affects:** Projections (actor activation logic), Parties service (degraded response handling), operational alerting

**D16 — Index Actor Batch Event Processing**

- **Decision:** Index actor should batch event processing — accumulate N events or T milliseconds of events before persisting state, rather than persisting after every single event
- **Rationale:** Under burst load (e.g., 1000 concurrent party creations), persisting the full index state after every single event creates a serialization bottleneck. Batching amortizes the persistence cost. DAPR actor turn-based concurrency naturally queues events, enabling batch processing within a single turn
- **Consequence:** Projection eventual consistency window slightly increased during bursts. Batch size/time configurable
- **Affects:** PartyIndexProjectionActor implementation, consistency SLA documentation

### API & Communication Patterns (continued)

**D17 — Maximum Composite Payload Size**

- **Decision:** Composite commands enforce a maximum sub-operation count (e.g., 100 sub-operations per composite). Payloads exceeding the limit are rejected with a specific error before processing
- **Rationale:** Unbounded composite payloads risk actor turn timeout under DAPR. A party with 50 contact channels and 50 identifiers in a single create = 101 sub-operations — realistic upper bound. The limit protects against malformed or adversarial payloads
- **Consequence:** FluentValidation rule on composite command types. Limit is configurable per deployment
- **Affects:** Contracts (validation rules), Server (Handle guard clause), deployment configuration

### Testing & Quality

**D18 — Projection Testability: Pure Handler Classes Extracted from Actors**

- **Decision:** Projection logic implemented in pure handler classes (`PartyIndexProjectionHandler`, `PartyDetailProjectionHandler`). Actors are thin wrappers that delegate to handlers and manage DAPR state
- **Rationale:** Same pattern as EventStore aggregate Handle/Apply — pure functions wrapped by infrastructure. Tier 1 testable without DAPR dependency. Actor behavior tested at Tier 2
- **Consequence:** Must maintain handler/actor separation discipline. Handlers receive events and return state mutations — no DAPR awareness
- **Affects:** Projections (code structure), test infrastructure

**D19 — Composite Command Test Matrix: Designed Upfront in Story Definitions**

- **Decision:** Test case catalogs for composite commands (especially `UpdatePartyComposite`) must be defined in story specifications before implementation begins
- **Rationale:** `UpdatePartyComposite` has combinatorial complexity (person details only, channels only, identifiers only, all three, partial updates, additions + removals in same call, duplicate detection, invalid ID rejection). Estimated 15-25 meaningful test cases. Discovering the matrix during implementation risks incomplete coverage
- **Consequence:** Story definitions for composite command work include explicit test case catalogs
- **Affects:** Story preparation (Scrum Master), Server tests, QA review

### Decision Impact Analysis

**Implementation Sequence:**

1. D8 + D9 + D17 — Composite command contracts with payload limits and aggregate handlers (foundational)
2. D10 — Sub-operation idempotency and conflict detection (part of handler implementation)
3. D4 + D5 — Projection actor structure with partitioned index (interface-first, single-key v1.0)
4. D18 — Pure handler extraction (enables all projection testing)
5. D16 — Index actor batch event processing (performance optimization)
6. D15 — Projection health monitoring with corruption handling
7. D11 — MCP translation layer with architectural fitness enforcement
8. D6 — PersonalData attribute placement (v1.0 markers, v1.1 activation)
9. D14 — Projection rebuild tooling via admin endpoint (operational readiness)
10. D7 — IsNaturalPerson reclassification (v1.1)

**Cross-Component Dependencies:**

- D8/D9 → D10: Composite commands require idempotency and conflict detection design
- D8/D9 → D17: Composite commands require payload size limits
- D4 → D5: Index actor granularity determines state management strategy
- D4 → D18: Actor structure determines handler extraction pattern
- D4 → D16: Index actor requires batch processing strategy
- D18 → D14: Pure handlers enable projection rebuild
- D15 → D14: Health monitoring triggers rebuild on corruption
- D6 → D7: Personal data scope determines reclassification complexity
- D11 → fitness test: MCP boundary enforced via compilation test in CI
- D19 → D8/D9: Test matrix must exist before composite command implementation

**Quantitative Validation (from Comparative Analysis):**

- Composite aggregate pattern scored 2.65/3.00 vs sequential (1.90) and saga (1.45)
- Hybrid projection pattern scored 2.85/3.00 vs per-party only (2.30) and per-tenant only (1.85)
- Both highest-scored options were selected — decisions are quantitatively defensible

## Implementation Patterns & Consistency Rules

_These patterns ensure multiple AI agents write compatible, consistent code. Every rule addresses a specific conflict point where agents could make different choices._

### Type Declaration Patterns

**Commands:**
- `sealed record` with `{ get; init; }` properties (not positional parameters — additive-safe)
- No suffix (not `CreatePartyCommand`)
- Naming: imperative verb + entity (`CreateParty`, `AddContactChannel`)
- Composite naming: verb + entity + `Composite` (`CreatePartyComposite`, `UpdatePartyComposite`)
- Commands carry `PartyId` (aggregate ID). `TenantId` extracted from request context, not on command records
- Entity IDs are client-generated UUIDs — commands carry the ID, events echo it

```csharp
// CORRECT
public sealed record AddContactChannel
{
    public required string PartyId { get; init; }
    public required string ContactChannelId { get; init; }  // Client-generated UUID
    public required ContactChannelType Type { get; init; }
    public required string Value { get; init; }
}

// WRONG — positional parameters (breaks binary compat on reorder)
public sealed record AddContactChannel(string PartyId, string ContactChannelId, ContactChannelType Type, string Value);
```

**Events:**
- `sealed record` with `{ get; init; }` properties
- No suffix (not `PartyCreatedEvent`)
- Success events implement `IEventPayload`
- Rejection events implement `IRejectionEvent`
- Naming: entity + past participle (`PartyCreated`, `ContactChannelAdded`)
- Rejection naming: entity + `Cannot` + reason (`PartyCannotBeCreatedWithoutType`)

```csharp
// Success event
public sealed record ContactChannelAdded : IEventPayload
{
    public required string ContactChannelId { get; init; }
    public required ContactChannelType Type { get; init; }
    public required string Value { get; init; }
}

// Rejection event
public sealed record PartyCannotBeCreatedWithoutType : IRejectionEvent;
```

**Event Versioning:**
- Additive optional `{ get; init; }` properties on existing event records
- Never create V2 events (`PartyCreatedV2` is forbidden)
- Tolerant reader pattern — consumers ignore unknown fields
- Same rule applies to projection models in Contracts/Models

**Aggregate State:**
- `sealed class` (not record — state is mutable by Apply methods)
- `{ get; private set; }` for scalar properties
- `private readonly List<T> _field = []; public IReadOnlyList<T> Field => _field;` for collections
- `Apply(TEvent)` methods mutate state — this is the explicit design from EventStore
- Naming: `{Domain}State` (`PartyState`)

```csharp
public sealed class PartyState
{
    public PartyType Type { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public PersonDetails? Person { get; private set; }
    public OrganizationDetails? Organization { get; private set; }
    public bool IsNaturalPerson { get; private set; }

    private readonly List<ContactChannel> _contactChannels = [];
    public IReadOnlyList<ContactChannel> ContactChannels => _contactChannels;

    private readonly List<PartyIdentifier> _identifiers = [];
    public IReadOnlyList<PartyIdentifier> Identifiers => _identifiers;

    public void Apply(ContactChannelAdded e)
    {
        _contactChannels.Add(new ContactChannel
        {
            Id = e.ContactChannelId,
            Type = e.Type,
            Value = e.Value
        });
    }
}
```

**Value Objects:**
- `sealed record` (immutable, structural equality)
- No suffix (not `PostalAddressVO`)
- Descriptive noun naming (`PostalAddress`, `EmailAddress`, `PhoneNumber`)

**Aggregate:**
- Naming: `{Domain}Aggregate` (`PartyAggregate`)
- Inherits `EventStoreAggregate<TState>` (e.g., `EventStoreAggregate<PartyState>`)
- All `Handle` methods are **synchronous** — return `DomainResult`, never `Task<DomainResult>`
- Domain logic is pure — no I/O in Handle
- Simple commands return `DomainResult` (via `DomainResult.Success()`, `.Rejection()`, `.NoOp()`)
- Composite commands return `CompositeCommandResult` (extends `DomainResult` with Applied/Skipped/Rejected collections)

### Namespace & Project Organization

**Namespace Convention:** `Hexalith.Parties.{Project}.{SubFeature}` — folders match namespaces (EventStore convention)

**Contracts Project (shared types — zero runtime dependencies beyond netstandard2.1):**
```
Hexalith.Parties.Contracts/
├── Commands/                              # .Commands namespace
│   ├── CreateParty.cs
│   ├── CreatePartyComposite.cs
│   ├── UpdatePartyComposite.cs
│   ├── AddContactChannel.cs
│   ├── UpdateContactChannel.cs
│   ├── RemoveContactChannel.cs
│   ├── AddIdentifier.cs
│   ├── RemoveIdentifier.cs
│   ├── DeactivateParty.cs
│   └── ...
├── Events/                                # .Events namespace
│   ├── PartyCreated.cs
│   ├── ContactChannelAdded.cs
│   ├── ContactChannelUpdated.cs
│   ├── ContactChannelRemoved.cs
│   ├── PreferredContactChannelChanged.cs  # FR11
│   ├── IdentifierAdded.cs
│   ├── IdentifierRemoved.cs
│   ├── PartyDeactivated.cs
│   ├── PartyMerged.cs                     # v2 forward-compat placeholder (FR37)
│   ├── PartyCannotBeCreatedWithoutType.cs  # IRejectionEvent
│   └── ...
├── State/                                 # .State namespace
│   └── PartyState.cs
├── ValueObjects/                          # .ValueObjects namespace
│   ├── PostalAddress.cs
│   ├── EmailAddress.cs
│   ├── PhoneNumber.cs
│   ├── SocialMediaHandle.cs
│   ├── PersonDetails.cs
│   ├── OrganizationDetails.cs
│   ├── ContactChannel.cs
│   └── PartyIdentifier.cs
├── Models/                                # .Models namespace (query result types)
│   ├── PartyDetail.cs
│   └── PartyIndexEntry.cs                    # Includes CreatedAt, LastModifiedAt for FR68 date filtering
└── Results/                               # .Results namespace
    └── CompositeCommandResult.cs
```

**Server Project (aggregate logic — references Contracts + EventStore):**
```
Hexalith.Parties.Server/
├── Aggregates/
│   └── PartyAggregate.cs                  # All Handle methods
└── Processors/
    └── PartyProcessor.cs                  # IDomainProcessor if needed
```

**Projections Project (read side — references Contracts + DAPR):**
```
Hexalith.Parties.Projections/
├── Handlers/                              # Pure logic, no DAPR awareness
│   ├── PartyDetailProjectionHandler.cs
│   └── PartyIndexProjectionHandler.cs
└── Actors/                                # DAPR wrappers, thin delegation
    ├── PartyDetailProjectionActor.cs
    └── PartyIndexProjectionActor.cs
```

**Parties Service Project (REST + MCP — references Contracts + Server + Projections):**
```
Hexalith.Parties/
├── Controllers/
│   └── PartiesController.cs               # REST API, route: api/v1/parties
├── Mcp/
│   ├── CreatePartyMcpTool.cs              # MCP protocol name: create_party
│   ├── UpdatePartyMcpTool.cs              # MCP protocol name: update_party
│   ├── FindPartiesMcpTool.cs              # MCP protocol name: find_parties
│   ├── GetPartyMcpTool.cs                 # MCP protocol name: get_party
│   └── DeletePartyMcpTool.cs              # MCP protocol name: delete_party
├── Validation/
│   ├── CreatePartyCompositeValidator.cs
│   └── UpdatePartyCompositeValidator.cs
├── ErrorHandling/
├── Extensions/
│   └── PartiesServiceCollectionExtensions.cs
├── Models/                                # Request/response DTOs if different from Contracts
└── Configuration/
```

**Client Project (consumer package — references Contracts only):**
```
Hexalith.Parties.Client/
├── Abstractions/
│   ├── IPartiesCommandClient.cs
│   └── IPartiesQueryClient.cs
└── Extensions/
    └── PartiesClientServiceCollectionExtensions.cs
```

### API & Data Format Patterns

**REST API:**
- Controller naming: `{Resource}Controller` (`PartiesController`)
- Route pattern: `api/v1/parties` (URL-path versioning, plural resource)
- Async commands: `202 Accepted` with `CorrelationId`
- Query results: `200 OK` with direct response body (no wrapper)
- Errors: ProblemDetails (RFC 9457) with `correlationId` and `tenantId` extensions
- Content type: `application/json`, errors as `application/problem+json`

**JSON Conventions:**
- Property naming: `camelCase` (System.Text.Json default)
- Date format: ISO 8601 strings
- Null handling: Omit null properties from JSON output
- Enums: String serialization (not integer)
- Boolean: `true`/`false` (never `1`/`0`)

**MCP Tool Conventions:**
- Class naming: `{ToolName}McpTool` (`CreatePartyMcpTool`)
- Protocol name: `snake_case` (`create_party`, `update_party`, `find_parties`, `get_party`, `delete_party`)
- Registration: `AddMcpTools()` extension method with assembly scanning
- Response types: party-returning tools → `PartyDetail`. Search tools → `PartyIndexEntry[]`
- All tools that return a party return the same `PartyDetail` shape

**Event/Pub-Sub Conventions:**
- CloudEvents 1.0 envelope (inherited from EventStore)
- Pub/sub topic: `{tenant}.{domain}.events` (e.g., `acme.parties.events`)
- Dead letter topic: `deadletter.{tenant}.{domain}.events`
- Event payload JSON: same `camelCase` convention as API

**DAPR State Key Conventions:**
- Aggregate actor: `{tenant}:{domain}:{aggregateId}` (e.g., `acme:parties:550e8400-...`)
- Detail projection: `{tenant}:party-detail:{partyId}` (e.g., `acme:party-detail:550e8400-...`)
- Index projection: `{tenant}:party-index:{partitionKey}` (e.g., `acme:party-index:default`)

### Process Patterns

**Error Handling:**
- Domain validation failures → `DomainResult.Rejection(rejectionEvents)` — never exceptions for domain logic
- Infrastructure failures → exceptions caught by `IExceptionHandler` → ProblemDetails
- Validation failures → FluentValidation → `ValidationException` → HTTP 400 ProblemDetails
- Auth failures → `CommandAuthorizationException` → HTTP 403 ProblemDetails
- Tenant mismatch → `TenantMismatchException` → HTTP 403 ProblemDetails

**Validation:**
- FluentValidation assembly scanning (auto-discovery, no explicit registration)
- One validator per command type: `{CommandType}Validator` (e.g., `CreatePartyCompositeValidator`)
- Validator inherits `AbstractValidator<T>`
- Use `[GeneratedRegex]` for compiled regex patterns
- Two validation layers, never overlapping:
  1. FluentValidation on API/MCP entry (structural: required fields, format, max payload size)
  2. Domain validation in aggregate Handle (business rules → rejection events)

**Logging:**
- Structured logging via `ILogger<T>`
- No PII in log messages — enforced via awareness of `[PersonalData]` attributes
- Log levels: `Information` for commands/events, `Warning` for rejections, `Error` for infrastructure failures
- Correlation ID propagated through all log entries

**DI Registration:**
- `AddParties()` on `IServiceCollection` — registers server-side services
- `AddPartiesClient()` — registers client-side abstractions only
- `UseParties()` on `IHost` — runtime initialization
- `AddHexalithParties()` on `IDistributedApplicationBuilder` — Aspire hosting extensions
- All registration methods return the builder/collection for fluent chaining

**Configuration:**
- Options pattern: `{Feature}Options` as `record` with `{ get; init; }` properties and defaults
- Configuration prefix: `Parties:{Section}` (e.g., `Parties:Publisher`, `Parties:Projections`)
- Bound via `IOptions<TOptions>` DI pattern
- Environment-specific overrides via standard .NET configuration providers

### Testing Patterns

**Test Organization:**
- Test class naming: `{ClassUnderTest}Tests` (e.g., `PartyAggregateTests`)
- Test method naming: `{Method}_{Scenario}_{ExpectedResult}` (e.g., `Handle_CreatePartyComposite_EmitsPartyCreatedAndChannelAddedEvents`)
- One test class per production class
- File name = class name

**Test Data Builders:**
- `{Domain}TestData` static class in `Hexalith.Parties.Testing` project
- Builders return valid objects by default — tests override only what they're testing
- Factory methods: `CreatePersonWithChannels(int channelCount = 3)`, `ValidCreatePersonComposite()`, etc.

**Assertion Conventions:**
- Shouldly for all assertions (`result.IsSuccess.ShouldBeTrue()`)
- Composite results: assert on collection properties (`result.Applied.ShouldContain(...)`, `result.Skipped.ShouldBeEmpty()`)
- No custom assertion extensions unless reuse exceeds 5 occurrences

**Test Tier Compliance:**
- Tier 1 tests: zero infrastructure dependencies (no DAPR, no HTTP, no database)
- Tier 2 tests: DAPR slim init only
- Tier 3 tests: full Aspire topology via Docker
- Projection handler tests are Tier 1 (pure handlers, no DAPR)
- Projection actor tests are Tier 2 (DAPR actor lifecycle)

### Enforcement Guidelines

**All AI Agents MUST:**
1. Use `sealed record` with `{ get; init; }` for commands, events, and value objects
2. Use `sealed class` with `{ get; private set; }` for aggregate state — mutable via Apply
3. Implement `IEventPayload` or `IRejectionEvent` on all events
4. Return `DomainResult` from simple Handle, `CompositeCommandResult` from composite Handle — never throw for domain rejections
5. Keep all Handle methods synchronous — no `Task<DomainResult>`
6. Use client-generated UUIDs for all entity IDs — commands carry ID, events echo it
7. Use ProblemDetails (RFC 9457) for all API error responses
8. Use `[GeneratedRegex]` for compiled regex patterns
9. One public type per file, file name = type name
10. Test method naming: `{Method}_{Scenario}_{ExpectedResult}`
11. No PII in log messages
12. `camelCase` JSON, ISO 8601 dates, string enums, omit nulls
13. Additive-only event evolution — never V2 events
14. Configuration prefix: `Parties:{Section}`

**Architectural Fitness Tests (enforced in CI):**
- MCP layer: zero references to domain event types — only command types and query result types
- Projection handlers: zero DAPR references
- Contracts project: zero runtime dependencies beyond netstandard2.1
- Client project: no references to Server, Projections, or Parties service
- Test tier compliance: Tier 1 tests have zero infrastructure dependencies

**Anti-Patterns (explicitly forbidden):**
- Positional record parameters on commands/events
- `Task<DomainResult>` return from aggregate Handle
- `V2` event types instead of additive properties
- PII in structured log messages
- Domain logic in MCP translation layer
- DAPR references in projection handler classes
- Direct state store access from MCP layer
- Explicit FluentValidation registration (use assembly scanning)

## Project Structure & Boundaries

### Complete Project Directory Structure

```
Hexalith.Parties/
│
├── .editorconfig                          # Copied from EventStore (Allman, 4-space, CRLF, UTF-8)
├── .gitignore                             # Copied from EventStore
├── CLAUDE.md                              # AI agent context (Parties-specific)
├── LICENSE                                # MIT
├── README.md                              # Project overview, getting started
├── Directory.Build.props                  # Shared build props (net10.0, nullable, TreatWarningsAsErrors, NuGet metadata, MinVer)
├── Directory.Packages.props               # Central package management (all dependency versions)
├── global.json                            # SDK 10.0.300, rollForward: latestPatch
├── Hexalith.Parties.slnx                  # Modern XML solution format
│
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                         # Push/PR: restore → build (Release) → Tier 1+2 → fitness tests
│   │   └── release.yml                    # Tag v*: tests → pack → push NuGet (6 packages)
│   ├── ISSUE_TEMPLATE/
│   │   ├── 01-bug-report.yml
│   │   ├── 02-feature-request.yml
│   │   └── config.yml
│   └── DISCUSSION_TEMPLATE/
│       └── q-a.yml
│
├── deploy/
│   └── dapr/                              # Production DAPR component configs per backend
│       ├── pubsub-kafka.yaml
│       ├── pubsub-rabbitmq.yaml
│       ├── pubsub-servicebus.yaml
│       ├── statestore-cosmosdb.yaml
│       ├── statestore-postgresql.yaml
│       ├── resiliency.yaml
│       ├── accesscontrol.yaml
│       └── subscription-parties.yaml
│
├── docs/                                  # Project documentation
│   └── (v1.0: getting-started, architecture-overview)
│
├── src/
│   ├── Hexalith.Parties.Contracts/        # Shared domain types — zero runtime deps beyond netstandard2.1
│   │   ├── Hexalith.Parties.Contracts.csproj
│   │   ├── Commands/
│   │   │   ├── CreateParty.cs
│   │   │   ├── CreatePartyComposite.cs
│   │   │   ├── UpdatePartyComposite.cs
│   │   │   ├── AddContactChannel.cs
│   │   │   ├── UpdateContactChannel.cs
│   │   │   ├── RemoveContactChannel.cs
│   │   │   ├── AddIdentifier.cs
│   │   │   ├── RemoveIdentifier.cs
│   │   │   ├── UpdatePersonDetails.cs
│   │   │   ├── UpdateOrganizationDetails.cs
│   │   │   ├── SetIsNaturalPerson.cs
│   │   │   ├── DeactivateParty.cs
│   │   │   └── ReactivateParty.cs
│   │   ├── Events/
│   │   │   ├── PartyCreated.cs                    # : IEventPayload
│   │   │   ├── PersonDetailsUpdated.cs
│   │   │   ├── OrganizationDetailsUpdated.cs
│   │   │   ├── ContactChannelAdded.cs
│   │   │   ├── ContactChannelUpdated.cs
│   │   │   ├── ContactChannelRemoved.cs
│   │   │   ├── IdentifierAdded.cs
│   │   │   ├── IdentifierRemoved.cs
│   │   │   ├── IsNaturalPersonChanged.cs
│   │   │   ├── PartyDeactivated.cs
│   │   │   ├── PartyReactivated.cs
│   │   │   ├── PartyDisplayNameDerived.cs
│   │   │   ├── PreferredContactChannelChanged.cs    # FR11
│   │   │   ├── PartyMerged.cs                       # v2 forward-compat placeholder (FR37)
│   │   │   ├── PartyCannotBeCreatedWithoutType.cs   # : IRejectionEvent
│   │   │   └── PartyCannotAddDuplicateChannel.cs    # : IRejectionEvent
│   │   ├── State/
│   │   │   └── PartyState.cs                       # sealed class, Apply methods
│   │   ├── ValueObjects/
│   │   │   ├── PersonDetails.cs                    # sealed record
│   │   │   ├── OrganizationDetails.cs
│   │   │   ├── ContactChannel.cs
│   │   │   ├── PartyIdentifier.cs
│   │   │   ├── PostalAddress.cs
│   │   │   ├── EmailAddress.cs
│   │   │   ├── PhoneNumber.cs
│   │   │   ├── SocialMediaHandle.cs
│   │   │   ├── ContactChannelType.cs               # enum
│   │   │   ├── IdentifierType.cs                   # enum
│   │   │   └── PartyType.cs                        # enum (Person, Organization)
│   │   ├── Models/
│   │   │   ├── PartyDetail.cs                      # Query result — full party view
│   │   │   └── PartyIndexEntry.cs                  # Search result — lightweight summary (includes CreatedAt, LastModifiedAt for FR68 date filtering)
│   │   └── Results/
│   │       └── CompositeCommandResult.cs           # Extends DomainResult: Applied/Skipped/Rejected
│   │
│   ├── Hexalith.Parties.Client/           # Consumer package — references Contracts only
│   │   ├── Hexalith.Parties.Client.csproj
│   │   ├── Abstractions/
│   │   │   ├── IPartiesCommandClient.cs
│   │   │   └── IPartiesQueryClient.cs
│   │   └── Extensions/
│   │       └── PartiesClientServiceCollectionExtensions.cs  # AddPartiesClient()
│   │
│   ├── Hexalith.Parties.Server/           # Aggregate logic — references Contracts + EventStore
│   │   ├── Hexalith.Parties.Server.csproj
│   │   ├── Aggregates/
│   │   │   └── PartyAggregate.cs                   # All Handle methods (sync only)
│   │   └── Processors/
│   │       └── PartyProcessor.cs                   # IDomainProcessor
│   │
│   ├── Hexalith.Parties.Projections/      # Read side — references Contracts + DAPR
│   │   ├── Hexalith.Parties.Projections.csproj
│   │   ├── Handlers/                               # Pure logic — no DAPR awareness
│   │   │   ├── PartyDetailProjectionHandler.cs
│   │   │   └── PartyIndexProjectionHandler.cs
│   │   ├── Actors/                                 # DAPR wrappers — thin delegation
│   │   │   ├── PartyDetailProjectionActor.cs
│   │   │   └── PartyIndexProjectionActor.cs
│   │   └── Configuration/
│   │       └── ProjectionOptions.cs                # Batch size, partition strategy
│   │
│   ├── Hexalith.Parties/       # REST + MCP — references Contracts + Server + Projections
│   │   ├── Hexalith.Parties.csproj
│   │   ├── Controllers/
│   │   │   └── PartiesController.cs                # Route: api/v1/parties
│   │   ├── Mcp/
│   │   │   ├── CreatePartyMcpTool.cs               # create_party
│   │   │   ├── UpdatePartyMcpTool.cs               # update_party
│   │   │   ├── FindPartiesMcpTool.cs               # find_parties
│   │   │   ├── GetPartyMcpTool.cs                  # get_party
│   │   │   └── DeletePartyMcpTool.cs               # delete_party
│   │   ├── Validation/
│   │   │   ├── CreatePartyCompositeValidator.cs
│   │   │   ├── UpdatePartyCompositeValidator.cs
│   │   │   └── AddContactChannelValidator.cs
│   │   ├── ErrorHandling/
│   │   │   └── PartiesExceptionHandler.cs
│   │   ├── Extensions/
│   │   │   └── PartiesServiceCollectionExtensions.cs  # AddParties(), AddMcpTools()
│   │   ├── Configuration/
│   │   │   └── PartiesApiOptions.cs
│   │   └── Models/
│   │       └── (request/response DTOs if needed)
│   │
│   ├── Hexalith.Parties.Aspire/           # Aspire hosting extensions
│   │   ├── Hexalith.Parties.Aspire.csproj
│   │   └── HexalithPartiesExtensions.cs            # AddHexalithParties()
│   │
│   ├── Hexalith.Parties.AppHost/          # Aspire AppHost — DAPR topology
│   │   ├── Hexalith.Parties.AppHost.csproj
│   │   ├── Program.cs
│   │   ├── DaprComponents/
│   │   │   ├── statestore.yaml
│   │   │   ├── pubsub.yaml
│   │   │   ├── accesscontrol.yaml
│   │   │   ├── resiliency.yaml
│   │   │   └── subscription-parties.yaml
│   │   └── Properties/
│   │       └── launchSettings.json
│   │
│   ├── Hexalith.Parties.ServiceDefaults/  # Shared service config
│   │   ├── Hexalith.Parties.ServiceDefaults.csproj
│   │   └── Extensions.cs                          # OpenTelemetry, health checks, resilience
│   │
│   └── Hexalith.Parties.Testing/          # Testing utilities
│       ├── Hexalith.Parties.Testing.csproj
│       └── PartyTestData.cs                        # Test data builders
│
├── tests/
│   ├── Hexalith.Parties.Contracts.Tests/           # Tier 1 — pure domain types
│   │   ├── Hexalith.Parties.Contracts.Tests.csproj
│   │   ├── Commands/
│   │   ├── Events/
│   │   ├── State/
│   │   └── ValueObjects/
│   │
│   ├── Hexalith.Parties.Client.Tests/              # Tier 1 — client abstractions
│   │   └── Hexalith.Parties.Client.Tests.csproj
│   │
│   ├── Hexalith.Parties.Server.Tests/              # Tier 1 — aggregate Handle/Apply (pure functions)
│   │   ├── Hexalith.Parties.Server.Tests.csproj
│   │   └── Aggregates/
│   │       ├── PartyAggregateCreateTests.cs
│   │       ├── PartyAggregateUpdateTests.cs
│   │       ├── PartyAggregateCompositeTests.cs
│   │       └── PartyAggregateIdempotencyTests.cs
│   │
│   ├── Hexalith.Parties.Projections.Tests/         # Tier 1 — projection handler logic (pure)
│   │   ├── Hexalith.Parties.Projections.Tests.csproj
│   │   └── Handlers/
│   │       ├── PartyDetailProjectionHandlerTests.cs
│   │       └── PartyIndexProjectionHandlerTests.cs
│   │
│   ├── Hexalith.Parties.Tests/           # Tier 2 — API + MCP integration
│   │   ├── Hexalith.Parties.Tests.csproj
│   │   ├── Controllers/
│   │   ├── Mcp/
│   │   ├── Validation/
│   │   └── FitnessTests/
│   │       └── ArchitecturalFitnessTests.cs         # MCP boundary enforcement
│   │
│   └── Hexalith.Parties.IntegrationTests/           # Tier 3 — full Aspire topology
│       ├── Hexalith.Parties.IntegrationTests.csproj
│       └── (end-to-end scenarios)
│
└── samples/
    └── Hexalith.Parties.Sample/                     # BasicConsumingApp
        ├── Hexalith.Parties.Sample.csproj
        └── Program.cs                               # REST + MCP + event subscription demo
```

### Architectural Boundaries

**Dependency Direction (strict — no violations):**

```
Contracts ← Client         (consumer package)
Contracts ← Server         (aggregate logic)
Contracts ← Projections    (read side)
Contracts + Server + Projections ← Parties service  (API surface)
Parties service ← AppHost       (hosting)
All src/ ← Testing         (test utilities)
```

**Forbidden Dependencies:**
- Client must NOT reference Server, Projections, or Parties service
- Projections handlers must NOT reference DAPR (only actors reference DAPR)
- MCP layer (Parties/Mcp/) must NOT reference domain event types — only commands and models
- Contracts must NOT reference any runtime dependency beyond netstandard2.1

**NuGet Packages Published (6):**

| Package | Contents | Dependencies |
|---------|----------|-------------|
| `Hexalith.Parties.Contracts` | Commands, events, state, value objects, models, results | None (netstandard2.1) |
| `Hexalith.Parties.Client` | Client abstractions, DI extensions | Contracts |
| `Hexalith.Parties.Server` | Aggregate, processors | Contracts, EventStore.Server |
| `Hexalith.Parties.Projections` | Handlers, actors | Contracts, DAPR |
| `Hexalith.Parties.Testing` | Test data builders | Contracts |
| `Hexalith.Parties.Aspire` | Hosting extensions | Aspire |

### Requirements to Structure Mapping

**FR Category → Project Location:**

| FR Category | Primary Project | Key Files |
|-------------|----------------|-----------|
| Party Lifecycle (FR1-7) | Contracts (types), Server (Handle) | `Commands/CreateParty.cs`, `PartyAggregate.cs` |
| Contact Channels (FR8-11) | Contracts (types), Server (Handle) | `Commands/AddContactChannel.cs`, `ValueObjects/ContactChannel.cs` |
| Identifiers (FR12-13) | Contracts (types), Server (Handle) | `Commands/AddIdentifier.cs`, `ValueObjects/PartyIdentifier.cs` |
| Discovery & Search (FR14-19) | Projections (handlers/actors), Contracts (models) | `PartyIndexProjectionHandler.cs`, `PartyIndexEntry.cs` |
| AI Agent / MCP (FR20-25, FR74) | Parties/Mcp/ | `CreatePartyMcpTool.cs`, `UpdatePartyMcpTool.cs` |
| Developer Integration (FR26-33) | Client (abstractions), Contracts (types) | `IPartiesCommandClient.cs`, `AddPartiesClient()` |
| Event-Driven (FR34-38) | AppHost (DAPR config), EventStore | `DaprComponents/pubsub.yaml`, `subscription-parties.yaml` |
| Multi-Tenancy (FR39-43) | Parties service (middleware), EventStore | `PartiesController.cs` (JWT extraction) |
| GDPR (FR44-55) | Contracts (`[PersonalData]`), Server (v1.1) | Attribute placement on value objects |
| System Resilience (FR64, 71) | ServiceDefaults, Parties service | `Extensions.cs` (health checks) |

**Cross-Cutting Concern → Location:**

| Concern | Location |
|---------|----------|
| Multi-tenancy enforcement | EventStore (write-side), Projections actors (read-side query filtering) |
| GDPR `[PersonalData]` markers | Contracts value objects (attribute placement) |
| Observability | ServiceDefaults `Extensions.cs`, all projects via `ILogger<T>` |
| Error handling | Parties service `ErrorHandling/`, Server rejection events |
| Validation | Parties service `Validation/` (FluentValidation), Server `Handle` (domain rules) |
| Architectural fitness | Parties.Tests `FitnessTests/ArchitecturalFitnessTests.cs` |

### Integration Points

**Internal Communication:**

```
MCP Tool → (constructs command) → MediatR Pipeline → AggregateActor → Handle → Events
REST Controller → (same path) → MediatR Pipeline → AggregateActor → Handle → Events
Events → DAPR Pub/Sub → Projection Actors → Handlers → Update Actor State
Query → Projection Actor → Return State (PartyDetail or PartyIndexEntry[])
```

**External Integrations:**
- DAPR state store: Actor state persistence (events, snapshots, projection state)
- DAPR pub/sub: Event publishing to subscribers
- DAPR configuration store: Runtime configuration
- MCP protocol: AI agent tool interface (5 tools)
- NuGet: Package distribution (6 packages)

**Data Flow (Write Path):**

```
Client → REST/MCP → FluentValidation → MediatR → AggregateActor
  → Rehydrate (events/snapshot) → Handle(cmd, state) → DomainResult
  → Persist events → Publish to pub/sub → Return result
```

**Data Flow (Read Path):**

```
Published events → DAPR subscription → Projection Actor
  → Handler.Apply(event) → Update actor state (batch)

Client → REST/MCP → Query → Projection Actor → Return PartyDetail/PartyIndexEntry[]
```

### Development Workflow

**Local Development:**

```bash
dotnet aspire run --project src/Hexalith.Parties.AppHost
# Starts: Parties service + DAPR sidecar + Redis (state + pub/sub) + Aspire dashboard
```

**Build & Test:**

```bash
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release

# Tier 1 — No external deps
dotnet test tests/Hexalith.Parties.Contracts.Tests/
dotnet test tests/Hexalith.Parties.Client.Tests/
dotnet test tests/Hexalith.Parties.Server.Tests/
dotnet test tests/Hexalith.Parties.Projections.Tests/

# Tier 2 — DAPR slim init required
dapr init --slim
dotnet test tests/Hexalith.Parties.Tests/

# Tier 3 — Full DAPR + Docker
dapr init
dotnet test tests/Hexalith.Parties.IntegrationTests/
```

## Architecture Validation Results

### Coherence Validation

**Decision Compatibility: PASS**

All 19 architectural decisions are internally consistent with no contradictions:

- **Projection stack** (D1 + D4 + D5): DAPR actor-managed projections with hybrid granularity and partitioned index state form a coherent read model strategy
- **Composite command group** (D8 + D9 + D10 + D12 + D17): Atomic operations in single actor turns, with idempotency, conflict detection, and payload size limits working together
- **Platform delegation** (D3 + D13): Consistent pattern — snapshot and event ordering are EventStore's responsibility, not Parties'
- **Projection lifecycle** (D14 + D15 + D18): Pure handler extraction enables rebuild, health monitoring triggers rebuild on corruption — clean chain
- **GDPR preparation** (D6 + D7): Type-dependent `[PersonalData]` markers at MVP with documented reclassification complexity for v1.1
- **MCP boundary** (D11): Translation layer boundary machine-verifiable via CI fitness tests — no domain logic leakage possible

Technology versions are compatible: .NET 10, DAPR SDK 1.16.1, Aspire 13.1.2, MediatR 14.0.0, FluentValidation 12.1.1, MCP SDK 1.0.0.

**Pattern Consistency: PASS**

- Type declaration patterns (sealed record/class) uniform across all domain types
- Naming conventions consistent: commands (imperative verb + entity), events (entity + past participle), state (Domain + State), aggregate (Domain + Aggregate)
- JSON conventions defined once and applied globally (camelCase, ISO 8601, string enums, omit nulls)
- DI registration follows consistent builder pattern (AddParties/AddPartiesClient/UseParties/AddHexalithParties)
- Test conventions uniform ({Method}\_{Scenario}\_{ExpectedResult})

**Structure Alignment: PASS**

- 10 src projects map cleanly to all 19 architectural decisions
- Dependency direction is strict and machine-verifiable: Client → Contracts only, MCP → no event types, Handlers → no DAPR
- 6 NuGet packages align with project boundaries
- FR-to-structure mapping covers all 11 FR groups

### Requirements Coverage Validation

**Functional Requirements Coverage: 74/74 COVERED**

Coverage is phase-aware: MVP stories implement the MVP subset, while v1.1/v1.2/future stories and architecture decisions preserve deferred roadmap coverage.

| FR Group | FRs | Coverage | Resolution |
|----------|-----|----------|------------|
| Party Lifecycle (FR1-7) | 7 | 7/7 | Commands, events, aggregate Handle/Apply, display name derivation |
| Contact Channels (FR8-11) | 4 | 4/4 | FR11 (preferred channel) resolved — `PreferredContactChannelChanged` event added |
| Identifiers (FR12-13) | 2 | 2/2 | Add/remove commands and events |
| Discovery & Search (FR14-19, FR56, FR68, FR72) | 10 | 10/10 | FR68 resolved — `PartyIndexEntry` includes `CreatedAt`/`LastModifiedAt` for date filtering |
| AI Agent / MCP (FR20-25, FR74) | 7 | 7/7 | 5 MCP tools, composite commands, forgiving input, complete responses |
| Developer Integration (FR26-33, FR57-60, FR69) | 12 | 12/12 | Client abstractions, REST API, OpenAPI, sample, DI, error catalog |
| Event-Driven (FR34-38, FR63, FR70, FR73) | 8 | 8/8 | FR37 resolved — `PartyMerged` forward-compat placeholder added to Events |
| Multi-Tenancy (FR39-43, FR61-62) | 7 | 7/7 | JWT extraction, fail-closed, deployment validation |
| GDPR (FR44-55) | 12 | 12/12 | v1.1 deferred; D6/D7 foundation with `[PersonalData]` attributes at MVP |
| Resilience (FR64, FR71) | 2 | 2/2 | Graceful degradation, health/readiness endpoints |
| Admin (FR65-67) | 3 | 3/3 | v1.2 deferred |

**Non-Functional Requirements Coverage: 33/33 COVERED**

| NFR Category | NFRs | Coverage | Key Architectural Support |
|-------------|------|----------|--------------------------|
| Performance (NFR1-6) | 6 | 6/6 | D8/D9 composite commands (< 1s), D4 projection actors (< 500ms), D3 snapshots (< 200ms rehydration), D16 batch processing (< 2s consistency) |
| Security (NFR7-13) | 7 | 7/7 | D6 type-dependent encryption (v1.1), EventStore JWT/tenant middleware, CI fitness tests |
| Scalability (NFR14-19) | 6 | 6/6 | D5 partitioned index (100K parties), D4 hybrid projection, DAPR actor model |
| Reliability (NFR20-24) | 5 | 5/5 | EventStore event replay, DAPR pub/sub at-least-once, D10 idempotent commands |
| Integration (NFR25-29) | 5 | 5/5 | OpenAPI 3.x, MCP 5 tools, stable event schemas, zero-dep Contracts |
| Developer Experience (NFR30-32) | 3 | 3/3 | Aspire AppHost (< 15 min deploy), minimal client packages |

### Implementation Readiness Validation

**Decision Completeness: PASS**

- All 19 decisions documented with technology versions, rationale, consequences, and affected components
- Implementation sequence explicitly ordered with cross-component dependencies mapped
- Quantitative validation provided (comparative analysis scores: composite 2.65/3.00, hybrid projection 2.85/3.00)

**Structure Completeness: PASS**

- Complete directory tree defined to individual `.cs` file level
- Architectural boundaries machine-verifiable via 5 CI fitness tests
- 6 NuGet packages with explicit dependency chains
- FR-to-structure mapping complete for all 11 FR categories + 6 cross-cutting concerns

**Pattern Completeness: PASS**

- 14 enforcement guidelines covering all agent conflict points
- 8 explicit anti-patterns (forbidden practices)
- Code examples for all major patterns: commands, events, state, aggregate, value objects, composite results
- Test data builder convention, assertion convention, tier compliance rules all specified
- DI registration, configuration, logging, error handling, validation all documented

### Gap Analysis Results

**Gaps Found During Validation: 4 (all resolved)**

| # | Gap | Priority | Resolution |
|---|-----|----------|------------|
| 1 | FR11 — No explicit event for marking preferred contact channel | Important | Added `PreferredContactChannelChanged` event to both Events listings |
| 2 | FR68 — Date range filtering not addressed in key-value projection model | Important | Added `CreatedAt`/`LastModifiedAt` to `PartyIndexEntry` model documentation |
| 3 | MCP tool naming — PRD, architecture, and Epic 12 use `find_parties`, `get_party`, `create_party`, `update_party`, and `delete_party` | Important | Resolved by making the architecture/Epic 12 names canonical. `find_parties` unifies search + list, and `delete_party` maps to soft deactivation (FR4), not GDPR erasure. |
| 4 | FR37 — `PartyMerged` forward-compat event placeholder missing from Events listing | Minor | Added `PartyMerged.cs` to Events in both patterns and structure sections |

**Observations (non-blocking):**

- `UpdatePartyComposite` includes `UpdateIdentifiers[]` but no standalone `UpdateIdentifier` command exists. PRD only specifies add (FR12) and remove (FR13). Recommend removing `UpdateIdentifiers[]` from composite or confirming it's an intentional architectural addition for identifier value corrections (e.g., fixing a typo in a VAT number)
- FR11 preferred channel marking can be handled via `UpdateContactChannel` (setting `IsPreferred` flag) plus the new `PreferredContactChannelChanged` event — confirm during story preparation

### Architecture Completeness Checklist

**Requirements Analysis**

- [x] Project context thoroughly analyzed (74 FRs, 33 NFRs mapped)
- [x] Scale and complexity assessed (High — event sourcing, CQRS, DAPR, multi-tenancy, GDPR, 4 surfaces)
- [x] Technical constraints identified (EventStore conventions, DAPR dependencies, platform validation mandate)
- [x] Cross-cutting concerns mapped (10 concerns: multi-tenancy, GDPR, schema evolution, portability, AI ergonomics, projections, observability, degradation, DX, testability)

**Architectural Decisions**

- [x] 19 critical decisions documented with versions, rationale, and consequences
- [x] Technology stack fully specified with verified current versions
- [x] Integration patterns defined (write path, read path, internal/external)
- [x] Performance considerations addressed (composite commands, batch processing, partitioned state)
- [x] Decisions stress-tested via 10 advanced elicitation methods + party mode review

**Implementation Patterns**

- [x] Naming conventions established (commands, events, state, aggregate, value objects, MCP tools)
- [x] Structure patterns defined (namespace/project organization, file naming)
- [x] Communication patterns specified (REST, MCP, pub/sub, DAPR state keys)
- [x] Process patterns documented (error handling, validation, logging, DI, configuration)
- [x] 14 enforcement guidelines + 5 fitness tests + 8 anti-patterns

**Project Structure**

- [x] Complete directory structure defined (10 src, 6 tests, 1 sample, deploy, docs, CI)
- [x] Component boundaries established with machine-verifiable fitness tests
- [x] Integration points mapped (write path, read path, internal communication, external integrations)
- [x] Requirements to structure mapping complete (all FR groups + cross-cutting concerns)

### Architecture Readiness Assessment

**Overall Status: READY FOR IMPLEMENTATION**

**Confidence Level: HIGH**

The architecture has been validated through:
- 10 advanced elicitation methods (pre-mortem, ADR, self-consistency, first principles, red team/blue team, failure mode analysis, what-if scenarios, chaos monkey, comparative analysis, Occam's razor)
- 5 additional elicitation methods on implementation patterns (code review gauntlet, reverse engineering, self-consistency, challenge from critical perspective, critique and refine)
- Party mode multi-agent review (2 sessions)
- Comprehensive requirements coverage validation (74 FRs + 33 NFRs)
- Internal coherence validation (19 decisions, patterns, structure)

**Key Strengths:**

1. **Quantitatively defensible decisions** — Composite aggregate pattern (2.65/3.00) and hybrid projection model (2.85/3.00) selected via comparative analysis, not intuition
2. **Machine-verifiable boundaries** — 5 architectural fitness tests enforce critical constraints in CI, preventing erosion during implementation
3. **Platform validation focus** — Architecture explicitly tests EventStore's programming model (composite commands, convention discovery, projection infrastructure) rather than working around it
4. **Forward-compatible by design** — Every contract (events, API, NuGet, projection interface) supports additive evolution. `PartyMerged` placeholder validates the principle
5. **GDPR-ready infrastructure** — `[PersonalData]` attributes at MVP are zero-cost preparation for v1.1 crypto-shredding activation
6. **Comprehensive test strategy** — Three-tier testing with pure handler extraction enabling Tier 1 coverage of projection logic without DAPR

**Areas for Future Enhancement:**

1. Full-text search via dedicated search engine (v1.1 — D2)
2. Crypto-shredding activation with key management (v1.1 — D6/D7)
3. Automated projection drift detection (v1.1 — D14)
4. Admin portal frontend architecture (v1.2)
5. Multi-key index partitioning under scale pressure (D5 interface-first)
6. `UpdateIdentifiers[]` in composite — confirm or remove during story preparation

### Implementation Handoff

**AI Agent Guidelines:**

- Follow all 19 architectural decisions exactly as documented — decisions are collaborative outcomes, not suggestions
- Use implementation patterns consistently across all components — 14 enforcement rules, 8 anti-patterns
- Respect project structure and boundaries — dependency direction is strict and CI-enforced
- Refer to this document for all architectural questions before making autonomous decisions
- Test matrix for composite commands (D19) must be defined in story specifications before implementation begins

**First Implementation Priority:**

1. Project scaffolding: solution file, Directory.Build.props, Directory.Packages.props, global.json, .editorconfig (mirror EventStore structure)
2. Contracts project: commands, events, state, value objects, models, results
3. Server project: PartyAggregate with Handle/Apply methods
4. Tier 1 tests for aggregate logic (pure domain unit tests)
