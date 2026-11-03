using Hexalith.EventStore.Client.Gateway;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises diagnostic lifetime and actual persistence failures independently of an unavailable native-proof gateway.</summary>
public sealed class MemoriesAuthoritativePersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiagnosticWriteFailurePreservesGatewayRefusalAndCaptureFailure(bool failCapture)
    {
        var primary = new EventStoreGatewayException(503, "Native proof unavailable");
        var captureFailure = new IOException("actor diagnostics failed");
        using var operation = new CancellationTokenSource();
        await operation.CancelAsync();
        string directory = Path.Combine(Path.GetTempPath(), "native-proof-refusal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        bool captureEntered = false, persistenceEntered = false;
        try
        {
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => MemoriesAuthoritativePersistence.CaptureNativeRefusalAsync(
                primary, async token =>
                {
                    operation.IsCancellationRequested.ShouldBeTrue();
                    token.IsCancellationRequested.ShouldBeFalse();
                    token.CanBeCanceled.ShouldBeTrue();
                    captureEntered = true;
                    await Task.Delay(1, token).ConfigureAwait(false);
                    if (failCapture) { throw captureFailure; }
                }, async (captured, token) =>
                {
                    captured.ShouldBe(failCapture ? captureFailure : null);
                    token.IsCancellationRequested.ShouldBeFalse();
                    persistenceEntered = true;
                    // A directory is an invalid file destination on every supported filesystem.
                    await File.WriteAllTextAsync(directory, "synthetic refusal", token).ConfigureAwait(false);
                }));
            captureEntered.ShouldBeTrue(); persistenceEntered.ShouldBeTrue();
            failure.InnerExceptions.ShouldContain(primary);
            (failure.InnerExceptions[^1] is IOException or UnauthorizedAccessException).ShouldBeTrue();
            if (failCapture) { failure.InnerExceptions.ShouldContain(captureFailure); }
        }
        finally { Directory.Delete(directory); }
    }

    [Fact]
    public async Task CanceledOperationStillCapturesDiagnosticsThenRethrowsTheOriginalRefusal()
    {
        var primary = new EventStoreGatewayException(503, "Native proof unavailable");
        using var operation = new CancellationTokenSource();
        await operation.CancelAsync();
        string path = Path.Combine(Path.GetTempPath(), "native-proof-captured-" + Guid.NewGuid().ToString("N") + ".txt");
        bool captured = false;
        try
        {
            EventStoreGatewayException failure = await Should.ThrowAsync<EventStoreGatewayException>(() => MemoriesAuthoritativePersistence.CaptureNativeRefusalAsync(
                primary, async token =>
                {
                    operation.IsCancellationRequested.ShouldBeTrue();
                    token.IsCancellationRequested.ShouldBeFalse();
                    token.CanBeCanceled.ShouldBeTrue();
                    await Task.Delay(1, token).ConfigureAwait(false);
                    captured = true;
                }, (captureFailure, token) =>
                {
                    captureFailure.ShouldBeNull();
                    captured.ShouldBeTrue();
                    token.IsCancellationRequested.ShouldBeFalse();
                    return File.WriteAllTextAsync(path, "synthetic refusal", token);
                }));
            failure.ShouldBeSameAs(primary);
            (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ShouldBe("synthetic refusal");
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task TimedOutCaptureFinishesCancellationCleanupBeforeFreshBudgetPersistence()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var primary = new EventStoreGatewayException(503, "Native proof unavailable");
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleaned = false, persisted = false;
        string path = Path.Combine(Path.GetTempPath(), "native-proof-cleaned-" + Guid.NewGuid().ToString("N") + ".txt");
        Task capture = MemoriesAuthoritativePersistence.CaptureNativeRefusalAsync(primary, async captureToken =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, captureToken).ConfigureAwait(false); }
            finally
            {
                cleanupEntered.TrySetResult();
                await finishCleanup.Task.ConfigureAwait(false);
                cleaned = true;
            }
        }, async (failure, persistenceToken) =>
        {
            cleaned.ShouldBeTrue("Receipt persistence must follow actual cooperative cancellation cleanup.");
            failure.ShouldBeOfType<TaskCanceledException>();
            persistenceToken.IsCancellationRequested.ShouldBeFalse();
            await Task.Delay(30, persistenceToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(path, "captured after cleanup", persistenceToken).ConfigureAwait(false);
            persisted = true;
        }, captureBudget: TimeSpan.FromMilliseconds(20));
        try
        {
            await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            capture.IsCompleted.ShouldBeFalse();
            persisted.ShouldBeFalse();
            finishCleanup.TrySetResult();
            EventStoreGatewayException failure = await Should.ThrowAsync<EventStoreGatewayException>(() => capture.WaitAsync(TimeSpan.FromSeconds(5), token));
            failure.ShouldBeSameAs(primary);
            persisted.ShouldBeTrue();
            (await File.ReadAllTextAsync(path, token)).ShouldBe("captured after cleanup");
        }
        finally { finishCleanup.TrySetResult(); File.Delete(path); }
    }
}
