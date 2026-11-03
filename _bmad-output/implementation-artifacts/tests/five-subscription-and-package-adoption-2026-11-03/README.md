# Subscription safety and package adoption qualification

Three source transport deferrals are resolved. DW-100 and unchanged-URI refresh
advance within their existing publication, owner and deployment gates. No package
version changed. Executed baseline revisions are in `verification.json`; owner
source and binary hashes are in `eventstore-identities.json`, and UI configuration,
package/project dependency types and executed hashes are in `ui-lanes.json`.

| Lane | Result | Receipt |
| --- | --- | --- |
| EventStore client Debug | 63 passed, zero skips | `final-client.xml` |
| Production hub/server Debug | 68 passed, zero skips | `final-server.xml` |
| Mapped-source UI Debug | 55 passed, zero skips | `final-source-ui.xml` |
| Selected-package UI Release | 55 passed, zero skips | `final-package-ui.xml` |
| Compatible older package tuple Release | 50 passed, zero skips | `final-legacy-ui.xml` |
| Runtime preparation controls | 57 passed, zero skips | `final-ci.xml` |
| Runtime child identity/refusal and cleanup | 12 passed, zero skips | `final-identities.xml` |
| Focused authenticated refresh/erasure child | 1 passed, zero skips, strengthened after review | `final-focused-runtime.xml` |
| Diagnostic primary/capture failures | 4 passed, zero skips | `final-native-diagnostics.xml` |
| Full malformed-PDF control | Failed twice: inventory HTTP 409 | `owned-runtime-second.xml`, `runtime-second-runtime.failed-producer.json` |
| Focused wrapper native-proof/replay | Still fails: native gateway HTTP 503 | `final-focused-wrapper.xml`, `final-focused-native-proof-refusal.json` |

The strengthened review2 child proves the same canonical URI receives revision 1 then 2,
different canonical outputs, changed current/search content, terminal erasure of
both outputs, settled mapping/intent state, and unchanged unrelated-case response
bytes. The original full control and its failed-PDF assertion remain intact.
The wrapper still requires native proof and authoritative replay; neither wrapper
failure is a passing qualification. Source payload deletion with a retained failed
record is incomplete cleanup. Direct actor inspection reporting Available does
not override a gateway 503. The producer cause behind the inventory 409 and the
native batch/read discrepancy remain unproved. No historical completeness,
writer drain, production cleanup, named owner acceptance or deployment is claimed.

Owned-run ownership receipts report successful topology disposal and absent session
directories; the final rerun confirms both conditions. Private configuration/credentials stay
outside the repository. Public diagnostic paths are normalized; private file
manifests retain hashes rather than private contents.

## Commands

Run projects individually. The UI commands and exact older tuple are retained in
`ui-lanes.json` and `legacy-ui-build.log.gz`. The older tuple is FrontComposer
4.7.4, EventStore 3.132.2 and Tenants 6.0.0. Review2 captures its executed binary
hashes before restoring the selected cohort. The first historical run lacked
those hashes and retains its narrower evidence scope.

```bash
dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1
dotnet tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests.dll -class '*OptimisticReconcileTests' -class '*PartiesProjectionSubscriptionTests' -class '*ProjectionFreshnessFallbackTests' -class '*ProjectionTransportPackageTests'
bash scripts/prepare-aspire-runtime-hosts.sh --gateway-profile packaged --configuration Debug --include-memory-qualification true
PARTIES_MEMORIES_OWNED_RUNTIME=1 dotnet tests/Hexalith.Parties.IntegrationTests/bin/Debug/net10.0/Hexalith.Parties.IntegrationTests.dll -method '*ActualSelectedMemoriesRunsTheRecoveryAndPhysicalErasureControl'
PARTIES_MEMORIES_OWNED_RUNTIME=1 dotnet tests/Hexalith.Parties.IntegrationTests/bin/Debug/net10.0/Hexalith.Parties.IntegrationTests.dll -method '*ActualSelectedMemoriesRefreshErasureAndReplayControl'
```

The source UI lane uses the pinned module tool's generated mapping hooks. The
owner Debug assemblies execute `EventStoreSignalRClientTests`,
`ProjectionChangedHubTests` and `ProjectionMembershipTransportTests` with xUnit
single-dash class filters. The authenticated fixture supplies private child
configuration; invoking the child alone requires that owned configuration.

