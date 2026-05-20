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
| 2 | A minimal isolated consumer compile test references only the packed package, imports commands, events, models, results, value objects, rejections, and `[PersonalData]`, and proves XML documentation is generated, packaged, and accessible from the package output rather than only from source build output. |
| 3 | Contract/event compatibility tests prove existing public members remain source-compatible and new event/result fields are additive only. `PartyMerged` remains a documented placeholder contract with no required runtime production path unless a later story defines merge behavior. |
| 4 | Reflection tests prove required personal-data annotations remain discoverable from the package without referencing server, projection, UI, Dapr, AdminPortal, Testing, or security infrastructure implementations, and the tests do not log personal-data sample values. |
| 5 | Architectural/package fitness tests fail on forbidden references, missing package metadata/docs, absent XML docs, or unapproved public contract drift. Drift proof must use a checked-in public API snapshot, approval baseline, reflection baseline, or equivalent explicit compatibility mechanism. |

## Party-Mode Review Clarifications

- Package-boundary proof must inspect the actual packed `.nupkg` and generated `.nuspec` dependency graph. Source project references and broad solution builds are useful supporting evidence, but they are not sufficient proof that external consumers avoid service infrastructure dependencies.
- `Hexalith.EventStore.Contracts` is the only currently known candidate for an approved contract-basic dependency. Development must either document why it is stable, contract-only, and acceptable in the public package surface, or remove/hide it from packed dependencies and public API through a deliberate replacement or shared abstraction. Do not leave the decision implicit.
- If `Hexalith.EventStore.Contracts` remains, the story completion notes must state the allowed public surface and why it does not pull EventStore runtime, hosting, Dapr, gateway authorization, persistence, actor, or infrastructure behavior into `Hexalith.Parties.Contracts`.
- Public contract drift must be intentionally governed. Removing or renaming exported contract members, changing serialized names, changing enum values, or tightening constructor/property requirements is out of scope unless the implementation records an explicit migration decision; additive members and additive placeholder contracts are allowed.
- `PartyMerged` is a reserved, documented placeholder for forward compatibility in this story. It must not require server, projection, REST, MCP, UI, or EventStore runtime behavior and must not imply merge semantics until a later product/architecture decision defines them.
- Existing `tests/Hexalith.Parties.Contracts.Tests` references to AdminPortal or Testing projects must not be used as package-purity evidence. Package-purity tests should inspect the Contracts project and packed output directly, and consumer-usability tests should compile from a clean package reference.
- XML documentation evidence must come from the package output or isolated package consumption. A source build XML file alone does not prove the published package carries usable public API documentation.
- Personal-data metadata tests should enumerate the relevant public contract models and nested value objects through reflection, assert required `[PersonalData]` markers remain discoverable, and use redacted or synthetic non-personal fixtures so test output cannot disclose personal values.

## Advanced Elicitation Clarifications

- A library package may not produce a useful `.deps.json`; absence of that artifact is not a failure by itself. The required dependency proof must still inspect the `.nuspec`, `lib/net10.0` compile/runtime assets, package references visible to a clean consumer, and any generated assets that actually exist after `dotnet pack`.
- The clean-consumer proof must consume the packed package from a local package source, not a project reference. It should fail if the consumer can compile only because the repository source tree, sibling submodules, or broad solution references are still available.
- If `Hexalith.EventStore.Contracts` is retained, the proof must distinguish a contract-only compile dependency from a runtime dependency leak by checking both the `.nuspec` dependency graph and the consumer's transitive package/project closure. Any Dapr, hosting, actor, gateway, persistence, MediatR, FluentValidation, UI, AdminPortal, Picker, MCP, Server, or Projections dependency remains forbidden unless a later architecture decision changes AC1.
- Public API drift evidence must cover source shape and serialized contract shape. Renaming public members, changing required constructor/property semantics, changing discriminator/type names, changing enum values, or changing serialized member names is breaking unless explicitly captured as an approved migration decision; additive optional members and placeholder-only contracts are acceptable.
- XML documentation evidence must verify the XML file is packed next to the public assembly and can be read from the installed package by a clean consumer. A local source `bin` output, IDE tooltip, or generated file outside the `.nupkg` is insufficient.
- Reflection-based personal-data metadata checks must run against the packaged assembly loaded by the clean consumer or an equivalent package-output path. Assertions should report type/member names only and must not print sample person names, email addresses, phone numbers, identifiers, addresses, or other personal values.
- Package metadata inherited from `Directory.Build.props` currently describes the Dapr-native service broadly. If the Contracts package keeps that generic metadata, story completion notes must explicitly justify it; otherwise provide package-specific description/tags so consumers do not mistake the contract package for the actor host or runtime service.

## Non-Goals

