# Deferred Work

### DW-1: Follow-up review still recommended for 8-2-identifier-correctness-and-zero-risk-hygiene after the review budget was exhausted

status: done 2026-10-22
origin: review-budget-followup
source_spec: `spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`
severity: low
archived: 2026-10-25

### DW-2: Follow-up review still recommended for 8-3-platform-api-prerequisites after the review budget was exhausted

status: done 2026-10-22
origin: review-budget-followup
source_spec: `spec-8-3-platform-api-prerequisites.md`
severity: low
archived: 2026-10-25

### DW-3: Add a fail-continuing lane runner

status: done 2026-09-07
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
archived: 2026-10-13

### DW-4: Add inspectable local test output and property forwarding

status: done 2026-09-06
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
archived: 2026-10-13

### DW-5: Define a safe consent and channel identifier contract

status: done 2026-09-07
resolution: resolved by sweep bundle dw-safe-consent-identifiers
resolution-undo: 7adbf51424c125b77bca8b3b97165cbe979cd88a46e456ab347a6b829727a629 2026-09-07 7374617475733a206f70656e
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`
archived: 2026-10-13

### DW-6: Correct and validate the advanced Hexalith.Builds checkout

status: done 2026-09-06
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
archived: 2026-10-13

### DW-7: Test persisted LRU eviction after restore

status: done 2026-09-07
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
archived: 2026-10-13

### DW-8: Test intermediate Memories state migration

status: done 2026-09-07
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
archived: 2026-10-13

### DW-9: Deliver granular EventStore.Client registration and coexistence proof

status: done 2026-09-07
origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
source_spec: none
archived: 2026-10-13

### DW-10: Deliver integrated AppHost topology parity proof

status: done 2026-09-08
origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
source_spec: none
archived: 2026-10-13

### DW-11: Deliver the external runtime deployment handoff

origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
location: external platform-operations owner repository
source_spec: none
reason: This independently governed operational handoff was split from the G8 owner-proof action because it requires platform-owner coordination after local run and publish parity are established. Also recorded in "Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18": deferral_id: `external-runtime-deployment` authored_by_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md` owner: `External platform-operations and deployment owners` exit_proof: `Consume immutable Parties image tags and provide environment-specific DAPR components, subscriptions, resiliency, deny-default access control, ingress, secrets, registry credentials, signing/scanning, and promotion evidence in the owner repository; prove rollback to the prior immutable image set.` rollback: `This repository keeps workload source, CI, immutable image publication, and the local Parties AppHost migration rollback topology. Runtime rollback remains an external orchestrator operation that redeploys the prior immutable image set and platform configuration.` evidence: `docs/deployment-guide.md and the Epic 8 architecture spine assign runtime deployment outside this repository; Story 8.13 retired the historical in-repo deploy assets, which must not be restored.`
status: open

### DW-12: Enforce multi-audience JWT validation at runtime

status: done 2026-09-07
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
archived: 2026-10-13

### DW-13: Harden EventStore AppHost publish and credential scanning

status: done 2026-10-22
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
archived: 2026-10-25

### DW-14: Validate G8-A delivery receipt integrity

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
archived: 2026-10-25

### DW-15: Bind the operational-index ACL route, verb, policy, and action

status: done 2026-09-06
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
archived: 2026-10-13

### DW-16: Reconcile BMAD branching guidance with default-main policy

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
archived: 2026-10-25

### DW-17: Pin the Node and npm runtime in release workflows

status: done 2026-10-22
origin: migrated from legacy ledger (flat source_spec "/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md"), 2026-09-06
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
archived: 2026-10-25

### DW-18: Fail semantic-release closed to the local binary

status: done 2026-09-07
origin: migrated from legacy ledger (flat source_spec "/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md"), 2026-09-06
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
archived: 2026-10-13

### DW-19: Hoist multi-token search candidate normalization

status: done 2026-10-17
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
archived: 2026-10-21

### DW-20: Normalize full-phrase multi-token relevance coverage

status: done 2026-10-17
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
archived: 2026-10-21

### DW-21: Make Party SDK erasure writes atomic

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
archived: 2026-10-13

### DW-22: Define idempotent ErasedAt semantics for retries

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
archived: 2026-10-13

### DW-23: Define erasure freshness metadata semantics

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
archived: 2026-10-13

### DW-24: Bound missing-CaseId warning deduplication

status: done 2026-10-17
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
archived: 2026-10-21

### DW-25: Document Party index ProjectionVersion semantics

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
archived: 2026-10-21

### DW-26: Define stable Article 30 operation categories

status: done 2026-10-17
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
archived: 2026-10-21

### DW-27: Report invalid PageSize separately from InvalidCursor

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
archived: 2026-10-25

### DW-28: Harden query compatibility shim lifecycle and state handling

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
archived: 2026-10-13

### DW-29: Preserve the cursor codec failure reason

status: done 2026-08-03
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
archived: 2026-10-13

### DW-30: Add authenticated projected-query end-to-end proof

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
archived: 2026-10-25

### DW-31: Add runtime DAPR ACL enforcement proof

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
archived: 2026-10-25

### DW-32: Clean up query shim and configuration naming

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
archived: 2026-10-25

### DW-33: Clean up Group 5 DI, health, naming, and status-key polish

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
archived: 2026-10-25

### DW-34: Strengthen cursor, payload, and hosted-service test fidelity

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
archived: 2026-10-25

### DW-35: Define whole-payload json-redacted event handling

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-36: Align reconciliation LastIndexedEvent with the canonical fold

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
archived: 2026-10-21

### DW-37: Test blocked Memories cleanup without a CaseId

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
archived: 2026-10-21

### DW-38: Harden rebuild completion against null manifest collections

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
archived: 2026-10-13

### DW-39: Refresh the G5 matrix validation-evidence identity

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
archived: 2026-10-13

### DW-40: Pin the reusable commitlint workflow revision

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-25

### DW-41: Verify all release package and container artifacts

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-25

### DW-42: Pin Story 8.7 G5 validation evidence to an exact commit

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
archived: 2026-10-25

### DW-43: Restore dropped G8 and cross-story guidance in Epic 8 context

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
archived: 2026-10-25

### DW-44: Prevent rebuild finalization from overwriting concurrent live projection writes

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
archived: 2026-10-13

### DW-45: Document and attribute the sprint-plan --fresh rebuild fix

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
archived: 2026-10-25

### DW-46: Align PartySdkProjectionFold logging with house style

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-47: Give projection-fold drop diagnostics stable logger categories

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-48: Bound dropped-event diagnostic volume during full rebuilds

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-49: Handle converter format and overflow failures during event deserialization

status: done 2026-10-17
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-50: Bound and page the per-party Article 30 processing-activity read model

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartyProcessingActivityFold and PartyProcessingSdkReadModel.Records
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyProcessingSdkReadModel.Records` grows unbounded — one ever-growing JSON blob per party, re-serialized on every processing-activity projection write. A real scalability concern but needs a pagination/archival design, not a quick patch. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `PartyProcessingActivityFold` retains one ever-growing list and performs a linear `FindIndex` for every event, producing unbounded state values and quadratic rebuild work.
status: open
decision: 2026-09-06 Paged bucketed model — Partition records into bounded state buckets and add cursor-paged reads with an explicit compatibility and migration path.

implementation_2026_10_25: Fixed-capacity inline/bucket storage, pure rebuild generation plans, bounded SDK staging, revision-scoped cursor reads and complete compatibility exports are implemented. Source projection/query controls pass. Matching SDK package publication/adoption and deployed writer drain remain separate gates; owner review/publication is pending.
verification_2026_10_25: `tests/five-processing-and-memories-erasure-2026-10-25/`; source receipt remains separate from package/deployment qualification.


review1_2026_10_25: Review-1 corrections add SDK-recoverable bounded audit manifests, no-op repeated unresolved delivery, coordinated checkpoint repair, bounded chunk IDs, retained sequence coverage and final revision revalidation. Parties query/recovery source lane passes 879 controls; full source client restore retains the FrontComposer HXW006 blocker. Matching generation SDK publication/adoption remains separate. Final executed evidence: `tests/five-processing-and-memories-erasure-2026-10-25/review1-verification.json`.

resolution_2026_10_25_final: Source bucket/paging/recovery implementation is reviewed and published in EventStore 694e37bad3e667931fbafc6a1ed9f85e4a0b4912 and Memories 7b0c8b5692600e293346eea7a84652eb87e4c346; matching SDK package publication/adoption and writer drain remain required. Status stays open; source proof does not close this gate.
verification_final: `tests/five-processing-and-memories-erasure-2026-10-25/`; spec `spec-five-processing-and-memories-erasure-deferred-items.md`.


qualification_2026_10_28: Restored EventStore 3.128.4 supplies the generation/recovery APIs. Current package controls execute large legacy conversion, bounded staging, interrupted restart, retained audit facts, concurrent-write refusal, scoped/stale/corrupt cursor rejection and complete authorized history/export reads. This supersedes the earlier package-publication blocker for the selected identity only; source diagnostics are recorded separately.
remaining_gate_2026_10_28: Drain all deployed older processing writers, independently reconcile historical private recovery material, authorize migration/cutover and qualify deployed state plus rollback. The default-false historical reconciliation attestation remains required. No production conversion or writer-drain proof is supplied; status remains open.
verification_2026_10_28: `tests/five-data-lifecycle-qualification-2026-10-28/` binds current nupkg provenance and executed assemblies to commands and per-test XML.


### DW-51: Resolve projection-handler performance and validation-coupling debt

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
archived: 2026-10-25

### DW-52: Clean up rollback-shim naming, configuration, and test debt

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
archived: 2026-10-25

### DW-53: Correct the Epic 7 rollback-retention approval chronology

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
archived: 2026-10-13

### DW-54: Refresh etags before retrying incomplete erasure batches

status: done 2026-09-07
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13

### DW-55: Test Memories erasure cleanup with disabled indexing and durable mappings

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13

### DW-56: Replace actor-era query failure vocabulary on the SDK path

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartySdkQueryService failure vocabulary
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: PartySdkQueryService still returns actor-era failure reasons on the SDK path, which misleads operators after AC8 actor deletion.
status: done 2026-10-26
decision: 2026-09-07 Compatible canonical aliases — Add semantic SDK and read-model aliases in EventStore.Contracts while preserving legacy wire values. Switch Parties source and tests to the aliases and retain old constants as obsolete compatible members.

resolution: EventStore provides semantic QueryExecutionFailure, ReadModelUnavailable and ResponseMismatch constants with unchanged historical wire values; legacy members remain compatible and obsolete. Compiled owner consumers and qualified Parties source consumers adopt semantic names. A narrow Parties bridge keeps the user-selected 3.127.0 package graph buildable until matching package publication.
verification: `tests/five-owner-config-and-recovery-deferred-items-2026-10-23/final-verification.json` records reviewed owner publication/adoption and current results. Original candidate and pre-review receipts remain historical; matching published aliases are still absent from the selected 3.127.0 package.
historical_remaining_gate_2026_10_23: Matching published EventStore aliases and package consumer adoption are not established by source proof.
closure_state_2026_10_23: Independent source review, owner main publication and root adoption are complete. EventStore owner 596de60e140da779f170279a822ded010070d774 supplies the aliases; user-selected package 3.127.0 still requires the legacy bridge, so matching package adoption stays open.

resolution_2026_10_26: Adopted published EventStore 3.128.0 (repository c5d1c280d8c1c1a207cd159d817f614e93c93130). Parties binds semantic published constants directly; package query/gateway compatibility controls retain historical wire values. This supersedes the former 3.127.0 package prerequisite; historical source receipts remain attached to their original revisions.
verification_2026_10_26: `tests/five-published-sdk-and-runtime-closure-2026-10-26/`

### DW-57: Move PartyEventTypeResolver out of the retired Actors folder

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-25

### DW-58: Provide Dapr-actor to SDK read-model key backfill

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Dapr-actor to SDK read-model deployment migration
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Story File List deletes actor projection paths without an AppHost/deploy cutover that migrates existing actor state into SDK keys.
status: open

implementation_2026_10_28: Six synthetic actor-to-SDK rehearsal controls now execute the actual SDK detail/shared rebuild protocols, recreate stores/workloads, inspect persisted detail/processing/index and preserve actor/foreign-tenant bytes. Incomplete/unreadable history and competing canonical writes refuse publication. A discovered shared-index Commit/Verify identity conflict is fixed by retaining the admitted conditional write policy. The migration handoff documents source-head/key completeness, writer drain, erasure, verification and rollback.
remaining_gate_2026_10_28: DW-58 remains open. In-memory persistence/source protocol evidence does not qualify a deployed provider, production capture, writer drain, runtime authorization or cutover.
verification_2026_10_28: `tests/five-reconciliation-and-migration-2026-10-28/`; `docs/read-model-migration.md`.


### DW-59: Preserve mappings when unit ID and source URI match different rows

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-60: Restore bounded allowlisted party IDs on SDK detail queries

status: done 2026-10-19
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-61: Update the Story 7.4 projection compatibility E2E spec

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
archived: 2026-10-25

### DW-62: Use TimeProvider for erasure cleanup timestamps

status: done 2026-10-19
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
archived: 2026-10-21

### DW-63: Protect Memories mapping replacement and clearing with concurrency-safe retries

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-64: Persist partial Memories cleanup progress after caller cancellation

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-65: Compensate after cancellation between Memories ingestion and mapping persistence

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-66: Use ingestion-time identity for Memories compensating deletion

status: done 2026-09-07
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13

### DW-67: Resolve consumer validation artifacts from central package versions

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-68: Contain consumer-validation package caches in the disposable workspace

status: done 2026-09-07
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13

### DW-69: Restrict consumer validation to configured NuGet sources

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-70: Compare forbidden NuGet package IDs case-insensitively

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-71: Pin reusable CI and CodeQL workflows to immutable revisions

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-72: Publish Aspire-hosted services with Production defaults

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-73: Fail publish preflight when the OIDC client secret is missing

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-74: Reject duplicate keys in merged BMAD configuration arrays

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-75: Tolerate deleted historical tags during release verification

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-76: Restore the Playwright accessibility lane as a required CI gate

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-77: Exercise mTLS across every configured Dapr sidecar

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-78: Align PartyErased timestamp resolution across projection folds

status: done 2026-10-19
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-79: Parallelize state-store reads in PartyDetailSdkProjectionHandler.PrepareRebuildAsync

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-80: Make Memories rebuild reconciliation atomic with concurrent erase and re-add

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-81: Move the Memories mapping ledger behind EventStore persistence

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-25

### DW-82: Verify empty Memories mappings against an authoritative inventory

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryCleanupService.DeleteByPartyAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryCleanupService.DeleteByPartyAsync` treats zero local mappings as cleaned even when state loss, legacy indexing, or configuration drift could leave remote units behind.
status: open
decision: 2026-09-07 Query Memories — Add or adopt a bounded Memories API that inventories units by canonical Parties source identity, reconcile local mappings from it, and require zero remote units before reporting cleanup complete.

implementation_2026_10_24: Memories now durably inventories bounded tenant/source admissions, historical canonical units, aliases, output-known state, independently observed workflow terminality and physical deletion. Parties fences and reconciles that inventory even with empty local state, retains partial discoveries and requires complete terminal source closure. SDK-protected version-bound cursors invalidate after admission/mutation; incomplete legacy coverage never becomes complete through dedup/search or maintenance registry cleanup.
remaining_gate_2026_10_24: Source capability and consumer controls are implemented; unchanged catalog packages still require owner publication/adoption. Legacy coverage requires explicit qualified owner reconciliation after older writers drain. No production migration or inferred historical completeness is supplied.
verification_2026_10_24: `spec-five-memories-inventory-and-publication-deferred-items.md`; `tests/five-memories-inventory-and-publication-2026-10-24/` separates actual Redis/FalkorDB adapters, consumer state/HTTP controls and live prerequisites.

