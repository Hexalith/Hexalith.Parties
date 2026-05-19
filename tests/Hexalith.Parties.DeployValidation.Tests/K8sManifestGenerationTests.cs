namespace Hexalith.Parties.DeployValidation.Tests;

using YamlDotNet.RepresentationModel;

/// <summary>
/// Manifest-shape tests for the aspirate-generated Kubernetes deployment under
/// <c>deploy/k8s/</c> (Story 9-1). These are fitness assertions parsed from the
/// committed YAML; they do not require a live Kubernetes cluster, a DAPR install,
/// or invoking aspirate. They run inside <c>scripts/test.ps1 -Lane deploy</c>.
/// </summary>
public sealed class K8sManifestGenerationTests
{
    private static readonly string[] ExpectedAppIds =
    [
        "eventstore",
        "eventstore-admin",
        "eventstore-admin-ui",
        "parties",
        "parties-mcp",
        "tenants",
    ];

    [Fact]
    public void K8sDirectoryExistsWithExpectedTopLevelLayout()
    {
        string k8sDir = K8sDirectory();
        Directory.Exists(k8sDir).ShouldBeTrue($"deploy/k8s/ must exist (resolved to '{k8sDir}').");

        // Top-level scripts and docs are present and committed alongside aspirate output.
        File.Exists(Path.Combine(k8sDir, "README.md")).ShouldBeTrue(
            "deploy/k8s/README.md must exist and document regen + parity (AC1, AC2, AC3, AC6).");
        File.Exists(Path.Combine(k8sDir, "regen.ps1")).ShouldBeTrue(
            "deploy/k8s/regen.ps1 must exist as the documented regeneration entry point.");
        File.Exists(Path.Combine(k8sDir, "deploy-local.ps1")).ShouldBeTrue(
            "deploy/k8s/deploy-local.ps1 must exist (AC3, AC4).");
        File.Exists(Path.Combine(k8sDir, "teardown-local.ps1")).ShouldBeTrue(
            "deploy/k8s/teardown-local.ps1 must exist (AC6).");

        File.Exists(Path.Combine(k8sDir, "kustomization.yaml")).ShouldBeTrue(
            "Top-level kustomization.yaml must exist (aspirate-emitted).");
        File.Exists(Path.Combine(k8sDir, "namespace.yaml")).ShouldBeTrue(
            "namespace.yaml must exist (aspirate-emitted).");

        // The aspirate-emitted deploy/k8s/dapr/ directory holds only broken
        // placeholders for DAPR component CRs (statestore.yaml with metadata=[],
        // pubsub.yaml with spec.type=pubsub which is invalid). regen.ps1 strips
        // them after aspirate emits, and removes the matching references from
        // the top-level kustomization.yaml. The authoritative DAPR Components
        // live under deploy/dapr/ and are applied directly by deploy-local.ps1.
        Directory.Exists(Path.Combine(k8sDir, "dapr")).ShouldBeFalse(
            "deploy/k8s/dapr/ must NOT exist after regen.ps1 — aspirate emits "
            + "broken placeholders that regen.ps1 strips. The authoritative DAPR "
            + "Components live at deploy/dapr/statestore.yaml + deploy/dapr/pubsub.yaml.");
    }

    [Fact]
    public void DeploymentExistsForEveryAppHostWiredAppId()
    {
        string k8sDir = K8sDirectory();
        foreach (string appId in ExpectedAppIds)
        {
            string deploymentPath = Path.Combine(k8sDir, appId, "deployment.yaml");
            File.Exists(deploymentPath).ShouldBeTrue(
                $"Expected aspirate to emit a Deployment for app id '{appId}' at '{deploymentPath}'. "
                + "Re-run regen.ps1 if this fails after an AppHost change.");

            string servicePath = Path.Combine(k8sDir, appId, "service.yaml");
            File.Exists(servicePath).ShouldBeTrue(
                $"Expected aspirate to emit a Service for app id '{appId}' at '{servicePath}'.");

            string kustomizationPath = Path.Combine(k8sDir, appId, "kustomization.yaml");
            File.Exists(kustomizationPath).ShouldBeTrue(
                $"Expected aspirate to emit a per-app kustomization for app id '{appId}' at '{kustomizationPath}'.");
        }
    }

