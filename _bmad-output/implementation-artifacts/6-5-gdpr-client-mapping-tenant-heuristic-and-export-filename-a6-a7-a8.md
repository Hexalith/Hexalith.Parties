---
baseline_commit: 4a3b518
---

# Story 6.5: GDPR client mapping, tenant heuristic, and export filename (A6/A7/A8)

Status: ready-for-dev

## Story

As a maintainer,
I want GDPR client helper behavior centralized,
so that admin and consumer GDPR paths classify failures and export names consistently.

## Acceptance Criteria

1. Given GDPR HTTP status mapping is duplicated, when the shared mapping is added, then `HttpAdminPortalGdprClient` owns one `AdminPortalGdprOutcome` mapping surface and AdminPortal uses it instead of maintaining a second mapping table.
2. Given tenant-text detection is repeated, when the heuristic is centralized, then callers use `PartiesTextHeuristics.ContainsTenant` from `Contracts`.
3. Given export filename builders currently produce different formats, when `PartyExportFileName.Build` is introduced, then all export callers use `party-{tenant}-{yyyyMMddTHHmmssZ}.json`.
4. Given A8 intentionally changes AdminPortal output, when tests are updated, then they explicitly acknowledge the approved change from `party-{tenant}-export-{yyyyMMddHHmmss}Z.json` to the canonical client-shaped format.
5. Given GDPR paths are privacy-sensitive, when failures and filenames are rendered or logged, then no PII, raw problem details, tenant secrets, party display names, contact values, identifiers, tokens, or payloads are exposed.

## Tasks / Subtasks

- [ ] Centralize GDPR outcome mapping (AC: 1, 5)
  - [ ] Move or expose the single mapping surface from `Hexalith.Parties.Client`.
  - [ ] Update AdminPortal callers to use the client-owned mapping.
  - [ ] Remove duplicate status-code tables.
- [ ] Centralize tenant text heuristic (AC: 2, 5)
  - [ ] Add `PartiesTextHeuristics.ContainsTenant` in Contracts.
  - [ ] Replace local heuristic copies.
  - [ ] Keep heuristic behavior bounded and deterministic.
- [ ] Centralize export filename building (AC: 3-5)
  - [ ] Add `PartyExportFileName.Build` in Contracts.
  - [ ] Use UTC timestamps and the canonical `yyyyMMddTHHmmssZ` shape.
  - [ ] Sanitize tenant/party inputs according to existing safe filename behavior.
  - [ ] Replace AdminPortal and Client builders.
- [ ] Add tests (AC: 1-5)
  - [ ] Cover representative HTTP status to GDPR outcome mapping.
  - [ ] Cover tenant heuristic positives/negatives without raw detail leakage.
  - [ ] Cover canonical filename format, UTC handling, invalid characters, and no PII.
  - [ ] Update tests expecting the old AdminPortal `-export-` filename shape.
- [ ] Validate (AC: 5)
  - [ ] Run `git diff --check`.
  - [ ] Run focused Client/AdminPortal/GDPR tests.
  - [ ] Run solution build if available.

## Dev Notes

### Decision Context

- This story implements A6, A7, and A8.
- A8 is pinned: the canonical filename is `party-{tenant}-{yyyyMMddTHHmmssZ}.json`.
- The one expected behavior change is AdminPortal export filename shape. Do not introduce any other user-visible behavior change.

### Guardrails

- Keep GDPR copy and failures bounded. Do not pass raw ProblemDetails into UI copy.
- Do not put HTTP client or AdminPortal dependencies into Contracts.
- Do not change GDPR command/query contracts, EventStore gateway routes, erasure semantics, or export package payloads.
- Do not include party display names, contact values, identifiers, or raw tenant secrets in filenames.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.5-GDPR-client-mapping-tenant-heuristic-and-export-filename-A6A7A8`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Consumer-portal-consent--GDPR-rights-Epics-4-5`
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`
- `docs/gdpr-portability-export.md`

