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

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md"), 2026-09-06
location: scripts/test.ps1 and CI test-shard loops
source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
reason: `scripts/test.ps1 -Lane all` and each CI shard currently stop at the first failing project, so a package-mode restore blocker can hide later project-specific failures until the first blocker is resolved. The legacy ledger recorded status resolved and resolution Story 8-11 (sprint-change-proposal-2026-07-07-validation-ladder-runner.md). `scripts/test.ps1 -ContinueOnFailure` runs every project and prints a PASS/FAIL summary (exit 1 if any failed); the CI `Run test shard` loop continues after a failing project and summarizes all failures. Default fail-fast behavior preserved, but the authoritative migration manifest requires this entry to remain open.
status: open

### DW-4: Add inspectable local test output and property forwarding

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md"), 2026-09-06
location: scripts/test.ps1
source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
reason: CI writes TRX/coverage artifacts and some local blockers require properties such as `UseHexalithProjectReferences=true`, but the local lane runner currently exposes neither a results-directory/logger option nor a safe property-forwarding interface. The legacy ledger recorded status resolved and resolution Story 8-11 (sprint-change-proposal-2026-07-07-validation-ladder-runner.md). `scripts/test.ps1 -ResultsDirectory <path>` emits a per-project TRX (local CI parity) and `-Properties <k=v>,<k=v>` forwards each value as `-p:<value>` to `dotnet test`, but the authoritative migration manifest requires this entry to remain open.
status: done 2026-09-06
resolution: already resolved: scripts/test.ps1:12-18 exposes ResultsDirectory and Properties; scripts/test.ps1:25-30 creates the requested result root; scripts/test.ps1:51-64 forwards per-project TRX/results and MSBuild properties.

### DW-5: Define a safe consent and channel identifier contract

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md"), 2026-09-06
location: RecordConsent and RevokeConsent command contracts
source_spec: `_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`
reason: `RecordConsent` and `RevokeConsent` currently accept `ChannelId`/`ConsentId` values that can contain legacy `channel:purpose` separators, so applying the new `PartyIdentifier` semantic-ID helper would break existing consent IDs while leaving aggregate not-found messages able to echo raw consent/channel identifiers.
status: open
decision: 2026-09-06 Separate compatible validators — Add separate channel-segment and legacy-composite consent validators, preserve existing stored IDs, and replace unsafe error detail.

### DW-6: Correct and validate the advanced Hexalith.Builds checkout

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
location: references/Hexalith.Builds
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
reason: Checkout `63d3221` supplied `v1.16.3` as a NuGet version and caused Actions runs `29467970597` and `29468665570` to fail during restore. Builds `v4.18.11` corrected the value to `1.16.3`; commit `6516faf` adds the evaluated central-version release guard and fixtures. Builds `v4.19.0` retains both changes and adds the MTP-compatible shared test contract exposed by follow-up run `29482004796`; the Parties gitlink/signoff adopt that release. The legacy ledger recorded status resolved and resolution `_bmad-output/implementation-artifacts/spec-gh-29467970597-fix-invalid-builds-package-version.md`; Hexalith.Builds `640b59c1434e4e1e079771c401e11048772c7a27` (`v4.19.0`), but the authoritative migration manifest requires this entry to remain open.
status: done 2026-09-06
resolution: already resolved: The root and checkout both select references/Hexalith.Builds commit 8db7459d065926501ee045b3aaf7b816780905e5, exact tag v4.27.1, superseding the verified v4.19.0 fix; root history also contains a97530d2 and 9377c572 for catalog/test-evidence corrections.

### DW-7: Test persisted LRU eviction after restore

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
location: references/Hexalith.Memories persisted-LRU tests
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
reason: Incidental review found the new workflow recency field is tested across serialization and eviction separately, but not after serialize/restore at the 256-entry limit; a restored actor could evict a recently refreshed workflow and reapply a delayed transition.
status: open

### DW-8: Test intermediate Memories state migration

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
location: references/Hexalith.Memories migration tests
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
reason: Incidental review found no test for persisted state containing `AppliedTransitionSequences` while lacking the newer `AppliedTransitionWorkflowOrder`, leaving the immediate predecessor format's eviction queue reconstruction unverified.
status: open

### DW-9: Deliver granular EventStore.Client registration and coexistence proof

origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
location: EventStore.Client registration across Parties and FrontComposer
source_spec: none
reason: This independently shippable EventStore.Client package change was split from the G8 owner-proof action so the EventStore.Aspire JWT prerequisite can be completed first.
status: open
decision: 2026-09-06 Add EventStore extensions — Add independently selectable EventStore.Client extensions with idempotency, order, and coexistence tests, then adapt consumers while retaining generic registration.

