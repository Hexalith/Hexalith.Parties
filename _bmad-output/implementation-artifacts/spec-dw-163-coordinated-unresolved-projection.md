---
title: 'DW-163 coordinated unresolved projection recovery'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'dispatch'
baseline_commit: '6b0641829b68d22656fb173eec6578c65e8e91f0'
review_loop_iteration: 1
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

Current outcome: the review-loop-1 implementation is verified and ready for another independent review. Earlier implementation-audit notes describe the rejected first candidate; the updated tasks and Spec Change Log govern. This handoff is already inside the parent rendered bmad-build workflow; the parent owns review orchestration.

## Code Map

- `src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs`: `ProjectAsync` checks coordinated presence before the unresolved audit; `RecordUnresolvedProcessingAsync` currently creates Processing alone. Retain the missing-slot guard and minimum checkpoint admission.
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`: adjust the two absent-slot audit cases; retain seeded-slot, concurrency-exhaustion, and genuine missing-slot cases. Its private fake cannot validate SDK recovery.
- `tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs`: new persisted-state regressions using the existing EventStore.Testing dependency and real batch protocol.
- EventStore `ReadModelBatch.RetainManifestForRecovery` and `IReadModelPendingBatchRecoveryStore.RecoverPendingAsync` supply recovery; use both participating keys before reading state. The pinned 3.124.0 lacks these APIs; cached 3.125.1 exposes them.

## Tasks & Acceptance

**Execution:**
- [x] Update the owning Builds `Props/Directory.Packages.props` to select EventStore 3.125.1, then align Parties `docs/architecture.md`, `docs/ci.md`, `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` current observations and `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` expectations with actual selections. Preserve historical approvals, exact identity checks, and the no-override gate. Leave Git recording/publication to an explicitly authorized action.
- [x] In `src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs`, retain manifests only for two immutable empty initialization slots. Preserve ordinary supported delivery batch scope, payload, concurrency and non-retained behavior; do not initialize a clean supported first delivery. Recovery preflight must leave legacy non-retained normal retries usable, while recovering both initialization keys before interpreting absence. Validate before hashing, keep a bounded event/dispatch-independent initialization identity, and audit unresolved events afterward with the existing optimistic policy. Fail closed on unresolved own recovery; retain the genuine missing-slot guard. No local SDK plumbing or personal-data-bearing retained manifests. A full payload-bearing manifest must never be retained for ordinary batches; preserve that baseline behavior rather than extending SDK recovery beyond empty initialization.
- [x] In both mapped test files, preserve the existing recovery coverage and add actual legacy first-create retry compatibility, no retained personal data after normal pre-install interruption and real SDK erasure, mixed supported-prefix/unresolved redelivery, interrupted compensation, and controlled competing handler deliveries. Assert persisted checkpoints, audit outcomes, ETags and unchanged normal batch manifests. A marker prepared before any initialization envelope may remain as an immutable empty manifest when support changes; supported recovery must complete without retaining event payloads or changing normal batching.
- [x] Update only DW-163 in `deferred-work.md` after verification; preserve the earlier reproduction as legacy-mode evidence.

**Acceptance Criteria:**
- Given absent slots, when an unresolved delivery retries, then both slots exist, failed processing is audited, and Detail remains unadvanced.
- Given interrupted initialization, when support or dispatch identity changes, then recovery preserves the original manifest and a supported delivery folds both slots successfully.
- Given a competing sequence-5 erasure, when initialization reconciles, then both newer tombstones survive.
- Given a genuinely missing historical slot, when a live tail arrives, then rebuild remains required.

## Implementation Notes

Replanned 2026-10-18. Intent gaps: none; the user authorized shared-catalog adoption of EventStore 3.125.1. Irreversibles: none in local planning/implementation; package publication, deployment and Git recording are excluded. Footprint includes the dependency prerequisite, handler, tests and accurate dependency documentation, so dispatch replaces oneshot. No production candidate is retained yet. The user approved and continued at the implementation checkpoint on 2026-10-18.

Current-source SDK Debug build: zero warnings/errors; 31 focused recovery tests passed. A standalone 3.125.1 package probe reproduced the legacy erasure overwrite, preserved both newer tombstones with retained manifests, and recovered an original pending batch under a changed dispatch ID. These establish SDK prerequisites, not Parties acceptance. Evidence: `tests/dw-163-resume-2026-10-18/README.md`. Previous draft and historical implementation notes are preserved verbatim beside it in `previous-draft.md`.

Implementation audit 2026-10-18: all four tasks and all four acceptance criteria are satisfied by the inspected baseline-to-working-tree diff and persisted recovery regressions. Production now initializes two empty slots in a distinct SDK projection namespace, recovers both participating keys, and retains normal-dispatch manifests. The initialization namespace prevents caller dispatch-ID collisions. No local SDK protocol implementation was added.

A concurrent workspace `/pushall` recorded initial edits and advanced Parties to `7e73709992512d944f4950e6efb28c6132f9473c`, Builds to `332b2c144defba546fe0a09626a4a72f2340e22f`, and EventStore source to `d55c6f7c11e9bce6f17d490a84c83f1561979d63`. The original baseline remains unchanged. This workflow performed no Git mutations. Current receipt expectations match the new graph; historical approvals remain unchanged.

Both affected Debug/source builds passed with zero warnings/errors; full Projections 335/335 and service 1219/1219 passed, as did 24/24 recovery cases against actual EventStore 3.125.1 packages. Recorded SHA-256 values match the current handler, tests and catalog. The warning-override and whitespace gates passed. Full topology acceptance was not claimed. Evidence: [implementation verification](tests/dw-163-implementation-2026-10-18/README.md). Independent review is next.

## Spec Change Log

- 2026-10-18, review loop 1: R1/R2/R3 triggered re-derivation. Constrain retained manifests to event-independent empty initialization; preserve the legacy normal-delivery path, and allow supported delivery after a zero-envelope initialization interruption without requiring the empty marker to complete. This avoids terminal namespace deadlocks, upgrade retry regressions, and unerasable personal-data copies. KEEP: shared-catalog 3.125.1 adoption, exact dependency gates/current observations, frozen intent, incoming scope validation, separate bounded initialization namespace, both-key initialization recovery, optimistic failed audits, genuine missing-slot rebuild guard, erasure protection, meaningful persisted/package regressions, and all historical evidence. Preserve concurrent workspace commits and unrelated dependency changes.

Review-loop-1 implementation audit: the inspected diff limits retained manifests to empty initialization, preserves normal delivery scope/payload/concurrency/non-retention, and permits legacy pending normal batches through their original path. All updated tasks and four frozen acceptance criteria are satisfied. The added cases cover mixed prefixes, interrupted compensation, overlapping handlers, actual erasure after normal pre-install interruption, and terminal fallback fixtures. Tested source/catalog hashes match current files.

Final Debug/source builds have zero warnings/errors; Projections 335/335 and service 1230/1230 passed. All 35 persisted recovery cases passed in both source and published-package mode. Parent reran the portable consumer build with its explicit workspace property (zero warnings/errors) and all 35 package cases passed. The first candidate is marked historical. Final evidence: [review-loop-1 verification](tests/dw-163-review-loop-1/README.md). DW-163 remains open until independent review finishes.

## Review Triage Log

- Medium, verified: a prepared event-dependent audit batch cannot resume when the event becomes supported or the dispatch ID changes. A fixed empty initialization manifest would address that part, but was not retained because of the separate erasure race.
- Medium, verified: committed audit envelopes with empty ETags need compaction through their original batch identity before single-key audit updates can proceed. No candidate initialization path remains.
- Low, verified in withdrawn candidate: scope hashing before validation accepted oversized dispatch IDs on the unresolved branch. Withdrawal removes the new inconsistency; the existing supported path retains SDK validation.
- High, verified platform blocker: `BeforeReceipt` fault injection after competing tombstones demonstrates reconciliation overwriting the newer erased state. The required recovery guarantee belongs to EventStore and must not be reimplemented in Parties.
- Low, verified verification gap in withdrawn candidate: the test did not contend on the second operation after the first installed. Compensation/interrupted compaction and direct supported recovery need coverage when the platform guarantee permits implementation.

Independent follow-up: the earlier statement that initially absent slots imply stable manifests was incomplete. Resolution changes, changed dispatch IDs, and interrupted compaction invalidate that design. The portable reproducer preserves the rejected candidate solely to reproduce the separately verified SDK tombstone overwrite.

Review pass 1 (2026-10-18), each finding graded before grouping:

- BH-1 — medium: verified package probes at both terminal cleanup phases leave normal dispatch retrying; normal-scope SDK recovery immediately succeeds. Route bad_spec (R1).
- BH-2 — medium: verified with the actual baseline handler compiled in a temporary consumer: a first-create interruption resumes as Completed before upgrade, but the candidate returns projection-recovery-incomplete. Route bad_spec (R2).
- BH-3 — high: the package-backed probe completes a changed dispatch, runs PartySdkReadModelEraser successfully, and still decodes a private name from an orphaned normal retained manifest. The eraser targets canonical keys, not undiscoverable marker manifests. Route bad_spec (R3).
- BH-4 — low: custom batch implementations can omit recovery, but built-in DaprReadModelStore and the supported SDK fake provide it; no shipping caller lacking the capability was shown. Reject the additional capability guard: unlikely everyday failure and adds a new branch/contract.
- BH-5 — low: the generic retry reason hides a bounded SDK reason. Reject extra logging/metadata complexity for rare corrupt initialization recovery; no required diagnostic contract is lost.
- BH-6 — low: the real-store suite lacks a mixed supported-prefix/unresolved recovery case, although mocked fold coverage exists. Keep a direct regression addition (patch; superseded by re-derivation).
- BH-7 — low: compensation interruption is not exercised by the domain suite. Keep direct persisted-state regressions (patch; superseded by re-derivation).
- BH-8 — low: large personal-data manifests lack the external-manifest-key regression. This path was introduced solely by broad normal-batch retention; eliminating that retention resolves it with R3, without duplicating SDK manifest-limit tests.
- BH-9 — low: real overlapping handlers are not tested. Keep a deterministic concurrency regression for the fixed initializer (patch; superseded by re-derivation).
- BH-10 — low: docs describe unresolved-only initialization while the candidate also initializes supported first deliveries. Resolved by narrowing runtime behavior with R3, preserving the documented boundary.
- EC-1 — medium: package probe proves the terminal-anchor fallback defect; same R1 root cause as BH-1.
- EC-2 — medium: the preserved-normal-behavior claim is violated by the same demonstrated terminal-anchor retry deadlock; same R1.
- VG-1 — medium: verified the SDK anchor validation compares ProjectionType before returning invalid-recovery-evidence; candidate fallback only accepts recovery-scope-mismatch, and normal tests omit both terminal phases. Same R1.

Groups: R1 terminal namespace fallback; R2 legacy preflight compatibility; R3 overbroad normal initialization/retention and its unerasable payload copies. Each is caused by non-frozen implementation planning; all route to bad_spec, with R3 high. No intent gap requires a human decision. Lower patch entries are incorporated into re-derivation rather than patched onto the rejected design.

Review pass 2 (2026-10-18), each finding graded before grouping:

- BH2-1 — low: the corrupted-manifest probe produces a less specific Retryable reason and preserves both pending envelopes; the SDK's ETag/first-write checks still reject unsafe writes. Missing immutable evidence cannot be recovered by this domain. Reject additional ownership parsing/guards for rare corrupt SDK records; canonical state remains fail closed.
- BH2-2 — low: verified collision coverage uses a fresh second store, so it cannot detect a persisted marker collision after supported-only initialization was removed. Patch P1: reuse the original store and assert distinct marker identities plus persisted advancement.
- BH2-3 — medium: verified with the actual baseline handler at 6b0641829b68d22656fb173eec6578c65e8e91f0: interrupt AfterCommit, retry returns AlreadyCompleted, and an uncompacted subsequent tail returns Retryable/projection_partial_retry. This predates DW-163; the revised handler preserves the normal batch contract. Defer D1 rather than extending payload retention or changing normal behavior.
- BH2-4 — low: domain cancellation after installation is not separately tested, but forwarding and cancellation recovery remain in the SDK; no new cancellation-specific handler branch or failure was demonstrated. Reject additional fault-harness complexity for this extra combination.
- BH2-5 — low: initialization format compatibility is not pinned by a versioned byte fixture; a future DTO-default change could alter the fixed v1 manifest. Patch P2: freeze both empty operation payloads and document the identity-version rule in the test.
- BH2-6 — low: persisted reverse-order coverage is missing despite mocked coverage. Patch P3: add unresolved-first/supported-second recovery with checkpoint and audit assertions.
- BH2-7 — low: a second unresolved overlapping handler is not a separate case; the existing stronger supported overlap, fixed-manifest contention, and optimistic audit tests exercise the relevant branches. Reject another concurrency harness without a demonstrated distinct failure.
- BH2-8 — low: raw newer canonical tombstones are the adversarial input for initialization recovery, and real erasure is independently exercised by the privacy regression. Reject added three-slot eraser concurrency harness complexity without a distinct demonstrated failure.
- BH2-9 — low: an earlier audit paragraph is not individually labelled historical, although the current-outcome notice and change log explicitly identify earlier audits as the rejected candidate. Reject the finding whose fix edits this build's spec, per review routing rules; the current tasks and latest audit state non-retention unambiguously.
- BH2-10 — low: final packet lacks its own full source revision map. Patch P4: record canonical Parties/baseline/root dependency IDs from Git with the final tested hashes.
- VG2-1 — low, pre-verified: same-store collision detection is broken because no initializer marker exists in the second store. Same P1 as BH2-2.

Groups: P1 same-store collision proof; P2 versioned initializer byte contract; P3 reversed unresolved ordering; P4 source-revision provenance. All are direct test/metadata patches with no public runtime surface. D1 is pre-existing normal compaction behavior. Edge-case layer returned no findings. No intent_gap or bad_spec entry remains; no further loopback is needed.

Final patch and verification audit: P1–P4 are implemented and the current source hashes are recorded in the final packet. Parent independently rebuilt the affected source projects and package consumer; 145 focused handler, 37 source recovery, 335 projection, 1232 service and 37 package recovery cases pass, with zero warnings/errors/failures/skips/not-run tests. Required warning and whitespace gates pass. Public methods in the new test type have XML documentation. Two review passes are complete; no current-story finding remains unresolved. One pre-existing legacy normal postcommit compaction issue is deferred with baseline reproduction evidence. DW-163 is complete.

The approved frozen decision excludes Git recording, so the workflow's default local-commit step is not performed. Concurrent external commits remain preserved, and all remaining changes are left for the user. No publication or deployment was performed.

## Verification

Build each affected test project with `--configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; invoke its built DLL with `-class` first, then run both full affected assemblies. Require nonzero discovery and zero failures. Run `bash scripts/check-no-warning-override.sh`. Before production edits, capture the Aspire baseline and stop the owned instance before builds. Validate the adopted package graph separately; source evidence cannot close package adoption.
