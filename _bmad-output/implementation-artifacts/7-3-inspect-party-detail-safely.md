# Story 7.3: Inspect Party Detail Safely

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Parties administrator,
I want to inspect a selected party's details in the admin console,
so that I can understand its current record, contacts, identifiers, and compliance state.

## Acceptance Criteria

1. Given an administrator selects a party row, when the detail panel loads, then it displays selected party summary, contacts, identifiers, consent, restriction, erasure status, processing records, and safe EventStore Admin UI links where available, and the detail region remains a dense titled working region, not a decorative card shell.
2. Given a party is active, inactive, restricted, or erased, when the detail panel renders, then it shows the correct state with non-color-only indicators, and erased parties show terminal privacy-preserving state only.
3. Given the selected party is not found, forbidden, cross-tenant, gone/erased, timeout, malformed, or contract-unavailable, when the detail load resolves, then sensitive detail state is cleared before rendering the status, and stale personal detail is not retained in the UI.
4. Given the administrator signs out, changes tenant, loses admin permission, or changes route context, when context changes, then list/detail/GDPR/export state is cleared or reloaded according to the current context, and in-flight detail responses are canceled or suppressed when stale.
5. Given backend content, AI-created party data, operator-entered text, or ProblemDetails text is displayed, when the detail panel renders it, then all content is encoded through normal Razor/component text paths, and raw markup, raw HTML fragments, and JavaScript interpolation are not used.
6. Given detail UI tests run, when they cover selection, state indicators, erased state, forbidden/cross-tenant, malformed responses, tenant switch, sign-out, and encoded rendering, then the detail panel remains safe and predictable.

## Tasks / Subtasks

- [x] Preserve and harden the existing dense detail working region. (AC: 1, 2)
  - [x] Keep the existing sibling list/detail layout inside `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`; do not introduce a landing page, hero, decorative card shell, nested card layout, or separate admin SPA.
  - [x] Preserve summary, person/organization details, contact channels, identifiers, consent records, restrictions, GDPR operations, EventStore links, system metadata, and name history sections.
  - [x] Ensure active, inactive, restricted, and erased states have visible text and accessible names; do not rely on color-only badges.
  - [x] Ensure erased party detail responses show only terminal privacy-preserving state and do not leave stale detail, contact, identifier, consent, processing, export, or route-derived content visible.

- [x] Keep detail reads on the accepted Parties client/query boundary. (AC: 1, 3)
  - [x] Continue detail reads through `IPartiesQueryClient.GetPartyAsync` when registered, with `IQueryService` FrontComposer fallback in `PartiesAdminPortalApiClient.GetPartyAsync`.
  - [x] Preserve `PartyDetailProjectionActor`/`GetParty` query settings from `PartiesAdminPortalOptions`; do not call retired `api/v1/parties`, `api/v1/admin/**`, DAPR actors, projection actors, local caches, or EventStore internals directly from the portal component.
  - [x] Preserve `NormalizeDetail` null-collection protection in both the API client and component so malformed or partial detail payloads cannot crash rendering.
  - [x] Keep safe cache discriminator behavior; never include tenant ids, display names, emails, identifiers, consent text, ProblemDetails text, JWTs, claims, or raw query payloads in storage keys or telemetry dimensions.

- [x] Fail closed for unsafe detail states and context changes. (AC: 3, 4)
  - [x] Verify not-found and gone/erased responses remove or neutralize the selected row and clear `_detail` before rendering bounded status text.
  - [x] Verify forbidden and cross-tenant detail failures clear `_detail` and avoid showing scoped ids or stale personal detail while preserving safe browse context when appropriate.
  - [x] Verify authentication loss, tenant loss, non-admin authorization, tenant switch, and route changes increment/cancel list/detail versions and reset `AdminPortalPartyQueryService`, `PartiesAdminListCoordinator`, and `AdminPortalGdprStateCoordinator` consistently.
  - [x] Verify delayed previous-tenant or previous-route detail responses cannot write back into `_detail`, `_detailMetadata`, selected-row state, GDPR/export state, or visible status after the context has changed.

- [x] Preserve privacy-safe rendering and EventStore Admin UI delegation. (AC: 1, 5)
  - [x] Continue rendering all party, contact, identifier, consent, name-history, processing, export, and error/status text through normal Razor/component text encoding.
  - [x] Do not use `MarkupString`, `AddMarkupContent`, `innerHTML`, JavaScript interpolation, raw HTML fragments, or unencoded attributes for any party data or backend-provided text.
  - [x] Keep EventStore Admin UI links generated only through `AdminPortalEventStoreAdminLinks`; link labels must stay generic and URLs must use safe identifiers only.
  - [x] If EventStore Admin UI base address is unavailable, render the existing bounded disabled state instead of embedding or recreating a stream browser.

