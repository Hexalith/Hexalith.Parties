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

If exact-source push CI is missing, failed, or canceled, find the original `main` push run for the intended commit under Actions → CI. Wait for a running attempt to finish, or use **Re-run all jobs** (or `gh run rerun RUN_ID`) on the failed or canceled original run. A rerun retains the original commit SHA and ref, as documented in [GitHub's rerun instructions](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs). Wait until that push run completes successfully, then dispatch Release again from current `main`. If `main` advanced, its new tip requires its own successful push CI. If no original push run exists, CI from a normal reviewed `main` push must establish the required evidence. Successful scheduled or `repository_dispatch` runs cannot substitute for push proof. The unresolved EventStore package prerequisite described below must still be resolved before full CI can succeed.

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
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release
pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release
```

To reproduce local test-lane evidence and continue past a failing project:

```powershell
pwsh -NoProfile -File scripts/test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults
```

CI and default local commands run in package mode (`UseNuGetDeps=true`, `UseHexalithProjectReferences=false`). If unpublished Hexalith packages block restore, record the package-mode blocker and rerun source-mode triage with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` only as diagnostic evidence.

The effective package graph selects EventStore 3.115.0 through the Parties version pin in `Directory.Packages.props`, set before importing the shared catalog. Retained actor-history queries consume `IRetainedIdentityHistoryReader` and `RetainedIdentityHistoryStream`, which require this release. The Builds catalog at `af20682ac8fc420068a731ecb87cff84727a3d53` also defaults to 3.115.0. MSBuild evaluation confirms `UseNuGetDeps=true`, `UseHexalithProjectReferences=false`, and no EventStore project reference from the Parties domain host; its restored EventStore Client, Contracts, DomainService, and ServiceDefaults assets are packages at 3.115.0.

Package mode remains the authoritative CI and release path; source mode is diagnostic only and must not be used to hide package metadata or publication failures. The solution also builds its orchestration projects and the documented Commons HTTP/ServiceDefaults source fallbacks; these do not switch Parties' EventStore package consumers to source mode.

Package-mode verification on 2026-10-07 remains blocked by an upstream publication prerequisite. `dotnet restore Hexalith.Parties.slnx` succeeds, but `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` exits 1 with 0 warnings and 2 CS1061 errors at `src/Hexalith.Parties/Program.cs:64` and `:66`: the restored EventStore 3.115.0 packages do not expose `RequireEventStoreSidecarChannel` for `IEndpointConventionBuilder`. The August EventStore 3.88.0 projection-rebuild CS0246 failure is historical. Keep the host security calls and projection-rebuild work intact.

### Required EventStore publication

The live NuGet v3 indexes checked on 2026-10-07 end at 3.115.0 for DomainService, ServiceDefaults, Client, Contracts, Server, Testing, Aspire, SignalR, and Gateway, with no prereleases. The downloaded [DomainService 3.115.0 package](https://www.nuget.org/packages/Hexalith.EventStore.DomainService/3.115.0) has repository commit `283b07a52c9c70e1c940164a7011ee8c3ad98b2d`; its assembly does not contain the extension. There is currently no published version to select instead.

The API first appears in upstream [commit c4d5455a3b79ca1ba0113a286cf2432b2cace7fb](https://github.com/Hexalith/Hexalith.EventStore/commit/c4d5455a3b79ca1ba0113a286cf2432b2cace7fb), after [v3.115.0](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.115.0). The required change is already implemented upstream; EventStore owners must publish it:

- Release a coordinated EventStore package family from a validated descendant of that commit, including `Hexalith.EventStore.DomainService` and its `Hexalith.EventStore.ServiceDefaults` dependency. Do not reserve or guess a future package version in Parties.
- Preserve `EventStoreDomainServiceSecurityExtensions.RequireEventStoreSidecarChannel<TBuilder>` and `EventStoreDomainServicePolicies.SidecarChannel`, the ServiceDefaults sidecar authentication scheme, and the domain-service registration and startup endpoint inventory. The extension removes `IAllowAnonymous` metadata and applies authenticated app-channel authorization; a local replacement in Parties would omit platform security wiring.
- Verify upstream `EndpointInventory_RequiresTheSidecarChannelPolicyOnSidecarRoutes`, `UseEventStoreDomainService_EveryNonProbeEndpointRequiresCredentials`, and `SubscriptionRoute_RequiresTheAppChannelToken` before publication. Review the release's [upgrade instructions](https://github.com/Hexalith/Hexalith.EventStore/blob/c4d5455a3b79ca1ba0113a286cf2432b2cace7fb/docs/guides/upgrade-path.md): gateway operational routes require workload assertions in every environment. Outside Development, missing app-channel token or JWT configuration fails startup. Development relaxes startup validation and permits an absent app-channel token, but still denies operational requests it cannot authenticate.
- The current `src/Hexalith.Parties.AppHost/Program.cs` does not configure `APP_API_TOKEN` or workload issuance. After the package upgrade, verify host/sidecar token pairing and gateway workload credentials in the complete topology. Publication resolves the compiler prerequisite; it does not establish runtime or full-CI readiness.
- Once the packages are publicly available, update the central `HexalithEventStoreVersion` pin in Parties to the verified release so the installed family advances together. Re-run package-mode restore, Release solution build, and test lanes, including the gateway/sidecar security tests. Retain the current pin until that release is published.
- After the normal reviewed main push, require successful full `ci.yml` for that exact current main commit before dispatching Release. Passing the local CI test lane validates workflow contracts; it cannot replace the mandatory full-CI release proof.

The verification receipt is in [`eventstore-package-api-2026-10-07`](../_bmad-output/implementation-artifacts/tests/eventstore-package-api-2026-10-07/README.md).

## Secrets

Required release variables, NuGet policy configuration, and Zot secrets are listed in [ci-secrets-checklist.md](ci-secrets-checklist.md). Zot automation uses `HEXALITH_ZOT_USERNAME` and `HEXALITH_ZOT_API_KEY`; the API key is generated after Zot Keycloak/OIDC login and replaces the password for Docker-compatible clients.

## Pact Readiness

Pact.js scripts are not currently exposed at the repository root. To make Pact contract gates enforceable later, scaffold the Pact framework and add:

- `test:pact:consumer`
- `publish:pact`
- `test:pact:provider:remote:contract`
- `can:i:deploy:provider`
