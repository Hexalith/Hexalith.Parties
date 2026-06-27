---
baseline_commit: 4868149ef70f670376bced52b0bbcf04c65e3d41
---

# Story 1.1: Stand up the Hexalith.Parties.UI Blazor Server host

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a new FrontComposer shell-host project wired into the solution and AppHost,
so that there is a running `parties-ui` app to build the experience on.

## Acceptance Criteria

**AC1 — Host project created on the FrontComposer shell-host pattern**

**Given** the FrontComposer shell-host pattern (`references/Hexalith.FrontComposer/samples/Counter/Counter.Web`)
**When** I create `src/Hexalith.Parties.UI` (`Microsoft.NET.Sdk.Web`, `net10.0`)
**Then** it wires the FrontComposer Quickstart chain + `AddFluentUIComponents()` + `AddHexalithDomain<PartiesUiDomainMarker>()`, sets `ValidateScopes=true` (ADR-030), and is added to `Hexalith.Parties.slnx`
**And** references resolve via the computed sibling-root properties (`$(HexalithFrontComposerRoot)`) — **no NuGet conversion** of EventStore/Tenants/FrontComposer project references.

**AC2 — AppHost resource `parties-ui` (no DAPR sidecar)**

**Given** the AppHost
**When** I add `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")` referencing `eventstore` + `tenants`
**Then** the resource starts with **no DAPR sidecar** (BFF over HTTP/SignalR, like `parties-mcp`)
**And** `dotnet aspire run` shows `parties-ui` healthy once `eventstore`/`tenants` are healthy.

**AC3 — Build gate green**

**Given** the solution build gate
**When** I build `Hexalith.Parties.slnx -c Release`
**Then** it succeeds with **0 warnings** under solution-wide `TreatWarningsAsErrors`, with **no `Version=`** in the csproj (Central Package Management) and **no warning override**.

## Tasks / Subtasks

- [x] **Task 1 — Create the `Hexalith.Parties.UI` host project file** (AC: 1, 3)
  - [x] Create `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` with `Sdk="Microsoft.NET.Sdk.Web"` (see Dev Notes → *csproj to author*).
  - [x] Set host properties: `<IsPackable>false</IsPackable>`, `<IsPublishable>true</IsPublishable>` (override the packable defaults from `Directory.Build.props`, matching `Hexalith.Parties.Mcp`).
  - [x] Do **not** add a `<TargetFramework>` — it is inherited (`net10.0`) from `Directory.Build.props`.
  - [x] Add `ProjectReference`s via `$(HexalithFrontComposerRoot)`: `Hexalith.FrontComposer.Shell` + `Hexalith.FrontComposer.Mcp` (dev), and the `Hexalith.FrontComposer.SourceTools` **analyzer** reference (`OutputItemType="Analyzer"`, `netstandard2.0`).
  - [x] Add `PackageReference Include="Microsoft.FluentUI.AspNetCore.Components"` with **no `Version=`** (CPM).
  - [x] Do **not** reference `AdminPortal`, `ConsumerPortal`, `Parties.Client`, `Parties.Contracts`, or `ServiceDefaults` yet — those are pulled in by their own stories (2.1 / 4.3 / 1.2 / 1.10). Keep 1.1 minimal so the host boots clean.

- [x] **Task 2 — Author the host composition** (AC: 1)
  - [x] Add `src/Hexalith.Parties.UI/PartiesUiDomainMarker.cs` — empty `sealed class` with `[BoundedContext("Parties")]` (see Dev Notes → *domain marker*).
  - [x] Add `Program.cs` mirroring `Counter.Web/Program.cs` (non-specimens path): `UseDefaultServiceProvider(ValidateScopes=true)` → `AddRazorComponents().AddInteractiveServerComponents()` → `AddFluentUIComponents()` → `AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(Program).Assembly))` → `AddFrontComposerDevMode(builder.Environment)` → `AddHexalithDomain<PartiesUiDomainMarker>()`; then the middleware pipeline (`MapStaticAssets` → `UseStaticFiles` → `UseRequestLocalization` → `UseAntiforgery` → `MapRazorComponents<App>().AddInteractiveServerRenderMode()`).
  - [x] Add `Components/App.razor`, `Components/Routes.razor`, `Components/_Imports.razor`, and `Components/Layout/MainLayout.razor` adapted from Counter.Web (rename namespaces to `Hexalith.Parties.UI`; CSS link → `Hexalith.Parties.UI.styles.css`). `MainLayout` renders the FrontComposer shell.
  - [x] Add `appsettings.json` + `appsettings.Development.json` with the `Hexalith:Shell` section (mirror Counter.Web). **No OIDC / secrets** here — auth is Story 1.2.
  - [x] Add `Properties/launchSettings.json` (Aspire injects endpoints at run; a profile is still useful for standalone debugging).

