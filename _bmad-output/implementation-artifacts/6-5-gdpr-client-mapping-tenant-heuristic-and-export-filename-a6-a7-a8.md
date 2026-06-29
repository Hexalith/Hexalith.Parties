---
baseline_commit: 4a3b518
---

# Story 6.5: GDPR client mapping, tenant heuristic, and export filename (A6/A7/A8)

Status: done

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

- [x] Centralize GDPR outcome mapping (AC: 1, 5)
  - [x] Move or expose the single mapping surface from `Hexalith.Parties.Client`.
  - [x] Update AdminPortal callers to use the client-owned mapping.
  - [x] Remove duplicate status-code tables.
- [x] Centralize tenant text heuristic (AC: 2, 5)
  - [x] Add `PartiesTextHeuristics.ContainsTenant` in Contracts.
  - [x] Replace local heuristic copies.
  - [x] Keep heuristic behavior bounded and deterministic.
- [x] Centralize export filename building (AC: 3-5)
  - [x] Add `PartyExportFileName.Build` in Contracts.
  - [x] Use UTC timestamps and the canonical `yyyyMMddTHHmmssZ` shape.
  - [x] Sanitize tenant/party inputs according to existing safe filename behavior.
  - [x] Replace AdminPortal and Client builders.
- [x] Add tests (AC: 1-5)
  - [x] Cover representative HTTP status to GDPR outcome mapping.
  - [x] Cover tenant heuristic positives/negatives without raw detail leakage.
  - [x] Cover canonical filename format, UTC handling, invalid characters, and no PII.
  - [x] Update tests expecting the old AdminPortal `-export-` filename shape.
- [x] Validate (AC: 5)
  - [x] Run `git diff --check`.
  - [x] Run focused Client/AdminPortal/GDPR tests.
  - [x] Run solution build if available.

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

## Dev Agent Record

### Debug Log

- 2026-06-29T11:33:49+02:00 - Preserved existing `baseline_commit: 4a3b518`; moved story and sprint status to `in-progress`.
- 2026-06-29T11:40:00+02:00 - Added red-phase tests for Contracts helpers, client GDPR outcome mapping, and AdminPortal filename expectations.
- 2026-06-29T11:45:03+02:00 - Validated with focused direct xUnit runs because `dotnet test`/`scripts/test.ps1` MTP build target emitted only generic build-failed output in this workspace.

### Completion Notes

- Exposed `HttpAdminPortalGdprClient.MapGdprOutcome` as the client-owned GDPR HTTP status mapping surface and updated AdminPortal GDPR exception handling to call it.
- Added `PartiesTextHeuristics.ContainsTenant` in Contracts and replaced local tenant-text heuristic copies in Client/AdminPortal paths.
- Added `PartyExportFileName.Build` in Contracts, removed the AdminPortal-specific filename builder, and updated AdminPortal and Client export paths to use the shared UTC `yyyyMMddTHHmmssZ` filename shape.
- Updated tests to cover mapping, heuristic, filename canonicalization/sanitization, public API snapshot additions, and the approved AdminPortal filename shape change.
- Full Contracts/Client direct test runs still hit package-test infrastructure failures around nested `dotnet pack --artifacts-path`; focused story tests pass and the solution builds with the explicit `HexalithCommonsRoot` property.

### File List

- `_bmad-output/implementation-artifacts/6-5-gdpr-client-mapping-tenant-heuristic-and-export-filename-a6-a7-a8.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Services/GdprExportFileNameBuilder.cs` (deleted)
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.AdminPortal/_Imports.razor`
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`
- `src/Hexalith.Parties.Contracts/PartiesTextHeuristics.cs`
- `src/Hexalith.Parties.Contracts/PartyExportFileName.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprPrivacyGuardrailTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt`
- `tests/Hexalith.Parties.Contracts.Tests/PartiesTextHeuristicsTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/PartyExportFileNameTests.cs`
- `tests/e2e/specs/admin-parties-list.spec.ts`

### Change Log

- 2026-06-29: Centralized GDPR mapping, tenant heuristic, and export filename helpers for story 6.5; updated tests and validation evidence.
- 2026-06-29: Senior Developer Review (AI) — auto-fixed File List gap (e2e spec) and strengthened `MapGdprOutcome` test coverage (title/globalErrors tenant detection, 400/408/429/unknown status codes).

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot — 2026-06-29
**Outcome:** Approve (all AC implemented; no CRITICAL/HIGH findings)

### Validation evidence

- Builds clean (`-m:1`): Contracts, Client, AdminPortal, and all three test projects — 0 warnings, 0 errors.
- Focused tests pass via direct xUnit v3 runners: Contracts 16/16 (helpers + privacy guardrails), Contracts public-API snapshot 1/1, Client GDPR 33/33 (after coverage additions), AdminPortal components 111/111.
- `git diff --check` clean. No lingering `GdprExportFileNameBuilder` / `BuildSafeExportFileName` / duplicate `ContainsTenant` references remain in `src/`.

### Findings and resolutions

- **[MEDIUM][fixed] File List gap.** `tests/e2e/specs/admin-parties-list.spec.ts` was modified (filename regex updated to canonical `T...Z` shape) but absent from the story File List. Added.
- **[MEDIUM][fixed] Test coverage gap.** The consolidated `HttpAdminPortalGdprClient.MapGdprOutcome` added `title` and `globalErrors` tenant-detection branches, but the new theory only exercised the `detail` source. Added facts covering title-based and globalErrors-based `MissingTenant` detection plus a forbidden-without-signals case.
- **[LOW][fixed] Status-code coverage.** Extended the mapping theory with `400`, `408`, `429`, and an unknown (`418`) status to pin the full switch surface.
- **[LOW][noted] Client filename bound.** The shared `PartyExportFileName.Build` now truncates the token to 64 chars and trims dashes; the previous Client `BuildSafeExportFileName` did not truncate. This is within the story's "bounded and deterministic" guardrail and an improvement, not a regression — the canonical `yyyyMMddTHHmmssZ` format string is unchanged for the Client path.
