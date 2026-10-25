### DW-3: Add a fail-continuing lane runner

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md"), 2026-09-06
location: scripts/test.ps1 and CI test-shard loops
source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
reason: `scripts/test.ps1 -Lane all` and each CI shard currently stop at the first failing project, so a package-mode restore blocker can hide later project-specific failures until the first blocker is resolved. The legacy ledger recorded status resolved and resolution Story 8-11 (sprint-change-proposal-2026-07-07-validation-ladder-runner.md). `scripts/test.ps1 -ContinueOnFailure` runs every project and prints a PASS/FAIL summary (exit 1 if any failed); the CI `Run test shard` loop continues after a failing project and summarizes all failures. Default fail-fast behavior preserved, but the authoritative migration manifest requires this entry to remain open.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: scripts/test.ps1:152 and references/Hexalith.Builds/.github/workflows/domain-ci.yml:378 prove fail-continuing local and CI loops with aggregate failure reporting.

### DW-4: Add inspectable local test output and property forwarding

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md"), 2026-09-06
location: scripts/test.ps1
source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
reason: CI writes TRX/coverage artifacts and some local blockers require properties such as `UseHexalithProjectReferences=true`, but the local lane runner currently exposes neither a results-directory/logger option nor a safe property-forwarding interface. The legacy ledger recorded status resolved and resolution Story 8-11 (sprint-change-proposal-2026-07-07-validation-ladder-runner.md). `scripts/test.ps1 -ResultsDirectory <path>` emits a per-project TRX (local CI parity) and `-Properties <k=v>,<k=v>` forwards each value as `-p:<value>` to `dotnet test`, but the authoritative migration manifest requires this entry to remain open.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: scripts/test.ps1:12-18 exposes ResultsDirectory and Properties; scripts/test.ps1:25-30 creates the requested result root; scripts/test.ps1:51-64 forwards per-project TRX/results and MSBuild properties.

### DW-5: Define a safe consent and channel identifier contract

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md"), 2026-09-06
location: RecordConsent and RevokeConsent command contracts
source_spec: `_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`
reason: `RecordConsent` and `RevokeConsent` currently accept `ChannelId`/`ConsentId` values that can contain legacy `channel:purpose` separators, so applying the new `PartyIdentifier` semantic-ID helper would break existing consent IDs while leaving aggregate not-found messages able to echo raw consent/channel identifiers.
status: done 2026-09-07
archived: 2026-10-13
resolution: resolved by sweep bundle dw-safe-consent-identifiers
resolution-undo: 7adbf51424c125b77bca8b3b97165cbe979cd88a46e456ab347a6b829727a629 2026-09-07 7374617475733a206f70656e
decision: 2026-09-06 Separate compatible validators — Add separate channel-segment and legacy-composite consent validators, preserve existing stored IDs, and replace unsafe error detail.

### DW-6: Correct and validate the advanced Hexalith.Builds checkout

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
location: references/Hexalith.Builds
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
reason: Checkout `63d3221` supplied `v1.16.3` as a NuGet version and caused Actions runs `29467970597` and `29468665570` to fail during restore. Builds `v4.18.11` corrected the value to `1.16.3`; commit `6516faf` adds the evaluated central-version release guard and fixtures. Builds `v4.19.0` retains both changes and adds the MTP-compatible shared test contract exposed by follow-up run `29482004796`; the Parties gitlink/signoff adopt that release. The legacy ledger recorded status resolved and resolution `_bmad-output/implementation-artifacts/spec-gh-29467970597-fix-invalid-builds-package-version.md`; Hexalith.Builds `640b59c1434e4e1e079771c401e11048772c7a27` (`v4.19.0`), but the authoritative migration manifest requires this entry to remain open.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: The root and checkout both select references/Hexalith.Builds commit 8db7459d065926501ee045b3aaf7b816780905e5, exact tag v4.27.1, superseding the verified v4.19.0 fix; root history also contains a97530d2 and 9377c572 for catalog/test-evidence corrections.

### DW-7: Test persisted LRU eviction after restore

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
location: references/Hexalith.Memories persisted-LRU tests
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
reason: Incidental review found the new workflow recency field is tested across serialization and eviction separately, but not after serialize/restore at the 256-entry limit; a restored actor could evict a recently refreshed workflow and reapply a delayed transition.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: Memories commit 7331cd4ab82b71eaa7486f642ab482ccd045ca26, selected by the root gitlink, added Transition_RestoredRefreshedWorkflowAtLedgerLimitSurvivesEviction at CaseIngestionCounterLogicTests.cs:147.

### DW-8: Test intermediate Memories state migration

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md"), 2026-09-06
location: references/Hexalith.Memories migration tests
source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
reason: Incidental review found no test for persisted state containing `AppliedTransitionSequences` while lacking the newer `AppliedTransitionWorkflowOrder`, leaving the immediate predecessor format's eviction queue reconstruction unverified.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: Memories commit 7331cd4ab82b71eaa7486f642ab482ccd045ca26 added Transition_PredecessorStateAtLedgerLimitRestoresRecencyBeforeEviction at CaseIngestionCounterLogicTests.cs:188.

### DW-9: Deliver granular EventStore.Client registration and coexistence proof

origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
location: EventStore.Client registration across Parties and FrontComposer
source_spec: none
reason: This independently shippable EventStore.Client package change was split from the G8 owner-proof action so the EventStore.Aspire JWT prerequisite can be completed first.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: references/Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/Registration/AddEventStoreTests.cs:155 and references/Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/Registration/ReadModelAndCursorRegistrationTests.cs:75, plus src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:158, prove coexistence, idempotency, and consumer adoption.
decision: 2026-09-06 Add EventStore extensions — Add independently selectable EventStore.Client extensions with idempotency, order, and coexistence tests, then adapt consumers while retaining generic registration.

### DW-10: Deliver integrated AppHost topology parity proof

origin: migrated from legacy ledger (flat source_spec "none"), 2026-09-06
location: FrontComposer.AppHost or approved platform AppHost
source_spec: none
reason: This independently shippable platform-host change was split from the G8 owner-proof action because it depends on the EventStore Aspire and client-registration surfaces being proven first.
status: done 2026-09-08
archived: 2026-10-13
resolution: already resolved: references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.AppHost/Program.cs:54-81 composes Tenants and Parties through AddEventStoreDomainModule; references/Hexalith.FrontComposer/_bmad-output/implementation-artifacts/evidence/pact-provider-reconciliation/apphost-smoke.json:4,17-82 matches the current AppHost source hashes and records passed authenticated startup, command, query, projection, and clean shutdown proof.

### DW-12: Enforce multi-audience JWT validation at runtime

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
location: EventStore.Aspire and consuming host authentication
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
reason: This was split because the reusable EventStore.Aspire composition surface can ship independently before each host authentication configurator adopts ordered valid audiences.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: EventStore commit dfbbde37782fcb59b9fc4e7514107b20d263d9ba added ValidAudiences runtime configuration and ProductionAuthorityAuthenticationTests.cs:63-92 proves additional-audience acceptance and wrong-audience rejection.
decision: 2026-09-06 Add owner-level audiences — Add ordered ValidAudiences to owner options and configurators, retain Audience as the primary backward-compatible value, and test the acceptance and rejection matrix.

### DW-15: Bind the operational-index ACL route, verb, policy, and action

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md"), 2026-09-06
location: /admin/operational-index-metadata DAPR ACL
source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
reason: Incidental review of concurrent ACL edits found that independent string assertions can pass when `/admin/operational-index-metadata` is placed under the wrong app policy, verb, or action.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit 02ccd3176 added structural ACL parsing; tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:317-377 asserts the single EventStore policy, exact route inventory, POST verb, and allow action.

### DW-18: Fail semantic-release closed to the local binary

origin: migrated from legacy ledger (flat source_spec "/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md"), 2026-09-06
location: semantic-release workflow
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
reason: `npx semantic-release` predates this change and may fetch if local tooling is absent; resolving it requires a separate release-workflow policy decision.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: references/Hexalith.Builds/.github/workflows/domain-release.yml:484 invokes the installed semantic-release binary through npm exec --no.
decision: 2026-09-06 Use npm exec no — Invoke semantic-release through npm exec --no so only the installed dependency can run.

### DW-21: Make Party SDK erasure writes atomic

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
location: PartySdkReadModelEraser.EraseAsync
reason: Three independent `UpdateAsync` calls in `PartySdkReadModelEraser.EraseAsync` can leave detail/processing/index mutually inconsistent on mid-flight failure — no multi-key transactional write seam in the approved `ReadModelWritePolicy` API.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit 02ccd3176; src/Hexalith.Parties.Projections/Services/PartySdkReadModelEraser.cs:13-20,56-95 writes detail, processing, and index through one ReadModelBatch and retries optimistic conflicts.

### DW-22: Define idempotent ErasedAt semantics for retries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
location: PartySdkReadModelEraser.ApplyErasure
reason: Optimistic concurrency retries re-run `ApplyErasure` and refresh `ErasedAt` — `ApplyErasure` always stamps `UtcNow`; short-circuiting on `IsErased` needs a deliberate idempotency contract change.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit 02ccd3176; PartySdkReadModelEraser.cs:40,103-119,127-140,149-171 freezes cleanup time and preserves first-erasure metadata, with retry stability proven in PartySdkProjectionHandlerTests.cs:1788-1860.

### DW-23: Define erasure freshness metadata semantics

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)"), 2026-09-06
location: PartySdkReadModelEraser detail and index freshness metadata
reason: Erasure copies through pre-erasure `ProjectedAt`/`ProjectionVersion` on detail/index — stamping erasure-time freshness is entangled with the open AC7 freshness-mapping gap; index timestamps also cover unrelated remaining parties.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit 02ccd3176; PartySdkReadModelEraser.cs:116-119,137-140,160-171 stamps ProjectedAt on first cleanup, preserves it on retries, and retains ProjectionVersion; PartySdkProjectionHandlerTests.cs:1741-1762 and 1835-1860 pin the semantics.

### DW-28: Harden query compatibility shim lifecycle and state handling

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
location: Party SDK query compatibility shims
reason: Non-durable unbounded in-process last-known cache; no `ApplicationStopping` link; Actor-named constant bags; missing-detail vs empty-processing asymmetry — intentional shim/architecture trade-offs from the first Group 3 pass.
status: done 2026-09-06
archived: 2026-10-13
decision: 2026-09-06 Document bounded memory — Retain the bounded in-memory cache and document shutdown and restart loss as intentional until a durable successor exists.
resolution: closed by human decision: Retain the bounded in-memory cache and document shutdown and restart loss as intentional until a durable successor exists.
decision: 2026-09-06 Document bounded memory — Retain the bounded in-memory cache and document shutdown and restart loss as intentional until a durable successor exists.

### DW-29: Preserve the cursor codec failure reason

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
location: PartySdkQueryService.LogCursorRejected
reason: The cursor codec `failureReason` was discarded before rejection logging.
status: done 2026-08-03
archived: 2026-10-13
resolution: Resolved by `LogCursorRejected` in `PartySdkQueryService`.

### DW-38: Harden rebuild completion against null manifest collections

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.CompleteRebuildAsync
reason: `PartyIndexSdkProjectionHandler.CompleteRebuildAsync` would throw `NullReferenceException` (not a controlled result) if a persisted rebuild-completion manifest ever deserializes with null `Entries`/`RemovedPartyIds`. Not reachable under the current producer (`FinalizeAsync` always serializes non-null arrays); hardening-only.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: PartyIndexSdkProjectionHandler.cs:291 and :304 normalize null RemovedPartyIds and Entries to Array.Empty; introduced by commit 6fac8309.

### DW-39: Refresh the G5 matrix validation-evidence identity

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible and Story 8.3 G5 matrix row
reason: `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible` is RED: it hard-pins the Story 8.3 matrix's "Payload protection engine package" (G5) row to EventStore `v3.89.0`/`7854f8e5`, but the working tree is now at `v3.91.0`/`1d6e9321` (this story's resolved EventStore identity). Pre-existing to this review session, not caused by its patches. Out of Story 8.6 scope — G5 payload-protection is Story 8.7's territory and needs its own owner-reviewed identity-authorization update, not a Story 8.6 patch.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: PlatformApiPrerequisitesTests.cs:29-30 pins the current EventStore identity, while :1333-1349 validates the current gitlink, checkout, matrix ledger, and package identity; refreshed by commit e95d2f82.

### DW-44: Prevent rebuild finalization from overwriting concurrent live projection writes

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.FinalizeAsync
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review found the defect (blind `ReadModelBatchConcurrency.LastWrite` can drop a canonical entry added mid-rebuild) exists both as a still-unchecked `[ ]` Group 2 task and, separately, inside a `[x]`-checked "Fixed 2026-08-04 (partial)" bullet in `8-6-projection-and-query-sdk-migration.md` that itself states the underlying issue is "left open — not addressed by this patch." The two are never cross-referenced, and the defect was never logged here, so it is invisible to anyone scanning only this ledger. Also recorded in "Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)": The 2026-08-05 human-directed session restored operator diagnostic logging and fold-level tests, deferred the remaining findings, and recorded that this item fulfills the earlier tracker-reconciliation action. `PrepareRebuildAsync`/`FinalizeAsync` write with `ReadModelBatchConcurrency.LastWrite` (no ETag check) [`PartyDetailSdkProjectionHandler.cs:99,103`, `PartyIndexSdkProjectionHandler.cs:102`] — a rebuild finalize can silently overwrite a newer concurrent live `ProjectAsync` write with no conflict detection. Investigated 2026-08-05: switching to `Match(etag)` unilaterally is unsafe without knowing the EventStore SDK rebuild-plan executor's retry/abort contract on a write conflict — that contract lives in `Hexalith.EventStore.DomainService`'s rebuild orchestration, outside this repo's `IAsyncDomainProjectionRebuildHandler` / `IAsyncDomainSharedProjectionRebuildCompletionHandler` surface. Needs SDK-owner input, not a unilateral Parties-side change. Resolved 2026-08-16: EventStore v3.95 now provides the required bounded conflict contract. Parties rebuild plans use `Match(etag)` for existing rows and `CreateOnly` for absent rows; focused plan-policy tests pass for detail, processing, and index.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: PartyIndexSdkProjectionHandler.cs:248-250 uses ETag Match/CreateOnly finalization, and :282-309 rereads canonical state before notifications; implemented by commit 8e3953e0.

### DW-53: Correct the Epic 7 rollback-retention approval chronology

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: _bmad-output/implementation-artifacts/sprint-status.yaml
reason: Cosmetic: the Epic 7 rollback-retention action item is closed `done` citing an authorization SCP "approved 2026-08-02" for an action the same annotation dates to 2026-08-01 (approval postdating the act it authorizes by a day); resolves naturally when `sprint-status.yaml` is next synced.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: sprint-status.yaml:321-329 records that closure occurred under the 2026-08-01 user selection and formal authorization followed in the 2026-08-02 SCP; clarified by commit 03ab938c.

### DW-54: Refresh etags before retrying incomplete erasure batches

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartySdkReadModelEraser.ExecuteWithResumeAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: PartySdkReadModelEraser.ExecuteWithResumeAsync re-executes the original batch on Incomplete without refreshing etags; a partial apply can loop into sdk-read-model-cleanup-conflict.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs:1816 proves the current resumable batch contract retries the exact same attempt identity.

### DW-55: Test Memories erasure cleanup with disabled indexing and durable mappings

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: ProjectionPlatformAdapterTests and Memories cleanup composition
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: ProjectionPlatformAdapterTests invoke memories-search cleanup with Enabled=false and no seeded mappings, so Cleaned can pass without exercising DELETE/clearance.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: PartyMemoryIndexEntrySearchIndexerTests.cs:214-239 disables indexing, seeds a durable case-at-ingestion mapping, removes it, and verifies the persisted case is used in the DELETE URI; commit d4e1d83d.

### DW-66: Use ingestion-time identity for Memories compensating deletion

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryIndexingService.TryCompensatingDeleteAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `TryCompensatingDeleteAsync` gates cleanup on the current options snapshot even though configuration can change after ingestion and the unit retains its authoritative CaseId.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: Commit 90a7d090594745321aa8c851452f05ce809aa201 changed compensation to unit.CaseId; PartyMemoryIndexingService.cs:163-178 now validates and addresses deletion with the ingestion-time tenant and CaseId.

### DW-68: Contain consumer-validation package caches in the disposable workspace

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-consumer-package-references.py` places `NUGET_PACKAGES` under the work directory's parent, so cleanup leaves packages that can mask missing-feed failures in later runs.
status: done 2026-09-07
archived: 2026-10-13
resolution: already resolved: validate-consumer-package-references.py creates projects below work_directory, locates NUGET_PACKAGES under that workspace, and removes the whole workspace before every run, a safe layout present since commit f22ebbb95af6b9ef9c80a10778290cb0d0b90cad.

### DW-80: Make Memories rebuild reconciliation atomic with concurrent erase and re-add

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.CompleteRebuildAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyIndexSdkProjectionHandler.CompleteRebuildAsync` reads canonical index state once before external notifications, so a later live erase or re-add can race a stale `NotifyIndexedAsync` or `NotifyRemovedAsync` call.
status: done 2026-09-06
archived: 2026-10-13
resolution: 2026-10-19 Superseded by DW-173/DW-193 and spec-dw-173-erasure-search-notification-fence.md. Independent-instance regressions reproduce the residual stale-snapshot and in-flight-indexing races; the new bounded canonical checks and cleanup retries close them at the awaited operation boundary. The historical atomic claim does not establish terminal Memories workflow erasure, which remains separate in DW-168/DW-170.
historical-resolution: already resolved: Commit 8e3953e0; src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs:278-325 re-reads canonical state before each rebuild-completion notification, and tests/Hexalith.Parties.Projections.Tests/PartySdkProjectionHandlerTests.cs:1371-1480 covers concurrent erase and re-add.

### DW-89: Verify EventStore-only deny ACL across all Dapr sidecars

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: DocumentationFitnessTests and accesscontrol.*.yaml
reason: This note was deliberately placed before the closure-deferral section because EpicEightClosureFitnessTests.ParseDeferrals slices from that heading through end of file. Only one of six `accesscontrol.*.yaml` components is verified. `DocumentationFitnessTests.MaintainedDocumentationDescribesSdkRoutesUnderEventStoreOnlyDenyAcl` parses `accesscontrol.parties.yaml` alone, while the documentation it pins generalizes over all sidecar policies. Broadening the assertion is outside the Story 8.10 Code Map.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit f8fd7404; tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:172-189 enumerates every accesscontrol*.yaml file and verifies and documents its default-action posture.

### DW-93: Define authored_by_spec as non-activating deferral metadata

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Story 8.10 closure-deferral field vocabulary
reason: Story 8.10 accepted these deferrals as waiting work while preserving the current implementation as rollback; its clarified vocabulary separates authoring a wait from activating work. `authored_by_spec` — the spec that wrote this entry down and accepted the wait. Carries no §4 obligation.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit f8fd7404; spec-8-10-final-readiness-documentation-and-retirement-gate.md:48-54 defines authored_by_spec as authoring without activation or section 4 obligation.

### DW-94: Define activated_by_spec as deferral-activation metadata

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Story 8.10 closure-deferral field vocabulary
reason: `activated_by_spec` — the spec that started working the deferral. Triggers the full six-clause §4 gate in that spec, per I17.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit f8fd7404; spec-8-10-final-readiness-documentation-and-retirement-gate.md:46-54 distinguishes activation from authoring, and ARCHITECTURE-SPINE.md:279-283 records the enforced vocabulary.

### DW-95: Define delivered_slices as partial-delivery metadata

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: Story 8.10 closure-deferral field vocabulary
reason: `delivered_slices` — present only when part of a deferral has shipped. A delivered slice never advances the owning story's status.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: Commit f8fd7404; sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md:131-134 defines delivered_slices and states that a delivered slice does not advance the story.

### DW-108: Align diagnostic source gitlinks to the published nuget.org tags selected by the Builds catalog.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: root diagnostic gitlinks and the Builds catalog
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: Package mode restores EventStore 3.102.0, Tenants 5.6.0, and Memories 2.25.0, but the recorded gitlinks sit at v3.102.0-27, v5.7.0-5, and v2.25.2; frozen intent required asking before advancing source past those tags.
status: done 2026-09-07
archived: 2026-10-13
decision: 2026-09-06 Align exact catalog tags — Reset the three diagnostic gitlinks to exact catalog-selected release tags and reconcile live pins and signoff evidence while preserving package mode as authoritative.
resolution: Superseded by the user-approved default-branch-head policy in `spec-update-all-packages-and-submodules.md`. EventStore, Tenants, and Memories now match their verified default-branch heads; Builds remains the sole package-version authority and the live pins, signoff ledger, build, and test receipts were reconciled.