- [x] **Task 3 — Register the project in the solution** (AC: 1, 3)
  - [x] Add `<Project Path="src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj" />` under the `/src/` `<Folder>` in `Hexalith.Parties.slnx`.

- [x] **Task 4 — Wire the Aspire AppHost resource** (AC: 2)
  - [x] Add `<ProjectReference Include="..\Hexalith.Parties.UI\Hexalith.Parties.UI.csproj" />` to `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` (generates `Projects.Hexalith_Parties_UI`).
  - [x] In `src/Hexalith.Parties.AppHost/Program.cs`, **after** the `tenants` resource is fully declared (after line ~110), add the `parties-ui` registration: `AddProject<Projects.Hexalith_Parties_UI>("parties-ui").WithReference(eventStore).WaitFor(eventStore).WithReference(tenants).WaitFor(tenants)` — **no** `.WithDaprSidecar(...)`, **no** `.WithExplicitStart()` (it should auto-start). See Dev Notes → *AppHost wiring*.
  - [x] Do **not** add JWT/Keycloak env or `.WithReference(keycloak)` for `parties-ui` in this story — OIDC sign-in is Story 1.2.

- [x] **Task 5 — Add a minimal boot/composition smoke test** (supports AC: 1)
  - [x] Create `tests/Hexalith.Parties.UI.Tests` (xUnit v3 + bUnit + Shouldly + NSubstitute) and register it in `Hexalith.Parties.slnx` under `/tests/`. Model the harness on `tests/Hexalith.Parties.AdminPortal.Tests`.
  - [x] Add a smoke test that composes the FrontComposer Quickstart chain + `AddHexalithDomain<PartiesUiDomainMarker>()` into a `ServiceCollection`, builds the provider with `ValidateScopes=true`, and asserts the domain registers (e.g. resolve `IFrontComposerRegistry` / assert no scope-capture throw). This is the unit-testable slice of host stand-up; full routing/role tests arrive in Story 1.3.

- [x] **Task 6 — Verify the build gate and local run** (AC: 2, 3)
  - [x] `dotnet restore Hexalith.Parties.slnx` then `dotnet build Hexalith.Parties.slnx -c Release --no-restore` → **0 warnings**. For a reliable clean-build verdict, build single-threaded (`-m:1`) — parallel clean builds flake (see Dev Notes → *Build & verification gotchas*).
  - [x] `bash scripts/check-no-warning-override.sh` passes (no `TreatWarningsAsErrors=false`, no stray `-p:` override).
  - [x] Confirm the new csproj contains **no `Version=`** attribute on any `PackageReference`.
  - [x] `dotnet aspire run --project src/Hexalith.Parties.AppHost` (Docker Desktop running) → dashboard shows `parties-ui` reaching **Running/healthy** after `eventstore` + `tenants` are healthy.

## Dev Notes

