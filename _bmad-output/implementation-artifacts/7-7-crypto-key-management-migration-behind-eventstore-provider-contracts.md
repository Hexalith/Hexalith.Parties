---
story_key: 7-7-crypto-key-management-migration-behind-eventstore-provider-contracts
story_id: "7.7"
epic: "7"
created: 2026-06-29T18:13:37+02:00
source_status: backlog
target_status: ready-for-dev
baseline_commit: 6c9abbd
---

# Story 7.7: Crypto/key-management migration behind EventStore provider contracts

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a maintainer,
I want approved generic crypto/provider pieces migrated behind EventStore contracts,
so that Parties keeps legal policy while shared infrastructure owns reusable mechanics.

## Acceptance Criteria

1. Given Story 7.6 approved the split and its compatibility harness is green, when this story migrates runtime crypto/provider behavior, then the active Parties registration uses an approved `IEventPayloadProtectionService` path with EventStore provider-neutral metadata and typed unreadable outcomes, and the previous `PartyPayloadProtectionService` path remains restorable.
2. Given existing protected events may carry `json+pdenc-v1` bytes with missing or legacy `Unprotected` metadata, when the migrated provider reads current and historical payloads, then existing protected payloads remain readable or become safely unreadable without data loss, metadata incompatibility, or plaintext/key diagnostic leakage.
3. Given Story 7.6 found missing, destroyed, tampered, and provider-unavailable cases collapsing to `ProviderUnavailable`, when 7.7 completes, then typed EventStore unprotection distinguishes at least `KeyInvalidatedOrDeleted`, `MissingKey`, `ProviderUnavailable`, `ProviderDenied`, and `BytesMetadataMismatch` or `ConsistencyMismatch` without parsing provider exception text.
4. Given Parties owns GDPR policy and tenant/party key semantics, when generic provider mechanics are adopted, then party-specific commands, legal policy, user/admin copy, consumer self-scope, tenant/party key paths, erasure orchestration, certificates, export redaction, and processing-record redaction stay in Parties unless the accepted 7.6 ADR explicitly allows a narrow additive EventStore/shared-security API.
5. Given crypto-shredding is irreversible, when erasure has destroyed keys, then rehydration, projection delivery, export, processing records, erasure certificate reads, retry verification, and admin/consumer UX states preserve the current safe redaction/unavailable behavior and never restore personal fields.
6. Given EventStore gateway routes and public Parties contracts are already consumed, when this story completes, then public command/query shapes, EventStore `/api/v1/commands` and `/api/v1/queries` routes, DAPR `/process` routing, DAPR ACLs, GDPR legal semantics, and UI behavior remain compatible.
7. Given protected-data diagnostics are privacy-sensitive, when provider errors, unreadable outcomes, evidence artifacts, logs, telemetry, ProblemDetails, export/processing records, erasure certificates, and erasure reports are captured, then they contain only safe reason categories and no PII, key alias, destroyed-key detail, raw payload bytes, ciphertext marker detail, decrypted value, state-store key, provider exception text, actor id, or event payload.
8. Given Epic 7 is adapter-first, when rollback is exercised or inspection-validated, then restoring the previous Parties provider registration and/or rolling back the approved provider package or root submodule pointer preserves existing protected payload readability or safe unreadable classification.

## Tasks / Subtasks

- [x] Confirm Story 7.6 gate and migration owner path (AC: 1, 3, 4, 8)
  - [x] Read `_bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md` and `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md` before editing code.
  - [x] Verify the 7.6 harness expectations and record the current baseline: `CryptoKeyManagementCompatibilityHarnessTests` currently proves readable/legacy/restricted/redacted/no-leak behavior but still expects missing, destroyed, tampered, and provider-unavailable typed outcomes to collapse to `ProviderUnavailable`.
  - [x] Choose the approved migration path: consume an already-approved EventStore/shared-security provider package or root-submodule pointer, or add an owner-scoped additive EventStore/shared-security API first. Do not consume unapproved local-only APIs from a checked-out submodule.
  - [x] If EventStore source changes are required, keep them additive, provider-neutral, and validated by owner tests. Do not initialize nested submodules.

