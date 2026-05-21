# Story 6.1 - Activate Per-Party Personal Data Encryption

## Status

Done - 2026-05-21T19:25:00Z

## Scope

Validated and pinned the existing per-party personal data encryption behavior:

- Security tests now explicitly cover derived party display names (`PartyDisplayNameDerived`) so display and sort names are encrypted as personal data.
- Redaction tests now pin the missing-key fallback contract: protected payloads return the controlled `json-redacted` privacy status, encrypted leaves are replaced with `null`, and no crypto metadata or partial personal data is exposed.
- Existing encryption coverage already validates person fields, natural-person organization classification, non-natural organization pass-through, contact channels, identifiers, snapshots, key-version rehydration, and encryption-disabled pass-through.
- DI-wired integration coverage verifies encrypted-at-rest payloads, publish-time decryption, channel encryption, circuit breaker behavior after key deletion, mixed plaintext/encrypted replay compatibility, and disabled-crypto pass-through.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 131/131 tests.

```powershell
dotnet test tests\Hexalith.Parties.IntegrationTests\Hexalith.Parties.IntegrationTests.csproj --filter "FullyQualifiedName~EncryptionPipelineIntegrationTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 6/6 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
