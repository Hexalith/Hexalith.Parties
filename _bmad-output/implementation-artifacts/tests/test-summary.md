# Test Automation Summary

Story: 3.3 - Restrict / lift restriction and record / revoke consent

## Generated Tests

### API Tests
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - existing AdminPortal E2E request capture is used for restriction, lift restriction, add consent, and revoke consent command evidence.
- [x] No public API endpoint or browser-visible EventStore command/query transport was added; the Playwright request hook still fails the scenario if `/api/v1/commands` or `/api/v1/queries` appears in browser traffic.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - extends direct GDPR-route coverage for restrict, lift restriction, add consent, and revoke consent flows.
- [x] Restrict/lift/revoke now assert semantic in-app confirmation groups, polite confirmation announcements, cancel no-op behavior, no native dialog, exactly one fixture request after confirm, accepted `Saved - updating...`, and optimistic restricted/not-restricted badge changes.
- [x] Restriction privacy coverage now asserts sensitive free-text reason copy is not echoed in confirmation text, visible status text, or bounded request snapshots.

## Coverage

- API/request boundary: 4/4 Story 3.3 mutation paths covered through the AdminPortal fixture request capture.
- UI workflows: 4/4 covered from direct GDPR route open through cancel, confirm, status announcement, and request capture.
- Happy path: restriction, lift restriction, add consent, and revoke consent all assert accepted-processing status and command capture.
- Critical error cases: cancel for restrict/lift/revoke issues no request; missing and unsafe-party direct GDPR routes still expose no mutation controls.
- Accessibility assertions: semantic roles/labels, confirmation groups with accessible names, polite confirmation live regions, no hardcoded waits, and 320px plus 200% zoom overflow coverage.
- Privacy assertions: no browser-visible EventStore calls; no native dialogs; free-text restriction reason is not echoed into confirmation/status/request evidence.

## Validation

- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test --list` - discovered 31 specs, including `direct GDPR route opens operations destination without browser-visible command/query calls or overflow`.
- [ ] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts -g "direct GDPR route opens operations destination" --project chromium` - blocked before Playwright test execution because the sandbox denies local Kestrel socket binding: `System.Net.Sockets.SocketException (13): Permission denied`.
- [x] `bash scripts/check-no-warning-override.sh`

## Checklist Result

- API tests generated if applicable: yes, via the gated AdminPortal E2E fixture and request capture.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, Playwright `test`/`expect` and existing fixture patterns.
- Happy path covered: yes.
- Critical error cases covered: yes.
- Proper locators: yes, roles and labels scoped to the detail and confirmation surfaces.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets in `beforeEach`.
- Tests saved to appropriate directories: yes.
- Summary includes coverage metrics: yes.

## Next Steps

- Run the focused Playwright command above in an environment that permits local Kestrel socket binding.
