using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Commons.UniqueIds;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Client.Registration;
using Hexalith.Parties.Authorization;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Hosts the production subscription handler and tenant-access services with no external dependencies.</summary>
/// <param name="application">The controlled in-process HTTP host.</param>
internal sealed class PartiesTenantBootstrapHost(WebApplication application) : IAsyncDisposable
{
    /// <summary>Gets the real registered projection and authorization services.</summary>
    internal IServiceProvider Services => application.Services;

    /// <summary>Creates a client for the authenticated SDK subscription route.</summary>
    internal HttpClient CreateClient() => application.GetTestClient();

    /// <summary>Starts the owner-provided subscription/security registrations with in-memory stores and no background worker or external I/O.</summary>
    internal static async Task<PartiesTenantBootstrapHost> StartAsync(string channelToken, CancellationToken cancellationToken)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = "Test",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["APP_API_TOKEN"] = channelToken });
        builder.Services.AddHexalithTenants();
        builder.Services.AddSingleton<ITenantAccessService, TenantAccessService>();
        builder.Services.AddAuthentication().AddEventStoreSidecarChannelScheme();
        builder.Services.AddEventStoreWorkloadPolicies(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, []);
        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCloudEvents();
        app.MapEventStoreDomainEvents();
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            return new PartiesTenantBootstrapHost(app);
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => application.DisposeAsync();

    /// <summary>Gets the synthetic tenant used only by this disposable fixture.</summary>
    internal const string TenantId = "mtls-qualification-tenant";

    /// <summary>Gets the synthetic member used only by this disposable fixture.</summary>
    internal const string UserId = "mtls-qualification-member";

    /// <summary>Builds supported SDK tenant/member envelopes with fresh delivery identities.</summary>
    internal static IReadOnlyList<EventStoreDomainEventEnvelope> Envelopes(string tenantId, string userId)
        =>
        [
            Envelope(tenantId, new TenantCreated(tenantId, "Synthetic mTLS qualification", null, DateTimeOffset.UtcNow), 1),
            Envelope(tenantId, new UserAddedToTenant(tenantId, userId, TenantRole.TenantContributor), 2),
        ];

    /// <summary>Delivers both envelopes through the registered authenticated HTTP subscription handler.</summary>
    internal static async Task SeedAsync(HttpClient client, string token, CancellationToken cancellationToken)
    {
        foreach (EventStoreDomainEventEnvelope envelope in Envelopes(TenantId, UserId))
        {
            using HttpResponseMessage response = await DeliverAsync(client, envelope, token, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Authenticated tenant bootstrap returned {(int)response.StatusCode}.");
            }
        }
    }

    /// <summary>Sends one plain SDK envelope with optional app-channel authentication.</summary>
    internal static Task<HttpResponseMessage> DeliverAsync(
        HttpClient client, EventStoreDomainEventEnvelope envelope, string? token, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/tenants/events") { Content = JsonContent.Create(envelope) };
        if (token is not null)
        {
            request.Headers.Add("dapr-api-token", token);
        }
        return SendAsync(client, request, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static EventStoreDomainEventEnvelope Envelope<T>(string tenantId, T payload, int sequence)
        => new(UniqueIdHelper.GenerateSortableUniqueStringId(), tenantId, "system", typeof(T).FullName!, sequence, DateTimeOffset.UtcNow,
            UniqueIdHelper.GenerateSortableUniqueStringId(), "json", JsonSerializer.SerializeToUtf8Bytes(payload));
}
