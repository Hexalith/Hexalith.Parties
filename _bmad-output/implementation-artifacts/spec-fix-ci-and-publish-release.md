---
title: 'Restore green CI and publish the Parties release'
type: 'bugfix'
created: '2026-10-07'
status: 'in-review'
route: 'dispatch'
baseline_commit: '45d653a7d7f14a75d5227fbd780db3fbf22f70fe'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/docs/ci.md'
  - '{project-root}/docs/build-gate.md'
---

<frozen-after-approval reason="human-owned intent — bypass of human validation explicitly requested">

## Intent

**Problem:** Current main fails CI compilation before any test suite runs. The retained actor-history implementation consumes EventStore interfaces and stream contracts absent from the root EventStore 3.113.0 package selection. The repository therefore cannot publish its intended release through the normal verified source path.

**Approach:** Align the root dependency with the already published EventStore 3.115.0 release that supplies the retained-history APIs. Fix any resulting compilation, test, or workflow regressions with focused changes, validate all configured test projects, and produce a reviewed patch. After implementation review, the parent agent records the necessary commit, runs GitHub CI, dispatches Release using its supported validation bypass, handles the existing production review under the user's authorization, and independently verifies every manifest NuGet package on nuget.org.

## Boundaries & Constraints

**Always:** Preserve existing behavior, retained actor-history functionality, package inventory, container inventory, user changes, warnings-as-errors, package-mode CI, and root-only submodule boundaries. Inspect guidance before modifying files. Prefer the smallest dependency alignment over domain rewrites. Use individual test project execution under Microsoft.Testing.Platform. Report exact validation blockers without treating source-mode passes as package-mode proof. Keep shared build catalog source unchanged; select EventStore centrally before its import. All user checkpoints are bypassed under this request; this does not weaken automated validation.

**Never:** Remove retained-history logic to regain compilation, change test expectations to hide a production defect, initialize nested submodules, change repository-wide release protection, claim publication from a tag alone, or introduce inline PackageReference versions. Implementation handoff ends at a locally verified patch; parent handles Git publication and Release. Subagent must not render bmad-build again, push, commit, or trigger remote operations.

</frozen-after-approval>

## Code Map

- `Directory.Packages.props` sets HexalithEventStoreVersion to 3.113.0 before importing the shared Builds catalog; this one property governs the whole EventStore package family.
- `src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs` consumes IRetainedIdentityHistoryReader; `RetainedHumanActorHistoryFold.cs` consumes RetainedIdentityHistoryStream. CI 37589655038 reports CS0246 for these two types. Published Client 3.115.0 XML documentation proves the interface exists.
- `docs/ci.md` describes the effective dependency and manual release process; update current package statements without rewriting historical owner approvals.
- `scripts/test.ps1` inventories eleven unit projects, two service integration projects, one topology project, and one CI project. `tests/README.md` documents the lanes and direct execution fallback.
- `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs` and `PartiesContainerPublishWorkflowTests.cs` inspect effective build/package metadata; keep their invariants intact.
- `Hexalith.Parties.slnx` is the configured CI solution. Release builds consume external NuGet dependencies except documented unpublished Commons support seams and solution orchestration dependencies.
- `.github/workflows/release.yml` supports bypass-validation=true, selecting exact-source Commitlint proof. Production still has a required reviewer; authenticated account jpiquot is eligible and self review is permitted. The parent handles dispatch and approval without modifying environment policy.
- `tools/release-packages.json` lists nine packages; `release.config.cjs` packs, validates, publishes them plus three containers, then creates the GitHub Release. Preserve the manifest and collision checks.
- `RELEASE-CHECKLIST.md`, `.gitlink-signoff.tsv`, and `scripts/gitlink-rc-gate.sh` require owner selection for committed root pointers that differ from the prior release. The current release instruction authorizes selection of the existing pointers without implying wider platform parity.
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs` disables Keycloak for symmetric-JWT tests; override the test resource authentication settings after AppHost model construction. `Gateway/EventStoreGatewayE2ETests.cs` must report an unavailable fixture as an explicit skip with its actual reason.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props` -- select EventStore 3.115.0 centrally and explain that retained-history consumers require this floor.
- [x] `docs/ci.md` -- update the effective current EventStore package description and relevant stale catalog/source statements to verified facts.
- [x] `.gitlink-signoff.tsv` -- record the five existing committed root pointers missing release-selection signoff, tied to jpiquot's 2026-10-07 release instruction; preserve gitlinks and historical approvals, then pass both RC gate modes.
- [x] Existing `src/` and `tests/` failure sites -- repair additional compilation/test failures exposed by the authoritative build and all-project lane, only when evidence requires a change.
- [x] Aspire topology fixture and gateway test -- restore the intended test-only symmetric JWT settings and replace the unavailable-fixture silent pass with a dynamic skip; record actual local runtime blockers separately.
- [x] `Hexalith.Parties.slnx`, `scripts/test.ps1`, package validation scripts -- run the Release CI parity build, all runnable test projects, and applicable consumer/package validation; record results in implementation notes. The parent may complete lengthy final validation after handoff.

