using System.Text.Json.Serialization;

namespace Hexalith.Parties.Mcp.Tools;

internal sealed record PartiesMcpToolResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("toolName")] string ToolName)
{
    public static PartiesMcpToolResult ContractBlocked(string toolName)
        => new(
            "blocked",
            "contract_unavailable",
            "parties-mcp-client-contract-blocked",
            "Parties MCP tool dispatch is scaffolded, but execution is blocked until Story 12.5 lands or formally freezes the typed Parties client over the EventStore gateway.",
            toolName);
}
