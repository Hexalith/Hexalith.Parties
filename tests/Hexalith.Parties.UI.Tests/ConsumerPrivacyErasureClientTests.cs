using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.UI.Services;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class ConsumerPrivacyErasureClientTests
{
    [Fact]
    public async Task RequestMyErasureAsync_DelegatesToSelfScopedPathAndMapsPendingStatusAsync()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.RequestMyErasureAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr"));
        selfScoped.GetMyErasureStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord
            {
                PartyId = "party-secret",
                TenantId = "tenant-secret",
                Status = "ErasurePending",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        var sut = new ConsumerPrivacyErasureClient(selfScoped);

        ConsumerPrivacyErasureResult result = await sut.RequestMyErasureAsync();

        result.Outcome.ShouldBe(ConsumerPrivacyErasureOutcome.Pending);
        result.State.ShouldBe(ConsumerPrivacyErasureState.ErasurePending);
        result.CanCancel.ShouldBeTrue();
        await selfScoped.Received(1).RequestMyErasureAsync(Arg.Any<CancellationToken>());
        await selfScoped.Received(1).GetMyErasureStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelMyErasureAsync_DelegatesToSelfScopedPathAndMapsActiveStatusAsync()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.CancelMyErasureAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr"));
        selfScoped.GetMyErasureStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord
            {
                PartyId = "party-secret",
                TenantId = "tenant-secret",
                Status = "Active",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        var sut = new ConsumerPrivacyErasureClient(selfScoped);

        ConsumerPrivacyErasureResult result = await sut.CancelMyErasureAsync();

        result.State.ShouldBe(ConsumerPrivacyErasureState.Active);
        result.CanCancel.ShouldBeFalse();
        await selfScoped.Received(1).CancelMyErasureAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelMyErasureAsync_WhenRefreshStillPending_KeepsAuthoritativePendingStateAsync()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.CancelMyErasureAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr"));
        selfScoped.GetMyErasureStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord
            {
                PartyId = "party-secret",
                TenantId = "tenant-secret",
                Status = "ErasurePending",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        var sut = new ConsumerPrivacyErasureClient(selfScoped);

        ConsumerPrivacyErasureResult result = await sut.CancelMyErasureAsync();

        result.Outcome.ShouldBe(ConsumerPrivacyErasureOutcome.Pending);
        result.State.ShouldBe(ConsumerPrivacyErasureState.ErasurePending);
        result.CanCancel.ShouldBeTrue();
    }

    [Fact]
    public async Task GetMyErasureStatusAsync_MapsDeletionStartedToNonCancellableAsync()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GetMyErasureStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord
            {
                PartyId = "party-secret",
                TenantId = "tenant-secret",
                Status = "KeyDestroyed",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        var sut = new ConsumerPrivacyErasureClient(selfScoped);

        ConsumerPrivacyErasureResult result = await sut.GetMyErasureStatusAsync();

        result.State.ShouldBe(ConsumerPrivacyErasureState.KeyDestroyed);
        result.CanCancel.ShouldBeFalse();
    }
}
