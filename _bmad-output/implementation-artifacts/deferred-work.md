- source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
  summary: Add a lane-runner mode that continues after failed projects and reports every failing project in one run.
  evidence: `scripts/test.ps1 -Lane all` and each CI shard currently stop at the first failing project, so a package-mode restore blocker can hide later project-specific failures until the first blocker is resolved.
- source_spec: `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
  summary: Add inspectable local test result output and optional build/restore property forwarding to `scripts/test.ps1`.
  evidence: CI writes TRX/coverage artifacts and some local blockers require properties such as `UseHexalithProjectReferences=true`, but the local lane runner currently exposes neither a results-directory/logger option nor a safe property-forwarding interface.
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
