# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Parties.Tests/Projections/ProjectionPlatformAdapterTests.cs` - DI rollback switch coverage for `Parties:Projections:PlatformAdapterMode`, default EventStore adapter resolution, and local rollback adapter resolution.
- [x] `tests/Hexalith.Parties.Tests/Projections/ProjectionPlatformAdapterTests.cs` - EventStore rebuild checkpoint completion failure coverage proving local checkpoint cleanup is attempted and the EventStore completion failure is surfaced.

### E2E Tests

- [x] `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts` - Story 7.4 adapter-first evidence, rollback switch, blocked validation evidence, local compatibility boundary, EventStore-backed adapter, replay-from-zero delivery ordering, rebuild adapter routing, and focused parity test inventory.

## Coverage

- API endpoints: N/A for story 7.4; the Parties host still has no public API, and public traffic remains through the EventStore gateway.
- Projection adapter: default EventStore mode, local rollback mode, rebuild checkpoint save/delete scope mapping, EventStore completion failure surfacing, out-of-order delivery checkpoint order, and no checkpoint save after partial projection delivery.
- Rebuild compatibility: adapter-routed read/save/delete evidence, local replay mechanics preservation, fail-closed trusted party id enumeration, allowlisted event type resolution, state-store write failure, and processing-record no-PII coverage.
- UI behavior: no production UI workflow changed. Freshness UX stability remains covered by existing bUnit/contract tests and is pinned by story evidence; no new browser interaction was applicable.

## Validation

- [x] `git diff --check`.
- [x] `dotnet build src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`.
- [x] `tests/Hexalith.Parties.Projections.Tests/bin/Release/net10.0/Hexalith.Parties.Projections.Tests` passed 139/139.
- [x] `npm run typecheck` from `tests/e2e`.
- [x] `PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test -- specs/story-7-4-projection-platform-compatibility.spec.ts --project=chromium` passed 5/5.
- [ ] `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` remains blocked before test compilation by unrelated Tenants/package drift: `NU1102 Unable to find package Hexalith.Commons.UniqueIds with version (>= 3.19.0); nearest 2.18.0`.
- [ ] `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Release --no-build --no-restore -v:minimal` is blocked by sandbox IPC restrictions: `System.Net.Sockets.SocketException (13): Permission denied` while the .NET test CLI creates its named pipe. The compiled test executable was used instead.
- [ ] `npm run test -- specs/story-7-4-projection-platform-compatibility.spec.ts --project=chromium` without `PLAYWRIGHT_SKIP_WEBSERVER` is blocked by sandbox Kestrel socket binding restrictions. The file-only spec passes with the existing config escape hatch.

## Checklist Validation

- [x] API tests generated if applicable.
- [x] E2E tests generated if UI exists: story-evidence E2E generated; no UI workflow changed.
- [x] Tests use standard xUnit/Shouldly/NSubstitute and Playwright APIs.
- [x] Tests cover happy path: default EventStore mode, local rollback mode, adapter-first story evidence, and rebuild adapter routing.
- [x] Tests cover critical error cases: EventStore rebuild completion failure and partial projection delivery checkpoint suppression.
- [ ] All generated tests run successfully: blocked for the new xUnit tests by unrelated main test project dependency drift; Playwright story spec passes.
- [x] Tests use proper locators where applicable: the new Playwright test is filesystem/story evidence only and uses no DOM locators.
- [x] Tests have clear descriptions.
- [x] No hardcoded waits or sleeps.
- [x] Tests are independent.
- [x] Test summary created at the configured workflow output path.
- [x] Tests saved to appropriate directories.
- [x] Summary includes coverage metrics.

## Next Steps

- Re-run the main `Hexalith.Parties.Tests` build/test lane after the unrelated Tenants `Hexalith.Commons.UniqueIds >= 3.19.0` dependency drift is resolved.