### DW-110: Verify validate-publication-preflight.sh's commitlint-proof path is only ever reachable through an authorized bypass.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
location: scripts/validate-publication-preflight.sh
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: The script accepts `HEXALITH_RELEASE_SOURCE_CI_WORKFLOW=commitlint.yml` (the weaker proof path) with no independent check that an operator authorized the bypass; today it is reachable only through release.yml's gated `bypass-validation` input and the script is not wired into any workflow file yet, so it is not currently exploitable. Related to the already-open DW-106 (bypass-validation to proof-source mapping has no executed-bash test, only YAML substring-ordering). Settle by re-checking every caller of this script once it is wired into CI; if a future caller can set the env var independently of the gated input, this becomes a real authorization bypass.
status: done 2026-09-06
archived: 2026-10-13
resolution: already resolved: .github/workflows/release.yml:42-56 and :307 confine commitlint.yml to the typed bypass input; references/Hexalith.Builds/.github/workflows/domain-release.yml:472,1014 forwards only that selected input, release.config.cjs:12,22 merely invokes preflight, and no alternate caller exists.

### DW-115: Reconcile the README SDK prerequisite with global.json.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: README.md and global.json
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: Historical drift: README prescribed SDK 10.0.302 while global.json now selects 10.0.401. Story 8.10 Round 6 updates README and derives the maintained-document fitness expectation directly from global.json.
status: done 2026-10-04
archived: 2026-10-13
resolution: README and DocumentationFitnessTests now derive and guard the selected SDK; focused verification is recorded in tests/test-summary.md.

### DW-124: Publish a FrontComposer Shell release carrying the module-landing route surface consumed by Parties.UI.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-10-04)
location: src/Hexalith.Parties.UI/Program.cs:49,216; src/Hexalith.Parties.UI/Components/Routes.razor:7; references/Hexalith.Builds/Props/Directory.Packages.props
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Commit `37f1d04b` wires `FrontComposerRouteOptions.ReservedSegments` and `FcModuleLandingPage` into the UI host, but both types exist only in FrontComposer source (added 2026-09-27); the packaged `Hexalith.FrontComposer.Shell` `4.4.0` and `4.5.0` DLLs carry neither, so the package-mode UI build (the CI, bUnit, and released-container identity) cannot compile. Exit proof: a published Shell release containing both types, a Builds catalog bump selecting it, a green package-mode `Hexalith.Parties.UI` Release build, and matching 8.3 identity rows. Rollback: revert the `37f1d04b` routing wiring until that release exists.
decision: 2026-10-04 Ship FrontComposer release — owner chose to publish a FrontComposer release carrying the types and bump the catalog rather than gate on source mode or revert; the extra G4 surface is recorded in the 8.3 matrix and the 8.9 deferral meanwhile.
decision: 2026-10-05 Gate behind source mode — through commit `47e2da3244fd7d6d14e39bc30513b23504e9bc27` the Administrator chose Round 6 D4 option (b): the `FrontComposerRouteOptions` wiring compiles only under `HFC_ROUTE_OPTIONS` (defined when `HexalithFrontComposerFromSource=true`), and the Shell assembly is referenced through `FrontComposerShell` instead of `FcModuleLandingPage`.
status: done 2026-10-05
archived: 2026-10-13
resolution: resolved by `47e2da3244fd7d6d14e39bc30513b23504e9bc27`. At the 2026-10-05 final set (Parties `47e2da32` plus the EventStore `v3.113.0` checkout and the `3.113.0` pin), `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` passed with 0 warnings and 0 errors; the three CS0234 errors are gone. Follow-up DW-139 keeps package-mode reserved-segment and module-landing wiring open until a FrontComposer Shell release carries those types.

### DW-140: Classify the identity-binding and agent-provisioning commands in the Art.18 restriction inventory

origin: Story 8.10 full Release test lane at the 2026-10-05 final identity set
location: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateRestrictionTests.cs:229-289; src/Hexalith.Parties/Domain/PartyAggregate.Identity.cs:22,72,86,100,155-158
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: `PartyAggregateRestrictionTests.AllModificationHandleMethods_CallRejectIfRestricted` fails in `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` (Server.Tests 255/256): `ProvisionAgentParty`, `EstablishHumanActorBinding`, `RebindHumanActorBinding`, and `RevokeHumanActorBinding` (identity feature, commit `37d87f5a2869b076c651a58714d60f64647848bc`) are in neither the guarded nor the exempt list. Observed, not decided: the three binding handlers refuse restricted parties through `CanBind` (`IsRestricted: false`) rather than `RejectIfRestricted`, so a restricted party gets their generic rejection instead of the typed `PartyProcessingRestricted` outcome, and `ProvisionAgentParty` only creates a new agent party. Every recorded full Release lane since that commit was blocked (first by the EventStore contract errors, then by DW-124), so this is the first recorded full-lane run that exposes it. Choosing guard versus exempt is an I7 Art.18 policy decision for the Parties domain owners; Story 8.10 changes neither the handlers nor the inventory. Exit proof: each command classified (typed guard added, or exemption justified in the test) and Server.Tests green.
decision: 2026-10-05 Exempt, then prove refusal — Administrator / jpiquot (spec 8.10 "Closure-blocker resolution: DW-140, DW-141, DW-142 — 2026-10-05", decision 1): add `ProvisionAgentParty`, `EstablishHumanActorBinding`, `RebindHumanActorBinding`, and `RevokeHumanActorBinding` to the `exempt` inventory with one-line justifications and leave the production handlers unchanged. Provisioning is creation-only (a restricted party already exists, so it is rejected as an occupied identity, and an exact retry emits no events); the binding handlers refuse a restricted party through `CanBind` (`IsRestricted: false`) with `HumanActorBindingRejected("binding-unavailable")`, and an exact retry of a recorded transition emits no events.
status: done 2026-10-05
archived: 2026-10-13
resolution: `PartyAggregateRestrictionTests.AllModificationHandleMethods_CallRejectIfRestricted` now lists the four commands as exempt, each with its justification. The refusal is proven by `HumanActorBindingTests.RestrictedPersonParty_RefusesEstablishThatSucceedsUnrestricted`, `RestrictedBoundParty_RefusesRebindThatSucceedsUnrestricted`, and `RestrictedBoundParty_RefusesRevokeThatSucceedsUnrestricted`: each command succeeds on the unrestricted person party, then is rejected with `binding-unavailable` and no state-changing events after `ProcessingRestricted` is applied. No production handler changed. The Server.Tests result in the 2026-10-05 closure-blocker lane is recorded in `tests/test-summary.md`.

### DW-141: Stop PartiesOverview from passing the source-only FcPageTabs.ModuleRoute parameter to packaged Shell 4.5.0

origin: Story 8.10 full Release test lane at the 2026-10-05 final identity set
location: src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor:13; tests/Hexalith.Parties.UI.Tests/PartiesOverviewTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: high
reason: In the package-mode graph (CI, bUnit, and the released `parties-ui` container), `PartiesOverview.razor` (commit `c069af07`) sets `FcPageTabs ModuleRoute="/parties"`, but packaged `Hexalith.FrontComposer.Shell` `4.5.0` has no `ModuleRoute` parameter; Razor compiles the attribute, then rendering throws `InvalidOperationException: Object of type 'Hexalith.FrontComposer.Shell.Components.Layout.FcPageTabs' does not have a property matching the name 'ModuleRoute'`. The full Release lane fails 4 of 343 UI tests (`PartiesOverviewTests.ConsumerPolicyShowsOnlyMySpace`, `AdminPolicyShowsOnlyAdministration`, `BothPoliciesShowEachAuthorizedDestinationOnce`, `NeitherPolicyShowsOneSafeNoAccessState`), so `/parties` and `/parties/overview` fail at runtime in the package-mode host. This is the same source-only FrontComposer pattern as DW-124/DW-139, which `47e2da32` fixed only for the route options. Exit proof: package-mode `PartiesOverviewTests` green, either by gating the parameter like `HFC_ROUTE_OPTIONS` or by a FrontComposer Shell release carrying it plus a catalog bump. Rollback: revert the `c069af07` overview page. Bound to the `8.9-frontcomposer-ui-consolidation` slot (G4 surface).
decision: 2026-10-05 Gate behind source mode — Administrator / jpiquot (spec 8.10 closure-blocker resolution, decision 3): packaged Shell `4.5.0` lacks both `FcPageTabs.ModuleRoute` and `FcPageTabs.DefaultTabId`, so pass `ModuleRoute="/parties"` and `DefaultTabId="overview"` only when `HFC_ROUTE_OPTIONS` is defined, through an `@attributes` dictionary in a new `PartiesOverview.razor.cs` partial (empty in package mode), matching `47e2da32`.
status: done 2026-10-05
archived: 2026-10-13
resolution: `PartiesOverview.razor` now passes `@attributes="ModuleRouteTabAttributes"` to `FcPageTabs`; `PartiesOverview.razor.cs` fills that dictionary with `ModuleRoute`/`DefaultTabId` only under `#if HFC_ROUTE_OPTIONS`. In package mode all `PartiesOverviewTests` (`ConsumerPolicyShowsOnlyMySpace`, `AdminPolicyShowsOnlyAdministration`, `BothPoliciesShowEachAuthorizedDestinationOnce`, `NeitherPolicyShowsOneSafeNoAccessState`, `RoutesExposeTheSameOverviewTabAtAliasAndCanonicalUrl`) run in the UI.Tests lane; the gated source branch is compiled by the diagnostic `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:HexalithFrontComposerFromSource=true …`. Results are in `tests/test-summary.md`. Package-mode module-route tab wiring stays open under DW-139.

### DW-142: Classify the identity-feature contract properties in the personal-data inventory

origin: Story 8.10 full Release test lane at the 2026-10-05 final identity set
location: tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs; src/Hexalith.Parties.Contracts/Events/PartyCreated.cs; src/Hexalith.Parties.Contracts/State/PartyState.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: `PersonalDataInventoryTests.InspectedContractProperties_AreClassified` fails in the full Release lane (Contracts.Tests 179/180): `PartyCreated.CreatedAt`, `PartyState.AgentProvisioning`, `PartyState.HasBeenCreated`, `PartyState.HumanActorBindings`, `PartyState.HumanActorTransitions`, and `PartyState.HumanBindingVersion`, all added by commit `37d87f5a2869b076c651a58714d60f64647848bc`, are not classified as personal, non-personal, or deferred. Every recorded full Release lane since that commit was blocked, so this is the first recorded run that exposes it. Classifying actor bindings and transitions is an I8 personal-data decision for the Parties GDPR owners (related to DW-129's protection question); Story 8.10 does not classify them. Exit proof: each property classified with its rationale and Contracts.Tests green.
decision: 2026-10-05 Classify — Administrator / jpiquot (spec 8.10 closure-blocker resolution, decision 2, amended 2026-10-05 after review Edge 17): `PartyCreated.CreatedAt`, `PartyState.HasBeenCreated`, and `PartyState.HumanBindingVersion` are `NonPersonalMetadata`; `PartyState.AgentProvisioning` is `PersonalData`, because it is already marked `[PersonalData]` (`PartyState.cs:19-20`, identity feature `37d87f5a`) — the original decision classified it `NonPersonalMetadata` on the wrong premise that it only describes a non-natural agent organization; `PartyState.HumanActorBindings` and `PartyState.HumanActorTransitions` (they carry `ActorId` / `OperatorActorId`) are `DeferredPrivacyDesign`, bound to open DW-129. Every `NonPersonalMetadata` row must be unmarked. No `[PersonalData]` attribute or contract changes.
status: done 2026-10-05
archived: 2026-10-13
resolution: `PersonalDataInventoryTests` classifies the six properties as the amended decision states (`AgentProvisioning` = `PersonalData`, which `PersonalDataProperties_AreMarked` confirms is marked), so `InspectedContractProperties_AreClassified` covers them. The new guard `NonPersonalMetadataProperties_RemainUnmarked` fails any `NonPersonalMetadata` row whose property carries `[PersonalData]`, mirroring `OrganizationEntityFields_RemainUnmarkedByDefault`; a mutation run that reclassified `AgentProvisioning` as `NonPersonalMetadata` made it fail. No `[PersonalData]` marking or contract type changed. DW-129 stays the open carrier for the `HumanActorBindings` / `HumanActorTransitions` classification and their snapshot-protection evidence. The Contracts.Tests result (review loop 3) is recorded in `tests/test-summary.md`.

### DW-19: Hoist multi-token search candidate normalization

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
location: EvaluateEntry multi-token search hot path
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
reason: `EvaluateEntry` rebuilds the query-only full phrase and candidate collection for every party, creating O(entries) allocations in the 10K hot path despite the current performance gate passing.
status: done 2026-10-17
archived: 2026-10-21
resolution: Both local fuzzy and semantic search prepare the normalized full phrase and candidate array once per query, preserving matching, original token-count scoring, metadata, ordering and paging. Independent regressions detect per-entry phrase and candidate-array copies. Longer-query allocation growth at 10K entries falls from 2,400,728 bytes to 968 bytes in both providers. Final Debug/source build has zero warnings/errors; focused search/performance tests pass 80/80 and full service assembly passes 1098/1098 without failures/errors/skips. Four review verification findings are patched; no new deferred work. DW-20 remains open. See spec-dw-19-hoist-search-candidates.md and tests/dw-19-hoist-search-candidates-2026-10-17/validation.json.

### DW-20: Normalize full-phrase multi-token relevance coverage

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md"), 2026-09-06
location: multi-token relevance scoring
source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
reason: A deterministic full-phrase match is added alongside real query tokens, so coverage can exceed one before the final score is clamped and can inflate ordering relative to token-only matches.
status: done 2026-10-17
archived: 2026-10-21
resolution: Both local search providers exclude the synthetic full phrase from real-token coverage while preserving field matching, weights, metadata, duplicate-token semantics and once-per-query preparation. Corrected relevance and page ordering are covered by 29 direct cases. Final Debug/source build has zero warnings/errors; focused search/performance tests pass 109/109 and full service assembly passes 1127/1127 without failures/errors/skips. All four independent review findings and follow-up are resolved; no new deferred work. See spec-dw-20-normalize-relevance-coverage.md and tests/dw-20-normalize-relevance-coverage-2026-10-17/validation.json. The separate Aspire baseline healthy wait timed out with security Unhealthy; full-topology qualification is not claimed.

### DW-24: Bound missing-CaseId warning deduplication

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: s_caseIdMissingWarned
reason: `s_caseIdMissingWarned` is an unbounded static ConcurrentDictionary (one entry per tenant/party for process lifetime) — mirrors retired orchestrator pattern.
status: done 2026-10-17
archived: 2026-10-21
resolution: Replaced process-lifetime warning identities with singleton-local FIFO tracking capped at 1024 exact tenant/party pairs. Atomic admission bounds both retained identities and eviction bookkeeping; repeated retained pairs suppress warnings, and evicted pairs may warn again. Convergence/no-ingest, configuration recovery, privacy and persisted-case cleanup are covered. Final Debug/source build has zero warnings/errors; focused indexer tests pass 21/21 and full service assembly passes 1080/1080 without failures/errors/skips. Five review verification improvements and independent follow-up are complete; no new deferred work. See spec-dw-24-bounded-missing-case-warning.md and tests/dw-24-bounded-missing-case-warning-2026-10-17/validation.json. The separate 20-second Aspire baseline healthy wait timed out; topology qualification is not claimed.

### DW-25: Document Party index ProjectionVersion semantics

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: Party index projection fold and ProjectionVersion
reason: Index `ProjectionVersion` scheme (`global:N` / `{id}:{seq}` / keep-current) lacks Fold/class remarks for freshness/query consumers.
status: done 2026-10-18
archived: 2026-10-21
resolution: Documented the existing version selection and validation rules at the SDK handler/fold, read-model property and data-model guide. Consumer guidance covers duplicate persistence, rebuilds, cleanup, strict parsing, the legacy global-identifier ambiguity and the limits of source-position and timestamp freshness signals. Executable C# is unchanged. Final Debug/source build has zero warnings/errors; focused existing SDK projection tests pass 89/89 without failures/errors/skips. All four independent review findings are corrected and follow-up verified; no new deferred work. See spec-dw-25-index-projection-version.md and tests/dw-25-index-projection-version-2026-10-18/validation.json.

### DW-26: Define stable Article 30 operation categories

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)"), 2026-09-06
location: GetOperationCategory
reason: `GetOperationCategory` default arm returns a short event-type name rather than a stable category vocabulary — Art.30 taxonomy design choice.
status: done 2026-10-17
archived: 2026-10-21
decision: 2026-09-06 Use stable Other — Map unknown events to a stable Other category and retain EventType separately for bounded diagnostics.
resolution: Uncategorized processing records now use Other while retaining EventType and existing outcome/checkpoint behavior. Established categories, decodable redacted payloads and legacy persisted categories are preserved; serialized SDK rebuild output normalizes legacy categories. Debug source-reference builds pass with zero warnings/errors; focused tests pass 13/13 and full Projections.Tests passes 259/259 without failures/errors/skips. All four independent review findings are addressed; no new deferred work. See spec-dw-26-stable-processing-categories.md and tests/dw-26-stable-processing-categories-2026-10-17/validation.json. Baseline topology reported security Unhealthy and services Waiting; healthy topology acceptance is not claimed.

### DW-35: Define whole-payload json-redacted event handling

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04)"), 2026-09-06
location: whole-payload json-redacted event handling
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Whole-payload `json-redacted` events still depend on a resolvable CLR type and can apply a default-valued event produced from `{}`. The same behavior existed in the retired actor path, and the current field-level protection service does not normally produce a root encrypted marker; correcting it belongs to the broader payload-redaction contract rather than this migration patch chunk. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": A parameterless event can deserialize from an empty redacted payload into a valid `IEventPayload` and be applied as a real domain fact, while whole-payload redaction is otherwise intended to skip application and advance only the checkpoint.
status: done 2026-10-18
archived: 2026-10-21
decision: 2026-09-06 Always checkpoint only — Treat every root json-redacted payload as checkpoint-only regardless of CLR shape and add compatibility tests.
resolution: Whole-payload json-redacted empty objects and historical null now advance projection checkpoints before CLR resolution without applying default domain facts. Nonempty field-redacted objects and ordinary JSON retain existing behavior. Persisted active/erased state, audit retry repair, useful field-redaction effects and executed rebuild repair are covered. Final Debug/source builds have zero warnings/errors; focused cases pass 131/131, full projection suite 291/291 and unchanged-source service suite 1157/1157 without failures/errors/skips. All six independent review findings and follow-up are resolved; no new deferred work. Existing affected or unresolved-only projections need a full SDK rebuild. See spec-dw-35-redacted-checkpoints.md and tests/dw-35-redacted-checkpoints-2026-10-18/validation.json.

### DW-36: Align reconciliation LastIndexedEvent with the canonical fold

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.BuildReconciliationFold
reason: `PartyIndexSdkProjectionHandler.BuildReconciliationFold` recomputes `LastIndexedEvent` via a separate code path from the canonical `FoldCore`, used only on the already-confirmed idempotent-no-op/reconciliation branch — could pick a different "last event" for search-reconciliation notification metadata on a multi-event no-op batch, but doesn't affect canonical read-model correctness. Needs a dedicated multi-event test to pin the intended behavior.
status: done 2026-10-18
archived: 2026-10-21
resolution: Canonical index writes retain one payload-free source-event descriptor per active aggregate; recreated-handler retries use persisted provenance, including older deliveries. Historical rows use explicit synthetic attribution. Erasure removes target metadata, and serialized rebuilds preserve canonical selection. Baseline regression reproduced; final Debug/source builds have zero warnings/errors, full projection tests pass 316/316 and service tests 1158/1158 without failures/errors/skips. All eight accepted independent review findings were addressed through verification and documentation; no new deferred work. See spec-dw-36-canonical-search-notification.md and tests/dw-36-canonical-search-notification-2026-10-18/validation.json. External-search concurrency fencing remains separate.

### DW-37: Test blocked Memories cleanup without a CaseId

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyMemoryCleanupService
reason: `PartyMemoryCleanupService`'s new "no persisted CaseId and no fallback configured" blocked branch has zero test coverage.
status: done 2026-10-18
archived: 2026-10-21
resolution: Added eight cases covering null/empty/whitespace persisted CaseIds, empty/whitespace fallbacks, blocked mappings before/after eligible units, retained serialized inventory, scoped DELETE requests, sanitized diagnostics and recovery through restored configuration or captured-case repair. Executable production source is unchanged. Final Debug/source build has zero warnings/errors; cleanup tests pass 41/41 and the full service assembly passes 1195/1195 without failures/errors/skips. All three independent review findings are patched; no new deferred work. See spec-dw-37-blocked-memories-cleanup.md and tests/dw-37-blocked-memories-cleanup-2026-10-18/validation.json. The separate Aspire baseline failed to build with MSB4166 and canceled NuGet restores; no full-topology qualification is claimed.

