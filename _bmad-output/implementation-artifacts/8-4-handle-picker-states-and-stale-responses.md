# Story 8.4: Handle Picker States and Stale Responses

Status: done

## Story

As a host application user,
I want the picker to handle loading, empty, retry, degraded, unauthorized, forbidden, not-found, gone/erased, and transient failures,
so that selection remains safe and understandable across changing host context.

## Acceptance Criteria

1. Given the picker is loading results or selected display state, when a request is in flight, then it shows a localized loading state, and previous unsafe or stale results are not presented as current.
2. Given token, tenant, user, host configuration, selected id, or search options change, when an in-flight response returns, then stale responses are cleared or suppressed, and the picker reloads only for the current context.
3. Given unauthorized, forbidden, not-found, gone/erased, degraded/local-only, or transient-failure states occur, when the picker renders the state, then it shows bounded localized status text, and it uses non-color-only status indicators.
4. Given a retryable failure occurs, when the user activates retry, then the picker retries the current safe request context, and focus returns to the initiating control or relevant status region.
5. Given state tests run, when they cover loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, transient failures, stale responses, and context changes, then the picker never shows stale cross-context data.

## Gate Resolution (2026-05-24)

The Epic 8 scheduling gate for this story is resolved by a SCOPED RISK ACCEPTANCE, not a fully satisfied contract. Per `sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md` (approved by Jérôme), `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` now records a Risk Acceptance covering Epic 8 Stories 8.2–8.6 against the existing temporary picker bridge (`Hexalith.Parties.Picker` + `IPartiesQueryClient`). The full EventStore-fronted Parties client/gateway contract is still NOT globally `Satisfied`.

