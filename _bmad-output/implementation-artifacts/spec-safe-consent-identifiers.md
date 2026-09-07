---
title: 'Safe consent identifier compatibility and error hardening'
type: 'bugfix'
created: '2026-09-07'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: '8b5db017e6edded51684b0b4c1089c410c8138c1'
baseline_commit: '8b5db017e6edded51684b0b4c1089c410c8138c1'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md'
warnings:
  - oversized
deferred:
  - summary: >-
      RecordConsentValidator does not guard LawfulBasis with IsInEnum and RevokeConsentValidator
      leaves Reason unbounded, unlike every sibling command validator.
    evidence: |-
      AddContactChannelValidator, AddIdentifierValidator and CreatePartyValidator all call
      IsInEnum on their enum property, and RestrictProcessingValidator bounds Reason to 256
      characters. RecordConsent carries LawfulBasis straight into ConsentRecorded, so an
      out-of-range cast is persisted. Deferred rather than patched because the intent contract's
      Never clause forbids changing lawful-basis behaviour; adding the rule would reject commands
      that are accepted today.
    location: >-
      src/Hexalith.Parties/Validation/RecordConsentValidator.cs and RevokeConsentValidator.cs
    severity: medium
  - summary: >-
      The deterministic lowercased ConsentId can collide when two contact channels differ only by
      letter case, while the channel lookup itself is ordinal case-sensitive.
    evidence: |-
      PartyAggregate.Handle(RecordConsent) builds $"{channelId}:{purpose}".ToLowerInvariant() while
      state.ContactChannels.Any(c => c.Id == command.ChannelId) compares ordinally, so channels
      "Ch-Email-1" and "ch-email-1" collapse onto one consent id. Both lines are unchanged by this
      story and the intent's Always clause requires preserving that generation, so this is
      pre-existing behaviour, not a regression.
    location: >-
      src/Hexalith.Parties/Domain/PartyAggregate.cs:1362-1364
    severity: medium
  - summary: >-
      The consumer portal maps the client's new ArgumentException to a generic Failed outcome while
      the admin portal maps it to ValidationRejected.
    evidence: |-
      PartiesAdminPortalApiClient.ExecuteGdprCommandAsync catches ArgumentException and returns
      AdminPortalGdprOutcome.ValidationRejected, but SelfScopedPartiesClient calls
      HttpAdminPortalGdprClient directly and ConsumerConsentClient has a bare catch returning
      ConsumerConsentOperationOutcome.Failed. Unverified because MyConsentPage supplies purposes
      from a fixed catalogue and channel/consent ids from stored projections, all of which satisfy
      ConsentIdentifier today. What would settle it, if-true severity medium: evidence that a
      stored channel or consent id in a real tenant fails ConsentIdentifier, or a consumer path
      that feeds free-form channel/purpose input.
    location: >-
      src/Hexalith.Parties.UI/Services/ConsumerConsentClient.cs:43-47,67-70
    severity: medium (unverified)
  - summary: >-
      No test asserts that the production DI extension registers the consent validators, and the
      domain processor fails open when a validator is missing.
    evidence: |-
      PartyDomainProcessorValidationTests.CreateInvoker builds its own
      AddValidatorsFromAssemblyContaining scan rather than calling
      PartiesServiceCollectionExtensions.AddHexalithParties, and
      PartyDomainProcessor.TryRejectInvalidPayloadAsync logs and returns null when no validator
      resolves. A regression in the production registration surface would leave every command
      unvalidated with the suite green. Pre-existing infrastructure design, not introduced here.
    location: >-
      src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:376
    severity: medium
  - summary: >-
      ContactChannelNotFound and IdentifierNotFound still interpolate caller-supplied identifiers
      into their messages outside the two consent handlers.
    evidence: |-
      PartyAggregate.cs lines 412, 415, 423, 426, 1174, 1229 and 1305 still emit
      $"Contact channel '{id}' not found." / $"Identifier '{id}' not found.", so the same event type
      now has two message dialects depending on which command produced it. Those call sites belong
      to commands outside this story's intent contract and were not touched by it.
    location: >-
      src/Hexalith.Parties/Domain/PartyAggregate.cs:412,415,423,426,1174,1229,1305
    severity: medium
