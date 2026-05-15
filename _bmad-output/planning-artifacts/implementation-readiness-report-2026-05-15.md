---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
documentsIncluded:
  prd:
    primary:
      path: _bmad-output/planning-artifacts/prd.md
      size: 83158
      modified: 2026-05-14 10:40
    supporting:
      - path: _bmad-output/planning-artifacts/prd-validation-report.md
        size: 26454
        modified: 2026-03-02 16:05
        note: Matched PRD pattern; treated as supporting validation artifact, not primary PRD.
  architecture:
    primary:
      path: _bmad-output/planning-artifacts/architecture.md
      size: 87881
      modified: 2026-05-14 10:40
  epics:
    primary:
      path: _bmad-output/planning-artifacts/epics.md
      size: 157240
      modified: 2026-05-15 07:36
  ux:
    primary:
      - path: _bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md
        size: 7391
        modified: 2026-05-10 13:27
      - path: _bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md
        size: 1997
        modified: 2026-05-12 21:27
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-15
**Project:** Hexalith.Parties

## Document Discovery

### PRD Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/prd.md` (83,158 bytes, modified 2026-05-14 10:40)
- `_bmad-output/planning-artifacts/prd-validation-report.md` (26,454 bytes, modified 2026-03-02 16:05; supporting validation artifact)

**Sharded Documents:**
- None found

### Architecture Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/architecture.md` (87,881 bytes, modified 2026-05-14 10:40)

**Sharded Documents:**
- None found

### Epics & Stories Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/epics.md` (157,240 bytes, modified 2026-05-15 07:36)

**Sharded Documents:**
- None found

### UX Design Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md` (7,391 bytes, modified 2026-05-10 13:27)
- `_bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md` (1,997 bytes, modified 2026-05-12 21:27)

**Sharded Documents:**
- None found

### Discovery Issues

- No whole-vs-sharded duplicate conflicts found.
- `prd-validation-report.md` matched the PRD filename pattern but is treated as a supporting validation artifact rather than the primary PRD.

## PRD Analysis

### Functional Requirements

FR1: Authorized client can create a new party as either a person or an organization with type-specific details

FR2: Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix)

FR3: Authorized client can update organization-specific details (legal name, trading name, legal form, registration number)

FR4: Authorized client can deactivate a party (soft lifecycle management)

FR5: Authorized client can reactivate a previously deactivated party

FR6: System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation -- `"{FirstName} {LastName}"` for persons, `"{LegalName}"` for organizations; locale-aware formatting deferred to v1.1)

FR7: Each party has a client-generated, immutable UUID as its stable identity

FR8: Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social)

FR9: Authorized client can update an existing contact channel on a party

FR10: Authorized client can remove a contact channel from a party

FR11: Authorized client can mark a contact channel as preferred for its type

FR12: Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references)

FR13: Authorized client can remove an identifier from a party

FR14: Consumer can list parties with pagination and filtering by type (person/organization) and active status

FR15: Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields.

FR16: *(Deferred to v1.1)* Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1.

FR17: Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans. MVP emits `displayName`; `email` and `identifier` are reserved for the future search model.

FR18: Consumer can retrieve full party details by ID

FR19: Recently created or updated parties become discoverable in search results within the eventual consistency window defined by NFR6

FR20: AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP. Email and identifier resolution require candidate retrieval or the future dedicated search capability.

FR21: AI agent can create a complete party (type details + contact channels + identifiers) in a single composite operation

FR22: AI agent can update party details, add/modify/remove contact channels and identifiers via a single operation

FR23: AI agent can retrieve full party details and list parties via dedicated AI-optimized tools

FR24: AI agent party creation returns the complete created party record, not just an identifier

FR25: AI agent tools accept partial and incomplete input gracefully, with documented default behaviors for omitted fields, and clear validation error messages when required fields are missing

FR26: .NET developer can integrate party management via a single package and one-line dependency registration

FR27: Developer can send party commands via typed client abstractions without infrastructure knowledge

FR28: Developer can query parties via typed client abstractions without infrastructure knowledge

FR29: Developer can interact with the party service via REST API from any programming language

FR30: System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action -- enabling developers to resolve the issue without consulting documentation or debugging the service

FR31: Developer can deploy a running instance from source with standard container tooling

FR32: Getting-started documentation enables a developer to deploy and send their first command as a self-service experience

FR33: Contract types package has zero runtime dependencies beyond netstandard2.1 -- consuming applications inherit no infrastructure stack

FR34: System publishes domain events when party state changes

FR35: Consuming application can subscribe to party events and build domain-specific read models

FR36: System handles duplicate commands idempotently (safe deduplication in distributed scenarios)

FR37: Forward-compatible event contracts (including party merge) are available to consuming applications from day one

FR38: Consuming application documentation includes handler patterns for erasure and dangling reference cleanup, with explicit warning that `PartyErased` subscription is mandatory for all consuming apps regardless of which other events they handle

FR39: System isolates party data by tenant at all layers -- no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering

FR40: System identifies tenant from authenticated credentials, never from request payloads

FR41: System rejects requests without valid tenant identity (fail-closed)

FR42: Personal data fields are architecturally marked for automated privacy enforcement without domain code changes

FR43: Personal data fields are excluded from all application logging

FR44: Administrator can trigger right-to-erasure, rendering all personal data for a party permanently unreadable

FR45: System verifies erasure completion across all internal data stores and reports results

FR46: System notifies all subscribers when a party is erased so they can clean up their references

FR47: Administrator can record per-channel, per-purpose consent for a specific party

FR48: Administrator can revoke previously recorded consent

FR49: Administrator can restrict processing of a party's data (freeze while complaint is investigated)

FR50: Administrator can lift restriction on a party's data to resume processing

FR51: Administrator can export all data for a specific party in a machine-readable format

FR52: System maintains a complete, time-stamped record of all processing activities on party data

FR53: System encrypts personal data in stored events and snapshots using per-party keys

FR54: Events published to subscribers contain readable data -- subscribers never handle decryption

FR55: System returns an "erased" status for erased parties, not cryptographic errors

FR56: System publishes auto-generated API specification documentation accessible to developers

FR57: System supports versioned API endpoints that coexist during deprecation periods

FR58: System maps domain rejections to standardized HTTP error formats with a documented error catalog

FR59: System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage

FR60: Developer can run the full system locally with a single command for development and evaluation

FR61: System provides deployment validation tooling to verify security configuration before production use

FR62: System displays a non-dismissable compliance warning until GDPR features are activated

FR63: System guarantees at-least-once event delivery to subscribers

FR64: System degrades gracefully when infrastructure components are unavailable -- read operations continue when write-side components fail

FR65: Administrator can browse, search, and inspect party records via an administration interface

FR66: Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface

FR67: Consuming application developer can embed a party picker component in their UI for party search and selection

FR68: Consumer can filter parties by creation date or last-modified date range

FR69: Update operations (API and MCP) return the updated party state in the response, not just a confirmation

FR70: Published domain events include tenant context for consuming application routing decisions

FR71: System exposes health and readiness signals for infrastructure orchestration

FR72: *(Deferred to v1.1)* Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit.

FR73: System delivers events for a single aggregate in causal order to each subscriber. Architecture note: Architecture must verify DAPR pub/sub ordering guarantees. If per-aggregate ordering cannot be guaranteed, document required handler design (order-tolerant or sequence-checking) in the architecture document.

FR74: MCP update operations use patch semantics -- only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update

