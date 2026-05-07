# Sprint Change Proposal — EventStore-Fronted Architecture Pivot

- **Date:** 2026-05-07
- **Author:** Jérôme (with bmad-correct-course assistance)
- **Project:** Hexalith.Parties
- **Trigger:** "the aspire host should spin up the eventstore service and the eventstore ui"
- **Scope classification:** **Major** — fundamental replan; routes through new Epic 12 plus PRD/Architecture revisions.

---

## 1. Issue Summary

The Parties AppHost currently runs Parties as a **self-hosting actor service** that embeds `Hexalith.EventStore.Server` in-process and exposes a Parties-specific REST and MCP surface. The local Aspire topology never starts the standalone EventStore service or its Admin UI, so:

- developers cannot inspect Parties events, streams, or admin operations through EventStore tooling;
- Parties does not validate the platform's intended **service-split pattern** (domain service ⇢ EventStore HTTP/DAPR API), which is the contract every future Hexalith domain service should follow;
- the canonical EventStore-Sample shape (client + ServiceDefaults only, all traffic via `POST /api/v1/commands`) is not exercised by the platform's first real domain consumer.

The decision is to pivot the runtime topology so that **all client commands and queries are sent to EventStore**, which routes to **Parties-hosted actors** via DAPR. Parties stops exposing its own REST and in-process MCP surface and becomes a thin actor host plus projection runtime. The Admin Portal is rebuilt on `Hexalith.FrontComposer` and consumes EventStore queries; the EventStore Admin UI handles generic stream/event browsing.

### Issue category

