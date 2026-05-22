# Story 8.2: Implement Typeahead Search and Bounded Results

Status: blocked

## Story

As a host application user,
I want to type ahead by display name and see bounded party results,
so that I can quickly select the correct party.

## Acceptance Criteria

1. Given the user types a display-name query, when the debounce interval completes, then the picker queries through the accepted Parties client/EventStore gateway boundary, and it does not call retired REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
2. Given search results return successfully, when the picker displays them, then it shows a bounded result count in a stable compact layout, and each result includes enough non-PII-safe visible context to choose a party.
3. Given the query is empty or below the documented minimum, when search would otherwise run, then the picker avoids unnecessary queries, and it displays a localized idle or empty state.
4. Given search returns no matches, when results are displayed, then the picker shows a localized empty state, and it does not imply cross-tenant records exist.
5. Given typeahead tests run, when they cover debounce, result bounding, empty query, no results, tenant-safe querying, and endpoint boundary violations, then the picker search remains predictable and safe.

## Blocker

Story 8.2 is blocked by `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

That dependency is still `status: Required` and explicitly lists Story 8.2 as affected. The scheduling gate says no Epic 8 implementation story should be scheduled until the accepted EventStore-fronted Parties client/gateway contract is updated to `Satisfied` or `Risk Accepted`.

Implementing typeahead now would require guessing the accepted picker query method, debounce/failure semantics, and endpoint boundary rules.

## Required To Unblock

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Link the accepted picker query contract from sprint planning or story metadata.
- Confirm the accepted contract covers typeahead query shape, result bounds, tenant-safe failure semantics, and prohibited endpoint boundaries.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 8.2 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed the dependency explicitly lists Story 8.2 and Epic 8 scheduling as gated.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency.

### File List

- `_bmad-output/implementation-artifacts/8-2-implement-typeahead-search-and-bounded-results.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
