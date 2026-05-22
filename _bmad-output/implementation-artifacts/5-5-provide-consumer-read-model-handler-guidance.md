# Story 5.5 - Provide Consumer Read-Model Handler Guidance

## Status

Done - 2026-05-21T18:40:00Z

## Scope

Added consumer read-model guidance and validation:

- Event handler documentation now includes explicit read-model scope and privacy guidance.
- Guidance recommends stable `partyId`, last processed aggregate `sequenceNumber`, and minimal operational metadata as the baseline local record.
- Guidance warns consumers to copy display names, contact values, identifiers, natural-person flags, and other personal data only when their bounded context needs it.
- Existing sample replay tests cover party create, detail updates, contact and identifier changes, deactivation, reactivation, duplicate delivery, and out-of-order sequence handling.
- Documentation tests now pin the privacy/minimal-read-model guidance.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 56/56 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