- [x] Implement precise EventStore typed unprotection for Parties protected payloads (AC: 1, 2, 3, 7)
  - [x] Add or update the active provider/adapter so new protected party payloads emit `EventStorePayloadProtectionMetadata` with `PayloadProtectionState.Protected`, a safe non-secret scheme, no raw key alias, and bounded compatibility flags.
  - [x] Preserve legacy reads where protected bytes have missing metadata or the current legacy `Unprotected` metadata. A `json+pdenc-v1` serialization format must remain enough to route through the compatibility path.
  - [x] Override `TryUnprotectEventPayloadAsync` and, if snapshots are in scope, `TryUnprotectSnapshotAsync` so unreadable outcomes are typed instead of relying on the EventStore default exception-to-`ProviderUnavailable` fallback.
  - [x] Classify known destroyed keys as `KeyInvalidatedOrDeleted` using Parties-local erasure evidence such as `IPartyErasureRecordStore.GetStatusAsync` / `GetCertificateAsync` (`KeyDestroyed`, `Verified`, `Erased`) rather than moving GDPR policy into EventStore.
  - [x] Classify missing key material not known destroyed as `MissingKey`; provider outage as `ProviderUnavailable`; provider policy/permission denial as `ProviderDenied`; malformed/corrupt marker or bytes/metadata disagreement as `BytesMetadataMismatch` or `ConsistencyMismatch`.
  - [x] Do not parse exception messages as policy. Use typed exceptions, provider status codes, adapter-owned failure categories, or safe explicit outcomes.

- [x] Migrate DI registration behind a reversible adapter switch (AC: 1, 4, 6, 8)
  - [x] Update `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` so production registration uses the approved provider/adapter through `IEventPayloadProtectionService`.
  - [x] Keep `PartyPayloadProtectionService` or an equivalent compatibility adapter available as the rollback path until Story 7.8 explicitly removes proven duplicate infrastructure.
  - [x] Preserve existing `IKeyStorageBackend`, `IPartyKeyManagementService`, `IPartyKeyLifecycleService`, `IPartyErasureRecordStore`, `PartyErasureOrchestrator`, `ErasureVerificationService`, and `DecryptionCircuitBreaker` behavior unless the 7.6 ADR explicitly allows a generic shared primitive.
  - [x] Do not change public command/query contracts, EventStore gateway routes, DAPR ACLs, admin/consumer authorization, consumer self-scope, UI copy, or GDPR legal semantics.

- [x] Preserve erasure, redaction, and no-leak behavior end to end (AC: 4, 5, 7)
  - [x] Keep `json-redacted` fallback behavior for destroyed-key replay and corrupted protected markers.
  - [x] Verify `PartyDomainServiceInvoker` and `PartyProjectionUpdateOrchestrator` still allow post-erasure tail commands (`MarkPartyEncryptionKeyDeleted`, `MarkErasureVerified`, `CompletePartyErasure`) to proceed with redacted historical personal-data fields.
  - [x] For every new or touched protected-data diagnostic path, remove raw exception messages, exception objects, tenant/party actor ids, key aliases, state-store keys, payload bytes, and ciphertext marker details; log safe failure category/reason code only. Current 7.6 hardening already sanitized key lifecycle, retry actor, erasure orchestrator, verification, and payload protection exception text, but 7.7 must not copy older tenant/party-id logging patterns into the migrated provider path.
  - [x] Keep erasure certificates, erasure reports, export packages, and processing records PII-free and do not echo provider-private metadata.
  - [x] Treat key aliases as sensitive-by-default even though `EventStorePayloadProtectionMetadata.KeyAlias` exists.

- [x] Extend behavior tests and harness evidence (AC: 1, 2, 3, 5, 7, 8)
  - [x] Update `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` so destroyed, missing, tampered, and provider-unavailable cases assert the new precise unreadable categories.
  - [x] Add metadata compatibility tests for: new protected metadata, legacy missing metadata, legacy `Unprotected` metadata with `json+pdenc-v1`, metadata/bytes mismatch, provider-opaque metadata, and safe fallback for malformed metadata.
  - [x] Add no-leak sentinel coverage for provider metadata, key alias, state-store key, provider exception text, raw payload bytes, ciphertext marker detail, erasure status/certificate evidence, logs, exception messages, serialized evidence artifacts, and ProblemDetails-like payloads.
  - [x] Add or update focused tests around `PartyDomainServiceInvoker` and `PartyProjectionUpdateOrchestrator` if typed unreadable outcomes change their control flow.
  - [x] If EventStore contracts/server source changes, add or update owner tests in `references/Hexalith.EventStore/tests/Hexalith.EventStore.Contracts.Tests/Security/` and/or `references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/Security/`.

