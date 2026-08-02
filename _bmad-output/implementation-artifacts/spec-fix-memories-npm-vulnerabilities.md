---
title: 'Eliminate Memories release-tooling npm vulnerabilities'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
review_loop_iteration: 1
baseline_commit: 'dbb4d77cc18cc40276e76dd98000be42aad33447'
context:
  - '{project-root}/references/Hexalith.Memories/_bmad-output/project-context.md'
  - '{project-root}/references/Hexalith.Memories/docs/dev/release-runbook.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Hexalith.Memories root release-tooling lockfile reports six transitive development dependency vulnerabilities: one moderate, four high, and one critical. Production dependencies and the separate Web E2E workspace audit clean, but the release toolchain must also install without known advisories.

**Approach:** Apply the narrowest effective patched overrides and change the direct semantic-release toolchain where bundled dependencies prevent overrides from working. Preserve release behavior, regenerate the root lockfile, and update only the release validation surface required to prove the resulting install is reproducible, audits clean, and still executes commitlint and semantic-release.

## Boundaries & Constraints

**Always:** Work only in the `references/Hexalith.Memories` repository. Limit product changes to the root npm manifest/lockfile plus release configuration, tests, workflow, or documentation that must change to preserve and verify the existing release contract. Keep commitlint policy, semantic version analysis, package preparation, publication, GitHub Release creation, protected-branch compatibility, and CI-only publication behavior intact. Resolve every audited package to a non-vulnerable version, preserve the independently healthy `@semantic-release/github` dependency on `undici` 7.x, keep the lockfile at version 3, and validate from a clean `npm ci` install.

**Ask First:** Stop for approval if zero vulnerabilities requires npm 12, a change to Node engine policy, application-code or submodule edits, a custom package fork, a new external release service, or a behavioral change to the release contract.

**Never:** Do not run `npm audit fix --force`, suppress or lower audit severity, accept known dev-tool vulnerabilities as harmless, add broad unscoped downgrades, modify the clean `tests/Hexalith.Memories.Web.E2E` manifest or lockfile, initialize nested submodules, or change generated/build artifacts. Do not publish from the workstation or claim the release dry run passed when credentials or remote state prevent it.

</frozen-after-approval>

## Code Map

