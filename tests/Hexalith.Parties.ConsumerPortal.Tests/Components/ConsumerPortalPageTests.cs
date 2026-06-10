using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;

using Microsoft.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class ConsumerPortalPageTests : BunitContext
{
    [Fact]
    public void ConsumerPortalRouteShell_RendersPlainPrivacyCopy()
    {
        RenderedConsumerPortalPage page = RenderConsumerPortalPage(typeof(MyPrivacyPage));

        page.Heading.ShouldContain("Data privacy");
        page.Status.ShouldNotBeNullOrWhiteSpace();
        page.Markup.ShouldContain("hx-parties-consumer");
    }

    [Fact]
    public void ConsumerPortalRouteShell_DoesNotRenderBannedRegulatedPromises()
    {
        string markup = RenderConsumerPortalPage(typeof(MyPrivacyPage)).Markup;

        markup.ShouldNotContain("within 30 days", Case.Insensitive);
        markup.ShouldNotContain("pre-checked", Case.Insensitive);
        markup.ShouldNotContain("under one minute", Case.Insensitive);
        markup.ShouldNotContain("#0097A7", Case.Insensitive);
    }

    private RenderedConsumerPortalPage RenderConsumerPortalPage(Type component)
        => component.Name switch
        {
            nameof(EditMyProfilePage) => Capture(Render<EditMyProfilePage>()),
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
