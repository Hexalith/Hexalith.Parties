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
