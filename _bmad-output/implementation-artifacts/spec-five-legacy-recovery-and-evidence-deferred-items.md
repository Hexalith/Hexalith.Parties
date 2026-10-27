---
title: 'Resolve five legacy recovery and evidence deferrals'
type: 'bugfix'
created: '2026-10-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '67d16261ee9ba5f8b8595351ba96c211ebb5f9d2'
memories_baseline_commit: '22b792cf4f4a7aa5c9b6f129e6a491821169a8bf'
context:
  - docs/gdpr-processing-activity-records.md
  - tests/README.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Shorter rebuilds can discard audit facts, NL migration can activate
orphans, and retry upgrades lack historical replay proof. Two existing
EXT-PARTIES-1 preservation obligations remain unreconciled.

**Approach:** Resolve exactly five unnumbered entries under DW-198, originating
from `spec-five-processing-and-memories-erasure-deferred-items.md` or
`spec-ext-parties-1-owner-delivery.md`:

1. Preserve retained failed audit facts beyond the checkpoint during shorter rebuilds.
2. Qualify deterministic replay of original content-bearing NL retry workflows.
3. Reconcile pre-tombstone orphan legacy NL hashes before namespace activation.
4. Retain the EXT-PARTIES-1 review-triage record outside its owner-delivery spec.
5. Retain its Local and Live verification history outside that spec.

Qualify three safety items and reconcile two existing evidence records.
Commit reviewed changes, push Memories before its gitlink, then push Parties main.

## Boundaries & Constraints

**Always:** Preserve checkpoints, audit facts, erasure authority, exact ownership
and unrelated state. Reuse SDK batches/generations and Memories fences. Unknown
ownership requires reconciliation without copying/deleting content. Preserve
upgrade erasure guarantees and historical receipt bytes. Bind fresh proof to
full revisions and executed artifact hashes.

**Never:** Migrate production data, attest unknown historical completeness,
publish packages/images, deploy, initialize nested submodules or weaken gates.
Do not edit the Identity test lane, execute EXT Live/Local qualification, invent
owner dates or installed bindings, or change the stopped owner-delivery intent.
Evidence preservation does not establish Available, accepted or Live-qualified
delivery. Keep independent publication, deployment and owner-approval gates open.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Short rebuild | Checkpoint 50, failed audit facts through 100, replay through 50 | Refuse before staging/publication in both storage modes | Content-free refusal; unchanged persisted authority |
| Complete rebuild | Resolved replay covers retained audit boundary | Repair failed facts and advance coordinated checkpoints | ETag conflict preserves concurrent authority |
| Worker upgrade | Original same-name workflow suspended in description or embedding | Actual restart establishes deterministic terminal/retry behavior; replacement work progresses | Repair reproduced upgrade defect without replaying erased content |
| Orphan migration | Valid legacy hash but missing, foreign or concurrently removed syntactic owner | No orphan activation or destructive retirement; unrelated/live units preserved | Explicit bounded reconciliation refusal |
| Retained review | Thirteen existing triaged findings and reviewed sources | References and recorded hashes remain valid outside spec | Missing or mismatched evidence blocks closure |
| Retained history | Original Local exit 1 and both Live refusals | Exact retained bytes and original qualification status survive | Never relabel historical failures as success |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs`,
  `PartyDetailSdkProjectionHandler.ProcessingBuckets.cs` — rebuild preparation
  — guard retained inline/bucket audit bounds during pure preparation; retain
  optimistic publication.
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`,
  `tests/Hexalith.Parties.Tests/Projections/PartyProcessingGenerationRecoveryTests.cs`
  — projection/generation recovery controls.
- Under `references/Hexalith.Memories/src/Hexalith.Memories.Server/`:
  `Migration/RedisNaturalLanguageNamespaceMigrator.cs` normalizes missing
  syntactic source to empty; require ownership inside the final atomic mutation.
  `Workflows/NaturalLanguageEmbeddingRetryWorkflow.cs` immediately returns for
  raw legacy input. Reuse `NaturalLanguage/` retry ownership and erasure seams.
- Owner tests: `tests/Hexalith.Memories.Server.Tests/Migration/RedisNaturalLanguageNamespaceMigratorTests.cs`,
  `Workflows/NaturalLanguageEmbeddingRetryWorkflowTests.cs`; IntegrationTests
  `Ingestion/{NaturalLanguageErasureIntegrationTests,RetryWorkflowErasureIntegrationTests}.cs`.
  Extend historical helpers with same-name upgrade replay.
- `_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery/`
  — triage, source/hash manifests and historical results. All 17 retained hashes
  match; all 13 finding IDs exist. Local exit remains 1 and Available false.

## Tasks & Acceptance

**Execution:**

- [x] Projection handler and named Parties tests above — protect retained audit
  boundaries; cover shorter and complete rebuilds, both storage modes and conflicts.
- [x] Owner migrator and named migration/erasure tests — require exact live owner
  atomically; cover orphan, deletion races, restart and unrelated survivors.
- [x] Owner workflow and retry tests — pin original implementation/protocol;
  suspend each activity, upgrade the same-name worker and qualify replay/history
  erasure. Fix reproduced incompatibility; preserve modern retry progress.
- [x] New evidence directory
  `_bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/`
  — record executed commands, results, hashes, independent review and separate
  preservation checks for review triage and Local/Live history.
- [x] `deferred-work.md`, `tests/README.md`, `docs/gdpr-processing-activity-records.md`
  — reconcile the five entries only to demonstrated scope and explain refusals.
