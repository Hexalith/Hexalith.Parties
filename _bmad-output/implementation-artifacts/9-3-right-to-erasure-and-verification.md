# Story 9.3: Right to Erasure & Verification

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want to trigger erasure that cryptographically destroys a party's personal data and verify completeness,
so that GDPR Article 17 right-to-erasure requests are fulfilled with automated verification.

## Acceptance Criteria

1. **Erasure Command & Aggregate Terminal State**
    - Given an administrator triggers erasure for a party
    - When the erasure command is processed
    - Then the party aggregate transitions to a terminal "Erased" state
    - And all subsequent commands for this party are rejected with "party erased" error
    - And a distributed lock prevents in-flight commands from racing with erasure
    - And the party's per-party encryption key is destroyed via DAPR secret store (FR44)
    - And all personal data in events and snapshots becomes permanently unreadable
    - And event metadata (types, timestamps, aggregate IDs) survives — personal data doesn't
    - And an erasure certificate is generated with timestamp and key versions destroyed

2. **Erasure Verification Across All Data Stores**
    - Given key destruction is complete
    - When the erasure verification job runs
    - Then it verifies erasure across all internal data stores (FR45):
        - Detail projection actor state: personal data fields nullified or actor state cleared
        - Index projection actor state: party entry removed from search indexes
        - In-memory caches: explicitly invalidated (not TTL-dependent)
    - And each data store check is itemized in the verification report with timestamp and result
    - And the report is produced within 5 minutes of erasure trigger
    - And verification is resumable — partial failures retry from the last successful checkpoint
    - And concurrent erasure requests for the same party are idempotent (return existing certificate)

3. **PartyErased Event & Subscriber Notification**
    - Given internal verification is complete
    - When a `PartyErased` event is published via DAPR pub/sub
    - Then all subscribers are notified with partyId and tenantId (FR46)
    - And delivery is tracked per subscriber — unacknowledged erasures alert after configurable timeout
    - And notification is decoupled from internal verification — internal cleanup succeeds independently
    - And failed deliveries retry via DAPR pub/sub retry policy, then dead-letter with alert

4. **Erased Party Read Behavior**
    - Given an erased party
    - When a read request is made via any API path (REST, MCP, query)
    - Then the response returns an "erased" status — not decryption errors (FR55)
    - And the read path checks erasure state BEFORE attempting any decryption
    - And ALL read endpoints consistently return the erased status (sweep coverage)

5. **Key Destruction Failure Handling**
    - Given key destruction fails
    - When the retry policy is exhausted
    - Then an alert is raised via observability metrics
    - And the party remains in "erasure-pending" state (not terminal "erased")
    - And erasure verification is blocked until key destruction succeeds
    - And the administrator sees clear status: "Key destruction failed — retry or escalate"

6. **Erasure Audit Trail & Compliance Artifacts**
    - Given erasure completes (all phases)
    - When the erasure record is finalized
    - Then the erasure certificate is persisted in the append-only audit trail
    - And the verification report is retained for Article 30 compliance
    - And the audit trail survives independently of the erased party data
    - And the erasure certificate is retrievable by the administrator

## Tasks / Subtasks

