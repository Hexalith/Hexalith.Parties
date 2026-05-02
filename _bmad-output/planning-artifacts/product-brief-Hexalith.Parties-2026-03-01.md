---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments: []
date: 2026-03-01
author: Jérôme
---

# Product Brief: Hexalith.Parties

<!-- Content will be appended sequentially through collaborative workflow steps -->

## Executive Summary

Hexalith.Parties is an open-source (MIT-licensed), multi-tenant microservice that serves as the **single source of truth for party management** — individuals and organizations — across all applications in the Hexalith ecosystem. Inspired by enterprise party management models (Dynamics 365 F&O Global Address Book, SAP Business Partner, Oracle Trading Community Architecture), it delivers the same conceptual richness as a standalone, event-sourced, cloud-native microservice without ERP licensing or vendor lock-in.

The service manages the common foundation of any third-party entity: identity, type (person or organization), type-specific details (person name and date of birth, or organization legal name and legal form), contact channels (a unified, type-discriminated abstraction covering postal addresses, emails, phones, social media, and future contact methods), and identifiers (VAT, SIRET, national ID). It explicitly excludes business-specific roles — "customer" or "supplier" are relationships assigned by consuming applications, not intrinsic party properties.

Built on [Hexalith.EventStore](https://github.com/Hexalith/Hexalith.EventStore), a DAPR-native CQRS/Event Sourcing framework for .NET, the Party aggregate code has zero direct DAPR awareness — DAPR is an infrastructure concern of EventStore, not of the party domain. Consuming application developers do not need DAPR installed — they interact via the Hexalith.Parties.Client NuGet package over REST/gRPC. DAPR is only required for operators deploying the Parties service itself (.NET Aspire + Docker handles DAPR setup automatically for local development). The service exposes both a **command API** (with idempotent command handling) for writes and **read projections** for queries. Domain events are published for consuming applications to subscribe to, enabling real-time synchronization and supporting offline/disconnected scenarios through local read model construction.

A TypeScript web frontend provides an **embeddable party picker component** and **administration views** for browsing, searching, and updating party data. Full GDPR compliance is built in from the ground up: **crypto-shredding** for right-to-erasure, **per-channel per-purpose consent management**, **data portability export** (Article 20), **right to restriction** (Article 18), **records of processing activities** (Article 30), and **erasure verification** across all read models.

Hexalith.Parties is also the **first domain microservice built on the Hexalith.EventStore platform**, validating the programming model, infrastructure abstractions, and operational patterns for all future domain services in the ecosystem. Its success establishes confidence in the EventStore foundation for every subsequent domain microservice.

When every application builds its own contact management, the same party exists in multiple systems with inconsistent data, and regulatory compliance becomes fragmented and unreliable. Hexalith.Parties solves this once: one shared service, one source of truth, always in sync. New applications integrate via NuGet packages (Hexalith.Parties.Contracts, Hexalith.Parties.Client) and a one-line DI registration (`AddPartiesClient()`) in minutes rather than reimplementing party management over weeks.

---

## Core Vision

### Problem Statement

Modern distributed applications frequently need to manage third-party entities (contacts, customers, suppliers, actors) but each application tends to reimplement this complex concern independently. Party management involves intricate challenges: handling multiple contact methods with structured data, complying with European data protection regulations (GDPR — including consent, erasure, portability, restriction, and processing records), supporting both individuals and organizations with type-specific attributes, managing identifiers across jurisdictions, and keeping data consistent across systems. This duplication creates maintenance burden, inconsistent data quality, and fragmented regulatory compliance.

### Problem Impact

Without a shared party management service, every new application in the ecosystem must independently solve identity management, contact handling, and GDPR compliance — multiplying development effort and regulatory risk. Each reimplementation costs weeks of effort for GDPR alone, with additional time for multi-tenancy, contact management, and identifier handling. The same real-world party ends up duplicated across applications with divergent data, and regulatory obligations (erasure, portability, consent) must be fulfilled separately in each system — an error-prone and unsustainable approach.

### Why Existing Solutions Fall Short

- **CRM systems** (Salesforce, HubSpot) bundle party management with business-specific logic (sales pipelines, support tickets), making them too heavyweight and opinionated for use as a foundational building block.
- **Enterprise ERP modules** (D365 F&O Global Address Book, SAP Business Partner, Oracle Trading Community Architecture) offer the right conceptual model and domain richness but are locked within their respective ERP ecosystems, requiring expensive licenses and proprietary infrastructure.
- **Open-source solutions** rarely combine event sourcing, multi-tenancy, GDPR compliance, and infrastructure portability in a single, reusable service.
- **Custom per-app implementations** solve the immediate need but create duplication, inconsistency, and compounding maintenance costs across the portfolio.

### Proposed Solution

Hexalith.Parties provides a dedicated, reusable microservice that manages the complete lifecycle of parties — both persons and organizations. The **Party aggregate** is the core domain boundary, containing:

- **Identity** — client-generated, immutable UUIDs. Party IDs are stable and never change, ensuring consuming apps can safely use them as foreign keys without risk of ID reassignment.
- **Type** — person or organization (the only meaningful type distinction at this level)
- **Type-specific details:**
  - **PersonDetails** — first name, last name, date of birth, name prefix/suffix
  - **OrganizationDetails** — legal name, trading name, legal form, registration number
  - Exactly one is present based on party type. Display name and sort name are derived from the active detail type, with locale-aware name formatting. Name history is preserved in the event stream, supporting "name as of date" temporal queries for legal and audit purposes.
- **Contact channels** — a unified abstraction with type-discriminated payloads:
  - Common fields: channel type, verification status, preferred flag
  - **Postal payload:** street, city, zip/postal code, state/region, country, address lines
  - **Email payload:** email address
  - **Phone payload:** country code, number, extension
  - **Social payload:** platform, handle/URL
  - Extensible to future contact types (messaging apps, digital wallets) without schema changes.
- **Identifiers** — VAT numbers, SIRET, national IDs, and other jurisdiction-specific references, each with type, value, issuing authority, and validity period
- **Consent** — per-channel, per-purpose consent records. Consent is not a party-level flag but a structured, time-stamped, revocable record that tracks which processing purposes are consented for which contact channels (e.g., "marketing emails consented for email channel X on 2026-01-15").

The service explicitly excludes business-specific roles (customer, supplier, employee) — these are relationships assigned by consuming applications, not intrinsic party properties. Consuming apps reference parties by their stable UUID as a foreign key in their own domain contexts.

Built on Hexalith.EventStore, it follows the same aggregate programming model: domain logic expressed as pure `Handle(Command, State?) -> DomainResult` functions with `Apply(Event)` state mutations, auto-discovered by convention. The Party aggregate code has zero direct DAPR awareness — DAPR is an infrastructure concern managed entirely by EventStore, ensuring the domain logic remains pure and portable.

**Command side:** Commands sent via REST/gRPC API, routed to aggregate actors, persisting events and publishing to downstream subscribers. **Idempotent command handling** ensures that duplicate commands in distributed scenarios are safely deduplicated, a requirement for any shared microservice.

**Query side:** Read projections as first-class citizens — a default projection with paginated list, filtering, and display-name search included in v1. The projection infrastructure is extensible by design: the extensibility contract ships in v1, while email, identifier, semantic, and advanced search plugins (e.g., Elasticsearch) ship through the dedicated search capability. Consuming apps can also build domain-specific read models from published events, enabling offline/disconnected scenarios.

**Event publishing and encryption:** Events published to DAPR pub/sub are decrypted at publish time — consuming applications receive readable events and do not need access to encryption keys. After erasure, the `PartyErased` event notifies all subscribers to delete their local party data. Consuming apps never handle encryption directly.

**GDPR compliance:**

- **Crypto-shredding** — Personal data fields in events are encrypted with per-party keys (field-level encryption via attributes on the aggregate). Snapshots also participate in crypto-shredding since they contain current state with personal data. On right-to-erasure requests, the key is destroyed via DAPR secret store, rendering both event and snapshot data unreadable while preserving stream integrity.
- **Consent management** — Per-channel, per-purpose, time-stamped, revocable consent tracking. Not a simple boolean flag but a structured record of what processing was consented to, for which contact channel, and when. Designed to accommodate future regulations (ePrivacy, AI Act) as new consent purposes without structural changes.
- **Data portability** — Export all data for a specific party in machine-readable format (JSON) on request (GDPR Article 20).
- **Right to restriction** — Ability to freeze processing of a party's data while a complaint is investigated (GDPR Article 18).
- **Records of processing activities** — The event stream provides a complete, time-stamped record of all processing activities on party data, supporting GDPR Article 30 compliance.
- **Erasure verification** — Erasure triggers a domain event (`PartyErased`) that propagates to ALL projections, caches, and search indexes. A verification job confirms complete erasure across all read models.
- **Dangling reference guidance** — Consuming apps holding `partyId` foreign keys receive the `PartyErased` event and must handle reference cleanup (nullify, archive, or cascade delete depending on their domain rules). The Contracts package includes documentation and handler patterns for this scenario.

**Security architecture:**

- Tenant identity extracted from authenticated JWT token claims, never from request payloads
- Pub/sub topics tenant-scoped (`parties/{tenantId}/events`) with DAPR access control policies
- Automatic tenant filtering at the data access layer on all read queries
- Per-tenant encryption key namespaces in DAPR secret store with automated key rotation

**Frontend:** A TypeScript frontend with two delivery modes:
- **Party picker component** — an embeddable search-and-select widget that consuming apps integrate into their own UIs for party selection and quick creation. This is the primary frontend artifact, designed for maximum reuse.
- **Administration views** — standalone pages for operators to browse, search, and update party data, manage consent, and handle GDPR requests. Built with composable component boundaries (PartyList, PartyDetail, PartyForm, ContactChannelEditor, ConsentManager).

**Developer experience:** Consuming applications integrate via NuGet packages:
- **Hexalith.Parties.Contracts** — shared command, event, and query types (includes `PartyMerged` event contract for forward compatibility with v2 merge capability)
- **Hexalith.Parties.Client** — client abstractions (`IPartiesCommandClient`, `IPartiesQueryClient`) and one-line DI registration (`builder.Services.AddPartiesClient(config)`)

Applications send commands through `IPartiesCommandClient`, query parties through `IPartiesQueryClient`, subscribe to party events via DAPR pub/sub, and correlate parties in their own domain contexts using stable party UUIDs as foreign keys. Command rejections are returned as typed `DomainResult` responses with structured error details.

Consuming app developers do not need DAPR installed — the Client package communicates via REST/gRPC. DAPR is only required for deploying the Parties service itself.

Multi-tenancy is built-in at the contract level (Domain + AggregateId + TenantId), with tenant isolation enforced at every layer — aggregate, event store, projections, API, and frontend.

### Core Commands and Events

**Commands (v1):**

| Command | Description |
|---|---|
| `CreateParty` | Create a new party (person or organization) with initial details |
| `UpdatePersonDetails` | Update person-specific fields (name, date of birth) |
| `UpdateOrganizationDetails` | Update organization-specific fields (legal name, trading name) |
| `AddContactChannel` | Add a contact channel (postal, email, phone, social) |
| `UpdateContactChannel` | Update an existing contact channel |
| `RemoveContactChannel` | Remove a contact channel |
| `SetPreferredChannel` | Mark a contact channel as preferred for its type |
| `AddIdentifier` | Add an identifier (VAT, SIRET, etc.) |
| `RemoveIdentifier` | Remove an identifier |
| `RecordConsent` | Record consent for a specific purpose on a specific channel |
| `RevokeConsent` | Revoke previously recorded consent |
| `RestrictParty` | Freeze processing of a party's data (GDPR Article 18) |
| `LiftRestriction` | Resume processing after restriction |
| `RequestErasure` | Trigger crypto-shredding and erasure propagation |
| `ExportPartyData` | Generate a portable data export (GDPR Article 20) |
| `DeactivateParty` | Mark a party as inactive (soft deactivation) |
| `ReactivateParty` | Reactivate a previously deactivated party |

**Events (v1 Contracts):**

| Event | Description |
|---|---|
| `PartyCreated` | A new party was created |
| `PersonDetailsUpdated` | Person-specific fields were updated |
| `OrganizationDetailsUpdated` | Organization-specific fields were updated |
| `ContactChannelAdded` | A contact channel was added |
| `ContactChannelUpdated` | A contact channel was updated |
| `ContactChannelRemoved` | A contact channel was removed |
| `PreferredChannelSet` | A channel was marked as preferred |
| `IdentifierAdded` | An identifier was added |
| `IdentifierRemoved` | An identifier was removed |
| `ConsentRecorded` | Consent was recorded for a purpose/channel |
| `ConsentRevoked` | Consent was revoked |
| `PartyRestricted` | Processing was frozen (GDPR Article 18) |
| `RestrictionLifted` | Processing was resumed |
| `PartyErased` | Party data was crypto-shredded — consuming apps must clean up references |
| `PartyDataExported` | A portable data export was generated |
| `PartyDeactivated` | Party was marked inactive |
| `PartyReactivated` | Party was reactivated |
| `PartyMerged` | *(v2 logic, v1 contract)* Two parties were merged — includes `survivingId` and `mergedId` for consuming apps to update foreign keys |

### Key Differentiators

- **Single source of truth** — One shared service for all party data across all applications, always in sync through event-driven integration. Eliminates data silos and duplication.
- **First EventStore domain validation** — The first domain microservice on the Hexalith.EventStore platform, proving the programming model, infrastructure abstractions, and operational patterns for all future domain services.
- **Pure party domain brick** — Manages only the common foundation of third-party entities (identity, type, contact channels, identifiers, consent), free from business-specific concerns. Roles stay in consuming apps.
- **Enterprise model, open delivery** — D365 F&O Global Address Book / SAP Business Partner conceptual richness, delivered as an open-source MIT-licensed microservice with no ERP lock-in.
- **Hexalith.EventStore native** — Built on the shared DAPR-native CQRS/ES framework with convention-based aggregate discovery, MediatR pipeline, and FluentValidation. Party domain code has zero DAPR awareness. Consuming app developers don't need DAPR.
- **Unified contact channel model** — All contact methods unified under a single extensible abstraction with type-discriminated structured payloads, future-proof for new contact types.
- **CQRS with first-class read side** — Command API for writes with idempotent handling, default projections for queries, extensible projection infrastructure, snapshot strategy for scale.
- **Infrastructure portable** — Swap state stores (Redis, PostgreSQL, Cosmos DB) and message brokers (RabbitMQ, Kafka, Azure Service Bus) without code changes via DAPR.
- **GDPR by design** — Crypto-shredding (events and snapshots), per-channel per-purpose consent management, data portability export, right to restriction, processing records (Article 30), erasure verification, and dangling reference guidance. Consent model designed for regulatory extensibility beyond current GDPR.
- **Multi-tenant with defense in depth** — Tenant identity from JWT, tenant-scoped pub/sub, automatic tenant filtering on reads, per-tenant key namespaces, isolation enforced at every layer.
- **Designed for extension** — Clear aggregate boundaries, published events with stable contracts (including forward-compatible `PartyMerged`), and typed client interfaces allow consuming applications to extend party behavior without forking. Stable UUIDs as correlation keys.
- **Embeddable party picker** — TypeScript party picker component for direct embedding in consuming app UIs, plus standalone admin views for operators.

### Key Architecture Decisions

- **ADR-1: Party as single aggregate** — Contact channels, identifiers, consent records, and type-specific details (PersonDetails / OrganizationDetails) are value objects within the Party aggregate, not separate aggregates. One aggregate with type-discriminated composition, validated by type. Snapshots mitigate aggregate growth. This preserves transactional integrity and avoids distributed consistency problems. If v2 complexity warrants splitting into PersonAggregate and OrganizationAggregate, the contact channel, identifier, and consent value objects are shared and can be extracted into a common base.
- **ADR-2: Field-level crypto-shredding** — Personal data fields encrypted individually via aggregate attributes, with per-party keys managed through DAPR secret store. Snapshots also encrypted. Events published to pub/sub are decrypted at publish time — consuming apps never handle encryption. After erasure, consuming apps receive `PartyErased` and delete local data. Infrastructure-portable key management.
- **ADR-3: Extensible projection infrastructure** — Default projection with basic search included in v1. Extensibility contract (projection plugin interface) ships in v1. Advanced search plugins (Elasticsearch/OpenSearch) ship in v2. Consuming apps can build domain-specific read models from published events.
- **ADR-4: Type-discriminated contact channels** — Unified contact channel abstraction with type-specific structured payloads (postal: street/city/country, email: address, phone: number, social: platform/handle). Common fields (type, preferred, verified) plus discriminated payload. Extensible to future types without schema changes.
- **ADR-5: Stable client-generated UUIDs** — Party IDs are client-generated, immutable UUIDs. Never server-assigned sequential IDs. Ensures consuming apps can safely use party IDs as foreign keys without risk of ID change. For merge scenarios (v2), the `PartyMerged` event provides the old→new ID mapping.
- **ADR-6: Versioning and backward compatibility** — Consuming apps must be able to upgrade from MVP → v1.1 → v1.2 → v2 without breaking their integrations. The versioning strategy has three pillars: (1) **Event schema versioning** — events are append-only contracts; new fields are additive, never removing or renaming existing fields. If a breaking change is unavoidable, a new event type is introduced alongside the old one with a documented migration window. (2) **API versioning** — REST endpoints are versioned (e.g., `/api/v1/parties`); new versions coexist with previous versions during a deprecation period. (3) **NuGet package compatibility** — Hexalith.Parties.Contracts follows semantic versioning; minor versions add new commands/events without breaking existing consumers. Major version bumps (rare) include migration guides. The `PartyMerged` event shipping in MVP contracts (with v2 logic) is an example of this forward-compatibility approach.

### Scope: v1 vs Future

**v1 — Core:**

- Party aggregate (person + organization) with type-specific details, contact channels (type-discriminated), identifiers, and per-channel per-purpose consent
- Display name / sort name derivation with locale-aware formatting
- Command API (REST/gRPC) with idempotent command handling and typed rejection responses
- Event publishing via DAPR pub/sub (decrypted at publish time)
- Read projections with display-name search and filtering + projection extensibility contract for future email, identifier, semantic, and advanced search
- Multi-tenancy at all layers
- GDPR: crypto-shredding (events + snapshots), per-channel per-purpose consent, data portability export, right to restriction, processing records (Article 30), erasure verification, dangling reference guidance
- TypeScript party picker component (embeddable) + standalone admin views
- NuGet packages: Hexalith.Parties.Contracts (including forward-compatible `PartyMerged` event), Hexalith.Parties.Client (`IPartiesCommandClient`, `IPartiesQueryClient`, `AddPartiesClient()`)
- Client-generated stable UUIDs for party identity
- Validated at 100K parties per tenant. Performance beyond this threshold addressed by v2 Elasticsearch projection and horizontal scaling.

**v1 Known Limitations:**

- No duplicate detection — consuming apps must coordinate party creation or designate a "party master" application. Duplicate detection and merge ships in v2. Identifiers (VAT, SIRET) will serve as natural deduplication keys in v2.
- No email, identifier, semantic, fuzzy, or full-text search in the default v1 projection — v1 provides display-name search only. Dedicated search capabilities ship later.
- No party relationships — employment, hierarchies, legal representation deferred to v2.

**v2 — Roadmap:**

- Party relationships (employment, legal representation, organizational hierarchies)
- Party merge with `PartyMerged` event (consuming apps update foreign keys via old→new mapping)
- Cross-tenant party sharing with tenant-specific contact points
- Duplicate detection using identifier matching (VAT, SIRET) and fuzzy name matching
- Bulk import / batch command mechanism
- Fuzzy search via pluggable Elasticsearch projection
- Embeddable web component library (extracted from v1 composable components)
- Self-service portal for parties to manage their own data and consent
- Address validation / normalization extension points
- Horizontal scaling beyond 100K parties per tenant
- Possible aggregate split (PersonAggregate / OrganizationAggregate) if complexity warrants

---

## Target Users

### Primary Users

#### 1. Application Developer — "Marc, Full-Stack Developer"
**Profile:** Marc is a developer building a case management application. He needs to track parties involved in each case — clients, counterparties, legal representatives — but doesn't want to spend weeks building contact management and GDPR compliance from scratch. He works primarily in .NET but also builds services in TypeScript and Python.

**Problem Experience:** Every new project, Marc re-implements a contact model with addresses, phones, emails. Each time, GDPR compliance is incomplete or bolted on as an afterthought. Contact data ends up duplicated across applications with no single source of truth. He evaluates building his own vs. adopting a shared service — and the GDPR burden alone tips the decision.

**Success Vision:** Marc integrates Hexalith.Parties with a NuGet package or REST API call, gets GDPR compliance out of the box, subscribes to party events to build local read models in his domain context, and focuses on his business logic. Party management is a solved dependency.

**Key Interactions:**
- Discovers Hexalith.Parties on GitHub/NuGet while evaluating build-vs-adopt
- Integrates via `AddPartiesClient()` or REST/gRPC API (any language)
- Sends commands, subscribes to events, builds domain-specific read models
- Configures the MCP server for AI agent access in his applications
- Never needs to handle encryption or GDPR plumbing directly

#### 2. AI Agent — "Aria, Autonomous Workflow Agent"
**Profile:** Aria is an LLM-based AI agent operating within a user's workflow — processing emails, managing tasks, orchestrating business processes. Her core challenge is **identity resolution**: she encounters references to people and organizations in unstructured content and must determine whether they match an existing party or represent a new one.

**Problem Experience:** Aria processes an email from "J. Dupont at Acme Corp" and needs to determine: is this Jean Dupont the existing supplier, Jacques Dupont the client, or someone entirely new? Without a structured party registry, actors remain unstructured text with no linkage across workflows. Disambiguation is manual and error-prone.

**Success Vision:** Aria connects to Hexalith.Parties via MCP, searches for candidates by display name, retrieves candidate details when email or identifier evidence is needed, resolves ambiguities (asking the user to confirm when needed: "I found 3 matches for Dupont — which one?"), creates new parties from extracted data, and links party IDs to tasks and workflows. Identity resolution becomes a structured, reliable operation.

**Key Interactions:**
- Connects to Hexalith.Parties MCP server as a tool
- Searches and resolves parties by name, email, identifier (identity resolution)
- Disambiguates with the user when multiple candidates match
- Creates and updates parties from extracted data (emails, documents, conversations)
- Links stable party UUIDs to tasks, cases, and workflows in consuming applications

#### 3. End User via AI — "Sophie, Business Professional"
**Profile:** Sophie manages client relationships and supplier contacts daily. She works through her AI assistant (Claude, Copilot, etc.) which handles party data on her behalf via MCP. She never uses the Parties admin portal — her AI assistant is her interface.

**Problem Experience:** Sophie's contacts are scattered across email, spreadsheets, and various apps. When she needs a supplier's VAT number or a client's current address, she searches multiple systems. Updating a contact means changing it in several places. When she changes roles or projects, her contact context doesn't follow her.

**Success Vision:** Sophie tells her AI assistant "add this new supplier from the email I just received" or "what's the billing address for Acme Corp?" — the AI resolves it instantly from the shared party registry. When the AI finds ambiguities, it asks Sophie to confirm. Her contacts are always up to date, centralized, and GDPR-compliant — and they follow her across projects because they live in a shared service.

**Key Interactions:**
- Manages contacts through natural language requests to AI assistant
- Confirms disambiguation when the AI finds multiple matches ("Did you mean...?")
- AI assistant handles all operations via MCP transparently
- Benefits from centralized, always-current party data across all tools and projects

### Secondary Users

#### Administrator / DPO — "Laurent, Technical Operations & Data Protection"
**Profile:** Laurent is responsible for the operational health of the Hexalith ecosystem and serves as the Data Protection Officer contact point. He uses the Parties administration portal — the primary UI for this service — to manage party data quality, process GDPR requests, and investigate issues. Until v2 automates duplicate detection, Laurent manually identifies and flags near-duplicate records.

**Key Interactions:**
- Uses the web admin portal to browse, search, inspect, and correct party records
- Processes GDPR requests: right to erasure (crypto-shredding), data export, restriction of processing
- Manages consent records and verifies erasure completion across all projections
- Investigates data quality issues (duplicates, malformed records from AI-created parties)
- Escalation point when AI agents create incorrect or duplicate party records
- Monitors service health, tenant isolation, and encryption key rotation

### User Journey

| Phase | Developer (Marc) | AI Agent (Aria) | End User (Sophie) | Admin (Laurent) |
|---|---|---|---|---|
| **Discovery** | Finds on GitHub/NuGet while evaluating build-vs-adopt for party management | Configured as MCP tool by developer or discovered by AI assistant | Transparent — AI assistant handles it | Deployed as part of infrastructure |
| **Onboarding** | Adds NuGet package or calls REST API; configures MCP server | MCP server connection established; party search/create tools available | No onboarding needed — interacts via AI | Accesses admin portal, reviews data model |
| **Core Usage** | Sends commands, subscribes to events, builds domain read models | Resolves identities, disambiguates, creates/updates parties via MCP | "Add this contact", "Find supplier X" via natural language | Manages data quality, processes GDPR requests |
| **Aha! Moment** | "GDPR compliance and event-driven sync just work — I didn't build any of it" | Successfully resolves "J. Dupont" from an email to the correct existing party | "My AI already knows all my contacts across all projects" | Crypto-shredding erasure verified complete across all projections in one operation |
| **Long-term** | Party management is a solved dependency across all projects | Core identity resolution infrastructure for all AI-driven workflows | Single source of truth for all contacts, follows her across roles | Routine GDPR compliance, data quality oversight, duplicate management |

---

## Success Metrics

### North Star: Simplicity Drives Adoption

The single question that determines success: **"Is it faster to integrate Hexalith.Parties than to build my own party management?"** If the answer is yes for developers, and if AI agents become more capable with it than without it, adoption follows naturally.

### User Success Metrics

#### Developer Success (Marc)
- **Time to deploy:** A developer deploys a running Hexalith.Parties instance in under 15 minutes using .NET Aspire + Docker — on first attempt, without troubleshooting
- **Time to first command:** A developer sends their first successful `CreateParty` command within 30 minutes of starting integration, regardless of language (NuGet, REST, or gRPC)
- **Integration simplicity:** Consuming app integration requires a single NuGet package and one-line DI registration (`AddPartiesClient()`) — no DAPR knowledge needed on the client side
- **Comprehension speed:** A developer reads the getting-started guide and understands the command/query model well enough to write integration code without further documentation — the API is self-explanatory
- **Low dependency weight:** The Hexalith.Parties.Client NuGet package pulls minimal transitive dependencies — developers adopt party management, not an entire framework

#### AI Agent & End User Success (Aria & Sophie)
- **Single-prompt party creation:** A natural language sentence like "Add Jean Dupont from Acme Corp, email jean@acme.com" results in a complete party record with name, organization, and email channel — via one MCP tool call
- **Single-prompt modification:** Updating a contact channel or adding an identifier is achievable through one natural language request and one MCP tool call
- **MCP tool clarity:** MCP tools are well-named, well-documented, and produce predictable results — AI agents use them correctly without retry or confusion
- **Transparent interaction:** End users manage their contacts entirely through their AI assistant without needing to know Hexalith.Parties exists

#### Administrator Success (Laurent)
- **GDPR compliance:** Erasure, portability, restriction, and consent operations work correctly and completely — compliance is binary, it works or it doesn't
- **Erasure completeness:** Crypto-shredding propagates to all projections, caches, and search indexes with automated verification confirmation

### Business Objectives

#### Primary Objective: Adoption Through Simplicity
- **Developer adoption:** Developers choose Hexalith.Parties over building their own party management because integration is faster than reimplementation
- **AI ecosystem adoption:** AI agents and assistants use the MCP server as their standard party management tool because it makes them more capable
- **Developer funnel:** Discover (GitHub/NuGet) → Try (deploy locally) → Integrate (first command) → Stay (use in production)

#### Secondary Objective: Platform Validation
- **Prove Hexalith.EventStore viability:** Hexalith.Parties is the first domain microservice on EventStore — if Parties succeeds, EventStore is validated as a foundation for future domain services
- **Pattern replication:** Success is confirmed when the team builds a second domain service on EventStore, reusing the patterns established by Parties

### Key Performance Indicators

| KPI | Target | Measurement |
|---|---|---|
| Time to deploy (first attempt) | < 15 minutes, no troubleshooting | Tested with getting-started guide on clean machine |
| Time to first command | < 30 minutes (any language) | Measured from NuGet install or first REST call |
| MCP single-prompt creation | 1 sentence → 1 tool call → complete party | Tested across Claude, Copilot, and other AI assistants |
| MCP tool call success rate | Tools used correctly by AI agents without retries | Monitored via MCP server logs |
| Client package dependencies | Minimal transitive dependency footprint | NuGet dependency tree analysis |
| Consuming applications | ≥ 3 apps in first 12 months | Tracked through community and own projects |
| GDPR compliance | 100% — binary pass/fail | Automated test suite covering all GDPR operations |
| Second EventStore domain service | Built within 12 months of Parties v1 | Internal ecosystem tracking |
| Getting-started guide | Exists, tested, enables self-service onboarding | Developer testing with fresh eyes |

---

## MVP Scope

### Core Features

The MVP delivers the smallest version of Hexalith.Parties that creates real value: a party management microservice accessible via REST API and MCP server, with no frontend and no GDPR compliance features. Simplicity is the filter — if it doesn't contribute to "deploy in 15 minutes, first command in 30 minutes, single-prompt party creation via MCP," it's not MVP.

> **GDPR Notice:** MVP does not include GDPR compliance features (crypto-shredding, consent, erasure, portability, restriction). Do not store regulated EU personal data until v1.1 ships. MVP is intended for development, evaluation, and non-personal/test data only. A startup warning banner reminds operators of this limitation.

#### Party Aggregate
- **Party creation** — persons and organizations with type-specific details
- **PersonDetails** — first name, last name, date of birth, name prefix/suffix
- **OrganizationDetails** — legal name, trading name, legal form, registration number
- **Display name / sort name derivation** — simple concatenation rules (locale-aware formatting deferred to v1.1)
- **Contact channels** — unified type-discriminated abstraction (postal, email, phone, social) with add, update, remove, set preferred
- **Identifiers** — VAT, SIRET, national ID, and other jurisdiction-specific references with add and remove
- **Party deactivation / reactivation** — soft lifecycle management
- **Client-generated stable UUIDs** for party identity
- **`[PersonalData]` field attributes** — zero-cost architectural preparation marking which aggregate fields contain personal data, enabling crypto-shredding activation in v1.1 without code changes

#### REST API
- **Command API** — create, update, and manage parties via REST
- **Read projection** — list parties (paginated), display-name search (exact/prefix/contains), filter by type (person/organization). Email, identifier, and semantic search require the dedicated search capability.
- **Typed rejection responses** via `DomainResult`
- gRPC included only if provided automatically by EventStore with no additional deployment setup

#### MCP Server (5 tools, optimized for AI ergonomics)
- **`search_parties`** — search by display name for MVP identity resolution; use `get_party` on candidates when email or identifier evidence must be inspected
- **`get_party`** — retrieve full party details by ID
- **`create_party`** — composite creation: name + type details + contact channels + identifiers in a single tool call (not individual commands)
- **`update_party`** — update details, add/modify/remove contact channels and identifiers
- **`list_parties`** — paginated party listing with basic filters

> MCP tools are designed for AI agent consumption, not as 1:1 mirrors of the command API. A single `create_party` call with name, email, and organization creates a complete party record — achieving the "single-prompt creation" KPI.

#### NuGet Packages
- **Hexalith.Parties.Contracts** — shared command, event, and query types (includes `PartyMerged` event contract for forward compatibility)
- **Hexalith.Parties.Client** — client abstractions (`IPartiesCommandClient`, `IPartiesQueryClient`) with one-line DI registration (`AddPartiesClient()`)

#### Included via Hexalith.EventStore (zero additional effort)
- **Authentication & authorization** — JWT-based authentication with tenant identity extracted from token claims. Hexalith.Parties inherits EventStore's auth middleware; no custom auth implementation needed. The framework handles JWT validation, claims extraction, and tenant context propagation through the MediatR pipeline
- **Multi-tenancy** at all layers (aggregate, event store, projections, API) — tenant filtering is automatic and framework-enforced, not per-query manual code
- **Event publishing** via DAPR pub/sub for consuming app subscriptions
- **Idempotent command handling** for safe deduplication in distributed scenarios
- **Convention-based aggregate discovery** with MediatR pipeline and FluentValidation
- **Snapshot support** for Party aggregate rehydration performance

#### MVP Documentation Deliverables
- **README** with clear value proposition in first paragraph
- **Getting-started guide** tested by someone other than the author, with explicit prerequisites
- **Sample integration** showing REST API + MCP usage
- **GDPR disclaimer** in README, getting-started guide, and service startup banner

### Out of Scope for MVP

| Feature | Rationale | Phase |
|---|---|---|
| **All GDPR compliance** | Crypto-shredding, consent, portability, restriction, erasure, Article 30 — deferred to validate core model first. `[PersonalData]` attributes ship in MVP as zero-cost preparation | v1.1 — GDPR |
| **TypeScript admin portal** | MVP is API + MCP only. Admin views ship after core model is validated | v1.2 — Frontend |
| **Party picker component** | Embeddable widget deferred with frontend | v1.2 — Frontend |
| **Locale-aware name formatting** | Simple concatenation in MVP. i18n name ordering and honorifics deferred | v1.1 |
| **Advanced search (Elasticsearch)** | Default projection search is display-name-only for MVP. Email, identifier, semantic, fuzzy, and full-text search require the dedicated search capability | v2 |
| **Duplicate detection & merge** | Complex feature — MVP apps coordinate party creation manually | v2 |
| **Party relationships** | Employment, hierarchies, legal representation deferred | v2 |
| **Cross-tenant party sharing** | Multi-tenant isolation sufficient for MVP | v2 |
| **Bulk import / batch commands** | Individual commands sufficient for MVP scale | v2 |
| **Self-service portal** | Depends on frontend and consent management | v2 |

### MVP Success Criteria

The MVP is successful when these gates are passed:

| Gate | Criteria | Validation Method |
|---|---|---|
| **Deploy gate** | A developer deploys Hexalith.Parties in < 15 minutes on first attempt | Tested with getting-started guide on a clean machine by someone who didn't write the guide |
| **Integration gate** | A developer sends first `CreateParty` command in < 30 minutes | Timed walkthrough with NuGet package and REST |
| **MCP gate** | "Add Jean Dupont from Acme Corp, email jean@acme.com" creates a complete party via one `create_party` MCP tool call | Tested with Claude and other AI assistants |
| **Resolution gate** | AI agent searches for "Dupont" and correctly identifies existing party from candidates | MCP `search_parties` tool returns ranked results |
| **EventStore validation gate** | The Party aggregate follows EventStore conventions without workarounds or hacks | Code review — no EventStore bypasses |
| **Documentation gate** | Getting-started guide enables self-service onboarding without author assistance | Tested by fresh developer |
| **Go/no-go decision** | All gates pass → proceed to v1.1 (GDPR). Deploy or integration gates fail → simplify before adding features | Team review after MVP |

### MVP Known Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Developer stores real EU personal data in MVP | No erasure mechanism — compliance exposure | Prominent GDPR disclaimer + startup warning banner + documented emergency manual erasure procedure |
| MCP tool design takes longer than expected | Delays entire MVP | REST API is hard gate; MCP can ship as fast-follow if needed |
| EventStore bugs discovered during Parties development | Development time splits between Parties and EventStore fixes | Expected and valuable — this IS platform validation. Budget for it |
| Party aggregate grows large without snapshot tuning | Slow rehydration for parties with many contact channels | Confirm EventStore snapshot strategy works for Party aggregate early in development |

### Future Vision

#### Phase Roadmap

**v1.1 — GDPR Compliance:**
- Activate crypto-shredding via `[PersonalData]` attributes already in place (per-party keys, DAPR secret store)
- Per-channel per-purpose consent management
- Data portability export, right to restriction, erasure verification
- Processing records (Article 30)
- Dangling reference guidance for consuming apps
- Locale-aware name formatting

**v1.2 — Frontend:**
- TypeScript admin portal (browse, search, inspect, GDPR operations)
- Embeddable party picker component for consuming app UIs
- Composable component architecture (PartyList, PartyDetail, PartyForm, ContactChannelEditor, ConsentManager)

**v2 — Scale & Intelligence:**
- Duplicate detection using identifier matching and fuzzy name matching
- Party merge with `PartyMerged` event and old→new ID mapping
- Advanced search via pluggable Elasticsearch/OpenSearch projection
- Party relationships (employment, hierarchies, legal representation)
- Cross-tenant party sharing
- Bulk import / batch commands
- Self-service portal for parties to manage their own data
- Address validation / normalization extension points
- Horizontal scaling beyond 100K parties per tenant

#### Long-term Vision (2-3 years)
If Hexalith.Parties succeeds, it becomes the **standard open-source party management building block** for any application needing to identify and manage third-party entities — the equivalent of what authentication libraries (Identity, Auth0) did for user management, but for external parties. Its success validates Hexalith.EventStore as the foundation for an ecosystem of domain microservices, each solving a common cross-cutting concern (parties, documents, workflows, notifications) once and for all.
