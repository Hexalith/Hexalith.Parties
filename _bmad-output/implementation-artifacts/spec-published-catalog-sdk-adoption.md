---
title: Adopt the published catalog SDK and qualified owner artifacts
status: in-review
created: 2026-10-31
baseline_commit: 6febe55d1c711a305a5969f04b8b9fb1fa80722f
---

## Intent

The operator requests the latest stable packages and a fully verified release. EventStore3.129.0 became public during final qualification. It publishes the previously source-only declaration, canonical intent and runtime catalog APIs, but now requires actual catalog activation for domain/gateway readiness and admission. A version-only upgrade compiles while returning503 at runtime. Current public Tenants5.11.2 and Memories3.0.11 contain no compiled declaration source, so they cannot supply the new executable catalog.

## Constraints

- Preserve domain behavior, actual authentication, Dapr mTLS, declared ownership, persisted-state assertions, source-development host selection, default/publish profile decisions and all automated CI/release gates.
- API availability must be independent of source selection. Exact qualified3.129.0 package support may enable declaration/adapter branches; it must not select native source hosts or claim source qualification.
- Reuse existing SDK authority, canonical serialization and bootstrap, plus the existing Platform catalog-builder tool where appropriate. Do not introduce a permissive catalog authority or duplicate technical-module trust policy.
- Each participating actual host independently observes and activates its own bound generation through prepare/ready/commit/readback. Bind private run directory, instance/attempt, exact host set, loaded owner/adapter assemblies, component/ACL bytes and actual invocation coordinates. Preserve host/channel/workload negative controls.
- Newly published owner artifacts must carry their actual declaration source and canonical adapters, with exact SDK/source/version proof. Do not claim that rebuilding source qualifies an older public PE.
- Preserve the explicit older package compatibility lane where it is intentionally frozen. Its complete SDK cohort must be selected consistently and its results must not qualify3.129.0. Normal Release/publication must use the new qualified cohort.
- Do not initialize nested submodules, change production trust policy, weaken validation or rewrite historical receipts. Operator authorization covers dependency upgrades, owning commits/pushes, publication and configured reviewer approvals without further human pauses.

## Work and acceptance

1. Qualify Tenants' existing declarations and adapters in its existing public artifacts with published3.129.0. Qualify Memories' new public adapter artifact and correct stale tests that inspect only an older central catalog rather than effective inputs. Require owner CI/release success and independently downloaded package PE proof.
2. Adopt new owner versions and EventStore3.129.0 in Parties. Separate exact package API availability from source mode. Retain canonical hashing and complete declaration inventory. Preserve real legacy API refusal only in its explicit old-SDK lane.
3. Make the existing Parties adapter deliverable available to the published catalog composition. If its existing separate project is required, enroll it consistently in pack, validation, public inventory, consumer-boundary checks and publication proof; actual packed metadata/PE must pass.
4. Extend the existing explicit test package profile with actual run-private catalog bootstrap using existing shared tool/runtime seams. Include the sample's actual declaration inventories when the selected SDK supports them. Keep native/source/publish behavior and all current real domain bodies.
5. Run focused artifact/schema/canonical/config/identity-negative checks, standalone Release with zero warnings/errors, affected frontend/HTTP tests and the actual authenticated concurrent topology with persisted Counter/Greeting/Parties results and cleanup.
6. Complete three independent review layers and triage all findings before committing the adoption. Parent then requires exact main push CI+RC success, authorized production environment approvals, successful Release and independent retrieval/identity/source verification of every public manifest package.

## Evidence so far

- Existing3.128.4 compatibility baseline6febe55d: root standalone0warnings/errors; Ci.Tests502/502; frontend458+327+85+171; Memories recovery41/41; browser68/68; service2813total0failed4existing skips; expanded concurrent live1/1. Hosted build/unit/integration/accessibility/declaration checks pass, Aspire lane still running at this observation.
- Public EventStore3.129.0 archive embeds sourcec02afa0fce310ccdb696232f18829df96cba088e. Isolated Parties compilation succeeds, and real declaration readers build a schema3candidate from41routes+41operations. This is compilation/schema proof, not an activated runtime.
- Old public Tenants5.11.2/Memories3.0.11 have zero declaration-source attributes; actual SDK reader refuses enrollment. New owner release prerequisites are being implemented, not assumed complete.
- Owner EventStore's separate live-sidecar workflow exposes six stale fixture failures around catalog registration/activation. Those fixtures must retain real admission validation; current owner production runtime behavior is not weakened to accommodate them.

