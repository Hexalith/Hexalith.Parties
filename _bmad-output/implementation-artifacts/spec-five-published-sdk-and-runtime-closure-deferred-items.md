---
title: 'Close five published SDK and runtime deferred items'
type: 'bugfix'
created: '2026-10-26'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '463ad1bf14701f0622b68af4352e07941d1a7969'
context:
  - docs/development-guide.md
  - tests/README.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Package consumers still select EventStore 3.127.0 and bypass fixes
already shipped in 3.128.0. Completed runtime work also has stale umbrella and
authentication deferrals, obscuring the remaining release and migration gates.

**Approach:** Handle exactly five ledger items: DW-56 semantic query aliases;
unguarded batch recovery preserving newer canonical writes; legacy non-retained
batch compaction; DW-96 Story 8.6 residual review debt; and explicit Memories JWT
algorithm replacement. Adopt published EventStore 3.128.0, execute package and
runtime regressions, and reconcile each selected entry against current evidence.
Commit the reviewed task changes and push them to main as requested.

## Boundaries & Constraints

**Always:** Preserve historical receipts and current user work. Keep all retained
rollback seams. Bind new evidence to full source revisions and actual resolved
package versions. Require real persisted end-state and zero skipped targeted
runtime controls. Qualify the actual Memories Server project registration:
Server is non-packable, so changing a client package cannot prove its policy.
Replace its stale package-only prerequisite only with explicit project-host and
released-source provenance. Keep independent container/deployment and sidecar
token publication gates open. Mark selected entries done only when their stated
scope is actually proved; record unresolved evidence honestly.

**Never:** Publish packages or containers, deploy, migrate production state,
change submodule pointers, initialize nested references, disable warnings or
validation, or close DW-50/58/82/91/112/126/130/149/168/182/185 incidentally.
Memories refresh APIs absent from published 2.29.15 remain outside this batch.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| SDK query failure | Published contracts and unavailable read model | Semantic constants retain historical wire values | Existing gateway status remains compatible |
| Interrupted committed batch | Newer ordinary write or erasure tombstone | Recovery preserves newer serialized state | Foreign/uncertain ownership remains retryable |
| Legacy compaction | Committed non-retained receipt followed by a new tail | Original batch compacts; next tail progresses | Interrupted recovery resumes without payload retention |
| Runtime projection and ACL | Authenticated create/query; forbidden caller/path/verb | Exact projected fields, persisted envelopes, permitted calls and Dapr denials | No source-only/no-op proof substitutes |
| Explicit JWT policy | RS256/ES256 configuration, HS256 token, empty list | Effective selected algorithms replace defaults; selected tokens pass | HS256 denied; invalid lists fail startup |

</frozen-after-approval>

## Code Map

- `Directory.Packages.props` pins 3.127.0 before the Builds catalog. Published
  3.128.0 identifies `c5d1c280d8c1c1a207cd159d817f614e93c93130` and contains
  fixes `596de60e140da779f170279a822ded010070d774` and
  `da50f853b6c2c37193e9b1a8819858778d48d0f3`.
- `Directory.Build.targets` gates semantic aliases and nonexpiring recovery on
  source mode; the recovery symbol also enables published generation APIs.
- `src/Hexalith.Parties/Queries/PartySdkQueryFailureReason.cs` retains legacy
  package bindings. Preserve its internal consumer-facing vocabulary.
- `tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs`
  contains four legacy and four tombstone controls with source-only skips.
- `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` and
  `PartySdkCrossInstanceErasureTests.cs` cover query/erasure compatibility.
- `tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`
  supplies the authenticated projected-query and persisted-stream control;
  `Topology/PartiesMtlsTopologyTests.cs` supplies actual ACL enforcement.
- Story 8.6's six unchecked deferrals map to DW-30/32/33/53/50/51. ACL proof
  maps to DW-31. DW-50 remains an explicitly superseding open item.
- `src/Hexalith.Parties.AppHost/Program.cs` resolves Memories Server by project
  path. Its registered algorithm controls live in the referenced
  `tests/Hexalith.Memories.Server.Tests/Authentication/ServerRegisteredAlgorithmPolicyTests.cs`.
  Released 2.29.15 source is `38128624763c313238904c30aaeb88eb4fd9a6d0`;
  it contains the algorithm fix but predates refresh/inventory APIs.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props`, `Directory.Build.targets` — adopt 3.128.0
  and enable its available recovery/alias capabilities in package builds.
- [x] `src/Hexalith.Parties/Queries/PartySdkQueryFailureReason.cs` — use
  semantic published constants while preserving exact wire behavior.
- [x] `tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs`
  — remove obsolete source prerequisites; execute real package recovery controls.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`
  and `story-8-3-platform-api-prerequisite-matrix.md` — reconcile affected
  current selection assertions from canonical Git/package evidence, preserving
  dated parity receipts and keeping selection distinct from qualification.
- [x] `8-6-projection-and-query-sdk-migration.md` — reconcile six stale
  checkboxes with their owning ledger entries and fresh runtime proof.
- [x] `tests/five-published-sdk-and-runtime-closure-2026-10-26/` — retain
  sanitized package provenance, executed tests, review triage and commitlint proof;
  verify selected Memories registration and released-source equivalence.
- [x] `docs/development-guide.md`, `deferred-work.md` — explain effective
  package behavior and record only evidence-backed dispositions of the five items.

