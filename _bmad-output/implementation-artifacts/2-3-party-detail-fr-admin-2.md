---
baseline_commit: da6bfcf6dd3a4098f7b5ea78decb8cf0cd3e0b5a
---

# Story 2.3: Party detail (FR-Admin-2)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an admin,
I want to view a party's full detail,
so that I can review the record and decide what to do.

This is a reuse-and-enhance story for the existing `Hexalith.Parties.AdminPortal` master-detail surface. Do not build a parallel `PartyDetailPage.razor` or split the Admin route unless the split is explicitly justified and preserves the existing component behavior. `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` already owns `/admin/parties`, `/admin/parties/{RoutePartyId}`, `/admin/parties/{RoutePartyId}/gdpr`, route-level `Admin` authorization, safe route-id handling, detail loading, GDPR panel composition, erased-party privacy behavior, and tenant-switch cancellation. Enhance that implementation so FR-Admin-2 is complete, especially the phone master-detail reflow and focus contract.

## Acceptance Criteria

1. **AC1 - Full detail renders through the existing route component.** Given an Admin opens `/admin/parties/{id}` from the list, keyboard, or direct route, when `GetPartyAsync(id)` succeeds, then `PartiesAdminPortal.razor` renders the full `PartyDetail` without creating a second Admin detail page. The detail includes summary, type, lifecycle state, created/modified dates, person or organization sections, contact channels, identifiers, consent records, restriction/erasure fields, name history, EventStore admin links, and the existing GDPR operations entry point.

2. **AC2 - Party state uses color plus text and does not drift from shared semantics.** Given a detail payload is active, inactive, restricted, or erased, when the state renders, then it uses the Story 1.8 party-state badge semantics: color plus visible text label, no color-only meaning, Fluent status token pairs, and erased tombstone copy instead of personal data. Do not leave the current raw detail status text as the only state treatment if the shared primitive is available. Do not make `Hexalith.Parties.AdminPortal` reference `Hexalith.Parties.UI` directly just to reach `PartyStateBadge`; promote/share the primitive through a host-agnostic location or implement an AdminPortal-local equivalent with tests preventing semantic drift.

3. **AC3 - Freshness indicator replaces inline stale-age text.** Given detail metadata is current, stale, degraded, local-only, or carries `StaleDataAge`, when the detail renders, then the read state is shown through the Story 1.8 freshness semantics: dot plus word, `role="status" aria-live="polite"`, stale/degraded wording that says the UI is showing last-known data, and no raw backend metadata dump. Existing list-level degraded behavior from Story 2.2 remains intact.

4. **AC4 - Partial projection is honest.** Given the detail path has only display-name/partial projection data available, when the detail renders, then it shows the display name and marks the remaining sections as still loading or unavailable. It must not imply the party has no contacts, identifiers, consents, restrictions, or history merely because projection enrichment has not arrived.

5. **AC5 - Erased, gone, missing, and forbidden states remain privacy-preserving.** Given the detail read returns an erased payload, `Gone`, `NotFound`, `Forbidden`, malformed payload, or contract-unavailable failure, when the detail region updates, then no personal fields, previous detail fields, scoped tenant ids, raw exception strings, parser details, or backend problem details are rendered. The gone/erased state uses tombstone meaning equivalent to `"This party was erased."`, removes the row where existing behavior does so, and resets GDPR state through `AdminPortalGdprStateCoordinator`.

6. **AC6 - Phone master-detail reflows into a sheet/full-screen detail.** Given a phone-width viewport, when an Admin opens a row, presses Enter on a focused row, or lands directly on `/admin/parties/{id}`, then the desktop two-pane master-detail collapses to a sheet or full-screen detail. The detail has an accessible heading, a Back/Close control, a single-column layout at 320px width and 200% zoom, no horizontal content loss, no overlap, and no reliance on color alone to mark the active row.

7. **AC7 - Focus contract is explicit and test-covered.** Given the phone detail opens from a list row, when the sheet/full-screen detail appears, then focus moves into the detail surface. When Back/Close returns to the list, focus returns to the originating row control. Given the detail is opened by a direct route and no originating row exists, focus moves to the detail heading or first safe control without throwing. Routine stale/freshness updates announce politely and do not steal focus.

