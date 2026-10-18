# DW-63 / DW-169 verification

This packet records the bounded Memories mapping cleanup repair. `validation.json`
contains exact commands, dependency identities, source hashes, test summaries,
review disposition, rollback evidence and the expected unsafe-delete mutation
failure. `commitlint.txt` preserves successful validation of the exact commit
message using the installed repository CLI. Raw local build/test outputs remain
in the task validation directory; they are summarized here to avoid recording
local machine paths or runtime credentials.

Domain cleanup subtracts exact cleaned entries from fresh state and CAS-saves the
result, including `[]`. Keeping an empty ledger at its existing hashed key avoids
Redis ETag reuse after physical deletion. Reserved one-use health probes retain
physical key reclamation. The serialized entry schema and domain key are unchanged.

The deterministic Redis-style state fake serializes inventories by actual key,
resets versions on physical deletion/recreation, and permits absent-key creation.
Regressions verify final persisted inventory under append, replacement, legacy
case adoption, overlapping real cleanups/indexing, partial HTTP failure, late
state-read failure, retry exhaustion, missing ETags and caller cancellation.

Independent review found eight issues; all were patched and verified in a
follow-up review, with no new deferred work. The rollback exercise restored the
baseline C# source graph, built and ran its full service assembly, then restored
the implementation. The mutation exercise reinstated unsafe domain empty-key
deletion and correctly failed the overlapping cleanup/indexing regression.

The Aspire baseline was started and inspected, but did not establish a healthy
Parties runtime before shutdown. The pending wait lost its JSON-RPC connection
when the AppHost was stopped. No full-topology or terminal physical-erasure
qualification is claimed. The AppHost was confirmed stopped afterward.

Rollout must drain older Parties cleanup writers first. Do not expire or delete
domain mapping ledger keys while mapping operations can be in flight. Terminal
Memories workflow reconciliation, identical-identity recreation fencing, durable
cancellation recovery and the DW-81 persistence migration remain separate work.

Redis behavior was verified against the primary
[Dapr components-contrib 1.18.0 Redis source](https://raw.githubusercontent.com/dapr/components-contrib/v1.18.0/state/redis/redis.go).
