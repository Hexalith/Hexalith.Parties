using Hexalith.EventStore.Client.Projections;

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

    [Theory]
    [InlineData(null)]
    [InlineData("case-secret")]
    public async Task RecordMappingAsync_PreservesLegacyDistinctUnitWithSameSource(string? legacyCase)
    {
        var legacy = new PartyMemoryUnitMappingEntry("legacy-unit", SourceUri, legacyCase);
        var state = new PartyMemoryMappingState(legacy);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, TestContext.Current.CancellationToken);

        state.Entries.ShouldBe([legacy, new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId)]);
        (await CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>())
            .GetMappingsAsync(TenantId, PartyId, TestContext.Current.CancellationToken)).ShouldBe(state.Entries);
    }

    [Theory]
    [InlineData(false, "case-secret")]
    [InlineData(true, "case-secret")]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(true, " \t")]
    public async Task RecordMappingAsync_PreservesDistinctUnitWithMatchingSource(bool sourceFirst, string? existingCase)
    {
        var unitMatch = new PartyMemoryUnitMappingEntry(MemoryUnitId, "urn:original", existingCase);
        var sourceMatch = new PartyMemoryUnitMappingEntry("unit-retained", SourceUri, CaseId);
        var incoming = new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId);
        var state = new PartyMemoryMappingState(sourceFirst ? [sourceMatch, unitMatch] : [unitMatch, sourceMatch]);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        CancellationToken token = TestContext.Current.CancellationToken;

        await store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, token);

        state.Entries.ShouldBe(sourceFirst ? [sourceMatch, incoming] : [incoming, sourceMatch]);
        state.SaveAttempts.ShouldBe(1);
        IReadOnlyList<PartyMemoryUnitMappingEntry> readBack = await CreateStore(
            state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>()).GetMappingsAsync(TenantId, PartyId, token);
        readBack.ShouldBe(state.Entries);

        // An exact redelivery must preserve both units without another write.
        await store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, token);

        state.Entries.ShouldBe(readBack);
        state.SaveAttempts.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("case-secret")]
    public async Task RecordMappingAsync_ReevaluatesUnitMatchAfterConcurrentSourceMatchArrives(string? existingCase)
    {
        var original = new PartyMemoryUnitMappingEntry(MemoryUnitId, "urn:original", existingCase);
        var sourceMatch = new PartyMemoryUnitMappingEntry("unit-retained", SourceUri, CaseId);
        var state = new PartyMemoryMappingState(original);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        var concurrentStore = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        CancellationToken token = TestContext.Current.CancellationToken;
        state.BeforeSave = () => concurrentStore.RecordMappingAsync(
            TenantId, PartyId, sourceMatch.MemoryUnitId, sourceMatch.SourceUri, CaseId, token);

        await store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, token);

        state.Entries.ShouldBe([new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId), sourceMatch]);
        state.SaveAttempts.ShouldBe(3);
    }

    [Fact]
    public async Task RecordMappingAsync_UnitMatchAddedDuringRetryPreventsSourceReplacement()
    {
        var sourceMatch = new PartyMemoryUnitMappingEntry("unit-retained", SourceUri, CaseId);
        var state = new PartyMemoryMappingState(sourceMatch);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        var concurrentStore = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        CancellationToken token = TestContext.Current.CancellationToken;
        state.BeforeSave = () => concurrentStore.RecordMappingAsync(
            TenantId, PartyId, MemoryUnitId, "urn:original", CaseId, token);

        await store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, token);

        state.Entries.ShouldBe([sourceMatch, new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId)]);
        state.SaveAttempts.ShouldBe(3);
    }

    [Fact]
    public async Task RecordMappingAsync_UnitMatchRemovedDuringRetryPreservesDistinctSourceUnit()
    {
        var unitMatch = new PartyMemoryUnitMappingEntry(MemoryUnitId, "urn:original", CaseId);
        var sourceMatch = new PartyMemoryUnitMappingEntry("unit-retained", SourceUri, CaseId);
        var state = new PartyMemoryMappingState(unitMatch, sourceMatch);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        var cleanupStore = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        CancellationToken token = TestContext.Current.CancellationToken;
        state.BeforeSave = async () =>
        {
            _ = await cleanupStore.RemoveMappingsAsync(TenantId, PartyId, [unitMatch], token).ConfigureAwait(false);
        };

        await store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, token);

        state.Entries.ShouldBe([sourceMatch, new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId)]);
        state.SaveAttempts.ShouldBe(3);
        state.DeleteAttempts.ShouldBe(0);
    }

    [Fact]
    public async Task RecordMappingAsync_PreservesEveryDistinctUnitWithMatchingSource()
    {
        var unitMatch = new PartyMemoryUnitMappingEntry(MemoryUnitId, "urn:original", CaseId);
        var sourceMatch = new PartyMemoryUnitMappingEntry("unit-retained", SourceUri, CaseId);
        var state = new PartyMemoryMappingState(unitMatch, sourceMatch);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());
        CancellationToken token = TestContext.Current.CancellationToken;
        await store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, token);
        state.Entries.Count.ShouldBe(2);
        var freshStore = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await freshStore.RecordMappingAsync(TenantId, PartyId, "unit-new", SourceUri, CaseId, token);

        IReadOnlyList<PartyMemoryUnitMappingEntry> readBack = await CreateStore(
            state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>()).GetMappingsAsync(TenantId, PartyId, token);
        readBack.ShouldBe([new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId), sourceMatch, new PartyMemoryUnitMappingEntry("unit-new", SourceUri, CaseId)]);
        state.Entries.ShouldBe(readBack);
    }

    [Fact]
    public async Task RecordMappingAsync_DeduplicatesMatchingUnitWhileRetainingOtherUnitsAndCases()
    {
        var legacy = new PartyMemoryUnitMappingEntry(MemoryUnitId, "urn:legacy");
        var duplicate = new PartyMemoryUnitMappingEntry(MemoryUnitId, "urn:original", CaseId);
        var sourceMatch = new PartyMemoryUnitMappingEntry("unit-retained", SourceUri, CaseId);
        var otherCase = new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, "case-other");
        var unrelated = new PartyMemoryUnitMappingEntry("unit-other", "urn:other", CaseId);
        var state = new PartyMemoryMappingState(legacy, sourceMatch, duplicate, otherCase, unrelated);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await store.RecordMappingAsync(
            TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, TestContext.Current.CancellationToken);

        state.Entries.ShouldBe([
            new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId), sourceMatch, otherCase, unrelated,
        ]);
    }

    [Fact]
    public async Task RecordMappingAsync_UnitMatchInAnotherCasePreservesDistinctSourceUnit()
    {
        var otherCase = new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, "case-other");
        var stale = new PartyMemoryUnitMappingEntry("unit-stale", SourceUri, CaseId);
        var state = new PartyMemoryMappingState(otherCase, stale);
        var store = CreateStore(state.Client, new RecordingLogger<PartyMemoryUnitMappingStore>());

        await store.RecordMappingAsync(
            TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, TestContext.Current.CancellationToken);

        state.Entries.ShouldBe([otherCase, stale, new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId)]);
    }

    [Fact]
    public async Task RecordMappingAsync_PersistsSuppliedCaseIdInActualStatePayload()
    {
        List<PartyMemoryUnitMappingEntry>? saved = null;
        IReadModelStore client = CreateWritableClient([], value => saved = [.. value]);
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
        IReadModelStore client = CreateWritableClient(
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
    public async Task RecordMappingAsync_PreservesPriorIdentityWithinSameCase()
    {
        List<PartyMemoryUnitMappingEntry>? saved = null;
        IReadModelStore client = CreateWritableClient(
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

        saved.ShouldNotBeNull().ShouldBe([
            new PartyMemoryUnitMappingEntry("stale-unit", SourceUri, CaseId),
            new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId),
        ]);
    }

    [Fact]
    public async Task RecordMappingAsync_RetainsSameIdentityFromDifferentCase()
    {
        const string priorCaseId = "case-prior";
        var prior = new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, priorCaseId);
        List<PartyMemoryUnitMappingEntry>? saved = null;
        IReadModelStore client = CreateWritableClient([prior], value => saved = [.. value]);
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
        IReadModelStore client = Substitute.For<IReadModelStore, IReadModelConditionalEraser>();
        client.GetAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<List<PartyMemoryUnitMappingEntry>>([], "etag-1"));
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
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
        IReadModelStore client = CreateReadableClient();
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
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
        IReadModelStore client = CreateReadableClient();
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.RecordMappingAsync(TenantId, PartyId, MemoryUnitId, SourceUri, CaseId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-record-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = client.Received(1).TrySaveAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordMappingAsync_CallerCancellationDuringStateWritePropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        IReadModelStore client = CreateReadableClient();
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
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
        IReadModelStore client = CreateRemovalClient(includeRetained: false);
        _ = ((IReadModelConditionalEraser)client).TryEraseAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.ClearMappingsAsync("_health-probe", "_health-probe-00000000000000000000000000000001", CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-clear-failed");
        AssertSingleWarning(logger, nameof(InvalidOperationException));
    }

    [Fact]
    public async Task ClearMappingsAsync_NonCallerCancellationIsBoundedAndSanitized()
    {
        IReadModelStore client = CreateRemovalClient(includeRetained: false);
        _ = ((IReadModelConditionalEraser)client).TryEraseAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.ClearMappingsAsync("_health-probe", "_health-probe-00000000000000000000000000000001", CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-clear-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = ((IReadModelConditionalEraser)client).Received(1).TryEraseAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearMappingsAsync_CallerCancellationPropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        IReadModelStore client = CreateRemovalClient(includeRetained: false);
        _ = ((IReadModelConditionalEraser)client).TryEraseAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<bool>(cancellation.Token));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.ClearMappingsAsync("_health-probe", "_health-probe-00000000000000000000000000000001", cancellation.Token));

        logger.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMappingsAsync_StateReadFailureDoesNotExposeProviderException()
    {
        IReadModelStore client = Substitute.For<IReadModelStore, IReadModelConditionalEraser>();
        client.GetAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<List<PartyMemoryUnitMappingEntry>>>(
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
        IReadModelStore client = Substitute.For<IReadModelStore, IReadModelConditionalEraser>();
        client.GetAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<List<PartyMemoryUnitMappingEntry>>>(
                new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.GetMappingsAsync(TenantId, PartyId, CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-read-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = client.Received(1).GetAsync<List<PartyMemoryUnitMappingEntry>>(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMappingsAsync_CallerCancellationPropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        IReadModelStore client = Substitute.For<IReadModelStore, IReadModelConditionalEraser>();
        client.GetAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<ReadModelEntry<List<PartyMemoryUnitMappingEntry>>>(cancellation.Token));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.GetMappingsAsync(TenantId, PartyId, cancellation.Token));

        logger.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveMappingsAsync_StateSaveFailureDoesNotExposeProviderException()
    {
        IReadModelStore client = CreateRemovalClient(includeRetained: true);
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.RemoveMappingsAsync(TenantId, PartyId, SensitiveEntries(), CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-remove-failed");
        AssertSingleWarning(logger, nameof(InvalidOperationException));
    }

    [Fact]
    public async Task RemoveMappingsAsync_NonCallerCancellationIsBoundedAndSanitized()
    {
        IReadModelStore client = CreateRemovalClient(includeRetained: true);
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new OperationCanceledException(ProviderFailure)));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.RemoveMappingsAsync(TenantId, PartyId, SensitiveEntries(), CancellationToken.None));

        AssertSanitizedFailure(exception, "memory-unit-mapping-remove-failed");
        AssertSingleWarning(logger, nameof(OperationCanceledException));
        _ = client.Received(1).TrySaveAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMappingsAsync_CallerCancellationPropagatesWithoutLogging()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        IReadModelStore client = CreateRemovalClient(includeRetained: true);
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<bool>(cancellation.Token));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = CreateStore(client, logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.RemoveMappingsAsync(TenantId, PartyId, SensitiveEntries(), cancellation.Token));

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

    private static IReadModelStore CreateRemovalClient(bool includeRetained)
    {
        IReadModelStore client = Substitute.For<IReadModelStore, IReadModelConditionalEraser>();
        List<PartyMemoryUnitMappingEntry> entries = [.. SensitiveEntries()];
        if (includeRetained)
        {
            entries.Add(new PartyMemoryUnitMappingEntry("retained-unit", "urn:retained", "retained-case"));
        }

        client.GetAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<List<PartyMemoryUnitMappingEntry>>(entries, "etag-1"));
        return client;
    }

    private static IReadModelStore CreateReadableClient()
    {
        IReadModelStore client = Substitute.For<IReadModelStore, IReadModelConditionalEraser>();
        client.GetAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<List<PartyMemoryUnitMappingEntry>>([], "etag-1"));
        return client;
    }

    private static IReadModelStore CreateWritableClient(
        IReadOnlyList<PartyMemoryUnitMappingEntry> current,
        Action<List<PartyMemoryUnitMappingEntry>> capture)
    {
        IReadModelStore client = Substitute.For<IReadModelStore, IReadModelConditionalEraser>();
        client.GetAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<List<PartyMemoryUnitMappingEntry>>([.. current], "etag-1"));
        _ = client.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Do<List<PartyMemoryUnitMappingEntry>>(capture),
                "etag-1",
                Arg.Any<CancellationToken>())
            .Returns(true);
        return client;
    }

    private static PartyMemoryUnitMappingStore CreateStore(
        IReadModelStore client,
        RecordingLogger<PartyMemoryUnitMappingStore> logger) =>
        new(
            client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            logger);

    private static IReadOnlyList<PartyMemoryUnitMappingEntry> SensitiveEntries() =>
        [new PartyMemoryUnitMappingEntry(MemoryUnitId, SourceUri, CaseId)];
}
