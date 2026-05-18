# Story 1.8: Personal Data Marking and Log-Safe Domain Model

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a privacy-conscious operator,
I want personal data fields to be explicitly marked and excluded from application logging,
so that MVP domain contracts are prepared for GDPR enforcement without leaking sensitive data.

## Acceptance Criteria

1. **Person and derived-name fields are marked for privacy enforcement**
   - Given person detail fields are defined in contracts, events, state, query models, or projection models,
   - When the domain model is reviewed or tested,
   - Then first name, last name, date of birth, prefix, suffix, display name, sort name, and name-history display/sort values are marked as personal data where the current attribute model can represent them,
   - And the markings are discoverable by reflection-based automated privacy enforcement.

2. **Contact channel payloads are marked conservatively**
   - Given contact channel payloads are defined for postal, email, phone, social, command, event, state, detail, index, or search-facing contract models,
   - When the domain model is reviewed or tested,
   - Then all contact channel payload values that may identify a person are marked as personal data,
   - And organization contact channels are treated conservatively because they can identify natural persons.

3. **Identifier values are marked while type-dependent organization fields stay explicit**
   - Given identifier values are defined for VAT, SIRET, national ID, or other references,
   - When the domain model is reviewed or tested,
   - Then identifier values are marked as personal data,
   - And the story preserves the D6 distinction that organization entity fields are not encrypted by default unless `IsNaturalPerson` elevates the party to person-level handling,
   - And any gap that cannot be represented by a simple property attribute is documented as a deferred v1.1 crypto-shredding design decision instead of being hidden.

4. **Domain logging and telemetry stay payload-safe**
   - Given domain commands, events, rejections, projection handling, search indexing, erasure/rebuild paths, or exception handlers emit logs or telemetry,
   - When application logging occurs,
   - Then logs include safe metadata such as tenant id, aggregate/party id, correlation id, command/event type, sequence number, outcome code, store name, and bounded failure category where useful,
   - And for this story, log-safe means no `[PersonalData]` value, raw payload, diagnostic-enricher payload, object `ToString()` output, or serialized command/event body appears in logs or telemetry,
   - And logs do not include personal data field values, raw command payloads, raw event payloads, raw serialized JSON, raw ProblemDetails bodies, tokens, claims dictionaries, secrets, or infrastructure connection details.

5. **Automated tests pin the personal-data inventory and log-safety rules**
   - Given privacy-marking and log-safety tests run,
   - When they inspect known personal-data contract fields, known non-person organization fields, and representative logging paths,
   - Then required fields are marked consistently,
   - And organization legal/display fields remain intentionally unmarked by default while organization contact channels and identifiers remain marked,
   - And tests fail on missing `[PersonalData]` coverage or direct payload logging regressions,
   - And tests use synthetic placeholders without storing real personal data in assertion messages, log captures, or snapshots.
   - And test failure output identifies property names and classification categories, not captured runtime values.

## Tasks / Subtasks

- [x] Task 1: Inventory the current personal-data contract surface (AC: 1, 2, 3, 5)
  - [x] Inspect `PersonalDataAttribute`, value objects, commands, events, state, models, search DTOs, and security contracts before changing attributes.
  - [x] Confirm existing `[PersonalData]` coverage on `PersonDetails`, `PartyState.DisplayName`, `PartyState.SortName`, `PartyDetail.DisplayName`, `PartyDetail.SortName`, `PartyDetail.NameHistory`, `PartyIndexEntry.DisplayName`, `ContactChannel.Value`, `EmailAddress.Address`, `PostalAddress` fields, `PhoneNumber.Number`, `SocialMediaHandle.Handle`, `PartyIdentifier.Value`, `AddContactChannel.Value`, `UpdateContactChannel.Value`, `AddIdentifier.Value`, `ContactChannelAdded.Value`, `ContactChannelUpdated.Value`, and `IdentifierAdded.Value`.
  - [x] Verify whether `PartyDisplayNameDerived.DisplayName`, `PartyDisplayNameDerived.SortName`, `NameHistoryEntry.DisplayName`, and `NameHistoryEntry.SortName` are currently unmarked; if still unmarked, patch those narrow contract fields or record a clear reason if the attribute model intentionally relies on containing properties.
  - [x] Verify `PartyCreated.PersonDetails`, `PersonDetailsUpdated.PersonDetails`, `CreateParty.PersonDetails`, and `UpdatePersonDetails.PersonDetails` are covered through nested `PersonDetails` attributes; do not duplicate attributes on container properties unless the reflection scanner requires top-level command/event discovery.
  - [x] Keep `OrganizationDetails.LegalName`, `TradingName`, `LegalForm`, and `RegistrationNumber` aligned with architecture D6: organization entity fields are not marked by default for corporations; `IsNaturalPerson` is the future type-dependent escalation flag.

