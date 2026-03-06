# Story 7.3: Handler Patterns Documentation & Dangling Reference Guidance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event subscriber developer,
I want clear documentation with handler patterns for all event types including erasure,
So that I know exactly how to build compliant event handlers — especially for the mandatory PartyErased subscription.

## Acceptance Criteria

1. **Event handler patterns per event type documented**: Given the Contracts package documentation (in `/docs/` or package README), when reviewed for handler patterns, then handler pattern documentation exists for all event types (FR38):
   - `PartyCreated` — when to create local records vs. ignore
   - `PersonDetailsUpdated` / `OrganizationDetailsUpdated` — update local display names
   - `ContactChannelAdded/Updated/Removed` — keep local contact caches in sync
   - `PartyDeactivated` / `PartyReactivated` — flag local records for review
   - `PartyErased` (v1.1) — **MANDATORY** handler pattern with explicit warning

2. **PartyErased handler pattern documented explicitly**: Given the handler patterns documentation, when reviewed for the `PartyErased` section, then an explicit warning states: "PartyErased subscription is mandatory for ALL consuming apps regardless of which other events they handle" (FR38), and code examples show a complete handler implementation covering:
   - Find all local records referencing the erased `partyId`
   - Nullify the party reference
   - Replace display names with "[Erased Party]"
   - Preserve records with independent legal retention requirements
   - Log the erasure handling for audit trail

3. **Dangling reference guidance documented**: Given the documentation, when reviewed for dangling references, then it explains:
   - What happens when a referenced party is erased
   - How to detect and clean up dangling references
   - Strategies for foreign key management with party IDs

4. **Tolerant deserialization guidance documented**: Given the documentation, when reviewed for tolerant deserialization, then it explains:
   - How to handle unknown fields (ignore)
   - How to handle missing optional fields (documented defaults)
   - How to prepare for future event types (additive evolution)
   - Code examples demonstrating the tolerant reader pattern

5. **Completeness and cross-references**: Given the documentation, when reviewed for completeness, then it references the sample integration project (Epic 6, Story 6.3) as working reference code, and it links to DAPR pub/sub configuration requirements per broker.

## Tasks / Subtasks

