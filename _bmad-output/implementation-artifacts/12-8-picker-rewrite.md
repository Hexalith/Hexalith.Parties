# Story 12.8: Picker Rewrite

Status: blocked

## Story

As a developer embedding the Party Picker,
I want the picker to query through the EventStore-fronted Parties client boundary,
so that picker integration matches the platform contract after Parties public REST is removed.

## Acceptance Criteria

1. Given `src/Hexalith.Parties.Picker`, then all party data reads use `Hexalith.Parties.Client` typed query methods over the accepted EventStore gateway/query contract from Story 12.5; the picker must not issue direct `GET api/v1/parties`, `GET api/v1/parties/search`, `GET api/v1/parties/{id}`, DAPR actor, projection actor, search-service, controller, or `Hexalith.Parties` internals calls.
2. Given the current picker behavior from Story 10.3, then the rewrite preserves debounced type-ahead search, bounded page size, loading, empty, local-only/degraded, unauthorized, forbidden, transient-failure, not-found, gone, retry, selected, clear, disabled, read-only, stale-response, and context-change cleanup states.
3. Given search results are returned through `IPartiesQueryClient.SearchPartiesAsync`, then each result displays only the fields exposed by the typed query result for the authenticated tenant/user and keeps display name, contact values, identifiers, consent text, degraded reasons, and ProblemDetails text out of storage keys, logs, telemetry dimensions, URLs, DOM event names, and filenames.
4. Given a user selects a party, then the component returns the selected party id through the existing Blazor callback and JavaScript custom event contract, with event detail containing only the durable party id and non-sensitive bounded status/type summary.
5. Given host applications embed the picker, then the component remains independently consumable as the existing Parties-owned Razor class library/custom-element package shape and continues to support host request/auth injection through the accepted Parties client/EventStore gateway configuration.
6. Given token, tenant, user, host configuration, selected id, or search options change, then the picker cancels or ignores stale in-flight work and clears visible results, selected preview data, pending requests, and non-sensitive UI status before issuing new requests.
7. Given party data, search text, host labels, backend problem details, degraded reasons, or localization resources contain untrusted content, then the picker renders them through normal encoded Razor/component text paths and never through `MarkupString`, `AddMarkupContent`, raw HTML fragments, unsafe markdown, JavaScript interpolation, or `innerHTML`.
8. Given keyboard, screen-reader, localization, and compact embedded layout use cases, then the input, result list/options, selected summary, clear action, retry action, loading/error/status area, no-results state, and disabled/read-only state remain keyboard operable, screen-reader named, localized, visibly focused, responsive, and non-color-only.
9. Given picker integration tests, then they cover the same selection scenarios as Story 10.3 and add regression tests proving old Parties REST route literals and direct transport fakes are removed from production picker code.
10. Given Wave 2 sequencing, then normal production implementation does not claim complete readiness until Story 12.5 exposes or freezes the typed Parties client query contract; if Story 12.5 remains blocked, this story may add red guardrail tests, adapter seams, and documentation only, and must record the exact client/query blocker.

## Tasks / Subtasks

