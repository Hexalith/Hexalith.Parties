namespace Hexalith.Parties.DeployValidation.Tests;

public sealed class AppHostDaprTopologyValidationTests
{
    [Fact]
    public void AppHostDaprComponentsContainRequiredSplitAccessControlFiles()
    {
        string daprDir = AppHostDaprComponentsPath();

        File.Exists(Path.Combine(daprDir, "accesscontrol.yaml")).ShouldBeTrue();
        File.Exists(Path.Combine(daprDir, "accesscontrol.eventstore-admin.yaml")).ShouldBeTrue();
        File.Exists(Path.Combine(daprDir, "accesscontrol.tenants.yaml")).ShouldBeTrue();
        File.Exists(Path.Combine(daprDir, "accesscontrol.parties.yaml")).ShouldBeTrue();
        File.Exists(Path.Combine(daprDir, "resiliency.yaml")).ShouldBeTrue();
    }

    [Fact]
    public void AppHostDaprComponentsPreserveStableSharedComponentNamesAndScopes()
    {
        string daprDir = AppHostDaprComponentsPath();
        string stateStore = File.ReadAllText(Path.Combine(daprDir, "statestore.yaml"));
        string pubSub = File.ReadAllText(Path.Combine(daprDir, "pubsub.yaml"));

        stateStore.ShouldContain("name: statestore");
        stateStore.ShouldContain("name: actorStateStore");
        stateStore.ShouldContain("value: \"true\"");
        stateStore.ShouldContain("name: keyPrefix");
        stateStore.ShouldContain("value: \"none\"");
        stateStore.ShouldContain("- eventstore");
        stateStore.ShouldContain("- eventstore-admin");
        stateStore.ShouldContain("- parties");
        stateStore.ShouldContain("- tenants");

        pubSub.ShouldContain("name: pubsub");
        pubSub.ShouldContain("- eventstore");
        pubSub.ShouldContain("- parties");
        pubSub.ShouldContain("- tenants");
        pubSub.ShouldContain("parties=system.tenants.events");
    }

    [Fact]
    public void PartiesAccessControlAllowsOnlyEventStorePostInvocationPath()
    {
        string partiesAccessControl = File.ReadAllText(Path.Combine(AppHostDaprComponentsPath(), "accesscontrol.parties.yaml"));

        partiesAccessControl.ShouldContain("name: accesscontrol-parties");
        partiesAccessControl.ShouldContain("appId: eventstore");
        partiesAccessControl.ShouldContain("httpVerb: ['POST']");
        partiesAccessControl.ShouldNotContain("appId: \"*\"");
        partiesAccessControl.ShouldNotContain("httpVerb: ['GET'");
        partiesAccessControl.ShouldNotContain("httpVerb: ['PUT'");
        partiesAccessControl.ShouldNotContain("httpVerb: ['DELETE'");
    }

    [Fact]
    public void AppHostDaprComponentsDoNotContainPlainTextSecrets()
    {
        string allYaml = string.Join(
            Environment.NewLine,
            Directory.GetFiles(AppHostDaprComponentsPath(), "*.yaml").Select(File.ReadAllText));

        allYaml.ShouldNotContain("ConnectionString=", Case.Insensitive);
        allYaml.ShouldNotContain("Bearer ", Case.Insensitive);
        allYaml.ShouldNotContain("admin-pass", Case.Insensitive);
        allYaml.ShouldNotContain("client-secret", Case.Insensitive);
        allYaml.ShouldNotContain("password: ", Case.Insensitive);
        allYaml.ShouldContain("{env:REDIS_PASSWORD}");
    }

    private static string AppHostDaprComponentsPath()
    {
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull("Could not find Hexalith.Parties solution directory");
        return Path.Combine(solutionDir, "src", "Hexalith.Parties.AppHost", "DaprComponents");
    }

    private static string? FindSolutionDirectory()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hexalith.Parties.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }
}
