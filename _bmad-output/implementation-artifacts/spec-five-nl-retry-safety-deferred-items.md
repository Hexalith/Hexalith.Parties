---
title: 'Resolve five natural-language retry safety deferrals'
type: 'bugfix'
created: '2026-10-26'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '0e53be8dc88c46829be10af5f886b89dcd72464d'
memories_baseline_commit: '39be000902254c4717df6726bfcc3ea927b26b70'
context:
  - tests/README.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Memories natural-language retries can retain deleted tenant content,
misaddress malformed records, remove concurrent replacements, replay obsolete
legacy aliases, or publish stale generated output.

**Approach:** Resolve exactly five unnumbered deferrals originating from
`spec-five-processing-and-memories-erasure-deferred-items.md`:

1. Tenant deletion must purge live/dead retry payloads and scoped histories.
2. Qualify malformed historical retry payload addressing.
3. Make corrupt pruning and backlog trimming conditional on observed state.
4. Retire obsolete legacy aliases after modern-first completion using qualified lineage.
5. Bind final retry publication atomically to the current payload or version.

Implement in the owning Memories repository, qualify persisted outcomes, update
the Parties ledger and root gitlink, then commit and push both repositories to
`main` as requested.

## Boundaries & Constraints

**Always:** Preserve existing public constructor and ordinary ingestion behavior.
Reuse permanent tenant/unit fences, publication ownership and scoped workflow
erasure. Keep queue processing bounded and cancellation-aware. Require exact
tenant/unit addressing, observed-state comparisons and independently verified
workflow ownership before mutation. Preserve newer work, unrelated tenants,
foreign legacy addresses and uncertainty across restart. Unknown historical
coverage or ambiguous alias lineage requires explicit reconciliation; unit IDs,
timestamps and empty queues alone do not prove completeness. Legacy workflow
purge retains the existing default-false scheduler-drain requirement.

**Never:** Migrate or delete production data, attest historical completeness,
publish packages/images, deploy, initialize nested submodules, weaken validators,
or turn source qualification into package/deployment proof. Change only the
Memories root gitlink needed for this batch. Retain independent open gates.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Tenant deletion | Live/dead payloads and running/completed retry histories | Fence admission, purge owned content/history, preserve another tenant | Unproved scope, missing client or incomplete history discovery blocks success |
| Malformed addressing | Member A stores a payload naming B or another tenant | No retry or destructive mutation against B | Retain ambiguous obligations for reconciliation |
| Concurrent pruning | Selected bytes, score or membership changes before mutation | Preserve replacement in live/dead queues and hashes | Retry bounded cleanup against fresh state |
| Modern-first completion | Stable member sorts before a legacy alias | Retire proven obsolete aliases; preserve newer/distinct work | Ambiguous lineage cannot authorize deletion or stale publication |
| Publication race | Replacement or erasure during readiness/final write | Atomic rejection preserves newer output and queued work | Return a content-free stale/deleted outcome |

</frozen-after-approval>

## Code Map

The following owner paths are relative to `references/Hexalith.Memories/`.

- `src/Hexalith.Memories.Server/NaturalLanguage/FailedNaturalLanguageEmbeddingRegistry.cs`:
  dequeue/overflow validate tenant but not selected modern field identity;
  pruning/dead trimming remove unconditionally; completion misses differing aliases.
- `src/Hexalith.Memories.Server/NaturalLanguage/RetryNaturalLanguageEmbeddingActivity.cs`:
  reads current payload before indexing; retain identifier-only workflow history.
- `src/Hexalith.Memories.Server/Activities/Indexing/IndexNaturalLanguageSemanticActivity.cs`:
  awaits readiness after the retry check, then invokes publication.
- `src/Hexalith.Memories.Server/Ingestion/SourcePublicationVisibility.cs`:
  reuse atomic ownership/admission checks for the final expected-retry comparison.
