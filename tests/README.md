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

## Browser UI and visual checks

From `tests/e2e`, `npm run test:ui` runs the accessibility specimen and the admin,
consumer and picker browser flows together, retaining one complete report. This
is the blocking CI command. `npm run test:portals` runs only those three portal
specs; `npm run test:a11y` runs only the specimen.

`npm run test:visual` compares the deterministic specimen with the committed
Chromium/Linux PNG under `specs/parties-accessibility.spec.ts-snapshots/`.
Use Ubuntu 24.04 and the locked browser installation. After building the UI in
Release/package mode, run `CI=true PLAYWRIGHT_SKIP_WEBSERVER= npm run test:visual:update`
to regenerate the CI PNG, then inspect its diff. The pixel case is skipped on
Windows/macOS; its separate semantic baseline test still runs there. The separately maintained
semantic JSON under `baselines/` is checked as well and is not rewritten by this
image update command. See [the visual contract and exact build commands](../docs/accessibility.md).

## Forward skip-link source qualification (DW-91)

The normal `npm run test:a11y` gate retains the published-package skip-target
coverage. FrontComposer 4.7.0 predates the route-heading focus fix, so two
additional forward-Tab regressions are explicitly opt-in until a fixed package
is published and adopted. They never detect support from the application under
test: enabling them against an unfixed shell must fail.

Serve a Debug UI whose keyboard asset is the selected FrontComposer candidate,
then run from `tests/e2e` with its explicit URL:

```bash
BASE_URL=http://127.0.0.1:5073 PARTIES_E2E_FORWARD_SKIP_LINKS=1 PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test:a11y
```

The flag enables assertions only; it never substitutes application assets or
infers support. The default local Playwright server uses package dependencies,
so it cannot satisfy these assertions yet. The DW-91 evidence records a local
diagnostic host that substitutes only `fc-keyboard.js` through a temporary static
asset manifest, alongside an unmodified package host. This proves candidate
integration, not published-package adoption. Do not enable the flag in the
Release/package CI lane until a fixed package is adopted. DW-91 stays open for
publication/adoption and removal of the opt-in.

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
- No personal data, tokens, command payloads, tenant secrets, or local absolute paths in logs, snapshots, or public evidence. Public command/source receipts normalize local prefixes to documented `${VARIABLE}` placeholders while retaining exact relative paths, revisions, hashes, run IDs and arguments. Raw originals and placeholder values stay private.
- No recursive submodule initialization in test setup. Root-level submodules are enough unless nested submodules are explicitly requested.
- Use Shouldly assertions and NSubstitute when doubles are needed.

## References

- BMad knowledge fragments: `test-levels-framework`, `test-priorities-matrix`, `test-quality`, `fixture-architecture`, and `data-factories`.
- xUnit.net v3 getting started: https://xunit.net/docs/getting-started/v3/getting-started
- `dotnet test` CLI: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
- coverlet collector: https://github.com/coverlet-coverage/coverlet

## Complete local mTLS and isolation qualification

The ordinary health fixture remains unseeded. The explicit complete profile enables
Memories and the EventStore sample UI, starts the existing admin UI resource, and
seeds a synthetic tenant/member through the authenticated production callback.
It requires Linux, Docker, .NET 10, the pinned Aspire/Dapr tooling, `bash`, `curl`,
and the initialized root references including `references/Hexalith.Memories`.

Every fixture owns its control-plane names, loopback listeners (including each
sidecar's internal gRPC listener), trust/configuration directories and disposable
Redis, Redis Stack and FalkorDB containers. Configuration enters the AppHost before
model construction; the fixture does not change process-wide environment values.
Cleanup retains ownership evidence when Docker inspection or ownership checks fail.

`PartiesMtlsTopologyTests` and `EventStoreGatewayE2ETests` retain the original app
identities, namespaces and authoritative ACL bytes and run serially. Stop other
topologies using those original identities before this profile. The concurrent
`PartiesTopologyIsolationTests` profile uses suffixed literal app IDs, a private
namespace, remapped generated ACL/routing/workload identities and its own Sentry.
It verifies distinct persisted streams for the same party ID, rejects cross-session
calls and creates/queries another party after disposing the other session.

Concurrent routing qualification covers the exercised Parties gateway and Tenants flows. Reference admin/sample UI application clients still hardcode upstream target app IDs and are not concurrently qualified; their source remains unchanged.

For the Release/package consumer qualification:

```bash
dotnet build tests/Hexalith.Parties.IntegrationTests -c Release -m:1
PARTIES_MTLS_TOPOLOGY_TESTS=true dotnet \
  tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests.dll \
  -class Hexalith.Parties.IntegrationTests.Topology.PartiesMtlsTopologyTests \
  -class Hexalith.Parties.IntegrationTests.Gateway.EventStoreGatewayE2ETests \
  -class Hexalith.Parties.IntegrationTests.Topology.PartiesTopologyIsolationTests
```

The gateway case asserts an authenticated projected detail with current freshness
and independently deserializes exact Redis actor metadata/event keys derived from
`AggregateIdentity`, without scans. The ACL cases require Dapr's policy-denial
response for forbidden caller/verb/path combinations and successful permitted
controls. Application rejection alone does not establish ACL enforcement.

Without the explicit flag these runtime cases skip locally; GitHub Actions requires
them and infrastructure failures fail the tests. Skips provide no qualification.
The [batch receipts](../_bmad-output/implementation-artifacts/tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json)
separate these runtime results from source Debug consumer-build blockers. Use the
pinned module tool's generated mapping for Debug source builds; do not substitute
handwritten source selectors. These owner tests do not close DW-116 or qualify
Memories persistence, rich search or external deployment.

## Browser harness configuration checks

Run `npm --prefix tests/e2e run test:config` to execute the CI/local and
Test/Development server-ownership matrix without starting a browser. It also
uses the real Playwright runner to verify that a managed run refuses an
unrelated listener and starts and cleans up its own fixture. The required UI accessibility CI job runs these checks.
Managed runs start their own Test fixture; explicitly set
`PLAYWRIGHT_SKIP_WEBSERVER=1` and `BASE_URL` when using an externally managed host.
The UI stylesheet guard uses restore-evaluated build metadata, so changing
NuGet cache settings requires restoring and rebuilding the test project.
The CI job also runs `python3 tests/Hexalith.Parties.UI.Tests/verify-restored-assets.py`
(from the repository root) to compile and execute the real stylesheet resolver
against isolated primary and fallback NuGet caches without external feeds.