Total FRs: 74

### Non-Functional Requirements

NFR1: Command processing (create, update, manage party) completes in < 1 second at NFR17 throughput levels; MCP tool calls complete in < 1 second end-to-end including transport

NFR2: Query operations (search, get by ID, list) return results in < 500ms at NFR17 throughput levels

NFR3: Aggregate rehydration completes in < 200ms with snapshot strategy active

NFR4: Search across 100K parties per tenant returns results within 500ms

NFR5: Service accepts requests within 30 seconds of container launch (cold start)

NFR6: Read projections reflect write operations within 2 seconds at NFR17 throughput levels (eventual consistency window)

NFR7: All data encrypted in transit (TLS 1.2+)

NFR8: Personal data fields encrypted at rest using per-party keys (activated in v1.1)

NFR9: Tenant isolation enforced at all layers -- zero cross-tenant data leakage under any condition

NFR10: JWT token validation on every request; fail-closed on invalid or missing tokens

NFR11: Per-tenant encryption keys can be rotated without service downtime or data loss

NFR12: Personal data excluded from all application logs

NFR13: All API endpoints require authentication -- no anonymous access

NFR14: System supports multi-tenant operation (no per-tenant infrastructure, stateless routing, partitionable metadata) validated at 100 concurrent tenants for MVP

NFR14a: System architecture supports scaling beyond 100 tenants without per-tenant infrastructure changes

NFR15: Tenant metadata operations (routing, key lookup) complete in < 50ms regardless of total tenant count

NFR16: System supports up to 100,000 parties per tenant (MVP validation target -- sufficient for startups and SMBs; enterprise scale at millions of parties addressed in v2 via Elasticsearch projection and horizontal scaling)

NFR17: System sustains 100 read requests/second and 20 write requests/second per tenant

NFR18: Event store performance degrades < 10% at 100K parties per tenant with snapshot strategy active

NFR19: Read projections remain responsive (< 500ms) at 100K parties per tenant

NFR20: Service recovers from crash, replays necessary event state, and accepts requests within 30 seconds of restart

NFR21: When event store is unreachable, read projection queries continue serving cached data with a staleness indicator

NFR22: No data loss on service restart -- event store is the durable source of truth

NFR23: At-least-once event delivery to subscribers via DAPR pub/sub

NFR24: Idempotent command handling ensures safe retry without duplicate side effects

NFR25: REST API conforms to auto-generated OpenAPI 3.x specification

NFR26: MCP server implements MCP protocol specification with 5 tools

NFR27: Published events follow stable, versioned contract schemas (append-only, additive changes only)

NFR28: Client NuGet packages impose < 10 transitive dependencies totalling < 5 MB (Contracts: zero runtime dependencies beyond netstandard2.1)

NFR29: Service has zero direct dependencies on specific state store or message broker implementations

NFR30: A developer deploys a running instance from source in < 15 minutes on first attempt using the documented getting-started guide

NFR31: NuGet client package size < 5MB with < 10 transitive dependencies

NFR32: (v1.2) Frontend applies output encoding to all party data fields rendered in the admin portal -- no stored XSS from user-supplied or AI-created party data

Total NFRs: 33

### Additional Requirements

- MVP must ship without GDPR compliance features and must warn operators not to store regulated EU personal data until v1.1. The warning must be non-dismissable in the admin UI header and API response headers until GDPR features are activated.
- MVP does not include duplicate detection. `find_parties` display-name match metadata provides advisory signals only; deduplication remains a consuming app/operator responsibility until v2.
- REST is the only guaranteed API surface for MVP. gRPC is deferred to v1.1 if demand warrants.
- REST query endpoints include `GET /api/v1/parties`, `GET /api/v1/parties/{id}`, and `GET /api/v1/parties/search?q=` with display-name match metadata.
- MCP ships exactly five tools for MVP: `find_parties`, `get_party`, `create_party`, `update_party`, and `delete_party`; `delete_party` is an AI-ergonomic alias for soft deactivation and is not GDPR erasure.
- Authentication and authorization are inherited from Hexalith.EventStore middleware. Parties must not implement custom auth; tenant identity comes from token claims.
- JSON is the only MVP request/response format.
- Rate limiting is a deployment infrastructure concern, not application domain logic.
- Versioning strategy requires URL-path REST versioning, append-only event schema evolution, tolerant deserialization, semantic NuGet versioning, and deprecation-period coexistence.
- `Hexalith.Parties.Contracts` must have zero runtime dependencies beyond `netstandard2.1`; `Hexalith.Parties.Client` depends only on Contracts and HTTP abstractions, not DAPR, MediatR, or FluentValidation.
- Documentation deliverables include README, getting-started guide, GDPR disclaimer, OpenAPI spec, and an emergency manual erasure procedure for pre-v1.1.
- Sample integration must live at `/samples/BasicConsumingApp/`, demonstrate client registration, commands, queries, event subscription, MCP usage, and be CI-verifiable.
- GDPR v1.1 requirements include crypto-shredding, consent, portability, restriction, Article 30 records, erasure propagation, internal verification, subscriber tracking, metadata retention after erasure, explicit cache invalidation, search index purge, DPIA notice, and DPA template.
- Security constraints include fail-closed tenant handling, framework-enforced tenant filtering, deployment validation of DAPR security policies, log sanitization through `[PersonalData]` enforcement, and no request-payload tenant trust.
- Event sourcing constraints include atomic event persistence, append-only immutable event contracts, no event upcasting, tolerant deserialization, actor-serialized commands per aggregate, snapshot checksum integrity, and testing/optimization for up to 50 contact channels per party.
- Encryption constraints include `[PersonalData]` coverage verification in MVP, field-level encryption in v1.1, decrypted pub/sub publication in v1.1, a decryption circuit breaker, snapshot participation in crypto-shredding, and an architecture-level key caching decision to preserve NFR1.
- Graceful degradation constraints require documented behavior for unavailable DAPR secret store, state store, and pub/sub, with committed events never lost when publication is delayed.
- Event-side integration requires at-least-once delivery, idempotent subscriber handlers, `PartyMerged` contract availability in v1, dangling reference guidance, and mandatory `PartyErased` handler guidance.
- Infrastructure portability requires DAPR-backed state store/message broker replacement without code changes, Aspire + Docker local development, and deployment validation scripts.
- MVP hard gates require clean-machine deploy in < 15 minutes, first `CreateParty` in < 30 minutes with comprehension check, single-prompt MCP creation returning the full party, and self-service documentation onboarding.
- EventStore validation is strategic: fix or adapt EventStore when its abstraction does not fit rather than adding Parties-side workarounds.

### PRD Completeness Assessment

The PRD is strong for implementation-readiness traceability: it contains complete numbered FR and NFR inventories, phase annotations, success gates, integration surfaces, regulatory constraints, and measurable verification hooks. The main readiness watchpoints are not missing PRD content but cross-artifact alignment: deferred-vs-MVP items must remain consistently scoped in epics, architecture must explicitly resolve DAPR ordering for FR73, and architecture must decide key caching/snapshot encryption behavior early enough to avoid v1.1 rework.


## Epic Coverage Validation

### Epic FR Coverage Extracted

