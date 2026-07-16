# Hexalith.Parties — Domain-Focus Refactoring Analysis & Action Plan

> Analysis date: 2026-07-06 · rebased on origin/main `9b4b693`; dependency pins bumped to
> EventStore `63f8acf`, FrontComposer `f61c6a8`, Memories `1b1db8d`, Tenants `9c7aa5c`,
> Builds `6fcd894`
> Method: six parallel deep-dive reviews (host/SDK, contracts, security, UI, tests, dependencies/hygiene)
> plus restore/build probes against the then-pinned submodules.
> Scope: keep Hexalith.Parties strictly domain-focused; move reusable/cross-cutting code to
> Hexalith.Commons / Hexalith.EventStore / Hexalith.FrontComposer (or other technical modules).
> **No changes have been implemented — this is the analysis and roadmap only.**
> Supersedes the prior version of this file (written at `88d984b`; `src/`, `tests/`, `samples/` are
> unchanged since, so findings were re-verified rather than re-discovered).

---

## 1. Executive Summary

Hexalith.Parties has a **solid, well-tested domain core** — a pure 1,518-line `PartyAggregate`
(24 command handlers, zero infrastructure imports), clean projection fold functions (630 loc),
rich GDPR workflows (erasure, restriction, consent, portability), and a genuinely domain-specific
Memories-backed search subsystem (~3,000 loc). Around that core, however, the module has grown a
**parallel platform** that re-implements almost everything the technical modules already provide:

1. **The EventStore domain-service SDK is bypassed entirely.** Zero usages of
   `AddEventStoreDomainService` / `UseEventStoreDomainService`, `IDomainQueryHandler`,
   `IDomainProjectionHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec` —
   all verified present in the pinned `references/Hexalith.EventStore`. Instead the host ships a
   614-line reflection-based `/process` invoker, two hand-rolled Dapr projection actors
   (736 + 596 loc), two query actors (572 + 296 loc), a 633-line rebuild service that issues raw
   HTTP calls to the Dapr actor state API, duplicate health checks, duplicate correlation
   middleware, duplicate JWT config, and duplicate exception handlers. **~5,600–5,900 of the
   13,588 loc in the host lane is deletable platform plumbing.**
2. **A generic crypto-shredding engine lives inside the domain.** ~2,800 loc in
   `Hexalith.Parties.Security` + ~400 loc of contracts implement key management, field-level
   AES-256-GCM payload encryption, rotation, audit, circuit breaking and erasure verification
   behind EventStore's own `IEventPayloadProtectionService` hook — which the platform defines but
   nobody implements. Only the *policy* (which Party fields are personal data, when keys are
   created, the erasure workflow) is Parties-specific. Extraction target: a new
   **`Hexalith.EventStore.PayloadProtection`** package pair.
3. **The UI re-implements FrontComposer.** `Hexalith.Parties.UI` is actually the containerized
   BFF web host and carries ~840 loc of SignalR subscription / polling fallback / optimistic
   reconciliation / degraded-state plumbing that `FrontComposer.Shell` ships; **neither portal
   renders a single FrontComposer component** (`<Fc*` grep = 0); portal stylesheets contain
   **98 forbidden legacy v4/FAST design-token occurrences**; the custom Picker combobox
   (705-loc raw-HTML ARIA) duplicates Fluent UI V5 `FluentAutocomplete` and is **never registered
   at runtime**. A 1,453-loc E2E fixture is compiled into the production container.
4. **`Hexalith.Parties.Client` re-implements the EventStore gateway protocol** (including a
   private copy of `SubmitCommandRequest` duplicated twice) instead of using
   `IEventStoreGatewayClient`. **`Hexalith.Parties.Mcp` bypasses `Hexalith.FrontComposer.Mcp`**
   and has no tenant/command policy gating. `Hexalith.Parties.Authentication` (90 loc) is pure
   platform code (normalizes IdP claims into `eventstore:tenant`).
5. **Systemic identifier-rule violation.** 31 `Guid.TryParse` validations on aggregate IDs
   (all 18 validators + the aggregate itself at `PartyAggregate.cs:41,561`) — **ULID-formatted
   IDs are rejected today** — and GUIDs are *minted as persisted domain IDs* in the MCP tools and
   the AdminPortal create form. The ULID toolkit (`ByteAether.Ulid`,
   `Hexalith.Commons.UniqueIds`) is already on the dependency graph but unused.
6. **The build/CI baseline is red** (§2): `ModelContextProtocol` now uses the corrected
   `Update` pin, but package-mode restore still depends on several
   `Hexalith.Commons.*` / `Hexalith.Tenants.*` packages being published. Current upstream also
   defaults empty/Debug configurations to package mode. **This must be stabilized before any
   refactoring starts.**
7. **Dead weight**: MediatR (2 projects), `Dapr.Client`+`Dapr.Actors` in `Parties.Server`,
   `Aspire.Hosting.Redis` in the AppHost, `Hexalith.PolymorphicSerializations` (submodule + 2
   .slnx entries, zero consumers), dead `FrontComposer.Mcp`/`Parties.Client`/`FrontComposer.Shell`
   references in UI projects, 25 committed `*.csproj.lscache` files, a tracked `.aspire-run/*.pid`.
8. **Four runtime defects found in passing** (§9): broken GDPR Art. 20 JSON export (nonexistent
   JS function), the unwired Party picker, partially unstyled consumer pages (scoped-CSS
   copy-paste), and the GUID domain-ID minting.

**Estimated net effect**: `src/` shrinks by ~40–45% (≈13–15k loc deleted or moved to technical
modules), the project count goes from 15 to ~10, and the module converges on the platform
contract: aggregate + contracts + projection/query handlers + validators + domain UI + domain
clients + a thin AppHost. **Critical sequencing constraint**: ~12 platform gaps (§4.6) must be
filled in Commons/EventStore/FrontComposer *first*, and the package-publishing story must be
fixed, before the corresponding Parties code can be deleted.

---

## 2. Baseline Build/Test/CI Status (verified today)

| Lane | Command | Result |
|---|---|---|
| Source-referenced build | `dotnet build Hexalith.Parties.slnx -c Release -p:UseHexalithProjectReferences=true -p:TreatWarningsAsErrors=false` | ✅ **GREEN** — 0 errors, 0 warnings, 22 s |
| Package-mode restore (CI default) | `dotnet restore Hexalith.Parties.slnx` + `-c Release` semantics | ❌ `4148af9` fixed the duplicate `ModelContextProtocol` central-package pin (`Include`→`Update`), but package mode still fails until `Hexalith.Commons.ServiceDefaults`, `Hexalith.Commons.Http`, `Hexalith.Tenants.Client`, and `Hexalith.Tenants.Testing` are published or consumed from source |
| Default restore/build mode | `dotnet restore` + `dotnet build -c Release --no-restore` | ❌ now consistently package-mode by default after `57ace9d`/`fd94736`; CI/local builds must either publish the missing packages or pass `-p:UseHexalithProjectReferences=true` explicitly |
| GitHub Actions "Test Pipeline" | last 5 runs on main | ❌ all failure/cancelled |

This revision is rebased on `origin/main` `9b4b693`. The upstream `4148af9` MCP pin fix is kept;
the `57ace9d`/`fd94736` package-mode default remains the main restore/build risk until the
dependency-publishing story is settled.

