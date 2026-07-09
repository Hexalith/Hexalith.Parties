# CI/CD Pipeline

Hexalith.Parties uses GitHub Actions with shared Hexalith.Builds reusable workflows.

## Workflows

- `.github/workflows/ci.yml` delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main`.
- `.github/workflows/release.yml` delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main`.
- `.github/workflows/commitlint.yml` delegates Conventional Commit validation for pull requests.
- `.github/workflows/dependency-review.yml` delegates dependency review for pull requests.
- `.github/workflows/codeql.yml` delegates C# CodeQL scanning.
- `.github/workflows/rc-gate.yml` remains Parties-specific and validates root gitlink release-candidate signoff.

## CI

CI runs on pushes and pull requests to `main`, scheduled nightly runs, and Pact Broker `repository_dispatch` events with type `contract_requiring_verification_published`.

The shared CI workflow restores and builds `Hexalith.Parties.slnx`, initializes only root-declared submodules, runs package consumer validation, and executes the configured test tiers:

- Tier 1: unit, UI, package, and boundary tests.
- Tier 2: integration and CI contract tests.
- Tier 3: Aspire topology tests through `tests/Hexalith.Parties.IntegrationTests`.

Coverage is intentionally not enabled yet in `ci.yml`; the local `coverage` lane is still blocked under the current Microsoft.Testing.Platform/xUnit v3 setup until an MTP-compatible coverage path is configured.

## Release

Release runs on pushes to `main` through semantic-release. The release workflow:

- installs npm dependencies from `package-lock.json`;
- restores and builds `Hexalith.Parties.slnx`;
- runs the configured Tier 1 and Tier 2 test projects;
- packs and validates Parties NuGet packages through `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, and `scripts/validate-consumer-package-references.py`;
- publishes NuGet packages with `NUGET_API_KEY`;
- publishes exactly these Parties-owned containers to Zot through the shared release publisher:
  - `registry.hexalith.com/parties`
  - `registry.hexalith.com/parties-mcp`
  - `registry.hexalith.com/parties-ui`

The release workflow does not apply runtime deployment manifests and does not publish EventStore, Tenants, Memories, Sample, Redis, or FalkorDB images. Runtime deployment orchestration is owned outside this repository and consumes immutable release tags from Zot.

## Local Parity

Use these commands before pushing CI/CD changes:

```powershell
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release
pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release
```

To reproduce local test-lane evidence and continue past a failing project:

```powershell
pwsh -NoProfile -File scripts/test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults
```

CI and default local commands run in package mode (`UseNuGetDeps=true`, `UseHexalithProjectReferences=false`). If unpublished Hexalith packages block restore, record the package-mode blocker and rerun source-mode triage with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` only as diagnostic evidence.

## Secrets

Required release secrets are listed in [ci-secrets-checklist.md](ci-secrets-checklist.md). Zot automation uses `HEXALITH_ZOT_USERNAME` and `HEXALITH_ZOT_API_KEY`; the API key is generated after Zot Keycloak/OIDC login and replaces the password for Docker-compatible clients.

## Pact Readiness

Pact.js scripts are not currently exposed at the repository root. To make Pact contract gates enforceable later, scaffold the Pact framework and add:

- `test:pact:consumer`
- `publish:pact`
- `test:pact:provider:remote:contract`
- `can:i:deploy:provider`
