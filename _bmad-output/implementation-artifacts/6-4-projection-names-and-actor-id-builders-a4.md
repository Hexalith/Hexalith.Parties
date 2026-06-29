---
baseline_commit: 4a3b518
---

# Story 6.4: Projection names and actor id builders (A4)

Status: ready-for-dev

## Story

As a maintainer,
I want projection names and actor id formats defined once,
so that projection actors, rebuild code, tests, and clients cannot diverge silently.

## Acceptance Criteria

1. Given projection names and actor ids are hand-built in multiple places, when shared anchors are added, then `Contracts` exposes `PartyProjectionNames` and `PartyActorIds` builders for detail and index projections.
2. Given detail and index projection actors already have runtime-compatible id formats, when callers adopt the builders, then current actor ids are preserved unless a failing test proves a documented bug must be corrected.
3. Given projection rebuild code and live actors must agree, when rebuild code is updated, then tests prove rebuild formulas match live projection formulas or explicitly document any compatibility exception.
4. Given `Contracts` is infrastructure-free, when builders are added, then no Dapr actor runtime, persistence, EventStore server, or projection implementation package is referenced by `Contracts`.
5. Given the consolidation is complete, when scans run, then local projection-name constants and ad hoc actor-id format strings are removed or limited to tests that assert the canonical value.

## Tasks / Subtasks

- [ ] Add canonical projection anchors in Contracts (AC: 1, 4)
  - [ ] Add `PartyProjectionNames` for detail and index projection names.
  - [ ] Add `PartyActorIds` builder methods for detail and index actor ids.
  - [ ] Keep methods pure and BCL-only.
- [ ] Replace call sites (AC: 1-3, 5)
  - [ ] Update projection actors.
  - [ ] Update projection rebuild/replay services.
  - [ ] Update clients/tests that construct projection type or actor id strings.
  - [ ] Remove obsolete local constants.
- [ ] Add tests (AC: 2, 3, 5)
  - [ ] Assert canonical builder output for detail and index projections.
  - [ ] Assert live actor ids and rebuild ids agree.
  - [ ] Add regression coverage for the documented index rebuild key mismatch if implementation touches that path.
- [ ] Validate (AC: 4, 5)
  - [ ] Run `git diff --check`.
  - [ ] Run focused Projections and gateway/query tests.
  - [ ] Run solution build if available.

## Dev Notes

### Decision Context

- This story implements Class A item A4.
- The scope is in-repo consolidation. Do not adopt EventStore projection platform primitives here; that is deferred Class B/Epic 7.

### Guardrails

- Be careful around projection rebuild compatibility. If fixing the known index rebuild key mismatch changes persisted state behavior, document it in tests and completion notes.
- Do not move projection checkpointing, rebuild orchestration, or freshness vocabulary into shared platform packages in this story.
- Do not add infrastructure dependencies to `Contracts`.
- Projection logs must stay bounded and must not include party, tenant, or actor ids.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.4-Projection-names-and-actor-id-builders-A4`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Read-side-projections--CQRS`
- `src/Hexalith.Parties.Projections/`
- `docs/architecture.md`

