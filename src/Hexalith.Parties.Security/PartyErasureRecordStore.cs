using Dapr.Client;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class PartyErasureRecordStore(DaprClient daprClient) : IPartyErasureRecordStore {
    private const string StoreName = "statestore";

    public Task SaveStatusAsync(PartyErasureStatusRecord status, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(status);
        return daprClient.SaveStateAsync(StoreName, BuildStatusKey(status.TenantId, status.PartyId), status, cancellationToken: cancellationToken);
    }

    public async Task<PartyErasureStatusRecord?> GetStatusAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<PartyErasureStatusRecord>(
            StoreName,
            BuildStatusKey(tenantId, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public Task SaveCertificateAsync(ErasureCertificate certificate, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(certificate);
        return daprClient.SaveStateAsync(StoreName, BuildCertificateKey(certificate.TenantId, certificate.PartyId), certificate, cancellationToken: cancellationToken);
    }

    public async Task<ErasureCertificate?> GetCertificateAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<ErasureCertificate>(
            StoreName,
            BuildCertificateKey(tenantId, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public Task SaveVerificationReportAsync(ErasureVerificationReport report, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(report);
        return daprClient.SaveStateAsync(StoreName, BuildReportKey(report.TenantId, report.PartyId), report, cancellationToken: cancellationToken);
    }

    public async Task<ErasureVerificationReport?> GetVerificationReportAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<ErasureVerificationReport>(
            StoreName,
            BuildReportKey(tenantId, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

    private static string BuildCertificateKey(string tenantId, string partyId) => $"{tenantId}:erasure:{partyId}";

    private static string BuildReportKey(string tenantId, string partyId) => $"{tenantId}:erasure-report:{partyId}";

    private static string BuildStatusKey(string tenantId, string partyId) => $"{tenantId}:erasure-status:{partyId}";
}
