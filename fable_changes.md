# Hexalith.Parties — Domain-Focus Refactoring Analysis & Action Plan

> Analysis date: 2026-07-06 · Branch: `main` (clean, HEAD `88d984b`)
> Scope: keep Hexalith.Parties strictly domain-focused; move reusable/cross-cutting code to
> Hexalith.Commons / Hexalith.EventStore / Hexalith.FrontComposer (or other technical modules).
> No changes have been implemented — this is the analysis and roadmap only.

---

## 1. Executive Summary

Hexalith.Parties contains a solid, well-tested domain core (a pure 1,518-line `PartyAggregate`
with 203 aggregate facts, clean projection fold functions, rich GDPR domain workflows), but it is
wrapped in a **parallel platform** that re-implements almost every capability the technical
modules already provide:

- **The EventStore domain-service SDK is bypassed entirely.** `AddEventStoreDomainService()` /
  `UseEventStoreDomainService()` are used **zero** times. The host hand-maps `/process` through a
  614-line reflection-based invoker, ships two hand-rolled Dapr projection actors (596 + 736
  lines), two hand-rolled query actors (296 + 572 lines), and its own rebuild service (633 lines).
  `IDomainQueryHandler`, `IDomainProjectionHandler`, `IReadModelStore`, `ReadModelWritePolicy`,
  `IQueryCursorCodec` — the mandated platform abstractions — appear **nowhere in `src/`**.
- **Forbidden platform projects exist**: `Hexalith.Parties.ServiceDefaults` (a 44-line wrapper
  over `Hexalith.Commons.ServiceDefaults`) violates the explicit rule that domain modules must
  not ship `*.ServiceDefaults`. Sibling module Hexalith.Tenants has already removed its own.
- **A generic crypto-shredding engine lives inside the domain**: ~2,500 lines of key management,
  payload encryption, key rotation, audit and circuit-breaker code in `Hexalith.Parties.Security`
  (plus ~20 platform contracts in `Contracts/Security/`) implement EventStore's own
  `IEventPayloadProtectionService` hook. Nothing but the erasure *policy* is Parties-specific.
- **The UI re-implements FrontComposer**: SignalR projection subscription, fallback polling,
  optimistic reconciliation, token provider, degraded-state handling, grid/list state machines,
  result envelopes, and a from-scratch combobox (Picker) all duplicate shipped FrontComposer /
  Fluent UI V5 capabilities. 8 stylesheets carry 64 forbidden legacy v4/FAST tokens.
- **Systemic identifier rule violation**: ~30 `Guid.TryParse` validations on aggregate IDs (all
  22 validators + `PartyAggregate`) — meaning **ULID-formatted IDs are rejected today** — plus 8+
  `Guid.NewGuid()` sites minting message/correlation IDs. Both are forbidden (ULID rule).
- **Dead weight**: MediatR referenced by two projects with zero usage; `Dapr.Client`/`Dapr.Actors`
  unused in `Parties.Server`; `Aspire.Hosting.Redis` unused in the AppHost;
  Hexalith.PolymorphicSerializations referenced by the solution but used nowhere; 25 committed
  `*.csproj.lscache` files.

**Estimated net effect of the plan**: `src/` shrinks by roughly 40–50% (≈15,000+ lines of platform
plumbing deleted or moved), the project count drops from 15 to ~10, and the module converges on the
platform contract: aggregate + contracts + projections handlers + query handlers + validators +
domain UI + domain clients + a thin AppHost.

**Critical sequencing constraint**: several platform gaps must be filled in the technical modules
**first** (Section 8) — most notably projection rebuild/checkpoint support in the EventStore SDK
path, a data-protection engine package, and publish-mode JWT/OIDC helpers in EventStore.Aspire —
before the corresponding Parties code can be deleted.

---

## 2. Baseline Build/Test Status (established facts)

| Fact | Status |
| --- | --- |
| Clean parallel builds | Flake with CS0006/MSB4018 (Rebuild race / StaticWebAssets lock) — use `-m:1` for verdicts |
| `dotnet pack` / all `*PackageTests` | **Pre-existing red** (NU5118/NU5128, `process.ExitCode`) — not a regression signal |
| `Hexalith.Parties.Tests` host EXE | 485 total, **1 pre-existing failure**: `AppHostTenantsTopologyTests` asserts literal `Hexalith.EventStore\src\…` while the csproj uses `$(HexalithEventStoreRoot)\src\…` (`tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs:10`) |
| xUnit v3 (MTP) | `dotnet test --filter` broken — run test EXEs directly with `-class`/`-method` |
| MinVer | No git tags in worktree → rebuild subsets with `-p:MinVerVersionOverride=1.0.0` or mass `FileNotFoundException` |
| e2e a11y gate | Cannot fully pass locally (interactive Blazor dead in WSL sandbox); SSR specs via `PLAYWRIGHT_SKIP_WEBSERVER=1`, interactive gate in `ui-a11y` CI |
| `scripts/test.ps1` | **Bug**: `Hexalith.Parties.ConsumerPortal.Tests` is in no lane — silently never runs; `all`/`coverage` lanes use forbidden solution-level `dotnet test` |
| Wire format | EventStore serializes event payloads PascalCase/numeric enums — Parties readers must stay case-insensitive (`PartiesJsonOptions.Default`) |

Any migration step must re-establish this exact baseline (485/1 fail profile) before claiming green.

---

## 3. Current Project Inventory

