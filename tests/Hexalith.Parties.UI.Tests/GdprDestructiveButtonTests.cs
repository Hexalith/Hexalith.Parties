using Bunit;

using Hexalith.Parties.UI.Components.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class GdprDestructiveButtonTests : BunitContext
{
    public GdprDestructiveButtonTests()
    {
        Services.AddFluentUIComponents();
        JSInterop.SetupVoid(
            "Microsoft.FluentUI.Blazor.Utilities.Attributes.observeAttributeChange",
            _ => true);
    }

    [Fact]
    public async Task Irreversible_action_requires_labeled_exact_typed_confirmation_before_click()
    {
        var confirmed = 0;
        string? currentValue = null;

        IRenderedComponent<GdprDestructiveButton> cut = RenderIrreversible(
            currentValue,
            value => currentValue = value,
            () => confirmed++);

        FluentTextInput input = cut.FindComponent<FluentTextInput>().Instance;
        input.Label.ShouldBe("Type ERASE to confirm");
        input.AdditionalAttributes.ShouldNotBeNull();
        input.AdditionalAttributes!.ShouldContainKey("aria-describedby");
        string descriptionId = input.AdditionalAttributes["aria-describedby"]?.ToString() ?? string.Empty;
        descriptionId.ShouldNotBeEmpty();
        cut.Find($"#{descriptionId}").TextContent.ShouldContain("permanently removes party data");

        FluentButton disabledButton = cut.FindComponent<FluentButton>().Instance;
        disabledButton.Disabled.ShouldBeTrue();
        await cut.InvokeAsync(() => disabledButton.OnClick.InvokeAsync());
        confirmed.ShouldBe(0);

        await cut.InvokeAsync(() => input.ValueChanged.InvokeAsync("erase"));
        confirmed.ShouldBe(0);

        await SetCurrentConfirmationValueAsync(cut, currentValue);
        cut.FindComponent<FluentButton>().Instance.Disabled.ShouldBeTrue();

        await cut.InvokeAsync(() => input.ValueChanged.InvokeAsync("ERASE"));
        confirmed.ShouldBe(0);

        await SetCurrentConfirmationValueAsync(cut, currentValue);

        FluentButton enabledButton = cut.FindComponent<FluentButton>().Instance;
        enabledButton.Disabled.ShouldBeFalse();

        await cut.InvokeAsync(() => enabledButton.OnClick.InvokeAsync());

        confirmed.ShouldBe(1);
    }

    [Fact]
    public async Task Focus_blur_and_input_changes_do_not_fire_the_confirmed_callback()
    {
        var confirmed = 0;
        string? currentValue = null;

        IRenderedComponent<GdprDestructiveButton> cut = RenderIrreversible(
            currentValue,
            value => currentValue = value,
            () => confirmed++);

        TriggerFocusIfWired(cut);
        TriggerBlurIfWired(cut);
        await cut.InvokeAsync(() => cut.FindComponent<FluentTextInput>().Instance.ValueChanged.InvokeAsync("ERASE"));

        confirmed.ShouldBe(0);
    }

    [Fact]
    public async Task Reversible_action_uses_outline_without_typed_input_or_danger_fill()
    {
        var confirmed = 0;

        IRenderedComponent<GdprDestructiveButton> cut = Render<GdprDestructiveButton>(parameters => parameters
            .Add(component => component.IsIrreversible, false)
            .Add(component => component.ButtonText, "Restrict processing")
            .Add(component => component.Confirmed, EventCallback.Factory.Create(this, () => confirmed++)));

        cut.FindComponents<FluentTextInput>().ShouldBeEmpty();

        FluentButton button = cut.FindComponent<FluentButton>().Instance;
        button.Appearance.ShouldBe(ButtonAppearance.Outline);
        (button.Class ?? string.Empty).ShouldNotContain("gdpr-destructive-button__button--danger");

        await cut.InvokeAsync(() => button.OnClick.InvokeAsync());
        confirmed.ShouldBe(1);
    }

    [Fact]
    public async Task Irreversible_action_with_empty_expected_confirmation_stays_disabled()
    {
        var confirmed = 0;

        IRenderedComponent<GdprDestructiveButton> cut = Render<GdprDestructiveButton>(parameters => parameters
            .Add(component => component.IsIrreversible, true)
            .Add(component => component.ButtonText, "Erase party")
            .Add(component => component.ExpectedConfirmationValue, string.Empty)
            .Add(component => component.CurrentConfirmationValue, string.Empty)
            .Add(component => component.Confirmed, EventCallback.Factory.Create(this, () => confirmed++)));

        FluentButton button = cut.FindComponent<FluentButton>().Instance;
        button.Disabled.ShouldBeTrue();

        await cut.InvokeAsync(() => button.OnClick.InvokeAsync());

        confirmed.ShouldBe(0);
    }

    private static Task SetCurrentConfirmationValueAsync(
        IRenderedComponent<GdprDestructiveButton> cut,
        string? currentValue)
        => cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(GdprDestructiveButton.CurrentConfirmationValue)] = currentValue,
            })));

    private IRenderedComponent<GdprDestructiveButton> RenderIrreversible(
        string? currentValue,
        Action<string?> valueChanged,
        Action confirmed)
        => Render<GdprDestructiveButton>(parameters => parameters
            .Add(component => component.IsIrreversible, true)
            .Add(component => component.ButtonText, "Erase party")
            .Add(component => component.ConfirmationLabel, "Type ERASE to confirm")
            .Add(component => component.ConfirmationDescription, "This action permanently removes party data.")
            .Add(component => component.ExpectedConfirmationValue, "ERASE")
            .Add(component => component.CurrentConfirmationValue, currentValue)
            .Add(component => component.CurrentConfirmationValueChanged, EventCallback.Factory.Create<string?>(this, valueChanged))
            .Add(component => component.Confirmed, EventCallback.Factory.Create(this, confirmed)));

    private static void TriggerFocusIfWired(IRenderedComponent<GdprDestructiveButton> cut)
    {
        try
        {
            cut.Find("fluent-field").Focus();
        }
        catch (MissingEventHandlerException)
        {
        }
    }

    private static void TriggerBlurIfWired(IRenderedComponent<GdprDestructiveButton> cut)
    {
        try
        {
            cut.Find("fluent-field").Blur();
        }
        catch (MissingEventHandlerException)
        {
        }
    }
}
