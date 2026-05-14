---
project: Hexalith.Parties
date: 2026-05-13
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
documentsIncluded:
  prd:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\prd.md
  architecture:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\architecture.md
  epics:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\epics.md
  ux:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\ux-admin-portal-2026-05-10.md
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\ux-party-picker-2026-05-12.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-13
**Project:** Hexalith.Parties

## Step 1: Document Discovery

### PRD Files Found

**Whole Documents:**
- `prd.md` (82,922 bytes, modified 2026-05-12 21:23)
- `prd-validation-report.md` (26,454 bytes, modified 2026-03-02 16:05) - validation report, not selected as primary PRD

**Sharded Documents:**
- None found

### Architecture Files Found

**Whole Documents:**
- `architecture.md` (87,886 bytes, modified 2026-05-12 21:26)

**Sharded Documents:**
- None found

### Epics & Stories Files Found

**Whole Documents:**
- `epics.md` (125,974 bytes, modified 2026-05-12 21:28)

**Sharded Documents:**
- None found

### UX Design Files Found

**Whole Documents:**
- `ux-admin-portal-2026-05-10.md` (7,391 bytes, modified 2026-05-10 13:27)
- `ux-party-picker-2026-05-12.md` (1,997 bytes, modified 2026-05-12 21:27)

**Sharded Documents:**
- None found

### Selected Assessment Documents

- PRD: `prd.md`
- Architecture: `architecture.md`
- Epics & Stories: `epics.md`
- UX: `ux-admin-portal-2026-05-10.md`, `ux-party-picker-2026-05-12.md`

### Issues

- No whole-vs-sharded duplicate formats found.
- No required document type is missing.

## PRD Analysis

### Functional Requirements