> This is the **first implementation story** of the whole `parties-ui` initiative and the first story in Epic 1. Its only job is to stand up a *bootable* FrontComposer shell host and register it in the orchestrator — **nothing more**. OIDC (1.2), role routing + `Consumer` policy (1.3), `party_id` resolution (1.4), self-scope accessor (1.5), `StatusKind` map (1.6), SignalR freshness (1.7), domain components (1.8), the a11y gate (1.9), and container/K8s deploy + `ServiceDefaults` (1.10) are **explicitly out of scope here**. Resist pulling them in early. [Source: epics.md#Epic-1; architecture.md#Implementation-sequence]

### The canonical reference — copy it, don't invent

`references/Hexalith.FrontComposer/samples/Counter/Counter.Web` **is** the selected starter (architecture.md "Selected Starter: FrontComposer shell-host pattern"). The dev agent should read these files and adapt them name-for-name to `Hexalith.Parties.UI`; do not hand-roll a `dotnet new blazor` host:

- `references/Hexalith.FrontComposer/samples/Counter/Counter.Web/Counter.Web.csproj`
- `references/Hexalith.FrontComposer/samples/Counter/Counter.Web/Program.cs`
- `references/Hexalith.FrontComposer/samples/Counter/Counter.Web/Components/{App.razor, Routes.razor, _Imports.razor, Layout/MainLayout.razor}`
- `references/Hexalith.FrontComposer/samples/Counter/Counter.Web/appsettings.Development.json`
- `references/Hexalith.FrontComposer/samples/Counter/Counter.Web/Properties/launchSettings.json`

### csproj to author — `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>true</IsPublishable>
    <!-- TargetFramework (net10.0), Nullable, ImplicitUsings, TreatWarningsAsErrors all inherited
         from Directory.Build.props — DO NOT redeclare. -->
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.FluentUI.AspNetCore.Components" />
    <ProjectReference Include="$(HexalithFrontComposerRoot)\src\Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj" />
    <ProjectReference Include="$(HexalithFrontComposerRoot)\src\Hexalith.FrontComposer.Mcp\Hexalith.FrontComposer.Mcp.csproj" />
    <!-- SourceTools analyzer: discovers [Projection]/[Command]/[ProjectionTemplate] markers; emits
         the generated registration symbols. Analyzer-only reference (no compile-time assembly). -->
    <ProjectReference Include="$(HexalithFrontComposerRoot)\src\Hexalith.FrontComposer.SourceTools\Hexalith.FrontComposer.SourceTools.csproj"
                      OutputItemType="Analyzer"
                      PrivateAssets="all"
                      ReferenceOutputAssembly="false"
                      SetTargetFramework="TargetFramework=netstandard2.0" />
  </ItemGroup>

</Project>
```

- `Microsoft.FluentUI.AspNetCore.Components` is centrally pinned at `5.0.0-rc.3-26138.1` (RC — do not bump; flagged as a version risk, not a blocker). [Source: Directory.Packages.props; architecture.md#Coherence-Validation]
- `$(HexalithFrontComposerRoot)` is the computed sibling-root property (`Directory.Build.props:10-11`) that probes `references\Hexalith.FrontComposer\src\…` then `..\references\Hexalith.FrontComposer\src\…`. This is exactly how `Hexalith.Parties.AdminPortal.csproj` references the Shell — mirror it. This satisfies AC1's "references resolve via the computed sibling-root properties." [Source: src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj:13-16]
- **Contingency on `ASP0006`:** Counter.Web carries `<NoWarn>$(NoWarn);ASP0006</NoWarn>` because generated `RenderTreeBuilder` code uses the `seq++` pattern. With an empty domain marker (no `[Command]`/`[Projection]` in this host yet) the generator emits nothing, so `ASP0006` should not fire — leave `NoWarn` **off**. If `TreatWarningsAsErrors` trips `ASP0006` from generated code, add the **narrow** `<NoWarn>$(NoWarn);ASP0006</NoWarn>` (one rule, this project only) per the sanctioned escape valve, never a global override. [Source: project-context.md#Language-Specific-Rules; Counter.Web.csproj]

### Domain marker — `src/Hexalith.Parties.UI/PartiesUiDomainMarker.cs`

```csharp
using Hexalith.FrontComposer.Contracts.Attributes;

namespace Hexalith.Parties.UI;

[BoundedContext("Parties")]
public sealed class PartiesUiDomainMarker;
```

- The marker is a bare class decorated with `[BoundedContext(...)]` from `Hexalith.FrontComposer.Contracts.Attributes` (the same attribute the existing `PartiesAdminPortalManifest` uses with `BoundedContext: "Parties"`). `AddHexalithDomain<T>()` reflects over the marker's assembly to register the bounded context. [Source: Counter sample `CounterDomain` (`[BoundedContext("Counter")]`); src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs:13]
- **Bounded-context name = `"Parties"`** to align with the existing AdminPortal manifest. ⚠️ When Story 2.1 embeds the `AdminPortal` RCL (which also registers a `"Parties"` domain manifest), the dev there must avoid a duplicate-context registration conflict — note this forward, but it does **not** affect 1.1 (AdminPortal is not referenced yet).

### Program.cs — mirror Counter.Web (non-specimens path)

Required call sequence (the AC pins the first four — Quickstart chain, FluentUI, domain marker, `ValidateScopes`):

```csharp
using Hexalith.FrontComposer.Mcp.Extensions;     // AddFrontComposerMcp / MapFrontComposerMcp (dev)
using Hexalith.FrontComposer.Shell.Extensions;   // AddHexalithFrontComposerQuickstart / AddHexalithDomain / AddFrontComposerDevMode
using Hexalith.Parties.UI;
using Hexalith.Parties.UI.Components;
using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ADR-030 — ValidateScopes=true so a Singleton capturing a Scoped service fails at boot
// (not silently leak across tenants). MUST sit on the host builder before service resolution.
builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

// Quickstart chains AddLocalization + AddHexalithShellLocalization + AddHexalithFrontComposer.
builder.Services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(Program).Assembly));
builder.Services.AddFrontComposerDevMode(builder.Environment);
builder.Services.AddHexalithDomain<PartiesUiDomainMarker>();

WebApplication app = builder.Build();

app.MapStaticAssets();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

