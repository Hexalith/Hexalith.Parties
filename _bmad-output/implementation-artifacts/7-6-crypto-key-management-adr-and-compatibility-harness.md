---
story_key: 7-6-crypto-key-management-adr-and-compatibility-harness
story_id: "7.6"
epic: "7"
created: 2026-06-29T17:29:13+02:00
source_status: backlog
target_status: ready-for-dev
baseline_commit: c5ffed8
---

# Story 7.6: Crypto/key-management ADR and compatibility harness

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As an architect and security reviewer,
I want the crypto-shredding and key-management split decided and proved before any migration,
so that shared-platform adoption cannot weaken GDPR erasure guarantees.

## Acceptance Criteria

1. Given Parties currently owns party-specific GDPR policy and crypto-shredding behavior, when this story completes, then a new ADR classifies payload protection, key storage, key wrapping, key rotation, crypto-shredding workflow, audit, circuit breaker, event-type resolution, provider metadata, and unreadable-data classification into EventStore/shared security versus Parties policy.
2. Given Story 7.7 depends on this decision, when the ADR is accepted, then it explicitly names which production changes are allowed in 7.7, which APIs must be added or pinned in EventStore/shared security before adoption, and which Parties behaviors must remain local.
3. Given existing Parties encryption uses AES-GCM field envelopes and party keys, when the compatibility harness runs, then it proves readable round trip, tampered/unreadable protected payload, missing/destroyed key, provider-unavailable, erased/redacted, restricted-but-readable, and legacy unprotected payload cases against the real `PartyPayloadProtectionService` and EventStore payload-protection contracts.
4. Given protected-data failure paths are privacy-sensitive, when harness evidence is captured, then no PII, key alias, destroyed-key detail, raw payload bytes, ciphertext marker detail, decrypted value, state-store key, provider exception text, actor id, or event payload appears in logs, exceptions, ProblemDetails JSON, telemetry labels, processing records, exports, erasure certificates, erasure reports, or evidence artifacts.
5. Given Epic 7 uses adapter-first strangler migration, when this story changes source code, then production provider registration and runtime behavior remain unchanged unless the change is a compatibility adapter required by the harness and explicitly documented as non-migrating. Story 7.7 remains blocked until the harness is green.
6. Given public Parties contracts and EventStore gateway routes are already consumed, when this story completes, then public command/query shapes, EventStore `/api/v1/commands` and `/api/v1/queries` routes, DAPR `/process` routing, DAPR ACLs, GDPR legal semantics, consumer self-scope, and UI behavior remain compatible.

## Tasks / Subtasks

- [x] Produce the crypto/key-management split ADR (AC: 1, 2, 5)
  - [x] Create `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md`.
  - [x] Classify each item: payload protection contracts, AES-GCM provider mechanics, payload metadata, unreadable-data reason mapping, key storage, party key path/alias shape, key wrapping, key rotation, tenant key rotation, crypto-shredding workflow, audit, circuit breaker, restored/legacy backup handling, event-type resolution, and leak sentinel strategy.
  - [x] Keep party-specific commands, legal policy, user/admin copy, tenant/party key semantics, erasure orchestration, erasure certificates, export/processing-record redaction, and consumer/admin authorization in Parties unless the ADR explicitly routes a narrow additive API to EventStore/shared security.
  - [x] Identify any EventStore/shared-security additive API or pointer/package prerequisite for Story 7.7. Do not consume unapproved local-only APIs from a submodule checkout.
  - [x] Include the rollback set for 7.7: restore `PartyPayloadProtectionService`/key-provider registration, roll back provider package or submodule pointer, and preserve existing protected payload readability or safe unreadable classification.

- [x] Build the compatibility harness over real behavior (AC: 3, 5)
  - [x] Add a focused harness test class, preferably `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs`. Use xUnit v3, Shouldly, and NSubstitute.
  - [x] Use real `PartyPayloadProtectionService`, `PartyKeyManagementService`, `LocalDevKeyStorageBackend`, `PartyKeyLifecycleService`, and `DecryptionCircuitBreaker` unless a case specifically requires a controlled fake backend/provider.
  - [x] Assert readable round trip for protected party PII through `IEventPayloadProtectionService` and EventStore `PayloadProtectionResult` metadata conventions.
  - [x] Assert legacy unprotected payloads pass through unchanged and remain readable with `PayloadProtectionState.Unprotected` or equivalent compatibility metadata.
  - [x] Assert restricted parties remain readable; processing restriction is legal-policy state, not crypto destruction.
  - [x] Assert erased/destroyed-key payloads follow the existing redaction path and do not restore personal fields.
  - [x] Assert tampered/corrupt protected payloads become a bounded unreadable outcome or controlled exception path that the ADR classifies. Do not parse provider exception text as public policy.
  - [x] Assert missing key and provider-unavailable are distinguishable in harness evidence if the current EventStore contract supports it; if current Parties code cannot distinguish them yet, record the additive API or provider override required before 7.7.
  - [x] Do not add static source-text tests. Evidence must execute behavior.

