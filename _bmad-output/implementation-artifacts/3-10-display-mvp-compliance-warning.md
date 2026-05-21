# Story 3.10: Display MVP Compliance Warning

## Status

Done.

## Summary

Added a persistent MVP compliance warning while GDPR features are inactive:

- Startup logs now use the shared `MvpComplianceWarning.Message`.
- Responses now include `X-Hexalith-Parties-Mvp-Compliance-Warning` until `Parties:Compliance:GdprFeaturesActive=true`.
- The old retired `X-GDPR-Warning` surface remains forbidden by guardrails.
- README and getting-started docs describe the warning, the header, and the v1.1 activation switch.

## Files Changed

- `README.md`
- `docs/getting-started.md`
- `src/Hexalith.Parties/Compliance/MvpComplianceWarning.cs`
- `src/Hexalith.Parties/Middleware/MvpComplianceWarningMiddleware.cs`
- `src/Hexalith.Parties/Program.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/PartiesProcessEndpointTests.cs`

## Validation

- Passed: `dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartiesProcessEndpointTests|FullyQualifiedName~ArchitecturalFitnessTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` (23/23)
- Passed: `dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --filter "FullyQualifiedName~SampleOnboardingGuardrailTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` (7/7)

## Notes

The warning override was required because the current `Hexalith.EventStore` submodule revision emits unrelated nullable/analyzer warnings that are promoted to errors by the default build settings.
