---
baseline_commit: 4a3b518
---

# Story 6.3: Shared claims transformation library (A3)

Status: ready-for-dev

## Story

As a maintainer,
I want JWT claims transformation shared through a dedicated authentication library,
so that the actor host and UI host stop carrying parallel normalization logic.

## Acceptance Criteria

1. Given A3 was open in the original proposal, when this story starts, then the destination is already pinned: create a new `Hexalith.Parties.Authentication` project, not Commons and not `Contracts`.
2. Given the shared implementation needs `Microsoft.AspNetCore.Authentication`, when the project is added, then only the new authentication library takes that dependency and `Hexalith.Parties.Contracts` remains infrastructure-free.
3. Given both the actor host and UI host need tenant normalization, when services are registered, then both consume the shared transformation library while retaining host-specific JWT/OIDC/policy registration in their own projects.
4. Given transformation behavior already has coverage, when tests are moved or extended, then `tid`, `tenant_id`, JSON-array `tenants`, space-delimited `tenants`, idempotency, null, empty, and no-source cases remain covered.
5. Given the consolidation is complete, when the solution builds, then no circular project references or public API leaks are introduced.

## Tasks / Subtasks

- [ ] Add the new authentication library (AC: 1, 2, 5)
  - [ ] Create `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj`.
  - [ ] Add it to `Hexalith.Parties.slnx`.
  - [ ] Reference central package versions only; do not add `Version=` to the project file.
- [ ] Move/shared-implement claims transformation (AC: 2-4)
  - [ ] Place shared transformation code in the new project.
  - [ ] Use `PartiesClaimTypes` from Story 6.1 if available; otherwise coordinate sequencing so this story depends on 6.1.
  - [ ] Keep host-specific authentication registration in the host projects.
- [ ] Update callers (AC: 3, 5)
  - [ ] Update `Hexalith.Parties` actor host registration.
  - [ ] Update `Hexalith.Parties.UI` host registration.
  - [ ] Remove duplicate host-local transformation implementations.
- [ ] Add or move tests (AC: 4)
  - [ ] Add a focused test project or extend an existing test project according to local test organization.
  - [ ] Preserve Story 1.4 normalization and resolver tests.
- [ ] Validate (AC: 5)
  - [ ] Run `git diff --check`.
  - [ ] Run focused authentication/UI tests.
  - [ ] Run solution build if available.

## Dev Notes

### Decision Context

- This story resolves A3. The decision is not open for re-litigation during implementation.
- The project context and architecture already describe the exception: JWT `IClaimsTransformation` needs ASP.NET authentication types, so it cannot live in Contracts.

### Guardrails

- Do not move this logic into Commons as part of Epic 6. Cross-repo platform moves belong to deferred Epic 7.
- Do not change claim names, token shapes, OIDC setup, Keycloak realm configuration, or authorization policy behavior.
- Do not add a browser token flow or public endpoint.
- Do not log token or claim values.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.3-Shared-claims-transformation-library-A3`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`
- `_bmad-output/project-context.md#Critical-Implementation-Rules`
- `src/Hexalith.Parties.UI/Authentication/`

