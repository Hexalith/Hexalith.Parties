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

**Authorized continuation:** On 2026-11-13 the user explicitly authorized necessary Platform nested dependency repairs and matching prerequisite publication. Limit nested checkout/reference changes to this release path; preserve other active task work and use exact recorded revisions without recursive updates.

**Never:** Initialize unrelated nested submodules, load skills below references, weaken warnings or automated gates, invent approvals, print credentials, delete published releases, force push, or permanently remove environment protections. User approval checkpoints are waived for this maintenance run.

</frozen-after-approval>

## Code Map

- `global.json` and root submodule SDK pins already select latest stable11.0.100 (Microsoft release feed 2026-11-13); no artificial SDK edit needed.
- `references/Hexalith.Builds/Props/Directory.Packages.props` owns current effective package families. Registry audit `/tmp/parties-package-audit.json` finds all63 consumed stable NuGet/tool IDs current. Broader shared catalog updates are Conversations1.5.0 to1.6.0, Projects1.2.3 to1.2.4, and Swashbuckle.AspNetCore11.0.0 to11.0.1; make these in Builds owner and verify affected catalog controls after reading its DEVELOPMENT.md/configuration. Parent commits and pushes owner changes.
- `package.json`, `package-lock.json`: @semantic-release/github12.0.10 to12.0.11; keep Node24 engines and matching Node24 declarations.
- `Directory.Packages.props`: current evaluated Parties consumers deliberately retain the authenticated finite SDK4.2.0/OpenAPI2 view; the shared unknown-consumer SDK5.0.0 default remains unqualified. Align standalone fallbacks with the actual named consumer graph (FrontComposer5.0.0, Memories3.0.27) and preserve caller-selected catalogs.
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

- User authorization received for nested Platform dependency repairs and prerequisite release. Continue through reviewed finite catalog helpers from upstream Platform3de642ba and Parties84c25dac, preserve concurrent Story2.12/Folders changes, and wait for the Builds owner to deliver the independently verified host-compatible successor before binding any tuple. The Builds task remains sole publisher for that repository.

- Full browser gate81/81 passes. Hosted current Memories consumer fixture failure is repaired at1d94e17a3492ebbfca5f6ea181a44841c8a89473; all28 package controls pass. Its remaining source-debug failure is pre-existing FrontComposer source targetnet10 versus OpenIdConnect11 from current catalog, reported to the active owner task. Root gate is green; exact-source main CI remains queued behind an older cancelled runtime job. A force-cancel was requested for that superseded job, which had no started steps.

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

### SDK 11 evaluator isolation repair

The unchanged parallel catalog tests repeatedly failed with exit1 and empty
diagnostics while focused and serialized evaluation passed. Diagnostic direct
and supervised parallel queries observed shared MSBuild server connection
fallbacks. SDK11 now enables the shared server by default; bounded package
process supervisors intentionally reap their owned descendant trees. The
MSBuild evaluator therefore explicitly disables shared server use for each
query, after environment isolation, without adding retries, changing assertions
or serializing the suite. The same real parallel cases and broader assembly
will verify the repair. Original failed runs remain retained as evidence.

### Final review scope and remaining producer gate

Review the whole root maintenance diff from864c55a9, including published
FrontComposer-owner current-output/consumer fixes preserved at4aea34e0 and
84c25dac. The new owned evaluator repair passes15 parallel catalog cases and
2838 domain cases (14 explicit external-runtime skips), while thirteen other
unit projects pass3002 cases with one existing skip. Full664 Ci.Tests and the
standalone solution already passed before this last evaluator change; final
source data requires a committed root tuple and clean direct dependencies.

Prepared consumer code in the Platform owner is independently reviewed, with
three review patches implemented. Its real signed producer successor is now
Builds source9bbe8380 in normal CI43445886145; no package/release identity has
been invented. Parent must verify its normal signed release, bind it atomically
to Platform, regenerate fresh companions, pass unchanged automated source
qualification and normally release Platform. Final root CLI/gitlink/source
observation fields will then adopt that qualified tuple before root commit,
exact-source CI/range gates and normal publication. Current intermediate
5.2.1/Platform observations are preparation data, not final qualification.

## Review Triage Log

All three root review layers completed before triage: blind hunter10 findings, edge-case hunter1, verification reviewer1 Other finding. Full review input is `/tmp/parties-root-final-review.diff` from baseline864c55a9; independently pushed owner changes are preserved.

