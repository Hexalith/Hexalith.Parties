---
date: '2026-05-24'
project: 'Hexalith.Parties'
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
status: needs-work
includedFiles:
  prd:
    - _bmad-output/planning-artifacts/prd.md
    - _bmad-output/planning-artifacts/prd-validation-report.md
  architecture:
    - _bmad-output/planning-artifacts/architecture.md
  epics:
    - _bmad-output/planning-artifacts/epics.md
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23-epic8-picker-gate.md
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md
  ux:
    - _bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md
    - _bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-24
**Project:** Hexalith.Parties

## Step 1: Document Discovery

### PRD Files Found

**Whole Documents:**
- `prd.md` (85135 bytes, modified 2026-05-22 21:01:16 +02:00)
- `prd-validation-report.md` (26454 bytes, modified 2026-03-02 16:05:46 +01:00)

**Sharded Documents:**
- None

### Architecture Files Found

**Whole Documents:**
- `architecture.md` (103620 bytes, modified 2026-05-22 21:01:16 +02:00)

**Sharded Documents:**
- None

### Epics & Stories Files Found

**Whole Documents:**
- `epics.md` (208831 bytes, modified 2026-05-22 21:01:16 +02:00)
- `sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md` (64322 bytes, modified 2026-05-22 21:01:16 +02:00)
- `sprint-change-proposal-2026-05-23-epic8-picker-gate.md` (9687 bytes, modified 2026-05-23 11:14:33 +02:00)
- `sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md` (11164 bytes, modified 2026-05-24 13:48:22 +02:00)

**Sharded Documents:**
- None

### UX Design Files Found

**Whole Documents:**
- `ux-admin-portal-2026-05-10.md` (7488 bytes, modified 2026-05-15 08:22:58 +02:00)
- `ux-party-picker-2026-05-12.md` (1997 bytes, modified 2026-05-12 21:27:01 +02:00)

**Sharded Documents:**
- None

### Issues Found

