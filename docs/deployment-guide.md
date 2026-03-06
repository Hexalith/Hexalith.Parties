# Deployment Guide

This guide covers deploying the Hexalith.Parties service with DAPR in production environments.

## Prerequisites

- DAPR runtime installed (v1.14+ recommended)
- Kubernetes cluster with DAPR operator (for Kubernetes deployments)
- One of: Kafka, RabbitMQ, or Azure Service Bus for pub/sub
- One of: CosmosDB, PostgreSQL for state store (Redis for development only)
- PowerShell 5.1+ or PowerShell 7+ for the validation tool

---

## DAPR Component Configuration

### Selecting a Pub/Sub Broker

Choose one pub/sub component file based on your infrastructure:

| Broker | Config File | Ordering | Notes |
|--------|------------|----------|-------|
| Kafka | `pubsub-kafka.yaml` | Per partition (use aggregate-ID key routing) | Best for high throughput |
| RabbitMQ | `pubsub-rabbitmq.yaml` | Per queue binding | Good for moderate workloads |
| Azure Service Bus | `pubsub-servicebus.yaml` | Per session (use aggregate-ID session key) | Topics must be pre-created |

Copy the appropriate file to your DAPR component directory and configure environment variables.

### Selecting a State Store Backend

Choose one state store component file:

| Backend | Config File | Notes |
|---------|------------|-------|
| CosmosDB | `statestore-cosmosdb.yaml` | 2 MB entry size limit (D5). Monitor aggregate sizes. |
| PostgreSQL | `statestore-postgresql.yaml` | Auto-creates tables. No entry size limit concern. |
| Redis | Local dev only (`statestore.yaml`) | Not recommended for production. |

### State Store Requirements

- **actorStateStore: true** -- Required. Actors (aggregates, projections) persist state via DAPR actors.
- **Scoped to commandapi only** -- Only the command API process accesses state store.
- **Entry size limits (D5):** CosmosDB has a 2 MB per-document limit. If aggregate state approaches this limit, consider the partition interface for scale. Monitor `statestore` operation latencies and sizes.

### Required Component Files

A complete production DAPR deployment includes:

```
<dapr-components-dir>/
    accesscontrol.yaml          # Service invocation security
    resiliency.yaml             # Retry + circuit breaker policies
    pubsub-<broker>.yaml        # One of: kafka, rabbitmq, servicebus
    statestore-<backend>.yaml   # One of: cosmosdb, postgresql
    subscription-parties.yaml   # Event subscription routing (per tenant)
```

### Environment Variables

Configure these environment variables for your deployment:

**Access Control:**
- `DAPR_TRUST_DOMAIN` -- SPIFFE trust domain (default: `hexalith.io`)
- `DAPR_NAMESPACE` -- Kubernetes namespace (default: `hexalith`)

**State Store (CosmosDB):**
- `COSMOSDB_URL` -- CosmosDB account endpoint URL
- `COSMOSDB_MASTER_KEY` -- CosmosDB master key
- `COSMOSDB_DATABASE` -- Database name (default: `dapr`)
- `COSMOSDB_COLLECTION` -- Container name (default: `state`)

**State Store (PostgreSQL):**
- `POSTGRESQL_CONNECTION_STRING` -- PostgreSQL connection string

**Pub/Sub (Kafka):**
- `KAFKA_BROKERS` -- Comma-separated broker addresses
- `KAFKA_AUTH_TYPE` -- Authentication type (`none`, `password`, `mtls`, `oidc`)

**Pub/Sub (RabbitMQ):**
- `RABBITMQ_CONNECTION_STRING` -- RabbitMQ connection string

**Pub/Sub (Azure Service Bus):**
- `SERVICEBUS_CONNECTION_STRING` -- Service Bus connection string

**Subscriber Identity:**
- `SUBSCRIBER_APP_ID` -- App ID of the event subscriber service
- `OPS_MONITOR_APP_ID` -- App ID of the operations monitoring service

---

## Multi-Tenant Setup