    [Fact]
    public void DaprEnabledDeploymentsCarryFullAc4Annotations()
    {
        // AC4: every DAPR-enabled Deployment must carry dapr.io/enabled,
        // dapr.io/app-id, dapr.io/app-port, and dapr.io/config (pointing at
        // the per-app access-control Configuration CR). app-port and the
        // per-app config name are injected by regen.ps1 (aspirate 9.1.0 does
        // not emit them); without those injections, sidecar callbacks fail
        // and the access-control deny-by-default contract is bypassed.
        // eventstore-admin-ui and parties-mcp are NOT DAPR-enabled and are
        // intentionally excluded.
        var daprAppToConfig = new Dictionary<string, string>
        {
            { "eventstore", "accesscontrol" },
            { "eventstore-admin", "accesscontrol-eventstore-admin" },
            { "parties", "accesscontrol-parties" },
            { "tenants", "accesscontrol-tenants" },
            // Story 9.3 AC2 — Memories.Server composed in-cluster with deny-by-default ACL.
            { "memories", "accesscontrol-memories" },
        };
        string k8sDir = K8sDirectory();
        foreach ((string daprAppId, string expectedConfig) in daprAppToConfig)
        {
            string deploymentPath = Path.Combine(k8sDir, daprAppId, "deployment.yaml");
            YamlMappingNode root = LoadFirstYamlDocument(deploymentPath);

            // Aspirate emits identical annotation blocks at metadata.annotations
            // AND spec.template.metadata.annotations. The sidecar mutating webhook
            // reads the pod-template block, but a divergence between the two would
            // be a footgun -- assert both carry the same complete set.
            YamlMappingNode topAnnotations = (YamlMappingNode)((YamlMappingNode)root.Children[new YamlScalarNode("metadata")])
                .Children[new YamlScalarNode("annotations")];
            YamlMappingNode podAnnotations = (YamlMappingNode)((YamlMappingNode)((YamlMappingNode)((YamlMappingNode)root
                .Children[new YamlScalarNode("spec")])
                .Children[new YamlScalarNode("template")])
                .Children[new YamlScalarNode("metadata")])
                .Children[new YamlScalarNode("annotations")];

            foreach (var (label, annotations) in new[]
            {
                ("metadata.annotations", topAnnotations),
                ("spec.template.metadata.annotations", podAnnotations),
            })
            {
                ScalarValue(annotations, "dapr.io/enabled").ShouldBe("true",
                    $"{daprAppId} {label}: dapr.io/enabled must equal 'true'.");
                ScalarValue(annotations, "dapr.io/app-id").ShouldBe(daprAppId,
                    $"{daprAppId} {label}: dapr.io/app-id must equal '{daprAppId}'.");
                ScalarValue(annotations, "dapr.io/app-port").ShouldBe("8080",
                    $"{daprAppId} {label}: dapr.io/app-port must equal '8080' (injected by regen.ps1).");
                ScalarValue(annotations, "dapr.io/config").ShouldBe(expectedConfig,
                    $"{daprAppId} {label}: dapr.io/config must equal '{expectedConfig}' "
                    + "(per-app access-control Configuration CR; injected by regen.ps1 -- "
                    + "aspirate's default 'tracing' would silently bypass access control).");
            }
        }
    }

