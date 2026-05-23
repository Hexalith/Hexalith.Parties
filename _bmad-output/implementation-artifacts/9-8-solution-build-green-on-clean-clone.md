# Story 9.8: Solution Build Green on Clean Clone Before Epic 7/8 Resumes

Status: done - 2026-05-23 (code-review: 7 patches applied to scripts/check-no-warning-override.sh + docs/build-gate.md; 2 deferred; 8 dismissed)

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->
<!--
  Renumber note (2026-05-22): This story was originally drafted as Story 9.5 against the
  pre-greenfield Epic 9 v1 plan (file slug `9-5-solution-build-green-on-clean-clone`). During
  the rebase of the Epic 3 retrospective follow-through onto Epic 9 v2 (greenfield rewrite,
  2026-05-21), the slug was renumbered to 9-8 to avoid a semantic collision with v2 Story 9.5
  (operator-scripts-publish-and-teardown). All in-repo references in the Epic 3/4/5 retro batch,
  sprint-change-proposal-2026-05-22, build-gate.md, ci.md, test.yml, and the regression guard
  script were updated to "Story 9.8" / `9-8-solution-build-green-on-clean-clone` in the same
  follow-up commit. Other historical artifacts (submodules, deferred-work.md, prior planning
  documents) retain the original 9.5 numbering as historical record.
-->

## Story

As a developer or CI agent preparing to resume Epic 7 (Administration Console) and Epic 8 (Embeddable Party Picker) active work,
I want `dotnet build .\Hexalith.Parties.slnx --configuration Release` to exit zero on a fresh clone with root-level submodules only,
so that cross-project integration stories, CI gates, and the upcoming Epic 7/8 stories do not have to route around a chronically broken solution-level build.

## Acceptance Criteria

1. **Clean-clone solution build succeeds with root-level submodules only.**
   - **Given** a fresh clone of `Hexalith.Parties` with `git submodule update --init` (root-level submodules only — no recursive/nested initialization),
   - **When** `dotnet restore .\Hexalith.Parties.slnx` then `dotnet build .\Hexalith.Parties.slnx --configuration Release` is run from the repository root,
   - **Then** both commands exit zero
   - **And** no project requires `-p:TreatWarningsAsErrors=false`, `-p:WarningsAsErrors=`, or any other warnings-as-errors override at the command line.

2. **Opt-in projects do not break the default build.**
   - **Given** `Hexalith.Memories` is opt-in behind `EnableMemoriesSearch=true` (per Story 3.6),
   - **When** the default solution build runs without `EnableMemoriesSearch=true`,
   - **Then** any project that requires the `Hexalith.Memories/Hexalith.Commons` nested submodule is either excluded from the default solution graph or its build is conditioned on the same opt-in property
   - **And** the absence of nested submodules does not produce build failures.

3. **Submodule analyzer warnings do not promote to errors at the consumer.**
   - **Given** `Hexalith.EventStore` (and any other root-level submodule) is pinned at a commit that currently emits nullable/analyzer warnings,
   - **When** `Hexalith.Parties.slnx` builds projects that reference that submodule,
   - **Then** the consumer build either suppresses the inherited warnings at the consumer boundary (e.g., per-project `<NoWarn>` for the specific submodule-sourced rule IDs), bumps the submodule pointer to a commit that resolves the warnings, or documents the rule IDs as known-deferred with an issue link
   - **And** Story 3.10's `-p:TreatWarningsAsErrors=false` override is no longer required for `Hexalith.Parties.Tests`.

4. **Pre-existing CA2007 and related analyzer debt is closed.**
   - **Given** Story 3.9 resolved CA2007 in `tests/Hexalith.Parties.DeployValidation.Tests/K8sStory93LintTests.cs`,
   - **When** the rest of the solution is audited for the same warnings-as-errors blockers,
   - **Then** any remaining `CA2007` / `xUnit1051` / nullable / dead-code analyzer findings that promote to errors at the default build settings are either fixed or have a documented `<NoWarn>` scope with an issue link in the project file.

