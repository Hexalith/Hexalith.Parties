# Reviewed producer restart qualification — 2026-11-16

This packet binds the final review fixes to a new frozen prepared cohort.
The parent independently rebuilt both test projects, prepared the exact hosted
runtime, and reran every selected lane from the approved spec. The parent
directory preserves the earlier implementation and diagnostic attempts.

| Check | Passed | Failed / skipped |
| --- | ---: | ---: |
| Full owned wrapper | 1 | 0 / 0 |
| Authenticated runtime child | 1 | 0 / 0 |
| Controlled coordination cancellation | 1 | 0 / 0 |
| Consumer protocol / authority / runtime / inventory | 58 | 0 / 0 |
| Real-process signals and owned cleanup | 10 | 0 / 0 |

All selectors use `-parallelMode none -failSkips -result-xml`; errors and
not-run counts are also zero. Each live selector uses the same exact executed
identities. Individual explicit-cohort and retained-default Release builds
have zero warnings/errors. Defaults are compile evidence only.

| Independently frozen phase | Replacement elapsed (ms) |
| --- | ---: |
| cross-source-authority | 11374 |
| same-uri-refresh | 11077 |
| admitted-empty-mappings | 311043 |
| output-known | 311705 |
| accepted-deletion | 10679 |
| bounded-legacy-discovery | 11273 |

The prepared manifest SHA-256 is
`e3fb0bf07ca658f402c16d737fc613d124b86c625eaec56b26aa005f6a0fb3f3`.
`producer-restarts.json` proves original PID/start exit, distinct healthy
replacement, unchanged three backing containers, gateway/domain-service and
three sidecar lifetimes, producer bytes and authority generation/exclusion.
The five-minute replay guard remains enabled at both active-ingestion phases;
`replay-guard-events.json` retains events 9171 and both 9172 timeouts.

Review corrections add independent proof for the delayed output's own completed
deletion workflow, unchanged authority and receipt. The interruption receipt
retains the running accepted deletion, present unit and absent deletion receipt.
Cross-source historical hash bytes, provenance and both settled inventories are
captured before replacement and compared afterward. Both legacy refusals
reread persisted SDK obligations. Pending-start cancellation tests wait for the
late real process and prove its exact-incarnation exit; failed termination cannot
skip coordination cancellation, settlement, quiescence or evidence persistence.
Python/kernel pidfd prerequisites and a harmless preflight are documented.

`authority.json` retains 21 independent observations.
`owner-component-baseline.json` timestamps historical artifacts and six original
metadata records before the first restart. The isolated direct-owner component
is distinct from the main/survivor API scopes; its intentionally unknown obligation
survives until owned backing disposal. No source-wide completeness is invented.
Both API teardown receipts prove terminal unit/tenant absence, remote safety,
conditional state removal and no operator cleanup. Controlled cancellation keeps
Aspire running and proves replacement quiescence before backing removal.
`process-closure.json` independently confirms 21 exact process
incarnations and 6 backing containers absent, no session-named
containers remaining, and all runtime/outer scratch removed after child exit.

`commands.json` retains build, preparation and focused argv; `runtime-commands.json`
retains exact live-selector argv and explicit environment. `executed-identities.json`,
`prepared.json`, `source-identities.json`, `implementation-sha256.json` and
`owner-shared-dependencies.json` bind the source baseline, final working-tree code,
executed files and exact three shared owner boundary assemblies. Paths normalize
to `${REPOSITORY}`, `${ARTIFACTS}` and private/job placeholders; raw runtime
configuration, credentials and content-bearing workflow inputs are excluded.
`commitlint.json` and its log retain successful exact-message validation with
the pinned 21.2.4 CLI on Node 24.22.1. `SHA256SUMS` covers this packet.

All three independent review layers completed. Eight verified findings were
patched and checked; two findings were refuted. The remaining dependency-loader
claim is medium-unverified, separately recorded in the deferred ledger with the
evidence needed to settle it. No additional divergent dependency was demonstrated.

The first parent rerun exposed the new deletion-terminal assertion racing Dapr's
terminal publication after physical cleanup. Its child/wrapper failure XML,
exact source/executed identities and independently successful disposal remain in
`failed-terminal-publication-attempt.json` and the two `failed-terminal-*.xml.gz`
files. The correction waits under a bounded cancellation-aware deadline for the
exact workflow's Completed status. Those failed diagnostics are excluded from
the passing counts above; RUNNING never qualifies as terminal success.

The original source Debug NU1109 and package-preparation HXW011/HXW010 blockers
remain documented in `../commands.json`; no dependency gate was changed.
Global runner SIGINT cleanup remains unqualified: its original failed diagnostics
and separate exact-owned reconciliation remain in the parent packet.
Historical production inventory, older-writer exclusion, source publication/
package adoption, deployed key/trust/workload custody and owner acceptance remain
open. This qualifies the five local current-source restart boundaries only.
