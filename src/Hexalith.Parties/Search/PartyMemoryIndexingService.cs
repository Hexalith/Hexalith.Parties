using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

/// <summary>Reserves durable ingestion obligations and records only validated terminal unit identities.</summary>
internal sealed partial class PartyMemoryIndexingService(
    MemoriesClient memoriesClient,
    IPartyMemoryUnitMappingStore mappingStore,
    IOptionsMonitor<PartyMemorySearchOptions> options,
    ILogger<PartyMemoryIndexingService> logger,
    IPartyMemoryIngestionIntentStore intentStore)
{
    internal static bool SupportsExplicitSourceRefresh =>
#if HEXALITH_MEMORIES_SOURCE_REFRESH
        true;
#else
        false;
#endif

    /// <summary>Submits a new revision or reconciles its previously acknowledged workflow on redelivery.</summary>
    public async Task<PartyMemoryIndexingResult?> IndexAsync(
        PartyIndexEntry entry,
        PartyMemoryUnitMappingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);
        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(entry, context);
        if (unit is null)
        {
            return null;
        }

#if HEXALITH_MEMORIES_SOURCE_REFRESH
        if (context.SequenceNumber is null or < 0)
        {
            return new(unit.PartyId, unit.SourceUri, null, Indexed: false,
                FailureReason: "Canonical projection revision is unavailable; retry canonical redelivery before refresh admission.");
        }
#endif
        byte[] content = Encoding.UTF8.GetBytes(unit.Content);
        // Canonical redelivery deserializes the projection and loses its JsonIgnore search
        // collections. A retained canonical sequence identifies the application independently
        // of delivery-only rebuild/reconciliation markers and timestamps. Preserve the legacy
        // context identity for callers without a canonical sequence.
        byte[] revision = context.SequenceNumber is { } sequence
            ? JsonSerializer.SerializeToUtf8Bytes(new { Entry = entry, unit.TenantId, unit.CaseId, unit.PartyId, SequenceNumber = sequence })
            : JsonSerializer.SerializeToUtf8Bytes(new { Entry = entry, Context = context });
        string identity = Convert.ToHexStringLower(SHA256.HashData(revision));
        var intent = new PartyMemoryIngestionIntent(identity, unit.CaseId, unit.SourceUri);
#if HEXALITH_MEMORIES_SOURCE_REFRESH
        intent = intent with
        {
            ProducerAuthority = PartyMemoryCleanupService.BuildProducerAuthority(memoriesClient.BaseAddress),
            RefreshProtocolVersion = SourceRefreshProtocol.Version,
            SourceRevisionNumber = context.SequenceNumber.Value,
            RefreshIdentityDigest = SourceRefreshProtocol.CreateIdentityDigest(unit.SourceUri, "text/plain", context.SequenceNumber.Value, unit.Metadata, content),
        };
#endif
        bool reserved = false;
        try
        {
            PartyMemoryIngestionRegistration registration = await intentStore.RegisterAsync(
                unit.TenantId, unit.PartyId, intent, cancellationToken).ConfigureAwait(false);
            reserved = true;
            intent = registration.Intent;
            if (registration.Created)
            {
                string workflow;
                try
                {
#if HEXALITH_MEMORIES_SOURCE_REFRESH
                    if (intent.RefreshIdentityDigest != SourceRefreshProtocol.CreateIdentityDigest(unit.SourceUri, "text/plain", context.SequenceNumber!.Value, unit.Metadata, content))
                    {
                        throw new InvalidOperationException("memory-ingestion-original-refresh-body-unavailable");
                    }
                    string? predecessor = await memoriesClient.LookupMemoryUnitIdBySourceUriAsync(unit.TenantId, intent.CaseId, intent.SourceUri, cancellationToken).ConfigureAwait(false);
                    string? reopenVersion = null;
                    if (intent.Generation > 0)
                    {
                        SourceInventoryPage inventory = await memoriesClient.GetSourceInventoryAsync(unit.TenantId, intent.SourceUri, ct: cancellationToken).ConfigureAwait(false);
                        if (inventory.AdmissionClosed && !inventory.Permanent) { reopenVersion = inventory.Version; }
                    }
                    workflow = await memoriesClient.RefreshAsync(unit.TenantId, intent.CaseId, intent.SourceUri, content, "text/plain", "Hexalith.Parties",
                        unit.Metadata, intent.AttemptId, predecessor, context.SequenceNumber!.Value, cancellationToken, reopenVersion).ConfigureAwait(false);
#else
                    workflow = await memoriesClient.IngestAsync(unit.TenantId, unit.CaseId, unit.SourceUri,
                        content, "text/plain", "Hexalith.Parties", unit.Metadata, intent.AttemptId, cancellationToken).ConfigureAwait(false);
#endif
                }
                catch (MemoriesRemoteException ex) when (PartyMemoryIngestionEvidence.IsDefiniteSubmissionRejection(ex))
                {
                    // Only verified submission preflight rejections prove that no producer
                    // was admitted. Status-read failures never reopen submission.
                    intent = intent with { SubmissionRejected = true };
                    using var rejectedBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await intentStore.UpdateAsync(unit.TenantId, unit.PartyId, intent, rejectedBudget.Token).ConfigureAwait(false);
                    throw;
                }
                if (string.IsNullOrWhiteSpace(workflow))
                {
                    LogFailure(logger, "empty-workflow");
                    return Failed(unit, intent, "Memories returned no workflow identity; durable obligation retained.");
                }

                intent = intent with { WorkflowInstanceId = workflow };
                await intentStore.UpdateAsync(unit.TenantId, unit.PartyId, intent, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(intent.WorkflowInstanceId))
            {
#if HEXALITH_MEMORIES_SOURCE_REFRESH
                intent = await RecoverAdmissionAsync(unit, intent, context.SequenceNumber!.Value, cancellationToken).ConfigureAwait(false);
#endif
                if (string.IsNullOrWhiteSpace(intent.WorkflowInstanceId))
                {
                    return Failed(unit, intent, "Ingestion acceptance is unresolved; durable obligation retained.");
                }
            }

            intent = await ResolveAsync(unit, intent, cancellationToken).ConfigureAwait(false);
            if (intent.MemoryUnitId is null)
            {
                return Failed(unit, intent, "Ingestion has no validated terminal output; retry projection delivery.");
            }

            if (!await PublishAsync(unit, intent, cancellationToken).ConfigureAwait(false))
            {
                return Failed(unit, intent, "Cleanup closed this ingestion generation; retry canonical projection reconciliation.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new(unit.PartyId, unit.SourceUri, intent.WorkflowInstanceId, Indexed: true, FailureReason: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (reserved)
            {
                // The caller's budget cannot abandon an acknowledged remote producer. Attempt
                // bounded persistence/reconciliation independently, then preserve cancellation.
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await RecoverAsync(unit, intent, recovery.Token).ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            LogFailure(logger, ex.GetType().Name);
            if (reserved && intent.WorkflowInstanceId is not null)
            {
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await RecoverAsync(unit, intent, recovery.Token).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Failed(unit, intent, $"Indexing failed ({ex.GetType().Name}); durable recovery is required.");
        }
    }

#if HEXALITH_MEMORIES_SOURCE_REFRESH
    /// <summary>Recovers only the exact persisted retry identity from bounded authoritative source inventory.</summary>
    private async Task<PartyMemoryIngestionIntent> RecoverAdmissionAsync(PartyMemoryUnit unit, PartyMemoryIngestionIntent intent, long revision, CancellationToken cancellationToken)
    {
        if (intent.ProducerAuthority != PartyMemoryCleanupService.BuildProducerAuthority(memoriesClient.BaseAddress))
        {
            throw new InvalidOperationException("memory-ingestion-producer-authority-mismatch");
        }
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(intent.AttemptId)));
        if (intent.RefreshProtocolVersion != SourceRefreshProtocol.Version || intent.SourceRevisionNumber != revision
            || string.IsNullOrWhiteSpace(intent.RefreshIdentityDigest))
        {
            throw new InvalidOperationException("memory-ingestion-original-refresh-proof-unavailable");
        }
        string bodyBinding = intent.RefreshIdentityDigest;
        string? cursor = null;
        string? version = null;
        SourceInventoryEntry? match = null;
        int count = 0;
        HashSet<string> cursors = new(StringComparer.Ordinal);
        for (int pageNumber = 0; pageNumber < 11; pageNumber++)
        {
            SourceInventoryPage page = await memoriesClient.GetSourceInventoryAsync(unit.TenantId, intent.SourceUri, cursor, 100, cancellationToken).ConfigureAwait(false);
            if (page.TenantId != unit.TenantId || page.SourceUri != intent.SourceUri || string.IsNullOrWhiteSpace(page.Version)
                || (version is not null && version != page.Version) || page.Entries.Count > 100
                || (count += page.Entries.Count) > PartyMemoryIngestionIntentStore.Capacity)
            {
                throw new InvalidOperationException("memory-ingestion-inventory-scope-or-bound-mismatch");
            }
            version = page.Version;
            foreach (SourceInventoryEntry entry in page.Entries.Where(item => item.IdempotencyTokenHash == digest))
            {
                if (entry.CaseId != intent.CaseId || string.IsNullOrWhiteSpace(entry.WorkflowInstanceId) || match is not null
                    || entry.RefreshProtocolVersion != SourceRefreshProtocol.Version || entry.SourceRevisionNumber != revision
                    || entry.RefreshIdentityDigest != bodyBinding)
                {
                    throw new InvalidOperationException("memory-ingestion-inventory-retry-identity-mismatch");
                }
                match = entry;
            }
            cursor = page.NextCursor;
            if (cursor is null)
            {
                if (match is not null)
                {
                    intent = intent with { WorkflowInstanceId = match.WorkflowInstanceId };
                    await intentStore.UpdateAsync(unit.TenantId, unit.PartyId, intent, cancellationToken).ConfigureAwait(false);
                }
                return intent;
            }
            if (cursor.Length > 8192 || page.Entries.Count == 0 || !cursors.Add(cursor))
            {
                throw new InvalidOperationException("memory-ingestion-inventory-continuation-invalid");
            }
        }
        throw new InvalidOperationException("memory-ingestion-inventory-page-limit");
    }
#endif

    private async Task<PartyMemoryIngestionIntent> ResolveAsync(PartyMemoryUnit unit, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
    {
        if (intent.MemoryUnitId is not null)
        {
            return intent;
        }

        IngestionWorkflowStatus status = await memoriesClient.GetIngestionWorkflowStatusAsync(intent.WorkflowInstanceId!, cancellationToken).ConfigureAwait(false);
        string? canonical = PartyMemoryIngestionEvidence.Resolve(status, intent.WorkflowInstanceId!, unit.TenantId, intent.CaseId);
        if (canonical is null)
        {
#if HEXALITH_MEMORIES_SOURCE_REFRESH
            if (PartyMemoryIngestionEvidence.IsSuperseded(status, intent.WorkflowInstanceId!, unit.TenantId, intent.CaseId))
            {
                string? predecessor = await memoriesClient.LookupMemoryUnitIdBySourceUriAsync(unit.TenantId, intent.CaseId, intent.SourceUri, cancellationToken).ConfigureAwait(false);
                await intentStore.RegisterSupersededSuccessorAsync(unit.TenantId, unit.PartyId, intent, status.MemoryUnitId!, predecessor, cancellationToken).ConfigureAwait(false);
            }
#endif
            return intent;
        }

        intent = intent with { MemoryUnitId = canonical };
        await intentStore.UpdateAsync(unit.TenantId, unit.PartyId, intent, cancellationToken).ConfigureAwait(false);
        return intent;
    }

    private async Task RecoverAsync(PartyMemoryUnit unit, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
    {
        try
        {
            await intentStore.UpdateAsync(unit.TenantId, unit.PartyId, intent, cancellationToken).ConfigureAwait(false);
            if (intent.WorkflowInstanceId is null)
            {
                return;
            }

            intent = await ResolveAsync(unit, intent, cancellationToken).ConfigureAwait(false);
            if (intent.MemoryUnitId is null)
            {
                return;
            }

            _ = await PublishAsync(unit, intent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            LogRecoveryFailure(logger, ex.GetType().Name);
        }
    }

    /// <summary>Resumes live projection delivery only after canonical evidence authorizes a new generation.</summary>
    internal Task<bool> TryResumeLiveProjectionAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        => intentStore.TryReopenAsync(tenantId, partyId, cancellationToken);

    private async Task<bool> PublishAsync(PartyMemoryUnit unit, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
    {
        if (!await intentStore.CanPublishAsync(unit.TenantId, unit.PartyId, intent, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await mappingStore.RecordMappingAsync(unit.TenantId, unit.PartyId, intent.MemoryUnitId!,
            intent.SourceUri, intent.CaseId, cancellationToken).ConfigureAwait(false);
        // Cleanup may finish while mapping persistence is suspended. Its durable receipt keeps
        // canonical provenance even across process death, and this writer cannot claim indexed.
        return await intentStore.CanPublishAsync(unit.TenantId, unit.PartyId, intent, cancellationToken).ConfigureAwait(false);
    }

    private static PartyMemoryIndexingResult Failed(PartyMemoryUnit unit, PartyMemoryIngestionIntent intent, string reason)
        => new(unit.PartyId, unit.SourceUri, intent.WorkflowInstanceId, Indexed: false, FailureReason: reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories indexing failed with {ExceptionType}; the delivery remains retryable.")]
    private static partial void LogFailure(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories independent recovery failed with {ExceptionType}; durable obligations must be retried.")]
    private static partial void LogRecoveryFailure(ILogger logger, string exceptionType);
}
