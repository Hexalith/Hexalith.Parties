# Test Automation Summary

Story: 2.3 - Party detail (FR-Admin-2)

## Generated Tests

### API Tests
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - extends the Test-environment-only AdminPortal fixture with captured detail requests so browser tests can assert clicked and direct-route `GetPartyAsync(id)` behavior.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - covers desktop row-to-detail, direct detail route, detail badge/freshness/Edit/GDPR affordances, phone full-screen detail, row-focus restore on close, 320px overflow checks, zoom-equivalent narrow layout, and direct-route phone focus fallback.

### Component Regression Tests
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - covers detail freshness semantics, state badge semantics, partial projection honesty, routine refresh focus neutrality, Back/Edit affordances, active-row non-color cue, GDPR detail composition, privacy failures, and authorization/no-data-call guards.

## Coverage

- API request semantics covered: list/search from Story 2.2 plus Story 2.3 detail request IDs for row click and direct route.
- UI workflows covered: desktop detail open, direct route detail open, phone sheet/full-screen detail, close/back focus restore, direct-route no-origin focus fallback, 320px viewport, and zoom-equivalent narrow emulation.
- Critical error/degraded cases covered: stale/degraded detail freshness, partial display-name-only detail, erased/gone/malformed/forbidden privacy paths, and unauthenticated Admin route no browser-visible data requests.
- Accessibility assertions covered: semantic role/label locators, visible text state badges, polite freshness status, detail heading focus, row focus restore, search fallback focus, and no hardcoded waits.

## Validation

- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - 123 tests passed.
- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium --list` - 9 tests discovered.
- [ ] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` - blocked in this sandbox because Kestrel cannot bind a local socket: `System.Net.Sockets.SocketException (13): Permission denied`.
- [x] `bash scripts/check-no-warning-override.sh`

## Checklist Result

- API tests generated if applicable: yes.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, xUnit/bUnit and Playwright `test`/`expect`.
- Happy path covered: yes.
- Critical error cases covered: yes, through component tests and authorization E2E.
- Proper locators: yes, roles, labels, visible text, and scoped CSS only for component-owned state hooks.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets before each Playwright test; the spec remains serial because it shares an in-memory fixture.
- Summary created: yes.

## Next Steps

- Run the focused Playwright spec in an environment that permits local Kestrel socket binding.
