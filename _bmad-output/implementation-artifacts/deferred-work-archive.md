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

