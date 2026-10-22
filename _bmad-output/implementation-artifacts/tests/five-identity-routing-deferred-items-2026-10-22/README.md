# Five identity and routing deferrals — 2026-10-22

Baseline: `d55058ddaa4bfc94c47b799e5f5d9e9eff8dce77`.
The implementation closes DW-139, DW-145, DW-174, DW-175 and the unnumbered
DW-72 dependency-reconciliation entry. Commands, exit codes and retained output
are indexed in `validation.json`. Workspace/home paths are redacted; structured
host logs are omitted from the runner-summary receipts.

The final reviewed service assembly passes 2421/2421, Contracts passes 211/211,
and UI passes 412/412, with no errors, failures, skips or tests not run. Focused
review-correction suites pass identity 267/267, inventory 9/9 and routing 46/46.
`post-review-validation.json` indexes final full builds/suites and implementation
hashes. `validation.json` retains the initial implementation observations. Builds
keep warnings as errors and report zero warnings/errors. Published Shell 4.7.0
and EventStore 3.126.1 remain selected; neither dependencies nor gitlinks changed.

The identity worker boundary admits at most 16 queries process-wide with no
waiting queue. Saturation fails closed. A lease stays held until abandoned work
and associated asynchronous cancellation callbacks have both finished; no test
or implementation claims to terminate noncooperative work. Controlled clocks,
gates and completion signals cover initial/final admission, source replay,
cooperative cancellation, budget exhaustion during capacity acquisition, late
faults, saturation and recovery. Terminal historical validation precedes final
authority admission, with certificate/custody expiry checked at release time.

The privacy inventory explicitly classifies identity properties with contextual
rationales, including opaque linkages, transient proofs/authorization,
independent finite history custody, result wrappers and profile-protected provisioning. Production
annotations are unchanged.

Actual-host tests inspect reserved segments and discovered endpoints, reject
reserved module-name collisions, and request a registered fixture module through
published Shell routes. Parties overview bUnit tests cover alias, canonical and
unknown-tab fallback behavior and authorized destinations at the component boundary. Actual HTTP tests distinguish
the explicit Parties aliases, a registered generic Parties landing for an unknown
tab, and unavailable routes for unregistered modules. Real wrong-role HTTP
requests render the forbidden page inside the successful Blazor SSR shell;
host policy checks also cover Admin, TenantOwner, Consumer and unrelated roles.
These receipts do not promote the broader G4 prerequisites or recertify earlier
owner approvals or parity.

The pinned module launcher was attempted before code edits. An unset Dapr home
was corrected to the existing prepared isolated home; the launcher then failed
HXP020 because the mapped Parties source project is unavailable and explicitly
reported no resources started. Debug/source and full-topology qualification remain
blocked. `startup-baseline.txt` and `startup-summary.json` retain that boundary;
the focused package fallback does not replace it.

The warning guard passes. The whitespace command accepts the CRLF required by
`.editorconfig`; plain `git diff --check` in this checkout interprets added CR
characters as trailing whitespace. Three independent review layers completed and
corrections passed focused and final full verification. Two pre-existing budget
gaps (options access and post-fold selection) remain tracked separately. The exact
commit message passes pinned commitlint 21.2.2 with zero problems or warnings.
Commit and authorized push are performed after the final staged checks.