- [x] Prove protected-data no-leak behavior across outputs (AC: 4)
  - [x] Reuse `references/Hexalith.EventStore/src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs` if available through existing project references; otherwise add an equivalent local test-only sentinel without changing production contracts.
  - [x] Inject sentinel plaintext, key alias, provider-private blob, state-store key, connection string fragment, and provider exception text into harness inputs.
  - [x] Capture logger output and thrown exception messages for protection, unprotection, redaction, key lifecycle, key deletion, erasure verification, and circuit-breaker paths.
  - [x] Serialize relevant ProblemDetails-like error payloads, `ErasureCertificate`, `ErasureVerificationReport`, processing records/export outputs, and any harness evidence artifact; scan all captured strings/files for sentinels.
  - [x] Treat key aliases as sensitive-by-default even though EventStore metadata allows a `KeyAlias` field. Evidence must not echo raw aliases.

- [x] Preserve runtime and package boundaries (AC: 5, 6)
  - [x] Do not change EventStore gateway routing, public Parties host APIs, DAPR ACLs, consumer self-scope, admin/consumer UI copy, or public command/query contracts.
  - [x] Do not migrate production provider registration to a new EventStore/shared provider in this story. Registration remains `IEventPayloadProtectionService -> PartyPayloadProtectionService` unless the only change is a test-only harness registration.
  - [x] Keep `Hexalith.Parties.Contracts` infrastructure-free. Do not add EventStore Server, DAPR, ASP.NET, persistence, or provider implementation dependencies to contracts.
  - [x] If EventStore contract/source changes are required, make them additive, owner-scoped, and documented in the ADR; run the focused EventStore owner build/tests that cover the changed package. Never initialize nested submodules.
  - [x] Keep Central Package Management: no `Version=` attributes in `.csproj`, no casual package bumps, no `.sln` creation.

- [x] Validate and record handoff evidence (AC: 2-6)
  - [x] Run `git diff --check`.
  - [x] Run `bash scripts/check-no-warning-override.sh`.
  - [x] Run `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` or record the exact blocker.
  - [x] Run `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` or record the exact blocker.
  - [x] Run the security test assembly directly with xUnit v3 single-dash args for the new harness class, plus existing focused classes listed below.
  - [x] Run `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false` if the known unrelated submodule drift is resolved; otherwise record the same blocker precisely and provide focused evidence.
  - [x] Record in this story's Dev Agent Record that Epic 7 remains post-MVP platform maintenance and adds no PRD functional coverage.

## Dev Notes

### Story Classification

- Epic 7 is post-MVP platform maintenance. Story 7.6 is a decision and proof-harness slice, not feature delivery and not the production crypto migration. [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.6-Crypto/key-management-ADR-and-compatibility-harness`]
- Story 7.7 is the migration story and remains blocked until this ADR is accepted and the harness is green. [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Story-76`]
- Architecture rule AD-3 is binding: EventStore/shared security owns generic payload-protection contracts, metadata, workflow vocabulary, and provider hooks; Parties retains party-specific commands, GDPR policy, copy, tenant/party key semantics, and compatibility adapters until this story approves a migration. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-3---Crypto-Placement-Gate`]

### Required Source Discovery Results

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, with Epic 7 sequencing and Story 7.6 requirements.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`, `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md`, and `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md`.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/parties-ui-prd.md`; Story 7.6 adds no PRD functional coverage.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/index.md` and `EXPERIENCE.md`; there is no UI work in scope. UX guardrails matter only if an implementation accidentally touches UI, ProblemDetails, admin/consumer copy, or GDPR visible states.
- Loaded persistent facts from `_bmad-output/project-context.md`.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/7-5-projection-checkpoint-rebuild-migration-and-local-code-removal.md`.
- Loaded current Parties security implementation, EventStore payload-protection contracts, existing security tests, sprint status, and recent git history.

### Current Files Being Modified - Required Reading

Read each UPDATE or CREATE target completely before editing it.

- `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md` (CREATE)
  - Purpose: accepted decision record for B3/B4/B11 crypto/key-management placement.
  - Must include: owner split, approved 7.7 migration scope, additive API prerequisites, rollback set, no-leak evidence, and explicit "no production migration in 7.6" statement.

- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` (CREATE)
  - Purpose: executable compatibility harness for 7.6.
  - Must cover: readable, tampered/unreadable, missing/destroyed key, provider-unavailable, erased/redacted, restricted, legacy unprotected, and no-leak cases.
  - Preserve: xUnit v3, Shouldly, NSubstitute, no static source-text assertions.