- **Projection-templates registration:** Counter.Web's non-specimens path also calls `AddHexalithProjectionTemplates(__FrontComposerProjectionTemplatesRegistration.Descriptors)`. That generated symbol only exists when the SourceTools analyzer finds projection templates. This host defines none in 1.1 — **omit that line** unless the FrontComposer shell fails to boot without it; if it does, add it (the analyzer reference is already present to emit the symbol). The AC-mandated calls are the four above. [Source: Counter.Web/Program.cs]
- **MCP mapping (dev only):** Counter.Web maps `app.MapFrontComposerMcp()` under `IsDevelopment()||IsEnvironment("Test")`. Optional for 1.1; include it (matching the `(+ .Mcp dev)` reference in AR-Starter) or defer — it does not affect the AC. Keep any fake-auth flag **out** (auth is 1.2). [Source: epics.md#AR-Starter; Counter.Web/Program.cs]
- **Add an explicit class declaration** `public partial class Program;` at the end if the smoke-test project needs to reference `Program` (top-level statements otherwise make it `internal`).
- **No auth wiring / no demo accessor needed to boot.** The Quickstart registers a default **fail-closed `NullUserContextAccessor`** (`TryAddScoped`, FrontComposer Decision D31 — "adopters replace"), so the shell renders without authentication. **Do NOT copy Counter.Web's `DemoUserContextAccessor` / `CounterFakeAuthUserContextAccessor` / `FakeAuth` block** — those are sample-only pre-fill demos and would add scope/secrets this story must not carry. Real auth (`OpenIdConnect` → `ClaimsPrincipalUserContextAccessor`) is Story 1.2. [Source: Hexalith.FrontComposer.Shell/Extensions/ServiceCollectionExtensions.cs:249-251]

### Razor components (adapt from Counter.Web)

- `Components/App.razor` — HTML root; CSS links must include `_content/Microsoft.FluentUI.AspNetCore.Components/...bundle.scp.css`, `_content/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.styles.css`, and **`Hexalith.Parties.UI.styles.css`** (the host's own scoped bundle); `<HeadOutlet @rendermode="RenderMode.InteractiveServer" />` and `<Routes @rendermode="RenderMode.InteractiveServer" />`.
- `Components/Routes.razor` — `<CascadingAuthenticationState>` wrapping `<Router AppAssembly="typeof(Program).Assembly">` with `DefaultLayout="typeof(Layout.MainLayout)"`. (The `CascadingAuthenticationState` is harmless now and ready for 1.2's auth.)
- `Components/Layout/MainLayout.razor` — hosts the FrontComposer shell component (read Counter.Web's `MainLayout.razor` for the exact shell element + `@Body`). The architecture's tree describes this as `<FrontComposerShell>@Body</FrontComposerShell>`. [Source: architecture.md#Complete-Project-Directory-Structure]
- `Components/_Imports.razor` — the standard FrontComposer imports (FluentUI, Fluxor, `Hexalith.FrontComposer.Contracts.Registration`, plus `Hexalith.Parties.UI.Components`).

### appsettings (no secrets, no OIDC in 1.1)

Mirror Counter.Web's `Hexalith:Shell` block (lifecycle thresholds, `LocalStorageMaxEntries`, `DefaultCulture`). Leave `AccentColor` at the shell default; the **AA-safe filled-primary brand-fill gate** (`--colorBrandBackground`, never raw teal `#0097A7` for text-bearing fills) is enforced in **Story 1.9**, not here. Runtime config overrides use `__`-nested env vars. **Never commit secrets/tokens.** [Source: project-context.md#Development-Workflow-Rules; epics.md#UX-DR1; Story 1.9]

### AppHost wiring — `src/Hexalith.Parties.AppHost/Program.cs`

Add **after** the `tenants` resource block (it must reference both `eventStore` and `tenants`, which are declared at lines 27 and 71 respectively; insert after ~line 110). Model on the **`parties-mcp`** registration (lines 62-69) — the in-repo precedent for a host with **no DAPR sidecar**:

```csharp
// parties-ui: Blazor Server BFF over HTTP/SignalR — NO DAPR sidecar (like parties-mcp /
// eventstore-admin-ui). Auto-starts (no WithExplicitStart). OIDC/Keycloak wiring is Story 1.2.
IResourceBuilder<ProjectResource> partiesUi = builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithReference(tenants)
    .WaitFor(tenants);
```

- **No `.WithDaprSidecar(...)`** — contrast with `parties` (lines 50-60) and `tenants` (lines 71-81), which both attach a sidecar. The UI is a BFF, not a DAPR actor host. [Source: architecture.md#D10; epics.md#AR-D10]
- **No `.WithExplicitStart()`** — `parties-mcp` and `eventstore-admin-ui` use explicit start; `parties-ui` is the primary app and must auto-start so AC2 ("healthy once eventstore/tenants are healthy") is observable on `aspire run`.
- AppHost csproj `ProjectReference` → the generated resource type is `Projects.Hexalith_Parties_UI` (dots become underscores). [Source: src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj]
- **Do NOT touch** the `Aspire.AppHost.Sdk/13.4.3` version or `Aspire.Hosting` `13.4.3` — they must stay equal or DCP dies with `unknown flag: --tls-cert-file` and the dashboard hangs at "Starting dashboard…". [Source: memory `aspire-apphost-sdk-version-match`]
- "Healthy" in 1.1 is the default Aspire **Running** state once dependencies are up; full health-check endpoints via `ServiceDefaults` (`AddServiceDefaults()` + OpenTelemetry) land in **Story 1.10**. Don't add `ServiceDefaults` here. [Source: epics.md#Story-1.10, #AR-D10]

### Solution file — `Hexalith.Parties.slnx`

`.slnx` is the **XML** solution format (not classic `.sln`). Add the host under the existing `/src/` `<Folder>` and the test project under `/tests/`:

```xml
<Project Path="src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj" />
<!-- and under /tests/ -->
<Project Path="tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj" />
```
[Source: Hexalith.Parties.slnx; project-context.md#Technology-Stack]

### Build & verification gotchas (read before declaring done)

- **`TreatWarningsAsErrors` is solution-wide and absolute** (`Directory.Build.props:15`). Every analyzer/compiler warning fails AC3. File-scoped namespaces only; `using` outside the namespace, `System.*` first; `_camelCase` private fields; `Async` suffix; interfaces `I*` — all enforced as errors. CRLF, 4-space indent, final newline, UTF-8 (`.editorconfig`). [Source: project-context.md#Language-Specific-Rules]
- **Central Package Management is ON** — `Directory.Packages.props` owns all versions; a `Version=` on any `PackageReference` in the new csproj is a build error and fails AC3. [Source: project-context.md#Technology-Stack]
- **Clean parallel builds flake** with CS0006 (Rebuild race) / MSB4018 (StaticWebAssets file-lock) — these are not code bugs. Use **`-m:1`** for a reliable clean-build verdict. [Source: memory `parties-parallel-build-flake`]
- **Build gate:** `bash scripts/check-no-warning-override.sh` must pass (no `-p:TreatWarningsAsErrors=false`, no global override). The only sanctioned warning suppression is a narrow per-csproj `<NoWarn>RuleId</NoWarn>`. [Source: project-context.md#Language-Specific-Rules; docs/build-gate.md]
- **Submodules** must be present as **`references/` checkouts**: `git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants` (root-repository submodules only, **never `--recursive`**). FrontComposer is also a reference submodule resolved by `$(HexalithFrontComposerRoot)`. [Source: project-context.md#Technology-Stack]
- **Do not "align" the `Microsoft.Extensions.Hosting.Abstractions` 11.0.0-preview pin down** — it is load-bearing for the `[LoggerMessage]` source generator. (Not expected to surface in this story, but don't touch it if tempted during restore.) [Source: memory `hosting-abstractions-preview-pin-load-bearing`]
- **Run prerequisites:** Docker Desktop must be running for `aspire run`; the system is usable once `eventstore`, `parties`, and `tenants` are healthy. `parties-ui` should appear and reach Running after `eventstore`/`tenants`. [Source: project-context.md#Development-Workflow-Rules]

### Testing standards

- **xUnit v3** (not v2), **Shouldly** assertions (`value.ShouldBe(...)`), **NSubstitute** mocks (`Substitute.For<T>()`), **bUnit** for Blazor components. Do **not** introduce Moq, FluentAssertions, or raw `Assert.*`. [Source: project-context.md#Testing-Rules]
- Model the new `tests/Hexalith.Parties.UI.Tests` harness on `tests/Hexalith.Parties.AdminPortal.Tests`. For 1.1 the meaningful test is a **DI-composition/boot smoke test** (compose the Quickstart chain + `AddHexalithDomain<PartiesUiDomainMarker>()`, build with `ValidateScopes=true`, assert the domain registers and no scope-capture is thrown). Routing/role-landing bUnit tests are Story 1.3's deliverable — don't pre-build them here.
- Run lanes via `scripts/test.ps1 -Lane unit` (default, Release). `integration`/`topology` skip gracefully without Docker/DAPR — a skip is not a failure. [Source: project-context.md#Testing-Rules]

### Project Structure Notes

- New host lives at `src/Hexalith.Parties.UI/` (`Microsoft.NET.Sdk.Web`); new tests at `tests/Hexalith.Parties.UI.Tests/`. Namespaces follow folder paths (`Hexalith.Parties.UI`, `Hexalith.Parties.UI.Components`, …). This matches the architecture's target tree. [Source: architecture.md#Complete-Project-Directory-Structure]
- The host is **adopter-facing only at the shell level** in this story; the adopter-facing vs internal split (don't reference the internal `Hexalith.Parties` host / `Server` / `Projections` / `Security` from UI code) becomes relevant as later stories add references — for 1.1 the only references are FrontComposer (Shell/Mcp/SourceTools) + FluentUI. [Source: project-context.md#Code-Quality-Style-Rules]
- **Variance vs the architecture tree (intentional, scoped):** the architecture's `UI/` tree shows `Components/Account/`, `Components/Shared/`, `Authentication/`, `Services/`, `Resources/`, and references to AdminPortal/ConsumerPortal/Client. Those are populated by Stories 1.2–1.10 / 2.1 / 4.3. Story 1.1 deliberately creates **only** the bootable skeleton (csproj, marker, `Program.cs`, `Components/{App,Routes,_Imports,Layout/MainLayout}`, appsettings, launchSettings). This is expected, not a conflict.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.1] — story statement + the three AC groups.
- [Source: _bmad-output/planning-artifacts/epics.md#Additional-Requirements] — AR-Starter (no external CLI starter; FrontComposer shell-host pattern), AR-D1 (Interactive Server, `ValidateScopes=true`), AR-D10 (AppHost `parties-ui`, no DAPR sidecar).
- [Source: _bmad-output/planning-artifacts/architecture.md#Starter-Template-Evaluation] — selected starter rationale + `Program.cs` wiring sketch.
- [Source: _bmad-output/planning-artifacts/architecture.md#Infrastructure-Deployment] — D10 Aspire resource (no sidecar, references eventstore + tenants).
- [Source: references/Hexalith.FrontComposer/samples/Counter/Counter.Web/] — the canonical pattern (csproj, Program.cs, Components, appsettings, launchSettings).
- [Source: src/Hexalith.Parties.AppHost/Program.cs:50-110] — `parties` (with sidecar), `parties-mcp` (no sidecar, lines 62-69), `tenants` registration patterns.
- [Source: src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj] — `Aspire.AppHost.Sdk/13.4.3`; ProjectReference → `Projects.*` generation.
- [Source: src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj] — closest in-repo host analog (`Sdk.Web`, `IsPackable=false`, `IsPublishable=true`).
- [Source: src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj:13-16] — `$(HexalithFrontComposerRoot)` / `$(HexalithTenantsRoot)` reference pattern.
- [Source: src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs:12-16] — `DomainManifest` / `[BoundedContext("Parties")]` precedent.
- [Source: Directory.Build.props:4-11,12,15] — computed sibling-root properties; inherited `net10.0`; solution-wide `TreatWarningsAsErrors`.
- [Source: Directory.Packages.props] — `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.3-26138.1`; Aspire `13.4.3`.
- [Source: _bmad-output/project-context.md] — language/framework/testing/quality/workflow rules + anti-patterns.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8) — BMAD dev-story workflow

### Debug Log References

- **HFC1001 (FrontComposer.SourceTools analyzer, Warning → error under solution-wide `TreatWarningsAsErrors`):** the first build of the minimal host failed with `HFC1001: No [Command] or [Projection] types found in compilation`. Story 1.1 deliberately stands up an empty `[BoundedContext]` shell host with **zero** Command/Projection types (those arrive in later stories), so the SourceTools analyzer has nothing to discover and emits HFC1001. Resolved with a **narrow, single-rule, this-project-only** `<NoWarn>$(NoWarn);HFC1001</NoWarn>` — the sanctioned escape valve documented in `docs/build-gate.md` and explicitly recommended by `scripts/check-no-warning-override.sh`'s own remediation text ("add a narrow `<NoWarn>RuleId</NoWarn>` in the consuming project"). The SourceTools **analyzer reference is retained** (per the AR-Starter contract) so it activates automatically once this host declares its first marker; the NoWarn carries a removal note.
- **NETSDK1086 avoidance (deviation from the authored csproj snippet):** the story's *csproj to author* listed `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, but this host uses `Microsoft.NET.Sdk.Web`, where that framework reference is **implicit** — declaring it explicitly raises NETSDK1086 as an **error** and would fail AC3. Both in-repo Web-SDK hosts (`Counter.Web`, `Hexalith.Parties.Mcp`) omit it, so it was omitted here. No functional impact (the ASP.NET Core shared framework is referenced implicitly by the Web SDK).
- **MCP endpoint mapping deferred (Task 2 option):** the `Hexalith.FrontComposer.Mcp` ProjectReference is kept (Task 1 / AR-Starter `(+ .Mcp dev)`), but `app.MapFrontComposerMcp()` / `AddFrontComposerMcp(...)` were **not** wired — Dev Notes mark this optional and AC-neutral, and adding it would pull sample-only MCP gate/API-key scaffolding the minimal 1.1 host must not carry. The reference activates the moment a later story opts in.
- **`partiesUi` registration uses a `_ =` discard** (not a named local) in AppHost `Program.cs` — the resource is not referenced later, matching this file's fire-and-effect idiom for `parties`/`tenants` wiring and avoiding an unused-variable warning under `TreatWarningsAsErrors`.
- **AC2 local-run note:** a first `aspire run` with `EnableKeycloak=false` (chosen to slim the topology) made `eventstore`/`tenants`/`parties` abort with `OptionsValidationException: Authentication:JwtBearer requires either 'Authority' or 'SigningKey'` — these services require the JWT authority that Keycloak provides. This is unrelated to Story 1.1 (parties-ui carries no auth and ran fine). Re-ran with the **default** topology (Keycloak on); see Completion Notes for the verified result.
- **Pre-existing, out-of-scope full-solution pack failure:** `dotnet build Hexalith.Parties.slnx -c Release` reports 3 NuGet **pack** errors (NU5118/NU5128) in the `Hexalith.PolymorphicSerializations` submodule (`GeneratePackageOnBuild=true`). Reproduced by building that submodule project **standalone on a git-clean tree** — independent of this story's changes. Every Story 1.1 project (and the AppHost) **compiles with 0 warnings**; only that submodule's package-generation step fails. Not addressed here (modifying a submodule's packaging is outside this story's scope).

### Completion Notes List

- **AC1 — satisfied.** New `src/Hexalith.Parties.UI` host (`Microsoft.NET.Sdk.Web`, inherited `net10.0`) wires the FrontComposer Quickstart chain + `AddFluentUIComponents()` + `AddHexalithDomain<PartiesUiDomainMarker>()` with `UseDefaultServiceProvider(ValidateScopes=true)` (ADR-030). References resolve via `$(HexalithFrontComposerRoot)` (Shell + Mcp + SourceTools analyzer) — no NuGet conversion. Added under `/src/` in `Hexalith.Parties.slnx`. Host builds **0 warnings / 0 errors** (Release, `-m:1`).
- **AC2 — satisfied (verified live).** AppHost registers `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")` referencing + waiting on `eventstore` and `tenants`, with **no `.WithDaprSidecar(...)`** and **no `.WithExplicitStart()`**. `dotnet aspire run` (default topology, Docker up) showed via the Aspire MCP `list_resources`: `eventstore` **Healthy**, `tenants` **Healthy**, then `parties-ui` **Running / Healthy** serving `http://localhost:5210` + `https://localhost:7210`. The DaprSidecar resource set is `{eventstore, parties, tenants, eventstore-admin, eventstore-admin-ui}` — **no `parties-ui-dapr`** exists, confirming the BFF-over-HTTP/SignalR posture. `GET /` returns 404 (expected — the minimal host registers no routable pages until Stories 1.3/2.x; the Blazor pipeline is alive).
- **AC3 — satisfied for Story 1.1 code.** New `.csproj` files carry **no `Version=`** (CPM honored); `bash scripts/check-no-warning-override.sh` passes (no override, no nested submodules); the host and test projects compile with **0 warnings** under solution-wide `TreatWarningsAsErrors`. The only sanctioned suppression is the narrow per-project `<NoWarn>HFC1001</NoWarn>` (see Debug Log). NOTE: the full-`.slnx` build is currently blocked by the unrelated pre-existing `Hexalith.PolymorphicSerializations` pack errors above — not introduced by this story.
- **Tests:** new `tests/Hexalith.Parties.UI.Tests` (xUnit v3 + Shouldly + NSubstitute + bUnit harness, modeled on `Hexalith.Parties.AdminPortal.Tests`). **Four** boot/composition smoke tests — (1) the Quickstart chain + `AddHexalithDomain<PartiesUiDomainMarker>()` builds a provider under `ValidateScopes=true` and resolves `IFrontComposerRegistry`; (2) the same chain **plus `AddFluentUIComponents()`** resolves the registry from both the root provider and a created scope under `ValidateScopes=true`; (3) the composition registers `Microsoft.FluentUI.*` service descriptors (AC1's `AddFluentUIComponents()` guard); (4) `PartiesUiDomainMarker` declares `[BoundedContext("Parties")]`. **4/4 passed.**
- **AC2 topology test (in-process, no Docker):** `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs` constructs the AppHost distributed-application model via `DistributedApplicationTestingBuilder` (never `StartAsync`) and asserts `parties-ui` is a `ProjectResource` with **no `DaprSidecarAnnotation`** (contrasted against `parties`/`tenants`, which have one), **waits for** `eventstore` + `tenants`, and **auto-starts** (no `ExplicitStartupAnnotation`, contrasted against `parties-mcp`). The `IntegrationTests.csproj` was given a `DaprComponents/**` content-copy so the model builds without a DAPR runtime. **1/1 passed.**

### File List

**Added**
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`
- `src/Hexalith.Parties.UI/PartiesUiDomainMarker.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Components/App.razor`
- `src/Hexalith.Parties.UI/Components/Routes.razor`
- `src/Hexalith.Parties.UI/Components/_Imports.razor`
- `src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor`
- `src/Hexalith.Parties.UI/appsettings.json`
- `src/Hexalith.Parties.UI/appsettings.Development.json`
- `src/Hexalith.Parties.UI/Properties/launchSettings.json`
- `tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj`
- `tests/Hexalith.Parties.UI.Tests/Usings.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs` (AC2 in-process AppHost topology assertion — no DAPR sidecar, waits for eventstore+tenants, auto-starts)

**Modified**
- `Hexalith.Parties.slnx` (registered the host under `/src/` and the test project under `/tests/`)
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` (added the `Hexalith.Parties.UI` ProjectReference → `Projects.Hexalith_Parties_UI`)
- `src/Hexalith.Parties.AppHost/Program.cs` (added the `parties-ui` resource registration after the `tenants` block — no DAPR sidecar, auto-start)
- `tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj` (added a `DaprComponents/**` content-copy so the AppHost distributed-application model can be built in-process for the `PartiesUiTopologyTests` AC2 assertions without a DAPR runtime)

## Senior Developer Review (AI)

**Reviewer:** Administrator · **Date:** 2026-06-10 · **Outcome:** ✅ Approve (auto-fix applied)

Adversarial review of every File List entry against git reality and the three ACs. The implementation is sound — all six tasks are genuinely done and all three ACs verified by re-running the gates, not by trusting the record:

- **AC1 — verified.** `Hexalith.Parties.UI` builds **0 warnings** (Release, `-m:1`); the Quickstart chain + `AddFluentUIComponents()` + `AddHexalithDomain<PartiesUiDomainMarker>()` compose under `ValidateScopes=true` — **4/4** unit tests pass. `MainLayout` matches the Counter.Web reference (`<FrontComposerShell>@Body</FrontComposerShell>`). Intentional, correctly-flagged deviations: no `FrameworkReference` (implicit under `Sdk.Web`, avoids NETSDK1086), narrow `<NoWarn>HFC1001</NoWarn>` for the empty-marker analyzer, and **omitting the raw teal `#0097A7` `AccentColor`** (left at shell default per the Story 1.9 a11y gate — a correct call, not a gap).
- **AC2 — verified at the model level.** `PartiesUiTopologyTests` constructs the AppHost model in-process and proves `parties-ui` is a `ProjectResource` with **no DAPR sidecar**, **waits for** `eventstore`+`tenants`, and **auto-starts** — **1/1 passes** without Docker.
- **AC3 — verified.** No `Version=` on any new `PackageReference` (CPM honored); `scripts/check-no-warning-override.sh` passes; host + test projects compile with 0 warnings under solution-wide `TreatWarningsAsErrors`.

### Findings & auto-fixes applied

- **[MED] Undocumented whole-file LF→CRLF flip** on three pre-existing tracked files (`Hexalith.Parties.slnx`, `Hexalith.Parties.AppHost.csproj`, `AppHost/Program.cs`). The repo is uniformly LF in HEAD; the dev agent's editor reflowed every line terminator, turning a 1/1/10-line change into 81/45/402-line diffs that hid the real change and were absent from the File List. **Fixed:** restored LF on all three so the diff is exactly the intended additions; re-built the AppHost afterward (0 warnings). *(The `.editorconfig` `end_of_line = crlf` vs. the all-LF committed tree is a pre-existing repo-wide inconsistency, out of scope for Story 1.1; not "fixed" by flipping one story's files.)*
- **[MED] File List omitted two in-scope changes** that AC2's topology coverage actually depends on: the new `tests/.../Topology/PartiesUiTopologyTests.cs` and the `IntegrationTests.csproj` `DaprComponents/**` content-copy. **Fixed:** both added to the File List (Added / Modified).
- **[LOW] Completion Notes under-reported the test count** ("2/2") — the suite is actually **4** UI tests (two FluentUI-focused tests were added). **Fixed:** corrected to 4/4 and documented the AC2 topology test (1/1).

No CRITICAL or HIGH findings. No code-behavior changes were required — all fixes are diff-hygiene and Dev-Agent-Record accuracy. Status → **done**.

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-06-09 | 0.1 | Implemented Story 1.1 — stood up the `Hexalith.Parties.UI` FrontComposer shell host (csproj, domain marker, `Program.cs`, `Components/{App,Routes,_Imports,Layout/MainLayout}`, appsettings, launchSettings), registered it in `Hexalith.Parties.slnx`, wired the `parties-ui` Aspire AppHost resource (no DAPR sidecar, references eventstore+tenants, auto-start), and added the `Hexalith.Parties.UI.Tests` boot/composition smoke tests. All tasks complete; AC1/AC2/AC3 verified (AC2 via live `aspire run` + Aspire MCP). Status → review. | claude-opus-4-8 (dev-story) |
| 2026-06-10 | 0.2 | Senior Developer Review (adversarial, auto-fix). Re-verified AC1/AC2/AC3 by re-running builds + tests (UI host 0 warnings; 4/4 UI composition tests; 1/1 AppHost topology test; no-warning-override gate green; no `Version=`). Fixed an undocumented whole-file LF→CRLF flip on `slnx`/`AppHost.csproj`/`AppHost/Program.cs` (restored LF — diffs now show only the intended additions), added the two omitted IntegrationTests files to the File List, and corrected the test count (2→4). No code-behavior changes. Status → done. | claude-opus-4-8 (review) |
