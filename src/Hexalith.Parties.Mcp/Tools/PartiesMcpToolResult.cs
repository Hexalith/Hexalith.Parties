using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hexalith.Parties.Mcp.Tools;

internal sealed record PartiesMcpToolResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("toolName")] string ToolName,
    [property: JsonPropertyName("correlationId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CorrelationId = null,
    [property: JsonPropertyName("data")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Data = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static PartiesMcpToolResult Accepted(string toolName, string correlationId)
        => new(
            "accepted",
            "success",
            "parties-mcp-accepted",
            "The Parties command was accepted by the EventStore gateway.",
            toolName,
            correlationId);

    public static PartiesMcpToolResult Accepted(string toolName, IReadOnlyList<string> correlationIds)
        => new(
            "accepted",
            "success",
            "parties-mcp-accepted",
            "The Parties commands were accepted by the EventStore gateway.",
            toolName,
            // Surface the last correlationId on the record so observability tooling keyed on
            // CorrelationId can recover it; the full list remains available via Data.
            correlationIds.Count > 0 ? correlationIds[^1] : null,
            JsonSerializer.SerializeToElement(new { correlationIds }, JsonOptions));

    public static PartiesMcpToolResult Succeeded(string toolName, object? data = null, string? code = null, string? correlationId = null)
        => new(
            "succeeded",
            "success",
            code ?? "parties-mcp-succeeded",
            "The Parties MCP tool completed successfully.",
            toolName,
            correlationId,
            Data: data is null ? null : JsonSerializer.SerializeToElement(data, JsonOptions));

    public static PartiesMcpToolResult Failed(
        string toolName,
        string category,
        string code,
        string message,
        string? correlationId = null)
        => new("failed", category, code, message, toolName, correlationId);
}