| Project | Size | Actual role | Verdict |
| --- | --- | --- | --- |
| `Hexalith.Parties` | 67 cs, ~8.0k lines | Domain-service host + validators + auth policies + search + **hand-rolled platform plumbing** | **Keep, shrink drastically** |
| `Hexalith.Parties.Contracts` | 139 cs, ~2.3k lines | Commands/events/VOs + **platform intrusions** (paging, freshness, key-mgmt contracts, claim extraction) | **Keep, purge technical types** |
| `Hexalith.Parties.Server` | 1 cs, 1,518 lines | **Pure domain aggregate** (24 `Handle` methods) — misnamed; unused Dapr/MediatR refs | **Merge into domain lib, delete project** |
| `Hexalith.Parties.Projections` | 18 cs, ~3.1k lines | 2 clean fold handlers + **hand-rolled Dapr actors/rebuild service** | **Keep handlers on SDK, delete actors** |
| `Hexalith.Parties.ServiceDefaults` | 1 cs, 44 lines | Thin wrapper over Commons.ServiceDefaults (forbidden project type) | **Delete** |
| `Hexalith.Parties.Authentication` | 1 cs, 90 lines | Generic OIDC→EventStore tenant-claim transformation | **Promote to platform, delete** |
| `Hexalith.Parties.Security` | 26 cs, ~3.2k lines | Generic crypto-shredding engine + Parties GDPR policy | **Split: engine → EventStore; policy stays** |
| `Hexalith.Parties.Client` | 15 cs, ~1.3k lines | Typed domain command/query/GDPR clients | **Keep, extract generic plumbing** |
| `Hexalith.Parties.Testing` | 1 cs, 466 lines | Domain test-data builders (Tenants.Testing pattern) | **Keep, restyle** |
| `Hexalith.Parties.Mcp` | 8 cs, ~1.2k lines | Domain MCP tool server + hand-rolled MCP plumbing | **Keep, rebase on FrontComposer.Mcp** |
| `Hexalith.Parties.UI` | 49 cs + 15 razor, ~5.1k | Composition host + **FrontComposer re-implementations** + E2E fixture in prod | **Split/shrink** |
| `Hexalith.Parties.AdminPortal` | 28 cs + 11 razor, ~6.5k | Admin GDPR/party screens + hand-rolled grid state/API wrapper | **Keep, simplify** |
| `Hexalith.Parties.ConsumerPortal` | 28 cs + 8 razor, ~2.7k | Consumer profile/consent/privacy pages + duplicated envelopes | **Keep, simplify** |
| `Hexalith.Parties.Picker` | 14 cs + 2 razor, ~1.4k | Party picker custom element; from-scratch combobox | **Keep, rebuild on FluentAutocomplete** |
| `Hexalith.Parties.AppHost` | 1 cs, 438 lines | Aspire test topology (allowed) + duplicated Aspire helpers + speculative publish pipeline | **Keep, simplify** |
| `samples/Hexalith.Parties.Sample` | 3 cs, ~1.0k lines | Canonical external-consumer sample (typed client + event subscriber) | **Keep, fix IDs** |

Plus 15 test projects (~60k lines total) — see Section 9.

---

## 4. Boundary Violations — platform code living in Hexalith.Parties

### 4.1 EventStore SDK bypass (largest violation, ~5,000 src lines)

| Parties code | Platform replacement |
| --- | --- |
| `src/Hexalith.Parties/Program.cs:57-66` hand-maps `/process` via bespoke `IDomainServiceInvoker` | `AddEventStoreDomainService()` / `UseEventStoreDomainService()` (`references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:47,102`) map `/process`, `/replay-state`, `/query`, `/project` |
| `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` (614 lines, reflection command resolution + allowlist cache) | SDK `DomainServiceRequestRouter` + convention discovery; crypto-shredding hook via existing `IEventPayloadProtectionService` |
| `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` (596), `PartyIndexProjectionActor.cs` (736) — raw `Dapr.Actors.Runtime.Actor` subclasses with checkpoints/reminders/rebuild gates | `IDomainProjectionHandler` + `IReadModelStore`/`ReadModelWritePolicy` (explicitly forbidden to subclass projection actors) |
| `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs` (296), `PartyIndexProjectionQueryActor.cs` (572) — hand-rolled page/pageSize wire parsing (`:311-336`) | `IDomainQueryHandler` (one per query) + `IQueryCursorCodec`/`QueryCursorScope` |
| `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs` (633) + `LocalPartyProjectionPlatformAdapter.cs` (179) — raw HttpClient at the Dapr sidecar | **Platform gap** — add rebuild/checkpoint support to the EventStore SDK first (Section 8) |
| Raw Dapr state access in 7 files (`Projections/Actors/*`, `Security/PartyErasureRecordStore.cs`, `Security/KeyOperationAuditService.cs`, `Security/TenantKeyRotationService.cs`, `Security/PartyKeyRetryActor.cs`, `Parties/Search/PartyMemoryUnitMappingStore.cs`) | `IReadModelStore` / platform persistence |
| `catch (NotImplementedException)` as Dapr-remoting control flow — `src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs` (7 sites), `Queries/PartyIndexProjectionQueryActor.cs:220,234,259` | Disappears with SDK query routing |

### 4.2 Hosting / cross-cutting (→ Commons, EventStore)

| Parties code | Target |
| --- | --- |
| `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` — 44-line forwarder + 7 redundant OTel/resilience package refs; `IsPackable=true` | **Delete project**; hosts call `AddHexalithServiceDefaults(o => …)` directly (Tenants precedent: its ServiceDefaults folder is already empty) |
| `src/Hexalith.Parties/HealthChecks/DaprSidecarHealthCheck.cs`, `DaprStateStoreHealthCheck.cs`, `DaprPubSubHealthCheck.cs`, `PartiesHealthCheckExtensions.cs` | EventStore SDK — its own `DaprStateStoreHealthCheck` doc says it "Generalizes the per-domain copies that domain modules previously hand-wrote (Epic A5)"; the Parties copies are exactly that legacy. Keep only `TenantsIntegrationHealthCheck` / `MemoriesSearchHealthCheck` (domain integrations) |
| `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs` + `src/Hexalith.Parties.Security/ICorrelationContextAccessor.cs:6` (empty sub-interface) + `CorrelationContextAccessor.cs` | `Hexalith.Commons.Http` — Commons should ship the middleware; delete all three Parties copies |
| `src/Hexalith.Parties/ErrorHandling/PartiesGlobalExceptionHandler*` / validation handler | EventStore.DomainService or Commons (generic ProblemDetails handlers) |
| In-host JWT bearer stack (`src/Hexalith.Parties/Authentication/ConfigurePartiesJwtBearerOptions.cs`, `PartiesAuthenticationOptions.cs`; wiring at `Extensions/PartiesServiceCollectionExtensions.cs:55-64`) | Likely obsolete — comments at `PartiesServiceCollectionExtensions.cs:91-114` admit the gateway owns request-path RBAC and DAPR strips the JWT. Delete after confirming gateway coverage |
| `src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs:11` — normalizes `tenants`/`tenant_id`/`tid` into `"eventstore:tenant"` claim | **Platform gap** — add `EventStoreTenantClaimsTransformation` to EventStore (or Commons.TenantAccess); nothing Parties-specific |

### 4.3 Data protection / crypto-shredding (→ EventStore)

Generic engine (move wholesale — mechanics keyed only by `(tenantId, aggregateId, version)` strings):
`PartyKeyManagementService.cs` (252), `CachedPartyKeyManagementService.cs`, `PartyKeyLifecycleService.cs`,
`LocalDevKeyStorageBackend.cs` (303), `KeyOperationAuditService.cs`, `TenantKeyRotationService.cs` (377),
`ActorBackedPartyKeyRetryScheduler.cs` + `PartyKeyRetryActor.cs` + `CryptoPendingRecord.cs`,
`DecryptionCircuitBreaker.cs` (209), `PartyPayloadProtectionService.cs` (647, `$enc` markers,
`json+pdenc-v1`) and `EventStorePartyPayloadProtectionAdapter.cs` (389) — all under
`src/Hexalith.Parties.Security/`. EventStore already ships the *contracts*
(`IEventPayloadProtectionService`, `CryptoShreddingWorkflowRequest/State/Transitions`,
`KeyReferencePolicy`, `PayloadProtectionResult`) and test fakes; Parties implements the missing
*engine*. Promote as e.g. `Hexalith.EventStore.DataProtection` (Section 8).

