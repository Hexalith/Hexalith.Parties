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
status: done 2026-10-21
resolution: Historical G8-A receipt is bound to the producer parent, full delivery/integration identities and ancestry, original consumer gitlink, complete four-file Git inventory and exact 26-case JWT lane source. Twenty-one malformed-claim regressions reject missing, altered, abbreviated, duplicated and unrelated evidence. This is immutable provenance validation, not a new producer runtime or adoption receipt.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

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
status: done 2026-10-21
resolution: Default-main guidance now matches the authoritative Hexalith Git policy; typed branches are conditional on task need.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

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
status: done 2026-10-21
resolution: Added canonical PartyDetailQueryTypes/PartyIndexQueryTypes with obsolete public aliases and literal wire-compatibility assertions; layer Parties:Projections over legacy EventStore:Projections before binding, preserving per-property fallback and validation. Build guidance documents actor callbacks and the Commons HTTP source selector.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.
decision: 2026-09-06 Add compatible SDK names — Introduce canonical SDK names and a Parties-specific configuration section while retaining obsolete aliases and fallback binding.

### DW-33: Clean up Group 5 DI, health, naming, and status-key polish

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
location: Group 5 DI, health, query naming, and MessageId status handling
reason: Prior Group 5 cosmetic defer remains open (stringly DI absence checks; health "all components" naming; partial Ada→Synthetic rename; undocumented MessageId status-key change).
status: done 2026-10-21
resolution: Typed SDK DI checks replace string exclusions; existing architectural guards cover deleted local interfaces. Health/readiness test names describe registered dependencies. Privacy sentinels have explicit leak-detection scope, and a distinct MessageId/CorrelationId regression verifies both persisted status entries and correlation metadata.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

### DW-34: Strengthen cursor, payload, and hosted-service test fidelity

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
location: query and host test fixtures
reason: `TestCursorCodec` private double instead of production DI codec; collapsed index invalid-payload theory; six indistinguishable `<factory-registered>` hosted-service exclusions — intentional test-isolation / factory-registration limits from the first Group 5 pass.
status: done 2026-10-21
resolution: Replaced the default private cursor double with the production DI codec; preserve legacy bounded output and compact oversized Unicode scopes. Raw/compact binding and cross-handler tests pass. Pin each excluded hosted-service factory by its sorted concrete service types resolved from IL metadata. The already-expanded five-case invalid-payload theory is verified.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

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

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .github/workflows/commitlint.yml
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
reason: Incidental review found `.github/workflows/commitlint.yml` consumes `Hexalith/Hexalith.Builds/.github/workflows/commitlint.yml@main`, allowing unrelated upstream changes to alter validation without a reviewed Parties change.
status: done 2026-10-21
resolution: Pinned the reusable commitlint workflow to the existing immutable CI validation revision; required CI assertion verifies the caller pin and pull-request-title forwarding.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-41: Verify all release package and container artifacts

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .github/workflows/release.yml
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
reason: Incidental review found `.github/workflows/release.yml` treats a non-draft GitHub Release at the dispatched commit as sufficient proof, without verifying the complete NuGet and container artifact set.
status: done 2026-10-22
resolution: The post-release job now requires all nine declared NuGet archives and all three two-platform container images after exact-tag proof. It checks out exact dispatched source and immutable Builds, enforces bounded registry reads/retries, and always uploads byte-derived diagnostics and receipts.
verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.

### DW-42: Pin Story 8.7 G5 validation evidence to an exact commit

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review of the 2026-08-04 SCP recovery found the G5 row's `git ls-tree HEAD references/Hexalith.EventStore` / `references/Hexalith.Builds` commands resolve against whatever the working tree currently points to, unlike the sibling projection/query SDK and DataProtection rows in the same matrix, which pin to an exact Parties commit (`03ab938c637aa15f7a0af402afc8664dfc54d1a4`) for reproducibility. This pattern pre-dates the 2026-08-04 identity refresh; the refresh preserved rather than introduced it.
status: done 2026-10-21
resolution: The G5 validation cell records exact historical Parties, EventStore and Builds identities with executable ls-tree/tag commands. Historical identity proof is explicitly separate from current parity and adoption approval.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

### DW-43: Restore dropped G8 and cross-story guidance in Epic 8 context

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: _bmad-output/implementation-artifacts/epic-8-context.md
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review found the working-tree regeneration of `epic-8-context.md` (predating this SCP recovery; only its line endings were normalized here) silently dropped the G8 local-run/publish JWT, audience-relationship, HTTPS-metadata, and secret-free-manifest proof requirements, and the sentence tying `AddEventStoreDataProtection`/`DaprXmlRepository`/cursor-codec consumption to the DataProtection prerequisite identity. It also compressed the explicit list of what stays externally owned for Stories 8.12/8.13 (production manifests, DAPR components, ingress, secrets, scans, signatures, promotion gates) into one generic sentence. A future Story 8.8/8.9/8.10 session loading only the cached epic context would miss this guidance.
status: done 2026-10-21
resolution: Restored G8 local-run/publish authentication and topology proof, DataProtection prerequisite identity binding, and explicit external deployment ownership to the actual implementation-artifacts Epic 8 context. Existing gates and rollback requirements remain.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

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
status: done 2026-10-21
resolution: Attributed the original fresh-generation timestamp fix and its later upstream regression; restored the fresh-only creation-date reset and YAML/JSON timestamp assertions while preserving incremental timestamps, action items and explicit overrides.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

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

### DW-51: Resolve projection-handler performance and validation-coupling debt

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartyDetailSdkProjectionHandler and PartyIndexSdkProjectionHandler
reason: Minor/cosmetic, `PartyDetailSdkProjectionHandler`/`PartyIndexSdkProjectionHandler` family: sequential (not parallel) `GetAsync` calls doubling state-store round-trip latency on the busiest projection path; duplicated `StoreName` null-check across classes; `PartyErased.LastModifiedAt` immediately overwritten by `NormalizeEventTimestamps` (harmless while both timestamps match, would silently diverge otherwise); `PartyIndexSdkProjectionHandler.Validate` reusing `PartySdkReadModelAddresses.Detail(...)` purely for its validation side effect, coupling Index validation to Detail's address-shape rules.
status: done 2026-10-21
resolution: Parallelized paired initial live reads and initialization reloads; shared store-name validation and direct identity-segment validation remove handler duplication and Index-to-Detail address coupling. Advancing end-state, both completion orders, either-slot faults and actual token cancellation are covered. Existing erased timestamp regression remains green.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

