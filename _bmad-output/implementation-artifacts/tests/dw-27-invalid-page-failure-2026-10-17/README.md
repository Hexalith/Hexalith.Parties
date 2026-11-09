# DW-27 verification — 2026-10-17

This packet records the **October 17 pre-adoption state**. The later
[October 22 SDK adoption](../five-sdk-runtime-deferred-items-2026-10-22/final-verification.json)
completed DW-27 with published EventStore 3.126.1 and passing package/source
service suites. Its closure is recorded in `deferred-work-archive.md`.
The [November 9 resumed verification](../dw-27-invalid-page-failure-2026-11-09/validation.json)
checks the selected 4.1.0 package separately and records the earlier source-build
and broad-suite blockers. The later
[resumed build verification](../dw-27-invalid-page-failure-2026-11-09/resumed-build/validation.json)
records the catalog-selector repair, passing source/package builds and focused
tests, and the four remaining broad-suite failures.

The patch, manifest and receipts below remain historical evidence. Do not apply
the saved patch to the already-adopted consumer or regenerate these dated outputs
in the present checkout. The restoration verifier deliberately requires the
original pre-adoption consumer bytes and the preserved legacy SDK constructor/test
bytes; reconstruct that historical state in an isolated fixture to exercise it.
The inventory script also writes into this dated packet and is not a current
activation gate. Use fresh output locations for new evidence. A failed archival
command is a failed run; an older receipt records only its original run.

## Historical October 17 results and rollout instructions (superseded)

The remaining results and rollout instructions describe October 17 only.
The October 22 adoption supersedes their open-dependency conclusion and patch
application steps; they are retained to explain the original receipts.

The shared EventStore prerequisite is prepared in its owning repository. Parties
adoption remains gated on publication; its tracked code and package selection are
unchanged. `validation.json` records exact commands, working directories, counts,
and summary/raw log paths. Repository `.txt` files retain concise exact results
and relevant blocker diagnostics. Full original logs are local-only under
`/tmp/dw-27-validation/raw/`; every result records its raw path, byte count and
SHA-256. `evidence-reduction-check.json` binds those originals and confirms the
pre-review patch and manifest hashes were preserved; their archived copies are
recorded there. Initial cleanup reduced repository text logs from 2,211,979 to
13,511 bytes. Subsequent review receipts remain concise and keep raw logs in
the same local directory.

- EventStore Contracts and Server test projects: Debug/source builds pass with
  zero warnings and errors; focused suites pass 112/112 and 116/116.
- Parties consumer candidate: Debug/source build passes with zero warnings and
  errors; final focused handler/gateway tests pass 214/214. The earlier pre-review
  candidate full assembly reports
  1,171 tests, 1,170 passed and one failure: immutable dependency consumption proof
  requires the EventStore checkout to be clean. The required uncommitted
  prerequisite edits prevent that assertion. No assertion or gate was weakened.
- Restored tracked Parties: Debug/pinned-package build passes with zero warnings
  and errors; existing handler/gateway suites pass 146/146, without failures,
  errors, skips or tests not run.
- Whitespace, warning-override and patch-applicability checks pass. The root
  whitespace command explicitly permits the existing CRLF convention. The
  EventStore legacy cursor HTTP test and exception constructor are preserved.

The full EventStore assembly attempts did not complete. After more than five
minutes they had already failed outside the DW-27 classes and remained in
packaging or Dapr-dependent work; their owned processes were stopped (exit 143).
`eventstore-broad-run-blockers.json` records the commands and elapsed times.
Contracts failed
`CorrectiveOciProvenanceReleaseTests.ContainerPublicationRejectsMissingProvenanceInputs("version")`
when the nested publish output did not contain the expected provenance diagnostic;
it also skipped two package-inventory tests because
`EVENTSTORE_PACKAGE_CONTRACT_DIR` was unset. Server failed
`ActorBasedAuthIntegrationTests.ActorRbacValidation_CommandMessageCategoryPassedCorrectly`
with a canceled HTTP request; raw diagnostics include unavailable Dapr/HTTP2 and
host startup/shutdown errors. These failures were not reproduced against the
baseline, so their causes and baseline status are not established. Focused
project builds and all changed classes pass;
full EventStore qualification is not claimed.

`inspect-published-sdk.py` resolves `HexalithEventStoreVersion` through MSBuild,
then inventories the selected and latest public Contracts, Server, DomainService
and Gateway packages without changing dependency selection. The selected version
and each latest stable version resolve to 3.124.0 in this receipt. XML checks are
explicitly documentation observations; they do not prove assembly API absence.
`selected-contracts-dotnet-inspect.json` and
`selected-server-dotnet-inspect.json` retain actual metadata output from the
selected downloaded packages, with commands, package/output hashes and listed
members in `published-sdk-inspection.json`. That metadata does not list
`QueryAdapterFailureReason.InvalidPage` or
`QueryExecutionFailedException.AdapterFailureReason`. This is a package inventory,
not coherent restore or gateway qualification, and it emits no compatibility
verdict. Parties activation remains gated on published support and the required
package/gateway checks.

`parties-consumer-adoption.patch` contains the tested service classification and
both test-file changes. `consumer-patch-manifest.json` binds its hash and the
baseline/tested file bytes. The three tracked files were restored byte for byte;
`git apply --check` succeeds. Run
`python3 _bmad-output/implementation-artifacts/tests/dw-27-invalid-page-failure-2026-10-17/verify-compatibility-and-restoration.py`
only from an isolated checkout containing the original pre-adoption bytes. Its
default destination is the historical `compatibility-and-restoration-check.json`;
use `--receipt` with a fresh path for any new exercise. The current consumer is
already adopted and is expected to fail the restoration assertion.
That evidence includes the raw legacy cursor test source, baseline/current byte
hashes, the unchanged constructor signature hashes, and baseline/current hashes
for every restored tracked consumer file. It also applies the saved patch to
baseline copies in a temporary directory and verifies every tested candidate
hash. All proof checks use explicit failures, including under Python optimization.
`check-verification-script.py` passes normal and `PYTHONOPTIMIZE=1` success plus
corrupt candidate-hash rejection in both modes (four cases), without changing
tracked consumer files, patch or manifest. `verification-script-tests.json`
retains those results.

The review refresh strengthens accepted paging IDs/counts and independent
continuation expectations, tests invalid-size precedence over negative and
conflicting offsets, and documents/formats new public C# test members. It
intentionally updates the patch/manifest binding after 214/214 focused tests;
earlier full-assembly receipts refer only to the archived pre-review patch.
`eventstore-source-test-manifest.json` binds the ten final uncommitted EventStore
source/test/documentation files by SHA-256 to the final passing focused build and
test receipts. `validation.json` records those commands and manifest hashes. No
known blocked full suites were rerun for these review fixes.

After the shared prerequisite is published, verify
the real package contract and gateway mapping, adopt its actual approved version,
apply this patch, and repeat the source and pinned-package checks before closing
DW-27. This task did not publish, deploy, commit, push, or update dependencies.

The earlier Aspire baseline established Parties health only; EventStore, tenants
and UI were Finished. The exact AppHost stopped successfully. Full-topology
qualification remains unavailable as recorded in the spec.

Final parent verification rebuilt the saved candidate and passed 214/214 Parties
tests, 112/112 Contracts and 116/116 Server tests, all four verifier cases, the
restored pinned-package build, whitespace and warning guards. It independently
confirmed consumer restoration and shared source hashes. See
`parent-final-verification.json` for exact commands, result summaries and raw
paths/hashes. The targeted review follow-up found no remaining original issues.
Broader qualification and the publication/adoption dependency remain open.
