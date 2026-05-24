# Story 8.1: Compose Embeddable Party Picker Shell

Status: done

<!-- 2026-05-23: correct-course approved a scoped Story 8.1 risk acceptance. This story is ready-for-dev, but the broader EventStore-fronted Parties client/gateway dependency remains Required for later Epic 8 picker stories unless separately accepted or satisfied. -->
<!-- 2026-05-23: dev-story moved ready-for-dev -> in-progress -> review. Reconciled the existing Hexalith.Parties.Picker RCL against the accepted IPartiesQueryClient bridge; focused picker suite 65/65 green. -->
<!-- 2026-05-23: code-review closed Story 8.1. Five review patches applied; focused picker suite 70/70 green. -->

## Story

As a consuming application developer,
I want an embeddable FrontComposer/Blazor party picker component,
so that my application can offer party selection without building its own party search UI.

## Acceptance Criteria

1. Given a host application references the picker component, when it renders the picker, then the picker appears as an embeddable party search and selection control, and it is not an admin portal, party editor, tenant selector, GDPR surface, or EventStore stream browser.
2. Given the host supplies request/auth context through accepted Parties client or EventStore gateway configuration, when the picker initializes, then it uses that configuration for queries, and it does not persist, refresh, parse for authorization, or log tokens.
3. Given the picker is disabled or read-only, when it renders, then interactive search and selection controls are disabled appropriately, and current selection display remains stable and accessible.
4. Given the picker is embedded in different host layouts, when it renders at supported viewport/container sizes, then it maintains a bounded compact layout, and text and controls do not overlap or resize unpredictably.
5. Given picker shell tests run, when they cover enabled, disabled, read-only, missing host configuration, and embedded layout states, then the picker remains a bounded embeddable component.

## Gate Resolution (2026-05-23)