- `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs` (UPDATE only if needed)
  - Current state: implements EventStore `IEventPayloadProtectionService`, protects party-domain `[PersonalData]` fields using AES-GCM JSON field envelopes (`json+pdenc-v1`), redacts encrypted markers to `json-redacted` after destroyed keys, short-circuits redacted payloads, and records decryption failures in `DecryptionCircuitBreaker`.
  - What this story may change: only non-migrating compatibility behavior needed to classify `PayloadUnprotectionOutcome` or support the harness. Prefer test harness coverage first.
  - Preserve: domain `party` handling only, AES-256-GCM behavior, nonce/tag/ciphertext safety, key zeroing, redaction tolerance for corrupted markers, no PII logs, and existing serialization formats.

- `src/Hexalith.Parties.Security/PartyKeyManagementService.cs` (UPDATE only if needed)
  - Current state: creates AES-256 keys, reads latest/specific versions, rotates keys, deletes all party key versions, emits audit entries, and returns erasure certificates.
  - Preserve: `PartyEncryptionKeyDestroyedException` normalization for missing keys, audit rollback on rotation failure, meter names, and no raw key/key-path leakage in public outputs.

- `src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs` (UPDATE only if needed)
  - Current state: per-tenant/party circuit breaker with Closed/Open/HalfOpen states, threshold/break duration/max-open handling, and meter/log output.
  - Preserve: per-party isolation, bounded status, no payload/key logging. Note that current telemetry tags include tenant and party id; if ADR classifies this as too sensitive for future shared security, route the change to 7.7 or an additive owner story.

- `src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs` and `src/Hexalith.Parties.Security/ErasureVerificationService.cs` (UPDATE only if needed)
  - Current state: key destruction retry/verification and bounded erasure verification report aggregation.
  - Preserve: certificates, report statuses, retry behavior, and PII-free report output.

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (READ; UPDATE only for test-only seams if unavoidable)
  - Current state: registers GDPR/crypto services and production `IEventPayloadProtectionService` as `PartyPayloadProtectionService`.
  - Preserve: production registration and runtime behavior in 7.6. The migration to an approved provider belongs to 7.7.

- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` and `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs` (READ if harness exercises erased/replay paths)
  - Current state: use `PartyPayloadProtectionService.RedactProtectedPayload(...)` when destroyed-key conditions are encountered.
  - Preserve: post-erasure redaction fallback, command/query gateway routing, and no event payload logging.

- EventStore contracts under `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/` (READ; UPDATE only if ADR identifies additive API requirement)
  - Current state: `IEventPayloadProtectionService`, `PayloadProtectionResult`, `EventStorePayloadProtectionMetadata`, `PayloadUnprotectionOutcome`, unreadable reason codes, crypto-shredding workflow records, and readability decision records already exist.
  - Preserve: provider-neutral metadata, no raw keys/plaintext/provider blobs, additive-only contract evolution.

### EventStore API Facts

- `IEventPayloadProtectionService` has legacy protect/unprotect methods, metadata-aware unprotect overloads, typed snapshot methods, and default typed `TryUnprotectEventPayloadAsync` / `TryUnprotectSnapshotAsync` methods. The default typed event unprotect maps non-cancellation exceptions to `ProviderUnavailable`; custom providers should override it for precise unreadable categories. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`]
- `EventStorePayloadProtectionMetadata` is provider-neutral and explicitly forbids raw keys, plaintext, IVs/nonces, auth tags, and provider-private blobs. `KeyAlias` exists but callers must treat it sensitive-by-default. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata.cs`]
- `PayloadUnprotectionOutcome` carries readable bytes only when readable; unreadable outcomes carry a safe reason and metadata only. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/PayloadUnprotectionOutcome.cs`]
- `NoOpEventPayloadProtectionService` returns unprotected metadata for normal no-op paths and maps protected metadata to `MissingKey` in typed try-unprotect paths. Use this as compatibility context, not as the Parties provider. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs`]
- `ProtectedDataLeakSentinel` provides fixed sentinel values and directory/file scanners for proving protected payloads, key aliases, provider-private blobs, state-store keys, connection strings, and provider exception text do not leak. [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs`]