Contracts spillover: ~20 of the 30 files in `src/Hexalith.Parties.Contracts/Security/`
(`IKeyStorageBackend`, `TenantKeyMetadata`, `PartyKeyWrappingMetadata`, `KeyOperationAuditEntry`,
`TenantKeyRotation*`, …) are platform key-management contracts — move with the engine.

Domain policy that **stays** (retargeted onto the platform engine): `PersonalDataGraphInspector.cs`,
`PartyPersonalDataCommandGuard.cs`, `PartyErasureOrchestrator.cs`, `ErasureVerificationService.cs`,
`PartyErasureRecordStore.cs`, `LawfulBasis`, `ErasureCertificate`, consent/erasure report contracts.

### 4.4 UI plumbing (→ FrontComposer / EventStore)

| Parties code | FrontComposer capability it duplicates |
| --- | --- |
| `src/Hexalith.Parties.UI/Services/EventStoreSignalRProjectionStream.cs:31` (+ own `InfiniteRetryPolicy` :115), `PartiesProjectionSubscription.cs`, `IProjectionStream.cs` | `ProjectionSubscriptionService`, `SignalRProjectionHubConnectionFactory`, `ProjectionHubRetryPolicy` (Shell/Infrastructure/EventStore) |
| `UI/Services/ProjectionFreshnessFallback.cs:17`, `ProjectionFreshnessOptions.cs` | `State/ProjectionConnection` fallback polling driver/scheduler |
| `UI/Services/OptimisticReconcile.cs:100` | `State/ReconnectionReconciliation` + `State/PendingCommands` |
| `UI/Services/IProjectionAccessTokenProvider.cs` | `FrontComposerAccessTokenProvider` |
| `UI/Services/DegradedResponseHeaderHandler.cs`, `DegradedStateAccessor.cs` | Platform wire contract (`X-Service-Degraded`/`X-Stale-Data-Age`) → EventStore client / FC EventStore infra |
| `UI/Status/StatusKind.cs`, `StatusPresentation.cs`, `LiveRegionPoliteness.cs` | `EventStoreResponseClassifier` + `FcStatusBadge`/`FcStatusIcon` |
| `UI/IdentityBinding/*` (19 files, generic IdP-identity↔aggregate binding + `InMemoryIdentityBindingStore.cs:5` — non-durable store in prod) | FC auth-bridge pattern + durable platform persistence; Parties keeps only the `party_id` claim descriptor |
| `AdminPortal/Services/PartiesAdminListCoordinator.cs` + `AdminPortalListState/ListRequest/QueryBounds/SearchRequest/…` | `State/DataGridNavigation` (`IProjectionPageLoader`, LoadPage/Filter effects) |
| `AdminPortal/Services/AdminPortalCommandResult.cs:5` + ConsumerPortal's 8 near-identical `*Result`/`*Outcome` triples | FC `Contracts/Communication/CommandResult`, `CommandRejectionDetails`, `ProblemDetailsPayload` |
| `AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs` | EventStore.Admin.UI / FC deep-link concern |
| `Picker/Components/PartyPicker.razor:5-60` — raw `role="combobox"` markup, inline JS in `onkeydown` (`:8`), 164-line CSS, zero Fluent components | Fluent UI V5 `FluentAutocomplete`; generic picker state machine → FC reusable primitive |

### 4.5 Client plumbing (→ Commons / EventStore)

| Parties code | Target |
| --- | --- |
| `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs:11-82` — hand-rolled URI/scheme/tenant validation | `Hexalith.Commons.Http.HttpClientRegistration` (created precisely for this pattern) |
| Private `EventStoreCommandRequest` record (`HttpPartiesCommandClient.cs:268-276`, duplicated in the GDPR client) | Promote a command-submission envelope to EventStore.Client/Contracts |
| `SensitiveDetailPattern` + `SanitizeDetail` (`HttpPartiesCommandClient.cs:23-25,251-261`) | Commons.Http beside `BoundedProblemDetailsReader` |
| `Paging/PartiesPagedResultAdapter.cs` + `Contracts/Models/PagedResult.cs` | Use `Hexalith.Commons.Http.PagedResult<T>` + EventStore `ReadModelFreshness`; delete the duplicates |
| `Contracts/Models/ProjectionFreshnessMetadata.cs`, `ProjectionFreshnessStatus.cs` | SDK `ReadModelFreshness`/`ReadModelFreshnessThresholds` |
| `Contracts/Serialization/PartiesJsonOptions.cs` | Belongs beside the wire format in EventStore.Contracts (must stay case-insensitive — PascalCase wire payloads) |
| `Contracts/Authorization/PartiesClaimExtraction*` — generic ClaimsPrincipal extraction | Generic part → Commons (`Hexalith.Commons.TenantAccess`); Parties keeps claim-type names |
| `Mcp/McpContextForwardingHandler.cs`, `PartiesMcpRequestContext.cs`, `PartiesMcpToolResult.cs` | `Hexalith.FrontComposer.Mcp` (`AddFrontComposerMcp`, descriptor registry, `FrontComposerMcpUlidFactory`, result mapping) |

### 4.6 AppHost & deploy (allowed host, disallowed content)

| Item | Finding |
| --- | --- |
| `AppHost/Program.cs:56-66,77-87,150-160` hand-rolled sidecar wiring | `AddEventStoreDomainModule(...)` (`HexalithEventStoreDomainModuleExtensions.cs:46`) exists for exactly this |
| Local `WithJwtAuthentication` (`Program.cs:414-438`) + 5 repeated JWT env blocks (`:246-280`); OIDC block (`:317-338`) | `WithJwtBearerSecurity` / `WithOpenIdConnectSecurity` (`HexalithEventStoreSecurityExtensions.cs:123,185`) cover run mode; **publish-mode static-authority + multi-audience are platform gaps** (Section 8) |
| `ResolveOptionalReferenceProjectPath` (`Program.cs:399-412`) | `RepositoryProjectPaths.GetReferencedModuleProjectPath` (`RepositoryProjectPaths.cs:55`) |
| `PUBLISH_TARGET` switch (`Program.cs:345-366`) + `Aspire.Hosting.Azure.AppContainers`/`Docker`/`Kubernetes` packages | Speculative second publish pipeline, orthogonal to the real `deploy/k8s` path — delete |
| `Aspire.Hosting.Redis` | Unused (comment at `Program.cs:44-47`); remove. `Aspire.Hosting.Keycloak` likely transitive via EventStore.Aspire — verify and remove |
| Hard-coded issuer `https://auth.tache.ai/realms/tache` (`Program.cs:14`) | Deployment-specific constant → configuration |
| `KeycloakRealms/hexalith-realm.json` (11.5 KB) | Drifted fork of EventStore's canonical 9.1 KB realm — platform should support realm overlays |
| `deploy/k8s/eventstore*`, `redis/`, `falkordb/`, `memories/`, `deploy/zot/` | Platform/ops infrastructure deployed from a domain repo — move to platform/ops repos; keep `parties*`, `sample*`, `deploy/dapr` |
| `docs/kubernetes-deployment-architecture.md`, `deployment-guide.md` | Move with the deploy assets |

