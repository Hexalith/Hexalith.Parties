---
baseline_commit: e93a04b
---

# Story 2.5: Party picker re-skin + full WAI-ARIA combobox (D11 / UX-DR7)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an admin,
I want to link a related party via an accessible picker,
so that I can capture relationships inline by keyboard or pointer.

This story modernizes the existing `Hexalith.Parties.Picker` package and its tests. Do not build a second picker in AdminPortal, do not fork the current custom element, and do not move search transport into the browser. The current picker already uses the typed Parties query client, dispatches a bounded `party-selected` event, and is embedded by the create/edit form; this story completes the visual token migration and the missing active-option combobox behavior.

## Acceptance Criteria

1. **AC1 - Picker styling uses Fluent 2 token inputs only.** Given the existing `<hexalith-party-picker>` and `PartyPicker.razor.css`, when the picker renders in the FluentUI V5/FrontComposer shell, then its `--hx-picker-*` variables resolve through Fluent 2 tokens: `--colorNeutralStroke1`, `--colorNeutralBackground1`, `--colorNeutralForeground1`, `{colors.accent}` or the project brand/accent token used for non-text focus accents, and `--colorStatusDangerForeground1`. Legacy FAST token names such as `--neutral-stroke-rest`, `--neutral-layer-1`, `--neutral-layer-2`, `--neutral-foreground-rest`, `--neutral-foreground-hint`, `--accent-fill-rest`, and `--error-fill-rest` are removed from production picker CSS and docs.

2. **AC2 - Existing compact layout and contrast guardrails remain intact.** Given the token migration, when the picker is rendered at narrow widths, 200% zoom equivalent, dark/light theme, forced-colors, and reduced-motion settings, then the input row remains stable, long names wrap without overflow, focus remains visible, state is never color-only, no hard-coded state hex colors are introduced, and the picker still honors forced-colors and `prefers-reduced-motion`.

3. **AC3 - The input owns WAI-ARIA combobox focus.** Given search results are available, when a keyboard or screen-reader user focuses the picker input, then the input has `role="combobox"`, `aria-autocomplete="list"`, `aria-controls` referencing the listbox id, `aria-expanded` reflecting popup visibility, and `aria-activedescendant` referencing the active option id when an option is active. DOM focus remains on the input while navigating options.

4. **AC4 - The listbox and options expose stable active/selected semantics.** Given the result popup is visible, when options render, then the popup has `role="listbox"`, each option has `role="option"`, a stable `id`, localized visible text for name/type/status, `aria-selected="true"` only for the active option during navigation or the selected party after selection, and a non-color visual active cue. Options are excluded from the normal Tab sequence; Tab leaves the picker instead of walking every result.

5. **AC5 - Keyboard interaction matches the story contract.** Given the input is focused and results are available, when the user presses `ArrowDown` or `ArrowUp`, then the active option moves and updates `aria-activedescendant`/`aria-selected`; when `Enter` is pressed with an active option, that option is selected; when `Escape` is pressed, the popup closes without changing the durable selection; when `Backspace` is pressed on an empty input, the current selection is cleared. Printable text and native text editing keys continue to work normally and do not trigger custom interception that breaks browser text editing.

6. **AC6 - Search, degraded, and selection behavior is preserved.** Given the 300ms debounce and typed Parties client search path, when the picker searches or resolves a selected id, then it still uses `IPartiesQueryClient` through `PartyPickerApiClient`, preserves cancellation/version guards, honors `ContextKey`/`AuthContextKey`, does not fabricate rich search modes, and keeps `Idle/Loading/Ready/Empty/LocalOnly/Degraded/Unauthorized/Forbidden/TransientFailure/NotFound/Gone/Error` state handling. `LocalOnly` and `Degraded` results remain selectable and are identified with text, not color alone.

7. **AC7 - The DOM event contract remains bounded and compatible.** Given a party is selected by pointer or keyboard, when the custom element emits `party-selected`, then the event bubbles and is composed, and `detail` contains only `{partyId, partyType, status}`. It must not include display names, tenant ids, tokens, search text, query payloads, contact data, identifiers, degraded reasons, backend problem details, or raw exceptions. Existing AdminPortal create/edit form binding through `party-form-picker.js` continues to work.

