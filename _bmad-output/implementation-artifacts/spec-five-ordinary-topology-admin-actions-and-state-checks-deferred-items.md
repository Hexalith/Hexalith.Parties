---
title: 'Five ordinary topology, admin actions and state checks deferrals'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
baseline_commit: 'ef298d60d21e0a7113672276ea2c0c7e5f512cc3'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open ledger items block ordinary-topology qualification or leave inconsistent admin controls, unsafe metadata checks and stale browser selectors.

**Approach:** Complete the following five items as the requested batch: ordinary development catalog finalization; live ordinary-launcher stop timing; admin command-panel interactivity gates; remaining qualification metadata path checks; shared portal formatter browser selectors. Record executed evidence, review the changes, validate the commit message, and push the completed work to `main`.

## Boundaries & Constraints

**Always:** Preserve authorization, deny defaults, cleanup ownership, user changes and runtime opt-ins. Gate admin actions until `RendererInfo.IsInteractive`, retaining other disabled conditions. Count live timing as complete only after successful DCP stop. Inspect root submodules before committing.

**Never:** Enable optional services to satisfy catalog construction, weaken ACLs, change producers/dependencies, enable ordinary live tests in CI, or close blocked items without evidence. The Builds-owned receipt reopen remains deferred.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Catalog subset | Optional sample caller absent | Private ACL and invocations retain only modeled callers | Preserve deny-bearing input refusal |
| Catalog complete | All callers selected, including isolated IDs | Existing modeled grants remain | Original ACL files unchanged |
| Admin prerender | Circuit negotiation held | Actions disabled with `aria-disabled`; no operation from clicks | Existing busy/authorization guards remain |
| Metadata admission | Absent, linked, swapped or malformed metadata | Only absence follows absent/poll behavior; held reads return admitted bytes | Refuse unsafe entries; exact cleanup still runs |
| Formatter sections | Admin detail and consumer profile accordions | Existing date/boolean expectations remain scoped to correct item | Missing or duplicate regions fail |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogConfiguration.cs`, `Receiver()` -- renders private ACL and invocation observations; filter both against selected Dapr identities after deny validation. `PartiesDevelopmentCatalogBootstrap.cs`, `CompleteCoreAsync()` currently fails resolving absent `sample-blazor-ui`.
- `tests/Hexalith.Parties.IntegrationTests/Topology/DevelopmentCatalogConfigurationTests.cs` -- complete-profile controls; add subset and isolated-ID rows. `PartiesOrdinaryLauncherStopTimingTests.cs` already measures CLI/daprd exits, DCP status 143 and 30-second quiescence.
- `src/Hexalith.Parties.AdminPortal/Components/{PartiesAdminPortal,ConsentManagementPanel,RestrictionActionsPanel,PartyGdprOperationsPanel,PortabilityExportPanel,ProcessingRecordsPanel}.razor` -- action surfaces. Reuse ConsumerPortal interactivity gates and `AdminPortalBunitContext`.
- `tests/e2e/specs/admin-parties-list.spec.ts` -- held-negotiation controls and fixture request counters. `tests/e2e/helpers/interactive-shell.ts` -- established readiness helper.
- `scripts/qualify-disposable-ci.py` -- reuse `held_directory`, `read` and `read_bytes` for journal/inputs, state fallback, receipts, launch recovery, markers, terminal snapshots and attachments. Keep terminal refusal within `finalize_owner` cleanup handling.
- `tests/ci-lifecycle/{test_qualification_controller,test_launch_recovery}.py` -- existing metadata swap and recovery controls.
- `tests/e2e/specs/shared-portal-display-formatters.spec.ts` -- replace `sectionWithHeading()` with unique accordion items containing direct `dl` children; expand GDPR operations before its nested assertions.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- five open entries identified by these summaries; update only with demonstrated outcomes.

## Tasks & Acceptance

**Execution:**
- [x] Catalog configuration and tests -- demonstrate the absent-sample failure, then narrow private policies and invocation observations together; cover ordinary, complete and suffixed identities without widening grants.
- [x] Admin components and tests -- gate each standalone panel and parent `Can*` properties; retain callback guards. Cover Static/Server renders, busy/capability states and held negotiation across six panels.
- [x] Qualification controller and recovery tests -- demonstrate unsafe existence decisions before fixing them. Add optional held reads catching only `FileNotFoundError` and safe regular-file receipt admission; cover each migrated decision and cleanup after terminal-read refusal.
- [x] Formatter browser spec -- use accordion item semantics, await interactivity before actions and preserve compact admin dates, plain consumer dates, boolean transitions and empty date-of-birth checks.
- [x] Ordinary live test -- refresh runtime hosts after test builds, execute and retain timing JSON. Repair Parties-owned startup/stop defects; report external blockers accurately.
- [x] Evidence packet and ledger -- record commands/results and baseline failures and close only proven items; remaining workflow review and authorized commit/push follow implementation acceptance.

**Acceptance Criteria:**
- Given each reproduced defect, when its focused control runs before and after the fix, then it fails before and passes afterward.
- Given a Static or Server renderer, when admin panels render, then action availability matches interactivity and existing guards; held negotiation sends no operation.
- Given absent or unsafe metadata, when each migrated state decision runs, then only genuine absence takes the absent branch and metadata refusal cannot suppress exact owner cleanup.
- Given a ready ordinary topology, when DCP stops it, then all captured launchers exit 143, no owned worker survives and observed quiescence fits 30 seconds.
- Given affected lanes, when verification runs, then controls execute and failures remain visible without weaker gates.

## Implementation Notes

- Spec approved by the user on 2026-11-10. The implementation handoff handles local changes and verification; the orchestrator completes workflow review, commit and push after acceptance. No remote operations during implementation.

- Implementation audit: all five ledger rows are closed with executed controls. AdminPortal passes 339 tests in each dependency mode; final browser checks pass 28 cases and 15 negotiation repeats. Lifecycle passes 137, repository fitness 159, package model/process controls 91, and the UI gate 79. Four separate mTLS opt-ins remain skipped.
- The orchestrator caught and corrected an unintended detail-sheet visibility gate, with a failing Static assertion before restoration and passing Static/Server assertions afterward. Runtime evidence retains its executed source hashes separately.
- Matrix audit: catalog subset/complete/suffixed and deny rows run in configuration controls; prerender rows run in component and held-negotiation browser controls; metadata link/absence/swap/malformed/cleanup rows run in lifecycle controls; formatter assertions run in both final browser cases. Every matrix row has passing executed coverage.
- Machine runtime receipts are gzip-compressed without altering captured bytes. Workflow review, commitlint validation and push remain orchestration steps.

- Final review patches pass all affected gates: AdminPortal 354 in each mode, lifecycle 139, fitness 159, package model/process 91 with four separate opt-in skips, source catalog 16, and local publisher controls 2 in each mode. Browser UI plus formatters passes 81, admin negotiation repeats 15 and picker repeats 15, all with retries disabled. The first final UI attempt exposed a pre-existing picker hydration race; using the shared interactivity wait corrected it. See the packet final-validation.json and final-browser-runs.json.

## Spec Change Log

## Review Triage Log

Review pass 1 ran all three layers; none was skipped. Each finding was assessed before grouping.

- BH1 — medium, patch: the marker producer writes directly to its final name, while the new strict reader refuses an in-progress JSON document. An ordinary poll can now fail; publish the complete marker atomically and control that boundary.
- BH2 — medium, patch: capture_result accepts an array and returns exit zero (reproduced). Its changed held-byte admission must retain the object contract before recording a public result.
- BH3 — low, defer: the formatter file was already excluded from both maintained browser scripts before this change. The repaired tests pass directly, but CI enrollment remains a separate pre-existing maintenance gap.
- BH4 — low, patch: the new browser control ends its no-operation assertions before releasing negotiation. Add the same counters, confirmation and route assertions after activation to cover the entire observed interaction.
- BH5 — low, patch: the standalone theory checks all disabled states but directly exercises only add/export/refresh/restrict-open callbacks. Add reachable revoke/lift/confirmation callback checks, retaining the confirmation step.
- BH6 — low, rejected: production prerender uses Static/false and the connected renderer Server/true, which the controls cover. A synthetic Server/false row adds no demonstrated caller state here; testing another arbitrary renderer label is unnecessary complexity.
- BH7 — medium, defer: descendant read failures can omit a CLI/runtime from the pre-existing timing observer. The executed packet captures both roles for all five launchers, so this does not invalidate the current qualification; fault-injected capture refusal is a separate observer-hardening follow-up.
- BH8 — false, rejected: the launcher exits 143 only after waitpid reports no remaining children; the test requires that status and observed worker exits within budget. That already enforces containment. Timestamp ordering is a sampled diagnostic, and every retained record reports daprdOutlivedLauncher=false.
- BH9 — low, rejected: complete technical, Admin, Tenants and duplicate-verb controls already pin retained methods and verbs. The new path only filters callers before the unchanged grant processing; duplicating those grant inventories in every subset row adds no distinct demonstrated branch.
- BH10 — low, patch: text-only assertions do not require the newly selected accordion content to be visible. Add direct visibility assertions for the selected list/value while preserving all formatting checks.
- E1 — medium, patch: a roughly 600 KB nested terminal record raises RecursionError and skips exact cleanup (reproduced with cleanup_calls=0). Normalize this decoder refusal to ValueError at optional JSON admission so existing finalization handling runs cleanup.
- VG1 — medium, patch: the verified regression gap is real: the Static/Server theory and held browser case omit parent toolbar/row/paging controls. Extend the component controls, including conditional retries, without changing their existing guards.

Patch groups preserve the separate marker publication, public-result shape, decoder-depth, post-release browser, standalone callbacks, formatter visibility and parent-list coverage causes. No intent_gap or bad_spec entry requires loopback.

- Review resolution: all seven patch groups are fixed and the orchestrator reran the affected full gates. The extra picker readiness correction is supported by the retained failed run and passing repeated/browser gates. No unresolved implementation finding remains; the two pre-existing deferrals are recorded separately.

## Verification

- Build affected projects individually in Debug with repository source preparation; use serialized Release/package builds for topology parity. Keep warnings as errors; invoke built xUnit DLLs with `-class`/`-method`.
- Run catalog/model and process-cleanup controls in IntegrationTests, AdminPortal component tests, and affected repository fitness checks.
- `python3 -B -m unittest discover -s tests/ci-lifecycle -v` -- all lifecycle controls pass. Baseline: 29 controller tests pass.
- From `tests/e2e`: `npm run typecheck`, `npm run test:ui -- --retries=0` and the formatter spec with `--project=chromium --retries=0`; repeat affected admin cases five times with retries disabled.
- After test builds: `bash scripts/prepare-aspire-runtime-hosts.sh --gateway-profile packaged --configuration Release`; execute `PartiesOrdinaryLauncherStopTimingTests` from the built IntegrationTests DLL with `PARTIES_ORDINARY_TOPOLOGY_TESTS=true` and `PARTIES_ORDINARY_TOPOLOGY_TIMING_PATH` pointing into the new evidence packet.
- Validate the exact temporary commit-message file with the repository-pinned commitlint; fetch and inspect divergence before pushing.

Planning baseline: `ef298d60d21e0a7113672276ea2c0c7e5f512cc3`, clean `main`.
