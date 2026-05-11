# Story 10.3: Embeddable Party Picker Component

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consuming application developer,
I want an embeddable party picker component for my UI,
so that my users can search and select parties without building a custom party selector.

## Acceptance Criteria

1. Given the party picker component is embedded in a consuming application, when a user types a query, then the component provides debounced type-ahead party search through the approved Parties REST API and displays bounded loading, empty, local-only, degraded, forbidden, unauthorized, and transient-failure states.
2. Given search results are returned, when the component renders them, then each result displays party display name, party type, active/restricted/erased-safe status where available, and key contact information only when the backend response already exposes it for the current tenant and user.
3. Given a user selects a party, when selection completes, then the component returns the selected party id to the host application through the approved .NET callback and, for JavaScript hosts, a documented DOM custom event without leaking tenant ids, JWTs, search text, names, contact values, or identifiers into storage keys, logs, telemetry dimensions, or URLs.
4. Given the component is configured, when it calls the Parties API, then it communicates only with existing authenticated Parties REST read endpoints, uses host-provided token/configuration injection, enforces page-size bounds, forwards supported search mode/case options only when explicitly configured, and never stores or refreshes tokens itself.
5. Given rich Memories-backed search is unavailable, disabled, local-only, or degraded, when the user searches, then the component shows the API-provided `X-Parties-Search-Status` / degraded response status and continues approved local display-name search without claiming semantic, hybrid, graph, email, or identifier matching was used.
6. Given the component is used across tenants or signed-in users, when token, tenant context, or host configuration changes, then it clears visible results, selected-party preview data, pending requests, and cached non-sensitive UI state before issuing new requests.
7. Given the component is embedded in different host applications, when reviewed for architecture, then it is implemented as a composable FrontComposer/Blazor component with a documented custom-element or equivalent adapter for JavaScript hosts, not as a second admin portal, tenant selector, standalone TypeScript SPA, or duplicate Parties authorization model.
8. Given host applications need visual consistency, when the component is integrated, then it supports theming through existing FrontComposer/Fluent UI tokens or documented CSS custom properties without requiring host apps to fork the component.
9. Given party data, search text, backend problem details, degraded reasons, or host-provided labels may contain untrusted content, when the component renders them, then all values are encoded as text and no `MarkupString`, `AddMarkupContent`, raw HTML fragments, JavaScript interpolation, `innerHTML`, unsafe markdown, or stored XSS path is introduced.
10. Given keyboard, screen-reader, localization, and responsive use cases, when the component is tested, then the input, results list, selected state, clear action, loading/error status, and no-results state are keyboard operable, screen-reader named, localized, and usable in compact embedded layouts without overlapping text or relying on color alone.

## Scope Clarifications

- Story 10.3 is a consumer-facing picker package/component story. It does not add party create/edit/delete, GDPR operations, tenant lifecycle, membership, role, tenant configuration, admin dashboard, Memories management, or portal shell features.
- The backend remains authoritative for authentication, tenant isolation, authorization, lifecycle, erasure filtering, search status, and degraded behavior. The component may improve UX around those states, but it must not infer permissions from client-side token parsing alone.
- The component should use `GET /api/v1/parties/search` for typed search and may use `GET /api/v1/parties/{id}` only for an explicit selected-party preview if the host opts in. It should not add backend endpoints just to satisfy the picker.
- Empty query behavior must be intentional. If the picker supports initial browse, it uses `GET /api/v1/parties` with bounded page size; otherwise it stays empty until the user enters a query. Do not silently issue unbounded list calls.
- The selected value contract is the stable party id. Display names, emails, identifiers, and contact values are preview data only and must not become the host's durable foreign key.
- The component must handle Story 9.6 search as an optional capability. Do not depend on dirty or in-review 9.6 internals beyond the public response envelope and search-status/degraded metadata already exposed by REST.

## Embedding Contract

