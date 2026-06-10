using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class ConsumerProfileEditClientTests
{
    [Fact]
    public async Task UpdateMyProfileAsync_DelegatesOnlyToSelfScopedWriteMethod()
    {
        using var cts = new CancellationTokenSource();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        PartyDetail expectedDetail = Detail();
        selfScoped.UpdateMyProfileAsync(Arg.Any<SelfScopedProfileUpdateRequest>(), cts.Token)
            .Returns(new PartiesCommandResult<PartyDetail>("corr-1", expectedDetail));
        var sut = new ConsumerProfileEditClient(selfScoped);
        var request = new ConsumerProfileUpdateRequest
        {
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
            },
        };

        ConsumerProfileUpdateResult actual = await sut.UpdateMyProfileAsync(request, cts.Token);

        actual.Outcome.ShouldBe(ConsumerProfileUpdateOutcome.Accepted);
        actual.Detail.ShouldBeSameAs(expectedDetail);
        await selfScoped.Received(1).UpdateMyProfileAsync(
            Arg.Is<SelfScopedProfileUpdateRequest>(selfRequest =>
                selfRequest != null
                && selfRequest.PersonDetails == request.PersonDetails
                && selfRequest.OrganizationDetails == null),
            cts.Token);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_MapsValidationExceptionToSafeRejectedOutcome()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.UpdateMyProfileAsync(Arg.Any<SelfScopedProfileUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PartiesCommandResult<PartyDetail>>>(_ => throw new PartiesClientException(422, "Validation", null, "Ada is invalid", "corr-1"));
        var sut = new ConsumerProfileEditClient(selfScoped);

        ConsumerProfileUpdateResult actual = await sut.UpdateMyProfileAsync(new ConsumerProfileUpdateRequest());

        actual.Outcome.ShouldBe(ConsumerProfileUpdateOutcome.ValidationRejected);
        actual.ValidationFailures.ShouldNotBeNull();
        actual.ValidationFailures.Count.ShouldBe(1);
        actual.ValidationFailures[0].PropertyName.ShouldBe("Profile");
    }

    [Fact]
    public void ConsumerProfileEditClient_IsRegisteredAsScopedAdapter()
    {
        var services = new ServiceCollection();
        services.AddScoped<IConsumerProfileEditClient, ConsumerProfileEditClient>();

        services.ShouldContain(static descriptor =>
            descriptor.ServiceType == typeof(IConsumerProfileEditClient)
            && descriptor.ImplementationType == typeof(ConsumerProfileEditClient)
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