---

## 5. Duplication Findings (consolidation targets)

| # | Duplicate | Copies | Consolidation target |
| --- | --- | --- | --- |
| D1 | Correlation accessor/middleware | Security ×2 files + host middleware (`src/Hexalith.Parties.Security/CorrelationContextAccessor.cs`, `ICorrelationContextAccessor.cs:6`, `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs:5`) | `Hexalith.Commons.Http` (ship middleware there) |
| D2 | `Guid.NewGuid()` message/correlation-ID minting | Client ×2, Mcp ×1, Security ×3, Sample ×3 (paths in §7.2) | ULID helper in `Hexalith.Commons.UniqueIds` (currently used **nowhere** in src) |
| D3 | Raw Dapr state-store access | 7 files / 3 projects (§4.1) | `IReadModelStore` |
| D4 | Result/outcome envelope records | AdminPortal + ConsumerPortal ~8 near-identical `*Result`/`*Outcome`/`*ValidationFailure` triples (~27 DTO files each) | FC `CommandResult`/`CommandRejectionDetails`/`ProblemDetailsPayload` |
| D5 | Freshness indicator component | `UI/Components/Shared/DataFreshnessIndicator.razor` ≙ `ConsumerPortal/Components/FreshnessStatus.razor` | One shared component beside FC `FcProjectionConnectionStatus` |
| D6 | Labels/localization | 3 divergent approaches: `AdminPortalLabels.cs` (436-line hardcoded record), `ConsumerPortalLabels.cs` (resx), `PartyPickerLabels.cs` (hardcoded) | resx via FC `AddHexalithShellLocalization` |
| D7 | Freshness metadata model | `Contracts/Models/ProjectionFreshnessMetadata.cs` vs SDK `ReadModelFreshness` | EventStore.Client |
| D8 | `DaprStateStoreHealthCheck` | Parties copy vs SDK generalized version | EventStore SDK |
| D9 | EventStore command envelope | `HttpPartiesCommandClient.cs:268-276` + GDPR client copy | EventStore.Client/Contracts |
| D10 | `PagedResult<T>` | `Contracts/Models/PagedResult.cs` vs `Hexalith.Commons.Http.PagedResult<T>` (+ adapter `Paging/PartiesPagedResultAdapter.cs`) | Commons (add freshness), delete local |
| D11 | Keycloak realm JSON | AppHost fork vs EventStore canonical | EventStore realm + overlay mechanism |
| D12 | DaprComponents YAML | `AppHost/DaprComponents/*.yaml` vs `deploy/dapr/*.yaml` (10 near-parallel files, intentional local-vs-k8s variants) | Keep both but generate from one source or document dual-maintenance |
| D13 | Test helpers | `RecordingHttpMessageHandler` ×2 (AdminPortal.Tests:5, Picker.Tests:5); `RepositoryRoot` ×2 (Parties.Tests, Mcp.Tests); `StubOptionsMonitor`/`TestOptionsMonitor` ×3 in Parties.Tests | Commons/EventStore.Testing; FC testing helpers for `FakeAuthStateProvider`/`ManualTimeProvider` |
| D14 | Test tenant constants | `Testing/PartyTestData.cs:292` `DefaultTenantId = "test-tenant"` vs `Hexalith.EventStore.Testing.TestDataConstants.TenantId` | Reference EventStore.Testing |
| D15 | Client registration validation | `PartiesClientServiceCollectionExtensions.cs:11-82` vs `Commons.Http.HttpClientRegistration` | Commons |
| D16 | Six one-line `Consumer*Client` adapters in UI delegating `ISelfScopedPartiesClient` to six ConsumerPortal interfaces (e.g. `UI/Services/ConsumerProfileDataClient.cs:6`) | Collapse to one self-scoped client abstraction |

Not duplicated (keep as-is): `JsonSerializerOptions` — all 16 touchpoints correctly share
`PartiesJsonOptions.Default`.

---

## 6. Obsolete / Deprecated Code

| Item | Strategy |
| --- | --- |
| **MediatR** in `Hexalith.Parties.csproj` and `Hexalith.Parties.Server.csproj` — zero usage anywhere | Remove both refs (no code change; fitness tests already assert its *absence* elsewhere) |
| `Dapr.Client`/`Dapr.Actors` in `Parties.Server.csproj` — unused by its single file | Remove |
| `Dapr.AspNetCore` in `samples/Hexalith.Parties.Sample.csproj` — Dapr appears only in a comment (`Program.cs:25`) | Remove or actually wire up (`MapSubscribeHandler`) — decide with sample fix |
| `Aspire.Hosting.Redis` (+ probably `Aspire.Hosting.Keycloak`) in AppHost | Remove |
| Hexalith.PolymorphicSerializations solution reference — zero `[PolymorphicSerialization]`/`JsonDerivedType` usage in src | Either adopt for events or drop from `.slnx`/submodules |
| `[Obsolete]` retained-for-compat members: `Picker/Components/PartyPicker.razor:112` (`ApiBaseUrl`), `Picker/Services/PartyPickerSearchMetadata.cs:7,10,13` (+ self-suppressing `#pragma warning disable CS0618` at `:19`), `AdminPortal/Services/PartiesAdminPortalOptions.cs:5` | Schedule deletion in the Picker/AdminPortal simplification steps |
| In-host JWT stack (`ConfigurePartiesJwtBearerOptions` etc.) — vestigial per `PartiesServiceCollectionExtensions.cs:91-114` | Delete after gateway-authorization confirmation |
| Quarantined services pinned OFF the request path (`PartiesServiceCollectionExtensions.cs:93-115`): `TenantAccessService`, `DataSubjectAccessService`, consumer-policy path | Move to gateway authorization or delete, with their tests |
| E2E fixture + specimen pages in prod assemblies: `UI/Services/PartiesAdminPortalE2eFixture.cs` (16 types!), `UI/Components/Specimens/*`, fixture endpoints `UI/Program.cs:204-216` | Move to `Hexalith.Parties.Testing`/test tree; gate specimens behind FC DevMode |
| `Contracts/PartiesTextHeuristics.cs:5` — classifies 403s by sniffing "tenant" in problem-details text (used `AdminPortal/Services/PartiesAdminPortalApiClient.cs:592-620`, `Client/AdminPortal/HttpAdminPortalGdprClient.cs:155-157`) | Replace with structured gateway error code (platform gap), then delete |
| Generated BMAD docs snapshot (2026-06-02): claims 14 project folders, references removed `Hexalith.Tenants.Aspire`/`AddHexalithTenants` | Regenerate post-refactor; gitignore `project-scan-report.json` |
| 25 committed `*.csproj.lscache` files | Delete + gitignore |
| 6 `Skip=` facts `IntegrationTests/HealthChecks/HealthEndpointE2ETests.cs:37-160` (Story 12.1 deferral) | Revive or delete during migration |
| `TODO(Story 10-1.1)` `AdminPortal/Services/AdminPortalSearchResponse.cs:5`; `#pragma warning disable HXL001` `Parties/Search/PartyMemoryIndexingService.cs:57` | Resolve during respective steps |

