# Exact current package capabilities — 2026-11-15

The explicit EventStore **5.1.0**, Memories **4.0.0**, FrontComposer **6.0.0**
cohort passed all five original local boundaries. The original broader UI/runtime
receipts remain historical after the transport-control review corrections below;
production/runtime input bytes are unchanged. No dependency/default selection
changed. All five ledger items remain open for their external gates. Parent independent
review and publication are separate follow-through.

| Lane | Passing | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Actual isolated MSBuild capability policy | 55 | 0 | 0 |
| Current UI, including actual registered shared reconciler and local rollback | 465 | 0 | 0 |
| Current Admin portal | 359 | 0 | 0 |
| Current Consumer portal | 99 | 0 | 0 |
| Current Memories consumer/search and processing recovery/query | 1,264 | 0 | 0 |
| Current processing projection/generation | 218 | 0 | 0 |
| Older default 4.2.0 transport compatibility, separate | 3 | 0 | 0 |
| Package-matched full owned runtime wrapper | 1 | 0 | 0 |
| Actual full owned runtime child | 1 | 0 | 0 |
| Final repaired current 5.1.0 transport controls | 3 | 0 | 0 |
| Final repaired default 4.2.0 transport controls | 3 | 0 | 0 |

The 2,460 policy/consumer controls include 39 optimistic reconciliation, 17
freshness, 22 live-region, 18 bounded inventory, 27 actual REST/recovery, 39
processing-generation recovery and 941 query controls. The 218 projection
controls include the current-package generation case. Only the obsolete
old-package-without-generations compatibility method is excluded from that lane.
No targeted skip or zero-discovery run supplies qualification.

## Five dispositions

- **DW-100:** exact 6.0.0 enables shared UI and reconciliation readiness,
  witnesses, retry clocks and recovery generations. Registered execution checks
  the actual producer stack; unregistered local rollback remains available.
  Named owner acceptance, deployed composition and G4/Story 8.9 remain open.
- **Unchanged-URI refresh:** exact 4.0.0 enables explicit revision/refresh REST
  requests. The full runtime produces distinct canonical outputs for revisions
  1 and 2, proves changed current content at the same source URI, then erases
  both outputs. Historical inventory, deployment and owner gates remain open.
- **DW-82:** bounded/scoped inventory reconciles empty/partial local state and
  refuses incomplete coverage, foreign authority and uncertain terminal proof.
  Runtime recovery uses actual remote inventory. Unknown historical coverage
  and older-writer exclusion require independent owner evidence.
- **DW-168:** the full runtime executes lost acknowledgements, recreated adapters,
  admitted pre-materialization and output-known cleanup refusal, failed extraction,
  delayed physical deletion, durable empty mapping/intent state and unrelated
  case/tenant survivors. Production workload/key/trust, historical reconciliation
  and deployment remain open.
- **DW-50:** 5.1.0 requalifies bounded buckets/staging, interruption/restart,
  cursor scope/revision/integrity and complete authorized processing exports.
  Historical private recovery material, deployed older-writer drain and approved
  migration/cutover/rollback remain open.

## Identities and limits

The retained SDK 4.2 catalog view actually defaults to EventStore **4.2.0** and
Memories **3.0.27**, with FrontComposer **6.0.0**. The standalone fallbacks are
5.1.0/4.0.0. Initial default-catalog successes are retained in
`initial-observations.json`; they do not qualify the requested cohort. Exact
MSBuild properties and restored identities are independent evidence.

| Published family | Version | Nupkg repository revision |
| --- | --- | --- |
| EventStore | 5.1.0 | `49084f66f8d6545cae442875355a7066d5f5e73d` |
| Memories | 4.0.0 | `c4ee997702ede948ada5f99eeae7b3782e92003b` |
| FrontComposer | 6.0.0 | `d7e8f57808b610f7cddf95ecd06db178dfd7592f` |

The runtime uses existing nonpackable owner hosts with published libraries.
Current Memories host source `7a5c892508700b2e5c71c921cdaeedf68de5f03d`
contains the newer per-unit authority contract. Its initial full run failed
refresh with `source-publication-legacy-owner-reconciliation-required`; wrapper
and child each failed once, with zero skips. It supplies no package qualification.
Its topology disposed successfully, session directory disappeared and no cleanup
failure was recorded.

