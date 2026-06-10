# Test Automation Summary

## Generated Tests

### API Tests
- [x] Not applicable for Story 4.1: no API endpoint or production binding implementation was added.

### E2E / Topology Tests
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs` - Pins the accepted admin-link runtime boundary against the committed Keycloak realm import:
  - `party-id-mapper` maps user attribute `party_id` to claim `party_id`.
  - Mapper is single-valued with `multivalued=false`.
  - Mapper emits to id token, access token, and userinfo.
  - Bound Consumer seed user carries the `Consumer` role and exactly one synthetic `party_id`.
  - Consumer seed users cannot emit empty or ambiguous `party_id` attributes.

## Coverage

- API endpoints: 0/0 applicable for the Story 4.1 decision spike.
- Browser UI E2E workflows: 0 new Playwright workflows; no Story 4.1 UI workflow exists, and the existing Playwright harness has no authenticated Consumer fixture.
- Static topology contract: 3 new tests cover the ADR-selected IdP claim mapper and bound Consumer seed shape.
- Existing host routing coverage: `tests/Hexalith.Parties.UI.Tests/NoPartyBindingRoutingTests.cs` already covers bound, unbound, and ambiguous Consumer routing to `/me` or `NoPartyBinding`.

## Validation

- `git diff --check` passed.
- `dotnet test tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Release --no-restore --filter ConsumerPartyIdBindingRealmTests` attempted; local build failed before test execution with `Build failed with exit code: 1`.
- `dotnet test tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Release --filter ConsumerPartyIdBindingRealmTests -v minimal` attempted; local restore/build failed after `Determining projects to restore...` with `Build failed with exit code: 1`.
- `pwsh scripts/test.ps1 -Lane topology` attempted; local harness printed the same restore/build failure.

## Next Steps

- Rerun the topology lane once the local restore/build harness issue is resolved.
- When Story 4.2 adds the provisioning surface, extend Playwright or host-level tests with an authenticated Consumer fixture for bound and removed/unbound browser flows.
