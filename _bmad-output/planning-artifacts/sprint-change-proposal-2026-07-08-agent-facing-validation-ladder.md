---
title: Sprint Change Proposal - Agent-Facing Validation Fallback Ladder
date: 2026-07-08
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: minor
status: approved
implementation_status: implemented
approved: 2026-07-09T13:21:55+02:00
related:
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - docs/development-guide.md
  - docs/index.md
  - docs/ci.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-validation-ladder-runner.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
---

# Sprint Change Proposal - Agent-Facing Validation Fallback Ladder

## 1. Issue Summary

The root repository already documents the validation fallback ladder in
`docs/development-guide.md` and links it from `docs/index.md`. The remaining gap was
agent-facing: `AGENTS.md` requires agents to read
`references/Hexalith.AI.Tools/hexalith-llm-instructions.md`, but that shared
instruction file only said to run projects individually and did not name the
standard fallback ladder.

This can lead future validation records to mark work blocked when focused evidence
is still available.

## 2. Impact Analysis

- **Epic impact:** supports Epic 8 validation hygiene and NFR9. No epic scope or
  sequencing changes.
- **Story impact:** reinforces the completed Story 8.11 runner/guidance work. No
  story status changes.
- **PRD impact:** none. This is maintenance/tooling guidance only and adds no PRD
  functional coverage.
- **Architecture impact:** none. No runtime or domain boundary changes.
- **UX impact:** none.
- **Technical impact:** documentation-only change in the root-declared
  `Hexalith.AI.Tools` submodule instruction file.

## 3. Recommended Approach

Use **Direct Adjustment**. The root docs stay the detailed source; the shared
instruction file gains a concise ladder so every agent sees the fallback policy
before touching this repo.

Rejected alternatives:

- Reopen Story 8.11: unnecessary because runner support and root docs already
  exist.
- Change build/test scripts: unnecessary for this request.
- Modify PRD/epics: unnecessary because the change is not product scope.

## 4. Detailed Change Proposal

### `references/Hexalith.AI.Tools/hexalith-llm-instructions.md`

**OLD:** Testing standards required individual test projects and no solution-level
`dotnet test`, but omitted the fallback ladder.

**NEW:** Testing standards now require agents to climb the fallback ladder before
recording validation as blocked:

1. Run the most focused lane or test project build/run available.
2. Build the target xUnit v3 test project and invoke the built assembly directly
   with single-dash `-class` or `-method` filters instead of relying on
   `dotnet test --filter` under Microsoft.Testing.Platform.
3. Use serialized `-m:1` Release builds for first-failure triage, with only
   environment pins such as `NuGetAudit=false` and `MinVerVersionOverride=1.0.0`
   when needed.
4. Record the broad-gate blocker separately from focused evidence, without
   weakening the build gate.

## 5. Implementation Handoff

**Scope:** Minor. Implemented directly as a documentation/instruction update.

**Success criteria:**

- Future agents reading the required Hexalith instruction file see the fallback
  validation ladder before working in this repo.
- The ladder explicitly includes focused project/lane validation, direct xUnit v3
  assembly execution, serialized `-m:1` builds, and exact blocker recording.
- No PRD, epic, architecture, UX, runtime, or test-runner behavior changes.

## Checklist Summary

- [x] Trigger understood: agent-facing ladder guidance missing from the required
  shared instruction file.
- [x] PRD impact assessed: none.
- [x] Epic/story impact assessed: supports completed Story 8.11; no status change.
- [x] Architecture and UX impact assessed: none.
- [x] Direct adjustment selected.
- [x] Documentation updated.
