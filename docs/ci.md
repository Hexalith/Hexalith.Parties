# CI/CD Pipeline

Hexalith.Parties uses GitHub Actions with shared Hexalith.Builds workflows and actions.

## Workflows

- `.github/workflows/ci.yml` delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main`.
- `.github/workflows/release.yml` is a manual `workflow_dispatch` workflow with a protected, caller-owned release job. It uses `Hexalith/Hexalith.Builds/Github/prepare-domain-release@397c94a4e246c90b21cf408790fa0d55bf32d795` and takes the nested publisher from that same immutable Builds checkout.
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

CI explicitly selects the shared workflow's `microsoft-testing-platform`
command contract. Test evidence uses the xUnit v3 MTP-native TRX reporter;
VSTest-only `--logger` and `--collect` options are not passed to Parties test
executables. Release reuses successful exact-source `ci.yml` evidence without
duplicating those test tiers. Shared release preparation restores and builds without
rerunning those tests.

## Release

Release is an explicit operator action through `workflow_dispatch`; pushes to `main` run CI but never publish. Before the protected release job is requested, the caller proves that the dispatch selected the current `main` tip and has a successful exact-source push run of `ci.yml`. That full-CI proof is fixed in the caller gate, shared preparation, and publication environment; the Parties publication wrapper accepts only `ci.yml`. The preparation action, its `builds-execution-sha` input, and nested publishing tools must resolve to the same reviewed immutable Hexalith.Builds commit.

If exact-source push CI is missing, failed, or canceled, find the original `main` push run for the intended commit under Actions → CI. Wait for a running attempt to finish, or use **Re-run all jobs** (or `gh run rerun RUN_ID`) on the failed or canceled original run. A rerun retains the original commit SHA and ref, as documented in [GitHub's rerun instructions](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs). Wait until that push run completes successfully, then dispatch Release again from current `main`. If `main` advanced, its new tip requires its own successful push CI. If no original push run exists, CI from a normal reviewed `main` push must establish the required evidence. Successful scheduled or `repository_dispatch` runs cannot substitute for push proof. EventStore 3.117.0 resolves the package API prerequisite described below; the selected Parties commit still requires its own successful full CI.

The `production` environment must require human reviewers and allow deployments only from `main`. After approval, shared preparation:

- installs npm dependencies from `package-lock.json` and verifies their signatures;
- restores and builds `Hexalith.Parties.slnx`;
- permits publication only when `HEXALITH_RELEASE_PUBLISH_ENABLED` is exactly lowercase `true`;
- requires the explicit NuGet creator account in repository variable `NUGET_USER` and revalidates the exact dispatched `main` SHA against successful push proof from `ci.yml`.

The enabled job then prepares arm64 emulation and the shared container publisher, authenticates through SHA-pinned `NuGet/login@8d196754b4036150537f80ac539e15c2f1028841`, and runs semantic-release. Login and publication are skipped when preparation reports a frozen release. Semantic-release:

- packs and validates Parties NuGet packages through `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, and `scripts/validate-consumer-package-references.py`;
- publishes NuGet packages with the short-lived `NUGET_API_KEY` supplied exclusively by the login output;
- publishes exactly these Parties-owned containers to Zot through the shared release publisher:
  - `registry.hexalith.com/parties`
  - `registry.hexalith.com/parties-mcp`
  - `registry.hexalith.com/parties-ui`

