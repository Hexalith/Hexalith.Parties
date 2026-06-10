namespace Hexalith.Parties.UI.IdentityBinding;

public interface IIdentityBindingProvisioningService
{
    Task<IdentityBindingOperationResult> LinkAsync(
        CreateIdentityBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityBindingOperationResult> RotateAsync(
        RotateIdentityBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityBindingOperationResult> SuspendAsync(
        ChangeIdentityBindingStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityBindingOperationResult> RemoveAsync(
        ChangeIdentityBindingStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityBindingOperationResult> ReconcileAsync(
        ReconcileIdentityBindingRequest request,
        CancellationToken cancellationToken = default);
}
