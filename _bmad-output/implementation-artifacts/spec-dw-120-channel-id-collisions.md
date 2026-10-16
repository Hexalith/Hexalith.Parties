---
title: 'Prevent case-only contact-channel ID collisions (DW-120)'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_revision: 'ba5a8486c16f0d3c89535c3206e3fe350389c127'
context: []
---

<frozen-after-approval reason="user-authorized DW-120 resolution">

## Intent

Prevent case-only contact-channel IDs from sharing a deterministic consent ID.
Reject case-insensitive channel collisions in standalone additions and composite
creation/updates, retaining exact-ID retry behavior. Preserve ordinal channel
addressing and historical event replay. Reject updates and consent grants for a
historically ambiguous channel family; allow exact removals to resolve channel
ambiguity. Before consent idempotency or emission, reject an ID already owned by
a different exact channel, including revoked history or a removed channel.
Reject revocation when the same stored consent ID has multiple channel owners;
allow unambiguous revocation even while channel ambiguity remains.

Preserve existing consent ID generation for nonconflicting states, command/event
wire shapes, dependency versions, restriction/erasure precedence, and DW-119.
Add regression tests for creation, updates, historical replay/removal, and
consent collisions. Run the Debug source-reference build, focused domain and
DW-119 tests, and the entire Hexalith.Parties.Tests assembly. Investigate failures
without weakening assertions. Retain evidence; close DW-120 only after every
required gate passes. Do not push.

</frozen-after-approval>

## Implementation Notes

- Investigation: all aggregate, state, and projection channel lookups use exact
  IDs. Composite commands currently skip exact duplicate additions. Keep those
  contracts and validate case-only conflicts before emitting any success events.
- Scope: PartyAggregate validation and private helpers, focused tests under
  Server.Tests/Aggregates, docs/api-contracts.md, and DW-120 evidence/ledger.
  No intent gaps, irreversible operations, new public API, or data migrations.
- Runtime baseline uses existing Debug binaries with Aspire --no-build; runtime
  health is operational context only. The requested builds establish source proof.
- Implemented fixed conflict rejections before success events and consent
  idempotency, retaining exact state/projection replay. Added a private aggregate
  partial for collision checks; no public contract changes.
- Independent review corrections preserve exact not-found precedence and exact
  addition retries in historical ambiguous states. Composite collision checks
  examine new additions after exact existence validation. Added lifecycle and
  historical ownership regressions without changing existing assertions.

## Review Triage Log

2026-10-16: The configured context-free blind-hunter review completed. No review
layers were skipped. All seven findings were checked against their handlers,
state Apply methods, result classification, and tests.

- **medium / patch:** Ambiguity checks masked exact not-found outcomes for an
  absent third casing. Moved standalone checking after exact lookup and composite
  checking after existence validation; regression cases cover update, removal,
  consent, and composite lookup in unique and ambiguous states.
- **medium / patch:** Exact addition retries rejected in historical case pairs.
  Restored standalone no-op first and filtered composite collision detection to
  additions absent by exact ID. Both historical variants remain skipped when
  retried together. The test explicitly asserts NoOp and no rejection/events.
- **low / patch; medium / defer:** Documentation overstated single-owner
  revocation. Qualified the collision guard's guarantee and documented the
  pre-existing grant/revoke/regrant first-record limitation. That existing
  lifecycle bug is deferred separately; DW-120 does not change record renewal.
- **low / reject:** A family index could replace per-operation channel scans.
  Composite payloads already have a 100-operation bound; no measured normal-use
  regression was demonstrated. Refactoring indexes adds complexity beyond a
  simple correction; retain the localized guards.
- **low / patch:** Added ambiguous-state tests for erasure and restriction
  precedence, including consent's existing restriction exemption.
- **low / patch:** Added later-owner detection after a same-owner first record.
  Used replay-reachable history (first owner revoked, later foreign owner active):
  Apply(ConsentRevoked) selects the first matching row, so the proposed inverse
  active/revoked ordering cannot be built by those events. The test proves that
  checking only the first owner or idempotency before ownership is unsafe.
- **low / patch:** Added removal and exact-ID readdition with retained consent,
  asserting unchanged ownership and idempotency.

## Verification

- Final Debug source-reference builds for Server.Tests and Parties.Tests pass
  with zero warnings/errors.
- Focused channel/consent suite: 138/138; new collision regressions: 32/32;
  complete Server.Tests assembly: 291/291.
- DW-119 focused validators/processor tests: 86/86; full Parties.Tests assembly:
  978/978. No skips, errors, or failures in the final gates.
- Targeted independent follow-up confirms both behavior corrections, coverage,
  qualified documentation, and complete triage; no remaining concrete issues.
- Exact commands, source/dependency hashes, failure diagnoses, portable runner
  summaries, and pinned commitlint evidence are retained in
  `tests/dw-120-channel-id-collisions-2026-10-16/validation.json` and its siblings.
- Contracts, dependency versions/gitlinks, and completed DW-119 fixes/evidence
  remain unchanged from the baseline. DW-120 is closed; no push.
