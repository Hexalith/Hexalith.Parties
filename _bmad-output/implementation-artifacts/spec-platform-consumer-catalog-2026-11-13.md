---
title: 'Resolve the authenticated Platform consumer catalog view'
status: 'in-review'
baseline_commit: '341abd8d0c6cc06d57e70fc58f3f16c7e0c49b0c'
review_loop_iteration: 0
context:
  - '/home/administrator/projects/hexalith/parties/AGENTS.md'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformPackageCatalogQualification.cs'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformServerBindings.cs'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/tests/Hexalith.Platform.Composition.Tests/PlatformPartiesPackageFixture.cs'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/tests/Hexalith.Platform.Composition.Tests/PlatformCurrentPackageArtifactCohortTests.cs'
  - '/home/administrator/projects/hexalith/builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationOptions.cs'
  - '/home/administrator/projects/hexalith/builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationClosure.cs'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformDeclarationSet.cs'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformMcpCliCatalogVersion.cs'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/Directory.Build.props'
---

<frozen-after-approval reason="user authorized CI repairs and necessary Platform dependency work; human checkpoints waived">

## Intent

Fix the actual current tuple failure at Platform49edf00b, run43431743956, group30 CurrentPrivateDependencyVersionsRemainExact. Both349/412 historical lanes and all6catalog lanes passed. Current group executed17 cases with one failure: PlatformPartiesPackageFixture.CreateAsync line144 could not select an independently authenticated complete current private tuple. Preserve all closed tuple, signed artifact, source, image, native and unknown/future-version refusal boundaries. Never admit SDK5 by relabeling it, replace authenticated data with constants, invent approvals, or lower required counts.

Only local source/tests/docs edits by implementation agent. No skills, staging, commits, submodule initialization/update, remote operations, publication or other checkout mutation. Parent owns necessary direct owner checkouts, commits, fresh immutable companions and all hosted qualification. Other Story2.12 work in sibling platform checkout remains independent and uncommitted; preserve its main review records already present at49. No new packages or public APIs.

</frozen-after-approval>

## Code Map

Owning repo ../../references/Hexalith.Platform from this spec. SDK11.0.100. Signed Builds5.2.1 source0e7424fdce6373920fb2019d1ad2a74d8fe5295c and closed pair already independently verified. Current catalog raw SHA9e5d1fafaf46d051b68da2090399733d9470873082d20f6444493e3594b0cab3 has global SDK5 but finite named Platform SDK4.2/OpenAPI2 views. Do not change the shared catalog or signed producer.

- PlatformServerBindings.Version delegates to Builds CompositionDeclarationOptions.CatalogVersion, which reads snapshot.Versions (global) or first raw property. SelectCurrentOwnerCohort uses that value; ForCurrentOwnerVersions correctly admits only exact4.2/707/26-or27/Mcp201 current tuples.
- Existing signed Builds API CatalogPackageVersion(packageId, source, projectName) selects authenticated snapshot.ProjectVersions; a missing consumer view refuses. Source is ../Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationOptions.cs relative to Platform. CatalogVersionMaterializer records both default and exact conditional-project views and authenticates pinned files/mapping/hash.
- Current Platform.Composition is one of the33 finite named consumer projects. Align current binding/selection with its actual authenticated view, consistently across module bindings and private owner proof. Determine exact reachable version discrepancy with a focused reproduction before editing. Preserve legacy/frozen behavior: Builds4.47 has3-argument package API, but Builds4.44.3 does not. Existing HEXALITH_PLATFORM_EXTENSION_SDK and historical boundaries must compile/run unchanged. Missing, malformed, modified and unqualified snapshots must fail closed, never fall back from a missing required current view to another authority.
- Relevant helpers include PlatformMcpCliCatalogVersion (already uses package API and physical-pin authentication), PlatformCurrentOwnerProof, PublishedCatalogArtifactCohort.ForCurrentOwnerVersions and PlatformCurrentPackageArtifactCohortTests. Do not expand the finite admitted version set or image authority.
- Failed archive /tmp/parties-platform49-current-artifact.zip (artifact14043828003) retains actual commands, signed tool, source context and test30 output; current group29 passed38 old SDK4.2 artifact controls, then group30 passed16/17. Do not claim total current qualification yet.
- Parent deinitialized its17 task-created direct Platform owners after checking all clean/exact; Git object stores remain. Parent can restore only needed declared owners for real fixture validation. Sibling outer root owners under /home/administrator/projects/hexalith/parties/references remain available. For local build avoid inherited Parties .editorconfig by using an isolated exact owner snapshot outside the umbrella (existing proven pattern); no suppression or diagnostics relaxation.