Strategic pivot / architectural validation. The original Story 1.7 implementation took the in-process shortcut (the EventStore submodule's library path), which works locally but does not validate the multi-service deployment topology that is the platform's stated end-state.

### Evidence

- `src/Hexalith.Parties/Hexalith.Parties.csproj` line 18 — references `Hexalith.EventStore.Server.csproj` (server-side, hosts actors).
- `src/Hexalith.Parties/Program.cs` line 51 — `app.MapActorsHandlers()` (Parties hosts its own actors).
- `src/Hexalith.Parties.AppHost/Program.cs` lines 26–42 — no `eventstore`, `eventstore-admin`, `eventstore-admin-ui` resources; uses the `AddHexalithParties` overload that bypasses `AddHexalithEventStore`.
- `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj` — references only `EventStore.Client` + `ServiceDefaults` (the canonical domain-service shape).
- `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` — shows the full intended topology (eventstore + admin + admin-ui + tenants + sample).

---

## 2. Impact Analysis

### 2.1 Epic-level impact

**Direct-impact epics**

| Epic | Stories | Status | Impact |
|---|---|---|---|
| **1. Domain Foundation** | 1.6 REST error handling & retrieval | done | **Surface invalidated** — `PartiesController`, problem-details mapping, `GET /api/parties/{id}` deleted. EventStore gateway owns these. |
| | 1.7 AppHost & GDPR warning | done | **Surface invalidated** — AppHost rewritten; FR62 demoted to startup-log-only. |
| **2. Contact Channels & Identifiers** | 2.4 REST endpoints | done | **Surface invalidated** — channel/identifier endpoints removed; integration tests rewritten. |
| **3. Discovery & Search** | 3.3 Search match metadata & query endpoints | done | **REST surface invalidated**; query actor preserved. Queries flow through `POST /api/v1/queries`. |
| | 3.4 Projection tests | done | Tier-2/3 tests rewritten through EventStore. |
| **4. Composite Commands** | 4.4 Composite REST & validation | done | **Surface invalidated** — composite endpoint removed. FluentValidation relocates to the actor host. |
| **5. AI Agent (MCP)** | 5.1–5.4 | all done | **Heavily impacted** — in-process MCP removed; replaced by new thin `Hexalith.Parties.Mcp` host calling EventStore. |
| **6. Developer Integration** | 6.1 Client package | done | **Invalidated** — `Hexalith.Parties.Client` becomes a typed thin wrapper over `Hexalith.EventStore.Client`. |
| | 6.2 Getting started | done | Updated for new topology. |
| | 6.3 Sample integration | done | Updated to mirror `Hexalith.EventStore.Sample` shape. |
| **7. Event-Driven Integration** | 7.1–7.3 | all done | **Mostly preserved** — publish/subscribe semantics unchanged; documentation only. |
| **8. Operational Readiness** | 8.1 Deployment validation | done | Updated for the four-service topology. |
| | 8.2 Health/readiness | done | Reworked — Parties is now an actor host; user-facing readiness shifts to EventStore. |
| | 8.3 Projection health | done | Endpoint paths change; logic preserved. |
| **9. GDPR Compliance** | 9.5 Temporal name queries | done | **REST surface invalidated**; query actor preserved. |
| | 9.6 Memories-backed search | done | **REST surface invalidated**; search service preserved. |
| | 9.1–9.4 | all done | **Preserved** at aggregate/actor level. |
| **10. Administration & Frontend** | 10-1 Browse/search/inspect | done | **Heavily impacted** — admin portal rebuilt on FrontComposer + EventStore queries. |
| | 10-1-1 FrontComposer + Tenants | in-progress | Pause; rolled into Epic 12 admin-portal rebuild. |
| | 10-2 GDPR ops | ready-for-dev | Reworked — calls EventStore command surface. |
| | 10-3 Picker component | in-progress | Pause; rewritten to query through EventStore. |
| **11. Tenants Integration** | 11.1 AppHost & package | done | **Reworked** — AppHost is the central deliverable; recomposed around EventStore. |
| | 11.3 REST/MCP tenant authorization | done | **Gateway-level enforcement moves to EventStore's `ITenantValidator`/`IRbacValidator`**; aggregate-level enforcement preserved; `ITenantAccessService` dropped or repurposed for projection-side filtering. |
| | 11.4 Integration tests, deploy, docs | done | Updated. |

**Net counts**

- Stories preserved unchanged: **13** of 41 (aggregate, projection, contracts, encryption, GDPR aggregate logic).
- Stories with surface invalidated, logic preserved: **14** (REST/MCP layer rewrites only).
- Stories materially reworked: **11** (AppHost, MCP host, Client, Admin Portal, Picker, deployment validation).
- Stories with rescope decision: **3** (5.x → thin Parties.Mcp; 10-x → FrontComposer + EventStore Admin UI; 6.1 → thin client wrapper).

### 2.2 Artifact conflicts

**PRD (`_bmad-output/planning-artifacts/prd.md`)**

- **FR60 (round-trip CreateParty → GET by ID)** — restated against `POST /api/v1/commands` (Domain="Parties", CommandType="CreateParty") + `POST /api/v1/queries` (QueryType="GetParty").
- **FR62 (non-dismissable GDPR warning)** — demoted from per-response header to startup-log-only. Wording softened from "every API response" to "service startup logs at Warning level".
- **Epic 5 (MCP) narrative** — restated as "thin Parties.Mcp host that calls EventStore" rather than in-process `MapMcp`.
- **Epic 10 (Admin Portal) narrative** — restated as "FrontComposer-based admin portal that consumes EventStore queries; EventStore Admin UI handles generic stream/event browsing".
- **NFR5 (30-second startup)** — re-anchored at EventStore's gateway readiness with the Parties actor host warm.
- **Multi-tenancy enforcement contract (FR39–FR43)** — split: gateway-level tenant validation owned by EventStore's `ITenantValidator`/`IRbacValidator`; aggregate-level write-side isolation preserved in Parties actors; `ITenantAccessService` dropped from the request path.

**Architecture (`_bmad-output/planning-artifacts/architecture.md`)**

- **NEW ADR D14 — "EventStore-fronted command/query routing for Parties"** — load-bearing decision; rationale, alternatives, consequences.
- **NEW ADR D15 — "Admin portal on FrontComposer; EventStore Admin UI handles generic stream browsing"** — clarifies the boundary between the two UIs.
- **D3 (Snapshot) and D13 (Event ordering)** — preserved; reinforced by the change.
- **REST API Design section** — Parties endpoint catalog deleted; replaced by a payload catalog (`CommandType`/`QueryType` strings + payload schemas usable against EventStore's generic gateway).
- **Component Mapping table** — `Multi-Tenancy (FR39-43)` row repointed from `PartiesController.cs (JWT extraction)` to EventStore's auth pipeline.
- **Architecture Stack diagram** — replaced: gateway (eventstore) → actor host (parties) → projections (parties) → admin pair (eventstore-admin + eventstore-admin-ui) → admin portal (parties-admin-portal on FrontComposer) → MCP host (parties-mcp).
- **Solution Structure** — adds `Hexalith.Parties.Mcp` and `Hexalith.Parties.Mcp.Tests` projects; documents new role of `Hexalith.Parties` (actor host, no controllers).

**UX/UI specifications**

- No `ux*.md` artifact found in `_bmad-output/planning-artifacts`. Captured as a **known gap**: the rebuilt FrontComposer-based admin portal (Story 12.7) and the rewritten Picker (Story 12.8) need a fresh UX spec authored as part of Wave 2.

**Secondary artifacts**

- `_bmad-output/implementation-artifacts/sprint-status.yaml` — add Epic 12 with stories 12.0–12.10; existing `done` stories remain `done`.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` — assertions rewritten for the new four-service topology.
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` — invert: controllers MUST NOT exist in `Hexalith.Parties`; MCP host MUST be a separate project.
- `tests/Hexalith.Parties.IntegrationTests/**` — every test that hits Parties REST is rewritten against EventStore's `/api/v1/commands` shape (≈20+ files).
- `tests/Hexalith.Parties.DeployValidation.Tests/TenantsDeploymentValidationTests.cs` — validates the four-service deployment shape.
- `README.md`, `samples/Hexalith.Parties.Sample`, getting-started docs — sample HTTP commands and Aspire screenshots.
- DAPR component files under `src/Hexalith.Parties.AppHost/DaprComponents/` — split as in EventStore's own AppHost: `accesscontrol.yaml` (eventstore), `accesscontrol.eventstore-admin.yaml`, `accesscontrol.tenants.yaml`, `accesscontrol.parties.yaml` (new — for the parties actor host sidecar), `resiliency.yaml`.
- `src/Hexalith.Parties/Hexalith.Parties.csproj` — repurposed as the Parties actor host: keeps `EventStore.Server` reference (for the actor framework + `EventStoreAggregate<TState>` base), drops controllers, drops MCP wiring, drops Parties-specific middleware (except those still applicable to the actor sidecar surface).

### 2.3 Technical impact

- **Auth boundary shifts** from Parties' `[Authorize]` controllers to EventStore's gateway. Parties retains DaprInternal authentication for the actor invocation path.
- **Validation moves into the actor host** via FluentValidation convention discovery on payload types (the same mechanism EventStore.Server already uses for command-type discovery).
- **Tenant authorization moves to EventStore** entirely; `ITenantAccessService` is dropped from the request path. Parties' tenant-events projection (Story 11.2) is preserved for projection-side filtering.
- **GDPR warning surface narrows** to a startup-log-only contract per FR62 demotion.
- **Two-wave sequencing is structural**: Wave 2 (consumer migration: Client, Admin Portal, Picker, Sample, MCP, docs) cannot start until Wave 1 (server pivot: AppHost, actor host, validation relocation, tier 1/2 server tests) lands.

### 2.4 Risk assessment

- **Highest risk:** Story 12.0 spike. If EventStore's `SubmitCommand` MediatR handler does not cleanly invoke actors hosted in a separate `appId`, a platform-level patch in the EventStore submodule is required — outside this repo's sprint scope. **Spike result is the gate** for the remainder of Epic 12.
- **Medium risk:** integration test rewrite scope. Tier-2/Tier-3 suites are extensive; preserving coverage during rewrite needs careful checklists per file.
- **Lower risk:** Admin Portal rebuild on FrontComposer + EventStore queries — well-trod pattern from FrontComposer's own samples.

---

## 3. Recommended Approach

**Hybrid: Option 4 (new Epic 12) + Option 3 (targeted PRD MVP review).**

- New **Epic 12 — EventStore-Fronted Architecture Pivot** captures the rework. Existing `done` stories stay `done`; Epic 12 explicitly supersedes the listed surface-layer deliverables.
- Targeted PRD edits: FR60 restated, FR62 demoted, Epic 5 and Epic 10 narratives reframed, NFR5 re-anchored, FR39–FR43 split.
- Targeted Architecture additions: ADR D14 + ADR D15, replaced REST table with payload catalog, updated component mapping and topology diagram.
- **Story 12.0 is a feasibility spike and is the gate** for the remainder of Epic 12.
- **Two-wave sequencing** is enforced by the story order.

### Rationale

- Existing `done` stories produced reusable domain logic (contracts, Handle/Apply, projections, encryption, GDPR aggregate ops, search). Reverting via Option 2 would discard that. Option 1 (modify done stories in place) damages the audit trail. Option 3 alone is insufficient because most of the change is additive engineering, not de-scoping.
- The hybrid preserves all reusable work, makes the pivot first-class with proper acceptance criteria and review pipeline, and does only the minimum PRD/Architecture revision required to keep MVP scope honest.

### Effort and timeline

- **Epic 12 size:** ≈10 stories (12.0 spike + 12.1–12.10).
- **Comparable scale:** Epics 8 + 11 combined.
- **Wall-clock estimate:** 4–6 weeks of focused implementation, given Wave 1 and Wave 2 cannot overlap.

---

## 4. Detailed Change Proposals

### 4.1 New Epic 12 — EventStore-Fronted Architecture Pivot

To be added to `_bmad-output/planning-artifacts/epics.md` after Epic 11 / before Epic 10's section.

> ## Epic 12: EventStore-Fronted Architecture Pivot
>
> Hexalith.Parties moves to the canonical platform topology: clients submit all commands and queries to the EventStore service, which routes them via DAPR actor invocation to Parties-hosted actors. Parties no longer exposes its own REST or in-process MCP surface. The Admin Portal is rebuilt on Hexalith.FrontComposer and consumes EventStore queries. The EventStore Admin UI handles generic stream and event browsing.

**Story 12.0 — EventStore→Parties actor invocation feasibility spike**

> As a platform engineer,
> I want to verify that EventStore's `SubmitCommand`/`SubmitQuery` MediatR handlers route to Parties-hosted actors without changes to the EventStore submodule,
> So that the rest of Epic 12 can proceed without an upstream platform dependency.
>
> **Acceptance Criteria:**
> - **Given** a minimal AppHost wiring `eventstore` and a stub `parties` actor host project, **When** `POST /api/v1/commands` is submitted with `Domain="Parties"`, `CommandType="CreateParty"`, and a payload, **Then** the corresponding actor in `appId="parties"` receives the command and persists an event.
> - **Given** the same wiring, **When** `POST /api/v1/queries` is submitted with `Domain="Parties"`, **Then** the corresponding query actor responds.
> - **Given** the spike result, **Then** a written conclusion documents: (a) feasibility (yes/no), (b) any DAPR/EventStore configuration required, (c) any platform-level limitations discovered.
> - **Time-boxed to 2 working days.** A negative result blocks Epic 12 and triggers a follow-up sprint change targeting the EventStore submodule.

**Story 12.1 — AppHost recomposition**

> As a developer running Hexalith.Parties locally,
> I want the AppHost to start the EventStore service, EventStore Admin Server, EventStore Admin UI, Parties actor host, and Tenants service all sharing the same Redis state store and pub/sub,
> So that the local topology matches the canonical platform deployment shape.
>
> **Acceptance Criteria:**
> - **Given** `dotnet aspire run` on `Hexalith.Parties.AppHost`, **When** the application starts, **Then** resources `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants` are visible in the Aspire dashboard.
> - **Given** the running topology, **When** the EventStore Admin UI is opened, **Then** Parties events written by the actor host are visible in the stream browser.
> - **Given** the AppHost wiring, **Then** project references in `Hexalith.Parties.AppHost.csproj` include `Hexalith.EventStore`, `Hexalith.EventStore.Admin.Server.Host`, `Hexalith.EventStore.Admin.UI`, and `Hexalith.EventStore.Aspire` (with `IsAspireProjectResource="false"`).
> - **Given** DAPR components, **Then** `accesscontrol.yaml`, `accesscontrol.eventstore-admin.yaml`, `accesscontrol.tenants.yaml`, `accesscontrol.parties.yaml`, and `resiliency.yaml` exist under `src/Hexalith.Parties.AppHost/DaprComponents/`.
> - **Given** Keycloak is enabled, **Then** all four service projects (eventstore, eventstore-admin, parties, tenants) wire OIDC identically.

**Story 12.2 — Parties actor host**

> As a developer,
> I want `Hexalith.Parties` to become a thin actor host with no public REST or MCP surface,
> So that all client traffic flows through EventStore.
>
> **Acceptance Criteria:**
> - **Given** the Parties project, **Then** `Hexalith.Parties` has no controllers, no `MapMcp` registration, and no `MapControllers` call in `Program.cs`.
> - **Given** the Parties project, **Then** `MapActorsHandlers` and `MapDefaultEndpoints` (health) are the only public mappings.
> - **Given** the actor host, **Then** the DAPR sidecar is registered with `appId="parties"` and a Configuration CRD scoped to actor invocation only.
> - **Given** the GDPR notice (FR62 demoted), **Then** the warning is logged at `Warning` level at startup; no per-response header is added.
> - **Given** a fitness test, **Then** `ArchitecturalFitnessTests` asserts that `Hexalith.Parties` contains zero `[ApiController]` types and zero MCP tool registrations.

**Story 12.3 — Validation relocation; tenant validation owned by EventStore**

> As a developer,
> I want command and query validators to run inside the Parties actor host (or be discovered by EventStore's payload-type convention),
> And tenant authorization to be enforced exclusively at the EventStore gateway,
> So that the request path has a single, unambiguous validation and authorization boundary.
>
> **Acceptance Criteria:**
> - **Given** Parties FluentValidation validators (`CreatePartyValidator`, etc.), **Then** they are registered in the actor host's DI container and invoked before `Handle` runs in the actor.
> - **Given** EventStore's gateway, **Then** tenant validation uses EventStore's `ITenantValidator`/`IRbacValidator` plug-in surface; Parties does not contribute a tenant validator at the gateway level.
> - **Given** the request path, **Then** `ITenantAccessService` is removed from the command/query request path. Its projection-side use (Tenants event projection from Story 11.2) is preserved or refactored explicitly.
> - **Given** an unauthorized tenant, **When** a command is submitted, **Then** EventStore returns `403 Forbidden` with a problem-details body before the command reaches Parties.

**Story 12.4 — Server tier-1/tier-2 test rewrite**

> As a developer,
> I want server-side integration tests to drive through EventStore's gateway,
> So that the Parties actor host is verified through its actual production entry point.
>
> **Acceptance Criteria:**
> - **Given** the existing `tests/Hexalith.Parties.Tests/Controllers/**` suite, **Then** every assertion that hits a Parties REST URL is rewritten as a `POST /api/v1/commands` or `POST /api/v1/queries` against EventStore's gateway in a Tier-2 WebApplicationFactory rooted at the new topology.
> - **Given** the existing `tests/Hexalith.Parties.IntegrationTests/**` suite, **Then** every Tier-3 test against the AppHost topology asserts events end up in Redis with the EventStore stream key format.
> - **Given** prior coverage gaps, **Then** the Memories-backed search tests, key lifecycle tests, erasure tests, consent tests, and encryption tests retain at least the same scenario coverage post-rewrite.

**Story 12.5 — `Hexalith.Parties.Client` thin wrapper**

> As a consuming application developer,
> I want `Hexalith.Parties.Client` to be a typed thin wrapper over `Hexalith.EventStore.Client`,
> So that I can submit Parties commands and queries with strongly typed payloads without re-implementing EventStore client plumbing.
>
> **Acceptance Criteria:**
> - **Given** the rewritten `Hexalith.Parties.Client`, **Then** it references `Hexalith.EventStore.Client` and exposes typed methods (`CreatePartyAsync`, `UpdatePartyAsync`, `GetPartyAsync`, etc.) that serialize payloads and call `SubmitCommandAsync` / `SubmitQueryAsync` under the hood.
> - **Given** existing client tests, **Then** they are rewritten against `Hexalith.EventStore.Client.Testing` fakes.
> - **Given** the package surface, **Then** `Hexalith.Parties.Client` exposes no Parties REST URLs and no `HttpClient` factory tied to a Parties endpoint.

**Story 12.6 — `Hexalith.Parties.Mcp` thin host**

> As an AI agent,
> I want a Parties MCP host that proxies tool calls to EventStore commands and queries,
> So that Parties remains MCP-accessible after the in-process MCP wiring is removed.
>
> **Acceptance Criteria:**
> - **Given** a new project `src/Hexalith.Parties.Mcp`, **Then** it depends only on `Hexalith.Parties.Contracts`, `Hexalith.Parties.Client`, and `Hexalith.EventStore.Client` (and ServiceDefaults).
> - **Given** the Parties Mcp host, **Then** every previously registered MCP tool (`get_party`, `find_parties`, `create_party`, `update_party`, `delete_party`) is preserved in name and contract; tool implementations call `Hexalith.Parties.Client`.
> - **Given** the AppHost, **Then** `parties-mcp` is wired as a separate Aspire project resource that depends on `eventstore` and `parties`.
> - **Given** the new test project `tests/Hexalith.Parties.Mcp.Tests`, **Then** it covers the same scenarios as `tests/Hexalith.Parties.Tests/Mcp/*` previously did.

**Story 12.7 — Admin Portal rebuild on FrontComposer + EventStore queries**

> As an administrator,
> I want the Admin Portal to be a FrontComposer-based UI that reads via EventStore queries and uses the EventStore Admin UI for generic stream/event browsing,
> So that Parties admin operations follow the platform UX pattern and avoid duplicating EventStore Admin UI.
>
> **Acceptance Criteria:**
> - **Given** the rewritten `Hexalith.Parties.AdminPortal`, **Then** it integrates with `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts` and contributes Parties-domain views.
> - **Given** the portal navigation, **Then** generic event/stream browsing is delegated to the EventStore Admin UI via deep-links; Parties-specific views (party search, GDPR ops, consent, restriction, portability) live in the FrontComposer-based portal.
> - **Given** all data reads, **Then** they are issued through `POST /api/v1/queries` (via `Hexalith.Parties.Client`); no direct Parties REST calls remain.
> - **Given** Stories 10-1, 10-1-1, 10-2, **Then** their open scope is consolidated into this story; the original story files remain in `_bmad-output/implementation-artifacts/` for history.
> - **Given** a UX gap was identified, **Then** a UX spec is authored alongside the implementation under `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-XX.md`.

**Story 12.8 — Picker rewrite**

> As a developer embedding the Party Picker,
> I want the picker to query through EventStore,
> So that picker integration matches the platform contract.
>
> **Acceptance Criteria:**
> - **Given** the rewritten `Hexalith.Parties.Picker`, **Then** all data fetches use `Hexalith.Parties.Client` (over `Hexalith.EventStore.Client`).
> - **Given** picker integration tests, **Then** they cover the same selection scenarios as Story 10-3 originally.

**Story 12.9 — Sample + getting-started doc updates**

> As a new adopter,
> I want the Parties Sample and getting-started guide to demonstrate the EventStore-fronted topology,
> So that I onboard against the canonical platform pattern.
>
> **Acceptance Criteria:**
> - **Given** `samples/Hexalith.Parties.Sample`, **Then** it references only `Hexalith.Parties.Client` (and `ServiceDefaults`), not `Hexalith.Parties.Server` or any Parties REST URL.
> - **Given** `README.md` and `docs/getting-started`, **Then** all `curl` snippets and Aspire screenshots show EventStore as the entry point.

**Story 12.10 — Deployment validation + topology fitness rewrite**

> As an operator,
> I want deployment validation to assert the four-service topology,
> So that incorrect deployments fail fast.
>
> **Acceptance Criteria:**
> - **Given** `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`, **Then** assertions cover `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants` resources and shared statestore/pubsub.
> - **Given** `tests/Hexalith.Parties.DeployValidation.Tests/TenantsDeploymentValidationTests.cs`, **Then** validation rejects topologies missing any of the four services.
> - **Given** `ArchitecturalFitnessTests`, **Then** Parties contains zero controllers, zero MCP tool registrations, and the actor sidecar uses the dedicated `accesscontrol.parties.yaml` Configuration CRD.

### 4.2 PRD edits — `_bmad-output/planning-artifacts/prd.md`

(Concrete OLD→NEW wording to be authored by tech-writer in handoff. The required edits are:)

1. **FR60** — replace endpoint references with `POST /api/v1/commands` (`Domain="Parties"`, `CommandType="CreateParty"`) followed by `POST /api/v1/queries` (`QueryType="GetParty"`).
2. **FR62** — demote "non-dismissable GDPR compliance warning header on every response" to "GDPR notice logged at Warning level on service startup". Remove all language about per-response headers.
3. **NFR5** — re-anchor 30-second startup at the EventStore gateway readiness with the Parties actor host warm.
4. **FR39–FR43** — split into "gateway-level tenant authorization (EventStore-owned)" and "aggregate-level tenant isolation (Parties-owned)". Drop language that ties tenant authorization to Parties controllers.
5. **Epic 5 narrative** — restate as "thin Parties.Mcp host that calls EventStore" rather than in-process `MapMcp`.
6. **Epic 10 narrative** — restate as "FrontComposer-based admin portal that consumes EventStore queries; EventStore Admin UI handles generic stream/event browsing".

### 4.3 Architecture edits — `_bmad-output/planning-artifacts/architecture.md`

1. **Add ADR D14 — EventStore-fronted command/query routing for Parties.** Record: decision, rationale, alternatives considered (in-process actor host, gateway proxy, library embedding), consequences (auth boundary shift, validation relocation, tenant authorization ownership move, two-wave migration constraint).
2. **Add ADR D15 — Admin portal on FrontComposer; EventStore Admin UI handles generic stream browsing.** Record: decision, rationale, scope split (Parties-domain views vs generic events).
3. **Replace the REST API Design section** — delete the Parties endpoint catalog; replace with a payload catalog (CommandType / QueryType strings + payload schemas) usable against EventStore's generic gateway.
4. **Update Component Mapping table** — repoint `Multi-Tenancy (FR39-43)` from `PartiesController.cs (JWT extraction)` to EventStore's `ITenantValidator`/`IRbacValidator` pipeline.
5. **Replace the Architecture Stack diagram** — gateway (eventstore) → actor host (parties) → projections (parties) → admin pair (eventstore-admin + eventstore-admin-ui) → admin portal (parties-admin-portal on FrontComposer) → MCP host (parties-mcp).
6. **Update Solution Structure** — add `Hexalith.Parties.Mcp` and `Hexalith.Parties.Mcp.Tests`; document new role of `Hexalith.Parties` (actor host, no controllers).

### 4.4 Sprint status updates — `_bmad-output/implementation-artifacts/sprint-status.yaml`

Add Epic 12 entries with status `backlog` for all stories. Existing `done` and `in-progress` entries are unchanged. 10-1-1, 10-2, 10-3 are paused — annotated in the file header note that Epic 12 supersedes their consumer-facing scope.

---

## 5. Implementation Handoff

**Scope classification:** **Major** — fundamental replan with PM/Architect involvement.

### Recipients and responsibilities

- **Architect (Winston)** — author ADR D14 and ADR D15; replace the REST API Design section and the Component Mapping table; replace the Architecture Stack diagram; update Solution Structure.
- **Product Manager (John)** — apply PRD edits in §4.2 (FR60, FR62, NFR5, FR39–FR43, Epic 5 narrative, Epic 10 narrative).
- **Scrum Master (`bmad-create-story` + `bmad-create-epics-and-stories`)** — author Epic 12 in `epics.md` per §4.1; create individual story files under `_bmad-output/implementation-artifacts/12-*.md`; update `sprint-status.yaml` per §4.4.
- **Developer (Amelia)** — execute Story 12.0 spike first; on positive result, proceed Wave 1 (12.1–12.4) and then Wave 2 (12.5–12.10).
- **Test Architect (Murat / `bmad-tea`)** — author the test rewrite plan for Story 12.4 (server tests) and Story 12.10 (topology + deployment validation); confirm coverage parity post-rewrite.
- **UX Designer (Sally)** — author the UX spec for the rebuilt admin portal as part of Story 12.7.

### Success criteria

- Epic 12 closes with all 10 stories `done` and a positive Story 12.0 spike outcome.
- `dotnet aspire run` on `Hexalith.Parties.AppHost` starts eventstore + admin pair + parties actor host + tenants + (optional) keycloak; Parties events are visible in the EventStore Admin UI.
- `Hexalith.Parties` contains zero controllers and zero MCP tool registrations (architectural fitness test).
- All Tier-1/Tier-2/Tier-3 tests pass; coverage parity verified by the TEA review.
- The PRD and Architecture documents reflect §4.2 and §4.3 edits and pass `bmad-validate-prd`.
- Sample, getting-started, and admin portal exclusively use EventStore as the entry point.

### Sequencing

- **Gate:** Story 12.0 must produce a positive feasibility result before Wave 1 starts.
- **Wave 1 (server pivot):** 12.1 → 12.2 → 12.3 → 12.4. Cannot overlap with Wave 2.
- **Wave 2 (consumer migration):** 12.5 → 12.6 → 12.7 → 12.8 → 12.9 → 12.10. Starts only after Wave 1 lands.

### Out of scope

- Changes to the `Hexalith.EventStore` submodule itself. If Story 12.0 surfaces a platform-level limitation, a separate sprint change targeting that submodule is required.
- General-purpose de-scoping or feature removal beyond FR62 demotion and Epic 5/10 wording.
