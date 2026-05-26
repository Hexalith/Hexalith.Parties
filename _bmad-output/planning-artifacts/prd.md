---
stepsCompleted: ['step-01-init', 'step-02-discovery', 'step-02b-vision', 'step-02c-executive-summary', 'step-03-success', 'step-04-journeys', 'step-05-domain', 'step-06-innovation', 'step-07-project-type', 'step-08-scoping', 'step-09-functional', 'step-10-nonfunctional', 'step-11-polish', 'step-12-complete']
inputDocuments: ['product-brief-Hexalith.Parties-2026-03-01.md']
documentCounts:
  briefs: 1
  research: 0
  brainstorming: 0
  projectDocs: 0
workflowType: 'prd'
classification:
  projectType: 'Party Management Domain Microservice'
  domain: 'Party Management (Identity & Contact Management)'
  complexity: 'High'
  projectContext: 'Greenfield'
  dimensionalDetail:
    domain: 'Medium-High — Novel unified contact channel model, type-discriminated payloads, consent-per-channel-per-purpose'
    regulatory: 'High — GDPR: crypto-shredding, consent, erasure, portability, restriction, Article 30'
    technical: 'High — Event sourcing, CQRS, DAPR, multi-tenancy, field-level encryption, snapshot strategy'
    strategic: 'High — First EventStore domain service, reference implementation quality bar'
  designConstraints:
    - 'AI-native: MCP server is a primary consumption interface, not an add-on'
    - 'Package-first DX: NuGet client packages are the primary developer entry point'
    - 'Platform exemplar: Quality bar elevated beyond typical MVP'
  internalStrategicGoal: 'Validate Hexalith.EventStore programming model and operational patterns'
---

# Product Requirements Document - Hexalith.Parties

**Author:** Jérôme
**Date:** 2026-03-01

## Executive Summary

Hexalith.Parties is an open-source (MIT-licensed), event-sourced domain microservice that serves as the single source of truth for party management — persons and organizations — across all applications in the Hexalith ecosystem. A microservice (not an embedded library) because the core value proposition requires shared state: multiple applications referencing the same party data, synchronized through domain events. It solves a problem every application team encounters: rebuilding contact management, GDPR compliance, and identity resolution from scratch, resulting in duplicated data, fragmented regulatory obligations, and AI agents unable to access structured party information.

**One party, one truth, every consumer.** The same party data is accessible to developers via NuGet client packages, to AI agents via a 5-tool MCP server, and to operators via administration views — always in sync through domain events. Built on Hexalith.EventStore, a DAPR-native CQRS/Event Sourcing framework for .NET, the Party aggregate manages identity, type-specific details (person or organization), contact channels (a unified type-discriminated abstraction covering postal, email, phone, and social), identifiers (VAT, SIRET, national ID), and per-channel per-purpose consent records. Business-specific roles (customer, supplier) are explicitly excluded — these belong to consuming applications.

The service targets four audiences equally: **developers** who integrate party management as a solved dependency in minutes rather than weeks; **AI agents** that perform structured identity resolution — turning "J. Dupont at Acme Corp" into a disambiguated, linkable entity; **end users** whose AI assistants manage contacts seamlessly across projects; and **administrators/DPOs** who handle GDPR compliance (crypto-shredding, consent, erasure, portability, restriction) from a single service rather than across fragmented systems. Note: Hexalith.Parties manages external third-party entities (customers, suppliers, contacts), not authenticated users — it complements identity platforms (Auth0, Clerk, WorkOS), not competes with them.

Hexalith.Parties is also the first domain microservice built on the Hexalith.EventStore platform, validating the programming model, infrastructure abstractions, and operational patterns for all future domain services in the ecosystem.

### What Makes This Special

**Enterprise model, open delivery.** Hexalith.Parties delivers the conceptual richness of D365 Global Address Book, SAP Business Partner, and Oracle Trading Community Architecture as an open-source microservice — the right abstractions from day one, without ERP licensing or vendor lock-in. The architecture grows into enterprise-grade without rearchitecting.

**AI-native from day one.** The MCP server is a first-class consumption interface, not an afterthought. Five tools optimized for AI agent ergonomics enable single-prompt party creation, structured identity resolution, and disambiguation — capabilities no existing open-source party management solution provides. MCP adoption is the timing catalyst that makes this product uniquely relevant now.

**Package-first developer experience.** Consuming applications integrate via `Hexalith.Parties.Client` with a one-line DI registration (`AddPartiesClient()`). Developers don't need DAPR, don't handle encryption, and don't implement GDPR plumbing. Deploy the service in 15 minutes, send the first command in 30.

**GDPR by design.** Crypto-shredding with per-party keys, per-channel per-purpose consent management, data portability export, right to restriction, erasure verification across all projections, and processing records (Article 30). Compliance is structural, not bolted on — it works completely or not at all.

**Event-driven single source of truth.** Domain events propagate changes to all consumers in real time. Consuming applications subscribe and build domain-specific read models, supporting offline/disconnected scenarios. The event stream provides a complete, time-stamped record of all party data processing activities.

## Project Classification

- **Project Type:** Party Management Domain Microservice (multi-interface: REST/gRPC API, NuGet client packages, MCP server, domain event stream)
- **Domain:** Party Management — Identity & Contact Management
- **Complexity:** High
  - Domain: Medium-High — Novel unified contact channel model with type-discriminated payloads, consent-per-channel-per-purpose
  - Regulatory: High — GDPR crypto-shredding, consent, erasure, portability, restriction, Article 30
  - Technical: High — Event sourcing, CQRS, DAPR, multi-tenancy, field-level encryption
  - Strategic: High — First EventStore domain service, reference implementation quality bar
- **Project Context:** Greenfield
- **Design Constraints:** AI-native (MCP as first-class interface), Package-first DX, Platform exemplar

## Success Criteria

### User Success

**Developer Success (Marc)**
- **Time to deploy:** Deploy a running instance in under 15 minutes using .NET Aspire + Docker on first attempt — given documented prerequisites met, tested on a clean machine with no Docker cache
- **Time to first command:** First successful `CreateParty` command within 30 minutes of starting integration, regardless of language (NuGet, REST, or gRPC)
- **Comprehension check:** After first command, developer can explain the command → event → projection flow — understanding, not just execution
- **Integration simplicity:** Single NuGet package, one-line DI registration (`AddPartiesClient()`), no DAPR knowledge needed on the client side
- **Low dependency weight:** Client package dependency count reviewed and justified; no unnecessary transitive dependencies
- **Error experience quality:** `DomainResult` rejection messages are self-explanatory — developers understand what went wrong without debugging the service
- **README quality:** A developer reads the first paragraph and can explain what Hexalith.Parties does in one sentence

**AI Agent & End User Success (Aria & Sophie)**
- **Single-prompt party creation:** "Add Jean Dupont from Acme Corp, email jean@acme.com" creates a complete party via one MCP `create_party` tool call. Response returns the complete created party, not just the ID
- **Single-prompt modification:** Updating a contact channel or adding an identifier achievable through one MCP tool call
- **MCP tool clarity:** Tools well-named, well-documented, produce predictable results — AI agents use them correctly without retry or confusion
- **Forgiving input schemas:** MCP tools accept partial input gracefully; missing optional fields default sensibly; validation errors clearly state what's needed
- **Search result quality:** `find_parties` results include display-name match metadata (matched field, match type) sufficient for AI agents to rank candidates in simple name-based cases. Email, identifier, and semantic search are deferred to the dedicated search capability.
- **Transparent interaction:** End users manage contacts entirely through their AI assistant without needing to know Hexalith.Parties exists

**Administrator Success (Laurent)**
- **GDPR correctness:** All GDPR operations function correctly as designed, validated by automated tests. Legal compliance review is a separate, non-automated activity
- **Erasure completeness:** Crypto-shredding propagates to all projections, caches, and search indexes with automated verification confirmation
- **GDPR operability (v1.1):** A DPO completes a full erasure request (trigger, verify, confirm) within 15 minutes using the admin portal
- **GDPR discoverability (v1.1):** Admin dashboard provides pending requests, consent status overview, and erasure audit trail
- **Audit report speed (v1.1):** Erasure verification report itemizes all projection cleanup results, produced automatically within 5 minutes of erasure trigger

### Business Success

**Primary Objective: Adoption Through Simplicity**
- Developers choose Hexalith.Parties over building their own because integration is faster than reimplementation
- AI agents and assistants use the MCP server as their standard party management tool because it makes them more capable
- Developer funnel: Discover (GitHub/NuGet) → Try (deploy locally) → Integrate (first command) → Stay (use in production)
- Target: ≥ 3 consuming applications within 12 months, **with at least 1 external** (built by non-core-team for their own use case, not a demo), **at least 1 demonstrating full event lifecycle handling** (including `PartyErased` processing), and **at least 1 with > 1,000 parties** in a production-like context validating real-world scale
- Retention signal: at least 1 developer uses the service in a production context within 60 days of first integration
- MVP adoption: primarily non-EU use cases and evaluation; EU production adoption targets begin at v1.1 (GDPR)
- Community trajectory: GitHub stars, forks, NuGet downloads tracked monthly as leading indicators. If flat after 6 months, investigate and course-correct (improve docs, blog posts, meetup presentations)

