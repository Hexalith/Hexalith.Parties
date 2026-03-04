# Story 1.1: Project Scaffolding & Solution Structure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a properly structured .NET solution following EventStore conventions,
so that I have a consistent, buildable foundation for the party management service.

## Acceptance Criteria

1. **Solution file exists:** `Hexalith.Parties.slnx` (modern XML solution format) at repository root
2. **Build configuration files exist at repository root:**
    - `Directory.Build.props` (net10.0 default, nullable, TreatWarningsAsErrors, NuGet metadata, MinVer 7.0.0)
    - `Directory.Packages.props` (central package management with all dependency versions matching architecture spec)
    - `global.json` (SDK 10.0.103, rollForward: latestPatch)
    - `.editorconfig` (Allman, 4-space, CRLF, UTF-8 — copied from EventStore)
    - `.gitignore` (merged with existing — see Dev Notes)
    - `LICENSE` (MIT — already exists)
3. **All 9 source projects exist under `src/` with correct .csproj configuration:**
    - Hexalith.Parties.Contracts
    - Hexalith.Parties.Client
    - Hexalith.Parties.Server
    - Hexalith.Parties.Projections
    - Hexalith.Parties.CommandApi
    - Hexalith.Parties.Aspire
    - Hexalith.Parties.AppHost (Aspire.AppHost.Sdk)
    - Hexalith.Parties.ServiceDefaults
    - Hexalith.Parties.Testing
4. **All 6 test projects exist under `tests/` with test framework PackageReferences:**
    - Hexalith.Parties.Contracts.Tests
    - Hexalith.Parties.Client.Tests
    - Hexalith.Parties.Server.Tests
    - Hexalith.Parties.Projections.Tests
    - Hexalith.Parties.CommandApi.Tests
    - Hexalith.Parties.IntegrationTests
5. **Sample project exists under `samples/`:**
    - Hexalith.Parties.Sample
6. **Project references follow strict dependency direction:**
    - Contracts <- Client, Server, Projections
    - Contracts + Server + Projections <- CommandApi
    - All src/ <- Testing
7. **`dotnet restore Hexalith.Parties.slnx` completes without errors**
8. **`dotnet build Hexalith.Parties.slnx` compiles successfully (empty projects, no source files yet)**

## Tasks / Subtasks

