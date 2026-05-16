# Story 1.1: Set Up Initial Project from EventStore Solution Structure

Status: review

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
   - And the canonical project inventory is the source, test, and sample list under **Project Structure Notes**,
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
   - And the dependency-direction checks follow the explicit matrix under **Dependency Guardrails**,
   - And forbidden dependency directions are not introduced.

4. **Starter validation runs without nested submodule recursion**
   - Given the starter setup is validated,
   - When `dotnet restore Hexalith.Parties.slnx` and `dotnet build Hexalith.Parties.slnx --configuration Release` run,
   - Then all included source, test, and sample projects restore and build with warnings-as-errors intact,
   - And validation does not require recursive nested submodule initialization.

5. **Reusable starter lessons are visible**
   - Given starter setup documentation is reviewed,
   - When a future domain service uses Parties as a reference,
   - Then intentional deviations from EventStore structure are documented in this story's Dev Agent Record,
   - And only durable recurring-automation lessons are added to `_bmad-output/process-notes/story-creation-lessons.md`.

## Tasks / Subtasks

- [x] Task 1: Reconcile the current scaffold with EventStore starter requirements (AC: 1, 2, 5)
  - [x] Confirm `Hexalith.Parties.slnx`, `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `README.md`, and `LICENSE` exist at the repository root.
  - [x] Compare root build/style files against the EventStore pattern and the current project-context rules; document intentional Parties deviations instead of overwriting files blindly.
  - [x] Confirm `global.json` pins SDK `10.0.103` with `rollForward: latestPatch`.
  - [x] Confirm package versions live in `Directory.Packages.props`; do not add `Version=` attributes to project-local package references.

- [x] Task 2: Validate source, test, and sample boundaries (AC: 1, 3)
  - [x] Confirm the solution includes current Parties source projects under `src/`: `Hexalith.Parties`, `Hexalith.Parties.AdminPortal`, `Hexalith.Parties.AppHost`, `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, `Hexalith.Parties.Mcp`, `Hexalith.Parties.Picker`, `Hexalith.Parties.Projections`, `Hexalith.Parties.Security`, `Hexalith.Parties.Server`, `Hexalith.Parties.ServiceDefaults`, and `Hexalith.Parties.Testing`.
  - [x] Confirm the solution includes current test projects under `tests/`: `Hexalith.Parties.AdminPortal.Tests`, `Hexalith.Parties.Client.Tests`, `Hexalith.Parties.Contracts.Tests`, `Hexalith.Parties.DeployValidation.Tests`, `Hexalith.Parties.IntegrationTests`, `Hexalith.Parties.Mcp.Tests`, `Hexalith.Parties.Picker.Tests`, `Hexalith.Parties.Projections.Tests`, `Hexalith.Parties.Sample.Tests`, `Hexalith.Parties.Security.Tests`, `Hexalith.Parties.Server.Tests`, and `Hexalith.Parties.Tests`.
  - [x] Confirm `samples/Hexalith.Parties.Sample` remains the sample boundary and is included in the solution.
  - [x] Capture the observed `.slnx` membership evidence in the Dev Agent Record as three lists: source projects, test projects, and sample projects.
  - [x] If the observed solution membership differs from this story's inventory, classify it as either a legitimate later-story boundary, a missing expected project, or a stale story inventory entry before changing files.
  - [x] Do not remove later Epic 10-12 projects just because the original March scaffold listed fewer projects; later completed work legitimately expanded the structure.

