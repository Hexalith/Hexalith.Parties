# Story 10.2: Admin Portal - GDPR Operations

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want to process GDPR requests (erasure, restriction, consent, export) via the admin portal,
so that DPO operations are efficient and auditable through a visual interface.

## Acceptance Criteria

1. Given an authenticated administrator viewing a party, when the party supports GDPR operations, then the portal shows a GDPR operations surface for erasure, restriction, consent, portability export, processing records, and erasure audit state without adding tenant lifecycle, membership, role, or configuration management UI.
2. Given an authenticated administrator viewing a party, when they trigger "Request Erasure", then the portal calls the existing admin erasure API, requires an explicit confirmation step, displays the returned correlation id, polls or refreshes erasure status, and displays the erasure verification report when available.
3. Given erasure is pending, partially verified, failed, verified, or complete, when the portal renders the party, then the UI shows the current erasure state, blocks conflicting write operations, keeps the operator on bounded status/report views, and does not render erased personal data as normal party detail.
4. Given an authenticated administrator viewing a party, when they restrict processing or lift restriction, then the portal calls the existing admin restriction APIs, displays restricted status in the party detail view, preserves restriction reason where supplied, and makes the lift action available only when the party is currently restricted.
5. Given an authenticated administrator viewing a party with contact channels, when they manage consent records, then they can add consent with channel id, purpose, and lawful basis, revoke existing consent, and see consent history with granted/revoked timestamps and actor identities.
6. Given a data portability request, when the administrator exports party data, then the portal downloads the existing JSON export response, handles erased and erasure-in-progress responses distinctly, and records a visible audit cue that the export is also represented in processing activity records.
7. Given the admin dashboard is accessed by a DPO, when GDPR data is available, then it provides a compact operational summary for pending erasure requests, consent status overview, restricted-party indicators, and erasure audit trail entry points for the active tenant.
8. Given the administrator lacks a valid token, tenant context, or admin role, when any GDPR operation loads or submits, then the UI surfaces bounded authentication, tenant-context, or forbidden states without showing cached personal data or raw ProblemDetails payloads.
9. Given party fields, ProblemDetails details, consent purposes, processing summaries, or export metadata contain user-supplied or AI-created values, when the portal renders them, then all values are encoded as text and no `MarkupString`, `AddMarkupContent`, raw HTML, JavaScript interpolation, or stored XSS path is introduced.
10. Given the portal architecture is reviewed, when implementation is complete, then it extends the existing Hexalith.FrontComposer Blazor/Fluent UI shell and Story 10.1 party detail surface rather than building a standalone TypeScript SPA or duplicate Parties admin shell.

## Party-Mode Clarifications

- Story 10.2 is a frontend/admin-portal integration story. It must consume the existing admin endpoints and must not introduce new GDPR backend workflows, orchestration, persistence behavior, tenant authority, role semantics, or tenant-management UI.
- Hexalith.Tenants remains the source of truth for tenant context, membership, and role authorization. The frontend may display bounded authorization states, but backend `[Authorize(Policy = "Admin")]` and tenant extraction remain authoritative.
- The GDPR operation surface is scoped by active tenant, selected party, and operation. Any auth failure, missing tenant, forbidden response, cross-tenant outcome, tenant switch, sign-out, erased-party terminal state, or party selection change must clear visible GDPR state that may contain PII.
- After any successful mutation command, the UI must re-read the authoritative party/GDPR state before enabling follow-on actions. It must not infer completion from command acceptance, stale detail state, or cached operation results.
- Audit/report areas for erasure state, erasure certificate, Article 30 processing records, portability export, and DPO summary entry points must be reachable from the party detail operational surface without creating a second dashboard shell.
- All operator-facing labels, action text, confirmation copy, validation messages, status text, DPO summary text, and bounded error states must come from localization resources.
- Destructive and privacy-sensitive controls must use semantic headings, named controls, keyboard operation, focus return after completion/failure, accessible busy/status announcements, and no unnamed icon-only actions.
- Backend text remains untrusted. Party fields, consent values, ProblemDetails details, Article 30 summaries, erasure status/reason text, export metadata, and DPO summary values must render as encoded text only. Do not use `MarkupString`, `AddMarkupContent`, raw HTML fragments, JavaScript interpolation, or export preview rendering for untrusted payloads.
- Logging, telemetry, storage keys, test names, and download filenames may include operation type, non-PII party ids, timestamps, and correlation/status ids only. They must not include names, contact values, identifiers, consent purposes, export payloads, JWTs, claims, tenant membership dictionaries, or raw ProblemDetails details.