### DW-52: Clean up rollback-shim naming, configuration, and test debt

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: rollback query shims, configuration, DI tests, and health tests
reason: Minor/cosmetic, rollback-shim naming and test quality: `PartyDetailProjectionQueryActor` / `PartyIndexProjectionQueryActor` keep the "Actor" name with zero actor behavior (intentional temporary rollback shims); `PartySdkReadModelOptions.ConfigurationSection` reuses the retired `Hexalith.EventStore.Server.Configuration.ProjectionOptions`'s `"EventStore:Projections"` config key; the new `Dapr.Actors.AspNetCore` package reference and `$(HexalithCommonsHttpFromSource)` MSBuild property rename are undocumented but verified correct; the DI test `AddParties_UsesSdkReadModelsAndCursorCodecWithoutLocalProjectionMechanics` asserts absence via a brittle `descriptor.ServiceType.FullName` string match rather than a type reference; `HealthEndpoint_AllComponentsHealthy_Returns200WithoutRetiredProjectionActorCheckAsync` keeps an "all components healthy" framing that now excludes SDK read models from what "all" verifies; the PII seed rename (`"Ada"/"Lovelace"` → `"SyntheticPrivateFirstName8472"/"SyntheticPrivateLastName6391"`) landed in only 2 of dozens of usages across `EventStoreGatewayRoutingTests.cs`, with 7 other test files still using `"Ada"/"Lovelace"`; `DirectPartiesCommandRouter`'s test double now keys its completion write on `command.MessageId` instead of `command.CorrelationId`, correctly mirroring production `SubmitCommandHandler.cs` behavior but undocumented in the diff.
status: done 2026-10-21
resolution: The canonical query constants, obsolete aliases, Parties:Projections fallback and build-selector documentation already delivered by DW-32 are retained. Remaining DI, health, privacy-fixture and status-key cleanup is complete under DW-33, with runtime behavior and ordinary fixture data preserved.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

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
status: done 2026-10-21
resolution: Moved PartyEventTypeResolver and its tests to Serialization, extracted ResolveOutcome into its own file, updated all imports/reflection fixtures and corrected ownership documentation; allowlisted resolution and ambiguity tests pass.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

### DW-58: Provide Dapr-actor to SDK read-model key backfill

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Dapr-actor to SDK read-model deployment migration
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Story File List deletes actor projection paths without an AppHost/deploy cutover that migrates existing actor state into SDK keys.
status: open

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

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts
reason: This came from the DI and query-host sub-chunk comparing PartiesServiceCollectionExtensions.cs with 2c4a7af. `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts` still expects deleted projection-adapter registrations and old `ProjectionPlatformAdapterTests` method names — deferred, pre-existing e2e drift outside this DI chunk.
status: done 2026-10-21
resolution: Executed the existing SDK-oriented compatibility spec, corrected its two stale query test names, and pinned active Detail/Index outage rejection plus degraded processing behavior; all five source-contract cases pass.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

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

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: EpicEightClosureFitnessTests, PlatformApiPrerequisitesTests, and DocumentationFitnessTests
reason: Duplicated, timeout-free git/process helpers. `RunGit`/`TryRunGit` are defined in both `EpicEightClosureFitnessTests` and `PlatformApiPrerequisitesTests`, and `Read(root, relativePath)` a third time in `DocumentationFitnessTests`. `RunGit` drains stdout fully before stderr with no timeout; not a realistic deadlock at these output sizes, but the pattern should be consolidated.
status: done 2026-10-21
resolution: Consolidated repository Git/process calls through the existing shared runner, preserving literal arguments, concurrent capture, a shared execution deadline and bounded cleanup. Reused RepositoryRoot.Read. Focused process/fitness checks, complete Contracts and Client suites pass; the broader service suite retains the independently reproduced baseline Builds-receipt mismatch. Independent review and follow-up completed. See spec-dw-90-bounded-test-processes.md and tests/dw-90-bounded-test-processes-2026-10-21/validation.json.

### DW-91: Restore forward skip-link reachability after route focus

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: FrontComposer shell route-focus handling and tests/e2e/specs/parties-accessibility.spec.ts
reason: `frontcomposer-skip-link-reachability-after-route-focus` — **route to FrontComposer shell owners.** Measured 2026-08-19 on the accessibility specimen at FrontComposer `7a337a21`: once the shell hydrates it moves focus to the route `<h1>` (`h1#parties-accessibility-specimen-title`). That is a deliberate SPA announcement pattern, but it also advances the browser's sequential focus navigation point past both `.fc-skip-link` anchors, so the first `Tab` after load reaches the page's first interactive control rather than "Skip to content". A keyboard user would have to Shift+Tab backwards to reach a skip link after a client-side route change. On a cold document load the DOM order is correct — the skip links are the shell's first two focusable descendants, which `parties-accessibility.spec.ts` now asserts explicitly by seeding focus on `.fc-shell-root`. Question for the owners: should the shell reset the sequential focus navigation point (for example by focusing a container ahead of the skip links, or by focusing the skip link itself) so WCAG 2.4.1 bypass remains forward-reachable after route changes? Also recorded in "Deferred from: code review of story-8-10 (2026-09-06)": Skip links are no longer the real first-Tab keyboard stop after a client-side route change — already routed to FrontComposer shell owners as `frontcomposer-skip-link-reachability-after-route-focus` above; the review layer that raised this again confirmed no further action is needed beyond what that entry already tracks. [tests/e2e/specs/parties-accessibility.spec.ts:37-49]
status: open
progress: 2026-10-21 Reviewed FrontComposer source fix and producer/Parties source regressions delivered on main. Producer browser cases pass 105/105 across three engines, consumer candidate cases pass 8/8, and the original six package assertions remain enforced. Owner publication, catalog/package adoption and removal of the two explicit source opt-ins remain required before closure. See spec-dw-91-forward-skip-links.md and tests/dw-91-forward-skip-links-2026-10-21/validation-resume.json.
decision: 2026-09-06 Restore forward skip access — Adjust FrontComposer route focus so skip links remain forward reachable while retaining an accessible route announcement, then add producer and Parties Playwright coverage.

