using System.Globalization;

using Bunit;

using Hexalith.Parties.AdminPortal.Components;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

public sealed class ErasureVerificationReportPanelTests : BunitContext
{
    [Fact]
    public void StandaloneErasureStatusPanelKeepsItsPrimarySectionOutsideAccordion()
    {
        Services.AddFluentUIComponents();
        IRenderedComponent<ErasureStatusPanel> cut = Render<ErasureStatusPanel>(parameters => parameters
            .Add(p => p.Labels, new AdminPortalLabels())
            .Add(p => p.Status, null)
            .Add(p => p.Certificate, null));

        cut.FindComponents<FluentAccordionItem>().ShouldBeEmpty();
        cut.Find("section[aria-labelledby^='erasure-status-heading-']").ShouldNotBeNull();
        cut.Find("h4").TextContent.ShouldBe(new AdminPortalLabels().GdprErasureStatus);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("KeyDestroyed", false)]
    [InlineData("VerificationInProgress", false)]
    [InlineData("Failed", false)]
    [InlineData("Pending", false)]
    [InlineData("Partial", false)]
    [InlineData("Verified", true)]
    [InlineData("Complete", true)]
    [InlineData("Erased", true)]
    public void VerifiedKeyCertificateRequiresAffirmativeCleanupStatus(string? cleanupStatus, bool confirmed)
    {
        AdminPortalLabels labels = new();
        PartyErasureStatusRecord? status = cleanupStatus is null ? null : new()
        {
            TenantId = "tenant",
            PartyId = "party",
            Status = cleanupStatus,
            UpdatedAt = DateTimeOffset.UnixEpoch.AddYears(60),
        };

        IRenderedComponent<ErasureVerificationReportPanel> cut = Render<ErasureVerificationReportPanel>(parameters => parameters
            .Add(p => p.Certificate, Certificate())
            .Add(p => p.Status, status)
            .Add(p => p.Labels, labels));

        cut.Find("[role='status']").TextContent.ShouldBe(confirmed
            ? labels.GdprVerificationConfirmed : labels.GdprVerificationNeedsAttention);
        cut.FindAll("dt").Single(term => term.TextContent == labels.GdprVerifiedAcrossProjections)
            .NextElementSibling!.TextContent.ShouldBe(confirmed ? labels.Yes : labels.No);
        if (string.IsNullOrWhiteSpace(cleanupStatus))
        {
            cut.Markup.ShouldNotContain(labels.GdprReportTimestamp);
        }
    }

    [Fact]
    public void ReportTimestampComesFromCleanupStatusRatherThanOriginalKeyCertificate()
    {
        AdminPortalLabels labels = new();
        DateTimeOffset cleanupAt = DateTimeOffset.UnixEpoch.AddYears(60);
        PartyErasureStatusRecord status = new()
        {
            TenantId = "tenant",
            PartyId = "party",
            Status = "Complete",
            UpdatedAt = cleanupAt,
        };

        IRenderedComponent<ErasureVerificationReportPanel> cut = Render<ErasureVerificationReportPanel>(parameters => parameters
            .Add(p => p.Certificate, Certificate())
            .Add(p => p.Status, status)
            .Add(p => p.Labels, labels));

        string displayed = cut.FindAll("dt").Single(term => term.TextContent == labels.GdprReportTimestamp)
            .NextElementSibling!.TextContent;
        displayed.ShouldBe(PartyDisplayFormat.FormatDate(cleanupAt, CultureInfo.CurrentCulture, PartyDisplayFormat.CompactDateFormat));
        displayed.ShouldNotBe(PartyDisplayFormat.FormatDate(Certificate().Timestamp, CultureInfo.CurrentCulture, PartyDisplayFormat.CompactDateFormat));
    }

    private static ErasureCertificate Certificate() => new()
    {
        TenantId = "tenant",
        PartyId = "party",
        Timestamp = DateTimeOffset.UnixEpoch,
        KeyVersionsDestroyed = [1],
        VerificationStatus = ErasureVerificationStatus.Verified,
    };
}
