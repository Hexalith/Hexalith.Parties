# Story 9.2: Field-Level Encryption & Crypto-Shredding Activation

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want personal data fields encrypted at rest via `[PersonalData]` attributes with zero domain code changes,
so that GDPR encryption is structural and the domain remains encryption-unaware.

## Acceptance Criteria

1. **Field-Level Encryption on Event Persist**
    - Given a party command that writes personal data fields
    - When the event is persisted to the event store
    - Then all `[PersonalData]`-attributed fields are encrypted using the party's per-party key (FR53)
    - And domain code has zero DAPR awareness — encryption is handled by infrastructure (NFR8)

2. **Decryption at Publish Time**
    - Given encrypted events in the event store
    - When events are published to DAPR pub/sub
    - Then events are decrypted at publish time — subscribers receive readable data (FR54)
    - And subscribers never handle decryption

3. **Decryption Failure Circuit Breaker**
    - Given a decryption failure at publish time
    - When the circuit breaker activates
    - Then publication is prevented — unreadable events are never published
    - And the failure is logged and alerted

4. **Snapshot Field-Level Encryption**
    - Given snapshots for a party
    - When crypto-shredding is active
    - Then snapshots participate in field-level encryption
    - And snapshot invalidation is part of the erasure transaction

5. **Type-Dependent Personal Data Classification (D6)**
    - Given type-dependent personal data classification
    - When encryption is applied
    - Then person parties: all PII encrypted (names, DOB, derived fields)
    - And organization parties: entity-level fields NOT encrypted by default
    - And all party types: contact channels and identifiers always encrypted
    - And `IsNaturalPerson = true` organizations: elevated to person-level encryption scope

## Tasks / Subtasks

