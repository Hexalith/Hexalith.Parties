namespace Hexalith.Parties.UI.IdentityBinding;

public interface IIdentityProviderPartyAttributeClient
{
    Task SetPartyIdAsync(
        IdentityBindingKey key,
        string partyId,
        CancellationToken cancellationToken = default);

    Task ClearPartyIdAsync(
        IdentityBindingKey key,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPartyIdsAsync(
        IdentityBindingKey key,
        CancellationToken cancellationToken = default);
}
