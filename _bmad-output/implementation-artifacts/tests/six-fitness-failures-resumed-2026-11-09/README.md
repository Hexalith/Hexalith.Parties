# Six fitness failures: resumed verification, 2026-11-09

All six equivalent current controls pass. Final selected-package Debug build:
zero warnings/errors. Full Parties service: 2,852 passed, zero failed, 13 existing
runtime skips. Documentation: 10/10. Projections: 466 passed, one existing
compatibility skip. Tenants Server: 1,875/1,875. Tenants floor controls: 48/48,
including ten persistent restore/build entry-target cases.

The spec remains **in-review**. Tenants Contracts retains exactly the same 205
failing identities before and after the test-only patch (813 versus 823 passed).
Missing nested Builds fixtures dominate; no nested submodule was initialized.
The latest CLI 4.45.0 source build fails HXW005 because Commons source duplicates
restored Commons 2.30.10. Earlier CLI 4.44.13 source results are retained with their
unsupported-toolchain diagnostics. Package success does not clear those gates.
Sandbox socket failures are retained separately from successful socket-enabled
retries. Native XML records every skip reason; no new runtime skip was added.

Tenants test fix is local main commit
`60c1bd50c63513a466be2c301b48d3c4f1d3cf72`. GitHub rejected its push with HTTP 403
for jpiquot. The dependency checkout was returned to Parties-selected
`d88aef6b52d7e8a2e2d81132b1d76b7e1d1f0f35`; the unpublished commit remains on
Tenants local main. No unpublished dependency pointer was adopted.

All 99 files across the original 36-file qualification and historical six-fix
packets match their original Git blobs. The frozen intent and existing
DW-50/82/168/130/149 entry bodies remain unchanged and open. Available custody
records retain their unapproved/blocked bindings. No installed custody, deployed
writer drain, historical reconciliation, expiry or runtime qualification follows
from these source/unit results. Concurrent work updates other ledger entries.

The three original review layers and corrective follow-ups are retained with
exact diff hashes. Independent receipt review exposed four acceptance gaps;
exact full-suite summaries, native case/skip inventories, floor argument tuples,
six connected/detached mutation expectations and mandatory current input bindings
now enforce them. The original gate and its valid case plus 19 corruptions passed in normal
and optimized Python (40 fixture cases). A second three-layer review adds complete
floor inventories, explicit allowed skips, failure-cause comparisons, build and
selection invocation checks, assembly-snapshot checks and revision/input bindings.
The expanded gate has one valid case plus 37 corruptions (76 cases in both modes).

```bash
python3 _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/verify-receipt.py --receipt _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/validation.json --root .
python3 -O _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/verify-receipt.py --receipt _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/validation.json --root .
python3 _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/test-verify-receipt.py --receipt _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/validation.json --root .
python3 -O _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/test-verify-receipt.py --receipt _bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/validation.json --root .
```

The gate needs the repository, its recorded input bytes and historical Git
objects. It checks this stamped evidence rather than certifying future edits.
`validation.json` retains exact argument vectors, working directories, cleared
mapping selectors, overrides, exits, compressed/raw hashes and native XML.
Text uses documented path-only substitutions and deterministic gzip. Structured
skip reasons are derived from XML; the raw collector used an incorrect nested
reason selector. Long xUnit argument displays can coincide, so full inventory
comparison preserves name multiplicities and requires unique per-run test IDs.

Historical build arguments/exits, omitted seam diagnostics and original reviewer
transcripts cannot be reconstructed. Fresh evidence is additive. Replay helpers
are retained in their final forms; temporary source mappings must be regenerated
through the pinned tool. Full ambient environments, per-mode binaries and complete
transitive binary provenance are not retained. This is an evidence consistency
gate, not signed execution attestation. No new work is deferred by this run.

Second review evidence uses the `second-review-*` files. Added input hashes match
the recorded observed Parties revision and selected Tenants revision. The bound
owner test source is that selected baseline; the unpublished test patch remains
separately retained. The assembly snapshot was collected before the corrected
package build, so its digest cannot prove the final executed binary. Its path,
properties and digest shape are now checked. The 205 failure diagnostics match
before/after after normalizing only Python fixture elapsed durations. This does
not broaden the historical environment qualification. Fresh Tenants publication
preflight still returns HTTP 403. The retained .NET runs were not repeated here.
