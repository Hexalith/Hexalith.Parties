using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class ConsumerProfileDataClientTests
{
    [Fact]
    public async Task GetMyPartyAsync_DelegatesOnlyToSelfScopedAccessor()
    {
        using var cts = new CancellationTokenSource();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        PartyDetail expected = Detail();
        selfScoped.GetMyPartyAsync(cts.Token).Returns(expected);
        var sut = new ConsumerProfileDataClient(selfScoped);

        PartyDetail actual = await sut.GetMyPartyAsync(cts.Token);

        actual.ShouldBeSameAs(expected);
        await selfScoped.Received(1).GetMyPartyAsync(cts.Token);
    }

    [Fact]
    public void ConsumerProfileDataClient_IsRegisteredAsScopedAdapter()
    {
        var services = new ServiceCollection();
        services.AddScoped<IConsumerProfileDataClient, ConsumerProfileDataClient>();

        services.ShouldContain(static descriptor =>
            descriptor.ServiceType == typeof(IConsumerProfileDataClient)
            && descriptor.ImplementationType == typeof(ConsumerProfileDataClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static PartyDetail Detail()
        => new()
        {
            Id = "party-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
}
