# Story 1.1: Set Up Initial Project from EventStore Solution Structure

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer starting Hexalith.Parties,
I want the initial solution scaffold to follow the EventStore solution-structure pattern,
so that Parties validates the reusable domain-service starter approach before domain behavior is added.

## Acceptance Criteria

1. **EventStore-shaped solution entry point exists**
   - Given the architecture selects the EventStore solution structure pattern as the starter,
   - When the Parties solution structure is reviewed,
   - Then the repository uses `Hexalith.Parties.slnx` as the documented solution entry point,
   - And `/src`, `/tests`, and `/samples` are represented,
   - And a generic `dotnet new webapi` scaffold is not the architectural baseline.

2. **Root build configuration follows shared conventions**
   - Given the initial project structure is present,
   - When build configuration files are reviewed,
   - Then `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and `.editorconfig` consistently configure .NET 10, nullable, warnings-as-errors, MinVer, central package management, CRLF, UTF-8, file-scoped namespace conventions, and shared package metadata,
   - And package versions are not scattered into individual project files.

3. **Project boundaries are represented without forbidden references**
   - Given the initial projects are present,
   - When project references are inspected,
   - Then the current source, test, and sample projects map to the approved Parties boundaries only as needed for subsequent stories,
   - And forbidden dependency directions are not introduced.

4. **Starter validation runs without nested submodule recursion**
   - Given the starter setup is validated,
   - When restore/build or structural validation runs,
   - Then the solution can be restored and built enough to support the first domain story,
   - And validation does not require recursive nested submodule initialization.

5. **Reusable starter lessons are visible**
   - Given starter setup documentation is reviewed,
   - When a future domain service uses Parties as a reference,
   - Then intentional deviations from EventStore structure are documented,
   - And reusable platform starter lessons are visible to future service scaffolding work.

## Tasks / Subtasks

- [ ] Task 1: Reconcile the current scaffold with EventStore starter requirements (AC: 1, 2, 5)
  - [ ] Confirm `Hexalith.Parties.slnx`, `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `README.md`, and `LICENSE` exist at the repository root.
  - [ ] Compare root build/style files against the EventStore pattern and the current project-context rules; document intentional Parties deviations instead of overwriting files blindly.
  - [ ] Confirm `global.json` pins SDK `10.0.103` with `rollForward: latestPatch`.
  - [ ] Confirm package versions live in `Directory.Packages.props`; do not add `Version=` attributes to project-local package references.

