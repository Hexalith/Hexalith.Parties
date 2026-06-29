---
date: 2026-06-29
status: Accepted
epic: epic-7-platform-alignment
story: 7.1
decision: platform-target-destinations
---

# ADR: Epic 7 Platform Target Destinations

## Context

Epic 7 is post-MVP platform maintenance. It adopts existing or additive shared
Hexalith platform primitives where Parties currently carries generic
infrastructure, without changing product behavior or PRD feature coverage.

The approved architecture spine requires an adapter-first strangler migration:
prove target ownership, package/reference graph, behavior parity, compatibility,
and rollback before any Parties-local implementation is deleted. If a shared API
is missing or insufficient, the missing surface lands additively in the owning
submodule before Parties consumes it.

The repository currently uses root-declared submodules under `references/` and
project references through computed MSBuild root properties such as
`HexalithEventStoreRoot`, `HexalithTenantsRoot`, `HexalithMemoriesRoot`, and
`HexalithFrontComposerRoot`. `Hexalith.Commons` is present as a root submodule,
but `Directory.Build.props` has no `HexalithCommonsRoot` property yet. Central
Package Management is enabled, so any future package version belongs only in
`Directory.Packages.props`; `.csproj` files remain versionless.

## Decision

Parties will consume Epic 7 shared platform code through root `references/`
project references for the repositories already declared as root submodules,
including Commons. Story 7.2 owns adding `HexalithCommonsRoot` before any
Commons project references are added. This keeps the migration consistent with
EventStore, Tenants, Memories, and FrontComposer reference style, avoids a
mixed local-package graph during the adapter phase, and preserves Central
Package Management for external packages.

The future property shape is:

```xml
<HexalithCommonsRoot Condition="'$(HexalithCommonsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.Commons\src\libraries\Hexalith.Commons\Hexalith.Commons.csproj')">$(MSBuildThisFileDirectory)references\Hexalith.Commons</HexalithCommonsRoot>
<HexalithCommonsRoot Condition="'$(HexalithCommonsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)..\references\Hexalith.Commons\src\libraries\Hexalith.Commons\Hexalith.Commons.csproj')">$(MSBuildThisFileDirectory)..\references\Hexalith.Commons</HexalithCommonsRoot>
```

Future Commons references use this form, with no `Version=` attributes:

```xml
<ProjectReference Include="$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.ServiceDefaults\Hexalith.Commons.ServiceDefaults.csproj" />
```

If a later release changes the strategy to NuGet packages, that story must add
only `<PackageVersion Include="Hexalith.Commons.*" Version="..." />` entries to
`Directory.Packages.props` and versionless `<PackageReference Include="..." />`
entries to `.csproj` files.

## Target Destination Matrix

