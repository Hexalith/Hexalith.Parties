---
baseline_commit: b7303af096c0271fdae2516345eb45860c4ee535
---

# Story 2.4: Create and edit a party with validation (FR-Admin-3)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an admin,
I want to create and edit parties with validation,
so that I can author correct records.

This is the first net-new Admin records-management surface in Epic 2. Build it inside `Hexalith.Parties.AdminPortal` and wire it through the existing AdminPortal route/auth/query patterns. Do not bypass the AdminPortal API adapter, do not call the actor host directly, and do not add browser-callable command/query endpoints. The existing list/detail/GDPR component remains the owner of browse/detail behavior; this story adds the create/edit form routes and enables the existing Edit entry.

## Acceptance Criteria

1. **AC1 - Create and edit routes are protected AdminPortal routes.** Given an Admin opens `/admin/parties/new` or `/admin/parties/{id}/edit`, when the route renders, then it is served from the `Hexalith.Parties.AdminPortal` RCL, carries the `Admin` policy, passes through `IAdminPortalAuthorizationService`, and makes no list/detail/command calls for unauthenticated, non-Admin, or missing-tenant states.

2. **AC2 - Existing browse/detail/GDPR routes keep their behavior.** Given existing routes `/admin/parties`, `/admin/parties/{id}`, and `/admin/parties/{id}/gdpr`, when Story 2.4 is complete, then Story 2.1-2.3 behavior still works: safe route-id validation, list cancellation, detail focus restore, GDPR panel composition, stale/degraded last-known rendering, erased/gone privacy behavior, and phone detail sheet behavior remain intact.

3. **AC3 - Person/Organization chooser is a real radio group.** Given the form is displayed, when an Admin chooses party type, then the chooser uses FluentUI radio controls, exposes `role="radiogroup"` semantics, has visible labels for Person and Organization, supports keyboard selection, and swaps the relevant field set without losing the user's current input.

4. **AC4 - Create submits a `CreatePartyComposite` command.** Given `/admin/parties/new` has valid data, when the Admin submits, then the UI builds `CreatePartyComposite` with a generated valid GUID-form `PartyId`, the selected `PartyType`, the matching `PersonDetails` or `OrganizationDetails`, and supported contact/identifier sub-operations. The generated id is not a user-editable field.

5. **AC5 - Edit submits `UpdatePartyComposite` with route id authoritative.** Given `/admin/parties/{id}/edit`, when the Admin submits changes, then the UI uses the normalized safe route id as both the command route argument and `UpdatePartyComposite.PartyId`. Any party id value from form input, hidden fields, client state, query string, picker selection, or payload is ignored for command identity.

6. **AC6 - Edit form is initialized from the current detail without false empties.** Given an edit route opens, when `GetPartyAsync(id)` succeeds, then the form is prefilled from `PartyDetail`. Partial/display-name-only detail marks unavailable sections as still loading or disabled until enough data exists; erased/gone/missing/forbidden states show privacy-preserving copy and do not render stale personal fields.

7. **AC7 - Validation failures are inline, assertive, and input-preserving.** Given client-side validation fails or the gateway returns `PartyCommandValidationRejected`/HTTP 422, when validation is shown, then field errors are tied to fields via `aria-describedby`, are announced through `role="alert"`, preserve all user-entered values, and offer retry. Do not render raw problem details, payload fragments, stack traces, tenant ids, or personal data from backend error text.

8. **AC8 - Accepted commands use optimistic eventual-consistency UX.** Given create or edit command acceptance, when the command result has no confirmed `PartyDetail` payload yet, then the form/detail view reflects the intended change optimistically, announces `Saved - updating...` through `role="status" aria-live="polite"`, navigates to the created/edited detail route when safe, and silently reconciles on projection confirm or refresh. Acceptance is not read-your-write.

9. **AC9 - Command result payloads update the detail safely.** Given `PartiesCommandResult<PartyDetail>` contains an updated detail payload, when the command completes, then the AdminPortal state uses that payload as the optimistic/current detail only after normalizing it through the same detail privacy and partial-projection rules used by Story 2.3.