- [x] Task 1: Create root-level build configuration files (AC: #2)
    - [x] 1.1: Copy `.editorconfig` from `Hexalith.EventStore/.editorconfig` — do NOT modify
    - [x] 1.2: Merge `.gitignore` — diff existing root `.gitignore` against `Hexalith.EventStore/.gitignore`, keep all unique entries from both, do NOT blindly overwrite
    - [x] 1.3: Create `global.json` — see exact content in Dev Notes
    - [x] 1.4: Create `Directory.Build.props` — see exact content in Dev Notes
    - [x] 1.5: Create `Directory.Packages.props` — see exact content in Dev Notes
    - [x] 1.6: Verify `LICENSE` file exists at root (already present — MIT)
- [x] Task 2: Create solution file (AC: #1)
    - [x] 2.1: Create `Hexalith.Parties.slnx` — see exact content in Dev Notes
- [x] Task 3: Create all 9 source project stubs (AC: #3, #6)
    - [x] 3.1: Create `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj` — empty stub, inherits net10.0 from Directory.Build.props
    - [x] 3.2: Create `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj` — ProjectReference to Contracts
    - [x] 3.3: Create `src/Hexalith.Parties.Server/Hexalith.Parties.Server.csproj` — ProjectReference to Contracts, PackageReferences to Dapr and MediatR
    - [x] 3.4: Create `src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj` — ProjectReference to Contracts, PackageReferences to Dapr
    - [x] 3.5: Create `src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj` — ProjectReferences to Contracts, Server, Projections; PackageReferences for API; IsPackable=false
    - [x] 3.6: Create `src/Hexalith.Parties.Aspire/Hexalith.Parties.Aspire.csproj` — PackageReference to Aspire.Hosting
    - [x] 3.7: Create `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` — MUST use `Aspire.AppHost.Sdk/13.1.2` SDK (see Dev Notes)
    - [x] 3.8: Create `src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj` — PackageReferences for OpenTelemetry, Resilience; IsPackable=false
    - [x] 3.9: Create `src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj` — ProjectReference to Contracts
- [x] Task 4: Create all 6 test project stubs (AC: #4)
    - [x] 4.1: Create `tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj` — see test .csproj template in Dev Notes
    - [x] 4.2: Create `tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj`
    - [x] 4.3: Create `tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj`
    - [x] 4.4: Create `tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj`
    - [x] 4.5: Create `tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj`
    - [x] 4.6: Create `tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj`
- [x] Task 5: Create sample project stub (AC: #5)
    - [x] 5.1: Create `samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj`
- [x] Task 6: Verify build (AC: #7, #8)
    - [x] 6.1: Run `dotnet restore Hexalith.Parties.slnx` and verify zero errors
    - [x] 6.2: Run `dotnet build Hexalith.Parties.slnx` and verify successful compilation

## Dev Notes

### Architecture Patterns and Constraints

**CRITICAL: Mirror EventStore Convention Exactly**
The Hexalith.Parties solution mirrors the Hexalith.EventStore solution structure. The EventStore submodule at `Hexalith.EventStore/` (commit `6b9ddd8` on main) is the reference implementation. Copy and adapt its configuration files — do NOT write from memory.

**Key Technical Decisions:**

- .NET 10.0 target framework for ALL projects (including Contracts — architecture specified netstandard2.1 but overridden to net10.0 per project decision to avoid polyfill complexity)
- Modern XML solution format (.slnx) — NOT legacy .sln
- Central package management via Directory.Packages.props — NO version numbers in individual .csproj files
- MinVer 7.0.0 for git tag-based SemVer versioning (prefix: `v`)
- TreatWarningsAsErrors = true in all projects
- Nullable reference types enabled globally
- Allman brace style, 4-space indentation, CRLF, UTF-8

### Strict Dependency Direction (Machine-Verifiable)

```
Contracts <- Client         (consumer package)
Contracts <- Server         (aggregate logic)
Contracts <- Projections    (read side)
Contracts + Server + Projections <- CommandApi  (API surface)
CommandApi <- AppHost       (hosting)
All src/ <- Testing         (test utilities)
```

**Forbidden Dependencies (enforced via CI fitness tests in later stories):**

- Client must NOT reference Server, Projections, or CommandApi
- Projections handlers must NOT reference DAPR (only actors reference DAPR)
- Contracts should have minimal dependencies (shared types package consumed by all other projects)

### Contracts Project — net10.0 (Architecture Override)

The architecture document specifies netstandard2.1 for the Contracts project (FR33). This has been **overridden to net10.0** by project decision because netstandard2.1 requires:

- `<LangVersion>latest</LangVersion>` override (defaults to C# 8.0, cannot compile records/init/required)
- PolySharp polyfill for `IsExternalInit` and `RequiredMemberAttribute`
- `<ImplicitUsings>disable</ImplicitUsings>`

Targeting net10.0 eliminates all this complexity. The Contracts .csproj is a standard empty stub inheriting everything from Directory.Build.props — identical to EventStore's Contracts project.

### AppHost Project — Aspire SDK (CRITICAL)

The AppHost project MUST use the Aspire AppHost SDK, NOT `Microsoft.NET.Sdk`. This is required for Aspire orchestration to work:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.1.2">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>true</IsPublishable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Hexalith.Parties.CommandApi\Hexalith.Parties.CommandApi.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Redis" />
    <PackageReference Include="CommunityToolkit.Aspire.Hosting.Dapr" />
  </ItemGroup>
</Project>
```

### EventStore Package Consumption Strategy

Parties.Server, Parties.Projections, and other projects that depend on EventStore framework types will consume them via **NuGet packages** (not project references to the submodule). The submodule is present as a reference implementation for copying conventions.

For Story 1.1 (empty stubs), no EventStore NuGet PackageReferences are needed yet. They will be added in Story 1.2+ when source code requires them. The `Directory.Packages.props` should NOT include EventStore package versions yet — add them when first needed.

### Exact File Contents

#### global.json

```json
{
    "sdk": {
        "version": "10.0.103",
        "rollForward": "latestPatch"
    }
}
```

#### Directory.Build.props

Copy from `Hexalith.EventStore/Directory.Build.props` and change these metadata values:

```xml
<PackageProjectUrl>https://github.com/Hexalith/Hexalith.Parties</PackageProjectUrl>
<RepositoryUrl>https://github.com/Hexalith/Hexalith.Parties</RepositoryUrl>
<Description>DAPR-native party management microservice for .NET</Description>
<PackageTags>parties;crm;dapr;eventsourcing;cqrs;dotnet</PackageTags>
```

All other content (TargetFramework, Nullable, ImplicitUsings, TreatWarningsAsErrors, MinVer, IsPackable, etc.) stays identical to EventStore.

#### Directory.Packages.props

Copy from `Hexalith.EventStore/Directory.Packages.props` and add one new ItemGroup:

```xml
<ItemGroup Label="MCP">
  <PackageVersion Include="ModelContextProtocol" Version="1.0.0" />
</ItemGroup>
```

Keep ALL existing packages from EventStore (Dapr, Aspire, Microsoft.Extensions, Application, Testing) at the same versions. The complete package list is in EventStore's file — do not reconstruct from memory.

#### Hexalith.Parties.slnx

```xml
<Solution>
  <Folder Name="/samples/">
    <Project Path="samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj" />
    <Project Path="src/Hexalith.Parties.Aspire/Hexalith.Parties.Aspire.csproj" />
    <Project Path="src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj" />
    <Project Path="src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj" />
    <Project Path="src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj" />
    <Project Path="src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj" />
    <Project Path="src/Hexalith.Parties.Server/Hexalith.Parties.Server.csproj" />
    <Project Path="src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj" />
    <Project Path="src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj" />
    <Project Path="tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj" />
    <Project Path="tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj" />
    <Project Path="tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj" />
    <Project Path="tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj" />
    <Project Path="tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj" />
  </Folder>
</Solution>
```

### .csproj Templates by Project Type

#### Standard src project (e.g., Client, Aspire, ServiceDefaults, Testing)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Empty stub — inherits net10.0 from Directory.Build.props -->
</Project>
```

Add `<ProjectReference>` and `<PackageReference>` items per the dependency table below.

#### Contracts project (standard empty stub — same as EventStore)

```xml
<Project Sdk="Microsoft.NET.Sdk">

</Project>
```

#### AppHost project (Aspire SDK)

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.1.2">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>true</IsPublishable>
  </PropertyGroup>
</Project>
```

Add ProjectReferences and PackageReferences per dependency table below.

#### Test project template (all 6 test projects follow this pattern)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Hexalith.Parties.{ProjectUnderTest}\Hexalith.Parties.{ProjectUnderTest}.csproj" />
    <ProjectReference Include="..\..\src\Hexalith.Parties.Testing\Hexalith.Parties.Testing.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

Replace `{ProjectUnderTest}` with the matching src project name (e.g., Contracts, Client, Server, Projections, CommandApi). IntegrationTests references CommandApi.

#### Sample project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

### Per-Project Dependency Table

| Project             | SDK                       | ProjectReferences              | PackageReferences                                                                                                                                                                                                                                                                                      |
| ------------------- | ------------------------- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Contracts**       | Microsoft.NET.Sdk         | (none)                         | (none) — empty stub, net10.0 inherited                                                                                                                                                                                                                                                                 |
| **Client**          | Microsoft.NET.Sdk         | Contracts                      | (none for now)                                                                                                                                                                                                                                                                                         |
| **Server**          | Microsoft.NET.Sdk         | Contracts                      | Dapr.Client, Dapr.Actors, MediatR                                                                                                                                                                                                                                                                      |
| **Projections**     | Microsoft.NET.Sdk         | Contracts                      | Dapr.Client, Dapr.Actors                                                                                                                                                                                                                                                                               |
| **CommandApi**      | Microsoft.NET.Sdk         | Contracts, Server, Projections | FluentValidation, FluentValidation.DependencyInjectionExtensions, MediatR, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.AspNetCore.OpenApi. **IsPackable=false**                                                                                                                           |
| **Aspire**          | Microsoft.NET.Sdk         | (none)                         | Aspire.Hosting                                                                                                                                                                                                                                                                                         |
| **AppHost**         | Aspire.AppHost.Sdk/13.1.2 | CommandApi                     | Aspire.Hosting.Redis, CommunityToolkit.Aspire.Hosting.Dapr. **IsPackable=false, IsPublishable=true**                                                                                                                                                                                                   |
| **ServiceDefaults** | Microsoft.NET.Sdk         | (none)                         | Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.ServiceDiscovery, OpenTelemetry.Exporter.OpenTelemetryProtocol, OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http, OpenTelemetry.Instrumentation.Runtime. **IsPackable=false** |
| **Testing**         | Microsoft.NET.Sdk         | Contracts                      | (none for now)                                                                                                                                                                                                                                                                                         |
| **Sample**          | Microsoft.NET.Sdk         | (none)                         | (none for now). **IsPackable=false**                                                                                                                                                                                                                                                                   |

**NuGet-published projects (6):** Contracts, Client, Server, Projections, Testing, Aspire — these inherit `IsPackable=true` from Directory.Build.props.
**Non-published projects:** CommandApi, AppHost, ServiceDefaults, Sample, all test projects — must override to `IsPackable=false`.

### .gitignore Merge Strategy

The root already has a `.gitignore` (standard Visual Studio template). The EventStore submodule also has a `.gitignore`. To merge:

1. Read both files
2. Diff them — keep all unique entries from both
3. If they are identical (likely — both VS templates), keep the existing root file as-is
4. Do NOT blindly overwrite the root file

### What Already Exists (Do NOT Recreate or Overwrite)

- `.gitignore` — merge only, do not overwrite (see merge strategy above)
- `LICENSE` (MIT) — already present, do not touch
- `README.md` (minimal) — already present, do not touch
- `.gitmodules` — EventStore submodule config, do not touch
- `Hexalith.EventStore/` — submodule directory, do not touch
- `_bmad/` and `_bmad-output/` — BMAD tooling, do not touch
- `.claude/`, `.cursor/`, `.gemini/`, `.agent/`, `.agents/`, `.github/` — AI tooling configs, do not touch
- `docs/` — empty directory, do not touch

### Prerequisites

- **.NET SDK 10.0.103 or later patch** must be installed. The `global.json` pins to 10.0.103 with `rollForward: latestPatch`, meaning 10.0.104+ works but 10.0.102 (used by EventStore) does NOT. Run `dotnet --version` to verify before starting.
- **Docker** is not required for Story 1.1 (no containers), but will be needed for Story 1.7+.

### Known Discrepancy

The epics document (Story 1.1 AC) states "all 10 source projects" but enumerates only 9 project names. The architecture document also says "10 src projects" but lists 9. This story uses the correct count of **9 source projects** based on the actual enumerated list. No 10th project exists in any specification.

### ANTI-PATTERNS TO AVOID

1. **Do NOT create .sln file** — use .slnx (modern XML format) only
2. **Do NOT add version numbers in individual .csproj files** — all versions go in Directory.Packages.props
3. **Do NOT invent file structures** — copy from EventStore submodule and adapt
4. **Do NOT add source code files (.cs)** in this story — project stubs only (no Program.cs, no Class1.cs)
5. **Do NOT add .github/workflows/** — CI/CD is out of scope for Story 1.1
6. **Do NOT use `Microsoft.NET.Sdk` for AppHost** — must use `Aspire.AppHost.Sdk/13.1.2`
7. **Do NOT add EventStore NuGet packages to Directory.Packages.props** — defer until Story 1.2+ when code needs them
8. **Do NOT reconstruct Directory.Build.props or Directory.Packages.props from memory** — copy from EventStore and adapt
9. **Do NOT leave IsPackable=true on non-published projects** — CommandApi, ServiceDefaults, AppHost, Sample, and all test projects must override to false

### Project Structure Notes

```
Hexalith.Parties/
├── .editorconfig                    # Copied from EventStore
├── Directory.Build.props            # Based on EventStore, Parties metadata
├── Directory.Packages.props         # Based on EventStore + MCP SDK
├── global.json                      # SDK 10.0.103
├── Hexalith.Parties.slnx           # Modern XML solution
│
├── src/
│   ├── Hexalith.Parties.Contracts/
│   │   └── Hexalith.Parties.Contracts.csproj    # net10.0 (inherited), empty stub
│   ├── Hexalith.Parties.Client/
│   │   └── Hexalith.Parties.Client.csproj       # refs: Contracts
│   ├── Hexalith.Parties.Server/
│   │   └── Hexalith.Parties.Server.csproj       # refs: Contracts + Dapr + MediatR
│   ├── Hexalith.Parties.Projections/
│   │   └── Hexalith.Parties.Projections.csproj  # refs: Contracts + Dapr
│   ├── Hexalith.Parties.CommandApi/
│   │   └── Hexalith.Parties.CommandApi.csproj   # refs: Contracts, Server, Projections + API pkgs
│   ├── Hexalith.Parties.Aspire/
│   │   └── Hexalith.Parties.Aspire.csproj       # refs: Aspire.Hosting
│   ├── Hexalith.Parties.AppHost/
│   │   └── Hexalith.Parties.AppHost.csproj      # Aspire.AppHost.Sdk, refs: CommandApi
│   ├── Hexalith.Parties.ServiceDefaults/
│   │   └── Hexalith.Parties.ServiceDefaults.csproj  # OpenTelemetry + Resilience pkgs
│   └── Hexalith.Parties.Testing/
│       └── Hexalith.Parties.Testing.csproj      # refs: Contracts
│
├── tests/
│   ├── Hexalith.Parties.Contracts.Tests/
│   │   └── Hexalith.Parties.Contracts.Tests.csproj  # refs: Contracts, Testing + xUnit
│   ├── Hexalith.Parties.Client.Tests/
│   │   └── Hexalith.Parties.Client.Tests.csproj     # refs: Client, Testing + xUnit
│   ├── Hexalith.Parties.Server.Tests/
│   │   └── Hexalith.Parties.Server.Tests.csproj     # refs: Server, Testing + xUnit
│   ├── Hexalith.Parties.Projections.Tests/
│   │   └── Hexalith.Parties.Projections.Tests.csproj  # refs: Projections, Testing + xUnit
│   ├── Hexalith.Parties.CommandApi.Tests/
│   │   └── Hexalith.Parties.CommandApi.Tests.csproj   # refs: CommandApi, Testing + xUnit
│   └── Hexalith.Parties.IntegrationTests/
│       └── Hexalith.Parties.IntegrationTests.csproj   # refs: CommandApi, Testing + xUnit
│
└── samples/
    └── Hexalith.Parties.Sample/
        └── Hexalith.Parties.Sample.csproj
```

### References

- [Source: Hexalith.EventStore/Directory.Build.props] — Template for build properties (copy and update metadata)
- [Source: Hexalith.EventStore/Directory.Packages.props] — Template for centralized package versions (copy and add MCP SDK)
- [Source: Hexalith.EventStore/global.json] — Template for SDK pinning (update 10.0.102 -> 10.0.103)
- [Source: Hexalith.EventStore/.editorconfig] — Copy exactly for code style enforcement
- [Source: Hexalith.EventStore/Hexalith.EventStore.slnx] — Template for modern XML solution format
- [Source: Hexalith.EventStore/tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj] — Template for test project .csproj
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj] — Template for src project .csproj with dependencies
- [Source: _bmad-output/planning-artifacts/architecture.md#Solution-Structure] — Complete project list and dependency graph
- [Source: _bmad-output/planning-artifacts/architecture.md#AI-Agent-Enforcement-Guidelines] — 14 coding rules
- [Source: _bmad-output/planning-artifacts/architecture.md#Anti-Patterns] — 8 forbidden patterns
- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.1] — Acceptance criteria and BDD scenarios

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Code review remediation: `global.json` restored to SDK `10.0.103` per Story 1.1 AC and Epic specification.
- Code review remediation: `.gitignore` merged with missing unique entries from `Hexalith.EventStore/.gitignore` (`NUL`, codacy instruction path, test output files, `publish-output/`, `.cache_ggshield`, `.cursor\rules\codacy.mdc`, `.lycheecache`).
- ASPIRE004 warning during build: CommandApi is referenced by AppHost but is not an executable. Expected for empty stubs; will resolve in later stories when CommandApi gets a Program.cs and OutputType=Exe.

### Completion Notes List

- All 6 tasks completed successfully
- All root-level build configuration files created based on EventStore templates with Parties-specific metadata
- .editorconfig copied exactly from EventStore (Allman style, 4-space, CRLF, UTF-8)
- .gitignore merged to include all unique entries from root and EventStore reference file
- global.json aligned with Story 1.1 acceptance criteria (SDK 10.0.103)
- Directory.Build.props adapted from EventStore with updated PackageProjectUrl, RepositoryUrl, Description, PackageTags
- Directory.Packages.props copied from EventStore with added MCP ItemGroup (ModelContextProtocol 1.0.0)
- Solution file (slnx) created with all 16 projects in correct folder structure
- 9 source projects created with correct SDK, ProjectReferences, and PackageReferences per dependency table
- 6 test projects created with xUnit, Shouldly, NSubstitute, coverlet, and correct project references
- 1 sample project created as empty stub
- `dotnet restore` completed with 0 errors
- `dotnet build` completed with 0 errors, 1 expected warning (ASPIRE004)

### File List

- .editorconfig (new) — copied from EventStore
- .gitignore (modified) — merged with EventStore unique entries
- global.json (new) — SDK 10.0.103, latestPatch
- Directory.Build.props (new) — net10.0, nullable, TreatWarningsAsErrors, NuGet metadata, MinVer 7.0.0
- Directory.Packages.props (new) — central package management with all dependency versions + MCP SDK
- Hexalith.Parties.slnx (new) — modern XML solution file with 16 projects
- src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj (new)
- src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj (new)
- src/Hexalith.Parties.Server/Hexalith.Parties.Server.csproj (new)
- src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj (new)
- src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj (new)
- src/Hexalith.Parties.Aspire/Hexalith.Parties.Aspire.csproj (new)
- src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj (new)
- src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj (new)
- src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj (new)
- tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj (new)
- tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj (new)
- tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj (new)
- tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj (new)
- tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj (new)
- tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj (new)
- samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj (new)

### Change Log

- 2026-03-04: Story 1.1 implemented — created complete .NET solution scaffolding with 9 source projects, 6 test projects, 1 sample project, and all root-level build configuration files. Build verified successfully.
- 2026-03-04: Code review follow-up — fixed Story 1.1 compliance issues by restoring `global.json` SDK version to `10.0.103` and completing `.gitignore` merge with all unique EventStore entries.

## Senior Developer Review (AI)

### Review Date

2026-03-04

### Reviewer

Jérôme (AI-assisted adversarial review)

### Summary

- Reviewed Story 1.1 implementation against acceptance criteria, task claims, and actual workspace files.
- Fixed all identified High/Medium issues automatically.

### Findings Resolved

- **[HIGH] AC mismatch**: `global.json` used `10.0.102` instead of required `10.0.103`.

    **Resolution**: Updated `global.json` to `10.0.103`.

- **[HIGH] Task 1.2 marked complete but incomplete merge evidence**: `.gitignore` was missing unique EventStore entries.

    **Resolution**: Added missing unique entries from `Hexalith.EventStore/.gitignore`.

- **[MEDIUM] File list completeness gap**: `.gitignore` change not listed in Dev Agent Record.

    **Resolution**: Updated File List to include `.gitignore`.

### Notes

- Non-application/tooling files and BMAD artifacts were excluded from source-code quality review scope per workflow rules.
