---
title: 'DW-27 — Invalid page sizes versus cursors'
type: 'bugfix'
created: '2026-10-29'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Invalid envelope page sizes must be reported separately from invalid cursors so callers receive accurate query failures.

**Approach:** Verify the existing DW-27 implementation and regressions against the currently selected dependencies. For PartyIndex and PartySearch, envelope sizes outside 1–100 must yield InvalidPage before cursor decoding, read-model access or search; cursor and offset errors must retain InvalidCursor. Preserve gateway HTTP 400 mapping, safe response text, default and legacy paging, boundary sizes and valid continuation. Make a focused correction only if current verification reveals a gap.

</frozen-after-approval>

## Implementation Notes

- Investigated baseline `ce66f530f4d0e208fe5d6fe83ea277f7de2aa407`. The archived DW-27 resolution records completion on 2026-10-22 through `spec-five-sdk-runtime-deferred-items.md`; its existing ledger status remains accurate.
- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs`, `TryResolvePaging`, already rejects invalid envelope sizes with `QueryAdapterFailureReason.InvalidPage` before decoding cursor or offset inputs. Valid-size cursor and offset failures retain `InvalidCursor`.
- Existing `PartySdkQueryHandlerTests.Paging_*` controls exercise 48 invalid-size combinations plus cursor/offset preservation, validation precedence, boundaries/defaults, legacy clamping and valid continuation. Existing gateway controls execute the actual public router and Parties handlers with substituted Dapr transport, verify sanitized HTTP 400 responses for sizes 101/200 and reject size 201 before routing.
- Gateway compatibility intentionally preserves `reasonCode=query_invalid_page` for both categories. The response `reason` distinguishes `invalid-page` from `invalid-cursor`, with corresponding safe `detail` text.
- The only repository change is this fresh verification record; investigation and execution revealed no correction needed in production code or tests.
- Build: `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -m:1` — exit 0, zero warnings/errors. SDK 10.0.403 satisfies the pinned SDK's `latestPatch` roll-forward. Restored Contracts, Server and Testing use EventStore 3.128.4 packages; this run qualifies Debug/package behavior.
- Tests invoke `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll` with `-failSkips` and the following filters. All 68 cases passed, with zero failures, errors, skipped or not-run cases:
  - `-method '*PartySdkQueryHandlerTests.Paging_*' -xml ${PRIVATE_RESULTS}/handler-paging.xml` — 56 passed. The runner accepted `-xml` with a deprecation notice.
  - `-method '*EventStoreGatewayRoutingTests.PostQueries_ActualPagingHandlerFailure_MapsSafeProblemAsync' -result-xml ${PRIVATE_RESULTS}/gateway-handler-failures.xml` — 10 passed.
  - `-method '*EventStoreGatewayRoutingTests.PostQueries_PageSizeAbovePublicMaximum_RejectsBeforeHandlerAsync' -result-xml ${PRIVATE_RESULTS}/gateway-public-maximum.xml` — 2 passed.
- One initial gateway command used the unsupported interior wildcard `*EventStoreGatewayRoutingTests.PostQueries_*Pag*` and exited 3 before executing tests. The two explicit method filters above corrected the invocation. Raw output, per-test XML and command/results JSON are retained in the private run directory represented by `${PRIVATE_RESULTS}`.
- These focused controls establish the existing query and gateway behavior; full-suite, source-mapped and live-topology qualification were not part of this verification-only run.
- Review follow-up: retained [the sanitized validation receipt](tests/dw-27-verification-2026-10-29/validation.json), including exact commands, 68 per-test outcomes, selected package identities, source/executed-assembly hashes, original private XML/log checksums and successful pinned commitlint evidence. This receipt accompanies the spec as the verification-only change.
- Commit message `docs: verify completed DW-27 paging behavior` passed the installed repository-pinned commitlint CLI 21.2.2 with zero problems/warnings before use. The complete candidate, exact command, exit 0 and validator output are preserved in the receipt.

## Review Triage Log

- `medium` — The initial verification note referred only to private command/results JSON and XML, which future readers could not locate. Patched by retaining the sanitized receipt linked above with per-test outcomes and artifact identifiers/checksums; private raw paths and logs remain excluded.
- `false` — The review inferred uniform gateway responses for all sizes and page-size validation before every other failure. Existing implementation notes explicitly distinguish gateway handler failures for 101/200 from gateway ingress rejection at 201 and include the validation-precedence controls. `Paging_EnvelopeAndSearchModePrecedence_RemainsUnchangedAsync` passed for both list/search and verifies malformed envelope failures remain `InvalidEnvelope`, with unsupported search modes retaining `UnsupportedQueryType` before page-size checks. No behavior or frozen intent change is needed.
