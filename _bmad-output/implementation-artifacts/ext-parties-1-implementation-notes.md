# EXT-PARTIES-1 owner implementation notes

Prepared 2026-10-03 for the [owner build spec](spec-ext-parties-1-branch-b-authoritative-identity.md). Starting intent is the [Agents Parties proposal](../../../agents/_bmad-output/specs/spec-story-5-4-dependency-unblock/parties-identity-contract.md); the user authorized starting its Branch B owner work. This is an owner implementation plan, not dependency acceptance, live evidence or completion of Agents 5.4.

## Inspected baseline

| Checkout | Full source HEAD |
| --- | --- |
| Parties owner | `14d249fde316b0002aec84351d7a7cdf953d1d30` |
| Parties EventStore reference | `dfc0ac557c43363159b55bffb4d40feceab1f787` |

Identifiers were read directly from version control. Parties began clean on main. Its EventStore package catalog selects 3.104.0 and SDK pin is 10.0.401. Source reference mode must be selected explicitly with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; use Debug for local development. No dependency update is proposed by these observations.

The local main is six commits behind the already-known `origin/main` at `937cb2a343aaa74963db9bb867a2c3a01ff48677`. A read-only comparison found UI overview/routing, source-reference configuration, Aspire SDK and approved McpCli course-correction changes; it adds no provisioning or stable-actor authority. Reconcile that baseline before implementation and preserve its approved runtime qualification prerequisites. No fetch, checkout or update was performed here.

Stories 8.3, 8.5 and 8.6 are done. `Program.cs` already uses the domain-service SDK and current query/projection handlers use SDK stores. Do not reopen those stories or recreate retired actors because older project-context descriptions still mention them. No Parties sprint key is assigned to this new external-contract work; preserve existing tracking.

## Public surface and file map

New names are proposals within the owning packages, not accepted published contracts.

| Area | Proposed concrete files and responsibilities |
| --- | --- |
| Safe identity shape | `src/Hexalith.Parties.Contracts/Models/PartyIdentityEvidence.cs`, `PartyIdentityResult.cs`, `AgentPartyProvisioningResult.cs`, `HumanActorBindingEvidence.cs`, `HumanActorBindingResult.cs`; `ValueObjects/PartyIdentityClassification.cs`, `PartyIdentityOutcome.cs`, `HumanActorBindingOutcome.cs`. Every successful result binds contract version, exact tenant/Party, classification/liveness, source position and observation basis. Human success additionally carries opaque actor ID, binding version and half-open interval. Non-human results carry no human binding. |
| Provisioning | `src/Hexalith.Parties.Contracts/Commands/ProvisionAgentParty.cs`, `Events/AgentPartyProvisioned.cs`, `Events/Rejections/AgentPartyProvisioningRejected.cs`, `ValueObjects/AgentPartyIdentity.cs`. Carry exact tenant, Agent/Party, contract/logical identity, immutable creation fingerprint and source-authorized provenance; return original revision on exact retry. Creation and marker commit together. No PII in marker/result. |
| Attribution transitions | `src/Hexalith.Parties.Contracts/Commands/EstablishHumanActorBinding.cs`, `RevokeHumanActorBinding.cs`, `RebindHumanActorBinding.cs`; `Events/HumanActorBindingEstablished.cs`, `HumanActorBindingRevoked.cs`, `HumanActorBindingRebound.cs`, `Events/Rejections/HumanActorBindingRejected.cs`. Bind authenticated source/actor evidence, expected Party/binding revision, policy reference, immutable effective instant and logical retry identity. |
| Pure state/decisions | `src/Hexalith.Parties.Contracts/State/PartyState.cs`, `HumanActorBinding.cs`; `src/Hexalith.Parties/Domain/PartyAggregate.cs`. Add explicit created/provisioned evidence, immutable history, exact original result and correct rejection-only-state handling. All Apply functions are deterministic; do not use `PartyState.CreatedAt` or UtcNow as action/history authority. Preserve no-op rejection Apply ordering. |
| Admission | `src/Hexalith.Parties/Authorization/IPartyIdentityAuthority.cs`, `PartyIdentityAdmissionResult.cs`; `Domain/PartyDomainProcessor.cs`, `Extensions/PartiesServiceCollectionExtensions.cs`, and validators in `Validation/`. Select exact operation/target/source evidence before unprotecting state or dispatching. Missing or unsupported trust/policy fails closed. Gateway owns generic auth/RBAC; this port enforces Parties identity invariants, not a second generic RBAC engine. |
| Queries | `src/Hexalith.Parties.Contracts/Queries/ResolvePartyIdentity.cs`, `ResolveHumanActorBindingAt.cs`; `src/Hexalith.Parties/Queries/PartyIdentityQueryHandler.cs`, `HumanActorBindingAtQueryHandler.cs`. Implement IDomainQueryHandler behind the gateway with a shared verified-source reader; validate query/envelope/source/returned identity equality. Preserve typed internal failures and indistinguishable unauthorized public disclosure. |
| Client | `src/Hexalith.Parties.Client/Abstractions/IPartiesIdentityClient.cs`, `HttpPartiesIdentityClient.cs` and DI registration. Explicit immutable per-call scope; separate stable logical intent from delivery attempts; never mutate shared PartiesClientOptions.Tenant. Existing general command/query interfaces remain compatible. |

