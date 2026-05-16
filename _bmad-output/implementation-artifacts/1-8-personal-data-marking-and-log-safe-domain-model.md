# Story 1.8: Personal Data Marking and Log-Safe Domain Model

Status: ready-for-dev

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
   - And logs do not include personal data field values, raw command payloads, raw event payloads, raw serialized JSON, raw ProblemDetails bodies, tokens, claims dictionaries, secrets, or infrastructure connection details.

5. **Automated tests pin the personal-data inventory and log-safety rules**
   - Given privacy-marking and log-safety tests run,
   - When they inspect known personal-data contract fields and representative logging paths,
   - Then required fields are marked consistently,
   - And tests fail on missing `[PersonalData]` coverage or direct payload logging regressions,
   - And tests use synthetic placeholders without storing real personal data in assertion messages, log captures, or snapshots.

## Tasks / Subtasks

- [ ] Task 1: Inventory the current personal-data contract surface (AC: 1, 2, 3, 5)
  - [ ] Inspect `PersonalDataAttribute`, value objects, commands, events, state, models, search DTOs, and security contracts before changing attributes.
  - [ ] Confirm existing `[PersonalData]` coverage on `PersonDetails`, `PartyState.DisplayName`, `PartyState.SortName`, `PartyDetail.DisplayName`, `PartyDetail.SortName`, `PartyDetail.NameHistory`, `PartyIndexEntry.DisplayName`, `ContactChannel.Value`, `EmailAddress.Address`, `PostalAddress` fields, `PhoneNumber.Number`, `SocialMediaHandle.Handle`, `PartyIdentifier.Value`, `AddContactChannel.Value`, `UpdateContactChannel.Value`, `AddIdentifier.Value`, `ContactChannelAdded.Value`, `ContactChannelUpdated.Value`, and `IdentifierAdded.Value`.
  - [ ] Verify whether `PartyDisplayNameDerived.DisplayName`, `PartyDisplayNameDerived.SortName`, `NameHistoryEntry.DisplayName`, and `NameHistoryEntry.SortName` are currently unmarked; if still unmarked, patch those narrow contract fields or record a clear reason if the attribute model intentionally relies on containing properties.
  - [ ] Verify `PartyCreated.PersonDetails`, `PersonDetailsUpdated.PersonDetails`, `CreateParty.PersonDetails`, and `UpdatePersonDetails.PersonDetails` are covered through nested `PersonDetails` attributes; do not duplicate attributes on container properties unless the reflection scanner requires top-level command/event discovery.
  - [ ] Keep `OrganizationDetails.LegalName`, `TradingName`, `LegalForm`, and `RegistrationNumber` aligned with architecture D6: organization entity fields are not marked by default for corporations; `IsNaturalPerson` is the future type-dependent escalation flag.

- [ ] Task 2: Define or update reflection-based privacy coverage tests (AC: 1, 2, 3, 5)
  - [ ] Add or update focused tests in `tests/Hexalith.Parties.Contracts.Tests` or `tests/Hexalith.Parties.Security.Tests` that enumerate the expected `[PersonalData]` property inventory.
  - [ ] Cover nested value-object fields and top-level command/event/model fields separately so a regression in either layer is visible.
  - [ ] Assert organization entity fields are intentionally unmarked for default corporate handling, while contact channels and identifiers remain marked for all party types.
  - [ ] Include a test name or data row for `PartyDisplayNameDerived` and `NameHistoryEntry` so derived names do not slip through future crypto-shredding or log-sanitization preparation.
  - [ ] Avoid snapshot tests that print personal data values; assert property names and types instead.

