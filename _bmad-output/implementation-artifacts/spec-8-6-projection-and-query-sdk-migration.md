---
title: '8.6 Projection and query SDK migration'
type: 'refactor'
created: '2026-07-07T00:00:00+02:00'
status: 'draft'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-5-eventstore-domain-service-sdk-host-cutover.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
warnings:
  - oversized
  - multiple-goals
  - blocked-prerequisite
---

<intent-contract>

## Intent

**Problem:** Parties still owns generic projection/query mechanics — Dapr projection actors (`PartyDetailProjectionActor`, `PartyIndexProjectionActor`), query actors (`PartyDetailProjectionQueryActor`, `PartyIndexProjectionQueryActor`), a rebuild service, Epic 7 platform adapters, and `catch (NotImplementedException)` remoting control flow — even though the EventStore SDK now exposes `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, and `IQueryCursorCodec`. This makes Parties look like a platform host and blocks Epic 8 domain-focus cleanup.

**Approach:** Move the projection folds and query paths onto the EventStore SDK abstractions so Parties keeps only domain folds, query semantics, and tenant guardrails, then delete the Dapr actors, companion sequence keys, rebuild service, platform adapters, and `NotImplementedException` control flow — but only after a parity harness proves duplicate/out-of-order delivery, replay-from-zero, stale/degraded fallback, erased-party exclusion, GDPR processing-record reads, cursor scope stability, and rollback safety, and a full projection rebuild is executed and verified against aggregate replay.

## Boundaries & Constraints

**Always:** Preserve spine invariants I9 (replay-from-zero on every delivery, per-actor sequence checkpoints, set-based idempotency, duplicate/out-of-order tolerance) and I10 (stale/degraded reads render last-known and never throw; `ProjectionFreshnessMetadata` on every read; erased parties excluded from the index; full rebuild verified against aggregate replay before any local deletion). Keep the two read models' identity/semantics — `PartyDetailProjectionActor` id `{tenant}:party-detail:{partyId}`, `PartyIndexProjectionActor` id `{tenant}:party-index` with batched writes. Preserve GDPR processing-record reads and Art.20/Art.30 read behavior (I8), the `Domain="party"` gateway boundary and deny-by-default ACL (I1), tenant-access checks projection-side only (never on the gateway path), and the SDK host shape landed in Story 8.5 (I2). Keep every local rollback path until parity is proven (I3).

**Block If:** The 8.3 matrix row **"EventStore projection/query SDK" is `needs-additive-api`** — start no source migration until that row records owner-approved additive parity (or explicit "already available" proof) for G3 read-model erasure hooks, G10 index batching, G6 Parties freshness mapping, duplicate/out-of-order replay, full-rebuild verification, and cursor scope compatibility, plus a `references/Hexalith.EventStore` submodule-pin proof, all written into the matrix row **before** implementation. Also HALT if: the SDK cannot express batched index writes without losing set-based idempotency; `IReadModelStore`/`ReadModelWritePolicy` cannot exclude erased parties from the index; `IQueryCursorCodec` changes an existing pagination cursor's scope/encoding such that in-flight cursors break; or a full rebuild cannot be verified against aggregate replay. (These are the "Block If" halts the SDK migration must not paper over.)

**Block If — available-row identity:** HALT before consuming the `available` EventStore DataProtection/cursor row if the Story 8.3 matrix does not record a release or root-declared submodule gitlink matching the EventStore identity selected by Story 8.6. A different selected identity must be written to and revalidated in the row before registration or cursor migration. This is an availability-identity gate, not an additive-API request.

**Never:** Do not migrate crypto/DataProtection payload protection (Story 8.7), client/MCP/AppHost/build/deploy plumbing (Story 8.8), or UI freshness/optimistic-reconcile/status/picker primitives (Story 8.9). Do not delete any Dapr actor, sequence-checkpoint companion key, rebuild service, platform adapter, or `NotImplementedException` branch **before** its replacement has recorded parity evidence and proven rollback. Do not add a public API to the host or widen the DAPR ACL beyond `eventstore -> POST /process`. Do not change public command/query behavior, the `PartyDetail`/index read shapes, or GDPR legal semantics. Do not weaken warnings-as-errors, `.slnx`, CPM, or xUnit v3 discipline.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Replay-from-zero | Projection delivery for a party with existing events | SDK `IDomainProjectionHandler` folds from sequence zero, applies set-based, matches the pre-migration read model exactly | Checkpoint mismatch surfaces as rebuild-required, never a partial write |
| Duplicate / out-of-order delivery | Same event delivered twice / events arriving out of order | Idempotent no-op via sequence checkpoint; final state identical to in-order single delivery | No double-apply; no exception to the caller |
| Stale / degraded read | Read while projection is behind or the store is degraded | Returns last-known data with `ProjectionFreshnessMetadata` (`Stale`/`Degraded`/`Rebuilding`) via `IDomainQueryHandler` | Never throws on staleness; falls back to last-known cache |
| Erased-party exclusion | `PartyErased` folded into the index | Erased party removed from `party-index` read model; detail read returns PII-free tombstone | Index write policy excludes erased id; no PII in logs/metadata |
| Paginated index query | Index query with a page cursor minted before migration | `IQueryCursorCodec` decodes existing cursor scope and returns the same page window | Malformed/expired cursor → bounded rejection, not a leak or crash |
| Processing records (Art.30) | GDPR processing-record read on the query path | Returns `ProcessingActivityRecord[]` with identical semantics to pre-migration | Missing record → existing no-op/empty semantics |
| Full rebuild verification | Rebuild executed after handler migration | Rebuilt read models byte-for-behavior match aggregate replay for detail and index | Divergence blocks local-code deletion (Block If) |

</intent-contract>

## Code Map

Target SDK surfaces (owner: Hexalith.EventStore — 8.3 row `needs-additive-api`, must be proven first):
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs` -- projection fold target.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs` -- query path target.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`, `.../Projections/ReadModelWritePolicy.cs` -- read-model write + erasure/batching policy target.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs` -- pagination cursor target (8.3 DataProtection row: validate cursor-purpose stability + DAPR key-ring persistence).

Parties folds/semantics to REBIND onto the SDK (keep behavior):
- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`, `Handlers/PartyIndexProjectionHandler.cs` -- domain folds → `IDomainProjectionHandler` implementations.
- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`, `Queries/PartyIndexProjectionQueryActor.cs`, `Queries/IPartyProjectionQueryActor.cs` -- query semantics → `IDomainQueryHandler`; **`PartyIndexProjectionQueryActor` carries `catch (NotImplementedException)` remoting control flow to remove after parity.**
- `src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs`, `Abstractions/IIndexPartitionStrategy.cs` -- index partitioning semantics to preserve or fold into `ReadModelWritePolicy`.

Parties platform MECHANICS to DELETE only after parity (I3 rollback set):
- `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`, `Actors/PartyIndexProjectionActor.cs`, `Actors/PartyEventTypeResolver.cs` -- Dapr projection actors.
- `src/Hexalith.Parties.Projections/Services/IProjectionRebuildService.cs`, `Services/ProjectionRebuildService.cs`, `Services/PartyProjectionRebuildScope.cs`, `Services/PartyProjectionRebuildCheckpoint.cs` -- rebuild service + checkpoints.
- `src/Hexalith.Parties.Projections/Services/IPartyProjectionPlatformAdapter.cs`, `Services/LocalPartyProjectionPlatformAdapter.cs`, `Services/PartyProjectionPlatformFreshness.cs`, `Configuration/PartyProjectionPlatformAdapterMode.cs` -- Epic 7 compat adapters.
- `src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs`, `Domain/PartyProjectionUpdateOrchestrator.cs`, `Extensions/PartyDetailProjectionActorExtensions.cs` (**`catch (NotImplementedException)`**), `HealthChecks/ProjectionActorsHealthCheck.cs` -- host-side projection wiring to retire/replace.

Evidence + wiring:
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` -- swap actor registrations for SDK handler/store registrations behind parity.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- record projection/query SDK + DataProtection(cursor) parity + pin proof BEFORE migration.
- `_bmad-output/implementation-artifacts/tests/test-summary.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` -- record commands/results; move 8.6 status only after validation.

