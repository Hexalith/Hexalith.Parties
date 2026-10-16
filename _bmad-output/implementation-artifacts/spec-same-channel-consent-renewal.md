---
title: 'Resolve same-channel consent renewal lifecycle'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix the follow-up appended by DW-120: grant → revoke → regrant → revoke must revoke the current same-channel grant, with aggregate state and consent projections agreeing and historical consent evidence retained. Keep deterministic consent IDs, command/event wire shapes, dependency versions, DW-119 validation and DW-120 collision protections. Cross-channel ownership conflicts, including removed channels and revoked historical owners, must continue to reject.

Use the existing ordered event stream as authority. Exact immutable grant evidence identifies replay; it must not reopen a revoked grant. Same-owner active grant retries remain no-ops, including divergent retry payloads; original events remain the complete audit source. A genuine regrant appends history. Revocation closes all active records for an unambiguous owner, preserving already-revoked rows. Exact historical revocation evidence is replay and cannot revoke a renewal. Ambiguous historical revocation events cannot choose an owner and leave records unchanged. Byte-identical new lifecycle events cannot be distinguished from replay without changing the wire contract, so treat them as replay. Use event order, avoiding assumptions about monotonic wall clocks.

Add regression coverage for renewal, repeated revocation, ordered replay and duplicate delivery, existing repeated rows, projected persistence/rebuild convergence, and historical ownership conflicts. Run Debug source-reference builds, focused consent tests, full Server.Tests and full Parties.Tests assemblies. Investigate failures without weakening assertions. Update evidence and close the deferred follow-up only after all gates pass. Do not push.

</frozen-after-approval>

## Implementation Notes

- Investigation: PartyAggregate.Handle(RevokeConsent) selects the first row; PartyState appends every grant and revokes only the first row; PartyDetailProjectionHandler suppresses all subsequent grants by ID. Existing projection tests require active divergent retries to preserve the original row. DW-120 ownership checks inspect all historical rows and must remain before no-op checks.
- Small, reversible local change; no unresolved intent questions, migrations, external data mutations, dependency changes, or new public API. Use an internal shared ConsentHistory fold in Contracts (friend access for Projections), existing PartyState.Apply overloads, and projection handlers. Keep rejection Apply ordering intact.
- Verification: build each touched test project using `dotnet build <project> -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; invoke built xUnit assemblies directly with `-class` for focused cases and no filter for required full assemblies. Retain commands, timestamps, counts and hashes in tests/consent-renewal-2026-10-16.
- Pre-edit Aspire observation used existing binaries with `aspire start --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --no-build --non-interactive --format Json`; startup succeeded. `aspire describe` showed waiting resources; this is operational context, not topology qualification. The owned AppHost was stopped successfully before edits/builds. No dependency update.

- Implemented ConsentHistory shared fold, PartyState delegation, aggregate all-row active check, projection delegation, lifecycle regressions, and persisted incremental/rebuild tests. Public contracts remain unchanged; friend visibility is internal only.
- Initial server build failed with CS9035 in three new PartyDetail fixtures (nine missing-required-member errors); set their required type/display/sort fields. Corrected build passed with zero warnings/errors, without changing production or test assertions.
- Review clarified upgrade/replay limits: old checkpointed projections need explicit full rebuild. Added a seeded pre-fix read-model test. Exact row evidence replay protection applies to retained evidence; ignored divergent retries rely on sequence checkpoints and must not be forged as new events. Canonical full replay need not reproduce accidental legacy duplicate-row multiplicity; withdrawal preserves those stored rows and closes them all.
- Review follow-up adds persisted application/read-back of rebuild operations, explicit canonical-versus-legacy row assertions, and equivalent-offset timestamp replay coverage. Timestamp identity follows DateTimeOffset instant equality and preserves stored offsets. The old projected first-row revocation overwrite is also seeded and repaired by rebuild.

## Review Triage Log

- Medium, patched: old checkpointed projections cannot recover omitted renewals from later delivery. Document full SDK rebuild, seed the old missing-row/overwritten-revocation state, and prove full rebuild restores original history.
- Low, patched: legacy repeated-row multiplicity differs from canonical replay. Document and assert retained legacy rows all close while full replay deduplicates retries; accepted lifecycle evidence and active status agree, and original event payloads remain in the audit stream.
- Low wording gap, patched: ignored divergent active grants are not retained fingerprints. Such events cannot originate from normal RecordConsent commands; document ordered replay and sequence-checkpoint duplicate-delivery scope, and cover ignored events in checkpoint replay.
- Low wording gap, patched: ignored divergent already-revoked withdrawals likewise cannot originate from RevokeConsent commands. Retained-evidence checks do not promise arbitrary new-sequence insertion of ignored payloads; documented and checkpoint-covered.
- Low, patched: timestamp offsets were unspecified. UTC-instant equality is intentional; equivalent-offset grant and withdrawal replay regression preserves stored evidence.
- Medium, patched in final evidence: HEAD identifies the clean base rather than the uncommitted implementation. Record final SHA-256 hashes for changed source/tests and invariant files with exact gate results.
- Medium, patched: prepared rebuild candidate alone does not establish persistence. Execute both rebuild slot operations through the existing batch-store seam, read back detail and processing slots, and assert complete convergence and original event count.

## Verification

Final Debug source-reference server, projection, integration, and contract test builds pass with zero warnings/errors. Focused server consent/collision tests pass 71/71, DW-119 validation tests 86/86, and contract/state/public-API tests 66/66. Full Server.Tests passes 302/302, full Parties.Tests 978/978, and full Projections.Tests 238/238; no skips. Independent review follow-up found no remaining concrete supported-workflow behavior issue. Exact commands, UTC timestamps, summary hashes, source hashes, invariant comparisons and dependency identities are retained in `tests/consent-renewal-2026-10-16/validation.json`.

The deferred follow-up is closed only after these gates passed. Existing skipped renewal history requires the documented explicit full rebuild; no external data migration was performed. All edits remain local and uncommitted, respecting the repository instruction against unrequested commits. No push.
