using System.ComponentModel;
using System.Reflection;

using Hexalith.Parties.Mcp.Tools;

using ModelContextProtocol.Server;

using Shouldly;

namespace Hexalith.Parties.Mcp.Tests;

public sealed class PartiesMcpToolContractTests
{
    [Fact]
    public void ToolNamesPreserveCanonicalMcpSurfaceAndTemporalCompatibilityDecision()
    {
        string[] toolNames = GetToolAttributes()
            .Select(attribute => attribute.Name!)
            .OrderBy(name => name)
            .ToArray();

        toolNames.ShouldBe([
            "create_party",
            "delete_party",
            "find_parties",
            "get_party",
            "get_party_name_at",
            "update_party",
        ]);
    }

    [Fact]
    public void ToolDescriptionsAreAgentFriendlyAndNonEmpty()
    {
        MethodInfo[] toolMethods = GetToolMethods();

        foreach (MethodInfo method in toolMethods)
        {
            DescriptionAttribute? description = method.GetCustomAttribute<DescriptionAttribute>();
            description.ShouldNotBeNull($"{method.Name} must have a tool description.");
            string.IsNullOrWhiteSpace(description.Description).ShouldBeFalse();
            description.Description.ShouldContain("Parties", Case.Insensitive);
        }
    }

    [Fact]
    public void ToolAnnotationsClassifyReadsWritesAndIdempotentDelete()
    {
        IReadOnlyDictionary<string, McpServerToolAttribute> attributes = GetToolAttributes()
            .ToDictionary(attribute => attribute.Name!);

        attributes["get_party"].ReadOnly.ShouldBeTrue();
        attributes["find_parties"].ReadOnly.ShouldBeTrue();
        attributes["get_party_name_at"].ReadOnly.ShouldBeTrue();

        attributes["create_party"].Destructive.ShouldBeTrue();
        attributes["update_party"].Destructive.ShouldBeTrue();
        attributes["delete_party"].Destructive.ShouldBeTrue();
        attributes["delete_party"].Idempotent.ShouldBeTrue();
    }

    [Fact]
    public void ToolMethodsDispatchThroughInstanceInjectedClients()
    {
        MethodInfo[] toolMethods = GetToolMethods();

        toolMethods.ShouldAllBe(method => !method.IsStatic);
    }

    private static MethodInfo[] GetToolMethods()
        => typeof(PartiesMcpTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToArray();

    private static McpServerToolAttribute[] GetToolAttributes()
        => GetToolMethods()
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()!)
            .ToArray();
}