    [Fact]
    public void AspirateBrokenDaprPlaceholdersAreStrippedByRegen()
    {
        // AC2: aspirate 9.1.0 emits broken DAPR component placeholders
        // (statestore.yaml with metadata=[], pubsub.yaml with spec.type=pubsub
        // which is invalid -- DAPR requires pubsub.<backend>). If applied, these
        // would override the authoritative Redis Components in deploy/dapr/.
        // regen.ps1 strips them after aspirate emits, and removes the matching
        // references from the top-level kustomization.yaml. This test enforces
        // that contract: the placeholders MUST NOT survive into the committed
        // tree, and the kustomization MUST NOT reference them.
        string k8sDir = K8sDirectory();
        File.Exists(Path.Combine(k8sDir, "dapr", "statestore.yaml")).ShouldBeFalse(
            "deploy/k8s/dapr/statestore.yaml must NOT exist post-regen. "
            + "If this fails, regen.ps1's post-aspirate cleanup did not run "
            + "or the aspirate placeholder shape changed in a way the cleanup misses.");
        File.Exists(Path.Combine(k8sDir, "dapr", "pubsub.yaml")).ShouldBeFalse(
            "deploy/k8s/dapr/pubsub.yaml must NOT exist post-regen. "
            + "If this fails, regen.ps1's post-aspirate cleanup did not run "
            + "or the aspirate placeholder shape changed in a way the cleanup misses.");

        string kustomization = File.ReadAllText(Path.Combine(k8sDir, "kustomization.yaml"));
        kustomization.ShouldNotContain("dapr/statestore.yaml",
            customMessage: "Top-level kustomization.yaml must NOT reference dapr/statestore.yaml. "
                + "regen.ps1's post-aspirate cleanup is responsible for removing this line.");
        kustomization.ShouldNotContain("dapr/pubsub.yaml",
            customMessage: "Top-level kustomization.yaml must NOT reference dapr/pubsub.yaml. "
                + "regen.ps1's post-aspirate cleanup is responsible for removing this line.");
    }

    [Fact]
    public void AuthoritativeRedisStatestoreCarriesActorStateStoreTrue()
    {
        // AC2 field-level confirmation: the authoritative state store template
        // MUST declare actorStateStore=true (required for DAPR actor state on
        // the Parties actor host), and MUST carry a redisHost metadata entry
        // (without it, the first state operation fails with "missing redisHost").
        string statestorePath = Path.Combine(AuthoritativeDaprDirectory(), "statestore.yaml");
        File.Exists(statestorePath).ShouldBeTrue(
            "deploy/dapr/statestore.yaml must exist as the authoritative state store "
            + "Component (local-cluster default = Redis from `dapr init -k`).");

        YamlMappingNode root = LoadFirstYamlDocument(statestorePath);
        ScalarValue(root, "kind").ShouldBe("Component",
            "statestore.yaml must be a dapr.io Component CR.");
        ScalarValue(root, "apiVersion").ShouldBe("dapr.io/v1alpha1",
            "statestore.yaml must use apiVersion dapr.io/v1alpha1.");
        ScalarValue((YamlMappingNode)root.Children[new YamlScalarNode("spec")], "type")
            .ShouldBe("state.redis", "Local-cluster default state store must be Redis-backed.");

        YamlSequenceNode metadata = (YamlSequenceNode)((YamlMappingNode)root.Children[new YamlScalarNode("spec")])
            .Children[new YamlScalarNode("metadata")];
        bool hasActorStateStoreTrue = metadata.Children
            .OfType<YamlMappingNode>()
            .Any(entry => ScalarValue(entry, "name") == "actorStateStore"
                && ScalarValue(entry, "value") == "true");
        hasActorStateStoreTrue.ShouldBeTrue(
            "statestore.yaml metadata MUST include actorStateStore=true. "
            + "Without it, DAPR actor activation on the Parties actor host fails.");

        bool hasRedisHost = metadata.Children
            .OfType<YamlMappingNode>()
            .Any(entry => ScalarValue(entry, "name") == "redisHost");
        hasRedisHost.ShouldBeTrue(
            "statestore.yaml metadata MUST include redisHost. Without it, the "
            + "first state operation fails with 'missing redisHost'.");
    }