- **FR1:** Authorized client can create a new party as either a person or an organization with type-specific details
- **FR2:** Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix)
- **FR3:** Authorized client can update organization-specific details (legal name, trading name, legal form, registration number)
- **FR4:** Authorized client can deactivate a party (soft lifecycle management)
- **FR5:** Authorized client can reactivate a previously deactivated party
- **FR6:** System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation — `"{FirstName} {LastName}"` for persons, `"{LegalName}"` for organizations; locale-aware formatting deferred to v1.1)
- **FR7:** Each party has a client-generated, immutable UUID as its stable identity
- **FR8:** Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social)
- **FR9:** Authorized client can update an existing contact channel on a party
- **FR10:** Authorized client can remove a contact channel from a party
- **FR11:** Authorized client can mark a contact channel as preferred for its type
- **FR12:** Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references)
- **FR13:** Authorized client can remove an identifier from a party
- **FR14:** Consumer can list parties with pagination and filtering by type (person/organization) and active status
- **FR15:** Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields.
- **FR16:** *(Deferred to v1.1)* Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1.
- **FR17:** Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans. MVP emits `displayName`; `email` and `identifier` are reserved for the future search model.
- **FR18:** Consumer can retrieve full party details by ID
- **FR19:** Recently created or updated parties become discoverable in search results within the eventual consistency window defined by NFR6
- **FR20:** AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP. Email and identifier resolution require candidate retrieval or the future dedicated search capability.
- **FR21:** AI agent can create a complete party (type details + contact channels + identifiers) in a single composite operation
- **FR22:** AI agent can update party details, add/modify/remove contact channels and identifiers via a single operation
- **FR23:** AI agent can retrieve full party details and list parties via dedicated AI-optimized tools
- **FR24:** AI agent party creation returns the complete created party record, not just an identifier
- **FR25:** AI agent tools accept partial and incomplete input gracefully, with documented default behaviors for omitted fields, and clear validation error messages when required fields are missing
- **FR26:** .NET developer can integrate party management via a single package and one-line dependency registration
- **FR27:** Developer can send party commands via typed client abstractions without infrastructure knowledge
- **FR28:** Developer can query parties via typed client abstractions without infrastructure knowledge
- **FR29:** Developer can interact with the party service via REST API from any programming language
- **FR30:** System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action — enabling developers to resolve the issue without consulting documentation or debugging the service
- **FR31:** Developer can deploy a running instance from source with standard container tooling
- **FR32:** Getting-started documentation enables a developer to deploy and send their first command as a self-service experience
- **FR33:** Contract types package has zero runtime dependencies beyond netstandard2.1 — consuming applications inherit no infrastructure stack
- **FR34:** System publishes domain events when party state changes
- **FR35:** Consuming application can subscribe to party events and build domain-specific read models
- **FR36:** System handles duplicate commands idempotently (safe deduplication in distributed scenarios)
- **FR37:** Forward-compatible event contracts (including party merge) are available to consuming applications from day one
- **FR38:** Consuming application documentation includes handler patterns for erasure and dangling reference cleanup, with explicit warning that `PartyErased` subscription is mandatory for all consuming apps regardless of which other events they handle
- **FR39:** System isolates party data by tenant at all layers — no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering
- **FR40:** System identifies tenant from authenticated credentials, never from request payloads
- **FR41:** System rejects requests without valid tenant identity (fail-closed)
- **FR42:** Personal data fields are architecturally marked for automated privacy enforcement without domain code changes
- **FR43:** Personal data fields are excluded from all application logging
- **FR44:** Administrator can trigger right-to-erasure, rendering all personal data for a party permanently unreadable
- **FR45:** System verifies erasure completion across all internal data stores and reports results
- **FR46:** System notifies all subscribers when a party is erased so they can clean up their references
- **FR47:** Administrator can record per-channel, per-purpose consent for a specific party
- **FR48:** Administrator can revoke previously recorded consent
- **FR49:** Administrator can restrict processing of a party's data (freeze while complaint is investigated)
- **FR50:** Administrator can lift restriction on a party's data to resume processing
- **FR51:** Administrator can export all data for a specific party in a machine-readable format
- **FR52:** System maintains a complete, time-stamped record of all processing activities on party data
- **FR53:** System encrypts personal data in stored events and snapshots using per-party keys
- **FR54:** Events published to subscribers contain readable data — subscribers never handle decryption
- **FR55:** System returns an "erased" status for erased parties, not cryptographic errors
- **FR56:** System publishes auto-generated API specification documentation accessible to developers
- **FR57:** System supports versioned API endpoints that coexist during deprecation periods
- **FR58:** System maps domain rejections to standardized HTTP error formats with a documented error catalog
- **FR59:** System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage
- **FR60:** Developer can run the full system locally with a single command for development and evaluation
- **FR61:** System provides deployment validation tooling to verify security configuration before production use
- **FR62:** System displays a non-dismissable compliance warning until GDPR features are activated
- **FR63:** System guarantees at-least-once event delivery to subscribers
- **FR64:** System degrades gracefully when infrastructure components are unavailable — read operations continue when write-side components fail
- **FR65:** Administrator can browse, search, and inspect party records via an administration interface
- **FR66:** Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface
- **FR67:** Consuming application developer can embed a party picker component in their UI for party search and selection
- **FR68:** Consumer can filter parties by creation date or last-modified date range
- **FR69:** Update operations (API and MCP) return the updated party state in the response, not just a confirmation
- **FR70:** Published domain events include tenant context for consuming application routing decisions
- **FR71:** System exposes health and readiness signals for infrastructure orchestration
- **FR72:** *(Deferred to v1.1)* Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit.
- **FR73:** System delivers events for a single aggregate in causal order to each subscriber
- **FR74:** MCP update operations use patch semantics — only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update

**Total FRs:** 74

### Non-Functional Requirements

