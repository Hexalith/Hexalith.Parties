# CI Pipeline

Hexalith.Parties uses GitHub Actions for the main quality pipeline in `.github/workflows/test.yml`.

## Triggers

- Pull requests targeting `main` or `develop`.
- Pushes to `main` or `develop`.
- Pact Broker `repository_dispatch` events with type `contract_requiring_verification_published`.

## Jobs

- `lint`: checks out root-repository submodules under `references/`, restores the solution, runs a Release build with analyzers, and runs the Story 9.8 build-gate regression guard (`scripts/check-no-warning-override.sh`). See [`docs/build-gate.md`](build-gate.md) for the gate policy.
- `test`: runs the .NET test projects in four matrix shards with `fail-fast: false`; each project is executed by path with `dotnet test <projectPath>`. Within a shard the loop continues after a failing project, records a PASS/FAIL row per project in the job step summary, and fails the step at the end if any project failed — so one run reports every failing project instead of stopping at the first blocker.
- `ui-a11y`: builds the UI project and UI bUnit test project sequentially, runs the UI test project by path, then runs the Playwright accessibility workspace.
- `contract-test`: runs Pact.js contract scripts when they exist. Until the Pact framework is scaffolded, the job records a readiness gap in the GitHub step summary.
- `Quality Gate`: fails the workflow unless lint/build, test shards, and UI accessibility pass, and contract tests either pass or are intentionally skipped.

## Parties Container Publish

The `Publish Parties Containers` workflow in `.github/workflows/publish-parties-containers.yml` publishes only the Parties-owned container images to Zot:

- `registry.hexalith.com/parties`
- `registry.hexalith.com/parties-mcp`
- `registry.hexalith.com/parties-ui`

The workflow runs on pushes to `main`, `v*` tags, and manual dispatch. It restores the solution, builds the three container projects, authenticates to `registry.hexalith.com` with `ZOT_REGISTRY_USERNAME` and `ZOT_REGISTRY_API_KEY`, then calls `scripts/publish-parties-containers.ps1`.

The workflow uses immutable SemVer/MinVer image tags only. Git tags may keep their leading `v`, but image tags omit it. Mutable tags such as `latest` are not allowed. The script verifies each pushed manifest through the Zot v2 API after publication.

This workflow does not apply runtime deployment manifests and does not publish EventStore, Tenants, Memories, Sample, Redis, or FalkorDB images. Full runtime deployment orchestration is owned outside this repository and should consume the immutable image tags published to Zot.

Required registry secrets are listed in [`docs/ci-secrets-checklist.md`](ci-secrets-checklist.md). Zot currently authenticates browser users through Keycloak/OIDC and supports Zot API keys for automation; GitHub Actions must use the API key as the password value and must not store a human SSO password.

## Submodules

The checkout step uses `submodules: true`, which initializes root-repository submodules under `references/` only. Do not change this to recursive checkout unless nested submodules are explicitly required.

## Artifacts

Each test shard uploads `TestResults/` with TRX logs and XPlat Code Coverage output. Artifacts are retained for 30 days.

## Local Parity

Use these commands before pushing CI changes:

```powershell
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release
pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release
```

To reproduce the CI shard evidence locally — continue past a failing project and write inspectable TRX + coverage output — add `-ContinueOnFailure -ResultsDirectory TestResults`:

```powershell
pwsh -NoProfile -File scripts/test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults
```

CI installs .NET SDK `10.0.301` with `actions/setup-dotnet`, matching `global.json`.

The `.slnx` is used for restore/build only. Test lanes must run project paths explicitly, matching the local lane runner; do not replace them with solution-level `dotnet test`.

CI and the default local commands run in package mode (`UseNuGetDeps=true`, `UseHexalithProjectReferences=false`). If unpublished Hexalith packages block restore, record the package-mode blocker and rerun source-mode triage with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` only as diagnostic evidence; release/package validation must return to package mode.

For focused xUnit v3 reruns, build the target project and invoke the test executable directly with single-dash filters:

```powershell
dotnet build tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet tests/Hexalith.Parties.Server.Tests/bin/Release/net10.0/Hexalith.Parties.Server.Tests.dll -class Fully.Qualified.TestClass
dotnet tests/Hexalith.Parties.Server.Tests/bin/Release/net10.0/Hexalith.Parties.Server.Tests.dll -method Fully.Qualified.TestClass.TestMethod
```

Package compatibility tests in `Hexalith.Parties.Client.Tests` and `Hexalith.Parties.Contracts.Tests` may contact NuGet repository signature metadata. If the environment blocks `api.nuget.org:443`, record the package lane as blocked and rerun it in a network-enabled package-validation environment before release.

`dotnet format --verify-no-changes` is not part of this initial gate because the current repository baseline has pre-existing whitespace drift in sample and test files.

## Pact Readiness

The TEA configuration enables Pact.js utility guidance, but this repository does not currently expose the root package scripts expected by the contract stage. To make the gate enforce contracts, scaffold the Pact framework and add:

- `test:pact:consumer`
- `publish:pact`
- `test:pact:provider:remote:contract`
- `can:i:deploy:provider`

When enabled, configure the secrets listed in `docs/ci-secrets-checklist.md`.