FR1: Epic 1: Party Records and Lifecycle / Story 1.2: Create Party Aggregate with Stable Identity; Epic 1: Party Records and Lifecycle / Story 1.9: Return Updated Party State from Mutations
FR2: Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details
FR3: Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details
FR4: Epic 1: Party Records and Lifecycle / Story 1.6: Deactivate and Reactivate Parties; Epic 4: AI Agent Party Management / Story 4.6: Implement Delete Party as Soft Deactivation Tool
FR5: Epic 1: Party Records and Lifecycle / Story 1.6: Deactivate and Reactivate Parties
FR6: Epic 1: Party Records and Lifecycle / Story 1.2: Create Party Aggregate with Stable Identity; Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details
FR7: Epic 1: Party Records and Lifecycle / Story 1.2: Create Party Aggregate with Stable Identity
FR8: Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels
FR9: Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels
FR10: Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels
FR11: Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels
FR12: Epic 1: Party Records and Lifecycle / Story 1.5: Manage Party Identifiers
FR13: Epic 1: Party Records and Lifecycle / Story 1.5: Manage Party Identifiers; Epic 1: Party Records and Lifecycle / Story 1.9: Return Updated Party State from Mutations
FR14: Epic 2: Searchable Tenant-Safe Read Models / Story 2.2: Build Tenant Party Index Projection; Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties; Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool
FR15: Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata; Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions; Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool
FR16: Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions
FR17: Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata; Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions; Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool
FR18: Epic 2: Searchable Tenant-Safe Read Models / Story 2.1: Build Party Detail Projection; Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID; Epic 4: AI Agent Party Management / Story 4.3: Implement Get Party Tool
FR19: Epic 2: Searchable Tenant-Safe Read Models / Story 2.1: Build Party Detail Projection; Epic 2: Searchable Tenant-Safe Read Models / Story 2.2: Build Tenant Party Index Projection; Epic 2: Searchable Tenant-Safe Read Models / Story 2.7: Handle Projection Freshness and Graceful Degradation
FR20: Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata; Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool; Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety; Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses
FR21: Epic 4: AI Agent Party Management / Story 4.4: Implement Composite Create Party Tool; Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety; Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses
FR22: Epic 4: AI Agent Party Management / Story 4.5: Implement Patch-Oriented Update Party Tool; Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety; Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses
FR23: Epic 4: AI Agent Party Management / Story 4.1: Register Bounded MCP Tool Surface; Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool; Epic 4: AI Agent Party Management / Story 4.3: Implement Get Party Tool; Epic 4: AI Agent Party Management / Story 4.6: Implement Delete Party as Soft Deactivation Tool; Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety; Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses
FR24: Epic 4: AI Agent Party Management / Story 4.4: Implement Composite Create Party Tool; Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety; Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses
FR25: Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool; Epic 4: AI Agent Party Management / Story 4.3: Implement Get Party Tool; Epic 4: AI Agent Party Management / Story 4.4: Implement Composite Create Party Tool; Epic 4: AI Agent Party Management / Story 4.5: Implement Patch-Oriented Update Party Tool; Epic 4: AI Agent Party Management / Story 4.6: Implement Delete Party as Soft Deactivation Tool; Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety; Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses
FR26: Epic 3: Developer Integration and Local Adoption / Story 3.2: Provide Typed Parties Client Registration; Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration
FR27: Epic 3: Developer Integration and Local Adoption / Story 3.2: Provide Typed Parties Client Registration; Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API; Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration
FR28: Epic 3: Developer Integration and Local Adoption / Story 3.2: Provide Typed Parties Client Registration; Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API; Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration
FR29: Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API; Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog; Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration
FR30: Epic 1: Party Records and Lifecycle / Story 1.7: Idempotent Commands and Typed Rejections; Epic 3: Developer Integration and Local Adoption / Story 3.4: Map Domain Rejections to ProblemDetails; Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog
FR31: Epic 1: Party Records and Lifecycle / Story 1.1: Set Up Initial Project from EventStore Solution Structure; Epic 3: Developer Integration and Local Adoption / Story 3.6: Enable One-Command Local Run; Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation
FR32: Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation
FR33: Epic 3: Developer Integration and Local Adoption / Story 3.1: Publish Stable Contracts Package
FR34: Epic 5: Event-Driven Consumer Integration / Story 5.1: Publish Stable Party Domain Events; Epic 5: Event-Driven Consumer Integration / Story 5.3: Configure At-Least-Once Event Delivery; Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance
FR35: Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration; Epic 5: Event-Driven Consumer Integration / Story 5.3: Configure At-Least-Once Event Delivery; Epic 5: Event-Driven Consumer Integration / Story 5.4: Document Event Ordering and Subscriber Idempotency; Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance
FR36: Epic 1: Party Records and Lifecycle / Story 1.7: Idempotent Commands and Typed Rejections
FR37: Epic 3: Developer Integration and Local Adoption / Story 3.1: Publish Stable Contracts Package; Epic 5: Event-Driven Consumer Integration / Story 5.1: Publish Stable Party Domain Events; Epic 5: Event-Driven Consumer Integration / Story 5.6: Prepare Forward-Compatible Party Lifecycle Events
FR38: Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration; Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance; Epic 5: Event-Driven Consumer Integration / Story 5.7: Document Erasure Subscriber Responsibilities; Epic 6: GDPR Compliance Operations / Story 6.4: Publish PartyErased Subscriber Notification
FR39: Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID; Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties; Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata; Epic 2: Searchable Tenant-Safe Read Models / Story 2.6: Enforce Tenant-Safe Projection Reads; Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API; Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation; Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata
FR40: Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID; Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties; Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata; Epic 2: Searchable Tenant-Safe Read Models / Story 2.6: Enforce Tenant-Safe Projection Reads; Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API; Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation; Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata
FR41: Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID; Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties; Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata; Epic 2: Searchable Tenant-Safe Read Models / Story 2.6: Enforce Tenant-Safe Projection Reads; Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API; Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation; Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata
FR42: Epic 1: Party Records and Lifecycle / Story 1.8: Personal Data Marking and Log-Safe Domain Model; Epic 3: Developer Integration and Local Adoption / Story 3.1: Publish Stable Contracts Package; Epic 6: GDPR Compliance Operations / Story 6.1: Activate Per-Party Personal Data Encryption
FR43: Epic 1: Party Records and Lifecycle / Story 1.8: Personal Data Marking and Log-Safe Domain Model
FR44: Epic 6: GDPR Compliance Operations / Story 6.2: Trigger Right-to-Erasure by Crypto-Shredding; Epic 6: GDPR Compliance Operations / Story 6.9: Return Privacy-Preserving Erased Party Status
FR45: Epic 6: GDPR Compliance Operations / Story 6.3: Verify Erasure Across Internal Stores; Epic 6: GDPR Compliance Operations / Story 6.9: Return Privacy-Preserving Erased Party Status
FR46: Epic 5: Event-Driven Consumer Integration / Story 5.7: Document Erasure Subscriber Responsibilities; Epic 6: GDPR Compliance Operations / Story 6.4: Publish PartyErased Subscriber Notification
FR47: Epic 6: GDPR Compliance Operations / Story 6.5: Manage Per-Channel Per-Purpose Consent
FR48: Epic 6: GDPR Compliance Operations / Story 6.5: Manage Per-Channel Per-Purpose Consent
FR49: Epic 6: GDPR Compliance Operations / Story 6.6: Restrict and Resume Party Processing
FR50: Epic 6: GDPR Compliance Operations / Story 6.6: Restrict and Resume Party Processing
FR51: Epic 6: GDPR Compliance Operations / Story 6.7: Export Party Data Portability Package
FR52: Epic 6: GDPR Compliance Operations / Story 6.8: Record Processing Activities
FR53: Epic 6: GDPR Compliance Operations / Story 6.1: Activate Per-Party Personal Data Encryption; Epic 6: GDPR Compliance Operations / Story 6.10: Rotate Tenant Encryption Keys
FR54: Epic 5: Event-Driven Consumer Integration / Story 5.1: Publish Stable Party Domain Events
FR55: Epic 6: GDPR Compliance Operations / Story 6.1: Activate Per-Party Personal Data Encryption; Epic 6: GDPR Compliance Operations / Story 6.2: Trigger Right-to-Erasure by Crypto-Shredding; Epic 6: GDPR Compliance Operations / Story 6.9: Return Privacy-Preserving Erased Party Status; Epic 6: GDPR Compliance Operations / Story 6.10: Rotate Tenant Encryption Keys
FR56: Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog; Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation
FR57: Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API; Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog
FR58: Epic 3: Developer Integration and Local Adoption / Story 3.4: Map Domain Rejections to ProblemDetails; Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog
FR59: Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration
FR60: Epic 1: Party Records and Lifecycle / Story 1.1: Set Up Initial Project from EventStore Solution Structure; Epic 3: Developer Integration and Local Adoption / Story 3.6: Enable One-Command Local Run; Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation
FR61: Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation
FR62: Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog; Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation; Epic 3: Developer Integration and Local Adoption / Story 3.10: Display MVP Compliance Warning
FR63: Epic 5: Event-Driven Consumer Integration / Story 5.3: Configure At-Least-Once Event Delivery; Epic 5: Event-Driven Consumer Integration / Story 5.4: Document Event Ordering and Subscriber Idempotency; Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance
FR64: Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID; Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties; Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata; Epic 2: Searchable Tenant-Safe Read Models / Story 2.7: Handle Projection Freshness and Graceful Degradation; Epic 2: Searchable Tenant-Safe Read Models / Story 2.8: Projection Rebuild and Health Monitoring
FR65: Epic 7: Administration Console / Story 7.1: Compose Admin Console Working View; Epic 7: Administration Console / Story 7.2: Browse and Filter Party Results; Epic 7: Administration Console / Story 7.3: Inspect Party Detail Safely; Epic 7: Administration Console / Story 7.4: Handle Admin Empty, Error, and Degraded States; Epic 7: Administration Console / Story 7.5: Add Safe EventStore Admin UI Links; Epic 7: Administration Console / Story 7.8: Localize Admin Console Text and Status; Epic 7: Administration Console / Story 7.9: Enforce Admin Console Accessibility; Epic 7: Administration Console / Story 7.10: Enforce Admin Console Privacy and Encoding Rules
FR66: Epic 7: Administration Console / Story 7.6: Gate GDPR Operations on Accepted Client Contract; Epic 7: Administration Console / Story 7.7: Implement GDPR Operation Panels; Epic 7: Administration Console / Story 7.8: Localize Admin Console Text and Status; Epic 7: Administration Console / Story 7.9: Enforce Admin Console Accessibility; Epic 7: Administration Console / Story 7.10: Enforce Admin Console Privacy and Encoding Rules
FR67: Epic 8: Embeddable Party Picker / Story 8.1: Compose Embeddable Party Picker Shell; Epic 8: Embeddable Party Picker / Story 8.2: Implement Typeahead Search and Bounded Results; Epic 8: Embeddable Party Picker / Story 8.3: Emit Durable Selection by Party Id; Epic 8: Embeddable Party Picker / Story 8.4: Handle Picker States and Stale Responses; Epic 8: Embeddable Party Picker / Story 8.5: Enforce Picker Accessibility and Localization; Epic 8: Embeddable Party Picker / Story 8.6: Enforce Picker Privacy and Integration Boundary
FR68: Epic 2: Searchable Tenant-Safe Read Models / Story 2.2: Build Tenant Party Index Projection; Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties
FR69: Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details; Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels; Epic 1: Party Records and Lifecycle / Story 1.5: Manage Party Identifiers; Epic 1: Party Records and Lifecycle / Story 1.6: Deactivate and Reactivate Parties; Epic 1: Party Records and Lifecycle / Story 1.9: Return Updated Party State from Mutations
FR70: Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata
FR71: Epic 2: Searchable Tenant-Safe Read Models / Story 2.7: Handle Projection Freshness and Graceful Degradation; Epic 2: Searchable Tenant-Safe Read Models / Story 2.8: Projection Rebuild and Health Monitoring; Epic 3: Developer Integration and Local Adoption / Story 3.6: Enable One-Command Local Run
FR72: Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions
FR73: Epic 5: Event-Driven Consumer Integration / Story 5.4: Document Event Ordering and Subscriber Idempotency; Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance
FR74: Epic 4: AI Agent Party Management / Story 4.5: Implement Patch-Oriented Update Party Tool; Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety; Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses

