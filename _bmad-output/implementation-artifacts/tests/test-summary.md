# Test Automation Summary

Story: 2.5 - Party picker re-skin + full WAI-ARIA combobox (D11 / UX-DR7)

## Generated Tests

### API Tests
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - adds a Test-environment-only `IPartiesQueryClient` fixture for picker search and selected-display requests, with request capture for Playwright assertions.
- [x] No public API endpoints were added; picker traffic remains through `PartyPickerApiClient -> IPartiesQueryClient`.

### E2E Tests
- [x] `src/Hexalith.Parties.UI/Components/Specimens/PartyPickerSpecimen.razor` - adds a gated test specimen route for the real Blazor `PartyPicker` with a server-side access-token provider.
- [x] `tests/e2e/specs/party-picker.spec.ts` - covers input-owned combobox focus, `aria-controls`, `aria-expanded`, `aria-activedescendant`, non-tabstop options, keyboard selection, bounded `party-selected` detail, degraded-result Escape behavior, and AdminPortal bridge compatibility.

## Coverage

- API/client paths: picker search and selected-display resolution through typed `IPartiesQueryClient`; no browser REST transport introduced.
- UI features: 3/3 targeted Story 2.5 browser behaviors covered: combobox keyboard selection, degraded popup close, and AdminPortal `party-selected` bridge binding.
- Happy path: keyboard search/select for `Ada Lovelace`, selected-party display, input focus retention, bounded event payload.
- Critical cases: degraded result text plus Escape close without durable selection mutation; bridge ignores extra event fields in displayed output.
- Accessibility assertions: semantic Playwright roles/labels, listbox relationship checks, active-descendant assertions, no hardcoded waits.

## Validation

- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `cd tests/e2e && npm run typecheck`
- [x] `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1 -p:NuGetAudit=false`
- [x] `DiffEngine_Disabled=true tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` - 258 tests passed.
- [ ] `cd tests/e2e && npm run test -- specs/party-picker.spec.ts --project=chromium` - blocked before test execution in this sandbox because Kestrel cannot bind a local socket: `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated if applicable: yes, via the typed-client E2E fixture and request capture; no public endpoints added.
- E2E tests generated if UI exists: yes.
- Tests use standard framework APIs: yes, Playwright `test`/`expect` and existing .NET fixture patterns.
- Happy path covered: yes.
- Critical error/degraded cases covered: yes.
- Proper locators: yes, semantic roles and labels.
- Clear descriptions: yes.
- No hardcoded waits or sleeps: yes.
- Tests independent: yes, fixture state resets before each spec.
- Tests saved to appropriate directories: yes.
- Summary includes coverage metrics: yes.

## Next Steps

- Run `cd tests/e2e && npm run test -- specs/party-picker.spec.ts --project=chromium` in an environment that permits local Kestrel socket binding.