The stable actor issuer, administration policy and erasure/history policy are open in the primary spec. Do not fill them with JWT sub/oid, tenant role, Party ID or an implementation-chosen retention period. Public names and transport error codes can be decided during implementation; they are not additional permission questions.

## Creation and retry rules

1. New identities use one versioned tenant-and-Agent-bound mapping with canonical test vectors. Record that mapping before consumer adoption. Never apply it retroactively to an existing Party link; preserve every existing ID.
2. Compare immutable provisioning intent, not mutable display details. Conditional creation emits the original Party creation plus provenance marker at one commit; exact repeat/recovery returns the original marker revision and reevaluates current liveness separately.
3. `PartyState` may exist solely because rejection events replayed. That is not a created Party; valid first creation must still be possible. Conversely, a created unmarked legacy Organization is not an Agent-owned identity.
4. Changed tenant/Party/Agent/type/contract/fingerprint at the same logical identity conflicts, including generic CreateParty/CreatePartyComposite bypasses and reserved identities. An occupied unmarked ID remains conflict/unavailable until an approved same-ID provenance procedure exists; never invent a replacement or historical ownership.
5. Concurrent/lost-ack recovery reads the exact authoritative stream and compares original intent. Unknown result remains unavailable, with no fallback ID or reactivation.

## SDK evidence prerequisite

`IEventStoreGatewayClient.ReadStreamAsync` and Streams contracts are usable public source primitives. Existing `StreamsController` obtains metadata and event ranges in separate actor calls; `LatestSequence` is a sampled head hint, not a consolidated current-state proof. Continuation tokens are unsupported; `FromSequence` is exclusive, so the next range starts from the last returned sequence, not that sequence plus one.

Add a generic shared verified-prefix/head reader to the EventStore technical module first, under `src/Hexalith.EventStore.Client/Streams/IAuthoritativeEventStreamReader.cs` with result/evidence records in Contracts and an owning implementation/test lane. Its concrete design must prove exact tenant/domain/aggregate, complete contiguous source prefix through an authenticated current checkpoint, coherent observation basis, bounded reads/retries, source-protection availability and exact credential scope. It may build on existing gateway reads with explicit complete-prefix/head validation; it must not relabel projection timestamp, ETag or replay of supplied events as current authority. Do not introduce direct Dapr/actor calls or private persistence in Parties.

Parties strict queries stay unavailable without that verified reader. Ordinary PartySdkQueryService stale-cache behavior remains for its existing UI use; it cannot authorize the new seam. Where projection materialization is needed, use SDK read-model stores/policies and validate their checkpoint against the authoritative source, including duplicate conflicts, gaps and rebuild state. No stale fallback is eligible for authority.

## Actor attribution and Consumer compatibility