### DW-10: Deliver integrated AppHost topology parity proof

origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
location: FrontComposer.AppHost or approved platform AppHost
source_spec: none
reason: This independently shippable platform-host change was split from the G8 owner-proof action because it depends on the EventStore Aspire and client-registration surfaces being proven first.
status: open

### DW-11: Deliver the external runtime deployment handoff

origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
location: external platform-operations owner repository
source_spec: none
reason: This independently governed operational handoff was split from the G8 owner-proof action because it requires platform-owner coordination after local run and publish parity are established. Also recorded in "Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18": deferral_id: `external-runtime-deployment` authored_by_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md` owner: `External platform-operations and deployment owners` exit_proof: `Consume immutable Parties image tags and provide environment-specific DAPR components, subscriptions, resiliency, deny-default access control, ingress, secrets, registry credentials, signing/scanning, and promotion evidence in the owner repository; prove rollback to the prior immutable image set.` rollback: `This repository keeps workload source, CI, immutable image publication, and the local Parties AppHost migration rollback topology. Runtime rollback remains an external orchestrator operation that redeploys the prior immutable image set and platform configuration.` evidence: `docs/deployment-guide.md and the Epic 8 architecture spine assign runtime deployment outside this repository; Story 8.13 retired the historical in-repo deploy assets, which must not be restored.`
status: open

### DW-12: Enforce multi-audience JWT validation at runtime

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
location: EventStore.Aspire and consuming host authentication
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
reason: This was split because the reusable EventStore.Aspire composition surface can ship independently before each host authentication configurator adopts ordered valid audiences.
status: open
decision: 2026-09-06 Add owner-level audiences — Add ordered ValidAudiences to owner options and configurators, retain Audience as the primary backward-compatible value, and test the acceptance and rejection matrix.

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

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md"), 2026-09-06
location: /admin/operational-index-metadata DAPR ACL
source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
reason: Incidental review of concurrent ACL edits found that independent string assertions can pass when `/admin/operational-index-metadata` is placed under the wrong app policy, verb, or action.
status: done 2026-09-06
resolution: already resolved: Commit 02ccd3176 added structural ACL parsing; tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:317-377 asserts the single EventStore policy, exact route inventory, POST verb, and allow action.

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

origin: migrated from legacy ledger (flat source_spec "/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md"), 2026-09-06
location: semantic-release workflow
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
reason: `npx semantic-release` predates this change and may fetch if local tooling is absent; resolving it requires a separate release-workflow policy decision.
status: open
decision: 2026-09-06 Use npm exec no — Invoke semantic-release through npm exec --no so only the installed dependency can run.

### DW-19: Hoist multi-token search candidate normalization

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
location: EvaluateEntry multi-token search hot path
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
reason: `EvaluateEntry` rebuilds the query-only full phrase and candidate collection for every party, creating O(entries) allocations in the 10K hot path despite the current performance gate passing.
status: open

### DW-20: Normalize full-phrase multi-token relevance coverage

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
location: multi-token relevance scoring
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
reason: A deterministic full-phrase match is added alongside real query tokens, so coverage can exceed one before the final score is clamped and can inflate ordering relative to token-only matches.
status: open

### DW-21: Make Party SDK erasure writes atomic

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
location: PartySdkReadModelEraser.EraseAsync
reason: Three independent `UpdateAsync` calls in `PartySdkReadModelEraser.EraseAsync` can leave detail/processing/index mutually inconsistent on mid-flight failure — no multi-key transactional write seam in the approved `ReadModelWritePolicy` API.
status: done 2026-09-06
resolution: already resolved: Commit 02ccd3176; src/Hexalith.Parties.Projections/Services/PartySdkReadModelEraser.cs:13-20,56-95 writes detail, processing, and index through one ReadModelBatch and retries optimistic conflicts.

### DW-22: Define idempotent ErasedAt semantics for retries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
location: PartySdkReadModelEraser.ApplyErasure
reason: Optimistic concurrency retries re-run `ApplyErasure` and refresh `ErasedAt` — `ApplyErasure` always stamps `UtcNow`; short-circuiting on `IsErased` needs a deliberate idempotency contract change.
status: done 2026-09-06
resolution: already resolved: Commit 02ccd3176; PartySdkReadModelEraser.cs:40,103-119,127-140,149-171 freezes cleanup time and preserves first-erasure metadata, with retry stability proven in PartySdkProjectionHandlerTests.cs:1788-1860.

### DW-23: Define erasure freshness metadata semantics

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
location: PartySdkReadModelEraser detail and index freshness metadata
reason: Erasure copies through pre-erasure `ProjectedAt`/`ProjectionVersion` on detail/index — stamping erasure-time freshness is entangled with the open AC7 freshness-mapping gap; index timestamps also cover unrelated remaining parties.
status: done 2026-09-06
resolution: already resolved: Commit 02ccd3176; PartySdkReadModelEraser.cs:116-119,137-140,160-171 stamps ProjectedAt on first cleanup, preserves it on retries, and retains ProjectionVersion; PartySdkProjectionHandlerTests.cs:1741-1762 and 1835-1860 pin the semantics.

### DW-24: Bound missing-CaseId warning deduplication

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: s_caseIdMissingWarned
reason: `s_caseIdMissingWarned` is an unbounded static ConcurrentDictionary (one entry per tenant/party for process lifetime) — mirrors retired orchestrator pattern.
status: open

### DW-25: Document Party index ProjectionVersion semantics

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: Party index projection fold and ProjectionVersion
reason: Index `ProjectionVersion` scheme (`global:N` / `{id}:{seq}` / keep-current) lacks Fold/class remarks for freshness/query consumers.
status: open

### DW-26: Define stable Article 30 operation categories

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: GetOperationCategory
reason: `GetOperationCategory` default arm returns a short event-type name rather than a stable category vocabulary — Art.30 taxonomy design choice.
status: open
decision: 2026-09-06 Use stable Other — Map unknown events to a stable Other category and retain EventType separately for bounded diagnostics.

### DW-27: Report invalid PageSize separately from InvalidCursor

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
location: Paging.PageSize validation
reason: Out-of-range `PageSize` under `Paging` rejected as `InvalidCursor` even with no cursor — debugging misdirection only.
status: open
decision: 2026-09-06 Add invalid-page sentinel — Add and map an InvalidPage failure reason, distinguish cursor failures from paging validation, and cover EventStore and Parties compatibility.

### DW-28: Harden query compatibility shim lifecycle and state handling

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
location: Party SDK query compatibility shims
reason: Non-durable unbounded in-process last-known cache; no `ApplicationStopping` link; Actor-named constant bags; missing-detail vs empty-processing asymmetry — intentional shim/architecture trade-offs from the first Group 3 pass.
status: done 2026-09-06
decision: 2026-09-06 Document bounded memory — Retain the bounded in-memory cache and document shutdown and restart loss as intentional until a durable successor exists.
resolution: closed by human decision: Retain the bounded in-memory cache and document shutdown and restart loss as intentional until a durable successor exists.
decision: 2026-09-06 Document bounded memory — Retain the bounded in-memory cache and document shutdown and restart loss as intentional until a durable successor exists.

### DW-29: Preserve the cursor codec failure reason

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
location: PartySdkQueryService.LogCursorRejected
reason: The cursor codec `failureReason` was discarded before rejection logging.
status: done 2026-08-03
resolution: Resolved by `LogCursorRejected` in `PartySdkQueryService`.

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
status: open
decision: 2026-09-06 Always checkpoint only — Treat every root json-redacted payload as checkpoint-only regardless of CLR shape and add compatibility tests.

### DW-36: Align reconciliation LastIndexedEvent with the canonical fold

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.BuildReconciliationFold
reason: `PartyIndexSdkProjectionHandler.BuildReconciliationFold` recomputes `LastIndexedEvent` via a separate code path from the canonical `FoldCore`, used only on the already-confirmed idempotent-no-op/reconciliation branch — could pick a different "last event" for search-reconciliation notification metadata on a multi-event no-op batch, but doesn't affect canonical read-model correctness. Needs a dedicated multi-event test to pin the intended behavior.
status: open

### DW-37: Test blocked Memories cleanup without a CaseId

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyMemoryCleanupService
reason: `PartyMemoryCleanupService`'s new "no persisted CaseId and no fallback configured" blocked branch has zero test coverage.
status: open

### DW-38: Harden rebuild completion against null manifest collections

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.CompleteRebuildAsync
reason: `PartyIndexSdkProjectionHandler.CompleteRebuildAsync` would throw `NullReferenceException` (not a controlled result) if a persisted rebuild-completion manifest ever deserializes with null `Entries`/`RemovedPartyIds`. Not reachable under the current producer (`FinalizeAsync` always serializes non-null arrays); hardening-only.
status: done 2026-09-06
resolution: already resolved: PartyIndexSdkProjectionHandler.cs:291 and :304 normalize null RemovedPartyIds and Entries to Array.Empty; introduced by commit 6fac8309.

### DW-39: Refresh the G5 matrix validation-evidence identity

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible and Story 8.3 G5 matrix row
reason: `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible` is RED: it hard-pins the Story 8.3 matrix's "Payload protection engine package" (G5) row to EventStore `v3.89.0`/`7854f8e5`, but the working tree is now at `v3.91.0`/`1d6e9321` (this story's resolved EventStore identity). Pre-existing to this review session, not caused by its patches. Out of Story 8.6 scope — G5 payload-protection is Story 8.7's territory and needs its own owner-reviewed identity-authorization update, not a Story 8.6 patch.
status: done 2026-09-06
resolution: already resolved: PlatformApiPrerequisitesTests.cs:29-30 pins the current EventStore identity, while :1333-1349 validates the current gitlink, checkout, matrix ledger, and package identity; refreshed by commit e95d2f82.

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

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.FinalizeAsync
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review found the defect (blind `ReadModelBatchConcurrency.LastWrite` can drop a canonical entry added mid-rebuild) exists both as a still-unchecked `[ ]` Group 2 task and, separately, inside a `[x]`-checked "Fixed 2026-08-04 (partial)" bullet in `8-6-projection-and-query-sdk-migration.md` that itself states the underlying issue is "left open — not addressed by this patch." The two are never cross-referenced, and the defect was never logged here, so it is invisible to anyone scanning only this ledger. Also recorded in "Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)": The 2026-08-05 human-directed session restored operator diagnostic logging and fold-level tests, deferred the remaining findings, and recorded that this item fulfills the earlier tracker-reconciliation action. `PrepareRebuildAsync`/`FinalizeAsync` write with `ReadModelBatchConcurrency.LastWrite` (no ETag check) [`PartyDetailSdkProjectionHandler.cs:99,103`, `PartyIndexSdkProjectionHandler.cs:102`] — a rebuild finalize can silently overwrite a newer concurrent live `ProjectAsync` write with no conflict detection. Investigated 2026-08-05: switching to `Match(etag)` unilaterally is unsafe without knowing the EventStore SDK rebuild-plan executor's retry/abort contract on a write conflict — that contract lives in `Hexalith.EventStore.DomainService`'s rebuild orchestration, outside this repo's `IAsyncDomainProjectionRebuildHandler` / `IAsyncDomainSharedProjectionRebuildCompletionHandler` surface. Needs SDK-owner input, not a unilateral Parties-side change. Resolved 2026-08-16: EventStore v3.95 now provides the required bounded conflict contract. Parties rebuild plans use `Match(etag)` for existing rows and `CreateOnly` for absent rows; focused plan-policy tests pass for detail, processing, and index.
status: done 2026-09-06
resolution: already resolved: PartyIndexSdkProjectionHandler.cs:248-250 uses ETag Match/CreateOnly finalization, and :282-309 rereads canonical state before notifications; implemented by commit 8e3953e0.

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
status: open

### DW-47: Give projection-fold drop diagnostics stable logger categories

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold and PartyProcessingActivityFold diagnostics
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — drops detected inside the shared static helpers `PartySdkProjectionFold`/`PartyProcessingActivityFold` are logged under `PartyDetailSdkProjectionHandler`'s or `PartyIndexSdkProjectionHandler`'s log category depending purely on which handler called in. An operator filtering by the actual source class gets nothing, and the same drop reason can appear under two different categories. Fixing this cleanly needs a design decision (e.g., a dedicated logger category or `ILoggerFactory` seam), not a quick patch.
status: open
decision: 2026-09-06 Dedicated fold categories — Give each fold a dedicated typed or named logger category, inject it through handlers, and lock category stability with tests.

### DW-48: Bound dropped-event diagnostic volume during full rebuilds

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.AccumulateAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — `PartyIndexSdkProjectionHandler.AccumulateAsync` (the full-rebuild path) now re-emits a log line for every historically-known-bad event on every rebuild run, with no batching, sampling, or dedup — a real log-flooding risk on a large event store. Needs a product/ops decision on acceptable rebuild-time log volume, not a same-pass patch.
status: open
decision: 2026-09-06 Summarize diagnostics — Accumulate bounded counts by failure reason and emit one no-PII summary at rebuild completion.

### DW-49: Handle converter format and overflow failures during event deserialization

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold.DeserializeNew
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding, pre-existing (not caused by this session's patch): the catch filter only covers `JsonException`/`ArgumentNullException`/`NotSupportedException`/`InvalidOperationException`; a `FormatException` or `OverflowException` thrown by a custom converter propagates unhandled and crashes the whole dispatch instead of being skip-logged.
status: open

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

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: _bmad-output/implementation-artifacts/sprint-status.yaml
reason: Cosmetic: the Epic 7 rollback-retention action item is closed `done` citing an authorization SCP "approved 2026-08-02" for an action the same annotation dates to 2026-08-01 (approval postdating the act it authorizes by a day); resolves naturally when `sprint-status.yaml` is next synced.
status: done 2026-09-06
resolution: already resolved: sprint-status.yaml:321-329 records that closure occurred under the 2026-08-01 user selection and formal authorization followed in the 2026-08-02 SCP; clarified by commit 03ab938c.

### DW-54: Refresh etags before retrying incomplete erasure batches

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartySdkReadModelEraser.ExecuteWithResumeAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: PartySdkReadModelEraser.ExecuteWithResumeAsync re-executes the original batch on Incomplete without refreshing etags; a partial apply can loop into sdk-read-model-cleanup-conflict.
status: open

### DW-55: Test Memories erasure cleanup with disabled indexing and durable mappings

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: ProjectionPlatformAdapterTests and Memories cleanup composition
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: ProjectionPlatformAdapterTests invoke memories-search cleanup with Enabled=false and no seeded mappings, so Cleaned can pass without exercising DELETE/clearance.
status: done 2026-09-06
resolution: already resolved: PartyMemoryIndexEntrySearchIndexerTests.cs:214-239 disables indexing, seeds a durable case-at-ingestion mapping, removes it, and verifies the persisted case is used in the DELETE URI; commit d4e1d83d.

### DW-56: Replace actor-era query failure vocabulary on the SDK path

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartySdkQueryService failure vocabulary
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: PartySdkQueryService still returns actor-era failure reasons on the SDK path, which misleads operators after AC8 actor deletion.
status: open

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
status: open

### DW-60: Restore bounded allowlisted party IDs on SDK detail queries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Party SDK query detail-envelope validation
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: TryValidateDetailEnvelope only rejects reserved chars after TenantSafeProjectionReadGuardrailsTests were deleted; oversized/malformed party ids are weakly gated.
status: open

### DW-61: Update the Story 7.4 projection compatibility E2E spec

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts
reason: This came from the DI and query-host sub-chunk comparing PartiesServiceCollectionExtensions.cs with 2c4a7af. `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts` still expects deleted projection-adapter registrations and old `ProjectionPlatformAdapterTests` method names — deferred, pre-existing e2e drift outside this DI chunk.
status: open

### DW-62: Use TimeProvider for erasure cleanup timestamps

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: erasure cleanup TimeProvider usage
reason: Erasure cleanup timestamps still use `DateTimeOffset.UtcNow` instead of the newly registered `TimeProvider` — deferred, pre-existing certificate timestamp pattern across erasure store results.
status: open

### DW-63: Protect Memories mapping replacement and clearing with concurrency-safe retries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryUnitMappingStore.ClearMappingsAsync and ReplaceMappingsAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryUnitMappingStore.ReplaceMappingsAsync` and the empty-list delete can overwrite a concurrent indexing write after cleanup reads the prior mapping set. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `ClearMappingsAsync` and `ReplaceMappingsAsync` use unconditional delete/save operations, so concurrent indexing can lose a newly committed mapping and leave an undiscoverable Memories unit.
status: open