- Do not add party behavior, merge behavior, REST endpoints, MCP tools, UI, projections, EventStore runtime implementation, service registration, authorization policy, package publishing automation, or release pipeline changes.
- Do not retarget the package, introduce multi-targeting, or change MinVer/package versioning policy without a separate architecture decision.
- Do not initialize or update nested submodules; root-level submodules are enough unless the user explicitly requests nested submodules.

## Tasks

- [ ] Audit the current Contracts package baseline. (AC: 1, 2)
  - [ ] Inspect `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj`, inherited package metadata from `Directory.Build.props`, and central versions from `Directory.Packages.props`.
  - [ ] Build and pack the Contracts project, then inspect the generated `.nupkg`, `.nuspec`, compile assets, and any generated dependency assets that actually exist for the library package.
  - [ ] Decide whether the current `Hexalith.EventStore.Contracts` project reference is an approved contract-basic dependency or must be removed/hidden from the consumer package; record the rationale in the story completion notes.
  - [ ] If `Hexalith.EventStore.Contracts` remains, prove the packed dependency is contract-only and does not expose EventStore runtime, gateway authorization, Dapr, hosting, persistence, actor, or infrastructure implementation behavior.
- [ ] Harden package metadata and documentation output. (AC: 2, 5)
  - [ ] Ensure `Hexalith.Parties.Contracts` has package-specific metadata where inherited defaults are too generic.
  - [ ] Generate and include XML documentation for public APIs in the packed output, and verify the XML documentation from package consumption or package artifact inspection.
  - [ ] Verify the packed XML documentation is installed beside the public assembly and is readable from the clean package consumer path.
  - [ ] Keep MinVer/tag-driven versioning and central package management; do not add project-local package versions or manual package-version drift.
- [ ] Prove the public contract surface is stable and additive. (AC: 2, 3, 5)
  - [ ] Add or tighten tests that enumerate public commands, events, models, results, state, value objects, search contracts, and security contract interfaces.
  - [ ] Add compatibility guardrails, such as a checked-in public API snapshot, approval baseline, reflection baseline, or equivalent, that fail when existing public contract members, serialized names, enum values, required members, or constructor shapes are removed or renamed without an explicit migration decision.
  - [ ] Confirm forward-compatible placeholders such as `PartyMerged` remain contract-only, documented, additive, serialization-compatible, and do not force server/projection/runtime behavior.
- [ ] Prove package dependency boundaries. (AC: 1, 5)
  - [ ] Add a package/reference fitness test that fails if `Hexalith.Parties.Contracts` references Dapr, hosting, MediatR, FluentValidation, UI, Server, Projections, actor host, MCP, AdminPortal, Picker, or concrete infrastructure packages.
  - [ ] Keep test dependencies isolated to test projects; do not make the Contracts test project's own AdminPortal/UI references part of the package-boundary proof.
  - [ ] Verify the packaged dependency graph, not only project source references, by inspecting the packed `.nupkg`/`.nuspec` and, where practical, the dependency graph seen from a clean package consumer.
- [ ] Prove consumer usability. (AC: 2, 4)
  - [ ] Add a minimal package consumer smoke test or compile-only test that references only the packed Contracts output from a local package source and uses representative commands, events, value objects, query models, result types, rejection events, and `[PersonalData]`.
  - [ ] Confirm consumers can inspect personal-data metadata without server, projection, Dapr, hosting, UI, or security implementation references.
  - [ ] Ensure no sample/test assertion logs personal data values from contract examples.

## Dev Notes

This story is a packaging and contract-boundary hardening story. It should not add new party behavior, REST endpoints, MCP tools, UI, projections, or EventStore runtime work. The goal is to make the already-developed Contracts surface safe and predictable for external .NET consumers.

The current repo baseline is important:

- `Directory.Build.props` targets `net10.0`, enables nullable, warnings-as-errors, MinVer package versioning, `IsPackable=true` by default, and shared package metadata.
- `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj` currently has a project reference to `Hexalith.EventStore.Contracts`.
- Because `Hexalith.EventStore.Contracts` is currently a sibling submodule project reference, package validation must prove what external consumers receive after packing. Do not assume local project-reference behavior and packed NuGet dependency behavior are equivalent.
- Planning artifacts still contain older wording about `netstandard2.1` and zero runtime dependencies. Current project context says the repo targets `.NET 10`; do not silently retarget the whole package to `netstandard2.1` unless a fresh architecture decision explicitly approves that change.
- Treat `Hexalith.EventStore.Contracts` as the main decision point: if it remains, the story needs explicit evidence that it is an approved contract-basic dependency rather than leaked infrastructure; if it is removed, downstream types such as command results and event payload interfaces must keep compiling through a deliberate replacement or shared abstraction.
- Clean-consumer validation should make accidental source-tree coupling visible. Prefer an isolated temporary consumer project with only the local package feed configured, then assert it can compile representative contract usage without references to the application solution, sibling submodules, actor hosts, UI, Dapr, or server projects.

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

