---
story_key: 7-8-release-rollback-cleanup-and-readiness-gate
story_id: "7.8"
epic: "7"
created: 2026-06-29T18:54:33+02:00
source_status: backlog
target_status: ready-for-dev
baseline_commit: 34f7a94
---

# Story 7.8: Release, rollback, cleanup, and readiness gate

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As product leadership and architecture,
I want the cross-submodule release closed with rollback and readiness evidence,
so that Epic 7 remains controlled maintenance rather than an open-ended platform rewrite.

## Acceptance Criteria

1. Given Epic 7 adoption stories are done, when this story starts, then it verifies stories 7.1 through 7.7 are complete, reads their validation and review evidence, and records any residual blocker or deferred cleanup item before changing source.
2. Given Epic 7 used root `references/` submodules and project references, when final readiness is recorded, then every touched shared repository or package has its final commit hash or package version documented, including whether the working tree gitlink is clean or intentionally changed.
3. Given rollback must remain possible, when cleanup is attempted, then Parties-local infrastructure is removed only where previous stories prove parity and a non-deletion rollback path still exists; otherwise the item is explicitly deferred with owner, reason, and required evidence.
4. Given current projection migration evidence retained rollback-only and still-active local pieces, when Story 7.8 reviews projection cleanup, then `LocalPartyProjectionPlatformAdapter`, `PartyProjectionPlatformAdapterMode.Local`, actor companion sequence keys, and `ProjectionRebuildService` are deleted only if fresh tests prove EventStore-mode behavior, replay-from-zero idempotency, rebuild resume/cancel, GDPR processing-record reads, and rollback remain safe.
5. Given current crypto migration uses a Parties-owned EventStore adapter over existing AES/key policy, when Story 7.8 reviews crypto cleanup, then `PartyPayloadProtectionService`, party key management, erasure orchestration, certificates, export/processing-record redaction, and rollback provider registration are preserved unless an approved shared provider and compatibility proof replace them without weakening GDPR behavior.
6. Given Story 7.3 left one duplicated UI/e2e normalization follow-up, when this story addresses or defers it, then `PartiesAdminPortalE2eFixture` either consumes the shared Commons string helper with validated UI build/test evidence or the duplicate fixture helper remains documented as deferred due to build-surface constraints.
7. Given public consumers already use Parties packages, UI RCLs, EventStore gateway routes, and DAPR `/process`, when this story completes, then command/query contracts, `PagedResult<T>`, freshness records, auth/self-scope behavior, DAPR ACLs, GDPR legal semantics, and UI behavior remain compatible or any incompatibility is stopped before release.
8. Given final release readiness is the goal, when validation runs, then the agreed Parties build/test lanes, owner-submodule lanes for every touched submodule, package/API compatibility checks, deploy validation, `git diff --check`, and `scripts/check-no-warning-override.sh` pass or each blocker is recorded with exact command, error, scope, and release decision.
9. Given production KMS remains a pre-existing deployment prerequisite, when readiness notes are written, then they confirm Epic 7 did not make `LocalDevKeyStorageBackend` production-safe and did not authorize regulated EU personal data without a real KMS/secret-store-backed provider.
10. Given Epic 7 is post-MVP platform maintenance, when final documentation and sprint status are updated, then readiness notes state no PRD functional requirement coverage changed, durable docs/project context are updated only for stable new rules, and sprint status is advanced without rewriting unrelated history.

## Tasks / Subtasks

- [x] Establish final baseline and story sequence evidence (AC: 1, 2, 10)
  - [x] Read `_bmad-output/implementation-artifacts/7-1-*.md` through `7-7-*.md`, focusing on Debug Log References, Completion Notes, review findings, File Lists, rollback notes, and blocked evidence.
  - [x] Confirm `sprint-status.yaml` shows stories 7.1-7.7 as `done` and this story as `ready-for-dev` or `in-progress` before implementation.
  - [x] Capture `git rev-parse --short HEAD`, `git status --short`, and `git submodule status` at start. Do not run recursive submodule commands.
  - [x] Build a final Epic 7 evidence table covering each story: touched repositories, touched project/package references, validation lanes, rollback set, residual blocker, and deferred cleanup.

