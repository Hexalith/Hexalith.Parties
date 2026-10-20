using System.Collections.Concurrent;
using System.Diagnostics;

using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Verifies compensation and retained inventories across cancelled indexing acknowledgements.</summary>
public class PartyMemoryIndexingCancellationTests
{
    /// <summary>Preserves serialized inventory and awaits the DELETE before propagating mapping cancellation.</summary>
    /// <param name="boundary">Cancellation before persistence, during its await, or after a lost write acknowledgement.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CancelledMappingAwaitsCompensationAndPreservesPersistedInventory(int boundary)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        var original = new OperationCanceledException("private-mapping-failure", caller.Token);
        var retained = new PartyMemoryUnitMappingEntry("retained-unit", "urn:retained", "retained-case");
        var state = new PartyMemoryMappingState(retained);
        var store = new PartyMemoryUnitMappingStore(state.Client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions()), NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var mappingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMapping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new IndexingCancellationMappingStore(
            async (tenantId, partyId, memoryUnitId, sourceUri, caseId, token) =>
            {
                if (boundary == 1)
                {
                    state.BeforeSave = async () =>
                    {
                        mappingStarted.TrySetResult();
                        await releaseMapping.Task.ConfigureAwait(false);
                        throw original;
                    };
                }
                else if (boundary == 2)
                {
                    state.BeforeSave = () =>
                    {
                        state.SetEntries([retained, new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId)]);
                        caller.Cancel();
                        return Task.FromException(original);
                    };
                }
                else
                {
                    caller.Cancel();
                }

                try
                {
                    await store.RecordMappingAsync(tenantId, partyId, memoryUnitId, sourceUri, caseId,
                        token).ConfigureAwait(false);
                }
                catch (OperationCanceledException error)
                {
                    original = error;
                    throw;
                }

                throw new InvalidOperationException("The controlled mapping boundary must cancel.");
            });
        var remote = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        remote["retained-unit"] = true;
        var deleteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? path = null;
        string? authorization = null;
        await using WebApplication app = CreateApp(async context =>
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                remote["new-unit"] = true;
                await context.Response.WriteAsJsonAsync(new { instanceId = "new-unit" }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (!HttpMethods.IsDelete(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            path = context.Request.Path;
            authorization = context.Request.Headers.Authorization;
            deleteStarted.TrySetResult();
            await releaseDelete.Task.WaitAsync(context.RequestAborted).ConfigureAwait(false);
            remote.TryRemove("new-unit", out _);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });
        await app.StartAsync(testToken);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single() + "/gateway/") };
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        var service = CreateService(http, observed, logger);
        try
        {
            Task<PartyMemoryIndexingResult?> indexing = IndexAsync(service, caller.Token);
            if (boundary == 1)
            {
                await mappingStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), testToken);
                indexing.IsCompleted.ShouldBeFalse();
                caller.Cancel();
                releaseMapping.TrySetResult();
            }

            await deleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), testToken);
            indexing.IsCompleted.ShouldBeFalse();
            remote.Keys.ShouldContain("new-unit");
            releaseDelete.TrySetResult();

            OperationCanceledException error = await ObserveCancellationAsync(indexing);

            error.ShouldBeSameAs(original);
            error.CancellationToken.ShouldBe(caller.Token);
            path.ShouldBe("/gateway/api/v1/tenants/tenant-secret/cases/case-at-ingest/memory-units/new-unit");
            authorization.ShouldBe("Bearer test-token-secret");
            remote.Keys.ShouldBe(["retained-unit"]);
            state.Entries.ShouldContain(retained);
            state.Entries.Count.ShouldBe(boundary == 2 ? 2 : 1);
            if (boundary == 2)
            {
                state.Entries.ShouldContain(new PartyMemoryUnitMappingEntry("new-unit",
                    "urn:hexalith:parties:tenant-secret:party:party-secret", "case-at-ingest"));
            }

            var record = logger.Records.ShouldHaveSingleItem();
            record.Level.ShouldBe(LogLevel.Warning);
            record.Message.ShouldContain("Compensating delete succeeded");
            AssertSanitized(logger);
        }
        finally
        {
            releaseMapping.TrySetResult();
            releaseDelete.TrySetResult();
            await app.StopAsync(testToken);
        }
    }

    /// <summary>Retains the original cancellation for accepted and rejected compensation responses.</summary>
    /// <param name="status">The cleanup response status.</param>
    /// <param name="body">The cleanup error envelope, if any.</param>
    /// <param name="accepted">Whether the existing response classifier accepts the request outcome.</param>
    [Theory]
    [InlineData(202, "", true)]
    [InlineData(404, "{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", true)]
    [InlineData(404, "", false)]
    [InlineData(404, "{\"code\":\"CASE_NOT_FOUND\"}", false)]
    [InlineData(503, "private-response", false)]
    public async Task CompensationResponseNeverReplacesOriginalCancellation(int status, string body, bool accepted)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        var original = new OperationCanceledException("private-mapping-failure", caller.Token);
        var store = CancelledStore(caller, original);
        int deletes = 0;
        await using WebApplication app = CreateApp(async context =>
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                await context.Response.WriteAsJsonAsync(new { instanceId = "new-unit" }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (!HttpMethods.IsDelete(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            Interlocked.Increment(ref deletes);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
        });
        await app.StartAsync(testToken);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single() + "/") };
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        try
        {
            OperationCanceledException error = await ObserveCancellationAsync(
                IndexAsync(CreateService(http, store, logger), caller.Token));

            error.ShouldBeSameAs(original);
            deletes.ShouldBe(1);
            var record = logger.Records.ShouldHaveSingleItem();
            record.Level.ShouldBe(accepted ? LogLevel.Warning : LogLevel.Error);
            record.Message.ShouldContain(accepted ? "Compensating delete succeeded" : "Operator reconciliation is required");
            AssertSanitized(logger);
        }
        finally
        {
            await app.StopAsync(testToken);
        }
    }

    /// <summary>Bounds stalled recovery independently of the already cancelled caller.</summary>
    /// <param name="responseBody">Whether to stall after partial response body delivery.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryDeadlinePreservesOriginalCancellationWhenDeletionStalls(bool responseBody)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        var original = new OperationCanceledException("private-mapping-failure", caller.Token);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using WebApplication app = CreateApp(async context =>
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                await context.Response.WriteAsJsonAsync(new { instanceId = "new-unit" }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (!HttpMethods.IsDelete(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            if (responseBody)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{", context.RequestAborted).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
            }

            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
        });
        await app.StartAsync(testToken);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single() + "/") };
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        try
        {
            Task<PartyMemoryIndexingResult?> indexing = IndexAsync(
                CreateService(http, CancelledStore(caller, original), logger), caller.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10), testToken);
            var elapsed = Stopwatch.StartNew();

            OperationCanceledException error = await ObserveCancellationAsync(
                indexing.WaitAsync(TimeSpan.FromSeconds(8), testToken));

            error.ShouldBeSameAs(original);
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(8));
            var record = logger.Records.ShouldHaveSingleItem();
            record.Level.ShouldBe(LogLevel.Error);
            record.Message.ShouldContain("Operator reconciliation is required");
            record.Message.ShouldNotContain("succeeded");
            AssertSanitized(logger);
        }
        finally
        {
            release.TrySetResult();
            await app.StopAsync(testToken);
        }
    }

    /// <summary>Reports unavailable compensation safely without replacing caller cancellation.</summary>
    /// <param name="missingEndpoint">Whether configuration is missing instead of the connection being aborted.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompensationUnavailablePreservesOriginalCancellationAndLogsSafeRecoveryFailure(bool missingEndpoint)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        var original = new OperationCanceledException("private-mapping-failure", caller.Token);
        int deletes = 0;
        await using WebApplication app = CreateApp(context =>
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                return context.Response.WriteAsJsonAsync(new { instanceId = "new-unit" }, context.RequestAborted);
            }

            if (HttpMethods.IsDelete(context.Request.Method))
            {
                Interlocked.Increment(ref deletes);
                context.Abort();
            }

            return Task.CompletedTask;
        });
        await app.StartAsync(testToken);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single() + "/") };
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        var unavailable = new PartyMemorySearchOptions
        {
            Endpoint = missingEndpoint ? null : http.BaseAddress,
        };
        try
        {
            OperationCanceledException error = await ObserveCancellationAsync(
                IndexAsync(CreateService(http, CancelledStore(caller, original), logger, unavailable), caller.Token));

            error.ShouldBeSameAs(original);
            deletes.ShouldBe(missingEndpoint ? 0 : 1);
            var record = logger.Records.ShouldHaveSingleItem();
            record.Level.ShouldBe(LogLevel.Error);
            record.Message.ShouldContain(missingEndpoint ? "skipped" : nameof(HttpRequestException));
            record.Message.ShouldContain("Operator reconciliation is required");
            AssertSanitized(logger);
        }
        finally
        {
            await app.StopAsync(testToken);
        }
    }

    /// <summary>Avoids compensation when ingestion was cancelled before acknowledging a unit identity.</summary>
    [Fact]
    public async Task CancellationDuringIngestionDoesNotAttemptDeletionWithoutAcknowledgedIdentity()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        int deletes = 0;
        await using WebApplication app = CreateApp(context =>
        {
            if (HttpMethods.IsDelete(context.Request.Method))
            {
                Interlocked.Increment(ref deletes);
            }

            caller.Cancel();
            return Task.CompletedTask;
        });
        await app.StartAsync(testToken);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single() + "/") };
        var store = new IndexingCancellationMappingStore((_, _, _, _, _, _) => Task.CompletedTask);
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() =>
                IndexAsync(CreateService(http, store, logger), caller.Token));

            deletes.ShouldBe(0);
            store.RecordCount.ShouldBe(0);
            logger.Records.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(testToken);
        }
    }

    private static WebApplication CreateApp(RequestDelegate handler)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.Run(handler);
        return app;
    }

    private static async Task<OperationCanceledException> ObserveCancellationAsync(Task indexing)
    {
        try
        {
            await indexing.ConfigureAwait(false);
        }
        catch (OperationCanceledException error)
        {
            return error;
        }

        throw new InvalidOperationException("Indexing must propagate cancellation after compensation.");
    }

    private static IPartyMemoryUnitMappingStore CancelledStore(CancellationTokenSource caller, Exception original)
    {
        return new IndexingCancellationMappingStore(
            (_, _, _, _, _, _) =>
            {
                caller.Cancel();
                return Task.FromException(original);
            });
    }

    private static PartyMemoryIndexingService CreateService(HttpClient http, IPartyMemoryUnitMappingStore store,
        RecordingLogger<PartyMemoryIndexingService> logger, PartyMemorySearchOptions? cleanupOptions = null)
    {
        var cache = new OptionsCache<PartyMemorySearchOptions>();
        cache.TryAdd(Options.DefaultName, cleanupOptions ?? new PartyMemorySearchOptions
        {
            Endpoint = http.BaseAddress,
            CaseId = "case-current-secret",
            ApiToken = "test-token-secret",
        });
        var monitor = new OptionsMonitor<PartyMemorySearchOptions>(
            new OptionsFactory<PartyMemorySearchOptions>([], []), [], cache);
        return new PartyMemoryIndexingService(new MemoriesClient(http,
            Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance), store, monitor, logger);
    }

    private static Task<PartyMemoryIndexingResult?> IndexAsync(PartyMemoryIndexingService service, CancellationToken token) =>
        service.IndexAsync(new PartyIndexEntry
        {
            Id = "party-secret",
            Type = Hexalith.Parties.Contracts.ValueObjects.PartyType.Person,
            DisplayName = "synthetic-private-name",
            IsActive = true,
        }, new PartyMemoryUnitMappingContext("tenant-secret", "case-at-ingest", "PartyCreated"), token);

    private static void AssertSanitized(RecordingLogger<PartyMemoryIndexingService> logger)
    {
        foreach ((_, string message, Exception? exception) in logger.Records)
        {
            exception.ShouldBeNull();
            foreach (string secret in new[] { "tenant-secret", "party-secret", "case-at-ingest", "new-unit",
                "test-token-secret", "private-mapping-failure", "private-response", "synthetic-private-name", "127.0.0.1" })
            {
                message.ShouldNotContain(secret);
            }
        }
    }
}
