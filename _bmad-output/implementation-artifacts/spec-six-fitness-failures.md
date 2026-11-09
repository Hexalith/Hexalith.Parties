---
title: 'Reconcile the six dependency and documentation fitness failures'
type: 'bugfix'
created: '2026-10-28'
status: 'in-review'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix the six existing fitness failures recorded in
`tests/five-data-lifecycle-qualification-2026-10-28/parent-final-verification.json`
under this implementation-artifacts directory. Reproduce them, correct the
EventStore dependency graph without lowering approved dependency floors, refresh
current source/package observations and the Dapr technology table, and preserve
historical receipts, owner acceptance, rollback and blocking tests. Independently
investigate closure records; keep DW-50/82/168/130/149 open unless their actual
closure evidence exists. Run focused checks and full affected projects, complete
independent review, validate exact messages with pinned commitlint, and commit
and push the authorized changes on main.

</frozen-after-approval>

## Implementation Notes

- Baseline Parties main is `207aaf51f63448e51acf83b4c46fdb456df6526c`; working tree was clean. Full canonical revision is verified in the execution receipt.
- Focused invocation of the existing Debug service assembly reproduces all six
  recorded failures: two external graph configurations, the final selection
  receipt, McpCli and Platform source identities, and the Dapr table.
- Tenants unconditionally selects EventStore 3.128.3 despite the central 3.128.4
  selection. Repair in the owning repository; retain the routing-contract floor
  and source-mapping authority. Independently review before publishing the owner
  commit, then adopt that immutable revision in Parties.
- `PlatformApiPrerequisitesTests` current constants and the matrix current block
  lag eight committed root dependencies. Refresh only current observations;
  retain the old block as history bound to its original Parties revision.
  Historical approved constants and matrix statuses remain untouched.
- `docs/architecture.md` table/prose and `docs/project-overview.md` must describe
  the already-selected Dapr 1.19.0 packages. No Dapr dependency changes.
- Independent owner-record discovery found no accepted installed custody/expiry
  packet or deployed historical inventory/writer-drain qualification. All five
  deferred items remain open and their ledger entries stay unchanged.
- New execution evidence belongs in `tests/six-fitness-failures-2026-10-28/`;
  preserve the original qualification packet byte-for-byte. Local Debug builds
  and built xUnit v3 assemblies provide focused and full-project verification.

- Fresh untouched-root compilation succeeded (zero warnings/errors). The
  immediate rebuilt run overlapped the owner graph fix and current document
  edits: two graph cases pass and four observation cases fail. It is retained as
  an intermediate result, not substituted for the original six-failure run.
- Subsequent focused checks exposed stale catalog literals hidden behind the
  first assertions: FrontComposer 4.7.1, Memories 3.0.3, Tenants 5.11.1, Parties
  2.0.0, Dapr toolkit 13.6.2-beta.959, bUnit 2.13.3 and ASP.NET packages 10.0.14.
  Update exact current expectations/table entries without removing assertions or
  changing dependency files. Dapr runtime remains separately selected at 1.18.7.
- Required isolated Aspire precheck started successfully; `aspire wait parties
  --timeout 15` exited 17 with the security resource unhealthy and downstream
  services waiting. Captured state is diagnostic only. Exact owned AppHost stop
  succeeded before final verification; no topology qualification is claimed.
- Full owner Server.Tests passes 1,560/1,560. Owner Contracts.Tests has 193
  pre-existing environment failures from unavailable nested Builds content;
  unchanged baseline classes reproduce exactly the same failing case identities.
  Nested submodules remain uninitialized. Final commands and counts are retained
  separately from earlier builds and intermediate review runs.


## Review Triage Log

| Finding | Verdict / resolution | Evidence |
| --- | --- | --- |
| BH-01: low global package override bypasses assignment floor | medium / patched | Explicit target refuses below-floor global package selection before CollectPackageReferences and PrepareForBuild; actual entry points independently exercised. Exact source selection remains exempt. |
| BH-02: metadata hyphen mistaken for prerelease | medium / patched | Detect hyphen only before metadata delimiter; stable metadata accepted, real prerelease refused, numeric patch 3.128.10 preserved. |
| BH-03: new test process can hang | medium / patched | Concurrent output drains, 45-second deadline and bounded process-tree cleanup. |
| BH-04: other current technology rows stale | low / patched | Complete current table rows reconciled with effective selected catalog, including Memories, toolkit, bUnit and ASP.NET packages; original assertions retained. |
| BH-05: Dapr SDK/runtime distinction unclear | low / patched | Both documents distinguish SDK 1.19.0 from unchanged runtime catalog 1.18.7. |

- Independent follow-up confirms every finding resolved and independently reruns
  all 23 owner controls without failures/skips. No new deferred work.
- The user explicitly authorizes correcting the graph and pushing main. The new
  owner revision's RC ledger entry records only this source adoption, not owner
  API acceptance, deployment, custody, parity recertification or release approval.
- Existing strict source fitness requires a committed root gitlink and clean
  owner checkout. A separately validated local parent adoption commit therefore
  precedes final full parent verification. Parties is pushed only after those
  final suites and the remaining review/evidence checks.