review_loop_1_2026_10_24: Source inventory now retains independently verifiable scheduling, candidate/claim-check and terminal provenance. Proven unscheduled and independently failed work is physically reconciled; missing/terminated workflows and ambiguous payload writes remain fenced. Durable payload write tokens prevent deletion from certifying absence while a save can still materialize. The final real-backend lane passes 21/21; parent review/publication and legacy reconciliation remain open. Evidence: `tests/five-memories-inventory-and-publication-2026-10-24/review1-verification.json`; prior same-day counts above remain historical.
  review_loop_2_2026_10_24: Source inventory now batches exact deletion receipts and withholds terminal proof until every retained discarded candidate, failed-unit record and claim-checked payload is reconciled. Unknown legacy completeness stays open; final loop2 receipt is separate from earlier evidence.

qualification_2026_10_28: Restored Memories Client.Rest/Contracts 3.0.3 supplies explicit revision-aware refresh and bounded authoritative source inventory. Parties enables its existing protocol for this exact package selection; actual REST/serialized-state controls cover empty/partial mappings, bounded reconciliation, tenant/source/producer authority, incomplete coverage and pending/foreign terminal evidence. The current package publication/adoption prerequisite is superseded for these consumer controls.
remaining_gate_2026_10_28: Independently reconcile unknown legacy inventory after older writers drain, provide owner completeness attestation, and qualify the installed selected producer/workload configuration and deployed cleanup. Empty stores, dedup/search and synthetic fixture ownership cannot establish historical completeness; status remains open.
verification_2026_10_28: `tests/five-data-lifecycle-qualification-2026-10-28/`; prior source/live receipts remain historical and do not transfer qualification to a deployment.


### DW-83: Persist recovery when mapping and compensating deletion both fail

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-25

### DW-84: Schedule retry or backfill when Memories indexing lacks a CaseId

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-25

### DW-85: Keep Memories cleanup health observable when indexing is disabled

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-86: Validate erasure certificates before certifying store cleanup

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-87: Handle null Party index dictionaries with bounded recovery

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-88: Bound Party search inputs before cursor and index evaluation

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-21

### DW-89: Verify EventStore-only deny ACL across all Dapr sidecars

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
archived: 2026-10-13

### DW-90: Consolidate timeout-safe git and process test helpers

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
archived: 2026-10-25

### DW-91: Restore forward skip-link reachability after route focus

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: FrontComposer shell route-focus handling and tests/e2e/specs/parties-accessibility.spec.ts
reason: `frontcomposer-skip-link-reachability-after-route-focus` — **route to FrontComposer shell owners.** Measured 2026-08-19 on the accessibility specimen at FrontComposer `7a337a21`: once the shell hydrates it moves focus to the route `<h1>` (`h1#parties-accessibility-specimen-title`). That is a deliberate SPA announcement pattern, but it also advances the browser's sequential focus navigation point past both `.fc-skip-link` anchors, so the first `Tab` after load reaches the page's first interactive control rather than "Skip to content". A keyboard user would have to Shift+Tab backwards to reach a skip link after a client-side route change. On a cold document load the DOM order is correct — the skip links are the shell's first two focusable descendants, which `parties-accessibility.spec.ts` now asserts explicitly by seeding focus on `.fc-shell-root`. Question for the owners: should the shell reset the sequential focus navigation point (for example by focusing a container ahead of the skip links, or by focusing the skip link itself) so WCAG 2.4.1 bypass remains forward-reachable after route changes? Also recorded in "Deferred from: code review of story-8-10 (2026-09-06)": Skip links are no longer the real first-Tab keyboard stop after a client-side route change — already routed to FrontComposer shell owners as `frontcomposer-skip-link-reachability-after-route-focus` above; the review layer that raised this again confirmed no further action is needed beyond what that entry already tracks. [tests/e2e/specs/parties-accessibility.spec.ts:37-49]
status: done 2026-10-28
progress: 2026-10-21 Reviewed FrontComposer source fix and producer/Parties source regressions delivered on main. Producer browser cases pass 105/105 across three engines, consumer candidate cases pass 8/8, and the original six package assertions remain enforced. Owner publication, catalog/package adoption and removal of the two explicit source opt-ins remain required before closure. See spec-dw-91-forward-skip-links.md and tests/dw-91-forward-skip-links-2026-10-21/validation-resume.json.
decision: 2026-09-06 Restore forward skip access — Adjust FrontComposer route focus so skip links remain forward reachable while retaining an accessible route announcement, then add producer and Parties Playwright coverage.

implementation_2026_10_27: Source qualification now includes a deterministic regression for a delayed 150 ms landmark-focus retry found during this batch. Mounted skip targets focus once; overlay-editor focus retains its existing path. Forward route and compact keyboard assertions remain enforced. Published Shell 4.7.0 still needs matching publication/adoption. Qualification and remaining gates: `tests/five-frontcomposer-ui-deferred-items-2026-10-27/README.md`. Status stays open; this batch does not promote G4 or Story 8.9.

adoption_2026_10_28: Shell 4.7.1 at published producer 5688c3eba98567133285b0f51843d9daa8f24f0b is adopted. Cold/hydrated and client-navigation forward-Tab browser controls now execute without opt-ins or capability skips; the heading announcement and skip-target/onward flow pass against exact installed/served package bytes. This closes the named package-adoption exit only; G4 and Story 8.9 remain blocked. Evidence: `tests/five-published-frontcomposer-adoption-2026-10-28/README.md` (UI 445/445, Admin 327/327, Consumer 85/85 and browser 68/68, zero skips/retries).


### DW-92: Read the EventStore version from the Builds catalog

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
archived: 2026-10-25

### DW-93: Define authored_by_spec as non-activating deferral metadata

status: done 2026-09-06
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
archived: 2026-10-13

### DW-94: Define activated_by_spec as deferral-activation metadata

status: done 2026-09-06
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
archived: 2026-10-13

### DW-95: Define delivered_slices as partial-delivery metadata

status: done 2026-09-06
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
archived: 2026-10-13

### DW-96: Resolve or supersede Story 8.6 residual review debt

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Story 8.6 spec and runnable Parties/EventStore topology
reason: deferral_id: `8.6-residual-review-debt` authored_by_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md` owner: `Amelia (Parties Developer) + Murat (Test Architect) + Hexalith.EventStore SDK owners where producer/runtime proof is required` exit_proof: `Resolve or explicitly supersede every unchecked [Review][Defer] item in Story 8.6; in particular, exercise SDK handler discovery through an authenticated projected query and enforce the deny-default EventStore-only DAPR ACL in a runnable topology before removing retained host or ACL rollback seams.` rollback: `Keep the completed 8.6 SDK handlers and exact ACL as the production path, retain source/package selection plus the Parties AppHost and gateway topology as switch-back and diagnostic surfaces, and do not delete further host, query, projection, or ACL compatibility seams until the corresponding deferred proof passes.` evidence: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md and _bmad-output/implementation-artifacts/deferred-work.md record the unchecked review deferrals and their detailed evidence; sprint-status.yaml keeps 8.6 done because these are accepted non-blocking residual debts, not unimplemented acceptance tasks.`
status: done 2026-10-26

reconciliation_2026_10_26: All six historical Story 8.6 checkboxes are resolved against DW-30/32/33/53/51 or explicitly superseded by open DW-50. The current published package runtime passes authenticated projected-query and independently persisted stream verification. Existing isolated fixture identities/private namespaces qualify actual configured sidecar metadata, permitted 200 calls and all six Dapr-specific PermissionDenied 403 caller/verb/path controls, with all three selected runtime cases executed and zero skips. Original-name attempts collided with the user's non-mTLS discovery entries and remain historical failures; the passing receipt qualifies the explicitly configured isolated topology, not simultaneous original-name coexistence. Production ACL bytes, original default mode and every rollback seam remain intact.
resolution_2026_10_26: Scope of the residual umbrella is resolved or explicitly superseded. DCP now owns every fixed internal sidecar endpoint in the fixture graph before dynamic proxy allocation, and worker observation retains immutable planned names across model expansion. Complete Integration and focused custody controls pass; all fifteen failed/prior/final runtime sessions have no remaining owned workers, containers, networks or private directories. DW-50 and independent migration/deployment gates remain open.
verification_2026_10_26: `tests/five-published-sdk-and-runtime-closure-2026-10-26/`; exact runtime command sets both PARTIES_MTLS_TOPOLOGY_TESTS=true and PARTIES_MTLS_ISOLATED_IDENTITIES=true. Original-name, observer-race and port-allocation failures are preserved separately.

### DW-97: Complete Story 8.7 data-protection extraction

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Hexalith.Parties.Security and the Story 8.3 payload-protection row
reason: deferral_id: `8.7-data-protection-extraction` authored_by_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md` owner: `Hexalith.EventStore payload-protection owners + Amelia (Parties Developer) + Murat (Test Architect)` exit_proof: `Deliver the G5 runtime engine and Story 8.11 closure packet at an exact approved package or root-gitlink identity; pass protected, redacted, legacy, typed-unreadable, no-leak, Art.20, Art.30, erasure certificate/report, and exercised switch-back parity before changing Story 8.7 from blocked.` rollback: `Keep Hexalith.Parties.Security, all 18 MOVE files, all 5 KEEP files, EventStorePartyPayloadProtectionAdapter, local DI selection, and the compatibility harness. If shared-provider adoption later regresses, switch back to the retained local provider and rerun the compatibility harness before forward restoration.` evidence: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md — Payload protection engine package row; sprint-status.yaml keeps 8.7 blocked and the crypto-retention action open.`
status: open

### DW-98: Complete Story 8.8 runtime-boundary cleanup

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Parties runtime boundaries, AppHost, and Story 8.3 prerequisite matrix
reason: deferral_id: `8.8-runtime-boundary-cleanup` authored_by_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md` owner: `Hexalith.EventStore, Hexalith.Commons, Hexalith.FrontComposer, Builds, platform-AppHost owners + Amelia (Parties Developer) + Murat (Test Architect)` exit_proof: `Deliver and approve exact identities plus producer/consumer parity for G1/G2 degraded response and DAPR health, G6 envelopes/freshness, G7/G9 claims and identifiers, G8 security/typed-client/integrated topology, and G11 MCP/deep-link/capability helpers; exercise switch-back before deleting Parties-local paths or retiring the AppHost.` rollback: `Keep the Parties degraded middleware and health checks, Authentication project, typed clients, MCP context forwarding and five tools, AdminPortal links/probes, build selectors, and Parties AppHost. Revert each future adoption slice independently to these retained paths.` evidence: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md — EventStore degraded response, client envelopes, tenant claims, Aspire publish helpers, MCP/deep-link/search, Commons HTTP, and Builds rows; sprint-status.yaml keeps 8.8 blocked.`
status: open

