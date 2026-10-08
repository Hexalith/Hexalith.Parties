---
story_key: 8-7-data-protection-extraction
story_id: "8.7"
epic: "8"
created: 2026-07-16T00:23:38+02:00
revalidated: 2026-10-08
source_status: backlog
target_status: blocked
baseline_commit_at_story_start: a35b151
baseline_commit_at_revalidation: 9e74c2b66ea0e201c6a4f9200e875627fed3b541
eventstore_root_pin_at_revalidation: 8dd7dc2ecdb2c06ecb900676042aa42b66619ee0
eventstore_checkout_at_revalidation: 8dd7dc2ecdb2c06ecb900676042aa42b66619ee0
---

# Story 8.7: Data-protection extraction

Status: blocked

<!-- This story is complete enough for workflow intake, but production source migration is hard-gated by the Story 8.3 G5 row and the authoritative Epic 8 sequence. -->

## Current Gate Revalidation — 2026-10-08 (retry at 9e74c2b6)

G5 remains `needs-additive-api`; Story 8.7 remains `blocked`. At Parties
`9e74c2b66ea0e201c6a4f9200e875627fed3b541`, matching clean root checkouts are EventStore
`8dd7dc2ecdb2c06ecb900676042aa42b66619ee0` (`v3.117.1-7-g8dd7dc2e`) and Builds
`a283481c69393dcba911db6a0edcb152167238cc` (`v4.30.1-3-ga283481`). Both package selectors
remain `3.117.1`. These observations grant no new adoption or G5 approval.

EventStore's newer owner 8.3 core/postreview receipts explicitly grant no
successor, provider, package, deployment, or G5 authority. Owner 8.3 remains
`in-progress`; 8.4-8.11 remain `backlog`. A consumable runtime provider,
compatibility/lifecycle/persistence integration, AzureKeyVault backend,
package/release enrollment, dual-provider GDPR parity, post-v2 rollback,
owner 8.11 closure, and I2/I19a classification approval remain missing.
Parties 8.6 is `done`; the original frozen adoption identity still requires
approved reconciliation before activation.

The existing static procedure passed all 32 G5 checks; all 24 retained
MOVE/KEEP/adapter files, the local harness, four local DI registrations, and
matching clean source identities were verified. Exact commands and results are
in `tests/test-summary.md` under "Story 8.7 G5 retry at 9e74c2b6". Only this
story, its spec change log, and the test summary were updated. Production,
dependencies, frozen intent/original baseline, matrix approvals, and sprint
status are preserved. Product and provider-parity suites were not run or credited.

## Historical Gate Revalidation — 2026-10-08

G5 remains `needs-additive-api`; Story 8.7 remains `blocked`. At Parties
`998c0b649d7bbf01e0fed4cc79924662dd16cc91`, EventStore's root gitlink and clean checkout match
`9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` (`v3.117.1-5-g9542d3c9`); Builds' root gitlink and clean checkout
match `6f07763bd955d22ace0123798add528dc933bf51` (`v4.30.0-13-g6f07763`). The Parties pre-import pin and Builds
catalog both select EventStore `3.117.1`. These are inspected identities;
this audit grants no G5 approval or dependency adoption. The frozen spec still
names historical `c21bd749154d701c3b7d68e40d1008d3475e35c4` / `3.95.0`;
activation requires approved identity reconciliation.

Public policy/erasure hooks and the internal, non-packable v2 core are partial
delivery. Owner 8.2 is `done`, 8.3 is `in-progress`, and 8.4-8.11 remain
`backlog`. Compatibility/key-lifecycle/persistence integration, a consumable
runtime provider, AzureKeyVault backend, package/release enrollment, dual-provider
GDPR parity, post-v2 rollback, and the owner 8.11 availability closure remain
missing. I2/I19a policy-hook classification approval is pending. Existing owner
specification/contract approvals do not grant G5 availability. Production KMS
remains a separate release prerequisite.

Parties 8.6 is `done`; accepted Epic 8 closure deferrals do not complete 8.7.
All 32 recorded G5 static checks passed. All 24 MOVE/KEEP/adapter files, the
local harness, and local DI remain intact. Only documentation was updated;
product, dual-provider/GDPR, and post-v2 suites were not run or credited.
Exact commands and results are recorded in `tests/test-summary.md` under this date.

## Historical Gate Revalidation — 2026-10-05

G5 remains `needs-additive-api`; Story 8.7 remains `blocked`. At Parties
`b3794a4dcbe2fff3e9ea5c420a3c4695e2d1a8ca`, EventStore's root gitlink and clean checkout match
`865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`). Builds' root gitlink and clean checkout
match `90f3836dd7482db35c2c187a50999b99215919b0` (`v4.29.1-21-g90f3836`); both its catalog and the
Parties pre-import pin select EventStore `3.113.0`.

