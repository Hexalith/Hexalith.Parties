# Story 8.6: Enforce Picker Privacy and Integration Boundary

Status: blocked

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

## Blocker

Story 8.6 is blocked by `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

That dependency is still `status: Required` and explicitly lists Story 8.6 as affected. It also gates all Epic 8 implementation scheduling until the accepted EventStore-fronted Parties client/gateway contract is `Satisfied` or `Risk Accepted`.

Implementing privacy and boundary enforcement now would require guessing the accepted picker integration boundary, query API, telemetry/storage rules, and token handling expectations.

## Required To Unblock

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Link the accepted picker integration contract from sprint planning or story metadata.
- Confirm the accepted contract covers endpoint usage, callback payload shape, telemetry/storage boundaries, token handling, and privacy-safe rendered output.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.6 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed the dependency explicitly lists Story 8.6 and gates all Epic 8 implementation scheduling.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency.

### File List

- `_bmad-output/implementation-artifacts/8-6-enforce-picker-privacy-and-integration-boundary.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
