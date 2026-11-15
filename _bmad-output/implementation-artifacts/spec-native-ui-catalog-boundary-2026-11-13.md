---
title: 'Repair the private native Admin UI catalog boundary'
type: 'bugfix'
created: '2026-11-13'
status: 'in-review'
baseline_commit: '4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/docs/ci.md'
  - '{project-root}/tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj'
  - '{project-root}/tests/Hexalith.Parties.Ci.Tests/NativeAdminUiBuildProfileTests.cs'
  - '{project-root}/scripts/validate-aspire-runtime-cohort.py'
---

<frozen-after-approval reason="existing explicit user authorization to repair CI, synchronize dependencies and publish; no additional human checkpoint">

## Intent

Repair the concrete main CI build failure while preserving the real native Admin
UI application artifact, source selectors, exact private hosted SDK cohort,
catalog provenance and all automated refusal boundaries. Finish the original
user-requested release; this is a necessary CI repair, not new feature work.

## Boundaries

Root remains on genuine signed Builds5.2.2/02ed and qualified Platform2.1.0/b76.
Do not edit either published owner checkout, add a catalog wildcard, broaden the
finite named consumers, relabel the external project as another project, invent
a snapshot, disable HXW012, remove tests or solution projects, lower counts,
skip a build or accept unknown SDKs. Latest global SDK5 remains distinct from
the exact retained SDK4.2 private hosted fixture. Every ordinary native owner
build and publication keeps its original context and targets.

Source mapping remains authoritative for mapped Root projects and source-mode
dependency selectors. Any private external-artifact isolation must preserve
equivalent meaningful verification of the actual complete owner source, exact
selected public SDK packages, compiler output, framework, native UI assets and
recorded artifact provenance. Reuse existing private runtime preparation and
receipt mechanisms where appropriate. Do not silently remove inherited checks
without an authenticated replacement boundary. If a new shared Builds capability
is required, report the exact requirement to the parent instead of modifying
the immutable producer or duplicating technical-module logic.

Preserve all other user/concurrent changes. No Git staging, commits, pushes,
remote operations, submodule initialization or release operations; the parent
owns those. No skills under references. Implement the smallest justified local
repair and verify the actual affected build rather than only XML-shaped mocks.

</frozen-after-approval>

## Code Map and Reproduction

- Final Root source is4eca9e8f. CI43469191710 failed in build-and-test job132036286886.
  Normal pinned module preparation passed; failure occurs in the solution build.
- The tool-generated imports validate each actual project against its authenticated
  catalog view. Private `BuildNativeAdminUiArtifact` explicitly builds the real
  EventStore Admin UI project with hosted SDK4.2.0. That external project's
  ordinary name has the global5.0.0 catalog view; it is deliberately outside
  the finite Parties/Platform views. The inherited verifier emits HXW012 for
  Admin.Abstractions, Client, ServiceDefaults and SignalR4.2 versus5.0.
- `tests/Hexalith.Parties.EventStoreAdminUI.TestHost/...csproj` invokes the native
  owner through an MSBuild task, propagates the source/package selector and
  produces `native-admin-ui-artifact.tsv`, actual native DLL/deps, and exact assets.
  The private Program consumes the original native application's services and UI.
  Keep these real bodies and all asset/source provenance intact.
- `NativeAdminUiBuildProfileTests` proves actual MSBuild child property behavior
  across mapped source, mapped package and unmapped Debug/Release. It currently
  uses a tiny producer and cannot reproduce the actual authenticated snapshot.
- `scripts/prepare-aspire-runtime-hosts.sh` and
  `scripts/validate-aspire-runtime-cohort.py` already build/verify the independent
  private runtime cohort, its exact SDK archives/binaries, frameworks, owner
  input bytes and native UI provenance. The concurrent runtime CI lane uses it.
- The real pinned CI preparation helper was run successfully at this exact source.
  `/tmp/parties-exact-producer-preflight/github-env.txt` contains its validated
  generated environment keys. Read lines as key=value data, never evaluate them
  in a shell. The referenced workspace and genuine signed tool exist locally.
  Root `.config/dotnet-tools.json` pins5.2.2; `dotnet tool run hexalith-module --version`
  returns5.2.2+02ed624aa19781cac75d2c9112940e9bd830441a.
- Actual direct EventStore source is69b244d9 and ordinary Admin UI targets net11.0.
  Its explicit `HexalithUsePublishedSdkForHostedTests` profile switches SDK edges
  from source projects to selected public packages. Ordinary source publication
  remains distinct from this fixture. No nested owner initialization is needed.
