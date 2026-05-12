---
project: Hexalith.Parties
date: 2026-05-12
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
includedFiles:
  prd:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\prd.md
  prdSupporting:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\prd-validation-report.md
  architecture:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\architecture.md
  epicsStories:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\epics.md
  ux:
    - D:\Hexalith.Parties\_bmad-output\planning-artifacts\ux-admin-portal-2026-05-10.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-12
**Project:** Hexalith.Parties

## Step 1: Document Discovery

### Confirmed Document Inventory

#### PRD Files

Whole documents:

- `prd.md` (82,829 bytes, modified 2026-05-01 23:11:55) - selected as PRD source.
- `prd-validation-report.md` (26,454 bytes, modified 2026-03-02 16:05:46) - retained as supporting context if needed.

Sharded documents:

- None found.

#### Architecture Files

Whole documents:

- `architecture.md` (86,409 bytes, modified 2026-05-12 08:05:16) - selected as Architecture source.

Sharded documents:

- None found.

#### Epics & Stories Files

Whole documents:

- `epics.md` (121,839 bytes, modified 2026-05-07 17:43:59) - selected as Epics & Stories source.

Sharded documents:

- None found.

#### UX Design Files

Whole documents:

- `ux-admin-portal-2026-05-10.md` (7,391 bytes, modified 2026-05-10 13:27:27) - selected as UX source.

Sharded documents:

- None found.

### Discovery Issues

- No whole-vs-sharded duplicate formats found.
- No required document category is missing.
- PRD search found `prd-validation-report.md`; `prd.md` is the confirmed PRD source, with the validation report available only as supporting context.

## PRD Analysis

### Functional Requirements

FR1: Authorized client can create a new party as either a person or an organization with type-specific details

FR2: Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix)

FR3: Authorized client can update organization-specific details (legal name, trading name, legal form, registration number)

FR4: Authorized client can deactivate a party (soft lifecycle management)

FR5: Authorized client can reactivate a previously deactivated party

FR6: System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation - `"{FirstName} {LastName}"` for persons, `"{LegalName}"` for organizations; locale-aware formatting deferred to v1.1)

FR7: Each party has a client-generated, immutable UUID as its stable identity

FR8: Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social)

FR9: Authorized client can update an existing contact channel on a party

FR10: Authorized client can remove a contact channel from a party

FR11: Authorized client can mark a contact channel as preferred for its type

FR12: Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references)

FR13: Authorized client can remove an identifier from a party

FR14: Consumer can list parties with pagination and filtering by type (person/organization) and active status

FR15: Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields.

FR16: Deferred to v1.1: Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1.

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

FR30: System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action - enabling developers to resolve the issue without consulting documentation or debugging the service

FR31: Developer can deploy a running instance from source with standard container tooling

FR32: Getting-started documentation enables a developer to deploy and send their first command as a self-service experience

FR33: Contract types package has zero runtime dependencies beyond netstandard2.1 - consuming applications inherit no infrastructure stack

FR34: System publishes domain events when party state changes

FR35: Consuming application can subscribe to party events and build domain-specific read models

FR36: System handles duplicate commands idempotently (safe deduplication in distributed scenarios)

FR37: Forward-compatible event contracts (including party merge) are available to consuming applications from day one

FR38: Consuming application documentation includes handler patterns for erasure and dangling reference cleanup, with explicit warning that `PartyErased` subscription is mandatory for all consuming apps regardless of which other events they handle

FR39: System isolates party data by tenant at all layers - no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering

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

FR54: Events published to subscribers contain readable data - subscribers never handle decryption

FR55: System returns an "erased" status for erased parties, not cryptographic errors

FR56: System publishes auto-generated API specification documentation accessible to developers

FR57: System supports versioned API endpoints that coexist during deprecation periods

FR58: System maps domain rejections to standardized HTTP error formats with a documented error catalog

FR59: System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage

FR60: Developer can run the full system locally with a single command for development and evaluation

FR61: System provides deployment validation tooling to verify security configuration before production use

FR62: System displays a non-dismissable compliance warning until GDPR features are activated

FR63: System guarantees at-least-once event delivery to subscribers

