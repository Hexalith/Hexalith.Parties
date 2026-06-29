# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Parties.Tests/Projections/ProjectionPlatformAdapterTests.cs` - Covers EventStore-authoritative rebuild checkpoint save/read/delete behavior, terminal rows, tenant-wide resume selection, and fail-loud completion policy.
- [x] `tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildAndHealthHardeningTests.cs` - Covers production read freshness mapper usage for detail and index projection actors.

### E2E Tests
- [x] No E2E spec retained for Story 7.5. The generated static source-text spec was removed during review because behavioral xUnit coverage carries the migration proof.

## Coverage
- Story 7.5 acceptance criteria: 7/7 covered by focused C# projection, gateway, freshness, erasure, and topology tests.
- API gateway evidence: command and query gateway routes covered through existing focused gateway and integration test inventory.
- UI freshness behavior: dot-plus-word freshness indicator and polite live-region contract covered through existing UI/E2E tests; Story 7.5 did not change UI rendering.

## Validation
- [x] `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.ProjectionPlatformAdapterTests`
- [x] `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.ProjectionRebuildAndHealthHardeningTests`
- [x] `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.ProjectionRebuildServiceTests`
- [x] `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll`
- [x] `dotnet tests/Hexalith.Parties.Projections.Tests/bin/Release/net10.0/Hexalith.Parties.Projections.Tests.dll`
- [x] `git diff --check`
- [x] `bash scripts/check-no-warning-override.sh`
- [ ] `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false` remains blocked by unrelated submodule drift in EventStore/Tenants/PolymorphicSerializations.

## Next Steps
- Keep the existing C# focused projection and gateway lanes as the authoritative behavioral proof for checkpoint/rebuild semantics.
