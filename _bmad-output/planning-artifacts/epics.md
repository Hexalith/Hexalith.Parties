---
stepsCompleted: ['step-01-validate-prerequisites', 'step-02-design-epics', 'step-03-create-stories']
inputDocuments:
  - prd.md
  - architecture.md
  - prd-validation-report.md
---

# Hexalith.Parties - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.Parties, decomposing the requirements from the PRD, Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

**Party Lifecycle Management (MVP) — 7 FRs**

- FR1: Authorized client can create a new party as either a person or an organization with type-specific details
- FR2: Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix)
- FR3: Authorized client can update organization-specific details (legal name, trading name, legal form, registration number)
- FR4: Authorized client can deactivate a party (soft lifecycle management)
- FR5: Authorized client can reactivate a previously deactivated party
- FR6: System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation — "{FirstName} {LastName}" for persons, "{LegalName}" for organizations; locale-aware formatting deferred to v1.1)
- FR7: Each party has a client-generated, immutable UUID as its stable identity

**Contact Channel Management (MVP) — 4 FRs**

- FR8: Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social)
- FR9: Authorized client can update an existing contact channel on a party
- FR10: Authorized client can remove a contact channel from a party
- FR11: Authorized client can mark a contact channel as preferred for its type

**Identifier Management (MVP) — 2 FRs**

- FR12: Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references)
- FR13: Authorized client can remove an identifier from a party

**Party Discovery & Search (MVP unless noted) — 10 FRs**

- FR14: Consumer can list parties with pagination and filtering by type (person/organization) and active status
- FR15: Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability.
- FR16: *(Deferred to v1.1)* Consumer can perform semantic search across parties
- FR17: Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans. MVP emits `displayName`; future search can emit `email` and `identifier`.
- FR18: Consumer can retrieve full party details by ID
- FR19: Recently created or updated parties become discoverable in search results within the eventual consistency window defined by NFR6
- FR56: System publishes auto-generated API specification documentation accessible to developers
- FR68: Consumer can filter parties by creation date or last-modified date range
- FR72: *(Deferred to v1.1)* Consumer can query a party's historical name at a specific point in time (temporal name query)

**AI Agent Identity Resolution (MVP) — 7 FRs**

- FR20: AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP. Email and identifier resolution requires candidate detail retrieval or the future dedicated search capability.
- FR21: AI agent can create a complete party (type details + contact channels + identifiers) in a single composite operation
- FR22: AI agent can update party details, add/modify/remove contact channels and identifiers via a single operation
- FR23: AI agent can retrieve full party details and list parties via dedicated AI-optimized tools
- FR24: AI agent party creation returns the complete created party record, not just an identifier
- FR25: AI agent tools accept partial and incomplete input gracefully, with documented default behaviors for omitted fields, and clear validation error messages when required fields are missing
- FR74: MCP update operations use patch semantics — only specified fields are modified; unspecified fields remain unchanged

**Developer Integration (MVP) — 12 FRs**

- FR26: .NET developer can integrate party management via a single package and one-line dependency registration
- FR27: Developer can send party commands via typed client abstractions without infrastructure knowledge
- FR28: Developer can query parties via typed client abstractions without infrastructure knowledge
- FR29: Developer can interact with the party service via REST API from any programming language
- FR30: System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action
- FR31: Developer can deploy a running instance from source with standard container tooling
- FR32: Getting-started documentation enables a developer to deploy and send their first command as a self-service experience
- FR33: Contract types package has zero runtime dependencies beyond netstandard2.1
- FR57: System supports versioned API endpoints that coexist during deprecation periods
- FR58: System maps domain rejections to standardized HTTP error formats with a documented error catalog
- FR59: System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage
- FR60: Developer can run the full system locally with a single command for development and evaluation
- FR69: Update operations (API and MCP) return the updated party state in the response, not just a confirmation

**Event-Driven Integration (MVP) — 8 FRs**

- FR34: System publishes domain events when party state changes
- FR35: Consuming application can subscribe to party events and build domain-specific read models
- FR36: System handles duplicate commands idempotently (safe deduplication in distributed scenarios)
- FR37: Forward-compatible event contracts (including party merge) are available to consuming applications from day one
- FR38: Consuming application documentation includes handler patterns for erasure and dangling reference cleanup
- FR63: System guarantees at-least-once event delivery to subscribers
- FR70: Published domain events include tenant context for consuming application routing decisions
- FR73: System delivers events for a single aggregate in causal order to each subscriber

**Multi-Tenancy & Security (MVP) — 7 FRs**

- FR39: System isolates party data by tenant at all layers — no cross-tenant data access is possible
- FR40: System identifies tenant from authenticated credentials, never from request payloads
- FR41: System rejects requests without valid tenant identity (fail-closed)
- FR42: Personal data fields are architecturally marked for automated privacy enforcement without domain code changes
- FR43: Personal data fields are excluded from all application logging
- FR61: System provides deployment validation tooling to verify security configuration before production use
- FR62: System displays a non-dismissable compliance warning until GDPR features are activated

**GDPR Compliance (v1.1) — 12 FRs**

- FR44: Administrator can trigger right-to-erasure, rendering all personal data for a party permanently unreadable
- FR45: System verifies erasure completion across all internal data stores and reports results
- FR46: System notifies all subscribers when a party is erased so they can clean up their references
- FR47: Administrator can record per-channel, per-purpose consent for a specific party
- FR48: Administrator can revoke previously recorded consent
- FR49: Administrator can restrict processing of a party's data (freeze while complaint is investigated)
- FR50: Administrator can lift restriction on a party's data to resume processing
- FR51: Administrator can export all data for a specific party in a machine-readable format
- FR52: System maintains a complete, time-stamped record of all processing activities on party data
- FR53: System encrypts personal data in stored events and snapshots using per-party keys
- FR54: Events published to subscribers contain readable data — subscribers never handle decryption
- FR55: System returns an "erased" status for erased parties, not cryptographic errors

**System Resilience (MVP) — 2 FRs**

- FR64: System degrades gracefully when infrastructure components are unavailable — read operations continue when write-side components fail
- FR71: System exposes health and readiness signals for infrastructure orchestration

**Administration & Frontend (v1.2) — 3 FRs**

- FR65: Administrator can browse, search, and inspect party records via an administration interface
- FR66: Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface
- FR67: Consuming application developer can embed a party picker component in their UI for party search and selection

**Total: 74 Functional Requirements (54 MVP + 14 v1.1 + 3 v1.2 + 3 deferred)**

### NonFunctional Requirements

**Performance — 6 NFRs**

- NFR1: Command processing (create, update, manage party) completes in < 1 second at NFR17 throughput levels; MCP tool calls complete in < 1 second end-to-end including transport
- NFR2: Query operations (search, get by ID, list) return results in < 500ms at NFR17 throughput levels
- NFR3: Aggregate rehydration completes in < 200ms with snapshot strategy active
- NFR4: Search across 100K parties per tenant returns results within 500ms
- NFR5: Service accepts requests within 30 seconds of container launch (cold start)
- NFR6: Read projections reflect write operations within 2 seconds at NFR17 throughput levels (eventual consistency window)

**Security — 7 NFRs**

- NFR7: All data encrypted in transit (TLS 1.2+)
- NFR8: Personal data fields encrypted at rest using per-party keys (activated in v1.1)
- NFR9: Tenant isolation enforced at all layers — zero cross-tenant data leakage under any condition
- NFR10: JWT token validation on every request; fail-closed on invalid or missing tokens
- NFR11: Per-tenant encryption keys can be rotated without service downtime or data loss
- NFR12: Personal data excluded from all application logs
- NFR13: All API endpoints require authentication — no anonymous access

**Scalability — 6 NFRs**

- NFR14: System supports multi-tenant operation validated at 100 concurrent tenants for MVP
- NFR14a: System architecture supports scaling beyond 100 tenants without per-tenant infrastructure changes
- NFR15: Tenant metadata operations complete in < 50ms regardless of total tenant count
- NFR16: System supports up to 100,000 parties per tenant (MVP validation target)
- NFR17: System sustains 100 read requests/second and 20 write requests/second per tenant
- NFR18: Event store performance degrades < 10% at 100K parties per tenant with snapshot strategy active
- NFR19: Read projections remain responsive (< 500ms) at 100K parties per tenant

**Reliability — 5 NFRs**

- NFR20: Service recovers from crash, replays necessary event state, and accepts requests within 30 seconds of restart
- NFR21: When event store is unreachable, read projection queries continue serving cached data with a staleness indicator
- NFR22: No data loss on service restart — event store is the durable source of truth
- NFR23: At-least-once event delivery to subscribers via DAPR pub/sub
- NFR24: Idempotent command handling ensures safe retry without duplicate side effects

**Integration — 5 NFRs**

- NFR25: REST API conforms to auto-generated OpenAPI 3.x specification
- NFR26: MCP server implements MCP protocol specification with 5 tools
- NFR27: Published events follow stable, versioned contract schemas (append-only, additive changes only)
- NFR28: Client NuGet packages impose < 10 transitive dependencies totalling < 5 MB
- NFR29: Service has zero direct dependencies on specific state store or message broker implementations

**Developer Experience — 3 NFRs**

- NFR30: A developer deploys a running instance from source in < 15 minutes on first attempt
- NFR31: NuGet client package size < 5MB with < 10 transitive dependencies
- NFR32: (v1.2) Frontend applies output encoding to all party data fields rendered in the admin portal

**Total: 33 Non-Functional Requirements**

### Additional Requirements

**From Architecture — Starter Template:**

- Architecture specifies EventStore Solution Structure Pattern as the starter (manual scaffolding, no CLI generator). This impacts Epic 1 Story 1: project initialization must mirror EventStore conventions (Directory.Build.props, Directory.Packages.props, global.json, .editorconfig, modern XML solution format .slnx)

**From Architecture — Critical Architectural Decisions (19 total):**

- D1: Projection data store uses DAPR actor-managed, key/value JSON (no dedicated query DB)
- D2: Full-text search deferred to v1.1; basic key-lookup and list/filter in v1.0
- D3: Snapshot strategy managed entirely by Hexalith.EventStore
- D4: Hybrid projection actor granularity — per-party detail actor + per-tenant index actor
- D5: Index actor state uses partitioned state with interface-first approach (single-key v1.0)
- D6: Personal data scope is type-dependent (person vs. organization), with IsNaturalPerson flag for sole traders
- D7: IsNaturalPerson reclassification is a documented v1.1 complexity hotspot
- D8: MCP create uses composite aggregate command (CreatePartyComposite — single actor turn, atomic)
- D9: MCP update uses composite command with aggregate-side diff (UpdatePartyComposite — explicit add/update/remove lists)
- D10: Sub-operation idempotency — skip duplicate additions, reject invalid IDs, reject conflicting operations
- D11: MCP layer is a translation layer (no domain logic, no event type references, enforced via CI fitness test)
- D12: Partial failure eliminated by design — all composite operations are all-or-nothing
- D13: Event ordering delegated entirely to EventStore
- D14: Projection rebuild via event replay through pure handlers (manual v1.0, automated drift detection v1.1)
- D15: Projection health monitoring with auto-rebuild on corruption
- D16: Index actor batch event processing (configurable batch size/time)
- D17: Maximum composite payload size limit (e.g., 100 sub-operations, configurable)
- D18: Projection testability — pure handler classes extracted from actors (Tier 1 testable without DAPR)
- D19: Composite command test matrix must be designed upfront in story definitions

**From Architecture — Implementation Patterns & Constraints:**

- 14 enforcement guidelines for AI agents (sealed records, naming, JSON conventions, etc.)
- 8 explicit anti-patterns (positional parameters, V2 events, PII in logs, etc.)
- 5 architectural fitness tests enforced in CI
- Three-tier test strategy (Tier 1: no external deps, Tier 2: DAPR slim, Tier 3: full Aspire topology)
- 10 src projects, 6 test projects, 1 sample project
- 6 NuGet packages to publish
- Dependency direction strict and machine-verifiable

**From Architecture — Implementation Sequence Priority:**

1. Project scaffolding (solution file, build props, global.json, .editorconfig)
2. Contracts project (commands, events, state, value objects, models, results)
3. Server project (PartyAggregate with Handle/Apply methods)
4. Tier 1 tests for aggregate logic

**From Architecture — Open Items for Story Preparation:**

