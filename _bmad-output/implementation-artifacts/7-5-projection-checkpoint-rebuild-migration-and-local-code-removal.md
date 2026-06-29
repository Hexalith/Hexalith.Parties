---
story_key: 7-5-projection-checkpoint-rebuild-migration-and-local-code-removal
story_id: "7.5"
epic: "7"
created: 2026-06-29T16:41:58+02:00
source_status: backlog
target_status: ready-for-dev
baseline_commit: 2b08e48
---

# Story 7.5: Projection Checkpoint/Rebuild Migration and Local Code Removal

Status: ready-for-dev

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a maintainer,
I want Parties-local projection checkpoint and rebuild code replaced after adapter parity,
so that the EventStore projection platform becomes the single implementation.

## Acceptance Criteria

1. Given Story 7.4 proved projection adapter parity, when this story completes, then EventStore `IProjectionCheckpointTracker`, `IProjectionRebuildCheckpointStore`, and `IProjectionRebuildOrchestrator`-compatible primitives are the authoritative projection checkpoint/rebuild path, with any remaining Parties code reduced to thin compatibility adapters.
2. Given Parties actors currently persist local companion checkpoint keys (`{tenant}:party-detail:{partyId}:last-sequence`, `{tenant}:party-index:{partyId}:last-sequence`, and `{tenant}:rebuild-checkpoint:*`), when local checkpoint/rebuild infrastructure is removed or retired, then replay from sequence zero remains idempotent, duplicate/out-of-order events never corrupt read models, and any state cleanup is proven rollback-safe or explicitly deferred.
3. Given projection reads surface Parties public freshness metadata, when EventStore freshness becomes the production read source, then `ProjectionFreshnessMetadata`, `ProjectionFreshnessStatus`, query payload shape, UI `StatusKind`, stale/degraded last-known fallback, and aria-live behavior remain stable.
4. Given rebuild checkpoints can fail, conflict, or be canceled, when EventStore rebuild state is authoritative, then rebuild resume, cancel, reset/replay, terminal success/failure, active-rebuild detection, and store-unavailable behavior are explicitly defined and covered by tests. Story 7.4's fail-loud-versus-fail-soft observation must be resolved here.
5. Given erased parties must not reappear, when checkpoint/rebuild migration is exercised, then erasure cleanup removes or invalidates obsolete local checkpoint state, index queries still exclude erased parties, detail/export/processing-record flows remain PII-free, and logs/errors do not expose event payloads, actor ids, state keys, party names, key aliases, or decrypted values.
6. Given public traffic enters through the EventStore gateway, when validation runs, then integration or topology evidence verifies command -> publish -> projection -> query through `/api/v1/commands` and `/api/v1/queries` when environment permits; if topology is unavailable, the exact blocker is recorded and focused gateway/projection tests still run.
7. Given Epic 7 remains adapter-first and reversible, when local code is deleted, then rollback is a named DI/config switch, revertable deletion commit, or EventStore submodule pointer rollback, with no data migration unless a rollback script and approval are included.

## Tasks / Subtasks

- [ ] Establish migration scope from Story 7.4 evidence (AC: 1, 2, 4, 7)
  - [ ] Read Story 7.4 completion notes and review observations before editing; carry forward the two handoff items: decide fail-loud vs fail-soft EventStore rebuild checkpoint behavior, and wire `MapFreshness` into production reads.
  - [ ] Confirm Story 7.4 parity evidence is acceptable for deleting or reducing local checkpoint/rebuild code. If evidence is incomplete, add missing tests first and defer deletion that lacks proof.
  - [ ] Keep full replay from sequence zero as the production safety contract. `IProjectionCheckpointTracker.ReadLastDeliveredSequenceAsync` is documented as reserved for future incremental delivery; do not use it to skip replay in this story.
  - [ ] Do not change EventStore gateway routing, DAPR ACLs, public Parties read contracts, search behavior, crypto/key-management, GDPR legal semantics, or consumer self-scope policy.

