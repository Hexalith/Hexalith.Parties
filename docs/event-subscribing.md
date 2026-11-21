# Event Subscribing Guide

This guide explains how to subscribe to Hexalith.Parties domain events, build local read models, and handle ordering and delivery guarantees.

For event publishing configuration (topics, dead letters, retry policies), see [event-publishing.md](event-publishing.md).

## Quick Start

1. **Create an HTTP POST endpoint** that accepts CloudEvents at a route like `/events/parties`
2. **Register a DAPR subscription** pointing the topic to your endpoint (see [Subscription Configuration](#subscription-configuration))
3. **Implement idempotent handlers** for the event types you care about
4. **Acknowledge after durable completion or handoff.** Request redelivery if a transient failure occurs first. [Handler rules](event-handler-patterns.md#key-handler-rules)

The local sample demonstrates event dispatch with in-memory state. [Sample handler](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs)

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

Subscribers handle events relevant to their projection. If they store party references, they also handle `PartyErased`. The example shows dispatch after event-name normalization. [Cleanup obligations](event-handler-patterns.md#partyerased-handler-mandatory)

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

    case "PartyErased":
        // Complete required cleanup or durably hand it to the retry owner.
        break;

    default:
        // Only events explicitly outside this subscriber's obligations reach here.
      logger.LogInformation(
        "Unhandled event type '{EventType}' (normalized as '{NormalizedEventType}')",
        envelope.EventTypeName,
        eventType);
        break;
}

// Completed or durably handed-off work can be acknowledged.
// Return a retry outcome if a transient failure prevents either.

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

Use the CloudEvents source and ID to recognize a redelivery. Acknowledge a duplicate when its durable record confirms completed work or a handoff that the retry owner can resume after restart. Concurrent receipt of the same ID is insufficient.

### Fallback: `{correlationId}:{sequenceNumber}`

For local testing without DAPR (direct HTTP POST), fall back to constructing a deduplication key from the envelope:

```csharp
string eventId = string.IsNullOrWhiteSpace(cloudEventId)
    ? $"{envelope.CorrelationId}:{envelope.SequenceNumber}"
    : cloudEventId;
```

### Production Considerations

- **In-memory tracking** (`ConcurrentDictionary`) works for development and testing
- **Production deployments** commit the deduplication record with the effect, or durably hand the operation and its identity to a named retry owner. Retain enough evidence to cover redelivery and reconciliation.

### Key Rules

- Return success after durable completion, confirmed duplicate completion, durable handoff or an explicit decision that the event falls outside this subscriber’s obligations.
- Return a retryable failure or Dapr’s explicit `RETRY` result when the handler cannot confirm one of those outcomes. The retry owner reconciles exhausted deliveries. [Dead-letter policy](event-publishing.md#dead-letter-topic-setup)
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

When deliveries can arrive out of order, use sequence checks and make the accepted updates idempotent.

### Strategy 1: Sequence Checking

Track the last durably processed `sequenceNumber` per aggregate. For a contiguous projection, advance the checkpoint in the same commit as the effect. On older or repeated delivery, log “Skipping out-of-order event” and acknowledge only after confirming its effect or durable handoff. On a gap or an unverified effect, retry or durably hand the discrepancy to the reconciliation owner. Do not let an older update event recreate data that a newer cleanup event removed.

### Strategy 2: Order-Tolerant Projection Updates

Use absolute assignments such as setting a display name only after checking the sequence and erasure tombstone. Commit the update under those checks so a delayed write cannot restore erased data or overwrite a newer value.

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

- **Unknown event types** can be acknowledged when they fall outside the subscriber’s obligations.
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

`PartyErased` is the subscriber cleanup signal in the current contract. MVP soft deactivation events such as `PartyDeactivated` are not legal erasure. `PartyErased` carries `partyId`, `tenantId`, `erasedAt`, `erasureStatus` and `verificationStatus`. Pending or partial internal verification remains visible through the erasure status surfaces. Consumers holding party references must perform their own cleanup. [Event contract](../src/Hexalith.Parties.Contracts/Events/PartyErased.cs) · [Required handler](event-handler-patterns.md#partyerased-handler-mandatory)