**Additional CI drift** (`.github/workflows/test.yml`): the 4 test shards enumerate 13 of 15 test
projects — **`Hexalith.Parties.Authentication.Tests` and `Hexalith.Parties.ConsumerPortal.Tests`
are never run in CI**; jobs pin `dotnet-version: 10.0.302` while `global.json` demands
`10.0.302`; `scripts/test.ps1` omits ConsumerPortal.Tests from every lane.

**Baseline remediation (Phase 0, before any refactor)**: keep the `Update`-style MCP pin, make
CI restore+build configuration-consistent, and decide the package story: either publish
`Hexalith.Commons.Http/ServiceDefaults` + `Hexalith.Tenants.Client/Testing` to the feed, or
declare CI source-mode with `UseHexalithProjectReferences=true` until they ship. Then re-baseline:
run each test project individually (per repo rules) and record the green list.

---

## 3. Project / Module Inventory

Totals: 15 src projects (~38,400 loc), 1 sample, 15 test projects (1,893 facts/theories) + 75
Playwright e2e tests. Submodules pinned in this revision: EventStore v3.43.0-1-g63f8acf0,
Commons v2.26.0-3, Builds v4.16.3-26-g6fcd894, FrontComposer v1.6.1-2-gf61c6a8, Tenants v2.3.0-5-g9c7aa5c,
Memories v1.44.0-18-g1b1db8d, PolymorphicSerializations (dead), AI.Tools.

| Project | loc | What it actually is | Verdict |
|---|---|---|---|
| `Hexalith.Parties` | 8,032 | **The runtime web host** (Program.cs + DI + invoker + query actors + search + validators + auth + health + middleware) | Keep; shrink to SDK host (~4,600 loc domain remains) |
| `Hexalith.Parties.Server` | 1,518 | One file: `Aggregates/PartyAggregate.cs` — pure domain | Keep (name matches Tenants sibling convention) |
| `Hexalith.Parties.Contracts` | 2,344 | 139 files: commands/events/VOs/models/state/security contracts | Keep; evict platform-shaped types (§4.5) |
| `Hexalith.Parties.Projections` | 3,090 | 2 pure fold handlers (630) + actors/rebuild/adapters (2,460) | **Dissolve**: folds → host/Server as `IDomainProjectionHandler`; rest deleted |
| `Hexalith.Parties.Security` | 3,195 | Generic crypto-shredding engine + thin Parties policy | **Extract engine** → `Hexalith.EventStore.PayloadProtection`; policy stays |
| `Hexalith.Parties.ServiceDefaults` | 44 | Delegation shim over `Hexalith.Commons.ServiceDefaults` | **Delete** (forbidden project type; Tenants Epic B2 precedent) |
| `Hexalith.Parties.UI` | 5,162 | Containerized BFF **web host** (misnamed) + shared components + 1,453-loc E2E fixture | Rename → `Hexalith.Parties.WebApp`; gut platform plumbing; evict fixture |
| `Hexalith.Parties.AdminPortal` | 6,801 | Two monolith pages (1,825 + 889 loc) + GDPR panels + 777-loc API client | Keep; rebuild on FrontComposer (→ ~25% of size) |
| `Hexalith.Parties.ConsumerPortal` | 3,143 | 4 pages + contracts-only services; **not actually a FrontComposer module** | Keep; slim onto FC.Shell + shared UI RCL |
| `Hexalith.Parties.Picker` | 1,593 | Hand-rolled ARIA combobox, **unwired at runtime** | **Merge** into new shared Parties UI RCL; rebuild on `FluentAutocomplete`/`FcEntityPicker` |
| `Hexalith.Parties.Client` | 1,257 | Hand-rolled EventStore gateway client | Keep; rebuild on `IEventStoreGatewayClient` (~50% shrink) |
| `Hexalith.Parties.Mcp` | 1,230 | Standalone MCP host, 5 tools; bypasses FrontComposer.Mcp | Keep as deployable; replatform on FC.Mcp registry + gates |
| `Hexalith.Parties.Authentication` | 90 | Generic `eventstore:tenant` claims transformation | **Dissolve upstream** (EventStore client/security or Commons) |
| `Hexalith.Parties.AppHost` | 438 | Aspire topology; partially on platform helpers | Keep (allowed for module testing); rebuild on `AddEventStoreDomainModule` |
| `Hexalith.Parties.Testing` | 466 | `PartyTestData.cs` domain fixtures | Keep; ULID-ify; set `IsPackable=false` decision |
| `samples/Hexalith.Parties.Sample` | — | Consumer-onboarding sample (Client SDK + pub/sub) | Keep; drop unused `Dapr.AspNetCore`; fix phantom-tool comment |

Dependency graph is acyclic. Odd edges: host → `Hexalith.EventStore.Server` (the anti-pattern —
target is `.DomainService` only); UI host aggregates AdminPortal+ConsumerPortal+Picker+
Authentication; host → `Memories.Client.Rest` via a fragile cross-mode MSBuild trick
(`AdditionalProperties="UseHexalithProjectReferences=false"`).

---

## 4. Boundary Violations (code that belongs in technical modules)

### 4.1 EventStore SDK bypass — host lane (~5,600–5,900 loc deletable)

Everything below has a **verified platform equivalent** in the pinned
`references/Hexalith.EventStore`:

| Parties code (loc) | Platform replacement |
|---|---|
| `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` (614) — reflection `/process` dispatch, type cache, validator dispatch | `AddEventStoreDomainService()` → `DomainServiceRequestRouter` + keyed `IDomainProcessor` discovery; crypto-unprotect step → `IDomainServiceAdmissionStage` |
| `src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs` (572), `PartyDetailProjectionQueryActor.cs` (296), `IPartyProjectionQueryActor.cs` (36) | One `IDomainQueryHandler` per query type (8 queries), per `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/*`; paging via `QueryCursorScope`/`IQueryCursorCodec` |
| `src/Hexalith.Parties.Projections/PartyIndexProjectionActor.cs` (736), `PartyDetailProjectionActor.cs` (596), `PartyEventTypeResolver.cs` (160) | `IDomainProjectionHandler` (SDK maps `/project`) + `IReadModelStore`/`DaprReadModelStore` + `ReadModelWritePolicy`; event-type resolution is `DomainProjectionDispatcher`'s job |
| `src/Hexalith.Parties.Projections/ProjectionRebuildService.cs` (633) + `LocalPartyProjectionPlatformAdapter.cs` (179) + `IPartyProjectionPlatformAdapter` shadow abstraction | `EventReplayProjectionActor`, `ProjectionRebuildCheckpointStore`, `Contracts/Streams/ProjectionRebuild*`, admin replay endpoints |
| `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs` (354) + `EventStorePartyProjectionPlatformAdapter.cs` (238) | Platform `ProjectionUpdateOrchestrator` + SDK `/project` model |
| `src/Hexalith.Parties/HealthChecks/Dapr{Sidecar,PubSub,StateStore}HealthCheck.cs` + `PartiesHealthCheckExtensions.cs` (217) | `Hexalith.EventStore/HealthChecks/*` + `AddEventStoreDaprHealthChecks` (needs options overload — gap G2) |
| `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs` (18) | `Hexalith.EventStore/Middleware/CorrelationIdMiddleware` (ULID-aware) |
| `src/Hexalith.Parties/ErrorHandling/PartiesGlobalExceptionHandler.cs` (185) + `PartiesValidationExceptionHandler.cs` (51) | Platform `GlobalExceptionHandler`, `ValidationExceptionHandler`, `AuthorizationExceptionHandler`, `ProblemTypeUris` |
| `src/Hexalith.Parties/Authentication/` (146) | `Hexalith.EventStore/Authentication/ConfigureJwtBearerOptions` + `EventStoreAuthenticationOptions` |
| `src/Hexalith.Parties.ServiceDefaults/` (44 + full OTel package set) | `Hexalith.EventStore.ServiceDefaults` (SDK calls `AddServiceDefaults()` itself); telemetry via `AddEventStoreDomainTelemetry("parties")` |
| `src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs` (189) proxy-version shims | disappears with the actors |
| `src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs` (339) hand-rolled Dapr state CRUD | rebuild on `IReadModelStore` + `ReadModelWritePolicy` (mapping logic stays, plumbing goes) |
| AppHost manual sidecar/statestore/pubsub wiring (~120 loc ×3 blocks) + JWT env plumbing (~120 loc) | `AddEventStoreDomainModule` (its own doc: "A domain module no longer ships its own AppHost/Aspire wiring — Epic A4"); propose `WithEventStoreJwtAuthentication(audience)` upstream (gap G8) |

