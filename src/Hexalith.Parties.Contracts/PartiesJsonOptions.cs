using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hexalith.Parties.Contracts;

public static class PartiesJsonOptions
{
    public static JsonSerializerOptions Default { get; } = CreateReadOnlyDefault();

    public static JsonSerializerOptions CreateMutable()
        => new(Default);

    public static void ApplyTo(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PropertyNamingPolicy = Default.PropertyNamingPolicy;
        options.DefaultIgnoreCondition = Default.DefaultIgnoreCondition;
        options.PropertyNameCaseInsensitive = Default.PropertyNameCaseInsensitive;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions CreateReadOnlyDefault()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // Writes are camelCase, but reads must remain case-insensitive: the EventStore
            // framework serializes command/event envelope payloads with default System.Text.Json
            // options (PascalCase member names) via EventPersister, and historical events persisted
            // before this consolidation are also PascalCase on the wire. The serializer options
            // these canonical options replaced (JsonSerializerDefaults.Web in the projection/query
            // readers, JsonSerializerDefaults.General in the domain invoker) all tolerated that
            // casing; dropping that tolerance silently fails to map fields on read.
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly();
        return options;
    }
}