FR64: System degrades gracefully when infrastructure components are unavailable - read operations continue when write-side components fail

FR65: Administrator can browse, search, and inspect party records via an administration interface

FR66: Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface

FR67: Consuming application developer can embed a party picker component in their UI for party search and selection

FR68: Consumer can filter parties by creation date or last-modified date range

FR69: Update operations (API and MCP) return the updated party state in the response, not just a confirmation

FR70: Published domain events include tenant context for consuming application routing decisions

FR71: System exposes health and readiness signals for infrastructure orchestration

FR72: Deferred to v1.1: Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit.

FR73: System delivers events for a single aggregate in causal order to each subscriber. Architecture must verify DAPR pub/sub ordering guarantees. If per-aggregate ordering cannot be guaranteed, document required handler design (order-tolerant or sequence-checking) in the architecture document.

FR74: MCP update operations use patch semantics - only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update

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

NFR9: Tenant isolation enforced at all layers - zero cross-tenant data leakage under any condition

NFR10: JWT token validation on every request; fail-closed on invalid or missing tokens

NFR11: Per-tenant encryption keys can be rotated without service downtime or data loss

NFR12: Personal data excluded from all application logs

NFR13: All API endpoints require authentication - no anonymous access

NFR14: System supports multi-tenant operation (no per-tenant infrastructure, stateless routing, partitionable metadata) validated at 100 concurrent tenants for MVP

NFR14a: System architecture supports scaling beyond 100 tenants without per-tenant infrastructure changes

NFR15: Tenant metadata operations (routing, key lookup) complete in < 50ms regardless of total tenant count

NFR16: System supports up to 100,000 parties per tenant (MVP validation target - sufficient for startups and SMBs; enterprise scale at millions of parties addressed in v2 via Elasticsearch projection and horizontal scaling)

NFR17: System sustains 100 read requests/second and 20 write requests/second per tenant

NFR18: Event store performance degrades < 10% at 100K parties per tenant with snapshot strategy active

NFR19: Read projections remain responsive (< 500ms) at 100K parties per tenant

NFR20: Service recovers from crash, replays necessary event state, and accepts requests within 30 seconds of restart

NFR21: When event store is unreachable, read projection queries continue serving cached data with a staleness indicator

NFR22: No data loss on service restart - event store is the durable source of truth

NFR23: At-least-once event delivery to subscribers via DAPR pub/sub

NFR24: Idempotent command handling ensures safe retry without duplicate side effects

NFR25: REST API conforms to auto-generated OpenAPI 3.x specification

NFR26: MCP server implements MCP protocol specification with 5 tools

NFR27: Published events follow stable, versioned contract schemas (append-only, additive changes only)

NFR28: Client NuGet packages impose < 10 transitive dependencies totalling < 5 MB (Contracts: zero runtime dependencies beyond netstandard2.1)

NFR29: Service has zero direct dependencies on specific state store or message broker implementations

NFR30: A developer deploys a running instance from source in < 15 minutes on first attempt using the documented getting-started guide

NFR31: NuGet client package size < 5MB with < 10 transitive dependencies

NFR32: v1.2 frontend applies output encoding to all party data fields rendered in the admin portal - no stored XSS from user-supplied or AI-created party data

Total NFRs: 33

### Additional Requirements

