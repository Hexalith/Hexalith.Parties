---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
includedFiles:
  prd:
    primary: D:/Hexalith.Parties/_bmad-output/planning-artifacts/prd.md
    supporting:
      - D:/Hexalith.Parties/_bmad-output/planning-artifacts/prd-validation-report.md
  architecture:
    primary: D:/Hexalith.Parties/_bmad-output/planning-artifacts/architecture.md
  epics:
    primary: D:/Hexalith.Parties/_bmad-output/planning-artifacts/epics.md
  ux:
    primary:
      - D:/Hexalith.Parties/_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md
      - D:/Hexalith.Parties/_bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-17
**Project:** Hexalith.Parties

## Document Inventory

### PRD Files

**Whole Documents:**

- `prd.md` (83,158 bytes, modified 2026-05-14 10:40:21) - selected as primary PRD.
- `prd-validation-report.md` (26,454 bytes, modified 2026-03-02 16:05:46) - retained as supporting validation evidence.

**Sharded Documents:**

- None found.

### Architecture Files

**Whole Documents:**

- `architecture.md` (89,222 bytes, modified 2026-05-15 08:23:13) - selected as primary architecture.

**Sharded Documents:**

- None found.

### Epics & Stories Files

**Whole Documents:**

- `epics.md` (164,613 bytes, modified 2026-05-17 09:55:55) - selected as primary epics and stories source.

**Sharded Documents:**

- None found.

### UX Design Files

**Whole Documents:**

- `ux-admin-portal-2026-05-10.md` (7,488 bytes, modified 2026-05-15 08:22:58) - selected as UX source.
- `ux-party-picker-2026-05-12.md` (1,997 bytes, modified 2026-05-12 21:27:01) - selected as UX source.

**Sharded Documents:**

- None found.

### Discovery Issues

- No whole-versus-sharded duplicate conflicts found.
- No required document type appears to be missing.
- PRD search matched both the PRD and a validation report; the validation report is included only as supporting evidence.

## PRD Analysis

### Functional Requirements

FR1: Authorized client can create a new party as either a person or an organization with type-specific details

FR2: Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix)

FR3: Authorized client can update organization-specific details (legal name, trading name, legal form, registration number)

FR4: Authorized client can deactivate a party (soft lifecycle management)

FR5: Authorized client can reactivate a previously deactivated party

FR6: System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation — `"{FirstName} {LastName}"` for persons, `"{LegalName}"` for organizations; locale-aware formatting deferred to v1.1)

FR7: Each party has a client-generated, immutable UUID as its stable identity

FR8: Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social)

FR9: Authorized client can update an existing contact channel on a party

FR10: Authorized client can remove a contact channel from a party

FR11: Authorized client can mark a contact channel as preferred for its type

FR12: Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references)

FR13: Authorized client can remove an identifier from a party

FR14: Consumer can list parties with pagination and filtering by type (person/organization) and active status

FR15: Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields.

FR16: (Deferred to v1.1) Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1.

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

FR30: System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action — enabling developers to resolve the issue without consulting documentation or debugging the service

FR31: Developer can deploy a running instance from source with standard container tooling

FR32: Getting-started documentation enables a developer to deploy and send their first command as a self-service experience

FR33: Contract types package has zero runtime dependencies beyond netstandard2.1 — consuming applications inherit no infrastructure stack

FR34: System publishes domain events when party state changes

FR35: Consuming application can subscribe to party events and build domain-specific read models

FR36: System handles duplicate commands idempotently (safe deduplication in distributed scenarios)

FR37: Forward-compatible event contracts (including party merge) are available to consuming applications from day one

FR38: Consuming application documentation includes handler patterns for erasure and dangling reference cleanup, with explicit warning that `PartyErased` subscription is mandatory for all consuming apps regardless of which other events they handle

FR39: System isolates party data by tenant at all layers — no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering

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

FR54: Events published to subscribers contain readable data — subscribers never handle decryption

FR55: System returns an "erased" status for erased parties, not cryptographic errors

FR56: System publishes auto-generated API specification documentation accessible to developers

FR57: System supports versioned API endpoints that coexist during deprecation periods

FR58: System maps domain rejections to standardized HTTP error formats with a documented error catalog

FR59: System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage

FR60: Developer can run the full system locally with a single command for development and evaluation

FR61: System provides deployment validation tooling to verify security configuration before production use

FR62: System displays a non-dismissable compliance warning until GDPR features are activated

FR63: System guarantees at-least-once event delivery to subscribers

FR64: System degrades gracefully when infrastructure components are unavailable — read operations continue when write-side components fail

FR65: Administrator can browse, search, and inspect party records via an administration interface

FR66: Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface

FR67: Consuming application developer can embed a party picker component in their UI for party search and selection

FR68: Consumer can filter parties by creation date or last-modified date range

FR69: Update operations (API and MCP) return the updated party state in the response, not just a confirmation

FR70: Published domain events include tenant context for consuming application routing decisions

FR71: System exposes health and readiness signals for infrastructure orchestration

FR72: (Deferred to v1.1) Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit.

FR73: System delivers events for a single aggregate in causal order to each subscriber. Architecture must verify DAPR pub/sub ordering guarantees; if per-aggregate ordering cannot be guaranteed, document required handler design (order-tolerant or sequence-checking) in the architecture document.

FR74: MCP update operations use patch semantics — only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update

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

NFR9: Tenant isolation enforced at all layers — zero cross-tenant data leakage under any condition

NFR10: JWT token validation on every request; fail-closed on invalid or missing tokens

NFR11: Per-tenant encryption keys can be rotated without service downtime or data loss

NFR12: Personal data excluded from all application logs

NFR13: All API endpoints require authentication — no anonymous access

NFR14: System supports multi-tenant operation (no per-tenant infrastructure, stateless routing, partitionable metadata) validated at 100 concurrent tenants for MVP

NFR14a: System architecture supports scaling beyond 100 tenants without per-tenant infrastructure changes

NFR15: Tenant metadata operations (routing, key lookup) complete in < 50ms regardless of total tenant count

NFR16: System supports up to 100,000 parties per tenant (MVP validation target — sufficient for startups and SMBs; enterprise scale at millions of parties addressed in v2 via Elasticsearch projection and horizontal scaling)

NFR17: System sustains 100 read requests/second and 20 write requests/second per tenant

NFR18: Event store performance degrades < 10% at 100K parties per tenant with snapshot strategy active

NFR19: Read projections remain responsive (< 500ms) at 100K parties per tenant

NFR20: Service recovers from crash, replays necessary event state, and accepts requests within 30 seconds of restart

NFR21: When event store is unreachable, read projection queries continue serving cached data with a staleness indicator

NFR22: No data loss on service restart — event store is the durable source of truth

NFR23: At-least-once event delivery to subscribers via DAPR pub/sub

NFR24: Idempotent command handling ensures safe retry without duplicate side effects

NFR25: REST API conforms to auto-generated OpenAPI 3.x specification

NFR26: MCP server implements MCP protocol specification with 5 tools

NFR27: Published events follow stable, versioned contract schemas (append-only, additive changes only)

NFR28: Client NuGet packages impose < 10 transitive dependencies totalling < 5 MB (Contracts: zero runtime dependencies beyond netstandard2.1)

NFR29: Service has zero direct dependencies on specific state store or message broker implementations

NFR30: A developer deploys a running instance from source in < 15 minutes on first attempt using the documented getting-started guide

NFR31: NuGet client package size < 5MB with < 10 transitive dependencies

NFR32: (v1.2) Frontend applies output encoding to all party data fields rendered in the admin portal — no stored XSS from user-supplied or AI-created party data

Total NFRs: 33

### Additional Requirements

