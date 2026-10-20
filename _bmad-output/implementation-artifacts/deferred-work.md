# Deferred Work

### DW-1: Follow-up review still recommended for 8-2-identifier-correctness-and-zero-risk-hygiene after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-072046-c4fb; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: open

### DW-2: Follow-up review still recommended for 8-3-platform-api-prerequisites after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-8-3-platform-api-prerequisites.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-072046-c4fb; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: open

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

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
location: EventStore owner AppHost publish output
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
reason: This was split because owner-AppHost adoption and publish-output validation are independently shippable after the reusable JWT composition API exists.
status: open
decision: 2026-09-06 Authorize owner adoption — Adopt the helper in the owner AppHost and inspect publish output, failing on secret or poison values.

### DW-14: Validate G8-A delivery receipt integrity

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
location: G8-A owner-delivery receipt and Parties fitness tests
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
reason: The review confirmed that current Parties fitness tests parse only the marked matrix table and do not bind the supplemental receipt SHA, claimed four-file inventory, or focused EventStore test lane to the referenced Git objects.
status: open

### DW-15: Bind the operational-index ACL route, verb, policy, and action

status: done 2026-09-06
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
archived: 2026-10-13

### DW-16: Reconcile BMAD branching guidance with default-main policy

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md"), 2026-09-06
location: persistent BMAD project context and Hexalith Git guidance
source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
reason: The pre-existing project context still requires a typed branch and PR, while the authoritative Hexalith Git instructions say to work on `main` by default and branch only when genuinely required.
status: open

### DW-17: Pin the Node and npm runtime in release workflows

origin: migrated from legacy ledger (flat source_spec "/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md"), 2026-09-06
location: .github/workflows CI and release Node setup
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
reason: CI and release workflows use floating `lts/*`; changing this is pre-existing policy and the approved spec explicitly requires approval for Node engine policy changes.
status: open
decision: 2026-09-06 Pin Node 24 — Pin the tested Node 24 major and a repository-controlled npm version in CI and release workflows.

### DW-18: Fail semantic-release closed to the local binary

status: done 2026-09-07
origin: migrated from legacy ledger (flat source_spec "/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md"), 2026-09-06
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
archived: 2026-10-13

### DW-19: Hoist multi-token search candidate normalization

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
location: EvaluateEntry multi-token search hot path
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
reason: `EvaluateEntry` rebuilds the query-only full phrase and candidate collection for every party, creating O(entries) allocations in the 10K hot path despite the current performance gate passing.
status: done 2026-10-17
resolution: Both local fuzzy and semantic search prepare the normalized full phrase and candidate array once per query, preserving matching, original token-count scoring, metadata, ordering and paging. Independent regressions detect per-entry phrase and candidate-array copies. Longer-query allocation growth at 10K entries falls from 2,400,728 bytes to 968 bytes in both providers. Final Debug/source build has zero warnings/errors; focused search/performance tests pass 80/80 and full service assembly passes 1098/1098 without failures/errors/skips. Four review verification findings are patched; no new deferred work. DW-20 remains open. See spec-dw-19-hoist-search-candidates.md and tests/dw-19-hoist-search-candidates-2026-10-17/validation.json.

### DW-20: Normalize full-phrase multi-token relevance coverage

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
location: multi-token relevance scoring
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
reason: A deterministic full-phrase match is added alongside real query tokens, so coverage can exceed one before the final score is clamped and can inflate ordering relative to token-only matches.
status: done 2026-10-17
resolution: Both local search providers exclude the synthetic full phrase from real-token coverage while preserving field matching, weights, metadata, duplicate-token semantics and once-per-query preparation. Corrected relevance and page ordering are covered by 29 direct cases. Final Debug/source build has zero warnings/errors; focused search/performance tests pass 109/109 and full service assembly passes 1127/1127 without failures/errors/skips. All four independent review findings and follow-up are resolved; no new deferred work. See spec-dw-20-normalize-relevance-coverage.md and tests/dw-20-normalize-relevance-coverage-2026-10-17/validation.json. The separate Aspire baseline healthy wait timed out with security Unhealthy; full-topology qualification is not claimed.

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

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: s_caseIdMissingWarned
reason: `s_caseIdMissingWarned` is an unbounded static ConcurrentDictionary (one entry per tenant/party for process lifetime) — mirrors retired orchestrator pattern.
status: done 2026-10-17
resolution: Replaced process-lifetime warning identities with singleton-local FIFO tracking capped at 1024 exact tenant/party pairs. Atomic admission bounds both retained identities and eviction bookkeeping; repeated retained pairs suppress warnings, and evicted pairs may warn again. Convergence/no-ingest, configuration recovery, privacy and persisted-case cleanup are covered. Final Debug/source build has zero warnings/errors; focused indexer tests pass 21/21 and full service assembly passes 1080/1080 without failures/errors/skips. Five review verification improvements and independent follow-up are complete; no new deferred work. See spec-dw-24-bounded-missing-case-warning.md and tests/dw-24-bounded-missing-case-warning-2026-10-17/validation.json. The separate 20-second Aspire baseline healthy wait timed out; topology qualification is not claimed.

### DW-25: Document Party index ProjectionVersion semantics

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: Party index projection fold and ProjectionVersion
reason: Index `ProjectionVersion` scheme (`global:N` / `{id}:{seq}` / keep-current) lacks Fold/class remarks for freshness/query consumers.
status: done 2026-10-18
resolution: Documented the existing version selection and validation rules at the SDK handler/fold, read-model property and data-model guide. Consumer guidance covers duplicate persistence, rebuilds, cleanup, strict parsing, the legacy global-identifier ambiguity and the limits of source-position and timestamp freshness signals. Executable C# is unchanged. Final Debug/source build has zero warnings/errors; focused existing SDK projection tests pass 89/89 without failures/errors/skips. All four independent review findings are corrected and follow-up verified; no new deferred work. See spec-dw-25-index-projection-version.md and tests/dw-25-index-projection-version-2026-10-18/validation.json.

### DW-26: Define stable Article 30 operation categories

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: GetOperationCategory
reason: `GetOperationCategory` default arm returns a short event-type name rather than a stable category vocabulary — Art.30 taxonomy design choice.
status: done 2026-10-17
decision: 2026-09-06 Use stable Other — Map unknown events to a stable Other category and retain EventType separately for bounded diagnostics.
resolution: Uncategorized processing records now use Other while retaining EventType and existing outcome/checkpoint behavior. Established categories, decodable redacted payloads and legacy persisted categories are preserved; serialized SDK rebuild output normalizes legacy categories. Debug source-reference builds pass with zero warnings/errors; focused tests pass 13/13 and full Projections.Tests passes 259/259 without failures/errors/skips. All four independent review findings are addressed; no new deferred work. See spec-dw-26-stable-processing-categories.md and tests/dw-26-stable-processing-categories-2026-10-17/validation.json. Baseline topology reported security Unhealthy and services Waiting; healthy topology acceptance is not claimed.

### DW-27: Report invalid PageSize separately from InvalidCursor

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
location: Paging.PageSize validation
reason: Out-of-range `PageSize` under `Paging` rejected as `InvalidCursor` even with no cursor — debugging misdirection only.
status: open
decision: 2026-09-06 Add invalid-page sentinel — Add and map an InvalidPage failure reason, distinguish cursor failures from paging validation, and cover EventStore and Parties compatibility.
progress: 2026-10-17 Shared EventStore source prerequisite is prepared: additive InvalidPage sentinel, optional init-only exception discriminator, safe HTTP 400/query_invalid_page mapping, sanitized page logs and legacy cursor fallback. Contracts/Server Debug source builds have zero warnings/errors; focused suites pass 112/112 and 116/116.
dependency: Publish a compatible EventStore Contracts/Server/gateway SDK before activating Parties. Fresh public NuGet inspection still selects 3.124.0 and confirms the new APIs are absent; no package version was changed. The tested Parties producer and handler/gateway regressions are retained as the unapplied patch tests/dw-27-invalid-page-failure-2026-10-17/parties-consumer-adoption.patch.
verification: Candidate source build passes with zero warnings/errors and 214/214 focused tests. Its earlier pre-review full assembly has 1170/1171 passing, with immutable consumption proof blocked by the required dirty EventStore checkout. Restored tracked Parties pinned-package build passes with zero warnings/errors and 146/146 focused tests. Full EventStore attempts were stopped after packaging/Dapr failures outside the changed test classes (not reproduced against baseline); full topology and assembly qualification are not claimed. Exact commands, diagnostics and rollout gate are in tests/dw-27-invalid-page-failure-2026-10-17/validation.json. Keep DW-27 open until real package adoption and all required gates pass.


### DW-28: Harden query compatibility shim lifecycle and state handling

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
archived: 2026-10-13

### DW-29: Preserve the cursor codec failure reason

status: done 2026-08-03
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
archived: 2026-10-13

### DW-30: Add authenticated projected-query end-to-end proof

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
location: host AddEventStoreDomainService registration and EventStoreGatewayE2ETests
reason: Host `AddEventStoreDomainService(... PartyDetailProjectionHandler.Assembly)` remains source-text-only — closing properly needs reinstating the retired tenant seeder for authenticated query e2e. Also recorded in "Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)": Host wiring (`builder.AddEventStoreDomainService(typeof(PartyAggregate).Assembly, typeof(PartyDetailProjectionHandler).Assembly)`) is verified only as literal source text by `ArchitecturalFitnessTests`/`PlatformApiPrerequisitesTests`/ `RetiredLeafProjectFitnessTests`; no test queries a projected read model after an authenticated end-to-end command. Closing this needs `EventStoreGatewayE2ETests`, but its `PartiesAspireTopologyFixture.RequireSeededTenants()` unconditionally throws since Story 12.2 retired `TenantIntegrationTestSeeder` — reinstating that seeder is real work out of scope for a review-patch pass.
status: open
decision: 2026-09-06 Minimal test-only seeder — Add a narrowly scoped fixture seeder through the supported internal tenant-event callback, then submit an authenticated command and query its projection.

### DW-31: Add runtime DAPR ACL enforcement proof

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
location: DAPR ACL allow-list
reason: ACL allow-list has no runtime Dapr enforcement check beyond YAML fitness — same topology e2e class as the assembly-scan defer.
status: open

### DW-32: Clean up query shim and configuration naming

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
location: Party query shims, EventStore:Projections configuration, and build documentation
reason: Minor/cosmetic: query shim classes keep "Actor" names; `EventStore:Projections` config-key reuse; undocumented `Dapr.Actors.AspNetCore` / MSBuild property rename — intentional temporary trade-offs from the first Group 4 pass.
status: open
decision: 2026-09-06 Add compatible SDK names — Introduce canonical SDK names and a Parties-specific configuration section while retaining obsolete aliases and fallback binding.

### DW-33: Clean up Group 5 DI, health, naming, and status-key polish

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
location: Group 5 DI, health, query naming, and MessageId status handling
reason: Prior Group 5 cosmetic defer remains open (stringly DI absence checks; health "all components" naming; partial Ada→Synthetic rename; undocumented MessageId status-key change).
status: open

### DW-34: Strengthen cursor, payload, and hosted-service test fidelity

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
location: query and host test fixtures
reason: `TestCursorCodec` private double instead of production DI codec; collapsed index invalid-payload theory; six indistinguishable `<factory-registered>` hosted-service exclusions — intentional test-isolation / factory-registration limits from the first Group 5 pass.
status: open

### DW-35: Define whole-payload json-redacted event handling

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04)"), 2026-09-06
location: whole-payload json-redacted event handling
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Whole-payload `json-redacted` events still depend on a resolvable CLR type and can apply a default-valued event produced from `{}`. The same behavior existed in the retired actor path, and the current field-level protection service does not normally produce a root encrypted marker; correcting it belongs to the broader payload-redaction contract rather than this migration patch chunk. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": A parameterless event can deserialize from an empty redacted payload into a valid `IEventPayload` and be applied as a real domain fact, while whole-payload redaction is otherwise intended to skip application and advance only the checkpoint.
status: done 2026-10-18
decision: 2026-09-06 Always checkpoint only — Treat every root json-redacted payload as checkpoint-only regardless of CLR shape and add compatibility tests.
resolution: Whole-payload json-redacted empty objects and historical null now advance projection checkpoints before CLR resolution without applying default domain facts. Nonempty field-redacted objects and ordinary JSON retain existing behavior. Persisted active/erased state, audit retry repair, useful field-redaction effects and executed rebuild repair are covered. Final Debug/source builds have zero warnings/errors; focused cases pass 131/131, full projection suite 291/291 and unchanged-source service suite 1157/1157 without failures/errors/skips. All six independent review findings and follow-up are resolved; no new deferred work. Existing affected or unresolved-only projections need a full SDK rebuild. See spec-dw-35-redacted-checkpoints.md and tests/dw-35-redacted-checkpoints-2026-10-18/validation.json.

### DW-36: Align reconciliation LastIndexedEvent with the canonical fold

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.BuildReconciliationFold
reason: `PartyIndexSdkProjectionHandler.BuildReconciliationFold` recomputes `LastIndexedEvent` via a separate code path from the canonical `FoldCore`, used only on the already-confirmed idempotent-no-op/reconciliation branch — could pick a different "last event" for search-reconciliation notification metadata on a multi-event no-op batch, but doesn't affect canonical read-model correctness. Needs a dedicated multi-event test to pin the intended behavior.
status: done 2026-10-18
resolution: Canonical index writes retain one payload-free source-event descriptor per active aggregate; recreated-handler retries use persisted provenance, including older deliveries. Historical rows use explicit synthetic attribution. Erasure removes target metadata, and serialized rebuilds preserve canonical selection. Baseline regression reproduced; final Debug/source builds have zero warnings/errors, full projection tests pass 316/316 and service tests 1158/1158 without failures/errors/skips. All eight accepted independent review findings were addressed through verification and documentation; no new deferred work. See spec-dw-36-canonical-search-notification.md and tests/dw-36-canonical-search-notification-2026-10-18/validation.json. External-search concurrency fencing remains separate.

### DW-37: Test blocked Memories cleanup without a CaseId

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyMemoryCleanupService
reason: `PartyMemoryCleanupService`'s new "no persisted CaseId and no fallback configured" blocked branch has zero test coverage.
status: done 2026-10-18
resolution: Added eight cases covering null/empty/whitespace persisted CaseIds, empty/whitespace fallbacks, blocked mappings before/after eligible units, retained serialized inventory, scoped DELETE requests, sanitized diagnostics and recovery through restored configuration or captured-case repair. Executable production source is unchanged. Final Debug/source build has zero warnings/errors; cleanup tests pass 41/41 and the full service assembly passes 1195/1195 without failures/errors/skips. All three independent review findings are patched; no new deferred work. See spec-dw-37-blocked-memories-cleanup.md and tests/dw-37-blocked-memories-cleanup-2026-10-18/validation.json. The separate Aspire baseline failed to build with MSB4166 and canceled NuGet restores; no full-topology qualification is claimed.

