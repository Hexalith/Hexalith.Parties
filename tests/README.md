# Hexalith.Parties Test Framework

Hexalith.Parties uses .NET 11, xUnit v3, Shouldly, NSubstitute, bUnit, Testcontainers, and Aspire-hosted integration test framework. The framework is already organized by production package boundary; keep that shape when adding coverage.

## Test Lanes

| Lane | Projects | Use For |
| --- | --- | --- |
| Tier 1 unit and component | `Hexalith.Parties.Contracts.Tests`, `Hexalith.Parties.Authentication.Tests`, `Hexalith.Parties.Client.Tests`, `Hexalith.Parties.Server.Tests`, `Hexalith.Parties.Projections.Tests`, `Hexalith.Parties.Security.Tests`, `Hexalith.Parties.AdminPortal.Tests`, `Hexalith.Parties.ConsumerPortal.Tests`, `Hexalith.Parties.UI.Tests`, `Hexalith.Parties.Picker.Tests`, `Hexalith.Parties.Mcp.Tests` | Pure domain behavior, contract shape, bUnit components, client/package boundaries, projection handler logic, and architectural fitness checks that do not require a live topology. |
| Tier 2 service integration | `Hexalith.Parties.Tests`, `Hexalith.Parties.Sample.Tests` | WebApplicationFactory flows, gateway routing, Dapr client substitutions, event subscriber behavior, and cross-component wiring that can run without a full external environment. |
| Tier 3 topology integration | `Hexalith.Parties.IntegrationTests` | Aspire/Dapr topology, sidecar health, gateway E2E behavior, and tests that depend on Docker, Dapr, or full orchestration. |
| CI publication validation | `Hexalith.Parties.Ci.Tests` | Static checks for the GitHub Actions Zot container publication contract. |

The runner-owned `Hexalith.Parties.Platform.IntegrationTests`,
`Hexalith.Parties.Platform.LifecycleTests` and
`Hexalith.Parties.Platform.AttachedLifecycleQualification.Tests` execute through
`hexalith-module test`. The local script inventories these projects separately
from its ordinary lanes.

The model-only `PartiesPublishEnvironmentTests` inspect the complete publish
graph without starting Docker or Dapr. In addition to the standard root
dependencies, initialize the optional Memories reference before running the
whole class: `git submodule update --init references/Hexalith.Memories`.
Its default run-model cases also cover the topology with optional services disabled.

## Package topology compatibility

Package-built topology sessions select `Parties:Testing:UsePackagedGateway=true`
before AppHost model construction. The owned gateway/sample test-host pair consumes
the centrally pinned production Gateway and DomainService packages. The sample
links the native Program, Counter/Greeting domain and command/query contract sources;
qualified catalog cohorts include the native declarations/adapters, while the
explicit older compatibility lane excludes source-only offline inventories.
Both hosts preserve native
configuration, HTTP/HTTPS launch profiles and shared AppHost wiring. The sample
copies base/Development JSON and runs the original Program from its output directory,
so normal Development and environment-provider precedence applies. Source admission
requires the complete native Program, domain/contracts inputs, JSON and launch profiles.
The separate non-packable `Hexalith.Parties.EventStoreSample.Contracts.TestArtifact`
contains the native linked sample contracts used by both the host and catalog builder.
Private Redis, mTLS custody, receiver credential denials, both sample domains,
real create/query/projection and persisted-event assertions remain required.
The profile is refused for publication; default native runs and publish models
keep the native gateway and sample projects. The catalog package lane selects
`accesscontrol.eventstore-admin.catalog.yaml` before mTLS rendering, preserving
Admin UI grants with a deny default; native/publish keep the original Admin ACL.

The supported catalog package profile also selects the nonpackable
`Hexalith.Parties.EventStoreAdminUI.TestHost`. It uses the actual SDK Admin UI
services and Razor application, preserves authentication, middleware, SignalR
and native root assets, and maps no subscription receiver. Its real HTTP
`GET /dapr/subscribe` returns404 without an HTTPS redirect; Dapr interprets that
as zero subscriptions. Native/default/publish keep the original UI host and its
`200 []` discovery. This changes no SDK receiver guard or catalog authority.

Prepare the package lane with `bash scripts/prepare-aspire-tier.sh`. Direct native
preparation requires `bash scripts/prepare-aspire-runtime-hosts.sh --gateway-profile native`
and a matching native AppHost selection. This shared `--gateway-profile` option
selects the matching gateway/sample pair; native preparation builds that pair plus six
other real runtime hosts. Package preparation also builds the Parties host and its
catalog adapter. Source-built sessions retain both native projects;
their source catalog and Platform qualification remain separate.

## Running Tests

Run the focused lane first, then broaden only when the changed surface justifies it.

