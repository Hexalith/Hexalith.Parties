# Deployment Security Checklist

This checklist covers the security configuration requirements for deploying the Hexalith.Parties service with DAPR. Complete all items before promoting to production.

Canonical reference: [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md).

## Automated Verification

Run the deployment validation tool to automatically verify most checklist items:

```bash
# Validate generated Kubernetes manifests alongside DAPR configs
./deploy/validate-deployment.ps1 --config-path ./deploy/dapr -K8sPath ./deploy/k8s/

# JSON output for CI/CD integration
./deploy/validate-deployment.ps1 --config-path ./deploy/dapr -K8sPath ./deploy/k8s/ -Format json
```

Exit code `0` = all checks passed. Exit code `1` = at least one blocking finding. Exit code `2` = invalid arguments or redaction self-check failure. Exit code `3` = missing config or Kubernetes manifest path.

The K8s-manifest mode (`-K8sPath`) lints the eight Story 9.6 blocking categories: `DaprACL-WildcardAppId`, `DaprACL-WildcardOperation`, `K8sWorkload-DirtyTagOnConsumerImage`, `K8sWorkload-MissingDaprAnnotations`, `K8sWorkload-MissingImagePullSecret`, `K8sWorkload-MissingProbes`, `K8sWorkload-NonSemVerTag`, and `Secret-Plaintext`. JSON output uses schema version `1` and fields `{ severity, category, file, jsonpath, reason }` plus summary `{ findings, blocking, warnings, status }`.

The DAPR config mode also validates the security metadata in `topology.yaml` and `tenants-integration.yaml`. Production runs fail when JWT issuer/audience/signing-key references are missing, authentication is not marked fail-closed, tenant identity is allowed from request payloads, DAPR ACLs are broad, or HTTPS/DAPR mTLS transport policy is not enabled. JSON output is bounded and does not print tokens, signing keys, claims dictionaries, tenant membership payloads, or personal data values.

---

## Pre-Deployment Checklist

### Access Control

- [ ] `accesscontrol.yaml` exists in the DAPR component directory
- [ ] `defaultAction` is set to `deny` (secure by default)
- [ ] `trustDomain` is set to a real SPIFFE domain (NOT `public`)
- [ ] `namespace` matches the Kubernetes namespace for the deployment
- [ ] Policies restrict operations to known `appId` values only
- [ ] No ACL uses global wildcard operations (`*` or `/**`); documented EventStore gateway prefix routes may use `/api/v1/.../**`

### State Store Scoping

- [ ] State store component has `actorStateStore: "true"` metadata
- [ ] `scopes` list contains ONLY `parties` (no other app-ids)
- [ ] Redis uses the Story 9.3 passwordless in-cluster Service contract until the production managed-store follow-up replaces the MVP carve-out

### Pub/Sub Scoping (Three-Layer Architecture)

All three layers must be configured for each pub/sub component:

- [ ] **Layer 1 -- Component Scoping:** `scopes` lists `parties` and authorized subscriber app-ids only
- [ ] **Layer 2 -- Publishing Scoping:** `publishingScopes` denies subscribers from publishing (`{env:SUBSCRIBER_APP_ID}=`)
- [ ] **Layer 3 -- Subscription Scoping:** `subscriptionScopes` restricts subscribers to authorized tenant topics only
- [ ] `subscriptionScopes` includes `parties=system.tenants.events` for the Parties Tenants event consumer
- [ ] `enableDeadLetter` is set to `"true"`
- [ ] Redis-backed MVP pub/sub uses `redis:6379` with no plaintext password metadata

### Hexalith.Tenants Integration

- [ ] `subscription-tenants.yaml` exists for topic `system.tenants.events`
- [ ] Tenants subscription `scopes` includes `parties`
- [ ] Tenants subscription has `deadLetterTopic`
- [ ] `tenants-integration.yaml` sets `pubsubName: pubsub`, `topicName: system.tenants.events`, and `commandApiAppId: eventstore`
- [ ] `tenants-integration.yaml` sets `tenantIdentitySource: authenticatedCredentials`, `allowTenantFromPayload: false`, and `metadataRequired: true`
- [ ] No Parties configuration bypasses Hexalith.Tenants authorization
- [ ] Tenant identity comes only from authenticated credentials and authoritative Tenants metadata, never from command/query request payloads
- [ ] Troubleshooting/runbook owners are assigned for identity provider, tenant administrator, tenant operator, and platform operator issues

### Authentication

- [ ] `topology.yaml` `deploymentSecurity.authentication` defines `jwtIssuer` and `jwtAudience`
- [ ] JWT signing material is referenced by `signingKeySecretName` and `signingKeySecretKey`; no inline signing key is committed
- [ ] `failClosed` is `true` so missing/invalid JWT configuration blocks startup or deployment
- [ ] Validation output does not include tokens, signing keys, claim dictionaries, membership payloads, or personal data

### Transport

- [ ] `topology.yaml` `deploymentSecurity.transport.httpsRequired` is `true`
- [ ] `deploymentSecurity.transport.daprMtlsRequired` is `true`
- [ ] `deploymentSecurity.transport.localDevelopmentHttpAllowed` is `false` for production manifests
- [ ] Any HTTP exception is documented as local-development only and never promoted to production

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
- [ ] Verify with validation tool: `./deploy/validate-deployment.ps1 --config-path <config-dir> -K8sPath <k8s-dir>`

Parties does not create tenant lifecycle, membership, role, global administrator, or configuration authority state. The JWT tenant claim selects context; Hexalith.Tenants membership and role authorize access.

---

## v1.1 Preparation (Secret Store & Key Management)

- [ ] Secret store component deployed and accessible
- [ ] Key management infrastructure provisioned (Azure Key Vault, HashiCorp Vault, etc.)
- [ ] Tenant key provider can create or select tenant key material without returning raw tenant keys to status, logs, metrics, or audit records
- [ ] Per-tenant key namespace strategy defined, including tenant key metadata separate from party ids and party key paths
- [ ] Party-level key paths preserve `{tenant}/parties/{partyId}/v{version}` unless a tested migration is explicitly planned
- [ ] Rotation policy defines operation ids, retry/resume expectations, maximum operator-visible status retention, and bounded failure categories
- [ ] Backup procedures account for crypto-shredding (party key deletion = party personal data erasure) and tenant key rotation (rewrap metadata is recoverable operational state)
- [ ] Rollback plan confirms interrupted tenant rotations can resume from recorded progress and that old tenant wrapping metadata remains readable until each party key is safely rewrapped
- [ ] Status, audit, and metrics validation confirms no tenant key material, wrapped party key bytes, tokens, raw provider errors, or personal data are emitted

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
