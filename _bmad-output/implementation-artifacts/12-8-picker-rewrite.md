# Story 12.8: Picker Rewrite

Status: done

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

- [x] Replace the picker direct REST transport with the typed Parties query client. (AC: 1, 2, 3, 5, 10)
  - [x] Replace `PartyPickerApiClient` or its internals so search goes through `IPartiesQueryClient.SearchPartiesAsync(query, page, pageSize, ct)` or the accepted successor method from Story 12.5.
  - [x] Remove production hard-coded old route literals such as `api/v1/parties/search`, `api/v1/parties`, and `api/v1/parties/{id}` from `src/Hexalith.Parties.Picker`.
  - [x] Keep page size bounded by `PartyPickerDefaults.MaxPageSize` and preserve query normalization that strips control characters.
  - [x] Preserve host-controlled auth/request customization by flowing it through the configured Parties client/EventStore gateway path; do not store, refresh, parse, or log JWTs in the picker.
  - [x] If rich search status metadata is not yet available through the typed client, expose a bounded `unknown/not available` state rather than fabricating local-only, degraded, semantic, hybrid, graph, email, or identifier matching.

- [x] Preserve and tighten component behavior. (AC: 2, 4, 6, 8)
  - [x] Keep `PartyPicker` public parameters source-compatible where practical: selected id, selected callback, disabled/read-only, debounce, labels, result template, custom-event dispatch, page size, search mode/case options where the client contract still supports them.
  - [x] If `ApiBaseUrl` remains, document and enforce that it points to the EventStore gateway/Parties client configuration rather than a Parties actor-host REST endpoint; otherwise replace it with a clearer options shape.
  - [x] Keep stale-search versioning/cancellation so older tenant/user/token/query responses cannot repopulate the result list after context changes.
  - [x] Keep `party-selected` or the accepted existing JavaScript custom event payload limited to party id plus non-sensitive summary; do not include query text, tenant ids, names, contacts, identifiers, raw status details, or tokens.
  - [x] Keep compact layout dimensions stable so loading/error text, localized labels, long names, badges, and clear/retry controls do not shift or overlap.

- [x] Preserve privacy, authorization, and safe rendering. (AC: 3, 6, 7)
  - [x] Treat missing auth/request context as authentication required before any client call.
  - [x] Map client/EventStore authorization, forbidden, not-found, gone/erased, degradation, timeout, cancellation, malformed response, and transient transport failures into existing bounded picker states.
  - [x] Clear visible results and selected preview data after `401`, `403`, tenant/user/token/configuration change, sign-out, context mismatch, or stale response.
  - [x] Render party display names, host labels, status messages, degraded reasons, ProblemDetails details, and localization values as encoded text only.
  - [x] Do not log or persist party names, contact values, identifiers, consent details, search text, JWTs, claims, tenant ids, membership dictionaries, raw query payloads, or raw backend error details.

- [x] Update package/DI registration and adopter documentation. (AC: 5, 10)
  - [x] Update `PartyPickerServiceCollectionExtensions` so consumers get the picker services plus the required Parties client/EventStore gateway services without direct DAPR, actor, server, MVC, MediatR, FluentValidation, or projection dependencies.
  - [x] Keep `Hexalith.Parties.Contracts` free of Blazor, Fluent UI, FrontComposer, custom-element, JavaScript, and EventStore transport dependencies.
  - [x] Keep custom-element registration in the picker package/host adapter only; do not move Parties domain UI into `Hexalith.FrontComposer`.
  - [x] Update picker usage docs/examples to show EventStore/Parties client configuration, selected-id persistence rules, privacy rules, and the behavior when rich search metadata is unavailable.

