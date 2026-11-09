---
title: 'DW-27 reconcile prerequisite observations'
type: 'bugfix'
created: '2026-11-09'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 5c54b8c972e875e578e78834a306a0687f93d50b
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve the four PlatformApiPrerequisitesTests failures blocking DW-27 by
reconciling current source observations with the already committed dependency
selections and correcting the live EventStore sprint search. Investigate the
continued-review receipt before changing assertions. Preserve meaningful graph,
identity and immutable-consumption checks, pinned packages/tooling, historical
receipts, approvals and all existing paging behavior. Preserve the existing DW-27
work in this working tree. Do not commit, push, publish or deploy; never fabricate
owner signoff. Run the prerequisite class first, then the full affected service
suite in mapped Debug and selected-package modes. Keep pinned prepare-build as
source-mapping authority and disclose its diagnostics. Resume DW-27 review only
after required checks pass, and update its status from fresh evidence. If an
owner decision becomes necessary, identify the exact missing approval.

</frozen-after-approval>

## Implementation Notes

Planning facts: no unresolved intent gaps or irreversible actions; the patch is
limited to two coupled observation files, this spec, fresh evidence and the
existing DW-27 status/review record. No runtime API or dependency changes. Use the
oneshot route for this small correction under the user's explicit authorization.
The initial tree is clean at the baseline above; earlier DW-27 work is committed
and retained. The receipt at
`tests/dw-27-invalid-page-failure-2026-11-09/continued-review/validation.json`
records 114 prerequisite passes and four failures, plus HXW010/HXW011; its full
suite counts are historical, not current passes.

Implementation map and ordered actions:

1. `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`:
   update current-only SHA/describe constants and dictionaries. Builds fails
   first and masks EventStore/FrontComposer drift: reconcile all five changed
   selections, including McpCli/Platform. Bind the new current selection to a
   separately attributed matrix row. Preserve the November 6/7/8 historical rows
   with their exact old identities; remove their accidental dependence on changed
   current dictionary values. Keep independent effective-package evaluation and
   all strict dirty/mismatch/nested-submodule refusals. Update only the live
   `8-7-server-persistence-and-snapshot-integration` expectation to `in-progress`.
2. `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`:
   add a November 9 current observation of the November 8 synchronized sources.
   Retain the former current observation as dated history, preserving its body.
   Append an agent-attributed selection record citing existing source signoff;
   do not restamp a human I20 decision. Update the current structural G5 search,
   and append the new sprint observation without rewriting dated backlog prose.
3. `_bmad-output/implementation-artifacts/tests/dw-27-prerequisite-observations-2026-11-09/`:
   retain exact commands, mapping output/environment, original logs/XML, failures,
   skips, hashes and preservation checks in a fresh evidence packet. Use fresh
   result paths and a runner whose failing required checks return nonzero.
4. `_bmad-output/implementation-artifacts/spec-dw-27-invalid-page-failure.md`:
   after prerequisite/full checks and independent review, append current evidence
   and update status only if justified. DW-27 is already closed in the deferred
   ledger; do not duplicate or rewrite that historical closure.

Selected changed sources, directly observed through Git:

- Builds `27145e52ffab5623198ba5efdab5cdb71aa42d28`, `v4.45.0`.
- EventStore `b437879df2339692d177774d1f076936da6ebd62`, `v4.1.0-55-gb437879d`.
- FrontComposer `e972d0f06a8eec3446db0b2270fdb14eef4c1186`, `v4.7.6-25-ge972d0f0`.
- McpCli `1c1f06b46bbe9e115022be8af75b10085c8ac360`, `v2.0.1-31-g1c1f06b4`.
- Platform `e39a9a2587ec150d550510763ab69db305d13fdb`, `v1.7.1-17-ge39a9a25`.

`.gitlink-signoff.tsv` records all five under explicit delegated synchronization
in commit `42b76cd6d79a676ff65c75c724b4d88af8f7f112`. This authorizes source
selection bookkeeping, not parity re-validation. No additional approval is needed
for this observation correction. G5 remains `needs-additive-api`, Parties Story
8.7 blocked, retention open and all six canonical parity receipts Unvalidated.
Actual promotion would require the missing Story 8.11 content/identity-bound G5
closure with named availability/security approvals and I2/I19a hook classification;
I16 current-bound parity re-validation also needs its own approval/evidence.

Acceptance and verification:

- Given selected HEAD gitlinks and a mapped override, when prerequisite fitness
  runs, then observations match the committed selection and strict immutable
  consumption still rejects mismatched/dirty checkouts.
- Given dated rows and receipts, when current observations advance, then their
  historical identity/content bindings remain intact and no parity is promoted.
- Given EventStore owner 8.7 is in progress, when live structural searches replay,
  then they succeed while G5 approval/rollback gates stay closed.
- Given the corrected prerequisite class passes, when the full service assembly
  runs in each mode, then no test fails and every runtime skip is disclosed.

