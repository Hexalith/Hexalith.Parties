using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Parties.CommandApi.Mcp;

internal static class McpSessionContext
{
    public static readonly AsyncLocal<string?> Tenant = new();

    public static readonly AsyncLocal<string?> UserId = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
