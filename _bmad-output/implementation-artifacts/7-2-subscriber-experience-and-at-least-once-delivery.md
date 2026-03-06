# Story 7.2: Subscriber Experience & At-Least-Once Delivery

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event subscriber developer,
I want verified at-least-once delivery with clear ordering guarantees and idempotent handler patterns,
So that my consuming application can build reliable read models from party events.

## Acceptance Criteria

1. **At-least-once delivery guaranteed**: Given a consuming application subscribed to party events, when events are published, then at-least-once delivery is guaranteed via DAPR pub/sub (FR63, NFR23) and consuming applications must implement idempotent handlers (duplicate events possible).

2. **Causal ordering per broker documented**: Given the event subscription documentation, when reviewed for ordering guarantees, then causal ordering guarantees per aggregate are documented per broker (FR73): Redis Streams (within consumer group), RabbitMQ (per queue), Kafka (per partition with key-based routing configured), and handler design requirements are specified if ordering cannot be guaranteed (sequence-checking, order-tolerant projection updates).

3. **Selective event handling**: Given a consuming application building a local read model, when it subscribes to party events, then it can selectively handle events (e.g., only `PersonDetailsUpdated`, not all events) (FR35) and it can build domain-specific projections (e.g., customer summary from party data).

4. **PartyMerged forward-compat tolerant deserialization**: Given the `PartyMerged` event type in Contracts, when a consuming application encounters it in the event stream, then tolerant deserialization handles it gracefully even before v2 implementation (FR37) and consuming applications can register handlers for it proactively.

5. **Unknown event type handling**: Given a consuming application's event handler, when it receives an unknown event type (future additive events), then tolerant deserialization ignores unknown fields and handles missing optional fields and the handler continues processing without error (NFR27).

6. **10-event delivery verification**: Given an integration test with a subscriber, when 10 party events are published in sequence for the same aggregate, then the subscriber receives all 10 events and delivery is confirmed (no lost events).

## Tasks / Subtasks