- [x] Task 3: Enforce dependency-direction guardrails (AC: 3)
  - [x] Confirm Contracts remains a shared contract boundary and does not reference hosting, Dapr service hosting, MediatR, FluentValidation, UI, or infrastructure packages beyond approved EventStore contract dependency.
  - [x] Confirm Client depends only on accepted contract/client abstractions and does not reference Server, Projections, `src/Hexalith.Parties`, AdminPortal, Picker, or MCP projects.
  - [x] Confirm Server owns aggregate/domain behavior and does not depend on UI, MCP, AdminPortal, Picker, or sample projects.
  - [x] Confirm projection handlers keep Dapr awareness in actor/wrapper infrastructure rather than pure handler logic.
  - [x] Confirm the main `src/Hexalith.Parties` project remains the actor-host/service boundary and does not become a generic public Web API baseline.
  - [x] Confirm `src/Hexalith.Parties.Mcp` is a thin host using client/contract abstractions and does not embed domain event handling or actor-host internals.
  - [x] Record the observed project-reference and package-reference graph in the Dev Agent Record, including any package that looks infrastructure-facing but is already accepted by project context.
  - [x] Call out any existing exception explicitly instead of normalizing it silently; defer any exception that would change architecture policy, package ownership, or public surface boundaries.

- [x] Task 4: Validate build and starter fitness (AC: 2, 4)
  - [x] Run `dotnet restore Hexalith.Parties.slnx`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [x] Confirm the build covers the solution's included source, test, and sample projects; do not treat a partial project build as satisfying AC4.
  - [x] Check for project-local `Version=` package references and root TFM/package metadata drift.
  - [x] If build fails because a sibling root-level submodule is missing, initialize/update only root-level submodules; do not use recursive nested submodule initialization.
  - [x] If build fails because of unrelated in-progress development, capture the exact blocker and avoid broad repairs outside starter structure.
  - [x] Run existing architectural fitness coverage for solution membership, forbidden references, and package-version placement when present; add narrow coverage only when the current tests do not already guard a touched structural rule.
  - [x] Prefer focused guardrail commands before broader suites when structural files change:
    - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~FitnessTests`
    - `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~FitnessTests`
    - `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj --configuration Release --filter FullyQualifiedName~FitnessTests`

- [x] Task 5: Record reusable starter lessons (AC: 5)
  - [x] Update this story's Dev Agent Record with any confirmed EventStore-to-Parties deviations that future domain services should know.
  - [x] Record any durable lesson in `_bmad-output/process-notes/story-creation-lessons.md` only if it affects recurring story creation/review automation, not ordinary implementation notes.
  - [x] Do not edit `_bmad-output/project-context.md` unless a new durable project-wide rule is discovered and validated.

## Dev Notes

### Current Implementation Context

- This story is a new backlog-tracked artifact for the current Epic 1 plan, but the repository already contains a historical completed scaffold story: `_bmad-output/implementation-artifacts/1-1-project-scaffolding-and-solution-structure.md`.
- Treat the historical story as implementation intelligence, not as the canonical status artifact for this new story key. It records that the original scaffold was created and reviewed in March 2026.
- The current repository has advanced beyond the original scaffold. Later completed work added AdminPortal, Picker, MCP, Security, DeployValidation, Sample tests, EventStore/Tenants/FrontComposer integration, and EventStore-fronted boundary changes. Do not delete those projects to match older counts.
- This story's implementation should be an audit/reconciliation pass over the current scaffold. Create or modify files only when the current state fails the acceptance criteria.
- Preserving later AdminPortal, Picker, MCP, Security, DeployValidation, Sample, EventStore/Tenants/FrontComposer, and EventStore-fronted boundary work is a constraint, not a request to implement new feature behavior in those areas.

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
If `Directory.Packages.props` differs from `_bmad-output/project-context.md`, prefer the checked-in package file for validation evidence and record the context drift as a documentation follow-up rather than changing package versions in this scaffold audit story.

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

Dependency-direction matrix for this story:

| Project or surface | Allowed dependency direction | Forbidden checks for this story |
| --- | --- | --- |
| `Hexalith.Parties.Contracts` | Contract packages and approved EventStore contract abstractions only. | No hosting, Dapr service-hosting, MediatR, FluentValidation, UI, infrastructure, Server, Projections, AdminPortal, Picker, MCP, or sample references. |
| `Hexalith.Parties.Client` | Contracts and accepted client abstractions only. | No Server, Projections, actor host, AdminPortal, Picker, MCP, sample, or domain-internal references. |
| `Hexalith.Parties.Server` | Contracts, EventStore aggregate/domain abstractions, validation patterns already owned by the server boundary. | No UI, MCP host, AdminPortal, Picker, sample, AppHost, or service-host implementation references. |
| `Hexalith.Parties.Projections` | Contracts plus projection/read-model abstractions needed for tenant-safe read models. | No coupling pure projection handlers to actor-host startup, UI, MCP host, sample, or AppHost topology. |
| `src/Hexalith.Parties` actor host | Service/actor hosting and Dapr sidecar integration. | No public REST controllers, public minimal APIs, Swagger/OpenAPI, or in-process MCP tools. |
| `Hexalith.Parties.Mcp` | Thin MCP host using client/contract abstractions. | No aggregate/domain event handling, actor-host internals, projection implementation ownership, or UI coupling. |
| AdminPortal and Picker | FrontComposer/Blazor consumer surfaces. | Do not move UI code into actor host, Server, Contracts, or MCP as part of scaffold cleanup. |
| Samples and tests | Consume public or intended integration surfaces for validation. | Do not create production dependencies from source projects back into samples or test helpers. |

### Submodule and Build Guidance

- Never run `git submodule update --init --recursive` for this story.
- If submodules are needed for restore/build, initialize/update only root-level submodules.
- Treat sibling directories `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Memories`, `Hexalith.FrontComposer`, and `Hexalith.AI.Tools` as root-level submodules/dependencies.
- Do not edit sibling submodules unless a validated starter-structure issue explicitly requires a cross-repo change.

### Testing Requirements

- Minimum validation for this story is `dotnet restore Hexalith.Parties.slnx` and `dotnet build Hexalith.Parties.slnx --configuration Release`.
- The `.slnx` file is the intentional entry point. Do not add a legacy `.sln` as a tooling workaround; capture unsupported local tooling as a blocker or prerequisite instead.
- If structural or dependency-direction changes are needed, run the narrowest related fitness tests, especially tests under `tests/Hexalith.Parties.Tests/FitnessTests/` and package-boundary tests under contract/client test projects.
- Use the exact focused test filters from Task 4 when the affected guardrail exists; do not add duplicate guardrail tests until the existing fitness coverage is inspected and found missing.
- Do not require Docker, full Dapr initialization, or full Aspire topology unless the change touches AppHost/topology behavior.
- Use xUnit v3 and Shouldly patterns already present in the repository.

### Anti-Patterns To Avoid

- Do not scaffold with `dotnet new webapi` and then reshape it.
- Do not add package versions to individual `.csproj` files.
- Do not remove later completed projects to match older March 2026 source counts.
- Do not replace the current solution with a reconstructed solution from memory.
- Do not treat an observed mismatch between the historical March scaffold and the current `.slnx` as a delete/rename instruction; audit the current repository first and preserve legitimate later-story boundaries.
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

OpenAI GPT-5 Codex

### Debug Log References

- 2026-05-15: Root scaffold audit confirmed required root files exist: `Hexalith.Parties.slnx`, `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `README.md`, and `LICENSE`.
- 2026-05-15: Confirmed `global.json` pins SDK `10.0.103` with `rollForward: latestPatch`; `Directory.Build.props` provides `net10.0`, nullable, implicit usings, warnings-as-errors, MinVer, and Parties package metadata.
- 2026-05-15: Confirmed no project-local package `Version=` attributes under root/src/tests/samples; central versions remain in `Directory.Packages.props`.
- 2026-05-15: Release restore/build passed for `Hexalith.Parties.slnx`. Restore/build emitted a skip notice for a missing nested `Hexalith.EventStore\Hexalith.Tenants/...` path, but root-level Tenants is present and no recursive submodule update was required.
- 2026-05-15: Fixed stale Client fitness expectation by making the EventStore contract transitive dependency set explicit while keeping direct Dapr/MediatR/Server/Projections references forbidden.
- 2026-05-15: Fixed EventStore command request validation boundary: `SubmitCommandRequestValidator` now targets the contract request type actually bound by the gateway controller.
- 2026-05-15: Fixed gateway routing test host setup to apply overrides with `ConfigureTestServices` and remove irrelevant EventStore hosted services, reducing gateway suite time from timeout-prone to 16 seconds.