- MVP explicitly excludes GDPR compliance features and regulated EU personal data use until v1.1; a non-dismissable warning must persist in the admin UI header and API response headers until GDPR features are active.
- MVP explicitly excludes duplicate detection; `find_parties` metadata is advisory and consuming apps/operators remain responsible for deduplication until v2.
- MVP includes REST command API, read projections, 5 MCP tools, NuGet Contracts and Client packages, EventStore-provided JWT auth/multi-tenancy/event publishing/idempotency/convention discovery/snapshots, and documentation including a GDPR disclaimer and emergency manual erasure procedure.
- MVP hard gates are deploy in < 15 minutes, first `CreateParty` in < 30 minutes with comprehension check, single-prompt MCP creation returning full party, and self-service documentation.
- v1.1 growth requirements include crypto-shredding, consent, portability, restriction, erasure verification, processing records, locale-aware formatting, semantic search, temporal name query API, GDPR dashboard, and production EU operational observability.
- v1.2 growth requirements include FrontComposer-based admin portal, embeddable party picker, and reusable components for list/detail/form/contact channel/consent workflows.
- v2/future requirements include duplicate detection, party merge, advanced search via Elasticsearch/OpenSearch projection, party relationships, cross-tenant party sharing, bulk import, self-service portal, address validation extension points, and horizontal scale beyond 100K parties per tenant.
- Compliance requirements include GDPR Articles 17, 6, 20, 18, and 30; erasure propagation and verification; metadata survival after crypto-shredding; explicit cache invalidation; search index purge; DPIA support; and DPA template documentation.
- Security requirements include field-level encryption, tenant filtering, JWT claim extraction, event encryption/decryption, erasure verification, input validation, log sanitization, DAPR access control, deployment validation, key versioning, key access audit trail, backup/restore guidance, and fail-closed tenant isolation.
- Technical constraints include atomic event persistence, append-only event contracts, no event upcasting, tolerant deserialization, Party aggregate size testing up to 50 contact channels, checksum-verified snapshots, actor-serialized commands, preserved name history, `[PersonalData]` coverage scanning, encryption key caching strategy, and graceful degradation for DAPR component outages.
- Integration requirements include DAPR pub/sub event publishing, at-least-once delivery with idempotent consuming handlers, forward-compatible `PartyMerged`, dangling-reference guidance, infrastructure portability through DAPR, Aspire + Docker local development, deployment validation scripts, URL-path API versioning, RFC 9457 Problem Details, and a runnable sample integration.
- Project constraints include scope-fixed/timeline-flexible MVP execution, EventStore infrastructure effort tracking with a 60% escalation trigger, preference to fix EventStore gaps rather than work around them in Parties, REST as the only guaranteed MVP API transport, and no rate limiting in Parties domain logic.

### PRD Completeness Assessment

The PRD is unusually complete for implementation planning: it defines explicit FR/NFR identifiers, phases, user journeys, success gates, architecture-sensitive constraints, integration surfaces, compliance obligations, and validation expectations. Main readiness risks to validate in later steps are not missing PRD intent; they are traceability and feasibility risks: whether epics cover all 74 FRs and 33 NFRs, whether deferred scope is consistently reflected in stories, whether architecture resolves ordering/encryption/projection and actor-host boundary questions, and whether MVP stories avoid accidentally pulling v1.1/v1.2/v2 work into the launch path.

## Epic Coverage Validation

### Epic FR Coverage Extracted

