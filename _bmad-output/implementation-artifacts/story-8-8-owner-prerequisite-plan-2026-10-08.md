# Story 8.8 owner prerequisite tasks — 2026-10-08

The user selected the recommended path with `do recommended`: keep Story 8.8 a
Parties consumer migration, prepare separate upstream tasks, and prove existing
key/cursor/payload compatibility. This packet completes the authorized planning.
It does not activate DW-98 or certify producer delivery, parity or release readiness.

The Parties spec remains draft and the story remains blocked on Story 8.7 and
per-row gates. The human/reviewer gate must record the continuity decision in the
matrix I20 approval table before either consumer activation relies on it; both
8.7 and 8.8 inherit the same decision. The executor does not manufacture that row.
No invalidation, erasure-policy change or resequencing is selected.

## Execution order

First finish the existing EventStore publication handoff. Advance the existing
8.7/G5 owner work and collect proof for already-delivered G6/G8-A APIs. Prepare the
small missing client, authentication, MCP and AdminPortal tasks independently in
their owner repositories. The integrated topology handoff follows those inputs;
Parties adoption follows 8.7 closure and the relevant exact-identity acceptance.

Each item below is a separate owner delivery/validation task. It is not one giant
cross-repository implementation spec. Before execution, the owning repository
must adopt the item into its own workflow, name its accountable human/API/release
owner and record the exact consumption identity and producer proof. Existing
owner packets should be continued rather than duplicated. Repository-specific
baselines, root-only submodule rules and normal CI/release gates still apply.

## P1 — Publish the implemented sidecar security API

**Owner:** Hexalith.EventStore maintainers and release operator.

**Targets:** `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceSecurityExtensions.cs`,
`src/Hexalith.EventStore.ServiceDefaults/`, `.github/workflows/ci.yml`,
`.github/workflows/release.yml`, `tools/release-packages.json` in EventStore.

**Starting point:** Continue the existing
[completed local repair spec](spec-unblock-eventstore-api-publication.md) and
[validation receipt](tests/eventstore-ci-unblock-2026-10-08/README.md).
Its focused builds and tests passed, but local completion is not exact-current-main
full CI or publication. Preserve the in-progress owner checkout and integrate through
normal owner Git/release procedure; do not redo the fixes or weaken security gates.

**Deliver:** A coordinated published package family containing the existing public
`RequireEventStoreSidecarChannel<TBuilder>` API and matching ServiceDefaults
support. Use the actual release source's package manifest, successful exact-source
push CI and normal release workflow. The inspected manifest currently has 14
packages; the nine probed Parties dependencies are a consumer subset. Keep the
upstream validation-bypass input false. Return exact source/CI/release/tag/version
identities, public API/dependency inventory, package URLs/hashes and release evidence.
Do not guess a future package version.

**Proof:** Run `EndpointInventory_RequiresTheSidecarChannelPolicyOnSidecarRoutes`,
`UseEventStoreDomainService_EveryNonProbeEndpointRequiresCredentials` and
`SubscriptionRoute_RequiresTheAppChannelToken` before publication. Parties then
verifies the downloaded package API/provenance and coherent family, selects that
actual version through its own dependency change, restores and builds package-mode
Release, and runs its applicable lanes. Runtime credential acceptance remains P9.

**Current observation:** The fresh
[package probe](tests/story-8-8-planning-2026-10-08/package-probe.json), observed at
2026-10-08T08:05:46.883937+00:00, still finds 3.115.0 as latest for all nine indexes.
DomainService's assembly still lacks the required metadata name. This confirms
publication remains necessary; it does not reopen resolved G12.

## P2 — Finish the existing G5/Story 8.7 compatibility gate

**Owner:** EventStore payload-protection maintainers; Parties security and test owners.

**Targets:** Continue EventStore
`_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`,
its owner Stories 8.3–8.11, and Parties
[spec 8.7](spec-8-7-data-protection-extraction.md).

**Deliver:** Existing-format and key-access continuity, a consumable approved shared
provider/backend, public/package enrollment and its owner closure packet. Preserve
v1/legacy readability, lawful erasure/redaction semantics and the existing local
rollback path. The new AppHost must not redefine the key backend or encryption
format during its orchestration handoff.

**Proof:** Representative synthetic protected events/snapshots remain readable after
provider/topology changes and restart; erased data stays erased. Prove old-path to
new-path reads and switch-back after writes, including every permitted new format.
If the predecessor cannot read a new format, introducing that format remains gated
until 8.7 supplies compatibility. Record exact identities, results and required
I8 export/processing-record/certificate/no-leak parity. No cursor reset or durable
payload invalidation is authorized by the selected plan.