## Tasks & Acceptance

- [ ] Reproduce/record the exact global-versus-consumer values and identify the actual rejected tuple member.
- [ ] Correct current binding/selection using actual authenticated consumer context, preserving all historical and future-version refusals and APIs.
- [ ] Run the actual17-case PlatformCurrentPackageArtifactCohortTests class using genuine signed5.2.1 assemblies and recorded owner/catalog inputs; preserve group counts. Extend focused assertions if needed without changing original349/412 contracts. Keep image/native refusal cases passing. Run other directly affected existing selectors and narrow helper checks.
- [ ] Report exact commands/results and any unresolved risk; leave parent source/companions/remote work pending.

## Verification

Use genuine prepared signed tool /dev/shm/parties-platform-auth-recorded-artifacts/published/tools/net11.0/any or /tmp/parties-platform521-implementation-Y78BIc/published-builds/published/tools/net11.0/any. Exact source snapshot/evidence patterns are recorded in /tmp/parties-platform521-implementation-Y78BIc/validation-summary.json. Parent will restore needed direct owners; do not initialize them yourself. Run the real changed artifact class rather than only constant matrix tests. Keep validation positive/negative meaning, zero skips and current version identities.

## Review Triage Log

All three independent layers completed before triage: blind hunter5 findings, edge-case hunter0, verification-gap reviewer1. The initial implementation diff is retained at `/tmp/parties-platform-consumer-review.diff`.

| ID | Verdict | Route | Evidence and resolution |
| --- | --- | --- | --- |
| B1 | high | patch (parent prerequisite) | The checked-in signed5.2.1 API lacks the successor members, so the prepared NET11 code cannot ship alone. This is the explicitly pending producer prerequisite: parent must atomically adopt the independently verified genuine successor source/tool/range/binding/pair before any production source commit or qualification. Debug compile evidence grants no substitute credit. |
| B2 | false | reject | Identityless lower-level component/default context preserves pre-existing behavior and supplies no current owner authority. Actual current proof creation delegates to `PlatformPartiesRunProof.Validate`, which immediately refuses a missing `PlatformIdentity` at line66 and independently checks the signed/persisted plan mapping hash. No authenticated current identity can drop this field and acquire physical/default authority through this helper. |
| B3 | medium | patch | The MCP API branch remains gated solely on the EventStore extension symbol; NET11 already requires the successor shared Builds API independently of that SDK flag. An unsupported/nonextension EventStore compile could therefore query a different default view than the shared module/declaration paths. Include NET11 in the API branch while retaining the original frozen net10 branch; verify named MCP behavior with the extension flag absent. |
| B4 | medium | patch (group range-control) | The invalid-range loop changes `PlatformIdentity` and therefore `SourceMapping.ContentHash` without matching the snapshot; existing hash refusal can mask a removed range rule. Assert `DeclarationOptions(changed)` directly before downstream snapshot checks so each invalid range exercises the new guard. |
| B5 | medium | patch | Whole-view deletion does not establish package-level absence refusal while a global counterpart exists. In the genuine current fixture remove required EventStore and MCP entries individually, retain global values and rehash correctly; assert required-module refusal, optional MCP absence and closed cohort refusal as appropriate. Keep the original17 cases and body/image/native controls. |
| V1 | medium | patch (group range-control) | Pre-verified verification gap independently demonstrates that removing only the prerelease-minimum guard still leaves all original downstream exception assertions passing through mapping mismatch. The same direct options-construction assertions required by B4 close this precise causal gap. |