EventStore's v3.113.0 package/source selection already has a dated I20 approval.
The later Builds identity is an observation; the I20 approved selection remains
`360a2b9c4e96809365a7de785be9a68152d5ac28`. Neither source presence nor identity
selection is G5 availability approval, and this audit adopts no new dependency.
The locked 8.7 spec block still names `c21bd749154d701c3b7d68e40d1008d3475e35c4`
/ `3.95.0`; activation requires reconciling it with an approved G5 identity.

Public policy/erasure contracts and an internal, non-packable v2 core exist.
Owner 8.2 is `done`, 8.3 is `in-progress`, and 8.4-8.11 remain `backlog`.
Missing receipts: compatibility/key-lifecycle integration, AzureKeyVault backend,
package/release enrollment, a consumable runtime provider, dual-provider GDPR
parity, post-v2 rollback, and the 8.11 G5 availability approval/closure packet.
The I2/I19a policy-hook classification approval is also pending; Parties must
remain the policy writer. Existing owner contract/spec approvals do not close G5.

Parties 8.6 is `done`; accepted Epic 8 closure deferrals do not complete 8.7.
All 24 MOVE/KEEP/adapter files, the local harness, and local DI remain intact.
The 32 recorded G5 static checks passed. No production, DI, dependency, submodule,
or frozen-spec changes were made. Product tests were not run or credited.
Commands and results are recorded in `tests/test-summary.md` under this date.

## Story

As a security reviewer and maintainer,
I want generic data-protection mechanics owned by EventStore/shared security,
so that Parties keeps GDPR policy without owning reusable crypto infrastructure.

## Acceptance Criteria

1. Given the Story 8.3 `Payload protection engine package` row remains `needs-additive-api`, when this story is attempted, then production source migration halts as `blocked` until the row records the named security/owner approval, exact shared-engine release or root `references/Hexalith.EventStore` pin, stable format/state-key/actor/metric contract, green local-versus-provider parity evidence, and exercised rollback. A checked-out submodule source tree or owner-routing document alone is not approval.
2. Given the approved Epic 8 sequence remains `8.6 -> 8.7` and Story 8.6 is `done`, when owner-side G5 work or Story 8.7 preparation proceeds, then no Parties production migration or deletion starts until the Story 8.3 G5 row is consumable (`available` with named approval, exact identity, producer/consumer parity, and rollback). Completing 8.6 does not waive G5.
3. Given an approved shared payload-protection provider exists, when it is adopted, then it supplies versioned `pdenc-v2` writes with deterministic authenticated-additional-data binding, `json+pdenc-v1` reads, policy and erasure-state extension seams, precise typed-unreadable outcomes, and generic key storage/wrapping/rotation, audit, retry, circuit-breaker, and production-backend behavior without reusing ASP.NET Core cursor Data Protection as the payload engine.
4. Given protected, redacted, legacy, snapshot, or malformed persisted data, when either the retained local path or shared provider reads it, then `json+pdenc-v1`, `json-redacted`, legacy unprotected, missing metadata, metadata/bytes mismatch, large protected fields, and snapshots preserve the existing readable or bounded typed-unreadable semantics. Unknown versions fail closed; legacy data is not rewritten in place and never triggers a spurious decrypt attempt.
5. Given deleted, missing, denied, unavailable, tampered, or inconsistent key/payload states, when unprotection fails, then only erasure-record/certificate-backed `KeyInvalidatedOrDeleted` becomes `json-redacted` or a PII-free tombstone. Missing key, access denial, provider outage, integrity failure, and consistency failure remain distinct bounded outcomes; no provider exception-text parsing or provider detail reaches logs, exceptions, metrics, traces, or ProblemDetails.
6. Given new `pdenc-v2` payloads have been written, when the shared provider is rolled back, then the retained rollback path can read both v1 and v2 without data loss. Provider-default switching and v2 writes therefore occur only after either the retained local provider gains v2 read compatibility or an equally reversible dual-read/write-mode plan is approved, frozen in golden vectors, and exercised after real v2 writes.
7. Given the ownership split, when extraction completes, then generic crypto/key mechanics live only behind the approved shared provider while Parties retains party-specific commands, tenant/party rotation policy, GDPR/legal policy, natural-person classification, erasure orchestration, D7 verification, certificates/reports, and UX/copy. `IErasureVerificationService` and public `Hexalith.Parties.Contracts` remain compatible unless an explicit ADR and versioning plan approve a change.
8. Given the seven approved deletion proofs, when both providers are exercised, then payload/format compatibility, typed-unreadable plus retry/circuit behavior, no-leak diagnostics, Art.20 export, Art.30 processing records, erasure certificate/report plus I7 legal semantics, and post-v2 rollback are recorded in both the Story 8.3 G5 row and `_bmad-output/implementation-artifacts/tests/test-summary.md` before the corresponding local implementation is deleted.
9. Given generic key-management behavior moves, when key creation, wrapping, deletion, rotation, retry, cache invalidation, and audit execute, then stable key paths, Dapr state keys, actor/reminder names, metric names, tenant isolation, optimistic-concurrency behavior, transient key-buffer zeroing, and destroyed-key evidence remain compatible. The shared engine adds no new Parties-owned persistence path.
10. Given the security configuration boundary, when the application starts and processes protected payloads, then `Parties:CryptoShredding:IsEnabled` remains default-true and distinct from `Parties:Compliance:GdprFeaturesActive` default-false. `LocalDevKeyStorageBackend` remains development-only until removed after parity; a production KMS/backend is an independently enforced release prerequisite before regulated EU personal data is processed.
11. Given parity and rollback are green, when cleanup occurs, then only the approved MOVE-listed generic implementation is deleted; KEEP-listed domain files and the `EventStorePartyPayloadProtectionAdapter` seam remain in Parties with dependency-only changes as required. Generic-looking public contract types, correlation accessors, and middleware are inventoried and either retained behind compatibility adapters or moved through an approved additive/versioned plan rather than silently deleted.
12. Given Epic 8 is Class C post-MVP maintenance, when this story completes, then no public API, Dapr ingress, projection/query, client/MCP/AppHost/deploy, UI behavior, package version, or PRD functional coverage is widened. Documentation identifies the work as platform cleanup and records all commands, results, environment-limited skips, release/pin provenance, and rollback evidence.