The accepted Consumer ADR owns private `{tenant, issuer, subject} -> party_id` provisioning and IdP claim emission outside Party events. Preserve `PartyIdClaimResolver`, `ISelfScopedPartiesClient`, login mapping, onboarding redirects and browser-token boundaries. The new contract adds only opaque stable actor attribution and version/interval/provenance to Party domain, not private login mapping or proof documents. Document this extension explicitly and adjust `IdentityBindingBoundaryTests` by responsibility, never by renaming around their source checks.

`IdentityBindingProvisioningService.RotateAsync` rotates PartyId under one issuer/subject; it proves no login continuity. Its in-memory binding/IdP stores and rollback are not durable historical authority. Existing `sub`/`oid` extraction, TenantOwner Consumer administration and activity ActorId fields also do not establish global actor identity or a new attribution-administration policy.

Use one immutable effective instant T to close the predecessor interval at T and open its successor at T. History lookup is `[ValidFrom, ValidUntil)` and must reproduce recorded actor/version/source evidence across replay/restore. Gaps, overlaps, ambiguous sources and mismatches have explicit safe outcomes. Current revoked/non-human/restricted/erased identities cannot authorize a new action, even when a historical lookup succeeds. Caller/editor/regenerator aliases are compared by stable actor at consumer adoption, not Party ID.

## Verification plan

Create `eng/verify-ext-parties-1.ps1` with separate local and live qualification modes. Local mode builds and runs owning projects individually in Debug/source mode and executes focused xUnit assemblies with single-dash class filters. Live mode requires exact installed targets, authenticated role/source setup, failure injection and persisted-state assertions; missing credentials/policy/source evidence, substitutes or skipped required lanes fail qualification. A local pass is not availability.

| Vectors | Owning test files | Required assertion |
| --- | --- | --- |
| P-01–P-03 | `tests/Hexalith.Parties.Server.Tests/Aggregates/AgentPartyProvisioningTests.cs`, `PartyAggregateCreateTests.cs`, `PartyAggregateCompositeTests.cs`; `tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs` | One original creation/marker/result after exact/concurrent/lost-ack retry; divergent intent preserves state; rejection-only replay permits real creation. |
| P-04–P-06 | `tests/Hexalith.Parties.Tests/Domain/PartyDomainProcessorValidationTests.cs`; `tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs`; `tests/Hexalith.Parties.Tests/Gateway/PartyIdentityQueryHandlerTests.cs` | Wrong service/tenant/target and returned identity deny; concurrent tenant calls remain isolated; unknown/inactive/restricted/erased/gapped evidence cannot be usable. |
| P-07–P-09 | `tests/Hexalith.Parties.Server.Tests/Aggregates/HumanActorBindingTests.cs`; `tests/Hexalith.Parties.Contracts.Tests/State/HumanActorBindingTests.cs`; `tests/Hexalith.Parties.Tests/Gateway/HumanActorBindingAtQueryHandlerTests.cs` | Only verified active human binds; exact interval boundaries and actor/version/source identity survive replay; gap/overlap/mismatch cannot fall back to current. |
| P-10/compatibility | `tests/Hexalith.Parties.UI.Tests/IdentityBindingBoundaryTests.cs` and existing claim/self-scope tests; `tests/Hexalith.Parties.Contracts.Tests/Serialization/PartyIdentityContractTests.cs` | Preserve private Consumer mapping; no PII results/logging; alias exclusion evidence is reproducible without redefining consumer authority. |
| Live persisted end state | `tests/Hexalith.Parties.IntegrationTests/Identity/PartyIdentityContractIntegrationTests.cs` | Inspect real stored events/read models after restart/restore, failure recovery and cross-tenant denial, rather than only HTTP codes/mock counts. |

No builds, tests, runtime seams, migrations, deployments, owner messages, commits or submodule updates were performed in this planning run. Only the two new planning documents were written. The current artifacts do not populate target/date/command or AcceptedStatus in the Agents register. A verifier pathname is a deliverable to implement, not an already accepted compatibility command.
