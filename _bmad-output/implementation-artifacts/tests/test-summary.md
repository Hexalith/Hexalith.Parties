# Test Automation Summary

Story: 3.1 - GDPR operations page

## Generated Tests

### API Tests
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - aligns the gated Test-environment AdminPortal fixture with `AdminPortalGdprCapability.ProvisionalBridge()`, accepted provisional commands, non-empty export payloads, and bounded processing-record responses for browser assertions.
- [x] No public API endpoints were added; browser-visible command/query calls remain forbidden by Playwright request capture.

### Component Tests
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - adds review regression coverage for direct GDPR partial-detail routes rendering bounded no-PII state with no mutation controls, empty initial GDPR operation live-region behavior, and stale correlation cleanup after assertive GDPR command failures.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - extends direct `/admin/parties/{id}/gdpr` coverage for primary focus, provisional operation availability, D7 certificate/retry bounded fallback, processing-record completion, no browser-visible `/api/v1/commands` or `/api/v1/queries`, 320px plus 200% zoom overflow, missing-party bounded state, unsafe scoped-id no-fetch behavior, and detail action navigation.
- [x] `tests/e2e/specs/admin-area-authorization.spec.ts` - existing coverage includes unauthenticated `/admin/parties/{id}/gdpr` challenge with return URL preservation and no data leakage.

## Coverage

- API/client paths: AdminPortal fixture exercises direct party detail lookup and GDPR operations through `IPartiesAdminPortalApiClient`; no browser REST transport introduced.
- UI features: 4/4 targeted Story 3.1 browser behaviors covered: direct GDPR route, detail entry action, auth challenge, and bounded non-happy direct routes.
- Happy path: direct GDPR route loads the operations destination, focuses the GDPR heading, enables supported provisional controls, and completes processing-record refresh.
- Critical cases: missing and partial party routes render bounded state with no mutation controls; unsafe scoped route id is rejected before fetch; unauthenticated route preserves return URL; D7 certificate/retry remains bounded unavailable.
- Accessibility assertions: semantic roles/labels, focused primary heading, live status for operation completion, no hardcoded waits.
- Privacy assertions: rejected GDPR command correlations and stale prior command correlations are not echoed beside assertive failure announcements.

## Validation

- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1`
- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1`
- [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - 150 tests passed.
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --list`
- [ ] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium --grep "direct GDPR|detail GDPR action"` - blocked before test execution in this sandbox because Kestrel cannot bind a local socket: `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated if applicable: yes, via typed-client E2E fixture and request capture; no public endpoints added.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, Playwright `test`/`expect` and existing .NET fixture patterns.
- Happy path covered: yes.
- Critical error cases covered: yes.
- Proper locators: yes, semantic roles and labels.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets before each spec.
- Tests saved to appropriate directories: yes.
- Summary includes coverage metrics: yes.

## Next Steps

- Run the focused Playwright command above in an environment that permits local Kestrel socket binding.
