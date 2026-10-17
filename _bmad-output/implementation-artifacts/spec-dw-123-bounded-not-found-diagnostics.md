---
title: 'DW-123 bounded not-found diagnostics'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Standalone channel update/removal and identifier removal, plus the corresponding composite update checks, interpolate submitted identifiers into persisted rejection messages and composite rejection details. Consent already uses fixed not-found diagnostics; DW-123 tracks the remaining inconsistent handlers.

**Approach:** Use the fixed messages `Contact channel not found.` and `Identifier not found.` at all six remaining not-found branches, including composite `Rejected` details. Preserve rejection event types, validation and lookup order, exact identifier matching, all-or-nothing behavior, state, and successful command payloads. Prove bounded non-echoing output using distinct submitted identifiers, including a maximum-length valid identifier and an oversized composite identifier. Complete only DW-123; custody work DW-129/DW-148 and all other deferred work remain open.

</frozen-after-approval>

## Implementation Notes

Resumed and completed on 2026-10-17 at Parties revision `4e4f271faa5eb141834a1e53eeebe8a2e1aef68c` to verify the remaining completion gate. The diagnostic implementation and regression tests are already committed; this continuation changes only the completion records and evidence. The implementation, review findings, and failed validation run described below are historical. Current validation and review results are recorded separately in `tests/dw-123-bounded-not-found-diagnostics-2026-10-17/completion-validation.json`, preserving the original `validation.json` receipt.

Planning classification: no unresolved intent gaps; no migration, deployment, dependency update, or external mutation. Small change to one existing aggregate and focused existing aggregate tests, plus this spec and the DW-123 ledger entry. No new public API. Existing not-found tests in `PartyAggregateContactChannelTests.cs`, `PartyAggregateIdentifierTests.cs`, and `PartyAggregateCompositeTests.cs` are the regression anchors. Run the individual Debug/source-reference Server.Tests project; broaden to Parties.Tests because the aggregate is used by the processor. Inspect and stop the owned Aspire baseline before compiling, following repository guidance.

Implemented the six not-found branches and all three composite rejection-detail lists in `PartyAggregate.cs`. Extended the existing tests to check both 16-character and maximum-length valid missing IDs; the oversized composite control keeps the pre-lookup invalid-ID outcome. Preserved each existing file's line endings to avoid unrelated formatting changes.

Before the production fix, the three focused classes ran 100 tests: 88 passed and all 12 fixed-message cases failed. After the fix, the complete Server.Tests assembly passes 309/309 with no skips, failures or errors. Both affected individual project builds succeed in Debug/source mode with zero warnings/errors. Broader service verification and independent review are pending. Raw local receipts are under `/tmp/dw-123-validation`.

The owned isolated Aspire baseline reached Parties Running/Healthy and stopped successfully. EventStore exited 1, eventstore-admin and Tenants exited 134, and UI exited 150; this is not full-topology acceptance and no runtime source was changed. Concurrent root submodule changes are outside this work and remain untouched.

Independent review found five test/documentation gaps, with no production defect in the fixed-message change. Added valid sibling updates and serialized before/after state assertions to the three composite not-found tests; expanded oversized/unsafe IDs across channel update, channel removal and identifier removal; required exact single-event/single-detail rejection output; and added standalone/composite identifier case-variant controls. The ledger now distinguishes implemented behavior from its historical reason and records the completion blocker.

Broader service verification ran `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -xml /tmp/dw-123-validation/parties.xml`: exit 1, 1008/1010 pass, two failures, zero errors/skips/not-run. `PlatformApiPrerequisitesTests.FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` expects Builds `9f0121c77e3f085c58f5c926057606db8e6511a7` while the concurrent sync committed `4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77`; `DocumentationFitnessTests.MaintainedTechnologyTablesMatchCentralPackageCatalog` expects the current `13.6.1-beta.935` Dapr toolkit table row. Their bodies read repository/git/catalog data without executing PartyAggregate. No dependency, dated receipt, owner approval, or unrelated documentation was changed to hide these failures. Under the baseline's all-tests-pass completion rule, this spec remains in-progress and DW-123 remains open pending repair by the dependency-sync work.

