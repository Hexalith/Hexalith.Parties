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
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
  summary: Hoist normalized multi-token search candidates outside per-entry evaluation.
  evidence: `EvaluateEntry` rebuilds the query-only full phrase and candidate collection for every party, creating O(entries) allocations in the 10K hot path despite the current performance gate passing.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-30708560778-fix-ci-failures.md`
  summary: Normalize synthetic full-phrase coverage in multi-token relevance scoring.
  evidence: A deterministic full-phrase match is added alongside real query tokens, so coverage can exceed one before the final score is clamped and can inflate ordering relative to token-only matches.