## Verification

Use actual package archives and their embedded metadata/PEs, direct individual xUnit runners, the existing public catalog-builder/runtime APIs and the existing actual topology tests. Retain exact commands, source hashes, results and failures here when the implementation is concrete. Final public package count follows the authoritative release inventory.


### Public adapter packaging implementation

Parent now enrolls the existing `Hexalith.Parties.EventStoreExtensions` project as the eleventh public package. The project retains its existing factory/ownership/hash implementation; metadata now names its package and directly references the SDK Contracts assembly it consumes, preserving the common package dependency-boundary rule. Authoritative inventory, pinned preparation/publication count, strict package validator, trusted-publishing package list and count-drift controls move together to eleven. The recorded remote policy retains its existing wildcard; this edit does not register or modify any remote policy.

The isolated package client now references the actual adapter archive and executes after restore/build. It uses the published SDK reader on the restored Parties Contracts PE, requiring41routes/41operation schemas,28actual canonical adapters and the adapter-source assembly attribute. This detects a compiling but unenrolled public package. All SDK dependency family versions remain checked before build/execution. Final actual-archive execution requires the qualified3.129cohort and remains pending; syntax/inventory/whitespace checks already pass.


## Implementation checkpoint — delegated investigator

Memory fixture follow-up is committed and pushed as ef5c134b, preserving the
independent upstream 7352c6a2 fix. The actual selected shared catalog remains
3.128.4; the genuine owner floor/evaluated Client/restore cohort is 3.129.0.
The final focused 15 tests pass with no skips and zero-warning Release compilation.
Exact commitlint and all installed story/tenant/readiness hooks pass.

Root code, producer outputs and Platform tool sources have not been changed by
this investigator yet. The durable next-step/ownership/session checkpoint is
`tests/eventstore-3.129.0-adoption-investigation/claims.json` relative to this
implementation-artifacts directory. No active tool process needs resuming.
The prepared disposable current-root copy is
`/tmp/parties-eventstore-3129-audit-l14nfhpd/Parties`; it retains the actual
schema-3/41-route candidate probe and compile/API logs already recorded above.

Next bounded edits are the separate exact SDK API capability, sample Contracts
assembly split, existing Platform catalog tool gatewayAppId mapping, actual
package Gateway runtime hooks and AppHost lifecycle-owned private bootstrap.
Preserve the full Counter/Greeting/Parties persisted concurrent qualification;
there is no core-only reduction. Parent owns SDK mixed actor target/Sample
policies, independent gateway actor capture and public Parties adapter packaging.
Coordinate the original producer freeze only before the real live run.

## File Scope

Allowed files for this story:

- `.github/workflows/release.yml`
- `Directory.Build.targets` - implement or record the exact published catalog composition
- `Directory.Packages.props`
- `Hexalith.Parties.Standalone.slnx` - implement or record the exact published catalog composition
- `Hexalith.Parties.slnx` - implement or record the exact published catalog composition
- `README.md`
- `_bmad-output/implementation-artifacts/spec-published-catalog-sdk-adoption.md` - implement or record the exact published catalog composition
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`
- `_bmad-output/implementation-artifacts/tests/eventstore-3.129.0-adoption-investigation/claims.json` - implement or record the exact published catalog composition
- `docs/architecture.md`
- `docs/ci.md`
- `references/Hexalith.EventStore`
- `references/Hexalith.McpCli`
- `references/Hexalith.Memories`
- `references/Hexalith.Platform`
- `references/Hexalith.Tenants`
- `scripts/pack-release-packages.py`
- `scripts/prepare-aspire-runtime-hosts.sh` - enforce and independently verify the exact prepared SDK package cohort
- `scripts/validate-aspire-runtime-cohort.py` - enforce and independently verify the exact prepared SDK package cohort
- `scripts/validate-consumer-package-references.py`
- `scripts/validate-nuget-packages.py`
- `scripts/validate-publication-preflight.sh`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.client-only.yaml`
- `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`
- `src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogBootstrap.cs` - implement or record the exact published catalog composition
- `src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogConfiguration.cs` - implement or record the exact published catalog composition
- `src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogTool.cs` - implement or record the exact published catalog composition
- `src/Hexalith.Parties.AppHost/Program.cs` - implement or record the exact published catalog composition
- `src/Hexalith.Parties.Contracts/Routing/PartiesCatalogDeclarations.cs` - implement or record the exact published catalog composition
- `src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj`
- `src/Hexalith.Parties.EventStoreExtensions/PartiesCatalogAdapters.cs` - implement or record the exact published catalog composition
- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` - implement or record the exact published catalog composition
- `src/Hexalith.Parties/Program.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.Ci.Tests/AspireRuntimePreparationTests.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs`
- `tests/Hexalith.Parties.Ci.Tests/consumer_validation_cases.py`
- `tests/Hexalith.Parties.Ci.Tests/runtime_cohort_receipt_cases.py`
- `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshotTests.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.Contracts.Tests/PartiesCatalogAdapterTests.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.Contracts.Tests/PartiesCatalogTests.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.EventStoreGateway.TestHost/Program.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.EventStoreSample.Contracts.TestArtifact/Hexalith.Parties.EventStoreSample.Contracts.TestArtifact.csproj` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs`
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesCatalogParticipantStartup.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/DevelopmentCatalogConfigurationTests.cs` - verify finite profile transport, private bindings and unchanged native graph
- `tests/Hexalith.Parties.IntegrationTests/Topology/DevelopmentCatalogPreparationCleanupTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/PackagedGatewayTopologyTests.cs` - verify finite profile transport, private bindings and unchanged native graph
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesAspireTopologyFixtureTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesCatalogParticipantStartupTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesTopologyIsolationTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryOwnedRuntimeTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.Tests/Gateway/IsolatedDomainRouteCatalogGuard.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.Tests/Gateway/PartiesProcessTestFactory.cs` - implement or record the exact published catalog composition
- `tests/Hexalith.Parties.Tests/HealthChecks/HealthEndpointIntegrationTests.cs` - implement or record the exact published catalog composition
- `tools/nuget-trusted-publishing-policy.json`
- `tools/release-packages.json`

- `references/Hexalith.Builds`
- `.gitlink-signoff.tsv`
- `_bmad-output/implementation-artifacts/spec-sync-dependencies-and-publish-verified-release.md`

- `docs/component-inventory.md`
- `docs/index.md`
- `docs/project-overview.md`
- `docs/source-tree-analysis.md`
- `docs/event-publishing.md`
- `tests/README.md`
- `scripts/test.ps1`
- `tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs`
- `references/Hexalith.FrontComposer`

## Resumed implementation evidence

Upstream root f392b099 selects3.129.0 but incorrectly defines the native-host
source marker for packages. Availability now uses the separately qualified
`HEXALITH_EVENTSTORE_CATALOG_API`; native/source host selection retains its
source-only marker. Existing unit-isolation guards follow capability availability,
so package fixtures do not return503 solely because the new real catalog check is
unbootstrapped in that intentionally isolated HTTP fixture.

The linked sample Contracts now compile in a separate nonpackable/nonpublishable
artifact retaining `Hexalith.EventStore.Sample.Contracts` identity. The current
catalog API profile includes the actual native inventories and sample catalog
macro; the older compatibility profile excludes the four unavailable files. Actual
native Program/config/launch-profile bytes remain the inputs.

Focused runtime-preparation validation passes33/33 without skips, including exact
version/source marker controls, real copied configuration provider behavior and
missing source refusal. Ci.Tests Release compile has zero warnings/errors. Logs:
`/tmp/parties-3129-runtime-fixtures-build.log`,
`/tmp/parties-3129-runtime-fixtures-tests.log`.

