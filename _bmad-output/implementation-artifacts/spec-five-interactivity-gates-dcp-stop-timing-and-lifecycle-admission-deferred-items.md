---
title: 'Close five interactivity-gate, DCP stop-timing and lifecycle-admission deferrals'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
baseline_commit: '5e006a14c6fabb05de8b6ebb9427b7cfc7e54015'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five Parties-owned deferred-work rows were opened by
`spec-five-admission-interactivity-and-process-containment-deferred-items.md`
and are still open at HEAD `5e006a14`. First, the admin create/edit party
form is disabled only by `_submitting`/`_detailsUnavailable`, so a
pre-interactive Save or Enter can submit natively, as `/me/edit` did. Second,
the remaining prerendered consumer actions are ungated: Export my data,
Download, Cancel deletion request and both load-failure Retry buttons. Third,
the ordinary-profile `dapr-subreaper` launcher has never been stopped under
live DCP. Fourth (D1), qualification directory admission admits an existing
component regardless of owner or mode, and reopens a component it just made
by name. Fifth (D2), the `attachments.lock`/`provisioning.lock` opens and the
`launchRecord` existence check still work by path.

**Approach:** Make small, reversible fixes. Each gets a control that fails on
baseline. Settle the live-DCP row with an opt-in live test and a recorded local
run. All other open rows are owned outside Parties and stay untouched.

Decision (owner, 2026-11-10, "Gate them"): gate Export my data, Download,
Cancel deletion request and both Retry buttons with the same pattern, then
close the row. The admin command panels are equally ungated and become one new
deferral; they are not changed here.

Decision (owner, 2026-11-10, "Keep full spec"): keep the full spec (about
3,900 tokens) as one batch.

## Boundaries & Constraints

**Always:** Keep the consumer gate pattern (`!RendererInfo.IsInteractive`
plus `aria-disabled`) and FrontComposer's generated-form behaviour. Keep each
e2e case to a single domain request with its status assertions, and keep both
existing held controls. Keep `read()`/`write()` refusals, file modes (0o600
files, 0o700 directories), atomic replace and the admission of pre-existing
shared ancestors such as `/`, `/tmp`, `$RUNNER_TEMP` and the workspace. Keep
the `dapr-session` mTLS path, the 10 s launcher grace and the 30 s quiescence
budget unchanged. For .NET checks, build package-mode Release with
`-warnaserror`. Write new evidence only to the new packet
`tests/five-interactivity-gates-dcp-stop-timing-and-lifecycle-admission-2026-11-10/`.

