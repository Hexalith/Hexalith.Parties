# Test Automation Summary

Story: 2.2 - Parties list with search, filters, and paging (FR-Admin-1)

## Generated Tests

### API Tests
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - adds a Test-environment-only AdminPortal API fixture with captured list/search request summaries for browser-level assertions.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - covers debounced display-name search, combined type/active filters, server-side criteria capture, paging preservation, stale/degraded last-known rows, recoverable empty state, keyboard row activation, and type-ahead search focus.
- [x] `tests/e2e/playwright.config.ts` - enables the deterministic AdminPortal E2E fixture for the Playwright-hosted `Test` environment.

## Coverage

- API request semantics covered: search query, page, page size, type filter, active filter.
- UI workflows covered: 5/5 targeted FR-Admin-1 browser workflows.
- Critical error/degraded cases covered: stale/degraded search preserving last-known rows; empty search with clear-filters recovery.
- Authorization safety covered indirectly: the fixture requires an opt-in cookie, so existing unauthenticated Admin-route E2E tests remain anonymous.

## Validation

- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium --list` - 5 tests discovered.
- [x] `bash scripts/check-no-warning-override.sh`
- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` - 258 tests passed.
- [ ] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium` - not runnable in this sandbox because Kestrel socket binding fails with `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated if applicable: yes, via fixture request-capture assertions for server-side criteria.
- E2E tests generated if UI exists: yes.
- Tests use standard test framework APIs: yes, Playwright `test`, `expect`, semantic locators, and `expect.poll`.
- Happy path covered: yes.
- Critical error cases covered: yes, degraded/stale and empty-state recovery.
- Tests use proper locators: yes, labels, roles, and visible text; one scoped CSS locator targets the existing keyboard-focus grid wrapper.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets before each test and the spec is serial because it shares an in-memory fixture.
- Summary created: yes.

## Next Steps

- Run the focused Playwright spec in an environment with NuGet audit access and local Kestrel socket binding.