---

## 7. Rule Violations & Over-Engineering

### 7.1 Forbidden `Guid` usage on identifiers (ULID rule) — **highest-priority correctness fix**

- **`Guid.TryParse` validation (~30 sites)**: all 22 files in `src/Hexalith.Parties/Validation/*.cs`
  (e.g. `CreatePartyValidator.cs:14`, `UpdatePartyCompositeValidator.cs:14,27,34,52,68,73,90`) and
  `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:41,561`. Consequence: **valid ULID
  aggregate IDs are rejected today.** Fix: `Ulid.TryParse` / `AggregateIdentity` rules.
- **`Guid.NewGuid()` ID minting**: `Client/HttpPartiesCommandClient.cs:158`,
  `Client/AdminPortal/HttpAdminPortalGdprClient.cs:193`, `Mcp/Tools/PartiesMcpTools.cs:977`,
  `Security/PartyKeyManagementService.cs:224`, `Security/TenantKeyRotationService.cs:35,281`,
  `samples/.../Program.cs:55,72,88`; GUID-shaped `DefaultPartyId` in `Testing/PartyTestData.cs:11`.
  (Benign non-identifier uses: `MemoriesSearchHealthCheck.cs:186`, `PartyMemoryCleanupService.cs:244`,
  `PartiesProjectionSubscription.cs:123`.)

### 7.2 Legacy Fluent v4/FAST tokens (64 occurrences, 8 stylesheets — forbidden)

`AdminPortal/Components/PartiesAdminPortal.razor.css` (17), `ConsumerPortal/Components/MyPrivacyPage.razor.css` (13),
`MyProfilePage.razor.css` (13), `ConsumerRouteShell.razor.css` (9), `CreateEditPartyPage.razor.css` (6),
`EditMyProfilePage.razor.css` (4), `MyConsentPage.razor.css` (1), `PartyGdprOperationsPanel.razor.css` (1)
— plus ~1,130 lines of custom CSS/JS across the UI group, raw JS files
(`AdminPortal/wwwroot/party-form-picker.js`, `UI/wwwroot/consumer-privacy-export.js`), and inline JS
in `PartyPicker.razor:8`. Migrate to Fluent 2 tokens / component parameters; most custom CSS
disappears when `FcPageLayout`/`FcPageHeader`/Fluent components replace raw markup
(`ConsumerPortal/Components/ConsumerRouteShell.razor:1-18`).

### 7.3 One-type-per-file violations

Worst: `samples/.../PartyEventHandler.cs` (16 types), `UI/Services/PartiesAdminPortalE2eFixture.cs` (16/9 by count method),
`Parties/Search/PartySearchBoundary.cs` (7), `PartyMemoryUnitMappingStore.cs` (4), `samples/.../CustomerSummary.cs` (4),
`AdminPortal/Services/AdminPortalCommandResult.cs` (3), `Mcp/PartiesMcpRequestContext.cs` (3),
`Security/DecryptionCircuitBreaker.cs` (3), `HealthChecks/TenantsIntegrationHealthCheck.cs` (3), ~15 more with 2–3.

### 7.4 Over-engineering (simplification proposals)

| Item | Proposal |
| --- | --- |
| `PartyDomainServiceInvoker` reflection/allowlist machinery | Delete with SDK adoption; keep only the payload-protection hook + erasure orchestration via `IEventPayloadProtectionService` |
| `IPartyProjectionPlatformAdapter` dual-mode (Local vs EventStore) + `PartyProjectionPlatformAdapterMode` leaking into prod DI (`PartiesServiceCollectionExtensions.cs:278-284`) | Delete with SDK projection path |
| `PartyProjectionUpdateOrchestrator` registered under two interfaces with same-instance gymnastics (`:117-123`) | Platform poller concern — delete |
| 320-line `AddParties` megamodule (`PartiesServiceCollectionExtensions.cs:41-363`) | Split into per-concern registrations; most content dissolves into SDK calls |
| `PartiesAdminPortalApiClient.cs` (777 lines) triple-wrapping Client interfaces + rich-search HTTP probe | Portal consumes Client + FC `IQueryService`; probe → FC capability discovery |
| Static mutable `PartyAggregate.MaxSubOperations` (`PartyAggregate.cs:19-21`) test back-door | Option/constant |
| 978-line `PartiesMcpTools` class; generic date-parsing helper (`:960-975`) | Split per tool; helper → Commons |
| `Mcp/Program.cs:31-60` two hand-built transient client factories duplicating `AddPartiesClient` | Use the extension |
| K&R braces across host/server/testing code (`PartyAggregate.cs:16`, `PartyTestData.cs`) vs declared Allman standard; `NoWarn 1591` in `Contracts.csproj:7` hiding missing XML docs | Restyle + re-enable CS1591 during touch passes |

---

## 8. Platform APIs to introduce FIRST (in technical modules)

Ordered prerequisites — each unblocks a Parties deletion:

| # | Module | New/extended API | Unblocks |
| --- | --- | --- | --- |
| P1 | **Hexalith.EventStore (SDK)** | Projection rebuild/checkpoint support equivalent to `ProjectionRebuildService`; pub/sub + projection-responsiveness health checks; degraded-response/staleness headers as SDK cross-cutting concern | Deleting Projections actors/rebuild service, host health checks, degraded middleware |
| P2 | **Hexalith.EventStore.DataProtection** (new package) | `IAggregateKeyManagementService`, `IKeyStorageBackend`, key rotation/audit/retry/circuit-breaker engine, attribute-driven payload protector implementing `IEventPayloadProtectionService` | Moving the Security engine + ~20 Contracts/Security files |
| P3 | **Hexalith.EventStore.Client/Contracts** | Command-submission envelope DTO; `ReadModelFreshness` on paged results; structured gateway error codes (replaces tenant-text sniffing) | D7, D9, D10, `PartiesTextHeuristics` deletion |
| P4 | **Hexalith.EventStore or Commons.TenantAccess** | `EventStoreTenantClaimsTransformation` (from `PartiesClaimsTransformation`) + claim-type constant | Deleting `Hexalith.Parties.Authentication` |
| P5 | **Hexalith.EventStore.Aspire** | Publish-mode `WithJwtBearerSecurity` (static authority, multi-audience `ValidAudiences__N`); publish-mode `WithOpenIdConnectSecurity` overload; `WithDomainServiceRegistration(domain, version, appId)`; Dapr-config path probing; Keycloak realm overlay | AppHost simplification |
| P6 | **Hexalith.FrontComposer** | Adopt/port: SignalR projection stream + freshness fallback + optimistic reconcile (verify FC parity before delete); degraded-state seam; generic status primitives; IdentityBinding pattern with durable store; generic entity-picker primitive; freshness indicator component; a11y skip-links in `FrontComposerShell`; reusable a11y/style-guard test helpers | UI group shrink |
| P7 | **Hexalith.Commons.Http** | Correlation middleware (accessor already exists); problem-details scrubbing helper | D1, client cleanup |
| P8 | **Hexalith.Builds** | `HexalithXxxRoot` probing props (`Directory.Build.props:4-30`), `RemoveDuplicateLoggingSourceGenerator` target (`:78-82`), `check-no-warning-override.sh` | Root build-file slimming |

