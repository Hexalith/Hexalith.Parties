# Hexalith.Parties — Domain-Focus Refactoring Analysis & Action Plan

> Analysis date: 2026-07-06 · Branch: `main` (clean, HEAD `fd94736`)
> Scope: keep Hexalith.Parties strictly domain-focused; move reusable/cross-cutting code to
> Hexalith.Commons / Hexalith.EventStore / Hexalith.FrontComposer (or other technical modules).
> No changes have been implemented — this is the analysis and roadmap only.

**Revision note (this version).** Re-verified at HEAD `fd94736` by four independent read-only
verification passes over Parties `src/`/`tests/` and the platform submodules at Parties' current
pins (EventStore `a592bbd`, Commons `275edc0`, FrontComposer checked at pin `92edc30`). Since the
previous baseline (`88d984b`) the source tree (`src/`, `tests/`, `samples/`, `deploy/`, `docs/`,
`scripts/`, `.slnx`) is **byte-identical**; only two root build files changed and five submodule
pins were bumped. Material updates in this revision:

- **Dependency mode flipped**: `Directory.Build.props` now defaults `UseHexalithProjectReferences`
  to `false` for *all* configurations (including Debug) — local builds consume **NuGet packages**
  by default; project-reference builds are opt-in (`-p:UseHexalithProjectReferences=true`, which
  requires the nested `references/` submodules to be initialized).
- **Three platform gaps closed at the new EventStore pin** (P3 command envelope, P4 tenant claims
  transformation, and most of P1 rebuild/checkpoints) — see Section 8; the plan's platform-first
  phase shrinks accordingly and `Hexalith.Parties.Authentication` is deletable today.
- **One prior claim retracted**: the `AppHostTenantsTopologyTests` "literal path vs MSBuild
  variable" mismatch does not exist in the tree (test and csproj both use
  `$(HexalithEventStoreRoot)\src\…`); the cause of the previously recorded single test failure
  must be re-diagnosed when the baseline is re-run.
- Counts corrected: 18 validator files / 31 `Guid.TryParse` occurrences (was "22 files/~30"),
  74 legacy CSS tokens (was 64), 12 ConsumerPortal envelope types (was ~8), 18 IdentityBinding
  files (was 19), 9 types in the E2E fixture (was 16), 7 health-check files (was 6), 18 of 30
  `Contracts/Security` files are platform key-management contracts (was ~20).
