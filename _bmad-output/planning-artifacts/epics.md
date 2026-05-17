---
stepsCompleted: ['step-01-requirements-extracted', 'step-02-epics-designed', 'step-03-stories-generated', 'step-04-final-validation-passed']
inputDocuments:
  - prd.md
  - architecture.md
  - ux-admin-portal-2026-05-10.md
  - ux-party-picker-2026-05-12.md
---

# Hexalith.Parties - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.Parties, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

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
FR72: (Deferred to v1.1) Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit.
FR73: System delivers events for a single aggregate in causal order to each subscriber
FR74: MCP update operations use patch semantics - only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update

### NonFunctional Requirements

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
NFR32: (v1.2) Frontend applies output encoding to all party data fields rendered in the admin portal - no stored XSS from user-supplied or AI-created party data

### Additional Requirements

- Use the EventStore solution structure pattern as the starter instead of `dotnet new webapi`; manual scaffolding must mirror EventStore conventions to validate reuse for future domain services.
- Use `Hexalith.Parties.slnx` with `/src`, `/tests`, and `/samples`, central package management, shared build properties, MinVer, nullable enabled, warnings as errors, CRLF, UTF-8, Allman braces, and file-scoped namespaces.
- Align with the architecture's target package/project boundaries: Contracts, Client, Server, Projections, Parties service, Aspire hosting extensions, AppHost, ServiceDefaults, Testing, and sample integration.
- Keep `Hexalith.Parties.Contracts` free of runtime dependencies beyond the target framework and prevent dependencies on hosting, DAPR, MediatR, FluentValidation, UI, or infrastructure packages.
- Keep `Hexalith.Parties.Client` dependent only on Contracts and HTTP/client abstractions; it must not reference Server, Projections, or the Parties service.
- Implement read projections as DAPR actor-managed JSON state with per-party detail actors and per-tenant index actors.
- Design the party index state behind a partitioning interface so v1.0 can use single-key storage while scale deployments can switch strategies without changing the architecture.
- Keep projection handlers pure and DAPR-free; DAPR actor wrappers should be thin delegates to handler logic.
- Implement `CreatePartyComposite` so MCP create operations complete in one aggregate turn and satisfy the < 1 second MCP target.
- Implement `UpdatePartyComposite` with aggregate-side diff semantics; MCP translates forgiving patch intent into explicit command payloads without owning domain logic.
- Make composite operations all-or-nothing in a single actor turn, with sub-operation idempotency, duplicate detection, conflict detection, and configurable maximum payload size.
- Treat the MCP layer as a translation layer with no domain event references; enforce this boundary through architectural fitness tests.
- Keep REST and MCP surfaces on the same command/query path and shared validation/error handling model.
- Use URL-path API versioning (`/api/v1/parties`) and support versioned endpoints during deprecation periods.
- Map domain rejections to standardized HTTP ProblemDetails and maintain a documented error catalog.
- Let Hexalith.EventStore own write-side tenant validation, idempotency, event envelopes, snapshots, status tracking, pub/sub publication, and event ordering guarantees; Parties must not add write-side workarounds.
- Parties owns projection-side tenant/access behavior; read models, search, admin views, and tenant event consumption must fail closed.
- Mark personal data with type-dependent `[PersonalData]` attributes at MVP, including derived names and organization contact data where it can identify a person.
- Plan for mid-lifecycle `IsNaturalPerson` reclassification and stricter v1.1 crypto-shredding behavior without breaking MVP contracts.
- Provide deployment validation tooling for security configuration and keep DAPR access-control deny-by-default.
- Implement projection rebuild support through replaying events into pure handlers and surface degraded responses when projection state is corrupt or rebuilding.
- Add projection health monitoring that detects state corruption and triggers rebuild/operational alerting.
- Batch index actor event processing with configurable batch size and time windows to protect projection consistency under bursts.
- Provide three-tier tests: pure unit tests with no infrastructure, DAPR slim tests for actor/API/MCP integration, and full Aspire/Docker integration tests.
- Define the composite command test matrix in story specifications before implementation begins.
- Provide a runnable sample integration demonstrating command, query, event subscription, and MCP usage.
- Keep EventStore Admin UI stream, event, correlation, and command-status inspection delegated to EventStore Admin UI through safe links rather than duplicating stream browsing in Parties.

### UX Design Requirements

