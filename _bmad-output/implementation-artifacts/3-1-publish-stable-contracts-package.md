# Story 3.1: Publish Stable Contracts Package

Status: ready-for-dev

## Story

As a .NET developer,
I want a stable `Hexalith.Parties.Contracts` package,
so that I can reference party commands, events, models, and results without inheriting service infrastructure dependencies.

## Acceptance Criteria

1. Given the Contracts project is built, when package dependencies are inspected, then the package has no runtime dependencies beyond the target framework and approved serialization/contract basics, and it does not reference hosting, Dapr, MediatR, FluentValidation, UI, or service infrastructure packages.
2. Given a developer references the Contracts package, when they use party commands, events, value objects, query models, rejection models, and result types, then all public contract types compile from the package, and XML documentation is available for public APIs.
3. Given event contracts are reviewed, when future compatibility is assessed, then event shapes are append-only and additive, and forward-compatible placeholders such as `PartyMerged` are represented without forcing runtime behavior.
4. Given personal-data markers are present in contracts, when consuming applications inspect contract metadata, then personal-data classification is visible where required, and consumers do not need server infrastructure packages to read that metadata.
5. Given package validation tests run, when they inspect references and public API shape, then forbidden dependencies fail tests, and breaking public contract drift is detected before package publication.

## Acceptance Evidence

| AC | Evidence to provide |
| --- | --- |
| 1 | Packed `.nupkg`/`.nuspec` or assets inspection proves `Hexalith.Parties.Contracts` does not carry Dapr, hosting, MediatR, FluentValidation, UI, Server, Projections, or actor-host dependencies; any remaining dependency such as `Hexalith.EventStore.Contracts` is explicitly justified as approved contract infrastructure or removed. |
| 2 | A minimal consumer compile test or package smoke test imports commands, events, models, results, value objects, rejections, and `[PersonalData]`; XML documentation file is generated and packaged. |
| 3 | Contract/event compatibility tests prove existing public members remain source-compatible and new event/result fields are additive only. `PartyMerged` remains a placeholder contract with no required runtime behavior. |
| 4 | Reflection tests prove required personal-data annotations remain discoverable from the package without referencing server, projection, UI, Dapr, or security infrastructure implementations. |
| 5 | Architectural/package fitness tests fail on forbidden references, missing package metadata/docs, absent XML docs, or unapproved public contract drift. |

## Tasks

- [ ] Audit the current Contracts package baseline. (AC: 1, 2)
  - [ ] Inspect `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj`, inherited package metadata from `Directory.Build.props`, and central versions from `Directory.Packages.props`.
  - [ ] Build and pack the Contracts project, then inspect the generated `.nupkg`, `.nuspec`, `.deps.json`, and compile assets.
  - [ ] Decide whether the current `Hexalith.EventStore.Contracts` project reference is an approved contract-basic dependency or must be removed/hidden from the consumer package; record the rationale in the story completion notes.
- [ ] Harden package metadata and documentation output. (AC: 2, 5)
  - [ ] Ensure `Hexalith.Parties.Contracts` has package-specific metadata where inherited defaults are too generic.
  - [ ] Generate and include XML documentation for public APIs in the packed output.
  - [ ] Keep MinVer/tag-driven versioning and central package management; do not add project-local package versions.
- [ ] Prove the public contract surface is stable and additive. (AC: 2, 3, 5)
  - [ ] Add or tighten tests that enumerate public commands, events, models, results, state, value objects, search contracts, and security contract interfaces.
  - [ ] Add compatibility guardrails that fail when existing public contract members are removed or renamed without an explicit migration decision.
  - [ ] Confirm forward-compatible placeholders such as `PartyMerged` remain contract-only and do not force server/projection/runtime behavior.
- [ ] Prove package dependency boundaries. (AC: 1, 5)
  - [ ] Add a package/reference fitness test that fails if `Hexalith.Parties.Contracts` references Dapr, hosting, MediatR, FluentValidation, UI, Server, Projections, actor host, MCP, AdminPortal, Picker, or concrete infrastructure packages.
  - [ ] Keep test dependencies isolated to test projects; do not make the Contracts test project's own AdminPortal/UI references part of the package-boundary proof.
  - [ ] Verify the packaged dependency graph, not only project source references.
- [ ] Prove consumer usability. (AC: 2, 4)
  - [ ] Add a minimal package consumer smoke test or compile-only test that references the packed Contracts output and uses representative commands, events, value objects, query models, result types, rejection events, and `[PersonalData]`.
  - [ ] Confirm consumers can inspect personal-data metadata without server, projection, Dapr, hosting, UI, or security implementation references.
  - [ ] Ensure no sample/test assertion logs personal data values from contract examples.

## Dev Notes

This story is a packaging and contract-boundary hardening story. It should not add new party behavior, REST endpoints, MCP tools, UI, projections, or EventStore runtime work. The goal is to make the already-developed Contracts surface safe and predictable for external .NET consumers.

The current repo baseline is important:

- `Directory.Build.props` targets `net10.0`, enables nullable, warnings-as-errors, MinVer package versioning, `IsPackable=true` by default, and shared package metadata.
- `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj` currently has a project reference to `Hexalith.EventStore.Contracts`.
- Planning artifacts still contain older wording about `netstandard2.1` and zero runtime dependencies. Current project context says the repo targets `.NET 10`; do not silently retarget the whole package to `netstandard2.1` unless a fresh architecture decision explicitly approves that change.
- Treat `Hexalith.EventStore.Contracts` as the main decision point: if it remains, the story needs explicit evidence that it is an approved contract-basic dependency rather than leaked infrastructure; if it is removed, downstream types such as command results and event payload interfaces must keep compiling through a deliberate replacement or shared abstraction.

Existing contract surfaces include:

- Commands under `src/Hexalith.Parties.Contracts/Commands/`
- Events under `src/Hexalith.Parties.Contracts/Events/`
- Models under `src/Hexalith.Parties.Contracts/Models/`
- Results under `src/Hexalith.Parties.Contracts/Results/`
- Search contracts under `src/Hexalith.Parties.Contracts/Search/`
- Security contract interfaces and records under `src/Hexalith.Parties.Contracts/Security/`
- State and value objects under `src/Hexalith.Parties.Contracts/State/` and `src/Hexalith.Parties.Contracts/ValueObjects/`
- `PersonalDataAttribute` at the Contracts project root.

Existing tests to preserve and extend:

- `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Results/CompositeCommandResultTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs`

Be careful with the current Contracts test project. It references AdminPortal and Testing projects, which is acceptable for those existing UI/privacy tests but is not valid evidence that the Contracts package itself is lean. Add package-boundary proof that inspects the Contracts project/packed output directly.

## Current Code Surfaces

- `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj`
- `src/Hexalith.Parties.Contracts/PersonalDataAttribute.cs`
- `src/Hexalith.Parties.Contracts/Commands/*.cs`
- `src/Hexalith.Parties.Contracts/Events/*.cs`
- `src/Hexalith.Parties.Contracts/Models/*.cs`
- `src/Hexalith.Parties.Contracts/Results/*.cs`
- `src/Hexalith.Parties.Contracts/Search/*.cs`
- `src/Hexalith.Parties.Contracts/Security/*.cs`
- `src/Hexalith.Parties.Contracts/State/*.cs`
- `src/Hexalith.Parties.Contracts/ValueObjects/*.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj`
- `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Results/CompositeCommandResultTests.cs`
- `Directory.Build.props`
- `Directory.Packages.props`

## Suggested Validation

```powershell
dotnet build .\src\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj --configuration Release
dotnet pack .\src\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj --configuration Release --no-build --output .\artifacts\packages
dotnet package list .\src\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj --include-transitive
dotnet test .\tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --configuration Release
dotnet build .\Hexalith.Parties.slnx --configuration Release
```

`dotnet pack` includes package dependencies in the generated `.nuspec`, and `--no-build` is appropriate after the Release build has already succeeded. With .NET 10 SDK, prefer `dotnet package list` over the older `dotnet list package` form.

## Anti-Patterns

- Treating the Contracts package as stable because the source project builds, without inspecting the packed output.
- Using the Contracts test project's broad references as proof that external consumers inherit no infrastructure.
- Moving server, projection, UI, Dapr, MCP, or EventStore runtime behavior into Contracts to satisfy a compile error.
- Breaking existing public contract names, shapes, enum values, or serialized payload expectations without a migration story.
- Removing personal-data markers or hiding them behind infrastructure services consumers cannot reference.
- Retargeting the package or changing dependency policy based only on legacy architecture wording without recording the current `.NET 10` repo baseline and an explicit decision.

## Deferred Decisions

- Whether `Hexalith.EventStore.Contracts` is a permanent approved Contracts dependency or should be split behind a smaller shared contract abstraction is a package policy decision if the implementation cannot satisfy AC1 both cleanly and locally.
- Multi-targeting `netstandard2.1` plus `net10.0` remains out of scope unless explicitly approved; this story should first make the current `net10.0` package stable and testable.
- Public API snapshot tooling choice is left to the implementer unless the repo already has an established preferred tool.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 3.1 definition and Epic 3 context.
- `_bmad-output/planning-artifacts/prd.md` - FR33, FR37, FR42, NFR28, NFR31, and developer integration requirements.
- `_bmad-output/planning-artifacts/architecture.md` - package boundaries, dependency direction, NuGet package guidance, and validation commands.
- `_bmad-output/project-context.md` - current `.NET 10`, package, dependency, privacy, and submodule constraints.
- `_bmad-output/process-notes/story-creation-lessons.md` - L08 party review vs. elicitation sequencing.
- `README.md` - adopter-facing package positioning and current local topology.
- Microsoft Learn `dotnet pack` and `dotnet package list` documentation checked on 2026-05-20 for current .NET CLI validation command shape.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes

TBD

### File List

TBD

### Change Log

- 2026-05-20: Story created by BMAD pre-dev hardening automation as a ready-for-dev package-boundary and contract-stability story.
