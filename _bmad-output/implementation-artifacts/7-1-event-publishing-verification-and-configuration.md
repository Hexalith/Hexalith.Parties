# Story 7.1: Event Publishing Verification & Configuration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event subscriber developer,
I want party domain events reliably published via DAPR pub/sub with tenant context,
So that my consuming application receives structured, routable events on every party state change.

## Acceptance Criteria

1. **Events published on successful command**: Given any party command that produces domain events (create, update, deactivate, etc.), when the command is processed successfully, then all resulting events are published to DAPR pub/sub (FR34) and events are wrapped in CloudEvents 1.0 envelope (inherited from EventStore).

2. **Tenant context in events**: Given a published party event, when its envelope is inspected, then it includes tenant context for consuming application routing decisions (FR70) and the topic follows the pattern `{tenant}.parties.events`.

3. **Dead letter on delivery failure**: Given a published event that fails delivery, when DAPR retry policy is exhausted, then the event is routed to `deadletter.{tenant}.parties.events` and no events are lost via persist-then-publish with drain recovery (EventStore).

4. **Subscription configuration**: Given the DAPR pub/sub configuration, when reviewed for event publishing, then `subscription-parties.yaml` correctly routes party events and pub/sub component is configured for deployment (Redis Streams for local dev).

5. **Event sequence and format**: Given a party creation followed by contact channel addition, when events are published, then both `PartyCreated` and `ContactChannelAdded` events are published in sequence with camelCase, ISO 8601 dates, string enums (FR73 causal ordering).

6. **Multi-tenant isolation**: Given multiple tenants performing party operations simultaneously, when events are published, then each tenant's events are routed to their tenant-scoped topic with no cross-tenant event leakage.

## Tasks / Subtasks