## Tasks / Subtasks

- [ ] Satisfy both start gates before editing production source (AC: 1, 2, 12)
  - [x] Inspect the Story 8.3 G5 row. At story creation it remains `needs-additive-api`; no approved G5 provider or proof is recorded.
  - [x] Record provenance after concurrent `/pushall` activity: the root gitlink and checkout both equal `82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7` (`v3.67.0-1-g82ed167c`). The synchronized checkout still contains no G5 engine, `pdenc-v2`, `IPersonalDataPolicy`, or `IErasureStateProvider`, and no owner-approved G5 packet authorizes it.
  - [x] Confirm Story 8.6 is `blocked` and record that the authoritative sequence conflicts with the draft Story 8.7 spec's “independent of 8.6” note.
  - [x] Mark Story 8.7 blocked and halt before production code, package, or submodule edits.
  - [x] 2026-09-07 spec execution: revalidated G5 at live gitlink/checkout `d45206f7cbd80a112519c1d4687d7279a745f0c5` (`v3.103.0-2-gd45206f7`) / package `3.103.0`. Row remains `needs-additive-api`; halted with no production, DI, or dependency changes. Spec frozen identity `c21bd749`/`3.95.0` still does not match live checkout.
  - [x] 2026-10-03 gate audit: G5 remains `needs-additive-api` at EventStore `2c58ffda41759e895ace4b9625c9bd931a217672` (`v3.111.0`) / catalog `3.110.0`. Public policy/erasure contracts and internal non-packable v2 core now exist; runtime/backend/package, parity, rollback, and 8.11 closure are still incomplete. Retained all 24 MOVE/KEEP/adapter files and local DI; no production or dependency changes.
  - [x] 2026-10-05 gate audit: EventStore v3.113.0 and current Builds catalog still lack a consumable G5 provider, release enrollment, dual-provider parity, post-v2 rollback, 8.11 closure, and I2/I19a classification approval. All 32 recorded static checks and 24 retained-file checks passed; no production/dependency changes or product-test credit.
  - [x] 2026-10-08 gate audit: EventStore package 3.117.1 and the matching clean root checkouts still lack G5 runtime/release, dual-provider parity, post-v2 rollback, owner 8.11 closure, and I2/I19a classification approval. All 32 static G5 checks and retained-file/DI checks passed; production and dependency changes remain gated.
  - [ ] Before implementation resumes, update the existing G5 row with the named owner/reviewer, exact approved release or root gitlink, producer and consumer proof, frozen compatibility identities, and rollback instructions; change its status only when the review gate is genuinely satisfied.
  - [x] Resolve the `8.6 -> 8.7` sequence by completing 8.6 (done 2026-08-17). Parallel owner delivery still does not waive G5.

- [ ] Accept the owner-delivered G5 surface without inventing a local replacement (AC: 1, 3, 7, 9, 12)
  - [ ] Consume the approved additive shared payload-engine package/API only after security-owner approval and root provenance are recorded; do not guess a package, project, or API name before it lands.
  - [ ] Verify the provider exposes Parties-supplied `IPersonalDataPolicy` and `IErasureStateProvider`-equivalent seams so `PersonalDataGraphInspector`, natural-person rules, legal erasure evidence, and missing-versus-deleted classification remain domain-owned.
  - [ ] Freeze `pdenc-v2` envelope/version and canonical byte-stable AAD encoding in owner golden vectors. Bind at least version/envelope plus tenant, domain, aggregate/party identity, event or snapshot type, property path, and key version, or document the security-owner-approved equivalent.
  - [ ] Preserve and freeze `json+pdenc-v1`, `json-redacted`, legacy-unprotected, metadata scheme/flags, encrypted marker fields, nonce/tag sizes, key paths, state keys, actor/reminder names, and metric names needed for compatibility.
  - [ ] Require a pluggable production backend and startup/release guard; do not represent LocalDev/in-memory coverage as production KMS proof.

