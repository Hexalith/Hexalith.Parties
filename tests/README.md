# Hexalith.Parties Test Framework

Hexalith.Parties uses a .NET 10, xUnit v3, Shouldly, NSubstitute, bUnit, Testcontainers, and Aspire-hosted integration test framework. The framework is already organized by production package boundary; keep that shape when adding coverage.

## Test Lanes

| Lane | Projects | Use For |
| --- | --- | --- |
| Tier 1 unit and component | `Hexalith.Parties.Contracts.Tests`, `Hexalith.Parties.Authentication.Tests`, `Hexalith.Parties.Client.Tests`, `Hexalith.Parties.Server.Tests`, `Hexalith.Parties.Projections.Tests`, `Hexalith.Parties.Security.Tests`, `Hexalith.Parties.AdminPortal.Tests`, `Hexalith.Parties.ConsumerPortal.Tests`, `Hexalith.Parties.UI.Tests`, `Hexalith.Parties.Picker.Tests`, `Hexalith.Parties.Mcp.Tests` | Pure domain behavior, contract shape, bUnit components, client/package boundaries, projection handler logic, and architectural fitness checks that do not require a live topology. |
| Tier 2 service integration | `Hexalith.Parties.Tests`, `Hexalith.Parties.Sample.Tests` | WebApplicationFactory flows, gateway routing, Dapr client substitutions, event subscriber behavior, and cross-component wiring that can run without a full external environment. |
| Tier 3 topology integration | `Hexalith.Parties.IntegrationTests` | Aspire/Dapr topology, sidecar health, gateway E2E behavior, and tests that depend on Docker, Dapr, or full orchestration. |
| CI publication validation | `Hexalith.Parties.Ci.Tests` | Static checks for the GitHub Actions Zot container publication contract. |

The model-only `PartiesPublishEnvironmentTests` inspect the complete publish
graph without starting Docker or Dapr. In addition to the standard root
dependencies, initialize the optional Memories reference before running the
whole class: `git submodule update --init references/Hexalith.Memories`.
Its default run-model cases also cover the topology with optional services disabled.

## Running Tests

Run the focused lane first, then broaden only when the changed surface justifies it.

```powershell
# Fastest useful feedback for most source changes
.\scripts\test.ps1 -Lane unit

# Service wiring and WebApplicationFactory coverage
.\scripts\test.ps1 -Lane integration

# Dapr/Aspire topology validation
.\scripts\test.ps1 -Lane topology

# CI publication guardrails
.\scripts\test.ps1 -Lane ci

# Full solution test run
.\scripts\test.ps1 -Lane all

# Coverage collection through coverlet collector
.\scripts\test.ps1 -Lane coverage
```

When triaging a blocked lane, add `-ContinueOnFailure` so the run reports every failing project (with a PASS/FAIL summary and exit 1) instead of stopping at the first. Add `-ResultsDirectory TestResults` for inspectable per-project TRX output, and `-Properties <k=v>,<k=v>` to forward build/restore properties such as `MinVerVersionOverride=1.0.0` or `UseHexalithProjectReferences=true`. The default behavior (no `-ContinueOnFailure`) stays fail-fast. See [`docs/development-guide.md`](../docs/development-guide.md) for the full fallback validation ladder.

```powershell
# Run the whole lane, report every failing project, emit inspectable TRX
.\scripts\test.ps1 -Lane all -ContinueOnFailure -ResultsDirectory TestResults
```

Direct `dotnet test` must target an individual test project:

```powershell
dotnet test .\tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --configuration Release
dotnet test .\tests\Hexalith.Parties.IntegrationTests\Hexalith.Parties.IntegrationTests.csproj --configuration Release
```

xUnit v3 runs under Microsoft.Testing.Platform here. For focused filters, build the target project and invoke its test executable directly with single-dash arguments such as `-class Fully.Qualified.TestClass` or `-method Fully.Qualified.TestClass.TestMethod`.

## Browser UI and visual checks

From `tests/e2e`, `npm run test:ui` runs the accessibility specimen and the admin,
consumer and picker browser flows together, retaining one complete report. This
is the blocking CI command. `npm run test:portals` runs only those three portal
specs; `npm run test:a11y` runs only the specimen.

`npm run test:visual` compares the deterministic specimen with the committed
Chromium/Linux PNG under `specs/parties-accessibility.spec.ts-snapshots/`.
Use Ubuntu 24.04 and the locked browser installation. After building the UI in
Release/package mode, run `CI=true PLAYWRIGHT_SKIP_WEBSERVER= npm run test:visual:update`
to regenerate the CI PNG, then inspect its diff. The pixel case is skipped on
Windows/macOS; its separate semantic baseline test still runs there. The separately maintained
semantic JSON under `baselines/` is checked as well and is not rewritten by this
image update command. See [the visual contract and exact build commands](../docs/accessibility.md).