### Implementation Plan

- Audit the current scaffold first and avoid structural rewrites unless evidence shows an AC failure.
- Use existing architecture fitness tests for the guarded boundaries; add or adjust only narrow guardrail coverage when a current guardrail is stale.
- Preserve later AdminPortal, Picker, MCP, Security, DeployValidation, Sample, EventStore/Tenants/FrontComposer, and EventStore-fronted work as legitimate current structure.
- Avoid nested submodule initialization; use existing root-level submodules only.

### Completion Notes List

- Current `.slnx` membership matches the story inventory.
- Source projects observed: `Hexalith.Parties`, `Hexalith.Parties.AdminPortal`, `Hexalith.Parties.AppHost`, `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, `Hexalith.Parties.Mcp`, `Hexalith.Parties.Picker`, `Hexalith.Parties.Projections`, `Hexalith.Parties.Security`, `Hexalith.Parties.Server`, `Hexalith.Parties.ServiceDefaults`, `Hexalith.Parties.Testing`.
- Test projects observed: `Hexalith.Parties.AdminPortal.Tests`, `Hexalith.Parties.Client.Tests`, `Hexalith.Parties.Contracts.Tests`, `Hexalith.Parties.DeployValidation.Tests`, `Hexalith.Parties.IntegrationTests`, `Hexalith.Parties.Mcp.Tests`, `Hexalith.Parties.Picker.Tests`, `Hexalith.Parties.Projections.Tests`, `Hexalith.Parties.Sample.Tests`, `Hexalith.Parties.Security.Tests`, `Hexalith.Parties.Server.Tests`, `Hexalith.Parties.Tests`.
- Sample projects observed: `Hexalith.Parties.Sample`.
- Root build/style files align with the current project-context rules. Intentional deviations from EventStore include Parties-specific package metadata, checked-in package baselines, and a small `Directory.Build.targets` warning exception for FrontComposer submodule projects.
- Dependency guardrail evidence: Contracts references only EventStore.Contracts; Client references EventStore.Contracts, Parties.Contracts, Microsoft.Extensions.Http, and Microsoft.Extensions.Options; Server references Contracts plus EventStore.Client and server-owned Dapr/MediatR packages; Projections keeps pure handlers Dapr-free while actor/wrapper infrastructure owns Dapr awareness; the actor host maps only documented Dapr/internal `/process` and health endpoints; MCP remains a separate thin host using Client/Contracts/ServiceDefaults.
- Existing accepted exception recorded: EventStore.Contracts currently brings Dapr/Grpc/Hexalith.Commons.UniqueIds identity infrastructure transitively into the Client assets graph. Client direct references remain clean, and the fitness test now pins that transitive set explicitly.
- No durable recurring story-creation automation lesson was added to `_bmad-output/process-notes/story-creation-lessons.md`; findings were implementation and guardrail-specific.
- Validation passed: focused fitness suites, restore, Release build, and full solution tests.

### File List

- `_bmad-output/implementation-artifacts/1-1-set-up-initial-project-from-eventstore-solution-structure.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore/Validation/SubmitCommandRequestValidator.cs`

## Party-Mode Review

- Date/time: 2026-05-15T17:05:40+02:00
- Selected story key: `1-1-set-up-initial-project-from-eventstore-solution-structure`
- Command/skill invocation used: `/bmad-party-mode 1-1-set-up-initial-project-from-eventstore-solution-structure; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - The ready story needed a canonical solution/project membership source of truth before development.
  - Dependency guardrails needed a concrete allowed/forbidden matrix so review does not rely on interpretation.
  - Restore/build acceptance needed exact commands, full `.slnx` build expectations, and the root-level-only submodule constraint.
  - Preservation of later AdminPortal, Picker, MCP, Security, DeployValidation, Sample, EventStore/Tenants/FrontComposer, and EventStore-fronted work needed to be framed as a non-goal/constraint, not implementation scope.
  - Starter lesson recording needed a concrete destination and distinction between ordinary implementation notes and durable recurring-automation lessons.
