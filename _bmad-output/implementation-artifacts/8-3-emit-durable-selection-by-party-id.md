# Story 8.3: Emit Durable Selection by Party Id

Status: ready-for-dev

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

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-24 | 0.2 | Gate resolved via scoped risk acceptance for Stories 8.2–8.6 (sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md); status blocked → ready-for-dev. | correct-course |

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
