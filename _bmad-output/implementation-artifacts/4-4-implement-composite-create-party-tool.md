# Story 4.4 - Implement Composite Create Party Tool

## Status

Done - 2026-05-21T16:30:00Z

## Scope

Implemented MCP `create_party` hardening for AI-friendly composite creation:

- Person creation normalizes canonical and pre-pivot alias fields into `CreatePartyComposite`.
- Organization creation supports optional email, phone, and VAT/identifier input.
- Caller-supplied party IDs are validated before the EventStore gateway is called.
- Oversized create payloads are rejected with a structured MCP validation error before any command is dispatched.
- Missing required party details return structured validation errors and no partial command.
- Gateway command rejection maps to a safe MCP failure with correlation ID and no partial success payload.
- Successful command payloads continue returning the complete created `PartyDetail`, including detail, contact channel, identifier, and active/name fields supplied by the gateway.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --filter "FullyQualifiedName~PartiesMcpToolDispatchTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 26/26 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