- [ ] Make EventStore delivery checkpoints authoritative without breaking idempotent replay (AC: 1, 2, 5, 7)
  - [ ] Update `PartyProjectionUpdateOrchestrator` and `IPartyProjectionPlatformAdapter` usage so EventStore delivery checkpoint state is the authoritative shared progress record after both detail and index actors accept each event.
  - [ ] Retire local actor companion checkpoint writes only if tests prove replay-from-zero remains idempotent through handler-level set-based apply and no duplicate event mutates read models. Otherwise keep a thin local adapter and document why deletion is deferred.
  - [ ] Preserve save ordering: never save a delivered sequence after detail succeeds but index fails; cancellation must propagate.
  - [ ] Preserve duplicate-sequence diagnostics and monotonic checkpoint behavior. Same-sequence duplicates must be visible to operations and must not lower EventStore progress.
  - [ ] Remove or invalidate obsolete local companion keys during erasure/rebuild only after rollback evidence proves no recreated party is blocked by stale high-water marks.

- [ ] Replace local rebuild checkpoints with EventStore rebuild state (AC: 1, 2, 4, 5, 7)
  - [ ] Refactor `ProjectionRebuildService` so EventStore `IProjectionRebuildCheckpointStore` is the source of truth for detail/index rebuild checkpoint reads, writes, terminal completion, and active rebuild status.
  - [ ] Decide and implement the authoritative failure policy for EventStore checkpoint save/delete failures. If fail-loud is retained, prove an EventStore outage cannot leave local-only progress that the next run misreads as authoritative. If fail-soft is chosen, prove divergence is bounded, observable, and recoverable.
  - [ ] Use `ProjectionRebuildCheckpointScope` with domain `party`, projection names from `PartyProjectionNames`, tenant, optional aggregate id, and operation id where available. Do not invent a second scope vocabulary.
  - [ ] Evaluate whether `IProjectionRebuildOrchestrator.RebuildProjectionAsync` can replace local stream replay mechanics without losing Parties semantics. If not, keep `ProjectionRebuildService` as a thin facade over EventStore checkpoint/orchestration primitives and explicitly defer full orchestrator replacement.
  - [ ] Preserve rebuild resume from next sequence, cancellation, missing manifest fail-closed behavior, state-store failure behavior, redacted erased-party skip behavior, and `GetProcessingRecordsAsync` as a GDPR read over aggregate events.
  - [ ] Remove `LocalPartyProjectionPlatformAdapter` and `PartyProjectionPlatformAdapterMode.Local` only if rollback no longer depends on them. If retained, narrow them to rollback-only and document the switch.

- [ ] Wire EventStore freshness mapping into production reads (AC: 3, 5)
  - [ ] Update `PartyDetailProjectionActor.GetDetailReadAsync`, `PartyIndexProjectionActor.GetEntriesReadAsync`, or an equivalent read-side adapter so production reads use the projection platform freshness mapper rather than hand-rolled direct `ProjectionFreshnessMetadata.Create(...)` decisions.
  - [ ] Preserve public enum values and serialized shapes. Do not add, remove, rename, or reinterpret `ProjectionFreshnessStatus` values.
  - [ ] Map EventStore `Current` and serviceable `Aging` to Parties `Current`; map `Stale` to Parties `Stale`; map active rebuild to `Rebuilding`; map safe cached state-store failures to existing `Stale` or `Degraded`; map unsafe/missing context to `Unavailable`.
  - [ ] Preserve stale/degraded last-known fallback and fail-closed unavailable reads. Unavailable query results must not leak rows or PII.
  - [ ] Preserve UI behavior indirectly: freshness indicator dot plus word, routine status updates as `role="status" aria-live="polite"`, and no focus steal.

- [ ] Remove duplicate local projection infrastructure only where evidence exists (AC: 1, 2, 5, 7)
  - [ ] Delete local checkpoint/rebuild records, helpers, config flags, DI registrations, and tests that are no longer used only after replacement behavior is tested.
  - [ ] Keep `Hexalith.Parties.Contracts` infrastructure-free. EventStore server references belong in the host/internal projection boundary, not public contracts.
  - [ ] Keep one C# object/type per `.cs` file for any new or reshaped types.
  - [ ] Do not reset or update unrelated submodule pointers. The worktree has pre-existing submodule drift; record blockers rather than "fixing" pointers outside this story.
  - [ ] If EventStore source must change for missing migration observability or rollback hooks, make only additive owner-scoped changes, run the focused EventStore owner lane, and record the pointer/source status.

