---
title: Fix native Admin UI source mapping conflict
type: bugfix
created: 2026-11-10
status: done
route: oneshot
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix the existing HXW006 Debug source-mapping conflict in the native EventStore
Admin UI build invoked by the Parties test host. Respect the pinned mapper's
source selectors while retaining the existing package Release hosted profile.
Do not weaken mapping guards or warnings-as-errors, update dependency versions,
or change root gitlinks. Build IntegrationTests in source Debug, run the focused
catalog tests, and verify package Release compatibility. Record fresh validation
evidence, update relevant deferred-work records, then commit and push the
required changes to main.

</frozen-after-approval>

## Implementation Notes

- No intent gaps. Small reversible build correction in the Parties-owned test
  host; no data migrations, deployment or public API changes. Commit/push are
  explicitly authorized. The worktree began clean on main at
  87a7b600353b8967ab7b537c1e8dbd4a4b1b5cdc.
- `BuildNativeAdminUiArtifact` unconditionally overrides
  `UseHexalithProjectReferences=false;UseNuGetDeps=true` in its nested MSBuild
  call. Source mapping requires true/false. Keep shared artifact settings and
  the hosted SDK profile, whose package edges the mapper reconciles to selected
  source projects; add the explicit package selectors only outside source mode.
- Preserve native assets, artifact provenance and mapping imports. Verify actual
  nested property behavior in both modes and a deliberate source-selector
  conflict still fails with HXW006. Use the pinned 4.47.0 prepare-build output
  directly for each restore/build, followed by direct xUnit assembly filters.
- Focused tests: development catalog configuration, preparation cleanup,
  finalization, and private Admin UI composition. Package Release must build and
  retain the same tests and native UI profile. Append current evidence beside
  the related DW-202 history and generator deferral without rewriting historical
  receipts or unrelated FrontComposer mapping observations.
- Evidence packet:
  `tests/hxw006-admin-ui-source-mapping-2026-11-10/verification.json`, with exact
  commands, emitted environments, exit codes, logs, XML and tested hashes.

- Applied the conditional property list without changing any mapping task,
  warning policy, dependency version or gitlink. The observed nested-build
  regression fails only its source case before the fix and passes all three
  cases afterward in both compiled test configurations.
- Fresh mapped source Debug IntegrationTests build passes with zero warnings
  and errors. The same four focused IntegrationTests classes pass 61 cases;
  catalog gateway selectors pass 24. Package Release repeats both builds and
  85 focused cases successfully. Build-profile controls add three per mode:
  176 final checks, zero failures/skips. Baseline red runs remain in the packet.
- The native restored graph contains six Hexalith project dependencies in
  source mode and six package dependencies in Release. Archived assets and
  artifact TSV files preserve this distinction. A deliberate false source
  selector still fails with HXW006; warning-policy validation passes.
- AppHost baseline observed waiting resources and was stopped before edits;
  this is not a running-topology qualification. No full solution suite or
  package publication claim is made. Updated only current follow-up fields
  beside DW-202 and the already-resolved catalog-generator deferral.

- Independent blind review found no implementation defect. Applied an unmapped
  Debug control, sentinel SDK version forwarding, independent negative NuGet
  selector probe, complete imported-tree archives and supporting-file hashes,
  explicit command working directories, and the reproduction path correction.
  Final focused total is 178 passing cases, zero failures/skips; the earlier
  176-case runs remain historical. Both CI test configurations were rebuilt and
  the expanded four-case profile class rerun after the final test edits.
- Commitlint 21.2.4 validates the exact full commit message with zero problems or
  warnings. Fetch confirms main and origin/main are identical before commit;
  no dependency checkout or gitlink changed.

## Review Triage Log

| Finding | Verdict and disposition | Evidence |
| --- | --- | --- |
| R0: reproduction receipt path assumes packet cwd | low, patched | Example now opens the packet path from the repository root and supplies recorded cwd |
| R1: automate a full mapped smoke build in the fixture | low, rejected | The fixture explicitly tests nested-call behavior; full actual mapped builds and negative probes passed. A new tool/dependency CI lane would expand this scoped correction substantially |
| R2: structurally assert selector names are omitted | low, rejected | Current code omits them and the observed source/package matrix passes. An implementation-mirroring assertion adds no behavior proof |
| R3: unmapped Debug case absent | low, patched | Added fourth profile case and reran it in Debug and Release |
| R4: version test matches current cohort | low, patched | Fixture uses sentinel 99.1.2-profile and checks exact Restore/Build forwarding |
| R5: only one selector guard probed | low, patched | Both independent conflicting selectors still exit 1 with HXW006 |
| R6: artifact TSV omits effective inherited mapping values | low, rejected | TSV records explicit task properties as before; the complete packet separately binds mapping identity, native graph and copied assemblies. Changing artifact provenance schema and rebuilding hosts is unnecessary for this correction |
| R7: supporting evidence not indexed by receipt | low, patched | Every supporting file now has a relative path and SHA-256 in the receipt |
| R8: mapping archives omit imported pinned-tree props | medium, patched | Both archives now include and hash every generated mapping file recursively, including the imported catalog inputs |
| R9: command cwd absent from receipt | low, patched | Every recorded command now includes its actual repository working directory |
| R10: synthetic Restore/Build failure cases absent | low, rejected | The task retains MSBuild's default failure propagation and no ContinueOnError override. Additional tests would duplicate unchanged framework behavior; baseline real nested failure already propagates |

No review finding remains deferred. The one-shot workflow's sole review layer
completed; none was skipped. Source/package builds remain scoped evidence and
do not discharge unrelated owner, runtime or publication gates.
