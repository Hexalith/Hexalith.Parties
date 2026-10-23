using Hexalith.Parties.Search;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Creates isolated durable SDK fixtures for pre-existing service tests.</summary>
internal static class PartyMemoryTestServices
{
    public static IPartyMemoryIngestionIntentStore CreateIntentStore()
        => new PartyMemoryIngestionIntentStore(new PartyMemoryTestReadModelStore(), Options.Create(new PartyMemoryUnitMappingStoreOptions()));
}
