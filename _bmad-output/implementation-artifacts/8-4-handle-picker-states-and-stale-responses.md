# Story 8.4: Handle Picker States and Stale Responses

Status: blocked

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

## Blocker

Story 8.4 is blocked by `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

That dependency is still `status: Required`. Although Story 8.4 is not individually named in the affected-story list, the dependency explicitly gates all Epic 8 implementation scheduling until the accepted EventStore-fronted Parties client/gateway contract is `Satisfied` or `Risk Accepted`.

Implementing picker state handling now would require guessing how the accepted client boundary reports loading, stale response, authorization, not-found, erased, degraded, and transient-failure states.

## Required To Unblock

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Link the accepted picker query/state contract from sprint planning or story metadata.
- Confirm the accepted contract covers stale-response suppression, retry semantics, selected-display refresh states, and privacy-safe localized status mapping.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |

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