Target host shape (per the mandate and the Tenants sibling): `Program.cs` ≈
`builder.AddEventStoreDomainService(); … app.UseEventStoreDomainService();` + domain DI. Host
references drop from `Hexalith.EventStore.Server` to `.DomainService` + `.Client`.

### 4.2 Crypto-shredding engine — security lane (~2,800 loc to extract)

The platform defines the full hook surface (`IEventPayloadProtectionService`,
`EventStorePayloadProtectionMetadata`, `CryptoShreddingWorkflow*`, `UnreadableProtectedDataReason`,
`ProtectedDataLeakSentinel` test kit — all in `references/Hexalith.EventStore`) but ships **no
engine**. Parties implements the engine locally and it is subject-generic (depends only on
`(tenantId, subjectId)`):

- **Extract to new `Hexalith.EventStore.PayloadProtection` (+ `.Abstractions`)**:
  `PartyPayloadProtectionService` (647 — field-level AES-256-GCM JSON encryption),
  `EventStorePartyPayloadProtectionAdapter` (389), `TenantKeyRotationService` (377),
  `LocalDevKeyStorageBackend` (303), `PartyKeyManagementService` (252) + `Cached…` (126),
  `DecryptionCircuitBreaker` (209), `ErasureVerificationService` (139), `PartyKeyRetryActor`
  (103) + scheduler, `KeyOperationAuditService` (59), `PartyErasureRecordStore` (48), lifecycle/
  guard/exception types — plus ~90% of `Contracts/Security/` (9 interfaces, 11 records, 9 enums;
  renamed `Party*` → `Subject*`). Do **not** name it "DataProtection" (clashes with the ASP.NET
  key-ring feature in `Hexalith.EventStore.DomainService`).
- **Stays in Parties (the policy seam)**: `[PersonalData]` placements; the
  `OrganizationDetails.IsNaturalPerson → all strings personal` rule
  (`PersonalDataGraphInspector.cs:47-55,91-105` — walker generic, rule domain); the
  `CreateParty`/`CreatePartyComposite` guard exemption; erasure workflow
  commands/events/aggregate handlers/status projection; `LawfulBasis` (consent domain — misfiled
  under `Security/`, move beside `ConsentRecord`); `MvpComplianceWarning` + middleware.
- **Hard couplings to break during extraction** (engine → domain types): `is PartyCreated`
  key-ensure trigger (`PartyPayloadProtectionService.cs:64`), `identity.Domain == "party"` filter
  (`:324`), `PartiesJsonOptions.Default` (`:33`), `PartiesClaimTypes.PartyId` as metric tag
  (`DecryptionCircuitBreaker.cs:4,175`), host calls to static `RedactProtectedPayload`
  (`PartyProjectionUpdateOrchestrator.cs:139`, `PartyDomainServiceInvoker.cs:552-592`) →
  introduce `IPersonalDataPolicy` + `IErasureStateProvider` seams.
- **Reconcile with the platform workflow contracts**: Parties built a parallel
  `ErasureStatus`/`PartyErasureStatusRecord` lifecycle; zero references to the platform's
  `CryptoShreddingWorkflowState/Transitions/AuditEvent` — adopt them during extraction.
- **Crypto fixes to fold into the move**: no AAD binding in `EncryptNode`
  (`PartyPayloadProtectionService.cs:482-502` — encrypted fields are spliceable within a party;
  bind `tenant|subject|eventType|fieldPath|kv` as AAD in a `pdenc-v2` format with v1 read
  support); envelope encryption is simulated (tenant KEK never actually wraps DEKs; "rotation"
  only rewrites metadata pointers); `LocalDevKeyStorageBackend` is the **only** backend and is
  wired unconditionally (`PartiesServiceCollectionExtensions.cs:142`) — a restart destroys all
  DEKs; the extracted module needs a pluggable production backend (Vault/KeyVault); constrain
  snapshot `Type.GetType` resolution (`:304-354`) to an allowlist.

### 4.3 FrontComposer bypass — UI lane

Verified against `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell` (which
ships shell/nav/layout, aggregate list/detail pages, DataGrid state machine, EventStore
command/query clients, SignalR projection subscriptions, polling fallback, reconnection
reconciliation, pending-command optimism, auth bridge/token relay, lifecycle service, destructive
dialog, badges, envelopes) and `Hexalith.EventStore.SignalR`:

| Parties code | Platform replacement |
|---|---|
| UI host `Services/{EventStoreSignalRProjectionStream, IProjectionStream, PartiesProjectionSubscription}` (317) | `ProjectionSubscriptionService` + `SignalRProjectionHubConnectionFactory` + `ProjectionHubRetryPolicy` |
| UI host `ProjectionFreshnessFallback` + options + DI (222) | `ProjectionFallbackPollingDriver` / `ProjectionFallbackRefreshScheduler` |
| UI host `OptimisticReconcile.cs` (220) | `State/PendingCommands/*` + `ReconnectionReconciliationCoordinator` |
| UI host `DegradedStateAccessor` + `DegradedResponseHeaderHandler` (107) | `ProjectionConnectionState` + `FcProjectionConnectionStatus` (keep one header-capture DelegatingHandler) |
| UI host `IProjectionAccessTokenProvider` (35), `RedirectToChallenge.razor` | `FrontComposerAccessTokenProvider`, `IAuthRedirector` |
| AdminPortal browse surface: monolith pages, manual paging math, `PortalFilters`, debounce CTS, focus tracking, `AdminPortalListState` | `FcPageLayout/Header/Toolbar`, `FcAggregateListPage/DetailPage`, `State/DataGridNavigation/*`, `FluentPaginator`, `FluentDatePicker` |
| AdminPortal result envelopes + 200-loc status mapping (`PartiesAdminPortalApiClient.cs:568-631`) + ConsumerPortal envelope family (~310 combined) | `FrontComposer.Contracts.Communication` (`CommandResult`, `QueryResult`, `ProblemDetailsPayload`) + `EventStoreResponseClassifier` |
| Both portals' CTS/version-guard lifecycle idiom (~350), skeleton/failure/erased triads, inline confirm dialogs | `LifecycleStateService`/`FcLifecycleWrapper`, `FcDestructiveConfirmationDialog` |
| Picker (705-loc raw combobox) | Fluent UI V5 `FluentAutocomplete` (caveat: V5 autocomplete a11y not implemented yet — the one blocking item) or new `FcEntityPicker<T>` (gap G4) |