### DW-64: Persist partial Memories cleanup progress after caller cancellation

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryCleanupService.DeleteByPartyAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryCleanupService.DeleteByPartyAsync` uses the already-cancelled caller token in its `finally` mapping update, so cancellation can prevent the promised resumable audit state from being saved.
status: open

### DW-65: Compensate after cancellation between Memories ingestion and mapping persistence

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryIndexingService
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryIndexingService` rethrows caller cancellation from `RecordMappingAsync` without deleting the already-created Memories unit, leaving an untracked unit outside erasure discovery. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `PartyMemoryIndexingService` propagates caller cancellation from the mapping write without deleting the already-created unit, leaving it outside later erasure discovery.
status: open

### DW-66: Use ingestion-time identity for Memories compensating deletion

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryIndexingService.TryCompensatingDeleteAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `TryCompensatingDeleteAsync` gates cleanup on the current options snapshot even though configuration can change after ingestion and the unit retains its authoritative CaseId.
status: open

### DW-67: Resolve consumer validation artifacts from central package versions

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-consumer-package-references.py` hard-codes obsolete FrontComposer and Tenants versions instead of the currently evaluated dependency set.
status: open

### DW-68: Contain consumer-validation package caches in the disposable workspace

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-consumer-package-references.py` places `NUGET_PACKAGES` under the work directory's parent, so cleanup leaves packages that can mask missing-feed failures in later runs.
status: open

