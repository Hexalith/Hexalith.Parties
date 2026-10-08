# Branch B current-source continuation — 2026-10-08

The current complete Local verifier exited **1** at the EventStore Client test
build. The exact command and result are in [local-command.json](local-command.json):

```powershell
pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory /tmp/ext-parties-1-current-20261008/local -ArtifactsDirectory /tmp/ext-parties-1-current-20261008/artifacts -MemoriesRoot /tmp/ext-parties-1-current-20261008/optional-memories-package-mode
```

[Raw Client build output](local/Hexalith.EventStore.Client.Tests-build.log)
records CS1503 at `EventLocalImplementationBindingTests.cs:24` and `:26`: the
method group cannot convert to `System.Action`. The Contracts lane passed
23 cases before that failure. The failing external file was preserved.

The remaining nine owner lanes were built and executed individually with the
normal Debug/source flags and required class filters. No files were excluded,
restore overrides used, or dependencies changed by this continuation. Their
[exact commands and XML digests](remaining/manifest.json), together with the
[original Contracts lane](local/local-evidence.json), record **988 selected
passing cases across ten projects**. Builds for those ten lanes have zero
warnings/errors and XML has no failed/skipped/not-run cases. This evidence does
not establish a complete Local pass: the four required EventStore Client class
filters did not execute.

The [independent root audit](root-selected-audit.json) checks all ten XMLs,
**36 passing required classes**, and every frozen matrix row. Eight selected
production/matrix documents match run-start bytes. [Source movement](source-movement.json)
records one Parties fitness document and ten EventStore source/test documents
that changed externally during execution; observed owner HEADs stayed unchanged.
No unchanged-worktree or compiled-source Portable-PDB assertion is made. The
selected EventStore version is **3.117.1**, retained in
[the raw version output](local/eventstore-version.log).

[Live gate commands/results](live-gates.json) record exit **1** for missing
inputs, non-authoritative sentinel values supplied only to exercise the complete
capability gate, and LiveReadiness missing inputs. No endpoints were contacted.
No `EXT_PARTIES_*` environment inputs were present at the source observation.

[The current register bytes](source-observation/external-dependency-register.md)
confirm the project owner acting as Parties Maintainer and the integration date
**2026-10-08**. `TargetVersionOrCommit` and the complete compatibility command
remain `TBD`; `AcceptedStatus` remains `Uncommitted`.

[Platform registration](source-observation/PlatformCustodyServiceCollectionExtensions.cs)
and [its actor-history lifecycle document](source-observation/actor-history-lifecycle-2026-10-07.md)
confirm no production `IIdentityHistoryCustody` backend/registration. Independently
durable copy inventory, irreversible all-copy receipts, nonrollback restore,
approved actor-free successor continuation, and complete installed P-01–P-10
persisted-state/restart/restore/failure-injection targets remain unavailable.
The approved policy stays `party-actor-retention-v1`, 365 fixed days from
`binding-effective-at`; synthetic tests provide no installed qualification.

Both original open execution tasks and `in-progress` status are preserved.
This continuation changed only verification documentation/evidence. It preserved
the existing verifier/version-unification edit and unrelated owner changes; it
performed no source fix, dependency update, staging, commit, push, deployment or
production identity mutation. [Retained-file mapping](retained-file-manifest.json)
links unchanged raw receipt bytes to their original execution paths and hashes.
