# Story 10.1: Admin Portal - Browse, Search, and Inspect

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want a web-based admin portal to browse, search, and inspect party records,
so that I can manage party data without using API tools or CLI commands.

## Acceptance Criteria

1. Given an authenticated administrator, when they access the admin portal, then they can browse a paginated list of parties for the active tenant.
2. Given the party list, when the administrator searches by display name, then baseline name search returns party matches with existing Parties search metadata and pagination.
3. Given optional rich search capability is enabled by Story 9.6, when the administrator searches by email or identifier, then the portal exposes that capability only through the approved Parties search API and clearly handles degraded/local-only search status.
4. Given party list filters, when the administrator filters by party type or active status, then the list updates without leaking parties from other tenants or erased parties.
5. Given a party in the list, when the administrator opens it, then the portal displays full party detail: party type, person or organization details, contact channels, identifiers, consent records when available, active/inactive state, created date, and last modified date.
6. Given rendered party fields may contain user-supplied or AI-created data, when the portal displays them, then all values are output encoded as text and no raw HTML, `MarkupString`, script injection, or stored XSS path is introduced.
7. Given an administrator lacks a valid token, tenant claim, or admin role, when the portal loads data, then the UI surfaces bounded unauthorized/forbidden states without showing cached or cross-tenant party data.
8. Given the portal architecture is reviewed, when implementation is complete, then it uses the existing Hexalith.FrontComposer Blazor/Fluent UI shell and composable components rather than building a separate TypeScript SPA or duplicate tenant-management UI.

## Tasks / Subtasks

- [ ] Add Parties admin portal composition on top of Hexalith.FrontComposer (AC: 1, 8)
  - [ ] Add the minimum Parties-specific frontend project/package wiring needed to host or register a Parties admin capability against `Hexalith.FrontComposer`.
  - [ ] Reuse FrontComposer shell services, navigation, density, DataGrid, query transport, command feedback, auth redirect, and localization seams where available.
  - [ ] Do not create a standalone SPA that bypasses FrontComposer, unless an explicit architecture decision replaces the current checked-out submodule direction.
  - [ ] Do not add tenant lifecycle, membership, role, or configuration management screens in Parties; Hexalith.Tenants owns those capabilities.

- [ ] Register the party browse view and route (AC: 1, 4, 8)
  - [ ] Create a Parties admin route such as `/admin/parties` or the existing FrontComposer route convention for the Parties bounded context.
  - [ ] Render party rows from `PartyIndexEntry` with columns for display name, party type, active state, created date, and last modified date.
  - [ ] Use stable row keys based on party id; do not display or parse `tenant:domain:aggregateId` scoped ids as user-facing labels.
  - [ ] Keep the first screen dense and operational: no marketing page, hero section, or decorative shell.

- [ ] Wire list query behavior to existing Parties REST/search APIs (AC: 1, 2, 3, 4)
  - [ ] Use `GET /api/v1/parties` for baseline paginated browse with `page`, `pageSize`, `type`, `active`, `createdAfter`, `createdBefore`, `modifiedAfter`, and `modifiedBefore` query parameters where supported.
  - [ ] Use `GET /api/v1/parties/search` for display-name search and for rich search modes introduced by Story 9.6.
  - [ ] Preserve response metadata headers such as `X-Service-Degraded`, `X-Stale-Data-Age`, `X-Parties-Search-Status`, and `X-Parties-Search-Degraded-Reason` in a visible but bounded UI state.
  - [ ] Treat empty search text as list/browse behavior, matching `PartiesController.SearchPartiesAsync`.
  - [ ] Keep email/identifier search disabled or clearly local-only unless the Story 9.6 rich search path is enabled and healthy.

- [ ] Add filters, paging, and empty/error states (AC: 1, 2, 3, 4, 7)
  - [ ] Provide type filter options for `Person` and `Organization`.
  - [ ] Provide active status filter options for active and inactive parties.
  - [ ] Preserve pagination state and page-size bounds; do not allow client page sizes above the API cap of 100.
  - [ ] Show distinct states for zero parties, zero filtered results, degraded search, unauthorized, forbidden, and transient query failure.
  - [ ] Do not use cached rows after `401`, `403`, missing tenant, or tenant-switch failures.

- [ ] Add party inspect detail view (AC: 5, 6, 7)
  - [ ] Use `GET /api/v1/parties/{id}` for authoritative detail hydration.
  - [ ] Display person details and organization details in separate sections so type-specific personal-data handling remains visible.
  - [ ] Display contact channels with type, value, and preferred status.
  - [ ] Display identifiers with type and value.
  - [ ] Display consent records when the detail payload includes them; do not add consent editing actions in this story.
  - [ ] Display active/inactive, restricted, erased, created, and last-modified state when present.
  - [ ] Handle `404`, `410`, degraded projection, and stale-list/detail mismatch without crashing or leaking raw ProblemDetails payloads.

- [ ] Enforce frontend security and privacy rules (AC: 4, 5, 6, 7)
  - [ ] Render all party data fields as encoded text through normal Razor/component binding.
  - [ ] Do not use `MarkupString`, `AddMarkupContent`, `innerHTML`, raw HTML fragments, or untrusted data in JavaScript contexts.
  - [ ] Do not log full party names, contact channel values, identifier values, consent details, JWTs, claim sets, or membership dictionaries from the portal.
  - [ ] Do not include raw user search text, identifiers, email addresses, or PII in client storage keys.
  - [ ] Keep tenant and user cache scope fail-closed by reusing FrontComposer storage and query conventions.

- [ ] Add admin authorization and tenant-context UX coverage (AC: 1, 4, 7)
  - [ ] Require the existing backend admin policy for admin-only APIs; frontend affordances do not replace server authorization.
  - [ ] Make missing token, missing tenant claim, and missing admin role distinguishable in UI copy without exposing sensitive claim values.
  - [ ] Use Hexalith.Tenants-provided tenant context and role state from the completed Epic 11 stories; do not infer authorization from the JWT tenant claim alone.
  - [ ] Validate cross-tenant scoped ids are rejected or hidden consistently with `PartiesController.GetPartyAsync`.

- [ ] Add tests for the portal behavior (AC: 1-8)
  - [ ] Add bUnit/component tests for browse, search, filters, detail hydration, degraded status, and unauthorized/forbidden states.
  - [ ] Add API/transport tests or fakes proving the portal calls `GET /api/v1/parties`, `GET /api/v1/parties/search`, and `GET /api/v1/parties/{id}` with expected query parameters.
  - [ ] Add XSS regression tests using party values containing `<script>`, quotes, angle brackets, and HTML-like strings.
  - [ ] Add tests proving erased or cross-tenant records are not displayed after failed detail hydration.
  - [ ] Add smoke coverage for keyboard navigation/focus on list rows and detail navigation.

- [ ] Validate build and affected tests
  - [ ] Run the affected FrontComposer/portal test project(s).
  - [ ] Run `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release` if backend API behavior changes.
  - [ ] Run the affected integration tests only if full topology prerequisites are available; otherwise record the infrastructure skip reason.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.

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

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