The qualifying runtime instead uses an isolated host-source checkout at the exact
4.0.0 nuspec revision above. Existing compiler, cohort, package/archive,
framework, child-byte and refresh-capability guards remain active. The completed
manifest binds 13 projects, Release/net11.0, EventStore 5.1.0, Memories 4.0.0,
FrontComposer 6.0.0, Tenants 8.0.0 and McpCli 3.0.0. Source-host revisions, 528
Memories host source-file hashes and ten code/configuration overlay hashes are
retained. Those ten overlays matched the original tested implementation bytes;
`runtime-overlays.tar.gz` now preserves those exact historical files. Three
transport-test files have since changed; their final hashes, executed test/SDK
assembly hashes and restored nupkg identities accompany both focused reruns in
`review-transport-identities.json`. Broader original UI/runtime receipts are
historical; production/runtime input bytes are unchanged.
This is package-matched source-host plus package-library runtime evidence;
it does not qualify the newer source per-unit authority contract, production
inventory, writer exclusion or a deployment.

The qualifying wrapper and child each pass once. Six native persisted-stream /
first-final domain-replay observations agree with independent actor-state bytes.
Both canonical IDs are retained with their independent revision numbers; request
source-URI digests demonstrate unchanged source identity. Sidecar token and
unsigned command denials, domain-service ACL denials and catalog-generation
negative controls also execute. Owned topology disposal succeeds; its session
directory is absent and cleanup records no failure.

## Compatibility corrections and initial observations

EventStore 5.1.0's exact repository source retains uncertain canceled legacy RPCs
until physical connection termination. Its `RecoverPhysicalConnectionAsync`
advances recovery generation, then restores retained membership. The transport
control now uses evaluated selection metadata to require physical replacement
and generation increase for exact 5.1.0/source, while earlier admitted packages
retain their original same-connection/generation assertions. Both paths verify
revoked callbacks and a surviving unrelated subscriber. The current path observes
actual original server disconnection and survivor rejoin on the replacement
with bounded waits, without initiating a post-cancel membership operation. It
directly sends the canceled counter notification to that active connection,
sends one survivor group notification, then sends a direct ordered fence. After
the fence, canceled callbacks must equal zero and survivor callbacks exactly one.
The older package must retain its original connection/generation and must not
have disconnected. Boolean source metadata uses `bool.TryParse`, including
`True`/`TRUE`. Only this three-test class was rerun for current and default
packages after the correction; both pass with zero skips.
The first no-recovery expectation failed, and an early survivor-send attempt
raced asynchronous rejoin; both failed observations remain recorded.

The child and wrapper already execute ASP.NET WebApplicationFactory/integration
hosts. Explicit `Microsoft.AspNetCore.App` framework references make their
restored direct framework authority match their generated runtime configuration.
The initial strict mismatch refusal is preserved; the validator is unchanged.
The subsequent ordinary and package-matched preparations both pass.

A mixed class/method selector initially discovered zero policy tests; the final
method selectors discover and pass all 55 cases. That initial exit-zero attempt
is explicitly nonqualifying. Unknown versions are evaluated without restore.

## Exact commands

All builds are individual project builds. Ad hoc build/test work runs under
`run-with-temp`; `${SCRATCH}` is that job's private `${TMPDIR}` beneath the
repository so ancestor-based root detection works. `${PRIVATE}` is retained
private evidence outside it. No temporary copy, package feed or build output
escapes the owned job. Separate short runtime jobs avoid DCP Unix-socket limits.
No remote operation or nested-submodule initialization occurs.

The build command for UI.Tests, AdminPortal.Tests, ConsumerPortal.Tests,
Parties.Tests, Projections.Tests and IntegrationTests is:

```bash
dotnet build tests/${PROJECT}/${PROJECT}.csproj -c Release -m:1 \
  --disable-build-servers -p:UseArtifactsOutput=true \
  -p:ArtifactsPath=${SCRATCH}/artifacts-current \
  -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 \
  -p:HexalithEventStoreVersion=5.1.0 \
  -p:HexalithMemoriesVersion=4.0.0 \
  -p:HexalithFrontComposerVersion=6.0.0
```

