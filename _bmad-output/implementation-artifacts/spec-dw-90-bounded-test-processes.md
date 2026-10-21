---
title: 'DW-90: Consolidate bounded repository test helpers'
type: 'refactor'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '24ae57ea2386702ca9e7368e6b1f728b8956ca62'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-90 records duplicated repository/process helpers in the Epic 8, platform prerequisite and documentation fitness tests. Some Git calls drain pipes sequentially or wait without a deadline, so a verbose or stalled child can block the test lane indefinitely.

**Approach:** Reuse the existing shared package-test process runner for repository fitness checks, with literal argument lists, concurrent output capture, a shared execution deadline and bounded termination cleanup. Preserve strict versus optional Git failure behavior, untrimmed output and the existing five-minute MSBuild allowance. Consolidate file reads on RepositoryRoot, cover process failure and timeout behavior, and close DW-90 after independent review and verification. Commit the completed work and push main while preserving the pre-existing ledger edits.

</frozen-after-approval>

## Implementation Notes

- Selected DW-90 as the next item after completed DW-88/DW-89. Freeform work has no sprint key. User authorized proceeding with the dirty ledger files; their initial contents are saved outside the repository so staging can preserve unrelated edits.
- Small test-only refactor, no intent gaps, irreversible data operation, dependency change or public API. The oneshot route applies.
- Code map: tests/PackageTests.Shared/PackageTestProcess.cs and PackageTestProcessResult.cs already linked into Contracts/Client tests; extend the runner with token-list support and retain the string overload. Link those files into Parties.Tests and use a local RepositoryTestProcess adapter for RunGit/TryRunGit/RunProcess. Move duplicated Read to FitnessTests/RepositoryRoot.cs. Replace the unbounded GitObjectExists and adjacent IdentifierHygieneFitnessTests process call as part of the same consolidation.
- Verification: shared runner regression tests in Contracts.Tests (large dual pipes, token fidelity, nonzero/start failure, timeout with open/closed pipes, child termination); repository adapters in Parties.Tests (Git success, missing revision and strict diagnostics); affected fitness classes and complete affected test assemblies. Retain exact commands/results in tests/dw-90-bounded-test-processes-2026-10-21/.
- Startup baseline: isolated no-build Aspire start succeeded and parties became healthy; describe captured resource state; the exact AppHost stopped successfully before builds. This is startup evidence, not full topology qualification.

- Initial Debug builds of service, Contracts and Client test projects pass with zero warnings/errors. Focused fitness checks pass 54/54; process checks pass 9/9; complete Contracts passes 206/206; complete Client passes 330/330. Complete service passes 2167/2168; its sole failure is the existing dependency receipt expecting Builds fff4a212180d8cd1c5c0759655a9bc4fb6963c45 while baseline HEAD commits 17d0c6ffec5fb3c8d8058e67cb85644460cb5256. The failing method and receipt dictionaries are identical to baseline; no dependency receipt or gate is changed.
- Source launcher exits 2/HXR011 because HEXALITH_DAPR_HOME is unconfigured; focused Debug builds use the documented selected-package fallback.

- Independent review configured only Blind Hunter; no configured layers skipped. Five findings were checked and patched: preserve the post-exit capture cap, retain invocation context, test a shared deadline across stream/exit phases, avoid adding Python to service tests, and cover nonzero Git stdout.
- The unchanged receipt failure was reproduced with the complete original PlatformApiPrerequisitesTests.cs (including original helpers) from 24ae57ea2386702ca9e7368e6b1f728b8956ca62: original Debug build passes; the focused receipt test fails with the identical Builds SHA mismatch. Candidate source was restored and rebuilt. This is independent baseline evidence, not a waived gate.

## Review Triage Log

- Medium, patch: shared runner waited for pipes under the full execution timeout after direct-child exit, losing the prior capture cap. Wait for child exit while draining both streams, then capture under min(remaining execution budget, configured capture limit). The inherited-pipe regression passes and cleans its fixture-owned descendant.
- Medium, patch: migrating Platform removed argument context from timeout errors. Keep the executable/timeout prefix and append the literal invocation arguments; the phased-timeout test verifies the script context.
- Medium, patch: previous open/closed-pipe tests did not distinguish separate full phase deadlines. Add a child that closes both streams after two seconds but never exits, under one three-second execution allowance and bounded cleanup.
- Medium, patch: the new adapter argument test introduced Python into service tests. Use the already-required Git executable and command-scoped config instead. Existing package process tests retain their established Python prerequisite.
- Low, patch: optional Git failure coverage did not assert nonempty stdout. Add rev-parse of HEAD plus an absent revision and assert the exact emitted text alongside false.
- Medium, defer: a descendant that inherits pipes after its direct parent exits can outlive the runner; the original shared runner has the same limitation. The new regression owns explicit descendant cleanup. General orphan containment requires an independently qualified process-group/job strategy, beyond consolidation; record that work without claiming tree cleanup after parent exit.

- Follow-up independent review confirms all five corrections and reports no remaining concrete introduced defects. The pre-existing orphan containment limitation is deferred with baseline evidence.

- Final reviewed-source validation: all three Debug builds pass with zero warnings/errors; process regressions pass 11/11; affected fitness subset passes 54/54; complete Contracts passes 208/208; complete Client passes 330/330; service passes 2167/2168 with the identical baseline-only dependency receipt failure, no skips/errors/not-run. Warning/submodule guard and CRLF-aware whitespace checks pass. Commit message validates with locked commitlint 21.2.2 (zero problems/warnings). Pre-existing ledger edits are excluded from staging.