**Acceptance Criteria:**
- Given the current retained-history implementation, when the effective external EventStore package graph is restored and built, then its consumed APIs resolve without removing functionality.
- Given the configured test inventory, when each project runs, then all runnable checks pass and any environmental skip or blocker is reported separately.
- Given the final patch, when independent reviewers inspect it, then no unresolved high or medium regression remains before publication.
- Given a clean final commit after local validation, when the parent runs CI and Release, then CI is green and the nine exact release package versions are independently observable on nuget.org.

## Implementation Notes

Parent investigation: clean main at the baseline SHA; all root gitlinks match the index; local gitlink gate passes. Published EventStore Client 3.115.0 contains the missing interface. An investigative serialized Release build with HexalithEventStoreVersion=3.115.0 is running under parent session 93504, log /tmp/parties-release-build.log. Do not run another build concurrently until parent reports completion.

Implementation: parent confirmed investigative build completion (exit 0; zero warnings/errors). The central version now selects 3.115.0 before the catalog import. The selected Builds catalog actually defaults to 3.110.0, and the retained EventStore source identity is `48ef7171b9532f390b7b41b61ba679ba1030c923` (`v3.115.0-5-g48ef7171`), so `docs/ci.md` now reports package and diagnostic source identities separately.

Exposed release gate: parent ran `bash scripts/gitlink-rc-gate.sh --diff v1.1.1` and found missing signoff for current committed Builds, EventStore, FrontComposer, Memories, and Tenants pointers. Under the user's explicit release authorization, the ledger now records these exact selections with owner jpiquot and date 2026-10-07; it does not claim platform parity or discharge earlier migration gates. Both `--worktree` and `--diff v1.1.1` pass locally.

Whitespace verification: the changed `.props` lines preserve the CRLF required by `.editorconfig`. Plain `git diff --check` reports their carriage returns as trailing whitespace because no root `.gitattributes` or `cr-at-eol` Git setting is present. Use `git -c core.whitespace=cr-at-eol diff --check` to check the intended line endings without altering repository configuration.

Additional service regressions: the first all-project lane completed with 14/15 projects passing (2,727 tests: 2,714 passed, 7 failed, 6 existing topology health skips). EventStore 3.115.0 prefers `IAsyncDomainProcessor`, bypassing Parties' sync-only wrapper registration. `PartyDomainProcessor` now implements that existing async seam, and DI selects the wrapper for both contracts/case variants, retaining validation, protection, and erasure behavior. Capturing endpoint fixtures override both contracts. The wrapper adapts only its redacted in-memory replay copy to JSON accepted by the SDK, retaining stored protection metadata; the destroyed-key test additionally checks that the supplied protected stream remains intact. Existing endpoint/privacy expectations are preserved. The endpoint/processor classes passed 25/25 after these fixes.

Historical fitness reconciliation: `PlatformApiPrerequisitesTests` now separates current release-selection expectations from the original 2026-10-05 migration/a11y constants and required rows. The matrix has a distinct 2026-10-07 selection section; dated approval and parity receipts retain their original identities. All G5 structural/status/enrollment/rollback checks remain active. Current FrontComposer delegates projection/lifecycle announcements to its polite `FcSurfaceStatus`; two stale structural commands now inspect that actual composition without claiming runtime a11y parity. The three prerequisite/closure/documentation fitness classes passed 61/61, zero skips. Fresh evidence is appended to `_bmad-output/implementation-artifacts/tests/test-summary.md`.

Local package verification: all nine manifest packages packed at `0.0.0-ci-test` and passed metadata validation. The configured isolated client/portal consumer smoke built both with zero warnings/errors. That script locally packs support source packages; external EventStore package correctness is proved separately by the authoritative package-mode solution build.

Runtime baseline and topology limitation: the local Aspire skills were applied before C# edits. `aspire start`, `describe`, and `wait parties` showed healthy Parties/EventStore/UI/Keycloak and sidecars, with the Tenants host `Finished`; the started app was stopped before edits. The start command also stopped the owned topology test instance recognized by Aspire as the previous AppHost. Its initial reported pass is not credited as live gateway proof because the existing gateway test returns early when its fixture is unavailable. Parent must perform a clean topology rerun alongside final service/all-project validation before publication.