- [x] Add or strengthen focused tests for detail safety. (AC: 1-6)
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` to cover active, inactive, restricted, erased terminal state, not-found/gone, forbidden/cross-tenant, malformed/contract-unavailable detail failures, route selection changes, sign-out, tenant switch, and stale detail response suppression.
  - [x] Preserve existing tests for `PartiesAdminPortal_SelectParty_HydratesDetailAndEncodesUntrustedFields`, `PartiesAdminPortal_SelectParty_RendersRestrictionsSystemMetadataNameHistoryAndStaleAge`, EventStore link safety, GDPR coordinator reset, route hardening, and localized date/boolean rendering.
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` only if detail failure mapping, normalization, or query contract behavior changes.
  - [x] Add or retain source guardrails that prevent raw markup APIs and retired admin endpoints from entering the admin portal.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release` if client/query contract or source guardrails change.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.3 to `7-3-inspect-party-detail-safely`; this supersedes the older historical file `7-3-handler-patterns-documentation-and-dangling-reference-guidance.md`, which belongs to a prior plan and must not be used as the current story source.
- Epic 7 objective: administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 7: Administration Console`]
- Story 7.3 covers FR65 and UX-DR6, UX-DR9, UX-DR20, UX-DR21. [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.3: Inspect Party Detail Safely`]
- Detail projection architecture uses a per-party `PartyDetailProjectionActor` and per-tenant `PartyIndexProjectionActor`; the portal should consume query/client abstractions, not actor internals. [Source: `_bmad-output/planning-artifacts/architecture.md#D4 — Projection Actor Granularity: Hybrid (Per-Party Detail + Per-Tenant Index)`]

### Architecture Guardrails

- The Parties Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor. It must register Parties-domain views with the FrontComposer shell, read through EventStore query/client abstractions, route supported commands through the typed Parties client/EventStore command boundary, and delegate generic event/stream browsing to EventStore Admin UI safe deep-links. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- The portal must fail closed and clear sensitive state on sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- The party picker/admin UI stale-response rule applies here: token, tenant, user, host configuration, selected id, or route changes must suppress stale responses and handle loading, empty, retry, unauthorized, forbidden, not-found, gone/erased, and transient-failure states without leaking personal data. [Source: `_bmad-output/planning-artifacts/architecture.md#D20 — FrontComposer Party Picker and Admin Portal UX Contracts`]
- EventStore Admin UI links must use only safe identifiers and generic labels; if the Admin UI base address is unavailable, render a disabled bounded state instead of embedding a generic stream browser. [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md#EventStore Admin UI Boundary`]
- Labels, dates, counts, status messages, validation messages, operation outcomes, focus behavior, keyboard access, non-color-only state, and polite status announcements are part of the frontend architecture contract. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]

### Current Implementation to Preserve

`src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`

- Current state: renders `@page "/admin/parties"`, `@page "/admin/parties/{RoutePartyId}"`, and `@page "/admin/parties/{RoutePartyId}/gdpr"` as a working console with toolbar, grid, paging, status region, and sibling detail region.
- Current state: `SelectPartyAsync` uses `SwapDetailCts`, `_detailVersion`, `_isDetailLoading`, and `QueryService.ApiClient.GetPartyAsync` to load details and suppress stale responses.
- Current state: detail rendering includes summary, person details, organization details, contact channels, identifiers, consent records, restrictions, `PartyGdprOperationsPanel`, EventStore links, system metadata, and name history.
- Current state: route handling rejects unsafe route ids, navigates only with bounded non-PII route tokens, and does not render scoped ids as user-facing labels.
- Preserve: encoded Razor text rendering, `NormalizeDetail` null-collection protection, route hardening from Story 7.1, date filters from Story 7.2, cancellation/version guards, tenant/auth reset behavior, GDPR coordinator reset behavior, status announcements, and keyboard handling.

`src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`

- Current state: prefers `IPartiesQueryClient` for list/search/detail, falls back to FrontComposer `IQueryService`, maps transport and ProblemDetails failures to bounded `AdminPortalQueryFailureKind`, and routes GDPR operations through `IAdminPortalGdprClient`.
- Current state: `GetPartyAsync` uses `DetailProjectionType`, `DetailQueryType`, `DetailProjectionActorType`, and `DetailCacheDiscriminator`; it throws bounded failures for missing contract, empty detail, multiple detail items, transport failure, validation, forbidden, not found, gone, and transient states.
- Preserve: typed client preference, FrontComposer fallback, bounded failure mapping, null-collection normalization, and redaction of raw ProblemDetails details.

`tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`

- Current state: covers detail hydration and encoding, restriction/system metadata/name history, GDPR operations surface, EventStore link safety, unavailable EventStore UI state, GDPR coordinator reset, tenant-switch stale list suppression, detail gone, detail forbidden, cross-tenant scoped id failure, localization, keyboard reachability, coordinator transitions, route variants, unsafe route ids, and browse/filter behavior.
- Story change: strengthen detail-specific coverage for erased terminal detail payloads, stale detail response suppression after route/tenant/auth changes, malformed and contract-unavailable detail failures, and non-color-only state indicators.
- Preserve: scenario breadth; do not weaken privacy, accessibility, route-hardening, localization, tenant-isolation, GDPR, EventStore link, or transport guardrail tests.

### Previous Story Learnings

- Story 7.1 completed the operational first viewport and route support for `/admin/parties/{partyId}` and `/admin/parties/{partyId}/gdpr`; selected rows navigate only for bounded non-PII route tokens, while tenant-scoped ids stay in in-page detail state. Preserve this route hardening. [Source: `_bmad-output/implementation-artifacts/7-1-compose-admin-console-working-view.md`]
- Story 7.2 added created/modified date filters, bounded validation, and stronger browse/filter tests; preserve those controls and do not weaken search-mode unsupported-filter disabling. [Source: `_bmad-output/implementation-artifacts/7-2-browse-and-filter-party-results.md`]
- Story 10.1 established the read-only admin browse/search/detail foundation and recorded that the portal must not add tenant lifecycle, tenant management, or GDPR mutation scope into detail inspection work. Preserve that boundary. [Source: `_bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md`]
- Story 10.1.1 completed Tenants-backed authorization derivation, rich-search capability detection, Fluent UI migration, and coordinator/query-service lifecycle wiring. Do not regress to host-supplied auth booleans, raw HTML controls, or dead-code state services. [Source: `_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md`]
- Story 12.7 rebuilt the Admin Portal around FrontComposer/EventStore query boundaries and EventStore Admin UI deep-links. Preserve typed `IPartiesQueryClient` preference, `IQueryService` fallback, `IAdminPortalGdprClient` operations, safe link generation, and the existing UX specification. [Source: `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`]

### Anti-Patterns to Avoid

- Do not build a TypeScript SPA, standalone admin shell, duplicate tenant selector, duplicate authorization model, generic stream/event browser, or local detail cache inside Parties.
- Do not query retired REST paths, admin endpoints, DAPR actor internals, EventStore streams, or projection actors directly from the portal component.
- Do not add client-side filtering or client-side redaction to compensate for tenant isolation, erased-record handling, or detail projection behavior.
- Do not render party data, ProblemDetails text, consent text, processing summaries, operator-entered values, or backend-provided text through raw markup APIs.
- Do not put names, emails, identifiers, consent text, free text, raw errors, tenant ids, tokens, claim values, route fragments, or search text in URLs, storage keys, telemetry dimensions, filenames, logs, or EventStore Admin UI link labels.
- Do not weaken existing bUnit, privacy, accessibility, localization, route-hardening, tenant-switch, stale-response, or transport guardrail tests to make detail work easier.

### Testing

- Primary test file: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- API/client boundary test file: `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- Existing fake API: `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- Validation commands:
  - `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`
  - `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created ready-for-dev story context for current Epic 7 Story 7.3 and reconciled it with existing AdminPortal detail implementation history. | Codex |
| 2026-05-22 | 0.2 | Added focused detail safety regression coverage for state text, erased terminal payloads, bounded detail failures, and stale tenant detail suppression; moved story to review. | Codex |
| 2026-05-22 | 0.3 | Senior Developer Review (AI): all 6 ACs verified against implementation, all [x] tasks confirmed, file list matches git, 82/82 AdminPortal tests pass, 0 critical issues. Status → done. | Jérôme Piquot |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Resolved current sprint status and confirmed story key `7-3-inspect-party-detail-safely`.
- Confirmed historical `7-3-handler-patterns-documentation-and-dangling-reference-guidance.md` exists from a prior plan and is not the current story source.
- Loaded current Epic 7 Story 7.3 source, frontend architecture D20, UX admin portal references, Story 7.1 and 7.2 completion notes, Story 10.1/10.1.1/12.7 admin portal history, and current AdminPortal component/API-client/test references.
- Skipped web research; local project versions and existing Fluent UI component usage are authoritative for this story.
- Added delayed detail response support to the AdminPortal test fake so stale detail cancellation/suppression can be tested directly.
- Added focused component tests for non-color-only detail state text, erased detail terminal behavior, contract-unavailable and malformed detail failures, and tenant-switch cancellation of delayed detail loads.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed with 82/82 tests.
- Validation: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story status set to `ready-for-dev`.
- Sprint status updated for current story key `7-3-inspect-party-detail-safely`.
- Checklist review applied: story includes specific acceptance criteria, current files to inspect, preserve/change guidance, anti-patterns, validation commands, source references, and the historical 7.3 artifact warning.
- Strengthened existing detail safety coverage without changing production component behavior; current implementation already satisfied the newly pinned safety paths.
- Story moved to `review` after AdminPortal tests and Release solution build passed.

### File List

- `_bmad-output/implementation-artifacts/7-3-inspect-party-detail-safely.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
