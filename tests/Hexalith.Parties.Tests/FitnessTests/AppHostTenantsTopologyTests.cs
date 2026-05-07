using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class AppHostTenantsTopologyTests
{
    [Fact]
    public void AppHostProjectReferencesTenantsServiceAndAspireProjects()
    {
        string project = ReadAppHostProject();

        project.ShouldContain(@"Hexalith.Tenants\src\Hexalith.Tenants\Hexalith.Tenants.csproj");
        project.ShouldContain(@"Hexalith.Tenants\src\Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj");
        project.ShouldContain(@"IsAspireProjectResource=""false""");
    }

    [Fact]
    public void AppHostProgramComposesTenantsWithStableResourceNameAndTenantsDependency()
    {
        string program = ReadAppHostProgram();

        program.ShouldContain(@"AddProject<Projects.Hexalith_Parties>(""parties"")");
        program.ShouldContain(@"AddProject<Projects.Hexalith_Tenants>(""tenants"")");
        program.ShouldContain("AddHexalithTenants");
        program.ShouldContain("WithReference(tenantsResources.CommandApi)");
        program.ShouldContain("WaitFor(tenantsResources.CommandApi)");
    }

    [Fact]
    public void PartiesAspireExtensionAcceptsSharedDaprComponentsForTenantsComposition()
    {
        string extension = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.Aspire",
            "HexalithPartiesExtensions.cs"));

        extension.ShouldContain("IResourceBuilder<IDaprComponentResource> stateStore");
        extension.ShouldContain("IResourceBuilder<IDaprComponentResource> pubSub");
        extension.ShouldContain("AddHexalithParties(parties, daprConfigPath, stateStore, pubSub)");
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