- [x] Task 1: Enhance sample subscriber with comprehensive event handling (AC: #3, #4, #5)
  - [x] Add handlers for all remaining event types: `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, `ContactChannelUpdated`, `ContactChannelRemoved`, `PreferredContactChannelChanged`, `IdentifierAdded`, `IdentifierRemoved`, `PartyReactivated`, `PartyDisplayNameDerived`, `IsNaturalPersonChanged`
  - [x] Add `PartyMerged` handler (log and skip gracefully with forward-compat comment)
  - [x] Add explicit unknown event type fallback that logs and returns 200 OK
  - [x] Enhance `CustomerSummary` model with additional fields (`Phone`, `IdentifierCount`, `LastUpdated`) to demonstrate domain-specific projection building
  - [x] Add selective event handling documentation comments showing how subscribers can filter events

- [x] Task 2: Create subscriber-side delivery verification tests (AC: #1, #6)
  - [x] Test: Publish 10 sequential events for same aggregate, verify subscriber receives all 10 in order
  - [x] Test: Duplicate event delivery (same CloudEvents `id`) is handled idempotently — second delivery returns 200 OK, state unchanged
  - [x] Test: Subscriber processes events from multiple aggregates without cross-contamination
  - [x] Test: Subscriber handles burst of events (concurrent delivery) safely via `ConcurrentDictionary`

- [x] Task 3: Create tolerant deserialization and forward-compat tests (AC: #4, #5)
  - [x] Test: `PartyMerged` event deserialized and handled gracefully (logged, not errored)
  - [x] Test: Unknown event type (e.g., `FutureEventType`) returns 200 OK, handler continues
  - [x] Test: Event with unknown additional JSON fields deserializes without error (tolerant reader)
  - [x] Test: Event with missing optional fields deserializes with defaults

- [x] Task 4: Create subscriber experience and ordering documentation (AC: #2)
  - [x] Create `docs/event-subscribing.md` with subscriber setup guide
  - [x] Document causal ordering guarantees per broker: Redis Streams (consumer group), RabbitMQ (per queue), Kafka (per partition with key routing), Azure Service Bus (per session with aggregate-ID session key)
  - [x] Document required broker configuration for ordering per deployment target
  - [x] Document idempotent handler patterns: CloudEvents `id` deduplication, `{correlationId}:{sequenceNumber}` fallback
  - [x] Document handler design requirements when ordering cannot be guaranteed: sequence-checking, order-tolerant projection updates
  - [x] Document selective event handling patterns and event type filtering
  - [x] Cross-reference `docs/event-publishing.md` (Story 7.1) and sample project

- [x] Task 5: Create selective event handling tests (AC: #3)
  - [x] Test: Subscriber handles `PersonDetailsUpdated` and updates `CustomerSummary.DisplayName`
  - [x] Test: Subscriber handles `IdentifierAdded` and increments `CustomerSummary.IdentifierCount`
  - [x] Test: Subscriber ignores events it doesn't handle (returns 200 OK, no state mutation)
  - [x] Test: Subscriber builds domain-specific projection from a sequence of party lifecycle events

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

**Event Publishing Pipeline (EventStore submodule — READ-ONLY):**
- `IEventPublisher` / `EventPublisher`: Publishes events via `DaprClient.PublishEventAsync()` with CloudEvents 1.0 wrapping
- `IDeadLetterPublisher` / `DeadLetterPublisher`: Routes failed events to `deadletter.{tenant}.{domain}.events`
- `EventPublisherOptions`: Configures `PubSubName` (default: `"pubsub"`), `DeadLetterTopicPrefix` (default: `"deadletter"`)
- AggregateActor pipeline: 5-step persist-then-publish with drain recovery on publish failure
- CloudEvents metadata: `cloudevent.type` = event type name, `cloudevent.source` = `hexalith-eventstore/{tenantId}/{domain}`, `cloudevent.id` = `{correlationId}:{sequenceNumber}`

**Sample Subscriber (from Story 6.3 — EXTEND, do not rewrite):**
- `samples/Hexalith.Parties.Sample/PartyEventHandler.cs` — HTTP POST endpoint at `/events/parties`
- Already implements:
  - Idempotent deduplication via `ConcurrentDictionary<string, bool>` keyed on CloudEvents `id`
  - Fallback to `{correlationId}:{sequenceNumber}` for local testing
  - Tolerant deserialization (CloudEvents `data` wrapper + direct EventEnvelope fallback)
  - Handles: `PartyCreated`, `ContactChannelAdded`, `PartyDeactivated`
  - Unknown event types: logs and returns 200 OK
  - Thread-safe `CustomerSummary` in-memory store via `ConcurrentDictionary`
- `samples/Hexalith.Parties.Sample/CustomerSummary.cs` — Read model: `Id`, `DisplayName`, `Email`, `IsActive`
- `samples/Hexalith.Parties.Sample/Program.cs` — `app.MapPartyEventEndpoint()` registers the endpoint

**Sample Tests (from Story 6.3 — EXTEND, do not rewrite):**
- `tests/Hexalith.Parties.Sample.Tests/PartyEventHandlerTests.cs` — 8 tests:
  - `HandlePartyCreated_ShouldCreateCustomerSummaryAsync`
  - `HandlePartyCreated_Organization_ShouldUseLegalNameAsync`
  - `HandleContactChannelAdded_ShouldUpdateEmailAsync`
  - `HandlePartyDeactivated_ShouldMarkInactiveAsync`
  - `HandlePartyDeactivated_WhenAlreadyInactive_ShouldBeIdempotentAsync`
  - `HandlePartyDeactivated_WhenPartyNotFound_ShouldReturnOkAsync`
  - `HandleDuplicateEvent_ShouldBeIdempotentAsync`
  - `HandlePartyCreated_WithDifferentCloudEventId_ShouldProcessAgainAsync`
- `tests/Hexalith.Parties.Sample.Tests/CustomerSummaryStoreTests.cs` — concurrent safety tests

**Story 7.1 Integration Tests (EXTEND, do not rewrite):**
- `tests/Hexalith.Parties.IntegrationTests/Events/EventPublishingVerificationTests.cs` — publisher-side tests
- `tests/Hexalith.Parties.IntegrationTests/Events/TenantIsolationTests.cs` — multi-tenant topic routing
- `tests/Hexalith.Parties.IntegrationTests/Events/DeadLetterRoutingTests.cs` — failure routing

**Story 7.1 Documentation:**
- `docs/event-publishing.md` — production broker config, subscription patterns, dead letter, retry/circuit breaker policies, ordering guarantees per broker

**DAPR Configuration (already exists):**
- `src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml` — Redis, scopes: `commandapi`, `sample`
- `src/Hexalith.Parties.AppHost/DaprComponents/subscription-parties.yaml` — `tenant-a.parties.events` -> `/events/parties`, dead letter, scope: `sample`
- `src/Hexalith.Parties.AppHost/DaprComponents/resiliency.yaml` — exponential backoff + circuit breaker
- `deploy/dapr/` — production configs for Kafka, RabbitMQ, Azure Service Bus

### Event Wire Format (Reference for Test Construction)

Events arrive as CloudEvents 1.0 wrapping EventStore flat envelope:
```json
{
  "specversion": "1.0",
  "type": "PartyCreated",
  "source": "hexalith-eventstore/tenant-a/parties",
  "id": "{correlationId}:{sequenceNumber}",
  "data": {
    "aggregateId": "tenant-a:parties:550e8400-...",
    "tenantId": "tenant-a",
    "domain": "parties",
    "sequenceNumber": 1,
    "timestamp": "2026-03-06T10:30:00+00:00",
    "correlationId": "...",
    "causationId": "...",
    "userId": "user@example.com",
    "domainServiceVersion": "1.0.0",
    "eventTypeName": "PartyCreated",
    "serializationFormat": "json",
    "payload": "<base64-encoded JSON>",
    "extensions": {}
  }
}
```

**Serialization conventions**: camelCase, ISO 8601 dates, string enums, omit nulls (`System.Text.Json` defaults).

### All 14 Success Event Types (from Contracts/Events/)

| Event | Key Properties | Handler Notes |
|-------|---------------|---------------|
| `PartyCreated` | Type, PersonDetails?, OrganizationDetails? | Create local record |
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

Plus 13 rejection events (implementing `IRejectionEvent`) — also published per Decision D3.

### Ordering Guarantees Per Broker (FR73) — Document in `docs/event-subscribing.md`

| Broker | Ordering Guarantee | Required Configuration |
|--------|-------------------|----------------------|
| Redis Streams | Causal ordering within consumer group | Default (local dev) |
| RabbitMQ | Causal ordering per queue | Single queue per subscription |
| Kafka | Causal ordering per partition | Aggregate-ID-based key routing |
| Azure Service Bus | Causal ordering per session | Aggregate-ID as session key |

**When ordering cannot be guaranteed**: Handler must implement either sequence-checking (track last processed `sequenceNumber` per aggregate) or order-tolerant projection updates (idempotent `set` operations instead of incremental `delta` operations).

### Idempotent Handler Pattern (Already Established)

```csharp
// Primary: CloudEvents id header
// Fallback: {correlationId}:{sequenceNumber}
if (!_processedEventIds.TryAdd(eventId, true))
{
    logger.LogInformation("Skipping already-processed event {EventId}", eventId);
    return Results.Ok();  // Always return 200 OK for duplicates
}
```

**Key rules:**
- Always return 200 OK (even for duplicates, unknown events, missing aggregates)
- Use `ConcurrentDictionary` for thread-safe deduplication
- In production, replace in-memory tracking with persistent deduplication store
- DAPR retries on non-2xx responses — returning errors causes redelivery loops

### Test Construction Pattern (from Existing Sample Tests)

Tests POST CloudEvents-wrapped payloads to `/events/parties`:

```csharp
// 1. Create EventEnvelope with base64-encoded payload
var payload = new { type = "Person", personDetails = new { ... } };
var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, jsonOptions)));
var envelope = new { aggregateId = "tenant-a:parties:...", eventTypeName = "PartyCreated", payload = payloadBase64, ... };

