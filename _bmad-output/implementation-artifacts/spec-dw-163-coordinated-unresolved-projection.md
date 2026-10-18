---
title: 'DW-163 coordinated unresolved projection recovery'
type: 'bugfix'
created: '2026-10-17'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '6b0641829b68d22656fb173eec6578c65e8e91f0'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-state-instructions.md'
  - '{project-root}/docs/development-guide.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An unresolved first projection delivery creates only the processing-activity slot. Every later retry then fails the coordinated-slot guard with `projection-rebuild-required`, even after the event becomes resolvable.

**Approach:** Initialize both absent slots through the existing EventStore batch store: an empty, unadvanced detail placeholder and the existing failed processing audit. Keep audit initialization separate from the normal dispatch batch identity, with bounded deterministic scope hashing and create-only concurrency. Retain the existing optimistic audit updates when both slots already exist, the genuine missing-slot rebuild guard, and normal projection/rebuild behavior. Prove persisted slot visibility, repeated unresolved delivery, later successful recovery, interrupted initialization and competing writes. Complete only DW-163.

**Decision (2026-10-18):** Adopt EventStore 3.125.1 through the shared Builds catalog and reconcile Parties dependency receipts. The user selected this dependency-adoption scope. Git recording, package publication and deployment remain outside this authorization.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs`: `ProjectAsync` checks coordinated presence before the unresolved audit; `RecordUnresolvedProcessingAsync` currently creates Processing alone. Retain the missing-slot guard and minimum checkpoint admission.
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`: adjust the two absent-slot audit cases; retain seeded-slot, concurrency-exhaustion, and genuine missing-slot cases. Its private fake cannot validate SDK recovery.
- `tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs`: new persisted-state regressions using the existing EventStore.Testing dependency and real batch protocol.
- EventStore `ReadModelBatch.RetainManifestForRecovery` and `IReadModelPendingBatchRecoveryStore.RecoverPendingAsync` supply recovery; use both participating keys before reading state. The pinned 3.124.0 lacks these APIs; cached 3.125.1 exposes them.

## Tasks & Acceptance

**Execution:**
- [ ] Update the owning Builds `Props/Directory.Packages.props` to select EventStore 3.125.1, then align Parties `docs/architecture.md`, `docs/ci.md`, `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` current observations and `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` expectations with actual selections. Preserve historical approvals, exact identity checks, and the no-override gate. Leave Git recording/publication to an explicitly authorized action.
- [ ] In the handler, validate the incoming SDK scope before hashing; recover both pending keys and reload before interpreting absence. Use a bounded aggregate-specific, event/dispatch-independent initialization identity and two empty create-only slots with retained manifests; this also permits retry after a prepared marker with no installed envelope. Record the event-dependent failed audit afterward through the existing optimistic policy. Recovery failure remains retryable; never synthesize missing historical rows or implement SDK plumbing locally.
- [ ] Update the projection test file and add the service recovery class above: repeated unresolved delivery, supported recovery with same/changed dispatch IDs, marker preparation before installation, installation/compaction interruption, second-operation contention/compensation, empty-ETag recovery, maximum/oversized dispatch identities, and newer erasure at BeforeReceipt. Assert serialized persisted contents/checkpoints and fresh ETags.
- [ ] Update only DW-163 in `deferred-work.md` after verification; preserve the earlier reproduction as legacy-mode evidence.

**Acceptance Criteria:**
- Given absent slots, when an unresolved delivery retries, then both slots exist, failed processing is audited, and Detail remains unadvanced.
- Given interrupted initialization, when support or dispatch identity changes, then recovery preserves the original manifest and a supported delivery folds both slots successfully.
- Given a competing sequence-5 erasure, when initialization reconciles, then both newer tombstones survive.
- Given a genuinely missing historical slot, when a live tail arrives, then rebuild remains required.

## Implementation Notes

Replanned 2026-10-18. Intent gaps: none; the user authorized shared-catalog adoption of EventStore 3.125.1. Irreversibles: none in local planning/implementation; package publication, deployment and Git recording are excluded. Footprint includes the dependency prerequisite, handler, tests and accurate dependency documentation, so dispatch replaces oneshot. No production candidate is retained yet. The user approved and continued at the implementation checkpoint on 2026-10-18.

Current-source SDK Debug build: zero warnings/errors; 31 focused recovery tests passed. A standalone 3.125.1 package probe reproduced the legacy erasure overwrite, preserved both newer tombstones with retained manifests, and recovered an original pending batch under a changed dispatch ID. These establish SDK prerequisites, not Parties acceptance. Evidence: `tests/dw-163-resume-2026-10-18/README.md`. Previous draft and historical implementation notes are preserved verbatim beside it in `previous-draft.md`.

## Review Triage Log

- Medium, verified: a prepared event-dependent audit batch cannot resume when the event becomes supported or the dispatch ID changes. A fixed empty initialization manifest would address that part, but was not retained because of the separate erasure race.
- Medium, verified: committed audit envelopes with empty ETags need compaction through their original batch identity before single-key audit updates can proceed. No candidate initialization path remains.
- Low, verified in withdrawn candidate: scope hashing before validation accepted oversized dispatch IDs on the unresolved branch. Withdrawal removes the new inconsistency; the existing supported path retains SDK validation.
- High, verified platform blocker: `BeforeReceipt` fault injection after competing tombstones demonstrates reconciliation overwriting the newer erased state. The required recovery guarantee belongs to EventStore and must not be reimplemented in Parties.
- Low, verified verification gap in withdrawn candidate: the test did not contend on the second operation after the first installed. Compensation/interrupted compaction and direct supported recovery need coverage when the platform guarantee permits implementation.

Independent follow-up: the earlier statement that initially absent slots imply stable manifests was incomplete. Resolution changes, changed dispatch IDs, and interrupted compaction invalidate that design. The portable reproducer preserves the rejected candidate solely to reproduce the separately verified SDK tombstone overwrite.

## Verification

Build each affected test project with `--configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; invoke its built DLL with `-class` first, then run both full affected assemblies. Require nonzero discovery and zero failures. Run `bash scripts/check-no-warning-override.sh`. Before production edits, capture the Aspire baseline and stop the owned instance before builds. Validate the adopted package graph separately; source evidence cannot close package adoption.
