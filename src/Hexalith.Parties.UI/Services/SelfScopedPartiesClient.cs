using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Authentication;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The fail-closed, Scoped implementation of <see cref="ISelfScopedPartiesClient"/> (Story 1.5,
/// AR-D3 / ADR-030). Every call resolves the current consumer's <strong>own</strong> bound
/// <c>party_id</c> (fail-closed via <see cref="PartyIdClaimResolver"/>) and injects <em>that</em> id
/// into the underlying gateway client — a caller can never supply a <c>party_id</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Principal source.</strong> Uses <see cref="AuthenticationStateProvider"/>, the Blazor-correct
/// per-circuit principal source — <strong>not</strong> <c>IHttpContextAccessor</c>, whose
/// <c>HttpContext</c> is null once the interactive circuit is live.
/// </para>
/// <para>
/// <strong>Lifetime.</strong> Registered <strong>Scoped</strong> (per circuit) by
/// <see cref="SelfScopedPartiesClientServiceCollectionExtensions.AddSelfScopedPartiesClient"/>. Never
/// Singleton: the resolver and auth-state are per-request/per-circuit, and the host boots with
/// <c>ValidateScopes=true</c>, which fails closed if a Singleton captures this Scoped accessor.
/// </para>
/// <para>
/// <strong>PII hygiene.</strong> Never logs or surfaces the resolved <c>party_id</c>, tenant, consent
/// ids, or any claim/party value. The fail-closed exception message is deliberately generic.
/// </para>
/// </remarks>
internal sealed class SelfScopedPartiesClient(
    AuthenticationStateProvider authStateProvider,
    PartyIdClaimResolver resolver,
    IPartiesQueryClient queryClient,
    IPartiesCommandClient commandClient,
    IAdminPortalGdprClient gdprClient) : ISelfScopedPartiesClient
{
    public async Task<PartyDetail> GetMyPartyAsync(CancellationToken ct = default)
        => await queryClient.GetPartyAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<PartiesCommandResult<PartyDetail>> UpdateMyProfileAsync(
        SelfScopedProfileUpdateRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string resolvedId = await ResolveMyPartyIdAsync().ConfigureAwait(false);
        var command = new UpdatePartyComposite
        {
            PartyId = resolvedId,
            PersonDetails = request.PersonDetails,
            OrganizationDetails = request.OrganizationDetails,
        };

        return await commandClient.UpdatePartyCompositeWithResultAsync(resolvedId, command, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConsentRecord>> GetMyConsentAsync(CancellationToken ct = default)
        => await gdprClient.GetConsentAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<AdminPortalGdprCommandResult> GrantMyConsentAsync(string channelId, string purpose, LawfulBasis lawfulBasis, CancellationToken ct = default)
        => await gdprClient.AddConsentAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), channelId, purpose, lawfulBasis, ct).ConfigureAwait(false);

    public async Task<AdminPortalGdprCommandResult> RevokeMyConsentAsync(string consentId, CancellationToken ct = default)
        => await gdprClient.RevokeConsentAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), consentId, ct).ConfigureAwait(false);

    public async Task<AdminPortalGdprCommandResult> RequestMyErasureAsync(CancellationToken ct = default)
        => await gdprClient.RequestErasureAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<AdminPortalGdprCommandResult> CancelMyErasureAsync(CancellationToken ct = default)
        => await gdprClient.CancelErasureAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<PartyErasureStatusRecord?> GetMyErasureStatusAsync(CancellationToken ct = default)
        => await gdprClient.GetErasureStatusAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<AdminPortalGdprCommandResult> RestrictMyProcessingAsync(string? reason, CancellationToken ct = default)
        => await gdprClient.RestrictProcessingAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), reason, ct).ConfigureAwait(false);

    public async Task<AdminPortalGdprCommandResult> LiftMyRestrictionAsync(CancellationToken ct = default)
        => await gdprClient.LiftRestrictionAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<AdminPortalExportDownload> ExportMyDataAsync(CancellationToken ct = default)
        => await gdprClient.ExportPartyDataAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<ProcessingActivityRecord>> GetMyProcessingRecordsAsync(CancellationToken ct = default)
        => await gdprClient.GetProcessingRecordsAsync(await ResolveMyPartyIdAsync().ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>
    /// Resolves the current principal's single bound <c>party_id</c>, <strong>fail-closed</strong>.
    /// Throws (and never touches the underlying client) when the principal is unbound or ambiguously
    /// bound — the accessor never falls back to an arbitrary or caller-supplied id.
    /// </summary>
    /// <returns>The resolved bound <c>party_id</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The current principal has no single bound party (no / ambiguous <c>party_id</c> binding).
    /// </exception>
    private async Task<string> ResolveMyPartyIdAsync()
    {
        AuthenticationState state = await authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        PartyBindingResult binding = resolver.Resolve(state.User);
        return binding.IsBound
            ? binding.PartyId!
            : throw new InvalidOperationException("No bound party for the current principal."); // fail closed — no PII
    }
}

/// <summary>
/// DI registration for the Story 1.5 consumer self-scope accessor.
/// </summary>
public static class SelfScopedPartiesClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISelfScopedPartiesClient"/> → <see cref="SelfScopedPartiesClient"/> as
    /// <strong>Scoped</strong> (ADR-030). Never Singleton: the accessor captures the per-circuit
    /// <see cref="AuthenticationStateProvider"/> and Scoped <see cref="PartyIdClaimResolver"/>, and the
    /// host boots with <c>ValidateScopes=true</c> — a Singleton capture would fail the boot. Call
    /// unconditionally (mirroring <c>AddPartiesUiAuthorization</c>/<c>AddPartiesUiClaimsResolution</c>)
    /// so tests and degraded boot can compose it; its gateway-client dependencies are resolved lazily,
    /// only when a consumer page actually consumes the accessor.
    /// </summary>
    /// <param name="services">The service collection to register the accessor into.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSelfScopedPartiesClient(this IServiceCollection services)
    {
        services.AddScoped<ISelfScopedPartiesClient, SelfScopedPartiesClient>();
        return services;
    }
}