5. **CI signal locks the new baseline.**
   - **Given** the solution build is now green on clean clone,
   - **When** a CI workflow or scripted check runs the clean-clone build sequence,
   - **Then** the workflow fails on regression (any future change that re-introduces a warnings-as-errors override at the command line, or that requires nested-submodule initialization, must instead fix the underlying issue)
   - **And** the check is the gating signal that Epic 7 / Epic 8 active work can resume.

## Dev Notes

This story is a preparation-only gate carved out from the Epic 3 retrospective (action B8, 2026-05-22). It addresses a systemic pattern observed across Epic 3: every story routed around a broken solution-level build using focused project-level test commands. Stories 3.1, 3.2, 3.6, 3.9, and 3.10 each had to document workarounds in their Debug Log References or Dev Notes. The accumulated workarounds mean Epic 7 and Epic 8 cannot rely on CI as a quality signal until this is resolved.

Root causes observed during Epic 3:

- `tests/Hexalith.Parties.DeployValidation.Tests/K8sStory93LintTests.cs` had a `CA2007` warnings-as-errors blocker (resolved in Story 3.9).
- The default solution graph evaluated `Hexalith.Memories`, which currently requires a nested `Hexalith.Memories/Hexalith.Commons` submodule. Story 3.6 made Memories opt-in for the AppHost, but the solution graph may still pull it in.
- The current `Hexalith.EventStore` submodule revision emits unrelated nullable/analyzer warnings that the default `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` from `Directory.Build.props` promotes to errors. Story 3.10 worked around this by passing `-p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` at the command line — that workaround is not a fix.

Implementation guidance:

- Do **not** globally disable `TreatWarningsAsErrors`. Project-quality-as-default is a load-bearing repo convention. Suppress at the narrowest possible scope (specific rule IDs, specific project files, or upstream commit bump in the submodule).
- Prefer fixing the underlying warning over suppressing it. Suppressions must come with an issue link or a recorded reason.
- Verify on a fresh clone, not the current working tree: `git clone <repo> /tmp/clean-test && cd /tmp/clean-test && git submodule update --init && dotnet build .\Hexalith.Parties.slnx --configuration Release`.
- The Memories opt-in design from Story 3.6 (`EnableMemoriesSearch=true`) is the reference pattern for conditional project inclusion. Apply the same shape if any other project needs conditional exclusion from the default build.

## Tasks / Subtasks

- [x] Establish the current clean-clone build baseline. (AC: 1)
  - [x] Run `dotnet build .\Hexalith.Parties.slnx --configuration Release` on a fresh clone with `git submodule update --init` (root-level only) and capture the full error/warning list.
  - [x] Categorize each failure: nested-submodule dependency, submodule-inherited analyzer warning, in-repo analyzer warning, or other.
