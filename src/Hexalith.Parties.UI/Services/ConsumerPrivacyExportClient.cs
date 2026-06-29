using System.Text.Json;

using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.UI.Services;

internal sealed class ConsumerPrivacyExportClient(ISelfScopedPartiesClient selfScopedPartiesClient) : IConsumerPrivacyExportClient
{
    private const string JsonContentType = "application/json";

    private static readonly JsonSerializerOptions JsonOptions = PartiesJsonOptions.Default;

    public async Task<ConsumerPrivacyExportResult> ExportMyDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AdminPortalExportDownload download = await selfScopedPartiesClient
                .ExportMyDataAsync(cancellationToken)
                .ConfigureAwait(false);

            if (download.Payload.Length == 0)
            {
                return ConsumerPrivacyExportResult.Failure(ConsumerPrivacyExportOutcome.TransientFailure);
            }

            PartyDataPortabilityPackage? package = JsonSerializer.Deserialize<PartyDataPortabilityPackage>(download.Payload, JsonOptions);
            ConsumerPrivacyExportStatus status = MapStatus(package?.Status);
            ConsumerPrivacyExportOutcome outcome = MapOutcome(status);
            if (outcome is not ConsumerPrivacyExportOutcome.Ready
                and not ConsumerPrivacyExportOutcome.Restricted
                and not ConsumerPrivacyExportOutcome.Erased
                and not ConsumerPrivacyExportOutcome.Unavailable)
            {
                return ConsumerPrivacyExportResult.Failure(outcome);
            }

            return new ConsumerPrivacyExportResult(
                outcome,
                BuildSafeFileName(DateTimeOffset.UtcNow),
                JsonContentType,
                download.Payload,
                status,
                package?.Freshness);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ConsumerPrivacyExportResult.Failure(ConsumerPrivacyExportOutcome.TransientFailure);
        }
    }

    private static string BuildSafeFileName(DateTimeOffset timestamp)
        => $"my-data-export-{timestamp.UtcDateTime:yyyyMMddHHmmss}Z.json";

    private static ConsumerPrivacyExportStatus MapStatus(string? status)
        => status switch
        {
            "Exported" => ConsumerPrivacyExportStatus.Exported,
            "RestrictedExported" => ConsumerPrivacyExportStatus.RestrictedExported,
            "Erased" => ConsumerPrivacyExportStatus.Erased,
            "PersonalDataUnavailable" => ConsumerPrivacyExportStatus.PersonalDataUnavailable,
            _ => ConsumerPrivacyExportStatus.Unknown,
        };

    private static ConsumerPrivacyExportOutcome MapOutcome(ConsumerPrivacyExportStatus status)
        => status switch
        {
            ConsumerPrivacyExportStatus.Exported => ConsumerPrivacyExportOutcome.Ready,
            ConsumerPrivacyExportStatus.RestrictedExported => ConsumerPrivacyExportOutcome.Restricted,
            ConsumerPrivacyExportStatus.Erased => ConsumerPrivacyExportOutcome.Erased,
            ConsumerPrivacyExportStatus.PersonalDataUnavailable => ConsumerPrivacyExportOutcome.Unavailable,
            _ => ConsumerPrivacyExportOutcome.TransientFailure,
        };
}
