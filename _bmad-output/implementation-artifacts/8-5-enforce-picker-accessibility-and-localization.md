# Story 8.5: Enforce Picker Accessibility and Localization

Status: done

## Story

As a host application user using keyboard or assistive technology,
I want the party picker to be accessible and localized,
so that I can search and select parties without inaccessible or hard-coded interactions.

## Acceptance Criteria

1. Given the picker renders search input, result list, selection display, retry control, and status messages, when a user navigates by keyboard, then all controls are reachable and operable, and visible focus is always present.
2. Given result options are shown, when assistive technology inspects them, then each option has a useful accessible name and state, and selected, disabled, degraded, erased, and unavailable states are not color-only.
3. Given picker status changes, when loading, empty, error, retry, selection, or degraded states occur, then localized status text is announced appropriately, and announcements are bounded and privacy-safe.
4. Given labels, placeholders, validation messages, state text, and counts render, when localization resources are inspected, then user-facing text comes from localized strings or FrontComposer localization, and missing resources are detectable in tests.
5. Given accessibility/localization tests run, when they cover keyboard operation, visible focus, accessible names, status announcements, non-color-only state, localization, forced-colors, and reduced-motion expectations, then the picker is accessible for embedded use.

## Gate Resolution (2026-05-24)

The Epic 8 scheduling gate for this story is resolved by a SCOPED RISK ACCEPTANCE, not a fully satisfied contract. Per `sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md` (approved by Jérôme), `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` now records a Risk Acceptance covering Epic 8 Stories 8.2–8.6 against the existing temporary picker bridge (`Hexalith.Parties.Picker` + `IPartiesQueryClient`). The full EventStore-fronted Parties client/gateway contract is still NOT globally `Satisfied`.

Predecessor stories 8.1–8.4 are now accepted/ready, so the picker shell, result model, state model, and selection callback exist to validate against.