The package Gateway now applies the published native catalog registration,
independent finite receiver and HTTP host-security hooks under API availability.
The older compatibility branch remains explicit. Kestrel's1MiB request limit is
configured before Build. Its exact compiled public SDK check remains in progress.

The existing Platform catalog executable is extended only to map the already
canonical projection-actor query to an explicit actual gatewayAppId/QueryAsync;
missing binding and unsupported transports refuse. Existing domain receivers and
SDK policy generation remain unchanged. Platform ownership evidence is separate.

The exact runtime preparation gate now propagates the evaluated root stable SDK
version and explicit owner-host package profile to all eight selected hosts. It
then reads each actual ProjectAssetsFile and rejects SDK family version or
package/project drift, missing libraries and missing assets. Source/native mode
retains its existing build arguments. All37focused runtime-preparation cases pass;
four actual asset-mutation controls cannot be satisfied by a property echo. Logs:
`/tmp/parties-3129-runtime-cohort-build.log`,
`/tmp/parties-3129-runtime-cohort-tests.log`.

Actual current package Gateway compilation exposes an owner API gap:
ProjectionNotificationTopicAttribute.Bind is internal. The wrapper deliberately
retains native subscription binding rather than silently omitting it. Owner SDK
work supplies a public composition shim in the coordinated3.129.1 release. The
focused service/Gateway fixture build is blocked by exactly CS0122 until that
public API is available; Contracts/adapter/schema cases pass4/4 with zero-warning
build (`/tmp/parties-3129-contract-catalog-build.log`,
`/tmp/parties-3129-contract-catalog-tests.log`).

The existing public SDK topology validator requires retained actual JSON bytes
for both component/ACL, exact keyPrefix-none shared component participant scopes,
and real observations for each selected caller granted receiver access. Existing
root broad/YAML/default ACLs cannot simply be copied into that profile. The
coordinated SDK owner provides a strictly private Development auxiliary-client
observer and explicit package-reference profile for its existing native peers.
Default/publish ACLs and authority rules remain unchanged. Memory.Server also
now captures its actual configured AppId through the existing contract, pushed
as0a1f6bf9 with31/31existing actual HTTP auth-boundary cases and zero-warning build.

## Frozen review checkpoint

All investigator code is now frozen for the parent combined SDK/root/Platform
review. Root local base remainsf392b099; parent reconciles the three newer remote
commits while preserving these edits. No live topology has started and no child
build/test process remains running. AppHost Program preserves original CRLF and
has exactly four added registration lines.

Current focused evidence: runtime preparation38/38, Contracts/catalog/adapters4/4,
private transport configuration plus existing package/native model parity10/10;
all have zero-warning/zero-error Release compilation and no skips. The finite
extra profile resources are asserted individually: one modeled shared routing
component and14secret parameters; every original resource, endpoint, wait,
sidecar identity/config and unrelated environment binding is still compared.
The restore controls include wrong version, project type, missing SDK, missing
assets and missing Contracts while Client/ServiceDefaults remain present.

The real existing Platform tool produces schema3 with7Sample routes/7operations,
Counter QueryAsync bound to the explicit gateway and six Sample HTTP targets.
Removing gatewayAppId refuses before either catalog or receipt output. Its owner
patch is22added/1removed lines across two files, plus its scoped spec; no owner
commit/push was performed. Root uses the actual same executable template, raw
immutable copies of prepared root/Sample PEs and restored published peer owner/
adapter packages, not duplicated catalog authority or policy construction.

Bootstrap starts only in the explicit package profile. It rechecks actual SDK
assets immediately before tool preparation, propagates the same SDK/package
profile into dotnet-run child environments, retains modeled/private Redis,
and substitutes actual private JSON component/ACL paths in generated sidecar
arguments before launch. SDK hosts independently capture/prepare/ready/commit/
read back using the unchanged public authority. Native/default/publish ACLs and
host selection remain unchanged. Memory refresh support includes exact3.0.12/.13
with the same existing protocol; actual3.0.13/source0a public archives are verified
by the parent in `/tmp/memories3013-nuget-publication.json`.

