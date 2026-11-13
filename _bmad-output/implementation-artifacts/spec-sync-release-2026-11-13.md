---
title: 'Synchronize current dependencies and repair release CI'
type: 'bugfix'
created: '2026-11-13'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '864c55a9820f7e819714a7e6d92cc3d6ee82a2fe'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="user explicitly authorized updates, push, release and bypassing human validation">

## Intent

**Problem:** Current main already selects the latest stable .NET SDK 11.0.100 and effective NuGet packages, but CI retains .NET 10 output and consumer assumptions. Root submodules have advanced upstream. Publication needs working automated checks against the final synchronized source.

**Approach:** Synchronize all ten root submodules, align remaining current framework/version assumptions, upgrade the available npm release patch, repair concrete CI failures, validate and push all changes, then dispatch Release and independently verify all eleven NuGet artifacts.

## Boundaries & Constraints

**Always:** Preserve existing work and historical evidence. Centralize NuGet versions in Builds. Keep exact-source CI, runtime, cleanup and publication checks meaningful. Record source selection under codex-agent delegation, without claiming human review or runtime qualification. Latest stable versions are the default; preserve deliberate historical compatibility test cohorts. Parent owns synchronization, signoff ledger, commits, pushes, hosted CI, and release; implementation agent makes local fixes only and does not commit. Submodule commits must be pushed before root gitlinks are recorded.

**Never:** Initialize nested submodules, load skills below references, weaken warnings or automated gates, invent approvals, print credentials, delete published releases, force push, or permanently remove environment protections. User approval checkpoints are waived for this maintenance run.

</frozen-after-approval>

## Code Map