### DW-92: Read the EventStore version from the Builds catalog

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: PlatformApiPrerequisitesTests, docs/ci.md, and docs/architecture.md
reason: EventStore's exact version is hardcoded in five or more places (`PlatformApiPrerequisitesTests`, `docs/ci.md`, `docs/architecture.md` §3) rather than read from the Builds catalog's `HexalithEventStoreVersion`. This is the drift the "CI identity regression — one stale live assertion expecting EventStore 3.90.0" receipt already recorded once. (Updated 2026-09-06: the cited value was `3.95.0` when this note was written 2026-08-19; the 2026-09-05 catalog adopt advanced all cited places to `3.102.0` together, so the specific hardcoded value drifts each time the catalog moves — the underlying "not read from the catalog" gap remains.)
status: done 2026-10-21
note: 2026-10-05 (Story 8.10 review loop 3, Blind 11) — the effective EventStore version is now the Parties pre-import pin `HexalithEventStoreVersion=3.113.0` in `Directory.Packages.props`; the Builds catalog default at `360a2b9c` is `3.112.0`, so the governing value is the effective MSBuild property, not the catalog default. Consolidating the test-side version lookups into one effective-value reader is in this entry's scope: `CommonsHttpRestoreRoutingTests`, `PartiesContainerPublishWorkflowTests.ReadEffectiveEventStoreVersion`, the `DocumentationFitnessTests` catalog evaluator, the `AccessibilityStyleGuardTests` FrontComposer lookup, and `PlatformApiPrerequisitesTests.EventStorePackageVersion`. They agree today only because EventStore is the sole root pin; a root pin of another package would make them diverge.
resolution: One bounded actual-MSBuild version reader now supplies live prerequisite, documentation, publication, restore-routing and package-test lookups. Root pins, conditional/import order, Debug/Release selection and malformed values are covered. Historical package/source receipts retain their original versions and full IDs; live documentation names the governing properties and reproducible query.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

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
status: done 2026-10-22
resolution: Preserved and regression-tested the ICU-capable alpine-extra UI base with non-invariant globalization. A fresh SDK Rebuild/PublishContainer archive passes local linux/amd64 Development startup through shared Builds smoke helpers, all three health routes and the RCL stylesheet (HTTP 200), and final running-state proof. Owned container/image cleanup passes. This closes current startup qualification only; it does not repair the historical v1.1.1 release, certify arm64 or replace required release smoke.
verification: `tests/five-runtime-release-deferred-items-2026-10-22/ui-container-qualification.json` and `validation.json`; spec `spec-five-runtime-release-deferred-items.md`.

### DW-106: Execute the Release verify-source bash with a fake gh/GITHUB_OUTPUT harness the way EventStore does.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: .github/workflows/release.yml verify-source logic
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: Parties currently asserts bypass mapping by YAML substring order; a later assignment after esac can invert ci.yml vs commitlint.yml without failing those tests. Also recorded in "Deferred from: code review of story-8-10 (2026-09-06)": Release workflow's bypass-validation→proof-source mapping (`false→ci.yml`/`true→commitlint.yml`) is verified only by substring-ordering in the YAML text, not by executing the bash — already self-disclosed above ("Execute the Release verify-source bash with a fake gh/GITHUB_OUTPUT harness..."); the review layer that raised this again confirmed no further action is needed beyond what that entry already tracks. [.github/workflows/release.yml:44-56, tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs:100-116]
status: done 2026-10-21
resolution: The existing executable harness runs the tracked release source Bash with fake gh and GITHUB_OUTPUT. Exact-main, missing/malformed/wrong-source CI, API errors, successful Commitlint without CI, and retired bypass values are exercised. Mixed matching/unrelated runs succeed; split proof across invalid runs rejects. The retired bypass mapping is superseded by fixed full-CI proof.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


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
status: done 2026-10-22
resolution: Ordinary main pushes use a validated ancestor event.before and inspect every root pointer change throughout the complete push range. Missing bases, unsigned intermediate bumps, stale or placeholder signoffs, reset entries and malformed root declarations fail closed. Existing Tenants/Memories fitness pins and RC/tag/manual behavior are preserved.
verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.

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
status: done 2026-10-21
resolution: Required browser lane now measures keyboard-focused content controls in normal/forced colors and proves reduced-motion overrides against nonzero settings.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

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
status: done 2026-10-21
resolution: EvaluateProjectGraph consumes one complete JSON object with Utf8JsonReader/ParseValue. Nested, escaped-string, trailing-log and malformed/missing-payload regressions cover the behavior. Current live MSBuild emits no trailing logs; this is robustness evidence, not a claimed current reproduction.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-114: Retire resolved Epic 8 closure deferrals from the accepted-wait set.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs (ParseDeferrals)
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: `ParseDeferrals` reads packed fields from each reason but not the ledger entry's first-class status, so a resolved entry whose reason remains can still satisfy `ExpectedDeferrals`.
status: done 2026-10-21
resolution: Closure parsing uses first-class status from each whole ledger entry; resolved/missing/unknown status cannot satisfy accepted waits. Legacy accepted/open entries and required-field diagnostics remain supported. Completing an expected deferral now fails the stale expectation until its dependent invariants are deliberately reconciled.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

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
status: done 2026-10-21
resolution: The stylesheet guard consumes NuGetPackageFolders and the catalog-selected FrontComposer version captured as evaluated build metadata. The maintained CI probe restores a fixture package, compiles the actual resolver and metadata, and verifies asset bytes for globalPackagesFolder, RestorePackagesPath, NUGET_PACKAGES, empty NUGET_PACKAGES and fallback folders; no runtime cache guessing remains.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-118: Make Playwright web-server ownership deterministic and test its environment matrix.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/e2e/playwright.config.ts and tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: Local `ASPNETCORE_ENVIRONMENT=Test` runs may reuse an unrelated process at the expected URL, while current checks only inspect configuration text and do not execute the CI/local and Test/Development matrix.
status: done 2026-10-21
resolution: Managed Playwright runs always use reuseExistingServer=false. Eight executable configuration tests cover CI/local and Test/Development, explicit external-server ownership, an occupied-URL refusal and managed startup/response/cleanup by the real Playwright runner. They also pass on the declared Node 24.0.0 runtime floor. The existing required CI job runs test:config.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

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

