# Test Automation Summary

Story: 3.4 - Data export (Art.20) and processing records (Art.30)

## Generated Tests

### API Tests
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - extends the AdminPortal E2E fixture with export and processing-record request capture.
- [x] The fixture now returns a bounded JSON portability package and bounded Art.30 records without personal fields.
- [x] Browser request hooks continue to fail the scenario if `/api/v1/commands` or `/api/v1/queries` appears in browser-visible traffic.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - adds direct erased-party GDPR route coverage for Art.20 export and Art.30 processing records.
- [x] The scenario asserts export and records remain available while mutation controls are disabled, no native dialogs open, and no browser-visible EventStore calls are made.
- [x] The scenario asserts safe filename/status copy, single export and records fixture requests, bounded Art.30 labels and values, 320px/200% zoom overflow safety, and no erased personal data in UI or URL.

## Coverage

- API/request boundary: 2/2 Story 3.4 read/export paths covered through AdminPortal fixture request capture.
- UI workflows: 2/2 covered for direct erased GDPR route export and processing-record retrieval.
- Happy path: non-empty portability export and processing-record rendering covered.
- Critical error cases: existing bUnit coverage covers empty export payload, JS helper failure, contract failure, stale export suppression, gone route, and unsafe route IDs.
- Accessibility/privacy assertions: semantic role/label locators, live status checks, no hardcoded waits, no native dialogs, no browser-visible gateway calls, safe filename derivation, and narrow/zoom overflow coverage.

## Validation

- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` - 162 passed, 0 failed, 0 skipped.
- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test --list` - discovered 32 specs, including the new Story 3.4 erased-party GDPR route scenario.
- [ ] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts -g "direct GDPR route for an erased party keeps export and records available without PII" --project chromium` - blocked before Playwright test execution because the sandbox denies local Kestrel socket binding: `System.Net.Sockets.SocketException (13): Permission denied`.
- [x] `bash scripts/check-no-warning-override.sh`

## Checklist Result

- API tests generated if applicable: yes, via the gated AdminPortal E2E fixture and existing bUnit client/component coverage.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, Playwright `test`/`expect`, bUnit, xUnit v3, and existing fixture patterns.
- Happy path covered: yes.
- Critical error cases covered: yes through existing focused bUnit tests plus unsafe/missing route E2E coverage.
- Proper locators: yes, roles and labels scoped to the detail surface.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets in `beforeEach`.
- Tests saved to appropriate directories: yes.
- Summary includes coverage metrics: yes.

## Next Steps

- Run the focused Playwright command above in an environment that permits local Kestrel socket binding.