8. **AC8 - Privacy and boundary guardrails remain pinned by tests.** Given implementation is complete, when tests scan picker production source, then the picker still contains no old REST URLs, no direct HTTP transport, no actor/projection/server references, no DAPR dependency, no browser storage/token parsing/fingerprinting, no logging/telemetry, no navigation/file side effects, and no unsafe markup paths such as `MarkupString`, `AddMarkupContent`, or `innerHTML`.

9. **AC9 - Documentation reflects the new public contract.** Given the story is complete, when `docs/frontend/party-picker.md` is read, then it describes Fluent 2 token mapping, combobox active-option keyboard behavior, `aria-activedescendant`, listbox/option id semantics, the bounded event payload, and host responsibilities without retaining legacy FAST-token examples.

10. **AC10 - Automated verification covers the modernization.** Given the story is complete, when focused verification runs, then bUnit covers token CSS guardrails, combobox relationships, active-option keyboard navigation, Escape close, Enter select, empty Backspace clear, pointer selection, result count status, degraded/local-only selectable states, read-only/disabled behavior, custom element event shape, and AdminPortal bridge compatibility. Existing picker API/client/transport guardrail tests remain green.

## Tasks / Subtasks

### Part A - Fluent 2 token migration - AC1, AC2, AC9

- [x] **Task 1 - Replace picker CSS token fallbacks with Fluent 2 mappings** (AC1, AC2)
  - [x] Update `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css` so the `--hx-picker-*` aliases resolve from Fluent 2/FrontComposer tokens, not legacy FAST names.
  - [x] Remove `--neutral-*`, `--accent-fill-rest`, and `--error-fill-rest` references from production picker CSS.
  - [x] Keep the compact grid row, bounded `max-width`, `minmax(0, 1fr)` input column, `overflow-wrap: anywhere`, visible focus ring, forced-colors handling, and reduced-motion guard.
  - [x] Use danger/status color only for failure text; do not use danger for normal emphasis or ordinary controls.
  - [x] Do not hard-code new state colors. If a fallback is unavoidable for non-Fluent hosts, keep it scoped to a private `--hx-picker-*` alias and document why in the story completion notes.

- [x] **Task 2 - Update documentation and CSS guard tests** (AC1, AC2, AC9)
  - [x] Update `docs/frontend/party-picker.md` Theming and Accessibility sections to show Fluent 2 token mapping and remove legacy FAST examples.
  - [x] Extend `PartyPicker_LayoutCss_DeclaresBoundedCompactContract` or add a dedicated CSS test that asserts Fluent token names are present and legacy FAST token names are absent.
  - [x] Keep existing assertions for no `white-space: nowrap`, no CSS-generated state labels, forced-colors, reduced-motion, and long-word wrapping.

### Part B - WAI-ARIA combobox state machine - AC3, AC4, AC5

- [x] **Task 3 - Add active option state to `PartyPicker.razor`** (AC3, AC4, AC5)
  - [x] Track an active result index/id separately from durable selection. Reset active state when query, result set, context, auth, disabled/read-only state, or selected id changes.
  - [x] Generate stable option ids from `_listElementId` plus result index or a sanitized deterministic suffix for the current result set.
  - [x] Bind the input's `aria-activedescendant` only when the listbox is visible and the active index points to a rendered option.
  - [x] Keep `aria-expanded` tied to popup visibility, not only whether the last response had any results if the popup has been closed by `Escape`.
  - [x] Preserve the existing visible label, `aria-describedby` status relationship, `role=status` count messages, and localized labels.

- [x] **Task 4 - Convert option navigation to input-owned focus** (AC3, AC4, AC5)
  - [x] Add an input `@onkeydown` handler for `ArrowDown`, `ArrowUp`, `Enter`, `Escape`, and empty-input `Backspace`.
  - [x] Prevent default only for keys the picker handles; do not intercept normal text editing/navigation keys.
  - [x] Make result options non-tabstop popup descendants while preserving pointer selection. If buttons remain, set `tabindex="-1"` and keep their role/name/state correct; otherwise use semantic option elements with click handlers and verify accessibility with tests.
  - [x] Arrow keys move the active option and keep DOM focus on the input.
  - [x] Enter selects the active option and dispatches callbacks/DOM event exactly as pointer selection does.
  - [x] Escape closes the popup and clears active state without mutating `_query` or durable selection.
  - [x] Backspace on an empty input clears the current selection through the same path as the clear button.