- No whole-vs-sharded duplicate document conflicts found.
- No required document family is missing.
- Sprint change proposals matching the epic search pattern are included as planning context.

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
FR30: System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action — enabling developers to resolve the issue without consulting documentation or debugging the service
FR31: Developer can deploy the full Parties topology from source to a Kubernetes target (local cluster — kind/minikube/k3d/Docker Desktop — for MVP) using artifacts generated from the Aspire AppHost
FR31a: A single PowerShell pipeline (`pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>`) takes the operator from a clean checkout to a healthy 9-pod cluster in one command. The pipeline (Epic 9 v2, 2026-05-21) resolves the MinVer-stamped image tag, builds and pushes 7 container images to the self-hosted Zot OCI registry at `registry.hexalith.com` (ADR D-K8s-1), regenerates Kubernetes manifests via `dotnet aspirate generate`, applies three idempotent post-aspirate patches (Dapr annotations + JWT `secretKeyRef` + Zot `imagePullSecrets`), bootstraps three operator-managed Secrets (`hexalith-jwt-signing`, `hexalith-keycloak-admin`, `zot-pull-secret` — ADR D-K8s-2 Path B), applies Dapr CRs from `deploy/dapr/` (Components → Configurations → Subscriptions), then applies the Kustomization under `deploy/k8s/`. The topology is **enumerative**: 7 Aspirate-composed services (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`) plus 2 hand-authored carve-outs (`redis` MVP emptyDir + no AUTH; `keycloak` with randomized admin from Secret), totalling 9 workloads in namespace `hexalith-parties`. Image tags carry the MinVer-resolved version (regex `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$`) and are immutable per commit; mutable tags (`latest`, `staging-latest`, empty) are explicitly forbidden by `validate-deployment.ps1`. The `-ConfirmContext` gate (ADR D-K8s-3) replaces the legacy local-cluster regex allowlist so the same pipeline runs against any operator-owned kubectl context. The canonical architecture reference is `docs/kubernetes-deployment-architecture.md` (13 sections covering topology, configuration sources, operator workflow, reproducibility guarantees, and MVP boundaries). NFR30 (< 15 min from clean checkout to first successful query) remains in force.
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
FR72: *(Deferred to v1.1)* Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit.
FR73: System delivers events for a single aggregate in causal order to each subscriber. Architecture must verify DAPR pub/sub ordering guarantees. If per-aggregate ordering cannot be guaranteed, document required handler design (order-tolerant or sequence-checking) in the architecture document.
FR74: MCP update operations use patch semantics — only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update

Total FRs: 75 labeled functional requirement entries (`FR1`-`FR74`, plus `FR31a`).

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

Total NFRs: 33 labeled non-functional requirement entries (`NFR1`-`NFR32`, plus `NFR14a`).

### Additional Requirements

- MVP explicitly excludes GDPR compliance features; the service must warn operators not to store regulated EU personal data until v1.1 and keep a non-dismissable warning in admin UI headers and API response headers until GDPR activation.
- MVP explicitly excludes duplicate detection; `find_parties` display-name metadata is advisory, and consuming apps/operators remain responsible for deduplication until v2.
- MVP success gates are hard for deploy time, first command, MCP single-prompt creation, and documentation self-service quality; soft gates cover AI resolution and EventStore convention validation.
- v1.1 activates GDPR compliance: crypto-shredding, consent, data portability, restriction, erasure verification, processing records, locale-aware name formatting, semantic search, temporal name query API, admin dashboard, DPO workflow, GDPR banner removal, and production EU observability/runbooks.
- v1.2 introduces FrontComposer-based admin portal and embeddable party picker.
- v2 introduces duplicate detection and party merge, advanced search, relationships, cross-tenant party sharing, bulk import, self-service portal, address validation extensions, and horizontal scale beyond 100K parties per tenant.
- Business-specific roles such as customer and supplier are explicitly excluded from Hexalith.Parties and belong to consuming applications.
- The service manages external third-party entities, not authenticated users; it complements identity providers rather than replacing them.
- EventStore platform validation is a strategic project objective; if EventStore infrastructure work exceeds 60% of total effort for 2 consecutive sprints, a go/no-go review is required.
- EventStore defects discovered during Parties implementation are expected platform validation work; correct EventStore rather than adding Parties-side workarounds.
- Domain events are append-only immutable contracts; consumers must tolerate missing optional fields and unknown additive fields.
- Atomic event persistence is required: all events from one command are committed together or not at all.
- Party aggregate size is expected to handle up to 50 contact channels per party, with monitoring beyond that guideline.
- Snapshot integrity must be checksum-verifiable, and corrupted snapshots must rebuild from event stream with logged warning.
- Commands to the same aggregate must be serialized by actor semantics.
- Name history must be preserved in the event stream for temporal queries.
- Field-level encryption must be driven by `[PersonalData]` attributes with zero DAPR awareness in domain code.
- Erasure must include events, snapshots, projections, caches, and search indexes; reads of erased parties return erased status rather than crypto errors.
- DAPR pub/sub outage must not lose committed events; delayed delivery and retry/outbox behavior must be specified in architecture.
- Event-side consumers must implement idempotent handlers because delivery is at-least-once.
- `PartyMerged` must exist in v1 contracts for forward compatibility even if merge behavior is v2.
- Client packages must keep infrastructure out of consuming apps: Contracts has no runtime dependencies beyond netstandard2.1; Client depends only on Contracts and HTTP abstractions.
- REST is the only guaranteed MVP API surface; gRPC is explicitly not MVP and is only a v1.1 candidate if demand warrants.
- REST versioning uses URL-path versioning (`/api/v1/`) for discoverability.
- Errors must map to RFC Problem Details with a documented error catalog and status distinctions for syntactic validation, semantic validation, not found, conflict, and tenant violations.
- Rate limiting is an infrastructure concern and must not be implemented as Parties domain logic.
- Documentation deliverables include README, getting-started guide, GDPR disclaimer, OpenAPI spec, and sample integration.
- Sample integration must demonstrate command, query, event subscription, and MCP usage.

### PRD Completeness Assessment

The PRD is broad and unusually complete for readiness validation. It defines explicit FR/NFR inventories, target personas and journeys, MVP/growth phasing, success gates, technical constraints, security/compliance requirements, API surfaces, package expectations, and documentation deliverables.

Initial concerns to carry into coverage validation:

- Functional requirement numbering is non-linear because later requirements were inserted into topical sections. Traceability must use labels rather than assuming numeric order.
- `FR31a` is much more implementation-specific than the other FRs and may be better treated as a deployment epic acceptance bundle during story validation.
- MVP, v1.1, and v1.2 requirements are mixed in one FR list; epics and stories must preserve phase boundaries so post-MVP work is not accidentally treated as an MVP blocker.
- The project classification mentions REST/gRPC, while the API section states REST is the only guaranteed MVP surface and gRPC is a v1.1 candidate. Architecture and epics should align to the narrower API decision.
- Several NFRs require explicit verification evidence, especially cross-tenant isolation under load, DAPR event ordering, deployment time, package dependency limits, and degraded-mode read behavior.

## Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document contains a top-level `FR Coverage Map` and story-level `Requirements covered/prepared/supported` declarations. The top-level map covers `FR1` through `FR74`; `FR31a` is covered by Epic 9 and its stories, but is not present in the top-level map.

Top-level epic allocation:

- Epic 1 - Party Records and Lifecycle: `FR1`-`FR13`, `FR36`, `FR42`, `FR43`, `FR69`
- Epic 2 - Tenant-Safe Party Search and Retrieval: `FR14`-`FR19`, `FR39`-`FR41`, `FR64`, `FR68`, `FR71`, `FR72`
- Epic 3 - Developer Integration and Local Adoption: `FR26`-`FR33`, `FR56`-`FR62`
- Epic 4 - AI Agent Party Management: `FR20`-`FR25`, `FR74`
- Epic 5 - Event-Driven Consumer Integration: `FR34`, `FR35`, `FR37`, `FR38`, `FR63`, `FR70`, `FR73`
- Epic 6 - GDPR Compliance Operations: `FR44`-`FR55`
- Epic 7 - Administration Console: `FR65`, `FR66`
- Epic 8 - Embeddable Party Picker: `FR67`
- Epic 9 - Kubernetes Deployment Platform: `FR31a`, with support for `FR31`, `FR60`, `FR61`, and `NFR30`

Total FRs in epics: 75 labeled PRD FR entries are covered when Epic 9 story-level coverage for `FR31a` is included.

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
|---|---|---|---|
| FR1 | Authorized client can create a new party as either a person or an organization with type-specific details | Epic 1 | Covered |
| FR2 | Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix) | Epic 1 | Covered |
| FR3 | Authorized client can update organization-specific details (legal name, trading name, legal form, registration number) | Epic 1 | Covered |
| FR4 | Authorized client can deactivate a party (soft lifecycle management) | Epic 1 | Covered |
| FR5 | Authorized client can reactivate a previously deactivated party | Epic 1 | Covered |
| FR6 | System derives display name and sort name automatically from type-specific details using documented derivation rules | Epic 1 | Covered |
| FR7 | Each party has a client-generated, immutable UUID as its stable identity | Epic 1 | Covered |
| FR8 | Authorized client can add a contact channel to a party with type-specific structured data | Epic 1 | Covered |
| FR9 | Authorized client can update an existing contact channel on a party | Epic 1 | Covered |
| FR10 | Authorized client can remove a contact channel from a party | Epic 1 | Covered |
| FR11 | Authorized client can mark a contact channel as preferred for its type | Epic 1 | Covered |
| FR12 | Authorized client can add an identifier to a party | Epic 1 | Covered |
| FR13 | Authorized client can remove an identifier from a party | Epic 1 | Covered |
| FR14 | Consumer can list parties with pagination and filtering by type and active status | Epic 2 | Covered |
| FR15 | Consumer can search parties by display name in MVP | Epic 2 | Covered |
| FR16 | Deferred v1.1 semantic search across parties | Epic 2, prepared-deferred | Covered |
| FR17 | Search results include match metadata | Epic 2 | Covered |
| FR18 | Consumer can retrieve full party details by ID | Epic 2 | Covered |
| FR19 | Recently created or updated parties become discoverable within NFR6 window | Epic 2 | Covered |
| FR20 | AI agent can search and resolve parties by display name | Epic 4 | Covered |
| FR21 | AI agent can create a complete party in a single composite operation | Epic 4 | Covered |
| FR22 | AI agent can update details, channels, and identifiers via a single operation | Epic 4 | Covered |
| FR23 | AI agent can retrieve full details and list parties via AI-optimized tools | Epic 4 | Covered |
| FR24 | AI agent party creation returns the complete created party record | Epic 4 | Covered |
| FR25 | AI agent tools accept partial input gracefully with clear validation errors | Epic 4 | Covered |
| FR26 | .NET developer can integrate via a single package and one-line registration | Epic 3 | Covered |
| FR27 | Developer can send commands via typed client abstractions | Epic 3 | Covered |
| FR28 | Developer can query parties via typed client abstractions | Epic 3 | Covered |
| FR29 | Developer can interact via REST API from any programming language | Epic 3 | Covered |
| FR30 | System returns typed rejection responses with corrective action | Epic 3 | Covered |
| FR31 | Developer can deploy the full topology to Kubernetes using AppHost-generated artifacts | Epic 3 and Epic 9 | Covered |
| FR31a | One-command PowerShell Kubernetes publish pipeline to a healthy 9-pod cluster with immutable image tags and required patches/secrets/Dapr resources | Epic 9 | Covered |
| FR32 | Getting-started docs enable self-service deploy and first command | Epic 3 | Covered |
| FR33 | Contracts package has zero runtime dependencies beyond netstandard2.1 | Epic 3 | Covered |
| FR34 | System publishes domain events when party state changes | Epic 5 | Covered |
| FR35 | Consuming app can subscribe to party events and build read models | Epic 5 | Covered |
| FR36 | System handles duplicate commands idempotently | Epic 1 | Covered |
| FR37 | Forward-compatible event contracts are available from day one | Epic 5 | Covered |
| FR38 | Consumer docs include erasure and dangling-reference handler patterns | Epic 5 | Covered |
| FR39 | System isolates party data by tenant at all layers | Epic 2 | Covered |
| FR40 | System identifies tenant from authenticated credentials only | Epic 2 | Covered |
| FR41 | System rejects requests without valid tenant identity | Epic 2 | Covered |
| FR42 | Personal data fields are architecturally marked for privacy enforcement | Epic 1 | Covered |
| FR43 | Personal data fields are excluded from application logging | Epic 1 | Covered |
| FR44 | Administrator can trigger right-to-erasure | Epic 6 | Covered |
| FR45 | System verifies erasure across internal stores | Epic 6 | Covered |
| FR46 | System notifies subscribers when a party is erased | Epic 6 | Covered |
| FR47 | Administrator can record per-channel, per-purpose consent | Epic 6 | Covered |
| FR48 | Administrator can revoke previously recorded consent | Epic 6 | Covered |
| FR49 | Administrator can restrict processing | Epic 6 | Covered |
| FR50 | Administrator can lift processing restriction | Epic 6 | Covered |
| FR51 | Administrator can export party data in machine-readable format | Epic 6 | Covered |
| FR52 | System maintains time-stamped processing activity records | Epic 6 | Covered |
| FR53 | System encrypts personal data in events and snapshots | Epic 6 | Covered |
| FR54 | Published events contain readable data for subscribers | Epic 6 | Covered |
| FR55 | System returns erased status instead of crypto errors | Epic 6 | Covered |
| FR56 | System publishes auto-generated API specification documentation | Epic 3 | Covered |
| FR57 | System supports versioned API endpoints during deprecation | Epic 3 | Covered |
| FR58 | System maps domain rejections to standardized HTTP errors | Epic 3 | Covered |
| FR59 | System provides runnable sample integration project | Epic 3 | Covered |
| FR60 | Developer can run the full system locally with a single command | Epic 3 | Covered |
| FR61 | System provides deployment validation tooling for security config | Epic 3 and Epic 9 | Covered |
| FR62 | System displays non-dismissable compliance warning until GDPR activation | Epic 3 | Covered |
| FR63 | System guarantees at-least-once event delivery | Epic 5 | Covered |
| FR64 | System degrades gracefully when infrastructure components are unavailable | Epic 2 | Covered |
| FR65 | Administrator can browse, search, and inspect party records | Epic 7 | Covered |
| FR66 | Administrator can process GDPR requests through admin interface | Epic 7 | Covered |
| FR67 | Developer can embed a party picker component | Epic 8 | Covered |
| FR68 | Consumer can filter parties by creation or last-modified date range | Epic 2 | Covered |
| FR69 | Update operations return updated party state | Epic 1 | Covered |
| FR70 | Published events include tenant context | Epic 5 | Covered |
| FR71 | System exposes health and readiness signals | Epic 2 | Covered |
| FR72 | Deferred v1.1 temporal historical name query | Epic 2, prepared-deferred | Covered |
| FR73 | System delivers single-aggregate events in causal order, with architecture verification of Dapr guarantees | Epic 5 | Covered |
| FR74 | MCP update operations use patch semantics | Epic 4 | Covered |

### Missing Requirements

No PRD FRs are missing from the epics/story coverage set.

Traceability issue:

- `FR31a` is absent from the top-level `FR Coverage Map`, but is covered by Epic 9 and story-level declarations. Recommendation: update the top-level coverage map so `FR31a` appears beside Epic 9 and the map total matches the PRD inventory.

No FRs were found in the epics coverage set that are absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 75
- FRs covered in epics/stories: 75
- Missing FRs: 0
- Extra FR references not in PRD: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found:

- `ux-admin-portal-2026-05-10.md`
- `ux-party-picker-2026-05-12.md`

No sharded UX document folder was found.

### UX to PRD Alignment

- Admin Portal UX aligns with `FR65` (browse/search/inspect party records), `FR66` (GDPR request processing through admin interface), and `NFR32` (encoded frontend rendering / stored-XSS prevention).
- Party Picker UX aligns with `FR67` (embeddable party picker) and reinforces the PRD's privacy/security posture by making party id the only durable selection contract.
- UX scope is explicitly v1.2 and is consistent with the PRD phasing: Admin Portal and Party Picker are not MVP blockers.
- The Admin Portal UX intentionally delegates generic stream/correlation inspection to EventStore Admin UI safe deep-links, which aligns with the PRD boundary that Parties should provide a domain admin surface rather than a generic EventStore browser.
- The UX details for fail-closed states, tenant switch cancellation, stale response clearing, localization, accessibility, and privacy-safe rendering are represented in the epics as `UX-DR1` through `UX-DR32`.

### UX to Architecture Alignment

- Architecture decision D20 supports the Admin Portal UX: FrontComposer domain surface, Blazor/Razor, Fluent UI Blazor, EventStore query/client abstractions, typed Parties client/EventStore command boundary, and safe EventStore Admin UI deep-links.
- D20 also supports the Admin Portal's fail-closed state model, sensitive-state clearing, localization, keyboard/focus behavior, non-color-only status, and polite status announcements.
- The Architecture `Party Picker Frontend Surface` section directly supports the picker UX: embeddable FrontComposer/Blazor component, tenant-safe search, party-id-only durable selection, no personal data in durable host keys/URLs/telemetry/logs/DOM event names, and accepted EventStore-fronted Parties client/gateway boundary.
- Architecture coverage explicitly marks Admin `FR65`-`FR67` as 3/3 covered and `NFR32` as covered under Developer Experience.

### Alignment Issues

No blocking UX/PRD/Architecture alignment gaps were found.

Non-blocking traceability and dependency issues:

- The source UX documents describe the requirements in prose/tables, while the numbered `UX-DR1`-`UX-DR32` identifiers appear in `epics.md`. Recommendation: either add the `UX-DR` identifiers to the UX source documents or keep the epics UX inventory clearly marked as the canonical numbered extraction.
- Admin Portal production readiness depends on the accepted EventStore-fronted Parties client/gateway contract exposing required typed query and command capabilities. This is documented in both UX and architecture, but should remain visible as an implementation dependency for Epic 7.
- The architecture lists Admin frontend architecture under deferred post-MVP decisions while also marking D20 as decided by the FrontComposer/EventStore course correction. This is not a functional conflict, but the wording should be read as "implementation deferred, architecture decision made."

### Warnings

- UX is required by the PRD because `FR65`, `FR66`, `FR67`, and `NFR32` are user-facing/frontend requirements. UX documentation exists, so there is no missing-UX warning.
- Accessibility, localization, stale-response handling, encoded rendering, and privacy-safe telemetry/storage are contractual UX concerns; story quality review must verify these are not reduced to visual-only acceptance criteria.

## Epic Quality Review

### Review Scope

Reviewed `epics.md` against create-epics-and-stories standards:

- 9 epics
- 76 stories
- All stories include phase, coverage type, requirements covered/prepared/supported, user-story framing, and Given/When/Then acceptance criteria.
- No missing FR coverage was found in Step 3.
- No relational database/table upfront-creation anti-pattern was found; the system is event-sourced and projection-oriented, and storage work is generally introduced with the story that needs it.
- Architecture specifies an EventStore solution-structure starter pattern; Story 1.1 exists and addresses the starter-template/setup requirement.

### Epic-Level Checklist

| Epic | User Value Focus | Independence | Story Structure | Finding |
|---|---|---|---|---|
| Epic 1 - Party Records and Lifecycle | Strong | Pass | Strong | Story 1.1 is technical setup, but acceptable for greenfield starter-template requirement. |
| Epic 2 - Tenant-Safe Party Search and Retrieval | Strong | Pass | Strong | Story 2.9 is prepared-deferred and must not be treated as completed semantic/temporal search behavior. |
| Epic 3 - Developer Integration and Local Adoption | Strong | Pass | Strong | Good developer-value framing and testable ACs. |
| Epic 4 - AI Agent Party Management | Strong | Pass | Strong | Good user-value framing for MCP workflows. |
| Epic 5 - Event-Driven Consumer Integration | Strong | Pass | Strong | Stories 5.6/5.7 are preparation-only and should stay labeled that way. |
| Epic 6 - GDPR Compliance Operations | Strong | Pass for v1.1 | Strong | Story 6.10 is NFR/security coverage, not FR coverage; acceptable if tracked as NFR work. |
| Epic 7 - Administration Console | Strong | Conditional | Strong | Story 7.6 depends on accepted EventStore-fronted Parties client/gateway contract. |
| Epic 8 - Embeddable Party Picker | Strong | Pass | Strong | Good alignment with UX and privacy boundary. |
| Epic 9 - Kubernetes Deployment Platform | Mixed | Fails strict independence | Oversized | Explicit operator value exists, but title/framing is technical and several stories contain forward dependencies. |

### Critical Violations

#### 1. Epic 9 Contains Forward Story Dependencies

Forward dependencies are forbidden because they prevent stories from being independently completed and reviewed.

Examples:

- Story 9.2 says `dotnet aspirate generate` is invoked by `publish.ps1`, but `publish.ps1` is delivered later in Story 9.5.
- Story 9.3 references the matching Dapr Component from Story 9.4, the Keycloak admin Secret bootstrap from Story 9.5, post-aspirate patches from Stories 9.2/9.5, and a `CarveOutPreservationFitnessTest` delivered in Story 9.7.
- Story 9.4 references the post-aspirate Dapr-annotation patch from Stories 9.2/9.5.

Impact:

- Story completion cannot be verified cleanly without future stories.
- Reviewers may accept placeholder assumptions instead of executable evidence.
- Sprint sequencing becomes fragile because a change in Story 9.5 or 9.7 can invalidate earlier story acceptance.

Recommendation:

- Move acceptance criteria into the story that delivers the validating mechanism.
- For earlier stories, validate only artifacts available at that point through direct inspection or local commands.
- Reorder Epic 9 so shared publish/patch/test harness work lands before stories that depend on it, or split the shared harness into an earlier enabling story.

#### 2. Epic 9 Is MVP Scope but Numbered After Deferred v1.1/v1.2 Epics

Epic 9 is marked MVP and covers `FR31a`, `FR31`, `FR60`, `FR61`, and `NFR30`, but it appears after Epic 6 (v1.1), Epic 7 (v1.2), and Epic 8 (v1.2).

Impact:

- If implementation planning follows numeric epic order, MVP deployment work may be scheduled after deferred post-MVP features.
- The document mixes release phase and epic number in a way that can mislead sprint planning.

Recommendation:

- Either reorder Epic 9 before deferred post-MVP epics or add an explicit implementation sequence table that places Epic 9 in the MVP lane.
- Treat `Phase` as authoritative if numbering remains historical.

### Major Issues

#### 1. Epic 9 Is Technically Framed Despite Having Operator Value

`Kubernetes Deployment Platform` is a technical/infrastructure title. The epic text does describe operator/developer value, but the title and story breakdown emphasize infrastructure components rather than the user outcome.

Recommendation:

- Rename toward the outcome, for example: `Operators Deploy Parties to Kubernetes with One Command`.
- Keep infrastructure details in acceptance criteria and architecture references, not as the primary epic framing.

#### 2. Epic 9 Stories Are Oversized

Oversized examples:

- Story 9.1 includes Zot deployment, registry auth model, credential policy, tag policy, canonical docs, ADRs, deployment docs, and documentation fitness checks.
- Story 9.4 combines Dapr control plane installation, Components, access-control configurations, Subscriptions, ordering, and alternative-backend exclusion.
- Story 9.5 combines `publish.ps1`, `teardown.ps1`, context helper, MinVer resolution, secret bootstrap, Dapr/apply sequencing, idempotency, credential-safety, and runtime duration expectations.
- Story 9.7 combines deploy-validation unit fitness tests and live-cluster integration tests.

Impact:

- Stories are too large for focused review and rollback.
- Acceptance evidence will be broad and slow to produce.
- Bugs may be hard to localize.

Recommendation:

- Split Story 9.1 into registry, tagging/credential ADRs, and documentation consistency stories.
- Split Story 9.4 into Dapr Components/Subscribers and Dapr access-control stories.
- Split Story 9.5 into publish pipeline, teardown pipeline, shared context helper, and secret-safety/idempotency stories.
- Split Story 9.7 into static deploy-validation fitness tests and opt-in live-cluster integration tests.

#### 3. Story 7.6 Has an External Dependency That Must Be Resolved Before Scheduling

Story 7.6 explicitly depends on an accepted EventStore-fronted Parties client/gateway contract. This is transparent and well documented, but still a scheduling risk.

Recommendation:

- Create or link the external dependency record before scheduling Story 7.6.
- Add an entry criterion: contract shape accepted, versioned, and available to Admin Portal implementation.

#### 4. Prepared-Deferred Stories Can Be Misread as Runtime Feature Completion

Stories 2.9, 5.6, and 5.7 are intentionally preparation-only. They are valuable, but they do not complete deferred runtime behavior such as semantic search, temporal query, party merge, or GDPR erasure.

Recommendation:

- Preserve `prepared-deferred` labels.
- Do not count these as runtime feature delivery in sprint completion metrics.
- Ensure final readiness distinguishes "contract/docs prepared" from "feature implemented."

### Minor Concerns

- Story 1.1 contains slightly vague acceptance wording: "represented only as needed for subsequent stories" and "built enough to support the first domain story." Tighten with explicit project/build expectations if the story is reopened.
- Several story titles use technical verbs (`Build`, `Configure`, `Validate`, `Enforce`). The user-story body usually restores user value, but titles could be more outcome-oriented.
- `FR31a` is covered in Epic 9 but missing from the top-level `FR Coverage Map`; keep the map synchronized with the PRD.
- `UX-DR1`-`UX-DR32` are extracted in `epics.md` but not labeled in the UX source documents; keep one canonical traceability source or add identifiers to the UX docs.

### Best-Practices Compliance Summary

- Epic user value: mostly pass; Epic 9 needs reframing.
- Epic independence: pass for Epics 1-8 by phase; fail for Epic 9 due forward story dependencies.
- Story sizing: generally pass outside Epic 9; Epic 9 has multiple oversized stories.
- Acceptance criteria: generally strong BDD structure; a few vague phrases and long compound criteria need tightening.
- Dependencies: forward dependencies found in Epic 9; external dependency found in Story 7.6.
- Starter-template setup: present in Story 1.1.
- Database/entity creation timing: no upfront relational table anti-pattern found.

## Summary and Recommendations

### Overall Readiness Status

**NEEDS WORK**

The planning set is strong: required documents exist, PRD requirements are explicit, all PRD FRs have an epic/story coverage path, UX documentation exists and aligns with PRD/architecture, and most stories have good BDD acceptance criteria.

Do not treat the full implementation plan as ready until the Epic 9 structural issues are corrected. Epic 9 is MVP-scoped deployment work and currently contains forward dependencies, phase-order confusion, and oversized stories. Those are implementation-readiness defects, not content preferences.

### Critical Issues Requiring Immediate Action

1. **Fix Epic 9 forward dependencies.** Story 9.2 depends on Story 9.5, Story 9.3 depends on Stories 9.4/9.5/9.7, and Story 9.4 depends on Story 9.5. Move acceptance criteria to the story that delivers the required mechanism or reorder/split stories so each story is independently completable.
2. **Clarify Epic 9 implementation sequence.** Epic 9 is MVP work but appears after v1.1/v1.2 epics. Either move Epic 9 into the MVP epic sequence or add an explicit implementation-sequence table that makes phase order authoritative.

### Major Issues To Resolve

1. **Reframe Epic 9 around operator value.** The current title, `Kubernetes Deployment Platform`, is technical. The epic value is one-command operator/developer deployment; make that the epic title and goal.
2. **Split oversized Epic 9 stories.** Story 9.1, 9.4, 9.5, and 9.7 are too broad for clean implementation and review. Split registry/docs/ADR work, Dapr component/ACL work, publish/teardown/helper work, and static/live validation work.
3. **Resolve Story 7.6 dependency before scheduling.** Link or create the accepted EventStore-fronted Parties client/gateway contract dependency before Admin Portal GDPR operation work starts.
4. **Keep prepared-deferred stories distinct from runtime delivery.** Stories 2.9, 5.6, and 5.7 are useful preparation, but they must not be counted as completed semantic search, temporal query, party merge, or GDPR erasure runtime behavior.

### Minor Cleanup

1. Add `FR31a` to the top-level `FR Coverage Map`.
2. Decide whether `UX-DR1`-`UX-DR32` should be canonical in `epics.md` only or copied back into the UX documents.
3. Tighten vague Story 1.1 wording such as "as needed for subsequent stories" and "built enough to support the first domain story."
4. Consider outcome-oriented titles for technical-sounding stories where the user-story body already contains the real user value.

### Recommended Next Steps

1. Rewrite Epic 9 as a dependency-safe MVP epic before starting implementation: split large stories and remove forward acceptance criteria.
2. Add a release/implementation sequence table that separates MVP, v1.1, and v1.2 work independent of historical epic numbering.
3. Update the FR coverage map to include `FR31a` under Epic 9.
4. Create or link the EventStore-fronted Parties client/gateway contract dependency required by Story 7.6.
5. Preserve the current PRD, UX, and architecture alignment; most readiness risk is now in epic/story structure, not requirements coverage.

### Final Note

This assessment identified **10 issues** across **4 categories**:

- Traceability maintenance
- UX traceability and dependency visibility
- Epic sequencing/dependency structure
- Story sizing and acceptance-quality cleanup

Address the 2 critical issues before proceeding with Epic 9 implementation. The rest can be handled as backlog hygiene, but the major issues will materially improve reviewability and sprint execution.

**Assessment date:** 2026-05-24
**Assessor:** Codex using `bmad-check-implementation-readiness`