- MVP excludes GDPR compliance features and duplicate detection; startup/admin/API warnings remain non-dismissable until GDPR features are activated.
- MVP includes party aggregate lifecycle, type-specific person/organization details, contact channels, identifiers, deactivation/reactivation, stable client-generated UUIDs, personal-data attributes, display/sort name derivation, REST command API, read projection, MCP tools, Contracts and Client NuGet packages, EventStore-provided auth/multi-tenancy/eventing/idempotency/snapshot support, documentation, sample integration, and emergency manual erasure procedure.
- MVP hard gates: deploy in < 15 minutes, first `CreateParty` in < 30 minutes with comprehension check, single-prompt MCP creation returning complete party, and self-service documentation/README clarity.
- v1.1 adds GDPR compliance, crypto-shredding, consent, portability, restriction, erasure verification, processing records, locale-aware name formatting, semantic search, temporal name query, admin GDPR dashboard, DPO operability, warning removal, and operational observability.
- v1.2 adds administration frontend and party picker component; v2 targets scale, intelligence, duplicate resolution, advanced search, relationship graph, enrichment, client SDK expansion, and enterprise features.
- GDPR requirements include Article 17 erasure, Article 6 processing purpose tracking, Article 20 portability, Article 18 restriction, Article 30 processing records, `PartyErased` propagation, external acknowledgment tracking, metadata preservation after erasure, erased-party read behavior, cache invalidation, search purge, regulatory extensibility, DPIA notice, and DPA template.
- Security boundary: Parties owns field-level encryption, tenant filtering, JWT claim extraction, event encryption/decryption, internal erasure verification, input validation, and log sanitization; operators own DAPR secret store hardening, pub/sub access policies, IAM, network security, key backup procedures, and deployment configuration validation.
- Threat mitigations explicitly cover tenant spoofing, cross-tenant pub/sub subscription, cross-tenant projection queries, compromised sidecar key extraction, DAPR access-policy misconfiguration, MCP input injection, and personal-data log exposure.
- Key management requires per-party encryption keys through DAPR secret store with per-tenant namespaces, key versioning, key access audit trail, and backup/restore guidance that preserves erasure state.
- Tenant isolation must fail closed, reject missing tenant claims, include negative tests, and require custom projections to inherit tenant-filtering base behavior.
- Event-sourcing constraints require atomic event persistence, append-only immutable event contracts, additive schema evolution, tolerant deserialization with no upcasting, single Party aggregate with value objects, aggregate test coverage up to 50 contact channels, checksum-verifiable snapshots, actor-serialized commands, and name history preservation.
- Encryption constraints require zero DAPR awareness in domain code, decrypted pub/sub events, publish-time decryption circuit breaker, snapshot participation in crypto-shredding, automated `[PersonalData]` coverage verification, and a key-caching architecture that protects NFR1.
- Graceful degradation requires clear behavior when DAPR secret store, state store, or pub/sub components are unavailable, including committed events with delayed publishing but no event loss.
- Event integration requires DAPR pub/sub, idempotent handlers, forward-compatible `PartyMerged`, dangling reference guidance, tolerant deserialization, and decrypted events once GDPR encryption is active.
- Infrastructure portability requires DAPR-backed state store/message broker swaps without code changes, .NET Aspire + Docker local development, and a deployment validation script.
- Versioning strategy has three pillars: append-only event schema versioning, `/api/v1/parties` REST versioning with coexistence during deprecation, and semantic-versioned NuGet packages with migration guides for major changes.
- REST MVP endpoints include `GET /api/v1/parties`, `GET /api/v1/parties/{id}`, `GET /api/v1/parties/search?q=`, command endpoints following EventStore conventions, and OpenAPI 3.x publication.
- MCP MVP tools are exactly `search_parties`, `get_party`, `create_party`, `update_party`, and `list_parties`, designed for AI ergonomics rather than a 1:1 command mirror.
- Authentication and authorization are inherited from Hexalith.EventStore middleware; Parties must not implement custom auth.
- Data formats are JSON only for MVP; contracts package contains all command, event, and query types.
- Error model requires typed `DomainResult` rejections, RFC 9457 Problem Details mapping, documented error catalog, and specific status mappings for validation, not found, conflict, and tenant isolation errors.
- Rate limiting is an infrastructure concern, not application domain logic.
- NuGet package requirements include `Hexalith.Parties.Contracts` with zero runtime dependencies beyond `netstandard2.1` and `Hexalith.Parties.Client` with command/query clients and `AddPartiesClient()`, depending only on Contracts and HTTP abstractions.
- Documentation deliverables include README, getting-started guide, GDPR disclaimer, OpenAPI spec, and a runnable `/samples/BasicConsumingApp/` demonstrating command, query, event subscription, and MCP usage.
- Risk mitigations include snapshot strategy for aggregate growth, schema compatibility tests, key backup/rotation/hardening, erasure retry and blocking verification, personal-data attribute scans, handler guidance for `PartyErased`, log sanitization, degraded-mode behavior, tenant leakage tests, DAPR deployment validation, MVP compliance warning, and DAPR portability.