### DW-46: Align PartySdkProjectionFold logging with house style

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold.Log and Hexalith.Parties.Projections.csproj
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — the `Log` class's comment claims `[LoggerMessage]` can't be used because `Hexalith.Parties.Projections.csproj` lacks a direct `Microsoft.Extensions.Logging.Abstractions` package reference, but `Hexalith.Parties.Security.csproj` is in the identical situation and successfully uses `[LoggerMessage]` throughout (`PartyKeyLifecycleService.cs`, `DecryptionCircuitBreaker.cs`, `PartyErasureOrchestrator.cs`) via a package reference with `ExcludeAssets="all"`. Adopting the same fix (or correcting the comment if a real difference is found) needs a deliberate, verified change to build configuration, not a same-pass patch.
status: done 2026-10-18
archived: 2026-10-21
resolution: Converted the seven fold diagnostics to source-generated LoggerMessage methods using existing project configuration. User-approved stable IDs 4802–4808 and generated event names replace generic ID zero; templates, fields, severities, caller categories, checkpoint behavior and exception privacy are preserved. Operator filter migration is documented. All three independent review layers completed; eight small review corrections are verified and two low-value test-refactoring/coverage suggestions are rejected with evidence. Final Debug/source build has zero warnings/errors; focused tests pass 13/13 and full Projections.Tests passes 348/348 with no failures/errors/skips/not-run. Warning and whitespace gates pass; no new deferred work. DW-47 remains unchanged. See spec-dw-46-generated-projection-fold-logging.md and tests/dw-46-generated-projection-fold-logging-2026-10-18/validation.json.

### DW-47: Give projection-fold drop diagnostics stable logger categories

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold and PartyProcessingActivityFold diagnostics
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — drops detected inside the shared static helpers `PartySdkProjectionFold`/`PartyProcessingActivityFold` are logged under `PartyDetailSdkProjectionHandler`'s or `PartyIndexSdkProjectionHandler`'s log category depending purely on which handler called in. An operator filtering by the actual source class gets nothing, and the same drop reason can appear under two different categories. Fixing this cleanly needs a design decision (e.g., a dedicated logger category or `ILoggerFactory` seam), not a quick patch.
status: done 2026-10-18
archived: 2026-10-21
decision: 2026-09-06 Dedicated fold categories — Give each fold a dedicated typed or named logger category, inject it through handlers, and lock category stability with tests.

resolution: Both handlers now inject named fold loggers through ILoggerFactory, preserving event IDs 4802–4808, structured metadata, exception privacy and diagnostic frequency. Index summary 4801 retains its handler category. Operator filters and manual constructor migration are documented. Final Debug/source build has zero warnings/errors; category tests pass 31/31 and full projection assembly passes 379/379 without failures/errors/skips/not-run. All three independent review layers completed; four small test/evidence findings are corrected and four low-value suggestions are rejected with evidence. No new deferred work. See spec-dw-47-stable-fold-logger-categories.md and tests/dw-47-stable-fold-logger-categories-2026-10-18/final-validation.json.

### DW-48: Bound dropped-event diagnostic volume during full rebuilds

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartyIndexSdkProjectionHandler.AccumulateAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding — `PartyIndexSdkProjectionHandler.AccumulateAsync` (the full-rebuild path) now re-emits a log line for every historically-known-bad event on every rebuild run, with no batching, sampling, or dedup — a real log-flooding risk on a large event store. Needs a product/ops decision on acceptable rebuild-time log volume, not a same-pass patch.
status: done 2026-10-18
archived: 2026-10-21
decision: 2026-09-06 Summarize diagnostics — Accumulate bounded counts by failure reason and emit one no-PII summary at rebuild completion.
resolution: Rebuild accumulation now carries seven fixed saturating drop counters in serialized operation-owned candidates and emits one value-free structured summary per successful completion invocation; failed accumulation emits a bounded warning. Counts survive resumed handlers, repeated histories and empty accumulation without entering the canonical index. Legacy raw candidates remain readable; malformed envelopes reject. Live diagnostics, canonical state and search reconciliation are preserved. All eight independent review findings and follow-ups are resolved. Final Debug/source builds have zero warnings/errors; focused projection tests pass 145/145, full Projections.Tests 335/335 and full Parties.Tests 1195/1195 without failures/errors/skips. No new deferred work. See spec-dw-48-bounded-rebuild-diagnostics.md and tests/dw-48-bounded-rebuild-diagnostics-2026-10-18/validation.json. The separate 20-second Aspire healthy wait timed out (exit 17); no healthy topology qualification is claimed.