| ID | Scope | Owner repo/project | Destination API surface | Package/reference path | Parties adapter or facade | Release order | Rollback path | Required evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| B1 | Projection sequence checkpoint and replay dedupe | `references/Hexalith.EventStore/src/Hexalith.EventStore.Server` | `Hexalith.EventStore.Server.Projections.IProjectionCheckpointTracker`, `ProjectionCheckpoint`, and tracker reason codes | Existing `$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Server\Hexalith.EventStore.Server.csproj` project reference | Parties projection checkpoint adapter inside `Hexalith.Parties.Projections`; public read contracts stay unchanged | 7.4 introduces adapter, 7.5 removes duplicate local checkpoint plumbing after parity | Revert adapter DI registration or EventStore pointer; restore Parties-local actor checkpoint path | Duplicate and out-of-order event tests, replay from sequence zero, per-actor checkpoint parity, stale/degraded fallback, erased-party exclusion |
| B2 | Resumable projection rebuild and replay-from-zero | `references/Hexalith.EventStore/src/Hexalith.EventStore.Server` | `IProjectionRebuildOrchestrator`, `IProjectionRebuildCheckpointStore`, `ProjectionRebuildCheckpointScope`, and save-result records | Existing `$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Server\Hexalith.EventStore.Server.csproj` project reference | `ProjectionRebuildService` compatibility facade delegates to EventStore rebuild primitives | 7.4 proves adapter parity, 7.5 migrates local rebuild implementation | Revert facade registration or EventStore pointer; retain local `ProjectionRebuildService` state until 7.8 cleanup | Resume/cancel rebuild tests, checkpoint persistence parity, state-store failure handling, replay-from-zero validation |
| B3 | AES-GCM payload protection | `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts` plus provider implementation owned by 7.6/7.7 | `IEventPayloadProtectionService`, `EventStorePayloadProtectionMetadata`, `PayloadProtectionResult`, `PayloadUnprotectionOutcome`, unreadable-data decision records | Existing `$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj`; provider reference only after 7.6 approves it | `PartyPayloadProtectionService` adapter maps Parties metadata, legal states, and unreadable classifications to EventStore contracts | 7.6 ADR and harness first, 7.7 adoption second | Restore Parties provider registration and previous metadata reader; roll back provider package/submodule pointer | Readable, unreadable, missing-key, provider-unavailable, erased, restricted, and legacy-unprotected harness; no PII/key/raw-payload leak evidence |
| B4 | Party key-management subsystem | Generic mechanics in EventStore/shared security; party-specific policy remains `src/Hexalith.Parties.Security` and `src/Hexalith.Parties.Contracts.Security` | Additive key-provider mechanics only if 7.6 identifies a generic shared surface; `IPartyKeyManagementService` and legal policy remain local | Existing Parties project references remain until 7.6 approves an additive owner surface | Parties key-management facade keeps tenant/party semantics, erasure commands, certificates, and legal policy local | 7.6 classifies the split; 7.7 adopts only approved generic provider mechanics | Keep `PartyKeyManagementService` and `PartyErasureOrchestrator` as the active implementation; roll back provider pointer | Crypto-shredding irreversibility, certificate behavior, export/processing-record redaction, no destroyed-key detail leakage |
| B5 | ServiceDefaults | `references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults` | `HexalithServiceDefaults`, `HexalithServiceDefaultsOptions`, default health endpoint and OpenTelemetry helpers | Future `$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.ServiceDefaults\Hexalith.Commons.ServiceDefaults.csproj` project reference | Keep `Hexalith.Parties.ServiceDefaults` as a thin wrapper for Parties-specific health and DAPR hooks | 7.2 adds Commons root property and wrapper adoption | Revert wrapper to current local implementation or roll back Commons pointer | Build plus health endpoint, JSON console logging, OpenTelemetry source/meter, and DAPR health behavior parity |
| B6 | Correlation accessor and middleware | `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Metadatas` and additive Commons diagnostics middleware | `ContextMetadata.CorrelationId`, `Metadata`, and an additive bounded correlation middleware/accessor if needed | Future `$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.Metadatas\Hexalith.Commons.Metadatas.csproj`; diagnostics path only after owner story lands it | `CorrelationContextAccessor` facade preserves current header and command correlation semantics | 7.2 lands any missing Commons diagnostics API before Parties adoption | Restore local `CorrelationIdMiddleware` and accessor registration | Header propagation tests, malformed/non-string correlation handling, no PII in logs or ProblemDetails |
| B7 | ProblemDetails and global exception mapping | `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http` or Commons ServiceDefaults additive HTTP error mapping | Shared bounded HTTP error mapping over `ApplicationError`/ProblemDetails-compatible shapes; domain rejection semantics stay local | Future `$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.Http\Hexalith.Commons.Http.csproj` or ServiceDefaults reference after additive story | Parties client/admin/consumer facades map errors to bounded outcomes without leaking raw ProblemDetails | 7.2 adds or consumes Commons HTTP mapping | Restore local exception handler and typed-client error mapping | 400/422/401/403/404/410/timeout/5xx mapping tests, raw ProblemDetails non-leak tests, GDPR tombstone copy preservation |
| B8 | Jaro-Winkler and diacritic normalization | Pure text normalization in `references/Hexalith.Commons/src/libraries/Hexalith.Commons`; search-specific scoring in `references/Hexalith.Memories` only when configured | Additive Commons text normalization/similarity helpers; Memories search result/scoring contracts remain optional | Future `$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons\Hexalith.Commons.csproj`; existing optional `$(HexalithMemoriesRoot)` references remain gated | `LocalFuzzyPartySearchProvider` adapter keeps local fallback behavior and delegates only pure helpers | 7.3 lands Commons pure text helpers first, then Parties adoption; Memories remains optional | Revert to local normalization/scoring and keep Memories disabled path working | Local fallback search parity, Memories-enabled path parity, diacritic and similarity edge cases, no behavior drift in admin list/search |
| B9 | Projection freshness vocabulary | `references/Hexalith.EventStore/src/Hexalith.EventStore.Client` and contracts projections | `ReadModelFreshness`, `ReadModelFreshnessState`, `ReadModelFreshnessThresholds`, `ProjectionChangedNotification`, `ProjectionResponse` | Existing or future `$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Client\Hexalith.EventStore.Client.csproj`; contracts reference already exists | Compatibility mapper preserves `ProjectionFreshnessMetadata`, `ProjectionFreshnessStatus`, and UI `StatusKind` until a separate public contract story approves change | 7.4 introduces mapper; 7.5 may replace local freshness internals after parity | Revert mapper registration or EventStore pointer; retain public Parties freshness records | Fresh/current/stale/degraded/unavailable/local-only mapping tests, last-known fallback, SignalR/freshness reconciliation |
| B10 | `PagedResult<T>` | `references/Hexalith.Commons/src/libraries/Hexalith.Commons` additive generic paging surface | Additive Commons generic page result and metadata, with public Parties shape preserved | Future `$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons\Hexalith.Commons.csproj` after owner API lands | Adapter maps Commons paging to `Hexalith.Parties.Contracts.Models.PagedResult<T>` for public clients and UI | 7.2 lands additive Commons paging before Parties adoption | Revert adapter and keep current Parties `PagedResult<T>` | Serialization compatibility, null/empty collection normalization, page/size/total parity, package API compatibility tests |
| B11 | Mixed primitives | Split by owner: EventStore projections/security, Commons HTTP/diagnostics/text, FrontComposer lifecycle/orchestration, Parties policy | `DecryptionCircuitBreaker` follows payload protection; `PartyEventTypeResolver` and `IIndexPartitionStrategy` follow EventStore projection/type-resolution; typed-client error mapping follows Commons; UI lifecycle follows FrontComposer | Existing EventStore and FrontComposer root properties; future Commons root property; Parties-local policy projects remain | Separate compatibility facades per primitive; no catch-all shared package | 7.2/7.3 for utility/UI pieces, 7.4/7.5 for projection pieces, 7.6/7.7 for crypto pieces | Roll back each primitive by DI switch, source pointer, or deferred deletion; keep Parties-local code until 7.8 evidence | Owner-specific parity tests, allowlisted type-resolution tests, lifecycle/optimistic reconciliation tests, crypto circuit-breaker harness |

