# Test Automation Summary

Story: 3.2 - Erase a party with typed-name confirmation

## Generated Tests

### API Tests
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - captures erasure requests by `partyId` only so browser tests can prove the typed confirmation value is not passed through the test API/request-capture boundary.
- [x] No public API endpoint or browser-visible command/query transport was added; the Playwright request hook continues to fail the scenario if `/api/v1/commands` or `/api/v1/queries` appears in browser traffic.

### Component Tests
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - covers the typed-confirm dialog semantics, disabled-until-exact-match behavior, no command on mismatch/cancel, typed value cleanup, accepted-processing status, outcome politeness split, and unavailable-state guardrails.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - extends direct GDPR-route coverage for the erase dialog: `role="dialog"`, `aria-modal`, `aria-labelledby`, `aria-describedby`, focused typed-confirm input, disabled mismatch, no erasure request before exact match, safe cancel with typed value cleared from markup, polite enablement live region, accepted-processing status, no native dialogs, no display name in captured erasure request/URL, no browser-visible command/query calls, and 320px plus 200% zoom overflow.
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - retains critical route error cases for missing parties and unsafe scoped ids with bounded no-mutation GDPR state.

## Coverage

- API/request boundary: 1/1 Story 3.2 erase command path covered through the AdminPortal fixture request capture; typed names are not represented in the capture shape.
- UI workflow: 1/1 typed-name erase workflow covered from direct GDPR route open through cancel, exact-match confirm, optimistic accepted-processing acknowledgement, and request capture.
- Happy path: exact display-name match enables Erase, announces enablement politely, confirms through the UI fixture, and shows `Saved - updating...`.
- Critical error cases: mismatch leaves Erase disabled and issues no request; cancel clears the typed value; missing and unsafe party routes expose no GDPR mutation controls.
- Accessibility assertions: semantic locators, modal ARIA attributes, accessible title wiring, described typed input, focused input, polite live-region status, and no hardcoded waits.
- Privacy assertions: typed mismatch is removed after cancel; exact display name is not serialized into the erasure request capture or route URL; browser-visible EventStore command/query calls remain forbidden.

## Validation

- [x] `cd tests/e2e && npm run typecheck`
- [x] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --list` - discovered 18 specs, including `direct GDPR route opens operations destination without browser-visible command/query calls or overflow`.
- [ ] `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium --grep "direct GDPR route opens operations"` - blocked before Playwright test execution because the sandbox denies local Kestrel socket binding: `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated if applicable: yes, via the gated AdminPortal e2e fixture and request capture.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, Playwright `test`/`expect` and existing fixture patterns.
- Happy path covered: yes.
- Critical error cases covered: yes.
- Proper locators: yes, roles, labels, dialog status, and text scoped to the dialog/detail surfaces.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets in `beforeEach`.
- Tests saved to appropriate directories: yes.
- Summary includes coverage metrics: yes.

## Next Steps

- Run the focused Playwright command above in an environment that permits local Kestrel socket binding.
