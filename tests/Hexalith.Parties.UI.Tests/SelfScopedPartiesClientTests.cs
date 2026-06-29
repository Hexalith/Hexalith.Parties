using System.Security.Claims;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Services;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.5 AC1/AC2 — proves the accessor passes the <strong>resolved</strong> <c>party_id</c> (never a
/// caller-supplied id) to the underlying clients, and <strong>fails closed</strong> (throws, no client
/// call) for an unbound or ambiguously bound principal. The negative is asserted explicitly
/// (<see cref="ReceivedExtensions.DidNotReceiveWithAnyArgs{T}(T)"/>), mirroring Story 1.4's
/// fail-closed discipline.
/// </summary>
public sealed class SelfScopedPartiesClientTests
{
    private const string BoundPartyId = "party-123";

    [Fact]
    public async Task GetMyPartyAsync_BoundPrincipal_InjectsResolvedPartyIdIntoQueryClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.GetMyPartyAsync();

        // The RESOLVED id (not a caller-supplied one — the method takes no partyId) reaches the client.
        await queryClient.Received(1).GetPartyAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyPartyAsync_UnboundPrincipal_ThrowsAndNeverCallsQueryClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(UnboundPrincipal(), queryClient, gdprClient);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetMyPartyAsync());

        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
    }

    [Fact]
    public async Task GetMyPartyAsync_AmbiguousBinding_ThrowsAndNeverCallsQueryClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(AmbiguousPrincipal(), queryClient, gdprClient);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetMyPartyAsync());

        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
    }

    [Fact]
    public async Task RevokeMyConsentAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.RevokeMyConsentAsync("c1");

        await gdprClient.Received(1).RevokeConsentAsync(BoundPartyId, "c1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeMyConsentAsync_UnboundPrincipal_ThrowsAndNeverCallsGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(UnboundPrincipal(), queryClient, gdprClient);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.RevokeMyConsentAsync("c1"));

        await gdprClient.DidNotReceiveWithAnyArgs().RevokeConsentAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetMyConsentAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.GetMyConsentAsync();

        await gdprClient.Received(1).GetConsentAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantMyConsentAsync_BoundPrincipal_InjectsResolvedPartyIdAndForwardsCallerArguments()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.GrantMyConsentAsync("channel-1", "marketing", LawfulBasis.Consent);

        // The RESOLVED id is injected first; the caller's own arguments are forwarded unchanged.
        await gdprClient.Received(1).AddConsentAsync(
            BoundPartyId, "channel-1", "marketing", LawfulBasis.Consent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestMyErasureAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.RequestMyErasureAsync();

        await gdprClient.Received(1).RequestErasureAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyErasureStatusAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.GetMyErasureStatusAsync();

        await gdprClient.Received(1).GetErasureStatusAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelMyErasureAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.CancelMyErasureAsync();

        await gdprClient.Received(1).CancelErasureAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestrictMyProcessingAsync_BoundPrincipal_InjectsResolvedPartyIdAndForwardsReason()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.RestrictMyProcessingAsync("under-review");

        await gdprClient.Received(1).RestrictProcessingAsync(BoundPartyId, "under-review", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LiftMyRestrictionAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.LiftMyRestrictionAsync();

        await gdprClient.Received(1).LiftRestrictionAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportMyDataAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.ExportMyDataAsync();

        await gdprClient.Received(1).ExportPartyDataAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyProcessingRecordsAsync_BoundPrincipal_InjectsResolvedPartyIdIntoGdprClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.GetMyProcessingRecordsAsync();

        await gdprClient.Received(1).GetProcessingRecordsAsync(BoundPartyId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(AccessorMethodNames))]
    public async Task UnboundPrincipal_EveryAccessorMethod_ThrowsAndCallsNoUnderlyingClient(string methodName)
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(UnboundPrincipal(), queryClient, gdprClient);

        await Should.ThrowAsync<InvalidOperationException>(() => InvokeAsync(sut, methodName));

        // Fail-closed for EVERY method: no underlying gateway call may have been issued.
        queryClient.ReceivedCalls().ShouldBeEmpty();
        gdprClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(AccessorMethodNames))]
    public async Task AmbiguousBinding_EveryAccessorMethod_ThrowsAndCallsNoUnderlyingClient(string methodName)
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(AmbiguousPrincipal(), queryClient, gdprClient);

        await Should.ThrowAsync<InvalidOperationException>(() => InvokeAsync(sut, methodName));

        queryClient.ReceivedCalls().ShouldBeEmpty();
        gdprClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMyConsentAsync_BoundPrincipal_ReturnsUnderlyingResultUnchanged()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        IReadOnlyList<ConsentRecord> expected = new List<ConsentRecord>();
        gdprClient.GetConsentAsync(BoundPartyId, Arg.Any<CancellationToken>()).Returns(expected);
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        IReadOnlyList<ConsentRecord> actual = await sut.GetMyConsentAsync();

        // The accessor is a pass-through over the resolved id; it must not drop or transform the result.
        actual.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task GetMyPartyAsync_BoundPrincipal_ForwardsCallerCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient);

        await sut.GetMyPartyAsync(cts.Token);

        // The caller's token (not default) must flow through to the underlying client.
        await queryClient.Received(1).GetPartyAsync(BoundPartyId, cts.Token);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_BoundPrincipal_InjectsResolvedPartyIdIntoCommandClient()
    {
        using var cts = new CancellationTokenSource();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        var expected = new PartiesCommandResult<PartyDetail>("corr-1", null);
        commandClient.UpdatePartyCompositeWithResultAsync(BoundPartyId, Arg.Any<UpdatePartyComposite>(), cts.Token)
            .Returns(expected);
        SelfScopedPartiesClient sut = BuildSut(BoundPrincipal(BoundPartyId), queryClient, gdprClient, commandClient);
        var request = new SelfScopedProfileUpdateRequest
        {
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
            },
        };

        PartiesCommandResult<PartyDetail> actual = await sut.UpdateMyProfileAsync(request, cts.Token);

        actual.ShouldBeSameAs(expected);
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
            BoundPartyId,
            Arg.Is<UpdatePartyComposite>(command =>
                command != null
                && command.PartyId == BoundPartyId
                && command.PersonDetails == request.PersonDetails
                && command.OrganizationDetails == null),
            cts.Token);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_UnboundPrincipal_ThrowsAndNeverCallsCommandClient()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IAdminPortalGdprClient gdprClient = Substitute.For<IAdminPortalGdprClient>();
        SelfScopedPartiesClient sut = BuildSut(UnboundPrincipal(), queryClient, gdprClient, commandClient);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.UpdateMyProfileAsync(new SelfScopedProfileUpdateRequest()));

        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeWithResultAsync(default!, default!, default);
    }

    public static TheoryData<string> AccessorMethodNames()
    {
        var data = new TheoryData<string>();
        foreach (string name in AccessorMethodNameList)
        {
            data.Add(name);
        }

        return data;
    }

    private static readonly string[] AccessorMethodNameList =
    [
        nameof(ISelfScopedPartiesClient.GetMyPartyAsync),
        nameof(ISelfScopedPartiesClient.UpdateMyProfileAsync),
        nameof(ISelfScopedPartiesClient.GetMyConsentAsync),
        nameof(ISelfScopedPartiesClient.GrantMyConsentAsync),
        nameof(ISelfScopedPartiesClient.RevokeMyConsentAsync),
        nameof(ISelfScopedPartiesClient.RequestMyErasureAsync),
        nameof(ISelfScopedPartiesClient.CancelMyErasureAsync),
        nameof(ISelfScopedPartiesClient.GetMyErasureStatusAsync),
        nameof(ISelfScopedPartiesClient.RestrictMyProcessingAsync),
        nameof(ISelfScopedPartiesClient.LiftMyRestrictionAsync),
        nameof(ISelfScopedPartiesClient.ExportMyDataAsync),
        nameof(ISelfScopedPartiesClient.GetMyProcessingRecordsAsync),
    ];

    // Invokes an accessor method by name with throwaway arguments — used by the fail-closed theories,
    // where resolution throws before any argument can reach the underlying client. Returns the non-generic
    // Task (each Task<T> arm converts implicitly) so a single switch can drive every method shape.
    private static Task InvokeAsync(ISelfScopedPartiesClient sut, string methodName)
        => methodName switch
        {
            nameof(ISelfScopedPartiesClient.GetMyPartyAsync) => sut.GetMyPartyAsync(),
            nameof(ISelfScopedPartiesClient.UpdateMyProfileAsync) => sut.UpdateMyProfileAsync(new SelfScopedProfileUpdateRequest()),
            nameof(ISelfScopedPartiesClient.GetMyConsentAsync) => sut.GetMyConsentAsync(),
            nameof(ISelfScopedPartiesClient.GrantMyConsentAsync) => sut.GrantMyConsentAsync("channel-1", "marketing", LawfulBasis.Consent),
            nameof(ISelfScopedPartiesClient.RevokeMyConsentAsync) => sut.RevokeMyConsentAsync("c1"),
            nameof(ISelfScopedPartiesClient.RequestMyErasureAsync) => sut.RequestMyErasureAsync(),
            nameof(ISelfScopedPartiesClient.CancelMyErasureAsync) => sut.CancelMyErasureAsync(),
            nameof(ISelfScopedPartiesClient.GetMyErasureStatusAsync) => sut.GetMyErasureStatusAsync(),
            nameof(ISelfScopedPartiesClient.RestrictMyProcessingAsync) => sut.RestrictMyProcessingAsync("under-review"),
            nameof(ISelfScopedPartiesClient.LiftMyRestrictionAsync) => sut.LiftMyRestrictionAsync(),
            nameof(ISelfScopedPartiesClient.ExportMyDataAsync) => sut.ExportMyDataAsync(),
            nameof(ISelfScopedPartiesClient.GetMyProcessingRecordsAsync) => sut.GetMyProcessingRecordsAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unknown accessor method."),
        };

    private static SelfScopedPartiesClient BuildSut(
        ClaimsPrincipal user,
        IPartiesQueryClient queryClient,
        IAdminPortalGdprClient gdprClient,
        IPartiesCommandClient? commandClient = null)
        => new(
            new FakeAuthStateProvider(user),
            new PartyIdClaimResolver(),
            queryClient,
            commandClient ?? Substitute.For<IPartiesCommandClient>(),
            gdprClient);

    private static ClaimsPrincipal BoundPrincipal(string partyId)
        => new(new ClaimsIdentity(
            [
                new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"),
                new Claim(PartiesClaimTypes.PartyId, partyId),
            ],
            authenticationType: "test"));

    private static ClaimsPrincipal UnboundPrincipal()
        => new(new ClaimsIdentity(authenticationType: "test"));

    private static ClaimsPrincipal AmbiguousPrincipal()
        => new(new ClaimsIdentity(
            [
                new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-1"),
                new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-2"),
            ],
            authenticationType: "test"));
}