Total FRs in epics: 74

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Authorized client can create a new party as either a person or an organization with type-specific details | Epic 1: Party Records and Lifecycle / Story 1.2: Create Party Aggregate with Stable Identity<br>Epic 1: Party Records and Lifecycle / Story 1.9: Return Updated Party State from Mutations | Covered |
| FR2 | Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix) | Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details | Covered |
| FR3 | Authorized client can update organization-specific details (legal name, trading name, legal form, registration number) | Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details | Covered |
| FR4 | Authorized client can deactivate a party (soft lifecycle management) | Epic 1: Party Records and Lifecycle / Story 1.6: Deactivate and Reactivate Parties<br>Epic 4: AI Agent Party Management / Story 4.6: Implement Delete Party as Soft Deactivation Tool | Covered |
| FR5 | Authorized client can reactivate a previously deactivated party | Epic 1: Party Records and Lifecycle / Story 1.6: Deactivate and Reactivate Parties | Covered |
| FR6 | System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation — `"{FirstName} {LastName}"` for persons, `"{LegalName}"` for organizations; locale-aware formatting deferred to v1.1) | Epic 1: Party Records and Lifecycle / Story 1.2: Create Party Aggregate with Stable Identity<br>Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details | Covered |
| FR7 | Each party has a client-generated, immutable UUID as its stable identity | Epic 1: Party Records and Lifecycle / Story 1.2: Create Party Aggregate with Stable Identity | Covered |
| FR8 | Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social) | Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels | Covered |
| FR9 | Authorized client can update an existing contact channel on a party | Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels | Covered |
| FR10 | Authorized client can remove a contact channel from a party | Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels | Covered |
| FR11 | Authorized client can mark a contact channel as preferred for its type | Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels | Covered |
| FR12 | Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references) | Epic 1: Party Records and Lifecycle / Story 1.5: Manage Party Identifiers | Covered |
| FR13 | Authorized client can remove an identifier from a party | Epic 1: Party Records and Lifecycle / Story 1.5: Manage Party Identifiers<br>Epic 1: Party Records and Lifecycle / Story 1.9: Return Updated Party State from Mutations | Covered |
| FR14 | Consumer can list parties with pagination and filtering by type (person/organization) and active status | Epic 2: Searchable Tenant-Safe Read Models / Story 2.2: Build Tenant Party Index Projection<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties<br>Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool | Covered |
| FR15 | Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields. | Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions<br>Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool | Covered |
| FR16 | *(Deferred to v1.1)* Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1. | Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions | Covered |
| FR17 | Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans. MVP emits `displayName`; `email` and `identifier` are reserved for the future search model. | Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions<br>Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool | Covered |
| FR18 | Consumer can retrieve full party details by ID | Epic 2: Searchable Tenant-Safe Read Models / Story 2.1: Build Party Detail Projection<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID<br>Epic 4: AI Agent Party Management / Story 4.3: Implement Get Party Tool | Covered |
| FR19 | Recently created or updated parties become discoverable in search results within the eventual consistency window defined by NFR6 | Epic 2: Searchable Tenant-Safe Read Models / Story 2.1: Build Party Detail Projection<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.2: Build Tenant Party Index Projection<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.7: Handle Projection Freshness and Graceful Degradation | Covered |
| FR20 | AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP. Email and identifier resolution require candidate retrieval or the future dedicated search capability. | Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata<br>Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool<br>Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety<br>Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses | Covered |
| FR21 | AI agent can create a complete party (type details + contact channels + identifiers) in a single composite operation | Epic 4: AI Agent Party Management / Story 4.4: Implement Composite Create Party Tool<br>Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety<br>Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses | Covered |
| FR22 | AI agent can update party details, add/modify/remove contact channels and identifiers via a single operation | Epic 4: AI Agent Party Management / Story 4.5: Implement Patch-Oriented Update Party Tool<br>Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety<br>Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses | Covered |
| FR23 | AI agent can retrieve full party details and list parties via dedicated AI-optimized tools | Epic 4: AI Agent Party Management / Story 4.1: Register Bounded MCP Tool Surface<br>Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool<br>Epic 4: AI Agent Party Management / Story 4.3: Implement Get Party Tool<br>Epic 4: AI Agent Party Management / Story 4.6: Implement Delete Party as Soft Deactivation Tool<br>Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety<br>Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses | Covered |
| FR24 | AI agent party creation returns the complete created party record, not just an identifier | Epic 4: AI Agent Party Management / Story 4.4: Implement Composite Create Party Tool<br>Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety<br>Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses | Covered |
| FR25 | AI agent tools accept partial and incomplete input gracefully, with documented default behaviors for omitted fields, and clear validation error messages when required fields are missing | Epic 4: AI Agent Party Management / Story 4.2: Implement AI-Friendly Find Parties Tool<br>Epic 4: AI Agent Party Management / Story 4.3: Implement Get Party Tool<br>Epic 4: AI Agent Party Management / Story 4.4: Implement Composite Create Party Tool<br>Epic 4: AI Agent Party Management / Story 4.5: Implement Patch-Oriented Update Party Tool<br>Epic 4: AI Agent Party Management / Story 4.6: Implement Delete Party as Soft Deactivation Tool<br>Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety<br>Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses | Covered |
| FR26 | .NET developer can integrate party management via a single package and one-line dependency registration | Epic 3: Developer Integration and Local Adoption / Story 3.2: Provide Typed Parties Client Registration<br>Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration | Covered |
| FR27 | Developer can send party commands via typed client abstractions without infrastructure knowledge | Epic 3: Developer Integration and Local Adoption / Story 3.2: Provide Typed Parties Client Registration<br>Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API<br>Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration | Covered |
| FR28 | Developer can query parties via typed client abstractions without infrastructure knowledge | Epic 3: Developer Integration and Local Adoption / Story 3.2: Provide Typed Parties Client Registration<br>Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API<br>Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration | Covered |
| FR29 | Developer can interact with the party service via REST API from any programming language | Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API<br>Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog<br>Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration | Covered |
| FR30 | System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action — enabling developers to resolve the issue without consulting documentation or debugging the service | Epic 1: Party Records and Lifecycle / Story 1.7: Idempotent Commands and Typed Rejections<br>Epic 3: Developer Integration and Local Adoption / Story 3.4: Map Domain Rejections to ProblemDetails<br>Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog | Covered |
| FR31 | Developer can deploy a running instance from source with standard container tooling | Epic 1: Party Records and Lifecycle / Story 1.1: Set Up Initial Project from EventStore Solution Structure<br>Epic 3: Developer Integration and Local Adoption / Story 3.6: Enable One-Command Local Run<br>Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation | Covered |
| FR32 | Getting-started documentation enables a developer to deploy and send their first command as a self-service experience | Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation | Covered |
| FR33 | Contract types package has zero runtime dependencies beyond netstandard2.1 — consuming applications inherit no infrastructure stack | Epic 3: Developer Integration and Local Adoption / Story 3.1: Publish Stable Contracts Package | Covered |
| FR34 | System publishes domain events when party state changes | Epic 5: Event-Driven Consumer Integration / Story 5.1: Publish Stable Party Domain Events<br>Epic 5: Event-Driven Consumer Integration / Story 5.3: Configure At-Least-Once Event Delivery<br>Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance | Covered |
| FR35 | Consuming application can subscribe to party events and build domain-specific read models | Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration<br>Epic 5: Event-Driven Consumer Integration / Story 5.3: Configure At-Least-Once Event Delivery<br>Epic 5: Event-Driven Consumer Integration / Story 5.4: Document Event Ordering and Subscriber Idempotency<br>Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance | Covered |
| FR36 | System handles duplicate commands idempotently (safe deduplication in distributed scenarios) | Epic 1: Party Records and Lifecycle / Story 1.7: Idempotent Commands and Typed Rejections | Covered |
| FR37 | Forward-compatible event contracts (including party merge) are available to consuming applications from day one | Epic 3: Developer Integration and Local Adoption / Story 3.1: Publish Stable Contracts Package<br>Epic 5: Event-Driven Consumer Integration / Story 5.1: Publish Stable Party Domain Events<br>Epic 5: Event-Driven Consumer Integration / Story 5.6: Prepare Forward-Compatible Party Lifecycle Events | Covered |
| FR38 | Consuming application documentation includes handler patterns for erasure and dangling reference cleanup, with explicit warning that `PartyErased` subscription is mandatory for all consuming apps regardless of which other events they handle | Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration<br>Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance<br>Epic 5: Event-Driven Consumer Integration / Story 5.7: Document Erasure Subscriber Responsibilities<br>Epic 6: GDPR Compliance Operations / Story 6.4: Publish PartyErased Subscriber Notification | Covered |
| FR39 | System isolates party data by tenant at all layers — no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering | Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.6: Enforce Tenant-Safe Projection Reads<br>Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API<br>Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation<br>Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata | Covered |
| FR40 | System identifies tenant from authenticated credentials, never from request payloads | Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.6: Enforce Tenant-Safe Projection Reads<br>Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API<br>Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation<br>Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata | Covered |
| FR41 | System rejects requests without valid tenant identity (fail-closed) | Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.6: Enforce Tenant-Safe Projection Reads<br>Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API<br>Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation<br>Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata | Covered |
| FR42 | Personal data fields are architecturally marked for automated privacy enforcement without domain code changes | Epic 1: Party Records and Lifecycle / Story 1.8: Personal Data Marking and Log-Safe Domain Model<br>Epic 3: Developer Integration and Local Adoption / Story 3.1: Publish Stable Contracts Package<br>Epic 6: GDPR Compliance Operations / Story 6.1: Activate Per-Party Personal Data Encryption | Covered |
| FR43 | Personal data fields are excluded from all application logging | Epic 1: Party Records and Lifecycle / Story 1.8: Personal Data Marking and Log-Safe Domain Model | Covered |
| FR44 | Administrator can trigger right-to-erasure, rendering all personal data for a party permanently unreadable | Epic 6: GDPR Compliance Operations / Story 6.2: Trigger Right-to-Erasure by Crypto-Shredding<br>Epic 6: GDPR Compliance Operations / Story 6.9: Return Privacy-Preserving Erased Party Status | Covered |
| FR45 | System verifies erasure completion across all internal data stores and reports results | Epic 6: GDPR Compliance Operations / Story 6.3: Verify Erasure Across Internal Stores<br>Epic 6: GDPR Compliance Operations / Story 6.9: Return Privacy-Preserving Erased Party Status | Covered |
| FR46 | System notifies all subscribers when a party is erased so they can clean up their references | Epic 5: Event-Driven Consumer Integration / Story 5.7: Document Erasure Subscriber Responsibilities<br>Epic 6: GDPR Compliance Operations / Story 6.4: Publish PartyErased Subscriber Notification | Covered |
| FR47 | Administrator can record per-channel, per-purpose consent for a specific party | Epic 6: GDPR Compliance Operations / Story 6.5: Manage Per-Channel Per-Purpose Consent | Covered |
| FR48 | Administrator can revoke previously recorded consent | Epic 6: GDPR Compliance Operations / Story 6.5: Manage Per-Channel Per-Purpose Consent | Covered |
| FR49 | Administrator can restrict processing of a party's data (freeze while complaint is investigated) | Epic 6: GDPR Compliance Operations / Story 6.6: Restrict and Resume Party Processing | Covered |
| FR50 | Administrator can lift restriction on a party's data to resume processing | Epic 6: GDPR Compliance Operations / Story 6.6: Restrict and Resume Party Processing | Covered |
| FR51 | Administrator can export all data for a specific party in a machine-readable format | Epic 6: GDPR Compliance Operations / Story 6.7: Export Party Data Portability Package | Covered |
| FR52 | System maintains a complete, time-stamped record of all processing activities on party data | Epic 6: GDPR Compliance Operations / Story 6.8: Record Processing Activities | Covered |
| FR53 | System encrypts personal data in stored events and snapshots using per-party keys | Epic 6: GDPR Compliance Operations / Story 6.1: Activate Per-Party Personal Data Encryption<br>Epic 6: GDPR Compliance Operations / Story 6.10: Rotate Tenant Encryption Keys | Covered |
| FR54 | Events published to subscribers contain readable data — subscribers never handle decryption | Epic 5: Event-Driven Consumer Integration / Story 5.1: Publish Stable Party Domain Events | Covered |
| FR55 | System returns an "erased" status for erased parties, not cryptographic errors | Epic 6: GDPR Compliance Operations / Story 6.1: Activate Per-Party Personal Data Encryption<br>Epic 6: GDPR Compliance Operations / Story 6.2: Trigger Right-to-Erasure by Crypto-Shredding<br>Epic 6: GDPR Compliance Operations / Story 6.9: Return Privacy-Preserving Erased Party Status<br>Epic 6: GDPR Compliance Operations / Story 6.10: Rotate Tenant Encryption Keys | Covered |
| FR56 | System publishes auto-generated API specification documentation accessible to developers | Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog<br>Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation | Covered |
| FR57 | System supports versioned API endpoints that coexist during deprecation periods | Epic 3: Developer Integration and Local Adoption / Story 3.3: Expose Versioned REST Party API<br>Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog | Covered |
| FR58 | System maps domain rejections to standardized HTTP error formats with a documented error catalog | Epic 3: Developer Integration and Local Adoption / Story 3.4: Map Domain Rejections to ProblemDetails<br>Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog | Covered |
| FR59 | System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage | Epic 3: Developer Integration and Local Adoption / Story 3.8: Provide Runnable Sample Integration | Covered |
| FR60 | Developer can run the full system locally with a single command for development and evaluation | Epic 1: Party Records and Lifecycle / Story 1.1: Set Up Initial Project from EventStore Solution Structure<br>Epic 3: Developer Integration and Local Adoption / Story 3.6: Enable One-Command Local Run<br>Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation | Covered |
| FR61 | System provides deployment validation tooling to verify security configuration before production use | Epic 3: Developer Integration and Local Adoption / Story 3.9: Add Deployment Security Validation | Covered |
| FR62 | System displays a non-dismissable compliance warning until GDPR features are activated | Epic 3: Developer Integration and Local Adoption / Story 3.5: Generate OpenAPI and Error Catalog<br>Epic 3: Developer Integration and Local Adoption / Story 3.7: Write Getting Started Documentation<br>Epic 3: Developer Integration and Local Adoption / Story 3.10: Display MVP Compliance Warning | Covered |
| FR63 | System guarantees at-least-once event delivery to subscribers | Epic 5: Event-Driven Consumer Integration / Story 5.3: Configure At-Least-Once Event Delivery<br>Epic 5: Event-Driven Consumer Integration / Story 5.4: Document Event Ordering and Subscriber Idempotency<br>Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance | Covered |
| FR64 | System degrades gracefully when infrastructure components are unavailable — read operations continue when write-side components fail | Epic 2: Searchable Tenant-Safe Read Models / Story 2.3: Query Party Details by ID<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.5: Search Parties by Display Name with Match Metadata<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.7: Handle Projection Freshness and Graceful Degradation<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.8: Projection Rebuild and Health Monitoring | Covered |
| FR65 | Administrator can browse, search, and inspect party records via an administration interface | Epic 7: Administration Console / Story 7.1: Compose Admin Console Working View<br>Epic 7: Administration Console / Story 7.2: Browse and Filter Party Results<br>Epic 7: Administration Console / Story 7.3: Inspect Party Detail Safely<br>Epic 7: Administration Console / Story 7.4: Handle Admin Empty, Error, and Degraded States<br>Epic 7: Administration Console / Story 7.5: Add Safe EventStore Admin UI Links<br>Epic 7: Administration Console / Story 7.8: Localize Admin Console Text and Status<br>Epic 7: Administration Console / Story 7.9: Enforce Admin Console Accessibility<br>Epic 7: Administration Console / Story 7.10: Enforce Admin Console Privacy and Encoding Rules | Covered |
| FR66 | Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface | Epic 7: Administration Console / Story 7.6: Gate GDPR Operations on Accepted Client Contract<br>Epic 7: Administration Console / Story 7.7: Implement GDPR Operation Panels<br>Epic 7: Administration Console / Story 7.8: Localize Admin Console Text and Status<br>Epic 7: Administration Console / Story 7.9: Enforce Admin Console Accessibility<br>Epic 7: Administration Console / Story 7.10: Enforce Admin Console Privacy and Encoding Rules | Covered |
| FR67 | Consuming application developer can embed a party picker component in their UI for party search and selection | Epic 8: Embeddable Party Picker / Story 8.1: Compose Embeddable Party Picker Shell<br>Epic 8: Embeddable Party Picker / Story 8.2: Implement Typeahead Search and Bounded Results<br>Epic 8: Embeddable Party Picker / Story 8.3: Emit Durable Selection by Party Id<br>Epic 8: Embeddable Party Picker / Story 8.4: Handle Picker States and Stale Responses<br>Epic 8: Embeddable Party Picker / Story 8.5: Enforce Picker Accessibility and Localization<br>Epic 8: Embeddable Party Picker / Story 8.6: Enforce Picker Privacy and Integration Boundary | Covered |
| FR68 | Consumer can filter parties by creation date or last-modified date range | Epic 2: Searchable Tenant-Safe Read Models / Story 2.2: Build Tenant Party Index Projection<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.4: List and Filter Parties | Covered |
| FR69 | Update operations (API and MCP) return the updated party state in the response, not just a confirmation | Epic 1: Party Records and Lifecycle / Story 1.3: Update Person and Organization Details<br>Epic 1: Party Records and Lifecycle / Story 1.4: Manage Contact Channels<br>Epic 1: Party Records and Lifecycle / Story 1.5: Manage Party Identifiers<br>Epic 1: Party Records and Lifecycle / Story 1.6: Deactivate and Reactivate Parties<br>Epic 1: Party Records and Lifecycle / Story 1.9: Return Updated Party State from Mutations | Covered |
| FR70 | Published domain events include tenant context for consuming application routing decisions | Epic 5: Event-Driven Consumer Integration / Story 5.2: Include Tenant Context and Envelope Metadata | Covered |
| FR71 | System exposes health and readiness signals for infrastructure orchestration | Epic 2: Searchable Tenant-Safe Read Models / Story 2.7: Handle Projection Freshness and Graceful Degradation<br>Epic 2: Searchable Tenant-Safe Read Models / Story 2.8: Projection Rebuild and Health Monitoring<br>Epic 3: Developer Integration and Local Adoption / Story 3.6: Enable One-Command Local Run | Covered |
| FR72 | *(Deferred to v1.1)* Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit. | Epic 2: Searchable Tenant-Safe Read Models / Story 2.9: Prepare Deferred Search and Temporal Query Extensions | Covered |
| FR73 | System delivers events for a single aggregate in causal order to each subscriber | Epic 5: Event-Driven Consumer Integration / Story 5.4: Document Event Ordering and Subscriber Idempotency<br>Epic 5: Event-Driven Consumer Integration / Story 5.5: Provide Consumer Read-Model Handler Guidance | Covered |
| FR74 | MCP update operations use patch semantics — only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update | Epic 4: AI Agent Party Management / Story 4.5: Implement Patch-Oriented Update Party Tool<br>Epic 4: AI Agent Party Management / Story 4.7: Enforce MCP Boundary and Tool Safety<br>Epic 4: AI Agent Party Management / Story 4.8: Validate MCP Latency, Errors, and Complete Responses | Covered |

