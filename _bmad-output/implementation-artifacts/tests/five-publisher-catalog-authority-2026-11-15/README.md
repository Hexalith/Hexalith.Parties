# Publisher, catalog and exact-unit authority — 2026-11-15

Final reviewed source passes 14 publisher controls, 120 CI controls, 108 actual
Redis/FalkorDB/Dapr integration controls and 99 Server controls, with no targeted
skips. The final exact 5.1/8/4/6 package cohort passes its native two-session
isolation check (1/1). All actionable review findings are resolved.

`final-verification.json` is the final receipt. It records final selectors, source
hashes, executed binary hashes where captured, review outcomes and native cleanup.
The three `final-*-executed-tests.json.gz` files retain every executed test name.
`verification.json`, the original three receipts and `source-inputs-sha256.json`
remain the earlier implementation snapshot; their smaller totals and pending
workflow statements are historical. `patch-verification.json` records the focused
51-control review patch, and `erasure-boundary-verification.json` explains the
controlled legacy-worker cutover correction.

The initial implementation snapshot passed 14 publisher controls, 120 CI controls,
87 actual Redis/FalkorDB/Dapr integration controls and 56 focused Server controls,
with zero targeted skips. The explicitly selected EventStore 5.1.0 Release/package
native runtime passes its two-session isolation control (1/1). Counts are separate
lanes; the six current path/process checks are a subset of the publisher suite.

`verification.json` retains normalized commands, canonical source base revisions,
executed assembly hashes, selected package families, intermediate failure
dispositions and exact runtime cleanup results. Per-test names/results are retained
in the three `*-executed-tests.json.gz` files. `source-inputs-sha256.json` identifies
the implementation files; review and publication revisions belong to the parent
workflow. Public receipts omit raw process output, credentials and local prefixes.
Private full logs remain under `TestResults`. A final formatting-only CRLF
normalization is recorded separately from the executed binary identities.

Publisher evaluation admits default and custom assembly names using TargetPath
and AssemblyName together, with bounded diagnostics and owned process cleanup.
Malformed, missing, mismatched and symlinked paths refuse. The standalone catalog
fallback is exact 5.1.0; caller/catalog authority remains intact and explicit 5.0.0
continues to refuse the catalog/host/selector gates. The runtime profile selects
published SDK 5.1.0, Tenants 8.0.0, Memories 4.0.0, FrontComposer 6.0.0 and McpCli
3.0.0. That package run qualifies the catalog topology; it does not qualify the
subsequent Memories source authority protocol or a deployment.

Memories source uses the configured service as the trusted issuer. A nonreused
operator-reserved generation creates namespaced identities. Atomic admission or
candidate retention binds minimal tenant/unit/generation authority to the original
producer and writer-exclusion custody reference before content writes. HTTP,
directory, annotation and EventStore-adapter paths share this boundary. Directory
issuance refuses an absent authority store before a payload write. Source and
workflow deletion cannot free an issued identity, and omitting configuration cannot
borrow another source's receipt. Ordinary unconfigured ingestion and tracked
lost-ack recovery remain usable without granting new completeness authority.

The historical controls use real authenticated Dapr workflows: source-A history
is purged before identity reuse; source B is independently reconciled; and an older
producer completes between the actual absent lookup and ordinary or annotation
admission. Cleanup refuses missing unit authority and complete historical chunk/NL
hashes survive byte-for-byte, along with foreign survivors, across connection
recreation. Legacy discovery retains obligations without modifying historical
artifact hashes. Historical cleanup requires the exact-unit inventory/evidence
seam, separately backed by configured writer exclusion; source coverage and old
bare provenance markers supply no unit authority.

Only the disposable fixtures attest their known synthetic units and excluded
writers. A real operator-controlled cutover, independently complete historical
inventories, matching Memories package publication/adoption and deployment remain
open external gates. The owner procedure documents writer exclusion, restart,
restore/new-generation behavior and rollback. No real deployment, production
mutation or production attestation was performed. Independent review, commitlint,
owning pushes and parent adoption follow in the parent workflow.

The successful runtime left only stale CLR FIFOs and diagnostic sockets. Exact
owned process identities were absent before removal: 24 FIFOs and 12 sockets were
removed, zero live owned processes remained and the custody directory was removed.
The earlier path-blocked runtime's 18 stale FIFOs were likewise removed under
exact ownership checks. Build output lives under run-with-temp and has been removed.

The focused legacy-activity qualification correction passes both description and
embedding variants (2/2, no skips). The old arrangement exposed the replacement
worker while the victim still had retained activity history; asynchronous
redelivery reached its unconditional dispatch traps before the victim history
was purged. The final assertion observed that earlier dispatch. The corrected
cutover keeps the original worker connected while production deletion fences,
terminates and physically purges the suspended victim, before replacement-worker
exposure. A separate live unit retains raw-input refusal and the successful modern
retry, then has both histories and content physically erased. Late completion,
restart, zero post-upgrade dispatch and survivor assertions remain. No production
guard changed. `erasure-boundary-verification.json` records the old ordering,
corrected boundary, exact focused selector and private log paths; its per-test
receipt is compressed separately. Final parent verification and publication are recorded in the final receipts above.

Final runtime cleanup verified all named PIDs absent, then removed 14 stale CLR
FIFOs and seven diagnostic sockets; custody and disposable build workspaces are
removed. Production generation/exclusion, real historical inventory, matching
Memories package adoption and deployment remain external gates. Publication
metadata is recorded separately in `publication.json`.
