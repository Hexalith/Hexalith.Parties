# Story 8.6: Enforce Picker Privacy and Integration Boundary

Status: done

## Story

As a privacy-conscious host application developer,
I want the picker to avoid leaking personal data or bypassing the accepted Parties boundary,
so that embedded selection remains safe in every consuming application.

## Acceptance Criteria

1. Given party data, host labels, backend messages, degraded reasons, localized values, or raw user search text are displayed, when the picker renders content, then everything is encoded through normal component text paths, and raw markup, raw HTML fragments, and unsafe JavaScript interpolation are not used.
2. Given the picker creates storage keys, telemetry dimensions, URLs, logs, filenames, DOM event names, or JavaScript event payloads, when party/search/context data is available, then names, contacts, identifiers, consent text, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads are excluded, and only non-PII safe identifiers are used where explicitly allowed.
3. Given the picker needs party data, when it queries, then it goes through the EventStore-fronted Parties client boundary, and it never calls retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
4. Given host auth context is provided, when the picker operates, then it does not persist, refresh, parse for authorization, or log tokens, and missing or invalid context fails closed.
5. Given privacy/boundary tests run, when they inspect rendered output, callbacks, logs, telemetry, routes, storage, JavaScript event payloads, endpoint usage, and token handling, then no PII leakage or boundary bypass is detected.

## Gate Resolution (2026-05-24)

The Epic 8 scheduling gate for this story is resolved by a SCOPED RISK ACCEPTANCE, not a fully satisfied contract. Per `sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md` (approved by Jérôme), `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` now records a Risk Acceptance covering Epic 8 Stories 8.2–8.6 against the existing temporary picker bridge (`Hexalith.Parties.Picker` + `IPartiesQueryClient`). The full EventStore-fronted Parties client/gateway contract is still NOT globally `Satisfied`.