## P3 — Validate delivered G6 freshness and G8-A security

**Owner:** EventStore Client/Contracts/Aspire API maintainers; Parties consumer test owner.

**Targets:** `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs`,
`src/Hexalith.EventStore.Client/Gateway/`,
`src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs`, and
`tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs`.

**Deliver:** Current selected-identity acceptance and compatibility receipts for the
existing lifecycle/provenance and audience-aware JWT APIs. Request additive code
only if the focused consumer truth table exposes an actual gap. Historical
producer approvals and the 26-case JWT receipt do not certify current consumers.

**Proof:** Parties command outcomes, paging, warning/reason mapping and all six
freshness states on 200/304 responses remain compatible. JWT proof covers local
and external authority/issuer/audiences, algorithm policy, valid/invalid tokens,
HTTPS metadata, secret-free publish output and switch-back. Keep local adapters
and security wiring until exact-identity consumer proof passes.

## P4 — Add independently selectable client transports

**Owner:** EventStore.Client API maintainer.

**Targets:** `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs`
and `tests/Hexalith.EventStore.Client.Tests/`; add focused per-type files if necessary.

**Deliver:** Minimal additive command/query/GDPR registration using the existing
transport implementation. Keep the current aggregate registration compatible.
Avoid a new plugin framework or a separate client transport implementation.

**Proof:** Parties and FrontComposer typed clients coexist; handler ordering and
repeat-registration behavior are deterministic; options/endpoint/tenant validation
and cancellation remain compatible. Publish public API/package evidence and verify
Parties `Client.Tests/DependencyInjectionTests.cs` plus consumer/package checks.

## P5 — Deliver the approved claims and identifier surfaces

**Owners:** EventStore authentication/contracts and Commons UniqueIds maintainers.

**Targets:** EventStore `src/Hexalith.EventStore.Contracts/Identity/`, the approved
lightweight `src/Hexalith.EventStore.Authentication/` package boundary, and Commons
`src/libraries/Hexalith.Commons.UniqueIds/UniqueIdHelper.cs`.

**Deliver:** Public tenant-claim constant, `AggregateIdentity.IsValid(string)`,
reusable tenant-claims transformation and strict `UniqueIdHelper.IsValidUlid(string)`
under the already recorded G7/G9 ownership decision. Do not conflate permissive
aggregate/semantic IDs with strict ULID parsing or pull the server into Contracts.

**Proof:** Canonical and legacy claims, mixed/multiple/blank/malformed/idempotent
cases; valid/invalid ULIDs and semantic aggregate IDs; unchanged generation and
conversion. Parties host transient and UI scoped composition/order and authorization
must match the retained implementation. Delete Authentication only after the
complete package/API/parity and exercised switch-back ledger passes.

## P6 — Reconcile the five MCP operations with McpCli

**Owners:** McpCli transport/security maintainer and Parties operation maintainer.

**Targets:** McpCli `src/Hexalith.McpCli.Core/Catalog/` and `Execution/`; the five
current contracts/behaviors in Parties `src/Hexalith.Parties.Mcp/Tools/` and the
corresponding decorated module Contracts enrollment path.

**Deliver:** An explicit map for create_party, get_party, find_parties, update_party
and delete_party through the existing McpCli architecture, plus an accepted
transport/credential decision. Preserve stable schemas and soft-deactivation
semantics through approved compatibility or explicit versioning. Do not build a
new proprietary Parties or FrontComposer MCP relay as the migration destination.
Historical G11-A routing must be reconciled before consumption.

**Proof:** Tenant/user context comes from server authentication; caller tool/header
values cannot select identity. Approved downstream credentials are independently
sourced; inbound MCP tokens/API keys are never reused. Missing credentials/context
fail before dispatch; header values replace rather than append, reject CR/LF and
duplicates; authorization/resource visibility, cancellation, bounded outcomes and
no-secret diagnostics pass. Keep the existing five-operation host until accepted
replacement or withdrawal and consumer behavior tests pass.

## P7 — Deliver the focused EventStore Admin link builder

**Owner:** FrontComposer UI service maintainer; optional Commons URI-mechanics owner.

**Targets:** FrontComposer `src/Hexalith.FrontComposer.Contracts.UI/` for the small
public result/service contract and `src/Hexalith.FrontComposer.Shell/Services/`
for configured composition. Use Commons only for demonstrated reusable URI mechanics.

**Deliver:** Aggregate/stream and correlation links from a configured safe absolute
HTTP/HTTPS base, preserving its path/query and encoding identifiers once; typed
unavailable outcomes. Avoid a generalized navigation DSL.

