# Five producer restart boundaries — 2026-11-15

This receipt separates selected current-source workload replacement from the
existing package-matched and client-recreation profiles. The approved spec is
`../../spec-five-producer-restart-deferred-items.md`. No production code,
dependency selection, public control endpoint or deployment was changed.

The reviewed implementation and parent rerun are recorded in
[review-final/README.md](review-final/README.md). Use that packet for the current
code: wrapper/child and coordination cancellation pass once each, consumer
controls pass 58/58 and signal/owned-cleanup controls pass 10/10, with no skips.
The files directly in this directory retain the earlier implementation and
diagnostics; their implementation hashes and counts are historical.

All exact synthetic source scopes are qualified before the first replacement.
The owned setup endpoint refuses tenants created before the current producer
startup; later phases therefore consume persisted coverage and do not rerun or
bypass that startup guard.

The private Linux wrapper freezes only its session-owned Memories process.
While that incarnation cannot progress, fresh Redis reads and the unchanged
Dapr sidecar validate the exact workflow/inventory phase and unit authority.
A raced phase fails instead of receiving acknowledgement. STOP and CONT use a
held Linux pidfd after verifying its process start identity, so PID reuse
cannot redirect a signal. A finally block resumes an interrupted incarnation
before returning after cancellation or failed evidence; real-process tests
assert resume and completed child reaping. An incomplete replacement receives
bounded pidfd termination before ordinary fixture disposal, so replay-gate
startup cannot prevent worker quiescence. The private restart JWT lifetime
is thirty minutes, covering its child deadline and independent cleanup. Resource commands
stop/start only Memories; original PID/start exit and a distinct healthy
replacement must be observed. The three backing container IDs/endpoints,
gateway/domain-service and sidecar lifetimes, prepared manifest, actual host
bytes, generation and writer-exclusion reservation must remain unchanged.

Six authenticated, nonce-bound private file acknowledgements cover five rows:

| Boundary | Required observed replacement and persisted outcome |
| --- | --- |
| DW-82 | Admitted producer has no canonical output or local mapping; scoped absence cannot settle unknown terminal work after restart. |
| DW-168 | Separate output-known and accepted-deletion replacements; terminal workflow evidence and physical absence are required, with receipts and survivors retained. |
| Same-URI refresh | Settled revision 1 precedes replacement; revision 2 changes current content at the stable URI and both exact canonical outputs are erased. |
| Cross-source authority | Issued tenant/unit/case admission varies only SourceUri through the exact owner seam and refuses after restart; an isolated unknown historical unit remains incomplete under complete source B, with independent source-A/source-B hash preservation. |
| Bounded legacy discovery | Exact known historical key/hash bytes survive unknown-coverage refusal after replacement; only the disposable fixture's conditional exact-unit inventory permits bounded cleanup and retained authority/deletion receipts. |

The cross-source component has its own tenant/case, distinct from the main
party and both API survivor scopes. It loads the actual observed Server DLL
with exact shared dependency hashes. Admission copies the persisted workflow
input and changes only SourceUri. Unknown historical cleanup-discovery refusal
uses the real owner seam; it does not claim live HTTP deletion. That isolated
unknown fixture obligation remains unresolved until verified backing-resource
disposal, without masking main-party or survivor API teardown. A legitimately
qualified exact-unit receipt is not made invalid merely by changing source.

The fixture records its exact known keys and excluded synthetic writers. Its
`historical-v2` record is produced by the existing owner ReconcileUnitAsync
seam and is conditional on unchanged original authority/provenance
and generation custody. Native TAG results supply no historical completeness.
This evidence does not attest a production inventory or writer cutover.

The existing replay-safety guard stays enabled. Active admitted/output-known
replacements can wait for its documented five-minute drain timeout before the
worker and HTTP listener start. Their server phase budget is six minutes and
the child acknowledgement budget seven minutes; other phases retain 45/60
seconds. The child has a 24-minute internal and 25-minute fact deadline; the
wrapper has a 27-minute body and 35-minute fact deadline with separate startup
and completed cleanup. Receipts bind UTC phase/healthy timestamps and elapsed
milliseconds to actual guard events 9171/9172.