No feature acceptance or publisher qualification is claimed by these review results. Required actual signed-successor/current17 and unchanged hosted historical/current/catalog gates remain pending.

## Implementation Notes


### Producer boundary decision, 2026-11-13

The exact signed5.2.1 reproduction confirms global EventStore5.0.0 versus the authenticated `Hexalith.Platform.Composition` view4.2.0. Its shared `CompositionDeclarationClosure.Load` also uses the global module lookup for release provenance. The frozen initial local-only approach cannot fulfill the original user task. The user authorization for necessary prerequisite fixes/releases governs this extension: a genuine Builds successor must supply consumer context consistently through the shared module lookup, declaration closure, coordinator/startup and descriptor revalidation. DefaultSDK5 and finite namedSDK4.2 views, historical APIs and all refusal boundaries remain unchanged.

No local duplicated declaration closure or rewritten snapshot may ship. Incomplete task-owned adapter files were archived at `/tmp/parties-platform-consumer-rejected-adapter`; exact49 source bytes were restored before further work. The producer owner task has been asked to implement, qualify and normally release the missing capability. The original17-case current artifact class must then pass against the genuine successor, with named-view/missing-view/tamper controls; the signed5.2.1 probe is diagnostic evidence only.

Parent may prepare a real standalone current Platform checkout outside the umbrella with only its17 direct recorded owners. Implementation may overlay its own changed source/tests in that task-owned validation checkout for actual builds/tests; this is local verification, not immutable-source qualification or authority to modify sibling user worktrees. Parent retains all Git, immutable companion, source comparison, hosted qualification and publication operations.

Reproduction: `/tmp/parties-platform-consumer-94w6fvfp/reproduction.log`. The first actual workspace attempt refused outer umbrella nested owners as designed; a separate component probe established exact signed materializer/API values and is not represented as owner qualification.

Real standalone owner confirmation: checkout79b3ec6a with all17 exact direct recorded owners builds using the signed5.2.1 tool with zero warnings/errors. Actual `PlatformCurrentPackageArtifactCohortTests` executes17, passes16 and reproduces the identical complete-tuple selection failure; zero skips/not-run. Commands and XML are retained in the current79 baseline evidence. This is diagnostic failure evidence, not qualification.


### Current integration preparation

Platform main advanced to79b3ec6a with independently pushed Story2.12 fixes; parent fast-forwarded and preserved it. Another user-authorized Story2.12 publisher is preparing additional corrections before the new qualification freeze. Parent will integrate its published heads before final source A; do not edit Story2.12 files.

The producer is implementing a typed `ConsumerProjectName` property on shared `CompositionDeclarationOptions` with consistent module, closure and coordinator propagation. Implementation must consume the actual finalized producer API once confirmed. Retain real legacy net10.0 compilation paths: the frozen4.44.3/4.45/4.47 tools cannot reference the new member. Determine a precise compile boundary from actual supported tuples; current Platform will declare its genuine successor minimum before final qualification. Avoid reflection and a duplicate closure. Keep current named-view absence/tampering refusal explicit; never choose a global value after a required named-view failure.

Real local validation checkout: `/dev/shm/parties-platform-consumer-current49`, currently at79b3ec6a with17 exact direct owners. Its name records creation baseline only; `/tmp/parties-platform-consumer-current79-baseline-results.json` records the actual executed source. Parent may update this task-owned clone to final source bytes and signed producer. The original owning checkout remains `/home/administrator/projects/hexalith/parties/references/Hexalith.Platform`.

Published Story2.12 correction now integrated at Platform341abd8d0c6cc06d57e70fc58f3f16c7e0c49b0c with its genuine Foldersf1520098d812964622a3a01bf4ec9f3beca46828 gitlink. Both owning source checkouts are clean; standalone validation clone updated to those exact commits. No further owner publication is planned during the upcoming qualification. These feature source changes supply no lifecycle acceptance.


