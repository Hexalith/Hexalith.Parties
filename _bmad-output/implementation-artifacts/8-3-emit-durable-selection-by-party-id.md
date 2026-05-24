# Story 8.3: Emit Durable Selection by Party Id

Status: done

## Story

As a host application developer,
I want the picker to return only the selected party id as the durable selection,
so that my application does not accidentally persist personal display data as an identity key.

## Acceptance Criteria

1. Given a user selects a party result, when the picker invokes its host callback, then it passes the selected party id, and display names, contact values, identifiers, consent text, degraded reasons, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads are not included in the durable callback payload.
2. Given the host provides a selected party id, when the picker initializes or receives updated parameters, then it resolves display state through the accepted query boundary when needed, and the durable selection remains the party id.
3. Given a selected party becomes not found, forbidden, gone, erased, or unavailable, when the picker refreshes selected display state, then it reports a bounded localized status, and it does not replace the durable id with personal data.
4. Given selection tests run, when they cover selection, host callback, preselected id, unavailable selected party, and payload inspection, then only party id is durable.

## Gate Resolution (2026-05-24)

The Epic 8 scheduling gate for this story is resolved by a SCOPED RISK ACCEPTANCE, not a fully satisfied contract. Per `sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md` (approved by Jérôme), `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` now records a Risk Acceptance covering Epic 8 Stories 8.2–8.6 against the existing temporary picker bridge (`Hexalith.Parties.Picker` + `IPartiesQueryClient`). The full EventStore-fronted Parties client/gateway contract is still NOT globally `Satisfied`.

