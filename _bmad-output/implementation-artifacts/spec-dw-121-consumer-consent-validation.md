---
title: 'DW-121 consumer consent validation outcomes'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Consumer consent grant/withdraw adapters turn client-side ArgumentException validation failures into generic Failed results. The admin adapter and returned gateway validation results already use ValidationRejected. The consumer page forwards stored channel/consent identifiers, so malformed historical data can reach the validation boundary; no current tenant incident is claimed.

**Approach:** Map ArgumentException from the delegated grant and withdrawal operations to the existing safe ValidationRejected result. Preserve null-request guards, caller cancellation, other exception handling, returned outcome mappings, resolved consumer identity, and all client validation rules. Complete DW-121 only.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps or irreversible actions. Small footprint: ConsumerConsentClient.cs, its existing UI test file and a separate HTTP handler test double if needed, this spec, the DW-121 ledger resolution and compact validation evidence. No public API, dependency, persistence, authentication, component or styling change.

Code map: src/Hexalith.Parties.UI/Services/ConsumerConsentClient.cs owns both catch ladders and already maps returned ValidationRejected. SelfScopedPartiesClient resolves party/tenant claims and directly calls HttpAdminPortalGdprClient; preserve that path. The underlying client throws ArgumentException for invalid channel, purpose and consent identifiers before HTTP. ConsumerPortal MyConsentPage uses fixed purpose catalogue values and forwards projected identifiers. Existing UX uses the safe outcome without exposing exception text. Read repository baseline, UX guidance, docs/architecture.md, tracked build configuration and tests/README.md.

Verification: extend tests/Hexalith.Parties.UI.Tests/ConsumerConsentClientTests.cs with the real self-scoped/client validation path, synthetic malformed projected identifiers, invalid purpose and zero HTTP sends; prove valid commands still reach the gateway with the bound identity. Cover both operation exception/cancellation/null controls. Establish failing-before evidence before editing production. Build the individual UI test project in Debug/source mode, run focused and full UI suites plus the existing ConsumerPortal component suite, warning/nested-submodule and CRLF-aware whitespace gates. Record exact commands, results and limitations in implementation-artifacts/tests/dw-121-consumer-consent-validation-2026-10-17/validation.json. Independent blind review and follow-up precede completion. Inspect and stop the owned Aspire baseline before compiling. Existing unrelated active specs and ledger items stay unchanged.

Implemented the two ArgumentException catch branches after caller-cancellation handling and before the generic fallback. Added real HTTP-client validation/no-send and valid bound-envelope tests, delegated argument/transport/scope/noncaller-cancellation controls, caller-token propagation and null-request guards. The corrected failing-before run has 27 total tests: 17 pass and the 10 intended validation regressions fail. The initial cancellation test wrongly required exception-object identity across asynchronous cancellation; corrected it to assert propagated cancellation and token, with no production change.

Initial --no-restore build encountered CS1704 from stale mixed package/source restore assets. Building with restore and the explicit source-mode properties succeeds with zero warnings/errors; no dependency versions or tracked build configuration changed. Aspire start/describe succeeded, Parties remained waiting while the identity provider started, healthy wait timed out after 20 seconds (exit 17), and stop succeeded. No full-topology health qualification is claimed.

Initial verification after the fix: individual Debug/source UI test build succeeds with zero warnings/errors; focused adapter tests pass 27/27 and the full UI assembly passes 371/371 with no failures/errors/skips. Warning/nested-submodule and CRLF-aware whitespace gates pass. Existing C# files retain their original LF endings to avoid unrelated formatting; new artifacts use CRLF. The exact Conventional Commit candidate passes repository-pinned commitlint 21.2.2 with zero problems/warnings. ConsumerPortal full-suite and independent review remain pending.

The individual ConsumerPortal Debug/source build succeeds with zero warnings/errors, and its full component assembly passes 82/82 with no failures/errors/skips. Combined full affected suites pass 453/453. Sanitized evidence, exact commands and source/XML hashes are recorded in tests/dw-121-consumer-consent-validation-2026-10-17/validation.json. No component markup, client validation, scope resolution or dependency source was changed.

## Review Triage Log

- Low, patch: direct invalid-input tests did not substantiate the historical-projection trigger. Added real consent-query deserialization and overview forwarding for malformed channel/consent IDs; only the query is sent and command submission remains blocked. No actual tenant incident is claimed.
- Low, patch: returned-outcome coverage tested grant validation only. Expanded the existing theory across all twelve returned outcomes for both operations, preserving safe results without backend details.
- Low, patch: the accepting HTTP double could hide a route/type regression. Capture and assert exact POST command URI, commandType and grant lawfulBasis alongside bound tenant/party and unchanged identifiers.
- Low, patch: a synthetic scope exception did not exercise actual resolution. Added real missing/ambiguous principal cases for both operations; all return Failed without HTTP.

The four review findings improve verification only; production remains the same eight-line catch-branch change. No new deferred work. Final verification and independent follow-up are pending.

Independent follow-up confirmed all four review corrections and found no concrete remaining problem. Final Debug/source UI build has zero warnings/errors; focused adapter tests pass 44/44, the full UI suite passes 388/388 and the existing full ConsumerPortal suite passes 82/82 (470 total), with no failures/errors/skips. ConsumerPortal production/tests did not change during review, so its already-passing full suite remains applicable. The original-code baseline packet retains the earlier 27-case regression and its ten expected failures; the review tests were added afterward. DW-121 is complete; no new deferred work. The separate bounded Aspire baseline timeout is retained without claiming full-topology acceptance.
