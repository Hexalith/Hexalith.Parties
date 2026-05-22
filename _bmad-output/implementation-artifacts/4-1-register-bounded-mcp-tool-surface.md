# Story 4.1: Register Bounded MCP Tool Surface

## Status

Done.

## Summary

Tightened the Parties MCP host to expose exactly the canonical MVP party tools:

- `find_parties`
- `get_party`
- `create_party`
- `update_party`
- `delete_party`

Removed the previously registered temporal compatibility tool `get_party_name_at` from the MCP surface so internal/future capabilities are not advertised to agents. The MCP host remains a separate thin consumer host and continues to dispatch through injected Parties command/query clients.

## Files Changed

- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpToolNames.cs`
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs`
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolContractTests.cs`
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolDispatchTests.cs`

## Validation

- Passed: `dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolContractTests|FullyQualifiedName~PartiesMcpToolDispatchTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` (21/21)

## Notes

The warning override was kept consistent with Story 3.10 validation because the current EventStore submodule revision still has unrelated nullable/analyzer warnings when built under default warnings-as-errors settings.
