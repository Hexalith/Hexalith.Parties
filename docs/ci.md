# CI Pipeline

Hexalith.Parties uses GitHub Actions for the main quality pipeline in `.github/workflows/test.yml`.

## Triggers

- Pull requests targeting `main` or `develop`.
- Pushes to `main` or `develop`.
- Pact Broker `repository_dispatch` events with type `contract_requiring_verification_published`.

## Jobs

- `lint`: checks out root-level submodules, restores the solution, and runs a Release build with analyzers.
- `test`: runs the .NET test projects in four matrix shards with `fail-fast: false`.
- `contract-test`: runs Pact.js contract scripts when they exist. Until the Pact framework is scaffolded, the job records a readiness gap in the GitHub step summary.
- `Quality Gate`: fails the workflow unless lint/build and test shards pass, and contract tests either pass or are intentionally skipped.

## Submodules

The checkout step uses `submodules: true`, which initializes root-level submodules only. Do not change this to recursive checkout unless nested submodules are explicitly required.

## Artifacts

Each test shard uploads `TestResults/` with TRX logs and XPlat Code Coverage output. Artifacts are retained for 30 days.

## Local Parity

Use these commands before pushing CI changes:

```powershell
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore
dotnet test Hexalith.Parties.slnx --configuration Release
```

CI installs the latest .NET 10 SDK with `actions/setup-dotnet` using `10.0.x`. The repository `global.json` is pinned to the current latest SDK feature band and allows latest-feature roll-forward.

`dotnet format --verify-no-changes` is not part of this initial gate because the current repository baseline has pre-existing whitespace drift in sample and test files.

## Pact Readiness

The TEA configuration enables Pact.js utility guidance, but this repository does not currently expose the root package scripts expected by the contract stage. To make the gate enforce contracts, scaffold the Pact framework and add:

- `test:pact:consumer`
- `publish:pact`
- `test:pact:provider:remote:contract`
- `can:i:deploy:provider`

When enabled, configure the secrets listed in `docs/ci-secrets-checklist.md`.
