using YamlDotNet.RepresentationModel;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class DaprAccessControlFitnessTests
{
    private static readonly IReadOnlyDictionary<string, string[]> s_expectedCallersByConfig =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["accesscontrol.yaml"] = ["eventstore-admin", "parties", "sample-blazor-ui", "tenants"],
            ["accesscontrol-parties.yaml"] = ["eventstore"],
            ["accesscontrol-sample.yaml"] = ["eventstore"],
            ["accesscontrol-tenants.yaml"] = ["eventstore", "parties"],
            ["accesscontrol-eventstore-admin.yaml"] = ["eventstore-admin-ui"],
            ["accesscontrol-memories.yaml"] = [],
        };

    [Fact]
    public void AllAclConfigurationsDenyByDefaultAndUseExplicitCallers()
    {
        foreach ((string fileName, string[] callers) in s_expectedCallersByConfig)
        {
            YamlMappingNode accessControl = Mapping(Mapping(Load(fileName), "spec"), "accessControl");

            Scalar(accessControl, "defaultAction").ShouldBe("deny", fileName);
            string[] actualCallers = Sequence(accessControl, "policies")
                .OfType<YamlMappingNode>()
                .Select(static policy => Scalar(policy, "appId"))
                .Order(StringComparer.Ordinal)
                .ToArray();

            actualCallers.ShouldBe(callers.Order(StringComparer.Ordinal).ToArray(), $"{fileName} has a documented caller set.");
            actualCallers.ShouldAllBe(static appId => appId != "*" && appId != "parties-mcp" && appId != "redis" && appId != "keycloak" && appId != "falkordb");
        }
    }

    [Fact]
    public void AclOperationsAllowDocumentedPrefixesButRejectGlobalOrLooseWildcards()
    {
        foreach (string fileName in s_expectedCallersByConfig.Keys)
        {
            YamlMappingNode accessControl = Mapping(Mapping(Load(fileName), "spec"), "accessControl");
            foreach (YamlMappingNode operation in Sequence(accessControl, "policies")
                .OfType<YamlMappingNode>()
                .SelectMany(static policy => Sequence(policy, "operations").OfType<YamlMappingNode>()))
            {
                string route = Scalar(operation, "name");

                route.ShouldNotBe("*", fileName);
                route.ShouldNotBe("/**", fileName);
                route.EndsWith("/*", StringComparison.Ordinal).ShouldBeFalse($"{fileName} route '{route}' must not use loose one-segment wildcards.");
                if (route.EndsWith("/**", StringComparison.Ordinal))
                {
                    route.StartsWith("/api/v1/", StringComparison.Ordinal).ShouldBeTrue($"{fileName} may use only documented EventStore gateway prefix wildcards.");
                }
            }
        }
    }

    [Fact]
    public void RouteMapMatchesDocumentedTopology()
    {
        Dictionary<string, string[]> allowedReceiversByCaller = new(StringComparer.Ordinal);
        foreach ((string fileName, _) in s_expectedCallersByConfig)
        {
            string receiver = fileName switch
            {
                "accesscontrol.yaml" => "eventstore",
                "accesscontrol-eventstore-admin.yaml" => "eventstore-admin",
                "accesscontrol-memories.yaml" => "memories",
                "accesscontrol-parties.yaml" => "parties",
                "accesscontrol-sample.yaml" => "sample",
                "accesscontrol-tenants.yaml" => "tenants",
                _ => throw new InvalidOperationException(fileName),
            };

            YamlMappingNode accessControl = Mapping(Mapping(Load(fileName), "spec"), "accessControl");
            foreach (YamlMappingNode policy in Sequence(accessControl, "policies").OfType<YamlMappingNode>())
            {
                string caller = Scalar(policy, "appId");
                allowedReceiversByCaller[caller] = [.. allowedReceiversByCaller.GetValueOrDefault(caller, []), receiver];
            }
        }

        allowedReceiversByCaller["parties"].Order(StringComparer.Ordinal).ToArray().ShouldBe(["eventstore", "tenants"], "Memories search updates use the in-cluster Memories Service URL, not Dapr service invocation.");
        allowedReceiversByCaller["tenants"].ShouldBe(["eventstore"]);
        allowedReceiversByCaller["eventstore-admin-ui"].ShouldBe(["eventstore-admin"]);
        allowedReceiversByCaller["sample-blazor-ui"].ShouldBe(["eventstore"]);
        allowedReceiversByCaller.Values.SelectMany(static receivers => receivers).ShouldNotContain("memories");
        allowedReceiversByCaller.ShouldNotContainKey("parties-mcp");
    }

    private static YamlMappingNode Load(string fileName)
    {
        using StreamReader reader = File.OpenText(Path.Combine(DeploymentTestPaths.DaprDirectory, fileName));
        YamlStream stream = new();
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static YamlMappingNode Mapping(YamlMappingNode mapping, string key)
        => (YamlMappingNode)mapping.Children[new YamlScalarNode(key)];

    private static YamlSequenceNode Sequence(YamlMappingNode mapping, string key)
        => mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value)
            ? (YamlSequenceNode)value
            : [];

    private static string Scalar(YamlMappingNode mapping, string key)
        => ((YamlScalarNode)mapping.Children[new YamlScalarNode(key)]).Value!;
}