- FrontComposer **already ships shell skip-links** (`fc-skip-link`, present at Parties' pin) —
  removed from the platform-gap list. The generic entity-picker gap is confirmed real.

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
- **A generic crypto-shredding engine lives inside the domain**: ~3,200 lines of key management,
  payload encryption, key rotation, audit and circuit-breaker code in `Hexalith.Parties.Security`
  (plus 18 platform contracts in `Contracts/Security/`) implement EventStore's own
  `IEventPayloadProtectionService` hook. Nothing but the erasure *policy* is Parties-specific.
- **The UI re-implements FrontComposer**: SignalR projection subscription, fallback polling,
  optimistic reconciliation, token provider, degraded-state handling, grid/list state machines,
  result envelopes, and a from-scratch combobox (Picker) all duplicate shipped FrontComposer /
  Fluent UI V5 capabilities — every duplicated FC capability is confirmed present at Parties'
  pinned FC commit. 8 stylesheets carry 74 forbidden legacy v4/FAST tokens.
- **Systemic identifier rule violation**: 31 `Guid.TryParse` validations on aggregate IDs (all
  18 validators + `PartyAggregate`) — meaning **ULID-formatted IDs are rejected today** — plus 9
  `Guid.NewGuid()` sites minting message/correlation IDs. Both are forbidden (ULID rule).
- **Dead weight**: MediatR referenced by two projects with zero usage; `Dapr.Client`/`Dapr.Actors`
  unused in `Parties.Server`; `Aspire.Hosting.Redis` **and** `Aspire.Hosting.Keycloak` unused in
  the AppHost (no `AddRedis`/`AddKeycloak` call); Hexalith.PolymorphicSerializations referenced by
  the solution but used nowhere; 25 committed `*.csproj.lscache` files.

**Estimated net effect of the plan**: `src/` shrinks by roughly 40–50% (≈15,000+ lines of platform
plumbing deleted or moved), the project count drops from 15 to ~10, and the module converges on the
platform contract: aggregate + contracts + projections handlers + query handlers + validators +
domain UI + domain clients + a thin AppHost.

**Sequencing constraint (reduced since last revision)**: some platform gaps must still be filled in
the technical modules **first** (Section 8) — the data-protection engine (P2), publish-mode
JWT/OIDC helpers in EventStore.Aspire (P5), SDK surfacing of the now-existing rebuild machinery
(P1 residue), correlation middleware in Commons (P7) — but the command envelope (P3) and tenant
claims transformation (P4) already exist at the current EventStore pin and only need adoption.

---

## 2. Baseline Build/Test Status (established facts)

| Fact | Status |
| --- | --- |
| Dependency mode | **Default is now NuGet packages for all configurations** (`Directory.Build.props` since `57ace9d`/`fd94736`); project-reference builds require `-p:UseHexalithProjectReferences=true` + initialized nested submodules (not initialized in this workspace — do not initialize them by default) |
| Clean parallel builds | Flake with CS0006/MSB4018 (Rebuild race / StaticWebAssets lock) — use `-m:1` for verdicts |
| `dotnet pack` / all `*PackageTests` | **Pre-existing red** (NU5118/NU5128, `process.ExitCode`) — not a regression signal |
| `Hexalith.Parties.Tests` host EXE | Last recorded profile: 485 total, 1 failure. **The previously documented cause is retracted** — `AppHostTenantsTopologyTests.cs:14-22` asserts `$(HexalithEventStoreRoot)\src\…`, exactly matching `Hexalith.Parties.AppHost.csproj:12-15`; no literal-path mismatch exists. Re-run and re-diagnose in Phase 0 |
| xUnit v3 (MTP) | `dotnet test --filter` broken — run test EXEs directly with `-class`/`-method` |
| MinVer | No git tags in worktree → rebuild subsets with `-p:MinVerVersionOverride=1.0.0` or mass `FileNotFoundException` |
| e2e a11y gate | Cannot fully pass locally (interactive Blazor dead in WSL sandbox); SSR specs via `PLAYWRIGHT_SKIP_WEBSERVER=1`, interactive gate in `ui-a11y` CI |
| `scripts/test.ps1` | **Bug**: `Hexalith.Parties.ConsumerPortal.Tests` is in no targeted lane (unit=10, integration=2, topology=1, deploy=1 projects) — it runs only indirectly via the solution-level `all`/`coverage` lanes (line 58/61), which themselves use the forbidden solution-level `dotnet test` |
| Wire format | EventStore serializes event payloads PascalCase/numeric enums — Parties readers must stay case-insensitive (`PartiesJsonOptions.Default`) |

Any migration step must re-establish an exact recorded baseline in Phase 0 before claiming green.

---

## 3. Current Project Inventory

| Project | Size | Actual role | Verdict |
| --- | --- | --- | --- |
| `Hexalith.Parties` | 67 cs, ~8.0k lines | Domain-service host + validators + auth policies + search + **hand-rolled platform plumbing** | **Keep, shrink drastically** |
| `Hexalith.Parties.Contracts` | 139 cs, ~2.3k lines | Commands/events/VOs + **platform intrusions** (paging, freshness, key-mgmt contracts, claim extraction) | **Keep, purge technical types** |
| `Hexalith.Parties.Server` | 1 cs, 1,518 lines | **Pure domain aggregate** (24 `Handle` methods) — misnamed; unused Dapr/MediatR refs | **Merge into domain lib, delete project** |
| `Hexalith.Parties.Projections` | 18 cs, ~3.1k lines | 2 clean fold handlers + **hand-rolled Dapr actors/rebuild service** | **Keep handlers on SDK, delete actors** |
| `Hexalith.Parties.ServiceDefaults` | 1 cs, 44 lines | Thin wrapper over Commons.ServiceDefaults (forbidden project type) | **Delete** |
| `Hexalith.Parties.Authentication` | 1 cs, 90 lines | Generic OIDC→EventStore tenant-claim transformation | **Delete — platform equivalent now exists (P4 closed)** |
| `Hexalith.Parties.Security` | 26 cs, 3,195 lines | Generic crypto-shredding engine + Parties GDPR policy | **Split: engine → EventStore; policy stays** |
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
| `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs` (633) + `LocalPartyProjectionPlatformAdapter.cs` (179) — raw HttpClient at the Dapr sidecar | **Mostly closed at current pin**: EventStore now ships `ProjectionRebuildCheckpoint/Operation/Status` (Contracts/Streams), `IProjectionRebuildCheckpointStore` + `ProjectionUpdateOrchestrator` + `ActiveRebuildIndexCleanupService` (Server), and `AdminProjectionRebuildController` (gateway). Residual gap: none of it is surfaced through the **DomainService SDK** package — see P1 |
| Raw Dapr state access in 7 files (`Projections/Actors/*`, `Security/PartyErasureRecordStore.cs`, `Security/KeyOperationAuditService.cs`, `Security/TenantKeyRotationService.cs`, `Security/PartyKeyRetryActor.cs`, `Parties/Search/PartyMemoryUnitMappingStore.cs`) | `IReadModelStore` / platform persistence |
| `catch (NotImplementedException)` as Dapr-remoting control flow — `src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs` (7 sites: lines 23-137), `Queries/PartyIndexProjectionQueryActor.cs:220,234,259` | Disappears with SDK query routing |

### 4.2 Hosting / cross-cutting (→ Commons, EventStore)

| Parties code | Target |
| --- | --- |
| `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` — 44-line forwarder + 7 redundant OTel/resilience package refs; `IsPackable=true` | **Delete project**; hosts call `AddHexalithServiceDefaults(o => …)` directly (Tenants precedent: its ServiceDefaults folder is already empty) |
| `src/Hexalith.Parties/HealthChecks/` — 7 files: `DaprSidecarHealthCheck.cs`, `DaprStateStoreHealthCheck.cs`, `DaprPubSubHealthCheck.cs`, `ProjectionActorsHealthCheck.cs`, `PartiesHealthCheckExtensions.cs` (+ 2 domain checks) | EventStore SDK — its own `DaprStateStoreHealthCheck` doc says it "Generalizes the per-domain copies that domain modules previously hand-wrote (Epic A5)"; the Parties copies are exactly that legacy. `ProjectionActorsHealthCheck` dies with the actors. Keep only `TenantsIntegrationHealthCheck` / `MemoriesSearchHealthCheck` (domain integrations) |
| `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs` + `src/Hexalith.Parties.Security/ICorrelationContextAccessor.cs` (empty sub-interface) + `CorrelationContextAccessor.cs` | `Hexalith.Commons.Http` — Commons already has `CorrelationContextAccessor`/`HttpCorrelation` but **no middleware** (verified at pin `275edc0`); ship the middleware there, delete all three Parties copies |
| `src/Hexalith.Parties/ErrorHandling/PartiesGlobalExceptionHandler*` / validation handler | EventStore.DomainService or Commons (generic ProblemDetails handlers) |
| In-host JWT bearer stack (`src/Hexalith.Parties/Authentication/ConfigurePartiesJwtBearerOptions.cs`, `PartiesAuthenticationOptions.cs`; wiring at `Extensions/PartiesServiceCollectionExtensions.cs:55-64`) | Likely obsolete — comments at `PartiesServiceCollectionExtensions.cs:92-102` admit the gateway owns request-path RBAC and DAPR strips the JWT. Delete after confirming gateway coverage |
| `src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs` (90 lines) — normalizes `tenants`/`tenant_id`/`tid` into `"eventstore:tenant"` claim | **Gap closed**: `EventStoreClaimsTransformation` (115 lines) now exists at `references/Hexalith.EventStore/src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs` with identical claim mapping. Adopt and delete the project. Caveat: it lives in the gateway package (`Hexalith.EventStore`), not DomainService — if the Parties host still needs it locally, ask EventStore to expose it from a shared package rather than copying |

### 4.3 Data protection / crypto-shredding (→ EventStore)

Generic engine (move wholesale — mechanics keyed only by `(tenantId, aggregateId, version)` strings):
`PartyKeyManagementService.cs` (252), `CachedPartyKeyManagementService.cs` (126),
`PartyKeyLifecycleService.cs` (72), `LocalDevKeyStorageBackend.cs` (303),
`KeyOperationAuditService.cs` (59), `TenantKeyRotationService.cs` (377),
`ActorBackedPartyKeyRetryScheduler.cs` + `PartyKeyRetryActor.cs` + `CryptoPendingRecord.cs`,
`DecryptionCircuitBreaker.cs` (209), `PartyPayloadProtectionService.cs` (647 — `$enc` marker,
`json+pdenc-v1` format) and `EventStorePartyPayloadProtectionAdapter.cs` (389) — all under
`src/Hexalith.Parties.Security/`. EventStore already ships the *contracts*
(`IEventPayloadProtectionService`, `CryptoShreddingWorkflowRequest/State/Transitions`,
`KeyReferencePolicy`, `PayloadProtectionResult`) and test fakes, but at the current pin its only
implementations are `NoOpEventPayloadProtectionService` and a testing fake — the *engine* still
lives only in Parties (the new `EventStoreDataProtection*` key ring in DomainService backs only
the query-cursor codec, not payload protection). Promote as e.g.
`Hexalith.EventStore.DataProtection` (Section 8, P2).

Contracts spillover: 18 of the 30 files in `src/Hexalith.Parties.Contracts/Security/` are platform
key-management contracts (`IKeyStorageBackend`, `IPartyKeyManagementService`, `TenantKeyMetadata`,
`PartyKeyWrappingMetadata`, `KeyOperationAuditEntry`, `TenantKeyRotation*`, `CryptoShreddingOptions`,
`EncryptionAlgorithm`, …) — move with the engine. The other 12 are GDPR/erasure domain contracts
(`ErasureCertificate`, `ErasureVerification*`, `LawfulBasis`, `IPersonalDataCommandGuard`, …) — keep.

Domain policy that **stays** (retargeted onto the platform engine): `PersonalDataGraphInspector.cs`,
`PartyPersonalDataCommandGuard.cs`, `PartyErasureOrchestrator.cs`, `ErasureVerificationService.cs`,
`PartyErasureRecordStore.cs`, `LawfulBasis`, `ErasureCertificate`, consent/erasure report contracts.

### 4.4 UI plumbing (→ FrontComposer / EventStore)

All FC capabilities cited below are verified present at Parties' pinned FC commit `92edc30` —
adoption requires no FC platform work except where marked as a gap in Section 8.

| Parties code | FrontComposer capability it duplicates |
| --- | --- |
| `src/Hexalith.Parties.UI/Services/EventStoreSignalRProjectionStream.cs` (+ own `InfiniteRetryPolicy` :115), `PartiesProjectionSubscription.cs`, `IProjectionStream.cs` | `ProjectionSubscriptionService`, `SignalRProjectionHubConnectionFactory`, `ProjectionHubRetryPolicy` (Shell/Infrastructure/EventStore) |
| `UI/Services/ProjectionFreshnessFallback.cs`, `ProjectionFreshnessOptions.cs` | `State/ProjectionConnection` fallback polling driver/scheduler |
| `UI/Services/OptimisticReconcile.cs` (3 types) | `State/ReconnectionReconciliation` + `State/PendingCommands` |
| `UI/Services/IProjectionAccessTokenProvider.cs` | `FrontComposerAccessTokenProvider` |
| `UI/Services/DegradedResponseHeaderHandler.cs`, `DegradedStateAccessor.cs` | **Adopt the SDK contract instead of promoting the header**: EventStore models staleness/degradation as body metadata — `QueryResponseMetadata.IsStale/IsDegraded` (Contracts/Queries) + `QueryProblemReasonCodes.ProjectionStale`, populated by the gateway `QueriesController`; no `X-Service-Degraded` header exists platform-side. Replace the header pipeline with the metadata contract |
| `UI/Status/StatusKind.cs`, `StatusPresentation.cs`, `LiveRegionPoliteness.cs` | `EventStoreResponseClassifier` + `FcStatusBadge`/`FcStatusIcon` |
| `UI/IdentityBinding/*` (18 files, generic IdP-identity↔aggregate binding + `InMemoryIdentityBindingStore.cs` — non-durable store in prod) | FC auth-bridge pattern + durable platform persistence; Parties keeps only the `party_id` claim descriptor |
| `AdminPortal/Services/PartiesAdminListCoordinator.cs` + `AdminPortalListState/ListRequest/QueryBounds/SearchRequest/…` | `State/DataGridNavigation` (`IProjectionPageLoader`, LoadPage/Filter effects) |
| `AdminPortal/Services/AdminPortalCommandResult.cs` (3 types) + ConsumerPortal's 12 `*Result`/`*Outcome`/`*ValidationFailure` envelope types | FC `Contracts/Communication/CommandResult`, `CommandRejectionDetails`, `ProblemDetailsPayload` |
| `AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs` | EventStore.Admin.UI / FC deep-link concern |
| `Picker/Components/PartyPicker.razor` (705 lines) — raw `role="combobox"` (:12), inline JS in `onkeydown` (:8), 164-line CSS, zero Fluent components | Fluent UI V5 `FluentAutocomplete`; generic picker state machine → FC reusable primitive (confirmed FC gap — Section 8 P6) |

### 4.5 Client plumbing (→ Commons / EventStore)

| Parties code | Target |
| --- | --- |
| `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs:11-82` — hand-rolled URI/scheme/tenant validation | `Hexalith.Commons.Http.HttpClientRegistration` (created precisely for this pattern) |
| Private `EventStoreCommandRequest` record (`HttpPartiesCommandClient.cs:270-278`, duplicated at `HttpAdminPortalGdprClient.cs:393`) | **Gap closed**: adopt `SubmitCommandRequest`/`CommandEnvelope`/`SubmitCommandResponse` (EventStore.Contracts/Commands) via `IEventStoreGatewayClient.SubmitCommandAsync` (EventStore.Client/Gateway) |
| `SensitiveDetailPattern` + `SanitizeDetail` (`HttpPartiesCommandClient.cs:23-25,250-260`) | Commons.Http beside `BoundedProblemDetailsReader` |
| `Paging/PartiesPagedResultAdapter.cs` + `Contracts/Models/PagedResult.cs` | Use `Hexalith.Commons.Http.PagedResult<T>` + EventStore `ReadModelFreshness`; delete the duplicates |
| `Contracts/Models/ProjectionFreshnessMetadata.cs`, `ProjectionFreshnessStatus.cs` | SDK `ReadModelFreshness`/`ReadModelFreshnessThresholds` |
| `Contracts/PartiesJsonOptions.cs` (Contracts root) | Belongs beside the wire format in EventStore.Contracts (must stay case-insensitive — PascalCase wire payloads) |
| `Contracts/Authorization/PartiesClaimExtraction*` — generic ClaimsPrincipal extraction | Generic part → Commons (`Hexalith.Commons.TenantAccess`); Parties keeps claim-type names (`PartiesClaimTypes`, `PartiesRoles`) |
| `Mcp/McpContextForwardingHandler.cs`, `PartiesMcpRequestContext.cs` (3 types), `Tools/PartiesMcpToolResult.cs` | `Hexalith.FrontComposer.Mcp` (`AddFrontComposerMcp`, descriptor registry, `FrontComposerMcpUlidFactory`, result mapping) |

### 4.6 AppHost & deploy (allowed host, disallowed content)

| Item | Finding |
| --- | --- |
| `AppHost/Program.cs:56-66,77-87,150-160` hand-rolled sidecar wiring | `AddEventStoreDomainModule(...)` (`HexalithEventStoreDomainModuleExtensions.cs:46`) exists for exactly this |
| Local `WithJwtAuthentication` (`Program.cs:415-438`, call sites :246,251,259,266,275) + OIDC block (`:317-338`) | `WithJwtBearerSecurity` / `WithOpenIdConnectSecurity` (`HexalithEventStoreSecurityExtensions.cs:123,185`) cover run mode; **publish-mode static-authority + multi-audience remain platform gaps** — verified: `WithJwtBearerSecurity` has no `IsPublishMode` branch and single-audience only (Section 8, P5) |
| `ResolveOptionalReferenceProjectPath` (`Program.cs:399-412`) | `RepositoryProjectPaths.GetReferencedModuleProjectPath` (`RepositoryProjectPaths.cs:55`) |
| `PUBLISH_TARGET` switch (`Program.cs:345-367`) + `Aspire.Hosting.Azure.AppContainers`/`Docker`/`Kubernetes` packages | Speculative second publish pipeline, orthogonal to the real `deploy/k8s` path — delete |
| `Aspire.Hosting.Redis` **and** `Aspire.Hosting.Keycloak` | Both confirmed unused at composition level — Program.cs contains no `AddRedis`/`AddKeycloak` call (Redis intentionally external per comment :44-47; Keycloak composed by EventStore.Aspire). Remove both package refs |
| Hard-coded issuer `https://auth.tache.ai/realms/tache` (`Program.cs:14`, `PublishModeJwtIssuer`) | Deployment-specific constant → configuration |
| `KeycloakRealms/hexalith-realm.json` (11.5 KB) | Drifted fork of EventStore's canonical 9.1 KB realm — platform should support realm overlays |
| `deploy/k8s/eventstore*`, `redis/`, `falkordb/`, `memories/`, `deploy/zot/` | Platform/ops infrastructure deployed from a domain repo — move to platform/ops repos; keep `parties*`, `sample*`, `deploy/dapr` |
| `docs/kubernetes-deployment-architecture.md`, `deployment-guide.md` | Move with the deploy assets |

---

## 5. Duplication Findings (consolidation targets)

| # | Duplicate | Copies | Consolidation target |
| --- | --- | --- | --- |
| D1 | Correlation accessor/middleware | Security ×2 files + host middleware (`src/Hexalith.Parties.Security/CorrelationContextAccessor.cs`, `ICorrelationContextAccessor.cs`, `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs`) — name-duplicating Commons.Http's own accessor pair | `Hexalith.Commons.Http` (ship middleware there) |
| D2 | `Guid.NewGuid()` message/correlation-ID minting | 9 minting sites: Client ×2, Mcp ×1, Security ×3, Sample ×3 (paths in §7.1); 3 further benign non-identifier uses | ULID helper `UniqueIdHelper` in `Hexalith.Commons.UniqueIds` (exists at pin, currently referenced **nowhere** in Parties src — no csproj even references the package) |
| D3 | Raw Dapr state-store access | 7 files / 3 projects (§4.1) | `IReadModelStore` |
| D4 | Result/outcome envelope records | AdminPortal (3 types) + ConsumerPortal (12 files: `ConsumerConsentOperation*`, `ConsumerPrivacyErasure*`, `ConsumerPrivacyExport*`, `ConsumerPrivacyProcessing*`, `ConsumerProfileUpdate*`, `ConsumerProfileValidationFailure`) | FC `CommandResult`/`CommandRejectionDetails`/`ProblemDetailsPayload` |
| D5 | Freshness indicator component | `UI/Components/Shared/DataFreshnessIndicator.razor` ≙ `ConsumerPortal/Components/FreshnessStatus.razor` (same `ProjectionFreshnessMetadata` param, same dot+aria-live pattern) | One shared component beside FC `FcProjectionConnectionStatus` |
| D6 | Labels/localization | 3 divergent approaches: `AdminPortalLabels.cs` (436-line hardcoded record), `ConsumerPortalLabels.cs` (resx-backed), `PartyPickerLabels.cs` (hardcoded) | resx via FC `AddHexalithShellLocalization` |
| D7 | Freshness metadata model | `Contracts/Models/ProjectionFreshnessMetadata.cs` vs SDK `ReadModelFreshness` | EventStore.Client |
| D8 | `DaprStateStoreHealthCheck` | Parties copy vs SDK generalized version (EventStore itself carries two copies — flag upstream) | EventStore SDK |
| D9 | EventStore command envelope | `HttpPartiesCommandClient.cs:270-278` + `HttpAdminPortalGdprClient.cs:393` | **Adopt existing** `SubmitCommandRequest`/`CommandEnvelope` (EventStore.Contracts/Commands — gap closed) |
| D10 | `PagedResult<T>` | `Contracts/Models/PagedResult.cs` vs `Hexalith.Commons.Http.PagedResult<T>` (+ adapter `Paging/PartiesPagedResultAdapter.cs`) | Commons (add freshness), delete local |
| D11 | Keycloak realm JSON | AppHost fork vs EventStore canonical | EventStore realm + overlay mechanism |
| D12 | DaprComponents YAML | `AppHost/DaprComponents/*.yaml` vs `deploy/dapr/*.yaml` (10 near-parallel files, intentional local-vs-k8s variants) | Keep both but generate from one source or document dual-maintenance |
| D13 | Test helpers | `RecordingHttpMessageHandler` ×2 (AdminPortal.Tests, Picker.Tests — implementations differ slightly); `RepositoryRoot` ×2 (Parties.Tests/FitnessTests, Mcp.Tests); options-monitor stubs ×3 in Parties.Tests (`TestOptionsMonitor<T>`, 2× `StubOptionsMonitor`) | Commons/EventStore.Testing; FC testing helpers for `FakeAuthStateProvider`/`ManualTimeProvider` |
| D14 | Test tenant constants | `Testing/PartyTestData.cs:292` `DefaultTenantId = "test-tenant"` vs `Hexalith.EventStore.Testing.TestDataConstants.TenantId` | Reference EventStore.Testing |
| D15 | Client registration validation | `PartiesClientServiceCollectionExtensions.cs:11-82` vs `Commons.Http.HttpClientRegistration` | Commons |
| D16 | Six `Consumer*Client` adapters in UI delegating `ISelfScopedPartiesClient` to six ConsumerPortal interfaces (10–155 lines each — thin delegators, not one-liners) | Collapse to one self-scoped client abstraction |

Not duplicated (keep as-is): `JsonSerializerOptions` — all 16 touchpoints correctly share
`PartiesJsonOptions.Default`.

---

## 6. Obsolete / Deprecated Code

| Item | Strategy |
| --- | --- |
| **MediatR** in `Hexalith.Parties.csproj:48` and `Hexalith.Parties.Server.csproj:12` — zero usage in any src/samples .cs file | Remove both refs (no code change; the string appears only in fitness/package tests) |
| `Dapr.Client`/`Dapr.Actors` in `Parties.Server.csproj` — unused by its single file (`PartyAggregate.cs` has zero Dapr references) | Remove |
| `Dapr.AspNetCore` in `samples/Hexalith.Parties.Sample.csproj` — Dapr appears only in a comment (`Program.cs:25`) | Remove or actually wire up (`MapSubscribeHandler`) — decide with sample fix |
| `Aspire.Hosting.Redis` + `Aspire.Hosting.Keycloak` in AppHost (both confirmed uncomposed) | Remove |
| Hexalith.PolymorphicSerializations solution reference — zero `[PolymorphicSerialization]`/`JsonDerivedType` usage in src | Either adopt for events or drop from `.slnx`/submodules |
| `[Obsolete]` retained-for-compat members: `Picker/Components/PartyPicker.razor:112` (`ApiBaseUrl`), `Picker/Services/PartyPickerSearchMetadata.cs:7,10,13` (+ self-suppressing `#pragma warning disable CS0618` at `:19`), `AdminPortal/Services/PartiesAdminPortalOptions.cs:5` | Schedule deletion in the Picker/AdminPortal simplification steps |
| In-host JWT stack (`ConfigurePartiesJwtBearerOptions` etc.) — vestigial per `PartiesServiceCollectionExtensions.cs:92-102` | Delete after gateway-authorization confirmation |
| Quarantined services pinned OFF the request path (`PartiesServiceCollectionExtensions.cs:103,115`): `TenantAccessService`, `DataSubjectAccessService`, consumer-policy path | Move to gateway authorization or delete, with their tests |
| E2E fixture + specimen pages in prod assemblies: `UI/Services/PartiesAdminPortalE2eFixture.cs` (9 types), `UI/Components/Specimens/*` (accessibility + picker specimens), fixture endpoints `UI/Program.cs:204-216` | Move to `Hexalith.Parties.Testing`/test tree; gate specimens behind FC DevMode |
| `Contracts/PartiesTextHeuristics.cs` (7 lines, Contracts root) — classifies 403s by sniffing "tenant" in problem-details text (call sites `AdminPortal/Services/PartiesAdminPortalApiClient.cs:592-620`, `Client/AdminPortal/HttpAdminPortalGdprClient.cs:155-157`) | Replace with structured gateway error code (platform gap), then delete |
| Generated BMAD docs snapshot (2026-06-02): claims 14 project folders, references removed `Hexalith.Tenants.Aspire`/`AddHexalithTenants` | Regenerate post-refactor; gitignore `project-scan-report.json` |
| 25 committed `*.csproj.lscache` files (13 src+samples, 12 tests) | Delete + gitignore |
| 6 `Skip=` facts `IntegrationTests/HealthChecks/HealthEndpointE2ETests.cs:37,49,61,73,92,160` (Story 12.1 deferral) | Revive or delete during migration |
| `TODO(Story 10-1.1)` `AdminPortal/Services/AdminPortalSearchResponse.cs:5`; `#pragma warning disable HXL001` `Parties/Search/PartyMemoryIndexingService.cs:57` | Resolve during respective steps |

---

## 7. Rule Violations & Over-Engineering

### 7.1 Forbidden `Guid` usage on identifiers (ULID rule) — **highest-priority correctness fix**

- **`Guid.TryParse` validation (31 occurrences, 19 files)**: all 18 files in
  `src/Hexalith.Parties/Validation/*.cs` (29 occurrences — e.g. `CreatePartyValidator.cs`,
  `UpdatePartyCompositeValidator.cs` ×7, `CreatePartyCompositeValidator.cs` ×5) and
  `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:41,561`. Consequence: **valid ULID
  aggregate IDs are rejected today.** Fix: `Ulid.TryParse` / `AggregateIdentity` rules.
- **`Guid.NewGuid()` ID minting (9 sites)**: `Client/HttpPartiesCommandClient.cs:158`,
  `Client/AdminPortal/HttpAdminPortalGdprClient.cs:193`, `Mcp/Tools/PartiesMcpTools.cs:977`,
  `Security/PartyKeyManagementService.cs:224`, `Security/TenantKeyRotationService.cs:35,281`,
  `samples/.../Program.cs:55,72,88`; GUID-shaped `DefaultPartyId` in `Testing/PartyTestData.cs:11`.
  (Benign non-identifier uses, leave alone: `MemoriesSearchHealthCheck.cs:186`,
  `PartyMemoryCleanupService.cs:244`, `PartiesProjectionSubscription.cs:123`.)

### 7.2 Legacy Fluent v4/FAST tokens (74 occurrences, 8 stylesheets — forbidden)

`AdminPortal/Components/PartiesAdminPortal.razor.css` (18), `ConsumerPortal/Components/MyPrivacyPage.razor.css` (16),
`MyProfilePage.razor.css` (14), `ConsumerRouteShell.razor.css` (11), `CreateEditPartyPage.razor.css` (7),
`EditMyProfilePage.razor.css` (5), `MyConsentPage.razor.css` (2), `PartyGdprOperationsPanel.razor.css` (1)
— plus ~1,130 lines of custom CSS/JS across the UI group, raw JS files
(`AdminPortal/wwwroot/party-form-picker.js`, `UI/wwwroot/consumer-privacy-export.js`), and inline JS
in `PartyPicker.razor:8`. Migrate to Fluent 2 tokens / component parameters; most custom CSS
disappears when `FcPageLayout`/`FcPageHeader`/Fluent components replace raw markup
(`ConsumerPortal/Components/ConsumerRouteShell.razor:1-18`).

### 7.3 One-type-per-file violations

Worst: `samples/.../PartyEventHandler.cs` (16 types), `UI/Services/PartiesAdminPortalE2eFixture.cs` (9),
`Parties/Search/PartySearchBoundary.cs` (7), `PartyMemoryUnitMappingStore.cs` (4), `samples/.../CustomerSummary.cs` (4),
`AdminPortal/Services/AdminPortalCommandResult.cs` (3), `Mcp/PartiesMcpRequestContext.cs` (3),
`Security/DecryptionCircuitBreaker.cs` (3), `HealthChecks/TenantsIntegrationHealthCheck.cs` (3), ~15 more with 2–3.

### 7.4 Over-engineering (simplification proposals)

| Item | Proposal |
| --- | --- |
| `PartyDomainServiceInvoker` reflection/allowlist machinery | Delete with SDK adoption; keep only the payload-protection hook + erasure orchestration via `IEventPayloadProtectionService` |
| `IPartyProjectionPlatformAdapter` dual-mode (Local vs EventStore) + `PartyProjectionPlatformAdapterMode` leaking into prod DI (`PartiesServiceCollectionExtensions.cs:279-284`) | Delete with SDK projection path |
| `PartyProjectionUpdateOrchestrator` registered under two interfaces with same-instance gymnastics (`:121-123`) | Platform poller concern — delete (EventStore now has its own `ProjectionUpdateOrchestrator` server-side) |
| 323-line `AddParties` megamodule (`PartiesServiceCollectionExtensions.cs:41-363`, single method in a 364-line file) | Split into per-concern registrations; most content dissolves into SDK calls |
| `PartiesAdminPortalApiClient.cs` (777 lines) triple-wrapping Client interfaces + rich-search HTTP probe | Portal consumes Client + FC `IQueryService`; probe → FC capability discovery |
| Static mutable `PartyAggregate.MaxSubOperations` (`PartyAggregate.cs:19-21`) test back-door | Option/constant |
| 978-line `PartiesMcpTools` class; generic date-parsing helpers (`TryParseOptionalDate` :946-974, `ParseDateOfBirth` :921, `TryParseEnum` :882) | Split per tool; helpers → Commons |
| `Mcp/Program.cs:31-59` two hand-built transient client factories duplicating `AddPartiesClient` | Use the extension |
| K&R braces across host/server/testing code (`PartyAggregate.cs:16`, `PartyTestData.cs`) vs declared Allman standard; `NoWarn 1591` in `Contracts.csproj:7` hiding missing XML docs | Restyle + re-enable CS1591 during touch passes |

---

## 8. Platform APIs to introduce FIRST (in technical modules)

Status re-verified 2026-07-06 against Parties' current pins (EventStore `a592bbd`, Commons
`275edc0`, FrontComposer `92edc30`). ✅ = gap closed, adopt only; ◐ = partially closed; ✖ = still
missing, platform work required before the dependent Parties deletion.

