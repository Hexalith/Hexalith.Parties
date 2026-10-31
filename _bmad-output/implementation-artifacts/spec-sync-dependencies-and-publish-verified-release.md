---
title: 'Synchronize dependencies and publish a verified Parties release'
type: 'bugfix'
created: '2026-10-31'
status: 'in-review'
route: 'dispatch'
baseline_commit: 'a8b2549cfacc43d75373a6e16ad372de525ab309'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/docs/ci.md'
  - '{project-root}/docs/build-gate.md'
---

<frozen-after-approval reason="human validation bypass explicitly requested by user">

## Intent

**Problem:** Parties main cannot build in CI or publish because development catalog code consumes EventStore APIs absent from its latest published 3.128.4 packages. Several dependency tools and root submodule branches also lag upstream.

**Approach:** Restore compatibility with published dependencies while retaining source development capabilities, update dependencies to their latest compatible stable versions, synchronize root submodules, pass CI, and publish a verified release. The parent owns Git synchronization, submodule toolchain updates, hosted CI remediation and release execution after the implementation handoff.

## Boundaries & Constraints

**Always:** Preserve public domain behavior, source development catalog capabilities, hashing guarantees, automated release gates, package-only Release CI, warnings as errors, root submodule boundaries and existing user changes. Use the existing source-selection compilation symbol for unpublished APIs. Interpret SDL as SDK; latest stable SDK is 10.0.403. Keep Node 24 declarations aligned with Node 24. User authorizes commit/push, release, and configured reviewer approval without further human checkpoints. Parent may approve pending production deployments as authenticated configured reviewer jpiquot; preserve protection policy.

**Never:** Initialize/update nested submodules, weaken privacy/security, replace missing canonical hashing with a weaker approximation, publish unqualified upstream libraries just to satisfy compilation, disable automated gates, or claim NuGet publication from a GitHub release alone. Implementation agent must not commit, push, dispatch, or render bmad-build again.

</frozen-after-approval>

## Code Map

- `Directory.Build.targets` already defines `HEXALITH_DEVELOPMENT_CATALOG_SOURCE` when EventStore is source-selected. `tests/Hexalith.Parties.Tests` has a separate source-only symbol. Preserve source/package separation.
- `src/Hexalith.Parties.Contracts/Routing/PartiesCatalogDeclarations.cs`: guard assembly declaration attribute, Operations and private Operation method; Commands, Queries, Projections and Routes already use published APIs. `ReceiverMethods` may remain package-compatible.
- `src/Hexalith.Parties/Program.cs`: guard AddEventStoreDevelopmentDomainCatalog; ordinary packaged host must keep prior behavior.
- `src/Hexalith.Parties.EventStoreExtensions/PartiesCatalogAdapters.cs`: guard new assembly attribute and canonical adapter implementation. Package-mode unavailable enrollment must fail explicitly rather than silently substitute another hash or return empty success.
- `tests/Hexalith.Parties.Contracts.Tests/PartiesCatalogTests.cs`: operation schemas require source APIs; preserve that test in source mode and add package-compatible declaration inventory coverage (41 routes, 28 commands, 11 queries, 2 projections).
- `Directory.Packages.props`: import central versions from Builds; add root Tenants family override 5.11.2 before import. All other used stable NuGets already latest as of audit. UI-only runtime floor in `Directory.Build.props` is 10.0.13; latest installed runtime is 10.0.14, align it.
- `package.json`/lock: commitlint cli and config 21.2.4, semantic-release/github 12.0.10. `tests/e2e/package.json`/lock: axe-core/playwright 4.14.0, playwright/test 1.64.1; preserve @types/node 24.19.2 and TypeScript 7.0.2.
- `.config/dotnet-tools.json`: Builds module tool 4.38.3 to 4.39.0, aspirate unchanged. Root SDK and AppHost SDK already current.
- `.github/workflows/ci.yml`: unchanged immutable shared workflow pins unless concrete evidence requires a fix. UI and .NET failures currently identical missing source APIs.
- `.gitlink-signoff.tsv`/`scripts/gitlink-rc-gate.sh`: parent records exact synchronized pointers under explicit operator release authorization, without claiming wider parity.
- `.github/workflows/release.yml`, `release.config.cjs`, `tools/release-packages.json`: parent waits exact-source main push CI and RC success, dispatches release, approves two pending production jobs and verifies ten NuGet packages plus workflow publication proof.