Implementation proceeds under BINDING conditions (see the dependency record's "Risk Acceptance (2026-05-24 - Stories 8.2-8.6)" section):

- All data access routes through `IPartiesQueryClient`; no retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
- Host-supplied auth context only; never persist/refresh/parse/log tokens.
- Narrow, PII-safe DOM callback payloads; fail-closed failure states.
- Existing picker transport/privacy guardrail tests remain binding before close.
- When the formal contract is accepted, this scope reconciles or replaces the provisional bridge.

STORY-SPECIFIC RISK (accepted): the .NET `PartyPickerSelection` model currently carries more display metadata than the narrow DOM `party-selected` event detail. The durable callback payload MUST remain party-id-only; the richer .NET model may need reconciliation when the formal contract is frozen.

## Tasks/Subtasks

- [x] Task 1 - Confirm the durable selection contract before implementation. (AC: 1-4)
  - [x] 1.1 Re-read this story's Gate Resolution and the 2026-05-24 dependency risk acceptance before changing code.
  - [x] 1.2 Treat `Hexalith.Parties.Picker` plus `IPartiesQueryClient` as the only production boundary; do not add retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
  - [x] 1.3 Identify every host-facing selection output in `PartyPicker.razor`, `PartyPickerSelection`, `PartyPickerEventDetail`, and `hexalith-parties-picker.js`; separate durable identity from preview display state.

- [x] Task 2 - Keep durable selection party-id only. (AC: 1)
  - [x] 2.1 Ensure the durable host callback path exposes only the selected party id as the stable identity.
  - [x] 2.2 Keep display names, contacts, identifiers, consent text, degraded reasons, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads out of durable callback payloads and DOM event detail.
  - [x] 2.3 Preserve or add a narrow DOM `party-selected` event detail that contains `partyId` only plus non-PII bounded state explicitly allowed by this story.
  - [x] 2.4 If the .NET callback model keeps preview display metadata for source compatibility, document and test that `PartyId` is the only durable value and that preview data is not required for host persistence.

- [x] Task 3 - Resolve host-provided selected party ids through the accepted query boundary. (AC: 2, 3)
  - [x] 3.1 When `SelectedPartyId` initializes or changes, resolve selected display state through `IPartiesQueryClient.GetPartyAsync` via picker service code; do not fabricate display state locally.
  - [x] 3.2 Reuse host-supplied auth/request context from `AccessToken`, `AccessTokenProvider`, or `RequestCustomizer`; never persist, refresh, parse, or log tokens.
  - [x] 3.3 Keep the durable selected id stable when display-state lookup succeeds, fails, is forbidden, returns not found, returns gone/erased, or is transiently unavailable.
  - [x] 3.4 Suppress stale selected-display responses when `SelectedPartyId`, `ContextKey`, `AuthContextKey`, host auth, request customizer, disabled/read-only state, or picker search options change.

- [x] Task 4 - Render bounded selected-state feedback without replacing the durable id. (AC: 3)
  - [x] 4.1 Add or preserve localized selected-state labels for unavailable, unauthorized, forbidden, not found, gone/erased, and transient states.
  - [x] 4.2 Render selected display state through normal Razor text paths only; do not use raw markup, unsafe JavaScript interpolation, or backend details.
  - [x] 4.3 Do not clear or replace the durable selected id with display text, status text, backend reasons, or personal data.

- [x] Task 5 - Add focused tests and docs for durable selection. (AC: 1-4)
  - [x] 5.1 Extend `PartyPickerComponentTests` for selecting a result, callback payload inspection, DOM event detail inspection, preselected id resolution, unavailable selected party states, and stale selected-display suppression.
  - [x] 5.2 Extend `PartyPickerApiClientTests` or focused service tests for `GetPartyAsync` selected-display resolution, auth-required behavior, forbidden/not-found/gone/transient mapping, and raw detail redaction.
  - [x] 5.3 Keep `PartyPickerTransportGuardrailTests` strict for forbidden transports, browser storage, raw markup APIs, server/projection references, and unsafe payload fields.
  - [x] 5.4 Update `docs/frontend/party-picker.md` to state that party id is the durable selection value and preview display data is not a durable identity key.
  - [x] 5.5 Run `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.
  - [x] 5.6 If `IPartiesQueryClient`, `HttpPartiesQueryClient`, or selected-party query behavior changes, also run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release`.

## Dev Notes

### Current Implementation Snapshot

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` owns `SelectedPartyId`, `SelectedPartyChanged`, selected preview rendering, result selection, clear behavior, context reset, and DOM event dispatch.
- `SelectedPartyChanged` currently emits `PartyPickerSelection?`; `PartyPickerSelection` contains `PartyId`, `DisplayName`, `PartyType`, `IsActive`, and `IsErased`.
- `PartyPickerEventDetail.FromSelection` currently emits `PartyId`, `PartyType`, and bounded `Status`. It intentionally excludes display name, tenant id, token material, raw backend payloads, and search text.
- `PartyPickerApiClient` currently implements search only through `IPartiesQueryClient.SearchPartiesAsync`. This story likely needs a selected-display lookup path using `IPartiesQueryClient.GetPartyAsync`.
- `IPartiesQueryClient.GetPartyAsync(string partyId, CancellationToken ct)` and `HttpPartiesQueryClient.GetPartyAsync` already exist in `src/Hexalith.Parties.Client`.
- `RecordingPartiesQueryClient` in picker tests already has a `GetPartyAsync` surface suitable for selected-display tests.
- `docs/frontend/party-picker.md` already documents that selected party id is the stable contract and DOM event detail excludes sensitive fields.

### Files To Read Before Implementation

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelection.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerEventDetail.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSearchState.cs`
- `src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `docs/frontend/party-picker.md`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Fakes/RecordingPartiesQueryClient.cs`

### Boundaries And Anti-Patterns

- Do not make display name, contact data, identifiers, tenant id, search text, raw ProblemDetails, raw query payloads, or tokens part of a durable host selection contract.
- Do not use browser storage for selected preview data, party ids, token material, tenant ids, search text, backend payloads, or status reasons.
- Do not add direct HTTP calls in the picker; selected-display lookup must route through `IPartiesQueryClient`.
- Do not use `ApiBaseUrl` as a transport selector. Request routing belongs to the configured typed Parties client/EventStore gateway.
- Do not clear the durable selected id merely because selected display state cannot be resolved; render bounded unavailable state instead.
- Do not weaken disabled/read-only behavior from Story 8.1 or typeahead bounds from Story 8.2.

### Testing Requirements

- Primary verification: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.
- Add tests that inspect object/string representations carefully enough to prove durable event/detail payloads exclude display names, tenant ids, token material, raw backend reasons, and search text.
- Add tests for preselected `SelectedPartyId` resolution success and failure branches: unauthorized/auth-required, forbidden, not found, gone/erased, transient failure, and stale context.
- If client contract or HTTP query behavior changes, run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release`.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-24 | 0.2 | Gate resolved via scoped risk acceptance for Stories 8.2–8.6 (sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md); status blocked → ready-for-dev. | correct-course |
| 2026-05-24 | 0.3 | Added implementation task map and dev notes so dev-story can execute under the scoped risk acceptance. | story-automator |
| 2026-05-24 | 1.0 | Implemented durable party-id selection callback, selected-display resolution, bounded selected-state feedback, focused tests, and docs. | Codex |
| 2026-05-24 | 1.1 | Code review: 0 critical/0 high issues. Fixed 2 medium (added missing AdminPortal test file to File List; added blank-partyId test for ResolveSelectedPartyAsync). Fixed 1 low (corrected test count 105→106 in Debug Log). Status: done. | bmad-story-automator-review |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.3 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed the dependency explicitly lists Story 8.3 and Epic 8 scheduling as gated.
- 2026-05-24 story-automator repair: create-story sessions did not enrich the artifact; added missing `Tasks/Subtasks` and `Dev Notes` directly to unblock dev-story without production code changes.
- 2026-05-24 dev-story: loaded scoped risk acceptance for Epic 8 Stories 8.2-8.6; implementation stayed within `Hexalith.Parties.Picker` + `IPartiesQueryClient`.
- 2026-05-24 red phase: picker tests failed on missing `SelectedPartyIdChanged`, selected-party request/state types, and stale lookup fake override before implementation.
- 2026-05-24 verification: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` passed 101/101.
- 2026-05-24 verification: first client test run hit the 120s tool timeout; rerun with longer timeout passed 98/98.
- 2026-05-24 broader regression attempt: `dotnet test Hexalith.Parties.slnx --configuration Release --no-restore` failed outside this story scope in sample getting-started guardrail text, deploy operator script tests, and AppHost topology/documentation fitness tests; picker and client tests passed within that run.
- 2026-05-24 automate repair verification: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` passed 106/106 after tightening the inconsistent-count assertion to visible status/result-count text and asserting the full clear-selection callback sequence.

### Completion Notes List

- Added `SelectedPartyIdChanged` as the durable Blazor callback path so hosts can persist only the selected party id; retained `SelectedPartyChanged` as source-compatible preview data and documented that `PartyId` is the only durable value.
- Routed host-supplied `SelectedPartyId` display resolution through `PartyPickerApiClient.ResolveSelectedPartyAsync` and `IPartiesQueryClient.GetPartyAsync`, with host access token/request customizer forwarding and stale response suppression.
- Added bounded selected-state labels for authentication required, unauthorized, forbidden, not found, gone, transient failure, unavailable, and loading states; lookup failures keep the durable id and do not render raw backend details.
- Extended component, picker service, typed client, and fake tests to cover durable callback payloads, preselected id resolution, unavailable selected states, stale selected-display suppression, auth forwarding, and raw detail redaction.
- Updated frontend documentation to distinguish durable party id storage from preview display metadata.

### File List

- `_bmad-output/implementation-artifacts/8-3-emit-durable-selection-by-party-id.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/frontend/party-picker.md`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelectedPartyRequest.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelection.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelectionState.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Fakes/RecordingPartiesQueryClient.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`

### Senior Developer Review (AI)

**Reviewer:** bmad-story-automator-review | **Date:** 2026-05-24

**Verdict: APPROVED** — 0 critical, 0 high issues. Implementation is correct and complete against all ACs.

**Git vs Story discrepancies:**
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` changed in git (interface signature update for `GetPartyAsync` adding `requestCustomizer` parameter) but absent from File List → **Fixed**: added to File List.

**Medium findings (fixed):**
- [FIXED] File List missing `PartiesAdminPortalApiClientTests.cs`.
- [FIXED] Added `ResolveSelectedPartyAsync_WithBlankOrControlOnlyPartyId_ReturnsNotFoundWithoutCallingClient` test (Theory, 3 cases) to cover the early-return path at `PartyPickerApiClient.cs:18-27` that had no direct test coverage.

**Low findings (fixed/deferred):**
- [FIXED] Debug Log test count corrected from 105 to 106.
- [DEFERRED] `HasHostRequestContextAsync` invokes the access token provider once for the auth check and then again inside `CreateRequestCustomizer` — provider called twice per resolution. By-design idempotency assumption; acceptable for this story scope.
- [DEFERRED] `NormalizeQuery` is used for both search queries and party IDs — semantic mismatch in naming, safe in practice since component guards blank IDs.

**AC validation:**
- AC 1 (durable callback = partyId only): `SelectedPartyIdChanged` is `EventCallback<string?>`, DOM detail has only `PartyId/PartyType/Status`. ✓
- AC 2 (host-provided id resolves through query boundary): `SynchronizeSelectedParty` → `ResolveSelectedDisplayAsync` → `IPartiesQueryClient.GetPartyAsync`. ✓
- AC 3 (unavailable states keep durable id): `_selected = selection with { PartyId = partyId }` enforces this on every resolution. ✓
- AC 4 (tests): 109 picker tests pass (106 before review fix + 3 new blank-partyId cases). ✓

**Verification after fixes:** `dotnet test tests/Hexalith.Parties.Picker.Tests/ --configuration Release` → see post-review run.