**Acceptance Criteria:**
- Given published dependencies, when recovery/query controls run, then all
  targeted cases execute and preserve canonical state and wire compatibility.
- Given the complete owned topology, when authenticated projection and ACL
  controls run, then persisted outcomes and Dapr denials pass without skips.
- Given the selected project-host policy, when configured algorithms and signed
  tokens are exercised, then replacement, rejection and startup controls pass.
- Given final review and validation, when task files are committed, then the
  exact message passes pinned commitlint and the commit is pushed to main.

## Implementation Notes

- Final parent verification: Release solution 0 warnings/errors; service 2780
  cases, 0 failures/4 unrelated opt-in skips; projection 463 total, 0 failures, 1 skip; Integration 165 total, 0 failures, 21 skips;
  actual algorithm controls 8/8 and configured runtime 3/3, both zero skips.
  Review follow-up executed live cancellation/failure/retry and concurrent-session
  controls 5/5 with zero skips; actual 3.127.0 captured-byte recovery passes.
- Dependent DW96 closure expectations and the current invariant map are
  reconciled without relaxing any validator, rollback or retirement gate.
- The final commit is marked breaking because package-mode read-model erasure
  now requires verified historical reconciliation and explicit attestation.
  Exact final pinned commitlint validation succeeded.

- Parent staged and inspected the implementation diff, verified all 17 tracked
  source/document hashes and four executed assembly hashes, and audited every
  frozen matrix row against actual zero-skip passing targeted receipts.
- Published package adoption also activates the pre-existing guarded generation
  path; its historical-reconciliation attestation stays false by default.
- Runtime qualification required the existing isolated identity mode and fixes
  for mutable worker observation and DCP fixed-port bookkeeping. Actual query,
  ACL and teardown controls pass; original-profile failures stay historical.
- Full selected Memories tests retain five owner evidence failures. Independent
  review and final Git operations follow implementation verification.

- 2026-10-26: User approved the unchanged draft and requested continuation.
  Implementation starts from the recorded canonical baseline on main.

## Spec Change Log

## Review Triage Log


| Finding | Verdict | Route | Verified evidence |
|---|---|---|---|
| BH-1 | medium | patch | The package symbol now reaches the default-false erasure guard. The existing unchanged-byte guard test proves refusal; the upgrade guide must identify this as newly active for package upgrades and state the reconciliation prerequisite. |
| BH-2 | medium | patch | The legacy test currently creates rows with 3.128.0, so it does not independently bind compatibility to actual 3.127.0 serialized rows. Add pinned historical writer bytes and an import/recovery control. |
| BH-3 | medium | patch | The ordinary-write hook ignores ordinal, leaving the second participant compaction boundary unqualified. Add explicit second-participant cases. |
| BH-4 | low | patch | Indeterminate alone does not identify the injected fault. Record and assert that the requested phase/ordinal was reached. |
| BH-5 | medium | patch | The guide claims preservation of later deletes while the selected matrix only exercises writes and tombstones. Add a physical later-delete control at the same committed completion boundaries. |
| BH-6 | medium | patch | The fixture observer and endpoint declarations changed startup/disposal behavior; four existing real startup/cancellation/failure/retry controls remained skipped. Execute them with the supported isolated session option, preserving original default behavior. |
| BH-7 | medium | patch | Endpoint bookkeeping changed for every session; the existing real two-session survivor/disposal control was not executed. Run it against the changed fixture. |
| BH-8 | low | reject | The missing combined-list test is an additional coverage improvement. Registration directly replaces the entire IConfiguration Get<string[]>() result without a list-length branch, and actual signed RS256/ES256 replacement/HS256 rejection controls pass. No combined-list failure is demonstrated; adding a separate owner test/build surface for this negligible unobserved defect is disproportionate. |
| BH-9 | low | patch | Record the actual projection assembly and selected EventStore Client binary identity/hash; these implement the newly enabled behavior and were omitted from the explicit executed-assembly receipt. |
| BH-10 | low | patch | Moving HEAD commands cannot reproduce a dated selection after source updates. Pin reproduction to the recorded canonical revisions and keep moving-HEAD commands clearly labeled diagnostics. |
| PF-1 | medium | patch, resolved | Final full service exposed the stale expected-open DW96 ID; removed only that completed ID, preserving directional debt and blocked-story checks. Final full suite passes. |
| PF-2 | medium | patch, resolved | Dependent current invariant rows still assigned proof to completed DW96; reconciled retained executable evidence and successor/open-owner gates. Validator unchanged; focused closure72/72 and final full service pass. |

The edge-case reviewer returned no findings; the verification-gap reviewer returned no gaps. All three same-capability context-free review layers completed before triage. The finding-floor arithmetic is review metadata, not an issue.

## Verification

- Build individual affected projects; execute direct xUnit class filters for
  recovery, query, cross-instance erasure and registered algorithm controls.
- Run `PARTIES_MTLS_TOPOLOGY_TESTS=true` with the built IntegrationTests assembly
  and both `PartiesMtlsTopologyTests` and `EventStoreGatewayE2ETests` selectors.
- Run affected complete per-project suites and the Release/package solution
  build; use the documented focused fallback ladder for environmental blockers.
- Run `bash scripts/check-no-warning-override.sh`, CRLF-aware diff checks and
  the repository-pinned commitlint against the exact full message file.
- Fetch origin, inspect divergence, push main without force and verify its SHA.
