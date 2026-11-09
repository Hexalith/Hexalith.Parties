# DW-27 resumed build verification — 2026-11-09

DW-27 is already active with EventStore 4.1.0. This run reviews the existing behavior and verifies the subsequent catalog-selector alias repair; production and test code are unchanged.

| Check | Result |
| --- | --- |
| Mapped source Debug build | Pass; zero warnings/errors |
| Package Debug build | Pass; zero warnings/errors |
| Focused tests, each mode | 875 handlers + 77 gateway + 9 selectors = 961 passed; zero failures/skips |
| Full service suite, each mode | 2,848 passed, four existing prerequisite failures, 13 skipped |
| Historical fixture, normal and optimized Python | Pass |
| Warning/nested-submodule and whitespace guards | Pass |

[validation.json](validation.json) records exact commands, applied environment overrides, cleared keys, all failure messages/stacks, skip reasons and archive hashes. Adjacent compressed files preserve original command logs, test XML, emitted mapping and run script. Build each mode before its tests: both modes share their output path. To replay on another checkout, rerun the recorded pinned `prepare-build` command with a fresh workspace and use its newly emitted environment; temporary import paths in this packet belong to this run.

The four full-suite failures are the Builds committed-selection observation, McpCli and Platform source observations, and the EventStore sprint `backlog` expectation in `PlatformApiPrerequisitesTests`. Historical records and assertions are preserved. The former three-error catalog alias compilation blocker is cleared. The spec remains `in-review` because the broad suites still fail.

Source preparation reports the existing HXW010 tool-range mismatch (4.44.13 versus Platform `[4.45.0, 5.0.0-0)`) and HXW011 missing local tag. A passing mapped build does not establish a supported toolchain. The HTTP fixture establishes handler/protocol mapping; it does not qualify catalog admission, deployed transport, CI Release or integrated topology.

The exact archival fixture is unchanged. It invokes a monkeypatched verifier in-process using isolated historical inputs. Its generated temporary receipt command is synthetic: the fixture does not install an executable script at that path. The two successful checks establish reconstruction and custom receipt destination only; they do not test CLI argument parsing or standalone command replay. No historical receipt is regenerated and the pre-adoption patch is not applied to the current checkout.

`before.json.gz` binds all tracked root-repository files before this run's documentation update. The final preservation audit permits only the requested spec to differ, plus the newly created packet. No production/test behavior, dependency, Git index or history changes are part of this run.
