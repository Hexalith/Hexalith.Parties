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
        project.ShouldNotContain(@"Hexalith.Tenants\src\Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj");
        project.ShouldContain(@"Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj"" IsAspireProjectResource=""false""");
        project.ShouldContain(@"IsAspireProjectResource=""false""");
    }

    [Fact]
    public void AppHostProgramComposesStandaloneEventStoreTopologyWithStableResourceNames()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain(@"AddProject<Projects.Hexalith_EventStore>(""eventstore"")");
        program.ShouldContain(@"AddProject<Projects.Hexalith_EventStore_Admin_Server_Host>(""eventstore-admin"")");
        program.ShouldContain(@"AddProject<Projects.Hexalith_EventStore_Admin_UI>(""eventstore-admin-ui"")");
        program.ShouldContain(@"AddProject<Projects.Hexalith_Parties>(""parties"")");
        program.ShouldContain(@"AddProject<Projects.Hexalith_Tenants>(""tenants"")");
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

        program.ShouldContain("EventStore__DomainServices__Registrations__*|party|v1__AppId");
        program.ShouldContain("EventStore__DomainServices__Registrations__*|party|v1__MethodName");
        program.ShouldContain("EventStore__DomainServices__Registrations__*|party|v1__Domain");
        program.ShouldContain("EventStore__DomainServices__Registrations__*|party|v1__Version");
        program.ShouldContain(@"""parties""");
        program.ShouldContain(@"""process""");
        program.ShouldContain(@"""party""");
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
        program.ShouldContain(@"WithEnvironment(""EventStore__AdminServer__SwaggerUrl""");

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

    private static string ReadAppHostProject()
        => File.ReadAllText(RepositoryRoot.ProjectFile("Hexalith.Parties.AppHost"));

    private static string ReadAppHostProgram()
        => File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.AppHost",
            "Program.cs"));
}
