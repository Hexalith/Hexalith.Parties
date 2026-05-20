# Deployment Guide

This guide covers deploying the Hexalith.Parties service with DAPR in production environments.

## Prerequisites

- DAPR runtime installed (v1.14+ recommended)
- Kubernetes cluster with DAPR operator (for Kubernetes deployments)
- One of: Kafka, RabbitMQ, or Azure Service Bus for pub/sub
- One of: CosmosDB, PostgreSQL for state store (Redis for development only)
- PowerShell 5.1+ or PowerShell 7+ for the validation tool
- `kubectl` context the operator will pass to `publish.ps1` / `teardown.ps1` via `-ConfirmContext` (Story 9.5 ADR 9.5-2 replaced the prior local-cluster regex allowlist)

### Zot credentials

`deploy/k8s/publish.ps1` builds and pushes container images to the Zot registry at `registry.hexalith.com` (ADR 9.5-1). The cluster pulls those images via a Kubernetes `dockerconfigjson` Secret named `zot-pull-secret` that `publish.ps1` bootstraps from the operator's `~/.docker/config.json`. Operator-side setup is a one-time `docker login`:

```bash
docker login -u parties-publisher registry.hexalith.com
# password: captured from the infra-team secure store (Bitwarden / 1Password)
```

- **`parties-publisher`** is the dedicated build account in the cluster-side Zot `builders` group (`accessControl.groups.builders`). Human operator accounts (`jpiquot`, `qdassivignon` in the `admins` group) **are not** used for image push — they retain admin rights for repository management but stay separate from the build account stamped into every pull-secret manifest.
- After `docker login`, `~/.docker/config.json` must carry `auths["registry.hexalith.com"]` with a plain-text `auth` field (base64-encoded `parties-publisher:<password>`). Docker credential helpers (`credsStore`, `credHelpers["registry.hexalith.com"]`) are explicitly **not supported in MVP** — `publish.ps1` exits 6 with an actionable error if it sees either directive. Workarounds: remove the directive temporarily, set `$env:DOCKER_CONFIG` to point at a helper-free config, or pre-create `zot-pull-secret` manually.
- Cluster-side Zot configuration (htpasswd entry + `accessControl.groups.builders` membership for `parties-publisher`) is owned by the infra team. CI runners use the `kaniko` or `github-ci` builder account; that path is post-MVP and tracked in Story 9.6.
- The operator's credential never appears in `publish.ps1` stdout, stderr, or any committed file. Step 11 of `publish.ps1` re-emits the `auths` block wholesale (Path B — never decoded) into the `zot-pull-secret` data field.

---

## DAPR Component Configuration

### Selecting a Pub/Sub Broker

Choose one pub/sub component file based on your infrastructure:

| Broker            | Config File              | Ordering                                     | Notes                       |
| ----------------- | ------------------------ | -------------------------------------------- | --------------------------- |
| Kafka             | `pubsub-kafka.yaml`      | Per partition (use aggregate-ID key routing) | Best for high throughput    |
| RabbitMQ          | `pubsub-rabbitmq.yaml`   | Per queue binding                            | Good for moderate workloads |
| Azure Service Bus | `pubsub-servicebus.yaml` | Per session (use aggregate-ID session key)   | Topics must be pre-created  |

Copy the appropriate file to your DAPR component directory and configure environment variables.

### Selecting a State Store Backend

Choose one state store component file:

| Backend    | Config File                        | Notes                                                |
| ---------- | ---------------------------------- | ---------------------------------------------------- |
| CosmosDB   | `statestore-cosmosdb.yaml`         | 2 MB entry size limit (D5). Monitor aggregate sizes. |
| PostgreSQL | `statestore-postgresql.yaml`       | Auto-creates tables. No entry size limit concern.    |
| Redis      | Local dev only (`statestore.yaml`) | Not recommended for production.                      |

### State Store Requirements

- **actorStateStore: true** -- Required. Actors (aggregates, projections) persist state via DAPR actors.
- **Scoped to parties only** -- Only the command API process accesses state store.
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
    subscription-tenants.yaml   # Hexalith.Tenants authority event subscription
    tenants-integration.yaml    # Parties Tenants integration validation contract
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

The Hexalith.Parties service uses tenant-scoped topics for party event isolation and consumes Hexalith.Tenants as the authority for tenant lifecycle, membership, role, and configuration state.

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

### Hexalith.Tenants Authority Subscription