```powershell
# Check that every test or support project is classified exactly once
.\scripts\test.ps1 -Lane inventory

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
consumer and picker browser flows plus both shared portal display formatter cases,
retaining one complete report. This is the blocking CI command.
`npm run test:portals` runs the admin, consumer, picker and formatter specs;
`npm run test:a11y` runs only the specimen. Run `CI=true npm run test:ui -- --retries=0`
to qualify the maintained gate without retries. Both commands include
`specs/shared-portal-display-formatters.spec.ts`; a failed formatter case blocks success.

`npm run test:visual` compares the deterministic specimen with the committed
Chromium/Linux PNG under `specs/parties-accessibility.spec.ts-snapshots/`.
Use Ubuntu 24.04 and the locked browser installation. After building the UI in
Release/package mode, run `CI=true PLAYWRIGHT_SKIP_WEBSERVER= npm run test:visual:update`
to regenerate the CI PNG, then inspect its diff. The pixel case is skipped on
Windows/macOS; its separate semantic baseline test still runs there. The separately maintained
semantic JSON under `baselines/` is checked as well and is not rewritten by this
image update command. See [the visual contract and exact build commands](../docs/accessibility.md).

## Published FrontComposer UI qualification (DW-91/100/101/102/112)

The selected Shell and Contracts.UI 4.7.5 packages publish the approved freshness,
live-region, download, typed-confirmation and shell keyboard capabilities.
`Parties:FrontComposer:UseSharedUiPrimitives=true` enables the shared components
and download service; the option still defaults to false. Both paths retain
Parties labels, status mappings, safe filenames and domain authorization guards.
The exact 4.7.5 cohort also enables shared reconciliation with registration readiness,
operation witnesses, configured retry clocks and recovery generations. Existing 4.0.4
EventStore packages enable their cancellation-aware adapter APIs. Older package cohorts
and unregistered services retain local reconciliation. Source transport drain/membership
repairs require separate publication/adoption; named owner acceptance, G4 and Story 8.9
remain separate gates. See the [2026-11-03 receipt](../_bmad-output/implementation-artifacts/tests/five-subscription-and-package-adoption-2026-11-03/README.md).

`npm run test:ui` owns its Debug server locally and runs forward Tab after cold,
hydrated and client navigation, phone/tablet/desktop resize, typed confirmation,
repeated exports and cancellation controls without capability skips or asset
substitution. Each qualification compares served focus, keyboard, download and
shell CSS bytes with the selected installed package. Build metadata supplies the
restored package version and folders, including NuGet fallback folders.

Mapped source selections use the same behavioral controls and require pinned-tool
`source` mode, an absolute selected producer root and exact served/source bytes.
Package success supplies no source qualification. The separate
`npm run test:source-feedback` lane retains its source-only instrumented transfer
control; it does not run in the package gate. The retained qualification observed
HXR015 with module tool 4.34.0 (Aspire CLI 13.6.4 versus its SDK 13.6.1 catalog).
The current module tool 4.48.0 selects SDK 17.0.0; fresh source runs must still
pass all runtime prerequisites and do not inherit historical qualification.

Source preparation on 2026-10-31 succeeds with module tool 4.39.0 after
synchronizing the committed Platform identity. The tool derives its catalog
from the parent repository's committed Platform gitlink, then that Platform
commit's Builds gitlink. Stage selected source checkouts consistently and commit
the Platform selection before retrying a stale-catalog diagnostic. Runtime
topology qualification remains separate from successful build preparation.

See the [published-adoption receipt](../_bmad-output/implementation-artifacts/tests/five-published-frontcomposer-adoption-2026-10-28/README.md)
for exact individual Debug build/test commands, package/source identities, served
asset hashes, the initial restore refusal and the separately authorized one-line
EventStore 3.128.4 alignment. Leave the runtime option false for local rollback;
shell fixes remain supplied by the selected Shell package.

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

If another local topology advertises the same original Dapr app identities, add
`PARTIES_MTLS_ISOLATED_IDENTITIES=true` to that command. The selected gateway and
ACL controls then use the existing fixture's suffixed literal app IDs and private
namespace, assert their actual sidecar metadata, and invoke those exact targets.
Deny-default policies are remapped by the existing isolation profile; permitted
200 responses and Dapr-specific PermissionDenied 403s remain required. This
qualifies the configured isolated topology. Fixed internal sidecar sockets are declared as
proxyless TCP endpoints before DCP allocates dynamic proxy listeners. An original-name collision remains
separate evidence; the default profile and production ACL bytes are unchanged.

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

`PartySdkQueryHandlerTests` checks that otherwise valid list/search envelopes with sizes outside
1–100 return `InvalidPage` before cursor decoding, read-model access or search.
`EventStoreGatewayRoutingTests` exercises actual handlers and the published SDK's
HTTP mapping with substituted actor transport: sizes 101/200 reach Parties and
return HTTP 400 with `reason=invalid-page`. Unchanged public validation intercepts
sizes below 1 or above 200 before Parties. These isolated tests do not qualify
catalog admission or deployed domain-query transport. Valid-size cursor failures
keep their existing category; legacy payload paging and protected continuation
controls remain covered.

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
used. While the direct command is active, the exclusive supervisor also reaps
already-exited adopted children so daemon PID monitors can finish naturally; it
reserves the direct command's exit status to its own poll/wait path. Literal
argument lists and the legacy string overload retain .NET runtime argument parsing. Controls assert that an unrelated worker remains alive.
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
tool 4.41.0 from `module/hexalith.module-manifest.v2.json`; do not handwrite selectors.
Invoke the built xUnit assembly with single-dash `-class` filters for Search and HealthChecks,
then run the full affected assembly. Record Release/package validation independently.
The HTTP doubles establish consumer recovery behavior; physical erasure qualification
requires delayed ingestion and deletion against the selected Memories implementation.
An unavailable runtime leaves that qualification and its ledger closure unclaimed.

The five live Memories and legacy-backfill workload classes compile in
`Hexalith.Parties.Runtime.IntegrationTests`, separate from the environment-free
`Hexalith.Parties.Tests` assembly. Their existing class namespaces are preserved.
For the owned local runtime qualification, set `PARTIES_MEMORIES_RUNTIME_CONFIG` to a
private JSON file and invoke the built runtime integration assembly with `-class Hexalith.Parties.Tests.Search.PartyMemoryRuntimeTests`.
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

The earlier compact-navigation receipt retained a package publication/adoption
gate. Shell 4.7.1 now supplies that capability: the component and browser resize
checks run unconditionally for the approved package, as documented in
[the published qualification](#published-frontcomposer-ui-qualification-dw-91100101102112).
Historical negative controls and source-mapping blockers retain their original scope.

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
emission remains an upstream qualification gate. At the time of that receipt the producer binder
appended initialized algorithms, so those caller entries did not prove
an asymmetric-only policy; current project-host qualification is recorded below. Actual controls check authenticated Dapr outbound
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
capability can attest nonexpiring originals. At that receipt, Parties' corresponding integration
controls were source-only; current published qualification is recorded below.

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

## Published SDK and Server algorithm qualification (2026-10-26)

EventStore `3.128.0` supplies the semantic query aliases and nonexpiring recovery
capability in the default package build. Run the built service assembly with
`-class Hexalith.Parties.Tests.Projections.PartyDetailSdkProjectionRecoveryTests`,
`-class Hexalith.Parties.Tests.Gateway.PartySdkQueryHandlerTests` and
`-class Hexalith.Parties.Tests.Gateway.PartySdkCrossInstanceErasureTests`.
The four legacy compaction and four newer-tombstone controls now execute without
source-mode prerequisites. Actual resolved packages and their repository revision
are retained in the [bounded receipt](../_bmad-output/implementation-artifacts/tests/five-published-sdk-and-runtime-closure-2026-10-26/README.md).

The selected AppHost resolves Memories Server as a non-packable project. Build
`references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests` in Debug, then
invoke its built assembly with
`-class Hexalith.Memories.Server.Tests.Authentication.ServerRegisteredAlgorithmPolicyTests`.
These eight registered controls assert effective named bearer algorithms, signed
RS256/ES256 acceptance, HS256 rejection, absent defaults, programmatic setup,
reload behavior and invalid startup. The receipt compares the selected actual
registration, policy and test/factory sources with released `2.29.15`. Client
package selection is separate from this Server policy. Independent sidecar token
publication, containers/deployment, historical recovery reconciliation and the
existing migration/inventory gates remain open.


## Natural-language retry safety qualification (2026-10-26)

The [five-item receipt](../_bmad-output/implementation-artifacts/tests/five-nl-retry-safety-2026-10-26/README.md)
qualifies the selected Memories source with pinned module-tool source mappings.
Run its Server.Tests registry, tenant-cleanup, retry-worker and indexing filters,
then the IntegrationTests `NaturalLanguageErasureIntegrationTests`,
`RetryWorkflowErasureIntegrationTests` and `TenantIngestionLifecycleIntegrationTests`
against owned Redis/FalkorDB and an authenticated Dapr workflow sidecar.
Per-test results cover all five approved matrix rows without targeted skips.

Tenant deletion now requires independently reconciled historical retry coverage,
even with empty queues, and refuses uncertain scheduling. Fixtures attest only
the histories they created. Preserve default-false legacy scheduler-drain gates;
this source receipt supplies no production migration or completeness attestation.
The full server suite retains five prior package/documentation evidence failures;
the broad integration suite retains its nested-reference metadata gate. Neither
gate was weakened, and no nested submodule was initialized.

Final parent verification executes 125 unit/indexing and 90 actual Redis/Dapr/lifecycle
controls, all passing with zero targeted skips. All 16 review findings are resolved;
see the batch final-verification.json and final-executed-tests.json.


## Legacy audit, NL migration and worker upgrade qualification (2026-10-27)

The [bounded recovery receipt](../_bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/README.md)
binds the selected source revisions, executed assembly hashes, commands and results.
Build the affected projects in Debug with the pinned module tool's generated source
hooks. Invoke the built Parties assemblies with `-class` for
`Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionHandlerTests` and
`Hexalith.Parties.Tests.Projections.PartyProcessingGenerationRecoveryTests`.
The new focused controls require checkpoint 50 with failed facts through 100:
shorter rebuild preparation writes nothing, complete replay repairs every fact,
and a concurrent failed fact 101 prevents stale publication in both storage modes.
The existing old-package-only compatibility case is separate from these controls.

Run Memories Server.Tests migration and retry-workflow classes and the actual
IntegrationTests `NaturalLanguageErasureIntegrationTests` and
`RetryWorkflowErasureIntegrationTests` with owned Redis/FalkorDB and an authenticated
Dapr workflow sidecar. Migration requires exact live syntactic id, tenant, case
and a nonempty unchanged source URI inside its final atomic operation, before
shared ownership enrollment. Missing, foreign or concurrently removed ownership
retains the legacy content and returns
`nl-namespace-live-owner-reconciliation-required`. Foreign or malformed legacy
and canonical hashes also refuse before ownership enrollment, retaining all bytes
for reconciliation. The synthetic fixture's explicit repair grants no authority
to reconcile production data.

The historical helper pins the original protocol at Memories
`cdd73d7361f67781a523f63f04853491a687f426` and registers it under the production
workflow name. Description and embedding remain blocked while the original worker
stops. The upgraded worker resumes to the bounded legacy restart result without
executing either content activity again. The actual modern scheduler publishes
fresh replacement content; terminal historical input is then purged and remains
absent across another worker restart. No replay incompatibility reproduced in
these two qualified boundaries, so the production retry workflow was unchanged.
This does not attest unknown deployed histories or drain older schedulers.

Validate the separately retained EXT-PARTIES-1 evidence with:

```bash
python3 _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/verify-preservation.py
```

This read-only check requires no historical temporary files and executes neither
EXT Local nor Live. It checks the 13 triaged findings, 14 reviewed source snapshots,
17 original historical receipts and the enclosing 71-file checksum manifest.
Local exit 1, both Live refusals, `accepted=false`, `available=false` and
`liveQualified=false` remain historical facts. Installed hashes, a new owner date,
owner approval, independent publication/adoption and deployment remain open.

Final parent combined-source verification at Memories
`f480fd70a7c25587264f50b5174358eee709c513` passes 107 actual runtime controls,
41 package recovery controls and 2,790 service tests with zero failures. The
service lane retains four optional skips; targeted runtime/package controls
have none. The full projection suite has 464 passes and one compatibility skip.
The same five broad Memories evidence gates remain recorded separately.


## Data lifecycle package qualification (2026-10-28)

Build `tests/Hexalith.Parties.Tests` and
`tests/Hexalith.Parties.Projections.Tests` individually in Debug. The selected
Memories 3.0.3 package now compiles the existing explicit-refresh controls; the
selected EventStore 3.128.4 package supplies processing generation support. Run
the built xUnit assemblies with single-dash `-class`, `-class-` and `-method-`
filters and fresh XML. `-failSkips` makes an unexpected targeted skip fail.
The always-compiled
`SelectedPackageSendsRefreshRevisionAndAdvertisesItsCompiledCapability` control
asserts the actual request and assembly capability independently of the selector.
Run `PartyMemoryRestClientRecoveryTests` for focused package-activation changes.

The service lane includes `PartyMemory*` search controls except the two opt-in
runtime classes, plus `PartyProcessingGenerationRecoveryTests` and
`PartySdkQueryHandlerTests`. The projection lane runs
`PartySdkProjectionHandlerTests`, excluding the explicitly obsolete
`PublishedSdkPreservesLargeLegacyLiveAndRebuildCompatibilityAsync` case, which
requires the old package without generations. The separate current-package
generation control must execute. These lanes inspect actual REST requests and
serialized synthetic store end-state; they qualify consumer behavior, not a
live Memories producer, deployment or production historical completeness.

Retain effective capability properties, restored nupkg repository metadata and
hashes, exercised implementation/test assembly hashes, commands, per-test XML
and matrix coverage. Probe older and future unqualified package selectors
without restoring or changing dependency files; they must leave refresh
disabled. Attempt mapped source separately through pinned module tool 4.41.0;
retain the initial disposition and identify any diagnostic version override.

Invoke `eng/verify-ext-parties-1.ps1 -Mode Live` only with available inputs and a
fresh evidence directory. Missing custody prerequisites must refuse before
endpoint access and establish no expiry or lifecycle continuity. Missing inputs
are only the observed first refusal: Live also always refuses with all inputs
present because the complete authenticated P-01-P-10 persisted-state/restart/
restore/failure-injection qualification lanes are not implemented in the Live
verifier or installed. Input completion alone cannot enable qualification. The
[bounded receipt](../_bmad-output/implementation-artifacts/tests/five-data-lifecycle-qualification-2026-10-28/README.md)
records commands and the outstanding DW-50/82/168/130/149 external gates. Dated
earlier receipts retain their original scope.

## Subscription safety and runtime identity qualification (2026-11-03)

Build owning projects individually and run built xUnit assemblies. The dated receipt
records exact commands, source/binary hashes and source/package distinctions. New source
EventStore clients negotiate full generation-ordered membership snapshots; old servers
retain compatible RPCs and uncertainty for canceled operations. Ordinary acknowledged
legacy leaves retire bookkeeping. External callback removal drains entered invocations;
self-removal revokes and returns, with retired invocations still owned for later external
draining. Real production-hub controls cover scoped/ordinary groups, backend mutation
acknowledgement faults, replacement, survivors and reconnect, including skipped negotiation.

`bash scripts/prepare-aspire-runtime-hosts.sh --configuration Debug --include-memory-qualification true`
prepares the existing selected package hosts plus matching wrapper and child projects.
Immediate compiler/cohort validation stays enabled; a final validation binds any legitimate
transitive rebuilds before emitting the completion manifest. The owned runtime wrapper
selects the child through build metadata, refuses differing configuration/selection/shared
producer bytes, and records actual executed file hashes. Enable its existing explicit
`PARTIES_MEMORIES_OWNED_RUNTIME=1` opt-in only for the owned isolated topology. Runtime
failures, cleanup, publication and external acceptance are recorded independently; a failed
or skipped control supplies no refresh or erasure acceptance.

The focused runtime child is `SelectedMemoriesRefreshAndCleanupPersistAcrossClientRecreation`.
It recreates clients and adapters while the selected services remain running. The original
full/default `SelectedMemoriesIngestionAndCleanupPersistAcrossRestart` fact is unchanged.

The authority receipt records revisions 1 and 2 independently in `CanonicalOutputs`.
`MemoriesAuthoritativePersistence` requires both exact IDs, compares each native gateway
stream with independent actor-state bytes, replays its first and final event prefixes, and
checks stable rereads. Deletion state follows the actual prefix: a unit's first event may
already request deletion. Native refusals retain the original gateway failure plus bounded
actor inspection, typed batch/range and later proof observations in the private evidence directory.
The caller derives current root/epoch/binding stamps from the actual SDK authority cell; missing
and stale generation controls retain503. Replay grants only the exact owned tenant and requires
a live other-tenant403 with the same otherwise-authorized operator token.

Fresh producer cleanup is qualified without per-unit semantic inventory attestation.
Memories persists fresh candidate eligibility with new admission, then seals exact-key
coverage atomically with first candidate retention, before extraction. Pre-upgrade
admissions, candidate redelivery and failed retries cannot certify unknown legacy coverage. Tracked index writes retain exact keys
without creating completeness authority; false/untracked restore writes keep their existing behavior.
The existing authenticated virgin synthetic source setup remains a separate source scope
prerequisite. Source Debug and package-host runtime receipts retain their distinct identities.

## Ordinary launcher capture qualification (2026-11-10)

`OrdinaryLauncherProcessCaptureTests` inject task enumeration, children, identity,
process-role, access and malformed-PID failures. Unreadable subtrees retain diagnostics
while remaining siblings are captured and the owner can complete cleanup. Every launcher
must have both a CLI and daprd descendant with a start identity; a role observed under
another launcher cannot fill that gap. A complete-tree positive control remains eligible
for the live timing checks.

The observer rejects an empty task set and rereads process lifetimes around role and
descendant reads. `ParentExitCanYieldEmptyTaskEnumerationWhileChildSurvivesAsync` opens
real procfs enumeration before owned parent exit and proves the empty result can omit a
surviving child. `OrdinaryLauncherProcessLifetimeTests` uses an owned user/PID namespace
to reuse one numeric PID during a role read; the XML retains both kernel start identities.
These controls require Linux procfs and user/PID namespace support. The observer only reads
processes; each reproducer owns and completes its process cleanup.

The opt-in `PartiesOrdinaryLauncherStopTimingTests` uses the same capture helper and
records `captureFailures` in its JSON before refusing incomplete evidence after cleanup.
Prepare the selected runtime cohort after all test builds with
`bash scripts/prepare-aspire-runtime-hosts.sh --gateway-profile packaged --configuration Release`,
then invoke the built test class with `PARTIES_ORDINARY_TOPOLOGY_TESTS=true` and
`PARTIES_ORDINARY_TOPOLOGY_TIMING_PATH` set to a fresh evidence path. The live gate
still requires readiness, launcher exit 143, observed worker exits and quiescence
within 30 seconds. Ordinary live tests remain opt-in locally and disabled in CI.


The maintained local `scripts/test.ps1 -Lane topology` and `-Lane all` selections
collect `OrdinaryLauncherProcessLifetimeTests` through the complete IntegrationTests
project. Focused CI selectors do not collect these namespace controls. Ordinary
execution records an explicit unsupported skip if Linux namespaces or `unshare`
are unavailable; successful namespace entry followed by a child failure always
fails. Qualification requires `PARTIES_PROCESS_NAMESPACE_REQUIRED=true` and
`-failSkips`, with nonzero discovery. Run both process capture and lifetime classes
directly. The foreign-parent control additionally requires `python3`; it resets
PID allocation only inside the verified owned namespace after pipe setup, and
retains a true independent sibling through refusal and bounded owned cleanup.

## Snapshot, historical provenance and workload restart qualification (2026-11-11)

The [bounded receipt](../_bmad-output/implementation-artifacts/tests/five-snapshot-history-and-publish-2026-11-11/README.md)
separates owner source Debug from Parties package Release. Platform's source
inventory/seal/deadline controls use real regular-file leases and verify bounded
caller return, owned worker exit, copied private output and unavailable runtime.
Memories' actual Dapr controls preserve historical chunks/NL hashes through real
workflow purge and a two-producer absent-lookup/admission interleaving. A negative
workflow lookup supplies no completeness authority. Review rejected the proposed
source-wide prerequisite because it breaks ordinary cleanup and still fails
cross-source identity reuse. Ordinary fresh cleanup remains unchanged; only an
existing legacy-NL base-key collision now refuses admission. Historical identity
safety and older-writer drain remain unqualified.

Run both backfill runtime classes documented in `docs/read-model-migration.md`
against an owned provider, with `-failSkips`. The new process class resumes the
supported aggregate one-shot/shared staged protocols through five separate
workloads per canonical-state variant and checks durable canonical values,
erasure and nine survivors. Deployed migration acceptance remains open.

The receipt's `verify-sidecar-publication.py` executes actual SDK manifests for
all three selected target graphs and tests the paired-reference checker. Current
unsupported output is explicitly refused: the app has both secret references,
while `memories-dapr` has no environment. Passing refusal controls qualify the
checker and observed publisher limitation; positive sidecar delivery remains an
upstream gate. Raw manifests and synthetic credentials stay private and are removed.


## Publisher identity, catalog fallback and per-unit authority (2026-11-15)

The publisher resolves one bounded MSBuild JSON evaluation of `TargetPath` and
`AssemblyName`, preserving the build's exact configuration/properties. Default
and custom names are exercised with real MSBuild; missing, mismatched, malformed
and symlinked outputs refuse without disclosing raw diagnostics. Run the Python
suite through `run-with-temp`, with `ArtifactsPath` beneath its private `TMPDIR`.

The standalone EventStore fallback is exact `5.1.0`. Caller and catalog selections
retain precedence; explicit `5.0.0` remains outside the catalog/host/selector
qualification set. `AspireRuntimePreparationTests` evaluates positive and negative
cohorts and fallback precedence. The independently selected 5.1.0 Release/package
runtime runs `scripts/verify-catalog-runtime.py` with a short persistent results
path so DCP Unix sockets fit the platform limit. Keep disposable artifacts below
the repository when ancestor-based source identity checks are required.

Memories source now requires exact unit authority for complete semantic erasure.
Configured service generations issue namespaced identities through HTTP, directory,
annotation and EventStore-adapter paths, with issuance retained atomically before
content writes. Unit records and generation reservations survive workflow purge
and deletion. Existing bare markers and source-B coverage cannot certify source-A
artifacts; discovery retains obligations without changing historical hashes.
Missing issuer proof leaves ingestion and tracked lost-ack recovery usable while
complete-erasure claims refuse. Historical units need explicit exact-unit inventory
and writer-exclusion evidence; see the owner's pipeline-persistence procedure.

The source lane runs `UnitOwnershipAuthorityIntegrationTests`,
`SourceInventoryProducerIntegrationTests`, `RetryWorkflowErasureIntegrationTests`
and `RefreshPublicationIntegrationTests` against actual Redis/FalkorDB and an
authenticated Dapr workflow sidecar. The real historical controls purge persisted
workflow history and complete an older producer between an actual absent lookup
and ordinary/annotation admission. Complete persisted hashes and unrelated survivors
are compared across connection recreation. Fixture evidence covers only owned
synthetic units and excluded fixture writers. Publication/adoption and a real
operator cutover remain separate external gates. Exact commands, revisions,
per-test outcomes and cleanup are retained in the dated batch receipt.


## Repeat the released-catalog deferred-work checks

From the repository root on Linux, use the SDK in `global.json`, Python 3,
and the workstation's `run-with-temp` helper. Pass a new results directory
under an existing parent.

```bash
mkdir -p artifacts/qualification-results
bash scripts/verify-released-catalog-deferrals.sh artifacts/qualification-results/released-catalog
```

The command builds three Release test projects with the default package
catalog. The core lane covers processing generation recovery and queries
(DW-50), in-memory backfill (DW-58), source inventory recovery (DW-82), and
terminal cleanup and retries (DW-168). The projections lane exercises the
current staged SDK and excludes the legacy inline SDK control. The full UI
suite covers shared freshness, live regions, optimistic reconciliation, and
SignalR membership recovery (DW-100).

The results directory retains build and test logs, xUnit XML, resolved runtime
dependencies, test assembly hashes, and a count by test class. A passing run
requires zero skipped tests and passing results in each required class.
The temporary build tree is removed after the run.

Run the owned Redis/Dapr backfill using the
[migration rehearsal runbook](../docs/read-model-migration.md).

For the Memories 4.1.0 producer run, make a disposable Parties checkout inside
a `run-with-temp` job. Use its root-declared dependencies, with
`references/Hexalith.Memories` checked out at
`0856785c7c764a0dc305af058227d6485b65c697`, the source revision in the released
Memories client and contracts packages. Keep the original checkout as-is.
Run the following from the disposable repository root. Set `results` to
the absolute path for a new directory outside the job.

```bash
mkdir -- "$results"
catalog_artifacts="$PWD/artifacts/job-output"
bash scripts/prepare-aspire-runtime-hosts.sh \
  --artifacts-path "$catalog_artifacts" --include-memory-qualification true

