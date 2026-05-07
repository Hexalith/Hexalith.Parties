# Story 10.1: Admin Portal - Browse, Search, and Inspect

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want a web-based admin portal to browse, search, and inspect party records,
so that I can manage party data without using API tools or CLI commands.

## Acceptance Criteria

1. Given an authenticated administrator, when they access the admin portal, then they can browse a paginated list of parties for the active tenant.
2. Given the party list, when the administrator searches by display name, then baseline name search returns party matches with existing Parties search metadata and pagination.
3. Given optional rich search capability is enabled by Story 9.6 and the portal can deterministically detect that the backend capability is available and healthy, when the administrator searches by email or identifier, then the portal exposes that capability only through the approved Parties search API; otherwise the UI remains display-name-only and clearly handles degraded/local-only search status without emulating rich search client-side.
4. Given party list filters, when the administrator filters by party type or active party lifecycle status, then the list updates only within the currently authorized tenant scope without leaking counts, identifiers, display names, existence, parties from other tenants, or erased parties.
5. Given a party in the list, when the administrator opens it, then the portal displays full party detail: party type, person or organization details, contact channels, identifiers, consent records when available, active/inactive state, created date, and last modified date.
6. Given rendered party fields may contain user-supplied or AI-created data, when the portal displays them, then all values are output encoded as text and no raw HTML, `MarkupString`, script injection, or stored XSS path is introduced.
7. Given an administrator lacks a valid token, tenant claim, or admin role, when the portal loads data or tenant context changes, then the UI clears list/search/detail state, ignores in-flight responses from the previous tenant, and surfaces bounded unauthorized/forbidden states without showing cached or cross-tenant party data.
8. Given the portal architecture is reviewed, when implementation is complete, then it uses the existing Hexalith.FrontComposer Blazor/Fluent UI shell and composable components rather than building a separate TypeScript SPA, routing root, tenant selector, authorization model, or duplicate tenant-management UI.

## Party-Mode Clarifications

- Story 10.1 is read-only. It must not add create, edit, delete, activate, invite, assign-role, tenant lifecycle, tenant configuration, export, erasure, restriction, consent mutation, GDPR operation, or Memories-backed search management workflows.
- The only required data sources are existing Parties APIs: `GET /api/v1/parties`, `GET /api/v1/parties/search`, and `GET /api/v1/parties/{id}`. The portal must not introduce new browse/search/detail endpoints to satisfy this story.
- Baseline search means display-name search through the approved search API and whatever metadata that API already returns. Rich email/identifier search remains optional until Story 9.6 exposes a stable capability signal and healthy response path.
- Tenant context and admin role state come from the existing authenticated app/API context and Hexalith.Tenants integration. This story must not infer tenant authority from client-side parsing or invent role/membership semantics.
- Browser storage may persist only non-sensitive UI preferences such as page size when this matches FrontComposer conventions. Storage keys, routes, logs, telemetry dimensions, and test output must not contain tenant ids, party ids, display names, emails, identifiers, search text, JWTs, claims, or consent details unless an explicit later architecture decision allows it.
- Detail rendering order should be predictable: summary, party type/status, contact channels, identifiers, consent records, restrictions/erasure state, timestamps, and system metadata. Stale, not-found, forbidden, and gone/erased detail outcomes must leave browse context intact while clearing the selected detail data.

### UX State Matrix

| State | Grid behavior | Detail behavior | User-facing intent | Allowed actions |
| --- | --- | --- | --- | --- |
| Loading | Show loading state without stale tenant rows | Disable or clear detail until current-tenant data arrives | Work is in progress | Cancel via navigation only |
| No parties | Empty grid | No selected detail | No records exist for the tenant | Refresh |
| No filtered/search results | Empty grid scoped to current query | No selected detail | No parties match current filters/search | Clear filters/search |
| Display-name-only / rich search unavailable | Show display-name results only | Detail remains authoritative by id | Rich search is unavailable or degraded | Continue display-name search, refresh |
| Stale/degraded projection | Show bounded warning from response headers | Show bounded stale indicator when detail is affected | Data may lag but remains inspectable | Refresh |
| Unauthorized or missing token | Clear grid rows | Clear and disable detail | Sign-in is required | Sign in/retry where FrontComposer supports it |
| Missing tenant | Clear grid rows | Clear and disable detail | Tenant context is unavailable | Change/select tenant through existing Tenants UX |
| Forbidden/cross-tenant | Clear affected data without existence leak | Clear selected detail | Access is denied | Return to browse/retry |
| Not found | Keep browse context | Show bounded not-found state without raw ProblemDetails | The selected record is unavailable in this scope | Return to browse/refresh |
| Gone/erased | Remove affected row when safe | Show bounded gone/erased state | The party is erased or no longer inspectable | Return to browse/refresh |
| Transient failure | Keep only current-tenant non-sensitive shell state | Clear or keep disabled detail based on failed request scope | Retryable failure | Retry |

## Tasks / Subtasks

- [x] Add Parties admin portal composition on top of Hexalith.FrontComposer (AC: 1, 8)
  - [x] Add the minimum Parties-specific frontend project/package wiring needed to host or register a Parties admin capability against `Hexalith.FrontComposer`.
  - [x] Reuse FrontComposer shell services, navigation, density, DataGrid, query transport, command feedback, auth redirect, and localization seams where available.
  - [x] Do not create a standalone SPA that bypasses FrontComposer, unless an explicit architecture decision replaces the current checked-out submodule direction.
  - [x] Do not add tenant lifecycle, membership, role, or configuration management screens in Parties; Hexalith.Tenants owns those capabilities.

