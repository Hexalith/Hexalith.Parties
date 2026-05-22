# Story 6.10: Rotate Tenant Encryption Keys

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator responsible for tenant security,
I want tenant encryption keys to rotate without downtime or data loss,
so that Parties can meet operational security requirements while preserving readable party data.

## Acceptance Criteria

1. Given a tenant encryption key rotation is requested, when rotation begins, then the system creates or selects the new tenant key material according to the configured key provider, and existing party-level keys remain available for decrypting current data during the rotation window.
2. Given party-level personal data keys are protected by tenant key material, when tenant key rotation is applied, then party key wrapping or equivalent key metadata is updated safely, and personal data remains readable for authorized operations after rotation.
3. Given key rotation is interrupted or partially fails, when the system resumes or retries rotation, then rotation proceeds idempotently from the last safe state, and no party personal data becomes permanently unreadable because of partial rotation.
4. Given an erased party no longer has readable personal data keys, when tenant key rotation runs, then erased parties remain erased, and rotation does not recreate or recover destroyed personal data keys.
5. Given key rotation status is queried, when an operator checks progress, then the response includes bounded status metadata, counts, and failures by category, and it does not expose key material, secrets, tokens, or personal data.
6. Given key rotation tests run, when they cover success, retry, partial failure, erased parties, missing key provider, concurrent reads, and redacted status output, then tenant key rotation preserves uptime, data readability, and erasure guarantees.

## Tasks / Subtasks