## Endpoint Contract Assumptions

| Capability | Existing endpoint contract | UI contract |
| --- | --- | --- |
| Erasure request | `POST /api/v1/admin/parties/{partyId}/erase` | Explicit confirmation first; display accepted correlation id; then refresh erasure status. |
| Erasure status | `GET /api/v1/admin/parties/{partyId}/erasure-status` | Treat backend status as authoritative; pending, partial, failed, verified, complete, and erased states drive enabled actions. |
| Erasure certificate | `GET /api/v1/admin/parties/{partyId}/erasure-certificate` | Fetch only when status indicates report availability; show loading, unavailable, and failure states without exposing raw payload errors. |
| Verification retry | `POST /api/v1/admin/parties/{partyId}/retry-verification` | Offer only for backend-supported partial/failure states; refresh status after completion. |
| Restriction | `POST /api/v1/admin/parties/{partyId}/restrict` | Optional reason text; display domain `422` rejection through bounded command feedback. |
| Lift restriction | `POST /api/v1/admin/parties/{partyId}/lift-restriction` | Available only while party detail says the party is restricted; refresh detail after completion. |
| Consent add | `POST /api/v1/admin/parties/{partyId}/consent` | Command-style add for one channel, purpose, and lawful basis; backend validation remains authoritative. |
| Consent revoke | `DELETE /api/v1/admin/parties/{partyId}/consent/{consentId}` | Command-style revoke for one existing consent record; refresh consent/detail state after completion. |
| Consent read | `GET /api/v1/admin/parties/{partyId}/consent` or `PartyDetail.ConsentRecords` | Use the freshest available admin read when needed; do not derive a second consent state model. |
| Portability export | `GET /api/v1/admin/parties/{partyId}/export` | Direct JSON download from response body; safe non-PII filename; distinct `409` erasure-in-progress and `410` erased states. |
| Processing records | `GET /api/v1/admin/parties/{partyId}/processing-records` | Read-only Article 30 display from backend records; no client-derived compliance ledger. |
| DPO summary | Available party list/detail/admin reads until a tenant-wide summary endpoint exists | Compact tenant-scoped operational entry points only; record limitations when no backend summary endpoint exists. |

## Authorization and State Matrix

| Scenario | Expected UI behavior |
| --- | --- |
| Valid admin and active tenant | Load party detail and GDPR operations for the selected party. |
| Missing or expired token | Clear party/GDPR state and show bounded sign-in/authentication state. |
| Missing tenant claim/context | Clear party/GDPR state and show bounded tenant-context state. |
| Authenticated non-admin or missing admin role | Clear operation state and show bounded forbidden state without raw ProblemDetails. |
| Cross-tenant party id or forbidden scoped id | Clear selected party/GDPR state without existence leak; keep only safe browse shell state. |
| Tenant switch during in-flight request | Ignore stale responses from the previous tenant and clear visible PII until the new tenant load succeeds. |
| Erasure pending or verification in progress | Disable conflicting party mutation actions; keep status/report views bounded. |
| Erased or `410 Gone` response | Treat as terminal privacy state; do not rehydrate or display stale personal detail fields. |
| Transient endpoint failure | Preserve only non-sensitive current-shell state; show retry path and no raw backend details. |

## Tasks / Subtasks

