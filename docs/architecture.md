# Architecture — Hexalith.Parties

This guide maps the public gateway, the Parties domain service and the responsibilities shared with Platform. Epic 8 governs changes that move existing behavior into a shared module. [Data models](data-models.md) · [API contracts](api-contracts.md) · [Source tree](source-tree-analysis.md)

## 1. Executive summary

Hexalith.Parties is a **party-management domain service** for people and organizations (contact channels + identifiers). It is built as an **event-sourced, CQRS** microservice that runs as an **EventStore SDK domain-service host behind the Hexalith.EventStore gateway**. Consumers never call the domain host directly — public command/query traffic enters EventStore (`POST /api/v1/commands` and `/api/v1/queries`, `Domain="party"`), which authenticates, validates tenancy/RBAC, and routes the work to the `parties` host over DAPR service invocation.

Parties owns party identity, contact data, consent, erasure policy and the meaning of its read models. EventStore owns routing, event persistence and delivery. Tenants owns membership. The typed .NET client and Blazor UI call the gateway. Applications can embed the Picker, AdminPortal and ConsumerPortal components or subscribe to published domain events.

| Team | Working boundary |
| --- | --- |
| Domain and client | Keep commands, queries and GDPR behavior in Parties. Agent operations are declared in Parties Contracts for enrollment in Hexalith.McpCli. The existing five-tool `parties-mcp` host is retained for compatibility. |
| Platform and release | Use the module manifest to start the local Platform services. The Parties AppHost supports existing UI, identity and rollback scenarios. Keep it until its owner has demonstrated topology, security, publication and rollback parity. |
| Privacy and deployment | The first committed request starts a pending erasure. Production custody, completion of erasure after restart, and key revocation across replicas each need evidence from the deployed configuration. See §8 for owners and acceptance criteria. |

If a task has no declared parent, use its touched files to find every affected migration’s rollback or acceptance evidence. Schedule it in the latest matching slot, or after 8.9 if none matches. The affected owners resolve unclear mappings before work starts. [Epic 8 architecture spine](../_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md)

| Attribute | Value |
|-----------|-------|
| Repository type | Monolith — single cohesive .NET solution (`Hexalith.Parties.slnx`), 15 projects under `src` and one sample project. Tests comprise 21 xUnit projects (18 runnable through the Parties script and three through Platform), six support projects and Playwright e2e |
| Primary language | C# / **.NET 11** (SDK pinned `11.0.100`) |
| Architecture style | Event sourcing + CQRS + EventStore SDK domain/projection/query handlers, gateway-fronted (EventStore) |
| Orchestration | Platform module CLI for integrated development. Aspire 17.0.0 for the retained Parties AppHost. |
| Eventing | DAPR pub/sub (Redis local; Kafka/RabbitMQ/Service Bus in prod) |
| Persistence | Event stream + projection state in DAPR state store (Redis); optional Hexalith.Memories for rich search |
| Distribution | NuGet packages + immutable container images; runtime deployment orchestration is externally owned |

---

## 2. Architecture pattern

```
            ┌──────────── PUBLIC ────────────┐         ┌──────── INTERNAL (DAPR mesh) ─────────┐
 consumer ─►│  Hexalith.EventStore gateway   │ invoke  │  parties (SDK domain-service host)    │
            │  /api/v1/commands  /queries    ├────────►│  /process · /query · /project*         │
            │  authn · tenant · RBAC · route │         │  /replay-state · /project/rebuild/*    │
            └────────────────────────────────┘         │  ┌──────────── write side ──────────┐ │
                      ▲   │ persist + publish           │  │ PartyAggregate.Handle(command)    │ │
                      │   ▼ (CloudEvents)               │  │ → emits IEventPayload events      │ │
            ┌─────────┴───────────┐                     │  └──────────────┬───────────────────┘ │
 subscriber │ DAPR pub/sub topic  │                     │     events      │                     │
   apps  ◄──│ {tenant}.parties.   │                     │  ┌──────────────▼ read side ───────┐ │
            │ events              │                     │  │ IDomainProjectionHandler folds  │ │
            └─────────────────────┘                     │  │ IDomainQueryHandler + stores     │ │
 .NET app ─► IPartiesQueryClient ──► gateway ──► query   │  └─────────────────────────────────┘ │
 Legacy AI ─► parties-mcp (5 tools) ─► typed client       └──────────────────────────────────────┘
```

**Why this shape:** EventStore is a generic command/query/event substrate; Parties plugs in as a *domain service* registered under `Domain="party"`. This keeps authentication, tenant validation, RBAC, routing, persistence, and event publishing in EventStore, while Parties owns only domain rules and read-model projections. The DAPR sidecar is deny-by-default and permits only the `eventstore` app ID to invoke the exact POST SDK surface: `/process`, `/query`, `/admin/operational-index-metadata`, `/project`, `/project/v2`, `/project/v2/reconcile`, `/replay-state`, `/project/rebuild/v1`, `/project/rebuild/shared/v1`, `/project/rebuild/stage/v1`, `/project/rebuild/commit/v1`, `/project/rebuild/abort/v1`, and `/project/rebuild/verify/v1`.

---

## 3. Technology stack

The table shows the default Parties versions. Package mode follows its selected
catalog for HTTP resilience and service discovery.

