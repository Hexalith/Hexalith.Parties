using Dapr.Client;

using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

public sealed class PartyMemoryUnitMappingStoreTests
{
    private const string TenantId = "tenant-secret";
    private const string PartyId = "party-secret";
    private const string MemoryUnitId = "unit-secret";
    private const string SourceUri = "urn:secret";
    private const string CaseId = "case-secret";
    private const string ProviderFailure = "Ada Lovelace tenant-secret party-secret unit-secret urn:secret case-secret";

    private static readonly string[] _sensitiveValues = [TenantId, PartyId, MemoryUnitId, SourceUri, CaseId];

    [Fact]
    public async Task RecordMappingAsync_PersistsSuppliedCaseIdInActualStatePayload()
    {
        List<PartyMemoryUnitMappingEntry>? saved = null;
        DaprClient client = CreateWritableClient([], value => saved = [.. value]);
        var store = CreateStore(client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await store.RecordMappingAsync(
            TenantId,
            PartyId,
            MemoryUnitId,
            SourceUri,
            CaseId,
            CancellationToken.None);

        PartyMemoryUnitMappingEntry entry = saved.ShouldNotBeNull().ShouldHaveSingleItem();
        entry.MemoryUnitId.ShouldBe(MemoryUnitId);
        entry.SourceUri.ShouldBe(SourceUri);
        entry.CaseId.ShouldBe(CaseId);
    }

    [Fact]
    public async Task RecordMappingAsync_AdoptsMatchingLegacyEntryIntoSuppliedCase()
    {
        List<PartyMemoryUnitMappingEntry>? saved = null;
        DaprClient client = CreateWritableClient(
            [new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri)],
            value => saved = [.. value]);
        var store = CreateStore(client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await store.RecordMappingAsync(
            TenantId,
            PartyId,
            MemoryUnitId,
            SourceUri,
            CaseId,
            CancellationToken.None);

        PartyMemoryUnitMappingEntry entry = saved.ShouldNotBeNull().ShouldHaveSingleItem();
        entry.CaseId.ShouldBe(CaseId);
    }

    [Fact]
    public async Task RecordMappingAsync_ReplacesStaleIdentityWithinSameCase()
    {
        List<PartyMemoryUnitMappingEntry>? saved = null;
        DaprClient client = CreateWritableClient(
            [new PartyMemoryUnitMappingEntry("stale-unit", SourceUri, CaseId)],
            value => saved = [.. value]);
        var store = CreateStore(client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await store.RecordMappingAsync(
            TenantId,
            PartyId,
            MemoryUnitId,
            SourceUri,
            CaseId,
            CancellationToken.None);

        PartyMemoryUnitMappingEntry entry = saved.ShouldNotBeNull().ShouldHaveSingleItem();
        entry.ShouldBe(new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId));
    }

    [Fact]
    public async Task RecordMappingAsync_RetainsSameIdentityFromDifferentCase()
    {
        const string priorCaseId = "case-prior";
        var prior = new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, priorCaseId);
        List<PartyMemoryUnitMappingEntry>? saved = null;
        DaprClient client = CreateWritableClient([prior], value => saved = [.. value]);
        var store = CreateStore(client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await store.RecordMappingAsync(
            TenantId,
            PartyId,
            MemoryUnitId,
            SourceUri,
            CaseId,
            CancellationToken.None);

        saved.ShouldNotBeNull().Count.ShouldBe(2);
        saved.ShouldContain(prior);
        saved.ShouldContain(new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId));
    }

    [Fact]
    public async Task RecordMappingAsync_ConcurrencyConflictsLogTruthfulSanitizedTelemetryAndThrowSanitizedFailure()
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(([], "etag-1"));
        _ = client.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-record-conflict");
        logger.Records.Count.ShouldBe(5);
        for (int index = 0; index < logger.Records.Count; index++)
        {
            (LogLevel level, string message, Exception? loggedException) = logger.Records[index];
            level.ShouldBe(index < 4 ? LogLevel.Debug : LogLevel.Warning);
            AssertSanitizedLog(message, loggedException);
        }