### DW-49: Handle converter format and overflow failures during event deserialization

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: PartySdkProjectionFold.DeserializeNew
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: 2026-08-05 review-layer finding, pre-existing (not caused by this session's patch): the catch filter only covers `JsonException`/`ArgumentNullException`/`NotSupportedException`/`InvalidOperationException`; a `FormatException` or `OverflowException` thrown by a custom converter propagates unhandled and crashes the whole dispatch instead of being skip-logged.
status: done 2026-10-17
archived: 2026-10-21

resolution: `PartySdkProjectionFold.DeserializeNew` now treats converter FormatException and OverflowException as existing corrupt-payload skip-and-advance outcomes. Actual serializer, privacy, live/redacted audit, checkpoint and persisted handler cases pass; final Projections.Tests 252/252 and Parties.Tests 1027/1027, zero failures/errors/skips. See `spec-dw-49-converter-failures.md`.

### DW-59: Preserve mappings when unit ID and source URI match different rows

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartyMemoryUnitMappingStore
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Edge-case review found a second live mapping row can be dropped when two entries match the new unit id and source uri separately.
status: done 2026-10-18
archived: 2026-10-21
resolution: Prioritize eligible unit-ID mappings over source-only matches, preserving distinct cleanup units across row order, legacy-case adoption and ETag retries. Keep source-only replacement compatibility when no eligible unit exists. Debug/source build has zero warnings/errors; 180/180 focused and 1251/1251 service tests pass with no skips. Four independent review findings were patched and follow-up verified; no new deferred work. See spec-dw-59-preserve-colliding-memory-mappings.md and tests/dw-59-mapping-collisions-2026-10-18/validation.json.

### DW-60: Restore bounded allowlisted party IDs on SDK detail queries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Party SDK query detail-envelope validation
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: TryValidateDetailEnvelope only rejects reserved chars after TenantSafeProjectionReadGuardrailsTests were deleted; oversized/malformed party ids are weakly gated.
status: done 2026-10-19
archived: 2026-10-21
resolution: SDK detail/privacy queries now reuse PartyIdentifier.IsValid before any read-model or erasure-record access, preserving ordinal envelope agreement, optional EntityId fallback, ULIDs and supported legacy GUID forms. All six handlers have boundary, malformed-input, fallback and route regressions. Final Debug/source build has zero warnings/errors; focused handlers pass 685/685 and full affected service assembly passes 1898/1898 without failures/errors/skips/not-run. Four independent review findings are patched and follow-up verified; no new deferred work. See spec-dw-60-bounded-detail-query-identifiers.md and tests/dw-60-bounded-detail-query-identifiers-2026-10-19/validation.json.

### DW-62: Use TimeProvider for erasure cleanup timestamps

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: erasure cleanup TimeProvider usage
reason: Erasure cleanup timestamps still use `DateTimeOffset.UtcNow` instead of the newly registered `TimeProvider` — deferred, pre-existing certificate timestamp pattern across erasure store results.
status: done 2026-10-19
archived: 2026-10-21
resolution: All seven registered cleanup-result timestamps and both verification-service timestamps use the shared TimeProvider at completion. The original public constructor retains system-clock behavior; returned store timestamps survive sanitization. Clock-controlled asynchronous cleanup, multi-delegate completion and processor certificate read-back are covered. Debug/source builds have zero warnings/errors; focused verification/delegate/certificate checks pass 23/23, 10/10 and 1/1; full affected assemblies pass 244/244 and 1901/1901 without failures, errors, skips or not-run tests. Six independent review findings are patched and follow-up verified; no new deferrals. See spec-dw-62-erasure-cleanup-clock.md and tests/dw-62-erasure-cleanup-clock-2026-10-19/validation.json.

### DW-63: Protect Memories mapping replacement and clearing with concurrency-safe retries

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryUnitMappingStore.ClearMappingsAsync and ReplaceMappingsAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryUnitMappingStore.ReplaceMappingsAsync` and the empty-list delete can overwrite a concurrent indexing write after cleanup reads the prior mapping set. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `ClearMappingsAsync` and `ReplaceMappingsAsync` use unconditional delete/save operations, so concurrent indexing can lose a newly committed mapping and leave an undiscoverable Memories unit.
status: done 2026-10-18
archived: 2026-10-21
activated_by_spec: `_bmad-output/implementation-artifacts/spec-dw-63-concurrent-memories-cleanup.md`
resolution: Exact cleaned-entry subtraction now retries against fresh ETags, preserves concurrent additions and changed unit/source/case identities, and retains empty domain inventories to prevent Redis ETag reuse. Physical clearing is restricted to one-use health probes. Cleanup requires another pass when mappings remain. Final Debug/source build has zero warnings/errors; focused tests pass 133/133 and full service assembly passes 1187/1187. Rollback passes 1158/1158 baseline tests, and the unsafe-delete mutation is detected. All eight review findings are patched and independently verified; no new deferred work. See spec-dw-63-concurrent-memories-cleanup.md and tests/dw-63-concurrent-memories-cleanup-2026-10-18/validation.json. Drain older writers before rollout; separate cancellation, workflow-reconciliation and persistence-migration work remains open.

### DW-64: Persist partial Memories cleanup progress after caller cancellation

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryCleanupService.DeleteByPartyAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryCleanupService.DeleteByPartyAsync` uses the already-cancelled caller token in its `finally` mapping update, so cancellation can prevent the promised resumable audit state from being saved.
status: done 2026-10-20
archived: 2026-10-21
resolution: Confirmed deletion progress is reconciled through exact-entry, concurrency-safe removal using an independent five-second cooperative cancellation budget before caller cancellation propagates. Unfinished, ambiguous and concurrent mappings remain discoverable; failed recovery logs only its type and preserves caller cancellation. Debug/source build has zero warnings/errors; 96 focused and 1920 full service tests pass without failures/errors/skips/not-run. Exercised rollback passes 1901 baseline tests and restores implementation bytes exactly. All six independent review findings are patched and follow-up verified; no new deferrals. See spec-dw-64-cancelled-cleanup-progress.md and tests/dw-64-cancelled-cleanup-progress-2026-10-20/validation.json. Existing-build EventStore/Tenants startup failures remain a separate topology qualification limitation; terminal Memories workflow completion is not claimed.
activated_by_spec: `_bmad-output/implementation-artifacts/spec-dw-64-cancelled-cleanup-progress.md`

### DW-65: Compensate after cancellation between Memories ingestion and mapping persistence

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: PartyMemoryIndexingService
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryIndexingService` rethrows caller cancellation from `RecordMappingAsync` without deleting the already-created Memories unit, leaving an untracked unit outside erasure discovery. Also recorded in "Deferred from: bmad-build Story 8.6 review (2026-08-16)": `PartyMemoryIndexingService` propagates caller cancellation from the mapping write without deleting the already-created unit, leaving it outside later erasure discovery.
status: done 2026-10-20
archived: 2026-10-21
resolution: Caller cancellation after acknowledged Memories ingestion now awaits compensating DELETE under an independent five-second cooperative recovery token, then rethrows the original cancellation. Captured tenant/case/unit routing, response qualification, ordinary cancellation behavior and persisted mapping inventories are preserved. Final Debug/source build has zero warnings/errors; focused tests pass 61/61 and the complete affected service assembly passes 1933/1933 without failures/errors/skips. The baseline reproduces all five response regressions; exercised rollback passes 1920/1920 existing tests. All five independent review findings are patched and follow-up verified; no new deferred work. See spec-dw-65-cancelled-indexing-compensation.md and tests/dw-65-cancelled-indexing-compensation-2026-10-20/validation.json. Terminal deletion and durable reconciliation remain open as DW-168/DW-170; no complete-topology qualification is claimed.

### DW-67: Resolve consumer validation artifacts from central package versions

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-consumer-package-references.py` hard-codes obsolete FrontComposer and Tenants versions instead of the currently evaluated dependency set.
status: done 2026-10-20
archived: 2026-10-21

resolution: FrontComposer and Tenants support packages now use evaluated central MSBuild versions instead of obsolete constants, with exact generated artifact validation. Final Debug CI build has zero warnings/errors; 14 focused and 128 full affected CI tests pass without failures/errors/skips/not-run. Three independent review corrections are patched and verified; two pre-existing shared-normalization/restore-graph limitations are recorded separately below. Package versions, gitlinks, public contracts and runtime code are unchanged. See spec-dw-67-central-consumer-support-versions.md and tests/dw-67-central-consumer-support-versions-2026-10-20/validation.json.

### DW-69: Restrict consumer validation to configured NuGet sources

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-consumer-package-references.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The generated NuGet configuration lacks `<clear/>`, and the CLI always retains nuget.org, allowing undeclared user or machine feeds to hide incomplete local package output.
status: done 2026-10-20
archived: 2026-10-21
resolution: Generated consumer packageSources now clears inherited feeds, and explicit repeated --nuget-source values replace the nuget.org default while preserving both local feeds. Four CLI cases and a real offline NuGet restore cover exact source selection, inherited-feed rejection, explicit-feed success and XML escaping. Final Debug CI build has zero warnings/errors; 16 focused and 130 full affected CI tests pass without failures/errors/skips/not-run, including both new probes under PYTHONOPTIMIZE=1. Two introduced review findings are corrected and independently verified; inherited fallback-folder and disabled-source limits are documented and deferred separately. See spec-dw-69-consumer-nuget-source-isolation.md and tests/dw-69-consumer-nuget-source-isolation-2026-10-20/validation.json. Private-feed authentication binds to documented generated names; full Release consumer and fresh topology qualification remain separate gates.

### DW-70: Compare forbidden NuGet package IDs case-insensitively

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: scripts/validate-nuget-packages.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `scripts/validate-nuget-packages.py` performs case-sensitive package-ID checks even though NuGet identifiers are case-insensitive.
status: done 2026-10-20
archived: 2026-10-21
resolution: Forbidden dependency IDs and fragments are compared with casefold while preserving the policy vocabulary, substring matching and original diagnostic spelling. Eighteen executable cases cover 216 zipped-nuspec fixtures across four casings, direct/first/last-group layouts, allowed near matches and multi-ID diagnostics. Final Debug CI build has zero warnings/errors; 34 focused, 148 complete affected CI tests and 18 optimized-environment cases pass without failures/errors/skips/not-run. Three review verification gaps were corrected and independently checked with targeted mutations; no new deferred work. See spec-dw-70-case-insensitive-forbidden-packages.md and tests/dw-70-case-insensitive-forbidden-packages-2026-10-20/validation.json. Existing-build Parties baseline was healthy; complete Release package publication and fresh full-topology qualification remain separate CI gates.

### DW-71: Pin reusable CI and CodeQL workflows to immutable revisions

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows/ci.yml and .github/workflows/codeql.yml
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `.github/workflows/ci.yml` and `.github/workflows/codeql.yml` invoke reusable workflows through mutable `@main` references.
status: done 2026-10-20
archived: 2026-10-21
resolution: Both reusable validation callers now pin Builds 332b2c144defba546fe0a09626a4a72f2340e22f, with exact remote workflow-blob verification and aligned update guidance. Final Debug/source build has zero warnings/errors; 48 focused workflow and 148 full CI tests pass without failures/errors/skips/not-run. Three independent review findings are patched and verified, including commented-reference rejection and selected-object checkout-divergence/missing-object probes; no new deferred work. Release execution pin and root gitlinks remain unchanged. See spec-dw-71-immutable-ci-workflows.md and tests/dw-71-immutable-ci-workflows-2026-10-20/validation.json.

### DW-72: Publish Aspire-hosted services with Production defaults

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: src/Hexalith.Parties.AppHost/Program.cs
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `src/Hexalith.Parties.AppHost/Program.cs` emits `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` as `Development` for publish output.
status: done 2026-10-20
archived: 2026-10-21

resolution: A publish-only policy sets both .NET environment variables to Production on every project resource, including optional services, while local profiles and explicit Staging values remain intact. Debug/source builds have zero warnings/errors, with 8 publish/run cases, 3 existing UI cases and 16 fitness checks passing. The original baseline full assembly passed 1933/1933; after rebasing onto concurrent sync bec09f01aa00bd9ef83554fd099ed36fb59ab2c6, the full assembly passes 2001/2003 with two documentation/receipt failures reproduced unchanged on that parent checkout. Four review gaps are corrected and independently verified. Healthy local Parties and UI stop cleanly; full topology and Release qualification remain separate. Existing Memories production authentication and parent metadata drift are deferred below. DW-72 changes no dependency versions or gitlinks. See spec-dw-72-production-publish-environments.md and tests/dw-72-production-publish-environments-2026-10-20/validation.json.

### DW-73: Fail publish preflight when the OIDC client secret is missing

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: src/Hexalith.Parties.AppHost/Program.cs
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `src/Hexalith.Parties.AppHost/Program.cs` substitutes an empty client secret and continues producing deployment artifacts that cannot authenticate.
status: done 2026-10-20
archived: 2026-10-21
resolution: The retained AppHost rejects missing, empty and whitespace OIDC secrets before constructing its publish model, with actionable configuration guidance and unchanged valid secret bytes. Debug/source builds pass with zero warnings/errors; 40 model checks, 29 hostile-environment checks and 2004 service tests pass without failures or skips. Local Keycloak enabled/disabled behavior and the documented environment variable are covered. Independent review corrected fixture-provider precedence and verified the follow-up. See spec-dw-73-publish-oidc-secret.md and tests/dw-73-publish-oidc-secret-2026-10-20/validation.json. Full topology and release qualification remain separate.


### DW-74: Reject duplicate keys in merged BMAD configuration arrays

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: _bmad/scripts/config_utils.py
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `_bmad/scripts/config_utils.py` can retain repeated base codes or ids, leaving ambiguous effective configuration after overrides.
status: done 2026-10-20
archived: 2026-10-21
resolution: Per-input and merge-selected keyed identities now reject duplicates, including single-layer and nested arrays. Errors identify files, paths, sides and item indexes without identifier values. Valid replacement/order/append semantics remain intact; real central configuration and all 49 installed skill customizations match baseline. All 17 Python methods and 48 adjacent CI contract tests pass; Debug build has zero warnings/errors. actionlint and the warning/submodule guard pass. A dedicated CI job runs the suite. Independent review and follow-ups are resolved with no new deferred work. See spec-dw-74-duplicate-bmad-identifiers.md and tests/dw-74-duplicate-bmad-identifiers-2026-10-20/validation.json.

### DW-75: Tolerate deleted historical tags during release verification

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows/release.yml
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `.github/workflows/release.yml` can report a successful current publication as failed when an older release references a tag that no longer exists.
status: done 2026-10-20
archived: 2026-10-21
resolution: Publication verification skips only explicit HTTP 404 tag-ref responses, then still requires a validated non-draft release tag resolving to the exact dispatched SHA. Other API/transport failures and invalid refs or annotated objects remain fatal. The original workflow fails eight new regressions; final Debug/source build has zero warnings/errors, focused shell fixtures pass 36/36 and the complete affected CI assembly passes 184/184 without errors/failures/skips/not-run. Workflow lint and repository guards pass. All five independent review findings are corrected and follow-up verified; no new deferred work. See spec-dw-75-deleted-historical-release-tags.md and tests/dw-75-deleted-historical-release-tags-2026-10-20/validation.json.

### DW-76: Restore the Playwright accessibility lane as a required CI gate

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: .github/workflows, scripts/test.ps1, and tests/e2e/package.json
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The replacement CI workflow no longer runs `npm run test:a11y`, leaving axe, keyboard-focus, forced-colors, computed-style, and visual checks unexecuted. Also recorded in "Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)": The Playwright accessibility lane is wired into no workflow. `.github/workflows/` contains no Playwright or `test:a11y` step, `scripts/test.ps1` invokes no npm lane, and `tests/e2e/package.json`'s `test:a11y` script is called by nothing. Spine §7 I12 already records the always-on CI a11y lane as a separate open ledger item.
status: done 2026-10-20
archived: 2026-10-21
resolution: Restored unconditional blocking ui-a11y CI with Release/package browser execution and retained diagnostics; six Chromium cases and 188 CI tests pass. See spec-dw-76-required-accessibility-ci.md and tests/dw-76-required-accessibility-ci-2026-10-20/validation.json.

### DW-77: Exercise mTLS across every configured Dapr sidecar

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: configured Dapr sidecar topology tests
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Current tests inspect generated YAML and one synthetic sidecar but never start the mTLS topology or prove a cross-service invocation with all sidecars credentialed.
status: done 2026-10-20
archived: 2026-10-21
decision: 2026-09-06 Add test-only topology support — Introduce test-scoped readiness and tenant bootstrap, run all sidecars, and prove a credentialed cross-service invocation.

resolution: All eight complete-profile sidecars initialized Dapr security and answered their own metadata; real Parties-to-Tenants readiness returned 200 through the existing ACL. Authenticated callback bootstrap passed and missing/wrong tokens were rejected; controlled production subscription/access registrations verify membership and authorization effects. Final CI-runtime 1.18.2 proof: 2/2 pass, no skips, including the repaired admin UI-to-admin path; Release/package assembly: 98 pass, zero failures, 6 pre-existing health-test skips. Commands, scope limits and sanitized proof: tests/dw-77-all-sidecar-mtls-2026-10-20/validation.json and runtime-proof.txt.

### DW-78: Align PartyErased timestamp resolution across projection folds

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
location: PartyProcessingActivityFold.Fold and PartyDetailProjectionHandler.ApplyErasure
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyDetailProjectionHandler.ApplyErasure` assigns `ErasedAt = erased.ErasedAt` while `PartyProcessingActivityFold.Fold` assigns `@event.Timestamp.ToUniversalTime()`.
status: done 2026-10-19
archived: 2026-10-21
decision: 2026-09-06 Payload erasure instant — Use PartyErased.ErasedAt consistently across detail and processing folds and document the semantic choice.
resolution: Detail and Processing event-derived tombstones now use PartyErased.ErasedAt, preserving its offset independently of envelope audit/freshness timestamps. Six persisted live/rebuild regressions cover distinct times, duplicate delivery, legacy redelivery before repair, later checkpoints and cleanup. Debug/source build has zero warnings/errors; full Projections.Tests passes 385/385 without failures/errors/skips/not-run. All four independent review findings are patched and follow-up verified; no new deferred work. Existing checkpointed values require a full party-detail rebuild from retained readable history. See spec-dw-78-erasure-timestamp.md and tests/dw-78-erasure-timestamp-2026-10-19/validation.json.

### DW-79: Parallelize state-store reads in PartyDetailSdkProjectionHandler.PrepareRebuildAsync

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-6-projection-and-query-sdk-migration.md (2026-08-16)"), 2026-09-06
location: PartyDetailSdkProjectionHandler.PrepareRebuildAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PrepareRebuildAsync` awaits `readModelStore.GetAsync<PartyDetailSdkReadModel>` and `GetAsync<PartyProcessingSdkReadModel>` sequentially rather than concurrently with `Task.WhenAll`.
status: done 2026-10-20
archived: 2026-10-21
resolution: Rebuild preparation starts both independent snapshot reads before awaiting their joint completion, preserving full replay, keys, caller token and per-slot ETag concurrency. Final Debug build has zero warnings/errors; all 399 projection tests pass, including fourteen focused overlap/failure/cancellation cases. The original handler fails 13/14 focused cases and an early-cancellation mutation fails 2/2; implementation bytes are restored exactly. Independent review corrected two verification gaps and confirmed the follow-up; no new deferred work. Source-mode launcher and external-security topology blockers remain separately recorded; no Release or full-topology qualification is claimed. See spec-dw-79-parallel-rebuild-reads.md and tests/dw-79-parallel-rebuild-reads-2026-10-20/validation.json.

### DW-80: Make Memories rebuild reconciliation atomic with concurrent erase and re-add

status: done 2026-09-06
archived: 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
archived: 2026-10-13
reconciliation: 2026-10-19 The historical completion is superseded by the verified per-notification and post-operation reconciliation in DW-173/DW-193. The old once-per-completion read did not close the after-snapshot erasure race. See the archive correction and spec-dw-173-erasure-search-notification-fence.md; atomic remote workflow erasure is not claimed.

### DW-85: Keep Memories cleanup health observable when indexing is disabled

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: MemoriesSearchHealthCheck
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `MemoriesSearchHealthCheck` returns Healthy immediately when indexing is disabled although previously persisted mappings can still require remote deletion and mapping-store access.
status: done 2026-10-20
archived: 2026-10-21
resolution: Disabled indexing now retains mapping-store health observation and probes retained cleanup configuration without a rich-search client. Incomplete retained configuration or failed probes degrades health; unconfigured local-only mode explicitly reports remote cleanup unverified. Sentinel contents, independently bounded cancellation cleanup, provider-text privacy and production DI credentials are covered. Debug build has zero warnings/errors and all 53 health cases pass. Complete service run passes 2101/2102; the sole unchanged dependency-receipt gate fails after external workspace sync advanced the Builds gitlink without updating its selected receipt. No dependency/full-topology signoff is claimed. All five review findings are corrected and independently confirmed; no new deferred work. See spec-dw-85-disabled-indexing-cleanup-health.md and tests/dw-85-disabled-indexing-cleanup-health-2026-10-20/validation.json.

### DW-86: Validate erasure certificates before certifying store cleanup

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: ErasureVerificationService.VerifyErasureAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `ErasureVerificationService.VerifyErasureAsync` accepts an `ErasureCertificate` but never verifies it belongs to the requested tenant and party or represents a completed key-destruction state.
status: done 2026-10-20
archived: 2026-10-21
resolution: Verification rejects mismatched scope, incomplete/unknown key status and malformed version evidence before any cleanup. Verified empty version lists remain valid for idempotent deletion. Cleanup retries preserve original certificate bytes; reports/status retain cleanup outcomes and timestamps. Admin confirmation requires affirmative cleanup status and uses its timestamp. Three affected Debug builds have zero warnings/errors; Security.Tests passes 280/280, AdminPortal.Tests 201/201, focused service tests 143/143 and complete service 2108/2109 (sole unchanged Builds dependency-receipt mismatch, no skips/errors/not-run). Five independent review findings are patched and legacy certificate recovery is explicitly deferred to key-management owners; follow-up confirms the corrections. No dependency, gitlink or broad-gate signoff is claimed. See spec-dw-86-erasure-certificate-validation.md and tests/dw-86-erasure-certificate-validation-2026-10-20/validation.json.

### DW-87: Handle null Party index dictionaries with bounded recovery

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyIndexSdkReadModel
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: A malformed persisted `PartyIndexSdkReadModel` with null dictionaries can reach dictionary operations and throw rather than producing a controlled rebuild-required or corruption result.
status: done 2026-10-20
archived: 2026-10-21

resolution: Reject explicit null Entries, LastIndexedEvents, LastSequenceNumbers or ErasureSequenceNumbers before live folding (including optimistic retries), query caching and coordinated erasure writes. Use existing bounded failure surfaces; evict unsafe cached rows. Reject malformed wrapped/raw rebuild candidates while allowing a valid full replay candidate to replace corrupt entries/descriptors/checkpoints using its ETag. Preserve omitted-property defaults and surviving cleanup-created permanent fences, including fences absent from event history. Explicit-null canonical erasure-fence evidence blocks finalization until owner-qualified restoration. Recovery and unavailable prior remote-search inventory are documented in docs/data-models.md; DW-82 remains separate.
verification: Debug affected-project builds have zero warnings/errors; complete projection suite passes 428/428, focused query/cache/adapter suite passes 785/785. Full service suite passes 2127/2128 with the unchanged dependency-receipt fitness gate reporting concurrently advanced EventStore identity drift. Independent review and final guard evidence are recorded by spec-dw-87-null-index-dictionaries.md and tests/dw-87-null-index-dictionaries-2026-10-20/validation.json.

### DW-88: Bound Party search inputs before cursor and index evaluation

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: Party search payload parsing and cursor-scope construction
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: Search payload parsing validates paging and type but imposes no length limits on strings copied into cursor scope and processed against tenant entries.
status: done 2026-10-21
archived: 2026-10-21

resolution: Reject decoded Query, Mode, CaseId and Type strings over 512 raw UTF-16 units before cursor decoding, index reads and search evaluation, counting whitespace before trimming. Preserve accepted bounded inputs and unsupported-filter failures; document the public query limit and mode-validation precedence. Both affected Debug builds have zero warnings/errors. Final service suite passes 2161/2162, including all 34 new regressions; Contracts.Tests passes 201/201. The sole full-service failure is the unchanged dependency-receipt fitness gate after the concurrent Builds gitlink sync. All four independent review findings are patched and follow-up confirmed; no new deferred item. Source launcher remains blocked by missing isolated Dapr home (HXR011). See spec-dw-88-bounded-search-inputs.md and tests/dw-88-bounded-search-inputs-2026-10-21/validation.json.

### DW-119: RecordConsentValidator does not guard LawfulBasis with IsInEnum and RevokeConsentValidator leaves Reason unbounded, unlike every sibling command validator.

origin: spec-deferred 5a330376d9e2
location: src/Hexalith.Parties/Validation/RecordConsentValidator.cs and RevokeConsentValidator.cs
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
reason: AddContactChannelValidator, AddIdentifierValidator and CreatePartyValidator all call IsInEnum on their enum property, and RestrictProcessingValidator bounds Reason to 256 characters. RecordConsent carries LawfulBasis straight into ConsentRecorded, so an out-of-range cast is persisted. Deferred rather than patched because the intent contract's Never clause forbids changing lawful-basis behaviour; adding the rule would reject commands that are accepted today.
status: done 2026-10-16
archived: 2026-10-21
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
archived: 2026-10-21
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
archived: 2026-10-21
resolution: Consumer consent grant/withdraw now map delegated ArgumentException validation failures to the existing safe ValidationRejected outcome. Null requests, caller cancellation, other exceptions, returned mappings and fail-closed self-scoping remain covered. Ten validation regressions fail against the original adapter; final focused tests pass 44/44 and full UI/ConsumerPortal suites pass 388/388 and 82/82 (470 total), with zero failures/errors/skips. Debug/source builds have zero warnings/errors. Four independent review verification findings and follow-up are complete. Malformed historical identifiers are synthetic, with no live-tenant incident claimed. The separate 20-second Aspire baseline health timeout remains recorded; no runtime, dependency, gitlink, wire-contract or persistence changes. See spec-dw-121-consumer-consent-validation.md and tests/dw-121-consumer-consent-validation-2026-10-17/validation.json.

### DW-122: No test asserts that the production DI extension registers the consent validators, and the domain processor fails open when a validator is missing.

origin: spec-deferred f8aa7466374b
location: src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:376
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
original_reason: PartyDomainProcessorValidationTests.CreateInvoker builds its own AddValidatorsFromAssemblyContaining scan rather than calling PartiesServiceCollectionExtensions.AddHexalithParties, and PartyDomainProcessor.TryRejectInvalidPayloadAsync logs and returns null when no validator resolves. A regression in the production registration surface would leave every command unvalidated with the suite green. Pre-existing infrastructure design, not introduced here.
status: done 2026-10-16
archived: 2026-10-21

implementation_spec: `_bmad-output/implementation-artifacts/spec-dw-122-production-validation.md`
resolution: Production AddParties registration is covered across the command inventory; absent validators reject before protected-state access, preserving exactly four existing aggregate-only commands and honoring supplied validators for all four. Debug source build passes with zero warnings/errors, focused tests pass 106/106, and full affected assembly passes 998/998 without skips. Independent review and targeted follow-up completed. Named-enum compatibility is separately tracked by DW-195. Evidence: `tests/dw-122-production-validation-2026-10-16/validation.json`.

### DW-123: ContactChannelNotFound and IdentifierNotFound still interpolate caller-supplied identifiers into their messages outside the two consent handlers.

origin: spec-deferred 671760729b1e
location: src/Hexalith.Parties/Domain/PartyAggregate.cs:412,415,423,426,1174,1229,1305
source_spec: `spec-safe-consent-identifiers.md`
severity: medium
reason: PartyAggregate.cs lines 412, 415, 423, 426, 1174, 1229 and 1305 still emit $"Contact channel '{id}' not found." / $"Identifier '{id}' not found.", so the same event type now has two message dialects depending on which command produced it. Those call sites belong to commands outside this story's intent contract and were not touched by it.
status: done 2026-10-17
archived: 2026-10-21

implementation: 2026-10-17 All six remaining channel/identifier not-found branches now use fixed diagnostics; composite Rejected details agree with persisted rejection events. Rejection types and validation/lookup semantics are preserved. See spec-dw-123-bounded-not-found-diagnostics.md.
previous_validation_blocker: The original service run passed 1008/1010 after concurrent dependency sync 90af3047b50046cb4f6f91e01f182f562110aab3; FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph and MaintainedTechnologyTablesMatchCentralPackageCatalog failed. That historical result is preserved in tests/dw-123-bounded-not-found-diagnostics-2026-10-17/validation.json.
resolution: Completion revalidation at Parties 4e4f271faa5eb141834a1e53eeebe8a2e1aef68c passes Server.Tests 315/315 and Parties.Tests 1080/1080, with zero failures/errors/skips/not-run and zero warnings/errors in both Debug/source builds. All 12 original failing-before diagnostics and both former fitness blockers pass. Independent review identified four completion-record improvements, all addressed; no new deferred work. Production source, tests and dependencies were unchanged in this continuation. See spec-dw-123-bounded-not-found-diagnostics.md and tests/dw-123-bounded-not-found-diagnostics-2026-10-17/completion-validation.json.

### DW-127: Align ineligible identity resolver and client replies

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs:58,79; src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:58
severity: medium
status: done 2026-10-17
archived: 2026-10-21
resolution: Preserved typed Ineligible replies for explicit Unknown classification while rejecting incomplete wire evidence, unsupported/foreign observations and every nonresolved human binding. Actual query-handler/gateway JSON/client round trips cover inactive, restricted, erasing, erased, unbound and resolved controls. Final Debug/source Client.Tests 330/330, Parties.Tests 1027/1027 and Contracts.Tests 199/199 pass with zero skips/errors/failures; builds have zero warnings/errors. Independent review and follow-ups complete; no new deferred work. Current dependency/doc baseline observations refreshed without changing versions, gitlinks or approvals. See spec-dw-127-ineligible-identity-round-trip.md and tests/dw-127-ineligible-identity-round-trip-2026-10-17/validation.json. Resolves DW-127/DW-146 together; no installed identity or full-topology qualification claimed.

### DW-146: Align server-produced Ineligible human/Unknown evidence with the public client validation contract.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs:58,79; src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:58
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 1 and Verification 3 share this root cause: the resolver can return Ineligible with a live HumanBinding or Unknown classification, while the client turns those shapes into Unavailable/503. Both paths were already committed in 37d87f5a2869b076c651a58714d60f64647848bc and are unchanged by this repair. Exit proof: explicit fail-closed response semantics and producer/client round-trip tests for inactive, restricted, erasing, and unknown Parties.
status: done 2026-10-17
archived: 2026-10-21
resolution: Preserved typed Ineligible replies for explicit Unknown classification while rejecting incomplete wire evidence, unsupported/foreign observations and every nonresolved human binding. Actual query-handler/gateway JSON/client round trips cover inactive, restricted, erasing, erased, unbound and resolved controls. Final Debug/source Client.Tests 330/330, Parties.Tests 1027/1027 and Contracts.Tests 199/199 pass with zero skips/errors/failures; builds have zero warnings/errors. Independent review and follow-ups complete; no new deferred work. Current dependency/doc baseline observations refreshed without changing versions, gitlinks or approvals. See spec-dw-127-ineligible-identity-round-trip.md and tests/dw-127-ineligible-identity-round-trip-2026-10-17/validation.json. Resolves DW-127/DW-146 together; no installed identity or full-topology qualification claimed.

### DW-128: Validate binding command acknowledgments

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:121-150
severity: medium
status: done 2026-10-17
archived: 2026-10-21
resolution: Required per-attempt correlation/message identity, validated every case-insensitive ID occurrence and explicit outcome flags, and preserved compatible optional fields, provisioning, HTTP failures and cancellation. Independent review and follow-up completed. Final Debug/source Client.Tests passes 301/301 including 145 identity cases; Debug/source and Release/package builds pass with zero warnings/errors. See spec-dw-128-binding-command-acknowledgments.md and tests/dw-128-binding-acknowledgments-2026-10-17/validation.json. Resolves DW-128/DW-147 together; no new deferred work.

### DW-147: Reject empty or unrelated successful HTTP objects as binding command acknowledgments.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties.Client/HttpPartiesIdentityClient.cs:121-150
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 2: SubmitAsync deserializes any object without explicit false/rejected flags; SubmitCommandResponse permits default constructor values, and binding methods discard it. An empty object can complete the call without matching correlation/message identity. Pre-existing 37d87f5a2869b076c651a58714d60f64647848bc code, unchanged here. Exit proof: malformed, missing-field, and mismatched acknowledgment rejection plus valid acknowledgment acceptance.
status: done 2026-10-17
archived: 2026-10-21
resolution: Required per-attempt correlation/message identity, validated every case-insensitive ID occurrence and explicit outcome flags, and preserved compatible optional fields, provisioning, HTTP failures and cancellation. Independent review and follow-up completed. Final Debug/source Client.Tests passes 301/301 including 145 identity cases; Debug/source and Release/package builds pass with zero warnings/errors. See spec-dw-128-binding-command-acknowledgments.md and tests/dw-128-binding-acknowledgments-2026-10-17/validation.json. Resolves DW-128/DW-147 together; no new deferred work.

### DW-129: Reject ineffective identity-history snapshot protection

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:294; src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs:90; tests/Hexalith.Parties.Security.Tests/IdentityHistoryProtectionTests.cs:39
severity: high
status: done 2026-10-17
archived: 2026-10-21
note: 2026-10-05 (Story 8.10 review loop 3, Blind 8) — DW-142 names this entry as the open carrier for classifying `PartyState.HumanActorBindings` and `PartyState.HumanActorTransitions`, which `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs` currently records as `DeferredPrivacyDesign`. This entry's exit proof therefore also replaces those two `DeferredPrivacyDesign` rows with a decided classification in `PersonalDataInventoryTests`, with Contracts.Tests green.
resolution: Required current purpose-protected SDK snapshot evidence, self-contained detached ciphertext, and rejection of no-op/plaintext/encoding-only copies before reporting protected metadata. Replaced the two deferred history inventory classifications with independent purpose-protected personal data. Full Security.Tests 218/218 and Contracts.Tests 199/199 pass. Independent review and follow-up complete; see spec-dw-129-identity-history-snapshot-protection.md and tests/dw-129-identity-history-snapshot-protection-2026-10-17/validation.json. Existing SDK serialized-history capture and production custody qualification remain separate open work. No commit or push.

### DW-148: Require identity-history snapshot protection evidence before reporting protected metadata.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:294; src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs:90; tests/Hexalith.Parties.Security.Tests/IdentityHistoryProtectionTests.cs:39
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 3: the snapshot provider result is wrapped without effectiveness validation; the synthetic test provider returns its input. History evidence has no profile PersonalData annotation, while the adapter reports the wrapper as Protected. Existing identity-feature code from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: reject no-op custody snapshots and show persisted snapshot representation does not expose actor history. The synthetic test is not live custody qualification.
status: done 2026-10-17
archived: 2026-10-21
resolution: Resolved together with DW-129: typed provider evidence and self-contained ciphertext are mandatory; encrypted direct/serialized round trips cover live and destroyed profile keys, metadata-aware reads and denied provider outcomes. Six review corrections are verified; the pre-existing SDK folded-input integration limitation is documented and separately deferred. See spec-dw-129-identity-history-snapshot-protection.md and its validation packet. No production custody qualification is claimed.

### DW-163: D1 — Preserve coordinated Detail/Processing slots when recording an unresolved first projection delivery.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs:69-87,245-265
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: The detail handler creates only Processing on first unresolved delivery, then rejects all subsequent retries at the missing-slot guard (69–87, 245–265); verified medium, pre-existing at 890119f1.
status: done 2026-10-18
archived: 2026-10-21

legacy_implementation_blocker: 2026-10-17 Candidate two-slot audit initialization was withdrawn after SDK fault injection: a BeforeReceipt failure after newer erasure tombstones persist causes unguarded batch reconciliation to overwrite them. Prepared/committed manifest resumption also needs stable initialization identity. EventStore must supply safe recovery before this domain change; see `spec-dw-163-coordinated-unresolved-projection.md`. DW-163 remains open.

rejected_candidate_resolution: Adopted EventStore 3.125.1 through the shared Builds catalog. The handler validates SDK scope, recovers both pending keys, creates two empty create-only slots under a bounded event/dispatch-independent initialization identity with retained manifests, then records the failed audit with the existing optimistic policy. Initialization and normal delivery namespaces are separate; normal batches retain manifests so pending-key recovery preserves normal retry behavior. Genuine missing historical slots still require rebuild.
rejected_candidate_verification: Debug source builds passed with zero warnings/errors; focused handler tests 145/145, persisted recovery tests 24/24, full Projections assembly 335/335, full Parties assembly 1219/1219. The same 24 recovery tests passed against actual EventStore 3.125.1 NuGet packages; no-warning-override and CRLF-aware whitespace gates passed. Current receipts reflect the separately recorded concurrent /pushall graph without changing historical approvals. Evidence: `tests/dw-163-implementation-2026-10-18/README.md`. The 2026-10-17 blocker and reproducer remain legacy-mode evidence; no other deferred-work item is closed.

resolution: 2026-10-18 Review loop 1 limits retained manifests to two immutable empty initialization slots, created only for unresolved absent-slot deliveries. Both initialization keys recover before presence checks; normal non-retained retries and terminal namespace cleanup remain usable. Failed auditing uses the existing optimistic policy. Normal dispatch scopes, payloads and concurrency remain unchanged; real erasure leaves no orphaned payload manifest. Genuine missing historical slots still require rebuild.

verification: Debug/source builds pass with zero warnings/errors; focused handler tests 145/145, persisted source and published EventStore 3.125.1 package recovery tests 35/35 each, full Projections 335/335 and full service 1230/1230. Zero failures/errors/skips. Includes mixed-prefix redelivery, interrupted compensation, deterministic overlapping handlers, newer erasure tombstones, legacy first-create retries and real erasure after a normal pre-install interruption. Warning and whitespace gates pass. Revised source hashes and package graph: tests/dw-163-review-loop-1/validation.json. Earlier candidate evidence remains historical.

review_status: 2026-10-18 Revised implementation verified; independent parent review is pending before final closure.

final_review: Two review passes completed; all current-story findings are resolved or rejected with evidence. Final parent checks pass: 335 projection tests, 1232 service tests, 37 published-package recovery cases, zero build warnings/errors, warning and whitespace gates. The separate legacy normal postcommit compaction defect remains deferred because it predates DW-163.

### DW-166: D4 — Align Memories cleanup, compensation and health probing with the versioned server route without accepting a routing 404 as proof of erasure.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs:52; indexing compensation; Memories server routes
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: PartyMemoryCleanupService.cs:52 and indexing compensation use api/tenants, while the selected server maps /api/v1/tenants; success-on-404 can discard cleanup inventory; verified high, pre-existing.
status: done 2026-10-18
archived: 2026-10-21
progress: 2026-10-18 Implemented versioned cleanup, compensation and health probing with the shared Memories route builder. Only a structured MEMORY_UNIT_NOT_FOUND 404 counts as scoped absence; unqualified/malformed/case/tenant 404s retain mappings, fail compensation qualification and degrade the cleanup probe. Corrected the probe's invalid synthetic ID. Final Debug/source build has zero warnings/errors; focused tests pass 58/58. Independent review and follow-up are complete. Full assembly passes 1156/1157; closure remains open because the unchanged baseline dependency-receipt test expects a prior Builds gitlink. See spec-dw-166-versioned-memories-cleanup.md and tests/dw-166-versioned-memories-cleanup-2026-10-18/validation.json. DW-167/DW-168/DW-169 and terminal physical-erasure qualification remain separate.
resolution: 2026-10-18 Reconciled current dependency receipts with the already-committed root graph while preserving historical observations, approvals, source selections and assertion logic. Final Debug/source build has zero warnings/errors; platform prerequisite fitness tests pass 33/33 and full service assembly passes 1157/1157, including all 58 DW-166 regressions. Independent review and follow-up for the unchanged functional implementation are complete. See tests/dependency-receipts-2026-10-18/validation.json. The earlier blocked run remains preserved in its original packet; DW-167/DW-168/DW-169 and terminal physical-erasure qualification remain separate.

### DW-169: D7 — Use concurrency-aware removal when cleaning a party memory-unit mapping inventory.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs (ReplaceMappingsAsync)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: ReplaceMappingsAsync uses unconditional SaveStateAsync/DeleteStateAsync after a cleanup snapshot; a concurrent ETag-protected indexing addition can be overwritten; verified high, pre-existing.
status: done 2026-10-18
archived: 2026-10-21
activated_by_spec: `_bmad-output/implementation-artifacts/spec-dw-63-concurrent-memories-cleanup.md`
resolution: Exact cleaned-entry subtraction now retries against fresh ETags, preserves concurrent additions and changed unit/source/case identities, and retains empty domain inventories to prevent Redis ETag reuse. Physical clearing is restricted to one-use health probes. Cleanup requires another pass when mappings remain. Final Debug/source build has zero warnings/errors; focused tests pass 133/133 and full service assembly passes 1187/1187. Rollback passes 1158/1158 baseline tests, and the unsafe-delete mutation is detected. All eight review findings are patched and independently verified; no new deferred work. See spec-dw-63-concurrent-memories-cleanup.md and tests/dw-63-concurrent-memories-cleanup-2026-10-18/validation.json. Drain older writers before rollout; separate cancellation, workflow-reconciliation and persistence-migration work remains open.

### DW-171: D9 — Bound last-known-cache generation bookkeeping while retaining in-flight read invalidation.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: last-known cache EvictDetail and _keyGenerations bookkeeping
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Missing-detail reads call EvictDetail; _keyGenerations never expires or participates in the cache capacity limit; distinct valid missing IDs grow the singleton indefinitely; verified medium, pre-existing.
status: done 2026-10-17
archived: 2026-10-21
resolution: Replaced historical per-key generations with bounded active-read tokens and finally-based cleanup across detail, index and processing queries. Exact-key eviction rejects late reads without invalidating unrelated reads or reviving ended tokens; saturation fails closed and preserves degraded fallback. Missing-key churn retains zero identifiers. Final Debug/source build has zero warnings/errors; focused cache/query tests pass 93/93 and full Parties.Tests passes 1066/1066 with zero skips/errors/failures. Independent review and follow-up completed; no new deferred work. See spec-dw-171-bounded-cache-read-tracking.md and tests/dw-171-bounded-cache-read-tracking-2026-10-17/validation.json. Cross-instance cache invalidation remains the separate DW-172.

### DW-172: D10 — Prevent a second Parties instance from serving cached personal data after another instance completes erasure.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: erasure delegates; multi-instance degraded detail/index fallback cache
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Erasure delegates evict only the local singleton; the other host can serve pre-erasure detail/index from degraded fallback without authoritative erasure revalidation; verified high, pre-existing.
status: done 2026-10-18
archived: 2026-10-21

resolution: Canonical-read failures reject and evict personal detail and nonempty index caches across independent hosts; safe redacted/empty fallback and retained Article 30 records remain available. Real shared-erasure regressions pass 28/28, query/cache tests 98/98 and full host tests 1284/1284 without skips; Debug source build has zero warnings/errors. Independent review and corrections completed with no new deferred work. See spec-dw-172-cross-instance-cache-erasure.md and tests/dw-172-cross-instance-cache-erasure-2026-10-18/validation.json.

### DW-173: D11 — Fence rebuild search notifications against erasure after the canonical index snapshot is read.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: CompleteRebuildAsync and the search reconciliation adapter
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: CompleteRebuildAsync snapshots once before awaited removals and later publishes retained entries; the search adapter has no canonical reread/version fence, so completed cleanup can be followed by republishing erased data; verified high, pre-existing.
status: done 2026-10-19
archived: 2026-10-21
resolution: Canonical search reconciliation now rereads each target before and after awaited indexing/removal, compensates late indexing after erasure, and retries changed targets, live re-adds, malformed/unavailable state and failed compensation. Fresh live JsonIgnore contact/identifier collections survive only while checkpoint, provenance and persisted fields match. Debug/source builds have zero warnings/errors; 27 focused regressions and both complete affected assemblies pass (1311 host + 379 projection, no failures/errors/skips/not-run). Eight independent review findings are patched. The awaited indexing/mapping/cleanup guarantee does not certify terminal Memories workflow completion; DW-168/DW-170 remain open. See spec-dw-173-erasure-search-notification-fence.md and tests/dw-173-erasure-search-fence-2026-10-19/validation.json.

### DW-193: Fence live and rebuild search notifications against concurrent erasure; test both interleavings and reconcile deferred-work DW-80 with D11.
origin: retro action item epic-8-retro-erasure-search-fence
location: n/a
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
reason: retrospective action item from sprint-status.yaml (epic 8; owner Amelia (Developer) + Murat (Test Architect); sprint-status status open)
status: done 2026-10-19
archived: 2026-10-21
resolution: Canonical search reconciliation now rereads each target before and after awaited indexing/removal, compensates late indexing after erasure, and retries changed targets, live re-adds, malformed/unavailable state and failed compensation. Fresh live JsonIgnore contact/identifier collections survive only while checkpoint, provenance and persisted fields match. Debug/source builds have zero warnings/errors; 27 focused regressions and both complete affected assemblies pass (1311 host + 379 projection, no failures/errors/skips/not-run). Eight independent review findings are patched. The awaited indexing/mapping/cleanup guarantee does not certify terminal Memories workflow completion; DW-168/DW-170 remain open. See spec-dw-173-erasure-search-notification-fence.md and tests/dw-173-erasure-search-fence-2026-10-19/validation.json.

### DW-194: Preserve the typed destroyed-key outcome when the crypto-pending marker write fails; add focused fault-injection coverage.
origin: retro action item epic-8-retro-destroyed-key-marker-fault
location: n/a
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
reason: retrospective action item from sprint-status.yaml (epic 8; owner Amelia (Developer) + Murat (Test Architect); sprint-status status open)
status: done 2026-10-18
archived: 2026-10-21
resolution: Current-key protection preserves exact destroyed-key failures without retrying the crypto-pending marker, retains type-only diagnostics and propagates caller cancellation. Final Debug/source build has zero warnings/errors; focused faults pass 20/20 and full Security.Tests passes 238/238 without failures/errors/skips/not-run. All six independent review findings are corrected and follow-up verified; no new deferred work. See spec-dw-194-destroyed-key-marker-fault.md and tests/dw-194-destroyed-key-marker-fault-2026-10-18/validation.json.

### DW-195: Align named-enum command binding between Parties clients and EventStore

origin: DW-122 baseline reproduction
source_spec: `_bmad-output/implementation-artifacts/spec-dw-122-production-validation.md`
severity: medium
status: done 2026-10-16
archived: 2026-10-21
reason: A production-registered RecordConsent with lawfulBasis serialized as "Consent" passes Parties validation and throws JsonException at EventStoreAggregate command deserialization; its shared EventStorePayloadSerialization.Options has no string-enum converter. HttpAdminPortalGdprClient writes commands with PartiesJsonOptions.Default, which emits enum names. Reproduced before the DW-122 processor change in PartiesValidatorRegistrationTests.ProcessAsync_ProductionConsentValidators_AllowValidPayloads(revoke:false). Existing numeric-enum processing succeeds. This pre-existing client/platform compatibility defect requires an independently scoped reader/writer fix and consumer proof; DW-122 preserves both serializers. See tests/dw-122-production-validation-2026-10-16/README.md.
resolution: Declared the existing named-enum wire policy on all six Parties command properties using BCL converters. EventStore binding accepts names and historical numbers, including nested/nullable cases; canonical client output and event/snapshot serialization remain compatible. No shared serializer, platform source, dependency or gitlink changes. Final full Contracts.Tests 198/198, Client.Tests 174/174, Parties.Tests 1010/1010 (1382 total) pass without skips/errors/failures; expanded production/HTTP focus 93/93. Debug source builds have zero warnings/errors. Five independent review findings and the follow-up evidence note are resolved. See spec-dw-195-command-enum-compatibility.md and tests/dw-195-command-enum-compatibility-2026-10-16/validation.json. Existing full-topology startup failures remain separately recorded.

### DW-1: Follow-up review still recommended for 8-2-identifier-correctness-and-zero-risk-hygiene after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-072046-c4fb; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: done 2026-10-22
archived: 2026-10-25
resolution: Independent current-code follow-up review found no actionable in-scope defect; focused compatibility and prerequisite checks pass. Existing owner, transport, topology and migration gates retain their dispositions.
verification: `spec-five-review-runtime-convergence-deferred-items.md`; `tests/five-review-runtime-convergence-deferred-items-2026-10-22/review-followups.json` and `validation.json`.

### DW-2: Follow-up review still recommended for 8-3-platform-api-prerequisites after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-8-3-platform-api-prerequisites.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-072046-c4fb; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: done 2026-10-22
archived: 2026-10-25
resolution: Independent current-code follow-up review found no actionable in-scope defect; focused compatibility and prerequisite checks pass. Existing owner, transport, topology and migration gates retain their dispositions.
verification: `spec-five-review-runtime-convergence-deferred-items.md`; `tests/five-review-runtime-convergence-deferred-items-2026-10-22/review-followups.json` and `validation.json`.

### DW-13: Harden EventStore AppHost publish and credential scanning

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
location: EventStore owner AppHost publish output
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
reason: This was split because owner-AppHost adoption and publish-output validation are independently shippable after the reusable JWT composition API exists.
status: done 2026-10-22
archived: 2026-10-25
decision: 2026-09-06 Authorize owner adoption — Adopt the helper in the owner AppHost and inspect publish output, failing on secret or poison values.
resolution: The current owner AppHost already adopts the shared JWT helpers. The new verifier invokes its actual SDK manifest publisher privately for reciprocal sample/admin password and client-credentials variants, with synthetic poison inputs. It checks external authority/issuer, audiences, RS256, HTTPS metadata, workload identity and secret parameter references; the final ten publisher/mutation/failure controls reject credentials and development endpoints and verify private-output cleanup. Raw manifests are removed on success and failure. This closes local owner-manifest qualification, not a target deployment.
verification: `spec-five-sdk-runtime-deferred-items.md`; `tests/five-sdk-runtime-deferred-items-2026-10-22/final-verification.json`. Release/package qualification is separate from mapped Debug and deployment qualification.


### DW-14: Validate G8-A delivery receipt integrity

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md"), 2026-09-06
location: G8-A owner-delivery receipt and Parties fitness tests
source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
reason: The review confirmed that current Parties fitness tests parse only the marked matrix table and do not bind the supplemental receipt SHA, claimed four-file inventory, or focused EventStore test lane to the referenced Git objects.
status: done 2026-10-21
archived: 2026-10-25
resolution: Historical G8-A receipt is bound to the producer parent, full delivery/integration identities and ancestry, original consumer gitlink, complete four-file Git inventory and exact 26-case JWT lane source. Twenty-one malformed-claim regressions reject missing, altered, abbreviated, duplicated and unrelated evidence. This is immutable provenance validation, not a new producer runtime or adoption receipt.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-16: Reconcile BMAD branching guidance with default-main policy

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md"), 2026-09-06
location: persistent BMAD project context and Hexalith Git guidance
source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
reason: The pre-existing project context still requires a typed branch and PR, while the authoritative Hexalith Git instructions say to work on `main` by default and branch only when genuinely required.
status: done 2026-10-21
archived: 2026-10-25
resolution: Default-main guidance now matches the authoritative Hexalith Git policy; typed branches are conditional on task need.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

### DW-17: Pin the Node and npm runtime in release workflows

origin: migrated from legacy ledger (flat source_spec "/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md"), 2026-09-06
location: .github/workflows CI and release Node setup
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
reason: CI and release workflows use floating `lts/*`; changing this is pre-existing policy and the approved spec explicitly requires approval for Node engine policy changes.
status: done 2026-10-22
archived: 2026-10-25
decision: 2026-09-06 Pin Node 24 — Pin the tested Node 24 major and a repository-controlled npm version in CI and release workflows.
resolution: Root and browser projects enforce exact Node 24.22.1 and bundled npm 11.19.1 with engine-strict; browser, commitlint and immutable release preparation callers select the distribution before installing. Checksum-verified locked installs and wrong-Node/wrong-npm no-mutation rejection checks pass; dependency resolutions are unchanged.
verification: `tests/five-review-runtime-convergence-deferred-items-2026-10-22/validation.json`; spec `spec-five-review-runtime-convergence-deferred-items.md`.

### DW-27: Report invalid PageSize separately from InvalidCursor

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)"), 2026-09-06
location: Paging.PageSize validation
reason: Out-of-range `PageSize` under `Paging` rejected as `InvalidCursor` even with no cursor — debugging misdirection only.
status: done 2026-10-22
archived: 2026-10-25
decision: 2026-09-06 Add invalid-page sentinel — Add and map an InvalidPage failure reason, distinguish cursor failures from paging validation, and cover EventStore and Parties compatibility.
progress: 2026-10-17 Shared EventStore source prerequisite is prepared: additive InvalidPage sentinel, optional init-only exception discriminator, safe HTTP 400/query_invalid_page mapping, sanitized page logs and legacy cursor fallback. Contracts/Server Debug source builds have zero warnings/errors; focused suites pass 112/112 and 116/116.
historical_dependency_2026_10_17: Publish a compatible EventStore Contracts/Server/gateway SDK before activating Parties. Fresh public NuGet inspection still selects 3.124.0 and confirms the new APIs are absent; no package version was changed. The tested Parties producer and handler/gateway regressions are retained as the unapplied patch tests/dw-27-invalid-page-failure-2026-10-17/parties-consumer-adoption.patch.
historical_verification_2026_10_17: Candidate source build passes with zero warnings/errors and 214/214 focused tests. Its earlier pre-review full assembly has 1170/1171 passing, with immutable consumption proof blocked by the required dirty EventStore checkout. Restored tracked Parties pinned-package build passes with zero warnings/errors and 146/146 focused tests. Full EventStore attempts were stopped after packaging/Dapr failures outside the changed test classes (not reproduced against baseline); full topology and assembly qualification are not claimed. Exact commands, diagnostics and rollout gate are in tests/dw-27-invalid-page-failure-2026-10-17/validation.json. Keep DW-27 open until real package adoption and all required gates pass.
resolution: The selected published EventStore 3.126.1 SDK supplies InvalidPage and its safe gateway mapping. Parties now rejects envelope page sizes outside 1–100 before codec, read-model or search calls. The real public gateway router distinguishes HTTP 400/query_invalid_page from cursor failures, including public-schema-valid sizes 101/200; cursor, continuation and legacy controls remain covered. Full Parties.Tests passes 2534/2534 with zero skips. Earlier prerequisite observations above are historical and superseded by this package adoption.
verification: `spec-five-sdk-runtime-deferred-items.md`; `tests/five-sdk-runtime-deferred-items-2026-10-22/final-verification.json`. Release/package qualification is separate from mapped Debug and deployment qualification.


