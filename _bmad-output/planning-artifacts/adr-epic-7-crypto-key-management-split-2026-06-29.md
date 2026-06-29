# ADR: Epic 7 Crypto and Key-Management Split

Date: 2026-06-29

Status: Accepted for Story 7.6

## Context

Parties currently owns party-specific GDPR crypto-shredding behavior through `PartyPayloadProtectionService`, `PartyKeyManagementService`, `LocalDevKeyStorageBackend`, `PartyKeyLifecycleService`, erasure verification, and the party policy surface. Epic 7 is an adapter-first platform alignment effort, not a functional PRD expansion. Story 7.6 decides the ownership split and proves compatibility before Story 7.7 may migrate any runtime provider.

No production provider migration is approved in Story 7.6. The existing registration remains `IEventPayloadProtectionService -> PartyPayloadProtectionService`.

## Decision

EventStore/shared security owns provider-neutral payload-protection contracts, metadata vocabulary, typed unreadable outcomes, and shared provider hooks. Parties owns party-specific GDPR policy, tenant/party key semantics, erasure orchestration, and all user/admin-facing legal behavior until a later approved migration routes a narrow additive API to EventStore/shared security.

| Item | Owner | Decision |
| --- | --- | --- |
| Payload protection contracts | EventStore/shared security | `IEventPayloadProtectionService`, `PayloadProtectionResult`, `EventStorePayloadProtectionMetadata`, and typed unprotection outcomes stay provider-neutral and additive. |
| AES-GCM provider mechanics | Parties for current runtime, EventStore/shared security for future generic provider | Current AES-256-GCM JSON field envelopes remain local in `PartyPayloadProtectionService`. A future shared provider must prove identical readability or safe unreadable classification before adoption. |
| Payload metadata | EventStore/shared security | Metadata shape stays shared. Parties 7.7 must emit `Protected` metadata only after the provider contract/pointer is approved; Story 7.6 documents that current legacy constructors emit `Unprotected` metadata even for protected serialization format. |
| Unreadable-data reason mapping | EventStore/shared security taxonomy, Parties provider override | EventStore owns reason codes. Parties must override typed unprotection before migration so missing key, destroyed key, tamper, provider denied, and provider unavailable do not collapse into the default `ProviderUnavailable`. |
| Key storage | Parties policy and configured provider | `LocalDevKeyStorageBackend` remains dev-only. Production KMS selection remains deployment/security policy, not EventStore gateway policy. |
| Party key path/alias shape | Parties | `{tenant}/parties/{partyId}/v{version}` and tenant/party semantics remain local and sensitive-by-default. Raw aliases must not be echoed in logs or evidence. |
| Key wrapping | Parties until shared provider API exists | Tenant key metadata and party key wrapping remain local. A shared API may carry provider-neutral wrapping metadata, not raw keys or provider-private blobs. |
| Key rotation | Parties for party keys | Party key rotation remains local. EventStore may expose generic provider hooks but must not know party legal semantics. |
| Tenant key rotation | Parties policy | Tenant key rotation remains local because it is tied to Parties tenant/party wrapping semantics. |
| Crypto-shredding workflow | Parties orchestration with shared vocabulary | EventStore may own generic unreadable/readability vocabulary. Parties owns erase/cancel/verify command policy and key destruction workflow. |
| Audit | Parties for party key operations, shared security for provider-neutral hooks | Audit entries must stay bounded and PII-free. Shared hooks may carry operation category and safe failure type only. |
| Circuit breaker | Parties current runtime, shared security future primitive allowed | Current per-party decryption circuit breaker remains local. A future shared primitive must avoid payload, key alias, provider text, and actor-id labels. |
| Restored/legacy backup handling | EventStore/shared security taxonomy, Parties policy | EventStore reason codes classify restored/legacy unreadability. Parties decides GDPR response, redaction, export, and verification behavior. |
| Event-type resolution | EventStore/shared security for safe contract loading, Parties allowlist for party events | Resolution must remain allowlisted and fail closed. No `Type.GetType` on untrusted wire input for event payload routing. |
| Leak sentinel strategy | Shared test concept, local harness evidence | Reuse shared sentinel when referenced; otherwise keep local test-only sentinel. Evidence artifacts must use safe summaries and never echo sentinel values. |

## Parties Behaviors That Remain Local