8. **AC8 - Existing Admin safeguards are preserved.** Given a Consumer, non-Admin, unauthenticated user, or missing tenant context reaches `/admin/parties/{id}`, when authorization is evaluated, then no list/search/detail/GDPR data call occurs and no party data renders. Preserve `[Authorize(Policy = "Admin")]`, `AuthorizeRouteView`, `IAdminPortalAuthorizationService`, `_authContextClaim`, `_listVersion`, `_detailVersion`, `_listCts`, `_detailCts`, `_lifetimeCts`, safe route-id validation, and tenant-switch data clearing.

9. **AC9 - Edit and GDPR entries route safely.** Given a full detail is visible, when the Admin activates Edit or GDPR, then GDPR continues to route to `/admin/parties/{id}/gdpr` and compose the existing `PartyGdprOperationsPanel`. The Edit entry may route to the future Story 2.4 path if it exists or render a disabled/unavailable affordance with localized copy, but it must not create an incomplete edit implementation in this story.

10. **AC10 - Responsive and accessibility verification is automated.** Given the story is complete, when tests run, then bUnit covers detail states, privacy failures, shared badge/freshness semantics, and focus intent where observable; Playwright covers desktop detail, phone detail sheet/full-screen behavior, Back/Close focus restore, 320px viewport, and 200% zoom or equivalent viewport emulation. If this sandbox blocks Kestrel/Playwright, record that explicitly in the Dev Agent Record.

## Tasks / Subtasks

### Part A - Reuse the existing detail surface - AC1, AC8, AC9

- [x] **Task 1 - Keep FR-Admin-2 in `PartiesAdminPortal.razor`** (AC1, AC8)
  - [x] Reuse `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`; do not add `PartyDetailPage.razor`.
  - [x] Preserve the existing `@page` routes, `[Authorize(Policy = "Admin")]`, `RoutePartyId`, `ApplyRoutePartySelectionAsync`, and `SelectPartyAsync` flow.
  - [x] Preserve `NormalizeRoutePartyId`, `IsSafeRoutePartyId`, and `NavigateToPartyDetailRoute`; unsafe ids must never fetch detail or echo to rendered text.
  - [x] Preserve route variant `/admin/parties/{RoutePartyId}/gdpr` and current GDPR panel composition.

- [x] **Task 2 - Render the complete detail contract with safe entries** (AC1, AC9)
  - [x] Keep all existing detail sections: summary, person details, organization details, contact channels, identifiers, consent records, restrictions, EventStore links, system metadata, and name history.
  - [x] Add a visible Edit entry that routes only to the Story 2.4 edit path if that route exists; otherwise use a disabled/localized unavailable affordance.
  - [x] Keep the GDPR entry wired through `PartyGdprOperationsPanel`, `AdminPortalGdprStateCoordinator`, and `RefreshSelectedPartyAsync`.
  - [x] Do not introduce Create/Edit form behavior in this story.

### Part B - Replace inline state/freshness presentation - AC2, AC3, AC4

- [x] **Task 3 - Use shared or drift-proof party-state badge semantics** (AC2)
  - [x] Replace or wrap the current raw `DetailStatus(_detail)` display with a badge treatment equivalent to `PartyStateBadge`.
  - [x] Do not add a project reference from `Hexalith.Parties.AdminPortal` to `Hexalith.Parties.UI`.
  - [x] If primitives are promoted, move `PartyLifecycleState`, `PartyStateBadge`, and `DataFreshnessIndicator` to a host-agnostic shared UI project or RCL and update `Hexalith.Parties.UI` references/tests.
  - [x] If primitives stay duplicated for now, add tests proving AdminPortal state labels and token classes remain aligned with the shared component semantics.
  - [x] Remove or demote inline `.hx-parties-admin__badge`/raw detail state where it would create two divergent state systems.

- [x] **Task 4 - Render detail freshness through the freshness indicator semantics** (AC3)
  - [x] Map `AdminPortalQueryMetadata` and/or `ProjectionFreshnessMetadata` from detail responses into current/stale/degraded UI state.
  - [x] Replace the current inline `Data age: PT...` detail text with user-facing freshness copy.
  - [x] Keep the live region polite (`role=status`, `aria-live=polite`).
  - [x] Do not render raw backend age strings, exception text, tenant ids, or problem details.