- This spec remains in-review under the baseline's all-tests-pass completion
  requirement because the owner Contracts broad gate retains its 193 independently
  reproduced missing-nested-Builds failures. Publication does not erase that
  blocker or close any of DW-50/82/168/130/149.

- Publication-time fetch found eleven incoming Tenants main commits, ending at
  `d490461f4d6baeff4b2373ef9c54edfcd5e363a2`. Upstream raises its published
  EventStore floor to 3.128.4 for bounded legacy completion. Preserve all incoming
  work, adapt the default/guard and boundary controls to that stronger floor,
  and rerun owner checks and independent review before committing. Earlier
  3.128.3 observations above describe the initial baseline only.


### Resumed independent review — 2026-11-09

The resumed review covers the original two Parties delivery commits and the
Tenants owner fix; later unrelated commits are outside that historical diff.
Each finding below retains its own verdict before shared-root-cause grouping.
The scheduling findings BH-R03/VG-R01 share one patch. Exact reports, reviewed
diff hashes and current execution evidence are retained in the resumed packet.

| Finding | Verdict / resolution | Evidence |
| --- | --- | --- |
| BH-R01: floor also affects nested owners | false | Current target excludes `_TenantsNestedReferenceRoot`; the existing nested-owner control covers this later correction. |
| BH-R02: per-item overrides bypass property floor | false | No owned production project declares the proposed lower VersionOverride or later lower PackageVersion. The owner deliberately pins its EventStore family after catalog import; current graph checks independently verify the resulting items. Arbitrary future project edits are not a demonstrated reachable bypass in this change. |
| BH-R03: direct floor calls miss broken scheduling | medium / patch | A detached BeforeTargets attribute leaves direct-target tests green but both ordinary entry targets accept a below-floor override. Add persistent entry-target controls. |
| BH-R04: source fixture does not prove real mapping | false | The small isolated owner fixture checks selection semantics; the independent mapped-source parent build and its source/graph controls supply the real mapping proof. It is not the only source authority check. |
| BH-R05: cleanup may outlive five-second wait | low / rejected | The timeout already kills the process tree and bounds cleanup; the proposed post-kill race affects exceptional test diagnostics only. No surviving process or normal execution defect was demonstrated; further lifecycle branches are disproportionate here. |
| BH-R06: runtime prose lacks catalog guard | low / patch | Bind the SDK/runtime prose to the effective runtime catalog in the existing documentation check. Current runtime is still 1.18.7. |
| BH-R07: historical build argv/exits are absent | medium / patch | Preserve the historical packet and disclose that its build entries retain hashes rather than complete invocation records. The resumed packet retains exact argv, environments, exits and logs for every new build. No historical arguments are reconstructed as facts. |
| BH-R08: historical seam logs are absent | medium / patch | The old seam receipt cannot prove the omitted diagnostic text. Preserve that limitation and replace ongoing reliance with persistent entry-target assertions and retained fresh execution/mutation logs. |
| BH-R09: evidence packet has no reusable verifier | medium / patch | Add a focused verifier for the resumed manifest, archive hashes, XML identities/counts, six equivalent controls, original packet preservation and owner baseline/current failure equality. |
| BH-R10: clearance lacks exact reviewed diff binding | medium / patch | Retain all three current reviewer reports with the exact historical delivery diff hash and patch follow-up hashes. This does not reconstruct missing historical reviewer transcripts. |
| VG-R01: restore/build scheduling lacks regression proof | medium / patch | Independent mutation demonstrates both hooks can disconnect while direct tests still pass. Group with BH-R03 and cover both normal entry targets, rejected floor/prerelease, accepted package metadata/newer version and exact source identity. |

## Final Verification

- Upstream-aware owner fix published at `6deb5d5dbaad4e26405910b347d45b82deae1057`;
  preserves approved 3.128.4 floor and incoming main. Final focus 25/25, full
  Server 1,584/1,584; full Contracts 578/771, with the same 193 independently
  reproduced missing-nested-Builds failures. Exact owner message passed pinned
  commitlint 21.2.3 on Node 24.22.1; remote main matches the clean checkout.
- Parent local adoption `bcc791d180b7fdc7566c7b52452d53cd6626f5a0` contains only
  the exact owner gitlink and task-scoped source-selection ledger entry. Its
  exact message passed pinned root commitlint 21.2.2 before commit.
- Final parent Debug builds: zero warnings/errors. All fitness classes pass
  288/288, including all six exact originally failing case names. Full service:
  2,801 total, 2,797 pass, zero failures, four existing opt-in runtime skips.
  Full projections: 467 total, 466 pass, zero failures, one existing compatibility
  skip. No test was removed, relaxed or newly skipped.
- Warning/nested-submodule and push-range gitlink gates pass. Original receipt
  verifier plus fifteen negative controls pass. All 36 original packet blobs,
  root package selections and deferred-work ledger match the starting baseline.
- Final evidence is `tests/six-fitness-failures-2026-10-28/README.md` and adjacent
  manifests/XML/logs. Owner records remain unapproved/uninstalled for custody;
  no deployed historical drain/reconciliation packet was found. All five named
  DW items remain open. Broad owner-gate and Aspire precheck limitations remain
  explicit; this spec does not claim all-gates completion.
