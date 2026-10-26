# Published SDK and runtime closure — 2026-10-26

Baseline: Parties `463ad1bf14701f0622b68af4352e07941d1a7969`.
This packet records current executed consumer behavior separately from historical
owner receipts, source selection, runtime configuration and external release gates.

`package-provenance.json` records actual restored EventStore `3.128.0` package
identities, package hashes and repository revision
`c5d1c280d8c1c1a207cd159d817f614e93c93130`, including ancestry of both fixes.
It also records every unchanged root gitlink/checkout. The root pin is the only
dependency change; the pinned module tool retains source-mode authority.

`memories-server-policy-provenance.json` identifies the actual non-packable
Server project, observed AppHost project launch, executed assemblies, and exact
byte equivalence of the registered authentication slice plus six policy/test/
factory files with released `2.29.15` source
`38128624763c313238904c30aaeb88eb4fd9a6d0`. The selected project source is
`39be000902254c4717df6726bfcc3ea927b26b70`. No client-package upgrade is claimed
as Server policy proof. Domain-option reload remains supported; bearer policy
changes require restart.

The following table preserves the initial implementation runs. Review1 focused
results below supersede changed controls; complete final suite verification is
parent-owned.

| Executed control | Total | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Package recovery/query/cross-instance classes | 963 | 0 | 0 |
| Legacy, tombstone and newer ordinary-write recovery matrix | 12 | 0 | 0 |
| Registered selected Memories Server algorithm controls | 8 | 0 | 0 |
| Complete configured isolated gateway/ACL runtime | 3 | 0 | 0 |
| Complete Parties service suite | 2770 | 0 | 4 |
| Complete projection suite | 463 | 0 | 1 |
| Complete Integration suite | 165 | 0 | 21 |
| Cleanup and fixture controls | 32 | 0 | 4 |
| Complete selected Memories Server suite | 3057 | 5 | 1 |

The unskipped package matrix proves compacted writable ETags, next-tail progress,
interruption/restart and exact preservation of newer canonical bytes without
retaining personal manifests. The broader suite skips are existing optional
Memories runtimes, original-package compatibility, or explicitly required live
runtime profiles; none supplies qualification of those independent gates.
The registered algorithm controls exercise effective named bearer options and
real signed RS256/ES256 acceptance, HS256 denial and invalid startup.

The five full Memories-suite failures belong to unchanged owner evidence:
expired OpenBao C7 waiver on 2026-10-26, three stale EventStore `3.127.0`/
project-reference expectations, and changed AccessTelemetry closeout wording.
They remain failures in `validation.json`; authentication proof does not close
them or any corresponding deployment/security approval.

Original-name runtime attempts encountered another user topology's non-mTLS
Dapr discovery entries, yielding explicit TLS handshake failures instead of
ACL denials. `initial-failures.json` preserves all unsuccessful attempts.
The supported opt-in `PARTIES_MTLS_ISOLATED_IDENTITIES=true` selects existing
fixture-suffixed literal app IDs/private namespaces and remapped deny-default
ACLs. Metadata and invocation targets use those actual identities. It does not
change production ACL bytes or qualify original-name discovery coexistence.
The first isolated attempt exposed a worker observer race during fixture disposal;
its immutable declared/planned-name snapshot now retains actual PID/start custody
across live model mutation. The new controlled-worker regression and original
cleanup controls pass. A later startup listener collision is separately retained. Each fixed internal
sidecar socket is now explicitly declared as a proxyless TCP endpoint in the DCP
model before dynamic allocation. The endpoint regression and fresh complete
runtime pass. The passing runtime uses `parties-47f8635c1807` and
`parties-34e7f7c79fe7` namespaces; it asserts all eight actual app IDs and six real
Dapr PermissionDenied denials. All five selected ledger items are closed within
their stated scope; DW-50 is an explicit superseding open owner.

`cleanup.json` records verified quiescence and ownership-checked removal of the
first isolated failed session's retained control plane, followed by removal of
its dedicated credential/configuration directory. A final audit covers all fifteen
failed/prior/final runtime sessions: no owned workers, containers, networks or
private directories remain. No unrelated topology was stopped.

DW-50 and independent migration/reconciliation, authoritative inventory,
sidecar token publication, container/deployment and Memories refresh gates remain
open. All rollback seams are retained. Historical receipts remain attached to
their original revisions and do not certify the current package or source graph.

`validation.json` contains exact commands and sanitized summaries; raw log hashes
bind the private originals. Independent review, exact pinned commitlint evidence
and final commit/push are recorded by the parent workflow after implementation.

Review1 follow-up fixes are recorded in `review1-verification.json`. The focused
recovery class passes61/61, the matrix selection control passes1/1, and all four
actual live shutdown/cancellation/failure/cleanup-retry modes plus the existing
two-session persisted/cross-call/survivor test pass 5/5, each with zero skips.
The12 ordinary-write/delete interleavings now assert their exact phase/ordinal,
including both compaction participants, and preserve physical deletion. Two
additional controls import independently generated published3.127.0 marker and
envelope bytes, resume serialized interrupted recovery and advance the next tail.
The standalone old-package generator, exact deterministic bytes and provenance
are retained under `legacy-3.127.0/`; current package selection is unchanged.

The actual loaded projection and Client identities/hashes are captured during
those executions in `executed-assembly-hashes.json`. Review1 adds six owned runtime
sessions to the earlier15; the current cleanup receipt verifies all21 are absent.
The earlier combined-filter zero-test run remains explicitly unsuccessful and
unqualified in `initial-failures.json`. The development guide now describes the
new package upgrade erasure refusal, its exact reason code and required operator
reconciliation before rollout, preserving the default-false guard.

Parent final verification is in `parent-final-verification.json`: the final service
suite has 2,780 cases, zero failures and four unrelated opt-in skips; all three
configured runtime controls and eight algorithm controls pass without skips.
The full Release solution has zero warnings/errors. Live lifecycle/concurrency
review controls pass 5/5. All 23 task runtime sessions are cleaned. The commit
is marked breaking for the newly active package erasure attestation prerequisite.
Five unrelated full Memories owner evidence failures remain separate.