- 2026-05-20: Advanced elicitation applied low-risk clarifications for package artifact proof, clean-consumer validation, transitive dependency leakage, serialized public API drift, XML docs from installed packages, privacy-safe reflection metadata, and package-specific metadata.
- 2026-05-20: Party-mode review applied low-risk clarifications for packed-package proof, `Hexalith.EventStore.Contracts` dependency handling, public API drift evidence, XML documentation evidence, personal-data metadata discovery, and non-goal boundaries.
- 2026-05-20: Story created by BMAD pre-dev hardening automation as a ready-for-dev package-boundary and contract-stability story.

## Party-Mode Review

- Date/time: 2026-05-20T12:05:33+02:00
- Selected story key: `3-1-publish-stable-contracts-package`
- Command/skill invocation used: `/bmad-party-mode 3-1-publish-stable-contracts-package; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers recommended `needs-story-update`, not blocked, until low-risk package-boundary and evidence clarifications were added.
  - Shared architecture and product risk centered on the unresolved `Hexalith.EventStore.Contracts` dependency decision. Reviewers agreed the story should either justify it as an approved contract-basic dependency or require it to be removed/hidden from the packed dependency graph and public API.
  - Shared implementation and test risk centered on misleading evidence: broad solution builds and the existing Contracts test project can pass even when the published `.nupkg` still leaks unwanted dependencies or omits XML documentation.
  - Shared adopter risk centered on public API drift, undocumented placeholder semantics for `PartyMerged`, and personal-data metadata that might be present in source but not discoverable from a clean package consumer.
- Changes applied:
  - Added `Party-Mode Review Clarifications` requiring packed `.nupkg`/`.nuspec` dependency proof, explicit `Hexalith.EventStore.Contracts` handling, public API drift governance, placeholder-only `PartyMerged` semantics, isolated package-consumer proof, XML-doc package-output evidence, and privacy-safe reflection evidence.
  - Added `Non-Goals` to keep runtime behavior, REST/MCP/UI/projection/EventStore runtime work, release automation, retargeting, multi-targeting, and nested submodule initialization out of scope.
  - Strengthened acceptance evidence and task rows for isolated package consumption, XML documentation from package output, checked-in or equivalent public API baseline, additive event compatibility, forbidden dependency checks, and central package management.
- Findings deferred:
  - Whether `Hexalith.EventStore.Contracts` is a permanent approved contract-basic dependency or should be split behind a smaller shared contract abstraction remains a package policy decision if implementation cannot satisfy AC1 cleanly and locally.
  - The canonical long-term public API baseline mechanism remains implementer-selected unless the repo already has an established preferred tool.
  - Full merge semantics for `PartyMerged` remain out of scope; this story treats it as a reserved placeholder only.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- Date/time: 2026-05-20T19:03:49+02:00
- Selected story key: `3-1-publish-stable-contracts-package`
- Command/skill invocation used: `/bmad-advanced-elicitation 3-1-publish-stable-contracts-package`
- Batch 1 methods: Red Team vs Blue Team, Failure Mode Analysis, Security Audit Personas, Self-Consistency Validation, Architecture Decision Records
- Batch 2 methods: Pre-mortem Analysis, Chaos Monkey Scenarios, User Persona Focus Group, Critique and Refine, Expand or Contract for Audience
- Findings summary:
  - The highest-risk hidden failure is proving the source project instead of the packed package. A broad solution build can pass while a clean external consumer still inherits unwanted dependencies, lacks XML docs, or relies on sibling source projects.
  - The `Hexalith.EventStore.Contracts` decision needs transitive package evidence, not only local project-reference inspection, because the story's value depends on what external consumers receive from NuGet-like consumption.
  - Public API stability must include serialized names, enum values, required member semantics, constructors, and placeholder-only event contracts, not just type presence.
  - Personal-data metadata evidence is only adopter-relevant if it can be discovered from the packaged assembly without leaking sample personal values in test output.
- Changes applied:
  - Added `Advanced Elicitation Clarifications` covering library package artifact expectations, clean local-package-source consumption, `Hexalith.EventStore.Contracts` transitive dependency proof, serialized public API drift, XML docs from installed package output, privacy-safe metadata reflection, and package-specific metadata.
  - Tightened task rows for generated dependency assets, XML documentation from the consumer path, serialized-shape compatibility baselines, and clean package consumer validation.
  - Added Dev Notes warning that sibling submodule project-reference behavior and packed NuGet dependency behavior are not equivalent.
- Findings deferred:
  - Whether `Hexalith.EventStore.Contracts` remains a permanent approved contract-basic dependency or is split behind a smaller abstraction remains a package policy decision if AC1 cannot be satisfied locally.
  - The exact public API baseline tool remains implementer-selected unless a repo-standard tool is introduced elsewhere.
  - Any package retargeting, multi-targeting, release automation, or long-term package metadata policy remains outside this story.
- Final recommendation: `ready-for-dev`
