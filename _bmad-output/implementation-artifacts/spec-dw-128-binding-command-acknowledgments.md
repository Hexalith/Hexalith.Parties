---
title: 'DW-128 identity binding command acknowledgments'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-128 and its duplicate DW-147 track identity binding methods reporting success for empty or unrelated successful HTTP objects. The shared submission path deserializes a response without requiring an acknowledgment of the submitted attempt.

**Approach:** In `HttpPartiesIdentityClient.SubmitAsync`, require the response correlation ID to equal the fresh outgoing attempt ID using ordinal comparison. Preserve omitted/null optional message IDs; require a supplied message ID to match that attempt. Preserve correlation-only acknowledgments, optional binding result payloads, successful HTTP statuses, case-insensitive envelope field reads, explicit rejection handling, cancellation, and HTTP failure mapping. Reject malformed explicit success/rejected flags, including differently cased flags, with the existing fixed identity-unavailable exception. Keep provisioning's existing typed-result validation. Verify establish/rebind/revoke rejection and acceptance, fresh IDs across retries with stable logical intent, and provisioning compatibility. Complete only DW-128/DW-147; leave other ledger work unchanged.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps, irreversible operations, migrations, dependency updates, or public API changes. Small footprint: the existing identity client and identity client tests, this spec, the two ledger entries, and compact validation evidence. Use the oneshot route.

Investigation: `SubmitCommandResponse` contains required constructor parameter `CorrelationId`, optional `ResultPayload`, and optional `MessageId`, but deserialization permits missing constructor fields. Local `dotnet-inspect` verified the identical response API in cached EventStore 3.120.0 and currently selected 3.124.0. `CommandsController` forwards body correlation and returns HTTP 202 for synchronous and asynchronous paths; `SubmitCommandHandler` preserves it for normal, pending, and replay results. Message ID equality relies on this client's fresh delivery IDs and absence of an IdempotencyKey. Generic keyed gateway replay has a different message-ID contract and is outside this client change. `PartiesJsonOptions.Default` preserves case-insensitive DTO reads. Reuse the existing asynchronous test Handler and immutable command contracts.

Validation plan: individual Client.Tests Debug build with `UseHexalithProjectReferences=true`, focused identity tests before and after the fix, then the full Client.Tests assembly. Check package-reference Release compatibility separately because the shared helper also serves provisioning. Never run solution-level dotnet test or weaken warnings-as-errors. Preserve source line endings.

Owned baseline: isolated no-build Aspire start succeeded; `aspire wait parties --status healthy --timeout 30` succeeded. EventStore and Parties were Running/Healthy; eventstore-admin and Tenants exited 134, UI exited 150. This is not full-topology acceptance. Exact-target ordinary stop succeeded before compilation. Raw baseline resource state is local at `/tmp/dw-128-validation/aspire-baseline.json`; no startup credentials belong in tracked evidence.

Repository is clean on main at the start of implementation. No Git publication or dependency mutation is requested; leave the result uncommitted under the user's repository safeguards.

Implementation: the shared SubmitAsync path now validates all supplied correlation/message ID occurrences against the fresh attempt before deserialization, then verifies the typed acknowledgment. Optional message IDs remain compatible. Explicit flags are case-insensitive and require true success / false rejected values when present. The existing fixed 503 exception contains no response body. Added focused response and retry tests to HttpPartiesIdentityClientTests and a single-type CancellationResponseContent helper for deterministic cancellation while buffering a response.

Initial focused tests reproduced 51 failures before the production change (73/124 pass); all 124 passed afterward. The initial full client suite passed 280/280. Review follow-up expanded coverage; final Debug/source Client.Tests build succeeds with zero warnings/errors, and the full assembly passes 301/301, including 145/145 identity tests, with zero errors/failures/skips/not-run. Final Release/package Client build succeeds with zero warnings/errors against EventStore.Contracts 3.124.0. CRLF-aware Git whitespace checking passes. See tests/dw-128-binding-acknowledgments-2026-10-17/validation.json for commands and final source hashes.

Independent review and follow-up are complete. All findings below were verified and patched; no new work was deferred. DW-128 and DW-147 are closed together. No other ledger item changed, and no commit or push was performed.

## Review Triage Log

- Medium, patch: duplicate/case-variant message or correlation properties could hide a foreign value when deserialization kept a later property. Validate every occurrence before deserialization; four duplicate-field scenarios run across all three operations.
- Low, patch: successful retry tests did not actually replay an earlier acknowledgment. Added a stale first-attempt reply on the second submission for establish, rebind and revoke; every second call rejects while logical payload remains stable.
- Low, patch: binding success tests omitted optional result payloads. Enriched acknowledgments now pass for all three operations while correlation-only acceptance remains covered.
- Low, patch: cancellation during response buffering and HTTP failures with acknowledgment-shaped bodies lacked direct assertions. Added deterministic cancellation cases for all three methods and 401/422/503 status-preservation controls. Independent follow-up confirmed all four findings resolved with no concrete regression.

Repository build guard `bash scripts/check-no-warning-override.sh` passes. Final JSON/source-hash/CRLF checks pass; only the two selected ledger records changed.
