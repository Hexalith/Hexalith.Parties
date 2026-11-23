---
title: 'Verify five deferred recovery boundaries'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
baseline_commit: '36713d190971884dcc1bf1a80cea6a9e0d31c22f'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

**Problem.** Processing history can cache a revision before readback completes. Memories cleanup has incomplete evidence checks for typed admissions and can reuse an absence receipt after a new workflow finishes. Two migration and replay boundaries need additional persisted-state coverage.

**Approach.** Fix the local authority checks and exercise the missing cases for DW-50, DW-58, DW-82, DW-149 and DW-168. Retain the ledger's deployment, custody and owner gates.

## Boundaries & Constraints

**Always:** Use the existing read-model cache and SDK persistence interfaces, case-scoped Memories evidence, and disposable or synthetic fixtures. Keep ordinary command replay fail-closed when identity history cannot be opened.

**Never:** Change deployed data, approve a cutover, substitute synthetic custody for installed expiry, or widen a cleanup scope.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| DW-50 | Inline revision read, verification unavailable | Retain existing verified fallback | Do not cache the unverified revision |
| DW-58 | Canonical index changes after staging | Competitor bytes and revision survive commit | Report conflict and preserve unrelated keys |
| DW-82 | Typed retry identity with absent or conflicting admission evidence | Preserve the reservation and affected case before destructive reconciliation | Permit a later exact observation |
| DW-168 | Historical absence followed by a terminal same-unit producer | Require fresh scoped absence | Retain pending evidence across failed deletion |
| DW-149 | Ordinary command crosses synthetic acknowledged key expiry | Preserve persisted history and fail closed | Leave installed-provider qualification open |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` and `PartySdkQueryService.ProcessingBuckets.cs` read and verify inline history. `PartySdkLastKnownReadModelCache.cs` tracks cache admission generations. `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` covers fallback and revision races.
- `tests/Hexalith.Parties.Tests/Projections/PartyLegacyReadModelBackfillRuntimeTests.cs` and its fixture exercise SDK backfill against the disposable provider. `scripts/verify-backfill-and-memories-deferrals.py` pins qualifying test cases.
- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs`, its inventory partial and `PartyMemoryIngestionIntentStore.cs` reconcile persisted obligations. Reuse the wire evidence read by `PartyMemoryIndexingService.RecoverAdmissionAsync`. `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` covers receipt reuse and serialized state.
- `tests/Hexalith.Parties.IntegrationTests/Identity/PartyIdentityPersistedVerifierTests.cs` provides a deterministic clock, restartable persisted lane and test-only custody. `eng/verify-ext-parties-1.ps1` records the external gate.

## Tasks & Acceptance

- [x] `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` — defer inline cache admission until authoritative verification completes. Add history and export regressions for warm and empty fallback.
- [x] `tests/Hexalith.Parties.Tests/Projections/PartyLegacyReadModelBackfillRuntimeTests.cs` — exercise absent and existing index competitors between stage and commit. Require both cases in the verifier and its Python tests.
- [x] `src/Hexalith.Parties/Search/PartyMemoryCleanupService.Inventory.cs` and `PartyMemoryIngestionIntentStore.cs` — check the typed admission evidence before binding an unacknowledged retry. Add persisted failure and recovery coverage.
- [x] `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs` — include active, already terminal same-unit obligations in direct cleanup receipt decisions. Cover both receipt and workflow selection.
- [x] `tests/Hexalith.Parties.IntegrationTests/Identity/PartyIdentityPersistedVerifierTests.cs` — exercise ordinary profile and erasure commands before expiry and after acknowledged synthetic destruction and restart.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — record verified outcomes and remaining gates.

**Acceptance Criteria**

- Given each matrix condition, when the ordinary query, cleanup, backfill or replay entry point runs, then persisted state matches the expected outcome.
- Given a failed or canceled verification, when a caller retries, then tentative evidence cannot become authority for fallback or cleanup.

## Implementation Notes

- Complete inline reads and pages register cache admission before the canonical read and store revisioned records after readback. Invalid cursors are rejected before paged verification. Complete reads use the verified or empty degraded fallback when readback is unavailable.
- Typed retry inventory bindings wait for the final page before updating the persisted reservation. Cleanup compares the original protocol, source revision and identity digest, along with the source, case and producer. Missing evidence, conflicting claims and cancellation leave the reservation available for reconciliation.
- Direct cleanup settles exact same-unit terminal obligations together after a verified scoped absence. Failed DELETE or absence verification leaves active obligations pending.
- The post-stage backfill competitor writes an erasure fence using the disposable provider's physical ETag and the SDK conditional store. A stale SDK-visible writer is fenced. A metadata-only overwrite refused publication as indeterminate during diagnosis because the staged candidate could no longer be verified.
- The identity cases dispatch ordinary commands against the persisted test lane immediately before expiry and again after acknowledged key destruction and restart. They compare the persisted stream fingerprint and assert that custody copies remain unreadable.