### PRD Completeness Assessment

The PRD is highly complete for readiness analysis: it contains explicit numbered FRs and NFRs, phase annotations, measurable success gates, domain constraints, regulatory/security obligations, API and MCP surfaces, packaging expectations, documentation requirements, and risk mitigations. The main areas requiring downstream validation are alignment with architecture and epics for intentionally deferred items, event ordering feasibility under DAPR pub/sub, GDPR/v1.1 architecture assumptions, projection-side tenant isolation, and whether implementation stories trace every MVP requirement without accidentally pulling v1.1/v1.2 scope into MVP.

## Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document includes an explicit `FR Coverage Map` and states: `Coverage: 74/74 FRs mapped - zero gaps.`

| FR Number | PRD Requirement | Epic Coverage | Status |
| --------- | --------------- | ------------- | ------ |
| FR1 | Create party as person or organization | Epic 1 | Covered |
| FR2 | Update person details | Epic 1 | Covered |
| FR3 | Update organization details | Epic 1 | Covered |
| FR4 | Deactivate party | Epic 1 | Covered |
| FR5 | Reactivate party | Epic 1 | Covered |
| FR6 | Derive display and sort names | Epic 1 | Covered |
| FR7 | Client-generated immutable UUID | Epic 1 | Covered |
| FR8 | Add contact channel | Epic 2 | Covered |
| FR9 | Update contact channel | Epic 2 | Covered |
| FR10 | Remove contact channel | Epic 2 | Covered |
| FR11 | Mark preferred contact channel | Epic 2 | Covered |
| FR12 | Add identifier | Epic 2 | Covered |
| FR13 | Remove identifier | Epic 2 | Covered |
| FR14 | List parties with pagination and filters | Epic 3 | Covered |
| FR15 | Display-name search for MVP | Epic 3 | Covered |
| FR16 | Semantic search deferred to v1.1 | Epic 9 | Covered |
| FR17 | Search match metadata | Epic 3 | Covered |
| FR18 | Retrieve party details by ID | Epic 1 | Covered |
| FR19 | Search eventual consistency | Epic 3 | Covered |
| FR20 | AI agent search/resolve through MCP | Epic 5 | Covered |
| FR21 | Composite party creation | Epic 4+5 | Covered |
| FR22 | Composite party update | Epic 4+5 | Covered |
| FR23 | AI agent get/list tools | Epic 5 | Covered |
| FR24 | Return complete party on create | Epic 5 | Covered |
| FR25 | Forgiving input schemas | Epic 5 | Covered |
| FR26 | Single package and one-line registration | Epic 6 | Covered |
| FR27 | Typed command client abstractions | Epic 6 | Covered |
| FR28 | Typed query client abstractions | Epic 6 | Covered |
| FR29 | REST API from any language | Epic 1 | Covered |
| FR30 | Typed rejection responses | Epic 1 | Covered |
| FR31 | Deploy from source with containers | Epic 1 | Covered |
| FR32 | Getting-started self-service docs | Epic 6 | Covered |
| FR33 | Zero-dependency contracts package | Epic 1 | Covered |
| FR34 | Publish domain events | Epic 7 | Covered |
| FR35 | Subscribe to party events | Epic 7 | Covered |
| FR36 | Idempotent command handling | Epic 1 | Covered |
| FR37 | Forward-compatible event contracts | Epic 7 | Covered |
| FR38 | Erasure and dangling-reference handler docs | Epic 7 | Covered |
| FR39 | Tenant isolation at all layers | Epic 1 | Covered |
| FR40 | Tenant from credentials only | Epic 1 | Covered |
| FR41 | Fail-closed missing tenant identity | Epic 1 | Covered |
| FR42 | Personal-data field attributes | Epic 1 | Covered |
| FR43 | Personal data excluded from logs | Epic 1 | Covered |
| FR44 | Right-to-erasure crypto-shredding | Epic 9 | Covered |
| FR45 | Erasure verification | Epic 9 | Covered |
| FR46 | Notify subscribers on erasure | Epic 9 | Covered |
| FR47 | Per-channel per-purpose consent | Epic 9 | Covered |
| FR48 | Consent revocation | Epic 9 | Covered |
| FR49 | Restrict processing | Epic 9 | Covered |
| FR50 | Lift processing restriction | Epic 9 | Covered |
| FR51 | Data portability export | Epic 9 | Covered |
| FR52 | Processing activity records | Epic 9 | Covered |
| FR53 | Field-level encryption per-party keys | Epic 9 | Covered |
| FR54 | Decrypted events at publish time | Epic 9 | Covered |
| FR55 | Erased party returns erased status | Epic 9 | Covered |
| FR56 | Auto-generated API specification | Epic 3 | Covered |
| FR57 | Versioned API endpoints | Epic 1 | Covered |
| FR58 | Standardized HTTP error formats | Epic 1 | Covered |
| FR59 | Runnable sample integration project | Epic 6 | Covered |
| FR60 | Run full system locally with one command | Epic 1 | Covered |
| FR61 | Deployment validation tooling | Epic 8 | Covered |
| FR62 | Non-dismissable GDPR warning | Epic 1 | Covered |
| FR63 | At-least-once event delivery | Epic 7 | Covered |
| FR64 | Graceful degradation | Epic 8 | Covered |
| FR65 | Admin browse/search/inspect | Epic 10 | Covered |
| FR66 | Admin GDPR operations | Epic 10 | Covered |
| FR67 | Embeddable party picker | Epic 10 | Covered |
| FR68 | Date range filtering | Epic 3 | Covered |
| FR69 | Return updated state in responses | Epic 4 | Covered |
| FR70 | Tenant context in published events | Epic 7 | Covered |
| FR71 | Health/readiness signals | Epic 8 | Covered |
| FR72 | Temporal name query | Epic 9 | Covered |
| FR73 | Per-aggregate causal event ordering | Epic 7 | Covered |
| FR74 | MCP patch semantics | Epic 5 | Covered |

