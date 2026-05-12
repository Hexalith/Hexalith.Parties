using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class AppHostTenantsTopologyTests
{
    [Fact]
    public void AppHostProjectReferencesEventStoreTenantsAndAspireProjects()
    {
        string project = ReadAppHostProject();

        project.ShouldContain(@"Hexalith.EventStore\src\Hexalith.EventStore\Hexalith.EventStore.csproj");
        project.ShouldContain(@"Hexalith.EventStore\src\Hexalith.EventStore.Admin.Server.Host\Hexalith.EventStore.Admin.Server.Host.csproj");
        project.ShouldContain(@"Hexalith.EventStore\src\Hexalith.EventStore.Admin.UI\Hexalith.EventStore.Admin.UI.csproj");
        project.ShouldContain(@"Hexalith.EventStore\src\Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj");
        project.ShouldContain(@"Hexalith.Tenants\src\Hexalith.Tenants\Hexalith.Tenants.csproj");
        project.ShouldContain(@"Hexalith.Parties.Mcp\Hexalith.Parties.Mcp.csproj");
        project.ShouldNotContain(@"Hexalith.Tenants\src\Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj");
        project.ShouldContain(@"Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj"" IsAspireProjectResource=""false""");
        project.ShouldContain(@"IsAspireProjectResource=""false""");
    }

    [Fact]
    public void AppHostProgramComposesStandaloneEventStoreTopologyWithStableResourceNames()
    {
        string program = ReadAppHostProgram();

        Dictionary<string, string> resources = ExtractProjectResources(program);

        resources["eventstore"].ShouldBe("Projects.Hexalith_EventStore");
        resources["eventstore-admin"].ShouldBe("Projects.Hexalith_EventStore_Admin_Server_Host");
        resources["eventstore-admin-ui"].ShouldBe("Projects.Hexalith_EventStore_Admin_UI");
        resources["parties"].ShouldBe("Projects.Hexalith_Parties");
        resources["tenants"].ShouldBe("Projects.Hexalith_Tenants");
        resources["parties-mcp"].ShouldBe("Projects.Hexalith_Parties_Mcp");
        program.ShouldContain("AddHexalithEventStore");
        program.ShouldNotContain("AddHexalithParties(");
        program.ShouldNotContain("AddHexalithTenants(");
    }

    [Fact]
    public void AppHostProgramUsesSplitDaprConfigurationFiles()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain(@"ResolveDaprConfigPath(""accesscontrol.yaml"")");
        program.ShouldContain(@"ResolveDaprConfigPath(""accesscontrol.eventstore-admin.yaml"")");
        program.ShouldContain(@"ResolveDaprConfigPath(""accesscontrol.tenants.yaml"")");
        program.ShouldContain(@"ResolveDaprConfigPath(""accesscontrol.parties.yaml"")");
        program.ShouldContain(@"ResolveDaprConfigPath(""resiliency.yaml"")");
        program.ShouldContain("eventStoreAccessControlConfigPath");
        program.ShouldContain("adminServerAccessControlConfigPath");
        program.ShouldContain("tenantsAccessControlConfigPath");
        program.ShouldContain("partiesAccessControlConfigPath");
    }

    [Fact]
    public void AppHostProgramSharesEventStoreDaprComponentsWithPartiesAndTenants()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain("AppId = \"parties\"");
        program.ShouldContain("AppId = \"tenants\"");
        program.ShouldContain("WithReference(eventStoreResources.StateStore)");
        program.ShouldContain("WithReference(eventStoreResources.PubSub)");
        program.ShouldContain(@"WithEnvironment(""Tenants__ServiceName"", ""tenants"")");
        program.ShouldContain(@"WithEnvironment(""Tenants__CommandApiAppId"", ""eventstore"")");
        program.ShouldContain(@"WithEnvironment(""Tenants__PubSubName"", ""pubsub"")");
        program.ShouldContain(@"WithEnvironment(""Tenants__TopicName"", ""system.tenants.events"")");
    }

    [Fact]
    public void AppHostProgramMapsPartyDomainToPartiesActorHost()
    {
        string program = ReadAppHostProgram();

        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__\*\|party\|v1__AppId"",\s*""parties""\)");
        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__\*\|party\|v1__MethodName"",\s*""process""\)");
        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__\*\|party\|v1__Domain"",\s*""party""\)");
        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__\*\|party\|v1__Version"",\s*""v1""\)");
    }

    [Fact]
    public void AppHostProgramWiresKeycloakToEventStoreAdminPartiesAndTenants()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Authority"", realmUrl)");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Issuer"", realmUrl)");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Audience"", ""hexalith-eventstore"")");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Audience"", ""hexalith-parties"")");
        program.ShouldContain("eventStore.WithReference(keycloak)");
        program.ShouldContain("adminServer.WithReference(keycloak)");
        program.ShouldContain("parties.WithReference(keycloak)");
        program.ShouldContain("tenants.WithReference(keycloak)");
        program.ShouldContain("adminUI.WithReference(keycloak)");
        Regex adminUiSwaggerUrl = new(@"adminUI[\s\S]*?WithEnvironment\(""EventStore__AdminServer__SwaggerUrl"",\s*ReferenceExpression\.Create\(\$""\{adminServer\.GetEndpoint\(""https""\)\}/swagger/index\.html""\)\)");
        adminUiSwaggerUrl.Matches(program).Count.ShouldBe(
            2,
            "EventStore Admin UI must receive the Admin Server Swagger URL in both Keycloak-on and Keycloak-off branches.");

        // SigningKey="" must be cleared on every JWT-bearing service to avoid
        // dual-mode auth conflict; assert on count rather than presence so a
        // single-service regression cannot pass.
        int signingKeyClearCount = System.Text.RegularExpressions.Regex.Matches(
            program,
            @"WithEnvironment\(""Authentication__JwtBearer__SigningKey"",\s*""""\)").Count;
        signingKeyClearCount.ShouldBeGreaterThanOrEqualTo(
            4,
            $"Expected SigningKey clearing on at least 4 services (eventstore, eventstore-admin, parties, tenants); found {signingKeyClearCount}.");
    }

    [Fact]
    public void AppHostProgramAcceptsCrossServiceTokensViaValidAudiences()
    {
        // Cross-service DAPR invocations (eventstore -> parties /process,
        // eventstore -> tenants commands) carry tokens minted with the
        // `hexalith-eventstore` audience. The receiver services must list it
        // in TokenValidationParameters.ValidAudiences alongside their own
        // audience to avoid 401s on the invocation hop.
        string program = ReadAppHostProgram();

        program.ShouldContain(@"Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0"", ""hexalith-parties""");
        program.ShouldContain(@"Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__1"", ""hexalith-eventstore""");
        program.ShouldContain(@"Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0"", ""hexalith-tenants""");
    }

    [Fact]
    public void AppHostProgramClearsAdminUiAuthEnvWhenKeycloakDisabled()
    {
        // When EnableKeycloak=false the admin UI must explicitly clear stale
        // OIDC env values so a previous launch's Authority/ClientId cannot
        // leak into the dashboard wiring.
        string program = ReadAppHostProgram();

        program.ShouldContain(@"WithEnvironment(""EventStore__Authentication__Authority"", """")");
        program.ShouldContain(@"WithEnvironment(""EventStore__Authentication__ClientId"", """")");
    }

    [Fact]
    public void AppHostProgramValidatesPublishTargetEnum()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain("Unknown PUBLISH_TARGET");
        program.ShouldContain("InvalidOperationException");
    }

    [Fact]
    public void AppHostProgramWaitsForSharedDaprComponentsOnPartiesAndTenants()
    {
        string program = ReadAppHostProgram();

        int waitForStateStoreCount = System.Text.RegularExpressions.Regex.Matches(
            program,
            @"\.WaitFor\(eventStoreResources\.StateStore\)").Count;
        int waitForPubSubCount = System.Text.RegularExpressions.Regex.Matches(
            program,
            @"\.WaitFor\(eventStoreResources\.PubSub\)").Count;

        waitForStateStoreCount.ShouldBeGreaterThanOrEqualTo(2, "parties and tenants must both WaitFor the shared state store.");
        waitForPubSubCount.ShouldBeGreaterThanOrEqualTo(2, "parties and tenants must both WaitFor the shared pubsub.");
    }

    [Fact]
    public void AppHostProgramWiresPartiesMcpAsSeparateConsumerHost()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain(@"AddProject<Projects.Hexalith_Parties_Mcp>(""parties-mcp"")");
        program.ShouldMatch(@"partiesMcp[\s\S]*?\.WithReference\(eventStore\)[\s\S]*?\.WaitFor\(eventStore\)");
        program.ShouldMatch(@"partiesMcp[\s\S]*?\.WithReference\(parties\)[\s\S]*?\.WaitFor\(parties\)");
        program.ShouldContain(@"WithEnvironment(""Parties__Mcp__EventStoreGatewayBaseUrl""");
    }

    [Fact]
    public void AppHostProgramUsesWaitForForDependencyReadiness()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain(".WaitFor(eventStore)");
        program.ShouldContain(".WaitFor(tenants)");
        program.ShouldContain(".WaitFor(eventStoreResources.StateStore)");
        program.ShouldContain(".WaitFor(eventStoreResources.PubSub)");
        program.ShouldNotContain(".WaitForStart(");
    }

    private static string ReadAppHostProject()
        => File.ReadAllText(RepositoryRoot.ProjectFile("Hexalith.Parties.AppHost"));

    private static string ReadAppHostProgram()
        => File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.AppHost",
            "Program.cs"));

    private static Dictionary<string, string> ExtractProjectResources(string program)
    {
        Regex addProject = new(@"AddProject<(?<project>Projects\.[^>]+)>\(""(?<name>[^""]+)""\)");
        return addProject
            .Matches(program)
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => match.Groups["project"].Value,
                StringComparer.Ordinal);
    }
}
