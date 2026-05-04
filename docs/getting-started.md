# Getting Started with Hexalith.Parties

This guide walks you through deploying Hexalith.Parties locally and sending your first commands. By the end, you will have created a party record via REST, queried it back, connected an AI assistant via MCP, and integrated the NuGet client package from code.

**Time estimate:** Under 30 minutes from clone to first round-trip.

> **GDPR Notice:** This MVP does **not** include GDPR compliance features (crypto-shredding, consent management, right to erasure). **Do not store regulated EU personal data.** Every API response includes an `X-GDPR-Warning` header as a reminder. For pre-v1.1 erasure requests, see [Emergency Manual Erasure](#emergency-manual-erasure).

---

## Prerequisites

Before you begin, ensure you have the following installed:

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 10.0.103 or later | [dot.net](https://dot.net) |
| Docker Desktop | Latest | [docker.com](https://www.docker.com/products/docker-desktop/) |
| Git | Any recent version | [git-scm.com](https://git-scm.com) |
| `jq` | Latest (optional, Bash examples only) | [jqlang.org](https://jqlang.org) |

Verify your setup:

```bash
dotnet --version   # Should print 10.0.103 or higher
docker --version   # Should print Docker version XX.X.X
```

> Docker Desktop must be **running** before you proceed. The Aspire host launches DAPR sidecars in containers.

> **Shell note:** Every step below includes a Bash example and, where needed, a PowerShell equivalent for Windows. Use the version that matches your shell.

---

## Step 1: Deploy Locally (< 15 minutes)

Clone the repository and start the Aspire host:

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
dotnet aspire run --project src/Hexalith.Parties.AppHost
```

The terminal output will display a URL for the **Aspire dashboard** (typically `https://localhost:15XXX`). Open it in your browser to verify all resources show a green **Running** status.

**What gets started:**
- **commandapi** -- the REST API and MCP server
- **DAPR sidecar** -- event store and actor runtime
- **Keycloak** (port 8180) -- OIDC provider for authentication (enabled by default)

CommandApi also subscribes to Hexalith.Tenants events through DAPR pub/sub and maintains a local tenant access projection. A valid JWT tenant claim is necessary, but it is not sufficient: the authenticated user must be an active member of an active Hexalith.Tenants tenant with the required role. `TenantReader` can read and search parties, `TenantContributor` can read/search and create/update/deactivate/reactivate parties, and `TenantOwner` can also run party administration operations. Roles are cumulative, so an owner can perform reader and contributor operations too. Access checks fail closed when tenant/user state is missing, disabled, or insufficient. Because the projection is event-fed, a just-disabled tenant or removed user may remain accepted until the corresponding event is consumed unless a synchronous Tenants/EventStore authorization plugin is enabled. See [Tenants Access Projection](tenant-access-projection.md) for the operational details.

### Obtain an Authentication Token

Keycloak is enabled by default. The AppHost imports a development realm with a public client named `hexalith-parties` and test users, so you can get a token immediately.

**Default development credentials:**

- Realm: `hexalith`
- Client ID: `hexalith-parties`
- Username: `admin-user`
- Password: `admin-pass`

Store a token for the next steps.

**Bash**

```bash
export TOKEN=$(curl -s -X POST http://localhost:8180/realms/hexalith/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=hexalith-parties" \
  -d "username=admin-user" \
  -d "password=admin-pass" \
  | jq -r '.access_token')
```

**PowerShell**

```powershell
$tokenResponse = Invoke-RestMethod -Method Post -Uri "http://localhost:8180/realms/hexalith/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body "grant_type=password&client_id=hexalith-parties&username=admin-user&password=admin-pass"
$env:TOKEN = $tokenResponse.access_token
```

> If you sign in to the Keycloak admin console, use `admin/admin`. The imported realm users above are only for local development.

#### Alternative: Disable Keycloak

If you prefer simpler auth for local development, stop the Aspire host and restart with Keycloak disabled.

**Bash**

```bash
EnableKeycloak=false dotnet aspire run --project src/Hexalith.Parties.AppHost
```

**PowerShell**

```powershell
$env:EnableKeycloak = "false"
dotnet aspire run --project src/Hexalith.Parties.AppHost
Remove-Item Env:EnableKeycloak
```

This falls back to symmetric key JWT authentication. Set the signing key in `appsettings.Development.json` under `Authentication:JwtBearer:SigningKey` and generate tokens manually using that key.

---

## Step 2: First Command -- Create a Party

Find the `commandapi` HTTP endpoint in the Aspire dashboard before sending API calls. The examples below assume `http://localhost:5000`; replace that value if your local endpoint differs.

Send a `CreateParty` command via REST:

**Bash**

```bash
curl -s -X POST http://localhost:5000/api/v1/parties \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "partyId": "550e8400-e29b-41d4-a716-446655440000",
    "type": "person",
    "personDetails": {
      "firstName": "Jean",
      "lastName": "Dupont"
    }
  }'
```

**PowerShell**

```powershell
curl.exe -s -X POST http://localhost:5000/api/v1/parties `
  -H "Content-Type: application/json" `
  -H "Authorization: Bearer $env:TOKEN" `
  -d '{"partyId":"550e8400-e29b-41d4-a716-446655440000","type":"person","personDetails":{"firstName":"Jean","lastName":"Dupont"}}'
```

**Expected response** (HTTP 202 Accepted):

```json
{
  "correlationId": "a1b2c3d4-e5f6-..."
}
```

The `202 Accepted` status means the command was accepted for processing. The system is event-sourced, so the party will be available for query after projection processing (typically milliseconds).

> You will see an `X-GDPR-Warning` header in the response. This is the GDPR middleware reminding you that this MVP does not include GDPR compliance features.

---

## Step 3: First Query -- Retrieve and Search

### Get Party by ID

**Bash**

```bash
curl -s http://localhost:5000/api/v1/parties/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer $TOKEN" | jq
```

**PowerShell**

```powershell
curl.exe -s http://localhost:5000/api/v1/parties/550e8400-e29b-41d4-a716-446655440000 `
  -H "Authorization: Bearer $env:TOKEN"
```

This returns the full `PartyDetail` object with all person details, contact channels, and identifiers.

### Search Parties by Name

**Bash**

```bash
curl -s "http://localhost:5000/api/v1/parties/search?q=Dupont" \
  -H "Authorization: Bearer $TOKEN" | jq
```

**PowerShell**

```powershell
curl.exe -s "http://localhost:5000/api/v1/parties/search?q=Dupont" `
  -H "Authorization: Bearer $env:TOKEN"
```

Returns a paginated `PagedResult<PartySearchResult>` with matching parties.

### List All Parties

**Bash**

```bash
curl -s "http://localhost:5000/api/v1/parties?page=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN" | jq
```

**PowerShell**

```powershell
curl.exe -s "http://localhost:5000/api/v1/parties?page=1&pageSize=20" `
  -H "Authorization: Bearer $env:TOKEN"
```

Supports filtering by `type`, `active`, `createdAfter`, `createdBefore`, `modifiedAfter`, `modifiedBefore`.

---

## Step 4: MCP Server -- AI Assistant Integration

Hexalith.Parties exposes five MCP tools that AI assistants can use directly: `create_party`, `get_party`, `find_parties`, `update_party`, and `delete_party`.

### Configure Claude Desktop

Add the following to your Claude Desktop MCP configuration (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "hexalith-parties": {
      "url": "http://localhost:5000/mcp",
      "headers": {
        "Authorization": "Bearer <YOUR_TOKEN>"
      }
    }
  }
}
```

### Configure VS Code (GitHub Copilot)

Add to your VS Code `settings.json`:

```json
{
  "mcp": {
    "servers": {
      "hexalith-parties": {
        "url": "http://localhost:5000/mcp",
        "headers": {
          "Authorization": "Bearer <YOUR_TOKEN>"
        }
      }
    }
  }
}
```

### First MCP Tool Call

Once configured, ask your AI assistant:

> "Create a party for Marie Martin, a person"

The assistant will call the `create_party` tool, which auto-generates IDs and accepts forgiving input. You can then ask:

> "Find all parties named Martin"

The `find_parties` tool returns paginated search results.

---

## Step 5: NuGet Client Package -- .NET Integration

For .NET service-to-service integration, use the typed client package instead of raw HTTP calls.

### Install the Package

```bash
dotnet add package Hexalith.Parties.Client
```

### Configure Services

In your `Program.cs`:

```csharp
builder.Services.AddPartiesClient(builder.Configuration);
```

In `appsettings.json`:

```json
{
  "Parties": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

Use the HTTPS endpoint shown for `commandapi` in the Aspire dashboard if your local port differs.

### Send Commands and Queries

```csharp
public class MyService(IPartiesCommandClient commands, IPartiesQueryClient queries)
{
    public async Task CreateAndRetrievePartyAsync()
    {
        // Create a party -- returns a correlation ID
        string correlationId = await commands.CreatePartyAsync(
            new CreateParty
            {
                PartyId = "my-party-id",
                Type = PartyType.Person,
                PersonDetails = new PersonDetails
                {
                    FirstName = "Jean",
                    LastName = "Dupont"
                }
            },
            CancellationToken.None);

        // Query the party back
        PartyDetail party = await queries.GetPartyAsync(
            "my-party-id",
            CancellationToken.None);

        // Search parties
        PagedResult<PartySearchResult> results = await queries.SearchPartiesAsync(
            "Dupont", page: 1, pageSize: 20,
            CancellationToken.None);
    }
}
```

The client interfaces provide typed access to all command and query operations:

- **`IPartiesCommandClient`** -- `CreatePartyAsync`, `UpdatePersonDetailsAsync`, `AddContactChannelAsync`, `DeactivatePartyAsync`, and more
- **`IPartiesQueryClient`** -- `GetPartyAsync`, `ListPartiesAsync`, `SearchPartiesAsync`

---

## Non-.NET Developer Path

If you are not using .NET for your application, you can deploy Hexalith.Parties via Docker and interact exclusively through the REST API.

### Deploy with Docker

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
dotnet aspire run --project src/Hexalith.Parties.AppHost
```

> Docker Desktop is still required. The Aspire host orchestrates all containers.

### Use the REST API

All party operations are available via standard HTTP calls (see [Step 2](#step-2-first-command----create-a-party) and [Step 3](#step-3-first-query----retrieve-and-search) above). The API uses standard JSON with camelCase properties, string enums, and ISO 8601 dates.

**Skip** Step 5 (NuGet integration) -- it is .NET-specific. Steps 1-4 work with any language or HTTP client.

### API Overview

| Operation | Method | Endpoint |
|-----------|--------|----------|
| Create party | POST | `/api/v1/parties` |
| Get party | GET | `/api/v1/parties/{id}` |
| Search parties | GET | `/api/v1/parties/search?q=...` |
| List parties | GET | `/api/v1/parties` |
| Update person | POST | `/api/v1/parties/{id}/update-person-details` |
| Deactivate party | POST | `/api/v1/parties/{id}/deactivate` |
| Create composite | POST | `/api/v1/parties/create-composite` |

See the OpenAPI specification at `/openapi/v1.json` when running in development mode for the complete endpoint reference.

---

## GDPR Disclaimer

> **This MVP does not include GDPR compliance features.** Specifically:
>
> - No crypto-shredding (data encryption at rest per party)
> - No consent management
> - No right to erasure implementation
> - No data portability export
>
> **Do not store regulated EU personal data** in this version.
>
> Every API response includes an `X-GDPR-Warning` header as a reminder. GDPR compliance is planned for v1.1.

### Emergency Manual Erasure

If you receive an erasure request before v1.1 GDPR features are available, you must handle it manually at the data store level. This requires direct access to the underlying event store and projection databases. Consult your data protection officer and follow your organization's data breach/erasure procedures.

---

## Troubleshooting

### DAPR Not Running

**Symptom:** Application fails to start with connection errors to DAPR.

**Fix:** Ensure Docker Desktop is running. DAPR sidecars run in containers managed by Aspire.

```bash
docker ps  # Verify Docker is responsive
```

### Port Conflicts

**Symptom:** Address already in use errors on startup.

**Fix:** Check for processes using the default ports:

```bash
# Check common ports
netstat -an | grep -E "(5000|5001|8180)"
```

**PowerShell**

```powershell
Get-NetTCPConnection -LocalPort 5000,5001,8180 -ErrorAction SilentlyContinue
```

Stop conflicting processes or configure alternative ports in `launchSettings.json`.

### Authentication Errors (401 Unauthorized)

**Symptom:** All API calls return `401 Unauthorized`.

**Fix:**
1. Verify your JWT token is valid and not expired
2. Ensure the token contains the required tenant claim (`eventstore:tenant`) and an authenticated user identifier (`sub`)
3. If using Keycloak, verify the realm and client configuration at `http://localhost:8180`
4. If Keycloak is disabled, verify `Authentication:JwtBearer:SigningKey` is set in configuration

### Tenant Authorization Errors (403 Forbidden)

**Symptom:** API or MCP tools return `403 Forbidden` or an error with reason codes such as `unknown-tenant`, `tenant-disabled`, `not-member`, `insufficient-role`, or `tenant-state-stale`.

**Cause:** The token may identify a tenant, but Hexalith.Tenants local projection state did not authorize the user for the requested operation. Verify tenant lifecycle and membership in Hexalith.Tenants rather than adding Parties-local tenant management. Reader is enough for read/search, Contributor is required for party writes, and Owner is required for administration.

### Projection Delay

**Symptom:** Party created successfully (202) but GET returns 404.

**Cause:** Event-sourced projections need a moment to process. Wait a few seconds and retry. If the issue persists, check the Aspire dashboard for actor health.

### Tenants Projection Lag

**Symptom:** Tenant access decisions do not reflect a recent tenant disablement, membership removal, or role change.

**Cause:** CommandApi authorizes from a local Tenants projection updated by DAPR pub/sub. Verify DAPR sidecar health, the `system.tenants.events` subscription, Tenants event publishing, and projection replay/rebuild options available in your deployment.

---

## What's Next

- **Explore the API** -- Open `/openapi/v1.json` in your browser for the complete OpenAPI specification
- **Connect your AI assistant** -- Configure MCP tools for natural-language party management
- **Integrate from code** -- Use the NuGet client package for typed .NET access
- **v1.1 Roadmap** -- GDPR compliance features including crypto-shredding, consent management, and right to erasure
