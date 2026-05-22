# Story 8.1: Compose Embeddable Party Picker Shell

Status: blocked

## Story

As a consuming application developer,
I want an embeddable FrontComposer/Blazor party picker component,
so that my application can offer party selection without building its own party search UI.

## Acceptance Criteria

1. Given a host application references the picker component, when it renders the picker, then the picker appears as an embeddable party search and selection control, and it is not an admin portal, party editor, tenant selector, GDPR surface, or EventStore stream browser.
2. Given the host supplies request/auth context through accepted Parties client or EventStore gateway configuration, when the picker initializes, then it uses that configuration for queries, and it does not persist, refresh, parse for authorization, or log tokens.
3. Given the picker is disabled or read-only, when it renders, then interactive search and selection controls are disabled appropriately, and current selection display remains stable and accessible.
4. Given the picker is embedded in different host layouts, when it renders at supported viewport/container sizes, then it maintains a bounded compact layout, and text and controls do not overlap or resize unpredictably.
5. Given picker shell tests run, when they cover enabled, disabled, read-only, missing host configuration, and embedded layout states, then the picker remains a bounded embeddable component.

## Blocker

Story 8.1 is blocked by `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

That dependency is still `status: Required` and explicitly lists Story 8.1 as affected. The scheduling gate says no Epic 8 implementation story should be scheduled until the accepted EventStore-fronted Parties client/gateway contract is updated to `Satisfied` or `Risk Accepted`.

Implementing the picker shell now would require guessing the accepted query/configuration boundary that the picker must use and would risk creating a shell with the wrong host contract.

## Required To Unblock

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Link the accepted picker query/configuration contract from sprint planning or story metadata.
- Confirm the accepted contract covers picker typeahead, selected-display resolution, tenant-safe failure semantics, and privacy rules for tokens, storage, URLs, filenames, and callbacks.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.1 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed the dependency explicitly lists Story 8.1 and all Epic 8 scheduling as gated.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency.

### File List

- `_bmad-output/implementation-artifacts/8-1-compose-embeddable-party-picker-shell.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
