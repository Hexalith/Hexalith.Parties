using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Serialization;

/// <summary>Verifies command enum compatibility with the SDK payload reader.</summary>
public sealed class CommandEnumCompatibilityTests
{
    /// <summary>Accepts client enum names, case variants, and legacy numeric values.</summary>
    /// <param name="commandType">The command contract.</param>
    /// <param name="propertyName">The enum property.</param>
    /// <param name="wireName">The JSON property.</param>
    /// <param name="enumName">The expected enum name.</param>
    /// <param name="enumValue">The expected numeric value.</param>
    [Theory]
    [InlineData(typeof(CreateParty), "Type", "type", "Organization", 2)]
    [InlineData(typeof(CreatePartyComposite), "Type", "type", "Organization", 2)]
    [InlineData(typeof(AddContactChannel), "Type", "type", "Phone", 1)]
    [InlineData(typeof(UpdateContactChannel), "Type", "type", "Phone", 1)]
    [InlineData(typeof(AddIdentifier), "Type", "type", "TaxId", 4)]
    [InlineData(typeof(RecordConsent), "LawfulBasis", "lawfulBasis", "LegitimateInterest", 1)]
    public void CommandEnums_ClientNamesAndLegacyNumbers_BindWithSdkReader(
        Type commandType, string propertyName, string wireName, string enumName, int enumValue)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(enumName);
        JsonObject payload = JsonSerializer.SerializeToNode(Command(commandType), commandType, PartiesJsonOptions.Default)!.AsObject();
        payload[wireName]!.GetValue<string>().ShouldBe(enumName);