Fresh topology repair and evidence: the clean rerun exposed missing JWT credentials when the fixture disabled Keycloak while the normal AppHost cleared symmetric signing keys. The fixture now overrides Authority, SigningKey, Issuer, and RequireHttpsMetadata on its test resources after model construction, sharing its test key/issuer with the gateway token. Production auth guards and AppHost policy are unchanged. The gateway test now calls `Assert.Skip` with `UnavailableReason` instead of returning silently; its command/status/event assertions remain intact. The integration-project Release rebuild passed with zero warnings/errors (3.95 s; `/tmp/parties-release-topology-patched-build.log`). Its full rerun completed with **35 passed, 7 skipped, zero failed** (3m 16s; `/tmp/parties-release-topology-patched-tests.log`, TRX in `/tmp/parties-release-topology-patched-results`). Six skips are the existing declared health deferrals. The gateway's new explicit skip reason is `TimeoutException: Endpoint did not become ready within 00:03:00. Url: /health. Last status: ServiceUnavailable. Last error: n/a`.

Read-only Aspire observation confirmed Parties, EventStore, and EventStore Admin reached Running/Healthy after the JWT repair. Dapr sidecars independently exited while adding file-watch targets with `no space left on device`; the parent confirmed 38 GB free disk space and file-watch resource exhaustion. Tenants' dynamically launched package-mode project build also failed CS0246 for `IDomainServiceAdministratorVerifier` and `DomainServiceAdministratorClaim`. Both type names are absent from the published EventStore DomainService 3.115.0 package DLL and present only in the checked-out newer source. Forwarding the central version would not supply them. No source-reference workaround, submodule edit, gitlink update, or production-policy change was made. Live gateway proof remains unavailable locally; fresh-runner GitHub CI is the authoritative remaining topology gate. The parent separately reported the full service project passing **696/696**.

## Spec Change Log

## Review Triage Log

Review execution: all three lenses completed. The runtime rejected additional context-free threads (`agent thread limit reached`); the blind reviewer was fresh, while edge-case and verification-gap reviews reused the investigation and implementation threads. Their results are recorded individually below; this limits reviewer independence.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1: topology startup becomes a successful skip | high | The fixture catches every startup exception and the gateway skips before assertions; the recorded 503 timeout exits successfully. Patch the gateway to fail on GitHub Actions while retaining explicit local skips. |
| Blind 2: Tenants runtime compilation mismatch | medium | The dynamically launched upstream Tenants source requires two administrator contracts absent from its published SDK selection; neither root gitlinks nor upstream source were changed by this patch. Defer this pre-existing upstream mismatch and report it separately; do not credit a fresh runner as its remedy. |
| Blind 3: external runtime graphs omitted from fitness inventory | medium | The inventory already restricts itself to owned src/samples/tests, leaving the upstream Tenants runtime graph outside compilation coverage. Defer that pre-existing orchestration verification gap. |
| Blind 4: spec all-project command versus reconciled results | low | The initial all-project run had seven service failures; later focused/full service and topology reruns reconcile the inventory. Reject because the proposed fix edits this build's spec; exact executed results remain in test-summary. |
| Blind 5: async cancellation coverage | medium | Existing processor cases use CancellationToken.None and production endpoint cases do not cancel; a None-forwarding mutation would survive them. Patch with canceled-token execution through the production keyed async processor. Wider mid-unprotection behavior predates this seam. |
| Blind 6: combined HTTP protection/erasure case absent | low | The production endpoint verifies wrapper selection and the same wrapper's existing protected-history test verifies lifecycle behavior; the combined HTTP case is absent before and after this patch. Reject the additional complex fixture setup as negligible beyond the existing component coverage. |
| Blind 7: negative replay cases absent | false | Tampered ciphertext/base64 and malformed marker tests already cover the security implementation. The wrapper catches only destroyed-key exceptions; other faults escape before aggregate/status writes. The redaction helper serializes its output as valid JSON; the change normalizes only its exact json-redacted marker, leaving unsupported formats unchanged. |
| Blind 8: input immutability assertion incomplete | low | A marker and serialization-format assertion would miss changes to other payload bytes or metadata. Patch the existing test to compare full original bytes and metadata. |
| Blind 9: command status is not a direct stored-stream read | medium | The gateway checks Completed, eventCount, and aggregateId but never reads the stream. This test and its persistence assertion predate the patch. Defer a direct stored-stream verification enhancement; preserve all existing assertions. |
| Blind 10: current-selection SHA membership can swap rows | medium | New whole-section membership checks allow one dependency's identity to appear in another dependency's row. Patch the current-section assertion to bind each SHA to its named row. |
| Edge 1: unavailable application topology passes CI | high | Same concrete catch/skip root cause as Blind 1. Patch the required CI invocation to fail with its actual initialization reason. |
| Edge 2: current-selection identities can swap rows | medium | Same new table-membership defect as Blind 10. Patch named-row association. |
| Edge 3: test factory accepts sync-only processor then casts | false | This private factory has only two substitute implementations and both now implement IAsyncDomainProcessor; every actual factory caller supplies one of those types or selects production mode. A sync-only caller is not reachable in the test inventory. |
| Verification 1: topology startup regressions become CI skips | high | Pre-verified: the fresh TRX is NotExecuted while its required project exits successfully, and the workflow does not inspect gateway execution. Patch the gateway's GitHub Actions path to require execution. |
| Verification 2: production async cancellation unverified | medium | Pre-verified: no existing owned test cancels the production keyed async processor. Patch a canceled-token assertion using actual production DI registration. |

