# Test Automation Summary

## Generated Tests

### API Tests
- [x] Not applicable - Story 1.9 adds UI accessibility primitives and gates, not public API endpoints.

### bUnit Tests
- [x] `tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs` - Skip-link order, target focusability, and named shell landmarks.
- [x] `tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs` - Focus suppression guardrails, forced-colors/reduced-motion rules, and raw filled-button color guards.
- [x] `tests/Hexalith.Parties.UI.Tests/PartiesAccessibilitySpecimenTests.cs` - Specimen route enablement and deterministic representative content.

### E2E Tests
- [x] `tests/e2e/specs/parties-accessibility.spec.ts` - Specimen readiness, blocking axe gate, skip-link keyboard flow, representative control focus flow, forced-colors/reduced-motion observability, primary-button raw-teal guard, and visual baseline assertion.
- [x] `tests/e2e/helpers/a11y.ts` - Serious/critical axe violations fail, minor/moderate findings are report-only, and unknown impacts fail explicitly.

## Coverage

- API endpoints: 0/0 applicable.
- UI shell accessibility acceptance criteria: 6/6 covered by bUnit, Playwright, CI wiring, or documentation tests.
- Playwright gate checks listed: 6/6 in `npm run test:a11y`, including the visual baseline test.

## Validation

- [x] `dotnet build src/Hexalith.Parties.UI -c Release -m:1`
- [x] `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1`
- [x] Direct xUnit runner for Story 1.9 UI test classes: 13 tests passed.
- [x] `bash scripts/check-no-warning-override.sh`
- [x] `cd tests/e2e && npm ci` (completed with a Node engine warning because this sandbox has Node 22.22.1; CI uses Node 24)
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npm run test:a11y -- --list` lists all 6 Playwright checks.
- [ ] `cd tests/e2e && npm run test:a11y` full browser execution is blocked in this sandbox before tests run because Kestrel cannot bind `127.0.0.1:5072` (`System.Net.Sockets.SocketException (13): Permission denied`).
- [ ] Initial Playwright screenshot baseline could not be generated or validated in this sandbox for the same local-socket reason.

## Applied Test Gap Fixes

- `tests/e2e/package.json` now runs the visual baseline check as part of `npm run test:a11y` instead of excluding it.
- `.github/workflows/test.yml` labels the CI step as the Playwright a11y and visual gate to match the command behavior.
