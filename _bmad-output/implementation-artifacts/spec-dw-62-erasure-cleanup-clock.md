---
title: 'DW-62 erasure cleanup clock'
type: 'bugfix'
created: '2026-10-19'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-62 remains open: registered erasure cleanup delegates and the verification service stamp results with wall-clock time instead of the host's registered `TimeProvider`. Verification certificates inherit the report timestamp, so a host-supplied clock does not consistently control cleanup evidence.

**Approach:** Use the registered clock for all seven cleanup-result timestamps and inject it into `ErasureVerificationService` for unexpected-failure results and final reports. Read time when each result is produced, preserve timestamps returned by delegates, and retain the existing two-argument public constructor using `TimeProvider.System`. Move the existing delegate to its own file to follow the single-type rule. Add deterministic service and DI regressions for successful, failed and advancing-clock cleanup. Resolve DW-62 after validation and independent review; key-destruction timestamps, verification semantics, persistence, wire contracts and dependencies retain their existing behavior.

</frozen-after-approval>

## Implementation Notes

- Route assessment: no unresolved intent choices or irreversible actions; small local change to two production files, the moved delegate and two existing test classes. `AddParties` already registers an overridable `TimeProvider.System`; reuse it. No epic story key applies.
- Code map: `PartiesServiceCollectionExtensions.AddParties` composes five cleanup delegates with seven timestamp branches. `ErasureVerificationService.VerifyErasureAsync` adds failure and final report timestamps. `PartyDomainProcessor` copies report time into the final certificate; initial key-destruction certificates are separate from cleanup. Extend `ProjectionPlatformAdapterTests` and `ErasureVerificationServiceTests`, retaining failure sanitization and caller cancellation.
- Runtime baseline: isolated existing-build AppHost started successfully; Parties readiness passed and resource state was inspected before editing. This is a local baseline, not complete topology qualification. Stop the owned AppHost before builds.
- Implementation: all cleanup-result branches read the registered clock at result creation. Verification uses the same injected clock while retaining the original two-argument constructor and delegate-provided timestamps. The unchanged public cleanup delegate now has its own file. The existing mapping fake was extracted into `ErasureCleanupMappingStore` so controlled asynchronous inventory tests need no visibility changes to the internal production interface.
- Validation: source-reference Debug builds pass with zero warnings/errors. Focused tests pass 20/20 and 9/9; full affected Security and service assemblies pass 241/241 and 1900/1900 without failures, errors, skips or not-run tests. Two original timestamp failures were reproduced; two other baseline rows exposed an invalid dynamic substitute and were corrected with the handwritten fake. New task-variable awaits use `ConfigureAwait(true)` to satisfy both repository analyzers and xUnit. Exact results and hashes are recorded in `tests/dw-62-erasure-cleanup-clock-2026-10-19/validation.json`.
- Review corrections: strengthened asynchronous Memories success, multi-delegate report completion, failed/pending result sanitization and the legacy constructor. The existing processor test now uses the real clock-injected verification service and reads the saved certificate back through the controlled record-store port, asserting the report/certificate timestamp agreement. Its record store is a test double, not a deployed persistence qualification. The failing-SDK test now replaces the Memories inventory to avoid ambient Dapr dependencies. Independent follow-up confirms all six findings are addressed with no new defect.
- Final validation after review: source-reference Debug builds again pass with zero warnings/errors. Focused verification, cleanup delegation and certificate propagation pass 23/23, 10/10 and 1/1; complete Security and service assemblies pass 244/244 and 1901/1901 without failures, errors, skips or not-run tests. DW-62 is resolved; no new deferrals.

## Review Triage Log

- low, patched — Memories success previously used an immediately completed empty inventory. The new successful inventory read stays pending until the controlled clock advances, then asserts completion time.
- low, patched — A single delegate did not distinguish intermediate completion from final report time. The test now waits for two delegates at distinct clock values and asserts the report stays pending until the second completes.
- low, patched — Timestamp preservation did not cover the sanitizing copy. Failed and pending results now carry an earlier timestamp and a private error; both preserve their time while the error is bounded.
- low, patched — Certificate propagation lacked an explicit timestamp assertion. The processor now uses real verification with the supplied clock, captures certificate persistence through its controlled port and reads back the resulting timestamp/status.
- low, patched — The failing-SDK fixture left Memories inventory connected to the default Dapr service. It now uses the extracted empty mapping fake, so the clock assertion is independent of a sidecar.
- low, patched — Existing legacy callers did not assert system-clock behavior or the exact original constructor signature. The new compatibility test checks both without sleeps or timing tolerances.