Project names are fully prefixed `Hexalith.Parties.`. Final UI verification uses
`${SCRATCH}/artifacts-ui-final` to preserve prepared runtime hashes. Ci.Tests uses
the same build options with `${SCRATCH}/artifacts` and no family overrides; its
policy tests themselves evaluate every exact/unknown selector without restoration.
Every completed build reports zero warnings and errors.

Built assembly invocations use `-noLogo -parallelMode none -failSkips
-result-xml ${PRIVATE}/${LANE}.xml`, with these selections:

| Assembly | Additional selectors |
| --- | --- |
| Ci.Tests | `-method '*ExactSelectionControlsSymbolAndAssemblyMetadata' -method '*FrontComposerExactSelectionPreservesUiAndReadinessBoundaries'` |
| UI.Tests / AdminPortal.Tests / ConsumerPortal.Tests | Whole assembly |
| UI.Tests, separate transport controls | `-class Hexalith.Parties.UI.Tests.ProjectionTransportPackageTests` |
| Parties.Tests | `-class 'Hexalith.Parties.Tests.Search.PartyMemory*' -class- '*PartyMemoryRuntimeTests' -class- '*PartyMemoryPersistenceRuntimeTests' -class- '*PartyMemoryInventoryAclRuntimeTests' -class Hexalith.Parties.Tests.Projections.PartyProcessingGenerationRecoveryTests -class Hexalith.Parties.Tests.Gateway.PartySdkQueryHandlerTests` |
| Projections.Tests | `-class Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionHandlerTests -method- '*PublishedSdkPreservesLargeLegacyLiveAndRebuildCompatibilityAsync'` |

Final Parties.Tests is re-executed after the framework-reference correction
against the prepared primary child. The older-default transport check rebuilds
UI.Tests without family overrides in a separate disposable root.

Runtime preparation is identical in the primary tree and isolated source-host
snapshot, with separate private artifact roots:

```bash
HexalithEventStoreVersion=5.1.0 HexalithMemoriesVersion=4.0.0 \
HexalithFrontComposerVersion=6.0.0 \
bash scripts/prepare-aspire-runtime-hosts.sh \
  --gateway-profile packaged --configuration Release \
  --artifacts-path ${RUNTIME_ARTIFACTS} --include-memory-qualification true
```

The snapshot is a local shared clone of baseline
`f45d0f6a6ecaf75eec8dc16b8df83840e880a8c5`, with the byte-identical tracked
code/configuration overlays listed in `overlay-identities.json`. Each of the ten
root-declared repositories is locally cloned without nested initialization;
only the disposable Memories host checkout selects the exact 4.0.0 revision.
The primary worktree's branches, gitlinks and dependencies remain unchanged.

### Executable historical snapshot setup

The retained setup script pins baseline
`f45d0f6a6ecaf75eec8dc16b8df83840e880a8c5`, locally clones all ten root-declared
repositories, checks out their immutable `snapshot-identities.json` commits
(including Memories `c4ee997702ede948ada5f99eeae7b3782e92003b`), and restores
the ten archived historical overlays only after checking their original hashes.
It uses the same sparse paths and local shared-clone layout as the tested run.
No remote fetch/clone, submodule update or nested initialization is invoked; all
required commits must already exist in the supplied local repositories. The
source worktree and its branches are unchanged.

```bash
qualification_source=$(git rev-parse --show-toplevel)
qualification_receipt="$qualification_source/_bmad-output/implementation-artifacts/tests/five-current-package-capability-deferrals-2026-11-15"
qualification_results="$HOME/.local/state/parties-runtime-reproduction"
mkdir -p "$qualification_source/artifacts/reproduction-jobs" "$qualification_results"
TMPDIR="$qualification_source/artifacts/reproduction-jobs" run-with-temp \
  python3 "$qualification_receipt/reproduce-snapshot.py" \
    --source "$qualification_source" --results "$qualification_results" --prepare
```

`--prepare` runs the strict original preparation command above; omit it for
snapshot setup alone. The snapshot and all build outputs live below the wrapper's
`TMPDIR`. New preparation logs and its new per-project JSON receipts are retained
under the explicit results directory. They are new-run evidence, never substitutes
for unavailable original receipts. This correction checked script syntax and
archive hashes; it did not rerun broad preparation or runtime tests.

