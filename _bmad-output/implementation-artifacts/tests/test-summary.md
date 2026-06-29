# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` - Added a gateway negative test proving legacy PascalCase `PartyDetail` projection routing is rejected with `400 BadRequest` before query routing.
- [x] `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs` - Updated admin GDPR query contract assertions to require `PartyProjectionNames.Detail` for detail projection queries.

### E2E Tests
- [x] Existing API/E2E-style gateway coverage exercises EventStore query gateway routing for detail, index, and search projection adapters.
- [x] No Playwright UI test was added because story 6.4 changes projection names and actor id builders only; no UI workflow changed.

## Coverage
- API gateway projection-name validation: covered for canonical happy paths and legacy PascalCase rejection.
- Admin GDPR client detail projection queries: 5/5 relevant query methods now assert the canonical detail projection name.
- UI workflows: not applicable for this story.

## Validation
- `dotnet build src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj -c Release --no-restore -p:DisableTransitiveProjectReferences=true` passed.
- Existing Release test binaries passed for `AdminPortalGdprOperationContractTests` and `EventStoreGatewayRoutingTests`, but those binaries could not be refreshed because the focused project builds are blocked before compilation.
- `git diff --check` passed.

## Notes
- Direct `dotnet build` for `src/Hexalith.Parties.Client`, `tests/Hexalith.Parties.Client.Tests`, and `tests/Hexalith.Parties.Tests` remains blocked by a no-error MSBuild project-reference failure while resolving `Hexalith.EventStore.Contracts` as a project reference. The referenced EventStore contracts project builds successfully on its own, and `Hexalith.Parties.Contracts` builds with `DisableTransitiveProjectReferences=true`.

## Checklist
- [x] API tests generated where applicable.
- [x] E2E tests generated where UI exists; no UI/browser workflow applies to story 6.4.
- [x] Tests use standard xUnit v3 and Shouldly APIs.
- [x] Tests cover happy path through existing canonical projection gateway routing.
- [x] Tests cover a critical error case for legacy PascalCase projection type rejection.
- [x] Tests use semantic locators where UI E2E applies; no Playwright locator changes were needed.
- [x] Tests have clear descriptions.
- [x] No hardcoded waits or sleeps were added.
- [x] Tests are independent and have no order dependency.
- [x] Test summary created.
- [x] Tests saved to appropriate directories.
- [x] Summary includes coverage metrics.