### Missing Requirements

- None. All PRD functional requirements FR1-FR74 have at least one story-level coverage reference in the epics document.

### FRs Referenced By Epics But Not In PRD

- None found.

### Coverage Statistics

- Total PRD FRs: 74
- FRs covered in epics: 74
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found.

- `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md`
- `_bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md`

### UX to PRD Alignment

- Admin portal UX aligns to PRD FR65 and FR66: browse/search/inspect party records, process GDPR requests, and preserve privacy-safe administration behavior.
- Party picker UX aligns to PRD FR67: embeddable search and selection component for consuming applications.
- UX privacy and encoding requirements align to NFR32 and PRD security requirements FR39-FR43, especially no PII in URLs, storage keys, logs, telemetry, filenames, raw ProblemDetails, or durable picker payloads.
- UX operational states align to PRD reliability/security expectations: fail-closed tenant/auth handling, stale response suppression, forbidden/not-found/gone handling, and non-color-only degraded/blocked states.
- UX interaction constraints are represented in the epics inventory as UX-DR1 through UX-DR32 and mapped into Epic 7 and Epic 8 stories.

### UX to Architecture Alignment

- Architecture D20 supports the admin portal as a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor.
- Architecture D20 supports EventStore-fronted query/client access, typed Parties command boundary, and safe EventStore Admin UI deep-links instead of duplicating a generic stream browser.
- Architecture D20 supports the UX fail-closed model by requiring sensitive state clearing on sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures.
- Architecture dependency boundaries support the party picker and admin portal integration rule that UI should go through accepted Parties client/EventStore gateway boundaries rather than actor-host internals or projection actors.
- Architecture support for party picker is present but less explicit than admin portal support; the picker is named in D20 affected areas and covered by Epic 8, but a dedicated picker architecture subsection would reduce implementation ambiguity.

