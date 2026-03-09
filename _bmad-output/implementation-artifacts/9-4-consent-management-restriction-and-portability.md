# Story 9.4: Consent Management, Restriction & Portability

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want to manage per-channel per-purpose consent, restrict processing, and export party data,
so that GDPR Articles 6, 18, and 20 obligations are fulfilled.

## Acceptance Criteria

1. **Per-Channel Per-Purpose Consent Recording (FR47)**
    - Given a party with contact channels
    - When an administrator records consent for a specific channel and purpose
    - Then a consent record is created with: consent ID, channel ID, purpose, lawful basis, granted timestamp
    - And the consent record supports all lawful bases: `Consent`, `LegitimateInterest`, `ContractualNecessity`, `LegalObligation` (Article 6(1)(a-f))
    - And duplicate consent (same channel + same purpose) is idempotent — returns existing record, no new event
    - And the channel ID must reference an existing contact channel on the party — reject with `ContactChannelNotFound` if invalid
    - And consent records are part of the aggregate state and included in the party detail projection
    - And consent records survive channel removal — they are historical records (the channel existed when consent was granted); orphaned consent records remain valid for audit purposes

2. **Consent Revocation (FR48)**
    - Given an active consent record
    - When an administrator revokes consent
    - Then the consent is marked revoked with a timestamp
    - And the revocation is recorded as a domain event (processing activity log, Article 30)
    - And revoking an already-revoked consent is idempotent — no new event
    - And revoking a non-existent consent returns `ConsentNotFound` rejection event

3. **Right to Restriction — Freeze Processing (FR49)**
    - Given a party under investigation
    - When an administrator restricts processing
    - Then the party's data is frozen — no modifications allowed while restricted
    - And read access continues (data is NOT erased, just frozen)
    - And the following commands are BLOCKED: all party modification, contact channel, identifier, lifecycle, composite, and key rotation commands
    - And the following commands are ALLOWED during restriction: `RecordConsent`, `RevokeConsent` (Article 18(3) requires consent management), `LiftRestriction`, `EraseParty` (Article 17 supersedes restriction)
    - And restriction status is visible in party detail read endpoints (`IsRestricted`, `RestrictedAt` fields in `PartyDetail`)
    - And restricting an already-restricted party is idempotent — no new event

4. **Lift Restriction — Resume Processing (FR50)**
    - Given a restricted party
    - When an administrator lifts the restriction
    - Then processing resumes normally — all commands are accepted again
    - And the restriction period is recorded in the event stream (processing activity log)
    - And lifting restriction on a non-restricted party returns `PartyNotRestricted` rejection event

5. **Data Portability Export (FR51)**
    - Given a data portability request
    - When an administrator triggers export for a party
    - Then all party data is exported in machine-readable JSON format
    - And the export includes: party details (type, names, person/org fields), all contact channels, all identifiers, all consent records (active and revoked)
    - And personal data fields are decrypted in the export (portability requires readable data)
    - And the export request is logged as a processing activity
    - And export is rejected with 409 Conflict if the party's `ErasureStatus != Active` (erasure requested means data subject has withdrawn consent for processing — exporting during erasure is legally questionable)
    - And export is rejected with 410 Gone if the party is fully erased

6. **Processing Records — Article 30 Compliance (FR52)**
    - Given any processing activity on party data
    - When the activity completes
    - Then a complete, time-stamped record is maintained in the event stream
    - And records support Article 30 compliance reporting via a query endpoint
    - And the processing records endpoint returns: event type, timestamp, and activity summary for all events related to a party

## Tasks / Subtasks