Where FC already has the capability (most of P6), no platform work is needed — only adoption.

---

## 9. Test Impact Analysis

### 9.1 Tests that move with their src (platform-behavior tests)

- `Parties.Tests/HealthChecks/*` (~11 files incl. `DaprSidecarHealthCheckTests`, `DegradedResponseMiddlewareTests`, `ServiceDefaultsCompatibilityTests.cs:15`), `Middleware/CorrelationIdMiddlewareTests.cs`, `ErrorHandling/*` → EventStore/Commons suites.
- `Security.Tests` (18 files, 4.8k lines) → EventStore.DataProtection suite; keep only `PartyPersonalDataCommandGuardTests.cs` in Parties.
- `IntegrationTests/Events/*` (CloudEvents envelope, dead-letter, tenant topics) → EventStore integration suite.
- `Authentication.Tests/PartiesClaimsTransformationTests.cs:14` → platform auth suite.
- `UI.Tests` plumbing (`DegradedResponseHeaderHandlerTests`, `PartiesProjectionSubscriptionTests`, `ProjectionFreshness*`, `OptimisticReconcileTests`, a11y/style guard tests) → FC/EventStore.SignalR suites.
- `DeployValidation.Tests` (14 files, 3.8k lines) → deployment tooling home; retain slim Parties ACL route-map/topology checks (`DaprAccessControlFitnessTests.cs:68`).

### 9.2 Tests that pin the to-be-removed architecture (rewrite during migration)

- **~5.6k lines of actor/gateway tests**: `PartyDetailProjectionQueryActorTests.cs` (757), `PartyIndexProjectionQueryActorTests.cs` (911), `TenantSafeProjectionReadGuardrailsTests.cs` (820), `EventStoreGatewayRoutingTests.cs` (1279), `Projections/*ActorCorruptionTests.cs`, `ProjectionPlatformAdapterTests.cs`, `ProjectionRebuildServiceTests.cs` — port the *business assertions* (tenant guardrails, rejection handling) onto `IDomainQueryHandler`/`IDomainProjectionHandler` tests; drop actor mechanics.
- `PartyDomainServiceInvokerValidationTests.cs` (495) + `PartyDomainEventPublicationContractTests.cs` (438) → rewrite against SDK router.
- **Grep-based fitness net (~20 tests)** breaks on every project move: `ArchitecturalFitnessTests.cs` (968 lines, pins Program.cs mappings :81, csproj lists :146, quarantine boundaries :370,:409), `AppHostTenantsTopologyTests.cs` (16 facts), `ClientArchitecturalFitnessTests.cs:93`, `PartiesMcpProjectFitnessTests.cs:10,81`, `SampleOnboardingGuardrailTests.cs:80`, `PartyPickerTransportGuardrailTests.cs`, portal `Packaging/*Tests.cs`. **Update these in the same commit as each move, never after.**
- Quarantined-service tests (`TenantAccessServiceTests`, `DataSubjectAccessServiceTests`, `PartiesConsumerPolicyTests`, `HelperDrivenTenantAccessTests`) go with their services.
- `ContractsPublicApiSnapshotTests.cs:15` — regenerate snapshot when Contracts types move.

### 9.3 Domain tests to preserve (must keep passing throughout)

- `Server.Tests/Aggregates/` — **203 facts** of pure `Handle`/`Apply` (crown jewels; only namespace updates).
- `Projections.Tests/Handlers/` — 4 fold-function files, survive SDK migration nearly unchanged.
- `Contracts.Tests` serialization/state/authorization (~24 files; `PartiesJsonOptionsTests` guards case-insensitive wire reads).
- `Client.Tests` HTTP clients (549 + 683 lines), `Picker.Tests` (1,962-line component suite), portal page/auth tests, `Mcp.Tests` dispatch/contract (1,156), `Sample.Tests` (subscriber contract; note `PartyErasedHandlerPatternTests.cs:26` greps docs), `IntegrationTests/Topology/*` (legit AppHost stack tests), `Parties.Tests` Search (~90 facts)/Validation/State.

### 9.4 Verdict table

| Test project | Verdict |
| --- | --- |
| Server.Tests, Projections.Tests (handlers), Contracts.Tests, Client.Tests, Picker.Tests, AdminPortal.Tests, ConsumerPortal.Tests, Mcp.Tests, Sample.Tests | **Keep** (update greps/snapshots on moves) |
| Parties.Tests | **Split**: platform suites ← HealthChecks/Middleware/ErrorHandling; SDK rewrite ← Gateway/Projections/Domain; delete-or-move ← Authorization (quarantined); keep ← Search/Validation/State; revise per-step ← fitness/topology |
| UI.Tests | **Keep** domain; **move** plumbing tests with src |
| Security.Tests | **Move** with engine (keep command-guard tests) |
| Authentication.Tests | **Move** with transformer |
| IntegrationTests | **Split**: Topology keep; Events → EventStore; Security → moves |
| DeployValidation.Tests | **Move** to deploy tooling; keep slim domain checks |
| tests/e2e | Keep (CI-gated a11y) |

---

## 10. Keep-in-Parties vs Move-to-Technical-Module Classification

### Keep in Hexalith.Parties (domain)
- `PartyAggregate` (24 command handlers, display-name derivation) — merged into the domain library.
- All commands/events/value objects/state in Contracts (ULID-fixed validators).
- Projection fold handlers (`PartyDetailProjectionHandler`, `PartyIndexProjectionHandler`, name-history, rejection) retargeted on `IDomainProjectionHandler`.
- Query handlers (rewritten as `IDomainQueryHandler` per query) with tenant-guardrail semantics.
- GDPR domain policy: `PersonalDataGraphInspector`, `PartyPersonalDataCommandGuard`, `PartyErasureOrchestrator`, `ErasureVerificationService`, erasure records/certificates, `LawfulBasis`, consent contracts.
- Authorization policies (Admin/Consumer), `PartyIdClaimResolver`, Parties claim-type names.
- Party search services (Basic/LocalFuzzy/Semantic, Memories integration) + `TenantsIntegrationHealthCheck`, `MemoriesSearchHealthCheck`.
- Typed clients (`IPartiesCommandClient`, `IPartiesQueryClient`, GDPR client), MCP tool definitions, `PartyTestData`, domain UI screens (GDPR panels, Create/Edit party, MyProfile/MyConsent/MyPrivacy, `PartyStateBadge`), Picker custom-element contract + party data source, FC manifest/nav registration, AppHost (thinned), Sample, domain docs.

