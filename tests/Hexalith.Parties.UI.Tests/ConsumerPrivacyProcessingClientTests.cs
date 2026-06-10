using System.Net.Http;

using Hexalith.Parties.Client;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class ConsumerPrivacyProcessingClientTests
{
    [Fact]
    public async Task GetMyProcessingSummaryAsync_DelegatesToSelfScopedRecordsWithoutCallerSuppliedIdentity()
    {
        using var cts = new CancellationTokenSource();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GetMyProcessingRecordsAsync(cts.Token).Returns(
        [
            new ProcessingActivityRecord
            {
                SequenceNumber = 7,
                PartyId = "party-bound-001",
                TenantId = "tenant-secret",
                ActorId = "operator-secret",
                CorrelationId = "corr-secret",
                OperationCategory = "Read",
                Outcome = "Completed",
                EventType = "GdprOperationRecorded",
                Timestamp = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
                Summary = "Bounded GDPR operation record",
            },
        ]);
        var sut = new ConsumerPrivacyProcessingClient(selfScoped);

        ConsumerPrivacyProcessingResult actual = await sut.GetMyProcessingSummaryAsync(cts.Token);

        actual.Outcome.ShouldBe(ConsumerPrivacyProcessingOutcome.Ready);
        ConsumerPrivacyProcessingRecord record = actual.Records.ShouldHaveSingleItem();
        record.Category.ShouldBe(ConsumerPrivacyProcessingCategory.DataRead);
        record.Outcome.ShouldBe(ConsumerPrivacyProcessingRecordOutcome.Completed);
        record.Summary.ShouldBe("Bounded GDPR operation record");
        record.Summary.ShouldNotContain("party-bound-001", Case.Sensitive);
        record.Summary.ShouldNotContain("tenant-secret", Case.Sensitive);
        record.Summary.ShouldNotContain("operator-secret", Case.Sensitive);
        record.Summary.ShouldNotContain("corr-secret", Case.Sensitive);
        await selfScoped.Received(1).GetMyProcessingRecordsAsync(cts.Token);
        await selfScoped.DidNotReceive().GetMyPartyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyProcessingSummaryAsync_MapsEmptyRecordsToEmptyOutcome()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GetMyProcessingRecordsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = new ConsumerPrivacyProcessingClient(selfScoped);

        ConsumerPrivacyProcessingResult actual = await sut.GetMyProcessingSummaryAsync();

        actual.Outcome.ShouldBe(ConsumerPrivacyProcessingOutcome.Empty);
        actual.Records.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Export", "Accepted", ConsumerPrivacyProcessingCategory.DataExport, ConsumerPrivacyProcessingRecordOutcome.Accepted)]
    [InlineData("Consent", "Restricted", ConsumerPrivacyProcessingCategory.Consent, ConsumerPrivacyProcessingRecordOutcome.Limited)]
    [InlineData("Erasure", "Rejected", ConsumerPrivacyProcessingCategory.Erasure, ConsumerPrivacyProcessingRecordOutcome.Failed)]
    [InlineData("raw-event-type", "backend-status", ConsumerPrivacyProcessingCategory.Unknown, ConsumerPrivacyProcessingRecordOutcome.Unknown)]
    public async Task GetMyProcessingSummaryAsync_MapsBackendMetadataToBoundedEnums(
        string category,
        string outcome,
        ConsumerPrivacyProcessingCategory expectedCategory,
        ConsumerPrivacyProcessingRecordOutcome expectedOutcome)
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GetMyProcessingRecordsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ProcessingActivityRecord
            {
                SequenceNumber = 1,
                OperationCategory = category,
                Outcome = outcome,
                EventType = "RawBackendEventType",
                Timestamp = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
                Summary = "Safe summary",
            },
        ]);
        var sut = new ConsumerPrivacyProcessingClient(selfScoped);

        ConsumerPrivacyProcessingResult actual = await sut.GetMyProcessingSummaryAsync();

        ConsumerPrivacyProcessingRecord record = actual.Records.ShouldHaveSingleItem();
        record.Category.ShouldBe(expectedCategory);
        record.Outcome.ShouldBe(expectedOutcome);
    }

    [Fact]
    public async Task GetMyProcessingSummaryAsync_DropsUnsafeBackendSummaryContent()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GetMyProcessingRecordsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ProcessingActivityRecord
            {
                SequenceNumber = 1,
                PartyId = "party-bound-001",
                TenantId = "tenant-secret",
                ActorId = "operator-secret",
                CorrelationId = "corr-secret",
                OperationCategory = "Read",
                Outcome = "Completed",
                EventType = "RawBackendEventType",
                Timestamp = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
                Summary = "ProblemDetails payload for party-bound-001 tenant-secret operator-secret corr-secret RawBackendEventType",
            },
            new ProcessingActivityRecord
            {
                SequenceNumber = 2,
                OperationCategory = "Read",
                Outcome = "Completed",
                EventType = "GdprOperationRecorded",
                Timestamp = new DateTimeOffset(2026, 6, 10, 12, 1, 0, TimeSpan.Zero),
                Summary = """{"partyId":"party-bound-001","displayName":"Ada Secret"}""",
            },
        ]);
        var sut = new ConsumerPrivacyProcessingClient(selfScoped);

        ConsumerPrivacyProcessingResult actual = await sut.GetMyProcessingSummaryAsync();

        actual.Records.ShouldAllBe(static record => record.Summary.Length == 0);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException), ConsumerPrivacyProcessingOutcome.Forbidden)]
    [InlineData(typeof(HttpRequestException), ConsumerPrivacyProcessingOutcome.TransientFailure)]
    [InlineData(typeof(TimeoutException), ConsumerPrivacyProcessingOutcome.TransientFailure)]
    public async Task GetMyProcessingSummaryAsync_MapsFailuresToBoundedOutcomes(
        Type exceptionType,
        ConsumerPrivacyProcessingOutcome expected)
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        selfScoped.GetMyProcessingRecordsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ProcessingActivityRecord>>(exception));
        var sut = new ConsumerPrivacyProcessingClient(selfScoped);

        ConsumerPrivacyProcessingResult actual = await sut.GetMyProcessingSummaryAsync();

        actual.Outcome.ShouldBe(expected);
        actual.Records.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(401, ConsumerPrivacyProcessingOutcome.AuthenticationRequired)]
    [InlineData(403, ConsumerPrivacyProcessingOutcome.Forbidden)]
    [InlineData(404, ConsumerPrivacyProcessingOutcome.Unavailable)]
    [InlineData(410, ConsumerPrivacyProcessingOutcome.Erased)]
    [InlineData(503, ConsumerPrivacyProcessingOutcome.Unavailable)]
    public async Task GetMyProcessingSummaryAsync_MapsTypedClientFailuresToBoundedOutcomes(
        int status,
        ConsumerPrivacyProcessingOutcome expected)
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GetMyProcessingRecordsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ProcessingActivityRecord>>(
                new PartiesClientException(status, "Raw downstream title", "about:blank", "ProblemDetails party-bound-001", "corr-secret")));
        var sut = new ConsumerPrivacyProcessingClient(selfScoped);

        ConsumerPrivacyProcessingResult actual = await sut.GetMyProcessingSummaryAsync();

        actual.Outcome.ShouldBe(expected);
        actual.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMyProcessingSummaryAsync_PreservesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.GetMyProcessingRecordsAsync(cts.Token)
            .Returns(Task.FromException<IReadOnlyList<ProcessingActivityRecord>>(new OperationCanceledException(cts.Token)));
        var sut = new ConsumerPrivacyProcessingClient(selfScoped);

        await Should.ThrowAsync<OperationCanceledException>(() => sut.GetMyProcessingSummaryAsync(cts.Token));
    }

    [Fact]
    public void ConsumerPrivacyProcessingClient_IsRegisteredAsScopedAdapter()
    {
        var services = new ServiceCollection();
        services.AddScoped<IConsumerPrivacyProcessingClient, ConsumerPrivacyProcessingClient>();

        services.ShouldContain(static descriptor =>
            descriptor.ServiceType == typeof(IConsumerPrivacyProcessingClient)
            && descriptor.ImplementationType == typeof(ConsumerPrivacyProcessingClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