- [x] Register the party browse view and route (AC: 1, 4, 8)
  - [x] Create a Parties admin route such as `/admin/parties` or the existing FrontComposer route convention for the Parties bounded context.
  - [x] Render party rows from `PartyIndexEntry` with columns for display name, party type, active state, created date, and last modified date.
  - [x] Use stable row keys based on party id; do not display or parse `tenant:domain:aggregateId` scoped ids as user-facing labels.
  - [x] Keep the first screen dense and operational: no marketing page, hero section, or decorative shell.

- [x] Wire list query behavior to existing Parties REST/search APIs (AC: 1, 2, 3, 4)
  - [x] Use `GET /api/v1/parties` for baseline paginated browse with `page`, `pageSize`, `type`, `active`, `createdAfter`, `createdBefore`, `modifiedAfter`, and `modifiedBefore` query parameters where supported.
  - [x] Use `GET /api/v1/parties/search` for display-name search and for rich search modes introduced by Story 9.6.
  - [x] Preserve response metadata headers such as `X-Service-Degraded`, `X-Stale-Data-Age`, `X-Parties-Search-Status`, and `X-Parties-Search-Degraded-Reason` in a visible but bounded UI state.
  - [x] Treat empty search text as list/browse behavior, matching `PartiesController.SearchPartiesAsync`.
  - [x] Keep email/identifier search disabled or clearly local-only unless the Story 9.6 rich search path is enabled, healthy, and explicitly detectable; do not emulate email/identifier search client-side.
  - [x] Ignore or cancel in-flight list/search responses from a previous tenant after tenant context changes.

- [x] Add filters, paging, and empty/error states (AC: 1, 2, 3, 4, 7)
  - [x] Provide type filter options for `Person` and `Organization`.
  - [x] Provide active status filter options for active and inactive parties.
  - [x] Preserve pagination state and page-size bounds; do not allow client page sizes above the API cap of 100.
  - [x] Show distinct states for zero parties, zero filtered results, degraded search, unauthorized, forbidden, and transient query failure.
  - [x] Do not use cached rows after `401`, `403`, missing tenant, or tenant-switch failures.
  - [x] Ensure forbidden and cross-tenant failures do not reveal record existence, counts, identifiers, display names, or previous detail values.

- [x] Add party inspect detail view (AC: 5, 6, 7)
  - [x] Use `GET /api/v1/parties/{id}` for authoritative detail hydration.
  - [x] Display person details and organization details in separate sections so type-specific personal-data handling remains visible.
  - [x] Display contact channels with type, value, and preferred status.
  - [x] Display identifiers with type and value.
  - [x] Display consent records when the detail payload includes them; do not add consent editing actions in this story.
  - [x] Display active/inactive, restricted, erased, created, and last-modified state when present.
  - [x] Handle `404`, `410`, degraded projection, and stale-list/detail mismatch without crashing or leaking raw ProblemDetails payloads.
  - [x] On stale, `404`, `403`, or `410` detail outcomes, clear selected detail data, keep browse context intact, and provide a bounded return/refresh path without implying mutation or lifecycle actions.

- [x] Enforce frontend security and privacy rules (AC: 4, 5, 6, 7)
  - [x] Render all party data fields as encoded text through normal Razor/component binding.
  - [x] Do not use `MarkupString`, `@((MarkupString)...)`, `AddMarkupContent`, unsafe markdown rendering, `innerHTML`, raw HTML fragments, or untrusted data in JavaScript contexts for party values, status text, degraded messages, or API error text.
  - [x] Do not log full party names, contact channel values, identifier values, consent details, JWTs, claim sets, or membership dictionaries from the portal.
  - [x] Do not include raw user search text, identifiers, email addresses, or PII in client storage keys.
  - [x] Keep tenant and user cache scope fail-closed by reusing FrontComposer storage and query conventions.

- [x] Add admin authorization and tenant-context UX coverage (AC: 1, 4, 7)
  - [x] Require the existing backend admin policy for admin-only APIs; frontend affordances do not replace server authorization.
  - [x] Make missing token, missing tenant claim, and missing admin role distinguishable in UI copy without exposing sensitive claim values.
  - [x] Use Hexalith.Tenants-provided tenant context and role state from the completed Epic 11 stories; do not infer authorization from the JWT tenant claim alone.
  - [x] Validate cross-tenant scoped ids are rejected or hidden consistently with `PartiesController.GetPartyAsync`.

- [x] Add tests for the portal behavior (AC: 1-8)
  - [x] Add bUnit/component tests for browse, search, filters, detail hydration, degraded status, and unauthorized/forbidden states.
  - [x] Add API/transport tests or fakes proving the portal calls `GET /api/v1/parties`, `GET /api/v1/parties/search`, and `GET /api/v1/parties/{id}` with expected query parameters.
  - [x] Add XSS regression tests using party values containing `<script>`, quotes, angle brackets, and HTML-like strings.
  - [x] Add tests proving erased or cross-tenant records are not displayed after failed detail hydration.
  - [x] Add tests proving tenant switches clear list/search/detail state and that stale in-flight responses from the previous tenant cannot repopulate the UI.
  - [x] Add smoke coverage for keyboard navigation/focus on search, filters, pagination, list rows, detail navigation, retry/error actions, and focus restoration after paging/search/error retry.
  - [x] Add localization tests or resource checks for UI strings, status labels, empty/error messages, dates, booleans, and counts; avoid hard-coded component text except stable resource keys.
  - [x] Add accessibility checks that status badges are screen-reader labeled, visible focus is retained, and active/restricted/erased/stale states are not distinguished by color alone.

