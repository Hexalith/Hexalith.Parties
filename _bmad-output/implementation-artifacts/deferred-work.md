- source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
  summary: Add a lane-runner mode that continues after failed projects and reports every failing project in one run.
  evidence: `scripts/test.ps1 -Lane all` and each CI shard currently stop at the first failing project, so a package-mode restore blocker can hide later project-specific failures until the first blocker is resolved.
  status: resolved
  resolved_by: Story 8-11 (sprint-change-proposal-2026-07-07-validation-ladder-runner.md). `scripts/test.ps1 -ContinueOnFailure` runs every project and prints a PASS/FAIL summary (exit 1 if any failed); the CI `Run test shard` loop continues after a failing project and summarizes all failures. Default fail-fast behavior preserved.
- source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
  summary: Add inspectable local test result output and optional build/restore property forwarding to `scripts/test.ps1`.
  evidence: CI writes TRX/coverage artifacts and some local blockers require properties such as `UseHexalithProjectReferences=true`, but the local lane runner currently exposes neither a results-directory/logger option nor a safe property-forwarding interface.
  status: resolved
  resolved_by: Story 8-11 (sprint-change-proposal-2026-07-07-validation-ladder-runner.md). `scripts/test.ps1 -ResultsDirectory <path>` emits a per-project TRX (local CI parity) and `-Properties <k=v>,<k=v>` forwards each value as `-p:<value>` to `dotnet test`.
