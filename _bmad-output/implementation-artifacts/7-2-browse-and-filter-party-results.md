# Story 7.2: Browse and Filter Party Results

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Parties administrator,
I want to browse, search, and filter party records in the admin console,
so that I can quickly find the party I need to inspect.

## Acceptance Criteria

1. Given an authorized administrator opens `/admin/parties`, when the toolbar renders, then it includes search input, party type filter, active/status filter, retry control, and EventStore Admin UI availability indicator, and controls use the accepted Parties client/query contract.
2. Given the administrator performs a display-name search, when results are loaded, then the grid displays tenant-safe matching parties with display name, type, active/erased/restricted state, created/modified dates, and non-PII status indicators, and unsupported rich-search filters are disabled rather than silently sent.
3. Given the administrator filters by type, active status, created date, or modified date where supported, when the query runs, then the grid displays only matching current-tenant records, and invalid filters produce bounded validation feedback.
4. Given the result set is large, when the administrator browses results, then the grid uses server-side paging or virtualization, and it does not load unbounded result sets into the UI.
5. Given token, tenant, user, filters, or host configuration changes while results are in flight, when the stale response returns, then stale results are suppressed, and the console reloads or clears state for the current context only.
6. Given browse/filter UI tests run, when they cover search, filters, paging/virtualization, unsupported filters, stale response suppression, retry, and tenant switch, then the grid remains tenant-safe, scannable, and predictable.

## Tasks / Subtasks

- [x] Preserve the existing working browse/search toolbar while completing the Story 7.2 filter contract. (AC: 1, 2, 3)
  - [x] Keep the first viewport operational inside `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`; do not introduce a landing page, hero, marketing panel, or decorative shell.
  - [x] Preserve the existing Fluent UI search input, party type filter, active/status filter, search/clear buttons, retry button, rich-search capability controls, and EventStore Admin UI availability indicator.
  - [x] Add created-date and modified-date filter controls only through bounded, localized UI state; invalid ranges must show bounded validation feedback and must not call the query client.
  - [x] Disable unsupported rich-search filters in search mode rather than sending filters that the accepted search query contract ignores.

- [x] Keep browse/search data access on the accepted Parties client/query boundary. (AC: 1, 2, 3, 4)
  - [x] Continue list/search reads through `IPartiesQueryClient` when registered, with `IQueryService` as the FrontComposer fallback in `PartiesAdminPortalApiClient`.
  - [x] Do not call retired `api/v1/parties`, `api/v1/admin/**`, DAPR actors, projection actors, local search services, or `src/Hexalith.Parties` actor-host internals from the portal.
  - [x] Forward only supported list filters: page, bounded page size, party type, active status, created-after/before, and modified-after/before.
  - [x] Preserve safe cache discriminator behavior; never include tenant ids, display names, raw search text, emails, identifiers, consent text, JWTs, claims, or raw ProblemDetails details in storage keys or telemetry dimensions.

- [x] Maintain tenant-safe grid rendering and paging behavior. (AC: 2, 4, 6)
  - [x] Display display name, party type, active/inactive plus erased/restricted status, created date, modified date, and non-PII status indicators.
  - [x] Keep server-side paging bounded by `AdminPortalQueryBounds`; do not load unbounded result sets into component state.
  - [x] Keep row keys and route/deep-link behavior based on safe party ids only; never render tenant-scoped ids as user-facing labels.
  - [x] Preserve encoded Razor/component text rendering; do not use `MarkupString`, `AddMarkupContent`, `innerHTML`, JavaScript interpolation, or raw HTML fragments for any party data or error text.

- [x] Preserve fail-closed authorization, tenant switch, stale-response, and retry behavior. (AC: 1, 5, 6)
  - [x] Keep `AuthenticationStateProvider` plus `Hexalith.Tenants` membership/role derivation through `IAdminPortalAuthorizationService`.
  - [x] Preserve cancellation/version guards in list loading so token loss, tenant switch, sign-out, missing tenant, non-admin, forbidden, timeout, malformed response, and contract-unavailable states clear sensitive state before rendering.
  - [x] Keep retry bounded to current context only; retry must not replay stale tenant/user/filter state.
  - [x] Keep `AdminPortalPartyQueryService`, `PartiesAdminListCoordinator`, and `AdminPortalGdprStateCoordinator` wired into reset paths; do not create parallel state services unless the old ones are retired.