| Host shape | Required behavior |
| --- | --- |
| Blazor / FrontComposer host | Expose a strongly typed component with parameters for API base address, auth-token provider or request customizer, optional selected party id, search options, placeholder/labels, disabled/read-only state, and `EventCallback` selection changes. |
| JavaScript host | Provide a custom element or equivalent adapter that can be registered by the host, accepts primitive configuration via attributes/properties, and dispatches a `party-selected` event whose detail contains the selected party id and non-sensitive display summary. |
| Theming | Use existing Fluent UI / FrontComposer design tokens or documented CSS custom properties. Do not hard-code a one-off visual system or require host-level global CSS overrides that break other FrontComposer surfaces. |
| Authentication | Require token/request injection from the host. Do not persist JWTs, refresh tokens, claims, membership dictionaries, or tenant ids. Do not implement a hidden login flow. |
| State | Keep only ephemeral query text, current page, current result set, selected id, and non-sensitive UI status in component state. Persist nothing by default; if host-controlled persistence is later enabled, it must be opt-in and PII-free. |

## Tasks / Subtasks

- [x] Define the picker package and integration surface (AC: 3, 4, 7, 8)
  - [x] Choose the smallest Parties-owned frontend project/package shape that can reference or compose with Hexalith.FrontComposer without editing the FrontComposer submodule directly.
  - [x] If JavaScript host support is implemented via Blazor custom elements, add `Microsoft.AspNetCore.Components.CustomElements` only to the picker host/package project that needs it; do not add UI framework dependencies to `Hexalith.Parties.Contracts`.
  - [x] Expose a Blazor component API for API base URL, token/request injection, search options, selected id, disabled/read-only state, labels/resource keys, result template extension points where safe, and selection callback.
  - [x] Expose a JavaScript custom-element registration path or equivalent adapter with documented attributes/properties and the `party-selected` event contract.
  - [x] Package the component independently as a NuGet/Razor class library, npm package, or explicitly approved equivalent. If the packaging decision is not settled during implementation, record the chosen decision and why it satisfies "independently deployable".

- [x] Implement search and result loading behavior (AC: 1, 2, 4, 5, 6)
  - [x] Use the existing Parties REST search endpoint: `GET /api/v1/parties/search?q={query}&mode={mode}&caseId={caseId}&page={page}&pageSize={pageSize}` only for supported options.
  - [x] Keep page size bounded by the backend cap of 100 and choose a conservative default for type-ahead.
  - [x] Debounce user input and cancel or ignore stale in-flight requests when query, page, token, tenant context, base address, or host configuration changes.
  - [x] Treat empty/whitespace/invisible-only query text as no search unless initial browse is explicitly enabled.
  - [x] Read response-envelope status and metadata where available; also preserve `X-Parties-Search-Status`, `X-Parties-Search-Degraded-Reason`, `X-Service-Degraded`, and `X-Stale-Data-Age` when present.
  - [x] Do not call Memories directly and do not emulate semantic/email/identifier search in the client.
  - [x] Render local-only/degraded search as an explicit bounded state without backend exception text or raw degraded reason injection.

- [x] Implement selection and host notification (AC: 2, 3, 6, 9)
  - [x] Use the REST result party id as the selected value.
  - [x] Return selection to Blazor hosts through a typed callback and to JavaScript hosts through a documented custom event.
  - [x] Include only non-sensitive display summary fields in event detail; never include JWTs, claims, tenant ids, raw query text, contact values, identifiers, or backend ProblemDetails.
  - [x] Provide clear and keyboard-accessible selected, clear, and disabled states.
  - [x] Clear selection preview/result state on tenant/user/token/configuration changes unless the host explicitly rehydrates a selected id for the new context.