- [x] **Task 5 - Handle partial/display-name-only detail honestly** (AC4)
  - [x] Identify how the current client/API represents display-name-only detail metadata or partial projection detail.
  - [x] Render the display name plus "still loading" or localized equivalent for sections that are not yet enriched.
  - [x] Avoid rendering empty section copy that implies no contact channels/identifiers/consents/history exist when the data is simply unavailable.
  - [x] Keep stale/degraded last-known detail visible when available; do not blank the panel for a recoverable refresh state.

### Part C - Phone reflow and focus management - AC6, AC7

- [x] **Task 6 - Add responsive sheet/full-screen behavior** (AC6)
  - [x] Add component-scoped CSS or an existing stylesheet hook for `.hx-parties-admin__layout`, `.hx-parties-admin__list`, and `.hx-parties-admin__detail`.
  - [x] Desktop stays two-pane at `>=1024px`.
  - [x] Tablet/phone collapses detail to an overlay sheet or full-screen panel when a party is selected or a route id is present.
  - [x] At `320px` width and 200% zoom, detail content is single column, headings/actions do not overlap, and long values wrap without horizontal scroll except where an inner data element legitimately needs it.
  - [x] Mark the active list row with a non-color cue such as `aria-current`, selected text, border weight, or icon/checkmark.

- [x] **Task 7 - Implement deterministic focus movement and restore** (AC7)
  - [x] Track the originating row/control when detail opens from pointer or keyboard.
  - [x] On phone detail open, focus the detail heading or Back/Close control after render.
  - [x] On Back/Close, navigate back to `/admin/parties` if needed, close the detail sheet, and restore focus to the originating row when still present.
  - [x] If the detail was opened from a direct route or the row disappeared, focus the list region or search input as the safe fallback.
  - [x] Do not move focus for routine freshness/status updates.

### Part D - Privacy and regression tests - AC5, AC8, AC10

- [x] **Task 8 - Extend bUnit component coverage** (AC1-AC5, AC7, AC8)
  - [x] Add/adjust tests in `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`.
  - [x] Assert full detail sections still render from `PartiesAdminPortal.razor`.
  - [x] Assert active/inactive/restricted/erased state uses non-color-only badge semantics.
  - [x] Assert stale/degraded detail metadata uses the freshness indicator live region.
  - [x] Assert display-name-only/partial detail does not render false empty sections.
  - [x] Preserve existing erased/gone/malformed/forbidden tests and add regressions for no previous detail leakage.
  - [x] Assert non-Admin/missing tenant/unauthenticated route states still make no detail/GDPR calls.

- [x] **Task 9 - Add or extend Playwright Admin detail coverage** (AC6, AC7, AC10)
  - [x] Extend `tests/e2e/specs/admin-parties-list.spec.ts` or add `admin-party-detail.spec.ts`.
  - [x] Cover desktop row-to-detail and `/admin/parties/{id}` direct route.
  - [x] Cover phone viewport (`320px` width) and a 200% zoom equivalent through viewport/device-scale/emulation strategy.
  - [x] Assert detail sheet/full-screen is visible, list/detail do not overlap incoherently, Back/Close returns to the list, and focus returns to the originating row.
  - [x] Keep the existing Test-environment Admin fixture cookie guard intact.

