# Sprint Change Proposal — Duplicate LoggerMessage Source Generator (CS0757) in Hexalith.Parties

- **Date:** 2026-05-25
- **Author:** Jérôme (via Correct Course workflow)
- **Change scope classification:** Minor (build / dependency hygiene — no PRD, epic, story, or runtime-behavior impact)
- **Status:** Implemented and verified
- **Triggered by:** Pre-existing compile break discovered during the 2026-05-25 Aspire-solution-membership correction (see `sprint-change-proposal-2026-05-25-aspire-solution-membership.md`, Section 5).

---

## Section 1 — Issue Summary

**Problem statement.** `src/Hexalith.Parties/Hexalith.Parties.csproj` fails to compile with **13× `CS0757`** ("A partial method may not have multiple implementing declarations") in generated logging code. This blocks a full `dotnet build` of `Hexalith.Parties.slnx` and contradicts the assumed "feature-complete / builds clean" project state.

**How discovered.** Flagged as an out-of-scope follow-up while verifying the Aspire-solution-membership change. Confirmed pre-existing and independent of that change: it reproduces when building the project directly (no solution) and with a cleaned `obj/`; the only working-tree change at the time was `Hexalith.Parties.slnx`.

**Root cause.** Two C# source generators both recognize `[LoggerMessage]` and both emit implementations for the same partial methods, so every generated method has two implementing declarations:

| Generator | Origin |
|-----------|--------|
| `Microsoft.Extensions.Logging.Generators.dll` (in-box `LoggerMessageGenerator`) | AspNetCore shared framework ref pack `Microsoft.AspNetCore.App.Ref/10.0.8` |
| `Microsoft.Gen.Logging.dll` (R9 telemetry `LoggingGenerator`) | NuGet `Microsoft.Extensions.Telemetry.Abstractions 10.6.0` (`analyzers/dotnet/cs/`) |

Both write a generated tree (`LoggerMessage.g.cs` and `Logging.g.cs`) into the compilation; the result is `CS0757` for each of the 13 `[LoggerMessage]` methods.

**Evidence.**
- The `csc` analyzer arguments for the project include **both** generators (captured via `-getItem:CscCommandLineArgs`).
- `obj/Debug/net10.0/generated/` contains **both** `Microsoft.Extensions.Logging.Generators/.../LoggerMessage.g.cs` and `Microsoft.Gen.Logging/.../Logging.g.cs`.
- Each generated file is internally valid; the conflict is solely the duplication across the two trees.
- The 13 errors map 1:1 to the 13 `[LoggerMessage]` methods: `PartyDomainServiceInvoker` (5), `PartyDetailProjectionQueryActor.Log` (3), `PartyIndexProjectionQueryActor.Log` (3), `PartyProjectionUpdateOrchestrator.Log` (2).

**Why the telemetry generator is present (transitive, not pinned in `Directory.Packages.props`).**
```
Hexalith.Memories.Client.Rest 1.29.0          (direct PackageReference on Hexalith.Parties)
  └─ Microsoft.Extensions.Http.Resilience 10.6.0
       └─ Microsoft.Extensions.Telemetry 10.6.0
            └─ Microsoft.Extensions.Telemetry.Abstractions 10.6.0   ← ships Microsoft.Gen.Logging
```
`Hexalith.Parties.ServiceDefaults` reaches the same package through the same `Http.Resilience` path.

---

## Section 2 — Impact Analysis

- **Epic impact:** None. All 9 epics are DONE; this is post-implementation build hygiene.
- **Story impact:** None. No story acceptance criteria reference logging codegen or the build graph.
- **PRD / Architecture / UX impact:** None. No requirement, ADR, or UX flow changes.
- **Artifact conflicts:** None.
- **Runtime / behavior impact:** None. The fix is compile-time only; the in-box generator produces the same `ILogger` plumbing the code already targeted.

**Blast radius (measured, root repo only).** A project is affected only if it *both* declares `[LoggerMessage]` *and* receives the `Microsoft.Gen.Logging` analyzer:

| Project | Has `[LoggerMessage]` | Gets `Microsoft.Gen.Logging` analyzer | CS0757 |
|---------|:---:|:---:|:---:|
| `Hexalith.Parties` | yes | **yes** (via direct `Memories.Client.Rest` package ref) | **13** |
| `Hexalith.Parties.Projections` | yes | no | 0 |
| `Hexalith.Parties.Security` | yes | no | 0 |
| `Hexalith.Parties.ServiceDefaults` | yes | no | 0 |