- [x] Task 1: Create handler patterns documentation in `docs/event-handler-patterns.md` (AC: #1)
  - [x] Document `PartyCreated` handler — when to create local records vs. ignore, person vs. org dispatch, code example
  - [x] Document `PersonDetailsUpdated` / `OrganizationDetailsUpdated` — update display names, personal data awareness, code example
  - [x] Document `ContactChannelAdded/Updated/Removed` — local contact cache sync, order-tolerant update patterns, code example
  - [x] Document `PreferredContactChannelChanged` — update preferred contact references
  - [x] Document `IdentifierAdded/Removed` — local identifier cache sync, code example
  - [x] Document `PartyDeactivated` / `PartyReactivated` — flag records for review, soft-delete considerations, code example
  - [x] Document `PartyDisplayNameDerived` — update derived display/sort names
  - [x] Document `IsNaturalPersonChanged` — update type flag, potential data handling implications
  - [x] Document `PartyMerged` (v2 placeholder) — log-and-skip pattern with future implementation notes
  - [x] Document rejection events — why they are published (Decision D3), when to handle vs. ignore

- [x] Task 2: Create PartyErased handler pattern with mandatory subscription warning (AC: #2)
  - [x] Add prominent mandatory subscription warning box/callout (FR38)
  - [x] Document complete PartyErased handler implementation: find references, nullify party reference, replace display names with "[Erased Party]", preserve records with independent retention, log for audit
  - [x] Provide C# code example showing a complete PartyErased handler (modeled on invoice scenario from PRD — Clara's journey)
  - [x] Document testing strategy for PartyErased handlers (verify all references nullified, display names replaced, records preserved)

- [x] Task 3: Create dangling reference guidance section (AC: #3)
  - [x] Explain what happens when a referenced party is erased (foreign keys become orphaned)
  - [x] Document detection patterns: referential integrity audits, gap checking, scheduled cleanup jobs
  - [x] Document cleanup strategies: nullification, "[Erased Party]" placeholders, archival patterns
  - [x] Document foreign key management strategies: nullable party IDs, soft references vs. hard references
  - [x] Provide concrete example: invoice system with 4 invoices referencing an erased party (Clara's journey from PRD)

- [x] Task 4: Create tolerant deserialization guidance section (AC: #4)
  - [x] Document unknown field handling (ignore via `System.Text.Json` defaults)
  - [x] Document missing optional field handling (documented defaults, nullable properties)
  - [x] Document additive event evolution strategy (new optional fields, new event types)
  - [x] Provide C# code example demonstrating the tolerant reader pattern with `JsonSerializerOptions`
  - [x] Document `PartyMerged` (v2) and `PartyErased` (v1.1) as forward-compatibility examples

- [x] Task 5: Add cross-references, completeness, and update existing docs (AC: #5)
  - [x] Add cross-references to sample integration project (`samples/Hexalith.Parties.Sample/PartyEventHandler.cs`) as working reference
  - [x] Add links to DAPR pub/sub configuration docs (`docs/event-publishing.md`, `docs/event-subscribing.md`)
  - [x] Add links to broker-specific ordering configuration per deployment target
  - [x] Update `docs/event-subscribing.md` forward-compat section to link to new handler patterns doc (fix existing "Epic 9" reference to point to this doc)
  - [x] Verify all 14 domain event types + 13 rejection events are covered
  - [x] Add a "Quick Reference" summary table mapping event → handler action → code reference

- [x] Task 6: Create handler pattern tests to validate documentation accuracy (AC: #1, #2)
  - [x] Test: PartyErased handler correctly nullifies party reference in a mock local store
  - [x] Test: PartyErased handler replaces display name with "[Erased Party]"
  - [x] Test: PartyErased handler preserves records with independent retention
  - [x] Test: PartyErased handler logs erasure for audit trail

## Dev Notes

### Architecture & Conventions

- **Target framework**: net10.0 (pinned in `global.json` to SDK 10.0.103)
- **Build**: `TreatWarningsAsErrors=true`, nullable enabled, implicit usings, file-scoped namespaces
- **Central package management**: All package versions in `Directory.Packages.props` at solution root
- **Solution format**: `Hexalith.Parties.slnx` (modern XML format)
- **Code style**: `.editorconfig` — Allman braces, `_camelCase` private fields, `I` prefix for interfaces, `Async` suffix, 4 spaces, CRLF, UTF-8
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}`
- **Assertions**: Shouldly library
- **Test tiers**: Tier 1 (pure logic, no DAPR), Tier 2 (DAPR slim), Tier 3 (full Aspire topology)

### What Already Exists (DO NOT Recreate)

**Sample Event Handler (REFERENCE — do not modify):**
- `samples/Hexalith.Parties.Sample/PartyEventHandler.cs` (678 lines) — Complete working implementation with:
  - All 14 event type handlers including `PartyMerged` forward-compat
  - Idempotent deduplication via `ConcurrentDictionary<string, bool>` keyed on CloudEvents `id`
  - Fallback to `{correlationId}:{sequenceNumber}` for local testing
  - Tolerant deserialization (CloudEvents `data` wrapper + direct EventEnvelope fallback)
  - Unknown event types: logs and returns 200 OK
  - Thread-safe `CustomerSummary` in-memory projection model
  - Order-tolerant projection updates (set operations, not increments)
- `samples/Hexalith.Parties.Sample/CustomerSummary.cs` — Read model: `Id`, `DisplayName`, `Email`, `Phone`, `IdentifierCount`, `LastUpdated`, `IsActive`

**Existing Documentation (EXTEND — do not rewrite):**
- `docs/event-publishing.md` — Production broker config, topic naming, dead letter, retry/circuit breaker
- `docs/event-subscribing.md` — Wire format, event types table, idempotency, ordering per broker, forward-compat
  - Lines 281-284 contain a forward reference to "Epic 9" for PartyErased/dangling reference guidance — **this must be updated to reference the new handler patterns doc**

**Event Contracts (READ-ONLY — reference for documentation):**
- `src/Hexalith.Parties.Contracts/Events/` — 14 domain event records + 13 rejection events
- `PartyMerged.cs` — v2 placeholder: `SurvivorPartyId`, `MergedPartyId`
- `PersonalDataAttribute.cs` — `[PersonalData]` marks fields requiring encryption at v1.1
  - PersonDetails: FirstName, LastName, DateOfBirth, Prefix, Suffix (all marked)
  - ContactChannel: Value field marked (email/phone/postal address values)

**DAPR Configuration (reference for documentation):**
- `src/Hexalith.Parties.AppHost/DaprComponents/` — Local dev pubsub, subscription, resiliency, accesscontrol
- `deploy/dapr/` — Production configs for Kafka, RabbitMQ, Azure Service Bus

**Story 7.2 Sample Tests (371+ tests passing — EXTEND for PartyErased tests):**
- `tests/Hexalith.Parties.Sample.Tests/PartyEventHandlerTests.cs` — 21 tests for all event types
- `tests/Hexalith.Parties.Sample.Tests/TolerantDeserializationTests.cs` — 4 tolerant deserialization tests
- `tests/Hexalith.Parties.Sample.Tests/SelectiveEventHandlingTests.cs` — 4 selective handling tests
- `tests/Hexalith.Parties.Sample.Tests/SubscriberDeliveryVerificationTests.cs` — 4 delivery tests
- `tests/Hexalith.Parties.Sample.Tests/PublisherToSubscriberContractTests.cs` — 1 contract test
- `tests/Hexalith.Parties.Sample.Tests/PartyEventHandlerCollection.cs` — xUnit collection for test serialization

### GDPR Context for PartyErased Documentation (Architecture D6, D7)

- `[PersonalData]` attribute marks fields requiring encryption at v1.1
- Crypto-shredding: per-party encryption keys managed via DAPR secret store
- `PartyErased` event fires AFTER crypto-shredding destroys the key — event payload contains NO personal data
- Subscribers receive `PartyErased` with only `partyId` — they must clean up local references
- Events published to pub/sub are DECRYPTED at publish time (subscribers never see encrypted data)

### Clara's Journey — The Canonical Dangling Reference Example (from PRD)

Clara, a backend developer on invoice management:
1. Stores `partyId` as foreign key in invoice records
2. Subscribes to `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, and `PartyErased`
3. When `PartyErased` fires:
   - Finds all 4 invoices referencing the erased partyId
   - Nullifies the party reference (clears foreign key)
   - Replaces customer display name with "[Erased Party]"
   - Preserves invoice records (independent legal retention: 7 years)
   - Logs the erasure for audit trail

### Event Wire Format (Reference for Code Examples)

Events arrive as CloudEvents 1.0 wrapping EventStore flat envelope:
```json
{
  "specversion": "1.0",
  "type": "PartyErased",
  "source": "hexalith-eventstore/tenant-a/parties",
  "id": "{correlationId}:{sequenceNumber}",
  "data": {
    "aggregateId": "tenant-a:parties:550e8400-...",
    "tenantId": "tenant-a",
    "domain": "parties",
    "sequenceNumber": 42,
    "timestamp": "2026-03-06T10:30:00+00:00",
    "correlationId": "...",
    "causationId": "...",
    "userId": "admin@example.com",
    "domainServiceVersion": "1.0.0",
    "eventTypeName": "PartyErased",
    "serializationFormat": "json",
    "payload": "<base64-encoded JSON — contains only partyId, no personal data>",
    "extensions": {}
  }
}
```

**Serialization conventions**: camelCase, ISO 8601 dates, string enums, omit nulls (`System.Text.Json` defaults).

### All 14 Domain Event Types (from Contracts/Events/)

| Event | Key Properties | Handler Action |
|-------|---------------|----------------|
| `PartyCreated` | Type, PersonDetails?, OrganizationDetails? | Create local record (or ignore) |
| `PersonDetailsUpdated` | PersonDetails | Update display name |
| `OrganizationDetailsUpdated` | OrganizationDetails | Update org display name |
| `ContactChannelAdded` | ContactChannelId, Type, Value, IsPreferred | Add to local contact cache |
| `ContactChannelUpdated` | ContactChannelId, Type?, Value?, IsPreferred? | Update local contact cache |
| `ContactChannelRemoved` | ContactChannelId | Remove from local cache |
| `PreferredContactChannelChanged` | ContactChannelId | Update preferred flag |
| `IdentifierAdded` | IdentifierId, Type, Value | Add to local identifiers |
| `IdentifierRemoved` | IdentifierId | Remove from local identifiers |
| `PartyDeactivated` | (marker — no properties) | Flag local record inactive |
| `PartyReactivated` | (marker — no properties) | Re-flag active |
| `PartyDisplayNameDerived` | DisplayName, SortName | Update derived name |
| `IsNaturalPersonChanged` | IsNaturalPerson | Update type flag |
| `PartyMerged` | SurvivorPartyId, MergedPartyId | v2 placeholder — log and skip |

Plus 13 rejection events (implementing `IRejectionEvent`) — published per Decision D3.

### Handler Implementation Patterns (from Existing Sample)

**Idempotent handler pattern (established):**
```csharp
// Primary: CloudEvents id header
// Fallback: {correlationId}:{sequenceNumber}
if (!_processedEventIds.TryAdd(eventId, true))
{
    logger.LogInformation("Skipping already-processed event {EventId}", eventId);
    return Results.Ok();  // Always return 200 OK for duplicates
}
```

**Order-tolerant projection updates (established):**
```csharp
// BAD: Incremental (order-dependent)
summary.IdentifierCount++;

// GOOD: Idempotent set (order-tolerant)
summary.DisplayName = payload.DisplayName;  // Last-write-wins
summary.IsActive = false;                    // Absolute state, not toggle
```

**Key handler rules (established):**
- Always return 200 OK (even for duplicates, unknown events, missing aggregates)
- Use `ConcurrentDictionary` for thread-safe deduplication
- In production, replace in-memory tracking with persistent deduplication store
- DAPR retries on non-2xx responses — returning errors causes redelivery loops
- Do NOT log event payloads (Security Rule #5) — structured logging with correlation IDs only

### Critical Anti-Patterns to Avoid

- **Do NOT modify** the EventStore submodule (read-only)
- **Do NOT rewrite** the existing `PartyEventHandler.cs` — it is the documentation reference code (only extend tests)
- **Do NOT rewrite** existing docs (`event-publishing.md`, `event-subscribing.md`) — only update cross-references
- **Do NOT rewrite** existing tests — add new test methods alongside existing ones
- **Do NOT add** DAPR client packages to Sample project — it uses pure HTTP endpoints
- **Do NOT log** event payloads (Security Rule #5) — structured logging with correlation IDs only
- **Do NOT create** PartyErased logic in the aggregate — that's Epic 9 (v1.1); this story only documents the subscriber handler pattern
- **Do NOT break** existing tests — the 371+ existing tests must continue passing

### Project Structure Notes

```
docs/
    event-handler-patterns.md              (NEW — handler patterns per event type, PartyErased mandatory handler, dangling reference guidance, tolerant deserialization)
    event-publishing.md                    (NO CHANGE — Story 7.1)
    event-subscribing.md                   (MODIFY — update forward-compat section to link to handler-patterns.md, fix "Epic 9" reference)

tests/Hexalith.Parties.Sample.Tests/
    PartyErasedHandlerPatternTests.cs      (NEW — 4 tests validating PartyErased handler documentation accuracy)
    PartyEventHandlerTests.cs              (NO CHANGE expected)
```

### Previous Story Learnings (from Stories 7.1 and 7.2)

- **Event wire format**: CloudEvents 1.0 wrapping EventStore flat envelope; payload is base64-encoded
- **Idempotency key**: Use `cloudevent.id` (`{correlationId}:{sequenceNumber}`) for deduplication
- **Pub/sub scopes**: The `pubsub.yaml` must include all subscriber app-ids in `scopes`
- **Subscription YAML**: Uses `v2alpha1` API version with `deadLetterTopic` support
- **Test pattern**: `WebApplicationFactory<Program>` with xUnit/Shouldly, no DAPR sidecar needed
- **Forward-compat**: `PartyMerged` (v2) and `PartyErased` (v1.1) placeholders exist in contracts
- **Fully qualified event names**: The publisher emits fully qualified type names; the subscriber must normalize before dispatch
- **Contact/identifier projection**: Use identifier-ID tracking (not increment/decrement) for order-tolerant projections
- **xUnit parallel execution**: Use `[Collection("PartyEventHandler")]` to serialize test classes sharing static state
- **Existing test count**: 371+ tests passing (336 base + 13 from 7.1 + 22 from 7.2 review)

### Git Intelligence

Recent commits follow pattern `feat: Implement Story X.Y - <title>` with PRs from feature branches `feat/story-X-Y-slug`. Stories 7.1 and 7.2 completed sequentially. The codebase is stable on `main`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#GDPR Preparation D6, D7]
- [Source: _bmad-output/planning-artifacts/architecture.md#Event/Pub-Sub Conventions]
- [Source: _bmad-output/planning-artifacts/prd.md#FR38 — Handler patterns for erasure/dangling references]
- [Source: _bmad-output/planning-artifacts/prd.md#Clara's Journey — Lines 289-305]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR27 — Forward-compatible event contracts]
- [Source: samples/Hexalith.Parties.Sample/PartyEventHandler.cs]
- [Source: samples/Hexalith.Parties.Sample/CustomerSummary.cs]
- [Source: docs/event-subscribing.md]
- [Source: docs/event-publishing.md]
- [Source: tests/Hexalith.Parties.Sample.Tests/]
- [Source: _bmad-output/implementation-artifacts/7-1-event-publishing-verification-and-configuration.md]
- [Source: _bmad-output/implementation-artifacts/7-2-subscriber-experience-and-at-least-once-delivery.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

None — clean implementation with no failures.

### Completion Notes List

- Created comprehensive `docs/event-handler-patterns.md` (620+ lines) covering all 14 domain events, 13 rejection events, and the mandatory PartyErased handler pattern
- Each event type includes: when to handle vs. ignore, key properties, C# code examples, personal data awareness notes
- PartyErased section includes prominent mandatory subscription warning (FR38), complete 5-step handler implementation modeled on Clara's invoice journey, handler checklist, and testing strategy
- Dangling reference guidance covers: what happens on erasure, detection patterns (integrity audits, gap checking, scheduled cleanup), cleanup strategies (nullification, placeholders, archival), foreign key management (nullable vs. hard vs. soft references with SQL examples), and Clara's concrete 4-invoice example
- Tolerant deserialization guidance covers: unknown field handling (System.Text.Json defaults), missing optional fields (documented defaults table), additive event evolution, complete tolerant reader pattern code example, and PartyMerged/PartyErased as forward-compatibility examples
- Quick Reference summary table maps all events to handler actions and code references
- Cross-references to sample integration project, event-publishing.md, event-subscribing.md, and broker-specific ordering docs
- Updated `docs/event-subscribing.md` line 283: replaced "Epic 9" forward reference with link to new handler patterns doc
- Created 6 PartyErased/documentation validation tests in `PartyErasedHandlerPatternTests.cs` using NSubstitute mock logger, mock invoice store, and markdown content validation
- Review fixes: corrected fully qualified event-type guidance, aligned wire-format examples with the publishing contract, added explicit broker-template links, and strengthened tests to validate documentation content and audit-log details
- All 46 tests in `Hexalith.Parties.Sample.Tests` pass, 0 warnings, 0 errors

### Change Log

- 2026-03-06: Implemented Story 7.3 — Handler patterns documentation, PartyErased mandatory handler, dangling reference guidance, tolerant deserialization, cross-references, and 4 pattern validation tests
- 2026-03-06: Addressed senior review findings. Updated normalized event-type guidance, aligned wire-format examples with the publisher contract, added broker-template links, strengthened documentation validation tests, and approved the story.

### File List

- `docs/event-handler-patterns.md` (NEW) — Comprehensive handler patterns for all event types including PartyErased, dangling references, and tolerant deserialization
- `docs/event-subscribing.md` (MODIFIED) — Updated PartyErased forward-compat section and corrected fully qualified event dispatch guidance
- `tests/Hexalith.Parties.Sample.Tests/PartyErasedHandlerPatternTests.cs` (NEW) — 6 tests validating PartyErased handler guidance, markdown content, and audit-log requirements
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED) — Story status updated to review
- `_bmad-output/implementation-artifacts/7-3-handler-patterns-documentation-and-dangling-reference-guidance.md` (MODIFIED) — Task checkboxes, dev agent record, file list, change log, status

## Senior Developer Review (AI)

### Reviewer

Jérôme

### Date

2026-03-06

### Outcome

Approved after fixes

### Findings Resolved

1. Updated subscriber and handler-pattern docs to normalize fully qualified event names before switch-based dispatch.
2. Aligned CloudEvents and envelope examples with the actual publisher contract that emits fully qualified event type names.
3. Added explicit links to Kafka, RabbitMQ, and Azure Service Bus deployment templates from the handler guide.
4. Strengthened PartyErased tests to validate markdown content instead of only a copied example implementation.
5. Tightened the audit-log assertion to verify the documented count and `partyId` content.

### Validation

- `dotnet test .\\tests\\Hexalith.Parties.Sample.Tests\\Hexalith.Parties.Sample.Tests.csproj --no-restore --verbosity minimal` → 46 passed