## Dependency and Reference Strategy

The selected Commons consumption path is project references through a new
`HexalithCommonsRoot` property. Story 7.2 owns adding the property and first
Commons references. Story 7.1 intentionally does not edit `Directory.Build.props`,
`Directory.Packages.props`, any `.csproj`, or `Hexalith.Parties.slnx`.

Reference rules for future stories:

- Add only root-submodule project references for EventStore, Tenants, Memories,
  FrontComposer, and Commons unless a story explicitly switches to released
  packages.
- Never initialize nested submodules and never use recursive submodule update.
- Preserve `.slnx`; do not add a classic `.sln`.
- Keep `.csproj` package references versionless.
- If packages are used later, put versions only in `Directory.Packages.props`.
- Do not consume not-yet-released or unapproved APIs from a local checkout. Land additive
  owner APIs, validate owner gates, then update the root pointer or package
  reference before Parties adoption.

## Missing Shared API Stories

| Need | Owning story before Parties adoption | Owner | Required additive surface |
| --- | --- | --- | --- |
| Commons root MSBuild property | 7.2 | Parties repository | Add `HexalithCommonsRoot` to `Directory.Build.props` using the property shape in this ADR. |
| Commons correlation middleware/accessor | 7.2 | Commons | Add a bounded diagnostics middleware/accessor if `ContextMetadata` is insufficient for HTTP header propagation. |
| Commons ProblemDetails mapping | 7.2 | Commons | Add HTTP error mapping that supports bounded ProblemDetails-compatible output without domain rejection leakage. |
| Commons paging model | 7.2 | Commons | Add generic paging records or helpers if no stable public Commons paging API exists. |
| Commons pure text normalization/similarity | 7.3 | Commons | Add diacritic normalization and Jaro-Winkler or equivalent pure text helpers without Memories dependency. |
| EventStore projection compatibility gaps | 7.4 | EventStore | Add projection adapter hooks only if current checkpoint/rebuild/freshness contracts cannot preserve Parties semantics. |
| EventStore projection migration gaps | 7.5 | EventStore | Add rebuild/checkpoint evidence hooks only if 7.4 parity exposes missing observability or rollback controls. |
| Crypto/key-management split | 7.6 | EventStore/shared security and Parties | Classify provider contracts, key storage, wrapping, rotation, audit, circuit breaker, and event-type resolution before migration. |
| Payload/key provider implementation | 7.7 | EventStore/shared security | Add or pin approved provider implementation after 7.6 harness passes. |
| Final pin and cleanup evidence | 7.8 | Parties repository | Pin final submodule commits or package versions and defer any deletion lacking evidence. |

