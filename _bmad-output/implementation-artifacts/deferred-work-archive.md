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
resolution: already resolved: Commit 8e3953e0; src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs:278-325 re-reads canonical state before each rebuild-completion notification, and tests/Hexalith.Parties.Projections.Tests/PartySdkProjectionHandlerTests.cs:1371-1480 covers concurrent erase and re-add.

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

