using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Client.AdminPortal;

public interface IAdminPortalGdprClient
{
    Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken);

    Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken);

    Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken);

    Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken);

    Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(string partyId, string? reason, CancellationToken cancellationToken);

    Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken);

    Task<AdminPortalGdprCommandResult> AddConsentAsync(
        string partyId,
        string channelId,
        string purpose,
        LawfulBasis lawfulBasis,
        CancellationToken cancellationToken);

    Task<AdminPortalGdprCommandResult> RevokeConsentAsync(string partyId, string consentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken);

    Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken);
}
