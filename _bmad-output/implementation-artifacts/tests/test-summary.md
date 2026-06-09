# Test Automation Summary — Story 1.1 (Stand up the Hexalith.Parties.UI Blazor Server host)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/1-1-stand-up-the-hexalith-parties-ui-blazor-server-host.md`
**Framework (existing, reused):** xUnit v3 (3.2.2) · Shouldly · NSubstitute · bUnit · Aspire.Hosting.Testing
**Mode:** Auto-apply all discovered gaps.

## Context

Story 1.1 stands up a deliberately minimal, bootable FrontComposer shell host plus its Aspire
AppHost resource — nothing routable yet (`GET /` → 404 until Stories 1.3 / 2.x add pages). So the
meaningful automated coverage at this stage is **host DI-composition** and **AppHost topology**,
not page-level browser E2E (which legitimately belongs to Story 1.3 when routes/components land).
Existing coverage was 2 boot smoke tests; this run closes the gaps below.

## Gaps Discovered → Closed

| # | AC | Gap in existing tests | Fix |
|---|----|----|----|
| 1 | AC1 | Composition test omitted `AddFluentUIComponents()` — which AC1 explicitly mandates. | New test composes the AC1 chain **including FluentUI** and asserts FluentUI services are registered. |
| 2 | AC1 | `ValidateScopes=true` (ADR-030) was validated only at `BuildServiceProvider`, never **exercised in a scope** — the captive-dependency failure it guards only surfaces on scoped resolution. | New test opens an `IServiceScope` and resolves `IFrontComposerRegistry` from both root and scope. |
| 3 | AC2 | **Zero automated coverage** of the AppHost `parties-ui` resource (no DAPR sidecar / WaitFor eventstore+tenants / auto-start). Verified live only — no regression guard. | New `DistributedApplicationTestingBuilder` model-inspection test (no Docker; model is inspected, never started). |

## Generated / Modified Tests

### Host composition (AC1) — `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- [x] `HostServiceChain_WithFluentUi_ComposesAndResolvesRegistryWithinScope` — composes the
  AC1-pinned chain (`AddFluentUIComponents()` + FrontComposer Quickstart + `AddHexalithDomain<PartiesUiDomainMarker>()`),
  builds under `ValidateScopes=true`, and resolves `IFrontComposerRegistry` from both the root
  provider **and a created scope** (captive-dependency / ADR-030 guard).
- [x] `HostComposition_RegistersFluentUiComponentServices` — asserts `AddFluentUIComponents()`
  actually contributes `Microsoft.FluentUI.*` services to the container.
- (pre-existing, retained) `QuickstartChainWithDomainMarker_ComposesUnderValidateScopes`,
  `PartiesUiDomainMarker_DeclaresPartiesBoundedContext`.

### AppHost topology (AC2) — `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs`
- [x] `PartiesUiResource_HasNoDaprSidecar_WaitsForDependencies_AndAutoStarts` — builds the
  distributed-application model in-process and asserts the `parties-ui` resource:
  - is a `ProjectResource`;
  - has **no DAPR sidecar** (contrast: `parties` + `tenants` *do* — self-validating);
  - **waits for** `eventstore` and `tenants` (`WaitAnnotation`);
  - **auto-starts** — no `ExplicitStartupAnnotation` (contrast: `parties-mcp` *is* explicit-start).
  - Skips gracefully (`Assert.Skip`) if the model can't be constructed in the environment.
- Support change: `Hexalith.Parties.IntegrationTests.csproj` now copies the AppHost's
  `DaprComponents/**` into the test output so the model builds in-process **without Docker/DAPR**.

## Results

| Suite | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| `Hexalith.Parties.UI.Tests` | 4 | 4 | 0 | 0 |
| `Hexalith.Parties.IntegrationTests` › `PartiesUiTopologyTests` | 1 | 1 | 0 | 0 |

- Builds: `Hexalith.Parties.UI.Tests` and `Hexalith.Parties.IntegrationTests` compile **0 warnings**
  in Release under solution-wide `TreatWarningsAsErrors` (`-m:1`).
- Build gate `scripts/check-no-warning-override.sh` → **OK** (no warning override added; no `NoWarn`).

## Coverage

- **AC1 (host composition + FluentUI + ValidateScopes):** covered — 4 unit tests (2 new + 2 existing).
- **AC2 (AppHost `parties-ui`: no sidecar, WaitFor eventstore+tenants, auto-start):** covered — 1 model-inspection test.
- **AC3 (build gate, 0 warnings, no `Version=`, no override):** validated by the 0-warning Release
  builds + `check-no-warning-override.sh` (a build-gate concern, not a runtime test).
- **API tests:** N/A — the `parties-ui` host exposes **no public API** in Story 1.1 (BFF; the public
  surface is the EventStore gateway, owned by other suites).
- **Browser/page E2E:** intentionally **deferred to Story 1.3** — the 1.1 host registers no routable
  pages yet (`GET /` → 404), and shell rendering is already covered by FrontComposer's own
  `LayoutComponentTestBase`. Pre-building it here would duplicate that and cross story scope.

## Next Steps

- Story 1.3: add bUnit routing/role-landing + page-level E2E once routes and the `Consumer` policy land.
- Run the topology test in CI; it executes without Docker and guards the AC2 wiring against regressions.