### Missing Requirements

No uncovered PRD functional requirements were found in the epic coverage map.

No FRs were identified in the epics coverage map that are absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 74
- FRs covered in epics: 74
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found: `ux-admin-portal-2026-05-10.md`

The UX document is scoped to `Story 12.7 Admin Portal Rebuild on FrontComposer` and defines a Parties-specific admin portal surface. It explicitly excludes a landing page, marketing page, duplicated tenant management, and generic EventStore stream browsing.

### UX to PRD Alignment

- Aligned: UX directly supports FR65 admin browse/search/inspect through `/admin/parties`, result grid, detail panel, filters, search, status handling, and safe EventStore deep links.
- Aligned: UX directly supports FR66 admin GDPR operations through erasure, restriction, consent, portability, processing records, verification retry, and status/certificate flows.
- Aligned: UX supports NFR32 stored-XSS protection through output encoding, no raw markup, no JavaScript interpolation, no raw HTML fragments, and bounded ProblemDetails rendering.
- Aligned: UX reinforces FR39-FR41 and NFR9/NFR10 tenant isolation/fail-closed behavior through missing tenant, tenant switch, forbidden/cross-tenant, stale response, and sign-out clearing rules.
- Aligned: UX follows PRD search scope by supporting display-name search and capability-gated rich search rather than silently sending unsupported filters.
- Gap: UX does not cover FR67 embeddable party picker. The document is portal-only, so picker UX/design remains unaddressed by this UX artifact.
- Scope mismatch: PRD and epics describe a TypeScript admin portal, while the UX document defines a FrontComposer domain surface and references Razor/component text paths. This should be reconciled before implementation stories are treated as ready.

### UX to Architecture Alignment