- [x] Rewrite focused tests and guardrails. (AC: 1-10)
  - [x] Update `tests/Hexalith.Parties.Picker.Tests/**` so service tests use `IPartiesQueryClient` fakes rather than `HttpMessageHandler` fakes for old REST URLs.
  - [x] Preserve tests for initial render, debounce, search success, bounded page size, loading, empty, local-only/degraded or metadata-unavailable states, unauthorized, forbidden, not-found/gone, transient failure, retry, selected, clear, disabled/read-only, configuration-change cleanup, and stale-response suppression.
  - [x] Preserve selection contract tests for Blazor callback and JavaScript event payload.
  - [x] Preserve XSS tests for display name, contact value, identifier value, host labels, degraded reasons, ProblemDetails text, and localized labels.
  - [x] Add architecture/source tests proving `src/Hexalith.Parties.Picker` contains no old Parties REST route literals, no direct `HttpClient.GetAsync`/`SendAsync` transport to Parties routes, no DAPR actor/projection/search-service usage, and no raw markup APIs for untrusted values.
  - [x] Add package/reference tests proving the picker does not reference `Hexalith.Parties`, `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, DAPR, MediatR, FluentValidation, MVC controllers, Swagger/OpenAPI, or EventStore server assemblies.

- [x] Verify the rewritten picker. (AC: 1-10)
  - [x] Run `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release`.
  - [x] Run affected contracts/package fitness tests.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [x] If Story 12.5 remains blocked or the EventStore query contract is not frozen, record the exact limitation and do not mark unsupported transport behavior complete.

### Review Findings

_Code review performed 2026-05-13 (bmad-code-review). 3 decisions resolved, 19 patches applied, 6 deferred, 8 dismissed. Picker tests 53/53, Client 74/74, Contracts 57/57, full Release solution build clean. See `_bmad-output/implementation-artifacts/deferred-work.md` for deferred items._

#### Decisions (resolved 2026-05-13)

- [x] [Review][Decision] Generic exception catch policy in `PartyPickerApiClient.SearchAsync` — RESOLVED: add defensive `catch (Exception)` mapping to a bounded `Error` state without leaking exception text. Converted to patch P18 below.
- [x] [Review][Decision] `xunit.v3` framework upgrade scope creep — RESOLVED: intentional. Picker test project stays on `xunit.v3`. Upgrade of remaining test projects tracked as a separate cross-cutting concern.
- [x] [Review][Decision] `AddHexalithPartyPicker(configuration)` double-registration risk — RESOLVED: guard with `IsRegistered<IPartiesQueryClient>()` (or `TryAdd*`) check before calling `AddPartiesClient(configuration)`. Converted to patch P19 below.

#### Patches

- [x] [Review][Patch] `ApiBaseUrl`/`PartyPickerSearchRequest.ApiBaseAddress` is dead code [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:88,260`, `src/Hexalith.Parties.Picker/Services/PartyPickerSearchRequest.cs:5`] — `ApiBaseAddress` is built from `ApiBaseUrl` and threaded onto `PartyPickerSearchRequest`, but the new `PartyPickerApiClient` never reads it. AC5 required either documenting the parameter or replacing it. Remove `ApiBaseAddress` from the request DTO and either delete `ApiBaseUrl` or mark it `[Obsolete]` with explanation.
- [x] [Review][Patch] Dead `catch (HttpRequestException)` in `ScheduleSearchAsync` [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:243-254`] — `PartyPickerApiClient` catches `HttpRequestException` internally and returns a Response with SafeReason; `Task.Delay` cannot throw it. The razor catch is unreachable. Delete it.
- [x] [Review][Patch] `Page` not sanitized on failure/auth/idle responses [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:24,117,140`] — `SearchPartiesAsync` is called with `Math.Max(1, request.Page)` but `AuthenticationRequired`, `FailureResponse`, and the empty/Idle branches return raw `request.Page`. UI page state diverges between success and failure for the same input. Apply the same `Math.Max(1, …)` everywhere or compute once.
- [x] [Review][Patch] `payload.Items` null-deref in `ToSearchResponse` [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:157`] — `payload.Items.Count` throws `NullReferenceException` if the typed client deserializes a JSON body with `"items": null`. The `required` keyword only enforces initialization, not non-null. Null-guard `payload.Items` and map to `Empty`.
- [x] [Review][Patch] Null `Party`/`DisplayName` in result NPE during render [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:59,63`, `PartyPickerApiClient.cs:171`] — `@result.Party.DisplayName` throws if `Party` is null. Filter null `Party` items in `ToSearchResponse` or null-coalesce in the razor template.
- [x] [Review][Patch] `JSException` not caught in `DispatchSelectedDomEventAsync` [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:341-346`] — Only `InvalidOperationException` and `JSDisconnectedException` are caught. `Microsoft.JSInterop.JSException` (e.g., module import 404, JS-side throw) escapes and breaks the Blazor circuit. Add `catch (JSException)`.
- [x] [Review][Patch] Clear button ignores `Disabled`/`ReadOnly` [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:19`] — `disabled="@(_selected is null && string.IsNullOrWhiteSpace(_query))"` lets the user mutate selection state via the clear button while the picker is disabled or read-only. Add `IsInteractionDisabled` to the disabled binding.
- [x] [Review][Patch] Stub `PartyPickerSelection` from `SelectedPartyId` renders as "Active" [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:190-193,395-400`] — Host-set `SelectedPartyId` produces `new PartyPickerSelection { PartyId = … }` with null `IsActive`/`IsErased`. `StatusText` falls through to `EffectiveLabels.Active`, mislabeling erased/inactive parties. Either omit the badge for stub selections or fetch detail via `IPartiesQueryClient.GetPartyAsync`.
- [x] [Review][Patch] Asymmetric `SearchStatus = "Unavailable"` [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:157-178`] — Ready branch sets `Metadata.SearchStatus = "Unavailable"`, Empty branch omits `Metadata`. Set consistently across both branches (or null in both) so consumers can detect "metadata unavailable" deterministically.
- [x] [Review][Patch] Transport guardrail misses AC1 forbidden tokens [`tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs:7-15`] — AC1 enumerates DAPR actors, projection actors, `IPartySearchService`, controller routes; the guardrail only checks `api/v1/parties`, `api/v1/parties/search`, and HTTP basics. Add InlineData for `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, `PartyActor`, `IPartySearchService`, and `Dapr.Actors`.
- [x] [Review][Patch] `PartyPickerSearchMetadata` retains dead fields [`src/Hexalith.Parties.Picker/Services/PartyPickerSearchMetadata.cs`] — `ServiceDegraded`, `StaleDataAge`, `DegradedReason` and computed `IsLocalOnly`/`IsDegraded` are never populated by the new client. Dev Notes mandate "keep only metadata the typed client actually exposes." Either prune to `{ SearchStatus }` only or mark the others `[Obsolete]` with rationale.
- [x] [Review][Patch] `DelegateFingerprint` over-fires reset on per-render lambdas [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:281-285`] — `RuntimeHelpers.GetHashCode(Delegate)` changes when hosts pass a new lambda per render (a common Blazor pattern), flushing visible results and selection state each render. When `AuthContextKey` is set, prefer it; when both are null, document the requirement that delegates must be hoisted to fields by the host.
- [x] [Review][Patch] `HasHostRequestContextAsync` token-provider throws not handled [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:29,82-86`] — The call is outside the try/catch in `SearchAsync`. A throwing host token provider (cache failure, MSAL refresh exception) escapes `SearchAsync` and leaves the razor on `Loading`. Wrap in try/catch and map to `AuthenticationRequired` or `Error`.
- [x] [Review][Patch] Guardrail substring matching is brittle [`tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs:18-28`] — Whole-file substring matches on `"HttpClient"`/`"MarkupString"` will fail the build if a future XML doc comment references `<see cref="HttpClient"/>`. Strip XML doc and string comments before matching, or use a tokenized check.
- [x] [Review][Patch] Cross-test nested-type coupling [`tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs:296,334`, `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs:203-267`] — `PartyPickerComponentTests` reaches into `PartyPickerApiClientTests.RecordingPartiesQueryClient`/`SearchCall` (nested `internal` types of another test class). Extract `RecordingPartiesQueryClient` and `SearchCall` to a top-level `Hexalith.Parties.Picker.Tests.Fakes` namespace.
- [x] [Review][Patch] `DelayedPartiesQueryClient` does not record Last\* properties [`tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs:345-357`] — Override does not call `base.SearchPartiesAsync` and only mutates `SearchCalls`, so `LastMode`/`LastCaseId`/`LastRequestCustomizer` remain null when this fake is used. Either record in the override or delegate via `base`.
- [x] [Review][Patch] `ReadOnly` sets HTML `disabled` attribute, violating AC8 keyboard operability [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:15`] — `disabled="@IsInteractionDisabled"` where `IsInteractionDisabled = Disabled || ReadOnly` makes the input non-focusable in read-only mode. AC8 requires "disabled/read-only state remain keyboard operable". Use `readonly="@ReadOnly"` and `disabled="@Disabled"` as distinct attributes.
- [x] [Review][Patch] P18 — Defensive `catch (Exception)` in `SearchAsync` [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:34-72`] — Resolved from D1. Add a final `catch (Exception)` after the existing handlers that maps to `PartyPickerSearchState.Error` with a bounded `SafeReason` ("The Parties client could not complete the request.") and does not include the exception message/stack in the response.
- [x] [Review][Patch] P19 — Guard `AddHexalithPartyPicker(configuration)` against double registration [`src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs:11-18`] — Resolved from D3. Only call `services.AddPartiesClient(configuration)` if `IPartiesQueryClient` is not already registered (e.g., `services.Any(d => d.ServiceType == typeof(IPartiesQueryClient))`).

#### Deferred

- [x] [Review][Defer] `HttpRequestException` catch ignores `.StatusCode` [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:63`] — deferred, depends on `HttpPartiesQueryClient` contract guarantees owned by Story 12.5; pre-existing seam.
- [x] [Review][Defer] `CreateRequestCustomizer` silent unauth on second token-provider call [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:100-104`] — deferred, server-side 401 recovery is acceptable UX.
- [x] [Review][Defer] No upper bound on `request.Page` [`src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:39`] — deferred, server-side rejection is acceptable.
- [x] [Review][Defer] `SelectAsync` race against newer search re-render [`src/Hexalith.Parties.Picker/Components/PartyPicker.razor:295`] — deferred, Blazor render cycle serializes onclick handlers; race is unlikely in practice.
- [x] [Review][Defer] Substring assertions `ToString().ShouldNotContain("tenant"/"token")` [`tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs:289-290,314-315`] — deferred, pre-existing test pattern; minor false-positive risk on host display names.
- [x] [Review][Defer] Unrelated Gateway test failure `PostCommands_InvalidGatewayShape_Returns400BeforePartyInvocationAsync` — deferred, outside picker footprint; triage in EventStore Gateway routing.

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
- 2026-05-13: Resumed normal implementation after confirming Story 12.5 is `done` and `HttpPartiesQueryClient.SearchPartiesAsync` submits EventStore `POST /api/v1/queries` requests for `Domain="party"` / `QueryType="PartySearch"`.
- 2026-05-13: Picker focused tests passed after adding coverage for request-customizer-only hosts, malformed client responses, transient transport failures, disabled/read-only suppression, retry, gone/erased mapping, stale-response suppression, and selection callback privacy: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release --no-restore` (39/39).
- 2026-05-13: Client tests passed: `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --no-restore` (74/74).
- 2026-05-13: Contracts/package fitness tests passed: `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --no-restore` (57/57).
- 2026-05-13: Release solution build passed after restore metadata refresh: `dotnet build Hexalith.Parties.slnx --configuration Release` (0 warnings, 0 errors). The first `--no-restore` build attempt failed on stale AppHost extern-alias metadata and passed after normal restore.
- 2026-05-13: Full solution no-build regression did not complete within 15 minutes. Project/slice regression evidence: AdminPortal 67/67, MCP 21/21, Server 177/177, Projections 67/67, Security 129/129, Sample 52/52, DeployValidation 40/40, Integration 18 passed / 6 expected skips, root Tenants 5/5, Search non-performance 89/89, root Projections 15/15, HealthChecks 35/35, Fitness 38/38, ErrorHandling/Domain/Configuration/Authorization 43/43, Search performance 8/8.
- 2026-05-13: Root Gateway slice has one reproducible unrelated failure outside this story footprint: `PostCommands_InvalidGatewayShape_Returns400BeforePartyInvocationAsync` expects `400 BadRequest` but receives `202 Accepted` (Gateway slice 13 passed / 1 failed). This story changed only picker/story tracking files and did not modify Gateway/EventStore routing code.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story remains blocked for full production readiness because Story 12.5 is blocked and `IPartiesQueryClient.SearchPartiesAsync` is still implemented over old Parties REST in `HttpPartiesQueryClient`.
- Added the allowed adapter seam: the picker-owned `PartyPickerApiClient` now consumes `IPartiesQueryClient.SearchPartiesAsync` instead of constructing `GET api/v1/parties/search`.
- Preserved query normalization, page-size bounding, authentication-required preflight, bounded client error states, selected-id contract, and encoded Razor rendering behavior covered by existing component tests.
- Rich search metadata is currently unavailable through the typed client seam; the picker records bounded unavailable metadata instead of fabricating local-only/degraded details.
- Added guardrails proving production picker source has no retired Parties REST route literals, direct `HttpClient`/`GetAsync`/`SendAsync` transport markers, or raw markup markers.
- Updated adopter docs to describe EventStore gateway/Parties client configuration and the current metadata limitation.
- Cleared the Story 12.5 blocker and completed the normal picker rewrite over the typed EventStore-backed Parties query client.
- Added bounded malformed-client-response handling so invalid typed client failures surface as a safe picker error state without leaking backend details.
- Expanded picker behavior coverage for request customizers, malformed/transport failures, disabled/read-only behavior, retry, gone/erased handling, stale response suppression, and selection callback privacy.
- Full solution build and all picker/client/contracts/story-relevant suites are green; one unrelated root Gateway test remains failing outside the picker footprint and is logged in Debug Log References.

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
| 2026-05-13 | 1.0 | Completed Story 12.8 after Story 12.5 unblocked: verified EventStore-backed typed query transport, added safe malformed-client mapping, expanded picker behavior/privacy tests to 39/39, reran client/contracts/build/regression slices, and moved story to review with one unrelated Gateway failure logged. | Codex |
| 2026-05-10 | 0.2 | Added typed-client picker adapter seam and guardrail tests, verified focused/client/contracts/build checks, and blocked full completion on Story 12.5 EventStore query contract. | Codex |
| 2026-05-10 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
