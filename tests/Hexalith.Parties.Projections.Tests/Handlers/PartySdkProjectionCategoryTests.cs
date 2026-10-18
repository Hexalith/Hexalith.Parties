using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>Verifies handler category ownership through real logging factories and persisted output.</summary>
public sealed class PartySdkProjectionCategoryTests
{
    private const string ProcessingCategory = "Hexalith.Parties.Projections.Handlers.PartyProcessingActivityFold";
    private const string SdkCategory = "Hexalith.Parties.Projections.Handlers.PartySdkProjectionFold";
    private const string IndexCategory = "Hexalith.Parties.Projections.Handlers.PartyIndexSdkProjectionHandler";
    private const string PrivatePayload = "SyntheticPrivateCategoryPayload4938";
    private static readonly IOptions<PartySdkReadModelOptions> s_options =
        Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = "statestore" });

    /// <summary>Activates the actual SDK-scanned handlers and observes their dedicated fold categories.</summary>
    /// <param name="detail">Whether to exercise the aggregate-owned Detail handler.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SdkAssemblyScanningInjectsFoldLoggerFactoryAsync(bool detail)
    {
        var (store, batchStore, state) = CreateStore();
        using var sink = new ProjectionCategoryLoggerProvider();
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            Args = [],
        });
        builder.Configuration.Sources.Clear();
        builder.AddEventStoreDomainService(typeof(PartyDetailSdkProjectionHandler).Assembly);
        builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(sink);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(batchStore);
        builder.Services.AddSingleton(s_options);
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAsyncDomainProjectionHandler handler = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Single(handler => handler.ProjectionType == (detail ? PartyProjectionNames.Detail : PartyProjectionNames.Index));
        handler.GetType().ShouldBe(detail ? typeof(PartyDetailSdkProjectionHandler) : typeof(PartyIndexSdkProjectionHandler));

        (await handler.ProjectAsync(Request(), "sdk-activation", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        AssertDiagnostic(sink, detail, 4805);
        AssertPersisted(state, detail, unresolved: false);
    }

    /// <summary>Honors operator filters that enable only the responsible fold category.</summary>
    /// <param name="detail">Whether to exercise the Detail handler.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnablingOnlyAssignedFoldCategoryEmitsDiagnosticAndPreservesStateAsync(bool detail)
    {
        var (store, batchStore, state) = CreateStore();
        using var sink = new ProjectionCategoryLoggerProvider();
        string category = detail ? ProcessingCategory : SdkCategory;
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.None)
            .AddFilter(category, LogLevel.Warning)
            .AddProvider(sink));
        factory.CreateLogger<PartyDetailSdkProjectionHandler>().IsEnabled(LogLevel.Warning).ShouldBeFalse();
        factory.CreateLogger<PartyIndexSdkProjectionHandler>().IsEnabled(LogLevel.Warning).ShouldBeFalse();

        (await Handler(detail, store, batchStore, factory)
            .ProjectAsync(Request(), "fold-only", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        AssertDiagnostic(sink, detail, 4805);
        AssertPersisted(state, detail, unresolved: false);
    }

    /// <summary>Reports each existing drop path exactly once without changing checkpoint or audit semantics.</summary>
    /// <param name="detail">Whether to exercise the Detail handler.</param>
    /// <param name="eventId">The diagnostic's existing identifier.</param>
    [Theory]
    [InlineData(true, 4802)]
    [InlineData(false, 4802)]
    [InlineData(true, 4803)]
    [InlineData(false, 4803)]
    [InlineData(true, 4805)]
    [InlineData(false, 4805)]
    [InlineData(true, 4806)]
    [InlineData(false, 4806)]
    [InlineData(true, 4807)]
    [InlineData(false, 4807)]
    [InlineData(true, 4808)]
    [InlineData(false, 4808)]
    public async Task LiveAndUnresolvedAuditUseOnlyAssignedFoldCategoryAsync(bool detail, int eventId)
    {
        var (store, batchStore, state) = CreateStore();
        using var sink = new ProjectionCategoryLoggerProvider();
        using ILoggerFactory factory = Factory(sink);
        IAsyncDomainProjectionHandler handler = Handler(detail, store, batchStore, factory);
        ProjectionRequest request = Request(eventId);
        bool unresolved = eventId is 4802 or 4803 or 4808;

        DomainProjectionHandlerResult first = await handler.ProjectAsync(request, "first", TestContext.Current.CancellationToken);
        first.Status.ShouldBe(unresolved ? ProjectionDispatchStatus.Retryable : ProjectionDispatchStatus.Completed);
        first.ReasonCode.ShouldBe(unresolved ? PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason : null);
        AssertDiagnostic(sink, detail, eventId);
        AssertPersisted(state, detail, unresolved);
        string persisted = Snapshot(state);

        DomainProjectionHandlerResult repeated = await handler.ProjectAsync(request, "redelivery", TestContext.Current.CancellationToken);
        repeated.Status.ShouldBe(unresolved ? ProjectionDispatchStatus.Retryable : ProjectionDispatchStatus.AlreadyCompleted);
        Snapshot(state).ShouldBe(persisted);
        sink.Records.Count.ShouldBe(unresolved ? 2 : 1);
        sink.Records.All(record => record.Category == (detail ? ProcessingCategory : SdkCategory)).ShouldBeTrue();
    }

    /// <summary>Detail rebuilds preserve committed output with enabled, disabled, or absent fold logging.</summary>
    /// <param name="logging">Whether the fold logger is enabled, filtered out, or unavailable.</param>
    [Theory]
    [InlineData("enabled")]
    [InlineData("disabled")]
    [InlineData("missing")]
    public async Task DetailRebuildUsesProcessingCategoryOnceAndMatchesLiveStateAsync(string logging)
    {
        var (store, batchStore, state) = CreateStore();
        using var sink = new ProjectionCategoryLoggerProvider();
        using ILoggerFactory factory = Factory(sink, logging == "disabled" ? ProcessingCategory : null);
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options, logging == "missing" ? null : factory);
        DomainProjectionRebuildPlan plan = await handler.PrepareRebuildAsync(Request(), "rebuild", TestContext.Current.CancellationToken);
        (await batchStore.ExecuteAsync(new ReadModelBatch(
            new ReadModelBatchScope("statestore", "private-tenant", "party", "private-party", PartyProjectionNames.Detail, "rebuild"),
            plan.Operations), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        if (logging == "enabled")
        {
            AssertDiagnostic(sink, detail: true, 4805);
        }
        else
        {
            sink.Records.ShouldBeEmpty();
        }
        AssertPersisted(state, detail: true, unresolved: false);

        var (liveStore, liveBatchStore, liveState) = CreateStore();
        (await new PartyDetailSdkProjectionHandler(liveStore, liveBatchStore, s_options)
            .ProjectAsync(Request(), "live", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Snapshot(state).ShouldBe(Snapshot(liveState));
    }

    /// <summary>Optimistic retries re-fold safely while producing one diagnostic per delivery.</summary>
    /// <param name="detail">Whether to retry Detail's unresolved audit or Index's persistence callback.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OptimisticRetriesLogOnceAndPersistExpectedStateAsync(bool detail)
    {
        var (store, batchStore, state) = CreateStore(conflicts: 1);
        using var sink = new ProjectionCategoryLoggerProvider();
        using ILoggerFactory factory = Factory(sink);
        int eventId = detail ? 4803 : 4805;

        DomainProjectionHandlerResult result = await Handler(detail, store, batchStore, factory)
            .ProjectAsync(Request(eventId), "retry", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(detail ? ProjectionDispatchStatus.Retryable : ProjectionDispatchStatus.Completed);
        AssertDiagnostic(sink, detail, eventId);
        AssertPersisted(state, detail, unresolved: detail);
        store.ReceivedCalls().Count(static call => call.GetMethodInfo().Name == "TrySaveAsync").ShouldBe(2);
        var (baselineStore, baselineBatch, baselineState) = CreateStore();
        (await Handler(detail, baselineStore, baselineBatch, factory: null)
            .ProjectAsync(Request(eventId), "baseline", TestContext.Current.CancellationToken)).Status.ShouldBe(result.Status);
        Snapshot(state).ShouldBe(Snapshot(baselineState));
    }

    /// <summary>Filtering and missing factories omit fold sink calls while preserving persisted output and results.</summary>
    /// <param name="detail">Whether to exercise the Detail handler.</param>
    /// <param name="unresolved">Whether the delivery requires retry and an optional audit write.</param>
    /// <param name="missingFactory">Whether construction omits the factory instead of disabling its category.</param>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public async Task DisabledOrMissingFactoryPreservesResultsAndStateAsync(bool detail, bool unresolved, bool missingFactory)
    {
        var (enabledStore, enabledBatch, enabledState) = CreateStore();
        var (store, batchStore, state) = CreateStore();
        using var enabledSink = new ProjectionCategoryLoggerProvider();
        using ILoggerFactory enabledFactory = Factory(enabledSink);
        using var sink = new ProjectionCategoryLoggerProvider();
        using ILoggerFactory factory = Factory(sink, detail ? ProcessingCategory : SdkCategory);
        IAsyncDomainProjectionHandler handler = Handler(detail, store, batchStore,
            missingFactory ? null : factory, factory.CreateLogger<PartyIndexSdkProjectionHandler>());
        IAsyncDomainProjectionHandler enabled = Handler(detail, enabledStore, enabledBatch, enabledFactory);
        ProjectionRequest request = Request(unresolved ? 4803 : 4805);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            DomainProjectionHandlerResult expected = await enabled.ProjectAsync(request, $"enabled-{attempt}", TestContext.Current.CancellationToken);
            DomainProjectionHandlerResult actual = await handler.ProjectAsync(request, $"filtered-{attempt}", TestContext.Current.CancellationToken);
            actual.Status.ShouldBe(expected.Status);
            actual.ReasonCode.ShouldBe(expected.ReasonCode);
            Snapshot(state).ShouldBe(Snapshot(enabledState));
        }

        enabledSink.Records.Count.ShouldBe(unresolved ? 2 : 1);
        sink.Records.ShouldBeEmpty();
        AssertPersisted(state, detail, unresolved);
    }

    /// <summary>Shared rebuilds emit only bounded summaries in the retained handler category.</summary>
    /// <param name="unresolved">Whether accumulation fails on unresolved history.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedRebuildRetainsBoundedHandlerSummaryCategoryAsync(bool unresolved)
    {
        var (store, batchStore, state) = CreateStore();
        using var sink = new ProjectionCategoryLoggerProvider();
        using ILoggerFactory factory = Factory(sink);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options,
            logger: factory.CreateLogger<PartyIndexSdkProjectionHandler>(), loggerFactory: factory);
        var identity = new DomainSharedProjectionRebuildIdentity("private-tenant", "party", PartyProjectionNames.Index, "rebuild", "catalog");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        if (unresolved)
        {
            InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
                handler.AccumulateAsync(identity, candidate, Request(4803), TestContext.Current.CancellationToken));
            exception.Message.ShouldBe(PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason);
            state.ShouldBeEmpty();
        }
        else
        {
            ProjectionRequest history = Request() with
            {
                Events = [Request().Events[0], .. Enumerable.Range(2, 100).Select(sequence => Request().Events[1] with { SequenceNumber = sequence })],
            };
            candidate = await handler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
            candidate = await handler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
            sink.Records.ShouldBeEmpty();
            DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);
            _ = await batchStore.ExecuteAsync(new ReadModelBatch(
                new ReadModelBatchScope("statestore", identity.TenantId, "party", "private-party", PartyProjectionNames.Index, "rebuild"),
                plan.Operations), TestContext.Current.CancellationToken);
            sink.Records.ShouldBeEmpty();
            (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
            Read<PartyIndexSdkReadModel>(state, PartySdkReadModelAddresses.Index(identity.TenantId))!
                .LastSequenceNumbers["private-party"].ShouldBe(101);
        }

        ProjectionCategoryLogRecord summary = sink.Records.ShouldHaveSingleItem();
        summary.Category.ShouldBe(IndexCategory);
        summary.EventId.Id.ShouldBe(4801);
        summary.Level.ShouldBe(LogLevel.Warning);
        summary.Fields["Completed"].ShouldBe(!unresolved);
        summary.Fields[unresolved ? "UnknownType" : "CorruptPayload"].ShouldBe(unresolved ? 1L : 100L);
        AssertPrivacy(summary);
    }

    private static ILoggerFactory Factory(ProjectionCategoryLoggerProvider sink, string? disabledCategory = null)
        => LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace).AddProvider(sink);
            if (disabledCategory is not null)
            {
                builder.AddFilter(disabledCategory, LogLevel.None);
            }
        });

    private static IAsyncDomainProjectionHandler Handler(bool detail, IReadModelStore store, IReadModelBatchStore batchStore,
        ILoggerFactory? factory, ILogger<PartyIndexSdkProjectionHandler>? summaryLogger = null)
        => detail
            ? new PartyDetailSdkProjectionHandler(store, batchStore, s_options, factory)
            : new PartyIndexSdkProjectionHandler(store, s_options,
                logger: summaryLogger ?? factory?.CreateLogger<PartyIndexSdkProjectionHandler>(), loggerFactory: factory);

    private static ProjectionRequest Request(int eventId = 4805)
        => new("private-tenant", "party", "private-party",
        [
            new ProjectionEventDto(nameof(PartyCreated),
                JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person }, PartiesJsonOptions.Default),
                "json", 1, DateTimeOffset.UnixEpoch, "private-correlation"),
            new ProjectionEventDto(eventId == 4803 ? "UnknownCategoryTestEvent" : nameof(PartyCreated),
                eventId is 4802 or 4803 or 4807 ? "{}"u8.ToArray()
                    : eventId == 4808 ? "null"u8.ToArray()
                    : System.Text.Encoding.UTF8.GetBytes($"{{ invalid {PrivatePayload}"),
                eventId == 4802 ? "protobuf" : eventId is 4806 or 4807 ? "json-redacted" : "json",
                2, DateTimeOffset.UnixEpoch.AddSeconds(1), "private-correlation"),
        ]);

    private static void AssertDiagnostic(ProjectionCategoryLoggerProvider sink, bool detail, int eventId)
    {
        ProjectionCategoryLogRecord record = sink.Records.ShouldHaveSingleItem();
        record.Category.ShouldBe(detail ? ProcessingCategory : SdkCategory);
        record.EventId.Id.ShouldBe(eventId);
        record.Level.ShouldBe(eventId is 4806 or 4807 ? LogLevel.Information : LogLevel.Warning);
        record.Message.ShouldNotBeNullOrWhiteSpace();
        if (eventId is 4805 or 4806)
        {
            record.Fields["ExceptionType"].ShouldBe(nameof(JsonException));
        }

        AssertPrivacy(record);
    }

    private static void AssertPrivacy(ProjectionCategoryLogRecord record)
    {
        record.Exception.ShouldBeNull();
        string rendered = record.Message + JsonSerializer.Serialize(record.Fields);
        foreach (string value in new[] { PrivatePayload, "private-tenant", "private-party", "private-correlation" })
        {
            rendered.ShouldNotContain(value);
        }
    }

    private static void AssertPersisted(Dictionary<string, byte[]> state, bool detail, bool unresolved)
    {
        if (detail)
        {
            PartyDetailSdkReadModel model = Read<PartyDetailSdkReadModel>(state,
                PartySdkReadModelAddresses.Detail("private-tenant", "private-party"))!;
            PartyProcessingSdkReadModel processing = Read<PartyProcessingSdkReadModel>(state,
                PartySdkReadModelAddresses.Processing("private-tenant", "private-party"))!;
            model.LastSequenceNumber.ShouldBe(unresolved ? long.MinValue : 2);
            processing.LastSequenceNumber.ShouldBe(unresolved ? 1 : 2);
            processing.Records.Count.ShouldBe(2);
            processing.Records[0].SequenceNumber.ShouldBe(1);
            processing.Records[1].SequenceNumber.ShouldBe(2);
            if (unresolved)
            {
                model.Detail.ShouldBeNull();
                processing.Records[1].Outcome.ShouldBe("Failed");
            }
            else
            {
                model.Detail.ShouldNotBeNull().Id.ShouldBe("private-party");
            }
        }
        else
        {
            PartyIndexSdkReadModel? model = Read<PartyIndexSdkReadModel>(state, PartySdkReadModelAddresses.Index("private-tenant"));
            if (unresolved)
            {
                model.ShouldBeNull();
                state.ShouldBeEmpty();
            }
            else
            {
                model.ShouldNotBeNull().LastSequenceNumbers["private-party"].ShouldBe(2);
                model.Entries.ShouldHaveSingleItem().Key.ShouldBe("private-party");
            }
        }
    }

    private static TValue? Read<TValue>(Dictionary<string, byte[]> state, string key) where TValue : class
        => state.TryGetValue(key, out byte[]? bytes) ? JsonSerializer.Deserialize<TValue>(bytes, PartiesJsonOptions.Default) : null;

    private static string Snapshot(Dictionary<string, byte[]> state)
        => JsonSerializer.Serialize(state.OrderBy(static item => item.Key, StringComparer.Ordinal));

    private static (IReadModelStore Store, IReadModelBatchStore BatchStore, Dictionary<string, byte[]> State) CreateStore(int conflicts = 0)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        var state = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var etags = new Dictionary<string, string>(StringComparer.Ordinal);
        int version = 0;
        Configure<PartyDetailSdkReadModel>();
        Configure<PartyProcessingSdkReadModel>();
        Configure<PartyIndexSdkReadModel>();
        batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            foreach (ReadModelBatchOperation operation in call.Arg<ReadModelBatch>().Operations)
            {
                state[operation.Key] = operation.CanonicalValue.ToArray();
                etags[operation.Key] = (++version).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return ReadModelBatchResult.Completed("category-test");
        });
        return (store, batchStore, state);

        void Configure<TValue>() where TValue : class
        {
            store.GetAsync<TValue>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                string key = call.ArgAt<string>(1);
                return new ReadModelEntry<TValue>(Read<TValue>(state, key), etags.GetValueOrDefault(key));
            });
            store.TrySaveAsync("statestore", Arg.Any<string>(), Arg.Any<TValue>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    string key = call.ArgAt<string>(1);
                    if (conflicts > 0)
                    {
                        conflicts--;
                        return false;
                    }

                    if (call.ArgAt<string>(3) != (etags.GetValueOrDefault(key) ?? string.Empty))
                    {
                        return false;
                    }

                    state[key] = JsonSerializer.SerializeToUtf8Bytes(call.Arg<TValue>(), PartiesJsonOptions.Default);
                    etags[key] = (++version).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                });
        }
    }
}