// 2. Wrap in CloudEvents
var cloudEvent = new { specversion = "1.0", type = "PartyCreated", source = "...", id = "...", data = envelope };

// 3. POST to endpoint
var response = await client.PostAsJsonAsync("/events/parties", cloudEvent, jsonOptions);
response.StatusCode.ShouldBe(HttpStatusCode.OK);
```

**Use `WebApplicationFactory<Program>` for in-process testing** — no DAPR sidecar needed.

### Critical Anti-Patterns to Avoid

- **Do NOT modify** the EventStore submodule (read-only)
- **Do NOT rewrite** the existing `PartyEventHandler.cs` — extend it with additional handlers
- **Do NOT rewrite** existing tests — add new test methods alongside existing ones
- **Do NOT add** DAPR client packages to Sample project — it uses pure HTTP endpoints
- **Do NOT log** event payloads (Security Rule #5) — structured logging with correlation IDs only
- **Do NOT create** custom retry logic in subscriber — DAPR handles retries; return 200 OK always
- **Do NOT break** existing tests — the 349+ existing tests (336 + 13 from Story 7.1) must continue passing
- **Do NOT create** a separate subscriber abstraction framework — the HTTP POST endpoint pattern is the established convention

### Project Structure Notes

```
samples/Hexalith.Parties.Sample/
    PartyEventHandler.cs               (MODIFY — add handlers for remaining event types)
    CustomerSummary.cs                  (MODIFY — add Phone, IdentifierCount, LastUpdated fields)
    Program.cs                          (NO CHANGE expected)

