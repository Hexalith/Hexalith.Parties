# Test Automation Summary

Story: 3.6 - Admin erasure-verification report UI consumes the D7 contract

## Generated Tests

### API Tests
- [x] No new public API endpoint was introduced for Story 3.6; API-level coverage remains at existing client/service seams.
- [x] Existing AdminPortal service/client tests cover D7 certificate query and retry command availability, contract-unavailable mapping, nullable certificate behavior, sanitized failure handling, and capability transitions.
- [x] Existing bUnit test double coverage in `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` supports certificate success/failure queues and retry outcomes used by Story 3.6 component tests.

### E2E Tests
- [x] `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts` - verifies real-contract mode reads a bounded certificate, retries verification, refreshes to complete status, hides retry after completion, captures certificate/retry requests, and blocks browser-visible EventStore gateway calls.
- [x] `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts` - adds provisional-contract fallback coverage on a phone viewport: verification not available state, neighboring GDPR controls remain enabled, retry stays hidden, certificate/retry calls are not made, and raw backend/PII strings are not rendered.

### Component Tests
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - existing Story 3.6 bUnit coverage verifies bounded DPO report rendering, unavailable fallback, provisional no-certificate-call behavior, retry success refresh, certificate contract-unavailable mapping, and stale-party guards.

## Coverage

- API/client contract paths: existing focused tests cover certificate query and retry command envelopes, nullable certificate semantics, sanitized failure mapping, capability true/false transitions, projection query actor certificate routing, and retry domain invocation.
- UI report workflows: 2/2 discovered Story 3.6 E2E browser workflows covered (real D7 contract happy path plus provisional fallback/error path).
- Critical error/fallback cases: provisional bridge, contract-unavailable certificate fetch, no raw backend details, no certificate calls when unsupported, stale request guards, and retry refresh behavior are covered by bUnit and E2E.
- Accessibility/privacy assertions: role/status fallback assertions, semantic Playwright locators, mobile viewport fallback coverage, no hardcoded waits, no raw `ProblemDetails`, `ContractUnavailable`, `destroyed-key`, state key, party id, or key-material leakage in generated E2E assertions.

## Validation

- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`
- [x] `./tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests -noLogo -noColor -parallel none -method ...` - focused Story 3.6 bUnit methods: 5 total, 0 failed, 0 skipped.
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium --list` - discovered 2 generated tests.
- [ ] `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-build --filter ...` - blocked before execution by sandbox named-pipe binding: `System.Net.Sockets.SocketException (13): Permission denied`; in-process xUnit v3 runner used instead.
- [ ] `cd tests/e2e && npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium` - blocked before test execution because this sandbox denies local Kestrel socket binding: `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated if applicable: yes, existing client/service/API-seam tests are applicable; no new endpoint was added.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, Playwright `test`/`expect` plus xUnit v3, Shouldly, and bUnit.
- Happy path covered: yes.
- Critical error cases covered: yes.
- Proper locators: yes, role/label/text locators scoped to the party detail/report surface.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets in `beforeEach`; real-contract mode is opt-in per test cookie.
- Tests saved to appropriate directories: yes.
- Summary includes coverage metrics: yes.

## Next Steps

- Run the focused Playwright command in an environment that permits local Kestrel socket binding.
