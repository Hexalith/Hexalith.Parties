# Six fitness failures — 2026-10-28

All six failures in the original parent verification were reproduced and now
pass in the final existing fitness gates. Current observations describe actual
selected packages/source identities; historical observations and approvals keep
their original scope. All 36 original qualification files match their baseline
Git blobs. Root package selections and the deferred-work ledger are unchanged.

| Final check | Pass | Fail | Existing skip |
| --- | ---: | ---: | ---: |
| Parties fitness classes | 288 | 0 | 0 |
| Full Parties.Tests | 2797 | 0 | 4 |
| Full Parties.Projections.Tests | 466 | 0 | 1 |
| Tenants floor controls | 25 | 0 | 0 |
| Full Tenants.Server.Tests | 1584 | 0 | 0 |
| Full Tenants.Contracts.Tests | 578 | 193 | 0 |

Both parent builds and both final owner Debug builds have zero warnings/errors.
All five independent review findings are resolved, including the additional
upstream-floor recheck. Exact commands, counts, skip reasons, source/assembly
hashes, and deterministic compressed XML/logs are in `verification.json`.
`commitlint.json` records exact root messages verified with pinned CLI 21.2.2
and Node 24.22.1; the retained owner publication uses its pinned CLI 21.2.3.

The Tenants fix is published as `6deb5d5dbaad4e26405910b347d45b82deae1057`.
It preserves incoming main work and the approved 3.128.4 completion floor while
honoring newer versions and exact source authority. Local parent adoption
`bcc791d180b7fdc7566c7b52452d53cd6626f5a0` precedes final verification because
the source gates require a committed gitlink. That adoption grants no API owner
acceptance, historical parity recertification, custody or release approval.

The remaining broad-gate blocker is precise: from `references/Hexalith.Tenants`,
`dotnet tests/Hexalith.Tenants.Contracts.Tests/bin/Debug/net10.0/Hexalith.Tenants.Contracts.Tests.dll -noColor`
exits 1 with 193 failures. The unchanged publication/recovery/CI fixture classes
require absent nested `references/Hexalith.Builds` files, including
`Github/governed-provenance`. Baseline classes reproduce the same 193 identities
and retain matching source hashes. The baseline archive used a copied current
binary, not a rebuilt historical binary. No nested submodule was initialized.
The complete native XML and owner manifests retain every failing identity.

The four optional parent runtime skips require private owned Memories runtime
configuration; the projection compatibility skip retains its existing package
condition. No skips were added. The separate Aspire precheck started, but
`aspire wait parties --timeout 15` exited 17 while security was unhealthy and
services waited. The owned AppHost was stopped successfully; this is no complete
topology qualification.

DW-50/82/168/130/149 remain open: deployed writer drain, historical/terminal
reconciliation, accepted installed custody and complete authenticated expiry
qualification were not established. `owner-records.json` binds the independent
read-only record inspection to exact hashes; existing acceptance requirements
and blocking tests remain authoritative. The spec remains in-review because
the full owner contracts gate is not green.

Retained text uses UTF-8 path-only substitutions to `${PARTIES_ROOT}`,
`${OWNER_RECEIPTS}`, `${EXECUTION_RECEIPTS}`, `${WORKSPACE}` and `${USER_HOME}`,
then gzip with mtime 0. Manifests bind original, sanitized and compressed hashes.
Original raw execution files remain private; the historical packet is untouched.
