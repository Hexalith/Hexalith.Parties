---
title: 'DW-35 whole-payload redaction checkpoints'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A wholly redacted event delivered as `json-redacted` with an empty object can deserialize into a default-valued event and change the read model. An unknown CLR event type instead blocks its checkpoint, even though no domain payload remains.

**Approach:** Complete DW-35 by recognizing whole-payload redaction before CLR type resolution: `json-redacted` containing a root empty object or historical literal null is checkpoint-only, yields no domain payload, produces a Redacted processing record, and emits the existing privacy-safe informational diagnostic. Preserve nonempty field-redacted payload application, ordinary JSON handling, corrupt-payload diagnostics, sequence validation, duplicate filtering, and persistence through the EventStore SDK. Cover known and unknown types, parameterless events, case-insensitive formats, JSON whitespace, continuation, persisted Detail/Processing/Index results, and rebuild agreement. Do not change the protection producer, command replay, wire contracts, dependency versions or gitlinks. Leave changes uncommitted under repository safeguards.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps, irreversible actions, migrations or new public APIs. Small footprint: an early shape check and documentation in PartySdkProjectionFold, focused projection tests, adjustment of the existing field-redaction compatibility fixture, a short architecture note, this spec and DW-35's ledger resolution. The existing 2026-09-06 ledger decision supplies the intended checkpoint-only policy. Use the oneshot route.

Code evidence: PartyPayloadProtectionService.RedactProtectedPayload emits an empty object for a root encrypted marker, while nested markers produce nonempty objects with nulled leaves; both use json-redacted. Preserve the nonempty case. Existing ProcessingActivityFold_DecodableRedactedEventKeepsMappedCategory uses PartyDeactivated (an empty object); change that compatibility fixture to a populated field-redacted event and separately test the newly corrected empty-object behavior. Keep existing malformed-redacted converter tests and literal-null diagnostics unchanged. JSON parsing failures during shape inspection fall through to the existing resolution/deserialization policy.

Implement in src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFold.cs. Reuse RecordingLogger and nearby stateful serialized-store handler fixtures in tests/Hexalith.Parties.Projections.Tests. Add one focused test class named PartySdkProjectionRedactedCheckpointTests. Persisted assertions must read actual stored models, not merely inspect write calls. Update docs/architecture.md with the shape distinction and full-rebuild guidance for previously misapplied default events.

Before changing executable code, inspect the exact Parties Aspire baseline and stop only the instance started for this run. Build Projections.Tests individually in Debug/source mode with -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false; invoke the built xUnit assembly for failing-before evidence, focused compatibility cases and the complete suite. Rebuild/run the service assembly when warranted by the fold's callers. Record exact commands/results and independent review triage; keep unrelated topology failures separate from focused checks. Do not stage, commit, push or modify other repositories.

Aspire baseline: started the exact Parties AppHost in isolated no-build mode, observed parties/eventstore/security Healthy, and confirmed parties readiness with a 20-second bounded wait. Tenants, parties-ui and eventstore-admin were Finished. Stopped this run's exact Parties AppHost successfully before building. A separate Timesheets AppHost was preserved. This checks the previously built local topology only; no full current-source topology qualification is claimed. Raw output is local under /tmp/dw-35-validation.

Implemented the shape-specific early branch before type resolution; ordinary and nonempty field-redacted deserialization keep their prior paths. After correcting lazy-enumeration and dictionary-order test assertions, baseline reproduction has 11/19 focused and 6/7 persistence failures. Final Debug/source projection build has zero warnings/errors; focused compatibility cases pass 125/125 and the full projection assembly passes 285/285 without skips. New tests cover real handler writes and deserialize stored Detail/Processing/Index models; rebuild plans agree semantically with that persisted live state. Service verification and independent review are pending.

The final Debug/source service build also has zero warnings/errors, and its full assembly passes 1157/1157 without failures/errors/skips. Projection and service checks total 1442 passing cases. Exact commands and corrected before/after results are in tests/dw-35-redacted-checkpoints-2026-10-18/validation.json. Independent review is running.

Independent blind review identified six verification gaps, all classified low and routed to patch: state-changing persisted continuation, malformed unknown redaction retry behavior, Failed-to-Redacted audit replacement, post-erasure persistence, useful nonempty field-redacted state, and repair of historically incorrect inactive projections through rebuild. Test improvements are in progress. Documentation also explains that earlier unresolved-only Processing slots retain the coordinated-slot guard and need a rebuild; live consistency checks are not weakened.

All six review verification improvements are complete. Final projection Debug/source build passes with zero warnings/errors; focused cases pass 131/131 and the full projection assembly passes 291/291 without failures/errors/skips. The earlier 1157/1157 service result applies to the byte-identical final production fold; review changed projection tests and documentation only. Total affected full-suite cases: 1448. Independent follow-up verified every correction and found no concrete new defect. No new deferred work remains.

Existing post-erasure processing history retains its existing contract: new redacted tail events advance checkpoints without adding records or restoring erased domain data. Historical wrong inactive Detail/Index state is repaired by executed rebuild plans and persisted read-back; already-checkpointed live redelivery is proven insufficient. No production data was mutated. Dependency versions, gitlinks, producer output and command replay remain unchanged; all changes remain uncommitted under repository safeguards.

## Review Triage Log

- R1 — low, patch: continuation used a state-neutral event. A state-changing display-name tail now asserts persisted names, timestamps and history without masking the deactivation regression.
- R2 — low, patch: unknown malformed redacted data lacked a control. Direct fold and real-handler cases prove it remains unresolved/retryable.
- R3 — low, patch: Failed-to-Redacted audit replacement lacked persisted proof. Same-sequence redelivery replaces one record and advances all stored checkpoints.
- R4 — low, patch: post-erasure persistence lacked root-redaction coverage. Known/unknown redacted tails preserve tombstones, absent personal state/index entries and existing audit records, without adding new tail records. No historical-record deletion requirement was introduced.
- R5 — low, patch: nonempty field-redaction tests stopped at deserialization. Surviving organization type now persists in Detail/Index with a Succeeded processing outcome.
- R6 — low, patch: recovery documentation lacked historically incorrect-state proof. Executed full rebuild plans repair seeded inactive models and audit outcomes; stored read-back verifies the correction.

Independent follow-up confirmed all six findings resolved. The exact verification receipts are in tests/dw-35-redacted-checkpoints-2026-10-18/validation.json. DW-35 is complete; no other deferred item is closed by this change.