- [x] Validate build and affected tests
  - [x] Run the affected FrontComposer/portal test project(s).
  - [x] Run `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release` if backend API behavior changes.
  - [x] Run the affected integration tests only if full topology prerequisites are available; otherwise record the infrastructure skip reason.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.

## Dev Notes

### Epic Context

Epic 10 adds an administration and frontend layer for browsing, searching, inspecting, and later processing GDPR operations for party records. Story 10.1 is the read-only foundation for the admin portal. Stories 10.2 and 10.3 own GDPR operations and the embeddable party picker; do not pull those actions into this story. [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Administration & Frontend (v1.2)]

Epic 11 is intentionally scheduled before Epic 10 so the admin portal consumes tenant context from Hexalith.Tenants instead of duplicating tenant management. Tenant lifecycle, membership, roles, and configuration stay owned by Hexalith.Tenants. [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]

### Story Foundation

The story source requires a web-based admin portal where authenticated administrators can browse a paginated party list, search by display name in the baseline experience, expose email or identifier search only when the dedicated search capability is enabled, filter by party type and active status, and inspect full party details. The portal must be XSS-safe for user-supplied and AI-created party data. [Source: _bmad-output/planning-artifacts/epics.md#Story 10.1: Admin Portal - Browse, Search & Inspect]

PRD requirements mapped to this story are FR65 and NFR32: administrators can browse/search/inspect party records through an administration interface, and frontend rendering must output-encode all party data fields so stored XSS is not possible. [Source: _bmad-output/planning-artifacts/prd.md#Administration & Frontend (v1.2)]

### Current Backend Surface

`src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs`

- Current state: exposes authenticated browse, search, get-by-id, temporal name, and command endpoints under `/api/v1/parties`.
- Story usage: the portal should consume `ListPartiesAsync`, `SearchPartiesAsync`, and `GetPartyAsync` rather than creating new browse/search APIs.
- Preserve: tenant extraction from `eventstore:tenant`, page-size cap at 100, erased-party filtering on list/search, cross-tenant scoped-id rejection, degraded projection headers, `401` missing-tenant behavior, `403` cross-tenant behavior, `404` not found, and `410` erased state.

`src/Hexalith.Parties.CommandApi/Controllers/AdminController.cs`

- Current state: admin-only backend controller under `/api/v1/admin` for projection rebuild, key management, erasure, consent, restriction, portability export, and processing records.
- Story usage: Story 10.1 may link to or read admin-only state needed for inspection, but must not add GDPR mutation workflows. Those belong to Story 10.2.
- Preserve: `[Authorize(Policy = "Admin")]` backend enforcement and bounded ProblemDetails output.

`src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs`

- Current state: index rows include `Id`, `Type`, `IsActive`, `[PersonalData] DisplayName`, non-serialized searchable contact channels/identifiers, `CreatedAt`, `LastModifiedAt`, and `IsErased`.
- Story usage: list rows should use this as the primary shape. Do not assume contact channel or identifier values are present in serialized index responses because they are marked `[JsonIgnore]`.
- Preserve: `DisplayName` is personal data and must be rendered/logged carefully.

`src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`

- Current state: detail payload includes `DisplayName`, `SortName`, person/organization details, contact channels, identifiers, consent records, name history, created/modified timestamps, restricted state, and erased state.
- Story usage: detail view should be hydrated only after selecting a party row.
- Preserve: personal-data fields remain plain text in UI and must not be copied into storage keys, logs, telemetry dimensions, or raw HTML.

### Frontend Architecture Direction

`Hexalith.FrontComposer` is checked out as a root-level submodule and already contains Blazor/Fluent UI shell infrastructure: components, DataGrid filtering/search work, ETag query caching, auth redirect, command feedback, localization, storage, Fluxor state, and generated component conventions. Use that framework first. Do not initialize nested submodules. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md] [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md]

The repository's Epic 10 source says "TypeScript admin portal", but the current implementation context now includes a mature Blazor/Fluent UI FrontComposer submodule. This story should use FrontComposer unless the architect explicitly decides to replace that direction. Record any mismatch as an implementation note, not as permission to build a second portal stack.

Fluent UI Blazor component docs available through the local MCP confirm `FluentDataGrid<T>` supports tabular rendering, sorting, row click/focus events, paging integration, loading/error content, localization of grid strings, keyboard navigation, and `DataGridDisplayMode.Table` is recommended when using virtualization. The same docs in this environment did not expose a `FluentSearch` or `FluentTextField` component by those names; previous FrontComposer work used `FluentTextInput` with search input type as the safe fallback. [Source: Fluent UI Blazor MCP `FluentDataGrid` component details] [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md#Debug Log References]

### Security and XSS Guardrails

Microsoft's current ASP.NET Core XSS guidance says Razor encodes variable output by default, but unsafe raw output APIs are not automatically encoded. Blazor threat guidance specifically warns against rendering raw HTML from untrusted sources and prefers normal content rendering over markup rendering for user-supplied strings. Apply that rule to every party field. [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0] [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]

Use normal Razor/component text rendering for `DisplayName`, names, contact values, identifiers, consent purposes, and API ProblemDetails text. Never use `MarkupString`, `AddMarkupContent`, `innerHTML`, or JavaScript string interpolation with party data.

### Search and Degraded Behavior

Story 9.6 has active implementation work in progress and adds Memories-backed lexical, semantic, hybrid, and graph-assisted party search through the existing Parties REST and MCP search service. Story 10.1 should consume the approved public search behavior but avoid depending on in-progress internals. If rich search is unavailable, show the API-provided degraded/local-only status and keep baseline display-name search working. [Source: _bmad-output/implementation-artifacts/9-6-hexalith-memories-backed-party-search.md]

Do not describe local fuzzy display-name search as semantic search. If headers indicate degraded search, make that visible to administrators without exposing backend exception text or sensitive query content.

### Authorization and Tenant Boundaries

The backend list/search/detail endpoints remain tenant-scoped by the `eventstore:tenant` claim and Epic 11 Tenants access rules. The portal can improve UX around authorization states, but the backend remains the enforcement point.

Required behavior:

- Missing or invalid token: show an authentication-required state.
- Missing tenant claim: show a tenant-context-required state.
- Missing admin role for admin-only APIs: show a forbidden state.
- Cross-tenant scoped party id: do not display the party; surface bounded forbidden/not-found copy.
- Tenant switch: clear visible rows/detail until the new tenant query succeeds.

### Project Structure Notes

- Parties-specific portal code should live in a Parties-owned frontend project or adapter layer that references FrontComposer packages/submodule output; do not place Parties domain UI directly inside the FrontComposer submodule unless that submodule is intentionally being changed.
- Backend API changes, if any, stay in `src/Hexalith.Parties.CommandApi`.
- Shared public data contracts stay in `src/Hexalith.Parties.Contracts`; do not add UI framework dependencies there.
- Component tests should live near the new portal test project. Backend controller tests stay in `tests/Hexalith.Parties.CommandApi.Tests`.
- Full-topology tests follow the existing `tests/Hexalith.Parties.IntegrationTests` fixture pattern and should skip gracefully when DAPR/Aspire infrastructure is unavailable.
- No `project-context.md` persistent fact file was found during story creation.

### Previous Work Intelligence

Story 9.6 is currently `in-progress` and has dirty active-development files. Do not depend on uncommitted 9.6 code beyond public contracts already present in the working tree; treat rich search as optional/degraded until the story exits active implementation.

Story 11.4 created test and documentation guidance around Tenants integration. This story should assume tenant provisioning and Tenants-backed membership/roles are already the intended authority, not create Parties-local tenant administration.

FrontComposer Story 4.3 shipped DataGrid filtering components and state effects but recorded generator integration as deferred. Confirm the current submodule state before deciding whether to use generated projection views, hand-authored FrontComposer views, or a small Parties adapter. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md#Completion Notes List]

FrontComposer Story 5.2 shipped HTTP response classification, ETag caching, no-churn 304 behavior, auth redirect, and warning/validation feedback. Reuse those seams for portal query and error handling instead of writing raw `HttpClient` status-switch logic in components. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md#Completion Notes List]

### Testing Requirements

- Component tests cover successful list rendering, type filter, active filter, search submit/debounce if applicable, page-size cap, zero-results states, degraded search status, detail navigation, and detail hydration.
- Security tests include encoded display of `<script>alert(1)</script>`, quotes, angle brackets, HTML-like contact channel values, and identifier values.
- Authorization tests cover no token, no tenant claim, regular user/no admin role, and tenant switch clearing visible state.
- API integration or fake transport tests assert the correct endpoints and query parameters are used.
- Existing backend tests such as `AdminEndpointIntegrationTests` should remain green if admin controller behavior is not intentionally changed.
- Full topology tests are useful but optional if prerequisites are unavailable; do not convert infrastructure absence into a product failure.

### Latest Technical Information

- Fluent UI Blazor `FluentDataGrid<T>` supports sorting, loading/error content, row click/focus events, keyboard navigation, localization of grid UI strings, and table display mode for virtualization-sensitive grids. [Source: Fluent UI Blazor MCP `FluentDataGrid` component details]
- `FluentSearch` was not available from the local Fluent UI Blazor MCP in this environment; use the existing FrontComposer fallback pattern of `FluentTextInput` with search input type unless the checked package version exposes a stronger component at implementation time. [Source: Fluent UI Blazor MCP `FluentSearch` lookup] [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md#Debug Log References]
- Current root package versions include .NET 10, Dapr packages 1.16/1.17, Aspire 13.2.x, MediatR 14.1.0, FluentValidation 12.1.1, and MCP 1.0.0. FrontComposer currently pins `Microsoft.FluentUI.AspNetCore.Components` to `5.0.0-rc.2-26098.1`; do not opportunistically upgrade frontend packages in this story.
- ASP.NET Core XSS guidance for .NET 10 recommends encoding untrusted output at render time and avoiding non-encoding raw HTML output types for untrusted content. [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0]

### Implementation Guardrails

- Do not build a second tenant authority or tenant-management UI in Parties.
- Do not expose GDPR operation buttons, erasure workflows, consent editing, restriction actions, or export actions; Story 10.2 owns those.
- Do not add write commands to Story 10.1 except incidental route/navigation state.
- Do not display erased party details as normal party data.
- Do not treat search candidates from Memories as authoritative; detail display comes from Parties projection hydration.
- Do not use root-level or nested submodule update commands. Use the checked-out root-level submodule content as-is.
- Do not add production dependencies from UI code into `Hexalith.Parties.Contracts`.
- Do not store or log PII in cache keys, telemetry dimensions, console logs, or test output.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.1: Admin Portal - Browse, Search & Inspect]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Administration & Frontend (v1.2)]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]
- [Source: _bmad-output/planning-artifacts/prd.md#Administration & Frontend (v1.2)]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR32]
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements Overview]
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements Coverage Validation]
- [Source: src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs]
- [Source: src/Hexalith.Parties.CommandApi/Controllers/AdminController.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs]
- [Source: tests/Hexalith.Parties.CommandApi.Tests/Controllers/AdminEndpointIntegrationTests.cs]
- [Source: tests/Hexalith.Parties.IntegrationTests/Admin/AdminEndpointE2ETests.cs]
- [Source: _bmad-output/implementation-artifacts/9-6-hexalith-memories-backed-party-search.md]
- [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md]
- [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]