**Secondary Objective: Platform Validation**
- Hexalith.Parties validates Hexalith.EventStore as a viable foundation for domain microservices
- Success confirmed when a second domain service is built on EventStore, reusing ≥ 80% of Parties infrastructure patterns (aggregate discovery, projection setup, API scaffolding) without modification, within 12 months
- **Project health signal:** Track development time split (Parties domain logic vs. EventStore infrastructure work). Not a KPI — a retrospective signal
- **Escalation trigger:** If EventStore infrastructure work exceeds 60% of total effort for 2 consecutive sprints, conduct a go/no-go review on the current approach. EventStore bugs discovered during Parties development are expected and budgeted — they extend the timeline, not the scope

**Contingency: MCP Adoption Stalls**
- If MCP adoption stalls industry-wide, REST API + NuGet packages carry the primary adoption story. MCP remains available but de-emphasized in marketing. The success criteria pivot to developer integration speed as the primary differentiator.
- **Decision point:** Evaluated at 6-month post-MVP review. If zero external MCP integrations and industry MCP adoption metrics are flat, pivot marketing to REST-first positioning.

### Technical Success

- **EventStore convention compliance:** Party aggregate follows EventStore programming model (pure `Handle`/`Apply` functions, convention-based discovery) without workarounds or framework bypasses
- **Test coverage:** Three-tier test strategy for fault isolation: (1) pure domain unit tests (aggregate logic, no EventStore dependency), (2) EventStore integration tests (generic aggregates, validating framework behavior), (3) full-stack Parties integration tests (API endpoints, MCP tools, projections). This separation enables fast diagnosis of whether failures are domain bugs, framework bugs, or integration issues
- **Performance targets (monitoring, not launch blockers):** Party lookup by ID < 200ms, search by name < 500ms at target scale of 100K parties per tenant
- **Eventual consistency:** Created party visible in read projection within < 2 seconds under normal load
- **Event schema integrity:** Events are append-only contracts; schema evolution follows additive-only rules with no breaking changes
- **Multi-tenancy isolation:** Zero cross-tenant data leakage verified at aggregate, event store, projection, and API layers — tested with ≥ 10 concurrent tenants under load, not just sequential two-tenant checks
- **Snapshot strategy validated:** Party aggregate rehydration performance acceptable for parties with high contact channel counts
- **Event durability:** Zero event loss in crash recovery scenarios — verified through fault injection tests

### Measurable Outcomes

**Primary KPIs (tracked actively, reported monthly):**

| KPI | Target | Measurement |
|---|---|---|
| Time to first command | < 30 minutes (any language) | Timed walkthrough on clean machine by non-author |
| MCP single-prompt creation | 1 sentence → 1 tool call → complete party returned | Tested across Claude, Copilot, and other assistants |
| Consuming applications | ≥ 3 in 12 months (≥ 1 external, ≥ 1 full lifecycle) | Tracked through community and own projects |
| Cross-tenant isolation | Zero leakage under concurrent multi-tenant load | Isolation test suite with ≥ 10 tenants at NFR17 throughput (100 reads/sec, 20 writes/sec per tenant) |
| GDPR operations (from v1.1) | All operations correct as designed | Automated test suite |

**Supporting Metrics (tracked passively, reviewed quarterly):**

| Metric | Target | Measurement |
|---|---|---|
| Time to deploy | < 15 min, prerequisites met, no Docker cache | Clean machine test |
| Developer retention | ≥ 1 production use within 60 days | Follow-up tracking |
| Search result quality | Match metadata present, AI agents rank confidently | Structured resolution scenarios |
| Response time — lookup | < 200ms (party by ID) | Load tested at target scale |
| Response time — search | < 500ms (search by name) | Load tested at target scale |
| Eventual consistency | Created party in projection < 2s | Measured under normal load |
| Event durability | Zero loss in crash recovery | Fault injection tests |
| Client package dependencies | Reviewed, justified, no unnecessary deps | NuGet dependency tree analysis |
| Community trajectory | GitHub stars, forks, NuGet downloads trending up | Monthly tracking |
| Second EventStore service | Built within 12 months, ≥ 80% pattern reuse | Internal tracking |

## Product Scope

### MVP — Minimum Viable Product

The smallest version that creates real value: a party management microservice accessible via REST API and MCP server, with no frontend and no GDPR compliance features. If it doesn't contribute to "deploy in 15 minutes, first command in 30 minutes, single-prompt party creation via MCP," it's not MVP.

> **GDPR Notice:** MVP does not include GDPR compliance features. Do not store regulated EU personal data until v1.1. MVP is for development, evaluation, and non-personal/test data only. Startup warning banner is **non-dismissable** — it persists in the admin UI header and API response headers until v1.1 GDPR features are activated.

> **Duplicate Notice:** MVP does not include duplicate detection. AI agents creating parties from extracted data (emails, documents) will likely create near-duplicates. `find_parties` display-name match metadata provides advisory signals only; consuming apps and operators are responsible for deduplication until v2. Consider adding near-match advisory warnings in `create_party` responses as a v1.1 enhancement.

**Included:**
- Party aggregate: persons and organizations with type-specific details, contact channels (type-discriminated), identifiers, deactivation/reactivation, client-generated stable UUIDs
- `[PersonalData]` field attributes as zero-cost preparation for v1.1 crypto-shredding
- Display name / sort name derivation (simple concatenation; locale-aware deferred to v1.1)
- REST command API with typed, self-explanatory `DomainResult` rejection responses
- Read projection: paginated list, display-name search with match metadata, filter by type; eventual consistency < 2 seconds (email, identifier, and semantic search deferred to the dedicated search capability)
- MCP server: 5 tools (`find_parties`, `get_party`, `create_party`, `update_party`, `delete_party`); `find_parties` covers search and list modes; `delete_party` maps to soft deactivation, not GDPR erasure; `create_party` returns complete party; forgiving input schemas with sensible defaults and clear validation errors
- NuGet packages: Hexalith.Parties.Contracts (including forward-compatible `PartyMerged` event), Hexalith.Parties.Client (`AddPartiesClient()`)
- Via EventStore (zero additional effort): JWT auth, multi-tenancy, event publishing, idempotent commands, convention-based discovery, snapshot support
- Documentation: README (value proposition in first paragraph, testable clarity), getting-started guide (realistic scenario, tested on clean machine by non-author, **includes non-.NET developer path: Docker deploy + REST API**), sample integration, GDPR disclaimer, **emergency manual erasure procedure** (documented steps for handling erasure requests pre-v1.1 — scope, limitations, documents destructive nature and event stream integrity trade-offs)

**MVP Success Gates:**

| Gate | Type | Criteria | Validation |
|---|---|---|---|
| Deploy gate | **Hard** | Deploy in < 15 min, prerequisites met, no Docker cache | Clean machine, non-author tester |
| Integration gate | **Hard** | First `CreateParty` in < 30 min; developer explains command → event → projection flow | Timed walkthrough + comprehension check |
| MCP gate | **Hard** | Single-prompt creates complete party, full party returned in response | Tested with Claude and other assistants |
| Documentation gate | **Hard** | Guide enables self-service onboarding; README passes one-sentence clarity test | Tested by fresh developer |
| Resolution gate | Soft | AI agent resolves "Dupont" to correct party from candidates | MCP `find_parties` with match metadata |
| EventStore validation gate | Soft | Aggregate follows conventions, no workarounds | Code review |

All hard gates must pass to proceed to v1.1. Soft gates inform but don't block.

### Growth Features (Post-MVP)

**v1.1 — GDPR Compliance:**
- Activate crypto-shredding via `[PersonalData]` attributes (per-party keys, DAPR secret store)
- Per-channel per-purpose consent management
- Data portability export, right to restriction, erasure verification
- Processing records (Article 30), dangling reference guidance
- Locale-aware name formatting
- Semantic search via pluggable projection (deferred from MVP)
- Temporal name query API (FR72 — deferred from MVP; event stream history preserved at MVP)
- GDPR admin dashboard: pending requests, consent overview, erasure audit trail
- DPO operability: full erasure request completed within 15 minutes
- Remove non-dismissable GDPR warning banner
- Operational observability: alerting, monitoring dashboards, and runbooks required for production EU data deployments

**v1.2 — Frontend:**
- FrontComposer-based Blazor/Razor admin portal (browse, search, inspect, GDPR operations), consuming the EventStore-fronted Parties client/query/command boundary
- Embeddable party picker component for consuming app UIs
- Composable component architecture (PartyList, PartyDetail, PartyForm, ContactChannelEditor, ConsentManager)

### Vision (Future)

