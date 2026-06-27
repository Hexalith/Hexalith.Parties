---
project_name: parties
user_name: Administrator
date: 2026-06-27
workflow: bmad-correct-course
change_scope: moderate
status: implemented
mode: batch
requirements_basis: "Brownfield architecture + epics; no formal PRD exists for Parties."
---

# Sprint Change Proposal - Submodules Under References

## 1. Issue Summary

The repository had all root-declared Git submodules checked out as top-level `Hexalith.*` directories. Administrator requested that every submodule at the repository root be moved under `references/`, and that Visual Studio solutions, MSBuild projects, documentation, and Codex/Claude/Copilot-facing instructions be updated to match the new layout.

Evidence:

- `.gitmodules` declared eight root submodules at top-level paths.
- `Hexalith.Parties.slnx`, `Directory.Build.props`, AppHost project validation, package tests, README/docs, and BMAD artifacts referenced the old root paths.
- There is no formal PRD in `_bmad-output/planning-artifacts`; `epics.md` states the brownfield requirements basis is architecture + UX + docs.

## 2. Impact Analysis

Epic impact: no product epic scope changes. The change affects NFR/build-gate guidance and implementation story references that describe submodule locations.

Story impact: historical implementation artifacts and tests that assert onboarding/package paths needed path corrections. No new functional story is required.

Artifact conflicts:

- `.gitmodules` and Git index paths had to move every submodule to `references/Hexalith.*`.
- `Hexalith.Parties.slnx` project and folder paths had to use `references/...`.
- MSBuild root properties and AppHost base-path validation had to resolve `references/...`.
- Package tests had hard-coded `Hexalith.Commons` and `Hexalith.EventStore` paths.
- Agent instructions had to point to `references/Hexalith.AI.Tools/...`.
- Documentation and BMAD output had stale root/sibling checkout wording.

Technical impact: build/project resolution remains project-reference based. The move must not initialize nested submodules or reset existing submodule checkouts.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Rationale: this is a repository layout correction, not a product scope change. It can be implemented by moving Git submodule paths and updating affected text/project references. Rollback and MVP review are not warranted.

Effort: medium. Risk: medium, because Visual Studio solution paths, MSBuild root probing, package tests, and docs all needed coordinated updates.

## 4. Detailed Change Proposals

Submodule layout:

- OLD: `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, `Hexalith.Memories`, `Hexalith.Commons`, `Hexalith.Builds`, `Hexalith.AI.Tools`, `Hexalith.PolymorphicSerializations`
- NEW: `references/Hexalith.EventStore`, `references/Hexalith.Tenants`, `references/Hexalith.FrontComposer`, `references/Hexalith.Memories`, `references/Hexalith.Commons`, `references/Hexalith.Builds`, `references/Hexalith.AI.Tools`, `references/Hexalith.PolymorphicSerializations`

Build and solution:

- `Directory.Build.props` now probes `references\Hexalith.*`.
- `Hexalith.Parties.slnx` now includes submodule projects from `references/...`.
- `Hexalith.Parties.AppHost.csproj` validates `references\Hexalith.EventStore\src` and `references\Hexalith.Tenants\src`.
- AppHost optional project lookup now resolves optional submodule projects from `references/...`.

Instructions and docs:

- `AGENTS.md` and `CLAUDE.md` now load shared Hexalith instructions from `references/Hexalith.AI.Tools`.
- README, active docs, project context, planning artifacts, and implementation artifacts now describe repository-level submodules under `references/`.

Tests:

- Package probes in Client and Contracts tests now pack dependencies from `references/Hexalith.Commons` and `references/Hexalith.EventStore`.
- AppHost and onboarding fitness assertions now expect `references/Hexalith.EventStore references/Hexalith.Tenants`.

## 5. Implementation Handoff

Scope classification: Moderate, implemented directly by Developer.

Responsibilities completed:

- Move submodule paths without recursive initialization.
- Update solution, project, docs, agent instructions, package tests, and tracked path caches.
- Run static and targeted validation.

Success criteria:

- No top-level `Hexalith.*` directories remain.
- `.gitmodules` paths all begin with `references/`.
- MSBuild and solution paths resolve moved submodules.
- Docs and agent instructions no longer instruct root-directory submodule paths.
- Focused guardrail tests pass.

## Checklist Summary

- [x] Trigger and context understood: repository submodule layout correction.
- [x] Epic impact assessed: no product epic scope change.
- [x] Artifact impact assessed: solution, MSBuild, docs, instructions, tests.
- [x] Path forward selected: Direct Adjustment.
- [x] Proposal and implementation handoff produced.
- [N/A] PRD update: no formal PRD exists for this brownfield repository.
- [N/A] UX update: no UI/UX behavior changed.