- [x] Owner Git metadata and root Memories gitlink — validate exact commit
  messages using pinned commitlint, fetch and inspect divergence, push without
  force, and verify remote main identities.

**Acceptance Criteria:**

- Given synthetic state, when safety controls run, then persisted outcomes pass
  with zero targeted skips and unrelated survivors remain unchanged.
- Given EXT artifacts, when preservation checks run, then hashes/findings and
  historical failure/refusal statuses agree without executing Live.
- Given reviewed passing changes, when pushed, then owning main branches and
  the parent gitlink identify the reviewed result.

## Implementation Notes

- Final parent verification: Memories main is published and read back at
  `f480fd70a7c25587264f50b5174358eee709c513`. Rebased upstream continuation is
  preserved. Final runtime 107/107 and package recovery 41/41 pass without skips;
  full Parties service 2,790 pass/zero failures/four optional skips; full
  projections 464 pass/zero failures/one compatibility skip. Release solution
  and focused source builds have zero warnings/errors. The broad Memories suite
  retains exactly its five documented gates. No gate was weakened.
- All three review layers completed and all 12 findings were individually
  disposed. Eleven corrections are verified, one low-impact inherited
  corruption-classification improvement rejected; no new deferrals. The two
  existing preservation obligations retain failed Local/Live evidence unchanged.
- Source, protocol-body, executed-DLL and retained-inventory verification pass.
  Both exact full messages pass repository-pinned commitlint 21.2.2. This final
  metadata amendment changes no reviewed implementation or dependency selection.

- Parent verification: staged and inspected both repository diffs. The six matrix
  rows map to executed passing controls and the separate preservation script.
  Strict verification confirms 13 source files and 12 executed DLL hashes.
  Source/package targeted controls pass with zero skips; broad blockers remain
  separately recorded. Review, clean-owner reruns and final Git tasks follow.

- 2026-10-27: User approved the unchanged spec and requested continuation.
  Implementation is sequential under bmad-build step 03; prepare source and
  validation first. The parent owns final review, staging, commits, gitlink
  adoption and remote operations after verification. Do not push during the
  implementation handoff.
- Baseline isolated Aspire start, describe and stop completed before code edits.
  Sanitized logs are in the new evidence directory. Startup/resource observations
  are baseline evidence, not a claim of whole-topology readiness.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH-01 | low | reject | Wrong Redis types and invalid inventory JSON fail before enrollment or content mutation. These inherited integrity-error paths do not activate or delete unproved content; extending corruption error classification adds guards for an uncommon condition without demonstrated harm. |
| BH-02 | medium | patch | The source-replacement branch is unreachable and missing/empty source ownership is not isolated. Add real Redis cases without letting another identity guard satisfy the expected refusal. |
| BH-03 | medium | patch | The new same-name upgrade releases old work before purge. Move release after physical purge and retain post-completion/restart absence assertions to qualify the combined boundary. |
| BH-04 | medium | patch | Modern progress currently starts from newly enqueued work. Seed the retained retry before the historical worker and drive that unchanged admission through the upgraded scheduler. |
| BH-05 | medium | patch | Source verification ignores recorded normalized hashes, so a CRLF checkout can reject unchanged source. Accept only matching canonical text hashes for sources while keeping executed binaries exact. |
| BH-06 | medium | patch | Python optimization removes every assert while success is still printed. Use explicit failing checks and demonstrate rejection under optimized execution. |
| BH-07 | medium | patch | Missing or duplicate checksum/source rows can currently pass. Pin the retained manifest and reject incomplete or duplicate inventories while preserving the original archive bytes. |
| BH-08 | low | patch | Reproduction-run receipts omit test/server DLLs and daprd despite the documented provenance claim. Capture those exact executed binary hashes in each runner receipt. |
| BH-09 | low | patch | Timeout/startup errors can leave no invocation/result in the runner receipt. Record attempted commands before execution and explicit failed outcomes before teardown. |
| BH-10 | medium | patch | Build commands reference private wrappers and generated mapping paths. Retain a portable wrapper/configuration and exact pinned regeneration command so a fresh checkout can reproduce the approved source selection. |
| BH-11 | low | patch | The protocol comparison is recorded but not recomputed by the verifier. Compare the retained original and helper method bodies during verification and reject drift. |
| VG-01 | medium | patch | The reviewer mutation probe demonstrates that removing only the empty-source guard activates legacy content while all existing cases remain protected by other checks. Add an isolated persisted empty-source control; same root cause as BH-02. |

All patch findings above are resolved and verified; no new deferrals were added.
The owner commit is published and committed-root checks pass. Parent publication
is the final authorized Git action after this evidence amendment.

All three context-free review layers completed. The edge-case layer returned no
findings. The blind finding-floor calculation is metadata, not a defect.

## Verification

- Inspect baseline Aspire lifecycle with its skill before source changes.
- Build affected projects in Debug with supported source mappings; invoke built
  xUnit assemblies using `-class` for the six named classes above.
- Run actual Redis/FalkorDB and authenticated Dapr upgrade/restart/purge controls;
  assert persisted contents, history state and unrelated survivors.
- Run affected suites and solution/package checks; use the documented fallback
  ladder and record broad blockers separately.
- Validate retained artifacts without requiring historical temporary files.
- Run `bash scripts/check-no-warning-override.sh`, CRLF-aware whitespace checks,
  pinned commitlint and final source/artifact hash verification.
