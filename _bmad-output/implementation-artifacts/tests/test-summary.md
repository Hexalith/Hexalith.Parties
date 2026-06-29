# Test Automation Summary

## Generated Tests

### API Tests
- [x] No public API endpoint tests added. Story 6.1 consolidates BCL claim constants/extraction helpers and consumer binding behavior, with no new browser-callable or gateway endpoint.
- [x] `tests/Hexalith.Parties.Contracts.Tests/Authorization/PartiesClaimExtractionTests.cs` - Added fail-closed extraction coverage for missing/empty tenant claims, ambiguous `sub` without `oid` fallback, ambiguous `oid` fallback, and duplicate same-value `party_id` claims.
- [x] `tests/Hexalith.Parties.UI.Tests/PartyIdClaimResolverTests.cs` - Tightened resolver negatives so empty and multiple `party_id` cases include a valid tenant, isolating the party-binding failure path.

### E2E Tests
- [x] `tests/e2e/specs/consumer-party-binding.spec.ts` - Added the `no-tenant` Consumer state so a principal with a valid `party_id` but no normalized tenant claim is routed to `/no-party-binding`, never `/me`.
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - Added the test-only `no-tenant` fixture state and moved fixture claim literals to `PartiesClaimTypes`.

## Coverage

- API endpoints: 0/0 public endpoints applicable.
- Claim extraction helpers: normalized tenant success, missing tenant, empty tenant, `sub` before `oid`, empty `sub` fallback, ambiguous `sub`, ambiguous `oid`, missing user id, empty `party_id`, ambiguous different `party_id`, duplicate same-value `party_id`, ambiguous tenant, and `ClaimsIdentity` parity.
- Consumer binding UI: bound Consumer reaches `/me`; unbound, empty, ambiguous, missing-tenant, suspended, and removed Consumers route to `/no-party-binding` without browser-visible gateway calls.

## Validation

- [x] `npm run typecheck` in `tests/e2e` passed.
- [x] `git diff --check` passed.
- [x] Raw claim literal scan reports only `PartiesClaimTypes.cs` and the intentional Keycloak mapper wire-value topology test.
- [ ] `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release --no-restore -v:minimal` could not compile tests in this environment. MSBuild exits during `_GetProjectReferenceTargetFrameworkProperties` with `Build FAILED. 0 Warning(s) 0 Error(s)`, matching the pre-existing Story 6.1 validation blocker.
- [ ] `dotnet test tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -v:minimal` hit the same pre-compilation MSBuild failure.
- [ ] `npm run test -- specs/consumer-party-binding.spec.ts --project=chromium` could not start the Playwright web server in this sandbox: Kestrel failed to bind with `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist

- [x] API tests generated if applicable.
- [x] E2E tests generated for the Consumer binding UI.
- [x] Tests use standard xUnit/Shouldly and Playwright APIs.
- [x] Tests cover happy paths.
- [x] Tests cover critical fail-closed error cases.
- [x] Tests use semantic, accessible locators.
- [x] Tests have clear descriptions.
- [x] No hardcoded waits or sleeps were added.
- [x] Tests are independent and reset fixture state.
- [x] Test summary created.
- [x] Tests saved to the existing test directories.
- [x] Summary includes coverage metrics.