10. **AC10 - In-form party-picker integration is prepared without stealing Story 2.5.** Given the form includes the related-party field, when a picker selection is available, then the form stores the selected `{partyId, partyType, status}` as relationship input without leaving the form. Full Fluent 2 re-skin and WAI-ARIA combobox behavior remain Story 2.5 scope; do not duplicate or fork the picker implementation here.

11. **AC11 - Mobile and zoom layouts are usable.** Given 320px width and 200% zoom equivalent, when the create/edit form renders, then fields reflow to one column, toolbar actions do not overlap content, submit/cancel targets are at least 44px on touch layouts, validation messages remain next to their fields, and no horizontal page overflow is introduced.

12. **AC12 - Automated verification covers the workflow.** Given the story is complete, when tests run, then bUnit covers route auth, radio semantics, create command construction, route-id-authoritative edit command construction, client and gateway validation mapping, optimistic status, privacy failures, and browse/detail regressions; Playwright covers create, edit, validation alert focus/announcement observability, phone/zoom layout, and preserved Admin fixture authorization.

## Tasks / Subtasks

### Part A - Route and composition - AC1, AC2, AC6

- [x] **Task 1 - Add AdminPortal create/edit routes without breaking browse routes** (AC1, AC2)
  - [x] Add a dedicated AdminPortal form component such as `CreateEditPartyPage.razor` under `src/Hexalith.Parties.AdminPortal/Components/`, or a clearly separated form subcomponent composed by `PartiesAdminPortal.razor`.
  - [x] Add `@page "/admin/parties/new"` and `@page "/admin/parties/{RoutePartyId}/edit"` with `[Authorize(Policy = "Admin")]`.
  - [x] Ensure literal `/admin/parties/new` is treated as create, never as `RoutePartyId = "new"` detail selection.
  - [x] Preserve existing routes in `PartiesAdminPortal.razor`: `/admin/parties`, `/admin/parties/{RoutePartyId}`, `/admin/parties/{RoutePartyId}/gdpr`.
  - [x] Reuse or extract `NormalizeRoutePartyId`/`IsSafeRoutePartyId` behavior so edit ids reject unsafe route segments before any data call.

- [x] **Task 2 - Extend navigation and manifest constants** (AC1, AC2)
  - [x] Add route constants to `PartiesAdminPortalManifest`, for example `CreateRoute = "/admin/parties/new"` and `EditRoute = "/admin/parties/{partyId}/edit"`.
  - [x] Enable the existing disabled Edit action in `PartiesAdminPortal.razor` only when `_detail` is full, not erased, and the route id is safe.
  - [x] Add a Create action in the Admin toolbar using localized copy, without disrupting search/filter/paging layout.
  - [x] Cancel/Back returns to detail for edit and list for create, preserving the current focus contract where observable.

- [x] **Task 3 - Initialize edit state from `GetPartyAsync`** (AC6)
  - [x] Use `QueryService.ApiClient.GetPartyAsync(routePartyId, token)` for edit initialization.
  - [x] Reuse the Story 2.3 stale/degraded/partial/gone/forbidden privacy handling; do not display previous detail data after a failed edit load.
  - [x] Disable submit or affected field groups when only display-name-only partial data is available.
  - [x] Preserve cancellation/versioning patterns (`_detailVersion`, `_detailCts`, `_lifetimeCts`, tenant switch clearing) if the form is composed inside the existing component; if a separate component is used, implement equivalent route-load cancellation and disposal.

### Part B - Form model, validation, and commands - AC3, AC4, AC5, AC7, AC9