### DW-69: Restrict consumer validation to configured NuGet sources

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The generated NuGet configuration lacks `<clear/>`, and the CLI always retains nuget.org, allowing undeclared user or machine feeds to hide incomplete local package output.
status: open

### DW-70: Compare forbidden NuGet package IDs case-insensitively

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-nuget-packages.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-nuget-packages.py` performs case-sensitive package-ID checks even though NuGet identifiers are case-insensitive.
status: open

### DW-71: Pin reusable CI and CodeQL workflows to immutable revisions

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows/ci.yml and .github/workflows/codeql.yml
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `.github/workflows/ci.yml` and `.github/workflows/codeql.yml` invoke reusable workflows through mutable `@main` references.
status: open

### DW-72: Publish Aspire-hosted services with Production defaults

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: src/Hexalith.Parties.AppHost/Program.cs
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `src/Hexalith.Parties.AppHost/Program.cs` emits `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` as `Development` for publish output.
status: open

### DW-73: Fail publish preflight when the OIDC client secret is missing

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: src/Hexalith.Parties.AppHost/Program.cs
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `src/Hexalith.Parties.AppHost/Program.cs` substitutes an empty client secret and continues producing deployment artifacts that cannot authenticate.
status: open

### DW-74: Reject duplicate keys in merged BMAD configuration arrays

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: _bmad/scripts/config_utils.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `_bmad/scripts/config_utils.py` can retain repeated base codes or ids, leaving ambiguous effective configuration after overrides.
status: open

### DW-75: Tolerate deleted historical tags during release verification

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows/release.yml
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `.github/workflows/release.yml` can report a successful current publication as failed when an older release references a tag that no longer exists.
status: open

### DW-76: Restore the Playwright accessibility lane as a required CI gate

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows, scripts/test.ps1, and tests/e2e/package.json
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The replacement CI workflow no longer runs `npm run test:a11y`, leaving axe, keyboard-focus, forced-colors, computed-style, and visual checks unexecuted. Also recorded in "Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)": The Playwright accessibility lane is wired into no workflow. `.github/workflows/` contains no Playwright or `test:a11y` step, `scripts/test.ps1` invokes no npm lane, and `tests/e2e/package.json`'s `test:a11y` script is called by nothing. Spine §7 I12 already records the always-on CI a11y lane as a separate open ledger item.
status: open

### DW-77: Exercise mTLS across every configured Dapr sidecar

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: configured Dapr sidecar topology tests
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Current tests inspect generated YAML and one synthetic sidecar but never start the mTLS topology or prove a cross-service invocation with all sidecars credentialed.
status: open
decision: 2026-09-06 Add test-only topology support — Introduce test-scoped readiness and tenant bootstrap, run all sidecars, and prove a credentialed cross-service invocation.

### DW-78: Align PartyErased timestamp resolution across projection folds

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
location: PartyProcessingActivityFold.Fold and PartyDetailProjectionHandler.ApplyErasure
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyDetailProjectionHandler.ApplyErasure` assigns `ErasedAt = erased.ErasedAt` while `PartyProcessingActivityFold.Fold` assigns `@event.Timestamp.ToUniversalTime()`.
status: open
decision: 2026-09-06 Payload erasure instant — Use PartyErased.ErasedAt consistently across detail and processing folds and document the semantic choice.

