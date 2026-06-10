---
baseline_commit: 78a5956
---

# Story 2.1: Embed the Admin area behind the Admin policy

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an admin,
I want the Admin area mounted under `/admin`,
so that admin pages are reachable and protected.

This story embeds the existing `Hexalith.Parties.AdminPortal` Razor class library into the already-built `Hexalith.Parties.UI` Blazor Server host. It is an integration and authorization story only: do not rebuild the AdminPortal, split its list/detail/GDPR UI into separate pages, add create/edit flows, implement picker changes, alter Parties domain contracts, or expose backend command/query endpoints. The result is that `/admin/parties`, `/admin/parties/{partyId}`, and `/admin/parties/{partyId}/gdpr` are discoverable through the host router, protected by the `Admin` policy at route level, backed by the AdminPortal service/state registrations, and surfaced by an Admin-only `Parties` navigation entry.

## Acceptance Criteria

1. **AC1 - AdminPortal RCL is referenced and route-discovered by the UI host.** Given `Hexalith.Parties.UI` starts, when Blazor routing is built, then routable components from `Hexalith.Parties.AdminPortal` are discoverable for both static endpoint routing and interactive routing. `MapRazorComponents<App>()` includes the AdminPortal assembly via `AddAdditionalAssemblies(...)`, and `Routes.razor` includes the same assembly in `Router.AdditionalAssemblies`.

2. **AC2 - Every AdminPortal route is protected by the Admin policy.** Given the routable AdminPortal component(s), when `/admin/parties`, `/admin/parties/{partyId}`, or `/admin/parties/{partyId}/gdpr` are inspected or requested through the Blazor router, then they carry `@attribute [Authorize(Policy = "Admin")]` or an equivalent compile-time constant resolving to the exact `Admin` policy. Do not make `Hexalith.Parties.AdminPortal` reference `Hexalith.Parties.UI` just to reuse `PartiesUiAuthorization.AdminPolicy`.

3. **AC3 - AdminPortal services, state coordinators, options, and resource/localization seams are wired by the host.** Given the host service collection, when `Program.cs` composes UI services, then `AddHexalithPartiesAdminPortal()` is called so `IPartiesAdminPortalApiClient`, `PartiesAdminListCoordinator`, `AdminPortalGdprStateCoordinator`, `AdminPortalPartyQueryService`, `IAdminPortalAuthorizationService`, options validation, and localization/label seams resolve under `ValidateScopes=true`. Existing `AdminPortalLabels` and service coordinators are reused; do not create duplicate state services in the host.

4. **AC4 - Admin-only navigation exposes the AdminPortal as "Parties".** Given a principal satisfying the `Admin` policy, when the FrontComposer nav renders registered entries, then the Admin area entry is titled `Parties`, links to `/admin/parties`, and requires the `Admin` policy. Given a Consumer or unauthenticated principal, the `Parties` admin nav entry is absent. The existing Consumer `/me` nav entry remains Consumer-only.

5. **AC5 - `/admin` remains protected and lands on the AdminPortal entry point.** Given an Admin visits `/admin`, when the route resolves, then it redirects or navigates to `/admin/parties` without showing the old "coming soon" placeholder. Given a non-Admin visits `/admin` or any `/admin/*` page, route authorization denies access before Admin record data is rendered.

6. **AC6 - Forbidden and unauthenticated states are honest and PII-free.** Given a non-Admin Consumer principal attempts `/admin/*`, when access is denied, then the UI shows a role-needed Forbidden/access-denied explanation and never calls the AdminPortal data client or renders party rows/detail. Given an unauthenticated principal attempts `/admin/*`, the existing sign-in challenge path is used with the return URL preserved.

7. **AC7 - Existing AdminPortal behavior is preserved.** Given an authorized admin reaches `/admin/parties`, when the component loads, then current list/search/detail/GDPR panel behavior remains as it is today: tenant/admin checks via `AdminPortalAuthorizationService`, dense operational layout, no marketing/landing shell, no PII in forbidden/tombstone messages, and current bUnit component tests continue to pass.