- [x] Validate and record release/rollback evidence (AC: 1-8)
  - [x] Run `git diff --check`.
  - [x] Run `bash scripts/check-no-warning-override.sh`.
  - [x] Run `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` or record the exact blocker.
  - [x] Run `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` or record the exact blocker.
  - [x] Run the full `Hexalith.Parties.Security.Tests` assembly directly with xUnit v3. If focused filtering is used first, still run the full assembly before completion because Story 7.6 review found a missed regression outside the initial focused subset.
  - [x] Run focused Parties domain/projection tests covering rehydration, projection delivery, retry erasure verification, erasure certificate query, export/processing record redaction, and gateway routing.
  - [x] If EventStore source is touched, run the focused EventStore owner build/tests for contracts/security/server protected-data behavior and record the exact commands.
  - [x] Run `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false` if the known unrelated submodule reference drift is resolved; otherwise record the same blocker precisely and provide focused evidence.
  - [x] Document rollback: previous provider registration, provider package/root submodule pointer, data compatibility condition, and proof that legacy protected payloads remain readable or safely unreadable.

## Dev Notes

### Story Classification

- Epic 7 is post-MVP platform maintenance and adds no PRD functional requirements. This story must not be reported as MVP feature delivery. [Source: `_bmad-output/planning-artifacts/epics.md#Epic-7-Platform-Alignment---adopt-Commons/EventStore-Class-B`]
- Story 7.7 depends on Story 7.6. Sprint status and the completed 7.6 story show Story 7.6 is done, the ADR exists, the harness was reviewed, and the full security test assembly passed after review fixes. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#development_status`; `_bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md#Debug-Log-References`]
- Architecture rule AD-3 is binding: EventStore/shared security owns generic payload-protection contracts, metadata, workflow vocabulary, and provider hooks; Parties retains party-specific commands, GDPR policy, user/admin copy, tenant/party key semantics, and compatibility adapters until 7.6 approves a migration. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-3---Crypto-Placement-Gate`]

### Required Source Discovery Results

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, with Epic 7 sequencing and Story 7.7 requirements.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`, `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md`, `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md`, and `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md`.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/parties-ui-prd.md`; Story 7.7 adds no PRD functional coverage.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/index.md` plus targeted `DESIGN.md`, `EXPERIENCE.md`, and regulated-language review excerpts for erasure, export, processing-record, privacy-copy, and state requirements. There is no intended UI work. UX relevance is limited to preserving existing key-unavailable, erasure, export, processing-record, and admin/consumer copy behavior.
- Loaded persistent facts from `_bmad-output/project-context.md` and reference project contexts under `references/Hexalith.EventStore`, `references/Hexalith.Tenants`, `references/Hexalith.FrontComposer`, and `references/Hexalith.Memories`.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md`.
- Loaded current Parties security implementation, EventStore payload-protection contracts/carrier/redactor, security harness tests, sprint status, and recent git history.

### Current Files Being Modified - Required Reading

Read each UPDATE or CREATE target completely before editing it.

- `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs` (UPDATE or wrap)
  - Current state: implements EventStore `IEventPayloadProtectionService`, protects party-domain `[PersonalData]` fields using AES-GCM JSON field envelopes (`json+pdenc-v1`), redacts encrypted markers to `json-redacted` after destroyed keys, short-circuits redacted payloads, and records decryption failures in `DecryptionCircuitBreaker`.
  - Current gap: it does not override typed EventStore unprotection; default interface behavior maps any non-cancellation exception to `ProviderUnavailable`. It also returns default `Unprotected` metadata even when bytes are protected.
  - Preserve: domain `party` handling only, AES-256-GCM envelope readability, legacy `json+pdenc-v1` format, key zeroing, redaction fallback, no PII/key/provider diagnostics, and existing serialization formats.

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (UPDATE)
  - Current state: registers `IKeyStorageBackend -> LocalDevKeyStorageBackend`, `PartyKeyManagementService`, cached `IPartyKeyManagementService`, lifecycle/retry services, erasure store/orchestrator pieces, `DecryptionCircuitBreaker`, and `IEventPayloadProtectionService -> PartyPayloadProtectionService`.
  - What this story changes: switch active `IEventPayloadProtectionService` registration to the approved provider/adapter while keeping rollback to the previous Parties provider path.
  - Preserve: root submodule project references, no public API additions, EventStore gateway routing, DAPR ACL assumptions, Central Package Management, and no `Version=` in project files.

- `src/Hexalith.Parties.Security/PartyKeyManagementService.cs` and `src/Hexalith.Parties.Security/CachedPartyKeyManagementService.cs` (UPDATE only if provider classification requires it)
  - Current state: create/read/rotate/delete AES-256 party keys; missing key versions normalize to `PartyEncryptionKeyDestroyedException`; delete returns `ErasureCertificate`; cache invalidates on delete/rotation.
  - Current caveat: the exception name says destroyed, but absence of key material alone does not prove GDPR erasure unless correlated with Parties erasure status/certificate evidence.
  - Preserve: audit behavior, key zeroing, rotation rollback on audit failure, no raw key/key-path leakage, and idempotent delete behavior.

- `src/Hexalith.Parties.Security/PartyErasureRecordStore.cs` and `src/Hexalith.Parties.Contracts/Security/IPartyErasureRecordStore.cs` (READ; UPDATE only if needed)
  - Current state: persists erasure status, erasure certificate, and verification report in DAPR state.
  - Use for 7.7: classify `KeyInvalidatedOrDeleted` only when status/certificate evidence says `KeyDestroyed`, `Verified`, or `Erased`; otherwise treat absent key material as `MissingKey` or provider failure.
  - Preserve: store key shape unless a separate migration is approved, and do not expose state-store keys in diagnostics.

- `src/Hexalith.Parties.Security/PartyKeyLifecycleService.cs`, `PartyKeyRetryActor.cs`, `PartyErasureOrchestrator.cs`, and `ErasureVerificationService.cs` (READ; UPDATE only if touched)
  - Current state: key infrastructure failures are now logged/persisted with safe generic messages after Story 7.6 review fixes.
  - Preserve: party creation does not fail solely because key infrastructure is unavailable, durable retry remains bounded, erasure verification sanitizes store error messages, and raw provider exception text is never logged or persisted.

- `src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs` (READ; UPDATE only if provider/circuit ownership changes)
  - Current state: per-party circuit breaker with Closed/Open/HalfOpen states and meter/log output.
  - Preserve: per-party isolation and bounded status. If a shared circuit-breaker primitive is adopted, classify tenant/party metric tags under the no-leak policy before reuse and avoid adding payload/key/provider labels.

- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` and `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs` (UPDATE if typed unreadable control flow changes)
  - Current state: catch exact `PartyEncryptionKeyDestroyedException` and use `PartyPayloadProtectionService.RedactProtectedPayload(...)` so post-erasure tail commands/projections can proceed with redacted historical personal-data fields.
  - Current risk: existing fallback logging includes exception message or exception object in some paths. If this story touches these paths, sanitize to safe failure category/type only.
  - Preserve: no fallback for transient provider outages, key-version mismatches, or policy denials; those must fail/degrade instead of silently redacting recoverable data.

