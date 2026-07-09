# Solution Build Gate (Story 9.8)

## Policy

The solution-level build must remain green on a fresh clone with root-repository submodules under `references/` only. Specifically:

```
git clone <repo> /path/to/clean
cd /path/to/clean
git submodule update --init            # root-repository submodules only - no --recursive
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release
```

All four steps must exit zero with **no** command-line warnings-as-errors override and **no** nested-submodule initialization. The gate blocks:

- `-p:TreatWarningsAsErrors=false` (and MSBuild-equivalent `/p:`, `-property:`, `--property` forms; matched case-insensitively).
- `-p:WarningsAsErrors=` with an *empty* value (the full disable). The narrowing form `-p:WarningsAsErrors=<RuleId>` (which adds a single ID to the warnings-as-errors set) is intentionally allowed as a recommended escape valve.
- `git submodule update ... --recursive` and `submodules: recursive` in GitHub Actions checkout — root-repository submodules under `references/` only is the default per Story 3.6.

## CI enforcement

`.github/workflows/ci.yml` delegates to the shared Hexalith domain CI workflow, which runs the following steps on every PR and push to `main`:

1. Checkout with root-repository submodules only, not recursive.
2. Setup .NET SDK via `actions/setup-dotnet`.
3. Cache NuGet packages keyed by `global.json`, `Directory.Packages.props`, `Directory.Build.props`, and every `*.csproj`.
4. `dotnet restore Hexalith.Parties.slnx`.
5. `dotnet build "$SOLUTION_FILE" --configuration Release --no-restore`.
6. Package consumer validation and the configured test tiers.

`scripts/check-no-warning-override.sh` greps active CI/build scripts (`.yml`, `.yaml`, `.ps1`, `.sh`, `.cmd`, `.bat`) outside `_bmad-output/`, `docs/`, `node_modules/`, `bin/`, and `obj/` for the override and nested-submodule patterns. Only the guard script itself is excluded by basename; the rest of `scripts/` is scanned so a future helper script that re-introduces a regression is still caught. The guard requires a git working tree (it refuses to run against an extracted tarball) and surfaces real grep errors (exit >= 2) instead of swallowing them. If any match is found, the shared CI job fails with a clear message.

## Why this gate exists

Prior to Story 9.8, every focused `dotnet test` invocation in Epic 3, 4, 5, 6, and 7 stories carried a `-p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` override footnote. Across approximately 44 stories, the accumulated workaround meant CI could not act as a quality signal for cross-project integration work. Story 9.8 verified that the underlying warning promotions had been silently resolved by intervening submodule pointer bumps (commits `d986321`, `389c23f`), and this gate locks the green baseline.

The Epic 5 retrospective (action B13, 2026-05-22) escalated Story 9.8 to a program-level critical-path gate that must close before Epic 7 and Epic 8 active work resumes.

## What to do if the gate fails

### Case 1: the solution build itself fails

A warning has been promoted to an error. Fix the underlying warning. Do **not** re-add the override at the command line.

If you must suppress, suppress at the narrowest possible scope:

- A specific rule ID in a specific project file: `<NoWarn>CA2007;xUnit1051</NoWarn>` in that csproj's PropertyGroup.
- An issue link or recorded reason must accompany the suppression.

### Case 2: the regression guard fails

`scripts/check-no-warning-override.sh` found an `-p:TreatWarningsAsErrors=false` or `-p:WarningsAsErrors=` reference in an active CI or build script. Remove the override. If the build genuinely fails without it, treat that as Case 1 above.

### Case 3: a submodule update breaks the build

A submodule pointer bump may re-introduce a warning class:

1. **Preferred**: bump the submodule to a fixed upstream commit, or land the upstream fix in the submodule and then bump.
2. **Acceptable**: add a narrow `<NoWarn>RuleId</NoWarn>` in the consuming project with an issue link tracking the upstream fix.
3. **Not acceptable**: re-add the command-line override.

### Case 4: the nested-submodule guard fails

`scripts/check-no-warning-override.sh` found a `git submodule update --recursive` or `submodules: recursive` reference in an active CI or build script. Story 9.8 AC5 requires the default build path to use root-repository submodules under `references/` only (per Story 3.6). Remove `--recursive` and set the checkout `submodules:` option to `true` (root-repository submodules only). If a project genuinely needs nested submodules, make it opt-in following the `EnableMemoriesSearch=true` pattern (see `_bmad-output/implementation-artifacts/3-6-enable-one-command-local-run.md`).

## Local parity

Run the same sequence locally before pushing:

```powershell
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore
bash scripts/check-no-warning-override.sh
```

The regression guard script is bash; on Windows use Git Bash, WSL, or invoke under PowerShell with `bash scripts/check-no-warning-override.sh`.

## Non-goals

The gate does **not** enforce:

- Full test suite passing (covered by the `test` matrix and `Quality Gate` jobs in the same workflow).
- `dotnet format --verify-no-changes` (deferred per `docs/ci.md`).
- Recursive submodule build (intentionally excluded; root-repository submodules under `references/` only per Story 3.6).

## Related artifacts

- Story 9.8: `_bmad-output/implementation-artifacts/9-8-solution-build-green-on-clean-clone.md` (originally drafted as Story 9.5; renumbered to 9.8 during the 2026-05-22 rebase to avoid a slug collision with Epic 9 v2 Story 9.5 — operator-scripts-publish-and-teardown)
- Epic 3 retrospective (action B8 / TD-3-A): `_bmad-output/implementation-artifacts/epic-3-retro-2026-05-22.md`
- Epic 4 retrospective (action D16 / TD-4-B): `_bmad-output/implementation-artifacts/epic-4-retro-2026-05-22.md`
- Epic 5 retrospective (action B13 / TD-5-A): `_bmad-output/implementation-artifacts/epic-5-retro-2026-05-22.md`
- General CI documentation: `docs/ci.md`
