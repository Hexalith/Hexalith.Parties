# Spec 8.12: Parties-only Zot Container Publish CI

Status: done
Owner: Amelia (Developer)
Created: 2026-07-08
Source change proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-08-parties-only-zot-ci-container-publish.md`

## Intent

Publish only the Parties-owned container images to `registry.hexalith.com` from GitHub Actions using the current Zot Keycloak/OIDC + API-key authentication model.

## Scope

- Add a GitHub Actions workflow for `parties`, `parties-mcp`, and `parties-ui`.
- Add a reusable script that publishes those three projects through the .NET SDK container target.
- Use `ZOT_REGISTRY_USERNAME` and `ZOT_REGISTRY_API_KEY` GitHub secrets.
- Preserve immutable SemVer/MinVer tag policy and reject dirty/build-metadata tags.
- Verify pushed Zot manifests after publish.
- Document the CI secret contract and distinguish this Parties-only publish from external runtime deployment orchestration.

## Non-goals

- No Kubernetes deployment or manifest apply.
- No publication of EventStore, Tenants, Memories, Sample, Redis, or FalkorDB images.
- No `latest` or mutable tag policy.
- No committed API key or operator credential.

## Validation

- Focused CI tests cover workflow, script, and documentation contracts.
- Script dry-run validates repository selection and tag validation without pushing.
- Direct CI test project run passes locally.
- Live registry read checks for existing `0.0.0-preview.0.506` manifests return `200` for `parties`, `parties-mcp`, and `parties-ui`.
- GitHub Actions run `28943231201` published tag `0.0.0-preview.0.662` to Zot on 2026-07-08.
- Live registry manifest checks for `0.0.0-preview.0.662` return `200` for `parties`, `parties-mcp`, and `parties-ui`.