## Spec Change Log

- Review added cases for revisionless and rejected page reads, incomplete or contradictory typed inventory, older canonical-only reservations and repeated backfill commit. The original owner and installation gates still apply.

## Review Triage Log

| Finding | Verdict | Disposition |
| --- | --- | --- |
| Blind review found revisionless and blank-tag inline data can displace a verified fallback | Medium | Preserve direct legacy reads and require a nonblank verified ETag for cache admission. |
| Blind review found overlapping successful reads can complete out of order | Medium | Existing cache generations protect eviction, and the provider's ETags are opaque. Revision ordering across two independently successful observations remains in DW-50. |
| Blind review found an unverified bucket header can replace a usable inline fallback | Medium | The existing bucket-header behavior is unchanged by inline admission. Record the bucket migration fallback case under DW-50. |
| Blind review found incomplete inventory can bind a typed retry | High | Keep typed reservations pending until every page supplies complete coverage. |
| Blind review found uppercase token hashes can evade duplicate detection | High | Normalize validated hexadecimal tokens before lookup and duplicate detection. |
| Blind review found the already-terminal sibling check can accept an unacknowledged typed record | High | Exclude typed reservations without a workflow before any terminal shortcut. |
| Blind review found a newly observed legacy alias may reuse a prior receipt | Medium | The pre-existing alias contract has an explicit no-delete compatibility test. Track production rematerialization of a previously unknown alias under DW-168. |
| Blind review found outcome reporting can miss a concurrent record update | Medium | Exact CAS removal preserves changed obligations for later reconciliation. The direct result reports the scoped absence observed, and coordination of parallel imports remains in DW-168. |
| Blind review found paged cache admission lacked a fallback assertion | Medium | Test the shared cache after rejecting the page and subsequently losing the store. |
| Blind review found post-conflict commit retry was untested | Medium | Retry with a fresh dispatcher and compare physical winner state after both attempts. |
| Blind review found the expiry test calls the processor directly | Medium | The test proves ordinary processor replay over persisted history. Installed custody and append-boundary qualification remain in DW-149. |
| Edge review found incomplete coverage leaves later token claims unknown | High | Hold deferred typed bindings through the complete inventory check. |
| Edge review found an older typed sibling can have a canonical ID without a workflow | High | Retain the pending reservation and skip DELETE until typed admission is established. |
| Edge review found prior absence can be borrowed by a newly resolved alias | Medium | Preserve established alias behavior and retain the undrained-producer scenario in DW-168. |
| Edge review found settlement can race an updated sibling | Medium | Exact record removal preserves changed siblings. Later direct and bulk reconciliation are still possible. |
| Edge review found null and blank ETags do not prove inline revision | Medium | Do not admit unversioned complete reads to fallback. |
| Verification review found a rejected page was only checked for its response | Medium | Assert that rejected pages cannot update the last-known cache. |
| Verification review found older typed records with a unit ID lacked direct and bulk coverage | High | Seed and test a serialized reservation with a unit ID and no workflow. |
| Verification review found a historical receipt can select an unacknowledged typed sibling | High | Assert that selecting the receipt retains the sibling and avoids destructive work. |

## Verification

- Built the core, runtime integration and integration test projects with .NET SDK 11.0.100 in Release package mode. All three builds reported zero warnings and errors.
- Gateway passed 1,515 cases. Search passed 544 cases. A final 24-case focused rerun passed the new fallback, typed admission, shared-unit and bulk recovery cases, including canceled inventory pagination.
- The disposable Redis/Dapr backfill lane passed 18 cases, including both post-stage competitors. The maintained verifier accepted all 18 XML cases and all 12 workload phase receipts.
- Persisted identity passed 13 cases, including ordinary profile and erasure replay before and after synthetic expiry.
- `python3 -m unittest discover -s scripts -p 'test_*_integration.py'` passed 84 tests. The warning guard and CRLF-aware Git whitespace checks passed.
- The standalone .NET 11 Release package build passed with the pinned dependency checkouts. The final test-only fixture adjustment built and passed all 11 typed retry cases.
- A final independent backfill run passed all 18 cases and matched all 12 workload receipts on Dapr 1.19.2 and Redis 7.4.7. Both disposable resources were stopped.
- Pinned commitlint 21.2.4 accepted the exact commit message with zero errors or warnings.
- The legacy Aspire AppHost built, then the CLI connection failed because its required profile directory was read-only. The focused and disposable-provider checks above ran independently.