**CSS audit**: 98 forbidden legacy-token occurrences across 9 portal stylesheets
(28 `--type-ramp-*`, 34 `--neutral-*`, 5 `--neutral-fill-*`, 7 `--accent-*`, 24 other v4
family); UI host and Picker css are V5-token-clean (Picker carries theme-bypassing hex
fallbacks). Raw-CSS re-implementations: type ramp on `h1-h3`, hand-fabricated danger buttons,
scratch-built status pills vs `FluentBadge`/`FcStatusBadge`, hand-rolled responsive
master-detail sheet vs `FcLayoutBreakpointWatcher`.

**Misplaced tiers**: `PartiesAdminPortalE2eFixture.cs` (1,453 loc, 28% of the UI host, in the
production container) → test-fixtures assembly, Test-env only; `IdentityBinding/` (736 loc,
in-memory store inside a stateless multi-replica web host) → server side.

### 4.4 Client / MCP / Authentication

- **Client**: posts raw JSON to `api/v1/commands|queries` with a private `EventStoreCommandRequest`
  record duplicated in `HttpPartiesCommandClient.cs:270-278` and
  `AdminPortal/HttpAdminPortalGdprClient.cs:393-401` — field-identical to the public
  `SubmitCommandRequest` already referenced via EventStore.Contracts. Replace transport with
  `IEventStoreGatewayClient` + `AddEventStoreGatewayClient`; option validation via
  `Hexalith.Commons.Http.HttpClientRegistration`. Benchmark: `Hexalith.Tenants.Client` = 393 loc,
  zero HTTP plumbing. Typed interfaces + GDPR outcome mapping stay.
- **Mcp**: references `ModelContextProtocol.AspNetCore` directly; re-invents
  `FrontComposerMcpResult`/failure taxonomy/agent-context accessor; mints **persisted domain IDs
  with `Guid.NewGuid()`** (`Tools/PartiesMcpTools.cs:977` feeding PartyId/ContactChannelId/
  IdentifierId); has **no tenant-tool/command-policy gating** although the platform gates exist
  (`IFrontComposerMcp{CommandPolicy,TenantTool,ResourceVisibility}Gate`). Replatform on the
  FC.Mcp descriptor registry (architectural move — some FC.Mcp primitives are `internal`).
- **Authentication** (90 loc): generic normalization of IdP claims into `eventstore:tenant` →
  upstream as `EventStoreTenantClaimsTransformation` (EventStore client/security or Commons;
  **not** FrontComposer.Shell — the actor host must stay Blazor-free).

### 4.5 Contracts-level platform types

| Type(s) | Finding → target |
|---|---|
| `Models/PagedResult<T>` | Byte-identical to `Hexalith.Commons.Paging.PagedResult<T>` (a second copy exists in `Commons.Http`) → adopt Commons; carry `Freshness` alongside or extend platform |
| `Models/ProjectionFreshnessMetadata`, `ProjectionFreshnessStatus` (35 consumer files) | Platform generalization exists (`ReadModelFreshnessState`, `QueryResponseMetadata.IsStale/IsDegraded/WarningCodes`) but lacks 4 states + 4 warning codes → **extend EventStore contracts first (gap G6)**, then retire |
| `PersonalDataAttribute` | No platform equivalent → add to EventStore.Contracts (natural consumer is the PayloadProtection engine) — gap G5 |
| `Security/` engine contracts (`IKeyStorageBackend`, rotation, audit, `EncryptionAlgorithm`, key records) | Platform has workflow/hook contracts but no key-storage/rotation/audit contracts → move with the engine (§4.2) |
| `Security/CryptoShreddingOptions` | Not a contract (appsettings-bound tuning) → engine package |
| `Authorization/PartiesClaimTypes.EventStoreTenant` | Duplicates a platform-owned literal scattered as internal constants in EventStore → EventStore.Contracts should own the public constant (gap G7) |
| `Authorization/PartiesClaimExtraction*` (1 production consumer) | Generic claim extraction → Hexalith.Commons |
| `PartiesJsonOptions` | Domain-agnostic serializer policy → candidate for `Hexalith.Commons.Serialization` (verify contents first) |
| `PartiesTextHeuristics.ContainsTenant` | Error-text sniffing as contract → replace with stable reason codes (platform precedent `QueryProblemReasonCodes`) |
| `State/PartyState.cs` (312 loc, ~45 `Apply` methods) | Domain behavior inside a published contracts package → move to the domain project. Note: 19 no-op rejection `Apply`s whose **declaration order is load-bearing** (EventStore rehydrator matches type by short-name *suffix*) — file a platform issue to match full names |
| Degraded-header literals (`X-Service-Degraded`, `X-Stale-Data-Age`) — ~8 raw strings in host middleware vs constants in UI | Single home: Contracts, or `Commons.Http` if the degradation contract goes platform-generic (pairs with gap G1) |

### 4.6 Platform gaps — must be added to technical modules FIRST

| # | Gap | Target module |
|---|---|---|
| G1 | Degraded-response middleware (`X-Service-Degraded`/`X-Stale-Data-Age` from HealthCheckService) — or drop the feature | Hexalith.EventStore |
| G2 | `AddEventStoreDaprHealthChecks` options overload (tag policy, per-check selection: "pubsub degrades /health not /ready", no configstore) | Hexalith.EventStore |
| G3 | Read-model **erasure hook** (GDPR "erase aggregate from all read models"; today: `EraseAsync` actor methods + delegate list) | Hexalith.EventStore |
| G4 | Generic `FcEntityPicker<T>` over `IProjectionSearchProvider` (+ paging/search-mode request extension); per-record `FcFreshnessIndicator`; `FcStatusLiveRegion` + politeness policy; file/JSON download service; typed-name mode for `FcDestructiveConfirmationDialog`; skip links in `FrontComposerShell` | Hexalith.FrontComposer |
| G5 | `PersonalDataAttribute` + the PayloadProtection engine package pair (§4.2), incl. pluggable production key backend | Hexalith.EventStore |
| G6 | Freshness-state + warning-code extension (Rebuilding/Degraded/Unavailable/LocalOnly) | Hexalith.EventStore.Contracts |
| G7 | Public `eventstore:tenant` claim constant; public `AggregateIdentity.IsValid(string)` / `UniqueIdHelper.IsValidUlid(string)` predicate (today the regexes are ctor-throw-only/private) | EventStore.Contracts / Commons |
| G8 | `AddEventStoreDomainModule` adoption gaps: `WithEventStoreJwtAuthentication(audience)` helper; granular EventStore client registration that preserves module-typed clients (Story 1.7 fork root cause) | EventStore.Aspire / FrontComposer |
| G9 | `EventStoreTenantClaimsTransformation` (from Parties.Authentication) | EventStore client/security or Commons |
| G10 | Index-projection **batching** (`ProjectionOptions.BatchSize/BatchTimeWindowMs`) — port into the platform read-model path **or** consciously drop | Hexalith.EventStore |
| G11 | MCP auth/tenant header-relay DelegatingHandler; EventStore-admin-UI deep-link builder; search-capability health probe | FrontComposer / Commons.Http |
| G12 | Publish the missing packages (`Hexalith.Commons.Http`, `Hexalith.Commons.ServiceDefaults`, `Hexalith.Tenants.Client`, `Hexalith.Tenants.Testing`) or bless source-mode CI | Commons / Tenants release pipelines |