### DW-99: Complete Story 8.9 FrontComposer UI consolidation

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Parties UI RCLs and FrontComposer Contracts.UI/Shell
reason: deferral_id: `8.9-frontcomposer-ui-consolidation` activated_by_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md` activation_authority: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md` delivered_slices: `G4 work package F only (shell skip links and role="main"/role="navigation" landmarks), adopted 2026-08-18 at FrontComposer root gitlink 7a337a21d4ba261bf27aeb3feedde47789f0160a; amended to 5cbc5583 on 2026-09-05/06, f0c3b6fd on 2026-09-07, b0ad2fb6 on 2026-09-13, and bf40099f in Round 6 (2026-10-04), then 374bb833 by the explicit 2026-10-04 three-pin approval (superseded). Current 2cc8dd3a3ac76c03f5ea6f6f92e65829306db470 (v4.5.0-121-g2cc8dd3a) was approved by Administrator / jpiquot later on 2026-10-04 by the working-tree decision, committed in Parties 2f6157d3, and kept in the 2026-10-05 final set; identity selection does not certify parity. Source-only FrontComposerRouteOptions / FcModuleLandingPage routing was adopted in 37f1d04b; package 4.5.0 lacks both. Commit 47e2da32 gates the route options behind source mode (DW-124 resolved 2026-10-05), and DW-139 owns the package-mode release/catalog proof. Parity counts only for receipts rerun at the 2026-10-05 final set. Work packages A-E remain undelivered and Story 8.9 stays blocked. App-owned focus-visible CSS isolation was repaired; I13 remains undischarged until Playwright focuses a content control (DW-111, accepted as a named deferral on 2026-10-05).` owner: `Hexalith.FrontComposer Contracts.UI/Shell owners + Sally (UX Designer) + Amelia (Parties Developer) + Murat (Test Architect)` exit_proof: `Deliver the complete G4 primitive set at an exact approved FrontComposer identity and pass producer bUnit plus Parties bUnit/Playwright parity for picker semantics, freshness/live regions, safe downloads, typed-name confirmation, skip links, forced colors, reduced motion, focus, and GDPR copy before changing Story 8.9 from blocked.` rollback: `Keep the Parties picker, freshness/status regions, download helpers, typed erasure confirmation, optimistic reconciliation, portal components, and current Fluent 2 styling until each replacement slice proves parity; revert a failed slice independently. The delivered shell slice rolls back by restoring the Parties-owned skip links, #parties-main-content, and #parties-app-navigation from the parent of superproject commit 2b63ab9 and pinning FrontComposer back to 97f44c499e83a0ffbf054febd0aab384054ea39e; that revert reinstates the duplicate skip-link strict-locator ambiguity the slice resolved, so it must be paired with a Playwright rerun.` evidence: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md — FrontComposer UI primitives row; sprint-status.yaml keeps 8.9 blocked; tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs and _bmad-output/implementation-artifacts/tests/test-summary.md record the 2026-08-18 shell-slice adoption.`
status: open

### DW-100: Adopt FrontComposer per-record freshness, live-region, and optimistic-reconciliation primitives after G4-B/C delivery

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Parties UI state components and FrontComposer G4-B/C primitives
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
reason: This independently shippable UI-state slice was split from Story 8.9 after its hardened draft exceeded the 1,600-token workflow limit.
status: open

implementation_2026_10_27: FrontComposer now supplies transport-neutral record freshness, polite/assertive/none live regions and callback-based reconciliation. Parties retains its wire/status mapping and polling/subscription adapters. Source parity uses actual shared registration; production adoption remains gated on named owner acceptance and publication. Qualification and remaining gates: `tests/five-frontcomposer-ui-deferred-items-2026-10-27/README.md`. Status stays open; this batch does not promote G4 or Story 8.9.

adoption_2026_10_28: The opt-in 4.7.1 package now supplies actual shared freshness and polite/assertive/none live regions while preserving Parties labels, freshness mapping and unregistered local rollback. Registered package services continue to use corrected local reconciliation: readiness precedes dispatch, early confirmations wait for acceptance, cancellation drains work and completion occurs once. The package lacks the newer registration-readiness contract, so fixed shared-reconciler publication/adoption remains open. Package parity does not qualify the current source graph or all G4-B/C. Evidence: `tests/five-published-frontcomposer-adoption-2026-10-28/README.md` (UI 445/445, Admin 327/327, Consumer 85/85 and browser 68/68, zero skips/retries).


### DW-101: Consolidate Admin and Consumer exports onto the approved FrontComposer browser-download service after G4-D delivery

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Admin and Consumer exports and FrontComposer browser-download service
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
reason: This independently testable download and cleanup slice was split from Story 8.9 after its hardened draft exceeded the 1,600-token workflow limit.
status: done 2026-10-28

implementation_2026_10_27: FrontComposer now supplies injectable stream/file/unchanged-JSON download with safe leaf names, cancellation and browser/interop cleanup. Admin and Consumer source candidates preserve bytes and safe caller filenames. Existing package-mode helpers remain the rollback pending owner acceptance and matching publication. Qualification and remaining gates: `tests/five-frontcomposer-ui-deferred-items-2026-10-27/README.md`. Status stays open; this batch does not promote G4 or Story 8.9.

adoption_2026_10_28: Approved package service adoption is qualified in Admin and Consumer flows with the runtime opt-in. Component/browser controls preserve JSON bytes, safe caller filenames, MIME type, repeated transfer, object-URL/anchor cleanup, cancellation and existing failure feedback. The package browser gate serves original assets without source substitution. Retained helpers remain available when registration is disabled; G4 and Story 8.9 are not promoted. Evidence: `tests/five-published-frontcomposer-adoption-2026-10-28/README.md` (UI 445/445, Admin 327/327, Consumer 85/85 and browser 68/68, zero skips/retries).


### DW-102: Adopt the FrontComposer typed-name destructive confirmation mode for Admin erasure after G4-E delivery

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Admin erasure UI and FrontComposer destructive confirmation
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
reason: This independently shippable destructive-interaction slice was split from Story 8.9 after its hardened draft exceeded the 1,600-token workflow limit.
status: done 2026-10-28

implementation_2026_10_27: FrontComposer now supplies optional ordinal typed-name confirmation with guarded single submit, safe Cancel focus, Escape and a native accessible warning across the V5 shadow boundary. Admin keeps its authorization and exact-name callback guard. Ordinary dialog callers and the local package fallback remain covered; owner acceptance/publication remain open. Qualification and remaining gates: `tests/five-frontcomposer-ui-deferred-items-2026-10-27/README.md`. Status stays open; this batch does not promote G4 or Story 8.9.

adoption_2026_10_28: The approved package typed-name mode is adopted with actual shared rendering. Empty, case and whitespace mismatches refuse submission; ordinal exact match and domain authorization guards permit exactly one request. Safe initial Cancel focus, accessible warning/description, Cancel/Escape and one-shot restoration to the recreated initiating button pass. The unregistered local dialog remains rollback; G4 and Story 8.9 are unchanged. Evidence: `tests/five-published-frontcomposer-adoption-2026-10-28/README.md` (UI 445/445, Admin 327/327, Consumer 85/85 and browser 68/68, zero skips/retries).


### DW-103: Complete Fluent UI V5 and Fluent 2 styling and accordion conformance across all Parties UI RCLs

status: done 2026-10-23
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
archived: 2026-10-25

### DW-104: Delete the retained local crypto and key-management engine and reconcile published Parties security APIs after shared-provider adoption proves parity and rollback

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Hexalith.Parties.Security and published Parties security APIs
source_spec: `_bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md`
reason: This destructive cleanup is independently shippable and was split from Story 8.7 after its hardened draft exceeded the 1,600-token workflow limit.
status: open

### DW-105: Make the parties-ui release container stay healthy in Development so the shared OCI smoke can finish and GitHub Release assets attach.

status: done 2026-10-22
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
archived: 2026-10-25

### DW-106: Execute the Release verify-source bash with a fake gh/GITHUB_OUTPUT harness the way EventStore does.

status: done 2026-10-21
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
archived: 2026-10-25

### DW-107: Add MSBuild/PublishContainer proof for rebound OCI created labels and reject impossible RFC 3339 calendar days.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: RebindContainerProvenanceLabels and shared OCI validation
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: RebindContainerProvenanceLabels is only string-checked; the shared OCI validator ignores org.opencontainers.image.created, and the regex accepts dates such as 2026-02-31.
status: done 2026-10-25
decision: 2026-09-06 Authorize cross-repo hardening — Add real calendar validation, executable publish and rebind proof, and required created-label validation in the owning repositories.
implementation_2026_10_23: Parties and shared Builds targets reject impossible calendar dates; the publisher binds both mandatory created labels to its exact instant. The existing three-argument publication reader remains compatible while graph validation requires canonical equal image/artifact labels and equality across platforms. Executable MSBuild, producer Python, real consumer-reader and SDK dual-RID archive checks pass locally.
remaining_gate_2026_10_23: Shared immutable publisher/validator adoption and publication remain required. The current release execution pin is unchanged; fetching the exact historical object restored full CI (449 passed). Published working-source hardening and its parent gitlink do not qualify that unchanged immutable executor.
verification_2026_10_23: `tests/five-publish-and-process-safety-2026-10-23/`; spec `spec-five-publish-and-process-safety-deferred-items.md`.


implementation_2026_10_24: The release workflow and publication reader now select published Builds integration `11a3a79cfac4bba9548c6364a17a01ee9b561370`; validator bytes are pinned to SHA-256 `1913f75bdf39c494c5723bca5c04fcb2e6dc0474ad2d86c22e63aae96b9e850c`. Valid OCI fixtures contain canonical matching image/artifact created labels. Executable missing, malformed, impossible-calendar, unequal-pair and cross-platform created mutations reject. The authoritative NuGet-404 behavior remains intact. Full CI passes 455/455 and the unchanged owner publisher suite passes 150/150.
remaining_gate_2026_10_24: Parent review and immutable adoption records/commit remain final workflow steps. No release dispatch, deployment or production publication was performed.
verification_2026_10_24: `tests/five-memories-inventory-and-publication-2026-10-24/`; historical 2026-10-23 observations remain preserved.

review_loop_1_2026_10_24: Re-executed the unchanged immutable publisher controls (150/150), Parties CI/OCI readers (455/455), and canonical-created positive/negative controls. Exact executor/validator pins and authoritative NuGet-404 behavior remain preserved; no release was dispatched. Evidence: `tests/five-memories-inventory-and-publication-2026-10-24/review1-verification.json`; prior same-day counts above remain historical.
  review_loop_2_2026_10_24: The immutable published executor and validator pins, canonical created-label fixtures and executable negative mutations are preserved. The loop2 receipt reruns the reader/CI/publisher controls without releasing or deploying.

implementation_2026_10_25: Published immutable executor 11a3a79cfac4bba9548c6364a17a01ee9b561370 was fetched read-only; its validator SHA-256 matches 1913f75bdf39c494c5723bca5c04fcb2e6dc0474ad2d86c22e63aae96b9e850c. The unchanged owner publisher suite passes 150 tests. Current CI prerequisite restoration and final review remain in the batch receipt; no release was dispatched.
verification_2026_10_25: `tests/five-processing-and-memories-erasure-2026-10-25/`; source receipt remains separate from package/deployment qualification.


review1_2026_10_25: Review-1 reruns pass 455 CI and 150 publisher controls. A new actual dual-RID SDK local archive passes the published executor/validator identities above with matching canonical created labels. Parent owns final closure/adoption review; no release was dispatched. Final executed evidence: `tests/five-processing-and-memories-erasure-2026-10-25/review1-verification.json`.

resolution_2026_10_25_final: The published immutable executor/validator pin and actual dual-RID archive pass the executable created-label controls. This closes the requested local MSBuild/OCI proof and immutable adoption debt; no release dispatch or deployment was performed.
verification_final: `tests/five-processing-and-memories-erasure-2026-10-25/`; spec `spec-five-processing-and-memories-erasure-deferred-items.md`.


### DW-108: Align diagnostic source gitlinks to the published nuget.org tags selected by the Builds catalog.

status: done 2026-09-07
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
archived: 2026-10-13

### DW-109: Cover ordinary main pushes with the root-gitlink RC sign-off gate, and pin Tenants/Memories identities.

status: done 2026-10-22
origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-110: Verify validate-publication-preflight.sh's commitlint-proof path is only ever reachable through an authorized bypass.

status: done 2026-09-06
origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-13

### DW-111: Observe app-owned content focus and reduced-motion CSS at runtime, not as source greps.

status: done 2026-10-21
origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-112: Hide or retarget Skip to navigation when FrontComposer unmounts `#fc-nav` on compact viewports.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: The skip-to-navigation link renders whenever `HasNavigation` is true, but `#fc-nav` mounts only when `HasNavigation && !IsSubCompactDesktopViewport`. On Tablet/Phone the href has no focus target. Parties consumes the shell and does not edit this submodule; route to FrontComposer shell owners.
status: done 2026-10-28
decision: 2026-09-07 Hide compact link — Render Skip to navigation only when #fc-nav is mounted, preserve Skip to content, and add producer plus Parties viewport coverage.
implementation_2026_10_23: The owning FrontComposer shell now uses the identical link/target viewport condition. Producer desktop/compact/resize cases pass. Parties component/browser compact checks are explicitly opt-in; the published-package negative control fails as expected, while its existing desktop keyboard flow passes.
remaining_gate_2026_10_23: Matching FrontComposer publication/adoption and positive Parties compact browser/component source qualification remain open. Pinned source Debug reaches Platform HXR020; focused consumer mapping fails MSB4044 (AssetsFile), including explicit net10 fallback. No warning/source gate was bypassed.
verification_2026_10_23: `tests/five-publish-and-process-safety-2026-10-23/`; spec `spec-five-publish-and-process-safety-deferred-items.md`.


implementation_2026_10_24: Compact tests automatically execute for validated source selections and compare actual served owner focus/CSS bytes. Actual desktop/phone/tablet/resized browser coverage revealed a duplicated `fc-nav` inside FluentLayout's hidden drawer; the owner now places the unique focus target on the owning navigation item outside copied ChildContent. Source bUnit and actual browser controls pass. Forced package negative controls still execute and fail behaviorally.
remaining_gate_2026_10_24: Matching FrontComposer package publication/adoption remains separate. Positive mapped source qualification closes the former source-assets blocker, not package qualification.
verification_2026_10_24: `tests/five-memories-inventory-and-publication-2026-10-24/`; pinned Node 24.22.1 browser and warning-free mapped source builds are recorded independently from package controls.

review_loop_1_2026_10_24: Re-executed mapped source bUnit (415/415) and actual source browser (4/4, zero skips) with owner asset comparison. Forced package bUnit and actual-browser controls fail for duplicate navigation targets as expected; matching package adoption remains deferred. Evidence: `tests/five-memories-inventory-and-publication-2026-10-24/review1-verification.json`; prior same-day counts above remain historical.
  review_loop_2_2026_10_24: The preserved mapped compact/browser and package-negative controls are rerun in the loop2 receipt; owner package publication/adoption and deployed qualification remain open.

implementation_2026_10_27: Existing unique mounted navigation targets and compact link conditions remain intact. Source viewport/resize controls now include the landmark-focus ownership correction described under DW-91. Published Shell 4.7.0 negative controls and publication/adoption gates remain separate. Qualification and remaining gates: `tests/five-frontcomposer-ui-deferred-items-2026-10-27/README.md`. Status stays open; this batch does not promote G4 or Story 8.9.

adoption_2026_10_28: Shell 4.7.1 package adoption closes the named mounted-target gate. Component and actual desktop/phone/tablet/repeated-resize browser controls run unconditionally with exact installed/served asset comparisons: navigation bypass exists exactly with one mounted navigation target, and the content bypass/route announcement survive. Source launch currently refuses HXR015 and receives no qualification from these package passes; aggregate gates remain blocked. Evidence: `tests/five-published-frontcomposer-adoption-2026-10-28/README.md` (UI 445/445, Admin 327/327, Consumer 85/85 and browser 68/68, zero skips/retries).


### DW-113: Parse only the first complete JSON object from MSBuild `-getProperty/-getItem` stdout.

status: done 2026-10-21
origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-114: Retire resolved Epic 8 closure deferrals from the accepted-wait set.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
archived: 2026-10-25

### DW-115: Reconcile the README SDK prerequisite with global.json.

status: done 2026-10-04
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
archived: 2026-10-13

### DW-116: Prove a FrontComposer RCL static asset is served in source mode.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
archived: 2026-10-25

### DW-117: Resolve the Fluent UI package root through NuGet and MSBuild configuration.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
archived: 2026-10-25

### DW-118: Make Playwright web-server ownership deterministic and test its environment matrix.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
archived: 2026-10-25

### DW-119: RecordConsentValidator does not guard LawfulBasis with IsInEnum and RevokeConsentValidator leaves Reason unbounded, unlike every sibling command validator.

status: done 2026-10-16
origin: spec-deferred 5a330376d9e2
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
archived: 2026-10-21

### DW-120: The deterministic lowercased ConsentId can collide when two contact channels differ only by letter case, while the channel lookup itself is ordinal case-sensitive.

status: done 2026-10-16
origin: spec-deferred 92120d4622ad
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
archived: 2026-10-21

### DW-121: The consumer portal maps the client's new ArgumentException to a generic Failed outcome while the admin portal maps it to ValidationRejected.

status: done 2026-10-17
origin: spec-deferred 637e8540aa5c
source_spec: `spec-safe-consent-identifiers.md`
archived: 2026-10-21

### DW-122: No test asserts that the production DI extension registers the consent validators, and the domain processor fails open when a validator is missing.

status: done 2026-10-16
origin: spec-deferred f8aa7466374b
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
archived: 2026-10-21

### DW-123: ContactChannelNotFound and IdentifierNotFound still interpolate caller-supplied identifiers into their messages outside the two consent handlers.

status: done 2026-10-17
origin: spec-deferred 671760729b1e
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
archived: 2026-10-21

### DW-124: Publish a FrontComposer Shell release carrying the module-landing route surface consumed by Parties.UI.

status: done 2026-10-05
origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-10-04)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-13

### DW-125: Align E2E Node declarations with the supported runtime floor

status: done 2026-10-21
origin: Story 8.10 Round 6 ledger normalization (2026-10-04); preserved package-update deferral
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-dotnet-10-0-401-and-submodules.md`
archived: 2026-10-25

### DW-126: Reconcile the runtime-toolchain qualification packet

origin: Story 8.10 Round 6 ledger normalization (2026-10-04); preserved package-update deferral
location: scripts/validate-runtime-toolchain-evidence.py and the runtime-toolchain evidence schema/baseline
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-dotnet-10-0-401-and-submodules.md`
reason: The validator/schema/baseline expect 13.5.0-preview.1.260825-0345; the selected CommunityToolkit.Aspire.Hosting.Dapr is now 13.6.0-preview.1.261001-0243. Coordinated packet regeneration is required to qualify the current catalog.
status: done 2026-10-28
resolution_2026_10_28: The specifically named validator/schema/baseline and obsolete preview references are absent from current tracked operative tooling. Current CI delegates to the shared domain workflow and validates declarations through the pinned module tool; the effective catalog is evaluated independently. This obsolete-packet report is inapplicable to the current repository. No retired validator was restored and no runtime/package qualification was inferred. The separate current source-map identity/catalog/tool-version blockers remain recorded.
verification_2026_10_28: `tests/five-reconciliation-and-migration-2026-10-28/disposition-verification.json` and reproducible `verify-dispositions.py`.


### DW-127: Align ineligible identity resolver and client replies

status: done 2026-10-17
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-21

### DW-146: Align server-produced Ineligible human/Unknown evidence with the public client validation contract.

status: done 2026-10-17
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-21

### DW-128: Validate binding command acknowledgments

status: done 2026-10-17
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-21

### DW-147: Reject empty or unrelated successful HTTP objects as binding command acknowledgments.

status: done 2026-10-17
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-21

### DW-129: Reject ineffective identity-history snapshot protection

status: done 2026-10-17
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: high
archived: 2026-10-21

### DW-148: Require identity-history snapshot protection evidence before reporting protected metadata.

status: done 2026-10-17
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-21

### DW-130: Qualify ordinary replay after retained-history expiry

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:143; src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:669-716
severity: high (unverified)
status: open