**Never:** Change dependency versions, gitlinks or submodule contents. Add a CI
dependency or enable the live test in CI. Weaken the warnings-as-errors gate.
Edit existing evidence packets. Close the live-DCP row without recorded
live-DCP stop timing. Commit, push or change this spec's status from an
implementation subagent; the orchestrator commits after review.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Admin form before interactivity | `_blazor/negotiate` held on `/parties/new` and edit; Enter or forced Save | Fieldsets, Save and Cancel disabled; no command, no native POST, values kept | — |
| Admin form when interactive | Circuit established | Today's create/edit behaviour, one command | Unchanged |
| Consumer secondary actions before interactivity | Negotiation held; Export, Download, Cancel deletion or Retry | Disabled with `aria-disabled="true"`; no command | — |
| Existing private leaf | Target owned by another uid, or with group/other bits set | Refused before use | `ValueError` naming the path |
| Freshly made component swapped | A different directory, or a non-empty one, appears between mkdir and reopen | Refused | `ValueError` |
| Lock file is a link or not regular | `attachments.lock` or `provisioning.lock` | Refused; nothing is created through the link | `ValueError` |
| Dangling `launchRecord` link | A link exists at the `launchRecord` name | Refused as existing | `ValueError` |
| Live ordinary stop | Opt-in env set; ordinary profile under DCP | Records per-worker exit times and the launcher exit status against the 10 s and 30 s budgets | The row stays open unless the run shows 143 within budget |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor` (no code-behind) -- one page serves create and edit. The `EditForm` (`OnSubmit="SubmitAsync"`) is at 35, with `FormName` `create-party-`/`edit-party-{_instanceId}` at 203. Fieldsets: 46 and 126 are disabled by `_submitting`; 62, 75, 89 and 109 by `_submitting || _detailsUnavailable`. Submit (139-141) uses `_submitting || _detailsUnavailable`; Cancel (144) uses `_submitting` and calls `CancelAsync` (966-973). The submit guard is at 522; `OnAfterRenderAsync` is at 229.
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor:214-215` -- the `_editingDisabled` pattern to mirror. The Retry button (51-55) has no `Disabled`. `MyConsentPage.razor:399-400` has `SwitchDisabled`; its Retry (47-51) has no `Disabled`. `MyPrivacyPage.razor:235` has `ErasureRequestDisabled`. Export (44-46) is disabled by `_isPreparing`, Download (52-54) by `_isDownloading`, and Cancel deletion (160-162) by `_erasureBusy`; leave the always-disabled 168-170 alone.
- `references/Hexalith.FrontComposer/.../CommandFormEmitter.cs:649-651,2085-2090` -- the reference gate (read-only).
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalBunitContext.cs` -- applies `PageRenderer` at render time; Server/interactive is the default. `tests/Hexalith.Parties.AdminPortal.Tests/Components/CreateEditPartyPageTests.cs:26` subclasses `BunitContext` directly, and `PartiesAdminPortalAuthorizationTests.cs` and `PartiesAdminPortalComponentTests.cs` also render the page. Give admin an equivalent context with an interactive default.
- `tests/e2e/specs/consumer-portal-routes.spec.ts` -- the action table is at 35-60. The negotiation-held control (240-300) records main-frame `METHOD path` requests and expects exactly `[GET path]`. `domainCommandCount` is at 767-775.
- `tests/e2e/specs/admin-parties-list.spec.ts` -- create (541), edit (565), and validation (585, 596) fill `/new` right after `goto`, so add `expectInteractiveShell` there. `latestCreateRequest`/`latestUpdateRequest` are at 732-740 and `enableAdminFixture` at 692-701. Reset is `POST /__parties/specimens/admin-portal/reset`; requests are at `GET /__parties/specimens/admin-portal/requests`. `tests/e2e/helpers/interactive-shell.ts` and `helpers/consumer-download-cancellation.ts:10-12` (Export is clicked right after `goto`).
- `scripts/qualify-disposable-ci.py` -- `open_unlinked` 119-137; `held_directory` 140-166 (mkdir at 157, `FileExistsError` swallowed, reopen by name at 160); `private_directory` 203-209; `same_file` 212-213; `write` 216-273. `private_directory` targets: 506 (`$RUNNER_TEMP/hexalith-ci-qualification`), 507 (`ROOT/TestResults/ci-lifecycle`), 598, 679/739 (`temporary/<label>`), and 857/1030/1052/1058/1100 (`evidence/<label>`). There is no trusted-root parameter. The locks are at 1004-1015: `Path.open('a+')` at 1006, then `flock`; the directory is `diagnostics(item)` (591-593). The `launchRecord` `exists()` check is at 700, followed by the held `publicResult` create at 701-707; `recover_launch` is at 676-685.
- `tests/ci-lifecycle/test_qualification_controller.py:180-342` -- the swap controls to follow: capture `real_*`, use a one-shot `swapped` list, patch `Q.os.open`/`stat`/`lstat`/`replace`. `tests/ci-lifecycle/test_cleanup_matrix.py:25` points `diagnostics` at a temporary directory.
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesDaprCliSubreaper.cs` -- the launcher. In `PartiesAspireTopologyFixture.cs`: grace :74, `PostConfigure<DaprOptions>` :206-215, quiescence budget :618, `StopAsync` timeout :630, `QuiesceOwnedWorkersAsync` ~:840-943, `ConfirmOwnedWorkersStoppedAsync` :1004. The ordinary profile is the default constructor (:95) without `GITHUB_ACTIONS`. mTLS is selected by the presence of `ca.crt` (:103).
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesTopologyCleanupTests.cs:801-965` -- `LiveShutdown_*`, the mTLS-only live template (`session.Arguments(true, true)` at :834). `HealthEndpointE2ETests` is statically skipped because of Tenants readiness; don't unskip it. Local probe: Docker 29.8.2 is running, dapr 1.19.1 has a full init, the Aspire CLI is 13.6.4 and DCP is under `~/.aspire/bundle/dcp`.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- the five rows are the last five entries whose `source_spec` is `spec-five-admission-interactivity-and-process-containment-deferred-items.md`.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/` -- add an admin bUnit context with an interactive default and use it in the three classes that render the page. Add cases for Static (controls disabled, no request) and Server (today's behaviour), for both create and edit. The Static cases must fail on baseline.
- [x] `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor` -- add `!RendererInfo.IsInteractive` to every fieldset, Save and Cancel condition, with `aria-disabled`. Guard `SubmitAsync` the same way.
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` -- add a negotiation-held control for create and edit, modelled on the consumer one: Enter plus a forced Save, then assert disabled controls, zero create/update requests, no native POST and the values kept. Then release and assert the controls are enabled. Add `expectInteractiveShell` before the existing fills. The control must fail on baseline.
- [x] `src/Hexalith.Parties.ConsumerPortal/Components/{EditMyProfilePage,MyConsentPage,MyPrivacyPage}.razor` and their tests -- gate Export, Download, Cancel deletion and both Retry buttons. Add bUnit Static/Server rows and e2e held-negotiation rows for Export and Cancel deletion. Add `expectInteractiveShell` to the download-cancellation helper.
- [x] `tests/ci-lifecycle/test_qualification_controller.py` -- add controls for D1 and D2: a foreign-owner and a group/other-mode existing leaf; a mkdir-then-swap to a different directory and to a non-empty one; each lock as a symlink and as a non-regular entry; a dangling `launchRecord` link. They must fail on baseline.
- [x] `scripts/qualify-disposable-ci.py` -- D1: the target leaf of `private_directory` and every component made by the call must have owner `geteuid()` and no 0o077 bits. A freshly made component must reopen as an empty directory with those properties; check emptiness through a readable descriptor. Pre-existing ancestors are admitted as today. D2: open each lock `O_RDWR|O_CREAT|O_NOFOLLOW|O_CLOEXEC` 0o600 in the held diagnostics directory, then require `S_ISREG` and `nlink == 1` before `flock`; don't use `O_EXCL`, because both sides must lock the same inode. Require `launchRecord` and `publicResult` to share a parent and refuse any `lstat`-visible `launchRecord` entry in the held parent.
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/` -- add an opt-in live test gated by `PARTIES_ORDINARY_TOPOLOGY_TESTS=true` (it skips otherwise and is never set in CI). It starts the default ordinary fixture, asserts that `DaprPath` is the `dapr-subreaper` launcher, stops the fixture and records: each `*-dapr-cli` worker's exit time, the launcher exit status (143 vs 137), whether daprd outlived the launcher, and the total stop time. It asserts that no owned worker survives and that the 30 s budget holds. Startup readiness failures still exercise the stop.
- [x] New packet -- run the live test locally and record the timing JSON.
- [x] `deferred-work.md` -- resolve each row with evidence. The live row closes only if the run shows 143 within budget; otherwise it records fresh evidence and stays open. Add the admin-panels deferral (`PartiesAdminPortal`, `ConsentManagementPanel`, `RestrictionActionsPanel`, `PartyGdprOperationsPanel`, `PortabilityExportPanel`, `ProcessingRecordsPanel`) and any review deferrals.

**Acceptance Criteria:**
- Given each red-proof control, when it runs against the reverted code, then it fails; after the fix, it passes.
- Given the full `Hexalith.Parties.Tests`, AdminPortal.Tests, ConsumerPortal.Tests, the IntegrationTests process-only classes, the script `unittest` lanes and `tests/ci-lifecycle`, when they run, then everything passes except existing environment-gated skips.
- Given five retries-disabled repeats of the admin-list and consumer-portal e2e specs, when they run, then all cases pass.

## Implementation Notes

- Ledger rows (by summary): the admin form gate, the remaining consumer actions,
  D1 and D2 are done. The live-DCP stop-timing row stays open with fresh
  evidence. Three rows are new: ordinary catalog finalization (the live row's
  blocker), the admin command panels, and the 5e006a14 gitlink re-derivation.
- Admin: `InputDisabled`/`DetailsDisabled` add `!RendererInfo.IsInteractive`
  to all five fieldsets, Save and Cancel (`aria-disabled`), and `SubmitAsync`
  returns early when not interactive. `AdminPortalBunitContext` (interactive
  default) is the base of `CreateEditPartyPageTests` and
  `PartiesAdminPortalComponentTests`. `PartiesAdminPortalAuthorizationTests`
  only reflects over attributes and never renders, so it is unchanged. Before
  the circuit renders, the admin input has no accessible name, so the e2e
  control targets the `-PersonFirstName-error`-described host's `input`. Each
  field keeps an empty `role="alert"` region, so the control checks only that
  no validation alert text appears.
- Consumer: Export, Download, Cancel deletion and the three load-failure
  Retry buttons (`/me`, `/me/edit`, `/me/consent`) now take the same gate. Download appears only after an export, so its bUnit row
  reaches it through the Export button's `OnClick` callback. The privacy
  accordion case and the download-cancellation helper wait for interactivity
  before using Export.
- Lifecycle: `held_directory(create=True)` reopens each component it made with
  `O_RDONLY|O_DIRECTORY` and admits it only if it is empty and private to the
  user. `private_directory` also checks its leaf. `open_lock` creates or
  reuses single-link regular locks 0o600 without `O_EXCL`. `launch()` uses
  `lstat` on the launch record in the held parent.
- Live test: the default ordinary fixture fails before Build in
  `finalize-package-catalog`. `PartiesDevelopmentCatalogBootstrap.CompleteCoreAsync:360-361`
  finds no modeled project for a receiver invocation's `CallerAppId`. The
  first attempt also hit a stale 4.1.0 prepared cohort, fixed by rerunning
  `scripts/prepare-aspire-runtime-hosts.sh --configuration Release`. No
  launcher starts, so no stop timing exists. The test fails on its
  stop-reached assertion; it skips without the opt-in.
- Pre-existing failures from 5e006a14 (gitlink bump, no file here changed):
  three `PlatformApiPrerequisitesTests` gitlink-selection cases and the
  scripts lane's Builds-gitlink binding test.
- Verification: `tests/five-interactivity-gates-dcp-stop-timing-and-lifecycle-admission-2026-11-10/verification.json`.
- Review follow-up: the live test now asserts a 143 DCP exit for every
  captured launcher and an observed quiescence within 30 s. It records skipped
  launchers, observer and run failures, and writes its JSON on every path.
  Organization-edit Static/Server rows cover the remaining admin fieldset. The
  `/me` Retry is gated. The unresolved ordinary-profile caller is
  `sample-blazor-ui`, which the gateway `accesscontrol.yaml` grants. The D1
  same-user residual is stated. See `review-followup.json` in the packet.
- Post-review verification (orchestrator): all five package-mode Release
  builds have no warnings. Results:
  - Service: 2964 tests, 3 failed (the pre-existing 5e006a14 gitlink
    selections), 13 skipped.
  - AdminPortal 333 and ConsumerPortal 99, none failed.
  - Integration process-only: 84, none failed, 12 gated skips.
  - Lifecycle: 124 OK on 3.14. On 3.12 there are 9 errors, all from the local
    build missing `pidfd`.
  - Scripts: 74 run, 1 failed (the pre-existing Builds binding).
  - e2e: admin-list, consumer-portal and party-picker passed 305/305 over five
    retries-disabled repeats. parties-accessibility passed 17/17.
  - `shared-portal-display-formatters` failed 2 cases. It is pre-existing,
    stale against the accordion markup and not in CI.

  CI on 5e006a14 already fails at "Prepare module build"
  (HXW011/HXW009, Platform/Builds gitlink pins). Record:
  `tests/five-interactivity-gates-dcp-stop-timing-and-lifecycle-admission-2026-11-10/post-review-verification.json`.

## Spec Change Log

## Review Triage Log

Step-04 review pass 1. Three layers ran and none was skipped: Blind Hunter
(BH), Edge Case Hunter (ECH) and Verification Gap (VG). Reporters are in
brackets. No intent_gap or bad_spec entry was found, so there is no loopback.

- T1 medium, patch [BH2, VG-other]: the live test records the DCP exit code
  (143 vs 137) and quiescence but never asserts them. `quiescedMs` is checked
  only when the hook fired. A run where DCP escalates to SIGKILL inside the
  budget would pass. Fix: assert 143 for every captured launcher, and assert
  that quiescence was observed within 30 s when the stop is reached.
- T2 medium, patch [BH4, ECH2, ECH3]: in `CaptureDescendants` the catch sits
  outside both loops and catches only `FileNotFound`/`DirectoryNotFound`. One
  child that exits mid-walk drops its remaining siblings, and an ESRCH
  `IOException` escapes. Fix: a per-child `try`/`catch (IOException)`.
- T3 medium, patch [BH6, ECH3]: the failure callback catches only
  `InvalidOperationException` from `BeginStop`, and the fixture skips
  `DisposeApplicationAsync` when the callback throws
  (`PartiesAspireTopologyFixture.cs:442-449`), so partial startup leaks. Fix:
  catch every exception and record it.
- T4 low, patch [ECH6, BH4]: `CaptureLaunchers` silently skips a modeled
  `*-dapr-cli` with no state, PID, identity or matching command line, so that
  launcher escapes the survivor and budget checks. Fix: record each skipped
  name with its reason, and fail when any is skipped on an available
  topology.
- T5 low, patch [ECH4]: if disposal throws or times out, the sampler and
  watcher are never cancelled and no timing JSON is written. Fix: cancel the
  observers and write the record in a `finally`.
- T6 low, patch [BH5, ECH7]: `daprdOutlivedLauncher` is true when either exit
  is unobserved, including a surviving launcher. Fix: null when unobserved,
  false when the launcher outlived daprd; record that the sampling resolution
  is 10 ms.
- T7 medium, patch [VG1]: no Static row renders the Organization-details
  fieldset, so reverting its gate (`CreateEditPartyPage.razor:75`) keeps every
  test green. Fix: add organization-edit Static/Server rows.
- T8 medium, patch [VG2]: the `/me` load-failure Retry
  (`MyProfilePage.razor:44-48`) is the third consumer Retry with the same
  hazard, and it is ungated. That contradicts the closed "remaining prerendered
  consumer actions" row, and the owner's "Gate them" has one reading. Fix: gate
  it and move `MyProfilePageTests` to `ConsumerPortalBunitContext` with
  Static/Server rows.
- T9 low, patch [BH7, BH8]: the new ordinary-catalog ledger row does not name
  the unresolved `CallerAppId`. It also claims the mTLS profile is
  "unaffected", which was not run, and it omits that the prepared runtime
  cohort must be refreshed before a retry. Fix: name the id, qualify the
  claim, add the prepare step.
- T10 low, patch [BH10]: a same-user, empty, 0o700 substitute for a just-made
  directory is still admitted (Linux has no mkdir that returns a descriptor),
  but neither the docstring nor the D1 resolution says so. Fix: state the
  residual.
- T11 medium, patch (verification) [BH16, VG-other]: `party-picker.spec.ts`
  (admin form bridge on `/admin/parties/new`), `parties-accessibility.spec.ts`
  and `shared-portal-display-formatters.spec.ts` touch the changed pages but
  were not run. CI's `ui-a11y` job runs `npm run test:ui`, which includes the
  picker spec. Fix: run `npm run test:ui` and the formatter spec after the
  review patches.
- D1 low, defer [BH11]: other qualification-state checks are still by-path
  `exists()`, notably `recover_launch` polling the same `launchRecord`
  (`qualify-disposable-ci.py:736-738`). This predates the change and lies
  outside the D2 entry.
- D2 low, defer (T11 outcome): `shared-portal-display-formatters.spec.ts`
  waits for `section[dl]` headings that the `/me` and admin-detail accordions
  no longer render. Both cases fail on markup this change does not edit, and
  the spec is outside CI's `test:ui` set.
- R1 rejected [BH1, ECH10]: fixing the AC/verification wording and the
  "three classes" claim would edit this spec. The deviations are recorded in
  the Implementation Notes and in a ledger row.
- R2 low, rejected [BH3]: the budget constants are duplicated from the fixture.
  They drift only if a fixture constant changes, and the JSON is diagnostic.
- R3 low, rejected [BH6, ECH5]: initialization abandoned at the 7-minute
  `WaitAsync` bound. This needs a hung local opt-in run, and the fix adds a
  linked-token path.
- R4 false [BH9]: in context, the D2 sentence "all are refused or private now"
  refers only to the listed reverted cases, and each of them is. A lock the
  tool created keeps the tool's mode by design, so both sides lock one inode.
- R5 low, rejected [BH10]: there are no symlink or regular-file swap subtests.
  `O_NOFOLLOW`/`O_DIRECTORY` already refuse both, and a component that vanishes
  fails loudly with `FileNotFoundError`.
- R6 false [BH12]: no consumer handler needs a guard, because a static render
  has no event handlers, so a browser cannot invoke one. The Static row calls
  `OnClick` directly only to reach Download.
- R7 low, rejected [BH13]: the Download/Cancel assertions in the privacy bUnit
  test are vacuous. They do no harm, and the e2e rows cover the clicks.
- R8 low, rejected [BH14]: the synchronous no-navigation assertion follows
  the consumer control, whose red-proof caught the native POST. Playwright
  actions wait for any navigation they start.
- R9 low, rejected [BH15]: an e2e branch keyed on the title string is a style
  point.
- R10 rejected [BH17, ECH8]: there is no message while a circuit never
  connects. The owner decision chose disabled controls, matching FrontComposer,
  and the previous batch rejected the same finding (its R8).
- R11 low, rejected [BH18]: the bUnit contexts are duplicated across two test
  projects, and `_retryDisabled` follows its file's `_editingDisabled`.
- R12 low, rejected [BH19]: the matrix route wording is frozen, and the gaps
  in the attempt-1 and attempt-2 evidence are diagnostic only.
- R13 low, rejected [ECH1]: a concurrent writer could fill a just-made
  directory. The script is single-threaded and nothing hands it the path
  first.
- R14 low, rejected [ECH9]: the `RenderFragment` overloads are not
  overridden. The consumer context has the same shape, and no admin test uses
  them.

## Verification

Reuse the package-mode commands, the e2e server setup and the red-proof style
recorded in
`tests/five-admission-interactivity-and-process-containment-2026-11-10/verification.json`,
including `-p:MinVerVersionOverride=1.0.0`. Run xUnit v3 tests through the
built DLL with `-class`/`-method`. The e2e run needs
`CI=1 PLAYWRIGHT_SKIP_WEBSERVER=1` and the package-mode Release UI on
`http://127.0.0.1:5072`. Record `verification.json` in the new packet with the
same shape, gzipping the logs.

**Commands:**
- Build and run `tests/Hexalith.Parties.Tests`, `tests/Hexalith.Parties.AdminPortal.Tests` and `tests/Hexalith.Parties.ConsumerPortal.Tests`, plus the IntegrationTests classes `PartiesTopologyCleanupTests`, `DaprMtlsBootstrapTests`, `PartiesPublishEnvironmentTests` and `PartiesAspireTopologyFixtureTests` -- expected: 0 failed. Rerun `PlatformApiPrerequisitesTests` and `EpicEightClosureFitnessTests` after the ledger edits.
- `PARTIES_ORDINARY_TOPOLOGY_TESTS=true` on the new live test -- expected: timing recorded and no surviving worker.
- `python3 -m unittest discover -s scripts -p 'test_*_integration.py'` and `python3 -B -m unittest discover -s tests/ci-lifecycle -v` -- expected: OK (the known 3.12 `pidfd` errors aside).
- The admin-list and consumer-portal e2e specs with `--retries=0 --repeat-each=5`, plus `npm run typecheck` -- expected: pass.