- [ ] Prove projection, rebuild, freshness, and erasure parity (AC: 1-7)
  - [ ] Update focused unit tests for delivery checkpoint authority, save-after-both-projections, duplicate and out-of-order events, replay from sequence zero, and no checkpoint lowering.
  - [ ] Update rebuild tests for detail/index resume, active rebuild detection, cancel/reset/replay semantics, terminal success/failure rows, state-store failure, missing manifest fail-closed, and fail-loud/fail-soft policy.
  - [ ] Update read/freshness tests for `Current`, `Aging`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, safe cached fallback, unsafe unavailable reads, and no state-key/actor-id leakage.
  - [ ] Update erased-party tests proving index exclusion, detail tombstone/export/processing flows stay PII-free, obsolete checkpoint state is removed or harmless, and recreated parties are not blocked by stale sequence values.
  - [ ] Add or update integration/topology evidence for command -> publish -> projection -> query through EventStore gateway. Existing starting points are `EventStoreGatewayE2ETests`, `EventStoreGatewayRoutingTests`, and `PartiesAspireTopologyFixture`.
  - [ ] Remove or replace static story-evidence tests if they only assert source text. Behavioral xUnit coverage must carry the migration proof.

- [ ] Run validation and record release/rollback evidence (AC: 6, 7)
  - [ ] Run `git diff --check`.
  - [ ] Run `bash scripts/check-no-warning-override.sh`.
  - [ ] Run `dotnet build Hexalith.Parties.slnx -c Release --no-restore` or record the exact unrelated restore/build blocker.
  - [ ] Run focused Parties projection tests, gateway query tests, and projection health/degradation tests listed in "Testing and Validation Guidance".
  - [ ] Run integration/topology evidence when Docker/DAPR/Aspire are available; skip gracefully only with the fixture's explicit unavailable reason.
  - [ ] If EventStore source is touched, run the focused EventStore projection package build/tests individually; do not run solution-level `dotnet test` in EventStore.
  - [ ] Record rollback set: DI/config switch, revertable deletion commit, submodule pointer rollback, and data/contract compatibility statement.
  - [ ] Confirm Epic 7 remains post-MVP platform maintenance and adds no new PRD functional coverage.

## Dev Notes

### Story Classification

- Epic 7 is post-MVP platform maintenance. This story is not MVP feature delivery and must not be reported as new PRD functional coverage. [Source: `_bmad-output/planning-artifacts/epics.md#Epic-7-Platform-Alignment---adopt-CommonsEventStore-Class-B`]
- Story 7.5 covers B1 projection checkpoint/replay dedupe, B2 rebuild/replay-from-zero migration, B9 freshness production wiring, and the projection slice of B11. It must not migrate crypto/key management or change public Parties contracts. [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.5---Projection-CheckpointRebuild-Migration-And-Local-Code-Removal`]
- Adapter-first remains binding, but this is the story allowed to remove local projection checkpoint/rebuild infrastructure after evidence exists. Deletion without parity and rollback evidence must be deferred to Story 7.8 or a follow-up. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-1---Adapter-First-Migration`]

