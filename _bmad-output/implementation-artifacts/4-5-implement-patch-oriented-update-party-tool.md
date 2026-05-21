# Story 4.5 - Implement Patch-Oriented Update Party Tool

## Status

Done - 2026-05-21T16:40:00Z

## Scope

Implemented MCP `update_party` hardening for patch-oriented composite updates:

- Partial person and organization detail patches merge against current party details so omitted fields remain unchanged.
- Contact and identifier add/update/remove operations remain explicit `UpdatePartyComposite` sub-operations.
- VAT/identifier aliases normalize to a single identifier add sub-operation.
- Invalid composite update input is rejected even when an `active` lifecycle patch is also supplied, preventing silent partial lifecycle changes.
- Oversized update payloads are rejected with a structured MCP validation error before any read or command dispatch.
- Gateway validation rejection maps to a safe MCP failure with correlation ID and no partial-success payload.
- Successful command payloads return the complete updated `PartyDetail`.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolDispatchTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 32/32 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