- [x] **Task 4 - Add a form model and explicit field mapping** (AC3, AC4, AC5)
  - [x] Create an AdminPortal-local form model; keep it separate from event contracts so UI validation, labels, and dirty-state handling do not leak into `Contracts`.
  - [x] Map create to `CreatePartyComposite` with `PartyId`, `Type`, `PersonDetails` or `OrganizationDetails`, `ContactChannels`, and `Identifiers`.
  - [x] Map edit to `UpdatePartyComposite` with `PartyId`, `PersonDetails` or `OrganizationDetails`, contact adds/updates/removals, and identifier adds/removals.
  - [x] Use route id as authoritative for edit command identity. Do not bind `PartyId` to a user-editable input.
  - [x] Generate command-side contact/identifier ids as GUID-form strings where validators require them.

- [x] **Task 5 - Add Fluent radio type chooser and field groups** (AC3, AC11)
  - [x] Use `FluentRadioGroup`/`FluentRadio` or the repo's established FluentUI wrapper so the rendered UI has radiogroup/radio semantics.
  - [x] Person fields cover at least first name, last name, prefix, suffix, and date of birth where supported by `PersonDetails`.
  - [x] Organization fields cover legal name, trading name, legal form, registration number, and natural-person flag where supported by `OrganizationDetails`.
  - [x] Contact and identifier inputs should match existing value-object enums and validation boundaries; keep the initial UI focused and limited if the current contracts make dynamic collection editing too large.
  - [x] Preserve user input when switching type; do not silently discard hidden field values until submit mapping chooses the selected type.

- [x] **Task 6 - Extend `IPartiesAdminPortalApiClient` for commands** (AC4, AC5, AC7, AC8, AC9)
  - [x] Add `CreatePartyCompositeAsync(CreatePartyComposite command, CancellationToken cancellationToken)` returning a safe AdminPortal command result.
  - [x] Add `UpdatePartyCompositeAsync(string partyId, UpdatePartyComposite command, CancellationToken cancellationToken)` returning a safe AdminPortal command result.
  - [x] Implement these in `PartiesAdminPortalApiClient` using `IPartiesCommandClient.CreatePartyCompositeWithResultAsync` and `IPartiesCommandClient.UpdatePartyCompositeWithResultAsync`.
  - [x] Keep the browser -> UI host -> typed client -> EventStore gateway boundary. Do not call `POST /process` or add new public endpoints.
  - [x] Convert `PartiesClientException` 400/422 into validation state without exposing `Detail` if it may contain PII.

- [x] **Task 7 - Map validation into accessible messages** (AC7)
  - [x] Use an `EditContext` plus `ValidationMessageStore` or an equivalent custom validator component to manage client and server errors.
  - [x] Clear field errors on field change and clear all server errors on resubmit.
  - [x] Map `PartyCommandValidationRejected.Failures[*].PropertyName` and `.ErrorCode` to localized, non-PII field copy. If only HTTP 422/problem details are available, show a generic safe validation message at form level.
  - [x] Render field messages with stable ids and bind controls through `aria-describedby`.
  - [x] Render validation summary or form-level failure with `role="alert"` and move focus to the first blocking alert or invalid field after submit failure.

### Part C - Optimistic UX, picker hook, and responsive behavior - AC8, AC10, AC11

- [x] **Task 8 - Implement accepted-processing status** (AC8, AC9)
  - [x] Add localized copy for `Saved - updating...`, `Create party`, `Save changes`, validation retry, and safe command failure states in `AdminPortalLabels`.
  - [x] On command accepted with no payload, update visible form/detail state optimistically and keep a polite status region.
  - [x] On command accepted with payload, normalize and render the returned `PartyDetail` through the existing detail semantics.
  - [x] Refresh or reconcile through existing projection/freshness mechanisms; do not implement bespoke polling that conflicts with Story 1.7/AR-D6.
  - [x] On create success, navigate to `/admin/parties/{newPartyId}` only if the generated id passes the same safe route-id rule.

- [x] **Task 9 - Add the related-party picker slot without reimplementing Story 2.5** (AC10)
  - [x] Add a form field/slot for related party selection using the existing `<hexalith-party-picker>` custom element only if the current package surface is already available to AdminPortal.
  - [x] Listen for `party-selected` and store `{partyId, partyType, status}` in form state.
  - [x] Honor degraded/local-only/gone status in copy and do not block form editing solely because picker search is degraded.
  - [x] Do not fork picker CSS, rebuild combobox behavior, or claim full WAI-ARIA combobox compliance in this story.

