# Story 8.6: Enforce Picker Privacy and Integration Boundary

Status: ready-for-dev

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

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-24 | 0.2 | Gate resolved via scoped risk acceptance for Stories 8.2–8.6 (sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md); status blocked → ready-for-dev. | correct-course |

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
