using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Status;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.6 AC1/AC3/AC4 — pins the canonical, pure status→<see cref="StatusKind"/> mapping and the
/// <see cref="StatusKind"/>→politeness split. Every listed HTTP status, the <c>403</c> tenant-vs-role
/// branch, every <see cref="ProjectionFreshnessStatus"/>, every <see cref="StatusKind"/>'s politeness, the
/// timeout/cancellation cases (AC4), and the concrete ARIA strings are asserted here. These are the binding
/// proof that there is exactly one mapper and that a wrong politeness (blanket-polite on an error) is
/// impossible.
/// </summary>
public sealed class StatusPresentationTests
{
    // The expected politeness for every canonical state. Driving the StatusKind enum against THIS map (not
    // re-deriving from the switch) is what makes a newly-added-but-unmapped state fail loudly rather than
    // silently default. Keep in lockstep with StatusPresentation.PolitenessFor.
    private static readonly IReadOnlyDictionary<StatusKind, LiveRegionPoliteness> ExpectedPoliteness =
        new Dictionary<StatusKind, LiveRegionPoliteness>
        {
            [StatusKind.AcceptedProcessing] = LiveRegionPoliteness.Polite,
            [StatusKind.TenantUnavailable] = LiveRegionPoliteness.Polite,
            [StatusKind.Gone] = LiveRegionPoliteness.Polite,
            [StatusKind.Degraded] = LiveRegionPoliteness.Polite,
            [StatusKind.Validation] = LiveRegionPoliteness.Assertive,
            [StatusKind.Forbidden] = LiveRegionPoliteness.Assertive,
            [StatusKind.TransientFailure] = LiveRegionPoliteness.Assertive,
            [StatusKind.LoadFailure] = LiveRegionPoliteness.Assertive,
            [StatusKind.SignInRequired] = LiveRegionPoliteness.None,
        };

    [Theory]
    [InlineData(200, StatusKind.AcceptedProcessing)]
    [InlineData(202, StatusKind.AcceptedProcessing)]
    [InlineData(400, StatusKind.Validation)]
    [InlineData(422, StatusKind.Validation)]
    [InlineData(401, StatusKind.SignInRequired)]
    [InlineData(403, StatusKind.Forbidden)]
    [InlineData(404, StatusKind.Gone)]
    [InlineData(410, StatusKind.Gone)]
    [InlineData(408, StatusKind.TransientFailure)]
    [InlineData(429, StatusKind.TransientFailure)]
    [InlineData(500, StatusKind.LoadFailure)]
    [InlineData(503, StatusKind.LoadFailure)]
    [InlineData(418, StatusKind.LoadFailure)] // unknown → fail-safe LoadFailure (AC1 default)
    [InlineData(0, StatusKind.LoadFailure)]
    [InlineData(300, StatusKind.LoadFailure)]
    public void FromHttpStatus_maps_each_status_to_its_canonical_state(int statusCode, StatusKind expected)
        => StatusPresentation.FromHttpStatus(statusCode).ShouldBe(expected);

    [Theory]
    [InlineData("Tenant warming up", null, StatusKind.TenantUnavailable)] // tenant token in Title
    [InlineData("Forbidden", "Role not permitted", StatusKind.Forbidden)] // neither mentions tenant
    [InlineData("Access denied", "Tenant is warming up", StatusKind.TenantUnavailable)] // token in Detail, not Title
    [InlineData("TENANT unavailable", null, StatusKind.TenantUnavailable)] // case-insensitive match
    public void FromClientException_splits_403_into_tenant_vs_role(string title, string? detail, StatusKind expected)
    {
        var exception = new PartiesClientException(403, title, null, detail, null);

        StatusPresentation.FromClientException(exception).ShouldBe(expected);
    }

    [Fact]
    public void FromClientException_delegates_non_403_statuses_to_the_http_mapping()
    {
        StatusPresentation.FromClientException(new PartiesClientException(401, "Unauthorized", null, null, null))
            .ShouldBe(StatusKind.SignInRequired);
        StatusPresentation.FromClientException(new PartiesClientException(404, "Not Found", null, null, null))
            .ShouldBe(StatusKind.Gone);
        StatusPresentation.FromClientException(new PartiesClientException(500, "Server Error", null, null, null))
            .ShouldBe(StatusKind.LoadFailure);
    }

    [Fact]
    public void FromClientException_rejects_a_null_exception()
        => Should.Throw<ArgumentNullException>(() => StatusPresentation.FromClientException(null!));

    [Theory]
    [MemberData(nameof(EveryFreshnessStatus))]
    public void FromFreshness_maps_only_Current_to_fresh_and_everything_else_to_Degraded(ProjectionFreshnessStatus status)
    {
        StatusKind? expected = status == ProjectionFreshnessStatus.Current ? null : StatusKind.Degraded;

        StatusPresentation.FromFreshness(status).ShouldBe(expected);
    }