- [x] **Task 10 - Add scoped form styling** (AC11)
  - [x] Add `CreateEditPartyPage.razor.css` or extend the existing AdminPortal isolated stylesheet.
  - [x] Use Fluent/FrontComposer tokens; no hard-coded state colors and no raw teal fill.
  - [x] At 320px width and 200% zoom equivalent, form fields and actions wrap without page-level horizontal overflow.
  - [x] Keep focus-visible treatment intact and support forced-colors/reduced-motion if transitions are added.

### Part D - Tests and verification - AC1-AC12

- [x] **Task 11 - Extend bUnit AdminPortal coverage** (AC1-AC12)
  - [x] Add tests in `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` or a new form component test file.
  - [x] Extend `RecordingAdminPortalApiClient` with command capture for create/update and queued command results/failures.
  - [x] Assert unauthenticated, missing-tenant, and non-Admin create/edit routes make no command or detail calls.
  - [x] Assert `/admin/parties/new` does not load detail for party id `new`.
  - [x] Assert the radio group is discoverable by role/label and keyboard-selectable where bUnit can observe markup.
  - [x] Assert create command shape, edit route-id authority, validation mapping, input preservation, optimistic status, and safe failure copy.
  - [x] Keep existing Story 2.2/2.3 list/detail/GDPR regression tests green.

- [x] **Task 12 - Extend Playwright coverage** (AC3, AC7, AC8, AC11, AC12)
  - [x] Extend `tests/e2e/specs/admin-parties-list.spec.ts` or add `admin-party-form.spec.ts`.
  - [x] Extend `PartiesAdminPortalE2eFixture` to capture create/update requests and to return accepted, validation-rejected, and payload-success cases.
  - [x] Cover create happy path, edit happy path from detail Edit action, gateway validation failure with alert and preserved input, phone-width form, and 200% zoom equivalent.
  - [x] Keep the existing `parties-admin-e2e` cookie guard and reset route; do not make the fixture available outside Test environment.