Story 8.1 is unblocked by `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23-epic8-picker-gate.md` and the scoped Story 8.1 risk acceptance now recorded in `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

The accepted temporary picker bridge for this story is the existing `Hexalith.Parties.Picker` Razor class library plus the `IPartiesQueryClient` query boundary documented in `docs/frontend/party-picker.md`. This is enough to schedule shell composition and reconciliation work for Story 8.1, but it does not mark the full EventStore-fronted Parties client/gateway dependency `Satisfied`.

Stories 8.2, 8.3, 8.4, 8.5, and 8.6 remain governed by the dependency gate unless separately risk-accepted or the full dependency is later satisfied.

## Required To Unblock

Resolved by approval on 2026-05-23:

- Updated `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` with an explicit `Risk Accepted` scope for Epic 8 Story 8.1.
- Linked the accepted temporary picker query/configuration contract from the dependency record and this story.
- Confirmed the accepted scope covers picker shell initialization, bounded host configuration, tenant-safe failure semantics, capability/contract-unavailable behavior, and privacy rules for tokens, storage, URLs, filenames, telemetry, DOM events, and callbacks.
- Confirmed the existing `Hexalith.Parties.Picker` implementation is the accepted implementation surface to reconcile for Story 8.1.

## Tasks / Subtasks

> The scheduling gate above is resolved for Story 8.1 only. These tasks are captured so the dev agent extends the existing picker instead of recreating it.

- [x] Task 1 - Reconcile the existing picker shell with the accepted EventStore-fronted contract. (AC: 1, 2)
  - [x] 1.1 Read the existing `src/Hexalith.Parties.Picker` project in full before coding. Do not scaffold a second picker package or move picker UI into `src/Hexalith.Parties`.
  - [x] 1.2 Update `PartyPicker` initialization parameters only to match the accepted picker host contract. Preserve source compatibility where practical, especially documented host patterns in `docs/frontend/party-picker.md`.
  - [x] 1.3 Keep request/auth context host-supplied. The picker may accept an in-memory token provider/customizer, but it must not persist, refresh, parse for authorization, or log tokens.
  - [x] 1.4 If the accepted contract introduces capability detection or contract-unavailable states, surface those as bounded localized shell states instead of attempting fallback calls to retired endpoints or internals.

- [x] Task 2 - Preserve the embeddable shell boundary and compact layout. (AC: 1, 3, 4)
  - [x] 2.1 Keep the picker as a bounded search/selection control, not an admin portal, editor, tenant selector, GDPR UI, or EventStore stream browser.
  - [x] 2.2 Preserve disabled and read-only behavior: no search or selection mutation while disabled/read-only, stable selected display when supplied by the host, accessible names for the input and controls.
  - [x] 2.3 Preserve the compact CSS contract in `PartyPicker.razor.css`: stable input row, bounded max width, no overlapping long labels/names, visible focus, and non-color-only status.
  - [x] 2.4 If replacing the literal clear-button text, use an accessible icon/button pattern with the existing `ClearSelection` label and do not introduce a new visual system.

- [x] Task 3 - Preserve privacy, tenant-safety, and callback boundaries. (AC: 2, 3)
  - [x] 3.1 Do not store or log party names, contact values, identifiers, consent text, search text, tenant ids, JWTs, raw ProblemDetails, or raw query payloads.
  - [x] 3.2 Do not add durable host keys, URLs, filenames, telemetry dimensions, DOM event names, or JavaScript event payloads containing anything except the allowed stable selection data.
  - [x] 3.3 Cross-check current `PartyPickerSelection` and `PartyPickerEventDetail` against Story 8.3 before expanding callback data. Today the DOM event detail is intentionally narrow (`PartyId`, `PartyType`, `Status`); future work must not widen it with PII.
  - [x] 3.4 Render party data, host labels, degraded reasons, and backend messages through normal Razor text rendering. Do not use `MarkupString`, `AddMarkupContent`, raw HTML fragments, `innerHTML`, unsafe markdown, or JavaScript interpolation with untrusted values.

- [x] Task 4 - Update focused tests and docs after the accepted contract is known. (AC: 1-5)
  - [x] 4.1 Extend `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` for enabled, disabled, read-only, missing host configuration, contract-unavailable, compact layout, and stale context states.
  - [x] 4.2 Extend `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs` when accepted query/configuration semantics change.
  - [x] 4.3 Keep `PartyPickerTransportGuardrailTests` strict: no retired REST path construction, `HttpClient` calls, DAPR actors, projection actors, server/projection dependencies, raw markup APIs, or actor-host internals in production picker source.
  - [x] 4.4 Update `docs/frontend/party-picker.md` with the accepted host configuration pattern and remove any stale wording from the pre-pivot REST story if still present.
  - [x] 4.5 Run `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`. Run broader build/tests only if the accepted contract changes shared client or solution wiring.

### Review Findings

- [x] [Review][Patch][Applied] Disabled/read-only input events clear selection before the interaction guard [src/Hexalith.Parties.Picker/Components/PartyPicker.razor:203]
- [x] [Review][Patch][Applied] Host-supplied `SelectedPartyId` can render as a blank selected preview [src/Hexalith.Parties.Picker/Components/PartyPicker.razor:29]
- [x] [Review][Patch][Applied] Browser-storage guardrail misses string-indexed storage access [tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs:84]
- [x] [Review][Patch][Applied] Compact-layout coverage is too shallow for embedded/container and badge-overflow cases [tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs:446]
- [x] [Review][Patch][Applied] Typed-client routing test does not assert the forwarded query payload [tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs:392]

## Dev Notes

### Current Implementation Snapshot

The repository already contains a picker implementation. Treat it as the first thing to reconcile after the dependency is unblocked, not as something to replace.

- `src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj` is a Razor class library package (`PackageId=Hexalith.Parties.Picker`) referencing `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, and `Microsoft.AspNetCore.Components.CustomElements`.
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` renders the shell: label/input, clear button, selected preview, polite status region, retry button, result listbox, disabled/read-only handling, debounced search, stale-response suppression through `_searchVersion`, context-signature cleanup, and optional DOM event dispatch.
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css` contains the compact layout contract: grid layout, bounded width, stable input row, min-height status area, visible focus outlines, wrapped long names, and custom properties mapped to neutral/accent tokens.
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs` adapts to `IPartiesQueryClient`, normalizes query text, bounds page size, requires host request context, composes bearer token/request customizer, maps HTTP failures to bounded states, and avoids raw backend details.
- `src/Hexalith.Parties.Picker/Services/PartyPickerSearchRequest.cs`, `PartyPickerSearchResponse.cs`, `PartyPickerSearchState.cs`, `PartyPickerLabels.cs`, `PartyPickerSelection.cs`, and `PartyPickerEventDetail.cs` are the current shell/state/label/selection model.
- `src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs` registers the typed client adapter without adding picker dependencies to Contracts.
- `src/Hexalith.Parties.Picker/Extensions/PartyPickerCustomElementExtensions.cs` registers the custom element; `wwwroot/hexalith-parties-picker.js` dispatches the `party-selected` event.
- `docs/frontend/party-picker.md` documents Blazor usage, JavaScript custom-element usage, search behavior, privacy/state rules, and theming.
- `tests/Hexalith.Parties.Picker.Tests` already covers accessible initial render, localized labels, encoded results, no advanced search controls by default, missing auth, disabled/read-only states, retry, not-found/gone state, context/token stale cleanup, selection callback, DOM event payload privacy, typed client request composition, page-size bounds, bounded failures, packaging, and source transport guardrails.

### What This Story Changes After Unblock

Story 8.1 should only make the existing shell conform to the accepted EventStore-fronted picker contract. It should not implement the whole Epic 8 feature set.

- Typeahead result behavior belongs mostly to Story 8.2.
- Durable party-id selection semantics belong mostly to Story 8.3.
- Full state matrix and stale-response hardening belong mostly to Story 8.4.
- Accessibility/localization hardening belongs mostly to Story 8.5.
- Privacy/integration boundary hardening belongs mostly to Story 8.6.

Story 8.1 owns the embeddable shell, host configuration surface, disabled/read-only shell behavior, compact layout, package/custom-element registration, and tests proving the shell remains bounded and embeddable.

### What Must Be Preserved

- Do not add REST controllers, Swagger/OpenAPI endpoints, MCP hosting, DAPR actor calls, projection actor calls, local search services, or EventStore stream browsing to `src/Hexalith.Parties`.
- Do not call retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, controllers, server internals, or `Hexalith.Memories` directly from the picker.
- Do not move picker UI into the `Hexalith.FrontComposer` submodule unless a separate story explicitly changes that submodule.
- Do not add Blazor, Fluent UI, FrontComposer, custom-element, or JavaScript packaging dependencies to `Hexalith.Parties.Contracts`.
- Do not weaken tenant fail-closed behavior. Missing or stale host context must clear/suppress visible results and selected preview data before rendering current state.
- Do not broaden durable callback/event payloads with display names, contacts, identifiers, tenant ids, token material, search text, raw ProblemDetails, or raw query payloads.
- Do not introduce browser storage for tokens, tenant ids, query text, selected preview data, or backend details.

### Known Source Risks To Watch

- `PartyPickerSelection` currently includes `DisplayName`, `PartyType`, `IsActive`, and `IsErased` for the .NET callback, while `PartyPickerEventDetail` intentionally emits only `PartyId`, `PartyType`, and `Status`. Before changing selection models, cross-check Story 8.3's durable-party-id contract.
- `PartyPicker` has an obsolete `ApiBaseUrl` parameter for source compatibility. Do not revive it as transport selection; the docs say request routing belongs to configured `Hexalith.Parties.Client` / EventStore gateway configuration.
- `ResultTemplate` lets Blazor hosts render custom rows. The picker cannot make a host's template safe if the host uses raw markup, so docs/tests should keep warning that untrusted values must render as text.
- The old Story 10.3 artifact was created before the EventStore-fronted contract gate was formalized and contains stale REST endpoint language. Use current source, current docs, and the dependency record as authoritative over stale Story 10.3 wording.

### Previous Story Intelligence

- Story 7.7's 2026-05-23 risk acceptance is scoped only to GDPR panels. Story 8.1 has its own separate 2026-05-23 picker-shell risk acceptance; do not extend either acceptance to later Epic 8 stories.
- Stories 7.8, 7.9, and 7.10 closed localization, accessibility, and privacy/encoding expectations for the Admin Portal. Epic 8 inherits the same frontend discipline: labels/status text must be localizable, controls keyboard reachable, state not color-only, and rendered content encoded.
- Story 9.8 is done, so clean build/CI quality is available as a signal while Story 8.1 proceeds. Do not use that to bypass the explicit Epic 8 scheduling gate for later picker stories.
- Prior picker Story 10.3 delivered the current `Hexalith.Parties.Picker` RCL and tests. Reuse that code, but reconcile any stale REST assumptions with the newer EventStore-fronted architecture before implementation.

### Git Intelligence

Recent commits show the project is actively closing build gates and revalidating frontend blockers:

- `129f915 fix(story-9.8): harden build-gate regression guard from code review`
- `3251ba7 docs(story-7.7): re-verify blocking gates remain live (v0.3)`
- `21bf855 docs(bmad): refresh story 7.7 blocker context`
- `97da0e5 chore(epic-3-follow-through): rename Story 9.5 -> 9.8 after Epic 9 v2 rebase`
- `cba23f8 feat: Implement Sprint Change Proposal for Epic 3 Retrospective Follow-Through`

Actionable takeaway: blocked story artifacts are being kept explicit instead of silently scheduling gated work. Follow that pattern for Story 8.1.

### Technical Version Notes

- Root `global.json` pins .NET SDK `10.0.300`; projects target `net10.0`.
- `Directory.Packages.props` currently pins Dapr packages `1.17.9`, Aspire packages `13.3.3` (with Keycloak/Kubernetes previews), `Microsoft.AspNetCore.Components.CustomElements` `10.0.8`, `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.2-26098.1`, xUnit v3 `3.2.2`, bUnit `2.7.2`, and Shouldly `4.3.0`.
- The Fluent UI Blazor MCP docs available in this environment target `5.0.0.26098`, while the project pins `5.0.0-rc.2-26098.1`; treat MCP Fluent UI examples as version-adjacent only and prefer local patterns. Do not upgrade Fluent UI as part of Story 8.1.
- Microsoft Learn's .NET 10 Blazor docs support using Razor class libraries for reusable components and static assets, and Blazor custom elements for JavaScript hosts. Custom elements use kebab-case names and can receive parameters via HTML attributes or JavaScript properties; they do not support child content or templated components.
- Microsoft Learn's Blazor security docs confirm normal Razor string rendering treats markup as literal text, while raw HTML via `MarkupString` / markup APIs is unsafe for untrusted content. Keep picker data and backend text on the safe text-rendering path.

### Testing Requirements

- Focused picker suite: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.
- If `Hexalith.Parties.Client` query/configuration contracts change, also run relevant client tests, especially `tests/Hexalith.Parties.Client.Tests`.
- If shell embedding or custom-element registration changes, add or update packaging/custom-element tests and verify `docs/frontend/party-picker.md`.
- If a broader solution build is run and unrelated failures appear, record them explicitly instead of weakening Story 8.1 scope.

### References

- `_bmad-output/planning-artifacts/epics.md`, Epic 8 Story 8.1 and cross-story Epic 8 context.
- `_bmad-output/planning-artifacts/prd.md`, FR67 and v1.2 frontend scope.
- `_bmad-output/planning-artifacts/architecture.md`, Frontend Architecture and Party Picker Frontend Surface.
- `_bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md`.
- `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23.md`.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23-epic8-picker-gate.md`.
- `_bmad-output/implementation-artifacts/10-3-embeddable-party-picker-component.md`.
- `docs/frontend/party-picker.md`.
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`.
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css`.
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`.
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`.
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`.
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`.
- Microsoft Learn: `https://learn.microsoft.com/aspnet/core/blazor/components/js-spa-frameworks?view=aspnetcore-10.0`.
- Microsoft Learn: `https://learn.microsoft.com/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0`.
- Microsoft Learn: `https://learn.microsoft.com/aspnet/core/blazor/components/?view=aspnetcore-10.0#raw-html`.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-23 | 0.2 | Refreshed blocked story with current dependency status, existing picker implementation snapshot, post-unblock task plan, architecture guardrails, latest Blazor/RCL/custom-element notes, and focused test requirements. Story remains blocked; sprint status is not flipped. | Codex GPT-5 |
| 2026-05-23 | 0.3 | Applied approved correct-course decision for scoped Story 8.1 risk acceptance. Story is now ready-for-dev against the existing picker/client temporary bridge; later Epic 8 picker stories remain gated. | Codex GPT-5 |
| 2026-05-23 | 1.0 | dev-story implementation. Reconciled the existing picker shell against the accepted `IPartiesQueryClient` contract (no rebuild). Refined the clear control to an accessible decorative-icon button. Locked the bounded embeddable / compact-layout / disabled / read-only / contract-unavailable guarantees with new component tests; hardened the transport guardrail against browser storage; documented the shell boundary. Focused picker suite 65/65 green (Release). Status: in-progress → review. | Claude Opus 4.7 |
| 2026-05-23 | 1.1 | code-review closure. Applied five review patches for disabled/read-only mutation guards, selected-id fallback display, string-indexed browser-storage guardrails, compact layout/badge overflow coverage, and typed-client query assertions. Focused picker suite 70/70 green (Release). Status: review → done. | Codex GPT-5 |