project=Hexalith.Parties.IntegrationTests
manifest="$catalog_artifacts/runtime-profile/Release/prepared.json"
cp -- "$manifest" "$results/prepared.json"
PARTIES_TEST_REPOSITORY_ROOT="$PWD" PARTIES_MEMORIES_OWNED_RUNTIME=1 \
  Parties__Testing__PreparedRuntimeManifest="$manifest" \
  dotnet "$catalog_artifacts/bin/$project/release/$project.dll" \
  -method '*ActualCurrentSourceMemoriesRunsAuthorityAndPhysicalErasureControl' \
  -parallelMode none -failSkips -result-xml "$results/memories-producer.xml" \
  > "$results/memories-producer.log" 2>&1
```

This wrapper also exercises remote-only inventory discovery with complete and
incomplete historical coverage. It interrupts the consumer before its first
DELETE, recreates the clients, and checks the persisted obligation and the
producer response used by the successful cleanup call.

The package-matched Memories checkout also contains the two historical admission
race cases. On its owned Redis Stack, FalkorDB and Dapr Workflow test setup,
build `tests/Hexalith.Memories.Integration.NightlyTests` in Release and run its
built assembly with `-method '*ActualOlderProducerBetweenAbsentReadAndAdmissionCannotGainModernAuthority' -failSkips`.
The ordinary and annotation cases must both pass.

The authority wrapper assigns a fresh ownership generation to the disposable
producer. Before the job exits, retain the source and package identities and
collect `runtime.xml` and the outcome fields from `ownership.json` in its
private `parties-five-memory-runtime-*` evidence directory. The
[deferred-work ledger](../_bmad-output/implementation-artifacts/deferred-work.md)
tracks production evidence and owner acceptance.


## Repeat the released backfill and Memories checks

Use Linux, Python 3.11 or newer, .NET SDK 11.0.100, Docker and the pinned
Aspire 17.0.0 and Dapr 1.19.2 toolchain. [Getting started](../docs/getting-started.md)

Use a standalone `Hexalith.Memories` checkout at `v4.1.0` and initialize its
pinned direct Builds reference with `git submodule update --init -- references/Hexalith.Builds`.
Use the pinned Builds reference in Parties as well. The runner checks that both
Memories 4.1.0 NuGet packages identify the supplied producer commit.
If the Aspire CLI bundle is unavailable, restore the pinned tools with
`bash scripts/prepare-ci-package-dependencies.sh` and export `DcpDir` and
`AspireDashboardDir` as the directories containing `dcp` and `Aspire.Dashboard`.

Prepare a private Redis 7.4.7 and Dapr backfill provider and export
`PARTIES_BACKFILL_STATE_RUNTIME_CONFIG` as described in the migration runbook.
[Migration runbook](../docs/read-model-migration.md)

The Memories fixtures start their own Redis Stack and FalkorDB containers. For
the nightly test, start the Dapr placement service and scheduler in separate
terminals. For example, with ports dedicated to this run and a new scheduler
data directory, use the following commands with the Dapr binaries on `PATH`.

```bash
placement --listen-address 127.0.0.1 --port 50005 --healthz-port 50015 --enable-metrics=false
scheduler --listen-address 127.0.0.1 --port 50006 --healthz-port 50016 --enable-metrics=false \
    --etcd-client-port 50017 --etcd-initial-cluster dapr-scheduler-server-0=http://127.0.0.1:50018 \
    --etcd-data-dir /path/to/new-scheduler-data