- Changes applied:
  - Added AC traceability to the canonical project inventory, dependency matrix, exact restore/build commands, full included-project build expectation, Dev Agent Record destination, and lessons ledger boundary.
  - Expanded Task 2 with exact test project names and Task 4 with full-solution build, package-version, TFM, and structural fitness checks.
  - Added a non-goal/preservation constraint for later surfaces and integrations.
  - Added an explicit dependency-direction matrix for Contracts, Client, Server, Projections, actor host, MCP, UI surfaces, samples, and tests.
  - Clarified `.slnx` tooling behavior: do not add a legacy `.sln`; capture unsupported local tooling as a blocker or prerequisite.
- Findings deferred:
  - Automated architectural guardrail expansion remains conditional on whether existing fitness tests already cover the touched structure.
  - Any broader architecture or product-scope change remains out of scope for this scaffold audit story.
- Final recommendation: ready-for-dev

## Change Log

- 2026-05-15: Implemented scaffold audit, updated dependency guardrail tests, fixed EventStore command request validation type, optimized gateway routing test host, and moved story to review.
- 2026-05-15: Advanced elicitation applied pre-dev clarifications for solution membership evidence, dependency/package graph evidence, focused fitness validation, package-context drift handling, and historical-scaffold mismatch handling.
- 2026-05-15: Party-mode review applied pre-dev clarifications for project inventory, dependency guardrails, validation commands, scope constraints, and starter lesson recording.
- 2026-05-15: Story created by BMAD pre-dev hardening automation with current scaffold reconciliation context.

