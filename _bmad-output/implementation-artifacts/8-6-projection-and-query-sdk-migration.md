---
story_key: 8-6-projection-and-query-sdk-migration
story_id: "8.6"
epic: "8"
created: 2026-07-08T18:23:46+02:00
source_status: backlog
target_status: ready-for-dev
baseline_commit: 2c4a7af
eventstore_pin_at_creation: 0f428d0c914f2151aab15bb262f956a9630041dc
---

# Story 8.6: Projection and query SDK migration

Status: blocked

<!-- Note: This story is ready for dev workflow intake, but production source migration is hard-gated by the Story 8.3 projection/query SDK matrix row. -->

## Story

As a maintainer,
I want projection and query mechanics to use EventStore SDK abstractions,
so that Parties keeps only domain folds, query semantics, and tenant guardrails.

## Acceptance Criteria

1. Given the Story 8.3 matrix row "EventStore projection/query SDK" remains `needs-additive-api`, when this story starts, then source migration halts as blocked until the row records owner-approved additive parity or explicit already-available proof for G3 read-model erasure hooks, G10 index batching, G6 freshness mapping, duplicate/out-of-order replay, full-rebuild verification, cursor scope compatibility, and the current `references/Hexalith.EventStore` pin.
2. Given the prerequisite row is proven, when projection migration is implemented, then `PartyDetailProjectionHandler` and `PartyIndexProjectionHandler` run through EventStore SDK `IDomainProjectionHandler` implementations while preserving replay-from-zero, duplicate/out-of-order idempotency, erased-party behavior, payload redaction handling, and current detail/index read-model shapes.
3. Given SDK read-model writes replace local actor state writes, when detail and index projections persist state, then writes use `IReadModelStore` and `ReadModelWritePolicy`, preserve index batching or an approved equivalent, and exclude erased parties from the index.
4. Given query migration is implemented, when detail, index, search, export, processing-record, erasure-status, and erasure-certificate reads execute, then they run through EventStore SDK `IDomainQueryHandler` paths with the same tenant guardrails, payload validation, freshness metadata, GDPR semantics, and no-leak diagnostics as the current Dapr query actors.
5. Given pagination cursors exist before migration, when index/search pages are requested through the SDK query path, then `IQueryCursorCodec` preserves cursor purpose/scope compatibility or rejects only malformed/expired cursors with bounded existing error semantics.
6. Given full rebuild is available through the SDK path, when a rebuild is executed for detail and index, then rebuilt read models are verified against aggregate replay before any Dapr projection actor, companion sequence key, rebuild service, platform adapter, or query fallback is deleted.
7. Given stale, degraded, rebuilding, or unavailable projection state, when a read is requested, then the response returns last-known data and `ProjectionFreshnessMetadata` exactly as today; staleness never throws through the query contract.
8. Given the SDK path proves parity, when cleanup is performed, then Dapr projection actors, the Parties rebuild service, Epic 7 projection platform adapters, and projection/query `catch (NotImplementedException)` control flow are removed.
9. Given the host boundary from Story 8.5, when this migration completes, then the Parties host still exposes no public API, command/query ingress remains through EventStore gateway and DAPR `/process`, and the DAPR ACL remains deny-by-default with only `eventstore -> POST /process`.
10. Given Epic 8 is post-MVP maintenance, when this story is completed, then documentation and sprint status state this is platform cleanup only and no new PRD functional requirement coverage was delivered.

## Tasks / Subtasks

- [ ] Establish the hard prerequisite gate before editing production source (AC: 1, 9, 10)
  - [ ] Read `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` and confirm the "EventStore projection/query SDK" row is no longer `needs-additive-api`.
  - [x] Record the current `references/Hexalith.EventStore` commit in the matrix before migration; story creation observed `0f428d0c914f2151aab15bb262f956a9630041dc`, which is newer than the Story 8.5 proof pin.
  - [x] If owner proof is not locally available, mark this story blocked in the Dev Agent Record and sprint status; do not edit production source.
  - [x] Preserve root-declared submodule discipline: do not run recursive submodule commands or initialize nested submodules.