### DW-38: Harden rebuild completion against null manifest collections

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
archived: 2026-10-13

### DW-39: Refresh the G5 matrix validation-evidence identity

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
archived: 2026-10-13

### DW-40: Pin the reusable commitlint workflow revision

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .github/workflows/commitlint.yml
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
reason: Incidental review found `.github/workflows/commitlint.yml` consumes `Hexalith/Hexalith.Builds/.github/workflows/commitlint.yml@main`, allowing unrelated upstream changes to alter validation without a reviewed Parties change.
status: open

### DW-41: Verify all release package and container artifacts

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .github/workflows/release.yml
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
reason: Incidental review found `.github/workflows/release.yml` treats a non-draft GitHub Release at the dispatched commit as sufficient proof, without verifying the complete NuGet and container artifact set.
status: open

### DW-42: Pin Story 8.7 G5 validation evidence to an exact commit

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review of the 2026-08-04 SCP recovery found the G5 row's `git ls-tree HEAD references/Hexalith.EventStore` / `references/Hexalith.Builds` commands resolve against whatever the working tree currently points to, unlike the sibling projection/query SDK and DataProtection rows in the same matrix, which pin to an exact Parties commit (`03ab938c637aa15f7a0af402afc8664dfc54d1a4`) for reproducibility. This pattern pre-dates the 2026-08-04 identity refresh; the refresh preserved rather than introduced it.
status: open

### DW-43: Restore dropped G8 and cross-story guidance in Epic 8 context

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: _bmad-output/planning-artifacts/epic-8-context.md
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review found the working-tree regeneration of `epic-8-context.md` (predating this SCP recovery; only its line endings were normalized here) silently dropped the G8 local-run/publish JWT, audience-relationship, HTTPS-metadata, and secret-free-manifest proof requirements, and the sentence tying `AddEventStoreDataProtection`/`DaprXmlRepository`/cursor-codec consumption to the DataProtection prerequisite identity. It also compressed the explicit list of what stays externally owned for Stories 8.12/8.13 (production manifests, DAPR components, ingress, secrets, scans, signatures, promotion gates) into one generic sentence. A future Story 8.8/8.9/8.10 session loading only the cached epic context would miss this guidance.
status: open

### DW-44: Prevent rebuild finalization from overwriting concurrent live projection writes

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
archived: 2026-10-13

### DW-45: Document and attribute the sprint-plan --fresh rebuild fix

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .agents/skills/bmad-sprint-planning/scripts/sprint_plan.py
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review found this fix and its new test assertions are a distinct bug from the previously-documented STORY_RANK/`_slug()` regeneration incident, but no file in the current diff explains or attributes it, leaving a future reader unable to tell why `sprint_plan.py` changed.
status: open

### DW-46: Align PartySdkProjectionFold logging with house style

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold.Log and Hexalith.Parties.Projections.csproj
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — the `Log` class's comment claims `[LoggerMessage]` can't be used because `Hexalith.Parties.Projections.csproj` lacks a direct `Microsoft.Extensions.Logging.Abstractions` package reference, but `Hexalith.Parties.Security.csproj` is in the identical situation and successfully uses `[LoggerMessage]` throughout (`PartyKeyLifecycleService.cs`, `DecryptionCircuitBreaker.cs`, `PartyErasureOrchestrator.cs`) via a package reference with `ExcludeAssets="all"`. Adopting the same fix (or correcting the comment if a real difference is found) needs a deliberate, verified change to build configuration, not a same-pass patch.
status: done 2026-10-18
resolution: Converted the seven fold diagnostics to source-generated LoggerMessage methods using existing project configuration. User-approved stable IDs 4802–4808 and generated event names replace generic ID zero; templates, fields, severities, caller categories, checkpoint behavior and exception privacy are preserved. Operator filter migration is documented. All three independent review layers completed; eight small review corrections are verified and two low-value test-refactoring/coverage suggestions are rejected with evidence. Final Debug/source build has zero warnings/errors; focused tests pass 13/13 and full Projections.Tests passes 348/348 with no failures/errors/skips/not-run. Warning and whitespace gates pass; no new deferred work. DW-47 remains unchanged. See spec-dw-46-generated-projection-fold-logging.md and tests/dw-46-generated-projection-fold-logging-2026-10-18/validation.json.

### DW-47: Give projection-fold drop diagnostics stable logger categories

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold and PartyProcessingActivityFold diagnostics
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — drops detected inside the shared static helpers `PartySdkProjectionFold`/`PartyProcessingActivityFold` are logged under `PartyDetailSdkProjectionHandler`'s or `PartyIndexSdkProjectionHandler`'s log category depending purely on which handler called in. An operator filtering by the actual source class gets nothing, and the same drop reason can appear under two different categories. Fixing this cleanly needs a design decision (e.g., a dedicated logger category or `ILoggerFactory` seam), not a quick patch.
status: done 2026-10-18
decision: 2026-09-06 Dedicated fold categories — Give each fold a dedicated typed or named logger category, inject it through handlers, and lock category stability with tests.

resolution: Both handlers now inject named fold loggers through ILoggerFactory, preserving event IDs 4802–4808, structured metadata, exception privacy and diagnostic frequency. Index summary 4801 retains its handler category. Operator filters and manual constructor migration are documented. Final Debug/source build has zero warnings/errors; category tests pass 31/31 and full projection assembly passes 379/379 without failures/errors/skips/not-run. All three independent review layers completed; four small test/evidence findings are corrected and four low-value suggestions are rejected with evidence. No new deferred work. See spec-dw-47-stable-fold-logger-categories.md and tests/dw-47-stable-fold-logger-categories-2026-10-18/final-validation.json.

### DW-48: Bound dropped-event diagnostic volume during full rebuilds

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.AccumulateAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — `PartyIndexSdkProjectionHandler.AccumulateAsync` (the full-rebuild path) now re-emits a log line for every historically-known-bad event on every rebuild run, with no batching, sampling, or dedup — a real log-flooding risk on a large event store. Needs a product/ops decision on acceptable rebuild-time log volume, not a same-pass patch.
status: done 2026-10-18
decision: 2026-09-06 Summarize diagnostics — Accumulate bounded counts by failure reason and emit one no-PII summary at rebuild completion.
resolution: Rebuild accumulation now carries seven fixed saturating drop counters in serialized operation-owned candidates and emits one value-free structured summary per successful completion invocation; failed accumulation emits a bounded warning. Counts survive resumed handlers, repeated histories and empty accumulation without entering the canonical index. Legacy raw candidates remain readable; malformed envelopes reject. Live diagnostics, canonical state and search reconciliation are preserved. All eight independent review findings and follow-ups are resolved. Final Debug/source builds have zero warnings/errors; focused projection tests pass 145/145, full Projections.Tests 335/335 and full Parties.Tests 1195/1195 without failures/errors/skips. No new deferred work. See spec-dw-48-bounded-rebuild-diagnostics.md and tests/dw-48-bounded-rebuild-diagnostics-2026-10-18/validation.json. The separate 20-second Aspire healthy wait timed out (exit 17); no healthy topology qualification is claimed.