### Original per-project receipt custody

The original completed manifests reference 13 per-project receipt paths/hashes
inside their deleted disposable output trees. Those referenced files were not
copied into the retained packet, and neither private retained evidence nor the
parent holds their originals. Their original custody is **unavailable**. The
original manifests/hashes, actual runtime XML, executed identities, source hashes
and persisted/replay proof remain retained as independent evidence. No replacement
receipt was fabricated, rebuilt or assigned an original hash. See
`original-receipt-custody.json` for the exact unavailable snapshot references.

The actual full wrapper invocation in each owned short runtime job is:

```bash
HexalithEventStoreVersion=5.1.0 HexalithMemoriesVersion=4.0.0 \
HexalithFrontComposerVersion=6.0.0 \
Parties__Testing__PreparedRuntimeManifest=${RUNTIME_ARTIFACTS}/runtime-profile/Release/prepared.json \
PARTIES_MEMORIES_OWNED_RUNTIME=1 \
dotnet ${RUNTIME_ARTIFACTS}/bin/Hexalith.Parties.IntegrationTests/release/Hexalith.Parties.IntegrationTests.dll \
  -method '*ActualSelectedMemoriesRunsTheRecoveryAndPhysicalErasureControl' \
  -failSkips -parallelMode none -noLogo \
  -result-xml ${PRIVATE}/wrapper.xml
```

The wrapper selects `*SelectedMemoriesIngestionAndCleanupPersistAcrossRestart`
for its child and writes fresh child XML. Both XML files have exactly one passing
test and zero skips in the qualifying run. Private runtime evidence is copied
outside its short job before that job exits; credential-bearing configuration
and provisioning handles remain private. The outer job waits for all child work
and removes all disposable repository copies/build outputs when it exits.

The required baseline used isolated `aspire start --no-build --non-interactive`,
`aspire describe` and `aspire stop`; all exited zero. The early Waiting/Starting
snapshot establishes inspection only. Readiness is proved by the separate owned
runtime, not inferred from that baseline. Final static gates are
`bash scripts/check-no-warning-override.sh` and `git diff --check`.

## Receipt files

- `verification.json`, `executed-tests.json.gz` and fresh lane XML retain exact
  test names/results/timings. XML console output is omitted; raw XML SHA-256s
  identify the privately retained originals.
- `packages.json`, `consumer-identities.json`, effective-property JSON,
  prepared manifests, source/snapshot/overlay identity JSON and `runtime.json`
  bind restored packages, executed binaries, host sources and cleanup.
- Compressed build/preparation logs retain exact successful build evidence and
  the initial framework-authority refusal. `initial-observations.json` preserves
  nonqualifying default/selector/compatibility attempts.
- Absolute local prefixes are replaced with documented `${VARIABLE}` placeholders.
  No runtime credentials or private configuration are published. `SHA256SUMS`
  binds this retained packet; run `sha256sum -c SHA256SUMS` in this directory.

## Parent final verification and review

Three independent review layers completed. Ten concrete corrections to transport
observations and evidence were addressed; the browser asset qualifier's existing
5.0.0-only restriction remains a named deferred G4/browser verification blocker.
No browser qualification is claimed. Final current UI rerun passes 465/465 with
zero skips after the review patch; focused current/default transport reruns each
pass 3/3. The remaining 1,995 policy/portal/search/history/projection results and
full owned runtime wrapper/child passes remain valid: review changed only three
UI test/fixture files, with production and owned-runtime inputs unchanged.

`parent-verification.json` and `parent-ui.xml.gz` retain the final independent
build/run; `final-source-identities.json` distinguishes final reviewed source
from the preserved pre-review runtime overlays. `matrix-audit.json` binds all
five rows to passing executed tests. Historical snapshot reproduction setup was
executed successfully with local objects and verified overlays, without builds,
remote operations or nested initialization. All root-declared submodules are
clean with no unpublished commits; no gitlinks or dependencies changed.

The exact commit candidate passed pinned commitlint21.2.4 with zero problems or
warnings; `commitlint.log.gz` retains evidence. Root main matched origin/main at
publication preparation. Requested commit/push is the terminal publication
operation after this sealed receipt; external deployment/history/owner gates
remain open.
