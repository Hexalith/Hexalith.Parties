# Story 4.7 - Enforce MCP Boundary and Tool Safety

## Status

Done - 2026-05-21T17:00:00Z

## Scope

Added MCP boundary and safety guardrails:

- MCP project fitness now requires the exact accepted production references: client, contracts, service defaults, and ModelContextProtocol.
- MCP source fitness now rejects direct references to retired actor/server/projection infrastructure.
- MCP tool source fitness now rejects raw ProblemDetails, exception detail/message use, claim payload handling, and authorization/token wording in tool responses.
- Cross-tool dispatch coverage now proves `find_party`, `create_party`, `update_party`, and `delete_party` fail closed before any query or command client access when tenant/user context is missing.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolDispatchTests|FullyQualifiedName~PartiesMcpToolContractTests|FullyQualifiedName~PartiesMcpProjectFitnessTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 48/48 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