| Category | Technology | Version |
|----------|-----------|---------|
| Runtime | .NET | `net11.0` (SDK `11.0.100`, rollForward latestPatch) |
| Orchestration | .NET Aspire (`Aspire.Hosting` + hosting integrations) | `17.0.0` |
| Actors & pub/sub | DAPR client/actors/AspNetCore | `1.19.1` |
| | `CommunityToolkit.Aspire.Hosting.Dapr` | `17.0.0-preview.1.261120-0454` |
| Gateway/eventing | Hexalith.EventStore | package selected by `HexalithEventStoreVersion` in the effective MSBuild graph; source and installed acceptance remain separately verified |
| Tenancy | Hexalith.Tenants | package selected by `HexalithTenantsVersion` in the effective MSBuild graph; historical committed source gitlink `c9feedb6656d70c7271e851c844ee29bdfb28c3e` (`v5.8.0-131-gc9feedb6`), observed at Parties `7e73709992512d944f4950e6efb28c6132f9473c` on 2026-10-18; historical owner approvals and parity remain bound to their original identities |
| Validation | FluentValidation (+ DI ext.) | `12.2.0` |
| Mediation | MediatR | `15.0.0` |
| AuthN | Microsoft.AspNetCore.Authentication.JwtBearer | `11.0.0` |
| API docs | Microsoft.AspNetCore.OpenApi / Swashbuckle.SwaggerUI | `10.0.15` / `11.0.1` |
| MCP | ModelContextProtocol / .AspNetCore | `2.4.0` / `2.4.0` |
| UI | Microsoft.FluentUI.AspNetCore.Components | `5.1.0` |
| | Microsoft.AspNetCore.Components.CustomElements | `11.0.0` |
| Rich search (opt) | Hexalith.Memories.Client.Rest | package `4.1.4` by default |
| Observability | OpenTelemetry (exporter/hosting/instrumentation) | `1.21.0` core/hosting/exporter and ASP.NET Core/HTTP/runtime instrumentation |
| Resilience/discovery | Microsoft.Extensions.Http.Resilience / ServiceDiscovery | `11.0.1` |
| Versioning | MinVer (git-tag SemVer, prefix `v`) | `8.0.0` |
| Testing | xUnit v3 / Shouldly / NSubstitute / bunit / Testcontainers / YamlDotNet | `4.0.3` / `4.3.0` / `6.2.0` / `2.15.2` / `4.17.0`† / `18.1.0` |

