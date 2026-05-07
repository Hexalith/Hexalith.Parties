# Story 10.1.1: Admin Portal — FrontComposer / Tenants / Rich-Search Integration

Status: backlog

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

- [ ] Hexalith.Tenants integration (AC: 1)
  - [ ] Inject `AuthenticationStateProvider`; consume `Hexalith.Tenants` membership/role services.
  - [ ] Replace passive `[Parameter] bool IsAuthenticated/HasTenantContext/IsAdmin` with derived values; remove host-wiring contract.
  - [ ] Update bUnit tests to assert auth derivation; remove the host-bool fixtures.
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