tests/Hexalith.Parties.Sample.Tests/
    PartyEventHandlerTests.cs           (MODIFY — add tests for new handlers, delivery verification, tolerant deserialization)
    CustomerSummaryStoreTests.cs        (NO CHANGE expected)

tests/Hexalith.Parties.IntegrationTests/Events/
    SubscriberDeliveryVerificationTests.cs   (NEW — 10-event delivery test, burst handling)

docs/
    event-publishing.md                 (NO CHANGE — Story 7.1)
    event-subscribing.md                (NEW — subscriber setup, ordering, idempotency, selective handling)
```

### Previous Story Learnings (from Story 7.1)

- **DAPR event wire format**: Events arrive as CloudEvents wrapping EventStore flat envelope; payload is base64-encoded
- **Idempotency key**: Use `cloudevent.id` (`{correlationId}:{sequenceNumber}`) for deduplication
- **Pub/sub scopes**: The `pubsub.yaml` must include all subscriber app-ids in `scopes` for subscriptions to be active
- **Subscription YAML**: Uses `v2alpha1` API version with `deadLetterTopic` support
- **Test pattern**: `WebApplicationFactory` with xUnit/Shouldly works well for event handler tests
- **Forward-compat**: `PartyMerged` (v2) and `PartyErased` (v1.1) placeholders exist in contracts
- **Production subscription**: Single-tenant example that operators copy and render per tenant
- **Local subscription scope**: Must target the subscriber app-id (`sample`), not the publisher
- **Tier 2 mocked DaprClient**: NSubstitute mocks work well for publisher contract tests
- **Existing test count**: 349+ tests passing (336 base + 13 from Story 7.1)

### Git Intelligence

Recent commits follow pattern `feat: Implement Story X.Y - <title>` with PRs from feature branches. Stories 6.1 through 7.1 have been completed sequentially. The codebase is stable on `main`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#Event/Pub-Sub Conventions]
- [Source: _bmad-output/planning-artifacts/architecture.md#Projection Architecture]
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules]
- [Source: _bmad-output/planning-artifacts/prd.md#FR34, FR35, FR37, FR63, FR70, FR73]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR23, NFR24, NFR27]
- [Source: samples/Hexalith.Parties.Sample/PartyEventHandler.cs]
- [Source: samples/Hexalith.Parties.Sample/CustomerSummary.cs]
- [Source: tests/Hexalith.Parties.Sample.Tests/PartyEventHandlerTests.cs]
- [Source: tests/Hexalith.Parties.IntegrationTests/Events/]
- [Source: docs/event-publishing.md]
- [Source: _bmad-output/implementation-artifacts/7-1-event-publishing-verification-and-configuration.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Fixed xUnit parallel test execution issue by introducing `[Collection("PartyEventHandler")]` to serialize test classes sharing static state (`_processedEventIds` and `CustomerSummaryStore`)
- Added `ClearProcessedEventIds()` to `PartyEventHandler` for test cleanup
- Subscriber delivery verification tests placed in `Sample.Tests` instead of `IntegrationTests` to avoid `Program` class ambiguity (both CommandApi and Sample define top-level `Program`)

### Completion Notes List

- **Task 1**: Extended `PartyEventHandler.cs` with handlers for all 14 event types (10 new + 3 existing + PartyMerged). Enhanced `CustomerSummary` with `Phone`, `IdentifierCount`, `LastUpdated` fields. Added selective event handling comments and explicit unknown event fallback.
- **Task 2**: Created `SubscriberDeliveryVerificationTests.cs` with 4 tests: 10-event sequential delivery, duplicate idempotency, multi-aggregate isolation, concurrent burst safety.
- **Task 3**: Created `TolerantDeserializationTests.cs` with 4 tests: PartyMerged graceful handling, unknown FutureEventType, unknown JSON fields (tolerant reader), missing optional fields with defaults.
- **Task 4**: Created `docs/event-subscribing.md` covering subscriber setup, event wire format, all 14 event types, selective handling patterns, idempotent handler patterns (CloudEvents id + correlationId:sequenceNumber fallback), causal ordering guarantees per broker (Redis/RabbitMQ/Kafka/Azure Service Bus), broker configuration for ordering, handler design for unordered delivery (sequence-checking and order-tolerant projections), subscription configuration, and forward-compatibility guidance (PartyMerged v2, PartyErased v1.1).
- **Task 5**: Created `SelectiveEventHandlingTests.cs` with 4 tests: PersonDetailsUpdated updates DisplayName, IdentifierAdded increments count, unhandled events return 200 OK with no state mutation, full party lifecycle projection building.
- **Review fixes**: Normalized fully qualified event names before dispatch, made contact-channel and identifier projection state resilient to removals/replays, added a publisher-to-subscriber contract test using real `EventPublisher` CloudEvents metadata, and aligned ordering documentation with the checked-in deployment templates.

### File List

- `samples/Hexalith.Parties.Sample/CustomerSummary.cs` (MODIFIED) — Added Phone, IdentifierCount, LastUpdated fields
- `samples/Hexalith.Parties.Sample/PartyEventHandler.cs` (MODIFIED) — Added handlers for 10 new event types, PartyMerged forward-compat handler, explicit unknown event fallback, ClearProcessedEventIds(), new payload record types, selective handling comments, LastUpdated tracking
- `tests/Hexalith.Parties.Sample.Tests/PartyEventHandlerTests.cs` (MODIFIED) — Added 13 new handler tests, collection serialization
- `tests/Hexalith.Parties.Sample.Tests/PartyEventHandlerCollection.cs` (NEW) — xUnit collection definition for serialized test execution
- `tests/Hexalith.Parties.Sample.Tests/SubscriberDeliveryVerificationTests.cs` (NEW) — 4 delivery verification tests (10-event, duplicate, multi-aggregate, burst)
- `tests/Hexalith.Parties.Sample.Tests/TolerantDeserializationTests.cs` (NEW) — 4 tolerant deserialization tests (PartyMerged, unknown type, unknown fields, missing fields)
- `tests/Hexalith.Parties.Sample.Tests/SelectiveEventHandlingTests.cs` (NEW) — 4 selective event handling tests
- `tests/Hexalith.Parties.Sample.Tests/PublisherToSubscriberContractTests.cs` (NEW) — Verifies publisher-generated CloudEvents with fully qualified event type names are accepted end-to-end by the sample subscriber
- `tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj` (MODIFIED) — Added EventStore publisher test dependencies for contract validation
- `docs/event-subscribing.md` (NEW) — Subscriber experience documentation with ordering guarantees, idempotency patterns, selective handling
- `docs/event-publishing.md` (MODIFIED) — Clarified CloudEvents type naming and deployment-specific ordering configuration responsibilities
- `deploy/dapr/pubsub-kafka.yaml` (MODIFIED) — Clarified environment-specific partition routing requirement for Kafka ordering
- `deploy/dapr/pubsub-servicebus.yaml` (MODIFIED) — Clarified session-enabled deployment requirement for Azure Service Bus ordering
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED) — Synced story completion status after review

### Change Log

- 2026-03-06: Implemented Story 7.2 — Subscriber Experience & At-Least-Once Delivery. Added comprehensive event handlers for all 14 party event types, 25 new tests (38 total in Sample.Tests, 371 total in solution), subscriber documentation with ordering guarantees per broker.
- 2026-03-06: Addressed senior review findings. Normalized fully qualified event names, fixed stale contact/identifier projection behavior, added publisher-to-subscriber contract coverage, aligned deployment docs/templates, and advanced story status to done.

## Senior Developer Review (AI)

### Reviewer

Jérôme

### Date

2026-03-06

### Outcome

Approved after fixes

### Findings Resolved

1. Normalized fully qualified event names before the subscriber dispatch switch so real EventStore envelopes map to the intended handlers.
2. Replaced direct HTTP-only confidence with a publisher-to-subscriber contract test that replays `EventPublisher`-generated CloudEvents into the sample endpoint.
3. Made `ContactChannelRemoved` and `PreferredContactChannelChanged` update stored channel state and recompute projected `Email`/`Phone` values.
4. Replaced raw identifier increment/decrement behavior with identifier-ID tracking so `IdentifierCount` is derived from known identifiers.
5. Aligned ordering guidance with the checked-in deployment templates by documenting the required environment-specific routing/session configuration.

### Validation

- `dotnet test .\tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --verbosity minimal` → 40 passed