## Tasks & Acceptance

**Execution:** (all gated by the Block-If prerequisite — do not start until the 8.3 row is proven)
- [ ] Before source changes, verify `git ls-tree HEAD references/Hexalith.EventStore` and the checkout (or the released package version), confirm the DataProtection/cursor symbols exist at that exact identity, and update the existing 8.3 DataProtection row if the selected identity differs from its recorded value.
- [ ] `story-8-3-platform-api-prerequisite-matrix.md` -- record owner-approved additive parity + `references/Hexalith.EventStore` pin proof for the projection/query SDK and cursor-codec rows (G3/G6/G10 + duplicate/out-of-order + rebuild + cursor scope) -- satisfies the prerequisite gate before any source change.
- [ ] `tests/Hexalith.Parties.Tests/` (new parity harness) -- prove replay-from-zero, duplicate/out-of-order idempotency, stale/degraded last-known fallback, erased-party exclusion, processing-record reads, and cursor decode against BOTH the local actors and the SDK path -- the parity evidence that unlocks deletion.
- [ ] `PartyDetailProjectionHandler.cs`, `PartyIndexProjectionHandler.cs` -- implement `IDomainProjectionHandler`, preserving fold semantics, batched index writes, and erased-party exclusion via `IReadModelStore` + `ReadModelWritePolicy`.
- [ ] `PartyDetailProjectionQueryActor.cs`, `PartyIndexProjectionQueryActor.cs` -- reimplement query semantics as `IDomainQueryHandler`, moving pagination to `IQueryCursorCodec`; remove the `catch (NotImplementedException)` branches.
- [ ] `PartiesServiceCollectionExtensions.cs` -- register SDK handlers/store/cursor codec; keep actor registrations until parity, then remove.
- [ ] Delete the actor/rebuild/adapter/`NotImplementedException` files listed in the Code Map -- ONLY after the parity harness is green and a full rebuild is verified.
- [ ] Execute + verify a full projection rebuild against aggregate replay for detail and index -- record the command and diff evidence.