- [ ] Extend the Parties admin portal GDPR operations surface (AC: 1, 7, 10)
  - [ ] Add a GDPR operations area to the Story 10.1 party detail route or the established FrontComposer route convention.
  - [ ] Reuse FrontComposer shell services, navigation, command feedback, warning banners, auth redirect, localization, density, storage, and DataGrid/query state where available.
  - [ ] Keep the page operational and dense: no landing page, marketing hero, decorative cards, or duplicated tenant-management UI.
  - [ ] Do not implement tenant lifecycle, membership, role, or configuration management in Parties; Hexalith.Tenants remains authoritative.
  - [ ] Keep GDPR operation state isolated by active tenant id, selected party id, and operation type; clear stale state on tenant switch, sign-out, authorization failure, party change, and erased terminal state.

- [ ] Implement erasure request and status UX (AC: 2, 3, 8, 9)
  - [ ] Call `POST /api/v1/admin/parties/{partyId}/erase` only after an explicit confirmation that names the irreversible crypto-shredding consequence without echoing unnecessary PII.
  - [ ] Display the accepted correlation id and transition into a status state that queries `GET /api/v1/admin/parties/{partyId}/erasure-status`.
  - [ ] Display `ErasureStatusResponse.Status`, `UpdatedAt`, `ErasedAt`, and store-result statuses when present.
  - [ ] Fetch `GET /api/v1/admin/parties/{partyId}/erasure-certificate` when status indicates certificate/report availability.
  - [ ] Use explicit loading, unavailable, failure, and retry states for erasure status/certificate retrieval; do not imply that accepted erasure means completed erasure.
  - [ ] Offer `POST /api/v1/admin/parties/{partyId}/retry-verification` only for verification failure or partial states where the backend supports retry.
  - [ ] Disable normal party mutation actions while erasure is pending, key-destroyed, verification-in-progress, verified, or erased.
  - [ ] Treat `410 Gone` and erased-state responses as terminal privacy states; do not rehydrate or display stale detail fields.

- [ ] Implement restriction and lift-restriction actions (AC: 4, 8, 9)
  - [ ] Use `POST /api/v1/admin/parties/{partyId}/restrict` with optional reason text.
  - [ ] Use `POST /api/v1/admin/parties/{partyId}/lift-restriction` for restricted parties only.
  - [ ] Render `PartyDetail.IsRestricted` and `RestrictedAt` in the read-only detail view.
  - [ ] Handle domain rejection `422` as bounded operator feedback through the existing FrontComposer command outcome path.
  - [ ] Do not block consent management during restriction; Article 18 workflows require consent actions to remain available.

- [ ] Implement consent management UI (AC: 5, 8, 9)
  - [ ] Read existing consent records from `PartyDetail.ConsentRecords` or `GET /api/v1/admin/parties/{partyId}/consent` when a fresh admin-specific read is needed.
  - [ ] Add consent through `POST /api/v1/admin/parties/{partyId}/consent` with `channelId`, `purpose`, and `lawfulBasis`.
  - [ ] Revoke consent through `DELETE /api/v1/admin/parties/{partyId}/consent/{consentId}`.
  - [ ] Treat consent add/revoke as command-style operations over existing backend semantics; do not add replacement, batch, concurrency-token, or party-wide consent behavior unless the backend already exposes it.
  - [ ] Present lawful basis choices from the existing contract enum, not free-form UI strings.
  - [ ] Show active and revoked consent records with granted/revoked timestamps and actor fields as encoded text.
  - [ ] Validate required fields client-side for usability, but rely on backend validation and domain rejection as authoritative.
  - [ ] Do not add party-wide consent or batch consent semantics; current model is per-channel per-purpose.