        foreach (string property in new[] { wireName, propertyName })
        {
            foreach (JsonNode value in new JsonNode[] { JsonValue.Create(enumName), JsonValue.Create(enumName.ToLowerInvariant()), JsonValue.Create(enumValue), JsonValue.Create(int.MaxValue) })
            {
                JsonObject variant = (JsonObject)payload.DeepClone();
                variant.Remove(wireName);
                variant[property] = value.DeepClone();

                object bound = JsonSerializer.Deserialize(variant.ToJsonString(), commandType, EventStorePayloadSerialization.Options).ShouldNotBeNull();

                object actual = commandType.GetProperty(propertyName)!.GetValue(bound).ShouldNotBeNull();
                Convert.ToInt32(actual, System.Globalization.CultureInfo.InvariantCulture)
                    .ShouldBe(value.GetValueKind() == JsonValueKind.Number && value.GetValue<int>() == int.MaxValue ? int.MaxValue : enumValue);
            }
        }
    }

    /// <summary>Unknown enum names still fail instead of silently selecting a default.</summary>
    /// <param name="commandType">The command contract.</param>
    /// <param name="wireName">The JSON property.</param>
    [Theory]
    [InlineData(typeof(CreateParty), "type")]
    [InlineData(typeof(CreatePartyComposite), "type")]
    [InlineData(typeof(AddContactChannel), "type")]
    [InlineData(typeof(UpdateContactChannel), "type")]
    [InlineData(typeof(AddIdentifier), "type")]
    [InlineData(typeof(RecordConsent), "lawfulBasis")]
    public void CommandEnums_UnknownNames_AreRejected(Type commandType, string wireName)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        JsonObject payload = JsonSerializer.SerializeToNode(Command(commandType), commandType, PartiesJsonOptions.Default)!.AsObject();
        payload[wireName] = "unsupported-enum-value";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize(payload.ToJsonString(), commandType, EventStorePayloadSerialization.Options));
    }

    /// <summary>Composite commands bind nested enum names and numeric values.</summary>
    /// <param name="numeric">Whether to send historical numeric enum values.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompositeCommands_NestedEnums_BindWithSdkReader(bool numeric)
    {
        JsonObject create = JsonSerializer.SerializeToNode(Command(typeof(CreatePartyComposite)), PartiesJsonOptions.Default)!.AsObject();
        JsonObject update = JsonSerializer.SerializeToNode(new UpdatePartyComposite
        {
            PartyId = "party-1",
            AddContactChannels = [(AddContactChannel)Command(typeof(AddContactChannel))],
            UpdateContactChannels = [(UpdateContactChannel)Command(typeof(UpdateContactChannel))],
            AddIdentifiers = [(AddIdentifier)Command(typeof(AddIdentifier))],
        }, PartiesJsonOptions.Default)!.AsObject();
        if (numeric)
        {
            create["type"] = 2;
            create["contactChannels"]![0]!["type"] = 1;
            create["identifiers"]![0]!["type"] = 4;
            update["addContactChannels"]![0]!["type"] = 1;
            update["updateContactChannels"]![0]!["type"] = 1;
            update["addIdentifiers"]![0]!["type"] = 4;
        }

        CreatePartyComposite created = JsonSerializer.Deserialize<CreatePartyComposite>(create.ToJsonString(), EventStorePayloadSerialization.Options).ShouldNotBeNull();
        UpdatePartyComposite updated = JsonSerializer.Deserialize<UpdatePartyComposite>(update.ToJsonString(), EventStorePayloadSerialization.Options).ShouldNotBeNull();

        created.Type.ShouldBe(PartyType.Organization);
        created.ContactChannels.ShouldHaveSingleItem().Type.ShouldBe(ContactChannelType.Phone);
        created.Identifiers.ShouldHaveSingleItem().Type.ShouldBe(IdentifierType.TaxId);
        updated.AddContactChannels.ShouldHaveSingleItem().Type.ShouldBe(ContactChannelType.Phone);
        updated.UpdateContactChannels.ShouldHaveSingleItem().Type.ShouldBe(ContactChannelType.Phone);
        updated.AddIdentifiers.ShouldHaveSingleItem().Type.ShouldBe(IdentifierType.TaxId);
    }

    /// <summary>Nullable updates preserve both explicit null and omitted values.</summary>
    /// <param name="includeNull">Whether to explicitly include a null property.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdateContactChannel_NullOrOmittedType_RemainsNull(bool includeNull)
    {
        JsonObject payload = new() { ["partyId"] = "party-1", ["contactChannelId"] = "phone" };
        if (includeNull)
        {
            payload["type"] = null;
        }

        JsonSerializer.Deserialize<UpdateContactChannel>(payload.ToJsonString(), EventStorePayloadSerialization.Options)
            .ShouldNotBeNull().Type.ShouldBeNull();
    }

    /// <summary>Command compatibility does not change the default persisted enum shape.</summary>
    [Fact]
    public void DefaultEventAndSnapshotWriters_KeepNumericEnums()
    {
        ConsentRecorded consent = new()
        {
            PartyId = "party-1", TenantId = "tenant-a", ConsentId = "phone:billing", ChannelId = "phone",
            Purpose = "billing", LawfulBasis = LawfulBasis.LegitimateInterest, GrantedAt = DateTimeOffset.UnixEpoch, GrantedBy = "test",
        };
        PartyState state = new();
        state.Apply(new PartyCreated { Type = PartyType.Organization });
        state.Apply(consent);
        state.Apply(new ContactChannelAdded { ContactChannelId = "phone", Type = ContactChannelType.Phone, Value = "555" });
        state.Apply(new IdentifierAdded { IdentifierId = "tax", Type = IdentifierType.TaxId, Value = "123" });

        JsonSerializer.SerializeToElement(new PartyCreated { Type = PartyType.Organization }).GetProperty("Type").GetInt32().ShouldBe(2);
        JsonSerializer.SerializeToElement(new ContactChannelAdded { ContactChannelId = "phone", Type = ContactChannelType.Phone, Value = "555" }).GetProperty("Type").GetInt32().ShouldBe(1);
        JsonSerializer.SerializeToElement(new IdentifierAdded { IdentifierId = "tax", Type = IdentifierType.TaxId, Value = "123" }).GetProperty("Type").GetInt32().ShouldBe(4);
        JsonSerializer.SerializeToElement(new ContactChannelUpdated { ContactChannelId = "phone", Type = ContactChannelType.Phone }).GetProperty("Type").GetInt32().ShouldBe(1);
        JsonSerializer.SerializeToElement(new ContactChannelUpdated { ContactChannelId = "phone", Type = null }).GetProperty("Type").ValueKind.ShouldBe(JsonValueKind.Null);
        JsonSerializer.SerializeToElement(consent).GetProperty("LawfulBasis").GetInt32().ShouldBe(1);
        JsonElement snapshot = JsonSerializer.SerializeToElement(state, EventStorePayloadSerialization.Options);
        snapshot.GetProperty("type").GetInt32().ShouldBe(2);
        snapshot.GetProperty("consentRecords")[0].GetProperty("lawfulBasis").GetInt32().ShouldBe(1);
        snapshot.GetProperty("contactChannels")[0].GetProperty("type").GetInt32().ShouldBe(1);
        snapshot.GetProperty("identifiers")[0].GetProperty("type").GetInt32().ShouldBe(4);
    }

    private static object Command(Type commandType)
        => commandType.Name switch
        {
            nameof(CreateParty) => new CreateParty { PartyId = "party-1", Type = PartyType.Organization },
            nameof(CreatePartyComposite) => new CreatePartyComposite
            {
                PartyId = "party-1", Type = PartyType.Organization,
                ContactChannels = [(AddContactChannel)Command(typeof(AddContactChannel))],
                Identifiers = [(AddIdentifier)Command(typeof(AddIdentifier))],
            },
            nameof(AddContactChannel) => new AddContactChannel { PartyId = "party-1", ContactChannelId = "phone", Type = ContactChannelType.Phone, Value = "555" },
            nameof(UpdateContactChannel) => new UpdateContactChannel { PartyId = "party-1", ContactChannelId = "phone", Type = ContactChannelType.Phone },
            nameof(AddIdentifier) => new AddIdentifier { PartyId = "party-1", IdentifierId = "tax", Type = IdentifierType.TaxId, Value = "123" },
            nameof(RecordConsent) => new RecordConsent { PartyId = "party-1", TenantId = "tenant-a", ChannelId = "phone", Purpose = "billing", LawfulBasis = LawfulBasis.LegitimateInterest },
            _ => throw new ArgumentOutOfRangeException(nameof(commandType)),
        };
}
