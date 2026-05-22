# Story 4.6 - Implement Delete Party as Soft Deactivation Tool

## Status

Done - 2026-05-21T16:50:00Z

## Scope

Implemented MCP `delete_party` soft-deactivation semantics:

- Active parties dispatch to `DeactivatePartyWithResultAsync`.
- Already inactive parties return an idempotent success without dispatching another command.
- Delete responses now explicitly identify `operation = soft-deactivation`.
- Delete responses now explicitly report `gdprErasurePerformed = false`.
- Gateway payloads are returned under `partyDetail` with the soft-deactivation metadata.
- Payload-free accepted deactivation responses return bounded confirmation with requested inactive state.
- Missing/malformed IDs and missing context fail closed before query or command dispatch.
- Not-found/cross-tenant gateway responses map to sanitized structured MCP errors.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolDispatchTests|FullyQualifiedName~PartiesMcpToolContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 42/42 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