- [ ] Build the projection/query parity harness before deleting rollback paths (AC: 2, 3, 4, 5, 6, 7)
  - [ ] Add tests that compare current actor paths and SDK paths for replay-from-zero, duplicate delivery, out-of-order delivery, stale/degraded fallback, erased-party exclusion, cursor compatibility, and processing-record reads.
  - [ ] Include a full rebuild versus aggregate replay verification for both detail and index.
  - [ ] Cover GDPR Art.20 export, Art.30 `ProcessingActivityRecord[]`, erasure status, erasure certificate, and no-PII diagnostics.
  - [ ] Record all parity commands and results in `_bmad-output/implementation-artifacts/tests/test-summary.md`.

- [ ] Rebind projection folds to EventStore SDK abstractions (AC: 2, 3, 6, 7)
  - [ ] Update `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs` and `PartyIndexProjectionHandler.cs` through `IDomainProjectionHandler` implementations or thin adapters that keep the existing pure fold behavior.
  - [ ] Use `IReadModelStore` and `ReadModelWritePolicy` for detail and index writes; preserve set-based idempotency, single-key index semantics, batched index behavior or approved equivalent, and erased-party removal.
  - [ ] Preserve typed protected/redacted payload behavior and fail-closed event-type handling; do not introduce `Type.GetType` or broad type activation.

- [ ] Move query paths to EventStore SDK query handlers (AC: 4, 5, 7, 9)
  - [ ] Replace `PartyDetailProjectionQueryActor` and `PartyIndexProjectionQueryActor` semantics with `IDomainQueryHandler` implementations for `PartyDetail`, `GetParty`, `ExportPartyData`, `GetProcessingRecords`, `GetErasureStatus`, `GetErasureCertificate`, `PartyIndex`, and `PartySearch`.
  - [ ] Preserve tenant route validation, strict JSON payload validation, page/page-size guards, party-type allowlist, ISO timestamp offset requirements, and current malformed request outcomes.
  - [ ] Route pagination through `IQueryCursorCodec` only after cursor purpose/scope compatibility and DAPR key-ring persistence are proven.
  - [ ] Preserve optional Memories indexing/search behavior as best effort; do not make Memories required for local search.

- [ ] Replace host/service registrations without widening ingress (AC: 8, 9)
  - [ ] Update `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` to register SDK projection/query handlers, read-model store usage, write policy, and cursor codec.
  - [ ] Keep existing projection actors, rebuild service, adapters, and health checks registered until parity and rebuild evidence are recorded.
  - [ ] Verify `src/Hexalith.Parties/Program.cs` keeps the Story 8.5 SDK host shape and that DAPR ACL exposure remains `/process` only.

- [ ] Delete local mechanics only after parity is green (AC: 6, 8)
  - [ ] Remove Dapr projection actors and actor interfaces only after the SDK path proves detail/index parity.
  - [ ] Remove `ProjectionRebuildService`, rebuild checkpoint types, projection platform adapters, adapter mode, and local freshness adapter types only after SDK rebuild and rollback evidence is recorded.
  - [ ] Remove projection/query `catch (NotImplementedException)` fallback flow from `PartyDetailProjectionActorExtensions` and `PartyIndexProjectionQueryActor`.
  - [ ] Replace or remove `ProjectionActorsHealthCheck` only with equivalent SDK/read-model health evidence; projection degradation must remain non-readiness-blocking.

- [ ] Validate and close evidence (AC: 6, 8, 10)
  - [ ] Run focused and broad build/test lanes listed in Testing and Validation Guidance.
  - [ ] Run `git diff --check` and `bash scripts/check-no-warning-override.sh`.
  - [ ] Update `_bmad-output/implementation-artifacts/tests/test-summary.md` with parity, rebuild, and rollback evidence.
  - [ ] Move sprint status through workflow states without rewriting unrelated comments or statuses.

## Dev Notes

### Story Classification and Gate

