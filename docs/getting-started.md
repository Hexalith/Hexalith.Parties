# Getting Started with Hexalith.Parties

This guide starts the local EventStore-fronted topology, sends a Parties command through EventStore, queries it back through EventStore, and shows the typed .NET client and separate MCP host integration paths. The default path is Aspire-based local development; the Kubernetes section is optional and uses the same public EventStore gateway contract.

**Time estimate:** Under 30 minutes from clone to first command/query round-trip.

> **GDPR notice:** The service logs the current MVP GDPR limitation at startup and emits `X-Hexalith-Parties-Mvp-Compliance-Warning` while `Parties:Compliance:GdprFeaturesActive` is not enabled. The MVP is not approved for regulated EU personal data until v1.1 GDPR features are active; do not store regulated personal data in local examples.

---

## Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 10.0.301 (pinned in `global.json`) | [dot.net](https://dot.net) |
| Docker Desktop | Latest | [docker.com](https://www.docker.com/) |
| Git | Any recent version | [git-scm.com](https://git-scm.com) |
| `jq` | Latest, optional for Bash examples | [jqlang.org](https://jqlang.org) |

Aspire is used through the .NET SDK command `dotnet aspire run`; no separate local orchestration script is required for the default path. DAPR components and sidecars are composed by the AppHost for Aspire mode, so the DAPR CLI is not required unless you use the optional Kubernetes walkthrough.

**Additional prerequisites for the optional Kubernetes walkthrough (Step 1b):**

| Tool | Version | Download |
|------|---------|----------|
| Sandbox Kubernetes cluster | any operator-controlled context isolated from production | Use your platform team's approved local or sandbox cluster setup |
| `kubectl` | recent | [kubernetes.io/docs/tasks/tools/](https://kubernetes.io/docs/tasks/tools/) |
| DAPR CLI | recent | [docs.dapr.io](https://docs.dapr.io/getting-started/install-dapr-cli/) |
| PowerShell (`pwsh`) | 7+ | [github.com/PowerShell](https://github.com/PowerShell/PowerShell) |
| aspirate | `9.1.0` (pinned) | Installed automatically by `dotnet tool restore`. Do not install globally. |

Verify your setup:

```bash
dotnet --version
docker --version
```

Docker Desktop must be running before you start the Aspire host.

---

## Step 1: Deploy Locally

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
git submodule update --init references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
dotnet aspire run --project src/Hexalith.Parties.AppHost
```

Default commands run in package mode. If restore fails because an unpublished Hexalith package such as `Hexalith.Tenants.Client` is unavailable, record that as a package-mode release blocker and use the source-mode properties in [development-guide.md](development-guide.md) only for diagnostic triage.

Open the Aspire dashboard URL printed by the command and verify these resources are running:

- `security` - local Keycloak-backed security service for run-mode OIDC/JWT testing
- `eventstore` - public command/query gateway
- `eventstore-admin` - admin server used by the EventStore Admin UI
- `parties` - Parties domain actor/projection host behind EventStore
- `tenants` - tenant lifecycle, membership, role, and configuration authority
- `redis` - local DAPR state/pubsub backing service
- `statestore` and `pubsub` - DAPR components backed by Redis
- DAPR sidecars for `eventstore`, `eventstore-admin`, `parties`, and `tenants`

The `eventstore-admin-ui` and `parties-mcp` resources are explicit-start auxiliaries in the dashboard. Start `eventstore-admin-ui` when you need stream browsing, and start `parties-mcp` when an AI assistant needs the MCP tool host. The MCP host is separate from the `parties` actor host.

EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping. Parties owns domain execution and projection behavior behind the actor host. Do not call Parties internals to manage tenant lifecycle, RBAC, authorization, projection actors, or domain invocation.

If startup fails before the dashboard appears, first check that Docker Desktop is running and that the baseline root submodules exist under `references/`. A missing build, Commons, EventStore, FrontComposer, PolymorphicSerializations, or Tenants checkout is a setup problem, not a partial local topology that should be treated as ready.

Memories-backed rich search is optional for the default local Parties run. To include it, initialize the `references/Hexalith.Memories` submodule and run the AppHost with `EnableMemoriesSearch=true`; leave it unset for the baseline one-command local topology.

Readiness is confirmed by the Aspire dashboard health column and by the service-default endpoints exposed by each HTTP resource: `/ready` for readiness, `/health` for full health, and `/alive` for liveness. Treat the system as usable only after `eventstore`, `parties`, and `tenants` are healthy; a live `parties` actor host alone is not enough because public traffic enters through EventStore.

---

## Step 1b: Container Publication and Runtime Deployment

The historical in-repo Kubernetes walkthrough has been retired. This repository now publishes only the Parties-owned container images to Zot through GitHub Actions:

- `registry.hexalith.com/parties`
- `registry.hexalith.com/parties-mcp`
- `registry.hexalith.com/parties-ui`

Runtime deployment manifests, platform dependencies, Dapr components, ingress, secrets, and promotion policy are owned by the external deployment orchestrator. See [deployment-guide.md](deployment-guide.md) and [ci.md](ci.md).

---

## Step 2: Configure Tenant and Auth

Provision or use an active Hexalith.Tenants tenant membership before the first Parties call:

1. Create or use an active tenant in Hexalith.Tenants.
2. Add the local user as a tenant member.
3. Assign `TenantContributor` for create/update flows, or `TenantReader` for read/search-only flows.
4. Confirm Parties is subscribed to tenant events through DAPR pub/sub.

Set the EventStore gateway URL and tenant identifier before the first call. Copy the `eventstore` HTTPS port from the Aspire dashboard.

```bash
export EVENTSTORE_URL=https://localhost:<eventstore-port>
export TENANT_ID=tenant-a
```

```powershell
$env:EVENTSTORE_URL = "https://localhost:<eventstore-port>"
$env:TENANT_ID = "tenant-a"
```

Fetch a bearer token from the local Keycloak realm. Replace `<security-port>`, `<client-id>`, and `<client-secret>` with the values shown for the `security` resource in the Aspire dashboard:

```bash
export TOKEN=$(curl -s -X POST \
  "https://localhost:<security-port>/realms/hexalith/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=<client-id>" \
  -d "client_secret=<client-secret>" \
  -d "scope=openid" | jq -r .access_token)
```

```powershell
$tokenResponse = Invoke-RestMethod -Method Post `
  -Uri "https://localhost:<security-port>/realms/hexalith/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    grant_type    = "client_credentials"
    client_id     = "<client-id>"
    client_secret = "<client-secret>"
    scope         = "openid"
  }
$env:TOKEN = $tokenResponse.access_token
```

If you disable Keycloak for local development, use the repository's symmetric-key development token settings and keep the issuer, audience, tenant claim, and permission claims aligned with EventStore gateway expectations. Verify the environment variables are populated (`echo $EVENTSTORE_URL $TENANT_ID` / `echo $env:EVENTSTORE_URL $env:TENANT_ID`) before running the curl examples in Step 3.

---

## Step 3: First Command

All public write traffic goes through EventStore:

- Method: `POST`
- Route: `/api/v1/commands`
- Domain: `party`
- Command type: concrete Parties contract type, such as `Hexalith.Parties.Contracts.Commands.CreateParty`

**Bash**

The `-w` flag echoes the HTTP status alongside the body so non-JSON error responses are visible. Add `-k` only if the local dev certificate is not trusted (see Troubleshooting below for the recommended `dotnet dev-certs https --trust` fix).

```bash
curl --fail-with-body -sS -w '\n[HTTP %{http_code}]\n' -X POST "$EVENTSTORE_URL/api/v1/commands" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "messageId": "cmd-demo-party-001",
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "party-demo-001",
    "commandType": "Hexalith.Parties.Contracts.Commands.CreateParty",
    "payload": {
      "partyId": "party-demo-001",
      "type": "Person",
      "personDetails": {
        "firstName": "Demo",
        "lastName": "Contact"
      }
    },
    "correlationId": "cmd-demo-party-001"
  }'
```

**PowerShell**

```powershell
$body = @{
  messageId = "cmd-demo-party-001"
  tenant = $env:TENANT_ID
  domain = "party"
  aggregateId = "party-demo-001"
  commandType = "Hexalith.Parties.Contracts.Commands.CreateParty"
  payload = @{
    partyId = "party-demo-001"
    type = "Person"
    personDetails = @{
      firstName = "Demo"
      lastName = "Contact"
    }
  }
  correlationId = "cmd-demo-party-001"
} | ConvertTo-Json -Depth 8

Invoke-RestMethod -Method Post -Uri "$env:EVENTSTORE_URL/api/v1/commands" `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $env:TOKEN" } `
  -Body $body
```

Expected result: EventStore accepts the command and returns a correlation id. Command acceptance is not a read-your-write guarantee; projections may need a short moment before query results reflect the event.

The command -> event -> projection flow is: EventStore authenticates and authorizes the request, routes the command envelope to the Parties domain actor host, Parties validates and emits domain events, EventStore persists and publishes those events, and the Parties projection actors update read models that queries use. Use the returned correlation id when checking logs or EventStore Admin UI evidence.

---

## Step 4: First Queries

All public read traffic goes through EventStore:

- Method: `POST`
- Route: `/api/v1/queries`
- Domain: `party`

### Get a Party

**Bash**

```bash
curl -s -X POST "$EVENTSTORE_URL/api/v1/queries" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "party-demo-001",
    "queryType": "PartyDetail",
    "entityId": "party-demo-001"
  }' | jq
```

**PowerShell**

```powershell
$body = @{
  tenant = $env:TENANT_ID
  domain = "party"
  aggregateId = "party-demo-001"
  queryType = "PartyDetail"
  entityId = "party-demo-001"
} | ConvertTo-Json -Depth 6

Invoke-RestMethod -Method Post -Uri "$env:EVENTSTORE_URL/api/v1/queries" `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $env:TOKEN" } `
  -Body $body
```

### Search Parties

```bash
curl -s -X POST "$EVENTSTORE_URL/api/v1/queries" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "parties",
    "queryType": "PartySearch",
    "entityId": "parties",
    "payload": {
      "query": "Demo",
      "page": 1,
      "pageSize": 20
    }
  }' | jq
```

### List Parties

```bash
curl -s -X POST "$EVENTSTORE_URL/api/v1/queries" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "parties",
    "queryType": "PartyIndex",
    "entityId": "parties",
    "payload": {
      "page": 1,
      "pageSize": 20,
      "active": true
    }
  }' | jq
```

### Freshness and Eventual Consistency

Queries read projection state, not the write path directly. Immediately after a successful command, a first query can briefly miss the new party or include freshness metadata showing the projection is rebuilding or stale. Retry with bounded backoff, prefer the `PartyDetail` query for the created party id when checking the first round trip, and use EventStore Admin UI stream evidence when you need to distinguish a rejected command from a delayed projection.

---

## Step 5: Typed .NET Client

For .NET service-to-service integration, prefer the typed client package. It submits the same EventStore command/query envelopes internally.

```bash
dotnet add package Hexalith.Parties.Client
```

```csharp
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

builder.Services.AddPartiesClient(builder.Configuration);
```

```json
{
  "Parties": {
    "BaseUrl": "https://localhost:<eventstore-port>",
    "Tenant": "tenant-a"
  }
}
```

`Parties:BaseUrl` is the EventStore gateway base URL. It is not the `parties` actor-host URL.

```csharp
public sealed class MyService(
    IPartiesCommandClient commands,
    IPartiesQueryClient queries)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string correlationId = await commands.CreatePartyAsync(
            new CreateParty
            {
                PartyId = "party-demo-001",
                Type = PartyType.Person,
                PersonDetails = new PersonDetails
                {
                    FirstName = "Demo",
                    LastName = "Contact"
                }
            },
            cancellationToken);

        PartyDetail party = await queries.GetPartyAsync("party-demo-001", cancellationToken);
        PagedResult<PartySearchResult> results = await queries.SearchPartiesAsync("Demo", 1, 20, cancellationToken);
    }
}
```

---

## Step 6: MCP Host

AI assistants connect to the separate `parties-mcp` host, not to `/mcp` on the `parties` actor host.

Canonical tools:

- `create_party`
- `get_party`
- `find_parties`
- `update_party`
- `delete_party`

Use the `parties-mcp` endpoint shown in the Aspire dashboard. Replace `<parties-mcp-port>` with the parties-mcp HTTPS port from the Aspire dashboard, `<token>` with the bearer token from Step 2, and `<user-id>` with the authenticated user's identifier.

```json
{
  "mcpServers": {
    "hexalith-parties": {
      "url": "https://localhost:<parties-mcp-port>/mcp",
      "headers": {
        "Authorization": "Bearer <token>",
        "X-Tenant-Id": "tenant-a",
        "X-User-Id": "<user-id>"
      }
    }
  }
}
```

The MCP host forwards tenant, user, and authorization context to the EventStore gateway through the typed Parties client boundary. It does not call Parties actors, projections, validators, controllers, or DAPR service invocation directly.

---

## Step 7: Event Subscription

Subscriber applications consume EventStore-published DAPR events. The sample app owns its subscription and local read model:

- Subscription route: `/events/parties`
- Example topic: `tenant-a.parties.events`
- DAPR app id: `sample`

EventStore persists party events before publishing them to DAPR pub/sub. If publishing fails after persistence, drain/recovery processing retries the persisted event; a subscriber failure before acknowledgement can therefore result in the same envelope being delivered again. Subscriber code should persist processed event ids or otherwise be idempotent, tolerate duplicate delivery, acknowledge unknown additive event types, and define its own local event envelope type matching only the fields it consumes. Unknown future events should return success unless the subscriber explicitly owns a retryable failure.

---

## Non-.NET Developer Path

Non-.NET consumers can call the EventStore HTTP gateway directly with the command/query shapes above. Keep the same boundary:

| Operation | Method | Gateway route | Domain | Type |
|-----------|--------|---------------|--------|------|
| Submit command | POST | `/api/v1/commands` | `party` | `commandType` names a Parties command contract |
| Submit query | POST | `/api/v1/queries` | `party` | `queryType` is `PartyDetail`, `PartyIndex`, or `PartySearch` |
| Browse streams | Browser | EventStore Admin UI | n/a | Use `eventstore-admin-ui` from Aspire |
| AI tool access | MCP | `parties-mcp /mcp` endpoint | n/a | Use canonical MCP tool names |

---

## Troubleshooting

### SSL/TLS Certificate Problems

Local Aspire-issued dev certificates must be trusted before curl or HttpClient can reach `https://localhost`. Run `dotnet dev-certs https --trust` once per developer machine. On WSL or CI runners without that command, add `-k` (curl) or set `HttpClientHandler.ServerCertificateCustomValidationCallback` to a development-only override.

### EventStore Gateway Not Ready

Check the Aspire dashboard for `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants`. Public command/query readiness is EventStore-owned; Parties health only proves the actor host is alive behind the gateway.

### Docker or Submodule Startup Failures

If Redis, Keycloak, EventStore, or Tenants fail before the dashboard reaches healthy state, confirm Docker Desktop is running and rerun the baseline root-submodule command from Step 1. Do not use --recursive for the default local run; missing nested submodules are not required by the baseline AppHost path.

### Authentication Errors

`401` responses usually mean the request is missing a valid bearer token, tenant claim, or user identifier. Fix the identity provider or local development token configuration rather than calling Parties internals.

### Authorization Errors

`403` responses mean EventStore or its tenant/RBAC plug-ins rejected the request before Parties domain execution. Verify Hexalith.Tenants lifecycle, membership, and roles. `TenantReader` can read/search, `TenantContributor` can write, and `TenantOwner` can perform administration operations.

### Projection Delay

A command can be accepted before the projection is queryable. Retry with bounded backoff and check projection freshness or EventStore Admin UI stream evidence. Do not bypass EventStore by reading projection actors directly.

### MVP Compliance Boundary

This MVP is not approved for regulated EU personal data until the production KMS gate is met. `Parties:CryptoShredding:IsEnabled` is already enabled by default, but the current `LocalDevKeyStorageBackend` is in-memory and dev-only; a production KMS or secret-store-backed key provider must replace it before any real EU PII is processed. For evaluation, use synthetic names and non-sensitive contact data only. If sensitive data is accidentally entered in a non-production evaluation environment, stop using that dataset and follow your operator's manual deletion or environment rebuild procedure; do not treat the MVP as an erasure-complete system.

The warning remains non-dismissable in service startup logs and response metadata through `X-Hexalith-Parties-Mvp-Compliance-Warning`. It can only be removed by explicitly setting `Parties:Compliance:GdprFeaturesActive=true` after the v1.1 GDPR feature set is active.

### MCP Unavailable

Verify the `parties-mcp` resource is running and configured with the EventStore gateway base URL. Do not route assistants to `/mcp` on the `parties` actor host.

---

## What's Next

- Use the typed .NET client for normal service integration.
- Use `parties-mcp` for AI assistant integration.
- Use EventStore Admin UI for stream/event inspection.
- Review the picker and admin portal docs for UI embedding patterns.
