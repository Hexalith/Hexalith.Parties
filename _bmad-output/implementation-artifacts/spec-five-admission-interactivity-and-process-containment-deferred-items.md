---
title: 'Close five admission, interactivity and process-containment deferrals'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
baseline_commit: '7181fa29db850a75d88462f7099342b251a7af1a'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open Parties-owned deferred-work entries remain at HEAD
`7181fa29`. First, lifecycle `write()` and `private_directory()` check
ancestors for links by name and then act on the path. Second, consumer-portal
controls accept input before Blazor interactivity. Holding `_blazor/negotiate`
shows that Save submits the edit form natively (POST `/me/edit`) and discards
the typed values, while consent and erasure clicks are lost. Third, the
topology fixture's ordinary `dapr` CLI path can lose a daprd child that is
orphaned before the first poll. Fourth, the Memories sidecar token-emission
evidence predates the current Aspire 13.6.4 and Toolkit 13.6.2-beta.959 pins.
Fifth, the slow-filesystem snapshot-deadline risk (L2228) points at no code in
Parties.

**Approach:** Make small, reversible fixes, each with a red-proof control.
Settle the token-emission entry with fresh manifest evidence, and settle L2228
by tracing. Every other open entry is owned outside Parties (EventStore,
FrontComposer, Builds, Platform, Memories) and stays untouched.

Decision (owner, 2026-11-10): until `RendererInfo.IsInteractive`, disable the
edit fieldset and Save, the consent switches and the erasure button. This
matches FrontComposer's generated command forms. A control that holds
`_blazor/negotiate` must show disabled controls, no command and no native POST.
The admin edit page has the same hazard and is recorded as a new deferral, not
changed here.

Decision (owner, 2026-11-10): the fifth entry settles L2228 by tracing. Parties
has no snapshot capture/export code; the nearest match is Platform
`PlatformCurrentOwnerSourceState.cs:26-67`. Reassign the entry to Platform with
that evidence. This changes the ledger only and gets no code-fix credit.

Decision (owner, 2026-11-10): keep the full spec (about 2,600 tokens) as one
batch.

## Boundaries & Constraints

**Always:** Keep the existing read() refusals, file modes (0o600 files, 0o700
directories) and atomic replace. Keep the single domain request and the status
assertions in each e2e case, plus the existing `blazor.web.js`-held control.
Keep the mTLS `dapr-session` path and its PID-continuity proof unchanged. Keep
the AppHost publish model and the secret parameters unchanged. For .NET checks,
build package-mode Release with `-warnaserror`. Write new evidence only to the
new packet `tests/five-admission-interactivity-and-process-containment-2026-11-10/`.

**Never:** Change dependency versions, gitlinks or submodule contents. Add a CI
dependency. Weaken the warnings-as-errors gate. Edit existing evidence packets.
Claim sidecar token emission without manifest bytes that show it.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Write, parent swapped after admission | Ancestor replaced by a symlink between check and create/replace | Nothing is written through the link | `ValueError` |
| Write, ordinary | Private, unlinked tree | File 0o600, directory 0o700, atomic replace | — |
| Pre-interactive controls | Negotiation held; Save, Enter, consent switch or erasure clicked | Controls disabled; no command, no native POST, values kept | — |
| Interactive controls | Circuit established | Controls enabled; existing single-command behavior | Unchanged |
| CLI wrapper exits first | Fake CLI forks a child and exits before the first poll | Child is adopted, then terminated at teardown | Fail closed if not adopted |
| Sidecar manifest | docker/k8s/aca publish on the current pins | Recorded exactly: bindings present or absent | Entry stays open if absent |

</frozen-after-approval>

## Code Map