- **NFR1:** Command processing (create, update, manage party) completes in < 1 second at NFR17 throughput levels; MCP tool calls complete in < 1 second end-to-end including transport
- **NFR2:** Query operations (search, get by ID, list) return results in < 500ms at NFR17 throughput levels
- **NFR3:** Aggregate rehydration completes in < 200ms with snapshot strategy active
- **NFR4:** Search across 100K parties per tenant returns results within 500ms
- **NFR5:** Service accepts requests within 30 seconds of container launch (cold start)
- **NFR6:** Read projections reflect write operations within 2 seconds at NFR17 throughput levels (eventual consistency window)
- **NFR7:** All data encrypted in transit (TLS 1.2+)
- **NFR8:** Personal data fields encrypted at rest using per-party keys (activated in v1.1)
- **NFR9:** Tenant isolation enforced at all layers — zero cross-tenant data leakage under any condition
- **NFR10:** JWT token validation on every request; fail-closed on invalid or missing tokens
- **NFR11:** Per-tenant encryption keys can be rotated without service downtime or data loss
- **NFR12:** Personal data excluded from all application logs
- **NFR13:** All API endpoints require authentication — no anonymous access
- **NFR14:** System supports multi-tenant operation (no per-tenant infrastructure, stateless routing, partitionable metadata) validated at 100 concurrent tenants for MVP
- **NFR14a:** System architecture supports scaling beyond 100 tenants without per-tenant infrastructure changes
- **NFR15:** Tenant metadata operations (routing, key lookup) complete in < 50ms regardless of total tenant count
- **NFR16:** System supports up to 100,000 parties per tenant (MVP validation target — sufficient for startups and SMBs; enterprise scale at millions of parties addressed in v2 via Elasticsearch projection and horizontal scaling)
- **NFR17:** System sustains 100 read requests/second and 20 write requests/second per tenant
- **NFR18:** Event store performance degrades < 10% at 100K parties per tenant with snapshot strategy active
- **NFR19:** Read projections remain responsive (< 500ms) at 100K parties per tenant
- **NFR20:** Service recovers from crash, replays necessary event state, and accepts requests within 30 seconds of restart
- **NFR21:** When event store is unreachable, read projection queries continue serving cached data with a staleness indicator
- **NFR22:** No data loss on service restart — event store is the durable source of truth
- **NFR23:** At-least-once event delivery to subscribers via DAPR pub/sub
- **NFR24:** Idempotent command handling ensures safe retry without duplicate side effects
- **NFR25:** REST API conforms to auto-generated OpenAPI 3.x specification
- **NFR26:** MCP server implements MCP protocol specification with 5 tools
- **NFR27:** Published events follow stable, versioned contract schemas (append-only, additive changes only)
- **NFR28:** Client NuGet packages impose < 10 transitive dependencies totalling < 5 MB (Contracts: zero runtime dependencies beyond netstandard2.1)
- **NFR29:** Service has zero direct dependencies on specific state store or message broker implementations
- **NFR30:** A developer deploys a running instance from source in < 15 minutes on first attempt using the documented getting-started guide
- **NFR31:** NuGet client package size < 5MB with < 10 transitive dependencies
- **NFR32:** (v1.2) Frontend applies output encoding to all party data fields rendered in the admin portal — no stored XSS from user-supplied or AI-created party data

**Total NFR entries:** 33

### Additional Requirements

- MVP must not be represented as GDPR-compliant; regulated EU personal data should not be stored until v1.1, and the warning remains non-dismissable in the admin UI header and API response headers.
- MVP does not include duplicate detection; `search_parties` match metadata is advisory, and consuming apps/operators remain responsible for deduplication until v2.
- GDPR domain requirements include right to erasure, processing purpose tracking, data portability, right to restriction, Article 30 processing records, erasure propagation, erasure verification scope, metadata preservation after erasure, erased-party read behavior, cache invalidation, search index purge, regulatory extensibility, DPIA notice, and DPA template support.
- Security requirements include field-level encryption, tenant filtering, JWT claim extraction, event encryption/decryption, erasure verification inside the service boundary, input validation, log sanitization, and operator-owned DAPR/infrastructure hardening.
- Threat model coverage must include tenant spoofing, cross-tenant pub/sub subscription, cross-tenant projection queries, secret-store key extraction, DAPR access misconfiguration, malicious MCP input, and personal-data log exposure.
- Technical constraints include atomic event persistence, append-only immutable event contracts, tolerant event deserialization without upcasting, single Party aggregate model, 50-contact-channel aggregate size guideline, checksum-verified snapshots, actor-serialized commands per aggregate, and name history preservation in the event stream.
- Encryption and crypto-shredding constraints include `[PersonalData]` attribute-driven encryption, decrypted pub/sub events, a decryption circuit breaker, snapshot participation in crypto-shredding, automated `[PersonalData]` coverage verification, and key-caching design for v1.1 performance compatibility.
- Graceful degradation behavior must be specified for DAPR secret store, state store, and pub/sub outages; pub/sub outage must not create event loss.
- Integration constraints include DAPR pub/sub event publishing, idempotent consumer handlers, forward-compatible `PartyMerged` event contract, dangling reference guidance, tolerant deserialization, DAPR infrastructure portability, Aspire + Docker local development, and deployment validation scripts.
- API/backend constraints include REST as the only guaranteed MVP API surface, URL-path versioning, JSON-only request/response format, OpenAPI 3.x, five MCP tools, EventStore-owned authentication/authorization, RFC 9457 Problem Details mapping, documented error catalog, no application-domain rate limiting, and lean NuGet packages.
- Sample integration must demonstrate `AddPartiesClient()`, commands, queries, event subscription/read model, MCP usage, and be runnable with `dotnet run` and CI-verifiable.
- Architecture must verify DAPR pub/sub ordering guarantees for FR73; if per-aggregate ordering cannot be guaranteed, handler design must be documented as order-tolerant or sequence-checking.