- [x] Task 2: Define or update reflection-based privacy coverage tests (AC: 1, 2, 3, 5)
  - [x] Add or update focused tests in `tests/Hexalith.Parties.Contracts.Tests` or `tests/Hexalith.Parties.Security.Tests` that enumerate the expected `[PersonalData]` property inventory.
  - [x] Treat the expected inventory rule as the source of truth: person names, derived names, date of birth, contact payload values, and identifier values must be marked; organization entity fields remain unmarked by default under D6.
  - [x] Cover nested value-object fields and top-level command/event/model fields separately so a regression in either layer is visible.
  - [x] Assert organization entity fields are intentionally unmarked for default corporate handling, while organization contact channels and identifiers remain marked for all party types.
  - [x] Add positive and negative inventory cases so new domain contract properties fail loudly until classified as personal, non-personal, or deferred by documented D6/D7 policy.
  - [x] Record the expected inventory as property/type metadata only; do not store example names, contact values, identifier values, serialized payloads, or formatted object values in test data, snapshots, or assertion messages.
  - [x] Include a test name or data row for `PartyDisplayNameDerived` and `NameHistoryEntry` so derived names do not slip through future crypto-shredding or log-sanitization preparation.
  - [x] Avoid snapshot tests that print personal data values; assert property names and types instead.

- [x] Task 3: Audit log and telemetry surfaces for payload leakage (AC: 4, 5)
  - [x] Inspect `src/Hexalith.Parties/ErrorHandling`, `src/Hexalith.Parties/Domain`, `src/Hexalith.Parties/Extensions`, `src/Hexalith.Parties.Projections`, `src/Hexalith.Parties/Search`, and `src/Hexalith.Parties.ServiceDefaults`.
  - [x] Confirm logger messages use bounded metadata and do not interpolate raw command/event objects, serialized payload bytes, contact values, identifier values, person names, organization natural-person names, tokens, claims dictionaries, authorization headers, Dapr ports, connection strings, or raw backend response bodies.
  - [x] Include diagnostic enrichers, exception detail paths, object `ToString()` paths, and generated formatted messages in the audit so log-safety is not limited to obvious direct `ILogger` calls.
  - [x] Include validation and ProblemDetails paths in the audit: error codes, property names, and bounded failure categories are allowed, but attempted values, raw request bodies, exception `Data`, claims dictionaries, tokens, connection strings, and backend response bodies are not.
  - [x] Pay special attention to `PartiesGlobalExceptionHandler`, `PartiesValidationExceptionHandler`, `PartyProjectionUpdateOrchestrator`, `PartyDetailProjectionActor`, `PartyIndexProjectionActor`, `ProjectionRebuildService`, `PartyMemoryIndexingService`, `MemoriesPartySearchService`, `PartyMemoryCleanupService`, and `PartyMemoryUnitMappingStore`.
  - [x] Treat party id, tenant id, event type name, command type name, correlation id, sequence number, and bounded outcome/status codes as allowed log fields.
  - [x] If `ServiceDefaults` keeps `IncludeFormattedMessage = true`, prove generated messages do not include personal-data arguments; do not disable observability globally unless a specific leak requires it.

