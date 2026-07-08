# Deployment Boundary

Hexalith.Parties no longer keeps runtime deployment manifests or cluster apply scripts in this repository.

This repository owns:

- Source code for the `parties`, `parties-mcp`, and `parties-ui` workloads.
- Local Aspire/Dapr topology for development in `src/Hexalith.Parties.AppHost/`.
- GitHub Actions publication of Parties-owned container images to Zot.

Runtime deployment orchestration is owned outside this repository. The orchestrator must consume immutable image tags from:

- `registry.hexalith.com/parties`
- `registry.hexalith.com/parties-mcp`
- `registry.hexalith.com/parties-ui`

See [ci.md](ci.md) for the GitHub Actions publish contract, required secrets, and image tag policy.

## Local Development

Use Aspire for local development:

```bash
dotnet aspire run --project src/Hexalith.Parties.AppHost
```

Local Dapr component YAML remains under `src/Hexalith.Parties.AppHost/DaprComponents/` and is scoped to local development only. Do not treat those files as production deployment manifests.

## Runtime Requirements

The external deployment orchestrator is responsible for:

- Selecting an immutable image tag published by GitHub Actions.
- Providing registry pull credentials for Zot.
- Supplying environment-specific Dapr components, subscriptions, resiliency policy, access control, ingress, secrets, and platform dependencies.
- Validating image signatures, vulnerability scan policy, and promotion gates.

The historical in-repo `deploy/` Kubernetes, Dapr, and Zot manifests were retired after container publication moved to GitHub Actions.
