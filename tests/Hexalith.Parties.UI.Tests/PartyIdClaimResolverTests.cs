using System.Security.Claims;

using Hexalith.Parties.UI.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.4 AC1/AC2/AC3/AC4 — the fail-closed party-binding resolution test, independent of bUnit.
/// Builds a provider with <see cref="PartiesUiAuthorization.AddPartiesUiClaimsResolution"/> under
/// <c>ValidateScopes=true</c> (ADR-030 parity with the host) and resolves the Scoped
/// <see cref="PartyIdClaimResolver"/> <strong>inside a scope</strong>, then evaluates the resolver
/// against principals carrying zero / one / empty / multiple <c>party_id</c> claims. Proves: a single
/// valid claim binds (with tenant captured), and every other shape fails closed to
/// <see cref="PartyBindingResult.Unbound"/> — including the ambiguous two-claim case (AC3).
/// </summary>
public sealed class PartyIdClaimResolverTests
{
    [Fact]
    public void PresentSinglePartyId_ResolvesBound_WithPartyIdAndTenant()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        PartyIdClaimResolver resolver = scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>();

        ClaimsPrincipal user = Principal(
            new Claim(PartiesUiAuthorization.TenantClaimType, "tenant-a"),
            new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-123"));

        PartyBindingResult result = resolver.Resolve(user);

        result.IsBound.ShouldBeTrue();
        result.PartyId.ShouldBe("party-123");
        result.Tenant.ShouldBe("tenant-a");
    }

    [Fact]
    public void AbsentPartyId_ResolvesUnbound()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        PartyIdClaimResolver resolver = scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>();

        ClaimsPrincipal user = Principal(new Claim(PartiesUiAuthorization.TenantClaimType, "tenant-a"));

        resolver.Resolve(user).IsBound.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespacePartyId_ResolvesUnbound(string partyId)
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        PartyIdClaimResolver resolver = scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>();

        ClaimsPrincipal user = Principal(new Claim(PartiesUiAuthorization.PartyIdClaimType, partyId));

        resolver.Resolve(user).IsBound.ShouldBeFalse();
    }

    [Fact]
    public void MultiplePartyIds_ResolvesUnbound_AmbiguousBindingFailsClosed()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        PartyIdClaimResolver resolver = scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>();

        ClaimsPrincipal user = Principal(
            new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-1"),
            new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-2"));

        resolver.Resolve(user).IsBound.ShouldBeFalse();
    }

    [Fact]
    public void BoundPartyId_WithoutTenantClaim_BindsWithEmptyTenant()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        PartyIdClaimResolver resolver = scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>();

        ClaimsPrincipal user = Principal(new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-123"));

        PartyBindingResult result = resolver.Resolve(user);

        result.IsBound.ShouldBeTrue();
        result.PartyId.ShouldBe("party-123");
        result.Tenant.ShouldBe(string.Empty);
    }

    [Fact]
    public void BothClaimResolutionServices_ResolveUnderValidateScopes()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IClaimsTransformation>()
            .ShouldBeOfType<PartiesClaimsTransformation>();
    }

    [Fact]
    public void AddPartiesUiClaimsResolution_RegistersResolverAndTransformationAsScoped()
    {
        // ADR-030 / AC4: both services MUST be Scoped, never Singleton. ValidateScopes cannot catch a
        // Singleton here — neither service captures a scoped dependency — so the registered lifetime is
        // pinned directly; otherwise a Singleton regression would slip silently past boot-time validation.
        var services = new ServiceCollection();
        services.AddPartiesUiClaimsResolution();

        services.ShouldContain(d =>
            d.ServiceType == typeof(PartyIdClaimResolver) && d.Lifetime == ServiceLifetime.Scoped);
        services.ShouldContain(d =>
            d.ServiceType == typeof(IClaimsTransformation)
            && d.ImplementationType == typeof(PartiesClaimsTransformation)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Resolve_NullUser_ThrowsArgumentNullException()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        PartyIdClaimResolver resolver = scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>();

        Should.Throw<ArgumentNullException>(() => resolver.Resolve(null!));
    }

    [Fact]
    public async Task NormalizedTenantFromTransformation_IsCapturedIntoBoundScope()
    {
        // AC1 end-to-end: a principal carrying a raw `tid` token (no eventstore:tenant yet) plus a single
        // party_id is normalized by PartiesClaimsTransformation, then PartyIdClaimResolver captures the
        // normalized tenant alongside the party — proving the effective scope {tenant, party_id} is the
        // composed result of the transformation and the resolver, not just each in isolation.
        ClaimsPrincipal principal = Principal(
            new Claim("tid", "tenant-z"),
            new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-789"));

        var transformation = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);
        ClaimsPrincipal normalized = await transformation.TransformAsync(principal);

        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        PartyIdClaimResolver resolver = scope.ServiceProvider.GetRequiredService<PartyIdClaimResolver>();

        PartyBindingResult result = resolver.Resolve(normalized);

        result.IsBound.ShouldBeTrue();
        result.PartyId.ShouldBe("party-789");
        result.Tenant.ShouldBe("tenant-z");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPartiesUiClaimsResolution();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