### Previous Story Intelligence

- Story 7.5 is done and did not modify crypto/key-management. It retained adapter-first migration discipline, rollback-only local paths where needed, behavioral tests instead of source-text tests, and recorded unrelated submodule drift as a build blocker rather than resetting pointers. [Source: `_bmad-output/implementation-artifacts/7-5-projection-checkpoint-rebuild-migration-and-local-code-removal.md#Completion-Notes-List`]
- Recent commits are `feat(story-7.5): complete projection checkpoint rebuild migration`, `feat: complete story 7.4 projection platform adapter`, `feat: complete story 7.3 search normalization`, `feat(story-7.2): Commons ServiceDefaults, correlation, ProblemDetails, and paging`, and `feat(story-7.1): Platform target-destination ADR and release/rollback plan`. [Source: `git log -5`]
- Carry forward Story 7.5's hygiene: do not stage unrelated submodule pointer moves; do not add static source-text evidence tests; record exact blockers for full solution build if the known submodule drift remains.

### Project Structure Notes

- ADRs and planning decisions live under `_bmad-output/planning-artifacts/`.
- Parties-specific crypto implementation lives in `src/Hexalith.Parties.Security/`.
- Public party security abstractions live in `src/Hexalith.Parties.Contracts/Security/`; keep this project infrastructure-free.
- EventStore provider-neutral contracts live under `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/`.
- Existing focused tests live in `tests/Hexalith.Parties.Security.Tests/`; use this test project for the 7.6 harness unless integration-only behavior requires `tests/Hexalith.Parties.IntegrationTests/Security/`.
- Generated `bin/` and `obj/` trees are not source and must not be edited or cited as implementation targets.

### Testing and Validation Guidance

Run the smallest reliable lane first, then broaden:

- `git diff --check`
- `bash scripts/check-no-warning-override.sh`
- `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`
- `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`
- Direct xUnit v3 execution for:
  - `Hexalith.Parties.Security.Tests.CryptoKeyManagementCompatibilityHarnessTests`
  - `Hexalith.Parties.Security.Tests.PartyPayloadProtectionServiceTests`
  - `Hexalith.Parties.Security.Tests.PartyPayloadProtectionRedactTests`
  - `Hexalith.Parties.Security.Tests.DecryptionCircuitBreakerTests`
  - `Hexalith.Parties.Security.Tests.PartyEncryptionKeyDestroyedExceptionTests`
  - `Hexalith.Parties.Security.Tests.KeyManagementIntegrationTests`
  - `Hexalith.Parties.Security.Tests.ErasureVerificationServiceTests`
- Integration evidence when available:
  - `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs`
- Full solution build only if known unrelated submodule drift is resolved; otherwise record the exact blocker.
- If EventStore source is touched, run the focused EventStore contract/security package build/tests for the changed area. Do not initialize nested submodules.

### Latest Technical Information

- No external package or framework upgrade is required for Story 7.6. Use the pinned local stack from project context: .NET SDK `10.0.301`, `net10.0`, EventStore project references under `references/`, xUnit v3, Shouldly, and NSubstitute.
- The relevant "latest" source of truth is the checked-out EventStore contracts in `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/`, not public web docs. Do not change package versions for this story.

### Rollback Plan

- Primary rollback: revert the ADR and harness changes. No production data migration is allowed in 7.6.
- If production source was changed for compatibility classification, it must be revertable independently and documented as non-migrating.
- If EventStore contract source was changed additively, roll back the root submodule pointer or the additive commit before Story 7.7 consumes it.
- Public Parties contracts, protected payload readability/redaction, erasure certificates, processing records, export behavior, EventStore gateway routes, DAPR ACLs, and UI behavior must remain compatible.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.6-Crypto/key-management-ADR-and-compatibility-harness`]
- [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.6---CryptoKey-Management-ADR-And-Compatibility-Harness`]
- [Source: `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md#Target-Destination-Matrix`]
- [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Story-76`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-3---Crypto-Placement-Gate`]
- [Source: `_bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas`]
- [Source: `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`]
- [Source: `src/Hexalith.Parties.Security/PartyKeyManagementService.cs`]
- [Source: `src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs`]
- [Source: `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/PayloadUnprotectionOutcome.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs`]

