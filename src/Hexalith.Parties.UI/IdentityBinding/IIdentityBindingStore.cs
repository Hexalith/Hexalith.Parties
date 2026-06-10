namespace Hexalith.Parties.UI.IdentityBinding;

public interface IIdentityBindingStore
{
    Task<IdentityBindingRecord?> GetAsync(
        IdentityBindingKey key,
        CancellationToken cancellationToken = default);

    Task<IdentityBindingRecord> CreateAsync(
        IdentityBindingRecord binding,
        CancellationToken cancellationToken = default);

    Task<IdentityBindingRecord> ReplaceAsync(
        IdentityBindingRecord binding,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IdentityBindingKey key,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}