### DW-49: Handle converter format and overflow failures during event deserialization

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold.DeserializeNew
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding, pre-existing (not caused by this session's patch): the catch filter only covers `JsonException`/`ArgumentNullException`/`NotSupportedException`/`InvalidOperationException`; a `FormatException` or `OverflowException` thrown by a custom converter propagates unhandled and crashes the whole dispatch instead of being skip-logged.
status: done 2026-10-17

resolution: `PartySdkProjectionFold.DeserializeNew` now treats converter FormatException and OverflowException as existing corrupt-payload skip-and-advance outcomes. Actual serializer, privacy, live/redacted audit, checkpoint and persisted handler cases pass; final Projections.Tests 252/252 and Parties.Tests 1027/1027, zero failures/errors/skips. See `spec-dw-49-converter-failures.md`.

### DW-50: Bound and page the per-party Article 30 processing-activity read model

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartyProcessingActivityFold and PartyProcessingSdkReadModel.Records
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyProcessingSdkReadModel.Records` grows unbounded — one ever-growing JSON blob per party, re-serialized on every processing-activity projection write. A real scalability concern but needs a pagination/archival design, not a quick patch. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `PartyProcessingActivityFold` retains one ever-growing list and performs a linear `FindIndex` for every event, producing unbounded state values and quadratic rebuild work.
status: open
decision: 2026-09-06 Paged bucketed model — Partition records into bounded state buckets and add cursor-paged reads with an explicit compatibility and migration path.

### DW-51: Resolve projection-handler performance and validation-coupling debt

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartyDetailSdkProjectionHandler and PartyIndexSdkProjectionHandler
reason: Minor/cosmetic, `PartyDetailSdkProjectionHandler`/`PartyIndexSdkProjectionHandler` family: sequential (not parallel) `GetAsync` calls doubling state-store round-trip latency on the busiest projection path; duplicated `StoreName` null-check across classes; `PartyErased.LastModifiedAt` immediately overwritten by `NormalizeEventTimestamps` (harmless while both timestamps match, would silently diverge otherwise); `PartyIndexSdkProjectionHandler.Validate` reusing `PartySdkReadModelAddresses.Detail(...)` purely for its validation side effect, coupling Index validation to Detail's address-shape rules.
status: open

### DW-52: Clean up rollback-shim naming, configuration, and test debt

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: rollback query shims, configuration, DI tests, and health tests
reason: Minor/cosmetic, rollback-shim naming and test quality: `PartyDetailProjectionQueryActor` / `PartyIndexProjectionQueryActor` keep the "Actor" name with zero actor behavior (intentional temporary rollback shims); `PartySdkReadModelOptions.ConfigurationSection` reuses the retired `Hexalith.EventStore.Server.Configuration.ProjectionOptions`'s `"EventStore:Projections"` config key; the new `Dapr.Actors.AspNetCore` package reference and `$(HexalithCommonsHttpFromSource)` MSBuild property rename are undocumented but verified correct; the DI test `AddParties_UsesSdkReadModelsAndCursorCodecWithoutLocalProjectionMechanics` asserts absence via a brittle `descriptor.ServiceType.FullName` string match rather than a type reference; `HealthEndpoint_AllComponentsHealthy_Returns200WithoutRetiredProjectionActorCheckAsync` keeps an "all components healthy" framing that now excludes SDK read models from what "all" verifies; the PII seed rename (`"Ada"/"Lovelace"` → `"SyntheticPrivateFirstName8472"/"SyntheticPrivateLastName6391"`) landed in only 2 of dozens of usages across `EventStoreGatewayRoutingTests.cs`, with 7 other test files still using `"Ada"/"Lovelace"`; `DirectPartiesCommandRouter`'s test double now keys its completion write on `command.MessageId` instead of `command.CorrelationId`, correctly mirroring production `SubmitCommandHandler.cs` behavior but undocumented in the diff.
status: open

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
status: open
decision: 2026-09-07 Compatible canonical aliases — Add semantic SDK and read-model aliases in EventStore.Contracts while preserving legacy wire values. Switch Parties source and tests to the aliases and retain old constants as obsolete compatible members.

### DW-57: Move PartyEventTypeResolver out of the retired Actors folder

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Projections/Actors/PartyEventTypeResolver
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The resolver remains under Actors/ after projection actors were deleted, obscuring ownership.
status: open

### DW-58: Provide Dapr-actor to SDK read-model key backfill

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Dapr-actor to SDK read-model deployment migration
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Story File List deletes actor projection paths without an AppHost/deploy cutover that migrates existing actor state into SDK keys.
status: open

### DW-59: Preserve mappings when unit ID and source URI match different rows

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartyMemoryUnitMappingStore
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Edge-case review found a second live mapping row can be dropped when two entries match the new unit id and source uri separately.
status: done 2026-10-18
resolution: Prioritize eligible unit-ID mappings over source-only matches, preserving distinct cleanup units across row order, legacy-case adoption and ETag retries. Keep source-only replacement compatibility when no eligible unit exists. Debug/source build has zero warnings/errors; 180/180 focused and 1251/1251 service tests pass with no skips. Four independent review findings were patched and follow-up verified; no new deferred work. See spec-dw-59-preserve-colliding-memory-mappings.md and tests/dw-59-mapping-collisions-2026-10-18/validation.json.

### DW-60: Restore bounded allowlisted party IDs on SDK detail queries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Party SDK query detail-envelope validation
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: TryValidateDetailEnvelope only rejects reserved chars after TenantSafeProjectionReadGuardrailsTests were deleted; oversized/malformed party ids are weakly gated.
status: done 2026-10-19
resolution: SDK detail/privacy queries now reuse PartyIdentifier.IsValid before any read-model or erasure-record access, preserving ordinal envelope agreement, optional EntityId fallback, ULIDs and supported legacy GUID forms. All six handlers have boundary, malformed-input, fallback and route regressions. Final Debug/source build has zero warnings/errors; focused handlers pass 685/685 and full affected service assembly passes 1898/1898 without failures/errors/skips/not-run. Four independent review findings are patched and follow-up verified; no new deferred work. See spec-dw-60-bounded-detail-query-identifiers.md and tests/dw-60-bounded-detail-query-identifiers-2026-10-19/validation.json.

### DW-61: Update the Story 7.4 projection compatibility E2E spec

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts
reason: This came from the DI and query-host sub-chunk comparing PartiesServiceCollectionExtensions.cs with 2c4a7af. `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts` still expects deleted projection-adapter registrations and old `ProjectionPlatformAdapterTests` method names — deferred, pre-existing e2e drift outside this DI chunk.
status: open

### DW-62: Use TimeProvider for erasure cleanup timestamps

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: erasure cleanup TimeProvider usage
reason: Erasure cleanup timestamps still use `DateTimeOffset.UtcNow` instead of the newly registered `TimeProvider` — deferred, pre-existing certificate timestamp pattern across erasure store results.
status: done 2026-10-19
resolution: All seven registered cleanup-result timestamps and both verification-service timestamps use the shared TimeProvider at completion. The original public constructor retains system-clock behavior; returned store timestamps survive sanitization. Clock-controlled asynchronous cleanup, multi-delegate completion and processor certificate read-back are covered. Debug/source builds have zero warnings/errors; focused verification/delegate/certificate checks pass 23/23, 10/10 and 1/1; full affected assemblies pass 244/244 and 1901/1901 without failures, errors, skips or not-run tests. Six independent review findings are patched and follow-up verified; no new deferrals. See spec-dw-62-erasure-cleanup-clock.md and tests/dw-62-erasure-cleanup-clock-2026-10-19/validation.json.

### DW-63: Protect Memories mapping replacement and clearing with concurrency-safe retries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryUnitMappingStore.ClearMappingsAsync and ReplaceMappingsAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryUnitMappingStore.ReplaceMappingsAsync` and the empty-list delete can overwrite a concurrent indexing write after cleanup reads the prior mapping set. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `ClearMappingsAsync` and `ReplaceMappingsAsync` use unconditional delete/save operations, so concurrent indexing can lose a newly committed mapping and leave an undiscoverable Memories unit.
status: done 2026-10-18
activated_by_spec: `_bmad-output/implementation-artifacts/spec-dw-63-concurrent-memories-cleanup.md`
resolution: Exact cleaned-entry subtraction now retries against fresh ETags, preserves concurrent additions and changed unit/source/case identities, and retains empty domain inventories to prevent Redis ETag reuse. Physical clearing is restricted to one-use health probes. Cleanup requires another pass when mappings remain. Final Debug/source build has zero warnings/errors; focused tests pass 133/133 and full service assembly passes 1187/1187. Rollback passes 1158/1158 baseline tests, and the unsafe-delete mutation is detected. All eight review findings are patched and independently verified; no new deferred work. See spec-dw-63-concurrent-memories-cleanup.md and tests/dw-63-concurrent-memories-cleanup-2026-10-18/validation.json. Drain older writers before rollout; separate cancellation, workflow-reconciliation and persistence-migration work remains open.

### DW-64: Persist partial Memories cleanup progress after caller cancellation

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryCleanupService.DeleteByPartyAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryCleanupService.DeleteByPartyAsync` uses the already-cancelled caller token in its `finally` mapping update, so cancellation can prevent the promised resumable audit state from being saved.
status: done 2026-10-20
resolution: Confirmed deletion progress is reconciled through exact-entry, concurrency-safe removal using an independent five-second cooperative cancellation budget before caller cancellation propagates. Unfinished, ambiguous and concurrent mappings remain discoverable; failed recovery logs only its type and preserves caller cancellation. Debug/source build has zero warnings/errors; 96 focused and 1920 full service tests pass without failures/errors/skips/not-run. Exercised rollback passes 1901 baseline tests and restores implementation bytes exactly. All six independent review findings are patched and follow-up verified; no new deferrals. See spec-dw-64-cancelled-cleanup-progress.md and tests/dw-64-cancelled-cleanup-progress-2026-10-20/validation.json. Existing-build EventStore/Tenants startup failures remain a separate topology qualification limitation; terminal Memories workflow completion is not claimed.
activated_by_spec: `_bmad-output/implementation-artifacts/spec-dw-64-cancelled-cleanup-progress.md`

### DW-65: Compensate after cancellation between Memories ingestion and mapping persistence

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryIndexingService
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryIndexingService` rethrows caller cancellation from `RecordMappingAsync` without deleting the already-created Memories unit, leaving an untracked unit outside erasure discovery. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `PartyMemoryIndexingService` propagates caller cancellation from the mapping write without deleting the already-created unit, leaving it outside later erasure discovery.
status: done 2026-10-20
resolution: Caller cancellation after acknowledged Memories ingestion now awaits compensating DELETE under an independent five-second cooperative recovery token, then rethrows the original cancellation. Captured tenant/case/unit routing, response qualification, ordinary cancellation behavior and persisted mapping inventories are preserved. Final Debug/source build has zero warnings/errors; focused tests pass 61/61 and the complete affected service assembly passes 1933/1933 without failures/errors/skips. The baseline reproduces all five response regressions; exercised rollback passes 1920/1920 existing tests. All five independent review findings are patched and follow-up verified; no new deferred work. See spec-dw-65-cancelled-indexing-compensation.md and tests/dw-65-cancelled-indexing-compensation-2026-10-20/validation.json. Terminal deletion and durable reconciliation remain open as DW-168/DW-170; no complete-topology qualification is claimed.

### DW-66: Use ingestion-time identity for Memories compensating deletion

status: done 2026-09-07
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13

### DW-67: Resolve consumer validation artifacts from central package versions

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-consumer-package-references.py` hard-codes obsolete FrontComposer and Tenants versions instead of the currently evaluated dependency set.
status: done 2026-10-20

resolution: FrontComposer and Tenants support packages now use evaluated central MSBuild versions instead of obsolete constants, with exact generated artifact validation. Final Debug CI build has zero warnings/errors; 14 focused and 128 full affected CI tests pass without failures/errors/skips/not-run. Three independent review corrections are patched and verified; two pre-existing shared-normalization/restore-graph limitations are recorded separately below. Package versions, gitlinks, public contracts and runtime code are unchanged. See spec-dw-67-central-consumer-support-versions.md and tests/dw-67-central-consumer-support-versions-2026-10-20/validation.json.

### DW-68: Contain consumer-validation package caches in the disposable workspace

status: done 2026-09-07
origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13

### DW-69: Restrict consumer validation to configured NuGet sources

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The generated NuGet configuration lacks `<clear/>`, and the CLI always retains nuget.org, allowing undeclared user or machine feeds to hide incomplete local package output.
status: done 2026-10-20
resolution: Generated consumer packageSources now clears inherited feeds, and explicit repeated --nuget-source values replace the nuget.org default while preserving both local feeds. Four CLI cases and a real offline NuGet restore cover exact source selection, inherited-feed rejection, explicit-feed success and XML escaping. Final Debug CI build has zero warnings/errors; 16 focused and 130 full affected CI tests pass without failures/errors/skips/not-run, including both new probes under PYTHONOPTIMIZE=1. Two introduced review findings are corrected and independently verified; inherited fallback-folder and disabled-source limits are documented and deferred separately. See spec-dw-69-consumer-nuget-source-isolation.md and tests/dw-69-consumer-nuget-source-isolation-2026-10-20/validation.json. Private-feed authentication binds to documented generated names; full Release consumer and fresh topology qualification remain separate gates.

### DW-70: Compare forbidden NuGet package IDs case-insensitively

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-nuget-packages.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-nuget-packages.py` performs case-sensitive package-ID checks even though NuGet identifiers are case-insensitive.
status: done 2026-10-20
resolution: Forbidden dependency IDs and fragments are compared with casefold while preserving the policy vocabulary, substring matching and original diagnostic spelling. Eighteen executable cases cover 216 zipped-nuspec fixtures across four casings, direct/first/last-group layouts, allowed near matches and multi-ID diagnostics. Final Debug CI build has zero warnings/errors; 34 focused, 148 complete affected CI tests and 18 optimized-environment cases pass without failures/errors/skips/not-run. Three review verification gaps were corrected and independently checked with targeted mutations; no new deferred work. See spec-dw-70-case-insensitive-forbidden-packages.md and tests/dw-70-case-insensitive-forbidden-packages-2026-10-20/validation.json. Existing-build Parties baseline was healthy; complete Release package publication and fresh full-topology qualification remain separate CI gates.

### DW-71: Pin reusable CI and CodeQL workflows to immutable revisions

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows/ci.yml and .github/workflows/codeql.yml
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `.github/workflows/ci.yml` and `.github/workflows/codeql.yml` invoke reusable workflows through mutable `@main` references.
status: done 2026-10-20
resolution: Both reusable validation callers now pin Builds 332b2c144defba546fe0a09626a4a72f2340e22f, with exact remote workflow-blob verification and aligned update guidance. Final Debug/source build has zero warnings/errors; 48 focused workflow and 148 full CI tests pass without failures/errors/skips/not-run. Three independent review findings are patched and verified, including commented-reference rejection and selected-object checkout-divergence/missing-object probes; no new deferred work. Release execution pin and root gitlinks remain unchanged. See spec-dw-71-immutable-ci-workflows.md and tests/dw-71-immutable-ci-workflows-2026-10-20/validation.json.

### DW-72: Publish Aspire-hosted services with Production defaults

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: src/Hexalith.Parties.AppHost/Program.cs
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `src/Hexalith.Parties.AppHost/Program.cs` emits `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` as `Development` for publish output.
status: done 2026-10-20

resolution: A publish-only policy sets both .NET environment variables to Production on every project resource, including optional services, while local profiles and explicit Staging values remain intact. Debug/source builds have zero warnings/errors, with 8 publish/run cases, 3 existing UI cases and 16 fitness checks passing. The original baseline full assembly passed 1933/1933; after rebasing onto concurrent sync bec09f01aa00bd9ef83554fd099ed36fb59ab2c6, the full assembly passes 2001/2003 with two documentation/receipt failures reproduced unchanged on that parent checkout. Four review gaps are corrected and independently verified. Healthy local Parties and UI stop cleanly; full topology and Release qualification remain separate. Existing Memories production authentication and parent metadata drift are deferred below. DW-72 changes no dependency versions or gitlinks. See spec-dw-72-production-publish-environments.md and tests/dw-72-production-publish-environments-2026-10-20/validation.json.

### DW-73: Fail publish preflight when the OIDC client secret is missing

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: src/Hexalith.Parties.AppHost/Program.cs
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `src/Hexalith.Parties.AppHost/Program.cs` substitutes an empty client secret and continues producing deployment artifacts that cannot authenticate.
status: done 2026-10-20
resolution: The retained AppHost rejects missing, empty and whitespace OIDC secrets before constructing its publish model, with actionable configuration guidance and unchanged valid secret bytes. Debug/source builds pass with zero warnings/errors; 40 model checks, 29 hostile-environment checks and 2004 service tests pass without failures or skips. Local Keycloak enabled/disabled behavior and the documented environment variable are covered. Independent review corrected fixture-provider precedence and verified the follow-up. See spec-dw-73-publish-oidc-secret.md and tests/dw-73-publish-oidc-secret-2026-10-20/validation.json. Full topology and release qualification remain separate.


### DW-74: Reject duplicate keys in merged BMAD configuration arrays

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: _bmad/scripts/config_utils.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `_bmad/scripts/config_utils.py` can retain repeated base codes or ids, leaving ambiguous effective configuration after overrides.
status: done 2026-10-20
resolution: Per-input and merge-selected keyed identities now reject duplicates, including single-layer and nested arrays. Errors identify files, paths, sides and item indexes without identifier values. Valid replacement/order/append semantics remain intact; real central configuration and all 49 installed skill customizations match baseline. All 17 Python methods and 48 adjacent CI contract tests pass; Debug build has zero warnings/errors. actionlint and the warning/submodule guard pass. A dedicated CI job runs the suite. Independent review and follow-ups are resolved with no new deferred work. See spec-dw-74-duplicate-bmad-identifiers.md and tests/dw-74-duplicate-bmad-identifiers-2026-10-20/validation.json.

### DW-75: Tolerate deleted historical tags during release verification

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows/release.yml
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `.github/workflows/release.yml` can report a successful current publication as failed when an older release references a tag that no longer exists.
status: done 2026-10-20
resolution: Publication verification skips only explicit HTTP 404 tag-ref responses, then still requires a validated non-draft release tag resolving to the exact dispatched SHA. Other API/transport failures and invalid refs or annotated objects remain fatal. The original workflow fails eight new regressions; final Debug/source build has zero warnings/errors, focused shell fixtures pass 36/36 and the complete affected CI assembly passes 184/184 without errors/failures/skips/not-run. Workflow lint and repository guards pass. All five independent review findings are corrected and follow-up verified; no new deferred work. See spec-dw-75-deleted-historical-release-tags.md and tests/dw-75-deleted-historical-release-tags-2026-10-20/validation.json.

### DW-76: Restore the Playwright accessibility lane as a required CI gate

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows, scripts/test.ps1, and tests/e2e/package.json
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The replacement CI workflow no longer runs `npm run test:a11y`, leaving axe, keyboard-focus, forced-colors, computed-style, and visual checks unexecuted. Also recorded in "Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)": The Playwright accessibility lane is wired into no workflow. `.github/workflows/` contains no Playwright or `test:a11y` step, `scripts/test.ps1` invokes no npm lane, and `tests/e2e/package.json`'s `test:a11y` script is called by nothing. Spine §7 I12 already records the always-on CI a11y lane as a separate open ledger item.
status: done 2026-10-20
resolution: Restored unconditional blocking ui-a11y CI with Release/package browser execution and retained diagnostics; six Chromium cases and 188 CI tests pass. See spec-dw-76-required-accessibility-ci.md and tests/dw-76-required-accessibility-ci-2026-10-20/validation.json.

### DW-77: Exercise mTLS across every configured Dapr sidecar

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: configured Dapr sidecar topology tests
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Current tests inspect generated YAML and one synthetic sidecar but never start the mTLS topology or prove a cross-service invocation with all sidecars credentialed.
status: done 2026-10-20
decision: 2026-09-06 Add test-only topology support — Introduce test-scoped readiness and tenant bootstrap, run all sidecars, and prove a credentialed cross-service invocation.

resolution: All eight complete-profile sidecars initialized Dapr security and answered their own metadata; real Parties-to-Tenants readiness returned 200 through the existing ACL. Authenticated callback bootstrap passed and missing/wrong tokens were rejected; controlled production subscription/access registrations verify membership and authorization effects. Final CI-runtime 1.18.2 proof: 2/2 pass, no skips, including the repaired admin UI-to-admin path; Release/package assembly: 98 pass, zero failures, 6 pre-existing health-test skips. Commands, scope limits and sanitized proof: tests/dw-77-all-sidecar-mtls-2026-10-20/validation.json and runtime-proof.txt.

### DW-78: Align PartyErased timestamp resolution across projection folds

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
location: PartyProcessingActivityFold.Fold and PartyDetailProjectionHandler.ApplyErasure
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyDetailProjectionHandler.ApplyErasure` assigns `ErasedAt = erased.ErasedAt` while `PartyProcessingActivityFold.Fold` assigns `@event.Timestamp.ToUniversalTime()`.
status: done 2026-10-19
decision: 2026-09-06 Payload erasure instant — Use PartyErased.ErasedAt consistently across detail and processing folds and document the semantic choice.
resolution: Detail and Processing event-derived tombstones now use PartyErased.ErasedAt, preserving its offset independently of envelope audit/freshness timestamps. Six persisted live/rebuild regressions cover distinct times, duplicate delivery, legacy redelivery before repair, later checkpoints and cleanup. Debug/source build has zero warnings/errors; full Projections.Tests passes 385/385 without failures/errors/skips/not-run. All four independent review findings are patched and follow-up verified; no new deferred work. Existing checkpointed values require a full party-detail rebuild from retained readable history. See spec-dw-78-erasure-timestamp.md and tests/dw-78-erasure-timestamp-2026-10-19/validation.json.

### DW-79: Parallelize state-store reads in PartyDetailSdkProjectionHandler.PrepareRebuildAsync

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
location: PartyDetailSdkProjectionHandler.PrepareRebuildAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PrepareRebuildAsync` awaits `readModelStore.GetAsync<PartyDetailSdkReadModel>` and `GetAsync<PartyProcessingSdkReadModel>` sequentially rather than concurrently with `Task.WhenAll`.
status: done 2026-10-20
resolution: Rebuild preparation starts both independent snapshot reads before awaiting their joint completion, preserving full replay, keys, caller token and per-slot ETag concurrency. Final Debug build has zero warnings/errors; all 399 projection tests pass, including fourteen focused overlap/failure/cancellation cases. The original handler fails 13/14 focused cases and an early-cancellation mutation fails 2/2; implementation bytes are restored exactly. Independent review corrected two verification gaps and confirmed the follow-up; no new deferred work. Source-mode launcher and external-security topology blockers remain separately recorded; no Release or full-topology qualification is claimed. See spec-dw-79-parallel-rebuild-reads.md and tests/dw-79-parallel-rebuild-reads-2026-10-20/validation.json.

### DW-80: Make Memories rebuild reconciliation atomic with concurrent erase and re-add

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13
reconciliation: 2026-10-19 The historical completion is superseded by the verified per-notification and post-operation reconciliation in DW-173/DW-193. The old once-per-completion read did not close the after-snapshot erasure race. See the archive correction and spec-dw-173-erasure-search-notification-fence.md; atomic remote workflow erasure is not claimed.

### DW-81: Move the Memories mapping ledger behind EventStore persistence

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryUnitMappingStore
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryUnitMappingStore` persists operational state directly through `DaprClient`, outside the EventStore read-model and write-policy abstractions required for domain-module persistence.
status: open

### DW-82: Verify empty Memories mappings against an authoritative inventory

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryCleanupService.DeleteByPartyAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryCleanupService.DeleteByPartyAsync` treats zero local mappings as cleaned even when state loss, legacy indexing, or configuration drift could leave remote units behind.
status: open
decision: 2026-09-07 Query Memories — Add or adopt a bounded Memories API that inventories units by canonical Parties source identity, reconcile local mappings from it, and require zero remote units before reporting cleanup complete.

### DW-83: Persist recovery when mapping and compensating deletion both fail

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryIndexingService
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryIndexingService` records a double failure only in logs, so later erasure has no durable way to discover the orphaned Memories unit.
status: open

### DW-84: Schedule retry or backfill when Memories indexing lacks a CaseId

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryIndexEntrySearchIndexer.NotifyIndexedAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryIndexEntrySearchIndexer.NotifyIndexedAsync` returns success when CaseId is absent, so fixing configuration alone does not cause the skipped party to be indexed.
status: open

### DW-85: Keep Memories cleanup health observable when indexing is disabled

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: MemoriesSearchHealthCheck
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `MemoriesSearchHealthCheck` returns Healthy immediately when indexing is disabled although previously persisted mappings can still require remote deletion and mapping-store access.
status: open

### DW-86: Validate erasure certificates before certifying store cleanup

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: ErasureVerificationService.VerifyErasureAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `ErasureVerificationService.VerifyErasureAsync` accepts an `ErasureCertificate` but never verifies it belongs to the requested tenant and party or represents a completed key-destruction state.
status: open

### DW-87: Handle null Party index dictionaries with bounded recovery

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyIndexSdkReadModel
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: A malformed persisted `PartyIndexSdkReadModel` with null dictionaries can reach dictionary operations and throw rather than producing a controlled rebuild-required or corruption result.
status: open

### DW-88: Bound Party search inputs before cursor and index evaluation

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: Party search payload parsing and cursor-scope construction
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Search payload parsing validates paging and type but imposes no length limits on strings copied into cursor scope and processed against tenant entries.
status: open

### DW-89: Verify EventStore-only deny ACL across all Dapr sidecars

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
archived: 2026-10-13

### DW-90: Consolidate timeout-safe git and process test helpers

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: EpicEightClosureFitnessTests, PlatformApiPrerequisitesTests, and DocumentationFitnessTests
reason: Duplicated, timeout-free git/process helpers. `RunGit`/`TryRunGit` are defined in both `EpicEightClosureFitnessTests` and `PlatformApiPrerequisitesTests`, and `Read(root, relativePath)` a third time in `DocumentationFitnessTests`. `RunGit` drains stdout fully before stderr with no timeout; not a realistic deadlock at these output sizes, but the pattern should be consolidated.
status: open

### DW-91: Restore forward skip-link reachability after route focus

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: FrontComposer shell route-focus handling and tests/e2e/specs/parties-accessibility.spec.ts
reason: `frontcomposer-skip-link-reachability-after-route-focus` — **route to FrontComposer shell owners.** Measured 2026-08-19 on the accessibility specimen at FrontComposer `7a337a21`: once the shell hydrates it moves focus to the route `<h1>` (`h1#parties-accessibility-specimen-title`). That is a deliberate SPA announcement pattern, but it also advances the browser's sequential focus navigation point past both `.fc-skip-link` anchors, so the first `Tab` after load reaches the page's first interactive control rather than "Skip to content". A keyboard user would have to Shift+Tab backwards to reach a skip link after a client-side route change. On a cold document load the DOM order is correct — the skip links are the shell's first two focusable descendants, which `parties-accessibility.spec.ts` now asserts explicitly by seeding focus on `.fc-shell-root`. Question for the owners: should the shell reset the sequential focus navigation point (for example by focusing a container ahead of the skip links, or by focusing the skip link itself) so WCAG 2.4.1 bypass remains forward-reachable after route changes? Also recorded in "Deferred from: code review of story-8-10 (2026-09-06)": Skip links are no longer the real first-Tab keyboard stop after a client-side route change — already routed to FrontComposer shell owners as `frontcomposer-skip-link-reachability-after-route-focus` above; the review layer that raised this again confirmed no further action is needed beyond what that entry already tracks. [tests/e2e/specs/parties-accessibility.spec.ts:37-49]
status: open
decision: 2026-09-06 Restore forward skip access — Adjust FrontComposer route focus so skip links remain forward reachable while retaining an accessible route announcement, then add producer and Parties Playwright coverage.

### DW-92: Read the EventStore version from the Builds catalog

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: PlatformApiPrerequisitesTests, docs/ci.md, and docs/architecture.md
reason: EventStore's exact version is hardcoded in five or more places (`PlatformApiPrerequisitesTests`, `docs/ci.md`, `docs/architecture.md` §3) rather than read from the Builds catalog's `HexalithEventStoreVersion`. This is the drift the "CI identity regression — one stale live assertion expecting EventStore 3.90.0" receipt already recorded once. (Updated 2026-09-06: the cited value was `3.95.0` when this note was written 2026-08-19; the 2026-09-05 catalog adopt advanced all cited places to `3.102.0` together, so the specific hardcoded value drifts each time the catalog moves — the underlying "not read from the catalog" gap remains.)
status: open
note: 2026-10-05 (Story 8.10 review loop 3, Blind 11) — the effective EventStore version is now the Parties pre-import pin `HexalithEventStoreVersion=3.113.0` in `Directory.Packages.props`; the Builds catalog default at `360a2b9c` is `3.112.0`, so the governing value is the effective MSBuild property, not the catalog default. Consolidating the test-side version lookups into one effective-value reader is in this entry's scope: `CommonsHttpRestoreRoutingTests`, `PartiesContainerPublishWorkflowTests.ReadEffectiveEventStoreVersion`, the `DocumentationFitnessTests` catalog evaluator, the `AccessibilityStyleGuardTests` FrontComposer lookup, and `PlatformApiPrerequisitesTests.EventStorePackageVersion`. They agree today only because EventStore is the sole root pin; a root pin of another package would make them diverge.

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
status: open

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

### DW-101: Consolidate Admin and Consumer exports onto the approved FrontComposer browser-download service after G4-D delivery

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Admin and Consumer exports and FrontComposer browser-download service
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
reason: This independently testable download and cleanup slice was split from Story 8.9 after its hardened draft exceeded the 1,600-token workflow limit.
status: open

### DW-102: Adopt the FrontComposer typed-name destructive confirmation mode for Admin erasure after G4-E delivery

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Admin erasure UI and FrontComposer destructive confirmation
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
reason: This independently shippable destructive-interaction slice was split from Story 8.9 after its hardened draft exceeded the 1,600-token workflow limit.
status: open

### DW-103: Complete Fluent UI V5 and Fluent 2 styling and accordion conformance across all Parties UI RCLs

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: all Parties UI RCLs
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
reason: This independently reviewable design-system conformance slice was split from Story 8.9 after its hardened draft exceeded the 1,600-token workflow limit.
status: open

### DW-104: Delete the retained local crypto and key-management engine and reconcile published Parties security APIs after shared-provider adoption proves parity and rollback

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Hexalith.Parties.Security and published Parties security APIs
source_spec: `_bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md`
reason: This destructive cleanup is independently shippable and was split from Story 8.7 after its hardened draft exceeded the 1,600-token workflow limit.
status: open

### DW-105: Make the parties-ui release container stay healthy in Development so the shared OCI smoke can finish and GitHub Release assets attach.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: parties-ui release container and shared OCI smoke workflow
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: Release run 33980524472 published NuGet 1.1.1 and passed parties/parties-mcp smoke, then failed parties-ui with image-start-failure; the GitHub Release for v1.1.1 has no nupkg assets because semantic-release never reached the GitHub plugin.
status: open

### DW-106: Execute the Release verify-source bash with a fake gh/GITHUB_OUTPUT harness the way EventStore does.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: .github/workflows/release.yml verify-source logic
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: Parties currently asserts bypass mapping by YAML substring order; a later assignment after esac can invert ci.yml vs commitlint.yml without failing those tests. Also recorded in "Deferred from: code review of story-8-10 (2026-09-06)": Release workflow's bypass-validation→proof-source mapping (`false→ci.yml`/`true→commitlint.yml`) is verified only by substring-ordering in the YAML text, not by executing the bash — already self-disclosed above ("Execute the Release verify-source bash with a fake gh/GITHUB_OUTPUT harness..."); the review layer that raised this again confirmed no further action is needed beyond what that entry already tracks. [.github/workflows/release.yml:44-56, tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs:100-116]
status: open

### DW-107: Add MSBuild/PublishContainer proof for rebound OCI created labels and reject impossible RFC 3339 calendar days.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: RebindContainerProvenanceLabels and shared OCI validation
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: RebindContainerProvenanceLabels is only string-checked; the shared OCI validator ignores org.opencontainers.image.created, and the regex accepts dates such as 2026-02-31.
status: open
decision: 2026-09-06 Authorize cross-repo hardening — Add real calendar validation, executable publish and rebind proof, and required created-label validation in the owning repositories.

### DW-108: Align diagnostic source gitlinks to the published nuget.org tags selected by the Builds catalog.

status: done 2026-09-07
origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
archived: 2026-10-13

### DW-109: Cover ordinary main pushes with the root-gitlink RC sign-off gate, and pin Tenants/Memories identities.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
location: .github/workflows/rc-gate.yml; tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: rc-gate.yml enforces `.gitlink-signoff.tsv` only on a release-candidate-labeled PR or a push to `rc/**`/`release/**`/a `v*` tag — never an ordinary push to `main`, which is this repo's actual workflow. PlatformApiPrerequisitesTests now pins all eight root identities, including Tenants and Memories, so that portion is complete. The RC signoff workflow still excludes ordinary main pushes; enabling owner-signoff enforcement there remains open.
status: open

### DW-110: Verify validate-publication-preflight.sh's commitlint-proof path is only ever reachable through an authorized bypass.

status: done 2026-09-06
origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-13

### DW-111: Observe app-owned content focus and reduced-motion CSS at runtime, not as source greps.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css; tests/e2e/specs/parties-accessibility.spec.ts
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Frozen §4.6 already records I13 as not discharged because Playwright focuses `.fc-skip-link` rather than a content control. `AccessibilityStyleGuardTests` only `ShouldContain`s CSS tokens. Moving `class="parties-main-content"` onto `<FrontComposerShell>` would keep those greps green while isolation stamps nothing. A computed-style assertion on `parties-specimen-primary-action` belongs in `npm --prefix tests/e2e run test:a11y`, which CI does not run (DW-76).
decision: 2026-10-05 Accept as named closure deferral — Administrator / jpiquot accepted the missing runtime content-control focus, forced-colors, and reduced-motion proof as a named deferral owned by `8.9-frontcomposer-ui-consolidation` for Story 8.10 closure (8.3 I20 "I13 / DW-111 accepted closure deferral" row). I13 is not discharged and spine §7 keeps it Deferred; this entry stays open as the exit-proof carrier.
status: open

### DW-112: Hide or retarget Skip to navigation when FrontComposer unmounts `#fc-nav` on compact viewports.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: The skip-to-navigation link renders whenever `HasNavigation` is true, but `#fc-nav` mounts only when `HasNavigation && !IsSubCompactDesktopViewport`. On Tablet/Phone the href has no focus target. Parties consumes the shell and does not edit this submodule; route to FrontComposer shell owners.
status: open
decision: 2026-09-07 Hide compact link — Render Skip to navigation only when #fc-nav is mounted, preserve Skip to content, and add producer plus Parties viewport coverage.

### DW-113: Parse only the first complete JSON object from MSBuild `-getProperty/-getItem` stdout.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: `EvaluateProjectGraph` slices from the first `{` to end of string. Trailing restore/log text after the JSON object would throw `JsonException` instead of a graph assertion. Unverified whether current MSBuild emits trailing content on this invocation; settle by capturing a live `-getItem` payload. If true, severity is medium.
status: open

### DW-114: Retire resolved Epic 8 closure deferrals from the accepted-wait set.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs (ParseDeferrals)
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: `ParseDeferrals` reads packed fields from each reason but not the ledger entry's first-class status, so a resolved entry whose reason remains can still satisfy `ExpectedDeferrals`.
status: open

### DW-115: Reconcile the README SDK prerequisite with global.json.

status: done 2026-10-04
origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
archived: 2026-10-13

### DW-116: Prove a FrontComposer RCL static asset is served in source mode.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: The source-mode static-asset test requests only `_framework/blazor.web.js`, so it does not prove that a selected FrontComposer source asset is available through the UI host.
status: open

### DW-117: Resolve the Fluent UI package root through NuGet and MSBuild configuration.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs (ResolveNuGetPackagesRoot)
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: The style guard accepts an empty `NUGET_PACKAGES` value and otherwise assumes the default user cache, ignoring `RestorePackagesPath` and NuGet `globalPackagesFolder`.
status: open

### DW-118: Make Playwright web-server ownership deterministic and test its environment matrix.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/e2e/playwright.config.ts and tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: Local `ASPNETCORE_ENVIRONMENT=Test` runs may reuse an unrelated process at the expected URL, while current checks only inspect configuration text and do not execute the CI/local and Test/Development matrix.
status: open

### DW-119: RecordConsentValidator does not guard LawfulBasis with IsInEnum and RevokeConsentValidator leaves Reason unbounded, unlike every sibling command validator.

origin: spec-deferred 5a330376d9e2
location: src/Hexalith.Parties/Validation/RecordConsentValidator.cs and RevokeConsentValidator.cs
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
reason: AddContactChannelValidator, AddIdentifierValidator and CreatePartyValidator all call IsInEnum on their enum property, and RestrictProcessingValidator bounds Reason to 256 characters. RecordConsent carries LawfulBasis straight into ConsentRecorded, so an out-of-range cast is persisted. Deferred rather than patched because the intent contract's Never clause forbids changing lawful-basis behaviour; adding the rule would reject commands that are accepted today.
status: done 2026-10-16
implementation: 2026-10-16 Validators and focused processor rejection tests implemented and reviewed; see spec-dw-119-consent-validation.md.
historical_validation_blocker: 86 focused tests pass; the full project retains 53 baseline gateway-fixture/source-receipt failures. Original-validator comparison confirms no new failures; keep open until the repository completion gate is green.
historical_revalidation: 2026-10-16 Resumed review reproduced 86 focused passes and the same 53 full-project failures at 95f21bbcd09dfbfa8d1c71a4b382c12c8eef8a0f. Current dependency observations and matching representative failure diagnostics are preserved in tests/dw-119-consent-validation-2026-10-16/resume-validation.json; caller-facing validation limits are documented in docs/api-contracts.md.
validation_blocker: none; the Debug source-reference build passed with 0 warnings/errors, focused tests passed 86/86, and the full assembly passed 978/978 without skips. Independent review completed; all required completion gates pass.
revalidation: 2026-10-16 Both baseline blockers repaired together; current-source observations preserve dated receipts and owner approvals. See tests/dw-119-consent-validation-2026-10-16/full-gate-validation.json.
decision: 2026-09-07 Tighten validation — Renegotiate the frozen contract, add IsInEnum for LawfulBasis, and cap revoke reasons at 256 characters. Propagate focused boundary tests without changing command or event wire shape.

### DW-120: The deterministic lowercased ConsentId can collide when two contact channels differ only by letter case, while the channel lookup itself is ordinal case-sensitive.

origin: spec-deferred 92120d4622ad
location: src/Hexalith.Parties/Domain/PartyAggregate.cs:1362-1364
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
reason: PartyAggregate.Handle(RecordConsent) builds $"{channelId}:{purpose}".ToLowerInvariant() while state.ContactChannels.Any(c => c.Id == command.ChannelId) compares ordinally, so channels "Ch-Email-1" and "ch-email-1" collapse onto one consent id. Both lines are unchanged by this story and the intent's Always clause requires preserving that generation, so this is pre-existing behaviour, not a regression.
status: done 2026-10-16
implementation: Standalone/composite additions reject new case-insensitive collisions while preserving exact retries and ordinal lookups. Historical ambiguous channels reject updates/grants; exact removal remains available. Retained foreign consent ownership rejects grants before idempotency, and multi-owner consent IDs reject revocation without rewriting history.
validation_blocker: none; Debug source-reference builds pass with 0 warnings/errors, channel/consent focused tests pass 138/138 (32 new regression cases), DW-119 focused tests pass 86/86, the complete server assembly passes 291/291, and the full Hexalith.Parties.Tests assembly passes 978/978 without skips. Independent review and targeted follow-up completed.
evidence: spec-dw-120-channel-id-collisions.md; tests/dw-120-channel-id-collisions-2026-10-16/validation.json. Contracts, dependency versions/gitlinks, and DW-119 fixes/evidence are unchanged. The pre-existing same-owner consent-renewal limitation is tracked separately. No push.
decision: 2026-09-07 Reject case-only duplicates — Add case-insensitive channel uniqueness guards on creation and update while retaining existing consent IDs for nonconflicting states. Define safe handling for pre-existing ambiguous aggregates and add collision tests.

### DW-121: The consumer portal maps the client's new ArgumentException to a generic Failed outcome while the admin portal maps it to ValidationRejected.

origin: spec-deferred 637e8540aa5c
location: src/Hexalith.Parties.UI/Services/ConsumerConsentClient.cs:43-47,67-70
source_spec: `spec-safe-consent-identifiers.md`
reason: PartiesAdminPortalApiClient.ExecuteGdprCommandAsync catches ArgumentException and returns AdminPortalGdprOutcome.ValidationRejected, but SelfScopedPartiesClient calls HttpAdminPortalGdprClient directly and ConsumerConsentClient has a bare catch returning ConsumerConsentOperationOutcome.Failed. Unverified because MyConsentPage supplies purposes from a fixed catalogue and channel/consent ids from stored projections, all of which satisfy ConsentIdentifier today. What would settle it, if-true severity medium: evidence that a stored channel or consent id in a real tenant fails ConsentIdentifier, or a consumer path that feeds free-form channel/purpose input.
status: done 2026-10-17
resolution: Consumer consent grant/withdraw now map delegated ArgumentException validation failures to the existing safe ValidationRejected outcome. Null requests, caller cancellation, other exceptions, returned mappings and fail-closed self-scoping remain covered. Ten validation regressions fail against the original adapter; final focused tests pass 44/44 and full UI/ConsumerPortal suites pass 388/388 and 82/82 (470 total), with zero failures/errors/skips. Debug/source builds have zero warnings/errors. Four independent review verification findings and follow-up are complete. Malformed historical identifiers are synthetic, with no live-tenant incident claimed. The separate 20-second Aspire baseline health timeout remains recorded; no runtime, dependency, gitlink, wire-contract or persistence changes. See spec-dw-121-consumer-consent-validation.md and tests/dw-121-consumer-consent-validation-2026-10-17/validation.json.

### DW-122: No test asserts that the production DI extension registers the consent validators, and the domain processor fails open when a validator is missing.

origin: spec-deferred f8aa7466374b
location: src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:376
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
original_reason: PartyDomainProcessorValidationTests.CreateInvoker builds its own AddValidatorsFromAssemblyContaining scan rather than calling PartiesServiceCollectionExtensions.AddHexalithParties, and PartyDomainProcessor.TryRejectInvalidPayloadAsync logs and returns null when no validator resolves. A regression in the production registration surface would leave every command unvalidated with the suite green. Pre-existing infrastructure design, not introduced here.
status: done 2026-10-16

implementation_spec: `_bmad-output/implementation-artifacts/spec-dw-122-production-validation.md`
resolution: Production AddParties registration is covered across the command inventory; absent validators reject before protected-state access, preserving exactly four existing aggregate-only commands and honoring supplied validators for all four. Debug source build passes with zero warnings/errors, focused tests pass 106/106, and full affected assembly passes 998/998 without skips. Independent review and targeted follow-up completed. Named-enum compatibility is separately tracked by DW-195. Evidence: `tests/dw-122-production-validation-2026-10-16/validation.json`.

### DW-123: ContactChannelNotFound and IdentifierNotFound still interpolate caller-supplied identifiers into their messages outside the two consent handlers.

origin: spec-deferred 671760729b1e
location: src/Hexalith.Parties/Domain/PartyAggregate.cs:412,415,423,426,1174,1229,1305
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
reason: PartyAggregate.cs lines 412, 415, 423, 426, 1174, 1229 and 1305 still emit $"Contact channel '{id}' not found." / $"Identifier '{id}' not found.", so the same event type now has two message dialects depending on which command produced it. Those call sites belong to commands outside this story's intent contract and were not touched by it.
status: done 2026-10-17

implementation: 2026-10-17 All six remaining channel/identifier not-found branches now use fixed diagnostics; composite Rejected details agree with persisted rejection events. Rejection types and validation/lookup semantics are preserved. See spec-dw-123-bounded-not-found-diagnostics.md.
previous_validation_blocker: The original service run passed 1008/1010 after concurrent dependency sync 90af3047b50046cb4f6f91e01f182f562110aab3; FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph and MaintainedTechnologyTablesMatchCentralPackageCatalog failed. That historical result is preserved in tests/dw-123-bounded-not-found-diagnostics-2026-10-17/validation.json.
resolution: Completion revalidation at Parties 4e4f271faa5eb141834a1e53eeebe8a2e1aef68c passes Server.Tests 315/315 and Parties.Tests 1080/1080, with zero failures/errors/skips/not-run and zero warnings/errors in both Debug/source builds. All 12 original failing-before diagnostics and both former fitness blockers pass. Independent review identified four completion-record improvements, all addressed; no new deferred work. Production source, tests and dependencies were unchanged in this continuation. See spec-dw-123-bounded-not-found-diagnostics.md and tests/dw-123-bounded-not-found-diagnostics-2026-10-17/completion-validation.json.

### DW-124: Publish a FrontComposer Shell release carrying the module-landing route surface consumed by Parties.UI.

status: done 2026-10-05
origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-10-04)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
archived: 2026-10-13

