# Story 8.4: Handle Picker States and Stale Responses

Status: ready-for-dev

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

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-24 | 0.2 | Gate resolved via scoped risk acceptance for Stories 8.2–8.6 (sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md); status blocked → ready-for-dev. | correct-course |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.4 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed the dependency gates all Epic 8 implementation scheduling.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency.

### File List

- `_bmad-output/implementation-artifacts/8-4-handle-picker-states-and-stale-responses.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
