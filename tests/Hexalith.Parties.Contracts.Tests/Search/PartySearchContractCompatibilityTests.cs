using System.Reflection;
using System.Text.Json;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Search;

public sealed class PartySearchContractCompatibilityTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

    [Fact]
    public void PartySearchResult_DeserializesOlderPayloadWithoutScoreMetadata()
    {
        const string json = """
            {
              "party": {
                "id": "p-1",
                "type": "Person",
                "isActive": true,
                "displayName": "Ada Lovelace",
                "createdAt": "2026-05-01T00:00:00+00:00",
                "lastModifiedAt": "2026-05-01T00:00:00+00:00"
              },
              "matches": [
                {
                  "matchedField": "displayName",
                  "matchType": "prefix"
                }
              ]
            }
            """;

        PartySearchResult result = JsonSerializer.Deserialize<PartySearchResult>(json, s_jsonOptions)!;

        result.Party.Id.ShouldBe("p-1");
        result.RelevanceScore.ShouldBe(0d);
        result.Matches.Single().MatchedField.ShouldBe("displayName");
        result.Matches.Single().Score.ShouldBeNull();
    }

    [Fact]
    public void PartySearchResult_RoundTripsOptionalMetadataWithoutRequiredConstructorChanges()
    {
        PartySearchResult original = new()
        {
            Party = new PartyIndexEntry
            {
                Id = "p-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-01T00:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture),
            },
            Matches =
            [
                new MatchMetadata
                {
                    MatchedField = "displayName",
                    MatchType = "exact",
                    Score = 1.0,
                },
            ],
            RelevanceScore = 1.0,
        };

        string json = JsonSerializer.Serialize(original, s_jsonOptions);
        PartySearchResult roundTrip = JsonSerializer.Deserialize<PartySearchResult>(json, s_jsonOptions)!;

        roundTrip.Party.ShouldBe(original.Party);
        roundTrip.RelevanceScore.ShouldBe(1.0);
        roundTrip.Matches.Count.ShouldBe(1);
        roundTrip.Matches[0].ShouldBe(original.Matches[0]);
        RequiredProperties(typeof(PartySearchResult)).ShouldBe(["Matches", "Party"]);
        RequiredProperties(typeof(MatchMetadata)).ShouldBe(["MatchType", "MatchedField"]);
    }

    private static string[] RequiredProperties(Type type)
        => [.. type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property
                .GetCustomAttributes()
                .Any(static attribute => attribute.GetType().FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute"))
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)];
}