- source_spec: `_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`
  summary: Define a support-safe consent/channel identifier contract for GDPR consent commands.
  evidence: `RecordConsent` and `RevokeConsent` currently accept `ChannelId`/`ConsentId` values that can contain legacy `channel:purpose` separators, so applying the new `PartyIdentifier` semantic-ID helper would break existing consent IDs while leaving aggregate not-found messages able to echo raw consent/channel identifiers.

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

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
  summary: Correct and validate the advanced Hexalith.Builds checkout before adopting its package-version changes.
  evidence: Checkout `63d3221` supplied `v1.16.3` as a NuGet version and caused Actions runs `29467970597` and `29468665570` to fail during restore. Builds `v4.18.11` corrected the value to `1.16.3`; commit `6516faf` adds the evaluated central-version release guard and fixtures. Builds `v4.19.0` retains both changes and adds the MTP-compatible shared test contract exposed by follow-up run `29482004796`; the Parties gitlink/signoff adopt that release.
  status: resolved
  resolved_by: `_bmad-output/implementation-artifacts/spec-gh-29467970597-fix-invalid-builds-package-version.md`; Hexalith.Builds `640b59c1434e4e1e079771c401e11048772c7a27` (`v4.19.0`)
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
  summary: Add a persisted-LRU eviction regression test for the advanced Hexalith.Memories checkout.
  evidence: Incidental review found the new workflow recency field is tested across serialization and eviction separately, but not after serialize/restore at the 256-entry limit; a restored actor could evict a recently refreshed workflow and reapply a delayed transition.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md`
  summary: Add an intermediate-state migration test for the advanced Hexalith.Memories checkout.
  evidence: Incidental review found no test for persisted state containing `AppliedTransitionSequences` while lacking the newer `AppliedTransitionWorkflowOrder`, leaving the immediate predecessor format's eviction queue reconstruction unverified.
- source_spec: none
  summary: Deliver EventStore.Client granular typed-client registration with Parties and FrontComposer coexistence proof.
  evidence: This independently shippable EventStore.Client package change was split from the G8 owner-proof action so the EventStore.Aspire JWT prerequisite can be completed first.
- source_spec: none
  summary: Deliver FrontComposer.AppHost or approved platform AppHost integrated-topology parity proof.
  evidence: This independently shippable platform-host change was split from the G8 owner-proof action because it depends on the EventStore Aspire and client-registration surfaces being proven first.
- source_spec: none
  summary: Deliver the external platform-operations runtime deployment handoff for G8.
  evidence: This independently governed operational handoff was split from the G8 owner-proof action because it requires platform-owner coordination after local run and publish parity are established.
- source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
  summary: Add runtime multi-audience JWT enforcement and positive/negative token-validation proof to EventStore and consuming hosts.
  evidence: This was split because the reusable EventStore.Aspire composition surface can ship independently before each host authentication configurator adopts ordered valid audiences.
- source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
  summary: Harden the EventStore owner AppHost publish path and poison-scan an actual published artifact for credential leakage.
  evidence: This was split because owner-AppHost adoption and publish-output validation are independently shippable after the reusable JWT composition API exists.
- source_spec: `_bmad-output/implementation-artifacts/spec-8-8-eventstore-aspire-audience-aware-jwt-parity.md`
  summary: Automate integrity validation for the G8-A owner-delivery receipt and its selected producer identity.
  evidence: The review confirmed that current Parties fitness tests parse only the marked matrix table and do not bind the supplemental receipt SHA, claimed four-file inventory, or focused EventStore test lane to the referenced Git objects.
- source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
  summary: Bind the operational-index metadata ACL route to the EventStore policy, POST verb, and allow action in one focused assertion.
  evidence: Incidental review of concurrent ACL edits found that independent string assertions can pass when `/admin/operational-index-metadata` is placed under the wrong app policy, verb, or action.
- source_spec: `_bmad-output/implementation-artifacts/spec-align-assistant-commit-message-generation.md`
  summary: Reconcile persistent BMAD branch guidance with the Hexalith default-main Git policy.
  evidence: The pre-existing project context still requires a typed branch and PR, while the authoritative Hexalith Git instructions say to work on `main` by default and branch only when genuinely required.
- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
  summary: Pin the Node/npm runtime used by release-tooling workflows.
  evidence: CI and release workflows use floating `lts/*`; changing this is pre-existing policy and the approved spec explicitly requires approval for Node engine policy changes.
- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-fix-memories-npm-vulnerabilities.md`
  summary: Make the semantic-release workflow invocation fail closed to the installed local binary.
  evidence: `npx semantic-release` predates this change and may fetch if local tooling is absent; resolving it requires a separate release-workflow policy decision.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
  summary: Hoist normalized multi-token search candidates outside per-entry evaluation.
  evidence: `EvaluateEntry` rebuilds the query-only full phrase and candidate collection for every party, creating O(entries) allocations in the 10K hot path despite the current performance gate passing.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
  summary: Normalize synthetic full-phrase coverage in multi-token relevance scoring.
  evidence: A deterministic full-phrase match is added alongside real query tokens, so coverage can exceed one before the final score is clamped and can inflate ordering relative to token-only matches.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03)

- Three independent `UpdateAsync` calls in `PartySdkReadModelEraser.EraseAsync` can leave detail/processing/index mutually inconsistent on mid-flight failure — no multi-key transactional write seam in the approved `ReadModelWritePolicy` API.
- Optimistic concurrency retries re-run `ApplyErasure` and refresh `ErasedAt` — `ApplyErasure` always stamps `UtcNow`; short-circuiting on `IsErased` needs a deliberate idempotency contract change.
- Erasure copies through pre-erasure `ProjectedAt`/`ProjectionVersion` on detail/index — stamping erasure-time freshness is entangled with the open AC7 freshness-mapping gap; index timestamps also cover unrelated remaining parties.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 2)

- `s_caseIdMissingWarned` is an unbounded static ConcurrentDictionary (one entry per tenant/party for process lifetime) — mirrors retired orchestrator pattern.
- Index `ProjectionVersion` scheme (`global:N` / `{id}:{seq}` / keep-current) lacks Fold/class remarks for freshness/query consumers.
- `GetOperationCategory` default arm returns a short event-type name rather than a stable category vocabulary — Art.30 taxonomy design choice.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 3)

- Out-of-range `PageSize` under `Paging` rejected as `InvalidCursor` even with no cursor — debugging misdirection only.
- Non-durable unbounded in-process last-known cache; no `ApplicationStopping` link; Actor-named constant bags; missing-detail vs empty-processing asymmetry — intentional shim/architecture trade-offs from the first Group 3 pass.
- ~~Cursor codec `failureReason` discarded~~ — resolved 2026-08-03 (`LogCursorRejected` in `PartySdkQueryService`).

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 4)

- Host `AddEventStoreDomainService(... PartyDetailProjectionHandler.Assembly)` remains source-text-only — closing properly needs reinstating the retired tenant seeder for authenticated query e2e.
- ACL allow-list has no runtime Dapr enforcement check beyond YAML fitness — same topology e2e class as the assembly-scan defer.
- Minor/cosmetic: query shim classes keep "Actor" names; `EventStore:Projections` config-key reuse; undocumented `Dapr.Actors.AspNetCore` / MSBuild property rename — intentional temporary trade-offs from the first Group 4 pass.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-03 Group 5)

- Prior Group 5 cosmetic defer remains open (stringly DI absence checks; health "all components" naming; partial Ada→Synthetic rename; undocumented MessageId status-key change).
- `TestCursorCodec` private double instead of production DI codec; collapsed index invalid-payload theory; six indistinguishable `<factory-registered>` hosted-service exclusions — intentional test-isolation / factory-registration limits from the first Group 5 pass.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04)

- Whole-payload `json-redacted` events still depend on a resolvable CLR type and can apply a default-valued event produced from `{}`. The same behavior existed in the retired actor path, and the current field-level protection service does not normally produce a root encrypted marker; correcting it belongs to the broader payload-redaction contract rather than this migration patch chunk.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-04, fresh full-diff pass)

- `PartyIndexSdkProjectionHandler.BuildReconciliationFold` recomputes `LastIndexedEvent` via a separate code path from the canonical `FoldCore`, used only on the already-confirmed idempotent-no-op/reconciliation branch — could pick a different "last event" for search-reconciliation notification metadata on a multi-event no-op batch, but doesn't affect canonical read-model correctness. Needs a dedicated multi-event test to pin the intended behavior.
- `PartyMemoryCleanupService`'s new "no persisted CaseId and no fallback configured" blocked branch has zero test coverage.
- `PartyIndexSdkProjectionHandler.CompleteRebuildAsync` would throw `NullReferenceException` (not a controlled result) if a persisted rebuild-completion manifest ever deserializes with null `Entries`/`RemovedPartyIds`. Not reachable under the current producer (`FinalizeAsync` always serializes non-null arrays); hardening-only.
- `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible` is RED: it hard-pins the Story 8.3 matrix's "Payload protection engine package" (G5) row to EventStore `v3.89.0`/`7854f8e5`, but the working tree is now at `v3.91.0`/`1d6e9321` (this story's resolved EventStore identity). Pre-existing to this review session, not caused by its patches. Out of Story 8.6 scope — G5 payload-protection is Story 8.7's territory and needs its own owner-reviewed identity-authorization update, not a Story 8.6 patch.

- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
  summary: Pin the reusable commitlint workflow to an immutable reviewed revision.
  evidence: Incidental review found `.github/workflows/commitlint.yml` consumes `Hexalith/Hexalith.Builds/.github/workflows/commitlint.yml@main`, allowing unrelated upstream changes to alter validation without a reviewed Parties change.
- source_spec: `/home/administrator/projects/hexalith/parties/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`
  summary: Verify every expected package and container artifact before declaring a release successful.
  evidence: Incidental review found `.github/workflows/release.yml` treats a non-draft GitHub Release at the dispatched commit as sufficient proof, without verifying the complete NuGet and container artifact set.
- source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
  summary: Pin the Story 8.7 payload-protection (G5) matrix row's validation-evidence commands to an exact commit instead of a moving `HEAD` reference.
  evidence: Blind-hunter review of the 2026-08-04 SCP recovery found the G5 row's `git ls-tree HEAD references/Hexalith.EventStore` / `references/Hexalith.Builds` commands resolve against whatever the working tree currently points to, unlike the sibling projection/query SDK and DataProtection rows in the same matrix, which pin to an exact Parties commit (`03ab938c637aa15f7a0af402afc8664dfc54d1a4`) for reproducibility. This pattern pre-dates the 2026-08-04 identity refresh; the refresh preserved rather than introduced it.
- source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
  summary: Restore the G8 Aspire/AppHost proof-requirements paragraph and the DataProtection-identity-consumption sentence dropped from `epic-8-context.md` during its regeneration, and expand the compressed Story 8.12/8.13 Cross-Story Dependencies detail back out.
  evidence: Blind-hunter review found the working-tree regeneration of `epic-8-context.md` (predating this SCP recovery; only its line endings were normalized here) silently dropped the G8 local-run/publish JWT, audience-relationship, HTTPS-metadata, and secret-free-manifest proof requirements, and the sentence tying `AddEventStoreDataProtection`/`DaprXmlRepository`/cursor-codec consumption to the DataProtection prerequisite identity. It also compressed the explicit list of what stays externally owned for Stories 8.12/8.13 (production manifests, DAPR components, ingress, secrets, scans, signatures, promotion gates) into one generic sentence. A future Story 8.8/8.9/8.10 session loading only the cached epic context would miss this guidance.
- source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
  summary: Reconcile the two conflicting trackers of the `PartyIndexSdkProjectionHandler.FinalizeAsync` rebuild/live-write concurrency defect and add it to this ledger.
  evidence: Blind-hunter review found the defect (blind `ReadModelBatchConcurrency.LastWrite` can drop a canonical entry added mid-rebuild) exists both as a still-unchecked `[ ]` Group 2 task and, separately, inside a `[x]`-checked "Fixed 2026-08-04 (partial)" bullet in `8-6-projection-and-query-sdk-migration.md` that itself states the underlying issue is "left open — not addressed by this patch." The two are never cross-referenced, and the defect was never logged here, so it is invisible to anyone scanning only this ledger.
- source_spec: `_bmad-output/implementation-artifacts/spec-scp-2026-08-04-story-8-6-g5-receipt-recovery.md`
  summary: Document the `.agents/skills/bmad-sprint-planning/scripts/sprint_plan.py` `--fresh` rebuild fix (preserve `generated`/`last_updated` only when not forcing a fresh rebuild) and attribute it in the 8.6 story or this ledger.
  evidence: Blind-hunter review found this fix and its new test assertions are a distinct bug from the previously-documented STORY_RANK/`_slug()` regeneration incident, but no file in the current diff explains or attributes it, leaving a future reader unable to tell why `sprint_plan.py` changed.
- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Reconsider the `[LoggerMessage]` vs. plain-`ILogger` choice in `PartySdkProjectionFold.Log` for house-style consistency.
  evidence: 2026-08-05 review-layer finding — the `Log` class's comment claims `[LoggerMessage]` can't be used because `Hexalith.Parties.Projections.csproj` lacks a direct `Microsoft.Extensions.Logging.Abstractions` package reference, but `Hexalith.Parties.Security.csproj` is in the identical situation and successfully uses `[LoggerMessage]` throughout (`PartyKeyLifecycleService.cs`, `DecryptionCircuitBreaker.cs`, `PartyErasureOrchestrator.cs`) via a package reference with `ExcludeAssets="all"`. Adopting the same fix (or correcting the comment if a real difference is found) needs a deliberate, verified change to build configuration, not a same-pass patch.
- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Attribute dropped-event diagnostics to the class that actually detects the drop, not whichever handler happened to pass its `ILogger<T>` in.
  evidence: 2026-08-05 review-layer finding — drops detected inside the shared static helpers `PartySdkProjectionFold`/`PartyProcessingActivityFold` are logged under `PartyDetailSdkProjectionHandler`'s or `PartyIndexSdkProjectionHandler`'s log category depending purely on which handler called in. An operator filtering by the actual source class gets nothing, and the same drop reason can appear under two different categories. Fixing this cleanly needs a design decision (e.g., a dedicated logger category or `ILoggerFactory` seam), not a quick patch.
- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Decide an acceptable log-volume strategy (batching/sampling/dedup) for the new drop diagnostics during full projection rebuilds.
  evidence: 2026-08-05 review-layer finding — `PartyIndexSdkProjectionHandler.AccumulateAsync` (the full-rebuild path) now re-emits a log line for every historically-known-bad event on every rebuild run, with no batching, sampling, or dedup — a real log-flooding risk on a large event store. Needs a product/ops decision on acceptable rebuild-time log volume, not a same-pass patch.
- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Widen `PartySdkProjectionFold.DeserializeNew`'s catch filter to cover `FormatException`/`OverflowException` from custom converters.
  evidence: 2026-08-05 review-layer finding, pre-existing (not caused by this session's patch): the catch filter only covers `JsonException`/`ArgumentNullException`/`NotSupportedException`/`InvalidOperationException`; a `FormatException` or `OverflowException` thrown by a custom converter propagates unhandled and crashes the whole dispatch instead of being skip-logged.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-05)

Human-directed: the 2026-08-05 build session restored operator diagnostic logging and
added fold-level test coverage, then deferred the remaining open review findings below
rather than force them through this pass. This entry also fulfills the still-open
2026-08-04 action item above ("Reconcile the two conflicting trackers... and add it to
this ledger") for the `FinalizeAsync` concurrency defect.

- `PrepareRebuildAsync`/`FinalizeAsync` write with `ReadModelBatchConcurrency.LastWrite`
  (no ETag check) [`PartyDetailSdkProjectionHandler.cs:99,103`,
  `PartyIndexSdkProjectionHandler.cs:102`] — a rebuild finalize can silently overwrite a
  newer concurrent live `ProjectAsync` write with no conflict detection. Investigated
  2026-08-05: switching to `Match(etag)` unilaterally is unsafe without knowing the
  EventStore SDK rebuild-plan executor's retry/abort contract on a write conflict — that
  contract lives in `Hexalith.EventStore.DomainService`'s rebuild orchestration, outside
  this repo's `IAsyncDomainProjectionRebuildHandler` /
  `IAsyncDomainSharedProjectionRebuildCompletionHandler` surface. Needs SDK-owner input,
  not a unilateral Parties-side change.
  Resolved 2026-08-16: EventStore v3.95 now provides the required bounded conflict
  contract. Parties rebuild plans use `Match(etag)` for existing rows and `CreateOnly`
  for absent rows; focused plan-policy tests pass for detail, processing, and index.
- Host wiring (`builder.AddEventStoreDomainService(typeof(PartyAggregate).Assembly,
  typeof(PartyDetailProjectionHandler).Assembly)`) is verified only as literal source
  text by `ArchitecturalFitnessTests`/`PlatformApiPrerequisitesTests`/
  `RetiredLeafProjectFitnessTests`; no test queries a projected read model after an
  authenticated end-to-end command. Closing this needs `EventStoreGatewayE2ETests`, but
  its `PartiesAspireTopologyFixture.RequireSeededTenants()` unconditionally throws since
  Story 12.2 retired `TenantIntegrationTestSeeder` — reinstating that seeder is real work
  out of scope for a review-patch pass.
- `PartyProcessingSdkReadModel.Records` grows unbounded — one ever-growing JSON blob per
  party, re-serialized on every processing-activity projection write. A real scalability
  concern but needs a pagination/archival design, not a quick patch.
- Minor/cosmetic, `PartyDetailSdkProjectionHandler`/`PartyIndexSdkProjectionHandler`
  family: sequential (not parallel) `GetAsync` calls doubling state-store round-trip
  latency on the busiest projection path; duplicated `StoreName` null-check across
  classes; `PartyErased.LastModifiedAt` immediately overwritten by
  `NormalizeEventTimestamps` (harmless while both timestamps match, would silently
  diverge otherwise); `PartyIndexSdkProjectionHandler.Validate` reusing
  `PartySdkReadModelAddresses.Detail(...)` purely for its validation side effect,
  coupling Index validation to Detail's address-shape rules.
- Minor/cosmetic, rollback-shim naming and test quality: `PartyDetailProjectionQueryActor`
  / `PartyIndexProjectionQueryActor` keep the "Actor" name with zero actor behavior
  (intentional temporary rollback shims); `PartySdkReadModelOptions.ConfigurationSection`
  reuses the retired `Hexalith.EventStore.Server.Configuration.ProjectionOptions`'s
  `"EventStore:Projections"` config key; the new `Dapr.Actors.AspNetCore` package
  reference and `$(HexalithCommonsHttpFromSource)` MSBuild property rename are
  undocumented but verified correct; the DI test
  `AddParties_UsesSdkReadModelsAndCursorCodecWithoutLocalProjectionMechanics` asserts
  absence via a brittle `descriptor.ServiceType.FullName` string match rather than a type
  reference; `HealthEndpoint_AllComponentsHealthy_Returns200WithoutRetiredProjectionActorCheckAsync`
  keeps an "all components healthy" framing that now excludes SDK read models from what
  "all" verifies; the PII seed rename (`"Ada"/"Lovelace"` →
  `"SyntheticPrivateFirstName8472"/"SyntheticPrivateLastName6391"`) landed in only 2 of
  dozens of usages across `EventStoreGatewayRoutingTests.cs`, with 7 other test files
  still using `"Ada"/"Lovelace"`; `DirectPartiesCommandRouter`'s test double now keys its
  completion write on `command.MessageId` instead of `command.CorrelationId`, correctly
  mirroring production `SubmitCommandHandler.cs` behavior but undocumented in the diff.
- Cosmetic: the Epic 7 rollback-retention action item is closed `done` citing an
  authorization SCP "approved 2026-08-02" for an action the same annotation dates to
  2026-08-01 (approval postdating the act it authorizes by a day); resolves naturally
  when `sprint-status.yaml` is next synced.
- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Retry Incomplete erasure batches after re-reading store etags instead of replaying the same batch payload.
  evidence: PartySdkReadModelEraser.ExecuteWithResumeAsync re-executes the original batch on Incomplete without refreshing etags; a partial apply can loop into sdk-read-model-cleanup-conflict.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Add an erasure composition test that proves memories-search cleanup with indexing disabled and durable mappings present.
  evidence: ProjectionPlatformAdapterTests invoke memories-search cleanup with Enabled=false and no seeded mappings, so Cleaned can pass without exercising DELETE/clearance.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Rename ActorNotFoundInfrastructure/ActorException query failure vocabulary now that Dapr projection actors are gone.
  evidence: PartySdkQueryService still returns actor-era failure reasons on the SDK path, which misleads operators after AC8 actor deletion.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Move PartyEventTypeResolver out of the retired Projections/Actors packaging folder.
  evidence: The resolver remains under Actors/ after projection actors were deleted, obscuring ownership.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Document or automate Dapr-actor to SDK read-model key backfill for existing deployments.
  evidence: Story File List deletes actor projection paths without an AppHost/deploy cutover that migrates existing actor state into SDK keys.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Harden PartyMemoryUnitMappingStore upsert when MemoryUnitId and SourceUri match different existing rows.
  evidence: Edge-case review found a second live mapping row can be dropped when two entries match the new unit id and source uri separately.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Restore stronger party-id length/allowlist validation on the SDK query detail envelope.
  evidence: TryValidateDetailEnvelope only rejects reserved chars after TenantSafeProjectionReadGuardrailsTests were deleted; oversized/malformed party ids are weakly gated.

## Deferred from: code review of 8-6-projection-and-query-sdk-migration.md (2026-08-09)

DI + query host sub-chunk (`PartiesServiceCollectionExtensions.cs` vs `2c4a7af`).

- `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts` still expects deleted projection-adapter registrations and old `ProjectionPlatformAdapterTests` method names — deferred, pre-existing e2e drift outside this DI chunk.
- Erasure cleanup timestamps still use `DateTimeOffset.UtcNow` instead of the newly registered `TimeProvider` — deferred, pre-existing certificate timestamp pattern across erasure store results.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Protect Memories mapping replacement and clearing with ETag-aware retry semantics.
  evidence: `PartyMemoryUnitMappingStore.ReplaceMappingsAsync` and the empty-list delete can overwrite a concurrent indexing write after cleanup reads the prior mapping set.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Persist partial Memories cleanup progress with a token that survives caller cancellation.
  evidence: `PartyMemoryCleanupService.DeleteByPartyAsync` uses the already-cancelled caller token in its `finally` mapping update, so cancellation can prevent the promised resumable audit state from being saved.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Compensate when cancellation occurs after Memories ingestion but before mapping persistence.
  evidence: `PartyMemoryIndexingService` rethrows caller cancellation from `RecordMappingAsync` without deleting the already-created Memories unit, leaving an untracked unit outside erasure discovery.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Use ingestion-time endpoint and CaseId data for Memories compensating deletion.
  evidence: `TryCompensatingDeleteAsync` gates cleanup on the current options snapshot even though configuration can change after ingestion and the unit retains its authoritative CaseId.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Resolve consumer package-validation support artifacts from current central versions.
  evidence: `scripts/validate-consumer-package-references.py` hard-codes obsolete FrontComposer and Tenants versions instead of the currently evaluated dependency set.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Keep consumer package-validation caches inside the disposable validation workspace.
  evidence: `scripts/validate-consumer-package-references.py` places `NUGET_PACKAGES` under the work directory's parent, so cleanup leaves packages that can mask missing-feed failures in later runs.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Make consumer package validation use only explicitly configured NuGet sources.
  evidence: The generated NuGet configuration lacks `<clear/>`, and the CLI always retains nuget.org, allowing undeclared user or machine feeds to hide incomplete local package output.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Compare forbidden NuGet dependency identifiers case-insensitively.
  evidence: `scripts/validate-nuget-packages.py` performs case-sensitive package-ID checks even though NuGet identifiers are case-insensitive.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Pin reusable CI and CodeQL workflows to reviewed immutable revisions.
  evidence: `.github/workflows/ci.yml` and `.github/workflows/codeql.yml` invoke reusable workflows through mutable `@main` references.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Publish Aspire-hosted services with Production environment defaults.
  evidence: `src/Hexalith.Parties.AppHost/Program.cs` emits `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` as `Development` for publish output.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Fail publish preflight when the confidential OIDC client secret is absent.
  evidence: `src/Hexalith.Parties.AppHost/Program.cs` substitutes an empty client secret and continues producing deployment artifacts that cannot authenticate.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Reject duplicate keyed identifiers while merging BMAD configuration arrays.
  evidence: `_bmad/scripts/config_utils.py` can retain repeated base codes or ids, leaving ambiguous effective configuration after overrides.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Tolerate deleted historical tag references during release verification.
  evidence: `.github/workflows/release.yml` can report a successful current publication as failed when an older release references a tag that no longer exists.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Restore the Playwright browser accessibility lane as a required CI gate.
  evidence: The replacement CI workflow no longer runs `npm run test:a11y`, leaving axe, keyboard-focus, forced-colors, computed-style, and visual checks unexecuted.

- source_spec: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  summary: Add executable mTLS topology coverage across every configured Dapr sidecar.
  evidence: Current tests inspect generated YAML and one synthetic sidecar but never start the mTLS topology or prove a cross-service invocation with all sidecars credentialed.
