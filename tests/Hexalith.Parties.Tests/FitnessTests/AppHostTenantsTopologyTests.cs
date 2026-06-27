using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class AppHostTenantsTopologyTests
{
    [Fact]
    public void AppHostProjectReferencesEventStoreTenantsAndAspireProjects()
    {
        string project = ReadAppHostProject();

        project.ShouldContain(@"$(HexalithEventStoreRoot)\src\Hexalith.EventStore\Hexalith.EventStore.csproj");
        project.ShouldContain(@"$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Admin.Server.Host\Hexalith.EventStore.Admin.Server.Host.csproj");
        project.ShouldContain(@"$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Admin.UI\Hexalith.EventStore.Admin.UI.csproj");
        project.ShouldContain(@"$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj");
        project.ShouldContain(@"$(HexalithTenantsRoot)\src\Hexalith.Tenants\Hexalith.Tenants.csproj");
        project.ShouldContain(@"Hexalith.Parties.Mcp\Hexalith.Parties.Mcp.csproj");
        project.ShouldContain(@"Hexalith.Parties.UI\Hexalith.Parties.UI.csproj");
        project.ShouldNotContain(@"references\Hexalith.Tenants\src\Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj");
        project.ShouldContain(@"$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj"" IsAspireProjectResource=""false""");
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
        program.ShouldContain("AddHexalithEventStoreSecurity()");
        program.ShouldNotContain(@"AddKeycloak(""keycloak"", 8180)");
        program.ShouldMatch(@"adminUI\s*=\s*builder\.AddProject<Projects\.Hexalith_EventStore_Admin_UI>\(""eventstore-admin-ui""\)\s*\.WithExplicitStart\(\)");
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
        program.ShouldContain(@"ResolveDaprConfigPath(""accesscontrol.memories.yaml"")");
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
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__wildcard_party_v1__AppId"",\s*""parties""\)");
        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__wildcard_party_v1__MethodName"",\s*""process""\)");
        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__wildcard_party_v1__TenantId"",\s*""\*""\)");
        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__wildcard_party_v1__Domain"",\s*""party""\)");
        program.ShouldMatch(
            @"WithEnvironment\(""EventStore__DomainServices__Registrations__wildcard_party_v1__Version"",\s*""v1""\)");
    }

    [Fact]
    public void AppHostProgramWiresSecurityToEventStoreAdminPartiesMcpUiAndTenants()
    {
        string program = ReadAppHostProgram();
        string legacyTacheIssuer = "http://auth." + "tache.ai:8080/realms/tache";

        program.ShouldContain(@"const string PublishModeJwtIssuer = ""https://auth.tache.ai/realms/tache"";");
        program.ShouldNotContain(legacyTacheIssuer);
        program.ShouldContain(@"string requireHttpsMetadata = builder.ExecutionContext.IsPublishMode ? ""true"" : ""false"";");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Authority"", runModeAuthority)");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Issuer"", runModeAuthority)");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Authority"", publishModeAuthority ?? publishModeIssuer)");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Issuer"", publishModeIssuer)");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__RequireHttpsMetadata"", requireHttpsMetadata)");
        program.ShouldContain(@"WithEnvironment(""ASPNETCORE_ENVIRONMENT"", ""Development"")");
        program.ShouldContain(@"WithEnvironment(""DOTNET_ENVIRONMENT"", ""Development"")");
        program.ShouldContain("WithJwtAuthentication(eventStore, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)");
        program.ShouldContain("WithJwtAuthentication(adminServer, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)");
        program.ShouldContain("WithJwtAuthentication(parties, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)");
        program.ShouldContain("WithJwtAuthentication(partiesMcp, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)");
        program.ShouldContain("WithJwtAuthentication(tenants, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Audience"", ""hexalith-eventstore"")");
        program.ShouldContain(@"WithEnvironment(""Authentication__JwtBearer__Audience"", ""hexalith-parties"")");
        program.ShouldContain("eventStore.WithSecurityDependency(security)");
        program.ShouldContain("adminServer.WithSecurityDependency(security)");
        program.ShouldContain("parties.WithSecurityDependency(security)");
        program.ShouldContain("partiesMcp.WithSecurityDependency(security)");
        program.ShouldContain("tenants.WithSecurityDependency(security)");
        program.ShouldContain("adminUI.WithSecurityDependency(security)");
        program.ShouldContain("partiesUi.WithSecurityDependency(security)");
        Regex adminUiBaseUrl = new(@"adminUI[\s\S]*?WithEnvironment\(""EventStore__AdminServer__BaseUrl"",\s*ReferenceExpression\.Create\(\$""\{adminServer\.GetEndpoint\(""http""\)\}""\)\)");
        adminUiBaseUrl.Matches(program).Count.ShouldBe(
            1,
            "EventStore Admin UI must receive the Admin Server HTTP base URL on the unconditional path.");
        Regex adminUiSwaggerUrl = new(@"adminUI[\s\S]*?WithEnvironment\(""EventStore__AdminServer__SwaggerUrl"",\s*ReferenceExpression\.Create\(\$""\{adminServer\.GetEndpoint\(""http""\)\}/swagger/index\.html""\)\)");
        adminUiSwaggerUrl.Matches(program).Count.ShouldBe(
            1,
            "EventStore Admin UI must receive the Admin Server Swagger URL on the unconditional path.");

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
        string legacyPublicIssuerConstant = "PublishModePublic" + "KeycloakIssuer";

        program.ShouldContain(@"WithEnvironment(""EventStore__Authentication__Authority"", PublishModeJwtAuthority)");
        program.ShouldContain(@"WithEnvironment(""EventStore__Authentication__Issuer"", PublishModeJwtIssuer)");
        program.ShouldNotContain(legacyPublicIssuerConstant);
        program.ShouldContain(@"WithEnvironment(""EventStore__Authentication__Audience"", ""hexalith-eventstore"")");
        program.ShouldContain(@"WithEnvironment(""EventStore__Authentication__ClientId"", ""hexalith-eventstore"")");
        program.ShouldContain(@"WithEnvironment(""EventStore__Authentication__ClientCredentialsClientId"", ""hexalith-eventstore-ui"")");
        program.ShouldContain(@"WithEnvironment(""EventStore__SignalR__HubUrl"", ReferenceExpression.Create($""{eventStore.GetEndpoint(""http"")}/hubs/projection-changes""))");
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
        program.ShouldMatch(@"partiesMcp\s*=\s*builder\.AddProject<Projects\.Hexalith_Parties_Mcp>\(""parties-mcp""\)\s*\.WithExplicitStart\(\)");
        program.ShouldMatch(@"partiesMcp[\s\S]*?\.WithReference\(eventStore\)[\s\S]*?\.WaitFor\(eventStore\)");
        program.ShouldMatch(@"partiesMcp[\s\S]*?\.WithReference\(parties\)[\s\S]*?\.WaitFor\(parties\)");
        program.ShouldContain(
            @"WithEnvironment(""Parties__Mcp__EventStoreGatewayBaseUrl"", ReferenceExpression.Create($""{eventStore.GetEndpoint(""http"")}""))");
    }

    [Fact]
    public void AppHostProgramDoesNotManageAspireRedisAndReliesOnDaprPersistence()
    {
        string program = StripCSharpComments(ReadAppHostProgram());

        // Persistence is the DAPR state store / pub-sub layer. Redis is provided by `dapr init` at
        // 127.0.0.1:6379 and wired into the statestore/pubsub component metadata by
        // AddHexalithEventStore — it is NOT managed by Aspire. The AppHost must not compose an
        // Aspire Redis resource nor pass a redis argument to AddHexalithEventStore.
        program.ShouldNotContain("AddRedis(");
        program.ShouldNotContain("RedisResource");
        program.ShouldNotContain("redis: redis");
        program.ShouldContain("WithReference(eventStoreResources.StateStore)");
        program.ShouldContain("WithReference(eventStoreResources.PubSub)");
    }

    [Fact]
    public void AppHostProgramComposesMemoriesOnlyWhenRichSearchIsEnabled()
    {
        string program = StripCSharpComments(ReadAppHostProgram());

        program.ShouldContain(@"builder.Configuration[""EnableMemoriesSearch""]");
        program.ShouldContain(@"AddProject(""memories"", memoriesProjectPath)");
        program.ShouldContain("ResolveOptionalReferenceProjectPath");
        program.ShouldContain("Run 'git submodule update --init {normalizedSubmodulePath}'");
        program.ShouldContain("Do not use recursive submodule initialization for the default local run.");
        program.ShouldContain(@"WithEnvironment(""ConnectionStrings__falkordb"", ""falkordb:6379"")");
        program.ShouldMatch(@"if\s*\(builder\.ExecutionContext\.IsPublishMode[\s\S]*?string\.Equals\(builder\.Configuration\[""EnableMemoriesSearch""\][\s\S]*?AddProject\(""memories"", memoriesProjectPath\)");
        program.ShouldNotContain(@"AddProject<Projects.Hexalith_Memories_Server>(""memories"")");

        string daprDir = Path.Combine(RepositoryRoot.Locate(), "src", "Hexalith.Parties.AppHost", "DaprComponents");
        File.Exists(Path.Combine(daprDir, "accesscontrol.memories.yaml")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(daprDir, "statestore.yaml")).ShouldContain("- memories");
        File.ReadAllText(Path.Combine(daprDir, "pubsub.yaml")).ShouldContain("- memories");
    }

    [Fact]
    public void AppHostProgramUsesWaitForForDependencyReadiness()
    {
        string program = StripCSharpComments(ReadAppHostProgram());

        program.ShouldContain(".WaitFor(eventStore)");
        program.ShouldContain(".WaitFor(tenants)");
        program.ShouldContain(".WaitFor(eventStoreResources.StateStore)");
        program.ShouldContain(".WaitFor(eventStoreResources.PubSub)");
        program.ShouldNotContain(".WaitForStart(");
    }

    [Fact]
    public void AppHostProjectFailsMissingRootLevelSubmodulesWithActionableGuidance()
    {
        string project = ReadAppHostProject();

        project.ShouldContain("HexalithEventStoreBasePath");
        project.ShouldContain("HexalithTenantsBasePath");
        project.ShouldNotContain("HexalithMemoriesBasePath");
        project.ShouldContain("git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants");
        project.ShouldContain("Do not use recursive submodule initialization for the default local run.");
        project.ShouldNotContain("git -C Hexalith.Memories submodule update");
    }

    [Fact]
    public void LocalRunDocumentationUsesSingleAspireCommandAndRootLevelSubmodulesOnly()
    {
        string root = RepositoryRoot.Locate();
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string gettingStarted = File.ReadAllText(Path.Combine(root, "docs", "getting-started.md"));

        foreach (string document in new[] { readme, gettingStarted })
        {
            document.ShouldContain("dotnet aspire run --project src/Hexalith.Parties.AppHost");
            document.ShouldContain("git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants");
            document.ShouldNotContain("git submodule update --init --recursive");
            document.ShouldNotContain("git submodule update --recursive");
            document.ShouldNotContain("git -C Hexalith.Memories submodule update");
        }
    }

    private static string StripCSharpComments(string source)
    {
        // Remove block comments first, then single-line comments. This prevents fitness
        // assertions like ShouldNotContain(".WaitForStart(") from triggering on commented-
        // out code or XML doc references rather than live wiring.
        string withoutBlockComments = Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlockComments, @"//.*$", string.Empty, RegexOptions.Multiline);
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
        // Group by resource name so duplicate AddProject<>("name") calls produce a clear
        // assertion failure rather than crashing the test with ArgumentException.
        Dictionary<string, string[]> grouped = addProject
            .Matches(program)
            .GroupBy(match => match.Groups["name"].Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(match => match.Groups["project"].Value).ToArray(),
                StringComparer.Ordinal);
        string[] duplicates = grouped
            .Where(kvp => kvp.Value.Length > 1)
            .Select(kvp => kvp.Key)
            .ToArray();
        duplicates.ShouldBeEmpty($"AppHost declares duplicate resource names: [{string.Join(", ", duplicates)}].");
        return grouped.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[0], StringComparer.Ordinal);
    }
}