---

<intent-contract>

## Intent

**Problem:** `RecordConsent` and `RevokeConsent` have no command validators, their aggregate not-found messages interpolate caller-controlled identifiers, and the general colon-free semantic-ID rule cannot validate the deterministic `channel:purpose` identifiers already stored by Parties.

**Approach:** Add a dependency-free consent identifier contract with distinct channel-segment and consent-ID rules, register focused FluentValidation validators for both commands, and reuse the contract at aggregate and client boundaries. Keep not-found details fixed and bounded while preserving exact stored identifiers and lookup semantics.

## Boundaries & Constraints

**Always:** Accept existing support-safe opaque consent IDs and single-separator `channel:purpose` IDs; delegate channel-segment compatibility to `PartyIdentifier`; retain legacy GUID channel forms; keep purpose compatibility at 1–100 ASCII alphanumeric, hyphen, or underscore characters; preserve `RecordConsent` trimming/lowercased deterministic ID generation and `RevokeConsent` exact ordinal lookup/event payload; retain required rejection-event fields; use fixed validation and not-found messages that never contain submitted IDs.

**Never:** Do not migrate, normalize, rewrite, or delete stored consent records; do not apply `PartyIdentifier.IsValid` directly to composite consent IDs; do not change command/event JSON shape, restriction/erasure ordering, or lawful-basis behavior; do not edit `_bmad-output/implementation-artifacts/deferred-work.md` or any `.bmad-loop` ledger source; do not modify submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Record compatible channel | `ChannelId=ch-email-1`, `Purpose=marketing`, matching channel exists | Emit the unchanged `ConsentId=ch-email-1:marketing` and channel value | No error expected |
| Revoke compatible stored ID | Existing opaque `consent-1` or composite `ch-email-1:marketing` | Match ordinally and emit the exact supplied stored ID | No normalization or migration |
| Unsafe identifier | Blank, path/control/whitespace content, malformed/multi-separator composite, or component over its bound | Validator rejects before rehydration; direct aggregate call rejects before lookup | One fixed support-safe detail; raw input is absent |
| Valid identifier is missing | Bounded channel segment or consent ID is syntactically valid but absent | Preserve `ContactChannelNotFound` or `ConsentNotFound` contract | Fixed bounded message; no identifier interpolation |

</intent-contract>

## Code Map

- `src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs:8-50` -- reuse the established 128-character semantic-ID and legacy GUID compatibility rule; do not broaden it to allow colons.
- `src/Hexalith.Parties.Contracts/ValueObjects/ConsentIdentifier.cs` -- new public dependency-free helper: channel segments delegate to `PartyIdentifier`, while consent IDs accept either one support-safe opaque ID or one bounded `channel:purpose` composite without normalization.
- `src/Hexalith.Parties/Validation/RecordConsentValidator.cs` and `RevokeConsentValidator.cs` -- new one-type-per-file Fluent validators; assembly scanning in `PartiesServiceCollectionExtensions.AddHexalithParties` registers them automatically.
- `src/Hexalith.Parties/Domain/PartyAggregate.cs:1311-1427` -- add dedicated identifier backstops before lookup, keep deterministic write/exact revoke semantics, and replace channel/consent interpolation in not-found messages.
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs:103-127,187-223` -- reuse the same rules for Add/Revoke no-send validation before serializing gateway commands.
- `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt` -- record the intentional additive contract surface.
- `tests/Hexalith.Parties.Contracts.Tests/ValueObjects/ConsentIdentifierTests.cs` -- pin opaque/composite compatibility, legacy GUID channels, exact bounds, separators, and unsafe inputs.
- `tests/Hexalith.Parties.Tests/Validation/RecordConsentValidatorTests.cs`, `RevokeConsentValidatorTests.cs`, `IdentifierValidatorTests.cs`, and `Domain/PartyDomainProcessorValidationTests.cs` -- cover both command validators and prove malformed IDs become `PartyCommandValidationRejected` before rehydration; add both commands to the PartyId validator inventory.
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateConsentTests.cs` -- cover direct-handler backstops, fixed not-found details, no raw echo, and unchanged stored-ID behavior.
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs` -- prove valid legacy IDs still submit and malformed channel/consent IDs do not send.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Parties.Contracts/ValueObjects/ConsentIdentifier.cs`, `tests/Hexalith.Parties.Contracts.Tests/ValueObjects/ConsentIdentifierTests.cs`, and `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt` -- define and verify bounded channel, opaque consent, and legacy composite rules without infrastructure dependencies.
- [x] `src/Hexalith.Parties/Validation/RecordConsentValidator.cs`, `src/Hexalith.Parties/Validation/RevokeConsentValidator.cs`, `tests/Hexalith.Parties.Tests/Validation/RecordConsentValidatorTests.cs`, `tests/Hexalith.Parties.Tests/Validation/RevokeConsentValidatorTests.cs`, `tests/Hexalith.Parties.Tests/Validation/IdentifierValidatorTests.cs`, and `tests/Hexalith.Parties.Tests/Domain/PartyDomainProcessorValidationTests.cs` -- enforce and prove required PartyId/TenantId, command-specific identifiers, current purpose constraints, and pre-rehydration rejection at the EventStore domain-service boundary.
- [x] `src/Hexalith.Parties/Domain/PartyAggregate.cs` and `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateConsentTests.cs` -- enforce the same identifiers for direct calls and make valid-missing/invalid messages bounded without changing persisted IDs.
- [x] `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs` and `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs` -- fail locally without HTTP for unsafe Add/Revoke identifiers while preserving compatible payloads.

