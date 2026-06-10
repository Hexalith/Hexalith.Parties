---
baseline_commit: ec2676b
---

# Story 2.2: Parties list with search, filters, and paging (FR-Admin-1)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an admin,
I want to search and filter the parties list,
so that I can find a record quickly.

This is a reuse-and-enhance story for the existing `Hexalith.Parties.AdminPortal` browse surface. Do not build new `PartiesListPage.razor`, `PartyDetailPage.razor`, or parallel Admin list state. `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` already owns `/admin/parties`, `/admin/parties/{RoutePartyId}`, `/admin/parties/{RoutePartyId}/gdpr`, route-level `Admin` authorization, list/detail loading, tenant-switch cancellation, paging, query failure mapping, and GDPR panel composition. Enhance that implementation so FR-Admin-1 is true against the current code, especially the missing debounced search and combined search plus type/active filters.

## Acceptance Criteria

1. **AC1 - Debounced server-driven display-name search.** Given an Admin is on `/admin/parties`, when they type in the search input, then a debounced server request runs without requiring the Search button. The request uses `SearchPartiesAsync` in the allowlisted display-name lexical mode only (`DisplayName` or `Lexical`, depending on the existing query contract path), resets to page 1 for changed criteria, cancels/ignores superseded requests, and never calls email, identifier, semantic, graph, hybrid, or temporal-name search.

2. **AC2 - Search combines with Person/Organization and active filters.** Given a non-empty search query and selected type/active filters, when the debounced request fires, then the type and active filters are applied to the server-driven result set, not silently ignored and not disabled in the UI. The implementation may extend the typed query/client contract or the AdminPortal request layer, but the final observable behavior must satisfy `SearchPartiesAsync(query, page, pageSize, type, active, mode, ...)` semantics and preserve Central Package Management.

3. **AC3 - Paging is server-driven and stable.** Given list/search results span multiple pages, when the Admin uses Previous/Next, then the current query, type filter, active filter, page, and bounded page size are preserved. Page size is still bounded by `AdminPortalQueryBounds`, page resets only when search/filter criteria change, and stale/out-of-order responses cannot repaint the wrong tenant, criteria, or page.

4. **AC4 - Stale/degraded reads render last-known rows.** Given the list query returns degraded metadata, local-only metadata, or a stale/not-modified signal, when the list renders, then previously confirmed rows remain visible where available, a freshness indicator announces the degraded/stale state politely, and the screen never blanks or throws. A true cold load renders a skeleton or equivalent non-spinner-only loading affordance.

5. **AC5 - Empty state is recoverable.** Given a search/filter request completes with no matches, when the list renders, then it shows the exact empty guidance `"No parties match."` or a localized equivalent with that meaning, plus a clear-filters action that resets search, type, active, and paging. It must not render a dead-end empty state.

6. **AC6 - Erased-party handling remains privacy-preserving.** Given list or search results include erased parties or detail lookup returns erased/gone, when the Admin browse surface renders, then erased parties are excluded from the list/search result or shown only as an erased status with no personal fields. Do not client-filter in a way that leaks erased-count mismatches; prefer the existing server count contract and current erased-detail removal behavior.

7. **AC7 - Row activation works by pointer and keyboard.** Given focus is in the parties grid, when the Admin clicks a row or uses arrow-key row navigation and presses Enter, then the selected party opens `/admin/parties/{id}` using the existing safe route-id behavior. Type-ahead from the grid focuses the search input. Keyboard behavior must not expose tenant-scoped identifiers or display names in the route.

8. **AC8 - Authorization and no-data-call guarantees are preserved.** Given a Consumer, non-Admin, unauthenticated user, or missing tenant context reaches `/admin/parties`, when the component evaluates authorization, then no list/search/detail data call occurs and no party row/detail data is rendered. Route-level `[Authorize(Policy = "Admin")]`, `AuthorizeRouteView`, and internal `AdminPortalAuthorizationService` checks remain defense in depth.

## Tasks / Subtasks

### Part A - Request semantics and debounce - AC1, AC2, AC3