origin: Story 8.10 Round 6 ledger normalization (2026-10-04); preserved package-update deferral
location: tests/e2e/package.json
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-dotnet-10-0-401-and-submodules.md`
reason: Node >=24.0.0 is supported but @types/node selects major 26; choosing Node 24 declarations or raising the runtime floor remains a compatibility decision.
status: done 2026-10-21
resolution: Selected and locked Node 24.19.2 declarations for the supported Node 24 runtime floor; Node 24.22.0 typecheck and all eight configuration cases pass.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-126: Reconcile the runtime-toolchain qualification packet

origin: Story 8.10 Round 6 ledger normalization (2026-10-04); preserved package-update deferral
location: scripts/validate-runtime-toolchain-evidence.py and the runtime-toolchain evidence schema/baseline
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-dotnet-10-0-401-and-submodules.md`
reason: The validator/schema/baseline expect 13.5.0-preview.1.260825-0345; the selected CommunityToolkit.Aspire.Hosting.Dapr is now 13.6.0-preview.1.261001-0243. Coordinated packet regeneration is required to qualify the current catalog.
status: open

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
status: done 2026-10-21
resolution: Current and retained identity folds reject unsupported event metadata versions before releasing evidence. Existing JSON/protection checks remain strict; DW-150 is the duplicate report, counted as one task.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-150: Reject unsupported serialization formats and metadata versions before identity source folding.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentitySourceFold.cs:39
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 5: the fold deserializes every payload as JSON without checking its StreamReadEvent format/version. The owner authoritative reader validates continuity; StreamReadPageValidator only requires nonblank format and metadata version >=1, not Party representation support. Existing 37d87f5a2869b076c651a58714d60f64647848bc code, unchanged here. Exit proof: supported representations pass and otherwise contiguous unsupported-format/version streams are rejected.
status: done 2026-10-21
resolution: Require version-1 event metadata as well as the supported JSON and protection representation on both identity paths. Current/retained malformed-version regressions and supported-history controls pass. This reconciles the duplicate DW-131 report.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-132: Require test evidence on release validation bypass

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: .github/workflows/release.yml:49,312
severity: medium
status: done 2026-10-21
resolution: The committed caller, shared preparation and publication wrapper require exact-source ci.yml proof; successful Commitlint and every retired bypass fixture cannot substitute. The human policy authority is recorded in spec-align-parties-ci-cd-with-eventstore-tenants.md under Release-gate decision — 2026-10-07; this verifies that policy without changing it or publishing. DW-151 is the duplicate record of this same gap.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-151: Ensure the Commitlint-only release bypass cannot substitute for required test evidence.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: .github/workflows/release.yml:49,312
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 6: bypass-validation=true selects commitlint.yml exact-source proof while publication receives empty test-projects based on a CI-tested assumption. Protected operator approval supplies no test receipt. Existing workflow from 14d249fde316b0002aec84351d7a7cdf953d1d30, unchanged here. Exit proof: a release-policy decision and a test-backed path for the exact source; no release-policy change or publication is authorized by this entry.
status: done 2026-10-21
resolution: Duplicate of DW-132. The bypass was already removed and full exact-source CI is fixed at every publication boundary. Executable legacy bypass/Commitlint rejection plus publication-wrapper cases pass under the existing documented release policy; no new policy or publication action is introduced.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-133: Exercise real Parties admission proof construction

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Authorization/PartyIdentityAuthority.cs:14-38; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:25
severity: medium
status: done 2026-10-21
resolution: Production PartyIdentityAuthority and RSA-backed IdentityAdmissionProof tests bind every command/query scope field and reject absent, wrong-key, malformed, expired and future-issued proofs. Also closes duplicate DW-152.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-152: Test real Parties identity admission scope construction rather than replacing the whole authority.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Authorization/PartyIdentityAuthority.cs:14-38; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:25
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 8: repository C# searches find only substituted IPartyIdentityAuthority consumers; the real wrapper builds payload digest, logical ID, message ID, operation and tenant/target scope before invoking the owner verifier. Existing identity-feature code, unchanged here. Exit proof: real-wrapper signed evidence acceptance and changed payload/scope/operation/message/logical ID, expiry, and missing-proof rejection before protected state unwrap.
status: done 2026-10-21
resolution: Duplicate of DW-133; real registered authority/proof scope tests now cover both command and query admission, including crypto/time/target rejection.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-134: Make visual regression and baseline updates executable

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:105; tests/e2e/package.json:11
severity: medium
status: done 2026-10-21
resolution: The deterministic specimen has an asserted Chromium/Linux PNG, separate all-platform semantic JSON checks, loaded fonts and normalized copyright year. The updater replaces the asserted PNG. Ubuntu 24.04/Release qualification passes; deliberate layout mutation fails, updating changes the PNG, and a future-year control passes normalization.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-153: Provide an actual image regression baseline and an update command that updates the asserted artifact.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:105; tests/e2e/package.json:11
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 9: the browser test attaches a screenshot and checks byte length, then compares manually read JSON text/visibility fields. No image matcher or JSON updater consumes --update-snapshots. Existing f8fd74045d95d441ee5adb0809a254307d0a24a6 test, unchanged here. Exit proof: deterministic layout/style image regression is detected and test:visual:update regenerates its asserted baseline.
status: done 2026-10-21
resolution: Duplicate of DW-134, completed by the same asserted PNG, executable updater and mutation proof. This is one work item, not an additional closure toward the five-item batch.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-135: Observe content forced-colors and reduced-motion behavior

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:85
severity: medium
status: done 2026-10-21
resolution: Content controls now have direct forced-colors and reduced-motion computed-style proof; duplicate scope of DW-111/DW-154.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-154: Measure visible app-content focus and reduced-motion styles in the browser.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:85
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 10: media emulation is asserted, but the sole style value is a truthy outlineColor on a shell skip link; no content control outline style/width or animation/transition duration is checked. Existing browser test, unchanged here. Exit proof: focus a representative app-owned control and verify visible normal/forced-colors outline plus reduced-motion behavior. I13 remains open; this entry supplies no parity approval.
status: done 2026-10-21
resolution: Actual app-content button/link outlines, widths, offsets and colors are asserted; nonzero motion settings must be suppressed by the production stylesheet.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-136: Audit the complete shell with axe

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:25
severity: medium
status: done 2026-10-21
resolution: The blocking axe scan covers the complete specimen document, including shell navigation and skip links.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-155: Include navigation and skip links in the blocking accessibility scan.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:25
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 11: axe includes only #fc-main-content, while requiredSelectors merely assert that navigation and skip links exist. Existing browser test, unchanged here. Exit proof: full-shell or dedicated omitted-region scans fail on navigation/skip-link accessibility regressions.
status: done 2026-10-21
resolution: The shared full-document axe gate rejects deliberate inaccessible navigation and skip-link controls outside main content; duplicate scope of DW-136.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-137: Verify provisioning through the registered domain processor

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:157-159; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:22
severity: medium
status: done 2026-10-21
resolution: Party-keyed production IAsyncDomainProcessor registration provisions from serialized payload without caller Authorization, emits ordered creation/marker events and exact enriched identity, and preserves original provenance/revision on a later-snapshot retry. Also closes duplicate DW-156.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-156: Add successful provisioning-envelope verification through PartyDomainProcessor with emitted events and enriched result assertions.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:157-159; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:22
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Verification 1, pre-verified regression gap: aggregate tests supply hand-built Authorization and bypass the processor; processor tests cover only unsupported-policy rejection. Removing trusted Authorization injection can break registered command dispatch without those tests failing. Existing identity-feature paths from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: accepted provisioning envelope yields creation/marker events and matching result identity at the processor boundary.
status: done 2026-10-21
resolution: Duplicate of DW-137; registered production dispatch and enriched result/retry assertions satisfy the original boundary proof.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-138: Isolate wrong-purpose human custody rejection

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs:67
severity: medium
status: done 2026-10-21
resolution: Current human evidence now has a fixed-clock valid control plus independent wrong-purpose and expired-interval cases, each asserting bounded 503 failure. This also resolves duplicate DW-157.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-157: Separate expired-interval and wrong-purpose client rejection cases.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs:67
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Verification 2, pre-verified broken-verification gap: the current case combines an expired interval with other-purpose, so removing the purpose check still rejects on expiry. Existing identity-feature test from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: otherwise valid current human evidence with the wrong purpose is rejected independently, and expired evidence with the correct purpose is rejected independently.
status: done 2026-10-21
resolution: Duplicate of DW-138; the same valid control and independently corrupted purpose/interval cases supply its exit proof. Counted once in the five-item batch.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-139: Wire FrontComposer reserved segments and module landing in package mode after a Shell release

