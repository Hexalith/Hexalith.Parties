# Getting Started with Hexalith.Parties

This guide starts the local EventStore-fronted topology, sends a Parties command through EventStore, queries it back through EventStore, and shows the typed .NET client and separate MCP host integration paths.

**Time estimate:** Under 30 minutes from clone to first command/query round-trip.

> **GDPR notice:** The service logs the current MVP GDPR limitation at startup. Do not store regulated personal data in local examples. The retired per-response GDPR reminder header is no longer part of the public response contract.

---

## Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 10.0.300 or later (pinned in `global.json`) | [dot.net](https://dot.net) |
| Docker Desktop | Latest | [docker.com](https://www.docker.com/products/docker-desktop/) |
| Git | Any recent version | [git-scm.com](https://git-scm.com) |
| `jq` | Latest, optional for Bash examples | [jqlang.org](https://jqlang.org) |

**Additional prerequisites for the optional Kubernetes walkthrough (Step 1b):**

| Tool | Version | Download |
|------|---------|----------|
| Local Kubernetes cluster | any of kind, k3d, minikube, Docker Desktop Kubernetes | [kind](https://kind.sigs.k8s.io/), [k3d](https://k3d.io/), [minikube](https://minikube.sigs.k8s.io/) |
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

# Memories.Server is composed in-cluster by the AppHost (Epic 9 v2 Story 9.2 carries this forward from v1 Story 9.3 / ADR 9.3-2). Its build
# requires nested submodules in Hexalith.Memories (Hexalith.Memories pulls Hexalith.Commons
# and Hexalith.EventStore as its own submodules). Initialize them once before the first run:
git -C Hexalith.Memories submodule update --init Hexalith.Commons Hexalith.EventStore

dotnet aspire run --project src/Hexalith.Parties.AppHost
```

Open the Aspire dashboard URL printed by the command and verify these resources are running:

- `eventstore` - public command/query gateway
- `eventstore-admin` - admin server used by the EventStore Admin UI
- `eventstore-admin-ui` - generic stream and event browser
- `parties` - Parties domain actor/projection host behind EventStore
- `tenants` - tenant lifecycle, membership, role, and configuration authority

The `parties-mcp` resource is the separate MCP host when included by the AppHost. It is not hosted by the `parties` actor host.

EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping. Parties owns domain execution and projection behavior behind the actor host. Do not call Parties internals to manage tenant lifecycle, RBAC, authorization, projection actors, or domain invocation.

---

## Step 1b: Publish to a Kubernetes Cluster (Optional)

> **Canonical reference:** [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md) — full cluster topology and reproducibility contracts.

This step is an **alternative** to Step 1 for evaluating the production-shape deployment path against a Kubernetes cluster — see [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md) for the full 9-pod topology and reproducibility contracts. The publish script accepts any context the operator confirms via `-ConfirmContext` (Story 9.5, ADR D-K8s-3); any of the local Kubernetes distributions listed in the prerequisites table is a valid target, as is the in-MVP platform cluster. The Aspire-mode walkthrough in Step 1 remains valid and is the default path for everyday development.

**Time budget:** the entire walkthrough — `publish.ps1` through the first successful `CreateParty` command — fits inside the `< 15 min` first-deploy budget (NFR30) on a developer-class machine that already has the prerequisites installed.

Restore the pinned `aspirate` tool. Ensure the operator has run `docker login -u parties-publisher registry.hexalith.com` once (see `docs/deployment-guide.md` "Zot credentials"). Switch `kubectl` to the target context and run:

```bash
dotnet tool restore
kubectl config use-context kubernetes-admin@cluster.local   # or your local context
pwsh deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local
```

`publish.ps1` resolves the MinVer version from the AppHost, runs `dotnet aspirate generate` (building and pushing container images to `registry.hexalith.com/<app-id>:<minver>`), patches the consumer Deployments (Dapr annotations + JWT `secretKeyRef` + `imagePullSecrets: zot-pull-secret`), installs the DAPR control plane if missing, bootstraps three Secrets (`hexalith-jwt-signing`, `hexalith-keycloak-admin`, `zot-pull-secret`), applies the authoritative DAPR component CRs from `deploy/dapr/`, then applies the workloads via kustomize. Verify pod readiness:

```bash
kubectl get pods -n hexalith-parties
```

Expect **nine pods** in `Running` state by default (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`, `keycloak`, `redis`). Epic 9 v2 closes FR31a's enumerative service-graph contract by composing Memories.Server in-cluster (Dapr-enabled — v2 Story 9.2), shipping Keycloak as a hand-authored carve-out (`deploy/k8s/keycloak/` — admin password via `secretKeyRef` on `hexalith-keycloak-admin` — v2 Story 9.3), and shipping Redis as a hand-authored backing store (`deploy/k8s/redis/`, `emptyDir`-backed — v2 Story 9.3). FrontComposer remains out-of-scope for the MVP — no FrontComposer pod ships in this topology.

**Five** of the nine pods run a `daprd` sidecar — `eventstore`, `eventstore-admin`, `parties`, `tenants`, and `memories` carry `dapr.io/enabled: "true"`. `eventstore-admin-ui`, `parties-mcp`, `keycloak`, and `redis` do not. Confirm the Dapr annotations on the five Dapr-enabled Deployments:

```bash
for app in eventstore eventstore-admin parties tenants memories; do
  echo "--- $app ---"
  kubectl get deployment $app -n hexalith-parties -o jsonpath='{.metadata.annotations}{"\n"}'
done
```

Each should include `dapr.io/enabled: "true"`, `dapr.io/app-id`, `dapr.io/app-port: "8080"`, and `dapr.io/config: accesscontrol-<app-id>` (or `accesscontrol` for `eventstore`). The `dapr.io/app-port` and per-app `dapr.io/config` annotations are injected by `publish.ps1` (aspirate 9.1.0 does not emit them); see `deploy/k8s/README.md` "Known aspirate limitations".

**Operator-managed Secrets (v2 Story 9.5):** `publish.ps1` bootstraps `hexalith-jwt-signing`, `hexalith-keycloak-admin`, and `zot-pull-secret` (idempotent). Verify:

```bash
kubectl get secret hexalith-jwt-signing hexalith-keycloak-admin zot-pull-secret -n hexalith-parties
kubectl -n hexalith-parties get secret zot-pull-secret -o jsonpath='{.type}'   # expect: kubernetes.io/dockerconfigjson
```

All three Secrets must exist before any consumer Deployment becomes `Ready`. Their values are never echoed by `publish.ps1` (operator's Docker credential is base64-emitted via Path B without decoding).

Port-forward the EventStore gateway so Step 3 (First Command) can reach it on `http://localhost:8080`:

```bash
kubectl port-forward -n hexalith-parties svc/eventstore 8080:8080
```

```powershell
$env:EVENTSTORE_URL = "http://localhost:8080"
```

(Aspirate-emitted pods only bind the HTTP listener on port 8080. HTTPS on 8443 is not wired in the local-cluster MVP; managed-cloud TLS termination is out of scope for story 9-1.)

Sanity-check the wired-up gateway with a `CreateParty` call:

```bash
# Acquire a bearer token per Step 3's authentication subsection, export as BEARER_TOKEN.
curl -X POST "$EVENTSTORE_URL/api/v1/commands" \
  -H "Authorization: Bearer $BEARER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "commandType": "Hexalith.Parties.Contracts.Commands.CreateParty",
    "tenantId": "demo",
    "aggregateId": "01HXYZAB...",
    "command": { "partyType": "Person", "displayName": "Ada Lovelace" }
  }'
```

The accepted response carries a correlation id; a follow-up query against `/api/v1/queries` returns the persisted `PartyDetail`. **Step 3 (First Command) below covers the full payload, route, and authentication flow** — the K8s walkthrough mirrors that contract exactly. Use this snippet as the smoke check that the K8s deploy is reachable end-to-end.

### Tearing down the deployment

```bash
pwsh deploy/k8s/teardown.ps1 -ConfirmContext kubernetes-admin@cluster.local            # leaves the DAPR control plane installed
pwsh deploy/k8s/teardown.ps1 -ConfirmContext kubernetes-admin@cluster.local -PurgeDapr # also uninstalls DAPR (slower next deploy)
```

The teardown script enforces the same `-ConfirmContext` exact-match gate, deletes the kustomize set, removes the authoritative DAPR component CRs, and reports any residual resources before exit. Exit code `0` means the namespace is clean.

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

Fetch a bearer token from the local Keycloak realm. Replace `<keycloak-port>`, `<client-id>`, and `<client-secret>` with the values shown for the `keycloak` resource in the Aspire dashboard:

```bash
export TOKEN=$(curl -s -X POST \
  "https://localhost:<keycloak-port>/realms/hexalith/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=<client-id>" \
  -d "client_secret=<client-secret>" \
  -d "scope=openid" | jq -r .access_token)
```

```powershell
$tokenResponse = Invoke-RestMethod -Method Post `
  -Uri "https://localhost:<keycloak-port>/realms/hexalith/protocol/openid-connect/token" `
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

---

## Step 5: Typed .NET Client

For .NET service-to-service integration, prefer the typed client package. It submits the same EventStore command/query envelopes internally.

```bash
dotnet add package Hexalith.Parties.Client
```

```csharp
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
- `get_party_name_at`

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

Subscriber code should be idempotent, tolerate duplicate delivery, acknowledge unknown additive event types, and define its own local event envelope type matching only the fields it consumes. Unknown future events should return success unless the subscriber explicitly owns a retryable failure.

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

### Authentication Errors

`401` responses usually mean the request is missing a valid bearer token, tenant claim, or user identifier. Fix the identity provider or local development token configuration rather than calling Parties internals.

### Authorization Errors

`403` responses mean EventStore or its tenant/RBAC plug-ins rejected the request before Parties domain execution. Verify Hexalith.Tenants lifecycle, membership, and roles. `TenantReader` can read/search, `TenantContributor` can write, and `TenantOwner` can perform administration operations.

### Projection Delay

A command can be accepted before the projection is queryable. Retry with bounded backoff and check projection freshness or EventStore Admin UI stream evidence. Do not bypass EventStore by reading projection actors directly.

### MCP Unavailable

Verify the `parties-mcp` resource is running and configured with the EventStore gateway base URL. Do not route assistants to `/mcp` on the `parties` actor host.

---

## What's Next

- Use the typed .NET client for normal service integration.
- Use `parties-mcp` for AI assistant integration.
- Use EventStore Admin UI for stream/event inspection.
- Review the picker and admin portal docs for UI embedding patterns.