### Alignment Issues

- Architecture freshness issue: the architecture document still states "54 FRs" in its requirements overview and validation summary, while the current PRD and epics contain 74 FRs. This does not appear to remove coverage for the UI FRs, but it creates traceability noise for readiness.
- UX blocker reference issue: the admin UX uses dated blocker text `Blocked on Story 12.5 EventStore Parties client contract`, while the current epics use Story 7.x and 8.x numbering. If Story 12.5 is an external FrontComposer/EventStore dependency, it should be explicitly referenced in the architecture or dependency plan; otherwise the blocker text should be updated.
- Party picker architecture detail gap: the UX addendum defines durable selection, stale-response handling, host auth context, and boundary rules, while the architecture provides mostly general FrontComposer/client-boundary support. Epic 8 covers the implementation details, but architecture could better document the picker as its own frontend surface.

### Warnings

- UI is clearly implied and documented; there is no missing-UX warning.
- The frontend architecture is marked v1.2/deferred in architecture, while epics include concrete admin and picker stories. Implementation should confirm whether Epic 7 and Epic 8 are in-scope for the upcoming implementation wave or intentionally queued for v1.2.

## Epic Quality Review

### Review Scope

- Epics reviewed: 8
- Stories reviewed: 69
- Story format: all stories include `I want`, `So that`, acceptance criteria, and at least one Given/When/Then acceptance scenario.
- Coverage result from prior step: 74/74 PRD FRs mapped to at least one story-level reference.