- [x] Task 1: Define erasure domain events and contracts (AC: #1, #6)
    - [x] 1.1 Create `ErasePartyRequested` event in `Contracts/Events/` — record with `PartyId`, `TenantId`, `RequestedAt`, `RequestedBy` (admin identity)
    - [x] 1.2 Create `PartyErased` event in `Contracts/Events/` — record with `PartyId`, `TenantId`, `ErasedAt` (this is the subscriber-facing event, FR46)
    - [x] 1.3 Create `ErasureVerified` event in `Contracts/Events/` — record with `PartyId`, `TenantId`, `VerifiedAt`, `VerificationReportId`
    - [x] 1.4 Create `ErasePartyCommand` in `Contracts/Commands/` — command with `PartyId`, `TenantId`
    - [x] 1.5 Create `ErasureVerificationReport` record in `Contracts/Security/` — with `PartyId`, `TenantId`, `Timestamp`, `StoreResults` (list of per-store check results: store name, status, timestamp, error message), `OverallStatus` (Complete, Partial, Failed)
    - [x] 1.6 Create `ErasureVerificationStoreResult` record in `Contracts/Security/` — per-store result with `StoreName`, `Status` (Cleaned, Failed, Skipped), `Timestamp`, `ErrorMessage`
    - [x] 1.7 Create `ErasureStatus` enum in `Contracts/Security/` — `Active`, `ErasurePending`, `KeyDestroyed`, `VerificationInProgress`, `Verified`, `Erased`
    - [x] 1.8 Create `IErasureVerificationService` interface in `Contracts/Security/` — `VerifyErasureAsync(tenantId, partyId, erasureCertificate)` returning `ErasureVerificationReport`

- [x] Task 2: Implement aggregate erasure command handling (AC: #1, #5)
    - [x] 2.1 Add `ErasePartyCommand` handler to party aggregate — validate party exists, is not already erased, transition to `ErasurePending` state
    - [x] 2.2 Emit `ErasePartyRequested` event from aggregate Handle method
    - [x] 2.3 In aggregate Apply method: set `ErasureStatus = ErasurePending` on `ErasePartyRequested`; reject ALL subsequent commands (except idempotent re-erasure) when status is `ErasurePending` or `Erased`
    - [x] 2.4 Apply `PartyEncryptionKeyDeleted` in aggregate: transition to `KeyDestroyed` state
    - [x] 2.5 Apply `ErasureVerified` in aggregate: transition to `Verified` state
    - [x] 2.6 Apply `PartyErased` in aggregate: transition to terminal `Erased` state — aggregate rejects ALL commands permanently
    - [x] 2.7 Idempotent erasure: if already `Erased`, return existing erasure certificate from state, emit no new events
    - [x] 2.8 CRITICAL: Use ETag-guarded state update to prevent race conditions with in-flight commands — commands arriving after `ErasePartyRequested` must fail with "party erasure in progress"

- [x] Task 3: Implement erasure orchestration — key destruction phase (AC: #1, #5)
    - [x] 3.1 Create `PartyErasureOrchestrator` service (or extend aggregate lifecycle) — triggered after `ErasePartyRequested` event
    - [x] 3.2 Call `IPartyKeyManagementService.DeleteKeyAsync(tenantId, partyId)` — returns `ErasureCertificate` (already implemented in Story 9-1)
    - [x] 3.3 On success: emit `PartyEncryptionKeyDeleted` event, persist erasure certificate to DAPR state store at `{tenant}:erasure:{partyId}`
    - [x] 3.4 On failure: party stays `ErasurePending`, configure DAPR reminder-based retry (pattern from Story 8-3 / Story 9-1 CryptoPending), raise `parties.erasure.key_destruction_failed` metric
    - [x] 3.5 On retry exhaustion (configurable, default 5 retries over 15 minutes): raise alert, log with `LoggerMessage` pattern, admin status shows "Key destruction failed"

- [x] Task 4: Implement ErasureVerificationService — projection cleanup phase (AC: #2)
    - [x] 4.1 Create `ErasureVerificationService` in `Hexalith.Parties.Security/` — follows `ProjectionRebuildService` pattern from Story 8-3
    - [x] 4.2 Triggered internally after `PartyEncryptionKeyDeleted` event is applied
    - [x] 4.3 Add `ApplyErasure(string partyId)` method to `PartyDetailProjectionHandler` — nullifies all personal data fields, sets `IsErased = true`, preserves aggregate ID and tenant ID
    - [x] 4.4 Add `ApplyErasure(string partyId)` method to `PartyIndexProjectionHandler` — returns `null` (removes entry from index entirely)
    - [x] 4.5 Add `EraseAsync(string partyId)` to `IPartyDetailProjectionActor` and `IPartyIndexProjectionActor` interfaces — actor wraps handler call, persists cleaned/deleted state
    - [x] 4.6 Call each projection actor's `EraseAsync` method, capture per-store result (StoreName, Status, Timestamp)
    - [x] 4.7 Invalidate any in-memory caches explicitly for the erased partyId — NOT TTL-dependent
    - [x] 4.8 Handle actor deactivation: if actor is deactivated during cleanup, `EraseAsync` call activates it, handler checks `IsErased` flag on activation
    - [x] 4.9 Handle corrupted actor state (D15 pattern): if deserialization fails for erased party, treat as "already cleaned" — corrupted state + destroyed key = no data recoverable
    - [x] 4.10 Generate `ErasureVerificationReport` with per-store results, overall status, timestamps
    - [x] 4.11 Persist verification report to DAPR state store at `{tenant}:erasure-report:{partyId}`
    - [x] 4.12 Checkpoint-based progress: persist cleanup progress so partial failures can resume from last successful store
    - [x] 4.13 Emit `ErasureVerified` event when all stores verified clean

- [x] Task 5: Implement PartyErased event publication and subscriber tracking (AC: #3)
    - [x] 5.1 After `ErasureVerified`, publish `PartyErased` event to DAPR pub/sub topic `{tenant}.parties.events`
    - [x] 5.2 Decouple from internal verification — internal cleanup succeeds even if pub/sub publish fails
    - [x] 5.3 On publish failure: persist "publish pending" state, retry via DAPR reminder
    - [x] 5.4 Track delivery acknowledgment per subscriber (via DAPR pub/sub delivery tracking if available, or application-level tracking)
    - [x] 5.5 Alert on unacknowledged erasure after configurable timeout (default: 48 hours, matching Laurent's journey timeline)
    - [x] 5.6 Emit `PartyErased` as final event in aggregate — Apply method transitions to terminal `Erased` state

- [x] Task 6: Implement erased party read behavior (AC: #4)
    - [x] 6.1 Add erasure state check to party detail read path — check `IsErased` flag BEFORE any decryption attempt
    - [x] 6.2 Return `PartyErasedResponse` (or 410 Gone with erasure metadata) instead of party details when `IsErased = true`
    - [x] 6.3 Add erasure check to party search/index query — erased parties excluded from search results
    - [x] 6.4 Add erasure check to MCP `get-party` tool — return "party erased" status
    - [x] 6.5 Add erasure check to MCP `find-parties` tool — exclude erased parties from results
    - [x] 6.6 CRITICAL: Ensure read path checks erasure state before decryption — never attempt to decrypt with a destroyed key (would cause cryptographic error, not user-friendly "erased" message)

- [x] Task 7: Add admin erasure endpoints (AC: #1, #2, #6)
    - [x] 7.1 Add `POST /api/v1/admin/parties/{partyId}/erase` endpoint — `[Authorize(Policy = "Admin")]`, returns `202 Accepted` with correlation ID (same pattern as key rotation endpoint)
    - [x] 7.2 Add `GET /api/v1/admin/parties/{partyId}/erasure-status` endpoint — returns current `ErasureStatus` enum value + per-store verification results if available
    - [x] 7.3 Add `GET /api/v1/admin/parties/{partyId}/erasure-certificate` endpoint — returns `ErasureCertificate` + `ErasureVerificationReport` as compliance artifact
    - [x] 7.4 Add `POST /api/v1/admin/parties/{partyId}/retry-verification` endpoint — retriggers verification for stores that failed (partial failure recovery)

- [x] Task 8: Tier 1 unit tests (AC: #1, #2, #4, #5, #6)
    - [x] 8.1 Aggregate: `Handle_ErasePartyCommand_EmitsErasePartyRequestedEvent`
    - [x] 8.2 Aggregate: `Handle_ErasePartyCommand_AlreadyErased_ReturnsExistingCertificate`
    - [x] 8.3 Aggregate: `Handle_AnyCommand_WhenErasurePending_RejectsWithErasureError`
    - [x] 8.4 Aggregate: `Handle_AnyCommand_WhenErased_RejectsWithErasureError`
    - [x] 8.5 Aggregate: `Apply_ErasePartyRequested_SetsErasurePendingStatus`
    - [x] 8.6 Aggregate: `Apply_PartyErased_SetsTerminalErasedState`
    - [x] 8.7 DetailHandler: `ApplyErasure_NullifiesPersonalDataFields_PreservesAggregateId`
    - [x] 8.8 DetailHandler: `ApplyErasure_SetsIsErasedFlag`
    - [x] 8.9 IndexHandler: `ApplyErasure_ReturnsNull_RemovesFromIndex`
    - [x] 8.10 VerificationService: `VerifyErasure_AllStoresClean_ReturnsCompleteReport`
    - [x] 8.11 VerificationService: `VerifyErasure_OneStoreFails_ReturnsPartialReport`
    - [x] 8.12 VerificationService: `VerifyErasure_ActorCorrupted_TreatsAsClean`
    - [x] 8.13 VerificationService: `VerifyErasure_ResumesFromCheckpoint`
    - [x] 8.14 ErasureCertificate: idempotent — two calls for same party return same certificate

- [x] Task 9: Tier 2 integration tests (AC: #1, #2, #3, #4, #6)
    - [x] 9.1 Admin endpoint: `POST erase` → 401 without token, 403 without admin role, 202 with admin token
    - [x] 9.2 Admin endpoint: `GET erasure-status` returns correct state progression
    - [x] 9.3 Admin endpoint: `GET erasure-certificate` returns certificate after erasure completes
    - [x] 9.4 Read sweep: after erasure, `GET /party/{id}` returns erased status
    - [x] 9.5 Read sweep: after erasure, search queries exclude erased party
    - [x] 9.6 Read sweep: after erasure, MCP get-party returns erased status
    - [x] 9.7 Read sweep: after erasure, MCP find-parties excludes erased party
    - [x] 9.8 Concurrent erasure: two simultaneous requests → one succeeds, second returns existing certificate (idempotent)
    - [x] 9.9 Command rejection: send update command for party in `ErasurePending` state → rejected
    - [x] 9.10 Verification report persists to DAPR state store with all per-store results

- [x] Task 10: Tier 3 E2E tests (AC: #1, #2, #3)
    - [x] 10.1 Full Aspire topology: create party → trigger erasure → verify key destroyed in secret store
    - [x] 10.2 Full topology: erasure → detail projection returns erased status
    - [x] 10.3 Full topology: erasure → party excluded from index search results
    - [x] 10.4 Full topology: `PartyErased` event published to pub/sub topic

## Dev Notes

### Architecture Decisions

**ADR: Erasure Flow — Aggregate-Driven Saga with Verification Service**

The erasure flow is a three-phase saga:

1. **Phase 1 (Irreversible):** Aggregate handles `ErasePartyCommand` → emits `ErasePartyRequested` → key deletion via `IPartyKeyManagementService.DeleteKeyAsync()` → emits `PartyEncryptionKeyDeleted`
2. **Phase 2 (Retryable):** `ErasureVerificationService` cleans projections → verifies all stores → emits `ErasureVerified`
3. **Phase 3 (Best-effort):** Publish `PartyErased` to subscribers → track delivery → alert on timeout

The aggregate actor is the state machine owner. The `ErasureVerificationService` follows the `ProjectionRebuildService` pattern (Story 8-3) — background service, checkpoint-based, observable. These phases have different failure semantics: Phase 1 must succeed or the whole operation fails; Phase 2 can be retried from checkpoint; Phase 3 is tracked but not blocking.

**ADR: Projection Cleanup — Actor Self-Cleanup via Pure Handler Methods (D18)**

Both projection handlers get an `ApplyErasure()` method:

- `PartyDetailProjectionHandler.ApplyErasure()` — nullifies personal data fields, sets `IsErased = true`, preserves aggregate ID and tenant
- `PartyIndexProjectionHandler.ApplyErasure()` — returns `null` (removes entry entirely)

Actors wrap the handler call and persist the cleaned state. This is consistent with D18 (pure handler classes extracted from actors) and is Tier 1 testable.

**ADR: Erasure State — Events for Transitions, State Store for Artifacts**

Domain events track the state machine: `ErasePartyRequested` → `PartyEncryptionKeyDeleted` → `ErasureVerified` → `PartyErased`. Large compliance artifacts (verification report, erasure certificate) stored in DAPR state store at `{tenant}:erasure:{partyId}` and `{tenant}:erasure-report:{partyId}`.

### CRITICAL: Existing Story 9-1 Infrastructure to Reuse

**DO NOT re-implement any of this — it already exists and is tested:**

- `IPartyKeyManagementService.DeleteKeyAsync(tenantId, partyId)` → returns `ErasureCertificate` with `VerificationStatus` (Verified/Failed/Pending) and `KeyVersionsDestroyed` list
- `ErasureCertificate` record in `Contracts/Security/` — already defined with all fields needed
- `ErasureVerificationStatus` enum in `Contracts/Security/` — Pending, Verified, Failed
- `PartyEncryptionKeyDeleted` event in `Contracts/Events/` — already defined with PartyId, TenantId, DeletedAt
- `IKeyOperationAuditService.RecordOperationAsync()` — already audits key deletion operations
- `LocalDevKeyStorageBackend` — in-memory dev backend with `DeleteAllVersionsAsync` and read-back verification
- `PartyKeyManagementService.DeleteKeyAsync()` — full implementation: lists versions → deletes all → verifies via read-back → audits → returns certificate

**Key interfaces in `Contracts/Security/`:**

```csharp
// EXISTING — just call it:
Task<ErasureCertificate> DeleteKeyAsync(string tenantId, string partyId, CancellationToken ct);

// EXISTING — already tracks pending crypto:
Task<bool> IsCryptoPendingAsync(string tenantId, string partyId, CancellationToken ct);
```

### Race Condition Prevention

**Problem:** An in-flight command could write new encrypted data AFTER key deletion starts, using a cached key. This event would be encrypted with a destroyed key — permanently unrecoverable but not recognized as "erased."

**Solution:** The aggregate must reject ALL commands once `ErasePartyRequested` is applied. Use ETag-based concurrency on aggregate state — commands that arrive concurrently with erasure will fail their state update (ETag mismatch) and retry, at which point they see `ErasurePending` status and are rejected with a clear error.

```csharp
// In aggregate Handle method:
if (State.ErasureStatus is ErasureStatus.ErasurePending or ErasureStatus.Erased)
    return new CommandRejected("Party erasure in progress or completed. No modifications allowed.");
```

### Erased Party Read Path

**CRITICAL: Check erasure state BEFORE decryption.** If the read path attempts decryption with a destroyed key, the user gets a cryptographic error instead of a clear "erased" message.

```csharp
// In projection actor read method:
if (state?.IsErased == true)
    return new PartyErasedResponse(partyId, state.ErasedAt);
// Only attempt decryption for non-erased parties
```

All read paths must be updated: REST party detail endpoint, REST search endpoint, MCP `get-party` tool, MCP `find-parties` tool.

### Corrupted State During Erasure (D15 Pattern)

If a projection actor's state is corrupted (deserialization fails) AND the party's key has been destroyed, treat it as "already cleaned." Corrupted state with no encryption key = no personal data recoverable. Log the corruption but report as "Cleaned" in the verification report.

### Subscriber Notification Patterns

The `PartyErased` event is published to the standard topic `{tenant}.parties.events` via DAPR pub/sub. The event payload is minimal — only `PartyId` and `TenantId` (no personal data, consistent with existing event patterns). Subscribers follow the handler pattern already documented in `docs/event-handler-patterns.md` and tested in `tests/Hexalith.Parties.Sample.Tests/PartyErasedHandlerPatternTests.cs`:

```csharp
// Sample subscriber handler (already documented and tested):
// 1. Find all references to erased partyId
// 2. Nullify party references
// 3. Replace display names with "[Erased Party]"
// 4. Preserve records with independent legal retention requirements
// 5. Acknowledge delivery
```

### Observability Metrics (OpenTelemetry)

Extend the metrics from Story 9-1:

- `parties.erasure.requested` (counter, tags: tenant)
- `parties.erasure.completed` (counter, tags: tenant)
- `parties.erasure.key_destruction_failed` (counter, tags: tenant, error_type)
- `parties.erasure.verification_duration_ms` (histogram, tags: tenant)
- `parties.erasure.verification_partial` (counter, tags: tenant, failed_store)
- `parties.erasure.subscriber_unacknowledged` (gauge, tags: tenant, subscriber)

### Admin Endpoint Patterns

Follow the existing pattern from Story 9-1's key rotation endpoint:

- `POST .../erase` → validates input, fires background task, returns `202 Accepted` with correlation ID
- `GET .../erasure-status` → reads state from aggregate + verification report from state store
- `GET .../erasure-certificate` → reads compliance artifact from state store
- `POST .../retry-verification` → retriggers verification service for failed stores only

All endpoints: `[Authorize(Policy = "Admin")]`, same auth pattern as existing admin endpoints.

### Failure Mode Mitigations

| Component             | Failure Mode                      | Mitigation                                                                                    |
| --------------------- | --------------------------------- | --------------------------------------------------------------------------------------------- |
| Key deletion          | Backend unreachable               | DAPR reminder retry (5 retries / 15 min); party stays `ErasurePending`; alert on exhaustion   |
| Key deletion          | Partial deletion                  | `ErasureCertificate.Status = Failed`; retry deletes remaining versions                        |
| Projection cleanup    | Actor deactivated mid-cleanup     | `EraseAsync` persists state before returning; actor activation checks `IsErased` flag         |
| Projection cleanup    | Actor state corrupted             | D15 pattern: corrupted + key destroyed = "cleaned"; log and report as clean                   |
| Projection cleanup    | Timeout (>5 min)                  | Checkpoint progress; alert; continue rather than abort                                        |
| Index cleanup         | Entry in multiple partitions      | Verify service queries ALL partitions for partyId, not just primary                           |
| PartyErased publish   | DAPR pub/sub unavailable          | Persist "publish pending"; retry via reminder; internal verification NOT affected             |
| Subscriber            | Handler throws / down             | Dead-letter + retry; alert after configurable timeout (48h default)                           |
| Audit trail           | State store write fails           | Retry with backoff; alert; erasure valid (key destroyed) but certificate persistence critical |
| Concurrent erasure    | Two admins trigger simultaneously | Idempotent: second request returns existing certificate, no new events                        |
| Post-erasure commands | Commands queued for erased party  | Aggregate rejects with "party erased" error; clear message, not cryptic key-not-found         |

### Previous Story Intelligence (Story 9-1)

From Story 9-1 implementation (in review):

- **DAPR Secrets API is READ-ONLY** — key lifecycle uses `IKeyStorageBackend` abstraction. DeleteKeyAsync already handles this.
- **Secure key disposal:** `CryptographicOperations.ZeroMemory()` pattern established — follow for any key material in erasure flow
- **LoggerMessage pattern** for structured logging — use partial methods with `[LoggerMessage]` attribute
- **Admin endpoint pattern:** `[Authorize(Policy = "Admin")]` + `202 Accepted` + correlation ID
- **DAPR reminder-based retry** for CryptoPending — reuse same pattern for erasure retry
- **NSubstitute + DaprClient nullable generics:** may need `#pragma warning disable CS8620` (documented debug fix)
- **ETag concurrency** on metadata updates — apply same pattern for aggregate state guards
- **Circuit breaker** via Polly (`Microsoft.Extensions.Http.Resilience`) — 5-failure threshold, 30s break

### Testing Standards

**Tier 1 (Unit — ~50% effort):** Pure aggregate Handle/Apply logic, pure handler ApplyErasure, verification service logic

- Mock `IPartyKeyManagementService`, projection actors with NSubstitute
- Test state machine transitions: Active → ErasurePending → KeyDestroyed → Verified → Erased
- Test command rejection in each non-Active state
- Test idempotent erasure (second request returns existing certificate)
- Verify personal data nullification in detail handler
- Verify index removal in index handler

**Tier 2 (Integration — ~40% effort):** WebApplicationFactory + mocked DAPR

- Admin endpoint auth tests (401/403/202 pattern)
- **CRITICAL: Read sweep test** — after erasure, hit ALL read endpoints (REST detail, REST search, MCP get-party, MCP find-parties) → all must return erased status or exclude erased party
- Concurrent erasure idempotency
- Command rejection for erased/pending parties
- Verification report persistence and retrieval

**Tier 3 (E2E — ~10% effort):** Full Aspire topology

- Create party → trigger erasure → key destroyed in secret store
- Projection returns erased status after erasure
- Party excluded from search index after erasure

### Project Structure Notes

**New files to create:**

```
src/
  Hexalith.Parties.Contracts/
    Commands/
      ErasePartyCommand.cs                    # New command
    Events/
      ErasePartyRequested.cs                  # New domain event
      ErasureVerified.cs                      # New domain event
      PartyErased.cs                          # New domain event (subscriber-facing)
    Security/
      ErasureStatus.cs                        # Enum: Active → ErasurePending → ... → Erased
      ErasureVerificationReport.cs            # Verification report record
      ErasureVerificationStoreResult.cs       # Per-store result record
      IErasureVerificationService.cs          # Verification service interface
  Hexalith.Parties.Security/
    ErasureVerificationService.cs             # Background verification (follows ProjectionRebuildService)
  Hexalith.Parties.CommandApi/
    Controllers/
      AdminController.cs                      # MODIFY: add erase, erasure-status, erasure-certificate, retry-verification endpoints

tests/
  Hexalith.Parties.Server.Tests/
    ErasePartyCommandHandlerTests.cs          # Tier 1: aggregate Handle/Apply for erasure
  Hexalith.Parties.Projections.Tests/
    ErasureProjectionHandlerTests.cs          # Tier 1: ApplyErasure for detail and index handlers
  Hexalith.Parties.Security.Tests/
    ErasureVerificationServiceTests.cs        # Tier 1: verification service logic
  Hexalith.Parties.CommandApi.Tests/
    Controllers/
      ErasureEndpointTests.cs                 # Tier 2: admin endpoints + read sweep
  Hexalith.Parties.IntegrationTests/
    Security/
      ErasureE2ETests.cs                      # Tier 3: full topology erasure flow
```

**Files to modify:**

```
src/
  Hexalith.Parties.Server/
    Party aggregate Handle/Apply methods       # Add ErasePartyCommand handling + erasure state machine
  Hexalith.Parties.Projections/
    Handlers/
      PartyDetailProjectionHandler.cs          # Add ApplyErasure method
      PartyIndexProjectionHandler.cs           # Add ApplyErasure method
    Abstractions/
      IPartyDetailProjectionActor.cs           # Add EraseAsync method
      IPartyIndexProjectionActor.cs            # Add EraseAsync method
    Actors/
      PartyDetailProjectionActor.cs            # Implement EraseAsync + IsErased check on read
      PartyIndexProjectionActor.cs             # Implement EraseAsync
  Hexalith.Parties.CommandApi/
    Controllers/
      AdminController.cs                       # Add 4 erasure endpoints
    (REST read endpoints)                      # Add erasure state check before response
  Hexalith.Parties.Client/
    (MCP tools)                                # Add erasure state check in get-party, find-parties
```

**Alignment with existing patterns:**

- Contracts: events in `Events/` (flat, IEventPayload), commands in `Commands/`, security types in `Security/`
- Implementation: `ErasureVerificationService` follows `ProjectionRebuildService` pattern (same project, similar structure)
- Admin endpoints: extend existing `AdminController.cs` (same auth pattern, same async 202 pattern)
- Tests: `{Method}_{Scenario}_{ExpectedResult}` naming, Shouldly assertions, NSubstitute mocks
- Projection handlers: pure static methods, Tier 1 testable (D18)

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9, Story 9.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision D6 - Personal Data Scope]
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision D7 - IsNaturalPerson Reclassification]
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision D14 - Projection Rebuild Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision D15 - Projection Health Monitoring]
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision D18 - Pure Handler Classes]
- [Source: _bmad-output/planning-artifacts/prd.md#FR44 - Right to Erasure Trigger]
- [Source: _bmad-output/planning-artifacts/prd.md#FR45 - Erasure Verification]
- [Source: _bmad-output/planning-artifacts/prd.md#FR46 - PartyErased Subscriber Notification]
- [Source: _bmad-output/planning-artifacts/prd.md#FR55 - Erased Party Read Behavior]
- [Source: _bmad-output/planning-artifacts/prd.md#Journey 4 - Laurent GDPR Erasure]
- [Source: _bmad-output/planning-artifacts/prd.md#Journey 5 - Clara PartyErased Handler]
- [Source: _bmad-output/planning-artifacts/prd.md#GDPR Compliance Section - Article 17]
- [Source: _bmad-output/implementation-artifacts/9-1-per-party-encryption-key-management.md#Dev Notes]
- [Source: src/Hexalith.Parties.Contracts/Security/IPartyKeyManagementService.cs - DeleteKeyAsync]
- [Source: src/Hexalith.Parties.Contracts/Security/ErasureCertificate.cs]
- [Source: src/Hexalith.Parties.Contracts/Security/ErasureVerificationStatus.cs]
- [Source: src/Hexalith.Parties.Contracts/Events/PartyEncryptionKeyDeleted.cs]
- [Source: src/Hexalith.Parties.Security/PartyKeyManagementService.cs - DeleteKeyAsync implementation]
- [Source: src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs]
- [Source: src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs]
- [Source: src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs]
- [Source: tests/Hexalith.Parties.Sample.Tests/PartyErasedHandlerPatternTests.cs]
- [Source: docs/event-handler-patterns.md - PartyErased mandatory subscription]

## Dev Agent Record

### Agent Model Used

GPT-5.4

### Debug Log References

- 2026-03-09: Focused validation after review remediation
    - `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --no-restore --filter PartyAggregateErasureTests`
    - `dotnet test tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj --no-restore --filter ErasureVerificationServiceTests`
    - `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --no-restore --filter ErasureEndpointTests`

### Completion Notes List

- BMAD adversarial review findings were remediated by wiring the erasure saga through aggregate terminal transitions instead of stopping after key deletion and verification.
- Added a persistent erasure artifact/status store so admin endpoints now return real erasure status, certificates, and verification reports rather than placeholders.
- Replaced the empty erasure cleanup pipeline with real projection cleanup delegates for detail and index actors plus cache-cleanup reporting.
- Added aggregate coverage for the new internal erasure transition commands and updated Command API endpoint tests to assert real persisted behavior.
- Focused validation passed for aggregate, security, and Command API erasure test suites after fixing a nullable Dapr state-store wrapper and a duplicate using directive.

### File List

- `src/Hexalith.Parties.Contracts/Commands/CompletePartyErasure.cs`
- `src/Hexalith.Parties.Contracts/Commands/MarkErasureVerified.cs`
- `src/Hexalith.Parties.Contracts/Commands/MarkPartyEncryptionKeyDeleted.cs`
- `src/Hexalith.Parties.Contracts/Security/IPartyErasureRecordStore.cs`
- `src/Hexalith.Parties.Contracts/Security/PartyErasureStatusRecord.cs`
- `src/Hexalith.Parties.CommandApi/Controllers/AdminController.cs`
- `src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.Security/PartyErasureRecordStore.cs`
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/Controllers/ErasureEndpointTests.cs`
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateErasureTests.cs`

## Change Log

- 2026-03-09: BMAD review remediation completed
    - Fixed aggregate erasure progression by adding internal commands that emit `PartyEncryptionKeyDeleted`, `ErasureVerified`, and `PartyErased` at the correct saga phases.
    - Implemented `IPartyErasureRecordStore` and `PartyErasureRecordStore` so erasure status, certificates, and verification reports are persisted independently of erased party data.
    - Updated `AdminController` to persist erasure status transitions, return real status/certificate data, and complete the terminal `Erased` state when verification succeeds.
    - Replaced the empty erasure cleanup delegate list with real projection cleanup delegates for detail/index actors and cache cleanup reporting.
    - Added focused tests for new aggregate transitions and updated admin endpoint tests to validate stored status and certificate retrieval.
    - Validation passed: Server erasure tests `18/18`, Security erasure tests `11/11`, Command API erasure tests `10/10`.

## Senior Developer Review (AI)

### Review Follow-up

**Date:** 2026-03-09
**Reviewer:** GPT-5.4
**Outcome:** All previously reported High/Medium issues fixed. Story status moved to `done`.

**Resolved findings:**

- The erasure saga now advances the aggregate to terminal `Erased` instead of stopping after key destruction and verification.
- Verification no longer runs against an empty cleanup pipeline; it now invokes real cleanup delegates for detail and index projection actors and records cleanup results.
- `GET /api/v1/admin/parties/{partyId}/erasure-status` now returns persisted status and per-store verification details.
- `GET /api/v1/admin/parties/{partyId}/erasure-certificate` now returns persisted erasure certificates plus verification reports.
- Tests now validate the implemented behavior instead of placeholder endpoint responses.

**Validation summary:**

- `Hexalith.Parties.Server.Tests` filtered to `PartyAggregateErasureTests`: passed
- `Hexalith.Parties.Security.Tests` filtered to `ErasureVerificationServiceTests`: passed
- `Hexalith.Parties.CommandApi.Tests` filtered to `ErasureEndpointTests`: passed