### ATDD Artifacts

- Checklist: `_bmad-output/test-artifacts/atdd-checklist-10-1-admin-portal-browse-search-and-inspect.md`
- Wire-level transport scaffolds (8 skipped facts, AC1–AC5/AC7): `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs`
- Architectural fitness scaffolds (6 skipped facts, AC8 + read-only non-goals): `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalReadOnlySurfaceTests.cs`
- XSS guardrail scaffolds (3 skipped facts, AC6): `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalXssGuardrailTests.cs`
- Authorization-state scaffolds (4 skipped facts, AC4/AC7): `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalAuthorizationStateTests.cs`
- bUnit component tests (browse grid, search box, filters, detail layout, focus/accessibility, localization): deferred to green phase under `tests/Hexalith.Parties.AdminPortal.Tests` once `src/Hexalith.Parties.AdminPortal` is created — see checklist activation guidance.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-04T09:04:12Z - `/bmad-party-mode 10-1-admin-portal-browse-search-and-inspect; review;` completed with Winston, John, Sally, and Amelia.
- 2026-05-07T12:00:00+02:00 - Red phase: added `tests/Hexalith.Parties.AdminPortal.Tests` bUnit and transport tests before creating `src/Hexalith.Parties.AdminPortal`.
- 2026-05-07T12:15:00+02:00 - Green/refactor phase: implemented FrontComposer-registered `/admin/parties` route, metadata-preserving Parties REST transport, fail-closed state handling, labels surface, and encoded Razor detail rendering.
- 2026-05-07T12:30:00+02:00 - Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed 8/8; `dotnet build Hexalith.Parties.slnx --configuration Release` passed; unsafe-rendering scan for `MarkupString`, `AddMarkupContent`, `innerHTML`, logging, and browser storage found no matches.
- 2026-05-07T18:00:00+02:00 - BMad pass-1 code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) executed; 5 decision-needed + 37 patch + 6 defer + 4 dismiss findings logged in Review Findings. Pass-1 application: D1 (search wire shape → patch), D5 (drop unsupported `type`/`active` from search → patch), D2a (auth defaults flipped fail-closed → patch), and 25 patch items (P1–P15, P18, P21–P24, P26–P28, P32–P33) applied. D2b (Hexalith.Tenants integration), D3 (rich-search detection), D4 (FrontComposer Fluent UI reuse), and 12 patch items (P16, P17, P20, P25, P29–P31, P34–P37) deferred to follow-up story 10-1.1. Validation: `dotnet build Hexalith.Parties.slnx --configuration Release` clean; `dotnet test tests/Hexalith.Parties.AdminPortal.Tests --configuration Release` 16/16 (8 new tests added for status mapping, network failure, null-id validation, fail-closed defaults).
- 2026-05-07T22:30:00+02:00 - BMad dev-story continuation for unresolved patch findings P16, P17, P20, P25, P29, P30, P31, and P34-P37. Added restrictions, system metadata, name history, stale detail metadata, bounded query metadata, retry/no-leak tests, async stale-response coverage, and activated Story 10.1 ATDD guardrails. Validation: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` 22/22; `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release` 59 passed / 6 skipped GDPR Story 10.2 scaffolds; `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release` 42 passed / 15 skipped GDPR Story 10.2 scaffolds; `dotnet build Hexalith.Parties.slnx --configuration Release` clean; full `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` failed outside Story 10.1 scope on `AdminEndpointE2ETests.RebuildEndpoint_WithAdminToken_Returns202AcceptedAsync` returning 403, while isolated `SemanticSearchPerformanceBenchmarkTests.Search_100KEntries_ExactMatch_CompletesWithin500ms` passed after the parallel full-suite perf failure.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- 2026-05-04T09:04:12Z - party-mode review completed; applied low-risk clarifications for rich-search capability gating, tenant-switch race handling, no-leak authorization states, read-only non-goals, API source boundaries, UX state matrix, XSS/storage constraints, accessibility, localization, and focused test coverage.
- 2026-05-07 - Added a Parties-owned Blazor/Razor admin portal package registered against FrontComposer through a `DomainManifest` and route `/admin/parties`; no TypeScript SPA, tenant-management UI, or GDPR mutation workflow was introduced.
- 2026-05-07 - Implemented browse/search/filter/paging/detail hydration against existing `GET /api/v1/parties`, `GET /api/v1/parties/search`, and `GET /api/v1/parties/{id}` APIs with page-size capping, degraded/stale metadata surfacing, bounded failures, and cancellation/version guards for tenant-context changes.
- 2026-05-07 - Added normal Razor text rendering for party values plus XSS, authorization, localization-label, transport, and no-leak tests. Backend APIs were unchanged, so CommandApi tests were not required by the conditional validation task; full-topology integration tests were not run because this story changed only the frontend package and no Aspire/DAPR topology was needed.
- 2026-05-07 - Resolved remaining Story 10.1 patch findings by adding detail rendering for restrictions, erased/restricted timestamps, party id, sort name, and name history; bounding metadata from server-controlled headers/body fields; surfacing stale detail age; proving retry, gone/forbidden no-leak behavior, real async tenant-switch stale-response suppression, keyboard/native-control accessibility affordances, and date/boolean/count localization coverage.

## Party-Mode Review

- Date/time: 2026-05-04T09:04:12Z
- Selected story key: `10-1-admin-portal-browse-search-and-inspect`
- Command/skill invocation used: `/bmad-party-mode 10-1-admin-portal-browse-search-and-inspect; review;`
- Participating BMAD agents: Winston (System Architect), John (Product Manager), Sally (UX Designer), Amelia (Senior Software Engineer)
- Findings summary: reviewers agreed the story was directionally sound but needed pre-dev hardening around API contract boundaries, optional Story 9.6 rich-search gating, tenant switch race handling, no-leak authorization behavior, stale/degraded UI states, detail information hierarchy, accessibility, localization, XSS constraints, PII-safe storage/logging, and focused test expectations.
- Changes applied: tightened AC3/AC4/AC7/AC8; added read-only non-goals; added explicit API source and tenant authority constraints; added browser storage/logging privacy rules; added detail rendering order; added a UX state matrix; added task bullets for rich-search gating, stale tenant response handling, no-leak failures, stale/detail outcomes, raw HTML prohibitions, tenant-switch tests, keyboard/focus tests, localization checks, and accessible status indicators.
- Findings deferred: exact localized copy; exact visual treatment of degraded/stale status; final column styling beyond required fields; rich-search ranking/highlighting/relevance; deep-link policy for party detail routes; any new masking policy for emails, identifiers, consent metadata, or AI-derived labels; any GDPR, tenant-management, or write-operation workflows.
- Final recommendation: `ready-for-dev`

### File List

- `Hexalith.Parties.slnx`
- `Directory.Packages.props`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalListState.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalListRequest.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalPartyQueryService.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryBounds.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryException.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryFailureKind.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryMetadata.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryResult.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalSearchRequest.cs`
- `src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalOptions.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminListCoordinator.cs`
- `src/Hexalith.Parties.AdminPortal/_Imports.razor`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingHttpMessageHandler.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Usings.cs`
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs`
- `tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj`
- `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalAuthorizationStateTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalReadOnlySurfaceTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalXssGuardrailTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj`
- `_bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-05-07 - Implemented Story 10.1 admin portal browse/search/inspect foundation and marked ready for review.
- 2026-05-07 - Resolved remaining Story 10.1 BMad review patch findings P16, P17, P20, P25, P29-P31, and P34-P37; marked story ready for review.

### Review Findings

Pass-1 BMad code review (2026-05-07). Three reviewers: Blind Hunter (adversarial, diff-only), Edge Case Hunter (path enumeration), Acceptance Auditor (diff vs spec). 26+30+25 raw findings; 52 unique after merge.

#### Decision-needed

- [x] [Review][Decision][Applied] **D1 — Search response shape mismatch (silent zero-result bug against real backend)** — `PartiesController.SearchPartiesAsync` returns `PartySearchResponse {Results: PagedResult<PartySearchResult>, Status, DegradedReason, ScoreMetadata, SourceMetadata}`; `PartiesAdminPortalApiClient.SearchPartiesAsync` deserializes the body directly into `PagedResult<PartySearchResult>`, producing default empty values because the JSON has no top-level `items`/`page` fields. Tests pass only because doubles serialize the wrong shape too. Fix path also implicates D5. [src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:79]
- [x] [Review][Decision][Split: D2a applied, D2b deferred] **D2 — Authorization is theatrical: `[Parameter]` booleans default to fully-permissive AND no Hexalith.Tenants integration**

  Resolution: D2a (defaults flipped to `false`) applied — hosts now must explicitly grant access. D2b (Hexalith.Tenants integration with `AuthenticationStateProvider`) **deferred to follow-up story 10-1.1** because it requires significant new infrastructure (claims access, Tenants service consumer, role mapping) outside the scope of a code-review patch pass. — `IsAuthenticated`/`HasTenantContext`/`IsAdmin` default `true`; a host that forgets to wire them gets a fully-open admin UI. Story task `[x] Use Hexalith.Tenants-provided tenant context and role state from the completed Epic 11 stories` is unimplemented (no `AuthenticationStateProvider`, no claims access, no Tenants service consumed). Server-side enforcement remains the actual gate, but the spec demanded Tenants integration. [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:461-471]
- [x] [Review][Decision][Deferred to 10-1.1] **D3 — Rich-search detection is fake**

  Resolution: deferred — depends on Story 9.6 capability-signal stabilization. UI gates correctly when host passes `RichSearchAvailable = false`; detection probe added in follow-up. — `RichSearchAvailable` is a `[Parameter] bool` with no health probe / capability signal. AC3 demanded "deterministically detect that the backend capability is available and healthy". UI gating works when host passes `false`; detection logic missed.
- [x] [Review][Decision][Deferred to 10-1.1] **D4 — FrontComposer integration is decorative, not wired**

  Resolution: deferred — replacing raw `<table>`/`<select>`/`<input>` with `FluentDataGrid<T>`/`FluentTextInput`/`FluentSelect` and wiring FrontComposer transports/ETag caching/auth redirect is multi-week work and represents a separate story. AC8 needs revision in the follow-up. — package only references `Hexalith.FrontComposer.Contracts`; markup uses raw `<table>`/`<select>`/`<input>`/`<button>`. No `FluentDataGrid<T>`, no FrontComposer DataGrid filtering, ETag caching, auth redirect, or command feedback components. AC8 explicitly demanded reuse. Story marked `[x]`. [src/Hexalith.Parties.AdminPortal/]
- [x] [Review][Decision][Applied] **D5 — Search filters silently dropped server-side** — `PartiesAdminPortalApiClient.SearchPartiesAsync` appends `type=`/`active=` query params; backend `/api/v1/parties/search` accepts only `q, mode, caseId, page, pageSize`. Filters appear functional in UI but are ignored by the server. AC4 partially missed for the search path.

#### Patch

- [x] [Review][Patch][Applied] **P1 — `_isLoading` stuck `true` permanently when version mismatch on cancellation** [src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:LoadPageAsync finally]
- [x] [Review][Patch][Applied] **P2 — Detail click cancels in-flight list load via shared `_queryCts`** [Components/PartiesAdminPortal.razor:SelectPartyAsync]
- [x] [Review][Patch][Applied] **P3 — `_version` double-increment via CancelQuery + LoadPageAsync** [Components/PartiesAdminPortal.razor:CancelQuery, LoadPageAsync]
- [x] [Review][Patch][Applied] **P4 — OnParametersSetAsync re-entrancy: parallel HTTP calls and CTS double-dispose race** [Components/PartiesAdminPortal.razor:OnParametersSetAsync]
- [x] [Review][Patch][Applied] **P5 — Uncaught HttpRequestException / JsonException / TaskCanceledException (timeout) escape to render boundary** [Services/PartiesAdminPortalApiClient.cs and Components/PartiesAdminPortal.razor:LoadPageAsync]
- [x] [Review][Patch][Applied] **P6 — HTTP 400/422/429 responses fall through to generic `Unknown`** [Services/PartiesAdminPortalApiClient.cs:154-162]
- [x] [Review][Patch][Applied] **P7 — `partyId` null/empty not validated before `Uri.EscapeDataString`** [Services/PartiesAdminPortalApiClient.cs:GetPartyAsync]
- [x] [Review][Patch][Applied] **P8 — Page-size not bounded at component level (relies on transport collaborator)** [Components/PartiesAdminPortal.razor:CreateListRequest/CreateSearchRequest]
- [x] [Review][Patch][Applied] **P9 — `PageSize`/`RichSearchAvailable` not in parameter signature; change ignored without tenant switch** [Components/PartiesAdminPortal.razor:OnParametersSetAsync]
- [x] [Review][Patch][Applied] **P10 — `ContextKey` null vs empty signature collision** [Components/PartiesAdminPortal.razor:OnParametersSetAsync signature build]
- [x] [Review][Patch][Applied] **P11 — `ObjectDisposedException` on `InvokeAsync(StateHasChanged)` after `Dispose`** [Components/PartiesAdminPortal.razor:Dispose]
- [x] [Review][Patch][Applied] **P12 — `_ = await response.Content.ReadAsStringAsync()` dead read** [Services/PartiesAdminPortalApiClient.cs:164]
- [x] [Review][Patch][Applied] **P13 — `AddHttpClient<T>` + `TryAddScoped<I>` lifetime mismatch** [Extensions/PartiesAdminPortalServiceCollectionExtensions.cs]
- [x] [Review][Patch][Applied] **P14 — Constructor overloading on `PartiesAdminPortalApiClient` is a DI footgun** [Services/PartiesAdminPortalApiClient.cs:23-37]
- [x] [Review][Patch][Applied] **P15 — Hardcoded English strings in select options & aria-labels (`Person`, `Organization`, `Search capabilities`, `Party pages`)** [Components/PartiesAdminPortal.razor]
- [x] [Review][Patch][Applied] **P16 — Detail rendering missing restrictions section and `ErasedAt`/`RestrictedAt` timestamps** [Components/PartiesAdminPortal.razor detail panel]
- [x] [Review][Patch][Applied] **P17 — System metadata block missing in detail (Id, name history)** [Components/PartiesAdminPortal.razor detail panel]
- [x] [Review][Patch][Applied] **P18 — `NotFound`/`Gone` returned for list endpoint not mapped to `NoData`** [Components/PartiesAdminPortal.razor:ApplyFailure]
- [x] [Review][Patch][Applied] **P19 — Inconsistent URL escaping: `type=` and `active=` query parameters not escaped** [Services/PartiesAdminPortalApiClient.cs:67,72,103,108]
- [x] [Review][Patch][Applied] **P20 — `AdminPortalQueryMetadata` exposes raw server-controlled strings without bounds** [Services/AdminPortalQueryMetadata.cs]
- [x] [Review][Patch][Applied] **P21 — `/admin/parties` route hardcoded twice (component `@page` + manifest `Route` constant)** [Components/PartiesAdminPortal.razor and Services/PartiesAdminPortalManifest.cs]
- [x] [Review][Patch][Applied] **P22 — `FormatDate` locale-blind, drops time-of-day, prints `0001-01-01` sentinel for `default(DateTimeOffset)`** [Components/PartiesAdminPortal.razor:FormatDate]
- [x] [Review][Patch][Applied] **P23 — `PortalFilters.Type` Enum.TryParse case-insensitive accepts numeric strings** [Components/PartiesAdminPortal.razor:PortalFilters]
- [x] [Review][Patch][Applied] **P24 — `PartySearchResult.Party` potential null reference in UI projection** [Components/PartiesAdminPortal.razor search-to-row mapping]
- [x] [Review][Patch][Applied] **P25 — `UnauthorizedOrTenantChange` test does not actually verify stale-response suppression (synchronous test doubles)** [tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs]
- [x] [Review][Patch][Applied] **P26 — `ClientSideRichSearchAttempts` is a lying test (counter exposed but never incremented)** [tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs]
- [x] [Review][Patch][Applied] **P27 — `cut.Markup.ShouldContain("Active")` is ambiguous (matches headers, filter, badge)** [tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs]
- [x] [Review][Patch][Applied] **P28 — `RecordingAdminPortalApiClient.GetPartyAsync` throws on empty queue while List/Search return defaults (inconsistent)** [tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs]
- [x] [Review][Patch][Applied] **P29 — ATDD scaffolds in `tests/Hexalith.Parties.Client.Tests/AdminPortal/` and `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/` remain `[Fact(Skip)]` — green-phase activation never happened**
- [x] [Review][Patch][Applied] **P30 — A11y/keyboard/focus tests missing** despite story tasks marked `[x]` [tests/Hexalith.Parties.AdminPortal.Tests]
- [x] [Review][Patch][Applied] **P31 — Erased/cross-tenant component-level test missing** (transport-only coverage exists; no bUnit Gone/Forbidden flow) [tests/Hexalith.Parties.AdminPortal.Tests]
- [x] [Review][Patch][Applied] **P32 — `HttpClient.BaseAddress` null check missing** [Services/PartiesAdminPortalApiClient.cs ctor]
- [x] [Review][Patch][Applied] **P33 — Page > TotalPages handling — stale `Next` button state** [Components/PartiesAdminPortal.razor:ApplyRowsAsync]
- [x] [Review][Patch][Applied] **P34 — `AdminPortalQueryMetadata.StaleDataAge` not surfaced in detail panel** [Components/PartiesAdminPortal.razor:SelectPartyAsync]
- [x] [Review][Patch][Applied] **P35 — Gone/erased rows not removed from list when detail returns 410** [Components/PartiesAdminPortal.razor:SelectPartyAsync Gone branch]
- [x] [Review][Patch][Applied] **P36 — No retry affordance for transient failures** (UX matrix says "Retry"; only Search/Clear available) [Components/PartiesAdminPortal.razor]
- [x] [Review][Patch][Applied] **P37 — Localization tests partial: no date/boolean/count localization tests** [tests/Hexalith.Parties.AdminPortal.Tests]

#### Deferred

- [x] [Review][Defer] **W1 — `BoundPage` no upper limit / `_page` integer overflow** [Services/AdminPortalQueryBounds.cs] — deferred, defensive only (unrealistic at human click rate)
- [x] [Review][Defer] **W2 — `AdminPortalQueryException.StatusCode` is publicly exposed** [Services/AdminPortalQueryException.cs] — deferred, no current consumer leaks it
- [x] [Review][Defer] **W3 — Header parsing edge cases: multiple values, non-bool `X-Service-Degraded` synonyms, very long query, inverted date ranges** — deferred, defensive only
- [x] [Review][Defer] **W4 — `PortalFilters.Active` whitespace trim** — deferred, minor
- [x] [Review][Defer] **W5 — `AdminPortalQueryFailureKind` future-enum exhaustiveness** — deferred, no new kinds planned
- [x] [Review][Defer] **W6 — Adopt `IAsyncDisposable` to await in-flight task before final disposal** [Components/PartiesAdminPortal.razor] — deferred, immediate `ObjectDisposedException` covered by P11

#### Dismissed (4)

- slnx alphabetical ordering of new project entries — cosmetic.
- Manifest static-init load-order hazard — speculative, no observed issue.
- `Manifest.Projections` stringified type names — appears to match FrontComposer registry contract.
- XSS test using `cut.Markup.ShouldNotContain("<script>...")` — substring negation IS valid XSS proof.