- Epic 8 is Class C post-MVP maintenance. It must not be reported as new PRD functional delivery. [Source: `_bmad-output/planning-artifacts/epics.md#Epic-8-Domain-Focus-Refactoring-and-Platform-Extraction-Class-C`]
- Story 8.6 follows completed stories 8.1 through 8.5 and precedes 8.7 through 8.10 in the deletion-heavy sequence. Each remaining story must satisfy the Epic 8 architecture spine readiness gate before a dev session. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#Epic-8-Domain-Focus-Refactoring-and-Platform-Extraction-Class-C`]
- The Story 8.6 draft spec is explicitly `blocked-prerequisite` and says source migration must halt while the Story 8.3 projection/query SDK row remains `needs-additive-api`. [Source: `_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md#Boundaries--Constraints`]
- The matrix row currently records source surfaces but still requires additive or approved parity proof for read-model erasure hooks, index batching, freshness mapping, duplicate/out-of-order replay, rebuild verification, and cursor scope compatibility. [Source: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#EventStore-projectionquery-SDK`]

### Required Source Discovery Results

- Loaded persistent project rules from `_bmad-output/project-context.md` and referenced submodule project-context files. Critical rules include .NET 10, C# 14, `.slnx` only, Central Package Management, warnings-as-errors, xUnit v3 with Microsoft Testing Platform, Dapr package pin discipline, and root-declared submodule discipline.
- Loaded Epic 8 story requirements from `_bmad-output/planning-artifacts/epics.md`, Story 8.6 draft spec, Epic 8 context, and the Epic 8 architecture spine.
- Loaded previous-story intelligence from Story 8.5. The host already uses `AddEventStoreDomainService` and `UseEventStoreDomainService`; `AddEventStoreProjectionRuntimeCompatibility` stayed because projection/query/rebuild migration is deferred to this story.
- Loaded current projection/query implementation files before story creation: projection handlers, Dapr projection actors, query actors, actor extensions, projection rebuild service, projection platform adapters, health checks, `Program.cs`, and service registrations.
- Inspected target EventStore SDK surfaces in the current submodule pin: `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, and `IQueryCursorCodec`.

### Architecture and Domain Guardrails

- Domain focus is binding: Parties owns party aggregate rules, party projection fold semantics, query shape, tenant guardrails, GDPR semantics, and user-visible freshness; generic projection/query mechanics belong to EventStore SDK after proof. [Source: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md#Invariants--Rules`]
- Gateway boundary is binding: public command/query ingress remains through EventStore gateway with `Domain="party"`; the Parties host is not a public API host. DAPR service invocation stays deny-by-default and scoped to `eventstore -> POST /process`. [Source: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md#Boundary-Decisions`]
- Rollback is binding: no local actor, adapter, rebuild, or fallback code is deleted until a replacement path is proven and the rollback set can be restored by DI/submodule pointer or commit revert. [Source: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md#Story-Readiness-Gate`]
- Read behavior is binding: stale, degraded, rebuilding, unavailable, and local-only freshness states must continue to produce last-known data and `ProjectionFreshnessMetadata`; staleness is not an exception path.
- Erasure behavior is binding: erased parties are removed from the index and detail reads expose only PII-free tombstone semantics; companion sequence or SDK checkpoint state must not cause recreated parties to drop valid events.

### Current Implementation Map

- Projection folds to keep: `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs` and `src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs`.
- Query semantics to keep: `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`, `src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs`, and `src/Hexalith.Parties/Queries/IPartyProjectionQueryActor.cs`.
- Actor mechanics to replace after parity: `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`, `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`, and their interfaces/event resolver.
- Rebuild mechanics to replace after parity: `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs`, `IProjectionRebuildService.cs`, rebuild scope/checkpoint types, and index manifest fallback behavior.
- Adapter mechanics to delete after parity: `IPartyProjectionPlatformAdapter`, `LocalPartyProjectionPlatformAdapter`, `EventStorePartyProjectionPlatformAdapter`, `PartyProjectionPlatformFreshness`, and `PartyProjectionPlatformAdapterMode`.
- Host wiring to update carefully: `PartiesServiceCollectionExtensions.cs`, `Program.cs`, erasure cleanup delegates, projection health checks, and `PartyProjectionUpdateOrchestrator`.

### SDK Surface Notes

- `IDomainProjectionHandler` is a stateless full-replay projection handler surface. If it cannot persist or merge multi-read-model state by itself, use the approved `IReadModelStore` and `ReadModelWritePolicy` path rather than inventing a local platform store. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`]
- `IDomainQueryHandler` is the target for query path execution. Keep `Domain="party"` and existing query type names. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs`]
- `ReadModelWritePolicy` relies on idempotent transforms under optimistic retry. Every transform in this story must be safe under duplicate replay. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs`]
- `IQueryCursorCodec` uses an opaque DataProtection cursor with query type, scope, and position. Story 8.6 must prove cursor purpose stability and DAPR key-ring persistence before replacing cursor assumptions. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`]

