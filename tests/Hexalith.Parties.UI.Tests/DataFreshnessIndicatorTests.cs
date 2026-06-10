using AngleSharp.Dom;

using Bunit;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Components.Shared;
using Hexalith.Parties.UI.Status;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class DataFreshnessIndicatorTests : BunitContext
{
    [Theory]
    [InlineData(ProjectionFreshnessStatus.Current, "Up to date")]
    [InlineData(ProjectionFreshnessStatus.Stale, "Showing what we last knew - refreshing")]
    [InlineData(ProjectionFreshnessStatus.Rebuilding, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.Degraded, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.Unavailable, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.LocalOnly, "Showing last known")]
    public void Every_freshness_status_renders_a_decorative_dot_and_visible_text(
        ProjectionFreshnessStatus status,
        string expectedText)
    {
        IRenderedComponent<DataFreshnessIndicator> cut = Render<DataFreshnessIndicator>(parameters => parameters
            .Add(component => component.Freshness, ProjectionFreshnessMetadata.Create(status)));

        cut.Markup.ShouldContain(expectedText);

        IElement dot = cut.Find("[aria-hidden=\"true\"]");
        (dot.ClassName ?? string.Empty).ShouldContain("data-freshness-indicator__dot");
    }

    [Fact]
    public void Stale_with_timestamp_includes_as_of_time()
    {
        var asOf = new DateTimeOffset(2026, 6, 10, 13, 45, 0, TimeSpan.Zero);

        IRenderedComponent<DataFreshnessIndicator> cut = Render<DataFreshnessIndicator>(parameters => parameters
            .Add(component => component.Freshness, ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale))
            .Add(component => component.AsOf, asOf));

        cut.Markup.ShouldContain("Showing what we last knew - refreshing as of 13:45");
    }

    [Fact]
    public void Text_is_a_polite_status_live_region()
    {
        IRenderedComponent<DataFreshnessIndicator> cut = Render<DataFreshnessIndicator>(parameters => parameters
            .Add(component => component.Freshness, ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current)));

        IElement status = cut.Find("[role=\"status\"]");
        (string? role, string? ariaLive) = StatusPresentation.LiveRegionAttributes(LiveRegionPoliteness.Polite);
        status.GetAttribute("role").ShouldBe(role);
        status.GetAttribute("aria-live").ShouldBe(ariaLive);
        status.TextContent.ShouldContain("Up to date");
    }

    [Fact]
    public void Null_freshness_renders_last_known_instead_of_blank_or_throwing()
    {
        IRenderedComponent<DataFreshnessIndicator> cut = Render<DataFreshnessIndicator>();

        cut.Markup.ShouldContain("Showing last known");
        cut.Markup.Trim().ShouldNotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(EveryFreshnessStatus))]
    public void Component_tracks_the_canonical_fresh_vs_degraded_mapping(ProjectionFreshnessStatus status)
    {
        IRenderedComponent<DataFreshnessIndicator> cut = Render<DataFreshnessIndicator>(parameters => parameters
            .Add(component => component.Freshness, ProjectionFreshnessMetadata.Create(status)));

        string expectedClass = StatusPresentation.FromFreshness(status) is null
            ? "data-freshness-indicator--current"
            : "data-freshness-indicator--degraded";

        (cut.Find(".data-freshness-indicator").ClassName ?? string.Empty).ShouldContain(expectedClass);
    }

    public static TheoryData<ProjectionFreshnessStatus> EveryFreshnessStatus()
    {
        var data = new TheoryData<ProjectionFreshnessStatus>();
        foreach (ProjectionFreshnessStatus status in Enum.GetValues<ProjectionFreshnessStatus>())
        {
            data.Add(status);
        }

        return data;
    }
}