- FR1-FR13, FR36, FR42, FR43, FR69: Epic 1 - Party Records and Lifecycle
- FR14-FR19, FR39-FR41, FR64, FR68, FR71, FR72: Epic 2 - Tenant-Safe Party Search and Retrieval
- FR26-FR33, FR56-FR62: Epic 3 - Developer Integration and Local Adoption
- FR20-FR25, FR74: Epic 4 - AI Agent Party Management
- FR34, FR35, FR37, FR38, FR63, FR70, FR73: Epic 5 - Event-Driven Consumer Integration
- FR44-FR55: Epic 6 - GDPR Compliance Operations
- FR65, FR66: Epic 7 - Administration Console
- FR67: Epic 8 - Embeddable Party Picker

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --------- | --------------- | ------------- | ------ |
| FR1 | Authorized client can create a new party as either a person or an organization with type-specific details | Epic 1 - Party Records and Lifecycle | Covered |
| FR2 | Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix) | Epic 1 - Party Records and Lifecycle | Covered |
| FR3 | Authorized client can update organization-specific details (legal name, trading name, legal form, registration number) | Epic 1 - Party Records and Lifecycle | Covered |
| FR4 | Authorized client can deactivate a party (soft lifecycle management) | Epic 1 - Party Records and Lifecycle | Covered |
| FR5 | Authorized client can reactivate a previously deactivated party | Epic 1 - Party Records and Lifecycle | Covered |
| FR6 | System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation - `"{FirstName} {LastName}"` for persons, `"{LegalName}"` for organizations; locale-aware formatting deferred to v1.1) | Epic 1 - Party Records and Lifecycle | Covered |
| FR7 | Each party has a client-generated, immutable UUID as its stable identity | Epic 1 - Party Records and Lifecycle | Covered |
| FR8 | Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social) | Epic 1 - Party Records and Lifecycle | Covered |
| FR9 | Authorized client can update an existing contact channel on a party | Epic 1 - Party Records and Lifecycle | Covered |
| FR10 | Authorized client can remove a contact channel from a party | Epic 1 - Party Records and Lifecycle | Covered |
| FR11 | Authorized client can mark a contact channel as preferred for its type | Epic 1 - Party Records and Lifecycle | Covered |
| FR12 | Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references) | Epic 1 - Party Records and Lifecycle | Covered |
| FR13 | Authorized client can remove an identifier from a party | Epic 1 - Party Records and Lifecycle | Covered |
| FR14 | Consumer can list parties with pagination and filtering by type (person/organization) and active status | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR15 | Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields. | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR16 | (Deferred to v1.1) Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1. | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR17 | Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans. MVP emits `displayName`; `email` and `identifier` are reserved for the future search model. | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR18 | Consumer can retrieve full party details by ID | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR19 | Recently created or updated parties become discoverable in search results within the eventual consistency window defined by NFR6 | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR20 | AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP. Email and identifier resolution require candidate retrieval or the future dedicated search capability. | Epic 4 - AI Agent Party Management | Covered |
| FR21 | AI agent can create a complete party (type details + contact channels + identifiers) in a single composite operation | Epic 4 - AI Agent Party Management | Covered |
| FR22 | AI agent can update party details, add/modify/remove contact channels and identifiers via a single operation | Epic 4 - AI Agent Party Management | Covered |
| FR23 | AI agent can retrieve full party details and list parties via dedicated AI-optimized tools | Epic 4 - AI Agent Party Management | Covered |
| FR24 | AI agent party creation returns the complete created party record, not just an identifier | Epic 4 - AI Agent Party Management | Covered |
| FR25 | AI agent tools accept partial and incomplete input gracefully, with documented default behaviors for omitted fields, and clear validation error messages when required fields are missing | Epic 4 - AI Agent Party Management | Covered |
| FR26 | .NET developer can integrate party management via a single package and one-line dependency registration | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR27 | Developer can send party commands via typed client abstractions without infrastructure knowledge | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR28 | Developer can query parties via typed client abstractions without infrastructure knowledge | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR29 | Developer can interact with the party service via REST API from any programming language | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR30 | System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action - enabling developers to resolve the issue without consulting documentation or debugging the service | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR31 | Developer can deploy a running instance from source with standard container tooling | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR32 | Getting-started documentation enables a developer to deploy and send their first command as a self-service experience | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR33 | Contract types package has zero runtime dependencies beyond netstandard2.1 - consuming applications inherit no infrastructure stack | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR34 | System publishes domain events when party state changes | Epic 5 - Event-Driven Consumer Integration | Covered |
| FR35 | Consuming application can subscribe to party events and build domain-specific read models | Epic 5 - Event-Driven Consumer Integration | Covered |
| FR36 | System handles duplicate commands idempotently (safe deduplication in distributed scenarios) | Epic 1 - Party Records and Lifecycle | Covered |
| FR37 | Forward-compatible event contracts (including party merge) are available to consuming applications from day one | Epic 5 - Event-Driven Consumer Integration | Covered |
| FR38 | Consuming application documentation includes handler patterns for erasure and dangling reference cleanup, with explicit warning that `PartyErased` subscription is mandatory for all consuming apps regardless of which other events they handle | Epic 5 - Event-Driven Consumer Integration | Covered |
| FR39 | System isolates party data by tenant at all layers - no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR40 | System identifies tenant from authenticated credentials, never from request payloads | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR41 | System rejects requests without valid tenant identity (fail-closed) | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR42 | Personal data fields are architecturally marked for automated privacy enforcement without domain code changes | Epic 1 - Party Records and Lifecycle | Covered |
| FR43 | Personal data fields are excluded from all application logging | Epic 1 - Party Records and Lifecycle | Covered |
| FR44 | Administrator can trigger right-to-erasure, rendering all personal data for a party permanently unreadable | Epic 6 - GDPR Compliance Operations | Covered |
| FR45 | System verifies erasure completion across all internal data stores and reports results | Epic 6 - GDPR Compliance Operations | Covered |
| FR46 | System notifies all subscribers when a party is erased so they can clean up their references | Epic 6 - GDPR Compliance Operations | Covered |
| FR47 | Administrator can record per-channel, per-purpose consent for a specific party | Epic 6 - GDPR Compliance Operations | Covered |
| FR48 | Administrator can revoke previously recorded consent | Epic 6 - GDPR Compliance Operations | Covered |
| FR49 | Administrator can restrict processing of a party's data (freeze while complaint is investigated) | Epic 6 - GDPR Compliance Operations | Covered |
| FR50 | Administrator can lift restriction on a party's data to resume processing | Epic 6 - GDPR Compliance Operations | Covered |
| FR51 | Administrator can export all data for a specific party in a machine-readable format | Epic 6 - GDPR Compliance Operations | Covered |
| FR52 | System maintains a complete, time-stamped record of all processing activities on party data | Epic 6 - GDPR Compliance Operations | Covered |
| FR53 | System encrypts personal data in stored events and snapshots using per-party keys | Epic 6 - GDPR Compliance Operations | Covered |
| FR54 | Events published to subscribers contain readable data - subscribers never handle decryption | Epic 6 - GDPR Compliance Operations | Covered |
| FR55 | System returns an "erased" status for erased parties, not cryptographic errors | Epic 6 - GDPR Compliance Operations | Covered |
| FR56 | System publishes auto-generated API specification documentation accessible to developers | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR57 | System supports versioned API endpoints that coexist during deprecation periods | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR58 | System maps domain rejections to standardized HTTP error formats with a documented error catalog | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR59 | System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR60 | Developer can run the full system locally with a single command for development and evaluation | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR61 | System provides deployment validation tooling to verify security configuration before production use | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR62 | System displays a non-dismissable compliance warning until GDPR features are activated | Epic 3 - Developer Integration and Local Adoption | Covered |
| FR63 | System guarantees at-least-once event delivery to subscribers | Epic 5 - Event-Driven Consumer Integration | Covered |
| FR64 | System degrades gracefully when infrastructure components are unavailable - read operations continue when write-side components fail | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR65 | Administrator can browse, search, and inspect party records via an administration interface | Epic 7 - Administration Console | Covered |
| FR66 | Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface | Epic 7 - Administration Console | Covered |
| FR67 | Consuming application developer can embed a party picker component in their UI for party search and selection | Epic 8 - Embeddable Party Picker | Covered |
| FR68 | Consumer can filter parties by creation date or last-modified date range | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR69 | Update operations (API and MCP) return the updated party state in the response, not just a confirmation | Epic 1 - Party Records and Lifecycle | Covered |
| FR70 | Published domain events include tenant context for consuming application routing decisions | Epic 5 - Event-Driven Consumer Integration | Covered |
| FR71 | System exposes health and readiness signals for infrastructure orchestration | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR72 | (Deferred to v1.1) Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit. | Epic 2 - Tenant-Safe Party Search and Retrieval | Covered |
| FR73 | System delivers events for a single aggregate in causal order to each subscriber | Epic 5 - Event-Driven Consumer Integration | Covered |
| FR74 | MCP update operations use patch semantics - only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update | Epic 4 - AI Agent Party Management | Covered |