- [x] Pin and document final repository/package state (AC: 2, 7, 8)
  - [x] Record root submodule commits for `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Memories`, `Hexalith.Tenants`, and any other root submodule shown by `git submodule status`.
  - [x] Distinguish clean gitlinks from existing `+` drift. If a gitlink is intentionally part of the release, document the before/after commit and owner validation; if it is unrelated drift, leave it unstaged and record it as a release blocker or pre-release hygiene task.
  - [x] Verify Central Package Management remains intact: no `.csproj` `Version=` attributes, package versions only in `Directory.Packages.props`, and no classic `.sln` introduced.
  - [x] If package versions changed, record exact `<PackageVersion>` entries and package/API compatibility evidence.

- [x] Produce final readiness and rollback artifact (AC: 1, 2, 3, 7, 8, 9, 10)
  - [x] Create or update a single final readiness document under `_bmad-output/planning-artifacts/`, recommended name `epic-7-final-readiness-2026-06-29.md`.
  - [x] Include final submodule commits/package versions, exact validation commands/results, public compatibility notes, production KMS reminder, and the final rollback set.
  - [x] Include a deferred cleanup table with owner, file/surface, reason for deferral, required proof, and suggested follow-up story.
  - [x] State explicitly that Epic 7 remains post-MVP platform maintenance and adds no PRD FR coverage.

- [x] Review cleanup candidates without breaking rollback (AC: 3, 4, 5, 6)
  - [x] Projection: inspect `LocalPartyProjectionPlatformAdapter`, `PartyProjectionPlatformAdapterMode`, `ProjectionOptions`, `EventStorePartyProjectionPlatformAdapter`, `ProjectionRebuildService`, projection actors, and projection tests before deleting anything.
  - [x] Keep `ProjectionRebuildService` unless EventStore `IProjectionRebuildOrchestrator` can fully preserve current DAPR actor-state replay, trusted party-id enumeration, GDPR processing-record behavior, rebuild resume/cancel, and rollback.
  - [x] Keep actor companion sequence keys unless tests prove replay-from-zero idempotency, duplicate/out-of-order safety, erased-party cleanup, and recreated-party behavior without stale high-water marks.
  - [x] Crypto: inspect `EventStorePartyPayloadProtectionAdapter`, `PartyPayloadProtectionService`, `PartiesServiceCollectionExtensions`, key-management services, erasure services, domain/projection redaction fallbacks, and full security harness before deleting or narrowing registrations.
  - [x] Keep `PartyPayloadProtectionService` if it remains the AES-GCM implementation, snapshot handler, redaction helper, or rollback provider for `EventStorePartyPayloadProtectionAdapter`.
  - [x] Search/UI follow-up: either route the `PartiesAdminPortalE2eFixture` private normalization helper to Commons with UI build/test proof, or record it in the deferred cleanup table.

- [x] Validate Parties and owner repositories (AC: 7, 8, 9)
  - [x] Run `git diff --check`.
  - [x] Run `bash scripts/check-no-warning-override.sh`.
  - [x] Run `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false`; if blocked by known submodule drift, record the exact diagnostics and run focused lanes.
  - [x] Run the full direct xUnit v3 assemblies for touched areas, not only focused classes. At minimum consider `Hexalith.Parties.Security.Tests`, `Hexalith.Parties.Tests`, `Hexalith.Parties.Projections.Tests`, `Hexalith.Parties.Client.Tests`, `Hexalith.Parties.Contracts.Tests`, and deploy validation tests when restore/build is available.
  - [x] Run owner-submodule builds/tests for every touched submodule: Commons for service/text helpers, EventStore for projection/security contracts if touched, FrontComposer if UI orchestration is touched, Memories if search integration is touched, and Tenants if tenant integration changes.
  - [x] Run package/API compatibility checks for public Parties contracts/client/UI RCL surfaces.
  - [x] Run deploy/static validation if deployment or readiness artifacts claim deployability: `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/`.
  - [x] If Playwright/e2e evidence is used, run it with the repo's existing `tests/e2e` commands and document any sandbox-only `PLAYWRIGHT_SKIP_WEBSERVER=1` limitation separately from release readiness.