## Compatibility Strategy

- EventStore gateway routing remains authoritative. Parties still receives DAPR
  `/process` calls from EventStore; this ADR creates no public Parties host API.
- Public Parties contracts evolve additively only. `ProjectionFreshnessMetadata`,
  `ProjectionFreshnessStatus`, `PagedResult<T>`, command/query shapes, and GDPR
  read models remain compatible until a separate breaking-change plan approves a
  public contract change.
- Projection migration must preserve replay from sequence zero, at-least-once
  tolerance, duplicate/out-of-order safety, per-actor checkpoints, stale/degraded
  fallback, and erased-party exclusion.
- Crypto/key-management migration must preserve irreversible erasure,
  unreadable-payload classification, party-specific legal policy, PII-free logs
  and telemetry, export/processing-record redaction, and erasure certificate
  behavior.
- ServiceDefaults, correlation, ProblemDetails, paging, and search utility
  adoption must be adapter-backed and behavior-preserving. Domain rejection
  semantics and regulated copy stay in Parties.

## Rollback Strategy

Every implementation story must keep the previous Parties-local behavior
reachable until its validation lane passes and Story 7.8 records readiness.

- Utility stories roll back by restoring the local wrapper/DI registration or
  reverting the Commons pointer.
- Projection stories roll back by switching adapters back to Parties-local
  checkpoint/rebuild/freshness code and leaving projection state compatible.
- Crypto stories roll back by restoring the Parties provider registration and
  previous metadata reader; no rollback may make protected payloads unreadable
  unless the unreadable state is already represented by existing GDPR semantics.
- Final cleanup deletes local infrastructure only when the corresponding adapter
  story has passing parity and an explicit rollback pointer.

## Test Evidence Requirements

Evidence is captured in the story that performs adoption:

- `git diff --check` on every story.
- Build and focused unit tests for each touched Parties project.
- Owning submodule build/test lane for each touched submodule.
- Projection parity for duplicate, out-of-order, replay-from-zero,
  state-store failure, stale/degraded fallback, and erased-party exclusion.
- Crypto harness for readable, unreadable, missing-key, provider-unavailable,
  erased, restricted, legacy unprotected, and no-PII/no-key/raw-payload leakage.
- API/package compatibility tests for public Parties client/contracts/UI package
  surfaces.
- Story 7.8 records final submodule commits or package versions and readiness
  evidence.

## Consequences

This decision keeps Epic 7 executable without making unapproved cross-submodule
changes from Parties. It favors local project-reference adoption during the
adapter phase, so later stories must coordinate Commons additive APIs before
Parties can compile against them. It also delays deletion of local infrastructure
until behavior parity and rollback are proven, which reduces immediate cleanup
but keeps the platform migration reversible.