- [x] Task 1: Define consent domain contracts (AC: #1, #2)
    - [x] 1.1 Create `LawfulBasis` enum in `Contracts/Security/` — values: `Consent`, `LegitimateInterest`, `ContractualNecessity`, `LegalObligation`
    - [x] 1.2 Create `ConsentRecord` value object in `Contracts/ValueObjects/` — sealed record with `ConsentId` (string), `ChannelId` (string), `Purpose` (string), `LawfulBasis` (LawfulBasis), `GrantedAt` (DateTimeOffset), `GrantedBy` (string — admin identity from JWT), `RevokedAt` (DateTimeOffset?), `RevokedBy` (string? — admin identity); computed `IsActive => RevokedAt is null`
    - [x] 1.3 Create `RecordConsent` command in `Contracts/Commands/` — sealed record with `PartyId`, `TenantId`, `ChannelId`, `Purpose`, `LawfulBasis`
    - [x] 1.4 Create `RevokeConsent` command in `Contracts/Commands/` — sealed record with `PartyId`, `TenantId`, `ConsentId`
    - [x] 1.5 Create `ConsentRecorded` event in `Contracts/Events/` — sealed record implementing `IEventPayload` with `PartyId`, `TenantId`, `ConsentId`, `ChannelId`, `Purpose`, `LawfulBasis`, `GrantedAt`, `GrantedBy` (admin identity from JWT claims)
    - [x] 1.6 Create `ConsentRevoked` event in `Contracts/Events/` — sealed record implementing `IEventPayload` with `PartyId`, `TenantId`, `ConsentId`, `RevokedAt`, `RevokedBy` (admin identity from JWT claims)
    - [x] 1.7 Create `ConsentNotFound` rejection event in `Contracts/Events/` — sealed record implementing `IRejectionEvent` with `PartyId`, `TenantId`, `ConsentId`

- [x] Task 2: Define restriction domain contracts (AC: #3, #4)
    - [x] 2.1 Create `RestrictProcessing` command in `Contracts/Commands/` — sealed record with `PartyId`, `TenantId`, `Reason` (string, optional)
    - [x] 2.2 Create `LiftRestriction` command in `Contracts/Commands/` — sealed record with `PartyId`, `TenantId`
    - [x] 2.3 Create `ProcessingRestricted` event in `Contracts/Events/` — sealed record implementing `IEventPayload` with `PartyId`, `TenantId`, `RestrictedAt`, `Reason`
    - [x] 2.4 Create `RestrictionLifted` event in `Contracts/Events/` — sealed record implementing `IEventPayload` with `PartyId`, `TenantId`, `LiftedAt`
    - [x] 2.5 Create `PartyNotRestricted` rejection event in `Contracts/Events/` — sealed record implementing `IRejectionEvent` with `PartyId`, `TenantId`. NOTE: No `PartyAlreadyRestricted` event needed — idempotent restriction returns `DomainResult.NoOp()`, not a rejection (consistent with erasure idempotency pattern)

- [x] Task 3: Implement aggregate consent command handling (AC: #1, #2)
    - [x] 3.1 Add `IReadOnlyList<ConsentRecord> ConsentRecords` to `PartyState`
    - [x] 3.2 Add `Handle(RecordConsent, PartyState?)` to `PartyAggregate`
    - [x] 3.3 Add `Handle(RevokeConsent, PartyState?)` to `PartyAggregate`
    - [x] 3.4 Add `Apply(ConsentRecorded)` to `PartyAggregate`
    - [x] 3.5 Add `Apply(ConsentRevoked)` to `PartyAggregate`
    - [x] 3.6 Consent commands ARE allowed during restriction (Article 18(3))
    - [x] 3.7 Orphaned consent records survive channel removal

- [x] Task 4: Implement aggregate restriction command handling (AC: #3, #4)
    - [x] 4.1 Add `bool IsRestricted`, `DateTimeOffset? RestrictedAt`, `string? RestrictionReason` to `PartyState`
    - [x] 4.2 Add `Handle(RestrictProcessing, PartyState?)` to `PartyAggregate`
    - [x] 4.3 Add `Handle(LiftRestriction, PartyState?)` to `PartyAggregate`
    - [x] 4.4 Add `Apply(ProcessingRestricted)` to `PartyAggregate`
    - [x] 4.5 Add `Apply(RestrictionLifted)` to `PartyAggregate`
    - [x] 4.6 Add private `RejectIfRestricted(PartyState)` method
    - [x] 4.7 Reflection-based fitness test written first
    - [x] 4.7a `RejectIfRestricted()` guard added to all 8 simple modification Handle methods
    - [x] 4.7b `RejectIfRestricted()` guard added to lifecycle and key Handle methods
    - [x] 4.8 Consent, LiftRestriction, EraseParty, RestrictProcessing exempt from guard
    - [x] 4.9 `UpdatePartyComposite` has inline restriction check

- [x] Task 5: Update projections for consent and restriction (AC: #1, #3)
    - [x] 5.1 Add `IReadOnlyList<ConsentRecord> ConsentRecords` to `PartyDetail`
    - [x] 5.2 Add `bool IsRestricted` and `DateTimeOffset? RestrictedAt` to `PartyDetail`
    - [x] 5.3 `PartyDetailProjectionHandler` applies `ConsentRecorded`
    - [x] 5.4 `PartyDetailProjectionHandler` applies `ConsentRevoked`
    - [x] 5.5 `PartyDetailProjectionHandler` applies `ProcessingRestricted`
    - [x] 5.6 `PartyDetailProjectionHandler` applies `RestrictionLifted`
    - [x] 5.7 Index handler — skipped (optional, search doesn't need restriction status)

- [x] Task 6: Add admin consent, restriction, portability and processing records endpoints (AC: #1-#6)
    - [x] 6.1 `POST /api/v1/admin/parties/{partyId}/consent` endpoint
    - [x] 6.2 `DELETE /api/v1/admin/parties/{partyId}/consent/{consentId}` endpoint
    - [x] 6.3 `GET /api/v1/admin/parties/{partyId}/consent` endpoint
    - [x] 6.4 `POST /api/v1/admin/parties/{partyId}/restrict` endpoint
    - [x] 6.5 `POST /api/v1/admin/parties/{partyId}/lift-restriction` endpoint
    - [x] 6.6 `GET /api/v1/admin/parties/{partyId}/export` endpoint
    - [x] 6.7 `GET /api/v1/admin/parties/{partyId}/processing-records`
    - [x] 6.8 All endpoints use `[Authorize(Policy = "Admin")]` with tenant extraction

- [x] Task 7: Tier 1 unit tests (AC: #1-#6)
    - [x] 7.1–7.7 Consent aggregate Handle tests (7 tests)
    - [x] 7.8–7.11 Restriction aggregate Handle tests (4 tests)
    - [x] 7.12 Parameterized blocked commands test (12 commands)
    - [x] 7.13 Parameterized exempt commands test (4 commands)
    - [x] 7.14 Architectural fitness test (reflection-based)
    - [x] 7.15–7.18 Apply method state tests (4 tests)
    - [x] 7.19 Full consent sequence replay test
    - [x] 7.20 DetailHandler consent/restriction projection tests (5 tests)

- [x] Task 8: Tier 2 integration tests (AC: #1-#6)
    - [x] 8.1 POST consent auth tests (401, 403, 200)
    - [x] 8.2 DELETE consent/{id} returns 200
    - [x] 8.3 GET consent returns all records
    - [x] 8.4 POST restrict returns 200
    - [x] 8.5 POST lift-restriction returns 200
    - [x] 8.6 UpdatePersonDetails for restricted party returns 422
    - [x] 8.7 RecordConsent for restricted party succeeds
    - [x] 8.8 RevokeConsent for restricted party succeeds
    - [x] 8.9 EraseParty for restricted party succeeds
    - [x] 8.10 Restriction lifecycle interaction test
    - [ ] 8.11 _(DEFERRED)_ MCP restriction enforcement — pre-existing MCP architectural issue
    - [x] 8.12 GET export returns complete party JSON
    - [x] 8.13 GET export for restricted party succeeds
    - [x] 8.14 GET export for erasure-pending party returns 409
    - [x] 8.15 GET export for erased party returns 410
    - [x] 8.16 GET processing-records

- [x] Task 9: Tier 3 E2E tests (AC: #1, #3, #5)
    - [x] 9.1 Full topology: create party → record consent → verify in detail
    - [x] 9.2 Full topology: restrict → update rejected → lift → update succeeds
    - [x] 9.3 Full topology: create party with channels → export → verify JSON
    - [x] 9.4 Full topology: restrict → record consent → succeeds (Article 18(3))

## Dev Notes

### Architecture Decisions

**ADR: Consent as Aggregate State — Per-Channel, Per-Purpose Records**

Consent records are part of the `PartyAggregate` state, tracked via domain events (`ConsentRecorded`, `ConsentRevoked`). Each record links a specific `ChannelId` to a `Purpose` with a `LawfulBasis`. This is the correct DDD approach: consent is a domain concern of the Party aggregate, not an external service.

The `ConsentId` is deterministic — derived from `channelId + purpose` (e.g., hash or concatenation). This enables idempotency: recording the same consent twice returns NoOp without emitting duplicate events.

**ADR: Consent Record Growth and Aggregate Size**

The architecture document flags aggregate size as a first-order concern (50 channels + 10 identifiers is already significant). Consent records add `channels × purposes` entries that are never deleted (revoked records persist for audit). Worst case: 50 channels × 4 purposes = 200 consent records. This is acceptable because:

- Consent records are small value objects (~100 bytes each)
- 200 records × 100 bytes = ~20KB — negligible vs. event rehydration cost
- Revoked consents are essential historical audit records (Article 30)
- If consent volume becomes a concern in v2, consider: (a) separate consent projection actor, or (b) consent record archival after configurable retention period. For now, no pruning — audit completeness trumps size optimization.

**ADR: Orphaned Consent Records on Channel Removal**

When a contact channel is removed (`RemoveContactChannel`), existing consent records referencing that channel are NOT deleted or revoked. They are valid historical records — the channel existed when consent was granted. New consent cannot be recorded for a removed channel (channel validation rejects it), but existing records survive. This ensures audit trail completeness and avoids data loss.

**ADR: Restriction as Reversible State Toggle (Unlike Erasure State Machine)**

Restriction is a simple boolean toggle on PartyState (`IsRestricted`), NOT a multi-phase state machine like erasure. This is correct because:

- Restriction is reversible; erasure is permanent
- No background orchestration needed; just flip the flag
- No verification phase; restriction is immediate

State transitions:

```
Active ←→ Restricted (via RestrictProcessing / LiftRestriction)
Restricted → Erased (via EraseParty — erasure supersedes restriction)
```

**ADR: Restriction Guard — Aggregate-Level Enforcement**

Restriction is enforced at the aggregate level via a `RejectIfRestricted()` private method (same pattern as `RejectIfErasureInProgress()`). This is simpler and more reliable than extending `IPersonalDataCommandGuard`, because restriction affects ALL modifications, not just personal-data-bearing commands.

**ADR: Portability Export — Read from Projection (Not Event Stream)**

The portability export endpoint reads from the party detail projection, which already stores decrypted data. This avoids re-decrypting the event stream and leverages existing infrastructure. The projection includes all required data: party details, contact channels, identifiers, and (after this story) consent records.

**ADR: Article 30 Processing Records — Event Stream IS the Record**

The event stream already provides complete, time-stamped records of all processing activities. The processing records endpoint is a read-only query that retrieves and formats events — no new write infrastructure needed.

### CRITICAL: Existing Infrastructure to Reuse

**DO NOT re-implement any of this — it already exists and is tested:**

- `PartyAggregate.Handle()/Apply()` pattern — 18 existing Handle methods demonstrate the exact patterns to follow
- `RejectIfErasureInProgress()` — private method in aggregate, template for `RejectIfRestricted()`
- `ContactChannelNotFound` rejection event — already exists, reuse for invalid channel validation in `RecordConsent`
- `PartyErasureInProgress` rejection event — existing pattern for blocking rejection events
- `AdminController.cs` — 8 existing endpoints with auth, tenant extraction, and error handling patterns
- `PartyDetail` model — existing read model, extend with consent and restriction fields
- `PartyDetailProjectionHandler` — existing handler with Apply methods for all events
- `PartyTestData` — test data factory, extend with consent/restriction factories

**Key interfaces already available:**

```csharp
// Existing command pattern (follow exactly):
public sealed record EraseParty
{
    public required string PartyId { get; init; }
    public required string TenantId { get; init; }
}

// Existing event pattern (follow exactly):
public sealed record ErasePartyRequested : IEventPayload
{
    public required string PartyId { get; init; }
    public required string TenantId { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
}

// Existing rejection pattern (follow exactly):
public sealed record PartyErasureInProgress : IRejectionEvent
{
    public required string PartyId { get; init; }
    public required string TenantId { get; init; }
}
```

### Consent & Restriction Events via Pub/Sub

All new domain events (`ConsentRecorded`, `ConsentRevoked`, `ProcessingRestricted`, `RestrictionLifted`) are published to subscribers automatically via the existing pub/sub infrastructure — the EventStore publishes ALL aggregate events to `{tenant}.parties.events`. No additional pub/sub configuration is needed.

Note: `ConsentRecorded` contains `Purpose` and `LawfulBasis` which are NOT personal data — no `[PersonalData]` attribute needed on these fields. `ConsentRevoked` contains only `ConsentId` and timestamp. `ProcessingRestricted` contains only `Reason` (optional, not personal data). None of these events require field-level encryption.

### DeriveDisplayName and Restriction

`PartyDisplayNameDerived` is a system-generated event triggered within the Apply path of name-changing events (e.g., `PersonDetailsUpdated`). It is NOT a separate command — it runs inside Apply, not Handle. Therefore, no restriction guard is needed for display name derivation. The restriction guard on `UpdatePersonDetails` (which triggers the derivation) is sufficient.

### Consent Requires an Existing Channel

Consent can only be recorded for a channel that currently exists on the party. If a party has zero contact channels, `RecordConsent` will be rejected with `ContactChannelNotFound`. This is correct — you cannot consent to processing on a channel that doesn't exist yet. Add channels first, then record consent.

### Export Eventual Consistency

The export endpoint reads from the party detail projection, which may lag behind aggregate state by seconds (standard CQRS eventual consistency). If an admin records consent at T=0 and immediately exports at T=0.5s, the export might not include the just-recorded consent. This is inherent to the architecture — not a bug. If guaranteed-current data is needed, wait a moment after consent changes before exporting.

### Restriction Has No Automatic Timeout

Restriction persists indefinitely until explicitly lifted by any admin with the `Admin` policy. This is correct per GDPR — restriction duration is determined by the investigation timeline, not a timer. There is no auto-expiry mechanism. Any admin can lift restriction (not scoped to the admin who applied it). The `RestrictionReason` field helps other admins understand why the party is restricted.

### Recommended Purpose Values

Purpose is a free-form string to support diverse consuming applications. However, inconsistent purpose naming across apps degrades consent management quality. Document these recommended standard values in the Getting Started guide (Story 6-2):

- `marketing` — marketing communications
- `billing` — billing and invoicing
- `support` — customer support
- `analytics` — data analytics and reporting
- `legal-compliance` — legal and regulatory compliance
- `operational` — operational processing

These are recommendations, not enforced constraints. Consuming apps may define custom purposes.

### v2 Considerations

- **Party-level consent:** The current model is strictly per-channel per-purpose (as the PRD specifies). Some processing purposes (e.g., "analytics") apply to the party as a whole, not a specific channel. Consider allowing `ChannelId = "*"` or `null` for party-wide consent in v2.
- **Batch restriction:** The current API supports single-party restriction. For DPO scenarios requiring batch restriction (e.g., regulatory investigation affecting 50 parties), the admin portal (Epic 10, v1.2) would provide a batch UI. The current REST API supports sequential calls.
- **Consent-aware event filtering:** Consuming apps currently receive ALL events via pub/sub. If a consuming app needs only events for parties it has consent for, that filtering is the consuming app's responsibility (not the Parties service).

### Composite Command Atomicity

`UpdatePartyComposite` is handled as a single aggregate invocation — all sub-operations execute atomically. The restriction check happens once at the start of the Handle method. There is no race condition where restriction is applied between sub-operations, because the aggregate processes the composite as one unit.

### Restriction Guard Placement

The restriction guard must be added to ALL modification Handle methods. Follow this exact placement pattern:

```csharp
public static DomainResult Handle(UpdatePersonDetails command, PartyState? state)
{
    if (state is null) return DomainResult.Rejection(new PartyNotFound { ... });
    // Erasure check FIRST (permanent takes precedence)
    if (state.ErasureStatus is not ErasureStatus.Active)
        return RejectIfErasureInProgress(command, state);
    // Restriction check SECOND (reversible)
    if (state.IsRestricted)
        return RejectIfRestricted(command, state);
    // Business logic follows...
}
```

**Commands that need the restriction guard (exhaustive list):**

1. `UpdatePersonDetails`
2. `UpdateOrganizationDetails`
3. `SetIsNaturalPerson`
4. `AddContactChannel`
5. `UpdateContactChannel`
6. `RemoveContactChannel`
7. `AddIdentifier`
8. `RemoveIdentifier`
9. `DeactivateParty`
10. `ReactivateParty`
11. `RotatePartyKey`
12. `UpdatePartyComposite` (composite command)

**Mental model: Restriction blocks all USER-INITIATED modifications. System operations (erasure pipeline) and consent management continue.**

**Commands exempt from restriction guard:**

- `RecordConsent` — Article 18(3) requires consent management during restriction
- `RevokeConsent` — Article 18(3) requires consent management during restriction
- `LiftRestriction` — must be allowed to un-restrict
- `EraseParty` — Article 17 supersedes restriction
- `RestrictProcessing` — idempotent, already handles own guard
- `CreateParty` / `CreatePartyComposite` — new parties can't be restricted yet
- `MarkPartyEncryptionKeyDeleted` / `MarkErasureVerified` / `CompletePartyErasure` — internal erasure pipeline, must continue during restriction

### ConsentId Generation Strategy

Use deterministic ConsentId for idempotency:

```csharp
// Deterministic: same channel+purpose always produces same ID
// CRITICAL: Trim and normalize inputs to prevent near-duplicates
string consentId = $"{channelId.Trim()}:{purpose.Trim()}".ToLowerInvariant();
```

Input normalization is essential — without it, `"marketing"` and `"marketing "` (trailing space) or `"Marketing"` produce different ConsentIds, creating duplicate consent records. The aggregate state's ConsentRecords list is the source of truth.

### Data Portability Export Format

The export endpoint returns a JSON document structured for GDPR Article 20 compliance. **Implementation shortcut:** Serialize `PartyDetail` directly (it already contains party details, contact channels, identifiers, and consent records after this story) wrapped with an `exportedAt` timestamp. No custom DTO mapping needed — `PartyDetail` IS the export format:

```json
{
    "exportedAt": "2026-03-09T10:00:00Z",
    "partyId": "abc-123",
    "partyType": "Person",
    "details": {
        "firstName": "Jean",
        "lastName": "Dupont",
        "dateOfBirth": "1985-06-15"
    },
    "contactChannels": [
        {
            "id": "ch-1",
            "type": "Email",
            "value": "jean@example.com",
            "isPreferred": true
        }
    ],
    "identifiers": [
        { "id": "id-1", "type": "NationalId", "value": "1234567890" }
    ],
    "consentRecords": [
        {
            "consentId": "ch-1:marketing",
            "channelId": "ch-1",
            "purpose": "marketing",
            "lawfulBasis": "Consent",
            "grantedAt": "2025-09-01T00:00:00Z",
            "revokedAt": null,
            "isActive": true
        }
    ]
}
```

### Processing Records Endpoint

The processing records endpoint queries the event stream (via EventStore aggregate event retrieval) and returns a summary:

```json
{
    "partyId": "abc-123",
    "records": [
        {
            "timestamp": "2025-06-01T10:00:00Z",
            "activity": "PartyCreated",
            "summary": "Party record created"
        },
        {
            "timestamp": "2025-06-01T10:01:00Z",
            "activity": "ContactChannelAdded",
            "summary": "Email contact added"
        },
        {
            "timestamp": "2025-09-01T00:00:00Z",
            "activity": "ConsentRecorded",
            "summary": "Consent recorded for marketing via email"
        },
        {
            "timestamp": "2026-03-01T10:00:00Z",
            "activity": "ProcessingRestricted",
            "summary": "Processing restricted: investigation"
        }
    ]
}
```

This requires reading the aggregate's event stream. **Implementation approach:**

1. Check `Hexalith.EventStore` for an existing `IEventStreamReader` or `IEventStore.GetEventsAsync(aggregateId)` method — the aggregate replay mechanism already reads events for rehydration
2. If no public read API exists: the processing records endpoint can alternatively read from the party detail projection's event history (if the projection tracks event metadata) — this avoids coupling to EventStore internals
3. **Implemented path:** Reuse `ProjectionRebuildService` aggregate event reads over persisted actor-state envelopes to power the endpoint without introducing a new EventStore public API surface
4. If implemented, map event type names to human-readable activity summaries via a static dictionary (e.g., `"PartyCreated" → "Party record created"`, `"ConsentRecorded" → "Consent recorded"`)

### Failure Mode Mitigations

| Component                  | Failure Mode                                       | Mitigation                                                                                                                                                |
| -------------------------- | -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Consent recording          | Invalid channel ID                                 | Validate against state's ContactChannels; reject with existing `ContactChannelNotFound`                                                                   |
| Consent recording          | Duplicate consent                                  | Idempotent: deterministic ConsentId detects duplicates, returns NoOp                                                                                      |
| Consent revocation         | Non-existent consent                               | Reject with `ConsentNotFound`                                                                                                                             |
| Consent revocation         | Already revoked                                    | Idempotent: return NoOp                                                                                                                                   |
| Restriction                | Already restricted                                 | Idempotent: return NoOp                                                                                                                                   |
| Lift restriction           | Not restricted                                     | Reject with `PartyNotRestricted`                                                                                                                          |
| Restriction bypass         | In-flight commands                                 | Aggregate state check is atomic — commands arriving during restriction transition see the restricted state via ETag concurrency (same pattern as erasure) |
| Portability export         | Encrypted data                                     | Read from projection (already decrypted); if party is erased, return 410 Gone (erasure check before export)                                               |
| Portability export         | Erasure-pending party                              | Return 409 Conflict — erasure requested means data subject has withdrawn consent for processing; exporting during erasure is legally questionable         |
| Portability export         | Restricted party                                   | Export succeeds — portability (Article 20) is a separate right from restriction (Article 18); restriction only blocks modifications                       |
| Portability export         | Party not found                                    | Return 404 Not Found                                                                                                                                      |
| Consent recording          | Whitespace/case in Purpose creates near-duplicates | Trim + ToLowerInvariant on both ChannelId and Purpose before generating ConsentId; reject empty Purpose after trim                                        |
| Consent recording          | Invalid Purpose format                             | Validate: non-empty, max 100 chars, alphanumeric + hyphens + underscores only; reject with descriptive error                                              |
| PartyState deserialization | Existing parties fail after schema change          | All new fields use safe defaults: `ConsentRecords = []`, `IsRestricted = false`, nullable timestamps default to `null`; no migration needed               |
| MCP tools                  | Update via MCP during restriction                  | Aggregate-level enforcement covers ALL dispatch paths (REST, MCP, internal); verified by Tier 2 MCP-specific test                                         |
| Processing records         | No events found                                    | Return empty list (valid — party might be newly created)                                                                                                  |

### Previous Story Intelligence (Story 9-3)

From Story 9-3 implementation (in review):

- **Erasure state machine** established: `Active → ErasurePending → KeyDestroyed → VerificationInProgress → Verified → Erased` — restriction must coexist with this
- **`RejectIfErasureInProgress()` pattern** — exact template for `RejectIfRestricted()`
- **Admin endpoint pattern** confirmed: `[Authorize(Policy = "Admin")]` + tenant extraction + correlation ID
- **`ErasureVerificationService`** follows `ProjectionRebuildService` pattern — no similar service needed for consent/restriction (synchronous operations)
- **NSubstitute + DaprClient nullable generics:** may need `#pragma warning disable CS8620` (documented debug fix)
- **LoggerMessage pattern** for structured logging — use partial methods with `[LoggerMessage]` attribute for any new logging
- **Projection handler Apply pattern** — pure static methods (D18), Tier 1 testable

### Git Intelligence

Recent commits show:

- `6eb16e9` — MCP update tool + `IPersonalDataCommandGuard` interface (latest)
- `8ba6f11` — EventStore submodule update with GDPR payload protection
- `2c42713` — Stories 9-2 and 9-3 implementation (field-level encryption + erasure)

The command guard (`IPersonalDataCommandGuard`) was just introduced. For restriction, aggregate-level enforcement is preferred over extending the guard, because restriction blocks ALL commands (not just personal-data-bearing ones). However, the guard COULD be extended in a future story if restriction needs to be checked at the API layer before dispatch.

### Testing Standards

**Tier 1 (Unit — ~50% effort):** Pure aggregate Handle/Apply logic, projection handler Apply methods

- Mock nothing — aggregate and handler methods are pure static functions
- Test each Handle method: success path, idempotent path, rejection path
- Test Apply methods: state mutations
- Test restriction guard: parameterized tests covering ALL 12 blocked commands and ALL 4 exempt commands (not spot-check — exhaustive)
- Architectural fitness test: reflection-based test verifying all Handle methods call `RejectIfRestricted()` — catches future Handle methods that forget the guard (GDPR compliance safety net)
- Use `PartyTestData` factory — extend with `CreateRestrictedState()`, `ValidRecordConsent()`, `ValidRestrictProcessing()`, etc.
- Naming: `{Method}_{Scenario}_{ExpectedResult}`, Shouldly assertions

**Tier 2 (Integration — ~40% effort):** WebApplicationFactory + mocked DAPR

- Admin endpoint auth tests (401/403/200 pattern — note: consent/restriction endpoints are synchronous, so 200 not 202)
- Consent lifecycle: record → list → revoke → list (shows revoked)
- Restriction lifecycle: restrict → attempt update (rejected) → consent (allowed) → lift → update (allowed)
- Export: create party with data → export → verify JSON completeness
- Processing records: create party → perform operations → query records → verify completeness

**Tier 3 (E2E — ~10% effort):** Full Aspire topology

- Consent: create party → record consent → verify in party detail projection
- Restriction: restrict → update rejected → lift → update succeeds
- Export: create party → export → verify JSON

### Project Structure Notes

**New files to create:**

```
src/
  Hexalith.Parties.Contracts/
    Commands/
      RecordConsent.cs                          # New command
      RevokeConsent.cs                          # New command
      RestrictProcessing.cs                     # New command
      LiftRestriction.cs                        # New command
    Events/
      ConsentRecorded.cs                        # New domain event
      ConsentRevoked.cs                         # New domain event
      ConsentNotFound.cs                        # New rejection event
      ProcessingRestricted.cs                   # New domain event
      RestrictionLifted.cs                      # New domain event
      PartyNotRestricted.cs                     # New rejection event (no PartyAlreadyRestricted — idempotent restriction returns NoOp)
    Security/
      LawfulBasis.cs                            # New enum
    ValueObjects/
      ConsentRecord.cs                          # New value object

tests/
  Hexalith.Parties.Server.Tests/
    Aggregates/
      PartyAggregateConsentTests.cs             # Tier 1: consent Handle/Apply
      PartyAggregateRestrictionTests.cs         # Tier 1: restriction Handle/Apply + guard
  Hexalith.Parties.CommandApi.Tests/
    Controllers/
      ConsentEndpointTests.cs                   # Tier 2: consent admin endpoints
      RestrictionEndpointTests.cs               # Tier 2: restriction admin endpoints
      PortabilityEndpointTests.cs               # Tier 2: export + processing records
  Hexalith.Parties.IntegrationTests/
    Security/
      ConsentRestrictionE2ETests.cs             # Tier 3: full topology tests
```

**Files to modify:**

```
src/
  Hexalith.Parties.Contracts/
    State/
      PartyState.cs                             # Add ConsentRecords, IsRestricted, RestrictedAt, RestrictionReason
    Models/
      PartyDetail.cs                            # Add ConsentRecords, IsRestricted, RestrictedAt
  Hexalith.Parties.Server/
    Aggregates/
      PartyAggregate.cs                         # Add 4 Handle methods, 4 Apply methods, RejectIfRestricted() guard, add guard to 12 existing Handle methods
  Hexalith.Parties.Projections/
    Handlers/
      PartyDetailProjectionHandler.cs           # Add Apply methods for consent/restriction events
  Hexalith.Parties.CommandApi/
    Controllers/
      AdminController.cs                        # Add 7 new endpoints (consent CRUD, restrict/lift, export, processing-records)
  Hexalith.Parties.Testing/
    PartyTestData.cs                            # Add consent/restriction test data factories
```

**Alignment with existing patterns:**

- Commands: sealed records in `Commands/` folder, `PartyId` + `TenantId` required properties
- Events: sealed records implementing `IEventPayload` or `IRejectionEvent` in `Events/` folder
- Value objects: sealed records in `ValueObjects/` folder (same as `ContactChannel`, `PartyIdentifier`)
- Enums: in `Security/` folder for GDPR-related enums
- Aggregate: static `Handle()` and `Apply()` methods in `PartyAggregate`
- Admin endpoints: `[Authorize(Policy = "Admin")]`, tenant extraction, consistent error responses
- Tests: `{Method}_{Scenario}_{ExpectedResult}`, Shouldly, NSubstitute

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9, Story 9.4]
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns - GDPR/Privacy]
- [Source: _bmad-output/planning-artifacts/architecture.md#Design Principle 4 - Conservative Privacy]
- [Source: _bmad-output/planning-artifacts/prd.md#FR47 - Per-Channel Per-Purpose Consent]
- [Source: _bmad-output/planning-artifacts/prd.md#FR48 - Consent Revocation]
- [Source: _bmad-output/planning-artifacts/prd.md#FR49 - Right to Restriction]
- [Source: _bmad-output/planning-artifacts/prd.md#FR50 - Lift Restriction]
- [Source: _bmad-output/planning-artifacts/prd.md#FR51 - Data Portability Export]
- [Source: _bmad-output/planning-artifacts/prd.md#FR52 - Processing Records Article 30]
- [Source: _bmad-output/planning-artifacts/prd.md#Journey 4 - Laurent GDPR Erasure (consent review, restriction workflow)]
- [Source: _bmad-output/planning-artifacts/prd.md#GDPR Compliance Section - Articles 6, 18, 20, 30]
- [Source: _bmad-output/implementation-artifacts/9-3-right-to-erasure-and-verification.md#Dev Notes]
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs - Handle/Apply patterns, RejectIfErasureInProgress]
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs - Current state properties]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs - Read model]
- [Source: src/Hexalith.Parties.Contracts/ValueObjects/ContactChannel.cs - Channel model]
- [Source: src/Hexalith.Parties.Contracts/Security/IPersonalDataCommandGuard.cs - Guard pattern]
- [Source: src/Hexalith.Parties.CommandApi/Controllers/AdminController.cs - Admin endpoint patterns]
- [Source: src/Hexalith.Parties.Testing/PartyTestData.cs - Test data factory]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

### Completion Notes List

- Implemented the missing `GET /api/v1/admin/parties/{partyId}/processing-records` endpoint by reusing `ProjectionRebuildService` aggregate event reads and mapping persisted event envelopes to `ProcessingActivityRecord` summaries.
- Propagated JWT `sub` into consent commands via `ActorUserId`, removing the hardcoded `"admin"` audit identity and ensuring `GrantedBy` / `RevokedBy` reflect the authenticated administrator.
- Added `InvalidConsentPurpose` and enriched rejection contracts (`ConsentNotFound`, `PartyNotRestricted`, `PartyProcessingRestricted`) so domain rejections now carry the party/tenant context claimed by the story and tests.
- Normalized admin GDPR domain rejections to `422 Unprocessable Entity` and fixed export behavior to prefer erasure-status store state over lagging projections, returning `410 Gone` for erased parties even when the read model is stale.
- Validation completed with `Hexalith.Parties.Server.Tests` passing (`177/177`) and focused command API GDPR suites passing (`29/29` across consent, restriction, and portability tests).
- A pre-existing unrelated failure remains in the full `Hexalith.Parties.CommandApi.Tests` project: `ArchitecturalFitnessTests.McpNamespace_ReferencesOnlyCommandAndModelTypes` for `UpdatePartyMcpTool` referencing `IPersonalDataCommandGuard`.

### File List

- `src/Hexalith.Parties.Contracts/Commands/RecordConsent.cs` — added `ActorUserId` so consent audit events use the authenticated admin identity.
- `src/Hexalith.Parties.Contracts/Commands/RevokeConsent.cs` — added `ActorUserId` for revocation audit provenance.
- `src/Hexalith.Parties.Contracts/Events/ConsentNotFound.cs` — enriched rejection payload with party, tenant, and consent identifiers.
- `src/Hexalith.Parties.Contracts/Events/PartyNotRestricted.cs` — enriched restriction rejection payload with party context.
- `src/Hexalith.Parties.Contracts/Events/PartyProcessingRestricted.cs` — enriched blocked-command rejection payload with party context.
- `src/Hexalith.Parties.Contracts/Events/InvalidConsentPurpose.cs` — added explicit rejection for consent purpose validation failures.
- `src/Hexalith.Parties.Contracts/Models/ProcessingActivityRecord.cs` — added response model for Article 30 processing activity summaries.
- `src/Hexalith.Parties.Projections/Services/IProjectionRebuildService.cs` — exposed processing-record query contract.
- `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs` — implemented processing-record retrieval and event summary mapping.
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` — fixed consent identity propagation, invalid-purpose rejection typing, richer rejection payloads, and restriction rejection handling.
- `src/Hexalith.Parties.CommandApi/Controllers/AdminController.cs` — added processing-records endpoint, JWT subject extraction, `422` domain rejection handling, and corrected export erased-vs-lagging-projection behavior.
- `src/Hexalith.Parties.Testing/PartyTestData.cs` — updated consent test fixtures with actor identity defaults.
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateConsentTests.cs` — added/updated consent aggregate coverage for identities and rejections.
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateRestrictionTests.cs` — added/updated restriction rejection payload assertions.
- `tests/Hexalith.Parties.CommandApi.Tests/Controllers/ConsentEndpointTests.cs` — verified `422` admin rejection behavior and JWT subject propagation.
- `tests/Hexalith.Parties.CommandApi.Tests/Controllers/PortabilityEndpointTests.cs` — covered erased-vs-lagging-projection export behavior and processing-records responses.