    [Fact]
    public void AuthoritativeRedisPubsubCarriesEnableDeadLetterTrue()
    {
        // AC2 field-level confirmation: the authoritative pub/sub template
        // MUST declare enableDeadLetter=true (platform contract, validated by
        // Story 8.1) and MUST use a valid type (pubsub.<backend>) rather than
        // aspirate's invalid placeholder type 'pubsub'.
        string pubsubPath = Path.Combine(AuthoritativeDaprDirectory(), "pubsub.yaml");
        File.Exists(pubsubPath).ShouldBeTrue(
            "deploy/dapr/pubsub.yaml must exist as the authoritative pub/sub "
            + "Component (local-cluster default = Redis Streams from `dapr init -k`).");

        YamlMappingNode root = LoadFirstYamlDocument(pubsubPath);
        ScalarValue(root, "kind").ShouldBe("Component",
            "pubsub.yaml must be a dapr.io Component CR.");
        ScalarValue(root, "apiVersion").ShouldBe("dapr.io/v1alpha1",
            "pubsub.yaml must use apiVersion dapr.io/v1alpha1.");

        string type = ScalarValue((YamlMappingNode)root.Children[new YamlScalarNode("spec")], "type");
        type.ShouldStartWith("pubsub.", customMessage:
            $"pubsub.yaml spec.type='{type}' is invalid. DAPR requires "
            + "pubsub.<backend> (e.g., pubsub.redis, pubsub.kafka). Bare 'pubsub' "
            + "is the aspirate placeholder shape and DAPR rejects it.");

        YamlSequenceNode metadata = (YamlSequenceNode)((YamlMappingNode)root.Children[new YamlScalarNode("spec")])
            .Children[new YamlScalarNode("metadata")];
        bool hasEnableDeadLetterTrue = metadata.Children
            .OfType<YamlMappingNode>()
            .Any(entry => ScalarValue(entry, "name") == "enableDeadLetter"
                && ScalarValue(entry, "value") == "true");
        hasEnableDeadLetterTrue.ShouldBeTrue(
            "pubsub.yaml metadata MUST include enableDeadLetter=true (platform "
            + "contract, validated by Story 8.1).");
    }

    [Fact]
    public void AuthoritativeDaprTemplatesRemainTheBackingComponentSource()
    {
        // AC2: deploy/dapr/ remains the authoritative DAPR component template
        // directory (variant-specific state-store / pub-sub backends, access
        // control per app id, subscriptions, resiliency, topology). The deploy
        // script applies these alongside the aspirate-emitted set; alternative
        // backends are operator opt-in (see deploy-local.ps1 filter).
        string daprDir = AuthoritativeDaprDirectory();
        foreach (string fileName in new[]
        {
            "statestore.yaml",
            "pubsub.yaml",
            "statestore-cosmosdb.yaml",
            "statestore-postgresql.yaml",
            "pubsub-kafka.yaml",
            "pubsub-rabbitmq.yaml",
            "pubsub-servicebus.yaml",
            "accesscontrol.yaml",
            "accesscontrol.eventstore-admin.yaml",
            "accesscontrol.parties.yaml",
            "accesscontrol.tenants.yaml",
            "subscription-parties.yaml",
            "subscription-tenants.yaml",
            "resiliency.yaml",
            "topology.yaml",
            "tenants-integration.yaml",
        })
        {
            File.Exists(Path.Combine(daprDir, fileName)).ShouldBeTrue(
                $"deploy/dapr/{fileName} must remain present as the authoritative DAPR component template.");
        }
    }

    [Fact]
    public void K8sReadmeDocumentsAspirateVersionPinAndRegenCommand()
    {
        string readme = File.ReadAllText(Path.Combine(K8sDirectory(), "README.md"));

        // Aspirate is pinned via dotnet-tools.json — README must reference the
        // pinned version and the regen entry point.
        readme.ShouldContain("aspirate",
            customMessage: "README.md must reference aspirate.");
        readme.ShouldContain("9.1.0",
            customMessage: "README.md must document the pinned aspirate version (9.1.0).");
        readme.ShouldContain("dotnet tool restore",
            customMessage: "README.md must instruct developers to restore the local tool manifest.");
        readme.ShouldContain("regen.ps1",
            customMessage: "README.md must reference the regen.ps1 entry point (AC1).");
        readme.ShouldContain("deploy-local.ps1",
            customMessage: "README.md must reference deploy-local.ps1 (AC3).");
        readme.ShouldContain("teardown-local.ps1",
            customMessage: "README.md must reference teardown-local.ps1 (AC6).");
    }