- `scripts/qualify-disposable-ci.py:120-171` -- `open_unlinked` and `read_bytes`, the held `O_PATH` ancestor walk to reuse. `private_directory` is at :179-183 and `write` at :186-197 (mkstemp + `os.replace`, no fsync). `write` callers: :260/263/265/456/462/463/524/534/696. `private_directory` callers: :430/431/522/603/657/775/948/970/976/1018. The `publicResult` creation at :625 uses `O_NOFOLLOW` only on the last component.
- `tests/ci-lifecycle/test_qualification_controller.py:36,96-177` -- the existing link and swap controls, which patch `Q.os.open`, `Q.os.stat` and `Q.os.fstat`. Writer swap controls go here.
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor:78-120` -- the `EditForm` with a per-instance `FormName`; Save is disabled only by `_isSaving`. `MyConsentPage.razor:72-76` -- `FluentSwitch`. `MyPrivacyPage.razor:44-46,151-156` -- the erasure button. `App.razor:10,13` -- global `InteractiveServer` with prerender.
- `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.SourceTools/.../CommandFormEmitter.cs:649-651` -- the gating pattern to follow (read-only).
- `tests/e2e/specs/consumer-portal-routes.spec.ts:166-204` -- the `blazor.web.js`-held control: counts commands via `domainCommandCount` (:662-670) and checks a single script request. Click targets are at :27-31. The interactive helper is `tests/e2e/helpers/interactive-shell.ts`.
- `src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs:182-199` -- the `dapr-session` launcher; do not change it.
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs:680-768,817-938` -- owned-worker polling, the `dapr-session` detection and `EnsureCliWrapperOwnership`. The fixture can set `DaprOptions.DaprPath`, as `PartiesTopologyCleanupTests.cs:478` does.
- `tests/PackageTests.Shared/PackageTestProcessSupervisor.cs:120-190` -- an existing `PR_SET_CHILD_SUBREAPER` supervisor. IntegrationTests does not link it yet (`.csproj:52` links only `TestInfrastructure.Shared`).
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesTopologyCleanupTests.cs:460-654` -- process-only Linux controls (`OrdinaryCliWrapper_*`, `WrapperFirstExit_*`) and the Docker-gated `LiveShutdown_*`.
- `src/Hexalith.Parties.AppHost/Program.cs:238-260` -- Memories publish tokens. `tests/.../Topology/PartiesPublishEnvironmentTests.cs:85-114` -- model-only sidecar assertions with the "Toolkit drops these variables" comment. The prior record is `_bmad-output/implementation-artifacts/tests/five-publish-and-process-safety-2026-10-23/sdk-sidecar-emission.json`, which holds the exact publish commands.
- `references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformCurrentOwnerSourceState.cs:26-67` and `PlatformCurrentOwnerSourceInventory.cs:14-17,79` -- synchronous `File.ReadAllBytes` between deadline checks; read-only evidence for L2228. That spec's IB-05 cites an 8202-entry owner. The source spec named in the ledger, under `/tmp`, no longer exists.
- Ledger rows, by summary (line numbers at `7181fa29`): held-descriptor writes (L2465); negotiation-held control (L2473); CLI wrapper quiescence (L1788); sidecar token emission (L1863, with the dependent L1663); slow-filesystem snapshot deadlines (L2228).

## Tasks & Acceptance

**Execution:**
- [x] `tests/ci-lifecycle/test_qualification_controller.py` -- add writer and directory-admission swap controls (parent to symlink after the check; final-entry swap before replace). They must fail on baseline.
- [x] `scripts/qualify-disposable-ci.py` -- share the held ancestor walk. Create missing components with `mkdir(dir_fd=)`. Create the temporary file `O_CREAT|O_EXCL|O_NOFOLLOW` 0o600 in the held parent, fsync it, and `os.replace` with `src_dir_fd`/`dst_dir_fd`. Apply the same held parent to `publicResult`.
- [x] `src/Hexalith.Parties.ConsumerPortal/Components/{EditMyProfilePage,MyConsentPage,MyPrivacyPage}.razor` -- add the interactivity gate to the existing `Disabled` conditions.
- [x] `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/` -- add bUnit cases: a non-interactive renderer shows disabled controls; an interactive renderer keeps today's behavior.
- [x] `tests/e2e/specs/consumer-portal-routes.spec.ts` -- add a control that holds `_blazor/negotiate` and asserts disabled controls, no command and no native POST on all three routes. It must fail on baseline.
- [x] `tests/Hexalith.Parties.IntegrationTests/` -- run the ordinary CLI under a test-owned subreaper launcher that forwards SIGTERM and kills after a grace period. Teach the fixture to recognize it. Add a process-only control: a fake CLI forks a child and exits before the first poll; the child must be adopted, then terminated. It must fail on baseline.
- [x] New packet -- rerun the recorded docker/k8s/aca manifest publishes on the current pins. If the bindings are present, add a positive emission assertion and resolve L1863. Otherwise record fresh evidence and keep the entry open.
- [x] `deferred-work.md` -- resolve or advance each row with evidence. Reassign L2228 to Platform with the traced locations. Add the admin edit-page deferral and any review deferrals.

**Acceptance Criteria:**
- Given each red-proof control, when it runs against the reverted code, then it fails; after the fix, it passes.
- Given the full `Hexalith.Parties.Tests` and the IntegrationTests process-only classes, the script `unittest` lanes and `tests/ci-lifecycle`, when they run, then all pass except existing environment-gated skips.
- Given five retries-disabled consumer-portal e2e repeats, when they run, then all cases pass.

## Implementation Notes

- Ledger rows by summary (line numbers at `7181fa29`): held-descriptor writes
  (L2465) and negotiation-held control (L2473) are done; CLI wrapper quiescence
  (L1788) is done with a live-DCP remaining note; sidecar token emission
  (L1863) and its dependent L1663 stay open with fresh evidence; L2228 is
  reassigned to Platform. Two new open rows: the admin create/edit form, and the
  remaining prerendered consumer actions (Export, Cancel deletion request).
- Lifecycle: `held_directory()` is the shared `O_PATH` walk (also used by
  `read_bytes`). `private_directory()` creates missing components 0o700 with
  `mkdir(dir_fd=)`. `write()` creates `<name>.<hex>.new`
  `O_CREAT|O_EXCL|O_NOFOLLOW` 0o600 in the held parent, fsyncs it, re-walks the
  parent and refuses a moved one, renames with `src_dir_fd`/`dst_dir_fd`, then
  removes and refuses a destination entry that is not the written inode.
  `launch()` opens `publicResult` in its held parent. The reverted script fails
  6 and errors 1 of the new controls on Python 3.14 and 3.12 (the ordinary
  write's failure is its new 0o700 intermediate mode); destination-entry swaps
  already pass on baseline because rename never follows the final entry.
- UI: `RendererInfo.IsInteractive` gates the edit fieldset and Save,
  both consent switches (which now also carry `aria-disabled`) and Delete my
  data. bUnit pages render through `ConsumerPortalBunitContext`, which applies
  the renderer at render time (setting it earlier freezes service
  registration). On the reverted pages the three Static rows fail, and so does
  the consent Server row, because baseline switches have no `aria-disabled`.
- e2e: the negotiation-held control waits until `fluent-button` and
  `fluent-switch` are defined. Before the circuit renders, the upgraded input
  has no accessible name, so the control targets
  `fluent-text-input[aria-label="First name"] input`. On the reverted UI all
  three cases fail, and a probe (not committed) records `GET /me/edit` then
  `POST /me/edit` with the typed value gone for both Enter and Save. The
  `blazor.web.js`-held control now forces its button clicks, and the accordion
  case waits for interactivity before focusing Save or a switch.
- Process containment: `PartiesDaprCliSubreaper` writes a 0700
  `dapr-subreaper` Python launcher (`prctl(PR_SET_CHILD_SUBREAPER)`,
  `posix_spawn` of the CLI, signal forwarding, 10 s fixture grace, exit only on
  `ECHILD`). The fixture post-configures it for every non-`dapr-session`
  `DaprPath` on Linux and deletes it after workers are confirmed stopped.
  `EnsureCliWrapperOwnership` additionally requires the wrapper PID's command
  line to run the configured launcher. Red-proofs: the CLI run directly with the
  baseline fixture fails both new controls, and a launcher without the `prctl`
  fails the adoption control. The real dapr 1.19.1 CLI under the launcher
  forwards SIGTERM, daprd exits and the launcher exits 143 in 0.6 s.
- Manifests: docker/k8s/aca publishes on 13.6.4/13.6.2-beta.959 exit 0 with
  no `memories-dapr` env; no emission assertion was added.
- Verification: `tests/five-admission-interactivity-and-process-containment-2026-11-10/verification.json`.
  Not run: a live DCP ordinary-profile topology, Docker-gated suites, hosted
  lifecycle qualification, source-mode Debug builds, commit and commitlint.
- Review follow-up: a real-directory parent swap now reaches the
  `same_file` refusal; the fixture's `PostConfigure` installation and the
  launcher's post-grace SIGKILL have controls that fail when each is broken;
  edit Cancel follows the gate; launcher cleanup is best-effort and last. The
  live-DCP stop timing is a new open ledger entry. See
  `tests/five-admission-interactivity-and-process-containment-2026-11-10/review-followup.json`.
- Post-review verification (orchestrator): all four builds have no warnings.
  Results: service 2964 (0 failed, 13 skipped); ConsumerPortal 91; integration
  process-only 83 (0 failed, 11 gated skips); fitness 196; scripts 74; lifecycle
  120; e2e 155/155 over five retries-disabled repeats; typecheck clean. The
  Python 3.12 lifecycle lane errors 8 times, all on a `pidfd` missing from the
  local build, in tests this change did not touch. Record:
  `tests/five-admission-interactivity-and-process-containment-2026-11-10/post-review-verification.json`.
  Deferrals D1 and D2 were appended to the ledger.

## Spec Change Log

## Review Triage Log

Step-04 review pass 1 (three layers: Blind Hunter (BH), Edge Case Hunter (ECH)
and Verification Gap (VG); none skipped). Reporters are in brackets. No
intent_gap or bad_spec entry was found, so there is no loopback.

- T1 medium, patch [VG1]: no test reaches the `same_file` parent refusal. Every
  swap uses a symlink and fails earlier, at the "linked" check. Replacing
  `require(same_file(...))` with `pass` keeps all 25 controller tests green.
  Fix: add a real-directory parent swap that must fail with "replaced".
- T2 medium, patch [VG2, BH6]: no test exercises the fixture's
  `PostConfigure<DaprOptions>`. Deleting it leaves every test green, and the
  new custody guard then turns itself off. Fix: add a seam test for wrapping,
  `dapr-session` passthrough, no double-wrap and directory removal.
- T3 medium, patch [VG3, BH9]: no test reaches the launcher's SIGKILL
  escalation, because the fake runtime is `sleep`, which dies on SIGTERM. Fix:
  add a variant whose runtime ignores SIGTERM.
- T4 low, patch [BH11, ECH12, VG-other]: Cancel sits in the gated fieldset but
  keeps `aria-disabled="false"` and `Disabled=_isSaving`
  (`EditMyProfilePage.razor:121-124`). Fix: drive both from `_editingDisabled`.
- T5 low, patch [BH7, ECH11]: `Directory.Delete` of the launcher directory runs
  before `CleanupCiDaprMtlsAsync`, so an exception there skips mTLS cleanup and
  the observation cancellation (`PartiesAspireTopologyFixture.cs:677-680`).
  Fix: move it after them and make it best-effort.
- T6 low, patch [ECH2]: `write()`'s `finally` catches only
  `FileNotFoundError` on the temporary unlink. Any other `OSError` leaks the
  parent descriptor and replaces the original refusal. Fix: catch `OSError` and
  close in a nested `finally`.
- T7 low, patch [BH9]: `getattr(signal, 'SIGXFZ', None)` names a signal that
  does not exist, so it is dead code. Fix: delete it.
- T8 low, patch [BH4, ECH3]: the `private_directory` docstring says "private
  0o700", but existing directories are admitted without an owner or mode
  check. Fix: say that only created components are 0o700. The check itself is
  deferred (D1).
- T9 low, patch [BH13]: L1788 is `done`, but live DCP stop timing through the
  launcher stays hidden inside that row. Fix: add a separate open ledger row.
- T10 low, patch [ECH13]: the new prerendered-actions deferral leaves out the
  load-error Retry buttons (`EditMyProfilePage.razor:51-55`,
  `MyConsentPage.razor:47-51`). Fix: name them.
- T11 low, patch [BH16]: `sdk-sidecar-emission.json` does not define
  `${EVIDENCE_ROOT}`, does not say that `rawManifestSha256` hashes the
  uncompressed bytes, and does not record where the aca bicep came from. Fix:
  add these notes.
- D1 low, defer [BH4, ECH3]: directory admission checks neither owner nor mode,
  and a freshly made component is reopened by name. Both predate this change;
  the baseline used `mkdir(exist_ok=True)`.
- D2 low, defer [BH5]: `attachments.lock` and `provisioning.lock`
  (`qualify-disposable-ci.py:1001`) and the `launchRecord` exists check (:695)
  still work by path. Both predate this change and lie outside the entry's
  write() and private_directory() evidence.
- R1 low, rejected [BH1]: under a temporary-entry swap, the rename overwrites
  the genuine destination before the refusal unlinks it. Only a same-user
  writer in the 0o700 directory can cause this, and that writer could delete
  the file anyway. A pre-rename `lstat` would add a guard and still race.
- R2 low, rejected [BH2, ECH1]: a parent moved after the re-check lands the file
  in the held, moved directory. Nothing is written through a link, the ledger
  correctly says "refused before the rename", and a post-rename re-walk is a
  further guard against an unproven hostile writer.
- R3 low, rejected [BH3]: the directory is not fsynced after rename. The
  baseline had no fsync at all, the metadata is per-run on ephemeral runners,
  and the fix needs another descriptor kind.
- R4 low, rejected [BH8]: `IsRunning` accepts `<tool> <launcher>` without an
  exe check. Passing requires a process with the fixture marker, a matching
  start time and DCP's observed PID, which is unrealistic.
- R5 low, rejected [BH9, ECH6]: the launcher does not fail closed without
  `/proc/*/task/*/children`. The fixture's own `CaptureOwnedProcessTree`
  already needs `CONFIG_PROC_CHILDREN`.
- R6 low, rejected [BH9]: exit 125 prints no stderr. This is diagnostic only,
  and the exit code is distinct.
- R7 low, rejected [BH10]: the bUnit "no request" assertions do not click. They
  still pin that rendering issues no command, and the e2e control covers the
  click.
- R8 low, rejected [BH12, ECH14]: a circuit that never connects leaves the
  controls disabled without explanation. Before this change those controls
  looked enabled but discarded input, and the owner decision chose disabling.
- R9 rejected, excluded by intent [BH14]: Platform and upstream issue
  handoffs. The decision says L2228 changes the ledger only, and creating
  issues in other repositories is outward-facing.
- R10 rejected [BH15]: the AC, task and verification-list wording fixes would
  edit this spec. The destination pins and the ConsumerPortal.Tests run are
  already recorded.
- R11 false [BH15, ECH15]: the nine Python 3.12 lifecycle errors are a missing
  `pidfd` in the local 3.12 build. The unchanged code shows the same nine, and
  the earlier packet recorded the same class of failure.
- R12 low, rejected [ECH4, ECH10]: a bare or relative `DaprPath`, or a relative
  PATH entry, would resolve differently. No caller configures either; the
  AppHost sets only the absolute `dapr-session`.
- R13 low, rejected [ECH5]: a host without `python3` or `env -S`. The repository
  already needs `python3` (`PackageTestProcessSupervisor`, scripts), and
  coreutils has supported `-S` since 8.30.
- R14 low, rejected [ECH7]: a SIGKILL of the launcher itself orphans its
  children. The fixture keeps their identities, and
  `ConfirmOwnedWorkersStoppedAsync` refuses rather than leaks. PDEATHSIG would
  not reach daprd.
- R15 low, rejected [ECH8]: when the CLI fails before a stop, the stop reports
  143. The fix adds state tracking for a rare diagnostic.
- R16 low, rejected [ECH9]: a non-BMP CLI path breaks the JSON-to-Python
  literal. Unlikely.
- R17 false [ECH16]: interactive switches now emit `aria-disabled="false"`,
  which is accurate and harmless; the red-proof note already records this.
- R18 low, rejected [BH5]: "linked qualification metadata" is a cosmetic
  message also shared by read().

## Verification

Reuse the exact package-mode commands, the e2e server setup and the red-proof
style recorded in
`tests/five-paging-readiness-and-evidence-readers-2026-11-10/verification.json`
(relative to this directory), including `-p:MinVerVersionOverride=1.0.0`. Run
xUnit v3 tests through the built DLL with `-class`/`-method`. The e2e run needs
`CI=1 PLAYWRIGHT_SKIP_WEBSERVER=1` and the package-mode Release UI on
`http://127.0.0.1:5072`. Record `verification.json` in the new packet with the
same shape, gzipping the logs.

**Commands:**
- Build `tests/Hexalith.Parties.Tests` and `tests/Hexalith.Parties.IntegrationTests`, then run `Hexalith.Parties.Tests.dll` and the IntegrationTests classes `PartiesTopologyCleanupTests`, `DaprMtlsBootstrapTests` and `PartiesPublishEnvironmentTests` -- expected: 0 failed. Rerun `PlatformApiPrerequisitesTests` and `EpicEightClosureFitnessTests` after the ledger edits.
- `python3 -m unittest discover -s scripts -p 'test_*_integration.py'` and `python3 -B -m unittest discover -s tests/ci-lifecycle -v` -- expected: OK.
- The consumer-portal e2e spec with `--retries=0 --repeat-each=5`, and `npm run typecheck` -- expected: pass.