### PRD Completeness Assessment

The PRD is unusually complete and highly traceable: it contains explicit numbered FRs/NFRs, phase annotations, success gates, audience-specific success criteria, domain-specific regulatory/security constraints, API/developer experience requirements, and risk mitigations. The main readiness risk is not missing requirement detail; it is alignment complexity across MVP, v1.1, and v1.2 scope boundaries. Several deferred requirements are intentionally listed inside the FR/NFR set, so epic validation must preserve phase labels and avoid treating all 74 FRs as MVP work.

## Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document contains a dedicated `FR Coverage Map` with 74 unique FR mappings and declares `Coverage: 74/74 FRs mapped — zero gaps`.

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --------- | --------------- | ------------- | ------ |
| FR1 | Authorized client can create a new party as either a person or an organization with type-specific details | Epic 1 | Covered |
| FR2 | Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix) | Epic 1 | Covered |
| FR3 | Authorized client can update organization-specific details (legal name, trading name, legal form, registration number) | Epic 1 | Covered |
| FR4 | Authorized client can deactivate a party (soft lifecycle management) | Epic 1 | Covered |
| FR5 | Authorized client can reactivate a previously deactivated party | Epic 1 | Covered |
| FR6 | System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP simple concatenation; locale-aware deferred to v1.1) | Epic 1 | Covered |
| FR7 | Each party has a client-generated, immutable UUID as its stable identity | Epic 1 | Covered |
| FR8 | Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social) | Epic 2 | Covered |
| FR9 | Authorized client can update an existing contact channel on a party | Epic 2 | Covered |
| FR10 | Authorized client can remove a contact channel from a party | Epic 2 | Covered |
| FR11 | Authorized client can mark a contact channel as preferred for its type | Epic 2 | Covered |
| FR12 | Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references) | Epic 2 | Covered |
| FR13 | Authorized client can remove an identifier from a party | Epic 2 | Covered |
| FR14 | Consumer can list parties with pagination and filtering by type (person/organization) and active status | Epic 3 | Covered |
| FR15 | Consumer can search parties by display name in MVP; email and identifier search are deferred to the dedicated search capability | Epic 3 | Covered |
| FR16 | Deferred to v1.1: consumer can perform semantic search across parties | Epic 9 | Covered |
| FR17 | Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans | Epic 3 | Covered |
| FR18 | Consumer can retrieve full party details by ID | Epic 1 | Covered |
| FR19 | Recently created or updated parties become discoverable in search results within the eventual consistency window defined by NFR6 | Epic 3 | Covered |
| FR20 | AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP | Epic 5 | Covered |
| FR21 | AI agent can create a complete party (type details + contact channels + identifiers) in a single composite operation | Epic 4+5 | Covered |
| FR22 | AI agent can update party details, add/modify/remove contact channels and identifiers via a single operation | Epic 4+5 | Covered |
| FR23 | AI agent can retrieve full party details and list parties via dedicated AI-optimized tools | Epic 5 | Covered |
| FR24 | AI agent party creation returns the complete created party record, not just an identifier | Epic 5 | Covered |
| FR25 | AI agent tools accept partial and incomplete input gracefully, with documented default behaviors and clear validation error messages | Epic 5 | Covered |
| FR26 | .NET developer can integrate party management via a single package and one-line dependency registration | Epic 6 | Covered |
| FR27 | Developer can send party commands via typed client abstractions without infrastructure knowledge | Epic 6 | Covered |
| FR28 | Developer can query parties via typed client abstractions without infrastructure knowledge | Epic 6 | Covered |
| FR29 | Developer can interact with the party service via REST API from any programming language | Epic 1 | Covered |
| FR30 | System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action | Epic 1 | Covered |
| FR31 | Developer can deploy a running instance from source with standard container tooling | Epic 1 | Covered |
| FR32 | Getting-started documentation enables a developer to deploy and send their first command as a self-service experience | Epic 6 | Covered |
| FR33 | Contract types package has zero runtime dependencies beyond netstandard2.1 | Epic 1 | Covered |
| FR34 | System publishes domain events when party state changes | Epic 7 | Covered |
| FR35 | Consuming application can subscribe to party events and build domain-specific read models | Epic 7 | Covered |
| FR36 | System handles duplicate commands idempotently (safe deduplication in distributed scenarios) | Epic 1 | Covered |
| FR37 | Forward-compatible event contracts (including party merge) are available to consuming applications from day one | Epic 7 | Covered |
| FR38 | Consuming application documentation includes handler patterns for erasure and dangling reference cleanup | Epic 7 | Covered |
| FR39 | System isolates party data by tenant at all layers; all API surfaces carry tenant context and receive identical filtering | Epic 1 | Covered |
| FR40 | System identifies tenant from authenticated credentials, never from request payloads | Epic 1 | Covered |
| FR41 | System rejects requests without valid tenant identity (fail-closed) | Epic 1 | Covered |
| FR42 | Personal data fields are architecturally marked for automated privacy enforcement without domain code changes | Epic 1 | Covered |
| FR43 | Personal data fields are excluded from all application logging | Epic 1 | Covered |
| FR44 | Administrator can trigger right-to-erasure, rendering all personal data for a party permanently unreadable | Epic 9 | Covered |
| FR45 | System verifies erasure completion across all internal data stores and reports results | Epic 9 | Covered |
| FR46 | System notifies all subscribers when a party is erased so they can clean up their references | Epic 9 | Covered |
| FR47 | Administrator can record per-channel, per-purpose consent for a specific party | Epic 9 | Covered |
| FR48 | Administrator can revoke previously recorded consent | Epic 9 | Covered |
| FR49 | Administrator can restrict processing of a party's data (freeze while complaint is investigated) | Epic 9 | Covered |
| FR50 | Administrator can lift restriction on a party's data to resume processing | Epic 9 | Covered |
| FR51 | Administrator can export all data for a specific party in a machine-readable format | Epic 9 | Covered |
| FR52 | System maintains a complete, time-stamped record of all processing activities on party data | Epic 9 | Covered |
| FR53 | System encrypts personal data in stored events and snapshots using per-party keys | Epic 9 | Covered |
| FR54 | Events published to subscribers contain readable data; subscribers never handle decryption | Epic 9 | Covered |
| FR55 | System returns an erased status for erased parties, not cryptographic errors | Epic 9 | Covered |
| FR56 | System publishes auto-generated API specification documentation accessible to developers | Epic 3 | Covered |
| FR57 | System supports versioned API endpoints that coexist during deprecation periods | Epic 1 | Covered |
| FR58 | System maps domain rejections to standardized HTTP error formats with a documented error catalog | Epic 1 | Covered |
| FR59 | System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage | Epic 6 | Covered |
| FR60 | Developer can run the full system locally with a single command for development and evaluation | Epic 1 | Covered |
| FR61 | System provides deployment validation tooling to verify security configuration before production use | Epic 8 | Covered |
| FR62 | System displays a non-dismissable compliance warning until GDPR features are activated | Epic 1 | Covered |
| FR63 | System guarantees at-least-once event delivery to subscribers | Epic 7 | Covered |
| FR64 | System degrades gracefully when infrastructure components are unavailable; read operations continue when write-side components fail | Epic 8 | Covered |
| FR65 | Administrator can browse, search, and inspect party records via an administration interface | Epic 10 | Covered |
| FR66 | Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface | Epic 10 | Covered |
| FR67 | Consuming application developer can embed a party picker component in their UI for party search and selection | Epic 10 | Covered |
| FR68 | Consumer can filter parties by creation date or last-modified date range | Epic 3 | Covered |
| FR69 | Update operations (API and MCP) return the updated party state in the response, not just a confirmation | Epic 4 | Covered |
| FR70 | Published domain events include tenant context for consuming application routing decisions | Epic 7 | Covered |
| FR71 | System exposes health and readiness signals for infrastructure orchestration | Epic 8 | Covered |
| FR72 | Deferred to v1.1: consumer can query a party's historical name as it was at a specific point in time | Epic 9 | Covered |
| FR73 | System delivers events for a single aggregate in causal order to each subscriber | Epic 7 | Covered |
| FR74 | MCP update operations use patch semantics; unspecified fields remain unchanged | Epic 5 | Covered |

