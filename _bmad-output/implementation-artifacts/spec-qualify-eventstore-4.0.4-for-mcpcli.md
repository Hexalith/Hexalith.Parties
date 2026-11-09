---
title: 'Qualify the Parties catalog for the canonical McpCli runtime cohort'
type: 'bugfix'
created: '2026-11-03'
status: 'done'
route: 'dispatch'
baseline_commit: '4c57e79d6def9299ae279aa95cd852319fbc4832'
context:
  - '/home/administrator/projects/hexalith/parties/AGENTS.md'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="User authorized all necessary dependency and CI repairs for the Tenants release and downstream McpCli">

## Intent

Allow the existing Parties catalog integration to compile and expose its real declarations/adapters against the verified public EventStore4.0.4 cohort. McpCli's unchanged Tenants7 vectors require canonical platform scopes, which its frozen3.128.4 gateway rejects. Its repaired native runtime must include Parties alongside current Tenants, but Parties currently omits catalog code for4.0.4. Qualify this exact owner cohort without pretending source mode or compile-symbol injection establishes compatibility.

## Boundaries & Constraints

Work only from the existing clean owning checkout /home/administrator/projects/hexalith/parties. Do not edit nested submodules or initialize/update any dependencies. Read relevant tracked configuration, project/test and architecture guidance before edits; never load reference-directory skills. Preserve all concurrent work. No staging, commits, pushes, dependency-default updates, package publication, AppHost/private business runtime launches or synthetic authority. No changes to business semantics, operations, identity rules, authorization, event topics, validators or frozen McpCli vectors. Preserve all existing finite cohorts, unknown-version refusals, source-mode semantics, provenance checks and the independently reviewed legacy testing harness. Root coordinates commits, exact-source CI and downstream adoption.

</frozen-after-approval>

## Code Map

- Baseline main/origin is4c57e79d6def9299ae279aa95cd852319fbc4832, initially clean. Existing root .gitmodules declares references/Hexalith.AI.Tools and its mandatory baseline is already available. This is a standalone owner checkout, not a new/nested submodule initialization.
- Directory.Build.targets first two package conditions qualify3.129/3.130/3.132.2. Add only exact stable4.0.4 to both catalog API and hosted catalog capability conditions. Package mode must still leave HEXALITH_DEVELOPMENT_CATALOG_SOURCE unset. Unknown4.0.5, prerelease/build-metadata/four-part aliases and malformed inputs remain refused unless already intentionally supported; do not widen to all4.x.
- Existing tests/Hexalith.Parties.Ci.Tests/AspireRuntimePreparationTests.cs::CatalogApiAvailabilityDoesNotSelectNativeHosts imports the real targets into a temporary MSBuild project and verifies all three symbols. Extend it with4.0.4 true/true and representative false neighboring/alias cases, retaining all prior cases and source controls.
- src/Hexalith.Parties.Contracts has the compiled RouteCatalogDeclarationSource marker and operation/schema metadata behind the API constant. src/Hexalith.Parties.EventStoreExtensions has actual PartiesCatalogAdapters, and src/Hexalith.Parties registers native development catalog handling behind the hosted capability. Build and inspect these actual artifacts against4.0.4 so condition tests are not mistaken for API qualification. Verify declarations/operations/adapters are present, nonempty and accepted by the SDK's real catalog codec/builder or existing owner tests. Existing source/type inspection or an isolated helper process can be reused; never invent runtime acceptance from scalar flags.
- The adapter project is deliberately prepared separately by src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogBootstrap.cs. Keep this graph: the adapter is not in the native host's dependency closure. Do not add fake host dependencies or relax SDK split-owner loader checks to accommodate it. Downstream McpCli will reuse the existing Platform catalog-builder preparation path for separate real artifacts.
- Public4.0.4 archives are independently verified at /tmp/tenants-release-20261103-07JciR/eventstore-4.0.4-root-agent/verification.json, all14 from source326bfef884543cdbcaf96de7f4c4ad378dbad6d7. Use explicit HexalithEventStoreVersion=4.0.4, UseHexalithProjectReferences=false, UseNuGetDeps=true, EventStoreFromSource=false for this qualification; no producer substitution. McpCli's runtime also deliberately selects public Tenants5.11.2 and Memories3.0.11 for Parties' existing event-based dependency path. Check that exact combination where possible; it does not enroll those modules as the current Tenants7 CLI artifact.
- Keep Directory.Packages.props defaults and submodule gitlinks unchanged. Qualification adds an explicit supported cohort; the downstream runtime selects it. Preserve the existing4.7.4 FrontComposer/default3.132.2/6.0.0/3.0.13 standalone choices and existing historical evidence. Do not turn this repair into an unrelated package sweep.
- Run only the changed focused gate tests and real affected individual project builds. Start with existing repository-required Debug/local settings where useful; the public-cohort qualification and repository fallback ladder permit serialized Release/package builds with warnings as errors. Report source/package proof separately. No solution-level test command.