## Tasks / Subtasks

### Part A - Host reference and route discovery - AC1, AC3

- [x] **Task 1 - Add the AdminPortal RCL reference to the UI host** (`src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`) (AC1)
  - [x] Add a project reference to `..\Hexalith.Parties.AdminPortal\Hexalith.Parties.AdminPortal.csproj`.
  - [x] Keep Central Package Management intact: no package `Version=` attributes and no version edits.
  - [x] Do not add references from `AdminPortal` back to `UI`; dependency direction is host -> RCL.

- [x] **Task 2 - Register AdminPortal services in the UI host** (`src/Hexalith.Parties.UI/Program.cs`) (AC3)
  - [x] Import `Hexalith.Parties.AdminPortal.Extensions`.
  - [x] Call `builder.Services.AddHexalithPartiesAdminPortal()` after the FrontComposer/Fluent quickstart chain and before building the app.
  - [x] Preserve existing unconditional registrations for Admin/Consumer authorization, claim resolution, self-scope, and projection freshness.
  - [x] Preserve `ValidateScopes=true`; AdminPortal services are Scoped where they carry per-circuit/tenant state.
  - [x] Keep `AddPartiesClient(builder.Configuration)` gated on `Parties:BaseUrl`; AdminPortal's typed query/GDPR clients should resolve when the typed clients are configured and should retain its existing FrontComposer `IQueryService` fallback behavior otherwise.

