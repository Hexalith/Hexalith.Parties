---
title: 'DW-47: stabilize projection-fold logger categories'
type: 'refactor'
created: '2026-10-18'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e55e9aaff1bdd67f13486193e651c8283f04ef73'
context:
  - AGENTS.md
  - .editorconfig
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Shared projection-fold diagnostics inherit the calling handler's category. Operators filtering by the responsible fold receive nothing. DW-47 already specifies dedicated categories.

**Approach:** Inject `ILoggerFactory` to create named fold loggers. Detail's processing fold uses `Hexalith.Parties.Projections.Handlers.PartyProcessingActivityFold`; Index uses `Hexalith.Parties.Projections.Handlers.PartySdkProjectionFold`. Keep the Index rebuild summary under its handler category and document operator filter migration.

## Boundaries & Constraints

**Always:** Preserve events 4802–4808, names, templates, fields, levels, exception-type-only logging, checkpoints, and diagnostic frequency. Keep Detail's second walk silent and Index rebuild summaries bounded. Keep direct fold APIs accepting optional `ILogger`.

**Never:** Fall back to handler categories for fold diagnostics. Missing factories omit optional logging without changing projection results. Do not change persisted models, SDK registration, dependencies, gitlinks, search, or other deferred items.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Error handling |
|---|---|---|---|
| Detail live/rebuild/audit | Dropped event and factory | Processing-fold category, one diagnostic | Existing result and audit state |
| Index live/failure | Dropped event and factory | SDK-fold category, one diagnostic | Existing completion/retry result |
| Retry or applied redelivery | Repeated callback/delivery | Existing counts and checkpoint rules | Existing concurrency handling |
| Shared rebuild | Historical drops | Bounded summary 4801 in handler category | Existing fixed counters |
| Missing factory/disabled category | Optional logging unavailable | No fold sink calls; same persisted output | No new failure |

</frozen-after-approval>

## Code Map

All production handler paths below are under `src/Hexalith.Parties.Projections/Handlers/`.

- `PartyDetailSdkProjectionHandler.cs`: passes its optional typed logger to processing folds in live, rebuild, and unresolved-audit paths.
- `PartyIndexSdkProjectionHandler.cs`: passes its logger to unresolved failure and first persistence walks; `WriteRebuildDiagnostics` separately owns summary 4801.
- `PartyProcessingActivityFold.cs`: Detail's sole diagnostic carrier; delegates decoding to the SDK fold. Both folds are static, requiring named categories rather than generic logger arguments.
- `PartySdkProjectionFoldLog.cs`: generated methods already accept `ILogger`; remarks currently promise caller categories.
- `src/Hexalith.Parties/Program.cs`: SDK assembly scanning activates handlers through DI; the host already supplies a factory.
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`: two Index drop tests need factory migration; other typed logger assertions protect summaries. Existing logging/converter tests pin metadata and privacy.
- `README.md`: Projections is host-private. No Detail constructor caller in src, tests, or samples supplies its fourth argument.

## Tasks & Acceptance

Paths relative to the production handler directory above:

- [x] `PartyDetailSdkProjectionHandler.cs`: replace the optional typed logger with optional factory; cache the processing-fold logger and use it on all three existing diagnostic paths.
- [x] `PartyIndexSdkProjectionHandler.cs`: append optional factory and cache the SDK-fold logger for per-event paths. Retain typed logger for 4801.
- [x] `PartyProcessingActivityFold.cs` and `PartySdkProjectionFoldLog.cs`: align documentation with category ownership; preserve fold/generated-method behavior.

Other repository-relative paths:

- [x] `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionCategoryTests.cs`: test rendered categories, DI activation, live/rebuild/audit paths, retries, filtering, absent factory, and retained summary category. Assert persisted results and diagnostic counts. Put additional test types in separate files.
- [x] `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`: migrate only per-event logger arrangement; retain existing assertions.
- [x] `docs/development-guide.md`: document both categories, 4801, filter migration, and manual construction.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`: close only DW-47 after verification and independent review, linking evidence.

Given either handler is resolved by DI, when a dropped event is processed, then diagnostics use its assigned fold category without handler-category copies.

Given filtering or absent logging, when the same delivery executes, then persisted output and result codes agree with enabled logging.

Given retries, redeliveries and rebuilds, when existing diagnostic paths execute, then counts, checkpoint rules, privacy and bounded summaries remain correct.

## Implementation Notes

- Human approved this spec and requested implementation on 2026-10-18. Re-read from disk before accepting approval; contents are unchanged. A separate build-catalog commit has advanced main since planning; preserve it and the original baseline identifier.

- Dispatch route: two constructor seams and operational filter changes warrant a reviewable spec. No unresolved intent gaps or irreversible implementation actions remain.
- Detail's optional fourth argument changes from typed logger to factory; three-argument construction remains valid. Index retains its typed logger and appends a factory. Manual Index construction must supply a factory for fold diagnostics. These public types belong to the documented host-private package.
- Independent investigation confirms these call sites. No code changes or tests have run. Initial working tree was clean on main.
- Aspire baseline: exact Parties AppHost start and describe exited 0; security was Starting and dependent services Waiting. Exact-target stop exited 0. No healthy topology qualification is claimed; the separate Tenants AppHost was untouched.