- [x] **Task 1 - Add deterministic debounce support to the Admin browse surface** (`src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`) (AC1)
  - [x] Change search input behavior so typing schedules the query rather than only mutating `_filters.Query`.
  - [x] Use a cancellable/versioned debounce path that composes with the existing `_listVersion`, `_listCts`, `_lifetimeCts`, and tenant-switch reset logic.
  - [x] Return `Task`/`ValueTask` from async handlers; never use `async void`.
  - [x] Keep `ConfigureAwait(false)` on awaited code in C# blocks.
  - [x] Keep the explicit Search button as an immediate-submit affordance if desired, but it must not be the only path.

- [x] **Task 2 - Support combined search plus type/active filters** (`PartiesAdminPortal.razor`, `AdminPortalSearchRequest`, `PartiesAdminPortalApiClient`, `IPartiesQueryClient`/`HttpPartiesQueryClient` if required) (AC1, AC2)
  - [x] Remove the current "search mode disables filters" behavior for the Person/Organization and active filters.
  - [x] Stop creating `AdminPortalSearchRequest` with `Type = null` and `Active = null` when `_filters.Type` or `_filters.Active` is selected.
  - [x] Ensure the typed client path does not drop filters. Current `IPartiesQueryClient.SearchPartiesAsync` has no type/active parameters, so either extend it additively or apply an equivalent server-supported filtered search path.
  - [x] Preserve fail-closed search mode: only display-name lexical search is enabled for this story. Rich search capability buttons must not imply email/identifier search is part of FR-Admin-1.
  - [x] Do not use client-side filtering as the main solution for type/active; it breaks paging/count semantics and can leak erased-count discrepancies.

- [x] **Task 3 - Preserve paging criteria across requests** (`PartiesAdminPortal.razor`, request records, API client tests) (AC3)
  - [x] Reset `_page = 1` only on changed query/type/active/clear-filters criteria.
  - [x] Keep bounded page/pageSize behavior through `AdminPortalQueryBounds`.
  - [x] Preserve query/type/active values on Previous/Next.
  - [x] Keep the existing canonicalization of `_page` from the server response.

### Part B - UI states, shared primitives, and accessibility - AC4, AC5, AC6, AC7

- [x] **Task 4 - Render freshness with the shared indicator instead of only inline text** (`PartiesAdminPortal.razor`; possibly project references/shared package decision) (AC4)
  - [x] Prefer reusing `DataFreshnessIndicator` and `PartyStateBadge` semantics established in `src/Hexalith.Parties.UI/Components/Shared`.
  - [x] Do not make `Hexalith.Parties.AdminPortal` reference `Hexalith.Parties.UI` directly. If reuse requires a shared RCL/contract location, move primitives to a host-agnostic location or compose them from the host without reversing dependency direction.
  - [x] Map `PagedResult.Freshness` and `AdminPortalQueryMetadata` into fresh/stale/degraded UI state.
  - [x] Use `role="status" aria-live="polite"` for freshness/status transitions.
  - [x] Render last-known rows on degraded/stale reads instead of clearing `_rows` before a replacement payload succeeds.

- [x] **Task 5 - Add cold-load and empty-state affordances** (`PartiesAdminPortal.razor`, `AdminPortalLabels`) (AC4, AC5)
  - [x] Replace any spinner-only cold load with a `FluentDataGrid` skeleton, placeholder rows, or equivalent accessible loading state.
  - [x] Change the search empty copy from `"No parties match the current filters"` to `"No parties match."` or localized equivalent.
  - [x] Ensure the clear-filters action resets query, type, active, date filters, page, pending debounce, and focus back to search.
  - [x] Keep empty/status text PII-free and tenant-neutral.

- [x] **Task 6 - Preserve erased and route privacy behavior** (`PartiesAdminPortal.razor`, API client if needed) (AC6)
  - [x] Keep the existing "server filters erased; do not client-filter counts" rule unless the server contract changes to preserve count semantics.
  - [x] Keep erased detail payloads from rendering contact channels, identifiers, name history, or personal display data.
  - [x] Keep safe route-id validation and do not route tenant-scoped ids containing `:` to the browser URL.

