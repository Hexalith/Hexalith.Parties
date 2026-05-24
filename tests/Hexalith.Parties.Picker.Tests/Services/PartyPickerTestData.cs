using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Picker.Tests.Services;

internal static class PartyPickerTestData
{
    public static PartySearchResult Result(
        string id = "party-1",
        string name = "Ada Lovelace",
        PartyType type = PartyType.Person,
        bool active = true,
        bool erased = false)
        => new()
        {
            Party = new PartyIndexEntry
            {
                Id = id,
                DisplayName = name,
                Type = type,
                IsActive = active,
                IsErased = erased,
                CreatedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
            },
            Matches = [],
            RelevanceScore = 0.95,
        };
}