- [ ] Build a true local-versus-provider golden parity harness before switching defaults (AC: 3, 4, 5, 8, 9)
  - [ ] Parameterize or replace `CryptoKeyManagementCompatibilityHarnessTests` so identical fixed vectors run against the retained local rollback provider and shared provider, rather than testing only the local service through the EventStore adapter.
  - [ ] Cover fixed v1 event and snapshot reads, provider-produced v2 writes/reads, redacted and legacy reads, missing/legacy/protected metadata, bytes/metadata mismatch, unknown/malformed formats, the existing 1.5 MB field case, and no spurious legacy decrypt.
  - [ ] Add v2 negative vectors that alter or transplant each AAD-bound context component and assert authentication failure maps to the existing bounded tamper/integrity outcome without plaintext or provider text.
  - [ ] Cover destroyed versus missing keys, access denied, provider unavailable, malformed marker, circuit open/half-open/closed, retry scheduling and exhaustion, concurrent tenant rotation, audit, cache invalidation/zeroing, tenant isolation, and restart/end-state behavior.
  - [ ] Move reusable golden-vector ownership to the approved provider while retaining consuming compatibility and Parties policy/mapping tests locally.

- [ ] Prove real GDPR behavior through both provider paths (AC: 5, 7, 8, 10)
  - [ ] Exercise actual `ExportPartyData` to `PartyDataPortabilityPackage` behavior for live, erased, unavailable, and missing parties; representative DTO construction is not sufficient proof.
  - [ ] Exercise actual `GetProcessingRecords` to `ProcessingActivityRecord[]` behavior and preserve existing empty/no-op semantics.
  - [ ] Exercise party erasure orchestration, PII-free tombstones, erasure status, `ErasureVerificationReport`, and `ErasureCertificate`, including D7 cross-submodule verification and key-destruction evidence.
  - [ ] Preserve two-front-door erasure, Art.18 restrictions, consent-not-equal-lawful-basis, self-scope/tenant guards, and validation-before-unprotect behavior.
  - [ ] Run `ProtectedDataLeakSentinel` and explicit log/trace/exception/ProblemDetails assertions across protect, unprotect, retry, rotate, audit, export, and erase paths. Reject payload, key material/alias, party or tenant name, and actor identifiers.

- [ ] Stage the provider adoption while retaining a functional rollback path (AC: 3, 5, 6, 7, 11)
  - [ ] Update `EventStorePartyPayloadProtectionAdapter` to bind the shared provider while preserving current EventStore metadata and typed-outcome mapping; retain erasure-record/certificate evidence for deleted-versus-missing classification.
  - [ ] Add thin Parties policy, erasure-state, public-contract, and dependency adapters where required. Update `PartyPersonalDataCommandGuard`, `PersonalDataGraphInspector`, `PartyErasureOrchestrator`, `PartyDomainProcessor`, and `PartyProjectionUpdateOrchestrator` only at their provider dependencies; retain their domain semantics.
  - [ ] Update `PartiesServiceCollectionExtensions` and project/package references so local and shared providers can be selected during parity. Keep current local registrations and `PartyKeyRetryActor` until the relevant proof is green.
  - [ ] Stage rollout as: owner API/approval/pin; provider plus adapters with local default retained; dual-path parity; shared default; v2 write/read; rollback to a v2-capable retained path; then deletion. Do not combine first activation and deletion.
  - [ ] Do not rewrite historical events or perform key-material migration without a separately approved, tested rollback script and security-owner approval.

- [ ] Delete only the proven generic implementation and clean dependencies (AC: 7, 8, 9, 11)
  - [ ] After all governing proofs pass, delete the 18 MOVE files listed in Dev Notes. If a file's proof is absent, leave that file and the required registration intact.
  - [ ] Keep the five domain files and adapter seam listed in Dev Notes. Logical KEEP files may receive dependency-only changes but must not lose their domain policy.
  - [ ] Inventory `Hexalith.Parties.Contracts/Security`, especially key storage/management/rotation/audit interfaces and DTOs. Preserve public package compatibility through thin adapters or an intentional ADR/versioning plan.
  - [ ] Retain `CorrelationContextAccessor`, `ICorrelationContextAccessor`, `CorrelationIdMiddleware`, and their non-crypto usage unless a separately approved ownership change proves they are redundant.
  - [ ] Remove Dapr Actors and other package references only when no retained source uses them. Retain Dapr Client while kept erasure storage requires it; do not introduce direct Dapr/ORM persistence for new shared mechanics in Parties.
  - [ ] Verify `Hexalith.Parties.Security` contains domain GDPR policy/orchestration and provider adapters, not a second generic crypto/key engine.

