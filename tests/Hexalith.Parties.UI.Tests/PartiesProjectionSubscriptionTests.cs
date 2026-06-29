using Hexalith.Parties.Contracts;

using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.7 AC2/AC3/AC4 — the per-circuit subscription wrapper over the <see cref="IProjectionStream"/>
/// seam: subscribe/unsubscribe lifecycle, inert-when-no-hub-URL registration, and reconnect re-subscription
/// (delegated to the client's auto-rejoin — no manual re-subscribe, no duplicate registration).
/// </summary>
public sealed class PartiesProjectionSubscriptionTests
{
    private const string ProjectionType = PartyProjectionNames.Detail;
    private const string Tenant = "tenant-a";

    [Fact]
    public void Subscribe_RegistersOnTheStream_AndEnsuresStartedOnce()
    {
        var stream = new FakeProjectionStream { IsConnected = true };
        var wrapper = new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);

        using IDisposable handle = wrapper.Subscribe(ProjectionType, Tenant, () => { });

        stream.EnsureStartedCalls.ShouldBe(1);
        FakeProjectionStream.Registration registration = stream.Subscriptions.ShouldHaveSingleItem();
        registration.ProjectionType.ShouldBe(ProjectionType);
        registration.Tenant.ShouldBe(Tenant);
    }

    [Fact]
    public void DisposingHandle_Unsubscribes()
    {
        var stream = new FakeProjectionStream { IsConnected = true };
        var wrapper = new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);

        IDisposable handle = wrapper.Subscribe(ProjectionType, Tenant, () => { });
        handle.Dispose();

        stream.Unsubscribed.ShouldHaveSingleItem();
        stream.Subscriptions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposingWrapper_TearsDownAllSubscriptions()
    {
        var stream = new FakeProjectionStream { IsConnected = true };
        var wrapper = new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);

        _ = wrapper.Subscribe(ProjectionType, Tenant, () => { });
        _ = wrapper.Subscribe(PartyProjectionNames.Index, Tenant, () => { });

        await wrapper.DisposeAsync();

        stream.Unsubscribed.Count.ShouldBe(2);
        stream.Subscriptions.ShouldBeEmpty();
    }

    [Fact]
    public void InertWhenNoHubUrl_StillRegistersWithoutThrowing()
    {
        // A disconnected/unconfigured stream: IsConnected stays false but Subscribe still registers (so a
        // later connect auto-joins) and does not throw.
        var stream = new FakeProjectionStream { IsConnected = false };
        var wrapper = new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);

        using IDisposable handle = wrapper.Subscribe(ProjectionType, Tenant, () => { });

        wrapper.IsConnected.ShouldBeFalse();
        stream.Subscriptions.ShouldHaveSingleItem();
    }

    [Fact]
    public void ReconnectReSubscribes_CallbackStillFires_NoDuplicateRegistration()
    {
        var stream = new FakeProjectionStream { IsConnected = false };
        var wrapper = new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);

        int fired = 0;
        using IDisposable handle = wrapper.Subscribe(ProjectionType, Tenant, () => fired++);

        // A signal while disconnected fires the registered callback.
        stream.RaiseChanged(ProjectionType, Tenant);
        fired.ShouldBe(1);

        // Reconnect: the wrapper relied on the client's auto-rejoin (no manual re-subscribe). The SAME
        // callback re-fires and there is no duplicate registration.
        stream.IsConnected = true;
        stream.RaiseChanged(ProjectionType, Tenant);

        fired.ShouldBe(2);
        stream.Subscriptions.ShouldHaveSingleItem();
    }
}