### Active implementation direction

The review baseline is now341abd8d, preserving all independently published Story2.12 work. Historical49 failure and79 standalone reproduction remain diagnostic history. The producer's local API currently exposes `CompositionDeclarationOptions.ConsumerProjectName` and `PlatformCompositionProjectName`; consult its actual source without editing it. Parent owns its producer publication and will confirm any API adjustment before final consumer verification. Implement the thin typed Platform consumer integration consistently in shared declaration options, module binding and MCP package lookup. Use the genuine shared closure, never copy it. Preserve actual net10.0 frozen tools that cannot reference the new member; current Platform's eventual minimum is the genuine successor carrying that API. A `NET11_0_OR_GREATER` compile boundary is a candidate only if all real historical/current build configurations justify it; document/verify that boundary. Keep unknown/future SDK and image/native refusal unchanged.

Parent owns all signed binding/tool-range/pair measurements and source pins; do not guess successor source, version, archive identities, pair hashes or release IDs. Implementation may prepare source/tests now, but final positive verification must wait for the producer's real signed successor. No qualification may be claimed from unreleased source binaries. Do not edit the Builds owner or unrelated Story2.12 code.

Extend meaningful focused controls for named view absence, source/mapping/content/pin tampering and exact defaultSDK5 versus namedSDK4.2 behavior using authenticated data; do not replace actual current17 case proof with synthetic constants. Coordinate required local build/tool inputs through the parent. Report the required publication-dependent command as pending until it actually runs successfully. All Git/remote operations remain parent-owned.

Review patch verification: the three requested fixes were applied in the owning source and compiled with0 warnings/errors. The existing MCP recorded-pin case executes1/1 successfully in a NET11/SDK4.1 component build with the extension symbol absent and genuine named/default divergence. Direct invalid-range factory assertions and individual named EventStore/MCP pin removals remain within the original17 current cases. Exact task-owned unsigned Debug commands, results and file hashes are retained in `/tmp/parties-platform-consumer-implementation-6urxu5uq/review-fixes-summary.json`; this does not fulfill B1 or replace signed-successor/current17 verification.


### Final genuine verification sequence

The real current-owner proof requires clean Git owners, including Platform itself. Therefore final signed-successor verification must use a clean real committed source, not a dirty overlay. After producer publication: independently verify both signatures/source/catalog/original payloads; atomically set the exact producer source/tool/range/binding and measured complete validation pair; generate the current binding from the staged owner tree; validate/commit source A; generate/publish three new immutable companions from A; update only the two descriptors and validate/commit D. Prepare a new genuine standalone D checkout plus its17 exact direct owners. Run the actual current17 and directly affected controls there with the genuine signed successor before pushing main D. Validate both historical source comparisons and real historical reader preparation, then push D and require all unchanged automated qualification jobs and normal source publication. If a source fix is needed, make a new source commit and fresh companions rather than reusing or overwriting published qualification refs.

Producer source9bbe passed all seven normal CI jobs. Subsequent documentation-only main7db exposed a native process-discovery test race; owner corrected fixture isolation at02ed after72 focused controls and is running exact-source CI43451348835. The producer API remains finalized; no release/source/package identity is invented before publication.


### Final signed implementation verification

The parent independently verified both public signed Builds5.2.2 archives at02ed,
all354 original publisher members, official hashes and measured complete pair.
Sourcea777e629 atomically carries the typed integration and genuine5.2.2 binding;
final descriptor sourceb76e0c7c is pushed with three new immutable companions.
The actual clean final source plus17exact direct owners builds without warnings
and passes24affected controls, including all17current artifact cases, with zero
failures/skips/not-run. Actual349/412source comparisons and the real retained
17-owner reader verify. This closes B1; B3/B4/B5/V1 pass through genuine signed
current execution, with the separate no-extension component control retained.
Hosted tuple43462834314, catalog43462834460 and normal source publication remain
pending. These source/transport checks grant no Story or candidate acceptance.