origin: Story 8.10 final identity reconciliation (2026-10-05); follow-up of resolved DW-124
location: src/Hexalith.Parties.UI/Program.cs; src/Hexalith.Parties.UI/Components/Routes.razor; src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor; src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor.cs; src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj; references/Hexalith.Builds/Props/Directory.Packages.props
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: low
reason: Commit `47e2da3244fd7d6d14e39bc30513b23504e9bc27` cleared DW-124's package build failure by compiling the `FrontComposerRouteOptions.ReservedSegments` wiring (`admin`, `me`, `no-party-binding`) only under `HFC_ROUTE_OPTIONS`, which is defined only when `HexalithFrontComposerFromSource=true`. The package-mode graph (CI, bUnit, and the released `parties-ui` container) therefore registers no reserved segments, and packaged Shell `4.5.0` carries no `FcModuleLandingPage`, so the module-landing route surface stays source-only. Since 2026-10-05 (DW-141 resolution) the same gate also covers `PartiesOverview.razor`: its `FcPageTabs` `ModuleRoute="/parties"` and `DefaultTabId="overview"` parameters, which packaged Shell `4.5.0` lacks, are passed only under `HFC_ROUTE_OPTIONS` through the `ModuleRouteTabAttributes` `@attributes` dictionary in `PartiesOverview.razor.cs` (empty in package mode). Exit proof: a published `Hexalith.FrontComposer.Shell` release containing both types and the `FcPageTabs` `ModuleRoute`/`DefaultTabId` parameters, a Builds catalog bump selecting it, removal of the `HFC_ROUTE_OPTIONS` gate (in `Program.cs` and in `PartiesOverview.razor.cs`, with `PartiesOverview.razor` passing `ModuleRoute`/`DefaultTabId` directly), a green package-mode `Hexalith.Parties.UI` Release build and `PartiesOverviewTests`, a test pinning the reserved-segment set, and matching 8.3 G4 and identity rows. Rollback: keep the source-mode gate (the current state). Bound to the `8.9-frontcomposer-ui-consolidation` slot (G4 surface).
status: done 2026-10-22
resolution: The already-selected published Shell 4.7.0 exposes reserved route options, module landings and route-backed tabs. Removed HFC_ROUTE_OPTIONS and its attributes dictionary; PartiesOverview passes ModuleRoute and DefaultTabId directly. Published-package Release build, actual host collision/route/role checks and default-tab fallback checks pass. Current routing proof is appended to the Story 8.3 matrix; historical approvals and broader G4 prerequisites remain unchanged.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

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
status: done 2026-10-21
resolution: The existing Test-only shell scope is now qualified through actual host DI with seven cases covering admin, Consumer, anonymous, both fixture wrappers and route delegation. All three portal/picker specs execute successfully in the Release/package browser run. Production authentication remains unchanged.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-144: Require the stamped identity in every canonical closure receipt, not only Playwright

origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 4 and L3 Edge 16
location: tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs (DescribeReceiptGaps, DescribeAccessibilityStampGaps)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: The closure gate checks the selected FrontComposer SHA only in the Playwright accessibility evidence. The warning-policy, Release build, all-tests, package/consumer, and npm rows accept any exact `Pass`, so a row left over from a superseded identity would satisfy closure. That contradicts I16's rule that a receipt counts only when rerun at the stamped identity. Pre-existing: Parties `c782b68c` already had the Playwright-only check. Exit proof: each required row must carry a machine-checkable stamp of the final set (for example the EventStore source SHA or a named final-set token) and the Parties tree, with rejection cases. Rollback: keep the current Playwright-only stamp check.
status: done 2026-10-21
resolution: Every required passing closure receipt requires exact row-local Parties snapshot and dependency-set digests, with negative and actual filesystem/Git regressions. Current selected FrontComposer is checked independently. Only canonical receipt payload and three explicit closure bookkeeping values are normalized; all other content remains bound. Historical passes are preserved; current full closure rows are Unvalidated, Story 8.10 is in-review and Epic 8 in-progress pending genuine full requalification.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