The wrapper waits for child exit and settles phase coordination on every path,
then always performs topology disposal and writes an ownership receipt. The
child independently tears down both main/survivor API scopes even if evidence persistence
fails. Raw credentials, configuration, logs and private handles stay outside the
repository. Public receipts normalize local roots to `${REPOSITORY}`,
`${ARTIFACTS}`, `${RUNTIME}` and `${PRIVATE}` while retaining exact identities.

## Initial result before review fixes

The final frozen prepared cohort completed across 2026-11-15/16 (Europe/Paris):
full wrapper 1/1, authenticated child 1/1, private coordination cancellation 1/1,
focused consumer controls 51/51 and signal/owned-cleanup controls 7/7, including
five real-process signal controls. All have zero failures or targeted skips.
The full child completed in 977.258 seconds; the wrapper completed in 1022.829
seconds. Six native persisted-stream/domain-replay observations and 22 catalog
generation controls passed. Eighteen authority observations retain exact
records, source A/B bytes, bounded legacy refusal/reconciliation and terminal
receipts. Both main/survivor API teardown receipts prove terminal absence,
remote teardown safety and conditional synthetic-state removal. Owned topology
disposal completed, its session directory is absent and no primary or cleanup
failure was recorded.
`process-closure.json` independently confirms 21 owned process incarnations,
six backing containers and both runtime runners absent, and completed outer
`run-with-temp` disposal with no background jobs.

`verification.json`, `commands.json`, `executed-identities.json`, `prepared.json`
and the compressed xUnit XML bind the same final outputs across the full,
cancellation and focused selectors. The prepared manifest SHA-256 is
`c870bf54a91540bc45a1b3fcb334b1edf38b06b147447581a110215d55dc5cf4`.
`producer-restarts.json` records each old/new PID/start identity, actual original
exit and replacement health, exact backing-resource/project/three-sidecar
custody, producer byte hashes and independently frozen persisted phase.

| Completed phase | Replacement elapsed (ms) |
| --- | ---: |
| cross-source-authority | 10901 |
| same-uri-refresh | 11807 |
| admitted-empty-mappings | 309914 |
| output-known | 311182 |
| accepted-deletion | 10922 |
| bounded-legacy-discovery | 9388 |

The enabled replay guard emitted its first admitted-ingestion event 9171 at
23:37:06.723Z and timed out with event 9172 at 23:42:07.028Z; output-known
timed out at 23:47:23.542Z. `replay-guard-events.json` retains every exact
9171/9172 observation with its completed phase. These are the guard's documented
timeouts, followed by observed worker/listener startup and workflow recovery.
No guard disablement or claimed writer-drain attestation is involved.

`shorter-attempts.json` preserves initial stale-health refusal, the startup
source-qualification refusal and the shorter active-phase deadline failure.
All three completed normal owned disposal. The two global-SIGINT diagnostics
retain their original cleanup failures in `global-cancellation-limitation.json`
and separate exact-owned reconciliation; they are not passing qualification.
`setup-refusals.json` separately retains the component tenant prefix and
64-character-limit diagnostics, both with completed owned disposal. The final
private coordination cancellation has its own quiescence/cleanup proof in
`coordination-cancellation.json`; it cannot certify global runner cancellation.

`commands.json` retains exact source preparation and both blocked Debug argv
arrays with every generated mapping/environment import, plus the independent
package preparation command/results. Their NU1109/HXW011/HXW010 blockers are
separate from successful Release fallback and final prepared-host builds.
`api-teardown.json`, `authority.json`, `owner-component-baseline.json` and
`owner-shared-dependencies.json` keep the API and direct-owner proof scopes
explicit. Remaining external gates below stay open.

## Commands and build boundaries

All disposable builds/mappings use `run-with-temp`, with outputs below its
`TMPDIR`. A repository-local scratch parent keeps ancestor-based identity
checks valid; nested short runtime scratch keeps DCP sockets within their bound.
Useful evidence is copied outside scratch before either wrapper exits.

```bash
dotnet tool run hexalith-module -- prepare-build \
  --manifest module/hexalith.module-manifest.v2.json \
  --workspace ${TMPDIR}/mapping --mode source --output json
```

Source preparation succeeds diagnostically. Forward its generated environment
imports unchanged to individual Debug builds of `Hexalith.Parties.Tests` and
`Hexalith.Parties.IntegrationTests`; both refuse with NU1109: source EventStore
requires OpenAPI 11.0.0 while the selected catalog pins 10.0.15. Pinned
`--mode package` preparation independently refuses HXW011 (no retained Platform
release tag), with HXW010 (tool 5.2.2 outside the retained range). No dependency,
compiler, framework, cohort or source-mapping gate was weakened.

