---
title: 'DW-123 bounded not-found diagnostics'
type: 'bugfix'
created: '2026-10-17'
status: 'in-progress'
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

Planning classification: no unresolved intent gaps; no migration, deployment, dependency update, or external mutation. Small change to one existing aggregate and focused existing aggregate tests, plus this spec and the DW-123 ledger entry. No new public API. Existing not-found tests in `PartyAggregateContactChannelTests.cs`, `PartyAggregateIdentifierTests.cs`, and `PartyAggregateCompositeTests.cs` are the regression anchors. Run the individual Debug/source-reference Server.Tests project; broaden to Parties.Tests because the aggregate is used by the processor. Inspect and stop the owned Aspire baseline before compiling, following repository guidance.