UX-DR1: Admin portal first viewport must be the working console with global search, compact filters, tenant/auth state, result grid, and detail panel visible without a landing page or introductory screen.
UX-DR2: Admin portal routes must support `/admin/parties`, `/admin/parties/{partyId}`, and `/admin/parties/{partyId}/gdpr` when FrontComposer route support is available.
UX-DR3: Admin portal must generate EventStore Admin UI links only from safe identifiers such as stream id, aggregate id, command id, correlation id, or timestamp, using generic labels and disabled bounded reasons when unavailable.
UX-DR4: Admin portal toolbar must include search input, party type filter, active filter, retry control, and EventStore Admin UI availability indicator.
UX-DR5: Admin portal results must use server-side paging or virtualization and display display name, type, active/erased/restricted state, created/modified dates, and non-PII status indicators.
UX-DR6: Admin portal detail panel must show selected party summary, contacts, identifiers, consent, restriction, erasure, processing records, and safe EventStore Admin UI links.
UX-DR7: Admin portal must include a GDPR drawer or panel for erasure, restriction, consent, portability, processing records, confirmations, and operation outcomes.
UX-DR8: Admin portal status region must announce bounded loading, empty, blocked, forbidden, timeout, degraded, malformed response, stale response, and contract-unavailable states for screen readers.
UX-DR9: Admin portal list/detail behavior must clear sensitive detail state on sign-out, missing tenant, non-admin, tenant switch, stale response, forbidden, not found, erased/gone, timeout, malformed response, and contract-unavailable failures.
UX-DR10: Admin portal search mode must disable unsupported filters rather than silently sending ignored filters.
UX-DR11: Admin portal GDPR actions must remain disabled until the accepted EventStore-fronted Parties client/gateway contract exists, showing the bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract`.
UX-DR12: Admin portal erasure request flow must confirm by party id only, return command accepted outcome, and refresh authoritative erasure status through EventStore query.
UX-DR13: Admin portal erasure certificate flow must expose poll/refresh, safe verification result, and certificate download filename composed from party id plus timestamp only.
UX-DR14: Admin portal restriction flow must capture a bounded reason, show command accepted outcome, and refresh before follow-on actions.
UX-DR15: Admin portal consent flow must handle add, revoke, and history per channel and per purpose, with no party-wide or tenant-wide consent shortcut.
UX-DR16: Admin portal portability export must generate safe filename, content type, and payload through the accepted query/client path.
UX-DR17: Admin portal processing records must render read-only bounded summaries with safe correlation links.
UX-DR18: Admin portal must localize all labels, status messages, dates, booleans, counts, warning copy, validation messages, lawful-basis labels, and operation outcomes.
UX-DR19: Admin portal accessibility must provide keyboard reachability, focus restoration, accessible grid row names/states, landmark headings, dialog focus traps, polite status announcements, and non-color-only status.
UX-DR20: Admin portal must render all user, backend, AI-created, and operator-entered content through normal encoded Razor/component text paths and must not use raw markup or raw HTML fragments.
UX-DR21: Admin portal privacy rules must prevent personal data in URLs, storage keys, telemetry dimensions, link labels, filenames, logs, command/query payload logs, JWTs, claims dictionaries, tenant membership payloads, sidecar names, or DAPR ports.
UX-DR22: Party picker must be an embeddable FrontComposer/Blazor component for party search and selection, not an admin portal, editor, tenant selector, GDPR surface, or stream browser.
UX-DR23: Party picker must support debounced type-ahead display-name search with bounded result count and stable compact layout.
UX-DR24: Party picker must expose selected party id callback to the host application and use party id as the durable selection contract.
UX-DR25: Party picker must support disabled and read-only states.
UX-DR26: Party picker must handle loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, and transient-failure states.
UX-DR27: Party picker must clear stale responses when token, tenant, user, host configuration, selected id, or search options change.
UX-DR28: Party picker must support keyboard operation, visible focus, screen-reader naming, localized labels/status text, and non-color-only status.
UX-DR29: Party picker must render party data, host labels, backend messages, degraded reasons, and localized values through encoded rendering only.
UX-DR30: Party picker must not place names, contacts, identifiers, consent text, search text, tenant ids, tokens, raw ProblemDetails, or raw query payloads in storage keys, telemetry dimensions, URLs, logs, filenames, DOM event names, or JavaScript event payloads.
UX-DR31: Party picker must query through the EventStore-fronted Parties client boundary and must not call retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
UX-DR32: Party picker must receive request/auth context through accepted Parties client/EventStore gateway configuration and must not persist, refresh, parse for authorization, or log tokens.

### FR Coverage Map

FR1: Epic 1 - Party Records and Lifecycle
FR2: Epic 1 - Party Records and Lifecycle
FR3: Epic 1 - Party Records and Lifecycle
FR4: Epic 1 - Party Records and Lifecycle
FR5: Epic 1 - Party Records and Lifecycle
FR6: Epic 1 - Party Records and Lifecycle
FR7: Epic 1 - Party Records and Lifecycle
FR8: Epic 1 - Party Records and Lifecycle
FR9: Epic 1 - Party Records and Lifecycle
FR10: Epic 1 - Party Records and Lifecycle
FR11: Epic 1 - Party Records and Lifecycle
FR12: Epic 1 - Party Records and Lifecycle
FR13: Epic 1 - Party Records and Lifecycle
FR14: Epic 2 - Tenant-Safe Party Search and Retrieval
FR15: Epic 2 - Tenant-Safe Party Search and Retrieval
FR16: Epic 2 - Tenant-Safe Party Search and Retrieval
FR17: Epic 2 - Tenant-Safe Party Search and Retrieval
FR18: Epic 2 - Tenant-Safe Party Search and Retrieval
FR19: Epic 2 - Tenant-Safe Party Search and Retrieval
FR20: Epic 4 - AI Agent Party Management
FR21: Epic 4 - AI Agent Party Management
FR22: Epic 4 - AI Agent Party Management
FR23: Epic 4 - AI Agent Party Management
FR24: Epic 4 - AI Agent Party Management
FR25: Epic 4 - AI Agent Party Management
FR26: Epic 3 - Developer Integration and Local Adoption
FR27: Epic 3 - Developer Integration and Local Adoption
FR28: Epic 3 - Developer Integration and Local Adoption
FR29: Epic 3 - Developer Integration and Local Adoption
FR30: Epic 3 - Developer Integration and Local Adoption
FR31: Epic 3 - Developer Integration and Local Adoption
FR32: Epic 3 - Developer Integration and Local Adoption
FR33: Epic 3 - Developer Integration and Local Adoption
FR34: Epic 5 - Event-Driven Consumer Integration
FR35: Epic 5 - Event-Driven Consumer Integration
FR36: Epic 1 - Party Records and Lifecycle
FR37: Epic 5 - Event-Driven Consumer Integration
FR38: Epic 5 - Event-Driven Consumer Integration
FR39: Epic 2 - Tenant-Safe Party Search and Retrieval
FR40: Epic 2 - Tenant-Safe Party Search and Retrieval
FR41: Epic 2 - Tenant-Safe Party Search and Retrieval
FR42: Epic 1 - Party Records and Lifecycle
FR43: Epic 1 - Party Records and Lifecycle
FR44: Epic 6 - GDPR Compliance Operations
FR45: Epic 6 - GDPR Compliance Operations
FR46: Epic 6 - GDPR Compliance Operations
FR47: Epic 6 - GDPR Compliance Operations
FR48: Epic 6 - GDPR Compliance Operations
FR49: Epic 6 - GDPR Compliance Operations
FR50: Epic 6 - GDPR Compliance Operations
FR51: Epic 6 - GDPR Compliance Operations
FR52: Epic 6 - GDPR Compliance Operations
FR53: Epic 6 - GDPR Compliance Operations
FR54: Epic 6 - GDPR Compliance Operations
FR55: Epic 6 - GDPR Compliance Operations
FR56: Epic 3 - Developer Integration and Local Adoption
FR57: Epic 3 - Developer Integration and Local Adoption
FR58: Epic 3 - Developer Integration and Local Adoption
FR59: Epic 3 - Developer Integration and Local Adoption
FR60: Epic 3 - Developer Integration and Local Adoption
FR61: Epic 3 - Developer Integration and Local Adoption
FR62: Epic 3 - Developer Integration and Local Adoption
FR63: Epic 5 - Event-Driven Consumer Integration
FR64: Epic 2 - Tenant-Safe Party Search and Retrieval
FR65: Epic 7 - Administration Console
FR66: Epic 7 - Administration Console
FR67: Epic 8 - Embeddable Party Picker
FR68: Epic 2 - Tenant-Safe Party Search and Retrieval
FR69: Epic 1 - Party Records and Lifecycle
FR70: Epic 5 - Event-Driven Consumer Integration
FR71: Epic 2 - Tenant-Safe Party Search and Retrieval
FR72: Epic 2 - Tenant-Safe Party Search and Retrieval
FR73: Epic 5 - Event-Driven Consumer Integration
FR74: Epic 4 - AI Agent Party Management

## Release Phase and Coverage Legend

Every epic and story carries two planning fields:

- `Phase`: `MVP`, `MVP preparation`, `v1.1`, `v1.1 preparation`, `v1.2`, or `future`.
- `Coverage type`: `implemented`, `prepared`, or `deferred`.

`Implemented` means the story delivers the user-visible or system-visible requirement in that phase.
`Prepared` means the story lays contract, documentation, compatibility, or architectural groundwork but does not complete the requirement.
`Deferred` means the PRD requirement is intentionally post-MVP and must not be scheduled as MVP implementation work.

## Epic List

### Epic 1: Party Records and Lifecycle

Users can create, update, deactivate, reactivate, and identify parties as the durable source of truth for persons and organizations.

**Phase:** MVP
**Coverage type:** implemented
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR6, FR7, FR8, FR9, FR10, FR11, FR12, FR13, FR36, FR42, FR43, FR69

### Epic 2: Tenant-Safe Party Search and Retrieval

Consumers can list, search, retrieve, filter, and reliably observe party records through tenant-safe projections.

**Phase:** MVP with v1.1 preparation in Story 2.9
**Coverage type:** mixed
**FRs covered:** FR14, FR15, FR16, FR17, FR18, FR19, FR39, FR40, FR41, FR64, FR68, FR71, FR72

### Epic 3: Developer Integration and Local Adoption

Developers can integrate Parties through typed packages, REST APIs, documentation, samples, deployment tooling, versioned APIs, and clear error handling.

**Phase:** MVP
**Coverage type:** implemented
**FRs covered:** FR26, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR56, FR57, FR58, FR59, FR60, FR61, FR62

### Epic 4: AI Agent Party Management

AI agents can find, create, retrieve, update, and deactivate parties through a bounded MCP tool surface with complete responses and forgiving inputs.

**Phase:** MVP
**Coverage type:** implemented
**FRs covered:** FR20, FR21, FR22, FR23, FR24, FR25, FR74

### Epic 5: Event-Driven Consumer Integration

Consuming applications can receive ordered, tenant-aware party events and build their own lifecycle-aware read models.

**Phase:** MVP with v1.1/future preparation in Stories 5.6 and 5.7
**Coverage type:** mixed
**FRs covered:** FR34, FR35, FR37, FR38, FR63, FR70, FR73

### Epic 6: GDPR Compliance Operations

Administrators and DPO workflows can erase, restrict, export, verify, and audit party data with crypto-shredding and subscriber notifications.

**Phase:** v1.1
**Coverage type:** deferred for MVP implementation planning
**FRs covered:** FR44, FR45, FR46, FR47, FR48, FR49, FR50, FR51, FR52, FR53, FR54, FR55

### Epic 7: Administration Console

Administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface.

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**FRs covered:** FR65, FR66

### Epic 8: Embeddable Party Picker

Consuming application developers can embed a tenant-safe, accessible party picker component for search and selection.

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**FRs covered:** FR67

## Epic 1: Party Records and Lifecycle

Users can create, update, deactivate, reactivate, and identify parties as the durable source of truth for persons and organizations.

### Story 1.1: Set Up Initial Project from EventStore Solution Structure

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** Architecture starter setup; supports FR31, FR60

As a developer starting Hexalith.Parties,
I want the initial solution scaffold to follow the EventStore solution-structure pattern,
So that Parties validates the reusable domain-service starter approach before domain behavior is added.

**Acceptance Criteria:**

**Given** the architecture selects the EventStore solution structure pattern as the starter
**When** the initial Parties solution is created
**Then** it includes the documented solution entry point, `/src`, `/tests`, and `/samples` structure
**And** it does not use a generic `dotnet new webapi` scaffold as the architectural baseline.

**Given** the initial project structure is created
**When** build configuration files are reviewed
**Then** central package management, shared build properties, nullable, warnings-as-errors, MinVer, `.editorconfig`, CRLF, UTF-8, and file-scoped namespace conventions are configured consistently with EventStore patterns
**And** package versions are not scattered into individual project files.

**Given** the initial projects are created
**When** project references are inspected
**Then** Contracts, Client, Server, Projections, Parties service, Aspire, AppHost, ServiceDefaults, Testing, and Sample boundaries are represented only as needed for subsequent stories
**And** forbidden dependency directions are not introduced.

**Given** the starter setup is validated
**When** restore/build or structural validation runs
**Then** the initial solution can be restored and built enough to support the first domain story
**And** validation does not require recursive nested submodule initialization.

**Given** starter setup documentation is reviewed
**When** a future domain service uses Parties as a reference
**Then** intentional deviations from EventStore structure are documented
**And** reusable platform starter lessons are visible.

### Story 1.2: Create Party Aggregate with Stable Identity

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR1, FR6, FR7

As an authorized client,
I want to create a person or organization party with a client-generated stable UUID,
So that Parties can become the durable source of truth for party identity.

**Acceptance Criteria:**

**Given** an authorized command with a valid tenant context, client-generated party UUID, and person details
**When** the client creates a person party
**Then** the aggregate emits a party-created event for a person
**And** the party state stores the immutable UUID, party type, person details, active status, and derived display/sort names.

**Given** an authorized command with a valid tenant context, client-generated party UUID, and organization details
**When** the client creates an organization party
**Then** the aggregate emits a party-created event for an organization
**And** the party state stores the immutable UUID, party type, organization details, active status, and derived display/sort names.

**Given** a create command with missing or invalid type-specific details
**When** the command is handled
**Then** the aggregate rejects the command with a typed rejection event
**And** no successful party-created event is emitted.

**Given** an existing party state for the requested party UUID
**When** a create command is handled for the same party UUID
**Then** the aggregate rejects or idempotently skips duplicate creation according to the command idempotency rules
**And** the existing party identity is not changed.

**Given** a party has been created
**When** the state is rehydrated from emitted events
**Then** the rehydrated party preserves the same UUID, party type, active status, details, display name, and sort name.

### Story 1.3: Update Person and Organization Details

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR2, FR3, FR6, FR69

As an authorized client,
I want to update person-specific and organization-specific party details,
So that party records remain accurate as real-world identity details change.

**Acceptance Criteria:**

**Given** an existing active person party
**When** the client updates person details such as first name, last name, date of birth, name prefix, or name suffix
**Then** the aggregate emits a person-details-updated event
**And** the party state reflects the updated person details.

**Given** an existing active organization party
**When** the client updates organization details such as legal name, trading name, legal form, or registration number
**Then** the aggregate emits an organization-details-updated event
**And** the party state reflects the updated organization details.

**Given** an update changes fields used for display name or sort name derivation
**When** the update is applied
**Then** the aggregate recalculates display name and sort name using the documented MVP derivation rules
**And** the updated party state exposes the new derived names.

**Given** a person-specific update is submitted for an organization party, or an organization-specific update is submitted for a person party
**When** the command is handled
**Then** the aggregate rejects the command with a typed rejection event
**And** the existing party details remain unchanged.

**Given** an update command targets a party that does not exist
**When** the command is handled
**Then** the aggregate rejects the command with a typed not-found rejection
**And** no successful update event is emitted.

**Given** a party detail update succeeds
**When** the command result is returned
**Then** the response includes the updated party state
**And** the returned state matches the aggregate state after applying the emitted event.

### Story 1.4: Manage Contact Channels

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR8, FR9, FR10, FR11, FR69

As an authorized client,
I want to add, update, remove, and mark preferred contact channels on a party,
So that party records carry usable structured contact information.

**Acceptance Criteria:**

**Given** an existing active party
**When** the client adds a postal, email, phone, or social contact channel with valid type-specific data
**Then** the aggregate emits a contact-channel-added event
**And** the party state includes the new channel with its type-specific payload.

**Given** an existing contact channel on an active party
**When** the client updates the channel payload or metadata
**Then** the aggregate emits a contact-channel-updated event
**And** the party state reflects the updated channel.

**Given** an existing contact channel on an active party
**When** the client removes the channel
**Then** the aggregate emits a contact-channel-removed event
**And** the party state no longer includes the removed channel.

**Given** multiple contact channels of the same type exist for a party
**When** the client marks one channel as preferred for that type
**Then** the aggregate emits a preferred-contact-channel-changed event
**And** only that channel is preferred for its type.

**Given** a contact channel command targets a missing party, missing channel, invalid channel payload, or removed channel
**When** the command is handled
**Then** the aggregate emits a typed rejection event
**And** no successful contact channel event is emitted.

**Given** a contact channel mutation succeeds
**When** the command result is returned
**Then** the response includes the updated party state
**And** the returned state includes the applied contact channel change.

### Story 1.5: Manage Party Identifiers

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR12, FR13, FR69

As an authorized client,
I want to add and remove jurisdiction-specific identifiers on a party,
So that consumers can associate parties with external legal, tax, or registry references.

**Acceptance Criteria:**

**Given** an existing active party
**When** the client adds a valid identifier such as VAT, SIRET, national ID, or another jurisdiction-specific reference
**Then** the aggregate emits an identifier-added event
**And** the party state includes the identifier type, value, jurisdiction metadata where provided, and stable identifier key.

**Given** an existing identifier on an active party
**When** the client removes that identifier
**Then** the aggregate emits an identifier-removed event
**And** the party state no longer includes the removed identifier.

**Given** the client adds an identifier that already exists on the party
**When** the command is handled
**Then** the aggregate rejects or idempotently skips the duplicate according to the command idempotency rules
**And** the party state does not contain duplicate identifier entries.

**Given** an identifier command targets a missing party, missing identifier, invalid identifier payload, or removed identifier
**When** the command is handled
**Then** the aggregate emits a typed rejection event
**And** no successful identifier event is emitted.

**Given** an identifier mutation succeeds
**When** the command result is returned
**Then** the response includes the updated party state
**And** the returned state includes the applied identifier change.

### Story 1.6: Deactivate and Reactivate Parties

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR4, FR5, FR69

As an authorized client,
I want to deactivate and reactivate party records without deleting their history,
So that lifecycle changes are reversible and auditable.

**Acceptance Criteria:**

**Given** an existing active party
**When** the client deactivates the party
**Then** the aggregate emits a party-deactivated event
**And** the party state is marked inactive without removing party details, contact channels, identifiers, or event history.

**Given** an existing inactive party
**When** the client reactivates the party
**Then** the aggregate emits a party-reactivated event
**And** the party state is marked active again with its prior party data preserved.

**Given** a deactivate command targets an already inactive party
**When** the command is handled
**Then** the aggregate rejects or idempotently skips the duplicate lifecycle change according to the command idempotency rules
**And** the party remains inactive.

**Given** a reactivate command targets an already active party
**When** the command is handled
**Then** the aggregate rejects or idempotently skips the duplicate lifecycle change according to the command idempotency rules
**And** the party remains active.

**Given** a lifecycle command targets a party that does not exist
**When** the command is handled
**Then** the aggregate emits a typed not-found rejection
**And** no successful lifecycle event is emitted.

**Given** a lifecycle mutation succeeds
**When** the command result is returned
**Then** the response includes the updated party state
**And** the returned state reflects the new active/inactive status.

### Story 1.7: Idempotent Commands and Typed Rejections

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR30, FR36

As a developer integrating with Parties,
I want duplicate or invalid lifecycle commands to return stable typed outcomes,
So that retries are safe and failures are understandable without inspecting internal service state.

**Acceptance Criteria:**

**Given** a command with an idempotency key has already succeeded
**When** the same command is retried with the same idempotency key and equivalent payload
**Then** command handling does not create duplicate side effects
**And** the result is stable for the retry scenario.

**Given** a command conflicts with the current aggregate state, such as duplicate creation, invalid type-specific update, missing contact channel, missing identifier, or invalid lifecycle transition
**When** the command is handled
**Then** the aggregate emits the appropriate typed rejection event
**And** no success event for that command is emitted.

**Given** rejection events are replayed during aggregate rehydration
**When** the aggregate applies historical events
**Then** rejection events preserve EventStore replay compatibility
**And** rejection event apply methods do not mutate successful party state.

**Given** an invalid command is rejected
**When** the command result is returned
**Then** the result includes a stable rejection type/code and human-readable message suitable for mapping to external error responses
**And** the response does not expose personal data.

**Given** aggregate tests exercise retry and rejection paths
**When** the test suite runs
**Then** duplicate commands, invalid transitions, missing targets, and no-op replay behavior are covered
**And** tests assert that persisted rejection events do not corrupt party state.

### Story 1.8: Personal Data Marking and Log-Safe Domain Model

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR42, FR43

As a privacy-conscious operator,
I want personal data fields to be explicitly marked and excluded from application logging,
So that MVP domain contracts are prepared for GDPR enforcement without leaking sensitive data.

**Acceptance Criteria:**

**Given** person detail fields are defined in contracts/state
**When** the domain model is reviewed or tested
**Then** first name, last name, date of birth, prefix, suffix, display name, and sort name are marked as personal data where applicable
**And** the markings are discoverable by automated privacy enforcement.

**Given** contact channel payloads are defined for postal, email, phone, and social channels
**When** the domain model is reviewed or tested
**Then** all contact channel payload values that may identify a person are marked as personal data
**And** organization contact channels are treated conservatively when they can identify a natural person.

**Given** identifier values are defined for VAT, SIRET, national ID, or other references
**When** the domain model is reviewed or tested
**Then** identifier values that may identify a person are marked as personal data
**And** the model supports future type-dependent GDPR classification.

**Given** domain commands, events, rejections, or state transitions are logged
**When** application logging occurs
**Then** logs include safe metadata such as aggregate id, tenant/correlation metadata, command/event type, and outcome code
**And** logs do not include personal data field values, raw command payloads, or raw event payloads.

**Given** privacy-marking tests run
**When** they inspect known personal-data contract fields
**Then** required fields are marked consistently
**And** regressions in personal data marking fail the tests.

### Story 1.9: Return Updated Party State from Mutations

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR69; supports FR1-FR13

As a client application,
I want successful party mutation commands to return the resulting party state,
So that I can update my UI or workflow without issuing an immediate follow-up query.

**Acceptance Criteria:**

**Given** a create party command succeeds
**When** the command result is returned
**Then** the response includes the complete created party state
**And** the party state reflects all events emitted by the command.

**Given** a party detail, contact channel, identifier, or lifecycle mutation succeeds
**When** the command result is returned
**Then** the response includes the updated party state
**And** the returned state reflects the aggregate state after applying the mutation event.

**Given** a command emits multiple events in one aggregate turn
**When** the command result is assembled
**Then** the returned party state reflects the final aggregate state after all emitted events are applied in order
**And** no stale pre-command state is returned.

**Given** a mutation command is rejected
**When** the command result is returned
**Then** no updated party state is presented as a successful mutation result
**And** the rejection outcome remains explicit and typed.

**Given** aggregate command tests verify mutation responses
**When** the test suite runs
**Then** create, update, contact, identifier, deactivate, and reactivate success paths assert returned state correctness
**And** rejection paths assert that failed mutations do not return misleading updated state.

## Epic 2: Tenant-Safe Party Search and Retrieval

Consumers can list, search, retrieve, filter, and reliably observe party records through tenant-safe projections.

### Story 2.1: Build Party Detail Projection

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR18; supports FR19

As a consumer of party data,
I want each party to have a read-optimized detail projection,
So that I can retrieve complete party details without rehydrating the aggregate on every query.

**Acceptance Criteria:**

**Given** party lifecycle, detail, contact channel, identifier, and lifecycle events are published for a party
**When** the projection handler processes those events in order
**Then** it builds a party detail projection containing party id, type, active status, person or organization details, display/sort names, contact channels, identifiers, and relevant timestamps.

**Given** a party detail projection handler receives a rejection event
**When** the event is applied during normal processing or rebuild
**Then** the handler does not mutate the successful party detail state
**And** projection replay remains compatible with persisted rejection events.

**Given** a party detail projection is persisted through its actor wrapper
**When** the actor stores projection state
**Then** the DAPR actor state key uses the documented tenant/party detail convention
**And** the handler logic remains free of DAPR dependencies.

**Given** duplicate or late repeated events are delivered by pub/sub
**When** the projection handler processes them
**Then** processing is idempotent where event identity or sequence data allows
**And** duplicate delivery does not create duplicate contact channels or identifiers.

**Given** projection handler tests run without DAPR infrastructure
**When** they replay representative party event sequences
**Then** they verify detail state after create, update, contact channel, identifier, deactivate, and reactivate flows
**And** they verify event ordering assumptions documented by the architecture.

**Given** a party detail projection has been built for the current tenant
**When** the same party is retrieved through the documented detail-query path
**Then** the query returns the projected party details with active status, type-specific details, display/sort names, contacts, identifiers, and timestamps
**And** the result proves the projection is observable through consumer-facing read behavior rather than only internal handler state.

### Story 2.2: Build Tenant Party Index Projection

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR14, FR19, FR68

As a consumer browsing party records,
I want each tenant to have a read-optimized party index,
So that I can list and filter parties without scanning aggregate event streams.

**Acceptance Criteria:**

**Given** party create, detail update, lifecycle, and identifier/contact summary events are published for a tenant
**When** the party index projection handler processes those events
**Then** it maintains lightweight party index entries with party id, type, display name, sort name, active status, created timestamp, last-modified timestamp, and non-PII status indicators.

**Given** an indexed party changes display name, sort name, type-specific details, or active status
**When** the corresponding event is applied
**Then** the tenant index entry is updated consistently
**And** stale values are not retained in the index.

**Given** a tenant has many party index entries
**When** the index actor persists state
**Then** it uses the architecture's partition strategy abstraction
**And** v1.0 can use single-key storage while preserving a path to partitioned storage for larger tenants.

**Given** burst event delivery occurs for many party changes in the same tenant
**When** the index actor processes events
**Then** it can batch persistence using configurable batch size or timing
**And** the projection consistency target remains observable.

**Given** duplicate or repeated events are delivered
**When** the index handler processes them
**Then** the index remains idempotent where event identity or sequence data allows
**And** duplicate entries are not created.

**Given** projection handler tests run without DAPR infrastructure
**When** they replay representative tenant event streams
**Then** they verify create, update, deactivate, reactivate, date metadata, and duplicate delivery behavior
**And** the handler remains free of DAPR dependencies.

**Given** a tenant party index projection has been built for the current tenant
**When** consumers list, filter, or display-name search parties through the documented read path
**Then** results are served from the tenant-safe index with pagination, type/status/date filtering, display-name matching, and bounded match metadata where applicable
**And** the story demonstrates that the index enables observable consumer browsing and search behavior.

### Story 2.3: Query Party Details by ID

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR18, FR39, FR40, FR41, FR64

As a consumer of party data,
I want to retrieve full party details by party id,
So that applications and AI tools can display or use the current party record without replaying events.

**Acceptance Criteria:**

**Given** a valid authenticated request with tenant context and an existing party id for that tenant
**When** the consumer queries party details by id
**Then** the service reads the party detail projection
**And** returns the current party detail record for that tenant.

**Given** a party detail projection exists for another tenant
**When** a consumer from a different tenant queries the same party id
**Then** the query fails closed
**And** the response does not reveal whether the party exists in another tenant.

**Given** the requested party id has no readable projection in the current tenant
**When** the consumer queries party details by id
**Then** the service returns a bounded not-found or unavailable result
**And** no aggregate event replay is performed as an implicit query fallback.

**Given** the party has been deactivated
**When** the consumer queries party details by id
**Then** the returned detail record includes inactive status
**And** the record remains inspectable unless future GDPR erasure state prevents it.

**Given** the projection actor reports stale, rebuilding, or degraded state
**When** the consumer queries party details by id
**Then** the response includes a bounded freshness/degradation indicator
**And** personal data is not logged while handling the degraded result.

**Given** detail query tests run
**When** they cover success, missing tenant, cross-tenant, not-found, inactive, stale, and degraded states
**Then** tenant isolation and bounded response behavior are verified.

### Story 2.4: List and Filter Parties

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR14, FR39, FR40, FR41, FR64, FR68

As a consumer browsing party records,
I want to list parties with pagination and filters,
So that I can navigate a tenant's party directory efficiently.

**Acceptance Criteria:**

**Given** a valid authenticated request with tenant context
**When** the consumer lists parties
**Then** the service reads the tenant party index projection
**And** returns a paginated result ordered by the documented sort behavior.

**Given** the consumer provides a party type filter
**When** the list query is executed
**Then** the result includes only person or organization entries matching that filter
**And** the filter is applied within the current tenant only.

**Given** the consumer provides an active status filter
**When** the list query is executed
**Then** the result includes only entries matching the active/inactive status
**And** deactivated parties are not silently hidden unless the filter requests that behavior.

**Given** the consumer provides a creation date or last-modified date range
**When** the list query is executed
**Then** the result includes only entries whose indexed metadata matches the requested range
**And** invalid date ranges return a bounded validation error.

**Given** a tenant has more results than one page
**When** the consumer requests a page using the documented paging contract
**Then** the response returns the requested page and paging metadata
**And** the service does not return unbounded result sets.

**Given** the tenant index is stale, rebuilding, or degraded
**When** the consumer lists parties
**Then** the response includes a bounded freshness/degradation indicator
**And** only data proven to belong to the current tenant is returned.

**Given** list/filter tests run
**When** they cover pagination, type filter, active filter, date filters, invalid ranges, empty results, stale state, and cross-tenant isolation
**Then** all list behavior is verified against the tenant index projection.

### Story 2.5: Search Parties by Display Name with Match Metadata

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR15, FR17; supports FR20, FR39, FR40, FR41, FR64

As a consumer resolving party identity,
I want to search parties by display name with match metadata,
So that humans and AI agents can rank candidates confidently in MVP name-based lookup scenarios.

**Acceptance Criteria:**

**Given** a valid authenticated request with tenant context and a display-name search term
**When** the consumer searches parties
**Then** the service searches the tenant party index by display name
**And** returns only matching entries for the current tenant.

**Given** the search term exactly matches a party display name
**When** the search result is returned
**Then** the result includes match metadata indicating `displayName` and exact match
**And** the matched party is ranked ahead of weaker matches.

**Given** the search term is a prefix or contained text within display names
**When** the search result is returned
**Then** the result includes match metadata indicating `displayName` and the match type
**And** results are ordered by the documented match ranking rules.

**Given** the consumer expects email or identifier search in MVP
**When** the search query is evaluated
**Then** the service does not pretend to search unavailable fields
**And** the response contract reserves email and identifier match metadata for the future search model.

**Given** no parties match the display-name search
**When** the search result is returned
**Then** the response is an empty bounded result set
**And** no cross-tenant or personal-data leakage occurs.

**Given** the tenant index is stale, rebuilding, or degraded
**When** the consumer searches parties
**Then** the response includes a bounded freshness/degradation indicator
**And** only current-tenant data is returned.

**Given** search tests run
**When** they cover exact, prefix, contains, empty, inactive, paginated, degraded, and cross-tenant cases
**Then** match metadata and tenant isolation behavior are verified.

### Story 2.6: Enforce Tenant-Safe Projection Reads

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR39, FR40, FR41

As a tenant-scoped consumer,
I want every read-side query to fail closed when tenant context is missing or mismatched,
So that party data cannot leak across tenants through projections, search, or degraded states.

**Acceptance Criteria:**

**Given** a read query is submitted without a valid tenant identity
**When** the query reaches the projection read path
**Then** the query is rejected fail-closed
**And** no projection actor state is read.

**Given** a query includes or implies a party id that belongs to another tenant
**When** the current tenant attempts to retrieve, list, filter, or search
**Then** the service returns a bounded forbidden/not-found-style result
**And** the response does not confirm whether the party exists in another tenant.

**Given** a projection actor receives a request with a tenant context
**When** actor state keys or partition keys are resolved
**Then** they are derived from authenticated tenant context
**And** tenant identity is never accepted from request payload filters.

**Given** projection state is stale, rebuilding, corrupt, or degraded
**When** a read query is handled
**Then** fail-closed tenant checks still run before any data is returned
**And** degraded responses preserve only data proven to belong to the current tenant.

**Given** tenant isolation tests run with multiple concurrent tenants
**When** the tests create overlapping party ids, display names, filters, and degraded conditions
**Then** no list, search, or detail query returns data from another tenant
**And** missing/invalid tenant context rejects consistently.

**Given** application logging records projection read activity
**When** tenant-safe reads succeed or fail
**Then** logs include bounded metadata such as operation, tenant-safe outcome, and correlation id
**And** logs do not include personal data, raw query payloads, or tenant membership payloads.

### Story 2.7: Handle Projection Freshness and Graceful Degradation

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR19, FR64, FR71

As a consumer of eventually consistent party data,
I want read responses to expose freshness and degradation status,
So that my application can behave safely when projections lag or infrastructure is partially unavailable.

**Acceptance Criteria:**

**Given** a party mutation has been accepted and published
**When** the corresponding projection event is processed under normal load
**Then** the changed party becomes visible through detail, list, and search reads within the documented eventual consistency window.

**Given** a read projection is current
**When** the consumer performs detail, list, or search queries
**Then** the response indicates normal/current projection state
**And** no stale warning is included.

**Given** projection state is stale, rebuilding, or behind the latest known event position
**When** the consumer performs detail, list, or search queries
**Then** the response includes a bounded freshness indicator
**And** the result does not silently claim to be fully current.

**Given** write-side components or event processing are unavailable but read projection state is still readable
**When** the consumer performs read operations
**Then** reads continue from cached/projection state where safe
**And** the response includes a degraded or stale indicator.

**Given** projection state cannot be trusted for tenant-safe reads
**When** the consumer performs read operations
**Then** the service fails closed instead of returning potentially unsafe data
**And** the response includes a bounded unavailable/degraded outcome.

**Given** freshness/degradation tests run
**When** they simulate current, stale, rebuilding, unavailable write-side, unavailable projection, and unsafe projection states
**Then** each read path returns the documented status and preserves tenant isolation.

### Story 2.8: Projection Rebuild and Health Monitoring

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR64, FR71

As an operator of the Parties service,
I want projection corruption and rebuild state to be detected and handled predictably,
So that read models can recover without unsafe data exposure.

**Acceptance Criteria:**

**Given** a projection actor activates with valid state
**When** health checks inspect the projection
**Then** the projection is reported healthy
**And** normal read behavior remains available.

**Given** a projection actor detects corrupt, malformed, or incompatible state
**When** it activates or attempts to read state
**Then** it marks the projection degraded or rebuilding
**And** unsafe party data is not returned.

**Given** rebuild tooling replays party events through pure projection handlers
**When** a party detail or tenant index projection is rebuilt
**Then** the rebuilt state matches the state produced by normal event processing
**And** rejection events do not mutate successful projection state.

**Given** a rebuild is in progress for a tenant or party
**When** consumers query affected detail, list, or search paths
**Then** responses include bounded rebuilding/degraded status
**And** tenant isolation checks still run before any data is returned.

**Given** rebuild completes successfully
**When** the projection health state is refreshed
**Then** the projection returns to healthy/current status
**And** subsequent reads use the rebuilt projection state.

**Given** rebuild fails or cannot prove tenant-safe state
**When** consumers query affected paths
**Then** the service fails closed for unsafe reads
**And** operational logging records bounded metadata and correlation id without personal data.

**Given** projection health/rebuild tests run
**When** they simulate healthy, corrupt, rebuilding, successful rebuild, failed rebuild, and cross-tenant conditions
**Then** status transitions and read responses match the documented behavior.

**Given** projection health is exposed through the service readiness or operational health surface
**When** a projection is healthy, rebuilding, degraded, or unsafe
**Then** the health/readiness response reports the bounded projection state without personal data
**And** detail, list, and search reads show the matching freshness/degradation behavior.

### Story 2.9: Prepare Deferred Search and Temporal Query Extensions

**Phase:** v1.1 preparation
**Coverage type:** non-feature preparation / prepared-deferred
**Scheduling label:** preparation-only; does not deliver active MVP runtime behavior for deferred capabilities
**Requirements prepared:** FR16, FR72; future portions of FR15, FR17
**MVP implementation coverage:** none beyond preserving event/history and contract extension points required by earlier MVP stories

As a future maintainer of Parties search and audit features,
I want the MVP read model contracts to reserve explicit extension points for semantic search and temporal name queries,
So that v1.1 can add those capabilities without breaking existing consumers.

**Acceptance Criteria:**

**Given** MVP display-name search responses include match metadata
**When** response contracts are reviewed
**Then** `displayName` is the only active searchable field in MVP
**And** email and identifier match fields are reserved for the future search model without claiming current support.

**Given** semantic search is deferred to v1.1
**When** the MVP projection contracts and architecture notes are reviewed
**Then** they identify semantic search as a pluggable projection/search extension
**And** no MVP story requires a semantic search backend to function.

**Given** temporal name query is deferred to v1.1
**When** party name-changing events are persisted and projections are built
**Then** event history preserves enough name-change information for a future temporal query API
**And** the MVP does not expose a misleading temporal query endpoint.

**Given** future extension placeholders are added to contracts or documentation
**When** compatibility tests run
**Then** existing MVP consumers remain source-compatible
**And** no required runtime dependency on a future search engine is introduced.

**Given** a consumer attempts to use unsupported semantic or temporal query capability in MVP
**When** the request reaches the read/query surface
**Then** the service returns a bounded unsupported-capability response
**And** the response points to documented deferred capability behavior without exposing internal implementation details.

## Epic 3: Developer Integration and Local Adoption

Developers can integrate Parties through typed packages, REST APIs, documentation, samples, deployment tooling, versioned APIs, and clear error handling.

### Story 3.1: Publish Stable Contracts Package

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR33; supports FR37, FR42

As a .NET developer,
I want a stable `Hexalith.Parties.Contracts` package,
So that I can reference party commands, events, models, and results without inheriting service infrastructure dependencies.

**Acceptance Criteria:**

**Given** the Contracts project is built
**When** package dependencies are inspected
**Then** it has no runtime dependencies beyond the target framework and approved serialization/contract basics
**And** it does not reference hosting, DAPR, MediatR, FluentValidation, UI, or infrastructure packages.

**Given** a developer references the Contracts package
**When** they use party commands, events, value objects, query models, rejection models, and result types
**Then** all public contract types compile from the package
**And** XML documentation is available for public APIs.

**Given** event contracts are reviewed
**When** future compatibility is assessed
**Then** event shapes are append-only and additive
**And** forward-compatible placeholders such as party merge are represented without forcing runtime behavior.

**Given** personal data markers are present in contracts
**When** consuming applications inspect contract metadata
**Then** personal data classification is visible where required
**And** consuming applications do not need server infrastructure packages to read that metadata.

**Given** package validation tests run
**When** they inspect references and public API shape
**Then** forbidden dependencies fail the tests
**And** breaking public contract drift is detected before package publication.

### Story 3.2: Provide Typed Parties Client Registration

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR26, FR27, FR28

As a .NET developer,
I want to register a typed Parties client with one DI call,
So that I can send commands and queries without learning DAPR or service internals.

**Acceptance Criteria:**

**Given** a consuming .NET application references `Hexalith.Parties.Client`
**When** the developer calls the documented `AddPartiesClient()` registration
**Then** command and query client abstractions are registered in dependency injection
**And** the consuming app does not need to reference DAPR, MediatR, FluentValidation, Server, Projections, or the Parties service project.

**Given** the typed command client is resolved from DI
**When** the developer sends create, update, contact, identifier, deactivate, or reactivate commands
**Then** the client sends requests through the configured EventStore-fronted gateway/API boundary
**And** returns typed success or rejection results.

**Given** the typed query client is resolved from DI
**When** the developer queries by id, lists, filters, or searches parties
**Then** the client sends read requests through the configured gateway/API boundary
**And** returns typed query results with freshness/degradation metadata where applicable.

**Given** required client configuration such as base address or authentication context is missing
**When** the client is registered or used
**Then** configuration validation returns a clear developer-facing error
**And** no request is sent with incomplete configuration.

**Given** client package dependency tests run
**When** package dependencies are inspected
**Then** the Client package has fewer than 10 transitive dependencies totaling under 5 MB where measurable
**And** forbidden service infrastructure dependencies fail the tests.

### Story 3.3: Expose Versioned REST Party API

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR29, FR57; supports FR27, FR28, FR39, FR40, FR41

As a developer using any programming language,
I want to interact with Parties through a versioned REST API,
So that I can integrate party management without using the .NET client package.

**Acceptance Criteria:**

**Given** an authenticated HTTP client with valid tenant credentials
**When** it calls `/api/v1/parties` command endpoints for create, update, contact channel, identifier, deactivate, or reactivate operations
**Then** the API routes requests through the same domain command path as typed clients
**And** tenant identity is taken from authenticated credentials, not request payloads.

**Given** an authenticated HTTP client with valid tenant credentials
**When** it calls `/api/v1/parties` query endpoints for get by id, list, filter, or display-name search
**Then** the API routes requests through the tenant-safe projection query path
**And** returns typed response bodies consistent with the Contracts package.

**Given** unsupported or future API versions are requested
**When** the HTTP client calls the service
**Then** the API returns a documented versioning response
**And** supported v1 endpoints continue to coexist during future deprecation periods.

**Given** a request is missing authentication, missing tenant context, or contains mismatched tenant payload data
**When** the API handles the request
**Then** it rejects fail-closed before command or projection state is accessed
**And** the response does not leak cross-tenant existence information.

**Given** REST API tests run
**When** they cover command, query, versioning, auth, tenant, validation, and unsupported version cases
**Then** REST behavior is verified without introducing public REST endpoints in the actor host if architecture forbids that boundary.

### Story 3.4: Map Domain Rejections to ProblemDetails

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR30, FR58

As a developer integrating with Parties,
I want domain rejections to map to standardized HTTP error responses,
So that I can understand failures and corrective actions without debugging service internals.

**Acceptance Criteria:**

**Given** a domain command is rejected with a typed rejection event
**When** the rejection reaches the REST API boundary
**Then** it is mapped to an RFC 7807 ProblemDetails response
**And** the response includes stable type URI, title, status, human-readable detail, and corrective action where available.

**Given** a validation failure occurs before command handling
**When** the API returns the error response
**Then** it uses a standardized validation ProblemDetails shape
**And** field-level errors are bounded and encoded.

**Given** a tenant, authorization, or cross-tenant rejection occurs
**When** the API returns the error response
**Then** it fails closed with the documented status code
**And** it does not reveal whether data exists in another tenant.

**Given** infrastructure or projection degradation prevents a safe response
**When** the API returns the error response
**Then** it uses a bounded ProblemDetails response with retry/degradation guidance
**And** no raw exception, raw payload, or personal data is exposed.

**Given** rejection mapping tests run
**When** they cover duplicate creation, invalid type update, missing party, missing channel, missing identifier, invalid lifecycle transition, validation failure, auth failure, and degraded read/write failures
**Then** each case maps to the documented ProblemDetails response
**And** logs contain only safe metadata.

### Story 3.5: Generate OpenAPI and Error Catalog

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR56, FR58; supports FR29, FR30, FR57, FR62

As a developer evaluating or integrating Parties,
I want browsable API documentation and a documented error catalog,
So that I can understand commands, queries, responses, and failure modes without reading service code.

**Acceptance Criteria:**

**Given** the Parties REST API is running in development/documentation mode
**When** a developer opens the generated API specification
**Then** the OpenAPI 3.x document includes v1 command and query endpoints, request schemas, response schemas, auth requirements, and ProblemDetails responses.

**Given** the OpenAPI document is generated
**When** contract schemas are inspected
**Then** they align with the published Contracts package models
**And** undocumented or unsupported future capabilities are not advertised as available.

**Given** domain rejection mappings exist
**When** the error catalog is generated or reviewed
**Then** each stable rejection/error type includes type URI, status code, title, explanation, corrective action, and example response where appropriate.

**Given** compliance warning behavior is active for MVP
**When** the API documentation is viewed
**Then** it documents that MVP is not GDPR-compliant for regulated EU personal data until v1.1
**And** startup/API warning behavior is visible to developers.

**Given** API documentation tests run
**When** they validate OpenAPI generation and error catalog coverage
**Then** missing endpoints, missing ProblemDetails schemas, undocumented rejection types, or future capability leakage fail the tests.

### Story 3.6: Enable One-Command Local Run

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR31, FR60; supports FR71

As a developer trying Parties locally,
I want to start the full local system with one documented command,
So that I can evaluate and develop against the service without hand-wiring infrastructure.

**Acceptance Criteria:**

**Given** a developer has documented prerequisites installed
**When** they run the documented Aspire/AppHost command from the repository root
**Then** the Parties service, required DAPR sidecar configuration, state/pubsub backing services, and health/readiness endpoints start for local evaluation.

**Given** the local system starts successfully
**When** the developer opens the Aspire dashboard or equivalent local diagnostics
**Then** Parties service resources, DAPR sidecar status, and backing service health are visible
**And** the developer can identify the REST/API endpoint for first commands.

**Given** the local system is not ready yet
**When** health or readiness is checked
**Then** readiness remains false until required infrastructure and the Parties service can accept requests
**And** readiness becomes true within the documented cold-start target under normal local conditions.

**Given** a required local dependency is missing or misconfigured
**When** the developer starts the system
**Then** startup fails with actionable guidance
**And** no silent partial configuration is treated as production-ready.

**Given** local-run validation tests or scripted smoke checks run
**When** they start the AppHost in the supported local mode
**Then** they verify health/readiness and at least one authenticated or documented development-mode request path
**And** they do not require recursive submodule initialization.

### Story 3.7: Write Getting Started Documentation

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR32; supports FR31, FR56, FR60, FR62

As a developer new to Parties,
I want a tested getting-started guide,
So that I can deploy locally and send my first command as a self-service experience.

**Acceptance Criteria:**

**Given** a developer opens the getting-started guide
**When** they read the prerequisites and local setup section
**Then** they can identify required .NET, Docker/container tooling, DAPR/Aspire expectations, and supported development mode
**And** the guide does not require recursive submodule initialization.

**Given** a developer follows the guide on a clean machine with documented prerequisites
**When** they start the local system
**Then** they can reach a healthy/readiness-confirmed Parties instance within the documented deployment target
**And** troubleshooting steps explain common startup failures.

**Given** the local system is running
**When** the developer follows the first-command walkthrough
**Then** they can send a successful `CreateParty` request through REST
**And** understand the command -> event -> projection flow at a high level.

**Given** the developer continues the walkthrough
**When** they perform the first query
**Then** they can retrieve or search for the created party
**And** the guide explains eventual consistency and freshness indicators.

**Given** the developer wants to integrate from .NET
**When** they follow the client package section
**Then** they can register `AddPartiesClient()` and send a typed command/query
**And** the guide explains required configuration without exposing service internals.

**Given** the guide includes MVP compliance positioning
**When** the developer reads the warning
**Then** it clearly states that MVP is not for regulated EU personal data until v1.1 GDPR features are active
**And** it links to the emergency manual erasure procedure if included elsewhere.

**Given** documentation validation is performed
**When** a non-author or scripted doc check follows the guide
**Then** broken commands, missing prerequisites, stale endpoint names, or unclear first-command steps are caught.

### Story 3.8: Provide Runnable Sample Integration

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR59; supports FR26, FR27, FR28, FR29, FR35, FR38

As a developer evaluating Parties,
I want a runnable sample consuming application,
So that I can see command, query, event subscription, and MCP usage in one concrete integration.

**Acceptance Criteria:**

**Given** the sample application is built
**When** a developer opens it
**Then** it demonstrates `AddPartiesClient()` registration and required configuration
**And** it does not reference Server, Projections, DAPR actors, or service internals.

**Given** the sample is run against a local Parties instance
**When** the developer executes the command scenario
**Then** it creates a party and adds representative contact/identifier data
**And** it handles typed success and rejection outcomes.

**Given** the sample is run against a local Parties instance
**When** the developer executes the query scenario
**Then** it retrieves, lists, filters, and searches parties through the client/API boundary
**And** it displays freshness/degradation metadata where applicable.

**Given** party events are published
**When** the sample event subscription/read-model scenario runs
**Then** it demonstrates how a consuming application handles party lifecycle events
**And** it includes guidance for future `PartyErased` cleanup handling even if GDPR is deferred.

**Given** MCP support is available in the local topology
**When** the sample demonstrates AI agent usage
**Then** it shows the intended MCP interaction path or configuration
**And** it does not duplicate MCP tool implementation in the sample.

**Given** sample validation runs in CI or a scripted check
**When** the sample is built and smoke-tested
**Then** it compiles, runs its documented scenarios, and remains aligned with the current client/API contracts.

### Story 3.9: Add Deployment Security Validation

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR61; supports FR39, FR40, FR41

As an operator preparing Parties for deployment,
I want validation tooling for security-critical configuration,
So that unsafe tenant, auth, and DAPR access-control settings are caught before production use.

**Acceptance Criteria:**

**Given** deployment validation tooling is run against a Parties deployment configuration
**When** JWT/authentication settings are inspected
**Then** missing issuer, audience, signing/key configuration, or fail-closed behavior is reported as a blocking validation failure.

**Given** deployment validation tooling inspects tenant configuration
**When** tenant resolution or tenant metadata settings are incomplete
**Then** the tool reports a blocking validation failure
**And** it explains that tenant identity must come from authenticated credentials, not request payloads.

**Given** deployment validation tooling inspects DAPR access-control configuration
**When** wildcard app ids, wildcard operation paths, missing deny-by-default behavior, or missing Parties operation rules are detected
**Then** the tool reports a blocking validation failure
**And** the output identifies the unsafe configuration category without exposing secrets.

**Given** deployment validation tooling inspects transport settings
**When** TLS or production transport requirements are not met
**Then** it reports the issue with actionable remediation guidance
**And** local development exceptions are clearly scoped.

**Given** validation output is generated
**When** failures or warnings are reported
**Then** output is bounded, machine-readable where useful, and safe for logs/artifacts
**And** secrets, tokens, claims dictionaries, tenant membership payloads, and personal data are not printed.

**Given** validation tests run
**When** they cover valid config, missing auth, unsafe DAPR ACLs, missing tenant settings, development-mode exceptions, and redaction
**Then** validation behavior matches deployment security expectations.

### Story 3.10: Display MVP Compliance Warning

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR62

As a developer or operator evaluating MVP Parties,
I want a persistent compliance warning until GDPR features are active,
So that I do not mistakenly use MVP for regulated EU personal data.

**Acceptance Criteria:**

**Given** GDPR compliance features are not active
**When** the Parties service starts
**Then** startup logs and health/metadata surfaces expose a bounded MVP compliance warning
**And** the warning states that MVP is not for regulated EU personal data.

**Given** a REST API response is returned while GDPR features are inactive
**When** the response is produced
**Then** it includes the documented non-dismissable compliance warning header or metadata
**And** the warning does not include personal data or environment secrets.

**Given** OpenAPI, README, and getting-started documentation are viewed
**When** GDPR features are inactive or MVP docs are read
**Then** the compliance warning is visible and consistent across documentation surfaces
**And** it identifies v1.1 GDPR features as the activation point.

**Given** GDPR features become active in a future version
**When** the compliance-warning configuration is updated
**Then** the MVP warning can be removed or replaced through an explicit documented switch
**And** tests prevent accidental silent removal before activation criteria are met.

**Given** compliance warning tests run
**When** they cover startup, API metadata/header, documentation expectations, inactive mode, and activation switch behavior
**Then** the warning remains non-dismissable until explicitly activated or replaced.

## Epic 4: AI Agent Party Management

AI agents can find, create, retrieve, update, and deactivate parties through a bounded MCP tool surface with complete responses and forgiving inputs.

### Story 4.1: Register Bounded MCP Tool Surface

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR23; supports NFR26

As an AI agent integrator,
I want Parties to expose a small, well-documented MCP tool surface,
So that agents can reliably discover and use party management capabilities.

**Acceptance Criteria:**

**Given** the Parties MCP capability is enabled
**When** an MCP client lists available tools
**Then** exactly the canonical party tools are exposed: `find_parties`, `get_party`, `create_party`, `update_party`, and `delete_party`
**And** no internal command, projection, actor, admin, or infrastructure tools are exposed.

**Given** an MCP client inspects tool schemas
**When** schemas are returned
**Then** each schema includes clear descriptions, required fields, optional fields, defaults where applicable, and bounded validation guidance
**And** schemas are optimized for AI agent use rather than mirroring every low-level domain command.

**Given** the service has REST and typed client surfaces
**When** MCP tools are registered
**Then** they route through the same command/query paths as other consumers
**And** they do not bypass validation, tenant context, authorization, or projection safeguards.

**Given** an unknown or unsupported tool name is requested
**When** the MCP server handles the request
**Then** it rejects the request with a structured MCP error
**And** suggests supported tool names where safe.

**Given** MCP registration tests run
**When** they inspect registered tools and schemas
**Then** exactly the canonical tools are present
**And** forbidden internal capabilities are absent.

### Story 4.2: Implement AI-Friendly Find Parties Tool

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR20, FR23, FR25; supports FR14, FR15, FR17

As an AI agent,
I want to find and list parties using a forgiving `find_parties` tool,
So that I can resolve party identity from partial user-provided names.

**Acceptance Criteria:**

**Given** an MCP request includes a display-name search term
**When** `find_parties` is invoked
**Then** it searches the tenant-safe party index by display name
**And** returns candidates with party id, display name, party type, active status, and match metadata.

**Given** an MCP request omits a search term but requests listing behavior
**When** `find_parties` is invoked
**Then** it returns a bounded paginated party list for the current tenant
**And** supports available type, active, created-date, and modified-date filters.

**Given** partial or incomplete optional input is provided
**When** `find_parties` normalizes the request
**Then** omitted optional fields default sensibly
**And** validation errors clearly state what required or invalid fields need correction.

**Given** an AI agent expects email, identifier, or semantic search in MVP
**When** `find_parties` handles the request
**Then** it communicates that MVP supports display-name search only
**And** it does not claim unsupported search fields were evaluated.

**Given** the tenant context is missing, invalid, or unauthorized
**When** `find_parties` is invoked
**Then** it fails closed with a structured MCP error
**And** no cross-tenant existence information is exposed.

**Given** the projection is stale, rebuilding, or degraded
**When** `find_parties` returns results
**Then** the response includes bounded freshness/degradation metadata
**And** only current-tenant data proven safe is returned.

**Given** MCP tests run for `find_parties`
**When** they cover search, list, filters, defaults, unsupported fields, empty results, degraded state, and tenant failures
**Then** responses are predictable for AI agents and preserve tenant isolation.

### Story 4.3: Implement Get Party Tool

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR23; supports FR18, FR25

As an AI agent,
I want to retrieve complete party details by id using `get_party`,
So that I can inspect the current party record before deciding what action to take.

**Acceptance Criteria:**

**Given** an MCP request includes a valid party id for the current tenant
**When** `get_party` is invoked
**Then** it retrieves the party detail through the tenant-safe query path
**And** returns complete party details including type, active status, details, contact channels, identifiers, and freshness/degradation metadata where applicable.

**Given** the party id is missing or malformed
**When** `get_party` normalizes the request
**Then** it returns a structured validation error
**And** the error clearly states the required party id format.

**Given** the party does not exist or is not visible to the current tenant
**When** `get_party` is invoked
**Then** it returns a bounded not-found/unavailable MCP response
**And** it does not reveal whether the party exists in another tenant.

**Given** the projection is stale, rebuilding, degraded, or cannot prove tenant-safe state
**When** `get_party` is invoked
**Then** it includes bounded status metadata or fails closed when unsafe
**And** no raw projection or infrastructure error leaks to the agent.

**Given** the party is inactive
**When** `get_party` returns the party details
**Then** the response clearly indicates inactive status
**And** it does not treat soft deactivation as GDPR erasure.

**Given** MCP tests run for `get_party`
**When** they cover success, invalid id, not found, cross-tenant, inactive, degraded, and unsafe states
**Then** responses are structured, predictable, and tenant-safe.

### Story 4.4: Implement Composite Create Party Tool

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR21, FR24, FR25

As an AI agent,
I want to create a complete party in one `create_party` tool call,
So that a user's natural-language contact request becomes a usable party record without multiple fragile steps.

**Acceptance Criteria:**

**Given** an MCP request includes enough information to create a person with optional contact channels and identifiers
**When** `create_party` is invoked
**Then** the MCP layer normalizes the request into a `CreatePartyComposite` command
**And** the aggregate processes the party details, channels, and identifiers in one actor turn.

**Given** an MCP request includes enough information to create an organization with optional contact channels and identifiers
**When** `create_party` is invoked
**Then** the MCP layer normalizes the request into a `CreatePartyComposite` command
**And** the aggregate creates the complete organization party in one actor turn.

**Given** optional fields are omitted from the MCP request
**When** `create_party` normalizes input
**Then** sensible documented defaults are applied where valid
**And** missing required fields return clear structured validation errors.

**Given** a composite create command succeeds
**When** `create_party` returns its MCP response
**Then** the response includes the complete created party record, not just the id
**And** the returned record reflects all created details, contact channels, identifiers, active status, and derived names.

**Given** duplicate sub-operations, existing identifiers/channels, or conflicting payloads are submitted
**When** the composite command is handled
**Then** duplicate-safe/idempotent rules are applied by the aggregate
**And** the MCP response explains skipped, applied, or rejected sub-operations without exposing internal state.

**Given** the request exceeds configured composite payload limits
**When** `create_party` validates the request
**Then** it returns a structured validation error
**And** no partial party is created.

**Given** the command is rejected
**When** `create_party` returns its MCP response
**Then** no partial success is reported
**And** the error is structured, clear, and safe for an AI agent to use in follow-up reasoning.

**Given** MCP tests run for `create_party`
**When** they cover person, organization, optional defaults, missing required fields, duplicate sub-operations, payload limit, rejection, and complete response assembly
**Then** one tool call produces predictable all-or-nothing behavior.

### Story 4.5: Implement Patch-Oriented Update Party Tool

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR22, FR25, FR74

As an AI agent,
I want to update only the party fields I specify in one `update_party` tool call,
So that I can safely modify party records without sending or reconstructing full party state.

**Acceptance Criteria:**

**Given** an MCP request includes a party id and person detail changes
**When** `update_party` is invoked
**Then** the MCP layer normalizes only the specified fields into an `UpdatePartyComposite` command
**And** unspecified person fields remain unchanged.

**Given** an MCP request includes organization detail changes
**When** `update_party` is invoked
**Then** only specified organization fields are updated
**And** unspecified organization fields remain unchanged.

**Given** an MCP request includes contact channel additions, updates, removals, or preferred-channel changes
**When** `update_party` is invoked
**Then** those sub-operations are represented explicitly in the composite command
**And** aggregate-side rules decide applied, skipped, or rejected outcomes.

**Given** an MCP request includes identifier additions or removals
**When** `update_party` is invoked
**Then** those sub-operations are represented explicitly in the composite command
**And** duplicate or missing identifier behavior follows aggregate-side rules.

**Given** optional fields are omitted from the MCP request
**When** `update_party` normalizes input
**Then** omitted fields are treated as absent, not null updates
**And** documented validation errors distinguish missing required fields from intentional clears where clears are supported.

**Given** a composite update succeeds
**When** `update_party` returns its MCP response
**Then** the response includes the complete updated party record
**And** the returned record reflects all applied sub-operations in final aggregate order.

**Given** one sub-operation would violate aggregate rules
**When** the composite update is handled
**Then** the operation is all-or-nothing according to composite command design
**And** the MCP response does not report partial success as if the party were updated.

**Given** MCP tests run for `update_party`
**When** they cover partial detail updates, omitted fields, contact operations, identifier operations, duplicate-safe behavior, invalid transitions, payload limits, rejection, and complete response assembly
**Then** patch semantics are predictable for AI agents.

### Story 4.6: Implement Delete Party as Soft Deactivation Tool

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR23; supports FR4, FR25

As an AI agent,
I want a `delete_party` tool that safely removes a party from active use,
So that user requests to delete a contact map to MVP soft deactivation rather than GDPR erasure.

**Acceptance Criteria:**

**Given** an MCP request includes a valid party id for an active party
**When** `delete_party` is invoked
**Then** it maps to the domain soft-deactivation command
**And** the party is marked inactive rather than erased.

**Given** `delete_party` succeeds
**When** the MCP response is returned
**Then** it includes the updated party state or bounded confirmation showing inactive status
**And** it clearly distinguishes soft deactivation from GDPR erasure.

**Given** an AI agent or user intent appears to request GDPR erasure
**When** `delete_party` handles the request
**Then** MVP behavior communicates that GDPR erasure is not performed by this tool
**And** the response points to documented compliance limitations or future GDPR operations where appropriate.

**Given** the party id is missing, malformed, not found, already inactive, or not visible to the tenant
**When** `delete_party` is invoked
**Then** it returns a structured MCP success/idempotent/rejection response according to aggregate rules
**And** it does not expose cross-tenant existence information.

**Given** the tenant context is missing, invalid, or unauthorized
**When** `delete_party` is invoked
**Then** it fails closed before command handling
**And** no party state is read or mutated.

**Given** MCP tests run for `delete_party`
**When** they cover success, already inactive, missing id, not found, cross-tenant, unauthorized, and GDPR-erasure wording
**Then** soft-deactivation semantics are predictable and safe.

### Story 4.7: Enforce MCP Boundary and Tool Safety

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR25; supports FR20, FR21, FR22, FR23, FR24, FR74

As an architect maintaining Parties,
I want the MCP layer to remain a translation layer with strict safety boundaries,
So that AI agent features do not leak domain logic, internal infrastructure, or tenant data.

**Acceptance Criteria:**

**Given** MCP tool code is reviewed or tested
**When** dependencies are inspected
**Then** MCP code references command/query contracts and client abstractions only as intended
**And** it does not reference domain event types, projection actor internals, DAPR actor internals, or server implementation details.

**Given** MCP tools normalize forgiving AI input
**When** normalization occurs
**Then** normalization remains translation logic only
**And** domain invariants are enforced by aggregate/command handlers rather than duplicated in MCP tool code.

**Given** MCP tools return responses to AI agents
**When** responses include errors, degraded state, or validation guidance
**Then** responses are structured, bounded, and safe for agent reasoning
**And** they do not include raw command payloads, raw ProblemDetails details, secrets, tokens, claims dictionaries, tenant membership payloads, or personal data beyond the requested party result.

**Given** an MCP request has missing or invalid tenant/auth context
**When** any tool is invoked
**Then** the request fails closed before command/query state is accessed
**And** tool responses do not reveal cross-tenant existence.

**Given** an unknown tool, unsupported capability, or malformed payload is submitted
**When** the MCP server handles it
**Then** it returns a structured MCP error with safe suggestions where appropriate
**And** it does not dispatch to internal command or infrastructure surfaces.

**Given** architectural fitness tests run
**When** they inspect MCP registrations, project references, namespaces, and forbidden type usage
**Then** boundary violations fail the test suite.

### Story 4.8: Validate MCP Latency, Errors, and Complete Responses

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR24, FR25; supports FR20, FR21, FR22, FR23, FR74

As an AI agent integrator,
I want MCP tools to be fast, structured, and complete,
So that agents can use Parties without brittle retries or hidden follow-up queries.

**Acceptance Criteria:**

**Given** MCP command tools are invoked under target throughput conditions
**When** `create_party`, `update_party`, or `delete_party` complete successfully
**Then** each tool meets the documented MVP latency target where infrastructure is healthy
**And** composite create/update operations run in a single aggregate turn rather than sequential fragile commands.

**Given** MCP query tools are invoked under target throughput conditions
**When** `find_parties` or `get_party` complete successfully
**Then** each tool meets the documented query latency target where projections are healthy
**And** responses include freshness/degradation metadata where applicable.

**Given** `create_party` or `update_party` succeeds
**When** the MCP response is returned
**Then** it includes the complete created or updated party record
**And** agents do not need an immediate follow-up `get_party` call to know the resulting state.

**Given** MCP tools encounter validation, rejection, tenant, unsupported capability, degraded infrastructure, or internal failure scenarios
**When** responses are returned
**Then** errors are structured, predictable, and safe for agent reasoning
**And** personal data, secrets, raw payloads, and cross-tenant existence details are not exposed.

**Given** an MCP client retries the same idempotent request
**When** the request is equivalent and uses the same idempotency context
**Then** duplicate side effects are not created
**And** the retry response remains stable enough for agent workflows.

**Given** MCP end-to-end tests run
**When** they cover all five tools across success, validation error, domain rejection, tenant failure, degraded projection, unsupported capability, latency, and complete response scenarios
**Then** the tool surface is predictable and ready for AI-agent consumption.

## Epic 5: Event-Driven Consumer Integration

Consuming applications can receive ordered, tenant-aware party events and build their own lifecycle-aware read models.

### Story 5.1: Publish Stable Party Domain Events

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR34, FR54; supports FR37

As a consuming application developer,
I want Parties to publish stable domain events when party state changes,
So that my application can react and maintain its own read models.

**Acceptance Criteria:**

**Given** a party is created, updated, deactivated, reactivated, or has contacts/identifiers changed
**When** the corresponding aggregate command succeeds
**Then** a stable party domain event is persisted and published through the EventStore event pipeline
**And** the event payload represents the state change without requiring consumers to inspect internal aggregate state.

**Given** a command is rejected
**When** rejection events are persisted for audit/replay compatibility
**Then** external subscriber behavior is documented for rejection events
**And** consumers can distinguish successful state-change events from rejection events.

**Given** event contracts are published in the Contracts package
**When** consumers reference the package
**Then** they can deserialize supported party events
**And** event schemas remain append-only and additive.

**Given** personal data appears in published event payloads
**When** events are published to subscribers
**Then** payload shape follows the architecture's MVP/v1.1 privacy rules
**And** subscribers receive readable event payloads without needing to handle decryption.

**Given** event publication logs are produced
**When** events containing personal data are published
**Then** event publishing logs do not include raw personal data
**And** logs rely on safe envelope metadata instead of payload values.

**Given** event publication tests run
**When** they exercise create, detail update, contact channel, identifier, deactivate, reactivate, and rejection paths
**Then** expected event types are persisted/published
**And** event contracts remain stable for consumers.

### Story 5.2: Include Tenant Context and Envelope Metadata

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR70; supports FR39, FR40, FR41

As a consuming application developer,
I want party events to include tenant context and correlation metadata,
So that I can route, audit, and process events safely in a multi-tenant system.

**Acceptance Criteria:**

**Given** a successful party state-change event is published
**When** the event is delivered to subscribers
**Then** the event envelope includes authenticated tenant context
**And** consumers do not need to infer tenant from payload fields.

**Given** a party event is published
**When** the event envelope is inspected
**Then** it includes correlation, causation, aggregate identity, event type, timestamp, and version metadata according to EventStore conventions
**And** metadata fields are stable enough for consumer routing and diagnostics.

**Given** a command payload contains tenant-like values
**When** the event is emitted
**Then** envelope tenant context remains derived from authenticated credentials
**And** request payload tenant values are not trusted as event routing identity.

**Given** events are logged during publication
**When** logs are produced
**Then** logs include safe envelope metadata such as tenant, aggregate id, event type, and correlation id
**And** logs do not include raw event payloads, secrets, or personal data.

**Given** event metadata tests run
**When** they publish representative party events
**Then** tenant context and envelope metadata are present and consistent
**And** missing tenant context fails closed before publication.

### Story 5.3: Configure At-Least-Once Event Delivery

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR63; supports FR34, FR35

As a consuming application developer,
I want Parties events delivered through the configured pub/sub pipeline with at-least-once semantics,
So that my application can reliably observe party lifecycle changes.

**Acceptance Criteria:**

**Given** a party state-change event is persisted successfully
**When** the EventStore publication pipeline runs
**Then** the event is published to the configured DAPR pub/sub topic
**And** publication uses EventStore's persist-then-publish behavior.

**Given** event publication fails after persistence
**When** recovery or drain processing runs
**Then** the persisted event is retried for publication according to EventStore behavior
**And** successful command handling is not treated as data loss.

**Given** a subscriber receives a party event
**When** the subscriber acknowledges processing successfully
**Then** the event is considered delivered according to the DAPR pub/sub backend semantics
**And** subscriber retry behavior is documented for failures.

**Given** a subscriber fails before acknowledging processing
**When** the DAPR pub/sub backend retries delivery
**Then** the subscriber may receive the same party event more than once
**And** documentation and samples warn consumers to implement idempotent handlers.

**Given** deployment configuration is validated
**When** DAPR pub/sub components or access-control rules are missing or unsafe
**Then** validation reports a blocking issue
**And** wildcard app ids or wildcard paths are not accepted as safe.

**Given** event delivery tests or deployment validation run
**When** they cover normal publish, publication retry, subscriber failure, duplicate delivery, and missing pub/sub config
**Then** at-least-once behavior and unsafe configuration detection are verified.

### Story 5.4: Document Event Ordering and Subscriber Idempotency

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR73; supports FR35, FR63

As a consuming application developer,
I want clear guidance for event ordering and duplicate delivery,
So that my event handlers remain correct across supported deployment targets.

**Acceptance Criteria:**

**Given** Parties publishes events for a single aggregate
**When** the architecture and deployment docs describe delivery behavior
**Then** they state the expected per-aggregate causal ordering guarantees for supported deployment targets
**And** they identify any broker/backend configuration required for those guarantees.

**Given** a deployment target cannot guarantee per-aggregate ordering
**When** subscriber guidance is documented
**Then** it describes order-tolerant or sequence-checking handler patterns
**And** it warns consumers not to assume strict ordering unless the backend configuration supports it.

**Given** DAPR pub/sub delivers at-least-once
**When** consumer handler guidance is documented
**Then** it includes idempotency recommendations using event id, aggregate id, version/sequence, correlation id, or equivalent metadata
**And** it explains safe duplicate handling.

**Given** consumer read models process party lifecycle events
**When** examples or tests are reviewed
**Then** they handle duplicate and out-of-order events without corrupting local state
**And** destructive or privacy-relevant events are treated with special care.

**Given** ordering documentation tests or review checks run
**When** docs mention supported backends or ordering behavior
**Then** they remain aligned with EventStore/DAPR deployment guidance
**And** unsupported guarantees are not claimed.

### Story 5.5: Provide Consumer Read-Model Handler Guidance

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR35; supports FR34, FR38, FR63, FR73

As a consuming application developer,
I want concrete handler patterns for party events,
So that I can build domain-specific read models safely and predictably.

**Acceptance Criteria:**

**Given** a consuming application wants to maintain a local party read model
**When** it follows the handler guidance
**Then** it can handle party created, details updated, contact channel changed, identifier changed, deactivated, and reactivated events
**And** it stores only the fields needed for its own bounded context.

**Given** handlers process duplicate events
**When** the same event arrives more than once
**Then** the guidance shows how to skip or safely reapply the duplicate
**And** local read model state is not duplicated or corrupted.

**Given** handlers process events that arrive out of order for a backend that does not guarantee ordering
**When** sequence or version metadata indicates an older event
**Then** the guidance shows how to defer, skip, or reconcile the event
**And** it avoids overwriting newer local state with older data.

**Given** the consuming application only needs references to Parties
**When** it handles events
**Then** guidance recommends storing stable party id and necessary metadata rather than copying unnecessary personal data
**And** it flags privacy implications of local denormalization.

**Given** sample handler code or docs are validated
**When** representative event streams are replayed
**Then** the example read model reaches the expected state
**And** duplicate and out-of-order cases are covered.

### Story 5.6: Prepare Forward-Compatible Party Lifecycle Events

**Phase:** MVP preparation with future lifecycle/GDPR compatibility
**Coverage type:** non-feature preparation / prepared-deferred; implemented for FR37 contract compatibility
**Scheduling label:** preparation-only; does not deliver active MVP runtime behavior for future merge or GDPR lifecycle capabilities
**Requirements covered:** FR37
**Requirements prepared:** future merge behavior and v1.1 GDPR event naming conventions

As a consuming application developer,
I want party event contracts to anticipate future lifecycle capabilities,
So that my integration does not break when Parties adds merge, GDPR, or richer search/audit behavior.

**Acceptance Criteria:**

**Given** the Contracts package includes MVP party events
**When** event contract compatibility is reviewed
**Then** events support additive evolution without renaming or removing existing fields
**And** compatibility tests detect breaking event contract changes.

**Given** future party merge behavior is not active in MVP
**When** forward-compatible event contracts are reviewed
**Then** a `PartyMerged` or equivalent placeholder contract is represented where architecture requires it
**And** documentation states whether it can be emitted in MVP.

**Given** GDPR events are deferred to v1.1
**When** event naming and metadata conventions are reviewed
**Then** the MVP event model leaves room for `PartyErased`, consent, restriction, export, and processing-record events
**And** existing MVP event consumers are not forced to handle unavailable event types as if they were emitted.

**Given** event deserialization is tolerant
**When** consumers receive events with additive future fields
**Then** existing consumers can ignore unknown fields where the serialization contract supports it
**And** event version metadata remains available for compatibility decisions.

**Given** forward-compatibility tests run
**When** they inspect event contracts, version metadata, serialization, and placeholder docs
**Then** future-ready contracts remain additive and consumer-safe.

### Story 5.7: Document Erasure Subscriber Responsibilities

**Phase:** MVP documentation preparation for v1.1 GDPR
**Coverage type:** non-feature preparation / prepared-deferred; implemented for FR38 guidance
**Scheduling label:** preparation-only; does not deliver active MVP runtime behavior for future GDPR erasure
**Requirements covered:** FR38
**Requirements prepared:** FR46

As a consuming application developer,
I want explicit guidance for future `PartyErased` events,
So that my application is ready to clean up references when GDPR erasure ships.

**Acceptance Criteria:**

**Given** MVP does not yet activate GDPR erasure
**When** consumer documentation discusses event handling
**Then** it still warns that `PartyErased` handling will be mandatory for all consuming applications
**And** it explains the difference between MVP soft deactivation and future GDPR erasure.

**Given** a consuming application stores party references or denormalized party data
**When** it reads erasure guidance
**Then** it understands which local references or cached fields must be cleaned, anonymized, or reviewed when `PartyErased` is received
**And** it understands dangling reference trade-offs.

**Given** event handler examples are provided
**When** future `PartyErased` handling is described
**Then** examples show idempotent cleanup behavior
**And** they avoid logging or preserving erased personal data.

**Given** GDPR compliance is deferred to v1.1
**When** documentation is reviewed
**Then** it does not imply MVP performs legal erasure
**And** it links to MVP compliance warning and emergency manual erasure procedure where applicable.

**Given** documentation validation or review runs
**When** event integration docs are checked
**Then** erasure responsibility guidance is present wherever subscriber integration is documented
**And** the guidance remains consistent with Contracts package event naming.

## Epic 6: GDPR Compliance Operations

Administrators and DPO workflows can erase, restrict, export, verify, and audit party data with crypto-shredding and subscriber notifications.

### Story 6.1: Activate Per-Party Personal Data Encryption

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR53; supports FR42, FR55

As a privacy-conscious operator,
I want personal data in stored events and snapshots encrypted with per-party keys,
So that GDPR-era storage protects personal data while preserving event-sourced behavior.

**Acceptance Criteria:**

**Given** GDPR encryption is enabled for a tenant
**When** a party command persists events or snapshots containing `[PersonalData]` fields
**Then** personal data fields are encrypted at rest using the party's encryption key
**And** non-personal metadata needed for routing, tenant isolation, and event replay remains readable.

**Given** an event or snapshot is rehydrated for authorized processing
**When** the party key is available
**Then** encrypted personal data is decrypted for domain processing
**And** aggregate replay remains compatible with EventStore conventions.

**Given** a party is an organization with contact channels or identifiers that may identify a natural person
**When** encryption classification is applied
**Then** the type-dependent personal data rules are honored
**And** ambiguous data is treated conservatively.

**Given** the party key is missing, revoked, or inaccessible
**When** the system attempts to read encrypted personal data
**Then** it returns a controlled privacy-preserving status
**And** it does not expose cryptographic errors or partial personal data.

**Given** encryption tests run
**When** they cover person fields, organization fields, contact channels, identifiers, derived names, snapshots, rehydration, missing key, and classification edge cases
**Then** personal data is encrypted/decrypted correctly
**And** replay compatibility is preserved.

### Story 6.2: Trigger Right-to-Erasure by Crypto-Shredding

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR44; supports FR55

As an administrator or DPO,
I want to trigger right-to-erasure for a party,
So that personal data becomes permanently unreadable while the event stream remains auditable.

**Acceptance Criteria:**

**Given** an authorized administrator submits an erasure request for an eligible party
**When** the erasure command is accepted
**Then** the system revokes or destroys the party's personal data encryption key
**And** persisted personal data for that party becomes permanently unreadable.

**Given** an erasure request targets a party that is already erased
**When** the command is handled
**Then** the result is idempotent or returns a typed already-erased outcome
**And** no new personal data is exposed.

**Given** an erasure request targets a missing party, unauthorized tenant, restricted flow conflict, or invalid request
**When** the command is handled
**Then** it returns a typed rejection
**And** the response does not leak personal data or cross-tenant existence.

**Given** erasure is triggered
**When** domain state is updated
**Then** party status records erasure state
**And** future reads return privacy-preserving erased status rather than decrypted personal details.

**Given** erasure command tests run
**When** they cover success, already-erased, missing party, unauthorized tenant, invalid request, and missing key scenarios
**Then** crypto-shredding behavior is irreversible and typed outcomes are verified.

### Story 6.3: Verify Erasure Across Internal Stores

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR45

As an administrator or DPO,
I want erasure verification across internal projections, caches, and indexes,
So that I can prove the service no longer exposes erased personal data.

**Acceptance Criteria:**

**Given** a party erasure has been triggered
**When** verification runs
**Then** it checks aggregate readable state, snapshots, detail projections, index projections, caches, and search indexes that may hold party personal data
**And** each store returns a pass, fail, pending, or not-applicable result.

**Given** a projection or cache still exposes erased personal data
**When** verification detects the issue
**Then** verification reports a failed or partial state
**And** it identifies the affected internal store category without logging personal data values.

**Given** a projection is rebuilding or temporarily unavailable
**When** verification runs
**Then** it reports a pending or degraded verification state
**And** it supports retry without creating duplicate erasure side effects.

**Given** verification completes successfully
**When** the result is returned
**Then** it includes a bounded verification report suitable for an administrator or DPO
**And** the report can be correlated to the erasure request without including personal data.

**Given** verification tests run
**When** they cover complete erasure, projection residue, cache residue, search residue, unavailable stores, retry, and report redaction
**Then** verification behavior proves privacy-safe completion or actionable failure.

### Story 6.4: Publish PartyErased Subscriber Notification

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR46; supports FR38

As a consuming application developer,
I want to receive a `PartyErased` notification when a party is erased,
So that I can clean up local references and denormalized personal data.

**Acceptance Criteria:**

**Given** right-to-erasure completes or reaches the documented notification point
**When** the system publishes subscriber events
**Then** it emits a stable `PartyErased` event or equivalent erased-party notification
**And** the event includes tenant context, party id, correlation metadata, and privacy-safe erasure status.

**Given** `PartyErased` is published
**When** subscribers receive the event
**Then** the event contains enough information to identify local references that require cleanup
**And** it does not include erased personal data.

**Given** internal erasure verification is pending or partial
**When** subscriber notification behavior is defined
**Then** the event or companion status makes the notification semantics clear
**And** consumers can distinguish accepted erasure from fully verified erasure where the architecture requires that distinction.

**Given** event publication is at-least-once
**When** a subscriber receives duplicate `PartyErased` events
**Then** consumer guidance and sample handlers treat cleanup as idempotent
**And** duplicate events do not reintroduce or preserve personal data.

**Given** `PartyErased` publication tests run
**When** they cover success, duplicate delivery, missing tenant, already erased, partial verification, and event redaction
**Then** subscribers receive a stable privacy-safe event contract.

### Story 6.5: Manage Per-Channel Per-Purpose Consent

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR47, FR48

As an administrator or DPO,
I want to record and revoke consent per contact channel and purpose,
So that party communication preferences are auditable and precise.

**Acceptance Criteria:**

**Given** an authorized administrator records consent for a party contact channel and purpose
**When** the consent command is accepted
**Then** the system records consent with party id, channel id, purpose, lawful basis metadata where applicable, timestamp, source, and actor metadata
**And** it emits a stable consent-recorded event.

**Given** consent already exists for a channel and purpose
**When** the administrator records a new consent state
**Then** the system preserves consent history
**And** the current consent state reflects the latest valid record.

**Given** an authorized administrator revokes consent for a party contact channel and purpose
**When** the revoke command is accepted
**Then** the system records the revocation with timestamp, source, reason where applicable, and actor metadata
**And** it emits a stable consent-revoked event.

**Given** a consent command targets a missing party, erased party, missing channel, invalid purpose, unauthorized tenant, or unsupported party-wide shortcut
**When** the command is handled
**Then** it returns a typed rejection
**And** no consent shortcut is applied across all channels or the whole tenant.

**Given** consent history is queried
**When** the administrator retrieves consent records
**Then** the response shows bounded per-channel/per-purpose history
**And** it excludes unrelated tenant data and avoids logging personal data.

**Given** consent tests run
**When** they cover record, revoke, history, duplicate/idempotent requests, invalid channel, invalid purpose, erased party, cross-tenant, and no-shortcut behavior
**Then** consent management remains precise and auditable.

### Story 6.6: Restrict and Resume Party Processing

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR49, FR50

As an administrator or DPO,
I want to restrict and later resume processing of a party's data,
So that complaints or investigations can pause processing without erasing the party.

**Acceptance Criteria:**

**Given** an authorized administrator submits a processing restriction for an eligible party
**When** the restriction command is accepted
**Then** the party is marked restricted with timestamp, bounded reason, actor metadata, and correlation id
**And** a stable processing-restricted event is emitted.

**Given** a party is restricted
**When** normal update, contact, identifier, consent, search-sensitive, or processing operations are attempted
**Then** restricted-operation guards apply according to the documented policy
**And** disallowed operations return typed rejections without mutating party state.

**Given** an authorized administrator lifts processing restriction
**When** the lift restriction command is accepted
**Then** the party restriction state is cleared or marked lifted with timestamp, actor metadata, and correlation id
**And** a stable processing-restriction-lifted event is emitted.

**Given** restriction commands target a missing party, erased party, already restricted party, not-restricted party, unauthorized tenant, or invalid reason
**When** commands are handled
**Then** they return typed rejection or idempotent outcomes as documented
**And** no personal data leaks through error responses.

**Given** restriction status is queried
**When** an administrator retrieves party status
**Then** the response shows current restriction state and bounded history metadata
**And** it does not expose unrelated tenant data.

**Given** restriction tests run
**When** they cover restrict, blocked operations, allowed operations, lift restriction, duplicate/idempotent requests, erased party, invalid reason, and cross-tenant behavior
**Then** processing restriction behavior is enforceable and auditable.

### Story 6.7: Export Party Data Portability Package

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR51

As an administrator or DPO,
I want to export all data for a specific party in a machine-readable format,
So that the service can satisfy data portability requests.

**Acceptance Criteria:**

**Given** an authorized administrator requests portability export for an eligible party
**When** the export query runs
**Then** the system assembles a machine-readable package containing the party's readable personal data, contact channels, identifiers, consent records, restriction state, processing records, and relevant event-derived metadata
**And** the package is scoped to the requested party and tenant.

**Given** the party is erased or the personal data key is unavailable
**When** export is requested
**Then** the response returns privacy-preserving erased/unavailable status
**And** it does not expose cryptographic errors or partial personal data.

**Given** the party is restricted
**When** export is requested
**Then** the documented restriction policy is applied
**And** the response either exports allowed data or returns a typed restricted-processing outcome.

**Given** an export file or payload is generated
**When** filename, content type, and metadata are assigned
**Then** the filename uses party id plus timestamp only
**And** no names, contact values, identifiers, tenant ids, or free text are embedded in filenames or telemetry dimensions.

**Given** export is logged or audited
**When** operational records are created
**Then** they include bounded metadata such as party id, tenant, actor, timestamp, and correlation id
**And** they do not log exported payload content.

**Given** export tests run
**When** they cover success, erased party, missing key, restricted party, missing party, cross-tenant, filename safety, payload shape, and redaction
**Then** portability export is complete, bounded, and privacy-safe.

### Story 6.8: Record Processing Activities

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR52

As an administrator or DPO,
I want a complete time-stamped record of processing activities on party data,
So that the service can support GDPR audit and Article 30 reporting needs.

**Acceptance Criteria:**

**Given** a party command, query, consent change, restriction change, erasure operation, verification, export, or subscriber notification occurs
**When** the operation qualifies as a processing activity
**Then** the system records bounded processing metadata including operation category, party id, tenant, actor/system identity, timestamp, correlation id, and outcome.

**Given** processing activity records are stored
**When** their schema is reviewed
**Then** they avoid raw personal data, raw command/query payloads, tokens, claims dictionaries, and exported payload content
**And** they retain enough metadata to support audit and reporting.

**Given** an administrator queries processing records for a party
**When** the query is authorized
**Then** the system returns a time-ordered bounded list of processing activities for that party
**And** it excludes unrelated tenant data.

**Given** a party is erased
**When** processing records remain available
**Then** records preserve privacy-safe audit metadata
**And** they do not require decrypting erased personal data.

**Given** processing record tests run
**When** they cover command, query, consent, restriction, erasure, verification, export, notification, erased party, redaction, and cross-tenant cases
**Then** activity recording is complete and privacy-safe.

### Story 6.9: Return Privacy-Preserving Erased Party Status

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** FR55; supports FR44, FR45

As a consumer or administrator querying an erased party,
I want to receive an explicit erased status instead of cryptographic failures,
So that applications can behave correctly without exposing erased personal data.

**Acceptance Criteria:**

**Given** a party has been erased and its personal data key is unavailable
**When** a detail, list, search, export, or admin query encounters the party
**Then** the response returns an explicit erased/no-longer-inspectable status where the surface supports it
**And** it does not expose decrypted personal data or raw cryptographic errors.

**Given** an erased party appears in an index or status view
**When** the record is returned
**Then** only privacy-safe identifiers/status metadata are shown
**And** stale personal details are cleared before rendering or serialization.

**Given** a command attempts to mutate an erased party
**When** the command is handled
**Then** the system rejects the command with a typed erased-party outcome
**And** no new personal data is accepted for that erased party.

**Given** subscribers or clients receive erased status
**When** they handle the response or event
**Then** the status is stable and distinguishable from not found, inactive, restricted, and transient key-store failure
**And** documentation explains the differences.

**Given** erased status tests run
**When** they cover read paths, command paths, export, index/search, stale projections, key-store failures, and response redaction
**Then** erased parties are handled consistently and privacy-safely.

### Story 6.10: Rotate Tenant Encryption Keys

**Phase:** v1.1
**Coverage type:** operational/security NFR coverage when v1.1 is scheduled
**Requirements covered:** NFR11
**Requirements supported:** FR53, FR55
**Scheduling label:** NFR/security story; not counted as functional requirement coverage

As an operator responsible for tenant security,
I want tenant encryption keys to rotate without downtime or data loss,
So that Parties can meet operational security requirements while preserving readable party data.

**Acceptance Criteria:**

**Given** a tenant encryption key rotation is requested
**When** rotation begins
**Then** the system creates or selects the new tenant key material according to the configured key provider
**And** existing party-level keys remain available for decrypting current data during the rotation window.

**Given** party-level personal data keys are protected by tenant key material
**When** tenant key rotation is applied
**Then** party key wrapping or equivalent key metadata is updated safely
**And** personal data remains readable for authorized operations after rotation.

**Given** key rotation is interrupted or partially fails
**When** the system resumes or retries rotation
**Then** rotation proceeds idempotently from the last safe state
**And** no party personal data becomes permanently unreadable because of partial rotation.

**Given** an erased party no longer has readable personal data keys
**When** tenant key rotation runs
**Then** erased parties remain erased
**And** rotation does not recreate or recover destroyed personal data keys.

**Given** key rotation status is queried
**When** an operator checks progress
**Then** the response includes bounded status metadata, counts, and failures by category
**And** it does not expose key material, secrets, tokens, or personal data.

**Given** key rotation tests run
**When** they cover success, retry, partial failure, erased parties, missing key provider, concurrent reads, and redacted status output
**Then** tenant key rotation preserves uptime, data readability, and erasure guarantees.

## Epic 7: Administration Console

Administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface.

### Story 7.1: Compose Admin Console Working View

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65; UX-DR1, UX-DR2, UX-DR21

As a Parties administrator,
I want the admin portal to open directly to a working party management console,
So that I can search, browse, and inspect parties without passing through a landing page.

**Acceptance Criteria:**

**Given** an authorized administrator navigates to `/admin/parties`
**When** the Parties admin surface loads
**Then** the first viewport shows the working console with search, compact filters, tenant/auth state, result grid, and detail region
**And** no marketing, landing, hero, or introductory page is shown.

**Given** the admin console layout renders
**When** list and detail regions are displayed
**Then** they are sibling working regions rather than nested card shells
**And** the layout remains dense, scannable, and suitable for repeated administration work.

**Given** route support is available in FrontComposer
**When** a party row is selected
**Then** the detail state can deep-link to `/admin/parties/{partyId}`
**And** the route uses only the non-PII party id.

**Given** GDPR route support is available in FrontComposer
**When** an administrator opens party GDPR operations
**Then** the route can use `/admin/parties/{partyId}/gdpr`
**And** no names, emails, identifiers, consent text, free text, or raw errors are placed in the URL.

**Given** the admin console is rendered across supported viewport sizes
**When** the layout is inspected
**Then** the toolbar, results, detail region, and status region remain usable without text overlap
**And** primary workflows are visible or reachable without an explanatory feature page.

### Story 7.2: Browse and Filter Party Results

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65; UX-DR4, UX-DR5, UX-DR10

As a Parties administrator,
I want to browse, search, and filter party records in the admin console,
So that I can quickly find the party I need to inspect.

**Acceptance Criteria:**

**Given** an authorized administrator opens `/admin/parties`
**When** the toolbar renders
**Then** it includes search input, party type filter, active/status filter, retry control, and EventStore Admin UI availability indicator
**And** controls use the accepted Parties client/query contract.

**Given** the administrator performs a display-name search
**When** results are loaded
**Then** the grid displays tenant-safe matching parties with display name, type, active/erased/restricted state, created/modified dates, and non-PII status indicators
**And** unsupported rich-search filters are disabled rather than silently sent.

**Given** the administrator filters by type, active status, created date, or modified date where supported
**When** the query runs
**Then** the grid displays only matching current-tenant records
**And** invalid filters produce bounded validation feedback.

**Given** the result set is large
**When** the administrator browses results
**Then** the grid uses server-side paging or virtualization
**And** it does not load unbounded result sets into the UI.

**Given** token, tenant, user, filters, or host configuration changes while results are in flight
**When** the stale response returns
**Then** stale results are suppressed
**And** the console reloads or clears state for the current context only.

**Given** browse/filter UI tests run
**When** they cover search, filters, paging/virtualization, unsupported filters, stale response suppression, retry, and tenant switch
**Then** the grid remains tenant-safe, scannable, and predictable.

### Story 7.3: Inspect Party Detail Safely

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65; UX-DR6, UX-DR9, UX-DR20, UX-DR21

As a Parties administrator,
I want to inspect a selected party's details in the admin console,
So that I can understand its current record, contacts, identifiers, and compliance state.

**Acceptance Criteria:**

**Given** an administrator selects a party row
**When** the detail panel loads
**Then** it displays selected party summary, contacts, identifiers, consent, restriction, erasure status, processing records, and safe EventStore Admin UI links where available
**And** the detail region remains a dense titled working region, not a decorative card shell.

**Given** a party is active, inactive, restricted, or erased
**When** the detail panel renders
**Then** it shows the correct state with non-color-only indicators
**And** erased parties show terminal privacy-preserving state only.

**Given** the selected party is not found, forbidden, cross-tenant, gone/erased, timeout, malformed, or contract-unavailable
**When** the detail load resolves
**Then** sensitive detail state is cleared before rendering the status
**And** stale personal detail is not retained in the UI.

**Given** the administrator signs out, changes tenant, loses admin permission, or changes route context
**When** context changes
**Then** list/detail/GDPR/export state is cleared or reloaded according to the current context
**And** in-flight detail responses are canceled or suppressed when stale.

**Given** backend content, AI-created party data, operator-entered text, or ProblemDetails text is displayed
**When** the detail panel renders it
**Then** all content is encoded through normal Razor/component text paths
**And** raw markup, raw HTML fragments, and JavaScript interpolation are not used.

**Given** detail UI tests run
**When** they cover selection, state indicators, erased state, forbidden/cross-tenant, malformed responses, tenant switch, sign-out, and encoded rendering
**Then** the detail panel remains safe and predictable.

### Story 7.4: Handle Admin Empty, Error, and Degraded States

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65; UX-DR8, UX-DR9, UX-DR19, UX-DR21

As a Parties administrator,
I want every empty, error, and degraded admin state to be bounded and safe,
So that I can recover from problems without stale or sensitive data staying visible.

**Acceptance Criteria:**

**Given** the admin console has no matching results
**When** list or search completes successfully with no records
**Then** the UI shows a localized empty state
**And** it does not imply that records exist in another tenant.

**Given** token, tenant, or admin permission is missing
**When** the admin console evaluates access
**Then** it clears sensitive list, detail, GDPR, and export state
**And** shows the appropriate missing-token, missing-tenant, or admin-required state.

**Given** a tenant switch occurs while requests are in flight
**When** stale responses return
**Then** the UI suppresses stale data
**And** selected row, caches, detail state, GDPR state, and export state reset before loading the new tenant.

**Given** a forbidden, cross-tenant, not-found, gone/erased, timeout, transient failure, degraded, malformed response, or contract-unavailable condition occurs
**When** the console renders the state
**Then** it uses bounded localized status text
**And** raw response bodies, raw ProblemDetails details, tokens, claims, and personal data are not rendered.

**Given** a retryable list failure occurs
**When** the administrator uses retry
**Then** the console retries the current safe request context
**And** focus returns predictably to the initiating control or relevant status region.

**Given** empty/error/degraded UI tests run
**When** they cover all documented operator states, stale response suppression, retry, focus behavior, redaction, and tenant switch
**Then** the console handles failures without data leakage or confusing stale state.

### Story 7.5: Add Safe EventStore Admin UI Links

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65; UX-DR3, UX-DR21

As a Parties administrator,
I want safe links from party records to EventStore Admin UI diagnostics,
So that I can inspect streams, commands, and correlations without duplicating generic EventStore browsing in Parties.

**Acceptance Criteria:**

**Given** EventStore Admin UI is configured and available
**When** the admin console renders diagnostic links
**Then** it generates links only from safe identifiers such as stream id, aggregate id, command id, correlation id, or timestamp
**And** link labels use generic text such as `Open stream`, `Open command status`, or `Open correlation`.

**Given** EventStore Admin UI URL or availability is missing
**When** diagnostic links are rendered
**Then** the controls are disabled with a bounded localized reason
**And** no fallback embedded browser or generic event stream browser is shown inside Parties.

**Given** party data includes names, emails, identifiers, consent text, or free text
**When** EventStore links are generated
**Then** none of those values appear in URLs, link labels, storage keys, telemetry dimensions, or logs.

**Given** an administrator activates a safe EventStore link
**When** the navigation occurs
**Then** focus and navigation behavior remain predictable in the FrontComposer shell
**And** no raw payload or token is appended to the link.

**Given** EventStore link tests run
**When** they cover configured, unavailable, disabled, safe identifier, PII redaction, and navigation cases
**Then** links remain useful without becoming a privacy or boundary leak.

### Story 7.6: Gate GDPR Operations on Accepted Client Contract

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR66; UX-DR7, UX-DR11
**Dependency:** Accepted EventStore-fronted Parties client/gateway contract. If the dependency is tracked outside this epics document, reference the external dependency record in implementation planning before scheduling Story 7.6.

As a Parties administrator,
I want GDPR actions disabled until the accepted Parties client command/query contract is available,
So that the admin console does not present operations it cannot safely execute.

**Acceptance Criteria:**

**Given** the accepted EventStore-fronted Parties client/gateway contract is unavailable
**When** the admin console renders GDPR actions
**Then** erasure, restriction, consent, portability, verification, and processing-record actions are disabled
**And** each disabled action shows the exact bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract`.