qualification_2026_10_28: The existing EXT-PARTIES-1 Live gate refuses before endpoint access because all twelve required live inputs are absent. No installed qualified identity-history custody binding or expiry run is supplied. Existing synthetic custody controls establish only their bounded behavior.
remaining_gate_2026_10_28: Supply the complete owner-approved installed custody/policy/restore/failure-injection bindings and qualify actual exclusive 365-day expiry, then execute ordinary profile, erasure, replay and switch-back commands and inspect persisted lifecycle continuity without expired actor values. Ordinary read denial must not authorize history skipping. Preserve independent history custody and rollback; status remains open.
verification_2026_10_28: `tests/five-data-lifecycle-qualification-2026-10-28/custody-live-gate.txt`; no continuity or expiry qualification is claimed.


### DW-149: Determine whether finite history expiry prevents ordinary Party command replay and establish value-free lifecycle continuity if needed.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:143; src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:669-716
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 4: command replay unwraps every history event, but fallback catches only destroyed profile keys. A live qualified custody expiry outcome was not exercised, so harm remains unverified. Existing identity-feature paths, unchanged here. Settle by advancing an actual qualified provider beyond expiry and running profile, erasure, and replay/switch-back commands; demonstrate replayable lifecycle state without expired actor values or record the exact failure.
status: open

qualification_2026_10_28: Fresh EXT-PARTIES-1 Live execution refuses before transport on twelve missing installed-provider inputs. No provider was invented or selected, no expiry was advanced and no ordinary replay after qualified expiry was exercised.
remaining_gate_2026_10_28: Complete installed owner-approved custody acceptance and actual exclusive 365-day expiry qualification, then determine the behavior of profile, erasure, replay and switch-back commands from persisted end-state. If qualified expiry prevents ordinary replay, establish value-free lifecycle continuity without expired actor values; a generic read denial is insufficient authority. Preserve independent history custody and rollback; status remains open.
verification_2026_10_28: `tests/five-data-lifecycle-qualification-2026-10-28/`; synthetic provider receipts do not close this external gate.


### DW-131: Validate identity fold wire representations

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-150: Reject unsupported serialization formats and metadata versions before identity source folding.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-132: Require test evidence on release validation bypass

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-151: Ensure the Commitlint-only release bypass cannot substitute for required test evidence.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-133: Exercise real Parties admission proof construction

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-152: Test real Parties identity admission scope construction rather than replacing the whole authority.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-134: Make visual regression and baseline updates executable

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-153: Provide an actual image regression baseline and an update command that updates the asserted artifact.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-135: Observe content forced-colors and reduced-motion behavior

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-154: Measure visible app-content focus and reduced-motion styles in the browser.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-136: Audit the complete shell with axe

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-155: Include navigation and skip links in the blocking accessibility scan.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-137: Verify provisioning through the registered domain processor

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-156: Add successful provisioning-envelope verification through PartyDomainProcessor with emitted events and enriched result assertions.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-138: Isolate wrong-purpose human custody rejection

status: done 2026-10-21
origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
severity: medium
archived: 2026-10-25

### DW-157: Separate expired-interval and wrong-purpose client rejection cases.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-25

### DW-139: Wire FrontComposer reserved segments and module landing in package mode after a Shell release

status: done 2026-10-22
origin: Story 8.10 final identity reconciliation (2026-10-05); follow-up of resolved DW-124
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: low
archived: 2026-10-25

### DW-140: Classify the identity-binding and agent-provisioning commands in the Art.18 restriction inventory

status: done 2026-10-05
origin: Story 8.10 full Release test lane at the 2026-10-05 final identity set
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
archived: 2026-10-13

### DW-141: Stop PartiesOverview from passing the source-only FcPageTabs.ModuleRoute parameter to packaged Shell 4.5.0

status: done 2026-10-05
origin: Story 8.10 full Release test lane at the 2026-10-05 final identity set
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: high
archived: 2026-10-13

### DW-142: Classify the identity-feature contract properties in the personal-data inventory

status: done 2026-10-05
origin: Story 8.10 full Release test lane at the 2026-10-05 final identity set
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
archived: 2026-10-13

### DW-143: Give the AdminPortal E2E fixture routes a current shell scope

status: done 2026-10-21
origin: Story 8.10 review of the closure-blocker resolution (2026-10-05), Verification 1 and other
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
archived: 2026-10-25

### DW-144: Require the stamped identity in every canonical closure receipt, not only Playwright

status: done 2026-10-21
origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 4 and L3 Edge 16
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
archived: 2026-10-25

### DW-145: Extend the personal-data inventory to the identity-feature contracts

status: done 2026-10-22
origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 5
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
archived: 2026-10-25

### DW-158: Reconcile the upstream Tenants runtime project's administrator verifier contracts with a published EventStore SDK.

status: done 2026-10-22
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-159: Include external dynamically launched runtime project graphs in orchestration dependency verification.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-160: Extend the existing gateway command test with a direct persisted-stream and aggregate identity read.

status: done 2026-10-22
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-161: Extend the persistent release verification job to validate all declared NuGet registry package bytes and repository commit metadata.

status: done 2026-10-22
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-nuget-trusted-publishing.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-nuget-trusted-publishing.md`
archived: 2026-10-25

### DW-162: Commit the existing Story 8.8 available-row identity gate correction through its owning work.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md`
archived: 2026-10-25

### DW-163: D1 — Preserve coordinated Detail/Processing slots when recording an unresolved first projection delivery.

status: done 2026-10-18
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-164: D2 — Set an explicit SDK receiver identity for the optional sample resource matching its DAPR app ID.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-25

### DW-165: D3 — Resolve the optional Memories resource endpoint for local Aspire runs.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-25

### DW-166: D4 — Align Memories cleanup, compensation and health probing with the versioned server route without accepting a routing 404 as proof of erasure.

status: done 2026-10-18
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-167: D5 — Persist the canonical terminal Memories unit identity after duplicate ingestion.

status: done 2026-10-23
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-25

### DW-168: D6 — Retain Memories cleanup reconciliation until ingestion and deletion have reached their terminal states.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: Memories cleanup mappings; CaseService asynchronous ingestion/deletion workflows
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Cleanup discards mappings on HTTP success/404 although CaseService schedules deletion asynchronously and accepted ingestion may not yet have materialized a unit; verified high, pre-existing.
status: open
progress: 2026-10-23 Consumer terminal reconciliation, durable settled absence, cancellation recovery and canonical publication fences are implemented and independently reviewed. Focused source/current-package/2.27.1-floor checks and actual SDK/Dapr persistence pass. Keep this entry open: selected-producer delayed-ingestion/deletion and physical-erasure qualification are not established. The original 2.27.1 runtime attempt was blocked by authenticated gateway routing; the independently selected 2.29.8 runtime has not been live-qualified.
verification: `spec-five-memories-recovery-deferred-items.md`; `tests/five-memories-recovery-deferred-items-2026-10-23/validation.json` and historical `runtime-blocker.json`.

implementation_2026_10_24: Output-known inventory no longer certifies workflow terminality. Consumer imports require independent terminal proof before deletion/retirement. Actual Redis/FalkorDB tests establish positive backend contents before physical deletion, unrelated-source survival and permanent replay fences. The owned nonproduction runtime protocol explicitly reconciles only a fresh controlled tenant/source, then forces pre-materialization, post-output/pre-terminal and asynchronous deletion delays, lost acknowledgement and adapter restart. The actual authenticated three-target run now completes tenant/case provisioning, current-content refresh, restart/erasure and unrelated tenant/case controls. SDK native stream reads match independent persisted actor bytes; real gateway administrative replay reconstructs first/final domain states.
remaining_gate_2026_10_24: The owned selected-source live boundary is qualified with a real separate Memories domain-service host executing existing decisions/folds and unchanged events. Matching EventStore/Memories owner publication/adoption, production workload/key/trust configuration, legacy reconciliation and deployed qualification remain separate. Broad module composition still fails Platform HXR020; the retained focused component graph does not claim whole-topology health. Parent review and immutable adoption records remain pending.
verification_2026_10_24: `tests/five-memories-inventory-and-publication-2026-10-24/`; private runtime configurations/cleanup handles are retained outside the repository.

review_loop_1_2026_10_24: The enhanced authenticated three-host wrapper and child pass 1/1 each, zero skips, with normal lost-ack indexing recovery, real extraction-failure/Dapr-payload absence, native persisted stream/replay comparison, delayed producer/deletion controls and unrelated survivors. Payload/save and failed-metadata fences close the delayed-writer boundaries. Deployed/workload configuration and legacy gates remain separate. Evidence: `tests/five-memories-inventory-and-publication-2026-10-24/review1-verification.json`; prior same-day counts above remain historical.
  review_loop_2_2026_10_24: Discarded post-index losers and lost output acknowledgements retain independent cleanup obligations across restart. Annotation graph failure records non-scheduling only from the actual pre-child workflow history; ambiguous calls and lost Preparing work stay fenced. Source/package/runtime/deployed evidence remains separate.

qualification_2026_10_28: Restored Memories Client.Rest/Contracts 3.0.3 and EventStore 3.128.4 remove the earlier selected-package prerequisite for consumer refresh/inventory/recovery controls. Actual REST requests now carry canonical revision and stable refresh identity in package builds. Supersession, lost acknowledgement, cancellation, pending producers, delayed terminal evidence and uncertain deletion retain durable obligations across serialized restart. This consumer evidence does not recertify the historical source-owned runtime against the newly selected deployed package topology.
remaining_gate_2026_10_28: Qualify the installed selected producer's delayed-ingestion/deletion and physical-erasure behavior, production workload/key/trust configuration and deployment, and independently reconcile historical source inventory after older writers drain. Whole-topology health, immutable adoption/owner acceptance and production reconciliation remain separate. No deployment or historical completeness is claimed; status remains open.
verification_2026_10_28: `tests/five-data-lifecycle-qualification-2026-10-28/` retains per-test package evidence and exact external gates.


### DW-169: D7 — Use concurrency-aware removal when cleaning a party memory-unit mapping inventory.

status: done 2026-10-18
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-170: D8 — Preserve durable reconciliation for cancellation after Memories accepts ingest but before the Parties mapping persists.

status: done 2026-10-23
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-25

### DW-171: D9 — Bound last-known-cache generation bookkeeping while retaining in-flight read invalidation.

status: done 2026-10-17
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-172: D10 — Prevent a second Parties instance from serving cached personal data after another instance completes erasure.

status: done 2026-10-18
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-173: D11 — Fence rebuild search notifications against erasure after the canonical index snapshot is read.

status: done 2026-10-19
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-174: Enforce the identity-query deadline during synchronous admission and source folding.

status: done 2026-10-22
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-175: Bound noncooperative identity-query provider work after a caller deadline.

status: done 2026-10-22
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-176: Make long retained human-actor history folds linear.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-177: Show accepted-command acknowledgement before slow admin list loading finishes.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-178: Clear the admin updating notice when the saved party reaches the projection.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-179: Restore the erasure confirmation action after a dialog open failure.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-180: Reproduce whether immediate blob URL revocation loses a supported-browser privacy export.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-181: Resolve the loop-sweep ledger fallback through layered BMAD configuration.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-182: Obtain owner signoff for all root gitlinks changed since Parties v1.2.1 before another release.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: scripts/gitlink-rc-gate.sh; root gitlinks changed since v1.2.1; release publication gate
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `bash scripts/gitlink-rc-gate.sh --diff v1.2.1` exits 1 for Builds, Commons, EventStore, FrontComposer, McpCli, Memories, Platform, and Tenants; current release workflow blocks publication at that gate. These later pointers need a new owner selection.
status: open

### DW-183: Test reason-only degraded admin search preserving last known rows.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-184: Assert accessible checked state after switching the admin party-type radios.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-185: Narrow the operator-created NuGet trusted-publishing policy to the Parties package IDs and publish-only operations.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: operator-owned NuGet trusted-publishing policy (packageGlobPattern and key permissions)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The recorded active policy uses `packageGlobPattern: "*"` and grants unlist/relist in addition to push; the local nine-ID inventory does not limit the NuGet-issued key. The policy was created separately from the original release patch and must be changed by its owner.
status: open

### DW-186: Check whether BMAD visual-tool consumers require an array from the layered TOML setting.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-187: Test authenticated actor and tenant-delivery callbacks through the real service route.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-188: Exercise the release registry-floor comparison with isolated published-version fixtures.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-189: Run the existing contact-selector browser test in required CI.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-190: Require the FrontComposer shared-status structural command and test its missing-command outcome.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-191: Validate McpCli and Platform source-selection rows independently of the eight consumed dependency fitness checks.

status: done 2026-10-21
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
archived: 2026-10-25

### DW-192: Derive erasure status from committed events or reconcile it after event persistence; cover failed append and retry before reporting Verified or Erased.

status: done 2026-10-23
origin: retro action item epic-8-retro-erasure-status-after-commit
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
archived: 2026-10-25

### DW-193: Fence live and rebuild search notifications against concurrent erasure; test both interleavings and reconcile deferred-work DW-80 with D11.

status: done 2026-10-19
origin: retro action item epic-8-retro-erasure-search-fence
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
archived: 2026-10-21

### DW-194: Preserve the typed destroyed-key outcome when the crypto-pending marker write fails; add focused fault-injection coverage.

status: done 2026-10-18
origin: retro action item epic-8-retro-destroyed-key-marker-fault
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
archived: 2026-10-21

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Reject inconsistent consecutive revocations in certified expired actor-history prefixes.
  evidence: RetainedHumanActorHistoryFold advances expired certificate versions without live/revoked state; readable transitions enforce a stronger lifecycle. Later domain work, separate from the CI/release patch.

  status: done 2026-10-21
  resolution: Certified expired prefixes now track whether the current version is revoked and reject a second revocation without a rebind. Serialized establish/revoke/revoke plus readable-successor regression rejects before custody; establish/revoke/rebind and establish/rebind/revoke controls retain exact successor evidence.
  verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Run the existing admin browser accessibility assertions in a blocking CI lane.
  evidence: The shadow-root warning and dropdown semantics are tested by Playwright, but root CI executes only .NET suites with mocked JS interop; the later portal behavior can regress while those suites pass.
  status: done 2026-10-21
  resolution: The required combined UI browser command executes the existing shadow-root warning, accessible-description, dropdown relationship and option semantics in the full admin spec. All three portal/picker specs pass in Release/package mode with the specimen in one report; the CI job remains unconditional and blocking.
  verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Give concurrent Parties integration topologies independent Dapr control planes, namespaces and app identities.
  evidence: The existing fixed control-plane container names/ports reject incompatible ownership; explicit mTLS reuse still shares application identities. Private Redis fixes direct store isolation but does not add general parallel-topology support.
  status: done 2026-10-22
  resolution: Immutable session configuration owns control-plane containers/networks, explicit external/internal ports, namespaces, trust/configuration paths and suffixed literal identities. Generated ACLs, workload/routing IDs and component scope follow the session without process-global environment mutation. The real two-session case proves independent markers and same-party event streams, own-peer routing, cross-session rejection and survivor work after peer disposal; bootstrap tests cover conflicts, cancellation and failed-cleanup ownership retry.
  verification: `spec-five-runtime-isolation-and-form-authorization-deferred-items.md`; `tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json`. Release/package live acceptance passed with zero skips; mapped Debug test-build qualification limits are recorded separately.


- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Propagate the topology startup token through its final HTTP health waits.
  evidence: Both independent three-minute health waits lack the overall startup cancellation token at the original release baseline, so total startup can exceed the declared five-minute budget.

  status: done 2026-10-21
  resolution: Already implemented by the current fixture: one linked five-minute startup token reaches both final HTTP health waits, in-flight GetAsync and poll delays. All six existing fixture cases pass, including pending-request cancellation, environment restoration, owned control-plane cleanup and retry after failed cleanup. No extra fixture abstraction or runtime-topology qualification is claimed.
  verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Verify bounded Aspire shutdown before adding explicit teardown deadlines.
  evidence: Unverified medium: StopAsync and disposal lack caller-side bounds, but a nonterminating SDK shutdown path has not been reproduced; trace SDK host/DCP timeout behavior or demonstrate a hung owned process before changing cleanup semantics.
  status: done 2026-10-22
  resolution: Exact Aspire 13.6.1 IL and DCP 0.26.5 source establish SDK host/disposal bounds but no executable-before-container cleanup dependency. The fixture therefore stops owned hosts and then sidecars before fully awaited SDK disposal, re-observes late workers within one cumulative thirty-second quiescence budget, and retains ownership on failure. Direct Linux PID/start-clock observations repair a separately reproduced cached non-child Process false positive. Normal, canceled, failed-start and retained-cleanup-failure/retry cases pass with workers gone while backing containers still exist, followed by owned container/network/listener cleanup. Completed stop proof survives observer failures and failed-stop siblings across automatic and explicit retries. The final integration batch passes 28/28, including seven real host cases, with zero skips; total observed cleanup is below sixty seconds in these runs, not an unconditional global SDK bound. All recorded intermediate resources were checked absent by exact ownership.
  verification: `spec-five-sdk-runtime-deferred-items.md`; `tests/five-sdk-runtime-deferred-items-2026-10-22/final-verification.json` and `shutdown-sdk-inspection.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-119-consent-validation.md`
  summary: Reconcile the isolated gateway fixture with the synced EventStore folded-snapshot qualification hosted service.
  evidence: The final source-reference run fails 52 EventStoreGatewayRoutingTests cases at the explicit hosted-service snapshot before routing. The same failures occur with the original consent validators. Inspect FoldedSnapshotQualificationHostedService dependencies before deliberately revising KnownExcludedHostedServiceNames; evidence is in tests/dw-119-consent-validation-2026-10-16/validation.json.
  status: done 2026-10-16
  resolution: Retained the safe DI-check/log-only startup service; the exclusion snapshot and existing assertions are unchanged. Full assembly passes 978/978. See tests/dw-119-consent-validation-2026-10-16/full-gate-validation.json.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-119-consent-validation.md`
  summary: Refresh current-source dependency observations and fitness constants for the three root gitlinks advanced by the prior sync.
  evidence: FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph expects the pre-sync EventStore, FrontComposer, and Tenants pointers while HEAD records the synced identities. It fails identically with the original consent validators. Update exact current observations without recertifying dated parity evidence or fabricating owner signoff; evidence is in tests/dw-119-consent-validation-2026-10-16/validation.json.
  status: done 2026-10-16
  resolution: Refreshed the three current-source identities/describes against committed gitlinks; preserved dated observations and owner approvals. Dependency fitness and full assembly pass. See tests/dw-119-consent-validation-2026-10-16/full-gate-validation.json.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-120-channel-id-collisions.md`
  summary: Make renewed same-channel consent revocable after a grant, revoke, and regrant sequence.
  severity: medium
  evidence: PartyState.Apply(ConsentRecorded) appends a second row with the same deterministic ConsentId after regrant, while PartyAggregate.Handle(RevokeConsent) selects the first already-revoked row and returns NoOp. PartyDetailProjectionHandler also ignores regrants with an existing ConsentId. This predates DW-120 and affects one owner without case collisions; fix aggregate/state/projection renewal semantics together with replay and lifecycle tests.
  status: done 2026-10-16
  resolution: Shared ordered consent folding retains renewal history and revokes all active same-owner rows; historical cross-owner collisions still reject. Exact retained-evidence replay and sequence-checkpoint transport retries are safe. Debug source-reference builds pass with zero warnings/errors; focused server consent/collision tests pass 71/71, DW-119 tests 86/86, contract/state/API tests 66/66, full Server.Tests 302/302, full Parties.Tests 978/978, and full Projections.Tests 238/238 without skips. Independent review and follow-up completed. Existing projections that omitted renewals require full SDK rebuild, documented and covered through persisted rebuild read-back. See spec-same-channel-consent-renewal.md and tests/consent-renewal-2026-10-16/validation.json. Wire contracts, dependency versions, and completed DW-119/DW-120 evidence remain unchanged. No push.

### DW-195: Align named-enum command binding between Parties clients and EventStore

status: done 2026-10-16
origin: DW-122 baseline reproduction
source_spec: `_bmad-output/implementation-artifacts/spec-dw-122-production-validation.md`
severity: medium
archived: 2026-10-21

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-129-identity-history-snapshot-protection.md`
  summary: Qualify retained-history snapshot capture through the EventStore SDK's folded-state input.
  status: done 2026-10-23
  implementation_2026_10_23: Strict modeled PartyState codec bridges SDK folded JSON to typed profile/history protection and restores all modeled state. Actual SnapshotManager capture, persisted storage recreation and post-restore command controls use synthetic custody; production custody/retention/expiry qualification remains external.
  verification_2026_10_23: tests/five-recovery-integration-2026-10-23/validation.json; independent review and final publication are parent-owned.
  severity: medium
  evidence: SnapshotManager serializes state to JsonElement before calling protection, while the pre-existing Parties guard requires typed PartyState for independent profile/history protection. Retain that fail-closed guard; a safe owner-supported typed integration and persisted SDK capture/read-back test are needed. The DW-129 encrypted unit round trip proves local validation and serialized restore only; no production custody qualification or SDK capture availability is claimed.

  resolution_2026_10_23: Strict codec and shared writer-policy validation pass actual SDK capture, serialized restart, restored command and malformed/mixed-profile controls. Local integration debt closes; production custody and expiry remain unqualified.
  final_verification: `tests/five-recovery-integration-2026-10-23/final-verification.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-163-coordinated-unresolved-projection.md`
  summary: Make EventStore unguarded batch reconciliation preserve newer canonical writes, including erasure tombstones.
  evidence: A real SDK InMemoryReadModelStore fault at BeforeReceipt after newer sequence-5 Detail/Processing erasure tombstones persist makes CompactAndCompleteAsync restore the older initialization candidate. The six-case candidate reproduction passed five cases and failed this interleaving; candidate source/logs/XML are retained in /tmp/dw-163-validation. EventStore owns this persistence guarantee; the Parties candidate was withdrawn and DW-163 stays open.

  status: done 2026-10-26
  resolution: Published owner main 596de60e140da779f170279a822ded010070d774 preserves superseding ordinary state and finishes owned retained anchors before verification. Complete Client suite passes 2,116 cases with zero failures and two environment skips; mapped Parties source controls pass 999/999 including all four erasure-tombstone interleavings.
  historical_remaining_gate_2026_10_23: User-selected package 3.127.0 does not contain the recovery fix; its package closure stays open pending matching publication/adoption. The following legacy normal-batch compaction debt is unchanged.
  verification: `tests/five-owner-config-and-recovery-deferred-items-2026-10-23/final-verification.json`; earlier original-candidate and pre-review records remain historical.
  implementation_2026_10_23: Source review, owner publication and root adoption are complete. User-selected published SDK 3.127.0 demonstrably still overwrites newer state; matching publication/adoption remains an open prerequisite.

  resolution_2026_10_26: Published EventStore 3.128.0 contains owner 596de60e140da779f170279a822ded010070d774 and is adopted by the actual package consumer graph. All four newer-erasure interleavings execute and preserve serialized canonical state. This supersedes the 3.127.0 publication/adoption prerequisite; foreign and uncertain ownership remains retryable and no retained rollback seam is removed.
  verification_2026_10_26: `tests/five-published-sdk-and-runtime-closure-2026-10-26/`

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-166-versioned-memories-cleanup.md`
  summary: Reconcile current dependency source observations and the unchanged release gitlink fitness constants with the already-committed Builds selection.
  evidence: FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph fails in the final full service suite and its focused invocation: expected Builds 4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77, actual 544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9. The starting Parties commit d2ec85c9e2b85e2b0c7a50d92ffaf197590ea053 already selects the latter, and the fitness test's baseline/current Git blob identities are identical. Restore accurate current-source observations through owning work without weakening the gate, fabricating owner signoff or recertifying historical parity receipts. Exact failing command, exits and proof are recorded in tests/dw-166-versioned-memories-cleanup-2026-10-18/validation.json.
  status: done 2026-10-18
  resolution: Current observations and fitness constants match all committed source selections; the old current observation is retained as history. All 33 prerequisite fitness tests and 1157 service tests pass. Dependency versions, gitlinks, owner signoffs and historical parity receipts are unchanged. See tests/dependency-receipts-2026-10-18/validation.json.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-163-coordinated-unresolved-projection.md`
  summary: Complete legacy non-retained normal-batch compaction before a subsequent projection tail needs writable ETags.
  status: done 2026-10-26
  implementation_2026_10_23: EventStore adds a caller-attested nonexpiring legacy recovery capability using original committed marker/envelopes, conditional writes and content-free final anchors. Generic callers still refuse unknown v1 retention. Parties source selection uses it only for known nonexpiring detail/processing writers; package 3.127.0 publication/adoption and source qualification remain distinct gates.
  verification_2026_10_23: tests/five-recovery-integration-2026-10-23/validation.json; independent review and final publication are parent-owned.
  evidence: The baseline PartyDetailSdkProjectionHandler from 6b0641829b68d22656fb173eec6578c65e8e91f0, compiled against the actual 3.125.1 package graph, returns Indeterminate after an injected AfterCommit failure, AlreadyCompleted on original redelivery, then Retryable/projection_partial_retry for a subsequent supported tail while its committed envelopes remain uncompacted. Replaying the exact original SDK batch compacts them. This predates DW-163 and the approved fix preserves normal batching; do not repair it by retaining payload-bearing manifests without owner-supported erasure/recovery guarantees. Reproduction uses that tracked baseline handler with ReadModelBatchPhase.AfterCommit fault injection in the existing SDK fake.

  resolution_2026_10_23: Reviewed EventStore da50f853b6c2c37193e9b1a8819858778d48d0f3 is published and adopted at a9f4edb5a8a769e36499063b5b88413af01a7bd0. Source recovery, mixed deletes, conditional races and next-tail writes pass. Matching published SDK adoption remains open because package 3.127.0 is unchanged.
  final_verification: `tests/five-recovery-integration-2026-10-23/final-verification.json`.

  resolution_2026_10_26: Published EventStore 3.128.0 contains owner da50f853b6c2c37193e9b1a8819858778d48d0f3 and is adopted. All four legacy controls execute in package mode, including interruption/restart, erasure preservation, compacted ETags and successful next-tail progress without payload retention. This supersedes the earlier package prerequisite; DW-50 writer drain, historical reconciliation and migration gates remain open.
  verification_2026_10_26: `tests/five-published-sdk-and-runtime-closure-2026-10-26/`

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-67-central-consumer-support-versions.md`
  summary: Normalize NuGet versions consistently before validating support artifact filenames and nuspec versions.
  severity: medium
  evidence: The unchanged validate_support_packages helper rejects SDK-produced 6.5.4.0 and 6.5.4+build.9 packages: both filenames normalize to 6.5.4, while nuspec versions are respectively 6.5.4 and 6.5.4+build.9. Independent review reproduced both forms for FrontComposer.Contracts, Commons.Http and EventStore.Client, confirming a pre-existing shared-helper limitation. Current selected 4.6.0/5.8.0 versions do not trigger it; fix and cover all families together rather than changing established Commons/EventStore semantics inside DW-67.
  status: done 2026-10-21
  resolution: The shared support validator normalizes zero revisions, short cores and build metadata across Commons, EventStore, FrontComposer and Tenants. Strict filenames, meaningful revisions, prereleases and artifact inventories remain checked; real SDK-produced archive regressions cover the normalized forms.
  verification: `_bmad-output/implementation-artifacts/tests/five-package-validation-deferred-items-2026-10-21/validation.json`; spec `spec-five-package-validation-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-67-central-consumer-support-versions.md`
  summary: Qualify restored FrontComposer and Tenants consumer versions against their evaluated central support versions.
  severity: medium
  evidence: validate_consumer calls only validate_event_store_assets after restore, so correct local FrontComposer/Tenants support artifacts do not prove those versions were selected if NuGet chooses a different transitive requirement. The same gap exists in the baseline; add restored-asset family checks and a synthetic selected-version mismatch regression separately. DW-67 checks the generated direct support artifacts and preserves the existing configured-feed transitive boundary.
  status: done 2026-10-21
  resolution: Consumer validation checks every restored family package against its evaluated central version before build, and portal consumers require all four direct FrontComposer/Tenants support packages. Main-path wiring and stale, missing, project-backed and case-variant selection regressions cover both consumers.
  verification: `_bmad-output/implementation-artifacts/tests/five-package-validation-deferred-items-2026-10-21/validation.json`; spec `spec-five-package-validation-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-69-consumer-nuget-source-isolation.md`
  summary: Isolate consumer validation from inherited NuGet fallback package folders.
  severity: medium
  evidence: Independent review restored a synthetic package from an inherited fallbackPackageFolders cache despite generated packageSources containing only empty local feeds. This separate setting is unchanged from baseline; resetting packageSources does not reset fallback folders. The DW-69 documentation now limits its guarantee to packageSources. Add deliberate fallback isolation and a missing-package regression without weakening package-only Release consumer validation.
  status: done 2026-10-21
  resolution: Generated NuGet configuration clears fallbackPackageFolders. Real offline restores prove a populated parent fallback cannot satisfy an undeclared dependency while explicit feed selection succeeds with a fresh private cache.
  verification: `_bmad-output/implementation-artifacts/tests/five-package-validation-deferred-items-2026-10-21/validation.json`; spec `spec-five-package-validation-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-69-consumer-nuget-source-isolation.md`
  summary: Prevent inherited disabledPackageSources from suppressing explicitly selected consumer feeds.
  severity: medium
  evidence: An inherited disabledPackageSources entry for additional-source-1 suppresses an explicitly selected generated feed and makes a synthetic package restore fail with NU1101. Generated keys and inheritance predate DW-69. The documentation exposes the limitation; isolate/reset disabled-source settings and cover effective restored sources separately.
  status: done 2026-10-21
  resolution: Generated NuGet configuration clears disabledPackageSources. An offline restore succeeds with both generated local and explicit feed keys disabled by the parent, and asserts the effective source set and selected package.
  verification: `_bmad-output/implementation-artifacts/tests/five-package-validation-deferred-items-2026-10-21/validation.json`; spec `spec-five-package-validation-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-72-production-publish-environments.md`
  summary: Qualify production authentication configuration for optional Memories in retained AppHost publish output.
  evidence: The unchanged Memories publish branch supplies Redis/FalkorDB connection strings but no JWT authority/signing key, issuer or audience. Memories appsettings.Production.json supplies none, and ValidateServerAuthenticationOptions rejects their absence at startup. Pinned Aspire 13.6.1 adds launch-profile environment variables only in run mode, so baseline publish already defaults this optional service to Production; DW-72 makes that default explicit without changing its authentication configuration. Provide owner-supported configuration and production startup proof separately.
  status: open
  implementation_2026_10_23: The publish model configures external HTTPS OIDC authority/issuer, audience, asymmetric algorithms and tenant claim. App env emission contains secret parameter references; matching sidecar annotations are model-only proof. Actual app startup/liveness and missing-input controls pass with real Dapr/Redis/FalkorDB and an explicitly operator-configured sidecar; producer validators remain intact. All 20 final controls pass while an unrelated listener occupies 50002/9090; internal gRPC is allocated and unused metrics disabled. Authenticated Dapr outbound health and app-id metadata pass, missing/wrong sidecar tokens return 401, and a stopped-sidecar control fails readiness.
  remaining_gate_2026_10_23: Actual Aspire.Hosting 13.6.3 SDK manifest emission for docker/k8s/aca produces memories-dapr as dapr.v0 without APP_API_TOKEN or DAPR_API_TOKEN bindings. Selected CommunityToolkit.Aspire.Hosting.Dapr 13.6.1-beta.935 drops sidecar environment annotations. Positive sidecar token publication is an upstream publisher/adaptor gate; no generic publisher plumbing or dependency change was added. Effective algorithm replacement also remains gated: actual producer binding retains initialized HS256 alongside configured RS256/ES256. Deployment, Parties bearer forwarding and search remain unqualified.
  verification_2026_10_23: `tests/five-publish-and-process-safety-2026-10-23/`.


