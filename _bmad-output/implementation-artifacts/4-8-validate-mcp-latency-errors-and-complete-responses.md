# Story 4.8 - Validate MCP Latency, Errors, and Complete Responses

## Status

Done - 2026-05-21T17:10:00Z

## Scope

Added MCP readiness validation across latency, error, retry, and complete-response behavior:

- Healthy in-process calls across all five tools complete under the historical MVP 1 second MCP budget.
- `create_party`, `update_party`, and `delete_party` success paths are covered with complete gateway payload responses.
- `get_party` and `find_parties` success paths are covered, including projection freshness metadata on detail responses.
- Timeout, downstream HTTP failure, cancellation, validation, gateway rejection, missing context, and not-found/cross-tenant errors map to stable structured results.
- Error mappings are checked for safe wording that omits personal data, raw payloads, secrets, and infrastructure details.
- Already-inactive `delete_party` retries remain stable and side-effect free.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolDispatchTests|FullyQualifiedName~PartiesMcpToolContractTests|FullyQualifiedName~PartiesMcpProjectFitnessTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 52/52 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
