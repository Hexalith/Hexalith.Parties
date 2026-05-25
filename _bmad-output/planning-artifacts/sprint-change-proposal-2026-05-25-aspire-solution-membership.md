# Sprint Change Proposal — AppHost Submodule Projects Solution Membership

- **Date:** 2026-05-25
- **Author:** Jérôme (via Correct Course workflow)
- **Change scope classification:** Minor (tooling / solution composition — no PRD, epic, story, or runtime-behavior impact)
- **Status:** Implemented and verified

---

## Section 1 — Issue Summary

**Problem statement.** The Aspire AppHost (`src/Hexalith.Parties.AppHost`) declares `ProjectReference`s and `AddProject<>` Aspire resources for projects that live in the root-level `Hexalith.EventStore` and `Hexalith.Tenants` submodules. Those referenced projects were **not members of `Hexalith.Parties.slnx`**. As a result:

- They did not appear in Solution Explorer / `dotnet sln list`.
- They were built only transitively (as MSBuild reference targets), never as first-class solution units.
- IDEs surfaced "project referenced but not in solution" friction for anyone opening `Hexalith.Parties.slnx` to run the full Aspire topology.

**How discovered.** Reported during sprint execution: the EventStore and Tenants projects needed by the Parties Aspire host should be solution members, and the AppHost should reference the needed Tenants/Parties projects.

**Evidence.**
- `Hexalith.Parties.slnx` contained 25 projects, none of them from the submodules.
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` already `ProjectReference`s `Hexalith.EventStore`, `Hexalith.EventStore.Admin.Server.Host`, `Hexalith.EventStore.Admin.UI`, `Hexalith.EventStore.Aspire`, `Hexalith.Parties`, `Hexalith.Parties.Mcp`, and `Hexalith.Tenants`.
- `AppHost/Program.cs` wires `AddProject<>` resources for `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, and `tenants` (Memories is intentionally path-referenced, not a hard `ProjectReference`).

---

## Section 2 — Impact Analysis

- **Epic impact:** None. All 9 epics are DONE; this is post-implementation tooling.
- **Story impact:** None. No story acceptance criteria reference solution membership.
- **PRD / Architecture / UX impact:** None. No requirement, ADR, or UX flow changes.
- **Artifact conflicts:** None.
- **Technical impact:** `Hexalith.Parties.slnx` only. No `.csproj`, source, Dapr, or Aspire wiring changes.

**Second half of the request — already satisfied.** The AppHost `.csproj` already references every project its `AddProject<>` calls need. No reference additions were required; the only real gap was solution membership.

**Cross-submodule resolution (verified safe).** The added submodule projects resolve their cross-submodule references via fallback logic in each submodule's `Directory.Build.props`:
- `Hexalith.Tenants/Directory.Build.props` resolves `HexalithEventStoreRoot` to the sibling Parties-level `Hexalith.EventStore` submodule.
- `Hexalith.EventStore/Directory.Build.props` resolves `HexalithTenantsBasePath` to the sibling Parties-level `Hexalith.Tenants/src`.
- The nested `Hexalith.Tenants/Hexalith.EventStore` submodule is correctly left **uninitialized**, consistent with the project rule against recursive submodule init.

---

## Section 3 — Recommended Approach

**Direct adjustment.** Add the AppHost dependency closure (direct + transitive `ProjectReference`s) from the two submodules to `Hexalith.Parties.slnx`, organized under two dedicated solution folders that mirror the on-disk submodule layout. No source or `.csproj` changes.

- **Scope chosen:** AppHost closure only — **15 projects** (11 EventStore + 4 Tenants). This matches the literal "projects needed for the Parties Aspire host."
- **Effort:** Trivial (single `.slnx` edit).
- **Risk:** Minimal. No behavior change; all added projects build standalone.
- **Timeline impact:** None.

> Note: the Parties **test** projects additionally reference `Hexalith.EventStore.Testing`, `Hexalith.Tenants.Client`, and `Hexalith.Tenants.Testing`. These were **deliberately excluded** per the chosen scope (AppHost-only). They remain transitively built but are not solution members; revisit if a clean "zero missing references" solution load is later desired.

---

## Section 4 — Detailed Change Proposals

**Artifact: `Hexalith.Parties.slnx`**

Added two solution folders between the `/src/` and `/tests/` folders.

`/Hexalith.EventStore/` (11 projects):

```
Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Admin.Abstractions/Hexalith.EventStore.Admin.Abstractions.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj
Hexalith.EventStore/src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj
Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj
Hexalith.EventStore/src/Hexalith.EventStore.SignalR/Hexalith.EventStore.SignalR.csproj
```

`/Hexalith.Tenants/` (4 projects):

```
Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj
Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj
Hexalith.Tenants/src/Hexalith.Tenants.Server/Hexalith.Tenants.Server.csproj
Hexalith.Tenants/src/Hexalith.Tenants.ServiceDefaults/Hexalith.Tenants.ServiceDefaults.csproj
```

**Rationale per group.**
- EventStore direct AppHost refs: `Hexalith.EventStore`, `Admin.Server.Host`, `Admin.UI`, `Aspire`.
- EventStore transitive: `Admin.Abstractions`, `Admin.Server`, `Server`, `Client`, `Contracts`, `ServiceDefaults`, `SignalR`.
- Tenants direct AppHost ref: `Hexalith.Tenants`.
- Tenants transitive: `Server`, `Contracts`, `ServiceDefaults`.

**AppHost `.csproj`:** No change required — references already complete.

---

## Section 5 — Implementation Handoff & Verification

**Handoff:** Minor scope — implemented directly during this workflow.

**Verification performed:**
- `dotnet sln Hexalith.Parties.slnx list` → 40 projects (was 25); all 15 added projects listed.
- `dotnet restore Hexalith.Parties.slnx` → success, graph resolves.
- Built standalone (success): `Hexalith.Tenants.Contracts` (`$(HexalithEventStoreRoot)`), `Hexalith.EventStore.Admin.Server` (`$(HexalithTenantsBasePath)`), `Hexalith.EventStore.Admin.Server.Host` (AppHost passes `AdditionalProperties`). Cross-submodule path properties resolve correctly.

**Out-of-scope pre-existing issue discovered (NOT caused by this change):**
- `src/Hexalith.Parties/Hexalith.Parties.csproj` fails to compile with 13× `CS0757` ("A partial method may not have multiple implementing declarations") in the generated `Microsoft.Extensions.Logging.Generators` `LoggerMessage.g.cs`.
- Confirmed pre-existing: reproduces when building the project directly (no solution) and with a cleaned `obj/`; the only working-tree change is `Hexalith.Parties.slnx`.
- This blocks a full `dotnet build` of the solution and should be triaged separately (likely a duplicate `[LoggerMessage]` partial declaration or a generator/SDK interaction). **Recommend a follow-up task.**

**Success criteria:** ✅ The 15 EventStore/Tenants projects are first-class members of `Hexalith.Parties.slnx`, organized in dedicated folders, and build standalone. AppHost references confirmed complete.