## Dev Agent Record

### Agent Model Used

Codex GPT-5 (create-story / correct-course); Claude Opus 4.7 (dev-story implementation 2026-05-23)

### Debug Log References

#### dev-story implementation (2026-05-23, Claude Opus 4.7)

- Confirmed the existing `Hexalith.Parties.Picker` already conforms to the accepted contract: `PartyPickerApiClient` consumes `IPartiesQueryClient` (the scoped contract of record), auth is host-supplied via `AccessToken` / `AccessTokenProvider` / `RequestCustomizer`, and tokens are never persisted, refreshed, parsed for authorization, or logged (`TokenFingerprint` is a hash used only for context change-detection). No initialization parameter changes were required (Task 1.2) and no API-client query/configuration semantics changed (Task 4.2 no-op).
- The accepted `IPartiesQueryClient` boundary introduces no capability-detection / contract-unavailable state for the picker (unlike the Story 7.7 GDPR panels). Backend unavailability already maps to bounded localized shell states with retry, never fallback calls to retired endpoints (Task 1.4).
- Only production change: refined the clear control from a literal `x` to an accessible decorative-icon button — `<span class="hx-party-picker__icon" aria-hidden="true">&times;</span>` keeping the `ClearSelection` accessible name (Task 2.4), plus a minimal `.hx-party-picker__icon` font-size rule (no new visual system).
- Red→green: `PartyPicker_ClearButton_UsesAccessibleDecorativeIconPattern` failed on the literal `x` baseline, passed after the markup change.
- Test bug caught during red run: Shouldly string `ShouldNotContain` is case-insensitive, so the typed query text legitimately present in the input `value` attribute collided with a leak assertion. Rewrote the contract-unavailable test to use a non-colliding query (`northwind`) and distinct backend leak tokens (`0xCAFEF00D`, `Lovelace`, `backend stack`).
- Regression scope: the Picker project has no production consumers (only its own test project references it), and no shared client / solution wiring changed, so the focused picker suite is the complete regression boundary per Task 4.5. Did not run the full solution build (a known-unrelated AppHost EventStore/Memories submodule-drift blocker, recorded in Story 7.7, is out of scope for 8.1).
- Focused suite: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` → 65/65 passed (baseline 55 + 10 new: 6 component facts + 4 guardrail InlineData) under `TreatWarningsAsErrors`.

#### code-review closure (2026-05-23, Codex GPT-5)

- Applied five review patches: moved the disabled/read-only interaction guard before input mutation; rendered host-supplied `SelectedPartyId` as a stable selected-preview fallback; added string-indexed browser-storage guardrail coverage; bounded localized badge text for compact layouts; and asserted the typed-client forwarded query/page/page-size.
- Focused suite: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` → 70/70 passed.