- [x] **Task 7 - Add keyboard row activation and type-ahead focus** (`PartiesAdminPortal.razor`) (AC7)
  - [x] Provide row focus management compatible with `FluentDataGrid`.
  - [x] Support arrow-key row navigation and Enter opening the selected row.
  - [x] Support type-ahead from the grid by moving focus to the search input without stealing focus on ordinary status updates.
  - [x] Preserve pointer row/button activation and existing safe navigation behavior.

### Part C - Tests and verification - AC1-AC8

- [x] **Task 8 - Update component tests for the new FR-Admin-1 contract** (`tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`) (AC1-AC7)
  - [x] Replace tests asserting filters are disabled in search mode with tests proving type/active remain enabled and are forwarded with search.
  - [x] Add a debounce test using deterministic timing or a test seam. Do not use real sleeps that make tests flaky.
  - [x] Add tests for superseded search requests being cancelled/ignored.
  - [x] Add tests that Previous/Next preserve search/type/active/page size.
  - [x] Add tests for degraded/stale metadata preserving last-known rows and rendering a polite freshness/status region.
  - [x] Add tests for `"No parties match."` plus clear-filters reset.
  - [x] Add tests for keyboard row navigation/Enter and type-ahead focus where bUnit can observe it.

- [x] **Task 9 - Update API/client tests for filtered search** (`tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`, affected client tests) (AC1, AC2, AC3)
  - [x] Add tests proving `AdminPortalSearchRequest.Type` and `.Active` are not dropped.
  - [x] Add tests proving `SearchPartiesAsync` uses the display-name lexical allowlist mode.
  - [x] Add/update typed client tests if `IPartiesQueryClient.SearchPartiesAsync` gains additive optional parameters.
  - [x] Preserve existing failure mapping, bounded paging, contract-unavailable, and PII-safe problem-detail behavior.

