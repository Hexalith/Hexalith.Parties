# Epic 8 architecture validation

21 November 2026 · Hexalith.Parties · Domain and platform migration

## Decision

**Changes required before the affected migrations can use this spine for sign-off.** The release selection and historical Epic 8 closure are supported. Five high-priority findings affect privacy, ledger integrity, projection recovery or the ability to repeat an acceptance check. Five medium findings and one low finding follow. The November 2 closure remains valid for its recorded revision.

The review covered the final working-copy spine, the team overview, the parent decisions, the adopted Epic 8 SPEC, and the current code and decision records. The repository revision was `42b27f2e`. The reviewed spine was an uncommitted amendment. Its SHA-256 begins `31155d59cfb62498`. Sources for the working documents use local links. Line links for committed code and governance records select the reviewed Git revision.

## Findings

### Outage reads must respect erasure

**High.** I10 and I14 tell readers to render last-known data. If the canonical store is unavailable, another replica may already have erased an active party. The query service evicts active detail and nonempty or unknown indexes. It permits defensively redacted erased detail, absence, or a confirmed empty index, as applicable. Following the broad rule could reintroduce disclosure of personal data.

**Proposed action.** Parties query and UI owners should put the admission rule in I10 and reference it from I14. Clients must honor a denied read. Use the outage and cross-replica erasure cases as acceptance criteria.

