using Hexalith.Parties.Search;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Immutable options fixture that does not proxy a strong-named generic interface.</summary>
internal sealed class PartyMemoryTestOptionsMonitor(PartyMemorySearchOptions value) : IOptionsMonitor<PartyMemorySearchOptions>
{
    public PartyMemorySearchOptions CurrentValue => value;

    public PartyMemorySearchOptions Get(string? name) => value;

    public IDisposable? OnChange(Action<PartyMemorySearchOptions, string?> listener) => null;
}
