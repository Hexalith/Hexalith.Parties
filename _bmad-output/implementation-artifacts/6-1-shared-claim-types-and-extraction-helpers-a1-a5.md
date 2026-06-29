---
baseline_commit: 4a3b518
---

# Story 6.1: Shared claim types and extraction helpers (A1/A5)

Status: ready-for-dev

## Story

As a maintainer,
I want claim type constants and principal extraction helpers defined once,
so that tenant and user scope cannot drift across hosts, portals, and clients.

## Acceptance Criteria

1. Given duplicated claim literals exist for `eventstore:tenant`, `party_id`, `sub`, and `oid`, when shared anchors are added, then `Hexalith.Parties.Contracts` exposes `PartiesClaimTypes` constants and all application projects use them instead of local copies or raw string literals.
2. Given user and tenant extraction logic is repeated, when extraction helpers are added, then callers use one BCL-only shared helper surface that handles normalized tenant claims plus `sub`/`oid` consistently.
3. Given a principal has missing, empty, or ambiguous scope claims, when callers use the helper, then the result fails closed without throwing or silently choosing an arbitrary value.
4. Given `Contracts` is infrastructure-free, when this story is implemented, then no ASP.NET, Dapr, EventStore server, persistence, UI, or host package reference is added to `Hexalith.Parties.Contracts`.
5. Given the consolidation is complete, when tests and scans run, then constants/extraction parity is covered and the boundary fitness test remains green.

## Tasks / Subtasks

- [ ] Add shared claim constants in `src/Hexalith.Parties.Contracts/Authorization/PartiesClaimTypes.cs` (AC: 1, 4)
  - [ ] Include constants for `eventstore:tenant`, `party_id`, `sub`, and `oid`.
  - [ ] Keep the file BCL-only and named for the type.
- [ ] Add BCL-only extraction helpers in Contracts (AC: 2-4)
  - [ ] Prefer a focused static helper or extension type that works with `ClaimsPrincipal` / `ClaimsIdentity`.
  - [ ] Return explicit success/failure results for tenant and user extraction instead of throwing for routine missing claims.
  - [ ] Preserve current fail-closed behavior for ambiguous `party_id` and tenant values.
- [ ] Replace local constants and helper copies (AC: 1-3)
  - [ ] Update actor host authentication/authorization code.
  - [ ] Update UI authentication/self-scope code.
  - [ ] Update AdminPortal and ConsumerPortal call sites that inspect claims.
  - [ ] Remove obsolete local constants after callers move.
- [ ] Add or update tests (AC: 1-5)
  - [ ] Cover normalized tenant, subject fallback order, object id fallback, absent values, empty values, and ambiguity.
  - [ ] Add a scan or focused assertion that the known raw claim literals no longer appear outside the shared anchor, except in tests that intentionally assert wire values.
  - [ ] Keep existing Story 1.4 party binding tests green.
- [ ] Validate (AC: 5)
  - [ ] Run `git diff --check`.
  - [ ] Run focused Contracts/UI/authentication tests.
  - [ ] Run `dotnet build Hexalith.Parties.slnx -c Release --no-restore` if the environment supports it.

## Dev Notes

### Decision Context

- This story implements Class A items A1 and A5 from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md`.
- The readiness remediation pins shared claim constants and pure extraction helpers to `Hexalith.Parties.Contracts`.
- Do not move JWT transformation logic in this story; that is Story 6.3 and belongs in `Hexalith.Parties.Authentication`.

### Guardrails

- `Contracts` may use BCL types such as `System.Security.Claims`; it must not take ASP.NET or host dependencies.
- Consumer own-data access remains fail-closed. Do not weaken `PartyIdClaimResolver`, `NoPartyBinding`, or `ISelfScopedPartiesClient` behavior to make helpers easier to call.
- Do not add new claim names or change token semantics. This is consolidation, not a new authentication design.
- Do not log claim values, tenant ids, user ids, party ids, or tokens.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.1-Shared-claim-types-and-extraction-helpers-A1A5`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Critical-Implementation-Rules`
- `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs`
- `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`

