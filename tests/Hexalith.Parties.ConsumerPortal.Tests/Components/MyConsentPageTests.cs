using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class MyConsentPageTests : BunitContext
{
    public MyConsentPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void MyConsentPage_Loading_RendersSkeletonBeforeDataResolves()
    {
        var pending = new TaskCompletionSource<ConsumerConsentOverview>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new QueueConsentClient(_ => pending.Task);
        Services.AddSingleton<IConsumerConsentClient>(client);

        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();

        cut.Markup.ShouldContain("Loading consent choices");
        cut.Markup.ShouldContain("aria-busy=\"true\"");
        cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(1);
        client.OverviewCallCount.ShouldBe(1);
    }

    [Fact]
    public void MyConsentPage_NoActiveConsent_RendersSwitchesOffAndReadOnlyBasisRows()
    {
        Services.AddSingleton<IConsumerConsentClient>(new QueueConsentClient(_ => Task.FromResult(Overview())));

        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("h1").TextContent.ShouldBe("Consent");
            cut.Markup.ShouldContain("Things you control");
            cut.Markup.ShouldContain("Things we keep to run your account");
            cut.Markup.ShouldContain("Marketing emails");
            cut.Markup.ShouldContain("Product updates");
            cut.Markup.ShouldContain("Object (Art. 21)");
            cut.FindAll("[role='switch'][aria-checked='true']").Count.ShouldBe(0);
            cut.FindAll("[role='switch'][aria-checked='false']").Count.ShouldBe(2);
            cut.Markup.ShouldNotContain("Withdraw consent", Case.Sensitive);
            cut.Markup.ShouldNotContain("contact-secret", Case.Sensitive);
            cut.Markup.ShouldNotContain("ada@example.test", Case.Sensitive);
        });
    }

    [Fact]
    public void MyConsentPage_ActiveConsent_RendersMatchingSwitchOnOnly()
    {
        Services.AddSingleton<IConsumerConsentClient>(new QueueConsentClient(_ => Task.FromResult(Overview(
        [
            ActiveConsent("consent-secret-marketing", "marketing_emails"),
            RevokedConsent("consent-secret-product", "product_updates"),
        ]))));

        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role='switch'][aria-checked='true']").Count.ShouldBe(1);
            Switch(cut, "Marketing emails").Instance.Value.ShouldBeTrue();
            Switch(cut, "Product updates").Instance.Value.ShouldBeFalse();
            cut.Markup.ShouldNotContain("consent-secret", Case.Sensitive);
        });
    }

    [Fact]
    public void MyConsentPage_GrantConsent_OptimisticallyTurnsOnAndReconciles()
    {
        var grant = new TaskCompletionSource<ConsumerConsentOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new QueueConsentClient(
            _ => Task.FromResult(Overview()),
            _ => Task.FromResult(Overview([ActiveConsent("consent-secret-marketing", "marketing_emails")])))
        {
            GrantHandler = _ => grant.Task,
        };
        Services.AddSingleton<IConsumerConsentClient>(client);
        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();
        cut.WaitForAssertion(() => Switch(cut, "Marketing emails").Instance.Value.ShouldBeFalse());

        SetSwitch(cut, "Marketing emails", true);

        cut.WaitForAssertion(() =>
        {
            Switch(cut, "Marketing emails").Instance.Value.ShouldBeTrue();
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Saving...");
            cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(1);
            client.LastGrant.ShouldNotBeNull();
            client.LastGrant!.Purpose.ShouldBe("marketing_emails");
            client.LastGrant.LawfulBasis.ShouldBe(LawfulBasis.Consent);
        });

        grant.SetResult(new ConsumerConsentOperationResult(ConsumerConsentOperationOutcome.Accepted));

        cut.WaitForAssertion(() =>
        {
            Switch(cut, "Marketing emails").Instance.Value.ShouldBeTrue();
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Saved");
            client.OverviewCallCount.ShouldBe(2);
        });
    }

    [Fact]
    public void MyConsentPage_WithdrawConsent_UsesActiveConsentIdAndReconciles()
    {
        var withdraw = new TaskCompletionSource<ConsumerConsentOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new QueueConsentClient(
            _ => Task.FromResult(Overview([ActiveConsent("consent-secret-marketing", "marketing_emails")])),
            _ => Task.FromResult(Overview([RevokedConsent("consent-secret-marketing", "marketing_emails")])))
        {
            WithdrawHandler = _ => withdraw.Task,
        };
        Services.AddSingleton<IConsumerConsentClient>(client);
        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();
        cut.WaitForAssertion(() => Switch(cut, "Marketing emails").Instance.Value.ShouldBeTrue());

        SetSwitch(cut, "Marketing emails", false);

        cut.WaitForAssertion(() =>
        {
            Switch(cut, "Marketing emails").Instance.Value.ShouldBeFalse();
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Saving...");
            client.LastWithdraw.ShouldNotBeNull();
            client.LastWithdraw!.ConsentId.ShouldBe("consent-secret-marketing");
        });

        withdraw.SetResult(new ConsumerConsentOperationResult(ConsumerConsentOperationOutcome.Accepted));

        cut.WaitForAssertion(() =>
        {
            Switch(cut, "Marketing emails").Instance.Value.ShouldBeFalse();
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Saved");
            client.OverviewCallCount.ShouldBe(2);
        });
    }

    [Fact]
    public void MyConsentPage_ActiveConsentWithoutChannel_AllowsWithdrawByActiveConsentId()
    {
        var client = new QueueConsentClient(
            _ => Task.FromResult(Overview(
                [ActiveConsent("consent-secret-marketing", "marketing_emails")],
                channels: [])),
            _ => Task.FromResult(Overview(channels: [])));
        Services.AddSingleton<IConsumerConsentClient>(client);
        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();
        cut.WaitForAssertion(() => Switch(cut, "Marketing emails").Instance.Value.ShouldBeTrue());

        SetSwitch(cut, "Marketing emails", false);

        cut.WaitForAssertion(() =>
        {
            Switch(cut, "Marketing emails").Instance.Value.ShouldBeFalse();
            client.LastWithdraw.ShouldNotBeNull();
            client.LastWithdraw!.ConsentId.ShouldBe("consent-secret-marketing");
            client.LastGrant.ShouldBeNull();
        });
    }

    [Fact]
    public void MyConsentPage_Rejection_RevertsAndShowsPiiFreeAlert()
    {
        var client = new QueueConsentClient(_ => Task.FromResult(Overview()))
        {
            GrantHandler = _ => Task.FromResult(new ConsumerConsentOperationResult(ConsumerConsentOperationOutcome.ValidationRejected)),
        };
        Services.AddSingleton<IConsumerConsentClient>(client);
        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();
        cut.WaitForAssertion(() => Switch(cut, "Marketing emails").Instance.Value.ShouldBeFalse());

        SetSwitch(cut, "Marketing emails", true);

        cut.WaitForAssertion(() =>
        {
            Switch(cut, "Marketing emails").Instance.Value.ShouldBeFalse();
            cut.Find("[role='alert']").TextContent.ShouldContain("We couldn't save that consent choice. Please try again.");
            cut.Markup.ShouldNotContain("contact-secret", Case.Sensitive);
            cut.Markup.ShouldNotContain("ada@example.test", Case.Sensitive);
        });
    }

    [Fact]
    public void MyConsentPage_ErasedSelf_BlocksSwitchesAndSuppressesPersonalValues()
    {
        Services.AddSingleton<IConsumerConsentClient>(new QueueConsentClient(_ => Task.FromResult(Overview(isErased: true))));

        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Consent changes are unavailable for this profile.");
            cut.FindAll("[role='switch']").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("ada@example.test", Case.Sensitive);
            cut.Markup.ShouldNotContain("contact-secret", Case.Sensitive);
        });
    }

    [Fact]
    public void MyConsentPage_DegradedOverview_KeepsLastKnownRowsVisible()
    {
        Services.AddSingleton<IConsumerConsentClient>(new QueueConsentClient(_ => Task.FromResult(Overview(
            freshness: ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Degraded)))));

        IRenderedComponent<MyConsentPage> cut = Render<MyConsentPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Marketing emails");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Showing last known");
        });
    }

    private static void SetSwitch(IRenderedComponent<MyConsentPage> cut, string label, bool value)
    {
        IRenderedComponent<FluentSwitch> input = Switch(cut, label);
        _ = cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync(value));
    }

    private static IRenderedComponent<FluentSwitch> Switch(IRenderedComponent<MyConsentPage> cut, string label)
        => cut.FindComponents<FluentSwitch>()
            .Single(input => string.Equals(input.Instance.Label, label, StringComparison.Ordinal));

    private static ConsumerConsentOverview Overview(
        IReadOnlyList<ConsentRecord>? consentRecords = null,
        bool isErased = false,
        ProjectionFreshnessMetadata? freshness = null,
        IReadOnlyList<ConsumerConsentChannel>? channels = null)
        => new(
            isErased,
            freshness ?? ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
            channels ??
                [
                    new ConsumerConsentChannel("contact-secret", ContactChannelType.Email),
                ],
            consentRecords ?? []);

    private static ConsentRecord ActiveConsent(string consentId, string purpose)
        => new()
        {
            ConsentId = consentId,
            ChannelId = "contact-secret",
            Purpose = purpose,
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = new DateTimeOffset(2026, 06, 10, 0, 0, 0, TimeSpan.Zero),
            GrantedBy = "operator-secret",
        };

    private static ConsentRecord RevokedConsent(string consentId, string purpose)
        => ActiveConsent(consentId, purpose) with
        {
            RevokedAt = new DateTimeOffset(2026, 06, 10, 1, 0, 0, TimeSpan.Zero),
            RevokedBy = "operator-secret",
        };

    private sealed class QueueConsentClient(params Func<CancellationToken, Task<ConsumerConsentOverview>>[] overviewCalls)
        : IConsumerConsentClient
    {
        private int _overviewIndex;

        public int OverviewCallCount { get; private set; }

        public ConsumerConsentGrantRequest? LastGrant { get; private set; }

        public ConsumerConsentWithdrawRequest? LastWithdraw { get; private set; }

        public Func<ConsumerConsentGrantRequest, Task<ConsumerConsentOperationResult>>? GrantHandler { get; init; }

        public Func<ConsumerConsentWithdrawRequest, Task<ConsumerConsentOperationResult>>? WithdrawHandler { get; init; }

        public Task<ConsumerConsentOverview> GetMyConsentOverviewAsync(CancellationToken cancellationToken = default)
        {
            OverviewCallCount++;
            Func<CancellationToken, Task<ConsumerConsentOverview>> call = overviewCalls[Math.Min(_overviewIndex, overviewCalls.Length - 1)];
            _overviewIndex++;
            return call(cancellationToken);
        }

        public Task<ConsumerConsentOperationResult> GrantMyConsentAsync(
            ConsumerConsentGrantRequest request,
            CancellationToken cancellationToken = default)
        {
            LastGrant = request;
            return GrantHandler?.Invoke(request)
                ?? Task.FromResult(new ConsumerConsentOperationResult(ConsumerConsentOperationOutcome.Accepted));
        }

        public Task<ConsumerConsentOperationResult> WithdrawMyConsentAsync(
            ConsumerConsentWithdrawRequest request,
            CancellationToken cancellationToken = default)
        {
            LastWithdraw = request;
            return WithdrawHandler?.Invoke(request)
                ?? Task.FromResult(new ConsumerConsentOperationResult(ConsumerConsentOperationOutcome.Accepted));
        }
    }
}
