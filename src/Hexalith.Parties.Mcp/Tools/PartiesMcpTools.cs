using System.ComponentModel;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.Mcp.Tools;

[McpServerToolType]
internal static class PartiesMcpTools
{
    [McpServerTool(Name = PartiesMcpToolNames.GetParty, Title = "Get Party", ReadOnly = true)]
    [Description("Gets a party by identifier through the Parties EventStore client boundary.")]
    public static PartiesMcpToolResult GetParty(
        [Description("The party identifier to retrieve.")] string partyId)
        => PartiesMcpToolResult.ContractBlocked(PartiesMcpToolNames.GetParty);

    [McpServerTool(Name = PartiesMcpToolNames.FindParties, Title = "Find Parties", ReadOnly = true)]
    [Description("Finds parties using forgiving search, paging, type, and active filters through the Parties EventStore client boundary.")]
    public static PartiesMcpToolResult FindParties(
        [Description("Search text. Empty or omitted text lists parties.")] string? query = null,
        [Description("One-based page number.")] int page = 1,
        [Description("Requested page size.")] int pageSize = 20,
        [Description("Optional party type filter, such as Person or Organization.")] string? type = null,
        [Description("Optional active-state filter.")] bool? active = null)
        => PartiesMcpToolResult.ContractBlocked(PartiesMcpToolNames.FindParties);

    [McpServerTool(Name = PartiesMcpToolNames.CreateParty, Title = "Create Party", Destructive = true)]
    [Description("Creates a person or organization party from forgiving AI-friendly input through the Parties EventStore client boundary.")]
    public static PartiesMcpToolResult CreateParty(
        [Description("Optional caller-supplied party identifier.")] string? partyId = null,
        [Description("Party kind, such as person or organization.")] string? partyType = null,
        [Description("Person given name when creating a person.")] string? givenName = null,
        [Description("Person family name when creating a person.")] string? familyName = null,
        [Description("Organization legal name when creating an organization.")] string? legalName = null)
        => PartiesMcpToolResult.ContractBlocked(PartiesMcpToolNames.CreateParty);

    [McpServerTool(Name = PartiesMcpToolNames.UpdateParty, Title = "Update Party", Destructive = true)]
    [Description("Updates a Parties record using patch semantics where the route partyId is authoritative.")]
    public static PartiesMcpToolResult UpdateParty(
        [Description("The authoritative party identifier to update.")] string partyId,
        [Description("Optional person given name patch.")] string? givenName = null,
        [Description("Optional person family name patch.")] string? familyName = null,
        [Description("Optional organization legal name patch.")] string? legalName = null,
        [Description("Optional active-state patch.")] bool? active = null)
        => PartiesMcpToolResult.ContractBlocked(PartiesMcpToolNames.UpdateParty);

    [McpServerTool(Name = PartiesMcpToolNames.DeleteParty, Title = "Delete Party", Destructive = true, Idempotent = true)]
    [Description("Deactivates or soft-deletes a Parties record while preserving idempotent already-inactive behavior when the client contract can observe it.")]
    public static PartiesMcpToolResult DeleteParty(
        [Description("The party identifier to deactivate.")] string partyId)
        => PartiesMcpToolResult.ContractBlocked(PartiesMcpToolNames.DeleteParty);

    [McpServerTool(Name = PartiesMcpToolNames.GetPartyNameAt, Title = "Get Party Name At", ReadOnly = true)]
    [Description("Compatibility tool for Parties temporal name lookup; preserved as blocked scaffolding until an EventStore query path is frozen.")]
    public static PartiesMcpToolResult GetPartyNameAt(
        [Description("The party identifier to inspect.")] string partyId,
        [Description("The instant for the temporal lookup, as an ISO 8601 value.")] string asOf)
        => PartiesMcpToolResult.ContractBlocked(PartiesMcpToolNames.GetPartyNameAt);
}
