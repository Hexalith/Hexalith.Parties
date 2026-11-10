# Five ordinary topology, admin actions and state checks

Approved spec: `spec-five-ordinary-topology-admin-actions-and-state-checks-deferred-items.md`.
Baseline: `ef298d60d21e0a7113672276ea2c0c7e5f512cc3`.
All commands run from the repository root unless `commands-and-results.json` names another directory.
The implementation handoff performs no remote operations. Workflow review, commit-message validation and publication belong to the orchestrator.

## Changes

- Private catalog ACLs and invocation observations retain only selected Dapr identities after deny validation. Complete and isolated profiles preserve modeled grants; original ACL files remain unchanged.
- All six admin action surfaces require renderer interactivity alongside existing authorization, capability, busy and party-state conditions. Standalone action callbacks also enforce their gates.
- Optional metadata decisions admit held bytes and catch only `FileNotFoundError` as absence. JSON records must be objects. Terminal-read refusal remains inside finalization error handling so exact owner cleanup still runs.
- Formatter browser selectors require a unique accordion item, direct definition list and matching term. Actions wait for shell interactivity; nested GDPR assertions follow explicit expansion.
- Live ordinary timing requires a ready topology. Partial-startup teardown can retain diagnostics but cannot qualify the test.

## Executed gates

Every .NET build below completed with zero warnings and errors. Source builds use the pinned preparation environment retained in `source-preparation.json`; package builds use serialized Release builds. Exact commands, exit codes and log paths are in `commands-and-results.json`. Logs are gzip-compressed without changing their captured bytes, except the stopped Aspire baseline's dashboard login token is redacted.

| Gate | Result |
| --- | --- |
| Pinned source preparation | Success; source mapping `a969d0173bbc6bf0f254f32d86370b4d95d78d1eada5f44d9a2704fd723d1839` |
| AdminPortal, source Debug | 339 passed, zero skips |
| AdminPortal, package Release | 339 passed, zero skips |
| Source catalog/model/process cleanup | 90 passed; four separate mTLS live opt-ins skipped |
| Final source catalog configuration | 16 passed, including omitted-caller deny refusal |
| Final package catalog/model/process cleanup | 91 passed; four separate mTLS live opt-ins skipped |
| Repository fitness | 159 passed, zero skips |
| Python lifecycle suite | 137 passed |
| TypeScript typecheck | Passed |
| Browser UI lane, retries disabled | 79 passed |
| Formatter browser spec, retries disabled | 2 passed |
| Three held-negotiation admin cases, five repeats, retries disabled | 15 passed |
| Runtime hosts refreshed after all test builds | Passed; packaged EventStore 4.2.0 cohort |
| Ordinary live DCP timing | 1 passed, zero skips |

The four skipped controls belong to the existing complete mTLS live profile. They provide no live mTLS credit in this packet. The ordinary test is explicitly enabled only for this local run; no CI opt-in changed.

## Before/after evidence

- `catalog-baseline.log.gz`: the ordinary and suffixed ordinary rows retain absent `sample-blazor-ui` and fail; both complete-profile rows pass. All 16 final configuration controls pass afterward.
- `admin-standalone-baseline.log.gz`: three of four renderer/capability cases fail before the guards. `admin-parent-baseline.log.gz`: Static fails and Server passes. Both source and package full suites pass afterward.
- `admin-held-baseline.log.gz`: the negotiation-held control finds Edit enabled before the fix. The final control includes an active consent seeded through the UI so revoke availability is also tested; all five repeats pass.
- `run-lifecycle-baseline.py` replays the new per-site controls against the frozen baseline module without changing working files. All 29 original controller controls pass; the new cases expose 16 failed assertions and two incorrect outcomes (a missing snapshot-refusal field and receipt polling to timeout). `lifecycle-after.log.gz` records all 137 passing lifecycle controls afterward. The swap controls continue to return the bytes admitted through the held descriptor.
- `formatter-baseline.log.gz`: the admin action races circuit readiness and the consumer selector misses the accordion. The first corrected run, retained in `e2e-focused.log.gz`, caught an overly strict assumption that consumer terms were direct children of `dl`. The final selector keeps the direct list requirement and permits its term wrappers; `formatter-after.log.gz` passes both cases.
- `admin-baseline.log.gz` is an initial mixed class/method filter that matched zero tests. It contributes no evidence; the separate standalone and parent baseline runs above replace it.

## Ordinary DCP measurements

`ordinary-launcher-stop-timing.json` records a ready topology (`startup.available=true`), the subreaper launcher path, all five modeled launchers captured, no survivors, and no daprd outliving its launcher. All five launchers have DCP state `Finished` and exit code 143; none exits 137.

| Observation | Milliseconds |
| --- | ---: |
| Last owned worker exit | 9638.2 |
| Owned-worker quiescence | 10070.6 |
| Total stop | 13608.6 |
| Launcher exits after their DCP stopping observations | 111.7–140.3 |

