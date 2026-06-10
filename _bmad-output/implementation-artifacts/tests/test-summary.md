# Test Automation Summary

Story: 2.4 - Create and edit a party with validation (FR-Admin-3)

## Generated Tests

### API / Adapter Tests
- [x] Existing `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` coverage remains the command adapter/API-boundary test surface for create/update command mapping and safe validation outcomes.
- [x] Existing `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` fixture coverage captures create/update browser requests without adding browser-callable production endpoints.

### Component Tests
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/CreateEditPartyPageTests.cs` - covers protected create/edit route guards, create command construction, route-authoritative edit command construction, client validation, gateway validation safe copy, radio type switching with preserved hidden input, erased-detail privacy blocking, and optimistic accepted-without-payload navigation/status.
- [x] Existing AdminPortal component tests continue to cover browse/detail/GDPR regressions from Stories 2.1-2.3.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - covers create happy path, edit from the detail Edit action, client validation alert/preserved input, gateway validation rejection alert/preserved input/no raw backend identifiers, phone/zoom form layout, and preserved list/detail regressions.

## Coverage

- API endpoints: no browser-callable command/query endpoints added; command traffic remains covered through the AdminPortal API adapter and Test-environment-only fixture.
- UI workflows: create, edit, client validation, gateway validation rejection, optimistic accepted status, detail navigation, phone layout, and 200% zoom-equivalent overflow checks.
- Critical error/privacy cases: unauthenticated, missing tenant, non-admin, erased edit detail, client-side validation, and gateway validation rejection.
- Accessibility assertions: role/label Playwright locators, Fluent radio component contract in bUnit, assertive `role="alert"`, polite `role="status"`, and no hardcoded waits.

## Validation

- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - 138 tests passed.
- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `cd tests/e2e && npm run typecheck`
- [ ] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` - blocked in this sandbox before test execution because Kestrel cannot bind a local socket: `System.Net.Sockets.SocketException (13): Permission denied`.
- [x] `bash scripts/check-no-warning-override.sh`

## Checklist Result

- API tests generated if applicable: yes, via existing adapter/fixture coverage; no public command endpoints were added.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, xUnit v3/bUnit and Playwright `test`/`expect`.
- Happy path covered: yes, create and edit.
- Critical error cases covered: yes, client validation, gateway validation, authorization guards, and erased edit privacy.
- Proper locators: yes, semantic roles/labels in Playwright and component APIs in bUnit where static markup does not expose browser roles.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes; the Playwright fixture resets before each test and remains serial for shared fixture state.
- Summary created: yes.

## Next Steps

- Run the focused Playwright spec in an environment that permits local Kestrel socket binding.