- `UpdateIdentifiers[]` in composite — confirm or remove (PRD only specifies add/remove, not update)
- FR11 preferred channel marking — confirm via `UpdateContactChannel` + `PreferredContactChannelChanged` event
- MCP tool naming: architecture uses `find_parties` + `delete_party` (refined from PRD's `search_parties` + `list_parties`)

### FR Coverage Map

| FR | Epic | Description |
|---|---|---|
| FR1 | Epic 1 | Create party (person/organization) |
| FR2 | Epic 1 | Update person details |
| FR3 | Epic 1 | Update organization details |
| FR4 | Epic 1 | Deactivate party |
| FR5 | Epic 1 | Reactivate party |
| FR6 | Epic 1 | Display name / sort name derivation |
| FR7 | Epic 1 | Client-generated immutable UUID |
| FR8 | Epic 2 | Add contact channel |
| FR9 | Epic 2 | Update contact channel |
| FR10 | Epic 2 | Remove contact channel |
| FR11 | Epic 2 | Mark preferred contact channel |
| FR12 | Epic 2 | Add identifier |
| FR13 | Epic 2 | Remove identifier |
| FR14 | Epic 3 | Paginated list with filters |
| FR15 | Epic 3 | Display-name search in MVP; email/identifier search deferred |
| FR16 | Epic 9 | Semantic search (v1.1) |
| FR17 | Epic 3 | Match metadata in search results |
| FR18 | Epic 1 | Retrieve party details by ID |
| FR19 | Epic 3 | Eventual consistency for search |
| FR20 | Epic 5 | AI agent search/resolve via MCP |
| FR21 | Epic 4+5 | Composite party creation (aggregate: Epic 4, MCP: Epic 5) |
| FR22 | Epic 4+5 | Composite party update (aggregate: Epic 4, MCP: Epic 5) |
| FR23 | Epic 5 | AI agent get/list via MCP |
| FR24 | Epic 5 | Complete party returned on create |
| FR25 | Epic 5 | Forgiving input schemas |
| FR26 | Epic 6 | Single NuGet package integration |
| FR27 | Epic 6 | Typed command client abstractions |
| FR28 | Epic 6 | Typed query client abstractions |
| FR29 | Epic 1 | REST API from any language |
| FR30 | Epic 1 | Typed rejection responses (DomainResult) |
| FR31 | Epic 1 | Deploy from source with container tooling |
| FR32 | Epic 6 | Getting-started documentation |
| FR33 | Epic 1 | Zero-dep contracts package |
| FR34 | Epic 7 | Publish domain events on state change |
| FR35 | Epic 7 | Subscribe to events for read models |
| FR36 | Epic 1 | Idempotent command handling |
| FR37 | Epic 7 | Forward-compatible event contracts (PartyMerged) |
| FR38 | Epic 7 | Handler patterns for erasure/dangling references |
| FR39 | Epic 1 | Tenant isolation at all layers |
| FR40 | Epic 1 | Tenant from credentials, not payloads |
| FR41 | Epic 1 | Reject requests without valid tenant (fail-closed) |
| FR42 | Epic 1 | [PersonalData] field attributes |
| FR43 | Epic 1 | Personal data excluded from logs |
| FR44 | Epic 9 | Right-to-erasure (crypto-shredding) |
| FR45 | Epic 9 | Erasure verification |
| FR46 | Epic 9 | Erasure notification to subscribers |
| FR47 | Epic 9 | Per-channel per-purpose consent |
| FR48 | Epic 9 | Consent revocation |
| FR49 | Epic 9 | Right to restriction |
| FR50 | Epic 9 | Lift restriction |
| FR51 | Epic 9 | Data portability export |
| FR52 | Epic 9 | Processing records (Article 30) |
| FR53 | Epic 9 | Field-level encryption per-party keys |
| FR54 | Epic 9 | Decrypted events at publish time |
| FR55 | Epic 9 | Erased party returns "erased" status |
| FR56 | Epic 3 | Auto-generated OpenAPI specification |
| FR57 | Epic 1 | Versioned API endpoints |
| FR58 | Epic 1 | Standardized HTTP error formats |
| FR59 | Epic 6 | Sample integration project |
| FR60 | Epic 1 | Run full system locally (single command) |
| FR61 | Epic 8 | Deployment validation tooling |
| FR62 | Epic 1 | Non-dismissable GDPR warning |
| FR63 | Epic 7 | At-least-once event delivery |
| FR64 | Epic 8 | Graceful degradation |
| FR65 | Epic 10 | Admin browse/search/inspect |
| FR66 | Epic 10 | Admin GDPR operations |
| FR67 | Epic 10 | Embeddable party picker |
| FR68 | Epic 3 | Date range filtering |
| FR69 | Epic 4 | Return updated state in responses |
| FR70 | Epic 7 | Tenant context in published events |
| FR71 | Epic 8 | Health/readiness signals |
| FR72 | Epic 9 | Temporal name query (v1.1) |
| FR73 | Epic 7 | Causal event ordering per aggregate |
| FR74 | Epic 5 | MCP patch semantics |

**Coverage: 74/74 FRs mapped — zero gaps.**

## Epic List

### Planning Readiness Reconciliation

The canonical implementation direction is now the EventStore-fronted plan recorded in `sprint-change-proposal-2026-05-07.md`, `sprint-status.yaml`, and Epic 12 story files. Earlier REST, TypeScript-admin, and upfront-contract wording in this document is retained only where it describes historical story origin. Current implementation and future story creation must follow the sequence and supersession notes below.

Execution sequence:
1. Epics 1-9: domain, discovery, integration, operations, GDPR/privacy/search foundation.
2. Epic 11: Hexalith.Tenants integration for Parties. This is a prerequisite for admin/frontend work.
3. Epic 12: EventStore-fronted architecture pivot and consumer migration.
4. Epic 10: administration and picker user experience, consumed through the FrontComposer/EventStore/Parties client boundary.

### Epic 1: Domain Foundation & Party Lifecycle
A developer can deploy the service from source with a single command, create parties (persons and organizations) with type-specific details, retrieve them by ID, and verify the full command-event cycle works — with tenant isolation, [PersonalData] markers, log sanitization, and GDPR compliance warning from day one. Historical Epic 1 work defined a broad contract surface; future contract additions must be sliced with the behavior that first consumes them.
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR6, FR7, FR18, FR29, FR30, FR31, FR33, FR36, FR39, FR40, FR41, FR42, FR43, FR57, FR58, FR60, FR62

### Epic 2: Contact Channels & Identifiers
A developer can enrich parties with structured contact channels (postal, email, phone, social) and jurisdiction-specific identifiers (VAT, SIRET, national ID), including marking preferred channels. Adds all contact channel and identifier Handle/Apply implementations to the aggregate and extends the REST API.
**FRs covered:** FR8, FR9, FR10, FR11, FR12, FR13

### Epic 3: Party Discovery & Search (Read Projections)
Consumers can discover parties through paginated listing, display-name search with match metadata, and filtering by type and date range — all reflecting updates within 2 seconds. Builds the entire read projection infrastructure: PartyDetailProjectionActor, PartyIndexProjectionActor, pure handler extraction (D18), partitioned index state (D5), batch event processing (D16). Query REST endpoints and OpenAPI specification. Email and identifier search are intentionally deferred because the v1.0 index projection does not store those searchable fields.
**FRs covered:** FR14, FR15, FR17, FR19, FR56, FR68

### Epic 4: Composite Commands & Advanced Aggregate Logic
The party aggregate supports atomic composite operations — creating a full party with channels and identifiers in one command (CreatePartyComposite), and updating via explicit add/update/remove lists (UpdatePartyComposite) — with sub-operation idempotency, conflict detection, payload size limits, and complete updated state returned in responses. This isolates the most complex domain logic (D8, D9, D10, D12, D17) with the upfront test matrix (D19).
**FRs covered:** FR21 (aggregate), FR22 (aggregate), FR69

### Epic 5: AI Agent Party Management (MCP Server)
AI agents can perform complete party management through 5 MCP tools (create_party, find_parties, get_party, update_party, delete_party) — with forgiving input schemas, complete response payloads, patch semantics, and disambiguation support via match metadata. Focuses purely on the MCP translation layer (D11) with architectural fitness enforcement (zero domain event type references).
**FRs covered:** FR20, FR21 (MCP), FR22 (MCP), FR23, FR24, FR25, FR74

### Epic 6: Developer Integration & Documentation
A .NET developer integrates party management with a single NuGet package and one-line DI registration (AddPartiesClient()). A non-.NET developer follows the getting-started guide and sends their first command in under 30 minutes. A sample integration project demonstrates command, query, event subscription, and MCP usage patterns.
**FRs covered:** FR26, FR27, FR28, FR32, FR59

### Epic 7: Event-Driven Integration & Subscriber Experience
Consuming applications can subscribe to party domain events and build domain-specific read models, with forward-compatible contracts (including PartyMerged placeholder), documented handler patterns for erasure and dangling references, at-least-once delivery verification, tenant context in events, and causal ordering documentation. Event publishing infrastructure (DAPR pub/sub) is configured in Epic 1; this epic focuses on subscriber experience, documentation, and the sample event subscription demo.
**FRs covered:** FR34, FR35, FR37, FR38, FR63, FR70, FR73

### Epic 8: Operational Readiness & Production Hardening
Operators can deploy with confidence using deployment validation tooling (DAPR security config verification), monitor health and readiness signals, and trust that the service degrades gracefully when infrastructure components fail. Includes projection health monitoring with auto-rebuild on corruption (D15) and projection rebuild admin endpoint (D14).
**FRs covered:** FR61, FR64, FR71

### Epic 9: GDPR, Privacy, and v1.1 Search Extensions
Administrators can fulfill GDPR obligations and the platform can support privacy-safe v1.1 search and audit extensions. GDPR erasure, consent, restriction, portability, processing records, per-party keys, erased-state behavior, and subscriber notification remain the primary compliance scope. Temporal name queries and Hexalith.Memories-backed search are explicitly v1.1 extensions and must retain privacy, erasure, and tenant-isolation guarantees.
**FRs covered:** FR44, FR45, FR46, FR47, FR48, FR49, FR50, FR51, FR52, FR53, FR54, FR55, FR16, FR72

### Epic 11: Hexalith.Tenants Integration for Parties
Hexalith.Parties uses Hexalith.Tenants as the source of truth for tenant lifecycle, membership, roles, and tenant configuration while preserving Parties-owned tenant isolation for party aggregates, projections, REST, MCP, and event publication. This cross-cutting epic is scheduled before Epic 10 so the admin portal consumes tenant context instead of duplicating tenant management.
**FRs covered:** FR39, FR40, FR41, FR75

### Epic 10: Administration & Frontend (v1.2)
Administrators can browse, search, and inspect party records and process party-level GDPR requests via a FrontComposer-based Blazor/Razor admin portal that consumes EventStore-fronted Parties client/query/command contracts. Generic event and stream browsing is delegated to EventStore Admin UI through safe deep-links. Tenant lifecycle, membership, roles, and configuration remain owned by Hexalith.Tenants admin capabilities. Consuming application developers can embed a party picker component in their UIs for party search and selection.
**FRs covered:** FR65, FR66, FR67

---

## Epic 1: Domain Foundation & Party Lifecycle

A developer can deploy the service from source with a single command, create parties (persons and organizations) with type-specific details, retrieve them by ID, and verify the full command-event cycle works — with tenant isolation, [PersonalData] markers, log sanitization, and GDPR compliance warning from day one. Historical Epic 1 work defined a broad contract surface; future contract additions must be sliced with the behavior that first consumes them.

### Story 1.1: Project Scaffolding & Solution Structure

As a developer,
I want a properly structured .NET solution following EventStore conventions,
So that I have a consistent, buildable foundation for the party management service.

**Acceptance Criteria:**

**Given** a clean checkout of the Hexalith.Parties repository
**When** the solution structure is created
**Then** the following files exist at the repository root:
- `Hexalith.Parties.slnx` (modern XML solution format)
- `Directory.Build.props` (net10.0, nullable, TreatWarningsAsErrors, NuGet metadata, MinVer 7.0.0)
- `Directory.Packages.props` (central package management with all dependency versions matching architecture spec)
- `global.json` (SDK 10.0.103, rollForward: latestPatch)
- `.editorconfig` (Allman, 4-space, CRLF, UTF-8 — copied from EventStore)
- `.gitignore` (copied from EventStore)
- `LICENSE` (MIT)
**And** all 10 source projects exist as empty .csproj stubs under `src/`:
- Hexalith.Parties.Contracts, Client, Server, Projections, Parties service, Aspire, AppHost, ServiceDefaults, Testing
**And** all 6 test projects exist as empty .csproj stubs under `tests/`:
- Contracts.Tests, Client.Tests, Server.Tests, Projections.Tests, Parties.Tests, IntegrationTests
**And** the sample project exists as an empty .csproj stub under `samples/`:
- Hexalith.Parties.Sample
**And** project references follow the strict dependency direction:
- Contracts ← Client, Server, Projections
- Contracts + Server + Projections ← Parties service
- All src/ ← Testing
**And** `dotnet restore Hexalith.Parties.slnx` completes without errors
**And** `dotnet build Hexalith.Parties.slnx` compiles successfully (empty projects, no source files yet)

### Story 1.2: Domain Contracts — Initial Party Lifecycle Contract Slice

As a developer,
I want the initial party lifecycle contracts needed by the first aggregate and retrieval stories,
So that the domain model can grow incrementally with the behavior that consumes each contract.

Planning note: this story was completed historically with a broader contract surface. Do not use that historical breadth as the pattern for future stories. Contact/identifier contracts belong with Epic 2 behavior, projection query models with Epic 3 behavior, composite result contracts with Epic 4 behavior, MCP-specific schemas with Epic 5 behavior, and GDPR/privacy/search contracts with the relevant v1.1 stories.

**Acceptance Criteria:**

**Given** the Contracts project from Story 1.1
**When** all domain types are defined
**Then** the following command types exist as sealed records with `{ get; init; }` properties:
- `CreateParty`, `CreatePartyComposite`, `UpdatePartyComposite`
- `UpdatePersonDetails`, `UpdateOrganizationDetails`, `SetIsNaturalPerson`
- `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`
- `AddIdentifier`, `RemoveIdentifier`
- `DeactivateParty`, `ReactivateParty`
**And** commands carry `PartyId` (aggregate ID) but NOT `TenantId` (extracted from request context)
**And** entity IDs (ContactChannelId, IdentifierId) are client-generated UUIDs
**And** the following event types exist as sealed records implementing `IEventPayload`:
- `PartyCreated`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`
- `ContactChannelAdded`, `ContactChannelUpdated`, `ContactChannelRemoved`, `PreferredContactChannelChanged`
- `IdentifierAdded`, `IdentifierRemoved`
- `IsNaturalPersonChanged`
- `PartyDeactivated`, `PartyReactivated`
- `PartyDisplayNameDerived`
- `PartyMerged` (v2 forward-compatibility placeholder — FR37)
**And** rejection events exist implementing `IRejectionEvent`:
- `PartyCannotBeCreatedWithoutType`, `PartyCannotAddDuplicateChannel` (and other rejection scenarios)
**And** the following value objects exist as sealed records:
- `PersonDetails`, `OrganizationDetails`, `ContactChannel`, `PartyIdentifier`
- `PostalAddress`, `EmailAddress`, `PhoneNumber`, `SocialMediaHandle`
**And** enums exist: `PartyType` (Person, Organization), `ContactChannelType`, `IdentifierType`
**And** `PartyState` exists as a sealed class with `{ get; private set; }` properties, `Apply` methods for ALL event types, and private list backing fields for collections
**And** query models exist: `PartyDetail` (full party view), `PartyIndexEntry` (lightweight summary with CreatedAt, LastModifiedAt)
**And** `CompositeCommandResult` extends `DomainResult` with Applied/Skipped/Rejected collections
**And** `[PersonalData]` attributes are applied to: PersonDetails fields (first name, last name, DOB, prefix, suffix), all contact channel payloads, identifier values, and derived fields (display name, sort name) — following D6 type-dependent scope
**And** `IsNaturalPerson` boolean exists on OrganizationDetails or PartyState — when true, elevates org to person-level encryption scope
**And** the Contracts project has zero runtime dependencies beyond netstandard2.1 (FR33)
**And** no positional record parameters are used (anti-pattern)
**And** one public type per file, file name = type name

### Story 1.3: Party Aggregate — Party Creation

As a developer,
I want to create new parties (persons and organizations) via the Party aggregate,
So that the core party lifecycle begins with type-discriminated party creation and automatic display name derivation.

**Acceptance Criteria:**

**Given** no party exists with the specified ID
**When** a `CreateParty` command is handled with `PartyType.Person` and `PersonDetails` (first name, last name)
**Then** a `PartyCreated` event is emitted with the party type and person details
**And** a `PartyDisplayNameDerived` event is emitted with display name = "{FirstName} {LastName}" and sort name = "{LastName}, {FirstName}" (FR6)
**And** the party ID matches the client-generated UUID from the command (FR7)

**Given** no party exists with the specified ID
**When** a `CreateParty` command is handled with `PartyType.Organization` and `OrganizationDetails` (legal name)
**Then** a `PartyCreated` event is emitted with the party type and organization details
**And** a `PartyDisplayNameDerived` event is emitted with display name = "{LegalName}" and sort name = "{LegalName}" (FR6)

**Given** a party already exists with the specified ID
**When** a `CreateParty` command is handled with the same ID
**Then** the command is handled idempotently — no duplicate events are emitted (FR36)

**Given** a `CreateParty` command with no party type specified
**When** the command is handled
**Then** a `PartyCannotBeCreatedWithoutType` rejection event is returned
**And** the `DomainResult` indicates rejection with a clear error message

**Given** the `PartyAggregate` class
**When** reviewed for implementation patterns
**Then** it inherits `EventStoreAggregate<PartyState>`
**And** the `Handle` method is synchronous (returns `DomainResult`, not `Task<DomainResult>`)
**And** domain logic is pure — no I/O in Handle

### Story 1.4: Party Aggregate — Update Details & Lifecycle Management

As a developer,
I want to update person/organization details and manage party lifecycle (deactivate/reactivate),
So that parties can be maintained throughout their lifecycle with accurate information.

**Acceptance Criteria:**

**Given** an existing person party
**When** an `UpdatePersonDetails` command is handled with new first name and last name
**Then** a `PersonDetailsUpdated` event is emitted with the new details (FR2)
**And** a `PartyDisplayNameDerived` event is emitted with the re-derived display name and sort name (FR6)

**Given** an existing organization party
**When** an `UpdateOrganizationDetails` command is handled with new legal name
**Then** an `OrganizationDetailsUpdated` event is emitted with the new details (FR3)
**And** a `PartyDisplayNameDerived` event is emitted with the re-derived display name (FR6)

**Given** an existing organization party with `IsNaturalPerson = false`
**When** a `SetIsNaturalPerson` command is handled with `true`
**Then** an `IsNaturalPersonChanged` event is emitted

**Given** an active party
**When** a `DeactivateParty` command is handled
**Then** a `PartyDeactivated` event is emitted (FR4)
**And** the party state reflects `IsActive = false`

**Given** a deactivated party
**When** a `ReactivateParty` command is handled
**Then** a `PartyReactivated` event is emitted (FR5)
**And** the party state reflects `IsActive = true`

**Given** an already deactivated party
**When** a `DeactivateParty` command is handled again
**Then** the command is handled idempotently — no duplicate event

**Given** a `CreateParty` command targeting a person party type
**When** an `UpdateOrganizationDetails` command is subsequently handled
**Then** a rejection event is returned (type mismatch)

### Story 1.5: Party Aggregate Tier 1 Unit Tests

Planning note: this quality gate is attached to the related behavior stories and exists to preserve the historical sprint record. Future epics should express comparable unit, integration, accessibility, privacy, and fitness expectations as acceptance criteria or engineering tasks under the behavior story unless the test work creates a reusable independent test harness.

As a developer,
I want comprehensive Tier 1 unit tests for all party aggregate Handle/Apply methods,
So that domain logic correctness is verified without any infrastructure dependencies.

**Acceptance Criteria:**

**Given** the Hexalith.Parties.Server.Tests project
**When** all aggregate tests are implemented
**Then** the following test classes exist:
- `PartyAggregateCreateTests` — person creation, organization creation, idempotent create, rejection scenarios
- `PartyAggregateUpdateTests` — update person details, update organization details, type mismatch rejection, display name re-derivation
- `PartyAggregateLifecycleTests` — deactivate, reactivate, idempotent deactivate, reactivate already active
**And** tests follow naming convention: `{Method}_{Scenario}_{ExpectedResult}`
**And** Shouldly assertions are used for all assertions
**And** `PartyTestData` static class exists in the Testing project with builder methods: `ValidCreatePerson()`, `ValidCreateOrganization()`, etc.
**And** all tests are Tier 1 compliant — zero infrastructure dependencies (no DAPR, no HTTP, no database)
**And** all tests pass with `dotnet test tests/Hexalith.Parties.Server.Tests/`
**And** Apply method correctness is verified — events applied to state produce the expected state mutations

### Story 1.6: REST API, Error Handling & Party Retrieval

As a developer,
I want REST API command endpoints for party operations and a GET endpoint to retrieve parties by ID,
So that I can interact with the party service from any programming language and verify party creation.

**Acceptance Criteria:**

**Given** the Parties service project
**When** the REST API is implemented
**Then** `PartiesController` exists with route `api/v1/parties` (URL-path versioning — FR57)
**And** POST endpoints exist for: CreateParty, UpdatePersonDetails, UpdateOrganizationDetails, SetIsNaturalPerson, DeactivateParty, ReactivateParty
**And** `GET /api/v1/parties/{id}` returns full party details by ID (FR18)
**And** all endpoints require authentication — no anonymous access (NFR13)

**Given** a valid `CreateParty` command sent via POST
**When** the command is processed successfully
**Then** the response is `202 Accepted` with a `CorrelationId`

**Given** a `GET /api/v1/parties/{id}` request for an existing party
**When** the request is processed
**Then** the response is `200 OK` with the full party state as JSON (camelCase, ISO 8601 dates, string enums, null properties omitted)

**Given** a `GET /api/v1/parties/{id}` request for a non-existent party
**When** the request is processed
**Then** the response is `404 Not Found` as ProblemDetails (RFC 9457)

**Given** a command that fails FluentValidation
**When** the command is sent
**Then** the response is `400 Bad Request` as ProblemDetails with machine-readable error `type` URI (FR58)

**Given** a command that is rejected by domain logic
**When** the aggregate returns a rejection `DomainResult`
**Then** the response is `422 Unprocessable Entity` as ProblemDetails with human-readable message and corrective action (FR30)

**Given** a request without a valid tenant JWT claim
**When** the request is processed
**Then** the response is `401 Unauthorized` — fail-closed, never processed with null/default tenant (FR40, FR41)

**Given** a request from Tenant A
**When** the request tries to access Tenant B's party
**Then** the response is `403 Forbidden` (FR39)

**Given** personal data fields in party operations
**When** application logs are written
**Then** `[PersonalData]`-attributed fields are masked or excluded from all log output (FR43, NFR12)

**And** FluentValidation uses assembly scanning (auto-discovery, no explicit registration)
**And** content type is `application/json` for responses, `application/problem+json` for errors

### Story 1.7: AppHost, Local Development & GDPR Warning

As a developer,
I want to run the full party service locally with a single command and see a clear GDPR compliance warning,
So that I can develop and evaluate the service easily while being aware of compliance limitations.

**Acceptance Criteria:**

**Given** a developer with .NET 10 SDK and Docker installed
**When** they run `dotnet aspire run` on the AppHost project
**Then** the following components start:
- Hexalith.Parties with DAPR sidecar
- Redis (state store + pub/sub) for local development
- Aspire dashboard for observability
**And** the service accepts requests within 30 seconds of container launch (NFR5)

**Given** the AppHost project
**When** reviewed for DAPR configuration
**Then** the following DAPR component files exist under `DaprComponents/`:
- `statestore.yaml` (Redis for local dev)
- `pubsub.yaml` (Redis Streams for local dev)
- `subscription-parties.yaml` (event subscription configuration)
**And** `launchSettings.json` exists with appropriate profiles

**Given** the ServiceDefaults project
**When** reviewed for configuration
**Then** OpenTelemetry tracing and structured logging are configured
**And** health check endpoints (`/health`, `/ready`) are registered
**And** resilience patterns are configured

**Given** a running instance of the party service
**When** any API response is returned
**Then** a non-dismissable GDPR compliance warning header is included (FR62)
**And** the warning states that MVP does not include GDPR compliance features and regulated EU personal data should not be stored

**Given** service startup
**When** the service initializes
**Then** the GDPR compliance warning is logged at `Warning` level in startup logs (FR62)

**Given** the full local development setup
**When** a developer follows the sequence: start service → POST CreateParty → GET party by ID
**Then** the round-trip works end-to-end (FR60)
**And** the party is created via the aggregate actor and retrievable via the GET endpoint

---

## Epic 2: Contact Channels & Identifiers

A developer can enrich parties with structured contact channels (postal, email, phone, social) and jurisdiction-specific identifiers (VAT, SIRET, national ID), including marking preferred channels. Adds all contact channel and identifier Handle/Apply implementations to the aggregate and extends the REST API.

### Story 2.1: Party Aggregate — Contact Channel Management

As a developer,
I want to add, update, remove, and mark preferred contact channels on a party,
So that parties have structured, type-discriminated contact information.

**Acceptance Criteria:**

**Given** an existing party (person or organization)
**When** an `AddContactChannel` command is handled with type `Email` and an `EmailAddress` payload
**Then** a `ContactChannelAdded` event is emitted with the channel ID, type, and payload (FR8)
**And** the party state includes the new contact channel in its collection

**Given** an existing party
**When** an `AddContactChannel` command is handled with type `Postal` and a `PostalAddress` payload
**Then** a `ContactChannelAdded` event is emitted with structured postal address data (street, city, postal code, country)

**Given** an existing party
**When** an `AddContactChannel` command is handled with type `Phone` and a `PhoneNumber` payload
**Then** a `ContactChannelAdded` event is emitted with the phone number data

**Given** an existing party
**When** an `AddContactChannel` command is handled with type `Social` and a `SocialMediaHandle` payload
**Then** a `ContactChannelAdded` event is emitted with the social media handle data

**Given** an existing contact channel on a party
**When** an `UpdateContactChannel` command is handled with the channel's ID and updated payload
**Then** a `ContactChannelUpdated` event is emitted with the updated data (FR9)

**Given** an existing contact channel on a party
**When** a `RemoveContactChannel` command is handled with the channel's ID
**Then** a `ContactChannelRemoved` event is emitted (FR10)
**And** the party state no longer includes that channel

**Given** a party with multiple email contact channels
**When** one channel is marked as preferred via the appropriate command
**Then** a `PreferredContactChannelChanged` event is emitted (FR11)
**And** the previously preferred channel of that type (if any) is no longer preferred
**And** the new channel is marked as preferred in the party state

**Given** a party with an existing contact channel
**When** an `AddContactChannel` command is handled with the same channel ID
**Then** the command is handled idempotently — no duplicate event is emitted

**Given** an `UpdateContactChannel` command referencing a non-existent channel ID
**When** the command is handled
**Then** a rejection event is returned with a clear error message

**Given** a `RemoveContactChannel` command referencing a non-existent channel ID
**When** the command is handled
**Then** a rejection event is returned with a clear error message

### Story 2.2: Party Aggregate — Identifier Management

As a developer,
I want to add and remove jurisdiction-specific identifiers on a party,
So that parties carry structured legal and administrative references (VAT, SIRET, national ID).

**Acceptance Criteria:**

**Given** an existing party
**When** an `AddIdentifier` command is handled with identifier type `VAT` and a value
**Then** an `IdentifierAdded` event is emitted with the identifier ID, type, and value (FR12)
**And** the party state includes the new identifier in its collection

**Given** an existing party
**When** an `AddIdentifier` command is handled with identifier type `SIRET` and a value
**Then** an `IdentifierAdded` event is emitted with the SIRET identifier data

**Given** an existing party
**When** an `AddIdentifier` command is handled with identifier type `NationalId` and a value
**Then** an `IdentifierAdded` event is emitted with the national ID data

**Given** an existing identifier on a party
**When** a `RemoveIdentifier` command is handled with the identifier's ID
**Then** an `IdentifierRemoved` event is emitted (FR13)
**And** the party state no longer includes that identifier

**Given** a party with an existing identifier
**When** an `AddIdentifier` command is handled with the same identifier ID
**Then** the command is handled idempotently — no duplicate event

**Given** a `RemoveIdentifier` command referencing a non-existent identifier ID
**When** the command is handled
**Then** a rejection event is returned with a clear error message

### Story 2.3: Contact Channel & Identifier Unit Tests

Planning note: this quality gate is attached to the related behavior stories and exists to preserve the historical sprint record. Future epics should express comparable unit, integration, accessibility, privacy, and fitness expectations as acceptance criteria or engineering tasks under the behavior story unless the test work creates a reusable independent test harness.

As a developer,
I want comprehensive Tier 1 unit tests for all contact channel and identifier aggregate operations,
So that domain logic correctness is verified for the full party enrichment model.

**Acceptance Criteria:**

**Given** the Hexalith.Parties.Server.Tests project
**When** all contact channel and identifier tests are implemented
**Then** the following test classes exist:
- `PartyAggregateContactChannelTests` — add (all 4 types), update, remove, preferred marking, duplicate rejection, invalid ID rejection, idempotent add
- `PartyAggregateIdentifierTests` — add (VAT, SIRET, national ID), remove, duplicate rejection, invalid ID rejection, idempotent add
**And** tests follow naming convention: `{Method}_{Scenario}_{ExpectedResult}`
**And** Shouldly assertions are used
**And** `PartyTestData` is extended with builders: `ValidAddEmailChannel()`, `ValidAddPostalChannel()`, `ValidAddVatIdentifier()`, etc.
**And** all tests are Tier 1 compliant — zero infrastructure dependencies

**Given** a party with 50 contact channels
**When** the aggregate is tested at this size
**Then** all operations (add, update, remove, preferred) work correctly at the aggregate size guideline

**And** all tests pass with `dotnet test tests/Hexalith.Parties.Server.Tests/`

### Story 2.4: REST API — Contact Channel & Identifier Endpoints

As a developer,
I want REST API endpoints for all contact channel and identifier operations,
So that I can manage party contact information and identifiers from any programming language.

**Acceptance Criteria:**

**Given** the PartiesController
**When** contact channel and identifier endpoints are added
**Then** POST endpoints exist for:
- `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`
- `AddIdentifier`, `RemoveIdentifier`
- Preferred channel marking
**And** all endpoints follow the same patterns established in Story 1.6:
- `202 Accepted` on success with CorrelationId
- ProblemDetails (RFC 9457) for errors
- FluentValidation on entry (structural validation)
- Domain rejection → `422` with corrective action
- Authentication required, tenant from JWT

**Given** a valid `AddContactChannel` command with type `Email`
**When** sent via POST to the REST API
**Then** the response is `202 Accepted`
**And** the contact channel is added to the party aggregate

**Given** an `AddContactChannel` command with invalid payload (e.g., malformed email)
**When** sent via POST
**Then** the response is `400 Bad Request` with ProblemDetails describing the validation error

**Given** a `RemoveContactChannel` command referencing a non-existent channel
**When** sent via POST
**Then** the response is `422 Unprocessable Entity` with a domain rejection message

---

## Epic 3: Party Discovery & Search (Read Projections)

Consumers can discover parties through paginated listing, display-name search with match metadata, and filtering by type and date range — all reflecting updates within 2 seconds. Builds the entire read projection infrastructure: PartyDetailProjectionActor, PartyIndexProjectionActor, pure handler extraction (D18), partitioned index state (D5), batch event processing (D16). Query REST endpoints and OpenAPI specification. Email and identifier search are intentionally deferred because the v1.0 index projection does not store those searchable fields.

### Story 3.1: Party Detail Projection Handler & Actor

As a developer,
I want a projection that maintains full party detail read models updated from domain events,
So that consumers can retrieve complete, up-to-date party information by ID without rehydrating the aggregate.

**Acceptance Criteria:**

**Given** a `PartyCreated` event is published
**When** the `PartyDetailProjectionHandler` processes it
**Then** a `PartyDetail` read model is created with party ID, type, details, display name, active status, CreatedAt timestamp

**Given** a `PersonDetailsUpdated` event is published for an existing party
**When** the handler processes it
**Then** the `PartyDetail` read model is updated with the new person details and LastModifiedAt timestamp

**Given** a `ContactChannelAdded` event is published for an existing party
**When** the handler processes it
**Then** the `PartyDetail` read model includes the new contact channel in its collection

**Given** a `ContactChannelUpdated` event is published
**When** the handler processes it
**Then** the corresponding contact channel in the `PartyDetail` is updated

**Given** a `ContactChannelRemoved` event is published
**When** the handler processes it
**Then** the corresponding contact channel is removed from the `PartyDetail`

**Given** an `IdentifierAdded` event is published
**When** the handler processes it
**Then** the `PartyDetail` includes the new identifier

**Given** an `IdentifierRemoved` event is published
**When** the handler processes it
**Then** the corresponding identifier is removed from the `PartyDetail`

**Given** a `PartyDeactivated` event is published
**When** the handler processes it
**Then** the `PartyDetail` reflects `IsActive = false`

**Given** the `PartyDetailProjectionHandler` class
**When** reviewed for architecture compliance
**Then** it has zero DAPR references (D18 — pure handler, Tier 1 testable)
**And** it receives events and returns state mutations

**Given** the `PartyDetailProjectionActor` class
**When** reviewed for architecture compliance
**Then** it is a thin DAPR wrapper that delegates to `PartyDetailProjectionHandler`
**And** its state key follows the pattern `{tenant}:party-detail:{partyId}` (one actor per party — D4)
**And** tenant isolation is enforced at the actor key level

**Given** a domain event published via DAPR pub/sub
**When** the projection actor receives it
**Then** the `PartyDetail` state is updated within the eventual consistency window (< 2 seconds under normal load — FR19, NFR6)

### Story 3.2: Party Index Projection Handler & Actor

As a developer,
I want a projection that maintains lightweight party summaries per tenant for listing and filtering,
So that consumers can browse and filter parties efficiently without loading full detail records.

**Acceptance Criteria:**

**Given** a `PartyCreated` event is published
**When** the `PartyIndexProjectionHandler` processes it
**Then** a `PartyIndexEntry` is added to the tenant index with: party ID, type, display name, active status, CreatedAt, LastModifiedAt

**Given** a `PartyDisplayNameDerived` event is published
**When** the handler processes it
**Then** the corresponding `PartyIndexEntry` display name and sort name are updated

**Given** a `PartyDeactivated` event is published
**When** the handler processes it
**Then** the corresponding `PartyIndexEntry` reflects `IsActive = false` and LastModifiedAt is updated

**Given** a `ContactChannelAdded` event with an email address
**When** the handler processes it
**Then** the `PartyIndexEntry` updates freshness metadata only, including LastModifiedAt (FR68)
**And** no email value is stored in the v1.0 index projection

**Given** an `IdentifierAdded` event
**When** the handler processes it
**Then** the `PartyIndexEntry` updates freshness metadata only, including LastModifiedAt
**And** no identifier value is stored in the v1.0 index projection

**Given** the `PartyIndexProjectionHandler` class
**When** reviewed for architecture compliance
**Then** it has zero DAPR references (D18 — pure handler)

**Given** the `PartyIndexProjectionActor` class
**When** reviewed for architecture compliance
**Then** it is a thin DAPR wrapper delegating to `PartyIndexProjectionHandler`
**And** its state key follows the pattern `{tenant}:party-index:{partitionKey}` (one actor per tenant — D4)
**And** it implements `IIndexPartitionStrategy` abstraction (D5 — single-key v1.0, extensible)

**Given** a burst of 100 concurrent party creation events
**When** the index actor processes them
**Then** events are batch-processed (D16) — state persisted in batches, not after every single event
**And** batch size and time window are configurable via `ProjectionOptions`

**Given** the index actor state
**When** reviewed for data format
**Then** `PartyIndexEntry` includes `CreatedAt` and `LastModifiedAt` fields for date range filtering (FR68)

### Story 3.3: Search, Match Metadata & Query Endpoints

As a consumer,
I want to search parties by display name and receive match metadata in results,
So that I can find the right party quickly and AI agents can perform confident disambiguation.

**Acceptance Criteria:**

**Given** a tenant with multiple parties
**When** a `GET /api/v1/parties` request is made
**Then** a paginated list of `PartyIndexEntry` results is returned (FR14)
**And** pagination parameters (page, pageSize) are supported
**And** filtering by `type` (person/organization) is supported
**And** filtering by `active` status is supported

**Given** a tenant with parties
**When** a `GET /api/v1/parties/search?q=Dupont` request is made
**Then** parties matching "Dupont" by display name are returned (FR15)
**And** each result includes match metadata: matched field `displayName` and match type (exact, prefix, contains) (FR17)

**Given** a search query matching a party by email
**When** the v1.0 display-name search endpoint is used
**Then** no result is returned unless the email text also appears in the display name
**And** `email` match metadata is reserved for the future dedicated search capability

**Given** a search query matching a party by identifier value
**When** the v1.0 display-name search endpoint is used
**Then** no result is returned unless the identifier text also appears in the display name
**And** `identifier` match metadata is reserved for the future dedicated search capability

**Given** a `GET /api/v1/parties?createdAfter=2026-01-01&createdBefore=2026-06-01` request
**When** the request is processed
**Then** only parties created within the date range are returned (FR68)

**Given** a `GET /api/v1/parties?modifiedAfter=2026-01-01` request
**When** the request is processed
**Then** only parties modified after the specified date are returned (FR68)

**Given** the Parties service project
**When** the OpenAPI specification is reviewed
**Then** it is auto-generated from endpoint definitions (FR56)
**And** it conforms to OpenAPI 3.x (NFR25)
**And** Swagger UI is available in development mode for API exploration

**Given** all query endpoints
**When** reviewed for security
**Then** authentication is required on all endpoints (NFR13)
**And** tenant filtering is enforced — results only include the requesting tenant's parties (FR39)

### Story 3.4: Projection Unit & Integration Tests

Planning note: this quality gate is attached to the related behavior stories and exists to preserve the historical sprint record. Future epics should express comparable unit, integration, accessibility, privacy, and fitness expectations as acceptance criteria or engineering tasks under the behavior story unless the test work creates a reusable independent test harness.

As a developer,
I want comprehensive tests for projection handlers and actors,
So that read model correctness, search behavior, and eventual consistency are verified.

**Acceptance Criteria:**

**Given** the Hexalith.Parties.Projections.Tests project
**When** all projection tests are implemented
**Then** the following test classes exist:
- `PartyDetailProjectionHandlerTests` — event sequence: PartyCreated → ContactChannelAdded → ContactChannelUpdated → IdentifierAdded → PartyDeactivated; verify state at each step
- `PartyIndexProjectionHandlerTests` — entry creation, display name updates, contact/identifier event freshness updates, deactivation, date field updates
**And** all handler tests are Tier 1 compliant — zero DAPR references

**Given** a multi-event sequence (PartyCreated → ContactChannelAdded × 3 → IdentifierAdded × 2)
**When** processed by the detail projection handler
**Then** the resulting `PartyDetail` contains all 3 contact channels and 2 identifiers with correct data

**Given** a search scenario with 5 parties (3 persons, 2 organizations)
**When** search tests execute queries
**Then** match metadata correctly identifies the matched display-name field and match type
**And** type filtering returns only the requested party type
**And** active status filtering works correctly
**And** date range filtering returns correct results

**Given** tenant isolation test with parties from Tenant A and Tenant B
**When** Tenant A queries parties
**Then** zero Tenant B parties appear in results (FR39, NFR9)

**Given** the Parties.Tests project
**When** API integration tests are implemented
**Then** query endpoint tests verify pagination, filtering, search, and match metadata through the REST API layer

**And** all tests pass with `dotnet test`

### Post-Epic 3 Corrective Backlog

The Epic 3 retrospective found that implementation and architecture align on display-name-only v1.0 search, while older planning language implied email and identifier search. The corrected scope is:

- v1.0 index projection stores lightweight party summaries and supports display-name search only.
- Detail projection stores contact channels and identifiers, but these fields are not searchable in the v1.0 index.
- `email` and `identifier` match metadata values are reserved for the future dedicated search capability.

Carry-forward action items:

- Add executable no-PII logging regression coverage for command and query paths. Success criteria: tests fail if `[PersonalData]` values from party details, contact channels, identifiers, or display names are written to application logs.
- Add composite-to-projection regression coverage. Success criteria: composite command event sequences are replayed through `PartyDetailProjectionHandler` and `PartyIndexProjectionHandler`, producing the expected detail and index state without adding email or identifier search fields.
- Use an actor runtime readiness checklist for future projection stories. Success criteria: story review explicitly covers actor interfaces, DI registration, option binding, actor ID validation, state keys, flush behavior, and query consistency.
- Keep projection lifecycle work explicit in operational readiness. Success criteria: D14 projection rebuild and D15 degradation behavior remain represented by Story 8.3 scope and are not treated as implicit assumptions.

---

## Epic 4: Composite Commands & Advanced Aggregate Logic

The party aggregate supports atomic composite operations — creating a full party with channels and identifiers in one command (CreatePartyComposite), and updating via explicit add/update/remove lists (UpdatePartyComposite) — with sub-operation idempotency, conflict detection, payload size limits, and complete updated state returned in responses. This isolates the most complex domain logic (D8, D9, D10, D12, D17) with the upfront test matrix (D19).

### Story 4.1: CreatePartyComposite Aggregate Handler

As a developer,
I want to create a complete party with contact channels and identifiers in a single atomic command,
So that AI agents and API consumers can create fully-enriched parties without multiple sequential commands.

**Acceptance Criteria:**

**Given** no party exists with the specified ID
**When** a `CreatePartyComposite` command is handled with person details, 2 email channels, and 1 VAT identifier
**Then** the following events are emitted atomically in a single actor turn (D8):
- `PartyCreated` with person details
- `PartyDisplayNameDerived` with derived display name
- `ContactChannelAdded` × 2 (one per email channel)
- `IdentifierAdded` × 1 (VAT)
**And** a `CompositeCommandResult` is returned with all sub-operations in the `Applied` collection (FR21)

**Given** a `CreatePartyComposite` command with organization details, 3 contact channels (postal, email, phone), and 2 identifiers (VAT, SIRET)
**When** the command is handled
**Then** all 7 events are emitted atomically (PartyCreated + DisplayName + 3 channels + 2 identifiers)
**And** the `CompositeCommandResult.Applied` collection contains 7 entries

**Given** a `CreatePartyComposite` command with duplicate contact channel IDs in the payload
**When** the command is handled
**Then** duplicate additions are skipped — not rejected (D10 — essential for MCP retry safety)
**And** the `CompositeCommandResult.Skipped` collection contains the duplicate entries
**And** non-duplicate entries are still applied

**Given** a `CreatePartyComposite` command with no party type specified
**When** the command is handled
**Then** the entire composite is rejected — no events emitted (D12 — all-or-nothing)
**And** the `CompositeCommandResult` indicates rejection with specific error details

**Given** a `CreatePartyComposite` command with more than 100 sub-operations
**When** the command is handled
**Then** the command is rejected before processing with a "payload size exceeded" error (D17)
**And** the limit is configurable per deployment

**Given** a `CreatePartyComposite` command with only party details (no channels, no identifiers)
**When** the command is handled
**Then** only `PartyCreated` and `PartyDisplayNameDerived` events are emitted
**And** empty channel and identifier lists are accepted gracefully

**Given** a party already exists with the specified ID
**When** a `CreatePartyComposite` command is handled with the same ID
**Then** the command is handled idempotently — no duplicate events emitted

**Given** the `Handle(CreatePartyComposite)` method
**When** reviewed for implementation
**Then** it is synchronous (returns `CompositeCommandResult`, not `Task<>`)
**And** domain logic is pure — no I/O

### Story 4.2: UpdatePartyComposite Aggregate Handler

As a developer,
I want to update a party's details, channels, and identifiers in a single atomic command with explicit add/update/remove lists,
So that AI agents can make complex party modifications without multiple sequential commands.

**Acceptance Criteria:**

**Given** an existing person party with 2 email channels and 1 VAT identifier
**When** an `UpdatePartyComposite` command is handled with:
- `PersonDetails` present (replace person details)
- `AddContactChannels` with 1 new phone channel
- `UpdateContactChannels` with 1 existing email channel updated
- `RemoveContactChannelIds` with 1 existing email channel removed
- `AddIdentifiers` with 1 SIRET identifier
**Then** the following events are emitted atomically:
- `PersonDetailsUpdated`
- `PartyDisplayNameDerived` (re-derived)
- `ContactChannelAdded` × 1
- `ContactChannelUpdated` × 1
- `ContactChannelRemoved` × 1
- `IdentifierAdded` × 1
**And** a `CompositeCommandResult` is returned with all in `Applied` (FR22)
**And** the result includes the complete updated party state (FR69)

**Given** an `UpdatePartyComposite` command with `PersonDetails` absent (null)
**When** the command is handled
**Then** person details remain unchanged — absent means "no change" (D9)

**Given** an `UpdatePartyComposite` command with `AddContactChannels` containing a channel ID that already exists
**When** the command is handled
**Then** the duplicate addition is skipped (D10 — idempotency)
**And** it appears in `CompositeCommandResult.Skipped`

**Given** an `UpdatePartyComposite` command with `UpdateContactChannels` referencing a non-existent channel ID
**When** the command is handled
**Then** the entire composite is rejected with a specific error: "channel ID not found" (D10 — reject invalid IDs)

**Given** an `UpdatePartyComposite` command with `RemoveContactChannelIds` referencing a non-existent channel ID
**When** the command is handled
**Then** the entire composite is rejected with a specific error: "channel ID not found" (D10)

**Given** an `UpdatePartyComposite` command where the same channel ID appears in both `AddContactChannels` and `RemoveContactChannelIds`
**When** the command is handled
**Then** the entire composite is rejected with error: "conflicting operations on same channel ID" (D10)

**Given** an `UpdatePartyComposite` command where the same identifier ID appears in both `AddIdentifiers` and `RemoveIdentifierIds`
**When** the command is handled
**Then** the entire composite is rejected with error: "conflicting operations on same identifier ID" (D10)

**Given** an `UpdatePartyComposite` command with all lists empty and no details present
**When** the command is handled
**Then** a no-op result is returned — no events emitted, no error

**Given** an `UpdatePartyComposite` command with more than 100 total sub-operations
**When** the command is handled
**Then** the command is rejected before processing with "payload size exceeded" error (D17)

**Given** an `UpdatePartyComposite` command targeting a non-existent party
**When** the command is handled
**Then** a rejection is returned with "party not found"

### Story 4.3: Composite Command Unit Tests

Planning note: this quality gate is attached to the related behavior stories and exists to preserve the historical sprint record. Future epics should express comparable unit, integration, accessibility, privacy, and fitness expectations as acceptance criteria or engineering tasks under the behavior story unless the test work creates a reusable independent test harness.

As a developer,
I want a comprehensive test matrix for both composite aggregate handlers,
So that the most complex domain logic is thoroughly verified before any consumer uses it.

**Acceptance Criteria:**

**Given** the Hexalith.Parties.Server.Tests project
**When** the composite command test matrix (D19) is implemented
**Then** the following test categories exist for `CreatePartyComposite`:
- Happy path: person with channels and identifiers
- Happy path: organization with channels and identifiers
- Happy path: party only (no channels, no identifiers)
- Idempotent create (party already exists)
- Duplicate channel IDs in payload → skipped
- Missing party type → full rejection
- Payload size limit exceeded → rejection
- Maximum channels (50) in single create

**And** the following test categories exist for `UpdatePartyComposite`:
- Update person details only
- Update organization details only
- Add channels only
- Update channels only
- Remove channels only
- Add identifiers only
- Remove identifiers only
- Mixed: details + add + update + remove channels + add + remove identifiers
- Absent details → no change
- Duplicate add → skipped
- Invalid update channel ID → rejection
- Invalid remove channel ID → rejection
- Conflicting operations (same ID in add + remove) → rejection
- No-op (all lists empty, no details) → no events
- Non-existent party → rejection
- Payload size limit exceeded → rejection

**And** tests verify `CompositeCommandResult` structure:
- `Applied` collection contains correctly applied sub-operations
- `Skipped` collection contains idempotently skipped sub-operations
- Rejection contains specific error details

**And** all tests follow naming convention: `{Method}_{Scenario}_{ExpectedResult}`
**And** Shouldly assertions are used
**And** `PartyTestData` extended with: `ValidCreatePersonComposite()`, `ValidUpdateComposite()`, etc.
**And** all tests are Tier 1 compliant
**And** estimated 20-30 test cases covering the full combinatorial matrix

### Story 4.4: Composite Command REST & Validation

As a developer,
I want REST endpoints for composite commands with structural validation,
So that API consumers can create and update full parties in single HTTP requests.

**Acceptance Criteria:**

**Given** the PartiesController
**When** composite endpoints are added
**Then** a POST endpoint exists for `CreatePartyComposite`
**And** a POST endpoint exists for `UpdatePartyComposite`

**Given** a valid `CreatePartyComposite` request
**When** sent via POST
**Then** the response is `202 Accepted` with CorrelationId
**And** the response body includes the `CompositeCommandResult` showing applied/skipped sub-operations

**Given** a valid `UpdatePartyComposite` request
**When** sent via POST
**Then** the response is `202 Accepted`
**And** the response body includes the complete updated party state (FR69)

**Given** the `CreatePartyCompositeValidator` (FluentValidation)
**When** reviewed
**Then** it validates:
- Party type is required
- Total sub-operation count does not exceed the configurable maximum (D17)
- Channel IDs are valid UUIDs
- Required payload fields per channel type are present
**And** it does NOT duplicate domain validation (e.g., duplicate detection is domain logic in Handle)

**Given** the `UpdatePartyCompositeValidator` (FluentValidation)
**When** reviewed
**Then** it validates:
- Party ID is required
- Total sub-operation count does not exceed the configurable maximum
- Channel/identifier IDs in update/remove lists are valid UUIDs
**And** it does NOT check for conflicting operations (that is domain logic in Handle)

**Given** a composite command that fails FluentValidation
**When** sent via POST
**Then** the response is `400 Bad Request` as ProblemDetails

**Given** a composite command rejected by domain logic (e.g., conflicting operations)
**When** the aggregate returns a rejection
**Then** the response is `422 Unprocessable Entity` as ProblemDetails with specific error details

---

## Epic 5: AI Agent Party Management (MCP Server)

AI agents can perform complete party management through 5 MCP tools (create_party, find_parties, get_party, update_party, delete_party) — with forgiving input schemas, complete response payloads, patch semantics, and disambiguation support via match metadata. Focuses purely on the MCP translation layer (D11) with architectural fitness enforcement (zero domain event type references).

### Story 5.1: MCP Server Setup & get_party / find_parties Tools

As an AI agent,
I want to search for and retrieve party details through dedicated MCP tools,
So that I can perform identity resolution and access structured party information.

**Acceptance Criteria:**

**Given** the Parties service project
**When** the MCP server is configured
**Then** MCP tools are registered via `AddMcpTools()` extension method with assembly scanning
**And** the MCP server implements the MCP protocol specification (NFR26)
**And** the MCP server shares the same authentication pipeline as the REST API
**And** tenant context is extracted identically to REST (FR39)

**Given** an AI agent calling `get_party` with a valid party ID
**When** the tool executes
**Then** the complete `PartyDetail` is returned (all details, contact channels, identifiers, active status)
**And** the response shape matches what REST `GET /api/v1/parties/{id}` returns

**Given** an AI agent calling `get_party` with a non-existent party ID
**When** the tool executes
**Then** a clear error message is returned: "party not found"

**Given** an AI agent calling `find_parties` with query "Dupont"
**When** the tool executes
**Then** matching `PartyIndexEntry[]` results are returned (FR20)
**And** each result includes display-name match metadata: matched field and match type (FR17)
**And** results are sufficient for the AI agent to rank simple name-based candidates

**Given** an AI agent calling `find_parties` with query "Dupont Acme"
**When** the tool executes
**Then** results include parties whose display names match the query terms, with match metadata indicating the display-name match
**And** email, identifier, or organization-based resolution requires retrieving candidate party details or the future dedicated search capability

**Given** an AI agent calling `find_parties` with no query (list mode)
**When** the tool executes
**Then** a paginated list of parties is returned (FR23)
**And** optional filters (type, active status) are supported

**Given** the `GetPartyMcpTool` and `FindPartiesMcpTool` classes
**When** reviewed for naming conventions
**Then** class names follow `{ToolName}McpTool` pattern
**And** MCP protocol names are snake_case: `get_party`, `find_parties`

**Given** all MCP tool calls
**When** measured for latency
**Then** each completes in < 1 second end-to-end including transport (NFR1)

### Story 5.2: create_party MCP Tool

As an AI agent,
I want to create a complete party from a single natural-language-extracted input,
So that I can turn "Jean Dupont at Acme Corp, email jean@acme.com" into a structured party record in one tool call.

**Acceptance Criteria:**

**Given** an AI agent calling `create_party` with full input: type "person", first name "Jean", last name "Dupont", email "jean@acme.com"
**When** the tool executes
**Then** the translation layer constructs a `CreatePartyComposite` command with person details and one email contact channel
**And** the complete created `PartyDetail` is returned in the response — not just the ID (FR24)

**Given** an AI agent calling `create_party` with partial input: type "person", last name "Bernard", email "m.bernard@newcorp.fr"
**When** the tool executes
**Then** the tool accepts the partial input gracefully (FR25)
**And** omitted optional fields (first name, date of birth, prefix, suffix) use documented default behaviors (empty/null)
**And** the party is created successfully with available information
**And** the complete `PartyDetail` is returned

**Given** an AI agent calling `create_party` with type "organization", legal name "Acme Corp", VAT "FR12345678901"
**When** the tool executes
**Then** the translation layer constructs a `CreatePartyComposite` with organization details and one VAT identifier
**And** the complete created party is returned

**Given** an AI agent calling `create_party` with missing required fields (e.g., no party type)
**When** the tool executes
**Then** a clear validation error message is returned stating what's needed (FR25)
**And** the error is actionable — the AI agent understands what to fix

**Given** an AI agent calling `create_party` with a contact channel but no explicit channel ID
**When** the tool executes
**Then** the translation layer generates a UUID for the channel ID (forgiving input normalization)

**Given** the `CreatePartyMcpTool` class
**When** reviewed for architecture compliance (D11)
**Then** it contains input normalization (forgiving-to-strict conversion) and response assembly
**And** it does NOT contain business rules, domain validation, or state caching
**And** it references only command types and query result types — zero event type references

### Story 5.3: update_party & delete_party MCP Tools

As an AI agent,
I want to update party details and deactivate parties through MCP tools with patch semantics,
So that I can make targeted modifications without sending full party state.

**Acceptance Criteria:**

**Given** an AI agent calling `update_party` with party ID and only a new email address to add
**When** the tool executes
**Then** the translation layer constructs an `UpdatePartyComposite` command with only `AddContactChannels` populated
**And** all other fields (PersonDetails, RemoveContactChannelIds, etc.) remain absent — patch semantics (FR74)
**And** the complete updated `PartyDetail` is returned (FR69)

**Given** an AI agent calling `update_party` with party ID, updated first name, and a channel to remove
**When** the tool executes
**Then** the translation layer constructs an `UpdatePartyComposite` with `PersonDetails` present and `RemoveContactChannelIds` populated
**And** only specified fields are modified; unspecified fields remain unchanged

**Given** an AI agent calling `update_party` with a channel to add but no explicit channel ID
**When** the tool executes
**Then** the translation layer generates a UUID for the channel ID (forgiving input)

**Given** an AI agent calling `update_party` targeting a non-existent party
**When** the tool executes
**Then** a clear error message is returned: "party not found"

**Given** an AI agent calling `update_party` with an invalid channel ID in the remove list
**When** the tool executes
**Then** the error from the aggregate rejection is translated into a clear MCP error message

**Given** an AI agent calling `delete_party` with a party ID
**When** the tool executes
**Then** the translation layer maps this to a `DeactivateParty` command (soft delete — FR4)
**And** a confirmation response is returned

**Given** an AI agent calling `delete_party` for an already deactivated party
**When** the tool executes
**Then** the operation is handled idempotently — no error

**Given** the `UpdatePartyMcpTool` and `DeletePartyMcpTool` classes
**When** reviewed for architecture compliance (D11)
**Then** they contain input normalization and command construction only
**And** zero references to domain event types
**And** zero business rules or domain validation logic

### Story 5.4: MCP Tools Tests & Architectural Fitness

Planning note: this quality gate is attached to the related behavior stories and exists to preserve the historical sprint record. Future epics should express comparable unit, integration, accessibility, privacy, and fitness expectations as acceptance criteria or engineering tasks under the behavior story unless the test work creates a reusable independent test harness.

As a developer,
I want comprehensive tests for all MCP tools and a CI-enforced architectural fitness test,
So that MCP tool behavior is verified and the translation layer boundary is machine-enforced.

**Acceptance Criteria:**

**Given** the Hexalith.Parties.Tests project
**When** MCP tool tests are implemented
**Then** the following test classes exist under `Mcp/`:
- `CreatePartyMcpToolTests` — full input, partial input, missing required fields, generated channel IDs, complete response verification
- `FindPartiesMcpToolTests` — search query, empty query (list mode), match metadata presence, pagination
- `GetPartyMcpToolTests` — existing party, non-existent party, response shape
- `UpdatePartyMcpToolTests` — patch semantics (only specified fields), add channel only, remove channel only, mixed operations, generated IDs, non-existent party error
- `DeletePartyMcpToolTests` — active party, already deactivated (idempotent), non-existent party

**Given** the forgiving input normalization logic
**When** tested
**Then** the following scenarios are covered:
- Missing optional fields default to sensible values
- Missing channel/identifier IDs are auto-generated as UUIDs
- Partial person details (last name only, no first name) are accepted
- Clear validation errors for missing required fields (party type)

**Given** the `FitnessTests/ArchitecturalFitnessTests.cs` file
**When** the MCP boundary test is implemented
**Then** it verifies via reflection or compilation test that the `Parties/Mcp/` namespace:
- Has zero references to any type implementing `IEventPayload` or `IRejectionEvent`
- References only command types (from Contracts/Commands/) and model types (from Contracts/Models/)
**And** this test runs in CI and fails the build on violation (D11)

**Given** the architectural fitness tests
**When** all boundary tests are reviewed
**Then** the following additional boundaries are verified:
- Projection handlers have zero DAPR references
- Contracts project has zero runtime dependencies beyond netstandard2.1
- Client project has no references to Server, Projections, or Parties service
**And** all fitness tests are in `Parties.Tests/FitnessTests/`

**And** all tests pass with `dotnet test`

---

## Epic 6: Developer Integration & Documentation

A .NET developer integrates party management with a single NuGet package and one-line DI registration (AddPartiesClient()). A non-.NET developer follows the getting-started guide and sends their first command in under 30 minutes. A sample integration project demonstrates command, query, event subscription, and MCP usage patterns.

### Story 6.1: Client Package — Command & Query Abstractions

As a .NET developer,
I want to integrate party management via a single NuGet package with one-line DI registration,
So that I can send commands and query parties without knowing anything about DAPR, MediatR, or the service's infrastructure.

**Acceptance Criteria:**

**Given** the Hexalith.Parties.Client project
**When** the client abstractions are implemented
**Then** `IPartiesCommandClient` interface exists with methods for all party commands:
- `CreatePartyAsync`, `UpdatePersonDetailsAsync`, `UpdateOrganizationDetailsAsync`
- `AddContactChannelAsync`, `UpdateContactChannelAsync`, `RemoveContactChannelAsync`
- `AddIdentifierAsync`, `RemoveIdentifierAsync`
- `DeactivatePartyAsync`, `ReactivatePartyAsync`
- `CreatePartyCompositeAsync`, `UpdatePartyCompositeAsync`
**And** `IPartiesQueryClient` interface exists with methods:
- `GetPartyAsync(string partyId)` returning `PartyDetail`
- `ListPartiesAsync(...)` returning paginated `PartyIndexEntry[]`
- `SearchPartiesAsync(string query)` returning `PartyIndexEntry[]` with match metadata

**Given** the client project
**When** HTTP-based implementations are created
**Then** they communicate with the Parties REST API via standard `HttpClient`
**And** they handle serialization (camelCase JSON, ISO 8601 dates, string enums)
**And** they translate ProblemDetails error responses into typed exceptions or result objects

**Given** a consuming application's `Program.cs` or `Startup.cs`
**When** the developer adds `builder.Services.AddPartiesClient(configuration)`
**Then** `IPartiesCommandClient` and `IPartiesQueryClient` are registered in DI (FR26)
**And** the single line is all that's needed — no additional configuration required for basic usage

**Given** the Client project's `.csproj`
**When** reviewed for dependencies
**Then** it references only `Hexalith.Parties.Contracts` and HTTP abstractions
**And** it has zero references to DAPR, MediatR, FluentValidation, or any server-side infrastructure
**And** total transitive dependencies are < 10, package size < 5MB (NFR28, NFR31)

**Given** the Client project
**When** reviewed for architectural boundaries
**Then** it has no references to Server, Projections, or Parties service projects

**Given** a developer using the client abstractions
**When** they send a `CreateParty` command via `IPartiesCommandClient`
**Then** they receive a typed result without needing infrastructure knowledge (FR27)

**Given** a developer using the client abstractions
**When** they query parties via `IPartiesQueryClient`
**Then** they receive typed `PartyDetail` or `PartyIndexEntry[]` results without infrastructure knowledge (FR28)

### Story 6.2: Getting-Started Guide & README

As a developer evaluating Hexalith.Parties,
I want clear documentation that enables self-service onboarding,
So that I can deploy and send my first command without needing help from the core team.

**Acceptance Criteria:**

**Given** the repository README
**When** a developer reads the first paragraph
**Then** they can explain what Hexalith.Parties does in one sentence (success criteria: one-sentence clarity test)
**And** the value proposition is clear: "integrate, don't rebuild"

**Given** the README
**When** reviewed for content
**Then** it includes:
- Clear positioning (party management microservice, not auth, not CRM)
- Key features summary (event-sourced, MCP, NuGet, multi-tenant)
- GDPR disclaimer prominently placed
- Link to getting-started guide
- Link to architecture overview

**Given** the getting-started guide at `/docs/getting-started.md`
**When** a developer follows it
**Then** it follows this narrative arc (FR32):
1. Prerequisites (Docker, .NET 10 SDK) — explicit list, nothing assumed
2. Deploy: clone, `dotnet aspire run`, verify dashboard — target < 15 minutes (NFR30)
3. First command: POST `CreateParty` via REST (curl/Postman example) — target < 30 minutes
4. First query: GET party by ID, search by name — verify round-trip
5. MCP server: configure AI assistant, first `create_party` tool call
6. NuGet integration: add `Hexalith.Parties.Client`, `AddPartiesClient()`, send command from code
**And** each step includes exact commands to copy-paste
**And** a non-.NET developer path exists: Docker deploy + REST API only (no NuGet)

**Given** the getting-started guide
**When** reviewed for completeness
**Then** the GDPR disclaimer is present (FR62 reference)
**And** the emergency manual erasure procedure is referenced (for pre-v1.1 erasure requests)

### Story 6.3: Sample Integration Project

As a developer,
I want a runnable sample project demonstrating all integration patterns,
So that I have working reference code for commands, queries, event subscriptions, and MCP usage.

**Acceptance Criteria:**

**Given** the `/samples/Hexalith.Parties.Sample/` project
**When** reviewed for content
**Then** it demonstrates the following patterns (FR59):
1. `AddPartiesClient(configuration)` — one-line DI registration
2. Send commands: create a person party, add an email contact channel, add a VAT identifier
3. Query parties: get by ID, search by name, list with pagination
4. Subscribe to party events via DAPR pub/sub and build a simple local read model (e.g., `CustomerSummary`)
5. Handle `PartyDeactivated` event to update the local read model
6. MCP server configuration example (commented or documented)

**Given** the sample project
**When** a developer runs `dotnet run` (with the Parties service already running)
**Then** it executes successfully end-to-end
**And** console output shows each step completing (party created, queried, event received)

**Given** the sample project
**When** reviewed for CI integration
**Then** it can be built as part of the solution (`dotnet build Hexalith.Parties.slnx`)
**And** it does not break the CI pipeline (runnable but not requiring full infrastructure in CI)

**Given** the sample's event subscription handler
**When** a `PartyDeactivated` event is received
**Then** the local read model is updated accordingly
**And** the pattern demonstrates idempotent event handling

**Given** a developer reading the sample code
**When** they review the event subscription section
**Then** comments reference the dangling reference guidance for `PartyErased` (future v1.1)
**And** the `PartyMerged` forward-compat event is mentioned as a future subscription target

---

## Epic 7: Event-Driven Integration & Subscriber Experience

Consuming applications can subscribe to party domain events and build domain-specific read models, with forward-compatible contracts (including PartyMerged placeholder), documented handler patterns for erasure and dangling references, at-least-once delivery verification, tenant context in events, and causal ordering documentation. Event publishing infrastructure (DAPR pub/sub) is configured in Epic 1; this epic focuses on subscriber experience, documentation, and the sample event subscription demo.

### Story 7.1: Event Publishing Verification & Configuration

As an event subscriber developer,
I want party domain events reliably published via DAPR pub/sub with tenant context,
So that my consuming application receives structured, routable events on every party state change.

**Acceptance Criteria:**

**Given** any party command that produces domain events (create, update, deactivate, etc.)
**When** the command is processed successfully
**Then** all resulting events are published to DAPR pub/sub (FR34)
**And** events are wrapped in CloudEvents 1.0 envelope (inherited from EventStore)

**Given** a published party event
**When** its envelope is inspected
**Then** it includes tenant context for consuming application routing decisions (FR70)
**And** the topic follows the pattern `{tenant}.parties.events`

**Given** a published event that fails delivery
**When** DAPR retry policy is exhausted
**Then** the event is routed to the dead letter topic: `deadletter.{tenant}.parties.events`
**And** no events are lost — persist-then-publish with drain recovery on publish failure (EventStore)

**Given** the DAPR pub/sub configuration
**When** reviewed for event publishing
**Then** the subscription configuration file (`subscription-parties.yaml`) correctly routes party events
**And** the pub/sub component is configured for the deployment target (Redis Streams for local dev)

**Given** a party creation followed by contact channel addition
**When** events are published
**Then** both `PartyCreated` and `ContactChannelAdded` events are published in sequence
**And** event payload JSON uses camelCase, ISO 8601 dates, string enums (consistent with API conventions)

**Given** multiple tenants performing party operations simultaneously
**When** events are published
**Then** each tenant's events are routed to their tenant-scoped topic
**And** no cross-tenant event leakage occurs

### Story 7.2: Subscriber Experience & At-Least-Once Delivery

As an event subscriber developer,
I want verified at-least-once delivery with clear ordering guarantees and idempotent handler patterns,
So that my consuming application can build reliable read models from party events.

**Acceptance Criteria:**

**Given** a consuming application subscribed to party events
**When** events are published
**Then** at-least-once delivery is guaranteed via DAPR pub/sub (FR63, NFR23)
**And** consuming applications must implement idempotent handlers (duplicate events possible)

**Given** the event subscription documentation
**When** reviewed for ordering guarantees
**Then** causal ordering guarantees per aggregate are documented per broker (FR73):
- Redis Streams: yes (within a consumer group)
- RabbitMQ: yes (per queue)
- Kafka: yes (per partition, with key-based routing configured)
**And** required broker configuration for ordering is documented
**And** handler design requirements are specified if ordering cannot be guaranteed (sequence-checking, order-tolerant projection updates)

**Given** a consuming application building a local read model
**When** it subscribes to party events
**Then** it can selectively handle events (e.g., only `PersonDetailsUpdated`, not all events) (FR35)
**And** it can build domain-specific projections (e.g., customer summary from party data)

**Given** the `PartyMerged` event type in Contracts
**When** a consuming application encounters it in the event stream
**Then** tolerant deserialization handles it gracefully even before v2 implementation (FR37)
**And** consuming applications can register handlers for it proactively

**Given** a consuming application's event handler
**When** it receives an unknown event type (future additive events)
**Then** tolerant deserialization ignores unknown fields and handles missing optional fields
**And** the handler continues processing without error (NFR27)

**Given** an integration test with a subscriber
**When** 10 party events are published in sequence for the same aggregate
**Then** the subscriber receives all 10 events
**And** delivery is confirmed (no lost events)

### Story 7.3: Handler Patterns Documentation & Dangling Reference Guidance

As an event subscriber developer,
I want clear documentation with handler patterns for all event types including erasure,
So that I know exactly how to build compliant event handlers — especially for the mandatory PartyErased subscription.

**Acceptance Criteria:**

**Given** the Contracts package documentation (in `/docs/` or package README)
**When** reviewed for handler patterns
**Then** the following handler pattern documentation exists (FR38):

1. **Event handler patterns per event type:**
   - `PartyCreated` — when to create local records vs. ignore
   - `PersonDetailsUpdated` / `OrganizationDetailsUpdated` — update local display names
   - `ContactChannelAdded/Updated/Removed` — keep local contact caches in sync
   - `PartyDeactivated` / `PartyReactivated` — flag local records for review
   - `PartyErased` (v1.1) — **MANDATORY** handler pattern with explicit warning

2. **PartyErased handler pattern (explicit):**
   - Find all local records referencing the erased `partyId`
   - Nullify the party reference
   - Replace display names with "[Erased Party]"
   - Preserve records with independent legal retention requirements
   - Log the erasure handling for audit trail

3. **Dangling reference guidance:**
   - What happens when a referenced party is erased
   - How to detect and clean up dangling references
   - Strategies for foreign key management with party IDs

**Given** the handler patterns documentation
**When** reviewed for the `PartyErased` section
**Then** an explicit warning states: "PartyErased subscription is mandatory for ALL consuming apps regardless of which other events they handle" (FR38)
**And** code examples show a complete handler implementation

**Given** the documentation
**When** reviewed for tolerant deserialization guidance
**Then** it explains:
- How to handle unknown fields (ignore)
- How to handle missing optional fields (documented defaults)
- How to prepare for future event types (additive evolution)
**And** code examples demonstrate the tolerant reader pattern

**Given** the documentation
**When** reviewed for completeness
**Then** it references the sample integration project (Epic 6, Story 6.3) as working reference code
**And** it links to DAPR pub/sub configuration requirements per broker

---

## Epic 8: Operational Readiness & Production Hardening

Operators can deploy with confidence using deployment validation tooling (DAPR security config verification), monitor health and readiness signals, and trust that the service degrades gracefully when infrastructure components fail. Includes projection health monitoring with auto-rebuild on corruption (D15) and projection rebuild admin endpoint (D14).

### Story 8.1: Deployment Validation Tooling

As an operator,
I want a deployment validation tool that verifies security configuration before production use,
So that I can be confident DAPR access controls, tenant isolation, and pub/sub policies are correctly configured.

**Acceptance Criteria:**

**Given** a deployment validation script or tool
**When** executed against a target deployment
**Then** it verifies the following DAPR security configurations (FR61):
- Access control policies are defined and restrict cross-tenant pub/sub access
- State store access is scoped per tenant namespace
- Pub/sub topic access control prevents unauthorized subscription
- Secret store access is configured (preparation for v1.1 key management)

**Given** the validation tool
**When** a misconfiguration is detected
**Then** the specific misconfiguration is reported with:
- What is wrong
- What the correct configuration should be
- A reference to the security config checklist

**Given** the validation tool
**When** all checks pass
**Then** a "deployment validated" confirmation is output
**And** the validation results can be logged for audit purposes

**Given** the `/deploy/` directory
**When** reviewed for deployment artifacts
**Then** production DAPR component configurations exist for:
- `pubsub-kafka.yaml`, `pubsub-rabbitmq.yaml`, `pubsub-servicebus.yaml`
- `statestore-cosmosdb.yaml`, `statestore-postgresql.yaml`
- `accesscontrol.yaml`, `resiliency.yaml`
- `subscription-parties.yaml`
**And** a security config checklist document exists with operator responsibilities

**Given** the deployment documentation
**When** reviewed for operator guidance
**Then** it covers:
- Required DAPR component configuration per deployment target
- Minimum state store requirements (entry size limits for index actor — D5)
- Backup strategy guidance that accounts for crypto-shredding (v1.1 preparation)
- Network security and infrastructure IAM responsibilities (operator scope)

### Story 8.2: Health, Readiness & Graceful Degradation

As an operator,
I want health and readiness signals and graceful degradation under infrastructure failure,
So that I can monitor the service in production and trust that partial failures don't cause total outage.

**Acceptance Criteria:**

**Given** the running party service
**When** a health check request is made to `/health`
**Then** a component-level health status is returned (FR71):
- DAPR sidecar connectivity
- State store accessibility
- Pub/sub connectivity
- Projection actor responsiveness

**Given** the running party service
**When** a readiness check request is made to `/ready`
**Then** the service reports whether it is ready to accept requests (FR71)
**And** readiness is false during startup until the service can process commands

**Given** the DAPR state store becomes unavailable
**When** write commands are sent
**Then** commands fail gracefully with a clear error (not an unhandled exception) (FR64)
**And** read operations from projection actors continue serving cached/last-known state
**And** a staleness indicator is included in read responses (NFR21)

**Given** the DAPR pub/sub becomes unavailable
**When** commands are processed successfully
**Then** events are committed to the event store but not published (FR64)
**And** events are retried on recovery via EventStore persist-then-publish pattern
**And** consuming apps may experience delayed event delivery but never event loss

**Given** the DAPR sidecar becomes unavailable
**When** any request is sent
**Then** the service reports unhealthy via health check
**And** readiness reports false
**And** the failure is logged at `Error` level

**Given** the service recovers from a crash
**When** it restarts
**Then** it replays necessary event state and accepts requests within 30 seconds (NFR20)
**And** no data loss occurs — event store is the durable source of truth (NFR22)

**Given** component failure scenarios
**When** documented for operators
**Then** documented behavior exists for each failure mode:
- State store unavailable: writes fail, reads may serve stale data
- Pub/sub unavailable: events committed but not published, retry on recovery
- Sidecar unavailable: full degradation, unhealthy status
**And** operational runbooks reference these failure modes

### Story 8.3: Projection Health Monitoring & Rebuild

As an operator,
I want projections that self-heal on corruption and a manual rebuild capability,
So that read model issues are resolved automatically or with minimal operator intervention.

**Acceptance Criteria:**

**Given** a projection actor (detail or index) activating with corrupted state
**When** deserialization fails on actor activation
**Then** the actor catches the deserialization failure (D15)
**And** a corruption alert is logged at `Error` level with the affected tenant and actor key
**And** the actor triggers an automatic rebuild from the event stream (D14)
**And** callers receive a "service degraded" response during rebuild

**Given** an automatic projection rebuild in progress
**When** a query request arrives for the affected projection
**Then** the response includes a degraded status indicator
**And** the response does not return an error — it communicates that data may be stale or incomplete

**Given** the rebuild completes
**When** the projection state is restored
**Then** subsequent queries return normal responses without degradation indicators
**And** a "rebuild completed" event is logged at `Information` level

**Given** an operator wanting to manually rebuild projections
**When** they call the admin rebuild endpoint with a tenant ID
**Then** a per-tenant projection rebuild is triggered (D14)
**And** the rebuild replays events from EventStore through the pure projection handlers
**And** the rebuild is resumable (can restart from the last successfully processed event sequence number)

**Given** the admin rebuild endpoint
**When** reviewed for security
**Then** it requires authentication and elevated permissions
**And** it is not exposed via the public API — admin-only endpoint

**Given** the operational documentation
**When** reviewed for projection rebuild
**Then** it includes:
- Manual rebuild procedure (trigger, monitor, verify)
- Expected rebuild time estimates based on event count
- Impact on service availability during rebuild (queries return degraded responses)
- When to use manual rebuild (suspected drift, after state store migration)

---

## Epic 9: GDPR, Privacy, and v1.1 Search Extensions

Administrators can fulfill GDPR obligations and the platform can support privacy-safe v1.1 search and audit extensions. GDPR erasure, consent, restriction, portability, processing records, per-party keys, erased-state behavior, and subscriber notification remain the primary compliance scope. Temporal name queries and Hexalith.Memories-backed search are explicitly v1.1 extensions and must retain privacy, erasure, and tenant-isolation guarantees.

### Story 9.1: Per-Party Encryption Key Management

As an administrator,
I want per-party encryption keys managed via DAPR secret store,
So that each party's personal data can be independently encrypted and destroyed for GDPR erasure.

**Acceptance Criteria:**

**Given** a new party is created
**When** crypto-shredding is active (v1.1)
**Then** a per-party encryption key is created in the DAPR secret store (FR53)
**And** keys are organized in per-tenant namespaces

**Given** an existing per-party key
**When** a key rotation is triggered
**Then** a new versioned key is created (NFR11)
**And** the previous key version is retained read-only for historical event decryption
**And** re-encryption of historical events is NOT performed
**And** each encrypted field references its key version
**And** rotation occurs without service downtime or data loss

**Given** any key operation (create, read, rotate, delete)
**When** the operation completes
**Then** it is logged in an independent key access audit trail
**And** the audit trail is separate from the event stream

**Given** the key caching strategy
**When** per-party key lookups occur at command time
**Then** lookups do not violate NFR1 (< 1 second command processing)
**And** a caching strategy (per-request, short-TTL in-memory, or batch pre-fetch) is implemented

### Story 9.2: Field-Level Encryption & Crypto-Shredding Activation

As a developer,
I want personal data fields encrypted at rest via `[PersonalData]` attributes with zero domain code changes,
So that GDPR encryption is structural and the domain remains encryption-unaware.

**Acceptance Criteria:**

**Given** a party command that writes personal data fields
**When** the event is persisted to the event store
**Then** all `[PersonalData]`-attributed fields are encrypted using the party's per-party key (FR53)
**And** domain code has zero DAPR awareness — encryption is handled by infrastructure (NFR8)

**Given** encrypted events in the event store
**When** events are published to DAPR pub/sub
**Then** events are decrypted at publish time — subscribers receive readable data (FR54)
**And** subscribers never handle decryption

**Given** a decryption failure at publish time
**When** the circuit breaker activates
**Then** publication is prevented — unreadable events are never published
**And** the failure is logged and alerted

**Given** snapshots for a party
**When** crypto-shredding is active
**Then** snapshots participate in field-level encryption
**And** snapshot invalidation is part of the erasure transaction

**Given** type-dependent personal data classification (D6)
**When** encryption is applied
**Then** person parties: all PII encrypted (names, DOB, derived fields)
**And** organization parties: entity-level fields NOT encrypted by default
**And** all party types: contact channels and identifiers always encrypted
**And** `IsNaturalPerson = true` organizations: elevated to person-level encryption scope

### Story 9.3: Right to Erasure & Verification

As an administrator,
I want to trigger erasure that cryptographically destroys a party's personal data and verify completeness,
So that GDPR Article 17 right-to-erasure requests are fulfilled with automated verification.

**Acceptance Criteria:**

**Given** an administrator triggers erasure for a party
**When** the erasure command is processed
**Then** the party's per-party encryption key is destroyed via DAPR secret store (FR44)
**And** all personal data in events and snapshots becomes permanently unreadable
**And** event metadata (types, timestamps, aggregate IDs) survives — personal data doesn't

**Given** key destruction is complete
**When** the erasure verification job runs
**Then** it verifies erasure across all internal data stores (FR45):
- Projection actor states cleaned
- Search indexes purged
- Caches invalidated (explicit, not TTL-dependent)
**And** a verification report is produced itemizing all cleanup results
**And** the report is produced within 5 minutes of erasure trigger

**Given** erasure is complete
**When** a `PartyErased` event is published
**Then** all subscribers are notified (FR46)
**And** delivery is tracked — unacknowledged erasures alert after configurable timeout

**Given** an erased party
**When** a read request is made for that party
**Then** the response returns an "erased" status — not decryption errors (FR55)
**And** the read path checks erasure state before attempting decryption

**Given** key destruction fails
**When** the retry policy is exhausted
**Then** an alert is raised
**And** erasure verification is blocked until key destruction succeeds

### Story 9.4: Consent Management, Restriction & Portability

As an administrator,
I want to manage per-channel per-purpose consent, restrict processing, and export party data,
So that GDPR Articles 6, 18, and 20 obligations are fulfilled.

**Acceptance Criteria:**

**Given** a party with contact channels
**When** an administrator records consent for a specific channel and purpose
**Then** a consent record is created with: channel ID, purpose, lawful basis, timestamp (FR47)
**And** the consent record supports all lawful bases: consent, legitimate interest, contractual necessity, legal obligation

**Given** an active consent record
**When** an administrator revokes consent
**Then** the consent is revoked with a timestamp (FR48)
**And** the revocation is recorded in the processing activity log

**Given** a party under investigation
**When** an administrator restricts processing
**Then** the party's data is frozen — no modifications allowed while restricted (FR49)
**And** read access continues (data is not erased, just frozen)

**Given** a restricted party
**When** an administrator lifts the restriction
**Then** processing resumes normally (FR50)
**And** the restriction period is recorded in the processing activity log

**Given** a data portability request
**When** an administrator triggers export for a party
**Then** all party data is exported in machine-readable JSON format (FR51)
**And** the export includes: party details, all contact channels, all identifiers, all consent records

**Given** any processing activity on party data
**When** the activity completes
**Then** a complete, time-stamped record is maintained in the event stream (FR52)
**And** records support Article 30 compliance reporting

### Story 9.5: Temporal Name Queries

As a consumer,
I want to query historical party names at a point in time,
So that I can support legal and audit workflows without replaying the event stream on the request path.

**Acceptance Criteria:**

**Given** a party whose name has changed over time
**When** a temporal name query is made with a specific point in time
**Then** the party's name as it was at that timestamp is returned (FR72)
**And** the query uses pre-computed name history tracked in the party detail projection
**And** the request does not replay the event stream at query time.

**Given** a party with one or more name changes
**When** the full name history endpoint is called
**Then** name history entries are returned in chronological order
**And** each entry includes display name, sort name, change timestamp, and triggering event/source where available.

**Given** a timestamp before the party existed
**When** the temporal name query is made
**Then** the API returns `404 Not Found`.

**Given** a party has been erased
**When** the temporal name query is made after erasure
**Then** the API returns `410 Gone`
**And** erased name history is not returned.

**Given** an MCP caller
**When** `get_party_name_at` is called
**Then** it returns the same temporal name result and error semantics as REST.

### Story 9.6: Hexalith.Memories-Backed Party Search

As a consumer and AI agent,
I want Parties search to use Hexalith.Memories for lexical, semantic, hybrid, and graph-assisted retrieval,
So that party discovery uses the shared Hexalith search/memory module instead of a Parties-local search engine.

**Acceptance Criteria:**

**Given** Hexalith.Memories integration is enabled
**When** party events or projection changes occur
**Then** Parties indexes searchable party memory units into Memories
**And** indexed content includes display name, party type, contact channel values, identifier values, active/erased state, and useful event context
**And** metadata includes tenant id, party id, aggregate id, event type, timestamps, correlation id, causation id, and source service where available.

**Given** a consumer or AI agent calls party search with a natural-language query
**When** Memories integration is healthy
**Then** Parties uses Memories hybrid search by default
**And** matching memory units are hydrated back to authoritative Parties projection data
**And** response metadata includes Memories relevance, lexical, semantic, graph, and composite scores when available.

**Given** a caller requests lexical-only or semantic-only search
**When** the search mode is specified
**Then** Parties calls Memories single-axis search with axis `syntactic` or `semantic`.

**Given** graph context is requested from a known party or memory unit
**When** graph-assisted search executes
**Then** Parties uses Memories traversal or graph-scoped search
**And** hydrates related party results.

**Given** Memories is unavailable, disabled, or partially degraded
**When** a search request arrives
**Then** Parties falls back to local display-name search where possible
**And** returns a degraded indicator explaining that Memories-backed rich search was unavailable.

**Given** a party erasure is triggered
**When** erasure verification runs
**Then** all party-related Memories memory units and search indexes are purged or tombstoned
**And** erasure is not reported complete until Memories cleanup succeeds or is explicitly recorded as blocked.

**Given** dependency boundaries are checked
**When** `Hexalith.Parties.Contracts` is reviewed
**Then** it has no dependency on Hexalith.Memories packages.

---

## Epic 11: Hexalith.Tenants Integration for Parties

Hexalith.Parties uses Hexalith.Tenants as the source of truth for tenant lifecycle, membership, roles, and tenant configuration while preserving Parties-owned tenant isolation for party aggregates, projections, REST, MCP, and event publication.

### Story 11.1: AppHost and Package Integration

As a developer running Hexalith.Parties locally,
I want the Parties AppHost to compose with Hexalith.Tenants,
So that tenant lifecycle and membership are available through the same local topology as party management.

**Acceptance Criteria:**

**Given** the Parties AppHost
**When** the local development topology is started
**Then** the AppHost references the Tenants service/topology using the Tenants Aspire integration or equivalent local composition
**And** Tenants service configuration is visible in the topology.

**Given** local development setup
**When** the default sample environment is prepared
**Then** a default active tenant is seeded or documented through Hexalith.Tenants
**And** the sample/test user is assigned a role that permits party commands.

**Given** tenant authorization is enabled
**When** Parties starts
**Then** startup validates that Tenants integration configuration is present
**And** missing configuration fails fast with actionable diagnostics.

**Given** the Tenants service cannot be reached
**When** Parties health and readiness are checked
**Then** the failure is surfaced according to documented degraded behavior.

### Story 11.2: Tenants Event Consumption and Local Access Projection

As Hexalith.Parties,
I want to consume Hexalith.Tenants lifecycle, membership, role, and configuration events,
So that authorization decisions can be made locally without polling.

**Acceptance Criteria:**

**Given** relevant Hexalith.Tenants lifecycle, membership, role, or configuration events are published
**When** Parties receives them through DAPR pub/sub
**Then** Parties updates a local tenant access projection/cache.

**Given** the local tenant access projection/cache
**When** queried for tenant access
**Then** it records active tenant state, user membership, roles, and relevant tenant configuration.

**Given** a tenant is disabled or a user is removed from a tenant
**When** the corresponding Tenants event is processed by Parties
**Then** subsequent Parties commands and MCP tools fail closed for that tenant/user.

**Given** Tenants event consumption is eventually consistent
**When** developer documentation is reviewed
**Then** it explains the timing window
**And** it documents the synchronous enforcement path if the Tenants authorization plugin is enabled.

### Story 11.3: REST and MCP Tenant Authorization Enforcement

As a platform operator,
I want all Parties REST and MCP operations to enforce Tenants-backed access rules,
So that users cannot manage party data for inactive or unauthorized tenants.

**Acceptance Criteria:**

**Given** a REST command or query endpoint is called
**When** the request reaches Parties
**Then** the endpoint validates tenant access through `ITenantAccessService`.

**Given** an MCP tool is called
**When** the tool resolves session tenant context
**Then** the tool validates tenant access through the same `ITenantAccessService`
**And** it does not rely only on `McpSessionContext.Tenant`.

**Given** a Parties operation requires authorization
**When** access is evaluated
**Then** Reader permits read/search operations
**And** Contributor permits create/update/deactivate/reactivate operations
**And** Owner or a configured elevated role permits administrative party operations.

**Given** a request has missing tenant, inactive tenant, missing membership, insufficient role, or stale/unknown tenant state
**When** the request is evaluated
**Then** Parties rejects it with standardized ProblemDetails or MCP errors.

**Given** a command payload contains tenant information
**When** the payload is validated
**Then** tenant ID is ignored or rejected according to API contract rules
**And** tenant identity is never accepted from command payloads.

### Story 11.4: Tenants Integration Tests, Deployment Validation, and Documentation

As a developer and operator,
I want tests and docs proving Parties uses Hexalith.Tenants correctly,
So that tenant integration is reliable in CI and local development.

**Acceptance Criteria:**

**Given** fast tenant authorization test scenarios
**When** tests are written
**Then** they use `Hexalith.Tenants.Testing` where appropriate.

**Given** integration tests for Tenants-backed access
**When** the test suite runs
**Then** it covers active tenant allowed, disabled tenant denied, removed user denied, insufficient role denied, and cross-tenant projection isolation.

**Given** deployment validation tooling
**When** validation runs
**Then** it checks Tenants subscription/configuration
**And** reports actionable errors when integration is missing or unhealthy.

**Given** getting-started documentation
**When** a developer follows the guide
**Then** tenants are provisioned through Hexalith.Tenants.

**Given** tenant troubleshooting documentation
**When** reviewed
**Then** it distinguishes missing JWT claims from missing Tenants membership or role.

---

## Epic 10: Administration & Frontend (v1.2)

Administrators can browse, search, and inspect party records and process party-level GDPR requests via a FrontComposer-based Blazor/Razor admin portal that consumes EventStore-fronted Parties client/query/command contracts. Generic event and stream browsing is delegated to EventStore Admin UI through safe deep-links. Tenant lifecycle, membership, roles, and configuration management remain owned by Hexalith.Tenants admin capabilities; the Parties admin portal consumes active tenant context and must not duplicate Tenants management screens. Consuming application developers can embed a party picker component in their UIs for party search and selection.

Planning note: Epic 10 administration and picker delivery depends on Epic 11 Tenants integration and the Epic 12 EventStore-fronted consumer boundary. Story 12.7 is the active admin portal implementation path; Story 12.8 is the active picker rewrite path. Earlier TypeScript or direct Parties REST wording in Story 10.x is historical and superseded where it conflicts with Epic 12.

### Story 10.1: Admin Portal — Browse, Search & Inspect

As an administrator,
I want a web-based admin portal to browse, search, and inspect party records,
So that I can manage party data without using API tools or CLI commands.

**Acceptance Criteria:**

**Given** an authenticated administrator
**When** they access the admin portal
**Then** they can browse a paginated list of parties (FR65)
**And** they can search parties by display name in the baseline experience
**And** email or identifier search is available only when the dedicated search capability is enabled
**And** they can filter by party type (person/organization) and active status

**Given** a party in the list
**When** the administrator clicks to inspect
**Then** the full party detail is displayed:
- Party type and details (person or organization)
- All contact channels with type, value, and preferred status
- All identifiers with type and value
- Consent records (if v1.1 GDPR is active)
- Active/inactive status
- Creation and last modification dates

**Given** the admin portal
**When** rendering party data fields
**Then** output encoding is applied to all user-supplied data (NFR32)
**And** no stored XSS from party data (user-supplied or AI-created) is possible

**Given** the admin portal architecture
**When** reviewed
**Then** it is a FrontComposer-based Blazor/Razor domain surface
**And** it uses composable Parties-domain components for list, detail, GDPR operations, and safe EventStore Admin UI deep-links

### Story 10.2: Admin Portal — GDPR Operations

As an administrator,
I want to process GDPR requests (erasure, restriction, consent, export) via the admin portal,
So that DPO operations are efficient and auditable through a visual interface.

**Acceptance Criteria:**

**Given** an authenticated administrator viewing a party
**When** they trigger "Request Erasure"
**Then** crypto-shredding is triggered via the API (FR66)
**And** the erasure verification report is displayed when complete
**And** the full erasure flow (trigger → verify → confirm) completes within 15 minutes of active time

**Given** an authenticated administrator viewing a party
**When** they trigger "Restrict Processing"
**Then** the party's data is frozen via the API
**And** the restriction status is visible in the party detail view

**Given** an authenticated administrator viewing a party
**When** they manage consent records
**Then** they can add new consent (channel, purpose, lawful basis)
**And** they can revoke existing consent
**And** consent history is visible with timestamps

**Given** an authenticated administrator
**When** they trigger data portability export
**Then** the party data is exported as downloadable JSON
**And** the export is logged in the processing activity record

**Given** the admin dashboard
**When** accessed by a DPO
**Then** it provides: pending GDPR requests, consent status overview, and erasure audit trail

### Story 10.3: Embeddable Party Picker Component

Planning note: Story 10.3 / Story 12.8 party picker UX must cover embedding shape, type-ahead behavior, selected-id contract, host auth injection, stale-response clearing, accessibility, localization, and privacy-safe rendering/storage/logging. This UX source is separate from the admin portal UX artifact.

As a consuming application developer,
I want an embeddable party picker component for my UI,
So that my users can search and select parties without building a custom party selector.

**Acceptance Criteria:**

**Given** the party picker component
**When** embedded in a consuming application UI
**Then** it provides a search input with type-ahead party search (FR67)
**And** search results display party name, type, and key contact information
**And** selecting a party returns the party ID to the host application

**Given** the party picker component
**When** reviewed for architecture
**Then** it is a composable FrontComposer/Blazor picker component
**And** it communicates through the EventStore-fronted Parties client/query boundary
**And** it handles host request/auth injection through the accepted Parties client/EventStore gateway configuration

**Given** the party picker component
**When** used in different consuming applications
**Then** it is independently deployable (published as an npm package or similar)
**And** it supports theming/customization for host app visual consistency
