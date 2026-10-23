# Five owner configuration and recovery items

Candidate source verification is complete. The parent workflow owns independent
review, canonical owner commits, parent gitlink adoption, clean-checkout fitness
reruns and authorized publication. Final commit identities must be recorded before
claiming that publication is complete.

| Proof | Result |
| --- | --- |
| EventStore Client full Debug suite | 2,102 total; zero failures; two existing environment skips |
| EventStore Admin.UI full Debug suite | 1,121/1,121 |
| EventStore Sample full Debug suite | 194/194 |
| EventStore query-contract compatibility | 115/115 |
| Final batch/transaction/expiry lane after staging cleanup removal | 249/249 |
| Memories registered algorithm/options/token controls | 7/7, including RSA/ECDSA acceptance, HS256 rejection, defaults, invalid lists and reload |
| Parties mapped Debug focused suite, including registered health endpoints | 990/990 |
| Real Dapr/Redis inventory ACL health | Pass, zero skips; denied and repaired in both indexing modes |
| Dual-session actual UI/gateway/ACL/survivor runtime | 1/1, zero skips, 196.862 seconds |
| Retained Release/package Integration build | Pass, zero warnings/errors |

`validation.json` records the exact boundaries, baseline selections, commands,
assembly/log digests and separate failures. Source Debug checks use the current
pinned-tool-generated mapping. The launcher itself fails the untouched Platform
model gate; mapped Integration remains blocked by FrontComposer `MSB4044`.
No source selector or build gate was changed. The initial cold runtime attempt
failed a child Release copy/build and sidecar readiness before UI assertions; the
separately recorded retry passed after serial host builds.

The full Memories Server suite has two unrelated deployment assertions tied to
an absent forbidden nested Builds checkout and the old 3.126.0 asset selection.
The initial full Parties suite has two fixed health-fixture failures and two
fitness checks requiring clean committed owner checkouts. The corrected focused
health lane passes; the parent must rerun the full clean graph after commits.

`inventory-runtime.json` records authenticated non-default Redis-user proof,
prefix repair, the actual C# SDK/health result, sentinel absence and owned cleanup.
The remote search/cleanup responses in that control are deterministic; it does
not qualify live Memories search or existing-workflow authorization.

Package publication, Dapr sidecar secret emission, authoritative remote inventory,
legacy normal-batch compaction and migration remain independent open gates.
The retained EventStore 3.126.1 graph stays buildable through a narrow query
vocabulary bridge; it is not reported as published semantic-alias adoption.

To reproduce the inventory control after the mapped Debug service test build:

```bash
python3 inventory-runtime.py --workspace "$QUALIFIED_WORKSPACE" \
  --output-directory "$PRIVATE_RUNTIME_RECEIPTS" --daprd "$DAPR_RUNTIME_PATH"
```

The retained runner exposes workspace/output/runtime paths as arguments; its
owned backend, ACL-grant, C# invocation, absence and cleanup logic matches the
executed control. Do not publish its private runtime configuration or raw logs.

The broad Contracts runner was cancelled after about ten minutes in existing
package/provenance fixtures and exited 58. Its cancellation summary reported
2,404 tests, zero failures and two package-inventory skips; cancellation is not
a completed full-suite pass. Its focused compatibility fallback passed 115/115.
The runner and observed child processes exited. All three topology roots, all
recorded owned containers and all processes matching those session identities
were confirmed absent. The final Client rebuild and 249-case focused lane pass
after removing duplicate completed-staging cleanup; transaction retries still
require exact operation-key proof.

The five ledger dispositions remain open for the parent to finalize after review
and canonical adoption. In particular, the retained 3.126.1 default package does
not contain the batch recovery source fix: its affected closure remains open
pending matching publication and package adoption.