**Acceptance Criteria:**
- Given the EventStore domain processor receives either consent command with an unsafe identifier, when validation runs, then it emits only a bounded `PartyCommandValidationRejected` and does not rehydrate or invoke the aggregate.
- Given existing opaque or `channel:purpose` consent records, when `RevokeConsent` is validated and handled, then the exact matching identifier is accepted and emitted unchanged.
- Given direct aggregate invocation with an unsafe channel or consent identifier, when the relevant handler runs, then it rejects before lookup with one generic message containing none of the submitted value.
- Given a syntactically valid but absent channel or consent identifier, when lookup fails, then the established not-found event type and required fields remain while its message is fixed and contains no submitted identifier.
- Given either Admin GDPR consent method receives an unsafe identifier, when called, then it throws a bounded argument error and sends no HTTP request.

## Spec Change Log

- 2026-09-07: Implemented consent identifier compatibility, command validators, aggregate/client safety guards, fixed not-found details, and focused regression coverage.

## Review Triage Log

### 2026-09-07 — Review pass

- verdicts: 18 findings — high 2, medium 7, low 2, false 6, maybe-false 1
- findings:
  - [high] [patch] RecordConsent channel validation changes party-not-found and erasure precedence — the baseline checks state and erasure first, while the new guard currently runs before both despite the intent contract preserving erasure ordering.
  - [high] [patch] RevokeConsent identifier validation changes party-not-found and erasure precedence — the baseline checks state and erasure first, while the new guard currently masks both outcomes.
  - [maybe-false] [reject] The compatibility rule might exclude undocumented historical arbitrary consent strings — repository writers and fixtures demonstrate support-safe opaque and single-separator composite forms, and the intent explicitly defines that compatibility strategy rather than preserving unrestricted unsafe strings.
  - [medium] [patch] Aggregate purpose checks duplicate the new shared contract — the literal length and regular expression can drift from `ConsentIdentifier.IsValidPurpose` used by validators and the client.
  - [medium] [patch] InvalidConsentPurpose can retain an unbounded submitted purpose — the optional `Purpose` field is emitted unprotected even though the bundle requires bounded, non-echoing rejection details at direct aggregate boundaries.
  - [low] [patch] Blank-purpose rejection replaces the required tenant value with an empty string — preserving `command.TenantId` is a trivial correction while the rejection construction is being hardened.
  - [medium] [patch] New public consent-length constants are absent from the API snapshot — they are implementation details with no consumer requirement, so the smallest fix is to keep them private and test the documented literal boundary independently.
  - [medium] [patch] One Admin Portal no-echo assertion is vacuous — the `channel:purpose` row does not contain the hard-coded `unsafe-sensitive` marker, so the test must assert against the actual submitted channel and purpose.
  - [medium] [patch] Over-limit behavior is not independently exercised across boundary types — add literal boundary evidence plus representative over-limit processor, aggregate, and no-send client cases without duplicating the full input matrix at every layer.
  - [low] [reject] Observed fallback evidence omits the exact direct-run command — the authoritative verification section already lists exact project commands, and final verification will record the actual fallback and result without changing product behavior.
  - [medium] [patch] The exact 100-character purpose test is self-referential — literal 100/101 cases are needed at the helper and representative processor/client boundaries so changing the production constant cannot move the test oracle.
  - [medium] [patch] The public API snapshot does not cover the new maximum-ID constant — avoid adding unnecessary public constants and retain only the intentional public validation methods.
  - [false] [reject] Command records and wire schemas remain string-based — the intent explicitly forbids command/event JSON shape changes and locates enforcement in validators and shared boundary helpers.
  - [false] [reject] Aggregate and Admin Portal guards exceed the narrowest command-boundary reading — both boundaries are explicitly named in the intent contract's approach, code map, tasks, and acceptance criteria.
  - [false] [reject] ConsentNotFound retains the submitted ID in its required structured field — the requested hardening targets messages/error details, while the intent explicitly requires retaining required rejection fields and exact identifier semantics.
  - [false] [reject] Compatibility is not proven through a persistent store integration — applying `ConsentRecorded` to `PartyState` exercises the same replayed state consumed by the handler, and no storage migration or integration proof is required by the intent.
  - [false] [reject] No production corpus of historical identifiers is tested — the available writers, fixtures, and ledger define the supported opaque/composite compatibility set; unrestricted historical strings are neither evidenced nor required.
  - [false] [reject] Purpose validation broadens the named identifier work — the intent contract explicitly fixes the purpose segment grammar and requires current purpose constraints at validator and client boundaries.

