extern alias apphost;

using System.Text.Json.Nodes;

using Shouldly;

using CatalogConfiguration = apphost::Hexalith.Parties.AppHost.PartiesDevelopmentCatalogConfiguration;
using Resolver = apphost::Hexalith.Parties.AppHost.ReferenceProjectResolver;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks private transport narrowing against real owner declarations without proposing any catalog or authority.</summary>
public sealed class DevelopmentCatalogConfigurationTests
{
    /// <summary>The technical receiver retains every existing bounded owner grant for SDK endpoint qualification.</summary>
    [Fact]
    public void GatewayRetainsAllExistingTechnicalGrantsForSdkQualification()
    {
        string source = Source("accesscontrol.yaml");
        byte[] original = File.ReadAllBytes(source);
        var rendered = CatalogConfiguration.Receiver(source, Declaration("Hexalith.EventStore"), "gateway", Apps, auxiliary: false, technical: true);
        rendered.Invocations.Select(rule => rule!["CallerAppId"]!.GetValue<string>() + ":" + rule["Method"]!.GetValue<string>())
            .Order(StringComparer.Ordinal).ShouldBe(new[]
            {
                "eventstore-admin:/**", "tenants:/**", "parties:/**",
                "sample-blazor-ui:/api/v1/commands", "sample-blazor-ui:/api/v1/queries",
            }.Order(StringComparer.Ordinal));
        rendered.Invocations.Single(rule => rule!["CallerAppId"]!.GetValue<string>() == "eventstore-admin")!["HttpMethods"]!.ToJsonString()
            .ShouldBe("[\"GET\",\"POST\",\"PUT\"]");
        rendered.Configuration.ToJsonString().ShouldNotContain("timesheets");
        File.ReadAllBytes(source).ShouldBe(original);
        Should.Throw<InvalidDataException>(() => CatalogConfiguration.Receiver(source, Declaration("Hexalith.EventStore"), "gateway", Apps, auxiliary: false))
            .Message.ShouldContain("no qualified owner-declared finite transport");
    }

    /// <summary>Both source grants and independent invocation observations retain all verbs on a repeated path.</summary>
    [Fact]
    public void DuplicatePathsMergeVerbsInBothConfigurationAndInvocationObservations()
    {
        WithTemporaryConfiguration(source =>
        {
            JsonArray operations = source["spec"]!["accessControl"]!["policies"]![0]!["operations"]!.AsArray();
            operations.Clear();
            operations.Add(new JsonObject { ["name"] = "/finite", ["httpVerb"] = new JsonArray("POST"), ["action"] = "allow" });
            operations.Add(new JsonObject { ["name"] = "/finite", ["httpVerb"] = new JsonArray("GET", "POST"), ["action"] = "allow" });
        }, path =>
        {
            var rendered = CatalogConfiguration.Receiver(path, null, null, Apps, auxiliary: false, technical: true);
            rendered.Configuration["spec"]!["accessControl"]!["policies"]![0]!["operations"]!.AsArray().Single()!["httpVerb"]!.ToJsonString()
                .ShouldBe("[\"GET\",\"POST\"]");
            rendered.Invocations.Single(rule => rule!["CallerAppId"]!.GetValue<string>() == "eventstore-admin")!["HttpMethods"]!.ToJsonString()
                .ShouldBe("[\"GET\",\"POST\"]");
        });
    }

    /// <summary>An overlapping explicit deny cannot disappear through expansion or auxiliary-client narrowing.</summary>
    /// <param name="auxiliary">Whether the receiver otherwise has no incoming transport.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DenyBearingInputRefusesBeforeAnyNarrowing(bool auxiliary)
    {
        WithTemporaryConfiguration(source => source["spec"]!["accessControl"]!["policies"]![0]!["operations"]!.AsArray()
            .Add(new JsonObject { ["name"] = "/api/v1/commands", ["httpVerb"] = new JsonArray("POST"), ["action"] = "deny" }),
            path => Should.Throw<InvalidDataException>(() => CatalogConfiguration.Receiver(path,
                Declaration("Hexalith.EventStore"), "gateway", Apps, auxiliary, technical: true)).Message.ShouldContain("Deny-bearing"));
    }

    /// <summary>The UI's private client-only configuration has no incoming grants in the non-mTLS profile.</summary>
    [Fact]
    public void ClientOnlyConfigurationIsAnActualDenyAllInput()
    {
        var rendered = CatalogConfiguration.Receiver(Source("accesscontrol.client-only.yaml"), null, null, Apps, auxiliary: true);
        rendered.Invocations.ShouldBeEmpty();
        rendered.Configuration["spec"]!["accessControl"]!["defaultAction"]!.GetValue<string>().ShouldBe("deny");
        rendered.Configuration["spec"]!["accessControl"]!["policies"]!.AsArray().ShouldBeEmpty();
    }