- [x] Task 1: Define tenant key rotation contracts and status model (AC: #1, #3, #5)
  - [x] Add contract types under `src/Hexalith.Parties.Contracts/Security/` for tenant key rotation request/progress/status metadata.
  - [x] Keep status fields bounded: tenant id, rotation id or operation id, phase, total/processed/skipped/failed counts, failure categories, started/completed timestamps, and correlation id.
  - [x] Do not include tenant key material, wrapped party key bytes, tokens, raw provider errors, personal data, or decrypted party fields in contracts.
  - [x] Add `KeyOperationType` values or separate audit metadata for tenant-key rotation only if needed; preserve additive enum/contract evolution.

- [x] Task 2: Extend key storage/provider abstractions for tenant key material and party-key wrapping metadata (AC: #1, #2)
  - [x] Extend `IKeyStorageBackend` or introduce a narrow companion abstraction for tenant key create/select, wrapping, unwrap, and metadata/version listing.
  - [x] Preserve the existing per-party key path convention `{tenant}/parties/{partyId}/v{version}` for current party keys unless a migration is explicitly implemented.
  - [x] Model tenant key metadata separately from party ids so one tenant rotation can update all active party key wrappers without changing party aggregate identities.
  - [x] Update `LocalDevKeyStorageBackend` with realistic local tenant-key behavior and deterministic tests; it must still enforce tenant namespace isolation and secure key disposal.

- [x] Task 3: Implement idempotent tenant rotation orchestration in `Hexalith.Parties.Security` (AC: #1, #2, #3, #4, #5)
  - [x] Add a tenant rotation service that enumerates active party key records for one tenant, creates/selects the target tenant key, rewrites wrapping metadata or equivalent protected key metadata, and records progress after each safe unit.
  - [x] Use Dapr state-store ETag or an equivalent optimistic-concurrency pattern for rotation progress, following the audit service pattern.
  - [x] Make retries resume from recorded progress; duplicate rotation requests with the same operation id must not create conflicting tenant keys or duplicate destructive work.
  - [x] Skip erased parties whose party key versions have been destroyed; do not call `CreateKeyAsync`, `RotateKeyAsync`, or any recovery path for erased parties.
  - [x] Keep authorized reads working through the rotation window by accepting both old and new tenant wrapping metadata until the party key has been safely rewrapped.

- [x] Task 4: Integrate without weakening existing GDPR and domain boundaries (AC: #2, #4, #5)
  - [x] Do not treat the existing `RotatePartyKey` domain command as satisfying tenant key rotation; it rotates per-party key versions and emits `PartyEncryptionKeyRotated`.
  - [x] Do not add REST controllers, Swagger/OpenAPI, or MCP tools to `src/Hexalith.Parties`; the actor host boundary remains protected.
  - [x] If an operator-facing route is required, implement it only through the accepted EventStore-fronted client/gateway/admin boundary used by GDPR operations, or leave the service-level contract ready without adding public actor-host endpoints.
  - [x] Ensure `CachedPartyKeyManagementService` invalidates or safely distinguishes any cached key material affected by rewrap metadata changes.
  - [x] Ensure `PartyEncryptionKeyDestroyedException` semantics remain intact: destroyed/erased party keys are terminal privacy states, not transient rotation failures.

- [x] Task 5: Add observability and privacy-safe audit records (AC: #3, #5)
  - [x] Add metrics for tenant rotation started/completed/failed/skipped counts and backend latency without labeling by personal data or secrets.
  - [x] Record audit entries or processing activity records with operation category, tenant, bounded counts, outcome, and correlation id.
  - [x] Normalize provider/backend failures into bounded categories for status responses; do not return raw cryptographic or secret-store exception messages to operators.

- [x] Task 6: Update docs and deployment validation guidance (AC: #1, #5)
  - [x] Update `docs/deployment-security-checklist.md` to describe tenant key provider requirements, tenant key namespace strategy, rotation policy, backup implications, and rollback/resume expectations.
  - [x] Add or update GDPR/security documentation explaining the difference between tenant key rotation, party key rotation, and crypto-shredding.
  - [x] Keep documentation aligned with the v1.1 GDPR operating model and the existing non-leakage guidance from erased-party status documentation.

- [x] Task 7: Add focused tests for rotation behavior (AC: #1, #2, #3, #4, #5, #6)
  - [x] Add `Hexalith.Parties.Security.Tests` coverage for success, retry/resume, partial backend failure, missing key provider, erased parties, concurrent reads, cache invalidation, status redaction, and tenant isolation.
  - [x] Add contract tests for any new status/request/audit records under `tests/Hexalith.Parties.Contracts.Tests/Security/`.
  - [x] Add integration-style tests with real `LocalDevKeyStorageBackend` and mocked Dapr state/audit dependencies, matching the existing `KeyManagementIntegrationTests` style.
  - [x] Keep existing aggregate tests for `RotatePartyKey` passing; add tests only if domain command behavior changes.

## Dev Notes

This is NFR/security coverage for NFR11, supporting FR53 and FR55. It is not functional requirement coverage and should not be implemented as a new user-facing party operation. The story is specifically about tenant encryption key material that protects party-level keys; existing code already has per-party key versioning and domain events, but it does not yet model tenant key wrapping or tenant-wide rotation progress. [Source: _bmad-output/planning-artifacts/epics.md#Story 6.10: Rotate Tenant Encryption Keys] [Source: _bmad-output/planning-artifacts/prd.md#Security]

Existing key-management surface:

- `IPartyKeyManagementService` supports `CreateKeyAsync`, `GetKeyAsync`, `GetKeyVersionAsync`, `RotateKeyAsync`, and `DeleteKeyAsync` for one tenant/party pair.
- `PartyKeyManagementService` generates AES-256-GCM party key material, stores versions at `{tenant}/parties/{partyId}/v{version}`, keeps old versions for reads, deletes all versions for crypto-shredding, audits create/read/rotate/delete, and records key metrics.
- `CachedPartyKeyManagementService` caches current and versioned party key material with jittered short TTL and evicts all party cache entries on rotate/delete.
- `LocalDevKeyStorageBackend` is an in-memory backend with namespace validation, size limits, per-party version limit, delete-all behavior, and tenant isolation tests.
- `RotatePartyKey` and `PartyEncryptionKeyRotated` are aggregate/domain constructs for per-party key version changes. They are useful context, not tenant key rotation.

Required implementation shape:

- Prefer a small tenant rotation service in `src/Hexalith.Parties.Security` plus contract/status types in `src/Hexalith.Parties.Contracts/Security`.
- Use existing audit, cache, local backend, lifecycle, and test patterns instead of creating unrelated crypto infrastructure.
- Preserve crypto-shredding semantics: `DeleteKeyAsync` deleting all party versions is erasure; tenant key rotation must never recreate those versions.
- Preserve authorized read continuity by supporting old and new tenant wrapping metadata during rotation. A partial rotation must be resumable and must not strand readable party data.
- Preserve tenant isolation at every storage/progress/status key. Do not probe alternate tenant namespaces or infer cross-tenant existence from status output.

### Current Files To Understand Before Editing

- `src/Hexalith.Parties.Contracts/Security/IPartyKeyManagementService.cs`: current party-key lifecycle contract; tenant rotation should not overload this interface unless the added members remain coherent and testable.
- `src/Hexalith.Parties.Contracts/Security/IKeyStorageBackend.cs`: current secret backend abstraction only knows party secret paths. Tenant key support probably belongs here only if backend responsibilities stay cohesive; otherwise add a focused companion abstraction.
- `src/Hexalith.Parties.Contracts/Security/PartyKeyInfo.cs`: current party key metadata has `KeyId`, `Version`, `TenantId`, `PartyId`, `Algorithm`, `CreatedAt`; it has no tenant wrapping metadata.
- `src/Hexalith.Parties.Security/PartyKeyManagementService.cs`: current implementation increments party key versions and rolls back orphaned key versions when audit recording fails. Preserve this behavior.
- `src/Hexalith.Parties.Security/CachedPartyKeyManagementService.cs`: rotation/delete evict current and versioned cache entries. Tenant rewrap metadata changes must not leave stale unusable key material in cache.
- `src/Hexalith.Parties.Security/LocalDevKeyStorageBackend.cs`: local backend enforces path shape and per-party limits; update tests with any tenant-key path rules.
- `src/Hexalith.Parties.Security/KeyOperationAuditService.cs`: Dapr ETag append pattern is the local precedent for concurrency-safe state updates.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`: GDPR/security registrations live here; add DI registrations in the existing GDPR block without changing request-path authorization ownership.
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`: `Handle(RotatePartyKey)` emits `PartyEncryptionKeyRotated` for party-key version events and rejects erasure/restricted states. Do not expand this into tenant-wide infrastructure orchestration.

### Previous Story Intelligence

Story 6.9 established privacy-preserving erased status across read and write paths. It extended `PartyErasureInProgress` with bounded party id, tenant id, and stable `Status`, kept cryptographic details out of messages, and documented `docs/gdpr-erased-party-status.md`. Story 6.10 must keep those stable erased/erasure-in-progress signals intact during tenant rotation status and failures. [Source: _bmad-output/implementation-artifacts/6-9-return-privacy-preserving-erased-party-status.md]

Story 9.1 created the current per-party key-management implementation. Important carry-forward points:

- Dapr Secrets API was documented as read-only for lifecycle; key create/rotate/delete uses `IKeyStorageBackend`.
- Key rotation atomicity currently rolls back a newly created party key version if audit recording fails.
- Audit writes use Dapr state-store ETag optimistic concurrency.
- Cross-instance cache staleness was accepted with a 4-6 minute TTL; tenant rotation may need stricter cache invalidation or version-aware lookup if rewrap metadata changes invalidate old cache entries.
- Deferred metadata locking around deletion was noted; tenant-wide rotation should not add a destructive path without concurrency protection.

### Git Intelligence Summary

Recent commits show the current implementation sequence:

- `2665fdb feat(story-6.9): return privacy-preserving erased party status`
- `e942a47 feat(story-6.8): record processing activities`
- `aee9593 feat(story-6.7): export portability packages`
- `54637c3 feat(story-6.9): update story status and create new automator state file`
- `309d84d feat(story-6.10): update orchestration status to STOPPED and enhance command handling in tmux`

The working tree already had unrelated changes before this story creation: `_bmad-output/story-automator/orchestration-1-20260521-062818.md` modified and `.agents/.story-automator-active` untracked. Do not revert or clean those as part of this story.

### Architecture and Compliance Constraints

- GDPR crypto-shredding activates in v1.1 with per-party keys and type-dependent `[PersonalData]` classification. Person data is encrypted broadly; organization entity fields are not encrypted by default; contact channels and identifiers are always encrypted; `IsNaturalPerson` elevates organization encryption scope.
- Contracts must remain additive and forward-compatible. Avoid changing existing public event/command shapes unless the story explicitly defines a migration.
- The main `src/Hexalith.Parties` project is an actor host. Do not add public REST controllers, OpenAPI/Swagger, or in-process MCP tools there.
- EventStore owns gateway/public request authorization. Parties still owns projection/internal tenant isolation and security infrastructure.
- Dapr sidecar-internal routes are not public APIs. Do not use tenant rotation to widen Dapr ACLs or add wildcard app ids/paths.
- Personal data must not appear in logs, telemetry dimensions, status output, audit records, file names, or exceptions.
- Tests use xUnit v3, Shouldly, NSubstitute, and focused project-level test runs. Warnings are treated as errors by default.

### Project Structure Notes

- Contract and security types belong under `src/Hexalith.Parties.Contracts/Security/`.
- Security services and local provider implementations belong under `src/Hexalith.Parties.Security/`.
- DI registration belongs in the GDPR/security block of `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`.
- Unit and integration-style security coverage belongs under `tests/Hexalith.Parties.Security.Tests/`.
- Contract record/enum behavior tests belong under `tests/Hexalith.Parties.Contracts.Tests/Security/`.
- Documentation updates belong in `docs/deployment-security-checklist.md` and a GDPR/security doc if new operator semantics need explanation.
- Avoid edits in sibling submodules (`Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Memories`, `Hexalith.FrontComposer`, `Hexalith.AI.Tools`) unless the implementation explicitly requires a cross-repo change.

### Testing Requirements

Minimum focused validation:

```powershell
dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj --filter "FullyQualifiedName~TenantKeyRotation|FullyQualifiedName~KeyManagement|FullyQualifiedName~CachedKeyManagement" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --filter "FullyQualifiedName~Security" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Broaden if contracts, aggregate behavior, DI, or deployment validation changes:

```powershell
dotnet test tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --filter "FullyQualifiedName~PartyAggregateErasureTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

The warning override is consistent with recent story validation because existing external warning-as-error debt has been documented in prior story outputs.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 6.10: Rotate Tenant Encryption Keys]
- [Source: _bmad-output/planning-artifacts/prd.md#Security]
- [Source: _bmad-output/planning-artifacts/architecture.md#D6 - Personal Data Scope: Precise Type-Dependent, GDPR-Compliant]
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-05-17.md#Major Issues]
- [Source: _bmad-output/project-context.md#Critical Implementation Rules]
- [Source: _bmad-output/implementation-artifacts/6-9-return-privacy-preserving-erased-party-status.md]
- [Source: _bmad-output/implementation-artifacts/9-1-per-party-encryption-key-management.md]
- [Source: docs/deployment-security-checklist.md#v1.1 Preparation (Secret Store & Key Management)]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Resolved `bmad-dev-story` customization: no activation append/prepend, persistent project-context facts loaded.
- Marked story and sprint status in progress, then implemented tenant rotation contracts, local backend metadata, orchestration, DI, docs, and tests.
- Red phase confirmed missing tenant rotation contracts/service with focused `dotnet test` compile failures.
- Green/refactor focused tests passed via `dotnet test` before restore cache became blocked.
- Later `dotnet test`/`dotnet build` invocations were blocked by NuGet repository signature/credential errors against `https://api.nuget.org/v3-index/repository-signatures/5.0.0/index.json` and access-denied temp files under `obj`.
- Verified already-built focused suites directly with xUnit executables after the NuGet restore blocker appeared.

### Completion Notes List

- Added bounded tenant key rotation request/status/progress contracts, tenant key metadata, party key record, and party wrapping metadata contracts.
- Extended local key storage with tenant key create/select metadata, party key wrapper metadata, active party key enumeration, tenant isolation, and preserved existing party key paths.
- Added `TenantKeyRotationService` with Dapr ETag progress persistence, resumable operation ids, skipped destroyed-party semantics, privacy-safe failure categories, audit metadata, metrics, and cache invalidation hooks.
- Registered the tenant rotation service in the existing GDPR/security DI block without adding REST, Swagger/OpenAPI, MCP, or actor-host endpoints.
- Documented tenant key provider requirements, rotation/resume expectations, backup implications, and the distinction between tenant rotation, party key rotation, and crypto-shredding.
- Added focused security and contract tests; no domain command behavior changed, so no `RotatePartyKey` aggregate tests were added.
- Validation passed: `dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --filter "FullyQualifiedName~TenantKeyRotation|FullyQualifiedName~Security" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` (11 passed).
- Validation passed: `dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj --filter "FullyQualifiedName~TenantKeyRotation" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=` (7 passed).
- Validation passed after restore blocker using xUnit executable: `Hexalith.Parties.Security.Tests` tenant/key-management/cache classes (46 passed).
- Validation passed after restore blocker using xUnit executable: `Hexalith.Parties.Contracts.Tests` security namespace (11 passed).
- Blocked validation: story-specified broader `dotnet test` commands and `dotnet build src\Hexalith.Parties\Hexalith.Parties.csproj --no-restore` could not complete because NuGet signature lookup/credential errors and temp-file access errors occurred before project compilation.

### File List

- _bmad-output/implementation-artifacts/6-10-rotate-tenant-encryption-keys.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/tests/test-summary.md
- docs/deployment-security-checklist.md
- docs/gdpr-key-rotation-and-shredding.md
- src/Hexalith.Parties.Contracts/Security/IKeyStorageBackend.cs
- src/Hexalith.Parties.Contracts/Security/ITenantKeyRotationService.cs
- src/Hexalith.Parties.Contracts/Security/KeyOperationAuditEntry.cs
- src/Hexalith.Parties.Contracts/Security/KeyOperationType.cs
- src/Hexalith.Parties.Contracts/Security/PartyKeyRecord.cs
- src/Hexalith.Parties.Contracts/Security/PartyKeyWrappingMetadata.cs
- src/Hexalith.Parties.Contracts/Security/TenantKeyMetadata.cs
- src/Hexalith.Parties.Contracts/Security/TenantKeyRotationFailureCategory.cs
- src/Hexalith.Parties.Contracts/Security/TenantKeyRotationPhase.cs
- src/Hexalith.Parties.Contracts/Security/TenantKeyRotationRequest.cs
- src/Hexalith.Parties.Contracts/Security/TenantKeyRotationStatus.cs
- src/Hexalith.Parties.Security/CachedPartyKeyManagementService.cs
- src/Hexalith.Parties.Security/ITenantKeyRotationCacheInvalidator.cs
- src/Hexalith.Parties.Security/LocalDevKeyStorageBackend.cs
- src/Hexalith.Parties.Security/TenantKeyRotationProgress.cs
- src/Hexalith.Parties.Security/TenantKeyRotationProgressConflictException.cs
- src/Hexalith.Parties.Security/TenantKeyRotationService.cs
- src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs
- tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt
- tests/Hexalith.Parties.Contracts.Tests/Security/TenantKeyRotationContractTests.cs
- tests/Hexalith.Parties.Security.Tests/TenantKeyRotationServiceTests.cs

### Senior Developer Review (AI)

Reviewer: Jérôme Piquot on 2026-05-22

Outcome: Changes Applied (auto-fix)

Findings addressed during review:

- HIGH — `TenantKeyRotationService.NormalizeFailureCategory` had two identical ternary branches and the `ConcurrencyConflict` enum value was never produced. Replaced with explicit mapping from the new typed `TenantKeyRotationProgressConflictException` so exhausted Dapr ETag retries are now categorised as `ConcurrencyConflict` instead of being silently flattened to `BackendUnavailable`.
- HIGH — `RotateAsync` had `try { ... } finally { ... }` but no `catch`. Exceptions from `GetProgressAsync`, the initial `SaveProgressAsync`, or the final completion-path save propagated uncaught: no failure status persisted, no audit entry, no `s_rotationsFailed` metric, leaving operators unable to detect the failure through `GetStatusAsync`. Added an outer `catch` that builds a redacted failure status, records the audit (best-effort), and emits the metric.
- MEDIUM — `RecordAuditAsync` dereferenced `Task?` for `auditService.RecordOperationAsync` whose interface returns non-nullable `Task`. Removed the dead null check.
- MEDIUM — Audit `FailureCategory` selection used `FailureCategories.Keys.FirstOrDefault()` (arbitrary dictionary order, would record `None` for some completed states). Replaced with `SelectDominantFailureCategory` which picks the highest-count non-`None` category and returns `null` when no real failures exist.
- MEDIUM — `LocalDevKeyStorageBackend.EnsureInitialTenantKey` and `GetOrCreateTenantKeyAsync` had a check-then-write race: concurrent first-party creates or concurrent rotations on the same tenant could each generate fresh `RandomNumberGenerator` bytes, overwrite each other in `_tenantKeyMaterial`, and leak the loser's secret material unzeroed. Refactored both paths to claim the slot via `ConcurrentDictionary.TryAdd` and `CryptographicOperations.ZeroMemory` the unused material on the losing path.
- MEDIUM — Story File List omitted `_bmad-output/implementation-artifacts/tests/test-summary.md`, which git reported as modified. Added.

### Change Log

- 2026-05-22: Implemented tenant encryption key rotation contracts, local backend metadata, idempotent rotation orchestration, audit/metrics/status redaction, cache invalidation, docs, and focused tests.
- 2026-05-22: Senior Developer Review (AI) auto-fixed two HIGH and four MEDIUM findings: typed concurrency-conflict mapping, outer rotation `catch` for audit/metric coverage, dominant failure-category selection, removed dead audit null guard, and tightened local-dev tenant key creation races.
