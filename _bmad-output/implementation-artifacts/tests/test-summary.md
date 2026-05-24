# Test Automation Summary

## Story

- Story 8.6: Enforce Picker Privacy and Integration Boundary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerApiClientTests.cs` - Added fail-closed coverage for blank host tokens on search and selected-party lookup, verifying no typed-client call is made.

### E2E Tests

- [x] `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` - Added bUnit workflow coverage proving hostile localized labels render through text/attribute paths without creating executable DOM elements.

### Static Guardrail Tests

- [x] `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs` - Added source guardrails for logging, telemetry, route mutation, URL/file, and download side-effect surfaces that could leak party, search, or auth data.

## Coverage

- API boundary: 2/2 picker query paths covered (`SearchPartiesAsync`, `GetPartyAsync`) through `PartyPickerApiClient`/`IPartiesQueryClient`; blank and missing host auth fail closed without backend calls.
- UI privacy rendering: Story 8.6 rendered-content criteria covered by bUnit tests for party labels, localized labels, status text, degraded reasons, search results, selected display, and backend failure text.
- Callback/event payloads: DOM callback DTO and JavaScript dispatcher guardrails cover the approved `partyId`, `partyType`, `status` payload only.
- Integration boundary: static guardrails cover retired REST/admin endpoints, DAPR actors, projection actors, local search services, server/projection references, package references, storage/cookies, token parsing/fingerprinting, logging, telemetry, route, URL, and file surfaces.
- Checklist result: all applicable QA automate checklist items pass for Story 8.6.

## Validation

- [x] `dotnet test tests\Hexalith.Parties.Picker.Tests\Hexalith.Parties.Picker.Tests.csproj --configuration Release` - Passed 161/161.