- [x] **Task 5 - Preserve selection and popup behavior across states** (AC5, AC6)
  - [x] Selecting an option updates `_selected`, invokes `SelectedPartyIdChanged`, invokes `SelectedPartyChanged`, dispatches `party-selected` when enabled, and returns focus to the input.
  - [x] Search responses arriving after cancellation/version changes must not reactivate stale options.
  - [x] Disabled state removes interaction; read-only remains keyboard-reachable but does not mutate search or selection.
  - [x] `LocalOnly`/`Degraded` ready results can still be arrowed and selected; unavailable/failure/empty states do not leave a stale active descendant.

### Part C - Event contract, AdminPortal bridge, and docs - AC6, AC7, AC8, AC9

- [x] **Task 6 - Keep the custom-element event contract stable** (AC7)
  - [x] Reuse `PartyPickerEventDetail.FromSelection` and `wwwroot/hexalith-parties-picker.js`; do not add names, query text, tenant ids, tokens, errors, or backend details to the event.
  - [x] Add or extend tests proving keyboard selection and pointer selection dispatch the same bounded event shape.
  - [x] Preserve `bubbles: true` and `composed: true`.

- [x] **Task 7 - Verify AdminPortal create/edit integration still binds selection** (AC7, AC10)
  - [x] Keep `<hexalith-party-picker>` in `CreateEditPartyPage.razor`; do not implement picker internals there.
  - [x] Ensure `src/Hexalith.Parties.AdminPortal/wwwroot/party-form-picker.js` still receives `party-selected` from keyboard and pointer selection.
  - [x] Extend AdminPortal form tests or E2E fixture coverage only as needed to prove the bridge still binds `{partyId, partyType, status}` without stealing focus or leaving the form.

- [x] **Task 8 - Preserve transport, privacy, and package boundaries** (AC6, AC8)
  - [x] Keep `PartyPickerApiClient` as the only search/selected-display adapter and keep it on `IPartiesQueryClient`.
  - [x] Do not add `HttpClient`, raw REST URLs, DAPR actors, server/projection references, browser storage, token parsing, logging, telemetry, navigation, file download, or unsafe markup to picker production code.
  - [x] Keep `ApiBaseUrl` obsolete/source-compatible only; do not revive it as transport selection.
  - [x] Keep selected display names and statuses preview-only; durable host binding remains `SelectedPartyId`.

### Part D - Tests and focused verification - AC1-AC10

- [x] **Task 9 - Expand bUnit component coverage** (AC1-AC7, AC10)
  - [x] Add tests for `aria-controls`, `aria-expanded`, `aria-activedescendant`, listbox id, option id, active option `aria-selected`, and non-tabstop result options.
  - [x] Add keyboard tests for `ArrowDown`, `ArrowUp`, `Enter`, `Escape`, and empty-input `Backspace`.
  - [x] Add regression coverage for pointer selection, result count status, localized labels, disabled/read-only handling, selected-party display resolution, degraded/local-only selection, unauthorized/forbidden/not-found/gone text, and no raw backend detail leak.
  - [x] Add CSS token tests for Fluent 2 mappings and absence of legacy FAST tokens.

- [x] **Task 10 - Keep API/client and source guardrail tests green** (AC6, AC8)
  - [x] Keep `PartyPickerApiClientTests` green for query normalization, bounded page size, host auth, request customizer composition, safe failure mapping, freshness-to-state mapping, and no client call for missing auth.
  - [x] Keep `PartyPickerTransportGuardrailTests` green; extend forbidden-token scans if needed for the legacy FAST token removal.
  - [x] Keep `PartyPickerPackagingTests` green so the custom element script and static assets still ship.