- [x] Task 1: Create production DAPR deployment configurations (AC: #4)
  - [x] Create `deploy/dapr/pubsub-kafka.yaml` with Kafka pub/sub config
  - [x] Create `deploy/dapr/pubsub-rabbitmq.yaml` with RabbitMQ pub/sub config
  - [x] Create `deploy/dapr/pubsub-servicebus.yaml` with Azure Service Bus pub/sub config
  - [x] Create `deploy/dapr/subscription-parties.yaml` with tenant-scoped routing
  - [x] Create `deploy/dapr/resiliency.yaml` with retry and circuit breaker policies
  - [x] Create `deploy/dapr/accesscontrol.yaml` with cross-tenant pub/sub restrictions
- [x] Task 2: Verify and update local dev DAPR configuration (AC: #4, #2)
  - [x] Audit `src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml` for correct scopes
  - [x] Audit `src/Hexalith.Parties.AppHost/DaprComponents/subscription-parties.yaml` for dead letter routing
  - [x] Verify resiliency.yaml matches architecture specs (exponential backoff, circuit breaker)
  - [x] Ensure accesscontrol.yaml restricts cross-tenant pub/sub access
- [x] Task 3: Create event publishing verification integration tests (AC: #1, #5)
  - [x] Test: EventPublisher publishes an event batch to DAPR pub/sub
  - [x] Test: PartyCreated followed by ContactChannelAdded arrives in sequence
  - [x] Test: Event payload uses camelCase, ISO 8601, string enums
  - [x] Test: CloudEvents 1.0 envelope with correct `type`, `source`, `id` attributes
  - [x] Test: Multi-event batches are published sequentially in order
- [x] Task 4: Create tenant isolation verification tests (AC: #2, #6)
  - [x] Test: Events published to `{tenant}.parties.events` topic (tenant-scoped)
  - [x] Test: Two tenants publishing simultaneously — no cross-tenant event leakage
  - [x] Test: Tenant context present in event envelope (tenantId field)
- [x] Task 5: Create dead letter and resilience verification tests (AC: #3)
  - [x] Test: Failed event delivery routes to `deadletter.{tenant}.parties.events`
  - [x] Test: Publish failure returns a failed result when pub/sub is unavailable
  - [x] Test: A second publish attempt succeeds after a transient failure
- [x] Task 6: Create event publishing configuration documentation (AC: #4)
  - [x] Document production DAPR component configuration per broker (Kafka, RabbitMQ, ServiceBus)
  - [x] Document subscription configuration patterns with tenant-scoped topics
  - [x] Document dead letter topic setup and monitoring
  - [x] Document retry and circuit breaker policies

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

The EventStore submodule (`Hexalith.EventStore/`) already provides the complete event publishing pipeline:

- **`IEventPublisher`** / **`EventPublisher`**: Publishes events via `DaprClient.PublishEventAsync()` with CloudEvents 1.0 wrapping
- **`IDeadLetterPublisher`** / **`DeadLetterPublisher`**: Routes failed events to `deadletter.{tenant}.{domain}.events`
- **`EventPublisherOptions`**: Configures `PubSubName` (default: `"pubsub"`), `DeadLetterTopicPrefix` (default: `"deadletter"`)
- **AggregateActor pipeline**: 5-step persist-then-publish with drain recovery on publish failure
- **CloudEvents metadata**: `cloudevent.type` = event type name, `cloudevent.source` = `hexalith-eventstore/{tenantId}/{domain}`, `cloudevent.id` = `{correlationId}:{sequenceNumber}`
- **Existing integration tests** in `Hexalith.EventStore.Server.Tests/Actors/EventPublicationIntegrationTests.cs`:
  - `ProcessCommand_Success_TransitionsEventStored_EventsPublished_Completed`
  - `ProcessCommand_PublishFails_TransitionsToPublishFailed`
  - `ProcessCommand_Rejection_PublishesRejectionEvents_ThenCompleted`

**Key files in EventStore submodule (READ-ONLY — do not modify):**
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`

### Local Dev DAPR Configuration (Already Exists)

**`src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml`**:
- Type: `pubsub.redis`, Redis host: `localhost:6379`
- Scopes: `commandapi`, `sample`

**`src/Hexalith.Parties.AppHost/DaprComponents/subscription-parties.yaml`**:
- API version: `v2alpha1`, topic: `tenant-a.parties.events`
- Routes to: `/events/parties`, dead letter: `deadletter.tenant-a.parties.events`
- Scopes: `sample` (the sample subscriber exposes `/events/parties`)

**`src/Hexalith.Parties.AppHost/DaprComponents/resiliency.yaml`**:
- Outbound retry: exponential, maxInterval 10s, maxRetries 3
- Inbound retry: exponential, maxInterval 30s, maxRetries 10
- Circuit breaker: trip after 5 consecutive failures, timeout 30s

### Production Deployment Configs

The `deploy/dapr/` directory now contains these production-oriented configs:
- `pubsub-kafka.yaml` — Kafka pub/sub component
- `pubsub-rabbitmq.yaml` — RabbitMQ pub/sub component
- `pubsub-servicebus.yaml` — Azure Service Bus pub/sub component
- `subscription-parties.yaml` — Production subscription with tenant-scoped routing
- `resiliency.yaml` — Production retry and circuit breaker policies
- `accesscontrol.yaml` — Cross-tenant pub/sub access restrictions

**Topic naming**: `{tenant}.parties.events` (e.g., `acme.parties.events`)
**Dead letter**: `deadletter.{tenant}.parties.events`

Each production config is a valid DAPR YAML asset with documented configuration parameters for operators. Component files use environment variables for deployment-specific connection details. The subscription file is a concrete single-tenant example that operators copy and render per tenant before deployment.

### Event Types (All 14 Success Events to Verify)

All events implement `IEventPayload` in `src/Hexalith.Parties.Contracts/Events/`:

| Event | Key Properties |
|-------|---------------|
| `PartyCreated` | Type, PersonDetails?, OrganizationDetails? |
| `PartyDeactivated` | (marker event, no properties) |
| `PartyReactivated` | (marker event, no properties) |
| `PartyDisplayNameDerived` | DisplayName, SortName |
| `PartyMerged` | SurvivorPartyId, MergedPartyId (v2 placeholder) |
| `ContactChannelAdded` | ContactChannelId, Type, Value, IsPreferred |
| `ContactChannelUpdated` | ContactChannelId, Type?, Value?, IsPreferred? |
| `ContactChannelRemoved` | ContactChannelId |
| `PreferredContactChannelChanged` | ContactChannelId |
| `IdentifierAdded` | IdentifierId, Type, Value |
| `IdentifierRemoved` | IdentifierId |
| `OrganizationDetailsUpdated` | OrganizationDetails |
| `PersonDetailsUpdated` | PersonDetails |
| `IsNaturalPersonChanged` | IsNaturalPerson |

Plus 13 rejection events implementing `IRejectionEvent` — these ARE also published to pub/sub per Decision D3.

### Event Wire Format

Events arrive as CloudEvents 1.0 wrapping a flat EventStore Server envelope:
```json
{
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
```

**Serialization conventions**: camelCase, ISO 8601 dates, string enums, omit nulls (`System.Text.Json` defaults).

### Testing Strategy

**Integration tests should go in `tests/Hexalith.Parties.IntegrationTests/`** (Tier 3 — full Aspire topology with DAPR + Redis).

Test approach:
1. Use `WebApplicationFactory` or Aspire test host to start CommandApi with DAPR sidecar
2. Send commands via REST API (authenticated with test JWT token)
3. Subscribe to DAPR pub/sub topic to receive events
4. Assert on event content, ordering, tenant isolation

**If Tier 3 tests are too complex for this story** (require full Docker infrastructure), write Tier 2 tests instead:
- Mock `IEventPublisher` and verify it is called correctly
- Test `EventPublisher` with mock `DaprClient`
- Verify CloudEvents metadata construction
- Verify topic naming with tenant context

**Existing test count**: 336 tests passing (as of story 6.3 completion).

### Previous Story Learnings (from Story 6.3)

- **DAPR event wire format**: Events arrive as CloudEvents wrapping EventStore flat envelope; payload is base64-encoded
- **Idempotency**: Use `cloudevent.id` (`{correlationId}:{sequenceNumber}`) for deduplication
- **Pub/sub scopes**: The `pubsub.yaml` must include all subscriber app-ids in `scopes` for subscriptions to be active
- **Subscription YAML**: Uses `v2alpha1` API version with `deadLetterTopic` support
- **Test pattern**: WebApplicationFactory with xUnit/Shouldly works well for event handler tests
- **Forward-compat**: `PartyMerged` (v2) and `PartyErased` (v1.1) placeholders exist in contracts

### Critical Anti-Patterns to Avoid

- **Do NOT modify** the EventStore submodule. It is read-only. All event publishing logic lives there.
- **Do NOT create** custom event publishing code in Hexalith.Parties — use the existing `IEventPublisher` from EventStore.
- **Do NOT leave unresolved placeholder tokens** in deployed subscriptions. Render one concrete subscription per tenant before applying it.
- **Do NOT add** DAPR client packages to Contracts or Client projects. Only Server/CommandApi reference DAPR.
- **Do NOT log** event payloads (Security Rule #5). Structured logging with correlation IDs only.
- **Do NOT create** V2 events or modify existing event records. Additive optional properties only (NFR27).
- **Do NOT break** existing tests. The 336 existing tests must continue passing.

### Project Structure Notes

```
deploy/                                    (NEW directory)
  dapr/                                    (NEW)
    pubsub-kafka.yaml                      (new - Kafka pub/sub template)
    pubsub-rabbitmq.yaml                   (new - RabbitMQ pub/sub template)
    pubsub-servicebus.yaml                 (new - Azure Service Bus template)
    subscription-parties.yaml              (new - production subscription config)
    resiliency.yaml                        (new - production resiliency policies)
    accesscontrol.yaml                     (new - cross-tenant access control)

src/Hexalith.Parties.AppHost/DaprComponents/
    pubsub.yaml                            (review/update if needed)
    subscription-parties.yaml              (review/update if needed)
    resiliency.yaml                        (review/update if needed)
    accesscontrol.yaml                     (review/update if needed)

tests/Hexalith.Parties.IntegrationTests/   (add new tests)
    Events/                                (new folder)
      EventPublishingVerificationTests.cs  (new)
      TenantIsolationTests.cs             (new)
      DeadLetterRoutingTests.cs           (new)

docs/
    event-publishing.md                    (new - configuration documentation)
```

### Ordering Guarantees Per Broker (FR73)

- **Redis Streams**: Causal ordering within consumer group (local dev ✓)
- **RabbitMQ**: Causal ordering per queue (production option)
- **Kafka**: Causal ordering per partition with aggregate-ID-based key routing (production option)
- **Azure Service Bus**: Causal ordering per session with aggregate-ID session key

Document these ordering guarantees in `docs/event-publishing.md` so operators know what to configure.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.1]
- [Source: _bmad-output/planning-artifacts/architecture.md#Event/Pub-Sub Conventions]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries]
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules]
- [Source: _bmad-output/planning-artifacts/prd.md#FR34, FR35, FR63, FR70, FR73]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR23, NFR24, NFR27]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/EventPublisher.cs]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs]
- [Source: src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml]
- [Source: src/Hexalith.Parties.AppHost/DaprComponents/subscription-parties.yaml]
- [Source: samples/Hexalith.Parties.Sample/PartyEventHandler.cs]
- [Source: _bmad-output/implementation-artifacts/6-3-sample-integration-project.md]

## Change Log

- 2026-03-06: Story 7.1 implemented — production DAPR configs, Tier 2 integration tests, event publishing documentation
- 2026-03-06: Code review fixes — corrected local subscription scope, made production subscription example deployable, and aligned publisher test claims with actual coverage

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Initial test run: 1 failure in `PublishEvents_EventPayload_UsesCamelCaseIso8601StringEnumsAsync` — Shouldly `ShouldNotContain` uses case-insensitive comparison by default. Fixed by adding `Case.Sensitive` parameter.

### Completion Notes List

- **Task 1:** Created 6 production DAPR deployment config templates in `deploy/dapr/` adapted from EventStore submodule templates for the Parties domain. Each config uses environment variables for secrets (never hardcoded), three-layer scoping architecture (component, publishing, subscription), and documented operator instructions.
- **Task 2:** Audited all 4 local dev DAPR configs and corrected the subscription scope to target the sample subscriber app-id (`sample`), which is the local endpoint exposing `/events/parties`. Pubsub scopes remain `commandapi` + `sample`, resiliency uses exponential backoff + circuit breakers, and access control uses allow-default appropriate for self-hosted mode.
- **Task 3:** Created 5 Tier 2 publisher contract tests in `EventPublishingVerificationTests.cs` verifying the `EventPublisher` contract: publish calls to DAPR pub/sub, event ordering within a published batch, camelCase/ISO 8601 payload format, CloudEvents 1.0 envelope attributes (`type`/`source`/`id`), and sequential publication of multi-event batches.
- **Task 4:** Created 3 Tier 2 publisher contract tests in `TenantIsolationTests.cs` verifying tenant-scoped topic naming, concurrent tenant-specific publication to separate topics, and tenant context presence in event envelopes.
- **Task 5:** Created 3 Tier 2 publisher/dead-letter contract tests in `DeadLetterRoutingTests.cs` verifying dead letter topic routing (`deadletter.{tenant}.parties.events`), publish failure result when pub/sub is unavailable, and success on a second attempt after a transient failure.
- **Task 6:** Created comprehensive `docs/event-publishing.md` documenting production broker configuration (Kafka, RabbitMQ, Azure Service Bus), subscription patterns with tenant-scoped topics, dead letter setup and monitoring, retry/circuit breaker policies with effective retry counts per broker, and local development configuration.
- **Validation:** Ran `dotnet test tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj --no-restore` after the review fixes; 13 tests passed, 0 failed.

### File List

- `deploy/dapr/pubsub-kafka.yaml` (new)
- `deploy/dapr/pubsub-rabbitmq.yaml` (new)
- `deploy/dapr/pubsub-servicebus.yaml` (new)
- `deploy/dapr/subscription-parties.yaml` (new)
- `deploy/dapr/resiliency.yaml` (new)
- `deploy/dapr/accesscontrol.yaml` (new)
- `tests/Hexalith.Parties.IntegrationTests/Events/EventPublishingVerificationTests.cs` (new)
- `tests/Hexalith.Parties.IntegrationTests/Events/TenantIsolationTests.cs` (new)
- `tests/Hexalith.Parties.IntegrationTests/Events/DeadLetterRoutingTests.cs` (new)
- `docs/event-publishing.md` (new)
- `src/Hexalith.Parties.AppHost/DaprComponents/subscription-parties.yaml` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)
- `_bmad-output/implementation-artifacts/7-1-event-publishing-verification-and-configuration.md` (modified)

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot

### Review Date

2026-03-06

### Outcome

Approved after fixes.

### Findings Resolved

- Corrected the local development subscription scope so the subscriber app-id matches the actual `/events/parties` endpoint exposed by the sample application.
- Replaced the non-deployable `{tenant}` literal in the production subscription example with a concrete single-tenant example that operators copy and render per tenant before deployment.
- Updated publisher-focused tests and completion notes to reflect actual coverage boundaries instead of overstating full command-pipeline or drain-recovery verification.