### Previous Story Intelligence

- Story 8.5 established the SDK host shape but intentionally retained projection/rebuild compatibility services. Do not remove those services at the start of 8.6; remove them only after this story's parity evidence is green. [Source: `_bmad-output/implementation-artifacts/spec-8-5-eventstore-domain-service-sdk-host-cutover.md#Review-Fix-Plan`]
- `PartyDomainProcessor` was registered for multiple `party` casing variants because the SDK keyed lookup was exact-match. Keep domain casing behavior covered by regression tests when routing projections/queries through SDK handlers.
- SDK `/query`, `/project`, `/replay-state`, and metadata endpoints are in-process host capabilities after 8.5; they are not permission to widen public DAPR service invocation. [Source: `src/Hexalith.Parties/Program.cs`]
- The projection rollback action item remains open until Story 8.6 records parity, processing-record reads, rebuild-vs-replay, and rollback evidence. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#open-actions`]

### Technical Stack and Current External Facts

- Local project rules win over external versions: use .NET SDK `10.0.302`, `net10.0`, C# 14, Dapr packages pinned by the repo, Fluent UI Blazor pinned by the repo, xUnit v3, Shouldly, NSubstitute, and Microsoft Testing Platform.
- Official .NET 10 release notes list SDK `10.0.302` as the July 2026 servicing SDK and .NET 10 LTS support through November 14, 2028; do not upgrade as part of this story. [Source: https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md]
- Dapr official support policy supports the current and previous two minor versions; Dapr 1.18 is current-era, but this repo's Dapr package pin remains authoritative and must not be independently bumped. [Source: https://docs.dapr.io/operations/support/support-release-policy/]
- xUnit v3 supports Microsoft Testing Platform and direct test executable execution; use the repo's direct EXE approach for focused `-class` runs because `dotnet test --filter` can silently run zero tests in this workspace. [Source: https://xunit.net/docs/getting-started/v3/microsoft-testing-platform]

### Project Structure Notes

- Keep source in existing projects; do not create a new platform project in Parties for generic projection/query infrastructure.
- Keep public contracts in contracts/client projects unchanged unless the parity harness proves a compatible additive change is required.
- Do not introduce package `Version=` attributes in `.csproj` files; package versions belong in `Directory.Packages.props`.
- Do not introduce a classic `.sln`; this repo uses `.slnx`.
- Do not edit submodules unless the story explicitly records owner approval and a root gitlink update as story work.

### Testing and Validation Guidance

Use direct xUnit v3 assembly execution for focused tests after building the test projects. Example commands to adapt during implementation:

```bash
git -C references/Hexalith.EventStore rev-parse HEAD
rg -n -F "EventStore projection/query SDK" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal
dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal
dotnet ./tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll
dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartyDetailProjectionQueryActorTests -class Hexalith.Parties.Tests.Gateway.PartyIndexProjectionQueryActorTests -class Hexalith.Parties.Tests.Projections.ProjectionRebuildServiceTests -class Hexalith.Parties.Tests.Projections.ProjectionPlatformAdapterTests -class Hexalith.Parties.Tests.Gateway.TenantSafeProjectionReadGuardrailsTests
pwsh scripts/test.ps1 -Lane unit
pwsh scripts/test.ps1 -Lane topology
bash scripts/check-no-warning-override.sh
git diff --check
```

Add direct `-class` runs for the new SDK parity harness. If topology or Docker-backed checks skip, record the skip as environment-limited evidence, not as a passing release gate.

### Rollback Plan

