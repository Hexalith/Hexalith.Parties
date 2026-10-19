---
title: 'DW-173 and DW-193 erasure search notification fence'
type: 'bugfix'
created: '2026-10-19'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '750cf80f2f6f8380057cea9d834cd1a90bccc6be'
context: ['{project-root}/AGENTS.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Prevent live and shared-rebuild search reconciliation from leaving a party indexed after concurrent canonical erasure. Read canonical state for each notification instead of publishing retained snapshots. After awaited indexing, verify canonical state again and clean up if erasure won; preserve retryable failure and cancellation behavior. Verify both orderings with deterministic barriers and independent handler/eraser instances, preserving unrelated parties, tenant isolation, source metadata, and idempotent retries. Reconcile DW-173, DW-193 and the historical DW-80 completion claim with the verified guarantee. This covers the existing awaited indexing/mapping/cleanup contract; terminal Memories workflow completion, orphan recovery and cancellation gaps remain separately tracked in DW-168, DW-170 and related entries.

</frozen-after-approval>

## Implementation Notes

Planning: no human-only intent gaps or irreversible operations. Small footprint: existing `src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs` notification logic, focused regression tests in the projection and host test projects, `docs/data-models.md`, and ledger/evidence updates. No new public seam, dependencies, persistence mechanism or migration.

Reuse `IReadModelStore`, canonical index addresses and retained source-event descriptors. Live `ProjectAsync` currently notifies its folded snapshot; `CompleteRebuildAsync` rereads only once before awaited removals. Both need one bounded canonical reconciliation helper. A read failure cannot authorize indexing or announce convergence; failed post-index compensation stays retryable. Avoid singleton locks and unbounded retry loops. Retain rebuild synthetic attribution and live descriptor fallback. Use real SDK serialized in-memory storage and coordinated eraser in regressions; assert canonical tombstones and simulated external inventory, including the actual adapter/mapping pipeline where practical. Existing projection tests use several stateless mocks that must model persisted writes if they depend on newly required reads.

Validation: Debug source-reference builds and direct xUnit v3 focused assembly runs, followed by both affected complete assemblies. Retain baseline regression failures, exact commands and final outcomes under `tests/dw-173-erasure-search-fence-2026-10-19/`. Inspect Aspire state before code changes, stop the owned instance before builds, and report topology limitations separately.

Baseline runtime: isolated no-build Aspire start, Parties healthy wait, resource describe and exact-AppHost stop succeeded. EventStore and Tenants were Finished; full-topology qualification is not claimed. Initial seven behavioral regressions all fail against unchanged production code, reproducing stale live handoff, stale rebuild snapshot, ingestion/erasure overlap, missing compensation retries and false convergence during a post-index outage. Evidence logs are local under `/tmp/dw173-*`. Implementation rereads current canonical state per target, checks again after external work, requests removal after concurrent erasure and retries a target change or re-add. Existing stateless projection mocks were replaced with persisted stores where notifications must observe successful writes.

Independent Blind Hunter review returned eight findings. Two production issues were confirmed and patched: preserve JsonIgnore contact/identifier collections only for an exactly matching canonical target; reject null/mismatched target entries and null checkpoint dictionaries. Verification additions cover pre-notification outage, missing values, all invalid dictionaries, post-removal outage, unrelated concurrent writes, and cancellation during removal/compensation for both dispatch paths. Barrier regressions now have linked 15-second cancellation, cancellation-aware waits and finally-drained pending tasks. All 27 focused cases pass. The full affected suites and retained completion packet are being finalized. The user's repository instruction prohibits unrequested staging/commits, so the skill's automatic commit step will be skipped.

## Review Triage Log

- B1 — medium, patch: serialized index entries lose JsonIgnore search collections. Added exact canonical target matching before reusing the live fold; a dedicated regression fails before this correction and passes after it. Stored schemas and historical retry behavior remain unchanged.
- B2 — medium, patch: malformed null/mismatched target entries could authorize removal or another identity's indexing. Reject them as non-converged; validate all required dictionaries including checkpoints.
- B3 — medium, patch: extend canonical failure verification to pre-notification outage, missing state, null dictionaries, malformed target state and post-removal outage, asserting no unauthorized work or success.
- B4 — low, patch: add an actual unrelated aggregate write between target reads and require convergence without retry.
- B5 — medium, patch: test cancellation in ordinary removal and post-index compensation for both live and rebuild, followed by fresh-dispatch recovery.
- B6 — low, patch: bound barrier tests with linked cancellation and drain pending work in finally blocks; no timing-based race assertions.
- B7 — low, patch: reconcile DW-173/DW-193, the historical DW-80 claim and the related sprint action with the measured awaited-operation guarantee.
- B8 — low, patch: retain baseline failures, contact-field regression, exact validation commands, named final outcomes, source hashes and topology limits in the repository evidence packet.

Final completion: independent follow-up confirms all eight findings resolved. Both final Debug/source builds have zero warnings/errors. All 27 focused regressions and both complete affected assemblies pass (1311 host plus 379 projection, 1690 total unique complete-assembly cases) without errors, failures, skips or not-run cases. Final source/assembly hashes, named outcomes and retained failure transcripts are in `tests/dw-173-erasure-search-fence-2026-10-19/validation.json`; both parent and reviewer verified the hashes. CRLF-aware tracked whitespace, new-file whitespace, warning/nested-submodule gate and sprint YAML checks pass. DW-173/DW-193 and the related sprint action are done; DW-80's historical claim is corrected. No new deferred work, dependencies, migrations, deployments, staging or commits. Terminal Memories workflows and pre-mapping cancellation remain the existing DW-168/DW-170 obligations. Full-topology health remains unqualified despite passing Parties readiness.