### DW-145: Extend the personal-data inventory to the identity-feature contracts

origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 5
location: tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs (s_inspectedTypes)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: `s_inspectedTypes` omits contracts that carry actor identities: `EstablishHumanActorBinding`, `RebindHumanActorBinding`, `RevokeHumanActorBinding`, `ResolveHumanActorBindingAt`, `HumanActorBindingEvidence`, and the nested `HumanActorBinding`, `HumanActorBindingTransition`, `AgentPartyProvisioningResult`, and `AgentPartyIdentity` records. Members of the `[PersonalData]`-marked `PartyState.AgentProvisioning` are neither marked nor classified. All of these were added by `37d87f5a2869b076c651a58714d60f64647848bc`, before this delta; DW-142 classified only the six properties that failed. This is an I8 decision for the Parties GDPR owners, related to DW-129. Exit proof: every identity-feature contract type is inspected, and each property is classified with its rationale. Rollback: revert the added inventory rows.
status: done 2026-10-22
resolution: The inventory inspects identity commands, queries, events and nested binding, provisioning, response, admission and custody records. Every property has an explicit contextual classification and rationale, with completeness checks for identity roots and nested records. Opaque linkages remain sensitive; transient proofs/authorization, finite identity-history custody and profile-protected provisioning are distinguished. Production annotations remain unchanged; focused inventory passes 9/9 and the full Contracts suite passes 211/211.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

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
status: done 2026-10-21
resolution: Preflight inventories all seven AppHost external runtime roots and their 24-project closure in both Debug and Release, comparing EventStore packages against independently evaluated Parties selection. Missing references, package overrides, hidden source selection, unsupported edge contexts, unexpanded multi-target graphs and nonliteral resolver paths fail explicitly; cycles are bounded. This verifies graph selection, not compilation or runtime parity.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

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
status: done 2026-10-22
resolution: The persistent verifier downloads each of the nine exact declared packages, reuses archive and dependency-boundary checks, and requires exact nuspec ID/version/repository URL/type/commit. Receipts hash observed registry bytes without asserting equality with unsigned build archives.
verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.

### DW-162: Commit the existing Story 8.8 available-row identity gate correction through its owning work.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md"), 2026-10-13
location: Story 8.8 spec available-row identity heading; AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity
source_spec: `_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md`
reason: The committed isolated integration run fails AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity because spec-8-8 lacks the required heading; the main working tree already contains the 10-line correction as separate user work and this task preserves it unstaged.
status: done 2026-10-21
resolution: The previously separate ten-line Story 8.8 available-row heading correction was committed at 890119f1b4ced37ccec34787c812f064199fb3ef and remains intact. AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity and identity-gap cases pass. Frozen story intent and canonical sprint status are unchanged.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-163: D1 — Preserve coordinated Detail/Processing slots when recording an unresolved first projection delivery.

status: done 2026-10-18
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-164: D2 — Set an explicit SDK receiver identity for the optional sample resource matching its DAPR app ID.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.AppHost/Program.cs:237 (optional sample resource)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: AppHost sample at Program.cs:237 lacks the parties/tenants AppId binding; SDK fallback uses ApplicationName when DAPR_APP_ID is absent in the observed child path; verified medium, pre-existing.
status: done 2026-10-21
resolution: The optional sample explicitly receives EventStore__DomainService__AppId=sample. Actual run and publish model regressions compare it with the attached Dapr sidecar app identity.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-165: D3 — Resolve the optional Memories resource endpoint for local Aspire runs.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.AppHost/Program.cs:217 (optional Memories run-mode endpoint)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Program.cs:217 supplies http://memories:8080/ in run mode although Memories is a host project with an Aspire-assigned endpoint; verified medium, pre-existing.
status: done 2026-10-21
resolution: Memories search now receives the optional resource HTTP EndpointReference in both run and publish models. Regressions resolve two different allocated addresses and ports through that same reference and preserve the dependency wait and enablement.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-166: D4 — Align Memories cleanup, compensation and health probing with the versioned server route without accepting a routing 404 as proof of erasure.

status: done 2026-10-18
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

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

status: done 2026-10-18
origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
archived: 2026-10-21

### DW-170: D8 — Preserve durable reconciliation for cancellation after Memories accepts ingest but before the Parties mapping persists.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties/Search/PartyMemoryIndexingService.cs (mapping-write cancellation boundary)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Indexing rethrows caller cancellation at the mapping-write boundary without compensation, while the server workflow can continue producing a unit; verified high, pre-existing.
status: open

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

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs; PartyIdentitySourceFold.Fold; authority.Admit
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `authority.Admit` and `PartyIdentitySourceFold.Fold` execute between deadline checks, so a stalled provider or large source can exceed the configured budget; both paths predate the release patch.
status: done 2026-10-22
resolution: Initial and final authority admission, both folds and retained-source validation run within the original whole-query deadline. Internal fold overloads check cooperative cancellation between transitions. Controlled stalls, cancellation precedence, exact-position success and terminal historical source-expiry/authority-revocation regressions pass; final focused identity suite passes 267/267.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

### DW-175: Bound noncooperative identity-query provider work after a caller deadline.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryDeadline.cs (ReadAsync)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartyIdentityQueryDeadline.ReadAsync` stops awaiting a stalled provider task but leaves it running; repeated calls can accumulate worker tasks. The deadline work was introduced separately from this release patch.
status: done 2026-10-22
resolution: One finite process-wide limiter admits at most 16 current/historical queries without a waiting queue. Each query retains its lease until abandoned work and asynchronous provider cancellation callbacks both finish; late faults are observed. Controlled saturation, budget-acquisition and both completion-order recovery cases pass. Noncooperative work is bounded, not terminated; final full service suite passes 2421/2421.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