**v2 — Scale & Intelligence:**
- Duplicate detection (identifier matching + fuzzy name matching) and party merge with `PartyMerged` event
- Advanced search via pluggable Elasticsearch/OpenSearch projection
- Party relationships (employment, hierarchies, legal representation)
- Cross-tenant party sharing with tenant-specific contact points
- Bulk import / batch commands
- Self-service portal for parties to manage their own data and consent
- Address validation / normalization extension points
- Horizontal scaling beyond 100K parties per tenant

**Long-term (2-3 years):** Hexalith.Parties becomes the standard open-source party management building block — the equivalent of what authentication libraries did for user management, but for external parties. Its success validates Hexalith.EventStore as the foundation for an ecosystem of domain microservices.

## User Journeys

The following journeys illustrate how each target audience experiences Hexalith.Parties — from discovery through daily use. Each journey traces back to the success criteria above and maps forward to the functional requirements that follow.

### Journey 1: Marc Integrates Party Management — Developer Success Path

**Opening Scene:** Marc is two weeks into building a case management application for a law firm. He's just finished the authentication layer and is staring at his next task: "Implement contact management for case parties — clients, counterparties, witnesses, legal representatives." He's done this three times before. Last time, it took four weeks, and the GDPR implementation was incomplete. He opens a browser and searches "party management .NET open source."

**Rising Action:** Marc finds Hexalith.Parties on NuGet. He reads the README — first paragraph tells him exactly what it does. He clones the repo, runs `dotnet aspire run`, and has a running instance in 12 minutes. He opens the getting-started guide, follows the realistic scenario (create a person, add an email, search by name), and sends his first `CreateParty` command via the REST API. It works. The `DomainResult` comes back with the complete party. He checks the read projection — the party appears in search within a second.

He adds `Hexalith.Parties.Client` to his case management project. One line: `builder.Services.AddPartiesClient(config)`. He injects `IPartiesCommandClient`, sends a `CreateParty` for his test case, and it works. No DAPR setup. No encryption configuration. No GDPR plumbing. He writes his first integration test — create a party, query it back, assert the fields match.

**Climax:** Marc's colleague asks "How are you handling GDPR for the contact data?" Marc says: "It's handled. Hexalith.Parties does crypto-shredding, consent management, and erasure verification. I just send commands and subscribe to events." His colleague doesn't believe him. Marc shows the Contracts package — the `PartyErased` event, the consent commands, the `[PersonalData]` attributes. "When v1.1 ships, it activates automatically. I don't touch any of it."

**Resolution:** Marc's case management app goes to production. Party management took 2 days of integration instead of 4 weeks of building. When the law firm's DPO asks about GDPR, Marc points them to the Hexalith.Parties admin portal. Contact data is centralized, consistent, and compliant. Marc adds Hexalith.Parties to his standard stack for future projects. It's a solved dependency.

**Requirements revealed:** NuGet package integration, REST API, getting-started guide, `DomainResult` error clarity, read projection search, one-line DI registration, client abstractions.

---

### Journey 2: Aria Resolves an Identity — AI Agent Success Path

**Opening Scene:** Aria is an AI agent processing Sophie's morning emails. An email arrives from "J. Dupont at Acme Corp" about an invoice dispute. Aria needs to link this email to the correct party in Sophie's workflow system. But who is "J. Dupont"? There are three Duponts in the system.

**Rising Action:** Aria calls `find_parties` via MCP with the query "Dupont." The results come back with display-name match metadata — Jean Dupont (display-name prefix match), Jacques Dupont (display-name prefix match), and Julie Dupont (display-name prefix match, inactive). Aria calls `get_party` on the likely candidates to inspect contact channels and identifiers before linking the email.

Aria is 95% confident this is Jean Dupont. She links the email to his party ID in Sophie's task system. She drafts a response: "I've linked the invoice dispute email from Jean Dupont at Acme Corp to case #427. His billing address is 42 Rue de Rivoli, Paris."

**Climax:** A second email arrives — this one from "M. Bernard, new supplier." No existing party matches. Aria calls `create_party` with the information extracted from the email: person, first name "M." (incomplete), last name "Bernard", organization "Unknown", email "m.bernard@newcorp.fr". The tool accepts partial input gracefully — first name is stored as "M.", organization is omitted since it's uncertain. The complete party is returned in the response. Aria tells Sophie: "I've created a new contact for M. Bernard (m.bernard@newcorp.fr). I only have a partial first name — would you like to fill in the details?"

**Resolution:** Over the course of a week, Aria processes 50 emails, resolves 43 to existing parties autonomously (match metadata was sufficient), asks Sophie to disambiguate 5, and creates 2 new parties from extracted data. Sophie's contact registry grows organically, always structured, always up to date. Identity resolution is no longer a manual, error-prone task — it's a reliable, structured operation.

**Requirements revealed:** MCP `find_parties` with match metadata, `create_party` with forgiving input schemas, `get_party` for full details, partial input handling, complete party in create response.

---

### Journey 3: Sophie Manages Contacts Through Her AI — End User Happy Path

**Opening Scene:** Sophie is preparing for a client meeting. She needs the billing address for Acme Corp and the mobile number for her contact there, Jean Dupont. Three months ago, she'd have searched Outlook, checked a shared spreadsheet, and maybe texted a colleague. Today, she asks her AI assistant.

**Rising Action:** "What's the billing address for Acme Corp?" Her AI assistant calls `find_parties` for "Acme Corp", finds the organization, calls `get_party` for full details, and responds: "Acme Corp's billing address is 42 Rue de Rivoli, 75001 Paris. Their primary contact is Jean Dupont — would you like his details too?" Sophie says yes. The AI retrieves Jean's party record: mobile +33 6 12 34 56 78, email jean.dupont@acme.com.

After the meeting, Sophie says: "Jean mentioned they moved offices. Update Acme Corp's address to 15 Avenue des Champs-Élysées, 75008 Paris." The AI calls `update_party` to update the postal contact channel. Done in seconds.

**Climax:** Sophie switches to a new project next quarter. She's worried about losing her contact context. But her contacts live in Hexalith.Parties, not in her email or her old project's database. Her AI assistant connects to the same party registry. Every contact she built over the past year is still there, still current, still searchable. "Find all the suppliers I added last quarter" — the AI queries and lists them.

**Resolution:** Sophie never thinks about contact management. She never visits an admin portal. She never worries about whether an address is current. Her AI assistant handles everything, backed by a service she doesn't know exists. Her contacts follow her across projects, across roles, across tools. When a colleague asks "Do you have the VAT number for Bernard Corp?" Sophie just asks her AI and forwards the answer.

**Requirements revealed:** MCP tools designed for natural language interaction patterns, fast lookup response times, update operations via MCP, party data persistence across user contexts.

---

### Journey 4: Laurent Handles a GDPR Erasure Request — Admin/DPO Path

**Opening Scene:** Laurent receives a formal right-to-erasure request from Jean Dupont via the company's GDPR inbox. Jean wants all his personal data deleted. Laurent has 30 days to comply, but he wants to process it today. He logs into the Hexalith.Parties admin portal.

**Rising Action:** Laurent searches for "Jean Dupont" in the admin portal. He finds the party record and reviews it: 2 email addresses, 1 phone number, 1 postal address, 3 active consent records, and he's referenced by 2 consuming applications (the case management system and the invoice system). Laurent first checks the consent records — marketing email consent was given 6 months ago, processing consent for the case management system, and billing consent for invoicing.

Laurent clicks "Restrict Processing" first — freezing Jean's data while he coordinates with the consuming application teams. He sends them a heads-up: "Jean Dupont (party ID: abc-123) has requested erasure. You have 48 hours to archive or nullify your references before I trigger crypto-shredding."

**Climax:** Two days later, both teams confirm they've handled their references. Laurent returns to the admin portal and triggers "Request Erasure." The system destroys Jean's per-party encryption key via DAPR secret store. All personal data in events and snapshots becomes unreadable. The `PartyErased` event fires to all subscribers. The erasure verification job runs automatically — within 5 minutes, Laurent sees the report: all projections cleaned, search indexes purged, cache invalidated. Every line item shows green. Complete erasure, cryptographically guaranteed.

**Resolution:** Laurent replies to Jean's erasure request with confidence: "Your personal data has been erased from our systems. Erasure has been verified across all data stores and consuming applications. A record of this processing activity has been retained as required by Article 30." The entire process took 15 minutes of Laurent's active time, spread across 3 days. No manual database queries. No hoping he found every copy. One service, one operation, complete compliance.

**Requirements revealed:** Admin portal search and browse, party detail view with all contact channels and consent records, restriction workflow, erasure trigger with crypto-shredding, erasure verification report, `PartyErased` event propagation, Article 30 processing records. *(Note: Full GDPR journey activates at v1.1; admin portal at v1.2.)*

---

### Journey 5: Clara Subscribes to Party Events — Event Integration Developer Path

**Opening Scene:** Clara is a backend developer on the invoice management team. Her system tracks which parties are customers with outstanding invoices. She stores `partyId` as a foreign key in her invoice records. She needs her local customer cache to stay in sync with Hexalith.Parties — and she needs to handle the scenario where a party is erased.

