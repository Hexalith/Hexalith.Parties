# CI/CD Pipeline

Hexalith.Parties uses GitHub Actions with shared Hexalith.Builds workflows and actions.

## Workflows

- `.github/workflows/ci.yml` delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@1745ea0f09df9e518aeea3eeb1a7df836f67e09d`.
- `.github/workflows/release.yml` is a manual `workflow_dispatch` workflow with a protected, caller-owned release job. It uses `Hexalith/Hexalith.Builds/Github/prepare-domain-release@11a3a79cfac4bba9548c6364a17a01ee9b561370` and takes the nested publisher from that same immutable Builds checkout.
- `.github/workflows/commitlint.yml` delegates Conventional Commit validation to the retained immutable Builds revision `332b2c144defba546fe0a09626a4a72f2340e22f`, for pull requests and main pushes.
- `.github/workflows/dependency-review.yml` delegates dependency review for pull requests.
- `.github/workflows/codeql.yml` delegates C# CodeQL scanning to `Hexalith/Hexalith.Builds/.github/workflows/codeql.yml@332b2c144defba546fe0a09626a4a72f2340e22f`.
- `.github/workflows/rc-gate.yml` validates root gitlink signoff on ordinary `main` pushes, release candidates and tags.

The validation callers pin their reusable workflow definitions independently.
For a revision update, inspect the selected workflow and caller inputs, then
update the caller SHA and its matching `BuildsCiSha` or `BuildsValidationSha` in
`tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs`, and the
references in this document and `docs/architecture.md` together. The Builds
checkout must contain the selected Git object: the compatibility check reads
that immutable object, even when the checkout moves to another revision.
These pins are independent of the release execution pin and do not pin every
transitive action used by Builds. Validate an update with:

```bash
actionlint .github/workflows/ci.yml .github/workflows/codeql.yml
dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet tests/Hexalith.Parties.Ci.Tests/bin/Debug/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.PartiesContainerPublishWorkflowTests
```

## CI

CI runs on pushes and pull requests to `main`, scheduled nightly runs, and Pact Broker `repository_dispatch` events with type `contract_requiring_verification_published`.

The shared CI workflow restores and builds `Hexalith.Parties.slnx`, initializes only root-declared submodules, runs package consumer validation, and executes the configured test tiers:

- Tier 1: unit, UI, package, and boundary tests.
- Tier 2: integration and CI contract tests.
- Tier 3: Aspire topology tests through `tests/Hexalith.Parties.IntegrationTests`.

The blocking `ui-a11y` job retains its **UI accessibility** check name and runs on every CI trigger without path filters or
failure suppression. It installs Node 24, the SDK from `global.json`, locked
browser-test dependencies with Node 24 type declarations, and Chromium with its
system dependencies. It typechecks the tests, builds the UI in Release/package mode, then runs
`npm run test:ui` against Playwright's isolated Test-environment host. That single
Playwright invocation includes `parties-accessibility.spec.ts`,
`admin-parties-list.spec.ts`, `consumer-portal-routes.spec.ts`, and
`party-picker.spec.ts`, so every suite contributes to the same HTML/JUnit report
and retained diagnostics. Only the root Builds and Commons submodules are needed for this UI build;
Docker, Dapr and Keycloak are not required. The host's Test-only fixture opt-ins
and loopback address are explicit, and CI does not reuse an existing server.

The lane covers blocking axe violations, shell skip links, keyboard traversal,
media settings, button styling, the semantic visual baseline and the asserted
Chromium/Linux screenshot baseline. Admin dialog warnings and dropdown semantics,
contact Email selection, consumer routes and party-picker behavior run as required
checks without optional environment flags. For focused local runs,
`npm run test:a11y` retains the accessibility specimen suite, and
`npm run test:portals` runs the three portal/picker suites. HTML/JUnit reports,
axe summaries and available failure
traces, screenshots and videos are uploaded as `ui-a11y-results` for 14 days,
including after a failed run. A failed browser gate fails `ci.yml`, so it
also prevents successful exact-source CI proof for release. Repository branch
protection is managed separately. See the local reproduction commands in
[the development guide](development-guide.md#4-test).

The separate `bmad-config` job in the same CI workflow runs the offline BMAD
configuration suite on Ubuntu 24.04 using Python 3.11 or later. It checks
duplicate identifiers, layer precedence, resolver errors, and renderer
publication. Run it locally with:

```bash
python3 -B -m unittest discover -s _bmad/scripts/tests -v
```

Coverage is intentionally not enabled yet in `ci.yml`; the local `coverage` lane is still blocked under the current Microsoft.Testing.Platform/xUnit v3 setup until an MTP-compatible coverage path is configured.

CI explicitly selects the shared workflow's `microsoft-testing-platform`
command contract. Test evidence uses the xUnit v3 MTP-native TRX reporter;
VSTest-only `--logger` and `--collect` options are not passed to Parties test
executables. Release reuses successful exact-source `ci.yml` evidence without
duplicating those test tiers. Shared release preparation restores and builds without
rerunning those tests.

## Release

Release is an explicit operator action through `workflow_dispatch`; pushes to
`main` run CI but never publish. Before requesting the protected release job,
the caller proves that the dispatch selected the current `main` tip and has a
successful exact-source push run of `ci.yml` plus independent completed
successful `main` push proof from `rc-gate.yml`.

The gate validates both fixed workflow identities through the GitHub API. Each
run must bind its workflow ID, exact source SHA, `main` branch, `push` event,
completed status and success conclusion. The RC proof covers the original
main-push range. Missing, stale, pending, canceled, failed, manual, PR or tag
runs cannot substitute. The release-floor gitlink signoff check remains
required after preparation.

Full-CI proof stays fixed in the caller gate, shared preparation and publication
environment; the Parties publication wrapper accepts only `ci.yml`. Preparation,
its `builds-execution-sha` input and nested publishing tools must use the same
reviewed immutable Hexalith.Builds commit.

If either exact-source push proof is missing, failed, or canceled, find the original `main` push run for the intended commit under Actions → CI or Release Candidate Gate, respectively. Wait for a running attempt to finish, or use **Re-run all jobs** (or `gh run rerun RUN_ID`) on the failed or canceled original run. A rerun retains the original commit SHA and ref, as documented in [GitHub's rerun instructions](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs). Wait until that push run completes successfully, then dispatch Release again from current `main`. If `main` advanced, its new tip requires its own successful push CI and RC range proof. If no original push run exists, CI from a normal reviewed `main` push must establish the required evidence. Successful scheduled or `repository_dispatch` runs cannot substitute for push proof. EventStore 3.117.0 resolves the package API prerequisite described below; the selected Parties commit still requires its own successful full CI.

Both `release` and `verify-publication` independently declare the `production` environment. That environment must require human reviewers and allow deployments only from `main`. Local fitness tests remove each job's declaration in isolation; they do not exercise GitHub's environment protection. After approval, shared preparation:

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

The UI retains `InvariantGlobalization=false` and the ICU-capable
`mcr.microsoft.com/dotnet/aspnet:10.0-alpine-extra` base. DW-105's
[local qualification](../_bmad-output/implementation-artifacts/tests/five-runtime-release-deferred-items-2026-10-22/README.md)
rebuilds and SDK-publishes a fresh local image, then reuses the shared smoke
helpers in Development for bounded startup, `/alive`, `/health`, `/ready` and RCL
stylesheet requests. It qualifies local `linux/amd64` startup only. Release must
still pass the shared publisher's full smoke and publication verification.

The active NuGet trusted publishing policy has package owner `Hexalith`, creator account `jpiquot`, GitHub repository `Hexalith/Hexalith.Parties`, workflow filename `release.yml`, and environment `production`. Set `NUGET_USER=jpiquot` as a repository variable; the creator account is distinct from the package owner and GitHub actor. Authentication stays in the Parties job because NuGet matches the caller workflow identity; [NuGet/login issue 6](https://github.com/NuGet/login/issues/6) records the reusable-workflow limitation. No long-lived NuGet secret or fallback is used. Local policy JSON records registration and does not create a policy in NuGet.

Consumer validation uses published support dependencies and clears inherited NuGet package sources, fallback package
folders and disabled-source settings. By default it uses
the local Parties feed plus nuget.org. Supplying
`--nuget-source SOURCE` replaces the nuget.org default; repeat the option for
each desired source, including nuget.org if needed. Generated configuration excludes
inherited `packageSources`, `fallbackPackageFolders` and `disabledPackageSources`
entries. Consumer subprocesses also discard inherited
`NUGET_FALLBACK_PACKAGES`. Other NuGet settings retain their normal hierarchy.

Support artifacts use NuGet-normalized version identities and filenames, including
zero fourth components and build metadata. After restore, every selected
Commons, EventStore, FrontComposer and Tenants package must match its evaluated central
version. Portal consumers also require the four direct FrontComposer/Tenants
support packages; missing packages and project substitutions fail before build.

Explicit feeds are named `additional-source-1`, `additional-source-2`, and so
on, in option order. Credentials stored under an inherited feed's original
name are not reused. For a private feed, configure a credential provider or
set `NuGetPackageSourceCredentials_additional-source-1` (adjust the number for
later sources) to `Username=<user>;Password=<token>`, following
[NuGet's authenticated-feed guidance](https://learn.microsoft.com/en-us/nuget/consume-packages/consuming-packages-authenticated-feeds).

The publication preflight freezes the exact source, ten-package manifest, and complete three-container destination set. It rejects missing credentials, identity drift, or an existing package/container version before the first publication write; duplicate skipping is deliberately disabled.

After publication, verification considers only published, non-draft GitHub Releases
with canonical stable `vMAJOR.MINOR.PATCH` tags: exactly three numeric components,
no leading zeros except `0`, no prerelease and no build metadata. It resolves every
unique eligible tag, including annotated refs, and requires exactly one distinct
version at the dispatched commit. Non-version tags are ignored and duplicate
release entries for the same tag are deduplicated. Multiple distinct eligible
versions at that SHA fail closed regardless of API list order. A tag-ref lookup
returning HTTP 404 is logged and skipped because older releases can outlive
their tags. Other API failures, invalid responses and invalid tag objects still
fail verification. The matched tag is exposed as `verify-publication`'s
`matching-tag` output and supplies the release version to artifact verification.

The read-only verification job checks out the exact dispatched Parties source
and Builds `11a3a79cfac4bba9548c6364a17a01ee9b561370`, with persisted checkout
credentials disabled. It also initializes only the dispatched root Builds
gitlink, without recursion, and installs the SDK from `global.json` to evaluate
that source's package catalog. `scripts/verify-release-publication.py` requires
all ten unique IDs in `tools/release-packages.json`, downloads each `.nupkg`,
and reuses archive, README/license and dependency-boundary validation. Nuspec
ID, version, repository URL, repository type `git` and commit must match the
release exactly. Existing required dependency and version checks use the evaluated
source catalog. Metadata must be direct nuspec children, and referenced license
files must exist. Malformed archives, duplicate members/nuspecs and ambiguous or
partial inventories fail closed.

All three Zot images (`parties`, `parties-mcp`, `parties-ui`) must also pass the
immutable Builds OCI validator; its file hash is checked before loading.
The immutable selection is Builds `11a3a79cfac4bba9548c6364a17a01ee9b561370`,
with validator SHA-256
`1913f75bdf39c494c5723bca5c04fcb2e6dc0474ad2d86c22e63aae96b9e850c`.
Graph controls require canonical, equal image/artifact created instants across all
platform children. Executable fixtures reject missing labels, malformed values,
impossible calendar dates, unequal pairs and cross-platform disagreement; the
publisher's authoritative NuGet-404 distinction is preserved.

The validator checks tag and immutable index bytes, every child manifest/config digest and
size, exactly `linux/amd64` and `linux/arm64`, and each child's source,
revision, release version, release URL and documentation provenance. Verification
uses registry read requests and the existing protected production credentials;
it performs no publication writes. The job retains the production environment
boundary and has only `contents: read` GitHub permission.

Downloads have per-response byte bounds, a thirty-second absolute request timeout
covering connection setup, headers, bodies and redirects, four attempts with
two-second delays for missing/transient responses, and one ten-minute total
download budget. Uninterruptible resolver work can retain one of four shared
connection slots until it settles; late sockets are closed. Redirect bodies are
closed unread, same-origin credentials are preserved, cross-origin authorization
is removed, and HTTPS downgrades fail. Authentication failures and malformed
bytes fail immediately. Registry propagation may recover within those limits;
exhaustion fails the job. The always-run upload retains bounded downloaded
packages, partial failure captures, OCI raw manifests/configs, attempt diagnostics
and `publication-verification.json`
under the `publication-verification-RUN_ID-RUN_ATTEMPT` artifact for 30 days.
Receipts derive hashes from observed registry bytes. NuGet may add signatures,
so those hashes do not prove equality with unsigned build archives. OCI proof
covers the shared validator's manifest/config graph and provenance.

Root release tooling and the browser workspace both require Node **24.22.1**
and its bundled npm **11.19.1**. Their exact `engines` metadata and project
`.npmrc` (`engine-strict=true`) reject a different Node or npm before installing
dependencies. The browser `setup-node` and reusable commitlint caller select
that exact distribution. The release caller passes it as `node-version` to the
immutable preparation action,
so the action selects it before its first `npm ci --ignore-scripts`. Installing
or checking npm after preparation would be too late. Dependency resolutions stay
in the existing lockfiles; qualification uses the official distribution's SHA-256
receipt and both real locked installs plus wrong-Node/wrong-npm rejection checks.

Ordinary `main` pushes use the event's exact, nonzero `before` commit as the
root-gitlink gate base. It must exist and be an ancestor of the checked-out
source; invalid, missing, zero or divergent bases fail closed. `--push-range`
checks every root pointer change in every commit in that range, including
intermediate changes later replaced at the tip. The final ledger must authorize
each advanced pointer; stale SHAs, placeholder owners and `reset` entries cannot
authorize a bump. Existing RC, tag and manual `--diff` behavior remains available.
No fallback to the newly updated `origin/main` is used for main pushes. Each
main source SHA has a separate concurrency group, so later pushes cannot replace
its running or pending check. The complete range's root declarations are collected
before checking advances, including declarations added after their gitlinks.
Merge results are compared with their first parent while reachable branch
commits are also checked.

Local fixtures verify the selected range script and the declared per-SHA group.
GitHub scheduling qualification additionally requires two actual `main` push runs
whose lifetimes overlap and both complete successfully. Retain each run's exact
head SHA, event, branch, creation/completion timestamps, successful jobs and
logged `before` range. Canceled, missing or nonoverlapping runs leave the live
scheduling follow-up open; no workflow dispatch or release is needed for this
qualification.

The [2026-10-22 live receipt](../_bmad-output/implementation-artifacts/tests/five-review-runtime-convergence-deferred-items-2026-10-22/scheduler-proof.json)
records two successful exact-source main push runs with twelve seconds of run
overlap and four seconds of actual gate-job overlap. Both logs bind the preceding
main tip to the checked push-range.

The executable fixtures require Python, Git, Bash and `jq` on a non-Windows
system, plus the pinned Builds Git object available in the direct root checkout.
They use isolated temporary Git histories and loopback HTTP registries; they
never publish. Check executed and skipped counts when assessing local evidence.

```bash
dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet tests/Hexalith.Parties.Ci.Tests/bin/Debug/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.ReleasePublicationVerificationTests -class Hexalith.Parties.Ci.Tests.ReleaseArtifactVerificationTests -class Hexalith.Parties.Ci.Tests.GitlinkGateWorkflowTests
```

The release workflow does not apply runtime deployment manifests and does not publish EventStore, Tenants, Memories, Sample, Redis, or FalkorDB images. Runtime deployment orchestration is owned outside this repository and consumes immutable release tags from Zot.

## Local Parity

Use these commands before pushing CI/CD changes:

```powershell
dotnet restore Hexalith.Parties.Standalone.slnx
dotnet build Hexalith.Parties.Standalone.slnx --configuration Release --no-restore -m:1
pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release
pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release
```

To reproduce local test-lane evidence and continue past a failing project:

```powershell
pwsh -NoProfile -File scripts/test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults
```

CI and the local commands above use package references and Release builds. If
unpublished Hexalith packages block restore, record that package-mode blocker.
For separate source diagnostics, use the pinned module tool's `debug` command
from [getting-started.md](getting-started.md#step-1-deploy-locally); it selects
direct source projects and Debug builds through its persisted mapping. Manual
MSBuild mode selectors no longer select source references. A successful source
run does not clear a package CI failure.

The effective package graph selects EventStore through `HexalithEventStoreVersion`
and Commons through `HexalithCommonsVersion`. Root pins in
`Directory.Packages.props` and the imported Builds catalog govern these values;
query the effective Release graph after any dependency selection change:

```bash
dotnet msbuild src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -nologo -p:Configuration=Release -getProperty:HexalithEventStoreVersion,HexalithCommonsVersion
```

The repository fitness and publication tests use the same MSBuild evaluation
order, including root pins and conditional imports. Current selection does not
renew historical runtime qualification. The earlier 3.120.0 MSBuild/build
evidence and 3.117.1 [consumer receipt](../_bmad-output/implementation-artifacts/tests/eventstore-3.117.1-consumer-2026-10-08/README.md)
remain historical. Exact-source push CI and release gates must validate the
selected graph.

Before topology startup, `PlatformApiPrerequisitesTests` inventories the seven
external EventStore, Tenants, Memories and optional sample projects declared by
the AppHost, then follows their selected project references. It rejects missing
projects, unexpected source selection and EventStore packages that disagree
with the independently evaluated Parties package version. This preflight checks
dependency selection in both Debug and Release; separate project compilation
and runtime qualification remain required. Unexpanded multi-target graphs and
project-reference property modifiers fail with an explicit diagnostic until their
contexts are qualified. Removing an unset SDK publish-profile property is the
only supported no-op modifier.

The owned central catalog pins `Microsoft.AspNetCore.SignalR.Common` to `10.0.13`
for GHSA-334f-vvp4-7g6p, with its required Options and
DependencyInjection.Abstractions floors aligned to `10.0.13`. NuGet audit stays
active. The Web SDK prunes this package from the UI in favor of the shared ASP.NET
runtime, so the UI alone requires runtime `10.0.14` through
`RuntimeFrameworkVersion`. Local UI starts require that runtime to be installed.
The UI ships as a container outside the ten-package NuGet manifest;
the existing container liveness gate remains required. The separately built
EventStore diagnostic source project retains its own upstream catalog and can
still report the older SignalR vulnerability; this is not the owned package graph.

Package mode remains the authoritative CI and release path; source mode is diagnostic only. The standalone solution builds owned projects and restores external dependencies as packages. The development solution retains external source projects for diagnostics.

The standalone package-mode restore and Release build complete with zero warnings and errors. Earlier development-solution restores reported NU1903 warnings from the external EventStore SignalR diagnostic source project; it is excluded from the package-only CI solution. The earlier two CS1061 errors against 3.115.0 and the August projection-rebuild CS0246 failure are historical. The host security calls and projection-rebuild implementation remain intact.

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

## Platform declaration gate

The `platform-declaration` CI job has a ten-minute timeout. It initializes only
the direct EventStore, Tenants and Memories declaration owners, leaving their
nested references uninitialized, and restores the pinned tool. It runs
`dotnet tool run hexalith-module -- validate` with the Parties, EventStore,
Tenants and Memories manifests before checking the generated declaration and
running the malformed/unsafe/stale-input test matrix.

`scripts/render-platform-declaration.py` derives `callers` and `invocationRules`
from the owner ACL; its `TEMPLATE` owns every other declaration field. The strict
parser requires thirteen unique POST routes from EventStore with the existing
deny defaults, trust domain and namespace. After an owner-approved ACL change,
regenerate with `python3 scripts/render-platform-declaration.py`; a route-count
change also requires an approved parser-invariant and test update. Edit
`TEMPLATE` for non-ACL changes, then regenerate. Missing, duplicate, unsafe or
stale input fails without rewriting in `--check` mode.

The local module tool is pinned to 4.39.0 with Aspire CLI 13.6.4 alongside
aspirate; package CI continues to restore packages and build Release. Source mode is selected only
by the tool's persisted mapping, with no file-existence fallback. Legacy hosting
projects and package publication gates remain in their existing scope.

Source status on 2026-10-31: module tool 4.39.0 refuses fresh source mapping
with `HXW009`: its pinned Builds `Props/Directory.Packages.props` at
`970101f4e780f4609b945b8cc740adc3118564b2` is unavailable. An exact-SHA fetch
returned `upload-pack: not our ref`. Source debug/build commands require an
owner pin repair before they can establish source qualification; package
Release builds remain independently verified.

The five-item review-loop proof keeps its previous source/package/browser results as historical evidence, including the five imports produced by module tool 4.34.0. That run recorded HXR011 for absent isolated Dapr configuration and HXR012 for the available CLI 1.18.2/runtime 1.18.7 home against the older catalog tuple. Fresh mappings must use the current pinned tool and pass its prerequisites. Historical receipts do not qualify the broad Platform topology. The separate owned component runtime retains its declared tool environment and authenticated three-host boundary.

The five-item review-loop 2 receipt separates executed source, package, owned-runtime and browser controls. SDK inventory byte/cursor/entry/timeout controls and actual semantic/NL TopK caller tests qualify the selected source. Enabling `EnableMemoriesDomainService=true` independently selects its host and all three existing EventStore domain routes. Neither that model test nor the optional local host changes production deployment or release gates; matching owner publication/adoption and deployed trust configuration remain separate. Historical receipts remain historical after the second lifecycle correction.

Final executed evidence: [review-loop 2 receipt](../_bmad-output/implementation-artifacts/tests/five-memories-inventory-and-publication-2026-10-24/review2-verification.json).


The 2026-10-24 Memories inventory batch retains immutable Builds publisher `11a3a79cfac4bba9548c6364a17a01ee9b561370` and validator SHA-256 `1913f75bdf39c494c5723bca5c04fcb2e6dc0474ad2d86c22e63aae96b9e850c`. Loop3 rechecks source admission, maintenance publication, tenant teardown, total HTTP deadlines and privileged-route denials separately from package behavior. An existing submodule-mutating guard skip cannot establish runtime acceptance. Source browser, real backend, authenticated owned runtime and OCI negative controls retain their own receipts; owner publication and committed adoption remain parent-owned gates.

## Package-only CI and release solution

CI and release preparation use `Hexalith.Parties.Standalone.slnx`, which includes
every owned project and excludes external diagnostic source projects. Use this
solution for local Release/package parity. `Hexalith.Parties.slnx` remains the
development solution for source diagnostics. Building external projects alongside
package consumers previously caused concurrent EventStore output writes and mixed
Debug/Release outputs.

Consumer verification restores support dependencies from the configured package
feeds by default and validates their exact central family versions in restored
assets. Diagnostic source packing requires `--source-support-packages` and already
built Release support projects; CI and release do not select that option.

The optional mapped shared-download cancellation qualification lives in
`shared-feedback-source.spec.ts`. Run `npm run test:source-feedback` after a
mapped source UI build; it fails if the required source capability is absent.
The required package portal specs contain no skip or optional capability gate.

Aspire CI runs `scripts/prepare-aspire-tier.sh` before its test process.
The ten-minute preparation budget includes HTTPS trust and serial Release builds
of all eight external runtime hosts. These use their native source graphs,
independently of the package-only consumer solution. Preparing shared outputs
before concurrent topology launches prevents cold `dotnet run` builds from
colliding. The existing five-minute topology startup deadline remains unchanged.

The HTTPS helper, `scripts/prepare-aspire-https-trust.sh`, runs first.
It exports the SDK certificate trust directory while trusting the ephemeral
runner's development certificate, then persists the same directory for later
steps and checks that the certificate is trusted. Real UI redirects to HTTPS
retain normal certificate and host validation; no certificate bypass is used.

The trust-directory setup follows [Microsoft’s Linux OpenSSL guidance](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0#openssl-trust).