- Supported: Architecture accounts for admin portal/GDPR UI/party picker at the FR65-FR67 level, read projections for list/search/detail, tenant-filtered query enforcement, ProblemDetails, health/degradation behavior, and EventStore admin/rebuild operational surfaces.
- Supported: Architecture supports the UX decision to avoid duplicated tenant management through the later Hexalith.Tenants integration epic and tenant access projection.
- Supported: Architecture supports safe list/detail data needs through PartyIndexProjectionActor and PartyDetailProjectionActor, and supports display-name-only v1.0 search with extensibility for richer search.
- Gap: Architecture marks "Admin portal frontend architecture" as future enhancement and does not yet specify FrontComposer routing, deep-link behavior, component composition, localization resource strategy, focus management, live regions, or UX state-clearing rules.
- Gap: UX names production blockers on Wave 1 behavior and Story 12.5 typed Parties client/query/command contract, but those Story 12.4/12.5/12.7 dependencies are not represented in the current PRD, architecture, or epics inventory searched for this assessment.
- Gap: UX requires contract-unavailable fail-closed states with exact blocker messages. Architecture has graceful degradation generally, but not this UI-specific contract gating.
- Gap: UX requires safe EventStore Admin UI deep-links from safe identifiers only. Architecture has admin/rebuild concepts, but does not define this FrontComposer-to-EventStore Admin UI linking contract.

### Alignment Issues

1. Portal technology mismatch: PRD/epics say TypeScript admin portal; UX says FrontComposer domain surface with Razor/component rendering. This is a readiness issue because implementation stories may target the wrong frontend stack.
2. Story lineage mismatch: UX is Story 12.7 and depends on Story 12.5, while the discovered epics document currently models admin/frontend as Epic 10 and does not mention Stories 12.4/12.5/12.7.
3. Missing party picker UX: FR67 is covered by epics but not by the UX document.
4. Architecture under-specifies UX behavior for accessibility, localization, deep-linking, fail-closed state clearing, stale response suppression, and contract-unavailable gating.

### Warnings

- Warning: Do not start admin portal implementation until the frontend stack is reconciled: TypeScript vs. FrontComposer/Razor.
- Warning: Do not treat Story 12.7 as implementation-ready until Story 12.5 typed client/query/command contract is landed or formally frozen, matching the UX blocker.
- Warning: Add a separate UX artifact or story acceptance criteria for FR67 party picker.
- Warning: Add architecture or story-level acceptance criteria for localization, accessibility, privacy-safe URLs/storage/telemetry, and safe EventStore Admin UI deep-links.

## Epic Quality Review

### Critical Violations

#### C1: Story 1.2 Creates Too Much Future Surface Up Front

Story 1.2 (`Domain Contracts - Complete Type Definitions`) requires all commands, events, value objects, query models, composite result types, personal-data attributes, and future-facing types before the aggregate, projection, MCP, GDPR, or frontend stories consume them.

Best-practice violation: this is the equivalent of "create all models up front." It increases blast radius, makes the first implementation story very large, and allows future epics to inherit unvalidated contract choices.

Evidence:
- Story 1.2 requires command types for lifecycle, contact channels, identifiers, composite command results, query models, personal-data attributes, and `IsNaturalPerson`.
- Epic 1 overview explicitly says all event and command types, including channel/identifier types used in later epics, are defined in Contracts because they are contracts.

Recommendation:
- Split Story 1.2 into thin contract slices that land with the first story that needs them.
- Keep Epic 1 contract work to party lifecycle and retrieval only.
- Move contact/identifier contracts to Epic 2, projection query models to Epic 3, composite result contracts to Epic 4, MCP-specific models to Epic 5, and GDPR encryption/erasure contracts to Epic 9.

#### C2: Epic 10 Depends on Epic 11 Despite Numbering

Epic 11 (`Hexalith.Tenants Integration for Parties`) is placed before Epic 10 and says it is scheduled before Epic 10 so the admin portal consumes tenant context. That sequencing is logical, but the numbering creates a forward-dependency smell: Epic 10 depends on a higher-numbered epic.

Best-practice violation: Epic N should not require Epic N+1 to function. The document order tries to fix this, but the numbering communicates the opposite.

Recommendation:
- Renumber the Tenants integration epic before Administration, or rename sequencing explicitly so admin implementation cannot start before tenant integration.
- Update any story references and sprint plans after renumbering.

### Major Issues

#### M1: Several Epics Are Technical Milestones Rather Than User-Outcome Epics

Affected epics:
- Epic 1: `Domain Foundation & Party Lifecycle`
- Epic 4: `Composite Commands & Advanced Aggregate Logic`
- Epic 8: `Operational Readiness & Production Hardening`
- Epic 11: `Hexalith.Tenants Integration for Parties`

