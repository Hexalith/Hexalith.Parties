using System.Text.Json.Serialization;

namespace Hexalith.Parties.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectionFreshnessStatus
{
    Current,
    Stale,
    Rebuilding,
    Degraded,
    Unavailable,
    LocalOnly,
}
