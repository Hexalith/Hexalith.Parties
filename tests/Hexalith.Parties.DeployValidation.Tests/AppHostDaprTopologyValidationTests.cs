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
    public void PartiesAccessControlAllowsOnlyEventStorePostInvocationOnProcessPath()
    {
        string partiesAccessControl = File.ReadAllText(Path.Combine(AppHostDaprComponentsPath(), "accesscontrol.parties.yaml"));

        partiesAccessControl.ShouldContain("name: accesscontrol-parties");
        partiesAccessControl.ShouldContain("appId: eventstore");
        partiesAccessControl.ShouldContain("name: /process");
        partiesAccessControl.ShouldContain("httpVerb: ['POST']");
        partiesAccessControl.ShouldNotContain("name: /**");
        partiesAccessControl.ShouldNotMatch(@"appId:\s*['""]?\*['""]?", "wildcard appId must not appear");
        partiesAccessControl.ShouldNotContain("httpVerb: ['GET'");
        partiesAccessControl.ShouldNotContain("httpVerb: ['PUT'");
        partiesAccessControl.ShouldNotContain("httpVerb: ['DELETE'");
    }

    [Fact]
    public void AccessControlFilesUseDenyByDefaultPosture()
    {
        string daprDir = AppHostDaprComponentsPath();
        foreach (string fileName in new[]
        {
            "accesscontrol.yaml",
            "accesscontrol.eventstore-admin.yaml",
            "accesscontrol.tenants.yaml",
            "accesscontrol.parties.yaml",
        })
        {
            string content = File.ReadAllText(Path.Combine(daprDir, fileName));
            content.ShouldContain(
                "defaultAction: deny",
                customMessage: $"{fileName} must use defaultAction: deny at the spec level (deny-by-default posture).");
            content.ShouldNotMatch(
                @"defaultAction:\s*allow\s*(#.*)?\s*\n\s*trustDomain",
                $"{fileName} must not use defaultAction: allow at the spec level.");
        }
    }

    [Fact]
    public void EventStoreAccessControlAllowsPartiesAsCommandCaller()
    {
        string content = File.ReadAllText(Path.Combine(AppHostDaprComponentsPath(), "accesscontrol.yaml"));

        content.ShouldContain("appId: eventstore-admin");
        content.ShouldContain("appId: tenants");
        content.ShouldContain(
            "appId: parties",
            customMessage: "Parties must be an explicit allowed caller of EventStore for command/query routing.");
    }

    [Fact]
    public void EventStoreAdminAccessControlIsLockedDown()
    {
        string content = File.ReadAllText(Path.Combine(AppHostDaprComponentsPath(), "accesscontrol.eventstore-admin.yaml"));

        content.ShouldContain("defaultAction: deny");
        content.ShouldContain("policies: []");
    }

    [Fact]
    public void AppHostDaprComponentsDoNotContainPlainTextSecrets()
    {
        string daprDir = AppHostDaprComponentsPath();
        Regex commentLine = new(@"^\s*#.*$", RegexOptions.Multiline);
        Regex secretValue = new(
            @"value:\s*[""']?(?!\{env:)[^""'\s\{][^""'\n]*(?:password|secret|token|bearer|apikey|api[-_]?key)[^""'\n]*[""']?",
            RegexOptions.IgnoreCase);

        foreach (string yamlPath in Directory.GetFiles(daprDir, "*.yaml"))
        {
            string raw = File.ReadAllText(yamlPath);
            string codeOnly = commentLine.Replace(raw, string.Empty);

            secretValue.IsMatch(codeOnly).ShouldBeFalse(
                $"{Path.GetFileName(yamlPath)} contains a literal secret-looking value outside an {{env:...}} placeholder.");
            codeOnly.ShouldNotContain(
                "ConnectionString=",
                Case.Insensitive,
                customMessage: $"{Path.GetFileName(yamlPath)} embeds a connection string literal.");
        }

        // Both pubsub.yaml and statestore.yaml must reference REDIS_PASSWORD
        // through the env-var placeholder; assert per file rather than relying
        // on a concatenated read where one file's reference would mask the other.
        string pubsubText = File.ReadAllText(Path.Combine(daprDir, "pubsub.yaml"));
        string statestoreText = File.ReadAllText(Path.Combine(daprDir, "statestore.yaml"));
        pubsubText.ShouldContain(
            "{env:REDIS_PASSWORD",
            customMessage: "pubsub.yaml must reference REDIS_PASSWORD through an {env:} placeholder.");
        statestoreText.ShouldContain(
            "{env:REDIS_PASSWORD",
            customMessage: "statestore.yaml must reference REDIS_PASSWORD through an {env:} placeholder.");
    }

    [Fact]
    public void AppHostSourceAndLaunchSettingsDoNotContainPlainTextSecrets()
    {
        // Story 12.1 AC8 / Task 5 demands that "generated or loggable deployment
        // output does not include Keycloak credentials, connection strings,
        // bearer tokens, admin passwords, or operator-supplied secrets in plain
        // text." This scans the AppHost source and launchSettings (which are
        // copied to publish output and may surface in dashboard logging).
        // Full `aspire publish` manifest scanning is deferred to Story 12-10.
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull("Could not find Hexalith.Parties solution directory");

        string appHostDir = Path.Combine(solutionDir, "src", "Hexalith.Parties.AppHost");
        Regex secretAssignment = new(
            @"(password|secret|connectionstring|bearer\s|admin[-_]?pass|client[-_]?secret)\s*[:=]\s*[""']?(?!\{env:)[a-zA-Z0-9!@#\$%\^&\*\-_=\+/\\\.]+",
            RegexOptions.IgnoreCase);

        foreach (string sourceFile in Directory.GetFiles(appHostDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(appHostDir, "Properties"), "*.json", SearchOption.AllDirectories)))
        {
            string content = File.ReadAllText(sourceFile);
            secretAssignment.IsMatch(content).ShouldBeFalse(
                $"{Path.GetRelativePath(solutionDir, sourceFile)} appears to contain a literal secret assignment outside an {{env:}} or environment-variable reference.");
        }
    }

    [Fact]
    public void AppHostProgramWiresAdminUiToAdminServerWithExternalEndpoint()
    {
        // AC2: EventStore Admin UI must be wired to Admin Server so Parties
        // events are visible via the stream browser. Static evidence consists
        // of (a) the UI project resource declared in the AppHost composition
        // and (b) a SwaggerUrl env binding pointing at the admin server's
        // HTTPS endpoint, which is how the UI discovers the admin server's
        // OpenAPI surface in both Keycloak-on and Keycloak-off branches.
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull();

        string program = File.ReadAllText(Path.Combine(solutionDir, "src", "Hexalith.Parties.AppHost", "Program.cs"));

        program.ShouldContain(@"AddProject<Projects.Hexalith_EventStore_Admin_UI>(""eventstore-admin-ui"")");
        program.ShouldContain(@"AddHexalithEventStore(");
        program.ShouldContain(@"adminServer.GetEndpoint(""https"")");
        program.ShouldContain(@"EventStore__AdminServer__SwaggerUrl");
        Regex bothBranches = new(@"adminUI[\s\S]*?EventStore__AdminServer__SwaggerUrl[\s\S]*?else[\s\S]*?adminUI[\s\S]*?EventStore__AdminServer__SwaggerUrl");
        bothBranches.IsMatch(program).ShouldBeTrue("Admin UI swagger wiring must exist in both Keycloak-on and Keycloak-off branches.");
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
        int maxDepth = 10;
        while (dir is not null && maxDepth-- > 0 && !File.Exists(Path.Combine(dir.FullName, "Hexalith.Parties.slnx")))
        {
            dir = dir.Parent;
        }

        return dir is null || maxDepth < 0 ? null : dir.FullName;
    }
}