### Missing Requirements

No missing FR coverage was found.

No FRs were found in the epics coverage map that do not exist in the PRD.

### Coverage Statistics

- Total PRD FRs: 74
- FRs covered in epics: 74
- FRs missing from epics: 0
- Extra FRs in epics not present in PRD: 0
- Coverage percentage: 100%

### Coverage Notes

- FR16 and FR72 are mapped to Epic 2 as deferred/preparation work, matching their PRD v1.1 scope rather than MVP implementation.
- FR44-FR55 are mapped to Epic 6, which correctly isolates GDPR implementation from MVP stories.
- FR65-FR67 are mapped to Epics 7 and 8, which correctly isolates v1.2 frontend/admin/picker work from MVP backend delivery.
- FR73 is covered by Epic 5, but the PRD explicitly requires architecture verification of DAPR pub/sub ordering; later architecture alignment should confirm whether the story requires order-tolerant subscriber guidance rather than an absolute platform guarantee.

## UX Alignment Assessment

### UX Document Status

Found.

- `ux-admin-portal-2026-05-10.md`
- `ux-party-picker-2026-05-12.md`

### UX to PRD Alignment

- Admin portal UX aligns with PRD FR65 and FR66: browse/search/inspect party records, GDPR operation surfaces, and operational states are explicitly covered.
- Party picker UX aligns with PRD FR67: embeddable search and selection component with durable party-id selection contract.
- UX requirements are also represented in the epics document as UX-DR1 through UX-DR32, giving traceability from UX documents into stories.
- The UX documents correctly preserve phase boundaries: admin portal and picker are v1.2 surfaces; GDPR operation panels are gated on the accepted EventStore-fronted Parties client/gateway contract.
- Privacy, accessibility, localization, stale-response, tenant-switch, forbidden, erased/gone, and degraded-state behavior are explicitly reflected in UX-DR requirements and supporting stories.

