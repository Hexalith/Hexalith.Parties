namespace Hexalith.Parties.Contracts.Security;

public interface IErasureVerificationService
{
    Task<ErasureVerificationReport> VerifyErasureAsync(
        string tenantId,
        string partyId,
        ErasureCertificate erasureCertificate,
        CancellationToken cancellationToken = default);
}