### Missing Requirements

No missing FR coverage found. No FRs appear in the epics coverage map that are absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 74
- FRs covered in epics: 74
- FRs missing from epics: 0
- Extra FRs in epics but not PRD: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found:

- `ux-admin-portal-2026-05-10.md`
- `ux-party-picker-2026-05-12.md`

### UX to PRD Alignment

- Admin portal UX aligns to PRD FR65 and FR66: browse/search/inspect party records and process GDPR operations through an administration interface.
- Party picker UX aligns to PRD FR67: embeddable party search and selection component for consuming application UIs.
- Admin portal privacy and encoding rules align to NFR32 and the PRD's personal-data/log-sanitization posture.
- Admin portal GDPR flows align to the Laurent/DPO journey and GDPR requirements FR44-FR55, with the UX correctly treating full GDPR operations as dependent on the accepted command/client contract.
- Party picker durable selection by party id aligns with the PRD's stable party identity model and privacy constraints; it avoids making display names, contact values, tenant ids, raw ProblemDetails, or tokens durable host artifacts.
- UX adds operational detail not fully enumerated in the PRD, including stale-response clearing, focus management, status announcements, localization, bounded error states, and safe EventStore Admin UI deep-links. These are refinements rather than conflicts.

### UX to Architecture Alignment

