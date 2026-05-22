using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Sample;

// Hexalith.Parties sample integration project.
// Demonstrates typed EventStore-fronted commands/queries plus subscriber-owned DAPR event handling.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// One-line DI registration.
// Reads Parties:BaseUrl as the EventStore gateway URL and Parties:Tenant as the envelope tenant.
// The Parties:BaseUrl in appsettings.json is a placeholder — copy the eventstore HTTPS
// endpoint from the Aspire dashboard or override the value via environment/configuration.
// Registers IPartiesCommandClient and IPartiesQueryClient via HttpClient.
builder.Services.AddPartiesClient(builder.Configuration);

WebApplication app = builder.Build();

// Maps POST /events/parties to handle CloudEvents from the
// tenant-a.parties.events topic. The subscription is defined in
// DaprComponents/subscription-sample.yaml.
// Run the sample with a DAPR sidecar using app-id "sample" to receive live events.
// When no DAPR sidecar is present, this endpoint simply receives no traffic.
app.MapPartyEventEndpoint();

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
    Console.WriteLine("  Hexalith.Parties - Sample Integration Demo");
    Console.WriteLine("════════════════════════════════════════════════");
    Console.WriteLine("  Live event reception requires a DAPR sidecar with app-id 'sample'.");
    Console.WriteLine();

    try
    {
        // 1. Create a person party
        string partyId = Guid.NewGuid().ToString();
        Console.WriteLine($"[1] Creating person party (ID: {partyId})...");
        PartiesCommandResult<PartyDetail> createResult = await commands.CreatePartyWithResultAsync(
            new CreateParty
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails
                {
                    FirstName = "Demo",
                    LastName = "Contact",
                },
            },
            ct).ConfigureAwait(false);
        PrintCommandResult("Created", createResult);

        // 2. Add an email contact channel
        string channelId = Guid.NewGuid().ToString();
        Console.WriteLine($"[2] Adding email contact channel (ID: {channelId})...");
        PartiesCommandResult<PartyDetail> contactResult = await commands.AddContactChannelWithResultAsync(
            partyId,
            new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = channelId,
                Type = ContactChannelType.Email,
                Value = "party-contact@example.test",
                IsPreferred = true,
            },
            ct).ConfigureAwait(false);
        PrintCommandResult("Added contact channel", contactResult);

        // 3. Add a VAT identifier
        string identifierId = Guid.NewGuid().ToString();
        Console.WriteLine($"[3] Adding VAT identifier (ID: {identifierId})...");
        PartiesCommandResult<PartyDetail> identifierResult = await commands.AddIdentifierWithResultAsync(
            partyId,
            new AddIdentifier
            {
                PartyId = partyId,
                IdentifierId = identifierId,
                Type = IdentifierType.VAT,
                Value = "DEMO-IDENTIFIER-001",
            },
            ct).ConfigureAwait(false);
        PrintCommandResult("Added identifier", identifierResult);

        // 3b. Demonstrate typed rejection/problem handling without depending on internals.
        Console.WriteLine("[3b] Demonstrating typed rejection/problem handling...");
        try
        {
            await commands.CreatePartyWithResultAsync(
                new CreateParty
                {
                    PartyId = partyId,
                    Type = PartyType.Person,
                    PersonDetails = new PersonDetails
                    {
                        FirstName = "Duplicate",
                        LastName = "Contact",
                    },
                },
                ct).ConfigureAwait(false);
        }
        catch (PartiesClientException ex)
        {
            PrintClientProblem(ex);
        }

        // Allow eventual consistency before querying
        Console.WriteLine();
        Console.WriteLine("[*] Waiting 2 seconds for eventual consistency...");
        await Task.Delay(2000, ct).ConfigureAwait(false);

        // 4. Get party by ID
        Console.WriteLine($"[4] Querying party by ID ({partyId})...");
        PartyDetail party = await queries.GetPartyAsync(partyId, ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Found party {party.Id} (Type: {party.Type}, Active: {party.IsActive})");
        Console.WriteLine($"       Contact channels: {party.ContactChannels.Count}, Identifiers: {party.Identifiers.Count}");
        PrintFreshness(party.Freshness);

        // 5. Search by name
        Console.WriteLine("[5] Searching for 'Demo'...");
        PagedResult<PartySearchResult> searchResults = await queries.SearchPartiesAsync("Demo", 1, 10, ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Found {searchResults.TotalCount} result(s)");
        PrintFreshness(searchResults.Freshness);
        foreach (PartySearchResult result in searchResults.Items)
        {
            Console.WriteLine($"       - Party {result.Party.Id} (Type: {result.Party.Type}, Active: {result.Party.IsActive})");
        }

        // 6. List with pagination
        Console.WriteLine("[6] Listing active person parties (page 1, size 10)...");
        PagedResult<PartyIndexEntry> listResults = await queries.ListPartiesAsync(
            page: 1,
            pageSize: 10,
            type: PartyType.Person,
            active: true,
            createdAfter: null,
            createdBefore: null,
            modifiedAfter: null,
            modifiedBefore: null,
            ct).ConfigureAwait(false);
        Console.WriteLine($"    -> Page {listResults.Page}/{listResults.TotalPages}, {listResults.TotalCount} total parties");
        PrintFreshness(listResults.Freshness);
        foreach (PartyIndexEntry entry in listResults.Items)
        {
            Console.WriteLine($"       - Party {entry.Id} (Type: {entry.Type}, Active: {entry.IsActive})");
        }

        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════");
        Console.WriteLine("  Demo complete! All integration patterns shown.");
        Console.WriteLine("════════════════════════════════════════════════");
    }
    catch (Exception ex)
    {
        // Pass the exception to the structured logger so operators see the stack trace in logs,
        // while keeping the console output bounded to a non-sensitive troubleshooting hint.
        logger.LogError(ex, "Sample demo failed");
        Console.WriteLine();
        Console.WriteLine("[ERROR] Demo failed. Check EventStore gateway readiness, tenant configuration, and auth.");
    }

    // ── In-memory read model summary ─────────────────────────
    // If DAPR events were received during the demo, show the
    // CustomerSummary read model state. Keep output bounded to identifiers and counts.
    if (!CustomerSummaryStore.Customers.IsEmpty)
    {
        Console.WriteLine();
        Console.WriteLine("-- In-Memory CustomerSummary Read Model --");
        foreach (CustomerSummary customer in CustomerSummaryStore.Customers.Values)
        {
            Console.WriteLine($"  {customer.Id}: contacts={customer.ContactChannels.Count}, identifiers={customer.IdentifierCount}, active={customer.IsActive}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Press Ctrl+C to exit.");
}

static void PrintCommandResult(string label, PartiesCommandResult<PartyDetail> result)
{
    Console.WriteLine($"    -> {label}. CorrelationId: {result.CorrelationId}");
    if (result.Payload is not null)
    {
        Console.WriteLine($"       Updated party payload: {result.Payload.Id} ({result.Payload.Type}, Active: {result.Payload.IsActive})");
        PrintFreshness(result.Payload.Freshness);
    }
}

static void PrintClientProblem(PartiesClientException ex)
{
    Console.WriteLine($"    -> Rejected/problem response. Status: {ex.Status}, Title: {ex.Title ?? ex.Message}");
    if (!string.IsNullOrWhiteSpace(ex.Type))
    {
        Console.WriteLine($"       Type: {ex.Type}");
    }

    if (!string.IsNullOrWhiteSpace(ex.CorrelationId))
    {
        Console.WriteLine($"       CorrelationId: {ex.CorrelationId}");
    }
}

static void PrintFreshness(ProjectionFreshnessMetadata? freshness)
{
    if (freshness is null)
    {
        Console.WriteLine("       Freshness: not supplied by this response");
        return;
    }

    string warnings = freshness.WarningCodes.Count == 0
        ? "none"
        : string.Join(", ", freshness.WarningCodes);
    Console.WriteLine($"       Freshness: {freshness.Status}; warnings: {warnings}");
}

// MCP host configuration
// ----------------------
// MCP runs in the separate parties-mcp host. Do not point clients at /mcp on the
// parties actor host.
//
// Available MCP tools:
//   - find_parties: Search for parties by name or other criteria
//   - get_party:    Retrieve full party details by ID
//   - create_party: Create a new person or organization party
//   - update_party: Modify party details, contacts, or identifiers
//   - delete_party: Deactivate a party
//   - get_party_name_at: Retrieve a historical display name when name history is available
//
// Replace the angle-bracket placeholders below with real values:
//   - <parties-mcp-port> with the parties-mcp HTTPS port from the Aspire dashboard
//   - <token> with a bearer token issued by Keycloak (or your dev token provider)
//   - <user-id> with the authenticated user's identifier
//
// Claude Desktop configuration (~/.claude/claude_desktop_config.json):
//   {
//     "mcpServers": {
//       "hexalith-parties": {
//         "url": "https://localhost:<parties-mcp-port>/mcp",
//         "headers": {
//           "Authorization": "Bearer <token>",
//           "X-Tenant-Id": "tenant-a",
//           "X-User-Id": "<user-id>"
//         }
//       }
//     }
//   }
//
// VS Code (settings.json):
//   {
//     "mcp": {
//       "servers": {
//         "hexalith-parties": {
//           "url": "https://localhost:<parties-mcp-port>/mcp",
//           "headers": {
//             "Authorization": "Bearer <token>",
//             "X-Tenant-Id": "tenant-a",
//             "X-User-Id": "<user-id>"
//           }
//         }
//       }
//     }
//   }
//
// Authentication and tenant/user context are forwarded by parties-mcp to the
// EventStore gateway through the typed Parties client boundary.
