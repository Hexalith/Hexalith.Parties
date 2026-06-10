using System.Net.Http;

using Hexalith.Parties.Client;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.UI.Services;

internal sealed class ConsumerPrivacyProcessingClient(ISelfScopedPartiesClient selfScopedPartiesClient) : IConsumerPrivacyProcessingClient
{
    private const int MaxSummaryLength = 180;
    private static readonly string[] UnsafeSummaryFragments =
    [
        "partyId",
        "tenantId",
        "actorId",
        "correlationId",
        "payload",
        "processingRecords",
        "ProblemDetails",
        "exception",
        "stackTrace",
        "bearer",
        "token",
        "claim",
        "displayName",
        "contact value",
        "identifier value",
        "restriction reason",
        "access_token",
        "refresh_token",
    ];

    public async Task<ConsumerPrivacyProcessingResult> GetMyProcessingSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<ProcessingActivityRecord> records = await selfScopedPartiesClient
                .GetMyProcessingRecordsAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<ConsumerPrivacyProcessingRecord> safeRecords = records
                .OrderByDescending(static record => record.Timestamp)
                .ThenByDescending(static record => record.SequenceNumber)
                .Select(static record => new ConsumerPrivacyProcessingRecord(
                    MapCategory(record.OperationCategory),
                    MapOutcome(record.Outcome),
                    record.Timestamp,
                    BoundedSummary(record)))
                .ToList();

            return ConsumerPrivacyProcessingResult.FromRecords(safeRecords);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ConsumerPrivacyProcessingResult.Failure(ConsumerPrivacyProcessingOutcome.Forbidden);
        }
        catch (PartiesClientException exception)
        {
            return ConsumerPrivacyProcessingResult.Failure(MapFailureStatus(exception.Status));
        }
        catch (HttpRequestException)
        {
            return ConsumerPrivacyProcessingResult.Failure(ConsumerPrivacyProcessingOutcome.TransientFailure);
        }
        catch (TimeoutException)
        {
            return ConsumerPrivacyProcessingResult.Failure(ConsumerPrivacyProcessingOutcome.TransientFailure);
        }
        catch (TaskCanceledException)
        {
            return ConsumerPrivacyProcessingResult.Failure(ConsumerPrivacyProcessingOutcome.TransientFailure);
        }
        catch
        {
            return ConsumerPrivacyProcessingResult.Failure(ConsumerPrivacyProcessingOutcome.TransientFailure);
        }
    }

    private static ConsumerPrivacyProcessingCategory MapCategory(string? value)
        => value?.Trim() switch
        {
            "Read" or "Query" => ConsumerPrivacyProcessingCategory.DataRead,
            "Write" or "Command" or "Update" => ConsumerPrivacyProcessingCategory.DataChanged,
            "Export" => ConsumerPrivacyProcessingCategory.DataExport,
            "Consent" => ConsumerPrivacyProcessingCategory.Consent,
            "Erasure" or "Delete" => ConsumerPrivacyProcessingCategory.Erasure,
            "Restriction" => ConsumerPrivacyProcessingCategory.Restriction,
            "DomainEvent" or "Activity" => ConsumerPrivacyProcessingCategory.Activity,
            "" or null => ConsumerPrivacyProcessingCategory.Unknown,
            _ => ConsumerPrivacyProcessingCategory.Unknown,
        };

    private static ConsumerPrivacyProcessingRecordOutcome MapOutcome(string? value)
        => value?.Trim() switch
        {
            "Completed" or "Succeeded" or "Success" => ConsumerPrivacyProcessingRecordOutcome.Completed,
            "Accepted" or "Pending" => ConsumerPrivacyProcessingRecordOutcome.Accepted,
            "Restricted" or "Limited" => ConsumerPrivacyProcessingRecordOutcome.Limited,
            "Failed" or "Rejected" or "Error" => ConsumerPrivacyProcessingRecordOutcome.Failed,
            "" or null => ConsumerPrivacyProcessingRecordOutcome.Unknown,
            _ => ConsumerPrivacyProcessingRecordOutcome.Unknown,
        };

    private static ConsumerPrivacyProcessingOutcome MapFailureStatus(int status)
        => status switch
        {
            401 => ConsumerPrivacyProcessingOutcome.AuthenticationRequired,
            403 => ConsumerPrivacyProcessingOutcome.Forbidden,
            404 => ConsumerPrivacyProcessingOutcome.Unavailable,
            410 => ConsumerPrivacyProcessingOutcome.Erased,
            501 or 503 => ConsumerPrivacyProcessingOutcome.Unavailable,
            408 or 429 or >= 500 => ConsumerPrivacyProcessingOutcome.TransientFailure,
            _ => ConsumerPrivacyProcessingOutcome.TransientFailure,
        };

    private static string BoundedSummary(ProcessingActivityRecord record)
    {
        string normalized = string.Join(
            ' ',
            (record.Summary ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length == 0 || ContainsUnsafeSummaryContent(normalized, record))
        {
            return string.Empty;
        }

        return normalized.Length <= MaxSummaryLength
            ? normalized
            : normalized[..MaxSummaryLength];
    }

    private static bool ContainsUnsafeSummaryContent(string summary, ProcessingActivityRecord record)
        => LooksLikeRawStructuredContent(summary)
            || UnsafeSummaryFragments.Any(fragment => summary.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            || ContainsValue(summary, record.PartyId)
            || ContainsValue(summary, record.TenantId)
            || ContainsValue(summary, record.ActorId)
            || ContainsValue(summary, record.CorrelationId)
            || ContainsValue(summary, record.EventType);

    private static bool LooksLikeRawStructuredContent(string summary)
        => summary.StartsWith('{')
            || summary.StartsWith('[')
            || summary.Contains("\":", StringComparison.Ordinal);

    private static bool ContainsValue(string summary, string? value)
        => !string.IsNullOrWhiteSpace(value)
            && summary.Contains(value, StringComparison.OrdinalIgnoreCase);
}
