---
title: 'Close five deferred CI and release entry-point checks'
type: 'bugfix'
created: '2026-11-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '1783bb2360b08c53276780987b1467c72fb6ba07'
context: ['docs/build-gate.md', 'docs/ci-legacy-reference.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve the five consecutive runner-review deferrals for the root npm bootstrap, the four legacy command entry points, CI Python discovery, empty-cache Aspire preparation and PowerShell test-project inventory. Exercise the commands on the Parties commit and its recorded direct dependencies, repair reproducible failures in those contracts and document how to repeat the checks. Preserve the inventory check on ordinary test lanes and make it possible to check inventory independently. Record local evidence for each ledger entry.

</frozen-after-approval>

## Implementation Notes

The five entries are the consecutive findings R3-B1 through R3-B8 from the runner interruption review. R3-B2 through R3-B5 share one ledger entry. Validation used the direct dependency revisions recorded at the baseline above.

| Deferred contract | Reproduction and result |
| --- | --- |
| Root npm bootstrap | With Node 26.13.1 and npm 11.20.0, `npm ci --ignore-scripts` installed 330 packages. Commitlint 21.2.4 ran. Node loaded each of the four plugins in `release.config.cjs` with semantic-release 26.0.1 installed. |
| Legacy command entry points | The gitlink gate passed `--worktree`, `--diff` and `--push-range`. The latter two used the exact baseline parent. The public verifier passed the two archived hosted artifacts described below and rejected inconsistent fixtures. The lifecycle CLI inspected both restored binaries. The startup observer loaded the pinned Builds helper and rejected an invocation without a runner temporary directory. The controller and observer suites passed 43 and 23 tests respectively. Hosted lifecycle execution and release publication require their own runs. |
| Python test discovery | `python3 -B -m unittest discover -s scripts -p 'test_*_integration.py'` ran 72 tests successfully. This is the pattern used in the workflow. |
| Aspire cold-cache restore | With `NUGET_PACKAGES` pointing to a new empty directory, `bash scripts/prepare-ci-package-dependencies.sh` restored DCP and Dashboard 17.0.0. The lifecycle command `verify-runtime-cache` found both executable (90,792,236 and 86,131,944 bytes). In a separate restored cache, truncating DCP left NuGet satisfied. The original hook returned success and the downstream verifier refused the cache with HXLQ108. The corrected hook exited 1 before writing `GITHUB_ENV`. Run `python3 -B -m unittest discover -s scripts -p test_prepare_ci_package_dependencies.py` to check either empty tool and the successful permission repair and version export. |
| PowerShell inventory | `pwsh -NoProfile -File scripts/test.ps1 -Lane inventory` checked all 27 projects, including RuntimeArtifacts as support. The command uses the same check as the execution lanes. In temporary copies it rejected an unclassified project, a duplicate RuntimeArtifacts entry and a case-only path mismatch. |

### Public verifier

The baseline push and scheduled CI runs both failed in the public verifier. In the scheduled run the native TRX finished at 09:16:47.1082505 UTC, the runner observed native exit at 09:16:47.4756406 and recorded the terminal pass at 09:16:47.5057764. The verifier incorrectly used the terminal pass as the lower bound for every termination record. It now checks native exit against the validated TRX finish. Environment and access termination still require the terminal pass, and every timestamp must match the owner diagnostic and precede or equal final cleanup. Both original archives replayed successfully with their own workflow contexts, the recorded dependency commits and 46 passed native cases each. [Push run](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44925675038) · [Scheduled run](https://github.com/Hexalith/Hexalith.Parties/actions/runs/45005161156)

### Broader local checks

The standalone Release build passed with zero warnings and errors under SDK 11.0.100. Its initial attempt failed with ASPIRE009 when the CLI tried to write to a read-only home. The successful build set `DcpDir` and `AspireDashboardDir` to the two restored 17.0.0 tools directories.

`pwsh -File scripts/test.ps1 -Lane ci` passed 372 of 432 tests after the solution build, with 60 failures and no skips. The initial run before the build had 67 failures. The host kernel has `CONFIG_PROC_CHILDREN` disabled, so the Linux test-process supervisor cannot enumerate children. Five focused package-restore cases passed with the .NET 10.0.15 reference and host packs available to the SDK. All seven focused runtime-routing and SignalR dependency checks passed. Broad Python discovery ran 100 tests before the hosted-verifier changes and reported one suite setup error when the separate EventStore owner publisher exited with `Aspire.Hosting.DistributedApplicationException`.

## Review Triage Log

- Low. Added a passing release-style `--diff` invocation against the recorded parent in addition to the worktree and push-range checks.
- Medium. Added a successful cache fixture that verifies both executable bits and appending the version to an existing environment file.
- Low. Included the exact focused cache regression command in the reproduction table.
- Medium. Fixed case-sensitive project path comparison and verified a case-only mismatch exits 1.
- Medium. Recorded the ordinary CI lane failures, the successful Release build and the focused reruns.
- Re-review confirmed the native-exit bounds against the producer and both archived hosted runs. All 72 discovery tests and the cache regression passed.