#### create-story / correct-course (prior)

- Parsed user request `8.1` as `8-1-compose-embeddable-party-picker-shell`.
- Loaded BMad config from `_bmad/bmm/config.yaml`: planning artifacts under `_bmad-output/planning-artifacts`, implementation artifacts under `_bmad-output/implementation-artifacts`, document language English.
- Loaded persistent project context from `_bmad-output/project-context.md` and relevant sibling context from `Hexalith.FrontComposer/_bmad-output/project-context.md`, plus sibling submodule context for boundary rules.
- Loaded sprint status; confirmed `development_status[8-1-compose-embeddable-party-picker-shell] = blocked`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; confirmed risk acceptance is scoped to Story 7.7 only and Epic 8 remains Required.
- Applied approved correct-course proposal `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23-epic8-picker-gate.md`; dependency now carries a scoped Story 8.1 risk acceptance and sprint status now tracks Story 8.1 as ready-for-dev.
- Loaded Epic 8 Story 8.1 source from `_bmad-output/planning-artifacts/epics.md`, PRD FR67 context, architecture Party Picker Frontend Surface, and `ux-party-picker-2026-05-12.md`.
- Read existing picker implementation files under `src/Hexalith.Parties.Picker`, picker docs, picker component/API/packaging/transport tests, and old Story 10.3 to distinguish reusable current implementation from stale pre-pivot REST wording.
- Checked latest local package pins in `Directory.Packages.props`.
- Checked Fluent UI Blazor MCP version compatibility; docs target `5.0.0.26098`, while project pins `5.0.0-rc.2-26098.1`, so local patterns remain authoritative.
- Queried Microsoft Learn for .NET 10 Blazor custom elements, RCL reuse/static assets, and raw HTML/security guidance; captured only story-relevant constraints.