### DW-125: Align E2E Node declarations with the supported runtime floor

origin: Story 8.10 Round 6 ledger normalization (2026-10-04); preserved package-update deferral
location: tests/e2e/package.json
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-dotnet-10-0-401-and-submodules.md`
reason: Node >=24.0.0 is supported but @types/node selects major 26; choosing Node 24 declarations or raising the runtime floor remains a compatibility decision.
status: open

### DW-126: Reconcile the runtime-toolchain qualification packet

origin: Story 8.10 Round 6 ledger normalization (2026-10-04); preserved package-update deferral
location: scripts/validate-runtime-toolchain-evidence.py and the runtime-toolchain evidence schema/baseline
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-dotnet-10-0-401-and-submodules.md`
reason: The validator/schema/baseline expect 13.5.0-preview.1.260825-0345; the selected CommunityToolkit.Aspire.Hosting.Dapr is now 13.6.0-preview.1.261001-0243. Coordinated packet regeneration is required to qualify the current catalog.
status: open

### DW-127: Align ineligible identity resolver and client replies

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs:58,79; src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:58
severity: medium
status: done 2026-10-17
resolution: Preserved typed Ineligible replies for explicit Unknown classification while rejecting incomplete wire evidence, unsupported/foreign observations and every nonresolved human binding. Actual query-handler/gateway JSON/client round trips cover inactive, restricted, erasing, erased, unbound and resolved controls. Final Debug/source Client.Tests 330/330, Parties.Tests 1027/1027 and Contracts.Tests 199/199 pass with zero skips/errors/failures; builds have zero warnings/errors. Independent review and follow-ups complete; no new deferred work. Current dependency/doc baseline observations refreshed without changing versions, gitlinks or approvals. See spec-dw-127-ineligible-identity-round-trip.md and tests/dw-127-ineligible-identity-round-trip-2026-10-17/validation.json. Resolves DW-127/DW-146 together; no installed identity or full-topology qualification claimed.