## Verification

- `dotnet build Hexalith.Parties.slnx --configuration Release -m:1 -p:UseNuGetDeps=true -p:UseHexalithProjectReferences=false` -- zero errors and warnings without dependency override after the patch.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory /tmp/parties-release-tests` -- all fifteen runnable test projects pass; report skips.
- `python3 scripts/pack-release-packages.py /tmp/parties-release-packages 0.0.0-ci-test`, `python3 scripts/validate-nuget-packages.py /tmp/parties-release-packages`, and `python3 scripts/validate-consumer-package-references.py /tmp/parties-release-packages --work-directory /tmp/parties-release-consumers` -- local package inventory, metadata, and isolated consumers satisfy the configured release gates.
- `bash scripts/check-no-warning-override.sh` and `git -c core.whitespace=cr-at-eol diff --check` -- guards and whitespace pass.
- `bash scripts/gitlink-rc-gate.sh --worktree` and `bash scripts/gitlink-rc-gate.sh --diff v1.1.1` -- root working-tree and release-selection gates pass.
- Parent: validated Conventional Commit, push CI proof, Release with bypass-validation=true and existing reviewer approval, verify all nine manifest package nuspec identities and source metadata from nuget.org downloads.

Implementation handoff: final authoritative package-mode solution build passed with zero warnings/errors (13.72 s; `/tmp/parties-release-build-final.log`). Combined final rerun of endpoint, processor, prerequisite, closure, and documentation classes passed 86/86 with zero skips (8.393 s; `/tmp/parties-release-final-focused-tests.log`), and the parent reported full service 696/696. The patched topology's 35 passes and 7 explicit skips are recorded above, including the unavailable live gateway proof. The patch is ready for independent review; final all-project reconciliation, fresh-runner GitHub CI, and remote publication remain parent-owned.

Parent verification: full final service project passed 696/696 with zero skips; clean patched topology completed 35 passed and seven explicit skips, with the local Dapr watcher and unpublished Tenants SDK dependency limits recorded. Initial passing unit/component/CI projects, final service rerun, patched topology rerun, final solution build, and package consumer checks form the complete local inventory evidence. npm audit signatures passed all 499 packages (120 attestations). Remote CI/release gates remain pending.

Review patch verification: both changed test projects built with zero warnings/errors; the three affected service classes passed 42/42 (8.434 s; `/tmp/parties-release-review-fix-focused-tests.log`). A real gateway execution with `GITHUB_ACTIONS=true` returned exit 1, one failed and zero skipped with the existing `/health` 503 timeout (193.788 s; `/tmp/parties-release-review-fix-gateway-ci-test.log`), proving that the required CI lane can no longer turn startup regressions into successful skips. This deliberate negative check is not a passing gateway receipt. The parent post-review solution build passed with zero warnings/errors in 13.61 s. Remote CI and Release remain pending.

Parent full post-review service verification: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-build --results-directory /tmp/parties-release-post-review-service-results --report-xunit-trx --report-xunit-trx-filename Hexalith.Parties.Tests.trx` passed **697/697**, zero skips, in 18.867 s.

GitHub CI first patch (`bed34240206af063d705325cf9f3f5b17f635c7a`, run 37596156512): Release build, package-consumer validation, all unit projects, sample/CI projects, Commitlint, CodeQL, and the separately dispatched release-candidate gate passed. One service performance assertion failed: semantic search at 10K entries took 631 ms against its unchanged 500 ms threshold. Aspire was skipped because the blocking service lane failed. The semantic benchmark class is being placed in the existing non-parallel LocalSearchPerformanceCollection, matching its neighboring benchmark suite. Release run 37596391623 passed source/registry-floor preflight and waited for production approval; it was canceled before approval/publication because the source is being superseded. Upstream SDK publication scope remains an asynchronous user clarification; no upstream mutation or publication is authorized by an unanswered selection.

