# Story 10.1.1: Admin Portal — FrontComposer / Tenants / Rich-Search Integration

Status: in-progress

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
- [ ] Rich-search capability detection (AC: 2)
  - [ ] Add capability probe consumer (endpoint TBD with Story 9.6 owners).
  - [ ] Cache the probe result for the circuit lifetime; invalidate on tenant switch.
  - [ ] Surface degraded-probe state distinct from local-only.
- [ ] FrontComposer Fluent UI adoption (AC: 3)
  - [ ] Replace `<table>` with `FluentDataGrid<T>` and column descriptors.
  - [ ] Replace search/filter inputs with `FluentTextInput`/`FluentSelect`.
  - [ ] Replace pagination buttons with FrontComposer paging component.
  - [ ] Wire FrontComposer query transport (ETag caching, auth redirect).
- [ ] Activate scaffolding services (AC: 4)
  - [ ] Inject `AdminPortalPartyQueryService` into the component.
  - [ ] Drive `PartiesAdminListCoordinator.State` from real load/error transitions.
  - [ ] Tighten fitness tests to assert behavior, not type presence.
- [ ] Validate build and affected tests
  - [ ] `dotnet test tests\Hexalith.Parties.AdminPortal.Tests --configuration Release`.
  - [ ] `dotnet build Hexalith.Parties.slnx --configuration Release`.

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

### Completion Notes

- Completed AC1 Tenants integration: the portal no longer accepts host-supplied `IsAuthenticated`, `HasTenantContext`, or `IsAdmin` parameters.
- Added `AdminPortalAuthorizationService`, backed by `AuthenticationStateProvider` and `Hexalith.Tenants.Client.Projections.ITenantProjectionStore`, to derive authentication, active tenant context, and tenant-owner admin role.
- Updated bUnit coverage to seed Tenants membership/roles and assert owner/non-owner/unauthenticated behavior without host bool fixtures.
- Set story and sprint tracking to `in-progress`.
- HALT before rich-search capability detection because the story names the endpoint as TBD and the repository does not contain a stable capability endpoint contract to consume.

## File List

- src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor
- src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs
- src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj
- src/Hexalith.Parties.AdminPortal/_Imports.razor
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationService.cs
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationState.cs
- src/Hexalith.Parties.AdminPortal/Services/IAdminPortalAuthorizationService.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj
- _bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-05-07: Completed AC1 Tenants-backed authorization derivation and halted on the unresolved rich-search capability endpoint contract.