- `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/` (UPDATE only for owner-approved additive API)
  - Current state: provider-neutral `IEventPayloadProtectionService`, `PayloadProtectionResult`, `EventStorePayloadProtectionMetadata`, `EventStorePayloadProtectionMetadataCarrier`, `PayloadUnprotectionOutcome`, `SnapshotUnprotectionOutcome`, unreadable reason taxonomy, readability decisions, and crypto-shredding workflow records already exist.
  - Preserve: additive-only contract evolution, metadata validation, provider-neutral shape, no raw keys/plaintext/nonce/tag/provider blobs, and owner-submodule validation.

- `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs` and EventStore server protected-data tests (READ; UPDATE only for owner-approved shared diagnostic gaps)
  - Current state: central redactor maps protected-data diagnostics to safe reason/stage text and whitelisted ProblemDetails extensions.
  - Preserve: no provider exception text or secret-shaped metadata on public diagnostics.

- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` and related security tests (UPDATE)
  - Current state: executable 7.6 harness covers readable round trip, legacy unprotected, restricted-readable, destroyed-key redaction, missing key, tampered payload, provider unavailable, and no-leak evidence.
  - What this story changes: expected unreadable categories become precise; metadata compatibility and rollback evidence are added.
  - Preserve: xUnit v3, Shouldly, NSubstitute, behavior tests over real services, and no static source-text assertions.

### EventStore API Facts

- `IEventPayloadProtectionService.TryUnprotectEventPayloadAsync` default implementation delegates to metadata-aware unprotect and maps any non-cancellation exception to `ProviderUnavailable`; custom providers must override this for precise unreadable categories. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`]
- `EventStorePayloadProtectionMetadata` supports `Protected`, `Unprotected`, and `ProviderOpaque` states; `KeyAlias` exists but is sensitive-by-default, and metadata must never embed raw keys, plaintext, IVs/nonces, tags, or provider-private blobs. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata.cs`]
- `EventStorePayloadProtectionMetadataCarrier` serializes metadata under `eventstore.protection`, maps missing metadata to a legacy `Unprotected` record with `legacy=missing`, and maps malformed/forbidden metadata to `ProviderOpaque`. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs`]
- `PayloadUnprotectionOutcome` carries readable bytes only when readable; unreadable outcomes carry reason and metadata only. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/PayloadUnprotectionOutcome.cs`]
- `UnreadableProtectedDataReason` includes `MissingKey`, `KeyInvalidatedOrDeleted`, `ProviderUnavailable`, `ProviderDenied`, `ConsistencyMismatch`, `MalformedMetadata`, `UnknownMetadataVersion`, `ProviderOpaqueUnsupportedOperation`, and `BytesMetadataMismatch`. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/UnreadableProtectedDataReason.cs`]
- `NoOpEventPayloadProtectionService` returns readable output for unprotected metadata and `MissingKey` for protected metadata it cannot satisfy. Use it as EventStore compatibility context, not as the Parties provider. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs`]
- EventStore has owner tests for metadata carrier, unreadable taxonomy, payload protection hooks, diagnostic redaction, and unreadable protected-data runtime behavior. Extend those only if this story changes EventStore source. [Source: `references/Hexalith.EventStore/tests/Hexalith.EventStore.Contracts.Tests/Security/`; `references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/Security/`]

### Previous Story Intelligence

- Story 7.6 created the accepted crypto/key-management split ADR and harness. Review then found and fixed a missed no-leak issue in `PartyKeyRetryActor.ReceiveReminderAsync` and a related `PartyKeyLifecycleServiceTests` regression; the final full `Hexalith.Parties.Security.Tests` assembly passed 154 tests. [Source: `_bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md#Debug-Log-References`]
- Story 7.6 explicitly recorded the 7.7 blocker: current Parties typed unprotection still collapses destroyed/missing/tampered/provider-unavailable failures to EventStore's default `ProviderUnavailable`; 7.7 must add a precise override or consume an approved provider that supplies it. [Source: `_bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md#Completion-Notes-List`]
- Carry forward Story 7.6 hygiene: use behavioral compatibility harnesses, scan captured logs/evidence for sentinels, do not stage unrelated submodule pointer moves, and record exact blockers for full solution build if unrelated submodule drift remains.
- Recent commits are `feat(story-7.6): add crypto key management compatibility harness`, `feat(story-7.5): complete projection checkpoint rebuild migration`, `feat: complete story 7.4 projection platform adapter`, `feat: complete story 7.3 search normalization`, and `feat(story-7.2): Commons ServiceDefaults, correlation, ProblemDetails, and paging`. [Source: `git log -5`]