### DW-146: Align server-produced Ineligible human/Unknown evidence with the public client validation contract.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs:58,79; src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:58
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 1 and Verification 3 share this root cause: the resolver can return Ineligible with a live HumanBinding or Unknown classification, while the client turns those shapes into Unavailable/503. Both paths were already committed in 37d87f5a2869b076c651a58714d60f64647848bc and are unchanged by this repair. Exit proof: explicit fail-closed response semantics and producer/client round-trip tests for inactive, restricted, erasing, and unknown Parties.
status: done 2026-10-17
resolution: Preserved typed Ineligible replies for explicit Unknown classification while rejecting incomplete wire evidence, unsupported/foreign observations and every nonresolved human binding. Actual query-handler/gateway JSON/client round trips cover inactive, restricted, erasing, erased, unbound and resolved controls. Final Debug/source Client.Tests 330/330, Parties.Tests 1027/1027 and Contracts.Tests 199/199 pass with zero skips/errors/failures; builds have zero warnings/errors. Independent review and follow-ups complete; no new deferred work. Current dependency/doc baseline observations refreshed without changing versions, gitlinks or approvals. See spec-dw-127-ineligible-identity-round-trip.md and tests/dw-127-ineligible-identity-round-trip-2026-10-17/validation.json. Resolves DW-127/DW-146 together; no installed identity or full-topology qualification claimed.

### DW-128: Validate binding command acknowledgments

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:121-150
severity: medium
status: done 2026-10-17
resolution: Required per-attempt correlation/message identity, validated every case-insensitive ID occurrence and explicit outcome flags, and preserved compatible optional fields, provisioning, HTTP failures and cancellation. Independent review and follow-up completed. Final Debug/source Client.Tests passes 301/301 including 145 identity cases; Debug/source and Release/package builds pass with zero warnings/errors. See spec-dw-128-binding-command-acknowledgments.md and tests/dw-128-binding-acknowledgments-2026-10-17/validation.json. Resolves DW-128/DW-147 together; no new deferred work.

### DW-147: Reject empty or unrelated successful HTTP objects as binding command acknowledgments.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:121-150
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 2: SubmitAsync deserializes any object without explicit false/rejected flags; SubmitCommandResponse permits default constructor values, and binding methods discard it. An empty object can complete the call without matching correlation/message identity. Pre-existing 37d87f5a2869b076c651a58714d60f64647848bc code, unchanged here. Exit proof: malformed, missing-field, and mismatched acknowledgment rejection plus valid acknowledgment acceptance.
status: done 2026-10-17
resolution: Required per-attempt correlation/message identity, validated every case-insensitive ID occurrence and explicit outcome flags, and preserved compatible optional fields, provisioning, HTTP failures and cancellation. Independent review and follow-up completed. Final Debug/source Client.Tests passes 301/301 including 145 identity cases; Debug/source and Release/package builds pass with zero warnings/errors. See spec-dw-128-binding-command-acknowledgments.md and tests/dw-128-binding-acknowledgments-2026-10-17/validation.json. Resolves DW-128/DW-147 together; no new deferred work.