### DW-176: Make long retained human-actor history folds linear.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/RetainedHumanActorHistoryFold.cs (Append and Close)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `RetainedHumanActorHistoryFold.Append` scans prior bindings and `Close` searches by version for each transition; the scans existed at this spec's baseline.
status: done 2026-10-21
resolution: The retained fold validates the expired-prefix boundary, concatenates already ordered partitions and checks only the last binding on append/closure. A 4096-binding history preserves first/middle/final intervals and original source positions; late backdating and empty intervals reject. Existing sparse/expired/revoked lifecycle coverage remains green.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-177: Show accepted-command acknowledgement before slow admin list loading finishes.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor (LoadPageAsync and route selection)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesAdminPortal` sets its updating notice only after awaiting `LoadPageAsync` and route selection; this ordering predates the release patch.
status: done 2026-10-21
resolution: Accepted receipts render a polite notice before capability/list/detail reads, including unchanged authorization contexts. A new receipt starts a new list version; delayed reads cannot restore a cleared notice. Covered by pending-read, pre-receipt, supersession and route/authentication regressions. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-178: Clear the admin updating notice when the saved party reaches the projection.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor (_acceptedCommandPartyId)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesAdminPortal` clears `_acceptedCommandPartyId` on navigation and context reset but has no convergence clear, so the notice can remain after the row appears; this behavior predates the release patch.
status: done 2026-10-21
resolution: Current healthy matching rows clear the notice; wrapper degradation, every non-current payload freshness state, unrelated/empty pages and superseded reads retain it. List/search freshness is preserved. This closes matching-row visibility only; command-specific edit convergence is tracked separately below. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-179: Restore the erasure confirmation action after a dialog open failure.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor (_pendingErasureDialogOpen, ShowAsync, _confirmErasure)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartyGdprOperationsPanel` consumes `_pendingErasureDialogOpen` before `ShowAsync` and catches `JSException` while `_confirmErasure` keeps the action button replaced; this is later UI work.
status: done 2026-10-21
resolution: A failed Fluent dialog opening resets typed confirmation, restores the request action and announces a bounded transient failure without an older correlation. Repeated failures refocus the announcement; input-focus failures keep an opened dialog usable. Retry still requires fresh exact confirmation and sends only the explicitly confirmed command. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-180: Reproduce whether immediate blob URL revocation loses a supported-browser privacy export.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.UI/wwwroot/admin-privacy-export.js
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `admin-privacy-export.js` revokes the object URL immediately after `anchor.click()`; a supported-browser download trace would settle whether the scheduling can cancel the download. Unverified medium in later UI work.
status: done 2026-10-21
resolution: Supported Chromium completes the real erased-party export despite immediate object-URL revocation. The browser regression verifies exact bytes, JSON MIME type, bounded valid UTC filename, object-URL creation/revocation and retained digest metadata. No speculative download timing change or cross-browser guarantee is introduced.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-181: Resolve the loop-sweep ledger fallback through layered BMAD configuration.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: .agents/skills/bmad-loop-sweep/SKILL.md (Step 1 ledger fallback); layered BMAD configuration
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: With `BMAD_LOOP_LEDGER` absent, the skill reads legacy YAML despite describing `config.toml` layering, so a custom artifact location can be missed; this is agent-context work outside the release patch.
status: done 2026-10-21
resolution: Already resolved by installed sweep update 777e0e7d0c33dad7230e6477d1c3987a4b843b5f. Both installed entry points require the orchestrator-resolved BMAD_LOOP_LEDGER, never re-derive it, and escalate missing/unreadable paths. No legacy fallback or speculative TOML fallback was added; config-trace.py reproduces the installed contract.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

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
status: done 2026-10-21
resolution: A reason-only empty degraded search regression asserts retained grid rows, bounded polite degraded status, no empty state and no raw backend reason. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-184: Assert accessible checked state after switching the admin party-type radios.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: admin party-type radio aria-checked expressions and component/browser tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The new `aria-checked` expressions are not asserted by component tests or the browser test, which only checks radiogroup visibility; an inverted expression could misannounce the type. This is later UI work.
status: done 2026-10-21
resolution: The existing party-type switching regression now checks both rendered aria-checked values initially and after switching to Organization, back to Person and again to Organization. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

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
status: done 2026-10-21
resolution: Investigated; no installed tracked non-reference CIS consumer requires an array. Runtime layered resolution preserves the JSON-string setting, and the renderer accepts that scalar while rejecting a native array token. Both new configuration tests pass, including every override layer. Preserve installer-managed configuration; this closes the unverified concern without claiming a CIS consumer run.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-187: Test authenticated actor and tenant-delivery callbacks through the real service route.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: tests/Hexalith.Parties.Tests/Gateway/PartiesDomainServiceSecurityTests.cs; actor and /tenants/events service callbacks
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesDomainServiceSecurityTests` checks denial and endpoint policy metadata, but no owned test sends a valid channel request to the actor or `/tenants/events` handler. A later HTTP security change could block legitimate delivery without a failing test.
status: done 2026-10-21
resolution: Real production HTTP routes admit channel-only tenant delivery and registered PartyKeyRetryActor PUT calls. Tests inspect committed tenant membership/access, message deduplication, actor pending-state writes/deletion, reactivation read-back and real reminder callbacks through a private HTTP backend; missing, wrong and duplicate actor/tenant channel credentials reject. No runtime or authorization handler is replaced.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-188: Exercise the release registry-floor comparison with isolated published-version fixtures.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: .github/workflows/release.yml (registry-floor parts > floor comparison); CI tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: Current CI tests inspect `.github/workflows/release.yml` text and step ordering but do not run its `parts > floor` comparison; a mutation that admits a stale tag could reach the protected release job. This gate was added after the original Parties release patch.
status: done 2026-10-21
resolution: The CI assembly executes the exact registry-floor workflow bash with isolated GitHub/NuGet fixtures; numeric ordering, equality, prerelease and four-component behavior, invalid indexes and request failures are verified without publication.
verification: `_bmad-output/implementation-artifacts/tests/five-package-validation-deferred-items-2026-10-21/validation.json`; spec `spec-five-package-validation-deferred-items.md`.

