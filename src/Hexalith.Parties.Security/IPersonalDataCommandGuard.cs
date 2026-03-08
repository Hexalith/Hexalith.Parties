namespace Hexalith.Parties.Security;

/// <summary>
/// Determines whether a party command that writes personal data may proceed.
/// </summary>
public interface IPersonalDataCommandGuard
{
    /// <summary>
    /// Returns a blocking reason when the command must be rejected, otherwise <see langword="null"/>.
    /// </summary>
    Task<string?> GetBlockingReasonAsync(string tenantId, string partyId, object command, CancellationToken cancellationToken = default);
}