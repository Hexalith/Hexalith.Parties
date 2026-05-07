using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;

namespace Hexalith.Parties.Extensions;

internal static class PartyDetailProjectionActorExtensions
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<PartyDetail?> ReadDetailAsync(this IPartyDetailProjectionActor proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        Task<string?>? jsonTask = null;
        try
        {
            jsonTask = proxy.GetDetailJsonAsync();
        }
        catch (NotImplementedException)
        {
        }

        if (jsonTask is not null)
        {
            try
            {
                string? json = await jsonTask.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json) && !IsEmptyJsonString(json))
                {
                    PartyDetail? deserialized = JsonSerializer.Deserialize<PartyDetail>(json, s_jsonOptions);
                    if (deserialized is not null)
                    {
                        return deserialized;
                    }
                }
            }
            catch (NotImplementedException)
            {
                // Dapr remoting raises NotImplementedException at await time, not invoke time.
            }
            catch (JsonException)
            {
                // Malformed remote payload — fall through to the byte-array strategy below.
                // Earlier versions propagated this and aborted the request before the typed
                // actor proxy could be tried; bringing the catch here lets the resilience
                // ladder run to completion.
            }
        }

        Task<byte[]?>? serializedTask = null;
        try
        {
            serializedTask = proxy.GetSerializedDetailAsync();
        }
        catch (NotImplementedException)
        {
        }

        if (serializedTask is not null)
        {
            try
            {
                byte[]? payload = await serializedTask.ConfigureAwait(false);
                if (payload is { Length: > 0 } && !IsEmptyJsonPayload(payload))
                {
                    PartyDetail? deserialized = JsonSerializer.Deserialize<PartyDetail>(payload, s_jsonOptions);
                    if (deserialized is not null)
                    {
                        return deserialized;
                    }
                }
            }
            catch (NotImplementedException)
            {
            }
            catch (JsonException)
            {
                // Malformed remote payload — fall through to the typed-actor strategy.
            }
        }

        return await proxy.GetDetailAsync().ConfigureAwait(false);
    }

    private static bool IsEmptyJsonString(string json)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(json);
            return node switch
            {
                null => true,
                JsonObject obj => obj.Count == 0,
                JsonArray arr => arr.Count == 0,
                _ => false,
            };
        }
        catch (JsonException)
        {
            // Whitespace-only or otherwise malformed JSON is logically empty for the caller's
            // purpose: there is nothing to deserialize. Returning true coerces the resilience
            // ladder to fall through to the next strategy rather than feeding garbage into
            // JsonSerializer.Deserialize.
            return true;
        }
    }

    private static bool IsEmptyJsonPayload(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return true;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(payload);
            return node switch
            {
                null => true,
                JsonObject obj => obj.Count == 0,
                JsonArray arr => arr.Count == 0,
                _ => false,
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
