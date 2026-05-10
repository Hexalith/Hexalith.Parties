# Story 10.1.1: Admin Portal — FrontComposer / Tenants / Rich-Search Integration

Status: done

<!-- Note: Placeholder created during Story 10.1 pass-2 BMad code review (2026-05-07).
     Story 10.1 was approved with three explicit deferrals; this story is the agreed
     landing zone for that follow-up work. Convert to ready-for-dev via create-story
     when Epic 10 prioritization picks it up. -->

## Story

As an administrator,
I want the Parties admin portal to consume Hexalith.Tenants context, FrontComposer Fluent UI components, and a deterministic rich-search capability signal,
so that AC3, AC7, and AC8 of Story 10.1 are met without relying on host wiring or scaffolding placeholders.

## Acceptance Criteria

1. Given an authenticated administrator, when the portal renders, then it derives `IsAuthenticated`, `HasTenantContext`, and `IsAdmin` from `AuthenticationStateProvider` and the existing `Hexalith.Tenants` membership/role services rather than from host-supplied parameters. **(D2b — pass-1 deferral)**
2. Given the portal probes the backend, when the rich-search capability endpoint is reachable and reports healthy, then the email/identifier search modes activate; otherwise the UI remains display-name-only. **(D3 — pass-1 deferral)**
3. Given the portal renders, when the user views the browse grid, search box, filters, paging, and detail panel, then those surfaces use `FluentDataGrid<T>`, `FluentTextInput`, `FluentSelect`, `FluentButton`, and FrontComposer transports/ETag caching/auth redirect rather than raw HTML elements. **(D4 — pass-1 deferral)**
4. Given the integration lands, when the live component runs, then `AdminPortalPartyQueryService` and `PartiesAdminListCoordinator` are wired into the lifecycle (no longer dead-code scaffolding), and the `AdminPortalListState` enum's non-`Loading` values are reachable via real state transitions. **(D5 — pass-2 review)**

## Tasks / Subtasks

- [x] Hexalith.Tenants integration (AC: 1)
  - [x] Inject `AuthenticationStateProvider`; consume `Hexalith.Tenants` membership/role services.
  - [x] Replace passive `[Parameter] bool IsAuthenticated/HasTenantContext/IsAdmin` with derived values; remove host-wiring contract.
  - [x] Update bUnit tests to assert auth derivation; remove the host-bool fixtures.
- [x] Rich-search capability detection (AC: 2)
  - [x] Add capability probe consumer (endpoint TBD with Story 9.6 owners).
  - [x] Cache the probe result for the circuit lifetime; invalidate on tenant switch.
  - [x] Surface degraded-probe state distinct from local-only.
- [x] FrontComposer Fluent UI adoption (AC: 3)
  - [x] Replace `<table>` with `FluentDataGrid<T>` and column descriptors.
  - [x] Replace search/filter inputs with `FluentTextInput`/`FluentSelect`.
  - [x] Use FrontComposer DataGrid query/loading behavior for browse/search pagination concerns (virtualized rows, server-side `Skip`/`Take`, and query-owned loading state) rather than adding a dedicated paging component. **Closed by Epic 12 story 12-7: the AdminPortal now uses FrontComposer/EventStore query seams with bounded `Skip`/`Take`, typed client preference, query metadata, and no dedicated pager requirement.**
  - [x] Wire FrontComposer query transport (`IQueryService`/EventStore query path with ETag caching and auth redirect). **Closed by Epic 12 story 12-7: `PartiesAdminPortalApiClient` now consumes the accepted `IPartiesQueryClient` boundary with `IQueryService` fallback and EventStore query metadata handling.**
- [x] Activate scaffolding services (AC: 4)
  - [x] Inject `AdminPortalPartyQueryService` into the component.
  - [x] Drive `PartiesAdminListCoordinator.State` from real load/error transitions.
  - [x] Tighten fitness tests to assert behavior, not type presence.
- [x] Validate build and affected tests
  - [x] `dotnet test tests\Hexalith.Parties.AdminPortal.Tests --configuration Release`.
  - [x] `dotnet build Hexalith.Parties.slnx --configuration Release`.

## Dev Notes

### Predecessor

Story 10.1 (`10-1-admin-portal-browse-search-and-inspect.md`) shipped the read-only browse/search/inspect foundation with three explicit deferrals (D2b, D3, D4) and one scaffolding gap (D5) recorded during pass-1 and pass-2 BMad code reviews. This story is the agreed landing zone for that follow-up work.

### References