- [x] Task 4: Patch only narrow marking or logging gaps (AC: 1, 2, 3, 4)
  - [x] Add `[PersonalData]` to missing contract properties only when the field itself always carries personal data under D6 or the current MVP conservative rule.
  - [x] Prefer generated `LoggerMessage` or structured `ILogger` templates with safe fields when replacing unsafe logging.
  - [x] Do not add a new serializer, redaction framework, encryption layer, crypto-shredding runtime, key-management behavior, projection rebuild behavior, search-index erasure behavior, REST controller, MCP tool, AdminPortal UI, Picker UI, or public API surface in this story.
  - [x] Do not make `Hexalith.Parties.Contracts` depend on hosting, Dapr, MediatR, FluentValidation, UI, infrastructure, or logging implementation packages.
  - [x] Preserve EventStore aggregate identity and existing command/event contract shapes except for additive `[PersonalData]` attributes.

- [x] Task 5: Document unresolved v1.1 privacy-design questions in the Dev Agent Record (AC: 3, 4)
  - [x] If organization natural-person field handling cannot be represented with simple property attributes, record the exact gap and reference D6/D7 rather than changing default organization field markings.
  - [x] If framework-level log sanitization is not yet available in EventStore or service defaults, record the accepted MVP evidence and the remaining platform-level decision without inventing local ad hoc redaction.
  - [x] If reflection tests cannot see nested personal-data properties through command/event containers, record whether the scanner was extended or whether top-level container attributes were intentionally added.
  - [x] Record the bounded log-safety evidence gathered during implementation, including which representative logging/exception paths were inspected and which focused tests or grep-style checks were run.