Implementation proceeds under BINDING conditions (see the dependency record's "Risk Acceptance (2026-05-24 - Stories 8.2-8.6)" section):

- All data access routes through `IPartiesQueryClient`; no retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
- Host-supplied auth context only; never persist/refresh/parse/log tokens.
- Narrow, PII-safe DOM callback payloads; fail-closed failure states.
- Existing picker transport/privacy guardrail tests remain binding before close.
- When the formal contract is accepted, this scope reconciles or replaces the provisional bridge.

This story (8.6) is the privacy/integration-boundary enforcer for the picker; its acceptance criteria directly encode the binding conditions above as tested guarantees.

Predecessor stories 8.1-8.5 are done, so the picker shell, typeahead, durable selection callback, bounded state/retry handling, stale-response suppression, accessibility semantics, and localization hooks exist for this final boundary-hardening story.

## Tasks/Subtasks

- [x] Task 1 - Reconfirm the scoped bridge and privacy boundary before changing code. (AC: 1-5)
  - [x] 1.1 Re-read this story's Gate Resolution and the 2026-05-24 dependency risk acceptance.
  - [x] 1.2 Verify Stories 8.1-8.5 are reflected in the current picker shell, typeahead, selection, state/retry, accessibility, and localization behavior.
  - [x] 1.3 Keep all data access inside `Hexalith.Parties.Picker` and `IPartiesQueryClient`; do not add retired REST/admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.

- [x] Task 2 - Harden rendered content against PII leakage and raw markup. (AC: 1, 2, 5)
  - [x] 2.1 Ensure party labels, host labels, status messages, degraded reasons, localized values, and raw search text render only through normal Razor/component text paths.
  - [x] 2.2 Prohibit raw markup/HTML paths such as `MarkupString`, `AddMarkupContent`, `innerHTML`, unsafe markdown, or JavaScript string interpolation for untrusted picker content.
  - [x] 2.3 Ensure visible text, titles, `aria-*` attributes, CSS content, status regions, and selected-display state exclude raw names/contact values where not explicitly required, tenant ids, tokens, raw ProblemDetails, consent text, raw query payloads, and backend exception details.

- [x] Task 3 - Lock down callback, JavaScript, storage, route, telemetry, and logging surfaces. (AC: 2, 4, 5)
  - [x] 3.1 Keep the DOM `party-selected` payload restricted to the approved bounded detail (`partyId`, `partyType`, `status`) and exclude display names, contacts, tenant ids, search text, tokens, identifiers, consent text, backend details, and raw query payloads.
  - [x] 3.2 Verify picker JavaScript dispatch does not add or derive unsafe fields, does not interpolate untrusted content, and does not use browser storage, cookies, filenames, route fragments, or telemetry/log dimensions for party/search/auth data.
  - [x] 3.3 Ensure host auth context remains host-supplied only: the picker may attach host tokens to current requests but must not persist, refresh, parse for authorization, expose, or log them; missing/invalid context fails closed.

- [x] Task 4 - Enforce the integration boundary in source and package references. (AC: 3, 4, 5)
  - [x] 4.1 Ensure picker production code uses `PartyPickerApiClient`/`IPartiesQueryClient` only for party data and does not construct retired `/api/v1/parties` URLs, admin routes, controller calls, actor ids, DAPR actors, projection actors, local search services, or actor-host internals.
  - [x] 4.2 Ensure package/project references do not pull server, projection, actor-host, DAPR actor, local search, or admin surfaces into `Hexalith.Parties.Picker`.
  - [x] 4.3 Preserve fail-closed behavior when host configuration, token provider, request customizer, selected id, query, or typed-client responses are missing, malformed, unauthorized, forbidden, unavailable, stale, or degraded.

- [x] Task 5 - Add focused privacy/boundary guardrail tests and host docs. (AC: 1-5)
  - [x] 5.1 Extend `PartyPickerTransportGuardrailTests` or focused static tests for raw markup APIs, JavaScript event-detail whitelist, browser storage/cookies, route/URL construction, telemetry/logging strings, token persistence/parsing, package references, and forbidden integration symbols.
  - [x] 5.2 Ensure source scans exclude generated/cache/build artifacts (`bin`, `obj`, `.lscache`, generated output) so guardrails validate production/test sources without false positives.
  - [x] 5.3 Extend component/service tests where useful to prove rendered output, callbacks, safe reasons, selected-display fallback, retry, and event serialization do not leak raw party/search/auth/backend details.
  - [x] 5.4 Update `docs/frontend/party-picker.md` with the final Story 8.6 privacy and integration-boundary guarantees.
  - [x] 5.5 Run `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release`.
  - [x] 5.6 If `IPartiesQueryClient`, `HttpPartiesQueryClient`, or shared client contracts change, also run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release`.

## Dev Notes

### Current Implementation Snapshot

- Stories 8.1-8.5 leave `PartyPicker.razor` with a composed shell, typed typeahead through `PartyPickerApiClient`, durable `SelectedPartyIdChanged`, narrow DOM event detail dispatch, bounded loading/failure/retry states, stale-response suppression, ARIA relationships, localized labels, and forced-colors/reduced-motion CSS guards.
- `PartyPickerEventDetail` is the approved DOM callback DTO. It currently exposes only `PartyId`, `PartyType`, and `Status`; Story 8.6 should preserve that whitelist with tests.
- `wwwroot/hexalith-parties-picker.js` dispatches `party-selected` from the provided detail. It should remain a thin dispatcher and must not enrich, persist, log, or interpolate party/search/auth data.
- `PartyPickerTransportGuardrailTests` already scans picker source/project references for forbidden transports, raw markup APIs, browser storage/cookie APIs, DAPR/server/projection/admin symbols, and unsafe storage patterns. This story should close any remaining privacy/boundary gaps while avoiding generated/cache files.
- `docs/frontend/party-picker.md` already states the typed-client boundary, bounded status text, token handling, event payload limits, and raw markup prohibition; extend it with final host-facing guarantees instead of introducing a new doc.

### Files To Read Before Implementation

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerEventDetail.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelection.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSearchState.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerSelectionState.cs`
- `src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js`
- `src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `docs/frontend/party-picker.md`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerPackagingTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`

### Boundaries And Anti-Patterns

- Do not expose display names, contacts, tenant ids, user ids, raw search text, backend problem details, correlation ids, consent text, identifiers, raw query payloads, exception messages, or tokens through DOM callbacks, storage, routes, filenames, logs, telemetry, status text, `aria-*`, titles, CSS content, or JavaScript.
- Do not persist, refresh, parse for authorization, decode, fingerprint into public output, store, or log host tokens. Host auth stays in the current request path only.
- Do not introduce direct `HttpClient` usage, retired REST/admin endpoints, DAPR actors, projection actors, controllers, actor-host internals, local search services, or server/projection package references in the picker package.
- Do not use raw HTML/markup rendering or unsafe JavaScript interpolation for party, host, backend, localized, degraded, or search text.
- Do not expand the public DOM event payload beyond the approved bounded detail without updating the formal contract and tests.

### Testing Requirements

- Treat privacy/boundary guardrails as required close criteria for this story.
- Prefer static/source guardrails for forbidden APIs, event payload shape, project references, storage/cookies, and transport boundaries; pair them with bUnit/service tests for rendered/callback behavior.
- Keep scans narrowly scoped to source/test/doc files that represent the product boundary. Exclude generated/cache/build artifacts such as `.lscache`, `bin`, `obj`, coverage output, and generated temporary files.
- Keep the focused picker suite green before moving the story to review; run client tests only if shared client contracts change.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-24 | 0.2 | Gate resolved via scoped risk acceptance for Stories 8.2–8.6 (sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md); status blocked → ready-for-dev. | correct-course |
| 2026-05-24 | 0.3 | Added implementation task map and dev notes so dev-story can execute the privacy/integration-boundary enforcement under the scoped risk acceptance after Stories 8.1-8.5 established the picker behavior. | story-automator |
| 2026-05-24 | 0.4 | Completed picker privacy/integration-boundary hardening: token context no longer fingerprints raw token values, DOM event dispatch re-whitelists payload fields, guardrail tests/docs updated, and focused picker suite is green. | Codex |
| 2026-05-24 | 0.5 | Code review: added test verifying token value change (same presence) does not re-trigger selection lookup; strengthened JS whitelist negative check from whitespace-pattern to `detail: detail` pass-through check. 162/162 green. Status review → done. | claude-sonnet-4-6 |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.6 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; current status is scoped Risk Accepted for Epic 8 Stories 8.1-8.6, while the full EventStore-fronted client/gateway contract remains not globally `Satisfied`.
- Confirmed the dependency explicitly lists Story 8.6 under the 2026-05-24 risk acceptance with binding privacy, auth, callback, fail-closed, and integration-boundary conditions.
- 2026-05-24 story-automator repair: scoped risk acceptance is now recorded for Stories 8.2-8.6, sprint status shows Stories 8.2-8.5 done, and this story now has executable tasks and dev notes.
- 2026-05-24 dev-story: re-read the Story 8.6 gate resolution and dependency risk acceptance; confirmed Stories 8.1-8.5 are done in sprint status and represented in the current picker shell, typeahead, durable selection, retry/stale-response, accessibility, and localization behavior.
- 2026-05-24 dev-story: removed token-value fingerprinting from `PartyPicker.razor`; context signatures now track only token presence plus host-provided `AuthContextKey`/context/customizer identity.
- 2026-05-24 dev-story: changed `hexalith-parties-picker.js` to build a fresh `safeDetail` object with only `partyId`, `partyType`, and `status` before dispatching `party-selected`.
- 2026-05-24 validation: `dotnet test tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj --configuration Release` passed: 137 passed, 0 failed, 0 skipped.
- 2026-05-24 validation: `dotnet test Hexalith.Parties.slnx --configuration Release` was attempted; Story 8.6 picker tests passed in that run, but the broader solution failed in unrelated/pre-existing areas: AppHost compile drift, Sample/getting-started guardrails, DeployValidation operator-script expectations, a search benchmark, and a Client.Tests output file lock.

### Completion Notes List

- Completed Story 8.6 under the scoped Epic 8 picker risk acceptance without changing `IPartiesQueryClient`, `HttpPartiesQueryClient`, or shared client contracts.
- Preserved picker data access through `PartyPickerApiClient`/`IPartiesQueryClient`; no REST/admin/DAPR actor/projection/local-search/server boundary was introduced.
- Hardened host-auth handling by removing token fingerprinting from component state signatures; hosts use non-sensitive `AuthContextKey` to invalidate stale auth contexts.
- Hardened the DOM event boundary by re-whitelisting `party-selected` JavaScript detail to `partyId`, `partyType`, and `status` only.
- Extended static guardrails for token parsing/fingerprinting, JavaScript event-detail shape, generated/cache/build scan exclusions, package references, storage/cookies, raw markup, and forbidden transport symbols.
- Updated host documentation with final privacy and integration-boundary guidance.

### File List

- `_bmad-output/implementation-artifacts/8-6-enforce-picker-privacy-and-integration-boundary.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `docs/frontend/party-picker.md`
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js`
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`
