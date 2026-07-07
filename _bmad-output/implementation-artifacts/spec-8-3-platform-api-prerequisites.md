---
title: '8.3 Platform API prerequisites'
type: 'chore'
created: '2026-07-07T10:50:18+02:00'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
baseline_revision: '12c56f78274f5acba6385594f40652c071890540'
final_revision: 'edfe6762a7ff45976c19d09e0ae281d1bb94b4f8'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md'
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06.md'
  - '{project-root}/fable_changes.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Epic 8 later stories cannot safely delete Parties-owned platform plumbing until the replacement APIs are either approved as additive shared surfaces or explicitly proven already available. Without an evidence-backed prerequisite gate, later migrations could consume unapproved checked-out submodule code and lose rollback clarity.

**Approach:** Produce a Story 8.3 prerequisite matrix that maps each required platform surface to owner repo, current API/evidence, readiness status, proof required, and dependent Epic 8 stories. Add a focused fitness test so the matrix keeps covering the approved prerequisite set and remains a no-migration gate.

## Boundaries & Constraints

**Always:** Treat this as prerequisite evidence and guardrail work; cover EventStore projection/query SDK support, DataProtection, client envelopes/freshness/error codes, tenant claims transformation, Aspire publish helpers, FrontComposer UI primitives, Commons HTTP helpers, and Builds shared props/targets; preserve Story 8.1/8.2 residual blocker wording.

**Block If:** Implementing the matrix requires editing submodule source, changing EventStore/Commons/FrontComposer/Builds public APIs, consuming a not-yet-approved platform API from Parties source, or deciding platform ownership where planning artifacts are silent.

**Never:** Do not migrate Parties host/projection/query/security/UI source in this story, delete leaf projects, rewrite AppHost topology, change public package contracts, weaken GDPR crypto policy, or treat Epic 8 as PRD feature delivery.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Already available platform API | Required surface exists in a checked-out owner module with source evidence | Matrix row is `available`, cites the exact owner/API/evidence path, and lists validation command | No source migration is performed |
| Missing or partial platform API | Required surface lacks full parity or owner approval | Matrix row is `needs-additive-api` or `blocked`, names owner, proof required, and dependent stories | Later stories remain blocked until proof is added |
| Unapproved migration attempt | Story 8.3 artifact or test suggests Parties source should consume a checked-out submodule API now | Fitness test fails the matrix/guard wording | Fix artifact wording; do not change production source |