- Architecture D20 explicitly supports the admin portal as a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor.
- D20 supports the UX's EventStore-fronted boundary: Parties-domain views register with FrontComposer, read through EventStore query/client abstractions, route commands through the typed Parties client/EventStore command boundary, and delegate generic stream/event inspection to EventStore Admin UI safe deep-links.
- D20 supports the UX fail-closed states: sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures.
- D20 supports localization, focus management, keyboard access, non-color-only state, and polite status announcements.
- The party picker UX aligns with the architecture and epics by using the EventStore-fronted Parties client boundary and by avoiding retired direct Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
- Epic planning notes confirm Story 12.7 as the active admin portal implementation path and Story 12.8 as the active picker rewrite path; earlier TypeScript or direct Parties REST wording is historical and superseded where it conflicts.

### Alignment Issues

No hard UX/PRD/Architecture misalignment found.

### Warnings

- Admin portal production readiness is explicitly blocked until Wave 1 behavior is landed or frozen and the typed Parties client/query/command contract is available. This should remain visible in story sequencing and acceptance criteria.
- GDPR UX spans PRD v1.1 GDPR capability and v1.2 frontend delivery. Implementation planning must keep the capability/API layer and the visual admin surface separated so v1.1 compliance work does not accidentally depend on v1.2 UI completion.
- Epic 10 contains historical wording that is superseded by Epic 12. Future story execution should follow Story 12.7 and Story 12.8 where conflicts exist.

## Epic Quality Review

### Overall Structure

The epic set is mostly strong on requirement coverage and acceptance-criteria specificity. Most stories use clear `As/I want/So that` framing and Given/When/Then acceptance criteria. Epic goals generally describe consumer, developer, AI agent, administrator, or operator outcomes rather than raw implementation milestones.

However, the document is not yet implementation-clean. It carries historical planning notes, superseded wording, a missing referenced Epic 12, and one non-PRD FR reference. Those are readiness defects because implementation agents could follow the wrong path.

### Critical Violations

#### C1: Epic 12 Is Referenced As Canonical But Is Missing From This Epics Document

Evidence:

- Planning readiness says the canonical implementation direction is recorded in Epic 12 story files and the execution sequence includes `Epic 12: EventStore-fronted architecture pivot and consumer migration`.
- Epic 10 planning says Story 12.7 is the active admin portal implementation path and Story 12.8 is the active picker rewrite path.
- This document contains Epics 1-9, Epic 11, and Epic 10, but no Epic 12 section or Stories 12.7/12.8.

Impact:

- Forward dependency violation: Epic 10 depends on a future/missing Epic 12 boundary.
- Implementation agents cannot verify the active admin/picker path from the canonical epics document.
- Historical Story 10 wording may be executed despite being superseded.

Recommendation:

- Add Epic 12 and the active Story 12.7/12.8 content to the epics artifact, or explicitly link to the separate Epic 12 artifact in the document inventory and readiness report.
- Mark Story 10.x as legacy/reference-only if it is not executable.
- Re-run readiness after Epic 12 is present or explicitly selected as an assessment input.

#### C2: Epic 11 References `FR75`, Which Does Not Exist In The PRD

Evidence:

- Epic 11 summary lists `FRs covered: FR39, FR40, FR41, FR75`.
- The PRD contains FR1-FR74 only.
- The dedicated FR Coverage Map correctly maps 74/74 FRs and does not include FR75.

Impact:

- Traceability is internally inconsistent.
- If `FR75` represents a real Tenants integration requirement, it is missing from the PRD and coverage matrix.
- If it is accidental, it can mislead story scoping and acceptance checks.

