---
title: Verify the behavior behind five released-catalog deferrals
type: bugfix
created: 2026-11-23
status: done
route: oneshot
review_loop_iteration: 0
context: []
baseline_commit: 34fe48dbea826019dcbc42396f797ad6d479ea00
---

<frozen-after-approval reason="requested implementation and publication of five deferred-work items">

## Intent

Make the released-catalog check show which behavior actually passed for DW-50,
DW-58, DW-82, DW-100, and DW-168. A passing test elsewhere in the same class
currently suffices for each item, even when the report has a failed row and
inconsistent totals. Require the defining behaviors and their distinct theory
variants, verify the resolved package identities, and keep the full selected
suites running. Record local qualification in the ledger. Deployment, historical
reconciliation, writer drain, and owner acceptance require separate evidence.

</frozen-after-approval>

## Implementation Notes

Use the existing three-project runner and its retained xUnit and dependency
files. Exercise the report reader with missing cases for each item and malformed
counters. Run the normal entry point on the repository's committed package
selection in a standalone checkout.

## Verification

At the committed Builds pin `055d7df5b49669d5b705ce163b0b2e187ecb9a50`, the
.NET SDK 11.0.100 Release builds selected EventStore 5.4.0, Memories 4.1.0, and FrontComposer
6.0.1. The core suite passed 1,091 selected tests, projections passed 466, and
UI passed 465. All three reported zero failures, skips, errors, or unexecuted
cases. The reports contain 10 named cases for DW-50, six for DW-58, four for
DW-82, eight for DW-100, and three for DW-168.

DW-50 and DW-58 still require deployed writer drain, migration and rollback
evidence. DW-82 and DW-168 require historical inventory reconciliation and
deployed cleanup evidence. DW-100 requires production composition and owner
acceptance. All five ledger entries retain open status.

All six verifier regression tests passed. Full Python discovery reported
111 tests run and one additional suite setup error. The EventStore owner
AppHost exited with `Aspire.Hosting.DistributedApplicationException` during
manifest publication. The same failure was recorded before this batch in
`spec-five-ci-entrypoint-deferrals.md`.

## Review triage

- Medium, fixed. A core dependency manifest previously satisfied the projections check. Each active target must contain the correct test project and its DLL.
- Medium, fixed. A qualified package entry could omit its runtime DLL. Each expected package must now supply its own DLL in that target.
- Medium, fixed. Contradictory collection counters and explicit assembly errors now fail verification.
- Low, fixed. A regression test reads the escaped string argument emitted by xUnit v3.
- Medium, fixed. Revalidation invalidates a previous summary and publishes the new summary by atomic replacement.
- Low, fixed. The spec records the SDK, catalog, observed results and external prerequisites.
