# Deployment Security Checklist

This checklist covers the security configuration requirements for deploying the Hexalith.Parties service with DAPR. Complete all items before promoting to production.

## Automated Verification

Run the deployment validation tool to automatically verify most checklist items:

```bash
# Validate production DAPR configs
./deploy/validate-deployment.ps1 --config-path ./deploy/dapr

# JSON output for CI/CD integration
./deploy/validate-deployment.ps1 --config-path ./deploy/dapr --output json
```

Exit code `0` = all checks passed. Exit code `1` = failures detected.

---

## Pre-Deployment Checklist

### Access Control

- [ ] `accesscontrol.yaml` exists in the DAPR component directory
- [ ] `defaultAction` is set to `deny` (secure by default)
- [ ] `trustDomain` is set to a real SPIFFE domain (NOT `public`)
- [ ] `namespace` matches the Kubernetes namespace for the deployment
- [ ] Policies restrict operations to known `appId` values only
- [ ] Only `commandapi` has `/**` POST access; other services have `deny` default

### State Store Scoping

- [ ] State store component has `actorStateStore: "true"` metadata
- [ ] `scopes` list contains ONLY `commandapi` (no other app-ids)
- [ ] Connection string uses `{env:VAR_NAME}` reference (never hardcoded)
- [ ] Database/container is provisioned and accessible from the deployment environment

### Pub/Sub Scoping (Three-Layer Architecture)

All three layers must be configured for each pub/sub component:

- [ ] **Layer 1 -- Component Scoping:** `scopes` lists `commandapi` and authorized subscriber app-ids only
- [ ] **Layer 2 -- Publishing Scoping:** `publishingScopes` denies subscribers from publishing (`{env:SUBSCRIBER_APP_ID}=`)
- [ ] **Layer 3 -- Subscription Scoping:** `subscriptionScopes` restricts subscribers to authorized tenant topics only
- [ ] `subscriptionScopes` includes `commandapi=system.tenants.events` for the Parties Tenants event consumer
- [ ] `enableDeadLetter` is set to `"true"`
- [ ] Connection string/brokers use `{env:VAR_NAME}` references (never hardcoded)

### Hexalith.Tenants Integration

- [ ] `subscription-tenants.yaml` exists for topic `system.tenants.events`
- [ ] Tenants subscription `scopes` includes `commandapi`
- [ ] Tenants subscription has `deadLetterTopic`
- [ ] `tenants-integration.yaml` sets `pubsubName: pubsub`, `topicName: system.tenants.events`, and `commandApiAppId: commandapi`
- [ ] No Parties configuration bypasses Hexalith.Tenants authorization
- [ ] Troubleshooting/runbook owners are assigned for identity provider, tenant administrator, tenant operator, and platform operator issues

### Secret Store

- [ ] Secret store component configured (required for v1.1 key management)
- [ ] Secret store backend meets organizational security requirements

---

## Per-Broker Deployment Notes

### Kafka (`pubsub-kafka.yaml`)

- Authentication type set via `{env:KAFKA_AUTH_TYPE}` (prefer `mtls` or `oidc` for production)
- Broker addresses via `{env:KAFKA_BROKERS}`
- Partition-routing metadata required for ordered delivery per aggregate (add in environment-specific manifests)
- DAPR does NOT support wildcards in scoping -- strict string match only

### RabbitMQ (`pubsub-rabbitmq.yaml`)

- Connection string via `{env:RABBITMQ_CONNECTION_STRING}`
- `durable: "true"` and `deletedWhenUnused: "false"` for message persistence
- Causal ordering per queue binding (automatic)

### Azure Service Bus (`pubsub-servicebus.yaml`)

- Connection string via `{env:SERVICEBUS_CONNECTION_STRING}`
- Topics must be **pre-created** in Azure Service Bus (no auto-creation)
- Session-enabled topics required for ordered delivery per aggregate
- Configure session metadata in environment-specific manifests

---

## State Store Backend Notes

### CosmosDB (`statestore-cosmosdb.yaml`)

- **Entry size limit: 2 MB per document** (D5 architecture decision)
- Monitor aggregate state sizes in production to detect growth trends
- Provision database and container with partition key `/partitionKey`
- Set `DefaultTimeToLive` on the container for command status TTL support
- Connection via `{env:COSMOSDB_URL}` and `{env:COSMOSDB_MASTER_KEY}`

### PostgreSQL (`statestore-postgresql.yaml`)

- DAPR auto-creates table schema on first use
- Database user needs CREATE TABLE and CRUD permissions
- Connection via `{env:POSTGRESQL_CONNECTION_STRING}`
- `cleanupInterval` controls TTL expiration sweep (default: 3600s)

### Redis (local development only)

- Used in `src/Hexalith.Parties.AppHost/DaprComponents/statestore.yaml`
- Not recommended for production without proper persistence configuration

---

## Tenant Provisioning Checklist

For each new tenant:

- [ ] Provision the tenant, membership, and roles in Hexalith.Tenants
- [ ] Create a subscription file: `subscription-parties-<tenant-id>.yaml`
  - Set `metadata.name` to `parties-events-<tenant-id>`
  - Set `topic` to `<tenant-id>.parties.events`
  - Set `deadLetterTopic` to `deadletter.<tenant-id>.parties.events`
- [ ] Add subscriber app-id to pub/sub component `scopes` (if not already present)
- [ ] Add subscriber to `subscriptionScopes` with the tenant's topic
- [ ] Do NOT add subscriber to `publishingScopes` (subscribers should not publish)
- [ ] Verify with validation tool: `./deploy/validate-deployment.ps1 --config-path <config-dir>`

Parties does not create tenant lifecycle, membership, role, global administrator, or configuration authority state. The JWT tenant claim selects context; Hexalith.Tenants membership and role authorize access.

---

## v1.1 Preparation (Secret Store & Key Management)

- [ ] Secret store component deployed and accessible
- [ ] Key management infrastructure provisioned (Azure Key Vault, HashiCorp Vault, etc.)
- [ ] Per-tenant key namespace strategy defined
- [ ] Backup procedures account for crypto-shredding (key deletion = data erasure)
- [ ] Key rotation policy established

---

## Network Security & IAM (Operator Scope)

These items are outside the Hexalith.Parties service boundary but are critical for production security:

- [ ] DAPR sidecar mTLS enabled (Kubernetes with Sentry CA)
- [ ] Network policies restrict pod-to-pod communication
- [ ] Infrastructure IAM roles follow least-privilege principle
- [ ] Secret store backend access restricted to authorized pods/identities
- [ ] Log aggregation configured (DAPR logs + application logs)
- [ ] Monitoring and alerting configured for security events

---

## References

- [Deployment Guide](deployment-guide.md)
- [Event Publishing](event-publishing.md)
- [Event Subscribing](event-subscribing.md)
- [Getting Started](getting-started.md)
- [Validation Tool](../deploy/validate-deployment.ps1)