### Epic Structure Validation

| Epic | User Value Focus | Independence | Assessment |
| --- | --- | --- | --- |
| Epic 1: Party Records and Lifecycle | Strong: enables durable party creation, updates, identifiers, contact channels, lifecycle, idempotency, and privacy marking. | Mostly independent. Story 1.1 is a technical setup story, but this is required by the starter-template rule for a greenfield project. | Pass with accepted setup exception. |
| Epic 2: Searchable Tenant-Safe Read Models | User outcome is clear: consumers can retrieve, list, search, filter, and observe projection freshness. | Depends on Epic 1 events, which is allowed for Epic 2. | Pass, but title uses technical language. |
| Epic 3: Developer Integration and Local Adoption | Strong developer value: packages, REST, docs, samples, one-command local run, errors. | Can build on Epic 1 and Epic 2 outputs. | Pass. |
| Epic 4: AI Agent Party Management | Strong AI-agent value: bounded MCP tool surface for find/get/create/update/delete. | Can build on earlier domain/query/API foundation. | Pass. |
| Epic 5: Event-Driven Consumer Integration | Strong consumer value: event contracts, tenant context, delivery, ordering guidance, handler guidance. | Builds on domain events from Epic 1; does not require later epics. | Pass. |
| Epic 6: GDPR Compliance Operations | Strong DPO/operator value, but it is a v1.1 capability set. | Builds on personal-data marking, event publishing, and projection behavior from earlier epics. | Pass with phase-scope warning. |
| Epic 7: Administration Console | Strong administrator value through a FrontComposer admin surface. | Depends on an accepted Parties client/EventStore contract and external blocker text referencing Story 12.5. | Major issue: external dependency must be resolved or explicitly tracked. |
| Epic 8: Embeddable Party Picker | Strong host app/developer value through embeddable selection. | Depends on accepted query/client boundary; otherwise self-contained. | Pass with architecture-detail warning. |

