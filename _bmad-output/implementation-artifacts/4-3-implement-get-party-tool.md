# Story 4.3: Implement Get Party Tool

## Status

Done.

## Summary

Tightened `get_party` MCP behavior for tenant-safe agent retrieval:

- Validates party ids before calling the gateway.
- Returns complete `PartyDetail` data from the typed query client, including inactive status and freshness metadata.
- Maps erased party details to bounded not-found responses without exposing erasure details.
- Preserves existing safe client error mapping for unauthorized, forbidden, not-found, degraded, and downstream failures.

## Files Changed

- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs`
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolDispatchTests.cs`

## Validation

- Passed: `dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolDispatchTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` (20/20)

## Notes

No client or projection changes were needed; `get_party` continues to route through the tenant-safe query client boundary.