The individual Release fallback builds use:

```bash
dotnet build tests/${PROJECT}/${PROJECT}.csproj -c Release -m:1 \
  --disable-build-servers -p:UseArtifactsOutput=true \
  -p:ArtifactsPath=${TMPDIR}/artifacts \
  -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 \
  -p:HexalithEventStoreVersion=5.1.0 \
  -p:HexalithMemoriesVersion=4.0.0 \
  -p:HexalithFrontComposerVersion=6.0.0

HexalithEventStoreVersion=5.1.0 HexalithMemoriesVersion=4.0.0 \
HexalithFrontComposerVersion=6.0.0 \
bash scripts/prepare-aspire-runtime-hosts.sh \
  --gateway-profile packaged --configuration Release \
  --artifacts-path ${ARTIFACTS} --include-memory-qualification true

Parties__Testing__PreparedRuntimeManifest=${ARTIFACTS}/runtime-profile/Release/prepared.json \
PARTIES_MEMORIES_OWNED_RUNTIME=1 \
dotnet ${ARTIFACTS}/bin/Hexalith.Parties.IntegrationTests/release/Hexalith.Parties.IntegrationTests.dll \
  -method '*ActualCurrentSourceMemoriesRestartsAcrossFiveRecoveryBoundaries' \
  -parallelMode none -failSkips -result-xml ${PRIVATE}/wrapper.xml
```

The same three exact family properties remain in the runtime environment.
The wrapper selects the matching built child through existing strict metadata
and records executed file hashes. Its exact child method is
`SelectedMemoriesRecoverAcrossProducerWorkloadRestarts`.

The focused Parties.Tests invocation selects
`PartyMemoryProducerRestartProtocolTests`, `PartyMemoryUnitAuthorityRuntimeTests`,
`PartyMemoryRuntimeTests` and `PartyMemorySourceInventoryTests`, excludes
`*SelectedMemories*`, and uses `-parallelMode none -failSkips -result-xml`.
The IntegrationTests invocation selects `PartyMemoryOwnedCleanupTests` and
`PartyMemoryProducerRestartSignalTests` with
the same runner flags. Protocol controls cover foreign/malformed requests,
replayed nonces, failed restart, cancellation and failed evidence persistence;
authority/finalization controls require settled restoration and owned cleanup.

The initial Aspire inspection used exact AppHost `aspire start`,
`aspire describe` and `aspire stop`, each noninteractive and successful. Its
early Waiting/Starting snapshot is inspection only, with no readiness claim.

## Cancellation scope

The private coordination-cancellation control keeps Aspire running, cancels
after the admitted replacement PID is observed, and requires completed owned
disposal. Two global SIGINT diagnostics instead stopped the in-process control
plane and froze resource snapshots; the generic fixture retained ownership.
Their original failed receipts remain unchanged. Exact worker/backing absence
and separately verified removal of only stopped session-owned control-plane
containers and the empty labeled network are recorded as reconciliation.
Global runner interruption remains unqualified; private coordination success
does not certify that path.

## External gates

DW-82 and DW-168 remain open for unknown historical inventories, older-writer
exclusion, owner completeness/acceptance and production workload/key/trust
custody and deployment. Same-URI refresh still requires installed/deployed
qualification and the relevant owner acceptance. The newer current-source
per-unit authority contract requires independent publication/adoption; Memories
4.0.0 package results do not certify this source-host implementation. Bounded
legacy cleanup attests only the explicitly enumerated disposable fixture unit.
No production reconciliation, writer cutover, G4 or whole-topology acceptance
is supplied by these component tests.


Parent compile compatibility audit also builds both individual projects with the
retained defaults: EventStore 4.2.0, Memories 3.0.27 and FrontComposer 6.0.0.
Both Release builds pass with zero warnings/errors. See
[parent-default-compatibility.json](parent-default-compatibility.json); this is
compile evidence only and supplies no additional default-runtime qualification.
The [parent matrix audit](parent-matrix-audit.json) independently checks the raw
XML, process chain, exact executed hashes, API teardowns and owned disposal.