- [x] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5)
  - [x] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PersonalData`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyPersonalDataCommandGuardTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if `PartyState` changes.
  - [x] Run focused projection/search tests if a logging assertion or message in those surfaces changes.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, shared serialization, service defaults, or project references change.

## Dev Notes

### Current Implementation Context

- This is a reconciliation story over an already-evolved privacy surface. Start by proving current `[PersonalData]` coverage and log safety, then patch only concrete gaps.
- `PersonalDataAttribute` already exists in `Hexalith.Parties.Contracts` and is documented as preparation for v1.1 crypto-shredding and MVP log sanitization.
- `[PersonalData]` is classification metadata only. This story must not treat the attribute as runtime authorization, masking, encryption, retention, erasure, or serializer behavior unless an existing platform component already consumes it and only needs focused evidence.
- Many core fields are already marked: person details, contact channel values, identifier values, state/detail/index display names, and several command/event payload values.
- Current source inspection shows likely derived-name gaps to verify: `PartyDisplayNameDerived.DisplayName`, `PartyDisplayNameDerived.SortName`, `NameHistoryEntry.DisplayName`, and `NameHistoryEntry.SortName` are not directly marked at story creation time.
- `PartyDetail.NameHistory` is currently marked as a collection, but the nested `NameHistoryEntry` fields should still be tested explicitly because future serializers/scanners may inspect item properties rather than only the containing model.
- `OrganizationDetails` fields are intentionally type-dependent under D6. Do not mark all organization entity fields globally unless a later accepted architecture decision replaces D6.

### Party-Mode Review Clarifications

- Keep the implementation metadata-local: domain-contract marking, focused tests, and log-surface evidence only.
- `Log-safe` means no `[PersonalData]` value, raw command/event payload, diagnostic-enricher payload, object `ToString()` output, or serialized body reaches logs or telemetry.
- Positive marker inventory must cover person names, derived names, date of birth, contact payload values, identifier values, `PartyDisplayNameDerived`, and `NameHistoryEntry`.
- Negative marker inventory must protect D6: organization legal/trading/display fields are not marked by default, while organization contact channels and identifiers are still marked.
- Reflection tests should make newly introduced domain contract properties fail loudly until they are classified as personal, non-personal, or explicitly deferred by D6/D7 policy.
- Do not turn this story into a runtime redaction platform, serializer rewrite, middleware addition, projection/search rewrite, UI change, REST/MCP surface, or v1.1 crypto-shredding implementation.

### Architecture Patterns and Constraints

- D6 defines precise type-dependent personal-data scope: person parties include names, DOB, derived names; all party types include contact channels and identifiers; organization entity fields are not encrypted by default; `IsNaturalPerson` elevates organization parties to person-level handling in v1.1.
- Derived `DisplayName` and `SortName` fields on state/detail/index/history surfaces remain personal-data-marked even when the source party is an organization. This does not mean `OrganizationDetails.LegalName`, `TradingName`, `LegalForm`, or `RegistrationNumber` should be globally marked by default; it reflects the broader exposure risk of derived display/search values.
- D7 says mid-lifecycle `IsNaturalPerson` reclassification is a v1.1 complexity hotspot. This story may document the gap but should not implement re-encryption or retroactive event mutation.
- MVP scope is attribute placement and log-safety evidence. Runtime field-level encryption, crypto-shredding, key lookup, decrypted publication, erased-party reads, and backup/restore erasure behavior are v1.1 work unless already present and only need narrow tests.
- Contracts must remain dependency-light and additive. Attribute changes are acceptable; infrastructure dependencies in contracts are not.
- Structured logging should prefer safe metadata. The project already uses JSON console/OpenTelemetry and generated logger messages in several services; do not replace observability with broad suppression.
- The main `src/Hexalith.Parties` project remains an actor host. Do not add REST, Swagger/OpenAPI, or in-process MCP hosting while working this privacy-marking story.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/
  PersonalDataAttribute.cs
  ValueObjects/PersonDetails.cs
  ValueObjects/OrganizationDetails.cs
  ValueObjects/ContactChannel.cs
  ValueObjects/EmailAddress.cs
  ValueObjects/PostalAddress.cs
  ValueObjects/PhoneNumber.cs
  ValueObjects/SocialMediaHandle.cs
  ValueObjects/PartyIdentifier.cs
  ValueObjects/NameHistoryEntry.cs
  Commands/CreateParty.cs
  Commands/UpdatePersonDetails.cs
  Commands/UpdateOrganizationDetails.cs
  Commands/AddContactChannel.cs
  Commands/UpdateContactChannel.cs
  Commands/AddIdentifier.cs
  Events/PartyCreated.cs
  Events/PersonDetailsUpdated.cs
  Events/OrganizationDetailsUpdated.cs
  Events/PartyDisplayNameDerived.cs
  Events/ContactChannelAdded.cs
  Events/ContactChannelUpdated.cs
  Events/IdentifierAdded.cs
  Models/PartyDetail.cs
  Models/PartyIndexEntry.cs
  State/PartyState.cs
  Security/IPersonalDataCommandGuard.cs

src/Hexalith.Parties/
  ErrorHandling/PartiesGlobalExceptionHandler.cs
  ErrorHandling/PartiesValidationExceptionHandler.cs
  Domain/PartyDomainServiceInvoker.cs
  Domain/PartyProjectionUpdateOrchestrator.cs
  Extensions/PartiesServiceCollectionExtensions.cs
  Search/*.cs

src/Hexalith.Parties.Projections/
  Actors/PartyDetailProjectionActor.cs
  Actors/PartyIndexProjectionActor.cs
  Services/ProjectionRebuildService.cs

src/Hexalith.Parties.ServiceDefaults/
  Extensions.cs

tests/Hexalith.Parties.Contracts.Tests/
tests/Hexalith.Parties.Security.Tests/
```

### Previous Story Intelligence

