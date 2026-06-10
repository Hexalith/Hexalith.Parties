using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class MyPrivacyPageTests : BunitContext
{
    public MyPrivacyPageTests()
    {
        Services.AddFluentUIComponents();
        Services.TryAddSingleton<IConsumerPrivacyErasureClient>(new QueuePrivacyErasureClient());
        JSInterop.SetupVoid("HexalithPartiesConsumerPortal.downloadJson", _ => true);
    }

    [Fact]
    public void MyPrivacyPage_InitialRender_ReplacesPlaceholderWithExportAction()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());

        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        cut.Markup.ShouldContain("Export my data");
        cut.Markup.ShouldContain("Delete my data");
        cut.Markup.ShouldContain("right to erasure");
        cut.Markup.ShouldContain("Machine-readable JSON");
        cut.Markup.ShouldNotContain("ConsumerRouteShell");
        cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(1);
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Ready to prepare your export.");
    }

    [Fact]
    public void MyPrivacyPage_DeleteMyData_UsesInAppConfirmationWithoutTypedPii()
    {
        var erasure = new QueuePrivacyErasureClient();
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());
        Services.Replace(ServiceDescriptor.Singleton<IConsumerPrivacyErasureClient>(erasure));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Delete my data");

        cut.Find("[role='dialog']").TextContent.ShouldContain("Confirm deletion request");
        cut.Find("[role='dialog']").TextContent.ShouldContain("You can cancel until deletion begins");
        cut.Markup.ShouldNotContain("prompt", Case.Insensitive);
        cut.Markup.ShouldNotContain("confirm(", Case.Insensitive);
        cut.Markup.ShouldNotContain("display name", Case.Insensitive);
        cut.Markup.ShouldNotContain("email", Case.Insensitive);
        erasure.RequestCount.ShouldBe(0);
    }

    [Fact]
    public void MyPrivacyPage_RequestAccepted_ShowsCancellableCopyAndCancelAction()
    {
        var erasure = new QueuePrivacyErasureClient(
            status: _ => Task.FromResult(ConsumerPrivacyErasureResult.Active()),
            request: _ => Task.FromResult(new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                ConsumerPrivacyErasureState.ErasurePending,
                CanCancel: true)));
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());
        Services.Replace(ServiceDescriptor.Singleton<IConsumerPrivacyErasureClient>(erasure));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Delete my data");
        ClickFluentButton(cut, "Confirm delete my data");

        cut.WaitForAssertion(() =>
        {
            erasure.RequestCount.ShouldBe(1);
            cut.Markup.ShouldContain("You can cancel until deletion begins");
            cut.FindAll("[role='status'][aria-live='polite']")
                .Count(node => node.TextContent.Contains("Deletion requested.", StringComparison.Ordinal))
                .ShouldBe(1);
            cut.Markup.ShouldContain("Cancel deletion request");
        });
    }

    [Fact]
    public void MyPrivacyPage_CancelAccepted_ReconcilesToAuthoritativeStatus()
    {
        var erasure = new QueuePrivacyErasureClient(
            status: _ => Task.FromResult(new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                ConsumerPrivacyErasureState.ErasurePending,
                CanCancel: true)),
            cancel: _ => Task.FromResult(ConsumerPrivacyErasureResult.Active()));
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());
        Services.Replace(ServiceDescriptor.Singleton<IConsumerPrivacyErasureClient>(erasure));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Cancel deletion request"));

        ClickFluentButton(cut, "Cancel deletion request");

        cut.WaitForAssertion(() =>
        {
            erasure.CancelCount.ShouldBe(1);
            cut.Markup.ShouldContain("No deletion request is active.");
            cut.Markup.ShouldContain("Delete my data");
        });
    }

    [Theory]
    [InlineData(ConsumerPrivacyErasureState.KeyDestroyed)]
    [InlineData(ConsumerPrivacyErasureState.VerificationInProgress)]
    [InlineData(ConsumerPrivacyErasureState.Verified)]
    public void MyPrivacyPage_DeletionStarted_DisablesCancelWithNeutralCopy(ConsumerPrivacyErasureState state)
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());
        Services.Replace(ServiceDescriptor.Singleton<IConsumerPrivacyErasureClient>(new QueuePrivacyErasureClient(
            status: _ => Task.FromResult(new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                state,
                CanCancel: false)))));

        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Deletion has begun. Cancellation is no longer available.");
            cut.Markup.ShouldContain("Cancellation is unavailable after deletion begins.");
            cut.Markup.ShouldNotContain("succeeded", Case.Insensitive);
        });
    }

    [Fact]
    public void MyPrivacyPage_ErasedState_StatesPermanentAndDoesNotShowCancel()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());
        Services.Replace(ServiceDescriptor.Singleton<IConsumerPrivacyErasureClient>(new QueuePrivacyErasureClient(
            status: _ => Task.FromResult(new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Permanent,
                ConsumerPrivacyErasureState.Erased,
                CanCancel: false)))));

        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Once it's done, it's permanent - we can't undo it.");
            cut.Markup.ShouldNotContain("Cancel deletion request");
        });
    }

    [Fact]
    public void MyPrivacyPage_ErasureFailures_UseBoundedPiiFreeAlertAndKeepExportVisible()
    {
        Services.AddSingleton<IConsumerPrivacyExportClient>(new QueuePrivacyExportClient());
        Services.Replace(ServiceDescriptor.Singleton<IConsumerPrivacyErasureClient>(new QueuePrivacyErasureClient(
            request: _ => Task.FromResult(ConsumerPrivacyErasureResult.Failure(ConsumerPrivacyErasureOutcome.Rejected)))));
        IRenderedComponent<MyPrivacyPage> cut = Render<MyPrivacyPage>();

        ClickFluentButton(cut, "Delete my data");
        ClickFluentButton(cut, "Confirm delete my data");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("Deletion has already begun or cannot be changed right now.");
            cut.Markup.ShouldContain("Export my data");
            cut.Markup.ShouldNotContain("party-", Case.Sensitive);
            cut.Markup.ShouldNotContain("tenant", Case.Insensitive);
            cut.Markup.ShouldNotContain("correlation", Case.Insensitive);
            cut.Markup.ShouldNotContain("within 30 days", Case.Insensitive);
        });
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

    private sealed class QueuePrivacyErasureClient(
        Func<CancellationToken, Task<ConsumerPrivacyErasureResult>>? status = null,
        Func<CancellationToken, Task<ConsumerPrivacyErasureResult>>? request = null,
        Func<CancellationToken, Task<ConsumerPrivacyErasureResult>>? cancel = null) : IConsumerPrivacyErasureClient
    {
        private readonly Func<CancellationToken, Task<ConsumerPrivacyErasureResult>> _status =
            status ?? (_ => Task.FromResult(ConsumerPrivacyErasureResult.Active()));
        private readonly Func<CancellationToken, Task<ConsumerPrivacyErasureResult>> _request =
            request ?? (_ => Task.FromResult(new ConsumerPrivacyErasureResult(
                ConsumerPrivacyErasureOutcome.Pending,
                ConsumerPrivacyErasureState.ErasurePending,
                CanCancel: true)));
        private readonly Func<CancellationToken, Task<ConsumerPrivacyErasureResult>> _cancel =
            cancel ?? (_ => Task.FromResult(ConsumerPrivacyErasureResult.Active()));

        public int RequestCount { get; private set; }

        public int CancelCount { get; private set; }

        public Task<ConsumerPrivacyErasureResult> GetMyErasureStatusAsync(CancellationToken cancellationToken = default)
            => _status(cancellationToken);

        public Task<ConsumerPrivacyErasureResult> RequestMyErasureAsync(CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return _request(cancellationToken);
        }

        public Task<ConsumerPrivacyErasureResult> CancelMyErasureAsync(CancellationToken cancellationToken = default)
        {
            CancelCount++;
            return _cancel(cancellationToken);
        }
    }
}
