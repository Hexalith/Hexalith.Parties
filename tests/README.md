# Hexalith.Parties Test Framework

Hexalith.Parties uses a .NET 10, xUnit v3, Shouldly, NSubstitute, bUnit, Testcontainers, and Aspire-hosted integration test framework. The framework is already organized by production package boundary; keep that shape when adding coverage.

## Test Lanes

| Lane | Projects | Use For |
| --- | --- | --- |
| Tier 1 unit and component | `Hexalith.Parties.Contracts.Tests`, `Hexalith.Parties.Authentication.Tests`, `Hexalith.Parties.Client.Tests`, `Hexalith.Parties.Server.Tests`, `Hexalith.Parties.Projections.Tests`, `Hexalith.Parties.Security.Tests`, `Hexalith.Parties.AdminPortal.Tests`, `Hexalith.Parties.ConsumerPortal.Tests`, `Hexalith.Parties.UI.Tests`, `Hexalith.Parties.Picker.Tests`, `Hexalith.Parties.Mcp.Tests` | Pure domain behavior, contract shape, bUnit components, client/package boundaries, projection handler logic, and architectural fitness checks that do not require a live topology. |
| Tier 2 service integration | `Hexalith.Parties.Tests`, `Hexalith.Parties.Sample.Tests` | WebApplicationFactory flows, gateway routing, Dapr client substitutions, event subscriber behavior, and cross-component wiring that can run without a full external environment. |
| Tier 3 topology integration | `Hexalith.Parties.IntegrationTests` | Aspire/Dapr topology, sidecar health, gateway E2E behavior, and tests that depend on Docker, Dapr, or full orchestration. |
| CI publication validation | `Hexalith.Parties.Ci.Tests` | Static checks for the GitHub Actions Zot container publication contract. |

The model-only `PartiesPublishEnvironmentTests` inspect the complete publish
graph without starting Docker or Dapr. In addition to the standard root
dependencies, initialize the optional Memories reference before running the
whole class: `git submodule update --init references/Hexalith.Memories`.
Its default run-model cases also cover the topology with optional services disabled.

## Running Tests

Run the focused lane first, then broaden only when the changed surface justifies it.

```powershell
# Fastest useful feedback for most source changes
.\scripts\test.ps1 -Lane unit

# Service wiring and WebApplicationFactory coverage
.\scripts\test.ps1 -Lane integration

# Dapr/Aspire topology validation
.\scripts\test.ps1 -Lane topology

# CI publication guardrails
.\scripts\test.ps1 -Lane ci

# Full solution test run
.\scripts\test.ps1 -Lane all

# Coverage collection through coverlet collector
.\scripts\test.ps1 -Lane coverage
```

When triaging a blocked lane, add `-ContinueOnFailure` so the run reports every failing project (with a PASS/FAIL summary and exit 1) instead of stopping at the first. Add `-ResultsDirectory TestResults` for inspectable per-project TRX output, and `-Properties <k=v>,<k=v>` to forward build/restore properties such as `MinVerVersionOverride=1.0.0` or `UseHexalithProjectReferences=true`. The default behavior (no `-ContinueOnFailure`) stays fail-fast. See [`docs/development-guide.md`](../docs/development-guide.md) for the full fallback validation ladder.

```powershell
# Run the whole lane, report every failing project, emit inspectable TRX
.\scripts\test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults
```

Direct `dotnet test` must target an individual test project:

```powershell
dotnet test .\tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --configuration Release
dotnet test .\tests\Hexalith.Parties.IntegrationTests\Hexalith.Parties.IntegrationTests.csproj --configuration Release
```

xUnit v3 runs under Microsoft.Testing.Platform here. For focused filters, build the target project and invoke its test executable directly with single-dash arguments such as `-class Fully.Qualified.TestClass` or `-method Fully.Qualified.TestClass.TestMethod`.

## Architecture

Keep tests aligned with production ownership:

- Contracts tests protect stable command, event, value-object, and serialization shapes.
- Server tests cover aggregate `Handle` and `PartyState.Apply` behavior without Dapr or Aspire.
- Projection tests cover tenant isolation, event ordering assumptions, and fail-closed read-side behavior.
- Security tests cover GDPR, erasure, key-management, and audit paths without leaking personal data.
- AdminPortal and Picker tests use bUnit and existing test doubles.
- Integration and topology tests cover Dapr, Aspire, pub/sub, access-control components, and gateway behavior.

Reusable test data and helpers belong in `src/Hexalith.Parties.Testing` when they are shared across assemblies. Prefer pure helper methods first, then framework fixtures only when setup or cleanup has real lifecycle needs.

## Fixture And Data Rules

- Use `PartyTestData` or a nearby factory/helper instead of hardcoded repeated payloads.
- Prefer unique IDs for tests that can run in parallel; avoid shared mutable state.
- Keep assertions visible in the test body. Helpers may arrange or extract data, but should not hide the behavioral claim.
- New async tests should pass `TestContext.Current.CancellationToken` into touched async APIs when practical. The root xUnit1051 suppression covers existing debt, not new habits.
- When a test depends on Dapr/Aspire, place it in the integration or topology lane and document the environmental assumption in the test or fixture.

## Quality Gates

Every new test should be deterministic, isolated, explicit, focused, and fast:

- No sleeps or arbitrary timeouts when an observable state, response, or actor/topology status can be awaited.
- No broad cleanup of generated folders or shared state as part of unrelated tests.
- No personal data, tokens, command payloads, tenant secrets, or local absolute paths in logs, snapshots, or public evidence.
- No recursive submodule initialization in test setup. Root-level submodules are enough unless nested submodules are explicitly requested.
- Use Shouldly assertions and NSubstitute when doubles are needed.

## References

- BMad knowledge fragments: `test-levels-framework`, `test-priorities-matrix`, `test-quality`, `fixture-architecture`, and `data-factories`.
- xUnit.net v3 getting started: https://xunit.net/docs/getting-started/v3/getting-started
- `dotnet test` CLI: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
- coverlet collector: https://github.com/coverlet-coverage/coverlet

## Complete local mTLS qualification (DW-77)

The opt-in qualification starts the actual AppHost with `EnableMemoriesSearch`
and `EnableEventStoreSampleUi` enabled, then explicitly starts the existing admin
UI resource. It requires Linux, Docker, .NET 10, the pinned Aspire/Dapr tooling,
`bash`, `curl`, and the standard initialized root references plus
`references/Hexalith.Memories`. It uses the existing mTLS bootstrap and owns fresh
Redis, Redis Stack and FalkorDB containers; it never routes fixture state to
shared developer Redis. The fixed local Dapr control-plane names and ports
require serial execution with other Parties topologies stopped.

```bash
dotnet build tests/Hexalith.Parties.IntegrationTests -c Debug \
  -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
PARTIES_MTLS_TOPOLOGY_TESTS=true dotnet \
  tests/Hexalith.Parties.IntegrationTests/bin/Debug/net10.0/Hexalith.Parties.IntegrationTests.dll \
  -class Hexalith.Parties.IntegrationTests.Topology.PartiesMtlsTopologyTests
```

The runtime check requires all eight sidecar metadata responses and a real
Parties-to-Tenants `GET /ready` through the existing identity-specific ACL. It
also checks the production tenant callback with authenticated bootstrap envelopes
and rejects missing/wrong channel tokens. Tenant projection contents and access
decisions are inspected separately through production subscription, security and
access-service registrations in a controlled in-process host. The process-external
service has no public tenant-projection read endpoint.
This qualifies local mTLS transport and the registered subscription-handler trust
boundary; it does not certify external deployment, optional Memories search
behavior, or an authenticated command/projection round trip. Local runs without
the opt-in flag skip the runtime case; GitHub Actions requires it and treats
infrastructure failures as test failures. A skip cannot close DW-77.