- Story 1.2 clarified that stable party identity belongs to EventStore aggregate/stream metadata; do not add identifiers to success events just to support logging or privacy tests.
- Stories 1.3 through 1.5 reinforced privacy-safe assertion style: prove behavior without embedding raw names, contacts, identifiers, or payloads in logs or broad snapshots.
- Story 1.6 kept lifecycle soft-deactivation separate from erasure, deletion, anonymization, and crypto-shredding.
- Story 1.7 clarified that rejection messages, composite outcomes, public failure strings, logs, telemetry dimensions, test names, and assertion messages must not contain raw person names, contact values, identifier values, payload text, or infrastructure secrets.
- Recent hardening runs favor audit-first reconciliation stories with explicit deferred decisions instead of broad product/API changes.

### Testing Requirements

- Use xUnit v3 and Shouldly patterns already present in the repository.
- Prefer reflection tests for attribute inventory so new contract fields fail fast when they omit required `[PersonalData]` markings.
- Keep tests deterministic and synthetic. Use placeholder property names and types, not real personal data values.
- Add negative assertions for organization entity fields if useful, but phrase them around D6 intent so a future accepted architecture change can update the inventory deliberately.
- For logging tests, inspect message templates or captured structured state where feasible; avoid asserting full formatted log text when it would encourage personal-data snapshots.
- AC-to-test trace should map AC 1-3 to reflection inventory tests, AC 4 to log/telemetry guardrail tests or explicit audit evidence, and AC 5 to the focused test commands run during implementation.

### Anti-Patterns To Avoid

- Do not implement v1.1 crypto-shredding, key re-encryption, secret-store behavior, payload protection, backup/restore erasure semantics, or EventStore platform redaction in this story.
- Do not mark all `OrganizationDetails` fields as personal data by default without an accepted D6 replacement decision.
- Do not log or snapshot raw command/event objects to prove log safety.
- Do not add broad serializers, middleware, logging providers, HTTP endpoints, MCP tools, UI screens, projection rebuild features, search cleanup behavior, or deployment topology changes.
- Do not make `Hexalith.Parties.Contracts` depend on infrastructure packages.
- Do not weaken existing EventStore apply-ordering, rejection-event, tenant-boundary, or actor-host guardrails while touching shared contracts.
- Do not recursively initialize nested submodules.

### Deferred Decisions