---

## 5. Duplication Findings (consolidation targets)

| Duplicate | Copies / evidence | Consolidation target |
|---|---|---|
| `SubmitCommandRequest` private clones | `HttpPartiesCommandClient.cs:270`, `HttpAdminPortalGdprClient.cs:393` | Use `Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest` |
| ID-format validation rule | **31 copies** (`.Must(id => Guid.TryParse(...))` in 18 validators + `PartyAggregate.cs:41,561`) | ONE `MustBeValidAggregateId()` FluentValidation extension in `src/Hexalith.Parties/Validation/`, delegating to the new platform predicate (G7) |
| Composite validators re-inline child rules | `CreatePartyCompositeValidator` / `UpdatePartyCompositeValidator` copy `AddContactChannel`/`AddIdentifier` blocks (~60 loc, drift already visible) | `RuleForEach(...).SetValidator(...)` composition |
| `NormalizeDetail(PartyDetail)` | 4 identical copies (3 AdminPortal + 1 ConsumerPortal) | Method on `PartyDetail` in Contracts |
| Freshness indicator | 3 implementations (AdminPortal `RenderFreshness`, ConsumerPortal `FreshnessStatus.razor`, UI host `DataFreshnessIndicator`) | FrontComposer `FcFreshnessIndicator` (G4) |
| Destructive confirmation UX | 3 implementations + host button | `FcDestructiveConfirmationDialog` (+typed-name mode, G4) |
| JSON download interop | 2 + host JS; AdminPortal copy calls **nonexistent** `HexalithPartiesAdminPortal.downloadJson` (`PartyGdprOperationsPanel.razor:799`) | FrontComposer download service (G4) |
| Status vocabulary | AdminPortal private 13-state `StatusKind` vs UI host canonical 9-state `Status/*` vs ConsumerPortal enums (~400 loc) | Mechanism → FrontComposer (G4); canonical table → shared Parties UI RCL |
| Person/Org form model + builders; `TextField`/`FieldError` fragments; server-validation mapping | AdminPortal + ConsumerPortal 70–85% identical (~680 loc) | Shared Parties UI RCL; mechanism → `ServerValidationApplicator` |
| Labels | `AdminPortalLabels` 436 loc hardcoded EN vs ConsumerPortal resx (980) vs `PartyPickerLabels` | One resx assembly in shared Parties UI RCL |
| Profile scoped-CSS | 79 identical lines ×2, and 2 pages use the classes **without defining them** (render unstyled) | One shared stylesheet |
| Correlation accessor | `Parties.Security.{I}CorrelationContextAccessor` = pass-through over `Hexalith.Commons.Http` | Delete; inject Commons types |
| Dapr health checks ×3 + correlation middleware + JWT config + exception handlers | Near-verbatim platform duplicates (§4.1) | Delete; consume EventStore |
| `ProtectedDataLeakSentinel` | `tests/Hexalith.Parties.Security.Tests/` copy of `Hexalith.EventStore.Testing/Security/` original | Reference `Hexalith.EventStore.Testing` |
| `RecordingHttpMessageHandler` | AdminPortal.Tests + Picker.Tests copies | `Hexalith.EventStore.Testing` Http helpers |
| `ManualTimeProvider` | UI.Tests | `Microsoft.Extensions.TimeProvider.Testing` FakeTimeProvider |
| `PagedResult<T>` | Parties copy + 2 Commons copies (platform should also dedupe its own) | `Hexalith.Commons.Paging.PagedResult<T>` |
| Stringly statuses duplicating enums | `PartyErased.ErasureStatus="Erased"`, `VerificationStatus="Complete"`, `PartyErasureInProgress.Status` vs the `Security/` enums; `"unspecified"`/`"unknown"` sentinels ×9 | Use enums (wire-compatible via `JsonStringEnumConverter`); shared constants |

---

## 6. Obsolete / Dead Code Findings

**Dead package/project references (verified by grep, zero usages)**
- `MediatR`: `Hexalith.Parties.Server`, `Hexalith.Parties` (also still pinned 14.2.0 in Builds).
- `Dapr.Client` + `Dapr.Actors`: `Hexalith.Parties.Server` (both); `Dapr.Client`: `Hexalith.Parties.Projections`.
- `Aspire.Hosting.Redis` (documented as intentionally uncomposed) + direct `Aspire.Hosting.Keycloak` (transitively supplied): `Hexalith.Parties.AppHost`.
- `Microsoft.Extensions.Hosting.Abstractions`: `Hexalith.Parties.Security`.
- `Hexalith.FrontComposer.Mcp`: `Hexalith.Parties.UI`.
- `Hexalith.Parties.Client` **and** `Hexalith.FrontComposer.Shell`: `Hexalith.Parties.ConsumerPortal`.
- `Dapr.AspNetCore`: `samples/Hexalith.Parties.Sample`.
- **`Hexalith.PolymorphicSerializations`: fully dead** — submodule + 2 `.slnx` entries, zero consumers anywhere (incl. all submodules). Remove from `.slnx` and `.gitmodules`.
- Fragile inverse case: the host uses `AddActors`/`MapActorsHandlers` **without** referencing `Dapr.Actors.AspNetCore` (flows transitively via EventStore.Server) — moot after migration.

**Dead code**
- `ConsumerRouteShell.razor(+css)` (103 loc) — unreferenced.
- `PartyErasureOrchestrator.ExecuteKeyDestructionAsync` — zero call sites in src and tests; key
  destruction is never triggered in any production flow (`MarkPartyEncryptionKeyDeleted` exists
  but nothing calls `DeleteKeyAsync`). Either dead or unwired v1.1 work — **decide explicitly**.
- Contracts production-dead types: `EmailAddress`, `PhoneNumber`, `SocialMediaHandle` (only the
  test inventory references them), `PartyDisplayFormat` (own test only; wrong layer),
  `PostalAddress` (test-data only). `TemporalNameResult` + `PartyMerged` are documented
  v2 placeholders pinned by package tests — keep by policy or drop with test updates.
- `HttpAdminPortalGdprClient.PostCommandAsync` unused `route` parameter; 4 decorative
  `AdminPortalGdprRoutes` constants; `AdminPortalSearchResponse.cs` unused;
  `PartiesPagedResultAdapter.Normalize` round-trip no-op; stale `@using Fluxor` in UI
  `_Imports.razor`; `PartyPickerSearchMetadata` 3 `[Obsolete]` binary-compat members
  (+ self-consuming `#pragma`); `PartyPicker.ApiBaseUrl` `[Obsolete]` parameter;
  `PartiesAdminPortalOptions` `[Obsolete]` option.

**Hygiene**
- **25 tracked `*.csproj.lscache`** files (13 src incl. sample, 12 tests) — delete, add
  `*.lscache` to `.gitignore`.
- Tracked `.aspire-run/parties-apphost.pid` — delete, ignore `.aspire-run/`.
- `tests/e2e/story-7-*.spec.ts` (3 files, 18 "tests") assert BMAD planning markdown under
  `_bmad-output/` — not E2E; delete or move out of the Playwright suite.
- `Testcontainers 4.13.0` central pin with zero consumers; `tests/README.md` claims
  Testcontainers usage — both stale.