- [ ] Task 2: Validate source, test, and sample boundaries (AC: 1, 3)
  - [ ] Confirm the solution includes current Parties source projects under `src/`: `Hexalith.Parties`, `Hexalith.Parties.AdminPortal`, `Hexalith.Parties.AppHost`, `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, `Hexalith.Parties.Mcp`, `Hexalith.Parties.Picker`, `Hexalith.Parties.Projections`, `Hexalith.Parties.Security`, `Hexalith.Parties.Server`, `Hexalith.Parties.ServiceDefaults`, and `Hexalith.Parties.Testing`.
  - [ ] Confirm the solution includes current test projects under `tests/`, including Contracts, Client, Server, Projections, service integration, IntegrationTests, AdminPortal, Picker, MCP, Security, Sample, and DeployValidation tests.
  - [ ] Confirm `samples/Hexalith.Parties.Sample` remains the sample boundary and is included in the solution.
  - [ ] Do not remove later Epic 10-12 projects just because the original March scaffold listed fewer projects; later completed work legitimately expanded the structure.

- [ ] Task 3: Enforce dependency-direction guardrails (AC: 3)
  - [ ] Confirm Contracts remains a shared contract boundary and does not reference hosting, Dapr service hosting, MediatR, FluentValidation, UI, or infrastructure packages beyond approved EventStore contract dependency.
  - [ ] Confirm Client depends only on accepted contract/client abstractions and does not reference Server, Projections, `src/Hexalith.Parties`, AdminPortal, Picker, or MCP projects.
  - [ ] Confirm Server owns aggregate/domain behavior and does not depend on UI, MCP, AdminPortal, Picker, or sample projects.
  - [ ] Confirm projection handlers keep Dapr awareness in actor/wrapper infrastructure rather than pure handler logic.
  - [ ] Confirm the main `src/Hexalith.Parties` project remains the actor-host/service boundary and does not become a generic public Web API baseline.
  - [ ] Confirm `src/Hexalith.Parties.Mcp` is a thin host using client/contract abstractions and does not embed domain event handling or actor-host internals.

- [ ] Task 4: Validate build and starter fitness (AC: 2, 4)
  - [ ] Run `dotnet restore Hexalith.Parties.slnx`.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [ ] If build fails because a sibling root-level submodule is missing, initialize/update only root-level submodules; do not use recursive nested submodule initialization.
  - [ ] If build fails because of unrelated in-progress development, capture the exact blocker and avoid broad repairs outside starter structure.
  - [ ] Run or add narrow architectural fitness coverage only if the current tests do not already guard dependency direction and package-version placement.

- [ ] Task 5: Record reusable starter lessons (AC: 5)
  - [ ] Update this story's Dev Agent Record with any confirmed EventStore-to-Parties deviations that future domain services should know.
  - [ ] Record any durable lesson in `_bmad-output/process-notes/story-creation-lessons.md` only if it affects recurring story creation/review automation, not ordinary implementation notes.
  - [ ] Do not edit `_bmad-output/project-context.md` unless a new durable project-wide rule is discovered and validated.

## Dev Notes

### Current Implementation Context

- This story is a new backlog-tracked artifact for the current Epic 1 plan, but the repository already contains a historical completed scaffold story: `_bmad-output/implementation-artifacts/1-1-project-scaffolding-and-solution-structure.md`.
- Treat the historical story as implementation intelligence, not as the canonical status artifact for this new story key. It records that the original scaffold was created and reviewed in March 2026.
- The current repository has advanced beyond the original scaffold. Later completed work added AdminPortal, Picker, MCP, Security, DeployValidation, Sample tests, EventStore/Tenants/FrontComposer integration, and EventStore-fronted boundary changes. Do not delete those projects to match older counts.
- This story's implementation should be an audit/reconciliation pass over the current scaffold. Create or modify files only when the current state fails the acceptance criteria.

### Architecture Patterns and Constraints

- Parties validates the Hexalith.EventStore domain-service starter pattern. If EventStore abstractions do not fit, prefer fixing/adapting EventStore rather than adding Parties-side workaround structure.
- Use `Hexalith.Parties.slnx` as the solution entry point. Do not introduce a legacy `.sln`.
- Keep source under `src/`, tests under `tests/`, and sample code under `samples/`.
- Root-level build settings are inherited by projects: `net10.0`, nullable enabled, implicit usings enabled, `TreatWarningsAsErrors=true`, MinVer `7.0.0`, central package management, UTF-8, CRLF, Allman braces, and file-scoped namespaces.
- Keep package versions centralized in `Directory.Packages.props`.
- Do not add broad refactors, rename projects, or move already completed feature work unless directly required by a failing acceptance criterion.

### Current Version Baseline

Use the checked-in repository versions as the source of truth for this story:

- .NET SDK: `10.0.103` via `global.json`, with `rollForward: latestPatch`.
- Target framework: `net10.0` via `Directory.Build.props`.
- Dapr packages: `1.17.9`.
- Aspire hosting packages: `13.2.2` / `13.3.2` as currently pinned by package.
- MediatR: `14.1.0`.
- FluentValidation: `12.1.1`.
- Microsoft.Extensions / ASP.NET Core packages: `10.x` as currently pinned.
- OpenTelemetry: `1.15.x` as currently pinned.
- MCP: `ModelContextProtocol` `1.0.0`, `ModelContextProtocol.AspNetCore` `1.3.0`.
- Testing: xUnit v3 `3.2.2`, runner `3.1.5`, Shouldly `4.3.0`, NSubstitute `5.3.0`, bUnit `2.7.2`, Testcontainers `4.10.0`, coverlet `10.0.0`.

Do not upgrade packages as part of this story unless a build break proves the checked-in baseline is internally inconsistent.

### Project Structure Notes

Current source boundaries to preserve:

```text
src/
  Hexalith.Parties
  Hexalith.Parties.AdminPortal
  Hexalith.Parties.AppHost
  Hexalith.Parties.Client
  Hexalith.Parties.Contracts
  Hexalith.Parties.Mcp
  Hexalith.Parties.Picker
  Hexalith.Parties.Projections
  Hexalith.Parties.Security
  Hexalith.Parties.Server
  Hexalith.Parties.ServiceDefaults
  Hexalith.Parties.Testing

