using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class MyPrivacyPageTests : BunitContext
{
    public MyPrivacyPageTests()
    {
        Services.AddFluentUIComponents();
        JSInterop.SetupVoid("HexalithPartiesConsumerPortal.downloadJson", _ => true);
    }

    [Fact]
    public void MyPrivacyPage_InitialRender_ReplacesPlaceholderWithExportAction()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());

        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        cut.Markup.ShouldContain("Export my data");
        cut.Markup.ShouldContain("Machine-readable JSON");
        cut.Markup.ShouldNotContain("ConsumerRouteShell");
        cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(1);
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Ready to prepare your export.");
    }

    [Fact]
    public void MyPrivacyPage_Preparing_UsesApprovedCopyAndOneStatusSource()
    {
        var pending = new TaskCompletionSource<ConsumerPrivacyExportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new QueuePrivacyExportClient(_ => pending.Task);
        Services.AddSingleton<IConsumerPrivacyExportClient>(client);
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Export my data");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(1);
            cut.Find("[role='status'][aria-live='polite']").TextContent
                .ShouldContain("Preparing your export - this can take a little while. We'll show it here the moment it's ready.");
            cut.Markup.ShouldNotContain("under one minute", Case.Insensitive);
            cut.Markup.ShouldNotContain("within 30 days", Case.Insensitive);
            client.CallCount.ShouldBe(1);
        });
    }

    [Fact]
    public void MyPrivacyPage_ReadyState_ShowsDownloadActionWithoutEchoingTransportData()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient(_ => Task.FromResult(Ready(
            payload: """{"partyId":"party-bound-001","tenantId":"tenant-secret","party":{"displayName":"Ada Secret"}}"""u8.ToArray()))));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Export my data");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Your JSON export is ready.");
            cut.Markup.ShouldContain("Download JSON");
            cut.Markup.ShouldNotContain("party-bound-001", Case.Sensitive);
            cut.Markup.ShouldNotContain("tenant-secret", Case.Sensitive);
            cut.Markup.ShouldNotContain("Ada Secret", Case.Sensitive);
        });
    }

    [Fact]
    public void MyPrivacyPage_Download_UsesStreamHelperWithSafeFileName()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient(_ => Task.FromResult(Ready(
            safeFileName: "my-data-export-20260610000000Z.json"))));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();
        ClickFluentButton(cut, "Export my data");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Download JSON"));

        ClickFluentButton(cut, "Download JSON");

        cut.WaitForAssertion(() =>
        {
            var invocation = JSInterop.Invocations.Single(i =>
                i.Identifier == "HexalithPartiesConsumerPortal.downloadJson");
            invocation.Arguments[0].ShouldBe("my-data-export-20260610000000Z.json");
            invocation.Arguments[1].ShouldBe("application/json");
            invocation.Arguments[2].ShouldBeOfType<DotNetStreamReference>();
        });
    }

    [Fact]
    public void MyPrivacyPage_EmptyPayloadAndTransientFailure_RenderRetryAlert()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient(_ => Task.FromResult(Ready(payload: []))));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Export my data");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("Your data is safe - try again.");
            cut.Markup.ShouldContain("Wait a short time, then retry the export.");
            cut.Markup.ShouldNotContain("completed", Case.Insensitive);
        });
    }

    [Fact]
    public void MyPrivacyPage_FailureState_UsesOneAlertAndNoCompetingStatus()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient(_ =>
            Task.FromResult(ConsumerPrivacyExportResult.Failure(ConsumerPrivacyExportOutcome.TransientFailure))));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Export my data");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role='alert']").Count.ShouldBe(1);
            cut.Find("[role='alert']").TextContent.ShouldContain("Your data is safe - try again.");
            cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(0);
        });
    }

    [Fact]
    public void MyPrivacyPage_JsFailure_MapsToTransientAlertAndKeepsDownloadVisible()
    {
        JSInterop.SetupVoid("HexalithPartiesConsumerPortal.downloadJson", _ => true)
            .SetException(new JSException("download helper unavailable"));
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient(_ => Task.FromResult(Ready())));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();
        ClickFluentButton(cut, "Export my data");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Download JSON"));

        ClickFluentButton(cut, "Download JSON");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("Your data is safe - try again.");
            cut.Markup.ShouldContain("Download JSON");
        });
    }

    [Theory]
    [InlineData(ConsumerPrivacyExportOutcome.Restricted, "Some data may be limited.")]
    [InlineData(ConsumerPrivacyExportOutcome.Erased, "Only status information is available for this profile.")]
    [InlineData(ConsumerPrivacyExportOutcome.Unavailable, "Personal data is unavailable right now.")]
    public void MyPrivacyPage_SafeTerminalStatuses_DoNotExposeRawValues(
        ConsumerPrivacyExportOutcome outcome,
        string expectedCopy)
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient(_ => Task.FromResult(Ready(
            outcome: outcome,
            payload: """{"status":"Erased","partyId":"party-secret","correlationId":"corr-secret"}"""u8.ToArray()))));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Export my data");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain(expectedCopy);
            cut.Markup.ShouldNotContain("party-secret", Case.Sensitive);
            cut.Markup.ShouldNotContain("corr-secret", Case.Sensitive);
        });
    }

    private static void ClickFluentButton(IRenderedComponent<MyPrivacyPage> cut, string text)
        => cut.FindAll("fluent-button")
            .First(button => button.TextContent.Contains(text, StringComparison.Ordinal))
            .Click();

    private static ConsumerPrivacyExportResult Ready(
        ConsumerPrivacyExportOutcome outcome = ConsumerPrivacyExportOutcome.Ready,
        string safeFileName = "my-data-export-20260610000000Z.json",
        byte[]? payload = null)
        => new(
            outcome,
            safeFileName,
            "application/json",
            payload ?? """{"status":"Exported"}"""u8.ToArray(),
            ConsumerPrivacyExportStatus.Exported,
            ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current));

    private sealed class QueuePrivacyExportClient(
        Func<CancellationToken, Task<ConsumerPrivacyExportResult>>? export = null) : IConsumerPrivacyExportClient
    {
        private readonly Func<CancellationToken, Task<ConsumerPrivacyExportResult>> _export =
            export ?? (_ => Task.FromResult(Ready()));

        public int CallCount { get; private set; }

        public Task<ConsumerPrivacyExportResult> ExportMyDataAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _export(cancellationToken);
        }
    }
}