**Given** the accepted EventStore-fronted Parties client/gateway contract becomes available
**When** capability detection refreshes
**Then** supported GDPR actions become enabled according to the available contract methods
**And** unsupported actions remain disabled with bounded reasons.

**Given** a contract-unavailable state occurs after an action panel was open
**When** the state is detected
**Then** sensitive form data and operation state are cleared
**And** no stale command can be submitted.

**Given** capability detection fails or returns malformed capability data
**When** the console renders GDPR controls
**Then** it fails closed by disabling affected actions
**And** it does not render raw backend details.

**Given** GDPR gating tests run
**When** they cover contract unavailable, available, partially available, malformed, stale, and tenant switch states
**Then** operation controls are enabled only when safe and supported.

### Story 7.7: Implement GDPR Operation Panels

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR66; UX-DR7, UX-DR12, UX-DR13, UX-DR14, UX-DR15, UX-DR16, UX-DR17

As a Parties administrator or DPO,
I want compact GDPR operation panels for a selected party,
So that I can trigger and monitor erasure, restriction, consent, portability, and processing-record workflows.

**Acceptance Criteria:**

**Given** an administrator opens the GDPR panel for a selected party
**When** the panel renders
**Then** it shows operation sections for erasure, erasure status/certificate, verification retry, restrict/lift restriction, consent add/revoke/history, portability export, and processing records where supported
**And** each section uses the accepted Parties client/query/command contract.

