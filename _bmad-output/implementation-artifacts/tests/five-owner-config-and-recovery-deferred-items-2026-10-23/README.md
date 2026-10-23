# Five owner configuration and recovery items

EventStore is pinned to **3.127.0**. The five source changes are implemented and
reviewed. Configured reference UI routing and inventory health are qualified.
Semantic query aliases, superseding-state batch recovery and the Memories server
algorithm policy retain their matching package publication/adoption gates.
The actual published EventStore 3.127.0 probe still overwrites superseding state;
its four recovery tests explicitly skip in package mode and pass in source mode.

Reviewed source is published on owner main:

- EventStore: `596de60e140da779f170279a822ded010070d774`
- Memories: `4190ec6549dd83bcaaf268e7bb723dcff8f7286b`
- Parties source adoption: `79768d1f2b7071db6fa49bffbfe179a43a00baa7`

| Final proof | Result |
|---|---|
| Release solution build | Zero warnings/errors; final changed test projects rebuilt afterward |
| Parties service suite | 2,647 total; 2,639 passed; eight documented prerequisite/environment skips |
| EventStore Client suite | 2,116 total; zero failures; two existing environment skips |
| EventStore Admin.UI / Sample suites | 1,121/1,121 and 194/194 |
| Memories registered algorithm/token/policy controls | 8/8 |
| Original generated source mapping with final task patches | 999/999; zero skips; historical dependency graph only |
| Actual 3.127.0 SDK/Dapr/Redis ACL health | Pass; denied/repaired in both modes; no sentinels; owned cleanup verified |
| Actual two-session UI/gateway/ACL/survivor runtime | 1/1; zero skips; 157.014 seconds |
| Runtime runner failure/deadline controls | 7/7 |
| Push-range gitlink signoff / warning guard | Pass |

The UI runtime seeds and verifies persisted counts 2 and 5, then requires each
actual page to display its own count. A gateway 404 fallback cannot satisfy the
assertion. Sample receives a fixture-only signing contract, audience, independent
application-channel token and own-session gateway allow-list; authentication and
ACLs remain enforced. Both fixture roots, owned workers and containers are absent.
Probe writes require expiry/conditional-erasure capabilities before mutation and
use a one-minute SDK TTL; ordinary ingestion remains durable. Authentication
policy changes require server restart; configuration notifications are preserved.

`final-verification.json` records current commands, counts, canonical owner
identities, log hashes, initial failures, corrections, skips and remaining gates.
`commitlint.json` preserves exact message-validation evidence. The three review
layers' 16 findings and three parent verification corrections are triaged in the
spec. The separate, preexisting Memories MCP algorithm-binding defect is deferred.

`validation.json` remains historical original-candidate evidence, preserving its
3.126.1 graph and 990/249-case measurements. `eventstore-3.127.0-adoption.json` and
`matrix-audit.json` record later pre-review observations. None retrospectively
qualifies the externally advanced source graph. Original source-launcher and
mapped Integration blockers, two unrelated broad Memories assertions and the
cancelled broad Contracts run remain explicitly bounded in those receipts.

Reproduce inventory health after building the current Release service assembly:

```bash
python3 inventory-runtime.py --workspace "$QUALIFIED_WORKSPACE" \
  --output-directory "$PRIVATE_RUNTIME_RECEIPTS" --daprd "$DAPR_RUNTIME_PATH" \
  --configuration Release
```

Keep private configurations and raw runtime logs outside the repository. This
control qualifies SDK persistence capability, not live Memories search, remote
inventory completeness, existing-workflow authorization or physical erasure.
No release dispatch, deployment, historical parity approval or migration occurs.