    [Fact]
    public void FromFreshness_metadata_overload_reads_the_status()
    {
        StatusPresentation.FromFreshness(ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current))
            .ShouldBeNull();
        StatusPresentation.FromFreshness(ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale))
            .ShouldBe(StatusKind.Degraded);
    }

    [Fact]
    public void FromFreshness_metadata_overload_rejects_null()
        => Should.Throw<ArgumentNullException>(() => StatusPresentation.FromFreshness((ProjectionFreshnessMetadata)null!));

    [Theory]
    [MemberData(nameof(EveryStatusKind))]
    public void PolitenessFor_maps_every_state_to_its_expected_politeness(StatusKind kind)
    {
        // A new StatusKind with no entry here (and no switch arm) fails loudly — never a silent default.
        ExpectedPoliteness.ShouldContainKey(kind);

        StatusPresentation.PolitenessFor(kind).ShouldBe(ExpectedPoliteness[kind]);
    }

    [Fact]
    public void PolitenessFor_never_announces_a_validation_or_failure_state_politely()
    {
        // The named anti-pattern guard (architecture.md): an error must never be polite.
        StatusPresentation.PolitenessFor(StatusKind.Validation).ShouldBe(LiveRegionPoliteness.Assertive);
        StatusPresentation.PolitenessFor(StatusKind.Forbidden).ShouldBe(LiveRegionPoliteness.Assertive);
        StatusPresentation.PolitenessFor(StatusKind.TransientFailure).ShouldBe(LiveRegionPoliteness.Assertive);
        StatusPresentation.PolitenessFor(StatusKind.LoadFailure).ShouldBe(LiveRegionPoliteness.Assertive);
    }

    [Fact]
    public void FromException_treats_timeouts_and_transport_cancellation_as_transient()
    {
        StatusPresentation.FromException(new TimeoutException()).ShouldBe(StatusKind.TransientFailure);
        StatusPresentation.FromException(new TaskCanceledException()).ShouldBe(StatusKind.TransientFailure);
        StatusPresentation.FromException(new OperationCanceledException()).ShouldBe(StatusKind.TransientFailure);
    }

    [Fact]
    public void FromException_treats_a_408_client_exception_as_transient()
    {
        var timeout408 = new PartiesClientException(408, "Request Timeout", null, null, null);

        StatusPresentation.FromException(timeout408).ShouldBe(StatusKind.TransientFailure);
        StatusPresentation.FromClientException(timeout408).ShouldBe(StatusKind.TransientFailure);
    }

    [Theory]
    [InlineData(401, "Unauthorized", null, StatusKind.SignInRequired)]
    [InlineData(403, "Tenant warming up", null, StatusKind.TenantUnavailable)] // tenant split survives the broad catch
    [InlineData(403, "Forbidden", "Role not permitted", StatusKind.Forbidden)]
    [InlineData(404, "Not Found", null, StatusKind.Gone)]
    [InlineData(500, "Server Error", null, StatusKind.LoadFailure)]
    public void FromException_routes_a_client_exception_through_the_full_mapping(
        int status, string title, string? detail, StatusKind expected)
    {
        // AC4 call sites catch broadly and call FromException; it must preserve the WHOLE FromClientException
        // mapping (incl. the 403 tenant-vs-role split), not just the 408/timeout case.
        var exception = new PartiesClientException(status, title, null, detail, null);

        StatusPresentation.FromException(exception).ShouldBe(expected);
    }

    [Fact]
    public void FromClientException_treats_a_403_with_no_problem_text_as_Forbidden()
    {
        // The tenant heuristic must be null-safe: a 403 carrying no Title/Detail is a hard denial, not a crash.
        var exception = new PartiesClientException(403, null!, null, null, null);

        StatusPresentation.FromClientException(exception).ShouldBe(StatusKind.Forbidden);
    }

    [Fact]
    public void FromException_treats_an_unrelated_exception_as_a_load_failure()
        => StatusPresentation.FromException(new InvalidOperationException()).ShouldBe(StatusKind.LoadFailure);

    [Fact]
    public void FromException_rejects_a_null_exception()
        => Should.Throw<ArgumentNullException>(() => StatusPresentation.FromException(null!));

    [Theory]
    [InlineData(LiveRegionPoliteness.Polite, "status", "polite")]
    [InlineData(LiveRegionPoliteness.Assertive, "alert", "assertive")]
    [InlineData(LiveRegionPoliteness.None, null, null)]
    public void LiveRegionAttributes_returns_the_single_source_ARIA_strings(
        LiveRegionPoliteness politeness, string? expectedRole, string? expectedAriaLive)
    {
        (string? role, string? ariaLive) = StatusPresentation.LiveRegionAttributes(politeness);

        role.ShouldBe(expectedRole);
        ariaLive.ShouldBe(expectedAriaLive);
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

    public static TheoryData<StatusKind> EveryStatusKind()
    {
        var data = new TheoryData<StatusKind>();
        foreach (StatusKind kind in Enum.GetValues<StatusKind>())
        {
            data.Add(kind);
        }

        return data;
    }
}