- [ ] Validate, document, and close the guarded evidence (AC: 4-12)
  - [ ] Run the focused and full direct xUnit v3 test-assembly lanes in Testing and Validation Guidance; do not use `dotnet test --filter` as proof.
  - [ ] Run source-mode/package-mode builds in the repository-supported configuration, sequentially with `-m:1`, Central Package Management, warnings-as-errors, and the pinned dependency versions.
  - [ ] Run unit, topology, no-warning-override, no-leak, and environment-backed provider/KMS or sidecar lanes. Record Docker/network/environment skips as unproven release gates, not passes.
  - [ ] Exercise post-v2 rollback and restore-forward, then record exact commands/results, owner approval, release/root pin, and each of the seven deletion proofs in both the G5 matrix row and `tests/test-summary.md`.
  - [ ] Run `git diff --check`; update sprint status and the Epic 7 crypto-retention action only when their stated exit conditions are actually met.

## Dev Notes

### Historical Start Blockers — 2026-07-16

- Epic 8 is Class C post-MVP maintenance with zero new PRD functional coverage. This story preserves completed behavior and corrects the domain/platform ownership boundary. [Source: `_bmad-output/planning-artifacts/epics.md#Story-8.7-Data-protection-extraction`]
- The Story 8.7 draft spec is explicitly `blocked-prerequisite`. The existing Story 8.3 G5 `Payload protection engine package` row remains `needs-additive-api`; the 2026-07-11 owner-routing proposal says routing is not delivery or approval. [Source: `_bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md`; `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#EventStore-Owner-Routing--2026-07-11`]
- After concurrent `/pushall` synchronization, the root-approved EventStore gitlink and checkout both equal `82ed167c...` (`v3.67.0-1-g82ed167c`). This removes checkout drift but does not open G5: the synchronized tree still exposes provider-neutral contracts and typed outcomes but no shared payload engine, `IPersonalDataPolicy`, `IErasureStateProvider`, or `pdenc-v2`, and no owner-approved G5 closure packet exists. ASP.NET Core Data Protection in EventStore backs opaque query cursors and is not G5.
- `epics.md`, Epic 8 context, the architecture spine, and the approved 2026-07-11 proposal retain `8.6 -> 8.7`; the draft spec calls 8.7 independent of 8.6. Treat story preparation and owner work as parallel-capable, but treat Parties source consumption as sequence-gated until an approved artifact resolves the conflict.
- Production KMS remains a release prerequisite before regulated EU personal data is processed, but the approved deletion guardrail says it is not the G5 story-start blocker. The actual start blockers are owner-approved G5 parity/provenance and the consuming-story sequence.

### Architecture and Domain Guardrails