- Parent diff audit confirms all implementation/documentation tasks and acceptance criteria. The ledger closure task depends on independent review and intentionally remains unchecked until that review finishes.
- Matrix audit: Detail live/audit and Index live/failure are covered by 12 cases in LiveAndUnresolvedAuditUseOnlyAssignedFoldCategoryAsync; Detail rebuild by DetailRebuildUsesProcessingCategoryOnceAndMatchesLiveStateAsync; retries by OptimisticRetriesLogOnceAndPersistExpectedStateAsync (2); shared rebuild by SharedRebuildRetainsBoundedHandlerSummaryCategoryAsync (2); disabled/missing logging by DisabledOrMissingFactoryPreservesResultsAndStateAsync (8). SDK scanning contributes 2 cases. All 27 ran and passed; full projection assembly passed 375/375, no failures/errors/skips/not-run. Build has zero warnings/errors.
- Parent verified all 14 evidence source hashes. Build output paths are normalized to repository-relative paths for retained evidence. The independent build-catalog commit 5efaae7f0120439bc8d942bbc227dabdeefa56d8 appears in the required original-baseline diff but is already committed and is not part of this staged implementation.

- Final completion: implementation and all task checks are complete. The earlier Code Map/initial notes describe the planning baseline; this is the current status. Three review layers completed. Review patches 2, 3 and 6 add selective enablement, optional rebuild logging comparisons and controlled SDK activation configuration. Parent final verification passes with zero build warnings/errors, 31/31 category cases and 379/379 full projection tests without failures/errors/skips/not-run. Final XML records every executed matrix case; Detail rebuild now contributes three cases and selective enabling contributes two.
- Review finding 7 is completed by the final base-relative CRLF-aware diff check, covering staged changes and retained evidence. Original and post-review checks remain separately recorded. All implementation source hashes are refreshed in final-validation.json. DW-47 alone is closed; no new deferred work was added. No push.

## Spec Change Log

## Review Triage Log

All three review layers completed. Edge Case Hunter returned no findings; Verification Gap Reviewer found no verification gaps and independently ran the 27 category cases. Blind Hunter returned these eight findings, each triaged independently:

1. **low / rejected — missing 4804 handler row:** the new class covers six IDs; existing PartySdkProjectionLoggingTests already exercises ambiguous resolution and metadata using a temporary resolver-cache seed. Both IDs 4803 and 4804 pass through the same already-tested injected logger. No real duplicate short-name event exists in current Contracts. Repeating reflective cache mutation across another class adds complexity for a rare state without covering another logger handoff.
2. **low / patch — selective enablement:** current tests disable a category but do not demonstrate the common operator configuration with only that fold category enabled. Add both handler cases using a real factory with default None and the assigned fold enabled.
3. **low / patch — rebuild optional logging:** Detail rebuild's enabled case is covered, while disabled/missing comparisons only exercise live projection. Extend rebuild comparisons to prove unchanged persisted results with filtered or omitted factory.
4. **low / rejected — changed retry snapshot:** the new fixture injects a failed save without changing the snapshot. Its claim is callback suppression; full-suite IndexHandler_ConcurrencyRetryRefoldsAndPreservesUnrelatedEntryAsync, IndexHandler_OptimisticRetryPublishesSuccessfulCanonicalProvenanceAsync and IndexHandler_RetrySnapshotGapStopsBeforeSecondPersistenceAttemptAsync already protect fresh-snapshot behavior. No concurrency/checkpoint logic changed. Extending this logger fixture with another writer model exceeds a simple correction for negligible extra coverage.
5. **low / rejected — batch fixture concurrency:** the fixture records persisted operation contents but does not implement the full batch concurrency protocol. Existing batch/recovery coverage owns that protocol and ran in the full suite; this change only replaces logger arguments. Duplicating that protocol in the category fixture would add complexity without exercising another changed runtime branch.
6. **low / patch — ambient activation configuration:** WebApplication.CreateBuilder loads ambient configuration and logging rules. Clear its configuration sources before SDK registration in the activation test to keep category expectations deterministic; do not mutate process environment.
7. **low / patch — staged whitespace:** the original gate accurately checked unstaged changes when it ran, but final evidence must also cover staging and the spec/evidence additions. Parent will run and record a CRLF-aware diff check against the implementation base after final staging.
8. **low / rejected — planning statements in spec:** Code Map and initial implementation notes preserve pre-implementation investigation; later notes explicitly record checked tasks and passing verification. Rewriting this build's spec as a review fix is rejected by the workflow and would discard append-only history. Final completion notes will identify the current state.

## Verification

Build each affected test project in Debug/source mode with `-m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`; require zero warnings/errors.

Run the new category class first, then the full projection test assembly directly with xUnit v3 single-dash options. Preserve full metadata/privacy/rebuild coverage and require no failures, errors, or skips. Verify host DI activation against the actual SDK registration. Run `bash scripts/check-no-warning-override.sh` and CRLF-aware whitespace checks. Retain exact commands, outputs, counts and source hashes in `_bmad-output/implementation-artifacts/tests/dw-47-stable-fold-logger-categories-2026-10-18/` before ledger closure.
