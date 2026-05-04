# Tenants Access Projection

Hexalith.Parties consumes Hexalith.Tenants events through DAPR pub/sub and keeps a local tenant access projection in the CommandApi process. The projection is updated by the public Tenants client pipeline registered with `AddHexalithTenants()` and exposed to Parties code through `ITenantAccessService`.

## Event Subscription

CommandApi maps the Tenants client subscription endpoint at `POST /tenants/events` with the configured `Tenants:PubSubName` and `Tenants:TopicName`. The default topic is `system.tenants.events`.

The subscription uses DAPR CloudEvents and the Tenants client `TenantEventProcessor`. Parties does not define a second event envelope, retry queue, or tenant event schema. Supported Tenants events include tenant lifecycle, membership, role, and configuration changes handled by the Tenants projection handler.

## Access Decisions

`ITenantAccessService` checks the local Tenants projection and returns a structured allowed/denied result. Expected authorization denials are returned as reason codes rather than exceptions so REST and MCP enforcement can translate them consistently.

The service fails closed when:

- The tenant id is missing.
- The user id is missing.
- The tenant projection is unknown or missing.
- The tenant is disabled.
- The user is not a tenant member.
- The user's role is insufficient or unmapped.

Role mapping is intentionally small:

| Tenants role | Read | Write | Admin |
| --- | --- | --- | --- |
| `TenantReader` | Yes | No | No |
| `TenantContributor` | Yes | Yes | No |
| `TenantOwner` | Yes | Yes | Yes |

Parties does not invent tenant lifecycle, membership, role, or configuration state. Tenant configuration values remain Tenants-owned and are only projected locally through the Tenants client.

## Consistency Window

Tenants event consumption is eventually consistent. A just-disabled tenant or just-removed user can be accepted until the matching Tenants event is delivered and processed by CommandApi. Strong synchronous enforcement is outside this projection path unless a separate Tenants/EventStore authorization plugin is enabled on the command gateway.

When the local projection is missing or lagging, check:

- DAPR sidecar health for `commandapi`.
- The configured pub/sub component and `Tenants:TopicName`.
- Whether `commandapi` is subscribed to `system.tenants.events`.
- Tenants event publishing health.
- Projection state after replay or rebuild, where the platform exposes those operations.

The default Tenants client projection and message-id deduplication are in-memory. Reprocessing the same message id after a process restart can be processed again unless a durable projection or deduplication store is configured.