**Rising Action:** Clara adds `Hexalith.Parties.Contracts` to her project and subscribes to party events via DAPR pub/sub. She implements three event handlers:

First, `PartyCreated` — she doesn't need all parties, only those her system cares about. She ignores this event and lets the invoice team create customer associations explicitly.

Second, `PersonDetailsUpdated` and `OrganizationDetailsUpdated` — when a party's name changes, her invoice system's customer display name needs to update too. She implements a handler that updates her local `CustomerReadModel` whenever the name changes for a party ID she tracks.

Third — the critical one — `PartyErased`. Clara follows the dangling reference guidance in the Contracts package documentation. She implements a handler that finds all invoices referencing the erased `partyId`, nullifies the party reference, and replaces the customer display name with "[Erased Party]". She preserves the invoice records (they have independent legal retention requirements) but removes all linkage to the erased party.

**Climax:** Three months in, the first real erasure happens. Laurent triggers crypto-shredding for a party referenced in 4 of Clara's invoices. The `PartyErased` event arrives. Clara's handler executes: 4 invoice records updated, party reference nullified, display name replaced. Her integration test for this exact scenario — which she wrote on day one — passes in production exactly as it did in dev. No data leak. No dangling reference. No manual intervention.

**Resolution:** Clara's event subscription runs for months without issues. She adds handlers for `ContactChannelUpdated` (to keep billing addresses in sync) and `PartyDeactivated` (to flag customers for review). Her local read model stays consistent with the party registry automatically. When the team builds a second service (procurement), they copy Clara's event handler patterns wholesale. The subscription model is proven, documented, and reusable.

**Requirements revealed:** Event publishing via DAPR pub/sub, Contracts package with event types, dangling reference guidance documentation, `PartyErased` event with clear handler patterns, event handlers for domain-specific read model construction, decrypted events at publish time.

---

### Journey Requirements Summary

| Journey | Primary Capabilities Required | MVP / Post-MVP |
|---|---|---|
| **Marc — Developer Integration** | REST API, NuGet packages, getting-started guide, `DomainResult` errors, read projection search, one-line DI | MVP |
| **Aria — AI Identity Resolution** | MCP 5 tools, match metadata in search, forgiving input schemas, complete party in create response | MVP |
| **Sophie — End User via AI** | MCP tools, fast response times (< 200ms lookup, < 500ms search), update via MCP | MVP |
| **Laurent — GDPR Erasure** | Admin portal, restriction workflow, crypto-shredding, erasure verification, consent management, Article 30 records | v1.1 (GDPR) + v1.2 (Frontend) |
| **Clara — Event Subscription** | Event publishing via pub/sub, Contracts package, `PartyErased` handler patterns, dangling reference guidance, decrypted events | MVP (events + contracts), v1.1 (erasure) |

**Cross-journey insights:**
- **Marc and Clara** represent two sides of consuming app integration — command-side (Marc) and event-side (Clara). Both must be frictionless.
- **Aria and Sophie** are the same interaction viewed from different layers — Aria is the mechanism, Sophie is the experience. MCP tool quality determines both.
- **Laurent's journey** is the compliance backbone — if it doesn't work completely, the entire trust model collapses.
- **Clara's journey** validates the "single source of truth" promise — if event subscriptions are unreliable or erasure handling is unclear, consuming apps can't trust the service.

## Domain-Specific Requirements

Party management operates under significant regulatory and security constraints. This section defines the compliance, security, technical, and integration requirements that shape the architecture — each traceable to user journeys and success criteria.

### Compliance & Regulatory

**GDPR (Primary regulatory framework):**
- **Right to erasure (Article 17):** Crypto-shredding via per-party encryption keys destroyed through DAPR secret store. Both events and snapshots participate. Erasure verification across all projections, caches, and search indexes. `Phase: v1.1 | Verification: automated test suite`
- **Processing purpose tracking (Article 6):** Per-channel, per-purpose, time-stamped, revocable records supporting all lawful bases — consent, legitimate interest, contractual necessity, legal obligation. Structured records track what processing purpose applies for which contact channel, and when. `Phase: v1.1 | Verification: integration tests`
- **Data portability (Article 20):** Export all party data in machine-readable JSON on request. `Phase: v1.1 | Verification: integration test`
- **Right to restriction (Article 18):** Freeze processing of a party's data while complaints are investigated. `Phase: v1.1 | Verification: integration test`
- **Records of processing activities (Article 30):** Event stream provides complete, time-stamped record of all processing activities. `Phase: v1.1 | Verification: integration test`
- **Erasure propagation:** `PartyErased` event notifies all subscribers. Delivery tracked — unacknowledged erasures alert after configurable timeout. Unacknowledged erasures surface in admin dashboard (v1.2) with subscriber identification, enabling DPO manual follow-up with consuming application teams. `Phase: v1.1 | Verification: integration test with multiple subscribers`
- **Erasure verification scope:** Internal (projections, caches, indexes) verified automatically. External (consuming app acknowledgment) tracked but enforcement is consuming app's responsibility. `Phase: v1.1 | Verification: automated verification job + subscriber tracking`
- **Metadata after erasure:** Event metadata (types, timestamps, aggregate IDs) survives crypto-shredding — personal data doesn't. Architecturally necessary to preserve stream integrity. Documented as explicit trade-off. `Phase: v1.1 | Verification: code review + documentation`
- **Erased party reads:** Read path checks erasure state before attempting decryption — returns "Party erased" status, not decryption errors. `Phase: v1.1 | Verification: integration test`
- **Cache invalidation on erasure:** Explicit invalidation across all projection caches — not TTL-dependent. `Phase: v1.1 | Verification: integration test`
- **Search index purge:** Confirmed synchronously before erasure verification reports completion. `Phase: v1.1 (default projection) / v2 (Elasticsearch) | Verification: integration test`
- **Regulatory extensibility:** Processing purpose model accommodates future regulations (ePrivacy, AI Act) without structural changes. `Phase: v1.1 | Verification: code review`
- **DPIA notice:** Operators processing personal data at scale may need a DPIA under Article 35. Service provides data inventory and processing records to support this. `Phase: v1.1 | Verification: documentation`
- **DPA template:** Ships as deployment aid for organizations acting as data processors. `Phase: v1.1 | Verification: documentation review`

**GDPR Notice for MVP:** MVP ships without GDPR compliance features. `[PersonalData]` field attributes ship as zero-cost preparation. Non-dismissable warning banner until v1.1.

### Security & Trust Model

**Security Boundary Model:**
- **In-scope (Hexalith.Parties):** Field-level encryption, tenant filtering, JWT claim extraction, event encryption/decryption, erasure verification within service boundary, input validation, log sanitization. `Phase: MVP (tenant filtering, JWT, validation, log sanitization) / v1.1 (encryption, erasure) | Verification: CI-automated + integration tests`
- **Operator responsibility:** DAPR secret store hardening, pub/sub access control policies, infrastructure IAM, network security, key backup procedures, deployment configuration validation. `Phase: all phases | Verification: deployment validation script + documentation`

**Threat Model (summary):**

| Threat Actor | Attack Vector | Mitigation | Phase | Verification |
|---|---|---|---|---|
| Malicious tenant | JWT tenant ID spoofing | JWT validated by framework middleware; tenant from claims only | MVP | CI-automated |
| Malicious tenant | Cross-tenant pub/sub subscription | DAPR access control policies; security config checklist; deployment validation script | MVP | deployment-validation |
| Malicious tenant | Query another tenant's data via projection | Framework-enforced tenant filtering; custom projections inherit filtering base class; CI negative tests | MVP | CI-automated |
| Compromised sidecar | Key extraction from secret store | Per-tenant key namespaces; key access auditing; infrastructure hardening (operator) | v1.1 | code-review + deployment-validation |
| Careless operator | Misconfigured DAPR access policies | Security config checklist; deployment validation script | MVP | deployment-validation |
| Malicious input via MCP | Injection through party data fields | MCP goes through same FluentValidation pipeline as REST API; tenant context extracted identically to REST | MVP | CI-automated |
| Log exposure | Personal data in application logs | `[PersonalData]` fields masked/excluded — framework-enforced | MVP | CI-automated |

**Key Management:**
- Per-party encryption keys via DAPR secret store with per-tenant namespaces. `Phase: v1.1 | Verification: integration tests`
- **Key rotation:** Versioned keys — each encrypted field references its key version. Previous versions retained read-only. Re-encryption of historical events NOT performed. `Phase: v1.1 | Verification: integration test with key version fixtures`
- **Key access audit trail:** All key operations (create, read, rotate, delete) logged independently of event stream. `Phase: v1.1 | Verification: integration test`
- **Backup/restore and erasure:** Backup procedures must preserve erasure state — restoring a backup must not restore destroyed encryption keys. Operator documentation includes backup strategy guidance that accounts for crypto-shredding. `Phase: v1.1 | Verification: documentation + deployment-validation`