These epics contain user or operator value, but their titles and several story goals are implementation-structure first. Epic 4 is the clearest issue: users need single-operation party creation/update, not "advanced aggregate logic."

Recommendation:
- Reframe titles and goals around outcomes:
  - Epic 1: "Create and Retrieve Parties Safely"
  - Epic 4: "Create and Update Complete Parties in One Operation"
  - Epic 8: "Operate Parties Safely in Production"
  - Epic 11: "Enforce Tenant Access Through Hexalith.Tenants"

#### M2: Test-Only Stories Do Not Independently Deliver User Value

Affected stories include:
- Story 1.5: Party Aggregate Tier 1 Unit Tests
- Story 2.3: Contact Channel & Identifier Unit Tests
- Story 3.4: Projection Unit & Integration Tests
- Story 4.3: Composite Command Unit Tests
- Story 5.4: MCP Tools Tests & Architectural Fitness

These are valuable work, but as standalone stories they are technical tasks. They are better represented as acceptance criteria on the behavior stories or as explicit engineering tasks under each story.

Recommendation:
- Fold test expectations into the relevant behavior stories.
- Keep only separate test stories where they deliver an independently usable quality gate, such as a reusable test harness or CI fitness test suite.

#### M3: Epic 9 Mixes GDPR Compliance With Search Expansion

Epic 9 is named `GDPR Compliance (v1.1)` but includes:
- Temporal name queries
- Hexalith.Memories-backed party search

Temporal legal/audit queries can fit GDPR-adjacent audit needs, but Hexalith.Memories-backed search is a discovery/search capability, not GDPR compliance. This reduces epic cohesion and makes acceptance harder to reason about.

Recommendation:
- Move Memories-backed search into a dedicated search/intelligence epic, or rename Epic 9 to accurately reflect a broader v1.1 compliance and discovery scope.

#### M4: Admin Frontend Stories Conflict With Current UX Direction

Epic 10 stories require a TypeScript application/component, while the UX document defines a FrontComposer domain surface and references Razor/component rendering. This is now a quality issue in addition to a UX-alignment issue because stories would drive implementation toward the wrong stack.

Recommendation:
- Update Stories 10.1-10.3 to match FrontComposer, or explicitly supersede the UX document if TypeScript remains the intended direction.

#### M5: Story Dependencies Are Mostly Sequential, But Some Stories Are Too Broad

Examples:
- Story 1.6 combines REST API, error handling, party retrieval, authentication, tenant failures, log sanitization, validation registration, and content negotiation.
- Story 1.7 combines AppHost, DAPR components, ServiceDefaults, health checks, warning headers, startup logging, and full round-trip local development.
- Story 8.2 combines health, readiness, multiple degraded infrastructure modes, crash recovery, staleness indicators, and runbook documentation.

Recommendation:
- Split broad stories where a single story spans multiple components and quality gates.
- Preserve user-value slices by keeping each split story demonstrable end-to-end.

### Minor Concerns

#### m1: Acceptance Criteria Are Mostly BDD, But Some Are Review-Only

Many ACs use solid Given/When/Then structure. Some are review-only, such as "When reviewed for architecture compliance" or "appropriate profiles exist." These can be testable, but they need either fitness tests, explicit file checks, or clear review criteria.

Recommendation:
- Convert review-only ACs into automated fitness checks where practical.
- Where manual review remains necessary, specify the concrete evidence expected.

#### m2: Some Expected Outcomes Are Vague

Examples include "clear error," "appropriate profiles," "resilience patterns are configured," "useful event context," and "documented degraded behavior."

Recommendation:
- Replace vague terms with measurable expected output, exact fields, or referenced error/status contracts.

#### m3: Epic Numbering Is Non-Linear

The document lists Epic 11 before Epic 10. The intent is understandable, but the numbering can confuse implementation order, dependency checks, and sprint planning.

Recommendation:
- Make numbering match execution order or add a separate explicit implementation sequence table.

### Best Practices Compliance Summary