## Forward skip-link source qualification (DW-91)

The normal `npm run test:a11y` gate retains the published-package skip-target
coverage. FrontComposer 4.7.0 predates the route-heading focus fix, so two
additional forward-Tab regressions are explicitly opt-in until a fixed package
is published and adopted. They never detect support from the application under
test: enabling them against an unfixed shell must fail.

Serve a Debug UI whose keyboard asset is the selected FrontComposer candidate,
then run from `tests/e2e` with its explicit URL:

```bash
BASE_URL=http://127.0.0.1:5073 PARTIES_E2E_FORWARD_SKIP_LINKS=1 PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test:a11y
```

The flag enables assertions only; it never substitutes application assets or
infers support. The default local Playwright server uses package dependencies,
so it cannot satisfy these assertions yet. The DW-91 evidence records a local
diagnostic host that substitutes only `fc-keyboard.js` through a temporary static
asset manifest, alongside an unmodified package host. This proves candidate
integration, not published-package adoption. Do not enable the flag in the
Release/package CI lane until a fixed package is adopted. DW-91 stays open for
publication/adoption and removal of the opt-in.

## Architecture

Keep tests aligned with production ownership:

- Contracts tests protect stable command, event, value-object, and serialization shapes.
- Server tests cover aggregate `Handle` and `PartyState.Apply` behavior without Dapr or Aspire.
- Projection tests cover tenant isolation, event ordering assumptions, and fail-closed read-side behavior.
- Security tests cover GDPR, erasure, key-management, and audit paths without leaking personal data.
- AdminPortal and Picker tests use bUnit and existing test doubles.
- Integration and topology tests cover Dapr, Aspire, pub/sub, access-control components, and gateway behavior.

Reusable test data and helpers belong in `src/Hexalith.Parties.Testing` when they are shared across assemblies. Prefer pure helper methods first, then framework fixtures only when setup or cleanup has real lifecycle needs.

## Fixture And Data Rules

- Use `PartyTestData` or a nearby factory/helper instead of hardcoded repeated payloads.
- Prefer unique IDs for tests that can run in parallel; avoid shared mutable state.
- Keep assertions visible in the test body. Helpers may arrange or extract data, but should not hide the behavioral claim.
- New async tests should pass `TestContext.Current.CancellationToken` into touched async APIs when practical. The root xUnit1051 suppression covers existing debt, not new habits.
- When a test depends on Dapr/Aspire, place it in the integration or topology lane and document the environmental assumption in the test or fixture.

## Quality Gates

Every new test should be deterministic, isolated, explicit, focused, and fast:

- No sleeps or arbitrary timeouts when an observable state, response, or actor/topology status can be awaited.
- No broad cleanup of generated folders or shared state as part of unrelated tests.
- No personal data, tokens, command payloads, tenant secrets, or local absolute paths in logs, snapshots, or public evidence. Public command/source receipts normalize local prefixes to documented `${VARIABLE}` placeholders while retaining exact relative paths, revisions, hashes, run IDs and arguments. Raw originals and placeholder values stay private.
- No recursive submodule initialization in test setup. Root-level submodules are enough unless nested submodules are explicitly requested.
- Use Shouldly assertions and NSubstitute when doubles are needed.

## References

- BMad knowledge fragments: `test-levels-framework`, `test-priorities-matrix`, `test-quality`, `fixture-architecture`, and `data-factories`.
- xUnit.net v3 getting started: https://xunit.net/docs/getting-started/v3/getting-started
- `dotnet test` CLI: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
- coverlet collector: https://github.com/coverlet-coverage/coverlet

## Complete local mTLS and isolation qualification

The ordinary health fixture remains unseeded. The explicit complete profile enables
Memories and the EventStore sample UI, starts the existing admin UI resource, and
seeds a synthetic tenant/member through the authenticated production callback.
It requires Linux, Docker, .NET 10, the pinned Aspire/Dapr tooling, `bash`, `curl`,
and the initialized root references including `references/Hexalith.Memories`.

