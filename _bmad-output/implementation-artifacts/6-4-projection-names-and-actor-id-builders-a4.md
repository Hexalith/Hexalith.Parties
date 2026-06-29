---
baseline_commit: 4a3b518
---

# Story 6.4: Projection names and actor id builders (A4)

Status: done

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

- [x] Add canonical projection anchors in Contracts (AC: 1, 4)
  - [x] Add `PartyProjectionNames` for detail and index projection names.
  - [x] Add `PartyActorIds` builder methods for detail and index actor ids.
  - [x] Keep methods pure and BCL-only.
- [x] Replace call sites (AC: 1-3, 5)
  - [x] Update projection actors.
  - [x] Update projection rebuild/replay services.
  - [x] Update clients/tests that construct projection type or actor id strings.
  - [x] Remove obsolete local constants.
- [x] Add tests (AC: 2, 3, 5)
  - [x] Assert canonical builder output for detail and index projections.
  - [x] Assert live actor ids and rebuild ids agree.
  - [x] Add regression coverage for the documented index rebuild key mismatch if implementation touches that path.
- [x] Validate (AC: 4, 5)
  - [x] Run `git diff --check`.
  - [x] Run focused Projections and gateway/query tests.
  - [x] Run solution build if available.

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

## Dev Agent Record

### Debug Log

- 2026-06-29T10:58:02+02:00: Marked story and sprint status in-progress; preserved existing `baseline_commit: 4a3b518`.
- 2026-06-29T11:07:40+02:00: Added `PartyProjectionNames` and `PartyActorIds` in Contracts; replaced production projection-name and actor-id call sites.
- 2026-06-29T11:07:40+02:00: Corrected `ProjectionRebuildService` index state key from `{tenant}:party-index:all` to the live actor key `{tenant}:party-index:default`; updated docs that listed the mismatch as open.
- 2026-06-29T11:07:40+02:00: `dotnet test` builds test assemblies but cannot execute in this sandbox because the .NET test runner fails to bind its IPC named pipe (`SocketException 13: Permission denied`); used direct xUnit v3 test executable invocation per project guidance.
- 2026-06-29T11:07:40+02:00: Full `Hexalith.Parties.Tests` direct executable passed 495 tests with 0 failures.

### Completion Notes

- Implemented BCL-only canonical projection anchors in `Hexalith.Parties.Contracts`.
- Preserved current detail and index projection actor id formats through `PartyActorIds.Detail(tenantId, partyId)` and `PartyActorIds.Index(tenantId)`.
- Replaced production ad hoc projection names and actor-id builders in projection actors, rebuild service, query actors, projection delivery, health check, erasure cleanup, and query client code.
- Added regression coverage proving canonical builder output and pinning rebuild index reads/writes to the same state key used by the live index actor.
- Validation passed for project builds, full `Hexalith.Parties.Tests`, focused direct xUnit suites, source literal scan, `git diff --check`, and warning-override/nested-submodule guard. Solution-level `.slnx` build remains blocked by a no-error MSBuild submodule metaproject failure in `Hexalith.Commons.ServiceDefaults`; direct affected project builds pass.
- Corrected the GDPR detail-query wire contract in `HttpAdminPortalGdprClient`: the projection-name adoption replaced the hand-written PascalCase `"PartyDetail"` `projectionType` (which the EventStore query gateway rejects with HTTP 400) with the canonical `PartyProjectionNames.Detail` (`"party-detail"`). This is a documented bug correction under AC2 — the prior value broke GDPR `GetParty`, `ExportPartyData`, `GetProcessingRecords`, `GetErasureStatus`, and `GetErasureCertificate` routing. Added regression coverage in `EventStoreGatewayRoutingTests` that pins the legacy PascalCase value to a 400 before query routing, and updated `AdminPortalGdprOperationContractTests` to assert the canonical wire value.

## File List

- `_bmad-output/implementation-artifacts/6-4-projection-names-and-actor-id-builders-a4.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/architecture.md`
- `docs/component-inventory.md`
- `docs/index.md`
- `src/Hexalith.Parties.Contracts/PartyActorIds.cs`
- `src/Hexalith.Parties.Contracts/PartyProjectionNames.cs`
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`
- `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`
- `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs`
- `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs`
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`
- `src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs`
- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`
- `src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs`
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt`
- `tests/Hexalith.Parties.Contracts.Tests/PartyProjectionAnchorTests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`
- `tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs`
- `tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesProjectionSubscriptionTests.cs`

## Change Log

- 2026-06-29: Added canonical projection name and actor id builders; adopted them across production call sites.
- 2026-06-29: Fixed index rebuild state key compatibility with the live index projection actor and documented the resolved mismatch.
- 2026-06-29: Added/updated contract, rebuild, client, gateway, projection, and UI tests for canonical projection anchors.
- 2026-06-29: Senior Developer Review (AI) — approved. Auto-fixed review findings: completed the File List (added `HttpAdminPortalGdprClient.cs`, `AdminPortalGdprOperationContractTests.cs`, `EventStoreGatewayRoutingTests.cs`), documented the GDPR PascalCase→canonical `projectionType` bug correction under AC2, and sorted the `PartyProjectionUpdateOrchestrator` using directives.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-29 · **Outcome:** Approve (auto-fixed)

**Scope verified:** Source projects (Contracts, Parties, Projections, Client) and test projects build clean (`-m:1`, 0 warnings / 0 errors); full `Hexalith.Parties.Tests` = 496 passed / 0 failed; targeted suites pass — `PartyProjectionAnchorTests` (11), `ProjectionRebuildServiceTests` (13), `EventStoreGatewayRoutingTests` (52), `AdminPortalGdprOperationContractTests` (17), `HttpPartiesQueryClientTests` (25), full Contracts incl. public-API snapshot (117), changed UI tests (17).

**Acceptance Criteria:** AC1 ✅ `PartyProjectionNames` + `PartyActorIds` exposed and tested · AC2 ✅ detail/index actor-id formats preserved (one documented correction: GDPR client `projectionType`) · AC3 ✅ rebuild index state key now equals the live actor key `{tenant}:party-index:default` with regression coverage · AC4 ✅ Contracts stays BCL-only (verified by standalone build; only `System` used) · AC5 ✅ no ad-hoc projection-name/actor-id literals remain in production outside the canonical anchor.

**Findings (all fixed, 0 CRITICAL):**
1. MEDIUM — File List omitted `HttpAdminPortalGdprClient.cs`, `AdminPortalGdprOperationContractTests.cs`, and `EventStoreGatewayRoutingTests.cs`. Fixed.
2. MEDIUM — Undocumented behavioral correction: GDPR detail queries previously sent PascalCase `"PartyDetail"`, which the query gateway rejects (HTTP 400); adoption of `PartyProjectionNames.Detail` fixed the latent routing bug. Documented in Completion Notes + Change Log.
3. LOW — `PartyProjectionUpdateOrchestrator` using directives out of alphabetical order. Fixed.
