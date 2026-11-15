---
title: 'Clean published catalog verification workspaces on exit'
type: 'bugfix'
created: '2026-11-14'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Completed local Parties verification jobs retain full build workspaces under `artifacts`, accumulating about 12.6 GiB. The user approved temporary job workspaces, durable reports, and selective cleanup of completed workspaces.

**Approach:** Add a focused published-catalog verification runner and use it in the existing `published-catalog-runtime` CI job. Keep the test entry build, existing runtime preparation, and the existing minimum-one-test/no-skips gate inside one private temporary directory. Explicitly redirect every build/test to its artifacts path and pass the resulting prepared-runtime manifest. Preserve logs, test results, and JSON receipts in a durable results directory before deleting the workspace on success, failure, or ordinary cancellation. Keep source discovery working for external outputs by adding a current-working-directory fallback to the existing AppHost and mTLS fixture marker searches. Preserve preparation/receipt validation and unrelated pending native UI changes. Validate the runner with synthetic commands exercising success, failure, cancellation, and output routing; wire these checks into CI. Remove only generated `bin`/`obj` directories from completed retained workspaces after checking active use, preserving reports, historical source-path records, and runtime-profile metadata.

</frozen-after-approval>

## Implementation Notes

- User approval is the preceding `apply` instruction; this bounded work continues around existing unrelated changes without staging, committing, or resetting them.
- Reuse `scripts/prepare-aspire-runtime-hosts.sh --artifacts-path`; its receipt validation remains authoritative. Set `Parties__Testing__PreparedRuntimeManifest` explicitly. Run from the original repository root.
- `dotnet test --help` confirms SDK11 supports `--artifacts-path` with project execution, preserving the existing MTP test selection and minimum-test/no-skips gate.
- Existing artifact workspaces are historical, dated October 7 or November 9. Retain their `runtime-profile` files and all evidence records; JSON path references remain historical observations after build cleanup.
- Added the complete driver in `scripts/verify-catalog-runtime.py` and seven process-level regressions in `scripts/test_verify_catalog_runtime.py`; the existing runtime CI job invokes both. Local defaults remain Debug, with CI explicitly selecting Release. Build, preparation, and testing share the same temporary artifacts root and retain existing hosted-SDK, minimum-test, and no-skips gates.
- Real SDK11 MTP launches the test worker with cwd at the output directory. Both repository marker resolvers now also validate the explicit `PARTIES_TEST_REPOSITORY_ROOT` supplied by the driver; their original assembly/cwd fallbacks remain. Actual-source .NET 11 harnesses passed with zero warnings/errors, and independent review verified the real MTP launch from external output.
- Native fixtures deliberately preserve ownership state on failed shutdown. Their temporary files now use durable `TestResults/.../runtime-custody`, outside disposable build storage; nonempty custody survives for cleanup retry and CI uploads exclude it. Empty successful custody is removed.
- Ten historical inactive workspaces had 20 generated `bin`/`obj` directories removed, reclaiming approximately 10.63 GiB. All 157 files outside those outputs retained their inode, size, and modification time. Cleanup details are in the ignored local `TestResults/artifact-cleanup-20261114T093558.110682Z/cleanup.json`; original receipts and source-path observations were not rewritten.
- Validation passed: `run-with-temp python3 -B -m unittest discover -s scripts -p 'test_verify_catalog_runtime.py' -v` (7 tests, including failures in all three phases, cancellation with a resistant descendant, exclusive result ownership, external test cwd, Release, and both hosted build properties); `actionlint .github/workflows/ci.yml`; focused `git diff --check`; Debug build of `Hexalith.Parties.Ci.Tests`; and its direct-DLL `CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes` test (1 passed). Full Docker/Dapr topology execution was not performed.

## Review Triage Log

- **high, patched:** MTP overrides the caller cwd, defeating implicit repository lookup. Verified with a real SDK11 worker; explicit validated source-root configuration fixes both marker searches without weakening source/receipt validation.
- **high, patched:** A cancellation-resistant grandchild survived after the group leader exited. The driver now requests managed cancellation, kills remaining owned group members, and waits for non-zombie quiescence before build cleanup. Both regression coverage and the independent original probe passed.
- **high, patched:** Removing the parent workspace erased intentionally retained topology custody. Native test temporary storage now lives outside build cleanup in the durable result directory and is excluded from CI report uploads.
- **medium, patched:** Concurrent explicit result destinations could both pass the empty-directory check. An exclusive owner file reserves the destination before logs; a deterministic pre-log competing-invocation regression passes.
- **medium, patched:** Synthetic tests omitted Release and required published-build properties. Both are now checked on actual recorded child arguments.
- Independent reviewer confirmed no remaining findings in the bounded fixes and explicitly did not exercise the full topology.
