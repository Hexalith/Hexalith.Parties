# Published FrontComposer adoption — 2026-10-28

The approved Shell and Contracts.UI 4.7.1 packages publish producer revision
`5688c3eba98567133285b0f51843d9daa8f24f0b`. Package primitives now compile and
execute when `Parties:FrontComposer:UseSharedUiPrimitives=true`; the option still
defaults to false. Registration keeps the corrected local reconciliation because
this package lacks registration readiness. Source readiness has a separate
compilation fence and qualification gate.

| Final verification | Result |
| --- | --- |
| Individual UI/Admin/Consumer Debug builds | Zero warnings/errors |
| Full UI assembly | 445 passed, zero skipped |
| Full Admin assembly | 327 passed, zero skipped |
| Full Consumer assembly | 85 passed, zero skipped |
| Primary Client boundary build/full assembly | Zero warnings/errors; 345 passed, zero skipped |
| Owned Debug package browser UI | 68 passed, zero skipped/retried |
| Node 24.22.1 typecheck/configuration | Passed; 8 configuration cases passed |
| Warning override / CRLF-aware whitespace guards | Passed |
| Fresh pinned source launch | Refused HXR015; no source pass claimed |

The 1,202 .NET passes and 68 browser passes are independent results. The full
assemblies and complete browser run are authoritative after the final focus fix;
older focused/failed receipts retain their original execution scope.

`implementation-verification.json` collects exact argument arrays, working
directories, explicit environment overrides, exit codes and normalized log/XML
hashes. Individual `*-build.json`, `*-full.json`, `browser-ui.json` and
`primary-client-*.json` retain the original command receipts. `browser-ui.xml`
and `browser-cases.json` preserve all native browser outcomes without retries.
`package-source-binary-identity.json` binds actual nuspec revisions, nupkg/DLL
hashes, identical copied DLLs in the host and three test outputs, current restore
asset hashes, operative source/configuration hashes and full repository/gitlink
identities. Source identities are observations, not qualification.

`browser-assets.json` extracts six observations from the native Playwright report:
served `fc-focus.js`, `fc-keyboard.js`, `fc-download.js` and `fc-shell.css` exactly
match the installed selected package. It retains the managed server command,
Test environment, runtime opt-in, Node and Chromium identity. The package tests
perform no asset replacement. A shared browser stream/operation observation seam
now exercises navigation cancellation through the registered service; its separate
source-feedback lane did not qualify here.
The existing semantic and PNG baselines pass unchanged.

Forward controls cover hydrated cold load, client navigation, heading
announcement, skip targets and onward Tab. Compact controls cover desktop,
phone, tablet and repeated resizing, requiring exactly one target and matching
navigation bypass. Shared freshness/live-region controls retain every domain
mapping and polite/assertive/none behavior, with unregistered local rollback.
Real registered package feedback waits for delayed registration and acceptance,
buffers early confirmation, ignores duplicates and drains cancellation.

Admin and Consumer use the actual package download service. Component and browser
checks preserve export bytes, safe names, MIME type and existing failure copy;
repeated Consumer downloads revoke both object URLs and remove their anchors.
Component disposal cancels the same operation ID used for begin/transfer. The
normal package browser case holds the actual browser stream read, navigates away,
observes cancellation of that operation before releasing the read, and proves
no late download, anchor or object URL. The served producer module is unmodified. Typed confirmation rejects empty/case/space mismatches,
retains guarded single submit and warning/description semantics, initially
focuses Cancel and handles Cancel/Escape. The Parties adapter restores focus once
to its recreated initiating control.

The first strict package restore failed NU1109: root EventStore 3.128.3 was below
Tenants 5.11.1's 3.128.4 floor. `ui-initial-build.txt` preserves that refusal.
The human separately approved exactly the root pin alignment to 3.128.4; the spec
records that decision. No other dependency, producer or Builds catalog edit was
made. `effective-properties.json` and the identity receipt bind the final graph.

The first whole browser run passed 67/68; the new return-focus assertion exposed
the trigger recreation problem. Its log/XML remain `pre-focus-fix-*` and the
focused post-fix control passes. `pre-final-*` preserves an earlier Consumer
compilation correction and the prior binary's results; none substitutes for the
final rebuilt 85-case suite.

`source-launch-blocker.json` records the exact pinned-tool command and exit 2.
The canonical evidence retry isolates HXR015: installed Aspire CLI 13.6.4 differs
from catalog-selected Aspire.AppHost.Sdk 13.6.1. The original attempt additionally
had HXE160 for an invalid evidence destination and is historical. No source map,
CLI/dependency selection or producer was changed to bypass the prerequisite.

DW-91, DW-101, DW-102 and DW-112 close only their named package exits. DW-100's
freshness/live-region portion qualifies, while fixed shared-reconciler package
publication/adoption remains open. G4-A, whole G4, Story 8.9, current source launch
and deployment remain separate. Disable shared registration for local component,
dialog and download rollback; the selected Shell package supplies shell fixes.

From the repository root, reproduce individual builds using the exact commands
in their receipts, then invoke the built xUnit assemblies with `-class` for a
focused run or `-result-xml` for a full run. Browser reproduction from `tests/e2e`
uses Node 24.22.1 and `npm run test:ui`; the config owns and cleans its server.
`run-check.py` captures commands after `--`; `--node24` uses `PARTIES_NODE24_BIN`
or the existing task-local Node installation. Public evidence normalizes local
prefixes to `${WORKSPACE}`, `${HOME}`, `${TMP}` and `${NODE24_BIN}` placeholders.
Exact raw originals and their placeholder values remain private.

Run `python3` on this directory's `verify-evidence.py` for read-only hash, result,
asset and disposition verification. `collect-evidence.py` extracts the native
browser report and installed binaries when the build/report outputs exist; it
never creates test results. Primary acceptance, independent reviews and Git
publication receipts are added by the owning workflow after this handoff.

Review corrections cover fallback Cancel/Escape focus, sensitive-context resets,
package navigation cancellation, matching operation IDs and evidence integrity.
`pre-review/` preserves the exact earlier results and its archive hashes;
`historical-results/` preserves individual replaced command outputs. Eleven helper
controls reject stale or incomplete results in normal and optimized Python.
`audit-matrix.py` binds all six approved matrix rows to executed passing cases.

All review findings are corrected. The independent verification recheck confirms
package navigation cancellation and the evidence controls with no unresolved gap.
xUnit case identity uses the unique record ID; truncated theory display names
are permitted while duplicate or missing record IDs fail verification.