`Hexalith.Parties` is the unique intersection because its **direct NuGet** `Hexalith.Memories.Client.Rest` reference flows analyzer assets transitively. (Submodule projects under `Hexalith.EventStore` / `Hexalith.Tenants` resolve their own `Directory.Build.props` and are out of scope for this repo's fix.)

**Safety of dropping the telemetry generator.** A repo-wide search found **no** R9-only logging attributes (`[LogProperties]`, `[TagProvider]`, `DataClassification`/redaction, `[LogPropertyIgnore]`) and no `Microsoft.Gen`-namespace usage. All `[LoggerMessage]` declarations are plain (`EventId` / `Level` / `Message`), which the in-box generator handles identically. `Microsoft.Gen.Logging` only generates code for the compiling project's own `[LoggerMessage]` methods — the resilience/telemetry runtime libraries do **not** depend on it — so removing it has no runtime effect.

---

## Section 3 — Recommended Approach

**Direct adjustment — central build fix.** Strip the conflicting `Microsoft.Gen.Logging` analyzer from the analyzer set before compile, keeping the in-box `Microsoft.Extensions.Logging.Generators` (which all other projects already use). Applied once in the root `Directory.Build.props` so it covers every root-repo project and is robust to a future project pulling the same transitive chain.

- **Scope chosen:** Central (`Directory.Build.props`), confirmed by the team during this workflow over a per-project edit.
- **Effort:** Trivial (single MSBuild `Target`).
- **Risk:** Minimal. Compile-time only; no source, package-version, or runtime change. No-op on projects that never had the analyzer.
- **Timeline impact:** None.

**Alternative considered (not chosen):** scope the same `Analyzer Remove` Target to `Hexalith.Parties.csproj` only. Smaller blast radius, but must be duplicated if another project later acquires the telemetry generator.

**Upstream note (non-blocking):** the dual-generator collision is a known packaging interaction between `Microsoft.Extensions.Telemetry.*` and the in-box logging generator. No action required here, but worth watching when `Microsoft.Extensions.Http.Resilience` / `Hexalith.Memories.Client.Rest` are next upgraded.

---

## Section 4 — Detailed Change Proposals

**Artifact: `Directory.Build.props` (repo root)**

Added before the closing `</Project>`:

```xml
<!-- Microsoft.Extensions.Telemetry.Abstractions (pulled transitively by
     Microsoft.Extensions.Http.Resilience) ships the Microsoft.Gen.Logging source
     generator, which also processes [LoggerMessage] and collides with the in-box
     Microsoft.Extensions.Logging.Generators, emitting duplicate partial-method
     implementations (CS0757). We use only plain [LoggerMessage] (no R9 redaction /
     [LogProperties] features), so drop the telemetry generator and keep the in-box one. -->
<Target Name="RemoveDuplicateLoggingSourceGenerator" BeforeTargets="CoreCompile">
  <ItemGroup>
    <Analyzer Remove="@(Analyzer)" Condition="'%(Filename)' == 'Microsoft.Gen.Logging'" />
  </ItemGroup>
</Target>
```

No source, `.csproj`, `Directory.Packages.props`, or runtime changes.

---

## Section 5 — Implementation Handoff & Verification

**Handoff:** Minor scope — implemented directly during this workflow.

**Verification performed (all `-c Debug --no-incremental -t:Rebuild`):**

| Project | Before fix | After fix |
|---------|:---:|:---:|
| `Hexalith.Parties` | CS0757 = 13 (×2 occurrences), build FAILED | **CS0757 = 0, Build succeeded** |
| `Hexalith.Parties.Projections` | CS0757 = 0 | CS0757 = 0, Build succeeded |
| `Hexalith.Parties.Security` | CS0757 = 0 | CS0757 = 0, Build succeeded |
| `Hexalith.Parties.ServiceDefaults` | CS0757 = 0 | CS0757 = 0, Build succeeded |

- Confirmed the in-box `Microsoft.Extensions.Logging.Generators` still runs (its generated output remains and the methods compile); only the duplicate `Microsoft.Gen.Logging` tree is removed.
- `TreatWarningsAsErrors=true` is global — the fix is warning-clean.

**Success criteria:** ✅ `Hexalith.Parties` compiles; the full-solution build is no longer blocked by `CS0757`; logging code generation is consistent (in-box generator) across all root-repo projects; no runtime or dependency-version change.
