using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;
using Hexalith.Parties.ConsumerPortal.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class ConsumerPortalPageTests : BunitContext
{
    public ConsumerPortalPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddSingleton<IConsumerPrivacyExportClient>(new StubPrivacyExportClient());
    }

    [Fact]
    public void MyPrivacyPage_RendersRealExportSurface()
    {
        RenderedConsumerPortalPage page = RenderConsumerPortalPage(typeof(MyPrivacyPage));

        page.Heading.ShouldContain("Data privacy");
        page.Status.ShouldNotBeNullOrWhiteSpace();
        page.Markup.ShouldContain("Export my data");
        page.Markup.ShouldContain("Machine-readable JSON");
        page.Markup.ShouldNotContain("ConsumerRouteShell");
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

    private sealed class StubPrivacyExportClient : IConsumerPrivacyExportClient
    {
        public Task<ConsumerPrivacyExportResult> ExportMyDataAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ConsumerPrivacyExportResult(
                ConsumerPrivacyExportOutcome.Ready,
                "my-data-export-20260610000000Z.json",
                "application/json",
                """{"status":"Exported"}"""u8.ToArray(),
                ConsumerPrivacyExportStatus.Exported,
                null));
    }
}