        logger.Records[^1].Message.ShouldContain("5 attempts");
        logger.Records[^1].Message.ShouldNotContain("retrying");
    }

    [Fact]
    public async Task RecordMappingAsync_StateWriteFailureDoesNotExposeProviderException()
    {
        DaprClient client = CreateReadableClient();
        _ = client.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-record-failed");
        AssertSingleWarning(logger, nameof(InvalidOperationException));
    }

    [Fact]
    public async Task RecordMappingAsync_NonCallerCancellationIsBoundedAndSanitized()
    {
        DaprClient client = CreateReadableClient();
        _ = client.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-record-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = client.Received(1).TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions?>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordMappingAsync_CallerCancellationDuringStateWritePropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        DaprClient client = CreateReadableClient();
        _ = client.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<bool>(cancellation.Token);
            });
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, cancellation.Token));

        logger.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClearMappingsAsync_StateDeleteFailureDoesNotExposeProviderException()
    {
        DaprClient client = Substitute.For<DaprClient>();
        _ = client.DeleteStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.ClearMappingsAsync(TenantId, PartyId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-clear-failed");
        AssertSingleWarning(logger, nameof(InvalidOperationException));
    }

    [Fact]
    public async Task ClearMappingsAsync_NonCallerCancellationIsBoundedAndSanitized()
    {
        DaprClient client = Substitute.For<DaprClient>();
        _ = client.DeleteStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.ClearMappingsAsync(TenantId, PartyId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-clear-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = client.Received(1).DeleteStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<StateOptions?>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearMappingsAsync_CallerCancellationPropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        DaprClient client = Substitute.For<DaprClient>();
        _ = client.DeleteStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(cancellation.Token));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.ClearMappingsAsync(TenantId, PartyId, cancellation.Token));

        logger.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMappingsAsync_StateReadFailureDoesNotExposeProviderException()
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(List<PartyMemoryUnitMappingEntry> Value, string ETag)>(
                new InvalidOperationException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.GetMappingsAsync(TenantId, PartyId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-read-failed");
        AssertSingleWarning(logger, nameof(InvalidOperationException));
    }

    [Fact]
    public async Task GetMappingsAsync_NonCallerCancellationIsBoundedAndSanitized()
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(List<PartyMemoryUnitMappingEntry> Value, string ETag)>(
                new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.GetMappingsAsync(TenantId, PartyId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-read-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = client.Received(1).GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
            "statestore",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMappingsAsync_CallerCancellationPropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<(List<PartyMemoryUnitMappingEntry> Value, string ETag)>(cancellation.Token));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.GetMappingsAsync(TenantId, PartyId, cancellation.Token));

        logger.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceMappingsAsync_StateSaveFailureDoesNotExposeProviderException()
    {
        DaprClient client = Substitute.For<DaprClient>();
        _ = client.SaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.ReplaceMappingsAsync(TenantId, PartyId, SensitiveEntries(), CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-replace-failed");
        AssertSingleWarning(logger, nameof(InvalidOperationException));
    }

    [Fact]
    public async Task ReplaceMappingsAsync_NonCallerCancellationIsBoundedAndSanitized()
    {
        DaprClient client = Substitute.For<DaprClient>();
        _ = client.SaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.ReplaceMappingsAsync(TenantId, PartyId, SensitiveEntries(), CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-replace-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = client.Received(1).SaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<PartyMemoryUnitMappingEntry>>(),
            Arg.Any<StateOptions?>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceMappingsAsync_CallerCancellationPropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        DaprClient client = Substitute.For<DaprClient>();
        _ = client.SaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(cancellation.Token));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.ReplaceMappingsAsync(TenantId, PartyId, SensitiveEntries(), cancellation.Token));

        logger.Records.ShouldBeEmpty();
    }

    private static void AssertSanitizedFailure(InvalidOperationException exception, string expectedMessage)
    {
        exception.Message.ShouldBe(expectedMessage);
        exception.InnerException.ShouldBeNull();
        string exceptionText = exception.ToString();
        AssertContainsNoSensitiveValues(exceptionText);
        exceptionText.ShouldNotContain("Ada Lovelace");
    }

    private static void AssertSanitizedLog(string message, Exception? loggedException)
    {
        AssertContainsNoSensitiveValues(message);
        message.ShouldNotContain("Ada Lovelace");
        loggedException.ShouldBeNull();
    }

    private static void AssertSingleWarning(
        RecordingLogger<PartyMemoryUnitMappingStore> logger,
        string expectedExceptionType)
    {
        (LogLevel level, string message, Exception? loggedException) = logger.Records.ShouldHaveSingleItem();
        level.ShouldBe(LogLevel.Warning);
        message.ShouldContain(expectedExceptionType);
        AssertSanitizedLog(message, loggedException);
    }

    private static void AssertContainsNoSensitiveValues(string text)
    {
        foreach (string sensitiveValue in _sensitiveValues)
        {
            text.ShouldNotContain(sensitiveValue);
        }
    }

    private static DaprClient CreateReadableClient()
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(([], "etag-1"));
        return client;
    }

    private static DaprClient CreateWritableClient(
        IReadOnlyList<PartyMemoryUnitMappingEntry> current,
        Action<List<PartyMemoryUnitMappingEntry>> capture)
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(([.. current], "etag-1"));
        _ = client.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Do<List<PartyMemoryUnitMappingEntry>>(capture),
                "etag-1",
                Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        return client;
    }

    private static PartyMemoryUnitMappingStore CreateStore(
        DaprClient client,
        RecordingLogger<PartyMemoryUnitMappingStore> logger) =>
        new(
            client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            logger);

    private static IReadOnlyList<PartyMemoryUnitMappingEntry> SensitiveEntries() =>
        [new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId)];
}
