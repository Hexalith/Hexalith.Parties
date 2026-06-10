using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class ConsumerConsentClientTests
{
    [Fact]
    public async Task GetMyConsentOverviewAsync_ComposesSelfPartyChannelsAndConsentRecords()
    {
        using var cts = new CancellationTokenSource();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        ConsentRecord consent = ActiveConsent("consent-secret", "marketing_emails");
        selfScoped.GetMyPartyAsync(cts.Token).Returns(Detail());
        selfScoped.GetMyConsentAsync(cts.Token).Returns([consent]);
        var sut = new ConsumerConsentClient(selfScoped);

        ConsumerConsentOverview actual = await sut.GetMyConsentOverviewAsync(cts.Token);

        actual.IsErased.ShouldBeFalse();
        actual.Freshness.ShouldNotBeNull();
        actual.ContactChannels.Count.ShouldBe(1);
        actual.ContactChannels[0].ChannelId.ShouldBe("contact-secret");
        actual.ContactChannels[0].Type.ShouldBe(ContactChannelType.Email);
        actual.ConsentRecords.ShouldBe([consent]);
        await selfScoped.Received(1).GetMyPartyAsync(cts.Token);
        await selfScoped.Received(1).GetMyConsentAsync(cts.Token);
    }

    [Fact]
    public async Task GrantMyConsentAsync_DelegatesToSelfScopedConsentMethod()
    {
        using var cts = new CancellationTokenSource();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GrantMyConsentAsync("contact-secret", "marketing_emails", LawfulBasis.Consent, cts.Token)
            .Returns(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-secret"));
        var sut = new ConsumerConsentClient(selfScoped);

        ConsumerConsentOperationResult actual = await sut.GrantMyConsentAsync(
            new ConsumerConsentGrantRequest("contact-secret", "marketing_emails", LawfulBasis.Consent),
            cts.Token);

        actual.Outcome.ShouldBe(ConsumerConsentOperationOutcome.Accepted);
        await selfScoped.Received(1).GrantMyConsentAsync("contact-secret", "marketing_emails", LawfulBasis.Consent, cts.Token);
        await selfScoped.DidNotReceive().GetMyPartyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithdrawMyConsentAsync_DelegatesToSelfScopedRevokeMethod()
    {
        using var cts = new CancellationTokenSource();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.RevokeMyConsentAsync("consent-secret", cts.Token)
            .Returns(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-secret"));
        var sut = new ConsumerConsentClient(selfScoped);

        ConsumerConsentOperationResult actual = await sut.WithdrawMyConsentAsync(
            new ConsumerConsentWithdrawRequest("consent-secret"),
            cts.Token);

        actual.Outcome.ShouldBe(ConsumerConsentOperationOutcome.Accepted);
        await selfScoped.Received(1).RevokeMyConsentAsync("consent-secret", cts.Token);
        await selfScoped.DidNotReceive().GetMyPartyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsentCommands_MapRejectedOutcomesToSafeResults()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GrantMyConsentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<LawfulBasis>(), Arg.Any<CancellationToken>())
            .Returns(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ValidationRejected, "corr-secret", "raw backend detail"));
        var sut = new ConsumerConsentClient(selfScoped);

        ConsumerConsentOperationResult actual = await sut.GrantMyConsentAsync(
            new ConsumerConsentGrantRequest("contact-secret", "marketing_emails", LawfulBasis.Consent));

        actual.Outcome.ShouldBe(ConsumerConsentOperationOutcome.ValidationRejected);
    }

    [Fact]
    public void ConsumerConsentClient_IsRegisteredAsScopedAdapter()
    {
        var services = new ServiceCollection();
        services.AddScoped<IConsumerConsentClient, ConsumerConsentClient>();

        services.ShouldContain(static descriptor =>
            descriptor.ServiceType == typeof(IConsumerConsentClient)
            && descriptor.ImplementationType == typeof(ConsumerConsentClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static PartyDetail Detail()
        => new()
        {
            Id = "party-secret",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "contact-secret",
                    Type = ContactChannelType.Email,
                    Value = "ada@example.test",
                    IsPreferred = true,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
        };

    private static ConsentRecord ActiveConsent(string consentId, string purpose)
        => new()
        {
            ConsentId = consentId,
            ChannelId = "contact-secret",
            Purpose = purpose,
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedBy = "operator-secret",
        };
}
