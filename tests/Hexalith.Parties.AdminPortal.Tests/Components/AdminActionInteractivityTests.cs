using Bunit;

using Hexalith.Parties.AdminPortal.Components;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

/// <summary>Standalone admin panels admit actions only on an interactive renderer with the caller's capability.</summary>
public sealed class AdminActionInteractivityTests : AdminPortalBunitContext
{
    /// <summary>Installs the component services without supplying an enclosing operations panel.</summary>
    public AdminActionInteractivityTests()
    {
        Services.AddFluentUIComponents();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Each standalone panel retains its capability guard and exposes its renderer gate accessibly.</summary>
    /// <param name="interactive">Whether a Server circuit has rendered.</param>
    /// <param name="capable">Whether the caller permits the action.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StandaloneActionsRequireInteractivityAndCapability(bool interactive, bool capable)
    {
        PageRenderer = new(interactive ? "Server" : "Static", interactive);
        int additions = 0, exports = 0, refreshes = 0, restrictions = 0, lifts = 0;
        string? revoked = null;
        var consent = Render<ConsentManagementPanel>(parameters => parameters
            .Add(panel => panel.Labels, new())
            .Add(panel => panel.CanAdd, capable).Add(panel => panel.CanRevoke, capable)
            .Add(panel => panel.ConsentRecords, [ActiveConsent()])
            .Add(panel => panel.Add, () => additions++)
            .Add(panel => panel.Revoke, (string id) => revoked = id));
        var restriction = Render<RestrictionActionsPanel>(parameters => parameters
            .Add(panel => panel.Labels, new()).Add(panel => panel.CanRestrict, capable)
            .Add(panel => panel.Restrict, () => restrictions++));
        var lift = Render<RestrictionActionsPanel>(parameters => parameters
            .Add(panel => panel.Labels, new()).Add(panel => panel.CanLift, capable)
            .Add(panel => panel.Lift, () => lifts++));
        var export = Render<PortabilityExportPanel>(parameters => parameters
            .Add(panel => panel.Labels, new()).Add(panel => panel.CanExport, capable)
            .Add(panel => panel.Export, () => exports++));
        var records = Render<ProcessingRecordsPanel>(parameters => parameters
            .Add(panel => panel.Labels, new()).Add(panel => panel.CanRefresh, capable)
            .Add(panel => panel.Refresh, () => refreshes++));
        bool enabled = interactive && capable;
        foreach (var button in consent.FindAll("fluent-button")
            .Concat(export.FindAll("fluent-button")).Concat(records.FindAll("fluent-button")))
        {
            button.HasAttribute("disabled").ShouldBe(!enabled);
            button.GetAttribute("aria-disabled").ShouldBe(enabled ? "false" : "true");
        }

        foreach (var (panel, available) in new[] { (restriction, 0), (lift, 1) })
        {
            for (int index = 0; index < 2; index++)
            {
                var button = panel.FindAll("fluent-button")[index];
                bool disabled = !enabled || index != available;
                button.HasAttribute("disabled").ShouldBe(disabled);
                button.GetAttribute("aria-disabled").ShouldBe(disabled ? "true" : "false");
            }
        }

        // Calling the handlers directly also exercises guards that DOM disabled semantics cannot prove.
        await consent.InvokeAsync(() => consent.FindComponents<FluentButton>()[0].Instance.OnClick.InvokeAsync());
        await export.InvokeAsync(() => export.FindComponent<FluentButton>().Instance.OnClick.InvokeAsync());
        await records.InvokeAsync(() => records.FindComponent<FluentButton>().Instance.OnClick.InvokeAsync());
        await restriction.InvokeAsync(() => restriction.FindComponents<FluentButton>()[0].Instance.OnClick.InvokeAsync());
        await lift.InvokeAsync(() => lift.FindComponents<FluentButton>()[1].Instance.OnClick.InvokeAsync());
        await consent.InvokeAsync(() => consent.FindComponents<FluentButton>()[1].Instance.OnClick.InvokeAsync());
        restrictions.ShouldBe(0);
        lifts.ShouldBe(0);
        revoked.ShouldBeNull();
        restriction.FindAll("[role='group']").Count.ShouldBe(enabled ? 1 : 0);
        lift.FindAll("[role='group']").Count.ShouldBe(enabled ? 1 : 0);
        consent.FindAll("[role='group']").Count.ShouldBe(enabled ? 1 : 0);
        if (enabled)
        {
            await restriction.InvokeAsync(() => restriction.FindComponents<FluentButton>()[^1].Instance.OnClick.InvokeAsync());
            await lift.InvokeAsync(() => lift.FindComponents<FluentButton>()[^1].Instance.OnClick.InvokeAsync());
            await consent.InvokeAsync(() => consent.FindComponents<FluentButton>()[^1].Instance.OnClick.InvokeAsync());
        }
        additions.ShouldBe(enabled ? 1 : 0);
        exports.ShouldBe(enabled ? 1 : 0);
        refreshes.ShouldBe(enabled ? 1 : 0);
        restrictions.ShouldBe(enabled ? 1 : 0);
        lifts.ShouldBe(enabled ? 1 : 0);
        revoked.ShouldBe(enabled ? "consent" : null);
        restriction.FindAll("[role='group']").ShouldBeEmpty();
        lift.FindAll("[role='group']").ShouldBeEmpty();
        consent.FindAll("[role='group']").ShouldBeEmpty();
    }

    /// <summary>A confirmation reached while authorized cannot execute a retained callback after permission is withdrawn.</summary>
    /// <param name="action">The consent or restriction confirmation to exercise.</param>
    [Theory]
    [InlineData("revoke")]
    [InlineData("restrict")]
    [InlineData("lift")]
    public async Task StandaloneConfirmationsRefuseCallbacksAfterCapabilityWithdrawal(string action)
    {
        int calls = 0;
        if (action == "revoke")
        {
            var cut = Render<ConsentManagementPanel>(parameters => parameters
                .Add(panel => panel.Labels, new()).Add(panel => panel.CanRevoke, true)
                .Add(panel => panel.ConsentRecords, [ActiveConsent()])
                .Add(panel => panel.Revoke, (string _) => calls++));
            await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()[1].Instance.OnClick.InvokeAsync());
            cut.FindAll("[role='group']").ShouldHaveSingleItem();
            var confirmation = cut.FindComponents<FluentButton>()[^1].Instance.OnClick;
            cut.Render(parameters => parameters.Add(panel => panel.CanRevoke, false));
            await cut.InvokeAsync(() => confirmation.InvokeAsync());
            cut.FindAll("[role='group']").ShouldBeEmpty();
        }
        else
        {
            bool lifting = action == "lift";
            var cut = Render<RestrictionActionsPanel>(parameters => parameters
                .Add(panel => panel.Labels, new()).Add(panel => panel.CanRestrict, !lifting).Add(panel => panel.CanLift, lifting)
                .Add(panel => panel.Restrict, () => calls++).Add(panel => panel.Lift, () => calls++));
            await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()[lifting ? 1 : 0].Instance.OnClick.InvokeAsync());
            cut.FindAll("[role='group']").ShouldHaveSingleItem();
            var confirmation = cut.FindComponents<FluentButton>()[^1].Instance.OnClick;
            cut.Render(parameters => parameters.Add(panel => panel.CanRestrict, false).Add(panel => panel.CanLift, false));
            await cut.InvokeAsync(() => confirmation.InvokeAsync());
            cut.FindAll("[role='group']").ShouldBeEmpty();
        }
        calls.ShouldBe(0);
    }

    private static ConsentRecord ActiveConsent() => new()
    {
        ConsentId = "consent", ChannelId = "channel", Purpose = "contact", LawfulBasis = LawfulBasis.Consent,
        GrantedAt = DateTimeOffset.UtcNow, GrantedBy = "admin",
    };
}
