# Current six-failure verification — 2026-11-09

All six equivalent controls pass in the current mapped-source Debug build.
Both current builds finish with zero warnings and errors. Parties fitness passes
304/304; full service passes 2,868 with 13 existing runtime skips (2,881 total).
Tenants Contracts passes 832 and fails 205 (1,037 total); the failing identities
exactly match the retained baseline. Missing nested Builds fixtures remain the
full-suite blocker. No nested submodule was initialized and no dependency changed.

The receipt checker now binds the exact sanitized payload inventory and recorded
input map, checks derived failure/skip arrays, requires requested evaluated mapping
hashes, and verifies the retained third-review companion. The expanded suite passes
64 cases in normal and optimized Python (128 total). All three independent
reviewers clear correction SHA-256
`d1dee87f58d46066a8fd936bde27b904540da553482317c06bb1f4f4fb577dae`.
Independent source-predicate/mapping-hash mutations are caught by the intended
semantic checks; sanitized-only native report substitutions are also refused.

Run the portable historical replay from the repository root:

```bash
python3 _bmad-output/implementation-artifacts/tests/six-fitness-failures-current-2026-11-09/replay.py
```

It reconstructs the exact 21 recorded inputs from their owning Git objects in a
temporary directory and runs the historical gate and all fixtures in both Python
modes. Current historical-packet preservation, frozen intent and protected deferred
entries are checked. It writes no active source files and initializes no submodule.
The original historical manifest and all 99 protected historical files remain
unchanged. The original gate correctly refuses today's changed source inputs;
historical replay does not qualify those inputs as the current dependency graph.

Current commands, source mapping, logs, native XML, original/sanitized/archive
digests, reviewed diff, clearance reports and limits are in `verification.json`
and adjacent archives. The fresh service results were collected before the
receipt-only corrections; no subsequent product or test-source edits occurred.
They qualify this shared workspace, not deployment, custody, expiry, historical
writer drain, reviewer authentication or complete binary provenance. The checksum
manifest provides an executable artifact-consistency check:

```bash
cd _bmad-output/implementation-artifacts/tests/six-fitness-failures-current-2026-11-09
sha256sum --check SHA256SUMS
```

GitHub now reports Tenants push permission. A real Git dry run reaches the remote
and refuses non-fast-forward local main, which has one local and three incoming
commits. The selected owner checkout and local main remain unchanged; this packet
publishes no owner change or dependency adoption. The earlier HTTP 403 remains
historical. No production acceptance is inferred. DW-50/82/168/130/149 remain open;
no new work is deferred. The spec remains **in-review** because its full owner
Contracts gate is still failing.
