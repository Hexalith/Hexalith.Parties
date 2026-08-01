---
title: 'Eliminate Memories release-tooling npm vulnerabilities'
type: 'bugfix'
created: '2026-08-01'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: '74e527f76c1fd859168d3f61bf1f4b28bcad837c'
context:
  - '{project-root}/references/Hexalith.Memories/_bmad-output/project-context.md'
  - '{project-root}/references/Hexalith.Memories/docs/dev/release-runbook.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Hexalith.Memories root release-tooling lockfile reports six transitive development dependency vulnerabilities: one moderate, four high, and one critical. Production dependencies and the separate Web E2E workspace audit clean, but the release toolchain must also install without known advisories.

**Approach:** Preserve the existing direct commitlint and semantic-release ranges, add narrow overrides for patched versions within their accepted major lines, and regenerate only the root lockfile. Prove the resulting install is reproducible, audits clean, and still executes commitlint and semantic-release.

## Boundaries & Constraints

**Always:** Work only in the `references/Hexalith.Memories` repository and limit product changes to its root `package.json` and `package-lock.json`. Keep all current direct development dependency ranges. Resolve `fast-uri` to `3.1.5`, `js-yaml` to `4.3.1`, and the `npm` subtree to `npm` `11.18.0`, `brace-expansion` `5.0.9`, `tar` `7.5.22`, and `undici` `6.28.0`. Preserve the independently healthy `@semantic-release/github` dependency on `undici` 7.x. Keep the lockfile at version 3 and validate from a clean `npm ci` install.

**Ask First:** Stop for approval if zero vulnerabilities requires a direct dependency major upgrade, npm 12, a change to Node engine policy, edits outside the two root package files, or any release configuration or application-code change.

**Never:** Do not run `npm audit fix --force`, suppress or lower audit severity, add broad unscoped downgrades, modify the clean `tests/Hexalith.Memories.Web.E2E` manifest or lockfile, initialize nested submodules, or change generated/build artifacts. Do not claim the release dry run passed when credentials or remote state prevent it.

</frozen-after-approval>

## Code Map

- `references/Hexalith.Memories/package.json` -- root release-tool declarations and the location for scoped security overrides.
- `references/Hexalith.Memories/package-lock.json` -- reproducible root dependency graph consumed by release CI through `npm ci`.
- `references/Hexalith.Memories/commitlint.config.mjs` -- commit-message policy exercised by the upgraded tool graph.
- `references/Hexalith.Memories/.releaserc.json` -- semantic-release plugin configuration whose loadability must remain intact.
- `references/Hexalith.Memories/.github/workflows/release.yml` -- CI consumer of the root lockfile; no edit expected.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.Memories/package.json` -- add exact security overrides for the six vulnerable transitive packages while retaining every direct dependency declaration.
- [ ] `references/Hexalith.Memories/package-lock.json` -- regenerate the root dependency graph from the updated manifest and verify that only intended dev-tooling resolutions change.
- [ ] `references/Hexalith.Memories/` -- perform a clean root install, audit all and production-only dependencies, inspect the resolved vulnerable-package paths, and smoke-test commitlint plus semantic-release.

**Acceptance Criteria:**
- Given the updated root manifest and lockfile, when `npm ci` runs, then it completes without manifest/lockfile drift or install errors.
- Given the clean installed tree, when `npm audit --audit-level=low` runs, then it exits successfully with zero vulnerabilities at every severity.
- Given the same tree, when `npm audit --omit=dev --audit-level=low` runs, then it confirms zero production vulnerabilities.
- Given the overridden graph, when the affected packages are listed, then npm resolves the exact patched versions and reports no invalid or extraneous dependency state.
- Given the updated tooling, when commitlint and the semantic-release executable are invoked, then both load and exit successfully without configuration errors.
- Given an available authenticated release context, when the release dry run executes, then it completes without publishing; otherwise the exact credential or remote-state blocker is recorded separately.

## Spec Change Log

## Design Notes

The vulnerable paths are confined to release tooling: commitlint reaches `fast-uri` and `js-yaml`, while semantic-release reaches the bundled npm subtree containing `brace-expansion`, `tar`, and `undici`. Upgrading npm alone is insufficient because published npm 11.18.0 and 12.0.2 tarballs still carry vulnerable bundled versions of `tar` and `brace-expansion`; nested npm overrides are therefore required. Staying on npm 11 avoids an unnecessary Node-engine policy change.

## Verification

**Commands:**
- `npm ci` -- expected: reproducible root install succeeds.
- `npm audit --audit-level=low` -- expected: zero vulnerabilities.
- `npm audit --omit=dev --audit-level=low` -- expected: zero production vulnerabilities.
- `npm ls npm tar brace-expansion fast-uri js-yaml undici` -- expected: patched versions, with no invalid or extraneous packages.
- `npm run commitlint` -- expected: repository-pinned commitlint executes successfully.
- `npm exec semantic-release -- --version` -- expected: semantic-release loads and prints its version.
- `npm run release:dry-run` -- expected: no-publish release analysis succeeds when credentials and remote context are available; otherwise record the exact blocker.