**Given** an erasure request is submitted
**When** confirmation is required
**Then** the confirmation displays party id only
**And** it returns command accepted outcome before refreshing authoritative erasure status.

**Given** erasure certificate or verification status is requested
**When** the operation completes
**Then** the panel shows safe verification results
**And** generated filenames use party id plus timestamp only.

**Given** restriction is applied or lifted
**When** the administrator submits a bounded reason where required
**Then** the operation returns command accepted outcome
**And** the panel refreshes current status before enabling follow-on actions.

**Given** consent is added, revoked, or viewed
**When** the administrator uses consent controls
**Then** consent is scoped per channel and per purpose
**And** no party-wide or tenant-wide consent shortcut is offered.

**Given** portability export or processing records are requested
**When** the operation completes
**Then** outputs use safe filenames, content types, bounded summaries, and safe correlation links
**And** exported payloads or raw processing details are not logged.

**Given** GDPR panel tests run
**When** they cover all supported flows, disabled unsupported flows, safe filenames, bounded reasons, stale state, redaction, and tenant switch
**Then** GDPR operations remain usable and privacy-safe.

### Story 7.8: Localize Admin Console Text and Status

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65, FR66; UX-DR18

As a Parties administrator,
I want all admin console labels and statuses localized,
So that the admin surface is consistent with FrontComposer localization expectations.