Parties must subscribe to the shared Tenants topic:

- Pub/sub name: `pubsub`
- Topic: `system.tenants.events`
- Hosting app id: `parties`
- Declarative subscription: `subscription-tenants.yaml`
- Integration manifest: `tenants-integration.yaml` (consumed by `validate-deployment.ps1`)
- Route: `/events/tenants`
- Dead-letter: `deadletter.system.tenants.events`

When production `subscriptionScopes` are enabled, include `parties=system.tenants.events`. Parties consumes this state for authorization only; tenant lifecycle, membership, roles, global administrators, and tenant configuration remain managed by Hexalith.Tenants.

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

### K8s manifest validation (Story 9.2)

Pass `-K8sPath` to additionally lint the generated Kubernetes manifests under `deploy/k8s/`. The same validator entry point covers DAPR config drift and K8s-manifest drift:

```bash
# Validate DAPR config + K8s manifests in one pass
./deploy/validate-deployment.ps1 --config-path ./deploy/dapr -K8sPath ./deploy/k8s/

# Demote cloud-only capability findings (managed-cloud opt-in)
./deploy/validate-deployment.ps1 -K8sPath ./deploy/k8s/ -AllowCloudCapabilities
```

The K8s lint reports workload shape issues (missing image, missing DAPR annotations, unresolved ConfigMap references), DAPR ACL/Subscription drift, plaintext credential leaks in `configMapGenerator.literals` / `Secret` resources, static tenant identifiers, and cloud-only capabilities (`StorageClass`, `IngressClass`, `Service.type: LoadBalancer`). Offending values are redacted in the output. See `deploy/k8s/README.md` → "K8s manifest lint" for the complete category list.

**Story 9.3 added three categories on top of Story 9.2:**

- `K8sTopology-MissingService` (fail) — fires when an AppHost-composed app folder is missing from `deploy/k8s/<app-id>/`, or when its Service selector does not match the Deployment label. Threshold-gated to skip synthetic single-app fixtures; on the committed full-topology tree (≥ 5 app folders present) the contract is enforced.
- `K8sSecret-JwtSigningKeyLiteral` (fail) — fires when any `ConfigMap`, `Secret`, or `Deployment` container env carries a non-empty literal value for `Authentication__JwtBearer__SigningKey`. The signing key must be sourced via `valueFrom.secretKeyRef` on the `hexalith-jwt-signing` Secret (bootstrapped by `publish.ps1`).
- `K8sDapr-ResiliencyCrdSchemaDrift` (fail / warn) — fires when `deploy/dapr/resiliency.yaml` contains the legacy nested `timeouts.daprSidecar.general` shape or the flat `components.<name>.{retry,timeout,circuitBreaker}` shape (Dapr 1.14.4 CRD requires `outbound`/`inbound` split for component targets with both directions). Unknown `apiVersion` values emit warn-severity informational findings; the live-cluster server-side dry-run lives in `publish.ps1` per Story 9.3 AC6.

### CI/CD Integration

Add the validation step to your deployment pipeline:

```yaml
# GitHub Actions example
- name: Validate DAPR configuration
  run: |
      pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s -Output json
```

The tool exits with code `0` on success (warnings allowed), `1` on at least one blocking failure, and `2` on argument / path errors, making it suitable for CI gate checks.

---

## Troubleshooting Common Misconfigurations

### "defaultAction: allow" in production

**Problem:** Access control permits unrestricted service invocation.
**Fix:** Set `defaultAction: deny` in `accesscontrol.yaml`. Only explicitly allowed operations will succeed.

### "trustDomain: public"

**Problem:** Self-hosted default trust domain does not provide identity verification.
**Fix:** Set `trustDomain` to your SPIFFE domain (e.g., via `{env:DAPR_TRUST_DOMAIN}`).

### State store accessible to multiple app-ids

**Problem:** Non-parties services can read/write actor state.
**Fix:** Set state store `scopes` to `[parties]` only.

### Subscribers can publish events

**Problem:** `publishingScopes` missing or grants publish access to subscribers.
**Fix:** Add subscribers to `publishingScopes` with empty topic list (e.g., `{env:SUBSCRIBER_APP_ID}=`).

### Hardcoded connection strings

**Problem:** Secrets embedded in YAML config files.
**Fix:** Use `{env:VAR_NAME}` references and set values via environment variables or secret store.

### Missing dead-letter configuration