tests/
  Hexalith.Parties.AdminPortal.Tests
  Hexalith.Parties.Client.Tests
  Hexalith.Parties.Contracts.Tests
  Hexalith.Parties.DeployValidation.Tests
  Hexalith.Parties.IntegrationTests
  Hexalith.Parties.Mcp.Tests
  Hexalith.Parties.Picker.Tests
  Hexalith.Parties.Projections.Tests
  Hexalith.Parties.Sample.Tests
  Hexalith.Parties.Security.Tests
  Hexalith.Parties.Server.Tests
  Hexalith.Parties.Tests

samples/
  Hexalith.Parties.Sample
```

### Dependency Guardrails

- `Hexalith.Parties.Contracts` is the common contract surface. Keep it small and additive.
- `Hexalith.Parties.Client` must not grow server, projection, service-host, MCP, picker, or admin dependencies.
- `Hexalith.Parties.Server` owns aggregate/domain behavior and should not reference UI, MCP host, sample, or service-host details.
- `Hexalith.Parties.Projections` owns read-side projection behavior. Keep pure projection logic testable without infrastructure coupling.
- `src/Hexalith.Parties` is the service/actor-host boundary. Current project context says this host must not reintroduce public REST, Swagger/OpenAPI, or in-process MCP hosting into the actor host as a generic baseline.
- `src/Hexalith.Parties.Mcp` is the separate thin MCP host boundary. Preserve that separation.
- `Hexalith.Parties.AdminPortal` and `Hexalith.Parties.Picker` are FrontComposer/Blazor consumer surfaces added by later work; they are valid current boundaries, not scaffold drift.

### Submodule and Build Guidance

- Never run `git submodule update --init --recursive` for this story.
- If submodules are needed for restore/build, initialize/update only root-level submodules.
- Treat sibling directories `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Memories`, `Hexalith.FrontComposer`, and `Hexalith.AI.Tools` as root-level submodules/dependencies.
- Do not edit sibling submodules unless a validated starter-structure issue explicitly requires a cross-repo change.

### Testing Requirements

- Minimum validation for this story is `dotnet restore Hexalith.Parties.slnx` and `dotnet build Hexalith.Parties.slnx --configuration Release`.
- If structural or dependency-direction changes are needed, run the narrowest related fitness tests, especially tests under `tests/Hexalith.Parties.Tests/FitnessTests/` and package-boundary tests under contract/client test projects.
- Do not require Docker, full Dapr initialization, or full Aspire topology unless the change touches AppHost/topology behavior.
- Use xUnit v3 and Shouldly patterns already present in the repository.

### Anti-Patterns To Avoid

- Do not scaffold with `dotnet new webapi` and then reshape it.
- Do not add package versions to individual `.csproj` files.
- Do not remove later completed projects to match older March 2026 source counts.
- Do not replace the current solution with a reconstructed solution from memory.
- Do not add public REST, Swagger/OpenAPI, or in-process MCP hosting back into `src/Hexalith.Parties` as part of starter cleanup.
- Do not relax Dapr access-control files or use wildcard app ids/paths.
- Do not treat Dapr sidecar-internal routes as public API surface.
- Do not recurse into nested submodules.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.1] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/architecture.md#Selected-Starter-EventStore-Solution-Structure-Pattern] - Starter rationale and required conventions.
- [Source: _bmad-output/planning-artifacts/architecture.md#Project-Structure-Boundaries] - Approved structure, boundaries, and dependency direction.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Current project-specific implementation guardrails.
- [Source: _bmad-output/implementation-artifacts/1-1-project-scaffolding-and-solution-structure.md] - Historical scaffold implementation and review intelligence.
- [Source: global.json] - Current SDK pin.
- [Source: Directory.Build.props] - Current shared build properties.
- [Source: Directory.Packages.props] - Current centralized dependency versions.
- [Source: Hexalith.Parties.slnx] - Current solution membership.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-15: Story created by BMAD pre-dev hardening automation with current scaffold reconciliation context.