### DW-129: Reject ineffective identity-history snapshot protection

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:294; src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs:90; tests/Hexalith.Parties.Security.Tests/IdentityHistoryProtectionTests.cs:39
severity: high
status: done 2026-10-17
note: 2026-10-05 (Story 8.10 review loop 3, Blind 8) — DW-142 names this entry as the open carrier for classifying `PartyState.HumanActorBindings` and `PartyState.HumanActorTransitions`, which `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs` currently records as `DeferredPrivacyDesign`. This entry's exit proof therefore also replaces those two `DeferredPrivacyDesign` rows with a decided classification in `PersonalDataInventoryTests`, with Contracts.Tests green.
resolution: Required current purpose-protected SDK snapshot evidence, self-contained detached ciphertext, and rejection of no-op/plaintext/encoding-only copies before reporting protected metadata. Replaced the two deferred history inventory classifications with independent purpose-protected personal data. Full Security.Tests 218/218 and Contracts.Tests 199/199 pass. Independent review and follow-up complete; see spec-dw-129-identity-history-snapshot-protection.md and tests/dw-129-identity-history-snapshot-protection-2026-10-17/validation.json. Existing SDK serialized-history capture and production custody qualification remain separate open work. No commit or push.

### DW-148: Require identity-history snapshot protection evidence before reporting protected metadata.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:294; src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs:90; tests/Hexalith.Parties.Security.Tests/IdentityHistoryProtectionTests.cs:39
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 3: the snapshot provider result is wrapped without effectiveness validation; the synthetic test provider returns its input. History evidence has no profile PersonalData annotation, while the adapter reports the wrapper as Protected. Existing identity-feature code from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: reject no-op custody snapshots and show persisted snapshot representation does not expose actor history. The synthetic test is not live custody qualification.
status: done 2026-10-17
resolution: Resolved together with DW-129: typed provider evidence and self-contained ciphertext are mandatory; encrypted direct/serialized round trips cover live and destroyed profile keys, metadata-aware reads and denied provider outcomes. Six review corrections are verified; the pre-existing SDK folded-input integration limitation is documented and separately deferred. See spec-dw-129-identity-history-snapshot-protection.md and its validation packet. No production custody qualification is claimed.

### DW-130: Qualify ordinary replay after retained-history expiry

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:143; src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:669-716
severity: high (unverified)
status: open

### DW-149: Determine whether finite history expiry prevents ordinary Party command replay and establish value-free lifecycle continuity if needed.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:143; src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:669-716
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 4: command replay unwraps every history event, but fallback catches only destroyed profile keys. A live qualified custody expiry outcome was not exercised, so harm remains unverified. Existing identity-feature paths, unchanged here. Settle by advancing an actual qualified provider beyond expiry and running profile, erasure, and replay/switch-back commands; demonstrate replayable lifecycle state without expired actor values or record the exact failure.
status: open

### DW-131: Validate identity fold wire representations

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Queries/PartyIdentitySourceFold.cs:39
severity: medium
status: open

### DW-150: Reject unsupported serialization formats and metadata versions before identity source folding.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentitySourceFold.cs:39
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 5: the fold deserializes every payload as JSON without checking its StreamReadEvent format/version. The owner authoritative reader validates continuity; StreamReadPageValidator only requires nonblank format and metadata version >=1, not Party representation support. Existing 37d87f5a2869b076c651a58714d60f64647848bc code, unchanged here. Exit proof: supported representations pass and otherwise contiguous unsupported-format/version streams are rejected.
status: open

### DW-132: Require test evidence on release validation bypass

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: .github/workflows/release.yml:49,312
severity: medium
status: open

### DW-151: Ensure the Commitlint-only release bypass cannot substitute for required test evidence.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: .github/workflows/release.yml:49,312
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 6: bypass-validation=true selects commitlint.yml exact-source proof while publication receives empty test-projects based on a CI-tested assumption. Protected operator approval supplies no test receipt. Existing workflow from 14d249fde316b0002aec84351d7a7cdf953d1d30, unchanged here. Exit proof: a release-policy decision and a test-backed path for the exact source; no release-policy change or publication is authorized by this entry.
status: open

### DW-133: Exercise real Parties admission proof construction

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Authorization/PartyIdentityAuthority.cs:14-38; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:25
severity: medium
status: open

### DW-152: Test real Parties identity admission scope construction rather than replacing the whole authority.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Authorization/PartyIdentityAuthority.cs:14-38; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:25
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 8: repository C# searches find only substituted IPartyIdentityAuthority consumers; the real wrapper builds payload digest, logical ID, message ID, operation and tenant/target scope before invoking the owner verifier. Existing identity-feature code, unchanged here. Exit proof: real-wrapper signed evidence acceptance and changed payload/scope/operation/message/logical ID, expiry, and missing-proof rejection before protected state unwrap.
status: open

### DW-134: Make visual regression and baseline updates executable

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:105; tests/e2e/package.json:11
severity: medium
status: open

### DW-153: Provide an actual image regression baseline and an update command that updates the asserted artifact.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:105; tests/e2e/package.json:11
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 9: the browser test attaches a screenshot and checks byte length, then compares manually read JSON text/visibility fields. No image matcher or JSON updater consumes --update-snapshots. Existing f8fd74045d95d441ee5adb0809a254307d0a24a6 test, unchanged here. Exit proof: deterministic layout/style image regression is detected and test:visual:update regenerates its asserted baseline.
status: open

### DW-135: Observe content forced-colors and reduced-motion behavior

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:85
severity: medium
status: open

### DW-154: Measure visible app-content focus and reduced-motion styles in the browser.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:85
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 10: media emulation is asserted, but the sole style value is a truthy outlineColor on a shell skip link; no content control outline style/width or animation/transition duration is checked. Existing browser test, unchanged here. Exit proof: focus a representative app-owned control and verify visible normal/forced-colors outline plus reduced-motion behavior. I13 remains open; this entry supplies no parity approval.
status: open

### DW-136: Audit the complete shell with axe

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:25
severity: medium
status: open

### DW-155: Include navigation and skip links in the blocking accessibility scan.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:25
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 11: axe includes only #fc-main-content, while requiredSelectors merely assert that navigation and skip links exist. Existing browser test, unchanged here. Exit proof: full-shell or dedicated omitted-region scans fail on navigation/skip-link accessibility regressions.
status: open

### DW-137: Verify provisioning through the registered domain processor

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:157-159; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:22
severity: medium
status: open

### DW-156: Add successful provisioning-envelope verification through PartyDomainProcessor with emitted events and enriched result assertions.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:157-159; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:22
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Verification 1, pre-verified regression gap: aggregate tests supply hand-built Authorization and bypass the processor; processor tests cover only unsupported-policy rejection. Removing trusted Authorization injection can break registered command dispatch without those tests failing. Existing identity-feature paths from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: accepted provisioning envelope yields creation/marker events and matching result identity at the processor boundary.
status: open

### DW-138: Isolate wrong-purpose human custody rejection

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs:67
severity: medium
status: open

### DW-157: Separate expired-interval and wrong-purpose client rejection cases.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs:67
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Verification 2, pre-verified broken-verification gap: the current case combines an expired interval with other-purpose, so removing the purpose check still rejects on expiry. Existing identity-feature test from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: otherwise valid current human evidence with the wrong purpose is rejected independently, and expired evidence with the correct purpose is rejected independently.
status: open

### DW-139: Wire FrontComposer reserved segments and module landing in package mode after a Shell release

origin: Story 8.10 final identity reconciliation (2026-10-05); follow-up of resolved DW-124
location: src/Hexalith.Parties.UI/Program.cs; src/Hexalith.Parties.UI/Components/Routes.razor; src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor; src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor.cs; src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj; references/Hexalith.Builds/Props/Directory.Packages.props
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: low
reason: Commit `47e2da3244fd7d6d14e39bc30513b23504e9bc27` cleared DW-124's package build failure by compiling the `FrontComposerRouteOptions.ReservedSegments` wiring (`admin`, `me`, `no-party-binding`) only under `HFC_ROUTE_OPTIONS`, which is defined only when `HexalithFrontComposerFromSource=true`. The package-mode graph (CI, bUnit, and the released `parties-ui` container) therefore registers no reserved segments, and packaged Shell `4.5.0` carries no `FcModuleLandingPage`, so the module-landing route surface stays source-only. Since 2026-10-05 (DW-141 resolution) the same gate also covers `PartiesOverview.razor`: its `FcPageTabs` `ModuleRoute="/parties"` and `DefaultTabId="overview"` parameters, which packaged Shell `4.5.0` lacks, are passed only under `HFC_ROUTE_OPTIONS` through the `ModuleRouteTabAttributes` `@attributes` dictionary in `PartiesOverview.razor.cs` (empty in package mode). Exit proof: a published `Hexalith.FrontComposer.Shell` release containing both types and the `FcPageTabs` `ModuleRoute`/`DefaultTabId` parameters, a Builds catalog bump selecting it, removal of the `HFC_ROUTE_OPTIONS` gate (in `Program.cs` and in `PartiesOverview.razor.cs`, with `PartiesOverview.razor` passing `ModuleRoute`/`DefaultTabId` directly), a green package-mode `Hexalith.Parties.UI` Release build and `PartiesOverviewTests`, a test pinning the reserved-segment set, and matching 8.3 G4 and identity rows. Rollback: keep the source-mode gate (the current state). Bound to the `8.9-frontcomposer-ui-consolidation` slot (G4 surface).
status: open

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

origin: Story 8.10 review of the closure-blocker resolution (2026-10-05), Verification 1 and other
location: src/Hexalith.Parties.UI/Program.cs (AdminPortal E2E fixture branch); src/Hexalith.Parties.UI/Services/PartiesAccessibilitySpecimenUserContextAccessor.cs; tests/e2e/specs/{admin-parties-list,consumer-portal-routes,party-picker}.spec.ts
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: With FrontComposer `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470`, `FrontComposerShell` renders `FcScopeBlocked` unless `IUserContextAccessor` yields a current tenant and user. The AdminPortal E2E fixture (Test environment, `AdminPortalE2E__Enabled`) replaces authentication, authorization and the API clients but keeps Quickstart's `NullUserContextAccessor`, and the 2026-10-04 repair supplies a synthetic scope only at the accessibility specimen route. The admin, consumer and picker Playwright specs therefore cannot observe their flows (pre-verified by code reading; not executed). Production (OIDC claims) and CI (no e2e job) are unaffected, and only `test:a11y` is a closure lane. Administrator / jpiquot chose to defer on 2026-10-05. Exit proof: the fixture branch supplies the fixture principal's tenant and user through `IUserContextAccessor`, a `PartiesUiHostCompositionTests` case resolves a non-null scope under the fixture composition, and a recorded green run of the non-a11y Playwright specs. Rollback: remove the fixture accessor registration.
decision: 2026-10-05 Defer — Administrator / jpiquot (spec 8.10 review of the closure-blocker resolution, Verification 1 and other): keep the AdminPortal E2E fixture's `NullUserContextAccessor` for Story 8.10; only `test:a11y` is a closure lane, and the fixture-scope repair and the non-a11y Playwright run wait for this entry's exit proof.
status: open

### DW-144: Require the stamped identity in every canonical closure receipt, not only Playwright

origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 4 and L3 Edge 16
location: tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs (DescribeReceiptGaps, DescribeAccessibilityStampGaps)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: The closure gate checks the selected FrontComposer SHA only in the Playwright accessibility evidence. The warning-policy, Release build, all-tests, package/consumer, and npm rows accept any exact `Pass`, so a row left over from a superseded identity would satisfy closure. That contradicts I16's rule that a receipt counts only when rerun at the stamped identity. Pre-existing: Parties `c782b68c` already had the Playwright-only check. Exit proof: each required row must carry a machine-checkable stamp of the final set (for example the EventStore source SHA or a named final-set token) and the Parties tree, with rejection cases. Rollback: keep the current Playwright-only stamp check.
status: open

### DW-145: Extend the personal-data inventory to the identity-feature contracts

origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 5
location: tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs (s_inspectedTypes)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: `s_inspectedTypes` omits contracts that carry actor identities: `EstablishHumanActorBinding`, `RebindHumanActorBinding`, `RevokeHumanActorBinding`, `ResolveHumanActorBindingAt`, `HumanActorBindingEvidence`, and the nested `HumanActorBinding`, `HumanActorBindingTransition`, `AgentPartyProvisioningResult`, and `AgentPartyIdentity` records. Members of the `[PersonalData]`-marked `PartyState.AgentProvisioning` are neither marked nor classified. All of these were added by `37d87f5a2869b076c651a58714d60f64647848bc`, before this delta; DW-142 classified only the six properties that failed. This is an I8 decision for the Parties GDPR owners, related to DW-129. Exit proof: every identity-feature contract type is inspected, and each property is classified with its rationale. Rollback: revert the added inventory rows.
status: open

### DW-158: Reconcile the upstream Tenants runtime project's administrator verifier contracts with a published EventStore SDK.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: upstream Tenants dynamically launched runtime project; published EventStore.DomainService SDK
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The retained Tenants source fails its dynamically launched package-mode build with CS0246 for IDomainServiceAdministratorVerifier and DomainServiceAdministratorClaim; neither type exists in published DomainService 3.115.0. Root solution package-mode compilation succeeds separately.
status: open

### DW-159: Include external dynamically launched runtime project graphs in orchestration dependency verification.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs (external runtime project inventory)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: PlatformApiPrerequisitesTests inventories only owned src, samples, and tests projects, so it cannot detect the pre-existing upstream Tenants source/package mismatch before topology startup.
status: open

### DW-160: Extend the existing gateway command test with a direct persisted-stream and aggregate identity read.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: existing EventStore gateway command test; persisted event stream and aggregate identity
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The pre-existing test observes Completed status and positive eventCount but does not read stored events; completion follows EventPersister in the SDK, but an independent persisted-stream assertion would strengthen that boundary.
status: open

### DW-161: Extend the persistent release verification job to validate all declared NuGet registry package bytes and repository commit metadata.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-nuget-trusted-publishing.md"), 2026-10-13
location: release verify-publication job; NuGet registry package bytes and repository commit metadata
source_spec: `_bmad-output/implementation-artifacts/spec-nuget-trusted-publishing.md`
reason: The pre-existing verify-publication job checks a GitHub Release tag only; this task performs independent nine-package download/metadata verification after Release rather than treating that tag as package publication evidence.
status: open