### DW-189: Run the existing contact-selector browser test in required CI.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: contact-selector Playwright create-form test; component FluentSelect.ValueChanged tests; configured CI workflow
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: Component tests invoke `FluentSelect.ValueChanged` directly while the Playwright create-form test is outside the configured CI workflow. Removing the rendered `FluentOption` children could pass required tests but leave users unable to select a contact type. This is later UI work.
status: done 2026-10-21
resolution: The unconditional blocking UI job runs the full admin, Consumer and picker specs in one Playwright invocation alongside the accessibility specimen, including actual contact Email option selection. CI contract tests preserve required execution and existing check identity; a Release/package browser run passes.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-190: Require the FrontComposer shared-status structural command and test its missing-command outcome.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
location: RequiredEvidenceTokensBySurface and RequiredDeliveredApiCommands (FrontComposer FcSurfaceStatus command)
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
reason: Existing RequiredEvidenceTokensBySurface and RequiredDeliveredApiCommands do not mandate the FcSurfaceStatus command; removing an individual FrontComposer command leaves other searches satisfying the row's nonempty-command guard. This gap predates the current conditional-expression refresh.
status: done 2026-10-21
resolution: FrontComposer required tokens now include FcSurfaceStatus and its structural aria-live expression. RequiredDeliveredApiCommands mandates the exact positive command/path; command-removal and wrong-path/negative-search cases protect this requirement.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-191: Validate McpCli and Platform source-selection rows independently of the eight consumed dependency fitness checks.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
location: CurrentReleaseGitlinks; McpCli and Platform source-selection documentation rows
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
reason: Existing CurrentReleaseGitlinks excludes these non-consumed root selections even though the current documentation table records them; release gitlink signing protects changed pointers, while direct table identity coverage remains future hardening.
status: done 2026-10-21
resolution: All ten current root selections, including non-consumed McpCli and Platform, match current table rows, committed gitlinks, clean checkouts and exact Git describes. Eighteen independent row-boundary cases and two standalone real Git-receipt cases cover both non-consumed dependencies. Describes must now occur in the identity cell; historical/prose mentions cannot repair stale identity evidence. No source pointer, owner approval or historical receipt changed.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-192: Derive erasure status from committed events or reconcile it after event persistence; cover failed append and retry before reporting Verified or Erased.
origin: retro action item epic-8-retro-erasure-status-after-commit
location: n/a
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
reason: retrospective action item from sprint-status.yaml (epic 8; owner Amelia (Developer) + Winston (Architect) + Murat (Test Architect); sprint-status status open)
status: open

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

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
  summary: Propagate the topology startup token through its final HTTP health waits.
  evidence: Both independent three-minute health waits lack the overall startup cancellation token at the original release baseline, so total startup can exceed the declared five-minute budget.

  status: done 2026-10-21
  resolution: Already implemented by the current fixture: one linked five-minute startup token reaches both final HTTP health waits, in-flight GetAsync and poll delays. All six existing fixture cases pass, including pending-request cancellation, environment restoration, owned control-plane cleanup and retry after failed cleanup. No extra fixture abstraction or runtime-topology qualification is claimed.
  verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

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

status: done 2026-10-16
origin: DW-122 baseline reproduction
source_spec: `_bmad-output/implementation-artifacts/spec-dw-122-production-validation.md`
severity: medium
archived: 2026-10-21

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

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-72-production-publish-environments.md`
  summary: Reconcile current dependency package documentation and source-selection receipts after the concurrent root-gitlink sync.
  evidence: After rebasing onto bec09f01aa00bd9ef83554fd099ed36fb59ab2c6, the complete service suite passes 2001/2003. MaintainedTechnologyTablesMatchCentralPackageCatalog expects docs/ci.md to describe EventStore 3.126.0 selected by the new Builds catalog, and FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph still expects Builds 332b2c144defba546fe0a09626a4a72f2340e22f while the parent selects 5d03f1db9c38db34ecac7fe6048937478ffb82cc. Both unchanged methods fail identically on the exact parent checkout; commands, results and matching test-source blob identities are retained in the DW-72 evidence. Refresh current observations without weakening gates, fabricating owner approval or recertifying historical parity evidence.
  status: done 2026-10-22
  resolution: The existing reconciliation is verified with fresh Release/package binaries at d55058ddaa4bfc94c47b799e5f5d9e9eff8dce77. MaintainedTechnologyTablesMatchCentralPackageCatalog and FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph both pass (2/2); the full service assembly passes 2421/2421. Selected EventStore 3.126.1, Builds 3fcb8f38919bb0961b06a19b57aa45623b35a33e and FrontComposer a258b992d08e73e2d3251c865ef01a982eba31ac agree with maintained observations. No dependencies, gitlinks, historical receipts or owner approvals change.
  verification: `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`; spec `spec-five-identity-routing-deferred-items.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-86-erasure-certificate-validation.md`
  summary: Provide an owner-supported recovery path for legacy erasure certificates.
  severity: medium
  evidence: Earlier cleanup retries overwrote key-destruction certificate status with Pending/Failed, indistinguishable from genuine key deletion failures. DW-86 must reject that ambiguous evidence; the current Parties gateway exposes no supported reissuance/persistence command. Key-management owners must qualify authoritative read-back and auditable certificate replacement before legacy retries can be certified. Never manually promote status or accept failed evidence.

- source_spec: `_bmad-output/implementation-artifacts/spec-dw-90-bounded-test-processes.md`
  summary: Contain test-process descendants that inherit output pipes after their direct parent has already exited.
  severity: medium
  evidence: Independent DW-90 review reproduced the same orphan in the baseline shared package runner and the consolidation candidate. Once the direct parent exits, Process.Kill(entireProcessTree: true) cannot identify that orphan; bounded output capture still fails promptly. The inherited-pipe regression explicitly owns and terminates its descendant. Qualify an appropriate process-group/job strategy separately before claiming orphan cleanup.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-admin-deferred-items.md`
  summary: Bind accepted edit convergence to a command-specific projection witness or expected edited state.
  severity: medium
  evidence: DW-178 deliberately closes matching-row identity visibility. An existing party already has that identity, so a healthy Current list/search response can contain its pre-edit row before the accepted update projects. The accepted receipt currently carries only party ID and authorization context, without a command/version witness. Matching identity must not be advertised as proof of the latest edit; qualify an owner-supported witness and an edit-specific stale-value regression separately.


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

- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-five-query-release-deferred-items.md`
  summary: The publication job's production environment is not isolated from the release job's environment assertion.
  evidence: The release job already contains environment production, so removing it from verify-publication still satisfies that assertion. GitHub is what enforces the environment rule.
  status: done 2026-10-22
  resolution: Both credential-bearing jobs are independently extracted and required to declare production. The negative control removes only verify-publication protection while release remains protected and must fail; the reciprocal mutation is also rejected. This checks declarations locally and does not claim execution of GitHub environment protection.
  verification: `tests/five-runtime-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-runtime-release-deferred-items.md`.
