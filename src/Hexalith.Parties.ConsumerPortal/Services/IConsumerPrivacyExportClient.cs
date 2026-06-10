namespace Hexalith.Parties.ConsumerPortal.Services;

public interface IConsumerPrivacyExportClient
{
    Task<ConsumerPrivacyExportResult> ExportMyDataAsync(CancellationToken cancellationToken = default);
}