A separate concurrent `/pushall` committed the original implementation/tests and draft spec in `90af3047b50046cb4f6f91e01f182f562110aab3`. This build session did not run that sync, change dependencies, push, or rewrite its commit. Review follow-up tests and evidence remain reviewable in the working tree.

## Review Triage Log

- Low, patch: The missing-operation composite tests lacked useful sibling work and state end-state assertions. Each now includes a valid PersonDetails change and checks exact serialized state equality after rejection.
- Low, patch: Oversized IDs were covered only for identifier removal. The existing unsafe-operation theory now covers all three changed composite branches with unsafe and 4096-character inputs.
- Low, patch: The unsafe-operation control allowed extra rejection events/details. It now requires exactly one typed rejection and exactly the fixed single-element Rejected collection, with no applied/skipped work or detail result.
- Low, patch: Identifier missing cases did not lock exact ordinal matching. Standalone and composite case-variant removal tests now reject while preserving the stored lowercase identifier.
- Low, patch: The ledger's historical reason could be read as describing current implementation. Added the implementation spec/evidence and the separate completion blocker; its open status is intentional while the broader gate fails.

Final review validation: Debug/source Server.Tests build succeeded with zero warnings/errors; the full final assembly passes **315/315**, zero failures/errors/skips/not-run. All 12 original failing-before cases now pass. Independent follow-up confirmed all five findings addressed and found no concrete remaining defect. Production source matches the concurrent committed implementation and frozen intent is unchanged. The compact evidence packet records exact commands, counts, source hashes and the two separate broader-gate failures.

## Completion Revalidation — 2026-10-17

At Parties revision `4e4f271faa5eb141834a1e53eeebe8a2e1aef68c`, both individual Debug/source builds succeed with zero warnings/errors. Full Server.Tests passes **315/315** and full Parties.Tests passes **1080/1080**, with zero failures, errors, skips or tests not run. The XML explicitly confirms that all 12 original failing-before diagnostic cases pass, together with `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` and `MaintainedTechnologyTablesMatchCentralPackageCatalog`. The original completion blocker is resolved. Independent review identified four completion-record improvements, all addressed below; no new work is deferred.

The warning-override guard passes. The frozen intent matches the baseline commit byte-for-byte, and `tests/dw-123-bounded-not-found-diagnostics-2026-10-17/validation.json` is unchanged. Fresh commands, counts, hashes and named regression outcomes are recorded in the separate `completion-validation.json` beside that historical receipt. No production source, tests, dependencies, gitlinks or runtime configuration changed in this continuation, and no new runtime qualification is claimed. Changes remain uncommitted under the repository safeguards.

Completion review triage:

- Low, patch: Completion tracking still showed the prior blocker during review. Finalized this spec and DW-123 after the green gate, retaining the original failed run in its historical receipt.
- Low, patch: The new receipt omitted the actual execution versions. Added SDK `10.0.401`, .NET runtime `10.0.12`, and xUnit `4.0.2+0c7aa4991f` from the CLI and both test XML files.
- Low, patch: Source-reference dependencies lacked checkout evidence. Recorded all ten root gitlinks, matching canonical checkout revisions, and empty explicit porcelain statuses. This observation was made after the tests; its timing is recorded rather than represented as a pre-build capture.
- Low, patch: The frozen-intent preservation claim lacked reproduction details. Executed and recorded the exact extraction/comparison command and matching SHA-256 hashes for the complete frozen block, including its opening and closing tags.

Independent completion follow-up verified all four corrections directly and found no concrete remaining defect in scope. The final whitespace, evidence consistency, source-preservation and frozen-block checks pass. DW-123 is complete.
