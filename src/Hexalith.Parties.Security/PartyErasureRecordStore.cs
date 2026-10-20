using Microsoft.Extensions.Options;

using Dapr.Client;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class PartyErasureRecordStore(DaprClient daprClient, IOptions<PartiesSecurityStoreOptions>? options = null) : IPartyErasureRecordStore {
    /// <summary>Creates an erasure store with the compatible default component.</summary>
    /// <param name="daprClient">The Dapr client.</param>
    public PartyErasureRecordStore(DaprClient daprClient) : this(daprClient, null)
    {
    }

    private readonly string _storeName = (options?.Value ?? new PartiesSecurityStoreOptions()).StateStoreName;

    public Task SaveStatusAsync(PartyErasureStatusRecord status, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(status);
        return daprClient.SaveStateAsync(_storeName, BuildStatusKey(status.TenantId, status.PartyId), status, cancellationToken: cancellationToken);
    }

    public async Task<PartyErasureStatusRecord?> GetStatusAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<PartyErasureStatusRecord>(
            _storeName,
            BuildStatusKey(tenantId, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public Task SaveCertificateAsync(ErasureCertificate certificate, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(certificate);
        return daprClient.SaveStateAsync(_storeName, BuildCertificateKey(certificate.TenantId, certificate.PartyId), certificate, cancellationToken: cancellationToken);
    }

    public async Task<ErasureCertificate?> GetCertificateAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<ErasureCertificate>(
            _storeName,
            BuildCertificateKey(tenantId, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public Task SaveVerificationReportAsync(ErasureVerificationReport report, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(report);
        return daprClient.SaveStateAsync(_storeName, BuildReportKey(report.TenantId, report.PartyId), report, cancellationToken: cancellationToken);
    }

    public async Task<ErasureVerificationReport?> GetVerificationReportAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<ErasureVerificationReport>(
            _storeName,
            BuildReportKey(tenantId, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

    private static string BuildCertificateKey(string tenantId, string partyId) => $"{tenantId}:erasure:{partyId}";

    private static string BuildReportKey(string tenantId, string partyId) => $"{tenantId}:erasure-report:{partyId}";

    private static string BuildStatusKey(string tenantId, string partyId) => $"{tenantId}:erasure-status:{partyId}";
}