### DW-162: Commit the existing Story 8.8 available-row identity gate correction through its owning work.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md"), 2026-10-13
location: Story 8.8 spec available-row identity heading; AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity
source_spec: `_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md`
reason: The committed isolated integration run fails AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity because spec-8-8 lacks the required heading; the main working tree already contains the 10-line correction as separate user work and this task preserves it unstaged.
status: open

### DW-163: D1 — Preserve coordinated Detail/Processing slots when recording an unresolved first projection delivery.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs:69-87,245-265
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: The detail handler creates only Processing on first unresolved delivery, then rejects all subsequent retries at the missing-slot guard (69–87, 245–265); verified medium, pre-existing at 890119f1.
status: done 2026-10-18

legacy_implementation_blocker: 2026-10-17 Candidate two-slot audit initialization was withdrawn after SDK fault injection: a BeforeReceipt failure after newer erasure tombstones persist causes unguarded batch reconciliation to overwrite them. Prepared/committed manifest resumption also needs stable initialization identity. EventStore must supply safe recovery before this domain change; see `spec-dw-163-coordinated-unresolved-projection.md`. DW-163 remains open.

rejected_candidate_resolution: Adopted EventStore 3.125.1 through the shared Builds catalog. The handler validates SDK scope, recovers both pending keys, creates two empty create-only slots under a bounded event/dispatch-independent initialization identity with retained manifests, then records the failed audit with the existing optimistic policy. Initialization and normal delivery namespaces are separate; normal batches retain manifests so pending-key recovery preserves normal retry behavior. Genuine missing historical slots still require rebuild.
rejected_candidate_verification: Debug source builds passed with zero warnings/errors; focused handler tests 145/145, persisted recovery tests 24/24, full Projections assembly 335/335, full Parties assembly 1219/1219. The same 24 recovery tests passed against actual EventStore 3.125.1 NuGet packages; no-warning-override and CRLF-aware whitespace gates passed. Current receipts reflect the separately recorded concurrent /pushall graph without changing historical approvals. Evidence: `tests/dw-163-implementation-2026-10-18/README.md`. The 2026-10-17 blocker and reproducer remain legacy-mode evidence; no other deferred-work item is closed.

resolution: 2026-10-18 Review loop 1 limits retained manifests to two immutable empty initialization slots, created only for unresolved absent-slot deliveries. Both initialization keys recover before presence checks; normal non-retained retries and terminal namespace cleanup remain usable. Failed auditing uses the existing optimistic policy. Normal dispatch scopes, payloads and concurrency remain unchanged; real erasure leaves no orphaned payload manifest. Genuine missing historical slots still require rebuild.

verification: Debug/source builds pass with zero warnings/errors; focused handler tests 145/145, persisted source and published EventStore 3.125.1 package recovery tests 35/35 each, full Projections 335/335 and full service 1230/1230. Zero failures/errors/skips. Includes mixed-prefix redelivery, interrupted compensation, deterministic overlapping handlers, newer erasure tombstones, legacy first-create retries and real erasure after a normal pre-install interruption. Warning and whitespace gates pass. Revised source hashes and package graph: tests/dw-163-review-loop-1/validation.json. Earlier candidate evidence remains historical.

review_status: 2026-10-18 Revised implementation verified; independent parent review is pending before final closure.

final_review: Two review passes completed; all current-story findings are resolved or rejected with evidence. Final parent checks pass: 335 projection tests, 1232 service tests, 37 published-package recovery cases, zero build warnings/errors, warning and whitespace gates. The separate legacy normal postcommit compaction defect remains deferred because it predates DW-163.

### DW-164: D2 — Set an explicit SDK receiver identity for the optional sample resource matching its DAPR app ID.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.AppHost/Program.cs:237 (optional sample resource)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: AppHost sample at Program.cs:237 lacks the parties/tenants AppId binding; SDK fallback uses ApplicationName when DAPR_APP_ID is absent in the observed child path; verified medium, pre-existing.
status: open

### DW-165: D3 — Resolve the optional Memories resource endpoint for local Aspire runs.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.AppHost/Program.cs:217 (optional Memories run-mode endpoint)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Program.cs:217 supplies http://memories:8080/ in run mode although Memories is a host project with an Aspire-assigned endpoint; verified medium, pre-existing.
status: open

### DW-166: D4 — Align Memories cleanup, compensation and health probing with the versioned server route without accepting a routing 404 as proof of erasure.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs:52; indexing compensation; Memories server routes
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: PartyMemoryCleanupService.cs:52 and indexing compensation use api/tenants, while the selected server maps /api/v1/tenants; success-on-404 can discard cleanup inventory; verified high, pre-existing.
status: done 2026-10-18
progress: 2026-10-18 Implemented versioned cleanup, compensation and health probing with the shared Memories route builder. Only a structured MEMORY_UNIT_NOT_FOUND 404 counts as scoped absence; unqualified/malformed/case/tenant 404s retain mappings, fail compensation qualification and degrade the cleanup probe. Corrected the probe's invalid synthetic ID. Final Debug/source build has zero warnings/errors; focused tests pass 58/58. Independent review and follow-up are complete. Full assembly passes 1156/1157; closure remains open because the unchanged baseline dependency-receipt test expects a prior Builds gitlink. See spec-dw-166-versioned-memories-cleanup.md and tests/dw-166-versioned-memories-cleanup-2026-10-18/validation.json. DW-167/DW-168/DW-169 and terminal physical-erasure qualification remain separate.
resolution: 2026-10-18 Reconciled current dependency receipts with the already-committed root graph while preserving historical observations, approvals, source selections and assertion logic. Final Debug/source build has zero warnings/errors; platform prerequisite fitness tests pass 33/33 and full service assembly passes 1157/1157, including all 58 DW-166 regressions. Independent review and follow-up for the unchanged functional implementation are complete. See tests/dependency-receipts-2026-10-18/validation.json. The earlier blocked run remains preserved in its original packet; DW-167/DW-168/DW-169 and terminal physical-erasure qualification remain separate.

### DW-167: D5 — Persist the canonical terminal Memories unit identity after duplicate ingestion.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: Memories indexing workflow duplicate branch and party memory-unit source mappings
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Indexing stores accepted workflow ID, but the duplicate workflow branch can return a different ExistingMemoryUnitId after the preflight TTL expires or fails open; source mapping replacement then loses the deletable ID; verified high, pre-existing.
status: open

### DW-168: D6 — Retain Memories cleanup reconciliation until ingestion and deletion have reached their terminal states.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: Memories cleanup mappings; CaseService asynchronous ingestion/deletion workflows
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Cleanup discards mappings on HTTP success/404 although CaseService schedules deletion asynchronously and accepted ingestion may not yet have materialized a unit; verified high, pre-existing.
status: open

### DW-169: D7 — Use concurrency-aware removal when cleaning a party memory-unit mapping inventory.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs (ReplaceMappingsAsync)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: ReplaceMappingsAsync uses unconditional SaveStateAsync/DeleteStateAsync after a cleanup snapshot; a concurrent ETag-protected indexing addition can be overwritten; verified high, pre-existing.
status: done 2026-10-18
activated_by_spec: `_bmad-output/implementation-artifacts/spec-dw-63-concurrent-memories-cleanup.md`
resolution: Exact cleaned-entry subtraction now retries against fresh ETags, preserves concurrent additions and changed unit/source/case identities, and retains empty domain inventories to prevent Redis ETag reuse. Physical clearing is restricted to one-use health probes. Cleanup requires another pass when mappings remain. Final Debug/source build has zero warnings/errors; focused tests pass 133/133 and full service assembly passes 1187/1187. Rollback passes 1158/1158 baseline tests, and the unsafe-delete mutation is detected. All eight review findings are patched and independently verified; no new deferred work. See spec-dw-63-concurrent-memories-cleanup.md and tests/dw-63-concurrent-memories-cleanup-2026-10-18/validation.json. Drain older writers before rollout; separate cancellation, workflow-reconciliation and persistence-migration work remains open.

### DW-170: D8 — Preserve durable reconciliation for cancellation after Memories accepts ingest but before the Parties mapping persists.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties/Search/PartyMemoryIndexingService.cs (mapping-write cancellation boundary)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Indexing rethrows caller cancellation at the mapping-write boundary without compensation, while the server workflow can continue producing a unit; verified high, pre-existing.
status: open

### DW-171: D9 — Bound last-known-cache generation bookkeeping while retaining in-flight read invalidation.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: last-known cache EvictDetail and _keyGenerations bookkeeping
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Missing-detail reads call EvictDetail; _keyGenerations never expires or participates in the cache capacity limit; distinct valid missing IDs grow the singleton indefinitely; verified medium, pre-existing.
status: done 2026-10-17
resolution: Replaced historical per-key generations with bounded active-read tokens and finally-based cleanup across detail, index and processing queries. Exact-key eviction rejects late reads without invalidating unrelated reads or reviving ended tokens; saturation fails closed and preserves degraded fallback. Missing-key churn retains zero identifiers. Final Debug/source build has zero warnings/errors; focused cache/query tests pass 93/93 and full Parties.Tests passes 1066/1066 with zero skips/errors/failures. Independent review and follow-up completed; no new deferred work. See spec-dw-171-bounded-cache-read-tracking.md and tests/dw-171-bounded-cache-read-tracking-2026-10-17/validation.json. Cross-instance cache invalidation remains the separate DW-172.

### DW-172: D10 — Prevent a second Parties instance from serving cached personal data after another instance completes erasure.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: erasure delegates; multi-instance degraded detail/index fallback cache
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Erasure delegates evict only the local singleton; the other host can serve pre-erasure detail/index from degraded fallback without authoritative erasure revalidation; verified high, pre-existing.
status: done 2026-10-18

resolution: Canonical-read failures reject and evict personal detail and nonempty index caches across independent hosts; safe redacted/empty fallback and retained Article 30 records remain available. Real shared-erasure regressions pass 28/28, query/cache tests 98/98 and full host tests 1284/1284 without skips; Debug source build has zero warnings/errors. Independent review and corrections completed with no new deferred work. See spec-dw-172-cross-instance-cache-erasure.md and tests/dw-172-cross-instance-cache-erasure-2026-10-18/validation.json.

### DW-173: D11 — Fence rebuild search notifications against erasure after the canonical index snapshot is read.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: CompleteRebuildAsync and the search reconciliation adapter
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: CompleteRebuildAsync snapshots once before awaited removals and later publishes retained entries; the search adapter has no canonical reread/version fence, so completed cleanup can be followed by republishing erased data; verified high, pre-existing.
status: done 2026-10-19
resolution: Canonical search reconciliation now rereads each target before and after awaited indexing/removal, compensates late indexing after erasure, and retries changed targets, live re-adds, malformed/unavailable state and failed compensation. Fresh live JsonIgnore contact/identifier collections survive only while checkpoint, provenance and persisted fields match. Debug/source builds have zero warnings/errors; 27 focused regressions and both complete affected assemblies pass (1311 host + 379 projection, no failures/errors/skips/not-run). Eight independent review findings are patched. The awaited indexing/mapping/cleanup guarantee does not certify terminal Memories workflow completion; DW-168/DW-170 remain open. See spec-dw-173-erasure-search-notification-fence.md and tests/dw-173-erasure-search-fence-2026-10-19/validation.json.

### DW-174: Enforce the identity-query deadline during synchronous admission and source folding.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs; PartyIdentitySourceFold.Fold; authority.Admit
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `authority.Admit` and `PartyIdentitySourceFold.Fold` execute between deadline checks, so a stalled provider or large source can exceed the configured budget; both paths predate the release patch.
status: open

### DW-175: Bound noncooperative identity-query provider work after a caller deadline.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryDeadline.cs (ReadAsync)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartyIdentityQueryDeadline.ReadAsync` stops awaiting a stalled provider task but leaves it running; repeated calls can accumulate worker tasks. The deadline work was introduced separately from this release patch.
status: open

### DW-176: Make long retained human-actor history folds linear.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/RetainedHumanActorHistoryFold.cs (Append and Close)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `RetainedHumanActorHistoryFold.Append` scans prior bindings and `Close` searches by version for each transition; the scans existed at this spec's baseline.
status: open

### DW-177: Show accepted-command acknowledgement before slow admin list loading finishes.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor (LoadPageAsync and route selection)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesAdminPortal` sets its updating notice only after awaiting `LoadPageAsync` and route selection; this ordering predates the release patch.
status: open

### DW-178: Clear the admin updating notice when the saved party reaches the projection.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor (_acceptedCommandPartyId)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesAdminPortal` clears `_acceptedCommandPartyId` on navigation and context reset but has no convergence clear, so the notice can remain after the row appears; this behavior predates the release patch.
status: open

### DW-179: Restore the erasure confirmation action after a dialog open failure.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor (_pendingErasureDialogOpen, ShowAsync, _confirmErasure)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartyGdprOperationsPanel` consumes `_pendingErasureDialogOpen` before `ShowAsync` and catches `JSException` while `_confirmErasure` keeps the action button replaced; this is later UI work.
status: open

### DW-180: Reproduce whether immediate blob URL revocation loses a supported-browser privacy export.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.UI/wwwroot/admin-privacy-export.js
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `admin-privacy-export.js` revokes the object URL immediately after `anchor.click()`; a supported-browser download trace would settle whether the scheduling can cancel the download. Unverified medium in later UI work.
status: open

### DW-181: Resolve the loop-sweep ledger fallback through layered BMAD configuration.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: .agents/skills/bmad-loop-sweep/SKILL.md (Step 1 ledger fallback); layered BMAD configuration
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: With `BMAD_LOOP_LEDGER` absent, the skill reads legacy YAML despite describing `config.toml` layering, so a custom artifact location can be missed; this is agent-context work outside the release patch.
status: open

### DW-182: Obtain owner signoff for all root gitlinks changed since Parties v1.2.1 before another release.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: scripts/gitlink-rc-gate.sh; root gitlinks changed since v1.2.1; release publication gate
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `bash scripts/gitlink-rc-gate.sh --diff v1.2.1` exits 1 for Builds, Commons, EventStore, FrontComposer, McpCli, Memories, Platform, and Tenants; current release workflow blocks publication at that gate. These later pointers need a new owner selection.
status: open

### DW-183: Test reason-only degraded admin search preserving last known rows.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: admin search SearchDegradedReason branch and component tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The new `SearchDegradedReason` branch preserves rows for an empty response, but existing tests use a nonempty page or other degradation signals; removing the branch would clear rows without a test failure. This is later UI work.
status: open

### DW-184: Assert accessible checked state after switching the admin party-type radios.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: admin party-type radio aria-checked expressions and component/browser tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The new `aria-checked` expressions are not asserted by component tests or the browser test, which only checks radiogroup visibility; an inverted expression could misannounce the type. This is later UI work.
status: open