### Move to technical modules
- **Hexalith.EventStore**: crypto-shredding engine + key-mgmt contracts (P2); tenant claims transformation (P4); Dapr health checks; ProblemDetails handlers; command envelope, freshness/paged models, structured error codes (P3); Aspire security/module helpers (P5); rebuild/checkpoint SDK support (P1); realm overlay.
- **Hexalith.Commons**: correlation middleware (+delete Parties accessors); problem-details scrubbing; `HttpClientRegistration` adoption; ULID minting via Commons.UniqueIds; generic claim extraction (TenantAccess).
- **Hexalith.FrontComposer**: SignalR projection stream/freshness/reconcile/token provider (adopt existing FC types); degraded-state seam; status primitives; IdentityBinding pattern; grid/list state (adopt `DataGridNavigation`); result envelopes (adopt `CommandResult`); generic picker primitive; freshness component; skip-links; MCP context/result plumbing (FrontComposer.Mcp); style/a11y guard test helpers.
- **Hexalith.Builds**: root-props probing, logging-generator dedup target, warning-override guard script.
- **Platform/ops repos**: `deploy/k8s/{eventstore*,redis,falkordb,memories}`, `deploy/zot`, platform deployment docs, generic deploy-validation tooling.

### Delete outright
- `Hexalith.Parties.ServiceDefaults` (project), `Hexalith.Parties.Authentication` (project, after P4), `Hexalith.Parties.Server` (project shell, after merging the aggregate), projection/query actors + rebuild service + platform adapters + `PartyDomainServiceInvoker`, in-host JWT stack (after gateway confirmation), quarantined services, MediatR/unused Dapr/Redis package refs, `PartiesTextHeuristics`, E2E fixture & specimens from prod, `[Obsolete]` members, `PUBLISH_TARGET` switch, lscache files.

---

## 11. Risks, Migration Order & Compatibility Concerns