### Project Structure Notes

- Parties-specific crypto implementation lives in `src/Hexalith.Parties.Security/`.
- Public party security abstractions live in `src/Hexalith.Parties.Contracts/Security/`; keep this project infrastructure-free.
- The Parties actor host composition and active provider registration live in `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`.
- EventStore provider-neutral contracts live under `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/`.
- EventStore server protected-data diagnostics and runtime behavior live under `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/`.
- Focused Parties crypto/security tests live in `tests/Hexalith.Parties.Security.Tests/`; domain/projection fallback tests live under `tests/Hexalith.Parties.Tests/`.
- Generated `bin/` and `obj/` trees are not source and must not be edited or cited as implementation targets.

### Testing and Validation Guidance

Run smallest reliable checks first, then broaden:

- `git diff --check`
- `bash scripts/check-no-warning-override.sh`
- `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`
- `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`
- Direct xUnit v3 execution for the full `Hexalith.Parties.Security.Tests` assembly.
- Focused direct xUnit v3 execution for:
  - `Hexalith.Parties.Security.Tests.CryptoKeyManagementCompatibilityHarnessTests`
  - `Hexalith.Parties.Security.Tests.PartyPayloadProtectionServiceTests`
  - `Hexalith.Parties.Security.Tests.PartyPayloadProtectionRedactTests`
  - `Hexalith.Parties.Security.Tests.PartyKeyLifecycleServiceTests`
  - `Hexalith.Parties.Security.Tests.PartyKeyManagementServiceTests`
  - `Hexalith.Parties.Security.Tests.KeyManagementIntegrationTests`
  - `Hexalith.Parties.Security.Tests.ErasureVerificationServiceTests`
  - `Hexalith.Parties.Security.Tests.LocalDevKeyStorageBackendTests`
