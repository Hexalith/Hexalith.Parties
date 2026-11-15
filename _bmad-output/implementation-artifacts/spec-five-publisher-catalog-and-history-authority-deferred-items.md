---
title: 'Resolve five publisher, catalog and historical-identity deferrals'
type: 'bugfix'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'cb9ba9ecfb76125efd407d294d3d01b62e8af687'
context: ['AGENTS.md', 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md', 'references/Hexalith.AI.Tools/hexalith-git-instructions.md', 'references/Hexalith.Memories/AGENTS.md', 'docs/build-gate.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five remaining deferrals concern custom publisher assembly names, unqualified EventStore 5.0.0 support, and Memories historical identity reuse after purge, during concurrent scheduling, and across source changes.

**Approach:** Fix evaluated publisher identity, settle exact catalog support, and address the three historical counterexamples through the selected owner-approved authority boundary. Review, verify and push owning changes to main before parent adoption.

**Approved decisions (2026-11-15):** User said "apply recommended": use evaluated TargetPath/AssemblyName; replace the standalone EventStore fallback with 5.1.0 and retain 5.0 rejection; reserve a nonreusable ownership generation for new Memories units and use per-unit reconciliation only for historical units needing cleanup. Reuse existing Redis/provenance machinery, without a separate allocator, signing infrastructure or generic migration framework. The trusted issuer is the configured Memories service; its deployment operator establishes the generation and exclusion of older writers. Source qualification may use an explicitly owned disposable generation, but cannot certify production exclusion.

## Boundaries & Constraints

**Always:** Preserve ordinary fresh ingestion, tracked lost-ack recovery, tenant isolation, cancellation, owned cleanup and unrelated survivors. Use run-with-temp for disposable jobs. Separate source, package and production evidence; retain unmet external gates.

**Never:** Infer unit completeness from absent workflow/base keys or source-wide reconciliation. Invent trusted authority or writer-exclusion proof, mutate production, weaken guards, initialize nested submodules, or add proprietary module CLIs.

**Ownership contract:** Atomically bind issuance to tenant/unit/generation before content writes across every ingestion path; retain minimal issuance/tombstone evidence across workflow purge and deletion. Existing bare modern markers are not new issuance proof. Bind historical reconciliation to the exact unit and evidence identity, with writer exclusion; changing source cannot change unit authority. Missing proof must refuse complete-erasure claims while ordinary ingestion and tracked retry recovery remain usable. Document one controlled writer cutover, restore/new-generation behavior and rollback. Do not enable or attest any real deployment in this batch.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| Custom AssemblyName | Resolve built output | Admit matching evaluated TargetPath; reject malformed, mismatched, missing or linked files without leaking output |
| Default 5.1.0 or explicitly selected 5.0.0 | Evaluate support | Fallback selects 5.1; 5.0 remains refused; caller/catalog precedence remains intact |
| Purged historical workflow | Reuse identity and clean up | Absent metadata cannot certify surviving chunks/NL as erased |
| Concurrent old producer | Complete between absent lookup and admission | Approved per-unit authority prevents false completeness |
| Source-A artifacts, reconciled source B | Reuse tenant/unit | Source-B evidence cannot authorize complete erasure |

</frozen-after-approval>

## Code Map

- `scripts/verify-eventstore-publish.py`: owner_assembly compares TargetPath to project.stem. `scripts/test_verify_eventstore_publish.py`: real MSBuild fixtures and redaction/process controls.
- `Directory.Packages.props`, `Directory.Build.targets`: fallback5.0 versus exact5.1 guards. `scripts/verify-catalog-runtime.py`: isolated native/runtime lane. `tests/Hexalith.Parties.Ci.Tests/AspireRuntimePreparationTests.cs`: evaluated cohort controls.
- Memories owner, `src/Hexalith.Memories.Server/`: `Ingestion/DaprIngestionWorkflowScheduler.cs` admits absent history; `Ingestion/SourceInventoryStore.cs` grants modern provenance; `Ingestion/SemanticKeyDiscovery.cs` trusts it. Annotation admission also reaches `Activities/Ingestion/AdmitSourceInventoryActivity.cs` and `Activities/Cases/ScheduleAnnotationIngestionActivity.cs`.
- Memories owner, `tests/Hexalith.Memories.IntegrationTests/Ingestion/RetryWorkflowErasureIntegrationTests.cs`: real Dapr historical artifacts and lost-ack controls. Parties implementation-artifact evidence `tests/five-snapshot-history-and-publish-2026-11-11/review-cross-source/` retains false-success proof.

## Tasks & Acceptance

**Execution:**

- [x] Publisher script/tests: evaluate TargetPath and AssemblyName together; preserve path, supervision and diagnostic guards.
- [x] Catalog files/CI tests: implement the selected support decision with exact positive/negative controls and actual runtime qualification where applicable.
- [x] Memories scheduler/inventory/tests: apply selected authority scope to purged identities; preserve fresh and tracked-retry behavior.
- [x] Memories annotation/admission/tests: cover concurrent historical creation through the same boundary.
- [x] Memories inventory/tests: cover cross-source reuse, complete persisted historical hashes and foreign survivors across recreation.
- [x] Ledger/spec/dated evidence: retain exact commands, full source bases, implementation results and external blockers for independent review.

**Acceptance Criteria:**

- Given custom evaluated identity, when resolution executes, then correct output is admitted and unsafe output refuses.
- Given a catalog support decision, when cohort controls execute, then only deliberately supported exact cohorts gain admission.
- Given each historical counterexample, when cleanup executes, then surviving historical artifacts cannot be reported completely erased; unresolved external authority remains explicitly open.

## Implementation Notes

Approved recommendation re-read from disk before implementation. Initial root and root-declared submodules were clean on main, except this workflow-owned draft. These are the final five unresolved flat ledger entries. Completed DW-196/197/198 are excluded. Implementation executes sequentially; the parent owns independent review and all staging/commit/push operations afterward. Keep production authority gates open, rather than treating source tests as a real cutover. Before code, inspect relevant owner configuration/guidance and obtain the available AppHost baseline per the required Hexalith instructions; record exact environment blockers without weakening checks.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH1 | medium | patch: directory settlement | Early workflow lookup/normalization precedes the scheduler proof catch; a pre-admitted directory entry can remain Preparing without any schedule call. |
| BH2 | medium | patch: reconciliation idempotence | The Lua expected-authority equality rejects an identical committed retry after a lost acknowledgement; accept only the identical desired authority while retaining all inventory guards. |
| BH3 | medium | patch: reconciliation bounds | The supplied list is sorted before its count guard, and SMEMBERS precedes any retained cardinality guard; both violate the bounded owner seam. |
| BH4 | low | patch: replacement coverage | Successful expected-authority replacement is an implemented owner operation, but only stale refusal was exercised; extend its existing persisted-state control. |
| BH5 | medium | patch: inventory omission coverage | The retained-obligation omission guard lacks an executed negative case; add a direct case preserving authority, provenance and content bytes. |
| BH6 | medium | patch: generation race coverage | The new global custody reservation spans tenants and reconciliation/issuance operations; add a direct competing-operation control proving one custody winner and no loser admission. |
| BH7 | medium | patch: event workflow verification | Prefixing event workflow IDs changes the production dedup-prefix branch; the adapter mock does not execute that branch or its retry/duplicate lifecycle. |
| BH8 | medium | patch: configured callers | Manual qualification in admission tests bypasses changed HTTP and CaseService ID issuance; execute configured entry points and bind returned IDs to authority. |
| BH9 | low | resolved: parent SDK qualification | A disposable actual Microsoft.NET.Sdk executable with Custom.Publisher AssemblyName built, resolved through owner_assembly and executed successfully; sdk-custom-probe.json retains the result. A new permanent harness is unnecessary. |
| BH10 | medium | patch: coherent fallback cohort | The exact older fallback families compiled the consumer but native preparation fails because current Tenants source requires RefreshTenantAccess absent from Contracts7.0.7. Align only fallback values to the already runtime-qualified5.1/8.0.0/4.0.0/6.0.0 cohort and verify the complete fallback selection. |
| EC1 | medium | patch: directory settlement | Duplicate of BH1: real scheduler preflight errors happen outside settlement, while the directory owns an already persisted Preparing admission. |
| EC2 | medium | patch: authority capacity | Accepted512-character Unicode evidence fields serialize beyond4096 characters, yet IsComplete rejects that size. Reject oversized authority before mutation using the same limit. |
| VG1 | medium | patch: configured callers | Filed regression gap accepted: existing endpoint/CaseService tests omit generation settings; manually qualified Redis controls cannot detect plain-ID regressions. |
| VG2 | medium | patch: directory failure coverage | Filed regression gap accepted: configured directory preparation-failure compensation is unexecuted; persist and settle Unscheduled across a recreated connection. |
| EC3 | medium | patch: settlement ownership | The expanded scheduler catch could settle a rejected conflicting producer. The correction uses a call-local acknowledged admission CAS result and full request binding; identical-body preflight failures must also preserve the existing owner. |
| P1 | false | rejected | ChangeAsync already returns before serialization when updated == current; an absent-entry proof callback does not create inventory. Read beyond the diff confirmed this refutation; a new no-mutation control verifies the outer-catch boundary. |

## Verification

Run publisher unittest classes with run-with-temp. Build individual CI/Memories test projects, invoke targeted xUnit executables with single-dash class filters, and use actual Dapr/Redis for historical controls. Inspect persisted bytes and owned cleanup; skips or missing prerequisites never qualify a gate.


Implementation verification (2026-11-15): publisher 14/14, CI cohort controls
120/120, Memories source integration 87/87, focused Server 56/56, and the
explicit 5.1.0 Release/package native runtime 1/1 pass with zero targeted skips.
The dated `tests/five-publisher-catalog-authority-2026-11-15/verification.json`
retains final commands, source bases, binary hashes and intermediate blockers.
Independent review, commitlint, owning pushes and parent adoption remain with
the enclosing workflow. No real deployment or writer-exclusion attestation was
performed; those external gates remain open.

## Publication Follow-through

The implementation step prohibits remote operations; these approved obligations
therefore follow independent review rather than blocking its entry:

- [x] Complete independent review and resolve actionable findings.
- [x] Validate exact owning commit messages with pinned commitlint, inspect divergence, commit and push Memories main, and verify its remote availability.
- [x] Record the reviewed owning revision, update parent adoption/signoff and validate the final publication inputs. Parties commit/push and remote verification are the terminal publication operation for this completed spec.

Parent matrix audit: all five frozen rows have executed passing coverage; see `tests/five-publisher-catalog-authority-2026-11-15/matrix-audit.json`. The initial implementation hashes are historical; `final-source-sha256.json` identifies the reviewed final files.

Review follow-through: the native runtime5.1 test used the current full catalog. An exact old-fallback-cohort probe exposed the stale Tenants7.0.7 native contract. Aligning the remaining fallback values to the already-qualified current cohort is necessary for coherent5.1 fallback support; explicit caller/catalog selections remain authoritative. All14 findings were classified before grouping; shared directory/caller findings are fixed once.


## Focused Review Patch Checks

The grouped patch and producer-ownership follow-up pass 51 focused controls with
zero failures or skips: authority 21, directory/event producers 9, configured and
ordinary callers 5, scheduler/directory units 13, fallback selection 3. Exact class
and method selectors, logs and current source hashes are retained in
`tests/five-publisher-catalog-authority-2026-11-15/patch-verification.json`.
The parent triage, actual custom-SDK evidence and compressed original receipts
are preserved. Full related lanes, refreshed final evidence and publication
remain parent follow-through; production authority and deployment gates stay open.


## Final Verification and Publication

All actionable findings are resolved, including call-local admission settlement
ownership (EC3); the existing no-op mutation guard refutes P1. The original
legacy-activity test exposed the replacement worker before victim history purge.
The corrected controlled cutover fences, terminates and physically purges through
the original worker before replacement exposure; no production activity guard
changed. Both variants and all 108 related integration controls pass.

Final results: publisher 14/14, CI 120/120, integration 108/108, Server 99/99 and
exact fallback native runtime 1/1; no targeted skips. All three final project
builds have zero warnings/errors. Native custody and disposable workspaces are
removed. Earlier counts remain historical implementation/patch receipts.
`tests/five-publisher-catalog-authority-2026-11-15/final-verification.json` is the
final source receipt; `publication.json` records owner publication and root
submodule availability.

Memories `7a5c892508700b2e5c71c921cdaeedf68de5f03d` is published on origin/main. Rebase preserves unrelated
release work `261e1b798c0a481b5a4841f2ccdf8574fe9ebe76`; all 24 tested files
are byte-identical across rebase. Parties adopts only this owning change.

Production writer exclusion, real per-unit historical inventories, matching
Memories package publication/adoption and deployed qualification remain open.
The publisher deferral is resolved; the 5.0 qualification request is superseded
by the approved support retirement, while 5.0 admission remains refused.
