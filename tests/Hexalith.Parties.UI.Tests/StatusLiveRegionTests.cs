using AngleSharp.Dom;

using Bunit;

using Hexalith.Parties.UI.Components.Shared;
using Hexalith.Parties.UI.Status;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.6 AC2/AC6 — bUnit proof that <see cref="StatusLiveRegion"/> renders the canonical politeness
/// split into the <b>actual DOM</b>. The polite kinds emit <c>role="status" aria-live="polite"</c>; the
/// assertive kinds emit <c>role="alert" aria-live="assertive"</c>; <see cref="StatusKind.SignInRequired"/>
/// and a <c>null</c> kind emit <b>no</b> live region at all. This is the binding proof that the DOM
/// semantics have a single source and that an error is never announced politely.
/// </summary>
public sealed class StatusLiveRegionTests : BunitContext
{
    [Theory]
    [MemberData(nameof(PoliteKinds))]
    public void PoliteKinds_render_a_polite_status_region_with_the_message(StatusKind kind)
    {
        IRenderedComponent<StatusLiveRegion> cut = Render<StatusLiveRegion>(p => p
            .Add(c => c.Kind, kind)
            .Add(c => c.Message, "msg"));

        IElement element = cut.Find("div");
        element.GetAttribute("role").ShouldBe("status");
        element.GetAttribute("aria-live").ShouldBe("polite");
        element.TextContent.ShouldContain("msg");
    }

    [Theory]
    [MemberData(nameof(AssertiveKinds))]
    public void AssertiveKinds_render_an_assertive_alert_region_with_the_message(StatusKind kind)
    {
        IRenderedComponent<StatusLiveRegion> cut = Render<StatusLiveRegion>(p => p
            .Add(c => c.Kind, kind)
            .Add(c => c.Message, "msg"));

        IElement element = cut.Find("div");
        element.GetAttribute("role").ShouldBe("alert");
        element.GetAttribute("aria-live").ShouldBe("assertive");
        element.TextContent.ShouldContain("msg");
    }

    [Fact]
    public void SignInRequired_renders_no_live_region()
    {
        IRenderedComponent<StatusLiveRegion> cut = Render<StatusLiveRegion>(p => p
            .Add(c => c.Kind, StatusKind.SignInRequired)
            .Add(c => c.Message, "msg"));

        cut.FindAll("[role]").ShouldBeEmpty();
        cut.FindAll("[aria-live]").ShouldBeEmpty();
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void NullKind_renders_no_live_region()
    {
        IRenderedComponent<StatusLiveRegion> cut = Render<StatusLiveRegion>(p => p
            .Add(c => c.Message, "msg"));

        cut.FindAll("[role]").ShouldBeEmpty();
        cut.FindAll("[aria-live]").ShouldBeEmpty();
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void ChildContent_renders_as_markup_inside_the_live_region()
    {
        IRenderedComponent<StatusLiveRegion> cut = Render<StatusLiveRegion>(p => p
            .Add(c => c.Kind, StatusKind.AcceptedProcessing)
            .Add(c => c.ChildContent, "<span>processing</span>"));

        IElement element = cut.Find("div");
        element.GetAttribute("role").ShouldBe("status");
        element.GetAttribute("aria-live").ShouldBe("polite");
        element.QuerySelector("span").ShouldNotBeNull(); // proves the RenderFragment rendered as real markup
        element.TextContent.ShouldContain("processing");
    }

    [Fact]
    public void Message_and_ChildContent_both_render_together()
    {
        IRenderedComponent<StatusLiveRegion> cut = Render<StatusLiveRegion>(p => p
            .Add(c => c.Kind, StatusKind.LoadFailure)
            .Add(c => c.Message, "msg")
            .Add(c => c.ChildContent, "<span>child</span>"));

        IElement element = cut.Find("div");
        element.GetAttribute("role").ShouldBe("alert");
        element.GetAttribute("aria-live").ShouldBe("assertive");
        element.TextContent.ShouldContain("msg");
        element.TextContent.ShouldContain("child");
    }

    [Fact]
    public void SignInRequired_renders_nothing_even_with_child_content()
    {
        // The no-announce guarantee must hold regardless of supplied content — no stray region for SignInRequired.
        IRenderedComponent<StatusLiveRegion> cut = Render<StatusLiveRegion>(p => p
            .Add(c => c.Kind, StatusKind.SignInRequired)
            .Add(c => c.Message, "msg")
            .Add(c => c.ChildContent, "<span>child</span>"));

        cut.FindAll("[role]").ShouldBeEmpty();
        cut.FindAll("[aria-live]").ShouldBeEmpty();
        cut.Markup.Trim().ShouldBeEmpty();
    }

    public static TheoryData<StatusKind> PoliteKinds() =>
    [
        StatusKind.AcceptedProcessing,
        StatusKind.TenantUnavailable,
        StatusKind.Gone,
        StatusKind.Degraded,
    ];

    public static TheoryData<StatusKind> AssertiveKinds() =>
    [
        StatusKind.Validation,
        StatusKind.Forbidden,
        StatusKind.TransientFailure,
        StatusKind.LoadFailure,
    ];
}