- [x] Update durable docs and sprint status only after evidence is coherent (AC: 8, 9, 10)
  - [x] Update `_bmad-output/project-context.md`, `docs/development-guide.md`, `docs/deployment-security-checklist.md`, or other durable docs only if Story 7 introduced stable implementation rules future agents must follow.
  - [x] Do not update PRD FR coverage for Epic 7; it remains maintenance scope.
  - [x] Move this story through `in-progress`, `review`, and `done` in `sprint-status.yaml` during dev/review workflows while preserving comments and status definitions.
  - [x] Ensure final File List includes story, readiness artifact, sprint status, and every source/test/doc file changed. Do not include unrelated gitlink drift as story work unless intentionally released and validated.

## Dev Notes

### Story Classification

- Epic 7 is post-MVP platform maintenance. It covers no new PRD functional requirements and must not be reported as MVP feature delivery. [Source: `_bmad-output/planning-artifacts/epics.md#Epic-7-Platform-Alignment---adopt-Commons/EventStore-Class-B`]
- Story 7.8 runs last in the approved sequence: `7.1 -> 7.2 -> 7.3 -> 7.4 -> 7.5 -> 7.6 -> 7.7 -> 7.8`. [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Sequencing`]
- Final readiness must record final submodule commits/package versions, exact Parties and owner-submodule lanes, projection parity, crypto harness evidence, public compatibility, and the no-PRD-scope-change confirmation. [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Final-Readiness-Evidence-for-Story-78`]