## Validation Summary

- Source discovery loaded project context facts, sprint status, canonical epics, PRD, whole architecture, Epic 7 architecture spine, Story 7.1 ADR/release plan, Story 7.5 previous-story intelligence, current Parties security implementation, EventStore security contracts/testing sentinels, focused security test inventory, and recent git history.
- Checklist fixes applied before finalizing: made the ADR output concrete, blocked production migration in 7.6, required behavioral compatibility harness coverage, required no-leak sentinel scanning, identified current UPDATE files and preserve rules, and named validation lanes and rollback conditions.
- Latest-technology review found no external dependency upgrade requirement. This story relies on pinned local .NET 10, current root submodule sources, and accepted Epic 7 ADR/release plan rather than changing package versions.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-29T17:39:20+02:00 - Story and sprint status moved to in-progress; existing `baseline_commit: c5ffed8` preserved.
- 2026-06-29T17:47:41+02:00 - Focused validations passed: `git diff --check`; `bash scripts/check-no-warning-override.sh`; `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`; `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false`; direct xUnit executable for `CryptoKeyManagementCompatibilityHarnessTests`, `PartyPayloadProtectionServiceTests`, `PartyPayloadProtectionRedactTests`, `DecryptionCircuitBreakerTests`, `PartyEncryptionKeyDestroyedExceptionTests`, `KeyManagementIntegrationTests`, and `ErasureVerificationServiceTests` (84 tests, 0 failed, 0 skipped).
- 2026-06-29T17:47:41+02:00 - Full solution build attempted with `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false`; blocked by pre-existing submodule reference drift: `Hexalith.EventStore.Admin.Server` cannot resolve `Hexalith.Tenants.*`, and `Hexalith.Tenants.Contracts` cannot resolve `Hexalith.EventStore.*` contract types. No nested submodules were initialized.
- 2026-06-29T17:55:20+02:00 - QA E2E generation workflow gap pass added explicit missing-key harness coverage and generated `_bmad-output/implementation-artifacts/tests/test-summary.md`; focused validations passed with 85 tests, 0 failed, 0 skipped. Full solution build remains blocked by the same pre-existing EventStore/Tenants submodule reference drift.
- 2026-06-29 - Adversarial review pass. Ran the FULL `Hexalith.Parties.Security.Tests` assembly (not just the dev's 85-test subset): 154 total, **1 failed** before fixes. Two findings fixed: (1) AC4 protected-data leak in `PartyKeyRetryActor.ReceiveReminderAsync` (raw `ex.Message` persisted to the Dapr state-store `CryptoPendingRecord.LastError` and logged together with the exception object); (2) regression in `PartyKeyLifecycleServiceTests.OnPartyCreatedAsync_MarksCryptoPending_WhenKeyCreationFails` caused by the dev's own `OnPartyCreatedAsync` no-leak edit (asserted the old leaked `"Backend unavailable"` reason). After fixes: `dotnet build` of `Hexalith.Parties.Security` and `Hexalith.Parties.Security.Tests` clean (0 warnings, 0 errors); full security assembly re-run **154 total, 0 failed, 0 skipped**; `git diff --check` clean; `scripts/check-no-warning-override.sh` clean.

### Completion Notes List

- Created the accepted Story 7.6 ADR documenting EventStore/shared-security ownership, Parties-local policy boundaries, Story 7.7 prerequisites, and rollback requirements.
- Added an executable compatibility harness over real Parties crypto/key services covering readable round trip, legacy unprotected payloads, restricted-but-readable policy state, missing/destroyed-key bounded outcomes, destroyed-key redaction, tampered payloads, provider-unavailable bounded outcomes, and safe evidence artifacts.
- Added a local test-only protected-data leak sentinel because the EventStore testing sentinel is present in the checkout but not available through the existing security test project references.
- Hardened protected-data failure diagnostics to avoid echoing provider exception messages in key lifecycle, payload protection, and erasure retry paths; public command/query contracts, EventStore gateway routes, DAPR routing/ACLs, UI behavior, production provider registration, and runtime payload formats remain unchanged.
- Recorded that current Parties typed unprotection still collapses destroyed/missing/tampered/provider-unavailable failures to EventStore's default `ProviderUnavailable`; the ADR blocks Story 7.7 migration until an additive provider override or approved shared provider maps precise unreadable categories.
- Epic 7 remains post-MVP platform maintenance and adds no PRD functional coverage.
- Review fix: closed the missed AC4 leak in the durable retry path. `PartyKeyRetryActor.ReceiveReminderAsync` no longer persists the raw `ex.Message` into the state-store `CryptoPendingRecord.LastError` (now a generic `"Key infrastructure unavailable."`) and no longer logs the exception object or its message (now logs `ex.GetType().Name` only), matching the hardening already applied to `PartyKeyLifecycleService`, `PartyErasureOrchestrator`, and `PartyPayloadProtectionService`.
- Review fix: updated `PartyKeyLifecycleServiceTests` to assert the generic crypto-pending reason and to guard that the raw exception text is never forwarded, repairing a regression introduced by the story's own `OnPartyCreatedAsync` no-leak change that the original validation lane did not catch because it excluded that test class.

### File List

- `_bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md`
- `src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs`
- `src/Hexalith.Parties.Security/PartyKeyLifecycleService.cs`
- `src/Hexalith.Parties.Security/PartyKeyRetryActor.cs`
- `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`
- `tests/Hexalith.Parties.Security.Tests/CapturingLogger.cs`
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs`
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementHarnessServices.cs`
- `tests/Hexalith.Parties.Security.Tests/PartyKeyLifecycleServiceTests.cs`
- `tests/Hexalith.Parties.Security.Tests/PartyPayloadProtectionServiceTests.cs`
- `tests/Hexalith.Parties.Security.Tests/ProtectedDataLeakSentinel.cs`

### Change Log

- 2026-06-29 - Added crypto/key-management split ADR and non-migrating compatibility/no-leak harness for Story 7.6.
- 2026-06-29 - Bounded protected-data failure diagnostics so provider exception text is not echoed through captured logs or exception evidence.
- 2026-06-29 - Added explicit missing-key compatibility harness coverage and refreshed the BMAD test automation summary for Story 7.6.
- 2026-06-29 - Adversarial review: closed AC4 leak in `PartyKeyRetryActor` (state-store record + logs) and fixed the `PartyKeyLifecycleServiceTests` regression from the story's own no-leak edit; full security test assembly green (154, 0 failed).

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot — 2026-06-29
**Outcome:** Approved (auto-fix applied)

Scope reviewed: ADR, compatibility/no-leak harness, and the non-migrating diagnostics hardening in `src/Hexalith.Parties.Security/`. ACs 1, 2, 3, 5, and 6 validated as implemented; AC4 (no-leak) had a verified gap that has now been fixed.

Findings:

1. **[HIGH — AC4, fixed]** `PartyKeyRetryActor.ReceiveReminderAsync` was excluded from the dev's no-leak hardening. On a key-creation retry failure it (a) persisted the raw `ex.Message` into the Dapr-persisted `CryptoPendingRecord.LastError` and (b) logged the exception object together with `ex.Message`, so provider/exception text could reach both the state store and logs. This is exactly the leak class AC4 forbids and the same pattern the dev corrected elsewhere. Fixed to store a generic reason and log `ex.GetType().Name` only. The file was also missing from the File List (now added).
2. **[HIGH — regression, fixed]** The dev's own `PartyKeyLifecycleService.OnPartyCreatedAsync` no-leak change broke `PartyKeyLifecycleServiceTests.OnPartyCreatedAsync_MarksCryptoPending_WhenKeyCreationFails`, which still asserted the old leaked `"Backend unavailable"` reason. The reported "85 tests, 0 failed" evidence did not surface this because the validation lane enumerated a focused subset that excluded `PartyKeyLifecycleServiceTests`. Running the full `Hexalith.Parties.Security.Tests` assembly (154 tests) exposed the failure. Test updated to assert the generic reason and to guard the raw text is never forwarded.
3. **[MEDIUM — documentation, addressed]** Validation evidence under-reported scope (focused subset vs. full assembly) and the File List omitted a changed production file. Both corrected in this review; future evidence should run the full security assembly.

Verification after fixes: `Hexalith.Parties.Security` and `Hexalith.Parties.Security.Tests` build clean (0 warnings/0 errors); full security assembly **154 total, 0 failed, 0 skipped**; `git diff --check` and `scripts/check-no-warning-override.sh` clean. The pre-existing full-`.slnx` submodule reference drift remains an unrelated, documented blocker (carried over from Story 7.5) and is out of scope for this story. No CRITICAL issues remain.