| Risk | Mitigation |
| --- | --- |
| **ULID fix is a behavior change**: validators currently *reject* ULIDs and *accept* GUIDs; existing stored aggregates have GUID-shaped IDs (`PartyTestData` default is GUID-shaped) | Accept any non-whitespace string per `AggregateIdentity` rules (don't swap one over-strict parse for another); keep GUID-shaped IDs valid for replay compatibility |
| **Wire compatibility**: EventStore payloads are PascalCase/numeric-enum | Any serialization move must preserve `PropertyNameCaseInsensitive=true`; `PartiesJsonOptionsTests` + `Sample.Tests` tolerant-deserialization tests are the guard |
| **Projection state migration**: hand-rolled actors own checkpoints/state under their own Dapr keys; SDK `IReadModelStore` may use different key shapes | Requires either a rebuild-from-stream cutover (preferred — event sourcing makes it safe) or a key-migration shim; plan a full projection rebuild as part of Step 6 |
| **SDK feature parity**: rebuild service, corruption handling, degraded headers, freshness thresholds exist only in Parties today | P1 platform work first; port the business assertions from `ProjectionRebuild*Tests`/`*CorruptionTests` into SDK tests |
| **Security engine move changes NuGet surface**: `Hexalith.Parties.Contracts` currently ships key-mgmt contracts publicly | Coordinate a major-version bump (`feat!:`); keep type-forwarders or an obsolete-shim release if external consumers exist |
| **Grep-based fitness tests** break on every move | Update in the same commit as each move; never batch |
| **Multi-repo coordination**: P1–P8 land in 4 different repos with submodule pins | One platform repo per PR, bump the submodule pin in Parties, then do the dependent Parties step; keep `main` green between steps |
| **Cross-suite flake traps** (parallel build, MinVer skew, pack red) | `-m:1` clean builds, `-p:MinVerVersionOverride=1.0.0` on rebuilt subsets, never treat pack/`*PackageTests` red as regression |
| **Keycloak realm fork drift** | Land realm-overlay support (P5) before touching auth topology |

**Ordering principle**: platform-first, then leaf projects (ServiceDefaults, Authentication,
Server-merge — cheap, independent), then the big host/projection cutover (single vertical change),
then UI consolidation, then deploy/docs cleanup. The ULID fix and dead-reference removals are
independent and can go first.

---

## 12. Ordered Action Plan

### Phase 0 — Baseline (plan step 1)
1. `dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx -c Release -m:1` — record warnings.
2. Run each test EXE directly; record the pass/fail profile (expect 485/1 on Parties.Tests, pack suites red).
3. Fix `scripts/test.ps1` first (add `ConsumerPortal.Tests` lane; remove solution-level `dotnet test` lanes) so the gate is trustworthy for the whole migration.

### Phase 1 — Zero-risk hygiene (steps 2–3, no behavior change)
4. Remove dead package refs: MediatR ×2, `Dapr.Client`/`Dapr.Actors` from Server.csproj, `Aspire.Hosting.Redis` (verify Keycloak), decide `Dapr.AspNetCore` in Sample.
5. Delete 25 `*.csproj.lscache` + gitignore; gitignore `docs/project-scan-report.json`.
6. Resolve PolymorphicSerializations: adopt for events or remove from `.slnx`.
7. One-type-per-file splits + Allman restyle on files being kept (`PartySearchBoundary.cs`, sample files, etc.); re-enable CS1591 in Contracts and document.

### Phase 2 — Identifier correctness (independent, high value)
8. Replace all ~30 `Guid.TryParse` validations (22 validators + `PartyAggregate.cs:41,561`) with `AggregateIdentity`/ULID rules; replace 8 `Guid.NewGuid()` mint sites with Commons.UniqueIds; fix `PartyTestData.DefaultPartyId`; keep GUID-shaped IDs accepted for replay compatibility. Update validator tests.

### Phase 3 — Platform prerequisites (steps 4–5; per-repo PRs P1–P8 of Section 8)
9. EventStore: SDK rebuild/checkpoints + health checks + degraded headers (P1); DataProtection package (P2); client envelope/freshness/error codes (P3); tenant claims transformation (P4); Aspire publish-mode helpers (P5).
10. Commons: correlation middleware + scrubbing (P7). FrontComposer: verify parity, add the genuinely-new pieces (P6). Builds: shared props/targets/script (P8).
11. Bump submodule pins in Parties (`chore(deps): bump …`) as each lands.

### Phase 4 — Leaf-project migrations (step 6)
12. **Delete `Hexalith.Parties.ServiceDefaults`**: hosts call `AddHexalithServiceDefaults(o => …)` inline; delete `ServiceDefaultsCompatibilityTests`; fix `K8sManifestPublishTests.cs:118`.
13. **Delete `Hexalith.Parties.Authentication`**: consume the platform transformation (P4); move its test.
14. **Merge `PartyAggregate` into the domain library**, delete `Hexalith.Parties.Server`; remove `MaxSubOperations` static; rename Server.Tests accordingly; update all fitness greps in the same commit.
15. **Move the Security engine** to EventStore.DataProtection (P2) with its 17 test files; retarget kept GDPR policy classes onto the platform engine; move ~20 `Contracts/Security` platform contracts; `feat!:` version bump; regenerate contracts API snapshot.
16. **Client cleanup**: `HttpClientRegistration`, platform command envelope, scrubbing helper, delete `PagedResult` duplicate + adapter; update Client fitness tests.
17. **Mcp cleanup**: adopt FrontComposer.Mcp plumbing, drop ServiceDefaults dep, use `AddPartiesClient`, split the 978-line tool class; update `PartiesMcpProjectFitnessTests`.

### Phase 5 — Host & projection cutover to the EventStore SDK (steps 6–7, the big one)
18. Rewrite `src/Hexalith.Parties/Program.cs` to the two-line SDK shape + domain registrations only.
19. Re-home projection folds as `IDomainProjectionHandler`; persist via `IReadModelStore`/`ReadModelWritePolicy`; delete both actors, rebuild service, platform adapters, orchestrator double-registration.
20. Rewrite query actors as per-query `IDomainQueryHandler` + `IQueryCursorCodec`; port tenant-guardrail assertions.
21. Delete `PartyDomainServiceInvoker` (keep payload-protection hook), correlation middleware (use Commons), Dapr health checks (use SDK), in-host JWT stack + quarantined services (after gateway confirmation); split `AddParties` remnants per concern.
22. Execute a **full projection rebuild** in the AppHost stack to cut over read-model state; verify freshness/degraded behavior end-to-end via `aspire run`.
23. Rewrite/port the ~5.6k lines of pinned tests (Section 9.2) alongside each sub-step; revise `ArchitecturalFitnessTests` to pin the *new* invariants (SDK usage, no raw Dapr, no actors).

### Phase 6 — UI consolidation (step 7 continued)
24. `Parties.UI`: swap local projection stream/freshness/reconcile/token provider for FC types; move degraded seam; delete `Consumer*Client` adapters; move E2E fixture + specimens to Testing/tests; fold Program.cs residue into FC options; adopt durable IdentityBinding.
25. Portals: adopt FC `CommandResult` envelopes, `DataGridNavigation`, `FcPageLayout`/`FcPageHeader`; shrink `PartiesAdminPortalApiClient`; converge labels on resx; dedupe freshness component; purge all 64 legacy tokens + shrink custom CSS; split the 1,825-line `PartiesAdminPortal.razor`.
26. Picker: rebuild internals on `FluentAutocomplete`; remove inline JS + `[Obsolete]` members; keep the custom-element contract.
27. Update bUnit suites as components change; run SSR a11y specs locally, interactive gate in CI.

### Phase 7 — AppHost & deploy (step 6 tail)
28. AppHost: adopt `AddEventStoreDomainModule`, `WithJwtBearerSecurity`/`WithOpenIdConnectSecurity` (incl. new publish-mode overloads), `WithDomainServiceRegistration`, path helpers; delete `PUBLISH_TARGET` switch + Azure/Docker/K8s packages; externalize the tache.ai issuer; realm overlay.
29. Move platform deploy assets (`eventstore*`, `redis`, `falkordb`, `memories`, `zot`) + platform deployment docs out; move generic DeployValidation tooling; keep slim Parties ACL/topology checks; document DaprComponents dual-maintenance.
30. Move `Directory.Build.props` probing + logging target + guard script to Hexalith.Builds; remove FrontComposer warning suppressions from `Directory.Build.targets` after upstream fixes; narrow the NU5118/NU5128 NoWarn scope.

### Phase 8 — Obsolete removal, docs, samples (steps 8–9)
31. Delete remaining `[Obsolete]` members, retirement-guard tests made unexpressible, skipped E2E facts (revive or drop), `PartiesTextHeuristics` (after P3 error codes).
32. Sample: ULID IDs, move the 50-line MCP comment block to docs, wire or drop Dapr.
33. Regenerate BMAD docs set; refresh README/docs to the new project list; keep domain docs (GDPR set, event pub/sub, picker) updated — mind `PartyErasedHandlerPatternTests` doc greps.

### Phase 9 — Verification (steps 10–11)
34. Per-project test EXE runs (`-class`/`-method` filters as needed) after each phase; `dotnet restore` + `dotnet build Hexalith.Parties.slnx -c Release -m:1` at each phase end; `-p:MinVerVersionOverride=1.0.0` on rebuilt subsets.
35. `aspire run` topology smoke: gateway command → `/process` → event → projection → query → UI freshness; MCP tool round-trip; Sample subscriber round-trip.
36. Final checklist below.

### Final verification checklist
- [ ] `dotnet build Hexalith.Parties.slnx -c Release -m:1` — zero warnings (TreatWarningsAsErrors).
- [ ] Every test EXE green except the documented pre-existing pack reds; `AppHostTenantsTopologyTests` fixed or still the only known red — no *new* failures vs baseline.
- [ ] `grep -r "AddEventStoreDomainService" src/Hexalith.Parties/Program.cs` — present; Program.cs ≤ ~20 lines.
- [ ] Zero hits in `src/` for: `Guid.TryParse` on IDs, `Guid.NewGuid` ID minting, `Dapr.Actors.Runtime.Actor` subclasses, `--neutral-|--accent-|--type-ramp-|--palette-` tokens, `MediatR`, `catch (NotImplementedException)`.
- [ ] `IDomainProjectionHandler`/`IDomainQueryHandler`/`IReadModelStore`/`IQueryCursorCodec` are the only projection/query mechanisms.
- [ ] Projects deleted: ServiceDefaults, Authentication, Server; no `*.ServiceDefaults` in the module.
- [ ] `.slnx` updated; no legacy `.sln`; fitness tests pin the new architecture.
- [ ] Submodule pins current; all commits Conventional (`npx commitlint --last --verbose`).
- [ ] Full projection rebuild executed and verified against aggregate replay.
- [ ] Docs regenerated; deploy tree contains only Parties-owned assets.

---

*Sources: six parallel deep-analysis passes over src/, tests/, samples/, deploy/, docs/, root build
files, and the four platform submodules (Commons, EventStore, FrontComposer, Tenants), 2026-07-06.*