### DW-30: Add authenticated projected-query end-to-end proof

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
location: host AddEventStoreDomainService registration and EventStoreGatewayE2ETests
reason: Host `AddEventStoreDomainService(... PartyDetailProjectionHandler.Assembly)` remains source-text-only — closing properly needs reinstating the retired tenant seeder for authenticated query e2e. Also recorded in "Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)": Host wiring (`builder.AddEventStoreDomainService(typeof(PartyAggregate).Assembly, typeof(PartyDetailProjectionHandler).Assembly)`) is verified only as literal source text by `ArchitecturalFitnessTests`/`PlatformApiPrerequisitesTests`/ `RetiredLeafProjectFitnessTests`; no test queries a projected read model after an authenticated end-to-end command. Closing this needs `EventStoreGatewayE2ETests`, but its `PartiesAspireTopologyFixture.RequireSeededTenants()` unconditionally throws since Story 12.2 retired `TenantIntegrationTestSeeder` — reinstating that seeder is real work out of scope for a review-patch pass.
status: done 2026-10-22
archived: 2026-10-25
resolution: The seeded authenticated gateway create now queries canonical projected Party detail and checks party/tenant identity, payload and freshness. The complete original-identity runtime case passed against real sidecars and private Redis.
verification: `spec-five-runtime-isolation-and-form-authorization-deferred-items.md`; `tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json`. Release/package live acceptance passed with zero skips; mapped Debug test-build qualification limits are recorded separately.
decision: 2026-09-06 Minimal test-only seeder — Add a narrowly scoped fixture seeder through the supported internal tenant-event callback, then submit an authenticated command and query its projection.

### DW-31: Add runtime DAPR ACL enforcement proof

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
location: DAPR ACL allow-list
reason: ACL allow-list has no runtime Dapr enforcement check beyond YAML fitness — same topology e2e class as the assembly-scan defer.
status: done 2026-10-22
archived: 2026-10-25
resolution: The original production ACL profile verifies all eight sidecars and allowed Parties-to-Tenants/admin controls, then requires HTTP 403, ERR_DIRECT_INVOKE, PermissionDenied and Dapr policy-denial text for six forbidden caller/verb/path combinations. Authoritative ACL bytes remain unchanged.
verification: `spec-five-runtime-isolation-and-form-authorization-deferred-items.md`; `tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json`. Release/package live acceptance passed with zero skips; mapped Debug test-build qualification limits are recorded separately.

