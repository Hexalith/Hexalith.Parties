using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Sample;

// ──────────────────────────────────────────────────────────────
// Hexalith.Parties Sample Integration Project
// Demonstrates: commands, queries, DAPR event subscription, MCP
// ──────────────────────────────────────────────────────────────

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── One-line DI registration ─────────────────────────────────
// Reads Parties:BaseUrl from appsettings.json (or environment variables)
// and registers IPartiesCommandClient + IPartiesQueryClient via HttpClient.
builder.Services.AddPartiesClient(builder.Configuration);

WebApplication app = builder.Build();

// ── DAPR pub/sub event endpoint ──────────────────────────────
// Maps POST /events/parties to handle CloudEvents from the
// tenant-a.parties.events topic. The subscription is defined in
// DaprComponents/subscription-sample.yaml.
// Run the sample with a DAPR sidecar using app-id "sample" to receive live events.
// When no DAPR sidecar is present, this endpoint simply receives no traffic.
app.MapPartyEventEndpoint();

// ── Run the demo after the host starts ───────────────────────
app.Lifetime.ApplicationStarted.Register(() =>
    _ = Task.Run(() => RunDemoAsync(app.Services, app.Lifetime.ApplicationStopping)));

await app.RunAsync().ConfigureAwait(false);

static async Task RunDemoAsync(IServiceProvider services, CancellationToken ct)
{
    // Small delay to let the host start up
    await Task.Delay(1000, ct).ConfigureAwait(false);

    using IServiceScope scope = services.CreateScope();
    IPartiesCommandClient commands = scope.ServiceProvider.GetRequiredService<IPartiesCommandClient>();
    IPartiesQueryClient queries = scope.ServiceProvider.GetRequiredService<IPartiesQueryClient>();
    ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SampleDemo");

    Console.WriteLine();
    Console.WriteLine("════════════════════════════════════════════════");
    Console.WriteLine("  Hexalith.Parties — Sample Integration Demo");
    Console.WriteLine("════════════════════════════════════════════════");
    Console.WriteLine("  Live event reception requires a DAPR sidecar with app-id 'sample'.");
    Console.WriteLine();

    try
    {
        // ── COMMANDS ─────────────────────────────────────────
        // 1. Create a person party
        string partyId = Guid.NewGuid().ToString();
        Console.WriteLine($"[1] Creating person party (ID: {partyId})...");
        string correlationId = await commands.CreatePartyAsync(
            new CreateParty
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails
                {
                    FirstName = "Jean",
                    LastName = "Dupont",
                },
            },
            ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Created. CorrelationId: {correlationId}");

        // 2. Add an email contact channel
        string channelId = Guid.NewGuid().ToString();
        Console.WriteLine($"[2] Adding email contact channel (ID: {channelId})...");
        correlationId = await commands.AddContactChannelAsync(
            partyId,
            new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = channelId,
                Type = ContactChannelType.Email,
                Value = "jean.dupont@example.com",
                IsPreferred = true,
            },
            ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Added. CorrelationId: {correlationId}");

        // 3. Add a VAT identifier
        string identifierId = Guid.NewGuid().ToString();
        Console.WriteLine($"[3] Adding VAT identifier (ID: {identifierId})...");
        correlationId = await commands.AddIdentifierAsync(
            partyId,
            new AddIdentifier
            {
                PartyId = partyId,
                IdentifierId = identifierId,
                Type = IdentifierType.VAT,
                Value = "FR12345678901",
            },
            ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Added. CorrelationId: {correlationId}");

        // Allow eventual consistency before querying
        Console.WriteLine();
        Console.WriteLine("[*] Waiting 2 seconds for eventual consistency...");
        await Task.Delay(2000, ct).ConfigureAwait(false);

        // ── QUERIES ──────────────────────────────────────────
        // 4. Get party by ID
        Console.WriteLine($"[4] Querying party by ID ({partyId})...");
        PartyDetail party = await queries.GetPartyAsync(partyId, ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Found: {party.DisplayName} (Type: {party.Type}, Active: {party.IsActive})");
        Console.WriteLine($"       Contact channels: {party.ContactChannels.Count}, Identifiers: {party.Identifiers.Count}");

        // 5. Search by name
        Console.WriteLine("[5] Searching for 'Dupont'...");
        PagedResult<PartySearchResult> searchResults = await queries.SearchPartiesAsync("Dupont", 1, 10, ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Found {searchResults.TotalCount} result(s)");
        foreach (PartySearchResult result in searchResults.Items)
        {
            Console.WriteLine($"       - {result.Party.DisplayName} (ID: {result.Party.Id})");
        }

        // 6. List with pagination
        Console.WriteLine("[6] Listing parties (page 1, size 10)...");
        PagedResult<PartyIndexEntry> listResults = await queries.ListPartiesAsync(
            page: 1,
            pageSize: 10,
            type: null,
            active: null,
            createdAfter: null,
            createdBefore: null,
            modifiedAfter: null,
            modifiedBefore: null,
            ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Page {listResults.Page}/{listResults.TotalPages}, {listResults.TotalCount} total parties");
        foreach (PartyIndexEntry entry in listResults.Items)
        {
            Console.WriteLine($"       - {entry.DisplayName} (Active: {entry.IsActive})");
        }

        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════");
        Console.WriteLine("  Demo complete! All integration patterns shown.");
        Console.WriteLine("════════════════════════════════════════════════");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Demo failed");
        Console.WriteLine();
        Console.WriteLine($"[ERROR] {ex.Message}");
        Console.WriteLine("  Ensure the Parties service is running (dotnet aspire run).");
    }

    // ── In-memory read model summary ─────────────────────────
    // If DAPR events were received during the demo, show the
    // CustomerSummary read model state.
    if (!CustomerSummaryStore.Customers.IsEmpty)
    {
        Console.WriteLine();
        Console.WriteLine("── In-Memory CustomerSummary Read Model ──");
        foreach (CustomerSummary customer in CustomerSummaryStore.Customers.Values)
        {
            Console.WriteLine($"  {customer.Id}: {customer.DisplayName} | Email: {customer.Email ?? "(none)"} | Active: {customer.IsActive}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Press Ctrl+C to exit.");
}

// ──────────────────────────────────────────────────────────────
// MCP Server Configuration
// ──────────────────────────────────────────────────────────────
// The Hexalith.Parties CommandApi exposes an MCP (Model Context Protocol)
// endpoint at /mcp for AI assistant integration.
//
// Available MCP tools:
//   - find_parties: Search for parties by name or other criteria
//   - get_party:    Retrieve full party details by ID
//   - create_party: Create a new person or organization party
//   - update_party: Modify party details, contacts, or identifiers
//   - delete_party: Deactivate a party
//
// Claude Desktop configuration (~/.claude/claude_desktop_config.json):
//   {
//     "mcpServers": {
//       "hexalith-parties": {
//         "url": "https://localhost:5001/mcp",
//         "headers": { "Authorization": "Bearer <token>" }
//       }
//     }
//   }
//
// VS Code (settings.json):
//   {
//     "mcp": {
//       "servers": {
//         "hexalith-parties": {
//           "url": "https://localhost:5001/mcp",
//           "headers": { "Authorization": "Bearer <token>" }
//         }
//       }
//     }
//   }
//
// Authentication: The MCP endpoint requires a valid bearer token
// from the Keycloak OIDC provider (port 8180, realm "hexalith").
// For local development, set EnableKeycloak=false to disable auth.
// ──────────────────────────────────────────────────────────────
