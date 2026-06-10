# Test Automation Summary

## Generated Tests

### API / Service Tests
- [x] `tests/Hexalith.Parties.UI.Tests/IdentityBindingProvisioningServiceTests.cs` - Covers admin-link provisioning service behavior: link, duplicate active rejection, rotation, version conflict, suspend/remove clearing, unauthorized operator denial, reconcile authorization, drift handling, IdP failure rollback, retained relink audit history, and bounded audit metadata.
- [x] `tests/Hexalith.Parties.UI.Tests/IdentityBindingBoundaryTests.cs` - Covers static no-event-stream, no public HTTP endpoint, no DAPR ACL expansion, and no browser-token/secrets regression boundaries.

### E2E Tests
- [x] `tests/e2e/specs/consumer-party-binding.spec.ts` - Adds browser-level Consumer landing coverage for Story 4.2 runtime claim shapes.
  - Bound Consumer with exactly one `party_id` reaches `/me`.
  - Unbound, empty, ambiguous, suspended, and removed runtime shapes reach `/no-party-binding`.
  - The flow performs no browser-visible `/api/v1/commands` or `/api/v1/queries` calls.
- [x] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` - Extends the Test-environment fixture with synthetic Consumer principals used by the Playwright workflow.

### Topology Tests
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs` - Pins the committed Keycloak mapper contract: `party_id`, `multivalued=false`, and token-surface emission.

## Coverage

- API endpoints: 0/0 applicable. Story 4.2 intentionally exposes no public identity-binding HTTP endpoint.
- Service/API behavior: duplicate active binding rejection, rotation, suspend/remove, unauthorized operator denial, reconcile authorization, optimistic version conflict, drift handling, IdP failure rollback, retained relink audit history, and audit metadata guardrails covered.
- Browser UI workflows: 6 Consumer landing cases covered by Playwright spec.
- Runtime claim shapes: bound, absent, empty, duplicated, suspended, and removed covered.
- Boundary guardrails: event-stream placement, public endpoint, DAPR ACL, and browser-token boundaries covered.

## Validation

- [x] `npm run typecheck` in `tests/e2e` passed.
- [x] `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- [x] `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- [x] `dotnet build tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- [x] `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor` passed: 273 tests, 0 failed.
- [x] `tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests -noLogo -noColor -namespace Hexalith.Parties.IntegrationTests.Topology` passed: 10 tests, 0 failed.
- [x] `git diff --check` passed.
- [x] Review rerun: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- [x] Review rerun: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor` passed: 278 tests, 0 failed.
- [x] Review rerun: `dotnet build tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- [x] Review rerun: `tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests -noLogo -noColor -namespace Hexalith.Parties.IntegrationTests.Topology` passed: 10 tests, 0 failed.
- [x] Review rerun: `npm run typecheck` in `tests/e2e` passed.
- [x] Review rerun: `git diff --check` passed.
- [ ] `npm run test -- specs/consumer-party-binding.spec.ts --project=chromium` could not start the Playwright web server in this sandbox: Kestrel failed with `System.Net.Sockets.SocketException (13): Permission denied`.

## Next Steps

- Rerun the Playwright spec in an environment that permits Kestrel to bind the configured local port.
