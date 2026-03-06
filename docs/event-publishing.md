# Event Publishing Configuration

This document describes how to configure DAPR pub/sub for Hexalith.Parties event publishing in production environments.

## Topic Naming

- **Event topic**: `{tenant}.parties.events` (e.g., `acme.parties.events`)
- **Dead letter topic**: `deadletter.{tenant}.parties.events` (e.g., `deadletter.acme.parties.events`)

Events are wrapped in CloudEvents 1.0 format by the EventStore publisher with these metadata attributes:

| Attribute | Value |
|-----------|-------|
| `cloudevent.type` | Event type name (e.g., `PartyCreated`) |
| `cloudevent.source` | `hexalith-eventstore/{tenantId}/parties` |
| `cloudevent.id` | `{correlationId}:{sequenceNumber}` |

## Production Broker Configuration

Production DAPR component templates are in `deploy/dapr/`. Choose one broker per deployment:

### Kafka (`pubsub-kafka.yaml`)

| Environment Variable | Description |
|---------------------|-------------|
| `KAFKA_BROKERS` | Comma-separated broker addresses (`broker1:9092,broker2:9092`) |
| `KAFKA_AUTH_TYPE` | Authentication type (`none`, `password`, `mtls`, `oidc`) |
| `SUBSCRIBER_APP_ID` | Your subscriber application's DAPR app-id |
| `OPS_MONITOR_APP_ID` | Operations monitoring tool's DAPR app-id |

**Ordering guarantee (FR73):** Causal ordering per partition. Use aggregate-ID-based key routing to ensure all events for the same aggregate are processed in order.

### RabbitMQ (`pubsub-rabbitmq.yaml`)

| Environment Variable | Description |
|---------------------|-------------|
| `RABBITMQ_CONNECTION_STRING` | Connection string (`amqp://<user>:<pass>@<host>:5672/`) |
| `SUBSCRIBER_APP_ID` | Your subscriber application's DAPR app-id |
| `OPS_MONITOR_APP_ID` | Operations monitoring tool's DAPR app-id |

**Ordering guarantee (FR73):** Causal ordering per queue. Each subscriber gets ordered delivery within its queue binding.

### Azure Service Bus (`pubsub-servicebus.yaml`)

| Environment Variable | Description |
|---------------------|-------------|
| `SERVICEBUS_CONNECTION_STRING` | Connection string with topic management permissions |
| `SUBSCRIBER_APP_ID` | Your subscriber application's DAPR app-id |
| `OPS_MONITOR_APP_ID` | Operations monitoring tool's DAPR app-id |

**Ordering guarantee (FR73):** Causal ordering per session. Use aggregate-ID as the session key.

**Note:** Topics must be pre-created in Azure Service Bus (no auto-creation).

## Subscription Configuration

The [deploy/dapr/subscription-parties.yaml](deploy/dapr/subscription-parties.yaml) file is a concrete single-tenant example. Copy it once per tenant and replace `sample-tenant` in the resource name and topic values before deployment.

```yaml
apiVersion: dapr.io/v2alpha1
kind: Subscription
metadata:
  name: parties-events-sample-tenant
spec:
  pubsubname: pubsub
  topic: "sample-tenant.parties.events"
  routes:
    default: /events/parties
  deadLetterTopic: "deadletter.sample-tenant.parties.events"
scopes:
  - "{env:SUBSCRIBER_APP_ID}"
```

For multi-tenant deployments, create one subscription per tenant or use DAPR's programmatic subscription API for dynamic routing.

## Dead Letter Topic Setup

When event delivery fails and DAPR retry policies are exhausted, events are routed to the dead letter topic.

### How it works

1. The `EventPublisher` publishes events to `{tenant}.parties.events`
2. If delivery fails after retry exhaustion, DAPR routes to `deadLetterTopic` configured in the subscription
3. The `DeadLetterPublisher` explicitly publishes command failure context to `deadletter.{tenant}.parties.events`

### Monitoring dead letters

- Subscribe an operations monitoring tool to `deadletter.{tenant}.parties.events`
- Add the monitoring app-id to pubsub component `scopes` and `subscriptionScopes`
- Dead letter messages include: original command, failure stage, exception type, error message, correlation ID

## Retry and Circuit Breaker Policies

The `deploy/dapr/resiliency.yaml` defines production retry and circuit breaker policies:

### Retry Policies

| Policy | Type | Max Interval | Max Retries | Purpose |
|--------|------|-------------|-------------|---------|
| `defaultRetry` | Exponential | 15s | 10 | General service retry |
| `pubsubRetryOutbound` | Exponential | 10s | 5 | Publisher → broker |
| `pubsubRetryInbound` | Exponential | 60s | 20 | Broker → subscriber |

### Circuit Breakers

| Breaker | Trip Condition | Timeout | Purpose |
|---------|---------------|---------|---------|
| `defaultBreaker` | >5 consecutive failures | 60s | General service protection |
| `pubsubBreaker` | >10 consecutive failures | 60s | Broker outage protection |

When the pub/sub circuit breaker opens, the `AggregateActor` receives immediate failure and transitions to `PublishFailed` state. The persist-then-publish pattern with drain recovery ensures no events are lost.

### Effective retry counts per broker

- **Redis Streams:** 0 built-in → effective = 5 (resiliency only)
- **RabbitMQ:** ~3 built-in → effective ~15 (3 × 5)
- **Kafka:** ~infinite built-in → resiliency rarely fires
- **Azure Service Bus:** 3 built-in → effective ~15 (3 × 5)

## Access Control

The `deploy/dapr/accesscontrol.yaml` defines service-to-service invocation policies:

- **Default action:** `deny` (secure by default)
- **commandapi:** Allowed to invoke domain services via POST
- **Subscribers:** No service invocation permissions (receive events via pub/sub only)

Configure `DAPR_TRUST_DOMAIN` and `DAPR_NAMESPACE` environment variables for your Kubernetes deployment.

## Local Development

Local development uses Redis-backed pub/sub configured in [src/Hexalith.Parties.AppHost/DaprComponents](src/Hexalith.Parties.AppHost/DaprComponents):

- **pubsub.yaml:** Redis on `localhost:6379`, scopes: `commandapi`, `sample`
- **subscription-parties.yaml:** Topic `tenant-a.parties.events`, scoped to the sample subscriber app-id `sample`
- **resiliency.yaml:** Conservative retry policies for fast local iteration
- **accesscontrol.yaml:** `defaultAction: allow` (no mTLS in self-hosted mode)

The sample subscriber endpoint lives in [samples/Hexalith.Parties.Sample/PartyEventHandler.cs](samples/Hexalith.Parties.Sample/PartyEventHandler.cs) and maps `/events/parties`. `commandapi` is the publisher only.

Run via .NET Aspire: `dotnet run --project src/Hexalith.Parties.AppHost`