The active NuGet trusted publishing policy has package owner `Hexalith`, creator account `jpiquot`, GitHub repository `Hexalith/Hexalith.Parties`, workflow filename `release.yml`, and environment `production`. Set `NUGET_USER=jpiquot` as a repository variable; the creator account is distinct from the package owner and GitHub actor. Authentication stays in the Parties job because NuGet matches the caller workflow identity; [NuGet/login issue 6](https://github.com/NuGet/login/issues/6) records the reusable-workflow limitation. No long-lived NuGet secret or fallback is used. Local policy JSON records registration and does not create a policy in NuGet.

The publication preflight freezes the exact source, nine-package manifest, and complete three-container destination set. It rejects missing credentials, identity drift, or an existing package/container version before the first publication write; duplicate skipping is deliberately disabled.

The release workflow does not apply runtime deployment manifests and does not publish EventStore, Tenants, Memories, Sample, Redis, or FalkorDB images. Runtime deployment orchestration is owned outside this repository and consumes immutable release tags from Zot.

## Local Parity

Use these commands before pushing CI/CD changes:

```powershell
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1
pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release
pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release
```

To reproduce local test-lane evidence and continue past a failing project:

```powershell
pwsh -NoProfile -File scripts/test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults
```

CI and default local commands run in package mode (`UseNuGetDeps=true`, `UseHexalithProjectReferences=false`). If unpublished Hexalith packages block restore, record the package-mode blocker and rerun source-mode triage with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` only as diagnostic evidence.

The effective package graph selects EventStore 3.117.0 through the Parties version pin in `Directory.Packages.props`, set before importing the shared catalog. The Builds catalog at `ad52c5bdd4361c59eedf12a16620150006403584` still defaults to 3.115.0. The explicit Parties pin supplies the published sidecar-channel API and retains the actor-history APIs. MSBuild evaluation and restored assets verify the selected package family; see [the upgrade receipt](../_bmad-output/implementation-artifacts/tests/eventstore-package-upgrade-2026-10-08/README.md).

Package mode remains the authoritative CI and release path; source mode is diagnostic only. The solution also builds its orchestration projects and the documented Commons HTTP/ServiceDefaults source fallbacks; these do not switch Parties' EventStore package consumers to source mode.

Normal package-mode `dotnet restore Hexalith.Parties.slnx` and `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` now succeed, with zero warnings and errors. The earlier two CS1061 errors against 3.115.0 and the August projection-rebuild CS0246 failure are historical. The host security calls and projection-rebuild implementation remain intact.

### Required EventStore publication

Resolved on 2026-10-08 by [EventStore v3.117.0](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.117.0), source `b830d9829af70536d2a3fd21c5e2a23b2ca2f256`. Publication used [successful exact-source full CI](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/37749252540) and the [normal Release workflow](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/37750172086), with the upstream validation-bypass input false. The [publication receipt](../_bmad-output/implementation-artifacts/tests/eventstore-ci-unblock-2026-10-08/README.md) retains the exact input, source gates, security test results, and release artifact.

All 14 packages in the release source's `tools/release-packages.json` were downloaded from the public NuGet feed at 3.117.0. Their nuspec repository commits match the release source, a descendant of [the API-introducing commit](https://github.com/Hexalith/Hexalith.EventStore/commit/c4d5455a3b79ca1ba0113a286cf2432b2cace7fb). The published [DomainService package](https://www.nuget.org/packages/Hexalith.EventStore.DomainService/3.117.0) exposes public static `RequireEventStoreSidecarChannel<TBuilder>` constrained to `IEndpointConventionBuilder`; its Client and ServiceDefaults dependencies both select 3.117.0. Package and DLL hashes, metadata inspection, and restored family versions are recorded in [the upgrade receipt](../_bmad-output/implementation-artifacts/tests/eventstore-package-upgrade-2026-10-08/README.md).

`EventStoreDomainServiceSecurityExtensions.RequireEventStoreSidecarChannel<TBuilder>` removes anonymous metadata and applies the authenticated app-channel policy. The ServiceDefaults authentication scheme and startup endpoint inventory remain required. Upstream full CI passed `EndpointInventory_RequiresTheSidecarChannelPolicyOnSidecarRoutes`, `UseEventStoreDomainService_EveryNonProbeEndpointRequiresCredentials`, and `SubscriptionRoute_RequiresTheAppChannelToken` at the release source.

Runtime topology acceptance remains a separate prerequisite:

- The current `src/Hexalith.Parties.AppHost/Program.cs` does not configure `APP_API_TOKEN` or workload issuance. The integrated-topology owner (FrontComposer or the approved Platform AppHost; see [architecture section 4](architecture.md#4-system-topology-aspire)) must wire each receiving `eventstore`, `parties`, and `tenants` application to its own sidecar using the SDK's [`WithGeneratedEventStoreAppChannelToken` or `WithEventStoreAppChannelToken`](https://github.com/Hexalith/Hexalith.EventStore/blob/b830d9829af70536d2a3fd21c5e2a23b2ca2f256/src/Hexalith.EventStore.Aspire/HexalithEventStoreAppChannelExtensions.cs). Provision the gateway's confidential OIDC workload client through [`WithEventStoreWorkloadClientCredentials`](https://github.com/Hexalith/Hexalith.EventStore/blob/b830d9829af70536d2a3fd21c5e2a23b2ca2f256/src/Hexalith.EventStore.Aspire/HexalithEventStoreTrustedEffectExtensions.cs) and the receiving services' `Authentication:JwtBearer` and `Authentication:Workload` settings. Grant `eventstore-audience.parties` and `eventstore-audience.tenants`, with the required `eventstore-operation.domain-service.{process,query,project,replay-state,metadata}` scopes as optional grants; the issuer requests exactly one audience/operation pair per assertion. This credential-wiring follow-up is required before topology acceptance; the package API upgrade does not implement it.
- Runtime acceptance must include a gateway command with the expected persisted state, accepted sidecar subscription/actor callbacks, and rejection of missing or forged app-channel/workload credentials without state changes. Run required topology checks without skips; unavailable infrastructure or skipped required checks remain a recorded blocker. The compiler fix does not establish integrated runtime readiness.
- After the normal reviewed main push, require successful full `ci.yml` for that exact current Parties main commit before dispatching Parties Release. Passing the local CI test lane validates workflow contracts; it cannot replace that full-CI release proof.

The [earlier 2026-10-08 observation](../_bmad-output/implementation-artifacts/tests/eventstore-package-api-2026-10-08/README.md) records the original 3.115.0 compiler failure and publication handoff; the [2026-10-07 receipt](../_bmad-output/implementation-artifacts/tests/eventstore-package-api-2026-10-07/README.md) also remains historical evidence.

## Secrets

Required release variables, NuGet policy configuration, and Zot secrets are listed in [ci-secrets-checklist.md](ci-secrets-checklist.md). Zot automation uses `HEXALITH_ZOT_USERNAME` and `HEXALITH_ZOT_API_KEY`; the API key is generated after Zot Keycloak/OIDC login and replaces the password for Docker-compatible clients.

## Pact Readiness

Pact.js scripts are not currently exposed at the repository root. To make Pact contract gates enforceable later, scaffold the Pact framework and add:

- `test:pact:consumer`
- `publish:pact`
- `test:pact:provider:remote:contract`
- `can:i:deploy:provider`