### DW-32: Clean up query shim and configuration naming

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)"), 2026-09-06
location: Party query shims, EventStore:Projections configuration, and build documentation
reason: Minor/cosmetic: query shim classes keep "Actor" names; `EventStore:Projections` config-key reuse; undocumented `Dapr.Actors.AspNetCore` / MSBuild property rename — intentional temporary trade-offs from the first Group 4 pass.
status: done 2026-10-21
archived: 2026-10-25
resolution: Added canonical PartyDetailQueryTypes/PartyIndexQueryTypes with obsolete public aliases and literal wire-compatibility assertions; layer Parties:Projections over legacy EventStore:Projections before binding, preserving per-property fallback and validation. Build guidance documents actor callbacks and the Commons HTTP source selector.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.
decision: 2026-09-06 Add compatible SDK names — Introduce canonical SDK names and a Parties-specific configuration section while retaining obsolete aliases and fallback binding.

### DW-33: Clean up Group 5 DI, health, naming, and status-key polish

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
location: Group 5 DI, health, query naming, and MessageId status handling
reason: Prior Group 5 cosmetic defer remains open (stringly DI absence checks; health "all components" naming; partial Ada→Synthetic rename; undocumented MessageId status-key change).
status: done 2026-10-21
archived: 2026-10-25
resolution: Typed SDK DI checks replace string exclusions; existing architectural guards cover deleted local interfaces. Health/readiness test names describe registered dependencies. Privacy sentinels have explicit leak-detection scope, and a distinct MessageId/CorrelationId regression verifies both persisted status entries and correlation metadata.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

### DW-34: Strengthen cursor, payload, and hosted-service test fidelity

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)"), 2026-09-06
location: query and host test fixtures
reason: `TestCursorCodec` private double instead of production DI codec; collapsed index invalid-payload theory; six indistinguishable `<factory-registered>` hosted-service exclusions — intentional test-isolation / factory-registration limits from the first Group 5 pass.
status: done 2026-10-21
archived: 2026-10-25
resolution: Replaced the default private cursor double with the production DI codec; preserve legacy bounded output and compact oversized Unicode scopes. Raw/compact binding and cross-handler tests pass. Pin each excluded hosted-service factory by its sorted concrete service types resolved from IL metadata. The already-expanded five-case invalid-payload theory is verified.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

### DW-40: Pin the reusable commitlint workflow revision

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .github/workflows/commitlint.yml
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
reason: Incidental review found `.github/workflows/commitlint.yml` consumes `Hexalith/Hexalith.Builds/.github/workflows/commitlint.yml@main`, allowing unrelated upstream changes to alter validation without a reviewed Parties change.
status: done 2026-10-21
archived: 2026-10-25
resolution: Pinned the reusable commitlint workflow to the existing immutable CI validation revision; required CI assertion verifies the caller pin and pull-request-title forwarding.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-41: Verify all release package and container artifacts

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .github/workflows/release.yml
source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
reason: Incidental review found `.github/workflows/release.yml` treats a non-draft GitHub Release at the dispatched commit as sufficient proof, without verifying the complete NuGet and container artifact set.
status: done 2026-10-22
archived: 2026-10-25
resolution: The post-release job now requires all nine declared NuGet archives and all three two-platform container images after exact-tag proof. It checks out exact dispatched source and immutable Builds, enforces bounded registry reads/retries, and always uploads byte-derived diagnostics and receipts.
verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.

### DW-42: Pin Story 8.7 G5 validation evidence to an exact commit

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review of the 2026-08-04 SCP recovery found the G5 row's `git ls-tree HEAD references/Hexalith.EventStore` / `references/Hexalith.Builds` commands resolve against whatever the working tree currently points to, unlike the sibling projection/query SDK and DataProtection rows in the same matrix, which pin to an exact Parties commit (`03ab938c637aa15f7a0af402afc8664dfc54d1a4`) for reproducibility. This pattern pre-dates the 2026-08-04 identity refresh; the refresh preserved rather than introduced it.
status: done 2026-10-21
archived: 2026-10-25
resolution: The G5 validation cell records exact historical Parties, EventStore and Builds identities with executable ls-tree/tag commands. Historical identity proof is explicitly separate from current parity and adoption approval.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

### DW-43: Restore dropped G8 and cross-story guidance in Epic 8 context

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: _bmad-output/implementation-artifacts/epic-8-context.md
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review found the working-tree regeneration of `epic-8-context.md` (predating this SCP recovery; only its line endings were normalized here) silently dropped the G8 local-run/publish JWT, audience-relationship, HTTPS-metadata, and secret-free-manifest proof requirements, and the sentence tying `AddEventStoreDataProtection`/`DaprXmlRepository`/cursor-codec consumption to the DataProtection prerequisite identity. It also compressed the explicit list of what stays externally owned for Stories 8.12/8.13 (production manifests, DAPR components, ingress, secrets, scans, signatures, promotion gates) into one generic sentence. A future Story 8.8/8.9/8.10 session loading only the cached epic context would miss this guidance.
status: done 2026-10-21
archived: 2026-10-25
resolution: Restored G8 local-run/publish authentication and topology proof, DataProtection prerequisite identity binding, and explicit external deployment ownership to the actual implementation-artifacts Epic 8 context. Existing gates and rollback requirements remain.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

### DW-45: Document and attribute the sprint-plan --fresh rebuild fix

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)"), 2026-09-06
location: .agents/skills/bmad-sprint-planning/scripts/sprint_plan.py
source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
reason: Blind-hunter review found this fix and its new test assertions are a distinct bug from the previously-documented STORY_RANK/`_slug()` regeneration incident, but no file in the current diff explains or attributes it, leaving a future reader unable to tell why `sprint_plan.py` changed.
status: done 2026-10-21
archived: 2026-10-25
resolution: Attributed the original fresh-generation timestamp fix and its later upstream regression; restored the fresh-only creation-date reset and YAML/JSON timestamp assertions while preserving incremental timestamps, action items and explicit overrides.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

### DW-51: Resolve projection-handler performance and validation-coupling debt

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: PartyDetailSdkProjectionHandler and PartyIndexSdkProjectionHandler
reason: Minor/cosmetic, `PartyDetailSdkProjectionHandler`/`PartyIndexSdkProjectionHandler` family: sequential (not parallel) `GetAsync` calls doubling state-store round-trip latency on the busiest projection path; duplicated `StoreName` null-check across classes; `PartyErased.LastModifiedAt` immediately overwritten by `NormalizeEventTimestamps` (harmless while both timestamps match, would silently diverge otherwise); `PartyIndexSdkProjectionHandler.Validate` reusing `PartySdkReadModelAddresses.Detail(...)` purely for its validation side effect, coupling Index validation to Detail's address-shape rules.
status: done 2026-10-21
archived: 2026-10-25
resolution: Parallelized paired initial live reads and initialization reloads; shared store-name validation and direct identity-segment validation remove handler duplication and Index-to-Detail address coupling. Advancing end-state, both completion orders, either-slot faults and actual token cancellation are covered. Existing erased timestamp regression remains green.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

### DW-52: Clean up rollback-shim naming, configuration, and test debt

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: rollback query shims, configuration, DI tests, and health tests
reason: Minor/cosmetic, rollback-shim naming and test quality: `PartyDetailProjectionQueryActor` / `PartyIndexProjectionQueryActor` keep the "Actor" name with zero actor behavior (intentional temporary rollback shims); `PartySdkReadModelOptions.ConfigurationSection` reuses the retired `Hexalith.EventStore.Server.Configuration.ProjectionOptions`'s `"EventStore:Projections"` config key; the new `Dapr.Actors.AspNetCore` package reference and `$(HexalithCommonsHttpFromSource)` MSBuild property rename are undocumented but verified correct; the DI test `AddParties_UsesSdkReadModelsAndCursorCodecWithoutLocalProjectionMechanics` asserts absence via a brittle `descriptor.ServiceType.FullName` string match rather than a type reference; `HealthEndpoint_AllComponentsHealthy_Returns200WithoutRetiredProjectionActorCheckAsync` keeps an "all components healthy" framing that now excludes SDK read models from what "all" verifies; the PII seed rename (`"Ada"/"Lovelace"` → `"SyntheticPrivateFirstName8472"/"SyntheticPrivateLastName6391"`) landed in only 2 of dozens of usages across `EventStoreGatewayRoutingTests.cs`, with 7 other test files still using `"Ada"/"Lovelace"`; `DirectPartiesCommandRouter`'s test double now keys its completion write on `command.MessageId` instead of `command.CorrelationId`, correctly mirroring production `SubmitCommandHandler.cs` behavior but undocumented in the diff.
status: done 2026-10-21
archived: 2026-10-25
resolution: The canonical query constants, obsolete aliases, Parties:Projections fallback and build-selector documentation already delivered by DW-32 are retained. Remaining DI, health, privacy-fixture and status-key cleanup is complete under DW-33, with runtime behavior and ordinary fixture data preserved.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

### DW-57: Move PartyEventTypeResolver out of the retired Actors folder

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)"), 2026-09-06
location: Projections/Actors/PartyEventTypeResolver
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: The resolver remains under Actors/ after projection actors were deleted, obscuring ownership.
status: done 2026-10-21
archived: 2026-10-25
resolution: Moved PartyEventTypeResolver and its tests to Serialization, extracted ResolveOutcome into its own file, updated all imports/reflection fixtures and corrected ownership documentation; allowlisted resolution and ambiguity tests pass.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

### DW-61: Update the Story 7.4 projection compatibility E2E spec

origin: migrated from legacy ledger ("Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)"), 2026-09-06
location: tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts
reason: This came from the DI and query-host sub-chunk comparing PartiesServiceCollectionExtensions.cs with 2c4a7af. `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts` still expects deleted projection-adapter registrations and old `ProjectionPlatformAdapterTests` method names — deferred, pre-existing e2e drift outside this DI chunk.
status: done 2026-10-21
archived: 2026-10-25
resolution: Executed the existing SDK-oriented compatibility spec, corrected its two stale query test names, and pinned active Detail/Index outage rejection plus degraded processing behavior; all five source-contract cases pass.
verification: spec-five-projection-migration-deferred-items.md; tests/five-projection-migration-deferred-items-2026-10-21/validation.json. Focused checks pass; unchanged Tenants NativeSourceProof blocks the broad Debug solution build, and full runtime/source qualification is not claimed.

### DW-81: Move the Memories mapping ledger behind EventStore persistence

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryUnitMappingStore
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryUnitMappingStore` persists operational state directly through `DaprClient`, outside the EventStore read-model and write-policy abstractions required for domain-module persistence.
status: done 2026-10-23
archived: 2026-10-25
resolution: Replaced direct Dapr mapping persistence with SDK conditional read/write/erase APIs, preserving hashed keys, legacy list/CaseId compatibility, exact subtraction, empty-list ETags and reserved probe deletion. Both real SDK/Dapr compatibility and two-client conflict tests pass.
verification: `spec-five-memories-recovery-deferred-items.md`; `tests/five-memories-recovery-deferred-items-2026-10-23/validation.json` and `matrix-audit.json`. Full Debug and Release affected assemblies pass after the separate upstream qualification fix; live erasure qualification remains separately unclaimed.

### DW-83: Persist recovery when mapping and compensating deletion both fail

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryIndexingService
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryIndexingService` records a double failure only in logs, so later erasure has no durable way to discover the orphaned Memories unit.
status: done 2026-10-23
archived: 2026-10-25
resolution: Content-free durable intents precede remote submission and preserve acknowledged workflow/canonical provenance through mapping failures, lost acknowledgements, cancellation and recreation. Recovery retains the producer rather than deleting it and later trusting a stale cached ID.
verification: `spec-five-memories-recovery-deferred-items.md`; `tests/five-memories-recovery-deferred-items-2026-10-23/validation.json` and `matrix-audit.json`. Full Debug and Release affected assemblies pass after the separate upstream qualification fix; live erasure qualification remains separately unclaimed.

### DW-84: Schedule retry or backfill when Memories indexing lacks a CaseId

origin: migrated from legacy ledger ("Deferred from: bmad-build Story 8.6 review (2026-08-16)"), 2026-09-06
location: PartyMemoryIndexEntrySearchIndexer.NotifyIndexedAsync
source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
reason: `PartyMemoryIndexEntrySearchIndexer.NotifyIndexedAsync` returns success when CaseId is absent, so fixing configuration alone does not cause the skipped party to be indexed.
status: done 2026-10-22
archived: 2026-10-25
resolution: Enabled indexing without CaseId now returns false, preserving projection reconciliation retry. Handler redelivery after configuration repair reads serialized persisted canonical state, indexes the latest intervening target and writes its stored mapping; an erasure control remains authoritative. Disabled/removal/cancellation behavior and bounded warning suppression remain covered. These are service integration checks, not durable Memories workflow runtime proof.
verification: `spec-five-sdk-runtime-deferred-items.md`; `tests/five-sdk-runtime-deferred-items-2026-10-22/final-verification.json`. Release/package qualification is separate from mapped Debug and deployment qualification.


### DW-90: Consolidate timeout-safe git and process test helpers

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: EpicEightClosureFitnessTests, PlatformApiPrerequisitesTests, and DocumentationFitnessTests
reason: Duplicated, timeout-free git/process helpers. `RunGit`/`TryRunGit` are defined in both `EpicEightClosureFitnessTests` and `PlatformApiPrerequisitesTests`, and `Read(root, relativePath)` a third time in `DocumentationFitnessTests`. `RunGit` drains stdout fully before stderr with no timeout; not a realistic deadlock at these output sizes, but the pattern should be consolidated.
status: done 2026-10-21
archived: 2026-10-25
resolution: Consolidated repository Git/process calls through the existing shared runner, preserving literal arguments, concurrent capture, a shared execution deadline and bounded cleanup. Reused RepositoryRoot.Read. Focused process/fitness checks, complete Contracts and Client suites pass; the broader service suite retains the independently reproduced baseline Builds-receipt mismatch. Independent review and follow-up completed. See spec-dw-90-bounded-test-processes.md and tests/dw-90-bounded-test-processes-2026-10-21/validation.json.

### DW-92: Read the EventStore version from the Builds catalog

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-08-19)"), 2026-09-06
location: PlatformApiPrerequisitesTests, docs/ci.md, and docs/architecture.md
reason: EventStore's exact version is hardcoded in five or more places (`PlatformApiPrerequisitesTests`, `docs/ci.md`, `docs/architecture.md` §3) rather than read from the Builds catalog's `HexalithEventStoreVersion`. This is the drift the "CI identity regression — one stale live assertion expecting EventStore 3.90.0" receipt already recorded once. (Updated 2026-09-06: the cited value was `3.95.0` when this note was written 2026-08-19; the 2026-09-05 catalog adopt advanced all cited places to `3.102.0` together, so the specific hardcoded value drifts each time the catalog moves — the underlying "not read from the catalog" gap remains.)
status: done 2026-10-21
archived: 2026-10-25
note: 2026-10-05 (Story 8.10 review loop 3, Blind 11) — the effective EventStore version is now the Parties pre-import pin `HexalithEventStoreVersion=3.113.0` in `Directory.Packages.props`; the Builds catalog default at `360a2b9c` is `3.112.0`, so the governing value is the effective MSBuild property, not the catalog default. Consolidating the test-side version lookups into one effective-value reader is in this entry's scope: `CommonsHttpRestoreRoutingTests`, `PartiesContainerPublishWorkflowTests.ReadEffectiveEventStoreVersion`, the `DocumentationFitnessTests` catalog evaluator, the `AccessibilityStyleGuardTests` FrontComposer lookup, and `PlatformApiPrerequisitesTests.EventStorePackageVersion`. They agree today only because EventStore is the sole root pin; a root pin of another package would make them diverge.
resolution: One bounded actual-MSBuild version reader now supplies live prerequisite, documentation, publication, restore-routing and package-test lookups. Root pins, conditional/import order, Debug/Release selection and malformed values are covered. Historical package/source receipts retain their original versions and full IDs; live documentation names the governing properties and reproducible query.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

### DW-103: Complete Fluent UI V5 and Fluent 2 styling and accordion conformance across all Parties UI RCLs

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: all Parties UI RCLs
source_spec: `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`
reason: This independently reviewable design-system conformance slice was split from Story 8.9 after its hardened draft exceeded the 1,600-token workflow limit.
status: done 2026-10-23
archived: 2026-10-25
resolution: All 52 UI component Razor/CSS files were audited. Admin and Consumer sibling sections use Fluent V5 accordions with primary content expanded; typography, status and focus styles use Fluent components and Fluent 2 tokens. Interactive content has token-based focus clearance. Standalone subpanels, form authority, exact-name erasure and Picker contracts are preserved. Full component suites, blocking axe, keyboard/forced-color/narrow browser controls and inspected zero-diff visual baseline pass in package mode; separate source Debug qualification is blocked at the selected Platform compile gate.
verification: `spec-five-recovery-and-ui-conformance-deferred-items.md`; `tests/five-recovery-and-ui-conformance-2026-10-23/validation.json` and `matrix-audit.json`.

### DW-105: Make the parties-ui release container stay healthy in Development so the shared OCI smoke can finish and GitHub Release assets attach.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: parties-ui release container and shared OCI smoke workflow
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: Release run 33980524472 published NuGet 1.1.1 and passed parties/parties-mcp smoke, then failed parties-ui with image-start-failure; the GitHub Release for v1.1.1 has no nupkg assets because semantic-release never reached the GitHub plugin.
status: done 2026-10-22
archived: 2026-10-25
resolution: Preserved and regression-tested the ICU-capable alpine-extra UI base with non-invariant globalization. A fresh SDK Rebuild/PublishContainer archive passes local linux/amd64 Development startup through shared Builds smoke helpers, all three health routes and the RCL stylesheet (HTTP 200), and final running-state proof. Owned container/image cleanup passes. This closes current startup qualification only; it does not repair the historical v1.1.1 release, certify arm64 or replace required release smoke.
verification: `tests/five-runtime-release-deferred-items-2026-10-22/ui-container-qualification.json` and `validation.json`; spec `spec-five-runtime-release-deferred-items.md`.

### DW-106: Execute the Release verify-source bash with a fake gh/GITHUB_OUTPUT harness the way EventStore does.

origin: migrated from legacy ledger ("Story 8.10 accepted Epic 8 closure deferrals — 2026-08-18"), 2026-09-06
location: .github/workflows/release.yml verify-source logic
source_spec: `_bmad-output/implementation-artifacts/spec-update-latest-hexalith-packages.md`
reason: Parties currently asserts bypass mapping by YAML substring order; a later assignment after esac can invert ci.yml vs commitlint.yml without failing those tests. Also recorded in "Deferred from: code review of story-8-10 (2026-09-06)": Release workflow's bypass-validation→proof-source mapping (`false→ci.yml`/`true→commitlint.yml`) is verified only by substring-ordering in the YAML text, not by executing the bash — already self-disclosed above ("Execute the Release verify-source bash with a fake gh/GITHUB_OUTPUT harness..."); the review layer that raised this again confirmed no further action is needed beyond what that entry already tracks. [.github/workflows/release.yml:44-56, tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs:100-116]
status: done 2026-10-21
archived: 2026-10-25
resolution: The existing executable harness runs the tracked release source Bash with fake gh and GITHUB_OUTPUT. Exact-main, missing/malformed/wrong-source CI, API errors, successful Commitlint without CI, and retired bypass values are exercised. Mixed matching/unrelated runs succeed; split proof across invalid runs rejects. The retired bypass mapping is superseded by fixed full-CI proof.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-109: Cover ordinary main pushes with the root-gitlink RC sign-off gate, and pin Tenants/Memories identities.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate (2026-09-06)
location: .github/workflows/rc-gate.yml; tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: rc-gate.yml enforces `.gitlink-signoff.tsv` only on a release-candidate-labeled PR or a push to `rc/**`/`release/**`/a `v*` tag — never an ordinary push to `main`, which is this repo's actual workflow. PlatformApiPrerequisitesTests now pins all eight root identities, including Tenants and Memories, so that portion is complete. The RC signoff workflow still excludes ordinary main pushes; enabling owner-signoff enforcement there remains open.
status: done 2026-10-22
archived: 2026-10-25
resolution: Ordinary main pushes use a validated ancestor event.before and inspect every root pointer change throughout the complete push range. Missing bases, unsigned intermediate bumps, stale or placeholder signoffs, reset entries and malformed root declarations fail closed. Existing Tenants/Memories fitness pins and RC/tag/manual behavior are preserved.
verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.