Implementation proceeds under BINDING conditions (see the dependency record's "Risk Acceptance (2026-05-24 - Stories 8.2-8.6)" section):

- All data access routes through `IPartiesQueryClient`; no retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
- Host-supplied auth context only; never persist/refresh/parse/log tokens.
- Narrow, PII-safe DOM callback payloads; fail-closed failure states for unauthorized, forbidden, unavailable, malformed, timeout, degraded, not found, gone/erased, and stale responses.
- Existing picker transport/privacy guardrail tests remain binding before close.
- When the formal contract is accepted, this scope reconciles or replaces the provisional bridge.

## Tasks/Subtasks

- [x] Task 1 - Reconfirm the scoped bridge and current state model before changing code. (AC: 1-5)
  - [x] 1.1 Re-read this story's Gate Resolution and the 2026-05-24 dependency risk acceptance.
  - [x] 1.2 Keep all data access inside `Hexalith.Parties.Picker` and `IPartiesQueryClient`; do not add retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
  - [x] 1.3 Identify current search, selected-display, context-change, clear, and disabled/read-only paths in `PartyPicker.razor`, `PartyPickerApiClient`, `PartyPickerLabels`, and existing picker tests.

- [x] Task 2 - Render safe loading and empty states without stale results. (AC: 1, 5)
  - [x] 2.1 Show localized loading text/status while search results or selected-display state are in flight.
  - [x] 2.2 Ensure stale or unsafe results are cleared, hidden, or marked non-current while a new safe request context is loading.
  - [x] 2.3 Preserve bounded empty-result behavior and ensure empty state does not leak raw query, token, tenant, backend detail, or stale party display data.

- [x] Task 3 - Suppress stale responses across host context changes. (AC: 2, 5)
  - [x] 3.1 Guard search responses with the current query/context/options identity so delayed responses cannot repopulate old results.
  - [x] 3.2 Guard selected-display responses with the current `SelectedPartyId`, host auth/request context, `ContextKey`, `AuthContextKey`, disabled/read-only state, and picker options.
  - [x] 3.3 When context changes, clear current transient request state and reload only for the current safe context.

- [x] Task 4 - Render bounded failure and degraded states with non-color-only indicators. (AC: 3)
  - [x] 4.1 Add or preserve localized labels for unauthorized, forbidden, not-found, gone/erased, degraded/local-only, transient failure, unavailable, and malformed states.
  - [x] 4.2 Render each state through Razor text and existing status/badge patterns; do not surface raw backend problem details, correlation ids, tokens, tenant ids, search text, display names from stale contexts, or raw query payloads.
  - [x] 4.3 Ensure state indicators use text or icon/text semantics and are not color-only.

- [x] Task 5 - Add retry behavior for retryable failures. (AC: 4)
  - [x] 5.1 Provide a retry control for retryable search or selected-display failures using the current safe request context.
  - [x] 5.2 After retry activation, return focus to the initiating control or the relevant status region.
  - [x] 5.3 Ensure retry does not reuse stale token, tenant, user, selected id, query, or request-customizer state.

- [x] Task 6 - Add focused state/stale-response tests and docs. (AC: 1-5)
  - [x] 6.1 Extend `PartyPickerComponentTests` for loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, transient failures, stale search responses, stale selected-display responses, and context changes.
  - [x] 6.2 Extend `PartyPickerApiClientTests` or focused service tests for retryable and non-retryable state mapping if service behavior changes.
  - [x] 6.3 Keep `PartyPickerTransportGuardrailTests` strict for forbidden transports, browser storage, raw markup APIs, server/projection references, and unsafe payload fields.
  - [x] 6.4 Update `docs/frontend/party-picker.md` with the bounded state/retry behavior hosts can rely on.
  - [x] 6.5 Run `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.
  - [x] 6.6 If typed client behavior changes, also run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release`.

## Dev Notes

### Current Implementation Snapshot

- Story 8.3 left `PartyPicker.razor` with durable `SelectedPartyIdChanged`, selected-display lookup through `PartyPickerApiClient.ResolveSelectedPartyAsync`, bounded `PartyPickerSelectionState`, and stale selected-display suppression for host context changes.
- `PartyPickerApiClient` routes search and selected-display lookup through `IPartiesQueryClient`; this story should extend state semantics without adding direct transport.
- Existing picker tests already cover typeahead, bounded result counts, durable selection payloads, selected-display resolution, stale selected-display suppression, and transport/privacy guardrails.
- `PartyPickerLabels` owns user-facing picker labels; add state/retry strings there rather than hard-coding text in component logic.
- `docs/frontend/party-picker.md` already documents durable party id selection and should be extended with loading/failure/retry/stale-response host behavior.

### Files To Read Before Implementation

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSearchState.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelection.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelectionState.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelectedPartyRequest.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerEventDetail.cs`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `docs/frontend/party-picker.md`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Fakes/RecordingPartiesQueryClient.cs`

### Boundaries And Anti-Patterns

- Do not replace durable selected party id with display text, state text, or backend details.
- Do not persist, refresh, parse, or log host tokens; host auth stays in `AccessToken`, `AccessTokenProvider`, or `RequestCustomizer`.
- Do not show stale results or stale selected display after token, tenant, user, selected id, query, search options, disabled/read-only state, or host context changes.
- Do not render raw ProblemDetails, correlation ids, exception messages, query payloads, tenant ids, tokens, or contact/display data from stale contexts.
- Do not use browser storage for state, selected preview, token, tenant, query, backend payload, or retry context.
- Do not use color as the only state indicator; render bounded text and accessible semantics.

### Testing Requirements

- Treat stale-response tests as required, not optional: delayed previous-context responses must be unable to repopulate current UI.
- Cover both search-result and selected-display request families where code paths differ.
- Keep picker tests green before moving the story to review; run client tests if `IPartiesQueryClient` or `HttpPartiesQueryClient` changes.

## Senior Developer Review (AI)

**Date:** 2026-05-24 | **Reviewer:** bmad-story-automator-review

**Outcome:** Approved with fixes applied

**AC Verification:**
- AC1 (loading state, stale results cleared): ✅ `ScheduleSearchAsync` sets `Results = []` + `Loading` state immediately via `InvokeAsync(StateHasChanged)`.
- AC2 (stale responses suppressed on context change): ✅ `_searchVersion`/`_selectionVersion` guards + CTS cancellation. Context signature change triggers `ResetEphemeralState()`.
- AC3 (bounded localized status, non-color-only): ✅ All 13 search states and 9 selection states mapped to localized labels. Text in `.hx-party-picker__badge` ensures non-color-only rendering.
- AC4 (retry with focus return): ✅ Search retry → `TryFocusInputAsync`; selected-display retry → `TryFocusSelectedAsync` (fixed: was incorrectly focusing search status div).
- AC5 (state/stale tests): ✅ 126 tests covering loading, empty, retry, degraded, unauthorized, forbidden, not-found, gone, transient, stale search, stale selected-display (context change + selected-id-only change — new test added).

**Issues Fixed (5 total, 0 critical/high):**
- [MEDIUM] Test gap: Added `PartyPicker_StaleSelectedDisplayResponse_DoesNotRepopulateAfterOnlySelectedPartyIdChange` — covers AC5 stale suppression when only `SelectedPartyId` changes.
- [MEDIUM] `DelayedSelectedPartiesQueryClient.EnsureSlot` and `DelayedPartiesQueryClient` now use `TaskCreationOptions.RunContinuationsAsynchronously` (consistency with `SequencedDelayedPartiesQueryClient`).
- [LOW] `StatusMessage` default arm returns `EffectiveLabels.Error` instead of silent `string.Empty` for unknown future states.
- [LOW] Selected-display retry focus corrected: `TryFocusSelectedAsync` targets `_selectedElement` (the relevant selected-party region, with `tabindex="-1"`), not the search status div.
- [LOW] `NormalizeQuery` accepts `string?` and returns `string.Empty` for null, guarding against `null!` bypass.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-24 | 0.2 | Gate resolved via scoped risk acceptance for Stories 8.2–8.6 (sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md); status blocked → ready-for-dev. | correct-course |
| 2026-05-24 | 0.3 | Added implementation task map and dev notes so dev-story can execute under the scoped risk acceptance. | story-automator |
| 2026-05-24 | 0.4 | Implemented bounded picker loading, stale-response suppression, failure/degraded states, retry behavior, focused tests, and host docs; status in-progress → review. | Codex |
| 2026-05-24 | 0.5 | Code review applied 5 fixes: (1) `StatusMessage` default arm returns bounded `Error` label instead of silent empty string; (2) selected-display retry focuses `_selectedElement` (relevant region) instead of search status div; (3) `NormalizeQuery` null-safe for external callers; (4) `DelayedSelectedPartiesQueryClient`/`DelayedPartiesQueryClient` use `RunContinuationsAsynchronously`; (5) added `PartyPicker_StaleSelectedDisplayResponse_DoesNotRepopulateAfterOnlySelectedPartyIdChange` test (AC5 gap). 0 critical/high issues. Verification: Picker tests 126/126 green. Status: review → done. | bmad-story-automator-review |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Implementation Plan

- Keep all picker data access routed through `PartyPickerApiClient` and `IPartiesQueryClient`.
- Preserve existing version/context-signature stale-response suppression and extend UI behavior around it with safe loading, failure, and retry states.
- Map typed projection freshness metadata to bounded local-only/degraded picker states without rendering raw backend warning details.
- Add selected-display retry through the current host request context while keeping durable party id display stable.
- Cover state handling with focused bUnit/service tests and document the host-facing behavior.

### Debug Log References

- Loaded Story 8.4 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; original artifact recorded it as `Required` before the later scoped risk acceptance.
- Confirmed the dependency formerly gated Epic 8 implementation scheduling before the 2026-05-24 scoped risk acceptance.
- 2026-05-24 story-automator repair: added missing `Tasks/Subtasks` and `Dev Notes` directly to unblock dev-story without production code changes.
- 2026-05-24 dev-story: Re-read Gate Resolution and dependency risk acceptance; confirmed Story 8.4 is scoped risk-accepted under the temporary picker bridge.
- 2026-05-24 dev-story: Initial focused picker test run failed on `PartyPicker_NewSearch_ShowsLoadingAndHidesPreviousResultsUntilCurrentResponse`; delayed search completion did not trigger the expected rerender. Updated the component to request renders through the Blazor dispatcher for loading and delayed response transitions.
- 2026-05-24 dev-story: Focused picker suite passed after implementation: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` → 125/125 passed.
- 2026-05-24 code-review: 5 fixes applied; picker suite → 126/126 green.
- 2026-05-24 dev-story: Broader solution regression attempted with `dotnet test Hexalith.Parties.slnx --configuration Release`; picker-focused validation remained green and `Hexalith.Parties.Client.Tests` passed 98/98, but the solution run failed outside the picker scope in pre-existing/non-picker areas: Security personal-data encryption assertion, Sample getting-started guardrail, Contracts packaging fixture, AppHost topology compile/fitness checks, DeployValidation operator-script checks, and a search performance benchmark.

### Completion Notes List

- Scoped bridge reconfirmed: all data access remains in `Hexalith.Parties.Picker` through `IPartiesQueryClient`; no retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, actor-host internals, browser storage, or raw markup APIs were added.
- Search loading now clears visible stale results immediately, renders localized loading text, and accepts responses only through the existing current request/version context.
- Search freshness metadata now maps to bounded local-only/degraded UI states; unauthorized, forbidden, not-found, gone, transient, unavailable, and malformed states remain bounded and non-leaking.
- Retry now covers retryable search failures and selected-display transient/unavailable failures using the current safe host request context, with focus returned to the input/status region.
- Focused component and API-client tests cover loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, transient failures, stale search responses, stale selected-display responses, and context changes.
- Host documentation now describes bounded state, retry, and stale-response guarantees.
- Full solution regression was attempted; remaining failures are outside the picker files changed for this story and are recorded in Debug Log References.

### File List

- `docs/frontend/party-picker.md`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`
- `_bmad-output/implementation-artifacts/8-4-handle-picker-states-and-stale-responses.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
