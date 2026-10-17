# DW-127 / DW-146 validation — 2026-10-17

The real identity query handler now round-trips Ineligible Unknown evidence through
SubmitQueryResponse JSON and HttpPartiesIdentityClient. Unknown never resolves;
incomplete, foreign or malformed evidence and every denial binding still fail closed.
Classification must be explicitly present; camel/Pascal names and numeric enums remain
compatible. Inactive, restricted, erasing, erased and unbound cases retain safe denials.

Final Debug/source results: Client **330/330**, Parties **1,027/1,027**, Contracts
**199/199**; **1,556 passed**, zero errors, failures, skips or not-run. Builds report
zero warnings/errors. The 16 producer/client cases and all 174 identity-client cases
pass. Before the repair, the client case failed 1/166 and the round trip failed all
five Unknown cases (11/16 passed). Review's missing-classification regression failed
1/5 before the required-property annotation.

The existing full-suite dependency/doc drift was reconciled against the committed
root graph and catalog, preserving dated history and owner approvals. Independent
review and follow-ups are complete; no new deferred work. Exact commands, results,
source hashes and supplemental temporary XML paths/digests are in validation.json.

Runtime baseline: security was Unhealthy and dependent resources waited; the exact
AppHost was stopped successfully before edits. No healthy full topology, installed
identity service or production custody qualification is claimed. No Git mutations,
dependency updates, deployment or production identity mutation.