- Focused Parties domain/projection tests covering destroyed-key redaction fallback and erasure certificate/status reads.
- If EventStore source is touched, run the focused EventStore contract/security/server tests for metadata, unreadable taxonomy, payload hooks, diagnostic redaction, and unreadable protected-data behavior. Follow EventStore's project-context rule: run test projects individually, not solution-level `dotnet test`.
- Full solution build only if known unrelated submodule drift is resolved; otherwise record the exact blocker and provide focused evidence.

### Latest Technical Information

- No external package or framework upgrade is required for Story 7.7. Use the pinned local stack from project context: .NET SDK `10.0.301`, `net10.0`, EventStore project references under `references/`, xUnit v3, Shouldly, and NSubstitute.
- The relevant current source of truth is the checked-out EventStore contracts and tests under `references/Hexalith.EventStore`, plus the accepted 7.6 ADR. Do not change package versions for this story unless an owner-approved provider package is explicitly part of the migration.

### Rollback Plan

- Primary rollback: restore the previous `IEventPayloadProtectionService -> PartyPayloadProtectionService` production registration and any previous key-provider registration.
- Pointer rollback: if an approved provider package or root EventStore submodule pointer was updated, roll it back to the pre-story version/commit.
- Data compatibility condition: existing `json+pdenc-v1` payloads remain readable, or if keys are destroyed/unavailable, classified through safe unreadable outcomes without exposing personal data.
- Contract condition: public Parties command/query contracts, EventStore gateway routes, DAPR `/process`, DAPR ACLs, GDPR semantics, consumer self-scope, export/processing records, erasure certificates, and UI behavior remain compatible.
- Local code deletion is not the rollback mechanism. Keep compatibility code until Story 7.8 records final readiness and evidence-backed cleanup.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.7-Crypto/key-management-migration-behind-EventStore-provider-contracts`]
- [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.7---CryptoKey-Management-Migration-Behind-EventStore-Provider-Contracts`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-3---Crypto-Placement-Gate`]
- [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Story-77`]
- [Source: `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md#Approved-Story-77-Production-Scope`]
- [Source: `_bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas`]
- [Source: `references/Hexalith.EventStore/_bmad-output/project-context.md#Critical-Dont-Miss-Rules`]
- [Source: `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`]
- [Source: `src/Hexalith.Parties.Security/PartyKeyManagementService.cs`]
- [Source: `src/Hexalith.Parties.Security/PartyErasureRecordStore.cs`]
- [Source: `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs`]
- [Source: `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs`]

## Validation Summary