- [x] Enforce authorization, tenant, privacy, and XSS rules (AC: 3, 4, 6, 9)
  - [x] Require host-provided auth on every request. Missing token or missing request customizer produces an authentication-required state instead of an anonymous request.
  - [x] Treat `401`, `403`, cross-tenant scoped-id rejection, `404`, `410`, and transient failures as distinct bounded states without existence leaks.
  - [x] Clear visible results and selected preview data after `401`, `403`, token removal, sign-out, tenant switch, or host configuration change.
  - [x] Render all party values, host labels, status messages, degraded reasons, and ProblemDetails fields as encoded text through normal Razor/component rendering.
  - [x] Do not use `MarkupString`, `AddMarkupContent`, raw HTML fragments, unsafe markdown rendering, `innerHTML`, or JavaScript string interpolation with untrusted values.
  - [x] Do not log or store names, emails, phone numbers, postal addresses, identifiers, consent details, search text, JWTs, claims, tenant ids, or membership dictionaries.

- [x] Add theming, localization, accessibility, and compact-layout behavior (AC: 8, 10)
  - [x] Reuse FrontComposer/Fluent UI density, typography, focus, status, and theme conventions where available.
  - [x] Support compact embedded layouts with stable dimensions for input, result rows, status area, and clear/select controls so loading text, long names, and localized labels do not shift or overlap the layout.
  - [x] Provide localized resources for placeholder text, loading, empty, local-only/degraded, unauthorized, forbidden, error, selected, and clear labels.
  - [x] Ensure input, result list, result options, selected chip/summary, clear action, and retry action have accessible names and visible focus.
  - [x] Ensure active/restricted/erased/stale/degraded states are not distinguished by color alone.
  - [x] Provide a no-results state distinct from unauthenticated, forbidden, local-only/degraded, and transient-error states.

- [x] Add adopter documentation and examples (AC: 3, 4, 7, 8, 10)
  - [x] Document Blazor usage with token/request injection, selected value binding, search options, theming, localization, and error-state behavior.
  - [x] Document JavaScript custom-element usage if implemented, including registration, attributes/properties, event payload, cleanup/disposal, and auth-token injection.
  - [x] Document privacy rules: selected party id is durable; display/contact data is preview only; host apps must not store search text or PII in route/query/storage keys.
  - [x] Document required backend endpoints and the behavior when rich search is degraded or local-only.
  - [x] Include a minimal sample integration without initializing or updating nested submodules.

- [x] Add focused tests (AC: 1-10)
  - [x] Add bUnit/component tests for initial render, debounce, search success, paging, loading, empty, local-only, degraded, unauthorized, forbidden, not-found/gone where applicable, transient failure, retry, selected, clear, disabled, and configuration-change cleanup states.
  - [x] Add fake transport tests proving the component calls only approved REST endpoints with expected query parameters and host-provided authorization.
  - [x] Add stale-response tests proving old tenant/query/token responses cannot repopulate results or selected preview after context changes.
  - [x] Add selection contract tests for Blazor callback and JavaScript custom-event payload.
  - [x] Add XSS tests with `<script>`, quotes, angle brackets, HTML-like display names, contact values, host labels, degraded reasons, and ProblemDetails text.
  - [x] Add accessibility/localization tests for accessible names, keyboard navigation, focus return after select/clear/retry, localized resource parity, and long localized labels in compact layout.
  - [x] Add package/registration tests proving the picker can be consumed without pulling UI dependencies into `Hexalith.Parties.Contracts`.

