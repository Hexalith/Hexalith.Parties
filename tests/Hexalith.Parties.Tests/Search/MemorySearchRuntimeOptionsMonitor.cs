using Hexalith.Parties.Search;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Supplies stable runtime probe options without reconfiguring host globals.</summary>
internal sealed class MemorySearchRuntimeOptionsMonitor(PartyMemorySearchOptions value) : IOptionsMonitor<PartyMemorySearchOptions>
{
    public PartyMemorySearchOptions CurrentValue => value;
    public PartyMemorySearchOptions Get(string? name) => value;
    public IDisposable? OnChange(Action<PartyMemorySearchOptions, string?> listener) => null;
}