```

The fixture uses the ordinary Dapr installation. For a separate installation,
set `PARTIES_MEMORIES_TEST_DAPRD` to its absolute executable path. The runner
applies that path override to a scratch copy of the released test fixture.

```bash
export MEMORIES_DAPR_PLACEMENT_HOST_ADDRESS=127.0.0.1:50005
export MEMORIES_DAPR_SCHEDULER_HOST_ADDRESS=127.0.0.1:50006
python3 scripts/verify-backfill-and-memories-deferrals.py \
    /path/to/memories-v4.1.0 /path/to/new-results-directory
```

The runner builds the three test projects in Release/package mode and executes
five lanes. Expect 11 backfill cases, with 12 reports from the empty and existing
target workloads, followed by 2 same-URI refresh, 4 bounded discovery, 5
cross-source authority and 2 purged-identity reuse cases. The destination must
be new. Its logs, XML and `summary.json` retain the executed test and package
identities. Remove the provider, placement and scheduler you started after the
run. The results qualify the disposable provider and producer paths used here.

## Exact current package boundary qualification (2026-11-15)

The explicit EventStore `5.1.0`, Memories `4.0.0` and FrontComposer `6.0.0`
cohort now enables exact shared UI/reconciliation and revision-aware refresh.
Unknown, neighboring, prerelease and build-metadata selections remain refused.
The repository's retained SDK 4.2 catalog view still evaluates EventStore `4.2.0`
and Memories `3.0.27` unless the exact approved cohort is selected explicitly;
these qualification commands change no dependency or default version property.

Build individual projects in Release with `-p:HexalithEventStoreVersion=5.1.0`,
`-p:HexalithMemoriesVersion=4.0.0`, `-p:HexalithFrontComposerVersion=6.0.0` and
`-p:UseArtifactsOutput=true -p:ArtifactsPath=$TMPDIR/artifacts` through
`run-with-temp`. Run built xUnit assemblies with single-dash selectors, fresh XML
and `-failSkips`; require nonzero discovery. The policy controls evaluate
unqualified selectors without restoring them. Full UI/Admin/Consumer suites,
`PartyMemory*` consumer classes (excluding opt-in runtime classes), processing
recovery/query and current generation projection controls retain per-test results.

The transport cancellation control uses evaluated EventStore build metadata.
Exact 5.1.0 retires an uncertain legacy RPC connection and advances its recovery
generation; earlier admitted packages retain their original connection/generation
assertions. Both paths retain the late-acknowledgement cancellation boundary and
verify revoked callbacks plus a surviving unrelated subscription. The current
path observes actual server membership after asynchronous recovery before delivery.

The child and wrapper explicitly reference their already-required
`Microsoft.AspNetCore.App` framework: they execute WebApplicationFactory and
ASP.NET integration hosts. This makes restored direct framework authority match
the generated runtime configuration; the strict compiler/cohort/framework guard
is unchanged. Prepare runtime hosts after test builds, using the same exact
family properties and `--include-memory-qualification true`. Bind the resulting
`prepared.json` through `Parties__Testing__PreparedRuntimeManifest` when using
private artifact paths, then opt into the full owned runtime control.

The retained current Memories source host contains a newer per-unit authority
contract and cannot qualify the unchanged 4.0.0 implementation: the first full
mixed-host run refused source refresh. A package-matched nonpackable host must
use the exact 4.0.0 nuspec source revision in an isolated disposable root-declared
repository copy. This does not adopt the newer source contract or attest any
production inventory, older-writer drain, deployment, G4 or owner acceptance.
See the [dated receipt](../_bmad-output/implementation-artifacts/tests/five-current-package-capability-deferrals-2026-11-15/README.md)
for final runtime disposition, package/source/assembly identities, commands,
cleanup, and the five entries' remaining external gates.


## Interrupted durable recovery and current-source authority (2026-11-15)

The [bounded five-entry receipt](../_bmad-output/implementation-artifacts/tests/five-interrupted-runtime-and-browser-deferrals-2026-11-15/README.md)
keeps three independent lanes. Explicit EventStore 5.1.0, Memories 4.0.0 and
FrontComposer 6.0.0 Release builds change no default or dependency selection.
The owned backfill lane passes nine controls, including six separate workloads
for each canonical-state variant. It interrupts a 4,097-record generation before
canonical cutover, resumes the same immutable history and operation in a new
process, compares all 64 previously persisted bucket byte values, bounds each
batch and bucket, and verifies the complete history, erasure and nine survivors.
See `docs/read-model-migration.md` for its opt-in configuration and retained
workload evidence. This remains aggregate one-shot recovery plus shared staged
recovery; it is not owner-authorized aggregate staged resume or a deployed cutover.

`ActualCurrentSourceMemoriesRunsAuthorityAndPhysicalErasureControl` is a distinct
opt-in current-source profile. It binds `Ingestion:OwnershipGeneration` and
`Ingestion:OwnershipWriterExclusionEvidenceId` to its new private disposable
session, then checks the actual host environment and running backing resources.
The child independently reads exact-unit issuance/provenance, producer bindings,
generation reservations and retained deletion receipts through fresh Redis
connections. Missing and foreign unit authority refuse real refresh admission
while the current unit and local obligations remain. The full existing delayed
ingestion/deletion, failed-producer, physical-erasure, survivor and authoritative
stream/replay controls execute with that authority. Clients and adapters are
recreated; the selected services remain running. Enable
`PARTIES_MEMORIES_OWNED_RUNTIME=1` only after the strict matching runtime
preparation and bind its `prepared.json` as documented above. The earlier
package-matched profile remains separate; this newer source-host authority
qualification supplies no published-package or production writer-exclusion proof.

The exact 6.0.0 browser helper compares all four served/restored assets before
maintained Admin export, Consumer JSON export and navigation-cancellation
controls. Exact 5.0.0 admission remains available. `test:config` also verifies
unknown/neighboring/prerelease/build-metadata refusal and every byte mismatch.
Run with pinned Node 26.13.1/npm 11.20.0 and Chromium, retries disabled. The receipt
retains three real asset attachments and nonzero passing browser results.
G4, deployment, historical custody and named owner acceptance remain separate.

Pinned source preparation succeeds diagnostically, but the mapped Debug child
build reports NU1109 (source EventStore requires OpenAPI 11.0.0 while the selected
central catalog pins 10.0.15). Pinned package preparation refuses the retained
Platform release identity/tool range with HXW011/HXW010. The dated receipt keeps
these failures separate from the successful direct per-project Release fallback;
no compiler, cohort, framework, source-mapping or acceptance gate is weakened.


## Producer workload restart qualification (2026-11-15)

`ActualCurrentSourceMemoriesRestartsAcrossFiveRecoveryBoundaries` is a separate
opt-in current-source lane. Prepare the same explicit 5.1.0/4.0.0/6.0.0 runtime
cohort with `--include-memory-qualification true`, bind its `prepared.json`, and
invoke this exact method with `PARTIES_MEMORIES_OWNED_RUNTIME=1`,
`-parallelMode none -failSkips -result-xml`. The child is
`SelectedMemoriesRecoverAcrossProducerWorkloadRestarts`. The existing package
profile and client-recreation profiles retain their separate evidence.

Before this opt-in run, provide `python3` 3.9 or newer with both
[`os.pidfd_open`](https://docs.python.org/3/library/os.html#os.pidfd_open) and
[`signal.pidfd_send_signal`](https://docs.python.org/3/library/signal.html#signal.pidfd_send_signal).
The combined minimum is Linux kernel 5.3 (`pidfd_send_signal` itself requires
5.1); the host must permit both pidfd syscalls and reads of the owned workers'
`/proc/<pid>/{stat,environ,cmdline,cwd,exe}` entries. Run this harmless preflight
first: it opens a pidfd for its own Python process and uses signal 0, which
checks access without sending a signal or interrupting any workload.

```bash
python3 - <<'PYTHON'
import os, signal, sys
assert sys.version_info >= (3, 9)
fd = os.pidfd_open(os.getpid())
try:
    signal.pidfd_send_signal(fd, 0)
    for name in ("stat", "environ", "cmdline"):
        with open(f"/proc/{os.getpid()}/{name}", "rb") as stream:
            stream.read(1)
    for name in ("cwd", "exe"):
        os.readlink(f"/proc/{os.getpid()}/{name}")