## Tasks & Acceptance

- [x] Given explicit package4.0.4, real target evaluation enables catalog API/host capability without source mode. Given every tested neighbor/alias/invalid input, it remains refused; all original cohort/source cases retain their results.
- [x] Given the actual verified public4.0.4 packages and deliberately retained runtime dependencies, Contracts, EventStoreExtensions and native host build without warnings/errors and retain actual valid catalog declarations/operations/adapters. No generated symbol override or source substitution is used.
- [x] Existing catalog preparation/provenance and relevant owner tests remain intact. No host dependency edge, trust boundary, business behavior, operation or event topic is changed. Record any concrete compile/API incompatibility and the smallest compatible owner fix; do not suppress it.
- [x] Save exact commands, exit codes, selected package/source identities, artifact hashes, test counts and limitations to /tmp/tenants-release-20261103-07JciR/parties404-qualification-handoff.json. Full native topology acceptance remains downstream actual CI, not a local claim.

## File List

- .github/workflows/ci.yml
- tests/Hexalith.Parties.Catalog.Tests/Hexalith.Parties.Catalog.Tests.csproj
- Directory.Build.targets
- tests/Hexalith.Parties.Ci.Tests/AspireRuntimePreparationTests.cs

## Verification

The original implementation and local qualification are recorded below. Current resumed evidence is in `tests/eventstore-4.0.4-resumed-2026-11-09/receipt.json` relative to this spec. No private business runtime is authorized for this local owner qualification.


## Local Qualification

All41 focused controls passed:22 real-target gates,15 retained preparation/provenance controls and4 isolated actual artifact controls. Contracts, EventStoreExtensions and the native host built Release with warnings as errors and zero warnings/errors using explicit public4.0.4/Tenants5.11.2/Memories3.0.11. Actual41 routes/41 operations/28 adapters validate; adapter remains outside host dependency closure. All14 verified archive hashes were rechecked and consumed SDK PE bytes match the public archives. Commands, hashes, results and limitations: /tmp/tenants-release-20261103-07JciR/parties404-qualification-handoff.json. No runtime acceptance or publication is claimed.


## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind1: new package cohort lacks persistent build/catalog gate | medium; patch | The default CI remains3.132.2, so existing local4.0.4 artifact proof would not catch later4-specific compilation/API regression. Add a focused package-mode CI job selecting exact4.0.4 and the retained runtime dependency overrides, building the three real affected graphs and running existing actual catalog tests plus the finite gate cases. Complete native topology remains downstream McpCli CI; do not duplicate or claim it from an offline owner job. |
| Blind2: capability properties unasserted | medium; patch | Runtime preparation reads HexalithEventStoreCatalogApiSupported/HostsSupported independently of compiler constants. Removing either property assignment currently leaves the gate theory green. Query/assert both real properties in every current case, preserving their absent/unqualified behavior and source controls. |

Edge-case and verification-gap layers returned no findings. Both fixes are internal qualification checks with no public API/default change and no private runtime launch.


## Review Fix Results

Both review findings are fixed: a persistent offline exact4.0.4 job compiles actual owners and runs their existing catalog tests through a focused linked test runner, preserving the legacy harness; all22 gate cases assert properties and symbols. Focused23 gate/workflow plus3 catalog tests passed; changed test graphs built without warnings/errors. YAML/Bash parse and semantic equality of every existing job passed. Direct CI runners fail on skips or zero tests. Exact follow-up: /tmp/tenants-release-20261103-07JciR/parties404-review-fixes.json. Full downstream native topology acceptance remains pending.


## Resumed Review Triage — 2026-11-09
The workflow-required historical-baseline diff also includes later work. Every finding below was triaged separately; unrelated implementation and concurrent edits are preserved.

