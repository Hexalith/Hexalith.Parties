using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security.Tests;

internal sealed class InMemoryPartyErasureRecordStore : IPartyErasureRecordStore
{
    private readonly Dictionary<string, ErasureCertificate> _certificates = new(StringComparer.Ordinal);

    private readonly Dictionary<string, ErasureVerificationReport> _reports = new(StringComparer.Ordinal);

    private readonly Dictionary<string, PartyErasureStatusRecord> _statuses = new(StringComparer.Ordinal);

    public Task SaveStatusAsync(PartyErasureStatusRecord status, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);
        _statuses[BuildKey(status.TenantId, status.PartyId)] = status;
        return Task.CompletedTask;
    }

    public Task<PartyErasureStatusRecord?> GetStatusAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken = default)
    {
        _statuses.TryGetValue(BuildKey(tenantId, partyId), out PartyErasureStatusRecord? status);
        return Task.FromResult(status);
    }

    public Task SaveCertificateAsync(ErasureCertificate certificate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        _certificates[BuildKey(certificate.TenantId, certificate.PartyId)] = certificate;
        return Task.CompletedTask;
    }

    public Task<ErasureCertificate?> GetCertificateAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken = default)
    {
        _certificates.TryGetValue(BuildKey(tenantId, partyId), out ErasureCertificate? certificate);
        return Task.FromResult(certificate);
    }

    public Task SaveVerificationReportAsync(ErasureVerificationReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        _reports[BuildKey(report.TenantId, report.PartyId)] = report;
        return Task.CompletedTask;
    }

    public Task<ErasureVerificationReport?> GetVerificationReportAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken = default)
    {
        _reports.TryGetValue(BuildKey(tenantId, partyId), out ErasureVerificationReport? report);
        return Task.FromResult(report);
    }

    private static string BuildKey(string tenantId, string partyId) => $"{tenantId}/{partyId}";
}