**Problem:** Failed messages are silently dropped.
**Fix:** Set `enableDeadLetter: "true"` on pub/sub components and `deadLetterTopic` on subscriptions.

### Missing Tenants subscription or scope

**Problem:** Parties does not receive `system.tenants.events`, so local tenant access state is missing or stale.
**Fix:** Add `subscription-tenants.yaml`, ensure it is scoped to `parties`, and add `parties=system.tenants.events` to pub/sub `subscriptionScopes`.

### Tenant authorization failures

| Observable symptom | Likely cause | Fix owner | Expected behavior |
| ------------------ | ------------ | --------- | ----------------- |
| REST `401`, MCP `missing-tenant` | JWT missing tenant claim | Identity provider owner | Request rejected before Tenants lookup |
| REST `401`, MCP `missing-user` | JWT missing subject/user id | Identity provider owner | Request rejected before Tenants lookup |
| REST `403`, MCP `unknown-tenant` | Tenant id absent from the local Tenants projection | Tenant administrator + Platform operator | Provision the tenant in Hexalith.Tenants and wait for projection convergence |
| REST `403`, MCP `not-member` | Valid tenant claim but no active Tenants membership (or user removed) | Tenant administrator | No party projection read or command routing |
| REST `403`, MCP `insufficient-role` | User role is below the operation requirement | Tenant administrator | Read/write/admin matrix enforced |
| REST `403`, MCP `tenant-disabled` | Tenant disabled in Hexalith.Tenants | Tenant operator | All tenant-scoped access fails closed |
| REST `403`, MCP `tenant-state-stale` | Tenants subscription, projection, or dependency unhealthy | Platform operator | Access fails closed until local state recovers. Operator-driven recovery — the response carries no `Retry-After` header today, so clients should not auto-retry in a tight loop |

---

## Failure Mode Runbook Reference

Use the health endpoints together with the remediation notes below when infrastructure problems occur in production.

### State store unavailable

- `/health` returns `503` because command durability is impaired.
- `/ready` returns `503`; the instance should stop receiving new write traffic.
- Write commands fail with `503 Dependency Unavailable` ProblemDetails instead of surfacing unhandled exceptions.
- Query endpoints may continue returning the last successfully persisted projection state, with `X-Service-Degraded: true` and `X-Stale-Data-Age` headers when stale reads are being served.

**Operator actions:**

1. Check the configured `statestore` backend health, credentials, and network access.
2. Restore state store connectivity before re-enabling traffic to the instance.
3. After recovery, confirm `/ready` returns `200` and stale-read headers disappear from normal query traffic.

### Pub/sub unavailable

- `/health` returns `200` with a degraded pub/sub component entry.
- `/ready` remains `200` because commands can still be durably committed.
- Events are committed to the event store but publication may be delayed until pub/sub connectivity recovers.
- Successful reads may include `X-Service-Degraded: true` and `X-Stale-Data-Age` headers to indicate delayed freshness.

**Operator actions:**

1. Inspect the configured broker/topic/session infrastructure and DAPR pub/sub component configuration.
2. Verify dead-letter and retry policies remain enabled in `resiliency.yaml`.
3. After recovery, watch subscriber lag until delayed publications are drained.

### DAPR sidecar unavailable

- `/health` returns `503` and `/ready` returns `503`.
- The sidecar failure is logged at `Error` level.
- No stale-read fallback is advertised because actor routing and state access through DAPR are not trustworthy in this mode.

**Operator actions:**

1. Restart or replace the pod/container hosting the DAPR sidecar.
2. Verify the sidecar can reach placement, state store, and pub/sub dependencies.
3. Confirm `/health` and `/ready` both return `200` before resuming normal traffic.

---

## Projection Rebuild

Projection actors (party detail, party index) maintain read models derived from aggregate events. If projection state becomes corrupted or drifts from the source events, the system provides both automatic and manual rebuild capabilities.

### Automatic Self-Healing (D15)

Projection actors detect state corruption on activation. When deserialization fails during `OnActivateAsync()`:

1. The actor logs the corruption at `Error` level with the affected tenant and actor key.
2. The actor enters a **degraded** state — query responses return last-known cached data (or empty) with `X-Service-Degraded` headers.
3. A rebuild is triggered automatically via a DAPR actor reminder. The rebuild reads aggregate events from the event store and replays them through the projection handlers.
4. After rebuild completes, the actor resumes normal operation and logs a "rebuild completed" message at `Information` level.