- Before deletion: rollback is the retained actor/query/rebuild/adapter registration set. If SDK parity fails, keep local projection/query mechanics and leave this story blocked.
- After deletion: rollback is a targeted revert of the migration/deletion commit plus a root `references/Hexalith.EventStore` pointer rollback if the SDK pin caused the issue.
- Data rollback is not allowed. Rebuild verification must prove persisted detail and index read models can be regenerated from aggregate replay without losing erased-party exclusion, processing records, or freshness semantics.
- Public routing rollback must preserve EventStore gateway ingress and DAPR `/process` ACL behavior from Story 8.5.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-8.6-Projection-and-query-SDK-migration`]
- [Source: `_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`]
- [Source: `_bmad-output/implementation-artifacts/epic-8-context.md`]
- [Source: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#EventStore-projectionquery-SDK`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md#Story-Readiness-Gate`]
- [Source: `_bmad-output/implementation-artifacts/spec-8-5-eventstore-domain-service-sdk-host-cutover.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`]
- [Source: `src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs`]
- [Source: `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`]
- [Source: `src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs`]
- [Source: `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs`]
- [Source: `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`]

## Validation Summary

- Source discovery loaded the Epic 8 planning artifacts, Story 8.6 draft spec, Story 8.3 matrix, Epic 8 architecture spine, previous Story 8.5 evidence, current projection/query source files, current sprint status, project-context rules, recent git history, and current EventStore submodule pin.
- Checklist fixes applied before finalizing: made the `needs-additive-api` gate explicit, required matrix proof before source changes, preserved rollback-only projection/query files until parity and rebuild evidence are recorded, required cursor compatibility proof, required full rebuild versus aggregate replay, and scoped the story away from crypto, client/MCP/AppHost/deploy, and UI work.
- Latest technical review found no dependency upgrade requirement. Current official .NET, Dapr, and xUnit/MTP information was checked only to confirm that repo pins and direct xUnit executable guidance remain appropriate.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-09T13:25:25+02:00 - Loaded sprint status and selected requested story `8-6-projection-and-query-sdk-migration` from `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- 2026-07-09T13:25:25+02:00 - Read `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`; the `EventStore projection/query SDK` row still has status `needs-additive-api`.
- 2026-07-09T13:25:25+02:00 - Ran `git -C references/Hexalith.EventStore rev-parse HEAD`; current pin is `0f428d0c914f2151aab15bb262f956a9630041dc`, matching `eventstore_pin_at_creation`.
- 2026-07-09T13:25:25+02:00 - Halted before production source edits per AC1 and the story block-if rule. No submodule update/init command was run.
- 2026-07-16T01:04:55+02:00 - Re-read the complete prerequisite matrix; the `EventStore projection/query SDK` row remains `needs-additive-api` and explicitly records that no Story 1.20 owner-approved `available` decision exists.
- 2026-07-16T01:04:55+02:00 - Re-read the EventStore sprint status and Story 1.20: Story 1.19 remains `review`, Story 1.20 remains `ready-for-dev`, and the required `1-20-owner-approved-parity-closure-proof-packet.md` is absent.
- 2026-07-16T01:04:55+02:00 - Verified the root EventStore gitlink is `82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7` while the pre-existing checkout is `97c335cc5685928166914e6b7725502b8017de8b`; the mismatch is not approved consumption identity proof. Halted without production source edits, tests, or submodule commands.

### Completion Notes List

- Source migration is blocked. The Story 8.3 `EventStore projection/query SDK` matrix row remains `needs-additive-api`, so owner-approved additive parity or explicit already-available proof is not locally recorded for the required G3 read-model erasure hooks, G10 index batching, G6 freshness mapping, duplicate/out-of-order replay, full rebuild verification, and cursor scope compatibility.
- Recorded the current EventStore submodule pin in the Story 8.3 matrix and moved Story 8.6 tracking to `blocked`.
- No production source files were edited and no tests were run because the story requires halting before source migration while the prerequisite row remains unresolved.
- Revalidated the gate on 2026-07-16: the active EventStore closure story has not started, its predecessor is still in review, no owner-approved closure packet exists, and the checked-out EventStore SHA does not match the root gitlink. Story 8.6 therefore remains blocked.

### File List

**Modified**
- `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-07-09 | 0.1 | Blocked Story 8.6 at the prerequisite gate because the Story 8.3 `EventStore projection/query SDK` row remains `needs-additive-api`; recorded the current EventStore pin and preserved all production source rollback paths. | GPT-5 Codex (dev-story) |
| 2026-07-16 | 0.2 | Revalidated the prerequisite gate; Story 1.20 remains unstarted with no owner-approved closure packet, and the EventStore checkout does not match the root gitlink. Preserved all production source rollback paths. | GPT-5 Codex (dev-story) |
