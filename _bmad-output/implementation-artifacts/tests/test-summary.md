# Test Automation Summary

Story: 2.1 - Embed the Admin area behind the Admin policy

## Generated Tests

### API Tests
- [x] Not applicable: story 2.1 adds host/RCL route discovery and authorization wiring only; no public API endpoints were added.

### Unit / Component Tests
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs` - covers lazy AdminPortal backend configuration so degraded/test host startup does not fail when no typed Parties client or FrontComposer `IQueryService` is registered.
- [x] `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs` - covers route-aware forbidden copy so `/admin*` denials explain the Admin role requirement without mislabeling other denied areas.

### E2E Tests
- [x] `tests/e2e/specs/admin-area-authorization.spec.ts` - covers unauthenticated access to `/admin`, `/admin/parties`, `/admin/parties/party-123`, and `/admin/parties/party-123/gdpr`.
- [x] `tests/e2e/playwright.config.ts` - supplies synthetic `Parties__BaseUrl` and `Parties__Tenant` values for browser-test host typed-client composition without requiring a live backend.

## Coverage

- Admin routes challenged unauthenticated: 4/4.
- Admin route return URLs preserved: 4/4.
- UI features covered: Admin landing compatibility route, AdminPortal list route, AdminPortal detail route, AdminPortal GDPR route.
- Critical error cases covered: unauthenticated users are sent to the sign-in challenge; old `/admin` placeholder text and representative party data are not rendered before authorization.
- Review regression covered: AdminPortal DI/options validation remains lazy when no data backend is configured.
- API endpoints: not applicable for this story.

## Validation

- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`
- [x] `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1 -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`
- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`
- [x] `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` - 256 total, 0 failed.
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - 112 total, 0 failed.
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-area-authorization.spec.ts --project=chromium --list` - 4 tests discovered.
- [x] `bash scripts/check-no-warning-override.sh`
- [ ] `cd tests/e2e && npm run test -- specs/admin-area-authorization.spec.ts --project=chromium` - attempted, but blocked by sandbox socket permission when Kestrel binds: `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated if applicable: yes, not applicable.
- E2E tests generated if UI exists: yes.
- Tests use standard test framework APIs: yes, Playwright `test`, semantic locators, and URL assertions.
- Happy path covered: yes for the protected unauthenticated browser flow into the sign-in challenge.
- Critical error cases covered: yes, unauthenticated access and no protected data/placeholder rendering before authorization.
- Tests use proper locators: yes, `getByRole` and `getByText`.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes.
- Summary created: yes.

## Next Steps

- Run the focused Playwright command in an environment that permits local Kestrel socket binding.
