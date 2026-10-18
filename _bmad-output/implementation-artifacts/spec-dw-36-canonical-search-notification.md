---
title: 'DW-36 canonical search notification metadata'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3358e3c71c44af877aa1b775db32b5c4f66c313a'
context: ['{project-root}/AGENTS.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A creation/deactivation/rejection batch notifies search with deactivation initially, but rejection on retry. State-dependent no-ops also cause divergence: reconciliation selects every decoded event instead of the canonical fold's last nonnull application.

**Approach:** Persist bounded event metadata with the canonical entry; reconciliation publishes the current entry with its retained provenance, including on older-delivery retries.

## Boundaries & Constraints

**Always:** Store at most one payload-free descriptor (sequence, event type, timestamp) per active aggregate, atomically through the existing SDK write policy. Preserve existing canonical selection, including applications returning the same entry reference. Retain provenance through checkpoint-only batches; remove it on erasure; preserve unrelated aggregates.

**Never:** Infer provenance from timestamps or replay against final state. Add no event/command wire changes, dependencies, infrastructure, unbounded history, production migrations or deployments. External-search erasure fencing stays separate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Search retry | Mutation followed by rejection or semantic no-op | Same canonical event metadata after serialized read-back; no second index write | Retryable until search converges |
| Equal timestamps / incremental batch | Several events share time; entry predates batch | Preserve actual canonical selection | Existing sequence validation |
| Older retry / checkpoint-only batch | Current entry has newer retained provenance | Reconcile current entry and provenance | Preserve cancellation |
| Historical row | Metadata absent | Synthetic `PartyProjectionReconciled`, entry `LastModifiedAt`; no invented original event | No mandatory rebuild |
| Erasure | Tombstone or SDK cleanup | Remove target descriptor; retain others; notify removal | Preserve permanent erasure fence |
| Rebuild | Serialized shared candidate | Canonical descriptors survive; existing synthetic rebuild notifications remain | Existing rebuild failures |

</frozen-after-approval>

## Code Map

All production paths below are under `src/Hexalith.Parties.Projections/`:

- `Handlers/PartyIndexSdkProjectionHandler.cs`: `FoldCore`, `BuildReconciliationFold`, optimistic callback, serialized rebuild candidate.
- `Handlers/PartyIndexProjectionHandler.cs`: authoritative `Apply`; null means no index effect. Leave its rules unchanged.
- `Handlers/PartyIndexFoldResult.cs`: replace in-memory payload DTO with lightweight descriptor.
- `Models/PartyIndexSdkReadModel.cs`: additive dictionary, default empty for historical rows.
- `Services/PartySdkReadModelEraser.cs`: `RemoveParty` reconstructs the index and must preserve unrelated descriptors.

Reuse SDK store seams and factories in `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Parties.Projections/Models/PartyIndexSdkReadModel.cs` and new sibling `PartyIndexEventMetadata.cs` — add serialization-compatible metadata, one type per file.
- [x] `src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs` and sibling `PartyIndexFoldResult.cs` — persist canonical selection; reconcile without independently selecting request events.
- [x] `src/Hexalith.Parties.Projections/Services/PartySdkReadModelEraser.cs` — remove target provenance and preserve others.
- [x] `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs` — reproduce baseline failure; cover matrix, optimistic retries and persisted rebuild/live equivalence.
- [x] `docs/data-models.md` — document provenance, legacy fallback and older retries.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — close only DW-36 after verification and independent review; retain exact evidence beside this spec under `tests/dw-36-canonical-search-notification-2026-10-18/`.

**Acceptance Criteria:**

- Given persisted state and failed search notification, when a recreated handler retries, then serialized metadata supplies canonical attribution without another index write.
- Given optimistic contention, when a write retries, then notification describes the successful candidate and unrelated metadata survives.
- Given historical rows, when reconciled, then search converges using explicitly synthetic provenance without a required rewrite.

## Implementation Notes

Planning: additive persistence, cleanup and rebuild compatibility make this a dispatch change requiring approval. Proposed behavior above is pending approval. No production edits or tests executed.

Baseline `3358e3c71c44af877aa1b775db32b5c4f66c313a`; initially clean tree. Read repository/state/build/test guidance, architecture and Story 8.6. Independent read-only investigation confirmed divergent selection. Replaying from empty loses incremental state; replaying final state turns prior effects into no-ops.

Isolated no-build Aspire start, Parties readiness, describe and exact-AppHost stop exited 0. Parties healthy; EventStore, Tenants, UI and Admin Finished. Full topology unqualified. Local observations: `/tmp/dw-36-validation/`.


Implementation completed before review: bounded metadata is persisted atomically with entries; reconciliation uses it or the approved historical marker; erasure removes only the target descriptor. Baseline regression fails once on old production code. Initial checks pass 121 focused handler cases, 311 projection cases and 1157 service cases with zero skips/errors; Debug/source builds have zero warnings/errors. Review verification additions are in progress.


Final completion: all eight accepted review findings are patched in tests/prose. Three review layers and verification follow-up are complete; no new deferred work. Parent verification rebuilt both projects from final bytes with zero warnings/errors and ran all 316 projection and 1158 service tests (1474 total) with zero failures/errors/skips/not-run. Named XML outcomes cover every matrix row and all acceptance criteria. CRLF-aware whitespace and warning/nested-submodule gates pass. The pinned commitlint 21.2.2 validates the exact local completion message; evidence is in `tests/dw-36-canonical-search-notification-2026-10-18/validation.json`. DW-36 is closed. Full topology and external-search concurrency fencing remain unqualified.

## Spec Change Log

## Review Triage Log

- B1 — low, rejected: arbitrary long namespace prefixes resolve by suffix and would be retained, but the bounded contract limits descriptor cardinality, not a new wire-name length policy. Normal event production uses fixed CLR type names; a new ingress rejection/truncation policy would add guards for a synthetic, uncommon case and could destroy legitimate source attribution.
- B2 — low, patch: old binaries reconstruct the shared index without the additive dictionary, so mixed-version writes can drop provenance. Document that the existing synthetic fallback handles this and later new-writer applications/rebuild restore descriptors.
- B3 — low, patch: external notifications use a successful-write/read snapshot; a concurrent update may occur afterward. Qualify the new documentation rather than implying external-search linearizability.
- B4 — false: no-rewrite acceptance concerns a delivery already complete at the initial read, covered by stable ETags on recreated-handler retry. The contention case becomes incorporated after preflight and retains the existing SDK update policy; no new unconditional write was added to the initial reconciliation path.
- B5 — low, patch: current persistence doubles use Parties serialization options. Add cross-serialization coverage using the configured production Dapr client's options for both descriptors and historical rows; this does not claim live-sidecar qualification.
- B6 — low, patch: decoded rejection/no-op tests do not exercise null payloads emitted by redaction/corrupt decoding. Add serialized checkpoint-only and retry cases for those advancing paths.
- B7 — low, patch: historical duplicate retries are covered, but transition through checkpoint-only delivery and later real provenance is not. Extend historical-row lifecycle coverage.
- B8 — low, patch: the existing cleanup conflict test changes ETags but has empty index contents. Assert that a reloaded serialized candidate removes target provenance and preserves another aggregate's newly changed descriptor.
- B9 — low, patch: existing new cases use short source names. Verify the fully qualified source name reaches the real search ingestion boundary unchanged.
- B10 — low, rejected: the requested fix edits this build's spec, which the review workflow excludes. The pending-approval statement is a planning-time note; append final implementation/verification notes rather than rewriting historical notes or frozen intent.
- V1 — medium, patch: DateTimeOffset equality compares instants, so existing substituted indexer assertions would miss conversion to UTC before notification. Compose recreated-handler reconciliation with the real indexing pipeline and assert the ingested timestamp's exact round-trip representation.

Follow-up: the verification reviewer independently confirmed V1 and B9 are resolved by exact timestamp/full-name assertions at the real ingestion boundary; no defect was found in the new test.

The edge-case layer returned no findings. B5–B9 and V1 require verification additions; B2–B3 require direct prose corrections. No production algorithm defect was established by review.

## Verification

Build the projection and service test projects individually in Debug with `-m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; require zero warnings/errors. Run built xUnit v3 assemblies directly: focused handler class, full projections, full service suite; require no failures/errors/skips. Record exact commands/counts/identities and limitations. Run `bash scripts/check-no-warning-override.sh` and `git diff --check`. Independent review precedes completion.

Approval recorded 2026-10-18: the user approved and requested implementation to continue. Frozen intent remains unchanged.
