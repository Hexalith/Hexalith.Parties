---
baseline_commit: 4a3b518
---

# Story 6.6: Shared role arrays and policy names (A9)

Status: ready-for-dev

## Story

As a maintainer,
I want role and policy names defined once,
so that authorization checks, navigation, and tests cannot drift through duplicated strings.

## Acceptance Criteria

1. Given role and policy names are repeated across hosts and portals, when shared anchors are added, then `Contracts` exposes `PartiesRoles` and policy-name constants used by the actor host, UI host, and portals.
2. Given the UI intentionally grants Admin access to `TenantOwner` aliases, when role arrays are centralized, then the shared base arrays are reused while the UI host still composes its documented `TenantOwner` additions.
3. Given Consumer and Admin navigation must never cross-render, when call sites adopt shared role/policy constants, then existing authorization behavior is unchanged.
4. Given policy names become shared anchors, when tests run, then Admin, TenantOwner, Consumer, forbidden, and unauthenticated paths remain covered.
5. Given this is a consolidation story, when implementation completes, then no new roles, policies, claims, or authorization semantics are introduced.

## Tasks / Subtasks

- [ ] Add shared role/policy anchors in Contracts (AC: 1, 5)
  - [ ] Add role names and policy names to a focused type such as `PartiesRoles`.
  - [ ] Keep UI-specific additions composable outside Contracts if needed.
- [ ] Replace duplicated strings (AC: 1-3)
  - [ ] Update actor host authorization registration.
  - [ ] Update UI host policy registration.
  - [ ] Update AdminPortal/ConsumerPortal route or nav references.
  - [ ] Update tests and topology fixtures.
- [ ] Preserve TenantOwner behavior (AC: 2-4)
  - [ ] Keep the UI host's documented `TenantOwner` and lowercase alias support if currently present.
  - [ ] Add tests that prove TenantOwner still lands in/authorizes Admin.
- [ ] Validate (AC: 3-5)
  - [ ] Run `git diff --check`.
  - [ ] Run focused UI authorization/nav tests and topology tests.
  - [ ] Run solution build if available.

## Dev Notes

### Decision Context

- This story implements Class A item A9.
- The proposal explicitly says not to force identical role arrays everywhere. Shared base roles live in Contracts; host-specific additions remain host composition.

### Guardrails

- Do not change role names emitted by Keycloak/tache or topology fixtures.
- Do not weaken policy gates or replace policy checks with role checks in components.
- Do not make Admin and Consumer nav cross-render.
- Do not add new roles or silently remove `TenantOwner` access.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.6-Shared-role-arrays-and-policy-names-A9`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Consumer-portal-consent--GDPR-rights-Epics-4-5`
- `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`
- `tests/Hexalith.Parties.UI.Tests/`