**Acceptance Criteria:**

**Given** the admin console renders toolbar, grid, detail, GDPR, empty, error, and status text
**When** labels or messages are displayed
**Then** they come from localized resource strings or the FrontComposer localization mechanism
**And** hard-coded user-facing strings are avoided.

**Given** dates, booleans, counts, lawful-basis labels, validation messages, warning copy, and operation outcomes are displayed
**When** locale changes or localized resources are inspected
**Then** formatting and text follow the active localization context
**And** fallback behavior is bounded.

**Given** ProblemDetails title or detail text is displayed
**When** the UI renders it
**Then** the text is encoded and bounded
**And** it is not used as a localization key.

**Given** localized resources are missing
**When** the console attempts to render affected text
**Then** fallback text is safe and discoverable in tests
**And** missing resource coverage can be detected before release.

**Given** localization tests run
**When** they cover core labels, status messages, validation, operation outcomes, date/number formatting, fallback, and ProblemDetails handling
**Then** the console is localization-ready.

### Story 7.9: Enforce Admin Console Accessibility

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65, FR66; UX-DR19

As a Parties administrator using keyboard or assistive technology,
I want the admin console to be accessible,
So that I can complete party administration workflows without mouse-only or color-only interactions.

**Acceptance Criteria:**

**Given** the admin console toolbar, grid, detail, retry controls, links, and GDPR panels render
**When** an administrator navigates by keyboard
**Then** all controls are reachable in logical order
**And** visible focus is always present.