- `src/Hexalith.Memories.Server/Activities/Tenants/DeleteTenantDataKeysActivity.cs`:
  omits retry namespaces/history. Reuse `Ingestion/TenantIngestionOwnership.cs`
  and `NaturalLanguage/RetryWorkflowErasure.cs`; do not delete Dapr keys directly.
- `src/Hexalith.Memories.Server/NaturalLanguage/NaturalLanguageEmbeddingRetryHostedService.cs`:
  owns scheduling and scoped/legacy addresses; retain durable content-free history
  discovery obligations before scheduling or removing queue entries.

## Tasks & Acceptance

**Execution:**
- [x] Owner registry path above — enforce exact addressing and conditional bounded
  pruning/overflow/dead trimming; track verified alias lineage through completion.
- [x] Owner retry activity, index activity and publication paths above — carry an
  internal expected retry identity into the same atomic operation as output writes;
  preserve existing ordinary indexing calls.
- [x] Owner tenant cleanup, scheduler and workflow erasure paths above — retain
  discoverable history obligations, reconcile live/dead state after fencing, and
  refuse incomplete cleanup; preserve compatible DI registration and constructors.
- [x] `tests/Hexalith.Memories.Server.Tests/NaturalLanguage/FailedNaturalLanguageEmbeddingRegistryTests.cs`
  and `tests/Hexalith.Memories.Server.Tests/Activities/Tenants/DeleteTenantDataKeysActivityTests.cs`
  — verify scope rejection and compatible composition.
- [x] `tests/Hexalith.Memories.IntegrationTests/Ingestion/NaturalLanguageErasureIntegrationTests.cs`
  and `tests/Hexalith.Memories.IntegrationTests/Ingestion/RetryWorkflowErasureIntegrationTests.cs`
  — execute every matrix row against actual Redis/Dapr, including adversarial
  interleavings, restart, physical absence and unrelated survivors.
- [x] Parties `deferred-work.md`, `tests/README.md` and
  `_bmad-output/implementation-artifacts/tests/five-nl-retry-safety-2026-10-26/`
  — retain sanitized commands, revisions, results and review dispositions; reconcile
  exactly the five selected entries against demonstrated scope.

**Acceptance Criteria:**
- Given unchanged valid callers, when retry and ordinary indexing execute, then
  they retain successful output and bounded live/dead queue behavior.
- Given the matrix states, when cleanup/retry races execute, then actual persisted
  bytes and histories satisfy all outcomes with zero skipped targeted controls.
- Given reviewed passing changes, when commits are prepared, then exact messages
  pass each repository's pinned commitlint, Memories is pushed before its parent
  pointer, and both remote main revisions match the intended commits.

## Implementation Notes

- Final mapped Debug Parties source-selection fitness: 115/115 passing,
  zero skips, against local adoption commit `23a03bfced8fd215f241bf18521299cb3300aa74`.
  Both messages pass pinned commitlint before and after commit. The final
  amendment updates evidence only; selected code and pointers are unchanged.

- Final corrected/rebased verification: 125 unit/indexing and 90 actual Redis/Dapr
  controls pass, zero targeted skips. All 16 individually triaged findings are resolved,
  with no new deferrals. Rebased full Server: 3069 total, the same five baseline
  evidence failures and one existing opt-in skip. Broad Integration: 515 total,
  170 confirmed nested-source metadata failures and one existing skip.
- Memories main is published at `22b792cf4f4a7aa5c9b6f129e6a491821169a8bf`. The approved frozen intent
  is unchanged. Current parent matrix/fitness selection records this exact adoption;
  historical receipts and release-owner gates remain intact.

- Initial implementation verification (historical after review corrections): 122 focused
  unit/indexing and 75 actual Redis/authenticated Dapr cases pass, zero skips.
  Source hashes match the executed receipt. Both Debug builds have zero warnings/errors.
- Full Server retains five unchanged prior evidence failures and one existing skip;
  broad Integration retains 170 nested-reference gate failures and one existing skip.
  Gates remain active and no nested references were initialized.