- [x] **Task 3 - Add AdminPortal route discovery for static and interactive routing** (`src/Hexalith.Parties.UI/Program.cs`, `src/Hexalith.Parties.UI/Components/Routes.razor`) (AC1)
  - [x] Chain `.AddAdditionalAssemblies(typeof(Hexalith.Parties.AdminPortal.Components.PartiesAdminPortal).Assembly)` to `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
  - [x] Add the AdminPortal assembly to `<Router AdditionalAssemblies="...">` in `Routes.razor`.
  - [x] Keep `AuthorizeRouteView`, not `RouteView`; route-level `[Authorize]` attributes are not enforced by plain `RouteView`.
  - [x] Do not replace `AppAssembly="typeof(Program).Assembly"`; AdminPortal should be scanned in addition to the host assembly.

### Part B - Admin policy enforcement and navigation - AC2, AC4, AC5, AC6

- [x] **Task 4 - Add route-level Admin policy metadata to AdminPortal routable pages** (`src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`) (AC2, AC6)
  - [x] Add `@attribute [Authorize(Policy = "Admin")]` or an AdminPortal-local `const string AdminPolicy = "Admin"` imported into the component.
  - [x] Add the necessary `@using Microsoft.AspNetCore.Authorization` if not already available through `_Imports.razor`.
  - [x] Do not reference `Hexalith.Parties.UI.Authentication.PartiesUiAuthorization` from the RCL.
  - [x] Keep the existing internal `AdminPortalAuthorizationService` checks; route authorization is the outer gate, component authorization remains defense in depth.

- [x] **Task 5 - Change host nav registration from transitional Admin landing to AdminPortal Parties entry** (`src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs`) (AC4)
  - [x] Change the Admin nav entry title from `Administration` to `Parties`.
  - [x] Change its href from `/admin` to `PartiesAdminPortalManifest.Route` (`/admin/parties`) or the exact same literal if avoiding an additional using is cleaner.
  - [x] Preserve `RequiredPolicy: PartiesUiAuthorization.AdminPolicy`.
  - [x] Preserve Consumer nav behavior for `/me` with `Consumer` policy.

- [x] **Task 6 - Replace the `/admin` placeholder with a protected landing redirect** (`src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor`) (AC5)
  - [x] Keep `@page "/admin"` and `[Authorize(Policy = PartiesUiAuthorization.AdminPolicy)]`.
  - [x] Redirect/navigate to `/admin/parties` on initialization, or render a minimal protected link that immediately sends admins to `/admin/parties` if navigation cannot run during prerender.
  - [x] Remove "coming soon" placeholder copy; `/admin` should no longer imply the area is absent.
  - [x] Keep non-Admin denial handled by `AuthorizeRouteView`.

- [x] **Task 7 - Tune the route-level Forbidden explanation** (`src/Hexalith.Parties.UI/Components/Routes.razor`) (AC6)
  - [x] For authenticated-but-not-authorized users, show concise Forbidden copy that states an Admin role is required for the area.
  - [x] Keep copy PII-free and generic; do not include tenant ids, user ids, party ids, or backend exception details.
  - [x] Preserve `RedirectToChallenge` for unauthenticated users.

### Part C - Tests and regression guards - AC1-AC7

- [x] **Task 8 - Add host composition tests for AdminPortal wiring** (`tests/Hexalith.Parties.UI.Tests/*`) (AC1, AC3)
  - [x] Add/extend tests proving `Hexalith.Parties.UI.csproj` references `Hexalith.Parties.AdminPortal`.
  - [x] Add/extend source/host composition tests proving `Program.cs` calls `AddHexalithPartiesAdminPortal()` and `AddAdditionalAssemblies(...)` with the AdminPortal assembly.
  - [x] Add/extend a `Routes.razor` test proving `Router.AdditionalAssemblies` includes the AdminPortal assembly and `AuthorizeRouteView` remains in use.

- [x] **Task 9 - Update nav registration and gating tests** (`tests/Hexalith.Parties.UI.Tests/PartiesUiNavigationRegistrationTests.cs`, `tests/Hexalith.Parties.UI.Tests/PartiesUiNavEntryGatingTests.cs`) (AC4)
  - [x] Assert the Admin entry title is `Parties`.
  - [x] Assert the Admin entry href is `/admin/parties`.
  - [x] Assert Admin sees only `Parties` and Consumer does not see it.
  - [x] Keep existing Consumer nav assertions intact.

- [x] **Task 10 - Add route policy reflection tests for AdminPortal routes** (`tests/Hexalith.Parties.AdminPortal.Tests/*` or `tests/Hexalith.Parties.UI.Tests/*`) (AC2)
  - [x] Reflect `PartiesAdminPortal` route attributes and assert `/admin/parties`, `/admin/parties/{RoutePartyId}`, and `/admin/parties/{RoutePartyId}/gdpr` are present.
  - [x] Reflect `AuthorizeAttribute` and assert exactly one Admin policy requirement with `Policy == "Admin"`.
  - [x] Add a guard that `Hexalith.Parties.AdminPortal.csproj` does not reference `Hexalith.Parties.UI`.

- [x] **Task 11 - Add/extend authorization behavior tests** (`tests/Hexalith.Parties.UI.Tests/*`, `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`) (AC5, AC6, AC7)
  - [x] Assert `/admin` remains protected by the Admin policy and points to `/admin/parties`.
  - [x] Assert authenticated non-Admin forbidden copy is PII-free and role-needed.
  - [x] Assert the existing `PartiesAdminPortal_NonAdmin_DrivesListCoordinatorToForbidden` path still does not render party data.
  - [x] If a route-level render test is added, use bUnit fake authorization policies; do not require live Keycloak.

- [x] **Task 12 - Run focused verification** (AC1-AC7)
  - [x] `dotnet build src/Hexalith.Parties.UI -c Release -m:1`
  - [x] `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1`
  - [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests -c Release -m:1`
  - [x] Direct xUnit executable runs for affected UI/AdminPortal test classes if `dotnet test --filter` returns zero tests under xUnit v3 MTP.
  - [x] `bash scripts/check-no-warning-override.sh`

## Dev Notes

### Source Discovery Summary

- Loaded `epics_content` from `_bmad-output/planning-artifacts/epics.md`; Epic 2 defines this story as the first Admin records management step and scopes later list/detail/create/edit/picker work to stories 2.2-2.5.
- Loaded `architecture_content` from `_bmad-output/planning-artifacts/architecture.md`; it defines `Hexalith.Parties.UI` as the Blazor Server BFF host and `Hexalith.Parties.AdminPortal` as the embeddable Admin RCL.
- Loaded UX source files under `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`; for this story, the relevant constraints are Admin comfortable density, policy-gated nav, no marketing landing, WCAG route/focus baseline, and PII-free forbidden/tombstone copy.
- No formal PRD file exists in `_bmad-output/planning-artifacts`; epics, architecture, UX spine, readiness reports, and brownfield docs are the source of truth.
- Loaded persistent project facts from `_bmad-output/project-context.md` and sibling submodule project contexts.
- Reviewed existing code in `src/Hexalith.Parties.UI`, `src/Hexalith.Parties.AdminPortal`, `tests/Hexalith.Parties.UI.Tests`, and `tests/Hexalith.Parties.AdminPortal.Tests`.
- Reviewed recent git history: Epic 1 completed host, OIDC, role routing, consumer self-scope, status mapping, SignalR freshness, shared domain components, accessibility gates, and deployment.

### Existing Code to Reuse

- Reuse `src/Hexalith.Parties.UI` as the only UI host. Do not create a second host.
- Reuse `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`; it already declares routes for `/admin/parties`, `/admin/parties/{RoutePartyId}`, and `/admin/parties/{RoutePartyId}/gdpr`.
- Reuse `AddHexalithPartiesAdminPortal()`; it already registers AdminPortal options, API client, list coordinator, GDPR state coordinator, query service, authorization service, EventStore admin links, and FrontComposer quickstart dependencies.
- Reuse `PartiesAdminPortalManifest.Route`, `DetailRoute`, and `GdprRoute` for route constants where dependency direction allows it.
- Reuse `AuthorizeRouteView` in `Routes.razor`; it is already the security-critical replacement for `RouteView`.
- Reuse existing `PartiesUiAuthorization.AdminPolicy` in the UI host only. In AdminPortal, use the literal/constant `Admin` locally to avoid an RCL -> host dependency.
- Reuse existing AdminPortal component tests; they already cover dense console layout, forbidden state, tenant switch reset, PII-safe routes, localized labels, and service-state transitions.

### Current Files Being Modified

| File | Current state | Story change | Preserve |
|---|---|---|---|
| `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` | Web host references Client, Contracts, ServiceDefaults, EventStore SignalR, FrontComposer; no AdminPortal reference. | Add AdminPortal project reference. | CPM/no `Version=`, HFC1001 `NoWarn`, no internal host/domain references. |
| `src/Hexalith.Parties.UI/Program.cs` | Wires ServiceDefaults, Razor components, FluentUI, FrontComposer, Admin/Consumer policies, claims, self-scope, freshness, optional typed clients/OIDC. | Register AdminPortal services and add AdminPortal assembly to Razor component endpoint discovery. | OIDC server-side token pattern, gated `AddPartiesClient`, no Dapr sidecar assumptions, `ValidateScopes=true`. |
| `src/Hexalith.Parties.UI/Components/Routes.razor` | Router scans only UI host assembly and uses `AuthorizeRouteView`. | Add AdminPortal assembly to `AdditionalAssemblies`; keep `AuthorizeRouteView`; tune Forbidden copy. | Redirect unauthenticated users through `RedirectToChallenge`; focus behavior. |
| `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs` | Registers `Administration` -> `/admin` for Admin and `My space` -> `/me` for Consumer. | Admin entry becomes `Parties` -> `/admin/parties`; still Admin-only. | Lowercase `parties` bounded-context grouping and Consumer nav. |
| `src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor` | Protected `/admin` placeholder says Admin area is coming soon. | Protected redirect/landing to `/admin/parties`. | Admin policy attribute and no data access from landing. |
| `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` | Routable RCL component with internal auth checks but no route-level `[Authorize]` attribute. | Add Admin policy route metadata. | Existing routes, component state machine, dense layout, list/detail/GDPR behavior, no host reference. |
| `tests/Hexalith.Parties.UI.Tests/*` | Pins host composition, area policy, nav registration/gating. | Update for AdminPortal reference, route discovery, `Parties` nav, `/admin/parties` href. | xUnit v3 + Shouldly + bUnit style. |
| `tests/Hexalith.Parties.AdminPortal.Tests/*` | Pins AdminPortal composition and component behavior. | Add route-level Admin policy guard and no UI-host reference guard. | Existing component tests and service registration tests. |

Do not modify `Hexalith.Parties.Contracts`, `Hexalith.Parties.Client`, `Hexalith.Parties.Picker`, `Hexalith.Parties` actor host authorization, EventStore/Tenants submodules, deploy manifests, or domain command/query contracts for this story.

### Technical Requirements

- The host remains Blazor Interactive Server. The browser talks to the UI host only; backend calls go through the typed Parties client to the EventStore gateway.
- Route discovery for an RCL requires both server endpoint disclosure and interactive router disclosure: `MapRazorComponents(...).AddAdditionalAssemblies(...)` and `Router.AdditionalAssemblies`.
- Admin navigation gating with `AuthorizeView`/FrontComposer `RequiredPolicy` is a visibility control only; the destination component must also carry `[Authorize(Policy = "Admin")]`.
- `AuthorizeRouteView` must remain in `Routes.razor`; plain `RouteView` ignores authorization metadata.
- `Hexalith.Parties.AdminPortal` must not reference `Hexalith.Parties.UI`; this avoids the architecture gap where RCLs depend on host-owned primitives.
- AdminPortal route-level policy string must be exactly `Admin`, matching `PartiesUiAuthorization.AdminPolicy` and existing policy registration.
- Non-Admin attempts must not trigger list/detail/GDPR data calls through `IPartiesAdminPortalApiClient`.
- Forbidden copy must be localized or at least routed through the existing label/copy seam if the RCL renders it; host route forbidden copy can remain generic and PII-free.
- Keep all services with claims/tenant/circuit state Scoped. Do not introduce Singleton capture of `AuthenticationStateProvider`, `PartyIdClaimResolver`, tenant projection stores, AdminPortal coordinators, or typed clients.

### Architecture Compliance

- **AR-D4 composition:** `Hexalith.Parties.UI` embeds `Hexalith.Parties.AdminPortal`; AdminPortal remains an RCL.
- **AR-D5 transport:** server-side BFF owns auth; no OIDC tokens reach the browser; no public command/query API is added.
- **AR-StatusMap and UX-DR8:** this story does not remap status/freshness; later Admin stories must resolve shared status/freshness primitives without making AdminPortal depend on the UI host.
- **NFR3/PII hygiene:** no party data in forbidden states, logs, route denial copy, or tests.
- **NFR9 quality gates:** .NET 10, `.slnx`, Central Package Management, `TreatWarningsAsErrors`, xUnit v3, Shouldly, bUnit where relevant.

### Previous Story Intelligence

- Epic 1 is complete. The immediate baseline commit is `78a5956`, after `feat(story-1.10)` and the Epic 1 retrospective.
- Story 1.3 established `PartiesUiAuthorization`, `AuthorizeRouteView`, Admin/Consumer nav gating, and `/admin`/`/me` area protection. Story 2.1 must extend those patterns, not replace them.
- Story 1.7 and the architecture retrospective note that `StatusKind`, freshness, and shared domain components currently live in `Hexalith.Parties.UI`; do not make AdminPortal reference the UI host to consume them.
- Story 1.9 established accessibility gates and no-marketing first viewport expectations. AdminPortal tests already assert the Admin console renders a working toolbar/grid/detail layout without hero/landing shell.
- Story 1.10 added ServiceDefaults/deploy wiring. This story should not change deployment topology or K8s manifests.

### Git Intelligence Summary

- Recent commits show a linear Epic 1 foundation sequence:
  - `ac523f0 feat(story-1.10): Deploy parties-ui container/K8s with production KMS gate`
  - `43053b3 feat(story-1.9): Accessibility foundation and CI a11y gate (WCAG 2.2 AA)`
  - `9610f70 feat(story-1.8): Shared domain components (party-state badge, freshness indicator, GDPR destructive button)`
  - `90f2b97 feat(story-1.7): Live freshness via SignalR with shared optimistic reconcile effect`
  - `7c88095 feat(story-1.6): Canonical StatusKind UI mapping with aria-live politeness split`
- Follow the existing pattern: narrow source changes, focused bUnit/static tests, no package drift, no broad refactors, and explicit verification notes.

### Latest Technical Information

- Microsoft Learn's ASP.NET Core Blazor routing guidance for .NET 10 says routable components in additional/component-library assemblies must be disclosed with `MapRazorComponents(...).AddAdditionalAssemblies(...)` for static routing and `Router.AdditionalAssemblies` for interactive routing. This directly applies to embedding `Hexalith.Parties.AdminPortal` in `Hexalith.Parties.UI`. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-10.0#route-to-components-from-multiple-assemblies]
- Microsoft Learn's Blazor authorization guidance states that `AuthorizeView` only controls rendered UI visibility and does not prevent navigation to a component; authorization must be implemented separately on the destination component. This is why this story requires both Admin-only nav and `[Authorize(Policy = "Admin")]` on AdminPortal routes. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0#authorizeview-component]
- The same Blazor authorization guidance states `[Authorize]` is available on routable Razor components and authorization is performed as part of routing, not child component rendering. This is why the policy belongs on the AdminPortal `@page` component(s). [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0#authorize-attribute]

### File Structure Requirements

| Action | File |
|---|---|
| UPDATE | `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` |
| UPDATE | `src/Hexalith.Parties.UI/Program.cs` |
| UPDATE | `src/Hexalith.Parties.UI/Components/Routes.razor` |
| UPDATE | `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs` |
| UPDATE | `src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor` |
| UPDATE | `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` |
| UPDATE | `tests/Hexalith.Parties.UI.Tests/PartiesUiNavigationRegistrationTests.cs` |
| UPDATE | `tests/Hexalith.Parties.UI.Tests/PartiesUiNavEntryGatingTests.cs` |
| UPDATE | `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs` |
| UPDATE/NEW | `tests/Hexalith.Parties.UI.Tests/*AdminPortal*Composition*Tests.cs` |
| UPDATE/NEW | `tests/Hexalith.Parties.AdminPortal.Tests/*Authorization*Tests.cs` |

### Testing Requirements

- Static/reflection tests must fail if AdminPortal routable components lose the `Admin` policy.
- Host composition tests must fail if route discovery omits AdminPortal for either static endpoint routing or interactive router routing.
- Nav tests must fail if Consumers can see the Admin `Parties` entry or if Admins lose the `Parties` entry.
- Component tests must fail if non-Admin or missing-tenant states render party rows/detail.
- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not add Moq, FluentAssertions, or raw `Assert.*`.
- Build/test with Release and warnings as errors.

### Project Structure Notes

- Alignment: `Hexalith.Parties.UI` is the host and owns route discovery, auth policies, and nav composition.
- Alignment: `Hexalith.Parties.AdminPortal` is an RCL and owns Admin page UI/services, but it must stay host-agnostic.
- Alignment: AdminPortal's current single routable component serves list/detail/GDPR routes. Do not force a page split in this story; that belongs only if later stories deliberately refactor the Admin UI.
- Detected conflict: architecture wants shared status/freshness primitives reused by RCLs, but current primitives live in `Hexalith.Parties.UI`. This story must not solve that by creating a reverse RCL -> host reference.
- Detected variance: `PartiesAdminPortalManifest.BoundedContext` is currently `"Parties"` while UI nav grouping uses lowercase `"parties"`. Do not change it unless tests prove FrontComposer route discovery/nav grouping requires the change; avoid unrelated manifest churn.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.1: Embed the Admin area behind the Admin policy`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Gap Analysis Results`]
- [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]
- [Source: `src/Hexalith.Parties.UI/Program.cs`]
- [Source: `src/Hexalith.Parties.UI/Components/Routes.razor`]
- [Source: `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs`]
- [Source: `src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs`]
- [Source: `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Parties.UI.Tests/PartiesUiNavigationRegistrationTests.cs`]
- [Source: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`]
- [Source: Microsoft Learn, ASP.NET Core Blazor routing, .NET 10, `AdditionalAssemblies` guidance]
- [Source: Microsoft Learn, ASP.NET Core Blazor authentication and authorization, .NET 10, `AuthorizeView` and `[Authorize]` guidance]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase: `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1` failed before implementation because the UI host did not reference AdminPortal.
- Found and fixed AdminPortal DI ambiguity when `IQueryService` fallback was present by registering `IPartiesAdminPortalApiClient` through the intended `IServiceProvider` constructor.
- `dotnet test --filter` could not run in this sandbox because the .NET test host named-pipe bind failed with `SocketException (13): Permission denied`; direct xUnit v3 executables were used instead.
- Required exact build commands without properties were blocked by NuGet audit network calls to `api.nuget.org` (`NU1900`) in the restricted environment. Equivalent compile builds passed with `-p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added the AdminPortal RCL as a UI host project reference and registered `AddHexalithPartiesAdminPortal()` in the host composition path.
- Added AdminPortal assembly discovery to both static Razor component endpoint routing and the interactive router while preserving `AuthorizeRouteView`.
- Protected AdminPortal routes with the exact `Admin` policy, kept internal AdminPortal authorization checks intact, and guarded against an RCL-to-host reference.
- Replaced the transitional Admin nav/landing path with an Admin-only `Parties` entry at `/admin/parties` and a protected `/admin` redirect/link.
- Added/updated host composition, route discovery, nav gating, landing/forbidden copy, AdminPortal route policy, and non-admin component regression tests.
- Verification passed with direct xUnit executables: UI tests 256/256, AdminPortal tests 112/112.
- Senior review fixed AdminPortal degraded/test host startup so the portal composes without a typed backend until data is requested.
- Senior review made the shared route-forbidden copy route-aware so Admin-only routes keep the Admin-role message without mislabeling Consumer-only route denials.

### File List

- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Components/Routes.razor`
- `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs`
- `src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalOptionsValidator.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiNavigationRegistrationTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiNavEntryGatingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalAuthorizationTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs`
- `tests/e2e/specs/admin-area-authorization.spec.ts`
- `tests/e2e/playwright.config.ts`
- `_bmad-output/implementation-artifacts/2-1-embed-the-admin-area-behind-the-admin-policy.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/story-automator/orchestration-1-20260609-205725.md`

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-06-10

Outcome: Approved after automatic fixes.

Findings fixed:

- [CRITICAL] `PartiesAdminPortalOptionsValidator` failed host startup when `Parties:BaseUrl` was absent and no FrontComposer `IQueryService` fallback was registered. This violated the story requirement to keep `AddPartiesClient(...)` gated and preserve degraded/test boot. Fixed by allowing lazy backend configuration when neither backend is registered; runtime data access still fails closed through `AdminPortalQueryException`.
- [MEDIUM] `Routes.razor` used the Admin-role forbidden message for every denied route, including Consumer-only routes such as `/me`. Fixed by making the copy route-aware: `/admin` and `/admin/*` show the Admin role requirement, other denied routes keep the generic access-denied copy.
- [MEDIUM] The story File List omitted changed E2E/test-summary/orchestration artifacts and the service validator test touched during review. Fixed by syncing the File List to the actual git changes.

Review verification:

- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`
- [x] `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1 -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`
- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`
- [x] `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` - 256 total, 0 failed.
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - 112 total, 0 failed.
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-area-authorization.spec.ts --project=chromium --list` - 4 tests discovered.
- [x] `bash scripts/check-no-warning-override.sh`
- [ ] `cd tests/e2e && npm run test -- specs/admin-area-authorization.spec.ts --project=chromium` - attempted, blocked by sandbox Kestrel socket bind permission: `System.Net.Sockets.SocketException (13): Permission denied`.

### Change Log

- 2026-06-10 - Embedded AdminPortal into the UI host, added Admin route protection/navigation, fixed AdminPortal API client DI registration, and added regression tests for story 2.1.
- 2026-06-10 - Senior review auto-fixed AdminPortal lazy backend validation, route-aware forbidden copy, and story File List discrepancies; marked story done.