- [ ] Implement portability export and processing records (AC: 6, 7, 8, 9)
  - [ ] Call `GET /api/v1/admin/parties/{partyId}/export` and produce a downloadable JSON file from the response body.
  - [ ] Use a safe filename derived from non-PII identifiers such as party id and timestamp; do not use display name, email, identifier, or purpose text in filenames or storage keys.
  - [ ] Handle `409 Conflict` erasure-in-progress and `410 Gone` erased-party states with distinct operator copy.
  - [ ] Read `GET /api/v1/admin/parties/{partyId}/processing-records` and display Article 30 activity summaries in chronological or sequence order.
  - [ ] Treat processing records and erasure certificates as backend-authoritative, read-only compliance records; do not derive or edit compliance state client-side.
  - [ ] Treat processing summaries as untrusted text; render normally encoded and do not expose backend stack traces.

- [ ] Add DPO dashboard summary widgets or compact panels (AC: 7, 8, 9, 10)
  - [ ] Provide entry points for pending erasure requests, recent erasure audit status, restricted parties, and consent overview for the active tenant.
  - [ ] If the backend does not yet expose tenant-wide GDPR summary endpoints, implement a bounded UI from available party list/detail/admin endpoints and record the limitation in completion notes.
  - [ ] Keep counts and summaries tenant-scoped and clear all summary state on tenant switch.
  - [ ] Do not introduce a separate dashboard data store or client-side PII index.

- [ ] Enforce authorization, tenant, privacy, and accessibility behavior (AC: 1-10)
  - [ ] Reuse the existing backend `[Authorize(Policy = "Admin")]` enforcement; frontend affordances must not replace server authorization.
  - [ ] Distinguish missing token, missing tenant claim, missing admin role, cross-tenant scoped id, and transient API failure in bounded UI states.
  - [ ] Clear visible party detail, consent records, processing records, and export state after tenant switch or any `401` / `403` outcome.
  - [ ] Never store or log full party names, contact values, identifiers, consent purposes, export payloads, JWTs, claims, or membership dictionaries.
  - [ ] Log only operation type, non-PII party id, correlation/status id, and outcome category when needed for audit/support diagnostics.
  - [ ] Make destructive erasure and restriction controls keyboard reachable, labeled for screen readers, and confirmation-based.
  - [ ] Manage focus after confirmations, command failures, status refreshes, and export-download outcomes; announce long-running erasure/export state changes through accessible status regions.
  - [ ] Use localized resource strings for labels, status messages, lawful-basis labels, and warning copy.

- [ ] Add operator and completion documentation (AC: 1-10)
  - [ ] Document page purpose, required admin role, active tenant behavior, supported GDPR actions, known backend dependencies, and privacy-safe failure behavior.
  - [ ] Record any missing tenant-wide DPO summary endpoint or infrastructure dependency as a bounded limitation, not as client-side aggregation scope.
  - [ ] In completion notes, list reused endpoints, confirm no new GDPR workflow/backing store was added, list localization resource files touched, summarize accessibility checks, and include dated validation evidence.

- [ ] Add tests for GDPR portal behavior (AC: 1-10)
  - [ ] Add bUnit/component tests for erasure confirm, accepted state, status polling/refresh, certificate/report rendering, retry affordance, and erased terminal state.
  - [ ] Add component tests for restriction, lift restriction, consent add/revoke/history, export download trigger, processing-record display, dashboard summaries, and empty/degraded states.
  - [ ] Add fake transport or adapter tests proving calls to the existing admin endpoints with expected methods, route ids, and JSON bodies.
  - [ ] Add fake transport/API contract tests for success, validation/domain rejection, forbidden, not found, conflict, gone, and transient failure shapes for each consumed admin endpoint.
  - [ ] Add XSS regression tests using `<script>`, quotes, angle brackets, party fields, contact values, consent purposes, channel labels, ProblemDetails details, processing summaries, erasure reason/status text, export metadata, and DPO summary values.
  - [ ] Add authorization and tenant-switch tests proving cached GDPR state is cleared after missing/expired token, `401`, `403`, missing tenant, non-admin user, cross-tenant scoped id, sign-out, tenant change, and stale in-flight response.
  - [ ] Add export-download tests for safe filename/content type, no inline rendering of untrusted export payloads, disabled/loading states, cancellation/failure handling, and tenant/auth failure cleanup.
  - [ ] Add accessibility/localization tests or assertions for named controls, keyboard flow, focus return after dialogs, accessible status announcements for polling/export, and localized resources for operational labels and statuses.
  - [ ] Keep existing backend endpoint suites green: erasure, consent, restriction, portability, and admin endpoint tests.