Recommendation:

- Replace `FR75` with the correct existing FR(s), or add a new PRD requirement through controlled PRD revision.
- Update the FR Coverage Map and the Epic 11 summary so they agree.

### Major Issues

#### M1: MCP Tool Names Diverge From The PRD Without A Clean Traceability Decision

Evidence:

- PRD names five MCP tools: `search_parties`, `get_party`, `create_party`, `update_party`, `list_parties`.
- Epic 5 names five tools as `create_party`, `find_parties`, `get_party`, `update_party`, `delete_party`.
- Story 5.1 folds list behavior into `find_parties` list mode.
- Story 5.3 adds `delete_party`, mapped to soft deactivation.
- The epics document itself notes an open item: architecture uses `find_parties` + `delete_party`, refined from PRD's `search_parties` + `list_parties`.

Impact:

- AI-agent contract expectations are ambiguous.
- PRD success criteria about well-named, predictable MCP tools cannot be validated against a stable tool list.
- `delete_party` is potentially confusing because the domain operation is soft deactivation and GDPR erasure is separate.

Recommendation:

- Make one canonical MCP tool naming decision in PRD, architecture, and epics.
- If `find_parties` and `delete_party` are intentional, revise PRD FR/tool references and document the soft-delete semantics explicitly.
- If the PRD is canonical, rename Epic 5 stories and ACs to `search_parties` and `list_parties`, and remove or reframe `delete_party`.

#### M2: Several Stories Are Standalone Quality Gates Rather Than Independently Valuable Product Stories

Evidence:

- Story 1.5: Party Aggregate Tier 1 Unit Tests
- Story 2.3: Contact Channel & Identifier Unit Tests
- Story 3.4: Projection Unit & Integration Tests
- Story 4.3: Composite Command Unit Tests
- Story 5.4: MCP Tools Tests & Architectural Fitness
- The document includes repeated planning notes acknowledging these quality gates are historical and should usually be acceptance criteria or engineering tasks under behavior stories.

Impact:

- These stories can pass without delivering new user-visible capability.
- Sprint planning may split behavior and verification in ways that allow unfinished increments.

Recommendation:

- For future executable stories, fold quality gates into the behavior stories they verify unless the test harness is reusable independent infrastructure.
- Keep historical test-only stories only if clearly marked as already-completed historical records.

#### M3: Story 1.2 Front-Loads Contracts Beyond The First Behavior Slice

Evidence:

- Story 1.2 defines initial lifecycle contracts but also includes `ContactChannelId`, `IdentifierId`, query models, `CompositeCommandResult`, `[PersonalData]` coverage across contact channels/identifiers, and `IsNaturalPerson`.
- The planning note warns that historical Epic 1 work defined a broader contract surface and future additions must be sliced with the behavior that first consumes them.

Impact:

- Violates the "create entities/contracts when first needed" principle if treated as an executable future story.
- Increases risk of unused or prematurely stabilized public contract shapes.

Recommendation:

- Treat Story 1.2 as historical if already complete.
- For active implementation, split contracts by consuming behavior: lifecycle in Epic 1, contact/identifier contracts in Epic 2, projection query models in Epic 3, composite result contracts in Epic 4, MCP schemas in Epic 5, GDPR/privacy/search contracts in v1.1 stories.

#### M4: Epic 10 Contains Superseded Packaging/Boundary Wording

Evidence:

- Epic 10 planning note says Story 12.7 and Story 12.8 are the active paths and earlier direct Parties REST/TypeScript wording is historical.
- Story 10.3 still says the picker is independently deployable "as an npm package or similar".
- Current UX and architecture specify a FrontComposer/Blazor picker communicating through the EventStore-fronted Parties client/query boundary.

Impact:

- Could produce a wrong package target or frontend architecture if copied into implementation.

Recommendation:

- Rewrite Story 10.3 to match the approved party picker UX addendum and FrontComposer/Blazor delivery model.
- Remove or mark the `npm package` wording as legacy/non-authoritative.

### Minor Concerns

- Epic numbering/order is confusing: Epic 11 appears before Epic 10 by design, while the missing Epic 12 is referenced between them. The execution sequence should be reflected directly in artifact organization.
- Epic 3 is user-value framed but also carries a large amount of projection infrastructure in one epic. This is acceptable because read projection is necessary for discovery/search value, but story slicing should keep read-model infrastructure tied to observable query outcomes.
- Some acceptance criteria rely on "reviewed for architecture compliance" checks. These are useful, but should be backed by explicit tests or fitness checks wherever possible.