### UX to Architecture Alignment

- Architecture D20 supports the admin portal as a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor.
- Architecture explicitly routes admin reads through EventStore query/client abstractions and commands through the typed Parties client/EventStore command boundary, matching the admin UX contract.
- Architecture delegates generic stream/event/correlation/command-status inspection to EventStore Admin UI safe deep-links, matching the admin UX route map and privacy constraints.
- Architecture includes a dedicated Party Picker Frontend Surface section matching the picker UX addendum: party id as durable selection, EventStore-fronted client/gateway boundary, no direct actor/controller/internal calls, stale response suppression, privacy-safe failure states, and no token persistence/parsing/logging.
- Architecture coverage summary marks Admin FR65-FR67 as 3/3 covered and v1.2 deferred, which matches the PRD and UX phase model.

### Alignment Issues

- Minor stale reference: the admin UX document says the contract-unavailable state should show the exact `Story 12.4/12.5 blocker`, but the current epics use Epic 7 stories for admin console work. This should be updated to reference the accepted blocker wording without stale story numbers, or to the current Epic 7 story IDs.

### Warnings

- UX production readiness depends on the accepted EventStore-fronted Parties client/gateway contract. Stories correctly gate GDPR/admin operations, but implementation should preserve the disabled fail-closed state until that contract exists.
- Admin and picker privacy rules are strict enough to require targeted tests: no personal data in URLs, storage keys, telemetry dimensions, logs, filenames, raw ProblemDetails rendering, DOM event names, or JavaScript payloads.
- Accessibility and localization are contractual in both UX and architecture; story acceptance should include keyboard, focus, status announcement, non-color-only state, and localized text checks rather than treating them as polish.

## Epic Quality Review

### Overall Structure

The epic set is largely implementation-ready from a planning-structure perspective. Epics are framed around actors and outcomes: authorized clients, consumers, developers, AI agents, consuming application developers, administrators/DPOs, and embedding application developers. Acceptance criteria are consistently written in Given/When/Then form and are mostly testable.

### Best Practices Compliance By Epic

| Epic | User Value Focus | Independence | Story Sizing | AC Quality | Traceability | Notes |
| ---- | ---------------- | ------------ | ------------ | ---------- | ------------ | ----- |
| Epic 1 - Party Records and Lifecycle | Pass | Pass | Pass with allowed setup exception | Pass | Pass | Story 1.1 is technical setup, but the architecture specifies a starter-structure requirement, so this is an allowed first story. |
| Epic 2 - Tenant-Safe Party Search and Retrieval | Pass | Pass after Epic 1 | Pass | Pass | Pass | Story 2.9 is preparation-only and must remain scoped to extension points, not runtime semantic search. |
| Epic 3 - Developer Integration and Local Adoption | Pass | Pass after domain/query foundations | Pass | Pass | Pass | Developer-facing value is clear: packages, REST, docs, samples, deployment validation. |
| Epic 4 - AI Agent Party Management | Pass | Pass after domain/query foundations | Pass | Pass | Pass | MCP stays bounded and shares domain/query paths. |
| Epic 5 - Event-Driven Consumer Integration | Pass with wording issue | Pass | Pass | Pass | Pass | Epic summary says "ordered" events, but architecture says ordering is broker-dependent. |
| Epic 6 - GDPR Compliance Operations | Pass | Pass after MVP foundations | Watch | Pass | Pass | v1.1 stories are coherent but high-complexity; split/confirm before v1.1 sprint scheduling. |
| Epic 7 - Administration Console | Pass | External dependency needs tracking | Pass | Pass | Pass | Story 7.6 depends on accepted EventStore-fronted Parties client/gateway contract. |
| Epic 8 - Embeddable Party Picker | Pass | External dependency needs tracking | Pass | Pass | Pass | Depends on accepted Parties client/EventStore gateway boundary. |

