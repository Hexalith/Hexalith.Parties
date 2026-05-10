using System.ComponentModel.DataAnnotations;

namespace Hexalith.Parties.Mcp;

internal sealed class PartiesMcpOptions
{
    public const string SectionName = "Parties:Mcp";

    [Required]
    public Uri EventStoreGatewayBaseUrl { get; init; } = new("http://eventstore");
}