## Preparation and evidence limits

The first preparation correctly refused static-asset drift after a transitive
qualification-project rebuild. Its original full log was superseded; only the
explicit failure excerpt survives. A second attempt was invalidated by editing
a running script and is retained as `runtime-preparation-interrupted.log.gz`.
The fixed script preserves immediate compiler/cohort checks and binds final
outputs only after all compilers finish. The completed 12-project Debug manifest,
per-project receipts and later diagnostic revalidation are retained separately.
`revalidate-completed-runtime.py` records the historical revalidation after
recorded builds; it is not a replacement for compilation or ordinary preparation.

Warning-override checks pass. Parties' existing CRLF files require
`git -c core.whitespace=cr-at-eol diff --check`; plain diff checking reports CRLF
as whitespace. EventStore follows its tracked LF attributes. No warning gate was
weakened. This receipt does not qualify all six canonical Epic 8 lanes.

## Review corrections and current evidence

`review1-triage.json` records every finding from all three independent layers.
The amended implementation bounds membership attempts to five seconds, owns
physical-connection termination/recovery before retiring uncertainty, and
restores authorized subscribers. Callback-initiated removals recognize any
executing registration owned by that client; external removers retain drains.
Explicit tenant rejection is pruned in legacy and snapshot rejoin paths, all
probe/fallback errors reach repair scheduling, and canceled hub admission removes
state even when semaphore acquisition never succeeds.

The strengthened live child directly reads qualified absence for every canonical
output and preserves real unit bytes and nonempty derived content in both survivor
scopes. Its `SurvivorsVerified` flag is set only after those checks. Earlier child
runs verified latest-output absence and empty-case metadata; they do not prove
these stronger assertions. The evaluated child path matches SDK artifacts/RID and
custom output-path controls in `review2-child-selection.json`.

`review2-owner-lanes.json`, `review2-lanes.json`, and
`review2-current-source-identities.json` bind current sources and executed lanes.
The live wrapper's exact source snapshot is retained separately in
`review2-runtime-source-identities.json`. A later extraction of bounded diagnostic
capture was validated by the three failure-injection controls; the live wrapper
was not rerun after that extraction. Its native-proof/replay gate remains failed.
Manual mixing of legacy mutation RPCs and snapshot ownership on one physical
connection is not qualified; the SDK selects one negotiated protocol per connection.


## Final primary verification after review patches

The final primary run executes the corrected diagnostic helper and the focused
`SelectedMemoriesRefreshAndCleanupPersistAcrossClientRecreation` fact. The child
passes all-output qualified absence, populated survivor preservation and client
recreation; selected services remain running. The wrapper continues to fail
RequireNativeProof at the native gateway with HTTP 503, while independently
persisted diagnostics survive. Full failed-PDF inventory HTTP 409 remains the
separate historical refusal. No broader native replay or producer qualification
is claimed.

`final-lanes.json` retains commands, actual dependency types and executed hashes;
`final-runtime-source-identities.json` binds the live code. The final runtime
manifest and private-file hash inventory are retained separately from sanitized
public diagnostics. `final-matrix-audit.json` verifies all five frozen matrix rows
against exact passing tests. Review-pass-2 patches are resolved; three pre-existing
issues are retained in the deferred ledger. Capture cancellation remains cooperative:
the helper awaits actual cleanup, then gives persistence its own bounded lifetime.
Membership permanent rejections stop unchanged-intent retries, transient failures
back off with jitter, and final recovery generation follows acknowledged rejoin.
Real package controls include stalled leave cancellation and actual transport
loss, new-connection rejoin and broadcast delivery.


## Source selection and main publication

EventStore task `f213e4115ec23cb736e8aa0113f61b9d4854480c` is the exact Parties source selection.
It is reachable from published owner main `342664954389359a3ad4ec979a8a503f923798ef`, which retains
all incoming owner work and passes the targeted client/server checks. Parties
consumer and source-selection commits are rebased onto the incoming CI commit.
`final-source-selection-verification.json` records 21 committed-selection
checks, both gitlink gates, ancestry and immutable-history checks. Exact commit
messages passed the pinned validator; see `commitlint-validation.json`. The final
Parties push follows this receipt. Remote CI and broader Memories acceptance
remain separate.