### 2026-09-07 — Review pass (follow-up)

- verdicts: 29 findings — high 0, medium 7, low 11, false 5, maybe-false 6
- findings:
  - `[low]` `[reject]` Aggregate still selects the over-length purpose message with a literal `100` that duplicates the private `MaximumPurposeLength` — real but only mis-selects a message string if the constant ever changes; the only fix is to publish the constant, which the previous pass deliberately rejected as unnecessary public surface.
  - `[low]` `[reject]` The RecordConsent channel guard bypasses `ValidateStandaloneSemanticId`, so a blank channel reports "Channel ID is invalid." rather than "… is required." — verified real, but a blank channel never reaches the direct handler past the validator, and the substitution would drop the `ConsentIdentifier` reuse the intent contract's approach requires at the aggregate boundary.
  - `[low]` `[patch]` `ValidateAggregateId(partyId)` in `AddConsentAsync`/`RevokeConsentAsync` duplicates the identical check inside `PostCommandAsync` and throws with `ParamName` "aggregateId" — removed both calls; `PostCommandAsync` still rejects an unsafe party id before any HTTP request and `AddConsentAsync_WhenPartyIdIsUnsafe_DoesNotSendRequestAsync` still passes.
  - `[maybe-false]` `[defer]` Consumer portal degrades the new client-side rejection to a generic `Failed` outcome — the bare catch is real, but every consumer-portal input path observed supplies catalogue purposes and stored ids that satisfy `ConsentIdentifier`; deferred with what would settle it.
  - `[medium]` `[defer]` `RecordConsentValidator` omits an `IsInEnum` guard for `LawfulBasis` that every sibling validator has — verified against `AddContactChannelValidator:28` and `CreatePartyValidator:22`; the intent contract's Never clause forbids changing lawful-basis behaviour, so it is deferred rather than patched.
  - `[medium]` `[patch]` The new `ValidateStandaloneSemanticId(command.PartyId, …)` backstops in both consent handlers ship with no test — confirmed reachable through a direct `Handle` call with non-null state; added `Handle_RecordConsent_UnsafePartyIdRejectsBeforeChannelLookupWithoutEcho` and `Handle_RevokeConsent_UnsafePartyIdRejectsBeforeConsentLookupWithoutEcho`.
  - `[low]` `[patch]` `IsValidChannelId` had positive coverage only, so a loosened implementation would not fail any assertion — added `IsValidChannelId_UnsafeIdentifier_ReturnsFalse` pinning blank, path, whitespace, composite and leading-separator inputs against `PartyIdentifier.IsValid`.
  - `[low]` `[patch]` The new `RevokeConsent` row was inserted before `RetryErasureVerification`, breaking the inventory's alphabetical ordering — moved it after `RetryErasureVerification`. The absence of a reflection-based completeness guard is a separate, pre-existing design choice and was not changed.
  - `[low]` `[reject]` Channel ids are not trim-tolerant, leaving `command.ChannelId.Trim()` provably dead after the new guard — verified dead, but the intent contract's Always clause requires preserving the existing trimming/lowercasing deterministic ID generation, so the line stays.
  - `[low]` `[reject]` `docs/data-models.md` was not updated for the new `CompositeOperationConflict` rejection — the command table already lists a single representative rejection per command and omits `PartyNotFound`/`ContactChannelNotFound` for `RecordConsent` today, so it does not claim to enumerate outcomes; the new grammar is documented on the public helper's XML comments.
  - `[low]` `[reject]` `InvalidConsentPurpose.Purpose` is now permanently null and undocumented — that null is exactly what the previous pass patched in to stop the unbounded echo; removing the field would change event JSON shape, which the intent forbids, and an `[Obsolete]` marker adds public surface.
  - `[false]` `[reject]` The `"*"` party-wide channel now yields `CompositeOperationConflict` instead of `ContactChannelNotFound` — `"*"` fails `PartyIdentifier.IsValid`, so it is the matrix's unsafe-identifier row ("reject before lookup"), not the valid-but-missing row that preserves the not-found event type; the outcome was a rejection before and after.
  - `[medium]` `[defer]` The lowercased deterministic `ConsentId` can collide across case-differing channels while the channel lookup is ordinal — both lines are unchanged by this story and the intent requires preserving them.
  - `[maybe-false]` `[reject]` carried — Stored consent ids that fail the new grammar would become unrevokable at the aggregate; the previous pass verified that repository writers and fixtures only produce support-safe opaque and single-separator composite forms and that the intent defines that compatibility strategy.
  - `[maybe-false]` `[reject]` carried — The client-side `ValidateConsentId` would block revoking such a stored id; same refutation and same evidence base as the row above, at the client boundary.
  - `[false]` `[reject]` An unbounded `TenantId` is echoed into `InvalidConsentPurpose`/`ConsentNotFound` — `TenantId` is a required structured field of both events and the intent explicitly requires retaining required rejection-event fields; the no-echo rule targets messages and details, and preserving `command.TenantId` was itself a patch from the previous pass.
  - `[low]` `[reject]` The aggregate literal `100` can drift from `ConsentIdentifier.MaximumPurposeLength` — same defect as the first row, same refutation of the fix.
  - `[medium]` `[defer]` `LawfulBasis` accepts an out-of-range cast — same finding as the `IsInEnum` row above, routed with it.
  - `[maybe-false]` `[defer]` `ConsumerConsentClient`'s bare catch maps the new `ArgumentException` to `Failed` — same finding as the consumer-portal row above, routed with it.
  - `[low]` `[reject]` `ContactChannelNotFound` no longer carries the missing channel id anywhere, since the event has no `ChannelId` field — verified, but the intent explicitly requires not-found messages that never contain submitted ids, and the proposed structured-logging substitute adds new logging surface rather than correcting a defect.
  - `[medium]` `[patch]` Verification gap: the `PartyId` backstops in both consent handlers are unpinned, so deleting them breaks no test while re-opening the unbounded-identifier leak into `InvalidConsentPurpose`/`ConsentNotFound` — same entry as the patch above; the two new aggregate facts close it.
  - `[maybe-false]` `[defer]` Consumer path classifies the client `ArgumentException` as `Failed` while the admin path classifies it as `ValidationRejected` — same finding as the consumer-portal row, routed with it; the reviewer likewise could not ground its reachability.
  - `[low]` `[patch]` `ValidateAggregateId` throws with `ParamName` "aggregateId" for a parameter the public signature calls `partyId` — resolved by the same deletion as the duplication row.
  - `[false]` `[reject]` Intent divergence (a): the rejection *type* for unsafe channel/consent ids changed rather than only the message — same refutation as the `"*"` row; the matrix separates the unsafe row from the valid-but-missing row.
  - `[maybe-false]` `[defer]` Intent divergence (b): the client guard's outcome surface is untested and differs per portal — same finding as the consumer-portal row, routed with it.
  - `[medium]` `[defer]` Intent divergence (c): pre-rehydration rejection is proven against a mirrored DI container, and the processor fails open on a missing validator — verified in `PartyDomainProcessorValidationTests.CreateInvoker` and `PartyDomainProcessor.TryRejectInvalidPayloadAsync`; pre-existing test-infrastructure design, deferred.
  - `[false]` `[reject]` Intent divergence (d): `string.Equals(…, StringComparison.Ordinal)` replaced `==`, which is already ordinal for `string` — true, and therefore no bad outcome; the explicit form documents the intent's exact-match requirement. The untested `PartyId` guard half of this divergence is the patch recorded above.
  - `[medium]` `[defer]` Intent divergence (e): `ContactChannelNotFound`/`IdentifierNotFound` still interpolate ids at the non-consent call sites, leaving one event type with two message dialects — verified at seven call sites belonging to commands outside this story's intent contract.
  - `[false]` `[reject]` Boundary note: the working tree carries an uncommitted `deferred-work.md` edit — that file is outside the reviewed change, is owned by the orchestrator, and this run neither read it as a claim nor modified it.