Semantic benchmark isolation verification: the affected service project Release build passed with zero warnings/errors (7.39 s); both benchmark classes and semantic provider behavior tests passed **30/30**, zero skips (1.830 s). The unchanged 10K semantic threshold measured 84 ms (exact/multi-token) and 40 ms (fuzzy). The complete service suite then passed **697/697**, zero skips (20.213 s; `/tmp/parties-release-benchmark-service.log`). Only the class collection attribute changed; production search and all performance/result assertions remain intact.

### Remote gates and publication blockers — 2026-10-07

Final source: `9096ea357fd627782828d671a6860c9eabe99c89` (main, pushed).

- [CI 37597236336](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597236336): the build-and-test job passed its Release warning-as-error build, isolated consumers, and **2,686/2,686** tests across eleven unit projects and three service/sample/CI projects, zero skips. Its separate required Aspire job failed: **35 passed, six existing health skips, one gateway failure** after `/health` returned 503 for three minutes. The fresh runner's Tenants launch log independently reports CS0246 for `IDomainServiceAdministratorVerifier` and `DomainServiceAdministratorClaim`. These unpublished SDK dependencies prevent a green full CI result; the new guard reports the failure instead of silently passing or skipping it.
- [Commitlint](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597236412), [CodeQL](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597236050), and [release-candidate gate](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597241012) passed for that exact source.
- [Release 37597780819](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597780819) used the supported `bypass-validation=true` input and the user's authorized production approval. Source/registry-floor preflight, npm signature verification, restore, Release build, nine-package metadata checks, isolated consumers, and publication preflight passed. NuGet then rejected the first upload with **HTTP 403**: `The specified API key is invalid, has expired, or does not have permission to access the specified package.` No GitHub Release was created and container publication did not run after that rejection.
- Independent verification of all nine NuGet indexes found **0/9** version `1.2.0` packages. `/tmp/parties-nuget-publication-1.2.0.json` contains each package's absence result. All nine indexes were queried again before cleanup.
- The failed attempt had created `v1.2.0` at the exact source above. After confirming that source, absent GitHub Release, and absent versions for all nine packages, the generated tag was removed. This restores a retry at `1.2.0`; no prior published tag was changed.
- Credential metadata: Parties has no repository or production-environment NuGet key override. It inherits organization `NUGET_API_KEY` (last updated 2025-09-20); no local NuGet key is available. A valid repository-level key with push permission for the nine Parties package IDs is required. Its value was neither read nor printed.
- Pending user input: repository credential update, plus whether this task may extend to fixing/releasing the upstream EventStore SDK (14 additional packages). An unanswered choice does not authorize that additional publication. No upstream repository was changed or published.

Historical recovery guidance is superseded by the caller-owned trusted publishing repair and the immutable 1.2.0 publication recorded below; do not rerun 37597780819 or attempt duplicate 1.2.0 pushes. Recheck current-main identity and all package indexes first. If source changes for upstream alignment, dispatch a new Release for its newly verified exact source instead. Full CI and publication acceptance remain incomplete; do not mark this spec done or claim published packages.


## Trusted publishing continuation and remaining CI blocker

The user created the active Hexalith-owned policy with creator jpiquot, repository Hexalith/Hexalith.Parties, workflow release.yml, and environment production. Source cdd72b1d4a73013b04e909b5e3d82b59e61386d1 passed Commitlint and CodeQL. CI 37607041148 passed build/package consumers and 2,713/2,713 build-and-test cases with zero skips. The required Aspire job still failed 35 passed / 6 pre-existing health skips / 1 gateway timeout. Fresh runner logs report upstream Tenants CS0246 for IDomainServiceAdministratorVerifier and DomainServiceAdministratorClaim, absent from published EventStore 3.115.0. Additional upstream SDK publication remains unanswered and is outside this authorized Parties release.

Release 37607197353 authenticated through caller-owned NuGet Trusted Publishing and published all nine 1.2.0 packages. Independent downloads match exact source cdd72b1d4a73013b04e909b5e3d82b59e61386d1. The three OCI indexes were published and metadata-validated, but UI liveness failed because plain Alpine lacks the culture support required by request localization. GitHub Release was not created. Repair and fresh patch release are tracked by spec-fix-ui-container-globalization.md. Preserve the existing 1.2.0 tag/package/image identities; full CI acceptance remains open.