## Tasks & Acceptance

**Execution:**
- [x] Contracts declarations, host registration and owner adapters -- separate unpublished source APIs from published package code while preserving source behavior and explicit unsupported package enrollment.
- [x] Contracts catalog tests -- retain source schema coverage and run package declaration inventory coverage; inspect additional compile/test failures and repair only concrete regressions.
- [x] Root central version, UI runtime floor, npm manifests/lockfiles and module tool -- apply audited latest compatible versions with pinned package tooling.
- [x] Package-mode standalone Release build and Contracts tests -- verify zero warnings/errors; run browser config/typecheck and narrow regression checks. Parent completes full project lanes and hosted checks after handoff.
- [ ] Parent: synchronize all ten root submodule main branches, update three lagging SDK pins and lagging npm tooling, record exact gitlink signoff, validate Conventional Commit candidates before commits and push.
- [ ] Parent: achieve successful exact-source CI and RC evidence, publish Release with authorized reviewer approvals, verify each manifest NuGet version independently.

**Acceptance Criteria:**
- Given latest published EventStore packages, when the standalone Release solution builds, then catalog code compiles without deleting source development capability or weakening security.
- Given source selection, when catalog schema tests build/run in Debug, then unpublished schema and canonical adapter code remain selected.
- Given synchronized repository pointers and validated commits, when pushed main CI runs, then required checks succeed.
- Given successful exact-source push evidence, when Release executes, then a GitHub release exists and all ten exact-version NuGet packages are retrievable.

## Implementation Notes

Baseline package build and Aspire start both fail with four CS0246 errors in Contracts. `aspire describe` confirms no AppHost is running. Baseline logs: /tmp/parties-release-baseline-build.log and /tmp/parties-aspire-baseline.log. Parent already fetched superproject and all ten origins nonrecursively; all branches clean and no local-only commits. User bypass overrides skill approval checkpoints; automated code review and validation remain required.

## Spec Change Log

- Implementation found one further unpublished catalog API in `PartyDomainProcessor`: `IDomainCommandPayloadInventory`. The interface and its source-only property now use the existing source symbol; the runtime payload allowlist remains active in both modes.
- The package-facing API snapshot excludes the unpublished `Operations` property. Source snapshot comparison excludes only that property; the retained source catalog schema test qualifies it separately.
- Compatible transitive npm resolutions were refreshed without `--force`, preserving direct version constraints and exact Node/npm engines. The parent authorized this continuation after the first audit.

## Review Triage Log

| Finding | Verdict | Evidence / resolution |
|---|---|---|
| Blind1 package browser still consumes old Shell | high | Confirmed axe4.14 label mismatch against4.7.2; parent fixes owner, publishes and adopts tested package before release. |
| Blind2 latest module source preparation HXW009 | medium | Exact970101f4 commit unavailable locally and origin rejects fetch; report actual blocker and use separate qualified diagnostic source path, never claim4.39 preparation passed. |
| Blind3 gitlink signoff absent | high | Confirmed; parent records final committed source identities after authorized sync, then gates must pass. |
| Blind4 module docs/assertion drift | medium | Confirmed stale4.38.3 current instructions; implementation agent aligns docs and derives assertion from manifest. |
| Blind5 Memories SDK documentation drift | medium | Confirmed README old minimum; parent updates alongside SDK pin. |
| Blind6 Node declarations mismatch | medium | Supported CI remains Node24; parent aligns declarations to latest24.x and repeats changed-workspace typechecks. |
| Blind7 declaration test repeats construction | low | Redundant concatenation equality removed; stable inventory and unique route assertions remain alongside processor/Platform coverage. |
| Blind8 canonical hashing behavioral coverage | false | Source hashing implementation unchanged; only source/package availability is guarded and package rejection tested. Existing producer canonical adapter tests own hash/subject behavior. |
| Blind9 missing source enrollment regression | false | Verification reviewer traced normal Platform source bootstrap, committed Serving=true/readback digest and create/query/persisted-event consumer tests; coverage exists at actual boundary. |
| Blind10 theme modes and post-render updates | medium | New first assertion assumedSystem although fixture defaultsLight; parent corrects default coverage and adds explicit System/post-render mode change checks, then full browser gate. |
| Edge hunter | false | No findings returned. |
| Verification gap reviewer | false | No grounded gaps after tracing normal source runner readiness and catalog bootstrap. |

