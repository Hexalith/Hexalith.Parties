using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public sealed class McpCliDecorationTests
{
    [Fact]
    public void ModuleDeclaresOnlyTheFourApprovedCommandsAndExistingRouting()
    {
        Assembly assembly = typeof(CreatePartyComposite).Assembly;
        HexalithModuleAttribute module = assembly.GetCustomAttribute<HexalithModuleAttribute>()!;
        module.Name.ShouldBe("parties");
        module.IdentifierKind.ShouldBe(IdentifierKind.String);
        module.WireTypeConvention.ShouldBe(WireTypeConvention.FullTypeName);
        module.FixedTenant.ShouldBeNull();
        module.Description.ShouldNotBeNullOrWhiteSpace();

        Type[] commands = assembly.GetTypes().Where(t => t.IsDefined(typeof(HexalithCommandAttribute))).OrderBy(t => t.Name).ToArray();
        commands.ShouldBe(new[] { typeof(CreatePartyComposite), typeof(DeactivateParty), typeof(ReactivateParty), typeof(UpdatePartyComposite) });
        assembly.GetTypes().Where(t => t.IsDefined(typeof(HexalithQueryAttribute))).ShouldBeEmpty();
        foreach (Type type in commands)
        {
            HexalithCommandAttribute command = type.GetCustomAttribute<HexalithCommandAttribute>()!;
            command.Domain.ShouldBe("party");
            command.AggregateIdProperty.ShouldBe("PartyId");
            command.WireType.ShouldBeNull();
            command.TenantProperty.ShouldBeNull();
            command.Description.ShouldNotBeNullOrWhiteSpace();
            command.Name.ShouldBe(type == typeof(CreatePartyComposite) ? "create-party" : type == typeof(UpdatePartyComposite) ? "update-party" : null);
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
            typeof(AddContactChannel), typeof(UpdateContactChannel), typeof(AddIdentifier), typeof(PersonDetails), typeof(OrganizationDetails)];
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
