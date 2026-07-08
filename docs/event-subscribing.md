# Event Subscribing Guide

This guide explains how to subscribe to Hexalith.Parties domain events, build local read models, and handle ordering and delivery guarantees.

For event publishing configuration (topics, dead letters, retry policies), see [event-publishing.md](event-publishing.md).

## Quick Start

1. **Create an HTTP POST endpoint** that accepts CloudEvents at a route like `/events/parties`
2. **Register a DAPR subscription** pointing the topic to your endpoint (see [Subscription Configuration](#subscription-configuration))
3. **Implement idempotent handlers** for the event types you care about
4. **Return 200 OK** for every event, including unknown types and duplicates

See the sample project in [samples/Hexalith.Parties.Sample/PartyEventHandler.cs](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) for a complete working implementation.

## Event Wire Format

Events arrive as CloudEvents 1.0 wrapping an EventStore flat envelope:

```json
{
  "specversion": "1.0",
  "type": "Hexalith.Parties.Contracts.Events.PartyCreated",
  "source": "hexalith-eventstore/tenant-a/parties",
  "id": "{correlationId}:{sequenceNumber}",
  "datacontenttype": "application/json",
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
    "eventTypeName": "Hexalith.Parties.Contracts.Events.PartyCreated",
    "serializationFormat": "json",
    "payload": "<base64-encoded JSON>",
    "extensions": {}
  }
}
```

- **`data.payload`**: Base64-encoded JSON of the event-specific payload
- **`type` / `data.eventTypeName`**: Typically fully qualified .NET event type names; normalize them before dispatch if you switch on short names like `PartyCreated`
- **Serialization**: camelCase property names, ISO 8601 dates, string enums, nulls omitted (`System.Text.Json` defaults)

## Available Event Types

### Domain Events

| Event | Key Properties | Typical Handler Action |
|-------|---------------|----------------------|
| `PartyCreated` | Type, PersonDetails?, OrganizationDetails? | Create local record |
| `PersonDetailsUpdated` | PersonDetails | Update display name |
| `OrganizationDetailsUpdated` | OrganizationDetails | Update org display name |
| `ContactChannelAdded` | ContactChannelId, Type, Value, IsPreferred | Add to local contact cache |
| `ContactChannelUpdated` | ContactChannelId, Type?, Value?, IsPreferred? | Update local contact cache |
| `ContactChannelRemoved` | ContactChannelId | Remove from local cache |
| `PreferredContactChannelChanged` | ContactChannelId | Update preferred flag |
| `IdentifierAdded` | IdentifierId, Type, Value | Add to local identifiers |
| `IdentifierRemoved` | IdentifierId | Remove from local identifiers |
| `PartyDeactivated` | (marker) | Flag local record inactive |
| `PartyReactivated` | (marker) | Re-flag active |
| `PartyDisplayNameDerived` | DisplayName, SortName | Update derived name |
| `IsNaturalPersonChanged` | IsNaturalPerson | Update type flag |
| `PartyMerged` | SurvivorPartyId, MergedPartyId | v2 placeholder -- log and skip |

Plus 13 rejection events (implementing `IRejectionEvent`) published when commands fail.

## Selective Event Handling

Subscribers do not need to handle every event type. Handle only the events relevant to your domain-specific projection:

```csharp
string eventType = NormalizeEventTypeName(envelope.EventTypeName);

switch (eventType)
{
    case "PartyCreated":
        // Build your local record
        break;

    case "PersonDetailsUpdated":
        // Update display name in your projection
        break;

    // Only handle events your projection cares about.
    // All other events fall through to the default case.

    default:
        // Always acknowledge unknown/unhandled events
      logger.LogInformation(
        "Unhandled event type '{EventType}' (normalized as '{NormalizedEventType}')",
        envelope.EventTypeName,
        eventType);
        break;
}

return Results.Ok(); // ALWAYS return 200 OK

  private static string NormalizeEventTypeName(string eventTypeName)
  {
    int separator = eventTypeName.LastIndexOf('.');
    return separator >= 0 ? eventTypeName[(separator + 1)..] : eventTypeName;
  }
```

This pattern allows subscribers to:
- Build focused domain-specific projections (e.g., a customer summary from party data)
- Ignore events irrelevant to their use case
- Remain forward-compatible as new event types are added

## Idempotent Handler Patterns

At-least-once delivery means your handler may receive the same event more than once. Implement idempotency to handle duplicates safely.

### Primary: CloudEvents `id` Deduplication

```csharp
private static readonly ConcurrentDictionary<string, bool> _processedEventIds = new();

// CloudEvents id from DAPR delivery header
string eventId = cloudEventId;

if (!_processedEventIds.TryAdd(eventId, true))
{
    logger.LogInformation("Skipping already-processed event {EventId}", eventId);
    return Results.Ok(); // Always 200 OK for duplicates
}
```

### Fallback: `{correlationId}:{sequenceNumber}`

For local testing without DAPR (direct HTTP POST), fall back to constructing a deduplication key from the envelope:

```csharp
string eventId = string.IsNullOrWhiteSpace(cloudEventId)
    ? $"{envelope.CorrelationId}:{envelope.SequenceNumber}"
    : cloudEventId;
```

### Production Considerations

- **In-memory tracking** (`ConcurrentDictionary`) works for development and testing
- **Production deployments** should use a persistent deduplication store (Redis, database) with TTL-based expiry
- **Thread safety**: Use `ConcurrentDictionary.TryAdd()` for atomic check-and-insert

### Key Rules

- **Always return 200 OK** -- even for duplicates, unknown events, and missing aggregates
- DAPR retries on non-2xx responses, so returning errors causes redelivery loops
- Never throw exceptions from event handlers; log and acknowledge instead
- Store the last processed `sequenceNumber` per aggregate when your read model can receive out-of-order events, and skip or reconcile older sequences rather than overwriting newer state

## Causal Ordering Guarantees Per Broker

Events for the same aggregate are published in causal order (sequence 1, 2, 3...). Whether that order is preserved at delivery depends on the broker and on the deployment configuration.

| Broker | Ordering Guarantee | Required Configuration |
|--------|-------------------|----------------------|
| Redis Streams | Causal ordering within one stream/consumer-group path | Default local setup; avoid parallel handlers that update the same aggregate without a sequence guard |
| RabbitMQ | Causal ordering per queue | Single queue per subscription binding; avoid competing consumers for order-sensitive projections |
| Kafka | Causal ordering per partition | Aggregate-ID-based key routing (see below) |
| Azure Service Bus | Causal ordering per session | Aggregate-ID as session key (see below) |

### Redis Streams (Local Development)

Redis Streams preserves insertion order. Consumer groups process messages in order by default. No additional configuration is needed for the local development setup.

### RabbitMQ

RabbitMQ delivers messages in FIFO order within a single queue. Each subscriber's queue binding receives events in the order they were published.

**Required**: One queue per subscription. Do not use competing consumers on the same queue if ordering matters.

### Kafka

Kafka guarantees ordering within a partition. To ensure causal ordering per aggregate:

**Required**: Configure aggregate-ID-based key routing so all events for the same aggregate land on the same partition.

Apply the broker-specific partition-key metadata supported by your Dapr Kafka component when rendering your environment-specific deployment manifests, and keep the publisher metadata aligned with that aggregate-based routing rule.

Without key routing, events from different aggregates may interleave on the same partition, which is fine, but events from the **same** aggregate could land on different partitions and arrive out of order.

### Azure Service Bus

Azure Service Bus supports sessions for ordered delivery within a session group.

**Required**: Use the aggregate-ID as the session key.

Provision session-enabled topics and subscriptions ahead of time. When rendering production manifests, add the Service Bus session metadata supported by your Dapr component and ensure every event for the same aggregate flows through the same session key.

## Handler Design When Ordering Cannot Be Guaranteed

If your deployment cannot guarantee ordering (e.g., multi-partition Kafka without key routing, or load-balanced subscribers), implement one of these strategies:

### Strategy 1: Sequence Checking

Track the last processed `sequenceNumber` per aggregate. Reject or defer out-of-order events:

```csharp
// Track last processed sequence per aggregate
private static readonly ConcurrentDictionary<string, long> _lastSequence = new();

long lastSeq = _lastSequence.GetOrAdd(aggregateId, 0);
if (envelope.SequenceNumber <= lastSeq)
{
    // Already processed or out of order. Acknowledge and do not overwrite newer state.
    logger.LogInformation("Skipping out-of-order event seq {Seq} for {AggregateId} (last: {Last})",
        envelope.SequenceNumber, aggregateId, lastSeq);
    return Results.Ok();
}
_lastSequence[aggregateId] = envelope.SequenceNumber;
```

For destructive or privacy-relevant events such as future erasure notifications, prefer a durable sequence store and a reconciliation workflow. Do not let an older update event recreate data that a newer cleanup event removed.

### Strategy 2: Order-Tolerant Projection Updates

Use idempotent `set` operations instead of incremental `delta` operations:

```csharp
// BAD: Incremental (order-dependent)
summary.IdentifierCount++;

// GOOD: Idempotent set (order-tolerant)
summary.DisplayName = payload.DisplayName;  // Last-write-wins
summary.IsActive = false;                    // Absolute state, not toggle
```

For counters like `IdentifierCount`, maintain a set of known identifier IDs and derive the count from the set size rather than incrementing/decrementing.

## Subscription Configuration

### Local Development

The sample subscriber is configured via DAPR components in `src/Hexalith.Parties.AppHost/DaprComponents/`:

- **subscription-parties.yaml**: Routes `tenant-a.parties.events` to `/events/parties`, scoped to `sample` app-id
- **pubsub.yaml**: Redis-backed pub/sub on `localhost:6379`

### Production

See [event-publishing.md](event-publishing.md) for production broker configuration requirements.

To subscribe your application:

1. Create an environment-owned Dapr subscription manifest for Parties events.
2. Replace the tenant name in the topic
3. Set your app-id in the `scopes` section
4. Add your app-id to the pubsub component's `scopes` and `subscriptionScopes`

```yaml
apiVersion: dapr.io/v2alpha1
kind: Subscription
metadata:
  name: parties-events-my-tenant
spec:
  pubsubname: pubsub
  topic: "my-tenant.parties.events"
  routes:
    default: /events/parties
  deadLetterTopic: "deadletter.my-tenant.parties.events"
scopes:
  - "my-subscriber-app"
```

## Forward Compatibility

### Tolerant Deserialization

The subscriber should handle:

- **Unknown event types**: Log and return 200 OK. New event types will be added in future versions.
- **Unknown JSON fields**: Use `System.Text.Json` with `PropertyNameCaseInsensitive = true` which ignores unknown fields by default.
- **Missing optional fields**: Nullable properties deserialize to `null` when absent.

### PartyMerged (v2)

`PartyMerged` is a forward-compatibility placeholder. When encountered:

```csharp
case "PartyMerged":
    // v2 placeholder: log and acknowledge.
    // When v2 is released, update this handler to:
    // 1. Look up the survivor party by SurvivorPartyId
    // 2. Merge data from the merged party into the survivor
    // 3. Remove or redirect the merged party's local record
    logger.LogInformation("PartyMerged acknowledged for {PartyId} (v2 not yet implemented)", partyId);
    break;
```

### PartyErased (v1.1 GDPR)

`PartyErased` will be introduced in v1.1 for GDPR crypto-shredding. MVP soft deactivation events such as `PartyDeactivated` are not legal erasure and must not be treated as the subscriber cleanup signal. `PartyErased` carries privacy-safe cleanup metadata only: `partyId`, `tenantId`, `erasedAt`, `erasureStatus`, and `verificationStatus`. Pending or partial internal verification remains visible through companion erasure status surfaces so consumers can distinguish accepted erasure from fully verified erasure. See the [Event Handler Patterns](event-handler-patterns.md#partyerased-handler-mandatory) guide for the mandatory handler implementation, dangling reference guidance, MVP compliance boundary link, and read model cleanup strategies.