**Given** search filters change or retries complete
**When** focus should be restored
**Then** focus returns to the search input, initiating control, or relevant status region according to the documented interaction
**And** focus is not lost to the document body.

**Given** result rows and party states render
**When** screen-reader labels are inspected
**Then** rows expose names and state through accessible labels
**And** erased, restricted, degraded, blocked, and inactive states are not color-only.

**Given** detail panels and dialogs render
**When** assistive technology inspects landmarks and dialog behavior
**Then** headings define useful landmarks
**And** dialogs trap focus and restore focus after completion or cancellation.

**Given** loading, empty, blocked, forbidden, degraded, malformed, stale, or operation outcome states occur
**When** state changes
**Then** a polite status region announces bounded updates
**And** announcements do not expose personal data unnecessarily.

**Given** accessibility tests run
**When** they cover keyboard navigation, focus restoration, labels, dialogs, status announcements, forced-colors, and reduced-motion expectations
**Then** accessibility regressions fail before release.

### Story 7.10: Enforce Admin Console Privacy and Encoding Rules

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR65, FR66; UX-DR20, UX-DR21; NFR32

As a privacy-conscious operator,
I want the admin console to prevent personal data leakage through UI, logs, routes, storage, and telemetry,
So that administration workflows do not create secondary privacy exposure.