- Ordinary unprepared whole-solution builds pass; reproduction must include the
  genuine prepared environment to expose this context boundary.

## Tasks & Acceptance

- [ ] Reproduce the actual private native UI build under the genuine prepared
  package environment, retaining the exact command and HXW012 failure.
- [ ] Repair the demonstrated boundary locally with the smallest coherent change,
  keeping Root mapped catalog guards and private native artifact verification.
- [ ] Verify mapped source behavior, mapped package behavior and ordinary private
  builds without replacing the native artifact or source selectors.
- [ ] Add a meaningful regression that fails before the repair and execute the
  actual native owner/host build with real signed5.2.2 mapping after the repair.
- [ ] Prove wrong/missing package or artifact inputs still refuse at their real
  boundary; keep actual compiler/receipt/asset checks and no warnings/errors.

If a correct repair requires a shared producer capability, provide a concrete
diagnosis and required API/contract to the parent rather than weakening guards.

## Verification

Use the real pinned prepared environment for the affected private host build.
Run only tests for edited files. Parent runs broader checks, all three reviews,
Git/remote work, exact-source hosted CI and final release verification.

## Implementation Notes

No implementation yet. This spec records the concrete failure and the existing
authorized maintenance boundary. All prior genuine producer and Platform source
qualification remains valid and must not be repeated or weakened unnecessarily.


## Review Triage Log

All three independent layers completed before triage: blind hunter7 findings,
edge-case hunter1, verification reviewer1. Review input is the full45911-byte
incremental diff at /tmp/parties-native-ui-boundary-review-20261113.diff.

| ID | Verdict | Route | Evidence and resolution |
| --- | --- | --- | --- |
| NB1 | medium | patch (import inventory) | Actual preprocessed owner imports Builds Directory.Packages.props and Logging.Build.targets, while the completed native-owner receipt omits both; MSBuildAllProjects is incomplete. Capture and bind the actual evaluated import closure, and exercise readback after an imported input changes. |
| NB2 | false | reject | Actual evaluated owner items contain39 Compile,1 EmbeddedResource and0 AdditionalFiles, all within the captured UI directory. No linked Compile/Razor/resource input outside it is present in this selected source. The four outside Analyzer items are the pinned SDK11 toolchain, not owner source; imported build inputs are separately repaired under NB1. A future linked-source capability is not a demonstrated current missing owner input. |
| NB3 | medium | patch (source inventory) | Actual receipt records generated .artifacts/ui-test-obj/project.assets.json and .csproj.lscache. An ordinary restore/editor update can invalidate an unchanged artifact. Exclude the demonstrated generated build/editor output paths while retaining genuine native sources, settings, assets, additions/removals and symlink refusals. |
| NB4 | medium | patch (saved hashes) | Provenance rehashes inputs after checking the precompile snapshot. A source edit in that window can be accepted as the new digest. Persist the saved original hashes so normal subsequent record/readback rejects any later edit. |
| NB5 | medium | patch (outer context) | The completed inner manifest is checked against its own header configuration/SDK/framework, without comparing them to the known outer host context. In particular a genuine Debug inner boundary can accompany a Release wrapper. Thread the existing expected context through readback and require equality; retain actual native project identity and the current source selection. |
| NB6 | medium | patch (completed wrapper regression) | The new executable regression stops after the native build target and the Python negative test stops at incomplete evidence. Cover completed matching wrapper provenance and changed native DLL bytes present before recording, plus mismatched inner context. Retain actual original-owner compilation and all existing asset/staging checks. |
| NB7 | low | patch (test budget) | The180-second live-archive test deadline is shorter than five supported93-second download retry budgets plus restore/compilation. Make the bounded test budget cover the supported transport budget without weakening archive verification. |
| NE1 | medium | patch (import inventory) | Independently confirmed actual imported Builds catalog and logging targets missing from native-owner files; same concrete missing imported input as NB1. |
| NV1 | medium | patch (completed wrapper regression) | Preverified mutation removes only completed owner/wrapper DLL equality: all41 tests remain green while the mutant records a changed DLL. Add a causally meaningful completed-manifest positive and pre-record changed-DLL refusal; this is the same test gap as NB6. |

The frozen maintenance intent is unchanged. These are local private-artifact
proof and regression repairs; genuine Builds/Platform source qualification,
Root mapping guards and all historical cohorts remain intact.