### Best Practices Compliance Checklist

| Area | Result | Notes |
| ---- | ------ | ----- |
| Epics deliver user value | Mostly pass | Epics are value-oriented, with Epic 8 as operator value. |
| Epic independence | Partial | Epic 10 has forward dependency on missing Epic 12; Epic 11 prerequisite is explicit. |
| Stories appropriately sized | Partial | Test-only stories and broad Story 1.2 contract slice need cleanup if still executable. |
| No forward dependencies | Fail | Epic 12/Story 12.7/12.8 references are unresolved in this artifact. |
| Database/entity/contracts created when needed | Partial | No database-table issue found; contract front-loading exists in Story 1.2. |
| Clear acceptance criteria | Mostly pass | BDD format is broadly present and specific. |
| Traceability to FRs maintained | Partial | Main FR map is complete, but Epic 11 `FR75` and MCP naming divergence must be resolved. |

### Remediation Priority

1. Add or select the Epic 12 artifact and resolve Story 12.7/12.8 references.
2. Fix or formalize the `FR75` reference.
3. Normalize MCP tool naming across PRD, architecture, epics, and UX.
4. Mark historical quality-gate stories as historical, or fold them into behavior stories before implementation.
5. Clean Story 10.3 packaging/boundary wording.

## Summary and Recommendations

### Overall Readiness Status

**NEEDS WORK**

The project is not blocked by missing PRD coverage: the PRD is complete, the epics document maps all 74 PRD FRs, and UX documentation exists for both the admin portal and party picker. The implementation risk is artifact consistency. The epics file mixes canonical guidance, historical guidance, missing Epic 12 references, and at least one non-existent FR reference. Those issues should be fixed before broad story execution, especially for admin/frontend and tenant/EventStore-fronted work.

### Critical Issues Requiring Immediate Action

1. **Missing Epic 12 / active Story 12.7 and 12.8 path**
   The epics document says Epic 12 is canonical for the EventStore-fronted architecture pivot and says Story 12.7/12.8 are active admin/picker paths, but Epic 12 is not included in the assessed artifact. This is a forward dependency and implementation-readiness blocker.

2. **Invalid `FR75` reference in Epic 11**
   Epic 11 lists `FR75`, but the PRD ends at FR74. Either this is a typo or a missing PRD requirement. The traceability record must be corrected.

### Major Issues Requiring Cleanup

1. **MCP tool naming mismatch**
   PRD names `search_parties` and `list_parties`; epics use `find_parties` and `delete_party`. Pick one canonical MCP contract and align PRD, architecture, epics, and tests.

2. **Standalone test/quality-gate stories**
   Several stories are test-only quality gates. Keep them only as historical records or merge their expectations into behavior stories before implementation planning.

3. **Story 1.2 contract front-loading**
   If still executable, Story 1.2 introduces contracts beyond the first lifecycle slice. Split contract additions by first consuming behavior.

4. **Story 10.3 legacy packaging wording**
   The party picker story still mentions `npm package or similar`, while the accepted direction is FrontComposer/Blazor through the EventStore-fronted Parties client/query boundary.

### Warnings To Track

- Admin portal production readiness depends on the typed Parties client/query/command contract.
- GDPR capability and frontend delivery span different phases; keep API/compliance work separate from v1.2 UI completion.
- Epic numbering and artifact organization are confusing because Epic 11 appears before Epic 10 and Epic 12 is referenced but absent.

### Recommended Next Steps

1. Add the Epic 12 artifact to the planning set or inline Epic 12 into `epics.md`, including Story 12.7 and Story 12.8.
2. Correct the Epic 11 `FR75` reference and update all traceability tables.
3. Decide the canonical MCP tool names and update PRD, architecture, epics, and future tests consistently.
4. Mark historical stories/notes as non-executable, or rewrite the epics document into a clean implementation backlog.
5. Re-run this readiness workflow after those corrections.

### Final Note

This assessment identified **12 findings requiring attention** across document traceability, UX sequencing, and epic/story quality: 2 critical issues, 4 major issues, 3 minor concerns, and 3 UX/planning warnings. The strongest part of the planning set is requirement coverage; the weakest part is canonical implementation clarity.

**Assessor:** Codex using `bmad-check-implementation-readiness`
**Completed:** 2026-05-13