- source_spec: `_bmad-output/implementation-artifacts/spec-dw-72-production-publish-environments.md`
  summary: Reconcile current dependency package documentation and source-selection receipts after the concurrent root-gitlink sync.
  evidence: After rebasing onto bec09f01aa00bd9ef83554fd099ed36fb59ab2c6, the complete service suite passes 2001/2003. MaintainedTechnologyTablesMatchCentralPackageCatalog expects docs/ci.md to describe EventStore 3.126.0 selected by the new Builds catalog, and FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph still expects Builds 332b2c144defba546fe0a09626a4a72f2340e22f while the parent selects 5d03f1db9c38db34ecac7fe6048937478ffb82cc. Both unchanged methods fail identically on the exact parent checkout; commands, results and matching test-source blob identities are retained in the DW-72 evidence. Refresh current observations without weakening gates, fabricating owner approval or recertifying historical parity evidence.
  status: done 2026-10-22
  resolution: The existing reconciliation is verified with fresh Release/package binaries at d55058ddaa4bfc94c47b799e5f5d9e9eff8dce77. MaintainedTechnologyTablesMatchCentralPackageCatalog and FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph both pass (2/2); the full service assembly passes 2421/2421. Selected EventStore 3.126.1, Builds 3fcb8f38919bb0961b06a19b57aa45623b35a33e and FrontComposer a258b992d08e73e2d3251c865ef01a982eba31ac agree with maintained observations. No dependencies, gitlinks, historical receipts or owner approvals change.
  verification: `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`; spec `spec-five-identity-routing-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-86-erasure-certificate-validation.md`
  summary: Provide an owner-supported recovery path for legacy erasure certificates.
  status: done 2026-10-23
  implementation_2026_10_23: Additive explicit RecoverKeyDestructionEvidence opt-in reuses key destruction/read-back/audit, validates scoped fresh Verified evidence and persists it before cleanup. Default retry behavior continues to refuse ambiguous evidence. No deployed recovery was executed.
  verification_2026_10_23: tests/five-recovery-integration-2026-10-23/validation.json; independent review and final publication are parent-owned.
  severity: medium
  evidence: Earlier cleanup retries overwrote key-destruction certificate status with Pending/Failed, indistinguishable from genuine key deletion failures. DW-86 must reject that ambiguous evidence; the current Parties gateway exposes no supported reissuance/persistence command. Key-management owners must qualify authoritative read-back and auditable certificate replacement before legacy retries can be certified. Never manually promote status or accept failed evidence.

  resolution_2026_10_23: Explicit opt-in regenerates fresh scoped Verified evidence through deletion/read-back/audit and persists it before cleanup; default refusal remains. Fault, malformed evidence and post-persistence cancellation/restart controls pass. No deployed recovery was executed.
  final_verification: `tests/five-recovery-integration-2026-10-23/final-verification.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-90-bounded-test-processes.md`
  summary: Contain test-process descendants that inherit output pipes after their direct parent has already exited.
  severity: medium
  evidence: Independent DW-90 review reproduced the same orphan in the baseline shared package runner and the consolidation candidate. Once the direct parent exits, Process.Kill(entireProcessTree: true) cannot identify that orphan; bounded output capture still fails promptly. The inherited-pipe regression explicitly owns and terminates its descendant. Qualify an appropriate process-group/job strategy separately before claiming orphan cleanup.
  status: done 2026-10-23
  resolution: Linux commands now run under a dedicated session/subreaper that terminates and reaps owned descendants through normal parent exit, inherited/closed streams, timeout and separate sessions. All 18 process controls pass, including unrelated-worker survival and runtime argument-parsing parity. Non-Linux platforms retain the prior bounded runner and its documented exited-parent cleanup limitation; no cross-platform containment claim is made.
  verification: `spec-five-recovery-and-ui-conformance-deferred-items.md`; `tests/five-recovery-and-ui-conformance-2026-10-23/validation.json` and `matrix-audit.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-admin-deferred-items.md`
  summary: Bind accepted edit convergence to a command-specific projection witness or expected edited state.
  severity: medium
  evidence: DW-178 deliberately closes matching-row identity visibility. An existing party already has that identity, so a healthy Current list/search response can contain its pre-edit row before the accepted update projects. The accepted receipt currently carries only party ID and authorization context, without a command/version witness. Matching identity must not be advertised as proof of the latest edit; qualify an owner-supported witness and an edit-specific stale-value regression separately.

  status: done 2026-10-22
  resolution: The circuit retains the submitted update and generated child IDs. Healthy Current authorized detail must match all replacement person/organization fields and added child IDs, types, values and preference before clearing the edit notice; list identity still reconciles creates. Receipt/detail version, submitting context, scope reset, route and disposal fences reject late work. This proves expected-state convergence, not that a specific command version projected.
  verification: `tests/five-review-runtime-convergence-deferred-items-2026-10-22/validation.json`; spec `spec-five-review-runtime-convergence-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-package-validation-deferred-items.md`
  summary: Reject malformed prerelease identifiers in the release registry-floor version index.
  severity: medium
  evidence: Independent review executes the unchanged registry-floor jq expression with 3.16.0-a..b and 3.16.0-.; both are accepted by the permissive prerelease regex and then excluded as prereleases. These invalid identifiers should make the registry evidence unprovable. The behavior predates DW-188, which qualifies stable numerical floor comparison; correct the grammar and add executable rejection fixtures in separate release-gate work.
  status: done 2026-10-21
  resolution: Require nonempty dot-separated prerelease identifiers before excluding prereleases. Full workflow-shell rejection fixtures and valid dotted/hyphenated controls pass.
  verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.


- source_spec: `_bmad-output/implementation-artifacts/spec-five-identity-routing-deferred-items.md`
  summary: Bound identity-query options acquisition and policy revalidation when an options provider is noncooperative.
  severity: medium
  evidence: Both baseline and current PartyIdentityQueryService call IOptionsMonitor.CurrentValue synchronously before creating the deadline and in PolicyIsCurrent. The admission/fold work boundary does not cover these pre-existing calls. A gated CurrentValue implementation should qualify caller completion, cancellation precedence and shared capacity before changing setup semantics.
  status: done 2026-10-22
  resolution: Both queries establish a thirty-second setup boundary before options access, then apply the configured cumulative budget from original query entry without releasing the lease. Every policy read uses bounded work; caller cancellation wins and abandoned options work retains capacity through late faults and cleanup.
  verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-identity-routing-deferred-items.md`
  summary: Include post-fold current and historical interval selection in the identity-query work budget.
  severity: medium
  evidence: Both baseline and current PartyIdentityQueryService synchronously materialize state.HumanActorBindings candidates and historical matches after the bounded fold. The following deadline checks deny late evidence but do not bound the scan itself. Qualify a gated or large-source selection with unavailable completion before releasing work; preserve exact interval semantics and cancellation precedence.
  status: done 2026-10-22
  resolution: Both interval scans now execute inside the same query deadline and work lease, with cooperative cancellation between items. Gated real-scanner tests prove bounded caller waiting and retained capacity; existing query and focused interval tests preserve ambiguity/gap outcomes, exclusive ends, custody and source positions.
  verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.


- source_spec: `_bmad-output/implementation-artifacts/spec-five-query-release-deferred-items.md`
  summary: Bind successful main-push range signoff proof to release publication.
  severity: medium
  evidence: Both the baseline and current publication workflow require successful exact-source ci.yml and a release-floor tip diff, but do not consume the independent RC workflow verdict. An unsigned intermediate pointer can fail a range check while the final signed tip passes the existing publication check. Qualify a governed exact-source range-proof integration separately; this batch preserves the approved full-CI and protected manual publication contract.
  status: done 2026-10-22
  resolution: The release caller now requires independent validated ci.yml and rc-gate.yml identities plus completed successful exact-SHA main push runs before protected publication. Executable fixtures accept both valid proofs and reject absent, stale, failed, pending, canceled, wrong-event, wrong-branch, wrong-workflow and malformed evidence. The release-floor signoff gate remains unchanged.
  verification: `tests/five-runtime-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-runtime-release-deferred-items.md`.

- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-five-query-release-deferred-items.md`
  summary: Historical identity reads still deserialize the query envelope before the deadline.
  evidence: ResolveAtAsync parses the envelope before PartyIdentityQueryDeadline exists. That order predates the options and selection budget. A bounded parse would need its own qualification.
  status: done 2026-10-22
  resolution: Historical deserialization now runs inside the configured cumulative deadline and existing shared work lease before authority admission. Gated tests prove timeout, caller-cancellation precedence, retained capacity through late completion/fault, saturation and recovery; malformed payloads fail closed and ordinary attribution outcomes remain covered. Failures before parsing return empty unparsed metadata with no evidence.
  verification: `tests/five-runtime-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-runtime-release-deferred-items.md`.

- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-five-query-release-deferred-items.md`
  summary: The first matching GitHub release tag becomes the publication version when several tags share the dispatched commit.
  evidence: maybe-false, medium if true. The selector breaks on the first non-draft tag whose peeled SHA matches. GitHub's list order for two tags on one SHA would settle whether a non-version tag can be proved.
  status: done 2026-10-22
  resolution: Publication inspects all unique non-draft canonical stable vMAJOR.MINOR.PATCH tags, skips non-version spellings and rejects multiple distinct exact-source versions. Executable fixtures prove both list orders, duplicate entries, annotated refs, deleted refs and safe outputs. Other API/object failures remain fatal.
  verification: `tests/five-runtime-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-runtime-release-deferred-items.md`.

- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-five-query-release-deferred-items.md`
  summary: Main-push workflow triggers and concurrency groups are not executed locally.
  evidence: GitHub evaluates the main push trigger and the per-SHA concurrency expression. Local fixtures run the step scripts those expressions select. A wording assertion would not execute scheduling.

  status: done 2026-10-22
  resolution: Two reviewed exact-source main push RC runs, 39820004780 and 39820014459, completed successfully. Their run lifetimes overlap twelve seconds and their actual gate jobs overlap four seconds. Logged event.before ranges bind the preceding remote main tips to both source SHAs; the RC workflow blob is unchanged. This is executed GitHub scheduling evidence, without a release dispatch or environment-approval claim.
  verification: `tests/five-review-runtime-convergence-deferred-items-2026-10-22/scheduler-proof.json`; spec `spec-five-review-runtime-convergence-deferred-items.md`.

- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-five-query-release-deferred-items.md`
  summary: The publication job's production environment is not isolated from the release job's environment assertion.
  evidence: The release job already contains environment production, so removing it from verify-publication still satisfies that assertion. GitHub is what enforces the environment rule.
  status: done 2026-10-22
  resolution: Both credential-bearing jobs are independently extracted and required to declare production. The negative control removes only verify-publication protection while release remains protected and must fail; the reciprocal mutation is also rejected. This checks declarations locally and does not claim execution of GitHub environment protection.
  verification: `tests/five-runtime-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-runtime-release-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-review-runtime-convergence-deferred-items.md`
  summary: Invalidate loaded admin form authorization across tenant/sign-in transitions and revalidate it before sending commands.
  severity: high
  evidence: Independent source review confirms the baseline CreateEditPartyPage loads authorization once, does not independently observe authentication transitions, and submits through the API client's current credentials without a pre-send comparison. Tenant A form values can therefore be sent after switching to B; an A/B/A transition can also evade a later signature-only comparison if no portal scope reset occurs. The current batch preserves that existing send behavior while qualifying accepted-receipt context, scope-reset and disposal fences. Add a stale-form/no-send regression and an authentication-transition epoch before claiming this broader form-context boundary.
  status: done 2026-10-22
  resolution: Authentication notifications and captured query-scope cancellation synchronously invalidate the form generation and cancel pending work. The form clears/reloads and fences stale loads, including A/B/A and unchanged signatures; snapshot-before-await submissions require fresh bounded authority and unchanged context/scope/generation. Full AdminPortal regression passes 315/315, including zero-send role/inactive/fault/timeout/transition/disposal cases and unchanged create/update single-send controls, while accepted-edit convergence remains covered.
  verification: `spec-five-runtime-isolation-and-form-authorization-deferred-items.md`; `tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json`. Release/package live acceptance passed with zero skips; mapped Debug test-build qualification limits are recorded separately.

- source_spec: `spec-five-runtime-isolation-and-form-authorization-deferred-items.md`
  summary: Make the referenced EventStore admin/sample UI Dapr client target identities configurable before qualifying their application API flows in concurrent suffixed sessions.
  evidence: Upstream AdminUIServiceExtensions calls AddEventStoreDaprServiceInvocation("eventstore-admin") and Sample.BlazorUI Program calls it with "eventstore"; the current owner tests qualify Parties gateway/Tenants invocation and all eight sidecar metadata endpoints, not those reference UI API flows. The frozen intent forbids upstream edits; target configurability needs an upstream change and later adoption.

  status: done 2026-10-23
  resolution: Reviewed owner main 596de60e140da779f170279a822ded010070d774 supplies validated configured targets and historical defaults. Root adoption 79768d1f2b7071db6fa49bffbfe179a43a00baa7 records it. Final actual dual-session runtime passes 1/1 with zero skips (157.014s), asserting distinct persisted counter values, own Admin UI targets, cross-session denial and survivor operation. Full Admin.UI and Sample suites pass.
  remaining_gate: Mapped Debug Integration is still blocked by the unchanged FrontComposer AssetsFile source gate. Runtime proof uses the separately built retained Release/package graph and reviewed reference hosts, not a published owner-package claim.
  verification: `tests/five-owner-config-and-recovery-deferred-items-2026-10-23/final-verification.json`; earlier original-candidate and pre-review records remain historical.
  implementation_2026_10_23: Review corrections, owner publication and root source adoption are complete. Runtime qualifies actual reference UI clients; matching packages and the separate mapped Integration build remain distinct boundaries.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-sdk-runtime-deferred-items.md`
  summary: Qualify descendant-process quiescence when a Dapr CLI wrapper exits before its runtime child.
  evidence: Unverified medium: the current fixture observes DCP executable PID/start identities and qualified listener cleanup, but the reviewer could not establish that an escaped daprd child is impossible before backing removal. A controlled producer-owned wrapper/orphan case or exact CLI/DCP descendant-stop contract would settle the claim; normal 22/22 live results alone do not establish this broader guarantee.
  status: open
  implementation_2026_10_23: Both generated mTLS launchers have executed PID-continuity proof. Early descendant observation retains exact PID/start identities across wrapper exit; controlled captured-child, separate-session, retry and unrelated-worker cases pass. An already-lost ordinary CLI wrapper now fails closed and retains backing resources.
  remaining_gate_2026_10_23: Polling cannot prove the ordinary CLI launch-to-first-observation race. Do not infer blanket orphan containment from a handshake-delayed control. Live direct-exec matrix results and any blocked modes are recorded separately from this unqualified ordinary CLI path.
  verification_2026_10_23: `tests/five-publish-and-process-safety-2026-10-23/`.


