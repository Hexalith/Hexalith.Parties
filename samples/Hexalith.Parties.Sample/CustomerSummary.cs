using System.Collections.Concurrent;

namespace Hexalith.Parties.Sample;

public sealed record CustomerSummary
{
    public required string Id { get; init; }

    public required string DisplayName { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
}

public static class CustomerSummaryStore
{
    private static readonly ConcurrentDictionary<string, CustomerSummary> _customers = new();

    public static ConcurrentDictionary<string, CustomerSummary> Customers => _customers;
}