| Finding | Verdict and route | Evidence |
| --- | --- | --- |
| BH1: Run the lifecycle controller safety tests automatically. | medium; defer | No checked workflow invokes the full tests/ci-lifecycle discovery; the verification-gap layer independently confirms the process-birth refusal test is omitted. This later lifecycle work predates this resumed qualification and is independent of the 4.0.4 offline job. |
| BH2: Determine whether the supplemental public/private result readers need the full released contract validator. | maybe-false (medium unverified); defer | Both readers accept their intentionally minimal synthetic fixtures. The owning CLI produces the real result first; no malformed production result was demonstrated. Trace that released producer/validator contract and construct a reachable invalid artifact to settle the claim. |
| BH3: Bind native TRX timestamps and execution identity to the current integration owner. | medium; defer | The supplemental public validator accepts a resealed report with all required names but Times in 1900. Its name/counter checks do not bind the native report to the current functional observation interval. This is a later integration-reader issue. |
| BH4: Require selector compatibility observations in the private current-owner reader. | medium; defer | The public reader now refuses false after a concurrent correction; the private reader still accepts selectorCompatibilityObserved=false in the parent probe. Preserve the other session correction and defer only the remaining private-reader issue. |
| BH5: Reject boolean and non-integer sequence/count representations in integration evidence. | medium; defer | Parent probes confirm both public/private readers accept metadataSequence=true and eventCount=1.0. Equality to len(events) does not enforce the advertised integer representation. This affects later evidence readers, not the 4.0.4 codec/build proof. |
| BH6: Bound public runtime and functional observation times to their recorded execution interval. | medium; defer | The public reader accepts measuredAtUtc in 2999 and observedAtUtc in 1900; its only measurement ordering check compares image start with measurement. The offline cohort job does not use this reader. |
| BH7: Enforce loaded-process and copied-output assembly scopes in the private evidence reader. | medium; defer | Parent probes relabel both inventories copied-static-image without refusal. The public reader enforces their distinct scopes, while the private reader currently checks identities/hashes alone. |
| BH8: Determine whether lifecycle metadata can reach a symlinked parent after directory admission. | maybe-false (medium unverified); defer | The standalone read() follows a symlinked parent in a parent probe. private_directory() rejects linked ancestor paths during admission, so hostile hosted-path reachability is unproven. Trace every caller and demonstrate a post-admission directory replacement to settle it. |
| BH9: Hardcoded Aspire version currently differs from the selected catalog. | false; reject | The preparation script and selected HexalithAspireAppHostSdkVersion both equal 13.6.4. No catalog/version divergence occurs in this checkout; a hypothetical later update is not an existing qualification defect. |
| BH10: Determine the required Dapr executable archive integrity policy for diagnostic workflows. | maybe-false (medium unverified); defer | The workflows download exact release URLs over HTTPS without a separate pinned digest. No altered executable or failed release authenticity condition was demonstrated. Establish the owner supply-chain requirement and trusted digest source before treating this hardening proposal as a confirmed defect. |
| EC1: The requested temporary qualification handoff is absent. | false; reject | It was absent at review start. Fresh observed-build/package/validator results now recreate the exact requested handoff and a durable repository copy; the missing historical receipts are explicitly retained as a limitation rather than reconstructed. |
| VG1: Automatically execute lifecycle controller process-birth refusal controls. | medium; defer | Pre-verified broken-verification gap: full 74-test local discovery passes but no checked workflow runs it. Same root cause as BH1, with one shared deferred entry; no live service or hosted credit is inferred. |
| VG2: The lifecycle project is absent from the PowerShell test inventory. | false; reject | The review-start failure was real, but concurrent edits now include Hexalith.Parties.Platform.LifecycleTests in $platformProjects. The current inventory entry directly resolves the cited missing-project claim; this run did not change that file. |
| VG3: Reconcile ordinary public evidence-reader source expectations with current committed gitlinks. | high; defer | Parent probe confirms validate() refuses current Platform e39a9a2587ec150d550510763ab69db305d13fdb and Builds 27145e52ffab5623198ba5efdab5cdb71aa42d28 before artifact inspection, since it requires historical published identities. This blocks the broader public gate and belongs to separate dependency/publication reconciliation; the 4.0.4 offline job is independent. |

## Resumed Qualification — 2026-11-09

All five real affected project builds pass with zero warnings/errors against explicit public 4.0.4/5.11.2/3.0.11. Final focused verification passes 53 tests with zero skips; the initial concurrent tool-policy mismatch and successful rebuilt recheck are both retained. The SDK validates 41 routes, 41 operation schemas and 28 descriptors/adapters. Eight independently downloaded selected cohort archives and four consumed SDK PE bodies match; the adapter remains outside the native host closure.

Fresh commands, results, hashes, review verdicts and limitations are retained in [the durable receipt](tests/eventstore-4.0.4-resumed-2026-11-09/receipt.json); the exact requested `/tmp/tenants-release-20261103-07JciR/parties404-qualification-handoff.json` is recreated from this run. Original temporary receipts and the complete historical 14-archive producer qualification were not reconstructed. Broader CI source-identity and lifecycle/evidence-reader issues are deferred. No hosted CI, full topology acceptance, dependency-default change, publication or commit is claimed.
