---
title: 'Fix mapped catalog selector compilation'
type: 'bugfix'
created: '2026-11-09'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Source-mapped service-test compilation fails with three CS0234 errors in `tests/Hexalith.Parties.Tests/Gateway/PartiesCatalogGatewaySelectorTests.cs` because the `eventstore` alias selects the executable host, while the query types belong to the reusable Gateway assembly.

**Approach:** Resolve all three types through their globally available Gateway assembly in both dependency modes. Verify the pinned `hexalith-module` prepare-build Debug mapping, catalog selector tests, DW-27 handler/gateway paging regressions and package compatibility. Preserve dependency versions, immutable historical evidence, existing changes and test assertions. Report the four existing prerequisite failures separately without reconciling observations. Do not commit or push.

</frozen-after-approval>

## Implementation Notes

- Intent gaps: none. Irreversible actions: none. Planned footprint: one test file plus this spec and a new validation packet; no production or dependency configuration changes.
- Ownership: the EventStore host excludes `Queries/**/*.cs`; `Hexalith.EventStore.Gateway.csproj` compiles those linked files. Its transitive reference is global in both source and package mode. Retain the separate host alias used for `Program` in `EventStoreGatewayRoutingTests.cs`.
- Existing DW-27 documentation, tests and receipts are user work. Preserve them byte for byte. Snapshot 3,685 protected files, including tracked historical artifacts and pre-existing untracked receipts, before changes.
- Use only the environment emitted by the pinned 4.44.13 tool for source mapping. Record its existing Platform version-range warning without changing either dependency.
- Exact commands, environments, exit codes and compressed logs/XML will be recorded in `tests/catalog-selector-source-build-2026-11-09/validation.json` beside the raw evidence.

- Removed only the source-only extern alias and conditional alias branch from the selector test; retained all three existing global aliases unconditionally. Namespace/test body is byte-identical to HEAD. No test cases or assertions changed.
- Reproduced the original mapped build failure (3 errors, 0 warnings), then verified mapped Debug and package Debug builds (both 0 errors/warnings). Each mode passes 9 selector cases and 952 paging cases with `-failSkips`.
- Separate package prerequisite-class run: 114 passed, 4 failed, 0 skipped. Names match the pre-existing November 9 receipt exactly: Builds observation, McpCli observation, Platform observation and EventStore sprint backlog expectation. No broad clean-suite claim or reconciliation.
- Warning override guard, target-file whitespace and preservation checks passed. Full commands, emitted mapping environment, immutable compressed logs/XML, source mapping files and protected-file hash manifest are in the new packet. Existing historical evidence and user changes are unchanged. No commit or push.

- Independent blind review completed. The one-shot workflow enables only that review layer; none was skipped. All findings concerned evidence presentation. Applied nine simple documentation/metadata refinements; rejected additional binary-provenance capture because it would require repeating already-passing source verification beyond the packet's focused claims. No implementation defect or new deferral remains in scope.
- Actual SDK: 10.0.403. Added runnable mapping apply/clear examples, exact per-class counts, dependency gitlink values and final checkout identities, original package assets and explicit compressed/uncompressed hash metadata. Final evidence audit verifies 14 command logs, five XML receipts and all archived-input digests. The documented read-only preservation comparison passes with all 3,685 protected files unchanged.

- A subsequent whole-tree `git diff --check` exits 2 on the pre-existing user-modified comment in `PartySdkQueryHandlerTests.cs:281`. Its bytes match the pre-change snapshot. Target-file whitespace still passes. Recorded the exact broad command/output separately without editing user work or relaxing the check. Final packet now contains 15 verified command-log archives.

## Review Triage Log

| Finding | Verdict and disposition | Evidence |
| --- | --- | --- |
| R01: source command environment replay | low, patched | README now applies each recorded source environment through a runnable subprocess example |
| R02: package command environment clearing | low, patched | Same example removes every recorded mapper key before applying package overrides |
| R03: actual SDK version | low, patched | `dotnet --version` exits 0 with 10.0.403; exact command/log added to receipt |
| R04: overrides versus inherited environment | low, patched | Receipt and README distinguish them; relevant inherited override is DOTNET_ROOT only |
| R05: independent dependency identity comparison | low, patched | Receipt now records full canonical before/after gitlink values and separately labelled final checkout heads |
| R06: derived package-selection summary | low, patched | Archived original package assets, both digests and extraction expression allow independent verification |
| R07: bind each test to retained DLL/deps hashes | low, rejected | Runs were sequential immediately after the corresponding build, with mode evaluation and raw logs/XML preserved. No retained source-binary provenance is claimed. Extra source capture would require repeating passing verification for a rare audit need outside this scoped compatibility check |
| R08: compressed versus uncompressed snapshot digest | low, patched | Explicit `uncompressed_sha256` and `archive_sha256` fields distinguish both |
| R09: protected-input selection/comparison | low, patched | README documents selection and a runnable read-only comparison; that command passes for all 3,685 files |
| R10: separate handler/gateway totals | low, patched | XML-derived counts show 875 handler and 77 gateway cases in each mode |