- Type-dependent v1.1 crypto-shredding for `OrganizationDetails` when `IsNaturalPerson` changes remains governed by architecture D6/D7.
- A platform-wide serializer/redaction framework that automatically masks `[PersonalData]` fields across all logs remains an EventStore/service-defaults integration decision unless already accepted elsewhere.
- Runtime event/snapshot encryption, key lookup, decrypted publication, and post-erasure read behavior remain v1.1 scope.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.8] - Story statement and BDD acceptance criteria for FR42 and FR43.
- [Source: _bmad-output/planning-artifacts/prd.md#Functional-Requirements] - FR42 personal-data marking and FR43 log exclusion.
- [Source: _bmad-output/planning-artifacts/prd.md#Log-Sanitization] - MVP requirement that `[PersonalData]` fields are masked or excluded from logging.
- [Source: _bmad-output/planning-artifacts/prd.md#Encryption-and-Crypto-shredding] - MVP marker scope and v1.1 encryption boundary.
- [Source: _bmad-output/planning-artifacts/architecture.md#D6-Personal-Data-Scope-Precise-Type-Dependent-GDPR-Compliant] - Type-dependent personal-data scope and `IsNaturalPerson` handling.
- [Source: _bmad-output/planning-artifacts/architecture.md#Logging] - Structured logging and no-PII logging guidance.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Contract, privacy, logging, testing, and submodule guardrails.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later party-mode and elicitation passes.
- [Source: _bmad-output/implementation-artifacts/1-7-idempotent-commands-and-typed-rejections.md] - Previous story privacy-safe rejection/logging guidance.
- [Source: src/Hexalith.Parties.Contracts/PersonalDataAttribute.cs] - Current marker attribute.
- [Source: src/Hexalith.Parties.Contracts/ValueObjects/PersonDetails.cs] - Current person-field markings.
- [Source: src/Hexalith.Parties.Contracts/ValueObjects/OrganizationDetails.cs] - Current D6 type-dependent organization model.
- [Source: src/Hexalith.Parties.Contracts/Events/PartyDisplayNameDerived.cs] - Current derived-name event surface to verify.
- [Source: src/Hexalith.Parties.Contracts/ValueObjects/NameHistoryEntry.cs] - Current name-history item surface to verify.
- [Source: src/Hexalith.Parties.ServiceDefaults/Extensions.cs] - Current JSON/OpenTelemetry logging defaults.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-18: Red test run for `FullyQualifiedName~PersonalData` failed on `NameHistoryEntry.DisplayName`, `NameHistoryEntry.SortName`, `PartyDisplayNameDerived.DisplayName`, and `PartyDisplayNameDerived.SortName`.
- 2026-05-18: Red test run for `PartiesGlobalExceptionHandlerTests` proved development 500 ProblemDetails echoed raw exception messages before the log-safety patch.
- 2026-05-18: Full `dotnet build Hexalith.Parties.slnx --configuration Release` initially hit stale/corrupt Picker generated output; cleaned Picker project/test Release outputs and reran successfully.

### Completion Notes List

- Added direct `[PersonalData]` markers to derived display/sort names on `PartyDisplayNameDerived` and `NameHistoryEntry`.
- Added metadata-only reflection inventory coverage for personal-data, non-personal, and deferred contract fields, including D6 organization entity-field negative coverage and nested container discoverability checks.
- Updated security payload classification so `PartyDisplayNameDerived` is treated as carrying protected data.
- Tightened representative log/ProblemDetails surfaces by removing raw exception-message echo from development 500 details and replacing selected failure/log reason fields with bounded exception type names.
- D6/D7 deferred gap remains: `OrganizationDetails` default corporate entity fields stay unmarked, while runtime type-dependent `IsNaturalPerson` escalation remains v1.1 design/crypto-shredding scope.
- Framework-level automatic `[PersonalData]` log redaction remains a platform/service-defaults decision; this story preserves `IncludeFormattedMessage = true` and bounds local message arguments inspected here.
- Reflection scanner coverage works through nested `PersonDetails`, contact-channel, identifier, and collection item types, so top-level command/event container attributes were not duplicated.
- Validation passed: focused contract/security/error/search/projection tests, full Release solution build, and full Release solution test suite.

### File List

- _bmad-output/implementation-artifacts/1-8-personal-data-marking-and-log-safe-domain-model.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/Hexalith.Parties.Contracts/Events/PartyDisplayNameDerived.cs
- src/Hexalith.Parties.Contracts/ValueObjects/NameHistoryEntry.cs
- src/Hexalith.Parties/ErrorHandling/PartiesGlobalExceptionHandler.cs
- src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
- src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
- src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs
- src/Hexalith.Parties/Search/PartyMemoryIndexingService.cs
- src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs
- tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs
- tests/Hexalith.Parties.Security.Tests/PartyPayloadProtectionServiceTests.cs
- tests/Hexalith.Parties.Tests/ErrorHandling/PartiesGlobalExceptionHandlerTests.cs

## Change Log

- 2026-05-18: Implemented story 1.8 privacy inventory, derived-name `[PersonalData]` markers, bounded ProblemDetails/log reason hardening, and validation evidence.
- 2026-05-17: Advanced elicitation applied pre-dev clarifications for metadata-only `[PersonalData]` scope, privacy-safe inventory test output, validation/ProblemDetails log-safety audit paths, derived-name versus organization D6 boundaries, and bounded evidence capture.
- 2026-05-16: Party-mode review applied pre-dev clarifications for log-safe meaning, positive/negative marker inventory tests, D6 organization-field protection, diagnostic-enricher/`ToString()` audit coverage, and metadata-local implementation scope.
- 2026-05-16: Story created by BMAD pre-dev hardening automation with current personal-data marker inventory, D6 type-dependent organization scope, and log-safe domain-model reconciliation context.

## Advanced Elicitation

- Date/time: 2026-05-17T09:06:45+02:00
- Selected story key: `1-8-personal-data-marking-and-log-safe-domain-model`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-8-personal-data-marking-and-log-safe-domain-model`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - The story can create false confidence if tests prove attributes by printing sample personal-data values or snapshots; evidence must identify property/type metadata and classifications only.
  - `[PersonalData]` must remain classification metadata in this story, not an implied runtime redaction, encryption, authorization, retention, or erasure platform.
  - Validation, ProblemDetails, exception `Data`, diagnostic enrichers, object `ToString()`, and formatted logger messages are high-risk leak paths that need explicit audit coverage, not just direct `ILogger` templates.
  - D6 needs sharper wording because derived display/sort fields should remain marked while organization entity fields stay unmarked by default unless future `IsNaturalPerson` handling is accepted.
  - Developers need bounded implementation evidence so this story does not expand into full EventStore platform redaction, serializer replacement, projection/search rewrites, or v1.1 crypto-shredding.
- Changes applied:
  - Tightened AC 5 so test failures expose property names and classification categories, not captured runtime values.
  - Added Task 2 guidance to keep inventory tests metadata-only and free of example personal-data values, serialized payloads, or formatted object output.
  - Added validation/ProblemDetails, exception `Data`, claims, token, connection-string, and backend-response checks to the log-safety audit task.
  - Added Dev Agent Record expectations for bounded log-safety evidence and focused checks.
  - Clarified Dev Notes that `[PersonalData]` is classification metadata only and that derived display/sort markings do not globally mark default organization entity fields.
- Findings deferred:
  - Runtime masking, serializer/framework redaction, EventStore platform redaction, encryption, retention, erasure, key management, and crypto-shredding remain outside this story unless separately accepted.
  - Type-dependent `OrganizationDetails` handling after `IsNaturalPerson` changes remains governed by D6/D7 and v1.1 design decisions.
  - Full telemetry provider replacement, projection/search cleanup behavior, UI, REST, MCP, and deployment changes remain out of scope for this hardening pass.
- Final recommendation: ready-for-dev

## Party-Mode Review

- Date/time: 2026-05-16T12:01:45+02:00
- Selected story key: `1-8-personal-data-marking-and-log-safe-domain-model`
- Command/skill invocation used: `/bmad-party-mode 1-8-personal-data-marking-and-log-safe-domain-model; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers recommended `ready-for-dev`; no blocker was identified.
  - The story is architecturally sound if implementation stays metadata-local: marker inventory, narrow attribute patches, and log-safety evidence.
  - The main false-confidence risk is weak reflection coverage that checks only obvious current fields and misses derived names, name history, or future unclassified contract properties.
  - D6 must be protected explicitly: organization entity fields stay unmarked by default, while organization contact channels and identifiers remain marked.
  - `Log-safe` needed a testable definition covering marked values, raw payloads, diagnostic enrichers, object `ToString()` paths, and serialized command/event bodies.
- Changes applied:
  - Clarified AC 4 with an explicit definition of log-safe for this story.
  - Clarified AC 5 and Task 2 to require positive and negative marker inventory tests.
  - Added explicit D6 negative-test guidance for organization entity fields and positive guidance for organization contact channels/identifiers.
  - Added diagnostic-enricher, exception detail, `ToString()`, and formatted-message audit guidance to Task 3.
  - Added party-mode review clarifications and change-log entry.
- Findings deferred:
  - Runtime redaction, serializer/middleware behavior, EventStore platform redaction, and crypto-shredding remain outside this story unless separately accepted.
  - Type-dependent v1.1 handling for `OrganizationDetails` when `IsNaturalPerson` changes remains governed by D6/D7.
  - Broader projection, search, UI, REST, MCP, deployment, and documentation changes remain out of scope.
- Final recommendation: ready-for-dev