**Tenant Isolation:**
- Fail-closed: requests without valid tenant claim rejected with 401 — never processed with null/default tenant. `Phase: MVP | Verification: CI-automated`
- Tenant isolation negative tests: query Tenant B's data as Tenant A; verify zero results. `Phase: MVP | Verification: CI-automated`
- Custom projections inherit tenant-filtering base class. `Phase: MVP | Verification: code-review + CI-automated`

**Log Sanitization:**
- `[PersonalData]`-attributed fields masked or excluded from all application logging — enforced at framework level via serializer, not per log statement. `Phase: MVP | Verification: CI-automated (test serializer masks marked fields)`

### Technical Constraints

**Event Sourcing Domain Patterns:**
- **Atomic event persistence:** All events from a single command are committed atomically — either all are persisted, or none are. No partial writes. `Phase: MVP (via EventStore) | Verification: integration test with fault injection`
- Events are append-only, immutable contracts. Additive fields only. Breaking changes require new event type with migration window. `Phase: MVP | Verification: CI-automated (schema comparison test against previous version)`
- **Event upcasting strategy:** Consumers tolerate missing optional fields. No upcasting — events stored as-written. New optional fields have documented default behavior when absent. Tolerant deserialization required. `Phase: MVP | Verification: CI-automated (unit tests with missing/extra fields)`
- Party aggregate: single aggregate with contact channels, identifiers, processing purpose records, and type-specific details as value objects. `Phase: MVP | Verification: code-review`
- **Aggregate size guideline:** Tested and optimized for up to 50 contact channels per party. Monitor beyond. `Phase: MVP | Verification: integration test with 50-channel party`
- **Snapshot integrity:** Verifiable via checksum. Corrupted snapshots trigger automatic rebuild from event stream with logged warning. `Phase: MVP | Verification: integration test with corrupted snapshot fixture`
- **Concurrent command serialization:** Commands to same aggregate serialized by actor. Sequential consistency guaranteed. `Phase: MVP (via EventStore) | Verification: integration test with parallel commands`
- Name history preserved in event stream for temporal queries. `Phase: MVP | Verification: integration test`

**Encryption & Crypto-shredding:**
- Field-level encryption via `[PersonalData]` attributes — domain code has zero DAPR awareness. `Phase: v1.1 (runtime encryption) / MVP (attributes only) | Verification: CI-automated`
- Events published to pub/sub decrypted at publish time. `Phase: v1.1 | Verification: integration test`
- **Decryption circuit breaker:** Failure at publish time prevents publication — never publishes unreadable events. `Phase: v1.1 | Verification: integration test with encryption failure injection`
- Snapshots participate in crypto-shredding. Invalidation part of erasure transaction. **Assumption:** EventStore supports field-level encryption in snapshots natively; if not, custom snapshot serialization is required (v1.1 architecture decision). `Phase: v1.1 | Verification: integration test`
- **`[PersonalData]` coverage verification:** Automated scan validates all personal data fields have the attribute, checked against documented field inventory. `Phase: MVP | Verification: CI-automated (reflection-based test)`
- **Encryption key caching (v1.1):** Per-party key lookups at command time must not violate NFR1 (< 1 second). Architecture must specify key caching strategy (per-request cache, short-TTL in-memory cache, or batch pre-fetch) to prevent secret store latency from dominating command processing time. `Phase: v1.1 (implementation) — architecture design at MVP to ensure NFR1 compatibility | Verification: performance test under realistic key lookup latency`

**Graceful Degradation:**
- DAPR secret store unavailable: commands writing personal data fail gracefully; reads from projections continue. `Phase: v1.1 | Verification: integration test with DAPR component shutdown`
- DAPR state store unavailable: commands fail; projection reads continue if independent storage. `Phase: MVP | Verification: integration test`
- DAPR pub/sub unavailable: events committed but not published; retry on recovery via EventStore outbox pattern or DAPR built-in retry policy (mechanism must be specified during architecture). Consuming apps may experience delayed event delivery but never event loss. Documented behavior. `Phase: MVP | Verification: integration test + documentation`

### Integration Requirements

Integration surfaces are specified in detail in **API Backend & Developer Tool Requirements**. Domain-level integration constraints:

**Event-Side Integration (consuming applications):**
- Event publishing via DAPR pub/sub with decrypted events. `Phase: MVP (publishing) / v1.1 (decryption) | Verification: integration test`
- At-least-once delivery — consuming apps must implement idempotent handlers. `Phase: MVP | Verification: documentation + integration test`
- `PartyMerged` event contract in v1 for forward compatibility. `Phase: MVP (contract only) | Verification: code-review`
- Dangling reference guidance and handler patterns in Contracts package. `Phase: MVP (documentation) / v1.1 (erasure handlers) | Verification: documentation review`
- Tolerant deserialization: ignore unknown fields, handle missing optional fields. `Phase: MVP | Verification: CI-automated`

**Infrastructure Portability (via DAPR):**
- Swap state stores and message brokers without code changes. `Phase: MVP (via EventStore) | Verification: deployment-validation`
- .NET Aspire + Docker for local development. `Phase: MVP | Verification: getting-started guide test`
- **Deployment validation script:** Verifies DAPR security configuration at deployment time. `Phase: MVP | Verification: deployment-validation`

### Risk Mitigations

*See also: Innovation & Novel Patterns → Risk Mitigation for innovation-specific risks, and Project Scoping → Risk Mitigation Strategy for strategic/resource risks.*

| Risk | Impact | Mitigation | Phase | Verification |
|---|---|---|---|---|
| Aggregate growth (>50 channels) | Slow rehydration | Snapshot strategy; size guideline; monitoring | MVP | integration test |
| Event schema evolution | Breaking consuming apps | Append-only; tolerant deserialization; new event types | MVP | CI-automated |
| Key loss | Permanent data loss | Per-tenant namespaces; backup procedures (operator) | v1.1 | deployment-validation |
| Key leak | Privacy breach | Per-tenant namespaces; rotation; hardening | v1.1 | deployment-validation |
| Key destruction failure | Incomplete erasure | Retry + alert + block verification | v1.1 | integration test |
| Partial `[PersonalData]` coverage | Unencrypted personal data | Automated attribute coverage scan | MVP | CI-automated |
| Consuming apps ignore `PartyErased` | Dangling references | Guidance; handler patterns; delivery tracking | MVP (docs) / v1.1 (tracking) | documentation + integration test |
| Personal data in logs | Privacy breach via logs | Framework-enforced log sanitization | MVP | CI-automated |
| DAPR component unavailable | Service degradation | Fail-safe writes; cached reads; documented behavior | MVP | integration test |
| Cross-tenant leakage | Trust catastrophe | Framework filtering; CI negative tests; fail-closed | MVP | CI-automated |
| DAPR misconfiguration | Tenant isolation breach | Security checklist; deployment validation script | MVP | deployment-validation |
| MVP used for EU regulated data | Compliance exposure | Non-dismissable banner; emergency erasure procedure | MVP | manual review |
| Crypto-shredding ruled legally insufficient | GDPR compliance story collapses | Pragmatic best practice with industry consensus (Spotify, Kurrent); legal review per deployment; alternative (event stream rewrite) documented as architecturally destructive | v1.1 | documentation + legal review |
| DAPR deprecation/decline | Infrastructure portability lost | Thin abstractions in EventStore; bounded refactoring to direct implementations | MVP | architecture review |

### Versioning & Backward Compatibility

Three-pillar strategy ensuring consuming apps upgrade from MVP → v1.1 → v1.2 → v2 without breaking:
1. **Event schema versioning:** Append-only; additive fields; no upcasting; tolerant deserialization; new event types for breaking changes. `Phase: MVP | Verification: CI-automated`
2. **API versioning:** REST endpoints versioned (`/api/v1/parties`); coexist during deprecation. `Phase: MVP | Verification: integration test`
3. **NuGet package compatibility:** Semantic versioning; minor versions non-breaking; major bumps include migration guides. `Phase: MVP | Verification: code-review`

## Innovation & Novel Patterns

This section articulates what makes Hexalith.Parties genuinely novel — not incremental improvements, but capabilities that don't exist together in any open-source solution we've identified.

### Three Problems Nobody Else Solves

Hexalith.Parties exists at the intersection of three unsolved problems. Each piece of the solution exists somewhere — nowhere do they exist together as a deployable, open-source microservice.

### 1. AI Agents Can't Do Identity Resolution Today

AI agents processing emails, documents, and conversations constantly encounter references to people and organizations — "J. Dupont at Acme Corp." They need to determine: is this an existing party or a new one? Which Dupont? CRM vendors (HubSpot, Nutshell, ActiveCampaign, Zoho) have shipped MCP servers, but these expose CRUD operations over data models designed for human users. They let AI agents *query* — they don't help AI agents *resolve*.

