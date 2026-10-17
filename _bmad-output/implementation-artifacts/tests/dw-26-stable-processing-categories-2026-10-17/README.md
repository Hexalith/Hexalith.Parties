# DW-26 validation — 2026-10-17

The baseline with the regression assertions fails six of ten focused tests because the category contains an event name instead of `Other`. After the mapper change, the focus passes 10/10 and the full projection assembly initially passes 256/256, with no failures, errors, skips, or unrun tests. Both Debug source-reference builds pass with zero warnings/errors.

Commands run from the Parties root:

```bash
dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -method '*ProcessingActivityFold_*' -xml /tmp/dw-26-validation/focus-baseline.xml
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -method '*ProcessingActivityFold_*' -xml /tmp/dw-26-validation/focus-final.xml
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -result-xml /tmp/dw-26-validation/full-final.xml
```

The first focused command returns 1 before the production fix; the later focused and full commands return 0. The runner reports `-xml` as a deprecated alias for `-result-xml`; all requested tests execute. Machine-readable counts are in `validation.json`. Representative build logs, failing baseline XML, final focused/full XML and full test output are retained here alongside topology describe/stop output. Repository prefixes and machine/user attributes are removed from retained runner output, and trailing whitespace is trimmed; test results are otherwise unchanged.

The exact Parties AppHost was started, inspected and stopped with Aspire. Inspection reported Keycloak (`security`) Unhealthy and the application resources Waiting. No healthy topology acceptance or package-mode/Release qualification is claimed.

The production change only replaces the uncategorized fold fallback with `Other`. The existing successful categories, event-type diagnostic field, outcomes, checkpoint handling and redelivery behavior are preserved. Existing persisted category strings are retained until normal replay/rebuild or successful replacement; no data migration was performed.

After independent review, three additional tests verify decodable redacted category mapping, legacy category retention during append, and serialized `PrepareRebuildAsync` processing-slot output. Final review build passes with zero warnings/errors; final focus passes 13/13 and the full assembly passes 259/259 without failures/errors/skips/unrun tests:

```bash
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -method '*ProcessingActivityFold_*' -method '*DetailRebuildPlan_NormalizesLegacyProcessingCategoryAsync' -result-xml /tmp/dw-26-validation/focus-review.xml
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -result-xml /tmp/dw-26-validation/full-review.xml
```