- Root `Directory.Build.targets` patches **FrontComposer submodule** warnings (CS9113/CS0162/
  CS1591/CS1574) — upstream to FrontComposer and drop locally.

---

## 7. Over-engineered / Unnecessarily Complex Code

| Finding | Simplification |
|---|---|
| `PartiesAdminPortal.razor` (1,825 loc) and `CreateEditPartyPage.razor` (889 loc) monoliths with nested filter classes, manual paging math, focus bookkeeping | Decompose onto `FcAggregateListPage`/`FcAggregateDetailPage` + DataGrid state machine; pages become composition + GDPR panels |
| AdminPortal dual-path query client (`IQueryService` fallback + `RequireContract` machinery) | Self-documented as retirable (`PartiesAdminPortalApiClient.cs:696-698`) — single path via FrontComposer clients |
| 13-state search + 9-state selection machines in Picker; 11-value `AdminPortalCommandResult` outcome; 10-value `AdminPortalQueryFailureKind` | Collapse onto platform response classifier + freshness metadata |
| `PartiesRoles`: 9 constants + 3 arrays existing to carry lowercase duplicates; `AdminPolicy`/`Admin` share a value | Case-insensitive comparison at consumers; halve the type |
| `PartiesClaimExtraction` 3×2 overload pairs for 1 production consumer | Keep principal overloads only (or move to Commons per §4.5) |
| Per-component `AuthenticationStateChanged` subscription + signature diffing (`PartiesAdminPortal.razor:601-714`) | `IUserContextAccessor` / `FcAuthorizedCommandRegion` |
| Reflection-based `GetPropertyValue(exception, "Reason")` in `PartiesGlobalExceptionHandler` | Platform typed handlers |
| `IIndexPartitionStrategy`/`SingleKeyPartitionStrategy` — degenerate strategy always returning one key | Delete |
| Contracts XML-docs: `GenerateDocumentationFile=true` + `NoWarn 1591`, only 9/139 files documented, while a package test asserts the XML exists | Either document the public surface or drop the pretense; 12 files violate the brace style |
| `Hexalith.Parties.Testing` inherits `IsPackable=true` for a test-data assembly | Set `IsPackable=false` (or deliberately ship it like Tenants.Testing — decide) |
| MSBuild dual-mode source/package switching keyed on `$(Configuration)` (`Directory.Build.props:19-25`) | Root cause of the phantom-error baseline; replace with an explicit opt-in property set by CI and documented for local dev |

---

## 8. Systemic Identifier (ULID) Violations

Ground truth: `Hexalith.EventStore.Contracts.Identity.AggregateIdentity` accepts
`^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$` (≤256 chars) — ULIDs pass. `Guid.TryParse` rejects
them, so **Parties currently rejects IDs the platform accepts**, and several surfaces mint GUIDs.

- **Validation (31 sites)**: all 18 validators in `src/Hexalith.Parties/Validation/` (29
  occurrences incl. composite sub-rules; messages hardcode "must be a valid GUID") +
  `PartyAggregate.cs:41,561` (emits `PartyCannotBeCreatedWithInvalidId`).
- **Minting persisted domain IDs (hard violations)**: `PartiesMcpTools.cs:977` (`NewId()` →
  PartyId/ContactChannelId/IdentifierId at 7 call sites); `CreateEditPartyPage.razor:385,602,623`.
- **Minting message/correlation IDs**: `HttpPartiesCommandClient.cs:158`,
  `HttpAdminPortalGdprClient.cs:193`, `TenantKeyRotationService.cs:35,281`,
  `PartyKeyManagementService.cs:224`.
- **Benign but fix for consistency**: health-probe IDs (`PartyMemoryCleanupService.cs:244`,
  `MemoriesSearchHealthCheck.cs:186`), DOM instance ids (Picker, portals, `GdprDestructiveButton`),
  `PartiesProjectionSubscription.cs:123`.
- **Fixtures/tests cement the GUID contract**: `PartyTestData.cs:11` GUID-format `DefaultPartyId`;
  test pins listed in §10.
- Remedy: platform predicate (G7) → one shared validator rule → flip aggregate + validators +
  fixtures together; mint via `UniqueIdHelper.GenerateSortableUniqueStringId()` / `UlidFactory`.
  **Compatibility**: existing GUID-keyed streams must keep working — accept both shapes
  (`AggregateIdentity` rules), never require ULID-only.

---

## 9. Runtime Defects Found During Analysis (fix independently of the refactor)

1. **GDPR Art. 20 export is broken in AdminPortal** — `PartyGdprOperationsPanel.razor:799`
   invokes nonexistent JS `HexalithPartiesAdminPortal.downloadJson`; the export always fails.
2. **The Party picker never works at runtime** — no host calls
   `RegisterHexalithPartyPickerCustomElement`/`AddHexalithPartyPicker`;
   `CreateEditPartyPage.razor:120` renders an unregistered `<hexalith-party-picker>`; the UI
   specimen `@inject`s an unregistered `PartyPickerApiClient` (DI failure on that route).
3. **`/me/consent` and `/me/edit` render partially unstyled** — scoped-CSS classes used without
   definition (copy-paste isolation bug).
4. **GUID minting of persisted domain IDs** (§8) — every MCP-created or AdminPortal-created
   party bakes a GUID into the event stream.
5. **Main branch does not build in CI** (§2) — including for consumers cloning without submodules.

---

## 10. Test Impacts

1,893 facts/theories across 15 projects + 75 Playwright tests. Buckets: **KEEP** (survives
as-is), **REWRITE** (behavior survives, harness pins old architecture), **DELETE/MOVE** (tests of
plumbing that moves to a technical module or is already covered there).