## Design Notes

The consent-ID validator is compatibility-oriented: `PartyIdentifier.IsValid(value)` accepts opaque legacy IDs; otherwise exactly one `:` separates a valid 128-character channel segment from the existing 1–100-character purpose grammar. The 229-character composite bound follows the current writer and prevents unbounded rejection payloads. Validation never trims or changes Revoke input.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: contract helper and additive API snapshot pass.
- `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: consent validator/processor coverage passes; record unrelated baseline failures separately.
- `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: aggregate consent suite passes.
- `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: Admin GDPR consent client coverage passes.
- `git diff --check && git status --short` -- expected: no whitespace errors and only intended Parties/spec changes; deferred-work ledger unchanged.

**Observed 2026-09-07:**
- Contracts project command passed 171/171 tests.
- Parties project command executed 583 tests: 581 passed and two unrelated fitness tests failed because the repository expects EventStore gitlink `3c6a5e33f9fbaf8469047ba3de72f70ab4425e66` while the unchanged index contains `034c177e3d37a38ea972573d3e0f72c20026d8b3`.
- Focused fallback command `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Validation.RecordConsentValidatorTests -class Hexalith.Parties.Tests.Validation.RevokeConsentValidatorTests -class Hexalith.Parties.Tests.Validation.IdentifierValidatorTests -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests` passed 54/54 tests.
- Server project command passed 247/247 tests.
- Client project command passed 149/149 tests.
- `git diff --check` and `git diff --cached --check` passed; the deferred-work ledger, `.bmad-loop` sources, and submodule pointers are unchanged.

## Auto Run Result

### Summary

Follow-up review pass over the shipped consent-identifier hardening. No intent gap or spec defect surfaced: the compatibility contract, the two command validators, the aggregate backstops and the client no-send guards all behave as the intent contract describes. Four small entries were patched — two new aggregate facts pinning the `PartyId` backstops, a negative theory pinning `IsValidChannelId`, removal of a duplicated aggregate-id check in the GDPR client, and an ordering repair in the validator inventory. Five entries were deferred as pre-existing or intent-excluded.

### Files

- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs` — dropped the redundant `ValidateAggregateId(partyId)` calls; `PostCommandAsync` already rejects an unsafe party id before any HTTP request.
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateConsentTests.cs` — added two facts proving both consent handlers reject an unsafe `PartyId` before channel/consent lookup with a fixed non-echoing message.
- `tests/Hexalith.Parties.Contracts.Tests/ValueObjects/ConsentIdentifierTests.cs` — added a negative theory tying `IsValidChannelId` rejections to `PartyIdentifier.IsValid`.
- `tests/Hexalith.Parties.Tests/Validation/IdentifierValidatorTests.cs` — restored alphabetical ordering of the `PartyId` validator inventory.
- Product behaviour is unchanged by this pass; no `src` file changed except the client deletion above.

### Review

- Four independent layers reported 29 findings: high 0, medium 7, low 11, false 5, maybe-false 6.
- Patched (4 entries — 1 medium, 3 low): the untested `PartyId` backstops; the `IsValidChannelId` coverage gap; the duplicated aggregate-id check and its misleading `ParamName`; the validator-inventory ordering.
- Deferred (5 entries): the missing `LawfulBasis` enum guard and unbounded `RevokeConsent.Reason`; the case-collapsing deterministic `ConsentId`; the consumer-portal `Failed` vs `ValidationRejected` mapping (unverified); the unasserted production validator registration against a fail-open processor; and the non-consent `ContactChannelNotFound`/`IdentifierNotFound` sites that still interpolate ids.
- Rejected: the aggregate's literal `100` message branch, twice — the only fix publishes a constant the previous pass deliberately kept private.
- Rejected: the channel guard's bypass of `ValidateStandaloneSemanticId` — the substitution would drop the `ConsentIdentifier` reuse the intent requires at the aggregate boundary.
- Rejected: the now-dead `command.ChannelId.Trim()` — the intent's Always clause requires preserving the existing deterministic ID generation verbatim.
- Rejected: the `docs/data-models.md` table — it lists one representative rejection per command and already omits others for `RecordConsent`.
- Rejected: re-populating `InvalidConsentPurpose.Purpose` or marking it obsolete — the null is the previous pass's patch, and both alternatives change event shape or public surface.
- Rejected: the `"*"` party-wide rejection-type change and intent divergence (a) — `"*"` is an unsafe identifier, which the matrix routes to reject-before-lookup rather than to the preserved not-found types.
- Rejected: the unbounded `TenantId` echo — a required structured field the intent requires retaining.
- Rejected: the loss of the missing channel id from `ContactChannelNotFound` — mandated by the intent's no-echo rule.
- Rejected (carried from the first pass): stored consent ids outside the supported opaque/composite forms; no repository writer or fixture produces them.
- Rejected: intent divergence (d), `string.Equals(…, Ordinal)` versus `==` — a documented no-op, not a defect.

### Follow-up Recommendation

`followup_review_recommended` is `false`. This was a follow-up pass and it patched no `high` entry — one medium (a test gap) and three low entries, none of which changed product behaviour beyond removing a duplicated client-side check. Patched counts by verdict: high 0, medium 1, low 3. The work has converged; the remaining open questions are recorded as deferred items rather than unverified risk in this change.

### Verification

- Contracts: 178/178 passed (up from 171 — the new negative theory).
- Server: 249/249 passed (up from 247 — the two new `PartyId` facts).
- Client: 149/149 passed, including the pre-existing `AddConsentAsync_WhenPartyIdIsUnsafe_DoesNotSendRequestAsync` which still rejects without sending after the duplicated check was removed.
- Parties: 583 executed, 581 passed. The same two failures as the baseline — `PlatformApiPrerequisitesTests.FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` and `Matrix_ValidationEvidenceCommandsAreReproducible` — caused by the unchanged EventStore gitlink receipt mismatch, unrelated to this bundle.
- `git diff --check` reported only a trailing blank line in this specification, resolved by this section; the deferred-work ledger, `.bmad-loop` sources and submodule pointers are unchanged.
- Every reviewed-diff file is committed. The working copy retains one uncommitted path, `_bmad-output/implementation-artifacts/deferred-work.md`, which carries a pre-existing orchestrator-owned edit that is outside the reviewed diff; this run neither modified nor committed it.

### Residual Risks

- The five deferred items remain open, of which the consumer-portal outcome mapping is unverified and depends on whether any stored channel or consent id fails `ConsentIdentifier` in a real tenant.
- The repository-level EventStore receipt mismatch continues to fail two unrelated fitness tests and must be resolved by the owning dependency workflow.
