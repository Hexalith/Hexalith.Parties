namespace Hexalith.Parties.Contracts.Security;

public interface IPartyErasureRecordStore {
    Task SaveStatusAsync(PartyErasureStatusRecord status, CancellationToken cancellationToken = default);

    Task<PartyErasureStatusRecord?> GetStatusAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task SaveCertificateAsync(ErasureCertificate certificate, CancellationToken cancellationToken = default);

    Task<ErasureCertificate?> GetCertificateAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task SaveVerificationReportAsync(ErasureVerificationReport report, CancellationToken cancellationToken = default);

    Task<ErasureVerificationReport?> GetVerificationReportAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