- [ ] Validate build and affected tests
  - [ ] Run the affected FrontComposer/Parties portal component test project(s).
  - [ ] Run focused backend suites if endpoint contracts change: `tests/Hexalith.Parties.Tests` erasure, consent, restriction, portability, and admin endpoint tests.
  - [ ] Run affected integration tests only when DAPR/Aspire prerequisites are available; otherwise record the infrastructure skip reason.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.

## Dev Notes

### ATDD Artifacts

- Checklist: `_bmad-output/test-artifacts/atdd-checklist-10-2-admin-portal-gdpr-operations.md`
- Client/adapter contract tests: `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs`
- Portal surface tests: `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprSurfaceTests.cs`
- Authorization/state tests: `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprAuthorizationStateTests.cs`
- Privacy/XSS guardrail tests: `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprPrivacyGuardrailTests.cs`

### Epic Context

Epic 10 adds an administration and frontend layer for browsing, searching, inspecting, processing GDPR operations, and later embedding a party picker. Story 10.2 owns the GDPR operations UI on top of the Story 10.1 read-only portal foundation. Story 10.3 owns the embeddable picker. [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Administration & Frontend (v1.2)]

Epic 11 intentionally precedes Epic 10 so Parties admin experiences consume tenant context, membership, and role state from Hexalith.Tenants. This story must not create Parties-owned tenant-management screens or a second tenant authority. [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]

### Story Foundation