### DW-111: Observe app-owned content focus and reduced-motion CSS at runtime, not as source greps.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css; tests/e2e/specs/parties-accessibility.spec.ts
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Frozen §4.6 already records I13 as not discharged because Playwright focuses `.fc-skip-link` rather than a content control. `AccessibilityStyleGuardTests` only `ShouldContain`s CSS tokens. Moving `class="parties-main-content"` onto `<FrontComposerShell>` would keep those greps green while isolation stamps nothing. A computed-style assertion on `parties-specimen-primary-action` belongs in `npm --prefix tests/e2e run test:a11y`, which CI does not run (DW-76).
decision: 2026-10-05 Accept as named closure deferral — Administrator / jpiquot accepted the missing runtime content-control focus, forced-colors, and reduced-motion proof as a named deferral owned by `8.9-frontcomposer-ui-consolidation` for Story 8.10 closure (8.3 I20 "I13 / DW-111 accepted closure deferral" row). I13 is not discharged and spine §7 keeps it Deferred; this entry stays open as the exit-proof carrier.
status: done 2026-10-21
archived: 2026-10-25
resolution: Required browser lane now measures keyboard-focused content controls in normal/forced colors and proves reduced-motion overrides against nonzero settings.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-113: Parse only the first complete JSON object from MSBuild `-getProperty/-getItem` stdout.

origin: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)
location: tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: `EvaluateProjectGraph` slices from the first `{` to end of string. Trailing restore/log text after the JSON object would throw `JsonException` instead of a graph assertion. Unverified whether current MSBuild emits trailing content on this invocation; settle by capturing a live `-getItem` payload. If true, severity is medium.
status: done 2026-10-21
archived: 2026-10-25
resolution: EvaluateProjectGraph consumes one complete JSON object with Utf8JsonReader/ParseValue. Nested, escaped-string, trailing-log and malformed/missing-payload regressions cover the behavior. Current live MSBuild emits no trailing logs; this is robustness evidence, not a claimed current reproduction.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-114: Retire resolved Epic 8 closure deferrals from the accepted-wait set.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs (ParseDeferrals)
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: `ParseDeferrals` reads packed fields from each reason but not the ledger entry's first-class status, so a resolved entry whose reason remains can still satisfy `ExpectedDeferrals`.
status: done 2026-10-21
archived: 2026-10-25
resolution: Closure parsing uses first-class status from each whole ledger entry; resolved/missing/unknown status cannot satisfy accepted waits. Legacy accepted/open entries and required-field diagnostics remain supported. Completing an expected deferral now fails the stale expectation until its dependent invariants are deliberately reconciled.
verification: `tests/five-closure-guidance-deferred-items-2026-10-21/validation.json`; spec `spec-five-closure-guidance-deferred-items.md`.

### DW-116: Prove a FrontComposer RCL static asset is served in source mode.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: The historical framework-only request did not prove selected FrontComposer RCL delivery. The source-mode RCL delivery assertion now passes with the owning restore prerequisite fixed.
status: done 2026-10-23
archived: 2026-10-25

implementation_2026_10_23: Actual RCL script request, JavaScript content type and exact selected-byte comparison pass in pinned-tool-mapped source mode with evaluated source root/mode metadata. Conditional canonical SDK assets-file defaults in FrontComposer Contracts/Schema resolve the outer multi-target restore prerequisite without bypassing source validation. Release/package evidence and full-topology launcher HXR020 remain separate. See tests/five-recovery-integration-2026-10-23/validation.json.

resolution_2026_10_23: Reviewed FrontComposer owner aee73e5841b4004803dafa851f060e143550716a is published and adopted in Parties a9f4edb5a8a769e36499063b5b88413af01a7bd0. The rebased mapped UI suite passes; package delivery and full-topology proof remain separate.
verification_2026_10_23: `tests/five-recovery-integration-2026-10-23/final-verification.json`.

### DW-117: Resolve the Fluent UI package root through NuGet and MSBuild configuration.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs (ResolveNuGetPackagesRoot)
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: The style guard accepts an empty `NUGET_PACKAGES` value and otherwise assumes the default user cache, ignoring `RestorePackagesPath` and NuGet `globalPackagesFolder`.
status: done 2026-10-21
archived: 2026-10-25
resolution: The stylesheet guard consumes NuGetPackageFolders and the catalog-selected FrontComposer version captured as evaluated build metadata. The maintained CI probe restores a fixture package, compiles the actual resolver and metadata, and verifies asset bytes for globalPackagesFolder, RestorePackagesPath, NUGET_PACKAGES, empty NUGET_PACKAGES and fallback folders; no runtime cache guessing remains.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-118: Make Playwright web-server ownership deterministic and test its environment matrix.

origin: migrated from legacy ledger ("Deferred from: code review of spec-8-10-final-readiness-documentation-and-retirement-gate.md (2026-09-06)"), 2026-09-07
location: tests/e2e/playwright.config.ts and tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-and-submodules.md`
reason: Local `ASPNETCORE_ENVIRONMENT=Test` runs may reuse an unrelated process at the expected URL, while current checks only inspect configuration text and do not execute the CI/local and Test/Development matrix.
status: done 2026-10-21
archived: 2026-10-25
resolution: Managed Playwright runs always use reuseExistingServer=false. Eight executable configuration tests cover CI/local and Test/Development, explicit external-server ownership, an occupied-URL refusal and managed startup/response/cleanup by the real Playwright runner. They also pass on the declared Node 24.0.0 runtime floor. The existing required CI job runs test:config.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-125: Align E2E Node declarations with the supported runtime floor

origin: Story 8.10 Round 6 ledger normalization (2026-10-04); preserved package-update deferral
location: tests/e2e/package.json
source_spec: `_bmad-output/implementation-artifacts/spec-update-all-packages-dotnet-10-0-401-and-submodules.md`
reason: Node >=24.0.0 is supported but @types/node selects major 26; choosing Node 24 declarations or raising the runtime floor remains a compatibility decision.
status: done 2026-10-21
archived: 2026-10-25
resolution: Selected and locked Node 24.19.2 declarations for the supported Node 24 runtime floor; Node 24.22.0 typecheck and all eight configuration cases pass.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-131: Validate identity fold wire representations

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Queries/PartyIdentitySourceFold.cs:39
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: Current and retained identity folds reject unsupported event metadata versions before releasing evidence. Existing JSON/protection checks remain strict; DW-150 is the duplicate report, counted as one task.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-150: Reject unsupported serialization formats and metadata versions before identity source folding.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentitySourceFold.cs:39
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 5: the fold deserializes every payload as JSON without checking its StreamReadEvent format/version. The owner authoritative reader validates continuity; StreamReadPageValidator only requires nonblank format and metadata version >=1, not Party representation support. Existing 37d87f5a2869b076c651a58714d60f64647848bc code, unchanged here. Exit proof: supported representations pass and otherwise contiguous unsupported-format/version streams are rejected.
status: done 2026-10-21
archived: 2026-10-25
resolution: Require version-1 event metadata as well as the supported JSON and protection representation on both identity paths. Current/retained malformed-version regressions and supported-history controls pass. This reconciles the duplicate DW-131 report.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-132: Require test evidence on release validation bypass

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: .github/workflows/release.yml:49,312
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: The committed caller, shared preparation and publication wrapper require exact-source ci.yml proof; successful Commitlint and every retired bypass fixture cannot substitute. The human policy authority is recorded in spec-align-parties-ci-cd-with-eventstore-tenants.md under Release-gate decision — 2026-10-07; this verifies that policy without changing it or publishing. DW-151 is the duplicate record of this same gap.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-151: Ensure the Commitlint-only release bypass cannot substitute for required test evidence.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: .github/workflows/release.yml:49,312
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 6: bypass-validation=true selects commitlint.yml exact-source proof while publication receives empty test-projects based on a CI-tested assumption. Protected operator approval supplies no test receipt. Existing workflow from 14d249fde316b0002aec84351d7a7cdf953d1d30, unchanged here. Exit proof: a release-policy decision and a test-backed path for the exact source; no release-policy change or publication is authorized by this entry.
status: done 2026-10-21
archived: 2026-10-25
resolution: Duplicate of DW-132. The bypass was already removed and full exact-source CI is fixed at every publication boundary. Executable legacy bypass/Commitlint rejection plus publication-wrapper cases pass under the existing documented release policy; no new policy or publication action is introduced.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-133: Exercise real Parties admission proof construction

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Authorization/PartyIdentityAuthority.cs:14-38; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:25
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: Production PartyIdentityAuthority and RSA-backed IdentityAdmissionProof tests bind every command/query scope field and reject absent, wrong-key, malformed, expired and future-issued proofs. Also closes duplicate DW-152.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-152: Test real Parties identity admission scope construction rather than replacing the whole authority.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Authorization/PartyIdentityAuthority.cs:14-38; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:25
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 8: repository C# searches find only substituted IPartyIdentityAuthority consumers; the real wrapper builds payload digest, logical ID, message ID, operation and tenant/target scope before invoking the owner verifier. Existing identity-feature code, unchanged here. Exit proof: real-wrapper signed evidence acceptance and changed payload/scope/operation/message/logical ID, expiry, and missing-proof rejection before protected state unwrap.
status: done 2026-10-21
archived: 2026-10-25
resolution: Duplicate of DW-133; real registered authority/proof scope tests now cover both command and query admission, including crypto/time/target rejection.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-134: Make visual regression and baseline updates executable

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:105; tests/e2e/package.json:11
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: The deterministic specimen has an asserted Chromium/Linux PNG, separate all-platform semantic JSON checks, loaded fonts and normalized copyright year. The updater replaces the asserted PNG. Ubuntu 24.04/Release qualification passes; deliberate layout mutation fails, updating changes the PNG, and a future-year control passes normalization.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-153: Provide an actual image regression baseline and an update command that updates the asserted artifact.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:105; tests/e2e/package.json:11
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 9: the browser test attaches a screenshot and checks byte length, then compares manually read JSON text/visibility fields. No image matcher or JSON updater consumes --update-snapshots. Existing f8fd74045d95d441ee5adb0809a254307d0a24a6 test, unchanged here. Exit proof: deterministic layout/style image regression is detected and test:visual:update regenerates its asserted baseline.
status: done 2026-10-21
archived: 2026-10-25
resolution: Duplicate of DW-134, completed by the same asserted PNG, executable updater and mutation proof. This is one work item, not an additional closure toward the five-item batch.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-135: Observe content forced-colors and reduced-motion behavior

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:85
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: Content controls now have direct forced-colors and reduced-motion computed-style proof; duplicate scope of DW-111/DW-154.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-154: Measure visible app-content focus and reduced-motion styles in the browser.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:85
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 10: media emulation is asserted, but the sole style value is a truthy outlineColor on a shell skip link; no content control outline style/width or animation/transition duration is checked. Existing browser test, unchanged here. Exit proof: focus a representative app-owned control and verify visible normal/forced-colors outline plus reduced-motion behavior. I13 remains open; this entry supplies no parity approval.
status: done 2026-10-21
archived: 2026-10-25
resolution: Actual app-content button/link outlines, widths, offsets and colors are asserted; nonzero motion settings must be suppressed by the production stylesheet.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-136: Audit the complete shell with axe

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/e2e/specs/parties-accessibility.spec.ts:25
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: The blocking axe scan covers the complete specimen document, including shell navigation and skip links.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-155: Include navigation and skip links in the blocking accessibility scan.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/e2e/specs/parties-accessibility.spec.ts:25
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Blind 11: axe includes only #fc-main-content, while requiredSelectors merely assert that navigation and skip links exist. Existing browser test, unchanged here. Exit proof: full-shell or dedicated omitted-region scans fail on navigation/skip-link accessibility regressions.
status: done 2026-10-21
archived: 2026-10-25
resolution: The shared full-document axe gate rejects deliberate inaccessible navigation and skip-link controls outside main content; duplicate scope of DW-136.
verification: `tests/five-accessibility-proof-deferred-items-2026-10-21/checks.json`; browser observations and rejection artifacts in the same directory; spec `spec-five-accessibility-proof-deferred-items.md`. Historical I13/Story 8.9 approval scope is unchanged.

### DW-137: Verify provisioning through the registered domain processor

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:157-159; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:22
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: Party-keyed production IAsyncDomainProcessor registration provisions from serialized payload without caller Authorization, emits ordered creation/marker events and exact enriched identity, and preserves original provenance/revision on a later-snapshot retry. Also closes duplicate DW-156.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-156: Add successful provisioning-envelope verification through PartyDomainProcessor with emitted events and enriched result assertions.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:157-159; tests/Hexalith.Parties.Tests/Domain/PartyIdentityAdmissionTests.cs:22
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Verification 1, pre-verified regression gap: aggregate tests supply hand-built Authorization and bypass the processor; processor tests cover only unsupported-policy rejection. Removing trusted Authorization injection can break registered command dispatch without those tests failing. Existing identity-feature paths from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: accepted provisioning envelope yields creation/marker events and matching result identity at the processor boundary.
status: done 2026-10-21
archived: 2026-10-25
resolution: Duplicate of DW-137; registered production dispatch and enriched result/retry assertions satisfy the original boundary proof.
verification: `tests/five-validation-boundary-deferred-items-2026-10-21/validation.json`; spec `spec-five-validation-boundary-deferred-items.md`.

### DW-138: Isolate wrong-purpose human custody rejection

origin: Story 8.10 baseline review after three-pin approval (2026-10-04)
location: tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs:67
severity: medium
status: done 2026-10-21
archived: 2026-10-25
resolution: Current human evidence now has a fixed-clock valid control plus independent wrong-purpose and expired-interval cases, each asserting bounded 503 failure. This also resolves duplicate DW-157.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-157: Separate expired-interval and wrong-purpose client rejection cases.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md"; original review: Story 8.10 baseline review after three-pin approval (2026-10-04)), 2026-10-13
location: tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs:67
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
reason: Verification 2, pre-verified broken-verification gap: the current case combines an expired interval with other-purpose, so removing the purpose check still rejects on expiry. Existing identity-feature test from 37d87f5a2869b076c651a58714d60f64647848bc, unchanged here. Exit proof: otherwise valid current human evidence with the wrong purpose is rejected independently, and expired evidence with the correct purpose is rejected independently.
status: done 2026-10-21
archived: 2026-10-25
resolution: Duplicate of DW-138; the same valid control and independently corrupted purpose/interval cases supply its exit proof. Counted once in the five-item batch.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-139: Wire FrontComposer reserved segments and module landing in package mode after a Shell release

origin: Story 8.10 final identity reconciliation (2026-10-05); follow-up of resolved DW-124
location: src/Hexalith.Parties.UI/Program.cs; src/Hexalith.Parties.UI/Components/Routes.razor; src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor; src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor.cs; src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj; references/Hexalith.Builds/Props/Directory.Packages.props
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: low
reason: Commit `47e2da3244fd7d6d14e39bc30513b23504e9bc27` cleared DW-124's package build failure by compiling the `FrontComposerRouteOptions.ReservedSegments` wiring (`admin`, `me`, `no-party-binding`) only under `HFC_ROUTE_OPTIONS`, which is defined only when `HexalithFrontComposerFromSource=true`. The package-mode graph (CI, bUnit, and the released `parties-ui` container) therefore registers no reserved segments, and packaged Shell `4.5.0` carries no `FcModuleLandingPage`, so the module-landing route surface stays source-only. Since 2026-10-05 (DW-141 resolution) the same gate also covers `PartiesOverview.razor`: its `FcPageTabs` `ModuleRoute="/parties"` and `DefaultTabId="overview"` parameters, which packaged Shell `4.5.0` lacks, are passed only under `HFC_ROUTE_OPTIONS` through the `ModuleRouteTabAttributes` `@attributes` dictionary in `PartiesOverview.razor.cs` (empty in package mode). Exit proof: a published `Hexalith.FrontComposer.Shell` release containing both types and the `FcPageTabs` `ModuleRoute`/`DefaultTabId` parameters, a Builds catalog bump selecting it, removal of the `HFC_ROUTE_OPTIONS` gate (in `Program.cs` and in `PartiesOverview.razor.cs`, with `PartiesOverview.razor` passing `ModuleRoute`/`DefaultTabId` directly), a green package-mode `Hexalith.Parties.UI` Release build and `PartiesOverviewTests`, a test pinning the reserved-segment set, and matching 8.3 G4 and identity rows. Rollback: keep the source-mode gate (the current state). Bound to the `8.9-frontcomposer-ui-consolidation` slot (G4 surface).
status: done 2026-10-22
archived: 2026-10-25
resolution: The already-selected published Shell 4.7.0 exposes reserved route options, module landings and route-backed tabs. Removed HFC_ROUTE_OPTIONS and its attributes dictionary; PartiesOverview passes ModuleRoute and DefaultTabId directly. Published-package Release build, actual host collision/route/role checks and default-tab fallback checks pass. Current routing proof is appended to the Story 8.3 matrix; historical approvals and broader G4 prerequisites remain unchanged.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

### DW-143: Give the AdminPortal E2E fixture routes a current shell scope

origin: Story 8.10 review of the closure-blocker resolution (2026-10-05), Verification 1 and other
location: src/Hexalith.Parties.UI/Program.cs (AdminPortal E2E fixture branch); src/Hexalith.Parties.UI/Services/PartiesAccessibilitySpecimenUserContextAccessor.cs; tests/e2e/specs/{admin-parties-list,consumer-portal-routes,party-picker}.spec.ts
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: With FrontComposer `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470`, `FrontComposerShell` renders `FcScopeBlocked` unless `IUserContextAccessor` yields a current tenant and user. The AdminPortal E2E fixture (Test environment, `AdminPortalE2E__Enabled`) replaces authentication, authorization and the API clients but keeps Quickstart's `NullUserContextAccessor`, and the 2026-10-04 repair supplies a synthetic scope only at the accessibility specimen route. The admin, consumer and picker Playwright specs therefore cannot observe their flows (pre-verified by code reading; not executed). Production (OIDC claims) and CI (no e2e job) are unaffected, and only `test:a11y` is a closure lane. Administrator / jpiquot chose to defer on 2026-10-05. Exit proof: the fixture branch supplies the fixture principal's tenant and user through `IUserContextAccessor`, a `PartiesUiHostCompositionTests` case resolves a non-null scope under the fixture composition, and a recorded green run of the non-a11y Playwright specs. Rollback: remove the fixture accessor registration.
decision: 2026-10-05 Defer — Administrator / jpiquot (spec 8.10 review of the closure-blocker resolution, Verification 1 and other): keep the AdminPortal E2E fixture's `NullUserContextAccessor` for Story 8.10; only `test:a11y` is a closure lane, and the fixture-scope repair and the non-a11y Playwright run wait for this entry's exit proof.
status: done 2026-10-21
archived: 2026-10-25
resolution: The existing Test-only shell scope is now qualified through actual host DI with seven cases covering admin, Consumer, anonymous, both fixture wrappers and route delegation. All three portal/picker specs execute successfully in the Release/package browser run. Production authentication remains unchanged.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-144: Require the stamped identity in every canonical closure receipt, not only Playwright

origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 4 and L3 Edge 16
location: tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs (DescribeReceiptGaps, DescribeAccessibilityStampGaps)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: The closure gate checks the selected FrontComposer SHA only in the Playwright accessibility evidence. The warning-policy, Release build, all-tests, package/consumer, and npm rows accept any exact `Pass`, so a row left over from a superseded identity would satisfy closure. That contradicts I16's rule that a receipt counts only when rerun at the stamped identity. Pre-existing: Parties `c782b68c` already had the Playwright-only check. Exit proof: each required row must carry a machine-checkable stamp of the final set (for example the EventStore source SHA or a named final-set token) and the Parties tree, with rejection cases. Rollback: keep the current Playwright-only stamp check.
status: done 2026-10-21
archived: 2026-10-25
resolution: Every required passing closure receipt requires exact row-local Parties snapshot and dependency-set digests, with negative and actual filesystem/Git regressions. Current selected FrontComposer is checked independently. Only canonical receipt payload and three explicit closure bookkeeping values are normalized; all other content remains bound. Historical passes are preserved; current full closure rows are Unvalidated, Story 8.10 is in-review and Epic 8 in-progress pending genuine full requalification.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

### DW-145: Extend the personal-data inventory to the identity-feature contracts

origin: Story 8.10 review loop 3 (2026-10-05), L3 Blind 5
location: tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs (s_inspectedTypes)
source_spec: `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
severity: medium
reason: `s_inspectedTypes` omits contracts that carry actor identities: `EstablishHumanActorBinding`, `RebindHumanActorBinding`, `RevokeHumanActorBinding`, `ResolveHumanActorBindingAt`, `HumanActorBindingEvidence`, and the nested `HumanActorBinding`, `HumanActorBindingTransition`, `AgentPartyProvisioningResult`, and `AgentPartyIdentity` records. Members of the `[PersonalData]`-marked `PartyState.AgentProvisioning` are neither marked nor classified. All of these were added by `37d87f5a2869b076c651a58714d60f64647848bc`, before this delta; DW-142 classified only the six properties that failed. This is an I8 decision for the Parties GDPR owners, related to DW-129. Exit proof: every identity-feature contract type is inspected, and each property is classified with its rationale. Rollback: revert the added inventory rows.
status: done 2026-10-22
archived: 2026-10-25
resolution: The inventory inspects identity commands, queries, events and nested binding, provisioning, response, admission and custody records. Every property has an explicit contextual classification and rationale, with completeness checks for identity roots and nested records. Opaque linkages remain sensitive; transient proofs/authorization, finite identity-history custody and profile-protected provisioning are distinguished. Production annotations remain unchanged; focused inventory passes 9/9 and the full Contracts suite passes 211/211.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