    /// <summary>Domain wildcard narrowing preserves the actual declared POST receivers and existing readiness probe.</summary>
    [Fact]
    public void TenantsRetainsItsActualDeclaredReceiverMethodsAndReadinessGrant()
    {
        var rendered = CatalogConfiguration.Receiver(Source("accesscontrol.tenants.yaml"), Declaration("Hexalith.Tenants"), "domain", Apps, auxiliary: false);
        rendered.Invocations.Count.ShouldBe(6);
        rendered.Invocations.Where(rule => rule!["CallerAppId"]!.GetValue<string>() == "eventstore")
            .Select(rule => rule!["Method"]!.GetValue<string>()).Order(StringComparer.Ordinal)
            .ShouldBe(new[] { "/process", "/query", "/project", "/replay-state", "/admin/operational-index-metadata" }.Order(StringComparer.Ordinal));
        rendered.Invocations.Single(rule => rule!["CallerAppId"]!.GetValue<string>() == "parties")!["HttpMethods"]!.ToJsonString()
            .ShouldBe("[\"GET\"]");
    }

    /// <summary>The existing Memory API host has no declared domain receiver; the private profile grants it no invented process operation.</summary>
    [Fact]
    public void AuxiliaryMemoryReceiverDeclaresNoIncomingGrantAndPreservesNativeInput()
    {
        string source = Source("accesscontrol.memories.yaml");
        byte[] original = File.ReadAllBytes(source);
        var rendered = CatalogConfiguration.Receiver(source, Declaration("Hexalith.Memories"), "server", Apps, auxiliary: true);
        rendered.Invocations.ShouldBeEmpty();
        rendered.Configuration["spec"]!["accessControl"]!["policies"]!.AsArray().ShouldBeEmpty();
        rendered.Configuration["spec"]!["accessControl"]!["defaultAction"]!.GetValue<string>().ShouldBe("deny");
        File.ReadAllBytes(source).ShouldBe(original);
        File.ReadAllText(source).ShouldContain("/process");
    }

    /// <summary>A default-open policy or unsupported wildcard is refused without a proposal being available.</summary>
    /// <param name="mutation">The transport input mutation.</param>
    [Theory]
    [InlineData("default-open-receiver")]
    [InlineData("default-open-caller")]
    [InlineData("unsupported-wildcard")]
    public void UnboundedTransportInputsRefuseBeforeCatalogGeneration(string mutation)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "parties-catalog-policy-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonObject source = CatalogConfiguration.Read(Source("accesscontrol.yaml"));
            JsonObject access = source["spec"]!["accessControl"]!.AsObject();
            if (mutation == "default-open-receiver") { access["defaultAction"] = "allow"; }
            else if (mutation == "default-open-caller") { access["policies"]![0]!["defaultAction"] = "allow"; }
            else { access["policies"]![0]!["operations"]![0]!["name"] = "/api/*/process"; }
            File.WriteAllText(temporary, source.ToJsonString());
            Should.Throw<InvalidDataException>(() => CatalogConfiguration.Receiver(temporary, Declaration("Hexalith.EventStore"), "gateway", Apps, auxiliary: false));
        }
        finally { File.Delete(temporary); }
    }

    private static void WithTemporaryConfiguration(Action<JsonObject> mutate, Action<string> verify)
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-policy-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonObject source = CatalogConfiguration.Read(Source("accesscontrol.yaml"));
            mutate(source);
            File.WriteAllText(path, source.ToJsonString());
            verify(path);
        }
        finally { File.Delete(path); }
    }

    private static IReadOnlyDictionary<string, string> Apps { get; } = new Dictionary<string, string>
    {
        ["eventstore"] = "eventstore", ["parties"] = "parties", ["tenants"] = "tenants",
        ["eventstore-admin"] = "eventstore-admin", ["eventstore-admin-ui"] = "eventstore-admin-ui",
        ["sample"] = "sample", ["sample-blazor-ui"] = "sample-blazor-ui", ["memories"] = "memories",
    };

    private static DirectoryInfo Root
    {
        get
        {
            DirectoryInfo? root = new(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) { root = root.Parent; }
            return root ?? throw new InvalidOperationException("The owning root is unavailable.");
        }
    }

    private static string Source(string name) => Path.Combine(Root.FullName, "src", "Hexalith.Parties.AppHost", "DaprComponents", name);

    private static string Declaration(string owner) => Resolver.ResolveRequired(Root, owner, Path.Combine("module", "hexalith.module-manifest.v2.json"));
}
