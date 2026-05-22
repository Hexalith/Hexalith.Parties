# Solution Build Gate (Story 9.8)

## Policy

The solution-level build must remain green on a fresh clone with root-level submodules only. Specifically:

```
git clone <repo> /path/to/clean
cd /path/to/clean
git submodule update --init            # root-level only - no --recursive
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release
```

All four steps must exit zero with **no** `-p:TreatWarningsAsErrors=false` or `-p:WarningsAsErrors=` overrides at the command line.

## CI enforcement

`.github/workflows/test.yml::lint` runs the build sequence on every PR and push to `main` / `develop`:

1. Checkout with `submodules: true` (root-level only, not recursive).
2. `dotnet restore Hexalith.Parties.slnx`.
3. `dotnet build "$SOLUTION_FILE" --configuration Release --no-restore`.
4. `bash scripts/check-no-warning-override.sh` (the regression guard).

`scripts/check-no-warning-override.sh` greps active CI/build scripts (`.yml`, `.yaml`, `.ps1`, `.sh`, `.cmd`, `.bat`) outside `_bmad-output/` and `docs/` for the override patterns. Only the guard script itself is excluded by basename; the rest of `scripts/` is scanned so a future helper script in `scripts/` that re-introduces the override is still caught. If a match is found, the lint job fails with a clear message and the entire workflow fails (downstream `test`, `contract-test`, and `Quality Gate` jobs depend on `lint`).

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
- Recursive submodule build (intentionally excluded; root-level submodules only per Story 3.6).

## Related artifacts

- Story 9.8: `_bmad-output/implementation-artifacts/9-8-solution-build-green-on-clean-clone.md` (originally drafted as Story 9.5; renumbered to 9.8 during the 2026-05-22 rebase to avoid a slug collision with Epic 9 v2 Story 9.5 — operator-scripts-publish-and-teardown)
- Epic 3 retrospective (action B8 / TD-3-A): `_bmad-output/implementation-artifacts/epic-3-retro-2026-05-22.md`
- Epic 4 retrospective (action D16 / TD-4-B): `_bmad-output/implementation-artifacts/epic-4-retro-2026-05-22.md`
- Epic 5 retrospective (action B13 / TD-5-A): `_bmad-output/implementation-artifacts/epic-5-retro-2026-05-22.md`
- General CI documentation: `docs/ci.md`
