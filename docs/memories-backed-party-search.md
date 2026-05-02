# Memories-Backed Party Search

Hexalith.Parties can route party discovery through Hexalith.Memories when `Parties:MemoriesSearch:Enabled` is `true`. Parties remains authoritative: Memories results are candidates, and Parties hydrates every returned party from its own projections before REST or MCP responses are produced.

## Configuration

```json
{
  "Parties": {
    "MemoriesSearch": {
      "Enabled": true,
      "Endpoint": "https://memories.example/",
      "RequireApiToken": true,
      "ApiToken": "<token>",
      "TenantId": "tenant-a",
      "CaseId": "parties",
      "EnabledAxes": [ "hybrid", "syntactic", "semantic", "graph" ]
    }
  }
}
```

When enabled, CommandApi registers `MemoriesClient` from `Hexalith.Memories.Client.Rest`. `Hexalith.Parties.Contracts` does not reference Memories packages.

## Modes

- `hybrid`: default rich search through Memories hybrid search.
- `lexical` or `syntactic`: single-axis syntactic search.
- `semantic`: single-axis semantic search.
- `graph`: traversal from a known Memories context.

REST keeps the existing paged result body and adds search metadata headers:

- `X-Parties-Search-Status`: `Rich`, `Degraded`, or `LocalOnly`.
- `X-Parties-Search-Degraded-Reason`: present when rich search is degraded.

MCP `find_parties` uses the same search service and preserves list behavior when `query` is empty.

## Hydration Rules

Parties omits Memories candidates that cannot hydrate to a readable Parties projection. This includes stale memory units, erased parties, wrong-tenant source URIs, unauthorized parties, wrong-case candidates, and duplicate hits for the same party.

## Erasure Cleanup

Party memory units use stable source URIs:

```text
urn:hexalith:parties:{tenantId}:party:{partyId}
```

Erasure workflows must delete or tombstone mapped Memories memory units. If Memories cleanup fails, Parties records blocked cleanup evidence and must not report erasure as complete until cleanup succeeds or the block is explicitly resolved.
