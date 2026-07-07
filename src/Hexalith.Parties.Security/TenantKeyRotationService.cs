using System.Diagnostics;
using System.Diagnostics.Metrics;

using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class TenantKeyRotationService(
    IKeyStorageBackend backend,
    DaprClient daprClient,
    IKeyOperationAuditService auditService,
    IEnumerable<ITenantKeyRotationCacheInvalidator> cacheInvalidators,
    ICorrelationContextAccessor correlationContextAccessor) : ITenantKeyRotationService
{
    private const string StoreName = "statestore";
    private const int MaxRetries = 5;

    internal static readonly Meter s_meter = PartyKeyManagementService.s_meter;
    internal static readonly Counter<long> s_rotationsStarted = s_meter.CreateCounter<long>("parties.tenant_keys.rotations_started", description: "Number of tenant key rotations started");
    internal static readonly Counter<long> s_rotationsCompleted = s_meter.CreateCounter<long>("parties.tenant_keys.rotations_completed", description: "Number of tenant key rotations completed");
    internal static readonly Counter<long> s_rotationsFailed = s_meter.CreateCounter<long>("parties.tenant_keys.rotations_failed", description: "Number of tenant key rotations failed");
    internal static readonly Counter<long> s_partyKeysSkipped = s_meter.CreateCounter<long>("parties.tenant_keys.party_keys_skipped", description: "Number of party key wrappers skipped during tenant key rotation");
    internal static readonly Histogram<double> s_backendLatency = s_meter.CreateHistogram<double>("parties.tenant_keys.backend_latency_ms", "ms", "Tenant key rotation backend operation latency");

    public async Task<TenantKeyRotationStatus> RotateAsync(TenantKeyRotationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);

        string correlationId = FirstNonBlank(request.CorrelationId, correlationContextAccessor.CorrelationId)
            ?? UniqueIdHelper.GenerateSortableUniqueStringId();
        long startTicks = Stopwatch.GetTimestamp();

        try
        {
            TenantKeyRotationProgress? progress = await GetProgressAsync(request.TenantId, request.OperationId, cancellationToken).ConfigureAwait(false);
            if (progress?.Status.Phase == TenantKeyRotationPhase.Completed)
            {
                return progress.Status;
            }

            TenantKeyMetadata targetTenantKey;
            try
            {
                targetTenantKey = progress?.TargetTenantKey
                    ?? await backend.GetOrCreateTenantKeyAsync(request.TenantId, request.OperationId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TenantKeyRotationStatus status = CreateFailureStatus(
                    request,
                    correlationId,
                    NormalizeFailureCategory(ex, tenantKeyProviderStage: true),
                    totalCount: 0,
                    processedCount: 0,
                    skippedCount: 0,
                    failedCount: 1);
                await SaveProgressAsync(new TenantKeyRotationProgress
                {
                    Status = status,
                    TargetTenantKey = null,
                    CompletedPartyKeyRecords = [],
                }, cancellationToken).ConfigureAwait(false);
                await RecordAuditAsync(status, keyVersion: 0, cancellationToken).ConfigureAwait(false);
                return status;
            }

            IReadOnlyList<PartyKeyRecord> records = await backend.ListPartyKeyRecordsAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
            HashSet<string> completedKeys = progress?.CompletedPartyKeyRecords.ToHashSet(StringComparer.Ordinal) ?? [];
            TenantKeyRotationStatus current = progress?.Status is null
                ? CreateStartedStatus(request, correlationId, records.Count)
                : progress.Status with
                {
                    Phase = TenantKeyRotationPhase.InProgress,
                    TotalCount = records.Count,
                    ProcessedCount = progress.Status.ProcessedCount,
                    FailedCount = 0,
                    FailureCategories = new Dictionary<TenantKeyRotationFailureCategory, int>(),
                    CompletedAt = null,
                    CorrelationId = correlationId,
                };

            s_rotationsStarted.Add(1, new KeyValuePair<string, object?>("tenant", request.TenantId));
            await SaveProgressAsync(new TenantKeyRotationProgress
            {
                Status = current,
                TargetTenantKey = targetTenantKey,
                CompletedPartyKeyRecords = completedKeys.Order(StringComparer.Ordinal).ToList(),
            }, cancellationToken).ConfigureAwait(false);

            foreach (PartyKeyRecord record in records)
            {
                string recordKey = BuildRecordKey(record);
                if (completedKeys.Contains(recordKey))
                {
                    continue;
                }

                try
                {
                    PartyKeyWrappingMetadata? existing = await backend
                        .GetPartyKeyWrappingMetadataAsync(record.TenantId, record.PartyId, record.Version, cancellationToken)
                        .ConfigureAwait(false);

                    if (existing?.TenantKeyVersion != targetTenantKey.Version)
                    {
                        await backend.SetPartyKeyWrappingMetadataAsync(
                            new PartyKeyWrappingMetadata
                            {
                                TenantId = record.TenantId,
                                PartyId = record.PartyId,
                                KeyVersion = record.Version,
                                TenantKeyId = targetTenantKey.KeyId,
                                TenantKeyVersion = targetTenantKey.Version,
                                RotationId = request.OperationId,
                                WrappedAt = DateTimeOffset.UtcNow,
                            },
                            cancellationToken).ConfigureAwait(false);

                        foreach (ITenantKeyRotationCacheInvalidator invalidator in cacheInvalidators)
                        {
                            await invalidator.InvalidatePartyAsync(record.TenantId, record.PartyId, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    completedKeys.Add(recordKey);
                    current = current with { ProcessedCount = current.ProcessedCount + 1 };
                    await SaveProgressAsync(new TenantKeyRotationProgress
                    {
                        Status = current,
                        TargetTenantKey = targetTenantKey,
                        CompletedPartyKeyRecords = completedKeys.Order(StringComparer.Ordinal).ToList(),
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (PartyEncryptionKeyDestroyedException)
                {
                    completedKeys.Add(recordKey);
                    current = current with { SkippedCount = current.SkippedCount + 1 };
                    s_partyKeysSkipped.Add(1, new KeyValuePair<string, object?>("tenant", request.TenantId), new KeyValuePair<string, object?>("category", "erased-party"));
                    await SaveProgressAsync(new TenantKeyRotationProgress
                    {
                        Status = current,
                        TargetTenantKey = targetTenantKey,
                        CompletedPartyKeyRecords = completedKeys.Order(StringComparer.Ordinal).ToList(),
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    TenantKeyRotationFailureCategory category = NormalizeFailureCategory(ex, tenantKeyProviderStage: false);
                    current = current with
                    {
                        Phase = TenantKeyRotationPhase.Failed,
                        FailedCount = current.FailedCount + 1,
                        FailureCategories = IncrementFailure(current.FailureCategories, category),
                        CompletedAt = DateTimeOffset.UtcNow,
                    };
                    await SaveProgressAsync(new TenantKeyRotationProgress
                    {
                        Status = current,
                        TargetTenantKey = targetTenantKey,
                        CompletedPartyKeyRecords = completedKeys.Order(StringComparer.Ordinal).ToList(),
                    }, cancellationToken).ConfigureAwait(false);
                    s_rotationsFailed.Add(1, new KeyValuePair<string, object?>("tenant", request.TenantId), new KeyValuePair<string, object?>("category", category.ToString()));
                    await RecordAuditAsync(current, targetTenantKey.Version, cancellationToken).ConfigureAwait(false);
                    return current;
                }
            }

            TenantKeyRotationStatus completed = current with
            {
                Phase = TenantKeyRotationPhase.Completed,
                FailedCount = 0,
                FailureCategories = new Dictionary<TenantKeyRotationFailureCategory, int>(),
                CompletedAt = DateTimeOffset.UtcNow,
            };
            await SaveProgressAsync(new TenantKeyRotationProgress
            {
                Status = completed,
                TargetTenantKey = targetTenantKey,
                CompletedPartyKeyRecords = completedKeys.Order(StringComparer.Ordinal).ToList(),
            }, cancellationToken).ConfigureAwait(false);
            s_rotationsCompleted.Add(1, new KeyValuePair<string, object?>("tenant", request.TenantId));
            await RecordAuditAsync(completed, targetTenantKey.Version, cancellationToken).ConfigureAwait(false);
            return completed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Without this catch, exceptions from progress reads, the initial/final SaveProgressAsync,
            // or the completion path bypass status persistence, audit, and metrics — leaving operators
            // unable to detect that the rotation actually failed via GetStatusAsync.
            TenantKeyRotationFailureCategory category = NormalizeFailureCategory(ex, tenantKeyProviderStage: false);
            TenantKeyRotationStatus status = CreateFailureStatus(
                request,
                correlationId,
                category,
                totalCount: 0,
                processedCount: 0,
                skippedCount: 0,
                failedCount: 1);
            s_rotationsFailed.Add(
                1,
                new KeyValuePair<string, object?>("tenant", request.TenantId),
                new KeyValuePair<string, object?>("category", category.ToString()));
            try
            {
                await RecordAuditAsync(status, keyVersion: 0, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Surface the original rotation failure even when the audit sink is unreachable.
            }

            return status;
        }
        finally
        {
            s_backendLatency.Record(
                Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", "tenant-rotate"));
        }
    }

    public async Task<TenantKeyRotationStatus?> GetStatusAsync(string tenantId, string operationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        TenantKeyRotationProgress? progress = await GetProgressAsync(tenantId, operationId, cancellationToken).ConfigureAwait(false);
        return progress?.Status;
    }

    private async Task<TenantKeyRotationProgress?> GetProgressAsync(string tenantId, string operationId, CancellationToken cancellationToken)
    {
        (TenantKeyRotationProgress? progress, _) = await daprClient
            .GetStateAndETagAsync<TenantKeyRotationProgress>(StoreName, BuildProgressKey(tenantId, operationId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return progress;
    }

    private async Task SaveProgressAsync(TenantKeyRotationProgress progress, CancellationToken cancellationToken)
    {
        string stateKey = BuildProgressKey(progress.Status.TenantId, progress.Status.OperationId);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            (_, string etag) = await daprClient
                .GetStateAndETagAsync<TenantKeyRotationProgress>(StoreName, stateKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            bool saved = await daprClient.TrySaveStateAsync(
                StoreName,
                stateKey,
                progress,
                etag,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (saved)
            {
                return;
            }
        }

        throw new TenantKeyRotationProgressConflictException(
            "Failed to save tenant key rotation progress after exhausting concurrent-write retries.");
    }

    private async Task RecordAuditAsync(TenantKeyRotationStatus status, int keyVersion, CancellationToken cancellationToken)
    {
        await auditService.RecordOperationAsync(
            new KeyOperationAuditEntry
            {
                OperationType = KeyOperationType.TenantRotate,
                TenantId = status.TenantId,
                PartyId = "tenant-key-rotation",
                KeyVersion = keyVersion,
                Timestamp = status.CompletedAt ?? DateTimeOffset.UtcNow,
                CorrelationId = FirstNonBlank(status.CorrelationId) ?? UniqueIdHelper.GenerateSortableUniqueStringId(),
                OperationId = status.OperationId,
                Outcome = status.Phase.ToString(),
                ProcessedCount = status.ProcessedCount,
                SkippedCount = status.SkippedCount,
                FailedCount = status.FailedCount,
                FailureCategory = SelectDominantFailureCategory(status.FailureCategories),
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static TenantKeyRotationFailureCategory? SelectDominantFailureCategory(
        IReadOnlyDictionary<TenantKeyRotationFailureCategory, int> failureCategories)
    {
        TenantKeyRotationFailureCategory? dominant = null;
        int max = 0;
        foreach (KeyValuePair<TenantKeyRotationFailureCategory, int> kvp in failureCategories)
        {
            if (kvp.Key == TenantKeyRotationFailureCategory.None || kvp.Value <= 0)
            {
                continue;
            }

            if (kvp.Value > max)
            {
                max = kvp.Value;
                dominant = kvp.Key;
            }
        }

        return dominant;
    }

    private static TenantKeyRotationStatus CreateStartedStatus(TenantKeyRotationRequest request, string correlationId, int totalCount)
        => new()
        {
            TenantId = request.TenantId,
            OperationId = request.OperationId,
            Phase = TenantKeyRotationPhase.InProgress,
            TotalCount = totalCount,
            ProcessedCount = 0,
            SkippedCount = 0,
            FailedCount = 0,
            FailureCategories = new Dictionary<TenantKeyRotationFailureCategory, int>(),
            StartedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        };

    private static TenantKeyRotationStatus CreateFailureStatus(
        TenantKeyRotationRequest request,
        string correlationId,
        TenantKeyRotationFailureCategory category,
        int totalCount,
        int processedCount,
        int skippedCount,
        int failedCount)
        => new()
        {
            TenantId = request.TenantId,
            OperationId = request.OperationId,
            Phase = TenantKeyRotationPhase.Failed,
            TotalCount = totalCount,
            ProcessedCount = processedCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            FailureCategories = new Dictionary<TenantKeyRotationFailureCategory, int> { [category] = failedCount },
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        };

    private static IReadOnlyDictionary<TenantKeyRotationFailureCategory, int> IncrementFailure(
        IReadOnlyDictionary<TenantKeyRotationFailureCategory, int> existing,
        TenantKeyRotationFailureCategory category)
    {
        Dictionary<TenantKeyRotationFailureCategory, int> updated = new(existing);
        updated.TryGetValue(category, out int count);
        updated[category] = count + 1;
        return updated;
    }

    private static TenantKeyRotationFailureCategory NormalizeFailureCategory(Exception ex, bool tenantKeyProviderStage)
    {
        if (tenantKeyProviderStage)
        {
            return TenantKeyRotationFailureCategory.MissingKeyProvider;
        }

        return ex is TenantKeyRotationProgressConflictException
            ? TenantKeyRotationFailureCategory.ConcurrencyConflict
            : TenantKeyRotationFailureCategory.BackendUnavailable;
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                return values[i];
            }
        }

        return null;
    }

    private static string BuildRecordKey(PartyKeyRecord record) => $"{record.TenantId}:{record.PartyId}:v{record.Version}";

    private static string BuildProgressKey(string tenantId, string operationId) => $"{tenantId}:tenant-key-rotation:{operationId}";
}