**Acceptance Criteria:**
- Given the projection/query SDK 8.3 row is still `needs-additive-api`, when 8.6 is attempted, then implementation HALTS `blocked` with that prerequisite as the blocking condition.
- Given the EventStore DataProtection row is `available`, when its recorded release/root gitlink is missing or differs from the EventStore identity selected by 8.6, then implementation HALTS before DataProtection registration or cursor migration; no additive API is requested for this identity mismatch.
- Given proven parity, when the projection/query paths run on the SDK, then detail and index read models, freshness metadata, erased-party exclusion, and processing-record reads are behaviorally identical to pre-migration.
- Given duplicate or out-of-order delivery, when folded through `IDomainProjectionHandler`, then final state matches single in-order delivery (idempotent).
- Given a full rebuild, when compared to aggregate replay, then detail and index match exactly; any divergence blocks deletion of local projection/query/rebuild code.
- Given the migration completes, when the tree is inspected, then no `catch (NotImplementedException)` projection/query control flow, Dapr projection actors, or Epic 7 platform adapters remain, and the DAPR ACL is unchanged.

## Design Notes

- **§4 readiness-gate mapping (spine authority).** (1) Prerequisites: 8.3 rows "EventStore projection/query SDK" (`needs-additive-api`) and "EventStore DataProtection"/cursor (`available`, with the selected release/root gitlink recorded and matched before consumption, plus 8.6 cursor-purpose + DAPR key-ring proof); predecessors 8.3 and 8.5 done. (2) Touched repos: `Hexalith.Parties` + `Hexalith.EventStore` (submodule-pin recorded, root submodules only). (3) Rollback path: every Code-Map "DELETE only after parity" file stays until the parity harness is green and rebuild is verified; revert = restore actor registrations. (4) Validation lanes + parity evidence: below. (5) Non-goals: crypto/DataProtection engine (8.7), client/MCP/AppHost/deploy (8.8), UI (8.9). (6) Parity-evidence checklist: I9 (replay-from-zero, dup/out-of-order, checkpoints), I10 (stale/degraded last-known, freshness metadata, erased-party exclusion, rebuild-verified), I8 (processing records, no-leak diagnostics).
- **Broad-story handling (gate requirement).** This story is hard-gated (Block-If prerequisite + parity-before-delete) AND internally sequenced: parity harness → handlers/store → query/cursor → rebuild verify → delete locals. If the owner prefers, it may be split into 8.6a (projection folds + read-model store) and 8.6b (query + cursor + rebuild + deletions) at dev-auto planning time; keep the same invariants and gate.

## Verification

**Commands:** (run the test EXE directly — `dotnet test --filter` runs zero tests under MTP; pin `-p:MinVerVersionOverride=1.0.0` on rebuilt projects; use `-m:1` for a clean build verdict)
- `dotnet build src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj -c Release -m:1` -- expected: green, warnings-as-errors clean.
- `pwsh scripts/test.ps1 -Lane unit` then the `Hexalith.Parties.Tests` / `Hexalith.Parties.Projections.Tests` EXEs directly with `-class` for the new parity harness -- expected: parity + idempotency + freshness + erased-exclusion + processing-record + cursor tests pass.
- `pwsh scripts/test.ps1 -Lane topology` -- expected: AppHost topology + DAPR ACL unchanged (skips gracefully without Docker; a skip is not a pass).
- Full rebuild command (to be defined against the SDK rebuild path) -- expected: detail + index match aggregate replay with recorded diff evidence.

**Manual checks:**
- Confirm no `catch (NotImplementedException)` remains in `PartyDetailProjectionActorExtensions.cs` / `PartyIndexProjectionQueryActor.cs` and no Dapr projection actor or Epic 7 adapter files remain after deletion.
