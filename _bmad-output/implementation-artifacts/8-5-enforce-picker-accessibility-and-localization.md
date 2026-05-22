# Story 8.5: Enforce Picker Accessibility and Localization

Status: blocked

## Story

As a host application user using keyboard or assistive technology,
I want the party picker to be accessible and localized,
so that I can search and select parties without inaccessible or hard-coded interactions.

## Acceptance Criteria

1. Given the picker renders search input, result list, selection display, retry control, and status messages, when a user navigates by keyboard, then all controls are reachable and operable, and visible focus is always present.
2. Given result options are shown, when assistive technology inspects them, then each option has a useful accessible name and state, and selected, disabled, degraded, erased, and unavailable states are not color-only.
3. Given picker status changes, when loading, empty, error, retry, selection, or degraded states occur, then localized status text is announced appropriately, and announcements are bounded and privacy-safe.
4. Given labels, placeholders, validation messages, state text, and counts render, when localization resources are inspected, then user-facing text comes from localized strings or FrontComposer localization, and missing resources are detectable in tests.
5. Given accessibility/localization tests run, when they cover keyboard operation, visible focus, accessible names, status announcements, non-color-only state, localization, forced-colors, and reduced-motion expectations, then the picker is accessible for embedded use.

## Blocker

Story 8.5 is blocked by `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

That dependency is still `status: Required` and gates all Epic 8 implementation scheduling. Story 8.5 also depends on the picker shell and state model from Stories 8.1 through 8.4, which are blocked.

Implementing accessibility and localization enforcement now would require inventing the picker component surface, result/state semantics, and localization ownership before the accepted boundary exists.

## Required To Unblock

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Unblock or explicitly risk-accept Stories 8.1 through 8.4 so there is a picker shell, result model, state model, and selection callback to validate.
- Confirm the accepted picker surface covers keyboard operation, accessible names, status announcements, localization resources, forced-colors behavior, and reduced-motion expectations.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract and predecessor picker shell remain unsatisfied. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.5 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed Stories 8.1 through 8.4 are blocked, leaving no accepted picker shell or state model to validate.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency and blocked predecessor stories.

### File List

- `_bmad-output/implementation-artifacts/8-5-enforce-picker-accessibility-and-localization.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