### DW-158: Reconcile the upstream Tenants runtime project's administrator verifier contracts with a published EventStore SDK.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: upstream Tenants dynamically launched runtime project; published EventStore.DomainService SDK
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The retained Tenants source fails its dynamically launched package-mode build with CS0246 for IDomainServiceAdministratorVerifier and DomainServiceAdministratorClaim; neither type exists in published DomainService 3.115.0. Root solution package-mode compilation succeeds separately.
status: done 2026-10-22
archived: 2026-10-25
resolution: The unchanged Tenants package-mode Release project restores EventStore DomainService 3.126.1, whose published assembly contains IDomainServiceAdministratorVerifier and DomainServiceAdministratorClaim. Its build succeeds with zero warnings/errors. The complete real eight-sidecar mTLS profile invokes Tenants readiness through Parties; authenticated gateway/persistence controls pass in the final 28/28, zero-skip integration batch, including seven real host cases. Exact restored package hashes, verifier assembly identity and source revisions are retained.
verification: `spec-five-sdk-runtime-deferred-items.md`; `tests/five-sdk-runtime-deferred-items-2026-10-22/final-verification.json`. Release/package qualification is separate from mapped Debug and deployment qualification.


### DW-159: Include external dynamically launched runtime project graphs in orchestration dependency verification.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs (external runtime project inventory)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: PlatformApiPrerequisitesTests inventories only owned src, samples, and tests projects, so it cannot detect the pre-existing upstream Tenants source/package mismatch before topology startup.
status: done 2026-10-21
archived: 2026-10-25
resolution: Preflight inventories all seven AppHost external runtime roots and their 24-project closure in both Debug and Release, comparing EventStore packages against independently evaluated Parties selection. Missing references, package overrides, hidden source selection, unsupported edge contexts, unexpanded multi-target graphs and nonliteral resolver paths fail explicitly; cycles are bounded. This verifies graph selection, not compilation or runtime parity.
verification: spec-five-verification-maintenance-deferred-items.md; tests/five-verification-maintenance-deferred-items-2026-10-21/validation.json. Both Debug builds are clean and 397 focused tests pass without skips; source/runtime qualification limits remain explicit.

### DW-160: Extend the existing gateway command test with a direct persisted-stream and aggregate identity read.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: existing EventStore gateway command test; persisted event stream and aggregate identity
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The pre-existing test observes Completed status and positive eventCount but does not read stored events; completion follows EventPersister in the SDK, but an independent persisted-stream assertion would strengthen that boundary.
status: done 2026-10-22
archived: 2026-10-25
resolution: The gateway independently reads exact actor metadata and EventEnvelope keys derived from AggregateIdentity, with no Redis scans. It validates bounded sequence, aggregate identity, command correlation/causation, user, event type and deserialized payload against the projected result.
verification: `spec-five-runtime-isolation-and-form-authorization-deferred-items.md`; `tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json`. Release/package live acceptance passed with zero skips; mapped Debug test-build qualification limits are recorded separately.

### DW-161: Extend the persistent release verification job to validate all declared NuGet registry package bytes and repository commit metadata.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-nuget-trusted-publishing.md"), 2026-10-13
location: release verify-publication job; NuGet registry package bytes and repository commit metadata
source_spec: `_bmad-output/implementation-artifacts/spec-nuget-trusted-publishing.md`
reason: The pre-existing verify-publication job checks a GitHub Release tag only; this task performs independent nine-package download/metadata verification after Release rather than treating that tag as package publication evidence.
status: done 2026-10-22
archived: 2026-10-25
resolution: The persistent verifier downloads each of the nine exact declared packages, reuses archive and dependency-boundary checks, and requires exact nuspec ID/version/repository URL/type/commit. Receipts hash observed registry bytes without asserting equality with unsigned build archives.
verification: `tests/five-query-release-deferred-items-2026-10-22/validation.json`; spec `spec-five-query-release-deferred-items.md`.

### DW-162: Commit the existing Story 8.8 available-row identity gate correction through its owning work.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md"), 2026-10-13
location: Story 8.8 spec available-row identity heading; AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity
source_spec: `_bmad-output/implementation-artifacts/spec-unblock-eventstore-api-publication.md`
reason: The committed isolated integration run fails AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity because spec-8-8 lacks the required heading; the main working tree already contains the 10-line correction as separate user work and this task preserves it unstaged.
status: done 2026-10-21
archived: 2026-10-25
resolution: The previously separate ten-line Story 8.8 available-row heading correction was committed at 890119f1b4ced37ccec34787c812f064199fb3ef and remains intact. AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity and identity-gap cases pass. Frozen story intent and canonical sprint status are unchanged.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-164: D2 — Set an explicit SDK receiver identity for the optional sample resource matching its DAPR app ID.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.AppHost/Program.cs:237 (optional sample resource)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: AppHost sample at Program.cs:237 lacks the parties/tenants AppId binding; SDK fallback uses ApplicationName when DAPR_APP_ID is absent in the observed child path; verified medium, pre-existing.
status: done 2026-10-21
archived: 2026-10-25
resolution: The optional sample explicitly receives EventStore__DomainService__AppId=sample. Actual run and publish model regressions compare it with the attached Dapr sidecar app identity.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-165: D3 — Resolve the optional Memories resource endpoint for local Aspire runs.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties.AppHost/Program.cs:217 (optional Memories run-mode endpoint)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Program.cs:217 supplies http://memories:8080/ in run mode although Memories is a host project with an Aspire-assigned endpoint; verified medium, pre-existing.
status: done 2026-10-21
archived: 2026-10-25
resolution: Memories search now receives the optional resource HTTP EndpointReference in both run and publish models. Regressions resolve two different allocated addresses and ports through that same reference and preserve the dependency wait and enablement.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-167: D5 — Persist the canonical terminal Memories unit identity after duplicate ingestion.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: Memories indexing workflow duplicate branch and party memory-unit source mappings
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Indexing stores accepted workflow ID, but the duplicate workflow branch can return a different ExistingMemoryUnitId after the preflight TTL expires or fails open; source mapping replacement then loses the deletable ID; verified high, pre-existing.
status: done 2026-10-23
archived: 2026-10-25
resolution: Validated terminal output identity is durably retained with workflow/tenant/case agreement, including duplicate canonical outputs and legacy workflow aliases. Distinct units sharing a source remain retained; settled receipts keep canonical provenance for late mappings.
verification: `spec-five-memories-recovery-deferred-items.md`; `tests/five-memories-recovery-deferred-items-2026-10-23/validation.json` and `matrix-audit.json`. Full Debug and Release affected assemblies pass after the separate upstream qualification fix; live erasure qualification remains separately unclaimed.

### DW-170: D8 — Preserve durable reconciliation for cancellation after Memories accepts ingest but before the Parties mapping persists.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md"), 2026-10-13
location: src/Hexalith.Parties/Search/PartyMemoryIndexingService.cs (mapping-write cancellation boundary)
source_spec: `_bmad-output/implementation-artifacts/spec-run-all-tests-and-fix-issues-2.md`
reason: Indexing rethrows caller cancellation at the mapping-write boundary without compensation, while the server workflow can continue producing a unit; verified high, pre-existing.
status: done 2026-10-23
archived: 2026-10-25
resolution: Caller cancellation after acceptance triggers independent bounded recovery, retains unresolved or canonical provenance across restart, and rethrows the original cancellation. Verified cleanup progress is persisted independently of interrupted exact mapping subtraction.
verification: `spec-five-memories-recovery-deferred-items.md`; `tests/five-memories-recovery-deferred-items-2026-10-23/validation.json` and `matrix-audit.json`. Full Debug and Release affected assemblies pass after the separate upstream qualification fix; live erasure qualification remains separately unclaimed.

### DW-174: Enforce the identity-query deadline during synchronous admission and source folding.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs; PartyIdentitySourceFold.Fold; authority.Admit
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `authority.Admit` and `PartyIdentitySourceFold.Fold` execute between deadline checks, so a stalled provider or large source can exceed the configured budget; both paths predate the release patch.
status: done 2026-10-22
archived: 2026-10-25
resolution: Initial and final authority admission, both folds and retained-source validation run within the original whole-query deadline. Internal fold overloads check cooperative cancellation between transitions. Controlled stalls, cancellation precedence, exact-position success and terminal historical source-expiry/authority-revocation regressions pass; final focused identity suite passes 267/267.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

### DW-175: Bound noncooperative identity-query provider work after a caller deadline.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/PartyIdentityQueryDeadline.cs (ReadAsync)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartyIdentityQueryDeadline.ReadAsync` stops awaiting a stalled provider task but leaves it running; repeated calls can accumulate worker tasks. The deadline work was introduced separately from this release patch.
status: done 2026-10-22
archived: 2026-10-25
resolution: One finite process-wide limiter admits at most 16 current/historical queries without a waiting queue. Each query retains its lease until abandoned work and asynchronous provider cancellation callbacks both finish; late faults are observed. Controlled saturation, budget-acquisition and both completion-order recovery cases pass. Noncooperative work is bounded, not terminated; final full service suite passes 2421/2421.
verification: `spec-five-identity-routing-deferred-items.md`; `tests/five-identity-routing-deferred-items-2026-10-22/validation.json`.

### DW-176: Make long retained human-actor history folds linear.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties/Queries/RetainedHumanActorHistoryFold.cs (Append and Close)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `RetainedHumanActorHistoryFold.Append` scans prior bindings and `Close` searches by version for each transition; the scans existed at this spec's baseline.
status: done 2026-10-21
archived: 2026-10-25
resolution: The retained fold validates the expired-prefix boundary, concatenates already ordered partitions and checks only the last binding on append/closure. A 4096-binding history preserves first/middle/final intervals and original source positions; late backdating and empty intervals reject. Existing sparse/expired/revoked lifecycle coverage remains green.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-177: Show accepted-command acknowledgement before slow admin list loading finishes.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor (LoadPageAsync and route selection)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesAdminPortal` sets its updating notice only after awaiting `LoadPageAsync` and route selection; this ordering predates the release patch.
status: done 2026-10-21
archived: 2026-10-25
resolution: Accepted receipts render a polite notice before capability/list/detail reads, including unchanged authorization contexts. A new receipt starts a new list version; delayed reads cannot restore a cleared notice. Covered by pending-read, pre-receipt, supersession and route/authentication regressions. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-178: Clear the admin updating notice when the saved party reaches the projection.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor (_acceptedCommandPartyId)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesAdminPortal` clears `_acceptedCommandPartyId` on navigation and context reset but has no convergence clear, so the notice can remain after the row appears; this behavior predates the release patch.
status: done 2026-10-21
archived: 2026-10-25
resolution: Current healthy matching rows clear the notice; wrapper degradation, every non-current payload freshness state, unrelated/empty pages and superseded reads retain it. List/search freshness is preserved. This closes matching-row visibility only; command-specific edit convergence is tracked separately below. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-179: Restore the erasure confirmation action after a dialog open failure.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor (_pendingErasureDialogOpen, ShowAsync, _confirmErasure)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartyGdprOperationsPanel` consumes `_pendingErasureDialogOpen` before `ShowAsync` and catches `JSException` while `_confirmErasure` keeps the action button replaced; this is later UI work.
status: done 2026-10-21
archived: 2026-10-25
resolution: A failed Fluent dialog opening resets typed confirmation, restores the request action and announces a bounded transient failure without an older correlation. Repeated failures refocus the announcement; input-focus failures keep an opened dialog usable. Retry still requires fresh exact confirmation and sends only the explicitly confirmed command. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-180: Reproduce whether immediate blob URL revocation loses a supported-browser privacy export.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: src/Hexalith.Parties.UI/wwwroot/admin-privacy-export.js
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `admin-privacy-export.js` revokes the object URL immediately after `anchor.click()`; a supported-browser download trace would settle whether the scheduling can cancel the download. Unverified medium in later UI work.
status: done 2026-10-21
archived: 2026-10-25
resolution: Supported Chromium completes the real erased-party export despite immediate object-URL revocation. The browser regression verifies exact bytes, JSON MIME type, bounded valid UTC filename, object-URL creation/revocation and retained digest metadata. No speculative download timing change or cross-browser guarantee is introduced.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-181: Resolve the loop-sweep ledger fallback through layered BMAD configuration.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: .agents/skills/bmad-loop-sweep/SKILL.md (Step 1 ledger fallback); layered BMAD configuration
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: With `BMAD_LOOP_LEDGER` absent, the skill reads legacy YAML despite describing `config.toml` layering, so a custom artifact location can be missed; this is agent-context work outside the release patch.
status: done 2026-10-21
archived: 2026-10-25
resolution: Already resolved by installed sweep update 777e0e7d0c33dad7230e6477d1c3987a4b843b5f. Both installed entry points require the orchestrator-resolved BMAD_LOOP_LEDGER, never re-derive it, and escalate missing/unreadable paths. No legacy fallback or speculative TOML fallback was added; config-trace.py reproduces the installed contract.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-183: Test reason-only degraded admin search preserving last known rows.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: admin search SearchDegradedReason branch and component tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The new `SearchDegradedReason` branch preserves rows for an empty response, but existing tests use a nonempty page or other degradation signals; removing the branch would clear rows without a test failure. This is later UI work.
status: done 2026-10-21
archived: 2026-10-25
resolution: A reason-only empty degraded search regression asserts retained grid rows, bounded polite degraded status, no empty state and no raw backend reason. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-184: Assert accessible checked state after switching the admin party-type radios.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: admin party-type radio aria-checked expressions and component/browser tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: The new `aria-checked` expressions are not asserted by component tests or the browser test, which only checks radiogroup visibility; an inverted expression could misannounce the type. This is later UI work.
status: done 2026-10-21
archived: 2026-10-25
resolution: The existing party-type switching regression now checks both rendered aria-checked values initially and after switching to Organization, back to Person and again to Organization. Independent review completed. See spec-five-admin-deferred-items.md and tests/five-admin-deferred-items-2026-10-21/validation.json.

### DW-186: Check whether BMAD visual-tool consumers require an array from the layered TOML setting.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: _bmad/config.toml; _bmad/cis/config.yaml (visual_tools)
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `_bmad/config.toml` and legacy `_bmad/cis/config.yaml` both encode `visual_tools` as a JSON string. No repository consumer requiring an array was found; a runtime consumer trace would settle this unverified medium agent-config concern.
status: done 2026-10-21
archived: 2026-10-25
resolution: Investigated; no installed tracked non-reference CIS consumer requires an array. Runtime layered resolution preserves the JSON-string setting, and the renderer accepts that scalar while rejecting a native array token. Both new configuration tests pass, including every override layer. Preserve installer-managed configuration; this closes the unverified concern without claiming a CIS consumer run.
verification: `spec-five-runtime-config-deferred-items.md`; `tests/five-runtime-config-deferred-items-2026-10-21/validation.json`.

### DW-187: Test authenticated actor and tenant-delivery callbacks through the real service route.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: tests/Hexalith.Parties.Tests/Gateway/PartiesDomainServiceSecurityTests.cs; actor and /tenants/events service callbacks
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: `PartiesDomainServiceSecurityTests` checks denial and endpoint policy metadata, but no owned test sends a valid channel request to the actor or `/tenants/events` handler. A later HTTP security change could block legitimate delivery without a failing test.
status: done 2026-10-21
archived: 2026-10-25
resolution: Real production HTTP routes admit channel-only tenant delivery and registered PartyKeyRetryActor PUT calls. Tests inspect committed tenant membership/access, message deduplication, actor pending-state writes/deletion, reactivation read-back and real reminder callbacks through a private HTTP backend; missing, wrong and duplicate actor/tenant channel credentials reject. No runtime or authorization handler is replaced.
verification: `spec-five-identity-callback-deferred-items.md`; `tests/five-identity-callback-deferred-items-2026-10-21/validation.json`.

### DW-188: Exercise the release registry-floor comparison with isolated published-version fixtures.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: .github/workflows/release.yml (registry-floor parts > floor comparison); CI tests
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: Current CI tests inspect `.github/workflows/release.yml` text and step ordering but do not run its `parts > floor` comparison; a mutation that admits a stale tag could reach the protected release job. This gate was added after the original Parties release patch.
status: done 2026-10-21
archived: 2026-10-25
resolution: The CI assembly executes the exact registry-floor workflow bash with isolated GitHub/NuGet fixtures; numeric ordering, equality, prerelease and four-component behavior, invalid indexes and request failures are verified without publication.
verification: `_bmad-output/implementation-artifacts/tests/five-package-validation-deferred-items-2026-10-21/validation.json`; spec `spec-five-package-validation-deferred-items.md`.

### DW-189: Run the existing contact-selector browser test in required CI.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md"), 2026-10-13
location: contact-selector Playwright create-form test; component FluentSelect.ValueChanged tests; configured CI workflow
source_spec: `_bmad-output/implementation-artifacts/spec-fix-ci-and-publish-release.md`
reason: Component tests invoke `FluentSelect.ValueChanged` directly while the Playwright create-form test is outside the configured CI workflow. Removing the rendered `FluentOption` children could pass required tests but leave users unable to select a contact type. This is later UI work.
status: done 2026-10-21
archived: 2026-10-25
resolution: The unconditional blocking UI job runs the full admin, Consumer and picker specs in one Playwright invocation alongside the accessibility specimen, including actual contact Email option selection. CI contract tests preserve required execution and existing check identity; a Release/package browser run passes.
verification: `spec-five-browser-deferred-items.md`; `tests/five-browser-deferred-items-2026-10-21/validation.json`.

### DW-190: Require the FrontComposer shared-status structural command and test its missing-command outcome.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
location: RequiredEvidenceTokensBySurface and RequiredDeliveredApiCommands (FrontComposer FcSurfaceStatus command)
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
reason: Existing RequiredEvidenceTokensBySurface and RequiredDeliveredApiCommands do not mandate the FcSurfaceStatus command; removing an individual FrontComposer command leaves other searches satisfying the row's nonempty-command guard. This gap predates the current conditional-expression refresh.
status: done 2026-10-21
archived: 2026-10-25
resolution: FrontComposer required tokens now include FcSurfaceStatus and its structural aria-live expression. RequiredDeliveredApiCommands mandates the exact positive command/path; command-removal and wrong-path/negative-search cases protect this requirement.
verification: `_bmad-output/implementation-artifacts/tests/five-test-harness-deferred-items-2026-10-21/validation.json`; spec `spec-five-test-harness-deferred-items.md`. The separate existing Builds receipt mismatch is reproduced against baseline and remains outside these corrections.

### DW-191: Validate McpCli and Platform source-selection rows independently of the eight consumed dependency fitness checks.

origin: migrated from legacy ledger (flat source_spec "_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md"), 2026-10-13
location: CurrentReleaseGitlinks; McpCli and Platform source-selection documentation rows
source_spec: `_bmad-output/implementation-artifacts/spec-gh-38399980224-fix-ci-and-publish-release.md`
reason: Existing CurrentReleaseGitlinks excludes these non-consumed root selections even though the current documentation table records them; release gitlink signing protects changed pointers, while direct table identity coverage remains future hardening.
status: done 2026-10-21
archived: 2026-10-25
resolution: All ten current root selections, including non-consumed McpCli and Platform, match current table rows, committed gitlinks, clean checkouts and exact Git describes. Eighteen independent row-boundary cases and two standalone real Git-receipt cases cover both non-consumed dependencies. Describes must now occur in the identity cell; historical/prose mentions cannot repair stale identity evidence. No source pointer, owner approval or historical receipt changed.
verification: `tests/five-release-proof-deferred-items-2026-10-21/validation.json`; spec `spec-five-release-proof-deferred-items.md`.


### DW-192: Derive erasure status from committed events or reconcile it after event persistence; cover failed append and retry before reporting Verified or Erased.
origin: retro action item epic-8-retro-erasure-status-after-commit
location: n/a
source_spec: `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md`
severity: low
reason: retrospective action item from sprint-status.yaml (epic 8; owner Amelia (Developer) + Winston (Architect) + Murat (Test Architect); sprint-status status open)
status: done 2026-10-23
archived: 2026-10-25
resolution: Lifecycle status queries now use metadata folded from committed events in the existing detail slot. Domain evaluation cannot publish premature lifecycle authority; legacy side records and cleanup tombstones cannot certify success. Complete committed replay backfills old rows without changing empty-v1 bytes. Connected failed/canceled append, retry, persisted projection and recreated-query controls pass.
verification: `spec-five-recovery-and-ui-conformance-deferred-items.md`; `tests/five-recovery-and-ui-conformance-2026-10-23/validation.json` and `matrix-audit.json`.