- [x] Resolve nested-submodule dependencies in the default build graph. (AC: 2)
  - [x] Identify any project still pulling `Hexalith.Memories` (or any other nested-submodule-dependent project) into the default solution graph.
  - [x] Either exclude from the default graph, or condition on `EnableMemoriesSearch=true` (following Story 3.6's pattern).
- [x] Resolve submodule-inherited analyzer warnings. (AC: 3)
  - [x] For each warning class, decide between: (a) narrow `<NoWarn>` at the consumer project with an issue link, (b) bump submodule pointer to a fixed upstream commit, or (c) upstream fix in the submodule + pointer bump.
  - [x] Remove the `-p:TreatWarningsAsErrors=false` workaround from any story or doc that relies on it; rerun the story's focused tests to confirm they still pass without the override.
- [x] Resolve in-repo analyzer debt. (AC: 4)
  - [x] Audit the solution for any remaining warnings-as-errors blockers at default settings.
  - [x] Fix at source where practical; suppress narrowly with rationale where not.
- [x] Lock the baseline with a CI check. (AC: 5)
  - [x] Add or update a CI workflow / scripted check that runs the clean-clone build sequence on every PR.
  - [x] Verify the check fails when a contrived regression is introduced (e.g., a test PR that re-adds `-p:TreatWarningsAsErrors=false` to a build script).
  - [x] Document the check in `docs/` so Epic 7/8 story authors can rely on it as the gating signal.

### Review Findings (2026-05-23)

- [x] [Review][Patch] AC5 — Nested-submodule-initialization regression not caught by guard [scripts/check-no-warning-override.sh:36-37] — Spec AC5 demands the gate fail on regressions that "re-introduce a warnings-as-errors override at the command line, **or that requires nested-submodule initialization**." The guard only greps for the override patterns; it does not check for `git submodule update --init --recursive`, `submodules: recursive`, or related forms. Add a second pattern set covering nested-submodule re-initialization in active CI/build scripts. (Source: auditor)
- [x] [Review][Patch] Guard silently passes outside a git repo [scripts/check-no-warning-override.sh:24] — `cd "$(git rev-parse --show-toplevel)"` resolves to `cd ""` outside a repo because `git rev-parse` writes the `fatal:` message to stderr and returns empty stdout. `set -e` does not stop the empty-arg `cd`; the script then scans `$PWD` and reports OK with exit 0. CI is safe (actions/checkout sets up the repo) but `docs/build-gate.md` Local parity section advertises local pre-push use, and any container/tarball invocation will silently pass. Fix: capture the result first and exit ≠ 0 if empty. (Source: blind+edge — Edge Case Hunter empirically reproduced the silent-OK behavior)
- [x] [Review][Patch] `2>/dev/null || true` masks real grep errors (exit ≥ 2) [scripts/check-no-warning-override.sh:38] — The `|| true` is required to suppress grep's exit 1 on zero matches under `set -e`, but it also swallows exit 2+ (unreadable file, broken pipe, invalid regex). Combined with `2>/dev/null`, a permission error or unreadable symlink under a submodule could silently mask a real override hit. Fix: capture exit code separately and tolerate only 0/1. (Source: blind+edge)
- [x] [Review][Patch] Pattern `-p:WarningsAsErrors=` substring-matches legitimate typed opt-ins [scripts/check-no-warning-override.sh:37] — `-p:WarningsAsErrors=CA2007` (Microsoft-recommended narrowing form: add a single ID to WAE) is matched identically to the empty disable form. No current scripts use the typed form, but the "What to do if the gate fails" section recommends narrow suppression, and a contributor using the CLI escape valve gets blocked with no explanation. Fix: anchor the pattern to the empty form only (`-p:WarningsAsErrors=` followed by end-of-line, whitespace, or quote) and/or document the substring trap in `docs/build-gate.md`. (Source: blind+edge)
- [x] [Review][Patch] MSBuild-equivalent forms `/p:`, `-property:`, `--property` not covered [scripts/check-no-warning-override.sh:36-37] — `dotnet build /p:TreatWarningsAsErrors=false`, `-property:TreatWarningsAsErrors=false`, and `--property TreatWarningsAsErrors=false` all bypass the guard. Verified no current uses; the `/p:` form is in active developer vocabulary in submodule story notes and could leak into a future helper script. Fix: extend the regex to accept `[-/]p:` and `[-/]{1,2}property[ :=]`. (Source: edge)
- [x] [Review][Patch] Case-insensitive override variants slip through [scripts/check-no-warning-override.sh:36-37] — `grep` is case-sensitive by default; `-p:treatwarningsaserrors=false` in a `.ps1` (PowerShell is case-insensitive on case-insensitive filesystems) would not be caught. No current variants in repo. Fix: add `-i` to the grep call. (Source: edge)
- [x] [Review][Patch] `docs/build-gate.md` "CI enforcement" lists 4 steps; the lint job has 6 [docs/build-gate.md:19-24 vs .github/workflows/test.yml:30-56] — Doc enumerates Checkout/Restore/Build/Guard but omits Setup .NET (lines 36-39) and Cache NuGet packages (lines 41-47). Either add the missing steps or rephrase to "runs the following enforcement steps in addition to setup and cache". (Source: edge)
- [x] [Review][Defer] `grep -r` does not respect `.gitignore` [scripts/check-no-warning-override.sh:26-38] — deferred, pre-existing behavior choice. Untracked dirs (`.vs/`, `TestResults/`, locally created folders) are scanned, which can yield local false positives that do not reproduce in CI. Migrating to `git grep` would scan tracked files only.
- [x] [Review][Defer] No portability note for BSD grep on macOS contributors [scripts/check-no-warning-override.sh:1 + docs/build-gate.md:67] — deferred, pre-existing scope. `grep --include`/`--exclude-dir` are GNU-grep extensions; macOS system grep is BSD. Docs cover Windows (Git Bash bundles GNU) but not macOS contributors who must `brew install grep`. CI is ubuntu-latest so the gate itself works.

**Review summary (2026-05-23):** 7 `patch`, 2 `defer`, 8 `dismiss` (no `decision-needed`). All three review layers (Blind Hunter, Edge Case Hunter, Acceptance Auditor) returned findings successfully. Spec ACs were cross-checked; AC1/AC2/AC3/AC4 already-satisfied claims confirmed via Dev Notes fresh-clone evidence (excluded from patch list as out of scope for the current diff).

## Dev Agent Record

### Implementation Plan

The story's anticipated scope was substantial (audit, fix, suppress, document multi-category analyzer debt across the consumer + submodule boundary). On execution, AC1–AC4 were found to be **already satisfied** in the working tree: incremental submodule pointer bumps during Epic 3–8 work (commits `d986321 chore: bump Hexalith.EventStore submodule to 697b307` and `389c23f chore: update subproject commit for Hexalith.EventStore`) silently resolved the underlying nullable/analyzer warning promotions that the original retrospectives documented. The actual implementation work narrowed to AC5: lock the green baseline against future regression.

### Validation evidence

**AC1 — Clean-clone Release build with no override:**

Fresh clone to `D:/temp/hexalith-9-5-clean` via `git clone D:/Hexalith.Parties D:/temp/hexalith-9-5-clean`, then `git submodule update --init` (root-level only, no `--recursive`). All 6 root-level submodules initialized at currently-pinned SHAs (`Hexalith.AI.Tools a83ba6e`, `Hexalith.Commons 1379767`, `Hexalith.EventStore 66f917f`, `Hexalith.FrontComposer e436cd0`, `Hexalith.Memories b1ac43c`, `Hexalith.Tenants c28ec14`).

```
dotnet restore Hexalith.Parties.slnx        -> EXIT 0
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore
                                            -> EXIT 0, 0 Warning(s), 0 Error(s), 27.29s
```

Build logs preserved at `_bmad-output/process-notes/story-9-8-baseline/freshclone-restore.log` and `_bmad-output/process-notes/story-9-8-baseline/freshclone-build.log`. 49 projects built. No `-p:TreatWarningsAsErrors=false` or `-p:WarningsAsErrors=` override required.

**AC2 — Nested-submodule dependencies absent from default graph:**

Fresh-clone build log inspected: zero references to `Hexalith.Memories`, `Hexalith.AI.Tools`, or `Hexalith.Commons` project assemblies in the build output. Memories/AI.Tools/Commons remain excluded from the default solution graph per Story 3.6's opt-in pattern. Note: `Hexalith.Commons` has been promoted to a root-level submodule (no longer nested under `Hexalith.Memories`) — the story Dev Notes reference to `Hexalith.Memories/Hexalith.Commons` is stale but does not affect the outcome.

Opt-in path verified: `dotnet build Hexalith.Parties.slnx --configuration Release -p:EnableMemoriesSearch=true` → EXIT 0, 0 warnings, 0 errors, 20.74s. Log preserved at `_bmad-output/process-notes/story-9-8-baseline/build-memories-opt-in.log` (in the temp clone, removed after verification).

**AC3 — Submodule-inherited analyzer warnings:**

Current `Hexalith.EventStore` SHA `66f917fc` emits no warnings at the Hexalith.Parties consumer boundary under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` from `Directory.Build.props`. Same for `Hexalith.Tenants c28ec14`, `Hexalith.FrontComposer e436cd0`. Story 3.10's `-p:TreatWarningsAsErrors=false` override is no longer required for `Hexalith.Parties.Tests` — confirmed by running the Story 5.1 canonical test command without the override:

```
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj \
    --filter "FullyQualifiedName~PartyDomainEventPublicationContractTests" --no-restore
-> EXIT 0, Passed 12, Failed 0
```

**AC4 — In-repo analyzer debt:**

49-project Release build, 0 warnings, 0 errors at default settings. No remaining `CA2007` / `xUnit1051` / nullable / dead-code findings promote to errors. No new `<NoWarn>` scopes required. Story 3.9's `K8sStory93LintTests.cs` fix held.

**AC5 — CI signal locks the new baseline:**

`.github/workflows/test.yml::lint` already runs the clean-clone build sequence on every PR/push to `main`/`develop` (checkout with `submodules: true` → restore → build Release). New step added: `Story 9.8 build-gate (no warning-override regression)` invokes `scripts/check-no-warning-override.sh`, which greps active CI/build scripts (`.yml`, `.yaml`, `.ps1`, `.sh`, `.cmd`, `.bat`) outside `_bmad-output/` and `docs/` for `-p:TreatWarningsAsErrors=false` and `-p:WarningsAsErrors=` patterns. The guard script excludes only itself by basename, not the whole `scripts/` directory, so a future helper in `scripts/` re-introducing the override is still caught.

**Guard verified test-side:**

- Clean tree → `bash scripts/check-no-warning-override.sh` → "OK: no warning-override regressions detected." → EXIT 0.
- Contrived regression at `scripts/contrived-regression.sh` containing `-p:TreatWarningsAsErrors=false` → guard detected it at the exact line, printed `::error::` message with remediation guidance, EXIT 1.
- Regression removed → guard back to EXIT 0.

Workflow YAML syntax validated with `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/test.yml'))"`.

Gate policy documented at `docs/build-gate.md`, linked from `docs/ci.md::lint` job description.

### Completion Notes

- All five ACs satisfied. AC1–AC4 confirmed already-resolved by accumulated submodule pointer bumps from Epic 4–8 work; AC5 implemented as new artifacts (`scripts/check-no-warning-override.sh`, workflow step, `docs/build-gate.md`).
- B13 (Epic 5 retro) success criterion partial: "warning-override footer removed from story-automator templates" — N/A: `.claude/` and `_bmad/` templates carry zero override references; the boilerplate was added ad-hoc per story by the developer, not by the template. Historical story records (~44 stories under `_bmad-output/implementation-artifacts/`) retain the override footer as historical record; these are documentation, not active build inputs, and are explicitly excluded from the regression guard.
- Updated story's "Hexalith.Memories/Hexalith.Commons nested submodule" reference is now stale — Commons was promoted to a root-level submodule sometime before this story executed. Outcome unaffected: Memories/Commons still excluded from the default graph.
- Tech debt closed: TD-3-A (Epic 3), TD-4-B (Epic 4), TD-5-A (Epic 5). Forward unblocking: Epic 7 / Epic 8 active work gate per B13 escalation is now satisfied.
- Deferred decisions retained: full-test-suite gating in CI is still a follow-up (Quality Gate step already requires `test` shards pass, but a single fail-fast lint step + full-suite parallel matrix is the current shape); the `netstandard2.1` multi-targeting decision remains out of scope.

### Debug Log

- Build logs: `_bmad-output/process-notes/story-9-8-baseline/restore.log`, `build.log`, `build-noincremental.log` (showed CS0006 parallel-race during `--no-incremental` rebuild — *not* a quality regression, an MSBuild parallel-build artifact when ref assemblies are deleted), `clean.log`, `build-cold.log`, `test-5-1-no-override.log`, `freshclone-restore.log`, `freshclone-build.log`.
- One contrived regression file (`scripts/contrived-regression.sh`) created and then removed during guard verification. Not in final tree.
- Temp clone path `D:/temp/hexalith-9-5-clean` removed after verification.

## File List

**Created:**

- `scripts/check-no-warning-override.sh` — regression guard script invoked from CI and locally.
- `docs/build-gate.md` — solution build-gate policy documentation.
- `_bmad-output/process-notes/story-9-8-baseline/restore.log` — initial restore evidence.
- `_bmad-output/process-notes/story-9-8-baseline/build.log` — initial Release build evidence (working tree).
- `_bmad-output/process-notes/story-9-8-baseline/build-noincremental.log` — `--no-incremental` parallel-race demonstration (informational).
- `_bmad-output/process-notes/story-9-8-baseline/clean.log` — dotnet clean evidence.
- `_bmad-output/process-notes/story-9-8-baseline/build-cold.log` — cold rebuild from cleaned state evidence.
- `_bmad-output/process-notes/story-9-8-baseline/test-5-1-no-override.log` — Story 5.1 focused test without override (passed).
- `_bmad-output/process-notes/story-9-8-baseline/freshclone-restore.log` — fresh-clone restore (AC1 evidence).
- `_bmad-output/process-notes/story-9-8-baseline/freshclone-build.log` — fresh-clone Release build (AC1 evidence — 0/0).

**Modified:**

- `.github/workflows/test.yml` — added `Story 9.8 build-gate (no warning-override regression)` step to the `lint` job.
- `docs/ci.md` — updated `lint` job description to reference the build-gate guard and link to `docs/build-gate.md`.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `9-8-solution-build-green-on-clean-clone` (originally `9-5-solution-build-green-on-clean-clone` pre-rebase, see renumber note above) status `ready-for-dev` → `in-progress` (B13 escalation) → `review` (this completion).

## Non-Goals

- Do not bump the major version of any package or refactor existing project shapes beyond the minimum required to make the build green.
- Do not change `Directory.Build.props` defaults for `TreatWarningsAsErrors`, `Nullable`, or `<LangVersion>` — those are load-bearing for repo-wide quality.
- Do not retarget any project, change MinVer/package versioning policy, or modify central package management beyond fixing the build.
- Do not initialize nested submodules in the default path; root-level submodules only remains the default per Story 3.6.

## Anti-Patterns

- Globally disabling `TreatWarningsAsErrors` to "make the build green." Suppress at the narrowest scope and document the reason.
- Adding `-p:TreatWarningsAsErrors=false` to scripts, CI, or documentation as a permanent workaround.
- Initializing nested submodules to make the default build pass; if a project needs nested submodules, it should be opt-in.
- Marking the story done based on a working-tree build success rather than a fresh-clone build success.
- Tagging the CI check as advisory; this story's whole point is to gate Epic 7/8 work on a green solution build.

## Deferred Decisions

- Whether to introduce a multi-targeting or `netstandard2.1` story remains out of scope; this story only makes the current `.NET 10` solution build green.
- Whether the CI check should also gate on `dotnet test` for the full test set is a follow-up; this story gates only on `dotnet build`.

## References

- `_bmad-output/implementation-artifacts/epic-3-retro-2026-05-22.md` - Action item B8 and tech debt items TD-3-A, TD-3-G.
- `_bmad-output/implementation-artifacts/3-6-enable-one-command-local-run.md` - `EnableMemoriesSearch=true` opt-in pattern.
- `_bmad-output/implementation-artifacts/3-9-add-deployment-security-validation.md` - CA2007 K8sStory93LintTests fix precedent.
- `_bmad-output/implementation-artifacts/3-10-display-mvp-compliance-warning.md` - `-p:TreatWarningsAsErrors=false` workaround that this story removes.
- `_bmad-output/process-notes/story-creation-lessons.md#L09` - Submodule cleanliness check that the solution-build gate complements.

## Change Log

- 2026-05-22: Story created by Correct Course (action B8 from Epic 3 retrospective). Status `ready-for-dev`. Epic 7 / Epic 8 active work gated on this story reaching `done`.
- 2026-05-22: Status `ready-for-dev` → `in-progress`. Escalated to program-level critical-path gate by Epic 5 retrospective action B13. Epic 7, 8, 9 new story work (other than 9.8 itself) blocked until this closes.
- 2026-05-22: Status `in-progress` → `review`. All 5 ACs satisfied. AC1–AC4 confirmed already-resolved by accumulated submodule bumps (`d986321`, `389c23f`); AC5 implemented as new artifacts: `scripts/check-no-warning-override.sh` regression guard, new step in `.github/workflows/test.yml::lint`, policy doc at `docs/build-gate.md`. Fresh-clone Release build EXIT 0, 0 warnings, 0 errors, 27.29s. Guard verified test-side (clean tree → EXIT 0; contrived regression → EXIT 1; removed → EXIT 0). Closes TD-3-A, TD-4-B, TD-5-A. Epic 7 / 8 active work gate now satisfied pending review.