### Critical Violations

None found.

No epic is a pure technical milestone. No explicit forward dependency on a later story was found. No epic-sized MVP story was found that would block immediate MVP sequencing.

### Major Issues

1. Epic 5 overpromises event ordering in its value statement.
   - Evidence: Epic 5 says consuming applications can receive "ordered, tenant-aware party events"; PRD FR73 wants per-aggregate causal order; architecture states ordering is broker-dependent and must be verified per deployment target.
   - Impact: A team could implement or document a stronger guarantee than DAPR/EventStore can provide for a given broker.
   - Recommendation: Reword Epic 5 to "documented ordering behavior, tenant-aware events, and idempotent subscriber guidance" unless the supported deployment target can prove per-aggregate ordering. Keep Story 5.4 as the enforcement point.

2. v1.1 GDPR stories are high-complexity and should be reconfirmed before scheduling.
   - Evidence: Story 6.1 spans event encryption, snapshot encryption, type-dependent classification, missing-key behavior, replay compatibility, and broad test coverage. Story 6.3 spans aggregate state, snapshots, detail projections, index projections, caches, search indexes, retries, and DPO reporting. Story 6.10 spans tenant key material, party-key wrapping, partial retries, erased parties, concurrent reads, and redacted status.
   - Impact: These may be independently valuable, but each has enough platform/security blast radius to exceed normal story size once implementation details are known.
   - Recommendation: Before v1.1 implementation, run story refinement and split any story whose implementation cannot be completed and tested in one sprint-sized unit. Likely split points are EventStore encryption primitives, Parties contract markers, snapshot integration, projection cleanup verification, status/reporting, and key-rotation orchestration.