- [x] **Task 11 - Run focused verification** (AC1-AC10)
  - [x] `dotnet build src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `dotnet build tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.Picker.Tests/bin/Release/net10.0/Hexalith.Parties.Picker.Tests`
  - [x] `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
  - [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests`
  - [x] `bash scripts/check-no-warning-override.sh`

## Dev Notes

### Source Discovery Summary

- Loaded `epics_content` from `_bmad-output/planning-artifacts/epics.md`; Story 2.5 covers D11/UX-DR7: modernize the existing picker, map it to Fluent 2 tokens, complete WAI-ARIA combobox semantics, preserve the `party-selected {partyId, partyType, status}` event, honor degraded/local-only/gone states, and keep create/edit form binding.
- Loaded `architecture_content` from `_bmad-output/planning-artifacts/architecture.md`; D11 places the work in `Hexalith.Parties.Picker/`, while AdminPortal only embeds the picker. UI remains a BFF over the typed client/EventStore gateway; no actor-host direct calls or browser command/query endpoints.
- Loaded UX files under `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`; relevant constraints are full combobox pattern, active option via `aria-activedescendant`, 300ms debounce, degraded/local-only selectable results, forced-colors/reduced-motion, visible focus, no color-only meaning, and Fluent 2 token mapping.
- Loaded `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md`; it confirms picker re-skin is genuinely needed because current CSS still uses legacy FAST tokens and no Fluent 2 `--colorNeutral*`/`--colorBrand*` token mapping.
- Loaded persistent project context from `_bmad-output/project-context.md`; .NET 10, `.slnx`, Central Package Management, `TreatWarningsAsErrors`, xUnit v3, Shouldly, NSubstitute, bUnit, Playwright, no PII logging, and gateway-boundary rules apply.
- Reviewed previous story `_bmad-output/implementation-artifacts/2-4-create-and-edit-a-party-with-validation-fr-admin-3.md`; Story 2.4 added the AdminPortal create/edit form and embeds `<hexalith-party-picker>` through `party-form-picker.js`. It explicitly left full picker re-skin and combobox compliance to Story 2.5.
- Reviewed current picker source in `src/Hexalith.Parties.Picker`, docs in `docs/frontend/party-picker.md`, AdminPortal bridge in `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor` and `wwwroot/party-form-picker.js`, and existing picker/AdminPortal tests.
- Reviewed recent git history: `e93a04b feat(story-2.4): Create and edit party form`, `b7303af feat(story-2.3): Party detail (FR-Admin-2)`, `da6bfcf feat(story-2.2): Parties list with search, filters, and paging`, `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`, `78a5956 docs(epic-1): Add retrospective and sync project docs`.

### Existing Code to Reuse

- Reuse `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` as the only picker UI implementation.
- Reuse `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css`; migrate its token aliases in place instead of adding host-specific CSS in AdminPortal.
- Reuse `PartyPickerApiClient`, `PartyPickerSearchRequest`, `PartyPickerSearchResponse`, `PartyPickerSearchState`, `PartyPickerSearchMetadata`, `PartyPickerSelection`, `PartyPickerSelectionState`, `PartyPickerLabels`, and `PartyPickerEventDetail`.
- Reuse `wwwroot/hexalith-parties-picker.js` for the bounded DOM event. Keep the safe-detail whitelist.
- Reuse `src/Hexalith.Parties.AdminPortal/wwwroot/party-form-picker.js` as the create/edit form bridge; only update it if keyboard selection exposes a real compatibility gap.
- Reuse `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`, `PartyPickerApiClientTests.cs`, `PartyPickerTransportGuardrailTests.cs`, and packaging tests. Extend these rather than introducing a second test fake.
- Reuse existing `RecordingPartiesQueryClient` test fake and the direct xUnit executable pattern used by previous stories.

### Current Files Being Modified

| File | Current state | Story change | Preserve |
|---|---|---|---|
| `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` | Labeled input already has `role=combobox`, `aria-controls`, `aria-autocomplete=list`, `aria-haspopup=listbox`, and status relationship. Results render as `button role=option` with `aria-selected` only reflecting durable selection; there is no active index, no `aria-activedescendant`, no arrow-key handling, and result buttons are currently tab-reachable. | Add active-option state, input `aria-activedescendant`, keyboard handling, popup open/closed state, stable option ids, non-tabstop options, and pointer/keyboard selection parity. | Typed-client search flow, debounce, cancellation/versioning, context/auth reset, selected-party resolution, localized labels, safe status text, disabled/read-only behavior, no raw backend detail rendering. |
| `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css` | Compact layout works, forced-colors/reduced-motion are present, but `--hx-picker-*` aliases still point at legacy FAST tokens with hard-coded fallback colors. | Map aliases to Fluent 2/FrontComposer tokens and remove legacy FAST token references from production CSS. Add active-option non-color visual cue. | Bounded width, stable grid, wrapping, focus-visible treatment, no CSS-generated labels, no `white-space: nowrap`. |
| `src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js` | Dispatches only whitelisted `partyId`, `partyType`, `status` in a bubbling/composed `party-selected` event. | Usually no change; add tests proving keyboard selection dispatches through same path. | No expanded payload, no `detail: detail`, no unsafe browser storage or navigation. |
| `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs` | Centralized labels for input, results, status, retry, selected display, active/inactive/erased, and failures. | Add labels only if needed for active option or keyboard hint; prefer no new visible instructional copy unless a target user naturally needs it. | All user-facing copy remains localizable and encoded by Razor. |
| `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs` | Typed-client adapter for search and selected display. Maps auth/freshness/failure states safely. | No behavioral change expected. Update only if tests reveal a state contract gap. | `IPartiesQueryClient` boundary, safe reason mapping, no REST/DAPR/server references, no problem-detail leakage. |
| `docs/frontend/party-picker.md` | Public docs still show legacy FAST theming examples and do not describe active-option `aria-activedescendant` behavior. | Update theming and accessibility docs to match Fluent 2 and full combobox behavior. | Stable durable selection guidance, bounded event detail, privacy and transport guidance. |
| `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor` | Embeds `<hexalith-party-picker>` in the related-party field. | No picker internals here. Add/adjust tests only if bridge compatibility requires it. | Protected Admin create/edit form behavior from Story 2.4. |
| `src/Hexalith.Parties.AdminPortal/wwwroot/party-form-picker.js` | Listens for `party-selected` and invokes `OnRelatedPartySelectedAsync`. | No change expected. Verify it receives keyboard and pointer selection events. | Bounded values only; no names, ids beyond party id, tokens, or problem details. |
| `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` | Covers initial ARIA roles, status region, localized labels, native result buttons, layout CSS, disabled/read-only, safe rendering, and many state paths. | Add active-descendant keyboard tests, non-tabstop options, Fluent token CSS assertions, Escape/Enter/Backspace behavior, and event parity. | Existing state/privacy/layout tests remain green. |
| `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs` | Pins no REST/DAPR/server/browser storage/token parsing/logging/unsafe markup and whitelisted event payload. | Extend scans for legacy FAST token removal if useful. | Existing boundary guardrails. |
| `tests/Hexalith.Parties.AdminPortal.Tests/Components/CreateEditPartyPageTests.cs` or E2E fixture tests | Covers create/edit and related-party bridge from Story 2.4. | Add only focused regression proving picker keyboard selection still binds into the form if current coverage does not observe it. | Route-id authority, validation, optimistic status, and form regression behavior. |

Do not modify EventStore/Tenants submodules, Parties actor host public API surface, DAPR access control, OIDC/sign-in flow, Consumer self-scope code, GDPR verification stubs, deployment manifests, or AdminPortal form command mapping for this story.

### Technical Requirements

- Implement the WAI-ARIA editable combobox with list autocomplete in the existing Blazor component. DOM focus remains on the input; active option focus is represented through `aria-activedescendant`.
- Do not use a focusable result button per result in the normal Tab order. The popup and its descendants must be excluded from page Tab sequence.
- Keep the list popup as `role=listbox`; do not switch to grid/tree/dialog.
- Keep `aria-autocomplete="list"` and the 300ms debounce default from `PartyPickerDefaults`.
- Treat active option and durable selected party as different states. Arrowing through results must not change `SelectedPartyId` until Enter or pointer selection.
- Escape closes the popup without clearing durable selection. Empty Backspace clears through the same callback path as the clear button.
- Keep pointer selection working for mouse/touch users and make it call the same selection path as Enter.
- Do not add visible in-app instructional text solely to explain keyboard shortcuts; tests should prove behavior.
- Do not add new package versions to `.csproj`; Central Package Management owns versions.
- Keep component event handlers as `Task`/`ValueTask`, not `async void`.

### Architecture Compliance

- **AR-D11:** Story work belongs in `Hexalith.Parties.Picker`; AdminPortal embeds it.
- **AR-D5 / gateway boundary:** picker reads through `Hexalith.Parties.Client`/`IPartiesQueryClient` to the EventStore gateway; no actor-host direct call and no browser-callable command/query API.
- **NFR1 / D9 accessibility:** full combobox semantics, keyboard parity, visible focus, forced-colors, reduced-motion, no color-only state.
- **NFR2 eventual consistency:** degraded/local-only/freshness states remain visible and usable where safe; never blank or throw on staleness.
- **NFR3 privacy:** no PII, tokens, tenant ids, query payloads, backend details, or display names in logs, telemetry, event payloads, or error text.
- **NFR7 theming:** Fluent 2/FrontComposer tokens only; no legacy FAST tokens or hard-coded state color model.
- **NFR9 build:** .NET 10, `.slnx`, Central Package Management, warnings-as-errors, xUnit v3, Shouldly, NSubstitute, bUnit, and direct test executable fallback apply.

### Previous Story Intelligence

- Story 2.4 is done at commit `e93a04b` and added the AdminPortal create/edit form with a related-party field that embeds `<hexalith-party-picker>`.
- Story 2.4 intentionally prepared the picker slot and event bridge but did not implement re-skinning or full combobox compliance. Do not duplicate picker CSS or behavior in `CreateEditPartyPage.razor`.
- Story 2.4 review fixed deferred picker attachment after route resets. Preserve that behavior; if bridge tests fail, fix the bridge without taking ownership of picker internals in AdminPortal.
- Story 2.3/2.4 test patterns favor focused bUnit tests, existing fakes/fixtures, direct xUnit executable fallback, and preserving list/detail/GDPR/form regressions.
- The AdminPortal form treats `{partyId, partyType, status}` as relationship input only; picker selection must not override route ids, command identity, or form submit behavior.

### Git Intelligence Summary

- Recent commits:
  - `e93a04b feat(story-2.4): Create and edit party form`
  - `b7303af feat(story-2.3): Party detail (FR-Admin-2)`
  - `da6bfcf feat(story-2.2): Parties list with search, filters, and paging`
  - `ec2676b feat(story-2.1): Embed the Admin area behind the Admin policy`
  - `78a5956 docs(epic-1): Add retrospective and sync project docs`
- Pattern: narrow changes, reuse package boundaries, extend existing tests/fakes, use bUnit for component contracts, keep direct xUnit executable fallback, and avoid package/build-gate drift.

### Latest Technical Information

- W3C WAI-ARIA APG Combobox Pattern says an editable combobox can use a listbox popup; focus is in the combobox input, Down/Up move focus within the popup, Enter accepts the selected suggestion, Escape dismisses, and native text editing keys should not be broken. [Source: https://www.w3.org/WAI/ARIA/apg/patterns/combobox/]
- W3C APG roles/states guidance says the combobox has `aria-controls`, popup role is `listbox`, `aria-expanded` reflects popup visibility, DOM focus remains on the combobox, active popup focus for listbox/grid/tree is represented with `aria-activedescendant`, and the active/selected option uses `aria-selected=true`. [Source: https://www.w3.org/WAI/ARIA/apg/patterns/combobox/#wai-aria-roles-states-and-properties]

### Project Structure Notes

- Alignment: `Hexalith.Parties.Picker` owns the embeddable picker and custom element.
- Alignment: `Hexalith.Parties.AdminPortal` owns the create/edit form that embeds the picker, but it must not fork picker internals.
- Detected conflict: docs currently show legacy FAST theming examples while UX/architecture require Fluent 2 token mapping. Update docs with the implementation.
- Detected ARIA gap: current component has a combobox role and listbox/options, but missing `aria-activedescendant`, active index, input-owned keyboard navigation, and popup descendants excluded from Tab sequence.
- Detected styling gap: current CSS already has forced-colors/reduced-motion and layout protections. Preserve those while changing tokens.
- Detected event-safety dependency: AdminPortal's bridge relies on the DOM event. Keyboard selection must dispatch the same event as pointer selection.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.5: Party picker re-skin + full WAI-ARIA combobox (D11 / UX-DR7)`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Admin - Party Records Management`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D11 - Party picker`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Interaction Primitives`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Components`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-accessibility.md#high-4.1.2-name-role-value-1.3.1-info-relationships`]
- [Source: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-09-v2.md#R3`]
- [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]
- [Source: `_bmad-output/implementation-artifacts/2-4-create-and-edit-a-party-with-validation-fr-admin-3.md#Previous Story Intelligence`]
- [Source: `docs/frontend/party-picker.md`]
- [Source: `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`]
- [Source: `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css`]
- [Source: `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`]
- [Source: `src/Hexalith.Parties.Picker/Services/PartyPickerEventDetail.cs`]
- [Source: `src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/wwwroot/party-form-picker.js`]
- [Source: `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`]
- [Source: `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`]
- [Source: `W3C WAI-ARIA APG Combobox Pattern`, https://www.w3.org/WAI/ARIA/apg/patterns/combobox/]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `dotnet build tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `DiffEngine_Disabled=true tests/Hexalith.Parties.Picker.Tests/bin/Release/net10.0/Hexalith.Parties.Picker.Tests` - passed, 170 tests.
- `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - passed, 141 tests.
- `bash scripts/check-no-warning-override.sh` - passed.
- Production/docs scan for legacy FAST token names - passed with no matches.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Validation checklist applied during story creation; critical token migration, combobox active-option, keyboard, event-contract, privacy, transport, and AdminPortal bridge risks are represented in acceptance criteria and tasks.
- Migrated picker CSS aliases from legacy FAST token inputs to Fluent 2/FrontComposer token inputs while preserving compact layout, wrapping, forced-colors, reduced-motion, and visible focus guardrails. Private `--hx-picker-*` fallback colors remain only for non-Fluent hosts.
- Added input-owned combobox active-option behavior: stable option ids, `aria-activedescendant`, popup visibility separate from result availability, non-tabstop options, ArrowUp/ArrowDown navigation, Enter selection, Escape close, and empty Backspace clear.
- Preserved typed-client search, cancellation/version guards, degraded/local-only selectable results, read-only/disabled behavior, and bounded DOM event payload shape for pointer and keyboard selection.
- Updated party picker documentation for Fluent 2 token mapping, active-descendant combobox behavior, listbox/option ids, keyboard behavior, bounded event details, and host responsibilities.
- Expanded picker/AdminPortal tests for token/doc guardrails, active option ARIA relationships, keyboard navigation and selection, Escape, Backspace clear, local-only/degraded selection, pointer/keyboard DOM event parity, and AdminPortal bridge bounded detail forwarding.
- Senior review auto-fixes applied: handled combobox keys now suppress browser defaults without blocking printable/native editing keys; pointer/keyboard selection now closes the stale popup; misleading whole-markup "Active" assertion was scoped to the selected badge.

### File List

- `_bmad-output/implementation-artifacts/2-5-party-picker-re-skin-full-wai-aria-combobox-d11-ux-dr7.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/story-automator/orchestration-1-20260609-205725.md`
- `docs/frontend/party-picker.md`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css`
- `src/Hexalith.Parties.UI/Components/Specimens/PartiesAccessibilitySpecimenRoutes.cs`
- `src/Hexalith.Parties.UI/Components/Specimens/PartyPickerSpecimen.razor`
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartyFormPickerBridgeTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/e2e/specs/party-picker.spec.ts`

### Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

Outcome: Approved after auto-fixes.

Findings fixed:

- HIGH: Handled combobox keys updated component state but did not suppress browser defaults. `ArrowDown`/`ArrowUp`, active `Enter`, popup `Escape`, and empty-input `Backspace` now call `preventDefault()` at the DOM event boundary only when the picker handles that key.
- HIGH: Selecting an option left the old result popup visible. Pointer and keyboard selection now close the popup, clear the active descendant, preserve the durable selection, and return focus to the input.
- MEDIUM: Story File List did not document the UI specimen, E2E fixture/spec, and automation artifacts changed for this story. File List now matches git reality.
- MEDIUM: A test asserted the whole markup did not contain `Active`, which conflicted with the required `aria-autocomplete` attribute. The assertion now checks the selected badge text directly.

Validation:

- `dotnet build src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `dotnet build tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `DiffEngine_Disabled=true tests/Hexalith.Parties.Picker.Tests/bin/Release/net10.0/Hexalith.Parties.Picker.Tests` - passed, 171 tests.
- `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - passed, 141 tests.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false` - passed.
- `cd tests/e2e && npm run typecheck` - passed.
- `bash scripts/check-no-warning-override.sh` - passed.
- `cd tests/e2e && npx playwright test specs/party-picker.spec.ts --project=chromium` - blocked before test execution by sandbox socket binding: `System.Net.Sockets.SocketException (13): Permission denied`.

### Change Log

- 2026-06-10: Implemented Story 2.5 picker Fluent 2 token migration, WAI-ARIA active-descendant combobox behavior, bounded event/bridge verification, documentation updates, and focused automated coverage.
- 2026-06-10: Senior review auto-fixes applied for combobox key default prevention, popup close after selection, File List completeness, and a misleading badge assertion; story approved and marked done.