| Epic | User Value | Independent Sequencing | Story Sizing | AC Quality | Traceability |
| ---- | ---------- | ----------------------- | ------------ | ---------- | ------------ |
| Epic 1 | Partial - value plus foundation work | Pass | Needs split | Good with some broad ACs | Strong |
| Epic 2 | Pass | Pass | Good | Good | Strong |
| Epic 3 | Pass | Pass | Good | Good | Strong |
| Epic 4 | Needs reframing | Pass | Mixed | Good | Strong |
| Epic 5 | Pass | Pass | Good | Good | Strong |
| Epic 6 | Pass | Pass | Good | Good | Strong |
| Epic 7 | Pass | Pass | Good | Good | Strong |
| Epic 8 | Operator value, title technical | Pass | Needs split | Mixed | Strong |
| Epic 9 | Mixed cohesion | Pass | Mixed | Good | Strong |
| Epic 11 | Operator/security value, numbering issue | Should precede Epic 10 | Good | Good | Strong |
| Epic 10 | Pass, but blocked by Epic 11 and UX mismatch | Fails if numbered order is used | Good | Good | Strong |

### Quality Review Conclusion

The epic set has excellent FR traceability and generally strong acceptance criteria, but it is not cleanly implementation-ready as-is. The main blockers are structural: oversized upfront contract work, technical/test-only stories, Epic 10/Epic 11 sequencing, and the admin frontend stack mismatch with the newer UX spec.

## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK

The planning package is strong enough to preserve product intent, but it is not ready for clean Phase 4 implementation without artifact cleanup. The PRD is complete, the required planning documents exist, and every PRD functional requirement is mapped to an epic. The blocking issues are in implementation packaging: story shape, sequencing, and frontend direction.

### Positive Readiness Signals

- Required artifacts exist: PRD, architecture, epics/stories, and UX.
- PRD extraction found 74 FRs and 33 NFRs with clear phase and quality expectations.
- Epic coverage map covers 74/74 PRD FRs.
- Architecture covers all major domains, including EventStore conventions, projections, MCP, GDPR preparation, tenant isolation, operational readiness, and package boundaries.
- Acceptance criteria are mostly Given/When/Then and are generally testable.

### Critical Issues Requiring Immediate Action

1. Split Story 1.2 before implementation. It creates too much future contract surface up front and violates incremental story slicing.
2. Resolve Epic 10/Epic 11 sequencing. Admin frontend depends on Tenants integration, but the numbering implies the opposite.
3. Reconcile admin frontend direction. PRD/epics say TypeScript; UX says FrontComposer/Razor domain surface.
4. Update admin stories to reflect Story 12.4/12.5/12.7 blockers or remove those blockers from the UX spec if they are no longer valid.
5. Move or reframe Memories-backed search out of the GDPR epic unless Epic 9 is intentionally broadened beyond compliance.

### Recommended Next Steps

1. Revise the epics document so execution order, numbering, and dependencies agree. Put Tenants integration before Administration in both number and position.
2. Refactor Story 1.2 into incremental contract stories tied to the first behavior that consumes each contract.
3. Convert test-only stories into acceptance criteria or engineering tasks under their behavior stories, except for reusable fitness-test infrastructure.
4. Update Epic 10 stories to match the UX spec's FrontComposer direction, including localization, accessibility, fail-closed state clearing, privacy-safe links/storage/telemetry, and contract-unavailable states.
5. Add a separate UX/design artifact or explicit acceptance criteria for FR67 party picker.
6. Clarify whether Story 12.5 typed Parties client/query/command contract is in scope for the current implementation wave and where it appears in the epics.
7. Rename technical epics around user/operator outcomes, especially Epic 4 and Epic 8.
8. Tighten vague acceptance criteria such as "clear error," "appropriate profiles," "resilience patterns configured," and "useful event context."

### Issue Count

This assessment identified 10 issues requiring attention across 4 categories:

- 2 critical structural violations
- 5 major epic/story quality issues
- 3 minor acceptance-criteria and numbering concerns
- UX alignment warnings are included in the major issue set where they overlap with implementation readiness

### Final Note

The artifacts are close. This is not a requirements coverage failure; it is a planning hygiene and sequencing failure. Address the critical issues before proceeding to implementation to avoid large early churn, wrong frontend-stack work, and avoidable dependency confusion.

Assessor: Codex using `bmad-check-implementation-readiness`

Assessment date: 2026-05-12