- [x] Validate build and affected tests
  - [x] Run the picker component test project(s).
  - [x] Run affected FrontComposer tests if FrontComposer adapters are changed.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release` if backend API behavior changes.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.

### Review Findings

- [x] [Review][Decision] Host auth injection is presence-checked but not applied to the outgoing query — resolved by extending the typed query client search method with an optional request customizer and composing picker host token/request customization into the outgoing EventStore gateway request.
- [x] [Review][Decision] Token/provider/customizer changes are not a cleanup boundary unless their nullness changes — resolved by adding `AuthContextKey`, fingerprinting the direct access-token value without retaining token text, and including delegate identity in the component context signature.
- [x] [Review][Patch] SearchMode and CaseId are exposed and documented but ignored by the typed-client search path [src/Hexalith.Parties.Picker/Components/PartyPicker.razor:112]
- [x] [Review][Patch] Local-only/degraded coverage is now a misleading success-only assertion instead of proving metadata-unavailable or degraded-state behavior [tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs:72]

## Dev Notes

### Epic Context

Epic 10 adds administration and frontend capabilities for browse/search/inspect, GDPR operations, and an embeddable party picker. Story 10.3 owns the reusable picker for consuming application UIs and must not pull in Story 10.1 admin portal browse/detail concerns or Story 10.2 GDPR operations. [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Administration & Frontend (v1.2)]

Epic 11 intentionally precedes Epic 10 so Parties frontend surfaces consume tenant context, membership, and roles from Hexalith.Tenants instead of duplicating tenant management. This picker must not create tenant lifecycle, membership, role, or configuration UI. [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]

### Story Foundation

The source story requires an embeddable party picker with type-ahead search, result display of party name/type/key contact information, selection returning party id, REST API communication, token injection from the host app, independent deployability, and theming/customization. [Source: _bmad-output/planning-artifacts/epics.md#Story 10.3: Embeddable Party Picker Component]

PRD requirement FR67 maps directly to this story: consuming application developers can embed a party picker component for party search and selection. The picker also inherits frontend XSS constraints from NFR32 because it renders user-supplied and AI-created party data. [Source: _bmad-output/planning-artifacts/prd.md#Administration & Frontend (v1.2)] [Source: _bmad-output/planning-artifacts/prd.md#NFR32]

### Current Backend Surface

`src/Hexalith.Parties/Controllers/PartiesController.cs`

- Current state: exposes authenticated list, search, detail, temporal name, and command endpoints under `/api/v1/parties`.
- Story usage: the picker should primarily consume `SearchPartiesAsync`; it may consume `ListPartiesAsync` only for explicit initial browse and `GetPartyAsync` only for selected-party preview.
- Preserve: tenant extraction from `eventstore:tenant`, page-size cap at 100, erased-party filtering, cross-tenant scoped-id rejection, `401` missing-tenant behavior, `403` cross-tenant behavior, `404` not found, `410` erased state, and degraded/search metadata headers.

`src/Hexalith.Parties/Search/PartySearchBoundary.cs`

- Current state: search uses `PartySearchRequest`, `PartySearchMode`, `PartySearchResponse`, `PartySearchExecutionStatus`, `PartySearchScoreMetadata`, and `PartySearchSourceMetadata` inside the CommandApi boundary.
- Story usage: the picker should consume only the public REST response envelope and headers, not internal service classes.
- Preserve: response status values `Rich`, `Degraded`, and `LocalOnly`; score/source metadata is useful for optional display/debug affordances but must not become required for selection.

`src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs`

- Current state: search rows wrap `PartyIndexEntry` plus match metadata such as score and matched fields.
- Story usage: render result rows from the returned party/index data and keep match metadata secondary. Do not assume `SearchableContactChannels` or `SearchableIdentifiers` are serialized because `PartyIndexEntry` marks those as `[JsonIgnore]`.

`src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs`

- Current state: index rows expose `Id`, `Type`, `IsActive`, `[PersonalData] DisplayName`, `CreatedAt`, `LastModifiedAt`, and `IsErased`; searchable contacts/identifiers are not serialized.
- Story usage: a result row can safely rely on id, type, active state, display name, and timestamps. Key contact information may be absent unless the REST response shape provides it.
- Preserve: `DisplayName` is personal data and must not be logged, stored in keys, or used in durable host identifiers.

`src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`

- Current state: detail payload includes display/sort names, person/organization details, contact channels, identifiers, consent records, name history, created/modified timestamps, restricted state, and erased state.
- Story usage: use only for an opt-in selected-party preview or host rehydration path. Search result display must not require detail hydration for every row unless performance and privacy tradeoffs are explicitly accepted.
- Preserve: personal data remains encoded text only and should not be copied into storage keys, telemetry dimensions, event names, logs, or URLs.

### Frontend Architecture Direction

Stories 10.1 and 10.2 intentionally adapted Epic 10's older "TypeScript admin portal" wording to the current repository reality: a checked-out root-level `Hexalith.FrontComposer` Blazor/Fluent UI shell with DataGrid, ETag query caching, auth redirect, command feedback, localization, storage, Fluxor state, and generated component conventions. Continue that direction unless an architect explicitly replaces it. [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md] [Source: _bmad-output/implementation-artifacts/10-2-admin-portal-gdpr-operations.md]

The picker is still an embeddable component. Current ASP.NET Core documentation supports rendering Razor components from JavaScript technologies through Blazor custom elements and passing parameters as attributes/properties. That is the preferred bridge to satisfy JavaScript-host embedding while preserving the FrontComposer/Blazor direction, unless implementation discovers a better approved packaging route. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/js-spa-frameworks?view=aspnetcore-10.0]

Razor class libraries are the normal packaging unit for reusable Blazor components and static assets. Use an RCL or similar Parties-owned package boundary for reusable picker code, with any JavaScript/custom-element bootstrap kept in the picker package instead of the core contracts assembly. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0]

`Hexalith.FrontComposer` currently pins `Microsoft.FluentUI.AspNetCore.Components` to `5.0.0-rc.2-26098.1`. Do not opportunistically upgrade frontend packages while implementing this story. [Source: Hexalith.FrontComposer/Directory.Packages.props]

### FrontComposer Seams To Reuse

FrontComposer Story 4.3 shipped DataGrid filtering/search patterns, state persistence conventions, keyboard focus guidance, and filter empty-state separation. Reuse its interaction model where applicable; do not build an unrelated picker state machine if an existing FrontComposer pattern fits. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md]

FrontComposer Story 5.2 shipped centralized HTTP response classification, bounded ETag caching, auth redirect, warning/validation feedback, and PII-safe storage key rules. The picker should reuse those seams or mirror their rules instead of scattering raw `HttpClient` status switches in Razor components. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md]

FrontComposer storage rules are fail-closed for tenant/user scope and forbid raw user-entered values, PII, hashes of user input, and arbitrary serialized query payloads in storage keys. The picker should persist nothing by default; if implementation adds opt-in host persistence, it must obey those same rules. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/3-6-session-persistence-and-context-restoration.md]

### Search and Degraded Behavior

Story 9.6 adds Memories-backed lexical, semantic, hybrid, and graph-assisted party search through the existing Parties search service and REST/MCP surfaces. The picker should consume the public REST search contract but avoid depending on in-progress or review-only internals. [Source: _bmad-output/implementation-artifacts/9-6-hexalith-memories-backed-party-search.md]

REST graph mode currently rejects direct `mode=graph` because graph context is exposed through MCP rather than the REST search endpoint. The picker must not expose graph mode in REST unless a stable REST graph context contract exists. [Source: src/Hexalith.Parties/Controllers/PartiesController.cs]

If rich search degrades, the response status/header contract is the user's source of truth. Do not describe local fuzzy/display-name fallback as semantic search, and do not create client-side email/identifier search by scanning hidden data.

### Security and XSS Guardrails

Microsoft's current ASP.NET Core XSS guidance says untrusted input must be encoded before rendering and warns that raw HTML output types are not automatically encoded. Blazor security guidance similarly distinguishes normal text rendering from raw markup APIs such as `AddMarkupContent` with user-supplied content. Apply that rule to party fields, host labels, search text, degraded reasons, and ProblemDetails. [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0] [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]

Required behavior:

- Use normal Razor/component text rendering for all untrusted values.
- Do not use `MarkupString`, `AddMarkupContent`, raw HTML fragments, unsafe markdown rendering, `innerHTML`, or JavaScript string interpolation with party data or backend text.
- Do not log party names, contact values, identifier values, consent details, search text, JWTs, claim sets, tenant ids, or membership dictionaries.
- Do not store PII, tenant ids, free-form search text, or token material in storage keys, custom-event names, telemetry dimensions, route parameters, or download/example filenames.

### Project Structure Notes

- Parties-specific picker code should live in a Parties-owned frontend package/project or adapter layer. Do not place Parties domain UI directly inside the `Hexalith.FrontComposer` submodule unless that submodule is intentionally being changed.
- Shared public domain contracts stay in `src/Hexalith.Parties.Contracts`; do not add Blazor, Fluent UI, FrontComposer, custom-element, or JavaScript packaging dependencies there.
- Backend API changes, if any, stay in `src/Hexalith.Parties`, but this story should prefer existing read endpoints.
- Component tests should live near the new picker test project. Backend controller tests stay in `tests/Hexalith.Parties.Tests`.
- Full-topology tests are useful only if DAPR/Aspire prerequisites are available; record an infrastructure skip rather than making absence of topology a product failure.
- No `project-context.md` persistent fact file was found during story creation.

### Previous Work Intelligence

Story 10.1 established the FrontComposer direction, API source boundaries, tenant-switch cleanup, XSS/storage constraints, localization/accessibility expectations, and read-only admin browse/search/detail behavior. Reuse those rules where the picker overlaps with search and selected-detail preview. [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md]

Story 10.2 reinforced that frontend GDPR and admin operations consume existing backend endpoints and must clear PII-bearing UI state on auth, tenant, party, and operation changes. The picker has a smaller read-only scope but must keep the same cleanup discipline. [Source: _bmad-output/implementation-artifacts/10-2-admin-portal-gdpr-operations.md]

Story 9.6 is currently `review` and Story 11.2 is currently `in-progress` in this working tree. Do not base picker implementation on uncommitted active-development details from those stories; rely on public REST contracts and the current sprint-status state.

### Latest Technical Information

- ASP.NET Core 10 documentation describes Blazor custom elements as a way to render Razor components from JavaScript frameworks such as Angular, React, and Vue, with parameters passed via HTML attributes or DOM properties. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/js-spa-frameworks?view=aspnetcore-10.0]
- ASP.NET Core 10 documentation describes Razor class libraries as the reusable component/static-asset packaging mechanism for Blazor components. [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0]
- Root package versions include .NET 10, Dapr packages 1.16/1.17, Aspire 13.2.x, MediatR 14.1.0, FluentValidation 12.1.1, and MCP 1.0.0. FrontComposer currently pins Fluent UI Blazor to `5.0.0-rc.2-26098.1`; do not upgrade as part of this story.
- ASP.NET Core XSS and Blazor security guidance for current docs support the same implementation rule: render untrusted data as text and avoid raw markup APIs for untrusted content. [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0] [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]

### Implementation Guardrails

- Do not add party mutation, GDPR operation, admin dashboard, tenant management, role management, Memories management, or MCP-only graph-search UX to this picker.
- Do not call Memories directly from the picker.
- Do not add a second frontend stack if FrontComposer/Blazor custom elements satisfy embedding.
- Do not store or log token material, tenant ids, query text, names, contact values, identifiers, consent details, or raw backend error text.
- Do not use display names or contact values as durable host identifiers.
- Do not issue unbounded list/search calls from type-ahead.
- Do not initialize or update nested submodules. Use the checked-out root-level submodule content as-is.

### Testing Requirements

- Component tests cover search debounce, request cancellation/stale-response suppression, page-size bounds, search status rendering, local-only/degraded behavior, selected/clear state, auth/tenant cleanup, empty states, and compact layout.
- Contract/adapter tests cover Blazor callback and JavaScript `party-selected` event payload shape.
- Security tests cover encoded rendering of script-like party data, host labels, ProblemDetails, and degraded reasons.
- Authorization tests cover missing token, `401`, `403`, tenant switch, sign-out, cross-tenant scoped id, `404`, and `410` cleanup behavior.
- Packaging tests prove the picker can be referenced without introducing UI dependencies into `Hexalith.Parties.Contracts`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.3: Embeddable Party Picker Component]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Administration & Frontend (v1.2)]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]
- [Source: _bmad-output/planning-artifacts/prd.md#Administration & Frontend (v1.2)]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR32]
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements Overview]
- [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md]
- [Source: _bmad-output/implementation-artifacts/10-2-admin-portal-gdpr-operations.md]
- [Source: _bmad-output/implementation-artifacts/9-6-hexalith-memories-backed-party-search.md]
- [Source: src/Hexalith.Parties/Controllers/PartiesController.cs]
- [Source: src/Hexalith.Parties/Search/PartySearchBoundary.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs]
- [Source: Hexalith.FrontComposer/Directory.Packages.props]
- [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md]
- [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md]
- [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/3-6-session-persistence-and-context-restoration.md]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/js-spa-frameworks?view=aspnetcore-10.0]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-04T15:03:37.6566633+02:00 - `/bmad-create-story 10-3-embeddable-party-picker-component` completed via automation.
- 2026-05-05T19:10:00+02:00 - Story moved to in-progress; direct dev-story implementation started without ATDD generation.
- 2026-05-05T19:22:00+02:00 - `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` passed: 21 tests.
- 2026-05-05T19:25:00+02:00 - `dotnet build Hexalith.Parties.slnx --configuration Release` blocked by pre-existing unrelated untracked 11.4 files in `tests/Hexalith.Parties.DeployValidation.Tests` and `tests/Hexalith.Parties.Tests`.
- 2026-05-10T20:19:47+02:00 - `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.
- 2026-05-10T20:19:47+02:00 - `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` passed: 902 tests, 6 expected integration skips.
- 2026-05-11 - BMad code review applied picker/client auth-boundary patches. Focused picker tests passed 30/30, client tests passed 60/60, AdminPortal tests passed 67/67, and targeted Client/Picker/AdminPortal Release builds passed. Full solution Release build remains blocked by unrelated Hexalith.Parties.Security missing-type/ref-assembly failures.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Created from Epic 10 / FR67 with FrontComposer/Blazor custom-element direction, REST search/status boundaries, host token injection, privacy-safe selection contract, XSS guardrails, and focused adapter/component test requirements.
- Added independent `Hexalith.Parties.Picker` Razor class library with plain Razor/CSS custom-property theming, host-owned auth injection, bounded Parties REST search, local-only/degraded/authorization/error states, context-change cleanup, and custom-element registration for JavaScript hosts.
- Added privacy-safe `party-selected` DOM event dispatch that carries only party id, type, and status, while .NET hosts receive a typed selection callback.
- Added adopter documentation and README link for Blazor and JavaScript host usage, privacy rules, search behavior, and theming.
- Added picker tests for approved REST calls, page-size bounds, host authorization, status header mapping, non-leaking failures, encoded rendering, localized labels, context cleanup, event payload privacy, and package boundary guardrails.
- Validation complete: Release solution build passed with 0 warnings and 0 errors; full no-build regression passed with 902 tests and 6 expected integration skips.
- BMad code review findings resolved: host auth/request customization now flows through the typed client request hook; search mode/case id are forwarded in query payloads; token/auth context changes clear picker UI state; metadata-unavailable coverage no longer claims local-only/degraded behavior.

### Change Log

- 2026-05-05 - Added embeddable party picker RCL, tests, documentation, package references, and solution registration.
- 2026-05-10 - Completed final build/regression validation and moved story to review.

### File List

- Directory.Packages.props
- Hexalith.Parties.slnx
- README.md
- docs/frontend/party-picker.md
- src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
- src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
- src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj
- src/Hexalith.Parties.Picker/_Imports.razor
- src/Hexalith.Parties.Picker/Components/PartyPicker.razor
- src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css
- src/Hexalith.Parties.Picker/Extensions/PartyPickerCustomElementExtensions.cs
- src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerDefaults.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerEventDetail.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerSearchMetadata.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerSearchMode.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerSearchRequest.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerSearchResponse.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerSearchState.cs
- src/Hexalith.Parties.Picker/Services/PartyPickerSelection.cs
- src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js
- tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj
- tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs
- tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs
- tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerPackagingTests.cs
- tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTestData.cs
- tests/Hexalith.Parties.Picker.Tests/Services/RecordingHttpMessageHandler.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs
- tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
- _bmad-output/implementation-artifacts/10-3-embeddable-party-picker-component.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