Hexalith.Parties makes different design decisions because its primary consumer is different: match metadata in search results enables autonomous disambiguation, forgiving input schemas accept partial AI-extracted data, composite `create_party` achieves single-prompt creation, and response schemas include everything an AI agent needs to make confident decisions without follow-up calls. The protocol (MCP) is standard — the data model and API design driven by AI agent consumption patterns are novel. **Identity resolution as a service for AI agents is a new category** that no existing open-source solution occupies.

### 2. GDPR Erasure in Event-Sourced Systems Has No Complete Open-Source Solution

In event-sourced systems, GDPR erasure is architecturally impossible without crypto-shredding — events are immutable by design. The pattern is documented (Verraes 2019, Kurrent/EventStore, Axon) and has been implemented at scale (Spotify's "Padlock"). But most implementations require domain code to participate in encryption, don't handle snapshots, and leave erasure verification to operators.

Hexalith.Parties ships the most complete open-source implementation we've identified: field-level encryption via `[PersonalData]` attributes (domain code has zero encryption awareness), snapshot participation in crypto-shredding, decrypted events at publish time (consumers never handle encryption), and automated erasure verification across all projections, caches, and search indexes. The innovation is the developer experience of GDPR compliance — a developer writes zero encryption code, an operator triggers erasure in one operation, and verification is automated and complete.

One important caveat: some GDPR interpretations argue key deletion is not equivalent to data deletion. Crypto-shredding is the pragmatic best practice for event-sourced systems where the alternative (rewriting the entire event stream) is worse. Legal review is recommended per deployment.

### 3. Enterprise Party Management Has Always Required Enterprise Infrastructure — Until Now

Type-discriminated contact channels, jurisdiction-specific identifiers, and structured consent are proven domain abstractions from D365 Global Address Book, SAP Business Partner, and Oracle TCA. Every developer who has built contact management from scratch for the 4th time knows these patterns are needed — but they've been locked inside ERP ecosystems requiring expensive licenses and proprietary infrastructure.

**Why now? Three infrastructure shifts converged:**
- **Event sourcing frameworks** (Hexalith.EventStore) make CQRS/ES viable without custom infrastructure
- **Infrastructure portability** (DAPR) decouples domain services from specific state stores and message brokers
- **AI agent protocols** (MCP) create a new consumption surface that ERPs cannot serve

This combination makes enterprise-grade party management viable as a standalone, MIT-licensed microservice for the first time.

### Market Context & Competitive Landscape

The MCP ecosystem has reached critical mass — 97M+ monthly SDK downloads, Linux Foundation governance via the Agentic AI Foundation, and adoption by OpenAI, Google, and Microsoft. Every major CRM vendor is shipping MCP servers, but these retrofit AI access onto human-centric data models.

No open-source solution we've identified occupies the intersection of: (1) dedicated party management with AI-native identity resolution, (2) event sourcing with CQRS, (3) GDPR crypto-shredding by design, and (4) infrastructure portability via DAPR.

| Capability | CRM MCP Servers | Hexalith.Parties | Custom Build | Time to Equivalent |
|---|---|---|---|---|
| AI identity resolution (match metadata, disambiguation) | No — query only | **Yes — first-class** | Build it yourself | 1-2 weeks |
| Forgiving partial input / composite creation for AI | No | **Yes — by design** | Build it yourself | 1 week |
| Open-source, event-sourced microservice | No (proprietary) | **Yes** | You build it | 4-6 weeks |
| GDPR crypto-shredding (zero domain code) | N/A | **Yes (v1.1)** | Build it yourself | 4-8 weeks |
| Automated erasure verification across all projections | No | **Yes** | Build it yourself | 1-2 weeks |
| Multi-tenancy at all layers | Per-account | **Yes** | Build it yourself | 2-4 weeks |
| One-line NuGet integration | N/A | **Yes** | N/A | — |
| **Total time to equivalent** | — | **30 minutes** | — | **13-23 weeks** |

### Validation Approach

- **AI identity resolution validation:** 10 structured scenarios tested across Claude, Copilot, and at least one other AI assistant: (1) unambiguous single match, (2) multiple candidates requiring disambiguation, (3) no match triggering party creation, (4) partial input from email extraction, (5) organization + person composite creation. Pass threshold: 8/10 scenarios resolved correctly on first tool call without retry.
- **Enterprise pattern validation:** Confirm unified contact channel model handles: multi-jurisdiction organization (3+ identifier types), individual with 10+ contact channels across 4 types, and mixed person/organization search — without schema changes.
- **Crypto-shredding validation:** Automated test suite verifying complete erasure across events, snapshots, projections, caches, and search indexes. Fault injection for key destruction failure. Verification that erased party reads return "Party erased" status, not decryption errors.
- **"Don't build it" validation:** Measure actual integration time for first consuming application. Target: < 1 day from NuGet install to first command in production code, vs. estimated 13-23 weeks to build equivalent capabilities.

### Risk Mitigation

*Innovation-specific risks. See also: Domain-Specific Requirements → Risk Mitigations for technical/operational risks, and Project Scoping → Risk Mitigation Strategy for strategic/resource risks.*

| Innovation Risk | Impact | Mitigation |
|---|---|---|
| MCP adoption stalls industry-wide | AI-native value proposition weakens | REST API + NuGet carry primary adoption; MCP de-emphasized but available |
| AI agents misuse forgiving input schemas | Data quality degrades | Same FluentValidation pipeline as REST; admin tooling for quality review |
| Enterprise pattern over-engineered for small use cases | Developer adoption friction | MVP strips to minimum viable abstractions; complexity opt-in |
| Innovation claims challenged by counterexample | Credibility risk | Claims scoped to "no open-source solution we've identified"; community invited to correct |

## API Backend & Developer Tool Requirements

Building on the functional requirements, this section specifies the concrete API surfaces, data formats, error handling, and developer experience contracts that shape implementation.

### Project-Type Overview

Hexalith.Parties is a hybrid **API backend microservice** and **developer tool** — it delivers value both as a running service (REST API, MCP server) and as a set of consumable packages (NuGet). The project-type requirements reflect both dimensions.

### API Surface & Endpoint Specification

**Command Endpoints (REST):**
All commands from the Party aggregate are exposed as REST endpoints following EventStore conventions. Commands are routed through the MediatR pipeline with FluentValidation, idempotent handling, and tenant context propagation.

**Query Endpoints (REST):**
- `GET /api/v1/parties` — paginated list with filters (type, active/inactive)
- `GET /api/v1/parties/{id}` — full party details by ID
- `GET /api/v1/parties/search?q=` — display-name search with match metadata. Email, identifier, and semantic search require the dedicated search capability.
- **OpenAPI 3.x specification** auto-generated from endpoint definitions and published with the service

**MCP Tools (5 tools):**
- `find_parties`, `get_party`, `create_party`, `update_party`, `delete_party`
- Naming decision: `find_parties` unifies search and list modes; `delete_party` is an AI-ergonomic alias for soft deactivation and is not GDPR erasure.
- Designed for AI ergonomics, not as 1:1 command API mirrors

**API Transport Decision:**
REST is the only guaranteed API surface for MVP. URL-path versioning (`/api/v1/`) chosen over header-based versioning for discoverability and simplicity. gRPC is a v1.1 candidate if demand warrants — not a "maybe" in MVP scope.

### Authentication & Authorization

Inherited entirely from Hexalith.EventStore middleware — no custom auth implementation in Parties. JWT-based authentication, tenant identity from token claims, framework-enforced tenant filtering on all queries, DAPR access control on tenant-scoped pub/sub topics. See EventStore documentation for details.

### Data Schemas & Formats

- **Request/Response format:** JSON only for MVP
- **Event schemas:** Append-only contracts following semantic versioning — new fields additive, never breaking
- **Contract package:** All command, event, and query types published in Hexalith.Parties.Contracts NuGet package

### Error Handling & Response Model

- **Domain rejections:** Typed `DomainResult` responses with structured error details
- **REST mapping:** RFC 9457 Problem Details format with machine-readable error `type` URIs mapped to a documented error catalog
  - `400` — syntactic validation failures (malformed requests)
  - `422` — semantic validation failures (well-formed command with invalid data, e.g., invalid country code)
  - `404` — party not found
  - `409` — conflict / idempotent duplicate detection
  - `403` — tenant isolation violation
- **Error catalog:** Documented error types shipped in Contracts package for consuming app error handling

### Rate Limiting

Rate limiting is a deployment infrastructure concern (API gateway / DAPR middleware), not application domain logic — no rate limiting code in the Parties service.

### API Versioning

- URL-path versioning: `/api/v1/parties` — explicit architectural decision for discoverability and simplicity over header-based versioning
- Version coexistence during deprecation periods
- Full versioning strategy (event schemas, NuGet packages) detailed in **Domain-Specific Requirements → Versioning & Backward Compatibility**

### Developer Experience & Packaging

**NuGet Packages (MVP):**
- `Hexalith.Parties.Contracts` — commands, events, query types (includes forward-compatible `PartyMerged`). **Zero runtime dependencies** beyond `netstandard2.1` — consuming apps inherit no infrastructure stack.
- `Hexalith.Parties.Client` — `IPartiesCommandClient`, `IPartiesQueryClient`, `AddPartiesClient()` one-liner. Depends only on Contracts + HTTP abstractions. No DAPR, no MediatR, no FluentValidation.

**Language Support:**
- .NET: first-class via NuGet packages (MVP)
- All languages: REST API directly (MVP)
- AI agents: MCP server regardless of language (MVP)
- TypeScript/Python client packages: v2 candidates based on adoption signals

**Installation & Integration:**
- Single NuGet package + one-line DI registration for .NET consumers
- REST API for non-.NET consumers — no SDK required
- Zero infrastructure dependencies in client packages — consuming apps don't inherit the service's stack

### Documentation Strategy

Docs-as-code in repository (`/docs` folder + README).

**MVP Documentation Deliverables:**
- **README** — value proposition in first paragraph, clear positioning
- **Getting-started guide** — tested by non-author developer, with explicit narrative arc:
  1. Prerequisites and deployment (< 15 minutes with .NET Aspire + Docker)
  2. First `CreateParty` command via REST (< 30 minutes)
  3. First query — search and retrieve the created party
  4. MCP server setup and first AI agent tool call
  5. NuGet package integration with `AddPartiesClient()` in a consuming app
- **GDPR disclaimer** — in README, getting-started guide, and service startup banner
- **OpenAPI spec** — auto-generated, browsable via Swagger UI in development mode

DocFx or dedicated documentation site deferred to post-MVP based on community need.

### Sample Integration

A separate, runnable reference project at `/samples/BasicConsumingApp/`:
- Demonstrates `AddPartiesClient()` + one-line DI registration
- Sends commands (create party, add contact channel)
- Queries parties (search, get by ID)
- Subscribes to party events and builds a simple local read model
- Configures and uses the MCP server for AI agent scenarios
- Runnable with `dotnet run`, CI-verifiable

## Project Scoping & Phased Development

This section captures the strategic rationale behind the MVP boundary, resource model, and risk mitigations that govern execution. Feature-level scope is defined in **Product Scope**.

### MVP Strategy & Philosophy

**MVP Approach:** Platform MVP with experience gates — validate Hexalith.EventStore as a viable domain service foundation, delivered through a developer experience that proves "integrate, don't rebuild." Event sourcing is chosen both for technical merit (complete audit trail, natural event-driven integration, crypto-shredding viability) and as strategic validation of the Hexalith.EventStore programming model. Priority ordering: (1) Platform validation, (2) Developer & AI agent experience, (3) Adoption proof through consuming applications.

**Resource Model:** Small team, scope-fixed/timeline-flexible. EventStore infrastructure work is expected and budgeted — it extends the timeline, not the scope. The 60% effort escalation trigger provides a go/no-go checkpoint.

**Feedback Model:** First-consumer friction is the highest-priority input for v1.1 prioritization. The MVP scope is fixed, but adoption friction signals from real consumers shape what ships next. Scope-fixed means the MVP doesn't expand — it means we ship, learn, and adjust post-MVP.

### MVP Scope Confirmation

The MVP scope defined in **Product Scope** has been pressure-tested and confirmed as the irreducible minimum. The MVP is large by design — the minimum for "don't build your own party management" must demonstrate enough breadth across all surfaces (API, MCP, NuGet, documentation) that the alternative (13-23 weeks of custom development) is clearly worse. All four core user journeys are supported at MVP: Marc (Developer), Aria (AI Agent), Sophie (End User), and Clara (Event Subscriber). Laurent's journey (GDPR/Admin) activates at v1.1 + v1.2.

See **Product Scope → MVP** for the complete feature set and success gates. See **Product Scope → Growth Features** and **Vision** for post-MVP phasing.

### Risk Mitigation Strategy

*Strategic and resource risks. See also: Domain-Specific Requirements → Risk Mitigations for technical/operational risks, and Innovation & Novel Patterns → Risk Mitigation for innovation-specific risks.*

**Technical Risks:**
- *Primary risk: complexity and instability* — mitigated by scope-fixed/timeline-flexible model, EventStore effort tracking with 60% escalation trigger, hard/soft gate distinction in MVP success criteria
- *Dual platform risk (building Parties while validating EventStore)* — mitigated by expectation setting: EventStore bugs are platform validation, not scope creep. Fix EventStore, don't work around it in Parties. Test strategy separates pure domain unit tests (no EventStore), EventStore integration tests (generic aggregates), and full-stack Parties integration tests for fast fault isolation
- *Two API surfaces (REST + MCP) day-one* — mitigated by shared FluentValidation pipeline; MCP is not a separate code path but a different projection of the same domain logic
- *Multi-tenancy blast radius* — mitigated by CI-automated tenant isolation negative tests with 10+ concurrent tenants
- *DAPR deprecation or community decline* — DAPR abstractions in EventStore are thin; replacing with direct implementations (e.g., PostgreSQL state store, RabbitMQ pub/sub) is bounded refactoring work, not a rewrite. Infrastructure portability is a design goal, not a DAPR dependency

**Market Risks:**
- *MCP adoption stalls* — REST API + NuGet carry adoption; MCP de-emphasized but available
- *No external adoption in 12 months* — investigate docs, outreach, developer experience friction; 3-app target with at least 1 external provides early signal

**Resource Risks:**
- *EventStore consumes more effort than expected* — timeline extends, scope doesn't. Escalation trigger at 60% for 2 consecutive sprints
- *Fundamental EventStore design gap (not a bug)* — highest-impact risk. Mitigation: early aggregate validation, snapshot strategy testing, and willingness to contribute architectural changes upstream. **Go/no-go outcomes if EventStore is fundamentally unviable:** (1) fork EventStore and fix the gap directly, (2) evaluate alternative ES framework (Marten, Wolverine), (3) descope to REST-only stateless service without event sourcing as interim delivery

## Functional Requirements

Each functional requirement below is traceable to user journeys and success criteria. Requirements are capabilities, not implementation — they specify *what* the system does, leaving *how* to the architecture phase. Phase annotations indicate MVP vs. post-MVP delivery.

### Party Lifecycle Management (MVP)

- **FR1:** Authorized client can create a new party as either a person or an organization with type-specific details
- **FR2:** Authorized client can update person-specific details (first name, last name, date of birth, name prefix/suffix)
- **FR3:** Authorized client can update organization-specific details (legal name, trading name, legal form, registration number)
- **FR4:** Authorized client can deactivate a party (soft lifecycle management)
- **FR5:** Authorized client can reactivate a previously deactivated party
- **FR6:** System derives display name and sort name automatically from type-specific details using documented derivation rules (MVP: simple concatenation — `"{FirstName} {LastName}"` for persons, `"{LegalName}"` for organizations; locale-aware formatting deferred to v1.1)
- **FR7:** Each party has a client-generated, immutable UUID as its stable identity

### Contact Channel Management (MVP)

- **FR8:** Authorized client can add a contact channel to a party with type-specific structured data (postal, email, phone, social)
- **FR9:** Authorized client can update an existing contact channel on a party
- **FR10:** Authorized client can remove a contact channel from a party
- **FR11:** Authorized client can mark a contact channel as preferred for its type

### Identifier Management (MVP)

- **FR12:** Authorized client can add an identifier to a party (VAT, SIRET, national ID, or other jurisdiction-specific references)
- **FR13:** Authorized client can remove an identifier from a party

### Party Discovery & Search (MVP)

- **FR14:** Consumer can list parties with pagination and filtering by type (person/organization) and active status
- **FR15:** Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields.
- **FR16:** *(Deferred to v1.1)* Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1.
- **FR17:** Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans. MVP emits `displayName`; `email` and `identifier` are reserved for the future search model.
- **FR18:** Consumer can retrieve full party details by ID
- **FR19:** Recently created or updated parties become discoverable in search results within the eventual consistency window defined by NFR6
- **FR56:** System publishes auto-generated API specification documentation accessible to developers
- **FR68:** Consumer can filter parties by creation date or last-modified date range
- **FR72:** *(Deferred to v1.1)* Consumer can query a party's historical name as it was at a specific point in time (temporal name query for legal and audit purposes). Name history is preserved in the MVP event stream; the query API ships in v1.1 alongside GDPR audit features, since the primary use case is legal/audit.

### AI Agent Identity Resolution (MVP)

- **FR20:** AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP. Email and identifier resolution require candidate retrieval or the future dedicated search capability.
- **FR21:** AI agent can create a complete party (type details + contact channels + identifiers) in a single composite operation
- **FR22:** AI agent can update party details, add/modify/remove contact channels and identifiers via a single operation
- **FR23:** AI agent can retrieve full party details and list parties via dedicated AI-optimized tools
- **FR24:** AI agent party creation returns the complete created party record, not just an identifier
- **FR25:** AI agent tools accept partial and incomplete input gracefully, with documented default behaviors for omitted fields, and clear validation error messages when required fields are missing
- **FR74:** MCP update operations use patch semantics — only specified fields are modified; unspecified fields remain unchanged. AI agents never need to send full party state to make a partial update

### Developer Integration (MVP)

- **FR26:** .NET developer can integrate party management via a single package and one-line dependency registration
- **FR27:** Developer can send party commands via typed client abstractions without infrastructure knowledge
- **FR28:** Developer can query parties via typed client abstractions without infrastructure knowledge
- **FR29:** Developer can interact with the party service via REST API from any programming language
- **FR30:** System returns typed rejection responses when commands fail, including error type URI, human-readable message, and corrective action — enabling developers to resolve the issue without consulting documentation or debugging the service
- **FR31:** Developer can deploy the full Parties topology from source to a Kubernetes target (local cluster — kind/minikube/k3d/Docker Desktop — for MVP) using artifacts generated from the Aspire AppHost
- **FR31a:** A single PowerShell pipeline (`pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>`) takes the operator from a clean checkout to a healthy 10-pod cluster in one command. The pipeline (Epic 9 v2, 2026-05-21) resolves the MinVer-stamped image tag, builds and pushes 7 container images to the self-hosted Zot OCI registry at `registry.hexalith.com` (ADR D-K8s-1), regenerates Kubernetes manifests via `dotnet aspirate generate`, applies three idempotent post-aspirate patches (Dapr annotations + JWT `secretKeyRef` + Zot `imagePullSecrets`), bootstraps three operator-managed Secrets (`hexalith-jwt-signing`, `hexalith-keycloak-admin`, `zot-pull-secret` — ADR D-K8s-2 Path B), applies Dapr CRs from `deploy/dapr/` (Components → Configurations → Subscriptions), then applies the Kustomization under `deploy/k8s/`. The topology is **enumerative**: 7 Aspirate-composed services (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`) plus 3 hand-authored carve-outs (`redis` MVP emptyDir + no AUTH; `keycloak` with randomized admin from Secret; `falkordb` graph backing store), totalling 10 workloads in namespace `hexalith-parties`. Dapr-equipped workloads include `eventstore`, `eventstore-admin`, `eventstore-admin-ui` (service-invocation client only), `parties`, `tenants`, and `memories`; `parties-mcp`, `redis`, `keycloak`, and `falkordb` remain non-Dapr. Image tags carry the MinVer-resolved version (regex `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$`) and are immutable per commit; mutable tags (`latest`, `staging-latest`, empty) are explicitly forbidden by `validate-deployment.ps1`. The `-ConfirmContext` gate (ADR D-K8s-3) replaces the legacy local-cluster regex allowlist so the same pipeline runs against any operator-owned kubectl context. The canonical architecture reference is `docs/kubernetes-deployment-architecture.md` (13 sections covering topology, configuration sources, operator workflow, reproducibility guarantees, and MVP boundaries). NFR30 (< 15 min from clean checkout to first successful query) remains in force.
- **FR32:** Getting-started documentation enables a developer to deploy and send their first command as a self-service experience
- **FR33:** Contract types package has zero runtime dependencies beyond netstandard2.1 — consuming applications inherit no infrastructure stack
- **FR57:** System supports versioned API endpoints that coexist during deprecation periods
- **FR58:** System maps domain rejections to standardized HTTP error formats with a documented error catalog
- **FR59:** System provides a runnable sample integration project demonstrating command, query, event subscription, and MCP usage
- **FR60:** Developer can run the full system locally with a single command for development and evaluation
- **FR69:** Update operations (API and MCP) return the updated party state in the response, not just a confirmation

### Event-Driven Integration (MVP)

- **FR34:** System publishes domain events when party state changes
- **FR35:** Consuming application can subscribe to party events and build domain-specific read models
- **FR36:** System handles duplicate commands idempotently (safe deduplication in distributed scenarios)
- **FR37:** Forward-compatible event contracts (including party merge) are available to consuming applications from day one
- **FR38:** Consuming application documentation includes handler patterns for erasure and dangling reference cleanup, with explicit warning that `PartyErased` subscription is mandatory for all consuming apps regardless of which other events they handle
- **FR63:** System guarantees at-least-once event delivery to subscribers
- **FR70:** Published domain events include tenant context for consuming application routing decisions
- **FR73:** System delivers events for a single aggregate in causal order to each subscriber
  - *Architecture note: Architecture must verify DAPR pub/sub ordering guarantees. If per-aggregate ordering cannot be guaranteed, document required handler design (order-tolerant or sequence-checking) in the architecture document.*

### Multi-Tenancy & Security (MVP)

- **FR39:** System isolates party data by tenant at all layers — no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering
- **FR40:** System identifies tenant from authenticated credentials, never from request payloads
- **FR41:** System rejects requests without valid tenant identity (fail-closed)
- **FR42:** Personal data fields are architecturally marked for automated privacy enforcement without domain code changes
- **FR43:** Personal data fields are excluded from all application logging
- **FR61:** System provides deployment validation tooling to verify security configuration before production use
- **FR62:** System displays a non-dismissable compliance warning until GDPR features are activated

### GDPR Compliance (v1.1)

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

### System Resilience (MVP)

- **FR64:** System degrades gracefully when infrastructure components are unavailable — read operations continue when write-side components fail
- **FR71:** System exposes health and readiness signals for infrastructure orchestration

### Administration & Frontend (v1.2)

- **FR65:** Administrator can browse, search, and inspect party records via an administration interface
- **FR66:** Administrator can process GDPR requests (erasure, restriction, consent, export) via the administration interface
- **FR67:** Consuming application developer can embed a party picker component in their UI for party search and selection

## Non-Functional Requirements

Quality attributes that constrain the architecture. All NFRs are measurable and include verification methods. Performance targets are validated at MVP scale (100K parties/tenant); scaling beyond this is a v2 concern.

### Performance

- **NFR1:** Command processing (create, update, manage party) completes in < 1 second at NFR17 throughput levels; MCP tool calls complete in < 1 second end-to-end including transport
- **NFR2:** Query operations (search, get by ID, list) return results in < 500ms at NFR17 throughput levels
- **NFR3:** Aggregate rehydration completes in < 200ms with snapshot strategy active
- **NFR4:** Search across 100K parties per tenant returns results within 500ms
- **NFR5:** Service accepts requests within 30 seconds of container launch (cold start)
- **NFR6:** Read projections reflect write operations within 2 seconds at NFR17 throughput levels (eventual consistency window)

### Security

- **NFR7:** All data encrypted in transit (TLS 1.2+)
- **NFR8:** Personal data fields encrypted at rest using per-party keys (activated in v1.1)
- **NFR9:** Tenant isolation enforced at all layers — zero cross-tenant data leakage under any condition
- **NFR10:** JWT token validation on every request; fail-closed on invalid or missing tokens
- **NFR11:** Per-tenant encryption keys can be rotated without service downtime or data loss
- **NFR12:** Personal data excluded from all application logs
- **NFR13:** All API endpoints require authentication — no anonymous access

### Scalability

- **NFR14:** System supports multi-tenant operation (no per-tenant infrastructure, stateless routing, partitionable metadata) validated at 100 concurrent tenants for MVP
- **NFR14a:** System architecture supports scaling beyond 100 tenants without per-tenant infrastructure changes
- **NFR15:** Tenant metadata operations (routing, key lookup) complete in < 50ms regardless of total tenant count
- **NFR16:** System supports up to 100,000 parties per tenant (MVP validation target — sufficient for startups and SMBs; enterprise scale at millions of parties addressed in v2 via Elasticsearch projection and horizontal scaling)
- **NFR17:** System sustains 100 read requests/second and 20 write requests/second per tenant
- **NFR18:** Event store performance degrades < 10% at 100K parties per tenant with snapshot strategy active
- **NFR19:** Read projections remain responsive (< 500ms) at 100K parties per tenant

### Reliability

- **NFR20:** Service recovers from crash, replays necessary event state, and accepts requests within 30 seconds of restart
- **NFR21:** When event store is unreachable, read projection queries continue serving cached data with a staleness indicator
- **NFR22:** No data loss on service restart — event store is the durable source of truth
- **NFR23:** At-least-once event delivery to subscribers via DAPR pub/sub
- **NFR24:** Idempotent command handling ensures safe retry without duplicate side effects

### Integration

- **NFR25:** REST API conforms to auto-generated OpenAPI 3.x specification
- **NFR26:** MCP server implements MCP protocol specification with 5 tools
- **NFR27:** Published events follow stable, versioned contract schemas (append-only, additive changes only)
- **NFR28:** Client NuGet packages impose < 10 transitive dependencies totalling < 5 MB (Contracts: zero runtime dependencies beyond netstandard2.1)
- **NFR29:** Service has zero direct dependencies on specific state store or message broker implementations

### Developer Experience

- **NFR30:** A developer deploys a running instance from source in < 15 minutes on first attempt using the documented getting-started guide
- **NFR31:** NuGet client package size < 5MB with < 10 transitive dependencies
- **NFR32:** (v1.2) Frontend applies output encoding to all party data fields rendered in the admin portal — no stored XSS from user-supplied or AI-created party data
