# Test Automation Summary

Story: 3.5 - EventStore erasure-verification contract

## Generated Tests

### API Tests
- [x] Existing Story 3.5 API/contract tests cover `GetErasureCertificateAsync`, `RetryErasureVerificationAsync`, bounded error mapping, projection-query routing, capability transitions, retry handler behavior, and DAPR ACL static validation.
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` now captures erasure-certificate and retry-verification requests for the AdminPortal E2E seam.
- [x] The fixture keeps the default provisional bridge disabled for certificate/retry and adds an opt-in Story 3.5 real-contract mode for UI workflow tests.

### E2E Tests
- [x] `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts` - verifies the real-contract GDPR panel flow: refresh failed verification status, retry verification, refresh to complete status, render bounded certificate state, and capture certificate/retry requests.
- [x] The scenario asserts no browser-visible `/api/v1/commands` or `/api/v1/queries` calls, no raw ProblemDetails/state-key/destroyed-key text, and no key-material leakage in fixture snapshots.

## Coverage

- API/client contract paths: existing focused Story 3.5 tests cover certificate query and retry command envelopes, nullable certificate semantics, sanitized failure mapping, capability true/false transitions, projection query actor certificate routing, and retry domain invocation.
- UI workflows: 1/1 discovered Story 3.5 E2E gap covered for the AdminPortal certificate/retry consumption seam.
- Happy path: retryable verification-failed state transitions to completed status with a verified certificate.
- Critical error/fallback cases: provisional bridge remains covered by existing E2E fallback assertions and existing bUnit/API tests cover contract-unavailable and sanitized failure outcomes.

## Validation

- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium --list` - discovered 1 generated test.
- [x] `bash scripts/check-no-warning-override.sh`
- [ ] `cd tests/e2e && npm run test -- specs/admin-gdpr-erasure-verification.spec.ts --project=chromium` - blocked before test execution because this sandbox denies local Kestrel socket binding: `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated if applicable: yes, existing focused Story 3.5 API/contract lanes were already present; this run added missing E2E fixture request capture.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, Playwright `test`/`expect` plus the existing AdminPortal E2E fixture pattern.
- Happy path covered: yes.
- Critical error cases covered: yes through the existing fallback/sanitization tests and the preserved provisional E2E behavior.
- Proper locators: yes, role/label locators scoped to the party detail surface.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets in `beforeEach` and Story 3.5 real-contract mode is enabled by a test-only cookie.
- Tests saved to appropriate directories: yes.
- Summary includes coverage metrics: yes.

## Next Steps

- Run the focused Playwright command above in an environment that permits local Kestrel socket binding.