[Working spine, line 253](../ARCHITECTURE-SPINE.md) · [Working spine, line 293](../ARCHITECTURE-SPINE.md) · [Index admission, line 395](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Queries/PartySdkQueryService.cs#L395) · [Detail admission, line 477](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Queries/PartySdkQueryService.cs#L477) · [Cross-instance regression, line 54](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.Tests/Gateway/PartySdkCrossInstanceErasureTests.cs#L54) · [Team overview, line 239](../../../../../docs/architecture.md)

### The Memories ledger uses an interface I19 forbids

**High.** I19 classifies the mapping inventory as an operational ledger and expressly excludes IReadModelStore. The shipped implementation uses that interface for conditional storage. DW-81 records concurrency and compatibility evidence from October 23. The I20 entry for classification and the approved consistency tests is pending. Treating the ledger as disposable projection state could lose the inventory needed to verify cleanup.

**Proposed action.** The Parties and EventStore owners should record whether conditional operations on this interface satisfy the operational-ledger contract or whether a dedicated interface is required. Identify the tests for rebuild exclusion, concurrent updates and cleanup before using this decision for a migration.

[Working spine, line 351](../ARCHITECTURE-SPINE.md) · [Mapping store, line 26](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs#L26) · [DW-81 resolution, line 1194](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/implementation-artifacts/deferred-work-archive.md#L1194) · [I19 approval, line 1896](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#L1896)

### Actor attribution history has its own retention rule

**High.** I8 covers per-party profile keys. The spine mentions the actor-history design in its October 5 record, when the decision was still deferred. A subsequent approved policy gives minimal human-actor history independent custody and an exclusive expiry 365 fixed days from binding effectiveness. Profile erasure immediately ends current eligibility. Historical use requires separately authorized custody, and restore must honor the original expiry.

**Proposed action.** Parties privacy and Platform custody owners should bind I7/I8 to the versioned policy and its restore rules. Keep production history disabled until the custody, restore and owner-compatibility evidence is accepted.

[Working spine, line 238](../ARCHITECTURE-SPINE.md) · [Working spine, line 728](../ARCHITECTURE-SPINE.md) · [Accepted scope and ownership, line 3](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/actor-history-retention-policy-v1.md#L3) · [Expiry and profile erasure, line 23](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/actor-history-retention-policy-v1.md#L23) · [Restore and activation, line 31](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/actor-history-retention-policy-v1.md#L31) · [Host configuration, line 18](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/appsettings.json#L18)

### CAP-1 still calls for retired tests on current HEAD

**High for acceptance.** The adopted Epic 8 SPEC requires a fresh checkout to pass PlatformApiPrerequisitesTests and EpicEightClosureFitnessTests. The November 16 CI migration retired both. The final spine identifies the discrepancy and protects the historical closure. A team still cannot execute the SPEC’s current acceptance criterion, and the successor approval row is pending.

**Proposed action.** The spec owner should date the closure criterion to its witnessed revision. Before a deletion relies on either retired check, map the affected behavior to a supported product test, obtain I18/I20 approval and run it at the selected identities.

[CAP-1, line 27](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md#L27) · [SPEC success signal, line 89](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md#L89) · [Working spine, line 464](../ARCHITECTURE-SPINE.md) · [Current product checks, line 15](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/docs/ci.md#L15) · [Successor approvals, line 1895](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#L1895)

### Live delivery and a full projection rebuild start differently

**High.** I9 says to replay from zero on every delivery. A live handler loads persisted state and folds new events after its checkpoint, with recovery for specific historical gaps. A full rebuild constructs a candidate from empty state. A future handler could discard valid state when a live request contains only a tail.

**Proposed action.** Describe live delivery, duplicate handling, recovery and full rebuild separately. Require live and rebuilt models to agree for the same complete history.

[Working spine, line 251](../ARCHITECTURE-SPINE.md) · [Live fold, line 291](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs#L291) · [Rebuild candidate, line 213](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs#L213) · [Tail recovery test, line 222](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs#L222)

### Tenant-claims routing and migration authority are distinct

**Medium.** Section 2 describes the tenant-claims supersession as approved. The July decision selected the destination. The approval table still calls for a parent-decision record covering G7/G9 and the McpCli course correction. The delivery receipt says that consumer parity and retirement authorization are outstanding.

**Proposed action.** Qualify the approval wording and link the pending I20 record. Preserve the existing Authentication rollback surface until the owner supplies the applicable evidence and approvals.

[Working spine, line 111](../ARCHITECTURE-SPINE.md) · [Routing decision, line 48](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md#L48) · [Divergence record, line 1897](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#L1897) · [Delivery scope, line 2201](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#L2201)

### Create retries have an observable result

**Medium.** I11 names IdentifierValidator and describes a typed rejection for a tombstoned identifier. The create handlers use PartyIdentifier.IsValid and return an idempotent no-op for an already-created aggregate, including when its creation evidence survives erasure. Both approaches prevent reuse, but callers receive different outcomes. The closure map identifies rejection as future work.

**Proposed action.** Document the current validation type and no-op behavior. If the product needs a distinct tombstone rejection, record the compatibility decision and cover both create paths.

[Working spine, line 275](../ARCHITECTURE-SPINE.md) · [Working spine, line 489](../ARCHITECTURE-SPINE.md) · [Simple create, line 708](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Domain/PartyAggregate.cs#L708) · [Composite create, line 58](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Domain/PartyAggregate.cs#L58) · [Identifier grammar, line 19](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs#L19)

### A dependency rollback can exercise two revisions

**Medium.** The parent permits rolling back a package or submodule revision. Section 4 asks for the rollback test at the same identity as the forward parity evidence. That wording works for an adapter switch at one revision but leaves no unambiguous way to record a real dependency downgrade.

**Proposed action.** Record the before and after package or source identities for a pointer rollback, and attach results to the revision each run exercised. Keep the single-identity check for an adapter-only switch.

[Working spine, line 405](../ARCHITECTURE-SPINE.md) · [Working spine, line 302](../ARCHITECTURE-SPINE.md) · [Inherited rollback rule, line 121](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#L121)

### Isolated Dapr tests transform the gateway identity

**Medium.** I1 names the canonical EventStore caller and ACL. The isolated mTLS profile derives a suffixed app ID, namespace and trust domain. An implementer comparing literal strings could reject a correctly isolated proof or overlook which identity was exercised.

**Proposed action.** Describe the isolated test mapping and record both the canonical and exercised identity with the evidence. Keep deployment ACL changes subject to I1 approval.

[Working spine, line 137](../ARCHITECTURE-SPINE.md) · [Test-profile rendering, line 134](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs#L134) · [Isolated identity mapping, line 201](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs#L201)

### The overview places the protection adapter in the wrong startup sequence

**Medium.** The overview says to register Parties protection before AddEventStoreServer. The Parties domain host calls AddEventStoreDomainService and then AddParties, which registers the protection adapter. AddEventStoreServer belongs to the separate gateway fixture. Following the overview could configure the wrong process during a host migration.

**Proposed action.** Document which host resolves IEventPayloadProtectionService and its actual registration sequence. Verify that resolution before retiring the local adapter.

[Team overview, line 172](../../../../../docs/architecture.md) · [Parties host, line 20](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Program.cs#L20) · [Adapter registration, line 213](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs#L213) · [Gateway fixture, line 36](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.EventStoreGateway.TestHost/Program.cs#L36)

### The gateway test deferral has already been resolved

**Low.** Section 9 still assigns work to replace a silent no-op. The gateway test explicitly skips outside an enabled profile and requires the fixture to be available on an enabled run. DW-96 is marked done.

**Proposed action.** Retire the stale reminder and date the old I18 map-coverage test in §7a to its archived revision. Keep successor-host and external-deployment checks attached to their own gates.

[Working spine, line 630](../ARCHITECTURE-SPINE.md) · [Working spine, line 557](../ARCHITECTURE-SPINE.md) · [Required fixture, line 41](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs#L41) · [Explicit skip, line 52](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs#L52) · [DW-96 status, line 729](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/implementation-artifacts/deferred-work.md#L729)

## Checks and limits

| Check | Result |
| --- | --- |
| Spine lint and identifiers | Zero mechanical findings. Independent comparison with HEAD found all 22 Epic 8 invariant IDs, six inherited decisions and 16 closure-map rows intact. |
| References | All 11 distinct local links in the spine and 31 in the overview resolved, including their heading fragments. |
| Release selection | The parent selects Platform v2.2.4 (a74abe17), Builds v6.4.3 (dc225a69) and EventStore source 7435254c. Platform selects the same Builds revision. Local Builds, CiCd and Platform checkouts differ from the parent pins. |
| Package selection | MSBuild evaluated Release package mode against the committed Builds catalog. The Parties host and gateway test host selected EventStore 4.2.0. The attached lifecycle suite selected 5.4.0. An explicit host selection of 5.4.0 took precedence. All three evaluated projects target net11.0. |
| Platform declaration | The generated declaration matched the checked-in version. All seven declaration checks passed. |
| Historical closure | The last validation table at c7dd607b records six passing lanes. The current tracker records Epic 8 and Story 8.10 as done, with Stories 8.7, 8.8 and 8.9 blocked. |

The November 21 amendments distinguish the two package selections, the three consumer erasure stages, and the per-party key provider from the ASP.NET Core key ring. The release-identity checks, source inspection and declaration tests were run for this review. Full release CI, live erasure, cross-replica revocation and production custody qualification were outside this validation. GATE-KMS and the deployed erasure-worker evidence remain open. [Deployment gate, line 454](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/parties-ui-prd.md#L454) · [Erasure action, line 375](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/implementation-artifacts/sprint-status.yaml#L375)

## Independent reviews

[Rubric and parent decisions](validate-2026-11-21-rubric.md) · [Reality check](validate-2026-11-21-reality.md) · [Adversarial review](validate-2026-11-21-adversarial.md)
