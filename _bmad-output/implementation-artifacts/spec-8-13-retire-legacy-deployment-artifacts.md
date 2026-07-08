# Spec 8.13: Retire Legacy In-Repo Deployment Artifacts

Status: done
Owner: Amelia (Developer)
Created: 2026-07-08
Source change proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-08-retire-legacy-deployment-artifacts.md`

## Intent

Remove retired deployment artifacts that no longer match the current operating model.

## Scope

- Delete the historical `deploy/` tree.
- Delete the retired static deployment-validation test project and fixtures.
- Add `Hexalith.Parties.Ci.Tests` for GitHub Actions/Zot publication contract checks.
- Replace deployment-validation test lane references with a `ci` lane.
- Update active docs to state that runtime deployment orchestration is external.
- Keep local Aspire/Dapr development components under `src/Hexalith.Parties.AppHost/DaprComponents/`.

## Non-Goals

- No runtime deployment orchestrator in this repository.
- No replacement Kubernetes, Dapr, or Zot manifests in this repository.
- No deletion or rewrite of historical BMAD story evidence.

## Validation

- `rg` active-file sweep for retired deployment paths returns no matches.
- `dotnet test tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Release`.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -Configuration Release`.
- `pwsh -NoProfile -File scripts/publish-parties-containers.ps1 -DryRun`.
- Registry API-key prefix scan returns no matches.