| # | Status | Module | New/extended API | Unblocks |
| --- | --- | --- | --- | --- |
| P1 | ◐ | **Hexalith.EventStore (SDK)** | Rebuild machinery now EXISTS platform-side (`ProjectionRebuildCheckpoint/Operation/Status` contracts, `IProjectionRebuildCheckpointStore`, `ProjectionUpdateOrchestrator`, `AdminProjectionRebuildController`, client hook in `EventStoreDomainEventProcessor`), but is **not surfaced through the DomainService SDK package** (zero Rebuild/Checkpoint hits there). Remaining: SDK surfacing + pub/sub & projection-responsiveness health checks (SDK's `DaprStateStoreHealthCheck` exists — note EventStore carries two copies to dedupe upstream) | Deleting Projections actors/rebuild service, host health checks |
| P2 | ✖ | **Hexalith.EventStore.DataProtection** (new package) | `IAggregateKeyManagementService`, `IKeyStorageBackend`, key rotation/audit/retry/circuit-breaker engine, attribute-driven payload protector implementing `IEventPayloadProtectionService`. Verified: only `NoOpEventPayloadProtectionService` + testing fake exist today; the new DomainService `EventStoreDataProtection*` key ring backs only the query-cursor codec | Moving the Security engine + 18 Contracts/Security files |
| P3 | ✅ | **Hexalith.EventStore.Client/Contracts** | Command envelope EXISTS: `CommandEnvelope`, `SubmitCommandRequest/Response`, `DomainServiceRequest` (Contracts/Commands) + `IEventStoreGatewayClient.SubmitCommandAsync`. Staleness EXISTS as `QueryResponseMetadata.IsStale/IsDegraded` + `QueryProblemReasonCodes.ProjectionStale` + `ReadModelFreshness*`. Remaining sub-gap: structured gateway error code for tenant-authorization failures (replaces `PartiesTextHeuristics` text sniffing) | D7, D9, D10 by adoption; `PartiesTextHeuristics` deletion needs the error-code sub-gap |
| P4 | ✅ | **Hexalith.EventStore** | `EventStoreClaimsTransformation` EXISTS (`src/Hexalith.EventStore/Authentication/`, 115 lines, maps `tenants`/`tenant_id`/`tid` → `eventstore:tenant`). Caveat: lives in the gateway package — if domain hosts need it, expose from a shared package instead of copying | Deleting `Hexalith.Parties.Authentication` (unblocked now) |
| P5 | ✖ | **Hexalith.EventStore.Aspire** | Publish-mode `WithJwtBearerSecurity` (static authority, multi-audience `ValidAudiences__N`) — verified absent (no `IsPublishMode` branch; single audience; authority always derived from the run-time Keycloak endpoint); publish-mode `WithOpenIdConnectSecurity` overload; `WithDomainServiceRegistration(domain, version, appId)`; Dapr-config path probing; Keycloak realm overlay | AppHost simplification |
| P6 | ◐ | **Hexalith.FrontComposer** | All cited FC capabilities (projection stream, freshness fallback, reconcile, token provider, status primitives, `DataGridNavigation`, `CommandResult` envelopes, `FcPageLayout`/`FcPageHeader`, `FcProjectionConnectionStatus`, localization, FrontComposer.Mcp) verified present **at Parties' pin** — adoption only. Shell skip-links already exist (`fc-skip-link`) — no work. Genuine gaps: generic entity-picker/autocomplete primitive (confirmed absent), durable IdentityBinding store pattern, freshness indicator component, reusable a11y/style-guard test helpers | UI group shrink |
| P7 | ✖ | **Hexalith.Commons.Http** | Correlation middleware (accessor + `HttpCorrelation` already exist; middleware verified absent); problem-details scrubbing helper | D1, client cleanup |
| P8 | ✖ | **Hexalith.Builds** | `HexalithXxxRoot` probing props (`Directory.Build.props:4-30`), `RemoveDuplicateLoggingSourceGenerator` target, `check-no-warning-override.sh` (not re-verified this pass — confirm against current Builds pin before starting) | Root build-file slimming |

---

## 9. Test Impact Analysis

### 9.1 Tests that move with their src (platform-behavior tests)

- `Parties.Tests/HealthChecks/*` (~11 files incl. `DaprSidecarHealthCheckTests`, `DegradedResponseMiddlewareTests`, `ServiceDefaultsCompatibilityTests`), `Middleware/CorrelationIdMiddlewareTests.cs`, `ErrorHandling/*` → EventStore/Commons suites.
- `Security.Tests` (18 files, 4,827 lines; largest `PartyPayloadProtectionServiceTests.cs` at 990) → EventStore.DataProtection suite; keep only `PartyPersonalDataCommandGuardTests.cs` (128 lines) in Parties.
- `IntegrationTests/Events/*` (CloudEvents envelope, dead-letter, tenant topics) → EventStore integration suite.
- `Authentication.Tests/PartiesClaimsTransformationTests.cs` → retire in favor of EventStore's own `EventStoreClaimsTransformation` coverage (verify the platform suite covers the `tenants`/`tenant_id`/`tid` matrix before deleting).
- `UI.Tests` plumbing (`DegradedResponseHeaderHandlerTests`, `PartiesProjectionSubscriptionTests`, `ProjectionFreshness*`, `OptimisticReconcileTests`, a11y/style guard tests) → FC/EventStore.SignalR suites.
- `DeployValidation.Tests` (14 files, 3.8k lines) → deployment tooling home; retain slim Parties ACL route-map/topology checks (`DaprAccessControlFitnessTests.cs:68`).

### 9.2 Tests that pin the to-be-removed architecture (rewrite during migration)

- **~5.5k lines of actor/gateway tests** (all in `tests/Hexalith.Parties.Tests/Gateway/`):
  `PartyIndexProjectionQueryActorTests.cs` (911), `PartyDetailProjectionQueryActorTests.cs` (757),
  `TenantSafeProjectionReadGuardrailsTests.cs` (820), `EventStoreGatewayRoutingTests.cs` (1279),
  plus `Projections/*ActorCorruptionTests.cs`, `ProjectionPlatformAdapterTests.cs`,
  `ProjectionRebuildServiceTests.cs` — port the *business assertions* (tenant guardrails,
  rejection handling) onto `IDomainQueryHandler`/`IDomainProjectionHandler` tests; drop actor mechanics.
- `Domain/PartyDomainServiceInvokerValidationTests.cs` (495) + `Domain/PartyDomainEventPublicationContractTests.cs` (438) → rewrite against SDK router.
- **Grep-based fitness net (~20 tests)** breaks on every project move: `FitnessTests/ArchitecturalFitnessTests.cs` (968 lines, pins Program.cs mappings, csproj lists, quarantine boundaries), `AppHostTenantsTopologyTests.cs` (asserts `$(HexalithEventStoreRoot)`-form project paths — currently consistent with the csproj), `ClientArchitecturalFitnessTests.cs:93`, `PartiesMcpProjectFitnessTests.cs`, `SampleOnboardingGuardrailTests.cs:80`, `PartyPickerTransportGuardrailTests.cs`, portal `Packaging/*Tests.cs`. **Update these in the same commit as each move, never after.**
- Quarantined-service tests (`TenantAccessServiceTests`, `DataSubjectAccessServiceTests`, `PartiesConsumerPolicyTests`, `HelperDrivenTenantAccessTests`) go with their services.
- `ContractsPublicApiSnapshotTests.cs` — regenerate snapshot when Contracts types move.

### 9.3 Domain tests to preserve (must keep passing throughout)

- `Server.Tests/Aggregates/` — **exactly 203 facts/theories** of pure `Handle`/`Apply` across 10 `PartyAggregate*Tests.cs` files (crown jewels; only namespace updates).
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
| Authentication.Tests | **Retire** once platform coverage confirmed |
| IntegrationTests | **Split**: Topology keep; Events → EventStore; Security → moves |
| DeployValidation.Tests | **Move** to deploy tooling; keep slim domain checks |
| tests/e2e (Playwright + @axe-core, `test:a11y`/`test:visual`) | Keep (CI-gated a11y) |

---

## 10. Keep-in-Parties vs Move-to-Technical-Module Classification

### Keep in Hexalith.Parties (domain)
- `PartyAggregate` (24 command handlers, display-name derivation) — merged into the domain library.
- All commands/events/value objects/state in Contracts (ULID-fixed validators).
- Projection fold handlers (`PartyDetailProjectionHandler`, `PartyIndexProjectionHandler`, name-history, rejection) retargeted on `IDomainProjectionHandler`.
- Query handlers (rewritten as `IDomainQueryHandler` per query) with tenant-guardrail semantics.
- GDPR domain policy: `PersonalDataGraphInspector`, `PartyPersonalDataCommandGuard`, `PartyErasureOrchestrator`, `ErasureVerificationService`, erasure records/certificates, `LawfulBasis`, consent contracts (the 12 GDPR/erasure files in `Contracts/Security/`).
- Authorization policies (Admin/Consumer), `PartyIdClaimResolver`, Parties claim-type names (`PartiesClaimTypes`, `PartiesRoles`).
- Party search services (Basic/LocalFuzzy/Semantic, Memories integration) + `TenantsIntegrationHealthCheck`, `MemoriesSearchHealthCheck`.
- Typed clients (`IPartiesCommandClient`, `IPartiesQueryClient`, GDPR client), MCP tool definitions, `PartyTestData`, domain UI screens (GDPR panels, Create/Edit party, MyProfile/MyConsent/MyPrivacy, `PartyStateBadge`), Picker custom-element contract + party data source, FC manifest/nav registration, AppHost (thinned), Sample, domain docs.

### Move to technical modules
- **Hexalith.EventStore**: crypto-shredding engine + 18 key-mgmt contracts (P2); Dapr health checks; ProblemDetails handlers; structured gateway error codes (P3 residue); Aspire publish-mode security helpers (P5); SDK surfacing of rebuild/checkpoints (P1 residue); realm overlay.
- **Hexalith.Commons**: correlation middleware (+delete Parties accessors); problem-details scrubbing; `HttpClientRegistration` adoption; ULID minting via Commons.UniqueIds; generic claim extraction (TenantAccess).
- **Hexalith.FrontComposer**: generic entity-picker primitive; durable IdentityBinding pattern; freshness indicator component; a11y/style-guard test helpers. (Everything else — projection stream, freshness, reconcile, token provider, status primitives, grid state, result envelopes, MCP plumbing — already exists at the pin: **adopt, don't build**.)
- **Hexalith.Builds**: root-props probing, logging-generator dedup target, warning-override guard script (re-verify against current Builds pin first).
- **Platform/ops repos**: `deploy/k8s/{eventstore*,redis,falkordb,memories}`, `deploy/zot`, platform deployment docs, generic deploy-validation tooling.

### Adopt existing platform APIs (no platform work, delete Parties copy)
- `SubmitCommandRequest`/`CommandEnvelope` + `IEventStoreGatewayClient` (replaces both private `EventStoreCommandRequest` records).
- `EventStoreClaimsTransformation` (replaces `Hexalith.Parties.Authentication` wholesale).
- `QueryResponseMetadata.IsStale/IsDegraded` + `ReadModelFreshness*` (replaces the `X-Service-Degraded` header pipeline and `ProjectionFreshnessMetadata`).
- FC skip-links, projection stream, `DataGridNavigation`, `CommandResult` envelopes, `AddFrontComposerMcp` (all at pin).

### Delete outright
- `Hexalith.Parties.ServiceDefaults` (project), `Hexalith.Parties.Authentication` (project — unblocked now), `Hexalith.Parties.Server` (project shell, after merging the aggregate), projection/query actors + rebuild service + platform adapters + `PartyDomainServiceInvoker`, in-host JWT stack (after gateway confirmation), quarantined services, MediatR/unused Dapr/Redis/Keycloak package refs, `PartiesTextHeuristics`, E2E fixture & specimens from prod, `[Obsolete]` members, `PUBLISH_TARGET` switch, lscache files.

---

## 11. Risks, Migration Order & Compatibility Concerns

| Risk | Mitigation |
| --- | --- |
| **ULID fix is a behavior change**: validators currently *reject* ULIDs and *accept* GUIDs; existing stored aggregates have GUID-shaped IDs (`PartyTestData` default is GUID-shaped) | Accept any non-whitespace string per `AggregateIdentity` rules (don't swap one over-strict parse for another); keep GUID-shaped IDs valid for replay compatibility |
| **Wire compatibility**: EventStore payloads are PascalCase/numeric-enum | Any serialization move must preserve `PropertyNameCaseInsensitive=true`; `PartiesJsonOptionsTests` + `Sample.Tests` tolerant-deserialization tests are the guard |
| **Projection state migration**: hand-rolled actors own checkpoints/state under their own Dapr keys; SDK `IReadModelStore` may use different key shapes | Requires either a rebuild-from-stream cutover (preferred — event sourcing makes it safe) or a key-migration shim; plan a full projection rebuild as part of Step 6. Note the SDK-side rebuild machinery now exists (P1) — align checkpoint formats with `ProjectionRebuildCheckpointStore` (the client already matches its operation-id format, `EventStoreDomainEventProcessor.cs:247`) |
| **SDK feature parity**: corruption handling and freshness thresholds exist only in Parties today; rebuild + staleness metadata now exist platform-side | Close the P1 residue first; port the business assertions from `ProjectionRebuild*Tests`/`*CorruptionTests` into SDK tests |
| **Degraded-state cutover**: Parties uses response headers, platform uses body metadata (`QueryResponseMetadata`) | Cut UI + client over to the metadata contract in one step; delete the header handler/middleware pair together |
| **Security engine move changes NuGet surface**: `Hexalith.Parties.Contracts` currently ships key-mgmt contracts publicly | Coordinate a major-version bump (`feat!:`); keep type-forwarders or an obsolete-shim release if external consumers exist |
| **Grep-based fitness tests** break on every move | Update in the same commit as each move; never batch |
| **Multi-repo coordination**: platform work lands in up to 4 repos with submodule pins | One platform repo per PR, bump the submodule pin in Parties, then do the dependent Parties step; keep `main` green between steps |
| **Dependency-mode duality**: default builds are NuGet-based; project-reference mode (`-p:UseHexalithProjectReferences=true`) needs initialized nested submodules | Platform-API adoption steps require *released* packages (or a temporary project-ref opt-in); sequence each adoption after the platform package publishes; never initialize nested submodules in shared workspaces by default |
| **Cross-suite flake traps** (parallel build, MinVer skew, pack red) | `-m:1` clean builds, `-p:MinVerVersionOverride=1.0.0` on rebuilt subsets, never treat pack/`*PackageTests` red as regression |
| **Keycloak realm fork drift** | Land realm-overlay support (P5) before touching auth topology |

**Ordering principle**: platform-first (now a smaller set: P1 residue, P2, P5, P6 gaps, P7), then
leaf projects (ServiceDefaults, Authentication — both unblocked today, Server-merge — cheap,
independent), then the big host/projection cutover (single vertical change), then UI consolidation,
then deploy/docs cleanup. The ULID fix, dead-reference removals, and both leaf-project deletions
are independent and can go first.

---

## 12. Ordered Action Plan

### Phase 0 — Baseline (plan step 1)
1. `dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx -c Release -m:1` — record warnings. Record the dependency mode used (default = NuGet since `fd94736`); if building from local sources, note `-p:UseHexalithProjectReferences=true` requires nested submodules.
2. Run each test EXE directly; record the pass/fail profile. **Re-diagnose the previously recorded single `Hexalith.Parties.Tests` failure** — the documented cause (topology-test path literal) is retracted; establish the real cause or confirm it no longer fails.
3. Fix `scripts/test.ps1` first (add `ConsumerPortal.Tests` to a targeted lane; remove solution-level `dotnet test` from `all`/`coverage`) so the gate is trustworthy for the whole migration.

### Phase 1 — Zero-risk hygiene (steps 2–3, no behavior change)
4. Remove dead package refs: MediatR ×2, `Dapr.Client`/`Dapr.Actors` from Server.csproj, `Aspire.Hosting.Redis` + `Aspire.Hosting.Keycloak` from AppHost (both confirmed uncomposed), decide `Dapr.AspNetCore` in Sample.
5. Delete 25 `*.csproj.lscache` + gitignore; gitignore `docs/project-scan-report.json`.
6. Resolve PolymorphicSerializations: adopt for events or remove from `.slnx`.
7. One-type-per-file splits + Allman restyle on files being kept (`PartySearchBoundary.cs`, sample files, etc.); re-enable CS1591 in Contracts and document.

### Phase 2 — Identifier correctness (independent, high value)
8. Replace all 31 `Guid.TryParse` validations (18 validators + `PartyAggregate.cs:41,561`) with `AggregateIdentity`/ULID rules; replace the 9 `Guid.NewGuid()` mint sites with Commons.UniqueIds (`UniqueIdHelper` — add the package reference, currently absent); fix `PartyTestData.DefaultPartyId`; keep GUID-shaped IDs accepted for replay compatibility. Update validator tests.

### Phase 3 — Adopt closed platform gaps + remaining platform prerequisites (steps 4–5)
9. **Adoption-only (no platform work, can start immediately)**: swap both private `EventStoreCommandRequest` records for `SubmitCommandRequest`/`IEventStoreGatewayClient` (P3); replace `ProjectionFreshnessMetadata`/`ProjectionFreshnessStatus` with `ReadModelFreshness*`; plan the degraded-header → `QueryResponseMetadata` cutover.
10. **Platform PRs still needed**: EventStore — SDK surfacing of rebuild/checkpoints + pub/sub/projection health checks + structured gateway error codes (P1 residue, P3 residue); EventStore.DataProtection package (P2); Aspire publish-mode security helpers (P5); consider exposing `EventStoreClaimsTransformation` from a non-gateway package (P4 caveat). Commons — correlation middleware + scrubbing (P7). FrontComposer — entity-picker primitive, durable IdentityBinding, freshness component, a11y/style-guard helpers (P6 gaps only). Builds — shared props/targets/script (P8, re-verify first).
11. Bump submodule pins in Parties (`chore(deps): bump …`) as each lands; with NuGet-default builds, also wait for the corresponding package release before the dependent Parties step.

### Phase 4 — Leaf-project migrations (step 6)
12. **Delete `Hexalith.Parties.ServiceDefaults`**: hosts call `AddHexalithServiceDefaults(o => …)` inline; delete `ServiceDefaultsCompatibilityTests`; fix `K8sManifestPublishTests.cs:118`.
13. **Delete `Hexalith.Parties.Authentication`** (unblocked — `EventStoreClaimsTransformation` exists at the current pin): consume the platform transformation; verify the platform test suite covers the claim matrix, then retire `PartiesClaimsTransformationTests`.
14. **Merge `PartyAggregate` into the domain library**, delete `Hexalith.Parties.Server`; remove `MaxSubOperations` static; rename Server.Tests accordingly; update all fitness greps in the same commit.
15. **Move the Security engine** to EventStore.DataProtection (P2) with its 17 test files; retarget kept GDPR policy classes onto the platform engine; move the 18 `Contracts/Security` platform contracts (keep the 12 GDPR/erasure ones); `feat!:` version bump; regenerate contracts API snapshot.
16. **Client cleanup**: `HttpClientRegistration`, `SubmitCommandRequest` adoption (from step 9), scrubbing helper, delete `PagedResult` duplicate + adapter; update Client fitness tests.
17. **Mcp cleanup**: adopt FrontComposer.Mcp plumbing, drop ServiceDefaults dep, use `AddPartiesClient` (replace `Mcp/Program.cs:31-59` hand-built factories), split the 978-line tool class; update `PartiesMcpProjectFitnessTests`.

### Phase 5 — Host & projection cutover to the EventStore SDK (steps 6–7, the big one)
18. Rewrite `src/Hexalith.Parties/Program.cs` to the two-line SDK shape + domain registrations only.
19. Re-home projection folds as `IDomainProjectionHandler`; persist via `IReadModelStore`/`ReadModelWritePolicy`; delete both actors, rebuild service (aligning to the platform rebuild machinery), platform adapters, orchestrator double-registration, `ProjectionActorsHealthCheck`.
20. Rewrite query actors as per-query `IDomainQueryHandler` + `IQueryCursorCodec`; port tenant-guardrail assertions.
21. Delete `PartyDomainServiceInvoker` (keep payload-protection hook), correlation middleware (use Commons), Dapr health checks (use SDK), in-host JWT stack + quarantined services (after gateway confirmation); split `AddParties` remnants per concern.
22. Execute a **full projection rebuild** in the AppHost stack to cut over read-model state; verify freshness/degraded behavior end-to-end via `aspire run` (now via `QueryResponseMetadata`, not headers).
23. Rewrite/port the ~5.5k lines of pinned tests (Section 9.2) alongside each sub-step; revise `ArchitecturalFitnessTests` to pin the *new* invariants (SDK usage, no raw Dapr, no actors).

### Phase 6 — UI consolidation (step 7 continued)
24. `Parties.UI`: swap local projection stream/freshness/reconcile/token provider for FC types (all verified at pin); cut degraded handling over to `QueryResponseMetadata`; collapse the six `Consumer*Client` adapters (10–155 lines each) to one self-scoped abstraction; move E2E fixture (9 types) + specimens to Testing/tests; fold Program.cs residue into FC options; adopt durable IdentityBinding (18 files today).
25. Portals: adopt FC `CommandResult` envelopes (replaces AdminPortal's 3 + ConsumerPortal's 12 envelope types), `DataGridNavigation`, `FcPageLayout`/`FcPageHeader`; shrink `PartiesAdminPortalApiClient` (777 lines); converge labels on resx; dedupe freshness component; purge all 74 legacy tokens + shrink custom CSS; split the 1,825-line `PartiesAdminPortal.razor`.
26. Picker: rebuild internals on `FluentAutocomplete` (or the new FC picker primitive from P6); remove inline JS + `[Obsolete]` members; keep the custom-element contract.
27. Update bUnit suites as components change; run SSR a11y specs locally, interactive gate in CI.

### Phase 7 — AppHost & deploy (step 6 tail)
28. AppHost: adopt `AddEventStoreDomainModule`, `WithJwtBearerSecurity`/`WithOpenIdConnectSecurity` (incl. new publish-mode overloads from P5), `WithDomainServiceRegistration`, path helpers; delete `PUBLISH_TARGET` switch + Azure/Docker/K8s packages; externalize the tache.ai issuer (`Program.cs:14`); realm overlay.
29. Move platform deploy assets (`eventstore*`, `redis`, `falkordb`, `memories`, `zot`) + platform deployment docs out; move generic DeployValidation tooling; keep slim Parties ACL/topology checks; document DaprComponents dual-maintenance.
30. Move `Directory.Build.props` probing + logging target + guard script to Hexalith.Builds; remove FrontComposer warning suppressions from `Directory.Build.targets` after upstream fixes; narrow the NU5118/NU5128 NoWarn scope.

### Phase 8 — Obsolete removal, docs, samples (steps 8–9)
31. Delete remaining `[Obsolete]` members, retirement-guard tests made unexpressible, skipped E2E facts (revive or drop), `PartiesTextHeuristics` (after the P3 error-code sub-gap lands).
32. Sample: ULID IDs, move the 50-line MCP comment block to docs, wire or drop Dapr.
33. Regenerate BMAD docs set; refresh README/docs to the new project list; keep domain docs (GDPR set, event pub/sub, picker) updated — mind `PartyErasedHandlerPatternTests` doc greps.

### Phase 9 — Verification (steps 10–11)
34. Per-project test EXE runs (`-class`/`-method` filters as needed) after each phase; `dotnet restore` + `dotnet build Hexalith.Parties.slnx -c Release -m:1` at each phase end; `-p:MinVerVersionOverride=1.0.0` on rebuilt subsets.
35. `aspire run` topology smoke: gateway command → `/process` → event → projection → query → UI freshness; MCP tool round-trip; Sample subscriber round-trip.
36. Final checklist below.

### Final verification checklist
- [ ] `dotnet build Hexalith.Parties.slnx -c Release -m:1` — zero warnings (TreatWarningsAsErrors).
- [ ] Every test EXE green except the documented pre-existing pack reds; no *new* failures vs the Phase-0 baseline.
- [ ] `grep -r "AddEventStoreDomainService" src/Hexalith.Parties/Program.cs` — present; Program.cs ≤ ~20 lines.
- [ ] Zero hits in `src/` for: `Guid.TryParse` on IDs, `Guid.NewGuid` ID minting, `Dapr.Actors.Runtime.Actor` subclasses, `--neutral-|--accent-|--type-ramp-|--palette-` tokens (74 → 0), `MediatR`, `catch (NotImplementedException)`.
- [ ] `IDomainProjectionHandler`/`IDomainQueryHandler`/`IReadModelStore`/`IQueryCursorCodec` are the only projection/query mechanisms.
- [ ] Projects deleted: ServiceDefaults, Authentication, Server; no `*.ServiceDefaults` in the module.
- [ ] `.slnx` updated; no legacy `.sln`; fitness tests pin the new architecture.
- [ ] Submodule pins current; all commits Conventional (`npx commitlint --last --verbose`).
- [ ] Full projection rebuild executed and verified against aggregate replay.
- [ ] Docs regenerated; deploy tree contains only Parties-owned assets.

---

*Sources: original six-pass deep analysis at `88d984b` plus four independent re-verification
passes at `fd94736` (2026-07-06) over src/, tests/, samples/, deploy/, docs/, root build files,
and the platform submodules at Parties' current pins — Commons `275edc0`, EventStore `a592bbd`,
FrontComposer `92edc30` (verified via git history), Tenants `e9cbe82`.*
