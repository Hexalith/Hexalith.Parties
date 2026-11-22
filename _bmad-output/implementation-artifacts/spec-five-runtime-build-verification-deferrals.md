---
title: 'Close five deferred runtime build and verification findings'
type: 'bugfix'
created: '2026-11-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'cc691319b8ee622198f7d8d93a87f85a86b9fd8a'
context: ['docs/ci.md', 'docs/build-gate.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve three findings from the runner review on reuse of a completed standalone solution build. An unchanged incremental build must allow the real packaged preparation command to produce its receipts without building again. Reuse must refuse changed source, build inputs, or runtime outputs. Retain existing fresh-build and package verification checks. Cover the successful preparation path and refusal paths with repeatable tests.

Reconcile two outstanding entries for checks already implemented. Verify the Linux test that waits for the original process to stop before simulating a hung signal helper. Validate the retained direct-owner dependency investigation against its final parent and child receipts, identifying the tested calls and dependency identities. Record evidence and the scope of each result in the deferred ledger.

</frozen-after-approval>

## Implementation Notes

The unmodified standalone Release solution restored and built with SDK 11.0.100 and its pinned direct dependencies. A second unchanged build succeeded. Preparation then failed at the first gateway because its entry DLL predated the new solution start. The ordinary rebuild and package archive checks remain in force.

The focused Linux hung-helper test passed (1/1, zero skips). The current direct-owner test lives in `Hexalith.Parties.Runtime.IntegrationTests`. Run the maintained `ActualOwnerDependencyTraceQualifiesDivergentAssetCounterexamples` method with `PARTIES_MEMORY_OWNER_DEPENDENCY_SERVER` set to the built Memories server DLL and `PARTIES_MEMORY_OWNER_DEPENDENCY_REDIS` set to an owned Redis endpoint. This run used disposable Redis 7.2.11 and the Memories gitlink recorded in the baseline above. The parent test passed with zero skips. The three child receipts each recorded five owner calls, 64 loaded assemblies, no dependency mismatches and a successful exit. The historical final parent/child receipts separately verified at 75 loaded assemblies per child.

## Review Triage Log

- Medium. The first digest omitted deployed configuration and Dapr component copies. Include the outputs identified by MSBuild's copy-to-output inventory and exercise a nested configuration mutation.
- High. A backdated source edit could survive MSBuild's timestamp check. Feed the compiler-input content hashes into the SDK's incremental compile inputs. The real SDK graph regression changes a C# constant, restores the original source timestamp and checks that the DLL changes. An unchanged third build preserves the DLL timestamp.
- Medium. Resolved analyzers and source generators are compiler inputs. Capture their bytes alongside the project's sources, references, imports and analyzer configuration.
- Medium. Capturing external inputs only after solution completion left an edit window. Capture hashes before compilation and compare with the files at completion. An external source removed during the build refuses completion.
- High. A new build could supersede a verified manifest immediately before publication. Invalidate the published manifest at build start and serialize final validation and atomic publication with marker updates. The stale-generation control refuses publication after another build starts.
- Medium. An abandoned output record could prevent a later successful build from completing. Allocate project records to the individual attempt. A truncated earlier record leaves the current attempt usable.
- Medium. A count of 13 could hide an incorrect replacement project. The preparation regression independently checks the required project identities, the evaluated Admin UI choice and each runtime or catalog role.

The second review identified two more cases in the copied-content finding. A backdated source for a PreserveNewest copy can leave stale deployed bytes, and a deployed copy can change after the project receipt is created. The build marker binds every evaluated copy source and verifies the deployed bytes against the candidates for that destination. The project receipt retains the verified copy hashes for completion and launch checks. Regression tests exercise both cases.

The real solution exposed multiple legitimate sources for `appsettings.json` and `appsettings.Development.json` in the aggregate support project and the integration wrapper. Requiring all candidates to match the destination rejected those builds. The corrected check binds all candidate inputs and requires the deployed bytes to match a declared source. The focused control accepts a legitimate selection and refuses a stale copy.

## Verification

Both final standalone Release package builds passed with zero warnings and errors. The maintained incremental regression verified all 13 expected projects and roles. Every entry binary predated the second build and kept its SHA-256 and nanosecond timestamp throughout preparation. The completed manifest bound 28 official package archives.

In the isolated checkout, the launch verifier refused a changed deployed Dapr access-control file and accepted its restored bytes. The build verifier also refused an edited Parties source file with its original timestamp and accepted the original source after restoration.

The Aspire preparation class passed all 135 cases with zero skips. The 45 runtime receipt cases, nine catalog-driver tests, 72 CI evidence tests, marker and SDK graph regressions, and the build policy check passed. The stopped-process and direct-owner tests both passed again on the final build with zero skips.