- [x] Preserve accessibility, localization, and dense admin ergonomics. (AC: 1, 2, 3, 6)
  - [x] Keep every toolbar control keyboard reachable with stable accessible labels.
  - [x] Keep status changes announced through the polite status region and never rely on color alone for active, inactive, restricted, erased, degraded, or blocked states.
  - [x] Localize new labels, validation messages, date labels, empty states, counts, and retry/status text through existing `AdminPortalLabels` patterns.
  - [x] Ensure toolbar, results, detail, paging, and status regions remain usable without text overlap at supported viewport sizes.

- [x] Add or update focused tests. (AC: 1-6)
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` to cover search, type filter, active filter, created/modified date filters, invalid ranges, unsupported-rich-search filter disabling, retry, paging, empty results, stale-response suppression, and tenant switch.
  - [x] Extend service/API client tests if date filters, validation, or query contract serialization changes.
  - [x] Preserve existing bUnit coverage for Fluent UI controls, auth/tenant fail-closed states, rich-search gating, XSS encoding, EventStore links, localization, keyboard reachability, coordinator transitions, route hardening, and tenant-switch cancellation.
  - [x] Add source guardrails in `tests/Hexalith.Parties.Client.Tests/AdminPortal/**` only if transport or query contract behavior changes.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release` if query/client contract behavior changes.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.2 to `7-2-browse-and-filter-party-results`; this supersedes the older historical file `7-2-subscriber-experience-and-at-least-once-delivery.md`, which belongs to a prior plan and must not be used as the current story source.
- Epic 7 objective: administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 7: Administration Console`]
- Story 7.2 covers FR65 and UX-DR4, UX-DR5, UX-DR10. [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.2: Browse and Filter Party Results`]
- Related functional requirements include listing with pagination and type/active filters, tenant isolation at all layers, and date filtering by creation or last-modified ranges. [Source: `_bmad-output/planning-artifacts/epics.md#Functional Requirements`]

### Architecture Guardrails

- The Parties Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor. It must register Parties-domain views with the FrontComposer shell, read through EventStore query/client abstractions, route supported commands through the typed Parties client/EventStore command boundary, and delegate generic event/stream browsing to EventStore Admin UI safe deep-links. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- The portal must fail closed and clear sensitive state on sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- The list supports browse, display-name search, rich-search capability gating, type and active filters where the accepted query contract supports them, server-side paging, stale response suppression, and tenant-switch cancellation. Search mode disables unsupported filters rather than silently sending ignored filters. [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md#Browse Search and Filter`]
- Result rows should show display name, type, active/erased/restricted state, created/modified dates, and non-PII status indicators. [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md#Information Architecture`]
- EventStore Admin UI links must use only safe identifiers and generic labels; if the Admin UI base address is unavailable, render a disabled bounded state instead of embedding a generic stream browser. [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md#EventStore Admin UI Boundary`]
- Labels, dates, counts, status messages, validation messages, operation outcomes, focus behavior, keyboard access, non-color-only state, and polite status announcements are part of the frontend architecture contract. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]

### Current Implementation to Preserve

`src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`

- Current state: renders `@page "/admin/parties"`, `@page "/admin/parties/{RoutePartyId}"`, and `@page "/admin/parties/{RoutePartyId}/gdpr"` as a working console with a Fluent UI search input, party type filter, active filter, search/clear/retry buttons, rich-search mode buttons, EventStore Admin UI availability state, `FluentDataGrid`, paging controls, and sibling detail region.
- Current browse/search behavior: `LoadPageAsync` routes empty query to `ListPartiesAsync` and non-empty query to `SearchPartiesAsync`; list requests include bounded page/page size, party type, and active state; search requests intentionally do not forward filter values yet.
- Story change: complete the browse/filter contract by adding created/modified date filters where supported, invalid filter validation, and stronger tests for unsupported search filters, stale responses, retry, and tenant switch.
- Preserve: encoded Razor text rendering, Fluent UI controls, route hardening from Story 7.1, cancellation/version guards, tenant/auth reset behavior, rich-search capability gating, status announcements, keyboard handling, and `NormalizeDetail` null-collection protection.

`src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`

- Current state: prefers `IPartiesQueryClient` for list/search/detail, falls back to FrontComposer `IQueryService`, maps failures to bounded `AdminPortalQueryFailureKind`, and routes GDPR operations through `IAdminPortalGdprClient`.
- Current list support: `AdminPortalListRequest` already carries `CreatedAfter`, `CreatedBefore`, `ModifiedAfter`, and `ModifiedBefore`; `BuildListFilters` serializes them for the query-service fallback.
- Story change: expose and validate date filters in the component only if the accepted list path supports them; do not add date filters to display-name rich search unless the search contract explicitly supports them.
- Preserve: typed-client preference, FrontComposer fallback, fail-closed contract-unavailable behavior, bounded skip/page-size logic, rich-search probe parsing, no retired REST/admin endpoint calls, and no PII cache discriminator.

`tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`

- Current state: covers dense row rendering, first-viewport console composition, route detail/GDPR variants, unsafe route id rejection, Fluent controls, auth/tenant fail-closed states, Tenants role derivation, search/filter behavior, rich-search capability, tenant switch, detail hydration, XSS encoding, EventStore links, localization, keyboard reachability, non-color-only status badges, coordinator transitions, and query-service cancellation.
- Story change: add focused assertions for date filter controls and invalid ranges, richer unsupported-filter behavior in search mode, server-side paging/virtualization boundaries, retry scoped to current context, and stale response suppression when filters or tenant context change.
- Preserve: scenario breadth; do not weaken privacy, accessibility, route-hardening, localization, or tenant-isolation tests.

### Project Structure Notes

- Production portal code belongs in `src/Hexalith.Parties.AdminPortal`.
- Admin portal component/service tests belong in `tests/Hexalith.Parties.AdminPortal.Tests`.
- Admin portal client/transport guardrails belong in `tests/Hexalith.Parties.Client.Tests/AdminPortal`.
- Keep Parties actor-host code in `src/Hexalith.Parties` free of public REST, admin UI, Swagger/OpenAPI, and in-process MCP hosting.
- Do not edit root-level submodules (`Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Tenants`, `Hexalith.Memories`) unless a follow-up explicitly owns that cross-repo change.
- Do not initialize or update nested submodules recursively.

### Technology and Library Requirements

- Runtime: .NET SDK `10.0.300` from `global.json`, target framework `net10.0`.
- Package versions are centrally managed in `Directory.Packages.props`; do not add versions to individual `.csproj` files.
- Relevant current versions: Microsoft Fluent UI Blazor `5.0.0-rc.2-26098.1`, xUnit v3 `3.2.2`, bUnit `2.7.2`, Shouldly `4.3.0`, Dapr `1.17.9`, Aspire `13.3.3`.
- Use existing Fluent UI Blazor components already present in the portal (`FluentTextInput`, `FluentSelect`, `FluentButton`, `FluentDataGrid`) unless the repo already exposes a stronger local Fluent UI control for date entry.
- Web research was intentionally not performed for this story; rely on local project versions and existing component patterns.

### Previous Story Intelligence

- Story 7.1 completed the operational first viewport and route support for `/admin/parties/{partyId}` and `/admin/parties/{partyId}/gdpr`; selected rows navigate only for bounded non-PII route tokens, while tenant-scoped ids stay in in-page detail state. Preserve this route hardening. [Source: `_bmad-output/implementation-artifacts/7-1-compose-admin-console-working-view.md`]
- Story 7.1 review added tests for GDPR route rendering, unsafe route id rejection, query/fragment-safe route comparison, and explicit rejection of `.` and `..` route ids. Do not regress these tests. [Source: `_bmad-output/implementation-artifacts/7-1-compose-admin-console-working-view.md#Senior Developer Review (AI)`]
- Story 10.1 established the read-only admin browse/search/detail foundation and recorded that the portal must not add tenant lifecycle, tenant management, or GDPR mutation scope into browse work. Preserve that boundary. [Source: `_bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md`]
- Story 10.1.1 completed Tenants-backed authorization derivation, rich-search capability detection, Fluent UI migration, and coordinator/query-service lifecycle wiring. Do not regress to host-supplied auth booleans, raw HTML controls, or dead-code state services. [Source: `_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md`]
- Story 12.7 rebuilt the Admin Portal around FrontComposer/EventStore query boundaries and EventStore Admin UI deep-links. Preserve typed `IPartiesQueryClient` preference, `IQueryService` fallback, `IAdminPortalGdprClient` operations, safe link generation, and the existing UX specification. [Source: `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`]

### Anti-Patterns to Avoid

- Do not create a landing page, hero, marketing copy, decorative page shell, or feature explainer before the working console.
- Do not build a TypeScript SPA, standalone admin shell, duplicate tenant selector, duplicate authorization model, generic stream/event browser, or local search/filter cache inside Parties.
- Do not call retired direct Parties REST/admin endpoints, projection actors, DAPR actors, local search services, or `src/Hexalith.Parties` actor-host internals from the portal.
- Do not silently send unsupported filters with search requests; either disable the controls or validate before dispatch.
- Do not use client-side filtering to simulate tenant isolation, rich search, erased-record filtering, or server-side paging.
- Do not render party data, ProblemDetails text, consent text, processing summaries, or operator-entered values through raw markup APIs.
- Do not put names, emails, identifiers, consent text, free text, raw errors, tenant ids, tokens, claim values, or search text in URLs, storage keys, telemetry dimensions, filenames, logs, or EventStore Admin UI link labels.
- Do not weaken existing bUnit, privacy, accessibility, localization, route-hardening, tenant-switch, or transport guardrail tests to make filter work easier.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.2: Browse and Filter Party Results`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md#Browse Search and Filter`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `_bmad-output/implementation-artifacts/7-1-compose-admin-console-working-view.md`]
- [Source: `_bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md`]
- [Source: `_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md`]
- [Source: `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`]
- [Source: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`]

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created ready-for-dev story context for current Epic 7 Story 7.2 and reconciled it with existing AdminPortal browse/search implementation history. | Codex |
| 2026-05-22 | 0.2 | Implemented created/modified date filters, bounded validation, and focused browse/filter tests; moved story to review. | Codex |
| 2026-05-22 | 0.3 | Senior Developer Review (AI): added `@using System.Globalization` and dropped fully-qualified globalization names from `PortalFilters.TryParseDate`; reverted attempted `TextInputType.Date` switch (enum value does not exist in Fluent UI Blazor 5.0.0-rc.2). AdminPortal tests pass 77/77. Story → done. | Claude Opus 4.7 |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Resolved current sprint status and confirmed story key `7-2-browse-and-filter-party-results`.
- Confirmed historical `7-2-subscriber-experience-and-at-least-once-delivery.md` exists from a prior plan and is not the current story source.
- Loaded current Epic 7 Story 7.2 source, frontend architecture D20, UX admin portal spec, project context, Story 7.1 completion/review notes, Story 10.1/10.1.1/12.7 admin portal history, current AdminPortal component/API-client/test references, and recent commit titles.
- Skipped web research; local project versions and existing Fluent UI component usage are authoritative for this story.
- Implemented created-after, created-before, modified-after, and modified-before list filters as localized `FluentTextInput` controls using `YYYY-MM-DD` values.
- Added bounded date parsing and range validation before list dispatch; invalid ranges update the polite status region and do not call the query client.
- Preserved display-name search contract by disabling type/active/date filters in search mode and continuing to send no filters to `AdminPortalSearchRequest`.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed with 77/77 tests.
- Validation: `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release` passed with 18/18 tests.
- Validation: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story status set to `ready-for-dev`.
- Sprint status updated for current story key `7-2-browse-and-filter-party-results`.
- Checklist review applied: story includes specific acceptance criteria, current files to inspect, preserve/change guidance, anti-patterns, validation commands, source references, and the historical 7.2 artifact warning.
- Added created/modified date list filters with bounded validation and localized labels.
- Added focused component tests for date filter request forwarding, invalid date-range validation, and disabled date filters in display-name search mode.
- Validation complete: AdminPortal tests, focused AdminPortal client guardrails, and full solution build pass.

### File List

- `_bmad-output/implementation-artifacts/7-2-browse-and-filter-party-results.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