Prepare fresh mapping with pinned `dotnet tool run hexalith-module -- prepare-build
--manifest module/hexalith.module-manifest.v2.json --workspace <fresh-workspace>
--mode source --output json`; use only its emitted environment. Build
`tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj` in Debug with
`--disable-build-servers -m:1`, invoke its built xUnit assembly with
`-class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests`, then
invoke the full assembly. Repeat with mapping environment cleared for the
selected-package Debug lane. Preserve XML per run; inspect selected properties
and warning/whitespace guards. HXW010 (4.44.13 outside Platform's
`[4.45.0, 5.0.0-0)` range) and HXW011 (no local release tag at selected Platform)
remain disclosed even if builds pass. Passing observational builds establish no
supported-toolchain, deployed transport, catalog or full-topology qualification.

The initial corrected source prerequisite run passed 116 and failed two, exposing
a previously masked test-root binding: `Directory.Build.targets` assigned
`HexalithTestRoot:Hexalith.Platform` from `HexalithSourceMappingRoot` (the Parties
root). Git describe consequently inspected the parent repository and exited 128.
Correct that one metadata value to the tool-emitted `HexalithPlatformRoot`.
This consumes existing mapping authority; no generated mapping or dependency
selection is modified. The footprint adds this single configuration line.
The first corrected run and its diagnostics remain retained; rerun targeted
fitness before attempting either full service suite.

Original-failure mapping: the aggregate committed-selection test first stopped
at Builds and masked the EventStore/FrontComposer advances; its current constants
and matrix now observe all five signed successors. The McpCli and Platform theory
cases use the same reconciled sources. The reproducibility failure was the live
owner 8.7 backlog search, now in-progress. The first correction's two subsequent
failures were both Platform describe operations using the wrong emitted root;
the metadata repair resolves their shared cause. Fresh mapped prerequisite
verification passes 118/118 without skips. Full verification is still pending.

## Review Triage Log

Prerequisite correction blind review (seven findings, independently inspected):

- P01 — low, rejected: five unchanged historical identities still inherit the
  current dictionary. They match every dated row today; all five identities moved
  by this correction now have explicit historical values. Complete duplicate maps
  for hypothetical future advances expand the test without a present regression.
- P02 — low, rejected: unchanged historical package rows reference the evaluated
  versions. The independently evaluated seven-package graph is unchanged and
  exactly matches those rows. New historical package maps address future scope;
  no current false acceptance or mismatched receipt was demonstrated.
- P03 — low, rejected: the old current table no longer receives current-row
  describe checks. It is deliberately historical; its entire original body is
  preserved and checked byte-for-byte apart from the heading, while new current
  rows retain exact SHA/describe checks. Additional dated-table parsing adds
  complexity without a changed historical artifact or current qualification gap.
- P04 — low, rejected: the test checks the signoff filename without parsing its
  five records. Investigation directly verifies all five exact records and their
  committed synchronization; the current row is explicitly observation-only.
  A new signoff parser would add a second approval-validation mechanism without
  a demonstrated missing/wrong decision in this change.
- P05 — false: clean matching mapped observations do not call the strict helper.
  This existing observation behavior is unchanged and never claims immutable
  consumption; its output states immutable_consumption=false. The strict helper
  retains mismatch, dirty and initialized-nested-submodule refusals unchanged.
- P06 — low, rejected as spec-only: the initial numbered map predates the metadata
  repair, which is fully documented by the subsequent append-only implementation
  note. That note names the exact file, property, failure and correction; no
  implementation requirement remains absent.
- P07 — low, rejected as spec-only: repeating the four test names in planning
  prose is unnecessary to locate their exact diagnostics in the linked receipt.
  Normal implementation bookkeeping above now maps the four original failures
  and the two subsequently exposed failures to their actual corrections.

No implementation issue remains from this layer and no new work is deferred.

Final verification: mapped-source and selected-package Debug builds both pass
with zero warnings/errors. In each mode the prerequisite class passes 118/118
without skips; the complete service assembly passes 2,852, fails zero, and skips
13 opted-out live-runtime cases. Final warning/CRLF-aware whitespace guards pass.
The fresh receipt is
`tests/dw-27-prerequisite-observations-2026-11-09/validation.json`; it retains the
original four failures, the intermediate Platform-root diagnostics, emitted
mapping, exact environments, XML, skip reasons and preservation evidence.
HXW010/HXW011 and the unsupported-toolchain qualification limit remain explicit.
No new owner decision or deferred issue is required for this correction.
The user's no-commit/push/publish/deploy boundary overrides workflow delivery.

Resumed DW-27 review additionally hardened this new packet's qualification gate.
A separate validate-receipt.py uses explicit checks and explicit receipt/root
arguments, passes normal/optimized Python, and passes 22 isolated positive/negative
fixture cases. It checks mandatory XML, hashes, the disclosed skip inventory,
mode/environment/TargetPath semantics, post-test final input hashes and preserved
files. Original collection/packaging archives retain their exact executed bytes
and are explicitly forensic records with documented replay/timeout assumptions.
The three runnable correction inputs are unchanged after the passing full suites.
Individual findings and dispositions are recorded in the resumed DW-27 spec;
no new work is deferred and no dependency/runtime change is introduced.

Final independent correction recheck confirms the new evidence gate and replay
qualifications resolve all nine patched findings; the disclosed forensic timeout
limit is retained. DW-27's original spec is now done after all required Debug
source/package checks and all three review layers. No new work is deferred.