Every fixture owns its control-plane names, loopback listeners (including each
sidecar's internal gRPC listener), trust/configuration directories and disposable
Redis, Redis Stack and FalkorDB containers. Configuration enters the AppHost before
model construction; the fixture does not change process-wide environment values.
Cleanup retains ownership evidence when Docker inspection or ownership checks fail.

`PartiesMtlsTopologyTests` and `EventStoreGatewayE2ETests` retain the original app
identities, namespaces and authoritative ACL bytes and run serially. Stop other
topologies using those original identities before this profile. The concurrent
`PartiesTopologyIsolationTests` profile uses suffixed literal app IDs, a private
namespace, remapped generated ACL/routing/workload identities and its own Sentry.
It verifies distinct persisted streams for the same party ID, rejects cross-session
calls and creates/queries another party after disposing the other session.

The reference UI clients accept `EventStore:AdminServer:AppId` (default
`eventstore-admin`) and `EventStore:GatewayAppId` (default `eventstore`). Targets
must contain only ASCII letters, digits, hyphens, dots or underscores; blank or
malformed values fail during SDK client registration. The retained AppHost injects
its own suffixed identities into both UI hosts. Bearer acquisition and SDK-owned
Dapr routing remain on the actual application-client paths. Dual-session coverage
prerenders both UI pages, verifies distinct persisted counter values in their
own sessions, rejects cross-session calls from both UI sidecars and repeats
the page/value checks after peer disposal. Consult the [current batch receipts](../_bmad-output/implementation-artifacts/tests/five-owner-config-and-recovery-deferred-items-2026-10-23/README.md)
for runtime results and source-build limitations.

For the Release/package consumer qualification:

```bash
dotnet build tests/Hexalith.Parties.IntegrationTests -c Release -m:1
PARTIES_MTLS_TOPOLOGY_TESTS=true dotnet \
  tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests.dll \
  -class Hexalith.Parties.IntegrationTests.Topology.PartiesMtlsTopologyTests \
  -class Hexalith.Parties.IntegrationTests.Gateway.EventStoreGatewayE2ETests \
  -class Hexalith.Parties.IntegrationTests.Topology.PartiesTopologyIsolationTests
```

The gateway case asserts an authenticated projected detail with current freshness
and independently deserializes exact Redis actor metadata/event keys derived from
`AggregateIdentity`, without scans. The ACL cases require Dapr's policy-denial
response for forbidden caller/verb/path combinations and successful permitted
controls. Application rejection alone does not establish ACL enforcement.

Without the explicit flag these runtime cases skip locally; GitHub Actions requires
them and infrastructure failures fail the tests. Skips provide no qualification.
The [batch receipts](../_bmad-output/implementation-artifacts/tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json)
separate these runtime results from source Debug consumer-build blockers. Use the
pinned module tool's generated mapping for Debug source builds; do not substitute
handwritten source selectors. These owner tests do not close DW-116 or qualify
Memories persistence, rich search or external deployment.

## Browser harness configuration checks

Run `npm --prefix tests/e2e run test:config` to execute the CI/local and
Test/Development server-ownership matrix without starting a browser. It also
uses the real Playwright runner to verify that a managed run refuses an
unrelated listener and starts and cleans up its own fixture. The required UI accessibility CI job runs these checks.
Managed runs start their own Test fixture; explicitly set
`PLAYWRIGHT_SKIP_WEBSERVER=1` and `BASE_URL` when using an externally managed host.
The UI stylesheet guard uses restore-evaluated build metadata, so changing
NuGet cache settings requires restoring and rebuilding the test project.
The CI job also runs `python3 tests/Hexalith.Parties.UI.Tests/verify-restored-assets.py`
(from the repository root) to compile and execute the real stylesheet resolver
against isolated primary and fallback NuGet caches without external feeds.

## SDK paging, Memories retry and owner publish qualification

`PartySdkQueryHandlerTests` and `EventStoreGatewayRoutingTests` exercise the
published EventStore SDK's `InvalidPage` result through actual gateway HTTP
routing. Envelope sizes outside 1–100 fail before cursor decoding, read-model
access or search. Valid-size cursor failures keep their existing category;
legacy payload paging and protected continuation controls remain covered.

`PartyMemoryIndexEntrySearchIndexerTests` keeps enabled indexing without a CaseId
retryable. Its handler redelivery cases serialize the committed canonical model,
repair configuration, and assert the latest target and stored mapping, including
an intervening erasure. These are service integration checks, not proof that a
Memories workflow has durably completed.

Run the EventStore owner's actual SDK manifest publisher and the mutation suite:

```bash
python3 scripts/verify-eventstore-publish.py
python3 -m unittest scripts/test_verify_eventstore_publish.py
```

The verifier builds the owner AppHost in Release and emits both UI grant variants
into a private temporary directory with synthetic poison credentials. It checks
external JWT audiences, workload and UI authentication, HTTPS metadata, and
resolvable secret parameter references. Mutated emitted copies must reject
literal credentials, poison values and local authentication endpoints. Raw
output is removed on success and failure; stdout contains bounded diagnostics
or hashes and counts. This qualifies actual SDK manifest emission, not a cloud
deployment or the Docker/Kubernetes/ACA target publishers.

Publisher command supervision is qualified on Linux. Each command owns a new
process session; a scoped child subreaper terminates and reaps only that command's
process group within a five-second cleanup allowance. Builds disable persistent
build servers. Windows/macOS and descendants that deliberately leave the owned
session are outside this qualification. Failure controls cover inherited pipes,
publication, malformed JSON, validation and second-emission cleanup without
retaining raw output or printing credential-bearing diagnostics.

The shared `PackageTestProcess` runner independently qualifies descendant
containment on Linux with Python 3 and procfs. A dedicated supervisor becomes a
subreaper and owns a fresh command session. On normal or early parent exit and
on timeout it terminates and reaps that command's descendants, including adopted
children that created separate sessions; inherited output streams drain under
the existing finite deadlines. No process-name lookup or runner-wide reaping is
used. Literal argument lists and the legacy string overload retain .NET runtime
argument parsing. Controls assert that an unrelated worker remains alive.
Windows and macOS retain the prior bounded runner and its exited-parent
containment limitation; this evidence does not qualify descendant reaping there.

Add `-class Hexalith.Parties.IntegrationTests.Topology.PartiesTopologyCleanupTests`
and `-class Hexalith.Parties.IntegrationTests.Topology.PartiesAspireTopologyFixtureTests`
to the explicit runtime command above for shutdown qualification. The live normal,
canceled and failed-start cases require real workers, then inspect worker exit,
owned container/network removal and released listeners under observation deadlines.
The fixture stops application hosts concurrently, then sidecars, and confirms
fresh OS identities by PID and kernel start-clock identity before Aspire removes backing
containers. One cumulative thirty-second worker-quiescence budget covers both
stop groups, exit observations and automatic fallback within each cleanup attempt.
Pending or absent startup snapshots must become stopped or stoppable before
backing dependencies drain; models that never enter SDK startup dispose normally.
Completed stops remain recorded until application disposal succeeds, including
observer failures and failed-stop siblings, so an exited `Unknown` worker cannot
block automatic or explicit cleanup retries. Exact OS exit checks still apply.
Docker inspections have a linked five-second local timeout and bounded owned
process cleanup. Controlled worker setup owns cleanup immediately after launch,
including cancellation and setup errors, with a bounded tree-termination fallback.
Controlled failure tests separately protect cleanup ordering, retained ownership,
retry and pending disposal. A missed observation deadline never abandons the
underlying disposal task. Installed Aspire/DCP bounds and exact receipts are
recorded with the batch evidence; no additional teardown deadline is inferred
from model-only checks.

## Memories ingestion and cleanup recovery

`PartyMemoryRecoveryTests`, mapping/concurrency tests and indexing/cleanup cancellation
tests assert serialized SDK state across fresh service instances. They cover reservation
failures, uncertain acknowledgement, canonical duplicate IDs, pending/failed/malformed
workflow evidence, delayed DELETE visibility, exact conditional subtraction, capacity
exhaustion and admission fencing. Cleanup health-probe success means route reachability;
it is never terminal erasure evidence.

Build `Hexalith.Parties.Tests` in Debug using source hooks generated by pinned module
tool 4.34.0 from `module/hexalith.module-manifest.v2.json`; do not handwrite selectors.
Invoke the built xUnit assembly with single-dash `-class` filters for Search and HealthChecks,
then run the full affected assembly. Record Release/package validation independently.
The HTTP doubles establish consumer recovery behavior; physical erasure qualification
requires delayed ingestion and deletion against the selected Memories implementation.
An unavailable runtime leaves that qualification and its ledger closure unclaimed.

For the owned local runtime qualification, set `PARTIES_MEMORIES_RUNTIME_CONFIG` to a
private JSON file and invoke `-class Hexalith.Parties.Tests.Search.PartyMemoryRuntimeTests`.
The file supplies `endpoint`, `daprHttpEndpoint`, `daprGrpcEndpoint`, `daprApiToken`,
`appApiToken`, `jwtIssuer`, `jwtAudience`, `jwtSigningKey`, `tenantClaimName` (default
`tenant`), `stateStoreName`, `fakeEmbeddingEnabled=true` and `ownedSourceInventoryQualificationEnabled=true`. Keep credentials outside
the repository. Set `Memories:Testing:UseFakeEmbedding=true` in the isolated selected
Memories Development configuration together with
`Memories:Testing:EnableSourceInventoryQualification=true`,
`Memories:Testing:SourceInventoryAdmissionDelayMilliseconds=5000`,
`Memories:Testing:SourceInventoryOutputDelayMilliseconds=5000` and
`Memories:Testing:SourceInventoryDeletionDelayMilliseconds=5000`. Platform does not
forward arbitrary host environment variables. These owner routes require the extra
`owned_source_inventory_test=true` JWT claim, a fresh owned tenant created after server
startup, a bounded empty-store check and fenced owner reconciliation; they are absent
in Production and cannot certify legacy inventory. The test creates a unique synthetic tenant/case, exercises
real ingestion/status/unit routes, recreates the Dapr SDK adapter, and verifies remote
absence plus persisted empty inventories, retained ETags and the admission fence.
Without the configuration path this test explicitly skips and supplies no runtime proof.

Recovery coordination regressions in `PartyMemoryCoordinationTests` share serialized
SDK state through real indexing/cleanup adapters and recreated services. They cover
changed revisions with distinct canonical IDs, definite submission rejection and
credential repair, mapping publication after completed cleanup, durable canonical
receipts without writer leases, canonical live re-add with a fresh generation, and
permanent erasure fences. `PartyMemoryCompensationTests` verifies that mapping failure
never deletes the producer and that redelivery can index the retained canonical unit.
Health coverage distinguishes status-read authorization rejection from a synthetic
missing-workflow route; synthetic probes cannot prove access to an existing workflow.
The opt-in runtime fixture performs bounded teardown and retains private cleanup
handles when teardown remains pending. Its required controlled pauses expose an admitted
producer before its first content write (including a structured unit-lookup 404), output
persistence before workflow terminal acknowledgement and asynchronous physical deletion.
A real forwarded POST then loses its acknowledgement; recreated consumer adapters recover
the admitted producer through remote inventory and refuse cleanup while it runs in either
pre-materialization or output-known phase.
Actual syntactic/vector/chunk/source-artifact/graph contents must be positive before
deletion and absent afterward, with another source surviving. An unexecuted or skipped
runtime remains an open DW-168 gate, independently of adapter success.

`PartyMemoryPersistenceRuntimeTests` is a separate opt-in SDK persistence qualification.
Set `PARTIES_MEMORIES_STATE_RUNTIME_CONFIG` to private `daprHttpEndpoint`,
`daprGrpcEndpoint`, `daprApiToken` and `stateStoreName` values for an owned sidecar.
It requires no JWT, fake embedding, tenant provisioning or Memories workflow. It seeds
legacy raw JSON, recreates SDK clients/adapters, checks same-source distinct-unit
preservation and exact subtraction, observes retained `[]` bytes with changed ETags,
physically deletes a reserved health probe, and verifies durable intent/canonical
cleanup receipts across restart. A passing run qualifies storage compatibility only;
it supplies no remote producer or physical-erasure evidence for DW-168.

Review iteration 2 adds real projection-adapter redelivery with ignored contact and
identifier collections, source dedup returning a previously cleaned canonical ID,
settled receipts with unavailable credentials/cases, receipt-bearing capacity,
legacy canonical provenance surviving subtraction failure and workflow expiry, and
configured mapping/projection store isolation. These assert durable recovery and
absence evidence; they do not establish producer content refresh or live reindexing
under permanent source dedup. The SDK persistence runtime tests additionally run
two clients against stale ETags and competing absent-key creates and inspect the
persisted winning values. Producer recreation/update support and the selected
runtime's authenticated gateway prerequisite remain upstream requirements; DW-82
and the DW-168 physical-erasure gate remain open.

Review iteration 3 exercises different contact and identifier applications with the
same event type and timestamp through the real projection handler and Memories
adapter. The retained canonical application sequence distinguishes their attempts;
same-sequence replay after service recreation performs no second submission.
Production `AddParties` registration is exercised through its resolved search
indexer, actual REST client and shared SDK state for transient removal/re-add,
durable generations, configured stores and permanent erasure fences. Persisted
preflight rejection is also cleaned before credentials are repaired without a
remote workflow or reopening the permanent fence.

`PartyMemoryRestClientRecoveryTests` uses the published REST client with controlled
HTTP responses to inspect the actual ingestion body, idempotency token and workflow
acknowledgement. It covers qualified rejection/repair with the same token, pending
acknowledgement reconciliation and ambiguous success/error bodies that cannot
permit resubmission. Status probes reject HTML, empty and unrelated successes and
require a matching valid envelope or qualified synthetic missing-status response;
these probes establish neither tenant authorization nor erasure.

The shared serialized SDK fixture atomically compares ETags and writes under one
lock, with asynchronous interception outside it. Both state-runtime tests use an
independent ten-second finally budget to conditionally erase only their generated
synthetic mapping, intent, probe and conflict keys with fresh ETags. Cleanup failure
fails an otherwise successful qualification and preserves any original test failure.
These additional checks do not establish producer content refresh, live reindexing
under permanent source dedup or delayed physical erasure in the selected runtime.

## Committed erasure and complete live search recovery

Erasure lifecycle queries use the aggregate-owned SDK detail slot's additive
`ErasureLifecycle` metadata, folded from committed events. Cleanup privacy fences
and legacy status records cannot certify terminal lifecycle state. Older rows
without lifecycle metadata remain unknown until a complete contiguous committed
prefix backfills the metadata, including when its checkpoint does not advance.
Failed or canceled append and successful retry are exercised through the real
processor, committed projection and freshly created queries in
`PartyDomainProcessorValidationTests`; projection tests also retain terminal
lifecycle through duplicate delivery, cleanup and restart.

Live search ingestion reconstructs ignored contact and identifier collections
from an independent complete contiguous replay, matched to canonical content,
sequence and available event provenance. New canonical entries retain only a
SHA-256 digest of the ignored collections in the existing SDK index slot. This
binds same-checkpoint replay without storing those personal values in recovery
manifests; canonical erasure removes the digest. Historical rows lack that
stronger digest witness and use their existing canonical content and sequence.
Partial, older and mismatching replay remains retryable. Recreated projection,
indexing and cleanup services qualify failed first submission followed by a
complete replay in `PartyMemoryCoordinationTests`, while projection tests cover
changed ignored values and concurrent updates/erasure. Shared rebuild completion
still lacks a complete ignored-content replay source; these live/retry tests do
not qualify that existing limitation or authorize retaining personal manifests.

## Selected Memories workflow retention contract

Selected Memories source `b55db82c060caf917a142fdb6585fa7340b01647` documents in
[`pipeline-persistence.md`](../references/Hexalith.Memories/docs/operations/pipeline-persistence.md#dapr-workflow-retention-caveat)
that completed Dapr workflow history remains in the actor state store until
explicit purge. That source adds no automatic purge or status-expiry feature.
This is the producer's documented persistence contract, not proof of the
retention configuration or workflow availability in any deployed runtime.

`PartyMemoryRecoveryTests` retains an older acknowledged running attempt across
serialized SDK state, a newer successful revision and recreated services. Current
revision redelivery does not poll superseded workflows. Cleanup opportunistically
resolves every retained obligation when scoped successful terminal evidence is
available. `PartyMemoryCleanupServiceTests` qualifies explicit 404
`INGESTION_STATUS_NOT_FOUND`, hypothetical 410 `INGESTION_STATUS_EXPIRED`, 503 and
transport outages across recreated cleanup services: the older obligation remains
uncertain, newer known work can settle, and cleanup succeeds only after matching
terminal evidence becomes available again. Expiry is fault injection, not a
claim that the selected producer implements expiry. No scheduling or production
reconciliation change was needed. Consumer doubles do not prove physical erasure,
authoritative remote inventory, deployed workflow retention or owner signoffs;
existing runtime gates, including DW-168, remain open.

## Publish and process safety qualification (2026-10-23)

The provenance CI class executes the actual MSBuild targets, and
`oci_validator_consumer_cases.py` executes the real publication image reader
against the current Builds validator. The shared three-argument reader API
continues to require both created labels, calendar validation and one identical
instant across platforms. The publisher additionally supplies its exact instant.
The evidence packet includes an SDK-generated dual-platform archive label receipt;
it does not establish adoption by the separately pinned release executor.

FrontComposer's compact navigation change has producer component coverage.
`PARTIES_E2E_COMPACT_SKIP_LINKS=1` enables the matching Parties component and
browser resize checks explicitly. Leave it disabled in Release/package CI until
a matching producer package is published and adopted. A forced package run must
fail against the current older shell. The pinned tool's source Debug and focused
consumer fallbacks remain subject to their recorded Platform/mapping blockers.

The optional actual Memories Production startup controls require real owned
Redis/FalkorDB instances, the built selected producer, and the selected daprd:

```bash
PARTIES_MEMORIES_PRODUCTION_TESTS=true \
PARTIES_MEMORIES_TEST_REDIS=127.0.0.1:<owned-redis-port> \
PARTIES_MEMORIES_TEST_FALKORDB=127.0.0.1:<owned-graph-port> \
PARTIES_MEMORIES_TEST_SERVER=<built-Memories.Server.dll> \
PARTIES_MEMORIES_TEST_DAPRD=<selected-daprd> \
dotnet tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests.dll -class Hexalith.Parties.IntegrationTests.Topology.PartiesPublishEnvironmentTests
```

The app startup test owns and reaps an operator-configured Dapr sidecar, supplies
its internal gRPC listener explicitly, disables unused metrics and retains the
producer startup validators. App authentication/secret-reference assertions,
operator-supplied sidecar startup and actual sidecar publication are separate
proofs. The selected Toolkit's full SDK manifest emits `memories-dapr` as `dapr.v0`
without either token binding on all three target graphs. Positive sidecar secret
emission remains an upstream qualification gate. The selected producer binder
also appends initialized algorithms, so caller RS256/ES256 entries do not prove
an asymmetric-only policy. Actual controls check authenticated Dapr outbound
health, app-id metadata, missing/wrong-token 401s and stopped-sidecar rejection.
Startup does not qualify publication, deployment, Parties bearer forwarding or search.

The live cleanup matrix is enabled by `PARTIES_MTLS_TOPOLOGY_TESTS=true`.
Direct generated mTLS launchers use `exec` and retain the runtime PID in both
profiles. Captured descendants remain owned across wrapper exit and cleanup
retry. An already-lost ordinary CLI wrapper cannot authorize backing removal;
the race before any descendant observation remains unqualified and open.
The dedicated Linux package supervisor handles SIGTERM/SIGINT, tracks its caller's
explicit PID/start identity and reaps owned descendants. Direct supervisor
SIGKILL remains outside cooperative guarantees.

See `_bmad-output/implementation-artifacts/tests/five-publish-and-process-safety-2026-10-23/`
for exact commands, matrix mappings, sanitized results and remaining gates.

## Owner configuration and ingestion-inventory health qualification

The [2026-10-23 batch receipts](../_bmad-output/implementation-artifacts/tests/five-owner-config-and-recovery-deferred-items-2026-10-23/README.md)
separate owner Debug suites, the qualified source consumer build, the retained
Release/package graph and actual runtime controls. The Parties focused lane covers
semantic wire compatibility, superseding erasure bytes, health registration,
independent inventory ACL failure/repair, cancellation and cleanup conflicts.

`ActualSdkHealthDetectsInventoryPrefixAclDenialAndRepairInBothModes` requires
`PARTIES_MEMORIES_INVENTORY_ACL_CONFIG` containing owned Dapr HTTP/gRPC endpoints,
a Dapr API token, state-store name and a loopback `inventoryAclRepairEndpoint`.
The receipt's runtime runner provisions a disposable authenticated Redis ACL user
and Dapr process, invokes the C# SDK/health test, grants the ingestion prefix,
asserts sentinel absence in actual Redis and removes its owned infrastructure.
Its remote search/cleanup responses are deterministic: this control qualifies
persistence capability, not live Memories search, tenant authorization, complete
inventory or physical erasure. An omitted runtime configuration skips and cannot
close the runtime gate.


## Recovery integration qualification (2026-10-23)

`PartiesUiHostCompositionTests.TestEnvironment_ServesSelectedFrontComposerScriptAsync`
requests `/_content/Hexalith.FrontComposer.Shell/js/fc-focus.js`, checks JavaScript content type,
and compares exact build-selected producer bytes. Evaluated metadata records source mode,
root and mapping mode. The fully mapped Debug source run now passes after FrontComposer
Contracts/Schema supply the canonical SDK assets path during outer evaluation; all validation
stays active. A passing Release/package run qualifies package delivery only.

Memories MCP's registered bearer tests exercise replacement of explicit algorithms, default
selection, invalid lists, configuration reload notifications and actual HTTP acceptance of
RS256/ES256 while rejecting HS256. Domain-option reload remains supported; changing an
already-selected bearer policy requires host restart.

EventStore's pending-batch controls test committed nonexpiring legacy compaction, recovery
interruption/restart, writable next-tail ETags, superseding bytes and refusal of unsupported
metadata. Generic recovery refuses relative-TTL v1 candidates; only the explicit caller
capability can attest nonexpiring originals. Parties' corresponding source-only integration
controls remain skipped in package mode until a matching SDK is published and adopted.

`IdentityHistoryProtectionTests` checks strict modeled snapshot restoration and preserves the
raw serialized-history guard. `PartyDomainProcessorValidationTests` exercises actual
SnapshotManager capture/read-back with serialized storage recreation and a post-restore
command, plus fresh audited certificate recovery with unavailable keys, read-back outages,
cancellation, foreign evidence and certificate-write failure. Synthetic history custody is
functional evidence; production retention and expiry qualification remain separate.

Exact current results and source prerequisites are recorded in
[the bounded batch receipt](../_bmad-output/implementation-artifacts/tests/five-recovery-integration-2026-10-23/validation.json).

Review controls additionally cover all modeled history null/required-member and enum failures,
mixed ciphertext/plaintext profiles using actual writer output, audit failure, malformed Verified
certificate recovery, post-save cancellation/restart, mixed deletes and eight mid-compaction races.
Current final results are in the batch `final-verification.json`; its `validation.json` is historical.

## Source inventory, refresh and compact navigation batch

The 2026-10-24 batch uses pinned module-tool source mappings for Debug. Run the actual
Memories `SourceInventoryIntegrationTests` and `RefreshPublicationIntegrationTests`
against disposable RedisStack/FalkorDB. They cover original deployed schemas, native
current filtering before TopK, >1,000 ordinary legacy results, both same-predecessor
revision orders, aliases, terminal evidence, restart and permanent erasure. Consumer
`PartyMemorySourceInventoryTests` exercises bounded HTTP inventory reconciliation,
partial progress and fail-closed scope/authorization/terminal controls.

Compact bUnit and browser coverage automatically executes when validated build metadata
selects FrontComposer source. The browser compares actual served focus/CSS bytes with
the selected owner files before qualification. `PARTIES_E2E_COMPACT_SKIP_LINKS=1` forces
the package negative control to execute its behavioral assertions; it never grants a
qualification skip. Matching package publication/adoption remains separate. Source
browser commands must own their server and preserve its Test specimen configuration.

The explicitly named owned focus harness is
`PARTIES_MEMORIES_OWNED_RUNTIME=1 dotnet tests/Hexalith.Parties.IntegrationTests/bin/Debug/net10.0/Hexalith.Parties.IntegrationTests.dll -method '*ActualSelectedMemoriesRunsTheRecoveryAndPhysicalErasureControl'`.
Export the five generated source-hook paths into that process; its child project builds
must use the same mapping. This scope reuses the existing owned bootstrap, backing
containers, worker observation and cleanup, but waits for EventStore, Memories and the separate Memories domain-service host with
their exact authenticated sidecars. This component graph leaves unrelated projects explicit-start, removes their app-less
sidecars, and empties only the five unused Party/sample/system domain AppIds. It retains
the actual three target apps, sidecars, state/pubsub, Redis/FalkorDB and mTLS control
plane. No unrelated domain processing or whole-topology health is qualified. The ordinary
fixture retains its full Parties/Tenants gates. Private phase and ownership receipts
retain readiness responses and both primary/cleanup failures. Target readiness or
accepted commands alone do not prove authoritative domain persistence. The driver
asserts the actual loaded Parties source capability and outgoing refresh/revision
fields. After physical erasure, the harness reads actual actor-owned events through
the authenticated SDK gateway, compares independent stored bytes and requests
canonical domain replay at the first and final sequence. The ordinary native Server
still exposes no `/process` route; this optional graph enables its separate real host.

The source workload lane also exercises the actual Memories command-store adapter,
including signed actor/source metadata across all three domains. The owned harness
requires exact receiving bound-command public-key/domain trust and an independent RSA
proof key, generated only for its disposable Development session. Missing or incorrect
Dapr API credentials and an unsigned bound command must return401 before any valid
command submission. The generic SDK retains its original constructor signatures and
adds a default-interface bound submission method plus an exact-domain proof-source seam.

The five-item review-loop regression lane adds `SourceInventoryProducerIntegrationTests` to the real adapter filters. Its prepare-save, tracking and ambiguous-scheduling controls execute the scheduler and authenticated inventory endpoint across serialized Redis recreation; failed-unit/source-payload deletion uses actual activities and real Redis/FalkorDB. `RefreshPublicationIntegrationTests` also exercises a real graph ACL write denial, both graph callers, durable repair after restart, retired neighbor metadata, direct deletion with missing reverse aliases, and the actual semantic TopK caller. The owned runtime child additionally verifies normal lost-ack projection redelivery without a second submission and real extraction-failure cleanup including Dapr source-payload absence. Mocked payload controls establish adapter behavior only; the owned child provides the separate live Dapr evidence.

Review-loop 2 extends the actual producer lane with discarded post-index dedup candidates after exhausted compensation, output-record acknowledgement loss, and annotation graph/child/Preparing boundaries across Redis recreation. Both ordinary source/token `IngestionWorkflow.RunAsync` dedup callers assert candidate and output provenance. The actual NL service TopK control places a retired closer vector ahead of current content. Public SDK reader controls reject oversized bodies with missing or lying Content-Length, bound entries/cursors, and cancel stalled bodies under both caller cancellation and the total HTTP timeout. Consumer REST tests cover canonical JSON redelivery with ignored contacts and changed context, unsupported/legacy proof negatives, and stable supersession successor submission across recreation. Final executed counts belong to the bounded batch receipt.

Final executed evidence: [review-loop 2 receipt](../_bmad-output/implementation-artifacts/tests/five-memories-inventory-and-publication-2026-10-24/review2-verification.json).


Review-loop 3 (2026-10-24): every late materialization of a publication-managed unit joins its exact bounded key set, including untracked NL and chunk maintenance writes. Source, alias, refresh/publication and claim-check authority keys have a bounded tenant ownership ledger. Tenant teardown installs a permanent content-free fence and reconciles actual claim-checked bytes before backend deletion; unresolved payload or graph write acknowledgements block success across restart. Each graph invocation has independent authority. An explicit graph-command ACL denial proves non-execution; timeouts and lost acknowledgements do not. The native registry's absent-tenant path also drains this technical ownership.

Admission uses the existing authorization bound of 128 tenant characters, 4096 source characters, and 512 case/workflow/candidate/predecessor characters, rejecting control characters before preparation or durable admission. Accepted boundary identities are read through the actual public SDK. Superseded refresh compensation records its deleted output for independent terminal proof; mutation receipt reads use one bounded multi-get. Graph publication overlap returns retryable 503 with error telemetry, and raw cleanup inventory reads retain the configured total HTTP deadline through body completion. The original public constructor signatures remain available, with production DI choosing the tenant-aware adapters.

Unknown historic coverage remains incomplete. DW-196, DW-197 and DW-198 retain the pre-existing legacy NL migration, queued NL retry erasure and sparse-keyspace deletion-discovery follow-ups. Matching package publication/adoption, deployment of the workload topology and production qualification remain separate gates. Earlier receipts are historical after these corrections; final loop3 results are recorded in the bounded batch receipt.
