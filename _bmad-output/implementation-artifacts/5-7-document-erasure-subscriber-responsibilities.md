# Story 5.7 - Document Erasure Subscriber Responsibilities

## Status

Done - 2026-05-21T19:10:00Z

## Scope

Added and validated erasure subscriber responsibility guidance:

- Event handler docs now explicitly distinguish MVP soft deactivation from future v1.1 GDPR erasure.
- `PartyDeactivated` is documented as active/inactive business state, not a legal erasure or subscriber cleanup signal.
- `PartyErased` guidance now links to the MVP compliance boundary and manual deletion/environment rebuild path for accidental sensitive data.
- `PartyErased` payload guidance now states cleanup metadata only: party id, tenant id, and erasure timestamp, with no personal data.
- Dangling-reference guidance now references the EventStore `PartyDetail` query boundary instead of retired direct Parties routes.
- Documentation tests pin the soft-deactivation/erasure distinction, compliance warning link, cleanup guidance, and current EventStore boundary.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --filter "FullyQualifiedName~PartyErasedHandlerPatternTests|FullyQualifiedName~SampleOnboardingGuardrailTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 16/16 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