## Verification

- `dotnet build Hexalith.Parties.Standalone.slnx -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true`
- Run individual built xUnit v3 assemblies; use direct single-dash class filters for focused tests.
- In tests/e2e: `npm run test:config`, `npm run typecheck`, and browser gate after package-mode UI build.
- `bash scripts/check-no-warning-override.sh`; `git -c core.whitespace=cr-at-eol diff --check`.
- Parent: both gitlink gates, pinned commitlint, hosted exact-source CI/RC/Release, per-package NuGet flat-container HTTP checks.


### Implementation verification (2026-10-31)

- `dotnet build Hexalith.Parties.Standalone.slnx -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true`: PASS, zero warnings and errors. Log: `/tmp/parties-verified-release-build.log`.
- `dotnet build tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true`: PASS, zero warnings and errors. Log: `/tmp/parties-release-contracts-build.log`.
- `dotnet tests/Hexalith.Parties.Contracts.Tests/bin/Release/net10.0/Hexalith.Parties.Contracts.Tests.dll`: PASS, 229 passed, zero failed/skipped. Includes declaration inventory and explicit unsupported package adapter enrollment. Log: `/tmp/parties-release-contracts-tests.log`.
- Exact Node 24.22.1/npm 11.19.1 installed with nvm and SHA-256 checksum verification; both locked `npm ci --ignore-scripts` installs pass. Browser workspace has zero npm advisories. Root `npm audit fix --package-lock-only` resolves critical Handlebars and unbundled undici findings, retaining 38 advisories (37 moderate, one high). The remaining high is undici 6.29.0 bundled in semantic-release's internal npm 11.21.1; npm cannot independently replace bundled dependencies. Logs: `/tmp/parties-release-npm-audit-fix.log`, `/tmp/parties-release-npm-install-final.log`, `/tmp/parties-release-npm-audit-final.json`.
- In `tests/e2e`, `npm run test:config`: PASS, eight tests; `npm run typecheck`: PASS.
- `dotnet tool restore`: PASS, module CLI 4.39.0 and unchanged aspirate restored. Log: `/tmp/parties-release-tool-restore.log`.
- Source verification command `dotnet tool run hexalith-module -- prepare-build --manifest /home/administrator/projects/hexalith/parties/module/hexalith.module-manifest.v2.json --workspace /tmp/parties-release-source-build --mode source --output json`: BLOCKED, exit 2/HXW009, pinned Builds `Props/Directory.Packages.props` at `970101f4e780f4609b945b8cc740adc3118564b2` unavailable. Parent's exact-SHA fetch also failed with remote `upload-pack: not our ref`; no source selector bypass used. Log: `/tmp/parties-release-source-mapping.json`.
- Browser command in `tests/e2e`, `CI=true npm run test:ui -- --max-failures=1`: FAIL, nine passed, one failed, 58 not run. Playwright's required Chromium 1248 was installed before this meaningful rerun. axe 4.14 reports serious `label-content-name-mismatch` on FrontComposer 4.7.2's theme toggle: `aria-label="Change theme"` omits the displayed mode in `Change theme: Light`. Failure: admin desktop detail test at `specs/admin-parties-list.spec.ts:163`; owner: FrontComposer `FcThemeToggle.razor`. No gate or assertion weakened; parent owns upstream remediation and package adoption. Log: `/tmp/parties-release-browser.log`.
- `bash scripts/check-no-warning-override.sh`: PASS. `git -c core.whitespace=cr-at-eol diff --check`: PASS before handoff.

Implementation agent made no commits, pushes, workflow dispatches, or nested submodule updates. Parent still owns full project lanes, source verification after resolver repair, shared UI remediation, exact-source hosted CI/RC, release approvals and publication proof.

Parent continuation: all root submodules fast-forwarded where needed without nested initialization. Updated compatible npm tooling in six submodules, browser tooling in Memories/FrontComposer/Tenants, and SDK 10.0.403 pins in Memories/McpCli/Platform. Memories preserves verifier-qualified semantic-release/cosmiconfig pins. Browser axe 4.14 exposed shared FcThemeToggle Title omitting current visible mode; owner component now supplies GetAccessibleLabel() and owner render tests assert accessible name for System/Dark. Parent will qualify and publish owner patch before selecting its package in Parties.