- source_spec: `_bmad-output/implementation-artifacts/spec-five-memories-recovery-deferred-items.md`
  summary: Provide and adopt producer support for refreshing changed Parties content under an unchanged canonical Memories source URI.
  severity: medium
  evidence: Selected reference CheckIdempotencyActivity falls back from a new token to the permanent source-URI record, and SaveDedupKeyActivity stores that record without expiry. Baseline indexing already used the same canonical URI. A new token cannot establish refreshed content or recreate a deleted source. This batch retains canonical provenance and rejects known-cleaned IDs; producer update/reindex support and consumer adoption need separate qualification.
  status: open

  implementation_2026_10_24: Mapped-source Parties calls explicit revision-aware Memories refresh. Native Redis visibility predicates precede TopK/paging, existing schemas gain an idempotent optional field, and graph source pointers remain monotonic. Both same-predecessor completion orders, stable retry/body binding, predecessor conflict, delayed superseded compensation, restart, exact-predecessor recreation and erasure replay fences are exercised through real adapters. Superseded consumer attempts retain canonical provenance and a durable successor link.
  remaining_gate_2026_10_24: The owned authenticated selected-source runtime now verifies a different canonical output and current changed content at the same URI. Explicit producer acknowledgement rejects older servers that ignore refresh fields, retaining uncertain admission across recreation. Unchanged catalog packages retain ordinary ingest defaults; matching Memories/EventStore publication/adoption and deployed qualification remain separate. No claim of refreshed content is made from a new token alone.
  verification_2026_10_24: `tests/five-memories-inventory-and-publication-2026-10-24/`.
  review_loop_1_2026_10_24: Canonical revision is required before reservation/submission. Lost-ack recovery requires exact tenant/source/authority/token plus durable explicit-refresh protocol/revision/body binding and independent terminal success. Redis publication retains durable graph repair authority; graph reads reject overlapping publication, and retired neighbor metadata is filtered. Both revision orders, actual semantic caller, graph denial/restart repair and alias/deletion convergence pass; source/package/deployed boundaries remain separate. Final evidence: `tests/five-memories-inventory-and-publication-2026-10-24/review1-verification.json`.
  review_loop_2_2026_10_24: Consumer reservations retain the original protocol/revision/body digest before admission. Canonical JSON redelivery recovers that persisted identity without resubmission or unproved legacy adoption; a proven unsubmitted successor binds its own actual body. Modern refresh uses bounded key provenance, incomplete legacy registration is rediscovered natively before sealing, and actual NL TopK/current publication controls remain required. Final evidence is retained separately in the loop2 receipt.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-memories-recovery-deferred-items.md`
  summary: Qualify whether ingestion-inventory persistence needs a separate health capability probe.
  severity: medium
  evidence: Original unverified finding: mapping and ingestion inventories use the same configured SDK backend, but only mapping sentinel persistence is probed. A real backend key-prefix permission or serializer configuration that permits mapping operations while rejecting ingestion-inventory writes would establish the claimed additional gap; no such supported deployment counterexample was demonstrated. Keep this claim unverified until that condition is reproduced.

  status: done 2026-10-23
  resolution: A password-authenticated non-default Redis ACL user with mapping-prefix permission and denied ingestion-prefix permission reproduces the gap through the actual C# Dapr SDK and health check. The new actual-shape inventory sentinel reports Degraded in enabled and disabled modes; granting the ingestion prefix repairs both. Exact conditional cleanup, independent cancellation budget, primary-failure retention and actual sentinel absence are qualified. Both owned runtime processes are removed.
  remaining_gate: This capability probe does not establish authoritative remote inventory, existing-workflow authorization or physical erasure.
  verification: `tests/five-owner-config-and-recovery-deferred-items-2026-10-23/final-verification.json`; earlier original-candidate and pre-review records remain historical.
  implementation_2026_10_23: Review corrections, actual 3.127.0 SDK ACL health control, reserved-sentinel absence and owned cleanup pass. One-minute TTL, missing-capability preflight and lost-response/foreign-state tests pass; remote inventory and physical-erasure debts remain separate.
  review_correction_2026_10_23: Probe writes now require both conditional erase and expiring-store capabilities before mutation and use one-minute SDK TTL through the actual registration CAS. Normal ingestion remains durable; foreign bytes and primary failures remain protected.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-memories-recovery-deferred-items.md`
  summary: Qualify workflow-status retention and opportunistic reconciliation of superseded acknowledged ingestion attempts.
  severity: medium-unverified
  evidence: Older pending attempts remain durably discoverable and are reconciled by cleanup; current-revision delivery does not poll every older workflow. The selected source search found no automatic workflow purge or status-expiry policy. Demonstrate the producer retention/purge contract and an expiry-before-cleanup case before requiring additional scheduling; expired or unavailable status must continue to block uncertain cleanup.
  status: done 2026-10-23
  resolution: The selected Memories source contract retains completed workflow history until explicit purge and adds no automatic purge feature. Serialized restart tests retain superseded acknowledged attempts while newer work progresses, reconcile terminal output at cleanup, and retain uncertainty under missing, hypothetical expired and unavailable status. No scheduling defect was demonstrated under that contract and no production scheduler was added. This qualifies the contract/consumer boundary, not deployed retention or physical erasure.
  verification: `spec-five-recovery-and-ui-conformance-deferred-items.md`; `tests/five-recovery-and-ui-conformance-2026-10-23/validation.json` and `matrix-audit.json`.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-memories-recovery-deferred-items.md`
  summary: Reconstruct complete searchable contact and identifier content for a first successful ingestion after failed submission and canonical redelivery.
  severity: medium
  evidence: Baseline 6c83309d08a10f36a0da866521b9d0da6f8fc998 already marks SearchableContactChannels and SearchableIdentifiers JsonIgnore and BuildReconciliationFold returns serialized canonical state. A first rejected/failed submission followed by restart can therefore submit a summary without those transient collections. Reconstruct only from canonical-matching replay or another authorized complete source; never publish stale content from an older delivery. This pre-existing projection-content requirement is separate from durable ingestion identity and cleanup provenance.
  status: done 2026-10-23
  resolution: Live delivery and first-success retry reconstruct ignored contact/identifier content only from a complete, contiguous canonical-matching replay. Stale or partial sources remain retryable. New internal digests bind complete content and are removed at erasure; legacy rows have the documented weaker witness. Real adapters, serialized restart, rejected first submission, same-checkpoint altered content, newer writes and erasure controls pass. Existing shared-rebuild completion source limitations and unchanged-URI producer refresh remain separate.
  verification: `spec-five-recovery-and-ui-conformance-deferred-items.md`; `tests/five-recovery-and-ui-conformance-2026-10-23/validation.json` and `matrix-audit.json`.


- source_spec: `_bmad-output/implementation-artifacts/spec-five-recovery-and-ui-conformance-deferred-items.md`
  summary: Qualify package-runner cleanup after external interruption of the runner or its dedicated Linux supervisor.
  evidence: Normal exit, early failure and timeout use owned cleanup; external SIGTERM can bypass Python finally and external caller termination also left descendants alive in the baseline runner. Add cancellation/signal and parent-disappearance controls before claiming interruption containment; SIGKILL remains outside cooperative cleanup.
  status: done 2026-10-23
  implementation_2026_10_23: Cooperative SIGTERM/SIGINT handlers are installed before launch; explicit caller PID/start identity is captured before Python starts and checked before/during work. Signal-during-work/cleanup, isolated caller death, inherited pipes, descendant reaping and unrelated-worker controls pass within bounds. The supervisor retains one cleanup deadline.
  resolution_2026_10_23: Independent review fixes remove filesystem bookkeeping from cleanup correctness and inject a deterministic signal during actual cleanup. All 26 process controls pass, including caller loss, missing state directory, inherited pipes and unrelated-worker survival. Changes are committed and published on main. Direct supervisor SIGKILL remains outside cooperative cleanup.
  verification_2026_10_23: `tests/five-publish-and-process-safety-2026-10-23/`.


- source_spec: `spec-five-publish-and-process-safety-deferred-items.md`
  summary: Replace initialized Memories algorithm defaults when explicit JWT algorithms are configured before qualifying an asymmetric-only policy.
  evidence: Independent blind and edge review probes against the selected producer options with the .NET 10 binder produce HS256,RS256,ES256,RS256,ES256 after RS256/ES256 configuration. The initialized collection and binding contract predate this caller change; app environment entries do not establish effective options. Require owner-supported replacement semantics and effective JwtBearerOptions verification; the Memories publication entry remains open.

  status: done 2026-10-26
  resolution: Owner registration replaces explicitly configured algorithm lists while retaining BindConfiguration reload notifications, absent defaults, programmatic initialization and the public init-only setter signature. Effective named JwtBearerOptions and real signed RS256/ES256 acceptance with HS256 rejection pass; blank and empty JSON lists fail startup.
  historical_remaining_gate_2026_10_23: Matching Memories package publication/adoption and the separate Dapr sidecar secret-token publisher gate remain open.
  verification: `tests/five-owner-config-and-recovery-deferred-items-2026-10-23/final-verification.json`; earlier original-candidate and pre-review records remain historical.
  implementation_2026_10_23: Reviewed server source is published in Memories main 4190ec6549dd83bcaaf268e7bb723dcff8f7286b and adopted at root 79768d1f2b7071db6fa49bffbfe179a43a00baa7. Eight registered authentication controls pass; matching package and independent publisher gates remain open.
  review_correction_2026_10_23: Domain-option reload notifications remain, but the named bearer policy retains its startup selection and requires restart for policy changes. Registered programmatic configuration is verified through public initialization and the registered options callbacks; MCP binding remains deferred separately.

  resolution_2026_10_26: The actual AppHost resolves non-packable Memories Server by project path at selected 39be000902254c4717df6726bfcc3ea927b26b70. Eight registered Server controls pass without skips: effective selected algorithms replace defaults, signed RS256/ES256 pass, HS256 fails and empty/blank lists fail startup. The actual registration slice and policy/test/factory sources are byte-identical to released 2.29.15 source 38128624763c313238904c30aaeb88eb4fd9a6d0. This explicit project-host/released-source proof supersedes the stale client-package-only prerequisite. The separate sidecar token publisher entry, container/deployment and refresh/inventory gates remain open; no client upgrade or producer change was needed.
  verification_2026_10_26: `tests/five-published-sdk-and-runtime-closure-2026-10-26/`

- source_spec: `spec-five-publish-and-process-safety-deferred-items.md`
  summary: Deliver supported Dapr sidecar secret-token emission through the selected SDK/target publisher before qualifying Memories publish authentication.
  status: open
  evidence: Actual SDK CLI manifests for docker/k8s/aca contain both Memories app secret references but no env/token bindings on memories-dapr. Selected Toolkit 13.6.1-beta.935 custom dapr.v0 callback and SDK 13.6.3 outer writer do not serialize these annotations. The omission predates the caller configuration and also affects existing generic helpers. App-only startup with an operator-configured sidecar is separate evidence; require owner-supported publisher delivery and full positive app/sidecar manifest proof.

- source_spec: `spec-five-owner-config-and-recovery-deferred-items.md`
  summary: Make Memories MCP explicit JWT algorithm lists replace initialized defaults through registered configuration.
  status: done 2026-10-23
  implementation_2026_10_23: Registered configuration now replaces explicit algorithm lists while preserving the public init setter, absent defaults and configuration notifications. Registered token/HTTP controls cover asymmetric acceptance, HS256 refusal and invalid lists; publication/adoption and deployment remain separate.
  verification_2026_10_23: tests/five-recovery-integration-2026-10-23/validation.json; independent review and final publication are parent-owned.
  evidence: The unchanged MCP composition root binds Authentication:JwtBearer while options initialize HS256/RS256/ES256. A reviewer probe of the actual source and registered binding produced HS256,RS256,ES256,RS256,ES256 for explicit RS256/ES256; deployment selects that asymmetric list. Existing MCP options tests bypass binding. Verify exact registered named JwtBearerOptions and signed-token rejection when fixing this separate ingress.

  resolution_2026_10_23: Reviewed Memories 5a852389327bf45df7d59b2c2e29c7adbe353f19 is published and adopted at a9f4edb5a8a769e36499063b5b88413af01a7bd0. Registered HTTP/token/default/invalid/reload controls pass in the full 120-case suite; deployment remains external.
  final_verification: `tests/five-recovery-integration-2026-10-23/final-verification.json`.


### DW-196: Fence legacy natural-language migration and erase both NL namespaces

status: done 2026-10-25
origin: review-loop-3
severity: medium

- source_spec: `_bmad-output/implementation-artifacts/spec-five-memories-inventory-and-publication-deferred-items.md`
  summary: Fence legacy natural-language migration and erase both NL namespaces.
  evidence: Pre-existing canonical-only deletion and the unfenced RedisNaturalLanguageNamespaceMigrator can retain or recreate legacy NL rows; verify the actual migrator, both namespaces, deletion races, restart and unrelated survivors. No production legacy migration is authorized by this batch.

implementation_2026_10_25: Atomic unit/source-fenced legacy copy/retirement and both-namespace deletion are implemented in Memories. Real native RedisSearch/FalkorDB race, recreation and survivor controls pass; final owner review/publication is pending.
verification_2026_10_25: `tests/five-processing-and-memories-erasure-2026-10-25/`; source receipt remains separate from package/deployment qualification.


review1_2026_10_25: Review-1 migration rejects incomplete canonical targets with an explicit owner-reconciliation result while retaining valid legacy content. The real adapter lane passes 69 controls with no skips; production migration remains unexecuted. Final executed evidence: `tests/five-processing-and-memories-erasure-2026-10-25/review1-verification.json`.

resolution_2026_10_25_final: Memories 7b0c8b5692600e293346eea7a84652eb87e4c346 publishes the reviewed atomic legacy migration and both-namespace erasure fixes. Actual persisted-state race/restart/survivor controls pass. Historical orphan qualification and production migration remain external.
verification_final: `tests/five-processing-and-memories-erasure-2026-10-25/`; spec `spec-five-processing-and-memories-erasure-deferred-items.md`.


### DW-197: Qualify erasure of natural-language retry payloads and delayed retry work

status: done 2026-10-25
origin: review-loop-3
severity: medium

- source_spec: `_bmad-output/implementation-artifacts/spec-five-memories-inventory-and-publication-deferred-items.md`
  summary: Qualify erasure of natural-language retry payloads and delayed retry work.
  evidence: Pre-existing live/dead NL retry registries retain queued raw JSON outside the workflow claim-check ledger; qualify actual registry/worker payload erasure and delayed retries across restart. No production legacy migration is authorized by this batch.

implementation_2026_10_25: Retry admission/requeue/overflow and exact-unit modern/legacy live/dead purge are fenced. New workflow inputs contain no raw content; an actual Dapr legacy raw-input history purge and worker recreation control passes with physical Redis key absence. Missing workflow cleanup capability fails explicitly. New IDs preserve tenant/unit boundaries; stored scope is checked before mutation. Matching legacy history fails closed until the owner explicitly qualifies scheduler quiescence, while foreign or ambiguous scope is never mutated. Final owner review/publication is pending.
verification_2026_10_25: `tests/five-processing-and-memories-erasure-2026-10-25/`; source receipt remains separate from package/deployment qualification.


review1_2026_10_25: Review-1 restores atomically fenced JSON-only live retry processing, canonical-unit guards before work/publication, valid-overflow dead caps and persisted successful NL output. Maintained IntegrationSlow controls execute actual content-bearing legacy activity history, purge, late completion and recreation without endpoint skips. Default legacy scheduler-quiescence refusal remains enforced. Final executed evidence: `tests/five-processing-and-memories-erasure-2026-10-25/review1-verification.json`.

resolution_2026_10_25_final: Memories 7b0c8b5692600e293346eea7a84652eb87e4c346 publishes scoped live/dead payload erasure and content-free retry execution. Real unit/case workflow-history, late completion, derived-output and replacement controls pass. Legacy scheduler qualification, tenant-wide cleanup and separately recorded stale retry races remain external.
verification_final: `tests/five-processing-and-memories-erasure-2026-10-25/`; spec `spec-five-processing-and-memories-erasure-deferred-items.md`.


### DW-198: Bound legacy semantic deletion discovery without full Redis keyspace scans

status: done 2026-10-25
origin: review-loop-3
severity: medium

- source_spec: `_bmad-output/implementation-artifacts/spec-five-memories-inventory-and-publication-deferred-items.md`
  summary: Bound legacy semantic deletion discovery without full Redis keyspace scans.
  evidence: FindSemanticKeysAsync retains its pre-existing complete KeysAsync scan; verify bounded exact-unit discovery or explicit owner reconciliation with sparse databases, exact scope and cancellation. No production legacy migration is authorized by this batch.

implementation_2026_10_25: Both deletion paths now share bounded exact-key/native-index discovery with scoped key and index coverage validation. Missing legacy coverage requires explicit reconciliation. Sparse-keyspace/cancellation controls pass; broader refresh compatibility verification and owner review/publication remain pending.
verification_2026_10_25: `tests/five-processing-and-memories-erasure-2026-10-25/`; source receipt remains separate from package/deployment qualification.

review1_2026_10_25: Review-1 native TAG queries retain discovered obligations without certifying historical coverage. Missing-TAG and unknown legacy coverage require explicit qualified owner reconciliation; old automatic legacy-complete markers are rejected. Modern exact provenance remains supported. Synthetic fixture owners reconcile only their own known inventories; this grants no production completeness. Final executed evidence: `tests/five-processing-and-memories-erasure-2026-10-25/review1-verification.json`.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Qualify deterministic replay of the original content-bearing NL retry workflow under upgraded workers (medium, unverified).
  evidence: Review R2-B5; suspend an actual original workflow in description or embedding, upgrade the registered worker, and verify terminal/retry behavior plus retained-history erasure. The new worker path is content-free; the replay defect itself has not been reproduced.
  status: done 2026-10-27
  resolution: Qualified the original cdd73d7361f67781a523f63f04853491a687f426 protocol under the production workflow name. Actual authenticated Dapr workers stop with description or embedding blocked, upgrade, resume to the bounded legacy restart result without reexecuting content activities, run the modern scheduler through fenced NL publication and purge historical input across another restart. No incompatibility reproduced at these boundaries; production retry behavior remains unchanged. Unknown deployed histories and legacy scheduler-drain attestation remain unqualified.
  verification: `spec-five-legacy-recovery-and-evidence-deferred-items.md`; `tests/five-legacy-recovery-and-evidence-2026-10-27/`. Source safety qualification and evidence preservation remain separate from publication/adoption, deployment and owner acceptance.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Tenant deletion must purge existing live/dead NL retry payloads and scoped workflow histories (high, pre-existing).
  evidence: Review R2-B7; approved Memories baseline cdd73d7 DeleteTenantDataKeysActivity already omits retry queues/payload hashes and RetryWorkflowErasure. Verify physical absence and unrelated-tenant survival through the tenant workflow.
  status: done 2026-10-26
  resolution: Tenant cleanup now installs the permanent fence, retains bounded content-free history discovery before scheduling/removal, preserves per-attempt scheduling uncertainty, reconciles live/dead payloads and purges independently scoped running/completed Dapr histories. Actual authenticated Dapr/Redis tests prove physical absence, restart and unrelated survival. Unknown historical coverage, uncertain scheduling and unqualified legacy scheduler drain still block cleanup; no production reconciliation is attested.
  verification: `spec-five-nl-retry-safety-deferred-items.md`; `tests/five-nl-retry-safety-2026-10-26/final-verification.json` and `final-executed-tests.json` (125 unit/indexing and 90 actual Redis/authenticated Dapr controls, zero targeted skips). Source qualification is separate from package publication/adoption and deployment.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Qualify malformed historical NL retry payload addressing (medium, pre-existing).
  evidence: Review R3-B4; Baseline cdd73d7 accepts payload MemoryUnitId differing from its hash field/member; current writers use exact addressing. Corrupt or older malformed layouts need explicit reconciliation and erasure qualification.
  status: done 2026-10-26
  resolution: Modern queue/hash addresses must exactly match parsed tenant and unit before retry, overflow or erasure. Misaddressed/foreign records cannot mutate another unit; malformed pruning retains a content-free unresolved-history obligation. Actual persisted-state controls exercise both tenant and unit mismatches and preserve unrelated bytes.
  verification: `spec-five-nl-retry-safety-deferred-items.md`; `tests/five-nl-retry-safety-2026-10-26/final-verification.json` and `final-executed-tests.json` (125 unit/indexing and 90 actual Redis/authenticated Dapr controls, zero targeted skips). Source qualification is separate from package publication/adoption and deployment.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Reconcile pre-tombstone orphan legacy NL hashes before namespace activation (medium, pre-existing).
  evidence: Review R3-B5; Baseline migration copies valid legacy hashes without syntactic unit existence. Current tombstones fence known erasures; no qualification of unknown historical orphan owners is inferred.
  status: done 2026-10-27
  resolution: Namespace migration checks exact live syntactic id, tenant, case and nonempty observed source URI in its final atomic script before shared key ownership enrollment. Missing, foreign and concurrently removed owners cause a bounded reconciliation refusal without activating or retiring content. Actual Redis tests preserve orphan and unrelated bytes across restart; explicit synthetic owner repair then permits migration. No production data was migrated and unknown historical completeness remains unqualified.
  verification: `spec-five-legacy-recovery-and-evidence-deferred-items.md`; `tests/five-legacy-recovery-and-evidence-2026-10-27/`. Source safety qualification and evidence preservation remain separate from publication/adoption, deployment and owner acceptance.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Correct named-argument casing in the concurrent identity-history test helper (medium, external work).
  evidence: Review R3-E4; The untracked tests/Hexalith.Parties.IntegrationTests/Identity/TestOnlyIdentityHistoryCustody.cs uses pending/destroyed/acknowledged against UnitStatus Pending/Destroyed/Acknowledged. Excluded from this build and preserved for its active owner.

  status: done 2026-10-28
  resolution: Already corrected in tracked main at `17290ed694801d3537a62b04b47c10ebab6e775d`: UnitStatus construction uses positional arguments. A fresh actual IntegrationTests Debug build and the persisted identity verifier controls qualify this bounded compiler/helper report; no custody backend or Live acceptance is implied.
  verification: `tests/five-reconciliation-and-migration-2026-10-28/`; `spec-five-reconciliation-and-migration-deferred-items.md`.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Make corrupt retry pruning and backlog trimming conditional on the selected payload or score (medium, pre-existing).
  evidence: Review R4-B10; baseline cdd73d7 removes selected queue members/hash fields without checking the observed bytes or score, so a concurrently replaced valid payload can be deleted. Qualify interleaved replacement through both pruning and trim paths; atomic type preflight does not settle selection freshness.
  status: done 2026-10-26
  resolution: Live corruption pruning, overflow and dead trimming compare the observed member, payload presence/bytes and score in the final atomic Redis mutation. Actual registry command-boundary interleavings preserve replacements in all three maintenance paths; bounded expansions and ordinary queue caps remain covered.
  verification: `spec-five-nl-retry-safety-deferred-items.md`; `tests/five-nl-retry-safety-2026-10-26/final-verification.json` and `final-executed-tests.json` (125 unit/indexing and 90 actual Redis/authenticated Dapr controls, zero targeted skips). Source qualification is separate from package publication/adoption and deployment.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Retire obsolete legacy retry aliases after modern-first completion using qualified lineage (medium, pre-existing).
  evidence: Review R5-B1; baseline cdd73d7 Complete removes the unit and exact completed JSON, leaving a different legacy JSON alias available to the old raw-input workflow. Qualify equal-score modern-first ordering and preserve genuinely newer work; current erasure fences still refuse erased units.
  status: done 2026-10-26
  resolution: Completion retires inspected aliases only when immutable admission lineage matches, while comparing their final observed score/payload. A content-free completion marker blocks obsolete legacy promotion across restart. Distinct/ambiguous lineage is retained for reconciliation, and modern work still progresses with batch size one.
  verification: `spec-five-nl-retry-safety-deferred-items.md`; `tests/five-nl-retry-safety-2026-10-26/final-verification.json` and `final-executed-tests.json` (125 unit/indexing and 90 actual Redis/authenticated Dapr controls, zero targeted skips). Source qualification is separate from package publication/adoption and deployment.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Bind final natural-language retry publication atomically to the current retry payload or version (medium, pre-existing).
  evidence: Review R5-B3; baseline cdd73d7 publishes generated input after asynchronous checks without a retry-version publication fence. Current exact-record checks protect generation boundaries but leave a final check/write race. Qualify replacement during index readiness and preserve newer output and queued work.
  status: done 2026-10-26
  resolution: Generated NL output compares the exact expected retry payload and live unit inside the final atomic publication operation, after readiness awaits. Replaced/erased attempts return content-free outcomes while preserving newer output and queue state. Scoped workflow version binding and verified terminal retirement retain replacement progress.
  verification: `spec-five-nl-retry-safety-deferred-items.md`; `tests/five-nl-retry-safety-2026-10-26/final-verification.json` and `final-executed-tests.json` (125 unit/indexing and 90 actual Redis/authenticated Dapr controls, zero targeted skips). Source qualification is separate from package publication/adoption and deployment.

- source_spec: `spec-five-processing-and-memories-erasure-deferred-items.md`
  summary: Preserve retained failed audit facts beyond the processing checkpoint during shorter rebuilds (high, pre-existing).
  evidence: Review R5-B4; baseline 1ef274e PrepareRebuildAsync replaces Processing using the requested replay without checking either checkpoint or retained audit boundaries. Qualify unresolved retained facts through sequence 100 followed by a resolved rebuild through 50, for both inline and bucket storage, without losing retained facts or inventing replay completeness.
  status: done 2026-10-27
  resolution: Pure rebuild preparation protects the last retained inline or bucket audit sequence independently of both checkpoints. Checkpoint 50 with failed facts through 100 refuses replay through 50 before staging/publication with unchanged persisted bytes. Full resolved replay repairs the facts and advances coordinated checkpoints; a concurrent failed fact 101 wins the ETag race in both storage modes.
  verification: `spec-five-legacy-recovery-and-evidence-deferred-items.md`; `tests/five-legacy-recovery-and-evidence-2026-10-27/`. Source safety qualification and evidence preservation remain separate from publication/adoption, deployment and owner acceptance.

resolution_2026_10_25_final: Memories 7b0c8b5692600e293346eea7a84652eb87e4c346 publishes bounded exact-key discovery and ownership checks. Missing TAG/unknown coverage remains an explicit reconciliation requirement; no production completeness or attestation is inferred.
verification_final: `tests/five-processing-and-memories-erasure-2026-10-25/`; spec `spec-five-processing-and-memories-erasure-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-parties-1-owner-delivery.md`
  summary: Retain the EXT-PARTIES-1 review-triage record outside the owner-delivery spec.
  evidence: Token split from the stopped owner-delivery spec. Findings BH-01–BH-10, EC-01, VG-01, and VG-02 stay in `_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery/review-triage.json`. This split does not authorize Identity test edits, a custody backend, or Live.
  status: done 2026-10-27
  resolution: The existing separate resume-recovery archive retains all 13 finding IDs and dispositions, its reviewed diff and 14 source snapshots. A reproducible read-only check verifies their recorded hashes against the existing 71-file manifest without historical temporary files. This reconciles evidence retention only; stopped owner-delivery intent and approval gates remain unchanged.
  verification: `spec-five-legacy-recovery-and-evidence-deferred-items.md`; `tests/five-legacy-recovery-and-evidence-2026-10-27/`. Source safety qualification and evidence preservation remain separate from publication/adoption, deployment and owner acceptance.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-parties-1-owner-delivery.md`
  summary: Retain the EXT-PARTIES-1 Local and Live verification history outside the owner-delivery spec.
  evidence: Token split from the stopped owner-delivery spec. Command results, source snapshots, and hashes stay in `_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery/`. The historical Local exit 1 is not Available. This split does not execute Live.
  status: done 2026-10-27
  resolution: The existing separate resume-recovery archive retains all 17 original historical receipt hashes and exact bytes, including the original 446-pass/4-fail Server XML, Local exit 1 and both Live refusals. Read-only checks confirm accepted, available and liveQualified remain false and installed hashes/date remain null. Neither EXT Local nor Live was executed; no owner delivery or production custody is qualified.
  verification: `spec-five-legacy-recovery-and-evidence-deferred-items.md`; `tests/five-legacy-recovery-and-evidence-2026-10-27/`. Source safety qualification and evidence preservation remain separate from publication/adoption, deployment and owner acceptance.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-frontcomposer-ui-deferred-items.md`
  summary: Reconciliation can miss a confirmation delivered before subscription registration.
  evidence: Both retained Parties ReconcileAsync and the extracted producer subscribe after IssueCommand; a fast projection confirmation then produces no read until another signal.

  status: done 2026-10-28
  resolution: Parties and FrontComposer now establish transport readiness before dispatch, buffer confirmations until acceptance and drain registration on rejection/cancellation. Delayed registration, an only-early confirmation, no-confirm old-Current, rejection and cancellation controls pass in retained/mapped/producer lanes. Source implementation is complete; matching FrontComposer package publication/adoption and G4 acceptance remain separate open gates.
  verification: `tests/five-reconciliation-and-migration-2026-10-28/`; `spec-five-reconciliation-and-migration-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-frontcomposer-ui-deferred-items.md`
  summary: Reconciliation does not re-enter polling after a connected subscription later disconnects.
  evidence: Both retained Parties ReconcileAsync and the extracted producer inspect connectivity once; later disconnect leaves completion waiting until cancellation or another confirmation.

  status: done 2026-10-28
  resolution: The operation keeps connectivity observation active through reconnects, serializes re-reads and drains polling/timers/subscriptions at completion or cancellation. Repeated disconnect/reconnect controls pass in retained, mapped and producer lanes. The producer callback contract and Parties driver now require observation for the full operation lifetime. Matching package publication/adoption and deployed qualification remain separate.
  verification: `tests/five-reconciliation-and-migration-2026-10-28/`; `spec-five-reconciliation-and-migration-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-reconciliation-and-migration-deferred-items.md`
  summary: Correlate projection confirmations and Current reads with the accepted command or aggregate checkpoint.
  evidence: Review BH-01: existing tenant/projection-only notifications can originate from another party while a queried old model reports Current. Qualify a command/version witness through the transport and query callback before claiming per-command causal confirmation; the new readiness buffer preserves the existing signal semantics.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-reconciliation-and-migration-deferred-items.md`
  summary: Observe brief disconnect/reconnect transitions and recover confirmations lost entirely between polling ticks.
  evidence: Review BH-02/EC-01: existing IsConnected-only interval sampling sees neither edge when an outage fits between ticks. Add an owner-supported transition/catch-up seam and deterministic sub-interval outage proof; long recurring-disconnect sampling is qualified separately.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-reconciliation-and-migration-deferred-items.md`
  summary: Retry a stale or failed reconciliation read after its sole observed confirmation while the transport stays connected.
  evidence: Review BH-03: baseline and current connected paths retry only on another confirmation; a lone stale/error read can remain pending after the model catches up. Qualify bounded retry semantics without mistaking unrelated old Current data for the accepted command.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-reconciliation-and-migration-deferred-items.md`
  summary: Provide cancellation-aware projection subscription and removal before claiming bounded teardown under stalled transport RPCs.
  evidence: Review BH-08: the existing IProjectionStream/EventStoreSignalRClient SubscribeAsync and UnsubscribeAsync surfaces provide no cancellation token, and the producer JoinGroup/LeaveGroup invocations are uncancellable. Current owned draining waits for them; synthetic delayed tests release them explicitly. Require owner-supported cancellation with no late callback registration and an actual stalled-RPC control before claiming bounded operation/circuit shutdown.