Outstanding: publish/retrieve the reviewed SDK3.129.1 (public native topic shim,
actual actor targets, real auxiliary observer, explicit peer package refs), adopt
normal Tenants6/Memory3.0.13 versions, reconcile selected SDK source pointers and
then run the full exact cohort/live proof. Root Gateway now calls the real public
WithEventStoreProjectionNotifications shim; current public3.129.0 lacks it, so
its full compile/live proof remains intentionally pending3.129.1. Root older
explicit3.128.4cohort requires the frozen Memory3.0.11/Tenants5.11.2 caller pins;
its source-host selector remains independent from catalog API availability.
Keep all183existing Aspire assertions, persisted Counter/Greeting/Parties, real
mTLS/authentication/writer/source checks and complete cleanup. Do not start live
until exact owner/public cohort is prepared and coordinate original producers.

## Tasks & Acceptance

The latest compatible release cohort is now SDK3.129.1 (pending publication of the reviewed additive owner fixes), Tenants6.0.0 (reviewed breaking trusted-catalog enrollment), Memories3.0.13 (all10officialarchives verified) and FrontComposer4.7.4 (all8officialarchives verified). Public package identity/source proof remains required; finite property selection support alone does not qualify a package.

Include the SDK's complete native-host/sample/actor/client-observer and six live-fixture compatibility fixes in the combined review. Preserve real admission, schema3 signed-surface eligibility, lifecycle/lease checks, authentication, actual identities and complete inventories. SDK normal source/publish defaults stay native; the qualified hosted test package property is explicit only. Parent bootstrap remains private Development composition using the actual modeled Dapr inputs and exact prepared runtime PEs, not a production trust implementation. The final parent run must preserve all183hosted Aspire test cases and actual authenticated concurrent persisted Counter/Greeting/Parties proof.

The public Parties adapter is included in all11package publication/consumer checks. Actual consumer execution must inspect its compiled41route/operation inventories and28adapters from the published archive. A manifest row or standalone compile does not qualify archive contents. Platform's existing public builder receives the actual Gateway app identity for the exact canonical projection actor transport and otherwise refuses unsupported targets.

## Combined source review checkpoint

All component implementers are frozen. SDK full owner solution Release build passes zero warnings/errors1m55. Combined Sample218, ServiceDefaults95, fixture15 tests pass without skips. Catalog regression run still in progress at packet creation. The prior six actual sidecar failures and auxiliary realKestrel/source-profile controls passed with source-hash-bound evidence. Root38runtime-cohort+4catalog+10ACL/model checks and actual Platform7route/7operation positive/missing-Gateway-negative tool proof pass. Full root runtime/package-consumer/official-publication proofs remain outstanding; no result is implied from static preparation.

## Review Triage Log

All three independent layers completed. Each finding is triaged separately below before grouping. Root-only bounded patches preserve the frozen intent; no new production trust policy or admission bypass is authorized.