</intent-contract>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- new evidence matrix and dependent-story gate for platform-owner APIs.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- source-level guard that required surfaces, statuses, evidence paths, and no-migration wording remain present.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` -- append Story 8.3 evidence and residual blockers.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- mark Story 8.3 complete after validation.
- `references/Hexalith.EventStore/src/**`, `references/Hexalith.Commons/src/**`, `references/Hexalith.FrontComposer/src/**`, `references/Hexalith.Builds/**`, and `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` -- read-only evidence sources; do not edit.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- create a matrix covering every approved prerequisite surface with owner, status, evidence path, proof required, dependent stories, and no-source-migration decision -- gives later stories a reviewable gate.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- add focused tests for required matrix rows, allowed status vocabulary, evidence files, dependent story coverage, and explicit no-migration wording -- prevents accidental narrowing of the prerequisite gate.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` -- append Story 8.3 commands, evidence, and blockers -- keeps BMAD continuity current.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- update `8-3-platform-api-prerequisites` from backlog to done after validation -- keeps sprint tracking aligned.

**Acceptance Criteria:**
- Given the approved Story 8.3 scope, when the matrix is inspected, then it contains one row for each required surface: EventStore domain-service host, EventStore projection/query SDK, EventStore DataProtection, EventStore client envelopes/freshness/error codes, tenant claims transformation, Aspire publish helpers, FrontComposer UI primitives, Commons HTTP helpers, and Builds shared props/targets.
- Given a row is marked `available`, when its evidence is checked, then the cited source path exists in the appropriate owner module and the row names the validation command or inspection used.
- Given a row is marked `needs-additive-api` or `blocked`, when later Epic 8 stories depend on it, then the matrix names the missing proof, owner, dependent story numbers, and whether Parties must keep the local rollback path.
- Given Story 8.3 completes, when production source diffs are reviewed, then no Parties host, projection, query, security, UI, AppHost, or package migration has started from an unapproved API.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 11: (high 0, medium 7, low 4)
- defer: 0
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Expanded the matrix so fable gaps G1-G12 are explicitly covered, including degraded response, DAPR health checks, G8 client/AppHost helper parity, G11 MCP/deep-link/search probes, and G12 package/source-mode CI.
  - `[medium]` `[patch]` Split EventStore DataProtection cursor/key-ring evidence from the separate payload-protection engine prerequisite so GDPR payload protection is no longer conflated with ASP.NET Data Protection naming.
  - `[medium]` `[patch]` Tightened the review gate and available-row proof wording so source evidence alone never authorizes later migration without release or submodule-pin validation.
  - `[medium]` `[patch]` Removed ambiguous tenant-claims ownership by naming EventStore as the proposed owner and requiring architecture-owner confirmation or redirect before deletion.
  - `[medium]` `[patch]` Strengthened matrix fitness coverage for owner-matching evidence paths and non-empty validation evidence.
  - `[medium]` `[patch]` Changed dependent-story assertions from global coverage to row-specific coverage for required surfaces.
  - `[medium]` `[patch]` Added available-row proof assertions so existing source surfaces still require release, package/source, or submodule-pin proof before consumption.
  - `[low]` `[patch]` Reworded the direct xUnit command as `dotnet ./tests/...dll` to avoid confusion with a nonexistent `dotnet tests` verb.
  - `[low]` `[patch]` Relaxed fitness tests so legitimate future status progress from `needs-additive-api` to `available` does not require changing hard-coded expected statuses.
  - `[low]` `[patch]` Relaxed row-count assertions so new prerequisite rows can be added without weakening required-row coverage.
  - `[low]` `[patch]` Replaced naive markdown table splitting with escaped-pipe-aware parsing and exact column validation.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 11: (high 0, medium 9, low 2)
- defer: 0
- reject: 3: (high 0, medium 0, low 3)
- addressed_findings:
  - `[medium]` `[patch]` Made the added fable-gap rows executable requirements so degraded health checks, payload protection, MCP/search, and package/source-mode CI rows cannot be removed silently.
  - `[medium]` `[patch]` Replaced raw `G1`-`G12` substring checks with exact token matching so `G10`, `G11`, or `G12` cannot satisfy `G1`.
  - `[medium]` `[patch]` Changed dependent-story checks to exact story-id parsing and required every matrix row to name dependent Epic 8 stories.
  - `[medium]` `[patch]` Removed `package/source` as sufficient proof for available rows and tightened the Commons HTTP row to require release or submodule-pin availability.
  - `[medium]` `[patch]` Added explicit owner-segment mapping so unknown or misspelled owners fail instead of bypassing evidence-prefix validation.
  - `[medium]` `[patch]` Normalized evidence paths, rejected absolute/traversal paths, and verified owner or allowed context evidence prefixes after normalization.
  - `[medium]` `[patch]` Required rollback wording for all rows and made the EventStore DataProtection row use explicit rollback-path language.
  - `[medium]` `[patch]` Stopped skipping arbitrary table rows containing `---`; only real markdown separator rows are ignored.
  - `[medium]` `[patch]` Strengthened mixed owner/context evidence validation so owner evidence remains required while Parties rollback evidence stays allowed.
  - `[low]` `[patch]` Replaced the multi-pattern required-surface `rg` verification with a fixed-string loop that checks each required surface independently.
  - `[low]` `[patch]` Moved the `MatrixRow` helper record into its own file to preserve the one-type-per-file rule.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 12: (high 0, medium 12, low 0)
- defer: 0
- reject: 2: (high 0, medium 0, low 2)
- addressed_findings:
  - `[medium]` `[patch]` Narrowed `Hexalith.EventStore.Aspire` evidence-prefix validation to the Aspire package path so unrelated EventStore files cannot satisfy Aspire prerequisite proof.
  - `[medium]` `[patch]` Changed fable-gap coverage from global token presence to row-specific exact token assertions, so `G10`, `G11`, and `G12` cannot satisfy earlier gap IDs and each required row owns its gaps.
  - `[medium]` `[patch]` Tightened available-row proof checks to require explicit `must validate` release or submodule-pin wording and reject negated release/pin phrasing.
  - `[medium]` `[patch]` Tightened rollback checks to require positive `Keep ... rollback path` wording and reject negated rollback phrasing.
  - `[medium]` `[patch]` Added duplicate matrix marker detection so tests cannot silently validate a stale matrix section.
  - `[medium]` `[patch]` Expanded the G7 row to require the public `eventstore:tenant` claim constant plus `AggregateIdentity.IsValid(string)` and `UniqueIdHelper.IsValidUlid(string)` predicate proof, with EventStore and Commons ownership.
  - `[medium]` `[patch]` Downgraded the G8 Aspire helper row from `available` to `needs-additive-api` and required `WithEventStoreJwtAuthentication(audience)` or documented `WithJwtBearerSecurity(..., audience)` replacement plus granular typed-client registration proof.
  - `[medium]` `[patch]` Expanded the G5 payload-protection row with AAD `pdenc-v2`, v1 read support, `IPersonalDataPolicy`, `IErasureStateProvider`, stable persisted format/state/actor/metric constraints, and golden harness migration proof.
  - `[medium]` `[patch]` Replaced the generic G12 owner with actionable Commons and Tenants release ownership and evidence paths.
  - `[medium]` `[patch]` Added validation-evidence token assertions so rows must name expected APIs, symbols, and missing-surface proof rather than only existing files.
  - `[medium]` `[patch]` Added a Story 8.3 revision-range guard that checks the recorded baseline-to-final diff did not modify forbidden production migration paths.
  - `[medium]` `[patch]` Replaced broad `Inspected ...` validation prose in the matrix with reproducible `rg -n -F` symbol checks or explicit missing-surface review notes.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 12: (high 0, medium 12, low 0)
- defer: 0
- reject: 2: (high 0, medium 0, low 2)
- addressed_findings:
  - `[medium]` `[patch]` Changed the no-production-migration guard to inspect the current baseline-to-worktree diff plus untracked files instead of the stale recorded `final_revision`.
  - `[medium]` `[patch]` Made the migration guard fail when the recorded baseline commit is unavailable instead of silently skipping validation.
  - `[medium]` `[patch]` Broadened forbidden migration path coverage to every `src/Hexalith.Parties.*` sibling package and the root package/build metadata files.
  - `[medium]` `[patch]` Added path-boundary validation for allowed context evidence so unrelated similarly named paths cannot satisfy matrix evidence.
  - `[medium]` `[patch]` Tightened dependent-story parsing so malformed story ids with alphanumeric suffixes do not satisfy required coverage.
  - `[medium]` `[patch]` Required exact dependent-story sets for required rows instead of allowing extra unexpected story ids.
  - `[medium]` `[patch]` Added distinct owner evidence checks so nested `Hexalith.EventStore.Aspire` evidence cannot also satisfy the broader `Hexalith.EventStore` owner segment.
  - `[medium]` `[patch]` Changed validation-symbol checks to search only the validation evidence column instead of the full row text.
  - `[medium]` `[patch]` Added executable `rg -n -F` validation command checks for every matrix row and corrected stale Builds and G12 command evidence.
  - `[medium]` `[patch]` Replaced broad degraded-response inspection prose with reproducible `rg` evidence plus an explicit missing-surface note.
  - `[medium]` `[patch]` Added exact `tenant header relay` validation-evidence wording for the MCP/search row.
  - `[medium]` `[patch]` Tightened the review gate so source migration proof must be recorded in the matrix row before migration starts; consuming stories can only add supplementary proof.

## Design Notes

Use a markdown artifact instead of production source changes because the story is an approval/evidence gate. The accompanying fitness test is intentionally about the artifact contract: it makes the prerequisite set executable without asserting that all platform work is complete today.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: test project builds or only a recorded Story 8.1 blocker appears.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- expected: new fitness tests pass.
- `for surface in 'EventStore domain-service host' 'EventStore projection/query SDK' 'EventStore DataProtection' 'EventStore client envelopes/freshness/error codes' 'Tenant claims transformation' 'Aspire publish helpers' 'FrontComposer UI primitives' 'Commons HTTP helpers' 'Builds shared props/targets'; do rg -n -F "$surface" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md >/dev/null || exit 1; done` -- expected: every required surface is present.
- `git diff --check` -- expected: no whitespace or conflict-marker issues.

**Observed 2026-07-07:**
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- pass; 10 passed.
- `for surface in 'EventStore domain-service host' 'EventStore projection/query SDK' 'EventStore DataProtection' 'EventStore client envelopes/freshness/error codes' 'Tenant claims transformation' 'Aspire publish helpers' 'FrontComposer UI primitives' 'Commons HTTP helpers' 'Builds shared props/targets'; do rg -n -F "$surface" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md >/dev/null || exit 1; done` -- pass; every required surface present.
- `git diff --check` -- pass.

## Auto Run Result

Status: done

Summary:
- Created the Story 8.3 platform prerequisite matrix as an evidence and no-migration gate for Epic 8 Stories 8.4-8.10.
- Covered the approved prerequisite surfaces and review-hardened the matrix to explicitly map fable gaps G1-G12.
- Added executable fitness coverage that preserves required rows, requires the added fable-gap rows, allows future status progress and extra rows, validates owner evidence, protects no-migration wording, and catches malformed markdown rows.
- Follow-up review hardened exact gap matching, exact story-id parsing, owner evidence normalization, available-row proof requirements, and rollback wording.
- Fresh follow-up review hardened G5/G7/G8/G12 prerequisite detail, reproducible validation evidence, duplicate marker detection, and the recorded no-production-migration diff guard.
- This follow-up review hardened the no-production-migration guard against stale final revisions, untracked files, omitted Parties sibling packages, broad context prefixes, nested owner-prefix overlap, non-executable evidence prose, and off-matrix proof wording.

Files changed:
- `_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md` -- new BMAD spec, review triage, verification, and run result.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- new platform API prerequisite matrix and review gate.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- matrix fitness tests with executable evidence and current-worktree migration guards.
- `tests/Hexalith.Parties.Tests/FitnessTests/MatrixRow.cs` -- helper record for parsed matrix rows.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` -- appended Story 8.3 verification evidence and residual blockers.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- marked Story 8.3 done.

Review findings breakdown:
- Initial review pass patches applied: 11 total: high 0, medium 7, low 4.
- Follow-up review pass patches applied: 11 total: high 0, medium 9, low 2.
- Fresh follow-up review pass patches applied: 12 total: high 0, medium 12, low 0.
- Current follow-up review pass patches applied: 12 total: high 0, medium 12, low 0.
- Deferred: 0.
- Rejected: 8 low-severity findings across all passes: four workflow-temporary status/finalization concerns, three story-status or matrix-gate semantics concerns already covered by the intent-contract/no-migration language, and one existing `RepositoryRoot` helper false positive.

Follow-up review recommendation: true. This pass made broad guard-test changes across migration-path coverage, current diff validation, owner evidence, executable validation commands, and migration-proof wording.

Verification performed:
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- pass; 10 passed.
- Required-surface fixed-string `rg` loop -- pass; every required matrix surface is checked independently.
- Focused fitness tests cover exact G1-G12 tokens -- pass.
- `git diff --check` -- pass.

Residual risks:
- Full `Hexalith.Parties.Tests` Release source-mode remains blocked by the Story 8.1 `Hexalith.Memories` Release guard.
- Full `Hexalith.Parties.Tests` Debug source-mode still has the five pre-existing tenant-event failures recorded by Stories 8.1 and 8.2.
- Several matrix rows remain `needs-additive-api` or `blocked`; later migration stories must not consume those areas until owner proof and rollback evidence are recorded.
