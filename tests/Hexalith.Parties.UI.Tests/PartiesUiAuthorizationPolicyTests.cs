using System.Security.Claims;

using Hexalith.Parties.UI.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.3 AC3 — the rigorous role→policy mapping test, independent of bUnit's faked authorization.
/// Builds a provider with <see cref="PartiesUiAuthorization.AddPartiesUiAuthorization"/> under
/// ValidateScopes=true (ADR-030 parity with the host), resolves the real
/// <see cref="IAuthorizationService"/>, and evaluates the <c>Admin</c>/<c>Consumer</c> policies against
/// <see cref="ClaimsPrincipal"/>s carrying <see cref="ClaimTypes.Role"/> claims. Asserts Admin and
/// TenantOwner satisfy <c>Admin</c> only, Consumer satisfies <c>Consumer</c> only, and a role-less
/// principal satisfies neither (fail-closed).
/// </summary>
public sealed class PartiesUiAuthorizationPolicyTests
{
    [Fact]
    public async Task AdminRolePrincipal_SatisfiesAdminPolicyOnly()
    {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal admin = PrincipalWithRole("Admin");

        (await authz.AuthorizeAsync(admin, null, PartiesUiAuthorization.AdminPolicy)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(admin, null, PartiesUiAuthorization.ConsumerPolicy)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task TenantOwnerRolePrincipal_SatisfiesAdminPolicyOnly()
    {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal owner = PrincipalWithRole("TenantOwner");

        (await authz.AuthorizeAsync(owner, null, PartiesUiAuthorization.AdminPolicy)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(owner, null, PartiesUiAuthorization.ConsumerPolicy)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ConsumerRolePrincipal_SatisfiesConsumerPolicyOnly()
    {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal consumer = PrincipalWithRole("Consumer");

        (await authz.AuthorizeAsync(consumer, null, PartiesUiAuthorization.ConsumerPolicy)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(consumer, null, PartiesUiAuthorization.AdminPolicy)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task RoleLessPrincipal_SatisfiesNeitherPolicy()
    {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal anonymous = new(new ClaimsIdentity(authenticationType: "Test"));

        (await authz.AuthorizeAsync(anonymous, null, PartiesUiAuthorization.AdminPolicy)).Succeeded.ShouldBeFalse();
        (await authz.AuthorizeAsync(anonymous, null, PartiesUiAuthorization.ConsumerPolicy)).Succeeded.ShouldBeFalse();
    }

    // RequireRole is case-SENSITIVE (ordinal), which is exactly why the role arrays enumerate explicit
    // case variants ("Admin","admin","Administrator","administrator","TenantOwner","tenantowner"). These
    // data-driven theories lock the contract: EVERY declared name actually satisfies its own policy and
    // never the other — so trimming a variant from the single source of truth fails the build, not a login.

    [Theory]
    [MemberData(nameof(AdminRoleNameCases))]
    public async Task EveryDeclaredAdminRoleName_SatisfiesAdminPolicyOnly(string roleName)
    {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal principal = PrincipalWithRole(roleName);

        (await authz.AuthorizeAsync(principal, null, PartiesUiAuthorization.AdminPolicy)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(principal, null, PartiesUiAuthorization.ConsumerPolicy)).Succeeded.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(ConsumerRoleNameCases))]
    public async Task EveryDeclaredConsumerRoleName_SatisfiesConsumerPolicyOnly(string roleName)
    {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal principal = PrincipalWithRole(roleName);

        (await authz.AuthorizeAsync(principal, null, PartiesUiAuthorization.ConsumerPolicy)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(principal, null, PartiesUiAuthorization.AdminPolicy)).Succeeded.ShouldBeFalse();
    }

    public static TheoryData<string> AdminRoleNameCases()
    {
        var data = new TheoryData<string>();
        foreach (string role in PartiesUiAuthorization.AdminRoleNames)
        {
            data.Add(role);
        }

        return data;
    }

    public static TheoryData<string> ConsumerRoleNameCases()
    {
        var data = new TheoryData<string>();
        foreach (string role in PartiesUiAuthorization.ConsumerRoleNames)
        {
            data.Add(role);
        }

        return data;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPartiesUiAuthorization();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ClaimsPrincipal PrincipalWithRole(string role)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "Test"));
}
