---
title: 'DW-127 ineligible identity response compatibility'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-127 and duplicate DW-146 track incompatible producer/client identity denial responses. The current resolver already omits human bindings for inactive, restricted and erasing humans, but still returns valid Unknown classification evidence with Ineligible, which the public client incorrectly translates into Unavailable/503.

**Approach:** Accept Unknown classification evidence only for Ineligible responses, preserving exact scope, source observation, supported classification, transport and binding validation. Ineligible responses never carry usable human bindings; Unknown never becomes Resolved. Document this existing denial contract and prove actual resolver/query-handler output survives gateway serialization and the real typed client for inactive, restricted, erasing, erased and Unknown Parties, with valid resolved controls. Cover malformed denial responses independently. Close DW-127/DW-146 together only after focused and affected full-suite verification. Preserve admission, policy, persistence, current/historical eligibility, wire shapes and dependencies; do not claim installed identity availability from fixtures.

</frozen-after-approval>

## Implementation Notes

Planning classification: one cohesive response-compatibility repair, no unresolved intent gap or irreversible action. Small client predicate change, contract XML documentation, existing client/gateway tests, one test-only Client project reference and a focused HTTP bridge helper. No production API signature, dependency version, gitlink, migration, deployment or production mutation. Use the oneshot route; leave source changes uncommitted under the repository safeguards.

Code map: HttpPartiesIdentityClient.ResolvePartyIdentityAsync globally restricts evidence classification to Human/Organization; allow Unknown only alongside Ineligible without relaxing other checks. PartyIdentityQueryService.ResolveAsync already requires active, unrestricted, nonerasing state before obtaining a human binding and returns Ineligible for Unknown. PartyIdentityResult documents the denial evidence contract. Reuse PartyIdentityQueryHandlerTests.Service/Envelope fixtures and PartyIdentityQueryHandler.ExecuteAsync; serialize the resulting payload through SubmitQueryResponse and send via HttpPartiesIdentityClient. Add a Client project reference to Parties.Tests rather than copying producer logic. Use a separately named HTTP handler type to keep one C# type per file. Expand existing HttpPartiesIdentityClientTests to reject Unknown Resolved, undefined/Agent classification, any denial binding, foreign/malformed/future/degraded observations. Existing human denial tests and resolved/historical client tests must remain green.

Acceptance: Given an admitted complete observation of an ineligible Human/Organization or Unknown Party, when the real producer response passes through gateway JSON and the typed client, then Ineligible and safe scoped evidence survive with no binding. Given an active authorized Human or Organization, the same path still resolves. Given malformed or foreign denial evidence, unsupported classification, or a human binding on any denial outcome, the client still fails closed with 503. Given caller cancellation or unavailable source, existing behavior remains unchanged.

Validation: establish a focused red reproduction against the unchanged client; Debug source-reference builds with warnings as errors and direct xUnit v3 class runs, then full Client.Tests and Parties.Tests assemblies. Record exact commands/counts, review findings and any independent broad-gate blocker in tests/dw-127-ineligible-identity-round-trip-2026-10-17/validation.json. Run whitespace and build-policy checks after the final change.

Runtime baseline: exact Parties AppHost started with aspire start --no-build. aspire wait parties --status healthy --timeout 15 timed out: security Running/Unhealthy and Parties/EventStore/Tenants/UI Waiting. Exact-target ordinary aspire stop succeeded before any source edit. This is not a qualified full-topology result.

Initial reproduction: the new client class run failed only Unknown denial (165/166 passed), and the actual query-handler/client round trip failed exactly five Unknown lifecycle cases (11/16 passed). The first gateway-test build found missing ConfigureAwait(false) in the new HTTP helper; corrected both awaits, then rebuilt with zero warnings/errors. The production fix extends the supported classification set with Unknown and separately restricts it to Ineligible; all preexisting source, scope, observation and nonresolved-binding guards remain intact. Contract XML documents this outcome-specific rule.

The first full Client run passes 322/322. The first full Parties run passes 1025/1027, including all 16 new round trips; the two failures are the preexisting dependency-observation and maintained-technology checks already documented under DW-123. To satisfy the completion gate, refresh only current observed source identities/catalog expectations and maintained Dapr/EventStore documentation. Preserve the 2026-10-16 source section as dated history; add a new 2026-10-17 observation at the actual committed graph, without recertifying owner approvals, historical parity, package releases or runtime custody. No dependency or gitlink changes. This prerequisite adds the existing matrix/fitness/docs files to the implementation footprint but introduces no independent product behavior.

A no-restore producer rebuild after the client package tests exposed mixed Commons.UniqueIds build assets (CS1704); explicit source-mode restore/build recovered with zero warnings/errors. The raw-wire test subsequently exposed CA2007 on a stored Task; ConfigureAwait(true) satisfies both CA2007 and xUnit1030 without weakening analyzers.

## Review Triage Log

- Medium, patch: omitted classification defaults to Unknown and became accepted after extending classification handling. Require explicit classification on the existing evidence property and add raw JSON omission/null plus camel/Pascal/numeric compatibility tests. No field or signature is added.
- Low, patch: the binding rejection tests covered Ineligible but did not independently isolate Unavailable/Conflict carrying otherwise valid Human evidence. Add all three nonresolved outcomes.
- Low, patch: round-trip assertions did not explicitly compare ContractVersion and the exact source ObservedAt. Add both against the actual source fixture.
- Low, patch: the evidence property's positive-classification documentation contradicts outcome-specific Unknown denial. Clarify it alongside the required-presence annotation.

- Low, patch (follow-up): the architecture table retained stale present-tense EventStore/Tenants selections beside the refreshed toolkit row. Align current package/source observations and retain the dated October 8 Tenants identity explicitly as history. Independent follow-up confirmed all four original patches and all current root identities/describes; no additional implementation concern was found.

Final verification: Debug/source Client.Tests passes 330/330 (174 identity cases), Parties.Tests passes 1027/1027 (all 16 new producer/client cases), and Contracts.Tests passes 199/199. All final builds have zero warnings/errors and all tests have zero skips, errors or failures. Current dependency/doc baseline checks pass after observing the committed graph, without changing dependencies or approvals. Independent review and two follow-ups are complete with no outstanding finding or new deferred work. The validation packet retains exact commands, intermediate analyzer/asset failures, red reproductions, final results and source hashes. DW-127/DW-146 close together; runtime qualification remains unavailable. No staging, commit or push.
