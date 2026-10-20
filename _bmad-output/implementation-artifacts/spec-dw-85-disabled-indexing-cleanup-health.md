---
title: 'DW-85: Observe Memories cleanup health with indexing disabled'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'e55da59006f55410b9390403c67253b4f8423e18'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-85 records that disabling Memories indexing makes health return immediately, hiding mapping-store and cleanup-route failures even though existing mappings still need erasure.

**Approach:** Always probe the mapping store. When indexing is disabled and cleanup configuration is retained, probe the cleanup route independently of rich search and report failed or incomplete cleanup capability as Degraded. Preserve the wholly unconfigured local-only default when its mapping probe succeeds, explicitly report that remote cleanup is unconfigured and unverified, and never imply that historical mappings or remote units are absent. Keep enabled-mode behavior and readiness registration unchanged. Verify the behavior, close DW-85, commit and push on main as requested.

</frozen-after-approval>

## Implementation Notes

- Freeform deferred work; no sprint story key. Clean main after rebasing the user's committed query changes on current origin/main. Checkout-only synchronization of the two remotely advanced root gitlinks leaves their tracked identities unchanged.
- Selection: DW-81 is constrained by Epic 8 spine I19: its class-(b) operational ledger cannot use replayable IReadModelStore/ReadModelWritePolicy without explicit reclassification. No generic operational-ledger seam was found. DW-82 needs an authoritative remote inventory; DW-83 needs durable operational recovery; DW-84 changes delivery retry/recovery policy. DW-85 is the next bounded independent local fix. Do not modify those stores, policies, dependencies or gitlinks.
- No unresolved intent gap, irreversible data operation or public API. Small runtime footprint: existing MemoriesSearchHealthCheck disabled branch and a private helper, focused tests and operator documentation. Reuse existing cleanup-route and mapping-sentinel probes. Do not add a search client or run rich-search probes while disabled.
- Configured means any endpoint, tenant, case or nonblank token is retained. A complete cleanup scope has an absolute endpoint, tenant and case, and a token when required. Partial retained configuration degrades. Wholly absent remote config explicitly reports cleanupConfigured=false and cleanupRouteReachable=false with a fixed reason; Healthy then describes local-only mode plus a working mapping store, not cleanup certification.
- Mapping probe retains unique reserved sentinel identities, round-trip verification and physical sentinel cleanup; real domain inventories remain untouched. Existing provider-failure sanitization and caller cancellation propagate as before. Existing enabled-mode nested outcomes remain unchanged; add no C# type to the production file.
- Tests reuse MemoriesSearchHealthCheckTests helpers; add disabled configured/unconfigured/partial cases, remote rejection, mapping read/clear failures, and caller cancellation. Health endpoint fixtures isolate optional remote cleanup and use serialized real mapping-store semantics. Verify a mapping outage degrades /health without blocking /ready.
- Baseline Aspire start used existing assets in isolation. The 15-second parties wait exited 17; subsequent describe showed parties healthy but eventstore and tenants finished. The exact AppHost was stopped successfully. No full-topology qualification is claimed.
- Validation: Debug affected test-project build, direct xUnit v3 health classes then complete service assembly, repository warning and whitespace guards, pinned commitlint. Preserve bounded command/results under tests/dw-85-disabled-indexing-cleanup-health-2026-10-20/.
- External work subsequently advanced seven submodule checkouts. Those changes are preserved and excluded from this commit. Debug validation uses a temporary archive of the recorded Builds gitlink 5d03f1db9c38db34ecac7fe6048937478ffb82cc via HexalithBuildsRoot; no dependency versions or tracked pointers are changed.
- First regression run against the original implementation: 41 health cases, 20 failed and 21 passed. Initial fixed run: all 41 passed. Expanded full service run: 2095 of 2096 passed, with the sole failure the unrelated FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph checkout/gitlink mismatch (expected Builds 5d03f1db9c38db34ecac7fe6048937478ffb82cc, actual fff4a212180d8cd1c5c0759655a9bc4fb6963c45). Source-mode launcher exits 2/HXW004 on externally advanced EventStore checkout vs staged gitlink. These checks remain intact; full source-mode qualification is not claimed.
- Review corrections stay within probe correctness: independently bounded sentinel cleanup on cancellation, exact sentinel-content validation, sanitized upstream HTTP reason text, a retained-credential production-DI case with no rich-search client, and explicit operator restart guidance.

## Review Triage Log

- Medium, patch: the cancellation fake ignored cancellation and concealed a sentinel leak after successful recording. Use a cancellation-aware fake that cancels immediately after recording, and give sentinel cleanup an independent bounded token before propagating caller cancellation.
- Medium, patch: the existing cleanup probe included provider-controlled ReasonPhrase for unexpected HTTP status; disabled mode newly exposes that text. Keep only the numeric status in health diagnostics and verify sensitive custom text is absent.
- Medium, patch: manually constructed clients did not exercise retained credentials through production DI. Add a disabled-mode production-registration case checking exact endpoint prefix, tenant/case, DELETE, bearer token, absent rich-search client and persisted sentinel cleanup.
- Low, patch: document endpoint/token startup capture and the restart required after configuration repair. This prevents stale endpoint or credential use from confusing operators.
- Medium, patch: a one-item round-trip could contain the wrong mapping. Verify exact sentinel unit/source/case and reject each independently altered field while still cleaning the probe.

- Final validation: Debug build has zero warnings/errors. All 53 health cases pass in the complete service run; overall 2101/2102 pass with no skips/errors/not-run. The sole failure is the unchanged dependency-receipt fitness gate: the selected Builds identity remains 5d03f1db9c38db34ecac7fe6048937478ffb82cc while external workspace sync ea926163fb51033b226541f069ebf6a5941922af committed gitlink fff4a212180d8cd1c5c0759655a9bc4fb6963c45. That sync also captured the initial DW-85 implementation; this task preserves its history and commits the reviewed corrections and closure separately. No dependency signoff or full-suite green claim is made.
- All five review fixes have independent follow-up confirmation; no new deferred work. Warning/submodule guard, whitespace check and exact pinned commitlint candidate pass. Bounded evidence and source hashes are in tests/dw-85-disabled-indexing-cleanup-health-2026-10-20/validation.json.
