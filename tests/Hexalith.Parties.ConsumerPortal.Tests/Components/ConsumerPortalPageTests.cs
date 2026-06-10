using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;

using Microsoft.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class ConsumerPortalPageTests : BunitContext
{
    [Theory]
    [InlineData(typeof(MyConsentPage), "Consent")]
    [InlineData(typeof(MyPrivacyPage), "Data privacy")]
    public void ConsumerPortalRouteShell_RendersPlainConsumerCopy(Type component, string expectedHeading)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHeading);

        RenderedConsumerPortalPage page = RenderConsumerPortalPage(component);

        page.Heading.ShouldContain(expectedHeading);
        page.Status.ShouldNotBeNullOrWhiteSpace();
        page.Markup.ShouldContain("hx-parties-consumer");
    }

    [Theory]
    [InlineData(typeof(MyConsentPage))]
    [InlineData(typeof(MyPrivacyPage))]
    public void ConsumerPortalRouteShell_DoesNotRenderBannedRegulatedPromises(Type component)
    {
        ArgumentNullException.ThrowIfNull(component);

        string markup = RenderConsumerPortalPage(component).Markup;

        markup.ShouldNotContain("within 30 days", Case.Insensitive);
        markup.ShouldNotContain("pre-checked", Case.Insensitive);
        markup.ShouldNotContain("under one minute", Case.Insensitive);
        markup.ShouldNotContain("#0097A7", Case.Insensitive);
    }

    private RenderedConsumerPortalPage RenderConsumerPortalPage(Type component)
        => component.Name switch
        {
            nameof(EditMyProfilePage) => Capture(Render<EditMyProfilePage>()),
            nameof(MyConsentPage) => Capture(Render<MyConsentPage>()),
            nameof(MyPrivacyPage) => Capture(Render<MyPrivacyPage>()),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown ConsumerPortal page type."),
        };

    private static RenderedConsumerPortalPage Capture<TComponent>(IRenderedComponent<TComponent> rendered)
        where TComponent : IComponent
        => new(
            rendered.Markup,
            rendered.Find("h1").TextContent,
            rendered.Find("[role='status'][aria-live='polite']").TextContent);

    private sealed record RenderedConsumerPortalPage(string Markup, string Heading, string Status);
}