- Source discovery loaded project context facts, reference project contexts, sprint status, canonical epics, PRD, UX design index, whole architecture, Epic 7 architecture spine, release/rollback plan, 7.6 ADR, previous story intelligence, current Parties security implementation, EventStore security contracts/redactor/tests, focused security test inventory, and recent git history.
- Checklist fixes applied before finalizing: made 7.6 prerequisite explicit, identified precise UPDATE files, required typed unreadable category migration rather than generic provider replacement, preserved legacy `json+pdenc-v1` compatibility, added erasure-record-based destroyed-key classification, named existing fallback logging risks, required full security assembly validation, and documented rollback.
- Latest-technology review found no external dependency upgrade requirement. This story relies on pinned local .NET 10, current root submodule sources, accepted Epic 7 architecture, and the Story 7.6 ADR rather than changing package versions.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-29T18:21:02+02:00 - Story and sprint status moved to in-progress; existing `baseline_commit: 6c9abbd` preserved.
- 2026-06-29T18:30:27+02:00 - Chose Parties-owned reversible adapter path. No EventStore/shared submodule source was changed and no nested submodules were initialized.
- 2026-06-29T18:30:27+02:00 - Added `EventStorePartyPayloadProtectionAdapter` as the active `IEventPayloadProtectionService` registration while keeping concrete `PartyPayloadProtectionService` registered as rollback-compatible provider.
- 2026-06-29T18:30:27+02:00 - Focused security validations passed: `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`; `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`; direct xUnit for `CryptoKeyManagementCompatibilityHarnessTests` (14 tests, 0 failed, 0 skipped); full direct xUnit `Hexalith.Parties.Security.Tests` assembly (160 tests, 0 failed, 0 skipped).
- 2026-06-29T18:30:27+02:00 - Quality gates passed: `git diff --check`; `bash scripts/check-no-warning-override.sh`.
- 2026-06-29T18:30:27+02:00 - Focused Parties domain/projection validation and full solution build attempted with `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`, `dotnet build src/Hexalith.Parties/Hexalith.Parties.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`, and `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false`; all are blocked by pre-existing submodule reference drift: `Hexalith.EventStore.Admin.Server` cannot resolve `Hexalith.Tenants.*`, and `Hexalith.Tenants.Contracts` cannot resolve `Hexalith.EventStore.*` contract types.

### Completion Notes List

- Confirmed Story 7.6 is done and the ADR is accepted; 7.7 uses the approved Parties-owned reversible adapter path rather than consuming unapproved local-only submodule APIs.
- Added an EventStore-facing Parties payload protection adapter that emits `PayloadProtectionState.Protected` metadata with a safe scheme, no key alias, and bounded compatibility flags for new `json+pdenc-v1` payloads.
- Overrode typed event and snapshot unprotection in the adapter so destroyed, missing, provider-unavailable, provider-denied, provider-opaque, malformed metadata, and bytes/metadata mismatch paths return precise EventStore unreadable categories without parsing provider exception text.
- Preserved legacy protected reads where metadata is missing or `Unprotected` while the serialization format is `json+pdenc-v1`.
- Kept the existing `PartyPayloadProtectionService` concrete registration available as rollback path; active production `IEventPayloadProtectionService` now resolves to `EventStorePartyPayloadProtectionAdapter`.
- Sanitized touched destroyed-key rehydration/projection fallback diagnostics to remove exception messages, exception objects, tenant/party actor ids, payload bytes, key aliases, and provider-private details.
- Rollback evidence: restore the previous `IEventPayloadProtectionService -> PartyPayloadProtectionService` registration; no provider package or root submodule pointer was intentionally changed; legacy protected payloads remain readable or become safely unreadable through typed outcomes.

### File List

- `_bmad-output/implementation-artifacts/7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs`
- `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs`
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs`
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementHarnessServices.cs`
- `tests/Hexalith.Parties.Security.Tests/InMemoryPartyErasureRecordStore.cs`
- `tests/Hexalith.Parties.Tests/Projections/ProjectionPlatformAdapterTests.cs`

### Change Log

- 2026-06-29 - Migrated active Parties payload protection behind a reversible EventStore adapter with protected metadata emission and precise typed unreadable outcomes.
- 2026-06-29 - Extended crypto/key-management harness coverage for precise categories, metadata compatibility, provider-opaque handling, provider denial, and malformed marker fallback.
- 2026-06-29 - Sanitized touched destroyed-key redaction fallback diagnostics and recorded release/rollback validation evidence.
- 2026-06-29 - Senior Developer Review (AI): fixed an unbounded `stackalloc` in `EventStorePartyPayloadProtectionAdapter.IsBase64String` (stack-overflow risk on the unprotection read path for large protected fields) and added a large-field round-trip regression test. Full `Hexalith.Parties.Security.Tests` assembly green at 165 tests.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot (adversarial AI review)
**Date:** 2026-06-29
**Outcome:** Approve (1 MEDIUM finding fixed automatically; 0 CRITICAL)

### Scope