### DW-79: Parallelize state-store reads in PartyDetailSdkProjectionHandler.PrepareRebuildAsync

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
location: PartyDetailSdkProjectionHandler.PrepareRebuildAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PrepareRebuildAsync` awaits `readModelStore.GetAsync<PartyDetailSdkReadModel>` and `GetAsync<PartyProcessingSdkReadModel>` sequentially rather than concurrently with `Task.WhenAll`.
status: open

### DW-80: Make Memories rebuild reconciliation atomic with concurrent erase and re-add

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.CompleteRebuildAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyIndexSdkProjectionHandler.CompleteRebuildAsync` reads canonical index state once before external notifications, so a later live erase or re-add can race a stale `NotifyIndexedAsync` or `NotifyRemovedAsync` call.
status: done 2026-09-06
resolution: already resolved: Commit 8e3953e0; src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs:278-325 re-reads canonical state before each rebuild-completion notification, and tests/Hexalith.Parties.Projections.Tests/PartySdkProjectionHandlerTests.cs:1371-1480 covers concurrent erase and re-add.

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

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: DocumentationFitnessTests and accesscontrol.*.yaml
reason: This note was deliberately placed before the closure-deferral section because EpicEightClosureFitnessTests.ParseDeferrals slices from that heading through end of file. Only one of six `accesscontrol.*.yaml` components is verified. `DocumentationFitnessTests.MaintainedDocumentationDescribesSdkRoutesUnderEventStoreOnlyDenyAcl` parses `accesscontrol.parties.yaml` alone, while the documentation it pins generalizes over all sidecar policies. Broadening the assertion is outside the Story 8.10 Code Map.
status: done 2026-09-06
resolution: already resolved: Commit f8fd7404; tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:172-189 enumerates every accesscontrol*.yaml file and verifies and documents its default-action posture.

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

