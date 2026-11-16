# CI/CD and product qualification

Parties is preparing to adopt the shared Hexalith.CiCd pipeline. Registration,
App delivery and hosted acceptance are pending. The eight legacy workflows are
unchanged during product preparation; CiCd delivery replaces the owned perimeter.
The prior pipeline description is retained as [historical reference](ci-legacy-reference.md).

## Product boundary

Use .NET SDK **11.0.100**, `Hexalith.Parties.Standalone.slnx`, Release output and
published libraries for CI qualification. Source dependencies are limited to
explicit, gitlink-pinned runtime executable/composition owners and their required
helpers. Do not initialize nested submodules in an umbrella workspace.

Keep compilation, security and dependency audits, commit validation and package
publishing. Retain actual runtime authentication, persistence, concurrency,
isolation, cleanup, module/ACL declarations, dependency compatibility, package,
container, browser and accessibility checks. Tests of workflow YAML, assistant
instructions, story status, historical CI receipts and controller orchestration
are retired. Historical records and actual product fixture bytes remain available.

All three native test projects participate in the solution build:
`Hexalith.Parties.Platform.IntegrationTests`,
`Hexalith.Parties.Platform.LifecycleTests` and
`Hexalith.Parties.Platform.AttachedLifecycleTests`. The published EventStore 5.1.1
fixture APIs compile with these projects. The package graph selects EventStore through `HexalithEventStoreVersion`; explicit compatibility selections retain authority. Compilation does not establish a
successful native runtime run.

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

After a successful fresh Release solution build, run
`bash scripts/prepare-aspire-runtime-hosts.sh --reuse-build true --include-memory-qualification true`
to bind those outputs without compiling or restoring. The completed-build marker
and existing strict cohort/archive checks must pass. Use the same optional
`--artifacts-path` as the solution build. The catalog verifier also accepts
`--reuse-build`; it retains its runtime custody/cleanup behavior.

Compatibility-cohort routing and enabled hooks remain pending. The shipped
native runner still compiles its model and tests for each private execution;
package-mode native hooks require shared artifact-reuse support before they can
satisfy the approved build-once boundary. Direct legacy qualification paths remain
available, and their results do not establish central-pipeline acceptance.

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
shared CI and the next scheduled nightly outcome remain acceptance requirements.
Use the [release checklist](../RELEASE-CHECKLIST.md) for that boundary.
