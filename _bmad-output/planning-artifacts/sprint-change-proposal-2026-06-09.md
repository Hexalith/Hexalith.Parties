---
project_name: parties
user_name: Administrator
date: 2026-06-09
scope_classification: Minor
status: implemented
status_detail: Implemented & build-verified
supersedes: []
relates_to: []
frontmatter_added: 2026-06-21
---

# Sprint Change Proposal — Package Version Update

- **Date:** 2026-06-09
- **Author:** Administrator (via Correct Course workflow)
- **Status:** Implemented & build-verified
- **Scope classification:** **Minor** (direct dependency-manifest change, no story/PRD impact)

---

## Section 1 — Issue Summary

**Trigger:** Routine maintenance request — "update packages to latest release version (or latest preview version if needed)."

**Context:** This repo uses **Central Package Management** (`Directory.Packages.props` is the single source of truth; csprojs carry no `Version=`). There is no PRD/epic/story backlog in `_bmad-output/planning-artifacts`, so this is a pure dependency-manifest update rather than a sprint-story course correction. All 47 pinned packages were checked against the live NuGet feed.

**Finding:** Of 47 packages, only a small set had genuinely newer, in-scope versions. Most are already at their latest stable or latest chosen pre-release; several are intentionally pinned and were deliberately left untouched.

---

## Section 2 — Impact Analysis

- **Epic impact:** None (no epics exist).
- **Story impact:** None.
- **Artifact conflicts:**
  - `Directory.Packages.props` — version edits (below).
  - `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` — `Aspire.AppHost.Sdk` pin must track `Aspire.Hosting` (else DCP fails: `unknown flag: --tls-cert-file`, dashboard hangs).
  - `_bmad-output/project-context.md` — its version table is **stale** (lists Aspire `13.4.0` / SDK `13.3.3 (skew)`; actual was `13.4.2` matched, now `13.4.3` matched). Not rewritten here — see Section 5.
- **Technical impact:** Compile-only (no API surface change). Build re-verified green.

---

## Section 3 — Recommended Approach

**Direct Adjustment** — bump only where a newer in-scope version exists and the project's pinning conventions allow it. Constraints respected:

- **.NET 10 floor held** — `Microsoft.Extensions.*` / `Microsoft.AspNetCore.*` stay on `10.0.8` stable; the `11.0.0-preview` family is a .NET 11 preview gated out by `global.json` (SDK `10.0.300`).
- **Dapr skew preserved** — `1.18.0-rc02` (Actors/Client) vs `1.17.9` (AspNetCore) is intentional; not aligned.
- **Aspire SDK ↔ Hosting kept matched** — both moved to `13.4.3` together.
- **xUnit stays on v3 stable** (`3.2.2`); v4 is a major pre-release, out of scope for "latest release."

**Effort:** trivial. **Risk:** low (patch bump + preview refresh). **Timeline:** done.

---

## Section 4 — Detailed Change Proposals

### Applied — `Directory.Packages.props`

| Package | OLD | NEW | Rationale |
|---|---|---|---|
| Aspire.Hosting | 13.4.2 | **13.4.3** | latest stable patch |
| Aspire.Hosting.Azure.AppContainers | 13.4.2 | **13.4.3** | latest stable patch |
| Aspire.Hosting.Docker | 13.4.2 | **13.4.3** | latest stable patch |
| Aspire.Hosting.Redis | 13.4.2 | **13.4.3** | latest stable patch |
| Aspire.Hosting.Testing | 13.4.2 | **13.4.3** | latest stable patch |
| Aspire.Hosting.Keycloak | 13.4.2-preview.1.26303.6 | **13.4.3-preview.1.26305.13** | preview-only package; tracks the 13.4.3 line |
| Aspire.Hosting.Kubernetes | 13.4.2-preview.1.26303.6 | **13.4.3-preview.1.26305.13** | preview-only package; tracks the 13.4.3 line |
| bunit | 2.8.1-preview | **2.8.4-preview** | latest preview (stable 2.7.2 would be a downgrade) |

### Applied — `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`

```diff
-<Project Sdk="Aspire.AppHost.Sdk/13.4.2">
+<Project Sdk="Aspire.AppHost.Sdk/13.4.3">
```

### Reverted during verification (load-bearing pin)

| Package | Attempted | Reverted to | Reason |
|---|---|---|---|
| Microsoft.Extensions.Hosting.Abstractions | 10.0.8 | **11.0.0-preview.4.26230.115** | Aligning it down to its .NET-10 siblings broke the build with **13× CS8795** — the downgraded transitive `Microsoft.Extensions.Logging.Abstractions` no longer emits the `[LoggerMessage]` source-generated partial implementations in the `Hexalith.Parties` host. The `11.0.0-preview` pin is intentional, not a stray leak. |

### Left unchanged (already latest / intentional)

- `Microsoft.Extensions.*` & `Microsoft.AspNetCore.*` @ `10.0.8` (.NET 10 stable floor).
- Dapr `1.18.0-rc02` / `1.17.9` (intentional skew, on latest rc).
- MinVer `8.0.0-rc.1`, NSubstitute `6.0.0-rc.1`, FluentUI `5.0.0-rc.3-26138.1`, CommunityToolkit.Aspire.Hosting.Dapr `13.4.0-preview` — already on latest pre-release.
- xUnit v3 `3.2.2` (latest stable; v4 is major pre-release).
- OpenTelemetry, MediatR, FluentValidation, ModelContextProtocol, Swashbuckle, Testcontainers, YamlDotNet, Shouldly, coverlet, Microsoft.NET.Test.Sdk — already latest.

---

## Section 5 — Implementation Handoff

- **Scope:** Minor — implemented directly.
- **Verification:** `dotnet restore` (clean) + `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` → **Build succeeded, 0 Warning(s), 0 Error(s)** (TreatWarningsAsErrors is solution-wide, so this is a true-green verdict).
- **Not yet done / follow-ups for owner:**
  1. Optional: run the test lanes (`scripts/test.ps1 -Lane unit`) before merge.
  2. `_bmad-output/project-context.md` version table is stale (predates the props file). Recommend refreshing it (or regenerating via `/bmad-document-project`) — out of scope for this change to avoid scope creep.
  3. Commit on a typed branch with Conventional Commits, e.g. `chore(deps): bump Aspire to 13.4.3 and bunit to 2.8.4-preview`.
- **Success criteria:** clean build with no warning override (✔), Aspire SDK/Hosting matched (✔), .NET 10 floor and Dapr skew preserved (✔).