The Hexalith.Parties service uses tenant-scoped topics for event isolation.

### Topic Naming Pattern

- Events: `<tenant-id>.parties.events`
- Dead-letter: `deadletter.<tenant-id>.parties.events`

### Adding a Tenant

1. Create a subscription file for the tenant (copy `subscription-parties.yaml` as template):
   ```yaml
   metadata:
     name: parties-events-<tenant-id>
   spec:
     topic: "<tenant-id>.parties.events"
     deadLetterTopic: "deadletter.<tenant-id>.parties.events"
   ```

2. Add the subscriber app-id to pub/sub component `subscriptionScopes` for the tenant's topic.

3. Verify configuration:
   ```bash
   ./deploy/validate-deployment.ps1 --config-path <your-config-dir>
   ```

---

## Backup Strategy

### State Store Backups

- Back up the state store database regularly (CosmosDB continuous backup, PostgreSQL pg_dump)
- State includes: aggregate state, actor reminders, command status entries

### Crypto-Shredding Preparation (v1.1)

In v1.1, per-party encryption keys enable GDPR erasure via key deletion. Backup strategy must account for:

- Key backup: Back up encryption keys separately from encrypted data
- Key deletion = data erasure: Deleting a party's key makes their data unrecoverable
- Backup retention: Align backup retention with GDPR requirements
- Key rotation: Plan for periodic key rotation without data loss

---

## Running the Validation Tool

Before every production deployment, run the validation tool:

```bash
# Console output (human-readable)
./deploy/validate-deployment.ps1 --config-path <your-dapr-config-dir>

# JSON output (CI/CD integration)
./deploy/validate-deployment.ps1 --config-path <your-dapr-config-dir> --output json
```

### CI/CD Integration

Add the validation step to your deployment pipeline:

```yaml
# GitHub Actions example
- name: Validate DAPR configuration
  run: |
    pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -Output json
```

The tool exits with code `0` on success and `1` on failure, making it suitable for CI gate checks.

---

## Troubleshooting Common Misconfigurations

### "defaultAction: allow" in production

**Problem:** Access control permits unrestricted service invocation.
**Fix:** Set `defaultAction: deny` in `accesscontrol.yaml`. Only explicitly allowed operations will succeed.

### "trustDomain: public"

**Problem:** Self-hosted default trust domain does not provide identity verification.
**Fix:** Set `trustDomain` to your SPIFFE domain (e.g., via `{env:DAPR_TRUST_DOMAIN}`).

### State store accessible to multiple app-ids

**Problem:** Non-commandapi services can read/write actor state.
**Fix:** Set state store `scopes` to `[commandapi]` only.

### Subscribers can publish events

**Problem:** `publishingScopes` missing or grants publish access to subscribers.
**Fix:** Add subscribers to `publishingScopes` with empty topic list (e.g., `{env:SUBSCRIBER_APP_ID}=`).

### Hardcoded connection strings

**Problem:** Secrets embedded in YAML config files.
**Fix:** Use `{env:VAR_NAME}` references and set values via environment variables or secret store.

### Missing dead-letter configuration

**Problem:** Failed messages are silently dropped.
**Fix:** Set `enableDeadLetter: "true"` on pub/sub components and `deadLetterTopic` on subscriptions.

---

## Projection Rebuild (Operational Awareness)

Projection actors (party detail, party index) can be rebuilt via admin endpoint (Story 8.3). If projection state becomes corrupted or out of sync:

1. Use the admin rebuild endpoint to trigger replay from event store
2. Monitor projection health via health check endpoints (Story 8.2)
3. Projection actors handle state corruption gracefully with self-healing (D15)

---

## References

- [Deployment Security Checklist](deployment-security-checklist.md) -- Complete security verification
- [Event Publishing](event-publishing.md) -- Production broker config, topic naming, dead-letter
- [Event Subscribing](event-subscribing.md) -- Wire format, event types, idempotency
- [Event Handler Patterns](event-handler-patterns.md) -- Handler patterns per event type
- [Getting Started](getting-started.md) -- Local development setup
