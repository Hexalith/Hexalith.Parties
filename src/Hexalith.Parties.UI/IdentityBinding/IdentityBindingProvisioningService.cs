using Hexalith.Parties.AdminPortal.Services;

namespace Hexalith.Parties.UI.IdentityBinding;

public sealed class IdentityBindingProvisioningService(
    IIdentityBindingStore store,
    IIdentityProviderPartyAttributeClient idpClient,
    IAdminPortalAuthorizationService authorizationService,
    TimeProvider timeProvider) : IIdentityBindingProvisioningService
{
    private const int MaxBoundedAuditValueLength = 128;

    public async Task<IdentityBindingOperationResult> LinkAsync(
        CreateIdentityBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityBindingOperationResult? rejected = await ValidateOperatorAndRequestAsync(
            request.OperatorSubject,
            request.VerificationReference,
            request.ReasonCode,
            cancellationToken).ConfigureAwait(false);
        if (rejected is not null)
        {
            return rejected;
        }

        IdentityBindingKey key = CreateKey(request.Tenant, request.IdpIssuer, request.IdpSubject);
        IdentityBindingRecord? existing = await store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing?.Status == IdentityBindingStatus.Active)
        {
            return IdentityBindingOperationResult.Failure("DuplicateActiveBinding");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        IdentityBindingRecord binding = CreateBinding(
            key,
            request.PartyId,
            request.OperatorSubject,
            request.VerificationReference,
            request.ReasonCode,
            now,
            existing?.Version + 1 ?? 1,
            existing);

        try
        {
            binding = existing is null
                ? await store.CreateAsync(binding, cancellationToken).ConfigureAwait(false)
                : await store.ReplaceAsync(binding, existing.Version, cancellationToken).ConfigureAwait(false);
        }
        catch (IdentityBindingStoreConflictException exception)
        {
            return IdentityBindingOperationResult.Failure(exception.Code);
        }

        IdentityBindingOperationResult? idpFailure = await TryApplyIdpUpdateAndRollbackAsync(
            binding,
            existing,
            ct => idpClient.SetPartyIdAsync(key, request.PartyId, ct),
            cancellationToken).ConfigureAwait(false);
        if (idpFailure is not null)
        {
            return idpFailure;
        }

        return IdentityBindingOperationResult.Success("Linked", binding);
    }

    public async Task<IdentityBindingOperationResult> RotateAsync(
        RotateIdentityBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityBindingOperationResult? rejected = await ValidateOperatorAndRequestAsync(
            request.OperatorSubject,
            request.VerificationReference,
            request.ReasonCode,
            cancellationToken).ConfigureAwait(false);
        if (rejected is not null)
        {
            return rejected;
        }

        IdentityBindingKey key = CreateKey(request.Tenant, request.IdpIssuer, request.IdpSubject);
        IdentityBindingRecord? current = await store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Status != IdentityBindingStatus.Active)
        {
            return IdentityBindingOperationResult.Failure("NoActiveBinding");
        }

        if (current.Version != request.ExpectedVersion)
        {
            return IdentityBindingOperationResult.Failure("VersionConflict");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        IdentityBindingRecord updated = AppendAudit(
            current with
            {
                PartyId = request.NewPartyId,
                UpdatedByOperator = request.OperatorSubject,
                UpdatedAtUtc = now,
                VerificationReference = request.VerificationReference,
                ReasonCode = request.ReasonCode,
                Version = current.Version + 1,
            },
            "Rotated",
            request.NewPartyId,
            request.OperatorSubject,
            request.VerificationReference,
            request.ReasonCode,
            now);

        try
        {
            updated = await store.ReplaceAsync(updated, request.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (IdentityBindingStoreConflictException exception)
        {
            return IdentityBindingOperationResult.Failure(exception.Code);
        }

        IdentityBindingOperationResult? idpFailure = await TryApplyIdpUpdateAndRollbackAsync(
            updated,
            current,
            ct => idpClient.SetPartyIdAsync(key, request.NewPartyId, ct),
            cancellationToken).ConfigureAwait(false);
        if (idpFailure is not null)
        {
            return idpFailure;
        }

        return IdentityBindingOperationResult.Success("Rotated", updated);
    }

    public Task<IdentityBindingOperationResult> SuspendAsync(
        ChangeIdentityBindingStatusRequest request,
        CancellationToken cancellationToken = default)
        => ChangeStatusAsync(request, IdentityBindingStatus.Suspended, "Suspended", cancellationToken);

    public Task<IdentityBindingOperationResult> RemoveAsync(
        ChangeIdentityBindingStatusRequest request,
        CancellationToken cancellationToken = default)
        => ChangeStatusAsync(request, IdentityBindingStatus.Removed, "Removed", cancellationToken);

    public async Task<IdentityBindingOperationResult> ReconcileAsync(
        ReconcileIdentityBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityBindingOperationResult? rejected = await ValidateOperatorAuthorizationAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rejected is not null)
        {
            return rejected;
        }

        IdentityBindingKey key = CreateKey(request.Tenant, request.IdpIssuer, request.IdpSubject);
        IdentityBindingRecord? binding = await store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> idpPartyIds = await idpClient.GetPartyIdsAsync(key, cancellationToken).ConfigureAwait(false);

        bool hasExactlyOneNonEmptyIdpPartyId = idpPartyIds.Count == 1 && !string.IsNullOrWhiteSpace(idpPartyIds[0]);
        bool storeHasActiveParty = binding?.Status == IdentityBindingStatus.Active
            && !string.IsNullOrWhiteSpace(binding.PartyId);
        bool inSync = storeHasActiveParty
            && hasExactlyOneNonEmptyIdpPartyId
            && string.Equals(binding!.PartyId, idpPartyIds[0], StringComparison.Ordinal);

        bool removedOrSuspendedWithoutAttribute = binding is not null
            && binding.Status != IdentityBindingStatus.Active
            && idpPartyIds.Count == 0;
        bool missingEverywhere = binding is null && idpPartyIds.Count == 0;

        IdentityBindingDriftReport drift = new(
            key,
            HasDrift: !(inSync || removedOrSuspendedWithoutAttribute || missingEverywhere),
            StoreStatus: binding?.Status.ToString() ?? "Missing",
            IdpAttributeShape: AttributeShape(idpPartyIds));

        return IdentityBindingOperationResult.Reconciled(drift, binding);
    }

    private async Task<IdentityBindingOperationResult> ChangeStatusAsync(
        ChangeIdentityBindingStatusRequest request,
        IdentityBindingStatus status,
        string action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityBindingOperationResult? rejected = await ValidateOperatorAndRequestAsync(
            request.OperatorSubject,
            request.VerificationReference,
            request.ReasonCode,
            cancellationToken).ConfigureAwait(false);
        if (rejected is not null)
        {
            return rejected;
        }

        IdentityBindingKey key = CreateKey(request.Tenant, request.IdpIssuer, request.IdpSubject);
        IdentityBindingRecord? current = await store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Status != IdentityBindingStatus.Active)
        {
            return IdentityBindingOperationResult.Failure("NoActiveBinding");
        }

        if (current.Version != request.ExpectedVersion)
        {
            return IdentityBindingOperationResult.Failure("VersionConflict");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        IdentityBindingRecord updated = AppendAudit(
            current with
            {
                PartyId = null,
                Status = status,
                UpdatedByOperator = request.OperatorSubject,
                UpdatedAtUtc = now,
                VerificationReference = request.VerificationReference,
                ReasonCode = request.ReasonCode,
                Version = current.Version + 1,
            },
            action,
            null,
            request.OperatorSubject,
            request.VerificationReference,
            request.ReasonCode,
            now);

        try
        {
            updated = await store.ReplaceAsync(updated, request.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (IdentityBindingStoreConflictException exception)
        {
            return IdentityBindingOperationResult.Failure(exception.Code);
        }

        IdentityBindingOperationResult? idpFailure = await TryApplyIdpUpdateAndRollbackAsync(
            updated,
            current,
            ct => idpClient.ClearPartyIdAsync(key, ct),
            cancellationToken).ConfigureAwait(false);
        if (idpFailure is not null)
        {
            return idpFailure;
        }

        return IdentityBindingOperationResult.Success(action, updated);
    }

    private async Task<IdentityBindingOperationResult?> ValidateOperatorAndRequestAsync(
        string operatorSubject,
        string verificationReference,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        IdentityBindingOperationResult? rejected = await ValidateOperatorAuthorizationAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rejected is not null)
        {
            return rejected;
        }

        return HasBoundedAuditValue(operatorSubject)
            && HasBoundedAuditValue(verificationReference)
            && HasBoundedAuditValue(reasonCode)
                ? null
                : IdentityBindingOperationResult.Failure("InvalidAuditMetadata");
    }

    private async Task<IdentityBindingOperationResult?> ValidateOperatorAuthorizationAsync(
        CancellationToken cancellationToken)
    {
        AdminPortalAuthorizationState state = await authorizationService
            .GetAuthorizationStateAsync(cancellationToken)
            .ConfigureAwait(false);
        return state.IsAuthenticated && state.HasTenantContext && state.IsAdmin
            ? null
            : IdentityBindingOperationResult.Failure("UnauthorizedOperator");
    }

    private async Task<IdentityBindingOperationResult?> TryApplyIdpUpdateAndRollbackAsync(
        IdentityBindingRecord committed,
        IdentityBindingRecord? previous,
        Func<CancellationToken, Task> idpUpdate,
        CancellationToken cancellationToken)
    {
        try
        {
            await idpUpdate(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RollBackStoreAsync(committed, previous, cancellationToken).ConfigureAwait(false);
            return IdentityBindingOperationResult.Failure("IdpAttributeUpdateFailed");
        }
    }

    private async Task RollBackStoreAsync(
        IdentityBindingRecord committed,
        IdentityBindingRecord? previous,
        CancellationToken cancellationToken)
    {
        try
        {
            if (previous is null)
            {
                await store.DeleteAsync(committed.Key, committed.Version, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await store.ReplaceAsync(previous, committed.Version, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (IdentityBindingStoreConflictException)
        {
            // A concurrent operator changed the record after the failed IdP write; leave the newer
            // version intact and let reconciliation surface the drift.
        }
    }

    private static IdentityBindingKey CreateKey(string tenant, string idpIssuer, string idpSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idpIssuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(idpSubject);

        return new(tenant, idpIssuer, idpSubject);
    }

    private static IdentityBindingRecord CreateBinding(
        IdentityBindingKey key,
        string partyId,
        string operatorSubject,
        string verificationReference,
        string reasonCode,
        DateTimeOffset now,
        long version,
        IdentityBindingRecord? existing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        IdentityBindingAuditEntry audit = new(
            now,
            operatorSubject,
            "Linked",
            partyId,
            verificationReference,
            reasonCode,
            version);

        return new(
            key,
            partyId,
            IdentityBindingStatus.Active,
            operatorSubject,
            operatorSubject,
            existing?.CreatedAtUtc ?? now,
            now,
            verificationReference,
            reasonCode,
            version,
            existing is null ? [audit] : [.. existing.AuditTrail, audit]);
    }

    private static IdentityBindingRecord AppendAudit(
        IdentityBindingRecord binding,
        string action,
        string? partyId,
        string operatorSubject,
        string verificationReference,
        string reasonCode,
        DateTimeOffset now)
    {
        IdentityBindingAuditEntry audit = new(
            now,
            operatorSubject,
            action,
            partyId,
            verificationReference,
            reasonCode,
            binding.Version);

        return binding with { AuditTrail = [.. binding.AuditTrail, audit] };
    }

    private static bool HasBoundedAuditValue(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= MaxBoundedAuditValueLength;

    private static string AttributeShape(IReadOnlyList<string> partyIds)
        => partyIds.Count switch
        {
            0 => "Missing",
            1 => string.IsNullOrWhiteSpace(partyIds[0]) ? "Empty" : "Single",
            _ => "Multiple",
        };
}