Sampling resolution is 10 ms. Launcher grace remains 10 seconds; the quiescence budget remains 30 seconds. The timing JSON's `stopIssuedMs` values are observed DCP stopping notifications, not independent measurements of signal delivery.

`runtime-profile/` retains the complete preparation manifest and its gzip-compressed project receipts; decompression restores the captured bytes. Selected published families are EventStore 4.2.0, Tenants 7.0.5, Memories 3.0.25, FrontComposer 4.7.6 and McpCli 2.0.1. `tool-versions.log.gz` records the local toolchain. `implementation-files.json` binds the implementation and test source bytes.

## Remaining boundary

The Builds-owned `receipt_owner()` path reopen remains deferred under its existing ledger entry. This batch hardens Parties receipt presence and regular-file admission; it does not change the pinned Builds helper. No dependency, producer, ACL default or optional service setting changed.


## Detail visibility review correction

The action-gating edit also changed `DetailSurfaceOpen`, causing loaded route detail to report closed during Static rendering. The original `_detail is not null || _isDetailLoading` expression is restored. The existing Static/Server route-detail control now requires both `data-detail-open` and `data-sheet-open` to remain `true` while retaining its renderer-dependent action assertions.

The strengthened control fails against the introduced guard (Static fails with `data-detail-open=false`; Server passes). After restoration, the full AdminPortal suites pass 339/339 in prepared source Debug and package Release. The rebuilt package Release browser host passes all 28 admin/formatter controls, including phone layouts, plus 15/15 held-negotiation runs across five repeats with retries disabled. All three corrective builds have zero warnings and errors. Exact commands and results are the `review detail visibility` entries in `commands-and-results.json`; their logs use the `review-detail-visibility-` prefix.

The earlier ordinary timing JSON, log and runtime receipts remain unchanged as evidence of that actual run. This correction changes route-detail presentation only; topology startup, identities, authorization, catalog and launcher/stop behavior are unchanged, so the ordinary runtime was not repeated. `runtime-implementation-files.json` preserves the source hashes associated with that earlier run; `implementation-files.json` now binds the corrected implementation and strengthened test. The earlier preparation receipts do not describe the subsequent browser-host rebuild.


## Review follow-ups: focused checks

The real lifecycle fixture now publishes its complete execution marker through a same-directory temporary file and atomic rename. Isolated Shouldly controls hold a deliberately partial temporary file while proving that the final name remains absent or retains its previous complete marker, then admits only the complete replacement. These two local controls do not invoke the real platform fixture or provide hosted qualification credit.

The controller refuses decoded non-object public results and normalizes decoder recursion failure to an admission ValueError. A genuinely nested terminal is decoded through the stdlib recursive decoder path so the control remains meaningful when an accelerator accepts deep arrays iteratively; exact owner cleanup still runs and finalization retains uncertainty.

Parent renderer coverage now includes toolbar, rows, first/last-page availability, rich-search availability and list retry conditions while retaining both detail-sheet visibility assertions. Real interactive paging and accepted-edit requests cover pending-read guards. The accepted-edit control uses a real form submission and no renderer transition. Standalone controls cover revoke/restrict/lift callbacks, successful confirmations and retained confirmation callbacks after capability withdrawal. Browser controls recheck counters, confirmation surfaces and route after hydration, and require visible formatter lists and values.

Only focused checks ran in this handoff: publisher 2/2, parent renderer 14/14, standalone 7/7, Python 4/4, browser 3/3, and typecheck. Both targeted builds have zero warnings and errors. Exact commands and logs are in `review-followups.json`. Final broad verification remains with the orchestrator. The earlier ordinary runtime evidence and its source snapshot remain unchanged; formatter CI enrollment and descendant-capture deferrals are unchanged.


## Final verification after review

`final-validation.json` records the orchestrator's final commands and outcomes. After all review patches, AdminPortal passes 354 tests in both source Debug and package Release, lifecycle passes 139, repository fitness passes 159, package catalog/model/process controls pass 91 (four existing mTLS live opt-ins remain skipped), and source catalog configuration passes 16. The atomic marker publisher builds without warnings in both configurations and its two local publication controls pass in each; they provide no real-platform qualification credit. Typecheck passes.

The first combined browser run passed 80 cases and exposed a pre-existing picker input filled before hydration replaced it. The shared interactivity helper now guards both picker navigation and the admin bridge. All 15 picker repeats then pass; the fresh combined UI/formatter gate passes 81/81 and all 15 held-negotiation repeats pass with retries disabled. The original failed run remains in final-browser-ui-and-formatters.log.gz; final-browser-runs.json binds the successful replacements.

Review also repaired atomic execution-marker publication, rejected non-object public results, normalized JSON decoder-depth refusal so exact cleanup still runs, and strengthened parent/callback/visibility checks. The two pre-existing CI enrollment and observer completeness follow-ups remain in the ledger. Original ordinary DCP evidence remains unchanged, with its executed source hashes preserved separately in runtime-implementation-files.json.

The exact commit message in commit-message.txt passed repository-pinned commitlint 21.2.4 with zero problems or warnings; commitlint.log.gz preserves that validation.