## Advanced Elicitation

- Date/time: 2026-05-15T19:37:45+02:00
- Selected story key: `1-1-set-up-initial-project-from-eventstore-solution-structure`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-1-set-up-initial-project-from-eventstore-solution-structure`
- Batch 1 methods: Red Team vs Blue Team, Failure Mode Analysis, Security Audit Personas, Self-Consistency Validation, Architecture Decision Records
- Batch 2 methods: Pre-mortem Analysis, Chaos Monkey Scenarios, User Persona Focus Group, Critique and Refine, Expand or Contract for Audience
- Findings summary:
  - The story was already ready for development after party-mode review, but implementation evidence could be made more explicit.
  - Solution membership and dependency/package graph checks needed a concrete Dev Agent Record evidence shape so development does not overcorrect historical scaffold drift.
  - Focused validation commands needed to be named for the existing architectural fitness surfaces before any broader test run or duplicate test creation.
  - The checked-in package baseline should remain authoritative for this audit story when it differs from generated project context.
- Changes applied:
  - Added `.slnx` evidence capture requirements for source, test, and sample membership.
  - Added classification guidance for observed solution membership differences before changing files.
  - Expanded dependency guardrail evidence to include package references and deferred architecture-policy exceptions.
  - Added focused fitness test commands for Parties, Client, and MCP guardrails.
  - Clarified package-context drift handling and historical March scaffold mismatch handling.
- Findings deferred:
  - Any package upgrade, architecture-policy change, public surface change, or project rename/delete decision remains out of scope unless a failing acceptance criterion proves it is required.
  - Additional fitness tests should be added only after existing guardrail coverage is inspected and found missing.
- Final recommendation: ready-for-dev
