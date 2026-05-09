namespace Hexalith.Parties.Tests.FitnessTests;

using System.Text.RegularExpressions;

using Shouldly;

// Story 12.0 spike fitness tests: snapshot the EventStore-to-Parties remote-invocation blockers.
// DELETE WHEN STORY 12.1 LANDS — once AppHost recomposition adds a separate `eventstore` resource and a
// Parties `/process` endpoint, these tests' "current state" assertions become semantically stale and
// their hard-coded source paths (e.g. `src/Hexalith.Parties/Program.cs`) may move. Promote any still-
// useful invariants into the 12.1+ test suites and remove this file as part of 12.1's DOD.
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

        Regex.IsMatch(source, @"AddProject<\s*Projects\.\S+\s*>\s*\(\s*""parties""").ShouldBeTrue();
        Regex.IsMatch(source, @"AddProject<\s*Projects\.\S+\s*>\s*\(\s*""eventstore""").ShouldBeFalse();
    }

    [Fact]
    public void PartiesRuntimeDoesNotExposeEventStoreDomainServiceProcessEndpoint()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties",
            "Program.cs"));

        // Catch MapPost / MapMethods variants and case differences on `/process`.
        Regex.IsMatch(source, @"\b(?:MapPost|MapMethods|MapGet|MapPut)\s*\(\s*""/?[Pp]rocess""").ShouldBeFalse();
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
        // Match `AddEventStoreServer(` regardless of the parameter name (`configuration`, `builder.Configuration`, etc.).
        int eventStoreServerIndex = source.IndexOf(
            "AddEventStoreServer(",
            StringComparison.Ordinal);

        customInvokerIndex.ShouldBeGreaterThanOrEqualTo(0);
        eventStoreServerIndex.ShouldBeGreaterThanOrEqualTo(0);
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

        // Match `IProjectionActor` as a whole word NOT preceded by `Party` (avoids matching local
        // `IPartyDetailProjectionActor` / `IPartyIndexProjectionActor`). Also catches inheritance
        // from EventStore's abstract base via `: ProjectionActor` form.
        Regex eventStoreContract = new(@"(?<!\w)(?:I?ProjectionActor)\b(?!\w)");
        Regex localPartyContract = new(@"\bIParty\w*ProjectionActor\b");

        bool DetectsEventStoreContract(string source)
        {
            // Ignore matches that are actually the local Party-prefixed interface name.
            string stripped = localPartyContract.Replace(source, string.Empty);
            return eventStoreContract.IsMatch(stripped);
        }

        DetectsEventStoreContract(detailSource).ShouldBeFalse();
        DetectsEventStoreContract(indexSource).ShouldBeFalse();
        detailSource.ShouldContain("IPartyDetailProjectionActor");
        indexSource.ShouldContain("IPartyIndexProjectionActor");
    }
}
