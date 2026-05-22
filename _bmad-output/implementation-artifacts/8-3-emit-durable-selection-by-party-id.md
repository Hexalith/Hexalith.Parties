# Story 8.3: Emit Durable Selection by Party Id

Status: blocked

## Story

As a host application developer,
I want the picker to return only the selected party id as the durable selection,
so that my application does not accidentally persist personal display data as an identity key.

## Acceptance Criteria

1. Given a user selects a party result, when the picker invokes its host callback, then it passes the selected party id, and display names, contact values, identifiers, consent text, degraded reasons, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads are not included in the durable callback payload.
2. Given the host provides a selected party id, when the picker initializes or receives updated parameters, then it resolves display state through the accepted query boundary when needed, and the durable selection remains the party id.
3. Given a selected party becomes not found, forbidden, gone, erased, or unavailable, when the picker refreshes selected display state, then it reports a bounded localized status, and it does not replace the durable id with personal data.
4. Given selection tests run, when they cover selection, host callback, preselected id, unavailable selected party, and payload inspection, then only party id is durable.

## Blocker

Story 8.3 is blocked by `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

That dependency is still `status: Required` and explicitly lists Story 8.3 as affected. The scheduling gate says no Epic 8 implementation story should be scheduled until the accepted EventStore-fronted Parties client/gateway contract is updated to `Satisfied` or `Risk Accepted`.

Implementing durable selection now would require guessing the accepted selected-display resolution contract and callback privacy rules.

## Required To Unblock

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Link the accepted selected-display query and callback contract from sprint planning or story metadata.
- Confirm the accepted contract covers selection payload shape, preselected id resolution, unavailable selected-party states, and callback privacy rules.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.3 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed the dependency explicitly lists Story 8.3 and Epic 8 scheduling as gated.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency.

### File List

- `_bmad-output/implementation-artifacts/8-3-emit-durable-selection-by-party-id.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
