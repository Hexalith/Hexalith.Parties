using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Shared entry and transport setup for deterministic recovery and live workloads.</summary>
internal static class PartyMemoryRecoveryFixture
{
    internal static PartyIndexEntry Entry() => new() { Id = "party", Type = Hexalith.Parties.Contracts.ValueObjects.PartyType.Person, DisplayName = "private-personal-name", IsActive = true };

    internal static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://127.0.0.1:1/gateway/") };
}