- [x] **Task 13 - Run focused verification** (AC1-AC12)
  - [x] `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests`
  - [x] `cd tests/e2e && npm run typecheck`
  - [x] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` or the new form spec. (Attempted twice; blocked before tests by sandbox `SocketException (13): Permission denied` when Kestrel binds local webServer.)
  - [x] `bash scripts/check-no-warning-override.sh`

## Dev Notes

### Source Discovery Summary

- Loaded `epics_content` from `_bmad-output/planning-artifacts/epics.md`; Story 2.4 covers FR-Admin-3: create/edit form, validated command submit, Person/Organization radiogroup, gateway validation rejection, optimistic "Saved - updating..." and projection reconcile.
- Loaded `architecture_content` from `_bmad-output/planning-artifacts/architecture.md`; browser traffic stays inside the Blazor Server UI host, AdminPortal is an RCL, command flow is optimistic through typed clients to the EventStore gateway, rejection becomes assertive field validation, and projection confirm reconciles later.
- Loaded UX files under `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`; relevant constraints are Admin terse operator copy, real radiogroup semantics, validation `role=alert`, command result `role=status`, no color-only meaning, no raw 500/problem text, and 320px/200% zoom support.
- Loaded `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md`; it confirms Create/Edit party form is genuinely absent and should be built as the only true net-new Admin surface after list/detail reuse.
- Loaded persistent project context from `_bmad-output/project-context.md`; .NET 10, `.slnx`, Central Package Management, `TreatWarningsAsErrors`, xUnit v3, Shouldly, NSubstitute, bUnit, Playwright, and gateway-boundary rules apply.
- Reviewed previous story `_bmad-output/implementation-artifacts/2-3-party-detail-fr-admin-2.md`; Story 2.3 completed detail in `PartiesAdminPortal.razor`, established disabled Edit entry, phone sheet/focus behavior, detail freshness/state semantics, and direct xUnit executable fallback.
- Reviewed current source in `src/Hexalith.Parties.AdminPortal`, typed command/query clients in `src/Hexalith.Parties.Client`, contracts in `src/Hexalith.Parties.Contracts`, validators in `src/Hexalith.Parties/Validation`, E2E fixture in `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`, and AdminPortal tests.
- Reviewed recent git history: `b7303af feat(story-2.3): Party detail (FR-Admin-2)`, `da6bfcf feat(story-2.2): Parties list with search, filters, and paging`, `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`.

### Existing Code to Reuse

- Reuse `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` for browse/detail/GDPR behavior and as the source for safe-route, cancellation, detail-normalization, freshness, focus, and privacy patterns.
- Reuse `AdminPortalPartyQueryService`, `IPartiesAdminPortalApiClient`, `PartiesAdminPortalApiClient`, `AdminPortalQueryResult<T>`, `AdminPortalQueryMetadata`, `AdminPortalQueryException`, `AdminPortalQueryFailureKind`, `PartiesAdminListCoordinator`, and `AdminPortalGdprStateCoordinator`.
- Reuse `IPartiesCommandClient.CreatePartyCompositeWithResultAsync` and `IPartiesCommandClient.UpdatePartyCompositeWithResultAsync` instead of composing low-level EventStore requests.
- Reuse contracts: `CreatePartyComposite`, `UpdatePartyComposite`, `PersonDetails`, `OrganizationDetails`, `AddContactChannel`, `UpdateContactChannel`, `AddIdentifier`, `PartyDetail`, `PartyType`, `ContactChannelType`, and `IdentifierType`.
- Reuse validation constraints from `CreatePartyCompositeValidator` and `UpdatePartyCompositeValidator`: GUID-form ids, valid `PartyType`, required person details for Person, required organization details for Organization, and bounded sub-operation count.
- Reuse command result type `PartiesCommandResult<PartyDetail>` and exception type `PartiesClientException`; treat 400/422 as safe validation state, not fatal UI crashes.
- Reuse `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` for bUnit command capture by extending it instead of introducing a second fake.
- Reuse `PartiesAdminPortalE2eFixture` and existing Playwright Admin fixture cookie/reset flow for browser tests.

### Current Files Being Modified

| File | Current state | Story change | Preserve |
|---|---|---|---|
| `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` | Single routable list/detail/GDPR component. Has disabled Edit button and unavailable copy. Owns safe route-id handling, list/detail cancellation, detail privacy, phone sheet/focus. | Enable Edit navigation, add Create toolbar action, optionally compose shared form subcomponent, and preserve existing browse/detail/GDPR behavior. | Existing `@page` routes, `[Authorize(Policy = "Admin")]`, `IAdminPortalAuthorizationService`, route-id safety, tenant-switch clearing, GDPR panel composition, no scoped id display. |
| `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor` | Does not exist. | Add create/edit route component if using dedicated page design. | Must live in AdminPortal RCL and use Admin policy/auth service. |
| `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor.css` or existing AdminPortal stylesheet | Form styles absent. | Add responsive one-column/zoom-safe form styling. | Fluent/FrontComposer tokens, forced-colors/reduced-motion safety, no hard-coded status colors. |
| `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` | Centralized AdminPortal copy for list/detail/GDPR, including disabled edit text. | Add create/edit labels, validation messages, command status, retry, field names, and safe failure copy. | Keep user-facing copy out of markup where practical; no PII/raw backend copy. |
| `src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs` | Read/search/detail and GDPR operations only. | Add create/update composite command methods returning a safe AdminPortal command result. | Existing tests/fakes continue to compile; no browser-visible endpoint. |
| `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` | Uses typed query client and GDPR client; no command client. | Inject/use `IPartiesCommandClient` and map command results/failures safely. | Existing query/GDPR behavior and capability probing. |
| `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs` | Route constants for list/detail/GDPR only. | Add create/edit route constants. | Existing route constants remain stable. |
| `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` | Captures list/search/detail requests; provides fixed fixture details. | Extend capture and fixture responses for create/update/validation browser tests. | Cookie guard, Test-environment-only enablement, reset/snapshot routes. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Components/*` | Existing bUnit coverage for auth, list, detail, GDPR, privacy, focus. | Add form route, validation, command, radio, optimistic, and regression tests. | Existing Story 2.1-2.3 tests green. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` | Captures read/GDPR calls. | Add create/update command captures and queued command results/failures. | Do not silently coerce ids or metadata in test fake. |
| `tests/e2e/specs/admin-parties-list.spec.ts` or new `admin-party-form.spec.ts` | List/detail browser coverage and phone detail tests. | Add create/edit form E2E coverage. | Existing list/detail tests stable and serial fixture reset behavior intact. |

Do not modify EventStore/Tenants submodules, Parties actor host public API surface, DAPR access control, OIDC/sign-in flow, Consumer self-scope code, GDPR verification stubs, deployment manifests, or the full picker re-skin/combobox implementation for this story.

### Technical Requirements

- Create/edit commands must go through `IPartiesCommandClient` via `PartiesAdminPortalApiClient`; the browser talks only to the UI host.
- Use `CreatePartyComposite` for create and `UpdatePartyComposite` for edit. Do not hand-roll JSON command envelopes in the UI.
- For create, generate a GUID-form `PartyId` because current composite validators require GUID strings. For edit, route id is authoritative even if it is later rejected by backend validation.
- Do not expose a user-editable party id field.
- Do not log or render raw form payloads, contact values, identifier values, tenant ids, exception details, stack traces, or problem-details text.
- Validation event `PartyCommandValidationRejected` carries property names and error codes only by design. Map those to local safe messages; never depend on backend validator messages containing user-safe copy.
- Use `EditContext`/`ValidationMessageStore` or a local custom validator component for server-side validation errors. Microsoft's .NET 10 Blazor forms guidance supports `EditForm` with `EditContext`, custom `ValidationMessageStore`, field clearing, and `FormName`; use those patterns for interactive forms. [Source: Microsoft Learn, ASP.NET Core Blazor forms validation]
- `FluentRadio` is intended for use inside `FluentRadioGroup`; use those components or the repo's established wrapper for the Person/Organization chooser. [Source: Fluent UI Blazor Radio documentation]
- Keep component event handlers as `Task`/`ValueTask`, not `async void`.
- Do not add package `Version=` attributes; versions remain in `Directory.Packages.props`.

### Architecture Compliance

- **AR-D4 composition:** AdminPortal remains the Admin RCL embedded by `Hexalith.Parties.UI`; create/edit lives there.
- **AR-D5 transport:** UI host/BFF talks to EventStore gateway via typed Parties client; no actor-host direct calls.
- **AR-D6 live freshness:** accepted-processing state reconciles through existing freshness/projection mechanisms, not bespoke polling.
- **AR-StatusMap / UX state patterns:** validation/failure uses assertive `role=alert`; accepted-processing uses polite `role=status`.
- **NFR2:** do not assume read-your-write. Optimistic echo is required until projection confirm.
- **NFR3:** no PII in logs, validation failures, unauthorized states, gone/erased copy, or telemetry.
- **NFR5 / UX-DR10:** Admin forms must reflow at phone width and 200% zoom without overlap or horizontal page overflow.
- **NFR7:** use FluentUI V5 and FrontComposer tokens; no hard-coded state colors.
- **NFR9:** .NET 10, `.slnx`, Central Package Management, warnings-as-errors, xUnit v3, Shouldly, NSubstitute, bUnit, and Playwright apply.

### Previous Story Intelligence

- Story 2.3 is done at commit `b7303af` and left Edit disabled in `PartiesAdminPortal.razor`; this story should enable that existing entry, not create a competing detail surface.
- Story 2.3 added `PartiesAdminPortal.razor.css`, phone detail sheet/full-screen behavior, detail heading focus, Back to list focus restore, active row non-color cue, badge semantics, freshness semantics, and partial-detail honesty. Preserve all of it.
- Story 2.2 established debounced search, server-side filters, paging preservation, degraded last-known list rows, keyboard row activation, and Test-environment Admin E2E fixture coverage.
- Story 2.2 and 2.3 tests use a direct xUnit v3 executable fallback because `dotnet test --filter` can be unreliable under MTP in this repo.
- Existing MCP tests already prove important command behavior: route id is authoritative for updates, partial patches merge from current detail where needed, invalid payloads fail before client calls, oversized data avoids PII echo, and gateway validation failures must not expose problem details.

### Git Intelligence Summary

- Recent commits:
  - `b7303af feat(story-2.3): Party detail (FR-Admin-2)`
  - `da6bfcf feat(story-2.2): Parties list with search, filters, and paging`
  - `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`
  - `78a5956 docs(epic-1): Add retrospective and sync project docs`
  - `ac523f0 feat(story-1.10): Deploy parties-ui container/K8s with production KMS gate`
- Pattern: narrow changes, reuse AdminPortal surfaces, extend existing fakes/fixtures, bUnit for component contracts, Playwright for browser layout/focus, direct xUnit executable fallback, and no package/build-gate drift.

### Latest Technical Information

- Microsoft Learn's .NET 10 Blazor forms binding guidance says `EditForm` can bind through either `Model` or `EditContext`, and assigning both is a runtime error. It also recommends unique `FormName` values for forms to prevent runtime form posting issues if interactivity is unavailable. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/binding?view=aspnetcore-10.0]
- Microsoft Learn's .NET 10 Blazor forms validation guidance documents `EditContext`, `ValidationMessageStore`, custom validation, clearing messages on validation/field change, and validator components. Use that for gateway validation mapping instead of ad hoc DOM-only errors. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-10.0]
- Fluent UI Blazor's Radio documentation describes `FluentRadio` as used inside `FluentRadioGroup` and exposes parameters including `AriaLabel`, `Checked`, `Disabled`, `Name`, `Required`, and `Value`. Use the Fluent components for the Person/Organization chooser. [Source: https://fluentui-blazor.azurewebsites.net/Radio]
- Playwright's input/action guidance supports `locator.fill`, radio `check`, and role/label-based interactions. Use role/label locators for form E2E tests instead of CSS-only selectors where possible. [Source: https://playwright.dev/docs/input]

### Project Structure Notes

- Alignment: `Hexalith.Parties.AdminPortal` owns Admin records management UI.
- Alignment: `Hexalith.Parties.UI` owns the host shell and Test-environment AdminPortal E2E fixture, but form behavior belongs in the AdminPortal RCL.
- Detected conflict: `architecture.md` still sketches separate `PartiesListPage.razor`/`PartyDetailPage.razor` pages, but live code and readiness corrections use `PartiesAdminPortal.razor` for browse/detail/GDPR. Follow live code for existing surfaces; adding a new create/edit component is acceptable because FR-Admin-3 is confirmed absent.
- Detected route risk: `/admin/parties/new` can be misread by the existing `/{RoutePartyId}` detail route if the literal create route is not explicit. Tests must pin this.
- Detected command surface gap: `IPartiesAdminPortalApiClient` has no create/update methods yet. Extend it rather than injecting `IPartiesCommandClient` directly into Razor.
- Detected validation gap: gateway validation may arrive as `PartyCommandValidationRejected` or as `PartiesClientException` 422/problem details depending on adapter path. The UI must safely handle both without PII echo.
- Detected picker sequencing: Story 2.5 owns full picker re-skin and WAI-ARIA combobox compliance. Story 2.4 should prepare/bind the form slot without forking picker internals.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.4: Create and edit a party with validation (FR-Admin-3)`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Admin - Party Records Management`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Integration Points & Data Flow`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State Patterns`]
- [Source: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md#Findings (reuse / boilerplate lens)`]
- [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs`]
- [Source: `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/CreatePartyComposite.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Events/PartyCommandValidationRejected.cs`]
- [Source: `src/Hexalith.Parties/Validation/CreatePartyCompositeValidator.cs`]
- [Source: `src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs`]
- [Source: `_bmad-output/implementation-artifacts/2-3-party-detail-fr-admin-2.md`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: Implemented create/edit AdminPortal form route, command adapter methods, route helper, validation mapping, optimistic accepted status, related-party picker bridge, responsive styles, bUnit coverage, E2E fixture/spec coverage, and regression updates.
- 2026-06-10: Playwright command `npx playwright test specs/admin-parties-list.spec.ts --project=chromium` attempted twice from `tests/e2e`; both attempts failed before test execution because Kestrel could not bind the local socket in this sandbox (`System.Net.Sockets.SocketException (13): Permission denied`).

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Validation checklist applied during story creation; critical route, command, validation, privacy, and regression risks are represented in acceptance criteria and tasks.
- Added `CreateEditPartyPage` under the AdminPortal RCL with protected create/edit routes, shared safe route-id handling, edit initialization through `QueryService.ApiClient.GetPartyAsync`, `EditContext`/`ValidationMessageStore` validation, safe alert/status regions, Fluent radio chooser, person/organization field groups, bounded contact/identifier inputs, optimistic accepted status, and safe navigation.
- Extended the AdminPortal API adapter with safe create/update composite command results over `IPartiesCommandClient`; no browser-callable endpoints or actor-host direct calls were added.
- Enabled browse/detail Create/Edit navigation while preserving existing list/detail/GDPR routes and regression coverage.
- Extended bUnit tests and fakes for route auth, create command shape, edit route-id authority, client/gateway validation, optimistic behavior, command adapter mapping, and Story 2.1-2.3 regressions.
- Extended Playwright fixture/spec coverage for create/edit/validation/mobile/zoom scenarios; browser execution is blocked in this sandbox by local socket permission, while TypeScript typecheck passes.

### File List

- `_bmad-output/implementation-artifacts/2-4-create-and-edit-a-party-with-validation-fr-admin-3.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor`
- `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor.css`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalCommandResult.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalRouteIds.cs`
- `src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs`
- `src/Hexalith.Parties.AdminPortal/wwwroot/party-form-picker.js`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/CreateEditPartyPageTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalAuthorizationTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- `tests/e2e/specs/admin-parties-list.spec.ts`

### Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

Outcome: Approved after automatic fixes.

Findings fixed:

- HIGH: `CreateEditPartyPage` loaded only during initialization, so Blazor route reuse could leave stale edit form data when `RoutePartyId` changed on the same component instance. Fixed by moving route-sensitive loading to `OnParametersSetAsync`, tracking the loaded route key, and resetting form/model state before each distinct route load.
- HIGH: related-party picker attachment ran only on first render, which can happen before the form and `<hexalith-party-picker>` element exist. Fixed by attaching once the form is renderable and reattaching after route resets.
- MEDIUM: missing typed command-client configuration could throw through submit instead of returning safe in-form command failure copy. Fixed `PartiesAdminPortalApiClient` to return `ContractUnavailable` as a bounded `AdminPortalCommandResult`.

Verification:

- `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` passed.
- `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` passed: 140 tests, 0 failed.

### Change Log

- 2026-06-10: Added protected AdminPortal create/edit party form routes with validation and optimistic command UX.
- 2026-06-10: Added AdminPortal command adapter methods for create/update composite commands and safe command result mapping.
- 2026-06-10: Enabled Admin toolbar Create and full-detail Edit navigation while preserving browse/detail/GDPR behavior.
- 2026-06-10: Added bUnit and Playwright fixture/spec coverage for form workflows and updated focused verification results.
- 2026-06-10: Senior review fixed route-parameter reload/reset, deferred related-party picker attachment, and safe command-client-unavailable handling.