The following stay in Parties unless a later ADR approves a narrow additive API:

- Party-specific commands and rejection events.
- GDPR legal policy and lawful-basis semantics.
- User/admin copy, consumer self-scope, and admin authorization.
- Tenant/party key path semantics and key lifecycle decisions.
- Erasure orchestration, cancellation, verification, certificates, and reports.
- Export and processing-record redaction policy.
- Compatibility adapters needed to keep existing protected payloads readable.

## Approved Story 7.7 Production Scope

Story 7.7 may proceed only after the Story 7.6 harness is green. It may:

- Add or consume an approved EventStore/shared-security provider package or root-submodule pointer.
- Add a Parties compatibility adapter that preserves existing `json+pdenc-v1` payload readability.
- Override typed EventStore unprotection in Parties or the adopted provider so unreadable categories are precise and bounded.
- Emit provider-neutral `Protected` metadata only when existing legacy records remain readable and metadata-less records remain safely classified.
- Keep redaction fallback behavior for destroyed keys and corrupted protected markers.

Story 7.7 must not:

- Change public command/query contracts or EventStore gateway routes.
- Move Parties legal policy, erasure orchestration, or consumer/admin authorization into EventStore.
- Treat key aliases, provider blobs, provider exception text, state-store keys, payload bytes, decrypted values, or actor ids as safe diagnostics.
- Consume unapproved local-only APIs from a checked-out submodule.

## Additive API and Pointer Prerequisites for Story 7.7

Before migration, EventStore/shared security or the approved provider must expose or pin:

1. A typed unprotection override that maps current Parties failures precisely:
   - destroyed key -> `KeyInvalidatedOrDeleted`
   - missing key that is not known destroyed -> `MissingKey`
   - tampered/corrupt bytes or bytes/metadata disagreement -> `BytesMetadataMismatch` or `ConsistencyMismatch`
   - provider outage -> `ProviderUnavailable`
   - provider policy denial -> `ProviderDenied`
2. A metadata emission contract that can mark AES-GCM protected payloads as `Protected` without leaking key aliases or provider-private material.
3. A compatibility adapter for legacy `json+pdenc-v1` field envelopes and `json-redacted` payloads.
4. A safe diagnostics contract that carries failure category and retryability only.
5. A no-leak sentinel test utility or package reference that can be consumed without changing production contracts.

## Compatibility Evidence Required by 7.6

The harness must execute behavior, not source-text assertions. Required evidence:

- Protected party PII round trips through the real Parties service and EventStore contract entry points.
- Legacy unprotected payloads pass through unchanged with EventStore-compatible unprotected metadata.
- Restricted parties remain readable; processing restriction is legal-policy state, not crypto destruction.
- Destroyed-key payloads follow safe unreadable/redaction behavior and do not restore personal fields.
- Tampered/corrupt protected payloads become bounded typed unreadable outcomes.
- Provider-unavailable outcomes do not leak provider exception text.
- Evidence artifacts and captured outputs do not echo sentinel plaintext, raw key aliases, provider-private blobs, state-store keys, connection strings, provider exception text, raw payload bytes, or ciphertext marker detail.

Current Story 7.6 evidence records that missing, destroyed, tampered, and provider-unavailable paths collapse to EventStore's default `ProviderUnavailable` typed outcome because `PartyPayloadProtectionService` has not yet overridden the typed contract. That is acceptable for 7.6 only because the migration remains blocked until 7.7 adds the precise override or consumes an approved provider that supplies it.

## Rollback for 7.7

If Story 7.7 adoption fails:

- Restore `PartyPayloadProtectionService` as the production `IEventPayloadProtectionService` registration.
- Roll back the approved provider package or root submodule pointer.
- Preserve readability of existing protected payloads or classify them as safe unreadable without exposing personal data.
- Keep `json-redacted` fallback behavior for destroyed-key replay.
- Leave public Parties contracts, EventStore `/api/v1/commands` and `/api/v1/queries`, DAPR `/process`, DAPR ACLs, GDPR semantics, consumer self-scope, and UI behavior unchanged.

## Consequences

This decision keeps Story 7.6 as a proof and decision slice. It allows additive shared-security work in Story 7.7 while preventing an accidental migration that weakens GDPR erasure guarantees or leaks protected-data diagnostics.