### Completion Notes List

#### dev-story implementation (2026-05-23)

- Reconciled the existing `Hexalith.Parties.Picker` shell against the accepted `IPartiesQueryClient` contract rather than rebuilding it. The shell already conformed (host-supplied auth, typed query boundary, narrow DOM/callback payloads, encoded rendering), so the deliverable was confirming conformance and locking the bounded embeddable guarantees with the AC5 state matrix.
- AC1/AC2 (Tasks 1, 3): verified the picker is a single bounded search/selection control on the accepted typed-client boundary; auth stays host-supplied with no token persistence/refresh/parse/log; backend failures map to bounded PII-safe states. Hardened the transport guardrail with browser-storage forbidden markers (`localStorage`, `sessionStorage`, `indexedDB`, `document.cookie`).
- AC3 (Task 2.2/2.4): disabled and read-only behavior preserved; a host-supplied `SelectedPartyId` keeps its selection display present and accessible while disabled; clear control is now an accessible icon button (localized `ClearSelection` name, decorative `aria-hidden` glyph).
- AC4 (Task 2.3): compact layout contract preserved and locked — single bounded root, wrapped long names, bounded `max-width`, visible `:focus-visible` focus, text-conveyed (non-color-only) status.
- AC5 (Task 4): extended `PartyPickerComponentTests` with enabled, compact-layout, layout-CSS-contract, backend-contract-unavailable, disabled-with-selection, and clear-button accessibility tests; missing-host-configuration and stale-context states were already covered. Updated `docs/frontend/party-picker.md` with a Shell Boundary And Layout section. No stale pre-pivot REST wording remained (existing REST mentions are intentional anti-REST guardrails).
- Scope discipline: did not implement Epic 8.2–8.6 behavior (typeahead richness, durable selection semantics, full state hardening, a11y/localization hardening, privacy/integration hardening). `PartyPickerSelection` / `PartyPickerEventDetail` left intentionally narrow pending Story 8.3.