3. Admin and picker stories rely on an external accepted client/gateway contract.
   - Evidence: Story 7.6 explicitly depends on the accepted EventStore-fronted Parties client/gateway contract; picker stories require accepted Parties client/EventStore gateway configuration. A dependency record exists at `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.
   - Impact: These stories are not schedulable until the dependency record is updated to `Satisfied` or `Risk Accepted` and linked from sprint/story planning.
   - Recommendation: Link and maintain the existing dependency record before scheduling Story 7.6, Story 7.7, and Epic 8 stories. Keep fail-closed contract-unavailable behavior implemented first.

### Minor Concerns

1. Preparation-only stories need scheduling discipline.
   - Evidence: Story 2.9, Story 5.6, and Story 5.7 are labeled non-feature preparation / prepared-deferred.
   - Impact: These are acceptable because they protect future compatibility, but they can become technical filler if not tied to concrete artifacts.
   - Recommendation: Keep acceptance criteria artifact-based: contract extension points, documented reserved fields, compatibility tests, consumer guidance, and no active runtime behavior for deferred features.

2. The admin UX document has stale story-number wording.
   - Evidence: It references the "Story 12.4/12.5 blocker" while current epics use Epic 7 story IDs.
   - Impact: Minor implementation confusion during v1.2 planning.
   - Recommendation: Replace the stale story-number reference with the bounded blocker text or current Epic 7 story references.

3. Story 1.1 is technical but justified.
   - Evidence: Story 1.1 sets up solution structure, build conventions, and boundaries.
   - Impact: Normally this would be a red flag, but the skill's starter-template rule and architecture require an initial setup story.
   - Recommendation: Keep Story 1.1 as the only technical setup story and preserve its validation around boundaries, buildability, and no recursive nested submodule initialization.

### Dependency Findings

- Epic sequencing is coherent: Epic 2 consumes Epic 1 domain output; Epic 3 exposes integration surfaces; Epic 4 uses domain/query foundations; Epic 5 publishes/consumes event contracts; Epic 6 starts after MVP privacy foundations; Epics 7 and 8 are deferred v1.2 surfaces.
- No circular dependency was found.
- No explicit forward dependency like "depends on future Story X.Y" was found.
- Existing external dependency tracking must be linked and updated for the accepted EventStore-fronted Parties client/gateway contract before scheduling v1.2 admin/picker work.

### Database / Entity Creation Timing

No relational database/table upfront creation issue was found. The plan uses EventStore aggregates, DAPR actor-managed projections, and contracts created when the corresponding story needs them. Story 1.1 establishes boundaries, but does not create all domain behavior upfront.

### Starter Template / Greenfield Check

The architecture specifies the EventStore solution-structure pattern as the starter. Epic 1 Story 1 correctly sets up the initial solution from that pattern and validates package boundaries, build configuration, project layout, and non-recursive submodule expectations.

### Quality Review Conclusion

The epics are structurally fit for MVP implementation planning after minor wording cleanup and with strict scheduling discipline for preparation/deferred work. The main readiness risks are not missing coverage or malformed stories; they are ensuring broker-dependent ordering is not overpromised, v1.1 security/privacy work is split when scheduled, and v1.2 UI work is blocked until its external client/gateway contract is accepted.

## Summary and Recommendations

### Overall Readiness Status

READY for MVP implementation planning.

NEEDS WORK before v1.1/v1.2 scheduling.

The MVP artifact set is complete enough to start implementation: required planning documents exist, all 74 PRD functional requirements are mapped to epics, UX is present for implied UI work, and the MVP stories are largely well-formed and traceable. Deferred GDPR and UI phases need targeted cleanup/refinement before those phases are scheduled.

### Critical Issues Requiring Immediate Action

No critical blockers were found.

### Issues Requiring Attention

Major issues:

1. Epic 5 overpromises event ordering in its value statement while architecture says ordering is broker-dependent.
2. v1.1 GDPR stories are high-complexity and should be split or reconfirmed before v1.1 implementation scheduling.
3. v1.2 admin and picker stories depend on an accepted EventStore-fronted Parties client/gateway contract; the existing dependency record must be linked and updated before scheduling.

Minor issues:

1. Preparation-only stories need strict scheduling discipline so they remain concrete contract/documentation/compatibility work.
2. Admin UX references stale Story 12.4/12.5 blocker wording while current epics use Epic 7 story IDs.
3. Story 1.1 is technical setup, but it is justified by the architecture starter-template requirement and should remain the only technical setup story.

### Recommended Next Steps

1. Reword Epic 5 to avoid claiming unconditional ordered event delivery; align it with broker-dependent ordering plus required subscriber idempotency/order-tolerance guidance.
2. Update the admin UX stale Story 12.4/12.5 reference to the current bounded blocker text or Epic 7 story references.
3. Link and update `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` before scheduling Story 7.6, Story 7.7, or Epic 8.
4. Keep Story 2.9, Story 5.6, and Story 5.7 explicitly preparation-only and acceptance-artifact-driven.
5. Before v1.1, refine Epic 6 into sprint-sized security/privacy slices where implementation scope exceeds a single independently testable story.
6. During MVP implementation, preserve the current phase boundaries: do not pull v1.1 GDPR runtime behavior or v1.2 UI work into MVP stories.
7. Add explicit implementation checks for the highest-risk NFRs: tenant isolation, no personal data in logs/telemetry, projection freshness/degradation, at-least-once delivery/idempotency, MCP latency, and no public REST/MCP creep in the Parties actor host.

### Final Note

This assessment identified 6 issues across 3 categories: event-ordering guarantee wording, deferred-phase scheduling/refinement, and UX/dependency cleanup. None block MVP implementation planning. Address the Epic 5 wording and stale UX blocker reference before implementation kickoff so developers do not inherit avoidable ambiguity. Treat v1.1/v1.2 items as deliberate pre-scheduling refinement work, not MVP blockers.

**Assessment completed:** 2026-05-17

**Assessor:** Codex using `bmad-check-implementation-readiness`
