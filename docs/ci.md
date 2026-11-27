# CI/CD and product qualification

Parties is preparing to adopt the shared Hexalith.CiCd pipeline. Registration,
App delivery and hosted acceptance are pending. Legacy workflows remain active
until CiCd replaces the owned perimeter. See the
[historical pipeline description](ci-legacy-reference.md).

## Product boundary

Use .NET SDK **11.0.100**, `Hexalith.Parties.Standalone.slnx`, Release output and
published libraries for CI qualification. Source dependencies are limited to
explicit, gitlink-pinned runtime executable/composition owners and their required
helpers. Do not initialize nested submodules in an umbrella workspace.

Keep compilation, security and dependency audits, commit validation and package
publishing. Retain actual runtime authentication, persistence, concurrency,
isolation, cleanup, module/ACL declarations, dependency compatibility, package,
container, browser and accessibility checks. Shared CI will retire checks of
workflow YAML, assistant instructions, story status, historical CI receipts and
controller orchestration. Legacy workflows still invoke controller tests during
preparation. Historical records and product fixtures remain available.

The standalone solution builds all three runner-owned projects,
`Hexalith.Parties.Platform.IntegrationTests`,
`Hexalith.Parties.Platform.LifecycleTests` and
`Hexalith.Parties.Platform.AttachedLifecycleQualification.Tests`.
The attached suite uses the current EventStore SDK for lineage and expiry.
The original attached project path forwards legacy builds to that suite. The
package graph selects EventStore through `HexalithEventStoreVersion`. Explicit
compatibility selections retain authority. Use `hexalith-module test` to
verify native runtime behavior.

## Local verification

Run disposable verification under `run-with-temp`, with source copies, feeds and
build outputs below its private `TMPDIR`. Preserve useful logs outside it. Run
test projects individually, using the solution only for restore/build. For focused
xUnit execution, invoke the built assembly with `-class` or `-method`.

Browser tooling remains under `tests/e2e`; its locked manifest includes the full
accessibility, portal and picker suite. Build the package-mode Release UI first,
then run `npm ci`, `npm run test:config`, `npm run typecheck` and `npm run test:ui`
from that directory. The Playwright fixture uses the prebuilt UI.

The standalone solution declares the package-mode executable owners through
`Hexalith.Parties.RuntimeArtifacts`: EventStore Admin server/UI, Tenants,
Memories server/domain service and the sample browser host. Its two required
nonpackable helpers are declared alongside them. Ordinary libraries remain
packages; runtime project references retain the Release configuration. Published
Aspire DCP/dashboard assets are restored by that same graph.

After a successful standalone Release package build, run
`bash scripts/prepare-aspire-runtime-hosts.sh --reuse-build true --include-memory-qualification true`.
Preparation checks the build inputs and the output bytes recorded for each
project that MSBuild visited. An unchanged incremental build can reuse an older
DLL. A source, profile or output mismatch requires another solution build.
Use the same optional `--artifacts-path` as the build. The catalog verifier
also accepts `--reuse-build`.

To repeat the regression, build the standalone solution twice with
`-c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true`,
then run `python3 scripts/verify-incremental-runtime-reuse.py`. The check
permits SDK property queries during preparation, verifies all 13 receipts
and compares the entry binaries' hashes and timestamps before and after.
It needs network access for the existing public package archive verification.

The browser hook is prepared for shared environment setup and the prebuilt
Release UI. Its registration and compatibility-cohort routing remain pending.
The shipped native runner still compiles its model and tests for each private
execution. Package-mode native hooks require shared artifact reuse to use the
same standalone build. Direct legacy qualification paths remain available.

## Adoption and releases

Hexalith.CiCd owns workflows, CI configuration, Dependabot, the Builds perimeter
and delivered agent policy. Module changes must preserve those owned bytes,
modes and gitlinks. After adoption, require successful
`python3 references/Hexalith.Builds/check.py` before every module push.

Releases follow the central nightly process after authenticated App adoption.
Keep the `parties`, `parties-mcp` and `parties-ui` identities and their strict
source/version/created checks. Shared `SourceRevisionId` and `Version` feed the
existing provenance properties; conflicting identities fail. The separately
released frozen publisher-created capability must be available before adoption;
per-project clocks and commit timestamps do not satisfy that requirement.

Keep the publishing switch exactly `true` and retention at 90 days. Maintainer
prerequisites, migration ordering, scoped publisher grants, App adoption, green
shared CI and a post-adoption central nightly outcome remain acceptance requirements.
Use the [release checklist](../RELEASE-CHECKLIST.md) for that boundary.
