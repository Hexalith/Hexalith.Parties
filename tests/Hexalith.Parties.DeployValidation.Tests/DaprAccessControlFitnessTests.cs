using YamlDotNet.RepresentationModel;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class DaprAccessControlFitnessTests
{
    private static readonly string s_appHostDaprDirectory = Path.Combine(
        DeploymentTestPaths.RepositoryRoot,
        "src",
        "Hexalith.Parties.AppHost",
        "DaprComponents");

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

    [Fact]
    public void SampleUiAclOperationsMatchDocumentedMatrixInDeployAndAppHostConfigurations()
    {
        foreach (string directory in new[] { DeploymentTestPaths.DaprDirectory, s_appHostDaprDirectory })
        {
            AssertOperations(
                directory,
                "accesscontrol.yaml",
                "sample-blazor-ui",
                [
                    ("POST", "/api/v1/commands"),
                    ("POST", "/api/v1/queries"),
                ]);
            AssertOperations(
                directory,
                directory == DeploymentTestPaths.DaprDirectory ? "accesscontrol-sample.yaml" : "accesscontrol.sample.yaml",
                "eventstore",
                [
                    ("POST", "/process"),
                    ("POST", "/project"),
                    ("POST", "/replay-state"),
                    ("POST", "/admin/operational-index-metadata"),
                ]);
        }
    }

    [Fact]
    public void PartiesAclUsesProjectionQueryActorRouteWithoutQueryServiceInvocation()
    {
        foreach (string directory in new[] { DeploymentTestPaths.DaprDirectory, s_appHostDaprDirectory })
        {
            AssertOperations(
                directory,
                directory == DeploymentTestPaths.DaprDirectory ? "accesscontrol-parties.yaml" : "accesscontrol.parties.yaml",
                "eventstore",
                [
                    ("POST", "/process"),
                ]);
        }
    }

    private static void AssertOperations(string directory, string fileName, string caller, (string Verb, string Route)[] expected)
    {
        YamlMappingNode accessControl = Mapping(Mapping(Load(directory, fileName), "spec"), "accessControl");
        YamlMappingNode policy = Sequence(accessControl, "policies")
            .OfType<YamlMappingNode>()
            .Single(policy => Scalar(policy, "appId") == caller);

        (string Verb, string Route, string Action)[] actual = Sequence(policy, "operations")
            .OfType<YamlMappingNode>()
            .SelectMany(static operation => Sequence(operation, "httpVerb")
                .OfType<YamlScalarNode>()
                .Select(verb => (
                    Verb: Scalar(verb),
                    Route: Scalar(operation, "name"),
                    Action: Scalar(operation, "action"))))
            .OrderBy(static operation => operation.Route, StringComparer.Ordinal)
            .ThenBy(static operation => operation.Verb, StringComparer.Ordinal)
            .ToArray();

        (string Verb, string Route, string Action)[] expectedOperations = expected
            .Select(static operation => (operation.Verb, operation.Route, Action: "allow"))
            .OrderBy(static operation => operation.Route, StringComparer.Ordinal)
            .ThenBy(static operation => operation.Verb, StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expectedOperations, $"{Path.Combine(directory, fileName)} must match the public UI ACL matrix for {caller}.");
    }

    private static YamlMappingNode Load(string fileName)
        => Load(DeploymentTestPaths.DaprDirectory, fileName);

    private static YamlMappingNode Load(string directory, string fileName)
    {
        using StreamReader reader = File.OpenText(Path.Combine(directory, fileName));
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

    private static string Scalar(YamlScalarNode scalar)
        => scalar.Value!;

    private static string Scalar(YamlMappingNode mapping, string key)
        => ((YamlScalarNode)mapping.Children[new YamlScalarNode(key)]).Value!;
}
