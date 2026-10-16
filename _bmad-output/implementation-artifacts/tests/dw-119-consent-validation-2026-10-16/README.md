# DW-119 consent validation evidence

The implementation rejects undefined lawful bases and revocation reasons longer than
256 characters. Null, empty, and short whitespace reasons remain valid. Diagnostics
are fixed; processor tests prove rejection precedes protected-state rehydration.
The reviewed change remains **in review** because the repository requires all existing
and new tests to pass before closure. No source-selection approval or release
qualification is asserted by this evidence.

## Commands and results

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Validation.RecordConsentValidatorTests -class Hexalith.Parties.Tests.Validation.RevokeConsentValidatorTests -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll
```

| Check | Result |
| --- | --- |
| Debug source-reference build | Pass, 0 warnings, 0 errors |
| Final focused classes | 86 passed, 0 failed |
| Final full project | 978 total, 925 passed, 53 failed |
| Original-validator comparison before two review cases | 976 total, 914 passed, 62 failed |

The original-validator comparison retained the new regression tests, replaced only
the two production validators with their contents at the recorded pre-work revision,
then used the identical build and full-project commands. Edited validators were
restored in a finally block. The original validators failed the same 53 existing
cases plus all nine new negative consent cases. The final run has no additional
failing cases. Two passing whitespace/padding regression cases were added during
review, explaining its final total of 978 rather than 976.

[validation.json](validation.json) records the exact parent revision, commands,
summary counts, and complete compared failing-case names. The initial focused run
was red (9 failures out of 84); all final focused tests pass.

## Baseline blockers

- Fifty-two `EventStoreGatewayRoutingTests` cases fail during fixture setup: the
  current EventStore source registers `FoldedSnapshotQualificationHostedService`,
  absent from the fixture's explicit hosted-service exclusion snapshot. Review the
  service's dependencies before deliberately reconciling that snapshot.
- `PlatformApiPrerequisitesTests.FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph`
  fails because its constants and current-source receipts still describe the
  submodule revisions preceding the earlier sync commit. Refresh current observations
  with exact evidence, retaining dated historical receipts and separate owner gates.

The baseline failures were not hidden or weakened. The two consent validators,
their tests, and the processor tests are the only C# files changed in DW-119.

## Runtime baseline

The explicitly targeted Parties AppHost started and its `parties` resource reported
healthy. Gateway, admin, Tenants, and UI projects were finished when described;
therefore this is not a passing full-topology qualification. The Parties AppHost
was stopped before compilation. No unrelated AppHost was stopped.
