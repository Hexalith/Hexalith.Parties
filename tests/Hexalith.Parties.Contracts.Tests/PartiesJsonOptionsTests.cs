using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public sealed class PartiesJsonOptionsTests
{
    [Fact]
    public void Default_IsReadOnlyAndUsesCanonicalWireShape()
    {
        PartiesJsonOptions.Default.IsReadOnly.ShouldBeTrue();

        var command = new CreateParty
        {
            PartyId = "party-1",
            Type = PartyType.Person,
        };

        string json = JsonSerializer.Serialize(command, PartiesJsonOptions.Default);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.GetProperty("partyId").GetString().ShouldBe("party-1");
        root.GetProperty("type").GetString().ShouldBe("Person");
        root.TryGetProperty("personDetails", out _).ShouldBeFalse();
        root.TryGetProperty("PersonDetails", out _).ShouldBeFalse();
    }

    [Fact]
    public void Default_WhenMutationIsAttempted_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(
            () => PartiesJsonOptions.Default.DefaultIgnoreCondition = JsonIgnoreCondition.Never);

        Should.Throw<InvalidOperationException>(
            () => PartiesJsonOptions.Default.Converters.Add(new JsonStringEnumConverter()));
    }

    [Fact]
    public void Default_SerializesRepresentativeEventsAndReadModelsWithStringEnums()
    {
        ConsentRecorded consent = new()
        {
            PartyId = "party-1",
            TenantId = "tenant-1",
            ConsentId = "consent-1",
            ChannelId = "email-1",
            Purpose = "marketing",
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            GrantedBy = "admin",
        };
        PartyDetail detail = new()
        {
            Id = "party-1",
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = "Acme",
            SortName = "Acme",
            CreatedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
        };

        string eventJson = JsonSerializer.Serialize(consent, PartiesJsonOptions.Default);
        string detailJson = JsonSerializer.Serialize(detail, PartiesJsonOptions.Default);

        eventJson.ShouldContain("\"lawfulBasis\":\"Consent\"");

        // Source is a non-nullable string with a default ("unspecified"); WhenWritingNull does not
        // omit non-null values, so it is always present on the wire.
        eventJson.ShouldContain("\"source\":\"unspecified\"");

        detailJson.ShouldContain("\"type\":\"Organization\"");
        detailJson.ShouldContain("\"isActive\":true");

        // Null optional read-model fields are omitted (WhenWritingNull): the detail above leaves
        // PersonDetails, OrganizationDetails, RestrictedAt, ErasedAt, and Freshness null.
        detailJson.ShouldNotContain("personDetails", Case.Insensitive);
        detailJson.ShouldNotContain("restrictedAt", Case.Insensitive);
    }

    [Fact]
    public void CreateMutable_ReturnsIndependentCopy()
    {
        JsonSerializerOptions copy = PartiesJsonOptions.CreateMutable();

        copy.IsReadOnly.ShouldBeFalse();
        copy.PropertyNamingPolicy.ShouldBe(PartiesJsonOptions.Default.PropertyNamingPolicy);
        copy.DefaultIgnoreCondition.ShouldBe(PartiesJsonOptions.Default.DefaultIgnoreCondition);
        copy.Converters.Count.ShouldBe(PartiesJsonOptions.Default.Converters.Count);
    }

    [Fact]
    public void ApplyTo_WithNullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartiesJsonOptions.ApplyTo(null!));
    }
}
