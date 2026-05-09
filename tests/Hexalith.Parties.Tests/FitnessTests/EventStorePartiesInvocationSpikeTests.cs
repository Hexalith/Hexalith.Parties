namespace Hexalith.Parties.Tests.FitnessTests;

using Shouldly;

public sealed class EventStorePartiesInvocationSpikeTests
{
    [Fact]
    public void PartiesAppHostDoesNotYetStartSeparateEventStoreApiResource()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.AppHost",
            "Program.cs"));

        source.ShouldContain("builder.AddProject<Projects.Hexalith_Parties>(\"parties\")");
        source.ShouldNotContain("builder.AddProject<Projects.Hexalith_EventStore>(\"eventstore\")");
    }

    [Fact]
    public void PartiesRuntimeDoesNotExposeEventStoreDomainServiceProcessEndpoint()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties",
            "Program.cs"));

        source.ShouldNotContain("MapPost(\"/process\"");
        source.ShouldNotContain("MapPost(\"process\"");
        source.ShouldNotContain("DomainServiceRequestRouter.ProcessAsync");
    }

    [Fact]
    public void PartiesKeepsInProcessDomainInvokerAheadOfEventStoreDaprInvoker()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties",
            "Extensions",
            "PartiesServiceCollectionExtensions.cs"));

        int customInvokerIndex = source.IndexOf(
            "AddTransient<IDomainServiceInvoker, PartyDomainServiceInvoker>",
            StringComparison.Ordinal);
        int eventStoreServerIndex = source.IndexOf(
            "AddEventStoreServer(configuration)",
            StringComparison.Ordinal);

        customInvokerIndex.ShouldBeGreaterThanOrEqualTo(0);
        eventStoreServerIndex.ShouldBeGreaterThan(customInvokerIndex);
    }

    [Fact]
    public void PartiesProjectionActorsDoNotImplementEventStoreGenericProjectionContract()
    {
        string detailSource = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.Projections",
            "Actors",
            "PartyDetailProjectionActor.cs"));
        string indexSource = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.Projections",
            "Actors",
            "PartyIndexProjectionActor.cs"));

        detailSource.ShouldNotContain("IProjectionActor");
        indexSource.ShouldNotContain("IProjectionActor");
        detailSource.ShouldContain("IPartyDetailProjectionActor");
        indexSource.ShouldContain("IPartyIndexProjectionActor");
    }
}