### Required Source Discovery Results

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 7 sequencing and Story 7.5.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md` and `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/parties-ui-prd.md`; Story 7.5 is maintenance and does not add PRD FR coverage.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/index.md`, `EXPERIENCE.md`, and `DESIGN.md`; relevant guardrails are stale/degraded last-known rendering, `StatusKind`, dot-plus-word freshness, aria-live politeness split, no focus steal, FrontComposer, and FluentUI Blazor V5.
- Loaded persistent project context from `_bmad-output/project-context.md` and EventStore project context from `references/Hexalith.EventStore/_bmad-output/project-context.md`.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md`.
- Loaded current Parties projection delivery, actors, rebuild service, query actors, DI registration, EventStore checkpoint/rebuild/freshness APIs, focused test inventory, sprint status, and recent git history.

### Current Files Being Modified - Required Reading

Read each UPDATE file completely before editing it.

- `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs` (UPDATE)
  - Current state: full-replays aggregate events from sequence zero, orders events, unprotects payloads, sends each event to detail then index projection actors, saves the platform delivered sequence after both actors accept, and best-effort indexes latest entries into Memories.
  - What this story changes: make EventStore checkpoint state authoritative while preserving full-replay delivery and save-after-both semantics.
  - Preserve: no public API, cancellation propagation, duplicate sequence diagnostics, redacted post-erasure fallback only for typed destroyed-key errors, best-effort Memories indexing, and no PII/event payload logging.

- `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` (UPDATE)
  - Current state: stores detail under `{tenant}:party-detail:{partyId}`, local companion checkpoint under `{stateKey}:last-sequence`, cached/last-known fallback, and direct `ProjectionFreshnessMetadata.Create(...)` decisions.
  - What this story changes: retire or reduce the companion checkpoint only with parity proof; route production freshness through the projection platform mapper.
  - Preserve: idempotent replay, redacted event checkpoint advance, corrupted checkpoint reset only for deserialization/key-not-found cases, infrastructure failures propagating, erasure cleanup, and metadata-only diagnostics.

- `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs` (UPDATE)
  - Current state: stores tenant index under `{tenant}:party-index:default`, manifest under `{tenant}:party-index:manifest`, per-party checkpoint keys under `{tenant}:party-index:{partyId}:last-sequence`, batched writes, cached fallback, and direct freshness decisions.
  - What this story changes: retire or reduce per-party checkpoint keys only with replay/idempotency proof; route production freshness through the projection platform mapper.
  - Preserve: `SingleKeyPartitionStrategy`, manifest writes only through persisted state, per-party O(1) persistence unless deliberately replaced, malformed actor id fail-closed behavior, no cross-tenant cache leakage, and erased-party removal.

- `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs` (UPDATE)
  - Current state: rebuilds detail and index read models by reading DAPR actor state directly, enumerates party ids from index/manifest, routes checkpoint read/write/delete through `IPartyProjectionPlatformAdapter`, and still serves `GetProcessingRecordsAsync`.
  - What this story changes: make EventStore rebuild checkpoint/orchestration primitives authoritative and remove duplicate local checkpoint storage where proven.
  - Preserve: resume from next sequence, cancellation, missing manifest fail-closed behavior, state-store failure propagation, decryption-failure skip behavior for erased parties, and processing-record reads.

- `src/Hexalith.Parties.Projections/Services/IPartyProjectionPlatformAdapter.cs` (UPDATE)
  - Current state: Parties compatibility boundary for delivered sequence, rebuild checkpoint, active rebuild, and freshness mapping.
  - What this story changes: narrow this boundary to thin compatibility over EventStore primitives or remove local-only members when callers migrate.
  - Preserve: public contracts remain outside this internal adapter; do not leak EventStore Server dependencies to `Contracts`.

- `src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs` (UPDATE)
  - Current state: wraps EventStore checkpoint tracker and rebuild checkpoint store; rebuild read falls back to local, save/delete write local plus EventStore, and save/delete failures currently throw.
  - What this story changes: remove local fallback as authoritative state or make it rollback-only; resolve fail-loud vs fail-soft policy for EventStore checkpoint write failures.
  - Preserve: scope mapping to tenant/domain/projection/aggregate/operation, active rebuild detection, freshness mapping, and bounded logs.

- `src/Hexalith.Parties.Projections/Services/LocalPartyProjectionPlatformAdapter.cs` (DELETE or UPDATE)
  - Current state: rollback adapter that reads/writes old local DAPR actor state keys and mirrors freshness mapping.
  - What this story changes: delete only if rollback no longer depends on it; otherwise mark/narrow as rollback-only and remove it from the default path.
  - Preserve: existing local state key knowledge if it is still needed for rollback or migration cleanup.

- `src/Hexalith.Parties.Projections/Configuration/ProjectionOptions.cs` and `PartyProjectionPlatformAdapterMode.cs` (UPDATE or DELETE)
  - Current state: projection batch settings plus `PlatformAdapterMode` defaulting to `EventStore` with `Local` rollback mode.
  - What this story changes: remove or narrow `Local` only after rollback is replaced by a named revert/pointer path.
  - Preserve: validation for `BatchSize > 0` and `BatchTimeWindowMs > 0`.

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (UPDATE)
  - Current state: registers projection actors, `LocalPartyProjectionPlatformAdapter`, `EventStorePartyProjectionPlatformAdapter`, `IPartyProjectionPlatformAdapter`, and `ProjectionRebuildService`.
  - What this story changes: default to EventStore authoritative services and delete/reduce local rollback registration only with rollback evidence.
  - Preserve: actor registrations, `IProjectionUpdateOrchestrator`/`IProjectionPollerDeliveryGateway` shared instance pattern, and no request-path tenant-access regression.

- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs` and `PartyIndexProjectionQueryActor.cs` (UPDATE if read/freshness integration requires it)
  - Current state: query actors route EventStore gateway query envelopes to projection actors, fail closed on unavailable reads, and serialize existing Parties read payload shapes.
  - What this story changes: only as needed to pass through platform freshness or topology evidence.
  - Preserve: route validation, tenant id allowlist, public query payload shapes, `GetProcessingRecords`, `ExportPartyData`, erasure status/certificate behavior, and no actor id leakage.