**No operator action is needed** for automatic self-healing. Monitor logs for `EventId 8300/8310` (corruption detected) and `EventId 8301/8311` (rebuild completed).

### Manual Rebuild Procedure

Use the admin rebuild endpoint when you suspect drift, after a state store migration, or after a schema change.

**Trigger a rebuild:**

```bash
# Rebuild all projections for a tenant
curl -X POST http://localhost:5000/api/v1/admin/projections/rebuild \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin-jwt>" \
  -d '{"tenantId": "tenant-a", "projection": "all"}'

# Rebuild only the detail projection for a specific party
curl -X POST http://localhost:5000/api/v1/admin/projections/rebuild \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin-jwt>" \
  -d '{"tenantId": "tenant-a", "projection": "detail", "partyId": "abc-123"}'

# Rebuild only the index projection
curl -X POST http://localhost:5000/api/v1/admin/projections/rebuild \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin-jwt>" \
  -d '{"tenantId": "tenant-a", "projection": "index"}'
```

**Response:** `202 Accepted` with a `correlationId` for log correlation. The rebuild runs asynchronously in the background.

**Parameters:**

| Parameter    | Required | Values                    | Description                                  |
| ------------ | -------- | ------------------------- | -------------------------------------------- |
| `tenantId`   | Yes      | string                    | Target tenant identifier                     |
| `projection` | Yes      | `detail`, `index`, `all`  | Which projection(s) to rebuild               |
| `partyId`    | No       | string                    | Specific party (detail only, omit for all)   |

### Monitoring a Rebuild

1. **Logs:** Search for `correlationId` from the 202 response. Look for `EventId 8320` (rebuild started) and `EventId 8321` (rebuild completed).
2. **Health endpoint:** During rebuild, `/health` may report `Degraded` for `projection-actors`. This is expected.
3. **Query responses:** GET requests during rebuild include `X-Service-Degraded: true` headers. Data may be stale or incomplete until rebuild finishes.

### Verifying Rebuild Success

After rebuild completes:

1. Check `/health` returns `Healthy` (no degraded components).
2. Query a known party via `GET /api/v1/parties/{id}` and verify data is current.
3. Confirm `X-Service-Degraded` headers are no longer present on GET responses.

### Expected Rebuild Times

Rebuild time depends on the number of events to replay:

| Event count (per tenant) | Estimated time  | Notes                               |
| ------------------------ | --------------- | ----------------------------------- |
| < 1,000                  | < 5 seconds     | Small tenants                       |
| 1,000 -- 10,000          | 5 -- 30 seconds | Medium tenants                      |
| 10,000 -- 100,000        | 30s -- 5 min    | Large tenants, sequential I/O bound |
| > 100,000                | 5+ minutes      | Consider rebuilding by party ID     |

These are estimates based on sequential event reads from the DAPR sidecar. Actual times depend on state store backend latency and network conditions.

### Impact on Service Availability

- **Queries** continue to serve last-known cached data during rebuild. Responses include `X-Service-Degraded: true` and `X-Stale-Data-Age` headers.
- **Write commands** are unaffected — they continue to be processed by aggregate actors independently.
- **New events** arriving during rebuild will be processed normally after rebuild completes and the actor reactivates.

### When to Use Manual Rebuild

| Scenario                     | Action                                                |
| ---------------------------- | ----------------------------------------------------- |
| Suspected projection drift   | Rebuild `all` for the affected tenant                 |
| After state store migration  | Rebuild `all` for all tenants                         |
| After schema change          | Rebuild `all` for all tenants                         |
| Single party data incorrect  | Rebuild `detail` with specific `partyId`              |
| Index missing entries        | Rebuild `index` for the affected tenant               |

### Admin Endpoint Security

The rebuild endpoint requires the `Admin` authorization policy (JWT with `admin` or `Administrator` role claim). It is **not** exposed on the public API — only operators with elevated permissions can trigger a rebuild.

---

## References

- [Deployment Security Checklist](deployment-security-checklist.md) -- Complete security verification
- [Event Publishing](event-publishing.md) -- Production broker config, topic naming, dead-letter
- [Event Subscribing](event-subscribing.md) -- Wire format, event types, idempotency
- [Event Handler Patterns](event-handler-patterns.md) -- Handler patterns per event type
- [Getting Started](getting-started.md) -- Local development setup