**Proof:** Blank IDs, unsupported schemes, user-info, malformed configuration,
existing query/path and reserved characters. Compare against Parties
`AdminPortalEventStoreAdminLinks` and its service/component tests independently
of P8; another G11 delivery does not permit this local helper's deletion.

## P8 — Deliver a bounded named-health probe

**Owners:** Commons.Http transport/parser maintainer and FrontComposer capability-service owner.

**Targets:** Commons `src/libraries/Hexalith.Commons.Http/` and
`test/Hexalith.Commons.Http.Tests/`; FrontComposer Contracts.UI/Shell capability
contract/service and producer tests.

**Deliver:** A small reusable HTTP/JSON named-check reader with explicit response
byte/depth/time bounds and cancellation. FrontComposer supplies the configured
Available/LocalOnly/Degraded capability mapping. Parties keeps its Memories/domain
search decisions. Avoid a generic monitoring platform or raw downstream detail.

**Proof:** Healthy, disabled/degraded, non-success, missing/wrong-type/malformed,
oversized and timeout inputs produce bounded fail-closed outcomes; caller cancellation
propagates. Validate Parties `PartiesAdminPortalApiClientTests` and affected component
behavior before retiring only its probe plumbing.

## P9 — Prove the integrated AppHost handoff

**Owner:** FrontComposer.AppHost or explicitly approved Platform AppHost maintainer;
external platform operations retains runtime deployment ownership.

**Targets:** Successor `src/Hexalith.FrontComposer.AppHost/Program.cs` or approved
host equivalent, EventStore.Aspire security/app-channel/workload helpers and owner
model/topology/publish tests. Preserve the Parties AppHost as the rollback oracle.

**Deliver:** Approved disposition of parties-mcp versus McpCli, standalone parties-ui
versus frontcomposer-ui, optional Memories/sample resources and Docker/Kubernetes/ACA
publish behavior. Configure receiving service app-channel credentials and gateway
workload issuance as described in Parties `docs/ci.md`. Record exact successor and
EventStore.Aspire identities. Retain the established key backend, application name
`Hexalith.Parties` and cursor purpose `Hexalith.Parties.QueryCursor.v1`, subject to
P2's proven provider compatibility and lawful erasure requirements.

**Proof:** EventStore/Parties/Tenants healthy; a gateway command has the expected
persisted end-state; forged/missing credentials cannot mutate state; required actor
and subscription callbacks work; ACL tuples remain equivalent. Check actual publish
artifacts without secrets, old cursor continuation, retained encrypted reads and
rollback/restart. Every deferral depending on Parties AppHost passes or is explicitly
re-approved against the successor before retirement. Runtime manifests stay external.

## P10 — Reconcile existing Commons HTTP and Builds consumption

**Owners:** Commons.Http and Builds maintainers; Parties build/consumer test owner.

**Targets:** Parties matrix available rows, root build props/targets/package import;
Commons `HttpClientRegistration`, bounded ProblemDetails and correlation surfaces;
Builds `Hexalith.Build.props`, `Hexalith.Package.props` and shared catalog.

**Deliver:** Exact selected source/package identity and facet-specific validation.
Current committed Commons is `116d26815eb81e35b3c161e1799e5ee12805fc0a`; Builds is
`af20682ac8fc420068a731ecb87cff84727a3d53`. Historical main-row identities differ.
An identity mismatch alone does not justify requesting new APIs. A focused shared
registration adoption is possible once gates close. Keep unsupported root probes,
Parties sensitive-detail scrubbing and correlation compatibility adapters.

**Proof:** Eager absolute HTTP/HTTPS/tenant validation, public messages, handler
composition, identifier and bounded-error semantics; effective warnings-as-errors,
source/package routing, package metadata and CI immutable-container provenance.
Use existing Client package/DI and CommonsHttpRestoreRouting/CI publication tests.
Do not import broad shared props blindly or weaken gates to make them fit.

## Common completion contract

Every consumed row must contain its actual approver, scope/date/reference, exact
package or committed root gitlink with matching clean checkout, public API/package
inventory, producer and Parties consumer results and exercised rollback. Selection
is not parity. Use the existing matrix, receipts and tests; add evidence only for
new behavior. No new tracker, approval framework, key service or deployment system
is part of this plan.

Consumer readiness requires both Story 8.7 closure/approved resequencing and the
relevant row's completed gates. Package publication alone fixes neither topology
credentials nor the remaining 8.8 prerequisites. The approved compatibility policy
is shared by 8.7 and 8.8; a cursor-only reset would require a later explicit decision.