Reviewed the active File List for Story 7.7: the new `EventStorePartyPayloadProtectionAdapter`, the reversible DI migration in `PartiesServiceCollectionExtensions`, the destroyed-key redaction diagnostic sanitization in `PartyDomainServiceInvoker` and `PartyProjectionUpdateOrchestrator`, and the extended security harness (`CryptoKeyManagementCompatibilityHarnessTests`, `CryptoKeyManagementHarnessServices`, `InMemoryPartyErasureRecordStore`, `ProjectionPlatformAdapterTests`). Cross-checked the adapter against the EventStore provider-neutral contracts under `references/Hexalith.EventStore/.../Security/`.

### AC / Task validation

- AC1–AC3, AC7 (typed precise unreadable categories, protected metadata emission, no-leak): VERIFIED. The adapter overrides `TryUnprotectEventPayloadAsync`/`TryUnprotectSnapshotAsync` and maps `KeyInvalidatedOrDeleted`, `MissingKey`, `ProviderUnavailable`, `ProviderDenied`, `BytesMetadataMismatch`, `ConsistencyMismatch`, `ProviderOpaqueUnsupportedOperation`, `MalformedMetadata`, and `UnknownMetadataVersion` without parsing provider exception text. Destroyed-key classification is gated on Parties-local erasure evidence (`IPartyErasureRecordStore` status/certificate), not key absence. Emitted metadata carries no key alias and no secret-shaped fields. Each is covered by a behavioral harness test.
- AC2, AC8 (legacy reads, rollback): VERIFIED. Legacy missing/`Unprotected` metadata with `json+pdenc-v1` stays readable; `PartyPayloadProtectionService` remains registered as the rollback provider (asserted by `AddParties_RegistersEventStorePayloadProtectionAdapterWithRollbackProvider`).
- AC4–AC6 (Parties keeps GDPR policy; erasure/redaction preserved; contracts/routes unchanged): VERIFIED. The adapter is Parties-owned; the projection/rehydration redaction fallback still fires on the typed `PartyEncryptionKeyDestroyedException` (the adapter's legacy `UnprotectEventPayloadAsync` is a pure pass-through that does not swallow it). Touched fallback diagnostics now log only event type + exception type name — no tenant/party id, exception message, or payload.
- File List vs git: ACCURATE. Every changed source/test file is listed; the only undocumented working-tree changes are `references/*` submodule-pointer drift and `_bmad-output/*` artifacts, both out of scope by story rule.

### Findings

- **[MEDIUM][FIXED] Unbounded `stackalloc` on the unprotection read path.** `EventStorePartyPayloadProtectionAdapter.IsBase64String` allocated `stackalloc byte[encoded.Length]`, where `encoded` is the base64 of the ciphertext (`"c"`) marker field. That length equals the protected `[PersonalData]` field size and is data-controlled, and `InspectPayloadShape` runs on every `TryUnprotectEventPayloadAsync` (projection/replay). A large protected field would overflow the stack (uncatchable process crash). Fixed by validating with allocation-free, bounded `System.Buffers.Text.Base64.IsValid` and added a 1.5 MB-field round-trip regression test (`LargeProtectedField_TryUnprotect_RoundTripsWithoutStackExhaustionAsync`).

### Validation evidence

- `git diff --check` — clean. `bash scripts/check-no-warning-override.sh` — OK.
- `dotnet build src/Hexalith.Parties.Security/...` (Release, `-m:1`, `MinVerVersionOverride=1.0.0`) — 0 warnings, 0 errors.
- `dotnet build tests/Hexalith.Parties.Security.Tests/...` (Release) — 0 warnings, 0 errors.
- Direct xUnit v3 full assembly `Hexalith.Parties.Security.Tests` — **165 total, 0 failed, 0 skipped** (was 164 pre-fix; +1 regression test).
- `src/Hexalith.Parties` host build and `tests/Hexalith.Parties.Tests` remain blocked by the same pre-existing submodule reference drift the dev recorded (`Hexalith.Tenants.Contracts` cannot resolve `Hexalith.EventStore` contract types — `IEventPayload`/`IQueryContract`/`IRejectionEvent`). The two touched domain files were statically verified: `LoggerMessage` EventId 8405 is unique and the new signature matches its call site (`SequenceNumber` is `long`).