### DW-93: Define authored_by_spec as non-activating deferral metadata

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Story 8.10 closure-deferral field vocabulary
reason: Story 8.10 accepted these deferrals as waiting work while preserving the current implementation as rollback; its clarified vocabulary separates authoring a wait from activating work. `authored_by_spec` — the spec that wrote this entry down and accepted the wait. Carries no §4 obligation.
status: done 2026-09-06
resolution: already resolved: Commit f8fd7404; spec-8-10-final-readiness-documentation-and-retirement-gate.md:48-54 defines authored_by_spec as authoring without activation or section 4 obligation.

### DW-94: Define activated_by_spec as deferral-activation metadata

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Story 8.10 closure-deferral field vocabulary
reason: `activated_by_spec` — the spec that started working the deferral. Triggers the full six-clause §4 gate in that spec, per I17.
status: done 2026-09-06
resolution: already resolved: Commit f8fd7404; spec-8-10-final-readiness-documentation-and-retirement-gate.md:46-54 distinguishes activation from authoring, and ARCHITECTURE-SPINE.md:279-283 records the enforced vocabulary.

### DW-95: Define delivered_slices as partial-delivery metadata

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Story 8.10 closure-deferral field vocabulary
reason: `delivered_slices` — present only when part of a deferral has shipped. A delivered slice never advances the owning story's status.
status: done 2026-09-06
resolution: already resolved: Commit f8fd7404; sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md:131-134 defines delivered_slices and states that a delivered slice does not advance the story.

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
reason: deferral_id: `8.9-frontcomposer-ui-consolidation` activated_by_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md` activation_authority: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md` delivered_slices: `G4 work package F only (shell skip links and role="main"/role="navigation" landmarks), adopted 2026-08-18 at FrontComposer root gitlink 5cbc5583142a6774ff7813698ad98ec267b336f0. Work packages A-E remain undelivered and Story 8.9 stays blocked. App-owned focus-visible CSS isolation was repaired; I13 remains undischarged until Playwright focuses a content control (DW-111).` owner: `Hexalith.FrontComposer Contracts.UI/Shell owners + Sally (UX Designer) + Amelia (Parties Developer) + Murat (Test Architect)` exit_proof: `Deliver the complete G4 primitive set at an exact approved FrontComposer identity and pass producer bUnit plus Parties bUnit/Playwright parity for picker semantics, freshness/live regions, safe downloads, typed-name confirmation, skip links, forced colors, reduced motion, focus, and GDPR copy before changing Story 8.9 from blocked.` rollback: `Keep the Parties picker, freshness/status regions, download helpers, typed erasure confirmation, optimistic reconciliation, portal components, and current Fluent 2 styling until each replacement slice proves parity; revert a failed slice independently. The delivered shell slice rolls back by restoring the Parties-owned skip links, #parties-main-content, and #parties-app-navigation from the parent of superproject commit 2b63ab9 and pinning FrontComposer back to 97f44c499e83a0ffbf054febd0aab384054ea39e; that revert reinstates the duplicate skip-link strict-locator ambiguity the slice resolved, so it must be paired with a Playwright rerun.` evidence: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md — FrontComposer UI primitives row; sprint-status.yaml keeps 8.9 blocked; tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs and _bmad-output/implementation-artifacts/tests/test-summary.md record the 2026-08-18 shell-slice adoption.`
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

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: root diagnostic gitlinks and the Builds catalog
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: Package mode restores EventStore 3.102.0, Tenants 5.6.0, and Memories 2.25.0, but the recorded gitlinks sit at v3.102.0-27, v5.7.0-5, and v2.25.2; frozen intent required asking before advancing source past those tags.
status: done 2026-09-07
decision: 2026-09-06 Align exact catalog tags — Reset the three diagnostic gitlinks to exact catalog-selected release tags and reconcile live pins and signoff evidence while preserving package mode as authoritative.
resolution: Superseded by the user-approved default-branch-head policy in `spec-update-all-packages-and-submodules.md`. EventStore, Tenants, and Memories now match their verified default-branch heads; Builds remains the sole package-version authority and the live pins, signoff ledger, build, and test receipts were reconciled.

### DW-109: Cover ordinary main pushes with the root-gitlink RC sign-off gate, and pin Tenants/Memories identities.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
location: .github/workflows/rc-gate.yml; tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: rc-gate.yml enforces `.gitlink-signoff.tsv` only on a release-candidate-labeled PR or a push to `rc/**`/`release/**`/a `v*` tag — never an ordinary push to `main`, which is this repo's actual workflow. PlatformApiPrerequisitesTests hardcodes a SHA constant only for EventStore/Commons/Builds/FrontComposer; Tenants and Memories have no such pin. Together, an unauthorized or unreviewed Tenants/Memories gitlink bump on main is caught by nothing until someone reads the diff by hand — exactly the failure mode this review round found live at HEAD.
status: open

### DW-110: Verify validate-publication-preflight.sh's commitlint-proof path is only ever reachable through an authorized bypass.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
location: scripts/validate-publication-preflight.sh
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: The script accepts `HEXALITH_RELEASE_SOURCE_CI_WORKFLOW=commitlint.yml` (the weaker proof path) with no independent check that an operator authorized the bypass; today it is reachable only through release.yml's gated `bypass-validation` input and the script is not wired into any workflow file yet, so it is not currently exploitable. Related to the already-open DW-106 (bypass-validation to proof-source mapping has no executed-bash test, only YAML substring-ordering). Settle by re-checking every caller of this script once it is wired into CI; if a future caller can set the env var independently of the gated input, this becomes a real authorization bypass.
status: done 2026-09-06
resolution: already resolved: .github/workflows/release.yml:42-56 and :307 confine commitlint.yml to the typed bypass input; references/Hexalith.Builds/.github/workflows/domain-release.yml:472,1014 forwards only that selected input, release.config.cjs:12,22 merely invokes preflight, and no alternate caller exists.

## Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)

### DW-111: Observe app-owned content focus and reduced-motion CSS at runtime, not as source greps.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css; tests/e2e/specs/parties-accessibility.spec.ts
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Frozen §4.6 already records I13 as not discharged because Playwright focuses `.fc-skip-link` rather than a content control. `AccessibilityStyleGuardTests` only `ShouldContain`s CSS tokens. Moving `class="parties-main-content"` onto `<FrontComposerShell>` would keep those greps green while isolation stamps nothing. A computed-style assertion on `parties-specimen-primary-action` belongs in `npm --prefix tests/e2e run test:a11y`, which CI does not run (DW-76).
status: open

### DW-112: Hide or retarget Skip to navigation when FrontComposer unmounts `#fc-nav` on compact viewports.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: The skip-to-navigation link renders whenever `HasNavigation` is true, but `#fc-nav` mounts only when `HasNavigation && !IsSubCompactDesktopViewport`. On Tablet/Phone the href has no focus target. Parties consumes the shell and does not edit this submodule; route to FrontComposer shell owners.
status: open

### DW-113: Parse only the first complete JSON object from MSBuild `-getProperty/-getItem` stdout.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: `EvaluateProjectGraph` slices from the first `{` to end of string. Trailing restore/log text after the JSON object would throw `JsonException` instead of a graph assertion. Unverified whether current MSBuild emits trailing content on this invocation; settle by capturing a live `-getItem` payload. If true, severity is medium.
status: open

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
  summary: Retire resolved Epic 8 closure deferrals from the accepted-wait set.
  evidence: `ParseDeferrals` reads packed fields from each reason but not the ledger entry's first-class status, so a resolved entry whose reason remains can still satisfy `ExpectedDeferrals`.
- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
  summary: Reconcile the README SDK prerequisite with global.json.
  evidence: README still tells contributors to install .NET SDK 10.0.302 while global.json selects 10.0.400, and no maintained assertion prevents those values from drifting.
- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
  summary: Prove a FrontComposer RCL static asset is served in source mode.
  evidence: The source-mode static-asset test requests only `_framework/blazor.web.js`, so it does not prove that a selected FrontComposer source asset is available through the UI host.
- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
  summary: Resolve the Fluent UI package root through NuGet and MSBuild configuration.
  evidence: The style guard accepts an empty `NUGET_PACKAGES` value and otherwise assumes the default user cache, ignoring `RestorePackagesPath` and NuGet `globalPackagesFolder`.
- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
  summary: Make Playwright web-server ownership deterministic and test its environment matrix.
  evidence: Local `ASPNETCORE_ENVIRONMENT=Test` runs may reuse an unrelated process at the expected URL, while current checks only inspect configuration text and do not execute the CI/local and Test/Development matrix.