- [x] **Task 10 - Run focused verification** (AC1-AC10)
  - [x] `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false` if shared primitives or host styles move.
  - [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` if shared primitives move.
  - [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests`
  - [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` if shared primitives move.
  - [x] `cd tests/e2e && npm run typecheck`
  - [x] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` or the new detail spec.
  - [x] `bash scripts/check-no-warning-override.sh`

## Dev Notes

### Source Discovery Summary

- Loaded `epics_content` from `_bmad-output/planning-artifacts/epics.md`; Story 2.3 is FR-Admin-2 and requires full `PartyDetail`, party-state badge, freshness indicator, Edit/GDPR entries, partial projection honesty, erased/missing tombstone privacy, and phone master-detail reflow.
- Loaded `architecture_content` from `_bmad-output/planning-artifacts/architecture.md`; Admin routes are `/admin/parties`, `/admin/parties/{id}`, and `/admin/parties/{id}/gdpr`; browser traffic stays in the Blazor Server UI host; backend reads go through the typed Parties client/EventStore gateway; stale/degraded reads render last-known content.
- No formal PRD exists in `_bmad-output/planning-artifacts`; epics, architecture, UX, readiness reports, and brownfield docs are the source of truth.
- Loaded UX files under `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`; relevant constraints are Admin desktop master-detail, phone sheet/full-screen reflow, focus moves into the sheet and restores to the row, state is not color-only, freshness is polite, and 320px/200% zoom must not lose content.
- Loaded `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md`; it explicitly warns that Story 2.3 must reuse and enhance `PartiesAdminPortal.razor` rather than rebuild detail pages.
- Loaded persistent project context from `_bmad-output/project-context.md` and reference submodule contexts.
- Reviewed previous story `_bmad-output/implementation-artifacts/2-2-parties-list-with-search-filters-and-paging-fr-admin-1.md`; Story 2.2 is done and established debounced search, filtered search, paging preservation, stale list rows, keyboard grid activation, Admin fixture E2E coverage, and direct xUnit executable fallback.
- Reviewed current source and tests in `src/Hexalith.Parties.AdminPortal`, `src/Hexalith.Parties.UI/Components/Shared`, `tests/Hexalith.Parties.AdminPortal.Tests`, and `tests/e2e`.
- Reviewed recent git history: `da6bfcf feat(story-2.2): Parties list with search, filters, and paging`, `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`, then Epic 1 foundation commits.

### Existing Code to Reuse

- Reuse `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` as the single Admin browse/detail/GDPR component.
- Reuse `AdminPortalPartyQueryService`, `IPartiesAdminPortalApiClient`, `PartiesAdminPortalApiClient`, `AdminPortalQueryMetadata`, `AdminPortalQueryResult<T>`, `AdminPortalQueryFailureKind`, `AdminPortalQueryException`, `PartiesAdminListCoordinator`, `AdminPortalGdprStateCoordinator`, and `AdminPortalEventStoreAdminLinks`.
- Reuse existing detail cancellation/versioning: `_detailVersion`, `_detailCts`, `_lifetimeCts`, `SwapDetailCts`, `ApplyRoutePartySelectionAsync`, `SelectPartyAsync`, `RefreshSelectedPartyAsync`, `ResetVisibleState`, and `SafeStateHasChangedAsync`.
- Reuse existing safe route-id behavior and `PartiesAdminPortalManifest.DetailRoute`; do not route scoped ids containing `:` into the browser URL.
- Reuse the existing GDPR panels under `src/Hexalith.Parties.AdminPortal/Components/*Panel.razor`; do not rebuild GDPR UI for this story.
- Reuse the Story 1.8 shared component semantics from `src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor`, `DataFreshnessIndicator.razor`, and `PartyLifecycleState.cs`, but respect the boundary that AdminPortal must not reference the UI host.
- Reuse the Playwright Admin fixture introduced in Story 2.2: `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`, `tests/e2e/playwright.config.ts`, and `tests/e2e/specs/admin-parties-list.spec.ts`.

### Current Files Being Modified

| File | Current state | Story change | Preserve |
|---|---|---|---|
| `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` | Single routable AdminPortal component; renders list and full detail; inline row badge span; detail status is text via `DetailStatus`; stale detail shows raw `Data age: PT...`; no explicit responsive sheet/back/focus-restore contract; safe route and cancellation logic already exist. | Add phone sheet/full-screen detail behavior, focus enter/restore, detail Back/Close, shared or drift-proof badge/freshness presentation, partial projection handling, and Edit entry affordance. | Routes, Admin policy, internal auth checks, safe route ids, tenant-switch cancellation/versioning, GDPR panel composition, erased/gone privacy behavior, no scoped id display. |
| `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` | Centralized Admin copy; has detail, status, GDPR, and empty strings. | Add localized strings for detail Back/Close, Edit unavailable or Edit route, still-loading partial projection copy, freshness copy if not supplied by shared component. | Keep user-facing copy out of component markup where practical; no PII or raw backend wording. |
| `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor.css` or equivalent stylesheet | No AdminPortal component stylesheet currently exists. | Add responsive master-detail/sheet/full-screen styling, focus-visible support, active-row non-color cue, wrapping rules for 320px/200% zoom. | Use Fluent/FrontComposer tokens; no hard-coded state colors; support forced-colors/reduced-motion if animations/transitions are added. |
| `src/Hexalith.Parties.UI/Components/Shared/*` | Shared Story 1.8 primitives live in the UI host today. | Update only if primitives are promoted into a host-agnostic location, or if tests need to prove semantics remain aligned. | Do not break existing UI tests or make AdminPortal depend on UI host. |
| `src/Hexalith.Parties.UI/Program.cs` and `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` | Test-only AdminPortal E2E fixture supports Story 2.2 list tests with cookie guard. | Update only if detail E2E fixture data/routes need richer detail states, phone detail scenario, or edit/GDPR route assertions. | Preserve fixture disabled-by-default behavior and cookie guard. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` | Large bUnit suite already covers browse layout, route detail, unsafe ids, stale detail age, state text, erased payload privacy, GDPR panels, tenant switch cancellation, failure privacy, and keyboard row activation. | Add/adjust tests for badge/freshness semantics, partial projection, phone detail state markers/focus intent where bUnit can observe, Edit/GDPR entries, and no-data-call guard on detail routes. | Existing privacy and cancellation tests must remain green. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` | Captures detail requests and supports queued detail successes/failures. | Extend only if tests need partial/degraded detail metadata helpers. | Request capture should not coerce ids or metadata silently. |
| `tests/e2e/specs/admin-parties-list.spec.ts` or new `admin-party-detail.spec.ts` | Story 2.2 covers list search/filter/paging/degraded/keyboard. | Add desktop detail, phone detail sheet/full-screen, focus restore, Back/Close, 320px, and zoom/emulation coverage. | Keep list tests stable and serial fixture reset behavior intact. |
| `tests/e2e/playwright.config.ts` | Single Chromium desktop project with web server for `Hexalith.Parties.UI`. | Add mobile project or per-test viewport/emulation only if needed. | Avoid unnecessary worker/server churn; keep `PLAYWRIGHT_SKIP_WEBSERVER` path working. |

Do not modify EventStore/Tenants submodules, Parties domain command/event contracts, GDPR backend stubs, Consumer self-scope code, deployment manifests, OIDC/sign-in flows, or picker re-skin behavior for this story.

### Technical Requirements

- Use the existing AdminPortal RCL and route component. Net-new `PartyDetailPage.razor` is a regression unless the implementation explicitly replaces the current component without duplicating behavior.
- Detail reads stay server-driven through `GetPartyAsync`; do not call the actor host directly and do not add browser-callable public APIs.
- Preserve the route privacy rule: browser route segment is a safe party id only, never `tenant:domain:aggregateId`.
- Preserve PII hygiene. User-visible failure, tombstone, unauthorized, malformed-payload, and empty/partial copy must not include personal fields, tenant ids, raw problem details, parser text, or stack traces.
- Preserve event-sourced eventual consistency UX: stale/degraded detail shows last-known content where available and announces politely; acceptance is not read-your-write.
- Component event handlers must return `Task`/`ValueTask` rather than `async void`; existing `ConfigureAwait(false)` style in C# blocks should be preserved.
- If using CSS isolation, add `.razor.css` next to the component and rely on the generated RCL scoped bundle; do not import CSS from Razor code blocks.
- Do not add package `Version=` attributes; versions stay in `Directory.Packages.props`.

### Architecture Compliance

- **AR-D4 composition:** AdminPortal remains an RCL embedded by `Hexalith.Parties.UI`.
- **AR-D5 transport:** browser talks to the Blazor Server UI host only; AdminPortal calls go through typed Parties clients/EventStore gateway.
- **AR-D6 live freshness:** this story displays projection freshness/degraded state; no bespoke per-screen polling should be added.
- **AR-StatusMap and UX-DR8:** freshness/status announcements are polite; validation/failure paths are assertive where they represent blocking errors. Do not blanket all status text as polite.
- **NFR2:** stale/degraded reads render last-known detail content when available, never blank/throw.
- **NFR3:** no PII in forbidden/tombstone/failure copy and no tenant-scoped id in browser route.
- **NFR5 / UX-DR10:** Admin desktop master-detail collapses to sheet/full-screen on phone; focus moves into detail and restores to the row on close.
- **NFR7 / UX-DR1-5:** use FluentUI/FrontComposer tokens and state token pairs; no hard-coded status colors.
- **NFR9:** .NET 10, `.slnx`, Central Package Management, `TreatWarningsAsErrors`, xUnit v3, Shouldly, bUnit, and Playwright gates apply.

### Previous Story Intelligence

- Story 2.2 is done at commit `da6bfcf` and enhanced `PartiesAdminPortal.razor` rather than creating new pages. Keep that reuse posture.
- Story 2.2 established deterministic debounce, filtered server search, stable paging, stale last-known list rows, keyboard row activation, and E2E Admin fixture coverage.
- Story 2.2 review fixed the Test-only AdminPortal E2E authorization guard; do not weaken `PartiesAdminPortalE2eFixture` cookie checks.
- Story 2.2 tests use direct xUnit v3 executable fallback because `dotnet test --filter` can be unreliable under MTP in this repo.
- Story 2.2 left `DataFreshnessIndicator` and `PartyStateBadge` in the UI host, and repeated the boundary rule that AdminPortal must not reference `Hexalith.Parties.UI`.
- Epic 1 stories established shared `StatusKind`, `StatusLiveRegion`, `DataFreshnessIndicator`, `PartyStateBadge`, SignalR freshness patterns, accessibility gates, and deploy/build constraints.

### Git Intelligence Summary

- Recent commits:
  - `da6bfcf feat(story-2.2): Parties list with search, filters, and paging`
  - `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`
  - `78a5956 docs(epic-1): Add retrospective and sync project docs`
  - `ac523f0 feat(story-1.10): Deploy parties-ui container/K8s with production KMS gate`
  - `43053b3 feat(story-1.9): Accessibility foundation and CI a11y gate (WCAG 2.2 AA)`
- Pattern: narrow changes, reuse of existing AdminPortal component, bUnit plus Playwright where browser behavior matters, direct xUnit executable fallback, no package drift, no broad refactors.

### Latest Technical Information

- Microsoft Learn's ASP.NET Core Blazor CSS isolation guidance for .NET 10 says component-specific styles use a matching `.razor.css` file next to the component, are scoped at build time, and RCL isolated styles are automatically bundled/imported for consuming apps. Use this for AdminPortal responsive styles instead of global CSS unless the repo has a stronger local pattern. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/css-isolation?view=aspnetcore-10.0]
- Microsoft Learn's ASP.NET Core Razor component guidance for .NET 10 confirms components are reusable UI units with processing logic and require interactivity for event-driven behavior in Blazor Web Apps. Keep `PartiesAdminPortal.razor` interactive and avoid splitting into static markup that cannot own focus/reflow behavior. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/?view=aspnetcore-10.0]
- Playwright emulation documentation confirms tests can configure desktop/mobile device parameters and override viewport per test with `page.setViewportSize`; use this for 320px and phone detail assertions. [Source: https://playwright.dev/docs/emulation]
- Playwright documentation also notes device presets include viewport, user agent, screen size, and touch behavior, while `isMobile` controls mobile viewport behavior. Prefer a named mobile project or explicit test-use block over ad hoc browser resizing when validating the phone sheet contract. [Source: https://playwright.dev/docs/emulation]

### File Structure Requirements

| Action | File |
|---|---|
| UPDATE | `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` |
| ADD/UPDATE | `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor.css` or existing AdminPortal stylesheet location |
| UPDATE | `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` |
| UPDATE IF NEEDED | host-agnostic shared UI primitive location/project references if `PartyStateBadge`/`DataFreshnessIndicator` are promoted |
| UPDATE IF NEEDED | `src/Hexalith.Parties.UI/Components/Shared/*` and corresponding UI tests if shared primitives move |
| UPDATE IF NEEDED | `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` |
| UPDATE | `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` |
| UPDATE IF NEEDED | `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` |
| UPDATE | `tests/e2e/specs/admin-parties-list.spec.ts` or new Admin detail Playwright spec |
| UPDATE IF NEEDED | `tests/e2e/playwright.config.ts` |

### Testing Requirements

- Tests must fail if FR-Admin-2 is implemented in a new parallel detail page while `PartiesAdminPortal.razor` still serves the route.
- Tests must fail if active/inactive/restricted/erased detail state is color-only or diverges from the shared badge semantics.
- Tests must fail if stale/degraded detail metadata renders raw `PT...` age text as the only freshness UI or lacks a polite live region.
- Tests must fail if display-name-only/partial detail implies empty data instead of still loading/unavailable sections.
- Tests must fail if erased/gone/malformed/forbidden detail paths leak personal fields, scoped ids, parser details, raw exception text, or previous detail contents.
- Tests must fail if phone-width detail remains an incoherent side-by-side layout, overlaps the list, lacks Back/Close, or fails focus enter/restore.
- Tests must fail if non-Admin/missing-tenant/unauthenticated detail routes call list/search/detail/GDPR data services.
- Use xUnit v3, Shouldly, NSubstitute where mocking is needed, bUnit for components, and Playwright for actual responsive/focus behavior. Do not add Moq, FluentAssertions, raw `Assert.*`, or real-time sleeps.

### Project Structure Notes

- Alignment: `Hexalith.Parties.AdminPortal` owns Admin list/detail/GDPR browse UI as an RCL.
- Alignment: `Hexalith.Parties.UI` owns host shell, route discovery, auth policy registration, and currently owns shared host-side primitives.
- Detected conflict: `architecture.md` still lists new `PartiesListPage.razor`, `PartyDetailPage.razor`, and `PartyGdprPage.razor`, but readiness analysis and live code show `PartiesAdminPortal.razor` already serves those routes. Follow the readiness correction: reuse and enhance the existing component.
- Detected gap: shared `DataFreshnessIndicator` and `PartyStateBadge` currently live in the UI host. AdminPortal must not reverse-reference the host, so either promote the primitives into a neutral location intentionally or keep AdminPortal-local semantics covered by drift tests.
- Detected gap: responsive AdminPortal CSS appears absent. Story 2.3 should introduce the smallest scoped styling needed for phone detail sheet/full-screen behavior, using Fluent/FrontComposer tokens.
- Detected gap: Playwright Admin coverage currently focuses list behavior at desktop width. Story 2.3 needs responsive detail browser coverage.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.3: Party detail (FR-Admin-2)`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements Overview`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Communication Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Information Architecture`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Responsive & Platform`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Interaction Primitives`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Components`]
- [Source: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md#Findings (reuse / boilerplate lens)`]
- [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]
- [Source: `_bmad-output/implementation-artifacts/2-2-parties-list-with-search-filters-and-paging-fr-admin-1.md#Previous Story Intelligence`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`]
- [Source: `src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor`]
- [Source: `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor`]
- [Source: `src/Hexalith.Parties.UI/Components/Shared/PartyLifecycleState.cs`]
- [Source: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`]
- [Source: `tests/e2e/specs/admin-parties-list.spec.ts`]
- [Source: Microsoft Learn, ASP.NET Core Blazor CSS isolation, .NET 10]
- [Source: Microsoft Learn, ASP.NET Core Razor components, .NET 10]
- [Source: Playwright documentation, Emulation]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- 2026-06-10: `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` passed: 122 tests, 0 failed.
- 2026-06-10: `cd tests/e2e && npm run typecheck` passed.
- 2026-06-10: `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` was attempted but blocked by sandbox socket permissions; Kestrel failed to bind with `System.Net.Sockets.SocketException (13): Permission denied`.
- 2026-06-10: `bash scripts/check-no-warning-override.sh` passed.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane unit` was attempted; the wrapper printed repeated `Build failed with exit code: 1` messages without actionable diagnostics because `dotnet test`/MTP is unreliable in this repo.
- 2026-06-10: `dotnet build Hexalith.Parties.slnx -c Release -m:1 -p:NuGetAudit=false` was attempted; Parties projects and test assemblies built, but the solution failed in `Hexalith.PolymorphicSerializations` package warnings-as-errors (`NU5118`, `NU5128`) outside the story surface.
- 2026-06-10: Direct xUnit executable regression pass: `Hexalith.Parties.Server.Tests` 217/217, `Hexalith.Parties.Projections.Tests` 139/139, `Hexalith.Parties.Security.Tests` 146/146, `Hexalith.Parties.AdminPortal.Tests` 122/122, `Hexalith.Parties.UI.Tests` 258/258, `Hexalith.Parties.Picker.Tests` 162/162, and `Hexalith.Parties.Mcp.Tests` 52/52 passed.
- 2026-06-10: Direct xUnit executable package-fixture checks were attempted for `Hexalith.Parties.Contracts.Tests` and `Hexalith.Parties.Client.Tests`; package tests failed because nested `dotnet pack` restore attempted NuGet vulnerability data from `api.nuget.org:443`, blocked by sandbox network restrictions (`NU1900 Permission denied`).
- 2026-06-10: Review fix verification: `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- 2026-06-10: Review fix verification: `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- 2026-06-10: Review fix verification: `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` passed: 123 tests, 0 failed.
- 2026-06-10: Review fix verification: `cd tests/e2e && npm run typecheck` passed.
- 2026-06-10: Review fix verification: `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium --list` passed: 9 tests discovered.
- 2026-06-10: Review fix verification: `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` remains blocked by sandbox socket permissions; Kestrel failed to bind with `System.Net.Sockets.SocketException (13): Permission denied`.
- 2026-06-10: Review fix verification: `bash scripts/check-no-warning-override.sh` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Enhanced the existing `PartiesAdminPortal.razor` route component; no parallel detail page was added.
- Replaced raw detail state and stale-age rendering with AdminPortal-local badge/freshness semantics aligned to Story 1.8 without adding an AdminPortal -> UI host project reference.
- Added partial/display-name-only handling so unavailable sections show still-loading copy instead of false empty states.
- Added Back-to-list, disabled Edit affordance, active-row non-color cue, mobile sheet/full-screen CSS, detail-heading focus, and row-focus restore.
- Added bUnit coverage for badge/freshness semantics, partial detail honesty, and Back/Edit/focus intent; added Playwright scenarios for desktop/direct/mobile/zoom-equivalent detail behavior.
- Senior review auto-fixed focus and partial-detail honesty regressions found during review.
- Playwright execution remains unverified in this sandbox because the configured Blazor/Kestrel web server cannot bind sockets here.
- Broader regression checks are partially environment-blocked: package-fixture tests require outbound NuGet audit access, and solution build is blocked by pre-existing `Hexalith.PolymorphicSerializations` package warnings-as-errors unrelated to this AdminPortal story.

### Senior Developer Review (AI)

- Finding 1 (High, AC7): `RefreshSelectedPartyAsync` reused the detail-open path and could move focus back to the detail heading during routine detail refreshes. Fixed by adding a `focusDetail` flag and disabling heading focus for refresh-only calls, with a regression source check.
- Finding 2 (High, AC4): partial/display-name-only detail still omitted person/organization and system metadata sections, which could make unavailable enrichment look absent. Fixed by rendering still-loading copy for the type-specific identity section, created/modified fields, and system metadata.
- Finding 3 (Medium, File List): git showed `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` changed but the story File List omitted it. Fixed the File List.
- Finding 4 (Medium, File List): git showed `_bmad-output/implementation-artifacts/tests/test-summary.md` and `_bmad-output/story-automator/orchestration-1-20260609-205725.md` changed but the story File List omitted them. Fixed the File List.
- Outcome: Approved after automatic fixes. No critical issues remain.

### File List

- src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor
- src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor.css
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs
- src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/e2e/specs/admin-parties-list.spec.ts
- _bmad-output/implementation-artifacts/2-3-party-detail-fr-admin-2.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/tests/test-summary.md
- _bmad-output/story-automator/orchestration-1-20260609-205725.md

### Change Log

- 2026-06-10: Created Story 2.3 context artifact and marked it ready for development.
- 2026-06-10: Implemented FR-Admin-2 detail semantics, responsive sheet/focus behavior, partial-detail honesty, and verification coverage; marked story ready for review.
- 2026-06-10: Senior review auto-fixed refresh focus behavior, partial-detail section honesty, and File List discrepancies; marked story done.