### EventStore API Facts

- `IProjectionCheckpointTracker` supports `ReadLastDeliveredSequenceAsync`, `SaveDeliveredSequenceAsync`, `TrackIdentityAsync`, and `EnumerateTrackedIdentitiesAsync`. Its read method is documented as reserved for future incremental delivery; do not use it to skip full replay in this story. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`]
- `IProjectionRebuildCheckpointStore` supports `ReadAsync`, `SaveAsync`, `ResetAsync`, `HasActiveOperatorRebuildForDomainAsync`, `ListActiveRebuildIndexPairsAsync`, and orphan active-index cleanup. Use this store as the authoritative rebuild checkpoint surface before deleting local checkpoint code. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs`]
- `IProjectionRebuildOrchestrator.RebuildProjectionAsync` requires an initial running checkpoint row before invocation and does not re-validate tenant/RBAC per page. Direct adoption must preserve Parties operator assumptions and tenant safety. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionRebuildOrchestrator.cs`]
- `ReadModelFreshnessState` has `Unknown`, `Current`, `Aging`, and `Stale`. Parties must map this to existing `ProjectionFreshnessStatus` values rather than replacing the public vocabulary. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessState.cs`]

### Previous Story Intelligence

- Story 7.4 added the adapter boundary, EventStore-backed adapter, local rollback adapter, delivery checkpoint save-after-both behavior, rebuild checkpoint routing, freshness mapper, and focused tests. It intentionally did not delete local projection infrastructure. [Source: `_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md#Completion-Notes-List`]
- Story 7.4 review approved the work but recorded two 7.5 decisions: EventStore rebuild checkpoint save/delete currently fail loud while reads fall back to local, and `MapFreshness` is unit-tested but not wired into production reads. [Source: `_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md#Senior-Developer-Review-AI`]
- Story 7.4 build caveat remains relevant: main Parties restore/build was blocked by unrelated Tenants submodule drift requiring `Hexalith.Commons.UniqueIds >= 3.19.0`; do not reset submodule pointers in this story. [Source: `_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md#Verification-caveat-carried-from-dev-re-confirmed`]
- Recent commits are `feat: complete story 7.4 projection platform adapter`, `feat: complete story 7.3 search normalization`, `feat(story-7.2): Commons ServiceDefaults, correlation, ProblemDetails, and paging`, and `feat(story-7.1): Platform target-destination ADR and release/rollback plan`. [Source: `git log -5`]

### Project Structure Notes

