using System.ComponentModel;
using System.Reflection;

using Hexalith.Parties.Mcp.Tools;

using ModelContextProtocol.Server;

using Shouldly;

namespace Hexalith.Parties.Mcp.Tests;

public sealed class PartiesMcpToolContractTests
{
    [Fact]
    public void ToolNamesPreserveCanonicalMcpSurface()
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
            "update_party",
        ]);

        toolNames.ShouldNotContain("get_party_name_at");
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

        attributes["create_party"].Destructive.ShouldBeTrue();
        attributes["update_party"].Destructive.ShouldBeTrue();
        attributes["delete_party"].Destructive.ShouldBeTrue();
        attributes["delete_party"].Idempotent.ShouldBeTrue();
    }

    [Fact]
    public void FindPartiesToolDoesNotExposeAdvancedSearchOrTemporalArguments()
    {
        MethodInfo findParties = GetToolMethods()
            .Single(method => string.Equals(method.GetCustomAttribute<McpServerToolAttribute>()?.Name, "find_parties", StringComparison.Ordinal));

        string[] parameterNames =
        [
            .. findParties.GetParameters()
                .Select(parameter => parameter.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!),
        ];

        parameterNames.ShouldBe([
            "query",
            "page",
            "pageSize",
            "type",
            "active",
            "createdAfter",
            "createdBefore",
            "modifiedAfter",
            "modifiedBefore",
            "cancellationToken",
        ]);
        parameterNames.ShouldNotContain(name => name.Contains("semantic", StringComparison.OrdinalIgnoreCase));
        parameterNames.ShouldNotContain(name => name.Contains("hybrid", StringComparison.OrdinalIgnoreCase));
        parameterNames.ShouldNotContain(name => name.Contains("graph", StringComparison.OrdinalIgnoreCase));
        parameterNames.ShouldNotContain(name => name.Contains("temporal", StringComparison.OrdinalIgnoreCase));
        parameterNames.ShouldNotContain(name => name.Contains("asOf", StringComparison.OrdinalIgnoreCase));
        parameterNames.ShouldNotContain(name => name.Contains("case", StringComparison.OrdinalIgnoreCase));

        string description = findParties.GetCustomAttribute<DescriptionAttribute>()!.Description;
        description.ShouldContain("display-name");
        description.ShouldContain("Email");
        description.ShouldContain("identifier");
        description.ShouldContain("semantic");
        description.ShouldContain("not evaluated in MVP");
    }

    [Fact]
    public void CanonicalToolsDoNotExposeInternalActorAdminProjectionOrInfrastructureNames()
    {
        string[] forbiddenTerms =
        [
            "actor",
            "projection",
            "admin",
            "infrastructure",
            "dapr",
            "eventstore-admin",
            "get_party_name_at",
        ];

        foreach (MethodInfo method in GetToolMethods())
        {
            McpServerToolAttribute attribute = method.GetCustomAttribute<McpServerToolAttribute>()!;
            string description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
            string combined = $"{attribute.Name} {attribute.Title} {description}";

            foreach (string forbiddenTerm in forbiddenTerms)
            {
                combined.ShouldNotContain(forbiddenTerm, Case.Insensitive);
            }
        }
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