### Critical Violations

- None found. There are no wholly technical epics with zero user value, no missing acceptance criteria, and no uncovered PRD functional requirements.

### Major Issues

1. Deferred requirements are counted as covered by preparatory stories.
   - Examples: Story 2.9 covers FR16 and FR72 while explicitly preparing deferred v1.1 semantic search and temporal query behavior; Stories 5.6 and 5.7 prepare future lifecycle/GDPR event handling.
   - Impact: Coverage appears complete, but some covered FRs are not implementation-complete for the current phase. This is acceptable only if the implementation wave is a multi-version roadmap rather than MVP-only delivery.
   - Recommendation: Mark each story with phase (`MVP`, `v1.1`, `v1.2`) and distinguish `implemented coverage` from `prepared/deferred coverage` in the epic map.

2. Epic 7 has an unresolved external forward dependency.
   - Examples: UX-DR11 and Story 7.6 require disabled GDPR actions to show `Blocked on Story 12.5 EventStore Parties client contract`; Story 12.5 is not present in this epics document.
   - Impact: Admin GDPR operations cannot be readiness-verified from this backlog alone because the enabling dependency lives outside the artifact set or uses stale numbering.
   - Recommendation: Add an explicit dependency record for Story 12.5, rename the blocker to the current story/dependency identifier, or add a prerequisite story in this backlog for the accepted client contract.

3. Phase scope is mixed across MVP, v1.1, and v1.2 stories.
   - Examples: Epic 6 is GDPR v1.1; Epic 7 and Epic 8 are frontend v1.2-facing; Epic 2 includes deferred search and temporal query preparation.
   - Impact: The backlog is implementation-ready as a roadmap, but not cleanly release-sliced for a single Phase 4 build wave.
   - Recommendation: Add release/phase metadata to epics and stories, and define which subset is entering implementation first.

### Minor Concerns

1. Some epic and story titles use technical implementation language even when the story body has user value.
   - Examples: `Searchable Tenant-Safe Read Models`, `Build Party Detail Projection`, `Build Tenant Party Index Projection`.
   - Recommendation: Consider renaming toward user outcomes, such as `Search and Retrieve Tenant-Safe Parties`, while preserving implementation details in acceptance criteria.

2. Story 6.10 uses non-standard requirement coverage wording.
   - Example: `Requirements covered: Supports FR53, FR55 and NFR11`.
   - Impact: Automated traceability can misclassify support references as primary coverage unless parsers are careful.
   - Recommendation: Use a consistent format such as `Requirements covered: NFR11; supports FR53, FR55`.

3. Architecture and UX references use mixed story numbering.
   - Example: Story 7.6 references Story 12.5 as a blocker, while current epics use 7.x/8.x numbering.
   - Recommendation: Normalize story identifiers before implementation handoff.

### Dependency Analysis

- No circular dependencies found inside the epics document.
- No Epic N depends on Epic N+1 for its own core value, except Epic 7's dependency on an external Story 12.5/client contract that is not represented in this artifact set.
- Within-epic sequencing is mostly sensible: setup before domain behavior, projections before queries, client/API before docs/samples, tool registration before MCP tool implementation, GDPR encryption before erasure operations, admin shell before admin actions.
- Database/entity creation timing is appropriate for this architecture: there is no broad upfront table-creation story; projection state and aggregate contracts are introduced where first needed.

### Best Practices Compliance Checklist

| Check | Result | Notes |
| --- | --- | --- |
| Epics deliver user value | Pass with minor title concerns | All epic descriptions state user/operator/developer outcomes. |
| Epics can function independently in sequence | Pass with one major external dependency | Epic 7's Story 12.5 blocker needs explicit tracking. |
| Stories are appropriately sized | Pass | Stories are narrow and acceptance criteria are concrete. |
| No forward dependencies | Major issue | External Story 12.5 blocker is unresolved in this artifact set. |
| Database/state created when needed | Pass | No broad upfront data-store buildout detected. |
| Clear acceptance criteria | Pass | All stories include Given/When/Then criteria. |
| Traceability to FRs maintained | Pass with phase warning | All FRs are mapped, but deferred coverage should be separated from implemented coverage. |

### Quality Assessment Summary

The epics are substantially implementation-ready, but the backlog needs phase and dependency cleanup before execution planning. The core MVP path through party lifecycle, projections, developer integration, MCP, and event-driven integration is well-structured. The risk is that the roadmap-level backlog currently presents deferred v1.1/v1.2 work as equally covered, and the admin GDPR surface points to an external or stale Story 12.5 dependency.

## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK

The implementation artifacts are strong, but they are not yet clean enough for unambiguous Phase 4 execution. The core MVP path appears ready in substance. The full backlog needs phase slicing, dependency cleanup, and artifact freshness fixes before implementation starts.

### Critical Issues Requiring Immediate Action

No critical coverage failures were found. All 74 PRD functional requirements are represented in the epics document, and no whole-vs-sharded document conflicts were found.

The following major issues should be treated as blockers for implementation planning:

1. Separate implemented coverage from deferred/preparatory coverage.
   - Story 2.9 maps FR16 and FR72 even though semantic search and temporal query are deferred to v1.1.
   - Future-facing stories and placeholder stories should not look equivalent to implementation-complete MVP stories.

2. Resolve the external Story 12.5 dependency.
   - UX-DR11 and Story 7.6 refer to `Blocked on Story 12.5 EventStore Parties client contract`, but Story 12.5 is not in the current epics document.
   - Either add the dependency to the plan, rename it to the current dependency identifier, or add a prerequisite story in this backlog.

3. Define the implementation wave scope.
   - The backlog mixes MVP, v1.1 GDPR, and v1.2 frontend stories.
   - Without phase metadata, teams may start future-phase stories before the MVP foundation is complete.

4. Refresh architecture traceability.
   - Architecture still says 54 FRs in places while the PRD and epics contain 74 FRs.
   - This creates unnecessary uncertainty even though the epics cover all current FRs.

### Recommended Next Steps

1. Add `phase` metadata to every epic/story: `MVP`, `v1.1`, `v1.2`, or `future`.
2. Split the coverage map into `implemented`, `prepared`, and `deferred` coverage, especially for FR16, FR72, GDPR stories, and frontend stories.
3. Resolve the Story 12.5 blocker by adding an explicit dependency artifact or updating the admin UX/story text to the current dependency identifier.
4. Update `architecture.md` so its FR/NFR counts and requirements coverage summary match the current PRD and epics.
5. Add a dedicated party picker architecture subsection covering durable selection, stale response suppression, host auth context, and accepted query boundary behavior.
6. Normalize minor traceability wording, especially Story 6.10's `Requirements covered` format.
7. After those changes, rerun this readiness check before assigning implementation stories.

### Issue Count

This assessment identified 7 issues across 4 categories:

- 0 critical coverage failures
- 3 major planning/readiness issues
- 3 minor quality/traceability concerns
- 1 architecture freshness issue

### Final Note

The artifacts are close. The product thinking is coherent, the FR coverage is complete, and the story acceptance criteria are unusually thorough. The remaining work is not inventing missing requirements; it is making the implementation boundary crisp enough that engineers can pick up stories without accidentally crossing phase lines or chasing an unnamed external dependency.

**Assessment Date:** 2026-05-15
**Assessor:** Codex using `bmad-check-implementation-readiness`
