using YamlDotNet.RepresentationModel;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class DaprManifestValidationTests
{
    private static readonly string[] s_expectedFiles =
    [
        "accesscontrol-eventstore-admin.yaml",
        "accesscontrol-memories.yaml",
        "accesscontrol-parties.yaml",
        "accesscontrol-tenants.yaml",
        "accesscontrol.yaml",
        "pubsub.yaml",
        "resiliency.yaml",
        "statestore.yaml",
        "subscription-parties.yaml",
        "subscription-tenants.yaml",
    ];

    private static readonly IReadOnlyDictionary<string, (string ApiVersion, string Kind, string Name)> s_expectedHeaders =
        new Dictionary<string, (string ApiVersion, string Kind, string Name)>(StringComparer.Ordinal)
        {
            ["accesscontrol-eventstore-admin.yaml"] = ("dapr.io/v1alpha1", "Configuration", "accesscontrol-eventstore-admin"),
            ["accesscontrol-memories.yaml"] = ("dapr.io/v1alpha1", "Configuration", "accesscontrol-memories"),
            ["accesscontrol-parties.yaml"] = ("dapr.io/v1alpha1", "Configuration", "accesscontrol-parties"),
            ["accesscontrol-tenants.yaml"] = ("dapr.io/v1alpha1", "Configuration", "accesscontrol-tenants"),
            ["accesscontrol.yaml"] = ("dapr.io/v1alpha1", "Configuration", "accesscontrol"),
            ["pubsub.yaml"] = ("dapr.io/v1alpha1", "Component", "pubsub"),
            ["resiliency.yaml"] = ("dapr.io/v1alpha1", "Resiliency", "resiliency"),
            ["statestore.yaml"] = ("dapr.io/v1alpha1", "Component", "statestore"),
            ["subscription-parties.yaml"] = ("dapr.io/v2alpha1", "Subscription", "parties-events-reference"),
            ["subscription-tenants.yaml"] = ("dapr.io/v2alpha1", "Subscription", "tenant-lifecycle-events"),
        };

    [Fact]
    public void DaprFolderContainsOnlyExpectedProductionCrFiles()
    {
        Directory.Exists(DaprDirectory).ShouldBeTrue("Story 9.4 owns the root deploy/dapr production CR set.");

        string[] entries = Directory.EnumerateFileSystemEntries(DaprDirectory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        entries.ShouldBe(s_expectedFiles, "deploy/dapr must contain only the production CR files; no subdirectories, generated output, or local placeholders.");
    }

    [Fact]
    public void DaprManifestsHaveExpectedHeaders()
    {
        foreach ((string fileName, (string apiVersion, string kind, string name)) in s_expectedHeaders)
        {
            YamlMappingNode root = LoadRoot(fileName);

            Scalar(root, "apiVersion").ShouldBe(apiVersion, fileName);
            Scalar(root, "kind").ShouldBe(kind, fileName);
            Scalar(Mapping(root, "metadata"), "name").ShouldBe(name, fileName);
        }
    }

    [Fact]
    public void RedisComponentsArePasswordlessScopedAndUseClusterServiceDns()
    {
        YamlMappingNode statestore = LoadRoot("statestore.yaml");
        YamlMappingNode pubsub = LoadRoot("pubsub.yaml");

        AssertComponent(statestore, "state.redis", "statestore.yaml");
        AssertComponent(pubsub, "pubsub.redis", "pubsub.yaml");

        AssertMetadataValue(statestore, "redisHost", "redis:6379");
        AssertMetadataValue(pubsub, "redisHost", "redis:6379");
        AssertNoMetadata(statestore, "redisPassword", "redisPasswordFromSecret", "secretKeyRef");
        AssertNoMetadata(pubsub, "redisPassword", "redisPasswordFromSecret", "secretKeyRef");
        AssertNoCredentialMetadata(statestore, "statestore.yaml");
        AssertNoCredentialMetadata(pubsub, "pubsub.yaml");
        AssertMetadataValue(statestore, "actorStateStore", "true");
        AssertMetadataValue(statestore, "keyPrefix", "none");
        AssertMetadataValue(pubsub, "enableDeadLetter", "true");

        SequenceValues(statestore, "scopes").ShouldBe(["eventstore", "eventstore-admin", "parties", "tenants", "memories"]);
        SequenceValues(pubsub, "scopes").ShouldBe(["eventstore", "parties", "tenants"]);
        MetadataValue(pubsub, "publishingScopes").ShouldBe("eventstore=tenant-a.parties.events;tenants=system.tenants.events");
        MetadataValue(pubsub, "subscriptionScopes").ShouldBe("parties=system.tenants.events");
    }

    [Fact]
    public void AccessControlConfigurationsAreDenyByDefaultAndDoNotUseWildcards()
    {
        foreach (string fileName in s_expectedFiles.Where(static f => f.StartsWith("accesscontrol", StringComparison.Ordinal)))
        {
            YamlMappingNode root = LoadRoot(fileName);
            YamlMappingNode accessControl = Mapping(Mapping(root, "spec"), "accessControl");

            Scalar(accessControl, "defaultAction").ShouldBe("deny", fileName);
            if (fileName is "accesscontrol-eventstore-admin.yaml" or "accesscontrol-memories.yaml")
            {
                Sequence(accessControl, "policies").Children.ShouldBeEmpty($"{fileName} must deny all peer invocation until a concrete Dapr route contract exists.");
            }

            foreach (YamlMappingNode policy in Sequence(accessControl, "policies").OfType<YamlMappingNode>())
            {
                string appId = Scalar(policy, "appId");
                appId.ShouldNotBe("*", fileName);
                appId.ShouldNotBe("parties-mcp", fileName);
                appId.ShouldNotBe("eventstore-admin-ui", fileName);
                appId.ShouldNotBe("redis", fileName);
                appId.ShouldNotBe("keycloak", fileName);

                foreach (YamlMappingNode operation in Sequence(policy, "operations").OfType<YamlMappingNode>())
                {
                    Scalar(operation, "name").ShouldNotBe("/**", fileName);
                }
            }
        }
    }

    [Fact]
    public void DeclarativeSubscriptionsTargetPubsubWithExplicitTopicRouteScopeAndDeadLetter()
    {
        AssertSubscription(
            "subscription-parties.yaml",
            "tenant-a.parties.events",
            "/events/parties",
            "deadletter.tenant-a.parties.events",
            ["sample"]);

        AssertSubscription(
            "subscription-tenants.yaml",
            "system.tenants.events",
            "/tenants/events",
            "deadletter.system.tenants.events",
            ["parties"]);
    }

    [Fact]
    public void ResiliencyPoliciesAreBoundedAndReferencedByTargets()
    {
        YamlMappingNode root = LoadRoot("resiliency.yaml");
        YamlMappingNode spec = Mapping(root, "spec");
        YamlMappingNode policies = Mapping(spec, "policies");
        YamlMappingNode targets = Mapping(spec, "targets");

        HashSet<string> declaredPolicies = [];
        foreach (string policySection in new[] { "retries", "timeouts", "circuitBreakers" })
        {
            YamlMappingNode section = Mapping(policies, policySection);
            foreach (KeyValuePair<YamlNode, YamlNode> entry in section.Children)
            {
                declaredPolicies.Add(((YamlScalarNode)entry.Key).Value!);
            }
        }

        HashSet<string> referencedPolicies = [];
        CollectScalarValues(Mapping(targets, "apps"), referencedPolicies);
        CollectScalarValues(Mapping(targets, "components"), referencedPolicies);

        declaredPolicies.ShouldAllBe(policy => referencedPolicies.Contains(policy), "Every declared resiliency policy must be attached to a real target.");
        referencedPolicies.ShouldAllBe(policy => declaredPolicies.Contains(policy), "Every resiliency target reference must point to a declared policy.");

        foreach (YamlMappingNode retry in Mapping(policies, "retries").Children.Values.OfType<YamlMappingNode>())
        {
            int maxRetries = int.Parse(Scalar(retry, "maxRetries"), System.Globalization.CultureInfo.InvariantCulture);
            maxRetries.ShouldBeInRange(0, 10);
        }

        foreach (KeyValuePair<YamlNode, YamlNode> timeout in Mapping(policies, "timeouts").Children)
        {
            int seconds = ParseSeconds(((YamlScalarNode)timeout.Value).Value!, ((YamlScalarNode)timeout.Key).Value!);
            seconds.ShouldBeInRange(1, 60);
        }

        foreach (YamlMappingNode breaker in Mapping(policies, "circuitBreakers").Children.Values.OfType<YamlMappingNode>())
        {
            int maxRequests = int.Parse(Scalar(breaker, "maxRequests"), System.Globalization.CultureInfo.InvariantCulture);
            maxRequests.ShouldBeInRange(1, 10);
            ParseSeconds(Scalar(breaker, "interval"), "circuit breaker interval").ShouldBeInRange(1, 60);
            ParseSeconds(Scalar(breaker, "timeout"), "circuit breaker timeout").ShouldBeInRange(1, 60);
            Scalar(breaker, "trip").ShouldNotBeNullOrWhiteSpace();
        }

        Scalar(Mapping(policies, "timeouts"), "daprSidecar").ShouldBe("5s");

        Mapping(Mapping(targets, "apps"), "eventstore").ShouldNotBeNull();
        Mapping(Mapping(targets, "apps"), "parties").ShouldNotBeNull();
        Mapping(Mapping(targets, "apps"), "tenants").ShouldNotBeNull();
        Mapping(Mapping(targets, "apps"), "memories").ShouldNotBeNull();
        Mapping(Mapping(targets, "components"), "pubsub").ShouldNotBeNull();
        Mapping(Mapping(targets, "components"), "statestore").ShouldNotBeNull();
    }

    private static string RepositoryRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
        }
    }

    private static string DaprDirectory => Path.Combine(RepositoryRoot, "deploy", "dapr");

    private static YamlMappingNode LoadRoot(string fileName)
    {
        string path = Path.Combine(DaprDirectory, fileName);
        File.Exists(path).ShouldBeTrue($"Missing Dapr manifest: {fileName}");

        using StreamReader reader = File.OpenText(path);
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1, $"{fileName} must contain exactly one Kubernetes resource document.");
        stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>($"{fileName} must have a mapping document root.");
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static void AssertComponent(YamlMappingNode root, string type, string fileName)
    {
        YamlMappingNode spec = Mapping(root, "spec");
        Scalar(spec, "type").ShouldBe(type, fileName);
        Scalar(spec, "version").ShouldBe("v1", fileName);
    }

    private static void AssertSubscription(
        string fileName,
        string topic,
        string route,
        string deadLetterTopic,
        string[] scopes)
    {
        YamlMappingNode root = LoadRoot(fileName);
        YamlMappingNode spec = Mapping(root, "spec");

        Scalar(spec, "pubsubname").ShouldBe("pubsub", fileName);
        Scalar(spec, "topic").ShouldBe(topic, fileName);
        Scalar(Mapping(spec, "routes"), "default").ShouldBe(route, fileName);
        Scalar(spec, "deadLetterTopic").ShouldBe(deadLetterTopic, fileName);
        SequenceValues(root, "scopes").ShouldBe(scopes);
    }

    private static void AssertMetadataValue(YamlMappingNode root, string name, string expected)
        => MetadataValue(root, name).ShouldBe(expected, name);

    private static void AssertNoMetadata(YamlMappingNode root, params string[] names)
    {
        HashSet<string> metadataNames = Metadata(root)
            .Select(static item => Scalar(item, "name"))
            .ToHashSet(StringComparer.Ordinal);

        foreach (string name in names)
        {
            metadataNames.ShouldNotContain(name);
        }
    }

    private static void AssertNoCredentialMetadata(YamlMappingNode root, string fileName)
    {
        foreach (YamlMappingNode item in Metadata(root))
        {
            string name = Scalar(item, "name");
            name.Contains("password", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(fileName);
            name.Contains("secret", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(fileName);
            name.Contains("auths", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(fileName);

            if (item.Children.TryGetValue(new YamlScalarNode("value"), out YamlNode? valueNode)
                && valueNode is YamlScalarNode value
                && !string.IsNullOrWhiteSpace(value.Value))
            {
                value.Value.Contains("Bearer eyJ", StringComparison.Ordinal).ShouldBeFalse(fileName);
            }
        }
    }

    private static string MetadataValue(YamlMappingNode root, string name)
        => Scalar(
            Metadata(root).Single(item => string.Equals(Scalar(item, "name"), name, StringComparison.Ordinal)),
            "value");

    private static IEnumerable<YamlMappingNode> Metadata(YamlMappingNode root)
        => Sequence(Mapping(root, "spec"), "metadata").OfType<YamlMappingNode>();

    private static string[] SequenceValues(YamlMappingNode mapping, string key)
        => Sequence(mapping, key)
            .OfType<YamlScalarNode>()
            .Select(static node => node.Value!)
            .ToArray();

    private static YamlMappingNode Mapping(YamlMappingNode mapping, string key)
        => (YamlMappingNode)mapping.Children[new YamlScalarNode(key)];

    private static YamlSequenceNode Sequence(YamlMappingNode mapping, string key)
        => (YamlSequenceNode)mapping.Children[new YamlScalarNode(key)];

    private static string Scalar(YamlMappingNode mapping, string key)
        => ((YamlScalarNode)mapping.Children[new YamlScalarNode(key)]).Value!;

    private static int ParseSeconds(string value, string policyName)
    {
        value.EndsWith('s').ShouldBeTrue($"{policyName} must use a bounded seconds value.");
        return int.Parse(value[..^1], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void CollectScalarValues(YamlNode node, ISet<string> values)
    {
        switch (node)
        {
            case YamlScalarNode scalar when !string.IsNullOrWhiteSpace(scalar.Value):
                values.Add(scalar.Value);
                break;
            case YamlMappingNode mapping:
                foreach (YamlNode child in mapping.Children.Values)
                {
                    CollectScalarValues(child, values);
                }

                break;
            case YamlSequenceNode sequence:
                foreach (YamlNode child in sequence.Children)
                {
                    CollectScalarValues(child, values);
                }

                break;
        }
    }
}
