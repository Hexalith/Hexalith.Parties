using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The <strong>single</strong> data path a Consumer principal uses against the EventStore gateway
/// (Story 1.5, AR-D3). It is the architectural <em>own-data-only</em> choke point: every method
/// resolves and injects the consumer's own bound <c>party_id</c> (via
/// <see cref="Hexalith.Parties.UI.Authentication.PartyIdClaimResolver"/>) and issues
/// <strong>only self-scoped</strong> operations.
/// </summary>
/// <remarks>
/// <para>
/// The interface is tripwire-by-construction: <strong>no method takes a <c>partyId</c> parameter</strong>
/// (the accessor injects the resolved id, so a caller can never supply an arbitrary one) and it exposes
/// <strong>zero</strong> list/search-shaped members — no member named <c>List*</c>/<c>Search*</c>, and no
/// member returning <see cref="PagedResult{T}"/>. A reflection test pins this so a Consumer principal can
/// never reach <c>ListPartiesAsync</c>/<c>SearchPartiesAsync</c> (AC1, AC6).
/// </para>
/// <para>
/// Surface scope is read + GDPR self-service only. Profile-WRITE
/// (<c>UpdatePersonDetails…</c>) is Epic 4 / FR-Consumer-2 (Story 4.5) and is intentionally absent —
/// it lands with the page that needs it.
/// </para>
/// </remarks>
public interface ISelfScopedPartiesClient
{
    /// <summary>Reads the current consumer's own party detail (→ <c>GetPartyAsync(myPartyId)</c>).</summary>
    Task<PartyDetail> GetMyPartyAsync(CancellationToken ct = default);

    /// <summary>Lists the current consumer's own consent records.</summary>
    Task<IReadOnlyList<ConsentRecord>> GetMyConsentAsync(CancellationToken ct = default);

    /// <summary>Grants a consent for the current consumer, with an honest lawful basis.</summary>
    Task<AdminPortalGdprCommandResult> GrantMyConsentAsync(string channelId, string purpose, LawfulBasis lawfulBasis, CancellationToken ct = default);

    /// <summary>Revokes one of the current consumer's own consents.</summary>
    Task<AdminPortalGdprCommandResult> RevokeMyConsentAsync(string consentId, CancellationToken ct = default);

    /// <summary>Requests erasure of the current consumer's own data (GDPR Art. 17).</summary>
    Task<AdminPortalGdprCommandResult> RequestMyErasureAsync(CancellationToken ct = default);

    /// <summary>Reads the current consumer's own erasure status.</summary>
    Task<PartyErasureStatusRecord?> GetMyErasureStatusAsync(CancellationToken ct = default);

    /// <summary>Restricts processing of the current consumer's own data (GDPR Art. 18).</summary>
    Task<AdminPortalGdprCommandResult> RestrictMyProcessingAsync(string? reason, CancellationToken ct = default);

    /// <summary>Lifts a processing restriction on the current consumer's own data.</summary>
    Task<AdminPortalGdprCommandResult> LiftMyRestrictionAsync(CancellationToken ct = default);

    /// <summary>Exports the current consumer's own data (GDPR Art. 20 portability).</summary>
    Task<AdminPortalExportDownload> ExportMyDataAsync(CancellationToken ct = default);

    /// <summary>Reads the processing-activity records for the current consumer's own data (GDPR Art. 30).</summary>
    Task<IReadOnlyList<ProcessingActivityRecord>> GetMyProcessingRecordsAsync(CancellationToken ct = default);

    // NO partyId params. NO List*/Search*. NO PagedResult<> returns. (AC1 + AC6 tripwire.)
}
