using System.Security.Claims;
using System.Text.Json;

using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    /// <summary>Returned outcomes preserve safe classifications for both consent operations.</summary>
    [Theory]
    [InlineData(AdminPortalGdprOutcome.Accepted, ConsumerConsentOperationOutcome.Accepted)]
    [InlineData(AdminPortalGdprOutcome.Completed, ConsumerConsentOperationOutcome.Accepted)]
    [InlineData(AdminPortalGdprOutcome.ValidationRejected, ConsumerConsentOperationOutcome.ValidationRejected)]
    [InlineData(AdminPortalGdprOutcome.Forbidden, ConsumerConsentOperationOutcome.Forbidden)]
    [InlineData(AdminPortalGdprOutcome.MissingTenant, ConsumerConsentOperationOutcome.Forbidden)]
    [InlineData(AdminPortalGdprOutcome.AuthenticationRequired, ConsumerConsentOperationOutcome.Forbidden)]
    [InlineData(AdminPortalGdprOutcome.NotFound, ConsumerConsentOperationOutcome.Forbidden)]
    [InlineData(AdminPortalGdprOutcome.ErasureInProgress, ConsumerConsentOperationOutcome.Erased)]
    [InlineData(AdminPortalGdprOutcome.Erased, ConsumerConsentOperationOutcome.Erased)]
    [InlineData(AdminPortalGdprOutcome.TransientFailure, ConsumerConsentOperationOutcome.TransientFailure)]
    [InlineData(AdminPortalGdprOutcome.ContractUnavailable, ConsumerConsentOperationOutcome.TransientFailure)]
    [InlineData(AdminPortalGdprOutcome.Unknown, ConsumerConsentOperationOutcome.Failed)]
    public async Task ConsentCommands_MapReturnedOutcomesToSafeResultsAsync(
        AdminPortalGdprOutcome outcome,
        ConsumerConsentOperationOutcome expected)
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        var response = new AdminPortalGdprCommandResult(outcome, "corr-secret", "raw backend detail");
        selfScoped.GrantMyConsentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<LawfulBasis>(), Arg.Any<CancellationToken>())
            .Returns(response);
        selfScoped.RevokeMyConsentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(response);
        var sut = new ConsumerConsentClient(selfScoped);

        ConsumerConsentOperationResult grant = await sut.GrantMyConsentAsync(
            new ConsumerConsentGrantRequest("contact-secret", "marketing_emails", LawfulBasis.Consent), TestContext.Current.CancellationToken);
        ConsumerConsentOperationResult withdraw = await sut.WithdrawMyConsentAsync(
            new ConsumerConsentWithdrawRequest("consent-secret"), TestContext.Current.CancellationToken);

        grant.Outcome.ShouldBe(expected);
        withdraw.Outcome.ShouldBe(expected);
        JsonSerializer.Serialize(new[] { grant, withdraw }).ShouldNotContain("raw backend detail", Case.Sensitive);
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

    /// <summary>Real client validation is returned safely without issuing a gateway request.</summary>
    [Theory]
    [InlineData(false, "channel/unsafe-sensitive", "marketing_emails")]
    [InlineData(false, "", "marketing_emails")]
    [InlineData(false, "contact-secret", "purpose/unsafe-sensitive")]
    [InlineData(false, "contact-secret", "")]
    [InlineData(true, "consent/unsafe-sensitive", "marketing_emails")]
    [InlineData(true, "", "marketing_emails")]
    public async Task ConsentCommands_RealClientInvalidInput_ReturnsValidationRejectedWithoutHttpAsync(
        bool withdraw,
        string identifier,
        string purpose)
    {
        using var handler = new ConsumerConsentCommandHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.example.test/") };
        ConsumerConsentClient sut = BuildWithHttpClient(httpClient);

        ConsumerConsentOperationResult actual = withdraw
            ? await sut.WithdrawMyConsentAsync(new ConsumerConsentWithdrawRequest(identifier), TestContext.Current.CancellationToken)
            : await sut.GrantMyConsentAsync(new ConsumerConsentGrantRequest(identifier, purpose, LawfulBasis.Consent), TestContext.Current.CancellationToken);

        actual.Outcome.ShouldBe(ConsumerConsentOperationOutcome.ValidationRejected);
        JsonSerializer.Serialize(actual).ShouldNotContain("unsafe-sensitive", Case.Sensitive);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Compatible identifiers still reach the gateway under the resolved consumer identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsentCommands_RealClientValidInput_PreservesBoundIdentityAsync(bool withdraw)
    {
        using var handler = new ConsumerConsentCommandHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.example.test/") };
        ConsumerConsentClient sut = BuildWithHttpClient(httpClient);
        const string channelId = "6f9619ff-8b86-d011-b42d-00c04fc964ff";
        const string purpose = "marketing_emails";
        const string consentId = channelId + ":" + purpose;

        ConsumerConsentOperationResult actual = withdraw
            ? await sut.WithdrawMyConsentAsync(new ConsumerConsentWithdrawRequest(consentId), TestContext.Current.CancellationToken)
            : await sut.GrantMyConsentAsync(new ConsumerConsentGrantRequest(channelId, purpose, LawfulBasis.Consent), TestContext.Current.CancellationToken);

        actual.Outcome.ShouldBe(ConsumerConsentOperationOutcome.Accepted);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].Uri.ShouldBe(new Uri("https://gateway.example.test/api/v1/commands"));
        JsonElement envelope = handler.Requests[0].Envelope;
        envelope.GetProperty("commandType").GetString().ShouldBe(withdraw ? typeof(RevokeConsent).FullName : typeof(RecordConsent).FullName);
        envelope.GetProperty("aggregateId").GetString().ShouldBe("party-secret");
        envelope.GetProperty("tenant").GetString().ShouldBe("tenant-a");
        envelope.GetProperty("domain").GetString().ShouldBe("party");
        JsonElement payload = envelope.GetProperty("payload");
        payload.GetProperty("partyId").GetString().ShouldBe("party-secret");
        payload.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        if (withdraw)
        {
            payload.GetProperty("consentId").GetString().ShouldBe(consentId);
        }
        else
        {
            payload.GetProperty("channelId").GetString().ShouldBe(channelId);
            payload.GetProperty("purpose").GetString().ShouldBe(purpose);
            payload.GetProperty("lawfulBasis").GetString().ShouldBe(nameof(LawfulBasis.Consent));
        }
    }

    /// <summary>Only delegated argument failures become validation rejections.</summary>
    [Theory]
    [InlineData(false, "argument", ConsumerConsentOperationOutcome.ValidationRejected)]
    [InlineData(true, "argument", ConsumerConsentOperationOutcome.ValidationRejected)]
    [InlineData(false, "argument-null", ConsumerConsentOperationOutcome.ValidationRejected)]
    [InlineData(true, "argument-null", ConsumerConsentOperationOutcome.ValidationRejected)]
    [InlineData(false, "http", ConsumerConsentOperationOutcome.Failed)]
    [InlineData(true, "http", ConsumerConsentOperationOutcome.Failed)]
    [InlineData(false, "scope", ConsumerConsentOperationOutcome.Failed)]
    [InlineData(true, "scope", ConsumerConsentOperationOutcome.Failed)]
    [InlineData(false, "cancellation", ConsumerConsentOperationOutcome.Failed)]
    [InlineData(true, "cancellation", ConsumerConsentOperationOutcome.Failed)]
    public async Task ConsentCommands_DelegatedException_PreservesSafeClassificationAsync(
        bool withdraw,
        string exceptionKind,
        ConsumerConsentOperationOutcome expected)
    {
        Exception exception = exceptionKind switch
        {
            "argument" => new ArgumentException("sensitive argument"),
            "argument-null" => new ArgumentNullException("sensitive parameter"),
            "http" => new HttpRequestException("sensitive transport"),
            "scope" => new InvalidOperationException("sensitive scope"),
            "cancellation" => new OperationCanceledException("sensitive timeout"),
            _ => throw new ArgumentOutOfRangeException(nameof(exceptionKind)),
        };
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GrantMyConsentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<LawfulBasis>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminPortalGdprCommandResult>(exception));
        selfScoped.RevokeMyConsentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminPortalGdprCommandResult>(exception));
        var sut = new ConsumerConsentClient(selfScoped);

        ConsumerConsentOperationResult actual = withdraw
            ? await sut.WithdrawMyConsentAsync(new ConsumerConsentWithdrawRequest("consent-secret"), TestContext.Current.CancellationToken)
            : await sut.GrantMyConsentAsync(new ConsumerConsentGrantRequest("contact-secret", "marketing_emails", LawfulBasis.Consent), TestContext.Current.CancellationToken);

        actual.Outcome.ShouldBe(expected);
        JsonSerializer.Serialize(actual).ShouldNotContain("sensitive", Case.Sensitive);
    }

    /// <summary>Cancellation requested by the caller continues to propagate.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsentCommands_CallerCancellation_PropagatesAsync(bool withdraw)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var exception = new OperationCanceledException(cts.Token);
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GrantMyConsentAsync("contact-secret", "marketing_emails", LawfulBasis.Consent, cts.Token)
            .Returns(Task.FromException<AdminPortalGdprCommandResult>(exception));
        selfScoped.RevokeMyConsentAsync("consent-secret", cts.Token)
            .Returns(Task.FromException<AdminPortalGdprCommandResult>(exception));
        var sut = new ConsumerConsentClient(selfScoped);

        OperationCanceledException actual = await Should.ThrowAsync<OperationCanceledException>(() => withdraw
            ? sut.WithdrawMyConsentAsync(new ConsumerConsentWithdrawRequest("consent-secret"), cts.Token)
            : sut.GrantMyConsentAsync(new ConsumerConsentGrantRequest("contact-secret", "marketing_emails", LawfulBasis.Consent), cts.Token));

        actual.CancellationToken.ShouldBe(cts.Token);
    }

    /// <summary>Null requests remain programming errors outside the operation exception mapping.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsentCommands_NullRequest_ThrowsWithoutDelegatingAsync(bool withdraw)
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        var sut = new ConsumerConsentClient(selfScoped);

        await Should.ThrowAsync<ArgumentNullException>(() => withdraw
            ? sut.WithdrawMyConsentAsync(null!, TestContext.Current.CancellationToken)
            : sut.GrantMyConsentAsync(null!, TestContext.Current.CancellationToken));

        selfScoped.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Malformed historical projection values survive the overview and are rejected before command submission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsentCommands_MalformedOverviewIdentifier_ReturnsValidationWithoutSubmittingAsync(bool withdraw)
    {
        const string channelId = "channel/unsafe-sensitive";
        const string consentId = "consent/unsafe-sensitive";
        PartyDetail detail = Detail() with
        {
            ContactChannels = [Detail().ContactChannels[0] with { Id = channelId }],
            ConsentRecords = [ActiveConsent(consentId, "marketing_emails") with { ChannelId = channelId }],
        };
        using var handler = new ConsumerConsentCommandHandler(detail);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.example.test/") };
        ConsumerConsentClient sut = BuildWithHttpClient(httpClient, detail);
        ConsumerConsentOverview overview = await sut.GetMyConsentOverviewAsync(TestContext.Current.CancellationToken);
        overview.ContactChannels[0].ChannelId.ShouldBe(channelId);
        overview.ConsentRecords[0].ConsentId.ShouldBe(consentId);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].Uri.ShouldBe(new Uri("https://gateway.example.test/api/v1/queries"));

        ConsumerConsentOperationResult actual = withdraw
            ? await sut.WithdrawMyConsentAsync(new ConsumerConsentWithdrawRequest(overview.ConsentRecords[0].ConsentId), TestContext.Current.CancellationToken)
            : await sut.GrantMyConsentAsync(new ConsumerConsentGrantRequest(overview.ContactChannels[0].ChannelId, "marketing_emails", LawfulBasis.Consent), TestContext.Current.CancellationToken);

        actual.Outcome.ShouldBe(ConsumerConsentOperationOutcome.ValidationRejected);
        handler.Requests.Count.ShouldBe(1);
        JsonSerializer.Serialize(actual).ShouldNotContain("unsafe-sensitive", Case.Sensitive);
    }

    /// <summary>Actual identity resolution remains fail-closed before gateway submission.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConsentCommands_UnboundPrincipal_FailsWithoutHttpAsync(bool withdraw, bool ambiguous)
    {
        var claims = new List<Claim> { new(PartiesClaimTypes.EventStoreTenant, "tenant-a") };
        if (ambiguous)
        {
            claims.Add(new Claim(PartiesClaimTypes.PartyId, "party-secret"));
            claims.Add(new Claim(PartiesClaimTypes.PartyId, "other-party-secret"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        using var handler = new ConsumerConsentCommandHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.example.test/") };
        ConsumerConsentClient sut = BuildWithHttpClient(httpClient, principal: principal);

        ConsumerConsentOperationResult actual = withdraw
            ? await sut.WithdrawMyConsentAsync(new ConsumerConsentWithdrawRequest("consent-secret"), TestContext.Current.CancellationToken)
            : await sut.GrantMyConsentAsync(new ConsumerConsentGrantRequest("contact-secret", "marketing_emails", LawfulBasis.Consent), TestContext.Current.CancellationToken);

        actual.Outcome.ShouldBe(ConsumerConsentOperationOutcome.Failed);
        handler.Requests.ShouldBeEmpty();
    }

    private static ConsumerConsentClient BuildWithHttpClient(HttpClient httpClient, PartyDetail? detail = null, ClaimsPrincipal? principal = null)
    {
        principal ??= new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"),
                new Claim(PartiesClaimTypes.PartyId, "party-secret"),
            ],
            authenticationType: "test"));
        var gdprClient = new HttpAdminPortalGdprClient(httpClient, Options.Create(new PartiesClientOptions { Tenant = "tenant-a" }));
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-secret", Arg.Any<CancellationToken>()).Returns(detail ?? Detail());
        var selfScoped = new SelfScopedPartiesClient(
            new FakeAuthStateProvider(principal),
            new PartyIdClaimResolver(),
            queryClient,
            Substitute.For<IPartiesCommandClient>(),
            gdprClient);
        return new ConsumerConsentClient(selfScoped);
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