- Projection implementation belongs in `src/Hexalith.Parties.Projections/` and host/EventStore adapter wiring belongs in `src/Hexalith.Parties/`.
- Public models and client shapes remain in `Hexalith.Parties.Contracts` / `Hexalith.Parties.Client`; avoid changing them for this migration.
- EventStore Server project references must stay internal to host/projection implementation. Do not add EventStore Server dependencies to `Contracts`, UI RCLs, or adopter-facing packages.
- Submodule rules remain binding: root-declared submodules only, no recursive update, and no unowned submodule edits.

### Testing and Validation Guidance

Run the smallest reliable lane first, then broaden:

- `git diff --check`
- `bash scripts/check-no-warning-override.sh`
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore` or exact blocker
- Focused Parties tests:
  - `tests/Hexalith.Parties.Tests/Projections/ProjectionPlatformAdapterTests.cs`
  - `tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs`
  - `tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildAndHealthHardeningTests.cs`
  - `tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs`
  - `tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs`
  - `tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs`
  - `tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs`
  - `tests/Hexalith.Parties.Tests/Gateway/TenantSafeProjectionReadGuardrailsTests.cs`
  - `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`
  - `tests/Hexalith.Parties.Tests/HealthChecks/ProjectionFreshnessAndDegradationTests.cs`
  - `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs`
  - `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyIndexProjectionHandlerTests.cs`
- Integration/topology evidence when available:
  - `tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`
  - `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs`
- xUnit v3 filtering: do not rely on classic VSTest `--filter`; use test executable single-dash xUnit v3 args when filtering is required.
- If EventStore source changes, follow EventStore rules: run test projects individually, do not run solution-level `dotnet test`, and use `ConfigureAwait(false)` on awaited calls.

### Rollback Plan

- Preferred rollback: revert this story's deletion commit while leaving Story 7.4 adapter parity in place.
- DI/config rollback: if retained, set `Parties:Projections:PlatformAdapterMode=Local` only as an explicit emergency rollback path, not as the default active path.
- EventStore rollback: roll back the `references/Hexalith.EventStore` pointer or additive EventStore projection API changes if this story touches the submodule.
- Data rollback: no irreversible data migration is allowed without an approved rollback script. Obsolete local checkpoint cleanup must be safe because full replay plus EventStore checkpoints can rebuild read models.
- Contract rollback: public Parties contracts, query payloads, UI freshness semantics, EventStore gateway route shape, and DAPR ACLs must remain compatible.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.5-Projection-checkpointrebuild-migration-and-local-code-removal`]
- [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.5---Projection-CheckpointRebuild-Migration-And-Local-Code-Removal`]
- [Source: `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md#Target-Destination-Matrix`]
- [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Story-75`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-2---Projection-Platform-Ownership`]
- [Source: `_bmad-output/project-context.md#Read-side-projections--CQRS`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Components`]
- [Source: `references/Hexalith.EventStore/_bmad-output/project-context.md#Critical-Implementation-Rules`]
- [Source: `_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md#Senior-Developer-Review-AI`]
- [Source: `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs`]
- [Source: `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`]
- [Source: `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`]
- [Source: `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs`]
- [Source: `src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionRebuildOrchestrator.cs`]

## Validation Summary

- Source discovery loaded project context facts, sprint status, canonical epics, PRD, whole architecture, UX spines, Epic 7 architecture spine/memlog, Story 7.1 ADR/release plan, Story 7.4 previous-story intelligence, current Parties projection/rebuild/query/freshness files, EventStore projection checkpoint/rebuild/freshness APIs, focused test inventory, and recent git history.
- Checklist fixes applied before finalizing: pinned Story 7.4 handoff items, clarified EventStore checkpoint authority without incremental delivery, required production freshness wiring, required fail-loud/fail-soft decision evidence, added erasure/no-PII cleanup guardrails, named rollback conditions, and replaced static evidence with behavioral test expectations.
- Latest-technology review found no external dependency upgrade requirement. This story relies on pinned local .NET 10, current root submodule sources, and accepted Epic 7 ADR/release plan rather than changing package versions.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