| Finding | Verdict | Route | Evidence and action |
| --- | --- | --- | --- |
| B1 | high | patch | Duplicate conditional/unconditional Authentication using is real when the default catalog marker is set; remove one directive, preserve the namespace import for legacy/security calls. |
| B2 | medium | patch | SDK3.129.0 exposes catalog declarations but lacks the new public topic shim and auxiliary observer. Keep API availability distinct from the complete hosted-profile capability; reject the unsupported full profile explicitly before launch. |
| B3 | medium | patch | The supported non-mTLS sample UI has Config=null while bootstrap requires a loaded receiver ACL. Supply the existing deny-all transport configuration for that private client-only participant. |
| B4 | medium | patch | Actual Admin command/storage/health services call the Gateway, but its owner manifest lacks their finite invocation rules, so current narrowing deletes their broad policy. Preserve qualified finite operations and actual caller participation without granting wildcard authority; reject unresolved transport rather than silently deleting functional access. |
| B5 | medium | patch | Register accepts any run mode while metadata resolution hardcodes Release. Bind metadata/receipts to the actual launched configuration, or reject an unsupported selection before launching any host; no cross-configuration binaries may qualify. |
| B6 | medium | patch | Matching assets and an existing TargetPath do not bind stale runtime SDK binaries/deps to the completed prepare build. Add an actual prepared-output receipt and immutable PE/deps/asset checks for every selected runtime, including Admin/UI. |
| B7 | medium | patch | Independent package restoration can select different peer declarations/adapters from the actual source-built peer outputs. Generate from, and bind to, actual prepared host owner/adapter PEs; independently retain public archive source/version proof. |
| B8 | medium | patch | The private directory currently survives both BeforeStart failure and AppHost disposal. Add owned cleanup after bounded child-process teardown and application disposal; preserve unrelated directories. |
| B9 | medium | patch | Existing PartiesAspireTopologyFixture.InitializeAsync calls actual StartAsync and the full isolation case runs its real packaged bootstrap. Positive startup is already covered by required final E2E. Add preparation/receipt negative cases exercising the same validation entrypoint for missing outputs and skew, and require the actual existing E2E gate rather than claiming model construction as startup proof. |
| B10 | false | reject | The unchanged real PartiesTopologyIsolationTests.SeedPersistedCounterAsync submits commands then queries the real Gateway and asserts two distinct persisted Counter states. The new bootstrap uses the changed Platform tool for that exact runtime catalog, so a wrong actor binding fails this existing required E2E; it is not a mocked-only consumer. |
| E1 | high | patch | Same demonstrated duplicate-import compilation failure as B1; preserve this independent row and group its fix only after verdict. |
| E2 | medium | patch | Receiver skips deny operations before expanding overlapping allows. A valid policy mutation can reauthorize an explicitly denied method. Refuse unsupported deny-bearing input before emitting private authority, with a grounded overlapping-deny negative. |
| E3 | medium | patch | Add de-duplicates only path and drops later distinct verbs. Merge the finite verb set in both emitted ACL and observed invocation inventory, with a split-verb input test. |
| G1 | medium | patch | Accepted verification-gap evidence: wrapper tests assert only subscribe200, separate helper tests do not execute wrapper wiring. Add actual Gateway tests for nondefault publisher component, subscription JSON/topic metadata and declarative absence. |

Groups: B1/E1 duplicate import; all remaining survivors have separate demonstrated root causes. B10 rejected with the concrete real transport/persisted consumer. SDK owner source has no surviving defect from this combined review and can proceed through its exact-source CI/publication while root patches and final actual topology proof continue.

### Bounded signed UI fixture adoption

The existing genuine Counter/Greeting UI-origin fixture JWT and explicit administrator UI issuer now sign `eventstore:surface=ui`. Schema3 eligibility requires the authenticated surface claim; this is explicit trusted test issuer intent, never an inference from roles/headers. Existing roles, tenant/domain/permission claims, signature validation, expiry, issuer/audience and negative authentication controls remain. Actual Counter/persisted and Admin/UI E2E exercises these exact tokens; the final actual new-cohort run is still pending package publication.

The real Party command/query UI-origin E2E issuer also signs the explicit UI surface. The existing local confidential authorization-code client gains only the standard hardcoded UI surface mapper; all public direct-grant client definitions, roles, tenant/domain/permission mappers and audiences remain identical. This local Development realm change supplies the new SDK contract and does not configure Production trust. Mapper configuration follows the official Keycloak protocol mapper reference: https://www.keycloak.org/admin-api/protocol-mappers . Existing realm authorization-flow/audience/role checks and full persisted UI/Party E2E remain binding validation.

### Accepted bootstrap/receipt patch evidence

B3 supplies an actual deny-all client-only Dapr configuration for both private
non-mTLS UI sidecars. Native and publish selections remain unchanged. B4 keeps
both actual Admin participants and preserves their source GET/POST/PUT grants
for the SDK-owned typed technical receiver qualification; no root catalog
operations or DELETE authority are invented. Admin receives the explicit
admin-receiver role, while receiver-free UI participants alone use aux-client.
Default-open, unsupported wildcard and unresolved ordinary owner transports fail.