### Required Source Discovery Results

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, including Epic 7 sequencing and Story 7.8 requirements.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`, `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md`, `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md`, `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md`, and `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md`.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/parties-ui-prd.md`; Story 7.8 adds no PRD functional coverage.
- Loaded persistent facts from `_bmad-output/project-context.md`.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md` and summary evidence from stories 7.1-7.6.
- Loaded current sprint status and current root submodule status. At story-creation time, `git submodule status` showed `+` drift for Builds, EventStore, FrontComposer, Memories, PolymorphicSerializations, and Tenants; Commons was at `e0819e3b8cab4f55408b7e8c1974d3a84b6eac6b`. Treat this as evidence to re-check, not as permission to stage unrelated gitlinks.

### Architecture and Release Guardrails

- Adapter-first is binding: introduce or consume adapters, prove old/new parity, and delete local infrastructure only after compatibility and rollback evidence exist. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-1---Adapter-First-Migration`]
- If a shared API is missing, it must land additively in the owning submodule first, then the root pointer or package reference is updated before Parties consumes it. Do not consume unapproved local-only APIs. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-5---Release-Sequencing-Before-References`]
- Every implementation story needs a named rollback: DI/adapter switch, package/submodule pointer rollback, dual-read comparison, or deferred deletion. Rollback must preserve data, projection state, gateway routes, and PII redaction. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-6---Compatibility-And-Rollback-Gates`]
- No story may rely on deleting local code as its rollback mechanism. Local code is kept until the replacement path is proven and Story 7.8 records readiness. [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Rollback-Sets`]

### Previous Story Intelligence

- Story 7.2 adopted Commons service defaults, correlation, ProblemDetails, and paging behind Parties facades. Rollback restores local wrappers/DI, Commons references/pointer, and internal adapters; no state or public contract migration was introduced. Broad lanes were partly blocked by sandbox/network or unrelated referenced-repo failures. [Source: `_bmad-output/implementation-artifacts/7-2-commons-service-defaults-correlation-problemdetails-and-paging.md#Completion-Notes-List`]
- Story 7.3 adopted Commons pure text helpers for search and left one medium follow-up: `PartiesAdminPortalE2eFixture` still has a private diacritic normalization helper because routing it to Commons changes the UI build surface. [Source: `_bmad-output/implementation-artifacts/7-3-search-normalization-and-frontcomposer-ui-orchestration-convergence.md#Senior-Developer-Review-AI`]
- Story 7.4 added the projection adapter boundary and did not delete local projection infrastructure. EventStore submodule source was not modified by that implementation. [Source: `_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md#Completion-Notes-List`]
- Story 7.5 made EventStore rebuild checkpoints authoritative in EventStore adapter mode, but `ProjectionRebuildService`, actor companion sequence keys, `LocalPartyProjectionPlatformAdapter`, and `PartyProjectionPlatformAdapterMode.Local` remain intentionally retained. Full adoption of EventStore rebuild orchestration is deferred. [Source: `_bmad-output/implementation-artifacts/7-5-projection-checkpoint-rebuild-migration-and-local-code-removal.md#Completion-Notes-List`]
- Story 7.6 created the accepted crypto/key-management split ADR and full security harness. A review found a missed no-leak path and a test regression, then fixed both; the full `Hexalith.Parties.Security.Tests` assembly passed 154 tests after review. Use full assemblies for Story 7.8 evidence, not only focused subsets. [Source: `_bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md#Senior-Developer-Review-AI`]
- Story 7.7 migrated the active payload protection path to `EventStorePartyPayloadProtectionAdapter` while keeping `PartyPayloadProtectionService` as the concrete AES-GCM implementation and rollback provider. A review fixed an unbounded stack allocation in the adapter and the full security assembly passed 165 tests. Broad host/main tests remained blocked by the same pre-existing submodule reference drift. [Source: `_bmad-output/implementation-artifacts/7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md#Senior-Developer-Review-AI`]

### Current Files Being Modified - Required Reading

Read each target completely before editing. The items below are likely cleanup/readiness targets, not automatic deletion targets.

- `_bmad-output/implementation-artifacts/sprint-status.yaml` (UPDATE)
  - Current state: Epic 7 is `in-progress`; story `7-8-release-rollback-cleanup-and-readiness-gate` was `backlog` when this story was created and should be moved by workflow status transitions.
  - Preserve: all comments, status definitions, story order, and unrelated epic statuses.

- `_bmad-output/planning-artifacts/epic-7-final-readiness-2026-06-29.md` (CREATE recommended)
  - Purpose: final readiness evidence, rollback set, submodule/package pin table, validation matrix, public compatibility notes, and deferred cleanup table.
  - Preserve: this is evidence, not a PRD feature claim.

- `Directory.Build.props`, `Directory.Packages.props`, and `Hexalith.Parties.slnx` (READ; UPDATE only with evidence)
  - Current state: root properties include EventStore, Tenants, Memories, Commons, and FrontComposer references; Central Package Management is enabled; package versions live in `Directory.Packages.props`.
  - Preserve: no package `Version=` in `.csproj`, no classic `.sln`, no recursive submodule assumptions.

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (READ/UPDATE if cleanup changes DI)
  - Current state: registers `PartyPayloadProtectionService`, active `EventStorePartyPayloadProtectionAdapter`, `LocalPartyProjectionPlatformAdapter`, `EventStorePartyProjectionPlatformAdapter`, and adapter-mode selection through `ProjectionOptions.PlatformAdapterMode`.
  - Preserve: gateway/DAPR assumptions, projection-side tenant access only, GDPR services, search fallback, and rollback registrations unless final evidence proves removal is safe.

- `src/Hexalith.Parties.Projections/Services/LocalPartyProjectionPlatformAdapter.cs` and `src/Hexalith.Parties.Projections/Configuration/PartyProjectionPlatformAdapterMode.cs` (DELETE only if proven)
  - Current state: local adapter reads/writes legacy DAPR actor rebuild checkpoint state and maps freshness; `Local` mode is an explicit emergency rollback path.
  - Preserve or defer if rollback still depends on it.

- `src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs` and `src/Hexalith.Parties.Projections/Services/PartyProjectionPlatformFreshness.cs` (READ/UPDATE if projection finalization changes)
  - Current state: EventStore adapter uses EventStore checkpoint/rebuild stores, maps EventStore freshness back to public Parties freshness metadata, and writes terminal rebuild checkpoints.
  - Preserve: public `ProjectionFreshnessMetadata`, stale/degraded fallback, idempotent replay, and fail-loud checkpoint save behavior.

- `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs` and projection actors/tests (READ before cleanup)
  - Current state from Story 7.5: still required for local stream replay mechanics, trusted party-id enumeration, and GDPR processing-record behavior.
  - Preserve unless EventStore orchestrator replacement is proven end to end.

- `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs` and `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs` (READ/UPDATE only with crypto evidence)
  - Current state: adapter emits EventStore protected metadata and precise typed unreadable outcomes; inner service still owns AES-GCM JSON field envelopes, snapshots, redaction helper, key reads, and legacy format compatibility.
  - Preserve: `json+pdenc-v1`, `json-redacted`, legacy missing/unprotected metadata reads, key zeroing, redaction fallback, and no-leak diagnostics.

- `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs`, `src/Hexalith.Parties/Search/SemanticPartySearchProvider.cs`, `src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs`, and `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` (READ if addressing search/UI cleanup)
  - Current state: production search providers delegate to Commons `StringHelper` through internal static shims; the UI/e2e fixture has a deferred private normalization helper.
  - Preserve: local fallback search when Memories is absent, deterministic ordering, page-size guards, and admin search behavior.

### Testing and Validation Guidance

Use focused checks to isolate failures, but final readiness needs broad evidence or exact blockers.

- `git diff --check`
- `bash scripts/check-no-warning-override.sh`
- `git submodule status`
- `git status --short`
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false`
- `scripts/test.ps1 -Lane unit` if the wrapper is reliable in the environment; otherwise run direct xUnit v3 assemblies.
- Direct xUnit v3 full assemblies for touched areas, especially `Hexalith.Parties.Security.Tests` because Story 7.6 proved focused subsets can miss regressions.
- Owner-submodule test lanes for each touched submodule.
- `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/` when deploy/readiness claims are made.
- Package/API compatibility tests for public packages and UI RCLs when references or public surfaces change.

### Latest Technical Information

- No external framework or package upgrade is required for Story 7.8. Use the pinned local stack from project context: .NET SDK `10.0.301`, `net10.0`, Dapr `1.18.4`, Aspire `13.4.6`, xUnit v3, Shouldly, NSubstitute, and root project references under `references/`.
- Do not browse for or adopt newer package versions as part of this final gate unless a release owner explicitly changes the strategy. This story closes the approved Epic 7 project-reference migration; it is not a dependency-upgrade story.

### Rollback Plan

- Utility rollback: restore local wrappers/DI or revert Commons references/pointer; public `PagedResult<T>`, correlation, ProblemDetails, and health behavior remain compatible.
- Search rollback: restore local normalization/similarity shim bodies and revert Commons helper usage if needed; keep Memories optional.
- Projection rollback: set `Parties:Projections:PlatformAdapterMode=Local` while retained, or revert deletion commit/submodule pointer if cleanup removed code with approval.
- Crypto rollback: restore the previous `IEventPayloadProtectionService -> PartyPayloadProtectionService` registration, or roll back the provider/submodule pointer if one was intentionally changed. Existing protected payloads must remain readable or safely unreadable.
- Release rollback: pin and document the pre-release commit/package set. If any final validation gate fails, do not delete rollback paths; record the blocker and leave Epic 7 cleanup deferred.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.8-Release-rollback-cleanup-and-readiness-gate`]
- [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.8---Release-Rollback-Cleanup-And-Readiness-Gate`]
- [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Final-Readiness-Evidence-for-Story-78`]
- [Source: `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md#Rollback-Strategy`]
- [Source: `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md#Rollback-for-77`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#Invariants--Rules`]
- [Source: `_bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas`]
- [Source: `_bmad-output/implementation-artifacts/7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md#Senior-Developer-Review-AI`]
- [Source: `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Parties.Projections/Services/LocalPartyProjectionPlatformAdapter.cs`]
- [Source: `src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs`]
- [Source: `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`]
- [Source: `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`]

## Validation Summary

- Source discovery loaded project context facts, sprint status, canonical Epic 7 story scope, Epic 7 architecture spine, implementation plan, release/rollback plan, platform-target ADR, crypto split ADR, PRD maintenance-scope confirmation, current root submodule status, recent git history, previous story evidence from 7.1-7.7, and current high-risk cleanup files.
- Checklist fixes applied before finalizing: made final pinning and rollback evidence concrete, prevented automatic deletion of rollback-only projection and crypto code, carried forward Story 7.3 deferred UI normalization work, required full security assembly evidence, required exact blocker recording for known submodule drift, and scoped documentation updates to durable rules only.
- Latest-technology review found no external dependency upgrade requirement. This story relies on pinned local .NET 10, current root submodule sources, accepted Epic 7 architecture, and previous story evidence.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `git rev-parse HEAD` -> `34f7a941997712955dab2d015cf57cdcfda46252`; `git rev-parse --short HEAD` -> `34f7a94`.
- `git status --short` and `git submodule status` captured existing sprint/story artifact changes and root gitlink drift in Builds, EventStore, FrontComposer, Memories, PolymorphicSerializations, and Tenants.
- `git ls-tree HEAD references/*`, `git -C references/<repo> describe --always --tags`, and `git submodule foreach --quiet 'printf "%s " "$name"; git status --short | wc -l'` used to build final submodule pin table.
- `rg -n '<PackageReference[^>]*Version=|Version=' --glob '*.csproj'` -> no package version attributes found in project files.
- `find . -maxdepth 2 -name '*.sln'` -> no classic `.sln` found.
- `git diff --check` -> pass.
- `bash scripts/check-no-warning-override.sh` -> pass.
- `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/` -> pass, 0 findings.
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false` -> fail, 115 referenced graph errors in EventStore/Tenants symbols; recorded as release blocker.
- Focused builds for security, projections, deploy validation, UI, client, contracts, and Commons owner projects were run; passing and blocked lanes are recorded in the readiness artifact.
- Direct xUnit v3 assemblies run: Security.Tests 165/165 pass; Projections.Tests 139/139 pass; Parties.Tests 532/532 pass; Client.Tests 125/127 pass with 2 NuGet-network package failures; Contracts.Tests 130/135 pass with 5 NuGet-network package failures; UI.Tests 323/324 pass with 1 navigation accessibility assertion failure; DeployValidation.Tests assembly interrupted after live-cluster skips and no final summary.

### Completion Notes List

- Final readiness evidence was created at `_bmad-output/planning-artifacts/epic-7-final-readiness-2026-06-29.md`.
- Story 7.8 performed no production source deletion. Projection local adapter/mode, actor companion sequence keys, `ProjectionRebuildService`, and Parties crypto/key-management implementation remain preserved because deletion-safe parity evidence is incomplete.
- Epic 7 remains post-MVP platform maintenance and adds no PRD functional requirement coverage.
- Release is not green from this workspace. Blockers are full solution build failure caused by current referenced EventStore/Tenants graph drift, one UI accessibility/navigation test failure, package compatibility tests blocked by NuGet repository signature network denial, deploy validation direct assembly hang/interruption, and unvalidated root gitlink drift.
- Durable project context and PRD FR coverage were not changed because Story 7.8 introduced no new stable implementation rules.

### File List

- `_bmad-output/implementation-artifacts/7-8-release-rollback-cleanup-and-readiness-gate.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/planning-artifacts/epic-7-final-readiness-2026-06-29.md`
- `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts`
- `tests/e2e/specs/story-7-8-release-readiness.spec.ts`

### Change Log

- 2026-06-29: Created final Epic 7 readiness artifact, recorded validation matrix and release blockers, preserved rollback-only cleanup candidates, updated story tasks/Dev Agent Record/File List, and moved sprint/story status to review.
- 2026-06-29: Added QA-generated Playwright release-readiness artifact tests and BMAD test automation summary.
- 2026-06-29: Updated stale Story 7.4 E2E artifact assertions discovered during the broader Epic 7 artifact-only test pass.
- 2026-06-29: Senior Developer Review (auto-fix): fixed self-invalidating status assertions in `story-7-8-release-readiness.spec.ts`, advanced story/sprint status to `done`, updated readiness Story Evidence 7.8 row to Done.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-29 · **Outcome:** Approved (auto-fix applied)

### Scope

Adversarial review of Story 7.8 deliverables. Story performed no production source changes; reviewable code artifacts are the two Playwright artifact-validation specs (`story-7-8-release-readiness.spec.ts`, modified `story-7-4-projection-platform-compatibility.spec.ts`). All other changed files are `_bmad-output/` evidence, excluded from code review per the review charter.

### Verification performed

- **File List vs git reality:** Matches. Submodule gitlink drift (`references/Hexalith.Builds/EventStore/FrontComposer/Memories/PolymorphicSerializations/Tenants`) is correctly excluded from File List and documented as a deferred release blocker, not staged as story work.
- **Repository/Package State table:** All 8 recorded submodule hashes (HEAD + working tree) verified exact against `git ls-tree HEAD` and `git submodule status`.
- **Cheap validation claims re-run and confirmed:** `git diff --check` → Pass; `scripts/check-no-warning-override.sh` → Pass (exact message match); no `<PackageReference Version=>` in csproj; no classic `.sln`.
- **e2e spec string assertions:** Every source-file probe in `story-7-8-release-readiness.spec.ts` (`EnsureTrustedPartyIdsAvailable`, `GetProcessingRecordsAsync`, `PartyProjectionPlatformAdapterMode.Local`, `EventStorePartyPayloadProtectionAdapter`, `PartyPayloadProtectionService inner`, `ProtectedSerializationFormat`, `StripDiacritics`, etc.) verified present in the real source.
- **Story 7.4 spec rename:** Updated assertions (`...WithoutLocalCheckpointWriteAsync`, `...WithoutLocalCleanupAsync`) match the renamed methods in `ProjectionPlatformAdapterTests.cs`; old names confirmed removed.
- **Spec execution:** Both specs run green via `PLAYWRIGHT_SKIP_WEBSERVER=1` — 11/11 passed before and after the fix.
- **AC trace:** All 10 acceptance criteria satisfied through readiness evidence (Decision, Story Evidence, Repository/Package State, Validation Matrix, Public Compatibility, Cleanup Decisions, Rollback Set, KMS, Release Blockers).

### Findings

- **HIGH (fixed):** `story-7-8-release-readiness.spec.ts:64-65` hard-asserted `Status: review` and sprint `7-8-…: review`. This review workflow's terminal step advances the story to `done`, so the spec was self-invalidating — it would fail the instant its own story completed review. Fixed to accept `/Status:\s+(review|done)/` and the equivalent sprint pattern, matching the `Status: done` precedent already used in `story-7-4-…spec.ts`. Re-ran: 11/11 green at `done` status.
- **LOW (informational, no fix):** `_bmad-output/story-automator/orchestration-6-20260629-064725.md` is git-modified but absent from the File List. It is transient automator session state in an excluded folder, not story deliverable; correctly omitted.

### Decision

0 Critical issues. The single HIGH issue is fixed and re-verified. Release itself remains intentionally blocked (5 documented blockers) — that is the honest readiness outcome this gate story records, not a story defect. Story → `done`.
