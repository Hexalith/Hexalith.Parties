---
title: 'Verify five deferred runtime and UI boundaries'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '37f23c6e52dc2f3b730b639f3487acd4b59108a6'
context: ['{project-root}/AGENTS.md', '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md']
---

<frozen-after-approval reason="human-owned intent">

## Intent

Extend committed verification for DW-168, DW-100, DW-58, and DW-99, and requalify the retained Story 8.8 runtime boundaries for DW-98 on the 2026-11-27 dependency cohort.

## Boundaries & Constraints

Assign each item to a subagent. Run the build and tests after all five implementations are in place, then fix failures and push the validated commits directly to main. Preserve Story 8.8 and other deferred gates wherever their separate acceptance criteria are still open.

</frozen-after-approval>

## Code Map

- DW-168. The direct theory and existing bulk pre-delete race in `PartyMemorySourceInventoryTests.cs` share the recovery handler and persisted stores. Reuse its deletion delay, absence body, and request history.
- DW-100. `PartiesAdminPortalE2eFixture.cs` has a one-shot profile load gate. Consumer updates return immediately and the party read supplies current freshness. Use the test-only endpoint registration in `Program.cs` and the existing DOM observer in `consumer-portal-routes.spec.ts`. Check matching specificity and Blazor CSS scope for the two freshness variants.
- DW-58. `verify_workloads` checks receipt metadata and phase details, as does `PartyLegacyBackfillProcessRuntimeTests.cs`. Reuse Python's receipt rewrite helper, the C# phase sequence, and the `BACKFILL_METHODS` report gate.
- DW-99. All four component test files already exercise fallback and shared registration. Reuse their local and shared component assertions and the Admin export data and filename checks.
- DW-98. Story 8.8, the Story 8.3 matrix, sprint status, and the deferred ledger hold the previous disposition. Read selected package APIs and root gitlinks independently and compare the published contract with retained Server, MCP and Aspire behavior.

## Tasks & Acceptance

- [x] `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` -- Extend the bulk race into two terminal outcomes.
- [x] `PartiesAdminPortalE2eFixture.cs`, `Program.cs`, fixture unit tests and `tests/e2e/specs/consumer-portal-routes.spec.ts` -- Add stale freshness and controllable saving, then verify Chromium rendering and live-region continuity.
- [x] `scripts/verify-backfill-and-memories-deferrals.py`, `scripts/test_backfill_and_memories_integration.py` and `PartyLegacyBackfillProcessRuntimeTests.cs` -- Enforce the same phase-specific key sets in both receipt readers and extend malformed envelope controls.
- [x] `StatusLiveRegionTests.cs`, `DataFreshnessIndicatorTests.cs`, `MyProfilePageTests.cs`, and `PartiesAdminPortalComponentTests.cs` -- Verify shared-feedback activation with a throwing downloader and independent download-only export.
- [x] `8-8-client-mcp-apphost-build-and-deploy-cleanup.md`, `story-8-3-platform-api-prerequisite-matrix.md`, `sprint-status.yaml`, and `deferred-work.md` -- Record selected identities, observed parity, and remaining Story 8.8 gates.

- Given the selected producer receipt is persisted, when the imported producer reaches either terminal unit, then fresh bulk instances require fresh deletion and qualified absence before persisting imported settlement and source closure, and preserve the unrelated unit.
- Given an interactive profile, when a gated save runs, then Chromium observes ready, Saving..., and saved on the same status node with stable live semantics. A stale profile has a visible non-live last-known dot. Both variants resolve fill and border to CanvasText in forced colors.
- Given a receipt with incorrect top-level keys or malformed `stagingEnvelope`, when either parent verifies it, then the receipt is rejected and its negative controls run in the matching gate.
- Given feedback registration with a throwing downloader, when each shared component renders, then it activates without resolving the downloader. Given download-only registration, when admin exports, then the downloader receives the export and the erasure dialog uses local markup.
- Given the cohort adopted at `2d52234a`, when the prerequisites and retained suites are checked, then all four tracking records show exact identities and an evidence-based Story 8.8 verdict.

## Implementation Notes

- The bulk race now checks both post-race outcomes through fresh bulk cleanup clients and reads the raw persisted receipts and closure.
- Browser tests exercise both profile routes, both freshness variants, and an explicitly released saving state. The profile stylesheet targets the child-rendered dot and keeps the system color in forced colors.
- The parent receipt verifiers share the phase-specific key policy across their negative controls. Each new C# case is required by the Python xUnit report gate.
- The Story 8.8 recheck records package provenance and retained test results. Its replacement and retirement gates remain open.
- Full verification exposed two prior issues. The hosted Release graph now passes its Commons selection through restore and build. The Story 7.4 verification asserts the current minimum checkpoint calculation and its rejection before the batch write.


## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and action |
| --- | --- | --- |
| Blind review, retained-suite commands | Low | Recorded exact commands in Story 8.8. The suite totals are in the tracked table and local XML supplements it. |
| Blind review, sprint comment | Low | Identified the adjacent status summary as the EventStore producer sprint. |
| Blind review, backfill command | Low | Recorded the selected-cohort backfill-only invocation in verification. |
| Blind review, ETag trimming | Low | The pre-existing trim semantics are independent of the exact property sets and the requested envelope type and digest controls. |
| Blind review, workload process IDs | Low | The two readers' pre-existing process ID value policy is outside the requested key schema and envelope checks. |
| Blind review, C# reader wiring | Medium | Connected the parent and schema theory to the same receipt reader and XML witness verification. |
| Blind review, normal-color paint | Medium | Added paint assertions to the current and last-known browser cases. |
| Blind review, observer scope | Medium | Extended observation to profile-wide mutations, including removal of the tracked header. |
| Blind review, save gate race | Medium | Ordered capture and gate acquisition under the fixture lock. |
| Blind review, graph handoff coverage | Medium | The real Release restore verifies the pinned dependency floor. The runtime reference probe verifies propagation with a distinct child default. |
| Blind review, Story 7.4 source guard | Low | The supplemental source check follows the implementation. Executable projection handler tests already cover gap rejection without a coordinated write. |
| Edge review, save gate race | Medium | Capture and gate acquisition now share the fixture lock. |
| Edge review, observer scope | Medium | The browser probe observes the full profile subtree. |
| Edge review, normal-color paint | Medium | The browser probe checks computed normal paint before changing media. |
| Verification review, C# reader wiring | Medium | The parent and negative cases now verify matching XML and JSON through the same entry point. |
| Verification review, normal-color paint | Medium | Normal paint checks cover both variants in addition to forced colors. |
| Verification review, packaged host rebuild | Medium | Host preparation now passes the evaluated Commons version and asserts the handoff in its compiler fixture. |

## Verification

- Standalone package-mode Release restore and build passed with zero warnings and errors. Packaged host preparation succeeded from both a standalone rebuild and the reused solution artifacts.
- Final Chromium package-mode run passed all 120 cases.
- Package-mode Release .NET run across 18 assemblies. 6,980 passed, zero failed and 73 conditional skips. The reviewed UI suite (487) and Aspire runtime preparation class (138) also passed. The separate owned backfill run covered the provider-gated backfill cases.
- Repository Python suites passed 141 script, 20 BMAD and 187 lifecycle cases. Browser tooling passed type checking, 22 configuration tests and five restored asset checks.
- The owned backfill run passed all 81 required cases and independently verified the empty and existing target receipts. The test process, sidecar and Redis container exited. The historical full Memories/backfill runner keeps its own dependency selection.
- The Platform package command stopped at `HXP020` after installing the selected Dapr runtime. The runner requires the trusted CI parent plan and key handoff. This independent qualification remains pending.

The owned backfill command uses the current package-mode Release output.

```bash
results="$PWD/TestResults/dw-168-100-58-99-98-2026-11-27"
mkdir -p "$results/workloads-final"
PARTIES_BACKFILL_WORKLOAD_EVIDENCE="$results/workloads-final" \
    run-with-temp python3 _bmad-output/implementation-artifacts/tests/five-snapshot-history-and-publish-2026-11-11/run-owned-backfill.py \
    --assembly "$PWD/tests/Hexalith.Parties.Runtime.IntegrationTests/bin/Release/net11.0/Hexalith.Parties.Runtime.IntegrationTests.dll" \
    --output "$results/backfill-final"
```

The independent Python verification loaded `scripts/verify-backfill-and-memories-deferrals.py`.
It ran `check_report` on `backfill-final/backfill.xml` with `BACKFILL_METHODS` and then ran
`verify_workloads` on `workloads-final`.

The attempted native profile command was
`dotnet tool run hexalith-module -- test --manifest module/hexalith.module-manifest.v2.json --profile platform-package --mode package --evidence TestResults/dw-168-100-58-99-98-2026-11-27/platform-package.json --output json`.
The resulting local evidence is retained with the [test logs](../../TestResults/dw-168-100-58-99-98-2026-11-27/).