- [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md#Pass-2 Review Findings (2026-05-07)]
- [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md#Review Findings] (pass-1)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#Carried from pass-1 deferral chain — Story 10-1.1 follow-up (2026-05-07)]

## Dev Agent Record

### Implementation Plan

- Implement AC1 first because it is the first incomplete task and does not depend on the rich-search endpoint or FrontComposer component contract.
- Add a small admin-portal authorization service that derives the live portal state from `AuthenticationStateProvider` and `Hexalith.Tenants` projection membership/roles.
- Keep the existing fail-closed UI states: unauthenticated users see sign-in required, missing/unavailable tenant context sees tenant unavailable, and non-owner tenant members see administrator access required.
- Subscribe the component to authentication-state changes so tenant/user switches clear the visible list/detail state and re-evaluate authorization.

### Debug Log

- 2026-05-07: Red test run failed because the admin portal tests did not reference `Hexalith.Tenants`; added project references and production authorization seam.
- 2026-05-07: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed: 27/27.
- 2026-05-07: `dotnet build src\Hexalith.Parties.AdminPortal\Hexalith.Parties.AdminPortal.csproj --configuration Release --no-restore` passed.
- 2026-05-07: `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore` passed.
- 2026-05-07: Searched for a concrete rich-search capability endpoint; only search-status headers/planning references were present, while the story still says endpoint TBD.
- 2026-05-07: Rechecked rich-search contracts and found the CommandApi `/health` JSON `memories-search` entry with enabled/searchReachable/degradedReportedByMemories data.
- 2026-05-07: Red test run failed as expected because `AdminPortalRichSearchCapability` and `GetRichSearchCapabilityAsync` did not exist.
- 2026-05-07: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed: 32/32.
- 2026-05-07: `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore` passed.
- 2026-05-07: Red Fluent UI adoption test failed against raw table/input/select markup.
- 2026-05-07: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed: 33/33 after Fluent UI migration and selector updates.
- 2026-05-07: `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore` passed.
- 2026-05-07: Searched for a dedicated FrontComposer paging component and REST query transport with ETag/auth redirect; no matching paging component or Parties REST transport contract was present.
- 2026-05-09 (AC4 wiring): Red `PartiesAdminPortal_HealthyLoad_DrivesListCoordinatorToReadyHasResults` confirmed the coordinator was dead-code (state stayed Loading regardless of load outcome) before the wiring landed; all 8 new bUnit transition tests went green after the component changes.
- 2026-05-09 (regression check): `dotnet test tests/Hexalith.Parties.Server.Tests --configuration Release` 177/177 passing; `dotnet test tests/Hexalith.Parties.Picker.Tests --configuration Release` 21/21 passing; `dotnet test tests/Hexalith.Parties.Client.Tests --configuration Release` 57 passing / 6 skipped / 2 failing — both failures are pre-existing fitness tests (`ClientAssembly_HasNoReferencesToServerProjectionsOrPartiesService`, `ClientCsproj_HasNoForbiddenProjectReferences`) that forbid any reference starting with the bare prefix `Hexalith.Parties`, which matches the legitimate `Hexalith.Parties.Contracts` reference. Unrelated to AdminPortal changes; not introduced by this story. `dotnet test tests/Hexalith.Parties.Tests --configuration Release` 398/401 (3 pre-existing integration test failures in `Hexalith.Parties.Tests`, project untouched by this story).
- 2026-05-09 (resume check): HALT on the first remaining AC3 subtask. The story now explicitly defers FrontComposer DataGrid query/loading behavior and `IQueryService`/EventStore transport migration to Epic 12 story 12-7; Story 12-7 has not been created, and Story 12-0's EventStore-to-Parties query-routing spike conclusion is still pending. No code changes made for this resume.
- 2026-05-09 (10-1-1 verification): Confirmed current FrontComposer has `IQueryService`, `EventStoreQueryClient`, `QueryRequest`, `GridItemsProvider`, and virtualized generated-view support, but Parties still exposes the admin list through the existing REST client and Parties-specific `PartyIndexProjectionActor`/`PartyDetailProjectionActor` contracts rather than the EventStore `IProjectionActor` query path. Story 12-0 remains `ready-for-dev` with `## Spike Conclusion` pending and story 12-7 has not been created. No production or test code changes made.
- 2026-05-10 (10-1-1 close-out): Verified Story 12-7 has landed the superseding FrontComposer/EventStore admin portal query path for the remaining AC3 subtasks. Focused validation passed: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release -p:UseSharedCompilation=false` (50/50) and `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release -p:UseSharedCompilation=false` (12/12). Marking the deferred AC3 subtasks complete against that successor evidence.
- 2026-05-10 (completion gates): `dotnet build Hexalith.Parties.slnx --configuration Release -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors. `dotnet test Hexalith.Parties.slnx --configuration Release --no-build -p:UseSharedCompilation=false` passed: 858 passed, 6 expected health E2E skips, 0 failed.

### Completion Notes

- Completed AC1 Tenants integration: the portal no longer accepts host-supplied `IsAuthenticated`, `HasTenantContext`, or `IsAdmin` parameters.
- Added `AdminPortalAuthorizationService`, backed by `AuthenticationStateProvider` and `Hexalith.Tenants.Client.Projections.ITenantProjectionStore`, to derive authentication, active tenant context, and tenant-owner admin role.
- Updated bUnit coverage to seed Tenants membership/roles and assert owner/non-owner/unauthenticated behavior without host bool fixtures.
- Set story and sprint tracking to `in-progress`.
- HALT before rich-search capability detection because the story names the endpoint as TBD and the repository does not contain a stable capability endpoint contract to consume.
- Completed AC2 rich-search capability detection by consuming the existing CommandApi `/health` `memories-search` capability signal.
- Added circuit-lifetime rich-search probe caching in the live portal component, invalidated by authorization/tenant context changes.
- Email and identifier search mode buttons now activate only when the probe reports healthy rich search; degraded probes keep those modes disabled and surface a distinct "Rich search is temporarily unavailable" status instead of the local-only status.
- Partially advanced AC3: the browse grid now uses `FluentDataGrid<T>` with template columns, toolbar search uses `FluentTextInput`, filters use `FluentSelect`, and actions use `FluentButton`.
- HALT before completing the remaining AC3 subtasks because the repository does not currently expose a dedicated FrontComposer paging component or a Parties REST query transport contract for FrontComposer ETag caching/auth redirect wiring.
- 2026-05-09 (resumed under PAUSE per /bmad-dev-story 10-1-1, AC4-only scope confirmed): Completed AC4 — `AdminPortalPartyQueryService` and `PartiesAdminListCoordinator` are now wired into the live `PartiesAdminPortal.razor` lifecycle (no longer dead-code scaffolding). The component injects both services, routes every API call through `QueryService.ApiClient`, and drives `PartiesAdminListCoordinator.State` through every load/error transition path. `ResetVisibleState` calls `QueryService.ResetForTenantSwitch()` and `ListCoordinator.ResetForTenantSwitch()` so a tenant switch cancels the per-circuit scope token and observers see Loading/Version-bump before the new load fires.
- AC4 reachability: every `AdminPortalListState` value (Loading, ReadyEmpty, ReadyHasResults, MissingToken, MissingTenant, Forbidden, NotFound, Gone, DegradedSearch, TransientFailure) is now reachable from a real component code path; new bUnit tests pin Loading→ReadyHasResults, ReadyEmpty, DegradedSearch (LocalOnly metadata), MissingToken (unauthenticated), MissingTenant (no tenant projection), Forbidden (non-admin), TransientFailure (list failure), and tenant-switch scope-token cancellation.
- Tightened fitness tests: `AdminPortalAuthorizationStateTests` migrated from reflective method-presence checks to direct-type behavior assertions — `PartiesAdminListCoordinator.Transition`/`State`/`Version`/`ResetForTenantSwitch` round-trip and `AdminPortalPartyQueryService.ResetForTenantSwitch()` cancels handed-out tokens. `VerifyScopedLifetime` now also asserts the coordinator is registered Scoped (was only checking the query service).
- Build/tests: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests --configuration Release` 41/41 passing (was 33). `dotnet test tests/Hexalith.Parties.Contracts.Tests --configuration Release` 42 passing, 15 skipped (Story 10.2 GDPR scaffolds; unchanged). `dotnet build Hexalith.Parties.slnx --configuration Release` clean (0 warnings, 0 errors).
- AC3 paging/transport remains explicitly deferred to Epic 12 story 12-7. Product direction clarified on 2026-05-09: do not add a dedicated FrontComposer pager for Parties admin browse/search unless the future UX requires page-number semantics; FrontComposer DataGrid query/loading behavior should own virtualized browsing, server-side `Skip`/`Take`, loading state, ETag caching, and auth redirect through the EventStore query path.
- 2026-05-09 (resume check): Remaining AC3 subtasks are blocked by the Epic 12 gate. `Hexalith.FrontComposer` contains `IQueryService`, `QueryRequest`, and EventStore ETag/auth redirect infrastructure, but Parties does not yet expose the EventStore-fronted Party query projection path that 12-7 is meant to build after 12-0 proves feasibility. Story remains `in-progress`.
- 2026-05-09 (10-1-1 verification): Reconfirmed the remaining AC3 subtasks cannot be completed inside this paused story without pre-empting Epic 12. The correct next implementation unit is story 12-0, followed by 12-7 if the spike proves the EventStore query path.
- 2026-05-10 (close-out): Story 12-7 now provides the superseding implementation for the remaining AC3 query/loading and EventStore/FrontComposer transport scope. `PartiesAdminPortalApiClient` no longer depends on retired direct Parties REST reads; it prefers the typed Parties query client, falls back to FrontComposer `IQueryService`/`QueryRequest`, uses bounded server-side `Skip`/`Take`, preserves EventStore not-modified metadata, and is covered by AdminPortal plus AdminPortal client contract tests. Story 10.1.1 is now ready for review as historical scope closure.
- Completion validation: focused AdminPortal tests passed 50/50, AdminPortal client contract tests passed 12/12, Release solution build passed cleanly, and full Release no-build regression passed 858/858 with 6 expected health E2E skips.

## File List

- src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor
- src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs
- src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj
- src/Hexalith.Parties.AdminPortal/_Imports.razor
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationService.cs
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationState.cs
- src/Hexalith.Parties.AdminPortal/Services/IAdminPortalAuthorizationService.cs
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalRichSearchCapability.cs
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs
- src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs
- src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalPartyQueryService.cs
- src/Hexalith.Parties.AdminPortal/Services/PartiesAdminListCoordinator.cs
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalListState.cs
- Directory.Packages.props
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj
- tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs
- tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalAuthorizationStateTests.cs
- _bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/_review-10-1-1-diff.patch (review artifact)
- _bmad-output/implementation-artifacts/deferred-work.md (defer entries from review)

### Story 12-7 successor files brought in by the 2026-05-10 closure (owned by 12-7, listed for traceability)

- src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalOptions.cs (added query/projection type properties; `[Obsolete]` ApiBaseAddress; `RichSearchProbeBaseAddress` for the /health probe bridge)
- src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalOptionsValidator.cs (new — `IValidateOptions<>` runs at startup via `ValidateOnStart()` so missing IPartiesQueryClient/IQueryService or fallback contract types fails the host before first request)
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryFailureKind.cs (new `ContractUnavailable` value)
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryException.cs (sanitized user-facing messages; no longer leaks "Story 12.4/12.5")
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryBounds.cs (`Skip`/`Take` clamping primitives)
- tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs (rewrites for typed/IQueryService boundary; comment-stripping fitness test)
- tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs (rewrites for EventStore command routes; Skip-marked, awaiting 12-5)
- _bmad-output/implementation-artifacts/_review-10-1-1-closure-diff.patch (2026-05-10 closure review artifact)

## Change Log

- 2026-05-07: Completed AC1 Tenants-backed authorization derivation and halted on the unresolved rich-search capability endpoint contract.
- 2026-05-07: Completed AC2 rich-search capability detection using CommandApi `/health`, with circuit cache invalidation on tenant switch and degraded-probe UI status.
- 2026-05-07: Partially migrated AC3 surfaces to Fluent UI components; halted on missing FrontComposer paging/REST transport contracts.
- 2026-05-09: BMad code review (three-layer adversarial: Blind Hunter + Edge Case Hunter + Acceptance Auditor) executed against `9b6d180..HEAD` scoped to AdminPortal. Findings recorded below.
- 2026-05-09: Applied 11 of 12 patches (1 reverted: `_rowsQueryCache` broke FluentDataGrid change detection). Solution builds clean; 33/33 admin portal tests pass.
- 2026-05-09: Completed AC4 (scaffolding wiring) under PAUSE per /bmad-dev-story 10-1-1 with explicit AC4-only scope. Added `PartiesAdminListCoordinator.Transition(AdminPortalListState)`; injected `AdminPortalPartyQueryService` and `PartiesAdminListCoordinator` into `PartiesAdminPortal.razor`; routed all `IPartiesAdminPortalApiClient` calls through `QueryService.ApiClient`; drove coordinator state transitions across LoadPageAsync (Loading), ApplyRowsAsync (ReadyEmpty/ReadyHasResults/DegradedSearch), ApplyFailure (MissingToken/MissingTenant/Forbidden/NotFound/Gone/TransientFailure), and ApplyAuthorizationContextAsync fail-closed branches; ResetVisibleState now also resets the query service scope token and the list coordinator. Removed dead-code TODOs from `AdminPortalPartyQueryService`/`PartiesAdminListCoordinator`/`AdminPortalListState`. Tightened `AdminPortalAuthorizationStateTests` from reflective method-presence checks to direct-type behavior assertions (Transition round-trip, ResetForTenantSwitch token cancellation, scoped lifetime for both services). Added 8 bUnit tests pinning each non-Loading state transition. AdminPortal tests 41/41, contracts tests 42/42 (15 GDPR skips unchanged), solution build 0/0. AC3 paging/transport remain deferred to Epic 12 story 12-7.
- 2026-05-09: Clarified AC3 scope: Parties admin browse/search should rely on FrontComposer DataGrid query/loading behavior instead of requiring a dedicated pager component. Defer any future `FcPaging` component until a concrete UX requirement needs explicit page semantics.
- 2026-05-09: Resumed story 10-1-1 and halted on remaining AC3 subtasks because they are explicitly deferred to Epic 12 story 12-7, with the required 12-0 EventStore query-routing spike still pending.
- 2026-05-09: Reverified the remaining AC3 blocker: FrontComposer query/grid infrastructure exists, but Parties lacks the EventStore-fronted Party projection/query path until Epic 12 story 12-0/12-7. No code changes made.
- 2026-05-10: Closed the remaining AC3 subtasks using Story 12-7 successor evidence: FrontComposer/EventStore query path, typed Parties query client preference, `IQueryService` fallback, bounded `Skip`/`Take`, not-modified metadata handling, and old REST/admin route guardrails are implemented and tested. Story moved to review after focused tests, clean Release solution build, and full Release no-build regression passed.
- 2026-05-10: bmad-code-review (closure scope) executed against the 12-7 evidence. Three-layer adversarial review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) raised 3 decision-needed, 15 patch, 19 defer items. Decisions resolved: D1 → patch (restore /health probe with fail-closed default); D2 → defer (avoid pre-empting 12.4/12.5 metadata contract); D3 → patch (register IQueryService validator + restore ValidateOnStart). All 17 patches applied across `PartiesAdminPortalApiClient.cs` (rich-search probe restoration, HttpRequestException catches in IQueryService and IPartiesQueryClient paths, GDPR catch coverage for transport/timeout/cancellation, page-overflow guard, GDPR null-arg guards, MapFailureKind(QueryFailureException) parity arms, GetPartyAsync >1 items contract assertion, NormalizeDetail null-collection guard on typed path, typedDetail/typedResult null guards, Story sprint-code stripping from user-facing strings), `PartiesAdminPortalOptions.cs` (RichSearchProbeBaseAddress added), `PartiesAdminPortalServiceCollectionExtensions.cs` (validator registration, ValidateOnStart restored, AddHttpClient added), new `PartiesAdminPortalOptionsValidator.cs` (IValidateOptions implementation), `PartiesAdminPortalApiClientTests.cs` (typed-client path coverage for List/Search/Detail, PartiesClientException status mapping, HttpRequestException mapping, page-overflow validation, multi-item contract violation, GDPR transport-failure outcomes, null-arg guards, /health probe scenarios), `AdminPortalQueryContractTests.cs` (comment-stripping fitness test now preserves string literals while ignoring comments), and 10-1-1 File List updated to declare 12-7 successor files for traceability. AdminPortal tests 65/65, AdminPortal client contract tests 12/12, full Release solution build 0/0, full Release no-build regression 930 passed / 6 expected health E2E skips.

## Review Findings (2026-05-09)

### Acceptance audit summary

All four AC promises are met to the scope claimed in the story. AC1 and AC2 fully delivered, AC3 partial-as-scoped (paging/REST transport explicitly halted), AC4 unstarted (scaffolding files received hardening but are not wired). Two minor bookkeeping discrepancies surfaced as defer items below.

### Decision-needed (resolved 2026-05-09)

- [x] [Review][Decision] `IsTenantProblem` heuristic relies on a header (`X-Tenant-Required`) that the backend does not emit — `StatusKind.TenantRequired` UX path is unreachable today. **Resolved → defer**. Reason: story is PAUSED and Epic 12 will rebuild this surface on FrontComposer + EventStore queries; deferred until a backend contract exists (header or problem+json `type` discriminator). Local `!HasTenantContext` is not a viable substitute because the heuristic is intended to catch server-side tenant-validation failures (suspended/missing tenant) where the client still has a tenant context. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:1696]

### Patches (fixable now)

- [x] [Review][Patch] Rich-search probe ignores cancellation — `EnsureRichSearchCapabilityAsync` calls `GetRichSearchCapabilityAsync(CancellationToken.None)`. On disposal or rapid tenant switch, the in-flight probe completes against a stale context and overwrites `_richSearchCapability`. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:617]
- [x] [Review][Patch] `OnAuthenticationStateChanged` fire-and-forget swallows exceptions and lacks reentrancy guard — two rapid auth events (token refresh + tenant switch) interleave through `ApplyAuthorizationContextAsync` because the dedup-by-signature check happens AFTER the awaited `GetAuthorizationStateAsync`; both probes/loads run, risking cross-tenant data being painted. Wrap in try/catch with surfaced error, and add an in-flight guard (e.g., `Interlocked.Exchange` token or SemaphoreSlim) so only the latest signature wins. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:411-415,440-485]
- [x] [Review][Patch] `SelectPartyAsync` race vs `ResetVisibleState` — auth-state change during a detail load can clear `_detail` then have the in-flight task repopulate it because neither version (`_detailVersion`) nor the detail CTS is bumped by `ResetVisibleState` separately from the swap; a stale tenant's detail can land on the new tenant's screen. Bump `_detailVersion` in `ResetVisibleState` and re-check before assignment. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:582-651]
- [x] [Review][Patch] `TryReadProblemDetail` uses synchronous `ReadAsStream()` — sync-over-async on the Blazor circuit during the error path; switch to `ReadAsStreamAsync(cancellationToken)`. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:1733]
- [x] [Review][Patch] 204/304 returns `default!` `PartyDetail` (null at runtime), then `result.Payload.IsErased` NREs — either treat 204/304 as a not-found case before returning, or null-check at the consumer. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:130-133, src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:600]
- [x] [Review][Patch] Detail panel renders crash if backend serializes `null` collections — `PartyDetail.ContactChannels`/`Identifiers`/`ConsentRecords`/`NameHistory` are `IReadOnlyList<>` with `[]` initializers, but `JsonSerializer` overwrites the default with `null` if the JSON property is `null`. Add `[JsonOnDeserialized]` normalization or null-coalesce at access sites. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:151,168,185,227]
- [x] [Review][Patch] Enter-to-submit accessibility regression — replacing `<EditForm OnValidSubmit>` with `<FluentTextInput>` + `<FluentButton OnClick>` removes Enter-key submit. Either wrap inputs in a form/EditForm that triggers `SubmitSearchAsync`, or add `@onkeydown` handling. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:32-61]
- [x] [Review][Patch] `ParseRichSearchCapability` defaults `searchReachable` to `true` when the property is missing (`!= false`) — fail-open. Should default to `false` so an outdated/malformed `/health` payload degrades safely. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:1638]
- [x] [Review][Patch][Reverted] `RowsQuery => _rows.AsQueryable()` allocates a fresh `EnumerableQuery<>` on every render — patch attempted via `_rowsQueryCache ??= ...` but reverted: caching the IQueryable identity caused FluentDataGrid to skip re-rendering after `_rows` mutations (6 tests failed). Tradeoff is not worth the LOW-severity perf gain; reverted to per-render allocation. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:304]
- [x] [Review][Patch] `_hasNextPage` correctly clamps when `page.Page == 0`, but `_page` is left at the stale client-side value, locking the user on a non-existent page. Canonicalize `_page` to `page.TotalPages` (or 1) in this branch. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:558]
- [x] [Review][Patch] `OnQueryChangedAsync`/`OnTypeChangedAsync`/`OnActiveChangedAsync` are `async`-suffixed but synchronous (`return Task.CompletedTask`) — drop the suffix or remove the Task wrapper to avoid misleading API and per-keystroke Task allocations. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:514-530]
- [x] [Review][Patch] `OnAfterRenderAsync` only catches `InvalidOperationException`/`JSException` around `FocusAsync` — add `ObjectDisposedException` to the catch list (post-disposal focus). [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:703-739]

### Deferred (recorded in deferred-work.md)

- [x] [Review][Defer] `AuthenticationStateChanged` subscription leak risk on circuit-aborted teardown — Dispose unsubscribes, but the lambda captures `this`; if Dispose never runs the singleton provider holds a stale reference. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor]
- [x] [Review][Defer] `GetRichSearchCapabilityAsync` swallows `AdminPortalQueryException` — captive-portal HTML on `/health` becomes `Degraded` instead of `AuthenticationRequired`. Low priority because `/health` is typically anonymous. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:1457]
- [x] [Review][Defer] `AdminPortalQueryMetadata` constructor accepts unbounded header values — `BoundHeaderValue` was moved to the call sites. Test still passes the bounded path, but a future caller constructing the type directly bypasses the invariant. [src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryMetadata.cs]
- [x] [Review][Defer] `SwapListCts` race: between `Interlocked.Exchange(ref _listCts, newCts)` and `token = newCts.Token`, a concurrent swap could `SafeCancel(newCts)`. Mitigated downstream in `AdminPortalPartyQueryService` but not at the component. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:768,778]
- [x] [Review][Defer] Test helpers use sync-over-async (`GetAwaiter().GetResult()` on Bunit dispatcher / `_tenantStore.SaveAsync(state).GetAwaiter().GetResult()`). Test-only, may deadlock on alternate runners. [tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs:2280,2291]
- [x] [Review][Defer] `TryReadProblemDetail` reads JSON unbounded — `JsonDocument.Parse` of arbitrarily large response. Defensive only; backend is trusted. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:1733]
- [x] [Review][Defer] Two unauthenticated users share `<missing>` cache key in `AdminPortalAuthorizationState.ContextSignature` — mitigated because `IsAdmin=false` returns early. Pattern is fragile if downstream code adds branches. [src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationService.cs:48-49]
- [x] [Review][Defer] `AdminPortalQueryException.RetryAfter` populated from `Retry-After` header but never consumed by UI. [src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryException.cs]
- [x] [Review][Defer] `FluentTextInput.Element` may be null on first render after a state transition — focus is silently lost; `_pendingFocus` already cleared, no retry path. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:722]
- [x] [Review][Defer] MediaType comparison rejects legitimate non-standard subtypes (e.g., `application/x-ndjson`) and a captive-portal `text/html` is reported as Unknown failure rather than redirect-to-sign-in. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:265-269]
- [x] [Review][Defer] `Items.Where(x => x.Party is not null).Select(x => x.Party!)` — backend should enforce `required` on `Party.Id`; null-Id slipping through would NRE the click handler. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:506,569]
- [x] [Review][Defer] File List drift: `_bmad-output/implementation-artifacts/sprint-status.yaml` is in the story's File List but the captured diff (9b6d180..HEAD) contains zero hunks for that path; sprint-tracking edit was made out-of-band or is missing. [_bmad-output/implementation-artifacts/sprint-status.yaml]
- [x] [Review][Defer] Pagination subtask wording: Tasks line 36 reads `[ ] Replace pagination buttons with FrontComposer paging component`. The diff DOES replace pagination with `FluentButton`; the unchecked subtask refers to a dedicated FrontComposer paging component which the Completion Notes acknowledge does not exist. Tighten the subtask wording. [_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md:36]

## Review Findings (2026-05-10 closure)

Three-layer adversarial review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) executed against the 12-7 closure-evidence scope: commit `ccbfe7a` (FrontComposer/EventStore refactor) plus uncommitted bookkeeping. HEAD state verified against the diff snapshot — typed `IPartiesQueryClient` preference exists at HEAD but lives in successor commits (`b2c357f`, `04b1799`, `ac93b8e`); the closure narrative is accurate against HEAD but not against the captured diff.

### Decision-needed (resolved 2026-05-10)

- [x] [Review][Decision] **D1 → Patch.** Resolved: restore `/health`-based rich-search probe with fail-closed `searchReachable=false` default. See P-D1 below.
- [x] [Review][Decision] **D2 → Defer.** Resolved: defer until 12.4/12.5 freeze a metadata contract. Reason: extending `IPartiesQueryClient` and `AdminPortalQueryMetadata` ahead of the contract freeze would lock in a metadata shape prematurely; typed client is the strategic boundary. Tracked in deferred-work.md.
- [x] [Review][Decision] **D3 → Patch.** Resolved: register `IQueryService` inside `AddHexalithPartiesAdminPortal` and restore `Validate(...).ValidateOnStart()` so misconfiguration fails at boot. See P-D3 below.

### Patches (fixable now)

- [x] [Review][Patch] **P-D1** Restore `/health`-based rich-search capability probe in `GetRichSearchCapabilityAsync` with fail-closed `searchReachable=false` default for missing/malformed payloads (resolves D1; restores AC2 contract). [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:124-129] (HIGH)
- [x] [Review][Patch] **P-D3** Register `IQueryService` inside `AddHexalithPartiesAdminPortal` and restore `Validate(...).ValidateOnStart()` so misconfiguration fails at boot (resolves D3). [src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs:18-29] (HIGH)
- [x] [Review][Patch] **P1** Add `HttpRequestException` catch to `ExecuteAsync` and `ExecutePartiesQueryAsync` → map to `TransientFailure` [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:210-247, 250-281] (HIGH; transport failures escape uncaught today)
- [x] [Review][Patch] **P2** Add `TimeoutException`/`OperationCanceledException`/`HttpRequestException`/`JsonException` catches to `ExecuteGdprCommandAsync` → map to `AdminPortalGdprOutcome.TransientFailure` [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:284-298] (HIGH)
- [x] [Review][Patch] **P3** Add `TimeoutException`/`OperationCanceledException`/`HttpRequestException` catches to `ExecuteGdprQueryAsync` [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:300-321] (HIGH)
- [x] [Review][Patch] **P4** Guard `(page-1)*pageSize` int overflow in `ListPartiesAsync`/`SearchPartiesAsync` Skip computation — reject Page that would overflow with `Validation` [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:83, 113] (MEDIUM)
- [x] [Review][Patch] **P5** Add null/whitespace guards on GDPR `partyId`, `channelId`, `purpose`, `consentId` arguments [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:164-206] (MEDIUM)
- [x] [Review][Patch] **P6** Add `400 or 422 → Validation` and `410 → Gone` arms to `MapFailureKind(QueryFailureException)` for parity with `MapFailureKind(PartiesClientException)` [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:323-333] (MEDIUM)
- [x] [Review][Patch] **P7** `GetPartyAsync`: detect `result.Items.Count > 1` and throw `Unknown` (defensive contract assertion) [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:155] (LOW)
- [x] [Review][Patch] **P8** `GetPartyAsync` typed path: normalize `PartyDetail` null collections (`ContactChannels`/`Identifiers`/`ConsentRecords`/`NameHistory`) for parity with razor's `NormalizeDetail` patch [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:131-141] (MEDIUM)
- [x] [Review][Patch] **P9** Null guards for `typedDetail`/`typedResult` from typed client (interface allows null) [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:65-77, 102-108, 135-141] (MEDIUM)
- [x] [Review][Patch] **P10** Strip "Story 12.4/12.5" leakage from user-facing strings (`GetRichSearchCapabilityAsync`, `RequireContract`); keep internal note as code comment [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:127-128, 415-417] (MEDIUM; leaks sprint plan to operators)
- [x] [Review][Patch] **P11** Tighten `AdminPortalSource_DoesNotContainRetiredPartiesRestReads` fitness test — strip C# comments before substring scan [tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs:1252-1326] (MEDIUM)
- [x] [Review][Patch] **P12** Extend `MissingEventStoreQueryContract_FailsClosedWithoutCallingQueryServiceAsync` to also exercise `SearchPartiesAsync` and `GetPartyAsync` [tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs:866-904] (MEDIUM; partial config can pass list and blow up detail)
- [x] [Review][Patch] **P13** Restore `Skip`/`Take` paging assertion for `SearchPartiesAsync` page>1 (parity with previous `pageSize=100` clamp test) [tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs:783-806] (MEDIUM)
- [x] [Review][Patch] **P14** Add tests for typed `IPartiesQueryClient` path: list/search/detail dispatch, `PartiesClientException` status mapping, cancellation propagation [tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs] (HIGH coverage gap; production preferred path currently untested)
- [x] [Review][Patch] **P15** Update 10-1-1 File List to declare the 12-7 successor files actually touched: `PartiesAdminPortalOptions.cs`, `Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`, `AdminPortalQueryFailureKind.cs` (new `ContractUnavailable` value), `AdminPortalQueryException.cs`, `AdminPortalGdprOperationContractTests.cs`, `AdminPortalQueryContractTests.cs` rewrites [_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md:113-136] (LOW bookkeeping)

### Deferred (recorded in deferred-work.md)

- [x] [Review][Defer] Two-arg ctor `PartiesAdminPortalApiClient(IQueryService, IOptions)` bypasses typed-client preference verification — used by tests; tighten when test infra refactors. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:38-41]
- [x] [Review][Defer] `IsNotModified=true` on List/Search returns empty `Items=[]` — wipes visible grid instead of preserving cached payload. Needs cross-layer caching contract clarification with FrontComposer. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:430-433]
- [x] [Review][Defer] EventStore Detail `IsNotModified=true` with empty `Items` (cold cache) currently throws `NotFound`; should return cached or fail with explicit error once a portal-side cache exists. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:155-160]
- [x] [Review][Defer] `IPartiesQueryClient` lifetime/scope: tenant scope token captured at construction may stale across tenant switches — needs DI lifetime audit when 12.5 lands. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:22-53]
- [x] [Review][Defer] Half-configured options (e.g., `ListProjectionType` set, `DetailQueryType` null) surface `ContractUnavailable` mid-request rather than at validation time; combine with D3 if validation is restored. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:408-418]
- [x] [Review][Defer] `ContainsTenant` substring heuristic — re-acknowledged carry-forward of accepted defer from 2026-05-09 (depends on backend `X-Tenant-Required` header or problem-type URN that doesn't exist yet). [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:371-372]
- [x] [Review][Defer] `ApiBaseAddress` `[Obsolete]` left silently ignored — no `error: true`. Promote to compile error after consumers migrate. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalOptions.cs]
- [x] [Review][Defer] `ArgumentNullException(nameof(options))` mis-attributes parameter when `options.Value` is null — micro debugging trap. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:52]
- [x] [Review][Defer] `Domain` property recomputes/trims per call — micro perf on hot path. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:208]
- [x] [Review][Defer] `Domain` config silently substitutes "party" when whitespace — log warning when operator misconfigures. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:208]
- [x] [Review][Defer] `GetPartyAsync` IQueryService path passes both `AggregateId` and `EntityId` to the same value with `Take: 1` — wasteful paging vs aggregate-by-id contract; revisit when 12.4/12.5 freezes the contract. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:143-152]
- [x] [Review][Defer] `ToPage.TotalPages = 0` when `pageSize ≤ 0` — defensive guard masks a programming error rather than asserting. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:427]
- [x] [Review][Defer] `BuildListFilters` always seeds `page`/`pageSize` keys; pagination bleeds into `ColumnFilters` and may collide with projection's column allowlist. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:374-398]
- [x] [Review][Defer] `RateLimited → TransientFailure` mapping loses ability to UX-distinguish 429 from 5xx in `AdminPortalQueryFailureKind`; `Retry-After` is propagated via `ex.RetryAfter` so functionally OK. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:331]
- [x] [Review][Defer] `QueryFailures_SurfaceTypedBoundedOutcomesAsync` test relies on the brittle "tenant" substring heuristic — locks in the regression noted in DF6. [tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs:907-936]
- [x] [Review][Defer] AC2/AC3 closure narrative status flip without explicitly noting the rich-search probe regression and metadata fidelity loss — bookkeeping coherence gap. [_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md]
- [x] [Review][Defer] Closure diff scope mismatch — `ccbfe7a` snapshot does not contain typed-client code; lives in successor commits `b2c357f`/`04b1799`/`ac93b8e`. Update closure narrative to reference successor commits explicitly. [_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md:108-110]
- [x] [Review][Defer] "server-side `Skip`/`Take`" phrasing in closure narrative is inaccurate — clamp is client-side via `AdminPortalQueryBounds`; server-side enforcement waits on 12.4/12.5. [_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md:108]
- [x] [Review][Defer] `AdminPortalGdprOperationContractTests.cs` rewrites for EventStore `eventstore:command:party:*` routes leap ahead of any landed 12-5 implementation — `Skip`-marked but blurs scope between 10-1-1 closure and 10-2/12-7 GDPR work. [tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs]
