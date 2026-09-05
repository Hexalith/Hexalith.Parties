---
title: 'Update Hexalith packages, repair CI, and ship a bypass-gated release'
type: 'bugfix'
created: '2026-09-05'
status: 'in-progress'
baseline_commit: 'a7524c5fa59ff4320dbfaa196149713228b80cf7'
review_loop_iteration: 0
context:
  - '{project-root}/docs/ci.md'
  - '{project-root}/references/Hexalith.EventStore/.github/workflows/release.yml'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties is not on the latest published Hexalith catalog (Builds gitlink `0fbbc735` still selects Memories 2.24.1; nuget.org and Builds `origin/main` select 2.25.0). Docs and fitness still pin EventStore 3.95.0 while the catalog is 3.102.0. Push CI on `main` fails at Build (`33969393002`). Release has no EventStore-parity `bypass-validation`, so the requested operator dispatch cannot run.

**Approach:** Adopt the latest published Hexalith versions through a Builds gitlink advance, repair Parties-owned compile and pin drift until push workflows succeed, add a false-default `bypass-validation` input, then dispatch Release with that input and prove the nine NuGet packages exist on nuget.org.

## Boundaries & Constraints

**Always:** Re-query nuget.org and `Hexalith.Builds` `origin/main` at implementation start and adopt any newer published catalog than the planning snapshot (Commons 2.30.0, EventStore 3.102.0, Tenants 5.6.0, FrontComposer 4.3.0, Polymorphic 1.19.2, Memories 2.25.0, Builds `8db7459d065926501ee045b3aaf7b816780905e5`). Consume versions only from the Builds catalog. Keep CI and pack in package mode. Checkout the adopted Builds commit; do not edit submodule source. Record the new gitlink in `.gitlink-signoff.tsv` as `validated-advance` owned by `jpiquot`. Pin `domain-release.yml@SHA` and `builds-execution-sha` to the same 40-hex SHA. Make `bypass-validation` boolean, optional, default `false`; `false` requires successful exact-source `ci.yml`, `true` requires successful exact-source `commitlint.yml`; both require live `main`. Keep the protected `production` environment, nine packages, and three containers. Work on `main`. This intent authorizes commit, push, release dispatch with `-f bypass-validation=true`, and approval of a pending `production` deployment for that run.

**Ask First:** Disabling or weakening `production` environment protection; changing Builds or other submodule source files; publishing if nuget.org already has a version at or above the next semantic-release candidate; advancing source gitlinks past the matching published tags.