The source story requires administrators to process erasure, restriction, consent, and export through the admin portal. It also requires a DPO dashboard with pending GDPR requests, consent status overview, and erasure audit trail. [Source: _bmad-output/planning-artifacts/epics.md#Story 10.2: Admin Portal - GDPR Operations]

PRD requirements mapped to this story are FR66 plus the GDPR capability requirements FR44-FR52: erasure, erasure verification, subscriber notification awareness, per-channel consent, consent revocation, restriction, lift restriction, data portability export, and processing activity records. [Source: _bmad-output/planning-artifacts/prd.md#Administration & Frontend (v1.2)] [Source: _bmad-output/planning-artifacts/prd.md#GDPR Compliance (v1.1)]

### Current Backend Surface

`src/Hexalith.Parties/Controllers/AdminController.cs`

- Current state: admin-only controller under `/api/v1/admin` with `[Authorize(Policy = "Admin")]`.
- Erasure endpoints already exist:
  - `POST /api/v1/admin/parties/{partyId}/erase`
  - `GET /api/v1/admin/parties/{partyId}/erasure-status`
  - `GET /api/v1/admin/parties/{partyId}/erasure-certificate`
  - `POST /api/v1/admin/parties/{partyId}/retry-verification`
- Consent endpoints already exist:
  - `POST /api/v1/admin/parties/{partyId}/consent`
  - `DELETE /api/v1/admin/parties/{partyId}/consent/{consentId}`
  - `GET /api/v1/admin/parties/{partyId}/consent`
- Restriction endpoints already exist:
  - `POST /api/v1/admin/parties/{partyId}/restrict`
  - `POST /api/v1/admin/parties/{partyId}/lift-restriction`
- Portability and audit endpoints already exist:
  - `GET /api/v1/admin/parties/{partyId}/export`
  - `GET /api/v1/admin/parties/{partyId}/processing-records`
- Preserve: tenant extraction from the authenticated user, admin policy enforcement, `401` missing tenant behavior, `403` forbidden behavior, `422` domain rejection ProblemDetails, `409` erasure-in-progress export behavior, and `410` erased-party export/processing behavior.

`src/Hexalith.Parties/Controllers/PartiesController.cs`

- Current state: authenticated party list/search/detail endpoints under `/api/v1/parties`.
- Story usage: reuse Story 10.1 detail hydration and degraded/search handling; this story layers GDPR actions onto the selected party.
- Preserve: cross-tenant scoped-id rejection, erased-party `410 Gone`, list/search erasure filtering, degraded projection headers, and safe ProblemDetails rendering.

`src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`

- Current state: detail payload includes `ConsentRecords`, `IsRestricted`, `RestrictedAt`, `IsErased`, and `ErasedAt`.
- Story usage: render current GDPR state from detail before invoking admin-specific endpoints.
- Preserve: `DisplayName`, `SortName`, `NameHistory`, contact channels, identifiers, and consent values are personal or sensitive operational data and must not be logged, stored in cache keys, or rendered as raw HTML.

`src/Hexalith.Parties.Contracts/ValueObjects/ConsentRecord.cs`

- Current state: consent records include `ConsentId`, `ChannelId`, `Purpose`, `LawfulBasis`, `GrantedAt`, `GrantedBy`, optional `RevokedAt`, optional `RevokedBy`, and computed `IsActive`.
- Story usage: active/revoked UI should derive state from `IsActive` and timestamps rather than inventing a second consent state model.

`src/Hexalith.Parties.Contracts/Models/ProcessingActivityRecord.cs`

- Current state: Article 30 records expose `SequenceNumber`, `EventType`, `Timestamp`, and `Summary`.
- Story usage: render processing records as an audit list/table; summary text is untrusted and must be encoded.

### GDPR Domain Constraints From Previous Stories

Story 9.3 established the erasure state machine and admin erasure endpoints. The erasure flow is asynchronous and may move through pending, key-destroyed, verification, verified, and erased states; the portal must not assume the `POST erase` response means complete erasure. [Source: _bmad-output/implementation-artifacts/9-3-right-to-erasure-and-verification.md]

Story 9.4 established per-channel per-purpose consent, restriction/lift restriction, portability export, and Article 30 processing records. Restriction blocks user-initiated party modifications but does not block consent management, lift restriction, or erasure pipeline commands. [Source: _bmad-output/implementation-artifacts/9-4-consent-management-restriction-and-portability.md]

Important inherited rules:

- Consent can be recorded only for an existing channel.
- Consent purpose is free-form but should be kept consistent; do not enforce a new global purpose taxonomy in this UI story.
- Export reads from the party detail projection and can be briefly stale after just-recorded consent; show bounded feedback rather than promising read-your-write export semantics.
- Export succeeds for restricted parties, returns `409 Conflict` for erasure-in-progress, and returns `410 Gone` for erased parties.
- Restriction persists until explicitly lifted; there is no auto-expiry.
- Batch restriction is deferred; this story should not invent batch backend semantics.

### Frontend Architecture Direction

Story 10.1 created the read-only admin portal story against the checked-out Hexalith.FrontComposer Blazor/Fluent UI shell, despite the older Epic 10 source saying "TypeScript admin portal". Continue that direction unless an architect explicitly changes the frontend architecture. [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md]

`Hexalith.FrontComposer` is a root-level submodule and already contains shell infrastructure for DataGrid filtering/search, ETag query caching, auth redirect, command feedback, localization, storage, Fluxor state, and generated component conventions. Use the checked-out root-level submodule content as-is. Do not initialize or update nested submodules. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md] [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md]

FrontComposer Story 5.2 introduced a response/error taxonomy for query and command outcomes. GDPR operation forms should consume that path for validation, warning, auth redirect, forbidden, not-found, rate-limit, and domain rejection behavior instead of branching directly on raw `HttpClient` status codes in components. [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md]

### Security and XSS Guardrails

Microsoft's current ASP.NET Core XSS guidance treats unencoded user input rendered into pages as the source of XSS risk and notes that raw HTML output APIs are not automatically encoded. Current Blazor security guidance says normal Razor string rendering writes text, while `MarkupString` / `AddMarkupContent` with untrusted content can create XSS risk. Apply that rule to party data, consent purpose text, ProblemDetails details, processing summaries, export metadata, and status messages. [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0] [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]

Required behavior:

- Use normal Razor/component text rendering for all user-controlled values.
- Do not use `MarkupString`, `AddMarkupContent`, `innerHTML`, raw HTML fragments, or JavaScript string interpolation with party data.
- Do not log party names, contact values, identifier values, consent purposes, export payloads, JWTs, claim sets, tenant membership dictionaries, or raw ProblemDetails detail values.
- Do not store PII or free-form user text in FrontComposer storage/cache keys or download filenames.

### UX and Accessibility Guardrails

- Destructive erasure must require explicit confirmation and should name the irreversible effect without repeating unnecessary PII.
- Restriction and erasure actions must be keyboard reachable and screen-reader labeled.
- Status chips, badges, and warning banners must not rely on color alone.
- Use localized resources for action labels, lawful-basis labels, status copy, and warnings.
- Keep dashboard summaries concise and scannable; this is an operational DPO surface, not a marketing page.

### Project Structure Notes

- Parties-specific portal code should live in a Parties-owned frontend project or adapter layer that references FrontComposer packages/submodule output.
- Do not place Parties domain UI directly inside the FrontComposer submodule unless that submodule is intentionally being changed.
- Backend changes, if any, stay in `src/Hexalith.Parties`.
- Shared public data contracts stay in `src/Hexalith.Parties.Contracts`; do not add UI framework dependencies there.
- Component tests should live near the new portal test project. Backend endpoint tests stay in `tests/Hexalith.Parties.Tests`.
- Existing backend focused suites include `ErasureEndpointTests`, `ConsentEndpointTests`, `RestrictionEndpointTests`, `PortabilityEndpointTests`, and `AdminEndpointIntegrationTests`.
- Full-topology tests follow existing integration fixtures and should skip gracefully when DAPR/Aspire infrastructure is unavailable.
- No `project-context.md` persistent fact file was found during story creation.

### Previous Work Intelligence

Story 10.1 is the immediate foundation. Build on its route, party detail hydration, authorization/tenant handling, XSS rules, and FrontComposer direction instead of creating a second portal surface. [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md]

Story 9.6 is currently `in-progress`; rich Memories-backed search is not required for GDPR operations. Treat any in-progress search internals as optional and do not depend on uncommitted implementation details.

Recent git history includes Story 11.1 party review bookkeeping, Memories search integration work, and subproject updates. This story should avoid submodule churn and keep the create-story output limited to the story artifact and sprint tracking.

### Latest Technical Information

- Root package versions include .NET 10, Dapr packages 1.16/1.17, Aspire 13.2.x, MediatR 14.1.0, FluentValidation 12.1.1, and MCP 1.0.0.
- FrontComposer currently pins `Microsoft.FluentUI.AspNetCore.Components` to `5.0.0-rc.2-26098.1`; do not opportunistically upgrade frontend packages in this story.
- Fluent UI Blazor DataGrid documentation continues to cover grid UI strings and multiline/table rendering concerns; prefer existing FrontComposer DataGrid abstractions before direct component use. [Source: https://fluentui-blazor.azurewebsites.net/datagrid]
- ASP.NET Core XSS guidance for .NET 10 and Blazor security guidance both support the same implementation rule for this story: render untrusted data as text and avoid raw markup APIs. [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0] [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]

### Implementation Guardrails

- Do not create new GDPR backend workflows when existing admin endpoints cover the requirement.
- Do not bypass `[Authorize(Policy = "Admin")]` or infer permission from frontend role display alone.
- Do not display erased personal data from stale detail state.
- Do not block consent actions merely because a party is restricted.
- Do not create batch restriction, party-wide consent, tenant-wide export, or DPO case-management features in this story.
- Do not duplicate Tenants administration in Parties.
- Do not add production UI dependencies to `Hexalith.Parties.Contracts`.
- Do not use root-level or nested submodule update commands.

### Testing Requirements

- Component tests cover erasure confirmation, status display, certificate/report display, retry affordance, restriction, lift restriction, consent add/revoke/history, export, processing records, and DPO summary entry points.
- Security tests cover raw HTML/script-like values in party fields, consent purpose, ProblemDetails details, processing summaries, and export metadata.
- Authorization tests cover no token, no tenant claim, regular user/no admin role, cross-tenant scoped id, and tenant-switch clearing.
- Transport tests assert method, route, and body for each existing admin endpoint.
- Backend suites remain green when contracts are unchanged.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.2: Admin Portal - GDPR Operations]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Administration & Frontend (v1.2)]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]
- [Source: _bmad-output/planning-artifacts/prd.md#GDPR Compliance (v1.1)]
- [Source: _bmad-output/planning-artifacts/prd.md#Administration & Frontend (v1.2)]
- [Source: _bmad-output/planning-artifacts/architecture.md#GDPR Compliance (v1.1)]
- [Source: _bmad-output/implementation-artifacts/9-3-right-to-erasure-and-verification.md]
- [Source: _bmad-output/implementation-artifacts/9-4-consent-management-restriction-and-portability.md]
- [Source: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md]
- [Source: src/Hexalith.Parties/Controllers/AdminController.cs]
- [Source: src/Hexalith.Parties/Controllers/PartiesController.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs]
- [Source: src/Hexalith.Parties.Contracts/ValueObjects/ConsentRecord.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/ProcessingActivityRecord.cs]
- [Source: tests/Hexalith.Parties.Tests/Controllers/ErasureEndpointTests.cs]
- [Source: tests/Hexalith.Parties.Tests/Controllers/ConsentEndpointTests.cs]
- [Source: tests/Hexalith.Parties.Tests/Controllers/RestrictionEndpointTests.cs]
- [Source: tests/Hexalith.Parties.Tests/Controllers/PortabilityEndpointTests.cs]
- [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/4-3-datagrid-filtering-sorting-and-search.md]
- [Source: Hexalith.FrontComposer/_bmad-output/implementation-artifacts/5-2-http-response-handling-and-etag-caching.md]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-9.0]
- [Source: https://fluentui-blazor.azurewebsites.net/datagrid]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- 2026-05-04T10:14:12Z - party-mode review completed; applied low-risk clarifications for endpoint contract assumptions, tenant/auth state cleanup, post-command refresh behavior, read-only compliance records, XSS/logging constraints, accessibility/localization requirements, operator documentation, and focused frontend/API contract tests.

### File List

## Party-Mode Review

- Date/time: 2026-05-04T10:14:12Z
- Selected story key: 10-2-admin-portal-gdpr-operations
- Command/skill invocation used: `/bmad-party-mode 10-2-admin-portal-gdpr-operations; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: reviewers agreed the story was directionally sound but needed stronger pre-dev clarity around existing endpoint contracts, tenant/party/operation state isolation, auth and tenant-switch cleanup, post-command authoritative refresh, erasure/export terminal states, read-only compliance record ownership, XSS/logging controls, localized accessible operation states, operator documentation, and testability of GDPR flows.
- Changes applied: added Party-Mode Clarifications; added Endpoint Contract Assumptions; added Authorization and State Matrix; tightened task bullets for state isolation, erasure status/certificate states, consent command semantics, backend-authoritative processing records, audit-conscious logging, accessible focus/status behavior, operator documentation, completion notes, fake transport/API contract coverage, XSS inputs, auth/tenant cleanup, export-download behavior, and accessibility/localization assertions.
- Findings deferred: exact localized operator-facing names; whether future policy requires dual-control or DPO approval beyond existing admin endpoints; whether future backend endpoints expose stable machine-readable error codes beyond current HTTP/domain outcomes; exact polling cadence/timeouts for long-running erasure and certificate retrieval; final tenant-wide DPO summary fields if a dedicated backend summary endpoint is later added; any future async export or signed URL handoff replacing the current direct JSON export contract.
- Final recommendation: `ready-for-dev`