- `global.json` and root submodule SDK pins already select latest stable11.0.100 (Microsoft release feed 2026-11-13); no artificial SDK edit needed.
- `references/Hexalith.Builds/Props/Directory.Packages.props` owns current effective package families. Registry audit `/tmp/parties-package-audit.json` finds all63 consumed stable NuGet/tool IDs current. Broader shared catalog updates are Conversations1.5.0 to1.6.0, Projects1.2.3 to1.2.4, and Swashbuckle.AspNetCore11.0.0 to11.0.1; make these in Builds owner and verify affected catalog controls after reading its DEVELOPMENT.md/configuration. Parent commits and pushes owner changes.
- `package.json`, `package-lock.json`: @semantic-release/github12.0.10 to12.0.11; keep Node24 engines and matching Node24 declarations.
- `Directory.Packages.props`: standalone fallbacks EventStore4.2.0, FrontComposer4.7.6 and Memories3.0.26 lag effective5.0.0,5.0.0,3.0.27. Preserve caller-selected catalogs.
- `tests/e2e/helpers/frontcomposer-assets.ts`: UI output hardcodes net10.0 while current host targets net11.0.
- `scripts/verify-eventstore-publish.py` and `scripts/test_verify_eventstore_publish.py`: publisher output assumes net10.0 while current EventStore targets net11.0.
- `scripts/validate-consumer-package-references.py`: generated smoke consumers still target net10.0; root packages target net11.0. Preserve intentional historical cases elsewhere.
- `references/Hexalith.Memories/Directory.Build.props`: target net10.0 conflicts with central JwtBearer11.0.0. Own minimal framework compatibility fix here, inspect owner guidance/build/tests first; retarget current owner projects to net11.0 and align active output assumptions as necessary. Do not initialize its nested dependencies; use published dependencies or explicit outer Builds catalog as required.
- `.gitlink-signoff.tsv`: parent adds exact fetched origin/main root revisions, including prior unsignoffed recorded pointers, with codex-agent source-selection provenance.
- `docs/ci.md`, `docs/architecture.md`: maintain active toolchain facts where fixes change them; retain dated historical evidence.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds/Props/Directory.Packages.props` -- update the three registry-confirmed shared catalog selections and necessary owner controls.
- [x] `package.json`, `package-lock.json`, `Directory.Packages.props` -- align latest patch and standalone package fallbacks, update directly affected assertions.
- [x] UI helper, publisher verifier and package consumer generator -- repair current net11 output and generated target selection with focused checks.
- [x] Memories owner build configuration and necessary active expectations -- restore current hosts with net11-compatible closure; leave owner changes uncommitted for parent.
- [x] Existing focused Python, TypeScript, Ci.Tests and affected owner checks -- run relevant validation, report exact outcomes and any remaining blockers.
- [ ] Parent operations -- synchronize source ledger, validate exact commit messages, push owner/root changes, require final hosted CI and range gate, dispatch release, verify release/package source metadata.

**Acceptance Criteria:**
- Given latest stable SDK and central versions, when SDK/package audit runs, then current pins match published stable releases or a documented intentional compatibility cohort.
- Given current net11 owned packages and hosts, when output inspection and consumer validation run, then paths and generated frameworks match actual artifacts.
- Given a synchronized final source, when automated CI and release execute, then required checks pass and all eleven manifest packages publish with that exact source revision.
- Given an external blocker, when its exact command fails, then evidence remains explicit and no publication or qualification is claimed.

## Implementation Notes

- Owning commits are pushed: Builds89487655de9e1c79848c5384261a8a2f009d2577 (catalog plus validated audit317/157/1), Memories9888a388f7129248ac3f2021bfb994ef9344ce59 (NET11 closure and compatible stable-validation fallback). Both sets of exact messages passed pinned commitlint. Memories maintainer push reported bypassed PR/three-check rules, as user authorized.
- Upstream Builds now supplies measured retained SDK4.2 views for exact Parties/Platform project names. Shared default remains5.0.0. Actual11 fresh local packages feed generated net11 client/portal consumers successfully:41 routes,41 schemas,28 intents. No local test package was uploaded.
- Final full builds for Parties and Memories passed with0 warnings/errors; all13 current retention/mechanism tests,493 Web tests and21 runbook/migration tests pass. Stable validation11 removes the observed preview advisory; an old original Builds catalog independently resolves the owner minimum fallback.
- All11 existing public3.1.1 archives were downloaded and their nuspec repository commits match tag source84e3df796ac977041a21ec7bd8e5789bdc44241a. This verifies existing publication only. Full registry audit, exact test commands, selected revisions, retained compressed receipts and pending release blockers are tracked under docs/implementation/sync-release-2026-11-13.

- Local implementation survived the server restart; no commit or push had occurred. Current Memories projects, prerequisite checks and active command paths now target net11.0. Retained explicit dependencies use the same no-pruning closure policy as Parties/Builds.
- Full standalone serialized Release/package build passes with zero warnings/errors. Publisher11 and generated consumer5 Python controls, UI typecheck, Builds catalog controls, Memories ci_alignment15 and coverage controls pass. Memories CLI build passes and19 prerequisite tests pass. Root Ci.Tests655/656 passed before UI assets were rebuilt; the failed security floor test is being rerun after the successful full build.
- Integration model test alias is guarded by its existing capability condition, matching the already-conditional body for unqualified SDK versions. Historical capability cohorts remain unchanged.
- Latest Platform source is not released and its tuple CI fails because signed Builds selection, recorded dependency pointers and same-source companions disagree. Parent requested clarification of nested-reference/prerequisite release scope; no such nested work has started.

## Spec Change Log

- Review patch pass aligns remaining active command/document framework paths. Full Memories package validation exposed implicit FrontComposer source selection despite package mode and a preview validation dependency; honor explicit source mode and select stable validation11 centrally. Existing historical cohorts and closed capability gates remain intact.

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| Blind1 EventStore5 capabilities | medium: effective catalog already selected5 before this change; exact5 capability gates remain intentionally closed pending real qualification, independently of source identity. | defer: prerequisite qualification |
| Blind2 retention output | medium: current build generatesnet11 while six active command paths still executenet10. | patch: align current paths |
| Blind3 contributor fallback | low: active Web fallback command still namesnet10. | patch: correct command |
| Blind4 active Parties docs | low: development and overview documents retain currentnet10 claims and commands. | patch: align current facts |
| Blind5 arbitrary publisher output | low: pre-existing fixed-layout helper supports the selected owner layout; generalized TargetPath support would add discovery behavior unrelated to the current owner CI repair. | reject: low-impact pre-existing enhancement |
| Blind6 generator regression coverage | medium: focused Python fixtures do not exercise both actual generators; run real fresh package consumer validation to cover their current emitted frameworks. | patch: execute actual consumer validation |
| Blind7 spec success claim | false: Verification lists required expected outcomes, while parent operations remain unchecked and implementation notes explicitly identify the pending release blocker. | reject |
| Blind8 pointer ledger | medium: source synchronization is pending parent work and exact new revisions need rows before root commit. | patch: parent provenance |
| Blind9 temporary evidence | medium: retain registry audit and exact verification outcomes in tracked implementation receipts before pushing. | patch: retain evidence |
| Verification1 retention output | medium: independently confirmed same six currentnet10 paths; existing compile and command construction prove a wrong executable after retargeting. | patch: same correction as Blind2 |


## Verification

- `dotnet --version` --11.0.100.
- Existing relevant Python unittest modules and UI `npm run typecheck` --pass.
- Package-mode serialized Release builds and individual Ci.Tests --pass, or exact first-failure evidence.
- `bash scripts/check-no-warning-override.sh` and `git diff --check` --pass.
- Final exact-source hosted CI/range gate and normal Release --success; independent NuGet verification binds every manifest package to source.