- [ ] Task 3: Audit log and telemetry surfaces for payload leakage (AC: 4, 5)
  - [ ] Inspect `src/Hexalith.Parties/ErrorHandling`, `src/Hexalith.Parties/Domain`, `src/Hexalith.Parties/Extensions`, `src/Hexalith.Parties.Projections`, `src/Hexalith.Parties/Search`, and `src/Hexalith.Parties.ServiceDefaults`.
  - [ ] Confirm logger messages use bounded metadata and do not interpolate raw command/event objects, serialized payload bytes, contact values, identifier values, person names, organization natural-person names, tokens, claims dictionaries, authorization headers, Dapr ports, connection strings, or raw backend response bodies.
  - [ ] Pay special attention to `PartiesGlobalExceptionHandler`, `PartiesValidationExceptionHandler`, `PartyProjectionUpdateOrchestrator`, `PartyDetailProjectionActor`, `PartyIndexProjectionActor`, `ProjectionRebuildService`, `PartyMemoryIndexingService`, `MemoriesPartySearchService`, `PartyMemoryCleanupService`, and `PartyMemoryUnitMappingStore`.
  - [ ] Treat party id, tenant id, event type name, command type name, correlation id, sequence number, and bounded outcome/status codes as allowed log fields.
  - [ ] If `ServiceDefaults` keeps `IncludeFormattedMessage = true`, prove generated messages do not include personal-data arguments; do not disable observability globally unless a specific leak requires it.

- [ ] Task 4: Patch only narrow marking or logging gaps (AC: 1, 2, 3, 4)
  - [ ] Add `[PersonalData]` to missing contract properties only when the field itself always carries personal data under D6 or the current MVP conservative rule.
  - [ ] Prefer generated `LoggerMessage` or structured `ILogger` templates with safe fields when replacing unsafe logging.
  - [ ] Do not add a new serializer, redaction framework, encryption layer, crypto-shredding runtime, key-management behavior, projection rebuild behavior, search-index erasure behavior, REST controller, MCP tool, AdminPortal UI, Picker UI, or public API surface in this story.
  - [ ] Do not make `Hexalith.Parties.Contracts` depend on hosting, Dapr, MediatR, FluentValidation, UI, infrastructure, or logging implementation packages.
  - [ ] Preserve EventStore aggregate identity and existing command/event contract shapes except for additive `[PersonalData]` attributes.

- [ ] Task 5: Document unresolved v1.1 privacy-design questions in the Dev Agent Record (AC: 3, 4)
  - [ ] If organization natural-person field handling cannot be represented with simple property attributes, record the exact gap and reference D6/D7 rather than changing default organization field markings.
  - [ ] If framework-level log sanitization is not yet available in EventStore or service defaults, record the accepted MVP evidence and the remaining platform-level decision without inventing local ad hoc redaction.
  - [ ] If reflection tests cannot see nested personal-data properties through command/event containers, record whether the scanner was extended or whether top-level container attributes were intentionally added.

- [ ] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PersonalData`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyPersonalDataCommandGuardTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if `PartyState` changes.
  - [ ] Run focused projection/search tests if a logging assertion or message in those surfaces changes.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, shared serialization, service defaults, or project references change.

## Dev Notes

### Current Implementation Context

- This is a reconciliation story over an already-evolved privacy surface. Start by proving current `[PersonalData]` coverage and log safety, then patch only concrete gaps.
- `PersonalDataAttribute` already exists in `Hexalith.Parties.Contracts` and is documented as preparation for v1.1 crypto-shredding and MVP log sanitization.
- Many core fields are already marked: person details, contact channel values, identifier values, state/detail/index display names, and several command/event payload values.
- Current source inspection shows likely derived-name gaps to verify: `PartyDisplayNameDerived.DisplayName`, `PartyDisplayNameDerived.SortName`, `NameHistoryEntry.DisplayName`, and `NameHistoryEntry.SortName` are not directly marked at story creation time.
- `PartyDetail.NameHistory` is currently marked as a collection, but the nested `NameHistoryEntry` fields should still be tested explicitly because future serializers/scanners may inspect item properties rather than only the containing model.
- `OrganizationDetails` fields are intentionally type-dependent under D6. Do not mark all organization entity fields globally unless a later accepted architecture decision replaces D6.

### Architecture Patterns and Constraints

- D6 defines precise type-dependent personal-data scope: person parties include names, DOB, derived names; all party types include contact channels and identifiers; organization entity fields are not encrypted by default; `IsNaturalPerson` elevates organization parties to person-level handling in v1.1.
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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-16: Story created by BMAD pre-dev hardening automation with current personal-data marker inventory, D6 type-dependent organization scope, and log-safe domain-model reconciliation context.