- [x] **Task 10 - Run focused verification** (AC1-AC8)
  - [x] `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1`
  - [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1`
  - [x] Direct xUnit v3 executable run for `Hexalith.Parties.AdminPortal.Tests` if `dotnet test --filter` returns zero tests under MTP.
  - [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1` if shared UI primitives or references move.
  - [x] `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1` if shared UI primitives move.
  - [x] `bash scripts/check-no-warning-override.sh`

## Dev Notes

### Source Discovery Summary

- Loaded `epics_content` from `_bmad-output/planning-artifacts/epics.md`; Story 2.2 is FR-Admin-1 under Epic 2 and requires debounced server-driven display-name search, Person/Organization and active filters, paging, stale/degraded last-known rendering, recoverable empty state, erased handling, and keyboard row activation.
- Loaded `architecture_content` from `_bmad-output/planning-artifacts/architecture.md`; AdminPortal is an RCL embedded by the UI host, routes live under `/admin/parties*`, browser traffic stays inside the Blazor Server UI host, and backend calls go through the typed Parties client/EventStore gateway.
- No formal PRD exists in `_bmad-output/planning-artifacts`; epics, architecture, UX, readiness reports, and brownfield docs are the source of truth.
- Loaded UX files under `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`; relevant constraints are Admin comfortable density, `FluentDataGrid`, `FluentSelect`, freshness indicator, stale last-known rendering, "No parties match." empty copy, keyboard parity, and no color-only state.
- Loaded `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md`; it warns that Epic 2/3 Admin stories must reuse `PartiesAdminPortal.razor` rather than rebuild list/detail/GDPR pages.
- Loaded persistent project context from `_bmad-output/project-context.md` and sibling submodule contexts.
- Reviewed previous story `_bmad-output/implementation-artifacts/2-1-embed-the-admin-area-behind-the-admin-policy.md`.
- Reviewed current source and tests in `src/Hexalith.Parties.AdminPortal`, `src/Hexalith.Parties.Client`, `src/Hexalith.Parties.Contracts`, `src/Hexalith.Parties.UI/Components/Shared`, `tests/Hexalith.Parties.AdminPortal.Tests`, and relevant UI shared-component tests.
- Reviewed recent git history: `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`, then Epic 1 foundation commits.

### Existing Code to Reuse

- Reuse `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` as the browse component for `/admin/parties`.
- Reuse `IPartiesAdminPortalApiClient`, `PartiesAdminPortalApiClient`, `AdminPortalListRequest`, `AdminPortalSearchRequest`, `AdminPortalQueryMetadata`, `AdminPortalQueryResult<T>`, `AdminPortalQueryFailureKind`, `AdminPortalQueryException`, `AdminPortalQueryBounds`, and `PartiesAdminListCoordinator`.
- Reuse existing list/detail cancellation/versioning: `_listVersion`, `_detailVersion`, `_listCts`, `_detailCts`, `_lifetimeCts`, `ApplyAuthorizationContextAsync`, `ResetVisibleState`, and `QueryService.ResetForTenantSwitch()`.
- Reuse existing safe route-id behavior and `PartiesAdminPortalManifest.DetailRoute`.
- Reuse `RecordingAdminPortalApiClient` and current component tests as the test seam for list/search/detail behavior.
- Reuse `DataFreshnessIndicator`, `PartyStateBadge`, and `StatusLiveRegion` semantics, but do not introduce an AdminPortal -> UI project reference.

### Current Files Being Modified

| File | Current state | Story change | Preserve |
|---|---|---|---|
| `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` | Single routable AdminPortal component; search only runs on button/Enter; filters are disabled when query is non-empty; `CreateSearchRequest()` forces type/active to null; rows are cleared before each load; inline badge/status text; no explicit skeleton; row activation is button click. | Add debounced input search, combined search+type/active filter behavior, stale last-known rendering, empty copy/action, shared badge/freshness semantics, keyboard row activation/type-ahead focus. | Route attributes, `Admin` policy, internal auth checks, tenant-switch cancellation/versioning, safe route ids, GDPR panel composition, PII-safe erased behavior. |
| `src/Hexalith.Parties.AdminPortal/Services/AdminPortalSearchRequest.cs` | Carries `Query`, page, pageSize, `Type`, `Active`. | Ensure type/active are meaningful and forwarded. Add mode only if needed to pin `DisplayName`/`Lexical`. | Record immutability and contract simplicity. |
| `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` | Typed `SearchPartiesAsync` path drops type/active and mode; FrontComposer fallback uses `SearchQuery` without `ColumnFilters`; list path supports type/active/date filters. | Implement filtered display-name search semantics or extend downstream client contract additively. Preserve fail-closed search allowlist. | Error mapping, contract-unavailable behavior, PII-safe messages, bounded paging, `ConfigureAwait(false)`. |
| `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` | `SearchPartiesAsync` accepts query/page/pageSize/cancellation/mode/caseId/customizer, no type/active filters. | Add optional type/active filter parameters if needed to support server-side filtered search. | Existing callers remain source-compatible through optional parameters or overloads. |
| `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs` | Search payload sends query/page/pageSize/mode/caseId only. | Include type/active in search payload if the server query contract supports or is extended to support it. | EventStore gateway path, party domain, projection actor type, typed error handling. |
| `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` | Labels include `NoMatches = "No parties match the current filters"` and generic status strings. | Add/adjust label for `"No parties match."`, clear-filters guidance, skeleton/freshness text as needed. | Localization seam; no inline user-facing strings in component when avoidable. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` | Tests current browse UI, authorization, search, date filters, degraded metadata, tenant-switch safety, erased behavior; currently asserts filter selects disabled in search mode and type/active not forwarded. | Update old assertions to new FR-Admin-1 behavior; add debounce, combined filters, paging preservation, stale last-known, empty clear action, keyboard tests. | bUnit style, Shouldly, no raw `Assert.*`, no live backend. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` | Tests typed-client preference, list filters, search fallback without filters, paging, failures. | Add/adjust tests for filtered display-name search and allowlist mode. | Existing failure mapping and contract-unavailable tests. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` | Captures list/search/detail/GDPR requests. | Extend only as needed for debounce/filter/paging assertions. | Request capture should not silently re-bound/coerce values. |
| `tests/Hexalith.Parties.UI.Tests/*` | Owns shared primitive tests for `DataFreshnessIndicator`, `PartyStateBadge`, `StatusLiveRegion`. | Update only if primitives move to a shared host-agnostic location. | Existing semantic and politeness guarantees. |

Do not modify EventStore/Tenants submodules, Parties domain command/event contracts, GDPR panels beyond list/detail integration needs, deployment manifests, OIDC/sign-in flows, or Consumer self-scope code for this story.

### Technical Requirements

- Use the existing AdminPortal RCL and route component. Net-new page components for list/detail/GDPR are a regression unless a later refactor story explicitly owns the split.
- Search must be server-driven. Do not satisfy filters by fetching a page and filtering it client-side.
- Search is display-name lexical only. The existing rich search capability probe may keep email/identifier buttons disabled or capability-labeled; it must not expand FR-Admin-1 scope.
- Combined search+type/active filters must preserve server paging/count semantics.
- Use cancellation/versioning to ignore stale responses after tenant switch, criteria change, page change, or component disposal.
- Preserve the typed client as the preferred boundary when `IPartiesQueryClient` is registered. The FrontComposer `IQueryService` fallback remains a degraded/lazy backend path.
- Keep all user-facing copy localized through `AdminPortalLabels` or an equivalent resource seam.
- Keep PII out of route-denial, empty, failure, tombstone, log, and test failure text. Display names can appear in authorized rows/detail only.
- Do not add package `Version=` attributes; versions stay in `Directory.Packages.props`.

### Architecture Compliance

- **AR-D4 composition:** AdminPortal remains an RCL embedded by `Hexalith.Parties.UI`.
- **AR-D5 transport:** browser talks to the Blazor Server UI host only; AdminPortal calls go through typed Parties clients/EventStore gateway.
- **AR-D6 live freshness:** this story should preserve and display projection freshness/degraded state; no bespoke per-screen polling is required here.
- **AR-StatusMap and UX-DR8:** freshness/status announcements are polite; validation/failure paths are assertive. If AdminPortal keeps its private status enum, map it deliberately to shared semantics and prevent drift with tests.
- **NFR2:** stale/degraded reads render last-known rows, never blank/throw.
- **NFR3:** no PII in forbidden/tombstone/failure copy and no tenant-scoped id in browser route.
- **NFR9:** .NET 10, `.slnx`, Central Package Management, `TreatWarningsAsErrors`, xUnit v3, Shouldly, bUnit.

### Previous Story Intelligence

- Story 2.1 embedded AdminPortal behind the Admin policy and is done at baseline commit `ec2676b`.
- Story 2.1 added/validated both host route discovery and route-level Admin authorization. Do not weaken `[Authorize(Policy = "Admin")]`, `AuthorizeRouteView`, or Admin-only nav.
- Story 2.1 review fixed lazy backend configuration so AdminPortal composes without a typed backend until data is requested. Preserve that degraded/test-host startup behavior.
- Story 2.1 kept AdminPortal host-agnostic. Do not make `Hexalith.Parties.AdminPortal` reference `Hexalith.Parties.UI` just to use shared UI primitives.
- Epic 1 stories established shared `StatusKind`, `StatusLiveRegion`, `DataFreshnessIndicator`, `PartyStateBadge`, SignalR freshness patterns, accessibility gates, and deploy/build constraints.

### Git Intelligence Summary

- Recent commits:
  - `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`
  - `78a5956 docs(epic-1): Add retrospective and sync project docs`
  - `ac523f0 feat(story-1.10): Deploy parties-ui container/K8s with production KMS gate`
  - `43053b3 feat(story-1.9): Accessibility foundation and CI a11y gate (WCAG 2.2 AA)`
  - `9610f70 feat(story-1.8): Shared domain components (party-state badge, freshness indicator, GDPR destructive button)`
- Pattern: narrow changes, static/bUnit regression tests, direct xUnit v3 executable fallback when MTP filtering is unreliable, no package drift, no broad refactors.

### Latest Technical Information

- ASP.NET Core Blazor event handling guidance for .NET 10 confirms async event handlers should return `Task`/`ValueTask`, not `async void`; event handlers automatically trigger UI renders. Apply this to debounced search handlers. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/event-handling?view=aspnetcore-10.0]
- ASP.NET Core Blazor data-binding guidance for .NET 10 confirms default binding uses `onchange`, `oninput` updates as the text box changes, and `@bind:after` can invoke async logic after the bound value is assigned. Use this to implement type-as-you-search cleanly with debounce. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/data-binding?view=aspnetcore-10.0]
- ASP.NET Core Blazor routing guidance for .NET 10 confirms additional routable component-library assemblies require `Router.AdditionalAssemblies` and `AddAdditionalAssemblies(...)`; Story 2.1 already wired this and Story 2.2 must preserve it. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-10.0]
- ASP.NET Core Blazor authorization guidance confirms `AuthorizeView` controls UI visibility but does not enforce handler security by itself. Keep route-level authorization and internal AdminPortal authorization checks. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0]

### File Structure Requirements

| Action | File |
|---|---|
| UPDATE | `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` |
| UPDATE | `src/Hexalith.Parties.AdminPortal/Services/AdminPortalSearchRequest.cs` |
| UPDATE | `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` |
| UPDATE | `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` |
| UPDATE IF NEEDED | `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` |
| UPDATE IF NEEDED | `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs` |
| UPDATE IF NEEDED | shared primitive location/project references if AdminPortal needs host-agnostic `PartyStateBadge`/`DataFreshnessIndicator` reuse |
| UPDATE | `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` |
| UPDATE | `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` |
| UPDATE | `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` |
| UPDATE IF NEEDED | `tests/Hexalith.Parties.UI.Tests/*` or moved shared-primitive tests |

### Testing Requirements

- Component tests must fail if typing in search does not trigger a debounced server request.
- Component/API tests must fail if type/active filters are disabled or dropped while search is non-empty.
- Tests must fail if paging loses current search/type/active criteria.
- Tests must fail if degraded/stale list metadata blanks previously confirmed rows.
- Tests must fail if empty search lacks a clear-filters path.
- Tests must fail if non-Admin/missing-tenant/unauthenticated states call list/search/detail.
- Tests must fail if erased detail renders personal fields or if browser routes include tenant-scoped identifiers.
- Use xUnit v3, Shouldly, NSubstitute where mocking is needed, and bUnit for components. Do not add Moq, FluentAssertions, raw `Assert.*`, or real-time sleeps.

### Project Structure Notes

- Alignment: `Hexalith.Parties.AdminPortal` owns Admin list/detail/GDPR browse UI as an RCL.
- Alignment: `Hexalith.Parties.UI` owns host shell, route discovery, auth policy registration, and shared host-owned primitives today.
- Detected conflict: architecture tree mentions new `PartiesListPage.razor`, `PartyDetailPage.razor`, and `PartyGdprPage.razor`, but readiness analysis and live code show the existing `PartiesAdminPortal.razor` already serves those routes. Follow the readiness correction: reuse and enhance the existing component.
- Detected gap: shared `DataFreshnessIndicator` and `PartyStateBadge` currently live in the UI host. AdminPortal must not reverse-reference the host, so either compose without crossing boundaries or move shared primitives into a neutral location intentionally.
- Detected gap: current typed `SearchPartiesAsync` cannot carry type/active filters. This must be fixed or equivalently supported before AC2 can pass.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.2: Parties list with search, filters, and paging (FR-Admin-1)`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements Overview`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Communication Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Interaction Primitives`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Components`]
- [Source: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md#Findings (reuse / boilerplate lens)`]
- [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]
- [Source: `_bmad-output/implementation-artifacts/2-1-embed-the-admin-area-behind-the-admin-policy.md#Previous Story Intelligence`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/AdminPortalSearchRequest.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`]
- [Source: `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`]
- [Source: `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Models/PagedResult.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs`]
- [Source: `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor`]
- [Source: `src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor`]
- [Source: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`]
- [Source: `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`]
- [Source: Microsoft Learn, ASP.NET Core Blazor event handling, .NET 10]
- [Source: Microsoft Learn, ASP.NET Core Blazor data binding, .NET 10]
- [Source: Microsoft Learn, ASP.NET Core Blazor routing, .NET 10]
- [Source: Microsoft Learn, ASP.NET Core Blazor authentication and authorization, .NET 10]

## Dev Agent Record

## Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

Outcome: Approved after auto-fixes. No critical issues remain.

### Findings Fixed

- [HIGH] Filter-only empty results rendered `No parties` instead of the required recoverable `"No parties match."` empty guidance. Fixed `EmptyText` to use match-empty copy whenever any search/filter criterion is active and added `PartiesAdminPortal_FilterOnlyEmptyResult_UsesRecoverableNoMatchesCopy`.
- [HIGH] The Test-environment AdminPortal E2E authorization service returned authenticated Admin state without checking the fixture cookie, weakening the component's internal no-data-call guard when the fixture is enabled. Fixed it to share the fixture cookie check with the authentication provider and added `PartiesAdminPortalE2eFixtureTests`.
- [MEDIUM] Git reality and the story File List diverged: `src/Hexalith.Parties.UI/Program.cs`, `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`, `tests/e2e/playwright.config.ts`, and `tests/e2e/specs/admin-parties-list.spec.ts` were changed but not recorded. Updated the File List.

### Validation Notes

- MCP/doc search was not performed during review; the story already captured current official Microsoft Learn references for the Blazor event, binding, routing, and authorization behavior used here.
- `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` was attempted but blocked by this sandbox's Kestrel socket-bind restriction: `System.Net.Sockets.SocketException (13): Permission denied`.

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1` blocked by offline NuGet audit (`NU1900`, `api.nuget.org` unavailable).
- `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-build -p:NuGetAudit=false` blocked by sandbox named-pipe bind permission.
- `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` passed: 118 total, 0 failed.
- `dotnet build tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- `NuGetAudit=false DiffEngine_Disabled=true tests/Hexalith.Parties.Client.Tests/bin/Release/net10.0/Hexalith.Parties.Client.Tests -class Hexalith.Parties.Client.Tests.HttpPartiesQueryClientTests -class Hexalith.Parties.Client.Tests.AdminPortal.AdminPortalQueryContractTests` passed: 31 total, 0 failed.
- Full `Hexalith.Parties.Client.Tests` direct run was attempted; package tests are blocked by their child `dotnet pack` artifact path in this sandbox after audit is disabled.
- `dotnet build tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- `dotnet build tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- `bash scripts/check-no-warning-override.sh` passed.
- Review: `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- Review: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- Review: `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- Review: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- Review: `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` passed: 119 total, 0 failed.
- Review: `DiffEngine_Disabled=true tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` passed: 258 total, 0 failed.
- Review: `NuGetAudit=false DiffEngine_Disabled=true tests/Hexalith.Parties.Client.Tests/bin/Release/net10.0/Hexalith.Parties.Client.Tests -class Hexalith.Parties.Client.Tests.HttpPartiesQueryClientTests -class Hexalith.Parties.Client.Tests.AdminPortal.AdminPortalQueryContractTests` passed: 31 total, 0 failed.
- Review: `dotnet build tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- Review: `cd tests/e2e && npm run typecheck` passed.
- Review: `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` blocked by sandbox Kestrel socket bind permission.
- Review: `bash scripts/check-no-warning-override.sh` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Implemented deterministic debounced display-name search with cancellable/versioned request handling and immediate Search button submission.
- Forwarded type/active filters through AdminPortal search requests, FrontComposer fallback column filters, and the additive typed `IPartiesQueryClient.SearchPartiesAsync` contract using lexical display-name mode.
- Preserved search criteria across paging, retained last-known rows for stale/degraded reads, added accessible cold-load and recoverable empty-state affordances, and kept erased/route privacy behavior intact.
- Added keyboard row navigation/Enter activation and type-ahead search focus handling without exposing tenant-scoped identifiers in routes.
- Updated AdminPortal, API, HTTP client, Picker fake, and regression tests for the new FR-Admin-1 contract.
- Review fixed filter-only empty-state copy and the Test-only AdminPortal E2E authorization guard, then added regression tests.

### File List

- `_bmad-output/implementation-artifacts/2-2-parties-list-with-search-filters-and-paging-fr-admin-1.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/story-automator/orchestration-1-20260609-205725.md`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesAdminPortalE2eFixtureTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Fakes/RecordingPartiesQueryClient.cs`
- `tests/e2e/playwright.config.ts`
- `tests/e2e/specs/admin-parties-list.spec.ts`

### Change Log

- 2026-06-10: Implemented FR-Admin-1 debounced search, filtered server search, paging preservation, stale/empty UI behavior, and keyboard activation.
- 2026-06-10: Review auto-fixes applied for filter-only empty-state copy, Test-only E2E authorization guard, review tests, and File List reconciliation.