Implementation proceeds under BINDING conditions (see the dependency record's "Risk Acceptance (2026-05-24 - Stories 8.2-8.6)" section):

- All data access routes through `IPartiesQueryClient`; no retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
- Host-supplied auth context only; never persist/refresh/parse/log tokens.
- Narrow, PII-safe DOM callback payloads; fail-closed failure states.
- Existing picker transport/privacy guardrail tests remain binding before close.
- When the formal contract is accepted, this scope reconciles or replaces the provisional bridge.

## Tasks/Subtasks

- [x] Task 1 - Reconfirm accessibility/localization scope and existing picker behavior. (AC: 1-5)
  - [x] 1.1 Re-read this story's Gate Resolution and the 2026-05-24 dependency risk acceptance.
  - [x] 1.2 Verify Stories 8.1-8.4 are reflected in the current picker shell, typeahead, durable selection, and state/retry behavior.
  - [x] 1.3 Keep all data access inside `Hexalith.Parties.Picker` and `IPartiesQueryClient`; do not add retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.

- [x] Task 2 - Strengthen accessible semantics for picker controls and results. (AC: 1, 2, 3)
  - [x] 2.1 Ensure search input, result list, options, selected display, status region, clear control, and retry control expose useful accessible names and relationships.
  - [x] 2.2 Ensure selected, disabled, degraded/local-only, erased/gone, unavailable, retryable, and loading states are represented with text or accessible semantics, not color alone.
  - [x] 2.3 Ensure result options expose selected state and remain operable with keyboard/assistive technology without relying on custom JavaScript focus traps.

- [x] Task 3 - Harden keyboard and focus behavior. (AC: 1, 5)
  - [x] 3.1 Keep every interactive control reachable and operable by keyboard in normal, disabled, read-only, loading, retry, and selected states.
  - [x] 3.2 Preserve or improve visible focus styling, including high-contrast/forced-colors behavior.
  - [x] 3.3 Ensure retry, clear, and selection interactions return focus to the initiating control or a relevant status/selected region.

- [x] Task 4 - Localize all user-facing picker text. (AC: 3, 4)
  - [x] 4.1 Ensure labels, placeholders, titles, button names, state text, selected-display text, retry text, and result-count messages come from `PartyPickerLabels` or the host-provided labels model.
  - [x] 4.2 Add labels for any missing visible or assistive-only strings introduced by this story.
  - [x] 4.3 Add tests that make missing hard-coded or default-only strings detectable without using raw backend details, tokens, tenant ids, query payloads, or contact data.

- [x] Task 5 - Add focused accessibility/localization coverage. (AC: 1-5)
  - [x] 5.1 Extend component tests for keyboard operation, focusable controls, accessible names, selected/disabled/degraded/gone/unavailable state text, status announcements, and retry/clear names.
  - [x] 5.2 Add CSS/static guard tests for visible focus, forced-colors, reduced-motion, and non-color-only state indicators where practical.
  - [x] 5.3 Keep `PartyPickerTransportGuardrailTests` strict for forbidden transports, browser storage, raw markup APIs, server/projection references, and unsafe payload fields.
  - [x] 5.4 Update `docs/frontend/party-picker.md` with host-facing accessibility and localization guarantees.
  - [x] 5.5 Run `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.

## Dev Notes

### Current Implementation Snapshot

- Story 8.4 leaves `PartyPicker.razor` with search input, clear button, selected display, `role="status"` polite announcements, retry button, listbox/options, durable `SelectedPartyIdChanged`, and bounded failure/degraded states.
- `PartyPicker.razor.css` already has visible `:focus-visible` styling and text badges for result/selection states; this story should harden and test those guarantees.
- `PartyPickerLabels` owns host-overridable user-facing strings. Add labels there instead of hard-coding visible or assistive-only text in Razor/CSS/tests.
- Existing picker tests cover shell rendering, localization injection, typeahead, bounded result counts, durable selection, state/retry behavior, stale response suppression, and transport/privacy guardrails.
- `docs/frontend/party-picker.md` already describes compact layout, visible focus outlines, text status, and localized clear labels; extend it with Story 8.5-specific host guarantees.

### Files To Read Before Implementation

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSearchState.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelectionState.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerEventDetail.cs`
- `docs/frontend/party-picker.md`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`

### Boundaries And Anti-Patterns

- Do not use raw HTML/markup, unsafe JavaScript interpolation, browser storage, telemetry/log payloads, or backend ProblemDetails to implement accessibility or localization.
- Do not expose names, contacts, tokens, tenant ids, query payloads, correlation ids, exception messages, or raw backend details through labels, titles, `aria-*` attributes, status text, DOM event payloads, CSS content, logs, or tests.
- Do not replace durable party id callbacks with localized display text.
- Do not add custom focus traps or JavaScript keyboard behavior unless native HTML/Blazor behavior is insufficient and tests prove the behavior remains bounded.
- Do not rely on color alone for selected, disabled, degraded, erased, unavailable, loading, or retryable states.

### Testing Requirements

- Treat accessibility/localization tests as required before moving to review.
- Prefer bUnit semantic assertions and static CSS/file guardrails over brittle pixel assertions.
- Cover both search-result and selected-display paths because they render different state text and controls.
- Keep picker-focused tests green before review; run broader tests only if shared client contracts change.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract and predecessor picker shell remain unsatisfied. | Codex |
| 2026-05-24 | 0.2 | Gate resolved via scoped risk acceptance for Stories 8.2–8.6 (sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md); status blocked → ready-for-dev. | correct-course |
| 2026-05-24 | 0.3 | Added implementation task map and dev notes so dev-story can execute under the scoped risk acceptance after Stories 8.1-8.4 established the picker shell, search, selection, and state behavior. | story-automator |
| 2026-05-24 | 1.0 | Implemented picker accessibility/localization hardening: ARIA relationships, localized result-list and party-type labels, atomic status announcements, forced-colors/reduced-motion CSS guards, focused tests, and host docs. Status: in-progress → review. | Codex GPT-5 |
| 2026-05-24 | 1.1 | Code review auto-fixes: added role="combobox" + aria-haspopup="listbox" to search input (H1/H2 — ARIA combobox pattern completion); removed dead TryFocusStatusAsync() method and _statusElement ref + tabindex (M1); corrected docs focus-target description for selected-party retry (M2); updated test assertion to use role selector. All 130 picker tests green. Status: review → done. | Claude Sonnet 4.6 |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Implementation Plan

- Keep Story 8.5 inside the existing `Hexalith.Parties.Picker` component, labels model, CSS, picker tests, and host documentation.
- Add red-phase bUnit/static guard coverage for accessible relationships, localized result metadata, label fallback completeness, status announcements, focus/forced-colors/reduced-motion CSS, and privacy-safe text.
- Implement only the missing accessibility/localization surface: ARIA relationships, result option selected state, localized result-list and party-type labels, atomic status announcements, and CSS accessibility guards.
- Preserve the existing typed-client boundary, host-supplied auth model, stale-response handling, retry behavior, and transport/privacy guardrails from Stories 8.1-8.4.

### Debug Log References

- Loaded Story 8.5 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; current status is scoped Risk Accepted for Epic 8 Stories 8.1-8.6, while the full EventStore-fronted client/gateway contract remains not globally `Satisfied`.
- Confirmed Stories 8.1 through 8.4 are done, leaving the picker shell, typeahead, durable selection, selected-display resolution, bounded state/retry behavior, and stale-response suppression available for Story 8.5 hardening.
- 2026-05-24 story-automator repair: scoped risk acceptance is now recorded for Stories 8.2-8.6, sprint status shows Stories 8.2-8.4 done, and this story now has executable tasks and dev notes.
- 2026-05-24 dev-story: resolved workflow customization, loaded BMAD config, checklist, persistent project context, sprint status, Story 8.5, dependency risk acceptance, prior Stories 8.1-8.4, and the required picker/docs/test files before implementation.
- 2026-05-24 red phase: focused picker tests failed as expected because `PartyPickerLabels` did not yet expose `Results`, `PersonType`, or `OrganizationType`.
- 2026-05-24 green/refactor: added localized result-list and party-type labels; wired search input/status/list/result ARIA relationships; rendered result options as native buttons with `role="option"` and explicit `aria-selected`; made status announcements atomic; added retry titles/labels; added forced-colors and reduced-motion CSS guards.
- 2026-05-24 verification: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release --no-restore` passed 129/129.
- 2026-05-24 verification: required command `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` passed 129/129.
- 2026-05-24 verification: `git diff --check` on changed Story 8.5 files produced no whitespace errors; Git reported only line-ending normalization warnings.
- 2026-05-24 automate: added native keyboard/result-option erased-state guardrail coverage, retry/clear button type/name assertions, and `_bmad-output/implementation-artifacts/tests/test-summary.md`; parent verification passed 130/130.

### Completion Notes List

- No production implementation started during story-automator repair; the artifact is now ready for dev-story under the scoped Epic 8 risk acceptance.
- Implemented Story 8.5 accessibility/localization hardening without changing picker data access: all search and selected-display behavior still routes through `Hexalith.Parties.Picker` and `IPartiesQueryClient`.
- Search input, result list, options, selected display, status region, clear control, and retry control now expose stronger accessible names/relationships, including input/list `aria-describedby`, atomic polite status, localized result-list label, `role="option"` result buttons, and explicit selected state.
- Added localized `PartyPickerLabels` entries for result-list naming and party-type display text, and covered default-label completeness plus host-provided label substitution for status, counts, retry, clear, selected display, party type, and state badges.
- Hardened non-color-only/focus expectations with static CSS guard coverage for visible focus, forced-colors handling, reduced-motion handling, bounded badges, and no CSS-generated state content.
- Updated `docs/frontend/party-picker.md` with host-facing accessibility and localization guarantees, including privacy-safe bounded announcements.
- Focused picker validation is green after automate: 130 passed, 0 failed, 0 skipped under Release configuration.

### File List

- `_bmad-output/implementation-artifacts/8-5-enforce-picker-accessibility-and-localization.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/frontend/party-picker.md`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