finally:
    os.close(fd)
print("pidfd preflight passed")
PYTHON
```

Every known synthetic source is qualified before the first restart, because
the owned setup route requires a tenant created after the current startup.
Later phases use that persisted coverage. The existing replay-safety guard
remains enabled: active-ingestion restarts allow its documented five-minute
drain, with six-minute phases/seven-minute acknowledgements. Other phases
retain 45/60 seconds; child and wrapper retain bounded overall deadlines and
completed cleanup. Exact guard events/timing belong to the dated receipt.

The private configuration adds `producerRestartDirectory` and
`producerRestartSecret`, bound to its session custody. Atomic local files carry
phase acknowledgements; no public control route is added. Six process
replacements cover source authority, settled revision 1, admitted work with
empty mappings, output-known nonterminal work, accepted physical deletion, and
unknown historical discovery. Linux SIGSTOP interrupts the exact owned process
before fresh Redis/Dapr phase validation. Raced evidence fails qualification.
Signals use a held Linux pidfd after start-identity verification; recycled
numeric PIDs cannot redirect STOP or CONT. Cancellation and evidence failure
resume the owned incarnation before return, with real-process tests requiring
resume and completed child reaping. Pending starts settle under a separate
cleanup deadline even if phase cancellation precedes PID observation; termination
failures cannot skip coordination cancellation, settlement or quiescence.
The wrapper requires original PID/start
exit, a distinct healthy incarnation,
unchanged backing container IDs/endpoints and sidecar lifetimes, unchanged
prepared producer bytes, and retained generation/exclusion reservation.

The child independently rereads persisted SDK obligations, exact authority,
terminal receipts, actual content and derived absence. A direct call to the exact running owner assembly rejects issued same-unit
admission when only SourceUri changes. Separate unknown historical artifacts
in an isolated component tenant remain incomplete under complete source B;
the actual owner cleanup-discovery seam refuses with original/source-B hashes
retained. Historical hashes, original provenance and both settled inventories
are captured before replacement and compared afterward. This direct component proof is separate from live HTTP deletion,
and valid exact-unit authority remains usable across source scope. The historical fixture records every known
key and complete hash before withholding authority; incomplete discovery must
refuse with all bytes retained. Only that disposable fixture's exact unit/key
inventory and excluded writers permit conditional historical-v2 attestation
and bounded cleanup. Unknown production history remains incomplete.

`PartyMemoryProducerRestartProtocolTests` covers foreign/malformed requests,
replayed nonces, failed replacement, cancellation and evidence failure. Execute
it alongside `PartyMemoryUnitAuthorityRuntimeTests`, `PartyMemorySourceInventoryTests` and
`PartyMemoryOwnedCleanupTests` and `PartyMemoryProducerRestartSignalTests`.
The deterministic teardown cases in `PartyMemoryRuntimeTests` remain in the
runtime integration assembly alongside their live workloads.
The opt-in `ActualCurrentSourceCanceledRestartCompletesOwnedCleanup` cancels
coordination after observing the admitted replacement, while Aspire stays
running, and requires completed owned disposal. External runner SIGINT is qualified separately below; the earlier packet's
reconciliation diagnostics do not qualify that interruption path. The wrapper awaits child exit and coordination
on every path before owned topology disposal; failures retain private evidence.
See the [dated receipt](../_bmad-output/implementation-artifacts/tests/five-producer-restart-deferred-items-2026-11-15/README.md)
and its [reviewed final rerun](../_bmad-output/implementation-artifacts/tests/five-producer-restart-deferred-items-2026-11-15/review-final/README.md)
for 58 consumer controls, 10 signal/cleanup controls and the live restart/
cancellation results, exact identities, cleanup and separate source/package
build blockers. Publication/adoption, deployed custody, historical reconciliation,
older-writer exclusion and owner acceptance remain external gates.


## Five erasure process boundaries (2026-11-16)

`MemoryErasureProcessRestartTests` in the Memories owner is a separate lane from
its existing algorithm controls and the Parties six-phase workload lane. Its
parent retains authenticated Dapr, RedisSearch and FalkorDB while private workers
run the actual built integration-test executable. Inherited anonymous pipes use
per-incarnation HMAC credentials, monotonic sequences, bounded frames and bounded
acknowledgements. No control endpoint is exposed. The parent independently checks
executed assembly hashes, kernel process birth counters, original exit, replacement
birth, unchanged sidecar lifetime, container IDs and database run IDs. Every path
reaps owned workers; strict sidecar exit and fixture cleanup are retained.

The matrix covers unit/case legacy-NL migration and both namespaces, original
nine-field retry histories suspended in description and embedding, modern and
legacy live/dead queue obligations, actual historical chunk/NL workflow output
through purge/reuse, and an older producer completing after an actual absent read
but before ordinary/annotation admission. Fresh independent Redis connections
compare complete field hashes, scoped queue/payload absence, permanent fences and
unrelated survivors. A distinct modern retry also progresses. These test workers
use the existing actual owner activities and fake embedding responses.

Prepare source hooks with the pinned module tool and build the individual owner
project in Debug. Execute the built assembly with both
`-class Hexalith.Memories.IntegrationTests.Ingestion.MemoryErasureProcessRestartTests`
and `-class Hexalith.Memories.IntegrationTests.Ingestion.MemoryErasureProcessProtocolTests`,
plus `-parallelMode none -failSkips -result-xml <fresh-path>`. The new process lane
has no prerequisite skips. `MEMORIES_ERASURE_RECEIPT_DIR` optionally retains its
content-free custody, process and persisted-hash receipts outside `TMPDIR`.

The Parties B7 control is
`PartyMemoryOwnerDependencyTests.ActualOwnerDependencyTraceQualifiesDivergentAssetCounterexamples`.
It requires `PARTIES_MEMORY_OWNER_DEPENDENCY_SERVER` naming the explicitly prepared
current-source owner and `PARTIES_MEMORY_OWNER_DEPENDENCY_REDIS` naming owned Redis.
Use `-failSkips`; an omitted configuration is an opt-in skip and supplies no proof.
Its disposable source-owner cohort must satisfy the child's compiled shared-assembly
versions and retain the original three exact byte guards. The receipt records the
diagnostic source `Version=4.0.0` preparation separately from package selection.
Three isolated children execute real owner calls and retain all loaded paths/hashes.
Options resolves from the selected .NET shared framework; an added divergent peer is
unselected. The valid divergent private `System.IO.Hashing` candidate remains unloaded
on these calls. Neither attempt reproduces reachable mismatched dependency reuse,
so no additional guard is claimed or installed.

The [dated receipt](../_bmad-output/implementation-artifacts/tests/five-process-erasure-boundaries-2026-11-16/README.md)
keeps failed attempts, exact commands, source/package identities, XML and cleanup
separate. Existing algorithm evidence is preserved. Package publication/adoption,
production historical inventory, writer exclusion, deployment and owner acceptance
remain independent gates.

The reviewed rerun additionally requires positive historical chunk/NL ownership,
text and embedding fields, nonexpiring tombstones, replacement-worker admission
bound to the original persisted absence observation, exactly one real scheduling
attempt and explicit rejection of retained pre-erasure retry input. B7 receipts
are fresh and nonce/mode/call/birth/exit-bound; private resolver selection is an
assertion. The final parent gates pass 183 targeted checks with zero skips.


## External runner interruption qualification (2026-11-16)

`PartyMemoryRunnerInterruptionTests` owns six fresh real integration runners and
interrupts each through a verified held Linux pidfd. It uses the same prepared
current-source Memories host and explicit EventStore 5.1.0 / Memories 4.0.0 /
FrontComposer 6.0.0 consumer cohort as the successful producer-restart lane.
Source-host evidence remains separate from published-package qualification.
Prepare the runtime after the individual test builds, including
`--include-memory-qualification true`, and bind its exact `prepared.json`.

```bash
PARTIES_MEMORIES_OWNED_RUNTIME=1 \
Parties__Testing__PreparedRuntimeManifest=<prepared.json> \
dotnet <built-Hexalith.Parties.IntegrationTests.dll> \
  -class Hexalith.Parties.IntegrationTests.Topology.PartyMemoryRunnerInterruptionTests \
  -parallelMode none -failSkips -result-xml <fresh-results.xml>