    [Fact]
    public void K8sReadmeDocumentsLocalClusterAllowlist()
    {
        // AC3: the local-cluster allowlist must be documented in the README so
        // operators know which kubectl contexts the deploy script will accept.
        string readme = File.ReadAllText(Path.Combine(K8sDirectory(), "README.md"));
        readme.ShouldContain("kind-", customMessage: "README must list 'kind-*' in the local allowlist.");
        readme.ShouldContain("k3d-", customMessage: "README must list 'k3d-*' in the local allowlist.");
        readme.ShouldContain("minikube", customMessage: "README must list 'minikube' in the local allowlist.");
        readme.ShouldContain("docker-desktop", customMessage: "README must list 'docker-desktop' in the local allowlist.");
    }

    [Fact]
    public void K8sReadmeDocumentsDaprComponentParityTable()
    {
        // AC2: the README carries the manual parity table mapping deploy/dapr/
        // templates to deploy/k8s/dapr/ analogs (or noting absence). Story 9.2
        // will automate this lint.
        string readme = File.ReadAllText(Path.Combine(K8sDirectory(), "README.md"));
        readme.ShouldContain("DAPR component parity",
            customMessage: "README must include a 'DAPR component parity' section per AC2.");
        readme.ShouldContain("deploy/dapr/",
            customMessage: "README must reference deploy/dapr/ as the authoritative source.");
        readme.ShouldContain("Story 9.2",
            customMessage: "README must defer automated parity lint to Story 9.2.");
    }

    [Fact]
    public void DotnetToolsManifestPinsAspirate()
    {
        string toolsManifest = Path.Combine(SolutionRoot(), ".config", "dotnet-tools.json");
        File.Exists(toolsManifest).ShouldBeTrue(
            ".config/dotnet-tools.json must exist (Task 2 — aspirate pin).");
        string content = File.ReadAllText(toolsManifest);
        content.ShouldContain("\"aspirate\"",
            customMessage: "dotnet-tools.json must declare the aspirate tool.");
        content.ShouldContain("\"9.1.0\"",
            customMessage: "dotnet-tools.json must pin aspirate at the version documented in deploy/k8s/README.md.");
        content.ShouldContain("\"rollForward\": true",
            customMessage: "Aspirate 9.1.0 targets net9.0; rollForward must be true so it runs on the .NET 10 SDK.");
    }

    [Fact]
    public void AppHostProgramKeepsPublishTargetBlockOrthogonalToAspirate()
    {
        // Task 9: keep the PUBLISH_TARGET=k8s / AddKubernetesEnvironment block.
        // The block is Aspire's native publisher and is orthogonal to aspirate's
        // generation pipeline (decision recorded in Completion Notes and inline
        // comment).
        string program = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Hexalith.Parties.AppHost", "Program.cs"));
        program.ShouldContain("AddKubernetesEnvironment",
            customMessage: "Program.cs must keep AddKubernetesEnvironment for the PUBLISH_TARGET=k8s branch.");
        program.ShouldContain("orthogonal to the aspirate",
            customMessage: "Program.cs must explain that AddKubernetesEnvironment is orthogonal to aspirate.");
    }

    private static string SolutionRoot()
    {
        string? dir = FindSolutionDirectory();
        dir.ShouldNotBeNull("Could not locate Hexalith.Parties solution root (look for *.slnx).");
        return dir;
    }

    private static string K8sDirectory() => Path.Combine(SolutionRoot(), "deploy", "k8s");

    private static string AuthoritativeDaprDirectory() => Path.Combine(SolutionRoot(), "deploy", "dapr");

    private static string? FindSolutionDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static YamlMappingNode LoadFirstYamlDocument(string path)
    {
        using var reader = new StreamReader(path);
        var stream = new YamlStream();
        stream.Load(reader);
        stream.Documents.Count.ShouldBeGreaterThan(0, $"{path} must contain at least one YAML document.");
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static string ScalarValue(YamlMappingNode mapping, string key)
    {
        YamlScalarNode keyNode = new(key);
        mapping.Children.ContainsKey(keyNode).ShouldBeTrue(
            $"YAML mapping missing required key '{key}'. Present keys: {string.Join(", ", mapping.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value))}.");
        YamlScalarNode value = (YamlScalarNode)mapping.Children[keyNode];
        return value.Value ?? string.Empty;
    }
}
