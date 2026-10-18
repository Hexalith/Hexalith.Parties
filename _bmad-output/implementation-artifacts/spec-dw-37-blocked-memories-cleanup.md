---
title: 'Cover Memories cleanup blocked by a missing CaseId'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close DW-37 by testing the existing `PartyMemoryCleanupService.DeleteByPartyAsync` behavior when a mapped unit has neither a persisted CaseId nor a nonblank fallback. Cleanup must report the block without sending an unscoped request or losing the mapping, continue processing units whose cases are known, and support retry after a fallback becomes available. Assert persisted inventory and sanitized progress diagnostics using the existing serialized-state fixture and real mapping store. Limit the change to tests and completion evidence; preserve production behavior and separately tracked cancellation and asynchronous Memories reconciliation work.

</frozen-after-approval>

## Implementation Notes

- Selected the next bounded, Parties-owned coverage gap from the deferred ledger. Investigation confirmed the existing missing-case branch is correct, so executable production source is unchanged.
- Added eight theory cases in `tests/Hexalith.Parties.Tests/Search/PartyMemoryCleanupServiceTests.cs`. Six combine null/empty/whitespace persisted cases with empty/whitespace fallbacks; two vary whether an eligible captured-case mapping precedes or follows blocked mappings.
- Tests exercise `PartyMemoryUnitMappingStore` with the existing serialized `PartyMemoryMappingState` fixture. They assert scoped HTTP paths, exact retained state, no physical ledger deletion, sanitized block/progress reporting, and no blocked-path log output. Fresh store/service instances prove recovery through a restored fallback or persisted case repair.
- Baseline revision: `2ffc4b4f45b577aff9c742f7fe0287433c82b7fc`. The existing cleanup class passed 33/33 before changes. Debug/source-reference build passed with zero warnings/errors; expanded cleanup class passed 41/41 and the owning service assembly passed 1195/1195, with no failures/errors/skips. Warning-override guard and whitespace checks passed.
- Runtime baseline command `aspire start --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --format Json` exited 2: MSB4166 child-node exits and canceled NuGet restores prevented startup. Subsequent `aspire describe` confirmed no running AppHost. No full-topology qualification is claimed.
- Independent Blind Hunter review found three verification improvements: HTTP verb assertions, recovery diagnostics/log assertions, and reproducible validation artifacts. All are patched without changing executable production source. Validation commands and outputs are retained in `tests/dw-37-blocked-memories-cleanup-2026-10-18/` alongside the result manifest.
- Final follow-up verified all three corrections with no unresolved issue. Final validation repeated after the test-assertion changes: build zero warnings/errors, focused cleanup 41/41, full service assembly 1195/1195, no failures/errors/skips. No new deferred work.

## Review Triage Log

- **Medium — patched:** The permissive HTTP double recorded only URIs, so an eligible request using GET could pass. The double now records methods, and the new tests require DELETE for every eligible/retry request and no request while all entries are blocked.
- **Low — patched:** Recovery asserted cleanup and state but omitted audit diagnostics. Restored-fallback retry now requires a null block reason; captured-case repair requires the exact two-unit success summary. Both paths assert empty service/store logs after recovery as well as before it.
- **Low — patched:** Counts alone omitted reproducible invocation details. The evidence manifest and retained build/test outputs record the exact Debug/source-reference build, focused xUnit class selection, full owner-assembly run, and separate runtime-start failure.
