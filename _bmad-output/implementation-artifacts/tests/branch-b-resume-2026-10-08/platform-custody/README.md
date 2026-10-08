# Branch B Platform custody verifier continuation — 2026-10-08

The Local verifier now requires Platform's existing `CustodyPrerequisiteTests`
and `IdentityHistoryCleanupTests`. The runner checks both filters for actual
passing cases and rejects missing, skipped, failed or not-run lanes. Synthetic
custody fixtures do not provide production qualification.

The normal complete Local command exited **0** with **1,095 passing cases**
across **eleven owner lanes** and **40 required classes**, zero build warnings or
errors and zero failed/skipped/not-run cases. The required custody lane passed
**115** cases: 68 prerequisite and 47 cleanup tests. Coverage includes missing
provider registration, finite expiry, exact scope, lost-ack retry, unavailable
outcomes and bounded provider cancellation.

```powershell
pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory EXECUTION_ROOT/local -ArtifactsDirectory EXECUTION_ROOT/artifacts -MemoriesRoot EXECUTION_ROOT/optional-memories-package-mode
```

`EXECUTION_ROOT` is `/tmp/ext-parties-1-platform-custody-20261008-OlHwiz`. The [final manifest](final-verification-manifest.json)
retains exact per-owner build/test arguments and the source-root test environment.
[Original full Local output](local-command.log), [raw owner receipts](local/local-evidence.json),
[custody XML](local/Hexalith.Platform.Custody.Tests-tests.xml), [root independent
matrix/class audit](root-local-audit.json), [final independent source/XML audit](root-final-audit.json), [verification audit](verification-audit.json)
and [retained-file digest mapping](retained-file-manifest.json) preserve outcomes.

Six external EventStore test documents changed during the whole Local run.
Four were compiled by selected assemblies; original PDB hashes differed from
current bytes. Three affected lanes were normally rebuilt without exclusions or
restore overrides and reran **23/107/168** cases. [Refresh receipts](current-source-refresh/refresh-evidence.json)
replace their original selected counts, without double counting. The [original
mismatch audit](external-drift-compiled-audit.json) and [current audit](external-drift-compiled-current-audit.json)
retain both observations. The two ProviderVerification documents were not
compiled by this verifier.

[Compiled-source checks](current-compiled-source-audit.json) match all **40 required
test-class files** and **eight relevant production custody/domain/query files**.
Before/after inventories and [final observations](source-final-observation.json)
disclose external HEAD/UI/docs/test movement. These checks do not assert an
unchanged whole worktree or audit all possible generated sources.

The explicit isolated Parties AppHost started, `aspire wait parties --status
healthy --timeout 30` passed, resources were inspected and the owned AppHost
stopped. [Sanitized resources](baseline-resources.json) are local health evidence.
No identity endpoint or production mutation ran.

[Current Platform inspection](platform-owner-discovery.json) retains exact searches
and inspected-file hashes for sibling Platform
`f043a2f242762233091abdaa5bbe1ab777bd0f12` and root reference
`332a7d8104e3f6c9aaa57cbc7f07afb5fa0859de`. Both hosts register identity
services. Custody registration resolves an optional `IIdentityHistoryCustody`,
whose only implementation is a synthetic fixture; no production registration
or implementation was found in either current tree.

Platform Story 4.0 records existing OpenBao `openbao/hexalith-keys`, raft
snapshots, immutable off-node recovery storage and completed isolated restores
on October 1. Its gate expired October 2. That generic recovery proof does not
supply actor-history purpose inventory, irreversible all-copy destruction
receipts, nonrollback restore reconciliation or successor continuation. The
architecture requires independent lifecycle/key custody; current EXT-SECRETS
library defaults also refuse unavailable production custody.

[Live gate receipts](live-gates.json) record exit **1** for complete-Live missing
inputs, non-authoritative sentinel capability inputs and LiveReadiness missing
inputs, all before endpoint access. Qualified actor-history custody,
all-copy/destruction/restore guarantees, approved successor continuation and
complete installed P-01–P-10 persisted-state/restart/restore/failure-injection
interfaces and targets remain unresolved. The approved 365-day policy, both open
spec tasks, frozen intent, original baseline and `in-progress` status remain
unchanged. Installed availability is not established.
