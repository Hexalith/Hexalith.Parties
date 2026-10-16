---
title: 'DW-122: enforce production command validation'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Production validator registration is untested because processor tests repeat the assembly scan independently. The processor also accepts commands when their validator is missing, allowing a registration regression to bypass consent validation (DW-122).

**Approach:** Exercise the real `AddParties` registration and reject missing validators before protected-state access using the existing bounded `PartyCommandValidationRejected` event. Preserve the existing validator-free behavior of exactly `MarkPartyEncryptionKeyDeleted`, `MarkErasureVerified`, `CompletePartyErasure`, and `RotatePartyKey`; any validator registered for these commands must still run. Verify production registration for the command inventory, valid and invalid consent processing, missing-validator rejection, and compatibility of the four legacy exceptions. Preserve command/event wire shapes, existing validation rules, consent lifecycle semantics, and dependencies. Close only DW-122 after verification and independent review.

</frozen-after-approval>

## Implementation Notes

- Planning facts: no unresolved intent gaps or irreversible actions. Small footprint: the processor missing-validator branch/logger, existing processor tests, focused production-registration tests, API documentation, this spec, DW-122 and portable test evidence. No new public API, migration, deployment, dependency change, or remote publication.
- Investigation: production method is `PartiesServiceCollectionExtensions.AddParties` (the ledger's older method name is stale). It already calls `AddValidatorsFromAssemblyContaining<CreatePartyValidator>()`; keep the registration mechanism. Existing test `ProcessAsync_CommandTypeWithNoRegisteredValidator_ProceedsToAggregate` asserts fail-open and must be replaced.
- Keep the four legacy exceptions as exact resolved Type comparisons inside the missing-validator branch only; do not bypass a supplied validator. Every other resolved type without a validator returns canonical CommandType plus fixed `CommandType` / `ValidatorNotRegistered` failure, without echoing payloads.
- Production composition tests must call `AddParties`, not reproduce its scan. Use disposable scopes/providers, existing Shouldly/NSubstitute conventions and xUnit cancellation tokens. Invalid/missing-validator tests must supply non-null protected snapshot/event state and assert only rejection plus no unprotection.
- Verification: build `tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj` with `-c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; invoke its built assembly directly for focused classes, then the full affected test project. Preserve warning gates and historical receipts. Runtime baseline and exact command results will be appended after completion.
- Runtime baseline: explicitly targeted `aspire start --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --no-build --non-interactive --format Json` succeeded; `aspire wait parties --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --timeout 30 --non-interactive` reported healthy. Resource inspection showed Parties/sidecars healthy, gateway/admin/Tenants exited 134, UI exited 150, and explicit-start resources not started. This is an unqualified existing-binary observation, not a passing full-topology gate. Stopped only this AppHost before compilation; the other running AppHost was preserved.
- Initial test compilation exposed a missing required test-event member and an untyped tuple switch; corrected the fixture. The subsequent Debug source build passed with zero warnings/errors. Red focused run: 64 cases, 56 passed, 8 failed. Seven failures demonstrate missing-validator bypass (CreateParty and both consent commands under three command-name representations). One additional baseline failure demonstrates named-enum binding incompatibility: the existing validator accepts `lawfulBasis:"Consent"`, then EventStore aggregate binding throws JsonException. The shared EventStore reader has no string-enum converter while the Parties client emits enum names. This requires a separate platform/consumer compatibility fix; retain the reproduction in evidence and defer it. The new positive processing tests use the numeric enum wire shape accepted by the existing dispatcher, matching the pre-existing processor fixture, without claiming named-enum end-to-end coverage.
- Implemented the exact four missing-validator legacy exceptions and fixed rejection/logger behavior. Added production registration/inventory, consent positive/negative, missing-registration alias, legacy-success, and supplied-validator tests; replaced the prior fail-open test. Production assembly registration and all validator rules are unchanged. Debug source build passed with zero warnings/errors; focused tests passed 102/102; full affected assembly passed 994/994 with no skips/errors/failures/not-run tests. Evidence: `tests/dw-122-production-validation-2026-10-16/validation.json`. Independent review remains pending. Preserved tracked C# line endings and used CRLF for the new file; whitespace checks recognize CRLF explicitly.

## Review Triage Log

Independent one-shot blind review: changed content 38.92 kB; finding floor `min(floor(sqrt(38.92) + 1), 10) = 7`. All seven findings were checked individually; no configured review layer was skipped.

- [low] [patch] The replaced CreateParty test reused inconsistent replay metadata (snapshot at checkpoint zero and application/json). Made that test's snapshot/checkpoints and protected event sequence/format admissible, so a validation bypass would reach the provider boundary. Existing unrelated fixtures are unchanged.
- [low] [patch] Supplied-validator coverage exercised only RotatePartyKey. Parameterized all four legacy command types with deliberately rejecting supplied validators; each must emit only the fixed supplied-validator rejection before state access.
- [low] [patch] Positive consent cases asserted only event type and used a zero enum value. Use nonzero LegitimateInterest and assert emitted party/tenant/consent/channel/purpose/lawful-basis fields; revocation also checks the submitted reason.
- [false] [reject] Additional keyed production processor composition was requested. Existing `PartiesProcessEndpointTests.PostProcess_InvalidPayload_UsesProductionProcessorValidationRejectionAsync` invokes the actual authenticated /process route through `PartiesProcessTestFactory(useProductionProcessor:true)`, which retains production keyed registrations; it asserts the precise validation event. `ProductionAsyncProcessor_AlreadyCanceledToken_ThrowsBeforeProducingEventsAsync` also directly resolves the keyed IAsyncDomainProcessor. These cases passed in the full project. The new tests intentionally isolate the processor with the real AddParties validator registry; there is no demonstrated missing production processor composition boundary.
- [low] [patch] The missing-validator branch was covered for CreateParty and consent, not the full explicit exception boundary. Added a production-registration removal case that iterates every other command in Contracts and requires the exact missing-validator rejection before unprotection.
- [low] [patch] Historical red source hashes were not captured before the serializer helper changed. Preserved `red-source.patch` plus baseline commit, reconstructed test-file SHA-256 identities and a successful apply-check against a separate baseline index. This reconstruction restores the pre-review fixtures and string-enum helper; it is explicitly not retroactive certification of original run-time hashes. Final-source hashes remain separate.
- [low] [patch] The new enum compatibility follow-up lacked an addressable lifecycle. Gave it DW-195 and explicit open status using the ledger's existing numbered-entry convention. No other deferred item is added or closed.

### Completion — 2026-10-16

The post-review Debug source build passed with zero warnings/errors. Focused classes passed 106/106 and the full affected Parties.Tests assembly passed 998/998, with zero errors, failures, skips, or not-run tests. The warning-policy gate passed. All recorded source and evidence digests match. Independent targeted follow-up confirmed all six corrections and the disproof of finding 4, with no remaining concrete defect. DW-122 is complete; DW-195 remains open. The exact local commit candidate passed pinned commitlint 21.2.2 with zero problems/warnings; the one-shot workflow requires the local commit, and no remote push is performed.
