using System.Security.Claims;

using Hexalith.Parties.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.Tests.Authorization;

/// <summary>
/// Story 1.5 AC3 — host-side analogue of the UI PartiesUiAuthorizationPolicyTests. Exercises the REAL
/// <see cref="ConsumerPolicy.Add"/> helper through a minimal AddAuthorizationCore (no submodules/config)
/// under ValidateScopes=true, alongside the existing Admin policy. Proves a Consumer-role principal
/// satisfies Consumer only, an Admin-role principal satisfies Admin only, and a role-less principal
/// satisfies neither (fail-closed).
/// </summary>
public sealed class PartiesConsumerPolicyTests {
    private const string AdminPolicyName = "Admin";

    [Fact]
    public async Task ConsumerRolePrincipalSatisfiesConsumerPolicyOnly() {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal consumer = PrincipalWithRole("Consumer");

        (await authz.AuthorizeAsync(consumer, null, ConsumerPolicy.Name)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(consumer, null, AdminPolicyName)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task AdminRolePrincipalSatisfiesAdminPolicyOnly() {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal admin = PrincipalWithRole("Admin");

        (await authz.AuthorizeAsync(admin, null, AdminPolicyName)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(admin, null, ConsumerPolicy.Name)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task RoleLessPrincipalSatisfiesNeitherPolicy() {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal anonymous = new(new ClaimsIdentity(authenticationType: "Test"));

        (await authz.AuthorizeAsync(anonymous, null, ConsumerPolicy.Name)).Succeeded.ShouldBeFalse();
        (await authz.AuthorizeAsync(anonymous, null, AdminPolicyName)).Succeeded.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(ConsumerRoleNameCases))]
    public async Task EveryDeclaredConsumerRoleNameSatisfiesConsumerPolicyOnly(string roleName) {
        using ServiceProvider provider = BuildProvider();
        IAuthorizationService authz = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal principal = PrincipalWithRole(roleName);

        (await authz.AuthorizeAsync(principal, null, ConsumerPolicy.Name)).Succeeded.ShouldBeTrue();
        (await authz.AuthorizeAsync(principal, null, AdminPolicyName)).Succeeded.ShouldBeFalse();
    }

    public static TheoryData<string> ConsumerRoleNameCases() {
        var data = new TheoryData<string>();
        foreach (string role in ConsumerPolicy.RoleNames) {
            data.Add(role);
        }

        return data;
    }

    private static ServiceProvider BuildProvider() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options => {
            ConsumerPolicy.Add(options);
            options.AddPolicy(AdminPolicyName, policy =>
                policy.RequireRole("Admin", "admin", "administrator", "Administrator"));
        });

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ClaimsPrincipal PrincipalWithRole(string role)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "Test"));
}
