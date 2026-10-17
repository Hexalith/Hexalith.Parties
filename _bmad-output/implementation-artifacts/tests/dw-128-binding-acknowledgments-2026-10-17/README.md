# DW-128 / DW-147 acknowledgment validation

The shared identity command client now requires the response correlation ID to match the fresh outgoing attempt. A supplied non-null message ID must also match. Every case-insensitive ID occurrence is checked before deserialization, preventing duplicate fields from hiding a foreign value. Explicit success/rejection flags must be correctly typed and non-rejecting. Failures use the existing fixed 503 identity-unavailable exception.

Correlation-only and explicit null-message acknowledgments remain supported, as do enriched binding replies, case-insensitive field names, and HTTP 200/202. Retries preserve logical intent while using distinct delivery IDs. Existing provisioning result validation, cancellation propagation, and HTTP failure mapping are covered.

Before the production fix, 51 of 124 focused identity tests failed because invalid responses completed successfully. The initial fix passed 124/124 focused tests and 280/280 full client tests. Following independent review, duplicate-field validation and direct stale-retry, enriched-result, cancellation-read and HTTP-failure tests were added. The final full client assembly passes **301/301**, including **145/145** identity tests, with no failures, errors, skips or unexecuted tests.

Final individual Client.Tests Debug/source build and Client Release/package build both pass with zero warnings/errors. Package-mode restore selects EventStore.Contracts 3.124.0. The [machine-readable receipt](validation.json) records exact commands, the canonical base revision and gitlinks, result counts, and final source SHA-256 hashes. Raw xUnit XML/build logs remain local under `/tmp/dw-128-validation`.

Independent review reported four findings; all were patched, and follow-up found no concrete remaining regression. No new work was deferred. The [implementation spec](../../spec-dw-128-binding-command-acknowledgments.md) contains the triage log.

The owned isolated Aspire baseline reached Healthy for EventStore and Parties. EventStore admin and Tenants exited 134; UI exited 150. The AppHost stopped normally before compilation. This transport fix does not claim full-topology or live gateway qualification. No dependencies changed; work remains uncommitted and unpushed under the user's repository safeguards.
