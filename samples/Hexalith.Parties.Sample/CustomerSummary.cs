using System.Collections.Concurrent;

namespace Hexalith.Parties.Sample;

public sealed record CustomerSummary
{
    public required string Id { get; init; }

    public required string DisplayName { get; set; }

    public ConcurrentDictionary<string, CustomerContactChannel> ContactChannels { get; } = new();

    public ConcurrentDictionary<string, CustomerIdentifier> Identifiers { get; } = new();

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int IdentifierCount { get; set; }

    public DateTimeOffset? LastUpdated { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed record CustomerContactChannel(string Type, string Value, bool IsPreferred);

public sealed record CustomerIdentifier(string Type, string Value);

public static class CustomerSummaryStore
{
    private static readonly ConcurrentDictionary<string, CustomerSummary> _customers = new();

    public static ConcurrentDictionary<string, CustomerSummary> Customers => _customers;
}