- Historical coverage and uncertain schedules require explicit reconciliation.
  Registered schedulers serialize scoped retirement; out-of-band Dapr purge is
  not execution-conditional. Ordinary indexing and existing constructors are retained.
- Ledger/docs are reconciled to source proof. Independent review and the authorized
  owner-first commits, gitlink adoption and pushes remain before completion.

- 2026-10-26: User approved the unchanged draft and requested continuation.
  Implementation proceeds from the recorded canonical revisions on main.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Verified evidence |
|---|---|---|---|
| BH-1 | high | patch | A stale dequeued version can acquire the lease and retire a newer terminal execution; the lease currently has no current-payload comparison. Revalidate admission before retirement. |
| BH-2 | medium | patch | The new scope exception escapes TickAsync tenant iteration and repeats on the first malformed tenant, starving later tenants. Isolate this demonstrated tenant failure. |
| BH-3 | high | patch | Rank selection returns only members; a replacement before later payload/score reads becomes the guarded state and is removed. Capture an atomic selection snapshot. |
| BH-4 | medium | patch | RemoveObservedAsync writes Unresolved before the conditional script; rejected pruning can permanently block cleanup. Commit that marker only after a successful comparison. |
| BH-5 | medium | patch | PurgeUnitAsync uses unbounded SADD while other writers enforce 65536; a new member can exceed the tenant cleanup bound. Preserve bounded idempotent admission. |
| BH-6 | medium | patch | Tenant cleanup already has the full validated union but repeats both queue scans per unit. Remove that quadratic rediscovery under the permanent tenant fence. |
| BH-7 | medium | patch | New permanent leases and default-false historical coverage need a supported owner reconciliation procedure. Document evidence, exact-scope inspection and uncertainty rules without granting inferred completeness. |
| BH-8 | medium | patch | ReadCurrentAsync accepts formatted JSON but publication compares a new serialization, rejecting unchanged valid bytes. Preserve observed bytes or conditionally normalize admission. |
| BH-9 | high | patch | TryDeserialize ignores unknown members and duplicate property ambiguity; record equality alone can authorize deletion of unproved alias lineage. Retain ambiguous JSON shapes. |
| BH-10 | medium | patch | RunRetryAsync bypasses sealed traced RunAsync, removing the existing linked span and outcome tags. Reuse traced execution without a new public/protected API. |
| EC-1 | high | patch | Independent edge tracing confirms the pre-payload-read selection race in BH-3; same root cause. |
| EC-2 | medium | patch | Independent edge tracing confirms missing capacity enforcement in PurgeUnitAsync; same root cause as BH-5. |
| EC-3 | medium | patch | Deletion pass confirms retry execution bypasses WorkflowTraceLinkedActivity.RunAsync; same root cause as BH-10. |
| VG-1 | medium | patch | Pre-verified gap: existing tenant deletion seeds every unit into the ledger; add actual queue-only/hash-only history discovery and physical absence assertions. |
| VG-2 | medium | patch | Pre-verified gap: helper tests manually manipulate authority; actual worker submissions always acknowledge. Add a worker submission failure/recreation test with persisted uncertainty and cleanup refusal. |
| VG-3 | medium | patch | Verified against the valid indented JSON fixture and bytewise HGET guard: unchanged noncanonical payload is refused. Same root cause as BH-8. |

## Verification

- Run the baseline Aspire lifecycle and inspect resources before source edits.
- Build affected projects in Debug using approved source mappings; execute built
  xUnit assemblies with `-class` filters for the named regression classes.
- Run complete affected per-project suites and relevant composition tests.
  Record broad blockers separately and follow the focused fallback ladder.
- Verify physical Redis contents and authenticated Dapr workflow history, rather
  than only mock calls or return codes. Reap only owned test infrastructure.
- Validate warning policy, whitespace and exact commit messages. Fetch and inspect
  divergence before each non-force push; preserve canonical revisions in receipts.
