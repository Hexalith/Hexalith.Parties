---
title: Adopt the published catalog SDK and qualified owner artifacts
status: done
review_loop_iteration: 1
created: 2026-10-31
baseline_commit: 6febe55d1c711a305a5969f04b8b9fb1fa80722f
---


## Completed adoption — 2026-11-09

The selected latest stable EventStore SDK is **4.2.0**, independently rechecked at20:22UTC. Qualified source **84e3df796ac977041a21ec7bd8e5789bdc44241a** is published as [Parties v3.1.1](https://github.com/Hexalith/Hexalith.Parties/releases/tag/v3.1.1). All eleven exact-source CI jobs, independent RC/commitlint/CodeQL and the protected Release publication verifier pass. All eleven freshly downloaded official NuGet archives pass signature, exact source, SDK4.2managed-reference and GitHub-release PE-byte checks; all three container images pass live registry verification for amd64 and arm64. [Final acceptance](tests/published-catalog-sdk-adoption-2026-11-09/completed-sdk420-v3.1.1/acceptance.json) binds the actual results and preserved failures.

This completes the catalog SDK adoption only. The original3.129-era intent and every dated checkpoint below remain historical. Existing deferred-work entries, six unvalidated canonical parity receipts, G5 availability, deployment and rollback-deletion decisions retain their separate status. Tenants PR68 remains unmerged; its exact CI-qualified source branch is explicitly selected without an owner approval claim.

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

- `.github/workflows/ci-lifecycle-qualification.yml` - use the same qualified shared CI toolchain
- `.config/dotnet-tools.json` - align the current published SDK/tool and exact evidence bindings
- `.github/workflows/current-owner-startup-diagnostics.yml` - align the current published SDK/tool and exact evidence bindings
- `docs/getting-started.md` - align the current published SDK/tool and exact evidence bindings
- `scripts/test_current_owner_integration.py` - align the current published SDK/tool and exact evidence bindings
- `scripts/test_public_package_integration.py` - align the current published SDK/tool and exact evidence bindings
- `scripts/verify-current-owner-integration.py` - align the current published SDK/tool and exact evidence bindings
- `scripts/verify-public-package-integration.py` - align the current published SDK/tool and exact evidence bindings
- `tests/Hexalith.Parties.Platform.IntegrationTests/PublicRuntimeImageObservations.cs` - align the current published SDK/tool and exact evidence bindings
- `tests/Hexalith.Parties.Tests/FitnessTests/PackageCatalogFallbackTests.cs` - align the current published SDK/tool and exact evidence bindings

- `.gitignore` - ignore only generated native Admin UI static-asset staging

- `.github/workflows/release.yml`
- `.github/workflows/ci.yml` - reconcile incoming staged enrollment with the accepted producer and retain backend/Aspire gates
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
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.eventstore-admin.catalog.yaml` - explicit strict package-profile Admin ACL; retain native/publish input
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
- `tests/Hexalith.Parties.EventStoreGateway.TestHost/Program.cs`
- `tests/Hexalith.Parties.EventStoreGateway.TestHost/PrivateDaprPlacementCredential.cs` - private local sidecar credential on the unchanged SDK placement probe
- `tests/Hexalith.Parties.IntegrationTests/Topology/PrivateDaprPlacementCredentialTests.cs` - real SDK protected metadata and readiness controls
- `tests/Hexalith.Parties.IntegrationTests/Topology/PrivateCatalogWorkloadAuthenticationTests.cs` - actual SDK signed shared-verifier and partial-dedicated refusal controls
- `tests/Hexalith.Parties.Tests/HealthChecks/DaprTenantsReadinessProbeTests.cs` - actual bound receiver readiness and malformed endpoint refusal
- `tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj` - bounded private UI composition, inventory and HTTP evidence
- `tests/Hexalith.Parties.IntegrationTests/Topology/PrivateAdminUiCompositionTests.cs` - bounded private UI composition, inventory and HTTP evidence
- `tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Properties/launchSettings.json` - private client-only native Admin UI composition and exact source artifact binding
- `tests/Hexalith.Parties.EventStoreAdminUI.TestHost/PrivateAdminUiComposition.cs` - private client-only native Admin UI composition and exact source artifact binding
- `tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Program.cs` - private client-only native Admin UI composition and exact source artifact binding
- `tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj` - private client-only native Admin UI composition and exact source artifact binding - implement or record the exact published catalog composition
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


## Final public 3.130.0 runtime preparation — Admin dependency qualification

Default public restore and standalone Release passed with zero warnings/errors. Individual static projects passed 6,245 cases, with five existing skips and only the three expected final committed-source/current-matrix metadata failures still outstanding; all 68 real browser cases passed. Integration Release compiled with zero warnings/errors. Published SDK3.130.0 archives bind source484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c; selected SDK1de42b706f52af0da6513f0b7663002606d17156 adds only an unused test stream helper and does not replace that published source identity.

Fresh preparation exposed a validator error before any topology started: Admin.Server.Host built successfully, but the universal SDK.Client minimum refused its actual package closure. Its owning project declares Admin.Server and ServiceDefaults. Admin.Server source uses Dapr.Client directly; the independently retrieved official3.130.0 nuspec and actual restored/deployed graph require Admin.Abstractions and Contracts, with no SDK.Client dependency. Only the exact declared Admin.Server.Host project now requires those four SDK packages. Admin.UI and all other hosts retain the existing Client/Contracts/ServiceDefaults minimum. Assets, deployed dependency manifests, receipt readback, exact package type/version, official archive identity/source and deployed PE byte checks remain enforced. A same-named foreign project receives no exception.

Controlled red restored the universal minimum and reproduced the original missing-Client refusal. All12 Python receipt cases pass, including removal of each required Admin dependency from both assets and deployed manifests, rehashed-readback attempts, foreign versions, source edges, stale PEs and ordinary/lookalike hosts. Exact command: `python3 tests/Hexalith.Parties.Ci.Tests/runtime_cohort_receipt_cases.py`. Its existing owning xUnit entrypoint `CompletedRuntimeReceiptsRefuseDriftBeforeAnyRunner` also passes1/1 with no skips. Logs: `/tmp/parties-public130-admin-receipt-{red,tests,xunit}.log`; XML: `/tmp/parties-public130-admin-receipt-xunit.xml`. No SDK product change or added unused dependency is required. Regenerate preparation after this correction; no earlier partial receipt qualifies the runtime.


### First actual public-cohort startup and diagnostic preservation

Fresh all-nine-host plus adapter preparation passed with zero warnings/errors and ten complete receipts (manifest SHA2569888005699af65d16f2b9366989ef617067a365aec01476fbea7728d17e9cd1d). The first exact concurrent live test failed before domain assertions: its final owned-cleanup cancellation hid the original initialization failure. This is a failed startup, not runtime qualification. The session launched no owned host/sidecar processes; the existing ownership-checking cleanup script removed its exact three control-plane containers/network, followed by its private root. No other resources were touched. Log/XML `/tmp/parties-public130-concurrent-live.{log,xml}` and cleanup receipt `/tmp/parties-public130-first-start-cleanup-receipt.json` retain the outcome.

The fixture now retains its original initialization exception and aggregates it with an independently failing later disposal exception. Existing budgets, fail-closed cleanup/retention and explicit same-owner cleanup retry remain unchanged. The existing controlled failure test verifies both exact exceptions and successful retry/idempotence. The full affected `PartiesAspireTopologyFixtureTests` class passes12/12 without skips; Integration Release rebuild has zero warnings/errors. Logs/XML `/tmp/parties-public130-startup-diagnostics-{build,fixture}.log` and `/tmp/parties-public130-startup-diagnostics-fixture.xml`. Fresh preparation is rerun before another actual topology attempt.


### Actual startup cause and latest stable patch adoption

The diagnostic retry preserved both failures and proved the original cause: the tracked native Admin.Server ACL intentionally has defaultAction allow for its non-mTLS local-development mode; strict private catalog preparation correctly refuses that input. This occurs before any worker launch or domain assertions. The exact-owned second control plane and private root are gone; `/tmp/parties-public130-diagnostic-cleanup-receipt.json` records cleanup. Neither failed attempt qualifies domain runtime behavior.

The explicit packaged run profile now selects a separate tracked deny-default Admin ACL before mTLS rendering/model capture. Its exact existing Admin.UI /** GET/POST/PUT/DELETE grants remain intact. Native, old compatibility and publish profiles retain the original input; default-open refusal remains unchanged. Actual model/file tests cover both isolated mTLS and ordinary profile selection, and the concurrent real-session proof additionally requires an ungranted Parties caller to receive the Dapr ACL denial while actual Admin.UI access still succeeds. No post-hoc catalog authority or SDK refusal bypass is introduced.

During diagnosis, external stable SDK3.130.1 published from selected source1de42b706f52af0da6513f0b7663002606d17156. All14 official archives are independently verified, receipt `/tmp/eventstore-31301-nuget-publication.json`, SHA25687de454403bc0c6658a05fc96cadade7922de5618fd6f46e8396a4650a1b0bd3. Full Release41246774822 is green. The only product-source delta from130 is an unused integration-test helper/Redis dependency. Root selects exactly131 with explicit capability aliases retaining130 and refusing132/future minors; Tenants owns its matching finite alias qualification. Earlier130 static/browser results remain historical, not final131 proof. Regenerate public restore/build/static/runtime receipts for131.


### Fresh official3.130.1 static and browser evidence

Normal public `dotnet restore Hexalith.Parties.Standalone.slnx --force-evaluate --no-http-cache` succeeds with no version/source overrides or candidate feed. `dotnet build Hexalith.Parties.Standalone.slnx --configuration Release --no-restore --disable-build-servers -m:1 -warnaserror` succeeds with zero warnings/errors in87.73s. All15 individual static projects execute6,255 cases:6,247 pass, five existing skips, no execution errors/not-run, and exactly three pending committed-source/current-matrix fitness failures. Those failures remain the MCP/Platform committed gitlinks and the current matrix's missing actual131 package observation; none is waived. FullCi517 andContracts230 pass. Normal restore immediately after Contracts succeeds. Commands/results/XML are recorded in `/tmp/parties-public1301-static/report.json`; this is actual defaultpublic131 evidence.

`CI=true PATH=/home/administrator/.nvm/versions/node/v24.22.1/bin:$PATH npm --prefix tests/e2e run test:ui` passes all68 real browser cases in1.2m, including actual restoredFC4.7.4 archive/served-asset byte checks and unchangedaxe4.14/visual gates. The managed server exits normally. Log `/tmp/parties-public1301-browser.log`. The affected preparation class passes42/42 and Admin configuration/model/fixture classes pass27/27 with no skips; actual Integration compilation succeeds with zero warnings/errors. The new ACL remains a private profile input and has not yet been qualified by a successful real topology; full concurrent and whole-assembly runtime proof still must pass.

Concurrent external source changes after these checks are separately assessed before final native preparation: selected local SDK1de/public131 remains distinct from latest remote SDK93b54710d33354af3baec95d4ea5671b1b7db5ee; selected local Tenants6f34 remains distinct from latest remote9fcc. Material runtime/API changes cannot be silently treated as metadata. No current whole-runtime/publication success is claimed from static or older130 results.


### Qualified release-cohort source selection

The operator explicitly chose a separate Parties prerequisite release. Select published SDKv3.130.1/source1de42b706f52af0da6513f0b7663002606d17156 and the finite Tenants qualification commit3dc758ed5d460751fa41916fff90eaf6fdb3ead9 on `fix/qualify-sdk-3.130.1`. The latter is a single descendant of6f34; an independent Git tree comparison proves all4,814 entries outside its five exact workflow push-trigger files and owning spec remain byte/mode-identical. It preserves the already qualified public131/66HTTP/71selector-floor/host-profile behavior. Both selected original checkouts are clean at those remote-published commit coordinates. Newer SDK and Tenant mains are fetched and preserved, without importing their unrelated runtime/API features into this release.

Final owner gates follow those actual selected sources: existing SDK tagv3.130.1 Integration and the normal separate-ref Tenant qualification CI. Newer unrelated main cancellation or missing tests cannot substitute for their actual gates. The existing ledger disposition remains `validated-advance` for moves from Root's old pointers; final ref annotations must name the real published SDK tag and Tenant qualification branch, rather than claim these are latest main tips. No new ledger line is written before final successful qualification. Final Integration rebuild against actual public131 and selected source identities succeeds with zero warnings/errors in34.53s. Fresh ten-artifact preparation and actual concurrent/whole-assembly runtime proof follow, with producer inputs frozen during live execution.


### External sync checkpoint and bounded CI reconciliation

A competing sync changed SDK/Tenant checkouts during preparation and later committed the pending Root changes asb1859b81, on top of incomingcc57ab3e. Both commits and every branch/ref are preserved. No actual131 topology started; the task-owned completed manifest was invalidated and its bytes retained in `/tmp/parties-public1301-drifted-prepared.json`, with source drift evidence `/tmp/parties-public1301-frozen-source-drift.json`. The operator explicitly confirmed external sync stopped before normal qualified-cohort reselection. This incomplete preparation is not runtime proof.

Incomingcc57 changes only RootCI and the Platform.IntegrationTests project/fixture. Its exact main CI41249147216 fails at startup. The still-accepted domain-ci.yml@1745ea0f09df9e518aeea3eeb1a7df836f67e09d declares neither integration-test-profiles nor registry-pull credential secrets. The same commit removes the original Main/Sample/Ci backend projects and the complete Aspire integration lane; the existing focused regression test reproduces their loss. Restore supported inputs and all original blocking lanes while retaining the new package project/stream-observer fixture code and leaving its separate package-workflow enrollment staged until a qualified workflow/tool handoff. This preserves the accepted defaultpublic131 compiler/cohort rather than silently selecting a different module preparation profile. No v2 producer pin, optional check, authentication policy or source gate is weakened.


### Safe checkpoint — private Admin UI composition (no live topology)

The actual stable131 concurrent run failed within its unchanged five-minute initialization budget at `sidecar-eventstore-admin-ui`, before any Counter/Greeting/Parties/security assertion. Gateway and Admin sidecar readiness had already passed. Exact source/log trace proves native UseAdminUI maps an empty GET/dapr/subscribe endpoint, while the public131 auxiliary-client observer correctly refuses any such SDK receiver path. The UI process stops, leaving its sidecar unready. The actual failure332.334s and all-owned automatic cleanup are retained in `/tmp/parties-public1301-concurrent-live.{log,xml}` and `/tmp/parties-public1301-concurrent-cleanup-receipt.json`. No runtime qualification is claimed. All session80b52fee5eb9 processes, Redis, control plane/network and private root are absent; producer freeze is thawed.

A Root-owned nonpackable AdminUI test host now compiles the genuine native UI as an explicitly ordered application artifact with global publicSDK131 properties; all SDK libraries remain ordinary official PackageReferences. It invokes the actual public AddAdminUI/RequireDaprSidecar/defaults/SignalR APIs and maps actual public SDK Components.App with the complete native middleware sequence. Only the empty discovery endpoint is absent; GET/dapr/subscribe retains native HTTPS-redirection exclusion, so unmatched routing can genuinely return404. Pinned Dapr1.18.7 primary source subscriptions.go197–211 interprets404 as zero subscriptions,nil. Native/default/publish remain selected on the original project/UseAdminUI behavior. No classifier/path/authority bypass, fake response, hidden middleware or substituted SDK library is introduced.

First ProjectReference attempt failed522 compiler errors because NuGet discards AdditionalProperties during path traversal, restoring a sourceSDK3.1290 graph before a package131 build. That failed attempt is retained, not qualification. The corrected explicit ordered Restore/Build/GetTargetPath application artifact step succeeds with zero warnings/errors (v2:9.12s; v3 with DLL/static/deps provenance:2.51s). Both actual nativeUI and wrapper assets files contain only package3.130.1 SDK libraries. Native project/source commit/global properties and native DLL/deps/static hashes are emitted to the wrapper output native-admin-ui-artifact.tsv; the existing runtime receipt collector now includes static assets/manifest/native proof files. Source tag remains1de and official SDK packages are unchanged.

Current completed edits: new host project, Program, one composition helper type and copied native launch profile; AppHost selection before AddProject in the explicit supported package run profile; preparation inventory switches AdminUI only when evaluated CatalogHostsSupported=true; both solution inventories include the nonpackable host. Build logs `/tmp/parties-private-admin-ui-build{,-v2,-v3}.log`. Public API inspection uses the allowed dotnet-inspect skill and prepared nativeUI DLL; logs `/tmp/parties-admin-ui-{public,app}-api-inspection.log`. No HTTP404, actualDapr zero-subscription, static HTTP bytes, UI/auth rendering or new actual topology success has been proved yet.

Remaining bounded work: focused real HTTP composition test (native200[] versus private404 without redirect), root CSS/interop/download and Fluent asset byte proof, actual SDK Razor/auth behavior, native/publish/model and preparation-fixture inventory checks, docs/support project inventory updates, then relevant build/tests. The new host is not yet covered by those tests. Verify/select next Tenant8e785341 qualification successor (tests/spec-only), preserve newer main60cc and SDK main7e. Then final Integration build and entirely fresh ten-artifact preparation LAST; run unchanged expanded concurrent method and full Integration assembly, cleanup/thaw. Selected SDK tag's actual publishedcompat49/1 false-green is a separate blocking owner gate under repair; never accept workflow success without actual counts. Final metadata/ledger/major commit/push/RootCI+RC/Release/all11NuGets remain incomplete.


### Private Admin UI composition — focused qualification 2026-11-01

The explicit supported package run profile selects the Root-owned nonpackable Admin UI composition before AddProject. Native and publish models retain the original native UI project and unchanged UseAdminUI. The wrapper uses the actual native UI application artifact, public SDK131 packages and actual Razor application. Ordered Restore/Build binds selected source1de, exact global131 properties, native project/DLL/deps and copied assets. Generated source wwwroot staging is ignored (not a vendored tracked asset tree); repeated normal builds have80 static endpoints with no duplicate route+selector binding. Gzip selector variants are intentional. Exact source/copy/provenance/static-manifest files are hashed in the immutable preparation receipt.

Focused current evidence: zero-warning Integration/Ci/service builds;8/8 actual HTTP/model tests (native200empty/private404 with no redirect, exact native CSS/JS + actual Fluent HTTP response, unchanged authentication challenge and genuine SDK Razor settings rendering);42/42 actual preparation tests;14/14 receipt Python cases including source/output/native-application/static-manifest drift and bad initial provenance hash refusal;10/10 documentation fitness cases. Logs `/tmp/parties-private-admin-ui-http-model.{log,xml}`, `...-preparation.{log,xml}`, `...-receipt-python.log`, `...-doc-fitness.{log,xml}` and `...-static-endpoint-proof.json`. These do not claim actual Dapr delivery/catalog enrollment. The unchanged concurrent live method now additionally requires genuine404/no redirect, actual Admin UI sidecar ID and zero subscriptions, native asset byte identity, real Razor rendering and successful authenticated Admin routing.

The incoming staged Platform.IntegrationTests project explicitly compiles with normal publicSDK3.130.1 in Release with warnings as errors (0warnings/errors,3.11s), `/tmp/parties-public1301-preserved-platform-project-build.log`. Standalone's BuildProject=false does not compile that project; this explicit compile preserves incoming work without activating or qualifying its staged module profile. Tenant exact8e full CI/538 Aspire cases and all separate workflows are independently verified; original checkout is normally selected at8e before fresh runtime preparation. SDK remains published tag1de. Fresh full runtime qualification remains pending; no successful public131 live assertion is claimed by this focused evidence.


### Actual private UI startup and catalog-tool consumer mismatch

Fresh131 preparation succeeded for9hosts+adapter with actual SDK1de/Tenant8e and all0warning/error builds; completed manifest `/tmp/parties-public1301-wrapper-prepared.json` SHA256bfb219299368e72a41cbaeaa538e5121e92a8a5c8c0e9a7d73e84927a2ea6150. The unchanged full concurrent method then failed before domain assertions in325.607s (1failed,0skips), `/tmp/parties-public1301-wrapper-concurrent-live.{log,xml}`. The actual AdminUI independently captured its receiver-free auxiliary surface with no subscription endpoint/incoming operations; its prior receiver refusal is resolved. No activated runtime success is claimed.

The actual coordinator now correctly refuses activation because all45 candidate idempotency policies bind default ConsumerAppId=eventstore while the isolated gateway is eventstore-979cfa850715. Every route has an enrolled actual application identity. Compact captured-file facts and exact catalog hash are preserved in `/tmp/parties-public1301-activation-consumer-mismatch.json`. The owning Platform tool calls the2argument DevelopmentIdempotencyBindings.Bind; the existing public131 SDK3argument API and native Aspire path already support actual gatewayAppId. Root's request already supplies that independently modeled identity. A bounded owner-tool correction is being qualified independently; do not rewrite candidate descriptors or weaken SDK activation checks.

All owned session979cfa850715 private files/processes/containers/network are gone; `/tmp/parties-public1301-wrapper-concurrent-cleanup-receipt.json` confirms automatic cleanup. Producer freeze thawed after cleanup. Any selected Platform successor requires fresh source/PE preparation before another live run. Startup/cleanup budgets, all Counter/Greeting/Parties/auth/mTLS/isolation assertions and full assembly gate remain unchanged.


### Reviewed owner consumer binding selected — 2026-11-01

Platform exactpushed d9d0eb18e8f30826289991e5972dd200c4d48fd0 (fix/catalog-consumer-run-binding, parent68b785b6) is normally selected after its single required independent review and all6 triaged corrections. The two product files Program.cs/csproj travel together: actual explicit gatewayAppId binds every command policy through the public SDK3argument factory only for qualified130/131; legacy129 retains its actual literal eventstore consumer and refuses unsupported scoped consumers. No descriptor rewriting or SDK gate changes occur. Ownerdefault129 build without a version override has0warnings/errors. Public130/131 real Sample+Tenant controls pass13each; copied actualRoot131 owners pass11controls with45actual policies,1canonicalactor query,19domain queries and7SampleRoutes, while original declaration/binding/operation/nonconsumer-policy data remain structurally equal. Proofpacket `/tmp/platform-catalog-consumer-ky973qv0/proof/packet-manifest.json`. OwnerCI remains a final gate; local runtime may overlap it.

The Builds checkout observation was stale: its prior local main ef40e875 contains pre-stop pushall merges with a tree exactly equal to the already qualified d8d0d70f (e7d8a28a846040882f31dafdb1b876d4def45890), not a new competing write. Normal detached d8 selection preserves ef40 plus newer fetched source heads/tags. SDK1de/Tenant8e remain selected. Platform original newer localmain799d29b1 is likewise preserved; qualifiedd9 is separately selected. No reset, force or nested operation was used.

Final integration-runner compilation after exact selection and meaningful nonnull CSS MIME assertion passes with0warnings/errors,5.77s (`/tmp/parties-public1301-consumer-fixed-integration-build.log`). Four focused actual HTTP cases are rerun before fresh preparation. Source/ref metadata remains preview-only: `/tmp/parties-final-metadata-preview.diff`; no ledger finalization or release qualification is asserted. Fresh runtime preparation/concurrent/fullassembly remain mandatory.


### Final source audit excludes unqualified FrontComposer metadata

Parent exact-source audit identifies the pre-stop e442544c FC nested-gitlink-only update as failing required owner-pin governance in both CI41246730936 and Quality41246730593. Its code/public474bytes are unchanged, but it cannot be called source-qualified. Previously qualified6811bfd6 has all5 exact-source gates green. The just-started consumer-fixed run was explicitly cancelled with SIGINT only to exactowned test PID399541, before qualification; its xUnit result57.413s/exit58 and separately observable cleanup cancellation are retained. This is not a product failure or a passing runtime proof. All owned session440735a068f3 processes/private root/network and its3actualAspirecontainers are absent, `/tmp/parties-public1301-consumer-fixed-cancelled-cleanup.json`.

Normal detached FC681 selection preserves e442 main and all newer refs. Fresh final-source Integration/preparation follows before live. Other selected-source gates are independently green (`/tmp/parties-selected-five-remote-gates.json`, FC681 source-audit receipt, Tenant8e full10851+538 case proofs and Platformd9 both matrix artifacts). SDK131/source1de coreCI/Lint and published-characterization108/108 are verified; only actual live-sidecar gate remains pending. Historical239case legacy receipt remains explicitly nonqualifying and unchanged. No ledger or release success is claimed.


### Actual SDK technical HTTP reachability defect — mandatory maintenance prerequisite

The final ten selected sources and fresh10receipt preparation were unchanged and successful, but actual publicSDK131 gateway Prepare rejects its loaded topology; the other7 participants reach Ready. The unchanged concurrent method fails before domain assertions in326.844s (1failed/0skipped), `/tmp/parties-public1301-final-concurrent-live.{log,xml}`. Exactowned automatic cleanup is complete: session85ca5575870e private root/processes/network and its3actualAspirecontainers are absent (`/tmp/parties-public1301-final-concurrent-cleanup-receipt.json`). Original source/producer freeze thawed only after those checks.

An isolated zero-warning diagnostic uses the actual prepared publicContracts131 DLL and actual captured activation/observation inputs, without modifying any host or library. It reproduces `unbound-technical-controller-code-or-policy` only for gateway;7others validate. RouteCatalogTopologyValidator.ValidateTechnicalReceiver computes reachable endpoints by path without intersecting actual HTTP verbs. Actual Tenant/Parties **POST grants therefore demandPOST witnesses fromGET/PUT-only routes (14each). More decisively, the exact existing Sample UI /api/v1/commands POST grant includes the descendant GET command-status endpoint by path: the validator rejects it despite that endpoint being unreachable byPOST. Narrowing Root's real POST paths cannot remove this counterexample without dropping supported status endpoints, path tricks, or broader caller grants; those are disallowed. All actual endpoint/auxiliary descriptor bindings otherwise match.

All29 bounded endpoint/caller/grant diagnostics, actual8observations+activation/catalog and publicSDK diagnostic source/build/results remain private0600 under `/tmp/parties-topology-diagnostic-85ca5575870e`; `technical-policy-diagnostics.json` records the exact Sample counterexample. Parent authorizes a minimal SDK maintenance correction from the qualified source lineage, preserving actual full observation, endpoint/code/auth/catalog/lease boundaries and unknown-method refusal. No newer feature main, fake inventory, synthetic authority or grant expansion is substituted. OwnerSDK131 executor0a635 fullCI/Lint/Integration is green (live304/4existingworker skips +metadata1, published108pass with unchanged nonqualifying239legacy receipt); the new Root cross-verb counterexample remains a real newly demonstrated prerequisite despite those successful owner gates.

Expected maintenance132 finite downstream changes are prepared only in `/tmp/parties-sdk132-finite-adoption.diff` (3Rootpaths). They retain earlier supported aliases and meaningful source/version/type/PE controls. No Rootpin, package cache, metadata signoff or runtime success is changed/claimed before actual official maintenance publication and owner finite-alias qualification. Full Root public static/runtime/183-plus-case assembly and final release/eleven-archive verification remain mandatory after adoption.


### Actual public SDK3.130.2 adoption and static qualification

Official maintenanceSDK3.130.2/source28e3f260bbaf4619d1a99d656fba04c0c532387b is now public: all14 actual nuget.org archives independently downloaded/verified (receipt `/tmp/eventstore-31302-nuget-publication.json`, SHA2568e07e4b6be063dd23b60483e3dd920078dd78c1bcfc3b70e8341a0751a2ae312), exact protected sourceCI/Lint/fullIntegration and entireRelease41277013578 includingOCI succeed. The first ordinary Rootrestore failsNU1102 on a stale officialindex; its normal retry with the same nuget.org source and noHTTPcache succeeds. Both logs are retained (`/tmp/parties-public1302-restore{,-retry}.log`). No alternativefeed/archiveinjection/globalcacheclear occurs.

Root SDKdefault/API/host qualifiers now admit exact132 while retaining129/130/131 support; the former future132negative now tests133. Actual source tags/finitealiases normally selected: SDKv3.130.2→28e, Tenant7df847270719bd392a4ad2ea48c700c3a9e3d939 and Platforme37d8cecc77cebe2ce675367e45a8f72759a3664. Newer feature mains and all user commits remain preserved. Owner localproofs use actualpublic132; their fullCI/artifact gates remain separately required before finalledger/release.

Normal public132 standaloneRelease build succeeds with0warnings/errors17.21s. All15 individual static projects execute6256cases:6248pass,5existing skips,0errors/notrun and exactly3 deferred current-source metadata failures (committedMCP/Platform observations and currentpackageparagraph). They are not waived and require the finalcommittedmetadata class rerun. Contracts postfixture normalrestore succeeds. Browser68/68 passes55.2s, actual FC474 assets/axe/visual gates unchanged. Explicit preserved stagedPlatform.IntegrationTests132compile succeeds0warnings/errors1.67s, without activating its unqualified module runtime. Integrationrunner compiles0warnings/errors25.22s; actualHTTP/model8/8 passes withoutskip. Logs/report `/tmp/parties-public1302-{standalone-build.log,static/report.json,browser.log,preserved-platform-project-build.log,integration-build.log,http-model.xml}`.

An isolated replay using the actual normal-restored public132ContractsDLL (verifiedbyte-equal to its actual nupkg) validates all8 captured previous131 host topologies unchanged; `/tmp/parties-topology-diagnostic-sdk132/{result.log,provenance.json}`. This confirms the specific methodreachability correction without alteringgrants/observations; it is not fresh runtime qualification. Final9hosts+adapter preparation and fullpersisted concurrent/wholeassembly are next. No ledgerfinalization, Rootcommit or release/publication success is asserted yet.


### Actual public3.130.2 startup and private placement credential

The first full concurrent method failed before domain assertions after327.637s
(`/tmp/parties-public1302-concurrent-live.{log,xml}`). All eight actual catalog
participants observed, became ready and committed. The genuine SDK placement
probe then received HTTP401 from its protected local `/v1.0/metadata`: its typed
HTTP client does not propagate `DAPR_API_TOKEN`, although normal DaprClient calls
do. The actual health body is retained at
`/tmp/parties-public1302-live-health.json`. The unchanged five-minute startup
budget expired; automatic cleanup removed all three actual containers, session
processes/network and private root. Exact cleanup proof is
`/tmp/parties-public1302-concurrent-cleanup-receipt.json`.

The nonpackable package gateway configures only the existing SDK placement
probe's typed HTTP client with the actual configured local API token, only in
Development with the explicit private catalog profile. It preserves the actual
SDK probe, health check, metadata `hostReady` decision and all other clients.
Missing/invalid credentials remain401/unhealthy; Production and ordinary profile
receive no additional credential. Focused verification and fresh immutable
preparation/live qualification follow; no success is claimed yet.


The placement correction passed all six actual-SDK controls, all14 affected
HTTP/model controls with zero skips and a zero-warning Release build. Fresh
nine-host/adapter preparation produced ten SDK132 receipts. The next unchanged
concurrent method failed232.555s at the existing three-minute Parties `/health`
budget: Gateway placement, catalog and writer-protocol checks were healthy;
Parties' actual Tenants readiness client invoked Dapr GET `/ready`, which the SDK
correctly refuses outside its committed business/auxiliary inventory. Actual
receiver ACL and all eight committed participants were retained. Logs/XML:
`/tmp/parties-public1302-placement-concurrent-live.*`; exact automatic cleanup
proof: `/tmp/parties-public1302-placement-concurrent-cleanup-receipt.json`.

The explicit private package run binds the existing validated
`Tenants:ReadinessBaseAddress` health-only setting to the actual modeled Tenants
HTTP endpoint. It preserves the actual receiver's health response and all
independent Dapr GETready ACL, mTLS and cross-session assertions. Native/default
and publish models have no such binding. This uses ordinary endpoint health
configuration rather than changing SDK catalog eligibility. Focused actual
unready/malformed/model controls and fresh complete runtime proof follow.


The final focused readiness check is28/28 with zero skips
(`/tmp/parties-public1302-readiness-unit-tests.{log,xml}`), including actual
receiver200/503 health decisions and six malformed-binding refusals. Actual
package/native/publish model and HTTP/probe regressions are14/14 with zero skips
(`/tmp/parties-public1302-readiness-http-model.{log,xml}`). The model binds exactly
the `tenants` HTTP EndpointReference, and native/publish environments lack the
private readiness setting. Both affected Release builds have zero warnings:
unit17.24s and Integration20.65s. Fresh preparation and unchanged actual
concurrent/full Integration qualification remain required.


### Actual SDK132 business admission diagnostic

The first readiness-corrected attempt failed before catalog activation because
its owned Docker Redis backend60914 refused while the actual container returned
PONG. Evidence and exact complete cleanup are retained at
`/tmp/parties-public1302-readiness-owned-redis-startup-diagnosis.json` and
`/tmp/parties-public1302-readiness-concurrent-cleanup-receipt.json` (318.849s).
A separate exact-image owned Redis preflight returned PONG through its actual
published port54955 and removed itself. No source or budget was changed.

The single fresh unchanged retry then initialized both complete sessions, each
with eight committed participants. Its first Counter command returned500 because
the real Sample `/process` workload policy returned401 `verifier-unconfigured`.
The private bootstrap had accidentally created an incomplete dedicated verifier
by setting only `Authentication:WorkloadJwtBearer:Audience`; SDK intentionally
refuses to inherit signing trust when that section exists. Actual
`Authentication:Workload:Audience` already narrows the receiver's valid audience.
Removing only the redundant dedicated-section entry preserves SDK's documented
shared-JWT verifier, exact audience, issuer, key, caller and operation checks.
The deliberately partial dedicated contract must remain refused.

The101.473s failed command result and both complete cleanup proofs are retained at
`/tmp/parties-public1302-redis-retry-concurrent-live.{log,xml}` and
`/tmp/parties-public1302-redis-retry-concurrent-cleanup-receipt.json`. Both private
roots, six data containers, control planes, processes and networks are absent.
Actual signed positive/wrong-audience/issuer/key/caller/partial-contract controls
and fresh full runtime proof follow; no business success is claimed yet.


The actual SDK issuer/validator controls pass6/6: shared verifier accepts the
exact signed gateway/audience/operation, and wrong audience, issuer, key, caller
and partial dedicated configuration refuse. Together with existing private
HTTP/model controls,20/20 pass with no skips; the affected Integration Release
build is zero-warning30.01s. Logs/XML:
`/tmp/parties-public1302-workload-http-model.*` and
`/tmp/parties-public1302-workload-integration-build.log`.

Both final SDK132 downstream owner gates are independently green. ExactTenant
7df847270719bd392a4ad2ea48c700c3a9e3d939 CI41278404929 includes10,863main and
538actual Aspire cases, all passing without skips; live125/published66/blocking270
and source6 plus Source/Story/Lint/CodeQL are green. ExactPlatform
e37d8cecc77cebe2ce675367e45a8f72759a3664 CI41278445212 has all three public
SDK1300/131/132 matrix lanes and their actual13-case artifacts/source/DLL
identities verified. Combined receipt:
`/tmp/downstream-sdk132-drafts-wd24gzkq/proof/final-owner-milestone2.json`.
Actual Root concurrent/full-assembly/cleanup and final committed-source fitness
remain pending; these owner results do not substitute for them.


The signed-verifier correction allows both sessions to initialize and real
Counter processing to persist/complete sequences2 and5. The next unchanged
method still fails176.551s at initial second-session seeding's existing60s total
budget (line91→POST153), before Greeting/UI/disposal assertions. Its fifth
Counter completes at06:33:58.586Z but the HTTP submission does not return before
the remaining budget expires. Actual state-client2157responses have median3.78ms,
p9511.2ms/max83.46ms; seven domain invocation responses have median437ms/max591ms.
This distinguishes admission/response/status overhead from a transport hang;
no deadline, assertion or production policy is changed from this observation.
Both sessions' six data containers, control planes, private roots, processes and
networks are gone, verified in
`/tmp/parties-public1302-workload-concurrent-cleanup-receipt.json`.

A bounded test-only Stopwatch diagnostic records session/message identity,
POST/status/persisted-verification phases, duration and status. It prints no
JWT, API token, payload or private key and preserves the60s budget, all five
commands and every persisted-state assertion. One attribution run follows
against verified unchanged prepared publicSDK132 host outputs.


## Current SDK resumption — 2026-11-09

The requested latest stable SDK is EventStore **4.1.0**. Direct nuget.org flat-container indexes for DomainService, Contracts and Server agree. All 14 official SDK archives were independently retrieved; their nuspecs bind source `dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a`. The effective normal Release graph already selects SDK4.1.0, Tenants7.0.5, Memories3.0.25 and FrontComposer4.7.6. Earlier3.129/3.130 observations above remain historical and do not qualify this cohort.

Normal standalone restore and Release build pass with zero warnings/errors. The 16 individual static projects report6,403passed,14existing skips,0failures/errors/not-run. All10catalog tests execute without skips. Fresh complete runtime preparation, integrated persisted effects, browser/package-consumer validation and exact-source hosted release gates remain required; status remains in-review.

The ordinary AppHost baseline was started through Aspire CLI and inspected before edits. The security resource was unhealthy (TLS handshake EOF), leaving service resources waiting. The exact owned AppHost was stopped through Aspire CLI; `aspire ps` is empty. This is baseline diagnostic evidence, not runtime acceptance.

Current main CI42609106160 fails before compilation withHXW010: selected Platform requires module CLI[4.45.0,5.0.0-0), but the repository tool manifest selects4.44.13. The supported helper restores that exact manifest. Align the manifest to4.45.0 while retaining immutable workflow and production/release gates. The independent public-runtime verifier also remains bound to its historical published Platform/Builds tuple; no current-source runtime acceptance is inferred from that older proof.

### Resumed review triage

All three independent layers reviewed the full original-baseline diff before triage. Existing committed work is preserved.

| Finding | Verdict | Route | Evidence and action |
| --- | --- | --- | --- |
| R-B1 incomplete public result schema | medium | defer | The separate G4 public evidence fixture omits required execution bindings. Its verifier and lifecycle migration predate this SDK resumption; full released-schema validation belongs to that existing evidence contract. |
| R-B2 persisted tenant binding | medium | defer | Public/private G4 evidence validators check only the synthetic prefix, rather than declared isolation identity. Record this separate existing evidence-contract defect. |
| R-B3 missing selector observation | medium | patch | The current SDK adoption emits selectorCompatibilityObserved but the public evidence predicate ignores it. Require an exact true observation with absent/false refusal controls. |
| R-B4 future image observation | medium | defer | A future measurement passes the existing independent G4 image verifier. Binding observation time to the issued execution interval is separate pre-existing lifecycle evidence work. |
| R-B5 unqualified SDK family member | medium | patch | The receipt validator ignores unlisted SDK dependencies; Admin.Cli is also absent from its known public inventory. Refuse unqualified family members, retaining only the exact bound nonpackable Sample.Contracts application artifact. |
| R-B6 added runtime input | medium | patch | Receipt readback checks existing filenames only, so a new Development configuration is unbound. Compare the current relevant output inventory with the completed receipt. |
| R-B7 nested lifecycle deadline | medium | defer | The independent G4 lifecycle qualification fact creates240s cancellation but its nested120s persistence methods do not accept that token. Preserve this separate test harness and record its deadline defect. |
| R-B8 removed failure diagnostic | medium | patch | The catalog helper reports a log path inside its deleted preparation directory. Preserve bounded compiler/tool diagnostics in the propagated failure before cleanup. |
| R-B9 kill/exit race | medium | patch | Both process helpers call Kill outside cleanup confirmation, so termination failures can lose the owned-process marker. Preserve cancellation and mark any unconfirmed process termination. |
| R-B10 stale UI asset staging | medium | patch | Native source assets are copied into persistent staging without removing deleted producer files. Remove only obsolete generated staged assets from source and output inventories. |
| R-E1 concurrent ownership marker | medium | patch | The cited guard snippet was inaccurate: actual code uses WriteAllText after nonexclusive directory creation. Use an exclusive CreateNew marker, never delete a competing owner's directory. |
| R-E2 historical acceptance cohort | low | reject | This finding proposes editing the build spec and is rejected under the review workflow. The user's latest-SDK instruction is independently recorded in the current checkpoint above. |
| R-G1 lifecycle verification unwired | medium | patch | Accepted verification-gap evidence: all74deterministic lifecycle controls pass manually but ordinary CI does not invoke them. Add a blocking ordinary CI invocation with its direct Builds dependency. |
| R-G2 private configuration verification unwired | medium | patch | Accepted verification-gap evidence: the replacement Platform suite does not execute retained private catalog configuration/cleanup tests. Execute the Docker-free classes in the4.1package check using root-declared owner manifests. |
| R-G3 local project inventory | medium | patch | scripts/test.ps1 omits the declared Platform.LifecycleTests project, causing every lane to fail inventory admission. Add it to the runner-owned project inventory. |


### Current package and catalog evidence — 2026-11-09

The [durable verification packet](tests/published-catalog-sdk-adoption-2026-11-09/verification.json) binds the official SDK archives, exact commands/logs, initial static results, focused controls, browser gate and local package consumers. SDK4.1.0 remains the latest stable version on a fresh direct nuget.org recheck. All14official archives bind source`dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a`.

Post-review checks: zero-warning Release compilation,33/33focused actual HTTP/catalog/authentication/cleanup controls,17/17receipt cases, all68browser cases, all11local packages and strict package validation. The actual client consumer executes declaration/adapter inventory; the portal consumer restores/builds. The earlier full static baseline remains6,403passes/14existing skips at its recorded source; these focused results do not renew that baseline to later externally committed sources. The affected CI controls passed153cases, with their single receipt-entrypoint failure rerun successfully after correcting the expanded fixture inventory. Independent lifecycle/controller and public/private evidence controls also pass; their simulation results supply no hosted-runtime credit.

Two actual concurrent attempts failed before domain assertions. The first exposed Tenants'McpCli.Abstractions2.0.0 beside the root's2.0.1. Preparation now propagates the exact rootMcpCliVersion into every selected host and launch; actual archives/PEs and negative version/type controls remain enforced. The second exposed a new real Platform helper besideProgram.cs, omitted by the old copy step. The wrapper now copies and hashes both actual current source files, preserving the producer's unchanged qualification policy. Both attempts retain their actual failure and exact cleanup receipts: no owned processes, containers, networks or private roots remain. No runtime success is claimed.

Fresh preparation completes all10artifacts with SDK4.1.0/Tenants7.0.5/Memories3.0.25/FrontComposer4.7.6/McpCli2.0.1. All10receipts validate. A resource-free invocation of the actual private wrapper then compiles and executes the actual current Platform tool against the actual prepared Parties/Tenants/Sample owners, successfully generating its canonical catalog. Actual tool inputs, public SDK identities, ownerPE hashes and generated receipt are retained in the packet. The probe's owned build directory was removed. This proves the repaired composition boundary, not live activation, persisted effects or full topology acceptance.

Other workspace sessions committed and rebuilt shared inputs during verification; a readback correctly refused drift after those builds. Their changes and commits are preserved. The user has been asked whether those build/sync jobs have finished so the required live producer freeze can be established. Do not start a new live run against changing prepared outputs.

Normal module package preparation with the corrected4.45.0tool now reachesHXW011: selected Platform`e39a9a2587ec150d550510763ab69db305d13fdb` has no local SemVer release tag. Historical public tuple evidence cannot qualify that source. Final concurrent/whole-assembly, normal package preparation, exact-source hostedCI/RC and release/publication gates remain outstanding. Status remainsin-review; no done or release qualification is asserted.


### Second resumed review triage — 2026-11-09

All three independent review layers completed before classification. The review uses the full original baseline plus an explicit source supplement. Committed work and concurrent user edits are preserved; the SDK composition correction is being derived without reverting them.

| Finding | Verdict | Route | Evidence and action |
| --- | --- | --- | --- |
| S-B1 public tuple mismatch | high | patch | Current root and selected Platform both bind released Builds 27145e52ffab5623198ba5efdab5cdb71aa42d28; released Platform v1.8.0 binds e39a9a2587ec150d550510763ab69db305d13fdb. Owner tuple CI 42515643804, catalog CI 42515643848 and qualified publication 42517865717 succeed. Align both strict observers/readers to this exact published tuple; no native runtime credit follows from that identity correction. |
| S-B2 diagnostic tuple mismatch | medium | patch | Diagnostic root initialization selects Builds 27145e52 but its assertion requires historical 3c809618. Correct only the selected-checkout assertion; immutable shared workflow definitions remain independently pinned. |
| S-B3 GDPR selector regression | high | patch | Original published client requests include PartyDetailProjectionQueryActor for GetParty and four GDPR query wires. Enroll those exact owner selectors and test positive, foreign and wrong-owner selectors at the real SDK router. |
| S-B4 preparation configuration | medium | patch | Three initial property probes omit the caller's requested build configuration. Pass the actual configuration to each selection probe. |
| S-B5 generated UI staging | low | reject | The cited source wwwroot is explicitly reserved and ignored as generated native-asset staging, with no tracked authored files. Moving the staging design adds unrelated complexity; producer deletion behavior has an executed regression test. |
| S-B6 native launch files | medium | patch | Runtime input enumeration omits extensionless apphosts and native shared libraries. Bind additions, deletions and byte changes; exercise all three mutations before the same launch entrypoint. |
| S-B7 post-preparation rebuild | medium | bad_spec | ProjectResource launch may rebuild after preparation. The SDK composition correction must launch the exact prepared outputs or enforce evaluated build-input consistency; a caller freeze alone supplies no permanent invariant. |
| S-B8 restored archive authenticity | medium | patch | A mutable restored archive cannot authenticate itself. Complete preparation freshly downloads every consumed public-family archive over official HTTPS, compares exact restored bytes and binds that proof through launch. A repacked local archive with unchanged nuspec/runtime is refused. |
| S-B9 missing-outcome clock rollback | medium | defer | Identity service's empty-stream path lacks the lower completion-time check present on its populated-source path. This independent identity-authority behavior predates SDK resumption; record it without altering domain semantics in this adoption. |
| S-B10 native withdrawal cancellation | medium | patch | The main real-service exercise omits Descriptor.RequestCancellation, unlike its anonymous companion. Link the existing token and preserve the 120-second deadline. |
| S-E1 missing owner observation | high | bad_spec | Actual 4.1.0 concurrent startup proves Admin refuses the manually supplied owner credentials without its independently observed ActorStateOwnersPath receipt. Use the SDK's public complete composition/observer; do not fabricate a receipt or bypass its boundary. |
| S-E2 public tuple mismatch | high | patch | Same independently verified reader/observer mismatch as S-B1; preserve exact source and release identities. |
| S-E3 diagnostic tuple mismatch | medium | patch | Same direct checkout assertion mismatch as S-B2. |
| S-E4 GDPR selector regression | high | patch | Same five original published query wires as S-B3; exact compatibility does not enroll arbitrary selectors. |
| S-G1 missing automated package topology | medium | patch | Accepted verification-gap evidence: ordinary CI exercises only model/configuration/cleanup for this private AppHost. Add a blocking current-package concurrent topology job using shared Dapr setup, exact preparation, persisted effects and refusal of skipped/empty execution. |
| S-G2 computed identity wire mismatch | high | patch | Published 4.1.0 router rejects client ProjectionType party against the two computed declarations' null selector. Omit that selector and pass actual serialized current/historical requests through the real catalog router before the producer. |

The fresh ordinary package preparation now succeeds after fetching the genuine public v1.8.0 tag without changing the selected checkout. Earlier HXW011 evidence remains historical. Exact command output is retained in the resumed evidence packet.

Fresh runtime preparation completed all ten artifacts with zero-warning builds. The actual concurrent method then failed after 326.015 seconds before domain assertions: Admin reports that the selected owner coordinates/private credential handoff changed, and its missing independent owner observation prevents catalog activation. Exact post-run inventory confirms no session containers, networks, processes or private directory remain. This failure grants no runtime acceptance.

Current focused evidence: 19 receipt controls pass, 48 public/private evidence controls pass, 19 catalog cases pass, and 345 gateway/identity cases pass before the last historical-wire case was added. The staging regression passes under genuine package-mapping environment imports. A later Client build hit MSB3026 on a concurrently used AdminPortal output; the independent build's source and outputs are preserved. Stable producer/build custody is required before another shared-output runtime qualification.


### Spec change log — current published composition

The second resumed review proved that the manually configured Admin owner handoff cannot satisfy SDK 4.1.0's independently observed receipt boundary. Preserve every native/default/publish decision, actual authentication, prepared-archive/PE proof, complete concurrent assertions and exact cleanup. Replace only the explicit package test profile's catalog authority composition with the SDK's public composition/observer, finalized after the fixture's fresh owned Redis and endpoint overrides and before BuildAsync. Never fabricate owner observations or use unpublished/internal SDK seams. Also preserve actual prepared execution after the catalog is bound; source edits must not trigger an unverified rebuild at launch.


### Final patch review triage — 2026-11-09

All three final patch review layers completed before classification. Findings are retained individually; independently overlapping reports share the same correction without widening trust policy.

| Finding | Verdict | Route | Evidence and action |
| --- | --- | --- | --- |
| F-B1 unenrolled finalization controls | medium | patch | The captured diff omitted the new class from offline CI. Enroll all 23 final model cases; configuration/cleanup/finalization now require at least 40 cases with skip refusal. |
| F-B2 versioned native files | medium | patch | ELF names such as libnative.so.1 were outside suffix matching. Bind .so.* names and exercise add/change/remove mutations. |
| F-B3 portal rebuild after preparation | medium | patch | parties-ui auto-starts and was outside the prepared set. Prepare and directly launch its exact Client/Contracts/SignalR graph without enrolling it as a catalog receiver; total preparation becomes eleven artifacts. |
| F-B4 business Redis binding | false | reject | The only caller passes PartiesTestRedis.Configure's exact resource after it has rebound every state/pubsub component to that same new container and before finalization. The actual fixture also independently verifies loaded component routing and persisted keys on that backend. No caller selecting retained business state at this location was demonstrated. |
| F-B5 pre-Build cleanup fault injection | maybe-false | defer | The actual concurrent case exercises complete finalization; existing owned-preparation failures exercise cleanup and CompleteOwnedTestModelAsync explicitly disposes failed composition. A fault injected after successful composition but before BuildAsync completion is still needed to establish any unowned-directory outcome. Record medium unverified; no definite cleanup defect is claimed. |
| F-B6 missing manifest boundaries | medium | patch | Add direct empty, oversized and symlink refusal cases to the existing manifest theory. |
| F-B7 transient archive failures | low | patch | Fresh downloads now retry three bounded transient attempts and never accept cached bytes when all attempts fail. Actual downloader controls cover refresh, retry and refusal. |
| F-B8 older identity router | medium | patch | The retained 4.0.4 cohort now executes the same actual catalog-enforced current/historical wire path. Its zero-warning isolated build and all 322 identity cases pass without skips; enroll this check in that existing offline lane. |
| F-B9 finalization phase attribution | low | patch | Set finalize-package-catalog before the bounded preparation call, preserving the startup deadline. |
| F-B10 preparation instructions | low | patch | Current instructions describe ten runtime projects plus the adapter and exact PE execution; historical receipts retain their original counts. |
| F-E1 incompatible auxiliary authority role | high | patch | Published SDK caller registration refuses aux-client plus AuthorityRole. Preserve the installed strict receiver-free profile, verify the actual SDK dedicated labelled/name-prefixed component, then remove only the competing profile selector. Genuine SDK positive and malformed/shared/foreign authority controls pass. |
| F-E2 mutable preparation substitution | high | patch | Successful re-preparation previously validated against the latest manifest while retaining an older catalog. Every new-flow readback now requires structural equality with the originally selected receipt, including start and cached dashboard restart paths. |
| F-G1 downloader cache control | medium | patch | Accepted verification-gap evidence: the repacking control replaced public_archive itself. New controls execute that production function with only urllib transport patched, proving stale-cache replacement and fail-closed retry exhaustion. |
| F-G2 synthetic compilation feeds | medium | patch | Fresh official downloads cannot retrieve synthetic 9.9.9 archives. Preserve real compilation and supply independent expected fixture archives at the HTTPS transport boundary, including deliberately stale public caches that must be replaced. |

The published SDK composition and final launch changes compile with zero warnings/errors in an isolated harness, and all 23 direct model controls pass without skips. The genuine 4.0.4 router additionally passes 322 identity cases. A complete combined source build, refreshed eleven-artifact preparation and actual concurrent/full runtime qualification remain separate required gates.


### Current continuation — SDK 4.2.0 published during qualification

At 2026-11-09 14:16 UTC, fresh direct official nuget.org indexes report EventStore 4.2.0 as the latest stable SDK. The user's latest-SDK instruction now selects that version; prior 4.1.0 results remain historical. Independent official Contracts, Client, Aspire and Server archives embed source `aa16227bd07f0564a3026d619a5abd973e02656f`. The actual public Aspire API diff is additive (runtime profile and SQLite startup-ordering APIs); existing composition signatures remain available. Exact 4.2.0 capability fences and an updated current offline cohort preserve 4.0.4 compatibility and refusal of unqualified versions. Shared catalog selection must follow the supported sealed package-mode policy, rather than overriding its validation.

The last 4.1.0 full standalone build passed with zero warnings/errors. Its sixteen static suites produced 6,442 passes, two CI-test failures and fourteen existing skips; both failures were corrected, and the entire CI suite then passed 617/617 without skips. Actual current/historical gateway/identity cases passed 346/346; the older SDK 4.0.4 router independently passed 322/322. All eleven packed Parties artifacts pass strict package validation and client/portal consumer checks. These results do not qualify the newly published 4.2.0.

The last configured browser gate passed all 68 cases with one retry. An unchanged focused repeat produced two passes and one failure. Its trace shows the export click while the Blazor connection was still starting, with no ready-export status; the existing single-export/domain assertions remain unchanged. This independent startup timing issue is recorded separately, without treating a retry as evidence of deterministic behavior.

Two further actual 4.1.0 concurrent attempts failed before domain assertions. Public composition first collided with the pre-created gateway APP token parameter; deferred fixture token assignment now preserves the SDK's idempotent binding, and all thirty affected model checks pass. The next attempt reaches the SDK owner-read observer but refuses an allocated proxied HTTP listener differing from the selected fixed session Dapr port. The guard is preserved; the fix must bind the actual owned listener to that reserved port before allocation. Both attempts independently confirm no session processes, containers, networks, private roots or new SDK catalog directories remain.


### Supported 4.2.0 prerequisite and runtime checkpoint

All fourteen SDK 4.2.0 official archives authenticate source `aa16227bd07f0564a3026d619a5abd973e02656f`. An isolated complete standalone Release build passes with zero warnings/errors. Actual 4.2.0 controls pass: catalog 20, identity client 178, gateway/identity 346, finite capability 41, and configuration/cleanup/finalization/topology model 53, without skips. The current workflow and matched control-plane profile controls pass 94/94. The new public SDK guards retain exact version admission and negative controls.

The original published Tenants 7.0.5 archive works with SDK 4.2.0: actual declaration/adapter discovery passes 23 routes, 23 operations and 12 adapters, with eight independent official archives and output PEs authenticated. Its source build fences are extended only to exact 4.2.0, with 224 compatibility controls, seven metadata controls and two pack controls passing. Its released artifact provenance remains `d88aef6b52d7e8a2e2d81132b1d76b7e1d1f0f35`; no replacement release or provenance relabeling is inferred.

Sealed module package mode intentionally rejects a consumer override of the producer catalog (HXW012). The supported route therefore updates the owning Builds catalog and coordinates the Platform pin after genuine publication. All intervening upstream changes are retained. Builds source commit `7564c965337f46a1c3b6244476b13b0fa85dd653` selects SDK 4.2.0, preserves the historical 4.1.0 authority, and adds genuine 4.2.0 authority and technical receiver enrollment. Its 390 focused/manifest/evidence controls pass without skips and Debug compilation has zero warnings/errors; its revision-bound audit is generated only after this real commit. No owner push or producer publication is claimed at this checkpoint. Platform's actual isolated 4.2.0 catalog-builder qualification also passes with the genuine released sample and unchanged legacy controls.

All eleven 4.2.0 runtime artifacts now prepare successfully with fresh official archive authentication, and every completed receipt rereads successfully. The actual concurrent run uses these exact PEs, the corrected fixed owner listener, a separately downloaded and SHA-256-verified Dapr 1.19.1 executable, and the matching immutable 1.19.1 control-plane image. The bootstrap's original default remains 1.18.7; the current CI runtime job explicitly selects the catalog's exact profile and refuses unqualified profiles. The run is ongoing; persisted-state, whole-assembly, hosted CI/RC and publication acceptance remain outstanding.

The [new continuation packet](tests/published-catalog-sdk-adoption-2026-11-09/continuation-sdk420-1434/verification.json) preserves these new 4.2.0 archive/build/test/preparation receipts and the previous 4.1.0 failures and results separately. It grants no live or publication acceptance.


### Actual SDK 4.2.0 concurrent success

The complete real concurrent method passed on 2026-11-09 at 14:48:59 UTC using SDK 4.2.0, authenticated published peers, exact prepared PEs, and the verified Dapr 1.19.1 native runtime/control plane. Both sessions independently verify Counter streams of two and five events, UI-owned authenticated calls, actual Greeting and rejection results, receiver credential refusals, cross-session refusal, persisted Parties events/projections, and survival after peer disposal. Status-read calls now supply one explicit authorized tenant header only on those reads. Actual missing, foreign and conflicting scopes remain forbidden. This corrects the earlier polling failure without relaxing any server policy or retrying a domain write.

Independent post-run enumeration confirms both session private roots are absent, no session processes/containers/networks remain, and no new SDK catalog directories remain. The prior unrelated SDK directory is preserved. The new browser gate also passes all 68 cases without retries. Sanitized native report, exact input/result bindings, official archives, preparation and cleanup receipts are retained in the continuation packet; raw resource logs remain private. The complete integration assembly and exact-source producer/consumer publication gates remain separate outstanding acceptance requirements.


### Complete integration assembly checkpoint — SDK 4.2.0

The complete integration assembly now passes 263 cases with zero failures and 15 existing explicitly deferred or opt-in skips. No skipped case receives runtime acceptance. The preceding whole run's five failures were two wrapper/child producer-byte mismatches and three controlled command-double profile leaks. Building the child from the same prepared producer and isolating command doubles to their declared original profile preserves both exact-byte refusal and real runtime profile validation. All 22 affected harness controls pass under the real 1.19.1 outer environment, and all eleven runtime receipts remain unchanged.

The successful whole run independently leaves no processes, containers, networks or private roots for any of its eight observed real topology sessions, and no new SDK catalog directories. Earlier failed command-double roots contained only verified synthetic credentials; normal cleanup-only plus validated owned-root deletion removed those three test artifacts without touching any real or unrelated resources.

Tenants prerequisite PR 68 at `5293ab9dca49b6298f1e3396367627009967cbfb` passes all six exact-source hosted workflows. Its tracked CONTRIBUTING.md requires one approval and the author cannot self-approve; the user has been asked to arrange that approval or explicitly waive this documentation rule. No merge bypass is performed. Builds exact-source push CI 42638222177 passes for `a8a87160f2708322d5520742b9a0c3f93d23db78`; authorized manual release 42641950035 is publishing through its configured production gate. Genuine new release/package coordinates are still pending at this checkpoint.

Platform's next exact SDK4.2 reader and private-source reference cohorts preserve the separately measured public Parties3.1 image's SDK4.1 provenance. Its public SDK4.2 executable qualification passes all 22 cases, and 91 artifact controls pass with the old SDK4.1 dictionaries and all 349 legacy obligations preserved. This permits a compatibility source checkpoint in Parties before final default/pin adoption: the future Platform private build uses its own sealed SDK4.2 mapping, while that interim Parties source may retain its older default/catalog pins. No unqualified source is described as published.


### Compatibility checkpoint review triage — SDK 4.2.0

All three independent layers completed against the full original-baseline diff before classification. The edge-case layer returned no findings. Each remaining finding receives an individual verdict below; earlier receipts and user changes remain preserved.

| Finding | Verdict | Route | Evidence and action |
| --- | --- | --- | --- |
| C-B1 external static content unbound | medium | patch | runtime_inputs binds the static-web-assets manifest but omits its external ContentRoots. The executed probe changes an actual referenced asset without changing any bound receipt input. Include those actual roots and their inventories in preparation/readback so additions, removals and changes fail before launch. |
| C-B2 legacy preparation substitution | medium | patch | PrepareCoreAsync rereads every receipt but discards it, unlike the newer composition path. Compare the reread to the originally selected object using the existing structural-equality guard, retaining the older SDK lane. |
| C-B3 unenrolled finalization controls | medium | patch | Carried F-B1: the interim checkpoint intentionally retained the older workflow, which still omits the new model class. Enroll the class and enforce executed-case/skip counts before committing that checkpoint. |
| C-B4 missing actual 4.2 compile lane | medium | patch | The interim source admits exact 4.2 but its retained offline workflow compiles only 4.0.4 and 4.1. Use the already implemented explicit 4.2 offline lane and its matched workflow controls while retaining the checkpoint's older default and source pins. |
| C-B5 stale completion after preflight refusal | medium | patch | The preparation script removes prepared.json only after project checks and three MSBuild probes; any earlier refusal leaves a prior completed manifest. Invalidate the selected packaged-profile manifest immediately after valid options/profile-path selection, before preflight, and exercise both missing-input and failed-probe refusals. |
| C-B6 loaded/disk identity gap | medium | defer | NativeAssemblyObservations records the loaded module MVID but hashes assembly.Location without comparing the disk PE MVID. Replacing that disk file can misattribute bytes to the already loaded module. This separate existing G4 observation contract predates the current SDK composition; record a direct PE-MVID binding follow-up. |
| C-B7 process-id reuse interval | medium | defer | signal_tool_processes checks /proc birth and then calls os.kill by numeric PID. Exit/reuse between those operations can target an unrelated process. This independent existing G4 lifecycle controller needs a stable process handle such as pidfd; its fake controls cannot prove atomic identity. |
| C-B8 unbounded diagnostic capture | medium | patch | RunAsync reads both streams completely into memory before retaining a 4096-character exception tail. A noisy compiler can exhaust the test process within the four-minute deadline. Drain streams with bounded retained diagnostics and exercise output larger than the bound. |
| C-B9 surviving build servers | medium | patch | The private reader dotnet build enables persistent MSBuild/compiler servers by default; waiting for its direct process does not end those owned services. Disable all build servers and shared compilation on this invocation, preserving the existing kill-tree/uncertain-cleanup behavior. The actual catalog-builder source starts no subprocesses. |
| C-B10 shared native asset staging | medium | patch | StageNativeAdminUiAssets copies/deletes the same source-project wwwroot for every artifacts path. Two independently prepared runs therefore mutate shared staging and receipts. Stage into each evaluated intermediate-output root, keep proper SDK static-asset metadata, and exercise two isolated preparations without shared source writes. |
| C-G1 actual finalization launch wiring unverified | medium | patch | Accepted verification-gap evidence: existing model tests install BindPreparedLaunch themselves; they do not detect omission from CompleteCoreAsync, especially for parties-ui. Exercise actual finalization with prepared inputs and assert all actual runtime projects select dotnet exec plus their exact receipt target; subsequent selected-input drift must refuse start/restart. |

These preparation corrections retain exact public archive authentication, selected receipt equality, all eleven artifacts, public SDK composition, old SDK defaults in the interim checkpoint, real authentication and complete persisted-state bodies. No deferred observation/controller issue receives qualification credit.


### Owning-source publication progress and preserved upstream

Builds' first normal release run 42641950035 failed on a GitHub HTTP500 notes push after creating immutable v4.46.0 at a8a87160f2708322d5520742b9a0c3f93d23db78, before publication. That tag remains unchanged and supplies no public-package acceptance. A narrowly scoped, tested notes-push retry is committed at 8039932ca0cbd00cff5944d4c456ebfe13e4743b. Subsequent upstream fixes are retained; current source 9b9e03f69b77e4a8e64e4185a1df1cb4bc315ba4 awaits its exact push CI42650358536 before normal successor release. Canonical catalog blob a9472e6159b0b7627b4de543651f9aa1912398ab has SHA256957818ed7df1b29b2299644afb06987393f617c5b87ff8551b9b95b52a23034d. No predicted release version or locally qualified tool hash is substituted for official publication.

Parties upstream ee806fd0e82bdf6eec3a8c1f671427080dbed2ac was independently fetched, inspected and fast-forwarded without overwriting any working edits. Its focused lifecycle campaign retains full-matrix distinction and redacted failures. All106 affected deterministic lifecycle controls pass. This local controller evidence supplies no real lifecycle campaign or SDK runtime acceptance. Fresh direct official Contracts/Aspire indexes still select stable4.2.0 at16:11UTC.


### Genuine producer release refusal and correction

Current-source CI42650358536 passed all seven Builds lanes. Existing legitimate release42653985043 received its configured production approval, passed its exact-source recheck, then failed actual4.46.1 qualification before any new tag: the positive packaged module command returned3/HXW001 rather than the required2/HXR003. The notes-push retry proxy depended on a custom environment variable that the product correctly removes from child processes. Our separately queued duplicate42654023394 was cancelled; the earlier existing run was preserved. Neither failed release supplies publication acceptance.

The four-file correction stores the original Git executable in the action's private directory, preserving the product environment allowlist and every expected outcome. Eight executable controls and the exact sanitized-child CLI reproduction pass. Full actual package qualification under the corrected proxy also passes, including source/package parity, evidence/refusal controls and actual packaged-seed HTTP/receiver checks. Its local dirty-source inventory truthfully remains releaseEligible=false. Exact pinned commitlint passes; clean owning commit32d2a0685663b5b47115b2c0972e4819c9a36b70 is normally pushed and awaits exact-source CI42657538253 and genuine publication. Historical v4.46.0 remains immutable.


### Reviewed compatibility source committed

All three compatibility review layers completed and all nine current preparation/verification corrections are implemented; the two independent observation/controller issues remain deferred above. The final common patch additionally removes two unused fixture collection definitions exposed by the selected catalog's newer xUnit analyzer, preserving the actual Health collection. Both standalone SDK4.1/4.2 Release builds pass with zero warnings/errors. Complete audit-enabled checkpoint CI passes633/633 and configuration/cleanup/actual-finalization controls pass51/51, with no skips. A partial local restore initially lacked required audited consumer graphs; the complete standalone restore corrects those verification inputs without changing assertions.

Clean checkpoint a69fa1794555acd66709ea382290eb3d5297f03a is normally pushed from an independent clone, preserving the original workspace's working edits and HEAD. Its34reviewed files retain SDK4.1 and all ten original dependency pins while qualifying exact4.2 offline and binding all ten runtime launches. Exact full pinned commitlint passes; exact-source main CI42662230480 is running. This source checkpoint permits the pending Platform private-source binding; it is not final default4.2/published-tuple adoption.

The refreshed original-workspace SDK4.2 static run passes6,156cases with14existing skips, plus all51catalog configuration/cleanup/model cases. Fresh preparation independently validates eleven receipts and1,043runtime inputs; test/producer UI PEs match. Those scratch receipts are retained before the complete standalone restore/build; final runtime preparation and actual whole-assembly acceptance must bind the resulting final outputs. Browser verification is running before that final freeze.


### Post-review SDK4.2 full runtime acceptance

The real metadata refresh exposed broad output cleanup removing SDK-generated app.css.gz after preparation. Both pre-launch guards refused before creating any live resources. Follow-up ebe1739d70a4e76447938b0bb8c53db508bf6019 limits removals to obsolete native originals and their derived output copies. Its actual SDK control fails against the old target and passes the correction, preserving completed manifest/asset hashes, current compressed files, unrelated SDK output and independent artifact roots. Exact pinned commitlint passes; normal main push and CI42665293643 are recorded.

After this correction, standalone SDK4.2 builds with zero warnings/errors and all633CI tests pass without skips. Final preparation plus test-entry metadata refresh preserves every one of11receipts and equal selected/copied UI PEs. The full integration assembly passes265cases,15existing explicit deferred/opt-in skips, zero failures/errors; the complete concurrent persisted-state/authentication/isolation/disposal body executes successfully. Independent enumeration confirms all eight real session roots, processes, containers and networks are absent, no new SDK directories remain, and all11receipts still reread unchanged after the run. No skipped test receives runtime acceptance.

The [post-review packet](tests/published-catalog-sdk-adoption-2026-11-09/continuation-sdk420-1434/postreview-sdk420-ebe1739d/verification.json) binds sanitized native results, exact source bytes, full static6156/14existing skips, model51, browser68without retries, preparation, post-run readbacks and cleanup. Raw resource logs remain private. The original workspace branch now includes ebe1739d through index-only alignment; all8774working file bytes, modes and links were independently preserved. Current final-default/published-tuple edits remain uncommitted and status remainsin-review.

Builds' genuine v4.47.0 tag and notes are public at qualified source5ec1779040606be89306129a7d2b245258c9acd2 after exact-source CI42661065183 success and authorized production approval. Release42664680297 is still publishing; Evidence.Cli4.47.0 appears officially, while complete Module.Cli/archive/inventory readback remains pending. Partial publication grants no complete tuple authority. TenantsPR68 still has no independent review or user waiver.


### Compatibility hosted CI and refreshed package consumers

All four exact-source workflows for ebe1739d70a4e76447938b0bb8c53db508bf6019 succeed: complete main CI42665293643, CodeQL42665293365, Commitlint42665293296 and the workflow named Release Candidate Gate42665292442. Main CI includes the actual4.0.4 and4.2 offline package lanes, runner-owned package integration, UI accessibility and existing declaration/controller/publisher gates. This qualifies the clean compatibility source, while its default intentionally remains4.1.

A fresh separate SDK4.2 standalone restore/build passes with NuGet audit enabled and zero warnings/errors; all11local packages strictly validate. Actual isolated client declaration/adapter execution and portal restore/build pass. The first consumer invocation inherited the packaging artifacts location and correctly refused its missing default assets file; rerunning with normal isolated consumer environment succeeds. Neither this local feed nor the compatibility policy gate substitutes for final4.2 release/public NuGet readback.


### Genuine Builds4.47 publication accepted

Normal release42664680297 completes successfully for source5ec1779040606be89306129a7d2b245258c9acd2 after exact-source CI42661065183. The actual upstream feature makes this genuine release v4.47.0; earlier predicted4.46.1 was never enrolled. Both independently downloaded official NuGet archives pass signature verification, exact nuspec source/version, actual tag/CI/release attempt bindings and cross-package Tooling byte agreement. The published inventory SHA256 is806a0fdf6fc08ea0b8a398756b04af43eb196a81d22ebbbff142dcbca9abcc0b; signed Module.Cli archive1eea9b282d1d247e1f6e9e2213c5969f7895df44105e6900c873b586c5c4f905, Tooling DLL35d0eb2b0f32ad5e3b1e4353f088800e76084b99e60f46838e6e497105f64f01, Module.AppHost DLL6aae774cfcf41e0488cf7b2517d67897f1726c68674219fea164d6998ad60ebe. Canonical catalog bloba9472e6159b0b7627b4de543651f9aa1912398ab has SHA256957818ed7df1b29b2299644afb06987393f617c5b87ff8551b9b95b52a23034d.

The durable producer readback is preserved in the post-review packet. Platform is binding this genuine tuple and the clean Partiesebe1739d source before its own same-code349/current456 qualification and publication. Old image/SDK4.1 and Works1.18.7 receipts remain unchanged. This producer acceptance does not complete final Parties4.2 source/pin/CI/RC/publication requirements.


### Platform committed current/legacy qualification and Actions recovery

Platform current081178aa5cc01f5dc736ba0d1980aa80fedc6fe1 records genuine Builds4.47/5ec1779 and clean Partiesebe1739d. Real same-code legacy6749955731a13749a889a69b289881daff585613 is committed and reachable on its qualification branch, retaining its original349profile, producer/provenance/Parties source. The rebind verifier permits exactly eight metadata differences; no historical source or image provenance is relabeled.

Independent clean local clones build in Release with zero warnings/errors and pass456/456current controls across30groups and349/349legacy controls across16groups, with zero failures/skips and unchanged clean source and test/dependency output identities. Exact commands, signed tool closures and every group XML are retained. Initial two enrollment failures arose from external harness output placement; coherent reruns put outputs under the ignored artifacts directory, preserve all tests/guards and pass. These local results grant no hosted, native or publication credit.

Hosted tuple42671091679 and catalog42671091693 first failed with documented GitHub internal orchestration errors and unscheduled jobs. Actual actionlint1.7.12 passes. GitHub declared incidenttp8cghg7tlc4 for Actions/Checks, later reporting mitigation/monitoring; one normal recovery attempt3is executing against unchanged081178aa. All four tuple jobs and six SDK rows must pass together on that actual latest attempt before normal source publication. Historical failures and official incident evidence remain retained. TenantsPR68stillawaits independent approval or explicit user waiver.


### Genuine published owner tuple and bounded Tenants source selection

Platform v1.9.0 is actually published at081178aa5cc01f5dc736ba0d1980aa80fedc6fe1 by normal publisher42680269970. Exact tuple42671091679 attempt3 passes all four jobs,456current and349same-code legacy controls, with clean unchanged sources/outputs. Catalog42671091693 attempt3 passes all six SDK rows. The official release/tag and published source-release receipt were independently read back; the durable post-review packet now retains that tuple and both actual hosted results. Builds v4.47.0/5ec1779040606be89306129a7d2b245258c9acd2 is independently verified with its official signed archives and published inventory.

The outstanding Tenants documentation rule applies to merging PR68. Root consumption can instead select its exact already pushed and CI-qualified5293ab9dca49b6298f1e3396367627009967cbfb qualification branch, consistent with this spec's existing separate-ref source-selection route. No PR merge, approval claim or waiver is performed. Public Tenants7.0.5 remains unchanged and independently qualified against actual SDK4.2 archives. The earlier question remains relevant only if an owner PR merge is later requested; it does not block this bounded source selection.

Final source-selection records now preserve the previous November9 source observation as history and separately bind the three actual qualified advances. Native current-owner evidence requires source packages3.1.0 and SDK4.2.0; it refuses the older3.0.0 private package cohort. Public readers and the native image observer bind the actual new Platform/Builds tuple while preserving the immutable released image's measured SDK4.1 bytes. Final root source CI, runtime gate, RC and eleven-package publication still remain required; status remainsin-review.


### Final source verification and legacy CI input correction

Final sourceb78247c3ea7975520f354484f24e31e30e798e1b is pushed after exact pinned commitlint21.2.4. Genuine published tool4.47.0 prepare-build succeeds with zero diagnostics, audited standalone Release restore/build succeeds with zero warnings/errors, all633CI controls and304Fitness tests pass, and both strict evidence-reader suites pass17/17. The three initial Fitness failures were stale current documentation and bunit catalog assertions; their corrections preserve every rule and historical row. A fresh official NuGet index still selects latest stable SDK4.2.0 at19:13UTC. Index-only original-workspace alignment independently preserves all8820working file bytes, modes and links; the checkout is clean atb78247c3 before the following bounded CI correction.

Hosted RC42682993316 passes. Initial main CI42682994160 exposes one missing input in its newly expanded4.0.4lane: the actual gateway test host needs two root EventStore appsettings files, but this lane initializes only Builds. The correction adds that root checkout, preserving frozen4.0.4/5.11.2/3.0.11package inputs, source-disabled selectors and the actual322router minimum. A fresh minimal checkout containing only Builds+EventStore builds with zero warnings/errors and passes the exact322tests; all restored SDK assets remain4.0.4. The bounded workflow test passes. Other hosted4.2andUI lanes pass; runtime/mainbuild are still executing. No release is dispatched from this incomplete source. Sanitized local test outcomes, raw-result hashes and exact commands are retained in tests/published-catalog-sdk-adoption-2026-11-09/final-source-b78247c3. Final all-job sourceCI, RC, release and eleven-package independent publication readback remain required; status staysin-review.


### Hosted package-runner prerequisite and coherent CI workflow pin

Actual first final-source runtime job129382234919 passes the authenticated concurrent test in279.862seconds, with1executed/1passed/0skipped oraborted; its downloaded TRX hash is4e3a05c8440f79fe3cfb77f114383cd99193f86226f900a3caceb65e88c0b090. Full hosted unit tests, SDK4.2offline, UI, publisher/controller/declaration checks, RC, commitlint and CodeQL pass atb78247c3. The ordinary package runner stops BEFORE owner allocation with actualHXR012: older CI workflow3c8096 installs workflow-owned DaprCLI1.18.2/runtime1.18.7, while actual moduletool4.47 requires1.19.1/1.19.1. Its execution/cleanup/invocation diagnostics remain preserved; no native acceptance or release authority is inferred from that prerequisite refusal.

The automatic and manual CI callers now select published Builds5ec1779040606be89306129a7d2b245258c9acd2. The actual old-to-new workflow diff was inspected: its existing ungoverned module path checks out the immutable workflow SHA, reads matching catalog versions and archive digests, retains strict isolated tests before provisioning, and uses the bounded40minuteintegration step with cancellation forwarding and4minutefinalizer. The root callers already supply every newly required manifest/profile/unit input. Current docs and the exactBuildsCiSha assertion move with those two caller lines. Release execution11a3a79 and independent validation pins remain unchanged. This corrects the real prerequisite mismatch without weakening or bypassing any gate. All gates must still pass on the final pushed source before authorized release/publication.


### Exact current native report names

CI42686394068 at e2a93bb365af5f2ea86a2a8079747ac84affdffb passes every execution job: full main unit/package integration, both SDK lanes, UI, concurrent authenticated topology, publisher/controller/declaration checks; independent RC/commitlint/CodeQL also pass. The last wrapper129404657570 refuses the otherwise successful28-case native report because pinned xunit4.0.3 MTP emits literal backslash+quote bytes5c22 in19inline-string display names, while its historical baseline stores plain quotes. Actual SDK4.2native reader bodies, separately measured public SDK4.1image bodies, persisted/authenticated behavior, exact source/job ownership and complete cleanup all satisfy the unchanged remaining predicates.

The correction retains canonical CASES and derives an exact CURRENT_TRX_CASES set for the pinned current reporter. All28unique cases, one occurrence each, unchanged counters and every other strict predicate remain required. An independent literal28-name reporter snapshot and missing/duplicate/wrong-parameter/unescaped/double-escaped/case-change controls pass; public18/18andprivate17/17reader suites pass. The parent independently executes the corrected unmodified reader against the actual downloaded native artifacts: all strict checks pass with28cases, without modifying captured bytes or granting new-source acceptance. Sanitized verification and raw input hashes are retained under tests/published-catalog-sdk-adoption-2026-11-09/final-source-e2a93bb3; raw native logs remain private. Normal exact-source CI must still succeed for the committed correction before release. Status remainsin-review.


### Final acceptance and publication closure

Exact-source CI42690523916 passes all eleven jobs at84e3df796ac977041a21ec7bd8e5789bdc44241a; RC42690522780, commitlint42690523452 and CodeQL42690523464 pass independently. Downloaded hosted reports prove6440unit passes/14existing skips across15declared projects,28ordinary native package cases and1actual concurrent topology case with no skipped/aborted native results. Strict source/SDK/image/persistence/ownership/cleanup readbacks pass. Earlier local standalone0warnings/errors,633CI controls,304Fitness,51model controls,68browser cases without retries,265whole-integration passes and complete cleanup of8owned sessions remain bound to their own captured source/input receipts.

Authorized Release42693002067 publishes immutablev3.1.1 at that source. Configured production gates receive normal eligible-reviewer approvals; no policy changes or admin bypass occur. NuGet accepts all11uploads at20:32UTC but asynchronous availability initially returns404, so first protected registry verification fails honestly and its evidence is retained. After all11official URLs become available, only the failed verification is requested for rerun; actual final attempt2 succeeds. The independent parent downloads all11official archives, verifies every NuGet signature and exact nuspec source/dependency cohort, reads all11managed PE source suffixes and SDK4.2assembly references, and compares their DLL bytes with all11actual GitHub release assets. Isolated consumers execute the real41route/41operation/28adapter inventory and build the portal. The parent independently revalidates the protected live-registry captures and all3images/6platforms, with identical NuGet archive hashes across independent and hosted downloads. No unsigned build archive is substituted for an official NuGet receipt.

All required adoption acceptance is achieved. Final artifact inventory and acceptance are retained in tests/published-catalog-sdk-adoption-2026-11-09/completed-sdk420-v3.1.1. Raw native logs remain private, historical failures/receipts remain preserved, and existing triaged deferrals are not silently closed.