B5 binds the launched AppHost AssemblyConfiguration to matching Debug/Release
preparation metadata and receipts. B6 records completed fresh rebuilds and
revalidates actual runtime entry/dependency/configuration/asset bytes plus all
runtime PE files, against embedded identities and runtime entries from the same
actual restored SDK/peer archives. Every prepared host participates, including
Admin/UI. The immutable complete manifest is checked again after the tool build,
immediately before BeforeStart allows runtime launch. Root SDK/FrontComposer/
Tenants/Memories selections propagate identically through prepare and dotnet-run.

B7 now feeds the real prepared peer owner/adapter PEs to the existing Platform
tool. It performs no independent version-skew owner restore. Its immutable copy
refuses same-name/different-byte libraries; public archive provenance remains
independently required. Memories owner profile5aaa3983 uses its actual official
3.0.13libraries, preserves ordinary source/publish edges, and includes its public
adapter PE. Both real published-profile native host builds and ordinary native
Server source build passed0warnings/errors;15actual SDK/owner deployed DLLs
matched their restored archive entries. Tenants owner profile is coordinated
separately. This profile changes no production authority or contract policy.

B8 uses new run-owned directories and immutable owner markers, removes partial
outputs after bounded stopped child failures, and disposes successful private
inputs with the AppHost. Unreaped owned children prevent deletion; foreign
ownership is never removed. B9 actual receipt mutations exercise the same
validator before runners can start. E2 overlapping explicit denies reject before
any expansion, including auxiliary receivers. E3 repeated path grants merge
finite verbs in both actual ACL and independent DeclaredInvocations.

Focused verification: zero-warning IntegrationTests Release compile against
public SDK3.129.0/Tenants5.11.2/Memories3.0.13;19/19config/overlapping-deny/
verb-merge/ownership/actual IValueProvider token/model-compatibility cases passed.
The unsupported3.129.0full profile fails before launch; this scoped result does
not claim SDK3.129.1runtime qualification. Runtime receipt suite7/7passed with
fresh/missing/stale entry outputs, deployed/archive/assets/deps/config/receipt
mutations, configuration mismatch and wrong package type/version controls.
Preparation/receipt integration cases15/15passed. Logs:
`/tmp/parties-reviewed-bootstrap-integration-build.log`,
`/tmp/parties-reviewed-bootstrap-focused-tests.log`,
`/tmp/parties-runtime-receipt-tests.log`,
`/tmp/parties-review-receipt-ci-tests.log`.

Actual sidecar probes now resolve the final modeled DAPR_API_TOKEN through
Aspire IValueProvider, including secret parameters and later fixture overrides.
Readiness/metadata/invocation clients share that credential; raw missing/wrong
credential rejection probes remain unchanged. The full existing real BeforeStart
and persisted Counter/Greeting/Parties/mTLS/Admin/UI gates remain required after
the exact published3.129.1cohort is prepared; no assertions or tests are removed.

### Required explicit Admin UI lifecycle checkpoint

The private catalog includes the actual explicitly started Admin UI. Every
private fixture scope now coordinates its real StartCommand, including ordinary
and Memories-only readiness scopes; the AppHost explicit-start annotation and
all waits remain unchanged. Prebuild authentication/channel setup detects the
actual bootstrap-owned secret parameter pair. After a real BeforeStart event,
the coordinator waits for DCP NotStarted and verifies the actual aux-client role,
absolute private directory and RequiredHostIds before issuing the command.

The command observer runs concurrently with the startup delegate: an existing
live-shutdown delegate can wait for Parties readiness without preventing the UI
from enrolling and catalog commit. A no-op controlled startup without BeforeStart
cancels its observer and never starts an optional UI. Actual command failure
cancels and joins the owned readiness-waiting startup, preserving its diagnostic.
The isolated infrastructure-failure model explicitly selects native metadata so
its intended startup failure remains independent of package capability guards;
its original reason/cleanup assertions remain binding.