The recorded Platform **v2.9.0** release uses the **Builds v6.9.1** catalog. In Release package mode, the Parties service, gateway test host, and attached lifecycle qualification suite select EventStore `5.6.0` by default. The compatibility suite selects `4.2.0` explicitly. The private Development catalog reader loads the prepared owner and adapter assemblies and checks its restored packages against the gateway test host’s evaluated `HexalithEventStoreVersion`. An explicit version takes precedence over the catalog default. Record the project, build mode and effective version when comparing results. [Package selection guards](../Directory.Build.targets) · [Private catalog reader](../src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogTool.cs) · [CI verification](ci.md#local-verification)

The earlier Tenants source identity `fcdcb4205a3f6e46f736cdd3e6f2b20ca2f241df`
(`v5.7.0-180-gfcdcb420`), recorded at Parties
`ccaf77399d92be696e44ecb8ab17acf77d97646a` on 2026-10-08, is historical.
Current selection does not recertify that identity's owner approvals or parity.

† `Testcontainers` is declared in `Directory.Packages.props` but referenced by **zero** test projects — container lifecycle is managed entirely by `Aspire.Hosting.Testing`. Treat it as dead config.

Solution-wide build settings (`Directory.Build.props`): `Nullable=enable`, `ImplicitUsings=enable`, **`TreatWarningsAsErrors=true`** (enforced by a build gate — see §11). Central package management via `Directory.Packages.props`.

The AppHost SDK and Aspire packages are aligned at `17.0.0`; DAPR client,
actors, and ASP.NET Core packages are aligned at `1.19.1`. This is the .NET SDK
package version; at the committed Builds revision the selected Dapr runtime catalog uses `1.19.2`.
The local mTLS bootstrap also accepts the `1.19.2` profile and selects its
matching digest-pinned control-plane image. An unset `DAPR_MTLS_RUNTIME_PROFILE`
uses the script’s `1.18.7` compatibility default. [mTLS bootstrap](../scripts/aspire-start-mtls.sh)

Historical root source identities follow the Story 8.10 final set approved by
Administrator / jpiquot on 2026-10-05 (authoritative record: the Story 8.3
reconciliation and I20 approval tables): AI.Tools `3f194e17`, Builds
`360a2b9c`, Commons `116d2681`, EventStore `865cd9e4` (`v3.113.0`),
FrontComposer `2cc8dd3a`, Memories `5b43fe2f`, PolymorphicSerializations
`98de6e01`, and Tenants `72b8e4f5`. The same decisions select EventStore
package `3.113.0`, approve I16 identity re-validation for this set (a receipt
counts as passing only when it was rerun at the stamped identity), and accept
the missing I13 runtime focus, forced-colors, and reduced-motion proof as the
named DW-111 deferral; I13 itself is not discharged.

Dated catalog and source
receipts retain their original identities and do not establish current
qualification. The [3.117.1 consumer receipt](../_bmad-output/implementation-artifacts/tests/eventstore-3.117.1-consumer-2026-10-08/README.md)
records historical downloaded package provenance and sidecar-channel API
verification. The [3.117.0 receipt](../_bmad-output/implementation-artifacts/tests/eventstore-package-upgrade-2026-10-08/README.md)
retains the earlier normal-release and consumer-build evidence. The October 5 source
approvals and parity receipts retain their historical scope. Integrated topology
acceptance and successful exact-source Parties full CI remain separate release
readiness gates.

---

## 4. System topology (Aspire)

**Platform development environment.** Run `dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json` from the Parties checkout. Platform composes the Parties, EventStore, Tenants and Memories services. Public traffic enters EventStore, and the Parties module declaration derives its allowed invocation routes from the domain sidecar ACL. [Module manifest](../module/hexalith.module-manifest.v2.json) · [Quick start and seven-service inventory](../README.md#quick-start)

**Retained Aspire environment.** The resources below belong to the Parties AppHost used for existing UI and identity scenarios and for migration rollback. Start it with `aspire run --apphost src/Hexalith.Parties.AppHost`. Before retiring the Parties AppHost under Story 8.8, verify that its successor preserves security, client behavior, publish targets and the recovery path. FrontComposer.AppHost is the planned successor for these UI scenarios unless the owners approve another host. [AppHost](../src/Hexalith.Parties.AppHost/Program.cs) · [Successor-host readiness](#successor-host-readiness) · [Epic 8 spine, I1a](../_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md)

| Resource | Kind | Start | Role |
|----------|------|-------|------|
| `eventstore` | project | auto | Public command/query gateway; routes `party` domain → `parties`/`process`. |
| `eventstore-admin` | project | auto | Admin server behind the admin UI. |
| `eventstore-admin-ui` | project | **explicit** | Blazor stream/event browser. |
| `parties` | project | auto | **This service** — EventStore SDK domain, projection, and query host (own DAPR sidecar). |
| `parties-mcp` | project | **explicit** | MCP tool host (no sidecar). |
| `tenants` | project | auto | Tenant lifecycle/membership/RBAC authority (own sidecar). |
| `memories` | project | conditional | Rich search backend (publish mode or `EnableMemoriesSearch=true`). |
| `sample` / `sample-blazor-ui` | project | conditional | EventStore subscriber sample (`EnableEventStoreSampleUi=true` or publish). |
| `keycloak` | container | conditional | Local IdP (run mode only; publish uses external `tache` realm). |
| `redis` | (DAPR backing) | — | **Not** an Aspire resource — provided by `dapr init` at `127.0.0.1:6379` for local development. |

The canonical Dapr ACL admits the gateway app ID `eventstore` in the `default` namespace and `public` trust domain. The isolated mTLS test profile adds the session suffix to app IDs and renders the session namespace while retaining the `public` trust domain and the same allowed operations. Record the canonical and suffixed gateway IDs, their namespaces and trust domain in the test evidence. For deployment evidence, compare every grant to the declaration at the selected revision. Record the caller app ID, namespace, trust domain, operation path, HTTP verb and allow/deny action, plus the global and caller deny defaults. Changes to the grants need the I1 owner decision. [Domain ACL](../src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml) · [Isolated profile](../src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs)

The Parties host authenticates Dapr’s application channel on its infrastructure callbacks. Canonical gateway operations also require a signed workload assertion scoped to the intended receiver and operation. Before cutover, verify that the successor accepts an authorized call. Record the refusal for each case separately, including absent or invalid channel credentials and workload assertions for the wrong caller, receiver or operation. [Host endpoint registration](../src/Hexalith.Parties/Program.cs) · [Workload verifier tests](../tests/Hexalith.Parties.IntegrationTests/Topology/PrivateCatalogWorkloadAuthenticationTests.cs)

`statestore` and `pubsub` DAPR components are created by `AddHexalithEventStore(...)` and wired into each sidecar. Local run-mode DAPR component YAML lives in `src/Hexalith.Parties.AppHost/DaprComponents/`; production Dapr components are owned by the external deployment orchestrator. The AppHost sets `EventStore:DomainService:AppId` to the corresponding DAPR app ID (`parties` or `tenants`). This keeps the SDK workload receiver audience aligned with the gateway invocation target; assembly-name fallback would reject valid internal assertions.

---

## 5. Write side — command processing

Entry: EventStore invokes SDK-owned `POST /process`; `Program.cs` registers the
contracts and projection assemblies with `AddEventStoreDomainService` and maps
the canonical surface with `UseEventStoreDomainService`.

1. The EventStore SDK resolves command types only from the registered contracts
   assembly, runs the registered **FluentValidation** validator (→
   `PartyCommandValidationRejected` on failure), and invokes the Parties
   aggregate with the protected current state and prior event stream.
2. **`PartyAggregate.ProcessAsync`** (`EventStoreAggregate<PartyState>`) rehydrates `PartyState` by replaying events through `Apply(...)`, then dispatches the command to the matching static `Handle(cmd, state)`.
3. **Handlers** (`src/Hexalith.Parties/Domain/PartyAggregate.cs`) enforce business rules in a guard cascade (null → id validity → idempotency no-op → type/required checks → emit events), then return a `DomainResult` of `IEventPayload`s (or rejection events). Success results may carry an enriched `PartyDetail` (`PartyCommandResult` / `CompositeCommandResult`).
4. EventStore **persists** the events to the aggregate stream and **publishes** them as CloudEvents (persist-then-publish; drain/recovery guarantees at-least-once).

**Command enum compatibility.** Parties clients keep their canonical camelCase,
string-enum JSON. Enum-valued command properties declare the built-in
`JsonStringEnumConverter<TEnum>` so the EventStore dispatcher accepts those names
and legacy numeric values, including nested composite commands and nullable
contact-channel updates. Unknown enum names remain invalid payloads; numeric
values still pass through the existing domain validators. The annotations belong
on command properties: shared enum definitions and event/state writers keep their
existing serialization shape. Ordinary `JsonSerializer.Serialize(command)` now
writes enum names for these command properties too. Property-level converters
also take precedence over converters registered in serializer options; tests or
clients requiring historical numeric payloads must construct those values
explicitly.

**Payload protection on the command path.** The Parties domain host calls `AddEventStoreDomainService` and then `AddParties`. The latter registers `EventStorePartyPayloadProtectionAdapter` as the host’s `IEventPayloadProtectionService`. It delegates party profile protection to `PartyPayloadProtectionService`. During replay, destruction of the profile key yields `json-redacted` for protected personal data. The separate gateway test host calls `AddEventStoreServer`. Before retiring the local adapter in Story 8.7, verify which service resolves in the Parties host and exercise the protected and redacted replay paths. [Domain host startup](../src/Hexalith.Parties/Program.cs) · [Service registration](../src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs) · [Gateway test host](../tests/Hexalith.Parties.EventStoreGateway.TestHost/Program.cs)

**Whole-payload redaction in SDK projections.** A `json-redacted` payload whose
root is an empty object (`{}`) or historical JSON `null` advances the projection
checkpoint without applying a domain event, regardless of whether its CLR type
can be resolved. Its processing-activity outcome is `Redacted`. This prevents an
empty object from becoming a default-valued lifecycle event. Nonempty objects
with field-level redaction retain their existing deserialization and application
behavior; ordinary `json` payloads retain their existing handling. Correcting
previously applied empty redacted events requires a full SDK projection rebuild;
incremental redelivery skips sequences already checkpointed. New unresolved first
deliveries initialize empty Detail and Processing slots through a create-only SDK
batch with a retained recovery manifest, then write the failed processing audit
optimistically. The fixed initialization identity permits retries after interrupted
installation or compaction even when event support or dispatch identity changes.
Detail stays unadvanced until a supported delivery folds the events. A projection
left with only a Processing slot by an earlier unresolved first delivery still
requires a rebuild: live delivery retains the coordinated-slot consistency guard.

**Independent identity-history snapshots.** `PartyState.HumanActorBindings` and
`HumanActorTransitions` contain personal actor and provenance history. Their finite
purpose custody is separate from profile-key encryption and erasure, so they do
not carry the profile `[PersonalData]` annotation. Snapshot custody implementations
must return the existing EventStore `SnapshotProtectionResult` from
`IIdentityHistoryCustody.ProtectSnapshotAsync`: current-version `Protected`
metadata with scheme `party-actor-history-v1`, and nonempty opaque ciphertext as
`byte[]` state. Ciphertext must be self-contained: nonnull `KeyAlias` or nonempty
compatibility flags are rejected because the restore seam receives only bytes.
Plain objects, copies of the profile snapshot, JSON bytes, and literal/base64
copies of the input are rejected before the adapter reports protected metadata.
These are structural checks using provider-supplied evidence, not proof of
encryption against a provider that falsely declares arbitrary bytes protected.
JSON serialization
stores these bytes as base64 inside the existing `identity-history-snapshot-v1`
marker/payload wrapper; restore decodes them before invoking custody. Old
unproven object payloads fail closed and require an owner-controlled rebuild
from qualified source evidence. This change does not rewrite stored snapshots.
The trusted custody provider remains responsible for real encryption, independent
keys, finite expiry and restore-safe lifecycle enforcement; metadata and unit
tests do not establish production custody qualification. Under `party-actor-retention-v1`, profile erasure immediately ends current human eligibility. An authorized attribution query may read the minimal opaque history under independent custody only before 365 fixed days have elapsed from the original binding-effective time. Restore must check the independently durable lifecycle before decryption and preserve that original expiry. Production history stays disabled pending custody, restore and owner compatibility evidence. [Actor-history retention policy](../_bmad-output/planning-artifacts/actor-history-retention-policy-v1.md)

The underlying write guard accepts typed `PartyState` only. EventStore's
`SnapshotManager` supplies folded JSON; the adapter now restores complete modeled
state through a strict codec before profile/history protection. Persisted reads
require valid protection evidence, while trusted post-SDK replay preserves typed
state and its collections. Actual SDK capture/read-back tests qualify this local
integration with synthetic custody; production custody and expiry remain separate.

For an otherwise valid ordinary `CreateParty` or `CreatePartyComposite`, the aggregate checks `PartyIdentifier.IsValid` and whether state already proves creation. A retry for an already-created ID emits no new events, including when creation evidence survives erasure. The simple command returns `DomainResult.NoOp()` and the composite returns empty result lists. [Create handlers](../src/Hexalith.Parties/Domain/PartyAggregate.cs)

Business-rule highlights: state-driven idempotency (`DomainResult.NoOp`), the erasure state machine (`ErasurePending → KeyDestroyed → Verified → Erased`), Art.18 restriction guards (with an Art.18(3) carve-out for consent ops), deterministic `ConsentId`, and the composite two-phase (validate-all-then-emit) with a `MaxSubOperations` cap.

---

## 6. Read side — projections (CQRS)

EventStore delivers events to the Parties detail and index projection handlers. Live delivery loads persisted state, skips sequences the projection has applied and folds new events after its checkpoint. The detail handler requests a full rebuild when only one of its coordinated detail and processing records survives. A full rebuild starts with an empty candidate and replays complete history. Compare the rebuilt model with the live model for the same history before retiring a projection path. [Detail handler](../src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs) · [Index handler](../src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs)

**Read models** ([data-models.md §5](data-models.md)):

- **Detail** — per party (`readmodel:{tenant}:party:…:detail`), materialises `PartyDetail` (plus processing-activity slot).
- **Index** — per tenant shared index (`…:index`), a `Dictionary<partyId, PartyIndexEntry>` for list/search. Erased parties are removed from the index.

The Memories mapping ledger tracks external search entries that still need cleanup. `PartyMemoryUnitMappingStore` uses `IReadModelStore` with ETags and retries. Cleanup subtracts its observed entries from fresh state, preserving concurrent writes. A failed read leaves cleanup unverified. Projection rebuilds must preserve this ledger. Parties and EventStore owners still need to approve the storage interface and its consistency tests for migration. [Mapping store](../src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs) · [Approval matrix](../_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md)

**Query path:** EventStore gateway routes handler-advertised query types to Parties `POST /query` (`IDomainQueryHandler` implementations such as `PartyDetailQueryHandler` / `PartyIndexQueryHandler` / GDPR variants). Wire query-type names remain the historical `PartyDetailProjectionQueryActor.*` / `PartyIndexProjectionQueryActor.*` constants. Successful projection reads carry `ProjectionFreshnessMetadata` using SDK-native `Current`/`Stale`/`Unavailable`. Safe last-known responses during store outages report `Stale` + `Metadata.IsDegraded`. Search defaults to `LocalFuzzyPartySearchProvider`; when `Parties:MemoriesSearch:Enabled=true` it swaps to `MemoriesPartySearchService` — see [memories-backed-party-search.md](memories-backed-party-search.md).

**Reads during a store outage.** Another replica may already have erased the party. If the canonical store cannot answer, the query service uses the following rules.

| Cached state | Query response |
| --- | --- |
| Active party detail, or a nonempty or unverified index | Evict the entry and return a failure without a payload or freshness metadata. |
| Erased party detail | Redact it again before returning any fields the query permits. |
| Absent party detail | Preserve the query’s existing absence outcome. |
| Confirmed empty index with intact tracking state | Return an empty page with degraded freshness. |
| Unavailable processing record for an export or Article 30 query | First verify that the party detail can be used. Then use cached processing records if available, or an empty degraded list. |

If the service denies the read, clear any previously displayed personal data for that read and show the failure. [Query implementation](../src/Hexalith.Parties/Queries/PartySdkQueryService.cs) · [Cross-replica erasure tests](../tests/Hexalith.Parties.Tests/Gateway/PartySdkCrossInstanceErasureTests.cs)

**Admin list and search.** When the server returns a valid empty page, clear the displayed rows and superseded detail. Use the counts and paging from that response, even if it reports degraded freshness. Reuse of a previous page requires the server to confirm that its representation validator still matches for the same authorized caller, tenant, operation, query, filters, sort and page. A degradation warning alone provides no such confirmation.

Story 8.9 still needs to reconcile this rule with the grid and its tests. Today the component clears rows on failure, but can retain old rows and counts after an empty success with degradation metadata. A component test expects a previous name to remain visible even after a different search. The typed list/search client supplies empty portal metadata on success. Verify denial, a new empty result and an explicitly confirmed unchanged result for both list and search before claiming parity. [Grid](../src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor) · [Component tests](../tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs) · [Portal adapters](../src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs)

### Actor inventory

| Actor | Project | Responsibility |
|-------|---------|----------------|
| `PartyKeyRetryActor` | Security | GDPR key-creation retry scheduling |

Dapr projection/query actors and `ProjectionRebuildService` were retired in Story 8.6 after SDK parity.

---

## 7. Eventing & subscribers

Events are published by EventStore as **CloudEvents 1.0**, topic `{tenant}.parties.events`, dead-letter `deadletter.{tenant}.parties.events`, id `{correlationId}:{sequenceNumber}`. Delivery is **at least once**. A subscriber acknowledges success after it has durably completed the work, recognized a previously completed duplicate, or durably handed the operation and its identity to a named retry owner. Subscribers may also acknowledge events explicitly outside their obligations. If transient failure prevents these outcomes, return a retryable failure or Dapr’s explicit `RETRY` result. The retry owner reconciles exhausted deliveries through the configured dead-letter policy. [Delivery policy](event-publishing.md#dead-letter-topic-setup)

- **[event-subscribing.md](event-subscribing.md)** — wire format, idempotency, ordering per broker.
- **[event-handler-patterns.md](event-handler-patterns.md)** — per-event handler patterns, the **mandatory `PartyErased` handler**, dangling-reference cleanup, tolerant deserialization.
- **[event-publishing.md](event-publishing.md)** — production broker config (Kafka/RabbitMQ/Service Bus), retry/circuit-breaker policies, dead letters, access control.

Parties is the **publisher only**; it does not consume its own party events. It *does* subscribe to **Hexalith.Tenants** events (`POST /tenants/events`, topic `system.tenants.events`) to maintain its local tenant-access projection.

---

## 8. Security & tenancy

EventStore owns public authn, tenant validation, and RBAC. Within `parties` (defense-in-depth):

- **JWT bearer** auth (`Authentication:JwtBearer`): OIDC `Authority` (prod) or symmetric `SigningKey` (dev, ≥32 chars); `MapInboundClaims=false`; RFC 9457 problem+json challenges. A single `"Admin"` authorization policy. `PartiesClaimsTransformation` normalises tenant claims to `eventstore:tenant`. The G7/G9 decision routes shared transformation to EventStore.Authentication with Commons identifier helpers. The Parties Authentication library remains the rollback path pending consumer parity and recorded retirement approval. [G7/G9 owner receipt](../_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#g7g9-owner-api-delivery-receipt--2026-07-31)
- **Tenant access projection** (`ITenantAccessService`, `src/Hexalith.Parties/Authorization/`): fed by Tenants pub/sub events; **fails closed**; role mapping `TenantReader`/`Contributor`/`Owner` → Read/Write/Admin. Eventually consistent (after a restart, in-memory projection starts empty and denies `UnknownTenant` until replay). **Used projection-side only — never on the gateway request path** (pinned by a fitness test). See [tenant-access-projection.md](tenant-access-projection.md).
- **Consumer own-data scope** (`parties-ui` BFF): a Consumer reaches `/me*` only with the `Consumer` policy and exactly one non-empty `party_id` claim. Runtime profile, consent, export, erasure, and processing-transparency operations use ConsumerPortal-owned caller-id-free ports (`IConsumerProfileDataClient`, `IConsumerProfileEditClient`, `IConsumerConsentClient`, `IConsumerPrivacyExportClient`, `IConsumerPrivacyErasureClient`, and `IConsumerPrivacyProcessingClient`) that the UI host adapts to `ISelfScopedPartiesClient`. That self-scoped accessor resolves the bound `party_id` fail-closed, injects it into typed client calls, and exposes no list/search or caller-supplied party-id surface.
- **Consumer identity binding provisioning** (`src/Hexalith.Parties.UI/IdentityBinding/`): Story 4.2 implemented an admin-link provisioning service outside the Parties event stream. The MVP store and IdP attribute adapter are in-memory host services; the runtime source of truth remains the IdP-emitted `party_id` claim. The service records bounded operator audit metadata, version-checks updates, rolls back store state when IdP attribute writes fail, and detects store/IdP drift without granting runtime access from the store alone.
- **Middleware pipeline** (`Program.cs`, order-sensitive): `CorrelationId → MvpComplianceWarning → ExceptionHandler → DegradedResponse → AuthN → AuthZ → CloudEvents`.

The standalone UI’s token relay still needs a real-gateway check for own-record reads and updates, expired sessions and isolation between two signed-in users. For new consent and erasure records, the privacy owner must approve an opaque actor identifier from authenticated gateway metadata. Historical records with missing attribution retain that uncertainty on replay. [Identity and privacy actions](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md)

### GDPR / crypto-shredding

`Hexalith.Parties.Security` implements per-party **AES-256-GCM** field encryption (keyed off `[PersonalData]`), key lifecycle/rotation, erasure verification, and an append-only key-operation audit. Two independent switches:

- `Parties:Compliance:GdprFeaturesActive` (default **false**) → only the MVP warning (startup log + `X-Hexalith-Parties-Mvp-Compliance-Warning` header).
- `Parties:CryptoShredding:IsEnabled` (default **true**) → the encryption/erasure feature itself.

**Production custody is unverified (GATE-KMS).** The default `LocalDevKeyStorageBackend` holds keys in process memory. A host restart loses those keys and makes previously encrypted personal data unreadable. A durable provider would hold authoritative secrets and wrapping metadata. Before adopting or replacing one, the custody and storage owners must record the applicable I19 disposition and consistency checks in the I20 ledger alongside the I8 evidence. [Provider contract](../src/Hexalith.Parties.Contracts/Security/IKeyStorageBackend.cs)

Before processing real regulated EU personal data, the deployment owner must show that the host refuses to start with ephemeral key storage outside Development. With the selected durable per-party key provider, verify that a synthetic protected record decrypts and replays after host replacement and on a second replica. [Key registration](../src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs) · [PRD deployment gate](../_bmad-output/planning-artifacts/parties-ui-prd.md#deployment-gates)

**Erasure completion.** Parties and the deployment owner must identify the consumer that processes the initial request and the owner who retries it. One request for a synthetic party must reach key destruction, persisted certification, cleanup, committed terminal events and redacted reads. Exercise cancellation before destruction and retry after a failed append or restart. The consumer UI offers cancellation while erasure is pending. It shows erasure as started during destruction and verification, then as permanent once the state is `Erased`. [Consumer privacy page](../src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor)

**Replica revocation.** The security and provider owners must show that every replica refuses retired key versions after certified destruction. Include a lookup already in flight when another replica destroys the key. [Sprint decision and owners](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md)


---

## 9. Cross-cutting concerns

The hosts consume `Hexalith.Commons.ServiceDefaults` directly for **OpenTelemetry** (logs/metrics/traces; OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; tracing source `"Hexalith.Parties"`), **structured JSON console logging**, **standard HTTP resilience**, **service discovery**, and the `/health` `/alive` `/ready` endpoints.

`DegradedResponseMiddleware` surfaces `X-Service-Degraded` / `X-Stale-Data-Age` on degraded GETs.

**Logging discipline (privacy):** event payloads are never logged; projection logs carry only projection name + coarse counts, never party/tenant/actor ids (AC7). Personal data is `[PersonalData]`-tagged and kept out of logs.

### Successor-host readiness

`AddPartiesDaprHealthChecks` tags the sidecar, state store and Tenants integration for `/ready`. Disabled Tenants integration reports healthy. Failure of an enabled Tenants probe, the sidecar or state store makes the host unready. Pub/sub, Memories search and SDK read-model degradation affect `/health` independently. Before retiring the Parties AppHost, test the successor topology with Tenants enabled and disabled and verify that projection-only degradation preserves readiness. [Health registrations](../src/Hexalith.Parties/HealthChecks/PartiesHealthCheckExtensions.cs) · [Tenants probe](../src/Hexalith.Parties/HealthChecks/TenantsIntegrationHealthCheck.cs)

---

## 10. Source tree & development

The annotated tree is in **[source-tree-analysis.md](source-tree-analysis.md)**; the project inventory in **[component-inventory.md](component-inventory.md)**; setup/build/test in **[development-guide.md](development-guide.md)**. At a glance:

```
src/
  Hexalith.Parties.AppHost/        # Current Aspire dev/rollback entry point; retire after G8 platform-host parity
  Hexalith.Parties.Aspire/         # platform composition helper; no server package dependency
  # Adopter-facing
  Hexalith.Parties.Client/         # typed EventStore gateway client
  Hexalith.Parties.Contracts/      # commands, events, value objects, read models (no infra deps)
  Hexalith.Parties.Picker/         # embeddable Blazor/custom-element party picker
  Hexalith.Parties.AdminPortal/    # protected Admin records/GDPR RCL
  Hexalith.Parties.ConsumerPortal/ # protected Consumer /me self-service RCL
  Hexalith.Parties.UI/             # Blazor Server browser UI/BFF (`parties-ui`)
  Hexalith.Parties.Mcp/            # parties-mcp host (5 tools)
  # Internal (domain-service-host private)
  Hexalith.Parties.Authentication/ # internal claims-transformation library; packable but not adopter-facing, retained as the G7/G9 rollback surface until 8.8
  Hexalith.Parties/                # SDK domain-service host, PartyAggregate, DI, middleware, query adapters
  Hexalith.Parties.Projections/    # SDK projection handlers/folds + read-model support
  Hexalith.Parties.Security/       # crypto-shredding / key management / erasure
  Hexalith.Parties.Testing/        # test utilities
tests/    # xUnit v3 projects (unit, integration, topology, CI)
samples/  # Hexalith.Parties.Sample — subscriber reference
scripts/  # local test runner, package validation, and Parties container publish helper
```

---

## 11. Testing & CI

- **Test runners.** The repository has 21 xUnit v3 suites. The Parties script owns 18 runnable suites in its unit, integration, topology and CI lanes. Platform runs the integration, lifecycle and attached lifecycle qualification suites with `hexalith-module test`. The legacy attached lifecycle project forwards builds to qualification and counts among the six support projects, alongside three support hosts and two artifact projects. Playwright runs separately. The gateway end-to-end test skips outside its enabled profile and requires an available fixture on enabled runs. [Runner inventory](../scripts/test.ps1)
- **CI and release.** Parties CI builds the standalone solution in Release using published libraries and pinned source revisions for runtime dependencies. Its jobs include public-package integration, EventStore package compatibility, the published catalog runtime, UI accessibility and Platform declaration checks. Release checks the exact `main` revision for successful push CI and the independent range signoff before protected approval. The shared CiCd pipeline migration is pending hosted acceptance. [CI workflow](../.github/workflows/ci.yml) · [Release workflow](../.github/workflows/release.yml) · [Adoption status](ci.md#adoption-and-releases)
- **Qualification.** A successful package restore establishes dependency selection. The package integration check exercises a gateway create/query, persisted events and cleanup. Record hosted results against the exact release source. The six passing Epic 8 closure lanes are evidence for their November 2 revision. A new deletion that relies on a retired check needs an approved product test run against the selected identities. For a dependency rollback, record both the forward and restored revisions with their respective results. [Integration test](../tests/Hexalith.Parties.Platform.IntegrationTests/PartyEnvironmentTests.cs) · [Closure record](../_bmad-output/implementation-artifacts/tests/test-summary.md)

---

## 12. Deployment

This repository publishes only the Parties-owned container images through GitHub Actions:

- `registry.hexalith.com/parties`
- `registry.hexalith.com/parties-mcp`
- `registry.hexalith.com/parties-ui`

Runtime deployment orchestration is externally owned and must consume immutable image tags from Zot. See [deployment-guide.md](deployment-guide.md) and [ci.md](ci.md).

---

## 13. Architectural decisions & gotchas

- **Single aggregate, discriminated** — `Party` with `PartyType` (Person/Organization), not a class hierarchy.
- **Gateway-fronted SDK host** — no public API on the domain host; the DAPR ACL is deny-by-default and only `eventstore` may call the exact POST `/process`, `/query`, projection, replay, metadata, and staged-rebuild routes.
- **Rejection events are persisted and replayed.** Their `PartyState.Apply` handlers leave state unchanged.
- **Projection replay** — live handlers resume from persisted checkpoints. Full rebuilds replay complete history from empty candidate state (§6).
- **Type resolution is allowlisted to the contracts assembly** — never `Type.GetType` on wire input.
- **Submodules referenced by path** — root-declared source dependencies are checked out under `references/` and resolved by MSBuild root properties rather than nested recursive submodules. The baseline build needs `Hexalith.Builds`, `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.PolymorphicSerializations`, and `Hexalith.Tenants`; `Hexalith.Memories` is optional for rich search. A fresh clone must initialize root-repository submodules only, **never** `--recursive`.
- **Replay dispatch.** A Parties compatibility test requires rejection-event `Apply` overloads to precede success handlers in source. For persisted event names, the selected EventStore resolver tries exact full name, exact short name, then the longest suffix starting at a type-name boundary. It reports ambiguity when the winning key has multiple candidates. [Ordering test](../tests/Hexalith.Parties.Tests/FitnessTests/PartyStateApplyOrderingFitnessTests.cs) · [Selected resolver](https://github.com/Hexalith/Hexalith.EventStore/blob/7435254cc1a148031224eebe1aafd53a8659b2cd/src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs)

- **The legacy Parties MCP host exposes exactly 5 tools** (`create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`); **`get_party_name_at` does not exist** (temporal name-as-of queries are reserved, not implemented). The README + `getting-started.md` previously listed a phantom 6th `get_party_name_at` tool — **corrected in this rescan.**
- **SDK rebuild/live agreement is pinned:** shared and detail read-model key formulas are guarded by projection/rebuild regression coverage.
- **AdminPortal erasure verification is implemented through existing gateway seams:** `GetErasureCertificateAsync` posts the `GetErasureCertificate` SDK domain query; `RetryErasureVerificationAsync` posts the additive `RetryErasureVerification` command and reuses the Parties erasure orchestrator. No public `parties` API was added; the internal `/query` route remains EventStore-only.
- **Contract-verification dispatch.** At revision `42b27f2e`, the caller accepts `contract_requiring_verification_published`. The current caller and pinned reusable workflow define no Pact verification job. The earlier skipped `contract-test` behavior belongs to the legacy pipeline. See §11 for current qualification. [Legacy CI reference](ci-legacy-reference.md) · [Current caller](../.github/workflows/ci.yml)


Recovery integration boundaries (2026-10-23): the source-selected EventStore SDK exposes an
explicit nonexpiring legacy recovery capability. Parties uses it only for its detail and
processing projections, whose original normal batches have no TTL/deadline. Generic recovery
continues to refuse missing-manifest v1 batches because their original relative TTL cannot be
reconstructed. Committed owned envelopes are compacted with CAS; a content-free final anchor
survives interrupted recovery, and superseding erasure values are preserved. Package 3.127.0
continues to use the existing consumer path pending publication/adoption of this additive API.

`RetryErasureVerification.RecoverKeyDestructionEvidence` defaults to false. Explicit recovery
of missing or ambiguous matching evidence reruns the existing authoritative key deletion,
read-back and audit, validates a fresh scoped Verified certificate, and persists it before
cleanup. Cancellation and certificate persistence failures cannot certify completion. Valid
certificates remain unchanged by ordinary cleanup retries.

SDK snapshot capture supplies Web JSON to `EventStorePartyPayloadProtectionAdapter`, which
uses `PartyStateSnapshotCodec` to restore every modeled private-set field and getter
collection before independent profile/history protection. The codec rejects duplicate,
unknown, partial, oversized or lossy shapes; Web numeric enums and Parties string enums are
accepted only through complete round-trip agreement (allowing null omission). The underlying
serialized-history protection guard remains active. Synthetic custody tests qualify serialized
capture, recreated-store restore and command behavior, with production custody/expiry gates
remaining external.

Persisted raw retained-history snapshots are unreadable under Legacy/Unprotected metadata;
independent history requires its purpose wrapper and custody. Codec hydration of plain folded
state is reserved for the trusted post-SDK domain replay handoff after that persisted-read gate.

Protected profile reads validate the complete personal-data policy before any profile key read.
A detached preview removes encrypted markers and restores modeled state; the existing writer
inspector rejects personal values left in plaintext, including collection values and natural-person
organization strings. A ciphertext marker elsewhere cannot authorize an unencrypted sensitive leaf.

The runner-owned `Hexalith.Parties.Platform.IntegrationTests` project adds a separate
Platform lane. Run it through `hexalith-module test`; it requires the validated
environment descriptor and private credential channel and never starts topology.

The `Hexalith.Parties.EventStoreExtensions` package carries the existing owner-declared canonical intent adapters for catalog composition. It is included in the public release inventory and remains nonpublishable as an application. Package delivery does not activate a catalog; each participating host must independently qualify and commit the bound runtime generation.

The catalog-enabled release cohort requires trusted catalog enrollment before a service becomes ready or accepts catalog dispatch. The run-only packaged test profile prepares the exact selected public packages through `scripts/prepare-aspire-runtime-hosts.sh` and supplies a private Development catalog. Production composition must provide the SDK authority and trust-provider configuration.

Public browser calls require the signed `eventstore:surface=ui` claim; agent calls use `agent` when the declared operation permits that surface. The local confidential `hexalith-parties-ui` OAuth client emits the UI claim alongside its existing audience and authorization claims. Configure the corresponding explicit client claim in an external identity provider; roles and request headers do not determine the caller surface.
