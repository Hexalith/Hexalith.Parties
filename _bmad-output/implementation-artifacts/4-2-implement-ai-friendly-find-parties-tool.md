# Story 4.2: Implement AI-Friendly Find Parties Tool

## Status

Done.

## Summary

Completed the `find_parties` MCP contract for MVP agent use:

- Keeps display-name search as the only active search mode.
- Uses list mode when search text is omitted.
- Preserves bounded page defaults and type/active filters.
- Adds created-date and modified-date list filters.
- Rejects malformed date filters with stable validation errors that do not echo raw input.
- Documents that email, identifier, semantic, graph, and temporal search are not evaluated in MVP.

## Files Changed

- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs`
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolContractTests.cs`
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolDispatchTests.cs`

## Validation

- Passed: `dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolContractTests|FullyQualifiedName~PartiesMcpToolDispatchTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` (23/23)

## Notes

The warning override remains scoped to the current EventStore submodule warning state; no domain or client behavior was changed outside the MCP adapter.