- [x] Confirm predecessor gates and current transport contract. (AC: 1, 5, 9, 10)
  - [x] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`.
  - [x] Confirm `IPartiesQueryClient.SearchPartiesAsync` is backed by EventStore `POST /api/v1/queries`, or stop normal implementation and add only red guardrail tests/adapter seams.
  - [x] Do not edit `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Tenants`, or `Hexalith.Memories` submodules for this story.

- [ ] Replace the picker direct REST transport with the typed Parties query client. (AC: 1, 2, 3, 5, 10)
  - [x] Replace `PartyPickerApiClient` or its internals so search goes through `IPartiesQueryClient.SearchPartiesAsync(query, page, pageSize, ct)` or the accepted successor method from Story 12.5.
  - [x] Remove production hard-coded old route literals such as `api/v1/parties/search`, `api/v1/parties`, and `api/v1/parties/{id}` from `src/Hexalith.Parties.Picker`.
  - [x] Keep page size bounded by `PartyPickerDefaults.MaxPageSize` and preserve query normalization that strips control characters.
  - [x] Preserve host-controlled auth/request customization by flowing it through the configured Parties client/EventStore gateway path; do not store, refresh, parse, or log JWTs in the picker.
  - [x] If rich search status metadata is not yet available through the typed client, expose a bounded `unknown/not available` state rather than fabricating local-only, degraded, semantic, hybrid, graph, email, or identifier matching.

- [ ] Preserve and tighten component behavior. (AC: 2, 4, 6, 8)
  - [ ] Keep `PartyPicker` public parameters source-compatible where practical: selected id, selected callback, disabled/read-only, debounce, labels, result template, custom-event dispatch, page size, search mode/case options where the client contract still supports them.
  - [ ] If `ApiBaseUrl` remains, document and enforce that it points to the EventStore gateway/Parties client configuration rather than a Parties actor-host REST endpoint; otherwise replace it with a clearer options shape.
  - [ ] Keep stale-search versioning/cancellation so older tenant/user/token/query responses cannot repopulate the result list after context changes.
  - [ ] Keep `party-selected` or the accepted existing JavaScript custom event payload limited to party id plus non-sensitive summary; do not include query text, tenant ids, names, contacts, identifiers, raw status details, or tokens.
  - [ ] Keep compact layout dimensions stable so loading/error text, localized labels, long names, badges, and clear/retry controls do not shift or overlap.

- [ ] Preserve privacy, authorization, and safe rendering. (AC: 3, 6, 7)
  - [ ] Treat missing auth/request context as authentication required before any client call.
  - [ ] Map client/EventStore authorization, forbidden, not-found, gone/erased, degradation, timeout, cancellation, malformed response, and transient transport failures into existing bounded picker states.
  - [ ] Clear visible results and selected preview data after `401`, `403`, tenant/user/token/configuration change, sign-out, context mismatch, or stale response.
  - [ ] Render party display names, host labels, status messages, degraded reasons, ProblemDetails details, and localization values as encoded text only.
  - [ ] Do not log or persist party names, contact values, identifiers, consent details, search text, JWTs, claims, tenant ids, membership dictionaries, raw query payloads, or raw backend error details.

- [ ] Update package/DI registration and adopter documentation. (AC: 5, 10)
  - [ ] Update `PartyPickerServiceCollectionExtensions` so consumers get the picker services plus the required Parties client/EventStore gateway services without direct DAPR, actor, server, MVC, MediatR, FluentValidation, or projection dependencies.
  - [ ] Keep `Hexalith.Parties.Contracts` free of Blazor, Fluent UI, FrontComposer, custom-element, JavaScript, and EventStore transport dependencies.
  - [ ] Keep custom-element registration in the picker package/host adapter only; do not move Parties domain UI into `Hexalith.FrontComposer`.
  - [ ] Update picker usage docs/examples to show EventStore/Parties client configuration, selected-id persistence rules, privacy rules, and the behavior when rich search metadata is unavailable.

- [ ] Rewrite focused tests and guardrails. (AC: 1-10)
  - [x] Update `tests/Hexalith.Parties.Picker.Tests/**` so service tests use `IPartiesQueryClient` fakes rather than `HttpMessageHandler` fakes for old REST URLs.
  - [ ] Preserve tests for initial render, debounce, search success, bounded page size, loading, empty, local-only/degraded or metadata-unavailable states, unauthorized, forbidden, not-found/gone, transient failure, retry, selected, clear, disabled/read-only, configuration-change cleanup, and stale-response suppression.
  - [ ] Preserve selection contract tests for Blazor callback and JavaScript event payload.
  - [ ] Preserve XSS tests for display name, contact value, identifier value, host labels, degraded reasons, ProblemDetails text, and localized labels.
  - [x] Add architecture/source tests proving `src/Hexalith.Parties.Picker` contains no old Parties REST route literals, no direct `HttpClient.GetAsync`/`SendAsync` transport to Parties routes, no DAPR actor/projection/search-service usage, and no raw markup APIs for untrusted values.
  - [x] Add package/reference tests proving the picker does not reference `Hexalith.Parties`, `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, DAPR, MediatR, FluentValidation, MVC controllers, Swagger/OpenAPI, or EventStore server assemblies.

- [ ] Verify the rewritten picker. (AC: 1-10)
  - [x] Run `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release`.
  - [x] Run affected contracts/package fitness tests.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [x] If Story 12.5 remains blocked or the EventStore query contract is not frozen, record the exact limitation and do not mark unsupported transport behavior complete.

## Dev Notes

### Source Context

- Epic 12 is sourced from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; that proposal is authoritative for Story 12.8.
- Story 12.8 supersedes the consumer-facing scope of Story 10.3. The old picker story is behavior inventory, not an approved transport model.
- The Epic 12 pivot decision is that all consumer commands and queries go to EventStore. `Hexalith.Parties` becomes an actor host/projection runtime and must not keep public REST or in-process MCP as the primary consumer surface.
- Story 12.8 is Wave 2. It depends on Story 12.5 because the picker should consume `Hexalith.Parties.Client`, and Story 12.5 is currently blocked until Wave 1 contracts land or freeze.
- No `project-context.md` persistent fact file was found during story creation.

### Current Implementation to Inspect

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` contains the current component API, debounce/cancellation/versioning, selected state, labels, result rendering, retry, clear, JavaScript event dispatch, and context-change reset behavior.
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs` currently builds `GET api/v1/parties/search` and reads search-status headers directly. This is the main obsolete transport path to replace.
- `src/Hexalith.Parties.Picker/Services/PartyPickerSearchRequest.cs`, `PartyPickerSearchResponse.cs`, `PartyPickerSearchMetadata.cs`, `PartyPickerSearchState.cs`, and `PartyPickerSelection.cs` define the current picker state/result contracts. Reuse them where they still fit; do not create a parallel state model unless it removes the old REST coupling.
- `src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs` currently registers only `PartyPickerApiClient`. Update this registration around the typed Parties client boundary.
- `src/Hexalith.Parties.Picker/Extensions/PartyPickerCustomElementExtensions.cs` and `wwwroot/hexalith-parties-picker.js` own custom-element registration. Keep JavaScript adapter work in the picker package or host adapter.
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs` currently asserts old REST URL construction. Rewrite these tests around `IPartiesQueryClient` fakes and add old-route absence checks.
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` already covers accessible render, localization labels, encoded results, local-only/degraded display, missing-token behavior, context cleanup, and event payload privacy. Preserve this scenario coverage.
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` currently exposes `SearchPartiesAsync(string query, int page, int pageSize, CancellationToken ct)`. Use this or the accepted successor from Story 12.5.
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs` currently calls old Parties REST URLs. If that remains true when implementation starts, Story 12.8 normal transport work is blocked by Story 12.5.

### Current Behavior Inventory From Story 10.3

- The picker is an embeddable Blazor/FrontComposer-aligned component, not an admin portal, tenant selector, standalone SPA, GDPR surface, party editor, or MCP UI.
- The selected value contract is the stable party id. Display names, emails, identifiers, and contact values are preview data only and must not become the host application's durable key.
- The component supports host-provided auth/request customization and must never persist, refresh, parse for authorization, or log tokens.
- Empty query behavior must remain intentional. The current picker does not issue unbounded initial browse calls.
- Rich search metadata was previously read from REST headers. After the EventStore/client rewrite, keep only metadata the typed client actually exposes.

### EventStore and Parties Client Guidance

- EventStore query ingress is `POST /api/v1/queries`; the typed Parties client should hide this transport from picker components.
- Parties query envelopes use `Domain="party"` unless a later accepted architecture update changes the domain.
- The picker should be boring: normalize UI input, call a typed query client, render bounded states, and report selected party id.
- Do not bypass a missing client feature by calling EventStore server DTOs directly from the picker, old Parties REST endpoints, DAPR actors, projection actors, `IPartySearchService`, controllers, or actor-host internals.
- If query response metadata needed for local-only/degraded states is missing from `IPartiesQueryClient`, record the client-contract gap and keep UI copy bounded.

### Technical Constraints

- Keep package versions aligned with the repository: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, Microsoft Fluent UI Blazor `5.0.0-rc.2-26098.1`, xUnit `2.9.3`, Shouldly `4.3.0`, and NSubstitute `5.3.0`.
- Do not initialize or update nested submodules. `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` are root-level submodules already present.
- Keep production picker code in `src/Hexalith.Parties.Picker` and tests in `tests/Hexalith.Parties.Picker.Tests`.
- Keep public domain contracts in `src/Hexalith.Parties.Contracts`; do not add UI framework, custom-element, or transport dependencies there.
- Do not add DAPR, MediatR, FluentValidation, MVC controller, Swagger/OpenAPI, EventStore server, Parties actor-host, projection actor, search-service, or security-internal references to the picker.
- Do not resurrect old Parties REST, admin REST, OpenAPI, Swagger, or in-process MCP URLs as compatibility shims.

### Latest Technical Information

- Microsoft Learn for ASP.NET Core 10 documents Blazor custom elements as a way to render Razor components from JavaScript technologies such as Angular, React, and Vue, with parameters passed via attributes or DOM properties. Custom element tag names use kebab-case. Source: https://learn.microsoft.com/aspnet/core/blazor/components/js-spa-frameworks?view=aspnetcore-10.0
- Microsoft Learn for ASP.NET Core 10 documents Razor class libraries as the reusable packaging model for Razor components, code, and static assets; RCL static assets are exposed under `_content/{PACKAGE ID}/{PATH}`. Source: https://learn.microsoft.com/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0
- Microsoft Learn Blazor security guidance states normal Razor string rendering writes text and avoids XSS, while raw markup APIs such as `AddMarkupContent` or `MarkupString` with untrusted input can introduce XSS. Source: https://learn.microsoft.com/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-10.0

### Security and Privacy Guidance

- EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping after the pivot.
- Parties owns domain execution behind EventStore. The picker must not reimplement tenant authorization, domain validation, projection repair, stream browsing, or actor invocation.
- Safe UI feedback may include bounded state labels, retry guidance, and non-PII status categories.
- Unsafe feedback includes raw query payload JSON, protected PII, display names in logs/storage keys, contact values, identifiers, consent purpose text, access tokens, signing keys, stack traces, DAPR ports, sidecar names, tenant membership dictionaries, and internal configuration.
- Treat JavaScript event inputs and host-provided values as untrusted. Validate or bound them before use and never put them directly into raw markup, URLs, storage keys, or logs.

### Testing Guidance

- Minimum focused tests:
  - Component calls `IPartiesQueryClient.SearchPartiesAsync` or the accepted successor instead of constructing old REST URLs.
  - Source/fitness tests prove `api/v1/parties`, `api/v1/parties/search`, and direct Parties REST route literals are absent from production picker code.
  - Search states remain covered for success, empty, auth required, unauthorized, forbidden, not found, gone, local-only/degraded or metadata unavailable, transient failure, malformed client response, cancellation, retry, disabled/read-only, and stale-response suppression.
  - Context-change tests prove token/tenant/user/configuration changes clear visible results and selected preview state before a new query can render.
  - Selection contract tests prove the Blazor callback and JavaScript event expose only party id plus bounded non-sensitive summary.
  - XSS/privacy tests cover display name, contact value, identifier value, host labels, degraded reasons, ProblemDetails detail, and localized labels.
  - Package/reference tests prove the picker has no forbidden service/server/projection/DAPR/MediatR/FluentValidation/MVC dependencies.

### Out of Scope

- Rewriting `Hexalith.Parties.Client` transport; Story 12.5 owns that.
- Implementing the separate Parties MCP host; Story 12.6 owns that.
- Rebuilding the Admin Portal; Story 12.7 owns that.
- Updating samples/getting-started docs; Story 12.9 owns that.
- Rewriting deployment validation/topology fitness; Story 12.10 owns that.
- Adding party create/edit/delete, GDPR operations, tenant lifecycle, membership, role, tenant configuration, Memories management, EventStore stream browsing, or MCP-only graph-search UX to the picker.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.8 source, Epic 12 pivot rationale, Wave 2 sequencing, and superseded picker scope.
- `_bmad-output/planning-artifacts/epics.md` - Story 10.3 original picker requirements and FR67 mapping.
- `_bmad-output/planning-artifacts/prd.md` - FR67 and frontend/XSS requirements.
- `_bmad-output/implementation-artifacts/10-3-embeddable-party-picker-component.md` - committed pre-pivot picker behavior inventory.
- `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md` - typed client boundary and current blocker for normal picker transport work.
- `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md` - consumer-host separation and EventStore client usage pattern.
- `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md` - FrontComposer/EventStore consumer migration guidance.
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` - current picker component.
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs` - old direct REST transport to replace.
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` - typed query client seam to consume.
- `tests/Hexalith.Parties.Picker.Tests/**` - current behavior and privacy test inventory.

## Project Structure Notes

- Keep picker production code in `src/Hexalith.Parties.Picker`.
- Keep picker tests in `tests/Hexalith.Parties.Picker.Tests`.
- Keep typed query/command client behavior in `src/Hexalith.Parties.Client`.
- Keep typed domain models in `src/Hexalith.Parties.Contracts`.
- Generated `bin/`, `obj/`, browser artifacts, screenshots, and test result outputs must stay out of commits unless a test explicitly owns them.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-05-10: Confirmed predecessor gate: Story 12.4 is `blocked`; Story 12.5 is `blocked`; `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs` still calls old Parties REST URLs, so `IPartiesQueryClient.SearchPartiesAsync` is not yet backed by EventStore `POST /api/v1/queries`.
- 2026-05-10: Limited implementation mode only. Added picker adapter seam over `IPartiesQueryClient.SearchPartiesAsync`, removed old Parties REST route literals from production picker source, and added guardrail tests preventing their return.
- 2026-05-10: Focused picker tests passed: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` (28/28).
- 2026-05-10: Client tests passed: `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release` (56 passed, 6 skipped).
- 2026-05-10: Contracts/package fitness check passed: `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release` (42 passed, 15 skipped).
- 2026-05-10: Solution build passed: `dotnet build Hexalith.Parties.slnx --configuration Release` (0 warnings, 0 errors).

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story remains blocked for full production readiness because Story 12.5 is blocked and `IPartiesQueryClient.SearchPartiesAsync` is still implemented over old Parties REST in `HttpPartiesQueryClient`.
- Added the allowed adapter seam: the picker-owned `PartyPickerApiClient` now consumes `IPartiesQueryClient.SearchPartiesAsync` instead of constructing `GET api/v1/parties/search`.
- Preserved query normalization, page-size bounding, authentication-required preflight, bounded client error states, selected-id contract, and encoded Razor rendering behavior covered by existing component tests.
- Rich search metadata is currently unavailable through the typed client seam; the picker records bounded unavailable metadata instead of fabricating local-only/degraded details.
- Added guardrails proving production picker source has no retired Parties REST route literals, direct `HttpClient`/`GetAsync`/`SendAsync` transport markers, or raw markup markers.
- Updated adopter docs to describe EventStore gateway/Parties client configuration and the current metadata limitation.

### File List

- `_bmad-output/implementation-artifacts/12-8-picker-rewrite.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/frontend/party-picker.md`
- `src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-10 | 0.2 | Added typed-client picker adapter seam and guardrail tests, verified focused/client/contracts/build checks, and blocked full completion on Story 12.5 EventStore query contract. | Codex |
| 2026-05-10 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
