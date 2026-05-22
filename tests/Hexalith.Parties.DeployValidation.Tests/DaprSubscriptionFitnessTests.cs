using YamlDotNet.RepresentationModel;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class DaprSubscriptionFitnessTests
{
    [Fact]
    public void ExactlyTwoDeclarativeSubscriptionsMapExpectedTopicsRoutesScopesAndDeadLetters()
    {
        string[] files = Directory.EnumerateFiles(DeploymentTestPaths.DaprDirectory, "subscription-*.yaml")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        files.ShouldBe(["subscription-parties.yaml", "subscription-tenants.yaml"]);
        AssertSubscription("subscription-parties.yaml", "tenant-a.parties.events", "/events/parties", "deadletter.tenant-a.parties.events", ["sample"]);
        AssertSubscription("subscription-tenants.yaml", "system.tenants.events", "/tenants/events", "deadletter.system.tenants.events", ["parties"]);
    }

    [Fact]
    public void ResiliencyTargetsReferenceOnlyDeclaredBoundedPolicies()
    {
        string resiliency = DeploymentTestPaths.ReadRepoFile("deploy/dapr/resiliency.yaml");

        foreach (string secondsValue in Regex.Matches(resiliency, @"(?m)^\s+(?:duration|maxInterval|timeout|daprSidecar|pubsubTimeout|subscriberTimeout):\s*(?<value>\d+)s\s*$").Select(static match => match.Groups["value"].Value))
        {
            int.Parse(secondsValue, System.Globalization.CultureInfo.InvariantCulture).ShouldBeInRange(1, 60);
        }

        resiliency.ShouldContain("eventstore:");
        resiliency.ShouldContain("parties:");
        resiliency.ShouldContain("tenants:");
        resiliency.ShouldContain("memories:");
        resiliency.ShouldContain("pubsub:");
        resiliency.ShouldContain("statestore:");
        resiliency.ShouldNotContain("eventstore-admin-ui:");
        resiliency.ShouldNotContain("parties-mcp:");
    }

    private static void AssertSubscription(string fileName, string topic, string route, string deadLetterTopic, string[] scopes)
    {
        using StreamReader reader = File.OpenText(Path.Combine(DeploymentTestPaths.DaprDirectory, fileName));
        YamlStream stream = new();
        stream.Load(reader);
        YamlMappingNode root = (YamlMappingNode)stream.Documents[0].RootNode;
        YamlMappingNode spec = Mapping(root, "spec");

        Scalar(spec, "pubsubname").ShouldBe("pubsub", fileName);
        Scalar(spec, "topic").ShouldBe(topic, fileName);
        Scalar(Mapping(spec, "routes"), "default").ShouldBe(route, fileName);
        Scalar(spec, "deadLetterTopic").ShouldBe(deadLetterTopic, fileName);
        Sequence(root, "scopes").OfType<YamlScalarNode>().Select(static node => node.Value).ToArray().ShouldBe(scopes);
    }

    private static YamlMappingNode Mapping(YamlMappingNode mapping, string key)
        => (YamlMappingNode)mapping.Children[new YamlScalarNode(key)];

    private static YamlSequenceNode Sequence(YamlMappingNode mapping, string key)
        => (YamlSequenceNode)mapping.Children[new YamlScalarNode(key)];

    private static string Scalar(YamlMappingNode mapping, string key)
        => ((YamlScalarNode)mapping.Children[new YamlScalarNode(key)]).Value!;
}
