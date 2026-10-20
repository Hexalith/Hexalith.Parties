using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public sealed class McpCliDecorationTests
{
    [Fact]
    public void ModuleDeclaresExactlySevenApprovedOperationsAndExistingRouting()
    {
        Assembly assembly = typeof(CreatePartyComposite).Assembly;
        HexalithModuleAttribute module = assembly.GetCustomAttribute<HexalithModuleAttribute>()!;
        module.Name.ShouldBe("parties");
        module.IdentifierKind.ShouldBe(IdentifierKind.String);
        module.WireTypeConvention.ShouldBe(WireTypeConvention.FullTypeName);
        module.FixedTenant.ShouldBeNull();
        module.SerializerOptionsProvider.ShouldBeNull();
        module.Description.ShouldNotBeNullOrWhiteSpace();

        Type[] commands = assembly.GetTypes().Where(t => t.IsDefined(typeof(HexalithCommandAttribute))).OrderBy(t => t.Name).ToArray();
        commands.ShouldBe(new[] { typeof(CreatePartyComposite), typeof(DeactivateParty), typeof(ReactivateParty), typeof(UpdatePartyComposite) });
        Type[] queries = assembly.GetTypes().Where(t => t.IsDefined(typeof(HexalithQueryAttribute))).OrderBy(t => t.Name).ToArray();
        queries.ShouldBe(new[] { typeof(GetParty), typeof(ListParties), typeof(SearchParties) });
        foreach (Type type in queries)
        {
            HexalithQueryAttribute query = type.GetCustomAttribute<HexalithQueryAttribute>()!;
            query.Domain.ShouldBe("party");
            query.WireType.ShouldBe(type == typeof(GetParty) ? "PartyDetail" : type == typeof(ListParties) ? "PartyIndex" : "PartySearch");
            query.ProjectionType.ShouldBe(type == typeof(GetParty) ? "party-detail" : "party-index");
            query.ProjectionActorType.ShouldBe(type == typeof(GetParty) ? "PartyDetailProjectionQueryActor" : "PartyIndexProjectionQueryActor");
            query.AggregateIdProperty.ShouldBe(type == typeof(GetParty) ? "PartyId" : null);
            query.AggregateId.ShouldBe(type == typeof(GetParty) ? null : "parties");
            query.TenantProperty.ShouldBeNull();
            query.Description.ShouldNotBeNullOrWhiteSpace();
            query.Example.ShouldNotBeNullOrWhiteSpace();
            JsonSerializer.Deserialize(query.Example!, type, new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }).ShouldNotBeNull();
            type.GetProperty("Page").ShouldBeNull();
            type.GetProperty("PageSize").ShouldBeNull();
        }
        foreach (Type type in commands)
        {
            HexalithCommandAttribute command = type.GetCustomAttribute<HexalithCommandAttribute>()!;
            command.Domain.ShouldBe("party");
            command.AggregateIdProperty.ShouldBe("PartyId");
            command.WireType.ShouldBeNull();
            command.TenantProperty.ShouldBeNull();
            command.CorrelationProperty.ShouldBeNull();
            command.IdempotencyKeyProperty.ShouldBeNull();
            command.ActorProperty.ShouldBeNull();
            command.Description.ShouldNotBeNullOrWhiteSpace();
            command.Name.ShouldBe(type == typeof(CreatePartyComposite) ? "create-party" : type == typeof(UpdatePartyComposite) ? "update-party" : null);
            command.Example.ShouldBe(type == typeof(CreatePartyComposite)
                ? """{"PartyId":"party-49","Type":"Person","PersonDetails":{"FirstName":"Ada","LastName":"Lovelace"}}"""
                : type == typeof(UpdatePartyComposite)
                    ? """{"PartyId":"party-49","PersonDetails":{"FirstName":"Augusta Ada","LastName":"Lovelace"}}"""
                    : """{"PartyId":"party-49"}""");
            type.GetInterfaces().ShouldNotContain(i => i.Name == "ICommandContract");

            JsonSerializerOptions options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
            object example = JsonSerializer.Deserialize(command.Example!, type, options)!;
            type.GetProperty("PartyId")!.GetValue(example).ShouldBe("party-49");
            type.GetProperty("PartyId")!.IsDefined(typeof(HexalithIdentifierAttribute)).ShouldBeTrue();
        }
    }

    [Fact]
    public void EveryExposedMemberIncludingNestedMembersHasADescriptionAndScalarIdsAreMarked()
    {
        Type[] types = [typeof(CreatePartyComposite), typeof(UpdatePartyComposite), typeof(ReactivateParty), typeof(DeactivateParty),
            typeof(GetParty), typeof(ListParties), typeof(SearchParties), typeof(AddContactChannel), typeof(UpdateContactChannel), typeof(AddIdentifier), typeof(PersonDetails), typeof(OrganizationDetails)];
        foreach (PropertyInfo property in types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
        {
            (property.GetCustomAttribute<DescriptionAttribute>()?.Description).ShouldNotBeNullOrWhiteSpace($"{property.DeclaringType!.Name}.{property.Name}");
            if (property.Name is "PartyId" or "ContactChannelId" or "IdentifierId")
            {
                property.PropertyType.ShouldBe(typeof(string));
                property.IsDefined(typeof(HexalithIdentifierAttribute)).ShouldBeTrue();
            }
        }
    }
}