| ID | Verdict | Route | Evidence and resolution |
| --- | --- | --- | --- |
| RB1 | medium | patch (publish-boundary group) | The hosted profile checks only `IsPublishing`; a real CLI invocation uses `_IsPublishing`, allowing the private NET11 override during publish. The existing test manually sets a different property. Guard the actual CLI signal as well and exercise real publish without that injected stand-in, preserving ordinary owner props. |
| RB2 | medium | patch | `validate_framework` accepts a matching TFM label without authenticating its shared-framework declarations. An incompatible `.NETCore.App10`/`net11.0` runtime configuration can be certified despite being unrunnable. Validate required framework identity/version generations and extend the real receipt refusal controls with otherwise coherent malformed/incompatible runtime configurations. |
| RB3 | medium | defer (pre-existing) | The owner publisher historically derives a DLL name from the fixed project stem; the evaluated TargetPath change preserves that old restriction. A custom AssemblyName is valid MSBuild but unsupported by this helper. All current fixed owner projects use the default name, so this is not a current release blocker; retain as a separate helper-capability limitation rather than expanding this repair. |
| RB4 | medium | patch | The actual preparation/compiler regression covers inherited net10 only; restore and synthetic-output proofs do not establish the NET11 profile through compilation. Extend the existing real compiler/preparation case with NET11 consuming actual locally packed NET10 fixtures and completing its receipt, retaining missing-contract refusal and NET10 coverage. |
| RB5 | medium | patch | Named substitute `.proj` probes establish the finite catalog-name rule but cannot detect overrides/import order in the forty actual owning projects. Keep those positive/unknown-name probes and evaluate all actual project files under the real Release package context. |
| RB6 | medium | patch (parent final-source group) | Current intermediate Platform constants/matrix record79 while actual prepared owner is341. Parent must atomically align SHA, anchored describe, matrix and final committed root pointer to the genuinely qualified final source before the committed-source class runs. No relaxed assertion is allowed. |
| RB7 | medium | patch (parent ledger) | No final prepared Platform source is signed off yet. This intentionally awaits actual final qualification; append the genuine remote-verified final source provenance before root commit and prove both push-range and release-floor RC gates. |
| RB8 | medium | patch (parent history) | Relative to initial baseline864, four rows in the newly archived Nov13 observation had been replaced during the first synchronization. Restore that original dated observation and retain the already published intermediate table separately with an explicit intermediate heading; preserve every prior receipt and approval. |
| RB9 | low | patch (parent evidence index) | Several status log references omit the actual `checks/` path and `.gz` suffix; the old five-case consumer capture may be missing. Resolve every evidence path to retained bytes, capture available original logs, and explicitly distinguish superseded/unretained historical observations from the existing current eight-case receipt. |
| RB10 | medium | patch (parent final audit) | The registry file is the genuine initial9:35 audit and must retain its original data. Label its scope as initial and add a separate final evaluated consumer audit with actual final source/tool/npm identities and explicit SDK4.2/OpenAPI2/Memories3.0.27 compatibility exceptions. Fresh official63-index observations at14:54 already identify newly published Memories4.0.0 and the CLI successor separately. |
| RE1 | medium | patch (parent final-source group) | The edge-case layer independently finds the same79-versus341 source-observation mismatch as RB6. Align all final records to the actual qualified committed pointer; preserve strict identity tests. |
| RV1 | medium | patch (publish-boundary group) | Verified Other finding reproduces actual `dotnet publish` with `_IsPublishing=true`, empty `IsPublishing`, and unintended NET11 output. The same real-command guard/test repair required by RB1 closes this issue. |

Required remote producer/Platform/root qualification and publication remain pending. No human checkpoint is required or invented.

### Final reviewed implementation and source selection, 2026-11-13

The preceding preparations retain their dated facts. Both genuine Builds5.2.2
packages at02ed624aa19781cac75d2c9112940e9bd830441a are publicly verified:
repository signatures, catalogSHA512, source metadata and all354 original
publisher members match, with only the repository signature added. Platform
sourceb76e0c7c70162653bbff6a8263ebfc4500c1631b atomically consumes the typed
API, exact tool/range/binding and measured21st complete validation pair. All
historical pairs and crossed-pair refusals remain intact. Fresh immutable
companions preserve the genuine349/412 obligations and real17-owner reader.
The clean final standalone source builds with0warnings/errors and passes all24
affected controls, including the actual17 current artifact cases, with0skips.
Main is pushed; exact hosted tuple43462834314 and catalog43462834460 remain
required before normal qualified source publication.

RB1/RV1, RB2, RB4 and RB5 are implemented and verified by the meaningful
publish/compiler/runtime-framework/actual40-project controls. Full final Ci.Tests
passes665/665. The domain assembly excluding only the pending committed-source
class passes2838 with14 existing external-fixture skips. Thirteen other unit
projects pass3002 with one existing skip. After the genuine5.2.2 tool pin,
71 current container/publication controls pass, and the full solution against
the released catalog builds without warnings/errors. RB8 preserves the initial
Nov13 observation and separately labels the first synchronization. RB9 resolves
all captured log references to retained compressed evidence. RB3 remains the
explicit pre-existing custom-AssemblyName helper limitation in deferred-work.

RB6/RE1 and RB7 now use the actual remote-verified Platformb76 source, matching
current constants, anchored descriptors, matrix and source-only provenance.
The root also preserves latest EventStore69b244d9, McpCli491f1148 and Builds
CI-guardfde085ee sources. The latter's catalog bytes are exactly equal to the
signed02ed producer, while the Platform tuple remains fixed at02ed. All ten
root owners are clean and remote-main verified. The17 authorized Platform
prerequisites were clean/exact before deinitialization; no nested owner remains
initialized in the umbrella workspace.

RB10 is resolved by final-evaluated-audit.json from the actual selected project,
tool manifest, package lock, owner revisions and fresh official registries. The
initial9:35 registry observation remains unchanged and labeled initial. The
current SDK is latest stable11.0.100. Exact SDK4.2/OpenAPI2 and Memories3.0.27
cohorts remain disclosed holds. The catalog selects Dapr hosting13.6.2-beta.959
for Aspire13.6; stable13.0.0 is older, and17.x betas require another toolchain.

The strict committed-source class must run against the final root commit.
Final exact-source rootCI/range proof, Release and independent verification of
all eleven newly published packages remain pending. No human checkpoint is
required; automated gates and original Story/acceptance boundaries are retained.

### Exact producer pin correction after hosted preflight

Hosted source8a900c2f correctly refuses before tests: HXW009 requires both the
indexed and actual root Builds gitlink to equal Platform's published02ed pin,
even when laterfde catalog bytes are identical. HXW011 independently requires
the normal qualified Platform release tag, which is still pending. Restore the
Root Builds selection to genuine released5.2.2 at02ed, preserving the newer
unreleased guard work on owning main. No guard or source proof is weakened.
Wait for actual Platform publication and fetch its real tag before the corrected
Root CI push. The prior8a observation and failure remain retained as history.
