using System.Text;

using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class ConsumerPrivacyExportClientTests
{
    [Fact]
    public async Task ExportMyDataAsync_DelegatesToSelfScopedExportWithoutCallerSuppliedIdentity()
    {
        using var cts = new CancellationTokenSource();
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.ExportMyDataAsync(cts.Token).Returns(new AdminPortalExportDownload(
            "party-party-bound-001-export-20260610000000Z.json",
            "application/json",
            """{"partyId":"party-bound-001","tenantId":"tenant-secret","status":"Exported","exportedAt":"2026-06-10T00:00:00Z","exportedBy":"operator-secret","correlationId":"corr-secret","party":null,"processingRecords":[]}"""u8.ToArray()));
        var sut = new ConsumerPrivacyExportClient(selfScoped);

        ConsumerPrivacyExportResult actual = await sut.ExportMyDataAsync(cts.Token);

        actual.Outcome.ShouldBe(ConsumerPrivacyExportOutcome.Ready);
        actual.ContentType.ShouldBe("application/json");
        actual.SafeFileName.ShouldStartWith("my-data-export-");
        actual.SafeFileName.ShouldEndWith("Z.json");
        actual.SafeFileName.ShouldNotContain("party-bound-001", Case.Sensitive);
        actual.SafeFileName.ShouldNotContain("tenant-secret", Case.Sensitive);
        actual.Payload.ShouldNotBeEmpty();
        await selfScoped.Received(1).ExportMyDataAsync(cts.Token);
        await selfScoped.DidNotReceive().GetMyPartyAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("RestrictedExported", ConsumerPrivacyExportOutcome.Restricted)]
    [InlineData("Erased", ConsumerPrivacyExportOutcome.Erased)]
    [InlineData("PersonalDataUnavailable", ConsumerPrivacyExportOutcome.Unavailable)]
    public async Task ExportMyDataAsync_MapsTerminalPackageStatusesToSafeOutcomes(
        string status,
        ConsumerPrivacyExportOutcome expected)
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.ExportMyDataAsync(Arg.Any<CancellationToken>()).Returns(new AdminPortalExportDownload(
            $"server-{status}-secret.json",
            "application/json",
            Encoding.UTF8.GetBytes(
                $$"""
                {"partyId":"party-secret","tenantId":"tenant-secret","status":"{{status}}","exportedAt":"2026-06-10T00:00:00Z","exportedBy":"operator-secret","correlationId":"corr-secret","party":null,"processingRecords":[]}
                """)));
        var sut = new ConsumerPrivacyExportClient(selfScoped);

        ConsumerPrivacyExportResult actual = await sut.ExportMyDataAsync();

        actual.Outcome.ShouldBe(expected);
        actual.Payload.ShouldNotBeEmpty();
        actual.SafeFileName.ShouldNotContain(status, Case.Sensitive);
    }

    [Fact]
    public async Task ExportMyDataAsync_TreatsEmptyPayloadAsTransientFailure()
    {
        ISelfScopedPartiesClient selfScoped = Substitute.For<ISelfScopedPartiesClient>();
        selfScoped.ExportMyDataAsync(Arg.Any<CancellationToken>()).Returns(new AdminPortalExportDownload(
            "server-empty.json",
            "application/json",
            []));
        var sut = new ConsumerPrivacyExportClient(selfScoped);

        ConsumerPrivacyExportResult actual = await sut.ExportMyDataAsync();

        actual.Outcome.ShouldBe(ConsumerPrivacyExportOutcome.TransientFailure);
        actual.Payload.ShouldBeEmpty();
    }

    [Fact]
    public void ConsumerPrivacyExportClient_IsRegisteredAsScopedAdapter()
    {
        var services = new ServiceCollection();
        services.AddScoped<IConsumerPrivacyExportClient, ConsumerPrivacyExportClient>();

        services.ShouldContain(static descriptor =>
            descriptor.ServiceType == typeof(IConsumerPrivacyExportClient)
            && descriptor.ImplementationType == typeof(ConsumerPrivacyExportClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