### DW-185: Narrow the operator-created NuGet trusted-publishing policy to the Parties package IDs and publish-only operations.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: operator-owned NuGet trusted-publishing policy (packageGlobPattern and key permissions)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The recorded active policy uses `packageGlobPattern: "*"` and grants unlist/relist in addition to push; the local nine-ID inventory does not limit the NuGet-issued key. The policy was created separately from the original release patch and must be changed by its owner.
status: open

### DW-186: Check whether BMAD visual-tool consumers require an array from the layered TOML setting.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: _bmad/config.toml; _bmad/cis/config.yaml (visual_tools)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `_bmad/config.toml` and legacy `_bmad/cis/config.yaml` both encode `visual_tools` as a JSON string. No repository consumer requiring an array was found; a runtime consumer trace would settle this unverified medium agent-config concern.
status: open

### DW-187: Test authenticated actor and tenant-delivery callbacks through the real service route.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: tests/Hexalith.Parties.Tests/Gateway/PartiesDomainServiceSecurityTests.cs; actor and /tenants/events service callbacks
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesDomainServiceSecurityTests` checks denial and endpoint policy metadata, but no owned test sends a valid channel request to the actor or `/tenants/events` handler. A later HTTP security change could block legitimate delivery without a failing test.
status: open

### DW-188: Exercise the release registry-floor comparison with isolated published-version fixtures.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: .github/workflows/release.yml (registry-floor parts > floor comparison); CI tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: Current CI tests inspect `.github/workflows/release.yml` text and step ordering but do not run its `parts > floor` comparison; a mutation that admits a stale tag could reach the protected release job. This gate was added after the original Parties release patch.
status: open

### DW-189: Run the existing contact-selector browser test in required CI.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: contact-selector Playwright create-form test; component FluentSelect.ValueChanged tests; configured CI workflow
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: Component tests invoke `FluentSelect.ValueChanged` directly while the Playwright create-form test is outside the configured CI workflow. Removing the rendered `FluentOption` children could pass required tests but leave users unable to select a contact type. This is later UI work.
status: open

### DW-190: Require the FrontComposer shared-status structural command and test its missing-command outcome.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
location: RequiredEvidenceTokensBySurface and RequiredDeliveredApiCommands (FrontComposer FcSurfaceStatus command)
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
reason: Existing RequiredEvidenceTokensBySurface and RequiredDeliveredApiCommands do not mandate the FcSurfaceStatus command; removing an individual FrontComposer command leaves other searches satisfying the row's nonempty-command guard. This gap predates the current conditional-expression refresh.
status: open

### DW-191: Validate McpCli and Platform source-selection rows independently of the eight consumed dependency fitness checks.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
location: CurrentReleaseGitlinks; McpCli and Platform source-selection documentation rows
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
reason: Existing CurrentReleaseGitlinks excludes these non-consumed root selections even though the current documentation table records them; release gitlink signing protects changed pointers, while direct table identity coverage remains future hardening.
status: open

### DW-192: Derive erasure status from committed events or reconcile it after event persistence; cover failed append and retry before reporting Verified or Erased.
origin: retro action item epic-8-retro-erasure-status-after-commit
location: n/a
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
reason: retrospective action item from sprint-status.yaml (epic 8; owner Amelia (Developer) + Winston (Architect) + Murat (Test Architect); sprint-status status open)
status: open

### DW-193: Fence live and rebuild search notifications against concurrent erasure; test both interleavings and reconcile deferred-work DW-80 with D11.
origin: retro action item epic-8-retro-erasure-search-fence
location: n/a
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
reason: retrospective action item from sprint-status.yaml (epic 8; owner Amelia (Developer) + Murat (Test Architect); sprint-status status open)
status: done 2026-10-19
resolution: Canonical search reconciliation now rereads each target before and after awaited indexing/removal, compensates late indexing after erasure, and retries changed targets, live re-adds, malformed/unavailable state and failed compensation. Fresh live JsonIgnore contact/identifier collections survive only while checkpoint, provenance and persisted fields match. Debug/source builds have zero warnings/errors; 27 focused regressions and both complete affected assemblies pass (1311 host + 379 projection, no failures/errors/skips/not-run). Eight independent review findings are patched. The awaited indexing/mapping/cleanup guarantee does not certify terminal Memories workflow completion; DW-168/DW-170 remain open. See spec-dw-173-erasure-search-notification-fence.md and tests/dw-173-erasure-search-fence-2026-10-19/validation.json.

### DW-194: Preserve the typed destroyed-key outcome when the crypto-pending marker write fails; add focused fault-injection coverage.
origin: retro action item epic-8-retro-destroyed-key-marker-fault
location: n/a
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
reason: retrospective action item from sprint-status.yaml (epic 8; owner Amelia (Developer) + Murat (Test Architect); sprint-status status open)
status: done 2026-10-18
resolution: Current-key protection preserves exact destroyed-key failures without retrying the crypto-pending marker, retains type-only diagnostics and propagates caller cancellation. Final Debug/source build has zero warnings/errors; focused faults pass 20/20 and full Security.Tests passes 238/238 without failures/errors/skips/not-run. All six independent review findings are corrected and follow-up verified; no new deferred work. See spec-dw-194-destroyed-key-marker-fault.md and tests/dw-194-destroyed-key-marker-fault-2026-10-18/validation.json.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Reject inconsistent consecutive revocations in certified expired actor-history prefixes.
  evidence: RetainedHumanActorHistoryFold advances expired certificate versions without live/revoked state; readable transitions enforce a stronger lifecycle. Later domain work, separate from the CI/release patch.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Run the existing admin browser accessibility assertions in a blocking CI lane.
  evidence: The shadow-root warning and dropdown semantics are tested by Playwright, but root CI executes only .NET suites with mocked JS interop; the later portal behavior can regress while those suites pass.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Give concurrent Parties integration topologies independent Dapr control planes, namespaces and app identities.
  evidence: The existing fixed control-plane container names/ports reject incompatible ownership; explicit mTLS reuse still shares application identities. Private Redis fixes direct store isolation but does not add general parallel-topology support.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Propagate the topology startup token through its final HTTP health waits.
  evidence: Both independent three-minute health waits lack the overall startup cancellation token at the original release baseline, so total startup can exceed the declared five-minute budget.

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Verify bounded Aspire shutdown before adding explicit teardown deadlines.
  evidence: Unverified medium: StopAsync and disposal lack caller-side bounds, but a nonterminating SDK shutdown path has not been reproduced; trace SDK host/DCP timeout behavior or demonstrate a hung owned process before changing cleanup semantics.

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

origin: DW-122 baseline reproduction
source_spec: `_bmad-output/implementation-artifacts/spec-dw-122-production-validation.md`
severity: medium
status: done 2026-10-16
reason: A production-registered RecordConsent with lawfulBasis serialized as "Consent" passes Parties validation and throws JsonException at EventStoreAggregate command deserialization; its shared EventStorePayloadSerialization.Options has no string-enum converter. HttpAdminPortalGdprClient writes commands with PartiesJsonOptions.Default, which emits enum names. Reproduced before the DW-122 processor change in PartiesValidatorRegistrationTests.ProcessAsync_ProductionConsentValidators_AllowValidPayloads(revoke:false). Existing numeric-enum processing succeeds. This pre-existing client/platform compatibility defect requires an independently scoped reader/writer fix and consumer proof; DW-122 preserves both serializers. See tests/dw-122-production-validation-2026-10-16/README.md.
resolution: Declared the existing named-enum wire policy on all six Parties command properties using BCL converters. EventStore binding accepts names and historical numbers, including nested/nullable cases; canonical client output and event/snapshot serialization remain compatible. No shared serializer, platform source, dependency or gitlink changes. Final full Contracts.Tests 198/198, Client.Tests 174/174, Parties.Tests 1010/1010 (1382 total) pass without skips/errors/failures; expanded production/HTTP focus 93/93. Debug source builds have zero warnings/errors. Five independent review findings and the follow-up evidence note are resolved. See spec-dw-195-command-enum-compatibility.md and tests/dw-195-command-enum-compatibility-2026-10-16/validation.json. Existing full-topology startup failures remain separately recorded.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-129-identity-history-snapshot-protection.md`
  summary: Qualify retained-history snapshot capture through the EventStore SDK's folded-state input.
  severity: medium
  evidence: SnapshotManager serializes state to JsonElement before calling protection, while the pre-existing Parties guard requires typed PartyState for independent profile/history protection. Retain that fail-closed guard; a safe owner-supported typed integration and persisted SDK capture/read-back test are needed. The DW-129 encrypted unit round trip proves local validation and serialized restore only; no production custody qualification or SDK capture availability is claimed.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-163-coordinated-unresolved-projection.md`
  summary: Make EventStore unguarded batch reconciliation preserve newer canonical writes, including erasure tombstones.
  evidence: A real SDK InMemoryReadModelStore fault at BeforeReceipt after newer sequence-5 Detail/Processing erasure tombstones persist makes CompactAndCompleteAsync restore the older initialization candidate. The six-case candidate reproduction passed five cases and failed this interleaving; candidate source/logs/XML are retained in /tmp/dw-163-validation. EventStore owns this persistence guarantee; the Parties candidate was withdrawn and DW-163 stays open.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-166-versioned-memories-cleanup.md`
  summary: Reconcile current dependency source observations and the unchanged release gitlink fitness constants with the already-committed Builds selection.
  evidence: FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph fails in the final full service suite and its focused invocation: expected Builds 4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77, actual 544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9. The starting Parties commit d2ec85c9e2b85e2b0c7a50d92ffaf197590ea053 already selects the latter, and the fitness test's baseline/current Git blob identities are identical. Restore accurate current-source observations through owning work without weakening the gate, fabricating owner signoff or recertifying historical parity receipts. Exact failing command, exits and proof are recorded in tests/dw-166-versioned-memories-cleanup-2026-10-18/validation.json.
  status: done 2026-10-18
  resolution: Current observations and fitness constants match all committed source selections; the old current observation is retained as history. All 33 prerequisite fitness tests and 1157 service tests pass. Dependency versions, gitlinks, owner signoffs and historical parity receipts are unchanged. See tests/dependency-receipts-2026-10-18/validation.json.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-163-coordinated-unresolved-projection.md`
  summary: Complete legacy non-retained normal-batch compaction before a subsequent projection tail needs writable ETags.
  evidence: The baseline PartyDetailSdkProjectionHandler from 6b0641829b68d22656fb173eec6578c65e8e91f0, compiled against the actual 3.125.1 package graph, returns Indeterminate after an injected AfterCommit failure, AlreadyCompleted on original redelivery, then Retryable/projection_partial_retry for a subsequent supported tail while its committed envelopes remain uncompacted. Replaying the exact original SDK batch compacts them. This predates DW-163 and the approved fix preserves normal batching; do not repair it by retaining payload-bearing manifests without owner-supported erasure/recovery guarantees. Reproduction uses that tracked baseline handler with ReadModelBatchPhase.AfterCommit fault injection in the existing SDK fake.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-67-central-consumer-support-versions.md`
  summary: Normalize NuGet versions consistently before validating support artifact filenames and nuspec versions.
  severity: medium
  evidence: The unchanged validate_support_packages helper rejects SDK-produced 6.5.4.0 and 6.5.4+build.9 packages: both filenames normalize to 6.5.4, while nuspec versions are respectively 6.5.4 and 6.5.4+build.9. Independent review reproduced both forms for FrontComposer.Contracts, Commons.Http and EventStore.Client, confirming a pre-existing shared-helper limitation. Current selected 4.6.0/5.8.0 versions do not trigger it; fix and cover all families together rather than changing established Commons/EventStore semantics inside DW-67.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-67-central-consumer-support-versions.md`
  summary: Qualify restored FrontComposer and Tenants consumer versions against their evaluated central support versions.
  severity: medium
  evidence: validate_consumer calls only validate_event_store_assets after restore, so correct local FrontComposer/Tenants support artifacts do not prove those versions were selected if NuGet chooses a different transitive requirement. The same gap exists in the baseline; add restored-asset family checks and a synthetic selected-version mismatch regression separately. DW-67 checks the generated direct support artifacts and preserves the existing configured-feed transitive boundary.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-69-consumer-nuget-source-isolation.md`
  summary: Isolate consumer validation from inherited NuGet fallback package folders.
  severity: medium
  evidence: Independent review restored a synthetic package from an inherited fallbackPackageFolders cache despite generated packageSources containing only empty local feeds. This separate setting is unchanged from baseline; resetting packageSources does not reset fallback folders. The DW-69 documentation now limits its guarantee to packageSources. Add deliberate fallback isolation and a missing-package regression without weakening package-only Release consumer validation.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-69-consumer-nuget-source-isolation.md`
  summary: Prevent inherited disabledPackageSources from suppressing explicitly selected consumer feeds.
  severity: medium
  evidence: An inherited disabledPackageSources entry for additional-source-1 suppresses an explicitly selected generated feed and makes a synthetic package restore fail with NU1101. Generated keys and inheritance predate DW-69. The documentation exposes the limitation; isolate/reset disabled-source settings and cover effective restored sources separately.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-72-production-publish-environments.md`
  summary: Qualify production authentication configuration for optional Memories in retained AppHost publish output.
  evidence: The unchanged Memories publish branch supplies Redis/FalkorDB connection strings but no JWT authority/signing key, issuer or audience. Memories appsettings.Production.json supplies none, and ValidateServerAuthenticationOptions rejects their absence at startup. Pinned Aspire 13.6.1 adds launch-profile environment variables only in run mode, so baseline publish already defaults this optional service to Production; DW-72 makes that default explicit without changing its authentication configuration. Provide owner-supported configuration and production startup proof separately.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-72-production-publish-environments.md`
  summary: Reconcile current dependency package documentation and source-selection receipts after the concurrent root-gitlink sync.
  evidence: After rebasing onto bec09f01aa00bd9ef83554fd099ed36fb59ab2c6, the complete service suite passes 2001/2003. MaintainedTechnologyTablesMatchCentralPackageCatalog expects docs/ci.md to describe EventStore 3.126.0 selected by the new Builds catalog, and FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph still expects Builds 332b2c144defba546fe0a09626a4a72f2340e22f while the parent selects 5d03f1db9c38db34ecac7fe6048937478ffb82cc. Both unchanged methods fail identically on the exact parent checkout; commands, results and matching test-source blob identities are retained in the DW-72 evidence. Refresh current observations without weakening gates, fabricating owner approval or recertifying historical parity evidence.