**Never:** Set `UseHexalithProjectReferences=true` in CI or release. Recursive/`--remote` submodule updates. `secrets: inherit`, `--skip-duplicate`, or `domain-release.yml@main`. Make bypass the default. Treat bypass as skipping environment approval, package/container validation, or nuget.org proof. Claim success from a frozen/skipped publish (`HEXALITH_RELEASE_PUBLISH_ENABLED` not `true`).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Catalog adopt | Builds gitlink at latest published catalog | Package-mode restore uses nuget.org identities matching the catalog | Missing published package fails restore; do not switch to source mode |
| Stale pins | Docs/tests still say EventStore 3.95.0 | Tests fail until Parties-owned pins match the adopted catalog | Refresh pins; keep historical receipts that are not live assertions |
| CS0619 parallelization | `CollectionBehavior(DisableTestParallelization = true)` | Build fails under current xUnit | Replace with `CollectionDefinition(..., DisableParallelization = true)` plus `[Collection]` |
| Normal release | `bypass-validation=false`, green `ci.yml` on live `main` | `source-ci-workflow=ci.yml`; protected release may proceed | Missing/stale/failed CI rejects before `production` |
| Authorized bypass | `bypass-validation=true`, green `commitlint.yml` on live `main` | `source-ci-workflow=commitlint.yml` crosses caller output into `domain-release.yml` | Missing Commitlint proof rejects; environment and publication checks remain |
| Malformed bypass | Input not exactly `true` or `false` | Preflight fails with no source-workflow output | Reject before secrets or publish |
| NuGet proof | Release completed | All nine `Hexalith.Parties.*` ids on nuget.org are newer than `1.0.0` and match the GitHub Release tag | Missing id, draft release, or frozen publish is failure |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds` -- gitlink today `0fbbc735` (Memories 2.24.1); adopt `origin/main` (snapshot `8db7459`, Memories 2.25.0). Read-only checkout. Catalog: `Props/Directory.Packages.props:6-11`.
- `.gitlink-signoff.tsv:43` -- last Builds authorization `a531665`; add a `validated-advance` line for the adopted SHA.
- `.github/workflows/release.yml:7-8,17-71,255-276` -- dispatch has no inputs; `verify-source` hard-codes `ci.yml`; reusable pin `53d53ae42abf7c87d385a078ab260531480bbf8a`. Copy EventStore caller: `inputs.bypass-validation`, job output `source-ci-workflow`, `source-ci-workflow: ${{ needs.verify-source.outputs.source-ci-workflow }}`. EventStore pattern (read-only): `references/Hexalith.EventStore/.github/workflows/release.yml:7-13,23-95,124`.
- `references/Hexalith.Builds/.github/workflows/domain-release.yml:76` -- already accepts `source-ci-workflow`; do not edit Builds.
- `tests/Hexalith.Parties.Server.Tests/AssemblyInfo.cs:3` and `tests/Hexalith.Parties.IntegrationTests/AssemblyInfo.cs:3` -- obsolete `CollectionBehavior`. Reuse `tests/Hexalith.Parties.Tests/Search/LocalFuzzySearchPerformanceBenchmarkTests.cs:13-24`.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:22-29,174,193,664-685,1345` -- live pins still 3.95.0 / stale SHAs.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:62-65` -- paired live evidence for those fitness assertions.
- `docs/ci.md:71`, `docs/architecture.md:56`, `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs:49-59,212` -- catalog text and frozen Builds SHA `53d53ae…`.
- `tools/release-packages.json` -- nine package ids (read-only unless inventory truly changed).
- `release.config.cjs` -- nuget.org push of `./nupkgs/Hexalith.Parties.*.nupkg` (read-only).

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.Builds`, `.gitlink-signoff.tsv` -- checkout the latest Builds commit whose catalog matches current nuget.org; authorize the gitlink.
- [ ] `tests/Hexalith.Parties.Server.Tests/AssemblyInfo.cs`, `tests/Hexalith.Parties.IntegrationTests/AssemblyInfo.cs` -- replace obsolete assembly `CollectionBehavior` with xUnit v3 `CollectionDefinition` + `[Collection]`.
- [ ] Fitness pins, Story 8.3 matrix, `docs/ci.md`, `docs/architecture.md` -- refresh live EventStore/Commons/Builds identities to the adopted catalog; do not rewrite historical receipts.
- [ ] `.github/workflows/release.yml` -- add EventStore-parity `bypass-validation`; emit and consume `source-ci-workflow`; retarget `uses` and `builds-execution-sha` to the adopted Builds SHA together.
- [ ] `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs` -- assert the new SHA pin, typed bypass input (default false), `false -> ci.yml`, `true -> commitlint.yml`, malformed rejection, and docs catalog text.
- [ ] Local package-mode restore, serialized Release build, focused CI/fitness tests, then `scripts/test.ps1` lanes that CI runs.
- [ ] Commit and push `main` (authorized by this intent). Watch `ci.yml`, `commitlint.yml`, `codeql.yml`, and any other required push workflows until they succeed on that SHA.
- [ ] Dispatch `gh workflow run release.yml --ref main -f bypass-validation=true`. If `production` waits, approve that pending deployment; do not change environment rules. Confirm the GitHub Release tag and all nine nuget.org versions.

**Acceptance Criteria:**
- Given package-mode restore, when MSBuild evaluates Hexalith versions, then they equal the latest published nuget.org identities selected by the adopted Builds catalog, including Memories 2.25.0 or newer.
- Given Server and Integration test projects, when they compile under current xUnit, then they do not use obsolete `CollectionBehavior.DisableTestParallelization`.
- Given `bypass-validation` false or true, when caller and `domain-release.yml` source checks run, then the selected workflow is `ci.yml` or `commitlint.yml` respectively, and non-main, stale, failed, or malformed proof is rejected.
- Given the repair commit on live `main`, when required push workflows finish, then they succeed for that exact SHA.
- Given the bypass dispatch of that SHA, when publication completes, then a non-draft GitHub Release exists for that SHA and nuget.org lists a version newer than `1.0.0` for each of the nine `tools/release-packages.json` ids.

## Spec Change Log

## Verification

**Commands:**
- `git -C references/Hexalith.Builds rev-parse HEAD` and nuget.org flatcontainer indexes -- expected: catalog versions equal latest published Commons/EventStore/Tenants/FrontComposer/Polymorphic/Memories.
- `dotnet restore Hexalith.Parties.slnx` then `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` -- expected: zero CS0619/CS0246 from Hexalith pins or CollectionBehavior.
- Build and invoke `Hexalith.Parties.Ci.Tests.dll` and `Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- expected: pass.
- `gh run list --workflow=ci.yml --branch main --limit 5` then `gh run watch <id> --exit-status` for CI, Commitlint, and CodeQL on the pushed SHA -- expected: success.
- `gh workflow run release.yml --ref main -f bypass-validation=true` then watch the run -- expected: publication success.
- `curl -sL https://api.nuget.org/v3-flatcontainer/hexalith.parties.contracts/index.json` (and the other eight ids) plus `gh release view <tag>` -- expected: versions match the Release tag and exceed `1.0.0`.