- `references/Hexalith.Memories/package.json` -- root release-tool declarations and the location for scoped security overrides.
- `references/Hexalith.Memories/package-lock.json` -- reproducible root dependency graph consumed by release CI through `npm ci`.
- `references/Hexalith.Memories/commitlint.config.mjs` -- commit-message policy exercised by the upgraded tool graph.
- `references/Hexalith.Memories/.releaserc.json` -- semantic-release plugin configuration whose loadability must remain intact.
- `references/Hexalith.Memories/tools/verify-semantic-release-config.mjs` -- offline smoke test for loading the real semantic-release configuration and plugin lifecycle.
- `references/Hexalith.Memories/.github/workflows/ci.yml` -- blocking PR gate for clean root installation, audit, and offline release-config verification.
- `references/Hexalith.Memories/.github/workflows/release.yml` -- production consumer whose install/preflight/release order must remain pinned.
- `references/Hexalith.Memories/tests/Hexalith.Memories.Cli.Tests/Ci/CiTestInventoryTests.cs` -- governance tests for active config precedence, manifest/lock resolutions, and CI/release workflow contracts.
- `references/Hexalith.Memories/docs/dev/release-runbook.md` -- operational rationale, compatibility boundary, ownership, and removal trigger for the alias.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Memories/package.json` -- pin semantic-release to the exact verified 25.0.8 version, retain other direct ranges, pin `fast-uri`/`js-yaml`, alias only semantic-release's unused transitive npm plugin, and expose the offline config-smoke command.
- [x] `references/Hexalith.Memories/package-lock.json` -- regenerate lockfile version 3; resolve the exact alias and security pins; remove every real `npm`, `tar`, and `brace-expansion` package entry.
- [x] `references/Hexalith.Memories/tools/verify-semantic-release-config.mjs` -- load the repository's actual semantic-release config and installed plugins without release/network side effects, assert the four-plugin allowlist and required lifecycle steps, and fail on shadow/default npm-plugin loading.
- [x] `references/Hexalith.Memories/.github/workflows/ci.yml` -- add a blocking root release-tooling sequence that runs clean install, full low-level audit, and offline config smoke on pull requests without changing the separate Web E2E install.
- [x] `references/Hexalith.Memories/tests/Hexalith.Memories.Cli.Tests/Ci/CiTestInventoryTests.cs` -- pin active config precedence, exact direct/override/lock resolutions, actual-package-name absence, root PR gates, and `npm ci < release preflight < semantic-release` production order.
- [x] `references/Hexalith.Memories/docs/dev/release-runbook.md` -- document why the official-package alias exists, advisory scope, compatibility owner/version, fail-closed invariant, validation, and removal trigger.
- [x] `references/Hexalith.Memories/` -- execute clean install, both audits, full tree inspection, commitlint, offline config smoke, and the complete CLI test project.

**Acceptance Criteria:**
- Given the updated root manifest and lockfile, when `npm ci` runs, then it completes without manifest/lockfile drift or install errors.
- Given the clean installed tree, when `npm audit --audit-level=low` runs, then it exits successfully with zero vulnerabilities at every severity.
- Given the same tree, when `npm audit --omit=dev --audit-level=low` runs, then it confirms zero production vulnerabilities.
- Given the overridden graph, when the full tree is listed, then npm reports no invalid or extraneous dependency state, resolves `fast-uri` and `js-yaml` to patched versions, and contains no `npm`, `tar`, or `brace-expansion` package.
- Given the alias safety guard, when the CLI test project runs, then it proves the exact semantic-release version and overrides match the lockfile, no real npm publication/bundled packages are installed, and no package key, alternate config file, `extends`, or top-level lifecycle key can shadow the explicit four-plugin allowlist.
- Given a pull request, when blocking CI runs, then it clean-installs the root lockfile, audits all root dependencies at low severity, and loads the real semantic-release config offline before merge.
- Given the production release workflow, when its steps are inspected, then root `npm ci` precedes release preflight and semantic-release execution in that order.
- Given the updated tooling, when commitlint and the offline semantic-release verifier are invoked, then both load and exit successfully without configuration or plugin errors.
- Given an available authenticated release context, when the release dry run executes, then it completes without publishing; otherwise the exact credential or remote-state blocker is recorded separately.

## Spec Change Log

- 2026-08-01: Human approved expanding the implementation from package overrides alone to direct semantic-release toolchain and narrowly necessary release-validation changes after npm bundled dependencies proved non-overridable. This avoids retaining a known-vulnerable bundled npm subtree while preserving the existing release contract.
- 2026-08-01: Temporary clean-room validation selected a scoped official-package alias for semantic-release's unused default npm plugin. The spec adds a governance test tying that alias to the repository's explicit plugin list so future configuration drift fails closed.
- 2026-08-01: Review loop 1 found that the working alias was not protected by PR root install/audit/runtime-config gates, active-config precedence checks, lockfile resolution assertions, or operational removal guidance. The spec adds those requirements to avoid green PRs that fail only post-merge or silently restore the vulnerable npm subtree. KEEP: zero-audit official-package alias, explicit four-plugin release lifecycle, CI-only publication, unchanged release behavior, and the proven 491-test baseline.

## Design Notes

The vulnerable paths are confined to release tooling: commitlint reaches `fast-uri` and `js-yaml`, while semantic-release installs `@semantic-release/npm` and its bundled npm tree even though `.releaserc.json` replaces the default plugin list and never uses npm publication. npm cannot override bundled dependencies. The scoped alias removes that unused tree without a custom fork or release-behavior change; the guard test makes its safety precondition explicit. If the explicit plugin list is removed, the aliased non-plugin causes a fail-closed configuration error rather than silently publishing through a different path.

## Verification

**Commands:**
- `npm ci` -- expected: reproducible root install succeeds.
- `npm audit --audit-level=low` -- expected: zero vulnerabilities.
- `npm audit --omit=dev --audit-level=low` -- expected: zero production vulnerabilities.
- `npm ls --all` -- expected: no invalid or extraneous packages; patched `fast-uri`/`js-yaml`; no `npm`, `tar`, or `brace-expansion` package.
- `npm run commitlint` -- expected: repository-pinned commitlint executes successfully.
- `npm run verify:semantic-release-config` -- expected: the real config and four-plugin lifecycle load offline with no npm publication plugin.
- `dotnet test tests/Hexalith.Memories.Cli.Tests/Hexalith.Memories.Cli.Tests.csproj --configuration Release` -- expected: release-governance assertions and the full CLI test project pass.
- `npm run release:dry-run` -- expected: no-publish release analysis succeeds when credentials and remote context are available; otherwise record the exact blocker.

**Observed:** Clean `npm ci`, both low-level audits, the complete npm tree, commitlint, the offline verifier, ten verifier negative cases, the independently clean Web E2E audit, and 493/493 CLI tests passed. The release dry run was not executed because quick-dev Step 3 prohibits remote operations; it was not reported as passing.

## Suggested Review Order

**Dependency graph design**

- Start with the exact toolchain pins and narrowly scoped security overrides.
  [`package.json:5`](../../references/Hexalith.Memories/package.json#L5)

- Confirm the unused npm plugin edge resolves to the reviewed official alias.
  [`package-lock.json:629`](../../references/Hexalith.Memories/package-lock.json#L629)

**Fail-closed configuration verification**

- Review the canonical branch, tag, plugin, command, and asset contract.
  [`verify-semantic-release-config.mjs:142`](../../references/Hexalith.Memories/tools/verify-semantic-release-config.mjs#L142)

- Inspect installed plugin loading without invoking lifecycle hooks.
  [`verify-semantic-release-config.mjs:208`](../../references/Hexalith.Memories/tools/verify-semantic-release-config.mjs#L208)

- Check the ten mutations proving configuration drift fails closed.
  [`verify-semantic-release-config.mjs:290`](../../references/Hexalith.Memories/tools/verify-semantic-release-config.mjs#L290)

**Blocking delivery gates**

- Ensure every pull request installs, audits, and verifies root release tooling.
  [`ci.yml:210`](../../references/Hexalith.Memories/.github/workflows/ci.yml#L210)

- Ensure production releases pass identical gates before preflight and publication.
  [`release.yml:82`](../../references/Hexalith.Memories/.github/workflows/release.yml#L82)

**Governance and operations**

- Pin manifest, lock, alias, and forbidden-package invariants in executable tests.
  [`CiTestInventoryTests.cs:177`](../../references/Hexalith.Memories/tests/Hexalith.Memories.Cli.Tests/Ci/CiTestInventoryTests.cs#L177)

- Protect configuration precedence and the exact four-plugin release behavior.
  [`CiTestInventoryTests.cs:260`](../../references/Hexalith.Memories/tests/Hexalith.Memories.Cli.Tests/Ci/CiTestInventoryTests.cs#L260)

- Verify CI and release gates remain unconditional and correctly ordered.
  [`CiTestInventoryTests.cs:323`](../../references/Hexalith.Memories/tests/Hexalith.Memories.Cli.Tests/Ci/CiTestInventoryTests.cs#L323)

- Close with ownership, validation, and the upstream removal trigger.
  [`release-runbook.md:71`](../../references/Hexalith.Memories/docs/dev/release-runbook.md#L71)
