using System.Collections.Concurrent;
using System.Text.Json;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Owns an isolated HTTP state/reminder backend for the production Dapr actor runtime.</summary>
internal sealed class PartyActorSidecarTestHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly ConcurrentDictionary<string, JsonElement> _state = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, JsonElement> _reminders = new(StringComparer.Ordinal);

    private PartyActorSidecarTestHost(WebApplication application) => _application = application;

    /// <summary>Gets the isolated runtime endpoint after the listener has started.</summary>
    internal string Endpoint => _application.Urls.Single();

    /// <summary>Gets the actual persisted pending record, independently of actor state-manager caches.</summary>
    internal CryptoPendingRecord? Pending => _state.TryGetValue("crypto-pending", out JsonElement value)
        ? value.Deserialize<CryptoPendingRecord>(PartiesJsonOptions.Default) : null;

    /// <summary>Gets whether the backend still contains the retry reminder.</summary>
    internal bool HasReminder => _reminders.ContainsKey("retry-key-creation");

    /// <summary>Gets the registration bytes replayed through the production reminder callback.</summary>
    internal JsonElement Reminder => _reminders["retry-key-creation"];

    /// <summary>Starts a private listener on an ephemeral loopback port.</summary>
    internal static async Task<PartyActorSidecarTestHost> StartAsync(CancellationToken cancellationToken)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [], EnvironmentName = "Test", ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication application = builder.Build();
        var host = new PartyActorSidecarTestHost(application);
        const string actor = "/v1.0/actors/PartyKeyRetryActor/tenant-a:party-1";
        application.MapGet(actor + "/state/{key}", (string key) => host._state.TryGetValue(key, out JsonElement value)
            ? Results.Json(value) : Results.NoContent());
        application.MapPut(actor + "/state", async (HttpRequest httpRequest) =>
        {
            // The pinned actor SDK sends JSON bytes using StringContent's default
            // text/plain media type. Read the actual protocol body without an
            // application/json-only minimal API binder.
            using JsonDocument operations = await JsonDocument.ParseAsync(httpRequest.Body,
                cancellationToken: httpRequest.HttpContext.RequestAborted).ConfigureAwait(false);
            // This fixture owns one pending-state key. Reject unsupported batches
            // before mutation instead of approximating multi-operation transactions.
            if (operations.RootElement.GetArrayLength() != 1)
            {
                return Results.BadRequest();
            }
            foreach (JsonElement operation in operations.RootElement.EnumerateArray())
            {
                JsonElement request = operation.GetProperty("request");
                string key = request.GetProperty("key").GetString()!;
                switch (operation.GetProperty("operation").GetString())
                {
                    case "upsert":
                        host._state[key] = request.GetProperty("value").Clone();
                        break;
                    case "delete":
                        host._state.TryRemove(key, out _);
                        break;
                    default:
                        return Results.BadRequest();
                }
            }
            return Results.NoContent();
        });
        application.MapPut(actor + "/reminders/{name}", async (string name, HttpRequest httpRequest) =>
        {
            using JsonDocument reminder = await JsonDocument.ParseAsync(httpRequest.Body,
                cancellationToken: httpRequest.HttpContext.RequestAborted).ConfigureAwait(false);
            host._reminders[name] = reminder.RootElement.Clone();
            return Results.NoContent();
        });
        application.MapDelete(actor + "/reminders/{name}", (string name) =>
        {
            host._reminders.TryRemove(name, out _);
            return Results.NoContent();
        });
        try
        {
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            return host;
        }
        catch
        {
            await application.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _application.DisposeAsync().ConfigureAwait(false);
}