- [ ] Task 1: Harden existing encryption infrastructure (AC: #1, #2, #4, #5)
    - [ ] 1.1 Harden `ProtectNode` — add logging when `PropertyInfo.GetValue` throws (currently silently skips); wrap in try/catch with `LoggerMessage` warning including property name and party ID
    - [ ] 1.2 Harden `UnprotectNodeAsync` — add specific `FormatException` catch for corrupted base64 in encrypted fields; log with "corrupted encrypted field" message including field path and party ID
    - [ ] 1.3 Harden `IsEncryptedMarker` — add warning log when JSON object has `alg`/`kv` fields but missing `$enc` marker (possible field corruption)
    - [ ] 1.4 Harden snapshot type resolution — use `Type.FullName` instead of `AssemblyQualifiedName` in `ProtectedSnapshotState.TypeName` to survive NuGet version changes; add version-tolerant `Type.GetType` fallback that strips assembly version info on resolution failure
    - [ ] 1.5 Add projection rebuild erased-party handling — `ProjectionRebuildService` must catch decryption failures for erased parties and apply `ErasureStatus.Erased` tombstone state instead of crashing the entire rebuild. Add try/catch around event replay that checks `ErasureStatus` and gracefully degrades

- [ ] Task 2: Implement decryption failure circuit breaker (AC: #3)
    - [ ] 2.1 Add `DecryptionCircuitBreaker` class in `Hexalith.Parties.Security/` — lightweight custom implementation using `ConcurrentDictionary<string, CircuitState>` where `CircuitState` is a small struct (failure count, opened timestamp, state enum). Only track non-closed parties (absence = closed). Do NOT use Polly per-party — Polly circuits are designed for shared resources, not per-entity isolation. Memory proportional to active failures, not total party count
    - [ ] 2.2 Wrap `UnprotectEventPayloadAsync` decryption path with circuit breaker — on open circuit, throw `DecryptionCircuitOpenException` with party ID and break duration
    - [ ] 2.3 Add `DecryptionCircuitOpenException` sealed class in Security project — carries party ID, tenant ID, break expiry
    - [ ] 2.4 Ensure `EventPublisher` in EventStore catches `DecryptionCircuitOpenException` and prevents publication (event stays in unpublished drain queue for retry)
    - [ ] 2.5 Add `LoggerMessage` entries for circuit breaker state transitions: half-open, open, closed
    - [ ] 2.6 Add OpenTelemetry counter `parties.encryption.circuit_breaker_trips` (tags: tenant, party_id, reason: transient|key_destroyed|unknown) — reason tag enables ops to distinguish infrastructure issues from expected erasure behavior
    - [ ] 2.7 CRITICAL: Detect erased party on decryption failure — query `PartyState.ErasureStatus` via actor state; if `ErasureStatus >= KeyDestroyed`, skip retry and dead-letter the event instead of infinitely retrying decryption of permanently-destroyed keys
    - [ ] 2.8 Add drain recovery rate limiting — after circuit breaker transitions to half-open, process max 5 events per recovery cycle with 2-second delay between events to prevent thundering herd on key management service
    - [ ] 2.9 Add max open duration safeguard — if circuit stays open > 5 minutes continuously, force transition to half-open regardless of timer (prevents stuck-open circuits from configuration edge cases)

- [ ] Task 3: Add crypto-shredding activation configuration (AC: #1)
    - [ ] 3.1 Create `CryptoShreddingOptions` record in `Contracts/Security/` with `IsEnabled` (default: true for v1.1), `CircuitBreakerFailureThreshold` (default: 3), `CircuitBreakerBreakDuration` (default: 60s)
    - [ ] 3.2 Bind to `Parties:CryptoShredding` configuration section in `PartiesServiceCollectionExtensions`
    - [ ] 3.3 Inject `IOptions<CryptoShreddingOptions>` into `PartyPayloadProtectionService` — when `IsEnabled=false`, skip encryption on persist but STILL decrypt existing encrypted events on publish (mixed-format store support)
    - [ ] 3.4 Log startup configuration state: enabled/disabled + circuit breaker thresholds
    - [ ] 3.5 Read options snapshot once per `ProtectEventPayloadAsync`/`UnprotectEventPayloadAsync` call (not per field) to ensure consistent behavior within a single event processing

- [ ] Task 4: Tier 1 unit tests — encryption roundtrip (AC: #1, #2, #4, #5)
    - [ ] 4.1 `ProtectEventPayload_PersonCreated_EncryptsPersonalDataFields` — verify `PersonDetails` fields + derived `DisplayName`/`SortName` encrypted, non-PII fields (Type, PartyId) remain plaintext
    - [ ] 4.2 `ProtectEventPayload_ContactChannelAdded_EncryptsValue` — verify `Value` encrypted, `ContactChannelId` and `Type` remain plaintext
    - [ ] 4.3 `ProtectEventPayload_IdentifierAdded_EncryptsValue` — verify `Value` encrypted, `IdentifierId` and `Type` remain plaintext
    - [ ] 4.4 `ProtectEventPayload_OrganizationCreated_DoesNotEncryptEntityFields` — verify legal name, trading name NOT encrypted for standard organization
    - [ ] 4.5 `ProtectEventPayload_OrganizationIsNaturalPerson_EncryptsAllStringFields` — verify `IsNaturalPerson=true` elevates all string fields to encrypted
    - [ ] 4.6 `UnprotectEventPayload_EncryptedPayload_DecryptsCorrectly` — full roundtrip: protect then unprotect, verify byte-level equivalence
    - [ ] 4.7 `UnprotectEventPayload_DifferentKeyVersion_UsesCorrectVersion` — encrypt with v1, rotate key, verify decryption retrieves v1 key for old events
    - [ ] 4.8 `ProtectEventPayload_NonPartyDomain_Passthrough` — verify non-party domain events pass through unchanged
    - [ ] 4.9 `ProtectEventPayload_NoPersonalData_Passthrough` — verify events without `[PersonalData]` fields (e.g., `PartyDeactivated`) pass through unchanged
    - [ ] 4.10 `ProtectSnapshotState_PartyState_EncryptsPersonalDataFields` — verify snapshot protection encrypts `DisplayName`, `SortName`, nested `PersonDetails`, `ContactChannel.Value`, `PartyIdentifier.Value`
    - [ ] 4.11 `UnprotectSnapshotState_ProtectedSnapshot_RestoresOriginalState` — roundtrip snapshot protect/unprotect
    - [ ] 4.12 `ProtectEventPayload_1000EncryptionsOfSameData_AllProduceDifferentCiphertext` — nonce uniqueness verification at scale (AES-GCM safety); encrypt same payload 1000 times under same key, verify all nonces are unique
    - [ ] 4.13 `ProtectEventPayload_CryptoShreddingDisabled_Passthrough` — verify `IsEnabled=false` skips encryption on persist
    - [ ] 4.14 `UnprotectEventPayload_CryptoShreddingDisabled_StillDecryptsExistingEncrypted` — verify `IsEnabled=false` still decrypts `json+pdenc-v1` format events (mixed-format store support)
    - [ ] 4.15 `UnprotectEventPayload_TamperedCiphertext_ThrowsCryptographicException` — verify AES-GCM authentication tag rejects modified ciphertext (tamper detection)
    - [ ] 4.16 `UnprotectEventPayload_CorruptedBase64_ThrowsFormatExceptionWithContext` — verify corrupted base64 in `$enc` field produces descriptive error with party ID
    - [ ] 4.17 `ProtectSnapshotState_TypeNameSurvivesVersionChange` — verify snapshot roundtrip using `FullName` type resolution (not assembly-qualified)
    - [ ] 4.18 **COMPLIANCE: Personal Data Registry Test** — `AllEventPayloadTypes_PersonalDataClassificationVerified` — enumerate all `IEventPayload` implementations via reflection, create sample instances, assert `ContainsProtectedData()` matches expected value per type. Prevents future event types from silently storing plaintext PII
    - [ ] 4.19 `UnprotectEventPayload_KeyDeleted_ThrowsAndPlaintextUnrecoverable` — crypto-shredding proof: encrypt event, delete key via `DeleteKeyAsync`, attempt `UnprotectEventPayloadAsync`, verify exception and confirm original plaintext cannot be recovered
    - [ ] 4.20 `UnprotectSnapshotState_EncryptedWithOldKeyVersion_DecryptsCorrectly` — snapshot encrypted with key v1, key rotated to v2, verify `UnprotectSnapshotStateAsync` retrieves v1 key and decrypts correctly
    - [ ] 4.21 `ProtectEventPayload_RealisticComposite_CompletesUnder50ms` — performance benchmark: encrypt `CreatePartyComposite` with 5 PersonDetails fields + 3 channels + 2 identifiers (10+ encrypted fields), verify total protect time < 50ms (NFR1 budget: 50ms encryption + 950ms remaining)
    - [ ] 4.22 `ProtectEventPayload_IsNaturalPersonReclassification_EncryptionScopeExpands` — create org with `IsNaturalPerson=false` (plaintext entity fields), change to `IsNaturalPerson=true`, verify subsequent events encrypt org string fields while historical events remain plaintext

- [ ] Task 5: Tier 1 unit tests — circuit breaker (AC: #3)
    - [ ] 5.1 `CircuitBreaker_ThreeConsecutiveFailures_OpensCircuit` — verify circuit opens after threshold
    - [ ] 5.2 `CircuitBreaker_OpenCircuit_ThrowsDecryptionCircuitOpenException` — verify subsequent calls fail fast
    - [ ] 5.3 `CircuitBreaker_BreakDurationExpires_TransitionsToHalfOpen` — verify recovery attempt
    - [ ] 5.4 `CircuitBreaker_HalfOpenSuccess_ClosesCircuit` — verify successful decryption resets circuit
    - [ ] 5.5 `CircuitBreaker_PerPartyIsolation_IndependentCircuits` — verify party A failure doesn't affect party B
    - [ ] 5.6 `CircuitBreaker_ErasedParty_SkipsRetryAndDeadLetters` — verify decryption failure for erased party (ErasureStatus >= KeyDestroyed) does NOT trigger retry loop; event is dead-lettered
    - [ ] 5.7 `CircuitBreaker_MaxOpenDuration_ForcesHalfOpen` — verify stuck-open circuit transitions to half-open after max duration (5 min)
    - [ ] 5.8 `CircuitBreaker_DrainRecovery_RateLimited` — verify recovery processes max 5 events per cycle with delays

- [ ] Task 6: Tier 1 unit tests — command guard integration (AC: #1)
    - [ ] 6.1 `CommandGuard_CryptoPending_BlocksPersonalDataWrite` — verify `PartyPersonalDataCommandGuard` returns blocking reason when crypto is pending
    - [ ] 6.2 `CommandGuard_NoCryptoKey_BlocksPersonalDataWrite` — verify blocking when no key exists
    - [ ] 6.3 `CommandGuard_KeyAvailable_AllowsPersonalDataWrite` — verify null return (allowed) when key exists
    - [ ] 6.4 `CommandGuard_NonPersonalDataCommand_AlwaysAllows` — verify commands without `[PersonalData]` fields (e.g., `DeactivateParty`) are never blocked

- [ ] Task 7: Tier 2 integration tests (AC: #1, #2, #3)
    - [ ] 7.1 `CreateParty_PersonWithChannels_EventsEncryptedInStore` — WebApplicationFactory: POST create party composite, read actor state via DAPR HTTP API, verify encrypted JSON markers (`$enc`, `alg`, `kv`, `n`, `t`, `c`)
    - [ ] 7.2 `CreateParty_PersonWithChannels_PublishedEventsDecrypted` — capture pub/sub output, verify subscriber receives plaintext personal data
    - [ ] 7.3 `UpdateParty_AddContactChannel_EncryptsChannelValue` — add channel to existing party, verify encrypted in store
    - [ ] 7.4 `DecryptionFailure_CircuitBreakerActivates_PublicationBlocked` — simulate key deletion mid-flow, verify circuit breaker prevents publication of unreadable events
    - [ ] 7.5 `Configuration_CryptoShreddingDisabled_NoEncryption` — set `Parties:CryptoShredding:IsEnabled=false`, verify events stored in plaintext
    - [ ] 7.6 `MixedFormatStore_PlaintextThenEncrypted_BothReadable` — store events with `IsEnabled=false`, enable crypto, store more events, verify both plaintext and encrypted events readable via projection query

- [ ] Task 8: Tier 3 E2E tests (AC: #1, #2, #4)
    - [ ] 8.1 `FullTopology_CreateParty_EncryptionRoundtrip` — Aspire topology: create party, verify stored encrypted, verify query returns decrypted detail via projection
    - [ ] 8.2 `FullTopology_KeyRotation_OldEventsStillDecryptable` — rotate key, create new events, verify old events still decrypt with v1 key and new events use v2 key

## Dev Notes

### CRITICAL: Most Infrastructure Already Exists

**Story 9-1's review remediation implemented the bulk of Story 9-2's infrastructure.** The dev agent MUST NOT reinvent any of these:

| Component | File | Status |
|-----------|------|--------|
| `PartyPayloadProtectionService` | `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs` | ✅ Implemented — protect/unprotect for events + snapshots |
| `PersonalDataGraphInspector` | `src/Hexalith.Parties.Security/PersonalDataGraphInspector.cs` | ✅ Implemented — deep graph scan with `[PersonalData]` + `IsNaturalPerson` logic |
| `IPersonalDataCommandGuard` | `src/Hexalith.Parties.Security/IPersonalDataCommandGuard.cs` | ✅ Implemented — blocks writes when crypto pending |
| `PartyPersonalDataCommandGuard` | `src/Hexalith.Parties.Security/PartyPersonalDataCommandGuard.cs` | ✅ Implemented |
| `[PersonalData]` attributes | `src/Hexalith.Parties.Contracts/` | ✅ Applied to all PII fields |
| DI registration | `PartiesServiceCollectionExtensions.cs` | ✅ Wired up |
| EventStore pipeline hooks | `Hexalith.EventStore/.../EventPersister.cs`, `EventPublisher.cs` | ✅ Calls `IEventPayloadProtectionService` |
| Existing tests | `tests/Hexalith.Parties.Security.Tests/PartyPayloadProtectionServiceTests.cs` | ✅ Baseline exists |

**What remains for Story 9-2:**
1. **Hardening** — reflection safety, corrupted field detection, snapshot type resolution, projection rebuild for erased parties (Task 1)
2. **Circuit breaker** for decryption failures at publish time with erasure detection (Task 2) — NEW
3. **Configuration toggle** (`CryptoShreddingOptions`) with mixed-format semantics (Task 3) — NEW
4. **Comprehensive test coverage** — roundtrip, circuit breaker, compliance registry, performance benchmark, crypto-shredding proof (Tasks 4-8) — EXPAND existing
5. **Admin encryption status** (optional) — health check reporting encrypted party count, CryptoPending count, config state. Implement if time permits; otherwise defer to Story 9.4

### Encryption Architecture (Already Implemented)

**Persist path:** `EventPersister.PersistEventsAsync()` → `IEventPayloadProtectionService.ProtectEventPayloadAsync()` → field-level AES-256-GCM encryption of `[PersonalData]` fields → encrypted JSON stored in DAPR actor state

**Publish path:** `EventPublisher.PublishEventsAsync()` → `IEventPayloadProtectionService.UnprotectEventPayloadAsync()` → decrypted JSON published to DAPR pub/sub → subscribers receive plaintext

**Snapshot path:** `SnapshotManager` → `ProtectSnapshotStateAsync()` / `UnprotectSnapshotStateAsync()` → `ProtectedSnapshotState` wrapper with base64-encoded encrypted payload. **HARDENING NOTE:** Current implementation stores `AssemblyQualifiedName` in `ProtectedSnapshotState.TypeName` — this breaks on NuGet version changes. Change to `Type.FullName` with a version-tolerant `Type.GetType` fallback that strips assembly version info on resolution failure.

**Encrypted field JSON format:**
```json
{
  "$enc": true,
  "alg": "AES256GCM",
  "kv": 1,
  "n": "<base64-nonce-12-bytes>",
  "t": "<base64-tag-16-bytes>",
  "c": "<base64-ciphertext>"
}
```

### Encrypted vs. Plaintext Field Examples

**PartyCreated event (person type) — stored in actor state:**
```json
{
  "partyId": "550e8400-...",            // plaintext (not personal data)
  "type": "Person",                     // plaintext
  "personDetails": {
    "firstName": { "$enc": true, ... }, // ENCRYPTED
    "lastName": { "$enc": true, ... },  // ENCRYPTED
    "dateOfBirth": { "$enc": true, ... }, // ENCRYPTED
    "prefix": { "$enc": true, ... },    // ENCRYPTED
    "suffix": { "$enc": true, ... }     // ENCRYPTED
  },
  "organizationDetails": null           // not present
}
```

**ContactChannelAdded event — stored in actor state:**
```json
{
  "contactChannelId": "7f3d2a...",     // plaintext (ID, not personal)
  "type": "Email",                      // plaintext (enum)
  "value": { "$enc": true, ... }       // ENCRYPTED (personal data)
}
```

**OrganizationCreated (standard org) — stored in actor state:**
```json
{
  "partyId": "...",
  "type": "Organization",
  "organizationDetails": {
    "legalName": "Acme Corp",          // PLAINTEXT (org entity data, NOT personal)
    "tradingName": "Acme",             // PLAINTEXT
    "legalForm": "SAS",                // PLAINTEXT
    "isNaturalPerson": false            // PLAINTEXT
  }
}
```

**OrganizationCreated (IsNaturalPerson=true) — stored in actor state:**
```json
{
  "partyId": "...",
  "type": "Organization",
  "organizationDetails": {
    "legalName": { "$enc": true, ... },    // ENCRYPTED (elevated)
    "tradingName": { "$enc": true, ... },  // ENCRYPTED (elevated)
    "legalForm": { "$enc": true, ... },    // ENCRYPTED (elevated)
    "isNaturalPerson": true                 // PLAINTEXT (not string)
  }
}
```

### Circuit Breaker Design

The circuit breaker prevents publishing unreadable events when decryption fails (e.g., key infrastructure unreachable, key corrupted).

**Per-party isolation:** Each party gets an independent circuit state. Party A's decryption failure does not affect party B. Implementation uses `ConcurrentDictionary<string, CircuitState>` tracking only non-closed parties — memory is proportional to active failures, not total party count. At 100K parties with 10 simultaneous failures, only 10 entries exist (~1KB). Do NOT use Polly per-party — Polly's circuit breaker is designed for shared resources (HTTP clients, databases), not per-entity state machines.

**State transitions:**
```
Closed → (3 consecutive failures) → Open → (60s timeout) → Half-Open → (1 success) → Closed
                                                             ↘ (1 failure) → Open
```

**Integration point:** Wrap the `DecryptNodeAsync` call in `UnprotectEventPayloadAsync`. When circuit is open, throw `DecryptionCircuitOpenException`. The EventStore `EventPublisher` already retries failed publications via the drain recovery mechanism — the event stays in the unpublished queue.

**CRITICAL: Do NOT add circuit breaker to EventStore code.** The circuit breaker lives in `PartyPayloadProtectionService.UnprotectEventPayloadAsync`. EventPublisher treats any exception from unprotect as a publication failure and retries.

**CRITICAL: Erased party drain recovery livelock prevention.** After crypto-shredding (key destruction), the EventPublisher drain recovery will infinitely retry publishing events whose keys are permanently destroyed. The circuit breaker MUST detect erasure state: query `PartyState.ErasureStatus` via DAPR actor state API (`/v1.0/actors/PartyAggregate/{id}/state/erasureStatus`). If `ErasureStatus >= KeyDestroyed`, dead-letter the event instead of retrying. This distinguishes "key temporarily unavailable" (retry) from "key permanently destroyed" (dead-letter).

**Drain recovery rate limiting:** After circuit breaker transitions from open to half-open, limit event processing to max 5 events per recovery cycle with 2-second delay between events. Prevents thundering herd on key management service after an outage recovery. Configurable via `CryptoShreddingOptions.DrainRecoveryBatchSize` (default: 5) and `DrainRecoveryDelayBetweenEvents` (default: 2s).

**Max open duration safeguard:** Circuit breaker includes a maximum open duration (5 minutes). If the circuit stays open for longer than this, force transition to half-open regardless of the standard break timer. Prevents stuck-open circuits from edge cases like system clock drift or configuration timing issues.

### Configuration Design

```csharp
// In Contracts/Security/
public sealed record CryptoShreddingOptions
{
    public bool IsEnabled { get; init; } = true;
    public int CircuitBreakerFailureThreshold { get; init; } = 3;
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan CircuitBreakerMaxOpenDuration { get; init; } = TimeSpan.FromMinutes(5);
    public int DrainRecoveryBatchSize { get; init; } = 5;
    public TimeSpan DrainRecoveryDelayBetweenEvents { get; init; } = TimeSpan.FromSeconds(2);
}
```

**appsettings.json:**
```json
{
  "Parties": {
    "CryptoShredding": {
      "IsEnabled": true,
      "CircuitBreakerFailureThreshold": 3,
      "CircuitBreakerBreakDuration": "00:01:00"
    }
  }
}
```

**CRITICAL: `IsEnabled` semantics for mixed-format stores:**
- `IsEnabled=false` on **persist**: skip encryption — events stored as plaintext `json` format
- `IsEnabled=false` on **publish**: still decrypt existing `json+pdenc-v1` events — subscribers always receive plaintext
- This enables gradual rollout: deploy with `IsEnabled=false`, verify pipeline health, then enable. Existing plaintext events remain readable. New encrypted events coexist alongside old plaintext events.
- Key management services still operate (keys created, rotated, etc.) regardless of `IsEnabled` state.
- Read options snapshot once per protect/unprotect call to ensure consistent behavior within a single event.
- **Runtime config change safety:** If `IsEnabled` toggles mid-batch (e.g., 10 events being published), the format string (`json` vs `json+pdenc-v1`) is authoritative — `UnprotectEventPayloadAsync` checks the format, not the config flag. Already-encrypted events are always decrypted regardless of config state. This is safe by design.

### `[PersonalData]` Attribute Coverage (Already Complete)

| Location | Fields | Type |
|----------|--------|------|
| `PersonDetails` | `FirstName`, `LastName`, `DateOfBirth`, `Prefix`, `Suffix` | Value object |
| `ContactChannel` | `Value` | Value object |
| `PartyIdentifier` | `Value` | Value object |
| `PostalAddress` | `Street`, `City`, `State`, `PostalCode`, `Country` | Value object |
| `EmailAddress` | `Address` | Value object |
| `PhoneNumber` | `Number` | Value object |
| `SocialMediaHandle` | `Handle` | Value object |
| `PartyState` | `DisplayName`, `SortName` | Aggregate state |
| `PartyDetail` | `DisplayName`, `SortName` | Projection model |
| `PartyIndexEntry` | `DisplayName` | Index model |
| `AddContactChannel` | `Value` | Command |
| `UpdateContactChannel` | `Value` | Command |
| `AddIdentifier` | `Value` | Command |
| `ContactChannelAdded` | `Value` | Event |
| `ContactChannelUpdated` | `Value` | Event |
| `IdentifierAdded` | `Value` | Event |

**Developer MUST NOT add `[PersonalData]` to any new fields** — the attribute coverage is already complete. If the dev adds new PII fields, those need the attribute, but existing fields are covered.

**COMPLIANCE: Personal Data Registry Test (Task 4.18).** To prevent future event types from silently storing plaintext PII, add a Tier 1 test that:
1. Enumerates ALL types implementing `IEventPayload` via reflection across the Contracts assembly
2. Creates sample instances of each (using `PartyTestData` builders where available, or default construction)
3. Asserts `PersonalDataGraphInspector.ContainsProtectedData()` returns the expected value for each type
4. Maintains an explicit dictionary mapping event type to expected classification (true/false)
5. Fails loudly when a new `IEventPayload` type is added without being classified — forces developers to consciously decide whether each new event carries personal data

This is the single most important test in this story for long-term GDPR compliance. Without it, a future story adding `PartyMerged` with personal data fields would silently store plaintext.

### Architecture Decisions to Follow

- **D6 (Type-Dependent Encryption Scope):** Already implemented in `PersonalDataGraphInspector.ShouldProtectProperty()` — `IsNaturalPerson=true` elevates encryption scope
- **D7 (IsNaturalPerson Reclassification):** No new key on reclassification — existing key's encryption scope expands. Handled by inspector logic. **Accepted trade-off:** Historical events for organizations reclassified as natural persons remain unencrypted in the immutable event stream (event immutability constraint). Snapshots will be re-encrypted on the next snapshot cycle. New events use expanded scope. Document this in deployment guide as a known GDPR limitation for reclassified organizations.
- **NFR8 (Zero DAPR Awareness in Domain):** Domain code (`PartyAggregate`, `PartyState`) has NO encryption imports — infrastructure handles everything
- **NFR1 (< 1 second command processing):** Key caching via `CachedPartyKeyManagementService` ensures key lookups < 50ms. Field-level encryption overhead must stay under 50ms for a realistic composite event (10+ encrypted fields) — verified by performance benchmark test (4.21)

### Accepted Trade-offs (Document in Deployment Guide)

1. **IsNaturalPerson reclassification:** Historical events for reclassified organizations remain unencrypted in the immutable event stream. Snapshots re-encrypted on next cycle. New events use expanded scope. This is an inherent limitation of append-only event stores.
2. **Encryption operation audit:** Per-field encryption/decryption operations are NOT individually audited (only key lifecycle operations are). At current volume, per-field audit would generate excessive entries. Key operation audit provides sufficient compliance evidence.
3. **Key material in memory cache:** `CachedPartyKeyManagementService` holds keys in memory for 4-6 minutes. During that window, keys are extractable from same-process memory. Mitigated by in-memory-only cache (no distributed cache) and jittered TTL.
4. **Encrypted field metadata visibility:** The `$enc` JSON marker reveals WHICH fields are encrypted, mapping personal data field positions. Mitigated: field names are public API contract (not secret); encryption protects VALUES, not schema.

### Security Hardening Requirements

- **Secure key disposal:** `CryptographicOperations.ZeroMemory()` on ALL `byte[]` key material after use (already in `PartyPayloadProtectionService`)
- **Nonce uniqueness:** 12-byte random nonce per AES-GCM operation via `RandomNumberGenerator.GetBytes(12)` — NEVER deterministic
- **Plaintext zeroing:** `CryptographicOperations.ZeroMemory(plaintext)` after encryption (already in `EncryptNode`)
- **No PII in logs:** `LoggerMessage` patterns use party ID and tenant ID, never field values
- **Tamper detection:** AES-GCM authenticated encryption rejects modified ciphertext with `CryptographicException` — add explicit test verifying this behavior
- **Corrupted field detection:** Log warning when JSON object contains `alg`/`kv` fields but missing `$enc` marker — possible corruption or partial encryption failure
- **Reflection safety in ProtectNode:** Wrap `PropertyInfo.GetValue()` calls in try/catch — log warning on failure instead of silently skipping (could mask encryption gaps)

### Subscriber Event Contract

**Published event guarantee:** All events published to DAPR pub/sub contain plaintext personal data. Subscribers NEVER receive encrypted fields. This is the invariant that the circuit breaker protects.

**Erasure event contract:** All events published before `PartyErased` contain plaintext. `PartyErased` is the terminal event for a party's lifecycle. After `PartyErased`, no further events are published for that party. Events in the store after key destruction become permanently unreadable and are dead-lettered by the circuit breaker.

**API error responses for encrypted parties:**
- **Circuit breaker open (transient):** Query for a party with an open circuit returns `503 Service Unavailable` with ProblemDetails extension `"retryAfter": "<circuit break expiry ISO 8601>"`. Indicates temporary key infrastructure issue.
- **Erased party:** Query for an erased party returns `410 Gone` with ProblemDetails extension `"erasureStatus": "erased"` and `"erasedAt": "<timestamp>"`. This is the Story 9.3 contract but document here for completeness.

### Projection Rebuild and Erased Parties

**Critical integration:** `ProjectionRebuildService` (Story 8-3) replays ALL events from the event stream when rebuilding projections. For erased parties, events after key destruction cannot be decrypted. The rebuild service MUST:
1. Catch decryption failures (`InvalidOperationException` from `UnprotectEventPayloadAsync`)
2. Check party's `ErasureStatus` — if `>= KeyDestroyed`, apply tombstone state (`IsErased=true`) instead of crashing
3. Skip remaining events for that party (all subsequent events are also undecryptable)
4. Log the skip at `Information` level (expected behavior, not an error)
5. Continue rebuilding other parties normally

Without this handling, a single erased party breaks the entire projection rebuild for its tenant.

### EventStore Pipeline Integration Points

**DO NOT MODIFY EventStore code.** The pipeline already calls `IEventPayloadProtectionService`:

1. `EventPersister.PersistEventsAsync()` → calls `ProtectEventPayloadAsync()` — line-level integration
2. `EventPublisher.PublishEventsAsync()` → calls `UnprotectEventPayloadAsync()` — line-level integration
3. `SnapshotManager` → calls `ProtectSnapshotStateAsync()` / `UnprotectSnapshotStateAsync()`

The `IEventPayloadProtectionService` is registered in DI as `PartyPayloadProtectionService`. Story 9-2 works entirely within this existing contract.

### Previous Story Intelligence (9-1)

**Key learnings from Story 9-1 implementation:**

- DAPR Secrets API is READ-ONLY — key lifecycle uses `IKeyStorageBackend` abstraction
- `LocalDevKeyStorageBackend` enforces realistic constraints (8192 bytes/secret, 100 secrets/party)
- Ambient correlation propagation via `ICorrelationContextAccessor` for audit trail
- NSubstitute + DaprClient nullable generics requires `#pragma warning disable CS8620` workaround
- `CachedPartyKeyManagementService` uses jittered TTL (4-6 min) to prevent thundering herd
- Key metadata stored in DAPR state store, key material in secrets store (separated)
- `PartyKeyLifecycleService` marks `CryptoPending` on failure, retries via in-memory flag
- Admin endpoints use `[Authorize(Policy = "Admin")]` + `202 Accepted` pattern

**From code review:**
- Audit entries now use ambient correlation ID (not random GUIDs)
- `PartyKeyLifecycleService` is exercised by `PartyPayloadProtectionService` on `PartyCreated`
- Local dev backend enforces per-party version limit

### Git Intelligence

Recent commits show Stories 8-2, 8-3, and 9-1 were implemented together. The codebase has:
- `Hexalith.Parties.Security` project with full key management infrastructure
- `Hexalith.Parties.Security.Tests` with comprehensive Tier 1 coverage
- Admin controller with key rotation endpoint
- Correlation middleware propagation

### Project Structure Notes

**New files to create:**
```
src/
  Hexalith.Parties.Contracts/
    Security/
      CryptoShreddingOptions.cs              # Configuration record
  Hexalith.Parties.Security/
    DecryptionCircuitBreaker.cs              # Per-party circuit breaker
    DecryptionCircuitOpenException.cs        # Exception for open circuit

tests/
  Hexalith.Parties.Security.Tests/
    DecryptionCircuitBreakerTests.cs         # Circuit breaker Tier 1 tests
  (existing tests to EXPAND, not replace:)
  Hexalith.Parties.Security.Tests/
    PartyPayloadProtectionServiceTests.cs    # Add roundtrip tests (4.1-4.13)
    PartyPersonalDataCommandGuardTests.cs    # Add guard tests (6.1-6.4)
  Hexalith.Parties.CommandApi.Tests/
    Security/
      EncryptionIntegrationTests.cs          # Tier 2 tests (7.1-7.5)
  Hexalith.Parties.IntegrationTests/
    Security/
      EncryptionE2ETests.cs                  # Tier 3 tests (8.1-8.2)
```

**Files to modify:**
```
src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs
  — Inject IOptions<CryptoShreddingOptions>, add IsEnabled check, integrate circuit breaker

src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs
  — Bind CryptoShreddingOptions, register DecryptionCircuitBreaker
```

**Alignment with unified project structure:**
- Configuration options as `sealed record` with `{ get; init; }` in `Contracts/Security/`
- Exception classes in `Security/` project (near usage)
- Tier 2 tests in `CommandApi.Tests/Security/` for API-level integration
- Tier 3 tests in `IntegrationTests/Security/` for full Aspire topology

### Testing Standards

**Tier 1 (Unit - ~50% effort):** Pure encryption roundtrip, circuit breaker state machine, command guard logic
- Mock `IKeyStorageBackend` and `IPartyKeyManagementService` with NSubstitute
- Use real `PersonalDataGraphInspector` (static utility, no mocks)
- Verify encrypted JSON structure (`$enc`, `alg`, `kv`, `n`, `t`, `c`)
- Verify nonce uniqueness across multiple encryptions
- Verify passthrough for non-personal-data events
- Verify tamper detection (modified ciphertext → `CryptographicException`)
- **Personal data registry test** — enumerate all `IEventPayload` types, verify classification. This is the compliance safety net.

**Tier 2 (Integration - ~35% effort):** WebApplicationFactory + mocked DaprClient
- POST create party composite → verify encrypted actor state via DAPR HTTP API
- Capture pub/sub output → verify plaintext personal data
- Simulate key infrastructure failure → verify circuit breaker behavior
- Test configuration toggle (enabled/disabled) and mixed-format store (plaintext + encrypted coexistence)

**Tier 3 (E2E - ~15% effort):** Full Aspire topology
- Create party → query via projection → verify decrypted detail
- Key rotation → old events still decryptable → new events use new key version

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9, Story 9.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision D6 - Personal Data Scope]
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision D7 - IsNaturalPerson Reclassification]
- [Source: _bmad-output/planning-artifacts/prd.md#FR53 - Field-level encryption per-party keys]
- [Source: _bmad-output/planning-artifacts/prd.md#FR54 - Decrypted events at publish time]
- [Source: _bmad-output/planning-artifacts/prd.md#FR55 - Erased party returns erased status]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR8 - Personal data encrypted at rest]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR11 - Key rotation without downtime]
- [Source: _bmad-output/implementation-artifacts/9-1-per-party-encryption-key-management.md]
- [Source: src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs]
- [Source: src/Hexalith.Parties.Security/PersonalDataGraphInspector.cs]
- [Source: src/Hexalith.Parties.Security/PartyPersonalDataCommandGuard.cs]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/EventPersister.cs]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/EventPublisher.cs]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