| Project | Tests | KEEP | REWRITE | DELETE/MOVE |
|---|---|---|---|---|
| Server.Tests (aggregate) | 203 | ~200 | ~3 (GUID pins) | 0 |
| Contracts.Tests | 116 | ~108 | ~8 (package-shape pins) | 0 |
| Projections.Tests | 93 | 83 (folds) | 0 | 10 (`PartyEventTypeResolverTests`) |
| Hexalith.Parties.Tests (host) | 448 | ~165 (Search/Authorization/fitness) | ~130 (validators, invoker-behavior, gateway routing splits, topology pins) | ~155 (query-actor, projection-actor/rebuild, Dapr health, degraded middleware, ServiceDefaults, correlation) |
| Security.Tests | 159 | ~35 (policy) | ~20 | ~105 (move with engine; wire-format tests are compat contracts — move, don't drop) |
| UI.Tests | 215 | ~170 | ~12 | ~33 (SignalR/polling/reconcile plumbing) |
| AdminPortal.Tests | 172 | ~165 | ~7 | 0 |
| ConsumerPortal.Tests | 63 | ~55 | ~8 (packaging pins) | 0 |
| Client.Tests | 96 | ~81 | ~15 (fitness/package pins) | 0 |
| Picker.Tests | 97 | 0 | ~10 (adapter behavior) | ~87 (move with component) |
| Mcp.Tests | 52 | ~47 | ~5 (csproj-text pins) | 0 |
| Authentication.Tests | 11 | 11 (move with the class upstream) | 0 | 0 |
| IntegrationTests (Aspire Tier-3) | 34 | 0 | ~23 (retarget to SDK topology) | ~11 (generic pub/sub delivery — platform-covered) |
| Sample.Tests | 58 | ~45 | ~13 (retarget from EventStore.Server internals to `EventStoreTesting` builders) | 0 |
| DeployValidation.Tests | 76 | ops lane — keep, **regenerate topology fixtures** after refactor; move generic lint machinery to Builds/AI.Tools | | |

**GUID-pinning tests to flip with the ULID fix** (hard pins):
`Validation/IdentifierValidatorTests.cs:105,131,133`, `ContactChannelValidatorTests.cs:30-113`,
`Domain/PartyDomainServiceInvokerValidationTests.cs:36,66,191,436-441`,
`Server.Tests/PartyAggregateCreateTests.cs:340`, `Middleware/CorrelationIdMiddlewareTests.cs:50`,
`AdminPortal.Tests/CreateEditPartyPageTests.cs:114`; plus pervasive GUID-shaped test data and
`PartyTestData` defaults.

**Meta-pins that will churn on every move**: `ContractsPackageTests.cs:85` (exact nupkg
dependency array), `ContractsPublicApiSnapshot.txt`, `ArchitecturalFitnessTests` (21),
`AppHostTenantsTopologyTests` (16, string-pins the old AppHost text), packaging tests in
AdminPortal/ConsumerPortal/Client/Mcp, `scripts/test.ps1` + CI shard lists (hard-coded project
inventories). Framework compliance is already clean: xUnit v3 + Shouldly + NSubstitute + bUnit
everywhere; zero MSTest/NUnit/FluentAssertions/Moq.

---

## 11. Keep-in-Parties vs Move-to-Technical-Module Classification

**KEEP in Parties (domain)**
- `PartyAggregate` (24 handlers) + `PartyState` (relocated out of the Contracts package).
- All commands (24), events (45), value objects, read models, GDPR/consent/erasure workflow
  contracts, `PartyCommandResult`/`CompositeCommandResult` (correct `DomainResult` extensions).
- Projection **fold** handlers (`PartyDetailProjectionHandler`, `PartyIndexProjectionHandler`) —
  re-hosted as `IDomainProjectionHandler` + read models.
- 8 domain queries — re-hosted as `IDomainQueryHandler`s with `QueryCursorScope` paging.
- All 18 command validators (ULID-ified, deduplicated via one shared rule).
- Memories-backed search subsystem (~3,000 loc) minus the state-store plumbing; domain health
  checks (`MemoriesSearchHealthCheck`, `TenantsIntegrationHealthCheck`).
- Authorization policy layer (`TenantAccessService` decisions, `ConsumerPolicy`,
  `DataSubjectAccessService`); GDPR capability matrix; MVP-compliance warning.
- Personal-data **policy**: `[PersonalData]` placements, `IsNaturalPerson` rule, guard
  exemptions, erasure workflow + status projection, `LawfulBasis`.
- Domain UI: portal pages as thin FrontComposer compositions, GDPR panels' business rules,
  `PartyStateBadge`/`PartyLifecycleState`, labels (as resx), picker *adapter* over the generic
  component, `SelfScopedPartiesClient` + consumer adapters, `PartyIdClaimResolver`, role landing.
- Typed client interfaces + GDPR outcome mapping; MCP tool semantics (arg coercion, composite
  assembly, soft-delete stance); `PartyTestData`; the Sample; the AppHost (testing topology).

**MOVE to technical modules**
- → **Hexalith.EventStore**: everything in §4.1 (delete in favor of existing SDK) + gaps
  G1–G3, G6–G10; the **PayloadProtection engine + abstractions** (§4.2); tenant claims
  transformation (G9).
- → **Hexalith.FrontComposer**: SignalR/polling/reconcile/degraded UI plumbing (delete in favor
  of Shell) + gaps G4, G11; generic picker; MCP hosting via FC.Mcp registry.
- → **Hexalith.Commons**: `PagedResult` adoption, claim extraction, JSON-options consolidation,
  correlation-accessor cleanup, ULID predicate (with G7).
- → **Hexalith.Builds / AI.Tools**: deployment lint machinery (`validate-deployment.ps1`,
  lint-negative fixtures, `check-no-warning-override.sh` pattern), FrontComposer warning patches.

**DELETE outright**: hand-rolled actors/invoker/rebuild/orchestrator/adapters, ServiceDefaults
project, dead types/refs/artifacts (§6), 98 legacy CSS tokens, story-7 Playwright specs.

---

## 12. Risks, Migration Order, Compatibility Concerns

1. **Persisted-format stability (highest risk, security lane)**: the crypto envelope
   (`json+pdenc-v1`, `$enc/alg/kv/n/t/c`), metadata scheme `parties-aes-gcm-json-fields`,
   snapshot `TypeName` strings, key paths `{tenant}/parties/{party}/v{n}`, state keys
   (`{tenant}:party-key-audit:{party}`, `{tenant}:erasure*:{party}`), **Dapr actor type name
   `PartyKeyRetryActor`** (rename orphans reminders/state), meter `Hexalith.Parties.Security` +
   `parties.*` metric names. Extraction must keep these stable or ship an explicit migration;
   the compat harness tests (`CryptoKeyManagementCompatibilityHarnessTests`, wire-format
   assertions) move with the engine as golden tests.
2. **Projection/actor state migration**: retiring the hand-rolled projection/query actors
   changes where read models live. Plan: stand up SDK read models → rebuild from streams
   (replay) → cut queries over → retire actors. Never dual-write.
3. **GUID→ULID**: accept-both, never reject existing GUID streams (§8). Wire messages keep
   string typing, so this is validation/minting-only — but the erasure/GDPR command surface is
   included (all its validators pin GUID today).
4. **Package-vs-source dependency fragility**: every conditional
   `ProjectReference`/`PackageReference` pair is a latent CS0246 storm (§2). Until Commons/
   Tenants publish, **CI must run source mode explicitly** or package mode must be made complete;
   keep the `Update` MCP pin either way.
5. **Sequencing on platform gaps**: nothing in §4.1/4.3 can be deleted before the corresponding
   gap lands and a platform release is pinned. Order platform work G12 → G7 → G2/G6 →
   G1/G3/G10 → G4/G8/G11 → G5 (engine last, it's the largest).
6. **Public API/package pins**: Contracts nupkg dependency array, public-API snapshot,
   packaging fitness tests, `scripts/test.ps1` + CI shards, DeployValidation manifest fixtures,
   `deploy/k8s` topology, `aspirate` config — all hard-code today's inventory; budget churn in
   every step.
7. **In-flight UX debt**: portals get restyled (token migration) while being decomposed; visual
   baseline (`parties-accessibility-shell.visual.json`) and axe gates must be re-captured
   deliberately, not silently.
8. **Aspire live-boot** validation depends on the environment's DCP/CLI alignment; keep the
   Tier-3 integration lane opt-in with graceful skip (already the pattern).

---

## 13. Action Plan (ordered)

**Phase 0 — Stabilize the baseline (blocks everything)**
1. Keep the `Update` MCP pin and choose the dependency mode deliberately: publish the missing
   packages or set `UseHexalithProjectReferences=true` explicitly in CI/local source builds.
2. Make CI configuration-consistent (`dotnet restore -p:Configuration=Release` or env
   `Configuration=Release`; if source mode is the decision, pass
   `-p:UseHexalithProjectReferences=true` everywhere until G12).
3. Fix CI drift: add the 2 missing test projects to shards, align `dotnet-version` with
   `global.json`, fix `scripts/test.ps1` lanes.
4. Hygiene commit: delete 25 `.lscache` + `.pid`, extend `.gitignore`, remove
   PolymorphicSerializations (slnx + .gitmodules), drop dead package refs (§6 — MediatR,
   Dapr.* in Server, Aspire.Hosting.Redis, Hosting.Abstractions, dead UI refs, sample Dapr).
5. Fix the four runtime defects (§9) — small, independent, user-visible.
6. Record baseline: build source-mode `.slnx`; run all 15 test projects individually; save the
   green list as the regression reference.

**Phase 1 — Inventory & classification sign-off (this document)**
7. Review §11 keep/move classification with module owners; open one tracking issue per platform
   gap G1–G12 in the owning repos (Commons, EventStore, FrontComposer, Tenants, Builds).

**Phase 2 — Platform work (in the technical modules, released & pinned before consumption)**
8. Land G12 (publish packages or bless source-mode CI), then G7 (ID predicate + claim constant),
   G2/G6 (health-check options, freshness extension), G1/G3/G10 (degraded middleware, erasure
   hook, batching decision), G4/G8/G11 (FrontComposer components + Aspire/registration helpers),
   G9 (claims transformation), and finally G5: `Hexalith.EventStore.PayloadProtection` +
   `.Abstractions` — engine moved from Parties with subject-generic renames, `IPersonalDataPolicy`
   / `IErasureStateProvider` seams, AAD binding (`pdenc-v2` + v1 read), pluggable key backend;
   engine tests move as golden compat suites. Bump submodules/pins in Parties per landing.

**Phase 3 — Migrate the host lane onto the SDK**
9. ULID first (small, independent): shared validator rule, aggregate fix, minting fixes,
   `PartyTestData` + test flips (§8, §10).
10. Re-host queries as 8 `IDomainQueryHandler`s + cursor paging; re-host folds as
    `IDomainProjectionHandler` + `IReadModelStore` read models (index batching per G10 outcome);
    port `PartyMemoryUnitMappingStore` onto `IReadModelStore`.
11. Flip `Program.cs` to `AddEventStoreDomainService()`/`UseEventStoreDomainService()`; delete
    invoker, query/projection actors, rebuild service, orchestrator, adapters, duplicate
    health/middleware/auth/error handlers, `PartyDetailProjectionActorExtensions`; extract the
    invoker's erasure-status/retry domain logic into a domain service first. Delete
    `Hexalith.Parties.ServiceDefaults`; dissolve `Hexalith.Parties.Projections`; drop the
    `EventStore.Server` reference. Rebuild AppHost on `AddEventStoreDomainModule` (+G8 helper).
    Execute the read-model replay/cutover plan (§12.2).

**Phase 4 — Migrate the UI/client lane onto FrontComposer**
12. Create the shared `Hexalith.Parties.UI` RCL (badges, status table, form model, labels-resx,
    shared stylesheet, picker adapter); rename the host → `Hexalith.Parties.WebApp`; evict the
    E2E fixture to a Test-env fixtures assembly and `IdentityBinding/` server-side.
13. Delete the ~840 loc SignalR/polling/reconcile/degraded plumbing in favor of Shell (gap G8
    registration first); rebuild AdminPortal browse/detail on `FcAggregateListPage`/`DetailPage`
    + DataGrid state; adopt FC envelopes/classifier; give ConsumerPortal a real FrontComposer
    manifest/registration; replace confirmation/lifecycle/freshness/download with FC components
    (G4); migrate the 98 legacy CSS tokens to Fluent 2 tokens / component params; fold Picker
    into the RCL on `FluentAutocomplete`/`FcEntityPicker` (track the V5 a11y caveat) and wire
    the custom-element registration.
14. Rebuild `Hexalith.Parties.Client` transport on `IEventStoreGatewayClient` (+Commons.Http
    registration validation); replatform `Hexalith.Parties.Mcp` on FC.Mcp registry + gates;
    dissolve `Hexalith.Parties.Authentication` into G9's platform type.

**Phase 5 — Contracts & cleanup**
15. Move `PartyState` out of the Contracts package; evict platform-shaped contracts (§4.5);
    delete dead types (§6) and stringly-status duplication; fix composite-validator composition;
    update the package/API-snapshot pins deliberately.
16. Remove obsolete tests per §10 buckets (move engine/component tests with their code); retarget
    Sample.Tests to `EventStore.Testing` builders; delete `story-7-*` specs; re-baseline visual/
    axe gates.

**Phase 6 — Docs, samples, verification**
17. Regenerate `docs/` (architecture, index, component inventory, source-tree, project-scan) —
    §6 of the hygiene audit lists the stale files; rewrite the actor/security/deployment docs to
    the SDK/PayloadProtection reality; fix README project tree (+Security/Authentication),
    tests/README lanes, `docs/ci.md`; fix the sample's phantom `get_party_name_at` comment;
    regenerate `deploy/k8s` manifests + DeployValidation fixtures for the new topology.
18. Final verification (checklist below), then per-repo release: technical modules first, then
    Parties.

Each phase is one or more Conventional-Commit PRs; run affected test projects individually per
step, and the full `.slnx` restore/build (source mode) at each phase boundary.

---

## 14. Final Verification Checklist

- [ ] `dotnet restore` + `dotnet build Hexalith.Parties.slnx -c Release` green in **both**
      source mode and package mode (or package mode formally deferred with CI pinned to source).
- [ ] GitHub Actions fully green; all 15 (post-refactor: ~10) test projects in the shards;
      SDK pin matches `global.json`.
- [ ] All test projects pass **individually**; counts reconciled against the Phase-0 baseline
      minus deliberate deletions (documented per §10).
- [ ] `grep -r "AddEventStoreDomainService" src/` ≥ 1; `grep -r "IProjectionActor\|ActorHost\|
      Dapr.Actors.Runtime" src/` = 0 outside the (extracted) engine; no `Hexalith.EventStore.Server`
      reference from the host.
- [ ] `grep -rn "Guid.TryParse\|Guid.NewGuid" src/` = 0 on identifier paths; a ULID-format party
      can be created, queried, erased end-to-end; an existing GUID-format party still loads.
- [ ] `grep -rn -- "--type-ramp-\|--neutral-\|--accent-\|--palette-" src/**/*.css` = 0;
      portals render `<Fc*`/Fluent components; axe + visual baselines re-captured green.
- [ ] Picker registered and functional in the create/edit form; Art. 20 export downloads;
      `/me/consent`, `/me/edit` fully styled.
- [ ] Crypto compat: golden harness green against the extracted engine; pre-refactor encrypted
      fixture decrypts; erased party stays erased; leak sentinel (EventStore.Testing) green;
      actor-type/state-key/meter names unchanged or migrated with evidence.
- [ ] `aspire run` topology boots via `AddEventStoreDomainModule`; Tier-3 integration lane passes
      (or skips gracefully with the documented DCP caveat).
- [ ] No `*.lscache`/`.aspire-run` tracked; PolymorphicSerializations gone; no dead package refs
      (`dotnet build` + grep audit per §6).
- [ ] Docs/README/test-lane scripts regenerated; DeployValidation fixtures match the new
      topology; sample builds against published (or source) packages and its guardrail tests pass.
- [ ] Public-API snapshot and package-dependency pins updated deliberately (diff reviewed, not
      auto-accepted).