Focused lifecycle and existing fixture tests:21/21passed, zero skips; Release
compile0warnings/errors against the bounded public SDK3.129.0compile cohort.
The actual native model retains AdminUI explicit-start and AdminServer wait;
all project wait targets have no HealthCheckAnnotation, so existing process
startup waits do not form a catalog-readiness cycle. Logs:
`/tmp/parties-catalog-ui-lifecycle-build.log`,
`/tmp/parties-catalog-ui-lifecycle-tests.log`.

SDK B7 integration binds only the existing Gateway actor-state owner map under
EventStore:RouteCatalog:DevelopmentActorStateOwners:0. AppId is the actual selected
Gateway identity, HttpEndpoint is its actual generated CLI EndpointReference,
and ApiToken is its corresponding run-private secret ParameterResource. The
Admin host's own token is never reused. SDK owns the strict code/lease/GET-only
actor-state qualification; root introduces no raw method/write authority.
The exact published SDK3.129.1/Tenants6complete runtime gate remains pending.

## Concurrent upstream reconciliation

The final remote-head audit at 14:46 UTC found SDK feature commit 5e3c2f0e60b114cdaab52594ccbb254829d40e0f, MCP operator documentation a267e4709f4cbea34511c470bb1823c79a732327, and Platform e243a680a54ff9a05275a6868ddd38c6d32cab96. Preserve all upstream work. The SDK feature changes semantic release selection to 3.130.0; the earlier intended 3.129.1 publication was never performed. Current finite package API/host capability selection therefore qualifies 3.130.0 explicitly and rejects unpublished 3.129.1. SDK 3.129.0 remains declaration API only. Prior source-build results above are historical evidence, not official package publication claims.

The replay fixture correction passed all 18 focused checks and all 17 unchanged real-sidecar cases, including the six hosted failures. It was committed locally as fcd875b26 and replayed unchanged onto the upstream SDK feature as dfb9bc86f40f6bb438de5495dbbd15d4a68dcb3c. The private Admin transport delta is reconciled separately against the same upstream source before publication. All final compiled/public-package/runtime proofs must use the actual 3.130.0 release and its exact source SHA.

Platform fast-forwarded cleanly; its CatalogBuilder sources are unchanged from the separately verified explicit Gateway binding. MCP's new upstream commit reintroduced the incompatible frozen Tenants source gitlink; retain its documentation/evidence and restore only the already qualified 33337d18 metadata pin without initializing nested dependencies. The Tenants 6 release continues from its already green exact c127 source; a subsequent finite 3.130.0 host-build alias must pass its existing effective-selection checks before final root preparation.

## Protected publication and current inventory qualification

The current SDK qualification uses existing protected PR376 at6bcd40e3158d5b99e8d94cd300575768a6e479ab. The earlier main reset is preserved; no additional main-rule bypass is used. The branch reconciles all existing packaging/audit work plus the actual Admin/native-actor/pubsub callback fixes. Integrated SDK solution Release build passes with zero warnings/errors, real native modes3/3, callback metadata10/10, full package-manifest137/137 and owned subprocess controls6/6, all zero skips. Exact protected automated checks, normal squash merge and exact-main publication proofs remain required. The subprocess test now observes actual child termination inside its unchanged seven-second total bound.

The current Root API-only compatibility check uses published SDK3.129.0 with the new Tenants6.0.0/Memories3.0.13/FrontComposer4.7.4 pins. Standalone Release builds with zero warnings/errors and all230Contracts tests pass. This does not qualify the complete3.130.0 hosted profile. Full service checks exposed two current inventory omissions: the separate non-packable sample contracts artifact and deny-only auxiliary client ACL. Documentation, explicit PowerShell support-project inventory and the exact expected project set now name them; all10DocumentationFitness tests and PowerShell syntax pass after a normal restore. A prior no-restore attempt correctly failed on temporary package-test cache paths already removed by test cleanup; normal restore resolved it. The remaining three service failures are the intentionally outstanding final gitlink/current-selection observations. The single CI-helper failure is the default3.130.0 package not being public yet. Neither is waived.

The existing operational follow-up documents and17packet/source files were preserved byte-for-byte in a separate documentation commit166fd757; the retained technical SHA256SUMS validates all11entries. No operational evidence collection, Secret/archive access, recovery, infrastructure change or external message was performed for this release task.