#### create-story / correct-course (prior)

- Story 8.1 is ready-for-dev under a scoped 2026-05-23 risk acceptance. Production implementation may start for the shell story only.
- Existing `Hexalith.Parties.Picker` code is now documented as the implementation surface to reconcile after unblock. A future dev agent should not create a duplicate picker or route through forbidden internals.
- Added post-unblock tasks, guardrails, source snapshots, risk notes, and focused test requirements to prevent reinvention and contract drift.
- No production code changed during create-story refresh or correct-course approval.

### File List

dev-story implementation (2026-05-23):

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` (modified — accessible decorative-icon clear button)
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css` (modified — `.hx-party-picker__icon` rule)
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` (modified — enabled, compact-layout, layout-CSS, contract-unavailable, disabled-with-selection, clear-button tests)
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs` (modified — browser-storage forbidden markers)
- `docs/frontend/party-picker.md` (modified — Shell Boundary And Layout section)
- `_bmad-output/implementation-artifacts/8-1-compose-embeddable-party-picker-shell.md` (story tracking)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status: in-progress → review)

code-review closure (2026-05-23):

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` (modified — disabled/read-only guard and selected-id fallback display)
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css` (modified — bounded wrapping badge text)
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` (modified — query assertions, disabled/read-only mutation guard, selected preview fallback, badge/layout coverage)
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs` (modified — string-indexed storage guardrail)
- `_bmad-output/implementation-artifacts/8-1-compose-embeddable-party-picker-shell.md` (story tracking, review findings applied, status done)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status: review → done)

create-story / correct-course (prior, no production code):

- `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23-epic8-picker-gate.md`
