using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hexalith.Parties.CommandApi.Tests.Mcp;

internal static class McpToolTestServices
{
    public static ServiceProvider BuildForFind(ITenantAccessService access)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>())
            .Returns(indexActor);

        return new ServiceCollection()
            .AddSingleton(access)
            .AddSingleton(actorProxyFactory)
            .AddSingleton<IPartySearchProvider, LocalFuzzyPartySearchProvider>()
            .AddSingleton<IPartySearchService, LocalPartySearchService>()
            .BuildServiceProvider();
    }

    public static ServiceProvider BuildForGet(ITenantAccessService access)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>())
            .Returns(detailActor);

        return new ServiceCollection()
            .AddSingleton(access)
            .AddSingleton(actorProxyFactory)
            .BuildServiceProvider();
    }

    public static ServiceProvider BuildForCreate(ITenantAccessService access, ICommandRouter router)
        => new ServiceCollection()
            .AddSingleton(access)
            .AddSingleton(router)
            .BuildServiceProvider();
}
