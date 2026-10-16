# Consent renewal verification — 2026-10-16

The same-channel renewal follow-up appended by DW-120 is closed. Grant → revoke
→ regrant → revoke closes the current grant; prior grant and revocation evidence
is preserved. Historical cross-channel ownership conflicts remain rejected.

Final Debug builds use `UseHexalithProjectReferences=true`, `UseNuGetDeps=false`
and `-m:1`, with zero warnings/errors. Built xUnit v3 assemblies are invoked
directly, following the repository test guidance.

| Gate | Result |
| --- | --- |
| Focused server consent, renewal and DW-120 collision tests | 71/71 |
| DW-119 validation/processor tests | 86/86 |
| Contract identifiers, state and public API snapshot | 66/66 |
| Full Hexalith.Parties.Server.Tests | 302/302 |
| Full Hexalith.Parties.Tests | 978/978 |
| Full Hexalith.Parties.Projections.Tests | 238/238 |

No tests were skipped. Thirteen new cases exercise repeated renewal, withdrawal
retries, retained historical evidence, exact grant/withdrawal replay, backwards
clocks, equivalent timestamp offsets, legacy repeated rows, revoked/removed
foreign owners in either history position, incremental persisted projection,
duplicate delivery, and full rebuild repair of old missing/overwritten history.
The rebuild test executes both batch operations and reads persisted detail and
processing records, retaining the complete original event count.

[validation.json](validation.json) records exact commands, UTC execution times,
exit codes, runner counts, summary hashes, final source/test hashes and unchanged
invariant groups against the full canonical base revision. The base revision is
not claimed to contain these uncommitted changes. Contract wire definitions,
dependency versions/gitlinks, DW-119 validation and DW-120 protections/evidence
are unchanged. All seven review findings and their dispositions are in
[the spec](../../spec-same-channel-consent-renewal.md); independent follow-up
found no remaining concrete issue in the supported workflow.

The initial server build failed with CS9035 because three new PartyDetail
fixtures omitted required type/display/sort fields (nine compiler errors).
The corrected fixtures compile cleanly. The failed command summary is retained;
no existing assertion was weakened, and no behavioral test failed.

State and projection share an internal consent fold. Each accepted renewal adds
a ledger row. Withdrawal closes every active same-owner row and never overwrites
past revocation evidence. Exact retained grant/revocation evidence is replay;
equivalent DateTimeOffset offsets identify the same instant. Ignored divergent
retries rely on SDK sequence checkpoints for duplicate transport delivery and
must not be inserted again with new sequence numbers. Full ordered replay
canonicalizes accidental duplicate rows; existing legacy rows are retained and
closed during withdrawal. The original event stream remains the complete audit
source. See [the model documentation](../../../../docs/data-models.md).

Existing persisted projections that skipped a renewal need a full SDK projection
rebuild from original history. Later delivery below their checkpoint cannot
restore missing evidence. This upgrade boundary is documented and tested;
no external migration or rebuild was executed here.

The baseline Aspire observation started existing binaries without a build;
resource inspection showed waiting resources. It is operational context, not
current-source topology qualification. The owned AppHost was stopped before
edits/builds. Changes remain local and uncommitted. No push.