- I3/I4: keep rollback paths until parity and exercised rollback; do not consume unapproved submodule APIs. I5: preserve public package compatibility. I7: preserve party GDPR/legal semantics, two-front-door erasure, D7 verification, Art.18, and consent/lawful-basis separation. I8: preserve protected payload compatibility, exports, processing records, certificates/reports, key zeroing, and no-leak behavior. [Source: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md#Invariants--Rules`]
- The approved crypto-retention proposal makes seven proofs mandatory before deletion: payload compatibility, typed unreadable/retry/circuit parity, no-leak diagnostics, Art.20 exports, Art.30 processing records, certificate/report plus I7 semantics, and rollback. Each proof must appear in the G5 row and test summary and pass against both paths. [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-crypto-key-management-retention.md#4.2-Exit-criteria--what-Story-8.7-must-prove-before-any-deletion`]
- EventStore remains the platform persistence boundary. Keep current domain-owned `PartyErasureRecordStore` behavior in scope as a KEEP concern; do not use this story to redesign it. New shared mechanics must not add a new Parties direct-Dapr or ORM state path. [Source: `references/Hexalith.AI.Tools/hexalith-state-instructions.md`]
- Public command/query ingress and Dapr ACLs are unaffected. Projection/query is Story 8.6; client/MCP/AppHost/build/deploy is 8.8; UI is 8.9. No UI behavior changes are required, but regulated no-leak and honest erasure/export behavior remain parity evidence.

### Current Implementation Facts to Preserve

- `PartyPayloadProtectionService` currently writes `json+pdenc-v1` field envelopes using AES-256-GCM with a fresh 12-byte nonce and 16-byte tag, and zeroes transient key/plaintext buffers. It passes identity/event type/property path through recursion but does not currently authenticate them as AAD; v2 is therefore an intentional new format, not a silent implementation swap.
- `EventStorePartyPayloadProtectionAdapter` is the active `IEventPayloadProtectionService` seam. It publishes bounded scheme/format metadata, supports legacy metadata, validates metadata/bytes consistency, and maps destroyed, missing, denied, tampered, unavailable, and circuit outcomes without exposing raw provider text.
- Only erasure record/certificate evidence distinguishes a destroyed key from an unexplained missing key. Preserve that rule through provider erasure-state policy; missing key alone must never become a GDPR-success claim.
- `PartiesServiceCollectionExtensions` currently registers LocalDev storage, audit, key management/lifecycle/cache/rotation, retry/circuit services, `PartyPayloadProtectionService`, the EventStore adapter, and `PartyKeyRetryActor`. A dual registration/selection stage is required before default switching.
- `CryptoKeyManagementCompatibilityHarnessTests` already covers local v1 roundtrip, legacy, redacted/deleted/missing/tampered/unavailable/denied/mismatch/snapshot/large-field and no-leak cases through the adapter. It is not yet a two-provider harness; its export/processing evidence is not a substitute for real end-to-end query behavior.
- Persisted/protocol identities include the key path form `{tenant}/parties/{party}/v{version}`, `parties.keys.*` metrics, retry actor `PartyKeyRetryActor`, state `crypto-pending`, and reminder `retry-key-creation`. The owner provider must inventory and freeze all audit/rotation keys, actor names, and metrics before migration.

### File Ownership and Change Boundaries

MOVE behind the approved provider and delete locally only after the corresponding proof and post-v2 rollback pass:

- `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`
- `src/Hexalith.Parties.Security/PartyKeyManagementService.cs`
- `src/Hexalith.Parties.Security/CachedPartyKeyManagementService.cs`
- `src/Hexalith.Parties.Security/PartyKeyLifecycleService.cs`
- `src/Hexalith.Parties.Security/IPartyKeyRetryScheduler.cs`
- `src/Hexalith.Parties.Security/ActorBackedPartyKeyRetryScheduler.cs`
- `src/Hexalith.Parties.Security/PartyKeyRetryActor.cs`
- `src/Hexalith.Parties.Security/IPartyKeyRetryActor.cs`
- `src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs`
- `src/Hexalith.Parties.Security/DecryptionCircuitOpenException.cs`
- `src/Hexalith.Parties.Security/KeyOperationAuditService.cs`
- `src/Hexalith.Parties.Security/TenantKeyRotationService.cs`
- `src/Hexalith.Parties.Security/TenantKeyRotationProgress.cs`
- `src/Hexalith.Parties.Security/TenantKeyRotationProgressConflictException.cs`
- `src/Hexalith.Parties.Security/ITenantKeyRotationCacheInvalidator.cs`
- `src/Hexalith.Parties.Security/LocalDevKeyStorageBackend.cs`
- `src/Hexalith.Parties.Security/PartyEncryptionKeyDestroyedException.cs`
- `src/Hexalith.Parties.Security/CryptoPendingRecord.cs`

KEEP in Parties; do not move without an ADR. Dependency-only edits are allowed to bind the shared provider while preserving domain semantics:

- `src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs`
- `src/Hexalith.Parties.Security/ErasureVerificationService.cs`
- `src/Hexalith.Parties.Security/PartyErasureRecordStore.cs`
- `src/Hexalith.Parties.Security/PartyPersonalDataCommandGuard.cs`
- `src/Hexalith.Parties.Security/PersonalDataGraphInspector.cs`

KEEP as the provider seam:

- `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`

Likely UPDATE/rebind surfaces after the gates open:

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`
- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs`
- `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs`
- `src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj`
- Parties/project package references only where the approved owner artifact requires them
- local adapter/policy tests and `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs`

Before cleanup, separately classify the adjacent public key/audit/rotation types in `src/Hexalith.Parties.Contracts/Security`. The domain services currently consume several generic-looking interfaces, and I5 forbids silently breaking their published package surface. Also keep the correlation accessor and middleware unless a separate ownership decision proves a replacement.

### Rollout and Rollback Design

The safe order is mandatory:

1. Owner G5 API, security approval, exact release/root pin, frozen formats/identities, and producer tests land.
2. Parties adds provider, policy/erasure adapters, and true dual-provider parity while the local path remains the default and fully registered.
3. Parties switches to the shared provider only after payload, legal-domain, no-leak, and state/metric parity is green.
4. The provider writes v2; tests read it through the provider, switch back to the retained rollback path, and prove both v1 and v2 remain readable without rewriting history or migrating keys.
5. Restore forward, rerun full validation, record all seven proofs, and only then delete the individually proven MOVE files.

The existing local provider cannot read v2 today. A DI-only rollback test performed before v2 writes is therefore insufficient and must not authorize deletion.

### Technical and Security Guidance

- Repository pins win: use SDK `10.0.302`, `net10.0`, C# 14, Dapr `1.18.4`, xUnit v3/Microsoft Testing Platform, Shouldly, NSubstitute, Central Package Management, `.slnx`, and warnings-as-errors. Do not bundle .NET, Dapr, test-library, or EventStore upgrades into Story 8.7.
- As of 2026-07-16, the repository pin aligns with SDK `10.0.302`; validate the accompanying .NET `10.0.10` runtime and package baseline through the dependency owners rather than bypassing repository pins in this refactor. [Official .NET 10.0.10 release](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.10/10.0.10.md)
- .NET `AesGcm` requires nonce uniqueness under a key; the supported nonce size is 12 bytes, and the explicit-tag-size constructor should be used. AAD passed to encryption must be reconstructed identically for decryption. Preserve 16-byte tags and map authentication mismatch to bounded typed outcomes. [Microsoft `AesGcm.Encrypt`](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.encrypt?view=net-10.0), [nonce sizes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.noncebytesizes?view=net-10.0), [`AesGcm.Decrypt`](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.decrypt?view=net-10.0)
- Continue `CryptographicOperations.ZeroMemory` for transient plaintext and key buffers. [Microsoft API](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0)
- ASP.NET Core Data Protection purpose/application-name and key-ring semantics are for its own isolation boundary; custom repositories also require separate at-rest protection assessment. They do not provide selective per-party crypto-shredding. [Microsoft purpose strings](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/purpose-strings?view=aspnetcore-10.0), [configuration overview](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
- xUnit v3 test projects are standalone executables under Microsoft Testing Platform. Keep this repository's direct assembly execution because prior solution/filter lanes could silently execute zero tests. [xUnit v3/MTP guidance](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)

### Previous Story and Git Intelligence

- Story 8.6 followed the same deletion-safety rule: it recorded the exact observed EventStore checkout, halted at `needs-additive-api`, edited no production source, and retained every rollback path. Apply that discipline here; do not infer approval from later producer commits or the visible checkout.
- Recent root history is primarily gitlink/provenance reconciliation and owner-routing planning. It reinforces exact root-pin evidence before consuming submodule APIs; it does not deliver G5.
- EventStore projection producer work demonstrates a useful owner-side pattern for G5: additive contracts, canonical/fingerprint goldens, structured outcomes instead of ambiguous exceptions, adversarial follow-up tests, and a real persisted-backend lane. G5 must still have its own named approval and payload-specific proofs.
- Preserve all concurrent user-owned commits and unrelated worktree changes. Only root-declared submodules may be initialized or updated, and nested submodules must not be initialized.

### Testing and Validation Guidance

Build sequentially and run direct xUnit v3 assemblies. Adapt paths or add provider-specific classes only after the approved package lands:

```bash
git ls-tree HEAD references/Hexalith.EventStore
git -C references/Hexalith.EventStore rev-parse HEAD
rg -n -F "Payload protection engine package" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md

dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal
dotnet ./tests/Hexalith.Parties.Security.Tests/bin/Debug/net10.0/Hexalith.Parties.Security.Tests.dll -class Hexalith.Parties.Security.Tests.CryptoKeyManagementCompatibilityHarnessTests
dotnet ./tests/Hexalith.Parties.Security.Tests/bin/Debug/net10.0/Hexalith.Parties.Security.Tests.dll

dotnet build tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal
dotnet ./tests/Hexalith.Parties.IntegrationTests/bin/Debug/net10.0/Hexalith.Parties.IntegrationTests.dll -class Hexalith.Parties.IntegrationTests.Security.EncryptionPipelineIntegrationTests

dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal
dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartyDetailProjectionQueryActorTests -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests

pwsh scripts/test.ps1 -Lane unit
pwsh scripts/test.ps1 -Lane topology
bash scripts/check-no-warning-override.sh
git diff --check
```

Add owner-side unit/golden tests and a security-owner-approved real production-backend/KMS or sidecar lane. If credentials, Docker, sidecars, or network are unavailable, record the lane as environment-limited and keep the release/deletion gate open.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-8.7-Data-protection-extraction`]
- [Source: `_bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md`]
- [Source: `_bmad-output/implementation-artifacts/epic-8-context.md`]
- [Source: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#Payload-protection-engine-package`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-crypto-key-management-retention.md`]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11-eventstore-additive-api-prerequisite-routing.md#Package-B--Shared-payload-protection-engine-for-Story-8.7`]
- [Source: `_bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md`]
- [Source: `_bmad-output/implementation-artifacts/7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md`]
- [Source: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `references/Hexalith.AI.Tools/hexalith-state-instructions.md`]
- [Source: `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`]
- [Source: `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`]
- [Source: `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`]
- [Source: `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs`]
- [Source: `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs`]

## Validation Summary

- Loaded the complete Epic 8 story requirements, PRD, whole and sharded architecture, UX conformance artifacts, draft Story 8.7 spec, Epic 8 context, Story 8.3 matrix, approved correction proposals, Epic 7 ADR/evidence, previous Story 8.6, persistent project contexts, repository instructions, current source, recent Git history, and official current technical guidance.
- Checklist corrections applied: retained the hard G5 gate; added the unresolved authoritative sequence gate; distinguished payload protection from cursor Data Protection; required exact root provenance; converted the existing harness into a true dual-provider plan; added real Art.20/Art.30 flow proof; preserved missing-versus-deleted semantics and public contracts; added AAD transplant tests; inventoried adjacent contract/correlation surfaces; and fixed the v2 rollback contradiction by requiring a post-write v2 rollback read before deletion.
- The story introduces no dependency upgrade or product/UX scope. Current official versions were reviewed only to confirm that repository pins remain authoritative and that the .NET security-patch delta must be routed separately.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-10-08 - Revalidated G5 at Parties `998c0b649d7bbf01e0fed4cc79924662dd16cc91`, EventStore `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` / package `3.117.1`, and Builds `6f07763bd955d22ace0123798add528dc933bf51`. Recorded partial owner delivery and missing runtime/release/parity/rollback/approval receipts. Preserved the frozen spec, local engine, public APIs, and all rollback files; no production or dependency changes.
- 2026-10-05 - Revalidated the closed G5 gate at Parties `b3794a4dcbe2fff3e9ea5c420a3c4695e2d1a8ca`, matching EventStore `865cd9e49273dffbb1cdae85efeaf1aac322e09e` / package `3.113.0`, and observed Builds `90f3836dd7482db35c2c187a50999b99215919b0`. Preserved the frozen spec, local engine, public APIs, and all rollback files. Recorded remaining provider/release/parity/rollback/approval receipts; no production or dependency edits.
- 2026-10-03 - Revalidated G5 at Parties `06714c166c090200ac87373b11d8243aa11b2126` and matching EventStore `2c58ffda41759e895ace4b9625c9bd931a217672` (`v3.111.0`). Owner 8.2 is done, 8.3 in-progress, 8.4-8.11 backlog. Recorded partial contract/core delivery and remaining backend/package/parity/rollback/closure blockers; halted before production, dependency, or submodule changes.
- 2026-07-16T00:23:38+02:00 - Loaded sprint status and selected requested story `8-7-data-protection-extraction` from `_bmad-output/implementation-artifacts/sprint-status.yaml` (`backlog`).
- 2026-07-16T00:23:38+02:00 - Inspected the Story 8.3 G5 row; it remains `needs-additive-api` and owner routing explicitly remains non-delivery.
- 2026-07-16T00:23:38+02:00 - Initial provenance inspection found an older root gitlink and newer user-owned checkout; neither contained the required G5 engine.
- 2026-07-16T00:32:11+02:00 - Revalidated after concurrent `/pushall`: root gitlink and checkout both equal `82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7`; the synchronized tree still lacks G5 and has no owner-approved G5 packet.
- 2026-07-16T00:23:38+02:00 - Confirmed Story 8.6 is blocked and the approved Epic 8 sequence remains authoritative despite the draft 8.7 spec's contradictory design note.
- 2026-07-16T00:23:38+02:00 - Halted before production, dependency, or submodule edits; all local crypto/key rollback files and registrations remain intact.
- 2026-09-06T16:34:00+02:00 - Course correction: Story 8.6 is `done` (2026-08-17). Story 8.7 stays `blocked` on G5; completing 8.6 does not waive that gate.
- 2026-09-07T23:02:00+02:00 - Spec `spec-8-7-data-protection-extraction.md` execution: G5 still `needs-additive-api` at EventStore `d45206f7cbd80a112519c1d4687d7279a745f0c5` / package `3.103.0`. Named missing receipts recorded; halted before production, dependency, or submodule edits.

### Completion Notes List

- 2026-10-08 closed-gate audit: 32/32 static checks passed; 24 retained files, local harness, local DI, and clean matching source identities verified. G5 stays `needs-additive-api`; 8.7 stays `blocked`; crypto-retention stays `open`. Product and provider-parity suites remain unrun and uncredited.
- 2026-10-05 static gate audit passed (32 G5 checks plus retained-file/DI checks). G5 remains `needs-additive-api`, Story 8.7 remains `blocked`, and the crypto-retention action remains `open`. I2/I19a classification and EventStore 8.11 closure are still prerequisites. Product/dual-provider/GDPR/post-v2 suites were not run or credited.
- 2026-10-03 gate audit supersedes the older source-absence statements: contracts and internal v2 source now exist, but no approved consumable G5 provider or closure does. Source migration stays blocked. Static inventory passed; dual-provider/GDPR/post-v2 suites were not run or credited.
- Source migration is blocked because no owner-approved G5 shared payload-protection engine, release/root pin, dual-path parity evidence, or exercised rollback is recorded. Story 8.6 is `done`; G5 remains the live halt.
- Historical story-creation absence claims are superseded by the current gate receipt: public policy/erasure contracts and internal v2 core now exist, but no approved consumable shared provider does. ASP.NET Core cursor Data Protection is not a substitute.
- No production source, project/package dependency, or submodule pointer was changed and no product tests were run during story creation. The local implementation remains the required rollback path.
- 2026-09-07 spec execution confirmed the same halt at EventStore `d45206f7` / package `3.103.0`. Dual-provider, GDPR, and post-v2 rollback suites were not run and are not credited.

### File List

**Added**

- `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md`

**Modified**

- `_bmad-output/implementation-artifacts/epic-8-context.md`
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md`