**Acceptance Criteria:**

**Given** user, backend, AI-created, or operator-entered content is rendered
**When** the admin console displays it
**Then** content is rendered through encoded Razor/component text paths
**And** raw markup, raw HTML fragments, JavaScript interpolation, and unsafe rendering APIs are not used.

**Given** routes, storage keys, telemetry dimensions, filenames, logs, link labels, DOM event names, or JavaScript event payloads are generated
**When** party data is available
**Then** names, emails, identifiers, consent text, free text, tenant membership payloads, JWTs, claims dictionaries, sidecar names, and DAPR ports are excluded
**And** safe identifiers are used only where explicitly allowed.

**Given** backend failures, malformed responses, or ProblemDetails responses occur
**When** the console handles them
**Then** raw bodies and raw details are not rendered or logged
**And** bounded localized summaries are used instead.

**Given** operator actions such as erasure, export, retry, and link navigation are logged
**When** logs or telemetry are emitted
**Then** they include operation category, non-PII id, correlation/status id, bounded outcome code, and retry category where useful
**And** they exclude personal data and secrets.

**Given** privacy and encoding tests run
**When** they cover malicious party data, AI-created content, ProblemDetails text, filenames, routes, telemetry, logs, storage, and DOM event payloads
**Then** no unsafe rendering or PII leakage is detected.

