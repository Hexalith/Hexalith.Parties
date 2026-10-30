# Five causal reconciliation items — 2026-10-30

The four final reconciliation findings from `spec-five-reconciliation-and-migration-deferred-items.md` now have qualified source implementations. DW-100 gains supported source integration but remains open for publication/adoption and owner acceptance. No dependency versions were changed, packages manually published, deployment performed or production state migrated.

| Verification | Result |
| --- | --- |
| Retained local diagnostic Debug build | Zero warnings/errors |
| Retained local diagnostic suite | 135 passed, zero failed/skipped |
| Shared producer diagnostic Debug build | Zero warnings/errors |
| Shared producer diagnostic suite | 135 passed, zero failed/skipped |
| Mixed shared/legacy-transport diagnostic suite | 135 passed, zero failed/skipped |
| Mixed retained/cancellable-transport diagnostic suite | 135 passed, zero failed/skipped |
| Owning EventStore SignalR Debug build/suite | Zero warnings/errors; 55 passed |
| Owning FrontComposer Shell Debug build | Zero warnings/errors |
| Owning FrontComposer reconciler class | 30 passed, zero failed/skipped |
| Historical pre-integration broad Shell suite | 3,885 passed, 12 failed, one named Live skip |
| Current pinned source mapping | Refused HXW009; missing pinned Builds object |
| Actual Parties UI test-project build / Aspire baseline | Refused missing catalog APIs |

The four diagnostic suites execute the same 135 cases with different compilation fences. Their counts overlap the owning-project controls; do not sum them as distinct tests. `qualification.json` binds source files, receipts and exact executed matrix methods. `verify-evidence.py` checks all four lanes independently without historical temporary files and works with Python optimization enabled.

The diagnostic projects link unchanged production/test source files directly. They deliberately bypass unrelated module composition, not compiler warnings. They are not a complete mapped project graph or actual NuGet-mode validation. In particular, the retained lane exercises retained local orchestration and legacy transport API shapes against linked producer sources; it does not certify installed package binaries. The source shared lane additionally compiles the supported adapter and shared reconciler. Actual owning-project checks are recorded independently.

Reproduce from the repository root:

```bash
python3 _bmad-output/implementation-artifacts/tests/five-causal-reconciliation-2026-10-30/create-focused-harness.py "$PWD" /tmp/causal-recheck
```

Build each of the four generated `Focused.csproj` files individually with `dotnet build -c Debug -m:1`, then invoke each built `Focused.dll` directly with `dotnet` and `-xml <new-receipt.xml>`. Preserve the historical receipts. Audit them using:

```bash
python3 _bmad-output/implementation-artifacts/tests/five-causal-reconciliation-2026-10-30/verify-evidence.py
python3 -O _bmad-output/implementation-artifacts/tests/five-causal-reconciliation-2026-10-30/verify-evidence.py
```

Operation witnesses are caller-owned: capture the accepted command/checkpoint and inspect the exact model just read. Current alone, an unrelated party at a higher checkpoint, and the correct party below the accepted checkpoint all remain pending. Missing witnesses cannot complete the Parties path. The generic producer preserves its legacy no-witness API without claiming command causality. New callers must supply a cancellation-aware witness; old Parties callers without one now remain pending until cancellation.

Intervals retry stale/error reads while connected, and durable connection generations retain outages during acceptance. Real SignalR tests hold signal and scoped-detail join/leave RPC acknowledgements, cancel before server release, wait for actual late server completion and verify revoked callbacks remain absent. A cancelled join preserves another same-group subscriber. These tests qualify client invocation cancellation and local callback authority; server group operations may finish later.

FrontComposer 4.7.1 lacks the new contract and remains on local orchestration. Current package-mode EventStore transport retains legacy uncancellable calls. `HFC_SHARED_RECONCILE_READINESS` and `HES_CANCELLABLE_PROJECTIONS` are independently source-gated. Actual package/runtime acceptance and bounded teardown in the default package graph remain open under DW-100.

The broad failures concern unavailable pinned AI.Tools/Builds objects and missing restore assets in governance-triggered solution builds. The Live skip requires separately provisioned tenants. The current source resolver refuses missing Builds revision `70ac3de6328157547096e025140c11144b1cd86f`; the Parties build cannot resolve `RouteCatalogDeclarationSourceAttribute` / `RouteOperationContract`. No nested submodules were initialized or gates relaxed. Historical initial failures, including the shared duplicate-read defect corrected before the final parity run and an async test-cleanup analyzer correction, retain separate receipts.

XML receipts are archived as deterministic `.xml.gz` files. `qualification.json` retains the original uncompressed SHA-256 values; the audit verifies both archived and original bytes. Command receipts retain the exact original XML output arguments.

Review boundaries remain explicit: raw synchronous delegates already executing may finish after removal; an older remote leave can race with a replacement join; and an uncertain join may finish on the server after local rollback. These pre-existing owner-protocol gaps are recorded separately in the deferred ledger. Captured registrations not yet entering invocation and Parties-owned cancellation-aware read effects have their own qualified boundaries. Client invocation cancellation does not attest physical server group absence.

Review remediation qualifies a fresh five-second leave deadline, caller startup cancellation, captured-snapshot signal/detail revocation, real hub survivor delivery and preserved legacy signatures. The isolated callback mutation builds cleanly but intentionally fails both captured-snapshot cases, demonstrating that the new tests require the revocation guard. `test-run-check.py` proves ordinary process-group timeout cleanup and stable receipt bytes; escaped sessions remain outside that claim. Final receipts are named `accepted-*`; earlier parity and review-fix receipts remain historical.

Post-integration checks retain clean owning Debug builds, EventStore 55/55 and FrontComposer reconciler 30/30. Normalized tested source hashes are unchanged. A concurrent Cursor `/pushall` run published the source commits recorded in `publication.json`; its additional root gitlink and SDK updates were preserved. The latest Parties UI build still has four catalog CS0246 errors; current pinned source mapping refuses HXW009 for Builds `69701dfcf54cb9c69fc6cb0c51b031af8d218142`. Earlier mapping refusals remain historical. Independent VG-01 recheck accepts the captured-snapshot remediation and both audit modes.