```

The six checkpoints are admitted work with empty mappings, output-known
nonterminal ingestion, accepted asynchronous deletion, settled revision 1 under
an unchanged URI, cross-source authority, and incomplete bounded legacy discovery.
Earlier private checkpoints acknowledge observation only; they never attest an
unperformed replacement. At the admitted checkpoint, the original producer exits
and a distinct owned replacement is observed before healthy startup. Its replay
guard remains enabled. The replacement is frozen and the same actual persisted
phase and backing custody are independently rechecked before interruption.
The other five cases freeze their original producer at the verified checkpoint.

The supervisor issues the session/root and separate inherited runner-lineage and
worker markers before launching the runner. The fixture consumes that authority
before initialization, labels fresh SDK and control-plane containers/networks
before creation, and publishes incremental and failed-start custody while it
still owns observations. DCP/helpers inherit the runner lineage; the runtime
consumer receives its worker marker before `Process.Start`. Recovery can therefore
identify owned creation even before the first complete startup receipt. Discovery
uses exact inherited markers and ownership labels, never similar resource names.

Direct consumer and bootstrap launches enter the fixture's existing worker
registry before their callers can proceed. Teardown refreshes exact-marker orphan
custody before both SDK removal calls and final control-plane cleanup, using the
same cumulative thirty-second budget. A failed fresh check revokes earlier
quiescence, retaining the application and builder. Periodic model observation
continues to follow captured process trees without a full process-table scan.
Startup admission and cleanup closing are decided atomically. Cleanup closes
admission before awaited failure diagnostics, so SDK factory disposal cannot
start a previously unstarted model after its quiescence proof. An already
admitted startup retains the existing pending-start and ownership checks.

The fixture leaves console cancellation to xUnit, retaining Aspire observations
until its explicit worker-quiescence and disposal sequence completes. After
Docker and executed-byte audits, the supervisor freshly rereads the frozen
persisted phase, including both existing source/authority/hash baselines at the
cross-source checkpoint. Held-pidfd delivery rechecks the current producer birth,
worker marker, kernel stopped state and server-issued phase expiry immediately
before signaling the bound runner. Admitted work requires non-null distinct
replacement proof and its matching pre-backing-removal quiescence receipt.

Independent closure checks every recorded incarnation, late marker-bound worker,
container, owned network, implicit anonymous image volume and session directory.
Live backing running state and network attachments are checked separately from
immutable recovery identity, so stopped backing refuses qualification while
remaining recoverable. Named and unrelated volumes survive. An unrelated process
sentinel must survive. Stale/foreign evidence, unavailable signaling and failed
cleanup fail qualification. Exact-custody emergency reconciliation is recorded
separately and can never turn a failed case into a pass.

`PartyMemoryRunnerInterruptionControlTests` covers absent/null replacement proof,
wrong/resumed/expired producers, real Python-runner/Docker pre-receipt crash and
cancellation, an orphaned consumer, failed helper termination, inherited output
handles, stopped backing
and anonymous-volume cleanup with named-volume survival. Its Docker controls use
the same explicit owned-runtime opt-in. These component controls are separate
from actual Aspire/DCP integration in the six main cases and four live fixture
modes; they do not establish the earlier cold-start timeout cause. The audit
helper reuses the shared scoped
Linux subreaper; Docker execution, reaping and stream draining have separate
10-second, 5-second and 3-second budgets. Termination/reaping failures cannot skip
recovery, log disposal, sentinel disposal or receipts. Default unsupported-platform
signal controls skip consistently; strict Linux qualification uses `-failSkips`.

`PartiesMarkedConsumerCleanupTests` retains a real orphan, Docker backing/network
and foreign process across failed consumer termination. Its application controls
exercise the fixture's actual disposal and builder-retention path, then retry
only after the exact marked lifetime has exited. The final control-plane refresh
also has a separate focused failure control.

Use `run-with-temp` for the disposable runtime and copy useful sanitized receipts
outside its `TMPDIR` before the wrapper exits. The
[dated packet](../_bmad-output/implementation-artifacts/tests/five-runner-interruption-2026-11-16/README.md)
versions the original evidence as historical v1. Its `review-1/` indexes preserve the prior corrected source, unchanged prepared
cohort, six-case matrix and verification as historical evidence. Review-loop 2
qualification remains historical in its separately frozen `review-2/` cohort.
Original failures, preparation blockers and separate emergency reconciliations
remain retained and cannot supply current-source acceptance.
The historical review-loop 1 cohort passes six actual SIGINT cases, 80 cleanup/fixture/
signal controls and 60 consumer controls, the ordinary wrapper and actual child,
controlled cancellation and all four live disposal modes. All targeted skips are
zero. Its 322 captured main lifetimes, 36 containers, six networks and twelve
anonymous volumes are absent; all jobs were awaited and disposable scratch removed.
Failed legacy fixture diagnostics 47/49/51 did not retain original worker custody;
their authenticated SDK-worker closure remains unestablished. Separate read-only
current observations show no remaining Parties ownership labels/worker markers or
processes tied to the exact driver scratch, and supply no recovery authority.
 Interrupted business work
remains incomplete; removing disposable backing resources supplies no physical
business-erasure or historical-completeness claim. Publication/adoption, real
historical inventory, writer exclusion, deployment and owner acceptance remain open.


Review-loop 2 publishes each authenticated process birth, container, network and
anonymous-volume identity before subsequent fallible inspection. Already captured
identities survive later discovery failures and exact object disappearance. A fresh
exact absence query resolves list/inspect races; other inspection failures remain
failures. Recovery still quiesces known workers during a discovery outage, and
backing removal requires a fresh inventory after established quiescence.

The admitted replacement retains an independent live `/ready` sample. At the
stopped, birth-bound boundary, procfs additionally proves that the producer owns
no listening TCP socket. A process that begins listening after a negative readiness
sample refuses the startup attestation. All discovery and live audits pass linked
readiness cancellation through supervised commands under a thirty-second aggregate
scan budget; execution cleanup, reaping and output draining keep their independent
deadlines and are awaited.

`PartyMemoryRunnerInterruptionDiscoveryTests` exercises partial discovery followed
by failure, disappeared versus still-present objects, cancellation during a real
slow command and worker quiescence during Docker discovery failure. The real
`ChangedPersistedStateRefusesRunnerSignal` control mutates its owned Redis inventory
after checkpoint publication, requires the specific final persisted-state rejection
and a still-live runner, restores exact bytes, then completes owned disposal.

The review-2 evidence queue reuses the shared exclusive Linux subreaper for every
job, including detached descendants and original-caller disappearance. It awaits
bounded termination/reaping before later work or scratch release, and blocks both
if containment fails. The retained diagnostic helper uses its included dependency.
Packet verification requires semantic final closure with empty remaining collections
and exactly the six selected sessions; integrity hashes alone cannot attest closure.
Review-loop 2 source 39 passes six actual SIGINT cases from one isolated current-layout
Release wrapper/prepared/runtime-child cohort. All 32 process, 88 cleanup, 60 exact
consumer rows across 38 unit and 22 runtime tests, 5 startup, 7 configuration and 5 queue
controls pass with zero targeted skips. The real changed-state refusal, ordinary
six-restart wrapper/actual child, coordination cancellation and four live modes pass.
Independent closure finds 325 captured lifetimes, 36 containers, 6 networks and 12
anonymous volumes absent, with no selected zombies or main emergency recoveries.
All final jobs were awaited and their scratch removed. Both integrity-rehashed false-closure
and wrong-session packet mutations are rejected.

Configuration-only controls build a direct model without an AppHost entrypoint;
real live/SIGINT paths retain generic AppHost startup and actual lifetime checks.
The exclusive supervisor reaps exited adopted children during active commands,
preserving direct exit status. Timeout verification requires real child admission.
The preferred mapped Debug builds passed; its actual native runtime refused before
a checkpoint with observed MSB4018 build contention and later readiness cancellation.
It drifted 71 of 100,387 frozen inputs/outputs and remains unqualified. The explicit
serialized Release fallback passed. Source 27 and all other failed/intermediate
cohorts remain historical; the original SDK-custody limits and external gates remain
open. Current details and exact commands are in the review-2 packet.

The [final qualification packet](../_bmad-output/implementation-artifacts/tests/five-runner-interruption-2026-11-16/review-final/README.md)
binds current d0a01fba/source 39 and one unchanged explicit Release cohort. All
six actual SIGINT cases pass, along with 32 process, 101 cleanup, 38 unit plus 22
runtime consumer, 5 startup, 7 configuration and 5 queue controls, real state
refusal/restoration, ordinary six-replacement wrapper/actual child, controlled
cancellation and all four live disposal modes. The final regressions cover all
12 recovery disappearance/present-error combinations and real zombie rejection
until parent reaping. The signal-helper control waits for the same birth to
reach T/t before exercising the actual hung helper, timeout, reap and resume.

Independent closure finds 320 captured lifetimes, 36 containers, 6 networks and
12 anonymous volumes absent. Complete pre/post readback confirms unchanged
executed files, prepared inputs and helpers. All final jobs and their scratch
are closed. The actual native d0 pre-checkpoint writer-lease/disposal failure
and its 41/105,880 input drifts remain unqualified; prior MSB4018 and missing
SDK custody remain historical. Invocation pins explicitly retain SDK 5.1.0,
Tenants 8.0.0, Memories 4.0.0, FrontComposer 6.0.0 and McpCli 3.0.0, with the
separate current-source host. Current-default adoption and all existing external
business and owner gates remain open.