## Epic 8: Embeddable Party Picker

Consuming application developers can embed a tenant-safe, accessible party picker component for search and selection.

### Story 8.1: Compose Embeddable Party Picker Shell

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR67; UX-DR22, UX-DR25, UX-DR32

As a consuming application developer,
I want an embeddable FrontComposer/Blazor party picker component,
So that my application can offer party selection without building its own party search UI.

**Acceptance Criteria:**

**Given** a host application references the picker component
**When** it renders the picker
**Then** the picker appears as an embeddable party search and selection control
**And** it is not an admin portal, party editor, tenant selector, GDPR surface, or EventStore stream browser.

**Given** the host supplies request/auth context through accepted Parties client or EventStore gateway configuration
**When** the picker initializes
**Then** it uses that configuration for queries
**And** it does not persist, refresh, parse for authorization, or log tokens.

**Given** the picker is disabled or read-only
**When** it renders
**Then** interactive search and selection controls are disabled appropriately
**And** current selection display remains stable and accessible.

**Given** the picker is embedded in different host layouts
**When** it renders at supported viewport/container sizes
**Then** it maintains a bounded compact layout
**And** text and controls do not overlap or resize unpredictably.

**Given** picker shell tests run
**When** they cover enabled, disabled, read-only, missing host configuration, and embedded layout states
**Then** the picker remains a bounded embeddable component.

### Story 8.2: Implement Typeahead Search and Bounded Results

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR67; UX-DR23, UX-DR31

As a host application user,
I want to type ahead by display name and see bounded party results,
So that I can quickly select the correct party.

**Acceptance Criteria:**

**Given** the user types a display-name query
**When** the debounce interval completes
**Then** the picker queries through the accepted Parties client/EventStore gateway boundary
**And** it does not call retired REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.

**Given** search results return successfully
**When** the picker displays them
**Then** it shows a bounded result count in a stable compact layout
**And** each result includes enough non-PII-safe visible context to choose a party.

**Given** the query is empty or below the documented minimum
**When** search would otherwise run
**Then** the picker avoids unnecessary queries
**And** it displays a localized idle or empty state.

**Given** search returns no matches
**When** results are displayed
**Then** the picker shows a localized empty state
**And** it does not imply cross-tenant records exist.

**Given** typeahead tests run
**When** they cover debounce, result bounding, empty query, no results, tenant-safe querying, and endpoint boundary violations
**Then** the picker search remains predictable and safe.

### Story 8.3: Emit Durable Selection by Party Id

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR67; UX-DR24, UX-DR30

As a host application developer,
I want the picker to return only the selected party id as the durable selection,
So that my application does not accidentally persist personal display data as an identity key.

**Acceptance Criteria:**

**Given** a user selects a party result
**When** the picker invokes its host callback
**Then** it passes the selected party id
**And** display names, contact values, identifiers, consent text, degraded reasons, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads are not included in the durable callback payload.

**Given** the host provides a selected party id
**When** the picker initializes or receives updated parameters
**Then** it resolves display state through the accepted query boundary when needed
**And** the durable selection remains the party id.

**Given** a selected party becomes not found, forbidden, gone, erased, or unavailable
**When** the picker refreshes selected display state
**Then** it reports a bounded localized status
**And** it does not replace the durable id with personal data.

**Given** selection tests run
**When** they cover selection, host callback, preselected id, unavailable selected party, and payload inspection
**Then** only party id is durable.

### Story 8.4: Handle Picker States and Stale Responses

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR67; UX-DR26, UX-DR27

As a host application user,
I want the picker to handle loading, empty, retry, degraded, unauthorized, forbidden, not-found, gone/erased, and transient failures,
So that selection remains safe and understandable across changing host context.

**Acceptance Criteria:**

**Given** the picker is loading results or selected display state
**When** a request is in flight
**Then** it shows a localized loading state
**And** previous unsafe or stale results are not presented as current.

**Given** token, tenant, user, host configuration, selected id, or search options change
**When** an in-flight response returns
**Then** stale responses are cleared or suppressed
**And** the picker reloads only for the current context.

**Given** unauthorized, forbidden, not-found, gone/erased, degraded/local-only, or transient-failure states occur
**When** the picker renders the state
**Then** it shows bounded localized status text
**And** it uses non-color-only status indicators.

**Given** a retryable failure occurs
**When** the user activates retry
**Then** the picker retries the current safe request context
**And** focus returns to the initiating control or relevant status region.

**Given** state tests run
**When** they cover loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, transient failures, stale responses, and context changes
**Then** the picker never shows stale cross-context data.

### Story 8.5: Enforce Picker Accessibility and Localization

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR67; UX-DR28

As a host application user using keyboard or assistive technology,
I want the party picker to be accessible and localized,
So that I can search and select parties without inaccessible or hard-coded interactions.

**Acceptance Criteria:**

**Given** the picker renders search input, result list, selection display, retry control, and status messages
**When** a user navigates by keyboard
**Then** all controls are reachable and operable
**And** visible focus is always present.

**Given** result options are shown
**When** assistive technology inspects them
**Then** each option has a useful accessible name and state
**And** selected, disabled, degraded, erased, and unavailable states are not color-only.

**Given** picker status changes
**When** loading, empty, error, retry, selection, or degraded states occur
**Then** localized status text is announced appropriately
**And** announcements are bounded and privacy-safe.

**Given** labels, placeholders, validation messages, state text, and counts render
**When** localization resources are inspected
**Then** user-facing text comes from localized strings or FrontComposer localization
**And** missing resources are detectable in tests.

**Given** accessibility/localization tests run
**When** they cover keyboard operation, visible focus, accessible names, status announcements, non-color-only state, localization, forced-colors, and reduced-motion expectations
**Then** the picker is accessible for embedded use.

### Story 8.6: Enforce Picker Privacy and Integration Boundary

**Phase:** v1.2
**Coverage type:** deferred for MVP implementation planning
**Requirements covered:** FR67; UX-DR29, UX-DR30, UX-DR31, UX-DR32

As a privacy-conscious host application developer,
I want the picker to avoid leaking personal data or bypassing the accepted Parties boundary,
So that embedded selection remains safe in every consuming application.

**Acceptance Criteria:**

**Given** party data, host labels, backend messages, degraded reasons, localized values, or raw user search text are displayed
**When** the picker renders content
**Then** everything is encoded through normal component text paths
**And** raw markup, raw HTML fragments, and unsafe JavaScript interpolation are not used.

**Given** the picker creates storage keys, telemetry dimensions, URLs, logs, filenames, DOM event names, or JavaScript event payloads
**When** party/search/context data is available
**Then** names, contacts, identifiers, consent text, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads are excluded
**And** only non-PII safe identifiers are used where explicitly allowed.

**Given** the picker needs party data
**When** it queries
**Then** it goes through the EventStore-fronted Parties client boundary
**And** it never calls retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.

**Given** host auth context is provided
**When** the picker operates
**Then** it does not persist, refresh, parse for authorization, or log tokens
**And** missing or invalid context fails closed.

**Given** privacy/boundary tests run
**When** they inspect rendered output, callbacks, logs, telemetry, routes, storage, JavaScript event payloads, endpoint usage, and token handling
**Then** no PII leakage or boundary bypass is detected.
